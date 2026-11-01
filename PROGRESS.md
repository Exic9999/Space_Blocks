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
