"""Bind the verified R161 CPU policy without modifying the historical builder."""
import copy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
N64_ENGINE = 'mupen64plus-ae-33bf7021-n64-v3-656d6d7c017f'


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [copy.deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']:e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or set(previous) != set(builder.POLICY):
        raise ValueError('Exactly four received policies required')
    for e in selected:
        old = previous[e['platform']]
        options = old['options']
        if e['platform'] == 'n64':
            if e['engineId'] != N64_ENGINE or options.count('cpu=cached-interpreter\n') != 1:
                raise ValueError('Exact R161 N64 / prior interpreter policy required')
            options = options.replace('cpu=cached-interpreter\n', 'cpu=dynarec\n', 1)
        if e['options'] != options or e['coreSha256'] != old['coreSha256']:
            raise ValueError('Only the received N64 CPU policy may change')
    original_policy = copy.deepcopy(builder.POLICY)
    original_incoming = builder.incoming
    name, maximum, _ = builder.POLICY['n64']
    builder.POLICY = dict(builder.POLICY, n64=(name, maximum, N64_PROFILE))
    sealed_runtime = dict(schemaVersion=1, engines=selected)
    try:
        incoming, policies = original_incoming(sealed_runtime)
        if incoming != expected_incoming or policies != expected_policies:
            raise ValueError('Manifest or Android policy differs from the verified kit')
    except Exception:
        builder.POLICY = original_policy
        raise

    def received_only(actual, package=None):
        if actual != sealed_runtime or package is not None:
            raise ValueError('Preparation must use exactly the verified R161 owners')
        return copy.deepcopy(incoming), copy.deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
