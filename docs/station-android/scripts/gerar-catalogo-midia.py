#!/usr/bin/env python3
"""Inventory game/cover pairs from SNES and Mega Drive gamelist.xml files.

This is a disk reference, not the authenticated Station catalog. It exports no
private paths. Magazine covers are paired only by an exact ROM/cover stem match;
multiple matches remain ambiguous and require an explicit choice in the index.
"""

import argparse
from collections import Counter, defaultdict
import csv
import hashlib
from pathlib import Path
from xml.etree import ElementTree

from PIL import Image


IMAGE_MIME = {"PNG": "image/png", "JPEG": "image/jpeg",
              "WEBP": "image/webp", "GIF": "image/gif"}
ROM_EXTENSIONS = {"snes": {".sfc", ".smc", ".zip", ".swc", ".fig"},
                  "megadrive": {".zip", ".bin", ".md", ".gen", ".smd"}}
FIELDS = ["platform", "xmlEntry", "collection", "name", "gamePresent",
          "gameSizeBytes", "gameSha256", "xmlCoverStatus", "xmlCoverMime",
          "xmlCoverSizeBytes", "xmlCoverWidth", "xmlCoverHeight", "xmlCoverSha256",
          "revistaCandidateCount", "revistaValidCount", "revistaDistinctHashCount",
          "revistaCandidateSha256"]


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def within(root, value):
    if not value:
        return None
    path = Path(value)
    path = (path if path.is_absolute() else root / path).resolve()
    return path if path.is_relative_to(root.resolve()) else None


def image_info(path, cache):
    if path in cache:
        return cache[path]
    if path is None or not path.is_file():
        result = ("missing", "", "", "", "", "")
    else:
        size = path.stat().st_size
        try:
            with Image.open(path) as picture:
                mime = IMAGE_MIME.get(picture.format, "")
                width, height = picture.size
                picture.verify()
            if not mime or not 1 <= size <= 5 * 1024 * 1024 or \
                    width < 1 or height < 1:
                result = ("invalid", mime, size, width, height, "")
            else:
                result = ("ok", mime, size, width, height, sha256(path))
        except (OSError, ValueError, SyntaxError):
            result = ("invalid", "", size, "", "", "")
    cache[path] = result
    return result


def collect(root, platform, cache):
    platform_root = root / platform
    games = ElementTree.parse(platform_root / "gamelist.xml").getroot().findall("game")
    magazine = defaultdict(list)
    for path in (platform_root / "media" / "revista").rglob("*"):
        if path.is_file() and path.suffix.lower() in (
                ".png", ".jpg", ".jpeg", ".webp", ".gif"):
            magazine[path.stem].append(path)
    result = []
    collection_folder = "## 1 -PT-BR ##" if platform == "snes" else "# PT-BR #"
    for ordinal, game in enumerate(games, 1):
        name = game.findtext("name")
        relative_game = game.findtext("path")
        if not name or not relative_game:
            raise ValueError(f"incomplete {platform} XML game #{ordinal}")
        game_path = within(platform_root, relative_game)
        if game_path is None or game_path.suffix.lower() not in ROM_EXTENSIONS[platform]:
            raise ValueError(f"unsafe {platform} XML game #{ordinal}")
        exists = game_path.is_file()
        cover_text = game.findtext("image")
        cover_path = within(platform_root, cover_text)
        if cover_text and cover_path is None:
            raise ValueError(f"unsafe {platform} XML image #{ordinal}")
        cover = (image_info(cover_path, cache) if cover_text else
                 ("not_declared", "", "", "", "", ""))
        candidates = magazine[game_path.stem]
        inspected = [image_info(candidate, cache) for candidate in candidates]
        valid_hashes = [info[5] for info in inspected if info[0] == "ok"]
        result.append({
            "platform": platform, "xmlEntry": ordinal,
            "collection": "pt-br" if collection_folder in Path(relative_game).parts
                          else "geral", "name": name,
            "gamePresent": "yes" if exists else "no",
            "gameSizeBytes": game_path.stat().st_size if exists else "",
            "gameSha256": sha256(game_path) if exists else "",
            "xmlCoverStatus": cover[0], "xmlCoverMime": cover[1],
            "xmlCoverSizeBytes": cover[2], "xmlCoverWidth": cover[3],
            "xmlCoverHeight": cover[4], "xmlCoverSha256": cover[5],
            "revistaCandidateCount": len(candidates),
            "revistaValidCount": sum(info[0] == "ok" for info in inspected),
            "revistaDistinctHashCount": len(set(valid_hashes)),
            "revistaCandidateSha256": ";".join(sorted(valid_hashes)) or "-"
        })
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--volume-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("output already exists; choose a new snapshot name")
    cache = {}
    rows = collect(args.volume_root, "snes", cache) + \
           collect(args.volume_root, "megadrive", cache)
    rows.sort(key=lambda row: (row["platform"], row["name"].casefold(),
                               row["xmlEntry"]))
    with args.output.open("w", encoding="utf-8", newline="") as destination:
        writer = csv.DictWriter(destination, fieldnames=FIELDS, delimiter="\t",
                                lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    print("items", len(rows))
    for platform in ("snes", "megadrive"):
        part = [row for row in rows if row["platform"] == platform]
        print(platform, dict(Counter(row["gamePresent"] for row in part)),
              "xml_cover", dict(Counter(row["xmlCoverStatus"] for row in part)),
              "revista_candidates", dict(Counter(row["revistaCandidateCount"]
                                              for row in part)))
    print("sha256", hashlib.sha256(args.output.read_bytes()).hexdigest())


if __name__ == "__main__":
    main()
