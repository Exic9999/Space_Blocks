"""Block-built mini ships: stats, editing rules and cached sprites."""
import math
import random
import pygame
from config import BLOCKS, GRID_W, GRID_H, BUILDABLE


def cells(grid):
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if ch != ".":
                yield x, y, ch


def count(grid):
    return sum(1 for _ in cells(grid))


def stats(grid):
    n = {c: 0 for c in "KHELTCS"}
    for _, _, ch in cells(grid):
        n[ch] += 1
    mass = sum(n.values())
    return dict(
        n=n, mass=mass,
        speed=max(90.0, 150 + 55 * n["E"] - 4 * mass),
        fuel_max=15 + 12 * n["T"],
        cargo=6 + 10 * n["C"],
        mine=0.6 + 0.8 * n["L"],
        hull=20 + 8 * n["H"] + 5 * n["K"],
        shield=n["S"],
        burn=0.5 + 0.2 * n["E"],
    )


def block_cost(grid, ch):
    return round(BLOCKS[ch][1] * (1 + 0.12 * max(0, count(grid) - 8)))


def neighbors(x, y):
    return ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))


def can_place(grid, x, y, maxblocks):
    if not (0 <= x < GRID_W and 0 <= y < GRID_H):
        return False
    if grid[y][x] != "." or count(grid) >= maxblocks:
        return False
    return any(0 <= nx < GRID_W and 0 <= ny < GRID_H and grid[ny][nx] != "."
               for nx, ny in neighbors(x, y))


def place(grid, x, y, ch):
    g = [list(r) for r in grid]
    g[y][x] = ch
    return ["".join(r) for r in g]


def candidate_cells(grid, ch, maxblocks, rng):
    """Valid cells for ch, best first (engines aft, lasers fore, rest near the spine)."""
    ys = [y for _, y, _ in cells(grid)]
    out = []
    for y in range(GRID_H):
        for x in range(GRID_W):
            if not can_place(grid, x, y, maxblocks):
                continue
            if ch == "E":
                s = y * 2 - abs(x - 3)
            elif ch == "L":
                s = -y * 2 - abs(x - 3)
            else:
                s = -abs(x - 3) * 1.5 - abs(y - sum(ys) / len(ys))
            out.append((s + rng.random() * 0.8, x, y))
    out.sort(reverse=True)
    return [(x, y) for _, x, y in out]


# ---- sprites -------------------------------------------------------------
_cache = {}


def _shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def draw_block(surf, x, y, ch, cell, tint=None):
    col = BLOCKS[ch][2]
    if tint and ch == "H":
        col = tuple((a + b) // 2 for a, b in zip(col, tint))
    r = pygame.Rect(x * cell, y * cell, cell, cell)
    pygame.draw.rect(surf, _shade(col, 0.55), r)
    pygame.draw.rect(surf, col, r.inflate(-2, -2))
    pygame.draw.line(surf, _shade(col, 1.35), r.topleft, (r.right - 2, r.top))
    pygame.draw.line(surf, _shade(col, 1.35), r.topleft, (r.left, r.bottom - 2))
    cx, cy = r.center
    q = max(1, cell // 4)
    if ch == "E":
        pygame.draw.rect(surf, (60, 40, 30), (cx - q, r.bottom - q - 1, 2 * q, q + 1))
    elif ch == "L":
        pygame.draw.rect(surf, (255, 220, 225), (cx - 1, r.top, 2, cell // 2))
    elif ch == "T":
        pygame.draw.circle(surf, _shade(col, 1.4), (cx, cy), max(2, cell // 3))
    elif ch == "C":
        pygame.draw.line(surf, _shade(col, 0.6), (r.left + 2, cy), (r.right - 3, cy))
    elif ch == "S":
        pygame.draw.circle(surf, (210, 225, 255), (cx, cy), max(2, cell // 3), 1)
    elif ch == "K":
        pygame.draw.ellipse(surf, (20, 50, 80), r.inflate(-cell // 2, -cell // 3))


def sprite(grid, cell=6, tint=None):
    """Upright ship sprite (nose up), cached by layout."""
    key = (tuple(grid), cell, tint)
    s = _cache.get(key)
    if s is None:
        s = pygame.Surface((GRID_W * cell, GRID_H * cell), pygame.SRCALPHA)
        for x, y, ch in cells(grid):
            draw_block(s, x, y, ch, cell, tint)
        bb = s.get_bounding_rect()
        s = s.subsurface(bb).copy()
        _cache[key] = s
    return s


def rotated(grid, ang, cell=6, tint=None, scale=1.0):
    """Sprite rotated to heading ang (radians, 0 = +x). Quantized to 6 degrees."""
    step = round(math.degrees(ang) / 6)
    key = ("r", tuple(grid), cell, tint, step, round(scale, 2))
    s = _cache.get(key)
    if s is None:
        base = sprite(grid, cell, tint)
        if scale != 1.0:
            base = pygame.transform.smoothscale(
                base, (max(2, int(base.get_width() * scale)), max(2, int(base.get_height() * scale))))
        s = pygame.transform.rotate(base, -(step * 6 + 90))
        if len(_cache) > 1500:
            _cache.clear()
        _cache[key] = s
    return s
