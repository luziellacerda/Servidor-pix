#!/usr/bin/env python3
"""Prepare Station artifact metadata from local files; keep the index private."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

MAX_FILES = 100_000
MAX_ARTIFACT = 1 << 40
MAX_EXPANDED = 4 << 40


def safe_member(name):
    if (not name or len(name) > 512 or name.startswith("/") or
            "\\" in name or ":" in name or any(ord(c) < 32 for c in name) or
            any(part in ("", ".", "..") for part in name.split("/"))):
        raise ValueError("unsafe archive member")


def archive_members(path, kind):
    if kind == "zip":
        with zipfile.ZipFile(path) as archive:
            result = []
            for entry in archive.infolist():
                name = entry.filename[:-1] if entry.is_dir() else entry.filename
                safe_member(name)
                mode = (entry.external_attr >> 16) & 0o170000
                if mode == 0o120000:
                    raise ValueError("archive contains a link")
                if not entry.is_dir():
                    result.append((name, entry.file_size))
            return result
    command = ["7z", "l", "-slt", "-ba", "-sccUTF-8", str(path)]
    listed = subprocess.run(command, capture_output=True, text=True,
                            check=True, timeout=300)
    result = []
    for block in listed.stdout.strip().split("\n\n"):
        fields = dict(line.split(" = ", 1) for line in block.splitlines()
                      if " = " in line)
        if "Path" not in fields:
            continue
        name = fields["Path"].rstrip("/")
        safe_member(name)
        if fields.get("Encrypted") == "+" or any(
                key in fields for key in ("Symbolic Link", "Hard Link")):
            raise ValueError("archive has encrypted or linked member")
        attributes = fields.get("Attributes", "")
        if any(part.startswith("l") for part in attributes.split()[1:]):
            raise ValueError("archive contains a link")
        if attributes.startswith("D"):
            continue
        if "Size" not in fields:
            raise ValueError("archive member size missing")
        result.append((name, int(fields["Size"])))
    return result


def describe(row, selected_launch):
    path = Path(row["filePath"])
    if not path.is_absolute() or not path.is_file():
        raise ValueError("artifact file unavailable")
    size = path.stat().st_size
    if not 0 < size <= MAX_ARTIFACT:
        raise ValueError("artifact size out of range")
    suffix = path.suffix.lower()
    kind = {".zip": "zip", ".rar": "rar", ".7z": "7z"}.get(suffix, "raw")
    if kind == "raw":
        members = [(path.name, size)]
    else:
        members = archive_members(path, kind)
    if not 0 < len(members) <= MAX_FILES:
        raise ValueError("archive file count out of range")
    expanded = sum(length for _, length in members)
    if not 0 < expanded <= MAX_EXPANDED:
        raise ValueError("expanded size out of range")
    names = [name for name, _ in members]
    if len(names) != len(set(names)):
        raise ValueError("duplicate archive member")
    launch = selected_launch or (names[0] if len(names) == 1 else None)
    if launch not in names:
        raise ValueError("explicit launchPath is required for this archive")
    before = path.stat()
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    after = path.stat()
    if (before.st_size, before.st_mtime_ns, before.st_ino) != (
            after.st_size, after.st_mtime_ns, after.st_ino):
        raise ValueError("artifact changed during preparation")
    return dict(fileName=path.name, sizeBytes=size, sha256=digest.hexdigest(),
                format=kind, launchPath=launch, expandedSizeBytes=expanded,
                fileCount=len(members))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", type=Path, required=True)
    parser.add_argument("--launch-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.resolve() == args.index.resolve():
        parser.error("output must differ from input")
    index = json.loads(args.index.read_text(encoding="utf-8"))
    manifest = json.loads(args.launch_manifest.read_text(encoding="utf-8"))
    if not isinstance(index.get("items"), list) or not isinstance(manifest, dict):
        parser.error("invalid index or launch manifest")
    for row in index["items"]:
        item_id = row.get("itemId", "unknown")
        try:
            row["artifact"] = describe(row, manifest.get(item_id))
        except (OSError, ValueError, KeyError, subprocess.SubprocessError,
                zipfile.BadZipFile) as error:
            parser.error(f"item {item_id}: preparation failed ({type(error).__name__})")
    output = args.output
    if not output.parent.is_dir():
        parser.error("output directory does not exist")
    fd, temporary = tempfile.mkstemp(prefix=".station-index-", dir=output.parent)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "w", encoding="utf-8") as destination:
            json.dump(index, destination, ensure_ascii=False, separators=(",", ":"))
            destination.write("\n")
            destination.flush()
            os.fsync(destination.fileno())
        os.replace(temporary, output)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
    print(f"prepared {len(index['items'])} Station items")


if __name__ == "__main__":
    main()
