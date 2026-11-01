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
            case "walk-corridor": SetMode(Mode.Walk); walk.x = 0; walk.z = -8; walk.yaw = 0; break;
            case "cabin": OpenCabin(sim.G.ships[0]); break;
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
        Phase("cmd wide", () => { SetMode(Mode.Cmd); cam.follow = null; cam.dist = 3500; cam.el = .6f; cam.tx = Data.EXx; cam.tz = Data.EXy; });
        Phase("cmd close", () => { cam.dist = 500; cam.tx = Data.EXx; cam.tz = Data.EXy + 100; });
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
