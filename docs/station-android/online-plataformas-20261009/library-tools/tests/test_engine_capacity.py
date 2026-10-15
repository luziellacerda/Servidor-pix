import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from station_online_profiles import (MAXIMUM_TRUSTED_ENGINES,
    MAXIMUM_TRUSTED_MANIFEST_BYTES, encoded, validate_manifest)


class TrustedEngineCapacityTests(unittest.TestCase):
    def manifest(self,count):
        return dict(schemaVersion=1,engines=[dict(platform='n64',engineId=f'test-owner-{i}',
            coreSha256='a'*64,runtimeSha256='b'*64,extensions=['z64'],options='',
            launchReady=True,recoveryProtocol='station-stream.v3') for i in range(count)])

    def test_new_release_fits_without_deleting_91_previous_owners(self):
        previous=self.manifest(91)
        candidate=self.manifest(95)
        validate_manifest(candidate)
        self.assertEqual(previous['engines'],candidate['engines'][:91])

    def test_explicit_count_limit_remains_bounded(self):
        self.assertEqual(MAXIMUM_TRUSTED_ENGINES,256)
        validate_manifest(self.manifest(256))
        with self.assertRaisesRegex(ValueError,'trusted engine manifest'):
            validate_manifest(self.manifest(257))

    def test_byte_limit_remains_independent(self):
        document=self.manifest(256)
        for row in document['engines']:row['options']='x'*8192
        self.assertGreater(len(encoded(document)),MAXIMUM_TRUSTED_MANIFEST_BYTES)
        with self.assertRaisesRegex(ValueError,'trusted engine manifest'):
            validate_manifest(document)

    def test_duplicate_identity_and_invalid_hash_still_rejected(self):
        document=self.manifest(67)
        document['engines'][-1]=copy.deepcopy(document['engines'][0])
        with self.assertRaisesRegex(ValueError,'duplicate'):validate_manifest(document)
        document=self.manifest(67);document['engines'][-1]['runtimeSha256']='wrong'
        with self.assertRaisesRegex(ValueError,'exact binary hashes'):validate_manifest(document)


if __name__=='__main__':unittest.main()
