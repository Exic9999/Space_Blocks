// The captain's own ship: visuals, Fly mode (chase camera), Eva mode (on foot on an island, hand-mining ore), HUD and input.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    readonly ShipVis capVis = new();
    Vector3 capPos, capCamPos; float capYaw, capRollV, capPitchV; bool capCamInit;
    string lastCapState = "docked";
    // on-foot state
    float evaYaw = 1.2f, evaPitch; Mesh oreMesh; bool oreBuilt; float evaSpark;
    float flyHintT;

    void SyncCaptain()
    {
        if (capVis.Gfx == null) capVis.Gfx = Scene.BuildShip(5, new Levels());
        if (!oreBuilt)
        {
            var ob = new MeshBuilder();
            for (int k = 0; k < 3; k++) { var sv = ob.T; ob.T = Matrix4x4.CreateRotationZ((k - 1) * .35f) * Matrix4x4.CreateRotationY(k * 2.1f) * Matrix4x4.CreateTranslation((k - 1) * .35f, .55f, (k % 2) * .3f) * sv; ob.Octa(.32f - k * .04f, 2.4f - k * .4f, Col.Hex(0xb69cff), 255); ob.T = sv; }
            oreMesh = ob.Build(); oreBuilt = true;
        }
    }

    // ---- where the ship is and how it is drawn, per captain state ----
    void UpdateCaptainVis(float t, float dt)
    {
        var c = sim.G.cap; var v = capVis; var pad = Data.CapPad; bool ev = false, iv = false; Vector3 ep = new(c.x, c.alt, c.y); float yaw = YawFor(c.ang), iry = 0, irx = 0; Vector3 ip = default;
        float turn = mode == Mode.Fly ? sim.InTurn : 0, climb = mode == Mode.Fly ? sim.InClimb : 0;
        capRollV += ((-turn * .5f) - capRollV) * MathF.Min(1, dt * 4); capPitchV += ((climb * .25f) - capPitchV) * MathF.Min(1, dt * 4);
        switch (c.state)
        {
            case "docked": iv = true; ip = new(pad[0], .95f, pad[1]); break;
            case "launch":
                {
                    float u = c.lt; if (u < .7f) { iv = true; float k = Sm(u / .7f), kl = MathF.Min(1, k * 1.5f), kz = MathF.Max(0, (k - .35f) / .65f); ip = new(pad[0] * (1 - Sm(MathF.Min(1, kz * 1.2f))), .95f + 6.5f * Sm(kl), pad[1] + (36 - pad[1]) * kz); iry = MathF.PI * Sm(MathF.Min(1, kl * 1.4f)); }
                    if (u >= .6f) { float k = (u - .6f) / .4f; ep = ExW(0, 20, 200 + (330 - 200) * k); yaw = sim.G.exHead + MathF.PI; ev = true; }
                    break;
                }
            case "dock":
                {
                    float u = c.lt; if (u < .4f) { float k = u / .4f; ep = ExW(0, 20, 330 + (200 - 330) * k); yaw = sim.G.exHead; ev = true; }
                    if (u >= .3f) { iv = true; float k = Sm((u - .3f) / .7f), kz = MathF.Min(1, k * 1.6f), ky = MathF.Max(0, (k - .45f) / .55f); ip = new(pad[0] * Sm(MathF.Min(1, k * 1.3f)), .95f + 6.5f * (1 - Sm(ky)), 36 + (pad[1] - 36) * kz); irx = -.08f * (1 - ky); }
                    break;
                }
            default: ev = true; ep = new(c.x, c.alt + (c.state == "fly" ? MathF.Sin(t * 1.3f) * 1.5f : 0), c.y); break;
        }
        v.ExtVis = ev; v.IntVis = iv; v.ExtPos = ep; capPos = ep; capYaw = yaw;
        v.Int = Matrix4x4.CreateRotationY(iry) * Matrix4x4.CreateRotationX(irx) * Matrix4x4.CreateTranslation(ip);
        float sc = (mode == Mode.Eva && c.state == "landed") ? 1.15f : Data.ES;
        v.Ext = Matrix4x4.CreateScale(sc) * Matrix4x4.CreateRotationX(c.state == "fly" ? capPitchV : 0) * Matrix4x4.CreateRotationZ(c.state == "fly" ? capRollV : 0) * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(ep);
    }
    void DrawCaptainExt(Camera3D cam, float t)
    {
        var v = capVis; if (v.Gfx == null || !v.ExtVis) return; lit.Draw(v.Gfx.Mesh, v.Ext);
    }
    void DrawCaptainGlows(Camera3D cam, float t)
    {
        var v = capVis; if (v.Gfx == null || !v.ExtVis) return; var c = sim.G.cap; float sc = (mode == Mode.Eva && c.state == "landed") ? 1.15f : Data.ES;
        float gl = c.state is "fly" ? .3f + .7f * c.speed / Sim.CapMax : c.state is "launch" or "dock" or "lift" or "land" ? 1f : .2f;
        foreach (var g in v.Gfx.Glows) Billboard(cam, Vector3.Transform(g.Pos, v.Ext), g.Size * sc * (gl * (.9f + .15f * MathF.Sin(t * 30)) + .1f), Tint(g.Col, g.Op));
    }

    // ---- cameras ----
    void UpdateCaptainCam(float dt)
    {
        var c = sim.G.cap;
        if (mode == Mode.Fly)
        {
            Vector3 look, want;
            if (c.state is "launch" or "dock") { want = ExW(0, 70, 640); look = ExW(0, 30, 300); capCamInit = false; capCamPos = want; }
            else
            {
                var fwd = new Vector3(MathF.Cos(c.ang), 0, MathF.Sin(c.ang)); want = capPos - fwd * 78 + new Vector3(0, 28 + capPitchV * -40, 0); look = capPos + fwd * 70 + new Vector3(0, 4, 0);
                if (!capCamInit) { capCamPos = want; capCamInit = true; } capCamPos += (want - capCamPos) * MathF.Min(1, dt * 5);
            }
            camPos = capCamPos; cam3 = new Camera3D(camPos, look, Vector3.UnitY, 62, CameraProjection.Perspective);
        }
        else if (mode == Mode.Eva)
        {
            var i = sim.G.isl[Math.Clamp(c.island, 0, sim.G.isl.Count - 1)]; float gy = sim.GroundY(i, c.ex, c.ez);
            camPos = new Vector3(i.x + c.ex, gy + 1.7f, i.y + c.ez); var f = new Vector3(-MathF.Sin(evaYaw) * MathF.Cos(evaPitch), MathF.Sin(evaPitch), -MathF.Cos(evaYaw) * MathF.Cos(evaPitch));
            cam3 = new Camera3D(camPos, camPos + f, Vector3.UnitY, 72, CameraProjection.Perspective);
        }
    }

    // ---- ore nodes (only drawn up close, on the island the captain stands on) ----
    void DrawOreNodes(float t)
    {
        var c = sim.G.cap; if (mode != Mode.Eva || c.state != "landed") return; var i = sim.G.isl[c.island]; var nodes = Sim.OreNodes(i); int vis = sim.VisibleNodes(i); int near = sim.NearestNode(i, c.ex, c.ez);
        for (int k = 0; k < vis; k++)
        {
            float gy = sim.GroundY(i, nodes[k].x, nodes[k].z); var p = new Vector3(i.x + nodes[k].x, gy, i.y + nodes[k].z); float pulse = .8f + .2f * MathF.Sin(t * 2.4f + k);
            lit.Draw(oreMesh, Matrix4x4.CreateScale(k == near ? 2.0f : 1.7f) * Matrix4x4.CreateRotationY(k * 1.3f) * Matrix4x4.CreateTranslation(p), pulse);
        }
    }
    void DrawOreGlows(float t)
    {
        var c = sim.G.cap; if (mode != Mode.Eva || c.state != "landed") return; var i = sim.G.isl[c.island]; var nodes = Sim.OreNodes(i); int vis = sim.VisibleNodes(i);
        for (int k = 0; k < vis; k++) { float gy = sim.GroundY(i, nodes[k].x, nodes[k].z); Billboard(cam3, new Vector3(i.x + nodes[k].x, gy + .8f, i.y + nodes[k].z), 3.2f + .5f * MathF.Sin(t * 2.4f + k), Tint(Col.Hex(0xa08cff), .55f)); }
        if (evaSpark > 0 && sim.NearestNode(i, c.ex, c.ez) >= 0) { int n = sim.NearestNode(i, c.ex, c.ez); float gy = sim.GroundY(i, nodes[n].x, nodes[n].z); for (int k = 0; k < 6; k++) Billboard(cam3, new Vector3(i.x + nodes[n].x + MathF.Sin(t * 30 + k * 2) * .5f, gy + .6f + MathF.Abs(MathF.Sin(t * 25 + k)) * 1.2f, i.y + nodes[n].z + MathF.Cos(t * 30 + k * 3) * .5f), .5f, Tint(Col.Hex(0xffe0ff), .9f)); }
    }

    // ---- mode transitions driven by the captain's state ----
    void OnCapState(string from, string to)
    {
        bool inBody = mode is Mode.Walk or Mode.Fly or Mode.Eva; if (!inBody) return;
        switch (to)
        {
            case "docked": if (mode == Mode.Fly) { walk.x = Data.CapPad[0] - 4.6f; walk.z = Data.CapPad[1] + .5f; walk.yaw = -1.57f; walk.pitch = 0; SetMode(Mode.Walk); } break;
            case "landed": evaYaw = 1.57f; evaPitch = -.05f; SetMode(Mode.Eva); break;
            case "fly": case "launch": case "dock": case "land": case "lift": if (mode != Mode.Fly) { capCamInit = false; SetMode(Mode.Fly); } break;
        }
    }
    void ReturnToBody()
    {
        switch (sim.G.cap.state)
        {
            case "docked": EnterWalk(); break;
            case "landed": evaYaw = evaYaw; SetMode(Mode.Eva); break;
            default: capCamInit = false; SetMode(Mode.Fly); break;
        }
    }
    string BodyLabel() => sim.G.cap.state switch { "docked" => "BRIDGE", "landed" => "ISLAND", _ => "FLY" };

    // ---- input ----
    void HandleFlyInput(float dt)
    {
        var c = sim.G.cap; bool k = false;
        sim.InThr = (Raylib.IsKeyDown(KeyboardKey.W) ? 1 : 0) - (Raylib.IsKeyDown(KeyboardKey.S) ? 1 : 0); sim.InTurn = (Raylib.IsKeyDown(KeyboardKey.D) ? 1 : 0) - (Raylib.IsKeyDown(KeyboardKey.A) ? 1 : 0);
        sim.InClimb = (Raylib.IsKeyDown(KeyboardKey.Space) ? 1 : 0) - (Raylib.IsKeyDown(KeyboardKey.LeftControl) ? 1 : 0);
        k = sim.InThr != 0 || sim.InTurn != 0 || sim.InClimb != 0;
        if (k && c.auto != "") sim.CapAutoOff();
        if (c.state != "fly") { sim.InThr = sim.InTurn = sim.InClimb = 0; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) { var f = sim.G.isl.Where(i => i.found).ToList(); if (f.Count > 0) { int ix = selIsl == null ? -1 : f.IndexOf(selIsl); selIsl = f[(ix + 1) % f.Count]; } }
        if (Raylib.IsKeyPressed(KeyboardKey.G) || Raylib.IsKeyPressed(KeyboardKey.L)) { if (selIsl != null && selIsl.found) sim.CapAutoGoto(selIsl); else sim.Say("Pick a target first with Tab.", 0xffbe5a); }
        if (Raylib.IsKeyPressed(KeyboardKey.R)) sim.CapAutoHome();
        if (Raylib.IsKeyPressed(KeyboardKey.X)) sim.CapAutoOff();
        if (Raylib.IsKeyPressed(KeyboardKey.F)) { if (sim.CapCanDock()) sim.CapDock(); else if (!sim.CapLand()) sim.Say("Nothing to land on or dock with here.", 0xffbe5a); }
        if (Raylib.IsKeyPressed(KeyboardKey.N) || Raylib.IsKeyPressed(KeyboardKey.M)) { if (Raylib.IsKeyPressed(KeyboardKey.M)) ToggleMusic(); else SetMode(Mode.Cmd); }
    }
    void HandleEvaInput(float dt)
    {
        var c = sim.G.cap; if (c.state != "landed") return; var isl = sim.G.isl[c.island]; var d = Raylib.GetMouseDelta(); evaYaw -= d.X * .0025f; evaPitch = Math.Clamp(evaPitch - d.Y * .002f, -1.2f, 1.2f);
        float mx = 0, mz = 0; if (Raylib.IsKeyDown(KeyboardKey.W)) mz -= 1; if (Raylib.IsKeyDown(KeyboardKey.S)) mz += 1; if (Raylib.IsKeyDown(KeyboardKey.A)) mx -= 1; if (Raylib.IsKeyDown(KeyboardKey.D)) mx += 1;
        float l = Mx.Hypot(mx, mz); if (l > 1) { mx /= l; mz /= l; } float sp = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? 9f : 5f, sy = MathF.Sin(evaYaw), cy = MathF.Cos(evaYaw);
        c.ex += (mx * cy + mz * sy) * sp * dt; c.ez += (-mx * sy + mz * cy) * sp * dt;
        float r = Mx.Hypot(c.ex, c.ez), rmax = isl.s * .86f; if (r > rmax) { c.ex *= rmax / r; c.ez *= rmax / r; }
        var (lx, lz) = sim.LandSpot(isl); float sd = Mx.Hypot(c.ex - lx, c.ez - lz); if (sd < 3.4f) { float k = 3.4f / MathF.Max(sd, .01f); c.ex = lx + (c.ex - lx) * k; c.ez = lz + (c.ez - lz) * k; }
        UseTools(dt);
        if (Raylib.IsKeyPressed(KeyboardKey.F) && sd < 7.5f) { if (sim.CapTakeoff()) { capCamInit = false; SetMode(Mode.Fly); } }
        if (Raylib.IsKeyPressed(KeyboardKey.N)) SetMode(Mode.Cmd);
        if (Raylib.IsKeyPressed(KeyboardKey.M)) ToggleMusic();
    }

    // ---- HUD ----
    void DrawFlyHud(float W, float H, float s)
    {
        var c = sim.G.cap; var G = sim.G;
        Raylib.DrawCircleLinesV(new Vector2(W / 2, H / 2 + 40 * s), 14 * s, new Color((byte)200, (byte)230, (byte)255, (byte)90));
        // gauges (bottom left)
        var gr = new Rectangle(14 * s, H - 172 * s, 250 * s, 150 * s); Box(gr, new Color((byte)11, (byte)19, (byte)38, (byte)215));
        DText("CAPTAIN'S SHIP", gr.X + 12 * s, gr.Y + 8 * s, 14 * s, Accent);
        void Gauge(string label, float f, Color col, int row, string val) { float y = gr.Y + 34 * s + row * 26 * s; DText(label, gr.X + 12 * s, y, 12 * s, Dim); var b = new Rectangle(gr.X + 70 * s, y + 3 * s, 120 * s, 8 * s); Raylib.DrawRectangleRec(b, new Color((byte)0, (byte)0, (byte)0, (byte)130)); Raylib.DrawRectangleRec(new Rectangle(b.X, b.Y, b.Width * Math.Clamp(f, 0, 1), b.Height), col); DText(val, gr.X + 198 * s, y, 12 * s, Txt); }
        Gauge("SPEED", c.speed / Sim.CapMax, Accent, 0, $"{(int)c.speed}"); Gauge("ALT", (c.alt - 15) / 400f, Rgb(0x6eeba0), 1, $"{(int)c.alt}");
        Gauge("FUEL", c.fuel / Sim.CapFuelMax, c.fuel < 8 ? Rgb(0xff6464) : Rgb(0x40ffd0), 2, $"{c.fuel:F0}"); Gauge("ORE", c.ore / Sim.CapCarry, Rgb(0xa08cff), 3, $"{(int)c.ore}");
        string st = c.state switch { "launch" => "LAUNCHING", "dock" => "DOCKING", "land" => "LANDING", "lift" => "LIFTING OFF", _ => c.auto == "goto" ? "AUTOPILOT: TARGET" : c.auto == "home" ? "AUTOPILOT: HOME" : "MANUAL" };
        DText(st, gr.X + 12 * s, gr.Y + 128 * s, 12 * s, c.auto != "" ? Rgb(0xffbe5a) : Dim);
        // target + Executor pointers
        void Pointer(Vector3 wp, string label, Color col, float dist)
        {
            var fw = Vector3.Normalize(cam3.Target - cam3.Position); bool behind = Vector3.Dot(wp - cam3.Position, fw) < 0; var sp = Raylib.GetWorldToScreen(wp, cam3);
            bool off = behind || sp.X < 20 || sp.X > W - 20 || sp.Y < 60 * s || sp.Y > H - 40 * s;
            var cen = new Vector2(W / 2, H / 2); var dir = behind ? cen - sp : sp - cen; if (dir.Length() < 1) dir = new Vector2(1, 0); dir = Vector2.Normalize(dir);
            Vector2 pos = off ? new Vector2(Math.Clamp(cen.X + dir.X * W, 40, W - 40), Math.Clamp(cen.Y + dir.Y * H, 90 * s, H - 70 * s)) : sp;
            if (off) { float a = MathF.Atan2(dir.Y, dir.X); Raylib.DrawTriangle(pos + new Vector2(MathF.Cos(a) * 14, MathF.Sin(a) * 14), pos + new Vector2(MathF.Cos(a + 2.5f) * 10, MathF.Sin(a + 2.5f) * 10), pos + new Vector2(MathF.Cos(a - 2.5f) * 10, MathF.Sin(a - 2.5f) * 10), col); }
            else { Raylib.DrawRectangleLinesEx(new Rectangle(pos.X - 12, pos.Y - 12, 24, 24), 2, col); }
            DTextC($"{label} {(int)dist}", pos.X, pos.Y + (off ? 18 : 16) * s, 12 * s, col);
        }
        var ex = new Vector3(G.exx, 30, G.exy); Pointer(ex, "EXECUTOR", Color.White, Mx.Hypot(G.exx - c.x, G.exy - c.y));
        if (selIsl != null) Pointer(new Vector3(selIsl.x, selIsl.h + sim.Bob(selIsl), selIsl.y), selIsl.name.ToUpper(), Accent, Mx.Hypot(selIsl.x - c.x, selIsl.y - c.y));
        // prompts
        var land = sim.CapCanLand(); string hint = sim.CapCanDock() ? "[F] Dock with the Executor" : land != null ? $"[F] Land on {land.name}" : "";
        if (hint == "") foreach (var oi in G.isl) { float dh = Mx.Hypot(oi.x - c.x, oi.y - c.y); if (dh < oi.s * .85f && c.state == "fly") { hint = $"Over {oi.name}: hold Ctrl to descend, then press F to land"; break; } }
        if (hint != "") DTextC(hint, W / 2, H - 110 * s, 20 * s, Rgb(0xffe9a0));
        DTextC("W/S throttle   A/D turn   Space/Ctrl climb   Tab target   G autopilot   R home   N map", W / 2, H - 28 * s, 13 * s, Dim);
        if (c.fuel <= 0) DTextC("OUT OF FUEL - drifting home (R)", W / 2, 90 * s, 18 * s, Rgb(0xff6464));
    }
    void DrawEvaHud(float W, float H, float s)
    {
        var c = sim.G.cap; if (c.state != "landed") return; var isl = sim.G.isl[c.island]; int n = sim.NearestNode(isl, c.ex, c.ez);
        if (sim.HurtT > 0) { var rc = new Color((byte)255, (byte)30, (byte)30, (byte)(Math.Clamp(sim.HurtT * 260, 0, 120))); Raylib.DrawRectangleGradientV(0, 0, (int)W, (int)(H * .25f), rc, new Color((byte)255, (byte)30, (byte)30, (byte)0)); Raylib.DrawRectangleGradientV(0, (int)(H * .75f), (int)W, (int)(H * .25f), new Color((byte)255, (byte)30, (byte)30, (byte)0), rc); }
        if (sim.Monsters.Any(q => q.awake && !q.dead)) DTextC("THE TREES ARE ALIVE!  [2] laser  [1] pick axe  or run for the ship", W / 2, 96 * s, 17 * s, Rgb(0xff6a5a)); else if (sim.Haunted && sim.Monsters.Count > 0) DTextC("Something stirs in the trees...", W / 2, 96 * s, 15 * s, Rgb(0xffbe5a));
        Raylib.DrawCircleLinesV(new Vector2(W / 2, H / 2), 3, new Color((byte)200, (byte)230, (byte)255, (byte)170));
        DTextC(isl.name.ToUpper(), W / 2, 52 * s, 14 * s, Accent); DTextC($"{(int)isl.aeth} Aetherite left on this island", W / 2, 72 * s, 12 * s, Dim);
        { var hb = new Rectangle(14 * s, H - 112 * s, 250 * s, 24 * s); Box(hb, new Color((byte)11, (byte)19, (byte)38, (byte)215)); DText("HEALTH", hb.X + 10 * s, hb.Y + 4 * s, 13 * s, Dim); var bar = new Rectangle(hb.X + 74 * s, hb.Y + 7 * s, 160 * s, 10 * s); Raylib.DrawRectangleRec(bar, new Color((byte)0, (byte)0, (byte)0, (byte)140)); Raylib.DrawRectangleRec(new Rectangle(bar.X, bar.Y, bar.Width * Math.Clamp(c.hp / 100f, 0, 1), bar.Height), c.hp < 35 ? Rgb(0xff5a5a) : Rgb(0x6eeba0)); }
        var gr = new Rectangle(14 * s, H - 80 * s, 250 * s, 60 * s); Box(gr, new Color((byte)11, (byte)19, (byte)38, (byte)215));
        DText($"ORE PACK  {(int)c.ore}/{Sim.CapCarry}   ({(int)(c.ore * Sim.ChunkVal)} Aetherite)", gr.X + 12 * s, gr.Y + 8 * s, 14 * s, Rgb(0xb69cff));
        var b = new Rectangle(gr.X + 12 * s, gr.Y + 34 * s, 226 * s, 10 * s); Raylib.DrawRectangleRec(b, new Color((byte)0, (byte)0, (byte)0, (byte)130)); Raylib.DrawRectangleRec(new Rectangle(b.X, b.Y, b.Width * c.ore / Sim.CapCarry, b.Height), Rgb(0xa08cff));
        var (lx, lz) = sim.LandSpot(isl); float sd = Mx.Hypot(c.ex - lx, c.ez - lz);
        string hint = n >= 0 && c.ore < Sim.CapCarry ? (evaTool == 0 ? "[hold E or left-click] Swing the pick axe" : "[hold left-click] Laser the ore") : n >= 0 ? "Pack full - fly it back to the Executor" : sd < 7.5f ? "[F] Board your ship" : "Look for the glowing ore beside the crystals";
        DTextC(hint, W / 2, H - 120 * s, 20 * s, Rgb(0xffe9a0)); if (sd < 7.5f && n >= 0) DTextC("[F] Board your ship", W / 2, H - 92 * s, 14 * s, Dim);
        if (c.mineT > 0) { float f = c.mineT / 1.5f; var rb = new Rectangle(W / 2 - 80 * s, H / 2 + 40 * s, 160 * s, 10 * s); Raylib.DrawRectangleRec(rb, new Color((byte)0, (byte)0, (byte)0, (byte)150)); Raylib.DrawRectangleRec(new Rectangle(rb.X, rb.Y, rb.Width * f, rb.Height), Rgb(0xffe0ff)); }
        DTextC("WASD move   Shift run   1 pick axe   2 laser gun   left-click use   F ship   N map", W / 2, H - 28 * s, 13 * s, Dim);
        { float tx = W - 250 * s; DText("[1] PICK AXE", tx, H - 66 * s, 15 * s, evaTool == 0 ? Rgb(0xffe9a0) : Dim); DText("[2] LASER GUN", tx, H - 44 * s, 15 * s, evaTool == 1 ? Rgb(0x7fe8ff) : Dim); }
    }
}
