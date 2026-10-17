"""Rebind the fully reviewed decisions to exact R173 binary identities.

No approvals, counts, text, ROMs, registry limits or existing rows are changed.
"""
from copy import deepcopy
import hashlib

COUNTS = dict(psx=93, n64=387, gamecube=24, wii=3)


def need(ok, why):
    if not ok:
        raise ValueError(why)


def prepare(data, delivery, previous, policy, registrar):
    need(previous['passed'] and previous['serverApplied'] and previous['apkRevision']=='R172',
         'Applied GameCube metadata authority required')
    before = previous['counts']
    need((data['catalog']['revision'],len(data['catalog']['items']),len(data['engines']['engines']),
          len(data['profiles']),len(data['modes']['modes'])) ==
         tuple(before[k] for k in ('catalogRevision','catalogItems','engines','profiles','modes')),
         'Current enrollment authority changed')
    incoming, kit = delivery['incoming.json'], delivery['MANIFESTO-KIT.json']
    proof = delivery['proofs/final-apk-proof.json']
    need(kit['revision']=='R173' and kit['preservedCorePolicies'] and kit['gamecubeRoomRecognitionFixed'] and
         kit['psxInputDelayBefore']==kit['psxInputDelayAfter']==8 and proof['versionCode']==15 and
         proof['passed'] and proof['preservedDexPartitions']==39 and proof['preservedEntryCount']==13855 and
         kit['apkSha256']==proof['apkSha256'], 'Exact scoped R173 candidate required')
    new = {e['platform']:e for e in incoming['engines']}
    old_ids = {'psx':'pcsx-rearmed-ff81ed17-player-v3-7a14d3a4d304',
               'n64':'mupen64plus-ae-33bf7021-n64-v3-8cfc9b1a45d4',
               'gamecube':'dolphin-5102a033-gamecube-v3-ff5113148661',
               'wii':'dolphin-5102a033-wii-v3-ff5113148661'}
    old = {e['platform']:e for e in data['engines']['engines'] if e['engineId'] in old_ids.values()}
    need(set(new)==set(old)==set(COUNTS) and len(incoming['engines'])==4, 'Four exact owners required')
    for platform, engine in new.items():
        expected = dict(old[platform], engineId=engine['engineId'],runtimeSha256=engine['runtimeSha256'])
        need(expected==engine and engine['engineId']==kit['independentEngineIds'][platform] and
             engine['engineId'] not in {e['engineId'] for e in data['engines']['engines']},
             'Unexpected emulation option/core/controller change or existing owner')
    extended = deepcopy(data['modes'])
    bindings = dict.fromkeys(COUNTS,0)
    source = {(p['engineId'],p['itemId'],p['contentSha256'],p['profileId']):p
              for p in data['profiles'] if p['engineId'] in old_ids.values()}
    need(len(source)==sum(COUNTS.values()), 'Applied exact decision set incomplete')
    seen = set()
    for before_mode, after_mode in zip(data['modes']['modes'],extended['modes']):
        owners = set(before_mode.get('engineIds',[])) & set(old_ids.values())
        if not owners:
            continue
        platform = before_mode['platform']
        need(platform in COUNTS and owners=={old_ids[platform]}, 'Wrong existing mode owner')
        key = (old_ids[platform],before_mode['itemId'],before_mode['contentSha256'],before_mode['profileId'])
        need(key in source and key not in seen and all(source[key][k]==before_mode[k] for k in policy.MODE_FIELDS),
             'Existing reviewed decision differs')
        seen.add(key)
        after_mode['engineIds'].append(new[platform]['engineId'])
        bindings[platform] += 1
    need(bindings==COUNTS and seen==set(source), 'Every approved/refused/solo decision must be preserved')
    registrar.reviewed_modes(data['modes'],extended,incoming)
    manifest = deepcopy(data['engines'])
    manifest['engines'].extend(deepcopy(incoming['engines']))
    profiles = policy.prepare(data['catalog']['items'],data['profiles'],manifest,extended)
    need(profiles[:len(data['profiles'])]==data['profiles'] and len(profiles)==len(data['profiles'])+507,
         'Unrequested profile mutation')
    for p in profiles[len(data['profiles']):]:
        platform = p['platform']
        key = (old_ids[platform],p['itemId'],p['contentSha256'],p['profileId'])
        expected = dict(source[key],engineId=new[platform]['engineId'],runtimeSha256=new[platform]['runtimeSha256'])
        need(p==expected, 'Existing approval/refusal/controller/mode changed')
    audit = dict(passed=True,revision='R173',enginesAdded=4,profilesAdded=507,modesAdded=0,
        preparedModeEngineBindingsAdded=507,modeCounts=COUNTS,previousDecisionsPreserved=True,
        sourceClassRecognitionOnly=True,emulationOptionsPreserved=True,psxInputDelayFrames=8,
        exactEngineIds=kit['independentEngineIds'],profileSha256={p:v['profileSha256'] for p,v in delivery['profile-policies.json']['profiles'].items()},
        before=before,after=dict(before,engines=len(manifest['engines']),profiles=len(profiles)),
        profilesCompactBytes=len(policy.encoded(profiles)),modesCompactBytes=len(policy.encoded(extended)),
        candidateSha256={n:hashlib.sha256(policy.encoded(v)).hexdigest() for n,v in
                         [('engines',manifest),('profiles',profiles),('modes',extended)]},
        apiRestartRequired=False,serverApplied=False,physicalGameplayValidated=False)
    return manifest,profiles,extended,audit
