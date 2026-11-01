// Main game: window, state, update loop, 3D world rendering.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public enum Mode { Title, Cmd, Walk, Cabin }

public sealed class CamState { public float az = .6f, el = .5f, dist = 1500, tx = Data.CX, ty = 0, tz = Data.CY; public Ship? follow; }
public sealed class IslandVis { public IslandGfx? Gfx; public float shown; }
public sealed class ShipVis { public ShipGfx? Gfx; public string Sig = ""; public Matrix4x4 Ext, Int; public bool ExtVis, IntVis; public Vector3 ExtPos; }

public sealed unsafe partial class Game
{
    // ---- core state ----
    public readonly Sim sim = new();
    Mode mode = Mode.Title;
    Ship? sel; Island? selIsl; bool selExec, execFollow;
    readonly CamState cam = new();
    readonly Walker walk = new();
    float T; // wall time
    bool quit;
    // ---- gfx ----
    LitShader lit = null!; Texture2D glowTex, reticleTex, fallTex, skyTex, whiteTex; Model skyModel;
    Mesh execHull, execTanks; List<GlowDef> execGlows = new();
    Mesh intMesh, intTank; List<PtLight> intLights = new(); List<ScreenDef> intScreens = new();
    readonly IslandVis[] islVis = new IslandVis[14]; readonly ShipVis[] shipVis = new ShipVis[5];
    IslandGfx? windowIsland;
    (Mesh body, Mesh arm, Mesh leg)[] figs = new (Mesh, Mesh, Mesh)[5];
    Camera3D cam3, icam;
    Vector3 camPos;
    // ---- storm fx ----
    sealed class StormFx { public float[] a = new float[18], r = new float[18], y = new float[18], sp = new float[18], sz = new float[18]; public Vector3[][] bolts = new Vector3[3][]; public bool[] on = new bool[3]; public float tb, flash; }
    readonly StormFx[] stormFx = new StormFx[3];
    readonly Random vr = new(5);

    public static string[] Args = Array.Empty<string>();

    public void Init(bool vsync, int w = 1280, int h = 720, bool audio = false)
    {
        var flags = ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint; if (vsync) flags |= ConfigFlags.VSyncHint;
        Raylib.SetConfigFlags(flags); Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(w, h, "Space Blocks"); Raylib.SetExitKey(KeyboardKey.Null);
        if (!vsync) Raylib.SetTargetFPS(0);
        Rlgl.DisableBackfaceCulling();
        lit = new LitShader();
        { var wi = Raylib.GenImageColor(4, 4, Color.White); whiteTex = Raylib.LoadTextureFromImage(wi); Raylib.UnloadImage(wi); }
        glowTex = Tex.Glow(); reticleTex = Tex.Reticle(); fallTex = Tex.Waterfall(); skyTex = Tex.Sky();
        skyModel = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(1, 32, 16));
        skyModel.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = skyTex;
        (execHull, execTanks, execGlows) = Scene.BuildExecutor();
        (intMesh, intTank, intLights, intScreens) = Interior.Build();
        windowIsland = Scene.BuildIsland(777, 70);
        for (int i = 0; i < 5; i++) figs[i] = Scene.BuildFigure(i);
        for (int i = 0; i < 3; i++) stormFx[i] = MakeStormFx();
        LoadUiFont();
        if (audio) InitAudio();
    }

    StormFx MakeStormFx()
    {
        var f = new StormFx();
        for (int k = 0; k < 18; k++) { f.a[k] = (float)vr.NextDouble() * 6.28f; f.r[k] = (float)vr.NextDouble(); f.y[k] = (float)vr.NextDouble() - .5f; f.sp[k] = .08f + (float)vr.NextDouble() * .2f; f.sz[k] = .7f + (float)vr.NextDouble() * .7f; }
        for (int k = 0; k < 3; k++) f.bolts[k] = new Vector3[9];
        return f;
    }

    // ---------------- new / load ----------------
    public void StartNew(bool crawl = true)
    {
        sim.NewGame(); sel = sim.G.ships[0]; selIsl = null; ResetViews(); SetMode(Mode.Cmd); SetMusic(true);
        if (crawl) StartCrawl(Data.Prologue, null);
    }
    public bool ContinueGame()
    {
        int? s = sim.LoadFromDisk(); if (s == null) return false;
        sel = sim.G.ships[s.Value]; selIsl = null; ResetViews(); SetMode(Mode.Cmd); return true;
    }
    void ResetViews()
    {
        foreach (var o in islVis) if (o?.Gfx != null) { Raylib.UnloadMesh(o.Gfx.Rock); Raylib.UnloadMesh(o.Gfx.Crystal); }
        foreach (var o in shipVis) if (o?.Gfx != null) Raylib.UnloadMesh(o.Gfx.Mesh);
        for (int i = 0; i < islVis.Length; i++) islVis[i] = new IslandVis();
        for (int i = 0; i < shipVis.Length; i++) shipVis[i] = new ShipVis();
        cam.follow = null; cam.tx = sim.G.exx; cam.tz = sim.G.exy; cam.ty = 0; cam.dist = 1500; cam.az = .6f; cam.el = .5f;
    }
    void SetMode(Mode m)
    {
        mode = m; showSheet = false;
        if (m == Mode.Walk) { Raylib.DisableCursor(); } else if (Raylib.IsCursorHidden()) Raylib.EnableCursor();
    }

    public void Boot() { sim.NewGame(1234); sel = sim.G.ships[0]; ResetViews(); mode = Mode.Title; }

    // ---------------- main loop ----------------
    public void Run()
    {
        double last = Raylib.GetTime();
        float saveT = 0;
        while (!Raylib.WindowShouldClose() && !quit)
        {
            double now = Raylib.GetTime(); float dt = (float)Math.Min(.1, now - last); last = now; T = (float)now;
            Frame(dt);
            if (mode != Mode.Title) { saveT += dt; if (saveT > 10) { saveT = 0; DoSave(); } }
        }
        if (mode != Mode.Title) DoSave();
        ShutdownAudio();
    }
    void DoSave() => sim.SaveToDisk(Math.Max(0, sim.G.ships.IndexOf(sel!)));

    public void Frame(float dt)
    {
        bool paused = showSheet || showMenu || crawlActive;
        if (mode != Mode.Title && !paused) sim.Step(dt * sim.Speed);
        if (sim.CrawlRequested) { sim.CrawlRequested = false; StartCrawl(Data.Epilogue, null); }
        if (sim.G.ships.Count > 0) { SyncVis(); UpdateShips(T, dt); }
        HandleInput(dt); UpdateAudio(dt);
        UpdateCam(dt);
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color((byte)5, (byte)7, (byte)16, (byte)255));
        switch (mode)
        {
            case Mode.Walk: RenderInterior(); break;
            case Mode.Cabin: RenderCabin(); break;
            default: RenderExterior(); break;
        }
        DrawHud(dt);
        Raylib.EndDrawing();
        if (sim.ScanFx > 0) sim.ScanFx -= dt;
    }

    // ---------------- vis sync ----------------
    void SyncVis()
    {
        foreach (var i in sim.G.isl)
        {
            var v = islVis[i.id]; if (v == null) v = islVis[i.id] = new IslandVis();
            if (i.found && v.Gfx == null) { v.Gfx = Scene.BuildIsland(i.seed, i.s); v.shown = 0; }
            if (v.Gfx != null && v.shown < 1) v.shown = Math.Min(1, v.shown + .03f);
        }
        foreach (var s in sim.G.ships)
        {
            var v = shipVis[s.idx]; if (v == null) v = shipVis[s.idx] = new ShipVis();
            string sig = $"{s.lv.eng},{s.lv.las},{s.lv.tank},{s.lv.car},{s.lv.hull}";
            if (v.Gfx == null || v.Sig != sig) { if (v.Gfx != null) Raylib.UnloadMesh(v.Gfx.Mesh); v.Gfx = Scene.BuildShip(s.idx, s.lv); v.Sig = sig; }
        }
    }
    Vector3 ExW(float lx, float ly, float lz) { var p = sim.ExPt(lx, lz); return new Vector3(p.x, ly, p.y); }
    Matrix4x4 ExM => Matrix4x4.CreateRotationY(sim.G.exHead) * Matrix4x4.CreateTranslation(sim.G.exx, 0, sim.G.exy);
    static float YawFor(float a) => MathF.Atan2(-MathF.Cos(a), -MathF.Sin(a));
    static float LerpAng(float a, float b, float t) { float d = (((b - a + MathF.PI) % MathF.Tau) + MathF.Tau) % MathF.Tau - MathF.PI; return a + d * t; }
    static float Sm(float x) => x * x * (3 - 2 * x);

    void UpdateShips(float t, float dt)
    {
        foreach (var s in sim.G.ships)
        {
            var v = shipVis[s.idx]; var pad = Data.Pads[s.idx % 5];
            float da = 60; var ti = s.target >= 0 ? sim.G.isl[s.target] : null;
            if (s.state == "mining" && ti != null) da = ti.h + ti.s * .6f + 25; else if (s.state == "out" && ti != null) da = ti.h + ti.s * .6f + 60;
            else if (s.state == "in") { float d = Mx.Hypot(s.x - sim.G.exx, s.y - sim.G.exy); da = d < 500 ? 20 : 60; } else if (s.state is "landing" or "takeoff" or "docked") da = 20;
            s.alt += (da - s.alt) * MathF.Min(1, dt * 1.2f);
            bool ev = true, iv = false; float? yaw = null; float roll = 0;
            Vector3 ep = default, ip = default; float irx = 0, iry = 0;
            if (s.state == "docked") { ev = false; iv = true; ip = new(pad[0], .95f, pad[1]); }
            else if (s.state == "landing")
            {
                float u = s.lt;
                if (u < .4f) { float k = u / .4f; ep = ExW(0, 20, 330 + (200 - 330) * k); yaw = sim.G.exHead; } else ev = false;
                if (u >= .3f)
                {
                    iv = true; float k = Sm((u - .3f) / .7f), kz = MathF.Min(1, k * 1.6f), ky = MathF.Max(0, (k - .45f) / .55f);
                    ip = new(pad[0] * Sm(MathF.Min(1, k * 1.3f)), .95f + 6.5f * (1 - Sm(ky)), 36 + (pad[1] - 36) * kz); irx = -.08f * (1 - ky);
                }
            }
            else if (s.state == "takeoff")
            {
                float u = s.lt;
                if (u < .7f)
                {
                    iv = true; float k = Sm(u / .7f), kl = MathF.Min(1, k * 1.5f), kz = MathF.Max(0, (k - .35f) / .65f);
                    ip = new(pad[0] * (1 - Sm(MathF.Min(1, kz * 1.2f))), .95f + 6.5f * Sm(kl), pad[1] + (36 - pad[1]) * kz); iry = MathF.PI * Sm(MathF.Min(1, kl * 1.4f));
                }
                if (u >= .6f) { float k = (u - .6f) / .4f; ep = ExW(0, 20, 200 + (330 - 200) * k); yaw = sim.G.exHead + MathF.PI; } else ev = false;
            }
            else
            {
                ep = new(s.x, s.alt + MathF.Sin(t * 1.3f + s.idx) * 3, s.y); float want = YawFor(s.ang);
                s.yaw = LerpAng(s.yaw, want, MathF.Min(1, dt * 3)); yaw = s.yaw; roll = MathF.Sin(t * .9f + s.idx) * .08f + (want - s.yaw) * .4f;
            }
            if (yaw != null) { if (s.state != "out" && s.state != "mining") s.yaw = yaw.Value; }
            float stormShake = s.inStorm ? ((float)vr.NextDouble() - .5f) * .35f : 0;
            v.ExtVis = ev; v.IntVis = iv; v.ExtPos = ep;
            v.Ext = Matrix4x4.CreateScale(Data.ES) * Matrix4x4.CreateRotationZ(roll + stormShake) * Matrix4x4.CreateRotationY(yaw ?? 0) * Matrix4x4.CreateTranslation(ep);
            v.Int = Matrix4x4.CreateRotationY(iry) * Matrix4x4.CreateRotationX(irx) * Matrix4x4.CreateTranslation(ip);
        }
    }

    // ---------------- camera ----------------
    void UpdateCam(float dt)
    {
        if (mode == Mode.Walk || mode == Mode.Cabin) return;
        if (execFollow && cam.follow == null) { float k0 = MathF.Min(1, dt * 3); cam.tx += (sim.G.exx - cam.tx) * k0; cam.ty += (0 - cam.ty) * k0; cam.tz += (sim.G.exy - cam.tz) * k0; }
        if (cam.follow != null)
        {
            var v = shipVis[cam.follow.idx]; Vector3 p = cam.follow.state == "docked" || v == null ? ExW(0, 20, 190) : v.ExtPos;
            float k = MathF.Min(1, dt * 3); cam.tx += (p.X - cam.tx) * k; cam.ty += (p.Y - cam.ty) * k; cam.tz += (p.Z - cam.tz) * k;
        }
        float ce = MathF.Cos(cam.el);
        camPos = new(cam.tx + cam.dist * ce * MathF.Sin(cam.az), cam.ty + cam.dist * MathF.Sin(cam.el), cam.tz + cam.dist * ce * MathF.Cos(cam.az));
        cam3 = new Camera3D(camPos, new Vector3(cam.tx, cam.ty, cam.tz), Vector3.UnitY, 55, CameraProjection.Perspective);
    }

    // ---------------- exterior rendering ----------------
    void DrawSky(Vector3 at, float radius)
    {
        Rlgl.DisableDepthTest(); Rlgl.DisableDepthMask();
        Raylib.DrawModelEx(skyModel, at, Vector3.UnitY, 0, new Vector3(radius), Color.White);
        Rlgl.EnableDepthMask(); Rlgl.EnableDepthTest();
    }
    void Billboard(Camera3D c, Vector3 p, float size, Color col, Texture2D? tex = null) => Raylib.DrawBillboard(c, tex ?? glowTex, p, size, col);
    static Color Tint(Color c, float op) => new(c.R, c.G, c.B, (byte)Math.Clamp(op * 255, 0, 255));

    void RenderExterior()
    {
        float t = T; var G = sim.G;
        Rlgl.SetClipPlanes(2, 80000);
        Raylib.BeginMode3D(cam3);
        DrawSky(camPos, 40000);
        lit.SetExterior(camPos);
        // islands
        foreach (var i in G.isl)
        {
            var v = islVis[i.id]; if (v?.Gfx == null) continue;
            float bob = MathF.Sin(t * .5f + i.seed) * 4; float f = MathF.Max(.15f, i.aeth / i.max), p = .8f + .2f * MathF.Sin(t * 2.4f + i.seed);
            float sc = MathF.Max(.01f, v.shown);
            lit.Draw(v.Gfx.Rock, Matrix4x4.CreateScale(sc) * Matrix4x4.CreateTranslation(i.x, i.h + bob, i.y));
            float R = i.s, cs = (.35f + .65f * f) * R * sc;
            lit.Draw(v.Gfx.Crystal, Matrix4x4.CreateScale(cs) * Matrix4x4.CreateTranslation(i.x - R * .25f * sc, i.h + bob + v.Gfx.Top * sc, i.y - R * .1f * sc), (.6f + .9f * f) * p);
        }
        // executor
        var exM = ExM;
        lit.Draw(execHull, exM); float fill = MathF.Min(1, G.aeth / 600f);
        lit.Draw(execTanks, exM, .25f + 1.1f * fill);
        // ships
        foreach (var s in G.ships) { var v = shipVis[s.idx]; if (v?.Gfx != null && v.ExtVis) lit.Draw(v.Gfx.Mesh, v.Ext); }
        // ---- additive / transparent effects ----
        Rlgl.DisableDepthMask(); Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var g in execGlows) Billboard(cam3, Vector3.Transform(g.Pos, exM), g.Size * (g.Size > 50 && g.Op > .9f ? .92f + .08f * MathF.Sin(t * 9 + g.Pos.X) : 1), Tint(g.Col, g.Op));
        var ff = ExW(0, 19, 239); DrawQuadAdd(ff, 110, 32, sim.G.exHead, Tint(Col.Hex(0x40a8ff), .2f + .1f * MathF.Sin(t * 3)));
        foreach (var i in G.isl)
        {
            var v = islVis[i.id]; float bob = MathF.Sin(t * .5f + i.seed) * 4;
            if (v?.Gfx != null)
            {
                float R = i.s, sc = MathF.Max(.01f, v.shown), f = MathF.Max(.15f, i.aeth / i.max), p = .8f + .2f * MathF.Sin(t * 2.4f + i.seed);
                Billboard(cam3, new(i.x - R * .25f * sc, i.h + bob + (v.Gfx.Top + R * .2f) * sc, i.y - R * .1f * sc), R * .9f * sc, Tint(Col.Hex(0x9d86ff), .8f * f * p));
                DrawWaterfall(i, v, bob, t);
            }
            else Billboard(cam3, new(i.x, i.h + bob, i.y), i.s * 2.2f, Tint(Col.Hex(0x6a8cff), .25f + .15f * MathF.Sin(t * 2 + i.id)));
        }
        foreach (var s in G.ships)
        {
            var v = shipVis[s.idx]; if (v?.Gfx == null) continue; var st = Sim.StatOf(s);
            bool moving = s.state is "out" or "in" or "landing" or "takeoff"; float gl = moving ? 1f : .25f;
            if (v.ExtVis) foreach (var g in v.Gfx.Glows) Billboard(cam3, Vector3.Transform(g.Pos, v.Ext), g.Size * Data.ES * v.Gfx.Boost * (gl * (.9f + .15f * MathF.Sin(t * 30 + s.idx)) + .1f), Tint(g.Col, g.Op));
            if (s.state == "mining" && s.target >= 0 && v.ExtVis)
            {
                var i = G.isl[s.target]; var a = v.ExtPos; var b = new Vector3(i.x - i.s * .25f, i.h + i.s * .14f, i.y - i.s * .1f);
                Raylib.DrawCylinderEx(a, b, 1.1f + MathF.Sin(t * 40) * .35f, 1.1f, 6, Tint(Col.Hex(0xff5a70), .75f));
                Billboard(cam3, b, 36 + MathF.Sin(t * 30) * 10, Tint(Col.Hex(0xff8aa0), .9f));
            }
        }
        if (sim.ScanFx > 0) { float k = 1 - sim.ScanFx / 1.4f; DrawRingAdd(new Vector3(sim.G.exx, 30, sim.G.exy), (700 + 260 * G.upSensors) * k, Tint(Col.Hex(0x5adcff), .5f * (1 - k))); }
        Raylib.EndBlendMode(); Rlgl.EnableDepthMask();
        DrawStorms(t);
        DrawSelection(t);
        Raylib.EndMode3D();
    }

    static void RlQuadVerts(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, float v0 = 0, float v1 = 1)
    {
        Rlgl.Color4ub(col.R, col.G, col.B, col.A);
        Rlgl.TexCoord2f(0, v0); Rlgl.Vertex3f(a.X, a.Y, a.Z); Rlgl.TexCoord2f(1, v0); Rlgl.Vertex3f(b.X, b.Y, b.Z);
        Rlgl.TexCoord2f(1, v1); Rlgl.Vertex3f(c.X, c.Y, c.Z); Rlgl.TexCoord2f(0, v1); Rlgl.Vertex3f(d.X, d.Y, d.Z);
    }
    void DrawQuadAdd(Vector3 c, float w, float h, float rotY, Color col)
    {
        float cs = MathF.Cos(rotY), sn = MathF.Sin(rotY); var r = new Vector3(cs, 0, -sn) * (w / 2); var u = new Vector3(0, h / 2, 0);
        Rlgl.SetTexture(whiteTex.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(col.R, col.G, col.B, col.A);
        var q0 = c - r - u; var q1 = c + r - u; var q2 = c + r + u; var q3 = c - r + u;
        Rlgl.TexCoord2f(0, 0); Rlgl.Vertex3f(q0.X, q0.Y, q0.Z); Rlgl.TexCoord2f(1, 0); Rlgl.Vertex3f(q1.X, q1.Y, q1.Z);
        Rlgl.TexCoord2f(1, 1); Rlgl.Vertex3f(q2.X, q2.Y, q2.Z); Rlgl.TexCoord2f(0, 1); Rlgl.Vertex3f(q3.X, q3.Y, q3.Z);
        Rlgl.End(); Rlgl.SetTexture(0);
    }
    void DrawRingAdd(Vector3 c, float r, Color col)
    {
        const int N = 64; Vector3 prev = c + new Vector3(r, 0, 0);
        for (int k = 1; k <= N; k++) { float a = k * MathF.Tau / N; var p = c + new Vector3(MathF.Cos(a) * r, 0, MathF.Sin(a) * r); for (int j = -1; j <= 1; j++) Raylib.DrawLine3D(prev + new Vector3(0, j * 2, 0), p + new Vector3(0, j * 2, 0), col); prev = p; }
    }
    void DrawWaterfall(Island i, IslandVis v, float bob, float t)
    {
        float R = i.s, sc = MathF.Max(.01f, v.shown), top = v.Gfx!.Top * sc;
        float wx = v.Gfx.Wx * sc, wz = v.Gfx.Wz * sc, h = R * 2.6f * sc, w = R * .16f * sc;
        float wa = MathF.Atan2(wz, wx); Vector3 right = new(-MathF.Sin(wa), 0, MathF.Cos(wa));
        Vector3 c = new(i.x + wx, i.h + bob + top - h / 2, i.y + wz); float scroll = -(t * .6f) * 1f, reps = R * 2.4f / 90f * sc;
        Rlgl.SetTexture(fallTex.Id); Rlgl.Begin(DrawMode.Quads);
        RlQuadVerts(c - right * w / 2 + new Vector3(0, h / 2, 0), c + right * w / 2 + new Vector3(0, h / 2, 0), c + right * w / 2 - new Vector3(0, h / 2, 0), c - right * w / 2 - new Vector3(0, h / 2, 0), Col.Hex(0xbfe4ff, 230), scroll, scroll + reps * 2);
        Rlgl.End(); Rlgl.SetTexture(0);
        Billboard(cam3, new(c.X, i.h + bob + top - h, c.Z), R * .7f * sc, Tint(Col.Hex(0xaee0ff), .35f));
    }
    void DrawStorms(float t)
    {
        var G = sim.G;
        for (int k = 0; k < 3; k++)
        {
            if (k >= G.storms.Count) continue; var q = G.storms[k]; var f = stormFx[k]; float R = q.r;
            float fade = MathF.Max(0, MathF.Min(1, MathF.Min(q.age / 8, (q.life - q.age) / 8)));
            var pc = new Vector3(q.x, 40, q.y);
            Rlgl.DisableDepthMask();
            for (int c = 0; c < 18; c++)
            {
                f.a[c] += f.sp[c] * Raylib.GetFrameTime();
                var p = pc + new Vector3(MathF.Cos(f.a[c]) * f.r[c] * R * .85f, f.y[c] * R * .55f, MathF.Sin(f.a[c]) * f.r[c] * R * .85f);
                Billboard(cam3, p, R * f.sz[c] * 1.15f * 1.5f, Tint(c % 3 != 0 ? Col.Hex(0x4a38b0) : Col.Hex(0x2e64ac), .3f * fade));
            }
            f.tb -= Raylib.GetFrameTime();
            if (f.tb <= 0)
            {
                f.tb = .1f + (float)vr.NextDouble() * .25f; bool on = vr.NextDouble() < .55; f.flash = on ? .16f * fade : 0;
                for (int b = 0; b < 3; b++)
                {
                    f.on[b] = on && fade > .3f; float bx0 = ((float)vr.NextDouble() - .5f) * R * 1.2f, bz0 = ((float)vr.NextDouble() - .5f) * R * 1.2f;
                    for (int i = 0; i < 9; i++) { float fr = i / 8f, jx = i > 0 && i < 8 ? 1 : 0; f.bolts[b][i] = new Vector3(bx0 + ((float)vr.NextDouble() - .5f) * R * .18f * jx, R * .55f - fr * R * 1.1f, bz0 + ((float)vr.NextDouble() - .5f) * R * .18f * jx); }
                }
            }
            Raylib.BeginBlendMode(BlendMode.Additive);
            for (int b = 0; b < 3; b++) if (f.on[b] && f.bolts[b][8] != default) for (int i = 0; i < 8; i++) Raylib.DrawLine3D(pc + f.bolts[b][i], pc + f.bolts[b][i + 1], new Color((byte)207, (byte)228, (byte)255, (byte)255));
            Billboard(cam3, pc, R * 1.1f, Tint(Col.Hex(0xaec8ff), f.flash * 3));
            Raylib.EndBlendMode(); Rlgl.EnableDepthMask();
        }
    }
    void DrawSelection(float t)
    {
        if (sim.G.exMoving)
        {
            var dest = new Vector3(sim.G.exDestX, 30, sim.G.exDestY); var from = new Vector3(sim.G.exx, 30, sim.G.exy); var dd = dest - from; float len = dd.Length(); int n = Math.Max(1, (int)(len / 60));
            Rlgl.DisableDepthMask(); Rlgl.DisableDepthTest();
            for (int k = 0; k < n; k += 2) Raylib.DrawLine3D(from + dd * (k / (float)n), from + dd * ((k + 1) / (float)n), Col.Hex(0xffffff, 160));
            Raylib.DrawBillboardPro(cam3, reticleTex, new Rectangle(0, 0, reticleTex.Width, reticleTex.Height), dest, Vector3.UnitY, new Vector2(160, 160), new Vector2(80, 80), t * -40f, Col.Hex(0xffffff));
            Rlgl.EnableDepthTest(); Rlgl.EnableDepthMask();
        }
        if (selIsl != null && selIsl.found)
        {
            var p = new Vector3(selIsl.x, selIsl.h, selIsl.y); float r = selIsl.s * 3.6f;
            Rlgl.DisableDepthMask(); Rlgl.DisableDepthTest();
            Raylib.DrawBillboardPro(cam3, reticleTex, new Rectangle(0, 0, reticleTex.Width, reticleTex.Height), p, Vector3.UnitY, new Vector2(r, r), new Vector2(r / 2, r / 2), t * 35f, Color.White);
            if (sel != null && shipVis[sel.idx]?.ExtVis == true)
            {
                var a = shipVis[sel.idx].ExtPos; var d = p - a; float len = d.Length(); int n = (int)(len / 50);
                for (int k = 0; k < n; k += 2) Raylib.DrawLine3D(a + d * (k / (float)n), a + d * ((k + 1) / (float)n), Col.Hex(0x5adcff, 180));
            }
            Rlgl.EnableDepthTest(); Rlgl.EnableDepthMask();
        }
    }

    // ---------------- interior rendering ----------------
    Vector3[] lpos = new Vector3[8], lcol = new Vector3[8]; float[] lrad = new float[8];
    void RenderInterior()
    {
        float t = T; var G = sim.G;
        icam = new Camera3D(new Vector3(walk.x, 1.65f + MathF.Sin(walk.bob * 7) * .025f, walk.z),
            new Vector3(walk.x, 1.65f, walk.z) + new Vector3(-MathF.Sin(walk.yaw) * MathF.Cos(walk.pitch), MathF.Sin(walk.pitch), -MathF.Cos(walk.yaw) * MathF.Cos(walk.pitch)), Vector3.UnitY, 72, CameraProjection.Perspective);
        Rlgl.SetClipPlanes(.08, 2000);
        Raylib.BeginMode3D(icam);
        DrawSky(icam.Position, 900);
        // nearest 8 lights
        var order = intLights.OrderBy(l => Vector3.DistanceSquared(l.Pos, icam.Position)).Take(8).ToList();
        for (int i = 0; i < order.Count; i++) { lpos[i] = order[i].Pos; lcol[i] = order[i].Col; lrad[i] = order[i].Rad; }
        lit.SetInterior(icam.Position, lpos.Take(order.Count).ToArray(), lrad.Take(order.Count).ToArray(), lcol.Take(order.Count).ToArray());
        lit.Draw(intMesh, Matrix4x4.Identity);
        // window island
        lit.SetExterior(icam.Position); lit.Draw(windowIsland!.Rock, Matrix4x4.CreateScale(1.4f) * Matrix4x4.CreateTranslation(-60, -6, -300));
        lit.SetInterior(icam.Position, lpos.Take(order.Count).ToArray(), lrad.Take(order.Count).ToArray(), lcol.Take(order.Count).ToArray());
        float fill = MathF.Min(1, G.aeth / 600f);
        foreach (var tk in Walker.Tanks) { float h = MathF.Max(.05f, 10 * fill); lit.Draw(intTank, Matrix4x4.CreateScale(1, h, 1) * Matrix4x4.CreateTranslation(tk[0], .5f + h / 2, tk[1])); }
        foreach (var s in G.ships) { var v = shipVis[s.idx]; if (v?.Gfx != null && v.IntVis) lit.Draw(v.Gfx.Mesh, v.Int); }
        foreach (var s in G.ships)
        {
            bool show = s.state == "docked" && (s.ph is "exit" or "idle" or "back"); if (!show) continue;
            var f = figs[s.idx]; float sw = s.walking ? MathF.Sin(t * 9 + s.idx) * .7f : 0;
            var basis = Matrix4x4.CreateRotationY(s.pa) * Matrix4x4.CreateTranslation(s.px, 0, s.pz);
            lit.Draw(f.body, basis);
            lit.Draw(f.leg, Matrix4x4.CreateRotationX(sw) * Matrix4x4.CreateTranslation(-.13f, .84f, 0) * basis); lit.Draw(f.leg, Matrix4x4.CreateRotationX(-sw) * Matrix4x4.CreateTranslation(.13f, .84f, 0) * basis);
            lit.Draw(f.arm, Matrix4x4.CreateRotationX(-sw * .8f) * Matrix4x4.CreateTranslation(-.34f, 1.42f, 0) * basis); lit.Draw(f.arm, Matrix4x4.CreateRotationX(sw * .8f) * Matrix4x4.CreateTranslation(.34f, 1.42f, 0) * basis);
        }
        DrawHologram(t); DrawScreens(t);
        Rlgl.DisableDepthMask(); Raylib.BeginBlendMode(BlendMode.Additive);
        DrawQuadAdd(new Vector3(0, 6.5f, 34), 32, 12, 0, Tint(Col.Hex(0x2a70c0), .14f + .06f * MathF.Sin(t * 2.4f)));
        foreach (var tk in Walker.Tanks) Billboard(icam, new Vector3(tk[0], 5, tk[1]), 7, Tint(Col.Hex(0x8a6aff), .35f));
        foreach (var s in G.ships) { var v = shipVis[s.idx]; if (v?.Gfx != null && v.IntVis) { bool mv = s.state is "landing" or "takeoff"; foreach (var g in v.Gfx.Glows) Billboard(icam, Vector3.Transform(g.Pos, v.Int), g.Size * ((mv ? 1f : .25f) * (.9f + .1f * MathF.Sin(t * 30)) + .1f), Tint(g.Col, g.Op)); } }
        Raylib.EndBlendMode(); Rlgl.EnableDepthMask();
        Raylib.EndMode3D();
    }
    void DrawHologram(float t)
    {
        var G = sim.G; float k = 3.4f / Data.W; var o = new Vector3(0, .98f, -28);
        Raylib.DrawCylinderEx(o + new Vector3(0, .02f, 0), o + new Vector3(0, .05f, 0), 2.3f, 2.3f, 24, new Color((byte)30, (byte)90, (byte)150, (byte)90));
        foreach (var i in G.isl)
        {
            if (!i.found) continue; var (lx, lz) = sim.ToLocal(i.x - G.exx, i.y - G.exy); var p = o + new Vector3(lx * k, .12f + i.h * k + .1f, lz * k);
            Raylib.DrawCylinderEx(p, p + new Vector3(0, .2f * (.7f + .5f * i.aeth / i.max), 0), .0f, .09f, 6, i == selIsl ? Col.Hex(0x5adcff) : Col.Hex(0x58e08a));
        }
        Raylib.DrawCylinderEx(o + new Vector3(0, .1f, 0), o + new Vector3(0, .5f, 0), .12f, 0, 3, Color.White);
        foreach (var s in G.ships) if (s.state != "docked") { var (sx, sz) = sim.ToLocal(s.x - G.exx, s.y - G.exy); Raylib.DrawSphere(o + new Vector3(sx * k, .35f + MathF.Sin(t * 2 + s.idx) * .02f, sz * k), .05f, Col.Hex(s.color)); }
        foreach (var q in G.storms) { var (qx, qz) = sim.ToLocal(q.x - G.exx, q.y - G.exy); Raylib.DrawCylinderEx(o + new Vector3(qx * k, .3f, qz * k), o + new Vector3(qx * k, .6f, qz * k), q.r * k, q.r * k, 16, new Color((byte)138, (byte)90, (byte)255, (byte)70)); }
    }
    void DrawScreens(float t)
    {
        for (int sI = 0; sI < intScreens.Count; sI++)
        {
            var sc = intScreens[sI]; int N = 24; Vector3 prev = default;
            for (int x = 0; x <= N; x++)
            {
                float u = x / (float)N, xx = u * 128, y = 32 + MathF.Sin(xx * .08f + t * (1.2f + sI * .3f)) * (10 + sI % 3 * 4) + MathF.Sin(xx * .21f + t * 2) * 4;
                var p = sc.Center + sc.Right * ((u - .5f) * sc.W * .92f) + sc.Up * (-(y - 32) / 64f * sc.H) + Vector3.Cross(sc.Right, sc.Up) * .04f;
                if (x > 0) Raylib.DrawLine3D(prev, p, Col.Hex(0x5adcff)); prev = p;
            }
        }
    }

    // ---------------- picking ----------------
    void Tap(Vector2 m)
    {
        var ray = Raylib.GetScreenToWorldRay(m, cam3); float bestD = float.MaxValue; Ship? bs = null; Island? bi = null;
        foreach (var s in sim.G.ships) { var v = shipVis[s.idx]; if (v == null || !v.ExtVis) continue; var h = Raylib.GetRayCollisionSphere(ray, v.ExtPos, 34); if (h.Hit && h.Distance < bestD) { bestD = h.Distance; bs = s; bi = null; } }
        foreach (var i in sim.G.isl) { var h = Raylib.GetRayCollisionSphere(ray, new Vector3(i.x, i.h, i.y), i.s * 1.25f); if (h.Hit && h.Distance < bestD) { bestD = h.Distance; bi = i; bs = null; } }
        if (bs != null) { sel = bs; selExec = false; selIsl = null; cam.follow = bs; return; }
        if (bi != null) { selIsl = bi; return; }
        if (selExec && ray.Direction.Y < -1e-4f) { float tt = -ray.Position.Y / ray.Direction.Y; var gp = ray.Position + ray.Direction * tt; sim.SailTo(gp.X, gp.Z); return; }
        Matrix4x4.Invert(ExM, out var inv); var lray = new Ray(Vector3.Transform(ray.Position, inv), Vector3.TransformNormal(ray.Direction, inv));
        var eh = Raylib.GetRayCollisionBox(lray, new BoundingBox(new Vector3(-170, 0, -250), new Vector3(170, 140, 250)));
        if (eh.Hit) { var d = sim.G.ships.FirstOrDefault(s => s.state == "docked"); if (d != null) { sel = d; selIsl = null; cam.follow = null; cam.tx = sim.G.exx; cam.tz = sim.G.exy; cam.ty = 0; } }
    }
}
