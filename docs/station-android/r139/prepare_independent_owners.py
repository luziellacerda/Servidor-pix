"""Bind received native owners to the already reviewed R133 game modes.

Read-only preparation. Real binaries are checked separately by the existing
administrative registrar. Historical generic/PSX profiles do not approve HLE.
"""
import argparse
import copy
import importlib.util
import json
import sys
from pathlib import Path


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def prepare(catalog, profiles, old_modes, installed_engines, provenance, research, operator, builder, merger):
    runtime = dict(schemaVersion=1, engines=[copy.deepcopy(e) for e in installed_engines["engines"]
                                          if e["platform"] in builder.POLICY])
    manifest, policies = builder.incoming(runtime)
    decisions = []
    fields = merger.FIELDS
    for reference in provenance["modes"]:
        item = catalog["items"][reference["catalogOrdinal"] - 1]
        system = operator.platform(item["platform"])
        if item["name"] != reference["name"] or system != reference["platform"]:
            raise ValueError("Current edition differs from reviewed ordinal/title")
        source_file = reference["sourceFile"]
        position = reference["sourceOrdinal"] - 1
        if source_file in ("profiles-complete.json", "server-prepared-modes.json"):
            source = profiles[position] if source_file == "profiles-complete.json" else old_modes["modes"][position]
            if (source["itemId"] != item["itemId"] or source["contentSha256"] != item["contentSha256"]
                    or source.get("approved") is not True):
                raise ValueError("Reviewed historical mode no longer binds this content")
            record = {key: copy.deepcopy(source[key]) for key in fields}
        else:
            entry = research[source_file]["entries"][position]
            if (entry["catalogOrdinal"] != reference["catalogOrdinal"] or entry["name"] != item["name"]
                    or entry["platform"] != system
                    or entry["classification"] not in ("simultaneous", "alternating-independent-ports")):
                raise ValueError("Documentary edition/mode differs from reviewed evidence")
            matches = [m for m in entry["modes"] if m["mode"] == reference["mode"]
                       and m["modeTitle"] == reference["modeTitle"]
                       and m["allowedPlayerCounts"] == reference["allowedPlayerCounts"]]
            if len(matches) != 1 or matches[0].get("adapterCompatible") is not True:
                raise ValueError("Exactly one reviewed compatible documentary mode required")
            source = matches[0]
            engine = next(e for e in runtime["engines"] if e["platform"] == system)
            if any(line not in engine["options"].splitlines() for line in source.get("requiredEngineOptions", [])):
                raise ValueError("Installed native owner lacks required game options")
            record = {key: copy.deepcopy(source[key]) for key in fields
                      if key not in ("approved", "controllerProfile", "sources")}
            record.update(approved=True, sources=copy.deepcopy(source.get("sources", entry["primarySources"])))
        if any(record[key] != reference[key] for key in ("maximumPlayers", "allowedPlayerCounts", "sources")):
            raise ValueError("Prepared mode exceeds or differs from published review")
        if system == "psx":
            raise ValueError("PSX needs its separate per-edition HLE ledger")
        record.update(itemId=item["itemId"], contentSha256=item["contentSha256"], platform=system,
                      controllerProfile=policies[system]["controllerProfile"])
        decisions.append(record)
    reviewed = dict(schemaVersion=1, modes=decisions)
    merged, receipt = merger.merge(operator, catalog, profiles, old_modes, runtime, reviewed,
                                   migrate_existing_native_profiles=False)
    if receipt["previewProfilesAdded"] != len(decisions):
        raise ValueError("Each reviewed decision must produce one current native owner binding")
    return manifest, policies, reviewed, merged, receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("catalog", "profiles", "modes", "installed-engines", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    docs = Path(__file__).resolve().parents[1]
    helpers = docs / "r128/operator"
    sys.path.insert(0, str(helpers))
    import build_incoming_manifest as builder
    import merge_scoped_modes as merger
    operator = load(docs / "online-plataformas-20261009/library-tools/station_online_profiles.py", "current_profile_operator")
    read = lambda path: json.loads(path.read_bytes())
    provenance = read(docs / "r133/catalog/reviewed-mode-provenance.json")
    filenames = {r["sourceFile"] for r in provenance["modes"]} - {"profiles-complete.json", "server-prepared-modes.json"}
    research = {name: read(docs / "r133/catalog" / name) for name in filenames}
    results = prepare(read(args.catalog), read(args.profiles), read(args.modes), read(args.installed_engines),
                      provenance, research, operator, builder, merger)
    inputs = {p.resolve() for p in (args.catalog, args.profiles, args.modes, args.installed_engines)}
    args.output.mkdir(mode=0o700, parents=True, exist_ok=True)
    names = ("incoming.json", "profile-policies.json", "reviewed-decisions.json", "reviewed-modes.json", "mode-merge-receipt.json")
    for name, value in zip(names, results):
        target = args.output / name
        if target.resolve() in inputs:
            raise ValueError("Preparation cannot replace an input")
        target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    receipt = results[-1]
    print(json.dumps({k: v for k, v in receipt.items() if k not in ("bindingsAdded", "pendingOrSkipped")}))


if __name__ == "__main__":
    main()
