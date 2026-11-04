#!/usr/bin/env python3
"""Stage Builds/WebGL for the ping.agaii.org static location and print a manifest with sha256 hashes.

Usage: python3 Tools/deploy/stage.py [--src Builds/WebGL] [--out Builds/stage]
The build uses uncompressed, content-hashed Build/* files, so no Content-Encoding headers are needed.
"""
import argparse, hashlib, json, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ap = argparse.ArgumentParser()
ap.add_argument('--src', default=str(ROOT / 'Builds/WebGL'))
ap.add_argument('--out', default=str(ROOT / 'Builds/stage'))
o = ap.parse_args()
src, out = Path(o.src), Path(o.out)
assert (src / 'index.html').exists(), 'no build at ' + str(src)
assert not list(src.rglob('*.unityweb')) and not list(src.rglob('*.gz')) and not list(src.rglob('*.br')), 'compressed build files found'
if out.exists():
    shutil.rmtree(out)
shutil.copytree(src, out, ignore=shutil.ignore_patterns('*_DoNotShip', '*.meta'))
# content-hash the Build/* files for cache busting and point index.html at them
index = (out / 'index.html').read_text()
for f in sorted((out / 'Build').iterdir()):
    h = hashlib.sha256(f.read_bytes()).hexdigest()[:12]
    stem, dot, ext = f.name.partition('.')
    new = f'{stem}.{h}.{ext}'
    f.rename(f.with_name(new))
    assert f.name in index, f'{f.name} not referenced in index.html'
    index = index.replace(f.name, new)
(out / 'index.html').write_text(index)
manifest = {}
total = 0
for p in sorted(out.rglob('*')):
    if p.is_file():
        manifest[str(p.relative_to(out))] = hashlib.sha256(p.read_bytes()).hexdigest()
        total += p.stat().st_size
(ROOT / 'Builds/stage-manifest.json').write_text(json.dumps(manifest, indent=1))
print(f'staged {len(manifest)} files, {total / 1e6:.1f} MB -> {out}')
for k in manifest:
    if k.startswith('Build/') or k == 'index.html':
        print(f'  {k}  {manifest[k][:16]}')
