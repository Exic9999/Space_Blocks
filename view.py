"""World rendering: camera, parallax space, islands, storms, Executor, ships, lock-on reticles."""
import math
import pygame
import islands
import executor
import ships
from config import *
import ui

ZOOMS = [0.45, 0.7, 1.0]


class Camera:
    def __init__(self):
        self.x, self.y = EXEC_POS[0], EXEC_POS[1] + 120
        self.zi = 1
        self.vw, self.vh = SCREEN_W, SCREEN_H

    @property
    def zoom(self):
        return ZOOMS[self.zi]

    def to_screen(self, wx, wy):
        return ((wx - self.x) * self.zoom + self.vw / 2, (wy - self.y) * self.zoom + self.vh / 2)

    def to_world(self, sx, sy):
        return ((sx - self.vw / 2) / self.zoom + self.x, (sy - self.vh / 2) / self.zoom + self.y)

    def pan(self, dx, dy):
        self.x = min(WORLD, max(0, self.x + dx))
        self.y = min(WORLD, max(0, self.y + dy))

    def zoom_by(self, d):
        self.zi = max(0, min(len(ZOOMS) - 1, self.zi + d))

    def center_on(self, x, y):
        self.x, self.y = x, y


class Art:
    """Prerendered/cached visuals shared by the game."""

    def __init__(self):
        self.backdrop = None
        self.layers = []
        self.island_surf = {}
        self.island_meta = {}
        self._scaled = {}
        self._meta_z = {}

    def build_space(self):
        # convert once to the display format; unconverted alpha surfaces blit ~50x slower
        self.backdrop = islands.render_backdrop((SCREEN_W, SCREEN_H), 3).convert()
        self.layers = [l.convert_alpha() for l in islands.make_star_layers((SCREEN_W, SCREEN_H), 11, 3)]

    def build_island(self, isl):
        surf, meta = islands.render_island(isl.seed, isl.width)
        self.island_surf[isl.id] = surf.convert_alpha()
        self.island_meta[isl.id] = meta
        lx, ly = meta["landing"]
        isl.land_dx = lx - surf.get_width() / 2
        isl.land_dy = ly - surf.get_height() / 2
        isl.radius = meta.get("radius", isl.radius)

    def scaled(self, iid, zoom):
        k = (iid, zoom)
        s = self._scaled.get(k)
        if s is None:
            b = self.island_surf[iid]
            s = pygame.transform.smoothscale(b, (max(2, int(b.get_width() * zoom)), max(2, int(b.get_height() * zoom))))
            self._scaled[k] = s
        return s

    def meta_z(self, iid, zoom):
        k = (iid, zoom)
        m = self._meta_z.get(k)
        if m is None:
            m0 = self.island_meta[iid]
            m = dict(m0)
            m["waterfalls"] = [(x * zoom, y * zoom, l * zoom) for x, y, l in m0["waterfalls"]]
            m["crystals"] = [(x * zoom, y * zoom, max(1, r * zoom)) for x, y, r in m0["crystals"]]
            self._meta_z[k] = m
        return m


def _draw_stars(screen, art, cam):
    screen.blit(art.backdrop, (0, 0))
    for i, layer in enumerate(art.layers):
        f = 0.04 + i * 0.05
        ox = int(-cam.x * f) % SCREEN_W
        oy = int(-cam.y * f) % SCREEN_H
        for dx in (ox - SCREEN_W, ox):
            for dy in (oy - SCREEN_H, oy):
                screen.blit(layer, (dx, dy))


def _draw_storm(screen, cam, st, t):
    sx, sy = cam.to_screen(st.x, st.y)
    r = st.r * cam.zoom
    if sx + r < 0 or sx - r > SCREEN_W or sy + r < 0 or sy - r > SCREEN_H:
        return
    s = pygame.Surface((int(r * 2) + 4, int(r * 2) + 4), pygame.SRCALPHA)
    c = (s.get_width() // 2, s.get_height() // 2)
    for i in range(7):
        a = t * (0.35 + i * 0.08) + i * 1.9
        rr = r * (0.35 + 0.1 * i)
        ox, oy = math.cos(a) * r * 0.25, math.sin(a * 1.2) * r * 0.25
        pygame.draw.circle(s, (150, 70, 210, 20 + i * 4), (c[0] + ox, c[1] + oy), int(rr))
    pygame.draw.circle(s, (210, 130, 255, 70), c, int(r), 2)
    for k in range(4):                      # lightning flickers
        if math.sin(t * 6 + k * 3 + st.x) > 0.93:
            a = k * 1.7 + t
            p1 = (c[0] + math.cos(a) * r * 0.3, c[1] + math.sin(a) * r * 0.3)
            p2 = (c[0] + math.cos(a + 0.4) * r * 0.8, c[1] + math.sin(a + 0.4) * r * 0.8)
            pygame.draw.line(s, (240, 220, 255, 220), p1, p2, 2)
    screen.blit(s, (sx - c[0], sy - c[1]))


def _reticle(screen, pos, r, t, color, label=None):
    x, y = pos
    rect = pygame.Rect(0, 0, max(8, 2 * r), max(8, 2 * r))
    rect.center = (x, y)
    for i in range(4):
        a = -(t * 1.3 + i * math.pi / 2)
        pygame.draw.arc(screen, color, rect, a - 0.5, a, 3)
    pygame.draw.circle(screen, color, (x, y), 3)
    if label:
        ui.text(screen, label, (x, y + r + 8), 13, color, True, "midtop")


def draw_world(game, screen):
    w, cam, art, t = game.world, game.cam, game.art, game.t
    _draw_stars(screen, art, cam)
    z = cam.zoom
    for st in w.storms:
        _draw_storm(screen, cam, st, t)
    sel_ship = game.selected_ship()
    # islands, back to front
    for isl in sorted(w.islands, key=lambda i: i.y):
        if not isl.discovered or isl.id not in art.island_surf:
            continue
        spr = art.scaled(isl.id, z)
        sx, sy = cam.to_screen(isl.x, isl.y)
        r = spr.get_rect(center=(sx, sy))
        if not screen.get_rect().colliderect(r.inflate(60, 200)):
            continue
        bob = math.sin(t * 0.6 + isl.id) * 5 * z
        r.y += bob
        screen.blit(spr, r)
        islands.draw_island_fx(screen, r.topleft, art.meta_z(isl.id, z), t)
        if game.sel_island == isl.id:
            _reticle(screen, (sx, sy + bob), isl.radius * z * 0.9 + 6 * math.sin(t * 3), t, C_ACCENT, isl.name)
        else:
            ui.text(screen, isl.name, (sx, r.bottom + 2), 12, (170, 210, 225), False, "midtop", alpha=150)
    # executor
    executor.draw(screen, cam, w.up["tanks"], w.up["sensors"], w.up["hangar"],
                  w.aeth / w.capacity, t, sum(1 for s in w.ships if s.state == "docked"))
    ex, ey = cam.to_screen(*EXEC_POS)
    ui.text(screen, "THE EXECUTOR", (ex, ey - EXEC_LEN * z * 0.5 - 18), 14, C_ACCENT, True, "midbottom", alpha=170)
    # scan pulse
    if w.pulse:
        t0, rad = w.pulse
        age = w.time - t0
        if age < 3.0:
            rr = rad * z * (age / 3.0)
            f = 1 - age / 3
            pygame.draw.circle(screen, (int(90 * f), int(220 * f), int(255 * f)), (ex, ey), max(2, int(rr)), 3)
    # ships
    for s in w.ships:
        sx, sy = cam.to_screen(s.x, s.y)
        if not (-80 < sx < SCREEN_W + 80 and -80 < sy < SCREEN_H + 80):
            continue
        spr = ships.rotated(s.grid, s.ang, 6, tuple(s.color), z * 1.7)
        if s.state not in ("docked", "mining") and s.fuel > 0:    # engine flame
            fl = pygame.Surface((24, 24), pygame.SRCALPHA)
            pygame.draw.circle(fl, (255, 170, 80, 130), (12, 12), 8)
            pygame.draw.circle(fl, (255, 240, 200, 210), (12, 12), 4)
            bx = sx - math.cos(s.ang) * 24 * z
            by = sy - math.sin(s.ang) * 24 * z
            screen.blit(fl, fl.get_rect(center=(bx, by)), special_flags=pygame.BLEND_RGBA_ADD)
        screen.blit(spr, spr.get_rect(center=(sx, sy)))
        if s.state == "mining":
            isl = w.island(s.target)
            meta = art.island_meta.get(s.target)
            if isl and meta and meta["crystals"]:
                cx, cy, _ = meta["crystals"][s.id % len(meta["crystals"])]
                surf = art.island_surf[isl.id]
                tx = isl.x + (cx - surf.get_width() / 2)
                ty = isl.y + (cy - surf.get_height() / 2)
                px, py = cam.to_screen(tx, ty)
                a = int(150 + 80 * math.sin(t * 20 + s.id))
                pygame.draw.line(screen, (255, 90, 120), (sx, sy), (px, py), 2)
                pygame.draw.circle(screen, (255, 200, 220), (px, py), max(2, int(3 * z)))
        if s is sel_ship:
            pygame.draw.circle(screen, C_ACCENT, (sx, sy), int(34 * z + 4), 1)
        if s.state != "docked":
            ui.text(screen, s.name.split()[0], (sx, sy - 22 * z - 8), 12, s.color, True, "midbottom", alpha=210)
    # lock-on lines from ships to their targets
    for s in w.ships:
        if s.target is None or s.state not in ("outbound", "mining"):
            continue
        isl = w.island(s.target)
        if not isl:
            continue
        a = cam.to_screen(s.x, s.y)
        b = cam.to_screen(isl.x, isl.y + isl.land_dy)
        bright = s is sel_ship
        k = 0.75 if bright else 0.3
        col = tuple(int(c * k) for c in s.color)
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        n = int(d // 18)
        for i in range(0, n, 2):
            f1, f2 = i / max(n, 1), (i + 1) / max(n, 1)
            pygame.draw.line(screen, col, (a[0] + (b[0] - a[0]) * f1, a[1] + (b[1] - a[1]) * f1),
                             (a[0] + (b[0] - a[0]) * f2, a[1] + (b[1] - a[1]) * f2), 2 if bright else 1)
        if bright:
            _reticle(screen, b, 22 * z + 8, t, s.color, "LOCKED")
    # off-screen arrow to selected island
    if game.sel_island is not None:
        isl = w.island(game.sel_island)
        sx, sy = cam.to_screen(isl.x, isl.y)
        if not (0 < sx < SCREEN_W and 0 < sy < SCREEN_H):
            cx, cy = SCREEN_W / 2, SCREEN_H / 2
            ang = math.atan2(sy - cy, sx - cx)
            px = max(300, min(SCREEN_W - 300, cx + math.cos(ang) * 600))
            py = max(70, min(SCREEN_H - 90, cy + math.sin(ang) * 600))
            pts = [(px + math.cos(ang) * 16, py + math.sin(ang) * 16),
                   (px + math.cos(ang + 2.5) * 12, py + math.sin(ang + 2.5) * 12),
                   (px + math.cos(ang - 2.5) * 12, py + math.sin(ang - 2.5) * 12)]
            pygame.draw.polygon(screen, C_ACCENT, pts)
            d = math.hypot(isl.x - cam.x, isl.y - cam.y)
            ui.text(screen, f"{isl.name}  {int(d)}m", (px, py + 22), 12, C_ACCENT, True, "midtop")
