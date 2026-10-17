#!/usr/bin/env python3
"""Offline, read-only audit of exact Station catalog/engine/mode bindings.

Reads production exports and a verified app engine manifest/kit. It never
publishes a mode, calls the API, or changes any input. Output is intentionally
sanitized: no URLs, license data, device identifiers or download paths.
"""

import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
from zipfile import ZipFile

sys.dont_write_bytecode = True


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def hash_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def rows(document, field):
    value = document if isinstance(document, list) else document.get(field)
    if not isinstance(value, list):
        raise ValueError(f"expected JSON array '{field}'")
    return value


def load_policy(path):
    spec = importlib.util.spec_from_file_location("station_coverage_policy", path)
    if spec is None or spec.loader is None:
        raise ValueError("cannot load local Station policy module")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def canonical_platform(value, policy):
    raw = str(value).strip().lower()
    # The catalog and APK may use different labels for the same console.
    if raw in {"nes", "nintendinho"}:
        return "nes"
    return policy.platform(raw)


def installed_manifest(args):
    if args.installed_kit:
        with ZipFile(args.installed_kit) as archive:
            if archive.namelist().count("engines-apk.json") != 1:
                raise ValueError("kit must contain one root engines-apk.json")
            return json.loads(archive.read("engines-apk.json")), hash_file(args.installed_kit)
    return load_json(args.installed_engines), hash_file(args.installed_engines)


def reviewed_decisions(document):
    if isinstance(document, list) or "decisions" in document:
        return rows(document, "decisions")
    if "entries" not in document:
        raise ValueError("review ledger lacks decisions or reviewed entries")
    flattened = []
    for entry in rows(document, "entries"):
        if entry.get("reviewState") != "reviewed" or entry.get("editionHeaderVerified") is not True:
            continue
        for mode in rows(entry.get("modes", []), "modes"):
            flattened.append({**mode, "itemId": entry.get("itemId"),
                              "contentSha256": entry.get("contentSha256")})
    return flattened


def validate_inputs(catalog, profiles, modes, app_engines, server_engines, args, policy):
    items = rows(catalog, "items")
    profile_rows = rows(profiles, "profiles")
    mode_rows = rows(modes, "modes")
    app_rows = rows(app_engines, "engines")
    server_rows = rows(server_engines, "engines")
    if len(items) != args.expected_items:
        raise ValueError(f"catalog has {len(items)} items; expected {args.expected_items}")
    if catalog.get("revision") != args.expected_revision:
        raise ValueError("catalog revision differs from required authority")
    ids = [item.get("itemId") for item in items]
    if any(not isinstance(item_id, str) or not item_id for item_id in ids):
        raise ValueError("catalog contains an item without itemId")
    if len(ids) != len(set(ids)):
        raise ValueError("catalog has duplicate itemId values")
    for label, engines in (("installed", app_rows), ("server", server_rows)):
        engine_ids = [engine.get("engineId") for engine in engines]
        if len(engine_ids) != len(set(engine_ids)):
            raise ValueError(f"{label} engine manifest has duplicate IDs")
    # The APK's manifest is a transport declaration: its controllerProfiles
    # are names. The server manifest contains full controller definitions.
    for engine in app_rows:
        if not isinstance(engine, dict) or not isinstance(engine.get("engineId"), str):
            raise ValueError("invalid installed engine declaration")
        if not str(engine.get("platform", "")).strip():
            raise ValueError("invalid installed engine platform")
        if any(not policy.hash_value(engine.get(key)) for key in ("coreSha256", "runtimeSha256")):
            raise ValueError("installed engine identity lacks exact hashes")
        if type(engine.get("launchReady")) is not bool or not isinstance(engine.get("extensions"), list):
            raise ValueError("invalid installed engine readiness or formats")
    # Reuse the server's exact platform aliases and controller hash recipe.
    policy.validate_manifest(server_engines)
    policy.validate_modes({"schemaVersion": 1, "modes": mode_rows})
    return items, profile_rows, mode_rows, app_rows, server_rows


def safe_extension(item):
    artifact = item.get("artifact") or {}
    launch_path = artifact.get("launchPath", "")
    if not isinstance(launch_path, str):
        return ""
    return Path(launch_path).suffix[1:].lower()


def matches_mode(profile, mode, engine_id, policy):
    if mode is None:
        return False
    if mode.get("itemId") != profile.get("itemId") or mode.get("contentSha256") != profile.get("contentSha256"):
        return False
    if canonical_platform(mode.get("platform", ""), policy) != canonical_platform(profile.get("platform", ""), policy):
        return False
    if "engineIds" in mode and engine_id not in mode["engineIds"]:
        return False
    return all(profile.get(key) == mode.get(key) for key in
               ("profileId", "maximumPlayers", "allowedPlayerCounts", "approved",
                "controllerProfile", "mode", "modeTitle"))


def audit_item(item, policy, app_by_platform, server_by_id, profiles_by_item, modes_by_key,
               review_by_item_mode):
    platform = canonical_platform(item.get("platform", ""), policy)
    item_id = item["itemId"]
    content = item.get("contentSha256")
    extension = safe_extension(item)
    installed = app_by_platform.get(platform, [])
    result = {
        "itemId": item_id,
        "name": str(item.get("name", "")),
        "platform": platform,
        "catalogVisible": item.get("catalogVisible", True) is True,
        "launchExtension": extension,
        "contentSha256": content if isinstance(content, str) else None,
        "classification": None,
        "reasons": [],
        "eligibleEngineIds": [],
        "approvedModes": [],
        "pendingModes": [],
        "localModes": [],
    }
    if not isinstance(content, str) or len(content) != 64:
        result["classification"] = "MISSING_CONTENT_IDENTITY"
        result["reasons"] = ["CATALOG_CONTENT_HASH_ABSENT"]
        return result
    if not installed:
        result["classification"] = "NO_INSTALLED_ONLINE_ENGINE"
        result["reasons"] = ["PLATFORM_NOT_DECLARED_IN_APK_ONLINE_MANIFEST"]
        return result
    ready = [engine for engine in installed if engine.get("launchReady") is True
             and engine.get("recoveryProtocol") == "station-stream.v3"]
    if not ready:
        result["classification"] = "INSTALLED_ENGINE_NOT_READY"
        result["reasons"] = ["LAUNCH_OR_STREAM_PROTOCOL_DISABLED"]
        return result
    format_eligible = [engine for engine in ready if extension and extension in engine.get("extensions", [])]
    if not format_eligible:
        result["classification"] = "FORMATO_INCOMPATIVEL"
        result["reasons"] = ["LAUNCH_EXTENSION_NOT_DECLARED_BY_APK_ENGINE"]
        return result
    eligible = []
    for engine in format_eligible:
        server = server_by_id.get(engine["engineId"])
        if server is None:
            result["reasons"].append("SERVER_ENGINE_ID_MISSING")
            continue
        if (canonical_platform(server.get("platform", ""), policy) != platform
                or any(server.get(key) != engine.get(key) for key in
                       ("coreSha256", "runtimeSha256", "recoveryProtocol"))):
            result["reasons"].append("SERVER_ENGINE_IDENTITY_MISMATCH")
            continue
        if server.get("launchReady") is not True:
            result["reasons"].append("SERVER_ENGINE_NOT_READY")
            continue
        if extension not in server.get("extensions", []):
            result["reasons"].append("SERVER_FORMAT_UNSUPPORTED")
            continue
        eligible.append((engine, server))
    if not eligible:
        result["classification"] = "SERVER_ENGINE_UNAVAILABLE"
        result["reasons"] = sorted(set(result["reasons"]))
        return result
    result["eligibleEngineIds"] = [engine["engineId"] for engine, _ in eligible]
    item_profiles = profiles_by_item.get(item_id, [])
    exact_profiles = []
    for engine, server in eligible:
        for profile in item_profiles:
            if any(profile.get(key) != expected for key, expected in (
                ("contentSha256", content), ("engineId", engine["engineId"]),
                ("coreSha256", engine["coreSha256"]), ("runtimeSha256", engine["runtimeSha256"]))):
                continue
            config = policy.profile_configuration(server, profile.get("controllerProfile"),
                                                  profile.get("maximumPlayers"))
            if config is None:
                result["reasons"].append("CONTROLLER_CONFIGURATION_UNAVAILABLE")
                continue
            if policy.profile_digest(config) != profile.get("profileSha256"):
                result["reasons"].append("PROFILE_FINGERPRINT_MISMATCH")
                continue
            exact_profiles.append((engine, profile))
    if not exact_profiles:
        if any(profile.get("contentSha256") == content for profile in item_profiles):
            result["reasons"].append("CURRENT_ENGINE_REVISION_UNBOUND")
        elif item_profiles:
            result["reasons"].append("CONTENT_REVISION_UNBOUND")
        else:
            result["reasons"].append("NO_PROFILE_FOR_EDITION")
        result["classification"] = "NO_EXACT_PROFILE"
        result["reasons"] = sorted(set(result["reasons"]))
        return result
    for engine, profile in exact_profiles:
        key = (item_id, content, profile.get("profileId"))
        mode = modes_by_key.get(key)
        mode_matches = matches_mode(profile, mode, engine["engineId"], policy)
        # 'documented' is not a claim of Android gameplay. It requires an
        # explicit current-engine mode with cited edition-specific evidence.
        documented = bool(mode_matches and mode.get("engineIds")
                          and engine["engineId"] in mode["engineIds"]
                          and mode.get("sources"))
        review = review_by_item_mode.get((item_id, content, profile.get("mode"),
                                          profile.get("maximumPlayers")), {})
        reviewed = bool(documented and review.get("approved") is True
                        and review.get("classificationOnly") is not True
                        and review.get("allowedPlayerCounts") == profile.get("allowedPlayerCounts")
                        and review.get("controllerProfile") == profile.get("controllerProfile")
                        and review.get("sources"))
        entry = {
            "engineId": engine["engineId"],
            "profileId": profile.get("profileId"),
            "mode": profile.get("mode"),
            "maximumPlayers": profile.get("maximumPlayers"),
            "allowedPlayerCounts": profile.get("allowedPlayerCounts", []),
            "documentedExactMode": documented,
            "reviewedGameModeEvidence": reviewed,
            "genericAutoProfile": (profile.get("profileId") in {"standard-2p-v1", "local-auto-2p-v1"}
                                   or str(profile.get("profileId", "")).startswith("local-auto-")
                                   or not mode_matches),
            "physicalAndroidGameplayValidated": profile.get("physicalAndroidGameplayValidated") is True,
        }
        if profile.get("approved") is not True:
            findings = (mode or {}).get("blockingFindings") or profile.get("blockingFindings") or review.get("blockingFindings", [])
            entry["pendingReasonCodes"] = [value for value in findings
                                           if isinstance(value, str) and value.replace("-", "").replace("_", "").isalnum()]
        multiplayer = (isinstance(profile.get("maximumPlayers"), int)
                       and profile["maximumPlayers"] >= 2
                       and isinstance(profile.get("allowedPlayerCounts"), list)
                       and bool(profile["allowedPlayerCounts"]))
        if multiplayer and profile.get("approved") is True:
            result["approvedModes"].append(entry)
        elif multiplayer:
            result["pendingModes"].append(entry)
        else:
            result["localModes"].append(entry)
    if result["approvedModes"]:
        if any(mode["reviewedGameModeEvidence"] for mode in result["approvedModes"]):
            result["classification"] = "REVIEWED_MULTIPLAYER_APPROVED"
            if any(not mode["reviewedGameModeEvidence"] for mode in result["approvedModes"]):
                result["reasons"].append("OTHER_APPROVED_MODES_LACK_GAME_PROOF")
        else:
            result["classification"] = "REGISTRY_APPROVED_WITHOUT_GAME_PROOF"
            result["reasons"].append("GAME_SPECIFIC_REVIEW_NOT_PROVEN")
    elif result["pendingModes"]:
        result["classification"] = "MULTIPLAYER_REVIEW_PENDING"
        result["reasons"].append("MODE_UNAPPROVED")
    elif result["localModes"]:
        result["classification"] = "LOCAL_ONLY"
    else:
        result["classification"] = "NO_EXACT_PROFILE"
    result["reasons"] = sorted(set(result["reasons"]))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("catalog", "profiles", "modes", "server-engines", "policy-module", "output-dir"):
        parser.add_argument("--" + name, required=True, type=Path)
    installed = parser.add_mutually_exclusive_group(required=True)
    installed.add_argument("--installed-engines", type=Path)
    installed.add_argument("--installed-kit", type=Path)
    parser.add_argument("--expected-items", type=int, default=4744)
    parser.add_argument("--expected-revision", type=int, default=100)
    parser.add_argument("--review-ledger", type=Path, action="append", default=[],
                        help="repeat for each immutable per-edition review ledger")
    args = parser.parse_args()
    paths = [args.catalog, args.profiles, args.modes, args.server_engines,
             args.policy_module, args.installed_kit or args.installed_engines]
    paths.extend(args.review_ledger)
    if any(not path.is_file() for path in paths):
        parser.error("all input files must exist")
    app_manifest, app_hash = installed_manifest(args)
    catalog = load_json(args.catalog)
    profiles = load_json(args.profiles)
    modes = load_json(args.modes)
    server_manifest = load_json(args.server_engines)
    policy = load_policy(args.policy_module)
    items, profile_rows, mode_rows, app_rows, server_rows = validate_inputs(
        catalog, profiles, modes, app_manifest, server_manifest, args, policy)
    app_by_platform = defaultdict(list)
    for engine in app_rows:
        app_by_platform[canonical_platform(engine["platform"], policy)].append(engine)
    server_by_id = {engine["engineId"]: engine for engine in server_rows}
    profiles_by_item = defaultdict(list)
    for profile in profile_rows:
        profiles_by_item[profile.get("itemId")].append(profile)
    modes_by_key = {(mode.get("itemId"), mode.get("contentSha256"), mode.get("profileId")): mode
                    for mode in mode_rows}
    review_by_item_mode = {}
    for ledger_path in args.review_ledger:
        for review in reviewed_decisions(load_json(ledger_path)):
            key = (review.get("itemId"), review.get("contentSha256"), review.get("mode"),
                   review.get("maximumPlayers"))
            if key in review_by_item_mode:
                raise ValueError("review ledgers have an ambiguous edition/mode")
            review_by_item_mode[key] = review
    results = [audit_item(item, policy, app_by_platform, server_by_id,
                          profiles_by_item, modes_by_key, review_by_item_mode) for item in items]
    classifications = Counter(row["classification"] for row in results)
    platforms = {}
    for name in sorted({row["platform"] for row in results}):
        subset = [row for row in results if row["platform"] == name]
        platforms[name] = {"items": len(subset),
                           "visible": sum(row["catalogVisible"] for row in subset),
                           "classifications": dict(sorted(Counter(row["classification"] for row in subset).items()))}
    summary = {
        "schema": "station.readonly-coverage-audit.v1",
        "mutatedServerState": False,
        "catalogRevision": catalog["revision"],
        "catalogItems": len(items),
        "visibleItems": sum(row["catalogVisible"] for row in results),
        "installedEngines": len(app_rows),
        "serverEngines": len(server_rows),
        "profilesRead": len(profile_rows),
        "modesRead": len(mode_rows),
        "classificationCounts": dict(sorted(classifications.items())),
        "actionableGapEditions": sum(row["classification"] not in {"REVIEWED_MULTIPLAYER_APPROVED", "LOCAL_ONLY"}
                                     or bool(row["reasons"]) for row in results),
        "exactModeSourceApprovedEditions": sum(any(mode["documentedExactMode"] for mode in row["approvedModes"])
                                               for row in results),
        "reviewedEvidenceApprovedEditions": sum(row["classification"] == "REVIEWED_MULTIPLAYER_APPROVED"
                                                for row in results),
        "registryApprovedUnverifiedEditions": sum(row["classification"] == "REGISTRY_APPROVED_WITHOUT_GAME_PROOF"
                                                  for row in results),
        "physicalGameplayValidatedEditions": sum(any(mode["physicalAndroidGameplayValidated"]
                                                   for mode in row["approvedModes"]) for row in results),
        "byPlatform": platforms,
        "inputSha256": {
            "catalog": hash_file(args.catalog), "profiles": hash_file(args.profiles),
            "modes": hash_file(args.modes), "serverEngines": hash_file(args.server_engines),
            "installedEngineSource": app_hash,
            **({"reviewLedgers": [hash_file(path) for path in args.review_ledger]} if args.review_ledger else {}),
        },
    }
    output = args.output_dir.resolve()
    if output.exists():
        raise ValueError("output directory already exists; choose a new audit run directory")
    output.mkdir(parents=True)
    (output / "coverage-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (output / "edition-status.jsonl").open("w", encoding="utf-8") as stream:
        for row in results:
            stream.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    with (output / "actionable-gaps.tsv").open("w", encoding="utf-8") as stream:
        stream.write("itemId\tname\tplatform\tclassification\treasons\n")
        for row in results:
            if row["classification"] in {"REVIEWED_MULTIPLAYER_APPROVED", "LOCAL_ONLY"} and not row["reasons"]:
                continue
            safe_name = row["name"].replace("\t", " ").replace("\r", " ").replace("\n", " ")
            stream.write("\t".join((row["itemId"], safe_name, row["platform"],
                                    row["classification"], ",".join(row["reasons"]))) + "\n")
    print(json.dumps({"catalogItems": len(items), "classifications": summary["classificationCounts"],
                      "outputDirectory": str(output)}, ensure_ascii=False))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"audit refused: {error}", file=sys.stderr)
        sys.exit(2)
