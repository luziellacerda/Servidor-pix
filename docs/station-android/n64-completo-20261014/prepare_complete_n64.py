"""Bind the complete N64 manual review to an exact received Android engine.

Read-only preparation. The canonical local registrar still verifies the actual
APK binaries and an unchanged live baseline before publishing anything.
This supplements the frozen R139 kit review; it never rewrites that evidence.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys


def prepare(catalog, profiles, old_modes, runtime, ledger, operator, builder, merger):
    if (ledger.get('schema') != 'station.n64.complete-edition-mode-review.v1'
            or ledger.get('pendingReviewEditions') != 0
            or ledger.get('physicalAndroidGameplayValidated') is not False):
        raise ValueError('A complete documentary review with separate physical acceptance is required')
    entries = ledger.get('entries', [])
    if len(entries) != ledger.get('totalEditions') or len(entries) != ledger.get('reviewedEditions'):
        raise ValueError('Review coverage differs')
    actual = {r['itemId']: r for r in catalog['items']
              if operator.platform(r['platform']) == 'n64' and r.get('catalogVisible', True)}
    if len({e['itemId'] for e in entries}) != len(entries) or set(actual) != {e['itemId'] for e in entries}:
        raise ValueError('Every visible N64 edition needs its own review; additions need a new review')
    runtime = dict(schemaVersion=1, engines=[copy.deepcopy(e) for e in runtime['engines']
                                           if e['platform'] in builder.POLICY])
    incoming, policies = builder.incoming(runtime)
    engine = next(e for e in incoming['engines'] if e['platform'] == 'n64')
    decisions = []
    for entry in entries:
        row = actual[entry['itemId']]
        manual = entry['originalPublisherManual']
        if (entry.get('reviewState') != 'reviewed' or entry.get('editionHeaderVerified') is not True
                or row.get('contentSha256') != entry['contentSha256']
                or not operator.hash_value(manual.get('sha256'))
                or not merger.https_evidence([manual.get('url')])
                or not entry['modes']):
            raise ValueError('Unreviewed or changed ROM identity / missing primary manual')
        counts = sorted({n for m in entry['modes'] for n in m['allowedPlayerCounts']})
        if (max(m['maximumPlayers'] for m in entry['modes']) != entry['maximumHumanPlayers']
                or counts != entry['onlineHumanCounts']
                or (entry['classification'] == 'single-player') != (not counts)):
            raise ValueError('Mode counts disagree with the documentary decision')
        for source in entry['modes']:
            if (source.get('approved') is not True or source['controllerProfile'] != 'direct-four-ports-v1'
                    or manual['url'] not in source['sources']):
                raise ValueError('Every mode needs the reviewed controller policy and original manual')
            decision = copy.deepcopy(source)
            decision.update(itemId=row['itemId'], contentSha256=row['contentSha256'], platform='n64',
                            **{k: engine[k] for k in ('engineId', 'coreSha256', 'runtimeSha256')})
            decisions.append(decision)
    if len(decisions) != ledger['modeCount']:
        raise ValueError('Incomplete reviewed-mode ledger')
    document = dict(schemaVersion=1, modes=decisions)
    operator.validate_modes(document)
    merged, receipt = merger.merge(operator, catalog, profiles, old_modes, runtime, document,
                                   migrate_existing_native_profiles=False)
    receipt.update(completeN64EditionCount=len(entries), completeN64ModeCount=len(decisions),
                   source='complete-original-manual-review-20261014',
                   targetN64EngineId=engine['engineId'], genericModesApproved=False,
                   physicalAndroidGameplayValidated=False)
    if any(x['platform'] != 'n64' for x in receipt['bindingsAdded']):
        raise ValueError('This review may only add N64 modes')
    return incoming, dict(schemaVersion=1, profiles=policies, exactModeApprovalRequired=True), document, merged, receipt


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('catalog', 'profiles', 'modes', 'runtime-receipt', 'review-ledger', 'profiles-module',
                'r128-operator-directory', 'output'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    sys.path.insert(0, str(args.r128_operator_directory))
    operator = load('complete_n64_profiles', args.profiles_module)
    builder = load('complete_n64_manifest', args.r128_operator_directory / 'build_incoming_manifest.py')
    merger = load('complete_n64_merge', args.r128_operator_directory / 'merge_scoped_modes.py')
    read = lambda p: json.loads(p.read_bytes())
    result = prepare(read(args.catalog), read(args.profiles), read(args.modes), read(args.runtime_receipt),
                     read(args.review_ledger), operator, builder, merger)
    args.output.mkdir(mode=0o700, parents=True, exist_ok=False)
    for name, value in zip(('incoming.json', 'profile-policies.json', 'reviewed-decisions.json',
                            'reviewed-modes.json', 'mode-merge-receipt.json'), result):
        (args.output / name).write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':')) + '\n')
    receipt = result[-1]
    print(json.dumps({k: receipt[k] for k in ('completeN64EditionCount', 'completeN64ModeCount',
                     'addedScopedModes', 'targetN64EngineId', 'physicalAndroidGameplayValidated')}))


if __name__ == '__main__':
    main()
