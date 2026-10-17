"""Accept the exact received R164 owners with unchanged R162 policies."""
import copy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
N64_ENGINE = 'mupen64plus-ae-33bf7021-n64-v3-bba0ea0d7ff7'


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [copy.deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']:e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or {e['platform'] for e in selected} != set(previous) or len(previous) != 4:
        raise ValueError('Exactly four received owners and the verified R162 reference required')
    for engine in selected:
        before = previous[engine['platform']]
        retained = lambda e:{k:v for k,v in e.items() if k not in {'engineId','runtimeSha256'}}
        if retained(engine) != retained(before) or engine['engineId'] == before['engineId']:
            raise ValueError('Only the exact runtime identity may differ from R162')
        if engine['platform'] == 'n64' and engine['engineId'] != N64_ENGINE:
            raise ValueError('Exact received R164 N64 owner required')
    old_policy = copy.deepcopy(builder.POLICY)
    name, maximum, _ = builder.POLICY['n64']
    builder.POLICY = dict(builder.POLICY, n64=(name, maximum, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    try:
        incoming, policies = builder.incoming(sealed)
        if incoming != expected_incoming or policies != expected_policies:
            raise ValueError('R164 policies differ from the independently checked Android hashes')
    except Exception:
        builder.POLICY = old_policy
        raise

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact verified R164 runtime is accepted')
        return copy.deepcopy(incoming), copy.deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
