import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

SCRIPTS = Path(__file__).resolve().parents[2] / 'docs/station-android/scripts'
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location('identity_discovery', SCRIPTS / 'atualizar-biblioteca-station.py')
discovery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(discovery)


class ContentIdentityTests(unittest.TestCase):
    def row(self):
        return dict(itemId='synthetic', platform='snes', name='Synthetic', revision=8,
                    coverId='synthetic-cover', metadata=dict(description='Keep synopsis'),
                    filePath='/synthetic/game.zip', coverPath='/synthetic/cover.jpg',
                    artifact=dict(sha256='a' * 64, launchPath='Synthetic.sfc', expandedSizeBytes=1024, fileCount=1))

    def entry(self, row):
        return dict(itemId=row['itemId'], platform=row['platform'], artifactSha256=row['artifact']['sha256'],
                    **{key: row['artifact'][key] for key in ('launchPath', 'expandedSizeBytes', 'fileCount')}, contentSha256='b' * 64)

    def test_only_exact_binding_publishes_distinct_payload_hash(self):
        row = self.row(); before = copy.deepcopy(row); entry = self.entry(row)
        discovery.bind_content_identities([row], {row['itemId']: entry})
        self.assertEqual(row.pop('contentSha256'), 'b' * 64)
        self.assertEqual(row, before)

    def test_replaced_or_unknown_binding_withdraws_old_identity(self):
        for key in ('platform', 'artifactSha256', 'launchPath', 'expandedSizeBytes', 'fileCount'):
            row = self.row(); entry = self.entry(row); entry[key] = 123 if isinstance(entry[key], int) else 'changed'
            row['contentSha256'] = 'b' * 64
            discovery.bind_content_identities([row], {row['itemId']: entry})
            self.assertNotIn('contentSha256', row)
        row = self.row(); row['contentSha256'] = 'b' * 64
        discovery.bind_content_identities([row], {})
        self.assertNotIn('contentSha256', row)

    def test_registry_rejects_duplicate_and_invalid_identities(self):
        with tempfile.TemporaryDirectory() as directory:
            registry = Path(directory) / 'identities.json'; good = self.entry(self.row())
            for field, value in [('contentSha256', None), ('contentSha256', 'B' * 64), ('artifactSha256', 'zip'), ('fileCount', True), ('expandedSizeBytes', 0)]:
                bad = dict(good, **{field: value}); registry.write_text(json.dumps(dict(schemaVersion=1, entries=[bad])))
                with self.assertRaises(ValueError): discovery.content_identities(registry)
            registry.write_text(json.dumps(dict(schemaVersion=1, entries=[good, good])))
            with self.assertRaises(ValueError): discovery.content_identities(registry)
            registry.write_text('{"schemaVersion":1,"schemaVersion":1,"entries":[]}')
            with self.assertRaises(ValueError): discovery.content_identities(registry)
            registry.write_text(json.dumps(dict(schemaVersion=True, entries=[])))
            with self.assertRaises(ValueError): discovery.content_identities(registry)
            registry.write_text(json.dumps(dict(schemaVersion=1, entries=[good])))
            self.assertEqual(discovery.content_identities(registry), {'synthetic': good})

    def test_scan_preserves_hash_then_withdraws_changed_content_and_keeps_ids(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); volume = root / 'media'; platform = volume / 'snes'; platform.mkdir(parents=True)
            source = platform / 'Synthetic.zip'
            def game(data):
                with zipfile.ZipFile(source, 'w') as archive: archive.writestr('Synthetic.sfc', data)
                os.utime(source, ns=(20000000000, 20000000000))
            game(b'synthetic-one' * 128)
            base = root / 'base.json'; base.write_text('{"revision":8,"items":[]}')
            seed = root / 'seed.json'; seed.write_text('{"items":[]}')
            overrides = root / 'overrides.json'; overrides.write_text('{}')
            registry = root / 'identities.json'
            config = dict(volumeRoot=str(volume), volumeDevice=volume.stat().st_dev, outputDirectory=str(root / 'published'),
                          baseIndex=str(base), seedSourceMap=str(seed), metadataOverrides=str(overrides), platforms={'snes': {'extensions': ['.sfc']}})
            discovery.publish(config); discovery.publish(config)
            index = root / 'published/index.json'; before = json.loads(index.read_text()); row = before['items'][0]
            entry = self.entry(row); entry['contentSha256'] = hashlib.sha256(b'synthetic-one' * 128).hexdigest()
            registry.write_text(json.dumps(dict(schemaVersion=1, entries=[entry]))); config['contentIdentityRegistry'] = str(registry)
            report = discovery.publish(config); published = json.loads(index.read_text())
            self.assertEqual(report['revision'], before['revision'] + 1)
            new_row = published['items'][0]; self.assertEqual(new_row.pop('contentSha256'), entry['contentSha256']); self.assertEqual(new_row, row)
            self.assertFalse(discovery.publish(config)['changed'])
            old_bytes = index.read_bytes(); registry.write_text('{"invalid":true}')
            with self.assertRaises(ValueError): discovery.publish(config)
            self.assertEqual(index.read_bytes(), old_bytes)
            registry.write_text(json.dumps(dict(schemaVersion=1, entries=[entry])))
            game(b'synthetic-two' * 256); discovery.publish(config); discovery.publish(config)
            replaced = json.loads(index.read_text())['items'][0]
            self.assertEqual(replaced['itemId'], row['itemId']); self.assertNotIn('contentSha256', replaced)
            self.assertNotEqual(replaced['artifact']['sha256'], row['artifact']['sha256'])


if __name__ == '__main__':
    unittest.main()
