"""Music player (crossfading loops from music.py) and tiny synthesized sfx. Safe with no audio device."""
import threading
import numpy as np
import pygame
from config import MUSIC_DIR


def _tone(freqs, dur, vol=0.4, decay=5.0, sr=44100):
    t = np.arange(int(sr * dur)) / sr
    y = np.zeros_like(t)
    for f, start in freqs:
        m = t >= start
        tt = t[m] - start
        y[m] += np.sin(2 * np.pi * f * tt) * np.exp(-decay * tt) * np.minimum(1, tt * 200)
    y = np.clip(y * vol, -1, 1)
    st = np.stack([y, y], axis=1)
    return (st * 32767).astype(np.int16)


class Music:
    def __init__(self):
        self.ok = True
        self.enabled = True
        self.ready = False
        self.volume = 0.55
        self.sounds, self.sfx = {}, {}
        self.current = None
        self.want = "title"
        try:
            pygame.mixer.init(44100, -16, 2, 1024)
        except Exception as e:
            print(f"[audio] disabled: {e}", flush=True)
            self.ok = False
            return
        for name, notes, dur, vol, dec in (
            ("ping", [(880, 0), (1320, 0.08)], 0.6, 0.3, 6),
            ("found", [(660, 0), (880, 0.1), (1320, 0.2)], 0.9, 0.3, 4),
            ("dock", [(110, 0), (165, 0.02)], 0.5, 0.5, 9),
            ("upgrade", [(523, 0), (659, 0.1), (784, 0.2), (1046, 0.3)], 1.2, 0.3, 3.5),
            ("alert", [(440, 0), (330, 0.18), (440, 0.36)], 0.8, 0.3, 4),
            ("click", [(1200, 0)], 0.1, 0.2, 40),
        ):
            try:
                self.sfx[name] = pygame.mixer.Sound(buffer=_tone(notes, dur, vol, dec).tobytes())
            except Exception:
                pass
        threading.Thread(target=self._load, daemon=True).start()

    def _load(self):
        try:
            import music
            paths = music.generate_tracks(MUSIC_DIR, progress=lambda m: print(f"[music] {m}", flush=True))
            for name, p in paths.items():
                self.sounds[name] = pygame.mixer.Sound(p)
            self.ready = True
            print("[music] score ready", flush=True)
        except Exception as e:
            print(f"[music] failed: {e}", flush=True)

    def play(self, name):
        self.want = name

    def toggle(self):
        self.enabled = not self.enabled
        if not self.enabled and self.ok:
            pygame.mixer.fadeout(600)
            self.current = None

    def beep(self, name, vol=0.6):
        if self.ok and self.enabled and name in self.sfx:
            self.sfx[name].set_volume(vol)
            self.sfx[name].play()

    def update(self):
        if not (self.ok and self.ready and self.enabled):
            return
        if self.current != self.want and self.want in self.sounds:
            if self.current in self.sounds:
                self.sounds[self.current].fadeout(2500)
            snd = self.sounds[self.want]
            snd.set_volume(self.volume)
            snd.play(loops=-1, fade_ms=3000)
            self.current = self.want
