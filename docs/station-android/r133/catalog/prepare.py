"""Read exact server baseline; prepare game-mode decisions for a supplied final owner receipt.

No server publication, APK change, game bytes, credentials or physical gameplay.
The runtime receipt is a parameter so a later final bundle does not inherit old engine IDs.
"""
import argparse
import collections
import copy
import csv
import hashlib
import io
import json
import subprocess
import sys
import types
from pathlib import Path
from urllib.parse import urlsplit

SERVER_COMMIT = "a6b58e9beb12eb564c1b58d9d4c32ed3e7513861"
DATA = "docs/station-android/online-plataformas-20261009/"
TOOLS = "docs/station-android/r128/operator/"
FILES = {
    "catalog": DATA + "catalogo-cruzado-completo.json",
    "profiles": DATA + "profiles-complete.json",
    "modes": DATA + "server-prepared-modes.json",
    "content": DATA + "content-identities.json",
    "return": DATA + "r128-retorno-servidor-20261011.json",
    "incoming": TOOLS + "build_incoming_manifest.py",
    "merge": TOOLS + "merge_scoped_modes.py",
    "operator": DATA + "library-tools/station_online_profiles.py",
}
PINNED_INPUT_SHA256 = {'catalog': '22499d9d7f8c9daabe9d7d859cca60e2f59e0e2c233cae4eb4bb32e4ff4375a1', 'profiles': 'a029fb61ce68fa26abadf0312b55b9ae163c6a3604a12bfd37e97d406fdfebaa', 'modes': 'd7c7ccf3c6a10e6f1fbce8475baacbbca83cd1e7f325ad8b24ab42a0fa6b78a6', 'content': '4c926082fc7e61d43146959e33681979f8100bbe9f832ffd218f83218127df9f', 'return': '13ec0dda1a8bc938c8453ee1b3b707699fb6738c70034828fc37cdca1ddae9c9', 'incoming': '362bcae9d8915f7f43aac4c48d06b35bdf57efc88d6acb9199aa06e3a0231426', 'merge': 'ddac598ca1671e6d682b626866a3b1984eb88c5ff7d677c7b4cd563a6a677783', 'operator': '3d5d5b5563e2a6825ab9e798da80313cdbf2640522bc4dfd6b47e25c7f69eae1'}
PRIVATE_INPUT_NAMES = {'catalog': 'catalog.json', 'profiles': 'profiles-complete.json', 'modes': 'original-prepared-modes.json', 'content': 'content-identities.json', 'return': 'server-return.json', 'incoming': 'server-build_incoming_manifest.py', 'merge': 'server-merge_scoped_modes.py', 'operator': 'server-station_online_profiles.py'}

# These are aliases of the same four independent native input ports. Special
# shared-controller/GBA-link/Co-Star profiles are intentionally not aliases.
PORT_ALIASES = {
    "n64": {"direct-four-ports-v1"},
    "gamecube": {"gamecube-four-ports-v1", "dolphin-gamecube-four-ports-v1"},
    "wii": {"wii-four-remotes-v1", "dolphin-wii-remotes-four-v1"},
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def script_module(name, data):
    module = types.ModuleType(name)
    module.__file__ = name + ".py"
    sys.modules[name] = module
    exec(compile(data, module.__file__, "exec"), module.__dict__)
    return module


def game_specific(sources):
    # The existing generic repository policy is useful provenance, but does not
    # prove a particular game's simultaneous mode, HLE support or player count.
    return bool(sources) and all(urlsplit(s).scheme == "https" and urlsplit(s).hostname
                                 and urlsplit(s).hostname != "github.com" for s in sources)


def canonical_for_client(profile, engine):
    name, maximum, platform = profile["controllerProfile"], profile["maximumPlayers"], engine["platform"]
    options = engine["options"]
    if name == "standard-2p-v1" and maximum <= 2:
        devices = [1, 1]
    elif name == "direct-four-ports-v1" and platform == "n64" and maximum <= 4:
        devices = [1, 1, 1, 1]
    elif name == "dolphin-gamecube-four-ports-v1" and platform == "gamecube" and maximum <= 4:
        devices = [1, 1, 1, 1]
    elif name == "dolphin-wii-remotes-four-v1" and platform == "wii" and maximum <= 4:
        devices = [1, 1, 1, 1]
    elif name == "psx-dualshock-2p-v1" and platform == "psx" and maximum <= 2:
        devices = [517, 517]
    elif name == "snes-multitap-port2-v1" and platform == "snes":
        devices = [1, 257, 1, 1, 1]
    elif name == "megadrive-sega-teamplayer-v1" and platform == "megadrive" and maximum <= 4 and not options:
        devices, options = [1] * 8, 'clownmdemu_input_protocol = "sega"\n'
    elif name == "megadrive-ea-4way-v1" and platform == "megadrive" and maximum <= 4 and not options:
        devices, options = [1] * 4, 'clownmdemu_input_protocol = "ea"\n'
    else:
        return None
    value = dict(schemaVersion=1, controllerProfile=name, devices=devices, coreOptions=options)
    return sha(json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    server_input=parser.add_mutually_exclusive_group(required=True)
    server_input.add_argument("--server-repo",type=Path)
    server_input.add_argument("--server-input-dir",type=Path,help="Exact eight raw blobs preserved by the private receipt; every SHA is pinned to the stated server commit")
    parser.add_argument("--runtime-receipt", type=Path, required=True)
    parser.add_argument("--local-engines", type=Path, required=True)
    parser.add_argument("--audit-dir", type=Path, required=True)
    parser.add_argument("--private-dir", type=Path, required=True)
    parser.add_argument("--documentary-decisions", type=Path, action="append", default=[],
                        help="Primary per-game research, bound by exact catalog ordinal/name/platform; repeatable")
    args = parser.parse_args()
    sys.dont_write_bytecode = True
    def raw(key,path):
        if args.server_input_dir is not None:
            value=(args.server_input_dir / PRIVATE_INPUT_NAMES[key]).read_bytes()
        else:
            value=subprocess.check_output(["git","-c","safe.directory="+args.server_repo.resolve().as_posix(),
                                           "-C",str(args.server_repo),"show",SERVER_COMMIT+":"+path])
        if sha(value)!=PINNED_INPUT_SHA256[key]:
            raise ValueError("Raw server input differs from the exact pinned commit: "+key)
        return value
    original={key:raw(key,path) for key,path in FILES.items()}
    data = {key: json.loads(value) for key, value in original.items() if key not in {"incoming", "merge", "operator"}}
    incoming = script_module("build_incoming_manifest", original["incoming"])
    official_merge = script_module("r132_readonly_mode_merge", original["merge"])
    operator = script_module("r132_readonly_profile_operator", original["operator"])
    runtime_bytes, engine_bytes = args.runtime_receipt.read_bytes(), args.local_engines.read_bytes()
    runtime, local = json.loads(runtime_bytes), json.loads(engine_bytes)
    final_manifest, policies = incoming.incoming(runtime)
    final_engines = {e["platform"]: e for e in final_manifest["engines"]}
    active = {}
    for engine in local["engines"]:
        if engine.get("launchReady") is True and engine.get("recoveryProtocol") == "station-stream.v3":
            active.setdefault(engine["platform"], engine)
    if any(active.get(platform, {}).get("engineId") != engine["engineId"] for platform, engine in final_engines.items()):
        raise ValueError("Local asset and supplied final runtime identify different engines")
    catalog, existing, old_modes = data["catalog"], data["profiles"], data["modes"]
    by_item = {row["itemId"]: row for row in catalog["items"]}
    if len(by_item) != len(catalog["items"]):
        raise ValueError("Catalog item identity is not unique")
    content = {row["itemId"]: row for row in data["content"]["entries"]}
    if any(content.get(row["itemId"], {}).get("contentSha256") != row.get("contentSha256") for row in catalog["items"]):
        raise ValueError("Catalog and original content identity registry differ")
    # The helper's pure function is the same official admission preparation used
    # by the operator. No .main() or write/publication function is executed.
    _, automatic = official_merge.merge(operator, catalog, existing, old_modes, runtime)
    sources = []
    for index, row in enumerate(existing):
        if row.get("platform") in PORT_ALIASES:
            sources.append(("profiles-complete.json", index + 1, row))
    for index, row in enumerate(old_modes["modes"]):
        if row.get("platform") in PORT_ALIASES:
            sources.append(("server-prepared-modes.json", index + 1, row))
    decisions, proposed, decisions_refs, refusals, seen = [], [], [], [], set()
    for filename, ordinal, source in sources:
        item = by_item.get(source["itemId"])
        platform = operator.platform(source.get("platform", ""))
        if item is None or not item.get("catalogVisible", True):
            continue
        key = (source["itemId"], source["contentSha256"], source["profileId"])
        if key in seen:
            continue
        seen.add(key)
        reason = None
        if source["contentSha256"] != item.get("contentSha256"):
            reason = "catalog-content-binding-mismatch"
        elif source.get("approved") is not True:
            reason = "previous-mode-not-approved"
        elif source["controllerProfile"] not in PORT_ALIASES[platform]:
            reason = "special-controller-profile-not-implemented"
        elif source["maximumPlayers"] > policies[platform]["maximumPlayers"]:
            reason = "game-mode-exceeds-implemented-native-ports"
        elif Path(item["artifact"]["launchPath"]).suffix[1:].lower() not in final_engines[platform]["extensions"]:
            reason = "launch-extension-unsupported"
        record = {k: copy.deepcopy(source[k]) for k in official_merge.FIELDS}
        record.update(itemId=item["itemId"], contentSha256=item["contentSha256"], platform=platform,
                      controllerProfile=policies[platform]["controllerProfile"])
        provenance = dict(catalogOrdinal=catalog["items"].index(item) + 1, name=item["name"], platform=platform,
                          sourceFile=filename, sourceOrdinal=ordinal, originalControllerProfile=source["controllerProfile"],
                          targetControllerProfile=record["controllerProfile"], sources=source.get("sources", []),
                          maximumPlayers=source["maximumPlayers"], allowedPlayerCounts=source["allowedPlayerCounts"],
                          engineIdentityDerivedFromSuppliedFinalReceipt=True, physicalGameplayValidated=False)
        if reason is not None:
            refusals.append(dict(provenance, reason=reason));continue
        if not game_specific(source.get("sources", [])):
            proposed.append(dict(mode=record, provenance=provenance,
                                 state="existing-test-policy-only", reason="per-game-mode-evidence-pending",
                                 applied=False, approvedAsActualGameSupport=False));continue
        decisions.append(record);decisions_refs.append(provenance)
    research_by_item, research_inputs = {}, []
    decision_keys = {(r["itemId"], r["profileId"]) for r in decisions}
    for research_path in args.documentary_decisions:
        research_bytes = research_path.read_bytes()
        research = json.loads(research_bytes)
        if research.get("schemaVersion") != 1 or not isinstance(research.get("entries"), list):
            raise ValueError("Documentary research schema must be 1 with entries")
        research_inputs.append(dict(file=research_path.name, sha256=sha(research_bytes), entries=len(research["entries"])))
        for entry in research["entries"]:
            ordinal = entry["catalogOrdinal"]
            if type(ordinal) is not int or not 1 <= ordinal <= len(catalog["items"]):
                raise ValueError("Research ordinal is outside the exact catalog")
            item = catalog["items"][ordinal - 1]
            platform = operator.platform(item["platform"])
            if entry["name"] != item["name"] or entry["platform"] != platform:
                raise ValueError("Research title/platform differ from exact catalog ordinal")
            if item["itemId"] in research_by_item:
                raise ValueError("Duplicate documentary research for a catalog item")
            if entry["classification"] not in {"simultaneous", "alternating-independent-ports", "single-player", "unsupported-special-controller", "pending-primary-evidence"}:
                raise ValueError("Unsupported documentary classification")
            documentary_counts = entry["documentarySimultaneousCounts"]
            if (not isinstance(documentary_counts, list) or documentary_counts != sorted(set(documentary_counts))
                    or any(type(n) is not int or n < 2 or n > 8 for n in documentary_counts)):
                raise ValueError("Documentary counts must be explicit unique simultaneous seats")
            if entry["classification"] == "single-player" and documentary_counts:
                raise ValueError("Single-player finding cannot contain simultaneous counts")
            alternating_counts = entry.get("documentaryAlternatingIndependentPortCounts", [])
            if (not isinstance(alternating_counts, list) or alternating_counts != sorted(set(alternating_counts))
                    or any(type(n) is not int or n < 2 or n > 4 for n in alternating_counts)):
                raise ValueError("Alternating counts require explicit unique independent human input ports")
            if entry["classification"] == "alternating-independent-ports":
                if documentary_counts or not alternating_counts or platform == "psx":
                    raise ValueError("Alternating native modes must not claim simultaneous seats or waive PSX HLE requirements")
            elif alternating_counts:
                raise ValueError("Alternating evidence must use its explicit documentary classification")
            if entry["classification"] != "pending-primary-evidence" and not game_specific(entry["primarySources"]):
                raise ValueError("A documentary conclusion requires specific primary HTTPS sources")
            if not entry.get("finding") or not isinstance(entry["finding"], str):
                raise ValueError("Each documentary classification requires a bounded finding")
            research_by_item[item["itemId"]] = entry
            for mode in entry.get("modes", []):
                if entry["classification"] not in {"simultaneous", "alternating-independent-ports"} or platform not in final_engines:
                    raise ValueError("Actionable modes require a documented native-owner input-compatible game")
                if not item.get("catalogVisible", True) or not operator.hash_value(item.get("contentSha256")):
                    raise ValueError("Actionable mode requires a visible content-bound catalog item")
                if mode.get("adapterCompatible") is not True:
                    raise ValueError("A documentary mode must state compatible implemented input ports")
                participation = mode.get("participation", entry["classification"])
                if participation not in {"simultaneous", "alternating-independent-ports"} or (participation == "alternating-independent-ports" and platform == "psx"):
                    raise ValueError("Mode participation must describe supported native independent ports without waiving PSX HLE review")
                source_urls = mode.get("sources", entry["primarySources"])
                if not game_specific(source_urls):
                    raise ValueError("No generic repository link can approve a game mode")
                requirements = mode.get("requiredEngineOptions", [])
                if any(line not in final_engines[platform]["options"].splitlines() for line in requirements):
                    raise ValueError("The final owner does not meet a documented mandatory engine option")
                record = {k: copy.deepcopy(mode[k]) for k in official_merge.FIELDS
                          if k not in {"approved", "controllerProfile", "sources"}}
                record.update(itemId=item["itemId"], contentSha256=item["contentSha256"], platform=platform,
                              approved=True, controllerProfile=policies[platform]["controllerProfile"], sources=source_urls)
                documented_mode_counts = alternating_counts if entry["classification"] == "alternating-independent-ports" else documentary_counts
                if not set(record["allowedPlayerCounts"]).issubset(documented_mode_counts):
                    raise ValueError("Room counts exceed documentary mode and independent-port evidence")
                if record["maximumPlayers"] > policies[platform]["maximumPlayers"]:
                    raise ValueError("Mode exceeds implemented native ports")
                if Path(item["artifact"]["launchPath"]).suffix[1:].lower() not in final_engines[platform]["extensions"]:
                    raise ValueError("Mode's actual launch extension is not implemented")
                key = (item["itemId"], record["profileId"])
                if key in decision_keys:
                    raise ValueError("Duplicate reviewed mode identity")
                decision_keys.add(key);decisions.append(record)
                decisions_refs.append(dict(catalogOrdinal=ordinal, name=item["name"], platform=platform,
                    sourceFile=research_path.name, sourceOrdinal=research["entries"].index(entry) + 1,
                    sources=source_urls, finding=entry["finding"], mode=record["mode"], modeTitle=record["modeTitle"],
                    maximumPlayers=record["maximumPlayers"], allowedPlayerCounts=record["allowedPlayerCounts"],
                    participation=participation,
                    targetControllerProfile=record["controllerProfile"], requiredEngineOptions=requirements,
                    configuredInputCondition=mode.get("configuredInputCondition", ""),
                    engineIdentityDerivedFromSuppliedFinalReceipt=True, physicalGameplayValidated=False))
    ledger = dict(schemaVersion=1, modes=decisions)
    merged, receipt = official_merge.merge(operator, catalog, existing, old_modes, runtime, ledger)
    preview = operator.prepare(catalog["items"], existing, final_manifest, merged)
    new_profiles = preview[len(existing):]
    if receipt["previewProfilesAdded"] != len(new_profiles) or len(new_profiles) != len(decisions):
        raise ValueError("Each reviewed decision must produce exactly one final owner profile")
    if not all(p["approved"] and p["maximumPlayers"] >= 2 for p in new_profiles):
        raise ValueError("Actionable room decisions must be documented modes with independent human input ports")
    profile_by_item = collections.defaultdict(list)
    for index, profile in enumerate(existing):
        profile_by_item[profile["itemId"]].append((index + 1, profile))
    prepared_by_item = collections.defaultdict(list)
    for index, mode in enumerate(old_modes["modes"]):
        prepared_by_item[mode["itemId"]].append((index + 1, mode))
    reviewed = collections.defaultdict(list)
    for profile in new_profiles:
        reviewed[profile["itemId"]].append(profile)
    proposed_ids = {p["mode"]["itemId"] for p in proposed}
    refused_ids = {catalog["items"][p["catalogOrdinal"] - 1]["itemId"]: p["reason"] for p in refusals}
    documented_modes = []
    for filename, rows in (("profiles-complete.json", existing), ("server-prepared-modes.json", old_modes["modes"])):
        for source_ordinal, source in enumerate(rows, 1):
            if not game_specific(source.get("sources", [])):
                continue
            item = by_item.get(source["itemId"])
            if item is None or source["contentSha256"] != item.get("contentSha256"):
                continue
            documented_modes.append(dict(
                catalogOrdinal=catalog["items"].index(item) + 1, name=item["name"],
                platform=operator.platform(item["platform"]),
                sourceFile=filename, sourceOrdinal=source_ordinal,
                maximumPlayers=source["maximumPlayers"],
                allowedPlayerCounts=source["allowedPlayerCounts"],
                controllerProfile=source["controllerProfile"],
                mode=source.get("mode", ""), modeTitle=source.get("modeTitle", ""),
                instructions=source.get("instructions", ""),
                approvedInHistoricalSource=source.get("approved") is True,
                sources=source["sources"], operatorApplied=False,
                physicalGameplayValidated=False))
    for entry in research_by_item.values():
        for mode in entry.get("modes", []):
            documented_modes.append(dict(catalogOrdinal=entry["catalogOrdinal"], name=entry["name"], platform=entry["platform"],
                sourceFile="documentary-research", maximumPlayers=mode["maximumPlayers"],
                allowedPlayerCounts=mode["allowedPlayerCounts"], controllerProfile=policies[entry["platform"]]["controllerProfile"],
                mode=mode["mode"], modeTitle=mode["modeTitle"], instructions=mode["instructions"],
                approvedInHistoricalSource=False, documentaryReviewPrepared=True,
                sources=mode.get("sources",entry["primarySources"]), operatorApplied=False, physicalGameplayValidated=False))
    matrix = []
    for ordinal, row in enumerate(catalog["items"], 1):
        item_id, platform = row["itemId"], operator.platform(row["platform"])
        engine = active.get(platform)
        previous = profile_by_item[item_id]
        compatible = []
        canonical_rejected = False
        for _, p in previous:
            if not engine or p.get("approved") is not True or p["contentSha256"] != row.get("contentSha256"):
                continue
            if any(p[field] != engine[field] for field in ("engineId", "coreSha256", "runtimeSha256")):
                continue
            if canonical_for_client(p, engine) != p["profileSha256"]:
                canonical_rejected = True;continue
            if p["maximumPlayers"] > operator.CEILINGS.get(platform, 0):
                canonical_rejected = True;continue
            compatible.append(p)
        counts = sorted({n for p in compatible for n in p["allowedPlayerCounts"]})
        historical_counts = sorted({n for _, p in previous if p.get("approved") for n in p["allowedPlayerCounts"]})
        prior_modes = prepared_by_item[item_id]
        research = research_by_item.get(item_id)
        if not row.get("catalogVisible", True):
            state, reason = "catalog-alias", "hidden-alias-not-a-separate-visible-selection"
        elif item_id in reviewed:
            state, reason = "reviewed-final-owner-binding-prepared", "operator-publication-and-fresh-signed-confirmation-required"
        elif platform == "psx":
            state, reason = "psx-hle-per-edition-review-pending", "no-production-per-edition-HLE-review-ledger-in-server-return"
        elif research and research["classification"] == "single-player":
            state, reason = "documented-single-player-no-online-room", "primary-game-source-identifies-single-player-mode-only"
        elif research and research["classification"] in {"unsupported-special-controller", "pending-primary-evidence"}:
            state, reason = "documentary-mode-blocked", research["reason"]
        elif item_id in refused_ids:
            state, reason = "native-mode-refused", refused_ids[item_id]
        elif item_id in proposed_ids:
            state, reason = "existing-test-policy-pending-game-review", "generic-policy-is-not-per-game-simultaneous-mode-evidence"
        elif counts:
            state, reason = "baseline-current-engine-profile-compatible", "historical-published-binding-matches-current-asset-fresh-production-not-checked"
        elif compatible:
            state, reason = "baseline-single-player-classification", "no-approved-simultaneous-online-seats"
        elif engine is None:
            state, reason = "no-current-client-online-engine", "server-platform-policy-does-not-integrate-an-Android-owner"
        elif Path(row["artifact"]["launchPath"]).suffix[1:].lower() not in engine["extensions"]:
            state, reason = "launch-format-pending", "installed-format-not-in-current-online-engine-extension-allowlist"
        elif canonical_rejected:
            state, reason = "controller-canonical-pending", "existing-profile-does-not-match-current-controller-or-platform-policy"
        else:
            state, reason = "no-approved-current-edition-mode-binding", "catalog-content-engine-runtime-profile-binding-pending"
        documentary_rows = [p for _, p in previous + prior_modes if game_specific(p.get("sources", []))]
        documentary_counts = sorted({n for p in documentary_rows if p.get("approved") is True for n in p["allowedPlayerCounts"]})
        if research:
            documentary_counts = sorted(set(documentary_counts) | set(research["documentarySimultaneousCounts"]))
        prepared_counts = sorted({n for p in reviewed.get(item_id, []) for n in p["allowedPlayerCounts"]})
        matrix.append(dict(catalogOrdinal=ordinal, name=row["name"], catalogPlatform=row["platform"], platform=platform,
                           visible=bool(row.get("catalogVisible", True)), state=state, reason=reason,
                           descriptivePlayerLabel=row.get("metadata", {}).get("players", ""),
                           historicalApprovedCounts=historical_counts,
                           documentarySimultaneousCounts=documentary_counts,
                           documentaryAlternatingIndependentPortCounts=research.get("documentaryAlternatingIndependentPortCounts", []) if research else [],
                           currentBundleCompatibleCounts=counts,
                           preparedFinalOwnerCounts=prepared_counts,
                           preparedFinalOwnerModes=[dict(mode=p["mode"], title=p["modeTitle"], allowedPlayerCounts=p["allowedPlayerCounts"])
                                                    for p in reviewed.get(item_id, [])],
                           currentEngineId=engine.get("engineId", "") if engine else "",
                           contentIdentityAvailable=bool(operator.hash_value(row.get("contentSha256"))),
                           gameSpecificModeEvidence=bool(documentary_rows or research and research["classification"] != "pending-primary-evidence"),
                           documentaryFinding=research["finding"] if research else "",
                           documentarySources=research["primarySources"] if research else [],
                           sourceReferences=[dict(file="profiles-complete.json", record=n) for n, _ in previous]
                                            + [dict(file="server-prepared-modes.json", record=n) for n, _ in prior_modes],
                           physicalGameplayValidated=False))
    # This is a review proposal, not an importable approval ledger. Keep all
    # generic historical policies, including offline/unsupported owners, so the
    # operator can see every gap without silently dropping whole platforms.
    all_existing_test_proposals = []
    for filename, rows in (("profiles-complete.json", existing), ("server-prepared-modes.json", old_modes["modes"])):
        for source_ordinal, source in enumerate(rows, 1):
            if game_specific(source.get("sources", [])):
                continue
            item = by_item.get(source["itemId"])
            system = operator.platform(source.get("platform", ""))
            engine = active.get(system)
            target = final_engines.get(system)
            original_decision = {key: copy.deepcopy(source[key]) for key in official_merge.FIELDS if key in source}
            original_decision.update(itemId=source["itemId"], contentSha256=source["contentSha256"], platform=system)
            binding_matches = bool(engine and item and source.get("approved") is True
                                   and source.get("contentSha256") == item.get("contentSha256")
                                   and all(source.get(field) == engine[field] for field in ("engineId", "coreSha256", "runtimeSha256"))
                                   and canonical_for_client(source, engine) == source.get("profileSha256")
                                   and source["maximumPlayers"] <= operator.CEILINGS.get(system, 0))
            review_candidate = None
            if item is None or source["contentSha256"] != item.get("contentSha256"):
                proposal_reason = "catalog-content-binding-mismatch"
            elif system == "psx":
                proposal_reason = "PSX-HLE-per-edition-evidence-required-no-legacy-approval-migration"
            elif not engine:
                proposal_reason = "no-current-client-online-engine"
            elif target:
                if source["controllerProfile"] not in PORT_ALIASES[system]:
                    proposal_reason = "special-controller-profile-not-implemented"
                elif source["maximumPlayers"] > policies[system]["maximumPlayers"]:
                    proposal_reason = "game-mode-exceeds-implemented-native-ports"
                else:
                    proposal_reason = "existing-test-policy-requires-explicit-game-review-before-new-owner-binding"
                    review_candidate = copy.deepcopy(original_decision)
                    review_candidate["controllerProfile"] = policies[system]["controllerProfile"]
                    review_candidate["approved"] = False
            elif binding_matches:
                proposal_reason = "historical-binding-matches-current-owner-no-new-mode-inferred"
            else:
                proposal_reason = "historical-binding-does-not-match-current-owner-policy"
            all_existing_test_proposals.append(dict(
                sourceFile=filename, sourceOrdinal=source_ordinal,
                catalogOrdinal=catalog["items"].index(item) + 1 if item else None,
                name=item["name"] if item else "", platform=system,
                originalDecision=original_decision,
                currentEngineId=engine["engineId"] if engine else None,
                existingBindingMatchesCurrentOwner=binding_matches,
                reviewCandidate=review_candidate, reason=proposal_reason,
                genericPolicyIsGameSpecificEvidence=False,
                published=False, approvedAsActualGameSupport=False,
                reviewOnly=True, physicalGameplayValidated=False))
    by_platform = []
    for platform in sorted({m["catalogPlatform"] for m in matrix}):
        rows = [m for m in matrix if m["catalogPlatform"] == platform]
        visible = [m for m in rows if m["visible"]]
        by_platform.append(dict(platform=platform, catalogEntries=len(rows), visible=len(visible),
                                aliases=len(rows) - len(visible), states=dict(collections.Counter(m["state"] for m in visible))))
    summary = dict(schemaVersion=1, revision=runtime["revision"], sourceCommit=runtime["sourceCommit"], serverCommit=SERVER_COMMIT, catalogRevision=catalog["revision"],
                   catalogEntries=len(matrix), visible=sum(m["visible"] for m in matrix),
                   aliases=sum(not m["visible"] for m in matrix), originalProfiles=len(existing), originalPreparedModes=len(old_modes["modes"]),
                   reviewedFinalOwnerModes=len(decisions), existingTestPolicyProposals=len(proposed), nativeModeRefusals=len(refusals),
                   allGenericHistoricalDecisions=len(all_existing_test_proposals),
                   gameSpecificDocumentedModeRecords=len(documented_modes),
                   additionalDocumentaryEntries=len(research_by_item), documentaryResearchInputs=research_inputs,
                   finalOwnerGamesPrepared=len(reviewed), documentedSinglePlayerGames=sum(m["state"] == "documented-single-player-no-online-room" for m in matrix),
                   preparedProfilesAdded=len(new_profiles), finalEngineIds=[e["engineId"] for e in final_manifest["engines"]],
                   originalProfilesPreserved=True, originalModesPreserved=True, unknownGamesApproved=False,
                   playerCountsInferredFromPlatformCeiling=False, previousPsxApprovalsMigrated=False,
                   operatorApplied=False, currentProductionAuthenticatedSnapshotCaptured=False, physicalGameplayValidated=False,
                   ps3Ps4Online=False, offlineSonyModulesPreserved=True, perPlatform=by_platform,
                   inputProvenance={key: dict(gitPath=FILES[key], sha256=sha(value)) for key, value in original.items()},
                   finalRuntimeReceiptSha256=sha(runtime_bytes), localEngineAssetSha256=sha(engine_bytes),
                   observedReturn=dict(privateKitReceived=data["return"]["privateKitReceived"],
                                       newFinalEnginesActivated=data["return"]["newFinalEnginesActivated"],
                                       nativeAdministrativeAuthenticationRequired=data["return"]["nativeAdministrativeAuthenticationRequired"],
                                       coversPublished=data["return"]["covers"]["published"]))
    summary["states"] = dict(collections.Counter(m["state"] for m in matrix if m["visible"]))
    summary["remainingGenericNativeSelections"] = sum(m["state"] == "existing-test-policy-pending-game-review" for m in matrix if m["visible"])
    summary["closedWithoutNewApproval"] = sum(m["state"] in {"documentary-mode-blocked", "psx-hle-per-edition-review-pending", "no-current-client-online-engine", "no-approved-current-edition-mode-binding"} for m in matrix if m["visible"])
    args.audit_dir.mkdir(parents=True, exist_ok=True);args.private_dir.mkdir(parents=True, exist_ok=True)
    write_json(args.audit_dir / "matrix.json", dict(schemaVersion=1, rows=matrix))
    with (args.audit_dir / "matrix.csv").open("w", encoding="utf-8", newline="") as handle:
        fields = ["catalogOrdinal", "name", "catalogPlatform", "platform", "visible", "state", "reason", "descriptivePlayerLabel",
                  "historicalApprovedCounts", "documentarySimultaneousCounts", "documentaryAlternatingIndependentPortCounts", "currentBundleCompatibleCounts", "preparedFinalOwnerCounts", "gameSpecificModeEvidence"]
        writer = csv.DictWriter(handle, fieldnames=fields);writer.writeheader()
        for row in matrix:
            writer.writerow({key: json.dumps(row[key], ensure_ascii=False) if isinstance(row[key], list) else row[key] for key in fields})
    write_json(args.audit_dir / "summary.json", summary)
    write_json(args.audit_dir / "reviewed-mode-provenance.json", dict(schemaVersion=1, modes=decisions_refs, refusals=refusals))
    write_json(args.audit_dir / "documented-modes.json", dict(schemaVersion=1, modes=documented_modes))
    write_json(args.private_dir / "reviewed-decisions.json", ledger)
    write_json(args.private_dir / "mode-additions.json", merged)
    write_json(args.private_dir / "official-merge-receipt.json", receipt)
    write_json(args.private_dir / "existing-test-policy-proposal.json", dict(
        schemaVersion=1, reviewOnly=True, published=False, importableApprovalLedger=False,
        genericPolicyIsGameSpecificEvidence=False,
        targetEngineIdentityDerivedFromSuppliedFinalReceipt=True,
        genericNativeOwnerCandidates=proposed, historicalDecisions=all_existing_test_proposals))
    write_json(args.private_dir / "automatic-only-receipt.json", automatic)
    write_json(args.private_dir / "prepared-new-profiles.json", new_profiles)
    # Preserve exact read-only inputs for the sole reproducible backup. This
    # records raw Git blobs and the supplied final receipt, not a deployment.
    inputs_dir = args.private_dir / "inputs"
    inputs_dir.mkdir(parents=True, exist_ok=True)
    input_names = {
        "catalog": "catalog.json", "profiles": "profiles-complete.json",
        "modes": "original-prepared-modes.json", "content": "content-identities.json",
        "return": "server-return.json", "incoming": "server-build_incoming_manifest.py",
        "merge": "server-merge_scoped_modes.py", "operator": "server-station_online_profiles.py"}
    for key, name in input_names.items():
        (inputs_dir / name).write_bytes(original[key])
    (inputs_dir / "runtime-identity-original.json").write_bytes(runtime_bytes)
    (inputs_dir / "engines-original.json").write_bytes(engine_bytes)
    documentary_names = []
    for path in args.documentary_decisions:
        name = "documentary-" + path.name
        if name in documentary_names:
            raise ValueError("Documentary basename collision in exact backup inputs")
        (inputs_dir / name).write_bytes(path.read_bytes())
        documentary_names.append(name)
    inventory_names = ["prepare.py", "build_research.py", "reviewed-decisions.json", "mode-additions.json",
                       "official-merge-receipt.json", "automatic-only-receipt.json", "prepared-new-profiles.json",
                       "existing-test-policy-proposal.json"]
    inventory_names += ["inputs/" + name for name in input_names.values()]
    inventory_names += ["inputs/runtime-identity-original.json", "inputs/engines-original.json"]
    inventory_names += ["inputs/" + name for name in documentary_names]
    inventory = {}
    for name in sorted(inventory_names):
        value = (args.private_dir / name).read_bytes()
        inventory[name] = dict(sha256=sha(value), bytes=len(value))
    if runtime["revision"] != "R133":
        raise ValueError("The aggregated backup receipt is explicitly for final R133")
    write_json(args.private_dir / "mode-preparation-receipt.json", dict(
        schema="station.r133.private-mode-preparation.v1", revision="R133",
        sourceCommit=runtime["sourceCommit"], finalRuntimeReceiptSha256=sha(runtime_bytes),
        finalEngineIds=summary["finalEngineIds"], serverCommit=SERVER_COMMIT,
        inventory=inventory, originalProfilesPreserved=len(existing), originalModesPreserved=len(old_modes["modes"]),
        preparedProfilesAdded=len(new_profiles), preparedModesAdded=len(decisions),
        published=False, deployed=False, serverActivationConfirmed=False, physicalGameplayValidated=False))
    print(json.dumps({k: summary[k] for k in ("catalogEntries", "visible", "reviewedFinalOwnerModes", "existingTestPolicyProposals", "nativeModeRefusals", "preparedProfilesAdded", "operatorApplied")}))


if __name__ == "__main__":
    main()
