"""Re-evaluate every published PSX decision against the exact R167 identity.

This pure preparer grants no approval from title/player metadata. The original
per-edition real-core evidence remains frozen. The received kit proves the same
core, owner, options, control policy and native sources, with new runtime identity
bindings. This does not assert byte equality of the two runtime code sections or
Android gameplay on R167.
"""
from copy import deepcopy

OLD_ENGINE = 'pcsx-rearmed-ff81ed17-player-v3-1b315b86b4a7'
NEW_ENGINE = 'pcsx-rearmed-ff81ed17-player-v3-4d5c4cb8f609'
OLD_RUNTIME = '1b315b86b4a789247a355f09eb04bf41027e1cb8e5941019da6ade486835dd36'
NEW_RUNTIME = '4d5c4cb8f609f5d1eb49a002d137d68172d03f59505443071d02903e25630991'
FIELDS = ('profileId', 'maximumPlayers', 'allowedPlayerCounts', 'approved',
          'controllerProfile', 'mode', 'modeTitle', 'instructions', 'sources')


def need(ok, why):
    if not ok:
        raise ValueError(why)


def prepare(operator, old_policy, catalog, profiles, modes, manifest, ledger, tests, kit_review):
    need(kit_review.get('passed') is True and kit_review.get('revision') == 'R167'
         and kit_review.get('originalKitSha256') == '82cf0bff9f4ec3083fc395cd6cf38d6d9f227df6aac06b09b6d97882c9947477'
         and kit_review.get('psxNativeSourcesEqualR165') is True
         and kit_review.get('runtimeIdentityAndFrozenSourceHashesVerified') is True
         and kit_review.get('coreAnd21DependenciesUnchanged') is True,
         'Exact independent R167 kit/policy/source verification required')
    engines = {e['engineId']: e for e in manifest['engines']}
    need(OLD_ENGINE in engines and NEW_ENGINE in engines, 'Both immutable received identities required')
    before, current = engines[OLD_ENGINE], engines[NEW_ENGINE]
    retained = lambda value: {k: v for k, v in value.items() if k not in {'engineId', 'runtimeSha256'}}
    need(retained(before) == retained(current) and before['runtimeSha256'] == OLD_RUNTIME
         and current['runtimeSha256'] == NEW_RUNTIME, 'PSX execution/control policy changed')
    config = operator.profile_configuration(current, old_policy.CONTROLLER, 2)
    need(config is not None and config['devices'] == [1, 1]
         and operator.profile_digest(config) == old_policy.PROFILE_SHA,
         'Exact two-port Android controller fingerprint required')
    items = {r['itemId']: r for r in catalog['items'] if operator.platform(r['platform']) == 'psx'
             and r.get('catalogVisible', True)}
    entries = ledger['entries']
    need(ledger['engineId'] == OLD_ENGINE and len(entries) == len(items)
         and {e['itemId'] for e in entries} == set(items), 'All current PSX editions need exact source review')
    evidence = {q['itemId']: q for q in tests}
    need(len(evidence) == len(tests) and set(evidence) == set(items), 'Complete immutable core evidence required')
    published = {(p['itemId'], p['profileId']): p for p in profiles if p['engineId'] == OLD_ENGINE}
    output = deepcopy(modes)
    decisions = []
    existing = {(m['itemId'], m['contentSha256'], m['profileId']) for m in modes['modes']}
    for entry in entries:
        item = items[entry['itemId']]
        need(entry['engineId'] == OLD_ENGINE and entry['runtimeSha256'] == OLD_RUNTIME
             and entry['contentSha256'] == item.get('contentSha256') and entry['catalogName'] == item['name']
             and entry['contentIdentityScheme'] == item.get('contentIdentityScheme', 'launch-file-v1'),
             'Source edition/runtime/content binding changed')
        for source in entry['modes']:
            prior = published.get((entry['itemId'], source['profileId']))
            need(prior is not None and all(prior[k] == source[k] for k in FIELDS)
                 and prior['contentSha256'] == item['contentSha256']
                 and prior['coreSha256'] == old_policy.CORE_SHA
                 and prior['runtimeSha256'] == OLD_RUNTIME
                 and prior['profileSha256'] == old_policy.PROFILE_SHA,
                 'Exact active R164 decision differs from its reviewed evidence')
            approved = source['approved']
            if source['maximumPlayers'] == 2:
                need(source['allowedPlayerCounts'] == [2], 'Only two independent PSX standard ports supported')
                if approved:
                    old_policy.approval_evidence(entry, source, evidence[entry['itemId']])
                else:
                    need(bool(source['blockingFindings']), 'Unapproved mode needs its explicit preserved reason')
            else:
                need(source['maximumPlayers'] == 1 and source['allowedPlayerCounts'] == []
                     and source.get('classificationOnly') is True, 'Local decision must grant zero seats')
            mode = {k: deepcopy(source[k]) for k in FIELDS}
            need(mode['profileId'].startswith('psx-20261015-'), 'Exact original scoped mode ID required')
            mode['profileId'] = mode['profileId'].replace('psx-20261015-', 'psx-r167-20261015-', 1)
            mode.update(itemId=item['itemId'], contentSha256=item['contentSha256'], platform='psx',
                        engineIds=[NEW_ENGINE], approved=approved)
            need((mode['itemId'], mode['contentSha256'], mode['profileId']) not in existing,
                 'R167 binding already exists; reconcile instead of replaying')
            operator.validate_modes(dict(schemaVersion=1, modes=[mode]))
            output['modes'].append(mode)
            decisions.append(dict(itemId=item['itemId'], contentSha256=item['contentSha256'],
                                  sourceEngineId=OLD_ENGINE, sourceProfileId=source['profileId'],
                                  engineId=NEW_ENGINE, runtimeSha256=NEW_RUNTIME,
                                  profileSha256=old_policy.PROFILE_SHA, **{k: mode[k] for k in FIELDS},
                                  classificationOnly=source.get('classificationOnly', False),
                                  blockingFindings=source.get('blockingFindings', []),
                                  physicalAndroidGameplayValidated=False))
    operator.validate_modes(output)
    need(output['modes'][:len(modes['modes'])] == modes['modes'], 'Earlier modes changed')
    approved_modes = [m for m in decisions if m['approved'] and m['maximumPlayers'] == 2]
    receipt = dict(reviewedEditions=len(entries), decisions=len(decisions), approvedMultiplayerModes=len(approved_modes),
                   approvedMultiplayerEditions=len({m['itemId'] for m in approved_modes}),
                   pendingMultiplayerModes=sum(not m['approved'] for m in decisions),
                   localClassificationsWithoutSeats=sum(m['maximumPlayers'] == 1 for m in decisions),
                   exactEditionAndReceivedRuntimePolicyReevaluated=True, metadataUsedForApproval=False,
                   sourceRealCoreEvidenceReusedForIdenticalCoreAndOptions=True,
                   physicalAndroidGameplayValidated=False)
    return output, decisions, receipt
