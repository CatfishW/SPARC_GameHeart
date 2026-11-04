#!/usr/bin/env python3
"""Chosen takes -> mastered game-size MP3s (StreamingAssets/Music) + rhythm charts (Resources/Charts).

  ~/Development/gura-wish-mv/.venv/bin/python build_music.py [song ...]

Per song:
  1. constant-tempo beat grid (librosa + linear fit), downbeat phase from kick energy
  2. lyric line timing from Whisper word timestamps aligned to the written lyrics
  3. game-size edit on bar lines (short intro, optional end after chorus 2) with musical fades
  4. loudness normalisation + look-ahead peak limiter, MP3 via ffmpeg
  5. charts for easy / normal / hard: vocal syllables + drum onsets quantised to the grid,
     holds on sustained words, full-width heartbeat pulses on section downbeats
"""
import difflib
import json
import re
import subprocess
import sys
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf
from scipy.ndimage import maximum_filter1d, uniform_filter1d

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
from songs import SONGS  # noqa: E402

RAW = HERE / 'raw'
WORK = HERE / 'work'
WORK.mkdir(exist_ok=True)
MUSIC_OUT = ROOT / 'Assets/StreamingAssets/Music'
CHART_OUT = ROOT / 'Assets/Resources/Charts'
MUSIC_OUT.mkdir(parents=True, exist_ok=True)
CHART_OUT.mkdir(parents=True, exist_ok=True)

# take, expected bpm, game edit (end after this lyric line index, 0-based), intro bars kept before first vocal
PICKS = {
    'first_beat': dict(take='first_beat_s1101', bpm=108, end_line=None, intro_bars=2),
    'frostbite': dict(take='frostbite_s2201', bpm=123, end_line=15, intro_bars=4),
    'sprint': dict(take='sprint_s3301', bpm=152, end_line=15, intro_bars=4),
    'fever': dict(take='fever_s4401', bpm=143.5, end_line=15, intro_bars=4),
    'night_tide': dict(take='night_tide_s5501', bpm=72, end_line=None, intro_bars=2),
    'symphony': dict(take='symphony_s6602', bpm=129, end_line=None, intro_bars=2),
}
BGM = {
    'title_theme': 'title_theme_s7701', 'control_center': 'control_center_s8801',
    'first_beat_inst': 'first_beat_inst_s9101', 'frostbite_inst': 'frostbite_inst_s9202', 'sprint_inst': 'sprint_inst_s9301',
    'fever_inst': 'fever_inst_s9401', 'night_tide_inst': 'night_tide_inst_s9501', 'symphony_inst': 'symphony_inst_s9601',
}
SR = 44100


def song_def(key):
    return next(s for s in SONGS if s['key'] == key)


# ---------------------------------------------------------------------------- lyrics
def lyric_lines(text):
    lines, kinds, kind = [], [], 'verse'
    for raw in text.split('\n'):
        raw = raw.strip()
        if not raw:
            continue
        m = re.match(r'\[(\w+)\]', raw)
        if m:
            kind = m.group(1).lower()
            continue
        lines.append(raw)
        kinds.append(kind)
    return lines, kinds


def tok(w):
    return re.sub(r"[^a-z']", '', w.lower())


def align_lyrics(song, words):
    """Map written lyric lines onto whisper words -> [(t_start, t_end)] per line (None if unmatched)."""
    lines, kinds = lyric_lines(song['lyrics'])
    written = []
    for li, ln in enumerate(lines):
        for w in ln.split():
            if tok(w):
                written.append((tok(w), li))
    heard = [tok(w['word']) for w in words]
    sm = difflib.SequenceMatcher(None, [w for w, _ in written], heard, autojunk=False)
    line_times = [[None, None] for _ in lines]
    for a, b, n in sm.get_matching_blocks():
        for k in range(n):
            li = written[a + k][1]
            ww = words[b + k]
            if line_times[li][0] is None or ww['start'] < line_times[li][0]:
                line_times[li][0] = ww['start']
            if line_times[li][1] is None or ww['end'] > line_times[li][1]:
                line_times[li][1] = ww['end']
    # interpolate unmatched lines
    for i, (s, e) in enumerate(line_times):
        if s is None:
            prev = next((line_times[j][1] for j in range(i - 1, -1, -1) if line_times[j][1] is not None), None)
            nxt = next((line_times[j][0] for j in range(i + 1, len(lines)) if line_times[j][0] is not None), None)
            if prev is not None and nxt is not None:
                line_times[i] = [prev + 0.2, max(prev + 0.4, nxt - 0.2)]
    return lines, kinds, line_times


def whisper_words(path):
    cache = WORK / (Path(path).stem + '.words.json')
    if cache.exists():
        return json.loads(cache.read_text())
    import mlx_whisper
    r = mlx_whisper.transcribe(str(path), path_or_hf_repo='mlx-community/whisper-large-v3-turbo', language='en',
                               word_timestamps=True, condition_on_previous_text=False)
    words = [dict(word=w['word'], start=float(w['start']), end=float(w['end']), p=float(w.get('probability', 1)))
             for s in r['segments'] for w in s.get('words', [])]
    cache.write_text(json.dumps(words))
    return words


# ---------------------------------------------------------------------------- beats
def beat_grid(y, sr, bpm_hint):
    oenv = librosa.onset.onset_strength(y=y, sr=sr, hop_length=256)
    tempo, beats = librosa.beat.beat_track(onset_envelope=oenv, sr=sr, hop_length=256, bpm=bpm_hint, tightness=300)
    bt = librosa.frames_to_time(beats, sr=sr, hop_length=256)
    idx = np.arange(len(bt))
    # robust linear fit (constant tempo)
    for _ in range(3):
        A = np.vstack([idx, np.ones_like(idx)]).T
        period, t0 = np.linalg.lstsq(A, bt, rcond=None)[0]
        res = bt - (t0 + period * idx)
        keep = np.abs(res) < max(0.03, 2.5 * np.std(res))
        idx, bt = idx[keep], bt[keep]
    period = float(period)
    t0 = float(t0)
    # fine search: tempo within +-0.45% and full phase, scored on a median-aggregated onset envelope
    hop2 = 128
    oenv2 = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop2, aggregate=np.median)
    times2 = np.arange(len(oenv2)) * hop2 / sr
    L = len(y) / sr

    def score(p_, t_):
        g = t_ + p_ * np.arange(int((L - t_) / p_))
        g = g[(g > 1) & (g < L - 1)]
        return np.interp(g, times2, oenv2).mean()

    bpm_fit = 60.0 / period
    best = (score(period, t0 % period), period, t0 % period)
    for b in np.linspace(bpm_fit * 0.9955, bpm_fit * 1.0045, 91):
        p_ = 60.0 / b
        for t_ in np.linspace(0, p_, 96, endpoint=False):
            sc = score(p_, t_)
            if sc > best[0]:
                best = (sc, p_, t_)
    _, period, t0 = best
    times = librosa.frames_to_time(np.arange(len(oenv)), sr=sr, hop_length=256)
    while t0 - period > 0:
        t0 -= period
    n = int((len(y) / sr - t0) / period) + 1
    beats = t0 + period * np.arange(n)
    # downbeat phase from low-frequency onset energy
    S = np.abs(librosa.stft(y, n_fft=2048, hop_length=256))
    freqs = librosa.fft_frequencies(sr=sr, n_fft=2048)
    low = S[(freqs > 35) & (freqs < 160)].sum(axis=0)
    lowd = np.maximum(0, np.diff(low, prepend=low[0]))
    lv = np.interp(beats, times, lowd)
    phase = int(np.argmax([lv[p::4].mean() for p in range(4)]))
    return 60.0 / period, beats, phase, rms_track(y, sr)


def rms_track(y, sr):
    hop = sr // 10
    r = np.array([np.sqrt(np.mean(y[i:i + hop] ** 2) + 1e-12) for i in range(0, len(y), hop)])
    return r


# ---------------------------------------------------------------------------- mastering
def limiter(x, ceiling=0.89, look=0.004, release=0.08, sr=SR):
    peak = np.max(np.abs(x), axis=1)
    w = max(1, int(look * sr))
    env = maximum_filter1d(peak, size=w * 2 + 1)
    g = np.minimum(1.0, ceiling / np.maximum(env, 1e-9))
    # release smoothing: gain may fall instantly, recover slowly
    a = np.exp(-1.0 / (release * sr))
    out = np.empty_like(g)
    cur = 1.0
    gs = g
    for i in range(len(gs)):  # simple loop is fast enough via numpy-less? use chunks
        v = gs[i]
        cur = v if v < cur else a * cur + (1 - a) * v
        out[i] = cur
    out = uniform_filter1d(out, size=w)
    return x * out[:, None]


def limiter_fast(x, ceiling=0.89, sr=SR):
    # vectorised approximation: block-wise gain from max filter, smoothed
    peak = np.max(np.abs(x), axis=1)
    env = maximum_filter1d(peak, size=int(0.01 * sr))
    g = np.minimum(1.0, ceiling / np.maximum(env, 1e-9))
    g = maximum_filter1d(g[::-1], size=1)[::-1]
    g = uniform_filter1d(np.minimum.accumulate(g[::1]) * 0 + g, size=int(0.02 * sr))
    g = np.minimum(g, np.minimum(1.0, ceiling / np.maximum(env, 1e-9)) + 0.02)
    y = x * g[:, None]
    return np.clip(y, -0.995, 0.995)


def master(x, target_rms_db=-14.5, ceiling=0.89):
    rms = np.sqrt(np.mean(x ** 2))
    gain = 10 ** (target_rms_db / 20) / max(rms, 1e-9)
    x = x * gain
    return limiter_fast(x, ceiling)


def export_mp3(x, out, bitrate='192k'):
    wav = WORK / (out.stem + '.master.wav')
    sf.write(str(wav), x, SR, subtype='PCM_16')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(wav), '-c:a', 'libmp3lame', '-b:a', bitrate, '-ar', str(SR), str(out)], check=True)
    return wav


def load_stereo(path):
    x, sr = sf.read(str(path), always_2d=True)
    if x.shape[1] == 1:
        x = np.repeat(x, 2, axis=1)
    if sr != SR:
        x = np.stack([librosa.resample(x[:, c], orig_sr=sr, target_sr=SR) for c in range(2)], axis=1)
    return x.astype(np.float32)


# ---------------------------------------------------------------------------- charting
DIFFS = [('easy', 0.0), ('normal', 1.0), ('hard', 2.0)]


def band_onsets(y, sr):
    S = np.abs(librosa.stft(y, n_fft=2048, hop_length=256)) ** 2
    freqs = librosa.fft_frequencies(sr=sr, n_fft=2048)
    times = librosa.frames_to_time(np.arange(S.shape[1]), sr=sr, hop_length=256)

    def band(lo, hi):
        b = np.log1p(S[(freqs >= lo) & (freqs < hi)].sum(axis=0))
        d = np.maximum(0, np.diff(b, prepend=b[0]))
        return d / (np.percentile(d, 99) + 1e-9)

    cent = librosa.feature.spectral_centroid(S=np.sqrt(S), sr=sr)[0]
    return times, band(30, 150), band(150, 2500), band(5000, 14000), cent


def at(times, arr, t, win=0.035):
    i0, i1 = np.searchsorted(times, [t - win, t + win])
    return float(arr[i0:max(i1, i0 + 1)].max()) if i1 > i0 else float(np.interp(t, times, arr))


def make_charts(key, y, sr, bpm, beats, phase, words, lines, kinds, line_times, energy, length):
    rng = np.random.default_rng(abs(hash(key)) % (2 ** 32))
    period = 60.0 / bpm
    times, kick, mid, high, cent = band_onsets(y, sr)
    cent_lo, cent_hi = np.percentile(cent, 15), np.percentile(cent, 85)
    sub = []  # 16th grid
    for i, b in enumerate(beats):
        for k in range(4):
            t = b + k * period / 4
            if 2.0 < t < length - 1.2:
                sub.append((t, i, k))
    downbeats = [b for i, b in enumerate(beats) if (i - phase) % 4 == 0]
    word_t = np.array([w['start'] for w in words if w['p'] > 0.2]) if words else np.zeros(0)
    word_e = np.array([w['end'] for w in words if w['p'] > 0.2]) if words else np.zeros(0)
    en_t = np.arange(len(energy)) / 10.0

    def energy_at(t):
        return float(np.interp(t, en_t, energy)) if len(energy) else 0.5

    def section_at(t):
        for (s, e), kd in zip(line_times, kinds):
            if s is not None and s - 0.3 <= t <= e + 0.6:
                return kd
        return 'inst'

    # candidate scores on the 16th grid
    cands = []
    for t, bi, k in sub:
        kk, mm, hh = at(times, kick, t), at(times, mid, t), at(times, high, t)
        vocal = 0.0
        if len(word_t):
            d = np.min(np.abs(word_t - t))
            vocal = max(0.0, 1 - d / (period / 4)) if d < period / 4 else 0.0
        on_beat = k == 0
        down = on_beat and (bi - phase) % 4 == 0
        weight = {0: 1.0, 2: 0.72, 1: 0.42, 3: 0.42}[k]
        s = (1.1 * vocal + 0.8 * kk + 0.55 * mm + 0.25 * hh) * weight + (0.35 if on_beat else 0) + (0.2 if down else 0)
        cands.append(dict(t=t, k=k, bi=bi, s=s, kick=kk, vocal=vocal, down=down, sec=section_at(t), e=energy_at(t),
                          c=float(np.interp(t, times, cent))))

    charts = []
    for name, lvl in DIFFS:
        # allowed subdivisions & density (notes per beat)
        if name == 'easy':
            allow = {0, 2} if bpm < 100 else {0}
            npb = 0.62
        elif name == 'normal':
            allow = {0, 2}
            npb = 1.05 if bpm < 140 else 0.85
        else:
            allow = {0, 1, 2, 3} if bpm < 135 else {0, 2}
            npb = 1.75 if bpm < 135 else 1.3
        if bpm < 85:
            npb *= 1.5  # slow songs need more subdivisions to stay engaging
            if name != 'easy':
                allow = allow | {1, 3}
        picked = []
        # per-bar selection proportional to energy & section
        bars = {}
        for c in cands:
            if c['k'] not in allow:
                continue
            bar = (c['bi'] - phase) // 4
            bars.setdefault(bar, []).append(c)
        for bar, cs in sorted(bars.items()):
            if not cs:
                continue
            e = np.mean([c['e'] for c in cs])
            sec = max(set(c['sec'] for c in cs), key=[c['sec'] for c in cs].count)
            mult = {'chorus': 1.25, 'bridge': 0.95, 'verse': 0.95, 'outro': 0.7, 'inst': 0.75}.get(sec, 0.9)
            quota = int(round(4 * npb * mult * (0.65 + 0.7 * e)))
            quota = max(1 if name == 'easy' else 2, quota)
            cs = sorted(cs, key=lambda c: -c['s'])
            chosen = []
            min_gap = period * (1.0 if name == 'easy' else 0.5 if name == 'normal' else 0.25) * 0.98
            for c in cs:
                if len(chosen) >= quota:
                    break
                if c['s'] < 0.35:
                    continue
                if any(abs(c['t'] - o['t']) < min_gap for o in chosen):
                    continue
                chosen.append(c)
            picked.extend(chosen)
        picked.sort(key=lambda c: c['t'])
        # enforce global min gap across bar borders
        cleaned = []
        min_gap = period * (1.0 if name == 'easy' else 0.5 if name == 'normal' else 0.25) * 0.98
        for c in picked:
            if cleaned and c['t'] - cleaned[-1]['t'] < min_gap:
                continue
            cleaned.append(c)
        picked = cleaned

        # holds on sustained vocal words
        holds = {}
        line_ends = [e for s_, e in line_times if e is not None]
        if len(word_t):
            for ws, we in zip(word_t, word_e):
                dur = we - ws
                need = 2.0 if name == 'easy' else 1.5 if name == 'normal' else 1.25
                last_word = any(abs(we - le) < 0.05 for le in line_ends)
                if dur >= need * period and (last_word or dur >= 2.2 * period):
                    q = round((dur) / (period / 2)) * (period / 2)
                    q = min(q, period * 4)
                    holds[round(ws, 2)] = max(period, q)
        notes = []
        last_lane = 1
        hold_until = [0.0] * 4
        prev_t = -9
        dirn = 1
        for idx, c in enumerate(picked):
            t = c['t']
            # pulses on section downbeats
            # lane choice: pitch proxy + flow + anti-jack
            pitch = np.clip((c['c'] - cent_lo) / max(cent_hi - cent_lo, 1), 0, 1)
            target = pitch * 3
            gap = t - prev_t
            options = []
            for lane in range(4):
                if hold_until[lane] > t - 0.02:
                    continue
                cost = abs(lane - target) * 0.6
                if lane == last_lane:
                    cost += 2.5 if gap < period * 0.6 else 0.9
                if abs(lane - last_lane) > 2:
                    cost += 0.6 if name != 'hard' else 0.2
                if gap < period * 0.3 and name == 'hard':
                    want = last_lane + dirn
                    if 0 <= want <= 3:
                        cost -= 1.2 if lane == want else 0
                cost += rng.random() * 0.8
                options.append((cost, lane))
            if not options:
                continue
            lane = min(options)[1]
            if lane in (0, 3):
                dirn = 1 if lane == 0 else -1
            hd = 0.0
            ht = round(min(word_t, key=lambda w: abs(w - t)), 2) if len(word_t) else None
            if ht is not None and ht in holds and abs(ht - t) < period / 4:
                hd = holds[ht]
                nxt = picked[idx + 1]['t'] if idx + 1 < len(picked) else length
                if name == 'easy':
                    hd = min(hd, nxt - t - period)
                if hd < period * 0.9:
                    hd = 0.0
            notes.append(dict(t=round(t, 4), lane=int(lane), type=1 if hd > 0 else 0, d=round(float(hd), 4)))
            if hd > 0:
                hold_until[lane] = t + hd + period * 0.25
            # chords on hard accents
            if name == 'hard' and c['down'] and c['kick'] > 0.7 and c['sec'] == 'chorus' and hd == 0:
                mate = 3 - lane
                if hold_until[mate] < t:
                    notes.append(dict(t=round(t, 4), lane=int(mate), type=0, d=0))
            last_lane = lane
            prev_t = t
        # heartbeat pulses: chorus starts + every 4 bars in choruses (8 elsewhere / on easy)
        pulses = []
        chorus_starts = [s_ for (s_, e_), kd in zip(line_times, kinds) if kd == 'chorus' and s_ is not None]
        chorus_starts = [cs_ for i_, cs_ in enumerate(chorus_starts) if i_ == 0 or cs_ - chorus_starts[i_ - 1] > 8 * period]
        for bi_, b in enumerate(beats):
            if (bi_ - phase) % 4 or b < 2.5 or b > length - 2.0:
                continue
            bar_i = (bi_ - phase) // 4
            sec = section_at(b + period)
            e_ = energy_at(b)
            every = 8 if name == 'easy' or sec != 'chorus' else 4
            near_chorus = any(-period * 4.5 < b - cs_ < period * 0.6 for cs_ in chorus_starts)
            if (bar_i % every == 0 and e_ > 0.35) or near_chorus:
                if not pulses or b - pulses[-1] > period * 7.5:
                    pulses.append(b)
        for pt in pulses:
            keep = []
            for n in notes:
                if abs(n['t'] - pt) < period * 0.45:
                    continue
                if n['type'] == 1 and n['t'] < pt < n['t'] + n['d'] + period * 0.25:
                    nd = pt - period * 0.5 - n['t']
                    n = dict(n, type=1 if nd >= period * 0.9 else 0, d=round(nd, 4) if nd >= period * 0.9 else 0)
                keep.append(n)
            notes = keep + [dict(t=round(float(pt), 4), lane=0, type=2, d=0)]
        hold_list = sorted([n for n in notes if n['type'] == 1], key=lambda n: n['d'])
        cap = int(len(notes) * (0.12 if name == 'easy' else 0.15 if name == 'normal' else 0.17))
        for n in hold_list[:max(0, len(hold_list) - cap)]:
            n['type'] = 0
            n['d'] = 0
        # drop notes overlapping holds in the same lane / easy: nothing during holds
        final = []
        for n in sorted(notes, key=lambda n: (n['t'], n['lane'])):
            clash = False
            for m in final:
                if m['type'] == 1 and m['t'] < n['t'] < m['t'] + m['d'] + 0.05:
                    if name == 'easy' or m['lane'] == n['lane'] or n['type'] == 2:
                        clash = True
                if n['type'] == 2 and abs(m['t'] - n['t']) < period * 0.45:
                    clash = True
                if m['type'] == 2 and abs(m['t'] - n['t']) < period * 0.45 and n is not m:
                    clash = True
            if not clash:
                final.append(n)
        # last note of the song becomes a pulse
        if final and final[-1]['type'] == 0 and name != 'easy':
            final[-1] = dict(t=final[-1]['t'], lane=0, type=2, d=0)
        nps = len(final) / max(1, (final[-1]['t'] - final[0]['t'])) if final else 0
        level = int(np.clip(round(nps * (1.9 if name == 'hard' else 1.7) + (2 if name == 'hard' else 1 if name == 'normal' else 0)), 1, 13))
        charts.append(dict(name=name, level=level, notes=final))
        print(f'   {name}: {len(final)} notes, {sum(1 for n in final if n["type"] == 1)} holds, '
              f'{sum(1 for n in final if n["type"] == 2)} pulses, nps={nps:.2f}, level={level}')
    return charts, downbeats


def tutorial_intro(chart, beats, phase, period):
    """First Beat teaches one thing at a time: taps (0-6 s), holds (6-12 s), pulses (12-22 s)."""
    for d in chart:
        rest = [n for n in d['notes'] if n['t'] >= 22.0]
        intro = []
        lanes = [1, 2, 0, 3, 1, 2, 3, 0]
        k = 0
        for b in beats:
            if 2.0 <= b < 6.0:
                intro.append(dict(t=round(b, 4), lane=lanes[k % 8], type=0, d=0)); k += 1
        hb = [b for b in beats if 6.0 <= b < 12.0]
        for i in range(0, len(hb) - 1, 2 if d['name'] != 'hard' else 2):
            intro.append(dict(t=round(hb[i], 4), lane=[0, 3, 1, 2][(i // 2) % 4], type=1, d=round(period * 1.5, 4)))
        pb = [b for i, b in enumerate(beats) if 12.0 <= b < 22.0]
        for i, b in enumerate(pb):
            if i % 4 == 0:
                intro.append(dict(t=round(b, 4), lane=0, type=2, d=0))
            elif d['name'] != 'easy' or i % 2 == 0:
                intro.append(dict(t=round(b, 4), lane=lanes[(k := k + 1) % 8], type=0, d=0))
        d['notes'] = sorted(intro + rest, key=lambda n: (n['t'], n['lane']))


def build_song(key):
    cfg = PICKS[key]
    song = song_def(key)
    src = RAW / (cfg['take'] + '.flac')
    print('==', key, src.name, flush=True)
    x = load_stereo(src)
    mono = librosa.resample(x.mean(axis=1), orig_sr=SR, target_sr=22050)
    bpm, beats, phase, _ = beat_grid(mono, 22050, cfg['bpm'])
    period = 60 / bpm
    words = whisper_words(src)
    lines, kinds, line_times = align_lyrics(song, words)
    first_vocal = next((s for s, e in line_times if s is not None), 4.0)
    # start: bar line `intro_bars` before the first vocal
    down = [b for i, b in enumerate(beats) if (i - phase) % 4 == 0]
    start_target = first_vocal - cfg['intro_bars'] * 4 * period
    start = max([b for b in down if b <= max(start_target, down[0])] or [0.0])
    if start < 2.5:
        start = 0.0
    # end
    if cfg['end_line'] is not None and line_times[cfg['end_line']][1] is not None:
        end_vocal = line_times[cfg['end_line']][1]
    else:
        end_vocal = max(e for s, e in line_times if e is not None)
    after = [b for b in down if b >= end_vocal + 0.2]
    end_bar = after[1] if len(after) > 1 else len(x) / SR
    fade = min(5.0, period * 8)
    end = min(len(x) / SR, end_bar + fade)
    print(f'   bpm={bpm:.3f} phase={phase} first_vocal={first_vocal:.2f} cut={start:.2f}..{end:.2f} ({end - start:.1f}s)')
    seg = x[int(start * SR):int(end * SR)].copy()
    n = len(seg)
    fi = int(0.012 * SR)
    seg[:fi] *= np.linspace(0, 1, fi)[:, None]
    fo = int(fade * SR)
    if end_bar < len(x) / SR - 0.5:
        seg[n - fo:] *= (0.5 + 0.5 * np.cos(np.linspace(0, np.pi, fo)))[:, None]
    else:
        seg[n - int(1.5 * SR):] *= np.linspace(1, 0, int(1.5 * SR))[:, None]
    seg = master(seg)
    length = n / SR
    mp3 = MUSIC_OUT / f'{key}.mp3'
    wav = export_mp3(seg, mp3)
    # analysis on the edited audio
    ym = librosa.resample(seg.mean(axis=1), orig_sr=SR, target_sr=22050)
    beats_e = beats - start
    beats_e = beats_e[(beats_e >= 0) & (beats_e < length)]
    phase_e = int(np.argmin([abs(((np.argmin(np.abs(beats - b)) - phase) % 4)) for b in beats_e[:1]])) if len(beats_e) else 0
    # recompute phase relative to the trimmed beats
    bi0 = int(np.argmin(np.abs(beats - (start + beats_e[0]))))
    phase_e = (phase - bi0) % 4
    words_e = [dict(w, start=w['start'] - start, end=w['end'] - start) for w in words if start <= w['start'] < end - 0.5]
    line_e = [[s - start, e - start] if s is not None and start <= s < end else [None, None] for s, e in line_times]
    rms = rms_track(ym, 22050)
    energy = np.clip((rms - np.percentile(rms, 5)) / (np.percentile(rms, 97) - np.percentile(rms, 5) + 1e-9), 0, 1)
    energy = uniform_filter1d(energy, 15)
    diffs, downbeats = make_charts(key, ym, 22050, bpm, beats_e, phase_e, words_e, lines, kinds, line_e, energy, length)
    if key == 'first_beat':
        tutorial_intro(diffs, beats_e, phase_e, period)
        for d in diffs:
            print(f'   tutorial {d["name"]}: {len(d["notes"])} notes')
    lyrics = [dict(t=round(s, 2), e=round(e, 2), text=ln) for (s, e), ln in zip(line_e, lines) if s is not None and e is not None and e < length - 1]
    sections = []
    for (s, e), kd in zip(line_e, kinds):
        if s is not None and (not sections or sections[-1]['kind'] != kd):
            sections.append(dict(t=round(s, 2), kind=kd, energy=round(float(np.interp(s, np.arange(len(energy)) / 10, energy)), 3)))
    chorus = next((s['t'] for s in sections if s['kind'] == 'chorus'), length * 0.3)
    chart = dict(song=key, title=song['title'], bpm=round(bpm, 3), offset=round(float(beats_e[0]), 4), length=round(length, 3),
                 previewStart=round(max(0.0, chorus - 2 * period), 3), gain=1.0,
                 beats=[round(float(b), 4) for b in beats_e], downbeats=[round(float(b), 4) for b in downbeats],
                 diffs=diffs, lyrics=lyrics, sections=sections, energy=[round(float(v), 3) for v in energy])
    (CHART_OUT / f'{key}.json').write_text(json.dumps(chart, separators=(',', ':')))
    print(f'   wrote {mp3.name} ({mp3.stat().st_size // 1024} KB) and chart', flush=True)


def build_bgm(key, take):
    src = RAW / f'{take}.flac'
    if not src.exists():
        print('missing', src.name)
        return
    x = load_stereo(src)
    # trim trailing silence and leading quiet, gentle fades for looping
    env = np.abs(x).max(axis=1)
    loud = np.where(uniform_filter1d(env, SR // 10) > 0.01)[0]
    a, b = (loud[0], loud[-1]) if len(loud) else (0, len(x))
    x = x[a:b + SR // 2]
    fi, fo = int(1.5 * SR), int(4 * SR)
    x[:fi] *= np.linspace(0, 1, fi)[:, None]
    x[-fo:] *= np.linspace(1, 0, fo)[:, None]
    x = master(x, target_rms_db=-18.5, ceiling=0.8)
    out = MUSIC_OUT / f'{key}.mp3'
    export_mp3(x, out, '160k')
    print('bgm', key, f'{len(x) / SR:.1f}s', out.stat().st_size // 1024, 'KB', flush=True)


def main():
    want = set(sys.argv[1:])
    for key in PICKS:
        if not want or key in want:
            build_song(key)
    for key, take in BGM.items():
        if not want or key in want:
            build_bgm(key, take)


if __name__ == '__main__':
    main()
