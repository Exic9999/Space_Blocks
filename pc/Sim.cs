// Pure simulation (no rendering). Port of the game logic in mobile3d/index.html.
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SpaceBlocks;

public sealed class Island
{
    public int id; public string name = "";
    public float x, y, h, s, aeth, max; public bool found; public int seed;
}
public sealed class Order { public string type = "auto"; public int isl = -1; }
public sealed class Pt { public float x, y; public Pt() { } public Pt(float x, float y) { this.x = x; this.y = y; } }
public sealed class Levels { public int eng, las, tank, car, hull; }
public sealed class Storm { public float x, y, r, vx, vy, age, life; }

public sealed class Ship
{
    public int idx; public string name = ""; public uint color;
    public float x, y, alt = 20, yaw;
    public string state = "docked", ph = "aboard";
    public float lt, fuel = 20, hull = 30, cargo;
    public Levels lv = new();
    public float loy = 100; public bool indep;
    public Order? order = new();
    public int target = -1;
    public List<Pt> wp = new();
    public int trips; public float docked;
    public float px, pz, pa;
    public List<float[]> path = new();
    public float idle, ang = -MathF.PI / 2;
    // transient-ish (saved too; harmless)
    public string nextState = "out"; public List<Pt> pendWp = new();
    public bool scout; public int lastScout = -1; public bool inStorm, warned, walking;
}

public sealed class GameState
{
    public float t, aeth = 80, delivered;
    public int upSensors, upQuarters;
    public List<Ship> ships = new();
    public List<Island> isl = new();
    public HashSet<string> beats = new();
    public int found = 3, storyV = 1;
    public List<Storm> storms = new();
    public float nextStorm = 50; public int stormsSeen, launches, indepEver;
    // Executor navigation: position, heading (nose = local -z = world (-sin h, -cos h)), speed, course
    public float exx = Data.CX, exy = Data.CY, exHead, exSpeed, exDestX, exDestY; public bool exMoving;
}

public sealed class LogLine { public string msg = ""; public uint color; public float t = 6; }
public sealed class Talk { public string name = "", call = "", text = ""; public uint color; public float t; }
public struct Stat { public float speed, mine, fuel, cargo, hull; }
public sealed class Plan { public Island? isl; public bool scout; }

public sealed class Sim
{
    public GameState G = new();
    public List<LogLine> Logs = new();
    public Talk? Talk; public int Speed = 1;
    public float EpilogueIn = -1; public bool CrawlRequested; // set when epilogue crawl should start
    public Random Rng = new();
    int mseed = 1; float beatCool;
    const uint C_DEF = 0xc8e1f0, C_CYAN = 0x5adcff, C_GOOD = 0x6eeba0, C_WARN = 0xffbe5a, C_BAD = 0xff6464, C_PURP = 0xa08cff;

    float Rnd() { mseed = (int)((long)mseed * 16807 % 2147483647); return (mseed - 1) / 2147483646f; }
    float Rf() => (float)Rng.NextDouble();

    public static Stat StatOf(Ship s) => new()
    {
        speed = 100 + 28 * s.lv.eng, mine = 1.2f + .9f * s.lv.las, fuel = 20 + 10 * s.lv.tank,
        cargo = 8 + 6 * s.lv.car, hull = 30 + 12 * s.lv.hull
    };

    public void Say(string m, uint c = C_DEF) { Logs.Add(new LogLine { msg = m, color = c }); if (Logs.Count > 5) Logs.RemoveAt(0); }

    public void SayTalk(string name, string call, uint color, string text, float secs)
    { Talk = new Talk { name = name, call = call, color = color, text = text, t = secs }; }
    public void ShipTalk(Ship s, string txt, float secs = 4.5f) => SayTalk(s.name, Data.Crew[s.idx].Call, s.color, txt, secs);
    public void Chat(Ship s) { var c = Data.Crew[s.idx]; ShipTalk(s, c.Lines[Rng.Next(c.Lines.Length)]); }

    // ---------------- new game / ships ----------------
    public void NewGame(int? seed = null)
    {
        mseed = seed ?? (Rng.Next(1, 1000000));
        var isl = new List<Island>();
        for (int i = 0; i < 14; i++)
        {
            float x = 0, y = 0; bool ok; int t = 0;
            do
            {
                float a = Rnd() * 6.283f, r = 450 + Rnd() * 1150;
                x = Data.CX + MathF.Cos(a) * r; y = Data.CY + MathF.Sin(a) * r;
                ok = isl.All(o => Mx.Hypot(o.x - x, o.y - y) > 300); t++;
            } while (!ok && t < 60);
            float s = 70 + Rnd() * 55;
            var o2 = new Island { id = i, name = Data.IslandNames[i], x = x, y = y, h = -90 + Rnd() * 200, s = s, aeth = 120 + Rnd() * 260, seed = (int)MathF.Floor(Rnd() * 1e6f) };
            o2.max = o2.aeth; isl.Add(o2);
        }
        isl = isl.OrderBy(o => Mx.Hypot(o.x - Data.CX, o.y - Data.CY)).ToList();
        for (int i = 0; i < isl.Count; i++) isl[i].id = i;
        for (int i = 0; i < 3; i++) isl[i].found = true;
        G = new GameState { isl = isl };
        beatCool = 0; Logs.Clear(); Talk = null; EpilogueIn = -1;
        for (int i = 0; i < 3; i++) AddShip(i);
        Say("Commander Jason, welcome aboard the Executor.");
    }

    public void AddShip(int i)
    {
        var c = Data.Crew[i]; var p = Data.Pads[i];
        G.ships.Add(new Ship { idx = i, name = c.Name, color = c.Color, x = ExPt(p[0] * .1f, 190).x, y = ExPt(p[0] * .1f, 190).y, px = p[0], pz = p[1] - 4 });
    }

    // Executor-local (hull) coordinates -> world, honouring heading. lx = starboard, lz = stern (+z is the hangar mouth).
    public Pt ExPt(float lx, float lz) { float c = MathF.Cos(G.exHead), s = MathF.Sin(G.exHead); return new Pt(G.exx + lx * c + lz * s, G.exy - lx * s + lz * c); }
    // World offset -> Executor-local x,z.
    public (float x, float z) ToLocal(float dx, float dz) { float c = MathF.Cos(G.exHead), s = MathF.Sin(G.exHead); return (dx * c - dz * s, dx * s + dz * c); }

    // ---------------- planning ----------------
    Island? Best(Ship s, Stat st)
    {
        Island? b = null; float bs = 0;
        foreach (var i in G.isl)
        {
            if (!i.found || i.aeth < 20) continue;
            float d = Mx.Hypot(i.x - G.exx, i.y - G.exy);
            if (d * 2 * .0085f > st.fuel * .85f) continue;
            if (G.storms.Any(q => Mx.Hypot(q.x - i.x, q.y - i.y) < q.r + 150)) continue;
            int crowd = G.ships.Count(o => o != s && o.target == i.id);
            float sc = i.aeth / (d + 400) * (1 - .3f * MathF.Min(crowd, 3)) + Data.Crew[s.idx].Bold * d * .00004f;
            if (sc > bs) { b = i; bs = sc; }
        }
        return b;
    }
    static bool CanGo(Ship s) => s.indep || (s.order != null && s.order.type != "hold");
    Plan? PlanFor(Ship s)
    {
        var st = StatOf(s); var o = s.order;
        if (!s.indep && (o == null || o.type == "hold")) return null;
        if (!s.indep && o!.type == "scout") return new Plan { scout = true };
        if (!s.indep && o!.type == "mine")
        {
            var i = G.isl[o.isl];
            if (i.found && i.aeth >= 15 && Mx.Hypot(i.x - G.exx, i.y - G.exy) * 2 * .0085f <= st.fuel * .9f) return new Plan { isl = i };
            Say(i.name + " is spent for " + s.name + ". Switching to auto.", C_WARN); s.order = new Order { type = "auto" };
        }
        if (s.trips > 0 && s.trips % 3 == 0 && s.lastScout != s.trips && G.isl.Any(i => !i.found)) { s.lastScout = s.trips; return new Plan { scout = true }; }
        var b = Best(s, st);
        return b != null ? new Plan { isl = b } : new Plan { scout = true };
    }

    void Launch(Ship s, Plan p)
    {
        List<Pt> wp;
        if (p.isl != null)
        {
            s.target = p.isl.id; wp = new() { ExPt(0, 400), new(p.isl.x, p.isl.y) }; s.scout = false;
            Say(s.name + " locked onto " + p.isl.name + " and launched.", C_CYAN);
        }
        else
        {
            s.target = -1; float a = Rf() * 6.283f, r = 900 + Rf() * 1100;
            wp = new() { ExPt(0, 400), new(Math.Clamp(G.exx + MathF.Cos(a) * r, 150, Data.W - 150), Math.Clamp(G.exy + MathF.Sin(a) * r, 150, Data.W - 150)) };
            s.scout = true; Say(s.name + " launched on a scouting run.");
        }
        s.nextState = "out"; G.launches++; s.pendWp = wp; s.state = "takeoff"; s.lt = 0; s.ph = "aboard"; Chat(s);
    }
    void GoHome(Ship s) { s.state = "in"; s.target = -1; s.scout = false; s.wp = new() { ExPt(0, 400), ExPt(0, 330) }; }

    public bool GiveOrder(Ship s, string type, int isl = -1)
    {
        if (s.indep && Rf() < (Data.Crew[s.idx].Bold > .8f ? .7f : .4f))
        { s.loy = MathF.Min(100, s.loy + 12); ShipTalk(s, "No. I have my own plans right now."); Say(s.name + " refused the order.", C_BAD); return false; }
        if (s.indep) { s.indep = false; Say(s.name + " is back under your command.", C_GOOD); }
        s.loy = 100; s.order = new Order { type = type, isl = isl };
        if (type == "mine")
        {
            s.target = isl; Say(s.name + " locked onto " + G.isl[isl].name + ".", C_CYAN);
            if (s.state == "out" || s.state == "mining") { s.state = "out"; s.wp = new() { new(G.isl[isl].x, G.isl[isl].y) }; }
        }
        if ((type == "return" || type == "hold" || type == "scout") && (s.state == "out" || s.state == "mining")) GoHome(s);
        if (type == "return") s.order = null;
        Chat(s); return true;
    }

    // ---------------- hangar walking paths ----------------
    public static float[][] PathFor(Ship s, bool outward)
    {
        var p = Data.Pads[s.idx % 5]; float hx = p[0] <= 0 ? p[0] - 3.7f : p[0] + 3.7f; var b = Data.BSpot[s.idx % 5];
        var there = new[] { new[] { hx, p[1] - 6 }, new[] { 0f, 2f }, new[] { 0f, -12f }, new[] { 0f, -21f }, new[] { b[0], b[1] } };
        if (outward) return there;
        var back = there.Reverse().ToList(); back.Add(new[] { hx, p[1] }); return back.ToArray();
    }
    bool WalkPath(Ship s, float dt)
    {
        if (s.path.Count == 0) return true;
        var w = s.path[0]; float dx = w[0] - s.px, dz = w[1] - s.pz, d = Mx.Hypot(dx, dz), mv = 5 * dt;
        s.pa = MathF.Atan2(dx, dz); s.walking = true;
        if (d <= mv) { s.px = w[0]; s.pz = w[1]; s.path.RemoveAt(0); } else { s.px += dx / d * mv; s.pz += dz / d * mv; }
        return s.path.Count == 0;
    }

    // ---------------- Executor navigation ----------------
    public const float ExMax = 45, ExTurn = .35f, ExBurn = .003f, ExReserve = 30f;
    public bool SailTo(float x, float y)
    {
        if (G.aeth < ExReserve + 5) { Say("Not enough Aetherite in stores to sail (reserve " + (int)ExReserve + ").", C_WARN); return false; }
        G.exDestX = Math.Clamp(x, 250, Data.W - 250); G.exDestY = Math.Clamp(y, 250, Data.W - 250); G.exMoving = true;
        Say("Executor setting course.", C_CYAN); return true;
    }
    public bool SailToIsland(Island i)
    {
        float dx = G.exx - i.x, dy = G.exy - i.y, d = Mx.Hypot(dx, dy); if (d < 1) { dx = 1; d = 1; }
        return SailTo(i.x + dx / d * 380, i.y + dy / d * 380);
    }
    public void Halt() { if (G.exMoving) Say("Executor holding position.", C_CYAN); G.exMoving = false; }
    void ExStep(float dt)
    {
        float target = 0;
        if (G.exMoving)
        {
            float dx = G.exDestX - G.exx, dy = G.exDestY - G.exy, d = Mx.Hypot(dx, dy);
            if (d < 15) { G.exMoving = false; Say("Executor on station.", C_CYAN); }
            else
            {
                float want = MathF.Atan2(-dx, -dy), diff = (((want - G.exHead + MathF.PI) % MathF.Tau) + MathF.Tau) % MathF.Tau - MathF.PI;
                G.exHead += Math.Clamp(diff, -ExTurn * dt, ExTurn * dt);
                target = MathF.Abs(diff) < .5f ? MathF.Min(ExMax, MathF.Max(8, d * .35f)) : 0;
            }
        }
        G.exSpeed += Math.Clamp(target - G.exSpeed, -25 * dt, 15 * dt);
        if (G.exSpeed > .01f)
        {
            float burn = G.exSpeed * dt * ExBurn;
            if (G.aeth - burn < ExReserve) { G.exMoving = false; G.exSpeed = 0; Say("Executor halted: stores too low to keep sailing.", C_WARN); }
            else
            {
                G.aeth -= burn; G.exx = Math.Clamp(G.exx - MathF.Sin(G.exHead) * G.exSpeed * dt, 250, Data.W - 250); G.exy = Math.Clamp(G.exy - MathF.Cos(G.exHead) * G.exSpeed * dt, 250, Data.W - 250);
            }
        }
        else G.exSpeed = 0;
    }

    // ---------------- main step ----------------
    public void Step(float dt)
    {
        G.t += dt;
        ExStep(dt);
        float sens = 300 + 130 * G.upSensors;
        foreach (var s in G.ships)
        {
            var st = StatOf(s); s.walking = false;
            s.loy -= dt * (s.indep ? 0 : (s.order != null && s.order.type != "hold" ? .15f : .5f) / (1 + .35f * G.upQuarters));
            if (!s.indep && s.loy <= 0)
            {
                s.indep = true; G.indepEver = 1; s.loy = 0; s.order = null;
                Say(s.name + " has gone independent!", C_WARN); ShipTalk(s, "You left me alone too long, Commander. I'll look after myself now.");
            }
            if (s.state == "docked")
            {
                { var hp = ExPt(0, 190); s.x = hp.x; s.y = hp.y; }
                s.docked += dt;
                if (s.fuel < st.fuel) { float need = MathF.Min(st.fuel - s.fuel, dt * 6), cost = need * .4f; if (G.aeth >= cost) { G.aeth -= cost; s.fuel += need; } }
                if (s.cargo > 0) { float d = MathF.Min(s.cargo, dt * 10); s.cargo -= d; float share = s.indep ? .6f : 1; G.aeth += d * share; G.delivered += d * share; if (s.cargo <= .01f) { s.cargo = 0; s.trips++; } }
                if (s.hull < st.hull) { float rp = MathF.Min(st.hull - s.hull, dt * 5), rc = rp * .3f; if (G.aeth >= rc) { G.aeth -= rc; s.hull += rp; } }
                bool ready = s.cargo <= 0 && s.hull >= st.hull * .8f && (s.fuel >= st.fuel * .8f || (s.docked > 40 && s.fuel >= st.fuel * .3f));
                if (s.ph == "exit") { if (WalkPath(s, dt)) { s.ph = "idle"; s.idle = 4 + Rf() * 7; s.pa = MathF.PI; } }
                else if (s.ph == "idle") { s.pa = MathF.PI; s.idle -= dt; if (s.idle <= 0 && CanGo(s) && ready) { s.path = PathFor(s, false).ToList(); s.ph = "back"; } }
                else if (s.ph == "back") { if (WalkPath(s, dt)) s.ph = "aboard"; }
                else if (s.ph == "aboard") { if (s.docked > 4 && ready) { var p = PlanFor(s); if (p != null) Launch(s, p); else s.docked = 3; } }
            }
            else if (s.state == "landing")
            {
                s.lt += dt / 6;
                if (s.lt >= 1)
                {
                    s.state = "docked"; s.docked = 0; { var hp = ExPt(0, 190); s.x = hp.x; s.y = hp.y; } s.ph = s.indep ? "aboard" : "exit";
                    var p = Data.Pads[s.idx % 5]; s.px = p[0] <= 0 ? p[0] - 3.7f : p[0] + 3.7f; s.pz = p[1];
                    s.path = PathFor(s, true).ToList(); if (s.ph == "aboard") s.path.Clear();
                }
            }
            else if (s.state == "takeoff")
            {
                s.lt += dt / 5;
                if (s.lt >= 1) { s.state = string.IsNullOrEmpty(s.nextState) ? "out" : s.nextState; { var mp = ExPt(0, 330); s.x = mp.x; s.y = mp.y; } s.wp = s.pendWp; s.ang = MathF.Atan2(MathF.Cos(G.exHead), MathF.Sin(G.exHead)); s.lt = 0; }
            }
            else
            {
                if (s.state == "in") { if (s.wp.Count >= 2) { s.wp[0] = ExPt(0, 400); s.wp[1] = ExPt(0, 330); } else if (s.wp.Count == 1) s.wp[0] = ExPt(0, 330); }
                var w = s.wp.Count > 0 ? s.wp[0] : null;
                if (w != null)
                {
                    float dx = w.x - s.x, dy = w.y - s.y, d = Mx.Hypot(dx, dy), mv = st.speed * dt;
                    if (d > 1) s.ang = MathF.Atan2(dy, dx);
                    if (d <= mv) { s.x = w.x; s.y = w.y; s.wp.RemoveAt(0); if (s.wp.Count == 0) Arrive(s); } else { s.x += dx / d * mv; s.y += dy / d * mv; }
                    s.fuel -= MathF.Min(mv, d) * .0085f;
                }
                foreach (var i in G.isl) if (!i.found && Mx.Hypot(i.x - s.x, i.y - s.y) < sens * .8f) Discover(i, s);
                if (s.state == "mining")
                {
                    var i = G.isl[s.target]; float g = MathF.Min(st.mine * dt, MathF.Min(i.aeth, st.cargo - s.cargo));
                    i.aeth -= g; s.cargo += g; if (s.cargo >= st.cargo - .01f || i.aeth <= .5f) GoHome(s);
                }
                if (s.state == "out" || s.state == "mining") { float home = Mx.Hypot(s.x - G.exx, s.y - G.exy) * .0085f * 1.3f; if (s.fuel < home + 2) GoHome(s); }
                s.inStorm = false;
                foreach (var q in G.storms)
                    if (Mx.Hypot(q.x - s.x, q.y - s.y) < q.r * .9f && q.age > 5)
                    {
                        s.inStorm = true; s.hull = MathF.Max(1, s.hull - dt * 2 / (1 + .5f * s.lv.hull));
                        if (Rf() < dt * .5f && !s.warned) { s.warned = true; Say(s.name + " is caught in an ion storm!", C_BAD); if (Rf() < .5f) ShipTalk(s, "Hull taking hits! Request permission to run, Commander!"); }
                    }
                if (!s.inStorm) s.warned = false;
                if (s.hull < st.hull * .35f && (s.state == "out" || s.state == "mining")) GoHome(s);
                if (s.fuel < 0) s.fuel = 0;
            }
        }
        foreach (var i in G.isl) i.aeth = MathF.Min(i.max, i.aeth + dt * .15f);
        // storms
        G.nextStorm -= dt;
        if (G.nextStorm <= 0 && G.storms.Count < 2)
        {
            G.nextStorm = 70 + Rf() * 60;
            float a2 = Rf() * 6.283f, d = 1500 + Rf() * 700, tx = G.exx + (Rf() - .5f) * 1200, ty = G.exy + (Rf() - .5f) * 1200;
            float sx = G.exx + MathF.Cos(a2) * d, sy = G.exy + MathF.Sin(a2) * d, dl = Mx.Hypot(tx - sx, ty - sy), sp = 22 + Rf() * 14;
            G.storms.Add(new Storm { x = sx, y = sy, r = 230 + Rf() * 130, vx = (tx - sx) / dl * sp, vy = (ty - sy) / dl * sp, age = 0, life = 130 });
            G.stormsSeen++; Say("Ion storm forming in the Reach. Keep ships clear.", C_PURP);
        }
        foreach (var q in G.storms) { q.age += dt; q.x += q.vx * dt; q.y += q.vy * dt; }
        G.storms.RemoveAll(q => q.age >= q.life);
        StoryStep(dt);
        foreach (var l in Logs) l.t -= dt; Logs.RemoveAll(l => l.t <= 0);
        if (Talk != null) { Talk.t -= dt; if (Talk.t <= 0) Talk = null; }
    }

    void Discover(Island i, Ship? s)
    {
        i.found = true; G.found++;
        Say((s != null ? s.name + " discovered " : "Discovered ") + i.name + " (" + MathF.Round(i.aeth) + " Aetherite).", C_CYAN);
        if (s != null) Chat(s);
    }
    void Arrive(Ship s)
    {
        if (s.state == "in") { s.state = "landing"; s.lt = 0; s.ang = MathF.Atan2(-MathF.Cos(G.exHead), -MathF.Sin(G.exHead)); return; }
        if (s.state == "out")
        {
            if (s.scout) { s.scout = false; GoHome(s); return; }
            var i = s.target >= 0 ? G.isl[s.target] : null; if (i != null && i.aeth > 1) s.state = "mining"; else GoHome(s);
        }
    }
    public float ScanFx;
    public void DoScan()
    {
        float r = 700 + 260 * G.upSensors; int n = 0;
        foreach (var i in G.isl) if (!i.found && Mx.Hypot(i.x - G.exx, i.y - G.exy) < r) { Discover(i, null); n++; }
        if (n == 0) Say("Scan complete. Nothing new in range."); ScanFx = 1.4f;
    }

    // ---------------- upgrades ----------------
    public bool BuyShipUp(Ship s, string key)
    {
        var u = Data.ShipUp.First(x => x.key == key); int lv = Lvl(s, key), c = Data.Cost(u.cost, lv);
        if (G.aeth < c || lv >= 8) return false;
        G.aeth -= c; SetLvl(s, key, lv + 1); s.fuel = MathF.Min(s.fuel, StatOf(s).fuel); Say(s.name + " fitted a " + u.label + ".", C_GOOD); return true;
    }
    public bool BuyExecUp(string key)
    {
        var u = Data.ExecUp.First(x => x.key == key); int lv = key == "sensors" ? G.upSensors : G.upQuarters, c = Data.Cost(u.cost, lv);
        if (G.aeth < c || lv >= u.max) return false;
        G.aeth -= c; if (key == "sensors") G.upSensors++; else G.upQuarters++; Say(u.label + " upgraded.", C_GOOD); return true;
    }
    public static int Lvl(Ship s, string k) => k switch { "eng" => s.lv.eng, "las" => s.lv.las, "tank" => s.lv.tank, "car" => s.lv.car, _ => s.lv.hull };
    static void SetLvl(Ship s, string k, int v) { switch (k) { case "eng": s.lv.eng = v; break; case "las": s.lv.las = v; break; case "tank": s.lv.tank = v; break; case "car": s.lv.car = v; break; default: s.lv.hull = v; break; } }

    // ---------------- story ----------------
    sealed class Beat { public string k = ""; public Func<GameState, bool> c = _ => false; public object w = ""; public string x = ""; }
    static readonly Beat[] Beats =
    {
        new(){k="welcome",c=g=>g.t>.3,w="EXEC",x="Commander Jason on deck. Systems nominal. Three pilots are waiting in the hangar. Send them to the islands: Aetherite is all that keeps us alive out here."},
        new(){k="launch",c=g=>g.launches>=1,w=0,x="First one out the door. Watch the burn, Commander. I'll be back with a full hold before you finish your coffee."},
        new(){k="deliver",c=g=>g.delivered>=20,w=1,x="That's the first real load in the tanks. Keep this up and I can start drawing a bigger cargo bay."},
        new(){k="storm",c=g=>g.stormsSeen>=1,w=2,x="Ion storm rolling in. The sky goes violet right before. If a ship is caught in it, call it home before the hull gives out."},
        new(){k="found",c=g=>g.found>=6,w="EXEC",x="Six islands charted. The map on the bridge table is filling in. Lock a pilot onto the richest ones."},
        new(){k="upg",c=g=>g.delivered>=100,w=1,x="We have stores to spare. Tap UPGRADE. A faster engine or a bigger tank pays for itself in a week."},
        new(){k="okoye",c=g=>g.delivered>=150,w=3,x="I heard your hangar lights from three islands over. Old hands, steady burn. Reporting for duty, Commander."},
        new(){k="indep",c=g=>g.indepEver>=1,w="EXEC",x="A pilot has gone independent. Pilots left alone too long look after themselves. Give them an order to win them back."},
        new(){k="pip",c=g=>g.delivered>=500,w=4,x="Hi. I'm the stowaway. Please don't space me. I fixed the third engine on Okoye's ship, if that helps."},
        new(){k="haven",c=g=>g.found>=12,w=2,x="Twelve islands and the maps still don't end. There's a story about a place called Haven, where the storms never come. I believe it."},
        new(){k="thousand",c=g=>g.delivered>=1000,w=3,x="A thousand units delivered. I have buried crews for less than this. Not this one, Commander."},
        new(){k="finale",c=g=>g.delivered>=1500,w="EXEC",x="All tanks full. The charts line up. Commander, I have a heading for Haven."},
    };
    public List<string> BeatLog = new(); // keys fired (for tests)

    void StoryStep(float dt)
    {
        var b = G.beats;
        if (!b.Contains("o") && G.delivered >= 150) { b.Add("o"); AddShip(3); Say("Okoye Adeyemi has joined the crew.", C_GOOD); }
        if (!b.Contains("p") && G.delivered >= 500) { b.Add("p"); AddShip(4); Say("Pip has joined the crew.", C_GOOD); }
        if (EpilogueIn > 0) { EpilogueIn -= dt; if (EpilogueIn <= 0) { EpilogueIn = -1; CrawlRequested = true; } }
        beatCool -= dt; if (beatCool > 0) return;
        foreach (var e in Beats)
        {
            string key = "s_" + e.k;
            if (!b.Contains(key) && e.c(G))
            {
                b.Add(key); beatCool = 11; BeatLog.Add(e.k);
                if (e.w is string) SayTalk("Executor", "ship AI", C_CYAN, e.x, 9);
                else { var c = Data.Crew[(int)e.w]; SayTalk(c.Name, c.Call, c.Color, e.x, 9); }
                if (e.k == "finale") { Say("Haven found. The story ends here. Play on, Commander.", 0xffc24a); EpilogueIn = 9.5f; }
                return;
            }
        }
    }

    // ---------------- save / load ----------------
    static readonly JsonSerializerOptions Jo = new() { IncludeFields = true, WriteIndented = false };
    public string Serialize(int selIdx) => JsonSerializer.Serialize(new SaveBlob { G = G, sel = selIdx }, Jo);
    public int Deserialize(string json)
    {
        var d = JsonSerializer.Deserialize<SaveBlob>(json, Jo) ?? throw new InvalidDataException("empty save");
        if (d.G == null) throw new InvalidDataException("no game");
        G = d.G; beatCool = 5; Logs.Clear(); Talk = null; EpilogueIn = -1;
        if (G.storyV == 0) { G.storyV = 1; foreach (var e in Beats) if (e.c(G)) G.beats.Add("s_" + e.k); }
        foreach (var s in G.ships)
        {
            if (s.hull <= 0) s.hull = StatOf(s).hull;
            if (s.state == "landing" || s.state == "takeoff") { s.state = "docked"; s.ph = "aboard"; s.docked = 0; s.path.Clear(); }
        }
        return Math.Clamp(d.sel, 0, G.ships.Count - 1);
    }
    public sealed class SaveBlob { public GameState? G; public int sel; }

    public static string SavePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceBlocks", "save.json");
    public void SaveToDisk(int selIdx)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!); var tmp = SavePath + ".tmp"; File.WriteAllText(tmp, Serialize(selIdx)); File.Move(tmp, SavePath, true); } catch { }
    }
    public int? LoadFromDisk() { try { return File.Exists(SavePath) ? Deserialize(File.ReadAllText(SavePath)) : null; } catch { return null; } }
}
