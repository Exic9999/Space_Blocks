using SpaceBlocks;

if (args.Contains("--selftest")) return SelfTest.Run();
var game = new Game();
int di = Array.IndexOf(args, "--demo");
if (di >= 0 && args.Length > di + 2) return game.RunDemo(args[di + 1], args[di + 2]);
if (args.Contains("--bench")) return game.RunBench();
if (args.Contains("--boxtest")) return game.RunBoxTest();
if (args.Contains("--uitest")) return game.RunUiTest();
if (args.Contains("--audiotest")) return game.RunAudioTest();

game.Init(true, 1280, 720, true);
game.Boot();
game.Run();
Raylib_cs.Raylib.CloseWindow();
return 0;
