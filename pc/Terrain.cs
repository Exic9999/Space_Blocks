// Pure island terrain (no rendering): heightfield with mountains, carved rivers that flow off the rim, flattened pads and ore
// clearings, walking trails (A* over the terrain, avoiding water), tree points and ore-node positions. Deterministic per (seed, radius).
using System.Numerics;
namespace SpaceBlocks;

public sealed class Peak { public float x, z, h, s; }
public sealed class FlatZone { public float x, z, r, h; }
public sealed class River { public List<Vector2> P = new(); public List<float> Y = new(); public float W, Depth; }
public sealed class TreeP { public float x, z, size, rot; public int kind; }

public sealed class IslandShape
{
    public readonly float R; public readonly int Seed;
    public readonly List<Peak> Peaks = new(); public readonly List<FlatZone> Flats = new(); public readonly List<River> Rivers = new();
    public readonly List<List<Vector2>> Trails = new(); public readonly List<TreeP> Trees = new();
    public Vector2 Pad, Clearing; public readonly List<Vector2> Clusters = new(); public readonly List<Vector2> Nodes = new(); public Vector2 Pond; public float PondR;
    public float MaxHeight, Summit; public Vector2 SummitPos; public int ClassIdx; // 0 small, 1 medium, 2 large, 3 huge

    public static int ClassOf(float R) => R < 65 ? 0 : R < 135 ? 1 : R < 260 ? 2 : 3;

    // ---------- noise ----------
    static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1442695041; h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16; return (h & 0xffffff) / 16777215f; } }
    static float VNoise(float x, float y, int s)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y); float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s); return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }
    public static float Fbm(float x, float y, int s, int oct = 3) { float v = 0, a = .5f, f = 1; for (int i = 0; i < oct; i++) { v += VNoise(x * f, y * f, s + i * 31) * a; a *= .5f; f *= 2.03f; } return v / .875f; }
    static float Sm(float x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    // ---------- height ----------
    float Raw(float x, float z)
    {
        float r = MathF.Sqrt(x * x + z * z), h = .11f * R + (Fbm(x / (R * .33f), z / (R * .33f), Seed) - .5f) * 2 * .035f * R;
        foreach (var p in Peaks)
        {
            float dx = x - p.x, dz = z - p.z, g = MathF.Exp(-(dx * dx + dz * dz) / (p.s * p.s)); if (g < .002f) continue;
            float rid = 1 - MathF.Abs(2 * Fbm(x * .9f / p.s, z * .9f / p.s, Seed + 7) - 1); h += p.h * g * (.74f + .5f * rid) * (1 - .35f * (1 - g));
        }
        float t = Sm((r - .86f * R) / (.14f * R)); return h * (1 - t) + .06f * R * t;
    }
    float FlatWeight(float x, float z, out float target)
    {
        float best = 0; target = 0;
        foreach (var f in Flats) { float d = MathF.Sqrt((x - f.x) * (x - f.x) + (z - f.z) * (z - f.z)); float w = 1 - Sm((d - f.r * .75f) / (f.r * .75f)); if (w > best) { best = w; target = f.h; } }
        return best;
    }
    // distance to the nearest river centreline and that river's width (and pond)
    public float RiverDist(float x, float z, out float width)
    {
        float best = 1e9f; width = 1;
        foreach (var rv in Rivers)
            for (int k = 0; k + 1 < rv.P.Count; k++)
            {
                var a = rv.P[k]; var b = rv.P[k + 1]; float abx = b.X - a.X, abz = b.Y - a.Y, l2 = abx * abx + abz * abz, t = l2 < 1e-6f ? 0 : Math.Clamp(((x - a.X) * abx + (z - a.Y) * abz) / l2, 0, 1);
                float px = a.X + abx * t - x, pz = a.Y + abz * t - z, d = px * px + pz * pz; if (d < best) { best = d; width = rv.W * (1 + .5f * ((k + t) / rv.P.Count)); }
            }
        return MathF.Sqrt(best);
    }
    public float Height(float x, float z)
    {
        float h = Raw(x, z); float fw = FlatWeight(x, z, out float ft); if (fw > 0) h = h * (1 - fw) + ft * fw;
        if (Rivers.Count > 0)
        {
            float d = RiverDist(x, z, out float w); float reach = w * 3.2f;
            if (d < reach) { float depth = MathF.Max(.9f, w * .75f), u = 1 - d / reach; h -= depth * u * u * (1 - fw); }
        }
        if (PondR > 0) { float d = MathF.Sqrt((x - Pond.X) * (x - Pond.X) + (z - Pond.Y) * (z - Pond.Y)); if (d < PondR * 1.8f) { float u = 1 - d / (PondR * 1.8f); h -= 1.0f * u * u * (1 - fw); } }
        return h;
    }
    public bool Wet(float x, float z, float margin = 0)
    {
        if (PondR > 0 && MathF.Sqrt((x - Pond.X) * (x - Pond.X) + (z - Pond.Y) * (z - Pond.Y)) < PondR + margin) return true;
        if (Rivers.Count == 0) return false; float d = RiverDist(x, z, out float w); return d < w * .95f + margin;
    }
    public float Slope(float x, float z) { float e = 1.2f; float dx = Height(x + e, z) - Height(x - e, z), dz = Height(x, z + e) - Height(x, z - e); return MathF.Sqrt(dx * dx + dz * dz) / (2 * e); }

    // ---------- construction ----------
    public IslandShape(int seed, float R)
    {
        this.R = R; Seed = seed; ClassIdx = ClassOf(R); var rng = new Mul32(seed ^ 0x2f3a); float Rn() => rng.Next();
        float baseH = .11f * R;
        // landing pad and the crystal clearing (both flattened)
        Pad = new Vector2(-.25f * R, .16f * R); Clearing = new Vector2(-.25f * R, -.1f * R);
        Flats.Add(new FlatZone { x = Pad.X, z = Pad.Y, r = MathF.Max(7, R * .07f), h = baseH }); Flats.Add(new FlatZone { x = Clearing.X, z = Clearing.Y, r = MathF.Max(10, R * .18f), h = baseH });
        // extra ore clusters on big islands
        int extra = ClassIdx switch { 2 => 2, 3 => 4, _ => 0 };
        for (int k = 0; k < extra; k++)
            for (int tries = 0; tries < 60; tries++)
            {
                float a = Rn() * 6.283f, rr = R * (.3f + Rn() * .35f), x = MathF.Cos(a) * rr, z = MathF.Sin(a) * rr; bool ok = true;
                foreach (var f in Flats) if (Hyp(x - f.x, z - f.z) < f.r + 22 + R * .06f) ok = false; if (!ok) continue;
                Clusters.Add(new Vector2(x, z)); Flats.Add(new FlatZone { x = x, z = z, r = 8, h = baseH }); break;
            }
        // mountains
        int npk = ClassIdx switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 };
        for (int k = 0; k < npk; k++)
            for (int tries = 0; tries < 80; tries++)
            {
                float a = Rn() * 6.283f, rr = R * (.28f + Rn() * .26f), x = MathF.Cos(a) * rr, z = MathF.Sin(a) * rr, s = R * (.17f + Rn() * .07f); bool ok = true;
                foreach (var f in Flats) if (Hyp(x - f.x, z - f.z) < f.r + s * 1.6f) ok = false;
                foreach (var q in Peaks) if (Hyp(x - q.x, z - q.z) < (s + q.s) * 1.3f) ok = false; if (!ok) continue;
                float h = R * (ClassIdx == 3 ? (k == 0 ? .42f : .3f + Rn() * .06f) : ClassIdx == 2 ? .3f + Rn() * .06f : .26f + Rn() * .05f);
                Peaks.Add(new Peak { x = x, z = z, h = h, s = s }); break;
            }
        // flat zones take the local base height
        foreach (var f in Flats) f.h = Raw(f.x, f.z) < baseH + .5f * R ? baseH + (Fbm(f.x * .05f, f.z * .05f, Seed) - .5f) * .02f * R : baseH;
        // rivers: from a spring on the flank of each mountain, down the steepest path, off the rim
        int nr = Math.Min(Peaks.Count, 3);
        for (int k = 0; k < nr; k++) BuildRiver(Peaks[k], k, Rn);
        if (Rivers.Count > 0) { var rv = Rivers[0]; Pond = rv.P[0]; PondR = rv.W * 2.4f; }
        else if (ClassIdx == 0) { Pond = new Vector2(R * .3f, R * .15f); PondR = MathF.Max(2.2f, R * .07f); } // small islands: a spring pool
        // height range, summit
        float mx = 0; for (int i = -20; i <= 20; i++) for (int j = -20; j <= 20; j++) { float x = i * R / 20, z = j * R / 20; if (x * x + z * z > R * R) continue; float h = Height(x, z); if (h > mx) { mx = h; SummitPos = new Vector2(x, z); } }
        MaxHeight = mx; Summit = mx;
        BuildTrails(); ScatterTrees(Rn); BuildNodes();
    }
    static float Hyp(float a, float b) => MathF.Sqrt(a * a + b * b);

    void BuildRiver(Peak pk, int index, Func<float> Rn)
    {
        Vector2 start = default; Vector2 dir = Vector2.UnitX; bool found = false; float a0 = Rn() * 6.283f;
        for (int j = 0; j < 14 && !found; j++)
        {
            float a = a0 + j * .5f; var d = new Vector2(MathF.Cos(a), MathF.Sin(a)); var p = new Vector2(pk.x, pk.z) + d * pk.s * .55f; bool ok = Hyp(p.X, p.Y) < .8f * R;
            foreach (var f in Flats) if (Hyp(p.X - f.x, p.Y - f.z) < f.r + 12) ok = false; if (ok) { start = p; dir = d; found = true; }
        }
        if (!found) return;
        var rv = new River { W = Math.Clamp(R * .016f, 1.2f, 4.2f) }; rv.Depth = MathF.Max(.9f, rv.W * .75f);
        var pos = start; var v = dir; float ds = MathF.Max(R / 80f, 1.1f); rv.P.Add(pos); float hStall = Raw(pos.X, pos.Y); int stall = 0; bool reached = false;
        for (int step = 0; step < 900 && !reached; step++)
        {
            float e = 1.0f; var g = new Vector2((Raw(pos.X + e, pos.Y) - Raw(pos.X - e, pos.Y)) / (2 * e), (Raw(pos.X, pos.Y + e) - Raw(pos.X, pos.Y - e)) / (2 * e));
            var want = -g; if (want.Length() < 1e-4f) want = pos / MathF.Max(pos.Length(), 1e-3f); else want = Vector2.Normalize(want);
            foreach (var f in Flats) { float d = Hyp(pos.X - f.x, pos.Y - f.z); if (d < f.r * 1.5f + 4) want += (pos - new Vector2(f.x, f.z)) / MathF.Max(d, .1f) * 1.2f; }
            v = Vector2.Normalize(v * .6f + Vector2.Normalize(want) * .4f); pos += v * ds; rv.P.Add(pos);
            float h = Raw(pos.X, pos.Y); if (h > hStall - .02f * ds) stall++; else { stall = 0; hStall = h; }
            if (stall > 25) { v = Vector2.Normalize(Vector2.Normalize(pos) * .8f + v * .2f); stall = 0; hStall = h; }
            if (pos.Length() >= .995f * R) reached = true;
        }
        if (!reached) { var dirOut = Vector2.Normalize(pos); rv.P.Add(dirOut * R * 1.0f); }
        else { rv.P[^1] = Vector2.Normalize(rv.P[^1]) * R * 1.0f; }
        // smooth twice (Chaikin), keep endpoints
        for (int it = 0; it < 2; it++) { var q = new List<Vector2> { rv.P[0] }; for (int i = 0; i + 1 < rv.P.Count; i++) { q.Add(rv.P[i] * .75f + rv.P[i + 1] * .25f); q.Add(rv.P[i] * .25f + rv.P[i + 1] * .75f); } q.Add(rv.P[^1]); rv.P = q; }
        Rivers.Add(rv);
    }
    void FinishRiverHeights()
    {
        foreach (var rv in Rivers) { rv.Y.Clear(); float prev = float.MaxValue; foreach (var p in rv.P) { float y = Height(p.X, p.Y) + rv.Depth * .32f; y = MathF.Min(y, prev - .01f); rv.Y.Add(y); prev = y; } }
    }

    // ---------- trails (A* on a coarse grid, water blocked, slope penalised) ----------
    void BuildTrails()
    {
        FinishRiverHeights();
        float cs = Math.Clamp(R / 42f, 1.4f, 6f); int n = (int)MathF.Ceiling(2 * R / cs) + 1; float o = -R;
        var hgt = new float[n * n]; var blocked = new bool[n * n]; var have = new bool[n * n];
        float H(int i, int j) { int k = i * n + j; if (!have[k]) { float x = o + i * cs, z = o + j * cs; hgt[k] = Height(x, z); blocked[k] = x * x + z * z > .88f * .88f * R * R || Wet(x, z, 2.4f + cs * .4f); have[k] = true; } return hgt[k]; }
        bool Blk(int i, int j) { if (i < 0 || j < 0 || i >= n || j >= n) return true; H(i, j); return blocked[i * n + j]; }
        (int, int) Cell(Vector2 p) => (Math.Clamp((int)MathF.Round((p.X - o) / cs), 0, n - 1), Math.Clamp((int)MathF.Round((p.Y - o) / cs), 0, n - 1));
        List<Vector2>? Path(Vector2 a, Vector2 b)
        {
            var (si, sj) = Cell(a); var (ti, tj) = Cell(b); if (Blk(si, sj) || Blk(ti, tj)) return null;
            var g = new float[n * n]; Array.Fill(g, float.MaxValue); var par = new int[n * n]; Array.Fill(par, -1); var closed = new bool[n * n];
            var pq = new PriorityQueue<int, float>(); int s0 = si * n + sj, t0 = ti * n + tj; g[s0] = 0; pq.Enqueue(s0, 0);
            while (pq.Count > 0)
            {
                int cur = pq.Dequeue(); if (closed[cur]) continue; closed[cur] = true; if (cur == t0) break; int ci = cur / n, cj = cur % n;
                for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue; int ni = ci + di, nj = cj + dj; if (Blk(ni, nj)) continue; int nk = ni * n + nj; if (closed[nk]) continue;
                    float step = cs * (di != 0 && dj != 0 ? 1.414f : 1f), rise = MathF.Abs(H(ni, nj) - H(ci, cj)) / cs, cost = step * (1 + 26 * rise * rise + 3 * rise);
                    float ng = g[cur] + cost; if (ng < g[nk]) { g[nk] = ng; par[nk] = cur; float hx = (ni - ti) * cs, hz = (nj - tj) * cs; pq.Enqueue(nk, ng + MathF.Sqrt(hx * hx + hz * hz)); }
                }
            }
            if (par[t0] < 0 && t0 != s0) return null; var pts = new List<Vector2>(); for (int c = t0; c >= 0; c = par[c]) { pts.Add(new Vector2(o + (c / n) * cs, o + (c % n) * cs)); if (c == s0) break; }
            pts.Reverse(); for (int pass = 0; pass < 6 && pts.Count > 4; pass++) for (int k = 1; k + 1 < pts.Count - 1; k++) pts[k] = pts[k] * .5f + (pts[k - 1] + pts[k + 1]) * .25f; // relax the grid zig-zag
            for (int it = 0; it < 2 && pts.Count > 2; it++) { var q = new List<Vector2> { pts[0] }; for (int i = 0; i + 1 < pts.Count; i++) { q.Add(pts[i] * .75f + pts[i + 1] * .25f); q.Add(pts[i] * .25f + pts[i + 1] * .75f); } q.Add(pts[^1]); pts = q; }
            return pts;
        }
        void Add(Vector2 a, Vector2 b) { var p = Path(a, b); if (p != null && p.Count >= 3) Trails.Add(p); }
        var padP = Pad + new Vector2(0, -1f); var toClear = Clearing + new Vector2(0, MathF.Max(10, R * .18f) * .8f);
        Add(padP, toClear);
        foreach (var c in Clusters) Add(Clearing + new Vector2(-4, 0), c + new Vector2(0, 5));
        int climbs = Math.Min(Peaks.Count, ClassIdx == 3 ? 3 : ClassIdx == 2 ? 2 : 1);
        for (int k = 0; k < climbs; k++) { var pk = Peaks[k]; Add(padP + new Vector2(2, 0), new Vector2(pk.x, pk.z)); }
        if (Peaks.Count == 0 && ClassIdx == 1) Add(padP, new Vector2(R * .45f, -R * .3f));
    }

    public float DistToTrails(float x, float z)
    {
        float best = 1e9f; foreach (var t in Trails) for (int k = 0; k + 1 < t.Count; k++) { var a = t[k]; var b = t[k + 1]; float abx = b.X - a.X, abz = b.Y - a.Y, l2 = abx * abx + abz * abz, u = l2 < 1e-6f ? 0 : Math.Clamp(((x - a.X) * abx + (z - a.Y) * abz) / l2, 0, 1); float px = a.X + abx * u - x, pz = a.Y + abz * u - z; best = MathF.Min(best, px * px + pz * pz); }
        return MathF.Sqrt(best);
    }

    // ---------- trees & ore ----------
    void ScatterTrees(Func<float> Rn)
    {
        int target = Math.Clamp((int)(R * R * .0115f), 25, 3200); float treeLine = .11f * R + .5f * (Peaks.Count > 0 ? Peaks.Max(p => p.h) : 0) + 3; var grid = new HashSet<long>(); float cell = 2.4f;
        for (int tries = 0; tries < target * 8 && Trees.Count < target; tries++)
        {
            float a = Rn() * 6.283f, rr = MathF.Sqrt(Rn()) * R * .9f, x = MathF.Cos(a) * rr, z = MathF.Sin(a) * rr; bool ok = true;
            foreach (var f in Flats) if (Hyp(x - f.x, z - f.z) < f.r + 2.5f) { ok = false; break; } if (!ok) continue;
            if (Wet(x, z, 3.2f) || DistToTrails(x, z) < 3.4f) continue; float h = Height(x, z); if (h > treeLine) continue; if (Slope(x, z) > .85f) continue;
            long key = ((long)(int)(x / cell) << 32) ^ (uint)(int)(z / cell); if (!grid.Add(key)) continue;
            Trees.Add(new TreeP { x = x, z = z, size = .75f + Rn() * .6f, rot = Rn() * 6.283f, kind = Rn() < .55f ? 0 : Rn() < .6f ? 1 : 2 });
        }
    }
    void BuildNodes()
    {
        int n0 = ClassIdx == 0 ? 6 : 8; float ring = MathF.Max(6.2f, R * .2f); ring = MathF.Min(ring, MathF.Max(10, R * .18f) * .85f);
        for (int k = 0; k < n0; k++) { float a = MathF.PI / 2 + .6f + k * ((MathF.Tau - 1.2f) / n0); Nodes.Add(Clearing + new Vector2(MathF.Cos(a), MathF.Sin(a)) * ring); }
        foreach (var c in Clusters) for (int k = 0; k < 6; k++) { float a = k * MathF.Tau / 6 + .4f; Nodes.Add(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 5.2f); }
    }
}

public static class Terrain
{
    static readonly Dictionary<(int, int), IslandShape> cache = new();
    public static IslandShape Get(int seed, float R) { var k = (seed, (int)MathF.Round(R * 10)); lock (cache) { if (!cache.TryGetValue(k, out var s)) cache[k] = s = new IslandShape(seed, R); return s; } }
    public static IslandShape Get(Island i) => Get(i.seed, i.s);
}
