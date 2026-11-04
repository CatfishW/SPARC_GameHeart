#!/usr/bin/env python3
"""Score every raw take: duration, tempo + beat regularity, loudness, and (for vocal songs)
how well Whisper recovers the written lyrics. Writes takes_report.json.

Run with the analysis venv: ~/Development/gura-wish-mv/.venv/bin/python score_takes.py
"""
import difflib
import json
import re
import sys
from pathlib import Path

import librosa
import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from songs import SONGS  # noqa: E402


def words(text):
    text = re.sub(r'\[[^\]]*\]', ' ', text)
    return re.findall(r"[a-z']+", text.lower())


def main():
    import mlx_whisper
    report = {}
    for song in SONGS:
        for seed in song['seeds']:
            f = HERE / 'raw' / f"{song['key']}_s{seed}.flac"
            if not f.exists():
                continue
            y, sr = librosa.load(str(f), sr=22050, mono=True)
            dur = len(y) / sr
            tempo, beats = librosa.beat.beat_track(y=y, sr=sr)
            bt = librosa.frames_to_time(beats, sr=sr)
            ibi = np.diff(bt)
            rms = librosa.feature.rms(y=y)[0]
            entry = dict(song=song['key'], seed=seed, duration=round(dur, 1), tempo=round(float(np.atleast_1d(tempo)[0]), 2),
                         ibi_cv=round(float(ibi.std() / max(ibi.mean(), 1e-6)), 4) if len(ibi) > 4 else 1.0,
                         rms_db=round(float(20 * np.log10(np.mean(rms) + 1e-9)), 1),
                         silent_tail=round(float(np.sum(rms[-200:] < 0.01) / 200), 2))
            if not song.get('instrumental'):
                r = mlx_whisper.transcribe(str(f), path_or_hf_repo='mlx-community/whisper-large-v3-turbo', language='en',
                                           condition_on_previous_text=False)
                got = words(' '.join(s['text'] for s in r['segments'] if s['end'] < dur - 1))
                want = words(song['lyrics'])
                entry['lyric_match'] = round(difflib.SequenceMatcher(None, want, got).ratio(), 3)
                entry['segments'] = [dict(t=round(s['start'], 2), e=round(s['end'], 2), text=s['text'].strip()) for s in r['segments']]
            report[f.stem] = entry
            print(f.stem, {k: v for k, v in entry.items() if k != 'segments'}, flush=True)
    (HERE / 'takes_report.json').write_text(json.dumps(report, indent=1))


if __name__ == '__main__':
    main()
