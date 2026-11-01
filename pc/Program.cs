using Raylib_cs;
using System.Numerics;
Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 720, "Space Blocks");
var cam = new Camera3D(new Vector3(6,5,6), Vector3.Zero, Vector3.UnitY, 55, CameraProjection.Perspective);
for (int f = 0; f < 5 && !Raylib.WindowShouldClose(); f++) {
  Raylib.BeginDrawing(); Raylib.ClearBackground(new Color(5,7,16,255));
  Raylib.BeginMode3D(cam); Raylib.DrawCube(Vector3.Zero, 2,2,2, Color.SkyBlue); Raylib.DrawGrid(10,1); Raylib.EndMode3D();
  Raylib.DrawText("hello " + Raylib.GetFPS(), 10, 10, 24, Color.White);
  Raylib.EndDrawing();
}
Raylib.TakeScreenshot("hello.png");
Console.WriteLine("GL ok");
Raylib.CloseWindow();
