#!/usr/bin/env python3
"""Compare the live site with Builds/stage-manifest.json (sha256 per file)."""
import hashlib, json, sys, urllib.request
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
BASE = sys.argv[1] if len(sys.argv) > 1 else 'https://ping.agaii.org/game/gameheart/'
man = json.loads((ROOT / 'Builds/stage-manifest.json').read_text())
bad = 0
for rel, h in man.items():
    url = BASE + urllib.request.quote(rel)
    try:
        data = urllib.request.urlopen(urllib.request.Request(url, headers={'Cache-Control': 'no-cache', 'User-Agent': 'gameheart-verify'}), timeout=60).read()
        ok = hashlib.sha256(data).hexdigest() == h
    except Exception as e:
        ok = False
        print('ERR', rel, e)
    if not ok:
        bad += 1
        print('MISMATCH', rel)
print(f'{len(man) - bad}/{len(man)} files match live')
sys.exit(1 if bad else 0)
