// Procedural headshot portraits for the crew, the captain and the ship AI (used on pilot chips and in the message box).
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    // idx 0-4 = crew, 5 = the commander, -1 = the Executor's ship AI
    void DrawPortrait(Rectangle r, int idx)
    {
        float s = r.Width; float cx = r.X + s / 2, cy = r.Y + s * .46f;
        uint suit, skin, accent; if (idx < 0) { suit = 0x1a4a7a; skin = 0x5adcff; accent = 0xffffff; } else { var cd = idx == 5 ? Data.Captain : Data.Crew[idx]; suit = cd.Color; skin = cd.Skin; accent = cd.Acc; }
        Raylib.DrawRectangleRec(r, Col.Mul(Rgb(suit), .22f)); Raylib.DrawRectangleGradientV((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height, Col.Mul(Rgb(suit), .35f), Col.Mul(Rgb(suit), .12f));
        if (idx < 0)
        {   // hologram bust for the ship AI
            for (int k = 0; k < 4; k++) Raylib.DrawCircleLinesV(new Vector2(cx, cy), s * (.14f + k * .06f), Col.Hex(0x5adcff, (byte)(200 - k * 45)));
            Raylib.DrawCircleV(new Vector2(cx, cy), s * .07f, Col.Hex(0xc8f4ff)); Raylib.DrawRectangleLinesEx(r, 1, Edge); return;
        }
        // shoulders
        Raylib.DrawTriangle(new Vector2(r.X + s * .08f, r.Y + s), new Vector2(r.X + s * .92f, r.Y + s), new Vector2(cx, r.Y + s * .66f), Col.Mul(Rgb(suit), .85f));
        Raylib.DrawRectangle((int)(cx - s * .06f), (int)(cy + s * .16f), (int)(s * .12f), (int)(s * .14f), Col.Mul(Rgb(skin), .85f));
        // hair / headgear behind the face
        switch (idx)
        {
            case 0: Raylib.DrawEllipse((int)cx, (int)(cy - s * .02f), s * .27f, s * .31f, Rgb(0x22141a)); Raylib.DrawRectangle((int)(cx - s * .27f), (int)cy, (int)(s * .12f), (int)(s * .3f), Rgb(0x22141a)); Raylib.DrawRectangle((int)(cx + s * .15f), (int)cy, (int)(s * .12f), (int)(s * .3f), Rgb(0x22141a)); break;
            case 4: for (int k = -2; k <= 2; k++) Raylib.DrawTriangle(new Vector2(cx + k * s * .09f - s * .06f, cy - s * .2f), new Vector2(cx + k * s * .09f + s * .06f, cy - s * .2f), new Vector2(cx + k * s * .11f, cy - s * .42f), Rgb(accent)); break;
            case 2: Raylib.DrawEllipse((int)cx, (int)(cy - s * .1f), s * .25f, s * .2f, Rgb(0x3a2a20)); break;
        }
        Raylib.DrawEllipse((int)cx, (int)cy, s * .2f, s * .24f, Rgb(skin));
        // headgear in front
        switch (idx)
        {
            case 1: Raylib.DrawRectangle((int)(cx - s * .23f), (int)(cy - s * .27f), (int)(s * .46f), (int)(s * .12f), Rgb(0xe8d34a)); Raylib.DrawRectangle((int)(cx - s * .1f), (int)(cy - s * .12f), (int)(s * .2f), (int)(s * .07f), Rgb(0x222a3a)); break;
            case 3: Raylib.DrawRectangle((int)(cx - s * .22f), (int)(cy - s * .3f), (int)(s * .44f), (int)(s * .17f), Rgb(0xe0b84a)); Raylib.DrawRectangle((int)(cx - s * .22f), (int)(cy - s * .22f), (int)(s * .44f), (int)(s * .05f), Rgb(0x2a8a4a)); break;
            case 5: Raylib.DrawRectangle((int)(cx - s * .22f), (int)(cy - s * .3f), (int)(s * .44f), (int)(s * .13f), Rgb(0x3f4a5e)); Raylib.DrawRectangle((int)(cx - s * .05f), (int)(cy - s * .3f), (int)(s * .1f), (int)(s * .1f), Rgb(0xe0b84a)); break;
            case 2: Raylib.DrawCircleV(new Vector2(r.X + s * .84f, r.Y + s * .2f), s * .07f, Rgb(0xffd890)); break;
        }
        var eye = new Color((byte)30, (byte)25, (byte)30, (byte)255); Raylib.DrawCircleV(new Vector2(cx - s * .08f, cy + s * .02f), s * .028f, eye); Raylib.DrawCircleV(new Vector2(cx + s * .08f, cy + s * .02f), s * .028f, eye);
        Raylib.DrawRectangle((int)(cx - s * .05f), (int)(cy + s * .13f), (int)(s * .1f), Math.Max(1, (int)(s * .02f)), Col.Mul(Rgb(skin), .55f));
        Raylib.DrawRectangleLinesEx(r, 1, Edge);
    }
}
