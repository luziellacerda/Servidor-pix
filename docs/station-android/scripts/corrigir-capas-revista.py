#!/usr/bin/env python3
"""Prepare a private cover-only correction, preserving every Station identity/ROM.

The output keeps canonical and hidden compatibility rows. Publication is a
separate operation; this script never changes the live index or source media.
"""

import argparse
from collections import Counter, defaultdict
import copy
import csv
import hashlib
import json
import os
from pathlib import Path
from xml.etree import ElementTree

from station_revista import EXTENSION_MIME, image_hash, select_revista_cover


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def private_json(path, content):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as output:
        json.dump(content, output, ensure_ascii=False, separators=(",", ":"))
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())


def correct(index, sources, disk, volume, revision):
    items = index["items"]
    if revision <= max([index["revision"]] + [r.get("revision", index["revision"])
                                            for r in items]):
        raise ValueError("revision must increase for catalog and every item")
    by_id = {r["itemId"]: r for r in sources["items"]}
    if len(by_id) != len(sources["items"]) or set(by_id) != {r["itemId"] for r in items}:
        raise ValueError("source map differs from indexed identities")
    if len({r["itemId"] for r in items}) != len(items):
        raise ValueError("duplicate index itemId")
    games, magazines = {}, {}
    for platform in ("snes", "megadrive"):
        root = volume / platform
        games[platform] = ElementTree.parse(root / "gamelist.xml").getroot().findall("game")
        magazine = defaultdict(list)
        for path in (root / "media" / "revista").rglob("*"):
            if path.is_file() and path.suffix.lower() in EXTENSION_MIME:
                magazine[path.stem].append(path)
        magazines[platform] = magazine
    result, audit = [], []
    rom_hashes = {}
    for row in items:
        source = by_id[row["itemId"]]
        platform, ordinal = source["sourcePlatform"], source["xmlEntry"]
        if platform not in games or not 1 <= ordinal <= len(games[platform]):
            raise ValueError("source map has an invalid XML entry")
        root = (volume / platform).resolve()
        game = games[platform][ordinal - 1]
        rom = (root / game.findtext("path", "")).resolve()
        expected = disk.get((platform, str(ordinal)))
        if not rom.is_relative_to(root) or str(rom) != source["sourcePath"] or \
                expected is None or expected["name"] != row["name"] or \
                game.findtext("name") != row["name"] or \
                expected["gamePresent"] != "yes" or \
                expected["gameSha256"] != source["sourceGameSha256"]:
            raise ValueError("indexed game differs from exact XML/ROM provenance")
        if rom not in rom_hashes:
            rom_hashes[rom] = digest(rom)
        if rom_hashes[rom] != source["sourceGameSha256"]:
            raise ValueError("source ROM changed since disk inventory")
        cover, rule, cover_hash = select_revista_cover(root, rom, magazines[platform], expected)
        previous_hash = image_hash(Path(row["coverPath"]))
        if previous_hash is None:
            raise ValueError("previous cover cannot be checked")
        corrected = copy.deepcopy(row)
        corrected["revision"] = revision
        corrected["coverPath"] = str(cover)
        result.append(corrected)
        audit.append({"platform": row["platform"], "itemId": row["itemId"],
                      "name": row["name"], "coverId": row["coverId"],
                      "catalogVisible": row.get("catalogVisible") is not False,
                      "selectionRule": rule, "previousCoverSha256": previous_hash,
                      "revistaSelectedSha256": cover_hash,
                      "coverChanged": previous_hash != cover_hash,
                      "sourcePlatform": platform, "sourceXmlEntry": ordinal})
    links = {}
    for row in result:
        link = (row["coverPath"], row["revision"])
        if row["coverId"] in links and links[row["coverId"]] != link:
            raise ValueError("coverId would identify different images")
        links[row["coverId"]] = link
    return {"revision": revision, "items": result}, audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-index", type=Path, required=True)
    parser.add_argument("--source-map", type=Path, required=True)
    parser.add_argument("--disk-tsv", type=Path, required=True)
    parser.add_argument("--volume-root", type=Path, required=True)
    parser.add_argument("--revision", type=int, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    target = args.output_dir.resolve()
    if target.is_relative_to(Path(__file__).resolve().parents[3]):
        parser.error("private output must be outside repository")
    index = json.loads(args.base_index.read_text(encoding="utf-8"))
    sources = json.loads(args.source_map.read_text(encoding="utf-8"))
    with args.disk_tsv.open(encoding="utf-8", newline="") as source:
        rows = list(csv.DictReader(source, delimiter="\t"))
    disk = {(r["platform"], r["xmlEntry"]): r for r in rows}
    if len(disk) != len(rows):
        raise ValueError("duplicate disk inventory entry")
    corrected, audit = correct(index, sources, disk, args.volume_root.resolve(), args.revision)
    target.mkdir(mode=0o700, parents=False, exist_ok=False)
    private_json(target / "index.json", corrected)
    private_json(target / "cover-audit.json", {"revision": args.revision, "items": audit})
    report = {"previousRevision": index["revision"], "revision": args.revision,
              "items": len(audit), "catalogItems": sum(r["catalogVisible"] for r in audit),
              "changedCovers": sum(r["coverChanged"] for r in audit),
              "changedVisibleCovers": sum(r["coverChanged"] and r["catalogVisible"] for r in audit),
              "selectionRules": dict(sorted(Counter(r["selectionRule"] for r in audit).items())),
              "identitiesAndArtifactsPreserved": True,
              "sourceIndexSha256": digest(args.base_index),
              "indexSha256": digest(target / "index.json"),
              "coverAuditSha256": digest(target / "cover-audit.json"),
              "diskSnapshotSha256": digest(args.disk_tsv)}
    private_json(target / "report.json", report)
    print(json.dumps(report, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
