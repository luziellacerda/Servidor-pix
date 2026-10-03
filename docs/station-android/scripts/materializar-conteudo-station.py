#!/usr/bin/env python3
"""Copy only indexed Station games/covers into a private, verifiable release tree.

The source mount may be inaccessible to the service account. This creates a
candidate tree outside Git; an operator must place it at the final service path
and validate ownership, traversal and the rewritten index before deployment.
"""

import argparse
from collections import Counter
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def copy_verified(source, target, expected):
    before = source.stat()
    target.parent.mkdir(mode=0o700, exist_ok=True)
    with source.open("rb") as original, target.open("xb") as output:
        os.fchmod(output.fileno(), 0o600)
        digest = hashlib.sha256()
        while block := original.read(1024 * 1024):
            output.write(block)
            digest.update(block)
        output.flush()
        os.fsync(output.fileno())
    after = source.stat()
    if (before.st_size, before.st_mtime_ns, before.st_ino) != \
            (after.st_size, after.st_mtime_ns, after.st_ino) or \
            digest.hexdigest() != expected:
        raise ValueError("source changed or digest mismatch while copying")
    return before.st_size


def cover_inspector():
    path = Path(__file__).with_name("cruzar-indice-catalogo.py")
    spec = importlib.util.spec_from_file_location("station_cover_check", path)
    module = importlib.util.module_from_spec(spec)
    old = sys.dont_write_bytecode
    try:
        sys.dont_write_bytecode = True
        spec.loader.exec_module(module)
    finally:
        sys.dont_write_bytecode = old
    return module.index_cover


def private_json(path, content):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as output:
        json.dump(content, output, ensure_ascii=False, separators=(",", ":"))
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    target = args.output_dir.resolve()
    if target.is_relative_to(Path(__file__).resolve().parents[3]):
        parser.error("private output must be outside the repository")
    if target == args.index.resolve() or target.is_relative_to(args.index.resolve().parent):
        parser.error("output must be separate from input index")
    source_index = json.loads(args.index.read_text(encoding="utf-8"))
    items = source_index.get("items")
    if not isinstance(items, list) or len(items) > 4096 or \
            type(source_index.get("revision")) is not int:
        parser.error("invalid Station index")
    target.mkdir(mode=0o700, parents=False, exist_ok=False)
    (target / "games").mkdir(mode=0o700)
    (target / "covers").mkdir(mode=0o700)
    inspect_cover = cover_inspector()
    copied = []
    checksums = []
    total_game = total_cover = 0
    platforms = Counter()
    for row in items:
        item_id, cover_id = row["itemId"], row["coverId"]
        if not isinstance(item_id, str) or not isinstance(cover_id, str) or \
                not item_id.isascii() or not cover_id.isascii() or \
                not all(c.isalnum() or c in "_-" for c in item_id + cover_id):
            raise ValueError("unsafe Station ID")
        game, cover = Path(row["filePath"]), Path(row["coverPath"])
        if not game.is_absolute() or not cover.is_absolute():
            raise ValueError("index contains relative path")
        artifact = row.get("artifact")
        if not isinstance(artifact, dict) or artifact.get("fileName") != game.name:
            raise ValueError("item artifact is absent or fileName differs")
        game_target = target / "games" / item_id / game.name
        total_game += copy_verified(game, game_target, artifact["sha256"])
        checksums.append((artifact["sha256"], game_target.relative_to(target).as_posix()))
        cover_info = inspect_cover(cover)
        if cover_info[0] != "ok":
            raise ValueError("indexed cover is unreadable or invalid")
        cover_target = target / "covers" / (cover_id + cover.suffix.lower())
        total_cover += copy_verified(cover, cover_target, cover_info[3])
        checksums.append((cover_info[3], cover_target.relative_to(target).as_posix()))
        copied.append(dict(row, filePath=str(game_target), coverPath=str(cover_target)))
        platforms[row["platform"]] += 1
    index_path = target / "index.json"
    private_json(index_path, {"revision": source_index["revision"], "items": copied})
    manifest_path = target / "files.sha256"
    fd = os.open(manifest_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as manifest:
        for digest, relative in sorted(checksums, key=lambda entry: entry[1]):
            manifest.write(digest + "  " + relative + "\n")
        manifest.flush()
        os.fsync(manifest.fileno())
    report = {"revision": source_index["revision"], "items": len(copied),
              "platformCounts": dict(sorted(platforms.items())),
              "gameBytes": total_game, "coverBytes": total_cover,
              "indexSha256": sha256(index_path),
              "filesManifestSha256": sha256(manifest_path)}
    private_json(target / "report.json", report)
    print(json.dumps(report, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
