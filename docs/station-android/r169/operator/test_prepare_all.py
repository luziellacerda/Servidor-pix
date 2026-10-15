from pathlib import Path
from copy import deepcopy
import tempfile
import unittest
from unittest.mock import patch

from prepare_all import (check_output_paths, digest_file, read_bounded,
                         load_profiles_tool, verify_owner_projection)


class OwnerProjectionTests(unittest.TestCase):
    def setUp(self):
        self.tool = load_profiles_tool()
        self.runtime = dict(platform="psx", engineId="test-psx-owner",
                            coreSha256="a" * 64, runtimeSha256="b" * 64,
                            options="wire=PXL1\n", controllerProfiles=["standard-2p-v1"])
        config = dict(schemaVersion=1, controllerProfile="standard-2p-v1",
                      devices=[1, 1], coreOptions=self.runtime["options"])
        self.policy = dict(controllerProfile="standard-2p-v1", maximumPlayers=2,
                           profileSha256=self.tool.profile_digest(config))
        self.server = deepcopy(self.runtime)
        self.server.update(requireExplicitEngineModes=True, controllerProfiles=[dict(
            controllerProfile="standard-2p-v1", maximumPlayers=2, configuration=config)])

    def test_apk_declaration_and_explicit_server_projection_agree(self):
        self.assertNotEqual(self.runtime, self.server)
        verify_owner_projection(self.server, self.runtime, self.policy, self.tool)

    def test_identity_options_or_controls_cannot_change(self):
        for key, value in [("engineId", "other-owner"), ("coreSha256", "c" * 64),
                           ("runtimeSha256", "c" * 64), ("options", "changed"),
                           ("controllerProfiles", ["other-controls"])]:
            with self.subTest(key=key):
                changed = dict(self.runtime, **{key: value})
                with self.assertRaises(ValueError):
                    verify_owner_projection(self.server, changed, self.policy, self.tool)

    def test_generic_mode_fallback_and_modified_device_policy_are_rejected(self):
        for changed in [dict(self.server, requireExplicitEngineModes=False),
                        dict(self.server, controllerProfiles=[])]:
            with self.assertRaises(ValueError):
                verify_owner_projection(changed, self.runtime, self.policy, self.tool)
        changed = deepcopy(self.server)
        changed["controllerProfiles"][0]["configuration"]["devices"][1] = 0
        with self.assertRaises(ValueError):
            verify_owner_projection(changed, self.runtime, self.policy, self.tool)


class PreparationDestinationTests(unittest.TestCase):
    def test_authority_reads_are_bounded_before_parsing_or_hashing(self):
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "authority.json"
            source.write_bytes(b"{}")
            self.assertEqual(read_bounded(source, 2), b"{}")
            self.assertEqual(len(digest_file(source, 2)), 64)
            with source.open("r+b") as stream:
                stream.truncate(3)
            with self.assertRaisesRegex(ValueError, "Bounded regular"):
                read_bounded(source, 2)
            with self.assertRaisesRegex(ValueError, "Bounded regular"):
                digest_file(source, 2)

    def test_empty_dedicated_directory_is_required(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.json"
            source.write_text("{}", encoding="utf-8")
            output = root / "candidate"
            output.mkdir()
            paths = [output / name for name in
                     ("manifest.json", "profiles.json", "modes.json", "audit.json")]
            check_output_paths([source], paths)
            (output / "unrelated.txt").write_text("x", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "empty preparation directory"):
                check_output_paths([source], paths)

    def test_dangling_symlink_output_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "candidate"
            output.mkdir()
            target = output / "manifest.json"
            try:
                target.symlink_to(root / "missing.json")
            except OSError:
                original = Path.is_symlink
                with patch.object(Path, "is_symlink",
                                  autospec=True,
                                  side_effect=lambda path: path == target or original(path)):
                    with self.assertRaises(ValueError):
                        check_output_paths([], [target, output / "profiles.json",
                                                output / "modes.json", output / "audit.json"])
                return
            paths = [target, output / "profiles.json", output / "modes.json",
                     output / "audit.json"]
            with self.assertRaises(ValueError):
                check_output_paths([], paths)
            self.assertTrue(target.is_symlink())


if __name__ == "__main__":
    unittest.main()
