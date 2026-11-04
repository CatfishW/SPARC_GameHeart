#!/usr/bin/env python3
"""Render Captain B with Qwen3-TTS on the existing benwulab GPU environment.

python3 Tools/voice/tts_batch.py --audition
python3 Tools/voice/tts_batch.py --speaker Ryan
Then run master_voice.py with the analysis venv; review its QA report before importing.
Generation never writes Unity assets. Audio8 originals remain in backups/audio8-20261104.
"""
import argparse
from pathlib import Path
import shlex
import subprocess

HERE = Path(__file__).resolve().parent
REMOTE_PYTHON = "/data/benwulab/cap-voice/.venv/bin/python"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--host", default="benwulab-remote")
    ap.add_argument("--remote-dir", default="/home/benwulab/gameheart-voice-20261104")
    ap.add_argument("--speaker", choices=["Ryan", "Aiden"], default="Ryan")
    ap.add_argument("--gpu", type=int, default=1)
    ap.add_argument("--seed", type=int, default=None, help="Override approved per-line seeds for a new take")
    ap.add_argument("--audition", action="store_true")
    ap.add_argument("--ids", nargs="*")
    args = ap.parse_args()
    if not args.remote_dir.startswith("/") or any(x in args.remote_dir for x in ["\n", "\r"]):
        ap.error("--remote-dir must be an absolute path")
    remote = args.remote_dir.rstrip("/")
    branch = "auditions" if args.audition else "raw"
    local = HERE / "qwen3" / branch
    local.mkdir(parents=True, exist_ok=True)
    ssh = ["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=20", args.host]
    mkdir = "from pathlib import Path; Path(" + repr(remote) + ").mkdir(parents=True,exist_ok=True)"
    subprocess.run(ssh + ["python3 -c " + shlex.quote(mkdir)], check=True)
    subprocess.run(["scp", "-q", str(HERE / "render_qwen3.py"), str(HERE / "lines.json"),
                    args.host + ":" + remote + "/"], check=True)
    command = [REMOTE_PYTHON, remote + "/render_qwen3.py", "--lines", remote + "/lines.json",
               "--out", remote + "/" + branch, "--speaker", args.speaker,
               "--gpu", str(args.gpu)]
    if args.seed is not None:
        command += ["--seed", str(args.seed)]
    if args.audition:
        command.append("--audition")
    if args.ids:
        command += ["--ids"] + args.ids
    subprocess.run(ssh + [" ".join(shlex.quote(x) for x in command)], check=True)
    subprocess.run(["rsync", "-a", "--exclude=.render.lock",
                    args.host + ":" + remote + "/" + branch + "/", str(local) + "/"], check=True)
    print("Fetched Qwen3 audio:", local, flush=True)


if __name__ == "__main__":
    main()
