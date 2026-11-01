// Walk-mode collision (hangar / corridor / bridge). Port of RECTS/BLOCK/TANKS/circlesBlocked.
namespace SpaceBlocks;

public static class Mx { public static float Hypot(float a, float b) => MathF.Sqrt(a * a + b * b); }

public sealed class Walker
{
    public float x, z = -23, yaw, pitch, bob;
    static readonly float[][] Rects = { new[]{-16f,16f,-6f,34f}, new[]{-3f,3f,-19.2f,-4.8f}, new[]{-14f,14f,-38f,-18f},
        new[]{-17f,-1.5f,-17.5f,-6.5f}, new[]{1.5f,17f,-17.5f,-6.5f}, new[]{-32f,-14.5f,-1f,15f} }; // + quarters, engineering, cargo hold (each overlaps its neighbour by > 0.9 so doors are walkable)
    static readonly float[][] Block = { new[]{-11f,11f,-38f,-35.3f}, new[]{-14f,-12.4f,-34f,-19.6f}, new[]{12.4f,14f,-34f,-19.6f}, new[]{-.9f,.9f,-21.8f,-20.2f},
        // corridor walls (door gap z -14..-10) and the hangar west wall (door gap z 3..7.5)
        new[]{-3.2f,-2.8f,-18.5f,-14f}, new[]{-3.2f,-2.8f,-10f,-5.9f}, new[]{2.8f,3.2f,-18.5f,-14f}, new[]{2.8f,3.2f,-10f,-5.9f}, new[]{-16.2f,-15.8f,-6f,3f}, new[]{-16.2f,-15.8f,7.5f,34f},
        // quarters: bunks, table, lockers, plant
        new[]{-17f,-14.9f,-16.9f,-6.9f}, new[]{-10.9f,-7.1f,-15.1f,-13.7f}, new[]{-15.6f,-4.6f,-17.5f,-16.7f}, new[]{-4.8f,-3.5f,-17f,-15.8f},
        // engineering: reactor, console bank
        new[]{9.4f,12.6f,-10.6f,-7.4f}, new[]{15.7f,17f,-17f,-6.9f},
        // cargo hold: crate rows, ore bin, west shelves
        new[]{-32f,-18f,-1f,3f}, new[]{-32f,-18f,9f,15f}, new[]{-30f,-26f,3f,8f}, new[]{-32f,-30.8f,-1f,15f} };
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
        if (Mx.Hypot(nx - Data.CapPad[0], nz - Data.CapPad[1]) < 3.4f && !Away(Data.CapPad[0], Data.CapPad[1]) && sim.G.cap.state is "docked" or "launch" or "dock") return true;
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
    public string Where() =>
        x < -16.2f && z > -1.5f && z < 15.5f ? "Cargo Hold" : x < -3.3f && z < -6.4f && z > -18 ? "Crew Quarters" : x > 3.3f && z < -6.4f && z > -18 ? "Engineering"
        : z < -18 ? "Command Bridge" : z < -5.8f ? "Main Corridor" : "Hangar Deck";
}
