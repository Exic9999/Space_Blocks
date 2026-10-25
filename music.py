"""Procedural ambient / cinematic music for Space Blocks.

Everything is synthesised with numpy: additive "church organ" pads, soft bell
arpeggios, a wistful lead line, sub-bass swells and an FFT-convolution reverb
with a synthetic decaying-noise impulse response.  All material (chords,
melodies, patterns) is original.

Each track is rendered as a *circular* buffer: note tails that run past the
loop end are folded back onto the start, and the reverb is a circular
convolution, so the files loop seamlessly.
"""

from __future__ import annotations

import os
import time
import wave

import numpy as np

SR = 44100
TABLE_N = 4096


# --------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------

def mtof(m: float) -> float:
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def _ramp(n: int) -> np.ndarray:
    """Raised-cosine ramp 0 -> 1 of n samples."""
    if n <= 0:
        return np.zeros(0)
    return 0.5 - 0.5 * np.cos(np.pi * np.arange(n) / n)


def _envelope(n: int, attack: int, release: int) -> np.ndarray:
    env = np.ones(n)
    attack = min(attack, n)
    release = min(release, n - attack)
    env[:attack] = _ramp(attack)
    if release > 0:
        env[n - release:] = _ramp(release)[::-1]
    return env


def _pan_gains(pan: float) -> tuple[float, float]:
    a = (pan + 1.0) * np.pi / 4.0
    return float(np.cos(a)), float(np.sin(a))


class _Mix:
    """Stereo buses of length L plus an overflow region that wraps to the start."""

    def __init__(self, length: int, extra: int):
        self.L = length
        self.E = extra
        self.buses: dict[str, np.ndarray] = {}

    def add(self, bus: str, start_sec: float, sig: np.ndarray, pan: float = 0.0, gain: float = 1.0):
        b = self.buses.get(bus)
        if b is None:
            b = self.buses[bus] = np.zeros((2, self.L + self.E))
        s = int(round(start_sec * SR)) % self.L
        sig = sig[: self.E]
        n = len(sig)
        gl, gr = _pan_gains(pan)
        b[0, s:s + n] += sig * (gl * gain)
        b[1, s:s + n] += sig * (gr * gain)

    def folded(self, bus: str) -> np.ndarray:
        b = self.buses.get(bus)
        if b is None:
            return np.zeros((2, self.L))
        out = b[:, : self.L].copy()
        out[:, : self.E] += b[:, self.L:]
        return out


# --------------------------------------------------------------------------
# instruments
# --------------------------------------------------------------------------

def _organ_table(bright: float) -> np.ndarray:
    """One cycle of an organ-like waveform. Fundamental of the table = note/2
    so harmonic 1 is the 16' rank, 2 = 8', 3 = quint, 4 = 4', ..."""
    ph = np.arange(TABLE_N + 1) / TABLE_N
    harms = [(1, 0.55), (2, 1.0), (3, 0.20), (4, 0.55),
             (6, 0.22), (8, 0.30), (10, 0.08), (12, 0.12), (16, 0.09), (20, 0.04), (24, 0.05), (32, 0.03)]
    tab = np.zeros(TABLE_N + 1)
    for h, a in harms:
        if h > 4:
            a *= bright
        elif h == 3 or h == 4:
            a *= 0.5 + 0.5 * bright
        tab += a * np.sin(2 * np.pi * h * ph)
    return tab / np.max(np.abs(tab))


def _pad_note(f: float, length: float, attack: float, release: float, table: np.ndarray,
              rng: np.random.Generator) -> list[tuple[np.ndarray, float]]:
    """Return a list of (mono signal, pan) voices forming a chorused organ note."""
    n = int((length + release) * SR)
    t = np.arange(n) / SR
    env = _envelope(n, int(attack * SR), int(release * SR))
    voices = []
    for cents, pan in ((-6.0, -0.75), (0.0, 0.0), (6.5, 0.75)):
        fv = 0.5 * f * 2.0 ** ((cents + rng.uniform(-1.5, 1.5)) / 1200.0)
        rate = rng.uniform(0.12, 0.35)
        depth = 0.0025
        phi = rng.uniform(0, 2 * np.pi)
        phase = fv * t - fv * depth / (2 * np.pi * rate) * np.cos(2 * np.pi * rate * t + phi)
        phase += rng.uniform()
        p = (phase % 1.0) * TABLE_N
        i = p.astype(np.int32)
        fr = p - i
        s = table[i] + fr * (table[i + 1] - table[i])
        # slow amplitude shimmer (chorus feel)
        s *= env * (1.0 + 0.12 * np.sin(2 * np.pi * rng.uniform(0.07, 0.2) * t + rng.uniform(0, 6.3)))
        voices.append((s, pan))
    return voices


def _bell(f: float, vel: float, tau: float, bright: float) -> np.ndarray:
    dur = min(tau * 5.0, 2.5)
    n = int(dur * SR)
    t = np.arange(n) / SR
    w = 2 * np.pi * f * t
    s = (np.sin(w) * np.exp(-t / tau)
         + 0.30 * np.sin(2.0 * w) * np.exp(-t / (tau * 0.45))
         + 0.10 * bright * np.sin(3.0 * w + 0.3) * np.exp(-t / (tau * 0.25))
         + 0.07 * bright * np.sin(5.43 * w) * np.exp(-t / 0.07)
         + 0.05 * bright * np.sin(8.2 * w) * np.exp(-t / 0.04))
    s *= _envelope(n, int(0.004 * SR), int(0.06 * SR))
    return s * vel


def _lead(f: float, dur: float, rng: np.random.Generator) -> np.ndarray:
    release = 0.9
    n = int((dur + release) * SR)
    t = np.arange(n) / SR
    vib_depth = 0.0045 * np.clip(t / 1.0, 0, 1)
    finst = f * (1.0 + vib_depth * np.sin(2 * np.pi * 5.1 * t + rng.uniform(0, 6.3)))
    ph = 2 * np.pi * np.cumsum(finst) / SR
    s = (np.sin(ph) + 0.38 * np.sin(2 * ph) + 0.16 * np.sin(3 * ph)
         + 0.07 * np.sin(4 * ph) + 0.04 * np.sin(5 * ph))
    attack = min(0.18, dur * 0.4)
    env = _envelope(n, int(attack * SR), int(release * SR))
    # gentle swell through the note
    env *= 0.8 + 0.2 * np.sin(np.pi * np.clip(t / max(dur, 0.1), 0, 1))
    return s * env


def _bass(f: float, length: float) -> np.ndarray:
    release = 1.6
    n = int((length + release) * SR)
    t = np.arange(n) / SR
    w = 2 * np.pi * f * t
    s = np.sin(w) + 0.22 * np.sin(2 * w) + 0.06 * np.sin(3 * w)
    return s * _envelope(n, int(length * 0.45 * SR), int(release * SR))


# --------------------------------------------------------------------------
# reverb
# --------------------------------------------------------------------------

def _impulse_response(rt60: float, rng: np.random.Generator, predelay: float = 0.025) -> np.ndarray:
    n = int(rt60 * 1.1 * SR)
    t = np.arange(n) / SR
    spec = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    ir = np.zeros(n)
    # frequency dependent decay: highs die faster (air / wall damping)
    bands = ((0, 700, 1.0), (700, 3000, 0.7), (3000, 8000, 0.42), (8000, SR, 0.22))
    for lo, hi, k in bands:
        mask = ((freqs >= lo) & (freqs < hi)).astype(float)
        band = np.fft.irfft(spec * mask, n=n)
        ir += band * np.exp(-6.9 * t / (rt60 * k))
    na = int(0.03 * SR)
    ir[:na] *= _ramp(na)  # soft onset
    # a few early reflections
    peak = np.max(np.abs(ir))
    for d, g in ((0.011, 0.5), (0.019, -0.35), (0.031, 0.3), (0.047, -0.22)):
        idx = int((d + rng.uniform(-0.002, 0.002)) * SR)
        ir[idx] += g * peak
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    return ir / np.sqrt(np.sum(ir ** 2))


def _circular_reverb(send: np.ndarray, rt60: float, rng: np.random.Generator) -> np.ndarray:
    L = send.shape[1]
    out = np.empty_like(send)
    for ch in range(2):
        ir = _impulse_response(rt60, rng)
        out[ch] = np.fft.irfft(np.fft.rfft(send[ch]) * np.fft.rfft(ir, n=L), n=L)
    return out


# --------------------------------------------------------------------------
# scores (all original)
# --------------------------------------------------------------------------

# chord = (voicing midi notes, bass root midi, length in beats)
TRACKS = {
    "title": dict(
        bpm=64.0, bars=16, seed=11, rt60=6.5, wet=1.25, pad_bright=0.55, pad_gain=1.0,
        chords=[
            ([57, 60, 64, 69], 33, 8),   # Am
            ([53, 57, 60, 67], 29, 8),   # Fadd9
            ([55, 60, 64, 67], 36, 8),   # C
            ([55, 59, 62, 67], 31, 8),   # G
            ([57, 60, 62, 65], 38, 8),   # Dm7
            ([57, 60, 64, 65], 29, 8),   # Fmaj7
            ([57, 59, 64, 69], 33, 8),   # Asus2
            ([56, 59, 64, 71], 28, 8),   # E
        ],
        arp=dict(steps_per_beat=2, octave=12, pattern=[0, 2, 4, 3, 5, 3, 4, 2],
                 tau=0.75, gain=0.20, bright=0.8),
        melody=[(69, 8, 2), (72, 10, 2), (76, 12, 4),
                (74, 16, 2), (76, 18, 1), (79, 19, 1), (76, 20, 4),
                (74, 24, 3), (71, 27, 1), (74, 28, 4),
                (77, 32, 2), (76, 34, 2), (74, 36, 2), (72, 38, 2),
                (69, 40, 3), (72, 43, 1), (77, 44, 4),
                (76, 48, 6), (71, 54, 2),
                (71, 56, 4)],
        lead_gain=0.32, pulse=None,
    ),
    "cruise": dict(
        bpm=102.4, bars=32, seed=23, rt60=5.0, wet=1.0, pad_bright=0.45, pad_gain=0.75,
        chords=[
            ([57, 62, 64, 65], 38, 8),   # Dm9
            ([58, 62, 65, 69], 34, 8),   # Bbmaj7
            ([58, 62, 65, 67], 31, 8),   # Gm7
            ([57, 62, 64, 69], 33, 8),   # Asus4
            ([57, 60, 64, 65], 29, 8),   # Fmaj7
            ([55, 60, 62, 64], 36, 8),   # Cadd9
            ([55, 58, 62, 65], 34, 8),   # Bb6
            ([57, 61, 64, 69], 33, 8),   # A
        ] * 2,
        arp=dict(steps_per_beat=2, octave=12, pattern=[0, 1, 2, 3, 4, 3, 2, 1],
                 tau=0.55, gain=0.24, bright=1.0),
        melody=[(69, 66, 2), (74, 68, 4),
                (77, 72, 4), (76, 76, 2), (74, 78, 2),
                (74, 80, 6), (70, 86, 2),
                (69, 88, 7),
                (72, 96, 4), (69, 100, 2), (72, 102, 2),
                (76, 104, 4), (74, 108, 2), (76, 110, 2),
                (77, 112, 4), (74, 116, 4),
                (76, 120, 4), (73, 124, 2)],
        lead_gain=0.26, pulse=None,
    ),
    "alert": dict(
        bpm=128.0, bars=24, seed=37, rt60=3.6, wet=0.8, pad_bright=0.3, pad_gain=0.7,
        chords=[
            ([52, 55, 59, 64], 28, 8),   # Em
            ([53, 57, 60, 65], 29, 8),   # F (phrygian b2)
            ([52, 55, 59, 64], 28, 8),   # Em
            ([52, 55, 60, 64], 36, 8),   # C
            ([52, 57, 60, 64], 33, 8),   # Am
            ([53, 58, 62, 65], 34, 8),   # Bb
            ([52, 55, 59, 64], 28, 8),   # Em
            ([53, 57, 60, 65], 29, 8),   # F
            ([52, 55, 60, 64], 36, 8),   # C
            ([52, 57, 60, 64], 33, 8),   # Am
            ([53, 57, 60, 65], 29, 8),   # F
            ([54, 59, 63, 66], 35, 8),   # B (dominant tension)
        ],
        arp=dict(steps_per_beat=4, octave=0, pattern=[0, 4, 2, 4, 1, 4, 3, 5],
                 tau=0.22, gain=0.22, bright=1.0),
        melody=[(71, 0.5, 7), (72, 8.5, 7), (71, 16.5, 7), (76, 24.5, 7),
                (76, 32.5, 7), (77, 40.5, 7), (71, 48.5, 7), (72, 56.5, 7),
                (76, 64.5, 7), (72, 72.5, 7), (72, 80.5, 7), (75, 88.5, 7)],
        lead_gain=0.18, pulse=dict(steps_per_beat=2, tau=0.16, gain=0.55),
    ),
}

DURATIONS = {name: spec["bars"] * 4 * 60.0 / spec["bpm"] for name, spec in TRACKS.items()}


# --------------------------------------------------------------------------
# rendering
# --------------------------------------------------------------------------

def _render(spec: dict) -> np.ndarray:
    rng = np.random.default_rng(spec["seed"])
    beat = 60.0 / spec["bpm"]
    L = int(round(spec["bars"] * 4 * beat * SR))
    loop_sec = L / SR
    mix = _Mix(L, int(12 * SR))

    def dyn(t_sec: float) -> float:
        """Periodic dynamic arc: quiet at the seam, swelling mid-loop."""
        return 0.62 + 0.38 * 0.5 * (1 - np.cos(2 * np.pi * (t_sec % loop_sec) / loop_sec))

    # ---- pads + sub-bass -------------------------------------------------
    pos = 0.0
    overlap = 1.6
    for voicing, root, beats in spec["chords"]:
        start = pos * beat
        length = beats * beat
        d = dyn(start + length / 2)
        table = _organ_table(spec["pad_bright"] + 0.5 * (d - 0.62))
        for note in voicing:
            for sig, pan in _pad_note(mtof(note), length + overlap, attack=1.4,
                                      release=2.2, table=table, rng=rng):
                mix.add("pad", start - overlap * 0.5, sig, pan, 0.11 * spec["pad_gain"] * d)
        bass = _bass(mtof(root), length + 0.4)
        mix.add("bass", start, bass, 0.0, 0.32 * (0.7 + 0.3 * d))
        mix.add("bass", start, _bass(mtof(root + 12), length + 0.4), 0.0, 0.10 * d)
        pos += beats

    # ---- arpeggio ostinato ------------------------------------------------
    arp = spec["arp"]
    step = beat / arp["steps_per_beat"]
    pos = 0.0
    k = 0
    for voicing, root, beats in spec["chords"]:
        tones = sorted(voicing)
        tones = [n + arp["octave"] for n in tones + [v + 12 for v in tones]]
        nsteps = int(round(beats * arp["steps_per_beat"]))
        for j in range(nsteps):
            t0 = pos * beat + j * step
            note = tones[arp["pattern"][k % len(arp["pattern"])] % len(tones)]
            accent = 1.0 if (k % arp["steps_per_beat"]) == 0 else 0.72
            vel = accent * (0.55 + 0.45 * dyn(t0)) * rng.uniform(0.9, 1.05)
            sig = _bell(mtof(note), vel, arp["tau"], arp["bright"])
            pan = 0.35 * np.sin(k * 0.9)
            mix.add("arp", t0 + rng.uniform(0, 0.004), sig, pan, arp["gain"])
            k += 1
        pos += beats

    # ---- pulse (alert) ------------------------------------------------------
    pulse = spec.get("pulse")
    if pulse:
        step = beat / pulse["steps_per_beat"]
        pos = 0.0
        k = 0
        for voicing, root, beats in spec["chords"]:
            for j in range(int(beats * pulse["steps_per_beat"])):
                t0 = pos * beat + j * step
                vel = (1.0 if j % 2 == 0 else 0.6) * (0.6 + 0.4 * dyn(t0))
                sig = _bell(mtof(root + 12), vel, pulse["tau"], 0.3)
                mix.add("pulse", t0, sig, 0.0, pulse["gain"])
                k += 1
            pos += beats

    # ---- lead melody --------------------------------------------------------
    for note, b0, blen in spec["melody"]:
        t0 = b0 * beat
        d = 0.75 + 0.25 * dyn(t0)
        for cents, pan in ((-3.0, -0.3), (3.0, 0.3)):
            sig = _lead(mtof(note) * 2 ** (cents / 1200.0), blen * beat, rng)
            mix.add("lead", t0, sig, pan, spec["lead_gain"] * d)

    # ---- fold, effects, reverb ---------------------------------------------
    pad = mix.folded("pad")
    bass = mix.folded("bass")
    arp_b = mix.folded("arp")
    pulse_b = mix.folded("pulse")
    lead = mix.folded("lead")

    # ping-pong delay on the arpeggio (circular roll keeps the loop seamless)
    d1 = int(round(beat * 0.75 * SR))
    pp = arp_b.copy()
    pp[0] += 0.32 * np.roll(arp_b[1], d1)
    pp[1] += 0.32 * np.roll(arp_b[0], 2 * d1)
    pp[0] += 0.14 * np.roll(arp_b[1], 3 * d1)
    arp_b = pp

    dry = 0.55 * pad + bass + 0.8 * arp_b + 0.8 * pulse_b + 0.75 * lead
    send = 0.9 * pad + 0.06 * bass + 0.6 * arp_b + 0.25 * pulse_b + 0.55 * lead
    wet = _circular_reverb(send, spec["rt60"], rng)
    out = dry + spec["wet"] * wet

    # mild mid/side widening
    mid = 0.5 * (out[0] + out[1])
    side = 0.5 * (out[0] - out[1]) * 1.1
    out = np.stack([mid + side, mid - side])

    out -= out.mean(axis=1, keepdims=True)
    out *= 0.8 / np.max(np.abs(out))
    return out


def _write_wav(path: str, stereo: np.ndarray):
    data = np.clip(stereo.T, -1.0, 1.0)
    pcm = (data * 32767.0).astype("<i2")
    tmp = path + ".tmp"
    with wave.open(tmp, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    os.replace(tmp, path)


def generate_tracks(out_dir: str, progress=None) -> dict[str, str]:
    """Render tracks to 44100 Hz, 16-bit, stereo WAV files in out_dir (create it). Return {name: path}.
    Skip rendering any file that already exists. Call progress(msg: str) if given, before each track."""
    os.makedirs(out_dir, exist_ok=True)
    paths = {}
    for name, spec in TRACKS.items():
        path = os.path.join(out_dir, f"{name}.wav")
        paths[name] = path
        if progress:
            progress(f"Composing music: {name}...")
        if os.path.exists(path):
            continue
        _write_wav(path, _render(spec))
    return paths


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "music")
    t_all = time.perf_counter()
    last = [t_all]

    def _progress(msg):
        now = time.perf_counter()
        print(f"[{now - t_all:6.2f}s] {msg}", flush=True)
        last[0] = now

    result = generate_tracks(out, _progress)
    print(f"total: {time.perf_counter() - t_all:.2f}s")
    for k, v in result.items():
        print(f"  {k}: {v}")
