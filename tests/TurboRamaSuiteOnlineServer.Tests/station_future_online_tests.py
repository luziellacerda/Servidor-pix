import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import types
import unittest

ROOT=Path(__file__).resolve().parents[2]
SCRIPTS=ROOT/'docs/station-android/scripts'
sys.path.insert(0,str(SCRIPTS))
from station_online_profiles import prepare,validate_manifest,validate_modes,NATIVE_CONTROLLERS,encoded
spec=importlib.util.spec_from_file_location('native_registration',SCRIPTS/'cadastrar-motor-online-station.py')
registration=importlib.util.module_from_spec(spec);spec.loader.exec_module(registration)

def engine(system,controller=None,maximum=None):
    maximum=maximum or (2 if system=='switch' else 4)
    controller=controller or NATIVE_CONTROLLERS[system]
    config=dict(schemaVersion=1,controllerProfile=controller,devices=['fixture-native-pad']*maximum,coreOptions='fixed fixture options')
    return dict(platform=system,engineId='fixture-'+system,coreSha256='a'*64,runtimeSha256='b'*64,extensions=['iso'],options='',launchReady=True,
        recoveryProtocol='station-stream.v3',maximumPlayers=maximum,controllerProfiles=[dict(controllerProfile=controller,maximumPlayers=maximum,configuration=config)])

def mode(system,count=4,controller=None):
    return dict(itemId='fixture-'+system,contentSha256='c'*64,platform=system,profileId='fixture-mode',maximumPlayers=count,allowedPlayerCounts=list(range(2,count+1)),approved=True,
        controllerProfile=controller or NATIVE_CONTROLLERS[system],mode='local-multiplayer',modeTitle='Fixture mode',instructions=['Fixture only.'],sources=[])

def item(system):return dict(itemId='fixture-'+system,platform=system,contentSha256='c'*64,artifact=dict(launchPath='Fixture.iso'),metadata={})

class FutureOnlineTests(unittest.TestCase):
    def test_all_future_platforms_bind_exact_four_or_two_controls(self):
        for system in NATIVE_CONTROLLERS:
            with self.subTest(platform=system):
                e=engine(system);maximum=e['maximumPlayers'];m=mode(system,maximum)
                result=prepare([item(system)],[],dict(schemaVersion=1,engines=[e]),dict(schemaVersion=1,modes=[m]))
                self.assertEqual(len(result),1);self.assertTrue(result[0]['approved']);self.assertEqual(result[0]['maximumPlayers'],maximum)
                self.assertEqual(result[0]['profileSha256'],hashlib.sha256(encoded(e['controllerProfiles'][0]['configuration'])[:-1]).hexdigest())
                self.assertEqual(prepare([item(system)],result,dict(schemaVersion=1,engines=[e]),dict(schemaVersion=1,modes=[m])),result)

    def test_wrong_ports_changed_content_and_small_engine_never_grant_four(self):
        m=mode('gamecube',controller='gamecube-gba-link-v1');e=engine('gamecube');manifest=dict(schemaVersion=1,engines=[e]);modes=dict(schemaVersion=1,modes=[m])
        self.assertEqual(prepare([item('gamecube')],[],manifest,modes),[])
        e=engine('gamecube',controller='gamecube-gba-link-v1',maximum=2)
        self.assertEqual(prepare([item('gamecube')],[],dict(schemaVersion=1,engines=[e]),modes),[])
        e=engine('gamecube',controller='gamecube-gba-link-v1');changed=item('gamecube');changed['contentSha256']='d'*64
        self.assertEqual(prepare([changed],[],dict(schemaVersion=1,engines=[e]),modes),[])

    def test_bad_modes_and_binary_hashes_fail_before_publication(self):
        e=engine('wii');e['coreSha256']='placeholder'
        with self.assertRaises(ValueError):validate_manifest(dict(schemaVersion=1,engines=[e]))
        e=engine('wii');del e['controllerProfiles']
        with self.assertRaises(ValueError):validate_manifest(dict(schemaVersion=1,engines=[e]))
        for count in (5,True):
            m=mode('wii');m['maximumPlayers']=count
            with self.assertRaises(ValueError):validate_modes(dict(schemaVersion=1,modes=[m]))
        m=mode('wii');m['allowedPlayerCounts']=[4,2]
        with self.assertRaises(ValueError):validate_modes(dict(schemaVersion=1,modes=[m]))

    def test_new_game_auto_two_and_new_engine_preserve_previous_refusals(self):
        e=engine('dreamcast');manifest=dict(schemaVersion=1,engines=[e]);game=item('dreamcast')
        first=prepare([game],[],manifest);self.assertEqual(first[0]['maximumPlayers'],2);self.assertTrue(first[0]['approved'])
        first[0]['approved']=False
        self.assertEqual(prepare([game],first,manifest),first)
        another=copy.deepcopy(e);another['engineId']='fixture-dreamcast-new';another['coreSha256']='e'*64
        second=prepare([game],first,dict(schemaVersion=1,engines=[e,another]))
        self.assertEqual(second[:1],first);self.assertEqual(len(second),2)

    def test_production_collection_modes_ready_without_fabricated_engines(self):
        folder=ROOT/'docs/station-android/online-plataformas-20261009'
        catalog=json.loads((folder/'catalogo-cruzado-completo.json').read_bytes());existing=json.loads((folder/'profiles-complete.json').read_bytes());manifest=json.loads((folder/'engines-app.json').read_bytes())
        generator_spec=importlib.util.spec_from_file_location('prepare_modes',SCRIPTS/'preparar-modos-servidor-todas-plataformas.py')
        generator=importlib.util.module_from_spec(generator_spec);generator_spec.loader.exec_module(generator)
        previous=ROOT/'docs/station-android/online-1a5-20261009'
        modes=generator.prepare(catalog,json.loads((previous/'primary-mode-review.json').read_bytes()),json.loads((previous/'sources.json').read_bytes()))
        self.assertEqual(len({m['itemId'] for m in modes['modes']}),273)
        self.assertEqual(prepare(catalog['items'],existing,manifest,modes),existing)
        self.assertTrue(all(not any(k in m for k in ('engineId','coreSha256','runtimeSha256')) for m in modes['modes']))
        self.assertTrue(all(m['maximumPlayers']==1 for m in modes['modes'] if m['platform']=='switch'))

    def test_registration_stages_real_binaries_and_rejects_changed_hash(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);artifacts=root/'source';artifacts.mkdir();(artifacts/'core.so').write_bytes(b'fixture core binary');(artifacts/'runtime.so').write_bytes(b'fixture runtime binary')
            e=engine('wiiu');e.update(library='core.so',runtimeLibrary='runtime.so',coreSha256=registration.digest(artifacts/'core.so'),runtimeSha256=registration.digest(artifacts/'runtime.so'))
            files={'catalog':dict(items=[item('wiiu')]),'profiles':[],'current':dict(schemaVersion=1,engines=[]),'modes':dict(schemaVersion=1,modes=[mode('wiiu')]),'incoming':dict(schemaVersion=1,engines=[e])}
            for key,value in files.items():(root/(key+'.json')).write_text(json.dumps(value))
            args=types.SimpleNamespace(**{key:root/(key+'.json') for key in files},artifacts=artifacts,output=root/'candidate')
            registration.stage(args)
            receipt=json.loads((args.output/'receipt.json').read_bytes());self.assertTrue(receipt['realBinaryHashesVerified']);self.assertEqual(receipt['profilesAdded'],1)
            self.assertEqual(json.loads((root/'profiles.json').read_bytes()),[])
            (artifacts/'core.so').write_bytes(b'changed fixture')
            with self.assertRaises(ValueError):registration.verify_binaries(files['incoming'],artifacts)

if __name__=='__main__':unittest.main()
