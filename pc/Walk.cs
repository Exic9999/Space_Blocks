// Walk-mode collision (hangar / corridor / bridge). Port of RECTS/BLOCK/TANKS/circlesBlocked.
namespace SpaceBlocks;

public static class Mx { public static float Hypot(float a, float b) => MathF.Sqrt(a * a + b * b); }

public sealed class Walker
{
    public float x, z = -23, yaw, pitch, bob;
    static readonly float[][] Rects = { new[]{-16f,16f,-6f,34f}, new[]{-3f,3f,-19.2f,-4.8f}, new[]{-14f,14f,-38f,-18f} };
    static readonly float[][] Block = { new[]{-11f,11f,-38f,-35.3f}, new[]{-14f,-12.4f,-34f,-19.6f}, new[]{12.4f,14f,-34f,-19.6f}, new[]{-.9f,.9f,-21.8f,-20.2f} };
    public static readonly float[][] Tanks = { new[]{-14.2f,28f}, new[]{14.2f,28f}, new[]{-14.2f,-1f}, new[]{14.2f,-1f} };

    public static bool Inside(float x, float z, float r)
    {
        bool ok = false;
        foreach (var q in Rects) if (x > q[0] + r && x < q[1] - r && z > q[2] + r && z < q[3] - r) { ok = true; break; }
        if (!ok) return false;
        foreach (var q in Block) if (x > q[0] - r && x < q[1] + r && z > q[2] - r && z < q[3] + r) return false;
        return true;
    }

    bool CirclesBlocked(Sim sim, float nx, float nz)
    {
        bool Away(float cx, float cz) => Mx.Hypot(nx - cx, nz - cz) > Mx.Hypot(x - cx, z - cz);
        for (int k = 0; k < Data.Pads.Length; k++)
        {
            var p = Data.Pads[k];
            if (Mx.Hypot(nx - p[0], nz - p[1]) < 3.4f && !Away(p[0], p[1]))
                foreach (var s in sim.G.ships)
                    if (s.idx % 5 == k && (s.state == "docked" || s.state == "landing" || s.state == "takeoff")) return true;
        }
        if (Mx.Hypot(nx, nz + 28) < 2.9f && !Away(0, -28)) return true;
        foreach (var t in Tanks) if (Mx.Hypot(nx - t[0], nz - t[1]) < 2.3f && !Away(t[0], t[1])) return true;
        return false;
    }

    // mx: strafe (+right), mz: +1 = backward / -1 = forward (same sign convention as the web build)
    public void Step(Sim sim, float dt, float mx, float mz)
    {
        float l = Mx.Hypot(mx, mz); if (l > 1) { mx /= l; mz /= l; }
        const float sp = 4.2f; float sy = MathF.Sin(yaw), cy = MathF.Cos(yaw);
        float dx = (mx * cy + mz * sy) * sp * dt, dz = (-mx * sy + mz * cy) * sp * dt;
        float nx = x + dx; if (Inside(nx, z, .45f) && !CirclesBlocked(sim, nx, z)) x = nx;
        float nz = z + dz; if (Inside(x, nz, .45f) && !CirclesBlocked(sim, x, nz)) z = nz;
        bob += l * dt;
    }
    public string Where() => z < -18 ? "Command Bridge" : z < -5.8f ? "Main Corridor" : "Hangar Deck";
}
