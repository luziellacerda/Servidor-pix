#!/usr/bin/env python3
"""Build a private, standalone Station index candidate from SNES/Mega Drive XML.

This does not merge the live index or deploy anything. Existing item IDs must be
reconciled with the protected index before this candidate can replace it.
"""

import argparse
from collections import Counter, defaultdict
import csv
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
from xml.etree import ElementTree
import zipfile

from station_revista import EXTENSION_MIME, select_revista_cover


PLATFORMS = ("snes", "megadrive")
ROM_MEMBERS = {"snes": {".sfc", ".smc", ".swc", ".fig"},
               "megadrive": {".bin", ".md", ".gen", ".smd"}}

def digest(path):
    hashed = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            hashed.update(block)
    return hashed.hexdigest()


def checked_path(root, relative):
    if not relative:
        raise ValueError("missing XML path")
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("XML path leaves platform root")
    return path


def select_cover(root, game, rom, magazine, expected):
    candidate, _, _ = select_revista_cover(root, rom, magazine, expected)
    return candidate, "revista"


def curated_zip(source, target, platform):
    with zipfile.ZipFile(source) as archive:
        members = [entry for entry in archive.infolist() if not entry.is_dir()]
        if len(members) == 1:
            if Path(members[0].filename).suffix.lower() not in ROM_MEMBERS[platform]:
                raise ValueError("ZIP does not contain a playable ROM")
            return source, False
        playable = [entry for entry in members if
                    Path(entry.filename).suffix.lower() in ROM_MEMBERS[platform]]
        if len(playable) != 1:
            raise ValueError("ZIP has no unique playable ROM")
        selected = playable[0]
        if not 0 < selected.file_size <= 4 * (1 << 40):
            raise ValueError("playable member size out of range")
        with archive.open(selected) as original, zipfile.ZipFile(
                target, "x", compression=zipfile.ZIP_DEFLATED,
                compresslevel=6, allowZip64=True) as curated:
            with curated.open(Path(selected.filename).name, "w", force_zip64=True) as output:
                for block in iter(lambda: original.read(1024 * 1024), b""):
                    output.write(block)
        return target, True


def load_disk(path):
    with path.open(encoding="utf-8", newline="") as source:
        rows = list(csv.DictReader(source, delimiter="\t"))
    result = {(row["platform"], int(row["xmlEntry"])): row for row in rows}
    if len(result) != len(rows):
        raise ValueError("duplicate disk inventory key")
    return result


def describe_function():
    path = Path(__file__).with_name("preparar-indice-artefatos.py")
    spec = importlib.util.spec_from_file_location("station_artifacts", path)
    module = importlib.util.module_from_spec(spec)
    before = sys.dont_write_bytecode
    try:
        sys.dont_write_bytecode = True
        spec.loader.exec_module(module)
    finally:
        sys.dont_write_bytecode = before
    return module.describe


def private_json(path, content):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as destination:
        json.dump(content, destination, ensure_ascii=False, separators=(",", ":"))
        destination.write("\n")
        destination.flush()
        os.fsync(destination.fileno())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--volume-root", type=Path, required=True)
    parser.add_argument("--disk-tsv", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--revision", type=int, required=True)
    args = parser.parse_args()
    if args.revision < 1:
        parser.error("revision must be positive")
    volume = args.volume_root.resolve()
    if not all((volume / platform / "gamelist.xml").is_file() for platform in PLATFORMS):
        parser.error("volume root must contain both platform gamelist.xml files")
    target = args.output_dir.resolve()
    if target.is_relative_to(Path(__file__).resolve().parents[3]):
        parser.error("private output must be outside the repository")
    target.mkdir(mode=0o700, parents=False, exist_ok=False)
    stage = target / "curated"
    stage.mkdir(mode=0o700)
    expected = load_disk(args.disk_tsv)
    describe = describe_function()
    items = []
    source_map = []
    platform_counts = Counter()
    cover_sources = Counter()
    repacked = 0
    skipped_missing = 0
    for platform in PLATFORMS:
        root = volume / platform
        magazine = defaultdict(list)
        for path in (root / "media" / "revista").rglob("*"):
            if path.is_file() and path.suffix.lower() in EXTENSION_MIME:
                magazine[path.stem].append(path.resolve())
        games = ElementTree.parse(root / "gamelist.xml").getroot().findall("game")
        for ordinal, game in enumerate(games, 1):
            expected_row = expected.get((platform, ordinal))
            if expected_row is None or expected_row["name"] != game.findtext("name"):
                raise ValueError("XML and disk inventory differ")
            rom = checked_path(root, game.findtext("path"))
            if not rom.is_file():
                if expected_row["gamePresent"] != "no":
                    raise ValueError("ROM disappeared since disk inventory")
                skipped_missing += 1
                continue
            if expected_row["gamePresent"] != "yes" or \
                    digest(rom) != expected_row["gameSha256"]:
                raise ValueError("ROM changed since disk inventory")
            relative = rom.relative_to(root.resolve()).as_posix()
            stable = hashlib.sha256((platform + ":" + relative).encode()).hexdigest()[:32]
            item_id = "station_" + stable
            cover, source = select_cover(root, game, rom, magazine, expected_row)
            cover_sources[source] += 1
            curated_path = stage / (item_id + ".zip")
            artifact_path, was_repacked = (curated_zip(rom, curated_path, platform)
                                           if rom.suffix.lower() == ".zip"
                                           else (rom, False))
            repacked += was_repacked
            collection = expected_row["collection"]
            catalog_platform = ("snesbr" if platform == "snes" else "megadrivebr") \
                if collection == "pt-br" else platform
            item = {"itemId": item_id, "name": expected_row["name"],
                    "platform": catalog_platform, "revision": args.revision,
                    "coverId": "cover_" + stable, "filePath": str(artifact_path),
                    "coverPath": str(cover)}
            item["artifact"] = describe(item, None)
            items.append(item)
            source_map.append({"itemId": item_id, "sourcePlatform": platform,
                               "collection": collection, "xmlEntry": ordinal,
                               "sourcePath": str(rom),
                               "sourceGameSha256": expected_row["gameSha256"]})
            platform_counts[catalog_platform] += 1
    if len(items) > 4096 or len({row["itemId"] for row in items}) != len(items):
        raise ValueError("invalid candidate item count or duplicate ID")
    index = {"revision": args.revision, "items": items}
    private_json(target / "index.json", index)
    private_json(target / "source-map.json", {"items": source_map})
    report = {"revision": args.revision, "items": len(items),
              "platformCounts": dict(sorted(platform_counts.items())),
              "coverSources": dict(sorted(cover_sources.items())),
              "curatedArchives": repacked, "missingXmlRomsExcluded": skipped_missing,
              "indexSha256": digest(target / "index.json"),
              "sourceMapSha256": digest(target / "source-map.json")}
    private_json(target / "report.json", report)
    print(json.dumps(report, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
