"""Headless end-to-end: boot, new game, orders, builder, upgrades, save/load, screenshots."""
import os, sys
os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); sys.path.insert(0, ROOT)
import pygame
import main, save
from config import *

g = main.Game()
def frames(n, dt=1/30):
    for _ in range(n):
        g.mouse = (640, 360); g.update(dt); g.draw()
def shot(name):
    os.makedirs(SHOT_DIR, exist_ok=True)
    pygame.image.save(g.screen, os.path.join(SHOT_DIR, name + ".png"))

frames(5); shot("01_title")
g.new_game(); assert g.mode == "intro"; frames(120); shot("02_intro")
g.finish_intro(); frames(30)
w = g.world; w.verbose = True
shot("03_play_start")
g.do_scan(); frames(40); shot("04_scan")
isl = [i for i in w.islands if i.discovered][0]
g.select_island(isl.id, center=True); g.select_ship(0); g.lock_on(False)
for _ in range(15): frames(60)           # 30 s sim
g.cam.center_on(isl.x, isl.y); shot("05_island_mining")
g.cam.center_on(EXEC_POS[0], EXEC_POS[1] + 100); shot("06_executor")
g.open("builder"); w.aeth = 500; frames(3); g.builder_place(2, 4); frames(2); shot("07_builder")
g.back(); g.open("upgrades"); frames(2); g.buy_upgrade("tanks"); frames(2); shot("08_upgrades")
g.back(); g.all_mode = True; g.lock_on(True); g.all_mode = False
for _ in range(20): frames(60)
shot("09_busy")
g.open("menu"); frames(2); shot("10_menu"); g.save_game("1"); g.back()
t0, aeth0 = w.time, w.aeth
g.load_game("1"); frames(30)
assert abs(g.world.time - t0) < 1 and abs(g.world.aeth - aeth0) < 5, "resume mismatch"
shot("11_after_load")
g.mode = "load"; frames(2); shot("12_load_screen")
print("SMOKE OK: states", [s.state for s in g.world.ships], "dialogue:", bool(g.dialogue), "aeth", int(g.world.aeth))
