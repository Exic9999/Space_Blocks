"""Story: prologue crawl and milestone beats triggered by play."""
from config import C_ACCENT, RES_NAME

PROLOGUE = [
    "THE HOLLOW WARS ENDED THE OLD STATIONS.\nWhat remained of the fleet scattered across the Veil, a sea of silence and storm.",
    "But the Veil is not empty.\nDrift Isles — living islands of stone, forest and waterfall — float through it, "
    "and in their roots grows AETHERITE, the crystal that burns cleaner than any star.",
    "Commander JASON holds the last wedge-class carrier: THE EXECUTOR.\nIts tanks are low, its hangar nearly empty, "
    "and its crew are small ships with big hearts.",
    "Somewhere beyond the storms a beacon pulses: HAVEN.\nGather enough Aetherite and the Wanderlust Drive will take you there.",
    "Lead your pilots. Build their ships, block by block.\nBut don't leave them idle for long...",
]

JASON = (235, 240, 250)
ECHO = C_ACCENT


def _crew(world, name):
    for s in world.ships:
        if s.name.startswith(name):
            return s
    return None


def _pages(*pairs):
    return list(pairs)


BEATS = [
    dict(id="welcome", cond=lambda w: w.time > 0.3,
         pages=[("Mira", "Executor, this is Mira. Pre-flight is green. Where to, Commander?"),
                ("ECHO", f"Tip: pick a pilot on the left, pick an island in the NAV list, then press LOCK ON. "
                         f"Press SCAN to ping for more islands. Idle pilots get restless.")]),
    dict(id="first_launch", cond=lambda w: w.stats["launches"] >= 1,
         pages=[("Tobias", "Launch clean. Fuel burn is nominal. Aetherite, here we come.")]),
    dict(id="first_found", cond=lambda w: w.stats["found"] >= 3,
         pages=[("ECHO", "A new island has appeared on the command deck HUD. Pilots can lock onto it when you give the word.")]),
    dict(id="first_deposit", cond=lambda w: w.stats["deposited"] >= 30,
         pages=[("ECHO", f"First {RES_NAME} secured in the tanks. Pilots keep a share to build their own ships — "
                         f"it keeps them loyal. Mostly.")]),
    dict(id="okoye", cond=lambda w: w.stats["deposited"] >= 150,
         pages=[("Okoye", "Executor... I never thought I'd see her fly again. Permission to dock, Commander?"),
                ("Jason", "Granted, Okoye. Welcome aboard.")]),
    dict(id="first_upgrade", cond=lambda w: w.stats["upgrades"] >= 1,
         pages=[("ECHO", "Executor upgrade installed. A carrier is only as strong as its tanks, bays and crew.")]),
    dict(id="independent", cond=lambda w: w.stats["independents"] >= 1,
         pages=[("ECHO", "Commander, a pilot has stopped waiting for orders and is acting on their own priorities. "
                         "Give them a task to earn them back — they may refuse at first.")]),
    dict(id="storms", cond=lambda w: w.stats["deposited"] >= 400,
         pages=[("ECHO", "Ion storms are thickening in the Veil. Shields help; careful routing helps more."),],
         effect=lambda w: [w.add_storm() for _ in range(2)]),
    dict(id="pip", cond=lambda w: w.stats["deposited"] >= 600,
         pages=[("Pip", "Hi! I'm Pip! I fly fast and I break things! Can I join? Please?"),
                ("Jason", "...Try not to break the Executor, Pip.")]),
    dict(id="beacon", cond=lambda w: w.stats["deposited"] >= 900,
         pages=[("ECHO", "Long-range scan has caught the Haven beacon. Marked on the HUD — it is far, past the storms."),
                ("Zeph", "I can hear it. It sounds like... home.")],
         effect=lambda w: [w.discover(i, None) for i in w.islands if i.special and not i.discovered]),
    dict(id="haven_reached", cond=lambda w: w.stats.get("haven_reached"),
         pages=[("Zeph", "Commander... Haven. The waterfalls fall upward here. You have to see this."),
                ("ECHO", "Haven is rich in Aetherite. The Wanderlust Drive needs 2000 delivered to the Executor.")]),
    dict(id="finale", cond=lambda w: w.stats["deposited"] >= 2000,
         pages=[("ECHO", "Wanderlust Drive primed. All tanks green. Course: Haven."),
                ("Okoye", "Never thought I'd live to see it."),
                ("Mira", "Whatever's out there — we'll meet it together, Commander."),
                ("Jason", "Then let's go home. Take us in, ECHO."),
                ("ECHO", "The Executor is yours to keep exploring. Sandbox mode unlocked.")],
         effect=lambda w: w.stats.__setitem__("won", 1)),
]


def check(world):
    """Return the pages of the next unseen beat whose condition holds, else None."""
    done = world.story["done"]
    for b in BEATS:
        if b["id"] in done:
            continue
        try:
            ok = b["cond"](world)
        except Exception:
            ok = False
        if ok:
            done.append(b["id"])
            if "effect" in b:
                b["effect"](world)
            return b["pages"]
    return None


def speaker_color(world, who):
    if who == "ECHO":
        return ECHO
    if who == "Jason":
        return JASON
    s = _crew(world, who)
    return s.color if s else JASON
