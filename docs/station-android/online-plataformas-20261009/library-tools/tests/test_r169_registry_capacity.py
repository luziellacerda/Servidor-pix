"""Exercise the R169 registry growth against the bounded production validator."""

from copy import deepcopy
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from station_online_profiles import (MAXIMUM_PREPARED_MODE_BYTES,
    MAXIMUM_PROFILE_REGISTRY_BYTES, MODE_FIELDS, encoded, prepare, validate_modes)


FIXTURE = Path(__file__).resolve().parents[3] / "r167" / "profiles-r167.json"


class R169RegistryCapacityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = json.loads(FIXTURE.read_text(encoding="utf-8"))
        assert len(cls.rows) == 492

    def unique_rows(self, repeats):
        rows = []
        for number in range(repeats * len(self.rows)):
            row = deepcopy(self.rows[number % len(self.rows)])
            row["itemId"] = f"station_{number:032x}"
            rows.append(row)
        return rows

    def test_reviewed_modes_cross_old_limit_and_stay_bounded(self):
        modes = []
        for row in self.unique_rows(25):
            modes.append(dict(itemId=row["itemId"], contentSha256=row["contentSha256"],
                              platform=row["platform"], engineIds=[row["engineId"]],
                              **{key: row[key] for key in MODE_FIELDS}))
        document = {"schemaVersion": 1, "modes": modes}
        self.assertGreater(len(encoded(document)), 8 * 1024 * 1024)
        self.assertLessEqual(len(encoded(document)), MAXIMUM_PREPARED_MODE_BYTES)
        self.assertEqual(len(validate_modes(document)), len(modes))
        self.assertEqual(validate_modes({"schemaVersion": 1, "modes": []}), [])

    def test_profile_reader_budget_accepts_R169_sized_history(self):
        rows = self.unique_rows(32)
        size = len((json.dumps(rows, ensure_ascii=False, indent=2) + "\n").encode())
        self.assertGreater(size, 16 * 1024 * 1024)
        self.assertLessEqual(size, MAXIMUM_PROFILE_REGISTRY_BYTES)
        manifest = {"schemaVersion": 1, "engines": [{"platform": "n64",
            "engineId": "synthetic-n64-owner", "coreSha256": "a" * 64,
            "runtimeSha256": "b" * 64, "extensions": ["z64"], "options": "",
            "launchReady": True, "recoveryProtocol": "station-stream.v3"}]}
        self.assertEqual(prepare([], rows, manifest), rows)
        # R172 publishes the same compact representation as the scanner.
        # An unused pretty representation must not reject bytes that fit.
        rows = self.unique_rows(40)
        size = len((json.dumps(rows, ensure_ascii=False, indent=2) + "\n").encode())
        self.assertGreater(size, MAXIMUM_PROFILE_REGISTRY_BYTES)
        self.assertLessEqual(len(encoded(rows)), MAXIMUM_PROFILE_REGISTRY_BYTES)
        self.assertEqual(prepare([], rows, manifest), rows)
        rows = self.unique_rows(42)
        self.assertGreater(len(encoded(rows)), MAXIMUM_PROFILE_REGISTRY_BYTES)
        with self.assertRaisesRegex(ValueError, "registry exceeds"):
            prepare([], rows, manifest)


if __name__ == "__main__":
    unittest.main()
