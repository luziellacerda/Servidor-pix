"""Exercise real inode, permissions and open-writer boundaries of raw imports."""
import hashlib
import os
from pathlib import Path
import stat
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from station_packages import PackagePending, prepare_raw


@unittest.skipUnless(os.geteuid() == 0, 'root importer permission tests')
class ReadonlyRawTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory(prefix='station-raw-test-')
        self.home = Path(self.work.name)
        self.source = self.home / 'Test game.iso'
        self.target = self.home / 'artifact.iso'
        self.body = b'GAME PAYLOAD\x00' * 512
        self.source.write_bytes(self.body)
        os.chown(self.source, 1000, 1000)
        self.source.chmod(0o644)
        self.before = self.source.stat()

    def tearDown(self):
        self.work.cleanup()

    def test_one_inode_readonly_and_real_identity(self):
        path, descriptor = prepare_raw(self.source, self.target, readonly_hardlink=True)
        current = self.source.stat()
        self.assertEqual(current.st_ino, path.stat().st_ino)
        self.assertEqual(current.st_uid, 0)
        self.assertEqual(stat.S_IMODE(current.st_mode), 0o444)
        self.assertEqual(current.st_mtime_ns, self.before.st_mtime_ns)
        self.assertEqual(descriptor['sha256'], hashlib.sha256(self.body).hexdigest())
        self.assertEqual(descriptor['sizeBytes'], len(self.body))
        self.assertEqual(descriptor['launchPath'], self.source.name)
        self.assertEqual(path.read_bytes(), self.body)

    def test_replacing_source_keeps_existing_artifact(self):
        prepare_raw(self.source, self.target, readonly_hardlink=True)
        replacement = self.home / 'replacement.iso'
        replacement.write_bytes(b'NEW GAME')
        os.replace(replacement, self.source)
        self.assertNotEqual(self.source.stat().st_ino, self.target.stat().st_ino)
        self.assertEqual(self.target.read_bytes(), self.body)

    def test_open_writer_refused_and_source_permissions_restored(self):
        with self.source.open('ab'):
            with self.assertRaisesRegex(PackagePending, 'source_writer_active'):
                prepare_raw(self.source, self.target, readonly_hardlink=True)
        self.assertFalse(self.target.exists())
        self.assertEqual(self.source.stat().st_uid, 1000)
        self.assertEqual(stat.S_IMODE(self.source.stat().st_mode), 0o644)
        self.assertEqual(self.source.read_bytes(), self.body)

    def test_writable_alias_refused(self):
        os.link(self.source, self.home / 'writable.iso')
        with self.assertRaisesRegex(PackagePending, 'writable_alias'):
            prepare_raw(self.source, self.target, readonly_hardlink=True)
        self.assertFalse(self.target.exists())
        self.assertEqual(self.source.stat().st_uid, 1000)

    def test_symbolic_link_refused(self):
        alias = self.home / 'alias.iso'
        alias.symlink_to(self.source)
        with self.assertRaisesRegex(PackagePending, 'unsafe_path'):
            prepare_raw(alias, self.target, readonly_hardlink=True)
        self.assertFalse(self.target.exists())
        self.assertEqual(self.source.stat().st_uid, 1000)

    def test_cross_volume_refused_without_changing_source(self):
        if Path('/dev/shm').stat().st_dev == self.before.st_dev:
            self.skipTest('second filesystem unavailable')
        with tempfile.TemporaryDirectory(dir='/dev/shm', prefix='station-raw-test-') as other:
            with self.assertRaisesRegex(PackagePending, 'same_volume'):
                prepare_raw(self.source, Path(other) / 'artifact.iso', readonly_hardlink=True)
        self.assertEqual(self.source.stat().st_uid, 1000)
        self.assertEqual(stat.S_IMODE(self.source.stat().st_mode), 0o644)

    def test_default_copy_retains_source_and_separate_inode(self):
        path, descriptor = prepare_raw(self.source, self.target)
        self.assertNotEqual(self.source.stat().st_ino, path.stat().st_ino)
        self.assertEqual(self.source.stat().st_uid, 1000)
        self.assertEqual(stat.S_IMODE(self.source.stat().st_mode), 0o644)
        self.assertEqual(descriptor['sha256'], hashlib.sha256(self.body).hexdigest())


if __name__ == '__main__':
    unittest.main(verbosity=2)
