// Procedural audio: synthesized ambient music loop (pad + arpeggio + echo) and UI blips. No asset files.
using System.Runtime.InteropServices;
using Raylib_cs;
namespace SpaceBlocks;

public sealed unsafe partial class Game
{
    bool audioOk, musicOn; Music music; nint musicMem; readonly Dictionary<string, Sound> sfx = new();
    int lastFound, lastLaunches, lastShips;

    void InitAudio()
    {
        try { Raylib.InitAudioDevice(); audioOk = Raylib.IsAudioDeviceReady(); } catch { audioOk = false; }
        if (!audioOk) return;
        var wav = SynthMusicWav(); musicMem = Marshal.AllocHGlobal(wav.Length); Marshal.Copy(wav, 0, musicMem, wav.Length);
        sbyte* ext = stackalloc sbyte[5] { (sbyte)46, (sbyte)119, (sbyte)97, (sbyte)118, 0 }; music = Raylib.LoadMusicStreamFromMemory(ext, (byte*)musicMem, wav.Length); music.Looping = true; Raylib.SetMusicVolume(music, .55f);
        sfx["scan"] = Blip(700, .3f); sfx["up"] = Blip(520, .25f); sfx["up2"] = Blip(440, .25f); sfx["order"] = Blip(880, .12f); sfx["found"] = Blip(1040, .35f); sfx["launch"] = Blip(330, .4f); sfx["join"] = Blip(660, .5f);
    }
    void ShutdownAudio() { if (!audioOk) return; Raylib.UnloadMusicStream(music); foreach (var s in sfx.Values) Raylib.UnloadSound(s); Raylib.CloseAudioDevice(); Marshal.FreeHGlobal(musicMem); audioOk = false; }
    public void SetMusic(bool on) { musicOn = on; if (!audioOk) return; if (on) Raylib.PlayMusicStream(music); else Raylib.PauseMusicStream(music); }
    void ToggleMusic() => SetMusic(!musicOn);
    void Play(string name) { if (audioOk && sfx.TryGetValue(name, out var s)) Raylib.PlaySound(s); }

    void UpdateAudio(float dt)
    {
        if (!audioOk) return; if (musicOn) Raylib.UpdateMusicStream(music);
        var G = sim.G; if (mode == Mode.Title) { lastFound = G.found; lastLaunches = G.launches; lastShips = G.ships.Count; return; }
        if (G.found > lastFound) Play("found"); if (G.launches > lastLaunches) Play("launch"); if (G.ships.Count > lastShips) Play("join");
        lastFound = G.found; lastLaunches = G.launches; lastShips = G.ships.Count;
    }

    static Sound Blip(float freq, float secs)
    {
        const int sr = 22050; int n = (int)(sr * secs); var w = new Wave { FrameCount = (uint)n, SampleRate = (uint)sr, SampleSize = 16, Channels = 1 };
        short* d = (short*)Raylib.MemAlloc((uint)(n * 2)); w.Data = d;
        for (int i = 0; i < n; i++) { float t = i / (float)sr, env = MathF.Exp(-t * 14f / secs * .5f) * MathF.Min(1, t * 200); d[i] = (short)(MathF.Sin(MathF.Tau * freq * t) * env * 9000); }
        var s = Raylib.LoadSoundFromWave(w); Raylib.UnloadWave(w); return s;
    }

    // 24 s loop: four chords, slow saw pad through a low-pass, triangle arpeggio, feedback echo (wraps for a seamless loop).
    static byte[] SynthMusicWav()
    {
        const int sr = 22050; float step = .75f; int notesPerChord = 8; int[][] chords = { new[]{57,60,64,71}, new[]{53,57,60,67}, new[]{48,52,55,62}, new[]{55,59,62,69} };
        int total = (int)(sr * step * notesPerChord * chords.Length); var dry = new float[total]; float F(int m) => 440f * MathF.Pow(2, (m - 69) / 12f);
        int n = 0;
        for (int ci = 0; ci < chords.Length; ci++)
            for (int k = 0; k < notesPerChord; k++, n++)
            {
                int start = (int)(n * step * sr); var ch = chords[ci];
                if (k == 0) foreach (int m in ch)
                    {
                        float f = F(m - 12), lp = 0; int len = (int)(6f * sr);
                        for (int i = 0; i < len; i++)
                        {
                            float t = i / (float)sr, env = t < 1.6f ? t / 1.6f * .05f : MathF.Max(0, .05f * (1 - (t - 1.6f) / 4.2f)); float saw = 2 * ((f * t) % 1f) - 1;
                            lp += (saw - lp) * .17f; dry[(start + i) % total] += lp * env;
                        }
                    }
                int note = ch[k % 4] + 12 * (k % 2 == 1 ? 1 : 2); float fa = F(note); int alen = (int)(.8f * sr);
                for (int i = 0; i < alen; i++)
                {
                    float t = i / (float)sr, env = t < .02f ? t / .02f * .09f : .09f * MathF.Exp(-(t - .02f) * 6.5f); float tri = MathF.Abs(2 * ((fa * t) % 1f) - 1) * 2 - 1;
                    dry[(start + i) % total] += tri * env;
                }
            }
        int delay = (int)(.45f * sr); var wet = new float[total];
        for (int pass = 0; pass < 4; pass++) for (int i = 0; i < total; i++) { wet[i] = dry[i] + .5f * wet[(i - delay + total) % total]; }
        var pcm = new short[total]; float peak = 1e-5f; for (int i = 0; i < total; i++) peak = MathF.Max(peak, MathF.Abs(wet[i]));
        for (int i = 0; i < total; i++) pcm[i] = (short)(Math.Clamp(wet[i] / peak * .8f, -1f, 1f) * 32000);
        using var ms = new MemoryStream(); using var bw = new BinaryWriter(ms);
        bw.Write("RIFF"u8); bw.Write(36 + total * 2); bw.Write("WAVEfmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16); bw.Write("data"u8); bw.Write(total * 2);
        foreach (var s in pcm) bw.Write(s); return ms.ToArray();
    }
}
