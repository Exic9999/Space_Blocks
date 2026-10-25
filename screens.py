"""Full-screen scenes: title, load, loading, intro crawl, builder, Executor upgrades, pause."""
import math
import time
import pygame
import ships
import save
import ui
import story
from config import *

TITLE_COL = (190, 235, 255)


def _stars(game, screen, drift=0.0):
    screen.blit(game.art.backdrop, (0, 0))
    for i, layer in enumerate(game.art.layers):
        ox = int(-game.t * (6 + i * 10) - drift * (1 + i)) % SCREEN_W
        for dx in (ox - SCREEN_W, ox):
            screen.blit(layer, (dx, int(math.sin(game.t * 0.1 + i) * 6)))


def _glow_text(screen, s, pos, size, col, anchor="center"):
    for r, a in ((6, 30), (3, 60)):
        g = ui.font(size, True).render(s, True, col)
        g.set_alpha(a)
        for dx, dy in ((-r, 0), (r, 0), (0, -r), (0, r)):
            screen.blit(g, g.get_rect(**{anchor: (pos[0] + dx, pos[1] + dy)}))
    ui.text(screen, s, pos, size, col, True, anchor)


# ---- title ------------------------------------------------------------------
def draw_title(game, screen):
    _stars(game, screen)
    t = game.t
    if game.title_island is not None:
        spr = game.title_island
        screen.blit(spr, spr.get_rect(center=(960, 400 + math.sin(t * 0.7) * 14)))
        import islands
        islands.draw_island_fx(screen, spr.get_rect(center=(960, 400 + math.sin(t * 0.7) * 14)).topleft,
                               game.title_meta, t)
    # wedge silhouette gliding
    ex = (t * 14) % (SCREEN_W + 700) - 350
    pts = [(ex, 120), (ex - 150, 250), (ex + 150, 250)]
    pygame.draw.polygon(screen, (40, 50, 72), pts)
    pygame.draw.polygon(screen, (110, 140, 175), pts, 1)
    _glow_text(screen, "SPACE BLOCKS", (400, 235), 70, TITLE_COL)
    ui.text(screen, "A  DRIFT  ISLES  ODYSSEY", (400, 300), 18, C_ACCENT, True, "center")
    m = game.mouse
    x0, y0 = 150, 380
    has = save.latest() is not None
    items = [("NEW GAME", game.new_game, True),
             ("CONTINUE", game.continue_game, has),
             ("LOAD GAME", lambda: game.open("load"), has),
             (f"MUSIC: {'ON' if game.music.enabled else 'OFF'}", game.music.toggle, True),
             ("QUIT", game.quit, True)]
    for i, (lab, fn, en) in enumerate(items):
        ui.button(screen, game.hits, (x0, y0 + i * 56, 260, 46), lab, fn, m, en, C_ACCENT, 20)
    ui.text(screen, "Commander Jason · The Executor · Aetherite is waiting", (SCREEN_W // 2, SCREEN_H - 28), 14, C_DIM, False, "center")
    if not game.music.ready:
        ui.text(screen, "composing score…", (SCREEN_W - 20, SCREEN_H - 24), 12, C_DIM, False, "topright")


def draw_loading(game, screen, msg, frac):
    screen.fill(C_BG)
    if game.art.backdrop is not None:
        screen.blit(game.art.backdrop, (0, 0))
    _glow_text(screen, "SPACE BLOCKS", (SCREEN_W // 2, 280), 64, TITLE_COL)
    ui.bar(screen, (SCREEN_W // 2 - 200, 380, 400, 14), frac, C_ACCENT)
    ui.text(screen, msg, (SCREEN_W // 2, 410), 15, C_DIM, False, "center")
    pygame.display.flip()
    pygame.event.pump()


# ---- load -------------------------------------------------------------------
def draw_load(game, screen):
    _stars(game, screen)
    ui.panel(screen, (340, 120, 600, 470), "LOAD GAME")
    y = 156
    for slot in save.SLOTS:
        info = save.info(slot)
        r = pygame.Rect(360, y, 560, 78)
        pygame.draw.rect(screen, (18, 32, 56), r, border_radius=8)
        pygame.draw.rect(screen, C_EDGE, r, 1, border_radius=8)
        name = "AUTOSAVE" if slot == "auto" else f"SLOT {slot}"
        ui.text(screen, name, (r.x + 14, r.y + 8), 17, C_ACCENT, True)
        if info:
            when = time.strftime("%b %d  %H:%M", time.localtime(info["saved_at"]))
            ui.text(screen, f"{when}   ·   T+{int(info['time'] // 60)}m   ·   crew {info['crew']}   ·   delivered {int(info['deposited'])}",
                    (r.x + 14, r.y + 36), 14, C_TEXT)
            ui.text(screen, f"Aetherite {int(info['aeth'])}   ·   chapter {info['chapter']}", (r.x + 14, r.y + 55), 12, C_DIM)
            ui.button(screen, game.hits, (r.right - 110, r.y + 20, 96, 38), "LOAD", lambda s=slot: game.load_game(s), game.mouse, True, C_GOOD, 16)
        else:
            ui.text(screen, "— empty —", (r.x + 14, r.y + 40), 14, C_DIM)
        y += 88
    ui.button(screen, game.hits, (360, 528, 140, 42), "BACK", game.back, game.mouse, True, C_DIM, 16)


# ---- intro crawl --------------------------------------------------------------
def draw_intro(game, screen):
    _stars(game, screen, drift=game.t * 20)
    i = game.intro_i
    if i >= len(story.PROLOGUE):
        game.finish_intro()
        return
    dur = 8.0
    a = min(1.0, game.intro_t / 1.2, (dur - game.intro_t) / 1.2)
    a = max(0.0, a)
    lines = story.PROLOGUE[i].split("\n")
    y = 260
    for k, ln in enumerate(lines):
        words, cur, out = ln.split(" "), "", []
        for wd in words:
            if ui.font(26).size(cur + " " + wd)[0] > 900:
                out.append(cur)
                cur = wd
            else:
                cur = (cur + " " + wd).strip()
        out.append(cur)
        for o in out:
            ui.text(screen, o, (SCREEN_W // 2, y), 26 if k == 0 else 21, TITLE_COL if k == 0 else C_TEXT,
                    k == 0, "center", alpha=int(255 * a))
            y += 38
        y += 12
    ui.text(screen, "click / SPACE to continue   ·   ESC to skip", (SCREEN_W // 2, SCREEN_H - 30), 13, C_DIM, False, "center")
    game.hits.add(pygame.Rect(0, 0, SCREEN_W, SCREEN_H), game.intro_next)
    if game.intro_t > dur:
        game.intro_next()


# ---- builder --------------------------------------------------------------------
CELL = 58


def draw_builder(game, screen):
    w, m = game.world, game.mouse
    _stars(game, screen)
    ship = game.selected_ship()
    ui.panel(screen, (30, 30, SCREEN_W - 60, SCREEN_H - 60), "SHIP BUILDER  ·  spend Executor Aetherite to fit parts", alpha=240)
    # pilot tabs
    x = 50
    for i, s in enumerate(w.ships):
        ui.button(screen, game.hits, (x, 62, 130, 30), s.name.split()[0], lambda i=i: game.select_ship(i), m, True, s.color, 14,
                  active=(i == game.sel_ship))
        x += 136
    gx, gy = 70, 130
    # grid
    ui.panel(screen, (gx - 14, gy - 14, CELL * GRID_W + 28, CELL * GRID_H + 28), alpha=200)
    hover = None
    for yy in range(GRID_H):
        for xx in range(GRID_W):
            r = pygame.Rect(gx + xx * CELL, gy + yy * CELL, CELL, CELL)
            ch = ship.grid[yy][xx]
            pygame.draw.rect(screen, (14, 24, 44), r)
            pygame.draw.rect(screen, (30, 50, 78), r, 1)
            if ch != ".":
                tmp = pygame.Surface((CELL, CELL), pygame.SRCALPHA)
                ships.draw_block(tmp, 0, 0, ch, CELL, tuple(ship.color))
                screen.blit(tmp, r)
            elif r.collidepoint(m):
                hover = (xx, yy)
                ok = ships.can_place(ship.grid, xx, yy, w.max_blocks())
                col = (80, 255, 160, 90) if ok else (255, 90, 90, 70)
                ov = pygame.Surface((CELL, CELL), pygame.SRCALPHA)
                ov.fill(col)
                screen.blit(ov, r)
            elif ships.can_place(ship.grid, xx, yy, w.max_blocks()):
                pygame.draw.circle(screen, (60, 110, 140), r.center, 3)
            game.hits.add(r, lambda xx=xx, yy=yy: game.builder_place(xx, yy))
    # palette
    px = gx + CELL * GRID_W + 50
    ui.text(screen, "PARTS", (px, 130), 14, C_ACCENT, True)
    for i, ch in enumerate(BUILDABLE):
        name, base, col, blurb = BLOCKS[ch]
        cost = ships.block_cost(ship.grid, ch)
        r = pygame.Rect(px, 156 + i * 56, 330, 50)
        sel = game.build_choice == ch
        pygame.draw.rect(screen, (34, 64, 100) if sel else (16, 28, 48), r, border_radius=6)
        pygame.draw.rect(screen, col if sel else (50, 80, 110), r, 2 if sel else 1, border_radius=6)
        tmp = pygame.Surface((34, 34), pygame.SRCALPHA)
        ships.draw_block(tmp, 0, 0, ch, 34)
        screen.blit(tmp, (r.x + 8, r.y + 8))
        ui.text(screen, name, (r.x + 52, r.y + 6), 16, C_TEXT, True)
        ui.text(screen, blurb, (r.x + 52, r.y + 28), 12, C_DIM)
        afford = w.aeth >= cost
        ui.text(screen, f"◈ {cost}", (r.right - 10, r.y + 14), 16, C_AETH if afford else C_BAD, True, "topright")
        game.hits.add(r, lambda ch=ch: setattr(game, "build_choice", ch))
    # stats
    st = ships.stats(ship.grid)
    sx = px + 360
    ui.text(screen, f"{ship.name.upper()}'S SHIP", (sx, 130), 14, C_ACCENT, True)
    rows = [("Speed", f"{st['speed']:.0f}"), ("Fuel", f"{st['fuel_max']}"), ("Cargo", f"{st['cargo']}"),
            ("Mining", f"{st['mine']:.1f}/s"), ("Hull", f"{st['hull']}"), ("Shield", f"{st['shield']}"),
            ("Parts", f"{ships.count(ship.grid)} / {w.max_blocks()}"), ("Pilot fund", f"◈ {int(ship.fund)}")]
    for i, (k, v) in enumerate(rows):
        ui.text(screen, k, (sx, 160 + i * 26), 14, C_DIM)
        ui.text(screen, v, (sx + 150, 160 + i * 26), 14, C_TEXT, True, "topright")
    ui.text(screen, f"Executor stores: ◈ {int(w.aeth)}", (sx, 390), 15, C_AETH, True)
    ui.text(screen, game.builder_msg, (sx, 420), 13, C_WARN)
    ui.text(screen, "Parts must touch the ship. Engines work best aft, lasers fore. Raise Hangar Bay level for bigger ships.",
            (60, SCREEN_H - 78), 12, C_DIM)
    ui.button(screen, game.hits, (SCREEN_W - 190, SCREEN_H - 90, 130, 40), "DONE", game.back, m, True, C_GOOD, 16)


# ---- executor upgrades -----------------------------------------------------------
def draw_upgrades(game, screen):
    w, m = game.world, game.mouse
    _stars(game, screen)
    ui.panel(screen, (90, 60, SCREEN_W - 180, SCREEN_H - 120), "THE EXECUTOR  ·  UPGRADES", alpha=240)
    ui.text(screen, f"Stores  ◈ {int(w.aeth)} / {w.capacity}", (120, 100), 18, C_AETH, True)
    y = 140
    for key, (title, mx, base, blurb) in UPGRADES.items():
        lvl = w.up[key]
        r = pygame.Rect(120, y, SCREEN_W - 240, 96)
        pygame.draw.rect(screen, (16, 28, 48), r, border_radius=8)
        pygame.draw.rect(screen, C_EDGE, r, 1, border_radius=8)
        ui.text(screen, title, (r.x + 16, r.y + 10), 20, C_TEXT, True)
        ui.text(screen, blurb, (r.x + 16, r.y + 38), 14, C_DIM)
        effect = {"tanks": f"Capacity {w.capacity} → {w.capacity + 250}",
                  "hangar": f"Service ×{1 + 0.5 * lvl:.1f} → ×{1 + 0.5 * (lvl + 1):.1f}, ship size {w.max_blocks()} → {w.max_blocks() + 4}",
                  "sensors": f"Scan {w.scan_radius}m → {w.scan_radius + 500}m",
                  "quarters": "Pilot patience +35%"}[key]
        ui.text(screen, effect if lvl < mx else "MAX LEVEL", (r.x + 16, r.y + 62), 14, C_ACCENT)
        for k in range(mx):
            pygame.draw.rect(screen, C_ACCENT if k < lvl else (30, 50, 76), (r.right - 330 + k * 26, r.y + 14, 20, 12), border_radius=3)
        cost = w.upgrade_cost(key)
        en = lvl < mx and w.aeth >= cost
        ui.button(screen, game.hits, (r.right - 190, r.y + 40, 170, 40), f"UPGRADE ◈ {cost}" if lvl < mx else "MAXED",
                  lambda key=key: game.buy_upgrade(key), m, en, C_GOOD, 16)
        y += 108
    ui.text(screen, game.builder_msg, (120, 580), 14, C_WARN)
    ui.button(screen, game.hits, (SCREEN_W - 260, SCREEN_H - 100, 130, 40), "DONE", game.back, m, True, C_GOOD, 16)


# ---- pause menu ------------------------------------------------------------------
def draw_menu(game, screen):
    ov = pygame.Surface((SCREEN_W, SCREEN_H), pygame.SRCALPHA)
    ov.fill((0, 0, 10, 170))
    screen.blit(ov, (0, 0))
    ui.panel(screen, (460, 130, 360, 470), "PAUSED", alpha=245)
    m = game.mouse
    y = 170
    ui.button(screen, game.hits, (490, y, 300, 44), "RESUME", game.back, m, True, C_GOOD, 18)
    y += 56
    for slot in ("1", "2", "3"):
        ui.button(screen, game.hits, (490, y, 300, 40), f"SAVE → SLOT {slot}", lambda s=slot: game.save_game(s), m, True, C_ACCENT, 16)
        y += 48
    ui.button(screen, game.hits, (490, y, 300, 40), "LOAD GAME", lambda: game.open("load"), m, True, C_ACCENT, 16)
    y += 48
    ui.button(screen, game.hits, (490, y, 300, 40), f"MUSIC: {'ON' if game.music.enabled else 'OFF'}", game.music.toggle, m, True, C_ACCENT, 16)
    y += 48
    ui.button(screen, game.hits, (490, y, 300, 40), "SAVE & QUIT TO TITLE", game.quit_to_title, m, True, C_WARN, 16)
    ui.text(screen, game.builder_msg, (640, 575), 13, C_GOOD, False, "center")
