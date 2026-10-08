#!/usr/bin/env python3
"""Check coverage, exact bindings, canonical controllers and preserved admissions."""
import hashlib
import json
from collections import Counter
from pathlib import Path

from build_inventory import family


def main():
    root = Path(__file__).resolve().parent
    def load(name):
        return json.loads((root / name).read_text(encoding="utf-8"))
    checks = 0
    def check(condition, name):
        nonlocal checks
        checks += 1
        if not condition:
            raise SystemExit("FAIL: " + name)
    catalog = load("catalog-rev20.json")
    items = {x["itemId"]: x for x in catalog["items"]}
    inventory = load("inventory.json")["items"]
    active = load("profiles-active-r81.json")
    additions = load("profiles-additions-r81.json")
    combined = load("profiles-combined-for-review.json")
    coverage = load("coverage.json")
    check(len(items) == len(catalog["items"]) == len(inventory) == 3734, "all3734uniqueIDs")
    check({x["itemId"] for x in inventory} == items.keys(), "allIDscovered")
    check(sum(x["catalogVisible"] for x in inventory) == 3479, "visible3479")
    check(sum(not x["catalogVisible"] for x in inventory) == 255, "compatibility255")
    check(len(active) == 1816 and combined[:len(active)] == active, "preserve1816existingentriesinorder")
    check(combined == active + additions, "combinedappendonly")
    check(sum(bool(x["contentSha256"]) for x in inventory) == 2071, "allcurrentcontentsbound")
    check(hashlib.sha256((root / "profiles-active-r81.json").read_bytes()).hexdigest() == "f3eb13fcb474edb5a1b549a3509aa47505765210b38d1f1dd81bc157b4ec555c", "activeproductionprofilesbytes")
    engines = load("engines-r81.json")["manifest"]["engines"]
    engines = {x["engineId"]: x for x in engines}
    canonical = {}
    for c in load("controller-profiles-r81.json")["entries"]:
        obj = c["canonical"]
        check(list(obj) == ["schemaVersion", "controllerProfile", "devices", "coreOptions"], "canonicalorder")
        digest = hashlib.sha256(json.dumps(obj, ensure_ascii=False, separators=(",", ":")).encode()).hexdigest()
        check(digest == c["profileSHA256"], "canonicalprofilehash")
        if c["release"] == "r81":
            canonical[(c["platform"], obj["controllerProfile"])] = c
    identities = set()
    per_item = Counter()
    fields = {"itemId", "contentSha256", "platform", "engineId", "coreSha256", "runtimeSha256", "profileId", "profileSha256", "maximumPlayers", "approved", "controllerProfile", "mode", "allowedPlayerCounts"}
    for p in combined:
        item = items[p["itemId"]]
        check(set(p) == fields, "publicregistryfieldwhitelist")
        check(item.get("catalogVisible", True), "visibleprofilenotcompatibilityalias")
        check(p["contentSha256"] == item.get("contentSha256"), "exactcurrentcontenthash")
        check(p["platform"] == family(item["platform"]), "engineplatformnormalization")
        engine = engines[p["engineId"]]
        check(engine["launchReady"] and p["coreSha256"] == engine["coreSha256"] and p["runtimeSha256"] == engine["runtimeSha256"], "exactlauncheligibleenginecoreandruntime")
        c = canonical[(p["platform"], p["controllerProfile"])]
        check(c["profileSHA256"] == p["profileSha256"], "controllerhashmatchesconfiguration")
        count = p["maximumPlayers"]
        allowed = p["allowedPlayerCounts"]
        check(1 <= count <= 4 and allowed == sorted(set(allowed)), "seatboundsanduniqueness")
        check(all(2 <= n <= count for n in allowed) and (allowed == [] if count == 1 else count in allowed), "modeadmissionbounds")
        key = tuple(p[k] for k in ["itemId", "contentSha256", "engineId", "coreSha256", "runtimeSha256", "profileId", "profileSha256"])
        check(key not in identities, "fullprofileidentityunique")
        check(p["approved"] is True, "existingauthorizationpreservedandcompatiblepreparationsauthorized")
        identities.add(key)
        per_item[p["itemId"]] += 1
    check(max(per_item.values()) <= 32, "peritemprofilebound")
    source_ids = {s["id"] for s in load("sources.json")["sources"]}
    for rule in load("mode-rules.json")["rules"]:
        check(set(rule["sourceIds"]) <= source_ids, "everymodehasresolvablesources")
        check(rule["allowedPlayerCounts"] == [] if rule["maximumStationPlayers"] == 1 else max(rule["allowedPlayerCounts"]) <= min(4, rule["nativeMaximum"]), "nogamegrantsabovenativecapacity")
    for item in inventory:
        original = items[item["itemId"]]
        check(item["coverId"] == original.get("coverId") and item["artifactSha256"] == (original.get("artifact") or {}).get("sha256"), "coverandartifactbelongtoexactitem")
        check(item["currentProfiles"] == [p for p in active if p["itemId"] == item["itemId"]], "currentstateisactualnotproposal")
        check(bool(item["nextActions"]), "everyIDhasactionordisposition")
    for name, digest in coverage["inputSha256"].items():
        check(hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, "inputchecksum")
    counts = Counter(x["platform"] for x in inventory if x["catalogVisible"])
    check(dict(counts) == coverage["visibleByPlatform"], "platformsreconciled")
    report = {"schemaVersion": 1, "passed": True, "checks": checks,
              "allCatalogIdsCovered": 3734, "existingProfilesPreserved": 1816,
              "preparedProfileAdditions": len(additions), "productionModified": False,
              "limits": ["Data/contract validation; not physical Android gameplay or WAN performance.", "Prepared combined registry is not the file currently loaded by production."]}
    (root / "package-validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
