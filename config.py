"""Shared constants for Space Blocks."""
import os

SCREEN_W, SCREEN_H = 1280, 720
FPS = 60

WORLD = 8000                      # square world, px
EXEC_POS = (WORLD // 2, WORLD // 2)
EXEC_LEN, EXEC_WID = 520, 330     # The Executor sprite size (nose up)
HANGAR_OFF = (0, 238)             # hangar mouth, relative to executor center
APPROACH_OFF = (0, 400)           # approach waypoint behind the stern

RES_NAME = "Aetherite"
GRID_W, GRID_H = 7, 7

# char -> (name, base cost, color, blurb)
BLOCKS = {
    "K": ("Cockpit",   0,  (120, 205, 235), "Pilot seat"),
    "H": ("Hull",      5,  (150, 160, 182), "+hull, armor"),
    "E": ("Engine",   15,  (255, 170, 80),  "+speed, efficiency"),
    "L": ("Laser",    20,  (255, 90, 110),  "+mining rate"),
    "T": ("Fuel Tank", 10, (90, 225, 160),  "+fuel range"),
    "C": ("Cargo Bay", 10, (225, 192, 100), "+cargo"),
    "S": ("Shield",   25,  (120, 150, 255), "-storm damage"),
}
BUILDABLE = "HELTCS"

STARTER_GRID = [
    ".......",
    "...L...",
    "..HKH..",
    "..TCT..",
    "...E...",
    ".......",
    ".......",
]

# Executor upgrade tracks: key -> (title, max level, base cost, blurb)
UPGRADES = {
    "tanks":    ("Storage Tanks", 5, 120, "Bigger Aetherite reserves"),
    "hangar":   ("Hangar Bay",    4, 150, "Faster service, larger ships"),
    "sensors":  ("Sensor Array",  4, 100, "Longer scan & passive detection"),
    "quarters": ("Crew Quarters", 3, 140, "Crew stay loyal longer"),
}

SAVE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "saves")
MUSIC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "music")
SHOT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shots")

# Palette (space / command deck)
C_BG = (6, 8, 18)
C_PANEL = (12, 20, 38)
C_PANEL2 = (18, 30, 54)
C_EDGE = (60, 130, 170)
C_TEXT = (200, 225, 240)
C_DIM = (110, 140, 165)
C_ACCENT = (90, 220, 255)
C_AETH = (150, 130, 255)
C_WARN = (255, 190, 90)
C_BAD = (255, 100, 100)
C_GOOD = (110, 235, 160)
