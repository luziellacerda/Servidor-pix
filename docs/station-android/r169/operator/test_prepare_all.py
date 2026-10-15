from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from prepare_all import check_output_paths, digest_file, read_bounded


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
