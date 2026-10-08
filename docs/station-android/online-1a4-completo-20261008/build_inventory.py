#!/usr/bin/env python3
"""Generate a complete, public handoff inventory. Never writes a live registry.

Metadata and title matches produce review candidates, never room permissions.
Only explicit mode rules produce appendable profiles. Existing admissions survive.
"""
import argparse
import csv
import hashlib
import json
import re
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def title_key(value):
    # A research join only. Does not establish ROM or authorization identity.
    value = re.sub(r"\.[A-Za-z0-9]{2,5}$", "", value)
    value = re.sub(r"\([^)]*\)|\[[^]]*\]", "", value).strip()
    value = re.sub(r",\s*(The|A|An)$", r" \1", value, flags=re.I)
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode().lower()
    value = re.sub(r"\b(the|a|an)\b", "", value)
    return re.sub(r"[^a-z0-9]", "", value)


def family(platform):
    return {"snesbr": "snes", "megadrivebr": "megadrive"}.get(platform, platform)


def hint_max(text):
    values = [int(x) for x in re.findall(r"\d+", text or "")]
    return max(values, default=None)


def tsv(path, rows, fields):
    with Path(path).open("w", encoding="utf-8", newline="") as stream:
        out = csv.DictWriter(stream, fieldnames=fields, delimiter="\t", lineterminator="\n", extrasaction="ignore")
        out.writeheader()
        for row in rows:
            value = {k: row.get(k) for k in fields}
            for k, v in value.items():
                if isinstance(v, (dict, list)):
                    value[k] = json.dumps(v, ensure_ascii=False, separators=(",", ":"))
                elif v is None:
                    value[k] = ""
            out.writerow(value)


def build(bundle):
    catalog = read(bundle / "catalog-rev20.json")
    active = read(bundle / "profiles-active-r81.json")
    controllers = read(bundle / "controller-profiles-r81.json")["entries"]
    rules = read(bundle / "mode-rules.json")["rules"]
    mappings = read(bundle / "reference-adapter-mappings.json")
    assert catalog["revision"] == 20
    active_by_id = defaultdict(list)
    for profile in active:
        active_by_id[profile["itemId"]].append(profile)
    controller_by_id = {(x["platform"], x["canonical"]["controllerProfile"]): x
                        for x in controllers if x["release"] == "r81"}
    reference_by_title = defaultdict(list)
    for platform, entries in mappings.items():
        for entry in entries:
            reference_by_title[(platform, title_key(entry["referenceEdition"]))].append(entry)
    rules_by_title = defaultdict(list)
    for rule in rules:
        for title in rule["titleKeys"]:
            rules_by_title[(rule["platform"], title_key(title))].append(rule)
    rows, proposals, prepared, draft_modes = [], [], [], []
    active_keys = {tuple(p[k] for k in ("itemId", "contentSha256", "engineId", "coreSha256", "runtimeSha256", "profileId", "profileSha256")) for p in active}
    for item in sorted(catalog["items"], key=lambda x: (x["platform"], x["name"].casefold(), x["itemId"])):
        item_id, platform = item["itemId"], item["platform"]
        visible = item.get("catalogVisible", True)
        base = family(platform)
        artifact = item.get("artifact") or {}
        metadata = item.get("metadata") or {}
        reference = reference_by_title[(base, title_key(artifact.get("fileName", item["name"])))]
        mode_rules = rules_by_title[(base, title_key(artifact.get("fileName", item["name"])))]
        if not mode_rules:
            mode_rules = rules_by_title[(base, title_key(item["name"]))]
        # Name joins are visible in the inventory and never reported as checksum matches.
        player_hint = metadata.get("players", "")
        max_hint = hint_max(player_hint)
        runtime_ready = base in ("snes", "megadrive")
        modes = [{k: v for k, v in rule.items() if k not in ("titleKeys", "profileSlug")} for rule in mode_rules]
        todo = []
        if not visible:
            state = "compatibility-id-download-only"
            todo.append("PRESERVE_ALIAS_DO_NOT_DUPLICATE_IN_LOBBY")
        elif not runtime_ready:
            state = "online-engine-not-launch-ready" if base == "neogeo" else "online-engine-absent"
            todo += ["INTEGRATE_AND_IDENTIFY_ONLINE_ENGINE", "BIND_EXACT_LAUNCH_CONTENT", "MAP_AND_DOCUMENT_MODES"]
        elif active_by_id[item_id]:
            state = "owner-authorized-for-online-tests"
            todo.append("PRESERVE_CURRENT_ADMISSION")
        else:
            state = "missing-profile-investigate"
            todo.append("BIND_EXACT_PROFILE")
        if runtime_ready and visible:
            if not modes:
                todo.append("CLASSIFY_MODE_DATA_NO_PER_GAME_APP_CODE")
            if reference:
                todo.append("CHECK_REFERENCE_CRC_OR_MD5_OFFLINE_NOT_AT_DOWNLOAD")
            if base == "megadrive" and reference:
                adapters = {x["adapter"] for x in reference}
                if adapters & {"GamepadPort1TeamPlayerPort2", "TeamPlayerPort1TeamPlayerPort2"}:
                    todo.append("IMPLEMENT_PHYSICAL_PORT_LAYOUT_AND_LOGICAL_INPUT_REMAP")
            if "J-Cart" in artifact.get("fileName", ""):
                todo.append("IMPLEMENT_JCART_CORE_BUS_AND_INPUT_MAPPING")
            if modes:
                todo.append("EXERCISE_EACH_DOCUMENTED_MODE_AND_SEAT")
        risks = []
        if max_hint and max_hint > 4:
            risks.append("DISPLAY_MAXIMUM_IS_NOT_FOUR_SIMULTANEOUS_HUMANS")
        if re.search(r"\b2\s*players?\b|\(2p\)", item["name"], re.I) and (max_hint or 0) > 2:
            risks.append("ARCADE_VARIANT_2P_CONFLICTS_WITH_DISPLAY_LABEL")
        if re.search(r"aerobiz|monopoly|clue|worm|jeopardy|wheel of fortune|pinball|golf|romance of|risk|nobunaga|california games|olympic|winter challenge|summer challenge", item["name"], re.I) and (max_hint or 0) > 2:
            risks.append("REVIEW_ALTERNATING_SHARED_CONTROLLER_OR_TOURNAMENT_MODE")
        if base in ("snes", "megadrive") and max_hint == 1 and active_by_id[item_id]:
            risks.append("OWNER_ADMISSION_2P_DOES_NOT_PROVE_GAME_HAS_TWO_PLAYERS")
        if platform.endswith("br") or re.search(r"\((?:BR|T)\)|PT-BR|\[h|hack|Futebol Brasileiro", artifact.get("fileName", "") + " " + item["name"], re.I):
            risks.append("TRANSLATION_OR_MODIFICATION_REQUIRES_VARIANT_CONTROL_CHECK")
        candidate = visible and (bool(max_hint and max_hint > 2) or bool(reference) or any(x["nativeMaximum"] > 2 for x in mode_rules) or "J-Cart" in artifact.get("fileName", ""))
        row = {"itemId": item_id, "name": item["name"], "platform": platform,
               "enginePlatform": base, "catalogVisible": visible, "itemRevision": item["revision"],
               "coverId": item.get("coverId"), "artifactFileName": artifact.get("fileName"),
               "artifactSha256": artifact.get("sha256"), "artifactSizeBytes": artifact.get("sizeBytes"),
               "artifactFormat": artifact.get("format"), "launchPath": artifact.get("launchPath"),
               "contentSha256": item.get("contentSha256"), "expandedSizeBytes": artifact.get("expandedSizeBytes"),
               "fileCount": artifact.get("fileCount"), "synopsisPresent": bool(metadata.get("description")),
               "playersLabel": player_hint, "metadataMaximumHint": max_hint, "candidate3or4": candidate,
               "onlineState": state, "currentProfiles": active_by_id[item_id],
               "documentedOriginalModes": modes, "referenceAdapters": reference,
               "referenceJoin": "title-only-checksum-not-compared" if reference else None,
               "reviewFlags": risks, "nextActions": todo, "physicalPhoneGameplayVerifiedByThisPackage": False}
        rows.append(row)
        if visible and runtime_ready and item.get("contentSha256"):
            for rule in mode_rules:
                c = controller_by_id[(base, rule["controllerProfile"])]
                profile_id = rule["profileSlug"]
                profile = {"itemId": item_id, "contentSha256": item["contentSha256"], "platform": base,
                           "engineId": c["engineId"], "coreSha256": c["coreSha256"], "runtimeSha256": c["runtimeSha256"],
                           "profileId": profile_id, "profileSha256": c["profileSHA256"],
                           "maximumPlayers": rule["maximumStationPlayers"], "approved": rule["existingR81ConfigurationReady"],
                           "controllerProfile": rule["controllerProfile"], "mode": rule["mode"],
                           "allowedPlayerCounts": rule["allowedPlayerCounts"]}
                report = {"itemId": item_id, "name": item["name"], "platform": platform,
                          "profile": profile, "sourceIds": rule["sourceIds"], "conditions": rule["conditions"],
                          "nativeMaximum": rule["nativeMaximum"], "originalModeDocumented": True,
                          "physicalPhoneGameplayVerified": False, "variantFlags": risks,
                          "publicationState": "prepared-not-published",
                          "technicalPrerequisites": rule.get("technicalPrerequisites", []),
                          "authorization": "owner-request-all-compatible-games-for-use-and-tests-20261008"}
                key = tuple(profile[k] for k in ("itemId", "contentSha256", "engineId", "coreSha256", "runtimeSha256", "profileId", "profileSha256"))
                if key not in active_keys:
                    proposals.append(report)
                    if rule["existingR81ConfigurationReady"]:
                        prepared.append(profile)
                    else:
                        draft_modes.append(report)
    write(bundle / "inventory.json", {"schemaVersion": 1, "catalogRevision": 20, "items": rows})
    fields = ["platform", "name", "itemId", "catalogVisible", "coverId", "playersLabel", "onlineState", "candidate3or4", "contentSha256", "artifactSha256", "artifactFileName", "launchPath", "documentedOriginalModes", "referenceAdapters", "reviewFlags", "nextActions"]
    tsv(bundle / "inventory.tsv", rows, fields)
    candidates = [x for x in rows if x["candidate3or4"]]
    tsv(bundle / "games-3a4.tsv", candidates, fields)
    write(bundle / "mode-proposals.json", {"schemaVersion": 1, "publicationState": "prepared-not-published", "entries": proposals})
    write(bundle / "profiles-additions-r81.json", prepared)
    write(bundle / "profiles-combined-for-review.json", active + prepared)
    write(bundle / "modes-needing-app-work.json", {"schemaVersion": 1, "entries": draft_modes})
    ui = []
    for row in rows:
        if not row["catalogVisible"]:
            continue
        for rule in row["documentedOriginalModes"]:
            ui.append({"itemId": row["itemId"], "contentSha256": row["contentSha256"],
                       "mode": rule["mode"], "displayName": rule["displayName"],
                       "playStyle": rule["playStyle"], "nativeMaximum": rule["nativeMaximum"],
                       "instructions": rule["conditions"], "sourceIds": rule["sourceIds"],
                       "authorizationSource": "exact-profile-only",
                       "physicalPhoneGameplayVerified": False})
    write(bundle / "mode-metadata-proposed.json", {"schemaVersion": 1, "revision": 1,
          "purpose": "Proposed signed display/help sidecar; current R81 API has not integrated these fields.", "entries": ui})
    coverage = {"schemaVersion": 1, "catalogRevision": 20, "catalogIds": len(rows),
                "visibleGames": sum(x["catalogVisible"] for x in rows),
                "compatibilityIds": sum(not x["catalogVisible"] for x in rows),
                "visibleByPlatform": dict(sorted(Counter(x["platform"] for x in rows if x["catalogVisible"]).items())),
                "currentProfilesPreserved": len(active), "boundContentIds": sum(bool(x["contentSha256"]) for x in rows),
                "visible3or4Candidates": len(candidates),
                "snesMegaVisible3or4Candidates": sum(x["enginePlatform"] in ("snes", "megadrive") for x in candidates),
                "itemsWithDocumentedOriginalModes": sum(bool(x["documentedOriginalModes"]) for x in rows),
                "preparedProfileAdditions": len(prepared), "modesNeedingAppWork": len(draft_modes),
                "idsOmitted": 0, "productionModified": False, "allGameModesPhysicallyVerified": False,
                "allDisplayCountsVerifiedAsSimultaneous": False,
                "inputSha256": {f: sha(bundle / f) for f in ["catalog-rev20.json", "profiles-active-r81.json", "controller-profiles-r81.json", "mode-rules.json", "reference-adapter-mappings.json"]}}
    write(bundle / "coverage.json", coverage)
    document = ["# Todos os candidatos a três/quatro jogadores no catálogo revision20", "",
                "Lista completa da união entre rótulo >2, referência de adaptador e modos documentados. **Candidato não significa multiplayer simultâneo confirmado.** Cada linha tem IDs/hashes/fontes/ação no JSON/TSV. Os demais IDs estão em inventory.tsv.", "",
                "| Plataforma | Nome no catálogo | Rótulo atual | Referência de controle | Modos documentados do original | Item ID |", "|---|---|---|---|---|---|"]
    for x in candidates:
        adapters = ", ".join(sorted({a["adapter"] for a in x["referenceAdapters"]})) or "A determinar"
        modes = ", ".join(f'{m["mode"]} ({m["nativeMaximum"]})' for m in x["documentedOriginalModes"]) or "A determinar"
        document.append("| " + " | ".join(str(v).replace("|", "\\|") for v in [x["platform"], x["name"], x["playersLabel"], adapters, modes, x["itemId"]]) + " |")
    (bundle / "games-3a4.md").write_text("\n".join(document) + "\n", encoding="utf-8")
    print(json.dumps(coverage, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, default=Path(__file__).resolve().parent)
    build(parser.parse_args().bundle)
