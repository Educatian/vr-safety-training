"""Procedural SFX/ambience for the jobsite (no licensing questions): writes Assets/_Game/Resources/Audio/*.wav, 22.05 kHz mono."""
import wave
from pathlib import Path
import numpy as np

SR = 22050
OUT = Path(__file__).resolve().parents[2] / "Assets/_Game/Resources/Audio"
rng = np.random.default_rng(7)


def t(sec):
    return np.arange(int(SR * sec)) / SR


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x); acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc; y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def env(n, attack, release):
    e = np.ones(n); a = int(attack * SR); r = int(release * SR)
    if a: e[:a] = np.linspace(0, 1, a)
    if r: e[-r:] *= np.linspace(1, 0, r)
    return e


def loopable(x, fade=0.4):
    """Crossfade the tail into the head so the clip loops without a click."""
    f = int(fade * SR)
    head, tail = x[:f].copy(), x[-f:]
    w = np.linspace(0, 1, f)
    x = x[:-f].copy(); x[:f] = head * w + tail * (1 - w)
    return x


def save(name, x, peak=0.85):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def ambience():
    n = t(24.4)
    brown = lowpass(np.cumsum(rng.normal(0, 1, len(n))) * 0.02, 300); brown -= lowpass(brown, 8)
    cicada = highpass(rng.normal(0, 1, len(n)), 3800) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.11 * n) ** 2) * (0.6 + 0.4 * np.sin(2 * np.pi * 38 * n))
    x = brown * 1.4 + cicada * 0.10
    for _ in range(14):   # far-off hammer taps and a distant truck
        at = rng.uniform(0, len(n) / SR - 0.3); i = int(at * SR)
        k = t(0.12); x[i:i + len(k)] += np.sin(2 * np.pi * 900 * k) * np.exp(-k * 45) * 0.12
    x += np.sin(2 * np.pi * 55 * n) * 0.05 * (0.5 + 0.5 * np.sin(2 * np.pi * 0.05 * n))
    save("ambience", loopable(x), 0.5)


def engine():
    n = t(4.4); f = 29
    x = sum(np.sin(2 * np.pi * f * h * n + h) / h for h in range(1, 12))
    x *= 0.8 + 0.2 * np.sin(2 * np.pi * f / 2 * n)
    x += lowpass(rng.normal(0, 1, len(n)), 500) * 0.4
    save("engine_loop", loopable(x, 0.3))


def saw():
    n = t(3.3)
    x = np.sin(2 * np.pi * 3600 * n) * 0.4 + np.sin(2 * np.pi * 7200 * n) * 0.15
    x += highpass(rng.normal(0, 1, len(n)), 1500) * 0.7 * (0.8 + 0.2 * np.sin(2 * np.pi * 9 * n))
    save("saw_loop", loopable(x, 0.3), 0.7)


def generator():
    n = t(3.3)
    x = sum(np.sin(2 * np.pi * 60 * h * n) / h ** 1.3 for h in range(1, 8)) + lowpass(rng.normal(0, 1, len(n)), 900) * 0.3
    save("generator_loop", loopable(x, 0.3), 0.6)


def backup():
    n = t(2.0)   # typical 1 kHz-ish backup alarm, 0.5 s on / 0.5 s off
    gate = ((n % 1.0) < 0.5).astype(float)
    x = (np.sin(2 * np.pi * 1100 * n) + 0.3 * np.sign(np.sin(2 * np.pi * 1100 * n))) * gate
    save("backup_loop", x * env(len(n), 0.002, 0.0), 0.6)


def one_shots():
    k = t(0.18)   # camera shutter: two clicks
    x = np.zeros(len(k)); c = highpass(rng.normal(0, 1, 400), 2000) * np.exp(-np.arange(400) / 60)
    x[:400] += c; x[int(0.07 * SR):int(0.07 * SR) + 400] += c * 0.8
    save("shutter", x)
    k = t(0.45)   # radio squelch chirp
    x = highpass(rng.normal(0, 1, len(k)), 900) * 0.4 * env(len(k), 0.005, 0.1)
    x[:int(0.08 * SR)] += np.sin(2 * np.pi * 1400 * k[:int(0.08 * SR)]) * 0.6
    save("radio", x, 0.55)
    k = t(0.6)    # success chime (two tones)
    x = np.sin(2 * np.pi * 880 * k) * np.exp(-k * 6) + np.concatenate([np.zeros(int(.12 * SR)), np.sin(2 * np.pi * 1318.5 * k[:-int(.12 * SR)]) * np.exp(-k[:-int(.12 * SR)] * 6)])
    save("success", x, 0.5)
    k = t(1.8)    # incident: three horn blasts
    x = sum(np.sin(2 * np.pi * f * k) for f in (311, 370, 466)) * (((k % 0.6) < 0.4).astype(float))
    save("alarm", x * env(len(k), 0.01, 0.1), 0.7)
    k = t(0.05)
    save("click", np.sin(2 * np.pi * 2000 * k) * np.exp(-k * 120), 0.4)
    for i in range(3):   # boots on packed clay + gravel grit
        k = t(0.22)
        thump = lowpass(rng.normal(0, 1, len(k)), 180 + 40 * i) * np.exp(-k * 30) * 3
        grit = highpass(rng.normal(0, 1, len(k)), 2500) * np.exp(-k * 50) * 0.3
        save(f"step_{i}", thump + grit, 0.5)


if __name__ == "__main__":
    ambience(); engine(); saw(); generator(); backup(); one_shots()
    print(sorted(p.name for p in OUT.glob("*.wav")))
