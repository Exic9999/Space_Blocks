"""The Executor: wedge-shaped carrier art (original design), hangar and tank fx."""
import math
import random
import pygame
from config import EXEC_LEN as L, EXEC_WID as W, C_AETH

_cache = {}


def tank_rects(level):
    """Storage tank capsules (x, y, w, h) in sprite coords; grows with Storage Tanks level."""
    n = min(6, 2 + level)
    out = []
    for i in range(n):
        side = -1 if i % 2 == 0 else 1
        row = i // 2
        y = int(L * 0.50 + row * 62)
        hw = (W / 2) * (y / L)
        x = int(W / 2 + side * (hw * 0.62) - 11)
        out.append((x, y, 22, 52))
    return out


def hull(level, sensors, hangar):
    key = ("hull", level, sensors, hangar)
    s = _cache.get(key)
    if s:
        return s
    s = pygame.Surface((W, L), pygame.SRCALPHA)
    rng = random.Random(5)
    cx = W / 2
    # base wedge with longitudinal shading
    for y in range(L):
        hw = (W / 2) * (y / L) + 1
        for x in range(int(cx - hw), int(cx + hw) + 1):
            f = min(1.0, abs(x - cx) / max(hw, 1))
            b = 70 + 55 * (1 - f) ** 1.5 + 10 * math.sin(y * 0.25)
            s.set_at((x, y), (int(b * 0.92), int(b * 0.98), int(b * 1.1), 255))
    # panel blocks
    for _ in range(420):
        y = rng.randint(14, L - 10)
        hw = (W / 2) * (y / L)
        x = int(cx + rng.uniform(-hw, hw) * 0.95)
        w, h = rng.randint(4, 14), rng.randint(2, 7)
        c = rng.randint(55, 118)
        r = pygame.Rect(x, y, w, h)
        if abs(r.centerx - cx) < (W / 2) * (y / L) - 4:
            pygame.draw.rect(s, (c, c + 6, c + 16, 255), r)
            pygame.draw.rect(s, (c - 22, c - 18, c - 6, 255), r, 1)
    # spine trench + edge lines
    pygame.draw.line(s, (30, 38, 52), (cx, 4), (cx, L - 130), 3)
    for side in (-1, 1):
        pygame.draw.line(s, (150, 175, 205), (cx, 1), (cx + side * (W / 2), L - 1), 2)
    for k in range(1, 5):   # chevron panel seams
        y = int(L * k / 5.0)
        hw = (W / 2) * (y / L)
        pygame.draw.line(s, (40, 50, 68), (cx - hw * 0.9, y), (cx + hw * 0.9, y), 1)
    # command bridge tower near stern
    ty = int(L * 0.70)
    pygame.draw.rect(s, (92, 104, 126), (cx - 46, ty, 92, 34))
    pygame.draw.rect(s, (120, 134, 160), (cx - 46, ty, 92, 34), 2)
    pygame.draw.rect(s, (70, 80, 102), (cx - 26, ty - 14, 52, 16))
    pygame.draw.rect(s, (130, 220, 255), (cx - 22, ty - 10, 44, 5))     # bridge windows
    for side in (-1, 1):                                                 # shield globes
        pygame.draw.circle(s, (150, 170, 200), (cx + side * 34, ty - 4), 11)
        pygame.draw.circle(s, (200, 225, 245), (cx + side * 34 - 3, ty - 7), 4)
    # sensor array (grows with level)
    for i in range(1 + sensors):
        y = int(L * 0.30 + i * 26)
        pygame.draw.circle(s, (90, 210, 255), (cx, y), 4)
        pygame.draw.circle(s, (230, 250, 255), (cx, y), 2)
    # storage tanks (static casings)
    for (x, y, w, h) in tank_rects(level):
        pygame.draw.rect(s, (36, 44, 62), (x - 3, y - 3, w + 6, h + 6), border_radius=8)
        pygame.draw.rect(s, (14, 20, 34), (x, y, w, h), border_radius=6)
    # stern: engine bank and hangar bay
    pygame.draw.rect(s, (46, 54, 72), (cx - W / 2 + 4, L - 34, W - 8, 34))
    for i in range(7):
        ex = int(cx - W / 2 + 30 + i * (W - 60) / 6)
        if abs(ex - cx) < 36:
            continue
        pygame.draw.rect(s, (28, 32, 44), (ex - 11, L - 28, 22, 24), border_radius=4)
    bay = pygame.Rect(cx - 36 - hangar * 2, L - 80, 72 + hangar * 4, 80)
    pygame.draw.rect(s, (10, 14, 24), bay, border_radius=6)
    pygame.draw.rect(s, (200, 150, 70), bay, 2, border_radius=6)
    _cache[key] = s
    return s


def draw(dest, cam, level, sensors, hangar, aeth_frac, t, docked_n):
    """cam.to_screen(wx, wy) -> screen pos; cam.zoom scales sprite."""
    from config import EXEC_POS
    z = cam.zoom
    key = ("scaled", level, sensors, hangar, round(z, 2))
    base = _cache.get(key)
    if base is None:
        h = hull(level, sensors, hangar)
        base = pygame.transform.smoothscale(h, (max(2, int(W * z)), max(2, int(L * z))))
        _cache[key] = base
    cx, cy = cam.to_screen(*EXEC_POS)
    r = base.get_rect(center=(cx, cy))
    if not dest.get_rect().colliderect(r):
        return
    dest.blit(base, r)
    ox, oy = r.left, r.top

    def sp(x, y):
        return (ox + x * z, oy + y * z)

    # tank fill (aetherite glow)
    for i, (x, y, w, h) in enumerate(tank_rects(level)):
        fill = max(0.0, min(1.0, aeth_frac))
        fh = h * fill
        pr = pygame.Rect(0, 0, w * z, fh * z)
        pr.bottomleft = sp(x, y + h)
        pygame.draw.rect(dest, (110 + 30 * math.sin(t * 2 + i), 90, 255), pr, border_radius=max(1, int(4 * z)))
        pygame.draw.line(dest, (230, 220, 255), pr.topleft, pr.topright, 1)
    # engine glow
    for i in range(7):
        ex = W / 2 - W / 2 + 30 + i * (W - 60) / 6
        if abs(ex - W / 2) < 36:
            continue
        flick = 0.75 + 0.25 * math.sin(t * 17 + i * 2.1)
        px, py = sp(ex, L - 4)
        for rad, a in ((14, 70), (9, 130), (5, 220)):
            g = pygame.Surface((rad * 2 * z + 2, rad * 2 * z + 2), pygame.SRCALPHA)
            pygame.draw.circle(g, (90, 170, 255, int(a * flick)), (g.get_width() // 2, g.get_height() // 2), max(1, int(rad * z)))
            dest.blit(g, g.get_rect(center=(px, py + 6 * z)), special_flags=pygame.BLEND_RGBA_ADD)
    # hangar interior light
    hx, hy = sp(W / 2, L - 42)
    pulse = 0.6 + 0.4 * math.sin(t * 3)
    g = pygame.Surface((int(84 * z) + 2, int(64 * z) + 2), pygame.SRCALPHA)
    pygame.draw.ellipse(g, (255, 190, 90, int(18 + 22 * pulse)), g.get_rect())
    dest.blit(g, g.get_rect(center=(hx, hy)), special_flags=pygame.BLEND_RGBA_ADD)
    # running lights
    if int(t * 2) % 2 == 0:
        for side in (-1, 1):
            px, py = sp(W / 2 + side * (W / 2 - 6), L - 8)
            pygame.draw.circle(dest, (255, 80, 80) if side < 0 else (80, 255, 120), (px, py), max(2, int(3 * z)))
