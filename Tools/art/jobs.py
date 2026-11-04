#!/usr/bin/env python3
"""Prompt list for every painted image in Game Heart: Rhythm of Life.

Writes jobs.json for qwen_task.py (Qwen-Image 2.1 + Pruna 6-step on chongyou GPU 1).
Phase 1: stills, stage backdrops, hub, and the neutral captain portrait.
Phase 2 (--phase 2): captain expressions edited from the chosen neutral portrait.
"""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent

FILM = ('cinematic stylized 3D animated feature film still, soft volumetric lighting, glowing bioluminescent details, '
        'rich crimson coral and teal palette, highly detailed, depth of field, no text, no watermark')
BODY = 'microscopic world inside the human body'
KID = 'a cheerful 10-year-old child with short curly black hair and warm brown skin'

STILLS = {
    'key_art': f'Epic key art: a colossal glowing human heart floating in a vast crimson cavern, {BODY}, rivers of glowing red blood cells spiraling around it, a tiny white nano-submarine with headlights approaching, god rays, dark empty space in the lower third, majestic',
    'prologue_ship': f'View from the cockpit window of a tiny white nano-submarine traveling through a glowing red artery tunnel, {BODY}, many red blood cells shaped like biconcave discs drifting past, warm light at the end of the tunnel, sense of speed',
    'heart_reveal': f'A gigantic human heart revealed inside a cathedral-like chest cavity, glowing arteries and veins, ribs arching like pillars, a tiny white nano-submarine for scale, awe, crimson and gold light',
    'control_center': 'Futuristic heartbeat control center inside the heart, curved holographic screens showing glowing ECG heartbeat lines, a bright pulsing electric spark node in the center, organic red walls with glowing circuits, cozy sci-fi cockpit, teal holograms against crimson',
    'cold_outside': f'{KID} in a puffy red winter coat and scarf walking through a snowstorm at dusk, breath fogging, snowy town street, cold blue light, warm windows in the background',
    'cold_inside': f'Inside a narrowed blood vessel near the skin, frost crystals forming on glowing red vessel walls, icy blue light, red blood cells squeezing through, cold mist, {BODY}',
    'cold_wrong': f'A frozen blood vessel, sluggish dim red blood cells covered in ice crystals, everything turning pale blue and dark, flickering red warning light, ominous cold, {BODY}',
    'cold_win': f'Warm golden blood flowing through glowing vessels, melting frost turning into droplets, cozy amber light, red blood cells rushing happily, warmth spreading, {BODY}',
    'exercise_outside': f'{KID} in a sports jersey sprinting on a red running track in a sunny stadium, motion blur, cheering crowd, dynamic low angle, summer light',
    'exercise_inside': f'Inside a working leg muscle, glowing orange muscle fibers flexing like cables, streams of bright red blood cells rushing in through capillaries carrying oxygen bubbles, energetic sparks, {BODY}',
    'exercise_wrong': f'Tired muscle fibers dimming to grey, only a few red blood cells trickling in, heavy fog, exhausted feeling, dim orange warning lights, {BODY}',
    'exercise_win': f'{KID} in a sports jersey crossing the finish line with arms raised in joy, confetti, sunny stadium, golden light, triumphant',
    'sick_outside': f'{KID} lying sick in bed with a thermometer, flushed cheeks, cozy bedroom, cup of tea and tissues on the nightstand, soft afternoon light',
    'sick_inside': f'Inside a blood vessel, spiky green germs swarming, brave round white blood cells charging toward them, dramatic battle lighting, green and crimson glow, {BODY}',
    'sick_wrong': f'Green germs multiplying and spreading everywhere inside a blood vessel, a few white blood cells stuck far behind, sickly green haze, ominous red warning glow, {BODY}',
    'sick_win': f'Inside a blood vessel, victorious white blood cells, defeated green germs dissolving into sparkles, clean bright red vessel walls, triumphant warm light, {BODY}',
    'sleep_outside': f'{KID} sleeping peacefully in a cozy bedroom at night, moonlight through the window, stars, soft blue and purple tones, a small night lamp',
    'sleep_inside': f'Calm deep indigo blood vessels at night with soft glowing repair particles mending muscle fibers, slow drifting red blood cells, dreamy and serene, {BODY}',
    'sleep_wrong': f'{KID} tossing and turning in bed at night, wide awake, messy blankets, alarm clock glowing red, restless mood',
    'sleep_win': f'Sunrise through a bedroom window, {KID} stretching happily in bed after a good sleep, golden morning light, birds outside',
    'finale': f'A magnificent human heart glowing with rainbow light, four swirling rivers of light around it in icy blue, fiery orange, emerald green and starry violet, celebration, epic, {BODY}',
    'ending': f'A tiny white nano-submarine sailing away from a glowing heart through a bright artery toward a new adventure, warm sunrise light at the end of the tunnel, hopeful farewell, {BODY}',
}

TUNNEL = ('Looking straight down a long round blood vessel tunnel, perfectly symmetrical, vanishing point exactly in the center, '
          'dark low-contrast center, glowing ribbed walls, cinematic, ' + BODY)
STAGES = {
    'stage_tutorial': TUNNEL + ', warm crimson and coral light, a few red blood cells drifting',
    'stage_cold': TUNNEL + ', frost and ice crystals on the walls, icy blue and teal light, drifting snow-like particles',
    'stage_exercise': TUNNEL + ', fiery orange and red energy, glowing speed streaks along the walls',
    'stage_sickness': TUNNEL + ', toxic green haze, small spiky germs clinging to the walls, crimson accents',
    'stage_sleep': TUNNEL + ', deep indigo and violet, tiny star-like glowing particles, dreamy calm',
    'stage_finale': TUNNEL + ', prismatic rainbow light, magenta and gold, celebration sparkles',
    'hub_bodymap': 'A holographic glowing human body silhouette standing in a dark sci-fi lab, glowing circulatory system with a bright heart at the center, teal and crimson holograms, centered, lots of dark negative space, no text',
}

CAPTAIN = ('Character portrait, waist-up, a friendly young explorer captain, a man in his late twenties with short spiky auburn hair, '
           'brass goggles pushed up on his forehead, white and crimson nano-suit with a glowing heart emblem on the chest, '
           'stylized 3D animated film character, facing the viewer, centered, plain flat bright green background')
EXPRESSIONS = {
    'captain_happy': 'warm friendly smile, relaxed and welcoming',
    'captain_excited': 'big excited grin, one fist raised in triumph, eyes wide with energy',
    'captain_worried': 'worried expression, eyebrows raised, biting lip, hand on cheek',
    'captain_thinking': 'thoughtful expression, finger on chin, looking slightly up',
    'captain_cold': 'shivering with cold, rubbing his arms, frost on his goggles, a red scarf around his neck',
    'captain_sleepy': 'sleepy expression, half-closed eyes, yawning with a hand over his mouth',
}


def main():
    phase = 2 if '--phase' in sys.argv and sys.argv[sys.argv.index('--phase') + 1] == '2' else 1
    jobs = []
    if phase == 1:
        for k, p in STILLS.items():
            for seed in (101, 202):
                jobs.append(dict(id=f'{k}__{seed}', prompt=f'{p}. {FILM}', width=1344, height=768, seed=seed))
        for k, p in STAGES.items():
            for seed in (303, 404):
                jobs.append(dict(id=f'{k}__{seed}', prompt=f'{p}. {FILM}', width=1344, height=768, seed=seed))
        for seed in (11, 22, 33, 44):
            jobs.append(dict(id=f'captain_neutral__{seed}', prompt=f'{CAPTAIN}, calm confident smile', width=896, height=1152, seed=seed))
    else:
        for k, e in EXPRESSIONS.items():
            for seed in (55, 66):
                jobs.append(dict(id=f'{k}__{seed}', width=896, height=1152, seed=seed, refs=['captain_ref.png'],
                                 prompt=f'The same character from the reference image, same face, hair, goggles and suit, now with {e}. '
                                        'Waist-up, centered, plain flat bright green background, stylized 3D animated film character'))
    (HERE / f'jobs_phase{phase}.json').write_text(json.dumps(jobs, indent=1))
    print(f'phase {phase}: {len(jobs)} jobs')


if __name__ == '__main__':
    main()
