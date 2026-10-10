"""Exercise complete automatic SNES/PS2/Saturn ingestion outside production."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile
from PIL import Image

SCRIPTS=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(SCRIPTS))
spec=importlib.util.spec_from_file_location('isolated_station_scanner',SCRIPTS/'atualizar-biblioteca-station.py')
scanner=importlib.util.module_from_spec(spec);spec.loader.exec_module(scanner)

@unittest.skipUnless(os.geteuid()==0,'real root importer permissions required')
class SaturnImportTests(unittest.TestCase):
    def test_complete_raw_cue_covers_identity_and_stable_rescan(self):
        with tempfile.TemporaryDirectory(prefix='station-saturn-isolated-') as name:
            root=Path(name);volume=root/'volume';home=root/'library';volume.mkdir();home.mkdir()
            def save(path,data):path.write_text(json.dumps(data)+'\n')
            base=home/'base.json';save(base,dict(revision=1,items=[]))
            identities=home/'identities.json';save(identities,dict(schemaVersion=2,entries=[]))
            profiles=home/'profiles.json';save(profiles,[])
            manifest=home/'engines.json';save(manifest,dict(schemaVersion=1,engines=[]))
            modes=home/'modes.json';save(modes,dict(schemaVersion=1,modes=[]))
            seed=home/'seed-source-map.json';save(seed,dict(items=[]))
            overrides=home/'metadata-overrides.json';save(overrides,{})
            config=dict(volumeRoot=str(volume),volumeDevice=volume.stat().st_dev,outputDirectory=str(home),
                baseIndex=str(base),seedSourceMap=str(seed),metadataOverrides=str(overrides),serviceGid=os.getgid(),
                contentIdentityRegistry=str(identities),autoContentIdentity=True,
                autoOnlineProfiles=dict(registry=str(profiles),engineManifest=str(manifest),preparedModes=str(modes),maintainerAuthorizedTwoSeats=True),
                platforms=dict(snes=dict(extensions=['.sfc']),ps2=dict(extensions=['.iso'],copyRawOnce=True,rawStorage='readonly-hardlink'),
                               saturn=dict(extensions=['.chd','.cue','.iso'],artifactMode='cue-disc',copyRawOnce=True,rawStorage='readonly-hardlink',catalogSeed='seed.json')))
            for platform in config['platforms']:
                folder=volume/platform;(folder/'media/revista').mkdir(parents=True)
            # Explicitly synthetic payloads validate transport descriptors, not emulation.
            inputs=[('snes','Synthetic SNES.sfc'),('ps2','Synthetic PS2.iso'),('saturn','Synthetic Saturn.chd')]
            for platform,filename in inputs:
                path=volume/platform/filename;path.write_bytes(b'SYNTHETIC-ISOLATED-PAYLOAD\0'*128);os.chown(path,1000,1000)
                Image.new('RGB',(480,720),(24,35,55)).save(volume/platform/'media/revista'/(path.stem+'.jpg'))
            cd=volume/'saturn';(cd/'Track 01.bin').write_bytes(b'\0'*4096)
            cue=cd/'Synthetic disc.cue';cue.write_text('FILE "Track 01.bin" BINARY\n  TRACK 01 MODE1/2048\n    INDEX 01 00:00:00\n')
            Image.new('RGB',(480,720),(24,35,55)).save(cd/'media/revista'/'Synthetic disc.jpg')
            save(cd/'seed.json',dict(schemaVersion=1,games={scanner.catalog_key('Synthetic Saturn'):dict(metadata=dict(description='Synthetic metadata from catalog'))}))
            first=scanner.publish(config,bootstrap=True)
            self.assertEqual(first['added'],4)
            index=json.loads((home/'index.json').read_text());rows=index['items']
            self.assertEqual(len(rows),4)
            self.assertEqual(len(json.loads(identities.read_text())['entries']),4)
            self.assertTrue(all(len(r['contentSha256'])==64 for r in rows))
            chd=next(r for r in rows if r['artifact']['launchPath'].endswith('.chd'))
            self.assertEqual(chd['metadata']['description'],'Synthetic metadata from catalog')
            self.assertEqual(Path(chd['filePath']).stat().st_ino,(cd/'Synthetic Saturn.chd').stat().st_ino)
            self.assertEqual((cd/'Synthetic Saturn.chd').stat().st_mode&0o777,0o444)
            disc=next(r for r in rows if r['artifact']['launchPath'].endswith('.cue'))
            self.assertEqual(disc['contentIdentityScheme'],'cue-set-v1')
            with zipfile.ZipFile(disc['filePath']) as archive:
                self.assertEqual(set(archive.namelist()),{'Synthetic disc.cue','Track 01.bin'})
            for row in rows:
                with Image.open(row['coverPath']) as image:
                    self.assertEqual((image.format,image.size),('JPEG',(480,720)))
            before=(home/'index.json').read_bytes()
            second=scanner.publish(config,bootstrap=True)
            self.assertEqual((second['changed'],second['added'],second['updated']),(False,0,0))
            self.assertEqual((home/'index.json').read_bytes(),before)

if __name__=='__main__':unittest.main(verbosity=2)
