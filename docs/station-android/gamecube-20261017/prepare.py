"""Add reviewed GameCube decisions for the existing, exact R172 engine.

Pure preparation only. Keep every previous registry entry and binary identity.
The authenticated operator compares this result with fresh protected inputs.
"""
from copy import deepcopy
import hashlib
import json

ENGINE = 'dolphin-5102a033-gamecube-v3-ff5113148661'
CORE = '3438ce727ab3a0d6f6fb4aa2a1b8068be5d17360e6c7aa0b8ff3594dbfde7c29'
RUNTIME = 'ff5113148661d4da2761a981d52aa21ff93082c056e5e95ac3de2cbc31841abb'
PROFILE = '89dc579c37afab7731a2d7322ab7fcf70ed0afa86ba7e491a605c45cd5443fc1'
CONTROLLER = 'dolphin-gamecube-four-ports-v1'


def need(ok, reason):
    if not ok:
        raise ValueError(reason)


def prepare(data, decisions, media, policy, registrar):
    need(media['passed'] and media['visibleItems'] == 20 and media['catalogItems'] == 21,
         'Complete, unchanged GameCube media audit required')
    policy.validate_manifest(data['engines'])
    policy.validate_modes(data['modes'])
    engine = next(e for e in data['engines']['engines'] if e['engineId'] == ENGINE)
    need(engine['platform'] == 'gamecube' and engine['coreSha256'] == CORE and
         engine['runtimeSha256'] == RUNTIME and engine['maximumPlayers'] == 4 and
         engine.get('requireExplicitEngineModes') is True and
         policy.profile_digest(policy.profile_configuration(engine, CONTROLLER, 4)) == PROFILE,
         'The exact R172 Dolphin/controller authority is required')
    items = {i['itemId']: i for i in data['catalog']['items'] if i['platform'] == 'gamecube'}
    audited = {i['itemId']: i for i in media['items']}
    need(set(items) == set(audited) and len(items) == 21 and data['catalog']['revision'] == media['catalogRevision'],
         'Catalog changed since the media audit')
    for identity, item in items.items():
        audit = audited[identity]
        need(item.get('catalogVisible', True) == audit['visible'], 'Catalog visibility changed')
        if audit['visible']:
            need(audit['contentMatch'] and audit['formatVerified'] and audit['stableSource'] and
                 item['contentSha256'] == audit['sha256'] == audit['expectedContentSha256'] and
                 item['artifact']['launchPath'].rsplit('.', 1)[-1].lower() == audit['format'],
                 'Edition/content/format changed')
    added_modes = decisions['modes']
    need(decisions['schemaVersion'] == 1 and decisions['engineId'] == ENGINE and len(added_modes) == 15,
         'Exact reviewed additions required')
    extended = deepcopy(data['modes'])
    extended['modes'].extend(deepcopy(added_modes))
    incoming = dict(schemaVersion=1, engines=[deepcopy(engine)])
    registrar.reviewed_modes(data['modes'], extended, incoming)
    for mode in added_modes:
        item = items.get(mode['itemId'])
        need(item and item.get('catalogVisible', True) and mode['platform'] == 'gamecube' and
             mode['contentSha256'] == item['contentSha256'] and mode['engineIds'] == [ENGINE] and
             mode['controllerProfile'] == CONTROLLER and mode['sources'],
             'Every decision needs the exact visible content, owner and documentary source')
    profiles = policy.prepare(data['catalog']['items'], data['profiles'], data['engines'], extended)
    need(profiles[:len(data['profiles'])] == data['profiles'] and len(profiles) == len(data['profiles']) + 15,
         'Only the fifteen reviewed profiles may be added')
    added = profiles[len(data['profiles']):]
    need(all(p['platform'] == 'gamecube' and p['engineId'] == ENGINE and p['profileSha256'] == PROFILE
             and p['coreSha256'] == CORE and p['runtimeSha256'] == RUNTIME for p in added),
         'Unexpected binary identity/platform/profile change')
    current = [p for p in profiles if p['engineId'] == ENGINE]
    need(len(current) == 24 and {p['itemId'] for p in current} ==
         {i['itemId'] for i in items.values() if i.get('catalogVisible', True)},
         'Every visible GameCube edition must have an explicit reviewed decision')
    playable = [p for p in current if p['approved'] and p['allowedPlayerCounts']]
    solo = [p for p in current if p['approved'] and p['maximumPlayers'] == 1]
    blocked = [p for p in current if not p['approved']]
    need(len(playable) == 10 and len({p['itemId'] for p in playable}) == 9 and
         len(solo) == 12 and len(blocked) == 2 and
         {p['mode'] for p in blocked} == {'gba-adventure', 'tingle-tuner-gba'} and
         sum(len(p['allowedPlayerCounts']) for p in playable) == 20,
         'Reviewed multiplayer/individual/GBA coverage differs')
    need(sum(p['approved'] and bool(p['allowedPlayerCounts']) for p in added) == 1 and
         any(p['mode'] == 'tech-com-force' and p['allowedPlayerCounts'] == [2] and p['approved'] for p in added),
         'Only the missing Terminator cooperative mode may add online seats')
    result = dict(passed=True, apkRevision='R172', apkChanged=False, enginesAdded=0, profilesAdded=15,
                  modesAdded=15, existingProfilesPreserved=len(data['profiles']),
                  existingModesPreserved=len(data['modes']['modes']),
                  catalogRevision=data['catalog']['revision'], catalogItems=len(data['catalog']['items']),
                  gamecubeCatalogItems=21, visibleGamecubeItems=20, hiddenGamecubeItems=1,
                  currentOwnerProfiles=24, multiplayerGames=9, multiplayerModes=10,
                  multiplayerCountBindings=20, individualModes=12, unsupportedGbaModes=2,
                  profilesCompactBytes=len(policy.encoded(profiles)), modesCompactBytes=len(policy.encoded(extended)),
                  serverRestartRequired=False, serverApplied=False, physicalGameplayValidated=False,
                  candidateSha256={k: hashlib.sha256(policy.encoded(v)).hexdigest()
                                   for k, v in [('profiles', profiles), ('modes', extended), ('engines', data['engines'])]})
    return incoming, profiles, extended, result
