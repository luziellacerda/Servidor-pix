"""Regression against bytes evaluated by the installed R157 DEX on Android."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent
DOCS = HERE.parents[1]
spec = importlib.util.spec_from_file_location('fixed_profiles',
    DOCS / 'online-plataformas-20261009/library-tools/station_online_profiles.py')
operator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(operator)
from repair_n64_profile_hashes import repair, LEGACY, ANDROID


class AndroidProfileHashTests(unittest.TestCase):
    def setUp(self):
        self.engine = json.loads((HERE/'installed-n64-engine.json').read_text())
        self.engine['controllerProfiles'] = [dict(controllerProfile='direct-four-ports-v1',
            maximumPlayers=4, configuration=dict(schemaVersion=1,
                controllerProfile='direct-four-ports-v1', devices=[1,1,1,1], coreOptions=self.engine['options']))]
        self.before = json.loads((HERE/'007-before.json').read_text())['profiles']
        self.after = json.loads((HERE/'007-after.json').read_text())['profiles']
        self.manifest = dict(schemaVersion=1, engines=[self.engine])

    def test_actual_android_digest_and_only_escaping_difference(self):
        config = operator.profile_configuration(self.engine, 'direct-four-ports-v1', 4)
        self.assertEqual(operator.profile_digest(config), ANDROID)
        self.assertIn(b'SN64\\/1', operator.profile_bytes(config))
        self.assertEqual(json.loads(operator.profile_bytes(config)), config)

    def test_actual_007_solo_and_arena_roundtrip(self):
        candidate, audit = repair(self.before, self.manifest, operator)
        self.assertEqual(candidate, self.after)
        self.assertEqual(audit['changedProfiles'], 2)
        repeated, second = repair(candidate, self.manifest, operator)
        self.assertEqual(repeated, candidate)
        self.assertEqual(second['changedProfiles'], 0)

    def test_unrelated_records_and_denials_preserved(self):
        extra = copy.deepcopy(self.before[0])
        extra.update(engineId='unrelated-engine', approved=False, profileSha256='e'*64)
        self.before[1]['approved'] = False
        candidate, _ = repair(self.before+[extra], self.manifest, operator)
        self.assertFalse(candidate[1]['approved'])
        self.assertEqual(candidate[-1], extra)
        self.assertEqual(candidate[0]['maximumPlayers'], 1)
        self.assertEqual(candidate[0]['allowedPlayerCounts'], [])
        self.assertEqual(candidate[1]['allowedPlayerCounts'], [2,3,4])

    def test_unknown_fingerprint_is_not_silently_authorized(self):
        self.before[1]['profileSha256'] = '0'*64
        with self.assertRaisesRegex(ValueError, 'Unexpected fingerprint'):
            repair(self.before, self.manifest, operator)

    def test_wrong_binary_and_controller_are_rejected(self):
        for field, value in (('coreSha256','0'*64), ('runtimeSha256','0'*64),
                             ('controllerProfile','standard-2p-v1')):
            bad = copy.deepcopy(self.before)
            bad[1][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, 'exact native'):
                repair(bad, self.manifest, operator)

    def test_new_scanner_profile_uses_android_hash(self):
        row = self.before[1]
        mode = {key:copy.deepcopy(row[key]) for key in
                ('itemId','contentSha256','platform',*operator.MODE_FIELDS)}
        mode['engineIds'] = [self.engine['engineId']]
        self.engine['requireExplicitEngineModes'] = True
        item = dict(itemId=row['itemId'], platform='n64', contentSha256=row['contentSha256'],
                    artifact=dict(launchPath='007.z64'))
        profiles = operator.prepare([item], [], self.manifest, dict(schemaVersion=1, modes=[mode]))
        self.assertEqual(len(profiles), 1)
        self.assertEqual(profiles[0]['profileSha256'], ANDROID)
        self.assertNotEqual(profiles[0]['profileSha256'], LEGACY)
        self.assertEqual(operator.prepare([item], profiles, self.manifest,
                         dict(schemaVersion=1, modes=[mode])), profiles)


if __name__ == '__main__':
    unittest.main()
