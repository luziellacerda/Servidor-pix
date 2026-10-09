import copy, hashlib, importlib.util, json, os, sys, tempfile, unittest, zipfile
from pathlib import Path
SCRIPTS=Path(__file__).resolve().parents[2]/'docs/station-android/scripts';sys.path.insert(0,str(SCRIPTS))
spec=importlib.util.spec_from_file_location('auto_online_discovery',SCRIPTS/'atualizar-biblioteca-station.py');discovery=importlib.util.module_from_spec(spec);spec.loader.exec_module(discovery)
from station_online_profiles import prepare

class AutoOnlineTests(unittest.TestCase):
    def test_stable_new_game_gets_identity_and_profile_without_recompilation(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);volume=root/'volume';folder=volume/'snes';folder.mkdir(parents=True)
            rom=folder/'Synthetic.zip'
            def game(content):
                with zipfile.ZipFile(rom,'w') as archive:archive.writestr('Synthetic.sfc',content)
                os.utime(rom,ns=(10000000000,10000000000))
            data=b'synthetic content'*2048;game(data)
            home=root/'published';home.mkdir();base=root/'base.json';base.write_text('{"revision":1,"items":[]}');seed=root/'seed.json';seed.write_text('{"items":[]}');overrides=root/'overrides.json';overrides.write_text('{}')
            registry=home/'identities.json';registry.write_text('{"schemaVersion":2,"entries":[]}')
            profiles=home/'profiles.json';profiles.write_text('[]')
            engines=root/'engines.json';engine=dict(platform='snes',engineId='synthetic-core',coreSha256='a'*64,runtimeSha256='b'*64,extensions=['sfc'],options='',launchReady=True,recoveryProtocol='station-stream.v3');engines.write_text(json.dumps(dict(schemaVersion=1,engines=[engine])))
            config=dict(volumeRoot=str(volume),volumeDevice=volume.stat().st_dev,outputDirectory=str(home),baseIndex=str(base),seedSourceMap=str(seed),metadataOverrides=str(overrides),platforms={'snes':{'extensions':['.sfc']}},contentIdentityRegistry=str(registry),autoContentIdentity=True,autoOnlineProfiles=dict(registry=str(profiles),engineManifest=str(engines),maintainerAuthorizedTwoSeats=True))
            discovery.publish(config);discovery.publish(config)
            first=json.loads((home/'index.json').read_bytes());row=first['items'][0]
            self.assertEqual(row['contentSha256'],hashlib.sha256(data).hexdigest())
            published=json.loads(profiles.read_bytes());self.assertEqual(len(published),1);self.assertTrue(published[0]['approved']);self.assertEqual(published[0]['contentSha256'],row['contentSha256'])
            before_index=(home/'index.json').read_bytes();before_profiles=profiles.read_bytes();before_registry=registry.read_bytes()
            self.assertFalse(discovery.publish(config)['changed']);self.assertEqual((home/'index.json').read_bytes(),before_index);self.assertEqual(profiles.read_bytes(),before_profiles);self.assertEqual(registry.read_bytes(),before_registry)
            data=b'replaced synthetic content'*4096;game(data);os.utime(rom,ns=(200000000000,200000000000));discovery.publish(config);discovery.publish(config)
            second=json.loads((home/'index.json').read_bytes())['items'][0]
            self.assertEqual(second['itemId'],row['itemId']);self.assertEqual(second['contentSha256'],hashlib.sha256(data).hexdigest())
            updated=json.loads(profiles.read_bytes());self.assertEqual(updated[:1],published);self.assertEqual(len(updated),2)
            snapshot=(home/'index.json').read_bytes();registry.write_text('{"invalid":true}')
            with self.assertRaises(ValueError):discovery.publish(config)
            self.assertEqual((home/'index.json').read_bytes(),snapshot)
    def test_existing_profiles_solo_and_missing_native_engines(self):
        engine=dict(platform='n64',engineId='synthetic',coreSha256='a'*64,runtimeSha256='b'*64,extensions=['z64'],options='',launchReady=True,recoveryProtocol='station-stream.v3')
        manifest=dict(schemaVersion=1,engines=[engine]);item=dict(itemId='one',platform='n64',contentSha256='c'*64,artifact=dict(launchPath='Game.z64'),metadata=dict(players='1'))
        profiles=prepare([item],[],manifest);self.assertEqual(profiles[0]['maximumPlayers'],1);self.assertEqual(profiles[0]['allowedPlayerCounts'],[])
        old=copy.deepcopy(profiles);self.assertEqual(prepare([item],profiles,manifest),old)
        item['platform']='wiiu';self.assertEqual(prepare([item],profiles,manifest),old)
        item['platform']='n64';item['catalogVisible']=False;self.assertEqual(prepare([item],[],manifest),[])

if __name__=='__main__':unittest.main()
