// The commander's pick axe and laser gun: first-person viewmodels, swinging, firing, ranged mining; and the gear every NPC carries.
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    [DllImport("opengl32.dll")] static extern void glClear(uint mask);
    int evaTool; // 0 pick axe, 1 laser gun
    Mesh pickMesh, gunMesh; bool toolsBuilt; Mesh[] figGear = new Mesh[5];
    float swingT, kick, boltT, flashT, fireCd; Vector3 boltA, boltB; bool boltHit;

    void BuildTools()
    {
        if (toolsBuilt) return; toolsBuilt = true; var steel = Col.Hex(0x9aa4b8); var wood = Col.Hex(0x7a5230); var gold = Col.Hex(0xe0b84a); var dark = Col.Hex(0x2a3040);
        var p = new MeshBuilder(); p.CylY(.016f, .02f, .78f, 0, .39f, 0, wood, 8); p.CylY(.024f, .024f, .05f, 0, .05f, 0, gold, 8); p.CylY(.024f, .024f, .05f, 0, .26f, 0, gold, 8);
        p.Box(.07f, .07f, .09f, 0, .79f, 0, dark); p.Box(.34f, .04f, .05f, 0, .8f, 0, steel);
        foreach (int s in new[] { -1, 1 }) { var sv = p.T; p.T = Matrix4x4.CreateRotationZ(-s * MathF.PI / 2 + s * .2f) * Matrix4x4.CreateTranslation(s * .17f, .8f, 0) * sv; p.CylY(.026f, 0f, .16f, 0, 0, 0, steel, 6); p.T = sv; }
        pickMesh = p.Build();
        var g = new MeshBuilder(); var body = Col.Hex(0x4a5468);
        g.Box(.075f, .12f, .34f, 0, 0, -.1f, body); g.Box(.06f, .05f, .3f, 0, .07f, -.1f, dark); g.CylZ(.022f, .022f, .26f, 0, .005f, -.37f, dark, 8); g.CylZ(.03f, .03f, .05f, 0, .005f, -.28f, gold, 8);
        g.Box(.05f, .15f, .07f, 0, -.12f, .02f, dark); g.Box(.012f, .02f, .24f, 0, .04f, -.12f, Col.Hex(0x5adcff), 255); g.Box(.02f, .04f, .02f, 0, .1f, -.22f, steel);
        for (int k = 0; k < 3; k++) g.CylZ(.034f, .034f, .012f, 0, .005f, -.18f - k * .04f, Col.Hex(0x40e0ff), 8, 255);
        gunMesh = g.Build();
        for (int i = 0; i < 5; i++) figGear[i] = Scene.BuildFigureGear(i);
    }

    // ---- ray from the crosshair against the ore nodes (laser mining, aiming) ----
    int RayOreNode(out float dist)
    {
        dist = 1e9f; var c = sim.G.cap; if (c.state != "landed") return -1; var isl = sim.G.isl[c.island]; var nodes = Sim.OreNodes(isl); int vis = sim.VisibleNodes(isl);
        var fwd = Vector3.Normalize(cam3.Target - cam3.Position); var ray = new Ray(cam3.Position, fwd); int best = -1;
        for (int k = 0; k < vis; k++) { var p = new Vector3(isl.x + nodes[k].x, sim.GroundY(isl, nodes[k].x, nodes[k].z) + .9f, isl.y + nodes[k].z); var h = Raylib.GetRayCollisionSphere(ray, p, 1.3f); if (h.Hit && h.Distance < dist) { dist = h.Distance; best = k; } }
        return best;
    }

    // ---- per-frame use of the tools while on foot ----
    void UseTools(float dt)
    {
        var c = sim.G.cap; if (c.state != "landed") return;
        if (Raylib.IsKeyPressed(KeyboardKey.One)) evaTool = 0; if (Raylib.IsKeyPressed(KeyboardKey.Two)) evaTool = 1;
        bool lmb = Raylib.IsMouseButtonDown(MouseButton.Left) && !UiOver(), eHeld = Raylib.IsKeyDown(KeyboardKey.E), mining = false;
        fireCd -= dt; kick = MathF.Max(0, kick - dt * 6); boltT -= dt; flashT -= dt;
        if (evaTool == 0) { mining = sim.MineTick(dt, eHeld || lmb); if (mining) swingT += dt; else swingT = 0; }
        else
        {
            if (lmb)
            {
                int n = RayOreNode(out float d); bool onNode = n >= 0 && d < 38;
                if (fireCd <= 0) { fireCd = .22f; kick = 1; flashT = .06f; boltT = .1f; var fwd = Vector3.Normalize(cam3.Target - cam3.Position); var right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY)); boltA = cam3.Position + right * .22f + Vector3.Normalize(Vector3.Cross(right, fwd)) * -.18f + fwd * .7f; boltB = onNode ? cam3.Position + fwd * d : cam3.Position + fwd * 70; boltHit = onNode; Play("order"); }
                mining = onNode && sim.MineNode(dt, n, 1.6f); if (!onNode) c.mineT = 0;
            }
            else if (eHeld) mining = sim.MineTick(dt, true); else c.mineT = 0;
        }
        evaSpark = mining ? .2f : MathF.Max(0, evaSpark - dt);
    }

    // ---- first-person viewmodel: its own depth range so it never clips into the world ----
    void DrawViewmodel(float t)
    {
        if (mode != Mode.Eva || sim.G.cap.state != "landed") return;
        Rlgl.DrawRenderBatchActive(); try { glClear(0x00000100); } catch { }
        Rlgl.SetClipPlanes(.02, 30); var vcam = new Camera3D(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, 62, CameraProjection.Perspective); Raylib.BeginMode3D(vcam);
        lit.SetExterior(Vector3.Zero); float walk = sim.G.cap.mineT > 0 ? 0 : 1; float bob = MathF.Sin(t * 7) * .008f * walk; Matrix4x4 m;
        if (evaTool == 0)
        {
            float a = swingT > 0 ? MathF.Sin(swingT * 9) : 0; // 0 idle .. swinging
            m = Matrix4x4.CreateRotationX(-.35f + a * 1.15f) * Matrix4x4.CreateRotationY(.25f) * Matrix4x4.CreateRotationZ(-.18f) * Matrix4x4.CreateTranslation(.3f, -.5f + bob - a * .06f, -.62f - a * .12f);
            lit.Draw(pickMesh, m);
        }
        else
        {
            m = Matrix4x4.CreateScale(.62f) * Matrix4x4.CreateRotationX(.02f + kick * .1f) * Matrix4x4.CreateRotationY(.05f) * Matrix4x4.CreateTranslation(.2f, -.17f + bob, -.55f + kick * .05f); lit.Draw(gunMesh, m);
            if (flashT > 0) { Rlgl.DisableDepthMask(); Raylib.BeginBlendMode(BlendMode.Additive); Raylib.DrawBillboard(vcam, glowTex, Vector3.Transform(new Vector3(0, .005f, -.52f), m), .22f, Col.Hex(0x9ff4ff)); Raylib.EndBlendMode(); Rlgl.EnableDepthMask(); }
        }
        Raylib.EndMode3D();
    }
    void DrawBolt(float t)
    {
        if (boltT <= 0 || mode != Mode.Eva) return; float k = MathF.Min(1, boltT / .1f);
        for (int j = -1; j <= 1; j++) Raylib.DrawLine3D(boltA + new Vector3(0, j * .015f, 0), boltB + new Vector3(0, j * .015f, 0), Tint(Col.Hex(0x7fe8ff), .9f * k));
        Raylib.DrawCylinderEx(boltA, boltB, .035f, .02f, 5, Tint(Col.Hex(0xcff8ff), .7f * k)); if (boltHit) Billboard(cam3, boltB, 1.6f * k + .4f, Tint(Col.Hex(0xffe0ff), .9f));
    }
}
