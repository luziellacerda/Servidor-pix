"""Regression checks for exact PSX approval and non-destructive preparation."""
from copy import deepcopy
from pathlib import Path
import importlib.util
import json
import sys
import unittest

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import prepare_psx_online as policy


class ReviewPolicyChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        module_path = HERE.parent / "online-plataformas-20261009/library-tools/station_online_profiles.py"
        spec = importlib.util.spec_from_file_location("psx_test_profile_operator", module_path)
        cls.operator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.operator)
        cls.ledger = json.loads((HERE / "reviewed-library.json").read_bytes())
        cls.tests = json.loads((HERE / "evidence/core-qualification.json").read_bytes())["editions"]
        cls.by_id = {r["itemId"]: r for r in cls.tests}
        cls.good = next(e for e in cls.ledger["entries"] if e["onlineApproved"])
        cls.mode = next(m for m in cls.good["modes"] if m["approved"])

    def test_actual_accepted_edition_passes(self):
        policy.approval_evidence(self.good, self.mode, self.by_id[self.good["itemId"]])

    def test_simulated_core_cannot_approve(self):
        evidence = dict(self.by_id[self.good["itemId"]], actualReceivedArm64CoreExecuted=False)
        with self.assertRaises(ValueError):
            policy.approval_evidence(self.good, self.mode, evidence)

    def test_changed_rom_and_changed_core_refused(self):
        for key in ("contentSha256", "coreSha256", "itemId"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                evidence = dict(self.by_id[self.good["itemId"]], **{key: "0" * 64})
                policy.approval_evidence(self.good, self.mode, evidence)

    def test_failed_replay_cannot_be_relabelled(self):
        for key in ("freshStartLockstepPassed", "recoveryReplayRamEqual", "recoveryReplayVideoEqual"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                policy.approval_evidence(self.good, self.mode, dict(self.by_id[self.good["itemId"]], **{key: False}))

    def test_blank_or_unreviewed_image_refused(self):
        for key in ("bootFrameVisuallyReviewed", "bootFrameMeaningful"):
            with self.subTest(key=key), self.assertRaises(ValueError):
                policy.approval_evidence(dict(self.good, **{key: False}), self.mode, self.by_id[self.good["itemId"]])

    def test_standard_ports_and_explicit_hle_required(self):
        for entry in (dict(self.good, biosPolicy="auto"), dict(self.good, standardTwoPortsDocumented=False)):
            with self.assertRaises(ValueError):
                policy.approval_evidence(entry, self.mode, self.by_id[self.good["itemId"]])

    def test_documentary_finding_and_https_required(self):
        for mode in (dict(self.mode, documentaryFinding=""), dict(self.mode, sources=["http://example.com/manual"])):
            with self.assertRaises(ValueError):
                policy.approval_evidence(self.good, mode, self.by_id[self.good["itemId"]])

    def test_local_classifications_grant_no_seats(self):
        local = [m for e in self.ledger["entries"] for m in e["modes"] if m["maximumPlayers"] == 1]
        self.assertTrue(local)
        self.assertTrue(all(m["classificationOnly"] and m["allowedPlayerCounts"] == [] for m in local))

    def test_every_multiplayer_approval_has_current_evidence(self):
        for entry in self.ledger["entries"]:
            for mode in entry["modes"]:
                if mode["approved"] and mode["maximumPlayers"] == 2:
                    policy.approval_evidence(entry, mode, self.by_id[entry["itemId"]])

    def test_rejected_editions_remain_explicit(self):
        for entry in self.ledger["entries"]:
            for mode in entry["modes"]:
                if not mode["approved"]:
                    self.assertTrue(mode["blockingFindings"])


if __name__ == "__main__":
    unittest.main()
