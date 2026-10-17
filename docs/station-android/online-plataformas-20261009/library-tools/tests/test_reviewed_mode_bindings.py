"""Add revision owners without duplicating or changing reviewed game decisions."""
from copy import deepcopy
import importlib.util
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
spec = importlib.util.spec_from_file_location("engine_registrar", ROOT / "cadastrar-motor-online-station.py")
registrar = importlib.util.module_from_spec(spec)
spec.loader.exec_module(registrar)


class ReviewedModeBindingTests(unittest.TestCase):
    def setUp(self):
        self.incoming = dict(schemaVersion=1, engines=[dict(
            platform="n64", engineId="new-n64-owner", coreSha256="a" * 64,
            runtimeSha256="b" * 64, extensions=["z64"], options="",
            launchReady=True, recoveryProtocol="station-stream.v3",
            maximumPlayers=4, requireExplicitEngineModes=True)])
        modes = []
        for name, approved, maximum in (("versus", True, 4), ("pending", False, 4), ("solo", True, 1)):
            modes.append(dict(itemId="synthetic-n64-game", contentSha256="c" * 64,
                platform="n64", engineIds=["original-owner", "previous-owner"],
                profileId=name, maximumPlayers=maximum,
                allowedPlayerCounts=[2, 3, 4] if maximum == 4 else [],
                approved=approved, controllerProfile="direct-four-ports-v1",
                mode=name, modeTitle=name, instructions=["Preserve this decision."],
                sources=["https://example.org/review"]))
        self.existing = dict(schemaVersion=1, modes=modes)
        self.candidate = deepcopy(self.existing)
        for mode in self.candidate["modes"]:
            mode["engineIds"].append("new-n64-owner")

    def test_reviewed_approvals_refusals_and_solo_keep_all_fields(self):
        original = deepcopy(self.existing)
        self.assertIs(registrar.reviewed_modes(self.existing, self.candidate, self.incoming), self.candidate)
        self.assertEqual(self.existing, original)
        self.assertEqual(registrar.extended_bindings(self.existing, self.candidate), 3)
        for before, after in zip(original["modes"], self.candidate["modes"]):
            self.assertEqual(before["engineIds"], after["engineIds"][:-1])
            self.assertEqual({k: v for k, v in before.items() if k != "engineIds"},
                             {k: v for k, v in after.items() if k != "engineIds"})

    def test_no_change_is_supported(self):
        self.assertEqual(registrar.reviewed_modes(self.existing, deepcopy(self.existing), self.incoming), self.existing)
        self.assertEqual(registrar.extended_bindings(self.existing, self.existing), 0)

    def test_every_decision_field_is_immutable(self):
        changes = dict(approved=False, contentSha256="d" * 64,
            allowedPlayerCounts=[2, 4], controllerProfile="standard-2p-v1",
            instructions=["Changed."], sources=["https://example.org/changed"],
            modeTitle="Changed", mode="changed", profileId="changed")
        for field, value in changes.items():
            with self.subTest(field=field):
                changed = deepcopy(self.candidate)
                changed["modes"][0][field] = value
                with self.assertRaises(ValueError):
                    registrar.reviewed_modes(self.existing, changed, self.incoming)

    def test_prior_owners_cannot_be_removed_or_reordered(self):
        for owners in (["previous-owner", "new-n64-owner"],
                       ["previous-owner", "original-owner", "new-n64-owner"],
                       ["original-owner", "new-n64-owner"]):
            changed = deepcopy(self.candidate)
            changed["modes"][0]["engineIds"] = owners
            with self.assertRaises(ValueError):
                registrar.reviewed_modes(self.existing, changed, self.incoming)

    def test_unscoped_decisions_cannot_be_scoped_or_vice_versa(self):
        old = deepcopy(self.existing)
        del old["modes"][0]["engineIds"]
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(old, self.candidate, self.incoming)
        changed = deepcopy(self.candidate)
        del changed["modes"][0]["engineIds"]
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)

    def test_unknown_owner_or_platform_is_rejected(self):
        changed = deepcopy(self.candidate)
        changed["modes"][0]["engineIds"][-1] = "unreceived-owner"
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)
        incoming = deepcopy(self.incoming)
        incoming["engines"][0].update(platform="psx", maximumPlayers=2)
        with self.assertRaisesRegex(ValueError, "matching platform"):
            registrar.reviewed_modes(self.existing, self.candidate, incoming)

    def test_missing_controller_capacity_is_rejected(self):
        incoming = deepcopy(self.incoming)
        incoming["engines"][0]["controllerProfiles"] = [dict(
            controllerProfile="direct-four-ports-v1", maximumPlayers=2,
            configuration=dict(schemaVersion=1, controllerProfile="direct-four-ports-v1",
                               devices=[1, 1], coreOptions=""))]
        with self.assertRaisesRegex(ValueError, "implemented controller"):
            registrar.reviewed_modes(self.existing, self.candidate, incoming)

    def test_mode_removal_reorder_and_duplicate_owners_are_rejected(self):
        changed = deepcopy(self.candidate)
        changed["modes"].pop()
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)
        changed = deepcopy(self.candidate)
        changed["modes"].reverse()
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)
        changed = deepcopy(self.candidate)
        changed["modes"][0]["engineIds"].append("new-n64-owner")
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)

    def test_new_modes_still_need_only_received_compatible_owners(self):
        changed = deepcopy(self.candidate)
        new = deepcopy(changed["modes"][0])
        new.update(profileId="new-mode", engineIds=["new-n64-owner"])
        changed["modes"].append(new)
        registrar.reviewed_modes(self.existing, changed, self.incoming)
        new["engineIds"] = ["previous-owner"]
        with self.assertRaises(ValueError):
            registrar.reviewed_modes(self.existing, changed, self.incoming)


if __name__ == "__main__":
    unittest.main()
