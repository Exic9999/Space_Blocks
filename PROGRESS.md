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

## Native Windows port of the 3D game (started 2026-11-01) — RESUME HERE
**Goal (user request):** turn `mobile3d/index.html` (Three.js, ~956 lines) into a very efficient native Windows PC game; language of my choice.
**Status:** orientation only — NO port code written yet. Last commit before this: df3d9a6.
**Findings so far**
- Source of truth for gameplay: `mobile3d/index.html` lines ~80-285 (crew/lore, story beats, newGame/step sim, orders, upgrades) — pure logic, easy to port. Lines ~287-580+ are Three.js rendering (islands, Executor wedge, 5 ship designs, cabins, hangar/corridor/bridge interiors, storms); remaining ~580-956 (interiors, walk mode, input, HUD, save) not yet read.
- Machine toolchains (checked): no rustc/cargo/gcc/cl/dotnet/go/node on PATH. Python 3.12 + `.venv` exists (pygame-ce, numpy, playwright). winget works (`winget install ...`).
- Existing Python 2D game (world.py, npc_ai.py etc.) is a separate sim; the 3D web game has its own JS sim (do not mix).
**Next steps:** (1) decide language/engine (candidates: C# .NET + Raylib-cs, or Rust + macroquad/wgpu; weigh toolchain install cost on this machine), (2) install toolchain, (3) port sim + save format first with a headless test mirroring tests/web3d, (4) port rendering/HUD/walk mode, (5) benchmark frame time vs browser build, (6) update this section + commit.

### Native port — decisions & status (updated 2026-11-01)
- **Chosen stack:** C# / .NET 8 + Raylib-cs 8.1.0 (OpenGL). Lives in `pc/` (project `pc/SpaceBlocks.csproj`). `pc/bin`, `pc/obj`, `pc/shots` are git-ignored.
- **Toolchain (user-scope, no admin):** .NET SDK 8.0.427 at `%LOCALAPPDATA%\dotnet8`. Use: `$env:PATH="$env:LOCALAPPDATA\dotnet8;$env:PATH"; cd pc; dotnet run -c Release`. GPU: Intel Iris Xe (OpenGL OK; verified a raylib window + screenshot works).
- **Fully read** mobile3d/index.html. Port map: sim = lines 80-285 + walk collision 760-791 (RECTS/BLOCK/TANKS/PADS); rendering = 287-757; HUD/input 806-897; save = localStorage JSON (replace with `%APPDATA%/SpaceBlocks/save.json`); audio = WebAudio synth (replace with generated raylib Wave/Sound).
- **Fidelity plan:** simple lit/vertex-colored meshes + additive glow billboards (no PBR/PMREM); immediate-mode HUD; WASD + mouse-look walk mode (no joystick). Accept: exact RNG parity with web is impossible (unseeded Math.random) — test invariants, not outputs.
- **Acceptance spec:** tests/web3d/test_3d.py (50 checks) — port invariants: orders, picking, bridge→corridor→hangar traversal, wall blocking, save/continue, crawl skip, legacy-save finale rule, dt=0.6 soak (no NaN, no ship stuck >10 game-min).
- **Milestones:** [x] 1 sim + headless `--selftest` (20/20 pass; files pc/Data.cs Sim.cs Walk.cs SelfTest.cs; run: `dotnet run -c Release -- --selftest`) · [x] 2 command view [x] 3 HUD/upgrades/menu/title/crawl [x] 4 walk mode [x] 5 cabins (simplified) [x] 6a save/continue (%APPDATA%SpaceBlockssave.json) · [ ] 6b audio · [ ] 7 `--bench`, publish self-contained exe.
- Finding: economy soft-lock exists in the web game too (spend all Aetherite on upgrades -> docked ships cannot refuel). Faithful port; consider a mercy rule later.
- Next action: milestone 2 (command-view rendering in pc/Render*.cs, Raylib).'

### Native port — milestone 2-5 done (2026-11-01)
- Files in `pc/`: Data.cs, Sim.cs, Walk.cs, SelfTest.cs (logic) · Gfx.cs (MeshBuilder, LitShader, procedural textures) · Scene.cs (islands, Executor, 5 ships, figures) · Interior.cs (hangar/corridor/bridge) · Game.cs (loop, vis sync, ship anim, camera, exterior/interior render, picking) · Hud.cs (input, HUD, title, menu, upgrades, crawl, cabin) · Demo.cs (harness) · Program.cs.
- **Run:** `$env:PATH="$env:LOCALAPPDATA\dotnet8;$env:PATH"; cd pc; dotnet run -c Release` · **Tests:** `dotnet bin/Release/net8.0/SpaceBlocks.dll --selftest` · **Screenshots:** `--demo <cmd|follow|island|exec|storm|walk-bridge|walk-hangar|walk-corridor|cabin|sheet|crawl|title> shots/x.png` · **Bench:** `--bench`.
- **Gotchas learned:** (1) Raylib-cs needs `Matrix4x4.Transpose()` on any System.Numerics matrix passed to DrawMesh. (2) Shader attribute locations pinned with `layout(location=..)` (raylib VAO uses fixed 0/2/3). (3) Backface culling disabled globally + shader flips normals for back faces. (4) `Rlgl.SetClipPlanes(near,far)` before each BeginMode3D (exterior 2..80000, interior .08..2000).
- **Perf (Intel Iris Xe, 1080p, MSAA4x, vsync off):** command view ~1000 fps, walk hangar ~420 fps, walk bridge ~305 fps; sim step 0.005-0.01 ms. (Periodic 50-80 ms "spikes" in --bench are GPU queue drains at >300fps, not stalls.)
- **Controls:** drag=orbit, wheel=zoom, click ship/island, right-click island=lock on, WASD pan, Space scan, L/R/H/C/G orders, U upgrades, V cabin, B bridge (walk: WASD+mouse, E enter ship, T talk, B back), Tab cycle islands, 1-5 pilots, ] speed, F5/F9 save/load, Esc menu.
- **DONE (milestones 6b-7):** audio (Audio.cs, procedural music + sfx, M toggles), `--uitest` 22/22, `--audiotest`, `--selftest` 20/20, README in pc/, self-contained exe published to `pc/dist/SpaceBlocks/SpaceBlocks.exe` (git-ignored; rebuild with the publish command in pc/README.md). Port complete; possible polish: more hull detail, terrain texture, gamepad, window-island view.
- Publish needs: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o distSpaceBlocks` from pc/.
