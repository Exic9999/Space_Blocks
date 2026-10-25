"""Headless sim test: orders -> launch -> mine -> return -> deposit; autonomy; save round trip."""
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import world as W

w = W.World(seed=42); w.verbose = False
print("islands", len(w.islands), "ships", [s.name for s in w.ships])
disc = [i for i in w.islands if i.discovered]
assert len(disc) >= 2
mira = w.ships[0]
ok, msg = w.give_order(mira, "mine", disc[0].id); assert ok, msg
states = set()
for _ in range(60*120):
    w.update(1/60); states.add(mira.state)
print("states seen:", sorted(states))
assert {"outbound","mining","inbound","docked"} <= states
print("aeth", round(w.aeth), "deposited", round(w.stats["deposited"]), "mira mined", round(mira.mined_total), "fund", round(mira.fund))
assert mira.mined_total > 10
# a pilot with a standing mine order, left alone ~3x patience, still goes independent
import npc_ai
w3 = W.World(seed=5); w3.verbose = False
p = w3.ships[3 if len(w3.ships) > 3 else 0]
w3.give_order(p, "mine", [i for i in w3.islands if i.discovered][0].id)
for _ in range(int(60 * npc_ai.patience(w3, p) * 3.2)): w3.update(1/60)
assert p.independent, "standing order must not buy infinite loyalty"
print("standing-order pilot went independent OK")
# idle crew go independent
for _ in range(60*400): w.update(1/60)
print("independent:", [(s.name, s.independent) for s in w.ships], "launches", w.stats["launches"])
assert any(s.independent for s in w.ships)
# save round trip
def snap(x):
    d = json.loads(json.dumps(x.to_dict()))
    for sh in d["ships"]: sh.pop("think_t")
    return d
d = snap(w)
w2 = W.World.from_dict(json.loads(json.dumps(w.to_dict())))
d2 = snap(w2)
assert d == d2, "save round trip mismatch"
for _ in range(600): w2.update(1/60)
print("round trip OK; parts built:", w.stats["parts_built"], "found:", w.stats["found"], "crew:", len(w.ships))
print("ALL OK")
