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


if __name__ == '__main__': unittest.main()
