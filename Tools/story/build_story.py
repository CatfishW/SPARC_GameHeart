#!/usr/bin/env python3
"""script.py -> Assets/Resources/Data/story.json and Tools/voice/lines.json.

Every voiced line gets a stable id derived from its text, so re-running only
asks the TTS batch for lines that changed.
"""
import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import script  # noqa: E402

OUT = ROOT / 'Assets/Resources/Data/story.json'
VOICE = ROOT / 'Tools/voice/lines.json'

SPOKEN = [('SA node', 'S A node'), ('°C', ' degrees'), ('…', '...'), ('—', ', '), ('100,000', 'one hundred thousand')]
voice_lines = {}


def spoken(text):
    for a, b in SPOKEN:
        text = text.replace(a, b)
    return text


def line(entry):
    speaker, mood, text = entry
    out = dict(speaker=speaker, mood=mood, text=text, voice='')
    if script.CAST[speaker]['voice']:
        vid = 'v_' + hashlib.sha1((speaker + '|' + text).encode()).hexdigest()[:10]
        out['voice'] = vid
        voice_lines[vid] = dict(id=vid, speaker=speaker, mood=mood, text=spoken(text))
    return out


def shots(seq):
    return [dict(art=s['art'], move=s.get('move', 'drift'), fx=s.get('fx') or '', tint=s.get('tint', '#FFFFFF'),
                 lines=[line(x) for x in s['lines']]) for s in (seq or [])]


def main():
    chapters = []
    for c in script.CHAPTERS:
        d = c.get('decision')
        decision = None
        if d:
            decision = dict(prompt=d['prompt'], correct=d['correct'],
                            vitals=[dict(label=a, value=b, trend=t) for a, b, t in d['vitals']],
                            feedback=[dict(choice=k, shots=shots(v)) for k, v in d['feedback'].items()],
                            right=[line(x) for x in d['right']])
        chapters.append(dict(
            id=c['id'], index=c['index'], title=c['title'], subtitle=c['subtitle'], song=c['song'],
            songTitle=c['song_title'], artist=c['artist'], bpmHint=c['bpm_hint'], color=c['color'],
            accent=c['accent'], stage=c['stage'], theme=c['theme'], intro=shots(c['intro']),
            decision=decision, win=shots(c['win']), struggle=shots(c['struggle']),
            quiz=[dict(q=q['q'], options=q['options'], answer=q['answer'], explain=q['explain']) for q in c['quiz']],
        ))
    barks = [dict(key=k, line=line(v)) for k, v in script.UI_LINES.items()]
    story = dict(prologue=dict(id='prologue', shots=shots(script.PROLOGUE['shots'])), chapters=chapters, barks=barks)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(story, indent=1, ensure_ascii=False))
    VOICE.parent.mkdir(parents=True, exist_ok=True)
    VOICE.write_text(json.dumps(list(voice_lines.values()), indent=1, ensure_ascii=False))
    print(f'story.json: {len(chapters)} chapters; {len(voice_lines)} voiced lines')


if __name__ == '__main__':
    main()
