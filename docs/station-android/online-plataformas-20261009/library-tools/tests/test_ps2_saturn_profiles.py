"""Verify real enrollment boundaries for the new platforms using isolated data."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from station_online_profiles import CEILINGS, NATIVE_CONTROLLERS, platform, prepare, validate_manifest, validate_modes

def engine(system):
    controller=NATIVE_CONTROLLERS[system]
    return dict(engineId='isolated-'+system,platform=system,library='isolated-core.so',
        runtimeLibrary='isolated-runtime.so',coreSha256='a'*64,runtimeSha256='b'*64,
        extensions=['iso','cso'] if system=='ps2' else ['cue','chd'],options='',
        launchReady=True,recoveryProtocol='station-stream.v3',maximumPlayers=2,
        controllerProfiles=[dict(controllerProfile=controller,maximumPlayers=2,
            configuration=dict(schemaVersion=1,controllerProfile=controller,devices=[1,1],coreOptions=''))])

class PlatformEnrollmentTests(unittest.TestCase):
    def test_exact_native_controller_binding_and_existing_records_preserved(self):
        previous=[dict(itemId='previous-unrelated-fixture',platform='psx',contentSha256='e'*64,
            engineId='isolated-previous-engine',coreSha256='a'*64,runtimeSha256='b'*64,
            profileId='previous-mode',profileSha256='f'*64,maximumPlayers=2,approved=False,
            controllerProfile='standard-2p-v1',mode='local-multiplayer',allowedPlayerCounts=[2])]
        for system in ('ps2','saturn'):
            e=engine(system);manifest=dict(schemaVersion=1,engines=[e])
            item=dict(itemId='isolated-'+system,platform=system,contentSha256='c'*64,
                      artifact=dict(launchPath='Game.iso' if system=='ps2' else 'Game.chd'))
            result=prepare([item],previous,manifest)
            self.assertEqual(result[:len(previous)],previous)
            self.assertEqual(len(result),2)
            self.assertEqual(result[-1]['allowedPlayerCounts'],[2])
            self.assertEqual(result[-1]['controllerProfile'],NATIVE_CONTROLLERS[system])
            config=e['controllerProfiles'][0]['configuration']
            self.assertEqual(result[-1]['profileSha256'],hashlib.sha256(json.dumps(config,ensure_ascii=False,separators=(',',':')).encode()).hexdigest())
            item['metadata']=dict(players='1')
            solo=prepare([item],[],manifest)[0]
            self.assertEqual((solo['maximumPlayers'],solo['allowedPlayerCounts']),(1,[]))

    def test_native_engine_requires_its_explicit_controllers(self):
        for system in ('ps2','saturn'):
            e=engine(system);e.pop('controllerProfiles')
            with self.assertRaisesRegex(ValueError,'explicit controller'):
                validate_manifest(dict(schemaVersion=1,engines=[e]))

    def test_platform_ceiling_rejects_three_players(self):
        for system in ('ps2','saturn'):
            e=engine(system);e['maximumPlayers']=3
            with self.assertRaisesRegex(ValueError,'platform ceiling'):
                validate_manifest(dict(schemaVersion=1,engines=[e]))

    def test_no_engine_or_wrong_engine_produces_no_online_binding(self):
        item=dict(itemId='isolated-ps2',platform='ps2',contentSha256='c'*64,artifact=dict(launchPath='Game.iso'))
        self.assertEqual(prepare([item],[],dict(schemaVersion=1,engines=[])),[])
        self.assertEqual(prepare([item],[],dict(schemaVersion=1,engines=[engine('saturn')])),[])
        e=engine('ps2');e['launchReady']=False
        self.assertEqual(prepare([item],[],dict(schemaVersion=1,engines=[e])),[])

    def test_explicit_unapproved_mode_stays_unapproved(self):
        item=dict(itemId='isolated-ps2',platform='ps2',contentSha256='c'*64,artifact=dict(launchPath='Game.iso'))
        mode=dict(itemId=item['itemId'],contentSha256=item['contentSha256'],platform='ps2',
                  profileId='isolated-review',maximumPlayers=2,allowedPlayerCounts=[2],approved=False,
                  controllerProfile=NATIVE_CONTROLLERS['ps2'],mode='versus',modeTitle='Fixture versus',instructions=[],sources=[])
        modes=dict(schemaVersion=1,modes=[mode])
        result=prepare([item],[],dict(schemaVersion=1,engines=[engine('ps2')]),modes)
        self.assertFalse(result[0]['approved'])
        changed=copy.deepcopy(item);changed['contentSha256']='d'*64
        self.assertEqual(prepare([changed],[],dict(schemaVersion=1,engines=[engine('ps2')]),modes),[])
        mode['maximumPlayers']=3;mode['allowedPlayerCounts']=[2,3]
        with self.assertRaisesRegex(ValueError,'player counts'):
            validate_modes(modes)

    def test_aliases_and_prior_platform_ceilings(self):
        for alias,canonical in [('ps2br','ps2'),('PlayStation 2','ps2'),('Sega Saturn','saturn'),('sega-saturn','saturn')]:
            self.assertEqual(platform(alias),canonical)
        self.assertEqual(CEILINGS['snes'],5)
        self.assertEqual(CEILINGS['megadrive'],2)
        self.assertTrue(all(CEILINGS[p]==4 for p in ('n64','dreamcast','gamecube','wii','wiiu')))

if __name__=='__main__':unittest.main(verbosity=2)
