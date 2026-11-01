// Static game data: crew, lore, constants. Ported from mobile3d/index.html.
namespace SpaceBlocks;

public sealed class CrewDef
{
    public string Name = "", Call = "", Bio = "";
    public uint Color, Hull, Acc, Skin;
    public float Bold;
    public string[] Lines = Array.Empty<string>();
}

public sealed class Chapter
{
    public string Title = "", Sub = "";
    public string[] Paras = Array.Empty<string>();
}

public static class Data
{
    public const float W = 3200f;
    public const float CX = 1600f, CY = 1600f; // world centre (island ring + Executor start)
    public const float ES = 6f; // exterior ship scale

    public static readonly CrewDef[] Crew =
    {
        new() { Name="Mira Voss", Call="Vixen", Color=0xff5a6e, Hull=0xb3202f, Acc=0x16181f, Skin=0xe0a98a, Bold=1f,
            Bio="Former champion of the Kestrel Run, grounded after she threaded a patrol cordon at full burn. Flies fast, talks faster, and has never once turned back early.",
            Lines=new[]{"Aetherite never sleeps, Commander. Neither do I.","I'll take the far rocks. Someone has to.","Tell the patrol I said hello. From behind."} },
        new() { Name="Tobias Kline", Call="Mule", Color=0xffc24a, Hull=0xc98a20, Acc=0xe8d34a, Skin=0xb98a68, Bold=.3f,
            Bio="A foundry-moon shipwright's son. He built the Mule from salvage and sketches blueprints on every spare surface. Believes there is no problem a bigger cargo bay cannot fix.",
            Lines=new[]{"Give me a bigger tank and I'll haul you a mountain.","Engines first, speeches later.","I drew up a better hangar crane. Want to see?"} },
        new() { Name="Zeph", Call="Lantern", Color=0x5ad0ff, Hull=0x2a8aa8, Acc=0x9ff4ff, Skin=0xf0cfb0, Bold=.6f,
            Bio="A cartographer's apprentice who thinks the islands are slowly alive. Hangs a lantern on every ship he flies so the islands know someone is visiting.",
            Lines=new[]{"The waterfalls here fall upward at night. I swear it.","I only mined a little. The rest I watched.","The island hummed when I landed. Did you hear it?"} },
        new() { Name="Okoye Adeyemi", Call="Bulwark", Color=0x7ee08a, Hull=0x3a7a4a, Acc=0xe0b84a, Skin=0x6b4a38, Bold=.5f,
            Bio="Retired convoy captain who lost her fleet in the Ion Wars. Careful, patient and exact. She still flies her old fleet's flag from the tail fin.",
            Lines=new[]{"Old hands, steady burn. Reporting for duty, Commander.","Check the shields twice. Then check them again.","I have buried enough crews. Not this one."} },
        new() { Name="Pip", Call="Spark", Color=0xe08aff, Hull=0x8a3ac8, Acc=0xff7acb, Skin=0xe8b898, Bold=1f,
            Bio="A seventeen-year-old stowaway from the mining rings who patched together a ship from scrap. Reckless, loud engines, quietly thrilled to be on a real crew.",
            Lines=new[]{"Is it true you can outrun a storm? I can!","I put a third engine on. Don't tell Tobias.","Is that a real bed? Mine's a hammock!"} },
    };

    public static readonly string[] IslandNames =
        { "Halcyon","Veil","Cirrus","Ember","Mirage","Tern","Lull","Aster","Brume","Kestrel","Solace","Quill","Drift","Fern" };

    // hangar pad positions (x,z) and bridge seat spots (x,z)
    public static readonly float[][] Pads = { new[]{-10f,22f}, new[]{10f,22f}, new[]{-10f,10f}, new[]{10f,10f}, new[]{0f,16f} };
    public static readonly float[][] BSpot = { new[]{-8f,-31f}, new[]{-2.5f,-31f}, new[]{3f,-31f}, new[]{8.5f,-31f}, new[]{0f,-24f} };

    public static readonly Chapter Prologue = new()
    {
        Title = "Space Blocks", Sub = "The Long Drift",
        Paras = new[]{
            "The Aetherite Wars are over. Nobody won. The old fleets burned through every reserve of the crystal that bends light and feeds every engine in the Reach, and the worlds they fought over went dark.",
            "Now only the floating islands remain, drifting through the void with waterfalls that never reach the ground and crystal that grows where the water falls.",
            "Commander Jason has one carrier left: the Executor, a battered wedge of a ship with an open hangar, thirsty tanks and five empty berths. Three pilots have signed on. They are not soldiers. They are a racer, a shipwright's son and a dreamer with a lantern.",
            "Ion storms roll through the Reach. Fuel runs thin. Pilots left alone too long begin to look after themselves.",
            "Somewhere beyond the charted islands lies Haven, a place the storms are said never to touch. To reach it, the Executor must be fed.",
            "Give the orders, Commander. The hangar doors are open." }
    };

    public static readonly Chapter Epilogue = new()
    {
        Title = "Haven", Sub = "A New Reach",
        Paras = new[]{
            "The Executor's tanks are full for the first time since the wars. Aetherite light spills from every seam of her hull.",
            "Beyond the last uncharted island the storms thin, then stop. Haven turns slowly in the dark, green and quiet, with waterfalls that fall in both directions.",
            "Mira circles it twice for the fun of it. Tobias is already sketching a dock. Zeph says the island hummed his name. Okoye lowers her old fleet's flag, then raises it again. Pip asks if this one is real.",
            "It is real, Commander. And you are the one who brought them here.",
            "The Reach is still out there. The islands keep drifting and the crystal keeps growing. The hangar doors are open." }
    };

    // upgrade tables: key -> (label, base cost, max level or 0, description)
    public static readonly (string key, string label, int cost, int max, string desc)[] ExecUp =
    {
        ("sensors", "Sensor Array", 100, 4, "Wider scan and detection"),
        ("quarters", "Crew Quarters", 140, 3, "Crew stay loyal longer"),
    };
    public static readonly (string key, string label, int cost, string desc)[] ShipUp =
    {
        ("eng", "Engine", 25, "Faster flight"),
        ("las", "Laser", 30, "Faster mining"),
        ("tank", "Fuel tank", 20, "Longer range"),
        ("car", "Cargo bay", 20, "Bigger loads"),
        ("hull", "Hull plating", 20, "Withstand ion storms"),
    };
    public static int Cost(int baseCost, int lv) => (int)Math.Round(baseCost * (1 + lv * .8));
}
