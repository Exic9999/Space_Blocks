"""Command-deck HUD: top bar, crew roster, NAV island list, radar, orders, comms log."""
import math
import pygame
import ships
import ui
from config import *
from world import mmss

STATE_LABEL = {
    "docked": "DOCKED", "outbound": "OUTBOUND", "mining": "MINING", "inbound": "INBOUND",
    "scouting": "SCOUTING",
}


def _order_label(game, s):
    w = game.world
    if s.independent:
        return "INDEPENDENT", C_BAD
    o = s.order
    if o is None:
        left = max(0, int(s.loyalty / 100 * 0))
        return "AWAITING ORDERS", C_WARN
    t = o["type"]
    if t == "mine":
        isl = w.island(o["island"])
        return f"MINE · {isl.name}", C_GOOD
    return {"hold": "HOLD", "return": "RETURNING", "scout": "SCOUT SORTIE",
            "auto": "AUTO (cooperative)", "build": "SPONSORED BUILD"}.get(t, t.upper()), C_ACCENT


def draw_top(game, screen):
    w = game.world
    ui.panel(screen, (0, 0, SCREEN_W, 40), alpha=235)
    ui.text(screen, "COMMAND DECK  ·  THE EXECUTOR", (14, 11), 16, C_ACCENT, True)
    frac = w.aeth / w.capacity
    ui.text(screen, f"{RES_NAME.upper()}", (420, 6), 12, C_AETH, True)
    ui.bar(screen, (420, 21, 220, 11), frac, C_AETH)
    ui.text(screen, f"{int(w.aeth)} / {w.capacity}", (646, 13), 14, C_TEXT, True)
    ui.text(screen, f"T+{mmss(w.time)}", (780, 13), 14, C_DIM, True)
    ui.text(screen, f"Delivered {int(w.stats['deposited'])}", (860, 13), 14, C_DIM)
    ui.text(screen, f"Islands {sum(1 for i in w.islands if i.discovered)}/{len(w.islands)}", (1000, 13), 14, C_DIM)
    if game.saved_flash > 0:
        ui.text(screen, "● SAVED", (1190, 13), 14, C_GOOD, True)


def draw_crew(game, screen):
    w, m = game.world, game.mouse
    ui.panel(screen, (6, 46, 262, 8 + 84 * max(3, len(w.ships)) + 20), "CREW ROSTER")
    y = 66
    for i, s in enumerate(w.ships):
        r = pygame.Rect(12, y, 250, 78)
        sel = i == game.sel_ship
        pygame.draw.rect(screen, (28, 52, 86) if sel else (16, 28, 48), r, border_radius=6)
        pygame.draw.rect(screen, s.color if sel else (50, 80, 110), r, 2 if sel else 1, border_radius=6)
        game.hits.add(r, lambda i=i: game.select_ship(i), "Select pilot")
        st = ships.stats(s.grid)
        pygame.draw.circle(screen, s.color, (r.x + 14, r.y + 14), 6)
        ui.text(screen, s.name, (r.x + 26, r.y + 5), 15, C_TEXT, True)
        lab, col = _order_label(game, s)
        ui.text(screen, f"{STATE_LABEL.get(s.state, s.state)}", (r.right - 8, r.y + 7), 11, C_DIM, True, "topright")
        ui.text(screen, lab, (r.x + 26, r.y + 24), 12, col, True)
        ui.text(screen, "FUEL", (r.x + 8, r.y + 42), 10, C_DIM)
        ui.bar(screen, (r.x + 38, r.y + 44, 62, 7), s.fuel / st["fuel_max"], C_GOOD if s.fuel > st["fuel_max"] * 0.3 else C_BAD)
        ui.text(screen, "HULL", (r.x + 106, r.y + 42), 10, C_DIM)
        ui.bar(screen, (r.x + 136, r.y + 44, 52, 7), s.hull / st["hull"], (120, 190, 255) if s.hull > st["hull"] * 0.4 else C_BAD)
        ui.text(screen, "CARGO", (r.x + 8, r.y + 58), 10, C_DIM)
        ui.bar(screen, (r.x + 46, r.y + 60, 54, 7), s.cargo / st["cargo"], C_AETH)
        ui.text(screen, "LOYAL", (r.x + 106, r.y + 58), 10, C_DIM)
        ui.bar(screen, (r.x + 142, r.y + 60, 46, 7), s.loyalty / 100, C_WARN if s.loyalty > 25 else C_BAD)
        ui.text(screen, f"◈{int(s.fund)}", (r.right - 8, r.y + 56), 12, C_AETH, True, "topright")
        y += 84


def draw_nav(game, screen):
    w, m = game.world, game.mouse
    ex, ey = EXEC_POS
    panel = pygame.Rect(SCREEN_W - 272, 46, 266, 372)
    ui.panel(screen, panel, "NAV · ISLANDS")
    disc = sorted([i for i in w.islands if i.discovered], key=lambda i: math.hypot(i.x - ex, i.y - ey))
    y = panel.y + 26
    for isl in disc[:9]:
        r = pygame.Rect(panel.x + 6, y, panel.w - 12, 28)
        sel = game.sel_island == isl.id
        pygame.draw.rect(screen, (30, 60, 96) if sel else (16, 28, 48), r, border_radius=5)
        if sel:
            pygame.draw.rect(screen, C_ACCENT, r, 1, border_radius=5)
        game.hits.add(r, lambda i=isl.id: game.select_island(i, center=True), "Select island (click map to center)")
        col = (255, 215, 120) if isl.special else (140, 235, 200)
        pygame.draw.circle(screen, col, (r.x + 10, r.centery), 4)
        ui.text(screen, isl.name, (r.x + 20, r.y + 2), 13, C_TEXT, True)
        d = int(math.hypot(isl.x - ex, isl.y - ey))
        ui.text(screen, f"{d}m", (r.right - 6, r.y + 2), 11, C_DIM, False, "topright")
        ui.bar(screen, (r.x + 20, r.y + 19, 130, 5), isl.aeth / isl.aeth_max, C_AETH)
        ui.text(screen, f"{int(isl.aeth)}", (r.right - 6, r.y + 14), 11, C_AETH, False, "topright")
        crowd = [s for s in w.ships if s.target == isl.id]
        for k, s in enumerate(crowd):
            pygame.draw.circle(screen, s.color, (r.x + 160 + k * 11, r.y + 21), 4)
        y += 31
    ship = game.selected_ship()
    ok = game.sel_island is not None and ship is not None
    lab = f"LOCK ON → {ship.name.split()[0].upper()}" if ship else "LOCK ON"
    ui.button(screen, game.hits, (panel.x + 8, panel.bottom - 66, panel.w - 16, 28), lab,
              lambda: game.lock_on(False), m, ok, C_GOOD, 14, "Send selected pilot to mine the selected island (L)")
    ui.button(screen, game.hits, (panel.x + 8, panel.bottom - 34, panel.w - 16, 26), "LOCK ON → ALL PILOTS",
              lambda: game.lock_on(True), m, game.sel_island is not None, C_WARN, 13, "Send the whole crew")


def draw_radar(game, screen):
    w = game.world
    cx, cy, R = SCREEN_W - 139, 576, 118
    ui.panel(screen, (SCREEN_W - 272, 424, 266, 290), "LONG-RANGE RADAR")
    rng = w.scan_radius + 1000
    bg = pygame.Surface((R * 2 + 4, R * 2 + 4), pygame.SRCALPHA)
    pygame.draw.circle(bg, (8, 30, 40, 235), (R + 2, R + 2), R)
    for k in (1, 2, 3):
        pygame.draw.circle(bg, (40, 130, 150, 90), (R + 2, R + 2), R * k // 3, 1)
    pygame.draw.line(bg, (40, 130, 150, 70), (2, R + 2), (R * 2 + 2, R + 2))
    pygame.draw.line(bg, (40, 130, 150, 70), (R + 2, 2), (R + 2, R * 2 + 2))
    screen.blit(bg, (cx - R - 2, cy - R - 2))
    # sweep
    a = game.t * 1.6
    sweep = pygame.Surface((R * 2 + 4, R * 2 + 4), pygame.SRCALPHA)
    for k in range(14):
        aa = a - k * 0.045
        pygame.draw.line(sweep, (90, 255, 210, max(0, 90 - k * 7)), (R + 2, R + 2),
                         (R + 2 + math.cos(aa) * R, R + 2 + math.sin(aa) * R), 2)
    screen.blit(sweep, (cx - R - 2, cy - R - 2))
    ex, ey = EXEC_POS

    def blip(wx, wy, clamp=False):
        dx, dy = (wx - ex) / rng * R, (wy - ey) / rng * R
        d = math.hypot(dx, dy)
        if d > R - 4:
            if not clamp:
                return None, False
            dx, dy = dx / d * (R - 5), dy / d * (R - 5)
            return (cx + dx, cy + dy), True
        return (cx + dx, cy + dy), False

    for st in w.storms:
        p, edge = blip(st.x, st.y)
        if p:
            pygame.draw.circle(screen, (200, 70, 120), p, max(3, int(st.r / rng * R)), 1)
    for isl in w.islands:
        if not isl.discovered:
            continue
        p, edge = blip(isl.x, isl.y, True)
        col = (255, 215, 120) if isl.special else (110, 255, 205)
        sz = 3 + int(3 * isl.aeth / isl.aeth_max)
        if isl.special:
            pulse = 1 + 0.4 * math.sin(game.t * 4)
            pygame.draw.circle(screen, col, p, int(7 * pulse), 1)
        pygame.draw.circle(screen, col if not edge else (60, 160, 140), p, sz)
        if game.sel_island == isl.id:
            pygame.draw.circle(screen, C_ACCENT, p, sz + 5 + int(2 * math.sin(game.t * 5)), 1)
    for s in w.ships:
        if s.state == "docked":
            continue
        p, _ = blip(s.x, s.y, True)
        pygame.draw.circle(screen, s.color, p, 3)
        if s.target is not None:
            isl = w.island(s.target)
            q, _ = blip(isl.x, isl.y, True)
            pygame.draw.line(screen, (*s.color,), p, q, 1)
    pygame.draw.polygon(screen, C_ACCENT, [(cx, cy - 6), (cx - 4, cy + 4), (cx + 4, cy + 4)])
    # camera window
    vw = SCREEN_W / game.cam.zoom / rng * R / 2
    vx, vy = blip(game.cam.x, game.cam.y, False)[0] or (cx, cy)
    pygame.draw.rect(screen, (120, 160, 190), (vx - vw, vy - vw * 0.56, vw * 2, vw * 1.12), 1)
    if w.pulse and w.time - w.pulse[0] < 3:
        pr = (w.time - w.pulse[0]) / 3 * (w.pulse[1] / rng * R)
        pygame.draw.circle(screen, C_ACCENT, (cx, cy), int(pr), 2)
    # click radar to recenter camera
    rr = pygame.Rect(cx - R, cy - R, R * 2, R * 2)

    def go(rng=rng):
        mx, my = pygame.mouse.get_pos()
        game.cam.center_on(ex + (mx - cx) / R * rng, ey + (my - cy) / R * rng)
    game.hits.add(rr, go, "Click to jump the view")
    ui.text(screen, f"range {int(rng)}m", (SCREEN_W - 266 + 8, 696), 11, C_DIM)


def draw_orders(game, screen):
    m = game.mouse
    ship = game.selected_ship()
    bar = pygame.Rect(276, SCREEN_H - 56, 730, 52)
    ui.panel(screen, bar, alpha=230)
    names = [("SCAN", "Ping for undiscovered islands (SPACE)", game.do_scan, True, C_ACCENT),
             ("RETURN", "Recall to hangar (R)", lambda: game.order("return"), ship is not None, C_ACCENT),
             ("HOLD", "Hold in hangar (H)", lambda: game.order("hold"), ship is not None, C_ACCENT),
             ("SCOUT", "Scouting sortie to find islands (C)", lambda: game.order("scout"), ship is not None, C_ACCENT),
             ("AUTO", "Let them manage themselves, loyally (G)", lambda: game.order("auto"), ship is not None, C_ACCENT),
             ("SPONSOR", "Pay a ship upgrade from the stores (P)", lambda: game.order("build"), ship is not None, C_WARN),
             ("BUILDER", "Design ships block by block (B)", lambda: game.open("builder"), ship is not None, C_GOOD),
             ("EXECUTOR", "Upgrade the carrier (U)", lambda: game.open("upgrades"), True, C_GOOD),
             ("MENU", "Pause menu (ESC)", lambda: game.open("menu"), True, C_DIM)]
    x = bar.x + 8
    bw = 68
    for lab, tip, fn, en, col in names:
        ui.button(screen, game.hits, (x, bar.y + 8, bw, 36), lab, fn, m, en, col, 12, tip)
        x += bw + 5
    ui.button(screen, game.hits, (x, bar.y + 8, 56, 36), "ALL" if game.all_mode else "ONE",
              lambda: setattr(game, "all_mode", not game.all_mode), m, True, C_WARN, 12,
              "Orders apply to: one pilot / all pilots (T)", active=game.all_mode)
    if game.scan_cd_label():
        ui.text(screen, game.scan_cd_label(), (bar.x + 8 + 37, bar.y - 12), 11, C_DIM, False, "midtop")


def draw_log(game, screen):
    w = game.world
    r = pygame.Rect(276, SCREEN_H - 176, 560, 112)
    s = pygame.Surface(r.size, pygame.SRCALPHA)
    pygame.draw.rect(s, (8, 14, 28, 140), s.get_rect(), border_radius=8)
    screen.blit(s, r)
    y = r.bottom - 18
    for e in reversed(w.log[-6:]):
        age = w.time - e["t"]
        a = 255 if age < 20 else max(90, 255 - int((age - 20) * 6))
        col = e["color"] or {"warn": C_WARN, "good": C_GOOD, "found": C_ACCENT}.get(e["kind"], C_TEXT)
        ui.text(screen, f"{mmss(e['t'])}  {e['text']}", (r.x + 8, y), 13, tuple(col), False, alpha=a)
        y -= 17
        if y < r.y:
            break


def draw_dialogue(game, screen):
    d = game.dialogue
    if not d:
        return
    who, line = d["pages"][d["i"]]
    r = pygame.Rect(300, 64, 680, 96)
    ui.panel(screen, r, alpha=240, edge=C_ACCENT)
    col = game.speaker_color(who)
    pygame.draw.circle(screen, col, (r.x + 34, r.y + 40), 20)
    ui.text(screen, who[:1], (r.x + 34, r.y + 40), 22, (10, 10, 20), True, "center")
    ui.text(screen, who.upper(), (r.x + 66, r.y + 10), 14, col, True)
    n = int(d["t"] * 55)
    shown = line[:n]
    # simple word wrap
    words, lines, cur = shown.split(" "), [], ""
    for wd in words:
        if ui.font(15).size(cur + " " + wd)[0] > r.w - 90:
            lines.append(cur)
            cur = wd
        else:
            cur = (cur + " " + wd).strip()
    lines.append(cur)
    for k, ln in enumerate(lines[:3]):
        ui.text(screen, ln, (r.x + 66, r.y + 32 + k * 19), 15, C_TEXT)
    ui.text(screen, "click / ENTER ▸" if n >= len(line) else "", (r.right - 12, r.bottom - 18), 11, C_DIM, False, "topright")
    game.hits.add(r, game.advance_dialogue)


def draw_tip(game, screen):
    tip = game.hits.tip_at(game.mouse)
    if tip:
        r = ui.text(screen, tip, (game.mouse[0] + 14, game.mouse[1] + 18), 13, C_TEXT)
        bg = r.inflate(10, 6)
        pygame.draw.rect(screen, (10, 18, 34), bg, border_radius=4)
        pygame.draw.rect(screen, C_EDGE, bg, 1, border_radius=4)
        ui.text(screen, tip, r.topleft, 13, C_TEXT)


def draw_hud(game, screen):
    draw_top(game, screen)
    draw_crew(game, screen)
    draw_nav(game, screen)
    draw_radar(game, screen)
    draw_log(game, screen)
    draw_orders(game, screen)
    draw_dialogue(game, screen)
    draw_tip(game, screen)
