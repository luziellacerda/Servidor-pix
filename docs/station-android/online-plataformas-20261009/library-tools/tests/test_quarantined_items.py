"""Run the complete scanner with an unavailable item retained for audit."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location('quarantine_scanner', SCRIPTS / 'atualizar-biblioteca-station.py')
scanner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scanner)


class QuarantinedItemTests(unittest.TestCase):
    def fixture(self, root):
        volume, home = root / 'volume', root / 'library'
        volume.mkdir(); home.mkdir()
        (volume / 'gamecube').mkdir()
        body = b'SYNTHETIC PUBLISHED IMAGE'
        digest = hashlib.sha256(body).hexdigest()
        source = home / 'published.iso'
        source.write_bytes(body)
        control = dict(itemId='synthetic-control', name='Synthetic control', platform='gamecube',
            revision=7, coverId='synthetic-cover', coverPath=str(home / 'cover.jpg'),
            filePath=str(source), catalogVisible=True, contentSha256=digest,
            metadata=dict(description='Synthetic control'),
            artifact=dict(format='raw', sha256=digest, sizeBytes=len(body), fileCount=1,
                          expandedSizeBytes=len(body), launchPath='published.iso'))
        retired = copy.deepcopy(control)
        retired.update(itemId='synthetic-quarantined', name='Synthetic quarantined', catalogVisible=False)
        retired.pop('artifact')
        index = dict(revision=49, items=[retired, control])
        def save(name, data):
            path = home / name
            path.write_text(json.dumps(data) + '\n')
            return str(path)
        entry = dict(itemId=control['itemId'], platform='gamecube', artifactSha256=digest,
            launchPath='published.iso', expandedSizeBytes=len(body), fileCount=1, contentSha256=digest)
        archived = dict(entry, itemId=retired['itemId'])
        identities = save('identities.json', dict(schemaVersion=2, entries=[entry, archived]))
        config = dict(volumeRoot=str(volume), volumeDevice=volume.stat().st_dev,
            outputDirectory=str(home), baseIndex=save('base.json', index),
            seedSourceMap=save('seed.json', dict(items=[])), metadataOverrides=save('overrides.json', {}),
            contentIdentityRegistry=identities, autoContentIdentity=True,
            cartridgeNormalization=dict(outputDirectory=str(home / 'normalized')),
            autoOnlineProfiles=dict(registry=save('profiles.json', []),
                engineManifest=save('engines.json', dict(schemaVersion=1, engines=[])),
                preparedModes=save('modes.json', dict(schemaVersion=1, modes=[])),
                maintainerAuthorizedTwoSeats=True),
            platforms=dict(gamecube=dict(extensions=['.iso'])))
        save('index.json', index)
        return config, home, index

    def test_full_scan_preserves_hidden_item_identity_and_other_rows(self):
        with tempfile.TemporaryDirectory(prefix='station-quarantine-fixture-') as name:
            config, home, index = self.fixture(Path(name))
            original = (home / 'index.json').read_bytes()
            registries = {n: (home / n).read_bytes() for n in ['identities.json', 'profiles.json', 'engines.json', 'modes.json']}
            for _ in range(2):
                result = scanner.publish(config, bootstrap=True)
                self.assertFalse(result['changed'])
                self.assertEqual(result['revision'], 49)
                self.assertEqual((home / 'index.json').read_bytes(), original)
                self.assertEqual(json.loads(original), index)
                self.assertNotIn('onlineIdentityPending', result)
                for n, body in registries.items():
                    self.assertEqual((home / n).read_bytes(), body, n)

    def test_visible_missing_artifact_is_still_rejected_without_publication(self):
        with tempfile.TemporaryDirectory(prefix='station-quarantine-invalid-') as name:
            config, home, index = self.fixture(Path(name))
            index['items'][0]['catalogVisible'] = True
            (home / 'index.json').write_text(json.dumps(index) + '\n')
            before = (home / 'index.json').read_bytes()
            with self.assertRaises(KeyError):
                scanner.publish(config, bootstrap=True)
            self.assertEqual((home / 'index.json').read_bytes(), before)


if __name__ == '__main__':
    unittest.main(verbosity=2)
