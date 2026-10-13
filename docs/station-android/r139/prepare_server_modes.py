"""Carry exact current R138 edition bindings to the received R139 runtime.

Content, controllers, options, modes, counts, approvals and refusals remain exact.
Player metadata is not used to create an approval.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path

BASE_RUNTIME = '463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f'
NEW_RUNTIME = 'ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378'


def read(path):
    return json.loads(Path(path).read_bytes())


def build(module, catalog, profiles, current, modes, incoming):
    module.validate_manifest(current)
    module.validate_manifest(incoming)
    module.validate_modes(modes)
    originals = {e['engineId']: e for e in current['engines']}
    rows = {r['itemId']: r for r in catalog['items'] if r.get('catalogVisible', True)}
    result = copy.deepcopy(modes)
    added = []
    preserved_fields = ('coreSha256', 'platform', 'options', 'extensions',
                        'controllerProfiles', 'maximumPlayers', 'requireExplicitEngineModes')
    for engine in incoming['engines']:
        if engine['runtimeSha256'] != NEW_RUNTIME or not engine['launchReady'] or engine.get('recoveryProtocol') != 'station-stream.v3':
            raise ValueError('Exact received R139 v3 engine required')
        sources = [e for e in current['engines']
                   if e['runtimeSha256'] == BASE_RUNTIME and e['platform'] == engine['platform']
                   and e['coreSha256'] == engine['coreSha256']]
        if len(sources) != 1:
            raise ValueError('Unique current R138 engine required')
        source = sources[0]
        if any(source.get(k) != engine.get(k) for k in preserved_fields):
            raise ValueError('R139 cannot change the existing core/controller/options policy')
        for profile in profiles:
            if profile['engineId'] != source['engineId']:
                continue
            row = rows.get(profile['itemId'])
            if row is None or profile['contentSha256'] != row['contentSha256']:
                continue
            if module.platform(row['platform']) != engine['platform']:
                continue
            if Path(row['artifact']['launchPath']).suffix[1:].lower() not in engine['extensions']:
                continue
            configuration = module.profile_configuration(engine, profile['controllerProfile'], profile['maximumPlayers'])
            if configuration is None or hashlib.sha256(module.encoded(configuration)[:-1]).hexdigest() != profile['profileSha256']:
                raise ValueError('R138 controller binding does not match the R139 engine')
            if profile['coreSha256'] != engine['coreSha256'] or profile['runtimeSha256'] != BASE_RUNTIME:
                raise ValueError('Source profile binary identity differs')
            identity = '\n'.join([profile['itemId'], profile['contentSha256'], profile['profileId'], engine['engineId']])
            mode = {k: copy.deepcopy(profile[k]) for k in
                    ('itemId', 'contentSha256', 'platform', *module.MODE_FIELDS)}
            mode['profileId'] = 'r139-' + hashlib.sha256(identity.encode()).hexdigest()[:24]
            mode['engineIds'] = [engine['engineId']]
            result['modes'].append(mode)
            added.append(dict(itemId=mode['itemId'], engineId=engine['engineId'],
                              sourceProfileId=profile['profileId'], profileId=mode['profileId'],
                              approved=mode['approved'], profileSha256=profile['profileSha256']))
    if not added:
        raise ValueError('No exact current R138 bindings found')
    module.validate_modes(result)
    preview = module.prepare(catalog['items'], profiles, incoming, result)
    if preview[:len(profiles)] != profiles or len(preview) != len(profiles) + len(added):
        raise ValueError('Only explicitly preserved R138 bindings may be added')
    expected = {(r['itemId'], r['engineId'], r['profileId']): r for r in added}
    for p in preview[len(profiles):]:
        key = (p['itemId'], p['engineId'], p['profileId'])
        if key not in expected or p['approved'] != expected[key]['approved'] or p['profileSha256'] != expected[key]['profileSha256']:
            raise ValueError('Generated approval differs from the exact source binding')
    return result, dict(schemaVersion=1, profilesAdded=len(added),
                        previousProfilesPreserved=len(profiles), previousModesPreserved=len(modes['modes']),
                        approvedAdded=sum(r['approved'] for r in added), refusedAdded=sum(not r['approved'] for r in added),
                        exactR138BindingsPreserved=True, playerMetadataUsedForApproval=False,
                        androidGameplayValidated=False, bindings=added)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('catalog', 'profiles', 'current', 'modes', 'incoming', 'profiles-module', 'output', 'audit'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('trusted_profiles', args.profiles_module)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    result, audit = build(module, read(args.catalog), read(args.profiles), read(args.current), read(args.modes), read(args.incoming))
    for path, value in ((args.output, result), (args.audit, audit)):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({k: v for k, v in audit.items() if k != 'bindings'}))


if __name__ == '__main__':
    main()
