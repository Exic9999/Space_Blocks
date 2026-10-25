"""Save slots with atomic writes. Slots: 1-3 plus 'auto'."""
import json
import os
import time
from config import SAVE_DIR
from world import World

SLOTS = ["auto", "1", "2", "3"]


def path(slot):
    return os.path.join(SAVE_DIR, f"slot_{slot}.json")


def save(world, slot, extra=None):
    os.makedirs(SAVE_DIR, exist_ok=True)
    data = dict(version=1, saved_at=time.time(), world=world.to_dict(), extra=extra or {})
    tmp = path(slot) + ".tmp"
    with open(tmp, "w") as f:
        json.dump(data, f)
    os.replace(tmp, path(slot))    # atomic: a crash can never leave a half-written save
    return path(slot)


def load(slot):
    with open(path(slot)) as f:
        data = json.load(f)
    return World.from_dict(data["world"]), data.get("extra", {})


def info(slot):
    """Metadata for the load screen, or None if empty/corrupt."""
    p = path(slot)
    if not os.path.exists(p):
        return None
    try:
        with open(p) as f:
            data = json.load(f)
        w = data["world"]
        return dict(slot=slot, saved_at=data["saved_at"], time=w["time"],
                    aeth=w["aeth"], crew=len(w["ships"]),
                    deposited=w["stats"].get("deposited", 0),
                    chapter=len(w["story"]["done"]))
    except Exception:
        return None


def latest():
    best = None
    for s in SLOTS:
        i = info(s)
        if i and (best is None or i["saved_at"] > best["saved_at"]):
            best = i
    return best["slot"] if best else None
