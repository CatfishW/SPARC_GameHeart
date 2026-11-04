#!/usr/bin/env python3
"""Offline Qwen3-TTS render worker; run in the existing benwulab voice environment."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import time

MODEL_DIR = "/data/benwulab/cap-voice/hf-cache/hub/models--Qwen--Qwen3-TTS-12Hz-1.7B-CustomVoice/snapshots/0c0e3051f131929182e2c023b9537f8b1c68adfe"
PROVIDER = "Qwen3-TTS-12Hz-1.7B-CustomVoice"
BASE_DIRECTION = (
    "Speak in natural American English as a friendly, confident male game guide. "
    "Use clear conversational phrasing and a comfortable, lively pace. "
    "Keep the same voice throughout. Speak only the supplied words."
)
MOODS = {
    "happy": "Sound warmly encouraging, with a relaxed smile.",
    "neutral": "Explain clearly and naturally, like helping a friend.",
    "excited": "Sound enthusiastic and encouraging, with bright energy; do not shout.",
    "worried": "Sound concerned but reassuring, with a little urgency.",
    "cold": "Sound a little chilly and surprised, then explain clearly.",
    "thinking": "Sound thoughtful and curious, with natural pauses.",
    "sleepy": "Sound calm and gentle, softly spoken but clearly audible; do not whisper.",
}
DEFAULT_SEED = 20261104
# Keep the approved retakes reproducible when the full batch is regenerated.
SELECTED_SEEDS = {
    "v_50e6a512b4": 20261121,
    "v_3ffeb699fe": 20261121,
    "v_284fb0c152": 20261121,
    "v_b0571f184f": 20261155,
}
LINE_DIRECTIONS = {
    "v_b0571f184f": "Clearly articulate the final s in the plural word taps, with a very short pause before steady.",
}
AUDITION_IDS = ["v_50e6a512b4", "v_cd273afe9a", "v_7b2fceb5c5"]


def atomic_json(path, value):
    temp = path.with_suffix(path.suffix + ".tmp")
    temp.write_text(json.dumps(value, indent=2) + "\n")
    temp.replace(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--lines", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--speaker", choices=["Ryan", "Aiden"], default="Ryan")
    ap.add_argument("--gpu", type=int, default=1)
    ap.add_argument("--seed", type=int, default=None, help="Override the approved per-line seed selection")
    ap.add_argument("--ids", nargs="*")
    ap.add_argument("--audition", action="store_true")
    ap.add_argument("--model-dir", default=MODEL_DIR)
    args = ap.parse_args()
    lines = json.loads(Path(args.lines).read_text())
    if len({x["id"] for x in lines}) != len(lines):
        raise ValueError("Duplicate line ids")
    for line in lines:
        if not re.fullmatch(r"v_[a-f0-9]{10}", line["id"]) or not line["text"].strip():
            raise ValueError("Invalid line id or empty text")
        if line["mood"] not in MOODS:
            raise ValueError("Unknown mood: " + line["mood"])
    selected = set(AUDITION_IDS if args.audition else (args.ids or [x["id"] for x in lines]))
    if not selected <= {x["id"] for x in lines}:
        raise ValueError("Requested unknown voice id")
    lines = [x for x in lines if x["id"] in selected]
    speakers = ["Ryan", "Aiden"] if args.audition else [args.speaker]
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    os.environ["CUDA_VISIBLE_DEVICES"] = str(args.gpu)
    os.environ["HF_HOME"] = "/data/benwulab/cap-voice/hf-cache"
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TOKENIZERS_PARALLELISM"] = "false"

    import fcntl
    import numpy as np
    import soundfile as sf
    import torch
    from qwen_tts import Qwen3TTSModel

    lock = open(out / ".render.lock", "w")
    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    torch.set_num_threads(6)
    torch.cuda.set_per_process_memory_fraction(0.25)
    free, total = torch.cuda.mem_get_info()
    if free < 8 * 1024**3:
        raise RuntimeError("Less than 8 GiB GPU memory available; leave other jobs untouched")
    print(json.dumps({"event": "load", "provider": PROVIDER, "gpu": args.gpu,
                      "free_gib": round(free / 1024**3, 2), "lines": len(lines),
                      "speakers": speakers}), flush=True)
    model = Qwen3TTSModel.from_pretrained(
        args.model_dir, device_map="cuda:0", dtype=torch.bfloat16,
        attn_implementation="sdpa", local_files_only=True,
    )
    failures = []
    records = []
    for speaker in speakers:
        folder = out / speaker.lower() if args.audition else out
        folder.mkdir(parents=True, exist_ok=True)
        for index, line in enumerate(lines):
            voice_id = line["id"]
            instruction = BASE_DIRECTION + " " + MOODS[line["mood"]]
            if voice_id in LINE_DIRECTIONS:
                instruction += " " + LINE_DIRECTIONS[voice_id]
            base_seed = args.seed if args.seed is not None else (SELECTED_SEEDS.get(voice_id, DEFAULT_SEED) if speaker == "Ryan" else DEFAULT_SEED)
            seed = (base_seed + int(hashlib.sha256((speaker + voice_id).encode()).hexdigest()[:8], 16)) % (2**31)
            request = dict(provider=PROVIDER, model_snapshot=Path(args.model_dir).name,
                           id=voice_id, text=line["text"], mood=line["mood"],
                           speaker=speaker, language="English", instruct=instruction,
                           seed=seed, temperature=0.65, top_p=0.9, repetition_penalty=1.05,
                           max_new_tokens=512, non_streaming_mode=True, renderer_version=1)
            signature = hashlib.sha256(json.dumps(request, sort_keys=True).encode()).hexdigest()
            wav_path = folder / (voice_id + ".wav")
            meta_path = folder / (voice_id + ".json")
            if wav_path.exists() and meta_path.exists():
                old = json.loads(meta_path.read_text())
                if old.get("request_sha256") == signature and old.get("audio_sha256") == hashlib.sha256(wav_path.read_bytes()).hexdigest():
                    records.append(old)
                    print("CACHED", speaker, voice_id, flush=True)
                    continue
            start = time.monotonic()
            try:
                torch.manual_seed(seed)
                torch.cuda.manual_seed_all(seed)
                with torch.inference_mode():
                    waves, sample_rate = model.generate_custom_voice(
                        text=line["text"], language="English", speaker=speaker,
                        instruct=instruction, non_streaming_mode=True,
                        max_new_tokens=512, temperature=0.65, top_p=0.9,
                        repetition_penalty=1.05,
                    )
                wave = np.asarray(waves[0], dtype=np.float32).reshape(-1)
                if not len(wave) or not np.all(np.isfinite(wave)):
                    raise ValueError("Empty or nonfinite audio")
                duration = len(wave) / sample_rate
                peak = float(np.max(np.abs(wave)))
                rms = float(np.sqrt(np.mean(wave.astype(np.float64)**2)))
                if not 0.2 <= duration <= 35 or rms < 0.0002:
                    raise ValueError("Silent or implausible audio duration")
                if peak >= 1:
                    wave = wave * (0.98 / peak)
                temp = wav_path.with_suffix(".tmp.wav")
                sf.write(str(temp), wave, sample_rate, subtype="PCM_16")
                temp.replace(wav_path)
                record = dict(request, request_sha256=signature,
                              audio_sha256=hashlib.sha256(wav_path.read_bytes()).hexdigest(),
                              sample_rate=int(sample_rate), duration=round(duration, 4),
                              peak=peak, rms=rms, elapsed=round(time.monotonic() - start, 2))
                atomic_json(meta_path, record)
                records.append(record)
                print(json.dumps({"event": "rendered", "speaker": speaker, "id": voice_id,
                                  "index": index + 1, "total": len(lines),
                                  "duration": record["duration"], "seconds": record["elapsed"]}), flush=True)
            except Exception as exc:
                failures.append({"id": voice_id, "speaker": speaker, "error": str(exc)})
                print("FAILED", speaker, voice_id, type(exc).__name__, str(exc), flush=True)
    atomic_json(out / "render-report.json", {"provider": PROVIDER, "records": records, "failures": failures})
    if failures:
        raise SystemExit("Rendering failed for " + str(len(failures)) + " lines")
    print("COMPLETE", len(records), flush=True)


if __name__ == "__main__":
    main()
