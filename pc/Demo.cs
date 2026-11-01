// Scripted harness: --demo <scenario> <out.png> (screenshots) and --bench (frame-time stats).
using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    void Prep(float simSeconds)
    {
        sim.Rng = new Random(7); sim.NewGame(1234); sel = sim.G.ships[0]; selIsl = null; ResetViews(); mode = Mode.Cmd;
        foreach (var s in sim.G.ships) sim.GiveOrder(s, "auto");
        for (int i = 0; i < (int)(simSeconds / .1f); i++) { sim.Step(.1f); if (i % 600 == 0) foreach (var s in sim.G.ships) sim.GiveOrder(s, "auto"); }
        sim.Logs.Clear(); sim.Talk = null;
    }
    void Snap(string path, int frames = 45) { for (int i = 0; i < frames; i++) Frame(1f / 60); var img = Raylib.LoadImageFromScreen(); Raylib.ExportImage(img, path); Raylib.UnloadImage(img); }

    public int RunDemo(string scen, string outPath)
    {
        Init(false, 1280, 720); Prep(scen == "storm" ? 200 : 260);
        if (scen == "storm") { sim.G.nextStorm = 0; for (int i = 0; i < 700; i++) sim.Step(.1f); }
        switch (scen)
        {
            case "title": sim.NewGame(1234); ResetViews(); mode = Mode.Title; break;
            case "cmd": cam.dist = 2300; cam.el = .55f; break;
            case "follow": sel = sim.G.ships[1]; cam.follow = sel; cam.dist = 600; cam.el = .35f; for (int i = 0; i < 60; i++) { Frame(1f / 30); } break;
            case "island": { var i = sim.G.isl[0]; cam.follow = null; cam.tx = i.x; cam.ty = i.h; cam.tz = i.y; cam.dist = 420; cam.el = .3f; selIsl = i; break; }
            case "exec": cam.dist = 700; cam.el = .4f; cam.az = 2.6f; break;
            case "storm": cam.dist = 2000; cam.el = .5f; if (sim.G.storms.Count > 0) { var q = sim.G.storms[0]; cam.tx = q.x; cam.tz = q.y; cam.dist = q.r * 3.2f; } break;
            case "walk-bridge": EnterWalk(); walk.x = 2; walk.z = -26; break;
            case "walk-hangar": SetMode(Mode.Walk); walk.x = 1; walk.z = 2; walk.yaw = MathF.PI; break;
            case "stations": { SetMode(Mode.Walk); sim.G.aeth = 300; foreach (var s2 in sim.G.ships) { sim.GiveOrder(s2, "hold"); } var sp0 = sim.G.ships[0]; sp0.state = "landing"; sp0.lt = .995f; sp0.cargo = 6; sp0.fuel = 4; sp0.hull = 12; for (int i = 0; i < 3000 && sp0.ph != "fuel"; i++) sim.Step(.05f); for (int i = 0; i < 20; i++) sim.Step(.05f); walk.x = 5; walk.z = 3.5f; walk.yaw = -.78f; walk.pitch = -.05f; break; }
            case "room-quarters": SetMode(Mode.Walk); walk.x = -4.5f; walk.z = -12; walk.yaw = 1.57f; break;
            case "room-engineering": SetMode(Mode.Walk); walk.x = 4.5f; walk.z = -12; walk.yaw = -1.2f; walk.pitch = -.05f; break;
            case "room-cargo": SetMode(Mode.Walk); walk.x = -17.5f; walk.z = 5.2f; walk.yaw = 1.57f; break;
            case "cap-hangar": SetMode(Mode.Walk); walk.x = 3.5f; walk.z = 9; walk.yaw = -1.1f; walk.pitch = -.12f; break;
            case "cap-fly": { sim.G.aeth = 300; foreach (var s2 in sim.G.ships) sim.GiveOrder(s2, "hold"); sim.CapLaunch(); for (int i = 0; i < 70; i++) sim.Step(.1f); selIsl = sim.G.isl[0]; selIsl.found = true; sim.Piloted = true; sim.InThr = 1; for (int i = 0; i < 40; i++) sim.Step(.1f); sim.InThr = 0; sim.CapAutoGoto(selIsl); for (int i = 0; i < 60; i++) sim.Step(.1f); lastCapState = sim.G.cap.state; mode = Mode.Fly; capCamInit = false; for (int i = 0; i < 30; i++) Frame(1f / 60); break; }
            case "cap-eva": { sim.G.aeth = 300; foreach (var s2 in sim.G.ships) sim.GiveOrder(s2, "hold"); sim.CapLaunch(); for (int i = 0; i < 70; i++) sim.Step(.1f); var ti = sim.G.isl[0]; ti.found = true; sim.CapAutoGoto(ti); for (int i = 0; i < 1500 && sim.G.cap.state != "landed"; i++) sim.Step(.1f); var nd = Sim.OreNodes(ti); var cc2 = sim.G.cap; cc2.ex = nd[0].x - 4; cc2.ez = nd[0].z + 3; evaYaw = MathF.Atan2(-(nd[0].x - cc2.ex), -(nd[0].z - cc2.ez)); evaPitch = -.15f; lastCapState = cc2.state; SetMode(Mode.Eva); for (int i = 0; i < 40; i++) Frame(1f / 60); break; }
            case "walk-corridor": SetMode(Mode.Walk); walk.x = 0; walk.z = -8; walk.yaw = 0; break;
            case "cabin": OpenCabin(sim.G.ships[0]); break;
            case "sail": { sim.G.aeth = 400; sim.SailTo(sim.G.exx + 500, sim.G.exy - 700); for (int i = 0; i < 700; i++) sim.Step(.1f); cam.dist = 900; cam.el = .5f; cam.az = 1.2f; selExec = true; execFollow = true; for (int i = 0; i < 50; i++) Frame(1f / 30); break; }
            case "talk": sim.ShipTalk(sim.G.ships[1], "Give me a bigger tank and I'll haul you a mountain.", 60); cam.dist = 1800; break;
            case "sheet": showSheet = true; sim.G.aeth = 400; break;
            case "crawl": StartCrawl(Data.Prologue, null); crawlT = 14; break;
            default: Console.WriteLine("unknown scenario"); return 2;
        }
        Snap(outPath, 45); Raylib.CloseWindow(); Console.WriteLine("wrote " + outPath); return 0;
    }

    public int RunBench(int frames = 600)
    {
        Init(false, 1920, 1080); Prep(300);
        var times = new List<double>(); var simMs = new List<double>(); var sw = new Stopwatch();
        void Phase(string name, Action setup)
        {
            setup(); times.Clear(); simMs.Clear();
            for (int i = 0; i < 30; i++) Frame(1f / 60);
            var wall = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                if (name.StartsWith("cmd")) { cam.az += .004f; }
                if (name.StartsWith("walk")) { walk.yaw += .004f; }
                sw.Restart(); sim.Step(1f / 60); simMs.Add(sw.Elapsed.TotalMilliseconds);
                sw.Restart(); Frame(1f / 60); double ft = sw.Elapsed.TotalMilliseconds; times.Add(ft); if (ft > 15 && Environment.GetEnvironmentVariable("SB_SPIKES") != null) Console.WriteLine($"   spike frame {i}: {ft:F1} ms");
            }
            { var sync = Raylib.LoadImageFromScreen(); Raylib.UnloadImage(sync); } double wallMs = wall.Elapsed.TotalMilliseconds;
            times.Sort(); simMs.Sort();
            Console.WriteLine($"{name,-14} WALL {wallMs / frames:F2} ms/frame = {1000.0 * frames / wallMs:F0} fps | cpu frame avg {times.Average():F2} ms  p50 {times[times.Count / 2]:F2}  p99 {times[(int)(times.Count * .99)]:F2}  max {times[^1]:F2}  | sim avg {simMs.Average():F3} ms  => {1000 / times.Average():F0} fps");
        }
        Phase("cmd wide", () => { SetMode(Mode.Cmd); cam.follow = null; cam.dist = 3500; cam.el = .6f; cam.tx = sim.G.exx; cam.tz = sim.G.exy; });
        Phase("cmd close", () => { cam.dist = 500; cam.tx = sim.G.exx; cam.tz = sim.G.exy + 100; });
        Phase("walk hangar", () => { SetMode(Mode.Walk); walk.x = 0; walk.z = 20; walk.yaw = 0; });
        Phase("walk bridge", () => { walk.x = 2; walk.z = -26; walk.yaw = 0; });
        var proc = Process.GetCurrentProcess(); Console.WriteLine($"working set {proc.WorkingSet64 / 1048576} MB, gen0 GCs {GC.CollectionCount(0)}, gen2 GCs {GC.CollectionCount(2)}");
        Raylib.CloseWindow(); return 0;
    }
}

public sealed unsafe partial class Game
{
    public int RunBoxTest()
    {
        Init(false, 640, 400);
        var mb = new MeshBuilder(); mb.Box(2, 2, 2, 0, 0, 0, Col.Hex(0xff8040)); mb.Sphere(1, 3, 0, 0, Col.Hex(0x40ff80));
        var mesh = mb.Build(); Console.WriteLine($"mesh verts {mesh.VertexCount} tris {mesh.TriangleCount} vao {mesh.VaoId}");
        double nr = double.Parse(Environment.GetEnvironmentVariable("NR") ?? "0.5"), fr = double.Parse(Environment.GetEnvironmentVariable("FR") ?? "500"); float off = float.Parse(Environment.GetEnvironmentVariable("OFF") ?? "0"); var cam = new Camera3D(new Vector3(8 + off, 6, 8 + off), new Vector3(1 + off, 0, off), Vector3.UnitY, 55, CameraProjection.Perspective);
        for (int f = 0; f < 5; f++)
        {
            Raylib.BeginDrawing(); Raylib.ClearBackground(Color.DarkGray);
            Rlgl.SetClipPlanes(nr, fr); Raylib.BeginMode3D(cam);
            Raylib.DrawCube(new Vector3(-3 + off, 0, off), 1, 1, 1, Color.Red);
            lit.SetExterior(cam.Position); lit.Draw(mesh, Matrix4x4.CreateTranslation(off, 0, off));
            Raylib.EndMode3D(); Raylib.EndDrawing();
        }
        var img = Raylib.LoadImageFromScreen(); Raylib.ExportImage(img, "shots/box.png"); Raylib.CloseWindow(); return 0;
    }
}

public sealed unsafe partial class Game
{
    public int RunUiTest()
    {
        int pass = 0, fail = 0; void Ok(string n, bool c, string x = "") { if (c) pass++; else fail++; Console.WriteLine((c ? "PASS " : "FAIL ") + n + (!c && x != "" ? " :: " + x : "")); }
        Init(false, 1280, 720); Boot(); Ok("boots to title", mode == Mode.Title);
        if (File.Exists(Sim.SavePath)) File.Move(Sim.SavePath, Sim.SavePath + ".uitest-bak", true);
        try
        {
            sim.Rng = new Random(3); StartNew(false); for (int i = 0; i < 90; i++) Frame(1f / 60);
            Ok("new game starts in command view", mode == Mode.Cmd && sim.G.ships.Count == 3 && sel != null);
            // tap the Executor -> selects a docked pilot
            sel = null; cam.dist = 2200; cam.el = .55f; Frame(1f / 60);
            var exs = Raylib.GetWorldToScreen(new Vector3(sim.G.exx, 60, sim.G.exy), cam3); Tap(exs);
            Ok("tapping the Executor selects a docked pilot", sel != null && sel.state == "docked");
            // tap an island by its projected position
            var isl = sim.G.isl[0]; cam.follow = null; cam.tx = isl.x; cam.tz = isl.y; cam.ty = isl.h; cam.dist = 900; Frame(1f / 60);
            var ip = Raylib.GetWorldToScreen(new Vector3(isl.x, isl.h, isl.y), cam3); Tap(ip);
            Ok("tapping an island selects it (raycast)", selIsl == isl);
            Order("lock"); Ok("LOCK ON gives a mine order to that island", sel!.order?.type == "mine" && sel.order.isl == isl.id);
            Order("hold"); Ok("order hold", sel.order?.type == "hold"); Order("scout"); Ok("order scout", sel.order?.type == "scout"); Order("auto"); Ok("order auto", sel.order?.type == "auto");
            // upgrades via sim as the sheet would
            sim.G.aeth = 500; showSheet = true; for (int i = 0; i < 5; i++) Frame(1f / 60); Ok("upgrade sheet renders", showSheet);
            int eng = sel.lv.eng; sim.BuyShipUp(sel, "eng"); for (int i = 0; i < 3; i++) Frame(1f / 60); Ok("buying engine rebuilds ship mesh", sel.lv.eng == eng + 1 && shipVis[sel.idx].Sig.StartsWith((eng + 1).ToString())); showSheet = false;
            // walk + cabin
            EnterWalk(); for (int i = 0; i < 30; i++) Frame(1f / 60); Ok("BRIDGE enters walk mode", mode == Mode.Walk && walk.Where() == "Command Bridge");
            for (int i = 0; i < 600 && walk.z < 20; i++) { walk.Step(sim, .016f, 0, 1); if (walk.z < -20 && walk.x < 1.9f) walk.Step(sim, .016f, 1, 0); }
            Ok("walk reaches the hangar", walk.z > 15, $"z={walk.z:F1} x={walk.x:F1}");
            FindNearby(); var ns = sim.G.ships.FirstOrDefault(s => s.state == "docked"); walk.x = Data.Pads[ns!.idx][0] + (Data.Pads[ns.idx][0] <= 0 ? 4.5f : -4.5f); walk.z = Data.Pads[ns.idx][1]; FindNearby();
            Ok("ENTER prompt available near a docked ship", nearShip != null);
            OpenCabin(nearShip ?? ns); for (int i = 0; i < 20; i++) Frame(1f / 60); Ok("cabin opens", mode == Mode.Cabin && cabShip != null);
            SetMode(Mode.Walk); Ok("leave ship returns to walk", mode == Mode.Walk); SetMode(Mode.Cmd); for (int i = 0; i < 10; i++) Frame(1f / 60); Ok("command view returns", mode == Mode.Cmd);
            for (int k = 0; k < 5; k++) { OpenCabin(sim.G.ships[Math.Min(k, sim.G.ships.Count - 1)]); Frame(1f / 60); SetMode(Mode.Cmd); } Ok("cabins for all crew build without error", true);
            // ---- real clicks, injected through the same path as the mouse ----
            Inject = true; float W = Raylib.GetScreenWidth(), H = Raylib.GetScreenHeight(), sc = S;
            void ClickAt(float x, float y) { InjMouse = new Vector2(x, y); InjClick = true; InjDown = true; Frame(1f / 60); InjDown = false; InjUp = true; Frame(1f / 60); }
            StartCrawl(Data.Prologue, null); Frame(1f / 60); ClickAt(W - 100 * sc + 44 * sc, 12 * sc + 17 * sc);
            Ok("clicking SKIP ends the crawl without opening the menu", !crawlActive && !showMenu);
            sel = sim.G.ships[0]; selIsl = null; sim.GiveOrder(sel, "auto"); Frame(1f / 60);
            float obw = Math.Min(130 * sc, (W - 20 * sc - 6 * sc * 6) / 6), holdX = 10 * sc + (obw + 6 * sc) + obw / 2, holdY = H - 152 * sc + 100 * sc + 20 * sc;
            showMenu = true; Frame(1f / 60); ClickAt(holdX, holdY); Ok("open menu blocks the order buttons underneath", sel.order?.type == "auto" && showMenu);
            showMenu = false; Frame(1f / 60); ClickAt(holdX, holdY); Ok("same click works once the menu is closed", sel.order?.type == "hold");
            float chip2X = 10 * sc + 2 * (128 * sc + 6 * sc) + 64 * sc, chipY = H - 152 * sc + 8 * sc + 28 * sc; sel = sim.G.ships[0];
            showSheet = true; Frame(1f / 60); ClickAt(chip2X, chipY); Ok("open upgrade sheet blocks pilot chips underneath", sel == sim.G.ships[0] && showSheet);
            showSheet = false; Frame(1f / 60); ClickAt(chip2X, chipY); Ok("pilot chip click selects when no overlay", sel == sim.G.ships[1]);
            ClickAt(W / 2, 6 * sc + 16 * sc); Inject = false;
            cam.az = 0; cam.tx = 1600; cam.tz = 1600; Pan(1, 0, 100); Ok("W pans the view forward (away from camera)", cam.tz < 1600 && MathF.Abs(cam.tx - 1600) < .01f);
            Pan(0, 1, 100); Ok("D pans the view right", cam.tx > 1600);
            // ---- helm: select the Executor and set a course with real clicks ----
            Inject = true; ClickAt(10 * sc + 64 * sc, H - 152 * sc + 8 * sc + 28 * sc); Ok("clicking the Executor chip selects the helm", selExec);
            cam.follow = null; execFollow = false; cam.el = 1.35f; cam.dist = 2600; cam.tx = sim.G.exx; cam.tz = sim.G.exy; for (int i = 0; i < 3; i++) Frame(1f / 60);
            Vector3 tgt = default; foreach (var (ox, oy) in Enumerable.Range(0, 36).Select(q => (MathF.Cos(q * .5236f) * (500 + q / 12 * 350), MathF.Sin(q * .5236f) * (500 + q / 12 * 350)))) { var c = new Vector3(Math.Clamp(sim.G.exx + ox, 300, 2900), 0, Math.Clamp(sim.G.exy + oy, 300, 2900)); var pr = Raylib.GetWorldToScreen(c, cam3); if (pr.X > 60 && pr.X < W - 200 && pr.Y > 90 * sc && pr.Y < H - 170 * sc && sim.G.isl.All(i2 => Mx.Hypot(i2.x - c.X, i2.y - c.Z) > 400) && sim.G.ships.All(s2 => Mx.Hypot(s2.x - c.X, s2.y - c.Z) > 150)) { tgt = c; break; } }
            var tsp = Raylib.GetWorldToScreen(tgt, cam3); sim.G.aeth = 300; ClickAt(tsp.X, tsp.Y); Ok("clicking empty space sets a course", sim.G.exMoving && Mx.Hypot(sim.G.exDestX - tgt.X, sim.G.exDestY - tgt.Z) < 80, $"moving={sim.G.exMoving} dest=({sim.G.exDestX:F0},{sim.G.exDestY:F0}) tgt=({tgt.X:F0},{tgt.Z:F0})");
            for (int i = 0; i < 900; i++) Frame(1f / 30); Ok("Executor is under way and the view follows it", Mx.Hypot(sim.G.exx - 1600, sim.G.exy - 1600) > 100, $"speed={sim.G.exSpeed:F1} head={sim.G.exHead:F2} moving={sim.G.exMoving} pos=({sim.G.exx:F0},{sim.G.exy:F0}) dest=({sim.G.exDestX:F0},{sim.G.exDestY:F0}) t={sim.G.t:F0} sp={sim.Speed}");
            Order("hold"); Ok("HALT stops the course", !sim.G.exMoving); Inject = false;
            EnterWalk(); Frame(1f / 60); FindNearby(); Ok("commander chair offers the helm", nearHelm); UseHelm(); Ok("using the helm opens the map with the Executor selected", mode == Mode.Cmd && selExec);
            selExec = false; sel = sim.G.ships[0];
            // ---- captain: board in the hangar, fly, land, mine by hand, come home, deposit ----
            Inject = false; sim.G.aeth = 400; foreach (var s3 in sim.G.ships) sim.GiveOrder(s3, "hold"); sim.G.cap.fuel = Sim.CapFuelMax; SetMode(Mode.Cmd); EnterWalk(); walk.x = Data.CapPad[0] - 4.5f; walk.z = Data.CapPad[1]; Frame(1f / 60); FindNearby();
            Ok("captain's ship is parked in the hangar and can be boarded", nearCap && sim.G.cap.state == "docked");
            // crowded spawn point: a docked pilot ship in reach AND a pilot walking right past — E must still board the captain's ship, once
            sim.AddShip(3); walk.x = 5.5f; walk.z = 6; var walker0 = sim.G.ships[0]; walker0.state = "docked"; walker0.ph = "idle"; walker0.idle = 9999; walker0.loc = "seat"; walker0.px = 6; walker0.pz = 6; walker0.path.Clear(); Frame(1f / 60); FindNearby();
            Ok("crowded spawn: board has priority over cabin and chat", nearCap && nearShip != null && nearNpc != null && EAction() == "board", EAction());
            UseE(); Ok("one E press boards the captain's ship (and only that)", mode == Mode.Fly && sim.G.cap.state == "launch" && mode != Mode.Cabin);
            for (int i = 0; i < 400 && sim.G.cap.state != "fly"; i++) Frame(1f / 30); Ok("launch animation ends in free flight", sim.G.cap.state == "fly" && mode == Mode.Fly);
            sim.InThr = 0; var fi = sim.G.isl[0]; fi.found = true; selIsl = fi; sim.CapAutoGoto(fi);
            for (int i = 0; i < 3000 && sim.G.cap.state != "landed"; i++) Frame(1f / 30); Ok("autopilot lands and the view switches to on-foot", sim.G.cap.state == "landed" && mode == Mode.Eva, $"state={sim.G.cap.state} mode={mode}");
            var enodes = Sim.OreNodes(fi); sim.G.cap.ex = enodes[0].x + 1.5f; sim.G.cap.ez = enodes[0].z; fi.aeth = fi.max; float ore00 = sim.G.cap.ore, isl00 = fi.aeth;
            for (int i = 0; i < 100; i++) sim.MineTick(.05f, true); Frame(1f / 60); Ok("hand mining while standing at an ore node yields ore", sim.G.cap.ore > ore00 && fi.aeth < isl00, $"ore={sim.G.cap.ore}");
            sim.G.cap.ore = 8; sim.CapTakeoff(); for (int i = 0; i < 200 && sim.G.cap.state != "fly"; i++) Frame(1f / 30); Ok("taking off from the island returns to flight view", sim.G.cap.state == "fly" && mode == Mode.Fly);
            SetMode(Mode.Cmd); Ok("map button shows where the captain is (FLY)", BodyLabel() == "FLY"); ReturnToBody(); Ok("returning to the body re-enters flight", mode == Mode.Fly);
            sim.G.cap.fuel = Math.Max(sim.G.cap.fuel, 20); sim.CapAutoHome(); for (int i = 0; i < 4000 && sim.G.cap.state != "docked"; i++) Frame(1f / 30);
            Ok("autopilot home docks and puts the captain on foot beside his ship", sim.G.cap.state == "docked" && mode == Mode.Walk && Mx.Hypot(walk.x - Data.CapPad[0], walk.z - Data.CapPad[1]) < 6.2f, $"state={sim.G.cap.state} mode={mode}");
            walk.x = Data.StSpot["deposit"][0]; walk.z = Data.StSpot["deposit"][1]; Frame(1f / 60); FindNearby(); Ok("deposit station is in reach", nearDep); float a00 = sim.G.aeth; int tcount = 0; while (sim.G.cap.ore >= 1 && tcount++ < 400) { sim.CapDepositTick(.1f); Frame(1f / 10); }
            Ok("carried ore is deposited into the Executor's tank", sim.G.cap.ore < 1 && sim.G.aeth > a00 + 30, $"aeth +{sim.G.aeth - a00:F0}");
            walk.x = Data.StSpot["fuel"][0]; walk.z = Data.StSpot["fuel"][1]; sim.G.cap.fuel = 6; Frame(1f / 60); FindNearby(); for (int i = 0; i < 300; i++) sim.CapRefuelTick(.1f); Ok("fuel station refuels the captain's ship", nearFuelSt && sim.G.cap.fuel > 29);
            // save while on the island, continue, return to the body
            sim.G.cap.fuel = 25; sim.CapLaunch(); for (int i = 0; i < 300 && sim.G.cap.state != "fly"; i++) Frame(1f / 30); selIsl = fi; sim.CapAutoGoto(fi); for (int i = 0; i < 3000 && sim.G.cap.state != "landed"; i++) Frame(1f / 30);
            DoSave(); sim.G.cap.state = "docked"; Ok("continue restores a captain who is standing on an island", ContinueGame() && sim.G.cap.state == "landed" && mode == Mode.Cmd); ReturnToBody(); for (int i = 0; i < 10; i++) Frame(1f / 60); Ok("map button then returns him to the island on foot", mode == Mode.Eva);
            SetMode(Mode.Cmd); sel = sim.G.ships[0];
            // save / continue
            sim.G.aeth = 777; DoSave(); sim.G.aeth = 1; Ok("continue restores progress", ContinueGame() && MathF.Abs(sim.G.aeth - 777) < .5f && mode == Mode.Cmd);
            // crawl
            StartCrawl(Data.Prologue, null); for (int i = 0; i < 5; i++) Frame(1f / 60); Ok("crawl overlay shown", crawlActive); EndCrawl(); Ok("crawl skip works", !crawlActive);
            // speed + soak in engine at x6
            sim.Speed = 6; for (int i = 0; i < 1800; i++) Frame(1f / 60); Ok("30 s of x6 frames ran clean", sim.G.t > 100 && !float.IsNaN(sim.G.aeth));
            // all modes render with a storm and discoveries in play
            sim.G.nextStorm = 0; for (int i = 0; i < 600; i++) Frame(1f / 60); Ok("storm frames render", true);
        }
        finally { if (File.Exists(Sim.SavePath + ".uitest-bak")) File.Move(Sim.SavePath + ".uitest-bak", Sim.SavePath, true); else if (File.Exists(Sim.SavePath)) File.Delete(Sim.SavePath); }
        Raylib.CloseWindow(); Console.WriteLine($"\n{pass}/{pass + fail} passed"); return fail == 0 ? 0 : 1;
    }

    public int RunAudioTest()
    {
        Init(false, 320, 200, true); Console.WriteLine("audio device ready: " + audioOk); if (!audioOk) { Raylib.CloseWindow(); return 2; }
        SetMusic(true); double t0 = Raylib.GetTime(); while (Raylib.GetTime() - t0 < 1.5) { UpdateAudio(.016f); Raylib.BeginDrawing(); Raylib.EndDrawing(); }
        bool playing = Raylib.IsMusicStreamPlaying(music); float tp = Raylib.GetMusicTimePlayed(music), len = Raylib.GetMusicTimeLength(music);
        Play("scan"); Play("up"); Console.WriteLine($"music playing={playing} played={tp:F2}s length={len:F1}s"); ShutdownAudio(); Raylib.CloseWindow();
        return playing && tp > .5f && len > 20 ? 0 : 1;
    }
}
