"""Remove clicks, crackle and clipping risk from the game's audio (2026-10-02 playtest: "치지직" noise).
- radio.wav was a white-noise squelch played on every notice -> replaced by a clean, soft two-tone chirp.
- One-shots: DC removed, 4 ms fade-in / 10 ms fade-out (footsteps, shutter and thunder started mid-waveform).
- Loops: seamless crossfade of the last 60 ms into the start (saw/generator/wind jumped at the loop point).
- Peaks normalised with headroom (loops <= 0.45, one-shots <= 0.6) so overlapping sources don't clip.
Originals are copied to Tools/audio/originals/ once. Run: python3 Tools/audio/declick.py
"""
import os, shutil, wave
import numpy as np

AUDIO = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Resources", "Audio")
ORIG = os.path.join(os.path.dirname(__file__), "originals")
LOOPS = {"ambience", "backup_loop", "engine_loop", "generator_loop", "rain_loop", "saw_loop", "wind_loop"}
ONESHOTS = {"alarm", "click", "shutter", "step_0", "step_1", "step_2", "success", "thunder", "whistle"}

def read(path):
    with wave.open(path) as w:
        sr, ch, n = w.getframerate(), w.getnchannels(), w.getnframes()
        x = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float32) / 32768.0
    return sr, x.reshape(-1, ch).mean(axis=1)

def write(path, sr, x):
    x = np.clip(x, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((x * 32767).astype("<i2").tobytes())

def fade(x, sr, fin=0.004, fout=0.010):
    a, b = max(1, int(sr * fin)), max(1, int(sr * fout))
    x = x.copy()
    x[:a] *= np.linspace(0, 1, a) ** 2
    x[-b:] *= np.linspace(1, 0, b) ** 2
    return x

def seamless(x, sr, xf=0.06):
    n = int(sr * xf)
    if len(x) < 4 * n: return x
    t = np.linspace(0, 1, n)
    out = x[: len(x) - n].copy()
    out[:n] = x[:n] * np.sin(t * np.pi / 2) + x[-n:] * np.cos(t * np.pi / 2)   # equal-power crossfade
    return out

def norm(x, peak):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > peak else x

def chirp(sr=22050):
    # Soft radio "blip": two short sine tones with smooth envelopes, no noise.
    def tone(f, d):
        t = np.arange(int(sr * d)) / sr
        env = np.sin(np.pi * np.clip(t / d, 0, 1)) ** 2
        return 0.30 * env * (np.sin(2 * np.pi * f * t) + 0.25 * np.sin(4 * np.pi * f * t))
    gap = np.zeros(int(sr * 0.02))
    return np.concatenate([tone(1250, 0.07), gap, tone(1650, 0.08)])

os.makedirs(ORIG, exist_ok=True)
for f in sorted(os.listdir(AUDIO)):
    if not f.endswith(".wav"): continue
    name, path = f[:-4], os.path.join(AUDIO, f)
    if not os.path.exists(os.path.join(ORIG, f)): shutil.copy2(path, os.path.join(ORIG, f))
    sr, x = read(os.path.join(ORIG, f))
    x = x - np.mean(x)
    if name == "radio": y = chirp(sr)
    elif name in LOOPS: y = norm(seamless(x, sr), 0.45)
    elif name in ONESHOTS: y = norm(fade(x, sr), 0.6)
    else: continue
    write(path, sr, y)
    print(f"{f:20s} {len(y)/sr:5.2f}s peak {np.max(np.abs(y)):.2f} start {abs(y[0]):.3f} end {abs(y[-1]):.3f}" + (f" seam {abs(y[-1]-y[0]):.3f}" if name in LOOPS else ""))
