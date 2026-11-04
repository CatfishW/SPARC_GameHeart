#!/usr/bin/env python3
"""Static audit: every asset the story references exists, and every chart is well-formed and playable."""
import json, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
A = ROOT / 'Assets'
story = json.loads((A / 'Resources/Data/story.json').read_text())
voice = {v['id'] for v in json.loads((A / 'Resources/Data/voice.json').read_text())['lines']}
problems = []
def shots(seq, where):
    for s in seq or []:
        if not (A / f"StreamingAssets/Art/{s['art']}.jpg").exists(): problems.append(f'{where}: missing art {s["art"]}')
        for ln in s['lines']:
            if ln['speaker'] == 'B':
                if ln['voice'] not in voice or not (A / f"Resources/Voice/{ln['voice']}.wav").exists(): problems.append(f'{where}: missing voice {ln["voice"]}')
                if not (A / f"Resources/Portraits/captain_{ln['mood'] or 'neutral'}.png").exists(): problems.append(f'{where}: missing mood {ln["mood"]}')
shots(story['prologue']['shots'], 'prologue')
for b in story['barks']:
    if b['line']['voice'] not in voice: problems.append(f'bark {b["key"]} missing voice')
for c in story['chapters']:
    w = c['id']
    for k in ('intro', 'win', 'struggle'): shots(c[k], f'{w}.{k}')
    if c.get('decision') and c['decision'].get('prompt'):
        for f in c['decision']['feedback']: shots(f['shots'], f'{w}.feedback.{f["choice"]}')
        for ln in c['decision']['right']:
            if ln['voice'] not in voice: problems.append(f'{w}.right missing voice')
        choices = {f['choice'] for f in c['decision']['feedback']} | {c['decision']['correct']}
        if choices != {'faster', 'slower', 'same'}: problems.append(f'{w}: decision choices {choices}')
    for q in c['quiz']:
        if not 0 <= q['answer'] < len(q['options']): problems.append(f'{w}: bad quiz answer')
    if not (A / f"StreamingAssets/Art/{c['stage']}.jpg").exists(): problems.append(f'{w}: missing stage {c["stage"]}')
    if not (A / f"StreamingAssets/Music/{c['song']}.mp3").exists(): problems.append(f'{w}: missing song mp3')
    if not (A / f"StreamingAssets/Music/{c['song']}_inst.mp3").exists(): problems.append(f'{w}: missing underscore')
    chart = json.loads((A / f"Resources/Charts/{c['song']}.json").read_text())
    P = 60 / chart['bpm']
    for d in chart['diffs']:
        ns = d['notes']
        tag = f"{c['song']}:{d['name']}"
        if [n['t'] for n in ns] != sorted(n['t'] for n in ns): problems.append(f'{tag}: unsorted')
        if ns[0]['t'] < 1.5: problems.append(f'{tag}: first note at {ns[0]["t"]:.2f}s')
        if ns[-1]['t'] + ns[-1]['d'] > chart['length'] - 0.4: problems.append(f'{tag}: last note too close to end')
        holds = [n for n in ns if n['type'] == 1]
        for n in ns:
            if not 0 <= n['lane'] <= 3: problems.append(f'{tag}: lane {n["lane"]}')
            if n['type'] == 1 and n['d'] <= 0: problems.append(f'{tag}: zero hold')
            for h in holds:
                if h is not n and h['lane'] == n['lane'] and h['t'] - 0.01 <= n['t'] <= h['t'] + h['d'] + 0.05 and n['type'] != 2:
                    problems.append(f'{tag}: note inside same-lane hold at {n["t"]:.2f}')
        same = {}
        for n in ns:
            key = (round(n['t'], 3), n['lane'] if n['type'] != 2 else -1)
            if key in same: problems.append(f'{tag}: duplicate note at {n["t"]:.2f}')
            same[key] = 1
        per_t = {}
        for n in ns: per_t.setdefault(round(n['t'], 3), []).append(n)
        for t, group in per_t.items():
            if any(g['type'] == 2 for g in group) and len(group) > 1: problems.append(f'{tag}: pulse shares time {t}')
            if len(group) > 2: problems.append(f'{tag}: {len(group)} notes at once {t}')
        counts = {k: sum(1 for n in ns if n['type'] == k) for k in (0, 1, 2)}
        print(f"{tag:22s} lvl {d['level']:2d}  taps {counts[0]:3d} holds {counts[1]:3d} pulses {counts[2]:2d}  span {ns[0]['t']:.1f}-{ns[-1]['t']:.1f}s / {chart['length']:.1f}s")
for k in ['title_theme', 'control_center']:
    if not (A / f'StreamingAssets/Music/{k}.mp3').exists(): problems.append(f'missing {k}')
print('\nPROBLEMS:' if problems else '\nAll content checks passed.')
for p in problems[:80]: print(' -', p)
sys.exit(1 if problems else 0)
