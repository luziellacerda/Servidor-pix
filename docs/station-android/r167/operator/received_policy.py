"""Accept only the four independently verified R167 kit identities."""
from copy import deepcopy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
EXPECTED = {
    'psx': ('pcsx-rearmed-ff81ed17-player-v3-4d5c4cb8f609', '4d5c4cb8f609f5d1eb49a002d137d68172d03f59505443071d02903e25630991'),
    'n64': ('mupen64plus-ae-33bf7021-n64-v3-cf820ae1f2ca', 'cf820ae1f2ca667c6b66019b64ed42f036780c7f5fbbcdbf766e153233a906fb'),
    'gamecube': ('dolphin-5102a033-gamecube-v3-5ce990f91db1', '5ce990f91db164d970222f0230fe988f9d11ff071837e6b0b4b57fa15713fe60'),
    'wii': ('dolphin-5102a033-wii-v3-5ce990f91db1', '5ce990f91db164d970222f0230fe988f9d11ff071837e6b0b4b57fa15713fe60'),
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
            raise ValueError('Received R167 core/options/controller identity differs')
    builder.POLICY = dict(builder.POLICY, n64=('direct-four-ports-v1', 4, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    incoming, policies = builder.incoming(sealed)
    if incoming != expected_incoming or policies != expected_policies:
        raise ValueError('Canonical policies differ from independently checked Android fingerprints')

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact checked R167 runtime manifest is accepted')
        return deepcopy(incoming), deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
