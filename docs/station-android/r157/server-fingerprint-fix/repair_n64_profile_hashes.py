"""Repair only the proven Android slash-escaping mismatch, without approvals.

This module is a pure data migration. Its native administrative caller must
take scan.lock, back up a fresh effective registry and publish atomically.
"""
import copy
import hashlib
from collections import Counter

CORE = 'fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8'
LEGACY = '2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c'
ANDROID = '688d519ae641911804fe3286e6b5f50cc7f8c06c42ccb1be36781f0fb067b83d'


def repair(profiles, manifest, operator):
    operator.validate_manifest(manifest)
    owners = {}
    for engine in manifest['engines']:
        if (engine['platform'] != 'n64' or engine['coreSha256'] != CORE
                or not engine['engineId'].startswith('mupen64plus-ae-33bf7021-n64-v3-')):
            continue
        if not engine['engineId'].endswith(engine['runtimeSha256'][:12]):
            raise ValueError('N64 owner must bind its exact runtime')
        configuration = operator.profile_configuration(engine, 'direct-four-ports-v1', 4)
        if (configuration is None or operator.profile_digest(configuration) != ANDROID
                or hashlib.sha256(operator.encoded(configuration)[:-1]).hexdigest() != LEGACY):
            raise ValueError('Only the proven unchanged N64 controller configuration may be repaired')
        owners[engine['engineId']] = engine
    result = copy.deepcopy(profiles)
    changed, already = Counter(), Counter()
    for previous, candidate in zip(profiles, result):
        engine = owners.get(previous['engineId'])
        if engine is None:
            continue
        if (any(previous[k] != engine[k] for k in ('platform', 'coreSha256', 'runtimeSha256'))
                or previous['controllerProfile'] != 'direct-four-ports-v1'):
            raise ValueError('A profile differs from its exact native controller/binary binding')
        value = previous['profileSha256']
        if value == ANDROID:
            already[engine['engineId']] += 1
        elif value == LEGACY:
            candidate['profileSha256'] = ANDROID
            changed[engine['engineId']] += 1
        else:
            raise ValueError('Unexpected fingerprint; new explicit review required')
        if {k:v for k,v in candidate.items() if k != 'profileSha256'} != {
                k:v for k,v in previous.items() if k != 'profileSha256'}:
            raise ValueError('Only the controller fingerprint may change')
    return result, dict(changedProfiles=sum(changed.values()), changedByEngine=dict(changed),
        alreadyCorrectProfiles=sum(already.values()), oldProfileSha256=LEGACY,
        androidProfileSha256=ANDROID, approvalsCountsModesAndBinaryBindingsPreserved=True,
        profileCountPreserved=len(result) == len(profiles))
