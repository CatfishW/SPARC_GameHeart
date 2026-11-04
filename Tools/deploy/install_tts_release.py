#!/usr/bin/env python3
"""Verify and install the prepared Game Heart voice release. Run --check without sudo first."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shlex
import shutil
import sys
import tempfile
import urllib.request

LIVE = Path("/www/wwwroot/ping.agaii.org/game/gameheart")
PUBLIC = "https://ping.agaii.org/game/gameheart/"


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def validate_stage(release):
    site = release / "site"
    if not site.is_dir() or site.is_symlink():
        raise RuntimeError("Expected a real release site directory")
    manifest = json.loads((release / "manifest.json").read_text())
    meta = json.loads((release / "release.json").read_text())
    if digest(release / "manifest.json") != meta["manifest_sha256"]:
        raise RuntimeError("The release manifest changed")
    if "index.html" not in manifest or not any(k.endswith(".data") for k in manifest):
        raise RuntimeError("Incomplete Unity release")
    actual = set()
    for p in site.rglob("*"):
        if p.is_symlink():
            raise RuntimeError("Symlinks are not allowed in a release")
        if p.is_file():
            actual.add(p.relative_to(site).as_posix())
    if actual != set(manifest):
        raise RuntimeError("Release file coverage does not match manifest")
    for rel, expected in manifest.items():
        clean = PurePosixPath(rel)
        if clean.is_absolute() or ".." in clean.parts or "\\" in rel:
            raise RuntimeError("Unsafe manifest path")
        if digest(site / rel) != expected:
            raise RuntimeError("Corrupt release file: " + rel)
    if not LIVE.is_dir() or LIVE.is_symlink():
        raise RuntimeError("Expected existing Game Heart live directory")
    for rel in manifest:
        ensure_live_destination(LIVE / rel)
    current_index = digest(LIVE / "index.html")
    if current_index not in [meta["previous_index_sha256"], manifest["index.html"]]:
        raise RuntimeError("The live game changed after this release was prepared; stop and review")
    unversioned = {rel: sha for rel, sha in manifest.items()
                   if rel != "index.html" and not rel.startswith("Build/")}
    if unversioned != meta["unversioned_sha256"]:
        raise RuntimeError("Unversioned release asset inventory changed")
    for rel, expected in unversioned.items():
        if not (LIVE / rel).is_file() or digest(LIVE / rel) != expected:
            raise RuntimeError("This voice release must preserve existing shared assets: " + rel)
    for rel, expected in meta.get("preserved_live_files", {}).items():
        clean = PurePosixPath(rel)
        if clean.is_absolute() or ".." in clean.parts or "\\" in rel:
            raise RuntimeError("Unsafe preserved asset path")
        ensure_live_destination(LIVE / rel)
        if not (LIVE / rel).is_file() or digest(LIVE / rel) != expected:
            raise RuntimeError("A preserved live asset changed: " + rel)
    return site, manifest, meta


def ensure_live_destination(dest):
    """Refuse writes through symlinks beneath the fixed live game directory."""
    relative = dest.relative_to(LIVE)
    if not relative.parts or ".." in relative.parts:
        raise RuntimeError("Destination is outside the live game file scope")
    for path in (dest, *dest.parents):
        if path.is_symlink():
            raise RuntimeError("Live destination contains a symlink: " + str(path))
        if path == LIVE:
            break


def atomic_copy(source, dest, uid, gid):
    ensure_live_destination(dest)
    if source.is_symlink() or not source.is_file():
        raise RuntimeError("Expected a regular source file: " + str(source))
    dest.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(prefix=".gh-voice-", dir=str(dest.parent))
    os.close(fd)
    temp = Path(temp_name)
    try:
        shutil.copyfile(source, temp)
        os.chmod(temp, 0o644)
        os.chown(temp, uid, gid)
        os.replace(temp, dest)
    finally:
        if temp.exists():
            temp.unlink()


def verify_public(manifest):
    for rel, expected in manifest.items():
        request = urllib.request.Request(
            PUBLIC + urllib.request.quote(rel),
            headers={"Cache-Control": "no-cache", "User-Agent": "GameHeart-Voice-Release-Verify"},
        )
        h = hashlib.sha256()
        with urllib.request.urlopen(request, timeout=90) as response:
            for chunk in iter(lambda: response.read(1024 * 1024), b""):
                h.update(chunk)
        if h.hexdigest() != expected:
            raise RuntimeError("Live HTTPS verification failed: " + rel)
    print("HTTPS verification passed:", len(manifest), "files", flush=True)


def validate_backup(backup, meta):
    if not backup.is_dir() or backup.is_symlink():
        raise RuntimeError("Expected a real previous-live backup directory")
    files = {}
    for path in sorted(backup.rglob("*")):
        if path.is_symlink():
            raise RuntimeError("Symlinks are not allowed in the previous-live backup")
        if path.is_file():
            files[path.relative_to(backup).as_posix()] = digest(path)
        elif not path.is_dir():
            raise RuntimeError("Unsupported file in previous-live backup: " + str(path))
    if files.get("index.html") != meta["previous_index_sha256"]:
        raise RuntimeError("Backup index does not match the prepared previous live build")
    return files


def restore_previous(release, meta):
    backup = release / "previous-live"
    files = validate_backup(backup, meta)
    # Validate every destination before restoring anything. Retain newer hashed files.
    for rel in files:
        ensure_live_destination(LIVE / rel)
    owner = LIVE.stat()
    for rel, expected in files.items():
        if rel == "index.html":
            continue
        dest = LIVE / rel
        if dest.is_file() and digest(dest) == expected:
            continue
        atomic_copy(backup / rel, dest, owner.st_uid, owner.st_gid)
    for rel, expected in files.items():
        if rel != "index.html" and digest(LIVE / rel) != expected:
            raise RuntimeError("Rollback payload verification failed: " + rel)
    atomic_copy(backup / "index.html", LIVE / "index.html", owner.st_uid, owner.st_gid)
    if digest(LIVE / "index.html") != meta["previous_index_sha256"]:
        raise RuntimeError("Restored index verification failed")
    return backup


def publication_state(manifest, meta):
    try:
        ensure_live_destination(LIVE / "index.html")
        current = digest(LIVE / "index.html")
    except Exception:
        return "live index cannot be verified; payload state is unverified"
    if current == meta["previous_index_sha256"]:
        return "previous index is active; payload state is unverified"
    if current == manifest["index.html"]:
        return "new release index is active; payload state is unverified"
    return "live index differs from both prepared builds; manual review is required"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--rollback", action="store_true",
                      help="Restore the verified previous-live backup; requires root")
    args = ap.parse_args()
    release = Path(__file__).resolve().parent
    site, manifest, meta = validate_stage(release)
    rollback_command = shlex.join(["sudo", "python3", str(Path(__file__).resolve()), "--rollback"])
    print("Release verified:", meta["release_id"], len(manifest), "files", flush=True)
    if args.check:
        print("Ready to install into", LIVE, flush=True)
        return
    if os.geteuid() != 0:
        raise SystemExit("Run this prepared installer with sudo; no password is stored in the release.")
    if args.rollback:
        backup = restore_previous(release, meta)
        print("ROLLBACK COMPLETE. Publication state: previous build restored; filesystem verified.", flush=True)
        print("Backup preserved:", backup, flush=True)
        print("Newer hashed build files were retained. HTTPS was not reverified.", flush=True)
        return
    backup = release / "previous-live"
    if digest(LIVE / "index.html") == manifest["index.html"]:
        print("This exact release is already installed; verifying it again.", flush=True)
    else:
        if backup.exists() or backup.is_symlink():
            raise RuntimeError("Backup already exists; preserve it and review before retrying")
        shutil.copytree(LIVE, backup, symlinks=True)
        validate_backup(backup, meta)
        print("Previous live game preserved at", backup, flush=True)
        owner = LIVE.stat()
        # Keep previous hashed build files for already-open clients. Publish the entry page last.
        for rel, expected in manifest.items():
            if rel == "index.html":
                continue
            dest = LIVE / rel
            if dest.is_file() and not dest.is_symlink() and digest(dest) == expected:
                continue
            atomic_copy(site / rel, dest, owner.st_uid, owner.st_gid)
        for rel, expected in manifest.items():
            if rel != "index.html" and digest(LIVE / rel) != expected:
                raise RuntimeError("Payload verification failed before publication: " + rel)
        atomic_copy(site / "index.html", LIVE / "index.html", owner.st_uid, owner.st_gid)
    try:
        for rel, expected in manifest.items():
            if digest(LIVE / rel) != expected:
                raise RuntimeError("Installed file hash mismatch: " + rel)
        print("Installed all 55 Qwen3-TTS voice lines:", PUBLIC, flush=True)
        verify_public(manifest)
    except Exception as exc:
        print("Published release verification failed:", str(exc), file=sys.stderr, flush=True)
        print("Attempting automatic rollback from", backup, file=sys.stderr, flush=True)
        try:
            restore_previous(release, meta)
        except Exception as rollback_error:
            print("AUTOMATIC ROLLBACK FAILED:", str(rollback_error), file=sys.stderr, flush=True)
            print("Publication state:", publication_state(manifest, meta), file=sys.stderr, flush=True)
            print("Rollback command after resolving the reported problem:",
                  rollback_command, file=sys.stderr, flush=True)
            raise SystemExit(2) from rollback_error
        print("Publication state: previous build restored; all backup files and index verified.",
              file=sys.stderr, flush=True)
        print("HTTPS was not reverified after rollback. Backup and new hashed files were retained.",
              file=sys.stderr, flush=True)
        raise SystemExit(1) from exc
    print("COMPLETE. Publication state: new release installed and HTTPS verified.", flush=True)
    print("Previous build backup:", backup, flush=True)
    print("Rollback command:", rollback_command, flush=True)

if __name__ == "__main__":
    main()
