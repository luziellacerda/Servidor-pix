"""Prepare exact reviewed modes for final real-owner engines, without approval inference.

Read-only inputs are the operator's current qualified catalog, frozen approved
profiles and prepared modes. Old rows are preserved. PSX needs an explicit
per-edition HLE review ledger; old RetroArch approval is never carried over.
No credentials, game bytes, publication, server restart or device operation.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
from urllib.parse import urlsplit

from build_incoming_manifest import incoming

FIELDS=("profileId","maximumPlayers","allowedPlayerCounts","approved",
        "controllerProfile","mode","modeTitle","instructions","sources")
NATIVE={"n64","gamecube","wii"}


def load(path):
    spec=importlib.util.spec_from_file_location("explicit_mode_operator",path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    if "requireExplicitEngineModes" not in path.read_text(encoding="utf-8"):
        raise ValueError("The explicit-mode operator patch is required before preparation")
    return module


def scoped_id(profile_id,engine_id):
    # This creates a binding ID, never a new gameplay mode or content identity.
    suffix="-owner-"+hashlib.sha256(engine_id.encode("utf-8")).hexdigest()[:12]
    retained=profile_id
    while len((retained+suffix).encode("utf-16-le"))//2>64:
        retained=retained[:-1]
    return retained+suffix


def https_evidence(value):
    if not isinstance(value,list) or not 1<=len(value)<=8:return False
    for source in value:
        if not isinstance(source,str) or len(source)>500:return False
        try:parsed=urlsplit(source)
        except ValueError:return False
        if parsed.scheme!="https" or not parsed.hostname or parsed.username is not None:return False
    return True


def merge(module,catalog,existing_profiles,old_modes,runtime,decisions=None,*,migrate_existing_native_profiles=True):
    manifest,policies=incoming(runtime)
    module.validate_modes(old_modes)
    if not isinstance(catalog,dict) or not isinstance(catalog.get("items"),list) or not isinstance(existing_profiles,list):
        raise ValueError("Current qualified catalog/profile input required")
    by_platform={e["platform"]:e for e in manifest["engines"]}
    qualified={}
    issues=[]
    for row in catalog["items"]:
        system=module.platform(row["platform"])
        if system not in by_platform or not row.get("catalogVisible",True):continue
        identity=row.get("contentSha256")
        if not module.hash_value(identity):
            issues.append(dict(itemId=row["itemId"],platform=system,reason="content-identity-pending"));continue
        extension=Path(row["artifact"]["launchPath"]).suffix[1:].lower()
        if extension not in by_platform[system]["extensions"]:
            issues.append(dict(itemId=row["itemId"],platform=system,reason="launch-extension-unsupported"));continue
        if system=="psx" and extension=="cue" and row.get("contentIdentityScheme")!="cue-set-v1":
            issues.append(dict(itemId=row["itemId"],platform=system,reason="cue-set-identity-pending"));continue
        if row["itemId"] in qualified:raise ValueError("Duplicate qualified catalog identity")
        qualified[row["itemId"]]=row
    output=copy.deepcopy(old_modes)
    keys={(r["itemId"],r["contentSha256"],r["profileId"]):r for r in output["modes"]}
    added=[]
    def append(source,origin):
        row=qualified.get(source["itemId"])
        if row is None or source.get("contentSha256")!=row["contentSha256"] or module.platform(source.get("platform",""))!=module.platform(row["platform"]):
            issues.append(dict(itemId=source["itemId"],reason="catalog-binding-mismatch",origin=origin));return
        system=module.platform(row["platform"]);engine=by_platform[system]
        if origin=="explicit-per-edition-review-ledger" and any(key in source and source[key]!=engine[key] for key in ("engineId","coreSha256","runtimeSha256")):
            raise ValueError("Explicit review ledger identifies a different final engine")
        controller=policies[system]["controllerProfile"]
        if source["controllerProfile"]!=controller or source["maximumPlayers"]>engine["maximumPlayers"]:
            issues.append(dict(itemId=source["itemId"],reason="controller-policy-mismatch",origin=origin));return
        mode={k:copy.deepcopy(source[k]) for k in FIELDS}
        mode.update(itemId=row["itemId"],contentSha256=row["contentSha256"],platform=system,
            profileId=scoped_id(source["profileId"],engine["engineId"]),engineIds=[engine["engineId"]])
        module.validate_modes(dict(schemaVersion=1,modes=[mode]))
        key=(mode["itemId"],mode["contentSha256"],mode["profileId"])
        if key in keys:
            if keys[key]!=mode:raise ValueError("Scoped binding identity collision; explicit review required")
            return
        keys[key]=mode;output["modes"].append(mode);added.append(dict(itemId=row["itemId"],platform=system,profileId=mode["profileId"],
            sourceProfileId=source["profileId"],engineId=engine["engineId"],approved=mode["approved"],origin=origin))
    # A fixed review ledger can be the only source of renewed bindings. Existing
    # scoped profiles still remain in the output prefix and in the preview.
    # Migrating them as well would append a second owner suffix to the same mode.
    for source in (existing_profiles if migrate_existing_native_profiles else []):
        system=module.platform(source.get("platform",""))
        if system not in NATIVE or source.get("approved") is not True:continue
        policy=policies[system]
        if source.get("coreSha256")!=policy["coreSha256"] or source.get("profileSha256")!=policy["profileSha256"] or source.get("controllerProfile")!=policy["controllerProfile"]:
            issues.append(dict(itemId=source["itemId"],platform=system,reason="prior-profile-not-the-same-real-owner-policy"));continue
        if not https_evidence(source.get("sources")):
            issues.append(dict(itemId=source["itemId"],platform=system,reason="prior-mode-evidence-pending"));continue
        append(source,"previous-exact-approved-native-profile")
    decisions=decisions or dict(schemaVersion=1,modes=[])
    if not isinstance(decisions,dict) or decisions.get("schemaVersion")!=1 or not isinstance(decisions.get("modes"),list):
        raise ValueError("Explicit per-edition review ledger required")
    for source in decisions["modes"]:
        system=module.platform(source.get("platform",""))
        if system not in by_platform:raise ValueError("Offline/other systems are outside this enrollment")
        if not https_evidence(source.get("sources")):raise ValueError("Explicit game-mode sources required")
        if system=="psx":
            if source.get("approved") is True and (source.get("biosPolicy")!="HLE-per-game-approved" or not https_evidence(source.get("reviewEvidence")) or source.get("maximumPlayers")!=2 or source.get("allowedPlayerCounts")!=[2]):
                raise ValueError("PSX requires explicit per-edition HLE evidence and two distinct simultaneous human inputs")
        append(source,"explicit-per-edition-review-ledger")
    module.validate_modes(output)
    if output["modes"][:len(old_modes["modes"])]!=old_modes["modes"]:
        raise ValueError("Previous prepared modes were modified")
    # Preview ONLY the new engines against the untouched approved-profile prefix.
    preview=module.prepare(catalog["items"],existing_profiles,manifest,output)
    if preview[:len(existing_profiles)]!=existing_profiles:raise ValueError("Previous profiles were modified")
    bound_items={r["itemId"] for r in output["modes"] if r["itemId"] in qualified and r["contentSha256"]==qualified[r["itemId"]]["contentSha256"] and any(e["engineId"] in r.get("engineIds",[]) for e in manifest["engines"])}
    for item_id,row in qualified.items():
        if item_id not in bound_items:issues.append(dict(itemId=item_id,platform=module.platform(row["platform"]),reason="explicit-reviewed-mode-pending"))
    summary=dict(schemaVersion=1,existingPreparedModesPreserved=len(old_modes["modes"]),addedScopedModes=len(added),
        existingProfilesPreserved=len(existing_profiles),previewProfilesAdded=len(preview)-len(existing_profiles),
        finalEngineIds=[e["engineId"] for e in manifest["engines"]],bindingsAdded=added,pendingOrSkipped=issues,
        inventedContentIdentity=False,previousPsxApprovalsMigrated=False,unknownGamesApproved=False,
        published=False,deployed=False,physicalGameplayValidated=False)
    return output,summary


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for key in ("catalog","profiles","modes","runtime-receipt","profiles-module","output","audit-json"):
        p.add_argument("--"+key,type=Path,required=True)
    p.add_argument("--decisions",type=Path)
    a=p.parse_args()
    module=load(a.profiles_module)
    read=lambda path:json.loads(path.read_bytes())
    result,receipt=merge(module,read(a.catalog),read(a.profiles),read(a.modes),read(a.runtime_receipt),read(a.decisions) if a.decisions else None)
    inputs={x.resolve() for x in (a.catalog,a.profiles,a.modes,a.runtime_receipt,a.profiles_module)}
    if a.decisions:inputs.add(a.decisions.resolve())
    if a.output.resolve() in inputs or a.audit_json.resolve() in inputs or a.audit_json.resolve()==a.output.resolve():
        raise ValueError("Preparation must not overwrite an input registry")
    a.output.parent.mkdir(parents=True,exist_ok=True)
    a.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    receipt["inputSha256"]={key:hashlib.sha256(path.read_bytes()).hexdigest() for key,path in (("catalog",a.catalog),("profiles",a.profiles),("modes",a.modes),("runtimeReceipt",a.runtime_receipt),("profilesOperator",a.profiles_module))}
    a.audit_json.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in receipt.items() if k not in ("bindingsAdded","pendingOrSkipped","inputSha256")}))


if __name__=="__main__":main()
