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
[x] P1 DONE: talk box bottom-left above chips/gauges with procedural headshot (Portrait.cs), portraits on pilot chips, Fly HUD hint "hold Ctrl to descend, then F to land"
[x] P2 DONE: Terrain.cs (IslandShape: heightfield+peaks, flattened pad/clearing/clusters, rivers to the rim, A* trails, trees, ore nodes; cached by (seed,R), not saved), island size classes + guaranteed placement (Sim.NewGame), Sim uses terrain (GroundY, LandSpot=Pad, DockPt, TopY, CruiseFloor, VisibleNodes), Executor never parks in/steers through islands, captain autopilot lands on the pad, pilots score islands by carryable yield, emergency reactor trickle (ReactorFloor 40) fixes the stores soft-lock; selftest 143, uitest 50. NEXT: P3 rendering (Game.UpdateShips hover still uses old ti.s*.6 + mining-beam end + crystal draw use old flat-top; Tap pick radius still 1.25R)
[x] P3 DONE: IslandGen.cs (terrain mesh with smooth normals + per-vertex colour: grass/meadow/rock/snow/riverbank, underside, boulders, 3 tree species baked far + instanced near trees in Eva, trail ribbons, river/pond/rim-waterfall water meshes with UV flow + WaterShader, mist billboards, decor tufts/flowers), meshes prepared on worker threads (Prepare/Finish, 2 at a time) and CPU copies freed after upload; bench: map ~290 fps, working set ~540 MB. Eva uses real terrain height. Still TODO in later phases: tree monsters hook (Game.treeMonsterHides), tool viewmodels, fidelity pass (spec/panel texture), Executor/ship/interior detail.
[x] P4 DONE: Tools.cs (commander pick axe + laser gun first-person viewmodels drawn in a separate depth range via opengl32 glClear(depth); keys 1/2, left-click uses tool; laser mines ore under the crosshair 1.6x faster (Sim.MineNode), bolt effect; NPC figures carry a holstered laser + strapped pick axe (Scene.BuildFigureGear)). NEXT: P5 tree monsters (Sim.Monsters, RollHaunting on landing 50%, MonsterStep, HitMonsters/MeleeMonsters, Captain.hp, hide trees via Game.treeMonsterHides, HUD health).
[x] P5 DONE: Sim monsters (RollHaunting 50% per landing -> ~half of nearest 26 trees; dormant until 18 units; chase 3.4 u/s; 12 dmg/s in reach; 60 hp; laser 25 hitscan, pick axe 22 melee in a cone; drop 1 ore; overwhelmed at 0 hp -> respawn at ship, half ore, monsters cleared; takeoff/load clear them; Captain.hp saved); MonsterGfx.Proto faces, Game draws monsters in DrawNearTrees, HUD health + warnings + hurt vignette; selftest 156, uitest 51.
[x] P6 DONE: LitShader gets Blinn-Phong spec (uSpec), rim light, triplanar procedural metal panel texture (Tex.Panel, uPanel scale on model-space position, per-panel tone variation); applied to Executor hull (1/44), ships (1/4.5), interiors (1/5), tools; Executor gets turret pods, antenna masts, raised plating, edge lights, blinking nav lights; island fidelity from P3 (smooth terrain, 3 tree species, flowing water shader, mountains with snow/rock, boulders, tufts/flowers).
[x] P7 tests (selftest 159, uitest 54, audiotest); also fixed: cursor pinned to crosshair on foot (Hud cursorLocked) and freed for menus; pilots use terrain-below cruise floor (Sim.CruiseFloor x,y,ang), not island peak, README updated, exe republished. PHASE 3 COMPLETE. Not yet played by a human with a real mouse/keyboard. Ideas: battle on the huge island (not implemented), NPC mining on foot, weather/day-night, mountain snow particles, wind sway for trees.

## Design review notes for P2+ (from review; follow these)
- Terrain.cs: bake flatten zones (landing pad, crystal clearing, ore clusters, trailheads) into Height(); trace rivers on UNcarved height with a step cap + momentum, stalled rivers continue outward to the rim, then carve; keep rivers out of flatten zones, trails avoid water. Cache must NOT be serialized (private / [JsonIgnore]). Selftests per size class: every river reaches the rim; pad + nodes dry and nearly flat; trails never in water; peak below flight ceiling.
- Replace every flat-top assumption (grep `.12f * i.s`, `Scene.GroundH`, `ti.s * .6f` pilot hover alt in Game.UpdateShips, mining-beam end `i.s * .14f`, `IslandGfx.Top`, CapCanLand/autopilot top/CapFly ground clamp) with ONE surface function + per-island max height. Pilots hover over the clearing (not centre/peak); raise cruise altitude over islands they cross.
- Placement: largest first, never accept a failed placement (shrink instead); clear zone around Executor start; separation >= sum of radii + ~250; inside world; first 3 found islands small/medium and in pilot range (~850); test >= 5 islands reachable from start. Do not loosen the soak check (5 ships, >= 8 beats): tune placement/aeth instead.
- Executor vs big islands: SailToIsland parks at radius + ~420; push SailTo destinations (and launch approach point ExPt(0,400)) out of island footprints; detour around footprints. Pick sphere for islands ~0.9R and prefer ship hits.
- Water = static mesh with UVs along flow (add UV support to MeshBuilder) + small water shader with a time uniform (no per-frame Rlgl quads). Apply island bob to water/trails/nodes/monsters.
- Old saves: terrain regenerates from seed; after load move a landed captain to the new landing spot. Node-count tests need new numbers (keep: visible count shrinks as aeth drops).
- Look at each size class (orbit + on foot), one waterfall->river shot, one trail-up-mountain shot; time building the huge island (discovery hitch); rerun --bench after P3.
- Monsters are temporary state: not saved, cleared on takeoff/load, re-roll per landing; F to board must always work; test roll ~50% over many landings. Viewmodel: avoid disabling depth test (try depth-only clear via opengl32 glClear(0x100) or render tool to a render texture).
- Commit after every milestone; this log is the resume point.
