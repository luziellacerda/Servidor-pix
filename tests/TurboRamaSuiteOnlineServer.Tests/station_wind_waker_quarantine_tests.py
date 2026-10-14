"""Offline, disposable fixtures for the single-item Wind Waker quarantine."""

import importlib.util
from contextlib import ExitStack, nullcontext
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = (Path(__file__).resolve().parents[2] / "docs" / "station-android" /
          "scripts" / "quarentenar-wind-waker-station.py")
SPEC = importlib.util.spec_from_file_location("wind_waker_quarantine", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class WindWakerQuarantineTests(unittest.TestCase):
    def setUp(self):
        self.old_sha, self.old_size = MODULE.BAD_SHA256, MODULE.BAD_SIZE
        self.body = b"GZLE01" + b"synthetic disposable fixture"
        MODULE.BAD_SHA256 = hashlib.sha256(self.body).hexdigest()
        MODULE.BAD_SIZE = len(self.body)

    def tearDown(self):
        MODULE.BAD_SHA256, MODULE.BAD_SIZE = self.old_sha, self.old_size

    def document(self):
        return dict(revision=47, items=[
            dict(itemId="station_control", platform="gamecube", catalogVisible=True,
                 artifact=dict(sha256="f" * 64, sizeBytes=100, format="raw")),
            dict(itemId=MODULE.ITEM_ID, platform="gamecube", revision=44,
                 coverId="cover_bad", catalogVisible=True, filePath="/fake/immutable.iso",
                 artifact=dict(sha256=MODULE.BAD_SHA256,
                               sizeBytes=MODULE.BAD_SIZE, format="raw")),
        ])

    def test_projection_changes_only_selected_visibility_artifact_and_revision(self):
        before = self.document()
        raw = MODULE.encode(before)
        original, after, selected = MODULE.prepare_index(raw)
        self.assertEqual(original, before)
        self.assertEqual(selected["artifact"]["sha256"], MODULE.BAD_SHA256)
        self.assertEqual(after["revision"], 48)
        self.assertEqual(after["items"][0], before["items"][0])
        self.assertFalse(after["items"][1]["catalogVisible"])
        self.assertNotIn("artifact", after["items"][1])
        self.assertEqual(after["items"][1]["filePath"], "/fake/immutable.iso")
        with self.assertRaises(ValueError):
            MODULE.prepare_index(MODULE.encode(after))

    def test_wrong_hash_or_unrelated_change_rejected(self):
        bad = self.document()
        bad["items"][1]["artifact"]["sha256"] = "0" * 64
        with self.assertRaises(ValueError):
            MODULE.prepare_index(MODULE.encode(bad))

    def test_source_is_verified_and_quarantine_is_outside_platform(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            source = root / "gamecube" / "Wind Waker.iso"
            source.parent.mkdir()
            source.write_bytes(self.body)
            config = dict(volumeRoot=str(root), platforms={"gamecube": {"folder": "gamecube"}})
            quarantine, target, info = MODULE.source_guard(config, source)
            self.assertEqual(info.st_size, len(self.body))
            self.assertFalse(quarantine.is_relative_to(source.parent))
            self.assertEqual(target.parent, quarantine)
            self.assertFalse(target.exists())
            source.write_bytes(b"GZLE01" + b"x" * (len(self.body) - len(b"GZLE01")))
            with self.assertRaises(ValueError):
                MODULE.source_guard(config, source)

    def test_shared_source_cannot_be_quarantined(self):
        state = dict(sources={"/fake/source.iso": dict(ids=[MODULE.ITEM_ID, "station_other"])})
        with self.assertRaises(ValueError):
            MODULE.find_source(state)

    def test_strict_plan_prevents_stale_apply(self):
        with self.assertRaises(ValueError):
            MODULE.compare_plan({"expectedRevision": 47}, {"expectedRevision": 48})

    def transaction_fixture(self, root):
        home = root / "live"
        home.mkdir()
        index = home / "index.json"
        backup = root / "station-wind-waker-backup-20261014-fixture"
        backup.mkdir()
        volume = root / "media"
        source = volume / "gamecube" / "Wind Waker.iso"
        source.parent.mkdir(parents=True)
        target = volume / MODULE.QUARANTINE_NAME / (
            MODULE.ITEM_ID + "-" + MODULE.BAD_SHA256 + ".iso")
        target.parent.mkdir()
        target.write_bytes(self.body)
        original = MODULE.encode(self.document())
        after = MODULE.encode(MODULE.prepare_index(original)[1])
        plan = dict(itemId=MODULE.ITEM_ID, expectedPid=42, indexPath=str(index),
                    sourcePath=str(source), quarantinePath=str(target))
        manifest = dict(schema="station.wind-waker-quarantine.backup.v1",
                        beforeIndexSha256=MODULE.sha(original),
                        afterIndexSha256=MODULE.sha(after), plan=plan,
                        sourcePath=str(source), quarantinePath=str(target))
        (backup / "manifest.json").write_bytes(MODULE.encode(manifest))
        (backup / "index-before.json").write_bytes(original)
        return home, index, backup, source, target, original, after, volume

    def transaction_patches(self, stack, index):
        stack.enter_context(patch.object(MODULE, "under_lock", return_value=nullcontext()))
        stack.enter_context(patch.object(MODULE, "bounded",
                                         side_effect=lambda path, maximum: (path.read_bytes(), path.stat())))
        stack.enter_context(patch.object(MODULE, "atomic",
                                         side_effect=lambda path, raw, info=None: path.write_bytes(raw)))
        stack.enter_context(patch.object(MODULE, "create_private_file",
                                         side_effect=lambda path, raw: path.write_bytes(raw)))
        stack.enter_context(patch.object(MODULE, "sync_dir"))
        stack.enter_context(patch.object(MODULE, "restore_path_guard"))
        stack.enter_context(patch.object(MODULE, "process_id", return_value=42))
        stack.enter_context(patch.object(MODULE, "effective_index", return_value=index))
        stack.enter_context(patch.object(MODULE, "write_receipt"))

    def test_real_rollback_restores_only_source_and_monotonic_index(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            index.write_bytes(after)
            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                report = MODULE.rollback({"volumeRoot": str(volume)}, 42, index, home,
                                         backup, volume / "receipt.json")
            self.assertEqual(report["operation"], "rollback")
            self.assertTrue(source.is_file())
            self.assertEqual(source.read_bytes(), self.body)
            self.assertFalse(target.exists())
            restored = json.loads(index.read_bytes())
            self.assertEqual(restored["revision"], 49)
            old = json.loads(original)
            restored["revision"] = old["revision"]
            self.assertEqual(restored, old)

    def test_restore_rejects_manifest_path_outside_quarantine(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            volume = root / "media"
            source = volume / "gamecube" / "Wind Waker.iso"
            source.parent.mkdir(parents=True)
            wrong_target = volume / "gamecube" / "other.iso"
            with self.assertRaises(ValueError):
                MODULE.restore_path_guard(
                    {"volumeRoot": str(volume),
                     "platforms": {"gamecube": {"folder": "gamecube"}}},
                    root, source, wrong_target)

    def test_index_commit_then_error_is_reported_as_published_and_recoverable(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            home = root / "live"
            home.mkdir()
            index = home / "index.json"
            original = MODULE.encode(self.document())
            projected = MODULE.encode(MODULE.prepare_index(original)[1])
            index.write_bytes(original)
            source = root / "media" / "gamecube" / "Wind Waker.iso"
            source.parent.mkdir(parents=True)
            source.write_bytes(self.body)
            target = root / "media" / MODULE.QUARANTINE_NAME / (
                MODULE.ITEM_ID + "-" + MODULE.BAD_SHA256 + ".iso")
            info = source.stat()
            submitted = dict(sourcePath=str(source), quarantinePath=str(target),
                             expectedSourceDevice=info.st_dev, expectedSourceInode=info.st_ino,
                             expectedSourceMtimeNs=info.st_mtime_ns)

            def late_atomic(path, raw, old_info=None):
                path.write_bytes(raw)
                raise OSError("simulated fsync failure after rename")

            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                stack.enter_context(patch.object(MODULE, "current_plan", return_value=submitted))
                stack.enter_context(patch.object(MODULE, "atomic", side_effect=late_atomic))
                report = MODULE.apply({}, 42, index, home, submitted, root / "receipt.json")
            self.assertEqual(report["publicationState"], "published")
            self.assertTrue(report["recoveryNeeded"])
            self.assertTrue(target.is_file())
            self.assertFalse(source.exists())
            self.assertEqual(index.read_bytes(), projected)

    def test_applied_receipt_failure_after_commit_requires_recovery(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            home = root / "live"
            home.mkdir()
            index = home / "index.json"
            original = MODULE.encode(self.document())
            index.write_bytes(original)
            source = root / "media" / "gamecube" / "Wind Waker.iso"
            source.parent.mkdir(parents=True)
            source.write_bytes(self.body)
            target = root / "media" / MODULE.QUARANTINE_NAME / (
                MODULE.ITEM_ID + "-" + MODULE.BAD_SHA256 + ".iso")
            info = source.stat()
            submitted = dict(sourcePath=str(source), quarantinePath=str(target),
                             expectedSourceDevice=info.st_dev, expectedSourceInode=info.st_ino,
                             expectedSourceMtimeNs=info.st_mtime_ns)

            def late_receipt(path, raw):
                if path.name == "applied.json":
                    raise OSError("simulated full disk for receipt")
                path.write_bytes(raw)

            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                stack.enter_context(patch.object(MODULE, "current_plan", return_value=submitted))
                stack.enter_context(patch.object(MODULE, "create_private_file", side_effect=late_receipt))
                report = MODULE.apply({}, 42, index, home, submitted, root / "receipt.json")
            self.assertEqual(report["publicationState"], "published")
            self.assertTrue(report["recoveryNeeded"])
            self.assertEqual(report["recoveryReason"], "applied-receipt-OSError")
            self.assertTrue(target.is_file())
            self.assertFalse(source.exists())

    def test_crash_recovery_restores_source_before_index_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            index.write_bytes(original)
            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                report = MODULE.recover({"volumeRoot": str(volume)}, 42, index, home,
                                        backup, volume / "receipt.json")
            self.assertEqual(report["outcome"], "original-source-restored")
            self.assertEqual(index.read_bytes(), original)
            self.assertEqual(source.read_bytes(), self.body)
            self.assertFalse(target.exists())

    def test_rollback_index_commit_then_error_keeps_source_restored(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            index.write_bytes(after)

            def late_atomic(path, raw, info=None):
                path.write_bytes(raw)
                raise OSError("simulated fsync failure after rollback rename")

            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                stack.enter_context(patch.object(MODULE, "atomic", side_effect=late_atomic))
                report = MODULE.rollback({"volumeRoot": str(volume)}, 42, index, home,
                                         backup, volume / "receipt.json")
            self.assertEqual(report["publicationState"], "published")
            self.assertTrue(report["recoveryNeeded"])
            self.assertEqual(report["recoveryReason"], "rollback-index-write-OSError")
            self.assertTrue(source.is_file())
            self.assertFalse(target.exists())
            self.assertEqual(json.loads(index.read_bytes())["revision"], 49)

    def test_rollback_receipt_failure_after_commit_requires_recovery(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            index.write_bytes(after)

            def late_receipt(path, raw):
                if path.name == "rolled-back.json":
                    raise OSError("simulated receipt failure")
                path.write_bytes(raw)

            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                stack.enter_context(patch.object(MODULE, "create_private_file", side_effect=late_receipt))
                report = MODULE.rollback({"volumeRoot": str(volume)}, 42, index, home,
                                         backup, volume / "receipt.json")
            self.assertEqual(report["publicationState"], "published")
            self.assertTrue(report["recoveryNeeded"])
            self.assertEqual(report["recoveryReason"], "rollback-receipt-OSError")
            self.assertTrue(source.is_file())
            self.assertFalse(target.exists())

    def test_recover_completed_rollback_after_missing_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            restored = json.loads(original)
            restored["revision"] = 49
            index.write_bytes(MODULE.encode(restored))
            target.replace(source)
            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                report = MODULE.recover({"volumeRoot": str(volume)}, 42, index, home,
                                        backup, volume / "receipt.json")
            self.assertEqual(report["outcome"], "rollback-already-applied")
            self.assertEqual(source.read_bytes(), self.body)
            self.assertFalse(target.exists())

    def test_private_receipt_write_failure_cannot_expose_partial_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary).resolve()
            target = folder / "receipt.json"
            with patch.object(MODULE.os, "fsync", side_effect=OSError("simulated partial write")):
                with self.assertRaises(OSError):
                    MODULE.create_private_file(target, b"partial")
            self.assertFalse(target.exists())
            self.assertEqual(list(folder.iterdir()), [])

    def test_recover_rejects_partial_existing_applied_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            index.write_bytes(after)
            (backup / "applied.json").write_bytes(b"{")
            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                with self.assertRaises(json.JSONDecodeError):
                    MODULE.recover({"volumeRoot": str(volume)}, 42, index, home,
                                   backup, volume / "receipt.json")
            self.assertTrue(target.exists())
            self.assertFalse(source.exists())

    def test_recover_rejects_partial_existing_rollback_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            home, index, backup, source, target, original, after, volume = (
                self.transaction_fixture(Path(temporary).resolve()))
            restored = json.loads(original)
            restored["revision"] = 49
            index.write_bytes(MODULE.encode(restored))
            target.replace(source)
            (backup / "rolled-back.json").write_bytes(b"{")
            with ExitStack() as stack:
                self.transaction_patches(stack, index)
                with self.assertRaises(json.JSONDecodeError):
                    MODULE.recover({"volumeRoot": str(volume)}, 42, index, home,
                                   backup, volume / "receipt.json")
            self.assertTrue(source.exists())
            self.assertFalse(target.exists())


if __name__ == "__main__":
    unittest.main()
