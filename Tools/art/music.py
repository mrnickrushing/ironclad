#!/usr/bin/env python3
"""Ironclad's music, made here from nothing but arithmetic (so there is nothing to license): a train shuffle
(boom-chick bass, chugging brushes, a banjo roll and a harmonica) while the train rolls, as "day"; a guitar picked
slowly over pads at the depot and in stations, as "night". Writes Assets/Resources/Audio/Music/<name>.ogg.

    python3 Tools/art/music.py            # every track
Needs numpy, scipy and ffmpeg.
"""
import os, subprocess, sys, tempfile, wave
import numpy as np
from scipy.signal import fftconvolve, butter, lfilter

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio", "Music")
NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "Eb": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "Ab": 8, "A": 9, "Bb": 10, "B": 11}

def hz(name, octave): return 440.0 * 2 ** ((NOTE[name] + (octave - 4) * 12 - 9) / 12.0)
def lowpass(x, cut, order=2):
    b, a = butter(order, cut / (SR / 2), "low"); return lfilter(b, a, x)
def highpass(x, cut, order=2):
    b, a = butter(order, cut / (SR / 2), "high"); return lfilter(b, a, x)

def env(n, attack, release, hold=None):
    """A soft envelope over n samples: rise, hold, fall."""
    e = np.ones(n)
    a = min(n, int(attack * SR)); r = min(n, int(release * SR))
    if a > 0: e[:a] = np.sin(np.linspace(0, np.pi / 2, a)) ** 2
    if r > 0: e[-r:] *= np.cos(np.linspace(0, np.pi / 2, r)) ** 2
    return e

def pad(freq, seconds, rng):
    """Three slightly detuned voices with a few soft harmonics, slowly breathing."""
    n = int(seconds * SR); t = np.arange(n) / SR
    out = np.zeros((n, 2))
    for k, det in enumerate((-0.004, 0.0, 0.005)):
        f = freq * (1 + det)
        ph = rng.uniform(0, 2 * np.pi)
        v = np.sin(2 * np.pi * f * t + ph) + 0.35 * np.sin(2 * np.pi * 2 * f * t + ph * 2) + 0.12 * np.sin(2 * np.pi * 3 * f * t) + 0.05 * np.sin(2 * np.pi * 4 * f * t)
        v *= 1 + 0.12 * np.sin(2 * np.pi * (0.11 + 0.05 * k) * t + ph)
        pan = 0.5 + (k - 1) * 0.35
        out[:, 0] += v * (1 - pan); out[:, 1] += v * pan
    return out * env(n, 1.4, 1.8)[:, None]

def keys(freq, seconds, vel=1.0):
    """A marimba: a quick wooden knock, a pure tone under it and the bar's fourth harmonic dying fast."""
    n = int(seconds * SR); t = np.arange(n) / SR
    v = np.sin(2 * np.pi * freq * t) * np.exp(-t * 5.5)
    v += 0.45 * np.sin(2 * np.pi * freq * 4 * t) * np.exp(-t * 22)
    v += 0.18 * np.sin(2 * np.pi * freq * 10 * t) * np.exp(-t * 60)
    v *= env(n, 0.002, 0.06)
    pan = 0.5 + 0.3 * np.sin(freq * 0.37)
    return np.stack([v * (1 - pan), v * pan], axis=1) * vel

def bass(freq, seconds):
    n = int(seconds * SR); t = np.arange(n) / SR
    v = np.sin(2 * np.pi * freq * t) + 0.3 * np.sin(2 * np.pi * 2 * freq * t) * np.exp(-t * 4)
    v *= np.exp(-t * 1.3) * env(n, 0.012, 0.12)
    return np.stack([v, v], axis=1)

def brush(seconds, rng, bright=6000):
    n = int(seconds * SR); t = np.arange(n) / SR
    v = highpass(rng.standard_normal(n), bright) * np.exp(-t * 32)
    return np.stack([v, v * 0.8], axis=1)

def kick(seconds=0.35):
    n = int(seconds * SR); t = np.arange(n) / SR
    f = 46 + 70 * np.exp(-t * 28)
    v = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 11)
    return np.stack([v, v], axis=1)

def room(x, rng, seconds=2.6, wet=0.3):
    """A hall: the dry sound plus itself played through a burst of fading noise."""
    n = int(seconds * SR); t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)) * np.exp(-t * 2.6)[:, None]
    ir = np.stack([lowpass(ir[:, 0], 5200), lowpass(ir[:, 1], 5200)], axis=1)
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    w = np.stack([fftconvolve(x[:, c], ir[:, c]) for c in range(2)], axis=1)
    out = np.zeros_like(w); out[:len(x)] += x * (1 - wet); out += w * wet
    return out

def add(mix, at, sound, gain=1.0):
    i = max(0, int(at * SR))
    j = min(len(mix), i + len(sound))
    if i < len(mix): mix[i:j] += sound[:j - i] * gain

def track(name, bpm, chords, seed, bright=1.0, drums=True):
    """chords: one per bar, each (root note, octave, [intervals in semitones], [melody steps])."""
    rng = np.random.default_rng(seed)
    beat = 60.0 / bpm; bar = beat * 4
    length = bar * len(chords)
    tail = 5.0
    mix = np.zeros((int((length + tail) * SR), 2))
    for b, (root, octv, tones, tune) in enumerate(chords):
        t0 = b * bar
        base = hz(root, octv)
        # Pad: the whole chord, held a little past the bar so the changes melt.
        for iv in tones: add(mix, t0, pad(base * 2 ** (iv / 12.0), bar + 1.2, rng), 0.05)
        # Bass on one and the push before three.
        add(mix, t0, bass(base / 2, beat * 2.2), 0.3)
        add(mix, t0 + beat * 2.5, bass(base / 2, beat * 1.4), 0.2)
        # Keys: a slow broken chord, never the same twice, with room to breathe.
        order = list(tones) + [tones[0] + 12]
        steps = [0, 1.5, 2, 3, 3.5] if b % 2 == 0 else [0.5, 1, 2.5, 3]
        for k, s in enumerate(steps):
            iv = order[(k * 2 + b) % len(order)]
            add(mix, t0 + s * beat + rng.uniform(-0.012, 0.012), keys(base * 2 * 2 ** (iv / 12.0), beat * 2.4, rng.uniform(0.6, 1.0)), 0.16 * bright)
        # A line on top, a few notes a bar.
        for s, iv in tune:
            add(mix, t0 + s * beat, keys(base * 4 * 2 ** (iv / 12.0), beat * 3, 0.9), 0.15 * bright)
        if drums:
            for q in range(8):
                add(mix, t0 + q * beat / 2 + (0.03 if q % 2 else 0), brush(0.2, rng, 7000 if q % 2 else 5200), 0.045 if q % 2 else 0.028)
            add(mix, t0, kick(), 0.34); add(mix, t0 + beat * 2.5, kick(), 0.2)
            add(mix, t0 + beat, brush(0.3, rng, 2400), 0.07); add(mix, t0 + beat * 3, brush(0.3, rng, 2400), 0.08)
    mix = room(mix, rng)
    # Wrap whatever rings past the end back onto the start: the loop has no seam.
    n = int(length * SR)
    loop = mix[:n].copy(); extra = mix[n:]; loop[:len(extra)] += extra[:n]
    loop = np.stack([lowpass(loop[:, 0], 9000), lowpass(loop[:, 1], 9000)], axis=1)
    # (filtering the wrapped loop twice round keeps the filter's own start-up out of the seam)
    loop /= np.abs(loop).max() / 0.72
    os.makedirs(OUT, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f: tmp = f.name
    with wave.open(tmp, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(loop, -1, 1) * 32767).astype("<i2").tobytes())
    dst = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
    os.unlink(tmp)
    print(name, "%.0f s" % length, "%d KB" % (os.path.getsize(dst) // 1024))

MAJ7, MIN7, DOM7, SUS = [0, 4, 7, 11], [0, 3, 7, 10], [0, 4, 7, 10], [0, 5, 7, 10]
MAJ, MIN, ADD9, SIX = [0, 4, 7], [0, 3, 7], [0, 4, 7, 14], [0, 4, 7, 9]

def pluck(freq, seconds, rng, bright=0.5):
    """A plucked string (banjo, guitar): a burst of noise going round a delay line and losing its edge each time."""
    n = int(seconds * SR); period = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, period)
    buf = lowpass(buf, 2500 + 6000 * bright)
    out = np.empty(n); damp = 0.996 - 0.004 * (1 - bright)
    for i in range(0, n, period):
        m = min(period, n - i)
        out[i:i + m] = buf[:m]
        buf = damp * 0.5 * (buf + np.roll(buf, 1))
    out *= env(n, 0.001, 0.05)
    pan = 0.5 + 0.28 * np.sin(freq * 0.11)
    return np.stack([out * (1 - pan), out * pan], axis=1)

def reed(freq, seconds, vib=5.2):
    """A harmonica: a buzzy reed with a slow wobble, breathed in and out."""
    n = int(seconds * SR); t = np.arange(n) / SR
    wob = 1 + 0.006 * np.sin(2 * np.pi * vib * t) * np.minimum(1, t / 0.25)
    ph = 2 * np.pi * np.cumsum(freq * wob) / SR
    v = sum(np.sin(k * ph) / k ** 1.15 for k in range(1, 9))
    v = lowpass(v, 3200) * (1 + 0.1 * np.sin(2 * np.pi * vib * t))
    v *= env(n, 0.05, 0.14)
    return np.stack([v * 0.9, v], axis=1)

def western(name, bpm, chords, seed, drive=True):
    """chords: one per bar, each (root, octave, intervals, [(beat, interval)] for the harmonica). With drive, a train
    shuffle: boom-chick bass, sixteenth brushes that chug, a picked banjo roll; without, a slow picked guitar over pads."""
    rng = np.random.default_rng(seed)
    beat = 60.0 / bpm; bar = beat * 4
    length = bar * len(chords); tail = 5.0
    mix = np.zeros((int((length + tail) * SR), 2))
    for b, (root, octv, tones, tune) in enumerate(chords):
        t0 = b * bar; base = hz(root, octv)
        for iv in tones[:3]: add(mix, t0, pad(base * 2 * 2 ** (iv / 12.0), bar + 1.0, rng), 0.02 if drive else 0.035)
        if drive:
            # Boom-chick: root on one and three, the fifth below on two and four.
            for q in range(4):
                f = base / 2 if q % 2 == 0 else base / 2 * 2 ** (-5 / 12.0)
                add(mix, t0 + q * beat, bass(f * 2, beat * 0.95), 0.11 if q % 2 == 0 else 0.08)
            # The chug: sixteenths, the wheels over the rail joints.
            for q in range(16):
                acc = (0.13, 0.045, 0.08, 0.045)[q % 4]
                add(mix, t0 + q * beat / 4 + (0.012 if q % 2 else 0), brush(0.11, rng, 4200 if q % 4 == 0 else 6800), acc)
            add(mix, t0, kick(), 0.1); add(mix, t0 + beat * 2, kick(), 0.08)
            add(mix, t0 + beat, brush(0.22, rng, 2200), 0.13); add(mix, t0 + beat * 3, brush(0.22, rng, 2200), 0.15)
            # A banjo roll over the chord, eighths, never quite the same.
            order = [tones[0], tones[2], tones[1] + 12, tones[2], tones[0] + 12, tones[2], tones[1] + 12, tones[2]]
            for q in range(8):
                iv = order[(q + b) % 8]
                add(mix, t0 + q * beat / 2 + rng.uniform(-0.006, 0.006), pluck(base * 2 * 2 ** (iv / 12.0), beat * 1.4, rng, 0.8), 0.42 if q % 2 == 0 else 0.3)
        else:
            add(mix, t0, bass(base, beat * 3.4), 0.1)
            order = [tones[0], tones[1], tones[2], tones[1] + 12, tones[2], tones[1]]
            for q in range(6):
                add(mix, t0 + q * beat * 2 / 3 + rng.uniform(-0.01, 0.01), pluck(base * 2 ** (order[q] / 12.0) * 2, beat * 3, rng, 0.5), 0.4)
        for st, iv in tune:
            add(mix, t0 + st * beat, reed(base * 4 * 2 ** (iv / 12.0), beat * (1.6 if drive else 2.6)), 0.13 if drive else 0.1)
    mix = room(mix, rng, wet=0.22 if drive else 0.34)
    n = int(length * SR)
    loop = mix[:n].copy(); extra = mix[n:]; loop[:len(extra)] += extra[:n]
    loop = np.stack([lowpass(loop[:, 0], 9500), lowpass(loop[:, 1], 9500)], axis=1)
    loop /= np.abs(loop).max() / 0.72
    os.makedirs(OUT, exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f: tmp = f.name
    with wave.open(tmp, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(loop, -1, 1) * 32767).astype("<i2").tobytes())
    dst = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", "4", dst], check=True)
    os.unlink(tmp)
    print(name, "%.0f s" % length, "%d KB" % (os.path.getsize(dst) // 1024))

MAJ, MIN, DOM7, SUS = [0, 4, 7], [0, 3, 7], [0, 4, 7, 10], [0, 5, 7]

# "day" plays while the train rolls: a shuffle in D, sixteen bars, the harmonica answering itself.
run = [
    ("D", 3, MAJ, [(0, 7), (1, 9), (2, 12)]), ("D", 3, MAJ, [(0.5, 9), (2, 7)]), ("G", 3, MAJ, [(0, 4), (1, 7), (2.5, 9)]), ("D", 3, MAJ, [(0, 7), (2, 4)]),
    ("D", 3, MAJ, [(0, 12), (1, 9), (2, 7)]), ("B", 2, MIN, [(0.5, 7), (2, 10)]), ("E", 3, MIN, [(0, 7), (1.5, 3)]), ("A", 2, DOM7, [(0, 4), (1, 7), (2, 10), (3, 7)]),
    ("D", 3, MAJ, [(0, 7), (1, 9), (2, 12), (3, 14)]), ("D", 3, DOM7, [(0, 16), (2, 10)]), ("G", 3, MAJ, [(0, 7), (1, 4), (2, 7)]), ("G", 3, MIN, [(0.5, 3), (2, 7)]),
    ("D", 3, MAJ, [(0, 4), (1, 7), (2, 12)]), ("A", 2, DOM7, [(0, 10), (1.5, 7), (3, 4)]), ("D", 3, MAJ, [(0, 0), (1, 4), (2, 7)]), ("A", 2, DOM7, [(0.5, 7), (2, 4), (3, 2)]),
]
# "night" plays at the depot and in stations: the same country at rest, a guitar picked slowly in G.
rest = [
    ("G", 3, MAJ, [(1, 7)]), ("C", 3, MAJ, [(0.5, 4), (2.5, 7)]), ("G", 3, MAJ, [(1, 12), (3, 9)]), ("D", 3, SUS, [(0.5, 7)]),
    ("E", 3, MIN, [(1, 7), (2.5, 3)]), ("C", 3, MAJ, [(0.5, 7)]), ("G", 3, MAJ, [(1, 4), (3, 7)]), ("D", 3, MAJ, [(1.5, 4)]),
]

if __name__ == "__main__":
    want = sys.argv[1:] or ["day", "night"]
    if "day" in want: western("day", 118, run, 11, drive=True)
    if "night" in want: western("night", 74, rest, 23, drive=False)
