"""Game Heart: Rhythm of Life — the whole story in one place.

Run build_story.py to turn this into Assets/Resources/Data/story.json plus the
voice-line list used by Tools/voice/tts_batch.py.

Dialogue keeps the lessons and several lines from the original SPARC Game Heart
storyline (cold → faster, exercise → faster, sickness → faster, sleep → slower).

Shot fields
  art      painted still in Resources/Art/Stills (1344x768) — see Tools/art/jobs.py
  move     camera move over the still: push, pull, panL, panR, rise, sink, drift
  tint     colour grade multiplier as hex
  lines    [(speaker, mood, text)] — speaker "B" (Captain B, voiced) or "N" (caption card)
  fx       optional ambient particle layer: snow, embers, germs, stars, cells, sparks, confetti
"""

CAST = {
    'B': dict(name='Captain B', voice='captain'),
    'N': dict(name='', voice=None),
}

PROLOGUE = dict(id='prologue', shots=[
    dict(art='prologue_ship', move='push', fx='cells', lines=[
        ('N', '', 'Somewhere inside a human body…'),
        ('B', 'happy', "Explorer! Welcome aboard. We've shrunk down smaller than a single blood cell."),
        ('B', 'neutral', "Follow those red blood cells. They'll carry us straight to the most important station in the body."),
    ]),
    dict(art='heart_reveal', move='rise', fx='cells', lines=[
        ('B', 'excited', 'Whoa… getting a close-up view of the heart is truly awe-inspiring!'),
        ('B', 'neutral', 'The heart is a powerful pump, working day and night to move blood through your whole body.'),
    ]),
    dict(art='control_center', move='pull', fx='sparks', lines=[
        ('B', 'thinking', 'This is the heartbeat control center. A tiny spark called the SA node sets the pace of every beat.'),
        ('B', 'happy', 'The heart changes its rhythm depending on what the body needs. Are you ready to take control?'),
    ]),
])

CHAPTERS = [
    dict(
        id='tutorial', index=1, title='First Beat', subtitle='Warm-Up at the Control Center',
        song='first_beat', song_title='First Beat', artist='Game Heart Sound Team',
        bpm_hint=108, color='#FF5A6E', accent='#FFD1D8', stage='stage_tutorial', theme='warm',
        intro=[
            dict(art='control_center', move='drift', fx='sparks', lines=[
                ('B', 'happy', "Let's start with a warm-up! Electrical signals will race down these lanes toward the beat line."),
                ('B', 'neutral', 'Tap each signal right as it reaches the line. Hold the long ones to keep the blood flowing.'),
                ('B', 'excited', "When a wide pulse arrives, that's a full heartbeat. Hit it with everything you've got!"),
            ]),
        ],
        decision=None,
        win=[dict(art='control_center', move='push', fx='sparks', lines=[
            ('B', 'excited', "Great job! You've got the hang of it. Now let's put your skills to the test out in the body."),
        ])],
        struggle=[dict(art='control_center', move='push', fx='sparks', lines=[
            ('B', 'neutral', 'Not bad for a first try! Watch the beat line and keep your taps steady.'),
        ])],
        quiz=[
            dict(q="The heart's main job in the body is to…",
                 options=['Pump blood throughout the body', 'Filter toxins from the bloodstream', 'Digest and absorb nutrients',
                          'Control signals from the brain'],
                 answer=0, explain='The heart is a muscular pump. Every beat pushes blood through the whole body.'),
            dict(q='The heart is one of the key organs in the ___ system.',
                 options=['Circulatory', 'Respiratory', 'Digestive', 'Nervous'],
                 answer=0, explain='The heart, blood, and blood vessels make up the circulatory system.'),
        ],
    ),
    dict(
        id='cold', index=2, title='Cold Snap', subtitle='A Winter Storm Hits',
        song='frostbite', song_title='Frostbite Pulse', artist='Game Heart Sound Team',
        bpm_hint=124, color='#5CC8FF', accent='#D6F3FF', stage='stage_cold', theme='cold',
        intro=[
            dict(art='cold_outside', move='panR', fx='snow', lines=[
                ('B', 'cold', 'Brrr! The body just stepped out into a snowstorm.'),
            ]),
            dict(art='cold_inside', move='push', fx='snow', lines=[
                ('B', 'thinking', 'Look, the blood vessels near the skin are narrowing to keep the warmth inside.'),
                ('B', 'cold', 'The muscles are shivering to make heat, and they need extra fuel to do it.'),
            ]),
        ],
        decision=dict(
            prompt="It's getting really cold. How should the heart respond?",
            vitals=[('Body temperature', '35.6 °C', 'down'), ('Skin vessels', 'Narrowed', 'warn'), ('Muscles', 'Shivering', 'warn')],
            correct='faster',
            feedback=dict(
                slower=[dict(art='cold_wrong', move='sink', fx='snow', lines=[
                    ('B', 'worried', "Uh-oh! With a slower beat, warm blood can't reach the core fast enough. The body's getting colder!"),
                ])],
                same=[dict(art='cold_wrong', move='sink', fx='snow', lines=[
                    ('B', 'worried', "Hmm, the same pace isn't enough. Shivering muscles are burning fuel and need more blood."),
                ])],
            ),
            right=[('B', 'excited', "That's it! The heart needs to beat faster when it's cold to keep the body warm. Let's go!")],
        ),
        win=[dict(art='cold_win', move='pull', fx='embers', lines=[
            ('B', 'happy', "Fantastic! Warm blood is flowing to the core. The body's holding on to its heat."),
        ])],
        struggle=[dict(art='cold_wrong', move='drift', fx='snow', lines=[
            ('B', 'worried', 'We kept the body going, but the rhythm slipped. A steadier beat would keep it warmer.'),
        ])],
        quiz=[
            dict(q='To keep warm in a cold environment, the heart should…',
                 options=['Beat faster', 'Beat slower', 'Stop temporarily', 'Keep exactly the same rhythm'],
                 answer=0, explain='A faster heartbeat moves warm blood to the core while shivering muscles make heat.'),
        ],
    ),
    dict(
        id='exercise', index=3, title='Full Sprint', subtitle='Race Day',
        song='sprint', song_title='Sprint Circuit', artist='Game Heart Sound Team',
        bpm_hint=150, color='#FF9A3C', accent='#FFE3C7', stage='stage_exercise', theme='fire',
        intro=[
            dict(art='exercise_outside', move='panL', fx='sparks', lines=[
                ('B', 'excited', "Time to get moving! The body's running a race."),
            ]),
            dict(art='exercise_inside', move='push', fx='cells', lines=[
                ('B', 'thinking', 'Working muscles need lots of oxygen, and the lungs are breathing faster to bring it in.'),
            ]),
        ],
        decision=dict(
            prompt='The muscles are calling for more oxygen. How should the heart respond?',
            vitals=[('Breathing', 'Fast', 'up'), ('Muscle oxygen demand', 'Very high', 'warn'), ('Carbon dioxide', 'Rising', 'up')],
            correct='faster',
            feedback=dict(
                slower=[dict(art='exercise_wrong', move='sink', fx=None, lines=[
                    ('B', 'worried', 'Oh no, the legs feel heavy! Without enough oxygen, the muscles tire out fast.'),
                ])],
                same=[dict(art='exercise_wrong', move='sink', fx=None, lines=[
                    ('B', 'worried', "Looks like not enough oxygen is getting to the limbs. Better speed it up!"),
                ])],
            ),
            right=[('B', 'excited', 'Exactly! A faster heartbeat delivers more oxygen to the muscles. Let\'s race!')],
        ),
        win=[dict(art='exercise_win', move='pull', fx='confetti', lines=[
            ('B', 'excited', 'That fast heart rate is exactly what we needed to send enough oxygen around. What a finish!'),
        ])],
        struggle=[dict(art='exercise_wrong', move='drift', fx=None, lines=[
            ('B', 'worried', 'We crossed the line, but the muscles ran low on oxygen. A stronger rhythm will power them up.'),
        ])],
        quiz=[
            dict(q='The heart beats faster during exercise to deliver more ___ to the muscles.',
                 options=['Oxygen', 'Carbon dioxide', 'Lactic acid', 'Calcium'],
                 answer=0, explain='Red blood cells carry oxygen. A faster heart delivers more of it every minute.'),
        ],
    ),
    dict(
        id='sickness', index=4, title='Fever Front', subtitle='Germs Have Broken In',
        song='fever', song_title='Fever Front', artist='Game Heart Sound Team',
        bpm_hint=140, color='#7DFF6B', accent='#E2FFD9', stage='stage_sickness', theme='toxic',
        intro=[
            dict(art='sick_outside', move='drift', fx=None, lines=[
                ('B', 'worried', 'Uh-oh, the body is sick. Germs have slipped past the defenses.'),
            ]),
            dict(art='sick_inside', move='push', fx='germs', lines=[
                ('B', 'thinking', "The body's turning up the heat with a fever, and the white blood cells are getting ready to fight."),
            ]),
        ],
        decision=dict(
            prompt='The immune system needs backup. How should the heart respond?',
            vitals=[('Temperature', '38.9 °C', 'up'), ('Germs', 'Multiplying', 'warn'), ('White blood cells', 'Mobilizing', 'up')],
            correct='faster',
            feedback=dict(
                slower=[dict(art='sick_wrong', move='sink', fx='germs', lines=[
                    ('B', 'worried', "The white blood cells can't reach the germs in time! The infection is spreading."),
                ])],
                same=[dict(art='sick_wrong', move='sink', fx='germs', lines=[
                    ('B', 'worried', 'The defenders are moving too slowly. The heart must work harder to fight off illness.'),
                ])],
            ),
            right=[('B', 'excited', 'Great! A faster heart rate carries the defenders where they are needed. Charge!')],
        ),
        win=[dict(art='sick_win', move='pull', fx='sparks', lines=[
            ('B', 'happy', "We did it! The fever's breaking and the germs are on the run."),
        ])],
        struggle=[dict(art='sick_wrong', move='drift', fx='germs', lines=[
            ('B', 'worried', "The body's still fighting. A stronger rhythm would get the defenders there faster."),
        ])],
        quiz=[
            dict(q='When the body has a fever and is fighting germs, the heart usually…',
                 options=['Beats faster', 'Beats slower', 'Takes a break', 'Beats only at night'],
                 answer=0, explain='A faster heartbeat helps rush white blood cells to fight the infection.'),
        ],
    ),
    dict(
        id='sleep', index=5, title='Night Tide', subtitle='Lights Out',
        song='night_tide', song_title='Night Tide', artist='Game Heart Sound Team',
        bpm_hint=72, color='#9D8CFF', accent='#E6E1FF', stage='stage_sleep', theme='night',
        intro=[
            dict(art='sleep_outside', move='drift', fx='stars', lines=[
                ('B', 'sleepy', "Shh… it's bedtime. The body's had a long, busy day."),
            ]),
            dict(art='sleep_inside', move='push', fx='stars', lines=[
                ('B', 'thinking', 'While we sleep, the body repairs muscles and stores energy for tomorrow.'),
            ]),
        ],
        decision=dict(
            prompt="Help the heart get a good night's rest. How should it respond?",
            vitals=[('Activity', 'Resting', 'down'), ('Oxygen demand', 'Low', 'down'), ('Repair mode', 'On', 'ok')],
            correct='slower',
            feedback=dict(
                faster=[dict(art='sleep_wrong', move='sink', fx=None, lines=[
                    ('B', 'worried', 'Too fast! Heartbeats should be slower to let the body sleep.'),
                ])],
                same=[dict(art='sleep_wrong', move='sink', fx=None, lines=[
                    ('B', 'thinking', "Hmm, the body doesn't need daytime power right now. It's time to slow down."),
                ])],
            ),
            right=[('B', 'sleepy', 'Perfect. During sleep, the heart rate slows down to help the body recover and rest.')],
        ),
        win=[dict(art='sleep_win', move='rise', fx='embers', lines=[
            ('B', 'happy', 'Good morning! The body woke up fully recharged.'),
        ])],
        struggle=[dict(art='sleep_wrong', move='drift', fx=None, lines=[
            ('B', 'sleepy', 'A few bumps in the night… a calmer rhythm will help the body rest.'),
        ])],
        quiz=[
            dict(q='During ___, the heart rate slows down to help the body recover and rest.',
                 options=['Sleep', 'Exercise', 'Stress', 'Fear'],
                 answer=0, explain='Asleep, the body needs less oxygen, so the heart can slow down and save energy.'),
        ],
    ),
    dict(
        id='finale', index=6, title='Symphony of the Heart', subtitle='Every Beat Together',
        song='symphony', song_title='Symphony of the Heart', artist='Game Heart Sound Team',
        bpm_hint=128, color='#FF4FD8', accent='#FFD9F6', stage='stage_finale', theme='finale',
        intro=[
            dict(art='finale', move='rise', fx='sparks', lines=[
                ('B', 'happy', "Explorer, look how far we've come. Cold, exercise, fever, and sleep."),
                ('B', 'thinking', 'Every situation needed a different rhythm, and you found the right one each time.'),
                ('B', 'excited', "Now let's play the heart's whole story, from the very first beat!"),
            ]),
        ],
        decision=None,
        win=[
            dict(art='ending', move='pull', fx='confetti', lines=[
                ('B', 'excited', "Fantastic work! You've mastered the art of controlling the heart's rhythm."),
                ('B', 'happy', 'Remember, your heart is always working, adjusting to keep you healthy and strong.'),
                ('B', 'neutral', "Every beat pushes life-giving blood, oxygen, and nutrients to where they're needed."),
                ('B', 'excited', "Now let's head for our next station and continue exploring!"),
            ]),
        ],
        struggle=[
            dict(art='ending', move='pull', fx='sparks', lines=[
                ('B', 'happy', "What a performance! The heart kept going all the way through."),
                ('B', 'happy', 'Remember, your heart is always working, adjusting to keep you healthy and strong.'),
                ('B', 'excited', "Now let's head for our next station and continue exploring!"),
            ]),
        ],
        quiz=[
            dict(q='About how many times does a heart beat in one day?',
                 options=['About 100,000 times', 'About 100 times', 'About 1,000 times', 'About 10 million times'],
                 answer=0, explain='At around 70 beats per minute, a heart beats roughly 100,000 times every day.'),
        ],
    ),
]

UI_LINES = {
    # Short barks Captain B says during play (voiced).
    'bark_fullcombo': ('B', 'excited', 'Full combo! Perfect rhythm!'),
    'bark_clear': ('B', 'happy', 'Rhythm restored!'),
    'bark_fail': ('B', 'worried', "Hang in there, we'll get it next time."),
    'bark_choose': ('B', 'thinking', "What does the body need? Choose the heart's rhythm."),
    'bark_quiz': ('B', 'happy', 'Pulse check! Let us see what you learned.'),
    'bark_correct': ('B', 'excited', 'Correct!'),
    'bark_wrong': ('B', 'thinking', 'Not quite. Here is why.'),
}
