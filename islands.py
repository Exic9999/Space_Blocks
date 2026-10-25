"""Procedural floating islands, deep-space backdrop and parallax star layers.

Stylized "blocky / voxel-ish" pixel art: everything for an island is composed on
a coarse block grid with numpy and scaled up nearest-neighbour, so every
element (rock, grass, trees, water, crystals) shares the same chunky pixels.

Public API (used by the rest of the game):
    render_island(seed, width=360) -> (surface, meta)
    draw_island_fx(dest, topleft, meta, t)
    render_backdrop(size, seed=0) -> Surface
    make_star_layers(size, seed=0, n=3) -> [Surface, ...]

All randomness is local (numpy Generator / random.Random seeded from the
arguments), so results are deterministic and never disturb global RNG state.
No display calls (convert / set_mode) are made, so it is safe to call before a
window exists.
"""
from __future__ import annotations

import math
import random

import numpy as np
import pygame

__all__ = ["render_island", "draw_island_fx", "render_backdrop", "make_star_layers"]


# --------------------------------------------------------------------------
# small helpers
# --------------------------------------------------------------------------
def _lerp(a, b, t):
    return a + (b - a) * t


def _mix(c1, c2, t):
    return tuple(int(round(_lerp(c1[i], c2[i], t))) for i in range(3))


def _scale(c, f):
    return tuple(max(0, min(255, int(round(v * f)))) for v in c)


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _value_noise(rng, h, w, cell, tile=False):
    """Smooth value noise in [0,1], shape (h, w), feature size ~cell px."""
    gh = int(math.ceil(h / cell)) + 2
    gw = int(math.ceil(w / cell)) + 2
    g = rng.random((gh, gw)).astype(np.float32)
    ys = np.arange(h, dtype=np.float32) / cell
    xs = np.arange(w, dtype=np.float32) / cell
    y0 = ys.astype(np.int32)
    x0 = xs.astype(np.int32)
    fy = ys - y0
    fx = xs - x0
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0]
    b = g[y0][:, x0 + 1]
    c = g[y0 + 1][:, x0]
    d = g[y0 + 1][:, x0 + 1]
    fx = fx[None, :]
    fy = fy[:, None]
    top = a + (b - a) * fx
    bot = c + (d - c) * fx
    return top + (bot - top) * fy


def _fbm(rng, h, w, cell, octaves=4, gain=0.5):
    total = np.zeros((h, w), np.float32)
    amp = 1.0
    norm = 0.0
    for _ in range(octaves):
        total += _value_noise(rng, h, w, max(cell, 1.0)) * amp
        norm += amp
        amp *= gain
        cell /= 2.0
    return total / norm


def _noise1d(rng, n, cell):
    return _value_noise(rng, 1, n, cell)[0]


def _box_blur(a, r):
    """Cheap separable box blur (float array)."""
    if r <= 0:
        return a
    k = 2 * r + 1
    p = np.pad(a, r, mode="constant")
    c = np.cumsum(p, axis=0)
    c = np.vstack([np.zeros((1, c.shape[1]), c.dtype), c])
    a1 = (c[k:] - c[:-k]) / k
    c = np.cumsum(a1, axis=1)
    c = np.hstack([np.zeros((c.shape[0], 1), c.dtype), c])
    return (c[:, k:] - c[:, :-k]) / k


def _arr_to_surface(img):
    h, w = img.shape[:2]
    return pygame.image.frombuffer(np.ascontiguousarray(img).tobytes(), (w, h), "RGBA").copy()


def _hsv(h, s, v):
    import colorsys
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, s, v)
    return (int(r * 255), int(g * 255), int(b * 255))


# --------------------------------------------------------------------------
# block-grid canvas with drawing primitives (all in grid units)
# --------------------------------------------------------------------------
class _Canvas:
    def __init__(self, w, h, rng):
        self.w, self.h = w, h
        self.img = np.zeros((h, w, 4), np.uint8)
        self.jit = rng.random((h, w)).astype(np.float32)  # per-block dither
        self.yy, self.xx = np.mgrid[0:h, 0:w].astype(np.float32)

    def _box(self, x0, y0, x1, y1):
        x0 = max(0, int(math.floor(x0)))
        y0 = max(0, int(math.floor(y0)))
        x1 = min(self.w, int(math.ceil(x1)) + 1)
        y1 = min(self.h, int(math.ceil(y1)) + 1)
        if x0 >= x1 or y0 >= y1:
            return None
        return slice(y0, y1), slice(x0, x1)

    def put(self, x, y, col, a=255):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img[y, x, :3] = col
            self.img[y, x, 3] = a

    def write(self, sl, mask, cols):
        """cols: (3,) or (...,3) array broadcastable to the slice."""
        sub = self.img[sl]
        if np.ndim(cols) == 1:
            sub[mask, :3] = cols
        else:
            sub[mask, :3] = cols[mask]
        sub[mask, 3] = 255

    def shade_blob(self, cx, cy, rx, ry, pal, rough=0.9, light=(-0.6, -0.8)):
        """Shaded ellipse blob. pal: (4,3) uint8 [shadow, mid, light, highlight]."""
        b = self._box(cx - rx - 1, cy - ry - 1, cx + rx + 1, cy + ry + 1)
        if b is None:
            return
        yy = self.yy[b] - cy
        xx = self.xx[b] - cx
        j = self.jit[b]
        d = (xx / max(rx, 0.5)) ** 2 + (yy / max(ry, 0.5)) ** 2
        m = d <= (1.0 + (j - 0.5) * rough / max(min(rx, ry), 1.0)) ** 2
        dot = (xx / max(rx, 0.5)) * light[0] + (yy / max(ry, 0.5)) * light[1]
        dot = dot + (j - 0.5) * 0.45
        idx = np.ones(dot.shape, np.int8)
        idx[dot > 0.25] = 2
        idx[(dot > 0.62) & (d < 0.75)] = 3
        idx[dot < -0.3] = 0
        cols = np.asarray(pal, np.uint8)[idx]
        self.write(b, m, cols)

    def tri(self, cx, ytop, ybot, half, pal):
        """Shaded upward triangle (pine tier)."""
        b = self._box(cx - half - 1, ytop, cx + half + 1, ybot)
        if b is None:
            return
        yy = self.yy[b]
        xx = self.xx[b] - cx
        j = self.jit[b]
        hw = (yy - ytop + 0.5) / max(ybot - ytop, 1) * half
        m = (np.abs(xx) <= hw + (j - 0.5) * 0.8) & (yy >= ytop) & (yy <= ybot)
        rel = xx / max(half, 1)
        dot = -rel * 0.9 - (yy - ytop) / max(ybot - ytop, 1) * 0.5 + 0.2 + (j - 0.5) * 0.4
        idx = np.ones(dot.shape, np.int8)
        idx[dot > 0.25] = 2
        idx[dot > 0.6] = 3
        idx[dot < -0.25] = 0
        cols = np.asarray(pal, np.uint8)[idx]
        self.write(b, m, cols)

    def rect(self, x0, y0, x1, y1, col):
        b = self._box(x0, y0, x1 - 1, y1 - 1)
        if b is None:
            return
        self.img[b][..., :3] = col
        self.img[b][..., 3] = 255

    def darken_ellipse(self, cx, cy, rx, ry, f, only=None):
        b = self._box(cx - rx, cy - ry, cx + rx, cy + ry)
        if b is None:
            return
        yy = self.yy[b] - cy
        xx = self.xx[b] - cx
        d = (xx / rx) ** 2 + (yy / ry) ** 2
        sub = self.img[b]
        m = (d <= 1.0) & (sub[..., 3] > 0)
        if only is not None:
            m &= only[b]
        k = (f + (1 - f) * np.clip(d, 0, 1))[..., None]
        sub[..., :3] = np.where(m[..., None], (sub[..., :3] * k).astype(np.uint8), sub[..., :3])


# --------------------------------------------------------------------------
# palettes
# --------------------------------------------------------------------------
def _pal4(base, spread=(0.62, 1.0, 1.25, 1.5), warm=(0, 0, 0)):
    out = []
    for i, f in enumerate(spread):
        c = _scale(base, f)
        if i >= 2:  # warm the lights a touch
            c = tuple(min(255, c[k] + warm[k]) for k in range(3))
        out.append(c)
    return np.array(out, np.uint8)


_GRASS_SETS = [
    # (grass base, alt patch, dirt) by hue region
    ((86, 170, 64), (120, 190, 70), (120, 78, 50)),     # lush green
    ((52, 160, 112), (80, 190, 140), (104, 72, 60)),    # teal meadow
    ((120, 176, 58), (160, 196, 72), (132, 90, 52)),    # spring yellow-green
    ((70, 150, 96), (110, 178, 96), (96, 70, 64)),      # deep forest
]

_ROCK_SETS = [
    # top, mid, bottom of the underside gradient
    ((150, 118, 96), (104, 78, 88), (44, 32, 66)),      # warm sandstone -> violet
    ((128, 120, 132), (86, 80, 108), (34, 30, 62)),     # slate
    ((150, 104, 84), (110, 66, 74), (48, 26, 58)),      # red clay
    ((118, 116, 104), (76, 84, 96), (28, 34, 58)),      # mossy grey
]

_TREE_GREENS = [
    (58, 140, 60), (40, 120, 72), (84, 160, 56), (36, 130, 116),
    (60, 110, 64), (96, 170, 84), (30, 104, 96), (70, 150, 110),
]
_AUTUMN = [(226, 120, 160), (240, 170, 70), (214, 92, 120), (236, 196, 88), (200, 110, 200)]


# --------------------------------------------------------------------------
# island
# --------------------------------------------------------------------------
def render_island(seed: int, width: int = 360) -> tuple[pygame.Surface, dict]:
    """Deterministic for a given seed. Returns (surface, meta).

    Surface is SRCALPHA, width x int(width*0.85), transparent background.
    meta = {"waterfalls": [(x, y, length)], "crystals": [(x, y, radius)],
            "landing": (x, y), "radius": float, "hue": float}
    (plus a few private "_..." keys used by draw_island_fx).
    """
    width = int(width)
    height = int(width * 0.85)
    # fixed block grid -> same island at every width (scaled nearest-neighbour)
    GW = 120
    GH = int(math.ceil(GW * 0.85))
    kx = width / GW
    ky = height / GH
    px = max(1, int(round(kx)))

    rng = np.random.default_rng([int(seed) & 0xFFFFFFFF, 0x15A1])
    rr = random.Random(int(seed) * 7919 + 17)
    cv = _Canvas(GW, GH, rng)
    img = cv.img
    YY, XX = cv.yy, cv.xx

    hue = rr.random()
    gset = _GRASS_SETS[int(hue * len(_GRASS_SETS)) % len(_GRASS_SETS)]
    rset = _ROCK_SETS[rr.randrange(len(_ROCK_SETS))]
    vein_col = rr.choice([(80, 255, 200), (110, 240, 255), (200, 110, 255), (255, 120, 220), (170, 130, 255)])
    crystal_hue = 0.62 + rr.random() * 0.14  # blue .. violet

    # ---------------- plateau (3/4 view ellipse) ----------------
    cx = GW * (0.5 + rr.uniform(-0.03, 0.03))
    rx = GW * rr.uniform(0.36, 0.43)
    ry = GH * rr.uniform(0.09, 0.125)
    cy = GH * 0.31 + rr.uniform(-1.5, 1.5)
    ang = np.arctan2((YY - cy) / ry, (XX - cx) / rx)
    # angular boundary noise (periodic): sum of a few sines
    bn = np.zeros_like(ang)
    for k in range(2, 7):
        bn += math.cos(rr.uniform(0, 6.28)) * np.sin(k * ang + rr.uniform(0, 6.28)) * (0.10 / k ** 0.6)
    ed = ((XX - cx) / rx) ** 2 + ((YY - cy) / ry) ** 2
    plateau = ed <= (1.0 + bn) ** 2
    cols_any = plateau.any(axis=0)
    xs_idx = np.arange(GW)
    yfront = np.full(GW, -1, np.int32)
    yback = np.full(GW, GH, np.int32)
    for x in xs_idx[cols_any]:
        ys = np.nonzero(plateau[:, x])[0]
        yfront[x] = ys.max()
        yback[x] = ys.min()
    xl = int(xs_idx[cols_any].min())
    xr = int(xs_idx[cols_any].max())

    # ---------------- underside profile ----------------
    depth_total = GH * rr.uniform(0.44, 0.54)
    u = (xs_idx - cx) / rx  # -1..1 across plateau
    base = np.clip(1 - np.abs(u) ** 2, 0, 1) ** 0.7 * rr.uniform(0.18, 0.3)
    shape = base.copy()
    n_lobes = rr.choice([1, 2, 2, 3, 3])
    lobes = []
    for i in range(n_lobes):
        c = rr.uniform(-0.4, 0.4) if n_lobes > 1 else rr.uniform(-0.15, 0.15)
        w = rr.uniform(0.45, 0.8) if i == 0 else rr.uniform(0.3, 0.55)
        a = rr.uniform(0.85, 1.0) if i == 0 else rr.uniform(0.35, 0.75)
        p = rr.uniform(1.4, 2.2)
        lobes.append((c, w, a))
        shape = np.maximum(shape, a * np.clip(1 - np.abs((u - c) / w), 0, 1) ** p)
    jag = (_noise1d(rng, GW, 3.0) - 0.5) * 4 + (_noise1d(rng, GW, 1.2) - 0.5) * 2.0
    bottom = yfront + 2 + shape * depth_total + jag * np.clip(shape * 3, 0, 1)
    # stalactites
    n_sp = rr.randint(5, 10)
    for _ in range(n_sp):
        x0 = rr.uniform(xl + 3, xr - 3)
        hw = rr.choice([1, 1, 1.5, 2, 2.5])
        ln = rr.uniform(3, 11) * (0.5 + float(np.interp(x0, xs_idx, shape)))
        for x in range(int(x0 - hw - 1), int(x0 + hw + 2)):
            if 0 <= x < GW and cols_any[x]:
                k = 1 - abs(x - x0) / (hw + 1)
                if k > 0:
                    bottom[x] = max(bottom[x], bottom[x] + ln * k ** 1.4 - 1)
    bottom = np.where(cols_any, np.minimum(bottom, GH * 0.9), -1).astype(np.int32)
    rock = (YY >= (yfront[None, :] - 1)) & (YY <= bottom[None, :]) & cols_any[None, :] & ~plateau
    rock |= (YY >= yfront[None, :]) & (YY <= bottom[None, :]) & cols_any[None, :]

    # floating debris rocks under / beside the island
    debris = []
    for _ in range(rr.randint(2, 4)):
        side = rr.choice([-1, 1])
        dx = cx + side * rr.uniform(0.35, 0.95) * rx
        dy = cy + ry + depth_total * rr.uniform(0.35, 0.95)
        drx = rr.uniform(1.8, 4.0)
        debris.append((dx, dy, drx))

    # ---------------- rock shading ----------------
    yf2 = yfront[None, :].astype(np.float32)
    span = np.maximum(bottom[None, :] - yf2, 1)
    f = np.clip((YY - yf2) / span, 0, 1)
    top_c = np.array(rset[0], np.float32)
    mid_c = np.array(rset[1], np.float32)
    bot_c = np.array(rset[2], np.float32)
    f3 = f[..., None]
    rc = np.where(f3 < 0.5, top_c + (mid_c - top_c) * (f3 / 0.5), mid_c + (bot_c - mid_c) * ((f3 - 0.5) / 0.5))
    # voxel facets: quantized coarse noise
    facet = _value_noise(rng, GH, GW, 4.0)
    facet = np.floor(facet * 4) / 4
    strata_n = _value_noise(rng, GH, GW, 7.0)
    band = (np.floor((YY + strata_n * 4.0) / 3.0) % 3)
    # ledge highlight: top row of each band catches light
    band_up = np.floor((YY - 1 + strata_n * 4.0) / 3.0) % 3
    ledge = band != band_up
    mul = 0.8 + facet * 0.4
    mul *= np.where(band == 0, 1.12, np.where(band == 1, 0.95, 0.8))
    mul *= np.where(ledge, 1.12, 1.0)
    lat = np.clip((XX - cx) / rx, -1.2, 1.2)
    mul *= 1.08 - 0.22 * (lat + 1) / 2
    mul *= 0.9 + 0.2 * cv.jit
    rc = rc * mul[..., None]

    # silhouette edges: rim light left, shadow right/bottom
    def nb_empty(mask, dy, dx):
        s = np.zeros_like(mask)
        ys = slice(max(0, dy), GH + min(0, dy))
        yd = slice(max(0, -dy), GH + min(0, -dy))
        xs_ = slice(max(0, dx), GW + min(0, dx))
        xd = slice(max(0, -dx), GW + min(0, -dx))
        s[yd, xd] = mask[ys, xs_]
        return mask & ~s

    left_e = nb_empty(rock, 0, -1)
    right_e = nb_empty(rock, 0, 1) | nb_empty(rock, 1, 0)
    right_e2 = nb_empty(rock, 0, 2) | nb_empty(rock, 2, 0)
    rc[right_e2] *= 0.82
    rc[right_e] *= 0.75
    rc[left_e] = rc[left_e] * 1.15 + np.array([14, 10, 18], np.float32)
    bot_e = nb_empty(rock, 1, 0) & (XX < cx + rx * 0.3)
    rc[bot_e] = rc[bot_e] * 0.8 + np.array(vein_col, np.float32) * 0.35  # nebula bounce rim

    # glowing veins: branching crack random-walks through the rock
    vein = np.zeros((GH, GW), bool)
    rock_cols = xs_idx[cols_any & (bottom - yfront > 10)]
    n_cracks = rr.randint(4, 8)
    for _ in range(n_cracks if len(rock_cols) else 0):
        x = float(rr.choice(list(rock_cols)))
        xi = int(x)
        y = yfront[xi] + 6 + rr.uniform(0, (bottom[xi] - yfront[xi]) * 0.35)
        dx = rr.choice([-1, 1]) * rr.uniform(0.7, 1.0)
        stack = [(x, y, dx, rr.randint(10, 22))]
        while stack:
            x, y, dx, ln = stack.pop()
            for _s in range(ln):
                xi, yi = int(round(x)), int(round(y))
                if not (0 <= xi < GW and 0 <= yi < GH) or not rock[yi, xi]:
                    break
                vein[yi, xi] = True
                if rr.random() < 0.58:
                    x += dx
                else:
                    y += 1
                if rr.random() < 0.2:
                    dx = -dx * rr.uniform(0.8, 1.1)
                if rr.random() < 0.06 and len(stack) < 6:
                    stack.append((x, y, -dx, int(ln * 0.5)))
    vein &= ~right_e & ~left_e
    vglow = _box_blur(vein.astype(np.float32), 1) * rock
    vglow2 = _box_blur(vein.astype(np.float32), 3) * rock
    vc = np.array(vein_col, np.float32)
    rc += (vglow * 0.55 + vglow2 * 0.6)[..., None] * vc
    rc[vein] = vc * 0.7 + 75
    rc = np.clip(rc, 0, 255)
    img[rock, :3] = rc[rock].astype(np.uint8)
    img[rock, 3] = 255

    # debris
    for (dx, dy, drx) in debris:
        h = drx * rr.uniform(1.6, 2.4)
        skew = rr.uniform(-0.4, 0.4)
        has_grass = rr.random() < 0.7
        for k in range(int(h) + 1):
            tt = k / max(h, 1)
            half = drx * (1 - tt) ** 0.9 + rr.uniform(-0.4, 0.4)
            ccx = dx + skew * k
            col_k = _mix(rset[0], rset[2], min(1.0, tt * 1.2))
            for x in range(int(round(ccx - half)), int(round(ccx + half)) + 1):
                rel = (x - (ccx - half)) / max(2 * half, 1)
                c = _scale(col_k, 1.15 - 0.4 * rel)
                if k == 0 and has_grass:
                    c = _scale(gset[0], 1.1)
                elif k == 1 and has_grass:
                    c = _scale(gset[0], 0.7)
                cv.put(x, dy + k, c)

    # ---------------- grass top ----------------
    gbase = np.array(gset[0], np.float32)
    galt = np.array(gset[1], np.float32)
    gp = _value_noise(rng, GH, GW, 4.0)
    patch = (gp > 0.6)[..., None]
    gc = np.where(patch, galt, gbase)
    depthy = np.clip((YY - cy + ry) / (2 * ry), 0, 1)  # 0 back .. 1 front
    gc = gc * (1.12 - 0.22 * depthy[..., None]) * (0.92 + 0.14 * cv.jit[..., None])
    back_e = nb_empty(plateau, -1, 0)
    gc[back_e] = gc[back_e] * 1.2 + 18
    gc = np.clip(gc, 0, 255)
    img[plateau, :3] = gc[plateau].astype(np.uint8)
    img[plateau, 3] = 255

    # front lip: grass side, dirt band, drips & vines
    gside = np.array(_scale(gset[0], 0.72), np.uint8)
    gside2 = np.array(_scale(gset[0], 0.56), np.uint8)
    dirt = np.array(gset[2], np.uint8)
    dirt2 = np.array(_scale(gset[2], 0.75), np.uint8)
    drip_n = _noise1d(rng, GW, 1.6)
    for x in range(xl, xr + 1):
        yf = yfront[x]
        if yf < 0:
            continue
        cv.put(x, yf + 1, gside)
        d = 1 + int(drip_n[x] * 3.2)
        for k in range(2, 2 + d):
            if yf + k <= bottom[x]:
                cv.put(x, yf + k, gside2 if k > 2 else gside)
        for k in range(2 + d, 4 + d):
            if yf + k <= bottom[x]:
                cv.put(x, yf + k, dirt if k == 2 + d else dirt2)
    # AO band under dirt
    for x in range(xl, xr + 1):
        yf = yfront[x]
        for k in range(5, 8):
            y = yf + k
            if 0 <= y < GH and rock[y, x] and not vein[y, x]:
                img[y, x, :3] = (img[y, x, :3] * (0.72 + 0.09 * (k - 5))).astype(np.uint8)
    # hanging vines and roots over the cliff face
    vine_d = _scale(gset[0], 0.5)
    vine_m = _scale(gset[0], 0.8)
    for _ in range(int((xr - xl) * rr.uniform(0.12, 0.22))):
        x = rr.randint(xl + 2, xr - 2)
        y0 = yfront[x] + 3
        ln = rr.randint(3, 12)
        root = rr.random() < 0.3
        for k in range(ln):
            y = y0 + k
            if y > bottom[x] - 1:
                break
            if root:
                cv.put(x, y, (70, 48, 40) if k % 3 else (96, 66, 50))
            else:
                cv.put(x, y, vine_d if k % 2 else vine_m)
                if rr.random() < 0.3:
                    cv.put(x + rr.choice([-1, 1]), y, vine_m)
        if not root and rr.random() < 0.4:
            cv.put(x, min(y0 + ln, bottom[x] - 1), rr.choice([(255, 150, 190), (255, 236, 120), (190, 160, 255)]))

    # ---------------- water: rivers + waterfalls ----------------
    n_wf = rr.choice([1, 1, 2, 2, 2, 3])
    wf_list = []
    used = []
    tries = 0
    while len(wf_list) < n_wf and tries < 60:
        tries += 1
        if len(wf_list) == 0 and n_wf >= 2 and rr.random() < 0.6:
            uu = rr.choice([-1, 1]) * rr.uniform(0.72, 0.86)
        else:
            uu = rr.uniform(-0.8, 0.8)
        wx = int(round(cx + uu * rx))
        if wx < xl + 2 or wx > xr - 3 or any(abs(wx - o) < 10 for o in used):
            continue
        used.append(wx)
        ww = rr.choice([3, 3, 4, 4, 5]) if abs(uu) < 0.7 else rr.choice([2, 3, 3])
        wf_list.append((wx, ww))
    water_mask = np.zeros((GH, GW), bool)
    wcol = np.array([70, 150, 226], np.uint8)
    wcol2 = np.array([120, 200, 245], np.uint8)
    wdeep = np.array([44, 104, 190], np.uint8)
    ponds = []
    for (wx, ww) in wf_list:
        # source pond somewhere on plateau behind the fall
        sx = cx + (wx - cx) * rr.uniform(0.1, 0.5) + rr.uniform(-6, 6)
        sy = cy - ry * rr.uniform(0.0, 0.5)
        ponds.append((sx, sy))
        # river meander from source to lip
        ex, ey = wx + ww / 2.0 - 0.5, float(yfront[wx])
        steps = int(max(abs(ex - sx), abs(ey - sy)) * 1.5) + 2
        ph = rr.uniform(0, 6.28)
        for i in range(steps + 1):
            t = i / steps
            x = _lerp(sx, ex, t) + math.sin(t * 5 + ph) * 2.0 * (1 - t) * t * 4
            y = _lerp(sy, ey, t)
            for oy in range(-1, 1):
                for ox in range(int(-ww / 2), int(ww / 2) + 1):
                    xi, yi = int(round(x + ox)), int(round(y + oy))
                    if 0 <= xi < GW and 0 <= yi < GH and plateau[yi, xi]:
                        water_mask[yi, xi] = True
        cv.shade_blob(sx, sy, rr.uniform(3, 5), rr.uniform(1.6, 2.4),
                      np.array([wdeep, wcol, wcol2, (200, 236, 255)], np.uint8), rough=0.6, light=(-0.2, 0.9))
    img[water_mask, :3] = np.where((cv.jit[water_mask] > 0.75)[:, None], wcol2, wcol)
    img[water_mask, 3] = 255
    sand = np.array(_scale(gset[0], 1.3), np.uint8)
    edge_w = (_box_blur(water_mask.astype(np.float32), 1) > 0.05) & ~water_mask & plateau
    img[edge_w, :3] = np.minimum(255, img[edge_w, :3].astype(np.int32) * 1.15).astype(np.uint8)
    del sand

    # ---------------- crystals (positions) ----------------
    n_cr = rr.randint(2, 6)
    crystals = []  # (gx, gy, size, kind) kind: 'top' or 'rock'
    tries = 0
    while len(crystals) < n_cr and tries < 200:
        tries += 1
        if rr.random() < 0.55:
            gx = rr.uniform(cx - rx * 0.8, cx + rx * 0.8)
            gy = rr.uniform(cy - ry * 0.6, cy + ry * 0.85)
            gxi, gyi = int(gx), int(gy)
            if not (0 <= gyi < GH and plateau[gyi, gxi]) or water_mask[max(0, gyi - 2):gyi + 3, max(0, gxi - 2):gxi + 3].any():
                continue
            kind = "top"
            base_ang = 0.0
        else:
            gyi = int(cy + ry + rr.uniform(4, depth_total * 0.6))
            if not (0 <= gyi < GH):
                continue
            row = np.nonzero(rock[gyi])[0]
            if len(row) < 6:
                continue
            side = rr.choice([-1, 1])
            gx = float(row.min() + 1) if side < 0 else float(row.max() - 1)
            gy = float(gyi)
            kind = "rock"
            base_ang = side * rr.uniform(0.75, 1.25)
        if any(math.hypot(gx - c[0], gy - c[1]) < 9 for c in crystals):
            continue
        crystals.append((gx, gy, rr.uniform(3.6, 5.4), kind, base_ang))
    while len(crystals) < 2:  # guarantee
        crystals.append((cx + (len(crystals) * 2 - 1) * rx * 0.4, cy + ry * 0.3, 4.0, "top", 0.0))

    # landing clearing
    lx, ly = cx + rr.uniform(-0.25, 0.25) * rx, cy + rr.uniform(-0.1, 0.35) * ry
    for _ in range(30):
        if not water_mask[max(0, int(ly) - 2):int(ly) + 3, max(0, int(lx) - 3):int(lx) + 4].any():
            break
        lx, ly = cx + rr.uniform(-0.4, 0.4) * rx, cy + rr.uniform(-0.2, 0.4) * ry

    # ---------------- vegetation placement ----------------
    tree_w = {
        "round": rr.uniform(0.6, 1.4), "pine": rr.uniform(0.2, 1.4), "cypress": rr.uniform(0.2, 1.0),
        "blossom": rr.uniform(0.15, 0.7), "willow": rr.uniform(0.0, 0.8), "shroom": rr.uniform(0.0, 0.25) * (rr.random() < 0.45),
    }
    kinds = list(tree_w)
    weights = [tree_w[k] for k in kinds]
    greens = rr.sample(_TREE_GREENS, 3)
    autumn = rr.sample(_AUTUMN, 2)
    items = []
    n_trees = int(rx * ry * rr.uniform(0.07, 0.1))
    occupied = []
    tries = 0
    while len(items) < n_trees and tries < n_trees * 12:
        tries += 1
        tx = rr.uniform(xl + 1, xr - 1)
        ty = rr.uniform(cy - ry * 1.05, cy + ry * 1.05)
        txi, tyi = int(tx), int(ty)
        if not (0 <= tyi < GH and 0 <= txi < GW and plateau[tyi, txi]):
            continue
        if water_mask[max(0, tyi - 1):tyi + 2, max(0, txi - 1):txi + 2].any():
            continue
        if math.hypot((tx - lx) / 1.0, (ty - ly) * 1.8) < 6.5:
            continue
        if any(c[3] == "top" and (math.hypot(tx - c[0], (ty - c[1]) * 1.5) < 4.5 or
                                  (abs(tx - c[0]) < c[2] + 3 and 0 < ty - c[1] < 13)) for c in crystals):
            continue  # keep crystals visible: nothing tall right in front of them
        if abs(tx - lx) < 6 and 0 < ty - ly < 12:
            continue  # same for the landing clearing
        if any(math.hypot(tx - ox, (ty - oy) * 1.8) < 2.6 for ox, oy in occupied):
            continue
        occupied.append((tx, ty))
        kind = rr.choices(kinds, weights)[0]
        items.append(("tree", ty, tx, kind, rr.random(), rr.random(), rr.random()))
    # bushes / flowers
    for _ in range(int(rx * 1.1)):
        tx = rr.uniform(xl, xr)
        ty = rr.uniform(cy - ry, cy + ry * 1.1)
        txi, tyi = int(tx), int(ty)
        if 0 <= tyi < GH and 0 <= txi < GW and plateau[tyi, txi] and not water_mask[tyi, txi]:
            if math.hypot(tx - lx, (ty - ly) * 1.8) < 4:
                continue
            if any(c[3] == "top" and abs(tx - c[0]) < c[2] + 1 and -1 < ty - c[1] < 4 for c in crystals):
                continue
            items.append(("bush", ty, tx, rr.random(), rr.random()))
    for c in crystals:
        if c[3] == "top":
            items.append(("crystal", c[1], c[0], c[2]))
    items.sort(key=lambda it: it[1])

    # flowers sprinkled directly into grass
    flower_cols = [(255, 236, 120), (255, 150, 190), (240, 240, 255), (180, 150, 255), (255, 180, 90)]
    fl_a = rr.sample(flower_cols, 3)
    for _ in range(int(rx * ry * 0.35)):
        x = int(rr.uniform(xl, xr))
        y = int(rr.uniform(cy - ry, cy + ry))
        if plateau[y, x] and not water_mask[y, x] and not back_e[y, x]:
            cv.put(x, y, rr.choice(fl_a))

    # shadows under trees (ambient occlusion on grass)
    for it in items:
        if it[0] == "tree":
            cv.darken_ellipse(it[2] + 0.8, it[1] + 0.3, 3.4, 1.4, 0.62, only=plateau)

    def leaf_pal(c, haze):
        c = _mix(c, (120, 150, 190), haze)
        return _pal4(c, (0.55, 0.85, 1.12, 1.38), warm=(18, 14, 0))

    crystal_draw = []

    def draw_crystal(gx, gy, size, rrc, base_ang=0.0):
        """Cluster of shards; base at (gx, gy) pointing up (or sideways if on rock)."""
        base_c = _hsv(crystal_hue + rrc.uniform(-0.03, 0.03), 0.55, 0.95)
        dark = _hsv(crystal_hue + 0.04, 0.75, 0.55)
        light = _hsv(crystal_hue - 0.04, 0.25, 1.0)
        core = (235, 240, 255)
        n = rrc.randint(3, 5)
        shards = []
        for i in range(n):
            a = base_ang + (rrc.uniform(-0.6, 0.6) if i else rrc.uniform(-0.12, 0.12))
            ln = size * (1.6 if i == 0 else rrc.uniform(0.7, 1.2))
            w = max(1.0, size * (0.42 if i == 0 else rrc.uniform(0.25, 0.38)))
            ox = rrc.uniform(-size * 0.5, size * 0.5) if i else 0
            shards.append((a, ln, w, ox))
        shards.sort(key=lambda s: -abs(s[0] - base_ang))
        pdx, pdy = math.cos(base_ang), math.sin(base_ang)
        for (a, ln, w, ox) in shards:
            dxu, dyu = math.sin(a), -math.cos(a)
            steps = int(ln * 1.6) + 1
            for s in range(steps + 1):
                tt = s / steps
                ww = w * (1.0 if tt < 0.65 else max(0.0, 1 - (tt - 0.65) / 0.35))
                bx = gx + ox * pdx + dxu * ln * tt
                by = gy + ox * pdy + dyu * ln * tt
                kk = -ww
                while kk <= ww + 0.01:
                    qx = bx + kk * math.cos(a)
                    qy = by + kk * math.sin(a)
                    col = light if kk < -0.25 else (base_c if kk < 0.6 else dark)
                    if abs(kk) < 0.6 and tt > 0.72:
                        col = core
                    cv.put(round(qx), round(qy), col)
                    kk += 0.5
        crystal_draw.append((gx + math.sin(base_ang) * size * 0.9, gy - math.cos(base_ang) * size * 0.9, size))

    rrc = random.Random(seed * 31 + 5)
    for it in items:
        kind0 = it[0]
        if kind0 == "crystal":
            draw_crystal(it[2], it[1], it[3], rrc)
            continue
        if kind0 == "bush":
            _, ty, tx, r1, r2 = it
            c = greens[int(r1 * 3)] if r2 > 0.25 else autumn[int(r1 * 2)]
            haze = max(0.0, (cy - ty) / ry) * 0.12
            pal = leaf_pal(c, haze)
            br = 1.2 + r2 * 1.3
            cv.shade_blob(tx, ty - br * 0.5, br, br * 0.8, pal, rough=1.0)
            if r1 > 0.6:
                cv.put(tx + (r2 - 0.5) * 2, ty - br, rr.choice(fl_a))
            continue
        _, ty, tx, kind, r1, r2, r3 = it
        haze = max(0.0, (cy - ty) / ry) * 0.18  # back rows hazier
        size = 0.75 + r1 * 0.6
        trunk = _mix((84, 56, 44), (120, 150, 190), haze * 0.5)
        trunk_l = _scale(trunk, 1.35)
        if kind == "round" or kind == "blossom":
            h = (5 + r2 * 4) * size
            c = greens[int(r3 * 3)] if kind == "round" else autumn[int(r3 * 2)]
            pal = leaf_pal(c, haze)
            cv.rect(tx, ty - h, tx + 1, ty + 1, trunk)
            cv.put(tx - 1, ty, trunk)
            R = (2.6 + r1 * 2.2) * (0.9 + 0.2 * r2)
            nb = 3 + int(r2 * 3)
            for k in range(nb):
                a = k / nb * 6.28 + r3 * 3
                cv.shade_blob(tx + math.cos(a) * R * 0.55, ty - h - R * 0.1 + math.sin(a) * R * 0.35,
                              R * 0.72, R * 0.62, pal)
            cv.shade_blob(tx, ty - h - R * 0.35, R * 0.8, R * 0.7, pal)
            if kind == "blossom":
                for k in range(int(R * 2)):
                    cv.put(tx + rr.uniform(-R, R) * 0.8, ty - h - R * 0.3 + rr.uniform(-R, R) * 0.6,
                           (255, 240, 230) if rr.random() < 0.5 else _scale(c, 1.5))
        elif kind == "pine":
            h = (9 + r2 * 9) * size
            pal = leaf_pal(_mix(greens[int(r3 * 3)], (30, 90, 80), 0.35), haze)
            cv.rect(tx, ty - 3, tx + 1, ty + 1, trunk)
            tiers = 3 + int(r2 * 2.5)
            top = ty - h
            half_max = 2.4 + r1 * 1.8
            for k in range(tiers):
                t0 = top + (h - 2) * (k / tiers) * 0.85
                t1 = top + (h - 2) * ((k + 1.6) / tiers) * 0.85
                cv.tri(tx + 0.5, t0, t1, half_max * (0.45 + 0.55 * (k + 1) / tiers), pal)
        elif kind == "cypress":
            h = (10 + r2 * 8) * size
            pal = leaf_pal(_mix(greens[int(r3 * 3)], (40, 100, 70), 0.3), haze)
            cv.rect(tx, ty - 2, tx + 1, ty + 1, trunk)
            cv.shade_blob(tx + 0.5, ty - h * 0.55, 1.6 + r1 * 0.9, h * 0.48, pal, rough=0.7)
        elif kind == "willow":
            h = (6 + r2 * 4) * size
            c = _mix(greens[int(r3 * 3)], (60, 170, 150), 0.5)
            pal = leaf_pal(c, haze)
            cv.rect(tx, ty - h, tx + 1, ty + 1, trunk)
            R = 3.0 + r1 * 2.0
            cv.shade_blob(tx + 0.5, ty - h, R, R * 0.7, pal)
            for k in range(int(R * 2.2)):
                dx = int(tx + 0.5 - R + k * (2 * R) / (R * 2.2))
                ln = int(R * 0.6 + (math.sin(k * 2.3 + r3 * 9) * 0.5 + 0.5) * h * 0.7)
                col = pal[1] if k % 2 else pal[0]
                for yy in range(int(ty - h), int(ty - h + ln)):
                    cv.put(dx, yy, col)
                cv.put(dx, int(ty - h + ln), pal[2])
        elif kind == "shroom":
            h = (5 + r2 * 4) * size
            stem = _mix((206, 196, 210), (120, 150, 190), haze)
            cv.rect(tx, ty - h, tx + 2, ty + 1, stem)
            cv.rect(tx, ty - h, tx + 1, ty + 1, _scale(stem, 1.08))
            capc = _hsv(rr.choice([0.5, 0.8, 0.9, 0.05]), 0.6, 0.85)
            pal = _pal4(capc, (0.55, 0.85, 1.1, 1.3))
            R = 3.2 + r1 * 2.0
            cv.shade_blob(tx + 1, ty - h, R, R * 0.55, pal)
            cv.rect(tx + 1 - R + 0.5, ty - h + 1, tx + 1 + R, ty - h + 2, pal[0])
            for k in range(3):
                cv.put(tx + 1 + rr.uniform(-R, R) * 0.6, ty - h - rr.uniform(0, R * 0.4), (250, 250, 230))

    # rock-face crystals jut sideways from underside
    for c in crystals:
        if c[3] == "rock":
            draw_crystal(c[0], c[1], c[2] * 0.85, rrc, c[4])

    # ---------------- outline (dark 1-block on silhouette) ----------------
    solid = img[..., 3] > 0
    grow = solid.copy()
    grow[1:, :] |= solid[:-1, :]
    grow[:-1, :] |= solid[1:, :]
    grow[:, 1:] |= solid[:, :-1]
    grow[:, :-1] |= solid[:, 1:]
    outline = grow & ~solid
    img[outline, :3] = (22, 16, 34)
    img[outline, 3] = 255

    # ---------------- waterfalls over the lip / rock face ----------------
    waterfalls = []
    wf_widths = []
    mist_pts = []
    for (wx, ww) in wf_list:
        top = int(yfront[wx])
        bot_rock = int(max(bottom[max(0, wx - 1):wx + ww + 1].max(), top))
        tail = int(rr.uniform(22, 34))
        end = min(GH, bot_rock + tail)
        for y in range(top, end):
            fall_t = (y - top)
            spread = 0 if y <= bot_rock else int((y - bot_rock) / 9)
            for x in range(wx - spread, wx + ww + spread):
                if not (0 <= x < GW):
                    continue
                stripe = ((x * 7 + (y // 3) * 3 + int(cv.jit[y, x] * 2)) % 5)
                if y <= bot_rock:
                    a = 235
                else:
                    a = int(235 * max(0.0, 1 - (y - bot_rock) / tail) ** 1.3)
                    if cv.jit[y, x] < ((y - bot_rock) / tail) ** 1.6 * 0.85:
                        continue
                if y <= bot_rock:
                    col = wcol2 if stripe == 0 else (wcol if stripe < 4 else wdeep)
                else:
                    col = (215, 240, 255) if stripe == 0 else ((150, 205, 245) if stripe < 4 else wcol)
                if fall_t < 2:
                    col = (220, 245, 255)
                if x in (wx - spread, wx + ww + spread - 1) and ww > 2:
                    col = _scale(col, 0.85)
                if img[y, x, 3] > 0 and a < 255:
                    # blend over existing art
                    k = a / 255.0
                    img[y, x, :3] = (img[y, x, :3] * (1 - k) + np.array(col) * k).astype(np.uint8)
                else:
                    img[y, x, :3] = col
                    img[y, x, 3] = a
        # foam lip
        for x in range(wx - 1, wx + ww + 1):
            cv.put(x, top - 1, (235, 250, 255))
        waterfalls.append(((wx + ww / 2.0) * kx, top * ky, int((bot_rock + tail - top) * ky * 1.2)))
        wf_widths.append(max(2, int(round(ww * kx))))
        mist_pts.append(((wx + ww / 2.0) * kx, (bot_rock + tail * 0.75) * ky, (ww + 5) * kx))

    # ---------------- compose final surface ----------------
    low = _arr_to_surface(img)
    surf = pygame.transform.scale(low, (width, height))

    # baked soft halo around crystals (static part of the glow)
    cr_meta = []
    for (gx, gy, size) in crystal_draw:
        x, y, r = gx * kx, gy * ky, size * kx * 1.6
        cr_meta.append((int(x), int(y), float(r)))
    for (x, y, r) in cr_meta:
        halo = _halo_alpha(int(r * 1.3), _hsv(crystal_hue, 0.5, 1.0))
        surf.blit(halo, (x - halo.get_width() // 2, y - halo.get_height() // 2))
    for (x, y, r) in mist_pts:
        halo = _halo_alpha(int(r), (190, 225, 255))
        surf.blit(halo, (int(x) - halo.get_width() // 2, int(y) - halo.get_height() // 2))

    rad = 0.5 * max((xr - xl) * kx, (max(bottom.max(), 0) - (cy - ry - 8)) * ky)
    meta = {
        "waterfalls": [(int(x), int(y), int(L)) for (x, y, L) in waterfalls],
        "crystals": cr_meta,
        "landing": (int(lx * kx), int(ly * ky)),
        "radius": float(rad),
        "hue": float(hue),
        "_wf_w": wf_widths,
        "_crystal_rgb": _hsv(crystal_hue, 0.65, 1.0),
        "_px": px,
        "_seed": int(seed),
        "_size": (width, height),
    }
    return surf, meta


_HALO_CACHE: dict = {}


def _halo_alpha(r, col):
    key = (r, col)
    s = _HALO_CACHE.get(key)
    if s is None:
        r = max(2, r)
        yy, xx = np.mgrid[-r:r + 1, -r:r + 1].astype(np.float32)
        d = np.sqrt(xx * xx + yy * yy) / r
        a = np.clip(1 - d, 0, 1) ** 2.2 * 90
        arr = np.zeros((2 * r + 1, 2 * r + 1, 4), np.uint8)
        arr[..., 0], arr[..., 1], arr[..., 2] = col
        arr[..., 3] = a.astype(np.uint8)
        s = _arr_to_surface(arr)
        _HALO_CACHE[key] = s
    return s


# --------------------------------------------------------------------------
# per-frame fx
# --------------------------------------------------------------------------
_GLOW_CACHE: dict = {}
_GLOW_LEVELS = 8


def _glow_sprite(r, col, level):
    """Opaque RGB sprite (black = no-op under BLEND_RGB_ADD)."""
    key = (r, col, level)
    s = _GLOW_CACHE.get(key)
    if s is None:
        k = 0.45 + 0.55 * level / (_GLOW_LEVELS - 1)
        rr_ = max(2, int(r * (0.85 + 0.3 * level / (_GLOW_LEVELS - 1))))
        yy, xx = np.mgrid[-rr_:rr_ + 1, -rr_:rr_ + 1].astype(np.float32)
        d = np.sqrt(xx * xx + yy * yy) / rr_
        inten = (np.clip(1 - d, 0, 1) ** 2.0) * k
        arr = np.zeros((2 * rr_ + 1, 2 * rr_ + 1, 4), np.uint8)
        for i in range(3):
            arr[..., i] = np.clip(inten * col[i] * 0.8, 0, 255).astype(np.uint8)
        arr[..., 3] = 255
        surf = pygame.image.frombuffer(arr.tobytes(), arr.shape[1::-1], "RGBA")
        s = pygame.Surface(surf.get_size())
        s.blit(surf, (0, 0))
        _GLOW_CACHE[key] = s
    return s


def _h(i, k):
    """Deterministic hash -> [0,1)."""
    v = math.sin(i * 12.9898 + k * 78.233) * 43758.5453
    return v - math.floor(v)


def draw_island_fx(dest: pygame.Surface, topleft: tuple[int, int], meta: dict, t: float) -> None:
    """Per-frame animation over the cached island surface (stateless, cheap)."""
    ox, oy = int(topleft[0]), int(topleft[1])
    add = pygame.BLEND_RGB_ADD
    fill = dest.fill
    clip = dest.get_clip()
    px = meta.get("_px", 3)
    widths = meta.get("_wf_w", [])
    seed = meta.get("_seed", 0)
    for wi, (x, y, L) in enumerate(meta["waterfalls"]):
        w = widths[wi] if wi < len(widths) else 3 * px
        x0 = ox + x - w // 2
        y0 = oy + y
        if not clip.colliderect((x0 - 30, y0, w + 60, L + 30)):
            continue
        n = 14 + w
        for i in range(n):
            sp = 55 + 50 * _h(i, wi + seed)
            yy = ((_h(i, 3 + wi) * L + t * sp) % L)
            fade = 1.0 - yy / L
            if fade <= 0.02:
                continue
            spread = int(yy / L * w * 0.8)
            xx = x0 - spread + int(_h(i, 7 + wi) * (w + 2 * spread))
            ln = 4 + int(8 * fade)
            v = int(110 * fade ** 1.5) + 20
            fill((v * 3 // 5, v * 4 // 5, v), (xx, y0 + int(yy), max(1, px // 2 + (i & 1)), ln), special_flags=add)
        # lip shimmer
        for i in range(3):
            ph = (t * 6 + i * 2.1 + wi) % 6.28
            b = int(60 + 60 * math.sin(ph))
            if b > 0:
                fill((b, b, b), (x0 + int(_h(i, wi) * w), y0 - px, px, px), special_flags=add)
        # drifting mist puffs near the end of the visible fall
        for i in range(4):
            phase = (t * 0.12 + _h(i, 11 + wi)) % 1.0
            my = y0 + L * (0.55 + 0.4 * phase)
            mx = x0 + w // 2 + math.sin(t * 0.7 + i * 1.9) * (6 + 14 * phase)
            lvl = int((1 - abs(phase * 2 - 1)) * (_GLOW_LEVELS - 1))
            if lvl > 0:
                g = _glow_sprite(int(7 + w * 0.6), (150, 190, 240), lvl)
                mw = g.get_width() // 2
                dest.blit(g, (int(mx) - mw, int(my) - mw), special_flags=add)
    col = meta.get("_crystal_rgb", (150, 130, 255))
    for ci, (x, y, r) in enumerate(meta["crystals"]):
        s = 0.5 + 0.5 * math.sin(t * 2.2 + ci * 1.7 + seed * 0.3)
        lvl = int(s * (_GLOW_LEVELS - 1) + 0.5)
        g = _glow_sprite(int(r * 1.4), col, lvl)
        hw, hh = g.get_width() // 2, g.get_height() // 2
        dest.blit(g, (ox + x - hw, oy + y - hh), special_flags=add)
        # sparkle
        sp = (t * 1.3 + _h(ci, 5)) % 1.0
        if sp < 0.25:
            b = int(255 * (1 - sp * 4))
            sx = ox + x + int((_h(ci, int(t * 1.3)) - 0.5) * r)
            sy = oy + y + int((_h(ci + 9, int(t * 1.3)) - 0.5) * r)
            fill((b, b, b), (sx - px // 2, sy, px, px), special_flags=add)


# --------------------------------------------------------------------------
# backdrop & stars
# --------------------------------------------------------------------------
def render_backdrop(size: tuple[int, int], seed: int = 0) -> pygame.Surface:
    """Opaque deep-space backdrop with nebulae and a dense star field."""
    W, H = int(size[0]), int(size[1])
    rng = np.random.default_rng([int(seed) & 0xFFFFFFFF, 0xBAC0])
    sw, sh = max(8, W // 4), max(8, H // 4)
    yy = np.linspace(0, 1, sh, dtype=np.float32)[:, None]
    xx = np.linspace(0, 1, sw, dtype=np.float32)[None, :]
    base = np.zeros((sh, sw, 3), np.float32)
    top = np.array([5, 6, 16], np.float32)
    bot = np.array([14, 8, 26], np.float32)
    base[:] = top + (bot - top) * yy[..., None]
    sc = sw / 320.0
    warp = _fbm(rng, sh, sw, 60 * sc, 3)
    n1 = _fbm(rng, sh, sw, 70 * sc, 5)
    n2 = _fbm(rng, sh, sw, 55 * sc, 5)
    n3 = _fbm(rng, sh, sw, 40 * sc, 4)
    dust = _fbm(rng, sh, sw, 18 * sc, 4)
    # broad diagonal band so nebulae cluster
    ang = rng.uniform(0, math.pi)
    band = np.exp(-(((xx - 0.5) * math.sin(ang) + (yy - 0.5) * math.cos(ang) + (warp - 0.5) * 0.6) ** 2) / 0.06)
    teal = _smoothstep(0.45, 0.78, n1 * 0.8 + band * 0.25) * (0.6 + 0.6 * dust)
    viol = _smoothstep(0.48, 0.8, n2 * 0.8 + band * 0.22) * (0.6 + 0.6 * dust)
    amb = _smoothstep(0.6, 0.85, n3 * 0.7 + band * 0.25 + teal * 0.15) * (0.5 + 0.8 * dust)
    lanes = _smoothstep(0.55, 0.75, _fbm(rng, sh, sw, 25 * sc, 4))
    col = base.copy()
    col += teal[..., None] * np.array([20, 92, 108], np.float32)
    col += viol[..., None] * np.array([96, 40, 132], np.float32)
    col += amb[..., None] * np.array([150, 84, 34], np.float32)
    col *= (1 - 0.55 * lanes * (teal + viol + amb).clip(0, 1))[..., None]
    col = np.clip(col, 0, 255).astype(np.uint8)
    small = pygame.image.frombuffer(np.ascontiguousarray(col).tobytes(), (sw, sh), "RGB")
    big = pygame.transform.smoothscale(small, (W, H))
    arr = pygame.surfarray.pixels3d(big)  # (W, H, 3)
    # star field
    neb = np.clip((teal + viol + amb), 0, 1)
    n_st = W * H // 180
    sx = rng.integers(0, W, n_st)
    sy = rng.integers(0, H, n_st)
    bright = (rng.random(n_st) ** 3.2) * 200 + 25
    nb = neb[np.clip(sy * sh // H, 0, sh - 1), np.clip(sx * sw // W, 0, sw - 1)]
    bright = bright * (0.7 + 0.6 * nb)
    tint = rng.random(n_st)
    r = bright * np.where(tint < 0.2, 0.8, np.where(tint > 0.85, 1.0, 0.95))
    g = bright * np.where(tint < 0.2, 0.88, np.where(tint > 0.85, 0.85, 0.95))
    b = bright * np.where(tint < 0.2, 1.0, np.where(tint > 0.85, 0.7, 1.0))
    st = np.stack([r, g, b], 1)
    cur = arr[sx, sy].astype(np.float32)
    arr[sx, sy] = np.clip(cur + st, 0, 255).astype(np.uint8)
    del arr
    # a few bright stars with flares
    surf = pygame.Surface((W, H))
    surf.blit(big, (0, 0))
    rr = random.Random(seed * 101 + 3)
    for _ in range(max(6, W * H // 70000)):
        x, y = rr.randrange(W), rr.randrange(H)
        c = rr.choice([(200, 220, 255), (255, 230, 200), (210, 200, 255), (180, 240, 255)])
        _bright_star(surf, x, y, c, rr.uniform(0.6, 1.0), rr.randint(4, 9))
    return surf


def _bright_star(surf, x, y, c, k, flare):
    add = pygame.BLEND_RGB_ADD
    g = _glow_sprite(flare + 3, c, int(k * (_GLOW_LEVELS - 1)))
    surf.blit(g, (x - g.get_width() // 2, y - g.get_height() // 2), special_flags=add)
    for i in range(1, flare + 1):
        f = (1 - i / (flare + 1)) ** 1.5 * k
        cc = (int(c[0] * f), int(c[1] * f), int(c[2] * f))
        for (dx, dy) in ((i, 0), (-i, 0), (0, i), (0, -i)):
            surf.fill(cc, (x + dx, y + dy, 1, 1), special_flags=add)
    surf.fill((255, 255, 255), (x, y, 1, 1))


def make_star_layers(size, seed=0, n=3) -> list[pygame.Surface]:
    """n transparent star layers (far -> near), tileable in both axes."""
    W, H = int(size[0]), int(size[1])
    rng = np.random.default_rng([int(seed) & 0xFFFFFFFF, 0x57A2])
    layers = []
    for li in range(n):
        k = li / max(1, n - 1)  # 0 far .. 1 near
        arr = np.zeros((H, W, 4), np.uint8)
        count = int(W * H / (900 + 2200 * k))
        xs = rng.integers(0, W, count)
        ys = rng.integers(0, H, count)
        a = (rng.random(count) ** 2 * (120 + 120 * k) + 50 + 40 * k).astype(np.int32)
        tint = rng.random(count)
        rgb = np.where(tint[:, None] < 0.25, [170, 200, 255],
                       np.where(tint[:, None] > 0.88, [255, 220, 180], [235, 238, 255]))
        arr[ys, xs, :3] = rgb
        arr[ys, xs, 3] = np.clip(a, 0, 255)
        # bigger stars (plus shape) on nearer layers; wrap with modulo -> tileable
        nb = int(count * (0.01 + 0.04 * k))
        bx = rng.integers(0, W, nb)
        by = rng.integers(0, H, nb)
        for (dx, dy, al) in ((0, 0, 255), (1, 0, 110), (-1, 0, 110), (0, 1, 110), (0, -1, 110)):
            xx = (bx + dx) % W
            yy = (by + dy) % H
            arr[yy, xx, :3] = (240, 244, 255)
            arr[yy, xx, 3] = np.maximum(arr[yy, xx, 3], al)
        layers.append(_arr_to_surface(arr))
    return layers
