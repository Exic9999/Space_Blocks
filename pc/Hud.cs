// HUD, input, title/menu/upgrades/crawl screens, cabin view. Immediate-mode UI.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    Font font; bool fontOk;
    bool showSheet, showMenu, confirmNew, crawlActive;
    Chapter? crawlCh; float crawlT, crawlDur; Action? crawlDone;
    readonly List<Rectangle> hit = new(), prevHit = new();
    Vector2 mouse, dragStart; bool dragging; float dragMoved;
    bool clickPressed; int curLayer;
    public bool Inject; public Vector2 InjMouse; public bool InjClick, InjDown, InjUp; bool mouseDown, mouseUp;
    int TopLayer => crawlActive ? 2 : (showSheet || showMenu) ? 1 : 0;
    float S => Math.Max(.7f, Raylib.GetScreenHeight() / 720f);
    static readonly Color Panel = new((byte)11, (byte)19, (byte)38, (byte)225), Edge = new((byte)60, (byte)130, (byte)170, (byte)255), Dim = new((byte)125, (byte)155, (byte)179, (byte)255), Txt = new((byte)200, (byte)225, (byte)240, (byte)255), Accent = new((byte)90, (byte)220, (byte)255, (byte)255);

    void LoadUiFont()
    {
        foreach (var p in new[] { @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf" })
            if (File.Exists(p)) { font = Raylib.LoadFontEx(p, 40, null, 0); Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear); fontOk = true; break; }
    }
    void DText(string s, float x, float y, float size, Color c)
    {
        if (fontOk) Raylib.DrawTextEx(font, s, new Vector2(x, y), size, 0, c); else Raylib.DrawText(s, (int)x, (int)y, (int)size, c);
    }
    float TW(string s, float size) => fontOk ? Raylib.MeasureTextEx(font, s, size, 0).X : Raylib.MeasureText(s, (int)size);
    void DTextC(string s, float cx, float y, float size, Color c) => DText(s, cx - TW(s, size) / 2, y, size, c);
    List<string> Wrap(string text, float maxW, float size)
    {
        var lines = new List<string>(); var cur = "";
        foreach (var w in text.Split(' '))
        {
            var test = cur.Length == 0 ? w : cur + " " + w;
            if (TW(test, size) > maxW && cur.Length > 0) { lines.Add(cur); cur = w; } else cur = test;
        }
        if (cur.Length > 0) lines.Add(cur); return lines;
    }
    static Color Rgb(uint c, byte a = 255) => Col.Hex(c, a);
    bool Over(Rectangle r) => Raylib.CheckCollisionPointRec(mouse, r);
    bool UiOver() { foreach (var r in prevHit) if (Over(r)) return true; return false; }
    void Box(Rectangle r, Color fill, Color? edge = null) { hit.Add(r); Raylib.DrawRectangleRec(r, fill); Raylib.DrawRectangleLinesEx(r, 1, edge ?? Edge); }
    bool Btn(Rectangle r, string txt, float size, bool on = false, bool en = true)
    {
        hit.Add(r); bool live = curLayer >= TopLayer; bool over = en && live && Over(r);
        Raylib.DrawRectangleRec(r, over ? new Color((byte)30, (byte)55, (byte)90, (byte)255) : new Color((byte)19, (byte)32, (byte)58, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1, on ? Accent : Edge);
        var c = !en ? new Color((byte)90, (byte)105, (byte)120, (byte)255) : on ? Accent : Txt; DTextC(txt, r.X + r.Width / 2, r.Y + r.Height / 2 - size * .6f, size, c);
        return over && clickPressed;
    }

    // ---------------- story crawl ----------------
    void StartCrawl(Chapter c, Action? done) { crawlCh = c; crawlT = 0; crawlDur = Math.Max(45, c.Paras.Sum(p => p.Length) / 9f); crawlDone = done; crawlActive = true; }
    void EndCrawl() { if (!crawlActive) return; crawlActive = false; var d = crawlDone; crawlDone = null; d?.Invoke(); }

    // ---------------- input ----------------
    Ship? nearShip, nearNpc; bool nearHelm;
    void FindNearby()
    {
        nearShip = nearNpc = null; nearHelm = Mx.Hypot(walk.x, walk.z + 22) < 3.8f;
        foreach (var s in sim.G.ships)
        {
            var p = Data.Pads[s.idx % 5];
            if ((s.state == "docked" || (s.state == "landing" && s.lt > .95f)) && Mx.Hypot(walk.x - p[0], walk.z - p[1]) < 6.2f) nearShip = s;
            if (s.state == "docked" && (s.ph is "exit" or "idle" or "back") && Mx.Hypot(walk.x - s.px, walk.z - s.pz) < 3.2f) nearNpc = s;
        }
    }
    void Order(string type)
    {
        if (selExec) { Play("order"); if (type is "hold" or "return") sim.Halt(); else if (type == "lock" && selIsl != null && selIsl.found) { sim.SailToIsland(selIsl); execFollow = true; } return; }
        if (sel == null) return; Play("order"); if (type == "lock") { if (selIsl != null && selIsl.found) sim.GiveOrder(sel, "mine", selIsl.id); } else sim.GiveOrder(sel, type); }

    void HandleInput(float dt)
    {
        if (Inject) { mouse = InjMouse; clickPressed = InjClick; InjClick = false; mouseDown = InjDown; mouseUp = InjUp; InjUp = false; } else { mouse = Raylib.GetMousePosition(); clickPressed = Raylib.IsMouseButtonPressed(MouseButton.Left); mouseDown = Raylib.IsMouseButtonDown(MouseButton.Left); mouseUp = Raylib.IsMouseButtonReleased(MouseButton.Left); }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (crawlActive) EndCrawl(); else if (showSheet) showSheet = false; else if (showMenu) showMenu = false;
            else if (mode == Mode.Walk || mode == Mode.Cabin) { SetMode(mode == Mode.Cabin && cabFrom == Mode.Walk ? Mode.Walk : Mode.Cmd); }
            else if (mode == Mode.Cmd) showMenu = true;
        }
        if (mode == Mode.Title || crawlActive || showMenu || showSheet) { dragging = false; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.F5)) { DoSave(); sim.Say("Game saved.", 0x6eeba0); }
        if (Raylib.IsKeyPressed(KeyboardKey.F9)) ContinueGame();
        if (Raylib.IsKeyPressed(KeyboardKey.RightBracket)) sim.Speed = sim.Speed == 1 ? 3 : sim.Speed == 3 ? 6 : 1;
        if (mode == Mode.Cmd)
        {
            if (clickPressed && !UiOver()) { dragging = true; dragMoved = 0; dragStart = mouse; }
            if (dragging && mouseDown) { var d = Raylib.GetMouseDelta(); dragMoved += MathF.Abs(d.X) + MathF.Abs(d.Y); cam.az -= d.X * .006f; cam.el = Math.Clamp(cam.el + d.Y * .005f, .08f, 1.45f); }
            if (dragging && mouseUp) { dragging = false; if (dragMoved < 10) Tap(mouse); }
            if (Raylib.IsMouseButtonPressed(MouseButton.Right) && !UiOver() && sel != null)
            {
                var ray = Raylib.GetScreenToWorldRay(mouse, cam3); foreach (var i in sim.G.isl) if (i.found && Raylib.GetRayCollisionSphere(ray, new Vector3(i.x, i.h, i.y), i.s * 1.25f).Hit) { selIsl = i; Order("lock"); break; }
            }
            float wheel = Raylib.GetMouseWheelMove(); if (wheel != 0 && !UiOver()) cam.dist = Math.Clamp(cam.dist * (wheel > 0 ? .9f : 1.12f), 250, 5500);
            if (Raylib.IsKeyDown(KeyboardKey.Z)) cam.dist = Math.Max(250, cam.dist * (1 - dt * 1.5f)); if (Raylib.IsKeyDown(KeyboardKey.X)) cam.dist = Math.Min(5500, cam.dist * (1 + dt * 1.5f));
            float fy = 0, fxr = 0;
            if (Raylib.IsKeyDown(KeyboardKey.W)) fy += 1; if (Raylib.IsKeyDown(KeyboardKey.S)) fy -= 1; if (Raylib.IsKeyDown(KeyboardKey.D)) fxr += 1; if (Raylib.IsKeyDown(KeyboardKey.A)) fxr -= 1;
            if (fy != 0 || fxr != 0) Pan(fy, fxr, cam.dist * .8f * dt);
            if (Raylib.IsKeyPressed(KeyboardKey.Space)) { sim.DoScan(); Play("scan"); } if (Raylib.IsKeyPressed(KeyboardKey.M)) ToggleMusic();
            if (Raylib.IsKeyPressed(KeyboardKey.L)) Order("lock"); if (Raylib.IsKeyPressed(KeyboardKey.R)) Order("return"); if (Raylib.IsKeyPressed(KeyboardKey.H)) Order("hold");
            if (Raylib.IsKeyPressed(KeyboardKey.C)) Order("scout"); if (Raylib.IsKeyPressed(KeyboardKey.G)) Order("auto");
            if (Raylib.IsKeyPressed(KeyboardKey.U)) showSheet = true;
            if (Raylib.IsKeyPressed(KeyboardKey.V) && sel != null) OpenCabin(sel);
            if (Raylib.IsKeyPressed(KeyboardKey.B)) EnterWalk();
            if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                var f = sim.G.isl.Where(i => i.found).ToList(); if (f.Count > 0) { int ix = selIsl == null ? -1 : f.IndexOf(selIsl); selIsl = f[(ix + 1) % f.Count]; }
            }
            for (int n = 0; n < 5; n++) if (Raylib.IsKeyPressed(KeyboardKey.One + n) && n < sim.G.ships.Count) { sel = sim.G.ships[n]; cam.follow = sel; cam.dist = Math.Min(cam.dist, 700); selIsl = null; }
        }
        else if (mode == Mode.Walk)
        {
            var d = Raylib.GetMouseDelta(); walk.yaw -= d.X * .0025f; walk.pitch = Math.Clamp(walk.pitch - d.Y * .002f, -1.2f, 1.2f);
            float mx = 0, mz = 0;
            if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) mz -= 1; if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) mz += 1;
            if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) mx -= 1; if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) mx += 1;
            walk.Step(sim, dt, mx, mz); FindNearby();
            if (Raylib.IsKeyPressed(KeyboardKey.E) && nearHelm && nearShip == null && nearNpc == null) UseHelm();
            else if (Raylib.IsKeyPressed(KeyboardKey.E) && nearShip != null) OpenCabin(nearShip); else if (Raylib.IsKeyPressed(KeyboardKey.E) && nearNpc != null) sim.Chat(nearNpc);
            if (Raylib.IsKeyPressed(KeyboardKey.T) && nearNpc != null) sim.Chat(nearNpc);
            if (Raylib.IsKeyPressed(KeyboardKey.B)) SetMode(Mode.Cmd);
        }
        else if (mode == Mode.Cabin)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Enter)) SetMode(cabFrom == Mode.Walk ? Mode.Walk : Mode.Cmd);
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && !UiOver()) cabAz -= Raylib.GetMouseDelta().X * .008f;
        }
    }
    public void Pan(float fy, float fxr, float k)
    {
        cam.follow = null; execFollow = false; float sn = MathF.Sin(cam.az), cs = MathF.Cos(cam.az);
        cam.tx = Math.Clamp(cam.tx + (-sn * fy + cs * fxr) * k, 0, Data.W); cam.tz = Math.Clamp(cam.tz + (-cs * fy - sn * fxr) * k, 0, Data.W);
    }
    void UseHelm() { selExec = true; execFollow = true; cam.follow = null; cam.dist = 1400; SetMode(Mode.Cmd); sim.Say("Helm: click empty space to set a course, or pick an island and press L.", 0x5adcff); }
    void EnterWalk() { walk.x = 0; walk.z = -22.5f; walk.yaw = 0; walk.pitch = 0; SetMode(Mode.Walk); }

    // ---------------- HUD ----------------
    float lastDt;
    void DrawHud(float dt)
    {
        lastDt = dt; prevHit.Clear(); prevHit.AddRange(hit); hit.Clear();
        float W = Raylib.GetScreenWidth(), H = Raylib.GetScreenHeight(), s = S;
        var G = sim.G;
        curLayer = 0; if (crawlActive) { curLayer = 2; DrawCrawl(W, H, s, dt); return; }
        if (mode == Mode.Title) { DrawTitle(W, H, s); return; }
        if (mode != Mode.Cabin)
        {
            // top bar
            Box(new Rectangle(0, 0, W, 44 * s), new Color((byte)11, (byte)19, (byte)38, (byte)235));
            string res = $"{(int)G.aeth} Aetherite   |   {(int)G.delivered} delivered   |   {G.found}/{G.isl.Count} charted";
            DText(res, 12 * s, 11 * s, 18 * s, Rgb(0xa08cff));
            float bx = W - 8 * s; float bw = 84 * s, bh = 32 * s;
            bx -= bw; if (Btn(new Rectangle(bx, 6 * s, bw, bh), "MENU", 15 * s)) showMenu = true;
            bx -= bw + 6 * s; if (Btn(new Rectangle(bx, 6 * s, bw, bh), "x" + sim.Speed, 15 * s)) sim.Speed = sim.Speed == 1 ? 3 : sim.Speed == 3 ? 6 : 1;
            bx -= bw + 6 * s; if (Btn(new Rectangle(bx, 6 * s, bw, bh), mode == Mode.Walk ? "MAP" : "BRIDGE", 15 * s)) { if (mode == Mode.Walk) SetMode(Mode.Cmd); else EnterWalk(); }
            bx -= bw + 6 * s; if (Btn(new Rectangle(bx, 6 * s, bw, bh), "SCAN", 15 * s)) { sim.DoScan(); Play("scan"); }
            bx -= bw + 6 * s; if (Btn(new Rectangle(bx, 6 * s, bw, bh), musicOn ? "MUSIC ON" : "MUSIC", 14 * s, musicOn)) ToggleMusic();
            // log
            float ly = 54 * s; foreach (var l in sim.Logs) { DText(l.msg, 13 * s + 1, ly + 1, 14 * s, new Color((byte)0, (byte)0, (byte)0, (byte)200)); DText(l.msg, 12 * s, ly, 14 * s, Rgb(l.color)); ly += 18 * s; }
        }
        if (mode == Mode.Cmd) DrawCmdHud(W, H, s);
        else if (mode == Mode.Walk) DrawWalkHud(W, H, s);
        else if (mode == Mode.Cabin) DrawCabinHud(W, H, s);
        // talk box
        if (sim.Talk != null && !crawlActive)
        {
            var tk = sim.Talk; float bw = Math.Min(560 * s, W - 40 * s); var lines = Wrap(tk.text, bw - 28 * s, 16 * s); float bh = (lines.Count + 1) * 21 * s + 18 * s;
            var r = new Rectangle((W - bw) / 2, H * .3f, bw, bh); Box(r, new Color((byte)11, (byte)18, (byte)36, (byte)240));
            DText($"{tk.name}  \"{tk.call}\"", r.X + 14 * s, r.Y + 9 * s, 16 * s, Rgb(tk.color)); float ty = r.Y + 9 * s + 24 * s; foreach (var ln in lines) { DText(ln, r.X + 14 * s, ty, 16 * s, Txt); ty += 21 * s; }
        }
        if (showSheet) { curLayer = 1; DrawSheet(W, H, s); }
        if (showMenu) { curLayer = 1; DrawMenu(W, H, s); }
        curLayer = 0;
        if (!crawlActive && mode != Mode.Cabin) DText($"{Raylib.GetFPS()} fps", W - 70 * s, H - 20 * s, 12 * s, Dim);
    }

    void DrawCmdHud(float W, float H, float s)
    {
        var G = sim.G; float ph = 152 * s;
        Box(new Rectangle(0, H - ph, W, ph), new Color((byte)11, (byte)19, (byte)38, (byte)235));
        float cx = 10 * s, cy = H - ph + 8 * s, cw = 128 * s, chh = 56 * s;
        {   // Executor chip
            var r = new Rectangle(cx, cy, cw, chh); if (Btn(r, "", 12 * s, selExec)) { selExec = true; execFollow = true; cam.follow = null; cam.dist = Math.Max(cam.dist, 900); }
            Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, 4 * s, r.Height), Color.White);
            DText("Executor", r.X + 10 * s, r.Y + 3 * s, 14 * s, Txt); DText(G.exMoving ? $"sailing {(int)G.exSpeed} u/s" : "on station", r.X + 10 * s, r.Y + 20 * s, 11 * s, Dim);
            void XBar(int k2, float f, Color c) { var b = new Rectangle(r.X + 10 * s, r.Y + 36 * s + k2 * 6 * s, cw - 18 * s, 4 * s); Raylib.DrawRectangleRec(b, new Color((byte)0, (byte)0, (byte)0, (byte)110)); Raylib.DrawRectangleRec(new Rectangle(b.X, b.Y, b.Width * Math.Clamp(f, 0, 1), b.Height), c); }
            XBar(0, G.aeth / 600f, Rgb(0xa08cff)); XBar(1, G.exSpeed / Sim.ExMax, Accent); cx += cw + 6 * s;
        }
        foreach (var sh in G.ships)
        {
            var st = Sim.StatOf(sh); var r = new Rectangle(cx, cy, cw, chh); bool on = sh == sel && !selExec;
            if (Btn(r, "", 12 * s, on)) { sel = sh; selExec = false; cam.follow = sh; cam.dist = Math.Min(cam.dist, 700); selIsl = null; }
            Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, 4 * s, r.Height), Rgb(sh.color));
            DText(sh.name.Split(' ')[0], r.X + 10 * s, r.Y + 3 * s, 14 * s, Txt); DText(sh.indep ? "independent" : sh.state, r.X + 10 * s, r.Y + 20 * s, 11 * s, Dim);
            void Bar(int k, float f, Color c) { var b = new Rectangle(r.X + 10 * s, r.Y + 36 * s + k * 6 * s, cw - 18 * s, 4 * s); Raylib.DrawRectangleRec(b, new Color((byte)0, (byte)0, (byte)0, (byte)110)); Raylib.DrawRectangleRec(new Rectangle(b.X, b.Y, b.Width * Math.Clamp(f, 0, 1), b.Height), c); }
            Bar(0, sh.fuel / st.fuel, Accent); Bar(1, sh.loy / 100f, sh.indep ? Rgb(0xffbe5a) : sh.loy < 35 ? Rgb(0xff6464) : Rgb(0x6eeba0)); Bar(2, sh.hull / st.hull, Rgb(0xff9a4a));
            cx += cw + 6 * s;
        }
        string inf = "";
        if (selExec) { float deg = (((-G.exHead * 180 / MathF.PI) % 360) + 360) % 360; inf = $"The Executor  speed {(int)G.exSpeed}/{(int)Sim.ExMax}  heading {(int)deg} deg  stores {(int)G.aeth} (reserve {(int)Sim.ExReserve})  course: " + (G.exMoving ? $"({(int)G.exDestX}, {(int)G.exDestY})" : "none - click empty space to set one"); }
        else if (sel != null) { var st = Sim.StatOf(sel); inf = $"{sel.name} \"{Data.Crew[sel.idx].Call}\"  fuel {(int)sel.fuel}/{st.fuel}  hull {(int)sel.hull}/{st.hull}  cargo {(int)sel.cargo}/{st.cargo}  order: {(sel.order != null ? sel.order.type + (sel.order.isl >= 0 ? " " + G.isl[sel.order.isl].name : "") : "none")}{(sel.indep ? "  (INDEPENDENT)" : "")}"; }
        if (selIsl != null) inf += (inf.Length > 0 ? "     |     " : "") + $"{selIsl.name}: " + (selIsl.found ? $"{(int)selIsl.aeth} Aetherite" : "unknown signal");
        DText(inf, 12 * s, H - ph + 70 * s, 14 * s, Txt);
        float by = H - ph + 100 * s, bh = 40 * s; var names = new List<(string, string)>();
        if (selExec) { if (selIsl != null && selIsl.found) names.Add(("SAIL TO ISLAND [L]", "lock")); names.AddRange(new[] { ("HALT [H]", "hold"), ("UPGRADE [U]", "up") }); }
        else { if (selIsl != null && selIsl.found) names.Add(("LOCK ON [L]", "lock")); names.AddRange(new[] { ("RETURN [R]", "return"), ("HOLD [H]", "hold"), ("SCOUT [C]", "scout"), ("AUTO [G]", "auto"), ("CABIN [V]", "cabin"), ("UPGRADE [U]", "up") }); }
        float bw = Math.Min(130 * s, (W - 20 * s - 6 * s * names.Count) / names.Count), bx = 10 * s;
        foreach (var (label, key) in names) { if (Btn(new Rectangle(bx, by, bw, bh), label, 13 * s, key == "lock")) { if (key == "up") showSheet = true; else if (key == "cabin") { if (sel != null) OpenCabin(sel); } else Order(key); } bx += bw + 6 * s; }
        // radar
        float rs = 130 * s, rx = W - rs - 8 * s, ry = 52 * s; var rr = new Rectangle(rx, ry, rs, rs); Box(rr, new Color((byte)11, (byte)19, (byte)38, (byte)205)); float k = rs / Data.W;
        if (clickPressed && curLayer >= TopLayer && Over(rr)) { cam.follow = null; execFollow = false; cam.tx = Math.Clamp((mouse.X - rx) / k, 0, Data.W); cam.tz = Math.Clamp((mouse.Y - ry) / k, 0, Data.W); cam.ty = 0; }
        foreach (var i in G.isl) if (i.found) Raylib.DrawRectangleRec(new Rectangle(rx + i.x * k - 3, ry + i.y * k - 3, 6, 6), i == selIsl ? Accent : Rgb(0x58c26a));
        Raylib.DrawRectangleRec(new Rectangle(rx + sim.G.exx * k - 4, ry + sim.G.exy * k - 4, 8, 8), Color.White);
        foreach (var q in G.storms) { Raylib.DrawCircleV(new Vector2(rx + q.x * k, ry + q.y * k), q.r * k, new Color((byte)130, (byte)90, (byte)255, (byte)70)); Raylib.DrawCircleLinesV(new Vector2(rx + q.x * k, ry + q.y * k), q.r * k, new Color((byte)160, (byte)120, (byte)255, (byte)220)); }
        foreach (var sh in G.ships) Raylib.DrawRectangleRec(new Rectangle(rx + sh.x * k - 3, ry + sh.y * k - 3, 6, 6), Rgb(sh.color));
        Raylib.DrawRectangleLinesEx(new Rectangle(rx + cam.tx * k - 10, ry + cam.tz * k - 10, 20, 20), 1, new Color((byte)255, (byte)255, (byte)255, (byte)140));
        // off-screen island arrow
        if (selIsl != null && selIsl.found)
        {
            var p = new Vector3(selIsl.x, selIsl.h, selIsl.y); var fwd = Vector3.Normalize(new Vector3(cam.tx, cam.ty, cam.tz) - camPos); bool behind = Vector3.Dot(p - camPos, fwd) < 0;
            var sp = Raylib.GetWorldToScreen(p, cam3);
            if (behind || sp.X < 0 || sp.X > W || sp.Y < 60 * s || sp.Y > H - ph)
            {
                var c = new Vector2(W / 2, (H - ph + 60 * s) / 2); var dir = behind ? c - sp : sp - c; if (dir.Length() < 1) dir = new Vector2(1, 0); dir = Vector2.Normalize(dir);
                float ex = Math.Clamp(c.X + dir.X * W, 30, W - 30), ey = Math.Clamp(c.Y + dir.Y * H, 70 * s, H - ph - 30 * s); float a = MathF.Atan2(dir.Y, dir.X);
                Raylib.DrawTriangle(new Vector2(ex + MathF.Cos(a) * 16, ey + MathF.Sin(a) * 16), new Vector2(ex + MathF.Cos(a + 2.5f) * 12, ey + MathF.Sin(a + 2.5f) * 12), new Vector2(ex + MathF.Cos(a - 2.5f) * 12, ey + MathF.Sin(a - 2.5f) * 12), Accent);
            }
        }
        // island labels
        foreach (var i in G.isl)
        {
            var p = new Vector3(i.x, i.h - i.s * 1.2f, i.y); var fwd = Vector3.Normalize(new Vector3(cam.tx, cam.ty, cam.tz) - camPos);
            if (Vector3.Dot(p - camPos, fwd) <= 0 || cam.dist > 2600) continue;
            var sp = Raylib.GetWorldToScreen(new Vector3(i.x, i.h - i.s * 1.3f, i.y), cam3); if (sp.X < 0 || sp.X > W || sp.Y < 50 * s || sp.Y > H - ph) continue;
            DTextC(i.found ? i.name : "?", sp.X, sp.Y, 14 * s, i.found ? Rgb(0xd8ecff) : Dim);
        }
    }
    void DrawWalkHud(float W, float H, float s)
    {
        { var Gx = sim.G; float deg = (((-Gx.exHead * 180 / MathF.PI) % 360) + 360) % 360; DTextC($"Executor: {(Gx.exMoving ? "sailing" : "on station")}  {(int)Gx.exSpeed} u/s  heading {(int)deg}  stores {(int)Gx.aeth}", W / 2, 74 * s, 13 * s, Dim); }
        DTextC(walk.Where().ToUpper(), W / 2, 52 * s, 14 * s, Accent);
        Raylib.DrawCircleLinesV(new Vector2(W / 2, H / 2), 3, new Color((byte)200, (byte)230, (byte)255, (byte)150));
        float y = H - 30 * s; var hints = new List<string>();
        if (nearHelm) hints.Add("[E] Helm: plot a course"); if (nearNpc != null) hints.Add($"[T] Talk to {nearNpc.name.Split(' ')[0]}"); if (nearShip != null) hints.Add($"[E] Enter {Data.Crew[nearShip.idx].Call}"); hints.Add("[B] Command view     WASD move, mouse look");
        foreach (var h in hints.AsEnumerable().Reverse()) { DTextC(h, W / 2, y, 16 * s, h.StartsWith("[B]") ? Dim : Accent); y -= 24 * s; }
    }
    void DrawSheet(float W, float H, float s)
    {
        Raylib.DrawRectangle(0, 0, (int)W, (int)H, new Color((byte)4, (byte)6, (byte)16, (byte)200)); var G = sim.G;
        float cw = Math.Min(460 * s, W - 20 * s), rowH = 44 * s; int rows = 1 + (sel != null ? Data.ShipUp.Length : 0) + Data.ExecUp.Length;
        float ch = 120 * s + rows * rowH; var r = new Rectangle((W - cw) / 2, Math.Max(10 * s, (H - ch) / 2), cw, ch); Box(r, new Color((byte)11, (byte)19, (byte)38, (byte)250));
        float x = r.X + 16 * s, y = r.Y + 12 * s;
        DText("UPGRADES", x, y, 20 * s, Accent); DText($"{(int)G.aeth} Aetherite", r.X + cw - 16 * s - TW($"{(int)G.aeth} Aetherite", 16 * s), y + 3 * s, 16 * s, Rgb(0xa08cff)); y += 36 * s;
        if (sel != null)
        {
            DText(sel.name + "'s ship", x, y, 14 * s, Dim); y += 22 * s;
            foreach (var u in Data.ShipUp) { int lv = Sim.Lvl(sel, u.key), c = Data.Cost(u.cost, lv); DText($"{u.label} Lv {lv + 1}", x, y, 15 * s, Txt); DText(u.desc, x, y + 18 * s, 11 * s, Dim); if (Btn(new Rectangle(r.X + cw - 96 * s, y - 4 * s, 80 * s, 34 * s), lv >= 8 ? "MAX" : c.ToString(), 14 * s, false, G.aeth >= c && lv < 8)) { if (sim.BuyShipUp(sel, u.key)) Play("up"); } y += rowH; }
        }
        DText("The Executor", x, y, 14 * s, Dim); y += 22 * s;
        foreach (var u in Data.ExecUp) { int lv = u.key == "sensors" ? G.upSensors : G.upQuarters, c = Data.Cost(u.cost, lv); DText($"{u.label} Lv {lv}/{u.max}", x, y, 15 * s, Txt); DText(u.desc, x, y + 18 * s, 11 * s, Dim); if (Btn(new Rectangle(r.X + cw - 96 * s, y - 4 * s, 80 * s, 34 * s), lv >= u.max ? "MAX" : c.ToString(), 14 * s, false, G.aeth >= c && lv < u.max)) { if (sim.BuyExecUp(u.key)) Play("up2"); } y += rowH; }
        if (Btn(new Rectangle(x, r.Y + ch - 46 * s, cw - 32 * s, 34 * s), "CLOSE", 15 * s)) showSheet = false;
    }
    void DrawMenu(float W, float H, float s)
    {
        Raylib.DrawRectangle(0, 0, (int)W, (int)H, new Color((byte)4, (byte)6, (byte)16, (byte)210));
        float cw = 340 * s, ch = 330 * s; var r = new Rectangle((W - cw) / 2, (H - ch) / 2, cw, ch); Box(r, new Color((byte)11, (byte)19, (byte)38, (byte)250));
        DTextC("COMMAND MENU", W / 2, r.Y + 14 * s, 20 * s, Accent); float y = r.Y + 56 * s, bw = cw - 40 * s, bh = 40 * s;
        if (Btn(new Rectangle(r.X + 20 * s, y, bw, bh), "Save now", 15 * s)) { DoSave(); sim.Say("Game saved.", 0x6eeba0); showMenu = false; } y += bh + 8 * s;
        if (Btn(new Rectangle(r.X + 20 * s, y, bw, bh), "Replay story crawl", 15 * s)) { showMenu = false; StartCrawl(Data.Prologue, null); } y += bh + 8 * s;
        if (Btn(new Rectangle(r.X + 20 * s, y, bw, bh), "Resume", 15 * s)) { showMenu = false; confirmNew = false; } y += bh + 8 * s;
        if (Btn(new Rectangle(r.X + 20 * s, y, bw, bh), confirmNew ? "Click again to erase and restart" : "New game (erases save)", 15 * s)) { if (!confirmNew) confirmNew = true; else { confirmNew = false; showMenu = false; StartNew(true); } } y += bh + 8 * s;
        if (Btn(new Rectangle(r.X + 20 * s, y, bw, bh), "Save and quit", 15 * s)) { DoSave(); quit = true; }
    }
    void DrawTitle(float W, float H, float s)
    {
        cam.az += lastDt * .04f; Raylib.DrawRectangleGradientV(0, 0, (int)W, (int)H, new Color((byte)5, (byte)7, (byte)16, (byte)120), new Color((byte)5, (byte)7, (byte)16, (byte)230));
        DTextC("SPACE BLOCKS", W / 2, H * .18f, 64 * s, Accent);
        float y = H * .18f + 84 * s; foreach (var l in Wrap("Commander Jason, the Executor drifts among the floating islands. Send your pilots to mine Aetherite, watch them land in the hangar, and walk the bridge with your crew.", Math.Min(560 * s, W - 40 * s), 17 * s)) { DTextC(l, W / 2, y, 17 * s, Dim); y += 24 * s; }
        float bw = 260 * s, bh = 46 * s, bx = (W - bw) / 2; y += 20 * s; bool has = File.Exists(Sim.SavePath);
        if (Btn(new Rectangle(bx, y, bw, bh), "Continue", 18 * s, false, has)) { if (!ContinueGame()) StartNew(); } y += bh + 10 * s;
        if (Btn(new Rectangle(bx, y, bw, bh), "New game", 18 * s)) StartNew(true); y += bh + 10 * s;
        if (Btn(new Rectangle(bx, y, bw, bh), "Quit", 18 * s)) quit = true;
        DTextC("Command view: drag to orbit, wheel to zoom, click a pilot, then an island, then LOCK ON. Press B to walk the bridge.", W / 2, H - 40 * s, 13 * s, Dim);
    }
    void DrawCrawl(float W, float H, float s, float dt)
    {
        crawlT += dt; Raylib.DrawRectangle(0, 0, (int)W, (int)H, new Color((byte)2, (byte)3, (byte)10, (byte)255));
        var c = crawlCh!; float bw = Math.Min(W * .7f, 760 * s), size = 22 * s; var items = new List<(string t, float sz, float gap, bool center)> { (c.Title.ToUpper(), 34 * s, 8 * s, true), (c.Sub.ToUpper(), 18 * s, 34 * s, true) };
        foreach (var p in c.Paras) { var ls = Wrap(p, bw, size); for (int i = 0; i < ls.Count; i++) items.Add((ls[i], size, i == ls.Count - 1 ? 24 * s : 4 * s, false)); }
        float total = 0; foreach (var it in items) total += it.sz * 1.35f + it.gap; float prog = crawlT / crawlDur; float y0 = H - prog * (H + total + 80 * s) + 20 * s; float y = H * 0 + y0 + H;
        y = H - prog * (total + H * .9f);
        foreach (var it in items)
        {
            float f = Math.Clamp(y / H, 0, 1), k = .35f + .65f * f; float alpha = Math.Clamp(f * 2.4f, 0, 1) * (y > H ? 0 : 1);
            if (y < H && y > -60 * s) { float sz = it.sz * k; var col = new Color((byte)143, (byte)230, (byte)255, (byte)(alpha * 255)); float tx = it.center ? W / 2 - TW(it.t, sz) / 2 : W / 2 - bw * k / 2; DText(it.t, tx, y, sz, col); }
            y += (it.sz * 1.35f + it.gap) * k;
        }
        if (Btn(new Rectangle(W - 100 * s, 12 * s, 88 * s, 34 * s), "SKIP", 15 * s)) EndCrawl(); if (crawlT >= crawlDur + .5f) EndCrawl();
    }

    // ---------------- cabin ----------------
    Mode cabFrom = Mode.Cmd; Ship? cabShip; Mesh cabMesh; bool cabBuilt; float cabAz; int cabIdx = -1;
    void OpenCabin(Ship s)
    {
        if (cabIdx != s.idx) { if (cabBuilt) Raylib.UnloadMesh(cabMesh); cabMesh = BuildCabin(s.idx); cabBuilt = true; cabIdx = s.idx; }
        cabShip = s; cabFrom = mode == Mode.Walk ? Mode.Walk : Mode.Cmd; cabAz = 0; SetMode(Mode.Cabin);
    }
    Mesh BuildCabin(int idx)
    {
        var mb = new MeshBuilder(); var cd = Data.Crew[idx]; var wall = Col.Hex(0x2c3550); var frame = Col.Hex(0x2a3040); var dk = Col.Hex(0x1b2030); var rnd = new Random(idx + 3);
        mb.Box(5, .1f, 4, 0, .0f, 0, Col.Hex(0x242a3a)); mb.Box(5, .1f, 4, 0, 2.8f, 0, wall);
        mb.Box(.1f, 2.8f, 4, -2.5f, 1.4f, 0, wall); mb.Box(.1f, 2.8f, 4, 2.5f, 1.4f, 0, wall); mb.Box(5, 2.8f, .1f, 0, 1.4f, 2.0f, wall);
        mb.Box(5, .6f, .1f, 0, .3f, -2.0f, wall); mb.Box(5, .6f, .1f, 0, 2.5f, -2.0f, wall); mb.Box(1.3f, 1.6f, .1f, -1.85f, 1.4f, -2.0f, wall); mb.Box(1.3f, 1.6f, .1f, 1.85f, 1.4f, -2.0f, wall);
        mb.Box(2.4f, 1.1f, .04f, 0, 1.7f, -2.03f, Col.Hex(0x04060e), 255);
        for (int k = 0; k < 70; k++) mb.Box(.012f, .012f, .02f, (float)(rnd.NextDouble() - .5) * 2.2f, 1.2f + (float)rnd.NextDouble() * 1f, -2.0f, Color.White, 255);
        mb.Box(.9f, .5f, 2.0f, -1.8f, .25f, -.4f, frame); mb.Box(.82f, .18f, 1.9f, -1.8f, .58f, -.4f, Col.Hex(0xe8ecf4)); mb.Box(.86f, .1f, 1.15f, -1.8f, .7f, .1f, Col.Hex(cd.Color));
        mb.Sphere(.2f, -1.8f, .75f, -1.1f, Col.Hex(0xe8ecf4), 1.5f, .5f, 1.2f); foreach (var (x, z) in new[] { (-2.2f, -1.4f), (-1.4f, -1.4f), (-2.2f, .6f), (-1.4f, .6f) }) mb.Box(.06f, 1.3f, .06f, x, .65f, z, frame); mb.Box(.9f, .06f, 2.0f, -1.8f, 1.3f, -.4f, frame);
        mb.Box(.7f, .15f, .7f, 1.6f, .55f, -1.2f, Col.Hex(0x222a3a)); mb.Box(.7f, .8f, .12f, 1.6f, 1.0f, -.9f, Col.Hex(0x222a3a)); mb.Box(1.4f, .5f, .5f, 1.4f, .35f, -1.75f, dk); mb.Box(.9f, .4f, .04f, 1.4f, .8f, -1.7f, Col.Hex(0x5adcff), 255);
        mb.Box(.8f, 1.8f, .5f, 2.1f, .9f, 1.5f, Col.Hex(0x4a566e)); mb.Box(1.2f, .05f, .2f, 0, 2.76f, 0, Col.Hex(0xfff2d0), 255);
        switch (idx)
        {
            case 0: for (int k = 0; k < 3; k++) mb.CylY(.07f, .02f, .2f + k * .04f, .2f + k * .35f, 1.45f, -1.85f, Col.Hex(0xffd070), 8, 90); mb.Sphere(.22f, 1.3f, 1.62f, -1.84f, Col.Hex(0xb3202f)); mb.Box(.9f, 1.2f, .03f, 0, 1.5f, 1.95f, Col.Hex(0xb3202f)); break;
            case 1: mb.Box(1.5f, .08f, .6f, .4f, .85f, 1.6f, Col.Hex(0x6a4a30)); for (int k = 0; k < 4; k++) mb.Box(.05f, .04f, .22f, .1f + k * .15f, .9f, 1.6f, Col.Hex(new uint[] { 0xff7a2a, 0xe0e0e0, 0x40a0ff, 0xffc24a }[k])); mb.Box(1.1f, .8f, .03f, 0, 1.6f, 1.95f, Col.Hex(0x1a3a6a)); mb.Sphere(.16f, -.2f, 1.2f, 1.6f, Col.Hex(0x3a9a4a)); break;
            case 2: for (int k = 0; k < 14; k++) { float x = -2.3f + k * .35f, y = 2.55f - MathF.Sin(k / 13f * MathF.PI) * .25f; mb.Sphere(.06f, x, y, -1.9f, Col.Hex(new uint[] { 0xffd890, 0x9ff4ff, 0xff9ac8 }[k % 3]), 1, 1, 1, 255, 3, 5); } for (int k = 0; k < 3; k++) mb.Box(.5f, .65f, .02f, -1.0f + k * .6f, 1.5f, 1.95f, Col.Hex(0xefe6d0)); mb.CylZ(.07f, .05f, 1.0f, 1.9f, .9f, .5f, Col.Hex(0xcfa050), 10); break;
            case 3: mb.Box(1.1f, .45f, .6f, .5f, .23f, 1.5f, Col.Hex(0x3a4a3a)); mb.Box(1.2f, .7f, .02f, 0, 1.7f, 1.95f, Col.Hex(0x2a8a4a)); mb.Box(1.2f, .15f, .025f, 0, 1.7f, 1.95f, Col.Hex(0xe0b84a)); for (int k = 0; k < 5; k++) mb.Sphere(.06f, -.6f + k * .17f, 1.15f, 1.9f, Col.Hex(0xe0b84a), 1, 1, 1, 0, 3, 6); break;
            default: mb.Sphere(.45f, .4f, .25f, 1.3f, Col.Hex(0xff7acb), 1, .55f, 1); for (int k = 0; k < 4; k++) mb.Box(.45f, .6f, .02f, -.9f + k * .5f, 1.5f + (k % 2) * .15f, 1.95f, Col.Hex(new uint[] { 0xff7acb, 0x5ad0ff, 0xffd45a, 0x7ee08a }[k])); for (int k = 0; k < 7; k++) mb.Box(.1f, .02f, .07f, -.4f + (float)rnd.NextDouble() * 1.6f, .07f, .7f + (float)rnd.NextDouble(), Col.Hex(new uint[] { 0xff5a6e, 0xffd45a, 0x5ad0ff, 0x7ee08a }[k % 4])); break;
        }
        return mb.Build();
    }
    void RenderCabin()
    {
        float t = T; float a = MathF.Sin(t * .25f) * .35f + cabAz;
        icam = new Camera3D(new Vector3(1.5f + MathF.Sin(a) * 1.5f, 1.75f, 1.7f), new Vector3(-.5f + MathF.Sin(a) * 1.1f, 1.0f, -.4f), Vector3.UnitY, 78, CameraProjection.Perspective);
        Rlgl.SetClipPlanes(.05, 50);
        Raylib.BeginMode3D(icam);
        lit.SetInterior(icam.Position, new[] { new Vector3(.4f, 2.3f, .4f), new Vector3(1.4f, 1.0f, -1.4f), new Vector3(-1.6f, 1.4f, -1.5f) }, new[] { 9f, 4f, 4f }, new[] { new Vector3(.9f, .75f, .55f), new Vector3(.1f, .3f, .4f), new Vector3(.3f, .25f, .45f) });
        lit.Draw(cabMesh, Matrix4x4.Identity);
        Raylib.EndMode3D();
    }
    void DrawCabinHud(float W, float H, float s)
    {
        var c = Data.Crew[cabShip!.idx]; var sh = cabShip; float cw = Math.Min(560 * s, W - 24 * s); var lines = Wrap(c.Bio, cw - 32 * s, 15 * s); float ch = (lines.Count + 3) * 21 * s + 70 * s;
        var r = new Rectangle((W - cw) / 2, H - ch - 14 * s, cw, ch); Box(r, new Color((byte)11, (byte)19, (byte)38, (byte)235));
        DText($"{c.Name}   callsign {c.Call}", r.X + 16 * s, r.Y + 12 * s, 19 * s, Rgb(c.Color)); float y = r.Y + 44 * s; foreach (var l in lines) { DText(l, r.X + 16 * s, y, 15 * s, Txt); y += 21 * s; }
        DText($"Loyalty {(int)sh.loy}%{(sh.indep ? " (independent)" : "")}   engines L{sh.lv.eng + 1}, lasers L{sh.lv.las + 1}, tanks L{sh.lv.tank + 1}, cargo L{sh.lv.car + 1}   {sh.trips} trips", r.X + 16 * s, y + 4 * s, 12 * s, Dim);
        if (Btn(new Rectangle(r.X + 16 * s, r.Y + ch - 46 * s, 160 * s, 34 * s), "LEAVE SHIP", 15 * s)) SetMode(cabFrom == Mode.Walk ? Mode.Walk : Mode.Cmd);
    }
}
