// Builds an island's meshes from its IslandShape: terrain, underside rock, trees (three species), boulders, trail ribbons, rivers and waterfalls.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed class IslandGfx
{
    public IslandShape Shape = null!; public Mesh Body, FarTrees, Trails, Water, Crystal, Decor; public bool HasTrails, HasWater, HasFar, DecorBuilt;
    public readonly List<Vector3> Mist = new(); public readonly List<(Vector3 pos, float size)> Falls = new(); public float PadY, ClearY;
}

public static class IslandBuilder
{
    static Color C(uint rgb) => Col.Hex(rgb);
    static Color Lerp(Color a, Color b, float t) => Col.Lerp(a, b, Math.Clamp(t, 0, 1));
    static float Sm(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    // ---------- tree prototypes: unit height, base at the origin (kind 0 pine, 1 broadleaf, 2 birch; variant tints the foliage) ----------
    static readonly MeshBuilder?[,] protos = new MeshBuilder?[3, 2];
    public static MeshBuilder TreeProto(int kind, int variant)
    {
        if (protos[kind, variant] is { } have) return have;
        var mb = new MeshBuilder(); var rng = new Mul32(kind * 17 + variant * 5 + 3); float R() => rng.Next();
        if (kind == 0)
        {
            Color bark = C(0x4a3626); mb.CylY(.032f, .05f, .34f, 0, .17f, 0, bark, 7); mb.CylY(.05f, .075f, .06f, 0, .03f, 0, bark, 7); // root flare
            for (int k = 0; k < 5; k++)
            {
                float t = k / 4f, rad = .44f * (1 - t * .72f), hh = .3f - t * .06f, y0 = .2f + k * .17f; var green = Hsl(.3f + (variant * .025f) + R() * .015f, .5f, .21f + t * .1f + R() * .03f);
                mb.CylY(rad, rad * .08f, hh, 0, y0 + hh / 2, 0, green, 9); mb.CylY(rad * .98f, rad * .98f, .02f, 0, y0 + .005f, 0, Col.Mul(green, .6f), 9);
            }
        }
        else if (kind == 1)
        {
            Color bark = C(0x5a4030); mb.CylY(.04f, .065f, .46f, 0, .23f, 0, bark, 7);
            for (int s = -1; s <= 1; s += 2) { var sv = mb.T; mb.T = Matrix4x4.CreateRotationZ(s * .8f) * Matrix4x4.CreateTranslation(0, .38f, 0) * sv; mb.CylY(.018f, .028f, .24f, 0, .12f, 0, bark, 5); mb.T = sv; }
            (float x, float y, float z, float r)[] blobs = { (0, .72f, 0, .3f), (.2f, .62f, .08f, .21f), (-.19f, .64f, -.07f, .22f), (.04f, .86f, -.1f, .19f), (-.05f, .58f, .18f, .2f) };
            for (int b = 0; b < blobs.Length; b++) { var gcol = Hsl(.25f + variant * .05f + R() * .02f, .52f, .28f + R() * .1f); mb.Sphere(blobs[b].r, blobs[b].x, blobs[b].y, blobs[b].z, gcol, 1.15f, .85f, 1.15f, 0, 4, 7); }
        }
        else
        {
            Color bark = C(0xe8e4d8); mb.CylY(.026f, .04f, .82f, 0, .41f, 0, bark, 6);
            for (int b = 0; b < 7; b++) mb.Box(.07f, .03f, .07f, 0, .08f + b * .1f, 0, C(0x2a2622));
            (float x, float y, float z, float r)[] blobs = { (0, .78f, 0, .2f), (.12f, .68f, .05f, .15f), (-.11f, .7f, -.05f, .15f), (.02f, .9f, .04f, .13f) };
            for (int b = 0; b < blobs.Length; b++) { var gcol = Hsl(.18f + variant * .04f + R() * .02f, .6f, .46f + R() * .1f); mb.Sphere(blobs[b].r, blobs[b].x, blobs[b].y, blobs[b].z, gcol, 1, 1.1f, 1, 0, 4, 6); }
        }
        protos[kind, variant] = mb; return mb;
    }
    public static Color Hsl(float h, float s, float l) => Scene.Hsl(h, s, l);

    static Color Shade(IslandShape sh, float x, float z, float h, float slope)
    {
        float R = sh.R, n = IslandShape.Fbm(x * .08f, z * .08f, sh.Seed + 3) - .5f, patch = IslandShape.Fbm(x * .03f, z * .03f, sh.Seed + 9);
        var grass = Hsl(.27f + n * .05f + (patch - .5f) * .05f, .5f, .34f + n * .09f + (patch > .62f ? .06f : 0)); var meadow = Hsl(.2f, .55f, .46f + n * .06f);
        var rock = Hsl(.09f, .1f, .4f + n * .14f); var rockDark = Hsl(.07f, .12f, .24f + n * .08f); var snow = C(0xeef2fa);
        var c = Lerp(grass, meadow, Sm((patch - .55f) * 4));
        float hh = h - .11f * R, peak = MathF.Max(1, sh.MaxHeight - .11f * R);
        c = Lerp(c, rock, Sm((slope - .42f) / .38f)); c = Lerp(c, rockDark, Sm((hh / peak - .35f) * 3.5f) * .8f);
        c = Lerp(c, snow, Sm((hh / peak - .70f) * 6f) * Sm((.95f - slope) * 4f));
        if (sh.Rivers.Count > 0 || sh.PondR > 0)
        {
            float bw = 1, rd = 1e9f; if (sh.Rivers.Count > 0) rd = sh.RiverDist(x, z, out bw); float bank = 1 - Sm((rd - bw * 1.0f) / (bw * 1.6f));
            if (sh.PondR > 0) { float pd = MathF.Sqrt((x - sh.Pond.X) * (x - sh.Pond.X) + (z - sh.Pond.Y) * (z - sh.Pond.Y)); bank = MathF.Max(bank, 1 - Sm((pd - sh.PondR) / (sh.PondR * .9f))); }
            c = Lerp(c, C(0x9a8a62), bank * .85f);
        }
        float r = MathF.Sqrt(x * x + z * z); c = Lerp(c, Hsl(.08f, .3f, .27f), Sm((r - .93f * R) / (.07f * R)));
        return c;
    }

    public sealed class IslandPrep { public IslandGfx G = null!; public MeshBuilder Body = null!, Far = null!, Trails = null!, Water = null!, Crystal = null!; }
    public static IslandGfx Build(Island isl) => Finish(Prepare(isl));
    public static IslandGfx Finish(IslandPrep p) { var g = p.G; g.Body = p.Body.Build(); g.FarTrees = p.Far.Build(); g.Trails = p.Trails.Build(); g.Water = p.Water.Build(); g.Crystal = p.Crystal.Build(); return g; }
    // CPU-only (safe on a worker thread); Finish uploads the meshes on the render thread.
    public static IslandPrep Prepare(Island isl)
    {
        var sh = Terrain.Get(isl); var g = new IslandGfx { Shape = sh }; var prep = new IslandPrep { G = g }; float R = sh.R; var rng = new Mul32(isl.seed ^ 0x7b1d); float Rn() => rng.Next();
        var body = new MeshBuilder();
        // ---- underside rock (icosphere, flattened lid + tapering belly) ----
        var (iv, ifc) = IcoSphere(3); var pos = new Vector3[iv.Count]; var cols = new Color[iv.Count];
        for (int k = 0; k < iv.Count; k++)
        {
            float x = iv[k].X * R, y = iv[k].Y * R, z = iv[k].Z * R; if (y > 0) y *= .05f; else { float f = 1 - MathF.Min(1, -y / R) * .8f; x *= f; z *= f; y *= 1.5f; }
            float nn = Scene.Noise3(x * .05f, y * .04f, z * .05f) * R * .06f; x += nn; z += nn * .8f; y += nn * .5f; pos[k] = new Vector3(x, y, z);
            float d = MathF.Min(1, -y / (R * 1.4f)), lo = Scene.Noise3(x * .2f, y * .2f, z * .2f) * .035f;
            cols[k] = new Color((byte)Math.Clamp((.42f - d * .22f + lo) * 255, 0, 255), (byte)Math.Clamp((.33f - d * .17f + lo) * 255, 0, 255), (byte)Math.Clamp((.29f - d * .1f + lo) * 255, 0, 255), (byte)255);
        }
        foreach (var t in ifc) body.Tri(pos[t[0]], pos[t[1]], pos[t[2]], Lerp(Lerp(cols[t[0]], cols[t[1]], .5f), cols[t[2]], .34f));
        // ---- terrain: polar grid, smooth normals, per-vertex colours ----
        int NR = Math.Clamp((int)(R / 2.6f), 30, 120), NS = Math.Clamp((int)(MathF.Tau * R / 2.6f), 72, 240); var P = new Vector3[NR + 1, NS]; var Cc = new Color[NR + 1, NS]; var Nn = new Vector3[NR + 1, NS];
        for (int ri = 0; ri <= NR; ri++) for (int si = 0; si < NS; si++)
        {
            float rr = R * .995f * ri / NR, a = si * MathF.Tau / NS, x = MathF.Cos(a) * rr, z = MathF.Sin(a) * rr; P[ri, si] = new Vector3(x, sh.Height(x, z), z);
        }
        for (int ri = 0; ri <= NR; ri++) for (int si = 0; si < NS; si++)
        {
            var p = P[ri, si]; var pr0 = P[Math.Max(ri - 1, 0), si]; var pr1 = P[Math.Min(ri + 1, NR), si]; var ps0 = P[ri, (si + NS - 1) % NS]; var ps1 = P[ri, (si + 1) % NS];
            var nrm = Vector3.Normalize(Vector3.Cross(ps1 - ps0, pr1 - pr0)); if (nrm.Y < 0) nrm = -nrm; if (ri == 0) nrm = Vector3.UnitY; Nn[ri, si] = nrm;
            Cc[ri, si] = Shade(sh, p.X, p.Z, p.Y, MathF.Sqrt(MathF.Max(0, 1 - nrm.Y * nrm.Y)) / MathF.Max(nrm.Y, .05f));
        }
        for (int ri = 0; ri < NR; ri++) for (int si = 0; si < NS; si++)
        {
            int s2 = (si + 1) % NS; Vector2 z0 = default;
            body.TriFull(P[ri, si], P[ri + 1, si], P[ri + 1, s2], Nn[ri, si], Nn[ri + 1, si], Nn[ri + 1, s2], Cc[ri, si], Cc[ri + 1, si], Cc[ri + 1, s2], z0, z0, z0);
            if (ri > 0) body.TriFull(P[ri, si], P[ri + 1, s2], P[ri, s2], Nn[ri, si], Nn[ri + 1, s2], Nn[ri, s2], Cc[ri, si], Cc[ri + 1, s2], Cc[ri, s2], z0, z0, z0);
        }
        var lip = Hsl(.08f, .3f, .24f);
        for (int si = 0; si < NS; si++) { int s2 = (si + 1) % NS; var a = P[NR, si]; var b = P[NR, s2]; var a2 = new Vector3(a.X, -.03f * R, a.Z); var b2 = new Vector3(b.X, -.03f * R, b.Z); body.Quad(a, b, b2, a2, lip); }
        // ---- boulders ----
        int nrock = (int)Math.Clamp(R * .4f + sh.Peaks.Count * 24, 14, 300);
        for (int k = 0; k < nrock; k++)
        {
            float a = Rn() * 6.283f, rr = MathF.Sqrt(Rn()) * R * .9f, x = MathF.Cos(a) * rr, z = MathF.Sin(a) * rr; if (sh.Wet(x, z, 1.5f) || sh.DistToTrails(x, z) < 2.2f) continue; bool bad = false; foreach (var f in sh.Flats) if (MathF.Sqrt((x - f.x) * (x - f.x) + (z - f.z) * (z - f.z)) < f.r + 1.5f) bad = true; if (bad) continue;
            float sz = (.5f + Rn() * 1.6f) * (1 + R / 220f), h = sh.Height(x, z); var rc = Hsl(.08f, .08f + Rn() * .08f, .3f + Rn() * .18f); body.Sphere(sz, x, h + sz * .15f, z, rc, 1.2f, .7f, 1f + Rn() * .3f, 0, 3, 5);
        }
        prep.Body = body;
        // ---- far trees: stylised, bigger than life so the forest reads from orbit; capped for large islands ----
        var ft = new MeshBuilder(); int stride = Math.Max(1, sh.Trees.Count / 700); int ci = 0; float treeH = Math.Clamp(R * .1f, 6, 26);
        for (int k = 0; k < sh.Trees.Count; k += stride, ci++)
        {
            var t = sh.Trees[k]; float h = sh.Height(t.x, t.z), s = treeH * t.size; var proto = TreeProto(t.kind, ci % 2);
            ft.Append(proto, Matrix4x4.CreateScale(s) * Matrix4x4.CreateRotationY(t.rot) * Matrix4x4.CreateTranslation(t.x, h - .02f * s, t.z), .88f + (ci * 37 % 25) * .01f); g.HasFar = true;
        }
        prep.Far = ft;
        // ---- trails: dirt ribbons that follow the ground, up the mountains too ----
        var tb = new MeshBuilder(); float tw = Math.Clamp(R * .011f, .9f, 2.4f); var dirt = C(0xb8996a); var dirtEdge = C(0x93774c);
        foreach (var tr in sh.Trails)
        {
            var pts = Resample(tr, 1.1f); Vector3? pl = null, pr = null; Vector3 lastL = default, lastR = default; bool have = false;
            for (int k = 0; k < pts.Count; k++)
            {
                var d = k + 1 < pts.Count ? pts[k + 1] - pts[k] : pts[k] - pts[k - 1]; if (d.LengthSquared() < 1e-6f) continue; d = Vector2.Normalize(d); var nrm2 = new Vector2(-d.Y, d.X);
                var l = pts[k] + nrm2 * tw * .5f; var r2 = pts[k] - nrm2 * tw * .5f; var vl = new Vector3(l.X, sh.Height(l.X, l.Y) + .14f, l.Y); var vr = new Vector3(r2.X, sh.Height(r2.X, r2.Y) + .14f, r2.Y);
                if (have) { tb.TriFull(lastL, lastR, vr, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, dirtEdge, dirtEdge, dirtEdge, default, default, default); tb.TriFull(lastL, vr, vl, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, dirtEdge, dirtEdge, dirtEdge, default, default, default);
                    var cl = Vector3.Lerp(lastL, lastR, .5f) + Vector3.UnitY * .03f; var cr = Vector3.Lerp(vl, vr, .5f) + Vector3.UnitY * .03f; var q = (lastR - lastL) * .22f; var q2 = (vr - vl) * .22f;
                    tb.TriFull(cl - q, cl + q, cr + q2, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, dirt, dirt, dirt, default, default, default); tb.TriFull(cl - q, cr + q2, cr - q2, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, dirt, dirt, dirt, default, default, default); g.HasTrails = true; }
                lastL = vl; lastR = vr; have = true; _ = pl; _ = pr;
            }
        }
        prep.Trails = tb;
        // ---- water: river ribbons (uv.y runs downstream), spring pool, rim waterfalls ----
        var wb = new MeshBuilder(); var tint = new Color((byte)70, (byte)170, (byte)215, (byte)0);
        foreach (var rv in sh.Rivers)
        {
            float dist = 0; Vector3 prevL = default, prevR = default; float prevV = 0; bool have = false;
            for (int k = 0; k < rv.P.Count; k++)
            {
                var p = rv.P[k]; var d = k + 1 < rv.P.Count ? rv.P[k + 1] - p : p - rv.P[k - 1]; if (d.LengthSquared() < 1e-6f) continue; d = Vector2.Normalize(d); var nrm2 = new Vector2(-d.Y, d.X);
                float w = rv.W * (1 + .5f * k / rv.P.Count) * 1.45f, y = rv.Y[k]; var vl = new Vector3(p.X + nrm2.X * w, y, p.Y + nrm2.Y * w); var vr = new Vector3(p.X - nrm2.X * w, y, p.Y - nrm2.Y * w);
                if (k > 0) dist += (rv.P[k] - rv.P[k - 1]).Length(); float v = dist / (w * 2.6f);
                float drop = k > 0 ? (rv.Y[k - 1] - rv.Y[k]) / MathF.Max((rv.P[k] - rv.P[k - 1]).Length(), .01f) : 0, fall = Math.Clamp((drop - .22f) * 3.5f, 0f, 1f);
                var cw = new Color(tint.R, tint.G, tint.B, (byte)(fall * 255));
                if (have) { wb.TriFull(prevL, prevR, vr, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, cw, cw, cw, new Vector2(0, prevV), new Vector2(1, prevV), new Vector2(1, v)); wb.TriFull(prevL, vr, vl, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, cw, cw, cw, new Vector2(0, prevV), new Vector2(1, v), new Vector2(0, v)); g.HasWater = true; if (fall > .5f && k % 6 == 0) g.Mist.Add(new Vector3(p.X, y + .8f, p.Y)); }
                prevL = vl; prevR = vr; prevV = v; have = true;
            }
            // the waterfall that pours off the island's rim
            var end = rv.P[^1]; float wEnd = rv.W * 1.7f; var dirOut = Vector2.Normalize(end); var sideV = new Vector2(-dirOut.Y, dirOut.X); float yTop = rv.Y[^1], len = R * 1.15f + 20; var top = new Vector3(end.X, yTop, end.Y);
            var tl = top + new Vector3(sideV.X, 0, sideV.Y) * wEnd; var tr2 = top - new Vector3(sideV.X, 0, sideV.Y) * wEnd; var out2 = new Vector3(dirOut.X, 0, dirOut.Y) * (R * .06f); var bl = tl + out2 + new Vector3(0, -len, 0); var br = tr2 + out2 + new Vector3(0, -len, 0);
            var cf = new Color(tint.R, tint.G, tint.B, (byte)255); var nrm3 = new Vector3(dirOut.X, 0, dirOut.Y); float vlen = len / (wEnd * 2.6f);
            wb.TriFull(tl, tr2, br, nrm3, nrm3, nrm3, cf, cf, cf, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, vlen)); wb.TriFull(tl, br, bl, nrm3, nrm3, nrm3, cf, cf, cf, new Vector2(0, 0), new Vector2(1, vlen), new Vector2(0, vlen)); g.HasWater = true;
            g.Falls.Add((top + new Vector3(dirOut.X, 0, dirOut.Y) * 2 + new Vector3(0, -len * .98f, 0), wEnd * 5f)); g.Mist.Add(top + new Vector3(0, .6f, 0));
        }
        if (sh.PondR > 0)
        {
            float py = sh.Height(sh.Pond.X, sh.Pond.Y) + .72f; int seg = 22; var cwp = new Color(tint.R, tint.G, tint.B, (byte)0); var c0 = new Vector3(sh.Pond.X, py, sh.Pond.Y);
            for (int s = 0; s < seg; s++) { float a0 = s * MathF.Tau / seg, a1 = (s + 1) * MathF.Tau / seg; var b0 = c0 + new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * sh.PondR * 1.12f; var b1 = c0 + new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * sh.PondR * 1.12f; wb.TriFull(c0, b0, b1, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, cwp, cwp, cwp, new Vector2(.5f, .5f), new Vector2(.5f + MathF.Cos(a0) * .5f, .5f + MathF.Sin(a0) * .5f), new Vector2(.5f + MathF.Cos(a1) * .5f, .5f + MathF.Sin(a1) * .5f)); }
            g.HasWater = true;
        }
        prep.Water = wb;
        g.PadY = sh.Height(sh.Pad.X, sh.Pad.Y); g.ClearY = sh.Height(sh.Clearing.X, sh.Clearing.Y);
        // ---- crystal cluster (unit scale) ----
        var cm = new MeshBuilder(); var cc2 = C(0x9d86ff); (float dx, float dz, float h)[] cl2 = { (0, 0, 1.0f), (.5f, .2f, .65f), (-.45f, .1f, .7f), (.15f, -.5f, .55f), (-.2f, .5f, .5f) };
        for (int k = 0; k < cl2.Length; k++) { cm.T = Matrix4x4.CreateScale(.8f, cl2[k].h * 3.2f, .8f) * Matrix4x4.CreateRotationZ((2 - k) * .12f) * Matrix4x4.CreateRotationY(k) * Matrix4x4.CreateTranslation(cl2[k].dx * .09f, cl2[k].h * .1f, cl2[k].dz * .09f); cm.Octa(.07f, 1, cc2, 255); }
        prep.Crystal = cm;
        return prep;
    }

    // human-scale decor for walking: grass tufts and flowers within reach of the landing site
    public static void BuildDecor(IslandGfx g)
    {
        if (g.DecorBuilt) return; g.DecorBuilt = true; var sh = g.Shape; var rng = new Mul32(sh.Seed ^ 0x4421); float Rn() => rng.Next(); var mb = new MeshBuilder();
        int n = (int)Math.Clamp(sh.R * sh.R * .05f, 500, 8000); float reach = MathF.Min(sh.R * .85f, 190);
        for (int k = 0; k < n; k++)
        {
            float a = Rn() * 6.283f, rr = MathF.Sqrt(Rn()) * reach, x = sh.Pad.X + MathF.Cos(a) * rr, z = sh.Pad.Y + MathF.Sin(a) * rr; if (x * x + z * z > .88f * .88f * sh.R * sh.R) continue; if (sh.Wet(x, z, .5f) || sh.DistToTrails(x, z) < 1.1f) continue;
            float h = sh.Height(x, z); if (sh.Slope(x, z) > .7f || h > .11f * sh.R + .45f * sh.MaxHeight) continue; float s = .5f + Rn() * .5f; var gc = Hsl(.26f + Rn() * .06f, .55f, .28f + Rn() * .12f);
            for (int b = 0; b < 3; b++) { float ba = b * 2.09f + Rn(); var tip = new Vector3(x + MathF.Cos(ba) * .22f * s, h + .55f * s, z + MathF.Sin(ba) * .22f * s); mb.Tri(new Vector3(x - .05f * s, h, z), new Vector3(x + .05f * s, h, z), tip, gc, 0, Vector3.UnitY); }
            if (Rn() < .22f) { uint[] fc = { 0xffe066, 0xff7aa8, 0xffffff, 0xb08cff, 0xff9a4a }; var f = C(fc[(int)(Rn() * fc.Length) % fc.Length]); mb.CylY(.012f, .012f, .35f * s, x, h + .17f * s, z, Hsl(.3f, .5f, .25f), 4, 0, false); mb.Sphere(.1f * s, x, h + .38f * s, z, f, 1, .6f, 1, 60, 3, 5); }
        }
        g.Decor = mb.Build();
    }

    static List<Vector2> Resample(List<Vector2> pts, float step)
    {
        var o = new List<Vector2> { pts[0] }; float acc = 0; for (int i = 1; i < pts.Count; i++) { var d = pts[i] - pts[i - 1]; float l = d.Length(); if (l < 1e-5f) continue; float pos = step - acc; while (pos <= l) { o.Add(pts[i - 1] + d * (pos / l)); pos += step; } acc = (acc + l) % step; }
        if (Vector2.Distance(o[^1], pts[^1]) > .05f) o.Add(pts[^1]); return o;
    }
    static (List<Vector3>, List<int[]>) IcoSphere(int sub)
    {
        float t = (1 + MathF.Sqrt(5)) / 2; var v = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < v.Count; i++) v[i] = Vector3.Normalize(v[i]);
        var f = new List<int[]> { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 }, new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
        for (int s = 0; s < sub; s++)
        {
            var cache = new Dictionary<long, int>(); var nf = new List<int[]>(); int Mid(int a, int b) { long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; if (cache.TryGetValue(k, out int m)) return m; v.Add(Vector3.Normalize((v[a] + v[b]) / 2)); return cache[k] = v.Count - 1; }
            foreach (var tr in f) { int a = Mid(tr[0], tr[1]), b = Mid(tr[1], tr[2]), c = Mid(tr[2], tr[0]); nf.Add(new[] { tr[0], a, c }); nf.Add(new[] { tr[1], b, a }); nf.Add(new[] { tr[2], c, b }); nf.Add(new[] { a, b, c }); }
            f = nf;
        }
        return (v, f);
    }
}

public static partial class MonsterGfx
{
    static readonly MeshBuilder?[] protos = new MeshBuilder?[3];
    // a tree with glowing red eyes, a dark mouth, raised branch-arms and root-feet (unit height, faces +z)
    public static MeshBuilder Proto(int kind)
    {
        if (protos[kind] is { } have) return have; var mb = new MeshBuilder(); mb.Append(IslandBuilder.TreeProto(kind, 0), Matrix4x4.Identity, .8f);
        float ez = kind == 0 ? .3f : .075f, ey = kind == 0 ? .42f : .46f; var eye = Col.Hex(0xff3a2a); var dark = Col.Hex(0x1a0f0c); var bark = Col.Hex(0x4a3020);
        foreach (int s in new[] { -1, 1 }) { mb.Sphere(.034f, s * .06f, ey, ez, eye, 1.3f, .8f, .7f, 255, 4, 6); }
        mb.Box(.1f, .035f, .03f, 0, ey - .1f, ez, dark); mb.Box(.07f, .02f, .03f, 0, ey - .13f, ez, dark);
        foreach (int s in new[] { -1, 1 }) { var sv = mb.T; mb.T = Matrix4x4.CreateRotationZ(-s * 1.0f) * Matrix4x4.CreateRotationX(-.5f) * Matrix4x4.CreateTranslation(s * .06f, .4f, .04f) * sv; mb.CylY(.014f, .03f, .34f, 0, .17f, 0, bark, 5); mb.T = sv; }
        for (int k = 0; k < 4; k++) { float a = k * 1.57f + .4f; var sv = mb.T; mb.T = Matrix4x4.CreateRotationZ(MathF.Cos(a) * .9f) * Matrix4x4.CreateRotationX(MathF.Sin(a) * .9f) * Matrix4x4.CreateTranslation(MathF.Cos(a) * .06f, .03f, MathF.Sin(a) * .06f) * sv; mb.CylY(.03f, .008f, .2f, 0, -.02f, 0, bark, 5); mb.T = sv; }
        protos[kind] = mb; return mb;
    }
}
