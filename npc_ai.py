"""Pilot decision making: orders, utility-based self direction, upgrades."""
import math
import random
import ships
from config import BLOCKS, BUILDABLE, WORLD
from crew import PERS


def dist(ax, ay, bx, by):
    return math.hypot(ax - bx, ay - by)


def patience(world, ship):
    return PERS[ship.pers]["patience"] * (1 + 0.35 * world.up["quarters"])


# ---- orders ---------------------------------------------------------------
def give_order(world, ship, otype, island_id=None):
    """Commander orders a pilot. Returns (accepted, message)."""
    p = PERS[ship.pers]
    if ship.independent and world.rng.random() < p["stubborn"]:
        ship.loyalty = min(100, ship.loyalty + 12)
        world.say(ship, "refuse", force=True)
        return False, f"{ship.name} refused the order"
    if ship.independent:
        ship.independent = False
        world.stats["reconciled"] = world.stats.get("reconciled", 0) + 1
    ship.loyalty = 100
    ship.last_order = world.time
    world.say(ship, "obey")
    order = {"type": otype}
    if island_id is not None:
        order["island"] = island_id
    ship.order = order
    if otype == "mine":
        isl = world.island(island_id)
        world.log_add(f"{ship.name} locked onto {isl.name}", "lock", ship.color)
        if ship.state in ("outbound", "mining", "scouting"):
            retarget(world, ship, isl)
        ship.sortie = False
    elif otype in ("return", "hold"):
        if ship.state not in ("docked", "inbound", "docking"):
            go_home(world, ship)
    elif otype == "scout":
        ship.sortie = False
        if ship.state in ("outbound", "mining"):
            go_home(world, ship)   # unload first; scouting launches after service
    elif otype == "build":
        pass
    return True, f"{ship.name}: {otype} acknowledged"


def retarget(world, ship, isl):
    ship.target = isl.id
    ship.state = "outbound"
    ship.wp = [world.island_hover(isl)]


def go_home(world, ship):
    ship.state = "inbound"
    ship.target = None if ship.state != "mining" else ship.target
    ship.wp = [world.approach_pos(), world.hangar_pos()]
    world.say(ship, "return")


# ---- planning -------------------------------------------------------------
def best_island(world, ship, st):
    p = PERS[ship.pers]
    ex, ey = world.exec_pos
    best, best_s = None, 0
    for isl in world.islands:
        if not isl.discovered or isl.aeth < 25:
            continue
        d = dist(isl.x, isl.y, ex, ey)
        trip = 2 * d * st["burn"] / st["speed"] * 1.2
        if trip > st["fuel_max"] * 0.85:
            continue
        crowd = sum(1 for s in world.ships if s is not ship and s.target == isl.id)
        score = isl.aeth / (d + 400) * (1 - 0.3 * min(crowd, 3)) + p["far"] * d * 0.00005
        if score > best_s:
            best, best_s = isl, score
    return best


def plan_for(world, ship):
    """What should a serviced, docked pilot do next? -> ('mine', isl) | ('scout',) | None"""
    st = ships.stats(ship.grid)
    o = ship.order
    if ship.independent or o is None:
        if ship.independent:
            isl = best_island(world, ship, st)
            return ("mine", isl) if isl else ("scout",)
        return None
    t = o["type"]
    if t == "hold":
        return None
    if t in ("return", "build"):
        ship.order = None
        return None
    if t == "scout":
        return ("scout",)
    if t == "mine":
        isl = world.island(o["island"])
        trip = 2 * dist(isl.x, isl.y, *world.exec_pos) * st["burn"] / st["speed"] * 1.2
        if isl.discovered and isl.aeth >= 15 and trip <= st["fuel_max"] * 0.9:
            return ("mine", isl)
        world.log_add(f"{isl.name} is spent for {ship.name}; switching to auto", "info", ship.color)
        ship.order = {"type": "auto"}
    if ship.order and ship.order["type"] == "auto":
        isl = best_island(world, ship, st)
        return ("mine", isl) if isl else ("scout",)
    return None


def scout_point(world, ship):
    ex, ey = world.exec_pos
    known = [(i.x, i.y) for i in world.islands if i.discovered]
    best, best_d = None, -1
    for _ in range(10):
        a = world.rng.uniform(0, math.tau)
        r = world.rng.uniform(1400, 3700)
        x = min(WORLD - 200, max(200, ex + math.cos(a) * r))
        y = min(WORLD - 200, max(200, ey + math.sin(a) * r))
        md = min([dist(x, y, kx, ky) for kx, ky in known] or [9999])
        if md > best_d:
            best, best_d = (x, y), md
    return best


def launch(world, ship, plan):
    st = ships.stats(ship.grid)
    if plan[0] == "mine":
        isl = plan[1]
        ship.target = isl.id
        ship.state = "outbound"
        ship.wp = [world.approach_pos(), world.island_hover(isl)]
        world.log_add(f"{ship.name} locked onto {isl.name} and launched", "lock", ship.color)
    else:
        ship.target = None
        ship.state = "scouting"
        ship.scout_legs = 0
        ship.wp = [world.approach_pos(), scout_point(world, ship)]
        world.log_add(f"{ship.name} launched on a scouting run", "info", ship.color)
    ship.svc_done = False
    ship.sortie = True
    ship.slot = None
    world.stats["launches"] = world.stats.get("launches", 0) + 1
    world.say(ship, "launch")


# ---- upgrades -------------------------------------------------------------
def choose_goal(world, ship):
    p = PERS[ship.pers]
    st = ships.stats(ship.grid)
    w = dict(p["build"])
    if st["fuel_max"] < 30:
        w["T"] = w.get("T", 1) * 2
    if st["n"]["E"] == 0:
        w["E"] = 9
    opts = [c for c in BUILDABLE if w.get(c, 0) > 0]
    return world.rng.choices(opts, [w[c] for c in opts])[0]


def try_build(world, ship, sponsor=False):
    """Spend personal fund (or Executor aeth when sponsored) on the pilot's chosen part."""
    built = 0
    for _ in range(3):
        if ship.goal is None:
            ship.goal = choose_goal(world, ship)
        ch = ship.goal
        cost = ships.block_cost(ship.grid, ch)
        if sponsor:
            if world.aeth < cost + 40:
                world.log_add(f"Not enough Aetherite to sponsor {ship.name}'s upgrade", "warn", ship.color)
                break
        elif ship.fund < cost:
            break
        spots = ships.candidate_cells(ship.grid, ch, world.max_blocks(), world.rng)
        if not spots:
            if ships.count(ship.grid) >= world.max_blocks():
                ship.goal = None
                if built == 0 and sponsor:
                    world.log_add(f"{ship.name}'s ship is at the hangar size limit", "warn", ship.color)
            break
        x, y = spots[0]
        if sponsor:
            world.aeth -= cost
        else:
            ship.fund -= cost
        ship.grid = ships.place(ship.grid, x, y, ch)
        ship.built += 1
        built += 1
        world.stats["parts_built"] = world.stats.get("parts_built", 0) + 1
        world.say(ship, "build", block=BLOCKS[ch][0], force=True)
        # mirror for a tidy ship
        mx = 6 - x
        if mx != x and ships.can_place(ship.grid, mx, y, world.max_blocks()):
            c2 = ships.block_cost(ship.grid, ch)
            if (world.aeth - 40 >= c2) if sponsor else (ship.fund >= c2):
                if sponsor:
                    world.aeth -= c2
                else:
                    ship.fund -= c2
                ship.grid = ships.place(ship.grid, mx, y, ch)
                ship.built += 1
                world.stats["parts_built"] += 1
        ship.goal = None
        st = ships.stats(ship.grid)
        ship.fuel = min(ship.fuel, st["fuel_max"])
        ship.hull = min(ship.hull + 4, st["hull"])
        if sponsor:
            break
    return built


# ---- per-tick thinking ----------------------------------------------------
def think(world, ship):
    st = ships.stats(ship.grid)
    s = ship.state
    if s == "docked":
        if not ship.svc_done:
            return
        if ship.order and ship.order["type"] == "build":
            try_build(world, ship, sponsor=True)
            ship.order = None
        try_build(world, ship)
        plan = plan_for(world, ship)
        if plan:
            # don't leave on fumes
            if ship.fuel >= 0.3 * st["fuel_max"] and ship.hull >= 0.4 * st["hull"]:
                launch(world, ship, plan)
        return
    if s in ("outbound", "scouting", "mining"):
        p = PERS[ship.pers]
        home = dist(ship.x, ship.y, *world.hangar_pos())
        need = home * st["burn"] / st["speed"] * 1.25
        low_fuel = ship.fuel < max(need + 1.5, p["caution"] * st["fuel_max"] * (0.5 if s == "mining" else 1.0))
        if low_fuel or ship.hull < 0.35 * st["hull"]:
            go_home(world, ship)
            return
        if s == "mining":
            isl = world.island(ship.target)
            if ship.cargo >= st["cargo"] - 0.01 or isl is None or isl.aeth <= 0.5:
                go_home(world, ship)
