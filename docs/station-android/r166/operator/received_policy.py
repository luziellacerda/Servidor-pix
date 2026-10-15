"""Accept only the four independently verified R166 kit identities."""
from copy import deepcopy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
EXPECTED = {
    'psx': ('pcsx-rearmed-ff81ed17-player-v3-3794feb8bb10', '3794feb8bb10dec2319bf9c367242fc2bdcf482b4d5eee1e7e6cfba8688699aa'),
    'n64': ('mupen64plus-ae-33bf7021-n64-v3-803a0bee5c5d', '803a0bee5c5deb7314696d2a63424cf6c3de995befece526ab774eb6fdbf830d'),
    'gamecube': ('dolphin-5102a033-gamecube-v3-bf11f4e2851c', 'bf11f4e2851c5186d0318fa11130af6079a22ea1c660d34a07e0b346ded00df8'),
    'wii': ('dolphin-5102a033-wii-v3-bf11f4e2851c', 'bf11f4e2851c5186d0318fa11130af6079a22ea1c660d34a07e0b346ded00df8'),
}


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']: e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or set(previous) != set(EXPECTED):
        raise ValueError('Exactly the four received owners and checked R165 reference required')
    for engine in selected:
        before = previous[engine['platform']]
        retained = lambda value: {k: v for k, v in value.items() if k not in {'engineId', 'runtimeSha256'}}
        if retained(engine) != retained(before) or (engine['engineId'], engine['runtimeSha256']) != EXPECTED[engine['platform']]:
            raise ValueError('Received R166 core/options/controller identity differs')
    builder.POLICY = dict(builder.POLICY, n64=('direct-four-ports-v1', 4, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    incoming, policies = builder.incoming(sealed)
    if incoming != expected_incoming or policies != expected_policies:
        raise ValueError('Canonical policies differ from independently checked Android fingerprints')

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact checked R166 runtime manifest is accepted')
        return deepcopy(incoming), deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
