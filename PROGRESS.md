# Space Blocks — development notes

Native Windows game in `pc/` (C# / .NET 8 / Raylib-cs). See `README.md` for how to build, run and test.
SDK note: if `dotnet` is not on PATH, a user-scope SDK may live at `%LOCALAPPDATA%\dotnet8`
(`$env:PATH="$env:LOCALAPPDATA\dotnet8;$env:PATH"`).

## Status
Complete and published. Tests: `--selftest` 20/20, `--uitest` 29/29, `--audiotest` OK.

## Gotchas
1. Raylib-cs needs `Matrix4x4.Transpose()` on any System.Numerics matrix passed to `DrawMesh`.
2. Pin shader attribute locations with `layout(location=0/2/3)`; raylib's VAO uses fixed locations.
3. Backface culling is off globally; the shader flips normals for back faces.
4. Call `Rlgl.SetClipPlanes(near, far)` before each `BeginMode3D`.
5. UI layering: `curLayer` / `TopLayer` in `Hud.cs` make overlays modal. Clicks are read once per frame
   (`clickPressed`) and can be injected (`Inject`, `InjMouse`, `InjClick`) for tests.

## Known simplifications and ideas
- Flat-shaded lighting (no PBR), simplified cabins, no pad numbers, no hull panel textures.
- Spending all Aetherite on upgrades can soft-lock docked ships (they cannot refuel); a mercy rule would fix it.
- Possible: gamepad support, hull textures, pad numbers.

---
# PHASE 2 (started 2026-11-01) — RESUME HERE
User request: (1) whole Executor explorable in first person incl. hangar resource tanks NPCs physically use (fuel/other resources); (2) Executor navigable in space on its own; (3) captain gets his own ship, can lock on + fly to planets; resources exist physically as "space ore" mined BY HAND and carried back to the Executor. Keep this log updated + post terminal updates. Commit messages carry no co-author trailers.

## Design (decided)
- **Executor motion:** `GameState.exx/exy/exHead/exSpeed/exDestX/exDestY`. Helper `Sim.ExPt(lx,lz)` rotates local (hull) coords to world. All "home" points (mouth, approach, docked pos) use it; `in` ships steer to live mouth. Heading theta: nose (-z local) = world (-sin t, -cos t). Speed ~45 u/s, turn .35 rad/s, burns ~.003 Aetherite per unit; set course from Cmd view (Executor chip: click empty space / SAIL TO ISLAND / HALT) or at the bridge helm (E near chair).
- **Interior:** add Crew Quarters (west of corridor) + Engineering (east) via door gaps in corridor walls (z -14..-10); collision via Walk Block rects.
- **Hangar stations** (Data.Stations): deposit (-11.4,-2.5), fuel (11.4,-2.5), repair (11.4,28); captain pad (0,3). Pilot dock FSM: ph = walk (path, then `next`) / unload / fuel / repair / idle / aboard; visits queue built on landing (only needed stations), independents skip bridge idle. Servicing (deposit/refuel/repair) happens ONLY while the pilot stands at the station. Lines drawn tank->pilot while working.
- **Captain ship:** `GameState.cap` (state docked/takeoff/fly/landing/landed, x,y,alt,ang,speed,fuel,ore chunks, island id). Modes: Fly (chase cam; W/S throttle, A/D turn, Space/Ctrl climb/descend; L lock target, G autopilot to target + auto land, R autopilot home + auto dock; F land/dock/board) and Eva (first-person on island top; E hold near ore node mines 1 chunk=5 Aetherite from island.aeth; F near ship boards). Ore nodes = physical crystals around the crystal clearing, visible count ~ island.aeth/max. Deposit: E at deposit station adds chunks to G.aeth (+delivered). Refuel own ship at fuel station (E). Low fuel => drift speed cap 40 (never hard soft-lock).
- Save: new fields default safely (old saves still load).

## Milestones (tick when done, commit each)
[x] M1 Executor motion (sim + render + helm UI + selftest) — DONE: Sim.ExStep/SailTo/SailToIsland/Halt, ExPt/ToLocal helpers, Executor chip + click-to-set-course + SAIL TO ISLAND + HALT, helm prompt (E at chair), course marker; selftest 29, uitest 35
[ ] M2 Hangar stations + NPC service workflow (sim + render + tests)
[ ] M3 New interior rooms (quarters, engineering) + collision
[ ] M4 Captain ship sim + Fly mode + autopilot/dock
[ ] M5 EVA island walking + physical ore + hand mining + deposit
[ ] M6 Tests (selftest/uitest), screenshots review, README/PROGRESS update, republish exe
