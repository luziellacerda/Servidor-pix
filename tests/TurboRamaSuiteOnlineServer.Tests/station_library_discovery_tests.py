import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
from PIL import Image

SCRIPTS = Path(__file__).resolve().parents[2] / 'docs/station-android/scripts'
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location('discovery', SCRIPTS/'atualizar-biblioteca-station.py')
discovery = importlib.util.module_from_spec(spec); spec.loader.exec_module(discovery)


class DiscoveryTests(unittest.TestCase):
    def test_add_without_xml_preserve_ids_metadata_and_stability(self):
        with tempfile.TemporaryDirectory() as temp:
            r=Path(temp); volume=r/'volume'; platform=volume/'n64'; platform.mkdir(parents=True)
            base=r/'base.json'; seed=r/'seed.json'; overrides=r/'overrides.json'
            base.write_text(json.dumps({'revision':4,'items':[]}));seed.write_text('{"items":[]}');overrides.write_text('{}')
            config=dict(volumeRoot=str(volume),volumeDevice=volume.stat().st_dev,outputDirectory=str(r/'content'),
                        baseIndex=str(base),seedSourceMap=str(seed),metadataOverrides=str(overrides),
                        platforms={'n64':{'extensions':['.z64','.v64','.n64']}})
            rom=platform/'Synthetic.zip'
            with zipfile.ZipFile(rom,'w') as z:z.writestr('Synthetic.z64',b'\x80\x37\x12\x40'+b'synthetic'*64)
            old=20_000_000_000;os.utime(rom,ns=(old,old))
            a=discovery.publish(config);self.assertEqual(a['added'],0)
            b=discovery.publish(config);self.assertEqual(b['added'],1);self.assertEqual(b['placeholderCovers'],1)
            index_path=r/'content/index.json';index=json.loads(index_path.read_text());item=index['items'][0]
            item_id=item['itemId'];old_revision=item['revision'];original_game=item['filePath']
            self.assertFalse(discovery.publish(config)['changed'])
            (platform/'gamelist.xml').write_text('<gameList><game><path>./Synthetic.zip</path><desc>Sinopse sintética.</desc></game><game><path>missing.zip</path></game></gameList>')
            c=discovery.publish(config);self.assertEqual(c['missingXmlRoms'],1);self.assertTrue(c['changed'])
            item=json.loads(index_path.read_text())['items'][0];self.assertEqual(item['itemId'],item_id);self.assertEqual(item['revision'],old_revision)
            self.assertEqual(item['metadata']['description'],'Sinopse sintética.')
            covers=platform/'media/revista';covers.mkdir(parents=True);Image.new('RGB',(1024,1536),'red').save(covers/'Synthetic.png')
            self.assertFalse(discovery.publish(config)['changed'])
            d=discovery.publish(config);self.assertEqual(d['updated'],1)
            item=json.loads(index_path.read_text())['items'][0];self.assertEqual(item['itemId'],item_id);self.assertEqual(item['revision'],old_revision+1)
            with Image.open(item['coverPath']) as image:self.assertEqual(image.size,(480,720))
            self.assertTrue(Path(original_game).exists())
            overrides.write_text(json.dumps({'n64:Synthetic.zip':{'description':'Descrição manual','name':'Nome manual'}}))
            self.assertTrue(discovery.publish(config)['changed'])
            item=json.loads(index_path.read_text())['items'][0];self.assertEqual(item['name'],'Nome manual')
            self.assertEqual(item['metadata']['description'],'Descrição manual')
            before=index_path.read_bytes();rom.unlink();discovery.publish(config);self.assertEqual(index_path.read_bytes(),before)
            config['volumeDevice']=-1
            with self.assertRaises(ValueError):discovery.publish(config)
            self.assertEqual(index_path.read_bytes(),before)

    def test_unsafe_archive_and_conflicting_cover_do_not_replace_catalog(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); rom=root/'Synthetic.zip';tmp=root/'curated.zip'
            with zipfile.ZipFile(rom,'w') as z:z.writestr('../escape.z64',b'synthetic')
            with self.assertRaises(ValueError):discovery.prepare_artifact(rom,tmp,{'.z64'})
            with zipfile.ZipFile(rom,'w') as z:z.writestr('one.z64',b'a');z.writestr('two.z64',b'b')
            with self.assertRaises(ValueError):discovery.prepare_artifact(rom,tmp,{'.z64'})

    def test_arcade_sets_remain_closed_and_bios_is_not_a_game(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); volume=root/'volume'; platform=volume/'neogeo'; platform.mkdir(parents=True)
            collection=platform/'Collection'; collection.mkdir()
            rom=collection/'synthetic.zip'; bios=platform/'neogeo.zip'
            with zipfile.ZipFile(rom,'w') as z:z.writestr('chip-p1.bin',b'program');z.writestr('chip-c1.bin',b'graphics')
            with zipfile.ZipFile(bios,'w') as z:z.writestr('synthetic-bios.bin',b'bios')
            original=rom.read_bytes(); original_bios=bios.read_bytes()
            for p in [rom,bios]:os.utime(p,ns=(20_000_000_000,20_000_000_000))
            covers=platform/'media/revista/Collection'; covers.mkdir(parents=True)
            Image.new('RGB',(1024,1536),'blue').save(covers/'synthetic.png')
            base=root/'base.json';base.write_text('{"revision":8,"items":[]}')
            seed=root/'seed.json';seed.write_text('{"items":[]}')
            overrides=root/'overrides.json';overrides.write_text('{}')
            config=dict(volumeRoot=str(volume),volumeDevice=volume.stat().st_dev,outputDirectory=str(root/'content'),
                        baseIndex=str(base),seedSourceMap=str(seed),metadataOverrides=str(overrides),
                        platforms={'neogeo':{'extensions':['.zip'],'artifactMode':'arcade-set','companions':['neogeo.zip']}})
            self.assertEqual(discovery.publish(config)['added'],0)
            self.assertEqual(discovery.publish(config)['added'],1)
            index_path=root/'content/index.json'; item=json.loads(index_path.read_text())['items'][0]
            self.assertEqual(item['platform'],'neogeo');self.assertEqual(item['folderPath'],['Collection'])
            self.assertEqual(item['artifact']['launchPath'],'synthetic.zip');self.assertEqual(item['artifact']['fileCount'],2)
            with zipfile.ZipFile(item['filePath']) as package:
                self.assertEqual(package.namelist(),['synthetic.zip','neogeo.zip'])
                self.assertEqual(package.read('synthetic.zip'),original)
                self.assertEqual(package.read('neogeo.zip'),original_bios)
            self.assertEqual(rom.read_bytes(),original)
            self.assertFalse(discovery.publish(config)['changed'])
            tmp=root/'reproduced.zip';discovery.prepare_arcade_artifact(rom,tmp,[bios])
            self.assertEqual(tmp.read_bytes(),Path(item['filePath']).read_bytes())
            old_id=item['itemId'];old_revision=item['revision'];old_file=item['filePath']
            with zipfile.ZipFile(bios,'w') as z:z.writestr('synthetic-bios.bin',b'updated bios')
            os.utime(bios,ns=(30_000_000_000,30_000_000_000))
            self.assertFalse(discovery.publish(config)['changed'])
            self.assertEqual(discovery.publish(config)['updated'],1)
            changed=json.loads(index_path.read_text())['items'][0]
            self.assertEqual(changed['itemId'],old_id);self.assertEqual(changed['revision'],old_revision+1)
            self.assertTrue(Path(old_file).exists());self.assertEqual(len(json.loads(index_path.read_text())['items']),1)

    def test_arcade_rejects_unsafe_duplicate_and_corrupt_members(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);rom=root/'game.zip';target=root/'prepared.zip'
            with zipfile.ZipFile(rom,'w') as z:z.writestr('../escape.bin',b'chip')
            with self.assertRaises(ValueError):discovery.prepare_arcade_artifact(rom,target,[])
            with zipfile.ZipFile(rom,'w') as z:z.writestr('CHIP.bin',b'chip');z.writestr('chip.bin',b'other')
            with self.assertRaises(ValueError):discovery.prepare_arcade_artifact(rom,target,[])
            with zipfile.ZipFile(rom,'w',zipfile.ZIP_STORED) as z:z.writestr('chip.bin',b'UNIQUE_SYNTHETIC_CHIP')
            data=rom.read_bytes().replace(b'UNIQUE_SYNTHETIC_CHIP',b'BROKEN_SYNTHETIC_CHIP');rom.write_bytes(data)
            with self.assertRaises(ValueError):discovery.prepare_arcade_artifact(rom,target,[])
            with self.assertRaises(ValueError):discovery.arcade_companions(root,{'companions':['../outside.zip']})
            with self.assertRaises(ValueError):discovery.arcade_companions(root,{'companions':['missing.zip']})
            with self.assertRaises(ValueError):discovery.prepare_arcade_artifact(rom,target,[rom])


if __name__ == '__main__': unittest.main()
