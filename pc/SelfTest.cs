// Headless invariants mirroring tests/web3d/test_3d.py (sim, save, walk). Run: SpaceBlocks --selftest
namespace SpaceBlocks;

public static class SelfTest
{
    static int pass, fail;
    static void Ok(string name, bool c, string extra = "")
    {
        if (c) pass++; else fail++;
        Console.WriteLine((c ? "PASS " : "FAIL ") + name + (!c && extra != "" ? " :: " + extra : ""));
    }

    public static int Run()
    {
        var sim = new Sim { Rng = new Random(7) }; sim.NewGame(1234);
        var G = sim.G;
        Ok("3 pilots at start", G.ships.Count == 3);
        Ok("14 islands, 3 charted", G.isl.Count == 14 && G.isl.Count(i => i.found) == 3);

        var s0 = G.ships[0];
        Ok("lock-on gives mine order", sim.GiveOrder(s0, "mine", 0) && s0.order!.type == "mine" && s0.order.isl == 0);
        sim.GiveOrder(s0, "hold"); Ok("order hold", s0.order!.type == "hold");
        sim.GiveOrder(s0, "scout"); Ok("order scout", s0.order!.type == "scout");
        sim.GiveOrder(s0, "auto"); Ok("order auto", s0.order!.type == "auto");

        G.aeth = 1000; int before = s0.lv.eng;
        Ok("buy engine upgrade", sim.BuyShipUp(s0, "eng") && s0.lv.eng == before + 1);
        Ok("buy hull plating", sim.BuyShipUp(s0, "hull") && s0.lv.hull == 1);
        Ok("buy exec sensors", sim.BuyExecUp("sensors") && G.upSensors == 1);

        var w = new Walker { x = 2, z = -22.5f };
        for (int i = 0; i < 1500 && w.z < 25; i++) w.Step(sim, .016f, 0, 1);
        Ok("walk bridge->corridor->hangar", w.z > 20, "z=" + w.z);
        for (int i = 0; i < 1500 && w.z > -24; i++) w.Step(sim, .016f, 0, -1);
        for (int i = 0; i < 80; i++) w.Step(sim, .016f, 1, 0);
        for (int i = 0; i < 800 && w.z > -33; i++) w.Step(sim, .016f, 0, -1);
        Ok("and back to the bridge", w.z < -32 && w.z > -37.6f, "z=" + w.z + " x=" + w.x);
        var wb = new Walker { x = 0, z = -34 }; for (int i = 0; i < 300; i++) wb.Step(sim, .016f, 0, -1);
        Ok("front window blocks walking out", wb.z > -38 + .45f - .001f, "z=" + wb.z);
        var ws = new Walker { x = 10, z = -25 }; for (int i = 0; i < 300; i++) ws.Step(sim, .016f, 1, 0);
        Ok("side wall blocks", ws.x < 14, "x=" + ws.x);
        var wp = new Walker { x = -10, z = 22 }; for (int i = 0; i < 200; i++) wp.Step(sim, .016f, 0, 1);
        Ok("player standing on a pad can walk off", wp.z > 22.5f, "z=" + wp.z);

        // ---- new rooms: every doorway walkable both ways, walls beside doors solid ----
        Walker Go(float x0, float z0, float mx, float mz, int n) { var q = new Walker { x = x0, z = z0 }; for (int i = 0; i < n; i++) q.Step(sim, .016f, mx, mz); return q; }
        var dq = Go(-1, -12, -1, 0, 220); Ok("corridor -> Crew Quarters through the west door", dq.x < -10 && dq.Where() == "Crew Quarters", "x=" + dq.x + " " + dq.Where());
        var dq2 = Go(dq.x, dq.z, 1, 0, 260); Ok("Crew Quarters -> corridor back through the door", dq2.x > -1.5f, "x=" + dq2.x);
        var de = Go(1, -12, 1, 0, 220); Ok("corridor -> Engineering through the east door", de.x > 10 && de.Where() == "Engineering", "x=" + de.x + " " + de.Where());
        var de2 = Go(de.x, de.z, -1, 0, 260); Ok("Engineering -> corridor back through the door", de2.x < 1.5f, "x=" + de2.x);
        var dc = Go(-12, 5.2f, -1, 0, 260); Ok("hangar -> Cargo Hold through the west door", dc.x < -22 && dc.Where() == "Cargo Hold", "x=" + dc.x + " " + dc.Where());
        var dc2 = Go(dc.x, dc.z, 1, 0, 300); Ok("Cargo Hold -> hangar back through the door", dc2.x > -13, "x=" + dc2.x);
        var wq = Go(-1.5f, -16.5f, -1, 0, 120); Ok("corridor wall beside the quarters door blocks", wq.x > -2.6f, "x=" + wq.x);
        var we = Go(1.5f, -8, 1, 0, 120); Ok("corridor wall beside the engineering door blocks", we.x < 2.6f, "x=" + we.x);
        var wc = Go(-12, 0, -1, 0, 120); Ok("hangar wall beside the cargo door blocks", wc.x > -15.4f, "x=" + wc.x);
        var rz = Go(7, -9, 1, 0, 120); Ok("reactor core blocks the walker", Walker.Inside(rz.x, rz.z, .45f) && rz.x < 9.0f, "x=" + rz.x);
        // full tour never leaves the walkable area
        var tour = new Walker { x = 2, z = -22.5f }; var path = new[] { (0f, 1f, 150), (-1f, 0f, 120), (1f, 0f, 250), (-1f, 0f, 120), (0f, -1f, 80) }; bool tourOk = true; foreach (var (a, b, n) in path) for (int i = 0; i < n; i++) { tour.Step(sim, .016f, a, b); if (!Walker.Inside(tour.x, tour.z, .45f)) tourOk = false; }
        Ok("walking tour stays inside the walkable area", tourOk);

        G.aeth = 321; string json = sim.Serialize(1);
        var sim2 = new Sim(); int sel = sim2.Deserialize(json);
        Ok("save round-trips progress", sel == 1 && sim2.G.ships.Count == 3 && MathF.Abs(sim2.G.aeth - 321) < .01f
            && sim2.G.isl.Count == 14 && sim2.G.ships[0].lv.eng == s0.lv.eng);

        // legacy save (no storyV) with a finished story: the finale must not replay
        var old = new Sim(); old.NewGame(5); old.G.delivered = 2000; old.G.found = 14;
        string lj = old.Serialize(0).Replace("\"storyV\":1", "\"storyV\":0");
        var leg = new Sim(); leg.Deserialize(lj); for (int i = 0; i < 100; i++) leg.Step(.5f);
        Ok("legacy save does not replay finale", !leg.BeatLog.Contains("finale") && leg.EpilogueIn < 0 && !leg.CrawlRequested);

        // ---- Executor navigation ----
        var nav = new Sim { Rng = new Random(11) }; nav.NewGame(99); foreach (var s in nav.G.ships) nav.GiveOrder(s, "hold"); nav.G.aeth = 200; float ax = nav.G.exx, ay = nav.G.exy, a0 = nav.G.aeth;
        Ok("SailTo accepts a course with stores above reserve", nav.SailTo(ax + 600, ay - 400));
        for (int i = 0; i < 4000 && nav.G.exMoving; i++) nav.Step(.1f);
        for (int i = 0; i < 80; i++) nav.Step(.1f); // coast to a stop
        float dist = Mx.Hypot(nav.G.exx - nav.G.exDestX, nav.G.exy - nav.G.exDestY);
        Ok("Executor arrives at its destination and stops", !nav.G.exMoving && dist < 40 && nav.G.exSpeed < 1, "dist=" + dist + " speed=" + nav.G.exSpeed);
        Ok("heading turned toward the course", MathF.Abs(nav.G.exHead) > .3f, "head=" + nav.G.exHead);
        Ok("sailing burned some Aetherite but not all", nav.G.aeth < a0 - 1 && nav.G.aeth > Sim.ExReserve, "aeth=" + nav.G.aeth);
        var docked0 = nav.ExPt(0, 190); Ok("docked ships ride along with the Executor", nav.G.ships.Where(s => s.state == "docked").All(s => MathF.Abs(s.x - docked0.x) < .5f && MathF.Abs(s.y - docked0.y) < .5f) && nav.G.ships.Any(s => s.state == "docked"));
        var low = new Sim { Rng = new Random(2) }; low.NewGame(5); low.G.ships.Clear(); low.G.aeth = 34; Ok("sailing refused when stores are at the reserve", !low.SailTo(1000, 1000) && !low.G.exMoving);
        low.G.aeth = 40; low.SailTo(400, 400); for (int i = 0; i < 6000 && low.G.exMoving; i++) low.Step(.1f);
        Ok("Executor halts itself above the reserve, never strands stores", !low.G.exMoving && low.G.aeth >= Sim.ExReserve - .5f, "aeth=" + low.G.aeth);
        // pilots keep working while the Executor sails: launch, mine, return, land on the moving hangar
        var mv = new Sim { Rng = new Random(21) }; mv.NewGame(321); foreach (var s in mv.G.ships) mv.GiveOrder(s, "auto"); mv.G.aeth = 300;
        for (int k = 0; k < 6000; k++) { mv.Step(.1f); if (k % 400 == 0 && !mv.G.exMoving && mv.G.aeth > 120) mv.SailTo(mv.G.exx + (k % 800 == 0 ? 500 : -500), mv.G.exy + (k % 1200 == 0 ? 300 : -300)); if (k % 1500 == 0) foreach (var s in mv.G.ships) mv.GiveOrder(s, "auto"); }
        Ok("pilots keep delivering while the Executor sails", mv.G.delivered > 60 && mv.G.ships.All(s => !float.IsNaN(s.x)), "delivered=" + mv.G.delivered);
        Ok("no pilot stranded airborne after the voyage", mv.G.ships.All(s => s.state != "out" || Mx.Hypot(s.x - mv.G.exx, s.y - mv.G.exy) < 2500));

        // ---- hangar stations: servicing only happens while standing at the station ----
        var stn = new Sim { Rng = new Random(31) }; stn.NewGame(404); stn.G.aeth = 200;
        var sp = stn.G.ships[0]; stn.GiveOrder(sp, "hold"); sp.state = "landing"; sp.lt = .995f; sp.cargo = 6; sp.fuel = 5; sp.hull = 15; float aethBefore = stn.G.aeth;
        var phases = new List<string>(); bool servicedWhileWalking = false, wrongSpot = false; float lc = sp.cargo, lf = sp.fuel, lh = sp.hull;
        for (int i = 0; i < 4000; i++)
        {
            string pb = sp.ph; stn.Step(.05f); if (phases.Count == 0 || phases[^1] != sp.ph) phases.Add(sp.ph);
            if (sp.state == "docked" && (pb == "walk" || pb == "idle") && sp.ph == pb && (MathF.Abs(sp.cargo - lc) > 1e-4f || MathF.Abs(sp.fuel - lf) > 1e-4f || MathF.Abs(sp.hull - lh) > 1e-4f)) servicedWhileWalking = true;
            if (sp.ph is "unload" or "fuel" or "repair") { var spot = Data.StSpot[sp.ph == "unload" ? "deposit" : sp.ph]; if (Mx.Hypot(sp.px - spot[0], sp.pz - spot[1]) > .1f) wrongSpot = true; }
            lc = sp.cargo; lf = sp.fuel; lh = sp.hull; if (sp.ph == "idle") break;
        }
        Ok("pilot visits deposit, fuel and repair stations in turn, then the bridge", string.Join(">", phases.Where(p => p != "walk").Distinct()) == "unload>fuel>repair>idle" || phases.Contains("unload") && phases.Contains("fuel") && phases.Contains("repair") && phases.Contains("idle"), string.Join(">", phases));
        Ok("no servicing while walking", !servicedWhileWalking);
        Ok("pilot stands at the station while working", !wrongSpot);
        var stCfg = Sim.StatOf(sp); Ok("pilot ends up unloaded, fuelled and repaired", sp.cargo == 0 && sp.fuel >= stCfg.fuel - .01f && sp.hull >= stCfg.hull - .01f, $"cargo={sp.cargo} fuel={sp.fuel} hull={sp.hull}");
        Ok("unloaded cargo reached the stores", stn.G.aeth > aethBefore - 20 && stn.G.delivered >= 5.9f, $"aeth {aethBefore}->{stn.G.aeth} delivered {stn.G.delivered}");
        // legacy save with the old phase names must still load and launch
        var leg2 = new Sim { Rng = new Random(8) }; leg2.NewGame(77); leg2.G.aeth = 200; string lsj = leg2.Serialize(0).Replace("\"ph\":\"aboard\"", "\"ph\":\"exit\"");
        var leg3 = new Sim { Rng = new Random(9) }; leg3.Deserialize(lsj); foreach (var s in leg3.G.ships) leg3.GiveOrder(s, "auto"); for (int i = 0; i < 3000; i++) leg3.Step(.1f);
        Ok("legacy-phase save loads and the crew launches", leg3.G.launches >= 1 && leg3.G.ships.All(s => s.ph is "walk" or "unload" or "fuel" or "repair" or "idle" or "aboard" || s.state != "docked"));

        // ---- captain's ship: launch, fly, autopilot, land, hand-mine, return, dock, deposit ----
        var cs = new Sim { Rng = new Random(61) }; cs.NewGame(2024); foreach (var s in cs.G.ships) cs.GiveOrder(s, "hold"); cs.G.aeth = 300; var cc = cs.G.cap;
        Ok("captain's ship starts docked with a full tank", cc.state == "docked" && cc.fuel >= Sim.CapFuelMax - .01f);
        Ok("launch starts the takeoff", cs.CapLaunch() && cc.state == "launch");
        for (int i = 0; i < 70 && cc.state == "launch"; i++) cs.Step(.1f);
        Ok("takeoff ends in free flight at the hangar mouth", cc.state == "fly" && Mx.Hypot(cc.x - cs.ExPt(0, 330).x, cc.y - cs.ExPt(0, 330).y) < 3, cc.state);
        float fx0 = cc.x, fy0 = cc.y, fuel0 = cc.fuel; cs.Piloted = true; cs.InThr = 1;
        for (int i = 0; i < 60; i++) cs.Step(.1f); cs.InThr = 0; cs.InTurn = 1; for (int i = 0; i < 20; i++) cs.Step(.1f); cs.InTurn = 0;
        float flown = Mx.Hypot(cc.x - fx0, cc.y - fy0); cs.Piloted = false;
        Ok("piloted flight moves the ship and burns fuel", flown > 150 && cc.fuel < fuel0 - 1, $"flown={flown:F0} fuel={cc.fuel:F1}");
        Ok("fuel burn is about 0.0085 per unit flown", MathF.Abs((fuel0 - cc.fuel) - flown * .0085f) < flown * .0085f * .6f + .5f, $"burn={fuel0 - cc.fuel:F2} expected~{flown * .0085f:F2}");
        for (int i = 0; i < 100; i++) cs.Step(.1f); Ok("hands-off the ship slows to a hover", cc.speed < 5, "speed=" + cc.speed);
        var tgtI = cs.G.isl[0]; tgtI.found = true; cs.CapAutoGoto(tgtI);
        for (int i = 0; i < 1500 && cc.state != "landed"; i++) cs.Step(.1f);
        Ok("autopilot flies to the locked island and lands", cc.state == "landed" && cc.island == tgtI.id && cc.fuel > 0, $"state={cc.state} fuel={cc.fuel:F1}");
        var (lsx, lsz) = cs.LandSpot(tgtI); Ok("landed ship sits on the island landing spot", Mx.Hypot(cc.x - (tgtI.x + lsx), cc.y - (tgtI.y + lsz)) < .5f);
        // hand mining
        var nodes = Sim.OreNodes(tgtI); cc.ex = nodes[0].x + 1.2f; cc.ez = nodes[0].z; tgtI.aeth = tgtI.max; float aeth0 = tgtI.aeth, ore0 = cc.ore;
        for (int i = 0; i < 14; i++) cs.MineTick(.1f, true); Ok("holding E for under 1.5 s mines nothing yet", cc.ore == ore0);
        for (int i = 0; i < 4; i++) cs.MineTick(.1f, true); Ok("one chunk mined after 1.5 s: ore +1, island -5", cc.ore == ore0 + 1 && MathF.Abs(tgtI.aeth - (aeth0 - 5)) < .01f, $"ore={cc.ore} isl={tgtI.aeth}");
        cc.ex += 40; for (int i = 0; i < 40; i++) cs.MineTick(.1f, true); Ok("nothing to mine away from the nodes", cc.ore == ore0 + 1);
        cc.ex = nodes[0].x + 1.2f; for (int i = 0; i < 1000; i++) cs.MineTick(.1f, true); Ok("pack caps at 12 chunks", cc.ore == Sim.CapCarry, "ore=" + cc.ore);
        tgtI.aeth = 3; cc.ore = 0; for (int i = 0; i < 40; i++) cs.MineTick(.1f, true); Ok("depleted island yields no ore", cc.ore == 0);
        tgtI.aeth = tgtI.max; cc.ore = 12;
        int nN = Sim.OreNodes(tgtI).Count; Ok("visible ore nodes shrink with the island's Aetherite", cs.VisibleNodes(tgtI) == nN && (tgtI.aeth = tgtI.max * .26f) > 0 && cs.VisibleNodes(tgtI) == (int)MathF.Ceiling(.26f * nN - .001f), "nodes=" + nN);
        tgtI.aeth = tgtI.max;
        // back home: lift off, autopilot home, dock, deposit
        Ok("takeoff from the island", cs.CapTakeoff() && cc.state == "lift"); for (int i = 0; i < 40 && cc.state == "lift"; i++) cs.Step(.1f); Ok("lift-off ends in flight", cc.state == "fly");
        if (cc.fuel < 25) cc.fuel = 25; cs.SailTo(cs.G.exx + 300, cs.G.exy - 300); cs.CapAutoHome();
        for (int i = 0; i < 4000 && cc.state != "docked"; i++) cs.Step(.1f);
        Ok("autopilot home finds the (moving) Executor and docks, ore intact", cc.state == "docked" && cc.ore == 12, $"state={cc.state} ore={cc.ore} d={Mx.Hypot(cc.x - cs.G.exx, cc.y - cs.G.exy):F0}");
        float aeBefore = cs.G.aeth, delBefore = cs.G.delivered; int steps2 = 0; while (cc.ore >= 1 && steps2++ < 200) { cs.CapDepositTick(.1f); cs.Step(.1f); }
        Ok("depositing 12 chunks adds 60 Aetherite and counts as delivered", cc.ore == 0 && cs.G.aeth - aeBefore > 55 && cs.G.delivered - delBefore > 59.9f, $"aeth +{cs.G.aeth - aeBefore:F1} delivered +{cs.G.delivered - delBefore:F1}");
        cc.fuel = 5; float fa = cs.G.aeth; for (int i = 0; i < 300; i++) cs.CapRefuelTick(.1f);
        Ok("refuelling at the station fills the tank and costs Aetherite", cc.fuel >= Sim.CapFuelMax - .01f && cs.G.aeth < fa - 5, $"fuel={cc.fuel} aeth {fa}->{cs.G.aeth}");
        // out of fuel: crawl, never stuck
        cs.CapLaunch(); for (int i = 0; i < 70 && cc.state == "launch"; i++) cs.Step(.1f); cc.fuel = 0; cs.Piloted = true; cs.InThr = 1; for (int i = 0; i < 80; i++) cs.Step(.1f); cs.Piloted = false;
        Ok("with an empty tank the ship still crawls home (speed capped)", cc.speed <= 36 && cc.speed > 5, "speed=" + cc.speed);
        // save mid-flight, load, mid-landing normalisation
        cc.state = "fly"; cc.x = 1300; cc.y = 900; cs.Step(.1f); string fj = cs.Serialize(0); var fl = new Sim(); fl.Deserialize(fj);
        Ok("saving mid-flight restores the flight", fl.G.cap.state == "fly" && MathF.Abs(fl.G.cap.x - cc.x) < 1 && MathF.Abs(fl.G.cap.fuel - cc.fuel) < .01f);
        cc.state = "land"; cc.island = 2; cc.lt = .4f; string lj2 = cs.Serialize(0); var fl2 = new Sim(); fl2.Deserialize(lj2); for (int i = 0; i < 20; i++) fl2.Step(.1f);
        Ok("saving mid-landing resumes as landed", fl2.G.cap.state == "landed" && fl2.G.cap.island == 2);
        cc.state = "dock"; string dj = cs.Serialize(0); var fl3 = new Sim(); fl3.Deserialize(dj); Ok("saving mid-dock resumes docked", fl3.G.cap.state == "docked");
        // a long random-autopilot soak: no NaN, never stuck mid-state
        var csk = new Sim { Rng = new Random(77) }; csk.NewGame(555); foreach (var s in csk.G.ships) csk.GiveOrder(s, "auto"); csk.G.aeth = 400; var ck = csk.G.cap; bool capBad = false; float stateT = 0; string lastSt = ck.state; float maxStateT = 0; var rr = new Random(5); int trips = 0;
        for (int k = 0; k < 40000; k++)
        {
            csk.Step(.1f); if (ck.state != lastSt) { lastSt = ck.state; stateT = 0; } else stateT += .1f;
            if (float.IsNaN(ck.x) || float.IsNaN(ck.y) || float.IsNaN(ck.alt) || ck.fuel < 0 || ck.ore < 0 || ck.ore > 12.01f) capBad = true;
            if (ck.state is "launch" or "dock" or "land" or "lift") maxStateT = MathF.Max(maxStateT, stateT);
            if (k % 40 == 0) foreach (var s in csk.G.ships) if (s.order == null && !s.indep) csk.GiveOrder(s, "auto");
            if (ck.state == "docked" && k % 20 == 0) { ck.fuel = Sim.CapFuelMax; csk.CapLaunch(); }
            else if (ck.state == "fly" && ck.auto == "" && k % 10 == 0) { var ti = csk.G.isl[rr.Next(csk.G.isl.Count)]; ti.found = true; csk.CapAutoGoto(ti); }
            else if (ck.state == "landed" && k % 30 == 0) { ck.ore = 12; csk.CapTakeoff(); trips++; }
            else if (ck.state == "fly" && ck.auto == "goto" && k % 400 == 0) csk.CapAutoHome();
            if (k % 500 == 0 && !csk.G.exMoving && csk.G.aeth > 200) csk.SailTo(csk.G.exx + (rr.Next(2) * 2 - 1) * 400, csk.G.exy + (rr.Next(2) * 2 - 1) * 400);
        }
        Ok("captain soak: no NaN, fuel and ore always in range", !capBad);
        Ok("captain soak: animations always finish (< 10 s each)", maxStateT < 10, "max=" + maxStateT);
        Ok("captain soak: made several island trips", trips >= 3, "trips=" + trips);

        // ---- island terrain: invariants for every size class ----
        foreach (var (R, tseed) in new[] { (40f, 11), (55f, 12), (90f, 13), (130f, 14), (200f, 15), (330f, 16), (320f, 17), (230f, 18), (100f, 19) })
        {
            var sw0 = System.Diagnostics.Stopwatch.StartNew(); var sh = Terrain.Get(tseed, R); long ms = sw0.ElapsedMilliseconds; string nm = $"R={R:F0} class {sh.ClassIdx}";
            Ok($"terrain {nm}: rivers {(sh.ClassIdx == 0 ? "absent on small islands" : "exist")} and reach the rim", (sh.ClassIdx == 0 ? sh.Rivers.Count == 0 : sh.Rivers.Count >= 1) && sh.Rivers.All(r => r.P[^1].Length() >= .97f * R), $"rivers={sh.Rivers.Count}");
            Ok($"terrain {nm}: water never runs uphill", sh.Rivers.All(r => { for (int k = 1; k < r.Y.Count; k++) if (r.Y[k] > r.Y[k - 1] + 1e-3f) return false; return true; }));
            float pH = sh.Height(sh.Pad.X, sh.Pad.Y); bool padOk = true; for (int a = 0; a < 12; a++) { float px = sh.Pad.X + MathF.Cos(a * .5236f) * 4.5f, pz = sh.Pad.Y + MathF.Sin(a * .5236f) * 4.5f; if (sh.Wet(px, pz) || MathF.Abs(sh.Height(px, pz) - pH) > .6f) padOk = false; }
            Ok($"terrain {nm}: landing pad is dry and flat", padOk);
            Ok($"terrain {nm}: every ore node is dry and gently sloped", sh.Nodes.Count >= 6 && sh.Nodes.All(n => !sh.Wet(n.X, n.Y) && sh.Slope(n.X, n.Y) < .4f), $"nodes={sh.Nodes.Count}");
            Ok($"terrain {nm}: trails exist (medium+) and never run through water", (sh.ClassIdx == 0 || sh.Trails.Count >= 1) && sh.Trails.All(t => t.All(p => !sh.Wet(p.X, p.Y))), $"trails={sh.Trails.Count}");
            Ok($"terrain {nm}: trees stand on dry land", sh.Trees.Count > 10 && sh.Trees.All(t => !sh.Wet(t.x, t.z)), $"trees={sh.Trees.Count}");
            Ok($"terrain {nm}: finite heights below the flight ceiling", sh.MaxHeight > 0 && sh.MaxHeight < 330 && !float.IsNaN(sh.Height(R * .5f, 0)), $"max={sh.MaxHeight:F0}");
            if (sh.ClassIdx == 3) Console.WriteLine($"   huge island built in {ms} ms ({sh.Trees.Count} trees, {sh.Trails.Count} trails, {sh.Rivers.Count} rivers)");
        }
        var mt = Terrain.Get(15, 200); Ok("large island has real mountains with a summit trail", mt.Peaks.Count >= 2 && mt.Summit > .2f * 200 && mt.Trails.Count >= 2, $"peaks={mt.Peaks.Count} summit={mt.Summit:F0} trails={mt.Trails.Count}");
        // ---- island placement across many worlds ----
        bool placeOk = true, boundsOk = true, clearOk = true, countOk = true, reachOk = true, sizesOk = true; string why = "";
        for (int wsd = 1; wsd <= 40; wsd++)
        {
            var pw = new Sim { Rng = new Random(wsd) }; pw.NewGame(wsd * 97 + 3); var L = pw.G.isl;
            if (L.Count != 14) countOk = false;
            for (int a = 0; a < L.Count; a++)
            {
                var o = L[a]; if (o.x < o.s + 150 - .5f || o.x > Data.W - o.s - 150 + .5f || o.y < o.s + 150 - .5f || o.y > Data.W - o.s - 150 + .5f) { boundsOk = false; why = $"bounds ws{wsd} {o.s:F0}"; }
                if (Mx.Hypot(o.x - Data.CX, o.y - Data.CY) < o.s + 470 - 1) { clearOk = false; why = $"exec zone ws{wsd}"; }
                for (int b = a + 1; b < L.Count; b++) if (Mx.Hypot(o.x - L[b].x, o.y - L[b].y) < o.s + L[b].s + 250 - .5f) { placeOk = false; why = $"overlap ws{wsd} {a}/{b}"; }
            }
            if (L.Count(o => Mx.Hypot(o.x - Data.CX, o.y - Data.CY) <= 1000) < 5) { reachOk = false; why = $"reach ws{wsd}"; }
            if (!(L.Max(o => o.s) >= 200 && L.Count(o => o.s < 65) >= 3 && L.Count(o => o.s >= 65 && o.s < 135) >= 3)) { sizesOk = false; why = $"sizes ws{wsd} max={L.Max(o => o.s):F0}"; }
        }
        Ok("40 worlds: always 14 islands", countOk); Ok("40 worlds: no two islands overlap (radius sum + 250 gap)", placeOk, why); Ok("40 worlds: every island fully inside the world", boundsOk, why);
        Ok("40 worlds: the Executor's start is clear", clearOk, why); Ok("40 worlds: at least 5 islands within pilot range of the start", reachOk, why); Ok("40 worlds: a mix of small, medium and at least one very large island", sizesOk, why);

        // ---- tree monsters: 50/50 haunting per landing, chase, fight, death, takeoff ----
        var tmS = new Sim { Rng = new Random(123) }; tmS.NewGame(88); var isl9 = tmS.G.isl[0]; isl9.found = true; int haunted = 0; for (int k = 0; k < 400; k++) if (tmS.RollHaunting(isl9)) haunted++;
        Ok("each landing is haunted about half the time (400 landings)", haunted > 160 && haunted < 240, $"{haunted}/400");
        var mc = tmS.G.cap; mc.state = "landed"; mc.island = 0; var mshape = Terrain.Get(isl9); var (lpx, lpz) = tmS.LandSpot(isl9); mc.ex = lpx - 4.5f; mc.ez = lpz + 1.5f; mc.hp = 100; mc.ore = 6; bool got = false; for (int k = 0; k < 40 && !got; k++) got = tmS.RollHaunting(isl9);
        Ok("a haunted landing turns some nearby trees into monsters", got && tmS.Monsters.Count >= 1 && tmS.Monsters.Count <= 26, "count=" + tmS.Monsters.Count);
        Ok("monsters start dormant, away from the ship, on dry ground", tmS.Monsters.All(m => !m.awake && Mx.Hypot(m.x - mshape.Pad.X, m.z - mshape.Pad.Y) > 22 && !mshape.Wet(m.x, m.z)));
        var m0 = tmS.Monsters.OrderBy(m => Mx.Hypot(m.x - mc.ex, m.z - mc.ez)).First(); mc.ex = m0.x - 14; mc.ez = m0.z; float dBefore = Mx.Hypot(mc.ex - m0.x, mc.ez - m0.z); for (int k = 0; k < 30; k++) tmS.Step(.1f);
        Ok("walking within 18 units wakes a monster and it closes in", m0.awake && Mx.Hypot(mc.ex - m0.x, mc.ez - m0.z) < dBefore - 4, $"d {dBefore:F1} -> {Mx.Hypot(mc.ex - m0.x, mc.ez - m0.z):F1}");
        mc.hp = 100; foreach (var mm in tmS.Monsters.ToList()) if (mm != m0) tmS.Monsters.Remove(mm); float hpA = mc.hp; for (int k = 0; k < 50; k++) tmS.Step(.1f);
        Ok("a monster in reach drains health (about 12 per second)", hpA - mc.hp > 25 && hpA - mc.hp < 75, $"hp {hpA} -> {mc.hp:F0}");
        // laser: three 25-damage hits kill a 60-hp monster; it leaves ore
        mc.hp = 100; m0.hp = 60; float ore1 = mc.ore; var iz = tmS.G.isl[0]; float eyeY = mshape.Height(mc.ex, mc.ez) + 1.7f, tx = m0.x - mc.ex, ty = mshape.Height(m0.x, m0.z) + 3.3f - eyeY, tz = m0.z - mc.ez, tl = MathF.Sqrt(tx * tx + ty * ty + tz * tz);
        int shots = 0; while (!m0.dead && shots < 6) { tmS.HitMonsters(mc.ex, eyeY, mc.ez, tx / tl, ty / tl, tz / tl, 70, 25, out _); shots++; }
        Ok("three laser hits kill a monster and it drops a chunk of ore", m0.dead && shots == 3 && mc.ore == ore1 + 1, $"shots={shots} ore {ore1}->{mc.ore}");
        var miss = tmS.HitMonsters(mc.ex, eyeY, mc.ez, 0, 1, 0, 70, 25, out _); Ok("a shot at the sky hits nothing", miss == null);
        // pick axe: three 22-damage swings
        tmS.Monsters.Clear(); var m1 = new Sim.Monster { tree = 0, x = mc.ex + 2.2f, z = mc.ez, awake = true }; tmS.Monsters.Add(m1); float yawTo = MathF.Atan2(-(m1.x - mc.ex), -(m1.z - mc.ez)); int swings = 0; while (!m1.dead && swings < 6) { tmS.MeleeMonsters(yawTo, 3.6f, 22); swings++; }
        Ok("three pick-axe swings kill a monster in front of you", m1.dead && swings == 3, "swings=" + swings); var behind = new Sim.Monster { x = mc.ex - 2.2f, z = mc.ez, awake = true }; tmS.Monsters.Add(behind); Ok("the pick axe cannot hit what is behind you", tmS.MeleeMonsters(yawTo, 3.6f, 22) == null);
        // overwhelmed: wake beside the ship with half the ore, trees calm down
        tmS.Monsters.Clear(); tmS.Monsters.Add(new Sim.Monster { x = mc.ex + 1, z = mc.ez, awake = true }); mc.hp = 3; mc.ore = 9; for (int k = 0; k < 30; k++) tmS.Step(.1f);
        Ok("being overwhelmed respawns you at the ship with half the ore", mc.hp >= 59 && mc.ore == 4 && tmS.Monsters.Count == 0 && Mx.Hypot(mc.ex - (lpx - 4.5f), mc.ez - (lpz + 1.5f)) < 3, $"hp={mc.hp} ore={mc.ore}");
        tmS.Monsters.Add(new Sim.Monster { x = mc.ex + 30, z = mc.ez }); Ok("boarding the ship is always possible, and clears the monsters", tmS.CapTakeoff() && tmS.Monsters.Count == 0);
        for (int k = 0; k < 40; k++) tmS.Step(.1f); tmS.G.cap.state = "landed"; tmS.Monsters.Add(new Sim.Monster { x = 1, z = 1 }); tmS.G.cap.hp = 77; string mj = tmS.Serialize(0); var ml = new Sim(); ml.Deserialize(mj);
        Ok("saving never stores monsters, but keeps health", ml.Monsters.Count == 0 && MathF.Abs(ml.G.cap.hp - 77) < .01f);
        // a long fight never produces NaN or runaway values
        var fs = new Sim { Rng = new Random(9) }; fs.NewGame(31); var fi2 = fs.G.isl[1]; fi2.found = true; var fc = fs.G.cap; fc.state = "landed"; fc.island = 1; var fls = fs.LandSpot(fi2); fc.ex = fls.x - 4.5f; fc.ez = fls.z + 1.5f; bool fightOk = true; int respawns = 0;
        for (int round = 0; round < 40; round++) { fs.RollHaunting(fi2); foreach (var mo in fs.Monsters) { mo.awake = true; } for (int k = 0; k < 150; k++) { float prevHp = fc.hp; fs.Step(.1f); if (fc.hp > prevHp + 20) respawns++; var tgt = fs.Monsters.FirstOrDefault(q => !q.dead); if (tgt != null && k % 7 == 0) fs.MeleeMonsters(MathF.Atan2(-(tgt.x - fc.ex), -(tgt.z - fc.ez)), 3.6f, 22); if (float.IsNaN(fc.hp) || float.IsNaN(fc.ex) || fs.Monsters.Any(q => float.IsNaN(q.x))) fightOk = false; } }
        Ok("40 rounds of fighting: no NaN, health stays in range", fightOk && fc.hp > 0 && fc.hp <= 100.01f);

        // ---- pilots clear the terrain below them, not the island's highest peak ----
        var cfS = new Sim { Rng = new Random(4) }; cfS.NewGame(2025); var big = cfS.G.isl.Where(o => Terrain.Get(o).Peaks.Count > 0).OrderByDescending(o => o.s).First(); var bsh = Terrain.Get(big);
        float overPeak = cfS.CruiseFloor(big.x + bsh.SummitPos.X, big.y + bsh.SummitPos.Y, 0), overPad = cfS.CruiseFloor(big.x + bsh.Pad.X, big.y + bsh.Pad.Y, 0), nowhere = cfS.CruiseFloor(Data.CX, Data.CY, 0);
        Ok("cruise floor rises over the summit but stays low over the pad", overPeak > overPad + 8 && overPad < cfS.TopY(big) - 8, $"peak={overPeak:F0} pad={overPad:F0} top={cfS.TopY(big):F0}");
        Ok("cruise floor is zero in open space", nowhere == 0);
        float aheadFloor = cfS.CruiseFloor(big.x + bsh.SummitPos.X - 150, big.y + bsh.SummitPos.Y, 0); Ok("cruise floor looks ahead so ships climb before a ridge", aheadFloor > overPad || bsh.SummitPos.X - 150 < -big.s);
        // soak at dt=0.6 for 90 game minutes
        var soak = new Sim { Rng = new Random(99) }; soak.NewGame(777);
        foreach (var s in soak.G.ships) soak.GiveOrder(s, "auto");
        var since = new Dictionary<int, (string key, float t)>(); float maxStuck = 0, maxDockStuck = 0; bool bad = false;
        int steps = (int)(5400 / .6f);
        for (int k = 0; k < steps; k++)
        {
            soak.Step(.6f);
            if (k % 100 == 0) foreach (var u in Data.ExecUp) if (soak.G.aeth > 250) soak.BuyExecUp(u.key);
            foreach (var s in soak.G.ships)
            {
                if (float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.fuel) || float.IsNaN(s.hull) || s.fuel < 0 || s.cargo < 0 || s.hull < 0) bad = true;
                string key = s.state + "/" + s.ph;
                if (!since.TryGetValue(s.idx, out var cur) || cur.key != key) since[s.idx] = (key, soak.G.t);
                else if (!s.indep && s.state != "docked") maxStuck = MathF.Max(maxStuck, soak.G.t - cur.t);
                else if (s.state == "docked" && s.ph is "walk" or "unload" or "fuel" or "repair" && soak.G.aeth > 5) maxDockStuck = MathF.Max(maxDockStuck, soak.G.t - cur.t);
            }
            if (k % 300 == 0) foreach (var s in soak.G.ships) soak.GiveOrder(s, "auto"); // player keeps crew loyal (every 3 game-min)
        }
        Ok("soak dt=0.6: no NaN/negatives", !bad);
        Ok("soak dt=0.6: no airborne ship stuck > 10 game-min", maxStuck < 600, "maxStuck=" + maxStuck);
        Ok("soak: no docked pilot stuck walking/servicing > 4 game-min", maxDockStuck < 240, "maxDockStuck=" + maxDockStuck);
        Ok("soak: crew joined and story beats fired", soak.G.ships.Count == 5 && soak.BeatLog.Count >= 8,
            soak.G.ships.Count + " ships, beats=" + string.Join(",", soak.BeatLog));
        Console.WriteLine($"   soak: delivered={soak.G.delivered:F0} found={soak.G.found} beats={soak.BeatLog.Count} maxStuck={maxStuck:F0}s");

        var soak2 = new Sim { Rng = new Random(5) }; soak2.NewGame(42);
        bool bad2 = false;
        for (int k = 0; k < 10800; k++) { soak2.Step(1f / 6); foreach (var s in soak2.G.ships) if (float.IsNaN(s.x)) bad2 = true; }
        Ok("soak dt=1/6 clean", !bad2);
        Console.WriteLine($"\n{pass}/{pass + fail} passed"); return fail == 0 ? 0 : 1;
    }
}
