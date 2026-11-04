#!/usr/bin/env python3
"""Master staged TTS takes and audit them; never import into Unity Assets.

Run with ~/Development/gura-wish-mv/.venv/bin/python and supply --raw-dir,
--out-dir and --provider. The raw directory must contain exactly one <id>.wav
for every entry in lines.json. ASR is on by default; --no-asr is explicitly
reported as unverified. Exit codes: 0 = passed, 2 = invalid audio/coverage,
3 = needs listening/transcript review. All generated files stay in --out-dir.
"""
from __future__ import annotations

import argparse
import ast
from datetime import datetime, timezone
import difflib
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import unicodedata

import numpy as np
import soundfile as sf
from scipy.signal import resample_poly

HERE = Path(__file__).resolve().parent
RATE = 44100
ENV_HZ = 30
TARGET_DBFS = -18.0
PEAK_CEILING = 0.9499  # Leaves PCM16 quantization headroom below 0.95.
ASR_MODEL = "mlx-community/whisper-large-v3-turbo"
INTERJECTIONS = {"brrr", "shh", "hmm", "mmm"}
CONTRACTIONS = {
    "let's": "let us", "i'm": "i am", "can't": "can not",
    "cannot": "can not", "won't": "will not", "shan't": "shall not",
    "you're": "you are", "we're": "we are", "they're": "they are",
    "i've": "i have", "you've": "you have", "we've": "we have",
    "they've": "they have", "i'll": "i will", "you'll": "you will",
    "we'll": "we will", "they'll": "they will", "it'll": "it will",
    "that's": "that is", "it's": "it is", "here's": "here is",
    "there's": "there is", "what's": "what is", "who's": "who is",
}


def sha256(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def write_json(path, value):
    path = Path(path)
    tmp = path.with_name(path.name + ".tmp")
    # Atomic replacement also avoids following an existing destination symlink.
    if tmp.is_symlink():
        raise ValueError(f"Refusing symlink temporary file: {tmp}")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2,
                              allow_nan=False) + "\n", encoding="utf-8")
    tmp.replace(path)


def normalized_words(text):
    text = unicodedata.normalize("NFKC", text).lower()
    text = text.translate(str.maketrans({"’": "'", "‘": "'", "ʼ": "'"}))
    words = re.findall(r"[a-z0-9]+(?:'[a-z0-9]+)?", text)
    result = []
    for i, word in enumerate(words):
        nxt = words[i + 1] if i + 1 < len(words) else ""
        if re.fullmatch(r"br{2,}", word):
            result.append("brrr")
        elif re.fullmatch(r"sh{2,}", word):
            result.append("shh")
        elif re.fullmatch(r"hm{2,}", word):
            result.append("hmm")
        elif word == "beatline":
            # A spelling boundary, not a pronunciation or script-word change.
            result.extend(["beat", "line"])
        elif word == "woah":
            result.append("whoa")
        elif word in CONTRACTIONS:
            # "It's had" is "it has had", not "it is had".
            if word.endswith("'s") and nxt in {"had", "been", "got", "done"}:
                result.extend([word[:-2], "has"])
            else:
                result.extend(CONTRACTIONS[word].split())
        elif word.endswith("n't"):
            result.extend([word[:-3], "not"])
        elif word.endswith("'s"):
            stem = word[:-2]
            if nxt in {"had", "been", "got", "done"}:
                result.extend([stem, "has"])
            elif nxt.endswith("ing") or nxt in {
                    "still", "always", "not", "sick", "cold", "warm",
                    "ready", "fully", "powerful", "healthy", "strong"}:
                result.extend([stem, "is"])
            else:
                # Preserve possessive words while ignoring apostrophe typography.
                result.append(stem + "s")
        else:
            result.append(word)
    # Preserve the medical term; do not equate "essay" or "a node" with SA node.
    i = 0
    canonical = []
    while i < len(result):
        if result[i:i + 3] == ["s", "a", "node"]:
            canonical.extend(["sa", "node"])
            i += 3
        else:
            canonical.append(result[i])
            i += 1
    return canonical


def match_transcript(expected, actual):
    want, got = normalized_words(expected), normalized_words(actual)
    previous = list(range(len(got) + 1))
    for i, word in enumerate(want, 1):
        current = [i]
        for j, other in enumerate(got, 1):
            current.append(min(current[-1] + 1, previous[j] + 1,
                               previous[j - 1] + (word != other)))
        previous = current
    matcher = difflib.SequenceMatcher(None, want, got, autojunk=False)
    changes = [
        {"operation": op, "expected": want[a:b], "heard": got[c:d]}
        for op, a, b, c, d in matcher.get_opcodes() if op != "equal"
    ]
    return {
        "normalized_expected": want, "normalized_transcript": got,
        "exact_match": want == got,
        "word_error_rate": round(previous[-1] / max(1, len(want)), 5),
        "sequence_match": round(matcher.ratio(), 5),
        "interjection_only": want != got and
            [w for w in want if w not in INTERJECTIONS] ==
            [w for w in got if w not in INTERJECTIONS],
        "changes": changes,
    }


def rms(y):
    return float(np.sqrt(np.mean(np.square(y, dtype=np.float64))))


def dbfs(value):
    return round(20 * math.log10(max(value, 1e-12)), 4)


def frame_rms(y, hop):
    return np.array([rms(y[i:i + hop]) for i in range(0, len(y), hop)])


def speech_rms(y, rate):
    hop = max(1, round(rate * 0.02))
    energy = frame_rms(y, hop)
    threshold = max(float(np.percentile(energy, 95)) * 10 ** (-35 / 20),
                    0.00002)
    active = energy >= threshold
    counts = np.minimum(hop, len(y) - np.arange(len(energy)) * hop)
    if not active.any():
        raise ValueError("No active speech energy")
    return float(np.sqrt(np.average(energy[active] ** 2,
                                    weights=counts[active])))


def flag(row, code, severity, detail):
    row["flags"].append({"code": code, "severity": severity, "detail": detail})


def read_and_master(path, row):
    info = sf.info(str(path))
    if not 8000 <= info.samplerate <= 192000:
        raise ValueError(f"Unsupported sample rate: {info.samplerate}")
    if not 1 <= info.channels <= 8 or info.frames <= 0:
        raise ValueError("Empty audio or unsupported channel count")
    if info.duration > 180:
        raise ValueError("A dialogue take exceeds 180 seconds")
    decoded, rate = sf.read(str(path), dtype="float64", always_2d=True)
    if not np.isfinite(decoded).all():
        raise ValueError("Decoded audio contains NaN or infinity")
    peak = float(np.max(np.abs(decoded)))
    clipped_fraction = float(np.mean(np.abs(decoded) >= 0.999))
    # Catch sustained full-scale plateaus as well as widespread clipping.
    longest = 0
    for channel in decoded.T:
        saturated = np.abs(channel) >= 0.999
        edges = np.diff(np.r_[False, saturated, False].astype(np.int8))
        lengths = np.flatnonzero(edges == -1) - np.flatnonzero(edges == 1)
        longest = max(longest, int(lengths.max()) if lengths.size else 0)
    row["input"] = {
        "sample_rate": rate, "channels": info.channels,
        "duration_seconds": round(len(decoded) / rate, 6),
        "rms_dbfs": dbfs(rms(decoded)), "peak": round(peak, 8),
        "clipped_fraction": round(clipped_fraction, 8),
        "longest_fullscale_run_ms": round(1000 * longest / rate, 4),
    }
    if peak > 1.0001 or clipped_fraction > 0.001 or longest / rate >= 0.003:
        raise ValueError("Gross clipping or out-of-range source samples")
    if clipped_fraction:
        flag(row, "source_near_fullscale", "warning",
             "Isolated near-full-scale samples; no gross clipping detected")
    y = decoded.mean(axis=1)
    dc = float(np.mean(y))
    y = y - dc
    row["input"]["dc_offset"] = round(dc, 8)
    if abs(dc) > 0.02:
        flag(row, "dc_offset", "review", "Source DC offset exceeds 0.02")
    if float(np.max(np.abs(y))) < 0.0001 or rms(y) < 0.00002:
        raise ValueError("Silent or effectively silent input after DC removal")
    if rate != RATE:
        divisor = math.gcd(rate, RATE)
        y = resample_poly(y, RATE // divisor, rate // divisor)
    hop = RATE // 100
    energy = frame_rms(y, hop)
    threshold = max(float(energy.max()) * 10 ** (-45 / 20), 0.00002)
    active = np.flatnonzero(energy >= threshold)
    if not active.size or active.size * hop / RATE < 0.08:
        raise ValueError("Insufficient sustained speech energy")
    first, last = int(active[0] * hop), min(len(y), int((active[-1] + 1) * hop))
    # Keep quiet consonants/breaths; no internal gating, filtering or compression.
    start, end = first - round(0.12 * RATE), last + round(0.18 * RATE)
    cleaned = np.pad(y[max(0, start):min(len(y), end)],
                     (max(0, -start), max(0, end - len(y))))
    fade = min(round(0.003 * RATE), len(cleaned) // 2)
    cleaned[:fade] *= np.linspace(0, 1, fade)
    cleaned[-fade:] *= np.linspace(1, 0, fade)
    before = speech_rms(cleaned, RATE)
    requested_gain = 10 ** (TARGET_DBFS / 20) / before
    gain = min(requested_gain, PEAK_CEILING / float(np.max(np.abs(cleaned))))
    cleaned *= gain
    if not np.isfinite(cleaned).all():
        raise ValueError("Non-finite samples after processing")
    if gain < requested_gain * 0.98:
        flag(row, "peak_limited_gain", "warning",
             "Linear gain reduced to retain peaks without compression")
    duration = len(cleaned) / RATE
    spoken_duration = max((last - first) / RATE, 0.01)
    count = len(normalized_words(row["text"]))
    wps = count / spoken_duration
    row["processing"] = {
        "gain_db": dbfs(gain), "target_speech_rms_dbfs": TARGET_DBFS,
        "original_start_seconds": round(start / RATE, 6),
        "original_end_seconds": round(end / RATE, 6),
        "boundary_padding_seconds": [0.12, 0.18],
        "speech_span_seconds": round(spoken_duration, 6),
        "words_per_second": round(wps, 4),
    }
    low, high = (0.7, 4.8) if count >= 5 else (0.35, 5.8)
    if not low <= wps <= high or duration > max(12, count / 0.7 + 3):
        flag(row, "unusual_delivery_duration", "review",
             f"{count} normalized words in {spoken_duration:.2f}s ({wps:.2f}/s)")
    return cleaned


def transcribe(path, audio_sha, cache_dir, model, engine):
    model_key = hashlib.sha256(("v1|en|" + model).encode()).hexdigest()[:16]
    cache_path = cache_dir / f"{audio_sha}.{model_key}.json"
    if cache_path.exists():
        value = json.loads(cache_path.read_text(encoding="utf-8"))
        if (value.get("audio_sha256") == audio_sha and
                value.get("model") == model and
                isinstance(value.get("text"), str)):
            return value, True
        raise ValueError("ASR cache does not match audio content/model")
    result = engine.transcribe(str(path), path_or_hf_repo=model, language="en",
                               temperature=0.0, condition_on_previous_text=False)
    if not isinstance(result.get("text"), str):
        raise ValueError("ASR returned no transcript field")
    # Never prompt ASR with the expected script: that can conceal omissions.
    segments = [
        {k: s[k] for k in ("start", "end", "avg_logprob",
                           "no_speech_prob", "compression_ratio") if k in s}
        for s in result.get("segments", [])
    ]
    value = {"audio_sha256": audio_sha, "model": model, "language": "en",
             "text": result["text"].strip(), "segments": segments}
    write_json(cache_path, value)
    return value, False


def load_lines(path):
    lines = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(lines, list) or not lines:
        raise ValueError("lines.json must be a nonempty list")
    seen = set()
    for line in lines:
        if not isinstance(line, dict) or not re.fullmatch(
                r"v_[0-9a-f]{10}", str(line.get("id", ""))):
            raise ValueError("Invalid voice id in lines.json")
        if line["id"] in seen:
            raise ValueError(f"Duplicate voice id: {line['id']}")
        seen.add(line["id"])
        if not isinstance(line.get("text"), str) or not normalized_words(line["text"]):
            raise ValueError(f"Empty script text: {line['id']}")
    return lines


def validate_provenance(raw, lines, provider):
    """Fail closed on stale takes, mixed casting, or mismatched render requests."""
    renderer = HERE / "render_qwen3.py"
    constants = {}
    for node in ast.parse(renderer.read_text(encoding="utf-8")).body:
        if isinstance(node, ast.Assign):
            for name in node.targets:
                if isinstance(name, ast.Name) and name.id in {
                        "PROVIDER", "BASE_DIRECTION", "MOODS", "LINE_DIRECTIONS"}:
                    constants[name.id] = ast.literal_eval(node.value)
    for name in ("PROVIDER", "BASE_DIRECTION", "MOODS", "LINE_DIRECTIONS"):
        if name not in constants:
            raise ValueError(f"Renderer has no literal {name} contract")
    fixed_keys = ("provider", "model_snapshot", "speaker", "language",
                  "temperature", "top_p", "repetition_penalty", "max_new_tokens",
                  "non_streaming_mode", "renderer_version")
    request_keys = fixed_keys + ("id", "text", "mood", "seed", "instruct")
    records, errors, common = {}, [], None
    for line in lines:
        try:
            sidecar = raw / (line["id"] + ".json")
            meta = json.loads(sidecar.read_text(encoding="utf-8"))
            if not isinstance(meta, dict) or any(k not in meta for k in request_keys):
                raise ValueError("Missing required rendering metadata")
            if meta["provider"] != provider or provider != constants["PROVIDER"]:
                raise ValueError("Provider does not match CLI and renderer")
            for key in ("id", "text", "mood"):
                if meta[key] != line.get(key):
                    raise ValueError(f"Sidecar {key} differs from current script")
            instruction = constants["BASE_DIRECTION"] + " " + constants["MOODS"][line["mood"]]
            if line["id"] in constants["LINE_DIRECTIONS"]:
                instruction += " " + constants["LINE_DIRECTIONS"][line["id"]]
            if meta["instruct"] != instruction:
                raise ValueError("Instruction differs from current base direction/mood")
            if (not isinstance(meta["speaker"], str) or not meta["speaker"] or
                    meta["language"] != "English" or not re.fullmatch(
                        r"[a-f0-9]{40}", str(meta["model_snapshot"]))):
                raise ValueError("Invalid voice, language or model snapshot")
            for key in ("temperature", "top_p", "repetition_penalty"):
                value = meta[key]
                if (isinstance(value, bool) or not isinstance(value, (int, float))
                        or not math.isfinite(value) or value <= 0):
                    raise ValueError(f"Invalid numeric generation parameter: {key}")
            if meta["top_p"] > 1:
                raise ValueError("top_p exceeds 1")
            for key in ("max_new_tokens", "renderer_version", "seed"):
                if type(meta[key]) is not int or meta[key] < (0 if key == "seed" else 1):
                    raise ValueError(f"Invalid integer generation parameter: {key}")
            if type(meta["non_streaming_mode"]) is not bool:
                raise ValueError("non_streaming_mode is not boolean")
            request = {k: meta[k] for k in request_keys}
            signature = hashlib.sha256(json.dumps(request, sort_keys=True).encode()).hexdigest()
            if meta.get("request_sha256") != signature:
                raise ValueError("Request signature does not match sidecar parameters")
            audio_sha = sha256(raw / (line["id"] + ".wav"))
            if meta.get("audio_sha256") != audio_sha:
                raise ValueError("Audio SHA256 does not match sidecar")
            fixed = {k: meta[k] for k in fixed_keys}
            if common is None:
                common = fixed
            elif fixed != common:
                differences = [k for k in fixed_keys if fixed[k] != common[k]]
                raise ValueError("Mixed batch generation settings: " + ", ".join(differences))
            records[line["id"]] = dict(meta, sidecar_sha256=sha256(sidecar))
        except Exception as exc:
            errors.append({"id": line["id"], "error": str(exc)})
    return records, {
        "common": common, "renderer_sha256": sha256(renderer), "errors": errors,
        "base_direction": constants["BASE_DIRECTION"],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--provider", required=True,
                        help="Exact provider/model name recorded in the raw sidecars")
    parser.add_argument("--lines", type=Path, default=HERE / "lines.json")
    parser.add_argument("--no-asr", action="store_true",
                        help="Skip transcription; result remains unverified (exit 3)")
    parser.add_argument("--asr-model", default=ASR_MODEL)
    parser.add_argument("--secondary-asr", type=Path,
                        help="Optional independent, unprompted ASR evidence tied to raw audio SHA256")
    args = parser.parse_args(argv)
    raw, out, manifest = args.raw_dir.resolve(), args.out_dir.resolve(), args.lines.resolve()
    assets = (HERE.parents[1] / "Assets").resolve()
    if (out == raw or out.is_relative_to(raw) or raw.is_relative_to(out) or
            out == assets or out.is_relative_to(assets) or
            out == manifest or out in manifest.parents):
        parser.error("Output must be separate from raw inputs, script files and Assets")
    if not raw.is_dir() or not args.provider.strip():
        parser.error("A raw directory and nonempty provider metadata are required")
    lines = load_lines(manifest)
    secondary = {}
    if args.secondary_asr:
        evidence = json.loads(args.secondary_asr.read_text(encoding="utf-8"))
        records = evidence if isinstance(evidence, list) else [evidence]
        for record in records:
            if (not isinstance(record, dict) or not isinstance(record.get("id"), str)
                    or not isinstance(record.get("text"), str)
                    or not record.get("asr_model") or record["asr_model"] == args.asr_model
                    or record.get("initial_prompt") is not None
                    or record.get("hotwords") is not None
                    or not re.fullmatch(r"[a-f0-9]{64}", str(record.get("audio_sha256", "")))):
                parser.error("Secondary ASR must be independent, unprompted and content-hash bound")
            if record["id"] in secondary:
                parser.error("Duplicate secondary ASR evidence")
            secondary[record["id"]] = record
    out.mkdir(parents=True, exist_ok=True)
    cache_dir = out / ".asr-cache"
    if not cache_dir.resolve().is_relative_to(out):
        parser.error("ASR cache may not point outside the output directory")
    cache_dir.mkdir(exist_ok=True)
    report_path = out / "qa_report.json"
    voice_path = out / "voice.json"
    report = {
        "schema": 1, "generated_utc": datetime.now(timezone.utc).isoformat(),
        "provider": args.provider, "script_sha256": sha256(manifest),
        "raw_directory": str(raw), "output_directory": str(out),
        "sample_rate": RATE, "channels": 1, "subtype": "PCM_16",
        "target_speech_rms_dbfs": TARGET_DBFS, "peak_ceiling": 0.95,
        "envelope_hz": ENV_HZ, "asr_model": None if args.no_asr else args.asr_model,
        "summary": {"status": "in_progress", "expected": len(lines)}, "lines": [],
    }
    expected = {line["id"] + ".wav" for line in lines}
    found = {str(p.relative_to(raw)) for p in raw.rglob("*")
             if p.is_file() and p.suffix.lower() == ".wav"}
    missing, extra = sorted(expected - found), sorted(found - expected)
    symlinks = sorted(str(p.relative_to(raw)) for p in raw.rglob("*") if p.is_symlink())
    missing_sidecars = sorted(line["id"] + ".json" for line in lines
                              if not (raw / (line["id"] + ".json")).is_file())
    report["coverage"] = {"missing": missing, "unexpected_wav": extra,
                          "symlinks": symlinks, "missing_sidecars": missing_sidecars}
    # An old successful manifest must never survive a failed rerun as if current.
    if voice_path.exists() or voice_path.is_symlink():
        voice_path.unlink()
    if missing or extra or symlinks or missing_sidecars:
        report["summary"].update(status="failed", reason="Exact raw coverage failed")
        write_json(report_path, report)
        print(json.dumps(report["summary"]), file=sys.stderr)
        return 2
    provenance, provenance_report = validate_provenance(raw, lines, args.provider)
    report["provenance"] = provenance_report
    if provenance_report["errors"]:
        report["summary"].update(status="failed", reason="Raw provenance failed")
        write_json(report_path, report)
        print(json.dumps(report["summary"]), file=sys.stderr)
        return 2
    engine = None
    if not args.no_asr:
        try:
            import mlx_whisper
            engine = mlx_whisper
        except ImportError as exc:
            report["summary"].update(status="failed", reason="ASR unavailable: " + str(exc))
            write_json(report_path, report)
            return 2
    table = []
    write_json(report_path, report)
    for line in lines:
        row = {k: line[k] for k in ("id", "text", "speaker", "mood") if k in line}
        row.update(provider=args.provider, flags=[])
        try:
            source = raw / (line["id"] + ".wav")
            row["generation"] = provenance[line["id"]]
            row["input_sha256"] = sha256(source)
            if row["input_sha256"] != row["generation"]["audio_sha256"]:
                raise ValueError("Raw audio changed after provenance validation")
            y = read_and_master(source, row)
            if sha256(source) != row["input_sha256"]:
                raise ValueError("Raw audio changed while being decoded")
            target = out / source.name
            tmp = target.with_suffix(".wav.tmp")
            if tmp.is_symlink():
                raise ValueError("Output temporary WAV is a symlink")
            sf.write(str(tmp), y, RATE, subtype="PCM_16", format="WAV")
            rendered, sr = sf.read(str(tmp), dtype="float64")
            if (sr != RATE or rendered.ndim != 1 or not np.isfinite(rendered).all()
                    or len(rendered) != len(y) or np.max(np.abs(rendered)) > 0.95):
                raise ValueError("Mastered WAV failed round-trip verification")
            tmp.replace(target)
            row["output_sha256"] = sha256(target)
            row["output"] = {
                "file": target.name, "sample_rate": sr, "channels": 1, "subtype": "PCM_16",
                "duration_seconds": round(len(rendered) / sr, 6),
                "rms_dbfs": dbfs(rms(rendered)),
                "speech_rms_dbfs": dbfs(speech_rms(rendered, sr)),
                "peak": round(float(np.max(np.abs(rendered))), 8),
            }
            envelope = frame_rms(rendered, RATE // ENV_HZ)
            scale = max(float(np.percentile(envelope, 95)), 1e-9)
            envelope = np.clip(envelope / scale, 0, 1)
            table.append({"id": line["id"], "duration": round(len(rendered) / sr, 6),
                          "env": [round(float(v), 4) for v in envelope]})
            if args.no_asr:
                flag(row, "asr_skipped", "review", "Speech content has not been verified")
            else:
                try:
                    asr, cached = transcribe(target, row["output_sha256"], cache_dir,
                                             args.asr_model, engine)
                    comparison = match_transcript(line["text"], asr["text"])
                    row["asr"] = dict(asr, cache_hit=cached, **comparison)
                    row["transcript"] = asr["text"]
                    if comparison["interjection_only"]:
                        flag(row, "asr_interjection_variance", "warning",
                             "Only a natural interjection differs; listen during audition")
                    elif not comparison["exact_match"]:
                        other = secondary.get(line["id"])
                        confirmed = (other is not None and other["audio_sha256"] == row["input_sha256"]
                                     and match_transcript(line["text"], other["text"])["exact_match"])
                        if confirmed:
                            row["secondary_asr"] = dict(other, **match_transcript(line["text"], other["text"]))
                            flag(row, "asr_decoder_disagreement", "warning",
                                 "Independent unprompted ASR matches the script; primary transcript retained for review")
                        else:
                            flag(row, "asr_text_mismatch", "review",
                                 "Normalized script words differ; inspect changes and listen")
                    if any(s.get("no_speech_prob", 0) > 0.7 for s in asr["segments"]):
                        flag(row, "asr_low_speech_confidence", "review",
                             "Whisper reported high no-speech probability")
                except Exception as exc:
                    flag(row, "asr_error", "review", str(exc))
            row["status"] = "needs_review" if any(
                f["severity"] == "review" for f in row["flags"]) else "passed"
        except Exception as exc:
            row["status"] = "failed"
            flag(row, "invalid_audio", "error", str(exc))
        report["lines"].append(row)
        write_json(report_path, report)
        print(f"{row['id']} {row['status']} " +
              ",".join(f["code"] for f in row["flags"]), flush=True)
    failed = [r["id"] for r in report["lines"] if r["status"] == "failed"]
    review = [r["id"] for r in report["lines"] if r["status"] == "needs_review"]
    warned = [r["id"] for r in report["lines"] if any(
        f["severity"] == "warning" for f in r["flags"])]
    status = "failed" if failed else "needs_review" if review else "passed"
    report["summary"] = {
        "status": status, "expected": len(lines), "mastered": len(table),
        "failed_ids": failed, "review_ids": review, "warning_ids": warned,
        "asr_verified": not args.no_asr and not failed and not review,
        "total_duration_seconds": round(sum(v["duration"] for v in table), 3),
        "voice_manifest_written": not failed,
    }
    if not failed:
        write_json(voice_path, {"lines": table})
        report["voice_manifest_sha256"] = sha256(voice_path)
    write_json(report_path, report)
    print(json.dumps(report["summary"], ensure_ascii=False), flush=True)
    return 2 if failed else 3 if review else 0


if __name__ == "__main__":
    sys.exit(main())
