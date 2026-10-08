#!/usr/bin/env python3
"""Prepare exact offline content identities; never approve multiplayer profiles.

Accepted catalogs: the R19 public export (items containing artifact descriptors),
the public records export with artifact descriptors, or the R77 records inventory
with flattened artifact* fields. The private mapping is {itemId: absolute artifact
path}. ZIP launch bytes are streamed without extraction or normalization.

Output matches contentIdentityRegistry in server commit
b472d8a653e065cccb00dfb15dbea3d56c03db98 exactly. No private path is emitted.
Use --existing to preserve prior identities whose bindings still match the current
catalog. Output must be a new file; this tool never overwrites a deployed registry.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
import tempfile
import zipfile

MAX_BYTES = 4 * (1 << 40)
MAX_FILES = 100000
MAX_ENTRIES = 4096
MAX_REGISTRY_BYTES = 16 * 1024 * 1024
MAX_CATALOG_BYTES = 32 * 1024 * 1024
CHUNK = 1024 * 1024
ENTRY_KEYS = frozenset(("itemId", "platform", "artifactSha256", "launchPath",
                        "expandedSizeBytes", "fileCount", "contentSha256"))
NO_EXISTING = object()


class ValidationError(ValueError):
    """Messages contain field names only, never operator paths or file contents."""


def need(condition, reason):
    if not condition:
        raise ValidationError(reason)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, "duplicate JSON member")
        result[key] = value
    return result


def load_json(path, limit):
    with Path(path).open("rb") as stream:
        data = stream.read(limit + 1)
    need(len(data) <= limit, "JSON size limit exceeded")
    return json.loads(data.decode("utf-8-sig"), object_pairs_hook=unique_object)


def text(value, limit, label):
    need(isinstance(value, str) and 0 < len(value.encode("utf-16-le")) // 2 <= limit
         and not any(ord(c) < 32 for c in value), "invalid " + label)
    return value


def integer(value, maximum, label):
    need(type(value) is int and 0 < value <= maximum, "invalid " + label)
    return value


def digest(value, label):
    need(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) is not None,
         "invalid " + label)
    return value


def member_path(value, *, directory=False):
    text(value, 4096, "launch/member path")
    candidate = value[:-1] if directory and value.endswith("/") else value
    need(bool(candidate) and not candidate.startswith("/") and "\\" not in candidate
         and ":" not in candidate
         and all(part not in ("", ".", "..") for part in candidate.split("/")),
         "unsafe launch/member path")
    return candidate


def catalog_records(document):
    need(isinstance(document, dict), "catalog object required")
    need(("items" in document) != ("records" in document), "one catalog row array required")
    rows = document.get("items", document.get("records"))
    need(isinstance(rows, list) and len(rows) <= MAX_ENTRIES, "invalid catalog row count")
    result = {}
    for row in rows:
        need(isinstance(row, dict), "catalog row object required")
        item = text(row.get("itemId"), 256, "itemId")
        need(item not in result, "duplicate catalog itemId")
        result[item] = row
    return result


def descriptor(row):
    platform = text(row.get("platform"), 64, "platform")
    if "artifact" in row:
        artifact = row["artifact"]
        need(isinstance(artifact, dict), "artifact object required")
    else:
        artifact = {name: row.get("artifact" + name[0].upper() + name[1:]) for name in
                    ("format", "sha256", "sizeBytes", "launchPath", "expandedSizeBytes", "fileCount")}
    form = artifact.get("format")
    need(form in ("raw", "zip"), "unsupported artifact format")
    sha = digest(artifact.get("sha256"), "artifact SHA256")
    size = integer(artifact.get("sizeBytes"), MAX_BYTES, "artifact size")
    expanded = integer(artifact.get("expandedSizeBytes"), MAX_BYTES, "expanded size")
    count = integer(artifact.get("fileCount"), MAX_FILES, "file count")
    launch = artifact.get("launchPath")
    member_path(launch)
    if form == "raw":
        need(count == 1 and size == expanded, "raw descriptor mismatch")
    return platform, form, sha, size, expanded, count, launch


def stream_digest(stream, expected):
    sha, total = hashlib.sha256(), 0
    while True:
        block = stream.read(min(CHUNK, expected - total + 1))
        if not block:
            break
        total += len(block)
        need(total <= expected, "payload exceeds expected size")
        sha.update(block)
    need(total == expected, "payload size mismatch")
    return sha.hexdigest()


def file_digest(path, expected):
    with path.open("rb") as stream:
        return stream_digest(stream, expected)


def snapshot(path):
    value = path.stat()
    need(stat.S_ISREG(value.st_mode) and not path.is_symlink(), "regular artifact required")
    return value.st_dev, value.st_ino, value.st_size, value.st_mtime_ns


def bind(row, path):
    """Independently verify the container and the exact emulator launch payload."""
    item = text(row.get("itemId"), 256, "itemId")
    platform, form, sha, size, expanded, count, launch = descriptor(row)
    need(isinstance(path, str) and bool(path), "artifact mapping string required")
    source = Path(path)
    need(source.is_absolute(), "absolute artifact path required")
    before = snapshot(source)
    need(before[2] == size, "artifact size mismatch")
    need(file_digest(source, size) == sha, "artifact digest mismatch")
    content_sha = sha
    if form == "zip":
        with zipfile.ZipFile(source) as archive:
            members = archive.infolist()
            # Directory records are also bounded; nothing is extracted to disk.
            need(len(members) <= MAX_FILES * 2, "ZIP member count limit exceeded")
            names, files, total = set(), [], 0
            for member in members:
                need(member.orig_filename == member.filename, "ZIP member name normalization refused")
                normalized = member_path(member.filename, directory=member.is_dir())
                need(normalized not in names, "duplicate ZIP member")
                names.add(normalized)
                mode = member.external_attr >> 16
                need(not stat.S_ISLNK(mode), "ZIP symbolic link refused")
                need(not member.flag_bits & 1, "encrypted ZIP member refused")
                need(member.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED),
                     "unsupported ZIP compression")
                if member.is_dir():
                    need(member.file_size == 0, "nonempty ZIP directory refused")
                    continue
                need(0 <= member.file_size <= MAX_BYTES, "invalid ZIP expanded size")
                total += member.file_size
                need(total <= expanded, "ZIP expanded size exceeds descriptor")
                files.append(member)
            need(len(files) == count, "ZIP file count mismatch")
            need(total == expanded, "ZIP expanded size mismatch")
            matches = [entry for entry in files if entry.filename == launch]
            need(len(matches) == 1 and matches[0].file_size > 0, "exact ZIP launch member missing/empty")
            with archive.open(matches[0]) as stream:
                content_sha = stream_digest(stream, matches[0].file_size)
        need(file_digest(source, size) == sha, "artifact changed during binding")
    need(snapshot(source) == before, "artifact changed during binding")
    entry = dict(itemId=item, platform=platform, artifactSha256=sha, launchPath=launch,
                 expandedSizeBytes=expanded, fileCount=count, contentSha256=content_sha)
    need(set(entry) == ENTRY_KEYS, "internal output schema mismatch")
    return entry


def existing_entries(document):
    need(isinstance(document, dict) and set(document) == {"schemaVersion", "entries"}
         and type(document["schemaVersion"]) is int and document["schemaVersion"] == 1,
         "invalid existing registry schema")
    entries = document["entries"]
    need(isinstance(entries, list) and len(entries) <= MAX_ENTRIES, "invalid existing entry count")
    result = {}
    for entry in entries:
        need(isinstance(entry, dict) and set(entry) == ENTRY_KEYS, "invalid existing entry fields")
        item = text(entry["itemId"], 256, "existing itemId")
        text(entry["platform"], 64, "existing platform")
        digest(entry["artifactSha256"], "existing artifact SHA256")
        digest(entry["contentSha256"], "existing content SHA256")
        member_path(entry["launchPath"])
        integer(entry["expandedSizeBytes"], MAX_BYTES, "existing expanded size")
        integer(entry["fileCount"], MAX_FILES, "existing file count")
        need(item not in result, "duplicate existing itemId")
        result[item] = dict(entry)
    return result


def preserve_existing(entry, row):
    platform, form, sha, _size, expanded, count, launch = descriptor(row)
    need(entry["platform"] == platform and entry["artifactSha256"] == sha
         and entry["launchPath"] == launch and entry["expandedSizeBytes"] == expanded
         and entry["fileCount"] == count
         and (form != "raw" or entry["contentSha256"] == sha),
         "existing identity differs from current descriptor; map its current artifact explicitly")


def prepare(catalog, mapping, existing=NO_EXISTING):
    records = catalog_records(catalog)
    need(isinstance(mapping, dict) and len(mapping) <= MAX_ENTRIES, "invalid artifact mapping")
    entries = {} if existing is NO_EXISTING else existing_entries(existing)
    for item, entry in entries.items():
        if item not in mapping:
            need(item in records, "existing identity absent from current catalog")
            preserve_existing(entry, records[item])
    for item in sorted(mapping):
        text(item, 256, "mapping itemId")
        need(item in records, "mapping item absent from catalog")
        entries[item] = bind(records[item], mapping[item])
    need(len(entries) <= MAX_ENTRIES, "merged entry count limit exceeded")
    result = dict(schemaVersion=1, entries=[entries[item] for item in sorted(entries)])
    encoded = (json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    need(len(encoded) <= MAX_REGISTRY_BYTES, "output registry size limit exceeded")
    return result, encoded


def write_registry(destination, data):
    destination = Path(destination)
    need(not destination.exists() and not destination.is_symlink(), "output must be a new file")
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="wb", prefix=".content-identity-", suffix=".tmp",
                                         dir=destination.parent, delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        # Hard-link publication is atomic and fails if any output appeared meanwhile.
        # Unlike replace(), it cannot overwrite production or another operator's work.
        os.link(temporary, destination)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--mapping", type=Path, required=True)
    parser.add_argument("--existing", type=Path, help="Qualified registry to preserve and merge strictly")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        catalog = load_json(args.catalog, MAX_CATALOG_BYTES)
        mapping = load_json(args.mapping, MAX_REGISTRY_BYTES)
        existing = load_json(args.existing, MAX_REGISTRY_BYTES) if args.existing is not None else NO_EXISTING
        output = args.output.resolve()
        inputs = [args.catalog.resolve(), args.mapping.resolve()]
        if args.existing is not None:
            inputs.append(args.existing.resolve())
        need(output not in inputs, "output overlaps input")
        need(not args.output.exists() and not args.output.is_symlink(), "output must be a new file")
        if isinstance(mapping, dict):
            need(all(not isinstance(value, str) or Path(value).resolve() != output for value in mapping.values()),
                 "output overlaps artifact")
        result, encoded = prepare(catalog, mapping, existing)
        write_registry(args.output, encoded)
        print(json.dumps(dict(entries=len(result["entries"]), registrySha256=hashlib.sha256(encoded).hexdigest(),
                              approvedProfiles=0)))
        return 0
    except ValidationError as error:
        print("Content identity preparation rejected: " + str(error) + ".", file=sys.stderr)
    except Exception as error:
        # Never print exception details, which can embed private absolute paths.
        print("Content identity preparation rejected (" + type(error).__name__ + ").", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
