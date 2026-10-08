#!/usr/bin/env python3
"""Compare a private metadata export with R78; write sanitized review documents only.

This command never reads ROMs, configuration, credentials or the production index.
The export must come from the configured Station process, not a guessed catalog.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path


SPACES = set("\t\n\v\f\r \u00a0\u202f\u205f\u3000") | {
    chr(value) for value in range(0x2000, 0x200C)
}
PLACEHOLDERS = (
    "Sinopse ainda não localizada para esta edição",
    "Sinopse ainda não disponível nesta edição",
    "Sinopse ainda não disponível", "Sinopse não disponível", "Sem sinopse",
    "Descrição não disponível", "Nenhuma descrição disponível", "Sem descrição",
    "No description available", "No synopsis available",
    "Description unavailable", "Synopsis unavailable",
)


def normalized(text):
    # Same spaces and ASCII/Latin-1 case folding as station_synopsis_selection.h.
    text = " ".join(part for part in "".join(
        " " if character in SPACES else character for character in text
    ).split(" ") if part)
    return "".join(
        chr(ord(character) + 32) if (
            "A" <= character <= "Z" or
            0xC0 <= ord(character) <= 0xD6 or 0xD8 <= ord(character) <= 0xDE
        ) else character for character in text
    )


KNOWN = {normalized(text) for text in PLACEHOLDERS}


def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def classify(text, name):
    comparable = normalized(text)
    if not text:
        return "empty"
    if not comparable:
        return "whitespace"
    if comparable.removesuffix(".") in KNOWN:
        return "placeholder"
    if comparable == normalized(name):
        return "title_only"
    return "prose_present_not_fact_checked"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(directory, name, document):
    (directory / name).write_text(
        json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def identity(row, revision):
    # Allowlisted public catalog fields; exclude ROM/cover filesystem paths,
    # URLs, folder paths, environment and every account/device identity.
    artifact = row.get("artifact") or {}
    return {
        "itemId": row["itemId"], "platform": row["platform"], "name": row["name"],
        "itemRevision": row.get("revision", revision),
        "catalogVisible": row.get("catalogVisible", True),
        "coverId": row.get("coverId"),
        "artifact": {key: artifact[key] for key in (
            "fileName", "sizeBytes", "sha256", "format", "launchPath",
            "expandedSizeBytes", "fileCount"
        ) if key in artifact},
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index-export", required=True, type=Path)
    parser.add_argument("--handoff", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    current = read(args.index_export)
    if (current.get("readOnly") is not True or
            current.get("credentialsExported") is not False or
            current.get("filePathsExported") is not False):
        raise ValueError("Expected the bounded metadata-only export.")
    previous = read(args.handoff / "catalog/existing-synopses-audit.json")
    fallback = read(args.handoff / "data/synopses-complete.json")
    candidates = read(args.handoff / "data/server-synopsis-candidates.json")
    native_only = read(args.handoff / "data/native-only-descriptions.json")
    old = {row["itemId"]: row for row in previous["records"]}
    now = {row["itemId"]: row for row in current["items"]}
    native = {row["itemId"]: row for row in fallback}
    if len(now) != len(current["items"]) or len(old) != len(previous["records"]):
        raise ValueError("Duplicate catalog identity.")
    if set(native) != set(old):
        raise ValueError("R78 fallback identity set differs from its audited catalog.")
    revision = current["revision"]
    source = {
        "sourceUtc": current["utc"], "configuredIndexRevision": revision,
        "indexFileSha256": current["indexFileSha256"],
        "servicePid": current["pid"],
        "authenticatedApiResponse": current["authenticatedApiResponse"],
        "sourceKind": "metadata-read-of-running-process-configured-index-file",
        "phoneCacheObserved": False, "productionChanged": False,
    }
    proposals = {}
    cas_rows = []
    for proposal in candidates["proposals"]:
        expected = proposal["precondition"]
        item_id = expected["itemId"]
        if item_id in proposals:
            raise ValueError("Duplicate proposed identity.")
        proposals[item_id] = proposal
        text = proposal["proposedDescription"]
        utf16 = len(text.encode("utf-16-le")) // 2
        if digest(text) != proposal["proposedDescriptionSha256"] or not 0 < utf16 <= 2000:
            raise ValueError("Invalid proposed synopsis.")
        row = now.get(item_id)
        actual = {} if row is None else {
            "itemId": item_id, "platform": row["platform"],
            "itemRevision": row.get("revision", revision),
            "artifactSha256": row.get("artifact", {}).get("sha256"),
            "previousDescription": row.get("metadata", {}).get("description", ""),
        }
        if row is not None:
            actual["previousDescriptionSha256"] = digest(actual["previousDescription"])
        different = [key for key, value in expected.items() if actual.get(key) != value]
        cas_rows.append({
            "itemId": item_id, "platform": expected["platform"],
            "itemRevision": expected["itemRevision"],
            "artifactSha256": expected["artifactSha256"],
            "previousDescriptionSha256": expected["previousDescriptionSha256"],
            "proposedDescriptionSha256": proposal["proposedDescriptionSha256"],
            "proposedUtf16CodeUnits": utf16, "sourceKind": proposal["sourceKind"],
            "status": "preconditions_match" if not different else "preconditions_differ",
            "differingFields": different, "applied": False,
            "freshCasRequiredAtWriteTime": True,
        })
    classification = Counter()
    platforms = defaultdict(Counter)
    changed = []
    added = []
    missing = []
    over_limit = []
    fields = ("name", "platform", "catalogVisible", "itemRevision", "artifactSha256",
              "artifactFileName", "artifactLaunchPath", "publishedServerDescription")
    for row in current["items"]:
        item_id = row["itemId"]
        text = row.get("metadata", {}).get("description", "")
        category = classify(text, row["name"])
        classification[category] += 1
        stats = platforms[row["platform"]]
        stats["total"] += 1
        stats["visible" if row.get("catalogVisible", True) else "compatibility"] += 1
        stats[category] += 1
        is_added = item_id not in old
        if len(text.encode("utf-16-le")) // 2 > 2000:
            over_limit.append(item_id)
        record = identity(row, revision)
        if is_added:
            stats["addedSinceRevision14"] += 1
            added.append({
                **record,
                "metadata": {key: row.get("metadata", {}).get(key, "") for key in (
                    "description", "developer", "publisher", "genre", "players", "releaseDate"
                )},
                "descriptionClassification": category,
                "r78ExactFallbackAvailable": False,
            })
        else:
            artifact = row.get("artifact", {})
            actual = {
                "name": row["name"], "platform": row["platform"],
                "catalogVisible": row.get("catalogVisible", True),
                "itemRevision": row.get("revision", revision),
                "artifactSha256": artifact.get("sha256"),
                "artifactFileName": artifact.get("fileName"),
                "artifactLaunchPath": artifact.get("launchPath"),
                "publishedServerDescription": text,
            }
            differences = [key for key in fields if actual[key] != old[item_id][key]]
            if differences:
                changed.append({**record, "differingFields": differences})
        if category != "prose_present_not_fact_checked":
            stats["missingSynopsis"] += 1
            if is_added:
                stats["addedMissingSynopsis"] += 1
            missing.append({
                **record, "classification": category,
                "addedSinceRevision14": is_added,
                "r78ExactFallbackAvailable": (
                    item_id in native and native[item_id]["platform"] == row["platform"]
                    and bool(native[item_id]["description"])
                ),
                "serverProposalAvailable": item_id in proposals,
                "previousDescriptionSha256": digest(text),
            })
    native_cas = []
    for excluded in native_only:
        row = now.get(excluded["itemId"], {})
        matches = (
            row.get("platform") == excluded["platform"] and
            row.get("revision", revision) == excluded["itemRevision"] and
            row.get("artifact", {}).get("sha256") == excluded["artifactSha256"] and
            digest(row.get("metadata", {}).get("description", "")) ==
            excluded["previousDescriptionSha256"]
        )
        native_cas.append({
            "itemId": excluded["itemId"], "platform": excluded["platform"],
            "utf16CodeUnits": excluded["utf16CodeUnits"],
            "sourcePreconditionsStillMatch": matches, "serverUpdateAllowed": False,
        })
    spy = [identity(row, revision) for row in current["items"]
           if row.get("artifact", {}).get("fileName", "").lower() == "spy.zip"]
    summary = {
        **source, "baselineRevision": previous["catalogRevision"],
        "baselineIds": len(old), "currentIds": len(now),
        "currentVisibleIds": sum(row.get("catalogVisible", True) for row in now.values()),
        "currentCompatibilityIds": sum(not row.get("catalogVisible", True) for row in now.values()),
        "addedIds": len(added), "removedIds": sorted(set(old) - set(now)),
        "commonIdsCompared": len(set(old) & set(now)),
        "commonComparedFields": list(fields), "commonChangedIds": changed,
        "currentDescriptionClassification": dict(classification),
        "currentDescriptionsOverUtf16Limit": over_limit,
        "missingSynopsisIds": len(missing),
        "missingVisibleIds": sum(row["catalogVisible"] for row in missing),
        "missingCompatibilityIds": sum(not row["catalogVisible"] for row in missing),
        "addedIdsWithoutSynopsis": sum(row["addedSinceRevision14"] for row in missing),
        "oldIdsWithoutServerSynopsisCoveredByR78": sum(row["r78ExactFallbackAvailable"] for row in missing),
        "synopsisPresenceDoesNotVerifyFacts": True,
        "proposalCasCounts": dict(Counter(row["status"] for row in cas_rows)),
        "proposalDescriptionsWouldFillCurrentlyEmpty": sum(row["serverProposalAvailable"] for row in missing),
        "proposalsApplied": 0, "platforms": {key: dict(value) for key, value in platforms.items()},
        "nativeOnlyDescriptionsExcluded": native_cas,
        "spyClassificationReview": {
            "items": spy, "currentPlatformChanged": False,
            "sameNamedItemsAreNotInterchangeable": True,
            "romContentExamined": False,
            "sources": [
                "https://github.com/finalburnneo/FBNeo/blob/master/src/burn/drv/konami/d_spy.cpp",
                "https://github.com/mamedev/mame/blob/master/src/mame/konami/spy.cpp",
            ],
            "sourceObservation": "Official drivers identify spy as Konami/GX857, not Neo Geo hardware.",
            "followup": "Check original archive identity and current client dispatch before any platform migration.",
        },
    }
    args.output.mkdir(parents=True, exist_ok=True)
    save(args.output, "COMPARACAO-CATALOGO-REV18.json", summary)
    save(args.output, "CATALOGO-NOVOS-IDS-REV18.json", {**source, "records": added})
    save(args.output, "SINOPSES-AUSENTES-REV18.json", {**source, "records": missing})
    save(args.output, "CAS-PROPOSTAS-R78-REV18.json", {**source, "applied": False, "records": cas_rows})
    print(json.dumps({
        "revision": revision, "items": len(now), "added": len(added),
        "missingSynopsis": len(missing), "newMissingSynopsis": summary["addedIdsWithoutSynopsis"],
        "cas": summary["proposalCasCounts"], "productionChanged": False,
    }))


if __name__ == "__main__":
    main()
