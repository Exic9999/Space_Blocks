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

## Native Windows port of the 3D game (2026-11-01) — RESUME HERE
**Goal (user request):** turn `mobile3d/index.html` (Three.js) into a very efficient native Windows PC game; language of my choice.
**STATUS: COMPLETE and published.** C# / .NET 8 / Raylib-cs 8.1.0 (OpenGL 3.3) in `pc/`. Launch: `pc\dist\SpaceBlocks\SpaceBlocks.exe` (self-contained; `dist/` is git-ignored, rebuild with the publish command below). Not yet played by a human with real mouse/keyboard: ask the user to try it and report problems.
**Tests (all passing):** `--selftest` 20/20 (sim, save, walk, legacy-save, soaks) · `--uitest` 29/29 (picking, orders, walk, cabin, save/continue, crawl, injected real clicks incl. overlay blocking, pan direction) · `--audiotest` (device ready, 24 s music loop streams).
**Commands** (SDK is user-scope, not on PATH): `$env:PATH="$env:LOCALAPPDATA\dotnet8;$env:PATH"; cd pc`
- run: `dotnet run -c Release` · tests: `dotnet bin/Release/net8.0/SpaceBlocks.dll --selftest|--uitest|--audiotest|--bench`
- screenshots: `--demo <cmd|follow|island|exec|storm|walk-bridge|walk-hangar|walk-corridor|cabin|sheet|crawl|title> shots/x.png` (view PNGs with the Read tool)
- publish: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o dist/SpaceBlocks`
**Layout (pc/):** Data/Sim/Walk (logic) · Gfx (MeshBuilder, LitShader, textures) · Scene/Interior (procedural models) · Game (loop, camera, render, picking) · Hud (UI, input, screens, cabin) · Audio · Demo/SelfTest (harnesses) · Program · README.md.
**Controls:** drag=orbit, wheel/Z/X=zoom, WASD pan, click ship/island/chip, right-click island=lock on, Space scan, L/R/H/C/G orders, U upgrades, V cabin, B bridge (walk: WASD+mouse, E enter ship, T talk, B back), Tab cycle islands, 1-5 pilots, ] speed, M music, F5/F9 save/load, Esc menu. Save: `%APPDATA%\SpaceBlocks\save.json` (autosave 10 s).
**Perf (Intel Iris Xe, 1080p, MSAA4x, vsync off):** command view ~1000 fps, walk hangar ~420 fps, walk bridge ~305 fps; sim step ~0.01 ms. Periodic 50-80 ms spikes in `--bench` are GPU-queue drains at >300 fps, not stalls. Published exe idles at ~275 MB working set.
**Known simplifications vs web build:** no PBR/env maps, simplified cabins (per-crew props reduced), no pad numbers, static flag, no hull panel textures, WASD/mouse instead of joystick/pinch. Game-design finding (exists in web build too): spending all Aetherite on upgrades soft-locks docked ships (cannot refuel); consider a mercy rule.
**Gotchas learned:** (1) Raylib-cs needs `Matrix4x4.Transpose()` for any System.Numerics matrix passed to DrawMesh. (2) Pin shader attribute locations with `layout(location=0/2/3)`. (3) Backface culling off + shader flips normals. (4) Call `Rlgl.SetClipPlanes(near,far)` before each BeginMode3D. (5) UI layering: `curLayer`/`TopLayer` in Hud.cs make overlays modal; clicks are read once per frame (`clickPressed`) and can be injected (`Inject/InjMouse/InjClick`) for tests.
**Possible next work:** play-test feedback, hull panel textures, pad numbers, gamepad, mercy rule for the soft-lock, push to origin (branch is several commits ahead, not pushed).
