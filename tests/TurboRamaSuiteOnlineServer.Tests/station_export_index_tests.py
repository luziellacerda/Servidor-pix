#!/usr/bin/env python3
"""Check the private export destination and ownership without sudo or production."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / \
    "docs/station-android/scripts/exportar-indice-efetivo.py"
spec = importlib.util.spec_from_file_location("station_export_index", SCRIPT)
exporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(exporter)


class PrivateExportTest(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix="station-export-")
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)
        self.home = self.root / "home"
        self.home.mkdir()
        self.source = self.root / "protected-index.json"
        self.source.write_text(json.dumps({"revision":1,"items":[
            {"platform":"snes","filePath":"/private/rom","coverPath":"/private/cover"}]}))
        self.owner = SimpleNamespace(pw_dir=str(self.home), pw_uid=os.getuid(), pw_gid=os.getgid())
        self.output = self.home / "index.json"

    def export(self, target=None, uid=0):
        stdout = io.StringIO()
        with patch.object(exporter.os,"geteuid",return_value=uid), \
             patch.object(exporter.pwd,"getpwnam",return_value=self.owner), \
             patch.object(exporter,"configured_index",return_value=self.source), \
             patch("sys.argv",[str(SCRIPT),"--output",str(target or self.output)]), \
             contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(io.StringIO()):
            exporter.main()
        return stdout.getvalue()

    def test_real_home_string_allows_private_export(self):
        before = self.source.read_bytes()
        result = self.export()
        self.assertEqual(self.output.read_bytes(), before)
        self.assertEqual(self.source.read_bytes(), before)
        self.assertEqual(self.output.stat().st_mode & 0o777, 0o600)
        self.assertEqual(self.output.stat().st_uid, os.getuid())
        self.assertEqual(json.loads(result)["items"], 1)
        self.assertNotIn("/private", result)
        self.assertNotIn(str(self.source), result)

    def test_existing_file_is_never_overwritten(self):
        self.output.write_text("existing")
        with self.assertRaises(SystemExit): self.export()
        self.assertEqual(self.output.read_text(), "existing")

    def test_output_outside_home_is_rejected(self):
        target = self.root / "index.json"
        with self.assertRaises(SystemExit): self.export(target)
        self.assertFalse(target.exists())

    def test_symlink_parent_is_rejected(self):
        link = self.root / "home-link"
        link.symlink_to(self.home, target_is_directory=True)
        with self.assertRaises(SystemExit): self.export(link / "index.json")
        self.assertFalse(self.output.exists())

    def test_export_requires_root(self):
        with self.assertRaises(SystemExit): self.export(uid=1000)
        self.assertFalse(self.output.exists())

    def test_broken_output_symlink_is_not_followed(self):
        other = self.root / "missing.json"
        self.output.symlink_to(other)
        with self.assertRaises(FileExistsError): self.export()
        self.assertTrue(self.output.is_symlink())
        self.assertFalse(other.exists())


if __name__ == "__main__":
    unittest.main()
