"""Task-isolated Qwen-Image 2.1 + Pruna 6-step batch for the Game Heart remaster art.

Runs from /tmp/gameheart-art on chongyou against the deployed weights, read-only. Touches neither the
WarmPixel Atelier service nor its queue or keys. Watches jobs.json and exits when STOP exists.
jobs.json: [{"id","prompt","width","height","seed","refs":[png...]}]
"""
import json, os, re, sys, time, traceback
from pathlib import Path

ROOT = Path('/tmp/gameheart-art')
DEPLOY = Path('/home/summer/apps/qwenimage-warmpix')
os.environ.update(HF_HOME=str(DEPLOY / 'cache'), HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1',
                  PYTORCH_CUDA_ALLOC_CONF='expandable_segments:True', OMP_NUM_THREADS='8')
if os.environ.get('CUDA_VISIBLE_DEVICES') != '1':
    raise SystemExit('GPU index 1 only')
server = (DEPLOY / 'app/server.py').read_text()
weight = re.search(r"PRUNA_WEIGHT = '([^']+)'", server).group(1)
sigmas = eval(re.search(r'PRUNA_SIGMAS = (\[[^\]]+\])', server).group(1))

import torch
from PIL import Image
from diffusers import FlowMatchEulerDiscreteScheduler, QwenImage21Pipeline

status = {'done': {}, 'failed': {}}
free, _ = torch.cuda.mem_get_info()
print('free GiB', round(free / 2**30, 1), flush=True)
torch.set_num_threads(8)
torch.set_float32_matmul_precision('high')
pipe = QwenImage21Pipeline.from_pretrained((DEPLOY / 'model-path.txt').read_text().strip(), torch_dtype=torch.bfloat16, local_files_only=True)
config = dict(pipe.scheduler.config)
pipe.load_lora_weights(str(DEPLOY / 'adapters'), weight_name=weight, adapter_name='pruna', local_files_only=True)
pipe.scheduler = FlowMatchEulerDiscreteScheduler.from_config(config, use_dynamic_shifting=True, shift_terminal=None)
if os.environ.get('QWEN_OFFLOAD', 'sequential') == 'sequential':
    pipe.enable_sequential_cpu_offload(gpu_id=0)
else:
    pipe.enable_model_cpu_offload(gpu_id=0)
if hasattr(pipe.vae, 'enable_tiling'):
    pipe.vae.enable_tiling()
with torch.inference_mode():
    pipe.enable_lora()
    pipe.set_adapters('pruna', adapter_weights=1.0)
print('ready', flush=True)
out = ROOT / 'out'
out.mkdir(exist_ok=True)
while True:
    jobs = json.loads((ROOT / 'jobs.json').read_text())
    pending = [j for j in jobs if not (out / (j['id'] + '.png')).exists() and j['id'] not in status['failed']]
    if not pending:
        if (ROOT / 'STOP').exists():
            break
        time.sleep(3)
        continue
    job = pending[0]
    started = time.monotonic()
    try:
        kwargs = dict(prompt=job['prompt'], width=job.get('width', 1536), height=job.get('height', 1024), num_inference_steps=6,
                      generator=torch.Generator('cuda').manual_seed(int(job.get('seed', 1031))), true_cfg_scale=1.0, use_kv_cache=True, sigmas=sigmas)
        refs = []
        for name in job.get('refs', []):
            with Image.open(ROOT / name) as im:
                refs.append(im.convert('RGB').copy())
        if refs:
            kwargs['image'] = refs
        with torch.inference_mode():
            image = pipe(**kwargs).images[0]
        tmp = out / (job['id'] + '.tmp.png')
        image.save(tmp)
        tmp.replace(out / (job['id'] + '.png'))
        print('ok', job['id'], round(time.monotonic() - started, 1), flush=True)
    except Exception as exc:
        status['failed'][job['id']] = str(exc)[:300]
        print('FAIL', job['id'], str(exc)[:300], flush=True)
        traceback.print_exc()
        torch.cuda.empty_cache()
