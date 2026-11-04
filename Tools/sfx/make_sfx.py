#!/usr/bin/env python3
"""Synthesise every UI / gameplay sound effect into Assets/Resources/Audio/*.wav (44.1 kHz)."""
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100
OUT = Path(__file__).resolve().parents[2] / 'Assets/Resources/Audio'
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(7)


def t_(d):
    return np.arange(int(d * SR)) / SR


def env(d, a=0.002, decay=0.1, curve=1.0):
    t = t_(d)
    e = np.minimum(1, t / max(a, 1e-4)) * np.exp(-np.maximum(0, t - a) / decay) ** curve
    return e


def sine(f, d, phase=0):
    t = t_(d)
    if callable(f):
        ph = 2 * np.pi * np.cumsum(f(t)) / SR
        return np.sin(ph + phase)
    return np.sin(2 * np.pi * f * t + phase)


def noise(d):
    return rng.standard_normal(int(d * SR))


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype='band', fs=SR, output='sos'), x)


def hp(x, f):
    return sosfilt(butter(2, f, btype='high', fs=SR, output='sos'), x)


def lp(x, f):
    return sosfilt(butter(2, f, btype='low', fs=SR, output='sos'), x)


def reverb(x, length=1.2, mix=0.25, damp=3000):
    ir = noise(length) * np.exp(-t_(length) / (length / 5))
    ir = lp(ir, damp)
    ir /= np.sqrt(np.sum(ir ** 2))
    wet = fftconvolve(x, ir)[:len(x) + int(length * SR)]
    dry = np.concatenate([x, np.zeros(len(wet) - len(x))])
    return dry * (1 - mix) + wet * mix


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, sec, total=None):
    pad = np.zeros(int(sec * SR))
    y = np.concatenate([pad, x])
    if total:
        y = np.concatenate([y, np.zeros(max(0, int(total * SR) - len(y)))])
    return y


def save(name, x, peak=0.85, stereo_width=0.0):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    fade = int(0.004 * SR)
    x[-fade:] *= np.linspace(1, 0, fade)
    if stereo_width > 0:
        d = int(0.011 * SR)
        r = np.concatenate([np.zeros(d), x[:-d]])
        x = np.stack([x, x * (1 - stereo_width) + r * stereo_width], axis=1)
    sf.write(str(OUT / f'{name}.wav'), x.astype(np.float32), SR, subtype='PCM_16')
    print(name, f'{len(x) / SR:.2f}s')


def kick(f0=150, f1=45, d=0.35, decay=0.12):
    return sine(lambda t: f1 + (f0 - f1) * np.exp(-t / 0.03), d) * env(d, 0.001, decay)


def chime(freqs, d=1.2, decay=0.35, bright=1.0):
    out = np.zeros(int(d * SR))
    for i, f in enumerate(freqs):
        for h, amp in ((1, 1), (2.01, 0.35 * bright), (3.02, 0.15 * bright), (4.2, 0.06 * bright)):
            out += amp * sine(f * h, d) * env(d, 0.002, decay / h ** 0.5)
    return out


def main():
    # gameplay
    tap = mix(hp(noise(0.06), 2500) * env(0.06, 0.0005, 0.012) * 0.6, sine(1850, 0.07) * env(0.07, 0.0008, 0.018) * 0.7,
              kick(260, 120, 0.06, 0.02) * 0.4)
    save('hit_tap', tap, 0.6)
    save('hit_perfect', reverb(mix(chime([2637, 3951], 0.35, 0.08, 0.6) * 0.6, hp(noise(0.05), 6000) * env(0.05, 0.0005, 0.01) * 0.2), 0.5, 0.18, 7000), 0.5)
    lub = mix(kick(110, 42, 0.32, 0.09), lp(noise(0.32), 300) * env(0.32, 0.002, 0.04) * 0.4)
    dub = kick(95, 38, 0.3, 0.07) * 0.7
    pulse = mix(lub, at(dub, 0.15), at(chime([660, 990], 0.6, 0.15, 0.3) * 0.15, 0.0))
    save('hit_pulse', reverb(pulse, 0.6, 0.12, 2500), 0.9)
    save('combo', reverb(mix(*[at(chime([f], 0.4, 0.12) * 0.5, i * 0.05) for i, f in enumerate([1047, 1319, 1568, 2093])]), 0.9, 0.3), 0.6, 0.3)
    # ui
    save('ui_hover', mix(sine(3200, 0.03) * env(0.03, 0.001, 0.008), sine(4800, 0.03) * env(0.03, 0.001, 0.005) * 0.3), 0.25)
    save('ui_click', mix(kick(400, 160, 0.08, 0.02) * 0.8, hp(noise(0.03), 3000) * env(0.03, 0.0005, 0.006) * 0.5, sine(2400, 0.05) * env(0.05, 0.001, 0.012) * 0.4), 0.55)
    save('ui_tick', sine(2600, 0.025) * env(0.025, 0.0008, 0.006), 0.3)
    save('metro', mix(bp(noise(0.08), 1500, 3500) * env(0.08, 0.0005, 0.01) * 0.8, sine(1600, 0.06) * env(0.06, 0.0005, 0.02)), 0.8)
    save('metro_hi', mix(bp(noise(0.08), 2500, 5000) * env(0.08, 0.0005, 0.01) * 0.8, sine(2400, 0.06) * env(0.06, 0.0005, 0.02)), 0.8)
    # transitions
    d = 2.6
    sweep = bp(noise(d), 200, 6000) * env(d, 1.2, 0.5) * np.linspace(0.2, 1, int(d * SR))
    boom = at(kick(90, 30, 1.6, 0.5), 1.15) * 1.2
    shimmer = at(chime([440, 659, 880, 1319], 1.6, 0.6, 0.6) * 0.35, 1.15)
    save('stinger', reverb(mix(sweep * 0.25, boom, shimmer), 2.0, 0.3), 0.9, 0.35)
    save('start', reverb(mix(bp(noise(0.8), 400, 8000) * env(0.8, 0.3, 0.15) * 0.3, at(chime([523, 784, 1047, 1568], 1.5, 0.5) * 0.5, 0.25),
                             at(kick(120, 40, 0.6, 0.2), 0.25)), 1.6, 0.3), 0.8, 0.3)
    save('ready', reverb(mix(sine(880, 0.25) * env(0.25, 0.002, 0.08), sine(1760, 0.25) * env(0.25, 0.002, 0.05) * 0.3), 0.6, 0.25), 0.5)
    save('go', reverb(mix(sine(lambda t: 880 + 900 * np.minimum(1, t / 0.08), 0.5) * env(0.5, 0.002, 0.15), kick(160, 50, 0.4, 0.12) * 0.7), 0.9, 0.3), 0.7)
    roll = np.zeros(int(1.3 * SR))
    for i in range(26):
        tt = i * 0.045
        roll += at(sine(1200 + i * 40, 0.03) * env(0.03, 0.0005, 0.008) * 0.6, tt, 1.3)[:len(roll)]
    save('results_roll', roll, 0.4)
    save('grade', reverb(mix(kick(120, 35, 1.0, 0.3) * 1.2, chime([392, 587, 784], 1.4, 0.45, 0.5) * 0.4, hp(noise(0.3), 3000) * env(0.3, 0.001, 0.05) * 0.2), 1.6, 0.3), 0.85, 0.3)
    sparkle = mix(*[at(chime([float(f)], 0.3, 0.07) * 0.25, 0.1 + i * 0.06) for i, f in enumerate([2093, 2637, 3136, 3951, 4186, 5274])])
    save('grade_s', reverb(mix(kick(130, 32, 1.2, 0.35) * 1.3, chime([523, 659, 784, 1047], 1.8, 0.6, 0.7) * 0.45, sparkle), 2.0, 0.35), 0.9, 0.35)
    save('quiz_right', reverb(mix(chime([659], 0.4, 0.12) * 0.6, at(chime([988, 1319], 0.8, 0.25) * 0.6, 0.11)), 1.0, 0.25), 0.6, 0.25)
    save('quiz_wrong', mix(lp(sine(196, 0.25) + 0.5 * np.sign(sine(196, 0.25)), 1200) * env(0.25, 0.005, 0.1) * 0.5,
                           at(lp(sine(147, 0.35) + 0.5 * np.sign(sine(147, 0.35)), 1000) * env(0.35, 0.005, 0.14) * 0.5, 0.16)), 0.45)
    alarm = mix(*[at(mix(sine(880, 0.12) * env(0.12, 0.003, 0.05) * 0.5, sine(932, 0.12) * env(0.12, 0.003, 0.05) * 0.4), i * 0.16) for i in range(3)])
    save('wrong_choice', reverb(mix(alarm, at(kick(80, 30, 0.8, 0.25) * 0.8, 0.0)), 1.0, 0.25), 0.6)


if __name__ == '__main__':
    main()
