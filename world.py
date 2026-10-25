"""World state + headless simulation (no pygame display needed)."""
import math
import random
import ships
import npc_ai
from config import *
from crew import CREW, PERS, LINES

ISLAND_NAMES = [
    "Aurelia Reach", "Verdant Spire", "Mistfall Isle", "Lumen Hollow", "Cascade Crown",
    "Emerald Drift", "Silverbrook", "Whisper Atoll", "Hanging Gardens", "Opal Terrace",
    "Thornwick", "Dewfall Perch", "Skyharbor", "Willow Veil", "Ember Cliff",
    "Cloudmoss", "Glimmer Shelf", "Tidewatch", "Saffron Ridge", "Lantern Rise",
]


def mmss(t):
    return f"{int(t) // 60:02d}:{int(t) % 60:02d}"


class Serial:
    def to_dict(self):
        return {k: v for k, v in self.__dict__.items() if not k.startswith("_")}

    @classmethod
    def from_dict(cls, d):
        o = cls.__new__(cls)
        o.__dict__.update(d)
        return o


class Island(Serial):
    def __init__(self, id, name, x, y, seed, width, aeth, special=False):
        self.id, self.name, self.x, self.y = id, name, x, y
        self.seed, self.width, self.special = seed, width, special
        self.aeth = self.aeth_max = aeth
        self.discovered = False
        self.visited = False
        self.land_dx, self.land_dy = 0.0, -0.12 * width
        self.radius = width * 0.4


class Storm(Serial):
    def __init__(self, x, y, r, vx, vy):
        self.x, self.y, self.r, self.vx, self.vy = x, y, r, vx, vy


class Ship(Serial):
    def __init__(self, id, name, pers, color, pos):
        self.id, self.name, self.pers, self.color = id, name, pers, color
        self.x, self.y = pos
        self.vx = self.vy = 0.0
        self.ang = -math.pi / 2
        self.grid = list(STARTER_GRID)
        st = ships.stats(self.grid)
        self.fuel, self.hull, self.cargo = st["fuel_max"], st["hull"], 0.0
        self.state = "docked"
        self.order = None
        self.target = None
        self.wp = []
        self.loyalty = 100.0
        self.independent = False
        self.last_order = 0.0
        self.fund = 0.0
        self.goal = None
        self.svc_done = False
        self.sortie = False
        self.scout_legs = 0
        self.slot = None
        self.mined_total = 0.0
        self.built = 0
        self.last_say = -99.0
        self.think_t = random.random()


class World:
    def __init__(self, seed=None, fresh=True):
        self.seed = seed if seed is not None else random.randrange(1 << 30)
        self.rng = random.Random(self.seed ^ 0x5EED)
        self.time = 0.0
        self.aeth = 120.0
        self.up = {k: 0 for k in UPGRADES}
        self.islands, self.ships, self.storms = [], [], []
        self.stats = dict(deposited=0.0, launches=0, parts_built=0, upgrades=0,
                          independents=0, found=0, scans=0)
        self.crew_joined = []
        self.story = dict(done=[], active=None)
        self.log = []
        self.scan_cd = 0.0
        self.pulse = None            # (t0, radius)
        self.events = []             # transient: consumed by the UI
        self.verbose = True
        self._slow_t = 0.0
        self._full_warn = -99.0
        if fresh:
            self.generate()

    # ---- geometry -------------------------------------------------------
    @property
    def exec_pos(self):
        return EXEC_POS

    def hangar_pos(self):
        return (EXEC_POS[0] + HANGAR_OFF[0], EXEC_POS[1] + HANGAR_OFF[1])

    def approach_pos(self):
        return (EXEC_POS[0] + APPROACH_OFF[0], EXEC_POS[1] + APPROACH_OFF[1])

    def slot_pos(self, slot):
        return (EXEC_POS[0] + (slot - 2) * 26, EXEC_POS[1] + HANGAR_OFF[1] - 30)

    def island(self, iid):
        for i in self.islands:
            if i.id == iid:
                return i
        return None

    def island_hover(self, isl):
        return (isl.x + isl.land_dx, isl.y + isl.land_dy - 26)

    # ---- capacities -----------------------------------------------------
    @property
    def capacity(self):
        return 200 + 250 * self.up["tanks"]

    @property
    def scan_radius(self):
        return 1200 + 500 * self.up["sensors"]

    @property
    def passive_radius(self):
        return 420 + 100 * self.up["sensors"]

    def max_blocks(self):
        return 10 + 4 * self.up["hangar"]

    def upgrade_cost(self, key):
        lvl = self.up[key]
        return UPGRADES[key][2] * (lvl + 1)

    # ---- generation -----------------------------------------------------
    def generate(self):
        r = random.Random(self.seed)
        ex, ey = EXEC_POS
        names = ISLAND_NAMES[:]
        r.shuffle(names)
        pts = []
        tries = 0
        while len(pts) < 17 and tries < 4000:
            tries += 1
            a = r.uniform(0, math.tau)
            d = r.uniform(750, 3500) if len(pts) >= 2 else r.uniform(750, 1050)
            x, y = ex + math.cos(a) * d, ey + math.sin(a) * d
            if not (350 < x < WORLD - 350 and 350 < y < WORLD - 350):
                continue
            if all(math.hypot(x - px, y - py) > 560 for px, py in pts):
                pts.append((x, y))
        for i, (x, y) in enumerate(pts):
            w = r.choice([300, 340, 380, 420])
            aeth = r.randint(300, 900)
            self.islands.append(Island(i, names[i % len(names)], x, y, r.randrange(1 << 20), w, aeth))
        # Haven: the story destination, far away
        for _ in range(500):
            a = r.uniform(0, math.tau)
            x, y = ex + math.cos(a) * 3400, ey + math.sin(a) * 3400
            if 400 < x < WORLD - 400 and 400 < y < WORLD - 400:
                break
        else:
            x, y = ex + 3000, ey - 3000
        self.islands.append(Island(len(pts), "Haven", x, y, 777, 460, 2500, special=True))
        for isl in self.islands[:2]:
            isl.discovered = True
        for _ in range(3):
            self.add_storm(r)
        for c in CREW:
            if c["join"] == 0:
                self.add_crew(c, announce=False)

    def add_storm(self, r=None):
        r = r or self.rng
        for _ in range(100):
            x, y = r.uniform(400, WORLD - 400), r.uniform(400, WORLD - 400)
            if math.hypot(x - EXEC_POS[0], y - EXEC_POS[1]) > 1000:
                break
        a = r.uniform(0, math.tau)
        sp = r.uniform(14, 30)
        self.storms.append(Storm(x, y, r.uniform(210, 330), math.cos(a) * sp, math.sin(a) * sp))

    def add_crew(self, c, announce=True):
        idx = len(self.ships)
        s = Ship(idx, c["name"], c["pers"], c["color"], self.slot_pos(idx % 5))
        s.slot = self.free_slot()
        s.x, s.y = self.slot_pos(s.slot)
        s.think_t = self.rng.random()
        self.ships.append(s)
        self.crew_joined.append(c["name"])
        if announce:
            self.log_add(f"{c['name']} joined the crew of the Executor", "good", c["color"])
        return s

    def free_slot(self):
        used = {s.slot for s in self.ships if s.state == "docked" and s.slot is not None}
        i = 0
        while i in used:
            i += 1
        return i

    # ---- logging / chatter ---------------------------------------------
    def log_add(self, text, kind="info", color=None):
        self.log.append(dict(t=self.time, text=text, kind=kind, color=color))
        self.log = self.log[-60:]
        self.events.append(("log", kind))
        if self.verbose:
            print(f"[{mmss(self.time)}] {text}", flush=True)

    def say(self, ship, kind, force=False, **fmt):
        if not force and self.time - ship.last_say < 25:
            return
        pool = LINES[ship.pers].get(kind)
        if not pool:
            return
        ship.last_say = self.time
        self.log_add(self.rng.choice(pool).format(**fmt), "chat", ship.color)

    # ---- commander actions ---------------------------------------------
    def give_order(self, ship, otype, island_id=None):
        return npc_ai.give_order(self, ship, otype, island_id)

    def do_scan(self):
        if self.scan_cd > 0:
            return None
        self.scan_cd = 14.0
        self.pulse = (self.time, self.scan_radius)
        self.stats["scans"] += 1
        found = []
        for isl in self.islands:
            if not isl.discovered and math.hypot(isl.x - EXEC_POS[0], isl.y - EXEC_POS[1]) < self.scan_radius + isl.radius:
                self.discover(isl, None)
                found.append(isl)
        if not found:
            self.log_add("Scan complete: nothing new within range", "info")
        self.events.append(("scan", len(found)))
        return found

    def buy_upgrade(self, key):
        lvl, cost = self.up[key], self.upgrade_cost(key)
        if lvl >= UPGRADES[key][1]:
            return False, "Already at max level"
        if self.aeth < cost:
            return False, f"Need {cost} {RES_NAME}"
        self.aeth -= cost
        self.up[key] += 1
        self.stats["upgrades"] += 1
        self.log_add(f"Executor upgraded: {UPGRADES[key][0]} → level {self.up[key]}", "good")
        self.events.append(("upgrade", key))
        return True, "Upgraded"

    def build_for(self, ship, x, y, ch):
        """Commander-sponsored placement from the Executor stores."""
        cost = ships.block_cost(ship.grid, ch)
        if not ships.can_place(ship.grid, x, y, self.max_blocks()):
            return False, "Can't place there"
        if self.aeth < cost:
            return False, f"Need {cost} {RES_NAME}"
        self.aeth -= cost
        ship.grid = ships.place(ship.grid, x, y, ch)
        ship.built += 1
        self.stats["parts_built"] += 1
        st = ships.stats(ship.grid)
        ship.hull = min(ship.hull + 4, st["hull"])
        self.log_add(f"{ship.name}'s ship fitted with a {BLOCKS[ch][0]}", "good", ship.color)
        self.events.append(("build", ch))
        return True, "Built"

    def discover(self, isl, by):
        isl.discovered = True
        self.stats["found"] += 1
        if by is not None:
            self.say(by, "found", force=True)
        self.log_add(f"New island discovered: {isl.name}" + (f" (by {by.name})" if by else ""),
                     "found", by.color if by else C_ACCENT)
        self.events.append(("found", isl.id))

    # ---- simulation -----------------------------------------------------
    def update(self, dt):
        self.time += dt
        self.scan_cd = max(0.0, self.scan_cd - dt)
        for st in self.storms:
            st.x += st.vx * dt
            st.y += st.vy * dt
            if not 200 < st.x < WORLD - 200:
                st.vx = -st.vx
            if not 200 < st.y < WORLD - 200:
                st.vy = -st.vy
        for isl in self.islands:
            if isl.aeth < isl.aeth_max:
                isl.aeth = min(isl.aeth_max, isl.aeth + 0.12 * dt)
        if self.aeth < 30:
            self.aeth += 0.4 * dt       # emergency reactor trickle
        for s in self.ships:
            self.update_ship(s, dt)
        self.passive_discovery()
        self._slow_t += dt
        if self._slow_t >= 1.0:
            self._slow_t = 0.0
            self.slow_tick()

    def passive_discovery(self):
        ex, ey = EXEC_POS
        for isl in self.islands:
            if isl.discovered:
                continue
            if math.hypot(isl.x - ex, isl.y - ey) < self.passive_radius + isl.radius:
                self.discover(isl, None)
                continue
            for s in self.ships:
                if s.state != "docked" and math.hypot(isl.x - s.x, isl.y - s.y) < 520 + isl.radius:
                    self.discover(isl, s)
                    break

    def slow_tick(self):
        # loyalty: idle pilots lose patience and go their own way
        for s in self.ships:
            if s.order is None and not s.independent:
                s.loyalty -= 100.0 / npc_ai.patience(self, s)
                if s.loyalty <= 0:
                    s.loyalty = 0
                    s.independent = True
                    self.stats["independents"] += 1
                    self.say(s, "independent", force=True)
                    self.log_add(f"{s.name} has gone independent — prioritising their own goals", "warn", s.color)
                    self.events.append(("independent", s.id))
            elif s.independent:
                s.loyalty = 0
        # crew joining
        for c in CREW:
            if c["name"] not in self.crew_joined and self.stats["deposited"] >= c["join"]:
                self.add_crew(c)
        # special island
        for s in self.ships:
            for isl in self.islands:
                if isl.special and not isl.visited and s.state != "docked" and \
                        math.hypot(isl.x - s.x, isl.y - s.y) < 700:
                    isl.visited = True
                    self.stats["haven_reached"] = 1

    def fund_share(self, ship):
        if ship.independent:
            return min(0.9, 0.25 + PERS[ship.pers]["greed"] * 1.2)
        return 0.25

    def update_ship(self, s, dt):
        st = ships.stats(s.grid)
        if s.state == "docked":
            self.service(s, st, dt)
        else:
            self.fly(s, st, dt)
        s.think_t -= dt
        if s.think_t <= 0:
            s.think_t = 0.4
            npc_ai.think(self, s)

    # -- docked service ---
    def service(self, s, st, dt):
        s.x, s.y = self.slot_pos(s.slot if s.slot is not None else 0)
        s.vx = s.vy = 0
        s.ang = -math.pi / 2
        svc = 1 + 0.5 * self.up["hangar"]
        if s.cargo > 0.01:
            room = self.capacity - self.aeth
            share = self.fund_share(s)
            amt = min(s.cargo, 8 * svc * dt)
            to_exec = amt * (1 - share)
            if to_exec <= room + 1e-9:
                s.cargo -= amt
                s.fund += amt * share
                self.aeth += to_exec
                self.stats["deposited"] += to_exec
                if self.stats["deposited"] and not self.stats.get("first_deposit"):
                    self.stats["first_deposit"] = 1
            elif self.time - self._full_warn > 30:
                self._full_warn = self.time
                self.log_add("Storage tanks full! Spend Aetherite or upgrade Storage Tanks", "warn")
        need = st["fuel_max"] - s.fuel
        if need > 0.01 and self.aeth >= 1:
            amt = min(need, 6 * svc * dt, self.aeth / 0.5)
            s.fuel += amt
            self.aeth -= amt * 0.5
        need = st["hull"] - s.hull
        if need > 0.01 and self.aeth >= 1:
            amt = min(need, 5 * svc * dt, self.aeth / 0.3)
            s.hull += amt
            self.aeth -= amt * 0.3
        fueled = s.fuel >= st["fuel_max"] - 0.05 or (self.aeth < 1 and s.fuel >= 0.35 * st["fuel_max"])
        repaired = s.hull >= st["hull"] - 0.05 or self.aeth < 1
        if s.cargo < 0.05 and fueled and repaired and not s.svc_done:
            s.svc_done = True
            s.think_t = 0

    # -- flying ---
    def fly(self, s, st, dt):
        maxv = st["speed"] * (0.25 if s.fuel <= 0 else 1.0)
        if s.state == "mining":
            self.mine(s, st, dt)
            return
        if not s.wp:
            s.wp = [self.approach_pos(), self.hangar_pos()]
            s.state = "inbound"
        tx, ty = s.wp[0]
        final = len(s.wp) == 1
        docking = s.state == "inbound" and final
        dx, dy = tx - s.x, ty - s.y
        d = math.hypot(dx, dy)
        arrive = 10 if docking else 36
        speed = maxv if not final else min(maxv, max(40, d * 2.2))
        if d > 1e-6:
            dvx, dvy = dx / d * speed, dy / d * speed
        else:
            dvx = dvy = 0
        # storm avoidance
        for sm in self.storms:
            sd = math.hypot(s.x - sm.x, s.y - sm.y)
            if sd < sm.r + 150 and not (docking or math.hypot(s.x - EXEC_POS[0], s.y - EXEC_POS[1]) < 520):
                k = (sm.r + 150 - sd) / 150
                ox, oy = (s.x - sm.x) / max(sd, 1), (s.y - sm.y) / max(sd, 1)
                dvx += (ox - oy * 0.6) * maxv * 1.3 * min(1, k)
                dvy += (oy + ox * 0.6) * maxv * 1.3 * min(1, k)
            if sd < sm.r:
                dmg = 4.0 / (1 + 0.5 * st["shield"]) * dt
                s.hull -= dmg
        k = min(1.0, dt * 2.5)
        s.vx += (dvx - s.vx) * k
        s.vy += (dvy - s.vy) * k
        sp = math.hypot(s.vx, s.vy)
        if sp > maxv:
            s.vx, s.vy = s.vx / sp * maxv, s.vy / sp * maxv
            sp = maxv
        s.x = min(WORLD, max(0, s.x + s.vx * dt))
        s.y = min(WORLD, max(0, s.y + s.vy * dt))
        if sp > 12:
            ta = math.atan2(s.vy, s.vx)
            da = (ta - s.ang + math.pi) % math.tau - math.pi
            s.ang += da * min(1.0, dt * 6)
        if s.fuel > 0 and sp > 5:
            s.fuel = max(0.0, s.fuel - st["burn"] * dt * (sp / st["speed"]))
        if s.hull <= 0:
            s.hull = 1
            lost = s.cargo
            s.cargo = 0
            self.log_add(f"{s.name} was disabled by an ion storm, cargo lost ({lost:.0f}); limping home", "warn", s.color)
            self.events.append(("alert", s.id))
            npc_ai.go_home(self, s)
        if s.fuel <= 0 and s.state != "inbound":
            self.log_add(f"{s.name} is out of fuel and drifting home", "warn", s.color)
            self.events.append(("alert", s.id))
            npc_ai.go_home(self, s)
        if d < arrive:
            s.wp.pop(0)
            if not s.wp:
                self.arrived(s)

    def arrived(self, s):
        if s.state == "outbound":
            s.state = "mining"
            isl = self.island(s.target)
            if isl:
                isl.visited = True
        elif s.state == "scouting":
            s.scout_legs += 1
            st = ships.stats(s.grid)
            if s.fuel > 0.55 * st["fuel_max"] and s.scout_legs < 3:
                s.wp = [npc_ai.scout_point(self, s)]
            else:
                npc_ai.go_home(self, s)
        elif s.state == "inbound":
            s.state = "docked"
            s.slot = self.free_slot()
            s.x, s.y = self.slot_pos(s.slot)
            s.vx = s.vy = 0
            s.target = None
            s.svc_done = False
            if s.order and s.order["type"] == "scout":
                s.order = None
            self.events.append(("dock", s.id))

    def mine(self, s, st, dt):
        isl = self.island(s.target)
        if isl is None:
            npc_ai.go_home(self, s)
            return
        hx, hy = self.island_hover(isl)
        t = self.time + s.id * 1.7
        hx += math.cos(t * 0.7) * 14
        hy += math.sin(t * 1.1) * 6
        s.vx = (hx - s.x) * 3
        s.vy = (hy - s.y) * 3
        s.x += s.vx * dt
        s.y += s.vy * dt
        s.ang += (-math.pi / 2 - s.ang) * min(1, dt * 3)
        rate = st["mine"] * dt
        room = st["cargo"] - s.cargo
        amt = min(rate, room, isl.aeth)
        if amt > 0:
            isl.aeth -= amt
            s.cargo += amt
            s.mined_total += amt
        if s.fuel > 0:
            s.fuel = max(0.0, s.fuel - 0.1 * dt)
        for sm in self.storms:
            if math.hypot(s.x - sm.x, s.y - sm.y) < sm.r:
                s.hull -= 4.0 / (1 + 0.5 * st["shield"]) * dt

    # ---- persistence ----------------------------------------------------
    def to_dict(self):
        return dict(
            seed=self.seed, time=self.time, aeth=self.aeth, up=self.up, stats=self.stats,
            crew_joined=self.crew_joined, story=self.story, scan_cd=self.scan_cd,
            log=self.log[-40:],
            islands=[i.to_dict() for i in self.islands],
            ships=[s.to_dict() for s in self.ships],
            storms=[s.to_dict() for s in self.storms],
        )

    @classmethod
    def from_dict(cls, d):
        w = cls(seed=d["seed"], fresh=False)
        for k in ("time", "aeth", "up", "stats", "crew_joined", "story", "scan_cd", "log"):
            setattr(w, k, d[k])
        for k in UPGRADES:
            w.up.setdefault(k, 0)
        w.islands = [Island.from_dict(x) for x in d["islands"]]
        w.ships = [Ship.from_dict(x) for x in d["ships"]]
        w.storms = [Storm.from_dict(x) for x in d["storms"]]
        for s in w.ships:
            s.color = tuple(s.color)
            s.wp = [tuple(p) for p in s.wp]
            s.think_t = random.random()
        return w
