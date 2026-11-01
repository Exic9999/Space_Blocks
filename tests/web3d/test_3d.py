"""Real-browser test suite for mobile3d/index.html (Playwright + system chromium).
Run:  .venv/bin/python tests/web3d/test_3d.py      (from /root/PythonProjects/space_blocks)
"""
import asyncio, json, os, sys, time
from playwright.async_api import async_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "..", "mobile3d", "index.html")
BUILD = os.path.join(HERE, "_build.html")
SHOTS = os.path.join(HERE, "shots"); os.makedirs(SHOTS, exist_ok=True)
html = open(SRC, encoding="utf-8").read().replace("https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js", "three.min.js")
assert "three.min.js" in html
open(BUILD, "w", encoding="utf-8").write(html)
URL = __import__("pathlib").Path(BUILD).resolve().as_uri()
ARGS = ["--no-sandbox", "--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"]
results = []

def ok(name, cond, extra=""):
    results.append((name, bool(cond), extra))
    print(("PASS " if cond else "FAIL ") + name + (" :: " + str(extra) if extra and not cond else ""), flush=True)

async def new_page(b, w=412, h=800, touch=True):
    ctx = await b.new_context(viewport={"width": w, "height": h}, has_touch=touch, is_mobile=touch)
    pg = await ctx.new_page()
    errs = []
    pg.on("pageerror", lambda e: errs.append("pageerror: " + str(e)))
    pg.on("console", lambda m: errs.append("console.error: " + m.text) if m.type == "error" else None)
    pg.errs = errs
    await pg.goto(URL)
    return pg

async def start_new(pg):
    await pg.click("#bNew")
    await pg.wait_for_selector("#crawl", state="visible")
    await pg.click("#cSkip")
    await pg.wait_for_function("window.GAME && GAME.mode==='cmd'", timeout=30000)
    await pg.wait_for_timeout(500)

async def ev(pg, js):
    return await pg.evaluate(js)

async def cdp_touch(pg, cdp, kind, pts):
    await cdp.send("Input.dispatchTouchEvent", {"type": kind, "touchPoints": [{"x": x, "y": y, "id": i} for i, (x, y) in enumerate(pts)]})

async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch(**({"executable_path": os.environ["CHROMIUM_PATH"]} if os.environ.get("CHROMIUM_PATH") else ({"executable_path": "/snap/bin/chromium"} if os.path.exists("/snap/bin/chromium") else {})), args=ARGS)

        # ---- boot + crawl
        pg = await new_page(b)
        ok("title screen visible", await ev(pg, "getComputedStyle(document.getElementById('title')).display!=='none'"))
        ok("Continue disabled with no save", await ev(pg, "document.getElementById('bCont').disabled"))
        await pg.click("#bNew")
        await pg.wait_for_selector("#crawl", state="visible")
        ok("crawl overlay shown with prologue", "Long Drift" in await pg.inner_text("#crawl") or "LONG DRIFT" in (await pg.inner_text("#crawl")).upper())
        await pg.click("#cSkip")
        await pg.wait_for_function("window.GAME && GAME.mode==='cmd'", timeout=30000)
        await pg.wait_for_timeout(600)
        ok("game starts in command view", await ev(pg, "GAME.mode")=="cmd")
        n = await ev(pg, "GAME.G().ships.length"); ok("3 pilots at start", n == 3, n)

        # ---- chips are really tappable (stable DOM)
        chip0 = await pg.query_selector(".chip")
        await pg.wait_for_timeout(700)
        ok("chip DOM stable across frames", await chip0.evaluate("e=>e.isConnected"))
        await pg.wait_for_timeout(1500); box = await pg.locator(".chip").nth(1).bounding_box()
        await pg.touchscreen.tap(box["x"]+box["width"]/2, box["y"]+box["height"]/2)
        await pg.wait_for_timeout(300)
        ok("tapping chip selects that pilot", await ev(pg, "GAME.sel.idx")==1, await ev(pg, "GAME.sel.idx"))
        # tap-and-hold ~120ms like a real finger
        await pg.wait_for_timeout(1500); box = await pg.locator(".chip").nth(2).bounding_box()
        cdp = await pg.context.new_cdp_session(pg)
        cx, cy = box["x"]+box["width"]/2, box["y"]+box["height"]/2
        await cdp_touch(pg, cdp, "touchStart", [(cx, cy)]); await pg.wait_for_timeout(150); await cdp_touch(pg, cdp, "touchEnd", [])
        await pg.wait_for_timeout(300)
        ok("slow tap (150ms) selects chip", await ev(pg, "GAME.sel.idx")==2, await ev(pg, "GAME.sel.idx"))

        # ---- island pick by tap, LOCK ON
        await ev(pg, "(()=>{const g=GAME,i=g.G().isl[0];g.cam.follow=null;g.cam.tx=i.x;g.cam.tz=i.y;g.cam.ty=i.h;g.cam.dist=600;g.cam.el=.45})()")
        await pg.wait_for_timeout(600)
        sp = await ev(pg, "(()=>{const i=GAME.G().isl[0];return GAME.project(i.x,i.h,i.y)})()")
        await pg.touchscreen.tap(sp[0], sp[1]); await pg.wait_for_timeout(300)
        ok("tapping an island selects it (raycast)", await ev(pg, "GAME.selIsl&&GAME.selIsl.id")==0, await ev(pg, "GAME.selIsl&&GAME.selIsl.id"))
        try: await pg.wait_for_selector("#orders button[data-o=lock]", timeout=15000)
        except Exception: pass
        lock = await pg.query_selector("#orders button[data-o=lock]")
        ok("LOCK ON button appears", lock is not None)
        if lock:
            await lock.click(); await pg.wait_for_timeout(200)
            o = await ev(pg, "GAME.sel.order")
            ok("LOCK ON gives a mine order to island 0", o and o.get("type")=="mine" and o.get("isl")==0, o)
        # order buttons
        for key, want in (("hold","hold"),("scout","scout"),("auto","auto")):
            await pg.click(f"#orders button[data-o={key}]"); await pg.wait_for_timeout(120)
            o = await ev(pg, "GAME.sel.order"); ok(f"order button {key}", o and o.get("type")==want, o)

        # ---- orbit drag (touch) and pinch zoom via CDP
        a0 = await ev(pg, "GAME.cam.az"); d0 = await ev(pg, "GAME.cam.dist")
        await cdp_touch(pg, cdp, "touchStart", [(200, 300)])
        for k in range(1, 8): await cdp_touch(pg, cdp, "touchMove", [(200+k*15, 300)]); await pg.wait_for_timeout(30)
        await cdp_touch(pg, cdp, "touchEnd", [])
        ok("one-finger drag orbits camera", abs(await ev(pg, "GAME.cam.az")-a0)>.2)
        await cdp_touch(pg, cdp, "touchStart", [(150, 300), (260, 300)])
        for k in range(1, 8): await cdp_touch(pg, cdp, "touchMove", [(150-k*10, 300), (260+k*10, 300)]); await pg.wait_for_timeout(30)
        await cdp_touch(pg, cdp, "touchEnd", [])
        d1 = await ev(pg, "GAME.cam.dist"); ok("pinch-out zooms in", d1 < d0, (d0, d1))
        await pg.mouse.move(200, 300); await pg.mouse.wheel(0, 400); await pg.wait_for_timeout(100)
        ok("wheel zooms out", await ev(pg, "GAME.cam.dist") > d1)

        # ---- radar tap
        r = await (await pg.query_selector("#radar")).bounding_box()
        await pg.touchscreen.tap(r["x"]+r["width"]*0.25, r["y"]+r["height"]*0.75); await pg.wait_for_timeout(300)
        tx, tz = await ev(pg, "[GAME.cam.tx,GAME.cam.tz]")
        ok("radar tap recenters camera", abs(tx-800)<120 and abs(tz-2400)<120, (tx, tz))

        # ---- upgrade sheet via real tap
        await ev(pg, "GAME.G().aeth=500")
        await pg.click("#orders button[data-o=up]"); await pg.wait_for_selector("#sheet", state="visible")
        await pg.click("button[data-s=eng]"); await pg.wait_for_timeout(200)
        ok("buying engine upgrade works", await ev(pg, "GAME.sel.lv.eng")==1)
        await pg.click("button[data-s=hull]"); await pg.wait_for_timeout(200)
        ok("buying hull plating works", await ev(pg, "GAME.sel.lv.hull")==1)
        await pg.click("#shClose")

        # ---- walk mode with real keyboard through all three rooms
        await ev(pg, "(()=>{const G=GAME.G();G.ships.forEach(s=>{s.state='docked';s.ph='aboard';s.docked=-1e9})})()")
        await pg.click("#bView"); await pg.wait_for_timeout(300)
        ok("BRIDGE enters walk mode", await ev(pg, "GAME.mode")=="walk")
        z0 = await ev(pg, "GAME.walk.z")
        await ev(pg, "(()=>{GAME.walk.x=-2;GAME.walk.z=-22.5})()")
        await pg.keyboard.down("s")
        t0 = time.time(); z = z0
        while time.time()-t0 < 60:
            await pg.wait_for_timeout(300); z = await ev(pg, "GAME.walk.z")
            if z > 24: break
        await pg.keyboard.up("s")
        ok("real keyboard walks player out of the bridge into the corridor", z > -12, z)
        tr = await ev(pg, "(()=>{const g=GAME;g.walk.x=-2;g.walk.z=-22.5;g.walk.keys={KeyS:1};for(let i=0;i<1500;i++)g.walkStep(1/30);g.walk.keys={};return[g.walk.x,g.walk.z]})()")
        ok("full traversal bridge -> corridor -> hangar and back", tr[1] > 25, tr)
        bk = await ev(pg, "(()=>{const g=GAME;g.walk.keys={KeyW:1};for(let i=0;i<1500;i++)g.walkStep(1/30);g.walk.keys={};return[g.walk.x,g.walk.z]})()")
        ok("and back to the bridge (no dead zones)", bk[1] < -18, bk)
        await pg.screenshot(path=os.path.join(SHOTS, "walk_hangar.png"))
        # joystick touch
        await ev(pg, "(()=>{GAME.walk.x=0;GAME.walk.z=-22;GAME.walk.yaw=0})()")
        jb = await (await pg.query_selector("#joy")).bounding_box()
        jx, jy = jb["x"]+jb["width"]/2, jb["y"]+jb["height"]/2
        await cdp_touch(pg, cdp, "touchStart", [(jx, jy)]); await cdp_touch(pg, cdp, "touchMove", [(jx, jy-45)])
        zj = -22
        for _ in range(60):
            await pg.wait_for_timeout(300); zj = await ev(pg, "GAME.walk.z")
            if zj < -23.5: break
        await cdp_touch(pg, cdp, "touchEnd", [])
        ok("joystick touch moves player forward", zj < -23.5, zj)
        await pg.wait_for_timeout(300)
        m = await ev(pg, "[GAME.walk.mx,GAME.walk.mz]"); ok("joystick releases to zero", m == [0, 0], m)
        # look drag
        y0 = await ev(pg, "GAME.walk.yaw")
        await cdp_touch(pg, cdp, "touchStart", [(300, 300)])
        for k in range(1, 6): await cdp_touch(pg, cdp, "touchMove", [(300+k*20, 300)]); await pg.wait_for_timeout(30)
        await cdp_touch(pg, cdp, "touchEnd", [])
        ok("drag looks around", abs(await ev(pg, "GAME.walk.yaw")-y0) > .2)
        # enter ship near a pad
        await ev(pg, "(()=>{GAME.walk.x=-5.5;GAME.walk.z=22;GAME.walk.yaw=Math.PI/2})()")
        try: await pg.wait_for_selector("#prompts button[data-p=enter]", timeout=20000)
        except Exception: pass
        enter = await pg.query_selector("#prompts button[data-p=enter]")
        ok("ENTER prompt appears near a landed ship", enter is not None)
        if enter:
            await enter.click(); await pg.wait_for_selector("#cabinui", state="visible")
            ok("cabin shows bio", len(await pg.inner_text("#cabinCard")) > 60)
            await pg.screenshot(path=os.path.join(SHOTS, "cabin.png"))
            await pg.click("#cabBack"); await pg.wait_for_timeout(300)
            ok("LEAVE SHIP returns to walk", await ev(pg, "GAME.mode")=="walk")
        await pg.click("#prompts button[data-p=back]"); await pg.wait_for_timeout(300)
        ok("COMMAND VIEW returns to map", await ev(pg, "GAME.mode")=="cmd")
        ok("no page/console errors (interactive)", not pg.errs, pg.errs[:3])

        # ---- standing on a pad when ship lands does not trap the player
        r = await ev(pg, """(()=>{const g=GAME,G=g.G();G.ships.forEach(s=>{s.state='docked';s.docked=-1e9});g.setMode('walk');g.walk.x=0;g.walk.z=22;g.walk.keys.KeyD=1;const x0=g.walk.x;for(let i=0;i<60;i++)g.walkStep(1/30);g.walk.keys.KeyD=0;return[x0,g.walk.x]})()""")
        ok("player standing on a pad can walk off", r[1] != r[0], r)
        # walls: cannot leave the ship
        r = await ev(pg, """(()=>{const g=GAME;g.walk.x=0;g.walk.z=-22;g.walk.yaw=0;g.walk.keys={KeyW:1};for(let i=0;i<600;i++)g.walkStep(1/30);g.walk.keys={};return[g.walk.x,g.walk.z]})()""")
        ok("front window blocks walking out", r[1] > -37.5, r)
        r = await ev(pg, """(()=>{const g=GAME;g.walk.x=12;g.walk.z=-25;g.walk.yaw=0;g.walk.keys={KeyD:1};for(let i=0;i<600;i++)g.walkStep(1/30);g.walk.keys={};return[g.walk.x,g.walk.z]})()""")
        ok("side wall blocks", r[0] < 14, r)
        await pg.context.close()

        # ---- save, reload, Continue, no replayed finale for old saves
        pg = await new_page(b)
        await start_new(pg)
        await ev(pg, "(()=>{const G=GAME.G();G.delivered=300;G.aeth=999;GAME.save()})()")
        await pg.reload(); await pg.wait_for_timeout(500)
        ok("Continue enabled after save", not await ev(pg, "document.getElementById('bCont').disabled"))
        await pg.click("#bCont"); await pg.wait_for_function("window.GAME && GAME.mode==='cmd'", timeout=30000)
        ok("Continue restores progress", await ev(pg, "GAME.G().delivered")>=300 and await ev(pg, "GAME.G().aeth")>900)
        ok("Continue skips the crawl", await ev(pg, "getComputedStyle(document.getElementById('crawl')).display")=="none")
        # legacy save (no storyV, delivered past finale)
        await ev(pg, """(()=>{const G=GAME.G();G.delivered=2000;G.found=14;delete G.storyV;G.beats={};GAME.save()})()""")
        await pg.reload(); await pg.wait_for_timeout(400); await pg.click("#bCont")
        await pg.wait_for_function("window.GAME && GAME.mode==='cmd'", timeout=30000)
        await pg.wait_for_timeout(4000)
        ok("legacy save does not replay finale crawl", await ev(pg, "getComputedStyle(document.getElementById('crawl')).display")=="none")
        await pg.context.close()

        # ---- viewports
        for name, w, h, touch in (("landscape", 844, 390, True), ("small", 360, 640, True), ("desktop", 1280, 720, False)):
            pg = await new_page(b, w, h, touch)
            await start_new(pg)
            ov = await ev(pg, "document.documentElement.scrollWidth<=window.innerWidth+1")
            ok(f"viewport {name}: no horizontal overflow", ov)
            await pg.screenshot(path=os.path.join(SHOTS, f"vp_{name}.png"))
            await pg.click("#bView"); await pg.wait_for_timeout(500)
            await pg.screenshot(path=os.path.join(SHOTS, f"vp_{name}_walk.png"))
            ok(f"viewport {name}: no errors", not pg.errs, pg.errs[:3])
            await pg.context.close()

        # ---- soak with worst-case timestep, stuck detection
        pg = await new_page(b)
        await start_new(pg)
        r = await ev(pg, """(()=>{const g=GAME,G=g.G();let bad=0,maxStuck=0;const since={};
          for(let i=0;i<9000;i++){g.step(0.6);G.ships.forEach(s=>{const k=s.state+'/'+s.ph;const e=since[s.idx];if(!e||e.k!==k)since[s.idx]={k,t:G.t};else maxStuck=Math.max(maxStuck,G.t-e.t);
            if(![s.x,s.y,s.fuel,s.hull,s.cargo,s.loy,s.px,s.pz,s.alt].every(isFinite))bad++;if(s.fuel<0||s.hull<=0||s.cargo<0)bad++});
            if(i==2000){G.delivered=700;G.aeth+=4000}}
          return{bad,maxStuck,t:G.t,ships:G.ships.length,found:G.found,delivered:G.delivered,beats:Object.keys(G.beats).filter(k=>k[0]=='s').length,states:G.ships.map(s=>s.state+'/'+s.ph)}})()""")
        ok("soak dt=0.6: no NaN/negatives", r["bad"]==0, r)
        # a ship may legitimately sit on HOLD or wait for fuel; independent pilots too. Report the max
        ok("soak dt=0.6: no ship stuck > 10 game-minutes in one state", r["maxStuck"] < 600, r)
        print("   soak:", json.dumps(r))
        # dt=1/6 soak too
        r2 = await ev(pg, """(()=>{const g=GAME,G=g.G();let bad=0;for(let i=0;i<30000;i++){g.step(1/6);if(i%50==0)G.ships.forEach(s=>{if(![s.x,s.y,s.fuel,s.hull,s.cargo,s.loy,s.px,s.pz].every(isFinite))bad++})}return{bad,delivered:G.delivered,storms:G.stormsSeen}})()""")
        ok("soak dt=1/6 clean", r2["bad"]==0, r2)

        # ---- cabin memory leak
        m0 = await ev(pg, "(()=>{GAME.openCabin(GAME.G().ships[0]);GAME.setMode('cmd');const i=GAME.renderer().info.memory;return[i.geometries,i.textures]})()")
        for k in range(30): await ev(pg, f"(()=>{{const G=GAME.G();GAME.openCabin(G.ships[{k}%G.ships.length]);GAME.setMode('cabin')}})()"); await pg.wait_for_timeout(60)
        await pg.wait_for_timeout(500)
        m1 = await ev(pg, "(()=>{const i=GAME.renderer().info.memory;return[i.geometries,i.textures]})()")
        ok("30 cabin opens do not leak geometries/textures", m1[0] <= m0[0] + 400 and m1[1] <= m0[1] + 60, (m0, m1))
        await pg.screenshot(path=os.path.join(SHOTS, "cabin_after.png"))
        ok("no errors (soak/cabin)", not pg.errs, pg.errs[:3])
        await pg.context.close()
        await b.close()

    fails = [r for r in results if not r[1]]
    print(f"\n{len(results)-len(fails)}/{len(results)} passed")
    sys.exit(1 if fails else 0)

asyncio.run(main())
