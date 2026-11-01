using SpaceBlocks;
if (args.Contains("--selftest")) return SelfTest.Run();
Console.WriteLine("Game UI not built yet. Use --selftest.");
return 0;
