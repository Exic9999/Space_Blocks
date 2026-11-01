// Procedural mesh builders: islands, The Executor, ships, interior, figures. Ported from the Three.js scene code.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed class Mul32
{
    uint a; public Mul32(int seed) { a = (uint)seed; }
    public float Next()
    {
        a += 0x6D2B79F5u; uint t = a; t = (t ^ (t >> 15)) * (1u | t); t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t; return ((t ^ (t >> 14))) / 4294967296f;
    }
}

public sealed class GlowDef { public Vector3 Pos; public float Size; public Color Col; public float Op; }
public sealed class ShipGfx { public Mesh Mesh; public List<GlowDef> Glows = new(); public float Boost; }

public static class Scene
{
    public static float Noise3(float x, float y, float z) => MathF.Sin(x * 1.3f + y * .7f) * MathF.Cos(z * 1.1f - y * .9f) + .5f * MathF.Sin(x * 2.9f + z * 2.3f + y * 1.7f);
    public static Color Hsl(float h, float s, float l)
    {
        h = ((h % 1f) + 1f) % 1f; l = Math.Clamp(l, 0, 1); s = Math.Clamp(s, 0, 1);
        float q = l < .5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        float F(float t) { t = (t + 1) % 1; return t < 1f / 6 ? p + (q - p) * 6 * t : t < .5f ? q : t < 2f / 3 ? p + (q - p) * (2f / 3 - t) * 6 : p; }
        return new Color((byte)(F(h + 1f / 3) * 255), (byte)(F(h) * 255), (byte)(F(h - 1f / 3) * 255), (byte)255);
    }
    static Color C(uint rgb) => Col.Hex(rgb);

    // ---------- icosphere ----------
    static (List<Vector3> v, List<int[]> f) Icosphere(int sub)
    {
        float t = (1 + MathF.Sqrt(5)) / 2;
        var v = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < v.Count; i++) v[i] = Vector3.Normalize(v[i]);
        var f = new List<int[]> { new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
        for (int s = 0; s < sub; s++)
        {
            var cache = new Dictionary<long, int>(); var nf = new List<int[]>();
            int Mid(int a, int b) { long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (cache.TryGetValue(k, out int m)) return m; v.Add(Vector3.Normalize((v[a] + v[b]) / 2)); return cache[k] = v.Count - 1; }
            foreach (var tr in f) { int a = Mid(tr[0], tr[1]), b = Mid(tr[1], tr[2]), c = Mid(tr[2], tr[0]); nf.Add(new[] { tr[0], a, c }); nf.Add(new[] { tr[1], b, a }); nf.Add(new[] { tr[2], c, b }); nf.Add(new[] { a, b, c }); }
            f = nf;
        }
        return (v, f);
    }

    // ---------- The Executor ----------
    public static (Mesh hull, Mesh tanks, List<GlowDef> glows) BuildExecutor()
    {
        var mb = new MeshBuilder(); var glows = new List<GlowDef>(); var rnd = new Random(21);
        var hull = C(0x5d677f);
        void Wedge(float nose, float stern, float hw, float depth, float y, Color c) =>
            mb.Extrude(new[] { new Vector2(0, nose), new Vector2(-hw, stern), new Vector2(hw, stern) }, y, y + depth, c);
        Wedge(-250, 230, 165, 36, 0, hull); Wedge(-190, 225, 132, 24, 38, Col.Mul(hull, 1.08f)); Wedge(-120, 215, 92, 20, 64, Col.Mul(hull, 1.16f));
        var trench = C(0x2a3350);
        for (int sx = -1; sx <= 1; sx += 2) for (int k = 0; k < 3; k++)
        {
            var sv = mb.T; mb.T = Matrix4x4.CreateRotationY(sx * .34f) * Matrix4x4.CreateTranslation(sx * (40 + k * 30), 38.6f, -10 + k * 30);
            mb.Box(2, 1, 430 - k * 70, 0, 0, 0, trench); mb.T = sv;
        }
        var dk = C(0x5a647c);
        mb.Box(70, 36, 50, 0, 102, 150, dk); mb.Box(96, 10, 26, 0, 124, 160, dk); mb.Box(30, 16, 18, 0, 134, 160, C(0x8892aa));
        for (int s = -1; s <= 1; s += 2) { mb.Sphere(13, s * 42, 132, 160, C(0xaab4cc), 1, 1, 1, 0, 6, 10); glows.Add(new GlowDef { Pos = new(s * 42, 132, 160), Size = 34, Col = C(0x7fd0ff), Op = .5f }); }
        mb.Box(80, 2.4f, .6f, 0, 126, 173.5f, C(0xffe9a0), 255); mb.CylY(1.2f, 1.2f, 26, 0, 150, 160, dk, 6);
        // engines & hangar
        var em = C(0x333a4e);
        for (int s = -1; s <= 1; s += 2) foreach (var ex in new[] { 78f, 116f, 150f })
        {
            float x = s * ex; mb.CylZ(15, 13, 34, x, 20, 238, em, 16); mb.CylZ(11, 11, 1, x, 20, 255.5f, C(0xc8f0ff), 12, 255);
            glows.Add(new GlowDef { Pos = new(x, 20, 258), Size = 64, Col = C(0x8fd8ff), Op = .95f });
        }
        mb.Box(110, 32, 4, 0, 19, 235.5f, C(0x04060c));
        var fm = C(0x5adcff);
        foreach (var (w, h, x, y) in new[] { (116f, 2.4f, 0f, 36f), (116f, 2.4f, 0f, 2f), (2.4f, 36f, -57f, 19f), (2.4f, 36f, 57f, 19f) }) mb.Box(w, h, 2, x, y, 238.5f, fm, 255);
        glows.Add(new GlowDef { Pos = new(0, 19, 245), Size = 150, Col = C(0x5adcff), Op = .5f });
        // greebles & window lights
        var gm = C(0x6a7390);
        for (int k = 0; k < 160; k++)
        {
            float z = -200 + (float)rnd.NextDouble() * 400, half = (z + 250) / 480 * 150, x = ((float)rnd.NextDouble() * 2 - 1) * half * .9f;
            float sx = 3 + (float)rnd.NextDouble() * 12, sy = 2 + (float)rnd.NextDouble() * 7, sz = 3 + (float)rnd.NextDouble() * 12;
            mb.Box(sx, sy, sz, x, 37 + sy / 2, z, Col.Mul(gm, .8f + (float)rnd.NextDouble() * .4f));
        }
        for (int k = 0; k < 420; k++)
        {
            float z = -230 + (float)rnd.NextDouble() * 440, half = (z + 250) / 480 * 158, x = ((float)rnd.NextDouble() * 2 - 1) * half, y = 39 + (float)rnd.NextDouble() * 40;
            if (y > 46) { float lim = (z + 120) / 335 * 92; if (MathF.Abs(x) > lim) y = 39; }
            mb.Box(2.4f, 1.5f, 2.4f, x, y + .8f, z, C(0xffe6a0), 255);
        }
        // ---- extra hull detail: turret pods, antenna masts, raised plating, edge-light strips ----
        var turretM = C(0x4a566e);
        foreach (var (tx, tz) in new[] { (-95f, 60f), (95f, 60f), (-120f, 150f), (120f, 150f), (-60f, -40f), (60f, -40f) })
        {
            mb.Box(9, 3, 9, tx, 39.5f, tz, turretM); mb.CylY(3.6f, 3.2f, 3.2f, tx, 42.5f, tz, C(0x59647c), 12); mb.Sphere(2.6f, tx, 44.2f, tz, C(0x6a7390), 1, .7f, 1, 0, 4, 8);
            var sv = mb.T; mb.T = Matrix4x4.CreateTranslation(tx, 44, tz - 5.5f) * sv; mb.CylZ(.55f, .55f, 9, 0, 0, 0, C(0x2a3040), 8); mb.CylZ(.8f, .8f, 1.2f, 0, 0, -4.4f, C(0x8892aa), 8); mb.T = sv;
        }
        foreach (var (ax, az, ah) in new[] { (-30f, 120f, 46f), (30f, 120f, 40f), (-48f, 155f, 34f), (48f, 155f, 52f), (0f, 90f, 28f) })
        { mb.CylY(.7f, .5f, ah, ax, 134 + ah / 2, az, C(0x8892aa), 5); mb.Sphere(1.6f, ax, 134 + ah, az, C(0xff5a5a), 1, 1, 1, 255, 3, 5); mb.Box(8, .5f, .5f, ax, 134 + ah * .7f, az, C(0x8892aa)); }
        for (int k = 0; k < 26; k++)
        {
            float z = -210 + k * 16, half = (z + 250) / 480 * 150 * .86f; float pw = 14 + (k % 3) * 5; mb.Box(pw, .9f, 12, -half * .5f, 36.7f, z, Col.Mul(hull, .92f + (k % 4) * .05f)); mb.Box(pw, .9f, 12, half * .5f, 36.7f, z, Col.Mul(hull, .95f + (k % 3) * .04f));
        }
        for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 9; k++) { float z = -170 + k * 44, half = (z + 250) / 480 * 163; mb.Box(5, 1.2f, 2.4f, s * half, 3, z, C(0xffd890), 255); }
        // storage tanks (separate mesh: glow follows stored Aetherite)
        var tb = new MeshBuilder(); var tc = C(0x6a4cd8);
        for (int s = -1; s <= 1; s += 2)
        {
            tb.CylZ(17, 17, 120, s * 78, 66, 140, tc, 20, 220); tb.Sphere(17, s * 78, 66, 80, tc, 1, 1, 1, 220, 6, 12); tb.Sphere(17, s * 78, 66, 200, tc, 1, 1, 1, 220, 6, 12);
            for (int k = 0; k < 4; k++) mb.Box(40, 3, 3, s * 78, 66, 90 + k * 35, dk);
        }
        return (mb.Build(), tb.Build(), glows);
    }

    // ---------- ships ----------
    static void Wing(MeshBuilder mb, Color c, int side, float root, float tip, float span, float sweep, float thick, float x, float y, float z)
    {
        var poly = new[] { new Vector2((x) * side, z), new Vector2((x + span) * side, z + sweep), new Vector2((x + span) * side, z + sweep + tip), new Vector2(x * side, z + root) };
        mb.Extrude(poly, y, y + thick, c);
    }
    public static ShipGfx BuildShip(int idx, Levels lv)
    {
        var cd = idx == 5 ? Data.Captain : Data.Crew[idx]; var g = new ShipGfx(); var mb = new MeshBuilder();
        Color hull = C(cd.Hull), acc = C(cd.Acc), dark = C(0x1b2030), glass = C(0x66e0ff), red = C(0xff5a70);
        void Gl(float x, float y, float z, float s, uint col = 0xffa860) => g.Glows.Add(new GlowDef { Pos = new(x, y, z), Size = s, Col = C(col), Op = .9f });
        void Eng(float r, float x, float y, float z, float len = .9f) { mb.CylZ(r, r * .78f, len, x, y, z, dark, 12); Gl(x, y, z + len / 2 + .2f, r * 4.4f); }
        void RBox(float w, float h, float d, float x, float y, float z, Color c, float rz = 0, int e = 0)
        { var sv = mb.T; mb.T = Matrix4x4.CreateRotationZ(rz) * Matrix4x4.CreateTranslation(x, y, z) * sv; mb.Box(w, h, d, 0, 0, 0, c, e); mb.T = sv; }
        if (idx == 0)
        {
            mb.CylZ(.75f, .5f, 4.6f, 0, 0, .3f, hull); mb.CylZ(.5f, .04f, 2.6f, 0, 0, -3.3f, hull); mb.Sphere(.55f, 0, .48f, -.9f, glass, 1, .6f, 1.8f, 200, 6, 10);
            foreach (int s in new[] { -1, 1 })
            {
                Wing(mb, hull, s, 2.8f, .8f, 3.7f, 2.0f, .12f, .45f, -.05f, .5f); RBox(.14f, .9f, 1.2f, s * .9f, .65f, 2.3f, acc, s * -.35f);
                mb.CylZ(.07f, .07f, 1.7f, s * 3.4f, -.05f, .2f, red, 6, 255); mb.CylZ(.1f, .1f, .4f, s * 3.4f, -.05f, -.7f, dark, 6);
            }
            mb.Box(.22f, .05f, 3.6f, 0, .74f, -.2f, acc); Eng(.5f, -.55f, 0, 2.8f); Eng(.5f, .55f, 0, 2.8f);
        }
        else if (idx == 1)
        {
            mb.Box(2.4f, 1.5f, 4.8f, 0, 0, .3f, hull); mb.Box(1.5f, .8f, 1.5f, 0, .95f, -1.3f, hull); mb.Box(1.2f, .5f, .1f, 0, .98f, -2.08f, glass, 200); mb.Box(1.8f, .3f, 1.2f, 0, .1f, -2.7f, dark);
            foreach (int s in new[] { -1, 1 })
            {
                mb.Box(1.1f, 1.2f, 3.2f, s * 1.9f, -.1f, .8f, acc); mb.Box(1.12f, .35f, 3.22f, s * 1.9f, -.05f, .8f, C(0xe8d34a));
                for (int k = 0; k < 4; k++) mb.Box(1.14f, .36f, .3f, s * 1.9f, -.05f, -.3f + k * .6f, C(0x222222));
                Wing(mb, hull, s, 1.6f, .8f, 1.4f, .6f, .1f, 1.2f, -.3f, 1.6f);
            }
            mb.CylZ(.95f, .75f, 1.3f, 0, 0, 3.35f, dark, 16); Gl(0, 0, 4.15f, 4.2f);
        }
        else if (idx == 2)
        {
            mb.CylZ(.45f, .3f, 5.8f, 0, 0, .6f, hull); mb.CylZ(.3f, .03f, 2.6f, 0, 0, -3.0f, hull); mb.Sphere(.4f, 0, .38f, -1, glass, 1, .7f, 1.9f, 200, 6, 10);
            foreach (int s in new[] { -1, 1 }) Wing(mb, hull, s, 1.5f, .55f, 3.4f, -.9f, .08f, .3f, 0, -.2f);
            for (int k = 0; k < 28; k++) { float a = k * MathF.Tau / 28; mb.Sphere(.09f, MathF.Cos(a) * 1.5f, MathF.Sin(a) * 1.5f, .4f, C(0x9ff4ff), 1, 1, 1, 255, 3, 5); }
            mb.CylY(.03f, .03f, 1.0f, 0, .9f, .9f, acc, 5); mb.Sphere(.22f, 0, 1.45f, .9f, C(0xffd890), 1, 1, 1, 255, 5, 8); Gl(0, 1.45f, .9f, 2.6f, 0xffc870);
            mb.Box(.05f, .5f, 1.2f, 0, .5f, 2.6f, acc); Eng(.4f, 0, 0, 3.65f, .7f);
        }
        else if (idx == 5)
        {   // Executor One: a small wedge that echoes the carrier, gold-striped, twin engines
            mb.Extrude(new[] { new Vector2(0, -3.4f), new Vector2(-1.55f, 2.6f), new Vector2(1.55f, 2.6f) }, -.3f, .3f, hull); mb.Extrude(new[] { new Vector2(0, -2.3f), new Vector2(-.95f, 2.1f), new Vector2(.95f, 2.1f) }, .3f, .7f, Col.Mul(hull, 1.15f));
            mb.Sphere(.5f, 0, .85f, -.7f, glass, 1, .7f, 1.5f, 200, 6, 10); foreach (int s in new[] { -1, 1 }) { mb.Box(.1f, .05f, 4.6f, s * .55f, .72f, .1f, acc); Wing(mb, hull, s, 1.8f, .7f, 1.6f, .9f, .1f, 1.0f, -.15f, .8f); mb.Box(.08f, .5f, 1.1f, s * 1.9f, .15f, 2.3f, acc); }
            mb.Box(.1f, .9f, 1.2f, 0, 1.0f, 2.0f, acc); Eng(.55f, -.85f, 0, 2.75f, 1.0f); Eng(.55f, .85f, 0, 2.75f, 1.0f);
        }
        else if (idx == 3)
        {
            mb.Box(2.2f, 1.3f, 4.6f, 0, 0, .3f, hull); mb.CylZ(1.05f, .35f, 1.8f, 0, 0, -3.0f, hull, 6);
            foreach (int s in new[] { -1, 1 }) { RBox(.5f, 1.1f, 2.8f, s * 1.45f, .05f, .2f, acc, s * .12f); mb.Box(.1f, .1f, 3.6f, s * 1.12f, .6f, .2f, C(0xe0b84a)); }
            mb.Box(1.2f, .5f, 1.2f, 0, .85f, -1.3f, hull); mb.Box(1.0f, .28f, .1f, 0, .9f, -1.93f, glass, 200);
            mb.CylY(.9f, .7f, .15f, 0, 1.05f, .9f, C(0xe0b84a), 18); mb.Box(.1f, 1.6f, 1.3f, 0, 1.1f, 2.1f, hull);
            mb.Box(.04f, .9f, 1.5f, 0, 1.75f, 2.85f, C(0x2a8a4a), 90); mb.Box(.045f, .2f, 1.5f, 0, 1.75f, 2.85f, C(0xe0b84a), 90);
            Eng(.55f, -.7f, -.1f, 2.9f, 1.0f); Eng(.55f, .7f, -.1f, 2.9f, 1.0f);
        }
        else
        {
            mb.Sphere(1, 0, 0, .2f, hull, 1.1f, .9f, 1.6f, 0, 8, 12); mb.Sphere(.5f, 0, .5f, -.9f, glass, 1, .7f, 1.3f, 200, 6, 10);
            uint[] pc = { 0xffd45a, 0x5ad0ff, 0x7ee08a, 0xff5a6e };
            for (int k = 0; k < 4; k++) mb.Box(.8f, .08f, .8f, (k % 2 == 1 ? 1 : -1) * .5f, .82f - k * .04f, .6f - k * .45f, C(pc[k]));
            foreach (int s in new[] { -1, 1 }) mb.Box(2.4f, .1f, .9f, s * 1.9f, -.05f, .7f, acc);
            mb.CylY(.03f, .03f, 1.2f, .4f, 1.0f, 1.2f, dark, 5); mb.Sphere(.12f, .4f, 1.65f, 1.2f, C(0xff7acb), 1, 1, 1, 255, 4, 6);
            Eng(.65f, -.95f, -.1f, 2.1f, 1.4f); Eng(.65f, 0, .25f, 2.2f, 1.4f); Eng(.65f, .95f, -.1f, 2.1f, 1.4f);
        }
        for (int k = 0; k < Math.Min(lv.las, 3); k++) mb.CylZ(.07f, .07f, 1.3f, (k - 1) * .5f, -.5f, -2.2f, red, 6, 255);
        for (int k = 0; k < Math.Min(lv.tank, 4); k++) { int s = k % 2 == 1 ? 1 : -1; mb.CylZ(.3f, .3f, 1.7f, s * (1.25f + (k >> 1) * .45f), -.72f, .9f, C(0x40e090), 10, 200); }
        for (int k = 0; k < Math.Min(lv.car, 4); k++) mb.Box(.7f, .5f, .9f, (k % 2 == 1 ? 1 : -1) * .5f, .95f + (k >> 1) * .45f, 1.9f + (k >> 1) * .2f, acc);
        for (int k = 0; k < Math.Min(lv.hull, 4); k++) { int sd = k % 2 == 1 ? 1 : -1; mb.Box(.12f, .5f, 1.4f, sd * (1.0f + (k >> 1) * .15f), .1f, -.4f + (k >> 1) * 1.6f, acc); }
        g.Mesh = mb.Build(); g.Boost = 1 + .18f * lv.eng; return g;
    }

    // ---------- crew figures (torso+head static; limbs separate) ----------
    // what every crew member carries: a laser pistol holstered on the hip and a pick axe strapped across the back
    public static Mesh BuildFigureGear(int idx)
    {
        var m = new MeshBuilder(); var steel = C(0x9aa4b8); var wood = C(0x7a5230); var dk = C(0x2a3040); var gold = C(0xe0b84a);
        var sv = m.T; m.T = Matrix4x4.CreateRotationZ(.55f) * Matrix4x4.CreateTranslation(.04f, 1.18f, -.2f) * sv; m.CylY(.016f, .02f, .95f, 0, 0, 0, wood, 6); m.Box(.3f, .045f, .05f, 0, .5f, 0, steel); m.Box(.06f, .06f, .07f, 0, .5f, 0, dk); m.CylY(.022f, .022f, .05f, 0, -.2f, 0, gold, 6); m.T = sv;
        m.Box(.07f, .2f, .13f, .29f, .88f, .02f, dk); m.Box(.06f, .08f, .15f, .29f, .98f, .03f, C(0x4a5468)); m.Box(.012f, .15f, .02f, .33f, .88f, .02f, C(0x5adcff), 255); m.Box(.1f, .03f, .06f, .26f, 1.0f, 0, gold);
        return m.Build();
    }
    public static (Mesh body, Mesh arm, Mesh leg) BuildFigure(int idx)
    {
        var cd = Data.Crew[idx]; var suit = C(cd.Color); suit = Col.Mul(suit, .8f); var sk = C(cd.Skin); var dk = C(0x222838);
        var b = new MeshBuilder(); b.Box(.5f, .62f, .3f, 0, 1.15f, 0, suit); b.Sphere(.17f, 0, 1.62f, 0, sk, 1, 1, 1, 0, 6, 8); b.Box(.34f, .1f, .34f, 0, 1.75f, 0, C(0x1a1a22));
        var a = new MeshBuilder(); a.Box(.14f, .6f, .18f, 0, -.3f, 0, suit);
        var l = new MeshBuilder(); l.Box(.2f, .8f, .24f, 0, -.4f, 0, dk);
        return (b.Build(), a.Build(), l.Build());
    }
}

