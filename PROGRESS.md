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
- **Hangar stations** (Data.StSpot/Data.StTank): deposit (-11.4,-2.5), fuel (11.4,-2.5), repair (11.4,28); captain pad (9,3). Pilot dock FSM: ph = walk (path, then `next`) / unload / fuel / repair / idle / aboard; visits queue built on landing (only needed stations), independents skip bridge idle. Servicing (deposit/refuel/repair) happens ONLY while the pilot stands at the station. Lines drawn tank->pilot while working.
- **Captain ship:** `GameState.cap` (state docked/takeoff/fly/landing/landed, x,y,alt,ang,speed,fuel,ore chunks, island id). Modes: Fly (chase cam; W/S throttle, A/D turn, Space/Ctrl climb/descend; L lock target, G autopilot to target + auto land, R autopilot home + auto dock; F land/dock/board) and Eva (first-person on island top; E hold near ore node mines 1 chunk=5 Aetherite from island.aeth; F near ship boards). Ore nodes = physical crystals around the crystal clearing, visible count ~ island.aeth/max. Deposit: E at deposit station adds chunks to G.aeth (+delivered). Refuel own ship at fuel station (E). Low fuel => drift speed cap 40 (never hard soft-lock).
- Save: new fields default safely (old saves still load).

## Milestones (tick when done, commit each)
[x] M1 Executor motion (sim + render + helm UI + selftest) — DONE: Sim.ExStep/SailTo/SailToIsland/Halt, ExPt/ToLocal helpers, Executor chip + click-to-set-course + SAIL TO ISLAND + HALT, helm prompt (E at chair), course marker; selftest 29, uitest 35
[x] M2 Hangar stations + NPC service workflow — DONE: Data.StSpot/StTank, Sim.Route/GoTo/NextVisit FSM (ph walk/unload/fuel/repair/idle/aboard), station consoles + gantry + teal fuel tank + service beams + labels, legacy-phase save normalisation; selftest 36
[x] M3 New rooms — DONE: Crew Quarters (west door off corridor, 6 bunks), Engineering (east door, reactor + live consoles), Cargo Hold (west door in hangar wall, crates, ore bin); Walker rects overlap >0.9, Block rects leave door gaps; door-traversal tests; selftest 47
[x] M4 Captain ship — DONE: sim + ship model idx 5 (Scene.BuildShip), captain pad (9,3) in hangar, Captain.cs (Fly chase-cam mode, HUD, input, autopilot G/R, F land/dock), walk interactions (E board, hold E deposit/refuel), map button shows BRIDGE/FLY/ISLAND, ReturnToBody()
[x] M5 EVA — DONE: Mode.Eva first-person on the island, human-scale island mesh (IslandVis.EvaGfx), ore nodes around the crystal clearing (visible count ~ aeth/max), hold E mines 5-unit chunks (pack 12), F boards; uitest 49, selftest 73
[x] M6 Tests + screenshots reviewed + README/PROGRESS updated + exe republished. PHASE 2 COMPLETE (selftest 73, uitest 50, audiotest OK); E key has one prioritised action (Hud.EAction/UseE). Not yet played with a real mouse/keyboard: ask the user for feedback. Ideas: captain hull damage in storms, ore nodes regrow visually, captain bunk interaction, second cargo hold use, gamepad.

---
# PHASE 3 (started 2026-11-01) — RESUME HERE
User request (verbatim intent): (1) move teammate/NPC talk messages to bottom-LEFT above their headshots (out of the user's way; add headshot portraits to pilot chips). (2) answer "how does the commander land" (Fly: Tab target + G autopilot lands automatically, or fly low over an island, F when the prompt shows; Ctrl descends) and make the manual route clearer on screen. (3) laser guns + pick axes for the commander and ALL NPCs (viewmodel tools for commander: LMB uses tool, 1=pickaxe 2=laser; NPC figures carry both). (4) stagger island sizes: small (a few resources), medium, large + one huge big enough for a team-vs-team space-laser battle (battle itself NOT implemented unless asked; just size/space). (5) massively different island looks: mountains with waterfalls -> rivers that flow off the floating island; (6) 50/50 chance, on landing, that trees turn into tree monsters (roll per landing; ~half of nearby trees become hostile, fight with laser/pickaxe); (7) walking trails on grass and up mountains; (8) increase fidelity of everything: trees, water, mountains, inside+outside of ship; (9) keep this log updated. Still: NO references to the AI assistant anywhere in files/commits, no co-author trailers; do NOT push unless asked (SSH deploy key configured, origin = git@github.com:Exic9999/Space_Blocks.git).

## Design (decided)
- New pure `Terrain.cs` (no Raylib): `IslandShape` from (seed,R): heightfield `Height(x,z)` = plateau + hills + ridged mountain peaks (count by size class) with rim falloff; rivers traced by steepest descent from a spring on the highest peak flank to the island edge (channel carved into Height; end = waterfall off the edge); trails = polylines (landing spot -> clearing -> clusters; spiral switchbacks up the highest peak); tree points; ore node positions on terrain. Cached per island (not saved; deterministic from seed). Sim uses it for GroundY, walking, monsters.
- Island size classes (world stays 3200): 5 small R38-55, 5 medium R70-110, 3 large R170-230, 1 huge R300-340; aeth scales with R; ore nodes scale (8 + clusters).
- Render: terrain mesh (polar grid, vertex colours by height/slope: grass/dirt/rock/snow), underside rock lid kept, far trees baked, near (EVA) trees instanced at human scale, trails as ribbon meshes, rivers/waterfalls as animated scrolling-UV water ribbons (dynamic Rlgl quads) + mist.
- Tree monsters: on landing roll 50%; if hit, ~half of the nearest 24 trees are monsters (dormant until the player is within 14, then chase 3 u/s, melee 10 dmg/s within 2.5; 60 HP; laser 25 dmg hitscan, pickaxe 20 dmg melee; drop 1 ore chunk). Player HP 100; at 0: wake at the ship, lose half the ore. Pure sim, headless tests.
- Fidelity: shader gets Blinn-Phong specular (per-draw uSpec), triplanar procedural panel texture on model-space position (uPanel) for hull/interior, rim light; tree species + tiers; richer Executor/ship/interior detail.
- Talk box: bottom-left above the chips (Cmd) / above gauges (Fly/Eva), with a procedural headshot portrait.

## Milestones
[ ] P1 talk box bottom-left + portraits on chips + clearer manual landing hint
[ ] P2 Terrain.cs + island size classes + Sim integration + tests
[ ] P3 island rendering (terrain, water/rivers/waterfalls, trails, trees, mountains) + screenshots
[ ] P4 tools: commander viewmodel laser/pickaxe (LMB, 1/2), NPC gear on figures
[ ] P5 tree monsters (sim + render + HUD + tests)
[ ] P6 fidelity pass (shader spec/panel texture, Executor/ships/interiors, trees, water)
[ ] P7 tests (selftest/uitest), README/PROGRESS, republish exe, final review
