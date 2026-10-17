"""Pure R172 projection: preserve reviewed decisions and extend exact owners.

No production file, account, room, service, APK or phone is changed here.
The native operator must compare this output against fresh protected inputs.
"""
from copy import deepcopy
import hashlib
import json

COUNTS = {"psx": 93, "n64": 387, "gamecube": 9, "wii": 3}
PILOT = "station_767e5e92f2f353479e6120f1c28f96a7"
PILOT_CONTENT = "6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73"


def need(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def prepare(data, received, authority, operator, registrar):
    need(authority.get("passed") and authority.get("serverApplied") and
         authority.get("revision") == "R171", "Applied R171 authority required")
    catalog, profiles, modes, manifest = (data[k] for k in ("catalog", "profiles", "modes", "engines"))
    previous = authority["data"]
    counts = previous["counts"]
    need((catalog["revision"], len(catalog["items"]), len(profiles), len(modes["modes"]), len(manifest["engines"])) ==
         tuple(counts[k] for k in ("catalogRevision", "catalogItems", "profiles", "modes", "engines")),
         "Current authority changed; review before registration")
    operator.validate_manifest(manifest)
    operator.validate_modes(modes)
    kit = received["MANIFESTO-KIT.json"]
    proof = received["proofs/final-apk-proof.json"]
    need(kit["revision"] == "R172" and kit["referenceKitR171Sha256"] == previous["acceptedKitSha256"] and
         kit["apkSha256"] == proof["apkSha256"] and proof["passed"] and proof["versionCode"] == 14 and
         proof["preservedEntryCount"] == 13855 and proof["preservedDexPartitions"] == 39 and
         kit["psxInputDelayBefore"] == 4 and kit["psxInputDelayAfter"] == 8 and
         kit["otherEmulationOptionsPreserved"] is True, "Exact scoped R172 kit required")
    incoming = received["incoming.json"]
    operator.validate_manifest(incoming)
    old_ids = previous["exactEngineIds"]
    new_ids = kit["independentEngineIds"]
    old_owners = {e["platform"]: e for e in manifest["engines"] if e["engineId"] in old_ids.values()}
    new_owners = {e["platform"]: e for e in incoming["engines"]}
    runtime = {e["platform"]: e for e in received["engines-apk.json"]["engines"] if e["platform"] in COUNTS}
    policies = received["profile-policies.json"]["profiles"]
    need(set(old_ids) == set(new_ids) == set(old_owners) == set(new_owners) == set(runtime) == set(policies) == set(COUNTS) and
         len(incoming["engines"]) == 4 and len(set(new_ids.values())) == 4,
         "Exactly four old and new independent owners required")
    existing_ids = {e["engineId"] for e in manifest["engines"]}
    expected_hashes = {}
    for platform, engine in new_owners.items():
        old = old_owners[platform]
        need(old["engineId"] == old_ids[platform] and engine["engineId"] == new_ids[platform] and
             engine["engineId"] not in existing_ids, "Owner already registered or changed")
        expected = deepcopy(old)
        expected.update(engineId=engine["engineId"], runtimeSha256=engine["runtimeSha256"])
        if platform == "psx":
            need(old["options"].count("inputDelayFrames=4\n") == 1, "Exact old PSX input delay required")
            expected["options"] = old["options"].replace("inputDelayFrames=4\n", "inputDelayFrames=8\n")
            for definition in expected["controllerProfiles"]:
                need(definition["configuration"]["coreOptions"] == old["options"], "Old PSX controller policy differs")
                definition["configuration"]["coreOptions"] = expected["options"]
        need(expected == engine, "Unrequested core, dependency, controller or option change")
        policy = policies[platform]
        need(all(policy[k] == engine[k] for k in ("engineId", "coreSha256", "runtimeSha256")),
             "Controller authority differs from actual engine")
        config = operator.profile_configuration(engine, policy["controllerProfile"], policy["maximumPlayers"])
        need(config is not None and operator.profile_digest(config) == policy["profileSha256"],
             "Android controller fingerprint differs")
        need(engine["controllerProfiles"] == [dict(controllerProfile=policy["controllerProfile"],
             maximumPlayers=policy["maximumPlayers"], configuration=config)] and
             engine.get("requireExplicitEngineModes") is True, "Explicit controller and mode policy required")
        strip = lambda e: {k: v for k, v in e.items() if k not in ("controllerProfiles", "requireExplicitEngineModes")}
        need(strip(engine) == strip(runtime[platform]) and
             runtime[platform]["controllerProfiles"] == [policy["controllerProfile"]],
             "Actual APK owner differs from server declaration")
        expected_hashes[platform] = policy["profileSha256"]
    merged = deepcopy(manifest)
    merged["engines"].extend(deepcopy(incoming["engines"]))
    operator.validate_manifest(merged)
    catalog_items = {i["itemId"]: i for i in catalog["items"]}
    need(len(catalog_items) == len(catalog["items"]), "Duplicate catalog identity")
    prior = {(p["engineId"], p["itemId"], p["contentSha256"], p["profileId"]): p
             for p in profiles if p["engineId"] in old_ids.values()}
    need(len(prior) == sum(COUNTS.values()) == sum(p["engineId"] in old_ids.values() for p in profiles),
         "Applied R171 reviewed profile set differs")
    extended = deepcopy(modes)
    mode_counts = dict.fromkeys(COUNTS, 0)
    selected = set()
    for before, after in zip(modes["modes"], extended["modes"]):
        owners = set(before.get("engineIds", [])) & set(old_ids.values())
        if not owners:
            continue
        platform = before["platform"]
        need(platform in COUNTS and owners == {old_ids[platform]}, "Reviewed owner platform differs")
        key = (old_ids[platform], before["itemId"], before["contentSha256"], before["profileId"])
        source = prior.get(key)
        item = catalog_items.get(before["itemId"])
        need(source is not None and key not in selected and item is not None and
             item.get("contentSha256") == before["contentSha256"] and
             operator.platform(item["platform"]) == platform and item.get("catalogVisible", True),
             "Missing or changed exact reviewed edition")
        need(all(source[k] == before[k] for k in operator.MODE_FIELDS), "Reviewed decision differs from applied profile")
        after["engineIds"].append(new_ids[platform])
        selected.add(key)
        mode_counts[platform] += 1
    need(mode_counts == COUNTS and selected == set(prior), "Every previous decision must have exactly one new owner binding")
    registrar.reviewed_modes(modes, extended, incoming)
    output = operator.prepare(catalog["items"], profiles, merged, extended)
    need(output[:len(profiles)] == profiles and len(output) == len(profiles) + 492,
         "Existing profiles changed or unexpected new profiles")
    added_counts = dict.fromkeys(COUNTS, 0)
    added_selected = set()
    for row in output[len(profiles):]:
        platform = row["platform"]
        need(platform in COUNTS and row["engineId"] == new_ids[platform], "Unexpected new profile owner")
        key = (old_ids[platform], row["itemId"], row["contentSha256"], row["profileId"])
        need(key in prior and key not in added_selected, "Unexpected new reviewed mode")
        expected = dict(prior[key], engineId=new_ids[platform], runtimeSha256=new_owners[platform]["runtimeSha256"],
                        profileSha256=expected_hashes[platform])
        need(expected == row, "Approval, refusal, content, controls or mode changed")
        added_counts[platform] += 1
        added_selected.add(key)
    need(added_counts == COUNTS and added_selected == selected, "Incomplete new revision bindings")
    need(any(p["itemId"] == PILOT and p["contentSha256"] == PILOT_CONTENT and p["platform"] == "psx" and
             p["approved"] and p["mode"] == "versus" and 2 in p["allowedPlayerCounts"] for p in output[len(profiles):]),
         "Reviewed Capcom vs. SNK VS pilot missing")
    audit = dict(schema="station.r172.server-preflight.v1", passed=True, baselineAppliedR171=True,
        enginesAdded=4, profilesAdded=492, modesAdded=0, modeEngineBindingsAdded=492,
        before=counts, after=dict(counts, engines=len(merged["engines"]), profiles=len(output)),
        modeCounts=mode_counts, previousBindingsPreserved=True, previousDecisionFieldsPreserved=True,
        profilesCompactBytes=len(operator.encoded(output)), modesCompactBytes=len(operator.encoded(extended)),
        profilesIndentedBytes=len((json.dumps(output, ensure_ascii=False, indent=2) + "\n").encode()),
        maximumProfileBytes=operator.MAXIMUM_PROFILE_REGISTRY_BYTES,
        maximumModeBytes=operator.MAXIMUM_PREPARED_MODE_BYTES,
        candidateSha256={k: digest(operator.encoded(v)) for k, v in (("manifest", merged), ("profiles", output), ("modes", extended))},
        exactEngineIds=new_ids, profileSha256=expected_hashes,
        serverApplied=False, physicalGameplayValidated=False)
    return merged, output, extended, audit
