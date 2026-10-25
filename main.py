"""SPACE BLOCKS — command the Executor, lead your pilots, harvest Aetherite from the Drift Isles."""
import math
import os
import sys
import time
import pygame

from config import *
import ui
import save
import story
import view
import hud
import screens
import islands
import ships
import world as worldmod
from audio import Music


class Game:
    def __init__(self, headless=False):
        pygame.init()
        pygame.display.set_caption("Space Blocks — The Executor")
        self.screen = pygame.display.set_mode((SCREEN_W, SCREEN_H))
        self.clock = pygame.time.Clock()
        self.hits = ui.Hits()
        self.mouse = (0, 0)
        self.t = 0.0
        self.running = True
        self.mode = "title"
        self.stack = []
        self.world = None
        self.cam = view.Camera()
        self.art = view.Art()
        self.music = Music()
        self.sel_ship = 0
        self.sel_island = None
        self.all_mode = False
        self.dialogue = None
        self.pending = []
        self.speed = 1
        self.autosave_t = 0.0
        self.saved_flash = 0.0
        self.story_t = 0.0
        self.alert_until = 0.0
        self.build_choice = "E"
        self.builder_msg = ""
        self.intro_i, self.intro_t = 0, 0.0
        self.dragging = False
        self.drag_moved = False
        self.title_island, self.title_meta = None, None
        print("[boot] building space backdrop…", flush=True)
        self.art.build_space()
        s, m = islands.render_island(2024, 520)
        self.title_island, self.title_meta = s, m
        print("[boot] ready — title screen", flush=True)

    # ---- helpers -------------------------------------------------------
    def selected_ship(self):
        if not self.world or not self.world.ships:
            return None
        return self.world.ships[min(self.sel_ship, len(self.world.ships) - 1)]

    def speaker_color(self, who):
        return story.speaker_color(self.world, who)

    def scan_cd_label(self):
        cd = self.world.scan_cd
        return f"recharging {cd:.0f}s" if cd > 0 else ""

    def flash(self, msg):
        self.builder_msg = msg

    # ---- scene control -------------------------------------------------
    def open(self, mode):
        if mode in ("builder", "upgrades", "menu", "load"):
            self.stack.append(self.mode)
        self.builder_msg = ""
        self.mode = mode
        self.music.beep("click", 0.3)

    def back(self):
        self.mode = self.stack.pop() if self.stack else ("play" if self.world else "title")
        self.builder_msg = ""

    def quit(self):
        self.running = False

    def prepare_world(self, w, label):
        self.world = w
        n = len(w.islands)
        for i, isl in enumerate(w.islands):
            screens.draw_loading(self, self.screen, f"{label}: rendering island {i + 1}/{n} — {isl.name}", (i + 1) / n)
            self.art.build_island(isl)
            isl.discovered = isl.discovered
        self.cam = view.Camera()
        self.sel_ship, self.sel_island = 0, None
        self.dialogue, self.pending = None, []
        self.autosave_t = 0.0
        self.stack = []

    def new_game(self):
        w = worldmod.World()
        self.prepare_world(w, "New game")
        self.mode = "intro"
        self.intro_i, self.intro_t = 0, 0.0
        self.music.play("title")
        print("[game] new game started", flush=True)

    def finish_intro(self):
        self.mode = "play"
        self.music.play("cruise")

    def intro_next(self):
        self.intro_i += 1
        self.intro_t = 0.0

    def load_game(self, slot):
        try:
            w, extra = save.load(slot)
        except Exception as e:
            self.flash(f"Load failed: {e}")
            print(f"[save] load failed: {e}", flush=True)
            return
        self.prepare_world(w, "Loading")
        c = extra.get("cam")
        if c:
            self.cam.x, self.cam.y, self.cam.zi = c
        self.mode = "play"
        self.music.play("cruise")
        print(f"[save] loaded slot {slot} (T+{worldmod.mmss(w.time)})", flush=True)

    def continue_game(self):
        s = save.latest()
        if s:
            self.load_game(s)

    def save_game(self, slot):
        save.save(self.world, slot, dict(cam=[self.cam.x, self.cam.y, self.cam.zi]))
        self.saved_flash = 2.5
        self.flash(f"Saved to {'autosave' if slot == 'auto' else 'slot ' + slot}")
        print(f"[save] wrote slot {slot} at T+{worldmod.mmss(self.world.time)}", flush=True)

    def quit_to_title(self):
        self.save_game("auto")
        self.world = None
        self.mode = "title"
        self.stack = []
        self.music.play("title")

    # ---- commands ------------------------------------------------------
    def select_ship(self, i):
        if 0 <= i < len(self.world.ships):
            self.sel_ship = i
            s = self.world.ships[i]
            if self.mode == "play" and s.state != "docked":
                pass

    def select_island(self, iid, center=False):
        self.sel_island = iid
        if center:
            isl = self.world.island(iid)
            self.cam.center_on(isl.x, isl.y)
        self.music.beep("click", 0.3)

    def targets(self):
        return list(self.world.ships) if self.all_mode else [self.selected_ship()]

    def order(self, otype):
        for s in self.targets():
            ok, msg = self.world.give_order(s, otype, self.sel_island if otype == "mine" else None)
        self.music.beep("click", 0.4)

    def lock_on(self, everyone):
        if self.sel_island is None:
            return
        tgts = list(self.world.ships) if everyone else [self.selected_ship()]
        for s in tgts:
            self.world.give_order(s, "mine", self.sel_island)
        self.music.beep("found", 0.5)

    def do_scan(self):
        r = self.world.do_scan()
        self.music.beep("ping", 0.7)

    def buy_upgrade(self, key):
        ok, msg = self.world.buy_upgrade(key)
        self.flash(msg)

    def builder_place(self, x, y):
        ok, msg = self.world.build_for(self.selected_ship(), x, y, self.build_choice)
        self.flash(msg)

    def advance_dialogue(self):
        d = self.dialogue
        if not d:
            return
        if d["t"] * 55 < len(d["pages"][d["i"]][1]):
            d["t"] = 99
            return
        d["i"] += 1
        d["t"] = 0
        if d["i"] >= len(d["pages"]):
            self.dialogue = None

    # ---- main update ---------------------------------------------------
    def handle_events(self):
        self.mouse = pygame.mouse.get_pos()
        for e in pygame.event.get():
            if e.type == pygame.QUIT:
                self.running = False
            elif e.type == pygame.MOUSEBUTTONDOWN:
                self.on_mouse_down(e)
            elif e.type == pygame.MOUSEBUTTONUP and e.button == 1:
                self.dragging = False
            elif e.type == pygame.MOUSEMOTION and self.dragging and self.mode == "play":
                dx, dy = e.rel
                if abs(dx) + abs(dy) > 2:
                    self.drag_moved = True
                self.cam.pan(-dx / self.cam.zoom, -dy / self.cam.zoom)
            elif e.type == pygame.MOUSEWHEEL and self.mode == "play":
                self.cam.zoom_by(1 if e.y > 0 else -1)
            elif e.type == pygame.KEYDOWN:
                self.on_key(e)

    def on_mouse_down(self, e):
        if e.button == 1:
            if self.hits.click(e.pos):
                return
            if self.mode == "play":
                self.dragging, self.drag_moved = True, False
                self.map_click(e.pos)
        elif e.button == 3 and self.mode == "play":
            isl = self.island_at(e.pos)
            if isl:
                self.sel_island = isl.id
                self.lock_on(False)

    def island_at(self, pos):
        wx, wy = self.cam.to_world(*pos)
        for isl in self.world.islands:
            if isl.discovered and math.hypot(isl.x - wx, isl.y - wy) < isl.radius * 1.05:
                return isl
        return None

    def map_click(self, pos):
        wx, wy = self.cam.to_world(*pos)
        for i, s in enumerate(self.world.ships):
            if s.state != "docked" and math.hypot(s.x - wx, s.y - wy) < 30 / self.cam.zoom:
                self.sel_ship = i
                return
        isl = self.island_at(pos)
        if isl:
            self.select_island(isl.id)

    def on_key(self, e):
        k = e.key
        if self.mode == "title":
            return
        if self.mode == "intro":
            if k == pygame.K_ESCAPE:
                self.finish_intro()
            elif k in (pygame.K_SPACE, pygame.K_RETURN):
                self.intro_next()
            return
        if self.mode != "play":
            if k == pygame.K_ESCAPE:
                self.back()
            return
        if self.dialogue and k in (pygame.K_RETURN, pygame.K_KP_ENTER):
            self.advance_dialogue()
            return
        w = self.world
        if k == pygame.K_ESCAPE:
            self.open("menu")
        elif k == pygame.K_SPACE:
            self.do_scan()
        elif k == pygame.K_l:
            self.lock_on(self.all_mode)
        elif k == pygame.K_r:
            self.order("return")
        elif k == pygame.K_h:
            self.order("hold")
        elif k == pygame.K_c:
            self.order("scout")
        elif k == pygame.K_g:
            self.order("auto")
        elif k == pygame.K_p:
            self.order("build")
        elif k == pygame.K_t:
            self.all_mode = not self.all_mode
        elif k == pygame.K_b:
            self.open("builder")
        elif k == pygame.K_u:
            self.open("upgrades")
        elif k == pygame.K_RIGHTBRACKET:
            self.speed = {1: 2, 2: 4, 4: 1}[self.speed]
        elif k == pygame.K_z:
            self.cam.zoom_by(1)
        elif k == pygame.K_x:
            self.cam.zoom_by(-1)
        elif k == pygame.K_f:
            s = self.selected_ship()
            if s:
                self.cam.center_on(s.x, s.y)
        elif k == pygame.K_e:
            self.cam.center_on(EXEC_POS[0], EXEC_POS[1] + 100)
        elif k == pygame.K_F5:
            self.save_game("1")
        elif k == pygame.K_F9 and save.info("1"):
            self.load_game("1")
        elif k == pygame.K_TAB:
            disc = [i for i in w.islands if i.discovered]
            ids = [i.id for i in disc]
            nxt = ids[(ids.index(self.sel_island) + 1) % len(ids)] if self.sel_island in ids else ids[0]
            self.select_island(nxt, center=True)
        elif pygame.K_1 <= k <= pygame.K_5:
            self.select_ship(k - pygame.K_1)

    def update(self, dt):
        self.t += dt
        self.saved_flash = max(0.0, self.saved_flash - dt)
        keys = pygame.key.get_pressed()
        if self.mode == "play":
            sp = 700 / self.cam.zoom * dt
            if keys[pygame.K_LEFT] or keys[pygame.K_a]:
                self.cam.pan(-sp, 0)
            if keys[pygame.K_RIGHT] or keys[pygame.K_d]:
                self.cam.pan(sp, 0)
            if keys[pygame.K_UP] or keys[pygame.K_w]:
                self.cam.pan(0, -sp)
            if keys[pygame.K_DOWN] or keys[pygame.K_s]:
                self.cam.pan(0, sp)
        if self.mode == "intro":
            self.intro_t += dt
        if self.world and self.mode in ("play", "builder", "upgrades"):
            if not self.dialogue:      # dialogue never freezes the sim
                pass
            for _ in range(self.speed):
                self.world.update(dt)
            self.process_events()
            self.autosave_t += dt
            if self.autosave_t >= 60 and self.mode == "play":
                self.autosave_t = 0
                save.save(self.world, "auto", dict(cam=[self.cam.x, self.cam.y, self.cam.zi]))
                self.saved_flash = 1.5
                print(f"[autosave] T+{worldmod.mmss(self.world.time)} aeth={int(self.world.aeth)} "
                      f"delivered={int(self.world.stats['deposited'])}", flush=True)
            self.story_t += dt
            if self.story_t > 1.0 and self.mode == "play":
                self.story_t = 0
                if not self.dialogue:
                    pages = story.check(self.world)
                    if pages:
                        self.dialogue = dict(pages=pages, i=0, t=0.0)
        if self.dialogue:
            self.dialogue["t"] += dt
        if self.world and self.mode in ("play", "builder", "upgrades"):
            self.music.play("alert" if self.t < self.alert_until else "cruise")
        self.music.update()

    def process_events(self):
        for kind, arg in self.world.events:
            if kind == "found":
                self.music.beep("found", 0.5)
            elif kind == "upgrade" or kind == "build":
                self.music.beep("upgrade", 0.5)
            elif kind == "dock":
                self.music.beep("dock", 0.35)
            elif kind == "alert":
                self.music.beep("alert", 0.5)
                self.alert_until = self.t + 30
        self.world.events.clear()

    # ---- draw ----------------------------------------------------------
    def draw(self):
        self.hits.clear()
        s = self.screen
        m = self.mode
        if m == "title":
            screens.draw_title(self, s)
        elif m == "load":
            screens.draw_load(self, s)
        elif m == "intro":
            screens.draw_intro(self, s)
        else:
            view.draw_world(self, s)
            hud.draw_hud(self, s)
            if self.speed > 1:
                ui.text(s, f"▶▶ x{self.speed}", (1100, 12), 14, C_WARN, True)
            if m == "builder":
                screens.draw_builder(self, s)
            elif m == "upgrades":
                screens.draw_upgrades(self, s)
            elif m == "menu":
                screens.draw_menu(self, s)
        pygame.display.flip()

    def run(self):
        while self.running:
            dt = min(self.clock.tick(FPS) / 1000.0, 0.05)
            self.handle_events()
            self.update(dt)
            self.draw()
        if self.world:
            save.save(self.world, "auto", dict(cam=[self.cam.x, self.cam.y, self.cam.zi]))
            print("[save] autosaved on exit", flush=True)
        pygame.quit()


if __name__ == "__main__":
    Game().run()
