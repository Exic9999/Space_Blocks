"""Long headless soak: 90 simulated minutes, crew on AUTO, story beats firing."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import world as W, story
w = W.World(seed=7); w.verbose = False
for s in w.ships: w.give_order(s, "auto")
beats = []
for step in range(60*60*90):
    w.update(1/60)
    if step % 60 == 0:
        p = story.check(w)
        if p: beats.append((int(w.time), p[0][0]))
        for s in w.ships:
            if s.order is None and not s.independent: w.give_order(s, "auto")
        if step % (60*300) == 0:
            for k in W.UPGRADES:
                w.buy_upgrade(k)
print("beats:", beats)
print("deposited", int(w.stats["deposited"]), "crew", len(w.ships), "parts", w.stats["parts_built"],
      "found", w.stats["found"], "upgrades", w.stats["upgrades"], "won", w.stats.get("won"))
print([ (s.name, round(s.mined_total), len(''.join(s.grid).replace('.',''))) for s in w.ships])
