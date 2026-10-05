import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from PIL import Image

SCRIPTS = Path(__file__).resolve().parents[2] / 'docs/station-android/scripts'
sys.path.insert(0, str(SCRIPTS))
import station_disc as disc
spec = importlib.util.spec_from_file_location('discovery', SCRIPTS/'atualizar-biblioteca-station.py')
discovery = importlib.util.module_from_spec(spec); spec.loader.exec_module(discovery)

@unittest.skipUnless(os.environ.get('STATION_CHDMAN'), 'Set STATION_CHDMAN to the real verifier')
class DiscDiscoveryTests(unittest.TestCase):
    def create_disc(self, root):
        (root/'data.bin').write_bytes(bytes(2352 * 300))
        (root/'disc.cue').write_text('FILE "data.bin" BINARY\n TRACK 01 MODE1/2352\n  INDEX 01 00:00:00\n')
        tool=Path(os.environ['STATION_CHDMAN'])
        subprocess.run([str(tool),'createcd','-i',str(root/'disc.cue'),'-o',str(root/'Game.img')],
                       env=disc.verifier_environment(tool),check=True,capture_output=True)
        return root/'Game.img',dict(chdVerifier=str(tool))

    def test_real_chd_delivers_raw_with_correct_extension_without_bios(self):
        with tempfile.TemporaryDirectory() as t:
            r=Path(t);source,config=self.create_disc(r);original=source.read_bytes()
            delivered,artifact=disc.prepare_disc_artifact(source,r/'delivery.zip',[],config,discovery.load_module('preparar-indice-artefatos').describe)
            self.assertEqual(delivered,source);self.assertEqual(source.read_bytes(),original)
            self.assertEqual((artifact['format'],artifact['fileName'],artifact['launchPath'],artifact['fileCount']),('raw','Game.chd','Game.chd',1))
            self.assertEqual(artifact['sha256'],hashlib.sha256(original).hexdigest())
            self.assertFalse(disc.bios_status([])['biosAvailable'])
            source.write_bytes(original[:len(original)//2])
            with self.assertRaises(disc.DiscPending):disc.validate_chd(source,config)
            source.write_bytes(b'not a CHD')
            with self.assertRaises(disc.DiscPending):disc.validate_chd(source,config)

    def test_nested_cd_is_not_arcade_and_old_fingerprints_are_unchanged(self):
        with tempfile.TemporaryDirectory() as t:
            r=Path(t);volume=r/'volume';neo=volume/'neogeo';cd=neo/'neogeocd';cd.mkdir(parents=True)
            with zipfile.ZipFile(neo/'neogeo.zip','w') as z:z.writestr('bios.bin',b'arcade BIOS')
            with zipfile.ZipFile(neo/'old.zip','w') as z:z.writestr('chip.bin',b'arcade chip')
            for p in neo.glob('*.zip'):os.utime(p,ns=(20_000_000_000,20_000_000_000))
            (r/'base').write_text('{"revision":9,"items":[]}');(r/'seed').write_text('{"items":[]}');(r/'overrides').write_text('{}')
            config=dict(volumeRoot=str(volume),volumeDevice=volume.stat().st_dev,outputDirectory=str(r/'out'),
                        baseIndex=str(r/'base'),seedSourceMap=str(r/'seed'),metadataOverrides=str(r/'overrides'),
                        platforms={'neogeo':{'extensions':['.zip'],'artifactMode':'arcade-set','companions':['neogeo.zip']}})
            discovery.publish(config);discovery.publish(config)
            old=json.loads((r/'out/index.json').read_text())['items'][0]
            source,spec=self.create_disc(cd);os.utime(source,ns=(20_000_000_000,20_000_000_000))
            with zipfile.ZipFile(cd/'neocdz.zip','w') as z:z.writestr('unknown.bin',b'not valid firmware')
            (cd/'media/revista').mkdir(parents=True);Image.new('RGB',(1024,1536),'blue').save(cd/'media/revista/Game.png')
            (cd/'gamelist.xml').write_text('<gameList><game><path>Game.img</path><name>CD Name</name><desc>CD synopsis</desc></game></gameList>')
            config['platforms']['neogeocd']=dict(spec,folder='neogeo/neogeocd',extensions=['.img','.chd'],artifactMode='chd-disc')
            report=discovery.publish(config,bootstrap=True);items=json.loads((r/'out/index.json').read_text())['items']
            self.assertEqual(report['added'],1);self.assertEqual(report['updated'],0);self.assertEqual(len(items),2)
            self.assertEqual(items[0],old);self.assertEqual(items[1]['platform'],'neogeocd')
            self.assertEqual(items[1]['metadata']['description'],'CD synopsis');self.assertEqual(items[1]['artifact']['launchPath'],'Game.chd')
            self.assertFalse(discovery.publish(config)['changed'])
            with Image.open(items[1]['coverPath']) as im:self.assertEqual(im.size,(480,720))

    def test_supplied_exact_bios_uses_closed_driver_zip_and_rejects_wrong_bytes(self):
        with tempfile.TemporaryDirectory() as t:
            r=Path(t);source,config=self.create_disc(r);firmware=b'F'*524288;zoom=b'Z'*131072
            identities=(('neocd.bin',len(firmware),zipfile.crc32(firmware),hashlib.sha1(firmware).hexdigest()),)
            zoom_identity=('000-lo.lo',len(zoom),zipfile.crc32(zoom),hashlib.sha1(zoom).hexdigest())
            bios=r/'supplied.zip'
            with zipfile.ZipFile(bios,'w') as z:z.writestr('different-label.rom',firmware);z.writestr('zoom.bin',zoom)
            # Test firmware identities are separate; production values never change.
            with patch.object(disc,'FIRMWARE',identities), patch.object(disc,'ZOOM',zoom_identity):
                _,artifact=disc.prepare_disc_artifact(source,r/'delivery.zip',[bios],config,discovery.load_module('preparar-indice-artefatos').describe)
            self.assertEqual((artifact['format'],artifact['launchPath'],artifact['fileCount']),('zip','Game.chd',2))
            with zipfile.ZipFile(r/'delivery.zip') as z:
                self.assertEqual(z.read('Game.chd'),source.read_bytes());bios=r/'bios.zip';bios.write_bytes(z.read('neocdz.zip'))
            with zipfile.ZipFile(bios) as z:self.assertEqual(z.namelist(),['neocd.bin','000-lo.lo'])
            with self.assertRaises(disc.DiscPending):disc.bios_members([bios])

if __name__=='__main__':unittest.main()
