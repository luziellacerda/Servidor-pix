import hashlib, importlib.util, json, sys, tempfile, unittest, zipfile
from pathlib import Path
SCRIPTS=Path(__file__).resolve().parents[2]/'docs/station-android/scripts'
sys.path.insert(0,str(SCRIPTS))
from station_content_sets import bind_set, canonical, cue_references
spec=importlib.util.spec_from_file_location('set_discovery',SCRIPTS/'atualizar-biblioteca-station.py')
discovery=importlib.util.module_from_spec(spec);spec.loader.exec_module(discovery)

class ContentSetTests(unittest.TestCase):
    def package(self,directory,files,launch,platform='psx',compression=zipfile.ZIP_STORED):
        file=Path(directory)/('package-'+str(len(list(Path(directory).iterdir())))+'.zip')
        with zipfile.ZipFile(file,'w',compression=compression) as archive:
            for name,body in files.items():archive.writestr(name,body)
        row=dict(itemId='synthetic-game',platform=platform,filePath=str(file),artifact=dict(format='zip',launchPath=launch,sha256=hashlib.sha256(file.read_bytes()).hexdigest(),expandedSizeBytes=sum(len(b) for b in files.values()),fileCount=len(files)))
        entry=dict(itemId=row['itemId'],platform=platform,artifactSha256=row['artifact']['sha256'],launchPath=launch,expandedSizeBytes=row['artifact']['expandedSizeBytes'],fileCount=len(files),contentSha256=hashlib.sha256(files[launch]).hexdigest())
        return row,entry
    def test_tracks_change_identity_without_changing_cue(self):
        with tempfile.TemporaryDirectory() as directory:
            files={'Game.cue':b'FILE "Game.bin" BINARY\n TRACK 01 MODE2/2352\n','Game.bin':b'first track'}
            row,entry=self.package(directory,files,'Game.cue')
            first=bind_set(row,entry)
            files['Game.bin']=b'different track'
            other,entry2=self.package(directory,files,'Game.cue')
            second=bind_set(other,entry2)
            self.assertEqual(entry['contentSha256'],entry2['contentSha256'])
            self.assertNotEqual(first['contentSha256'],second['contentSha256'])
            self.assertEqual(first['contentIdentityScheme'],'cue-set-v1')
    def test_container_compression_does_not_change_content_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            files={'Disc.cue':b'FILE "Track.bin" BINARY\n','Track.bin':b'content'*4096}
            row,entry=self.package(directory,files,'Disc.cue')
            other,second=self.package(directory,files,'Disc.cue',compression=zipfile.ZIP_DEFLATED)
            self.assertNotEqual(entry['artifactSha256'],second['artifactSha256'])
            self.assertEqual(bind_set(row,entry)['contentSha256'],bind_set(other,second)['contentSha256'])
    def test_cue_relative_tracks_and_utf8_order(self):
        with tempfile.TemporaryDirectory() as directory:
            files={'folder/Jogo.cue':'FILE "faixas/áudio.bin" BINARY\nFILE "a.bin" BINARY\n'.encode(), 'folder/faixas/áudio.bin':b'audio','folder/a.bin':b'data'}
            row,entry=self.package(directory,files,'folder/Jogo.cue')
            expected=canonical('Jogo.cue',[('Jogo.cue',len(files['folder/Jogo.cue']),hashlib.sha256(files['folder/Jogo.cue']).hexdigest()),('faixas/áudio.bin',5,hashlib.sha256(b'audio').hexdigest()),('a.bin',4,hashlib.sha256(b'data').hexdigest())])
            self.assertEqual(bind_set(row,entry)['contentSha256'],hashlib.sha256(expected).hexdigest())
    def test_missing_unsafe_and_malformed_dependencies_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            for reference in ('../outside.bin','/absolute.bin','C:\\track.bin','missing.bin'):
                files={'Game.cue':('FILE "'+reference+'" BINARY\n').encode(),'Game.bin':b'track'}
                row,entry=self.package(directory,files,'Game.cue')
                with self.assertRaises(ValueError):bind_set(row,entry)
            for raw in (b'REM no file\n',b'FILE "a.bin"\n',b'FILE '+b'x'*(512*1024)):
                with self.assertRaises(ValueError):cue_references(raw)
    def test_wiiu_requires_and_binds_every_content_file(self):
        with tempfile.TemporaryDirectory() as directory:
            files={'code/Game.rpx':b'code','content/data.bin':b'first data','meta/meta.xml':b'metadata'}
            row,entry=self.package(directory,files,'code/Game.rpx','wiiu');first=bind_set(row,entry)
            files['content/data.bin']=b'different data'
            other,entry2=self.package(directory,files,'code/Game.rpx','wiiu')
            self.assertNotEqual(first['contentSha256'],bind_set(other,entry2)['contentSha256'])
            files.pop('meta/meta.xml');missing,entry3=self.package(directory,files,'code/Game.rpx','wiiu')
            with self.assertRaises(ValueError):bind_set(missing,entry3)
    def test_persistent_registry_binds_scheme_and_withdraws_replaced_artifact(self):
        with tempfile.TemporaryDirectory() as directory:
            files={'Game.cue':b'FILE "Game.bin" BINARY\n','Game.bin':b'track'}
            row,entry=self.package(directory,files,'Game.cue');identity=bind_set(row,entry)
            registry=Path(directory)/'registry.json';registry.write_text(json.dumps(dict(schemaVersion=2,entries=[identity])))
            loaded=discovery.content_identities(registry);discovery.bind_content_identities([row],loaded)
            self.assertEqual(row['contentSha256'],identity['contentSha256']);self.assertEqual(row['contentIdentityScheme'],'cue-set-v1')
            row['artifact']['sha256']='f'*64;discovery.bind_content_identities([row],loaded)
            self.assertNotIn('contentSha256',row);self.assertNotIn('contentIdentityScheme',row)
            identity['contentIdentityScheme']='unknown-scheme';registry.write_text(json.dumps(dict(schemaVersion=2,entries=[identity])))
            with self.assertRaises(ValueError):discovery.content_identities(registry)

if __name__=='__main__':unittest.main()
