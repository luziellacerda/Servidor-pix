import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from station_online_profiles import prepare, MAXIMUM_PROFILE_CONTENT_BYTES


class ProfileVersionTests(unittest.TestCase):
    def manifest(self):
        return dict(schemaVersion=1, engines=[dict(platform='n64', engineId='synthetic-n64-owner',
            coreSha256='a'*64, runtimeSha256='b'*64, extensions=['z64'], options='',
            launchReady=True, recoveryProtocol='station-stream.v3')])

    def row(self, owner, mode):
        return dict(itemId='synthetic-n64-game', contentSha256='a'*64, platform='n64',
            engineId='synthetic-n64-owner-'+str(owner), coreSha256='b'*64, runtimeSha256='c'*64,
            profileId='synthetic-mode-'+str(mode), profileSha256='d'*64, controllerProfile='direct-four-ports-v1',
            maximumPlayers=4, allowedPlayerCounts=[2,3,4], approved=True, mode='battle-'+str(mode),
            modeTitle='Synthetic battle', instructions=[], sources=[])

    def test_all_modes_of_coexisting_versions_survive(self):
        rows=[self.row(owner, mode) for owner in range(5) for mode in range(8)]
        result=prepare([], rows, self.manifest())
        self.assertEqual(result,rows)
        self.assertEqual(len(result),40)

    def test_single_engine_mode_budget_still_enforced(self):
        with self.assertRaisesRegex(ValueError,'per-engine-binding'):
            prepare([], [self.row(0, mode) for mode in range(33)], self.manifest())

    def test_encoded_profile_budget_across_versions(self):
        rows=[self.row(owner,0) for owner in range(16)]
        for row in rows: row['instructions']=['\u03a9'*500]*8
        self.assertGreater(len(json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()),MAXIMUM_PROFILE_CONTENT_BYTES)
        with self.assertRaisesRegex(ValueError,'content projection'):
            prepare([], rows, self.manifest())

    def test_larger_immutable_revision_history_preserves_every_row(self):
        rows=[self.row(owner,mode) for owner in range(5) for mode in range(8)]
        for owner in range(50,58):
            row=self.row(owner,0)
            row['instructions']=['\u03a9'*400]*8
            rows.append(row)
        size=len(json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode())
        self.assertGreater(size,128*1024)
        self.assertLessEqual(size,MAXIMUM_PROFILE_CONTENT_BYTES)
        self.assertEqual(prepare([],rows,self.manifest()),rows)


if __name__=='__main__':
    unittest.main()
