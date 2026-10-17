"""Accept only the four independently verified R168 kit identities."""
from copy import deepcopy

N64_PROFILE = 'ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63'
EXPECTED = {
    'psx': ('pcsx-rearmed-ff81ed17-player-v3-b21623fee188', 'b21623fee188dabf304c36aff588f56e8f1999e1ee7ad5ecaa90865df5f27cd4'),
    'n64': ('mupen64plus-ae-33bf7021-n64-v3-a0c9d15cfbc7', 'a0c9d15cfbc7f84ebe7088de68f99d53419bdf840c4a24429723a4f64460d415'),
    'gamecube': ('dolphin-5102a033-gamecube-v3-04383e1f63a0', '04383e1f63a036b590aa22398bc0d2280a3bb04bcf472858f11f9ce4bd3577a9'),
    'wii': ('dolphin-5102a033-wii-v3-04383e1f63a0', '04383e1f63a036b590aa22398bc0d2280a3bb04bcf472858f11f9ce4bd3577a9'),
}


def configure(builder, merger, runtime, expected_incoming, expected_policies, prior):
    selected = [deepcopy(e) for e in runtime['engines'] if e['platform'] in builder.POLICY]
    previous = {e['platform']: e for e in prior['engines'] if e['platform'] in builder.POLICY}
    if len(selected) != 4 or set(previous) != set(EXPECTED):
        raise ValueError('Exactly the four received owners and checked R167 reference required')
    for engine in selected:
        before = previous[engine['platform']]
        retained = lambda value: {k: v for k, v in value.items() if k not in {'engineId', 'runtimeSha256'}}
        if retained(engine) != retained(before) or (engine['engineId'], engine['runtimeSha256']) != EXPECTED[engine['platform']]:
            raise ValueError('Received R168 core/options/controller identity differs')
    builder.POLICY = dict(builder.POLICY, n64=('direct-four-ports-v1', 4, N64_PROFILE))
    sealed = dict(schemaVersion=1, engines=selected)
    incoming, policies = builder.incoming(sealed)
    if incoming != expected_incoming or policies != expected_policies:
        raise ValueError('Canonical policies differ from independently checked Android fingerprints')

    def received_only(actual, package=None):
        if actual != sealed or package is not None:
            raise ValueError('Only the exact checked R168 runtime manifest is accepted')
        return deepcopy(incoming), deepcopy(policies)

    builder.incoming = received_only
    merger.incoming = received_only
