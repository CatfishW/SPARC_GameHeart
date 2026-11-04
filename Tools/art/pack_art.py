#!/usr/bin/env python3
"""Selected art -> StreamingAssets/Art/*.jpg (stills, backdrops) and Resources/Portraits/*.png (keyed captain).

  ~/Development/gura-wish-mv/.venv/bin/python pack_art.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
STILLS = ROOT / 'Assets/StreamingAssets/Art'
PORTRAITS = ROOT / 'Assets/Resources/Portraits'
STILLS.mkdir(parents=True, exist_ok=True)
PORTRAITS.mkdir(parents=True, exist_ok=True)

PICK = {
    'key_art': 'raw/key_art__101.png',
    'prologue_ship': 'gpt/g_prologue_ship.png',
    'heart_reveal': 'gpt/g_heart_reveal.png',
    'control_center': 'gpt/g_control_center.png',
    'cold_outside': 'gpt/kid_cold_outside.png',
    'cold_inside': 'gpt/g_cold_inside.png',
    'cold_wrong': 'gpt/g_cold_wrong.png',
    'cold_win': 'gpt/g_cold_win.png',
    'exercise_outside': 'gpt/kid_exercise_outside.png',
    'exercise_inside': 'gpt/g_exercise_inside.png',
    'exercise_wrong': 'gpt/g_exercise_wrong.png',
    'exercise_win': 'gpt/kid_exercise_win.png',
    'sick_outside': 'gpt/kid_sick_outside.png',
    'sick_inside': 'gpt/g_sick_inside.png',
    'sick_wrong': 'gpt/g_sick_wrong.png',
    'sick_win': 'gpt/g_sick_win.png',
    'sleep_outside': 'gpt/kid_sleep_outside.png',
    'sleep_inside': 'gpt/g_sleep_inside.png',
    'sleep_wrong': 'gpt/kid_sleep_wrong.png',
    'sleep_win': 'gpt/kid_sleep_win.png',
    'finale': 'gpt/g_finale.png',
    'ending': 'gpt/g_ending.png',
    'stage_tutorial': 'raw/stage_tutorial__303.png',
    'stage_cold': 'raw/stage_cold__303.png',
    'stage_exercise': 'raw/stage_exercise__303.png',
    'stage_sickness': 'raw/stage_sickness__303.png',
    'stage_sleep': 'raw/stage_sleep__303.png',
    'stage_finale': 'raw/stage_finale__303.png',
    'hub_bodymap': 'raw/hub_bodymap__404.png',
}
CAPTAIN = {
    'neutral': 'gpt/captain_base.png', 'happy': 'gpt/captain_happy.png', 'excited': 'gpt/captain_excited.png',
    'worried': 'gpt/captain_worried.png', 'thinking': 'gpt/captain_thinking.png', 'cold': 'gpt/captain_cold.png',
    'sleepy': 'gpt/captain_sleepy.png',
}


def matte(im):
    rgb = np.asarray(im.convert('RGB')).astype(float) / 255
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1); mn = rgb.min(-1); sat = (mx - mn) / np.maximum(mx, 1e-3)
    hue = np.zeros_like(mx); m = mx == g
    hue[m] = (60 * ((b - r) / np.maximum(mx - mn, 1e-3)) + 120)[m]
    green = m & (hue > 80) & (hue < 165) & (sat > 0.30) & (mx > 0.20)
    labels, n = ndimage.label(green)
    edge = set(labels[0]) | set(labels[-1]) | set(labels[:, 0]) | set(labels[:, -1]); edge.discard(0)
    outside = np.isin(labels, list(edge))
    backdrop = np.median(rgb[outside], axis=0) if outside.any() else np.array([0, 1, 0])
    holes = green & (np.linalg.norm(rgb - backdrop, axis=-1) < 0.22)
    mask = ndimage.binary_opening(outside | holes, iterations=1)
    keep = ndimage.binary_erosion(~mask, iterations=1)
    alpha = ndimage.gaussian_filter(keep.astype(float), 0.9)
    alpha = np.clip((alpha - 0.15) / 0.7, 0, 1)
    # despill: pull green toward max(r,b) near edges
    near = ndimage.gaussian_filter((~keep).astype(float), 4) > 0.01
    spill = np.clip((g - np.maximum(r, b)), 0, 1) * near
    g2 = g - spill
    out = np.dstack([r, g2, b, alpha])
    return Image.fromarray((out * 255).astype('uint8'), 'RGBA')


def trim(im, pad=8):
    a = np.asarray(im)[..., 3] > 16
    labels, n = ndimage.label(a)
    if n > 1:
        sizes = ndimage.sum(a, labels, range(1, n + 1))
        big = np.isin(labels, [i + 1 for i, s in enumerate(sizes) if s > 400])
        data = np.asarray(im).copy(); data[..., 3] = data[..., 3] * big
        im = Image.fromarray(data, 'RGBA'); a = big
    ys, xs = np.where(a)
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + pad), im.height)
    return im.crop(box)


def main():
    total = 0
    for key, src in PICK.items():
        im = Image.open(HERE / src).convert('RGB')
        if im.width > 1600:
            im = im.resize((1600, round(1600 * im.height / im.width)), Image.LANCZOS)
        elif im.width < 1600:
            im = im.resize((1600, round(1600 * im.height / im.width)), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1.2, 40, 2))
        out = STILLS / f'{key}.jpg'
        im.save(out, quality=86, optimize=True, progressive=False)
        total += out.stat().st_size
        print(f'{key:18s} {im.size} {out.stat().st_size // 1024} KB')
    print('stills total', total // 1024, 'KB')
    for mood, src in CAPTAIN.items():
        im = trim(matte(Image.open(HERE / src)))
        h = 1024
        im = im.resize((round(h * im.width / im.height), h), Image.LANCZOS)
        # pad to a fixed canvas so moods line up
        canvas = Image.new('RGBA', (768, 1024), (0, 0, 0, 0))
        x = (768 - im.width) // 2
        canvas.alpha_composite(im if im.width <= 768 else im.crop(((im.width - 768) // 2, 0, (im.width - 768) // 2 + 768, h)), (max(0, x), 0))
        canvas.save(PORTRAITS / f'captain_{mood}.png', optimize=True)
        print('portrait', mood, im.size)


if __name__ == '__main__':
    main()
