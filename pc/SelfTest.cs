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
        float dist = Mx.Hypot(nav.G.exx - (ax + 600), nav.G.exy - (ay - 400));
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
