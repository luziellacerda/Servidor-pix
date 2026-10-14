"""Build the v3 admin manifest from the ACTUAL final runtime/package receipt.

Engine IDs and runtime hashes are derived from the supplied final receipt, not
from R127 literals. Configuration/options remain pinned and are checked by the
canonical hashes consumed by the Android client. No game approval is invented.
"""
import argparse
import hashlib
import json
from pathlib import Path

POLICY = {
    "n64": ("direct-four-ports-v1", 4,
        "688d519ae641911804fe3286e6b5f50cc7f8c06c42ccb1be36781f0fb067b83d"),
    "gamecube": ("dolphin-gamecube-four-ports-v1", 4,
        "89dc579c37afab7731a2d7322ab7fcf70ed0afa86ba7e491a605c45cd5443fc1"),
    "wii": ("dolphin-wii-remotes-four-v1", 4,
        "cf3b214d63ce324a82b331fc96a95c6f35e8cc1c7b6bc2bcd659cf35743ceaed"),
    "psx": ("standard-2p-v1", 2,
        "81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a"),
}


def canonical(config):
    ordered = {key: config[key] for key in ("schemaVersion", "controllerProfile", "devices", "coreOptions")}
    # Android JSONObject.quote escapes '/', including the slash in SN64/1.
    return json.dumps(ordered, ensure_ascii=False, separators=(",", ":"), allow_nan=False).replace("/", "\\/").encode("utf-8")


def incoming(runtime, package=None):
    import copy
    engines = copy.deepcopy(runtime["engines"])
    if len(engines) != 4 or {e["platform"] for e in engines} != set(POLICY):
        raise ValueError("Exactly the four final real-owner engine receipts are required")
    profiles = {}
    for e in engines:
        name, maximum, expected = POLICY[e["platform"]]
        if e.get("controllerProfiles") != [name] or e.get("maximumPlayers") != maximum or e.get("recoveryProtocol") != "station-stream.v3" or e.get("launchReady") is not True:
            raise ValueError("Actual final Android engine policy differs")
        if not e["engineId"].endswith(e["runtimeSha256"][:12]):
            raise ValueError("Engine ID does not bind the final runtime")
        config = dict(schemaVersion=1, controllerProfile=name, devices=[1]*maximum, coreOptions=e["options"])
        value = hashlib.sha256(canonical(config)).hexdigest()
        if value != expected:
            raise ValueError("Canonical native policy differs from the approved source delivery")
        e["controllerProfiles"] = [dict(controllerProfile=name, maximumPlayers=maximum, configuration=config)]
        e["requireExplicitEngineModes"] = True
        profiles[e["platform"]] = dict(controllerProfile=name, profileSha256=value,
            maximumPlayers=maximum, engineId=e["engineId"], coreSha256=e["coreSha256"], runtimeSha256=e["runtimeSha256"])
    if package is not None and sorted(package["engineIds"]) != sorted(e["engineId"] for e in engines):
        raise ValueError("Runtime receipt does not identify the actual final package")
    return dict(schemaVersion=1, engines=engines), profiles


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--runtime-receipt", type=Path, required=True)
    p.add_argument("--package-receipt", type=Path)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--profile-receipt", type=Path, required=True)
    a = p.parse_args()
    runtime = json.loads(a.runtime_receipt.read_bytes())
    package = json.loads(a.package_receipt.read_bytes()) if a.package_receipt else None
    if package is not None and package.get("identityReceiptSha256") is not None and hashlib.sha256(a.runtime_receipt.read_bytes()).hexdigest()!=package["identityReceiptSha256"]:
        raise ValueError("Runtime receipt differs from the final package identity seal")
    manifest, profiles = incoming(runtime, package)
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    receipt = dict(schemaVersion=1, profiles=profiles, exactModeApprovalRequired=True,
        legacyRegistryChanged=False, sourceRuntimeReceiptSha256=hashlib.sha256(a.runtime_receipt.read_bytes()).hexdigest())
    if package is not None:
        receipt.update(revision=package["revision"], apkSha256=package["apkSha256"], apkBytes=package["apkBytes"])
    a.profile_receipt.write_text(json.dumps(receipt, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(dict(engineIds=[e["engineId"] for e in manifest["engines"]], profileHashes={k:v["profileSha256"] for k,v in profiles.items()})))


if __name__ == "__main__":
    main()
