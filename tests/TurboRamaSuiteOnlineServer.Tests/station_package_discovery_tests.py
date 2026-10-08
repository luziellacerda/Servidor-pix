import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
import hashlib
from PIL import Image

SCRIPTS = Path(__file__).resolve().parents[2] / 'docs/station-android/scripts'
sys.path.insert(0, str(SCRIPTS))
from station_packages import PackagePending, package_layout

spec = importlib.util.spec_from_file_location('discovery', SCRIPTS / 'atualizar-biblioteca-station.py')
discovery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(discovery)


class PackageTests(unittest.TestCase):
    def config(self, root, platform, mode, extensions):
        volume = root / 'volume'
        (volume / platform).mkdir(parents=True)
        (root / 'base.json').write_text('{"revision":20,"items":[]}')
        (root / 'seed.json').write_text('{"items":[]}')
        (root / 'overrides.json').write_text('{}')
        return dict(volumeRoot=str(volume), volumeDevice=volume.stat().st_dev,
            outputDirectory=str(root / 'catalog'), baseIndex=str(root / 'base.json'),
            seedSourceMap=str(root / 'seed.json'), metadataOverrides=str(root / 'overrides.json'),
            platforms={platform: dict(extensions=extensions, artifactMode=mode,
                artifactDirectory=str(volume / '.compiled/games'), copyRawOnce=True)})

    def test_cue_rebases_windows_path_and_download_contains_every_track_once(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self.config(root, 'psx', 'cue-disc', ['.cue', '.bin', '.iso', '.pbp'])
            folder = root / 'volume/psx'
            cue = folder / 'Game.cue'
            cue.write_text('FILE "C:\\old\\Track1.bin" BINARY\n TRACK 01 MODE2/2352\nFILE "Track2.bin" BINARY\n TRACK 02 AUDIO\n')
            (folder / 'Track1.bin').write_bytes(b'first track')
            (folder / 'Track2.bin').write_bytes(b'second track')
            (folder / 'Other.pbp').write_bytes(b'\0PBP' + b'game')
            for file in folder.iterdir():
                os.utime(file, ns=(1, 1))
            before = cue.read_bytes()
            report = discovery.publish(config, bootstrap=True)
            self.assertEqual(report['added'], 2)
            items = json.loads((root / 'catalog/index.json').read_text())['items']
            game = next(r for r in items if r['name'] == 'Game')
            self.assertEqual(game['artifact']['launchPath'], 'Game.cue')
            self.assertEqual(game['artifact']['fileCount'], 3)
            with zipfile.ZipFile(game['filePath']) as archive:
                self.assertEqual(archive.namelist(), ['Game.cue', 'Track1.bin', 'Track2.bin'])
                self.assertIn(b'FILE "Track1.bin"', archive.read('Game.cue'))
                self.assertEqual(archive.read('Track2.bin'), b'second track')
            self.assertEqual(cue.read_bytes(), before)
            self.assertFalse(discovery.publish(config)['changed'])
            old_id = game['itemId']
            (folder / 'Track2.bin').write_bytes(b'changed track')
            self.assertFalse(discovery.publish(config)['changed'])
            os.utime(folder / 'Track2.bin', ns=(1, 1))
            discovery.publish(config)
            self.assertEqual(discovery.publish(config)['updated'], 1)
            updated = next(r for r in json.loads((root / 'catalog/index.json').read_text())['items'] if r['name'] == 'Game')
            self.assertEqual(updated['itemId'], old_id)
            self.assertEqual(updated['revision'], game['revision'] + 1)

    def test_wiiu_folder_preserves_structure_and_auto_updates_on_content_change(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self.config(root, 'wiiu', 'wiiu-folder', ['.rpx'])
            folder = root / 'volume/wiiu/Game'
            for directory in ('code', 'content', 'meta'):
                (folder / directory).mkdir(parents=True)
            (folder / 'code/Game.rpx').write_bytes(b'RPX')
            (folder / 'content/data.bin').write_bytes(b'game data')
            (folder / 'meta/meta.xml').write_text('<menu/>')
            self.assertEqual(discovery.publish(config, bootstrap=True)['added'], 1)
            item = json.loads((root / 'catalog/index.json').read_text())['items'][0]
            self.assertEqual(item['folderPath'], [])
            self.assertEqual(item['artifact']['launchPath'], 'code/Game.rpx')
            with zipfile.ZipFile(item['filePath']) as archive:
                self.assertEqual(set(archive.namelist()), {'code/Game.rpx', 'content/data.bin', 'meta/meta.xml'})
            self.assertFalse(discovery.publish(config)['changed'])

    def test_missing_tracks_traversal_symlinks_and_incomplete_wiiu_are_pending(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            cue = root / 'Game.cue'
            for name in ('missing.bin', '../outside.bin'):
                cue.write_text('FILE "' + name + '" BINARY\n')
                with self.assertRaises(PackagePending):
                    package_layout(cue, 'cue-disc')
            outside = root / 'outside.bin'
            outside.write_bytes(b'outside')
            (root / 'link.bin').symlink_to(outside)
            cue.write_text('FILE "link.bin" BINARY\n')
            with self.assertRaises(PackagePending):
                package_layout(cue, 'cue-disc')
            code = root / 'Game/code'
            code.mkdir(parents=True)
            rpx = code / 'Game.rpx'
            rpx.write_bytes(b'RPX')
            with self.assertRaises(PackagePending):
                package_layout(rpx, 'wiiu-folder')

    def test_new_game_uses_same_platform_catalog_cover_and_keeps_manual_description(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self.config(root, 'gamecube', 'single-rom', ['.iso'])
            folder = root / 'volume/gamecube'
            (folder / 'media/catalogo').mkdir(parents=True)
            cover = folder / 'media/catalogo/published.png'
            Image.new('RGB', (480, 720), 'red').save(cover)
            (folder / 'station-catalog-seed.json').write_text(json.dumps({'schemaVersion': 1, 'games': {
                'examplegame': {'cover': 'media/catalogo/published.png',
                    'coverSha256': hashlib.sha256(cover.read_bytes()).hexdigest(),
                    'metadata': {'description': 'Catalog description'}}}}))
            config['platforms']['gamecube']['catalogSeed'] = 'station-catalog-seed.json'
            (folder / 'Example - Game.iso').write_bytes(b'synthetic game')
            report = discovery.publish(config, bootstrap=True)
            self.assertEqual(report['added'], 1)
            self.assertEqual(report['placeholderCovers'], 0)
            row = json.loads((root / 'catalog/index.json').read_text())['items'][0]
            self.assertEqual(row['metadata']['description'], 'Catalog description')
            self.assertTrue(Path(row['filePath']).is_relative_to(root / 'volume/.compiled'))
            (folder / 'gamelist.xml').write_text('<gameList><game><path>./Example - Game.iso</path><desc>Manual description</desc></game></gameList>')
            discovery.publish(config)
            self.assertEqual(json.loads((root / 'catalog/index.json').read_text())['items'][0]['metadata']['description'], 'Manual description')

    def test_zip_disc_keeps_tracks_and_incomplete_cue_does_not_publish_tracks(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self.config(root, 'psx', 'cue-disc', ['.cue', '.bin'])
            folder = root / 'volume/psx'
            with zipfile.ZipFile(folder / 'Game.zip', 'w') as archive:
                archive.writestr('Game.cue', 'FILE "Track1.bin" BINARY\n')
                archive.writestr('Track1.bin', b'track')
            (folder / 'Copying.cue').write_text('FILE "Incomplete1.bin" BINARY\nFILE "Incomplete2.bin" BINARY\n')
            (folder / 'Incomplete1.bin').write_bytes(b'incomplete track')
            report = discovery.publish(config, bootstrap=True)
            self.assertEqual(report['added'], 1)
            self.assertEqual(len(report['pending']), 1)
            item = json.loads((root / 'catalog/index.json').read_text())['items'][0]
            self.assertEqual(item['artifact']['fileCount'], 2)
            self.assertEqual(item['artifact']['launchPath'], 'Game.cue')

    def test_renamed_single_cue_payload_is_rebased_in_package_without_changing_original(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self.config(root, 'psx', 'cue-disc', ['.cue', '.bin'])
            folder = root / 'volume/psx'
            cue = folder / 'Renamed Game.cue'
            cue.write_text('FILE "old-name.bin" BINARY\n TRACK 01 MODE2/2352\n')
            original = cue.read_bytes()
            (folder / 'Renamed Game.bin').write_bytes(b'game track')
            report = discovery.publish(config, bootstrap=True)
            self.assertEqual(report['added'], 1)
            self.assertFalse(report['pending'])
            item = json.loads((root / 'catalog/index.json').read_text())['items'][0]
            self.assertEqual(item['artifact']['launchPath'], 'Renamed Game.cue')
            with zipfile.ZipFile(item['filePath']) as archive:
                self.assertIn(b'FILE "Renamed Game.bin"', archive.read('Renamed Game.cue'))
                self.assertEqual(archive.read('Renamed Game.bin'), b'game track')
            self.assertEqual(cue.read_bytes(), original)


if __name__ == '__main__':
    unittest.main()
