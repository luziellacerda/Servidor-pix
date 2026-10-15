"""Prepare exact PSX edition/mode bindings for the received R164 owner.

This module neither publishes data nor grants modes from descriptive player
counts. A local classification grants no multiplayer seats. Every multiplayer
approval needs an explicit primary mode finding and the per-edition real-core
qualification selected in the review ledger.
"""
from copy import deepcopy
from urllib.parse import urlsplit

ENGINE_ID = "pcsx-rearmed-ff81ed17-player-v3-1b315b86b4a7"
CORE_SHA = "e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9"
RUNTIME_SHA = "1b315b86b4a789247a355f09eb04bf41027e1cb8e5941019da6ade486835dd36"
PROFILE_SHA = "81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a"
CONTROLLER = "standard-2p-v1"
MODE_FIELDS = ("profileId", "maximumPlayers", "allowedPlayerCounts", "approved",
               "controllerProfile", "mode", "modeTitle", "instructions", "sources")


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def https_sources(values):
    if not isinstance(values, list) or not 1 <= len(values) <= 8:
        return False
    for value in values:
        if not isinstance(value, str) or len(value) > 500:
            return False
        try:
            parsed = urlsplit(value)
        except ValueError:
            return False
        if parsed.scheme != "https" or not parsed.hostname or parsed.username is not None:
            return False
    return True


def approval_evidence(entry, mode, evidence):
    """Fail closed on a missing, changed or merely simulated core finding."""
    require(entry.get("biosPolicy") == "HLE-per-game-approved", "Explicit per-edition HLE review required")
    require(entry.get("standardTwoPortsDocumented") is True, "Two distinct standard control ports required")
    require(https_sources(entry.get("reviewEvidence")), "Public review evidence required")
    require(mode.get("documentaryFinding") and https_sources(mode.get("sources")), "Primary game mode finding required")
    require(evidence.get("itemId") == entry["itemId"] and evidence.get("contentSha256") == entry["contentSha256"]
            and evidence.get("coreSha256") == CORE_SHA, "Qualification differs from the catalog edition/core")
    for key in ("passed", "actualReceivedArm64CoreExecuted", "contentIdentityVerified",
                "initialStateTransferred", "initialStateRamEqual", "freshStartLockstepPassed",
                "videoCheckpointsEqual", "bothStandardPortsPolledWithDistinctInputs", "exactPinnedHleObserved",
                "sourceStatPreserved", "recoveryStateAccepted", "recoveryRamInitiallyEqual",
                "recoveryReplayRamEqual", "recoveryReplayVideoEqual"):
        require(evidence.get(key) is True, "Real-core qualification failed or missing: " + key)
    require(evidence.get("discSerial") == entry.get("discSerial") and bool(entry.get("discSerial")),
            "The disc serial must be observed from the exact running core")
    require(entry.get("bootFrameVisuallyReviewed") is True and entry.get("bootFrameMeaningful") is True,
            "A video callback alone cannot approve a blank or stalled title")
    require(entry.get("runtimeReview") == "accepted-bounded-core-probe", "Explicit runtime review decision required")
    require(0 < evidence.get("stateBytes", 0) <= 16 * 1024 * 1024 and evidence.get("comparedFrames", 0) >= 2760,
            "Bounded actual state and meaningful frame run required")


def prepare(operator, catalog, profiles, modes, manifest, ledger, qualifications):
    operator.validate_manifest(manifest)
    operator.validate_modes(modes)
    engines = [e for e in manifest["engines"] if e["engineId"] == ENGINE_ID]
    require(len(engines) == 1, "Exact already registered PSX owner required")
    engine = engines[0]
    require(engine["platform"] == "psx" and engine["coreSha256"] == CORE_SHA
            and engine["runtimeSha256"] == RUNTIME_SHA and engine["maximumPlayers"] == 2
            and engine.get("requireExplicitEngineModes") is True, "Received owner policy differs")
    config = operator.profile_configuration(engine, CONTROLLER, 2)
    require(config is not None and config["devices"] == [1, 1]
            and operator.profile_digest(config) == PROFILE_SHA, "Android controller fingerprint differs")
    require(ledger.get("schema") == "station.psx.explicit-edition-mode-review.v1"
            and ledger.get("engineId") == ENGINE_ID, "Explicit current PSX review ledger required")
    items = {r["itemId"]: r for r in catalog["items"] if operator.platform(r["platform"]) == "psx"
             and r.get("catalogVisible", True)}
    entries = ledger.get("entries")
    require(isinstance(entries, list) and {e["itemId"] for e in entries} == set(items)
            and len(entries) == len(items), "Review must cover every exact visible PSX edition once")
    require(isinstance(qualifications, list), "Actual core qualification array required")
    tests = {q["itemId"]: q for q in qualifications}
    require(len(tests) == len(qualifications) and set(tests) == set(items),
            "Actual-core qualification must cover all current PSX editions exactly once")
    output = deepcopy(modes)
    present = {(m["itemId"], m["contentSha256"], m["profileId"]): m for m in output["modes"]}
    added = []
    for entry in entries:
        item = items[entry["itemId"]]
        require(entry["contentSha256"] == item.get("contentSha256") and entry["catalogName"] == item["name"],
                "Catalog content/name binding changed")
        require(entry.get("coverId") == item["coverId"] and entry.get("itemRevision") == item["revision"]
                and entry.get("metadata") == item.get("metadata", {}), "Catalog cover/revision/metadata binding changed")
        artifact = {k: item["artifact"][k] for k in ("fileName", "sizeBytes", "sha256", "format", "launchPath",
                    "expandedSizeBytes", "fileCount") if k in item["artifact"]}
        require(entry.get("artifact") == artifact, "Published artifact descriptor changed")
        require(entry.get("contentIdentityScheme", "launch-file-v1") == item.get("contentIdentityScheme", "launch-file-v1"),
                "Content identity scheme changed")
        require(entry.get("modes") and len(entry["modes"]) <= 12, "Explicit bounded game modes required")
        for source in entry["modes"]:
            mode = {key: deepcopy(source[key]) for key in MODE_FIELDS}
            require(mode["controllerProfile"] == CONTROLLER and https_sources(mode["sources"]), "Mode controller/source differs")
            require(mode["maximumPlayers"] in (1, 2), "PSX owner supports only two standard ports")
            if mode["maximumPlayers"] == 1:
                require(mode["allowedPlayerCounts"] == [] and source.get("classificationOnly") is True,
                        "Local classification must grant no online seats")
            else:
                require(mode["allowedPlayerCounts"] == [2], "Multitap or shared-controller modes are outside this owner")
                if mode["approved"]:
                    approval_evidence(entry, source, tests.get(entry["itemId"], {}))
            require(mode["profileId"].startswith("psx-20261015-") and len(mode["profileId"]) <= 64,
                    "Review binding ID must be immutable and scoped")
            mode.update(itemId=item["itemId"], contentSha256=item["contentSha256"], platform="psx", engineIds=[ENGINE_ID])
            operator.validate_modes(dict(schemaVersion=1, modes=[mode]))
            key = (mode["itemId"], mode["contentSha256"], mode["profileId"])
            if key in present:
                require(present[key] == mode, "Existing reviewed binding collision")
                continue
            present[key] = mode
            output["modes"].append(mode)
            added.append(mode)
    require(output["modes"][:len(modes["modes"])] == modes["modes"], "Earlier modes changed")
    operator.validate_modes(output)
    incoming = dict(schemaVersion=1, engines=[deepcopy(engine)])
    preview = operator.prepare(catalog["items"], profiles, incoming, output)
    require(preview[:len(profiles)] == profiles, "Earlier profiles changed")
    next_profiles = preview[len(profiles):]
    require(len(next_profiles) == len(added) and all(p["engineId"] == ENGINE_ID
            and p["profileSha256"] == PROFILE_SHA for p in next_profiles), "Unexpected profile projection")
    return incoming, output, dict(
        schema="station.psx.scoped-preparation.v1", exactReviewedEditions=len(entries),
        profilesAdded=len(next_profiles), modesAdded=len(added), enginesAdded=0,
        approvedMultiplayerModes=sum(p["approved"] and p["maximumPlayers"] == 2 for p in next_profiles),
        approvedMultiplayerEditions=len({p["itemId"] for p in next_profiles if p["approved"] and p["maximumPlayers"] == 2}),
        localClassifications=sum(p["maximumPlayers"] == 1 for p in next_profiles),
        pendingMultiplayerModes=sum(not p["approved"] and p["maximumPlayers"] == 2 for p in next_profiles),
        previousProfilesPreserved=len(profiles), previousModesPreserved=len(modes["modes"]),
        currentEngineId=ENGINE_ID, controllerProfileSha256=PROFILE_SHA,
        previousPsxApprovalsMigrated=False, metadataPlayerCountsUsedForApproval=False,
        coreProbeIsAndroidGameplay=False, physicalAndroidGameplayValidated=False, published=False)
