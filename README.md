# Space Blocks

A 3D space-colony management game for Windows. You are Commander Jason, aboard **The Executor**, a battered
wedge-shaped carrier that can sail among the floating islands. Your pilots mine Aetherite crystal and service their
ships at tanks in the hangar. You can also fly your own ship down to an island and mine ore by hand, then carry it back
to the Executor. Keep everyone loyal and alive through ion storms until you can reach **Haven**.

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

- **The crew.** Pilots (Mira, Tobias and Zeph at the start; Okoye joins at 150 Aetherite delivered, Pip at 500) fly to
  islands, mine with lasers and return to the hangar. On landing they walk to the stations they need: they unload
  cargo at the **Aetherite deposit tank**, refuel at the **fuel tank** and get repaired at the **repair gantry**.
  Nothing is serviced unless the pilot is standing at the station.
- **Loyalty.** Pilots who go too long without orders lose loyalty and eventually turn **independent**: they keep a
  larger share of what they mine and may refuse orders until you win them back.
- **Navigation.** The Executor can move. Select it with its chip in the command view, then click empty space or choose
  an island and sail there. Sailing burns Aetherite and stops at a 30-unit reserve, and pilots keep landing on the
  moving hangar. You can also use the helm at the commander's chair on the bridge.
- **Your own ship.** Board *Executor One* in the hangar, fly it by hand or lock onto an island and let the autopilot
  fly and land. Hand mining is done on foot: land on an island, walk to the glowing ore beside the crystals and hold E.
  Each chunk is 5 Aetherite taken from the island itself. Your pack holds 12 chunks. Fly home, dock, and drop the ore
  into the deposit tank. Refuel at the fuel station.
- **Explore the ship.** Walk the whole Executor in first person: hangar, corridor, bridge, crew quarters,
  engineering (with the reactor) and the cargo hold.
- **Storms and upgrades.** Ion storms roam the Reach and damage pilots. Spend Aetherite on per-ship upgrades (engine,
  laser, fuel tank, cargo bay, hull) and Executor upgrades (sensors, crew quarters).
- **Story.** A prologue crawl and a dozen milestone beats lead to a finale at Haven. The game continues as a sandbox.

Watch out: if you spend all your Aetherite on upgrades, docked ships may not be able to refuel, and the Executor will
refuse to sail below its reserve.

## Controls

**Command view (map)**

| Action | Input |
|---|---|
| Orbit / zoom / pan | drag left mouse / wheel (or Z, X) / W A S D (or click the radar) |
| Select pilot / ship / island | click (1-5 selects pilots, Tab cycles islands) |
| Lock a pilot onto an island | L, or right-click the island |
| Orders | R return, H hold, C scout, G auto |
| Executor | click its chip, then click empty space to set a course. L sails to the selected island, H halts |
| Scan for islands | Space |
| Upgrades / pilot cabin | U / V |
| Return to your body (bridge, flight or island) | B |
| Game speed / music | ] (x1, x3, x6) / M |
| Save / load / menu | F5 / F9 / Esc (autosaves every 10 seconds) |

**Inside the Executor (first person)**

| Action | Input |
|---|---|
| Move / look | WASD / mouse |
| Talk to a pilot / enter a pilot's ship | T or E / E |
| Board your own ship | E next to it (gold ring in the hangar) |
| Deposit ore / refuel your ship | hold E at the deposit tank / fuel tank |
| Use the helm | E at the commander's chair |
| Map | B |

**Flying your ship**

| Action | Input |
|---|---|
| Throttle / turn / climb / descend | W S / A D / Space / Ctrl |
| Choose a target island | Tab |
| Autopilot to target (lands automatically) / home (docks automatically) / cancel | G / R / X |
| Land on an island or dock with the Executor | F when the prompt shows |
| Map | N |

**On an island (on foot)**

| Action | Input |
|---|---|
| Move / run / look | WASD / Shift / mouse |
| Mine ore | hold E next to the glowing ore |
| Board your ship and lift off | F next to it |
| Map | N |

Your save is stored at `%APPDATA%\SpaceBlocks\save.json`.

## Tests and tools

The executable accepts a few command-line flags:

| Flag | What it does |
|---|---|
| `--selftest` | Headless checks of the simulation: orders, upgrades, Executor navigation, hangar stations, captain flight and mining, save/load and long soaks |
| `--uitest` | In-engine checks driven by injected mouse clicks: picking, helm, orders, walking, boarding, landing, mining, deposit, save/continue |
| `--audiotest` | Verifies the audio device and music stream |
| `--bench` | Frame-time benchmark at 1080p |
| `--demo <scenario> <out.png>` | Renders a scenario (for example `cmd`, `sail`, `stations`, `room-engineering`, `cap-fly`, `cap-eva`) to a screenshot |

## Code layout (`pc/`)

| File | Contents |
|---|---|
| `Data.cs`, `Sim.cs`, `Walk.cs` | Game data, simulation (including the Executor, stations and captain), save/load and walk collision (no rendering) |
| `Gfx.cs` | Merged-mesh builder, lighting shader, procedural textures |
| `Scene.cs`, `Interior.cs` | Procedural models: islands, the Executor, ships, crew, interior rooms |
| `Game.cs`, `Captain.cs`, `Hud.cs`, `Audio.cs` | Main loop and rendering, the captain's ship modes, UI and input, synthesized audio |
| `SelfTest.cs`, `Demo.cs` | Test and benchmark harnesses |

## Performance

One lighting shader, every model a single merged static mesh built once, a simulation step of roughly 0.01 ms,
additive billboards for glows and a single textured sphere for the sky. On an integrated Intel Iris Xe GPU at 1080p
with 4x MSAA it runs at about 1000 fps in the command view and 300-400 fps inside the ship.
