# Space Blocks

A 3D space-colony management game for Windows. You are Commander Jason, aboard **The Executor**, a battered wedge-shaped
carrier drifting among floating islands. Send your pilots out to mine Aetherite crystal, watch them land in the hangar,
walk the bridge with your crew, and keep everyone loyal and alive through ion storms until you can reach **Haven**.

Written in C# on .NET 8 with [Raylib](https://www.raylib.com/) (OpenGL 3.3). Everything is procedural: there are no
asset files. Models are built at startup, and the music and sound effects are synthesized.

## Play

Build and run (needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)):

```
cd pc
dotnet run -c Release
```

Or make a self-contained build that needs no .NET install:

```
cd pc
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o dist/SpaceBlocks
dist\SpaceBlocks\SpaceBlocks.exe
```

## How it works

- Pilots (Mira, Tobias and Zeph at the start; Okoye joins at 150 Aetherite delivered, Pip at 500) fly to islands, mine
  with lasers, and return to dock in the hangar to deposit cargo, refuel and repair.
- Pilots who go too long without orders lose loyalty and eventually turn **independent**: they keep a larger share of
  what they mine and may refuse orders until you win them back.
- Ion storms roam the Reach and damage ships. Hull plating helps; calling pilots home helps more.
- Spend Aetherite on per-ship upgrades (engine, laser, fuel tank, cargo bay, hull) and on Executor upgrades (sensors,
  crew quarters).
- A story crawl and a dozen milestone beats lead to a finale at Haven. The game continues as a sandbox afterwards.

Watch out: if you spend all your Aetherite on upgrades, docked ships may not be able to refuel.

## Controls

| Action | Input |
|---|---|
| Orbit / zoom | drag left mouse / mouse wheel (or Z, X) |
| Pan | W A S D, or click the radar |
| Select pilot / ship / island | click (1-5 selects pilots, Tab cycles islands) |
| Lock a pilot onto an island | L, or right-click the island |
| Orders | R return, H hold, C scout, G auto |
| Scan for islands | Space |
| Upgrades / pilot cabin | U / V |
| Walk the Executor | B, then WASD and mouse look. E enters a ship, T talks, B returns to the map |
| Game speed | ] (x1, x3, x6) |
| Music | M |
| Save / load / menu | F5 / F9 / Esc (autosaves every 10 seconds) |

Your save is stored at `%APPDATA%\SpaceBlocks\save.json`.

## Tests and tools

The executable accepts a few command-line flags:

| Flag | What it does |
|---|---|
| `--selftest` | Headless checks of the simulation, save/load, walk collision and a 90-minute soak |
| `--uitest` | In-engine checks of picking, orders, walk mode, cabins, save/continue, the crawl and overlay click blocking |
| `--audiotest` | Verifies the audio device and music stream |
| `--bench` | Frame-time benchmark at 1080p |
| `--demo <scenario> <out.png>` | Renders a scenario (for example `cmd`, `island`, `walk-hangar`, `cabin`) to a screenshot |

## Code layout (`pc/`)

| File | Contents |
|---|---|
| `Data.cs`, `Sim.cs`, `Walk.cs` | Game data, simulation, save/load and walk collision (no rendering) |
| `Gfx.cs` | Merged-mesh builder, lighting shader, procedural textures |
| `Scene.cs`, `Interior.cs` | Procedural models: islands, the Executor, ships, crew, interiors |
| `Game.cs`, `Hud.cs`, `Audio.cs` | Main loop, rendering, UI and input, synthesized audio |
| `SelfTest.cs`, `Demo.cs` | Test and benchmark harnesses |

## Performance

One lighting shader, every model a single merged static mesh built once, a simulation step of roughly 0.01 ms, additive
billboards for glows and a single textured sphere for the sky. On an integrated Intel Iris Xe GPU at 1080p with 4x MSAA
it runs at about 1000 fps in the command view and 300-400 fps inside the ship.
