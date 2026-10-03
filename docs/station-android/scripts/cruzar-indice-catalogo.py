#!/usr/bin/env python3
"""Sanitize a private Station index and cross its games/covers with a disk snapshot.

Never commit the input index: it contains private game and cover paths. Output
contains IDs, display names, revisions, hashes and comparison status only.
"""

import argparse
from collections import Counter, defaultdict
import csv
import hashlib
import json
import os
from pathlib import Path
import re

from PIL import Image


ID = re.compile(r"[A-Za-z0-9_-]{8,64}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
MIME = {"PNG": "image/png", "JPEG": "image/jpeg",
        "WEBP": "image/webp", "GIF": "image/gif"}
EXTENSION_MIME = {".png": "image/png", ".jpg": "image/jpeg",
                  ".jpeg": "image/jpeg", ".webp": "image/webp",
                  ".gif": "image/gif"}
FIELDS = ["platform", "itemId", "name", "itemRevision", "coverId",
          "catalogVisible", "catalogMatch", "artifactFileName", "artifactFormat", "artifactSizeBytes",
          "artifactSha256", "artifactLaunchPath", "artifactExpandedSizeBytes",
          "artifactFileCount", "indexGameReadable", "indexCoverStatus", "indexCoverMime",
          "indexCoverSizeBytes", "indexCoverSha256", "diskExactNameCandidates",
          "diskExactHashCandidates", "diskCoverHashCandidates", "sourcePlatform",
          "sourceCollection", "sourceXmlEntry", "sourceGameSha256", "sourceNameMatch", "sourceRomMatch",
          "sourceCoverMatch", "sourceXmlCoverMatch", "sourceRevistaMatch",
          "sourceRevistaSha256", "sourceRevistaRule", "diagnosticItemTag", "diagnosticCoverTag"]


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate index JSON property")
        result[key] = value
    return result


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def index_cover(path):
    try:
        size = path.stat().st_size
        if not 1 <= size <= 5 * 1024 * 1024:
            return ("invalid_size", "", size, "")
        with Image.open(path) as picture:
            mime = MIME.get(picture.format, "")
            picture.verify()
        if not mime or EXTENSION_MIME.get(path.suffix.lower()) != mime:
            return ("invalid_image", "", size, "")
        return ("ok", mime, size, sha256(path))
    except FileNotFoundError:
        return ("missing", "", "", "")
    except PermissionError:
        return ("unreadable", "", "", "")
    except (OSError, ValueError, SyntaxError):
        return ("invalid_image", "", "", "")


def disk_rows(path):
    if path is None:
        return [], {}, {}
    with path.open(encoding="utf-8", newline="") as source:
        rows = list(csv.DictReader(source, delimiter="\t"))
    by_name, by_hash = defaultdict(list), defaultdict(list)
    for row in rows:
        by_name[(row["platform"], row["name"])].append(row)
        if row["gameSha256"]:
            by_hash[row["gameSha256"]].append(row)
    return rows, by_name, by_hash


def catalog_rows(path):
    if path is None:
        return None
    with path.open(encoding="utf-8", newline="") as source:
        rows = list(csv.DictReader(source, delimiter="\t"))
    by_id = {}
    for row in rows:
        item_id = row["itemId"]
        if item_id in by_id:
            raise ValueError("duplicate signed catalog itemId")
        by_id[item_id] = row
    return by_id


def source_rows(path):
    if path is None:
        return None
    source_map = json.loads(path.read_text(encoding="utf-8"),
                            object_pairs_hook=unique_pairs)
    if not isinstance(source_map, dict) or not isinstance(source_map.get("items"), list):
        raise ValueError("invalid source map")
    by_id = {}
    for row in source_map["items"]:
        if not isinstance(row, dict) or not isinstance(row.get("itemId"), str) or \
                not ID.fullmatch(row["itemId"]) or row["itemId"] in by_id or \
                not isinstance(row.get("sourcePlatform"), str) or \
                type(row.get("xmlEntry")) is not int or row["xmlEntry"] < 1 or \
                not isinstance(row.get("collection"), str) or \
                not isinstance(row.get("sourceGameSha256"), str) or \
                not HASH.fullmatch(row["sourceGameSha256"]):
            raise ValueError("invalid source map item")
        by_id[row["itemId"]] = row
    return by_id


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", type=Path, required=True)
    parser.add_argument("--disk-tsv", type=Path)
    parser.add_argument("--catalog-tsv", type=Path,
                        help="private output from exportar-catalogo-assinado.py")
    parser.add_argument("--source-map", type=Path,
                        help="private candidate source map, to verify exact XML entry")
    parser.add_argument("--require-revista", action="store_true",
                        help="fail unless every cover matches the selected revista image")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.require_revista and (not args.source_map or not args.disk_tsv):
        parser.error("revista verification requires source map and disk snapshot")
    if args.output.resolve() == args.index.resolve():
        parser.error("output must differ from the private index")
    if args.catalog_tsv and args.output.resolve() == args.catalog_tsv.resolve():
        parser.error("output must differ from the signed catalog export")
    index = json.loads(args.index.read_text(encoding="utf-8"),
                       object_pairs_hook=unique_pairs)
    revision = index.get("revision")
    items = index.get("items")
    if type(revision) is not int or revision < 1 or not isinstance(items, list) or \
            len(items) > 4096:
        parser.error("invalid Station index revision or item count")
    disk, by_name, by_hash = disk_rows(args.disk_tsv)
    by_entry = {(row["platform"], row["xmlEntry"]): row for row in disk}
    if len(by_entry) != len(disk):
        parser.error("duplicate disk XML entry")
    signed_catalog = catalog_rows(args.catalog_tsv)
    sources = source_rows(args.source_map)
    cover_ids = {}
    item_ids = set()
    output = []
    for row in items:
        if not isinstance(row, dict):
            parser.error("invalid Station index item")
        item_id, cover_id = row.get("itemId"), row.get("coverId")
        item_revision = row.get("revision", revision)
        name, platform = row.get("name"), row.get("platform")
        if not isinstance(item_id, str) or not ID.fullmatch(item_id) or \
                item_id in item_ids or not isinstance(cover_id, str) or \
                not ID.fullmatch(cover_id) or type(item_revision) is not int or \
                item_revision < 1 or not isinstance(name, str) or \
                not 1 <= len(name) <= 120 or not isinstance(platform, str) or \
                not 1 <= len(platform) <= 120 or any(ord(c) < 32 for c in name+platform):
            parser.error("invalid Station index item identity")
        item_ids.add(item_id)
        game_path, cover_path = row.get("filePath"), row.get("coverPath")
        if not isinstance(game_path, str) or not Path(game_path).is_absolute() or \
                not isinstance(cover_path, str) or not Path(cover_path).is_absolute():
            parser.error("invalid Station index item path")
        cover_link = (cover_path, item_revision)
        if cover_id in cover_ids and cover_ids[cover_id] != cover_link:
            parser.error("coverId points to conflicting path or revision")
        cover_ids[cover_id] = cover_link
        artifact = row.get("artifact")
        artifact_hash = artifact.get("sha256", "") if isinstance(artifact, dict) else ""
        if artifact_hash and (not isinstance(artifact_hash, str) or
                              not HASH.fullmatch(artifact_hash)):
            parser.error("invalid Station artifact hash")
        cover = index_cover(Path(cover_path))
        if signed_catalog is None:
            catalog_match = "not_supplied"
        elif row.get("catalogVisible") is False and item_id not in signed_catalog:
            catalog_match = "compatibility_hidden"
        else:
            signed = signed_catalog.get(item_id)
            catalog_match = "missing_in_catalog" if signed is None else (
                "exact" if all(signed[field] == str(value) for field, value in
                    (("platform", platform), ("name", name),
                     ("itemRevision", item_revision), ("coverId", cover_id)))
                else "mismatch")
        disk_platform = {"snesbr": "snes", "megadrivebr": "megadrive"}.get(platform,
            platform)
        name_matches = by_name.get((disk_platform, name), [])
        hash_matches = [candidate for candidate in by_hash.get(artifact_hash, [])
                        if candidate["platform"] == disk_platform] if artifact_hash else []
        cover_matches = 0
        if cover[3]:
            cover_matches = sum(cover[3] == candidate["xmlCoverSha256"] or
                cover[3] in candidate["revistaCandidateSha256"].split(";")
                for candidate in (hash_matches or name_matches))
        try:
            with Path(game_path).open("rb") as source:
                source.read(1)
            game_readable = True
        except OSError:
            game_readable = False
        source_row = sources.get(item_id) if sources is not None else None
        if sources is not None and source_row is None:
            parser.error("source map is missing item " + item_id)
        source_disk = by_entry.get((source_row["sourcePlatform"],
                                    str(source_row["xmlEntry"]))) if source_row else None
        if source_row and source_disk is None:
            parser.error("source XML entry is missing for item " + item_id)
        source_rom_match = "" if source_row is None else (
            "yes" if source_disk["gamePresent"] == "yes" and
            source_disk["gameSha256"] == source_row["sourceGameSha256"] else "no")
        source_name_match = "" if source_row is None else (
            "yes" if name == source_disk["name"] else "no")
        source_cover_match = "" if source_row is None else (
            "yes" if cover[3] and (cover[3] == source_disk["xmlCoverSha256"] or
            cover[3] in source_disk["revistaCandidateSha256"].split(";")) else "no")
        selected_revista_hash = source_disk.get("revistaSelectedSha256", "") if source_disk else ""
        source_revista_match = "not_supplied" if not selected_revista_hash else (
            "yes" if cover[3] == selected_revista_hash and
            source_disk.get("revistaSelectionStatus") == "ok" else "no")
        if args.require_revista and source_revista_match != "yes":
            parser.error("indexed cover differs from selected revista for item " + item_id)
        output.append({"platform": platform, "itemId": item_id, "name": name,
            "itemRevision": item_revision, "coverId": cover_id,
            "catalogVisible": "no" if row.get("catalogVisible") is False else "yes",
            "catalogMatch": catalog_match,
            "artifactFileName": artifact.get("fileName", "") if isinstance(artifact, dict) else "",
            "artifactFormat": artifact.get("format", "") if isinstance(artifact, dict) else "",
            "artifactSizeBytes": artifact.get("sizeBytes", "") if isinstance(artifact, dict) else "",
            "artifactSha256": artifact_hash,
            "artifactLaunchPath": artifact.get("launchPath", "") if isinstance(artifact, dict) else "",
            "artifactExpandedSizeBytes": artifact.get("expandedSizeBytes", "") if isinstance(artifact, dict) else "",
            "artifactFileCount": artifact.get("fileCount", "") if isinstance(artifact, dict) else "",
            "indexGameReadable": "yes" if game_readable else "no",
            "indexCoverStatus": cover[0], "indexCoverMime": cover[1],
            "indexCoverSizeBytes": cover[2], "indexCoverSha256": cover[3],
            "diskExactNameCandidates": len(name_matches),
            "diskExactHashCandidates": len(hash_matches),
            "diskCoverHashCandidates": cover_matches,
            "sourcePlatform": source_row["sourcePlatform"] if source_row else "",
            "sourceCollection": source_row["collection"] if source_row else "",
            "sourceXmlEntry": source_row["xmlEntry"] if source_row else "",
            "sourceGameSha256": source_row["sourceGameSha256"] if source_row else "",
            "sourceNameMatch": source_name_match,
            "sourceRomMatch": source_rom_match,
            "sourceCoverMatch": source_cover_match,
            "sourceXmlCoverMatch": "" if source_disk is None else (
                "yes" if cover[3] and cover[3] == source_disk["xmlCoverSha256"] else "no"),
            "sourceRevistaMatch": source_revista_match,
            "sourceRevistaSha256": selected_revista_hash,
            "sourceRevistaRule": source_disk.get("revistaSelectionRule", "") if source_disk else "",
            "diagnosticItemTag": hashlib.sha256(item_id.encode("utf-8")).hexdigest(),
            "diagnosticCoverTag": hashlib.sha256(cover_id.encode("utf-8")).hexdigest()})
    if sources is not None and len(sources) != len(output):
        parser.error("source map has items missing from index")
    output.sort(key=lambda row: (row["platform"], row["name"].casefold(),
                                  row["itemId"]))
    fd = os.open(args.output, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as destination:
            writer = csv.DictWriter(destination, fieldnames=FIELDS, delimiter="\t",
                                    lineterminator="\n")
            writer.writeheader()
            writer.writerows(output)
    except BaseException:
        args.output.unlink(missing_ok=True)
        raise
    print(json.dumps({"revision": revision, "items": len(output),
        "signedCatalogItemsMissingInIndex": len(set(signed_catalog) - item_ids)
            if signed_catalog is not None else None,
        "catalogMatchCounts": dict(sorted(Counter(row["catalogMatch"]
                                          for row in output).items())),
        "platformCounts": dict(sorted(Counter(row["platform"] for row in output).items())),
        "coverStatusCounts": dict(sorted(Counter(row["indexCoverStatus"]
                                          for row in output).items())),
        "sourceRomMatchCounts": dict(sorted(Counter(row["sourceRomMatch"]
                                          for row in output).items())) if sources is not None else None,
        "sourceNameMatchCounts": dict(sorted(Counter(row["sourceNameMatch"]
                                          for row in output).items())) if sources is not None else None,
        "sourceCoverMatchCounts": dict(sorted(Counter(row["sourceCoverMatch"]
                                          for row in output).items())) if sources is not None else None,
        "sourceRevistaMatchCounts": dict(sorted(Counter(row["sourceRevistaMatch"]
                                          for row in output).items())),
        "sharedCoverIds": len(output) - len(cover_ids)}, ensure_ascii=False,
        sort_keys=True))


if __name__ == "__main__":
    main()
