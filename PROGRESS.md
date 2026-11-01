# Space Blocks — progress log (resume point if disconnected)

**Run:** `./run.sh`  (uses `.venv`: Python 3.12 + pygame-ce + numpy; system python3.14 has no pygame)
**Rebuild venv:** `uv venv --python 3.12 .venv && uv pip install --python .venv/bin/python pygame-ce numpy`
**Tests (headless):** `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy .venv/bin/python tests/sim_test.py | smoke.py | soak.py`
Screenshots land in `shots/`. Saves in `saves/` (autosave every 60s + on exit). Music cached in `assets/music/` (generated on first run, ~30s, in background).

## Design
Top-down 2D, block-grid mini ships (7x7). Commander Jason runs The Executor (wedge carrier).
Pilots (Mira, Tobias, Zeph; Okoye joins at 150 delivered, Pip at 600) fly to floating islands for Aetherite,
dock in the hangar to deposit/refuel/repair. Pilots keep a share to build their own parts.
Idle pilots lose loyalty -> go INDEPENDENT (selfish, may refuse orders). Ion storms damage ships; shields help.
Story: prologue crawl + 12 milestone beats; ends with Haven / Wanderlust Drive (sandbox continues).

## Modules
config, crew (roster/personalities/lines), ships (blocks/stats/sprites), npc_ai (orders + utility AI + building),
world (headless sim), story, save (atomic JSON slots), executor (art), islands (agent-built art), music (agent-built synth),
audio (player+sfx), view (world render), hud (command deck), screens (title/load/intro/builder/upgrades/menu), main.

## Controls
Click ship/island, right-click island = lock on. SPACE scan, L lock, R return, H hold, C scout, G auto, P sponsor build,
B builder, U executor upgrades, T one/all pilots, Tab cycle islands, 1-5 pilots, ]=speed, Z/X zoom, WASD pan, F5/F9 quick save/load, ESC menu.

## Status: all features implemented and tested (sim, smoke w/ screenshots, 90-min soak).
Ideas not done: combat/enemies, ship-to-ship trading, sfx beyond simple synth.

## Mobile builds (added 2026-11-01)
- `mobile/index.html` — 2D single-file web version. Smoke-checked only (loads, buttons render, no console errors); no test runner.
- `mobile3d/index.html` — Three.js 3D version (command map, walkable Executor: bridge/corridor/hangar, ship cabins, ion storms, saves).
  `index.before_storms.html` is a pre-storm snapshot; `t_*.html` and `*.png` are per-scene debug builds/screenshots.
- Browser tests: `.venv/Scripts/python.exe tests/web3d/test_3d.py` (Playwright; 50/50 pass). Set `CHROMIUM_PATH` to use a specific Chromium.
  The test swaps the Three.js CDN link for local `three.min.js`, so the CDN load itself is untested.

## Windows setup (verified 2026-11-01)
`winget install Python.Python.3.12`, then `python -m venv .venv` and
`.venv\Scripts\python.exe -m pip install pygame-ce numpy playwright` and `.venv\Scripts\python.exe -m playwright install chromium`.
Run the game with `.venv\Scripts\python.exe main.py` (`run.sh` assumes `.venv/bin`). Headless tests: set `SDL_VIDEODRIVER=dummy` and `SDL_AUDIODRIVER=dummy`.
All suites (sim_test, smoke, soak, web3d) passed on Windows.
