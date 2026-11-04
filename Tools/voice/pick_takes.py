#!/usr/bin/env python3
"""Pick the best TTS take per line, clean it, and export Unity voice assets.

  ~/Development/gura-wish-mv/.venv/bin/python pick_takes.py

Score = word match of the Whisper transcript against the script (minus a small penalty for
very slow delivery). Output: Assets/Resources/Voice/<id>.wav (mono, trimmed, levelled, gentle
high-pass) and Assets/Resources/Data/voice.json (duration + 30 Hz loudness envelope for the
portrait's talk animation, since WebGL cannot read audio output samples).
"""
import difflib
import json
import re
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = ROOT / 'Assets/Resources/Voice'
OUT.mkdir(parents=True, exist_ok=True)
DATA = ROOT / 'Assets/Resources/Data/voice.json'
CACHE = HERE / 'whisper_cache.json'


def words(t):
    return re.findall(r"[a-z0-9']+", t.lower().replace('-', ' '))


def main():
    import mlx_whisper
    lines = json.loads((HERE / 'lines.json').read_text())
    cache = json.loads(CACHE.read_text()) if CACHE.exists() else {}
    table = []
    sos = butter(2, 90, btype='highpass', fs=44100, output='sos')
    for ln in lines:
        best = None
        for f in sorted((HERE / 'raw').glob(ln['id'] + '_s*.wav')):
            if f.name not in cache:
                r = mlx_whisper.transcribe(str(f), path_or_hf_repo='mlx-community/whisper-large-v3-turbo', language='en')
                cache[f.name] = r['text']
            got = words(cache[f.name])
            want = words(ln['text'])
            score = difflib.SequenceMatcher(None, want, got).ratio()
            y, sr = librosa.load(str(f), sr=44100, mono=True)
            yt, idx = librosa.effects.trim(y, top_db=38)
            dur = len(yt) / sr
            rate = len(want) / max(dur, 0.1)
            score -= 0.05 * max(0, 2.1 - rate)  # penalise sluggish reads
            if best is None or score > best[0]:
                best = (score, f, yt)
        CACHE.write_text(json.dumps(cache))
        score, f, y = best
        y = sosfilt(sos, y)
        pad = int(0.06 * 44100)
        y = np.concatenate([np.zeros(pad // 2), y, np.zeros(pad)])
        rms = np.sqrt(np.mean(y[np.abs(y) > 0.01] ** 2)) if np.any(np.abs(y) > 0.01) else 0.1
        y = y * (10 ** (-17 / 20) / max(rms, 1e-6))
        peak = np.max(np.abs(y))
        if peak > 0.95:
            y = np.tanh(y / peak * 1.4) / np.tanh(1.4) * 0.95
        sf.write(str(OUT / f"{ln['id']}.wav"), y.astype(np.float32), 44100, subtype='PCM_16')
        hop = 44100 // 30
        env = np.array([np.sqrt(np.mean(y[i:i + hop] ** 2)) for i in range(0, len(y), hop)])
        env = np.clip(env / (np.percentile(env, 95) + 1e-9), 0, 1)
        table.append(dict(id=ln['id'], duration=round(len(y) / 44100, 3), env=[round(float(v), 2) for v in env]))
        flag = '' if score > 0.9 else '  <-- check'
        print(f"{ln['id']} {f.name.split('_')[-1]} score={score:.2f} dur={len(y) / 44100:.1f}s  {ln['text'][:60]}{flag}", flush=True)
    DATA.write_text(json.dumps(dict(lines=table), separators=(',', ':')))
    print('voice lines:', len(table))


if __name__ == '__main__':
    main()
