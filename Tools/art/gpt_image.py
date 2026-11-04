#!/usr/bin/env python3
"""gpt-image-2.5 client (subtoken.shop, OpenAI-compatible) for Game Heart art.

The API key lives only in ~/.config/gameheart/subtoken.env (never in the repo or build).

Usage:
  gpt_image.py gen  OUT.png "prompt" [--size 1536x1024] [--transparent] [--quality high]
  gpt_image.py edit OUT.png "prompt" REF.png [REF2.png ...] [--size ...] [--transparent]
  gpt_image.py batch jobs.json OUTDIR [--workers 3]    # [{"id","prompt","size","transparent","refs":[...]}]
"""
import base64
import json
import mimetypes
import sys
import time
import urllib.error
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ENV = Path.home() / '.config/gameheart/subtoken.env'


def creds():
    vals = dict(l.split('=', 1) for l in ENV.read_text().split('\n') if '=' in l)
    return vals['SUBTOKEN_API_KEY'].strip(), vals.get('SUBTOKEN_BASE', 'https://subtoken.shop/v1').strip()


def _save(resp, out):
    d = resp['data'][0]
    if d.get('b64_json'):
        Path(out).write_bytes(base64.b64decode(d['b64_json']))
    else:
        urllib.request.urlretrieve(d['url'], out)


def _post(url, data, headers, timeout=420):
    req = urllib.request.Request(url, data=data, headers=headers)
    return json.loads(urllib.request.urlopen(req, timeout=timeout).read())


def gen(out, prompt, size='1536x1024', transparent=False, quality='high', model='gpt-image-2.5'):
    key, base = creds()
    body = dict(model=model, prompt=prompt, size=size, quality=quality, n=1)
    if transparent:
        body['background'] = 'transparent'
        body['output_format'] = 'png'
    r = _post(base + '/images/generations', json.dumps(body).encode(),
              {'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
    _save(r, out)


def edit(out, prompt, refs, size='1024x1536', transparent=False, quality='high', model='gpt-image-2.5'):
    key, base = creds()
    boundary = uuid.uuid4().hex
    parts = []

    def field(name, value):
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())

    field('model', model)
    field('prompt', prompt)
    field('size', size)
    field('quality', quality)
    if transparent:
        field('background', 'transparent')
        field('output_format', 'png')
    for ref in refs:
        mime = mimetypes.guess_type(ref)[0] or 'image/png'
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="image[]"; filename="{Path(ref).name}"\r\n'
                     f'Content-Type: {mime}\r\n\r\n'.encode() + Path(ref).read_bytes() + b'\r\n')
    parts.append(f'--{boundary}--\r\n'.encode())
    r = _post(base + '/images/edits', b''.join(parts),
              {'Authorization': 'Bearer ' + key, 'Content-Type': f'multipart/form-data; boundary={boundary}'})
    _save(r, out)


def run_job(job, outdir):
    out = Path(outdir) / f"{job['id']}.png"
    if out.exists():
        return f'skip {out.name}'
    for attempt in range(3):
        t = time.time()
        try:
            if job.get('refs'):
                edit(str(out), job['prompt'], job['refs'], job.get('size', '1024x1536'), job.get('transparent', False))
            else:
                gen(str(out), job['prompt'], job.get('size', '1536x1024'), job.get('transparent', False))
            return f'ok {out.name} {time.time() - t:.0f}s'
        except urllib.error.HTTPError as e:
            msg = e.read()[:300]
            err = f'HTTP {e.code} {msg!r}'
        except Exception as e:  # network hiccups
            err = repr(e)[:300]
        time.sleep(5 + attempt * 10)
    return f'FAIL {out.name} {err}'


def main():
    a = sys.argv[1:]
    opt = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    if a[0] == 'batch':
        jobs = json.loads(Path(a[1]).read_text())
        Path(a[2]).mkdir(parents=True, exist_ok=True)
        with ThreadPoolExecutor(int(opt('--workers', 3))) as ex:
            for msg in ex.map(lambda j: run_job(j, a[2]), jobs):
                print(msg, flush=True)
        return
    flags = {'--size', '--quality'}
    pos = [x for i, x in enumerate(a) if not x.startswith('--') and (i == 0 or a[i - 1] not in flags)]
    transparent = '--transparent' in a
    if a[0] == 'gen':
        gen(pos[1], pos[2], opt('--size', '1536x1024'), transparent, opt('--quality', 'high'))
    else:
        edit(pos[1], pos[2], pos[3:], opt('--size', '1024x1536'), transparent, opt('--quality', 'high'))
    print('saved', pos[1])


if __name__ == '__main__':
    main()
