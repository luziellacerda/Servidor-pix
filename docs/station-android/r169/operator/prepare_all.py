"""Prepare additive R169 bindings from the applied R168 authority.

This module only prepares files. It never publishes data or restarts services.
It refuses changed catalog content, policies, native code, baseline counts, and
unreviewed modes. The operator must inspect outputs before deployment.
"""

import argparse
from copy import deepcopy
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import zipfile


DOC = Path(__file__).resolve().parents[2]
TOOLS = DOC / "online-plataformas-20261009" / "library-tools"
KIT_SHA256 = "859b33b156698abecadadf035f8117a4b2b680439c4fed32eb2001347dc6c832"
KIT_BYTES = 14918062
APK_SHA256 = "68521db5fbd46d6da9895c4d9b291febda5597c82ba3b51935ccd7e98520877d"
R168_KIT_SHA256 = "c0de71c3a4e10cce320f7ba6966e183871760157ca67e9010eb5bdfb0850177b"
NEW_IDS = {
    "psx": "pcsx-rearmed-ff81ed17-player-v3-cddba501a25c",
    "n64": "mupen64plus-ae-33bf7021-n64-v3-a69ac794a2d1",
    "gamecube": "dolphin-5102a033-gamecube-v3-48da63940e83",
    "wii": "dolphin-5102a033-wii-v3-48da63940e83",
}
EXPECTED_MODES = {"psx": 93, "n64": 387, "gamecube": 9, "wii": 3}
PILOT = "station_767e5e92f2f353479e6120f1c28f96a7"
PILOT_CONTENT = "6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73"
MODE_FIELDS = ("maximumPlayers", "allowedPlayerCounts", "approved", "controllerProfile",
               "mode", "modeTitle", "instructions", "sources")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_bounded(path, maximum):
    path = Path(path)
    require(not path.is_symlink() and path.is_file() and 0 < path.stat().st_size <= maximum,
            "Bounded regular authority file required: " + str(path))
    with path.open("rb") as stream:
        content = stream.read(maximum + 1)
    require(0 < len(content) <= maximum, "Authority file grew during reading: " + str(path))
    return content


def read_json(path, maximum):
    return json.loads(read_bounded(path, maximum))


def digest_file(path, maximum):
    path = Path(path)
    require(not path.is_symlink() and path.is_file() and 0 < path.stat().st_size <= maximum,
            "Bounded regular authority file required: " + str(path))
    value = hashlib.sha256()
    total = 0
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            total += len(block)
            require(total <= maximum, "Authority file grew during hashing: " + str(path))
            value.update(block)
    return value.hexdigest()


def check_output_paths(inputs, outputs):
    require(len({path.parent.resolve() for path in outputs}) == 1 and
            all(not any(parent.is_symlink() for parent in path.parents) for path in outputs),
            "Outputs must share one real preparation directory")
    parent = outputs[0].parent
    require(parent.is_dir() and not any(parent.iterdir()),
            "Create an empty preparation directory before running")
    require(len({path.resolve() for path in outputs}) == len(outputs) and
            not {path.resolve() for path in outputs} & {path.resolve() for path in inputs} and
            all(not path.exists() and not path.is_symlink() for path in outputs),
            "Preparation must not overwrite an input or follow a symlink")


def load_profiles_tool():
    path = TOOLS / "station_online_profiles.py"
    spec = importlib.util.spec_from_file_location("r169_station_profiles", path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def verify_kit(path):
    path = Path(path)
    require(path.stat().st_size == KIT_BYTES and digest_file(path, KIT_BYTES) == KIT_SHA256,
            "R169 kit ZIP size or hash differs")
    with zipfile.ZipFile(path) as archive:
        file_manifest = json.loads(archive.read("MANIFESTO-ARQUIVOS.json"))
        require(file_manifest["revision"] == "R169", "Wrong kit revision")
        files = file_manifest["files"]
        require(len(files) == 53 and set(archive.namelist()) == set(files) | {"MANIFESTO-ARQUIVOS.json"},
                "R169 kit file set differs")
        for name, entry in files.items():
            data = archive.read(name)
            require(len(data) == entry["bytes"] and digest(data) == entry["sha256"],
                    "R169 kit entry differs: " + name)
        received = {name: json.loads(archive.read(name))
                    for name in ("MANIFESTO-KIT.json", "engines-apk.json", "incoming.json",
                                 "profile-policies.json", "proofs/final-apk-proof.json")}
    kit = received["MANIFESTO-KIT.json"]
    require(kit["apkSha256"] == APK_SHA256 and kit["referenceKitR168Sha256"] == R168_KIT_SHA256,
            "R169 kit APK or donor differs")
    require(kit["preservedDexPartitions"] == 39 and kit["casInstructionPatchVerified"] is True,
            "R169 DEX delta not verified")
    require(kit["independentEngineIds"] == list(NEW_IDS.values()), "R169 engine IDs differ")
    proof = received["proofs/final-apk-proof.json"]
    require(proof["passed"] is True and proof["apkSha256"] == APK_SHA256 and
            proof["v2"] is True and proof["v3"] is True and
            proof["zipAlignment16KiB"] is True and
            set(proof["onlineEngineIds"].values()) == set(NEW_IDS.values()),
            "R169 signed APK proof differs")
    return received


def scoped_profile_id(source_id, platform, old_engine_id, new_engine_id):
    if platform == "psx":
        prefix = "psx-r168-20261015-"
        require(source_id.startswith(prefix), "R168 PSX profile prefix differs")
        return "psx-r169-20261015-" + source_id[len(prefix):]
    old_suffix = "-owner-" + digest(old_engine_id.encode())[:12]
    new_suffix = "-owner-" + digest(new_engine_id.encode())[:12]
    require(source_id.endswith(old_suffix) and re.fullmatch(r"[a-zA-Z0-9-]+", source_id),
            "R168 native scoped mode ID differs")
    return source_id[:-len(old_suffix)] + new_suffix


def prepare(catalog, profiles, modes, manifest, received, r168_receipt, operator=None):
    operator = operator or load_profiles_tool()
    require(r168_receipt.get("serverApplied") is True and
            r168_receipt.get("acceptedKitSha256") == R168_KIT_SHA256,
            "Applied R168 authority receipt required")
    previous = r168_receipt["publication"]
    require((len(manifest["engines"]), len(profiles), len(modes["modes"])) ==
            (previous["engineCount"], previous["profileCount"], previous["modeCount"]),
            "Current authority changed since R168; reconcile before registration")
    require(catalog.get("revision") == previous["catalogRevision"] and
            len(catalog["items"]) == previous["catalogItems"],
            "Current catalog changed since R168")
    operator.validate_manifest(manifest)
    operator.validate_modes(modes)
    incoming = received["incoming.json"]
    runtime = received["engines-apk.json"]
    policies = received["profile-policies.json"]["profiles"]
    expected_old = r168_receipt["exactEngineIds"]
    require(set(expected_old) == set(NEW_IDS) and len(incoming["engines"]) == 4,
            "Exactly four R168 and R169 owners required")
    all_engines = {entry["engineId"]: entry for entry in manifest["engines"]}
    source_engines = {entry["platform"]: entry for entry in manifest["engines"]
                      if entry["engineId"] in expected_old.values()}
    new_engines = {entry["platform"]: entry for entry in incoming["engines"]}
    runtime_engines = {entry["platform"]: entry for entry in runtime["engines"]
                       if entry["platform"] in NEW_IDS}
    require(set(source_engines) == set(new_engines) == set(runtime_engines) == set(policies) == set(NEW_IDS),
            "Missing R168/R169 owner or policy")
    for platform, engine in new_engines.items():
        old = source_engines[platform]
        require(old["engineId"] == expected_old[platform] and engine["engineId"] == NEW_IDS[platform]
                and engine == runtime_engines[platform] and engine["engineId"] not in all_engines,
                "Owner identity changed or R169 already registered")
        retained = lambda value: {key: item for key, item in value.items()
                                  if key not in {"engineId", "runtimeSha256"}}
        require(retained(old) == retained(engine), "R169 native policy/core/options changed")
        policy = policies[platform]
        require(policy["engineId"] == engine["engineId"] and
                policy["coreSha256"] == engine["coreSha256"] and
                policy["runtimeSha256"] == engine["runtimeSha256"],
                "R169 controller policy does not match engine")
        config = operator.profile_configuration(engine, policy["controllerProfile"],
                                                policy["maximumPlayers"])
        require(config is not None and operator.profile_digest(config) == policy["profileSha256"],
                "R169 Android controller fingerprint differs")
    merged_manifest = deepcopy(manifest)
    merged_manifest["engines"].extend(deepcopy(incoming["engines"]))
    operator.validate_manifest(merged_manifest)

    catalog_items = {item["itemId"]: item for item in catalog["items"]}
    require(len(catalog_items) == len(catalog["items"]), "Duplicate current catalog identity")
    prior_profiles = {(row["engineId"], row["itemId"], row["contentSha256"], row["profileId"]): row
                      for row in profiles if row.get("engineId") in expected_old.values()}
    added_modes = []
    counts = {platform: 0 for platform in NEW_IDS}
    old_mode_keys = {(row["itemId"], row["contentSha256"], row["profileId"])
                     for row in modes["modes"]}
    for mode in modes["modes"]:
        owner_ids = mode.get("engineIds", [])
        if len(owner_ids) != 1 or owner_ids[0] not in expected_old.values():
            continue
        platform = mode["platform"]
        require(platform in NEW_IDS and owner_ids == [expected_old[platform]],
                "R168 mode has ambiguous owner")
        item = catalog_items.get(mode["itemId"])
        require(item is not None and item["contentSha256"] == mode["contentSha256"] and
                operator.platform(item["platform"]) == platform,
                "R168 catalog edition/content differs")
        prior = prior_profiles.get((owner_ids[0], mode["itemId"], mode["contentSha256"], mode["profileId"]))
        require(prior is not None and all(prior[key] == mode[key] for key in MODE_FIELDS) and
                prior["coreSha256"] == source_engines[platform]["coreSha256"] and
                prior["runtimeSha256"] == source_engines[platform]["runtimeSha256"] and
                prior["profileSha256"] == policies[platform]["profileSha256"],
                "R168 active profile/mode policy differs")
        cloned = deepcopy(mode)
        cloned["profileId"] = scoped_profile_id(mode["profileId"], platform,
                                                owner_ids[0], NEW_IDS[platform])
        cloned["engineIds"] = [NEW_IDS[platform]]
        key = (cloned["itemId"], cloned["contentSha256"], cloned["profileId"])
        require(key not in old_mode_keys, "R169 scoped mode collision")
        added_modes.append(cloned)
        counts[platform] += 1
    require(counts == EXPECTED_MODES, "R168 reviewed mode set changed")
    psx_modes = [m for m in added_modes if m["platform"] == "psx"]
    psx_approved = sum(m["approved"] and m["maximumPlayers"] == 2 for m in psx_modes)
    psx_pending = sum(not m["approved"] for m in psx_modes)
    psx_local = sum(m["maximumPlayers"] == 1 for m in psx_modes)
    require((psx_approved, psx_pending, psx_local) == (23, 17, 53),
            "PSX reviewed approvals, refusals or local classifications changed")
    pilot = [row for row in added_modes if row["itemId"] == PILOT and row["platform"] == "psx"
             and row["mode"] == "versus"]
    require(len(pilot) == 1 and pilot[0]["contentSha256"] == PILOT_CONTENT and
            pilot[0]["approved"] is True and pilot[0]["allowedPlayerCounts"] == [2] and
            pilot[0]["controllerProfile"] == "standard-2p-v1",
            "Capcom vs. SNK exact VS 2P tuple is not approved")
    prepared_modes = deepcopy(modes)
    prepared_modes["modes"].extend(added_modes)
    operator.validate_modes(prepared_modes)
    prepared_profiles = operator.prepare(catalog["items"], profiles, incoming, prepared_modes)
    require(prepared_profiles[:len(profiles)] == profiles and
            len(prepared_profiles) - len(profiles) == sum(EXPECTED_MODES.values()),
            "R169 profile projection failed or changed earlier records")
    by_new_key = {(p["engineId"], p["itemId"], p["contentSha256"], p["profileId"]): p
                  for p in prepared_profiles[len(profiles):]}
    require(len(by_new_key) == len(added_modes), "R169 profile projection is not one-to-one")
    for mode in added_modes:
        key = (mode["engineIds"][0], mode["itemId"], mode["contentSha256"], mode["profileId"])
        profile = by_new_key.get(key)
        require(profile is not None and all(profile[field] == mode[field] for field in MODE_FIELDS),
                "R169 projected profile differs from reviewed mode")
    mode_bytes = len(operator.encoded(prepared_modes))
    profile_compact_bytes = len(operator.encoded(prepared_profiles))
    manifest_compact_bytes = len(operator.encoded(merged_manifest))
    profile_indented_bytes = len((json.dumps(prepared_profiles, ensure_ascii=False,
                                             indent=2) + "\n").encode("utf-8"))
    require(mode_bytes <= operator.MAXIMUM_PREPARED_MODE_BYTES and
            profile_indented_bytes <= operator.MAXIMUM_PROFILE_REGISTRY_BYTES and
            manifest_compact_bytes <= operator.MAXIMUM_TRUSTED_MANIFEST_BYTES,
            "R169 registry exceeds coordinated capacity")
    audit = dict(schema="station.r169.server-preflight.v1", serverApplied=False,
                 kitSha256=KIT_SHA256, apkSha256=APK_SHA256,
                 baselineRevision=previous["catalogRevision"], baselineAppliedR168=True,
                 previousEngineCount=len(manifest["engines"]), previousProfileCount=len(profiles),
                 previousModeCount=len(modes["modes"]), enginesAdded=4,
                 profilesAdded=len(prepared_profiles)-len(profiles), modesAdded=len(added_modes),
                 newEngineIds=NEW_IDS, modeCounts=counts,
                 psxApproved2p=psx_approved, psxPending=psx_pending,
                 psxLocalOnly=psx_local,
                 capcomVs2pProfileId=pilot[0]["profileId"],
                 modesCompactBytes=mode_bytes, profilesCompactBytes=profile_compact_bytes,
                 profilesIndentedBytes=profile_indented_bytes,
                 manifestCompactBytes=manifest_compact_bytes,
                 modesByteLimit=operator.MAXIMUM_PREPARED_MODE_BYTES,
                 profilesByteLimit=operator.MAXIMUM_PROFILE_REGISTRY_BYTES,
                 manifestByteLimit=operator.MAXIMUM_TRUSTED_MANIFEST_BYTES,
                 candidateSha256={"manifest": digest(operator.encoded(merged_manifest)),
                                  "profiles": digest(operator.encoded(prepared_profiles)),
                                  "modes": digest(operator.encoded(prepared_modes))},
                 previousBindingsPreserved=True, n64LogicChanged=False,
                 physicalAndroidGameplayValidated=False)
    return merged_manifest, prepared_profiles, prepared_modes, audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("kit-zip", "r168-receipt", "catalog", "profiles", "modes", "manifest",
                 "out-manifest", "out-profiles", "out-modes", "out-audit"):
        parser.add_argument("--" + name, type=Path, required=True)
    for name in ("catalog", "profiles", "modes", "manifest"):
        parser.add_argument("--r168-" + name + "-sha256", required=True)
    args = parser.parse_args()
    inputs = [args.kit_zip, args.r168_receipt, args.catalog, args.profiles,
              args.modes, args.manifest]
    outputs = [args.out_manifest, args.out_profiles, args.out_modes, args.out_audit]
    check_output_paths(inputs, outputs)
    baseline = {name: getattr(args, "r168_" + name + "_sha256")
                for name in ("catalog", "profiles", "modes", "manifest")}
    operator = load_profiles_tool()
    limits = {"catalog": 32 * 1024 * 1024,
              "profiles": operator.MAXIMUM_PROFILE_REGISTRY_BYTES,
              "modes": operator.MAXIMUM_PREPARED_MODE_BYTES,
              "manifest": operator.MAXIMUM_TRUSTED_MANIFEST_BYTES}
    authority = {name: read_bounded(getattr(args, name), limits[name])
                 for name in baseline}
    require(all(re.fullmatch(r"[0-9a-f]{64}", value) for value in baseline.values()) and
            all(digest(authority[name]) == baseline[name] for name in baseline),
            "Current authority differs from the private applied R168 hashes")
    received = verify_kit(args.kit_zip)
    result = prepare(json.loads(authority["catalog"]),
                     json.loads(authority["profiles"]),
                     json.loads(authority["modes"]),
                     json.loads(authority["manifest"]), received,
                     read_json(args.r168_receipt, 16 * 1024), operator=operator)
    for target, value in zip(outputs, result):
        require(target.parent.is_dir(), "Output directory must exist: " + str(target.parent))
        require(not target.exists() and not target.is_symlink(),
                "Refusing to overwrite output or follow a symlink: " + str(target))
        content = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8") if target == args.out_audit else operator.encoded(value)
        target.write_bytes(content)
        require(target.read_bytes() == content, "Prepared output write differs: " + str(target))
    print(json.dumps(result[-1], ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
