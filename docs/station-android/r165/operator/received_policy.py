"""Accept only the four independently verified R165 kit identities."""
from copy import deepcopy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
EXPECTED = {
    'psx': ('pcsx-rearmed-ff81ed17-player-v3-0a01a6ef6e99', '0a01a6ef6e99f0bb9b62f04b7cc17642e11f0aef83b691a8c308fc1722952e4b'),
    'n64': ('mupen64plus-ae-33bf7021-n64-v3-a74636835393', 'a746368353933cb7786897bf5a3f57635bd8456711878f3c3cf72872dbbc9772'),
    'gamecube': ('dolphin-5102a033-gamecube-v3-85dda51eaeda', '85dda51eaeda8e5b2bcc1f3ee4ed560390ad1705037c0151818a028c5738474a'),
    'wii': ('dolphin-5102a033-wii-v3-85dda51eaeda', '85dda51eaeda8e5b2bcc1f3ee4ed560390ad1705037c0151818a028c5738474a'),
}


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']: e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or set(previous) != set(EXPECTED):
        raise ValueError('Exactly the four received owners and checked R164 reference required')
    for engine in selected:
        before = previous[engine['platform']]
        retained = lambda value: {k: v for k, v in value.items() if k not in {'engineId', 'runtimeSha256'}}
        if retained(engine) != retained(before) or (engine['engineId'], engine['runtimeSha256']) != EXPECTED[engine['platform']]:
            raise ValueError('Received R165 core/options/controller identity differs')
    builder.POLICY = dict(builder.POLICY, n64=('direct-four-ports-v1', 4, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    incoming, policies = builder.incoming(sealed)
    if incoming != expected_incoming or policies != expected_policies:
        raise ValueError('Canonical policies differ from independently checked Android fingerprints')

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact checked R165 runtime manifest is accepted')
        return deepcopy(incoming), deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
