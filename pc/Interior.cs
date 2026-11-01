// Interior of the Executor: hangar, corridor, bridge. One merged static mesh + lights/screens for dynamic bits.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed class ScreenDef { public Vector3 Center, Right, Up; public float W, H; }
public sealed class PtLight { public Vector3 Pos; public Vector3 Col; public float Rad; }

public static class Interior
{
    static Color C(uint rgb) => Col.Hex(rgb);

    public static (Mesh mesh, Mesh tank, List<PtLight> lights, List<ScreenDef> screens) Build()
    {
        var mb = new MeshBuilder(); var lights = new List<PtLight>(); var screens = new List<ScreenDef>();
        var floorA = C(0x2c3446); var floorB = C(0x333c50); var wallA = C(0x3a4358); var wallB = C(0x2e3548); var trim = C(0x58627c); var ceil = C(0x1a2030);
        var topWall = C(0x2a3144); var cons = C(0x2a3144); var dkc = C(0x1b2030); var cyan = C(0x5adcff);
        void Floor(float x0, float x1, float z0, float z1)
        {
            for (float x = x0; x < x1 - .01f; x += 4) for (float z = z0; z < z1 - .01f; z += 4)
            {
                bool alt = ((int)MathF.Floor((x - x0) / 4) + (int)MathF.Floor((z - z0) / 4)) % 2 == 0;
                float w = MathF.Min(4, x1 - x), d = MathF.Min(4, z1 - z); mb.Box(w - .06f, .2f, d - .06f, x + w / 2, -.1f, z + d / 2, alt ? floorA : floorB);
            }
        }
        void Ceil(float x0, float x1, float z0, float z1, float h) => mb.Box(x1 - x0, .2f, z1 - z0, (x0 + x1) / 2, h + .1f, (z0 + z1) / 2, ceil);
        // wall along X axis (constant z) from xa..xb, or along Z (constant x) from za..zb, with alternating panels
        void WallZ(float z, float xa, float xb, float h, float y0 = 0)
        {
            int n = Math.Max(1, (int)((xb - xa) / 4)); float step = (xb - xa) / n;
            for (int i = 0; i < n; i++) { mb.Box(step - .08f, h, .3f, xa + step * (i + .5f), y0 + h / 2, z, i % 2 == 0 ? wallA : wallB); }
            mb.Box(xb - xa, .22f, .34f, (xa + xb) / 2, y0 + h - .1f, z, trim);
        }
        void WallX(float x, float za, float zb, float h)
        {
            int n = Math.Max(1, (int)((zb - za) / 4)); float step = (zb - za) / n;
            for (int i = 0; i < n; i++) mb.Box(.3f, h, step - .08f, x, h / 2, za + step * (i + .5f), i % 2 == 0 ? wallA : wallB);
            mb.Box(.34f, .22f, zb - za, x, h - .1f, (za + zb) / 2, trim);
        }
        // ---- hangar: x -16..16, z -6..34, h 14 ----
        Floor(-16, 16, -6, 34); Ceil(-16, 16, -6, 34, 14);
        WallX(-16, -6, 34, 14); WallX(16, -6, 34, 14);
        WallZ(-6, -16, -3, 14); WallZ(-6, 3, 16, 14); mb.Box(6, 9.5f, .3f, 0, 9.25f, -6, wallB); // doorway to corridor (3-wide each side, 4.5 high)
        // stern opening with force-field frame (field itself drawn dynamically)
        mb.Box(32, .4f, .4f, 0, 12.8f, 33.9f, cyan, 255); mb.Box(32, .4f, .4f, 0, .4f, 33.9f, cyan, 255);
        mb.Box(.4f, 13, .4f, -15.8f, 6.5f, 33.9f, cyan, 255); mb.Box(.4f, 13, .4f, 15.8f, 6.5f, 33.9f, cyan, 255);
        mb.Box(32, 2, .5f, 0, 13, 34, topWall);
        for (int k = 0; k < 5; k++) mb.Box(2, .12f, 6, k % 2 == 1 ? -6 : 6, 13.8f, 2 + k * 7, C(0xfff2d8), 255);
        foreach (var (x, z) in new[] { (-8f, 6f), (8f, 6f), (-8f, 26f), (8f, 26f) }) lights.Add(new PtLight { Pos = new(x, 11, z), Col = new(.5f, .55f, .62f), Rad = 28 });
        // pads: ring + stanchion
        foreach (var p in Data.Pads)
        {
            for (int s = 0; s < 36; s++)
            {
                float a0 = s * MathF.Tau / 36, a1 = (s + 1) * MathF.Tau / 36;
                Vector3 P(float r, float a) => new(p[0] + MathF.Cos(a) * r, .02f, p[1] + MathF.Sin(a) * r);
                mb.Quad(P(3.2f, a0), P(3.6f, a0), P(3.6f, a1), P(3.2f, a1), cyan, 255);
            }
            float sx = p[0] < 0 ? p[0] - 4.6f : p[0] + 4.6f; mb.Box(.5f, .9f, .5f, sx, .45f, p[1] - 1, C(0x4a566e)); mb.Box(.2f, .2f, .2f, sx, 1.0f, p[1] - 1, C(0x40e090), 255);
        }
        // aetherite tank frames (liquid drawn dynamically)
        foreach (var t in Walker.Tanks) { mb.CylY(1.9f, 1.9f, .5f, t[0], .25f, t[1], dkc, 18); mb.CylY(1.9f, 1.9f, .5f, t[0], 11.2f, t[1], dkc, 18); }
        // ---- corridor: x -3..3, z -18..-5.9, h 4.5 ----
        Floor(-3, 3, -18, -5.9f); Ceil(-3, 3, -18, -5.9f, 4.5f); WallX(-3, -18, -5.9f, 4.5f); WallX(3, -18, -5.9f, 4.5f);
        for (int k = 0; k < 3; k++) { mb.Box(.3f, .06f, 4.6f, -2.9f, 4.2f, -9 - k * 4, cyan, 255); mb.Box(.3f, .06f, 4.6f, 2.9f, 4.2f, -9 - k * 4, cyan, 255); }
        lights.Add(new PtLight { Pos = new(0, 3.8f, -12), Col = new(.5f, .65f, .75f), Rad = 16 });
        // ---- bridge: x -14..14, z -38..-18, h 9 ----
        Floor(-14, 14, -38, -18); Ceil(-14, 14, -38, -18, 9); WallX(-14, -38, -18, 9); WallX(14, -38, -18, 9);
        WallZ(-18, -14, -3, 9); WallZ(-18, 3, 14, 9); mb.Box(6, 4.5f, .3f, 0, 6.75f, -18, wallB);
        mb.Box(28, 2.5f, .5f, 0, 7.75f, -38, topWall); mb.Box(28, 2.5f, .5f, 0, 1.25f, -38, topWall); mb.Box(2, 9, .5f, -13, 4.5f, -38, topWall); mb.Box(2, 9, .5f, 13, 4.5f, -38, topWall);
        foreach (var x in new[] { -7f, 0f, 7f }) mb.Box(.4f, 6, .4f, x, 4.5f, -38, C(0x3a4358)); mb.Box(28, .3f, .3f, 0, 7.4f, -37.8f, cyan, 255);
        foreach (var (x, z) in new[] { (-8f, -24f), (8f, -24f), (0f, -31f) }) lights.Add(new PtLight { Pos = new(x, 7.6f, z), Col = new(.62f, .56f, .42f), Rad = 26 });
        for (int k = 0; k < 4; k++) mb.Box(1.6f, .12f, 18, -9 + k * 6, 8.9f, -28, C(0xfff0d0), 255);
        for (int k = 0; k < 5; k++)
        {
            float x = -9 + k * 4.5f; mb.Box(3.6f, 1.0f, 1.2f, x, .5f, -36, cons);
            var sv = mb.T; mb.T = Matrix4x4.CreateRotationX(-.3f) * Matrix4x4.CreateTranslation(x, 1.35f, -36.1f) * sv; mb.Box(3.2f, .9f, .05f, 0, 0, 0, C(0x051a22), 120); mb.T = sv;
            screens.Add(new ScreenDef { Center = new(x, 1.35f, -36.0f), Right = Vector3.UnitX, Up = Vector3.Normalize(new Vector3(0, MathF.Cos(.3f), MathF.Sin(.3f))), W = 3.1f, H = .8f });
        }
        for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 3; k++)
        {
            float z = -21.5f - k * 4.2f; mb.Box(1.2f, 1.0f, 2.6f, s * 13.2f, .5f, z, cons);
            var sv = mb.T; mb.T = Matrix4x4.CreateRotationZ(s * .3f) * Matrix4x4.CreateTranslation(s * 12.9f, 1.4f, z) * sv; mb.Box(.05f, .9f, 2.3f, 0, 0, 0, C(0x051a22), 120); mb.T = sv;
            screens.Add(new ScreenDef { Center = new(s * 12.85f, 1.4f, z), Right = Vector3.UnitZ, Up = Vector3.Normalize(new Vector3(-s * MathF.Sin(.3f), MathF.Cos(.3f), 0)), W = 2.2f, H = .8f });
        }
        // commander's chair & holotable base
        mb.Box(1.2f, .3f, 1.2f, 0, .55f, -21, C(0x4a3a30)); mb.Box(1.2f, 1.6f, .3f, 0, 1.3f, -20.4f, C(0x4a3a30)); mb.CylY(.15f, .15f, .5f, 0, .25f, -21, dkc, 10);
        mb.CylY(2.4f, 2.0f, .9f, 0, .45f, -28, C(0x1b2538), 28); mb.CylY(2.3f, 2.3f, .06f, 0, .93f, -28, C(0x1a4a7a), 28, 200);
        for (int s = 0; s < 48; s++) { float a0 = s * MathF.Tau / 48, a1 = (s + 1) * MathF.Tau / 48; Vector3 P(float r, float a) => new(MathF.Cos(a) * r, .97f, -28 + MathF.Sin(a) * r); mb.Quad(P(2.3f, a0), P(2.4f, a0), P(2.4f, a1), P(2.3f, a1), cyan, 255); }
        lights.Add(new PtLight { Pos = new(0, 2.2f, -28), Col = new(.25f, .6f, 1f), Rad = 10 });
        var tm = new MeshBuilder(); tm.CylY(1.5f, 1.5f, 1, 0, 0, 0, C(0x8a6aff), 18, 255, true);
        return (mb.Build(), tm.Build(), lights, screens);
    }
}
