"""Use the exact received R162 owners and unchanged R161 profile policy."""
import copy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
N64_ENGINE = 'mupen64plus-ae-33bf7021-n64-v3-57155617f470'
BRIDGE = 'libstation_ae_bridge.so'
BRIDGE_SHA = '3d0a9e9dc5ca31629f478023b849fe2037b4a68b9ac3689ba413a5f542191986'


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [copy.deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']:e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or set(previous) != set(builder.POLICY):
        raise ValueError('Four received owners and the verified R161 reference required')
    for engine in selected:
        old = previous[engine['platform']]
        if engine['options'] != old['options'] or engine['coreSha256'] != old['coreSha256']:
            raise ValueError('R162 must preserve every R161 option and core')
        dependencies = dict(old['dependencySha256'])
        if engine['platform'] == 'n64':
            if engine['engineId'] != N64_ENGINE:
                raise ValueError('Exact received R162 N64 owner required')
            dependencies[BRIDGE] = BRIDGE_SHA
        if engine['dependencySha256'] != dependencies:
            raise ValueError('Only the exact N64 VideoExt bridge may be added')
    original_policy = copy.deepcopy(builder.POLICY)
    name, maximum, _ = builder.POLICY['n64']
    builder.POLICY = dict(builder.POLICY, n64=(name, maximum, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    try:
        incoming, policies = builder.incoming(sealed)
        if incoming != expected_incoming or policies != expected_policies:
            raise ValueError('Received R162 policies differ from their Android canonical hashes')
    except Exception:
        builder.POLICY = original_policy
        raise

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact verified R162 runtime is accepted')
        return copy.deepcopy(incoming), copy.deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
