#!/usr/bin/env python3
"""Submit every take in songs.py to the YuE2 worker one by one and download the results.

The worker runs on benwulab-remote and is reached at tang-server 127.0.0.1:28190 through
the existing WarmPix tunnel. Only one job is in flight at a time so the shared GPU is not
crowded. Already-downloaded takes are skipped, so the script can be re-run.

Usage: python3 yue_queue.py [song_key ...]
"""
import json
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from songs import SONGS  # noqa: E402

RAW = Path(__file__).parent / 'raw'
RAW.mkdir(exist_ok=True)
API = '127.0.0.1:28190'


def ssh(cmd, data=None):
    return subprocess.run(['ssh', 'tang-server', cmd], input=data, capture_output=True, text=True, timeout=120)


def job_status(job_id):
    r = ssh(f'curl -s -m20 {API}/jobs/{job_id}')
    try:
        return json.loads(r.stdout)
    except json.JSONDecodeError:
        return {'status': 'unknown', 'raw': r.stdout[:200]}


def run_take(song, seed):
    job_id = f"gh-{song['key'].replace('_', '-')}-s{seed}"
    out = RAW / f"{song['key']}_s{seed}.flac"
    if out.exists():
        print('skip', out.name, flush=True)
        return
    st = job_status(job_id)
    if st.get('status') not in ('queued', 'running', 'completed'):
        body = json.dumps(dict(id=job_id, title=song['title'], style=song['style'], lyrics=song['lyrics'],
                               instrumental=bool(song.get('instrumental')), seed=seed))
        r = ssh(f"curl -s -m30 -X POST {API}/jobs -H 'Content-Type: application/json' --data-binary @-", data=body)
        print('submitted', job_id, r.stdout[:120].replace('\n', ' '), flush=True)
    started = time.time()
    while True:
        st = job_status(job_id)
        status = st.get('status')
        if status == 'completed':
            break
        if status in ('failed', 'cancelled', 'error'):
            print('FAILED', job_id, st.get('message'), flush=True)
            return
        if time.time() - started > 1800:
            print('TIMEOUT', job_id, flush=True)
            return
        time.sleep(15)
    remote = f'/tmp/{job_id}.flac'
    ssh(f'curl -s -m120 {API}/jobs/{job_id}/audio -o {remote}')
    subprocess.run(['scp', '-q', f'tang-server:{remote}', str(out)], check=True)
    ssh(f'rm -f {remote}')
    print('done', out.name, f'{time.time() - started:.0f}s', flush=True)


def main():
    keys = set(sys.argv[1:])
    for song in SONGS:
        if keys and song['key'] not in keys:
            continue
        for seed in song['seeds']:
            run_take(song, seed)
    print('ALL DONE', flush=True)


if __name__ == '__main__':
    main()
