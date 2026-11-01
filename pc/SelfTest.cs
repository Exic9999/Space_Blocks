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

        // soak at dt=0.6 for 90 game minutes
        var soak = new Sim { Rng = new Random(99) }; soak.NewGame(777);
        foreach (var s in soak.G.ships) soak.GiveOrder(s, "auto");
        var since = new Dictionary<int, (string key, float t)>(); float maxStuck = 0; bool bad = false;
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
            }
            if (k % 300 == 0) foreach (var s in soak.G.ships) soak.GiveOrder(s, "auto"); // player keeps crew loyal (every 3 game-min)
        }
        Ok("soak dt=0.6: no NaN/negatives", !bad);
        Ok("soak dt=0.6: no airborne ship stuck > 10 game-min", maxStuck < 600, "maxStuck=" + maxStuck);
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
