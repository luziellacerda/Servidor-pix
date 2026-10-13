"""Check that renewal cannot enlarge approval or resurrect a changed edition."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parent

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

profiles_module = load('profiles', ROOT.parent / 'online-plataformas-20261009/library-tools/station_online_profiles.py')
renewal = load('r139_modes', ROOT / 'prepare_server_modes.py')

class PreservedModes(unittest.TestCase):
    def inputs(self):
        engine = copy.deepcopy(json.loads((ROOT/'incoming-engine-manifest-candidate.json').read_bytes())['engines'][0])
        old = copy.deepcopy(engine)
        old.update(engineId=engine['engineId'].replace('ae72f1371e8e', '463bdb0b409e'), runtimeSha256=renewal.BASE_RUNTIME)
        config = profiles_module.profile_configuration(old, 'standard-2p-v1', 2)
        configuration_sha = hashlib.sha256(profiles_module.encoded(config)[:-1]).hexdigest()
        profiles, items, modes = [], [], []
        for n, approved in enumerate([True, False]):
            profile = dict(itemId='synthetic-'+str(n), contentSha256=str(n+1)*64, platform='snes',
                profileId='synthetic-mode-'+str(n), maximumPlayers=2, allowedPlayerCounts=[2], approved=approved,
                controllerProfile='standard-2p-v1', mode='two-player', modeTitle='Dois jogadores', instructions=[], sources=[],
                engineId=old['engineId'], coreSha256=old['coreSha256'], runtimeSha256=old['runtimeSha256'], profileSha256=configuration_sha)
            profiles.append(profile)
            items.append(dict(itemId=profile['itemId'], platform='snes', contentSha256=profile['contentSha256'],
                              artifact=dict(launchPath='synthetic.sfc'), metadata=dict(players='5')))
            modes.append({k: copy.deepcopy(profile[k]) for k in ('itemId','contentSha256','platform',*profiles_module.MODE_FIELDS)})
            modes[-1]['engineIds']=[old['engineId']]
        return dict(items=items), profiles, dict(schemaVersion=1,engines=[old]), dict(schemaVersion=1,modes=modes), dict(schemaVersion=1,engines=[engine])

    def test_exact_approval_and_refusal_despite_player_metadata(self):
        catalog, profiles, current, modes, incoming = self.inputs()
        result, audit = renewal.build(profiles_module, catalog, profiles, current, modes, incoming)
        self.assertEqual(result['modes'][:2], modes['modes'])
        self.assertEqual([m['approved'] for m in result['modes'][2:]], [True, False])
        self.assertEqual([m['allowedPlayerCounts'] for m in result['modes'][2:]], [[2], [2]])
        self.assertEqual(audit['approvedAdded'], 1)
        self.assertEqual(audit['refusedAdded'], 1)

    def test_changed_content_does_not_reuse_approval(self):
        catalog, profiles, current, modes, incoming = self.inputs()
        catalog['items'][0]['contentSha256'] = 'a'*64
        result, audit = renewal.build(profiles_module, catalog, profiles, current, modes, incoming)
        self.assertEqual(audit['profilesAdded'], 1)
        self.assertFalse(result['modes'][-1]['approved'])

    def test_fixed_core_options_cannot_change(self):
        data = self.inputs()
        data[-1]['engines'][0]['options'] += 'unexpected = "enabled"\n'
        with self.assertRaisesRegex(ValueError, 'core/controller/options policy'):
            renewal.build(profiles_module, *data)

    def test_unknown_game_has_no_generic_snes_approval(self):
        data = self.inputs()
        unknown = copy.deepcopy(data[0]['items'][0])
        unknown['itemId'] = 'synthetic-unknown'
        data[0]['items'].append(unknown)
        result, audit = renewal.build(profiles_module, *data)
        self.assertEqual(audit['profilesAdded'], 2)
        self.assertFalse(any(m['itemId']==unknown['itemId'] for m in result['modes']))

if __name__ == '__main__':
    unittest.main()
