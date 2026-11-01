# Space Blocks — native Windows build (C# / .NET 8 / Raylib)

A native port of the 3D web game (`../mobile3d`). Same story, crew, economy and ion storms; rendered with OpenGL 3.3
through Raylib. Procedural everything: no asset files, meshes are built at startup, music and sound are synthesized.

## Run
- Prebuilt: `pc\dist\SpaceBlocks\SpaceBlocks.exe` (self-contained, no .NET install needed; rebuild with the publish command below).
- From source (needs the .NET 8 SDK; if `dotnet` is not on PATH use the user-scope install: `$env:PATH="$env:LOCALAPPDATAdotnet8;$env:PATH"`): `cd pc; dotnet run -c Release`

## Controls
| | |
|---|---|
| Orbit / zoom | drag left mouse / wheel (Z, X) |
| Pan | WASD (or click the radar) |
| Select | click a pilot chip, ship, or island (1-5 = pilots, Tab = cycle islands) |
| Orders | LOCK ON (L or right-click island), RETURN R, HOLD H, SCOUT C, AUTO G |
| Scan / upgrades / cabin | Space / U / V |
| Walk the Executor | B (WASD + mouse; E enter a ship, T talk, B back) |
| Speed / music | ] / M |
| Save / load / menu | F5 / F9 / Esc (autosaves every 10 s to `%APPDATA%\SpaceBlocks\save.json`) |

## Tests and tools
`SpaceBlocks.exe` (or `dotnet bin/Release/net8.0/SpaceBlocks.dll`) accepts:
`--selftest` (headless sim/save/walk, 20 checks) · `--uitest` (in-engine picking, orders, walk, cabin, save, crawl: 22 checks) ·
`--audiotest` · `--bench` (1080p frame times) · `--demo <scenario> out.png` (screenshots; scenarios in `Demo.cs`).

## Publish
`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -o dist\SpaceBlocks`

## Layout
Data/Sim/Walk (game logic, no rendering) · Gfx (mesh builder, lit shader, textures) · Scene/Interior (procedural models) ·
Game (loop, camera, rendering) · Hud (UI, input, screens) · Audio · Demo/SelfTest (harnesses).

## Efficiency notes
One lighting shader; every model is a single merged static mesh built once; simulation is a fixed-cost step (~0.01 ms);
glows are additive billboards; the sky is one textured sphere. On an Intel Iris Xe laptop GPU at 1080p with 4x MSAA:
~1000 fps in the command view, 300-420 fps inside the ship (vsync on by default, so CPU/GPU mostly idle).
