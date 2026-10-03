#!/usr/bin/env python3
"""Reconcile a protected Station index with the complete SNES/Mega Drive candidate.

Preserves published item IDs by exact source path or ROM SHA256. Fails closed on
unmatched/ambiguous published games, unreadable covers or unprepared artifacts.
Inputs and output contain private paths; keep them outside Git.
"""

import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys


TARGET = {"snes", "snesbr", "megadrive", "megadrivebr"}
FAMILY = {"snes": "snes", "snesbr": "snes", "megadrive": "megadrive",
          "megadrivebr": "megadrive"}
ID = re.compile(r"[A-Za-z0-9_-]{8,64}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")


def module_function(filename, function):
    path = Path(__file__).with_name(filename)
    spec = importlib.util.spec_from_file_location(filename.replace("-", "_"), path)
    module = importlib.util.module_from_spec(spec)
    before = sys.dont_write_bytecode
    try:
        sys.dont_write_bytecode = True
        spec.loader.exec_module(module)
    finally:
        sys.dont_write_bytecode = before
    return getattr(module, function)


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON property")
        result[key] = value
    return result


def load(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_pairs)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def items(index, label):
    if not isinstance(index, dict) or type(index.get("revision")) is not int or \
            index["revision"] < 1 or not isinstance(index.get("items"), list):
        raise ValueError(label + " has invalid revision or items")
    found = set()
    for row in index["items"]:
        if not isinstance(row, dict) or not isinstance(row.get("itemId"), str) or \
                not ID.fullmatch(row["itemId"]) or row["itemId"] in found or \
                not isinstance(row.get("platform"), str):
            raise ValueError(label + " has invalid or duplicate itemId")
        revision = row.get("revision", index["revision"])
        if type(revision) is not int or revision < 1:
            raise ValueError(label + " has invalid item revision")
        found.add(row["itemId"])
    return index["items"]


def source_lookup(source_map, candidate):
    by_id = {row["itemId"]: row for row in candidate}
    mapped = source_map.get("items")
    if not isinstance(mapped, list) or len(mapped) != len(candidate):
        raise ValueError("source map does not cover candidate")
    by_path, by_hash = defaultdict(list), defaultdict(list)
    seen = set()
    for row in mapped:
        if not isinstance(row, dict):
            raise ValueError("invalid source map row")
        item_id, platform = row.get("itemId"), row.get("sourcePlatform")
        path, digest = row.get("sourcePath"), row.get("sourceGameSha256")
        if item_id not in by_id or item_id in seen or platform not in ("snes", "megadrive") \
                or not isinstance(path, str) or not Path(path).is_absolute() or \
                not isinstance(digest, str) or not HASH.fullmatch(digest):
            raise ValueError("invalid source map identity")
        if FAMILY.get(by_id[item_id]["platform"]) != platform:
            raise ValueError("source map platform differs from candidate console")
        seen.add(item_id)
        for source in {str(Path(path).resolve()), str(Path(by_id[item_id]["filePath"]).resolve())}:
            by_path[source].append(item_id)
        # Curated archives can differ from their original source archive. Both
        # identities are verified during preparation and belong to the same item.
        for source_hash in {digest, by_id[item_id]["artifact"]["sha256"]}:
            by_hash[(platform, source_hash)].append(item_id)
    return by_id, by_path, by_hash


def private_json(path, content):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as destination:
            json.dump(content, destination, ensure_ascii=False, separators=(",", ":"))
            destination.write("\n")
            destination.flush()
            os.fsync(destination.fileno())
    except BaseException:
        path.unlink(missing_ok=True)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-index", type=Path, required=True)
    parser.add_argument("--candidate-index", type=Path, required=True)
    parser.add_argument("--source-map", type=Path, required=True)
    parser.add_argument("--launch-manifest", type=Path,
                        help="private itemId to launchPath JSON for inherited archives")
    parser.add_argument("--revision", type=int, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    paths = [args.base_index, args.candidate_index, args.source_map]
    if args.launch_manifest:
        paths.append(args.launch_manifest)
    if args.output.resolve() in {path.resolve() for path in paths}:
        parser.error("output must differ from inputs")
    if args.output.resolve().is_relative_to(Path(__file__).resolve().parents[3]):
        parser.error("private output must be outside the repository")
    base, candidate, mapping = load(args.base_index), load(args.candidate_index), \
        load(args.source_map)
    base_items, candidate_items = items(base, "base"), items(candidate, "candidate")
    maximum_revision = max([base["revision"], candidate["revision"]] +
        [row.get("revision", base["revision"]) for row in base_items] +
        [row.get("revision", candidate["revision"]) for row in candidate_items])
    if args.revision <= maximum_revision:
        parser.error("merged revision must exceed all catalog and item revisions")
    manifest = load(args.launch_manifest) if args.launch_manifest else {}
    if not isinstance(manifest, dict):
        parser.error("launch manifest must be an object")
    describe = module_function("preparar-indice-artefatos.py", "describe")
    cover_status = module_function("cruzar-indice-catalogo.py", "index_cover")
    for row in candidate_items:
        item_id = row["itemId"]
        if row["platform"] not in TARGET or not isinstance(row.get("artifact"), dict):
            raise ValueError("candidate is not prepared: " + item_id)
        for key in ("filePath", "coverPath"):
            if not isinstance(row.get(key), str) or not Path(row[key]).is_absolute():
                raise ValueError("candidate has invalid paths: " + item_id)
        if cover_status(Path(row["coverPath"]))[0] != "ok":
            raise ValueError("candidate cover unavailable: " + item_id)
        if describe(row, row["artifact"].get("launchPath")) != row["artifact"]:
            raise ValueError("candidate artifact metadata is stale: " + item_id)
    by_id, by_path, by_hash = source_lookup(mapping, candidate_items)
    output = []
    matched = set()
    preserved = 0
    inherited = 0
    platform_changes = Counter()
    for original in base_items:
        row = dict(original)
        item_id = row["itemId"]
        file_path, cover_path = row.get("filePath"), row.get("coverPath")
        if not isinstance(file_path, str) or not Path(file_path).is_absolute() or \
                not isinstance(cover_path, str) or not Path(cover_path).is_absolute():
            raise ValueError("base item " + item_id + " has invalid paths")
        if row["platform"] in TARGET:
            matches = by_path.get(str(Path(file_path).resolve()), [])
            matches = [key for key in matches if
                       FAMILY[by_id[key]["platform"]] == FAMILY[row["platform"]]]
            if not matches:
                try:
                    matches = by_hash.get((FAMILY[row["platform"]], sha256(Path(file_path))), [])
                except OSError:
                    matches = []
            if len(matches) != 1 or matches[0] in matched:
                raise ValueError("published item has no unique ROM match: " + item_id)
            key = matches[0]
            matched.add(key)
            replacement = dict(by_id[key])
            replacement["itemId"] = item_id
            replacement["revision"] = args.revision
            if row["platform"] != replacement["platform"]:
                platform_changes[row["platform"] + " -> " + replacement["platform"]] += 1
            if Path(cover_path).resolve() == Path(replacement["coverPath"]).resolve():
                replacement["coverId"] = row["coverId"]
            output.append(replacement)
            preserved += 1
        else:
            if not Path(file_path).is_file():
                raise ValueError("inherited game unavailable: " + item_id)
            if cover_status(Path(cover_path))[0] != "ok":
                raise ValueError("inherited cover unavailable: " + item_id)
            try:
                prior = row.get("artifact")
                launch = manifest.get(item_id) or (prior.get("launchPath")
                    if isinstance(prior, dict) else None)
                prepared = describe(row, launch)
                if isinstance(prior, dict) and prior != prepared:
                    raise ValueError("inherited artifact metadata is stale")
                row["artifact"] = prepared
                row.setdefault("revision", base["revision"])
            except (OSError, ValueError, KeyError) as error:
                raise ValueError("inherited artifact not ready: " + item_id) from error
            output.append(row)
            inherited += 1
    for candidate_row in candidate_items:
        if candidate_row["itemId"] not in matched:
            row = dict(candidate_row)
            row["revision"] = args.revision
            output.append(row)
    if len(output) > 4096:
        raise ValueError("merged catalog exceeds 4096 items")
    item_ids = [row["itemId"] for row in output]
    if len(set(item_ids)) != len(item_ids):
        raise ValueError("itemId collision in merged index")
    covers = {}
    for row in output:
        cover_id, identity = row["coverId"], (row["coverPath"], row["revision"])
        if cover_id in covers and covers[cover_id] != identity:
            raise ValueError("coverId collision in merged index")
        covers[cover_id] = identity
    result = {"revision": args.revision, "items": output}
    private_json(args.output, result)
    report = {"revision": args.revision, "total": len(output),
              "preservedPublishedTargetIds": preserved,
              "newTargetItems": len(candidate_items) - preserved,
              "inheritedOtherPlatforms": inherited,
              "platformChangesWithPreservedId": dict(sorted(platform_changes.items())),
              "platformCounts": dict(sorted(Counter(row["platform"] for row in output).items())),
              "indexSha256": sha256(args.output)}
    print(json.dumps(report, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
