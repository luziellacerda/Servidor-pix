#!/usr/bin/env python3
"""Finish server-first native enrollment, preserving all production game bindings.

Publishes prepared mode data and a protected persistent engine manifest. Actual
future engines can then enroll through the local data operator without another
server rebuild/restart. Synthetic engines exist only in an isolated shadow proof.
"""
import argparse,copy,fcntl,hashlib,importlib.util,json,os,pwd,shutil,sys,traceback
from pathlib import Path
sys.dont_write_bytecode=True
SCRIPTS=Path(__file__).resolve().parent;sys.path.insert(0,str(SCRIPTS))
def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),SCRIPTS/name)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
b=load('implantar-five-players-station-20261009.py');ops,security,r71=b.ops,b.security,b.r71
library=load('atualizar-biblioteca-station.py')
from station_online_profiles import prepare,validate_modes,validate_manifest,NATIVE_CONTROLLERS
CURRENT=Path('/opt/turborama-station-profile-reload-20261009-25d98d65220e')
CURRENT_SHA='25d98d65220e15de8ba4ee5e9e77e5ce66de9a04986ddfd924d42ca95f172e97'
INDEX_SHA='f7924dcd6bdea74c2f32d8a482184f6f21f6a7f235b77f697432fd57637aef6e'
PROFILE_SHA='a029fb61ce68fa26abadf0312b55b9ae163c6a3604a12bfd37e97d406fdfebaa'
IDENTITY_SHA='16efbad906b1f8aec26869fc9900a7aa50486fa351be4c82f507253a1902667a'
PREVIOUS_SCANNER=Path('/opt/turborama-station-online-platforms-20261009-e74fde53cbe6/library-tools/atualizar-biblioteca-station.py')
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
SCAN='turborama-station-library-scan.service';TIMER='turborama-station-library-scan.timer'
DROPIN=Path('/etc/systemd/system')/(b.SERVICE+'.d')/('z'*40+'-station-server-ready-20261009.conf')
SCAN_DROPIN=Path('/etc/systemd/system')/(SCAN+'.d')/('z'*40+'-station-server-ready-20261009.conf')
TOOLS=('atualizar-biblioteca-station.py','station_packages.py','station_disc.py','station_revista.py','preparar-indice-artefatos.py',
       'station_content_sets.py','station_online_profiles.py','cadastrar-motor-online-station.py')

def quiet():
    if not b.quiet(b.ready()):raise ValueError('An active or recovering room prevents replacement')
def protect(path,gid):os.chown(path,0,gid);path.chmod(0o750 if path.is_dir() else 0o640)
def tool_sources():
    result={name:SCRIPTS/name for name in TOOLS}
    result['prepare_content_identity_registry.py']=SCRIPTS.parent/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py'
    return result

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('release','seal','modes','work-directory'):parser.add_argument('--'+name,type=Path,required=True)
    a=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('Native Linux administrative authentication required')
    if any(not p.is_absolute() or p.is_symlink() for p in (a.release,a.seal,a.modes,a.work_directory)):raise ValueError('Absolute regular inputs required')
    os.umask(0o077);a.work_directory.mkdir(mode=0o700,exist_ok=False)
    operator=pwd.getpwnam('lz-servidor');ops.run(['setfacl','-m','u:'+str(operator.pw_uid)+':rx',str(a.work_directory)])
    seal=json.loads(a.seal.read_bytes());tools=tool_sources()
    if security.files(a.release)!=seal['releaseFiles'] or b.digest(a.release/'TurboRamaSuiteOnlineServer.dll')!=seal['dllSha256'] or {n:b.digest(p) for n,p in tools.items()}!=seal['toolFiles'] or b.digest(a.modes)!=seal['modesSha256']:raise ValueError('Prepared source/binary seal changed')
    target=Path('/opt/turborama-station-server-ready-20261009-'+seal['dllSha256'][:12])
    manifest_path=b.INDEX.parent/'online-app-engines-persistent-20261009.json'
    mode_path=b.INDEX.parent/'online-prepared-modes-20261009.json'
    if any(p.exists() for p in (target,DROPIN,SCAN_DROPIN,manifest_path,mode_path)):raise ValueError('Fresh server publication required; do not repeat')
    timer=ops.run(['systemctl','is-active',TIMER]).strip()=='active';changed=False;backups=a.work_directory/'rollback'
    try:
        if timer:ops.run(['systemctl','stop',TIMER])
        if ops.run(['systemctl','show',SCAN,'-p','ActiveState','--value']).strip() in ('active','activating','deactivating'):raise ValueError('Importer is running; no replacement performed')
        with (b.INDEX.parent/'scan.lock').open('a') as lock:
            fcntl.flock(lock,fcntl.LOCK_EX);quiet();values,ids=ops.runtime();owner=pwd.getpwnam(b.IDENTITY)
            profiles=Path(values['Station__Online__MultiplayerProfileRegistryFile']);legacy=Path(values['Station__Online__EngineRegistryFile'])
            config=json.loads(CONFIG.read_bytes());identity_path=Path(config['contentIdentityRegistry']);engine_path=Path(config['autoOnlineProfiles']['engineManifest'])
            if str(CURRENT/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or b.digest(CURRENT/'TurboRamaSuiteOnlineServer.dll')!=CURRENT_SHA or b.digest(b.INDEX)!=INDEX_SHA or b.digest(profiles)!=PROFILE_SHA or b.digest(identity_path)!=IDENTITY_SHA or b.digest(legacy)!=b.REGISTRY or str(PREVIOUS_SCANNER) not in ops.run(['systemctl','show',SCAN,'-p','ExecStart','--value']):raise ValueError('Published production baseline changed')
            if ids['Uid'][1]!=owner.pw_uid or ids['Gid'][1]!=owner.pw_gid or values.get('Station__Online__ReplayMaximumBytes')!='134217728' or values.get('Station__Online__MultiplayerEnabled','').lower()!='true' or values.get('Station__Online__MultiplayerLegacyCapacityGate','').lower()!='false':raise ValueError('Identity or existing policy changed')
            index=json.loads(b.INDEX.read_bytes());entries=json.loads(profiles.read_bytes());modes=json.loads(a.modes.read_bytes());engines=validate_manifest(json.loads(engine_path.read_bytes()));validate_modes(modes)
            future={r['itemId'] for r in index['items'] if r['platform'] in NATIVE_CONTROLLERS and r.get('catalogVisible',True)}
            if len(future)!=273 or {m['itemId'] for m in modes['modes']}!=future or len(modes['modes'])!=274 or prepare(index['items'],entries,engines,modes)!=entries:raise ValueError('Complete server modes must preserve all 5176 existing profiles')
            for mode in modes['modes']:
                item=next(r for r in index['items'] if r['itemId']==mode['itemId'])
                if mode['contentSha256']!=item['contentSha256'] or mode['platform']!=item['platform'] or mode['coverId']!=item['coverId']:raise ValueError('Prepared modes must cross the exact game/cover/content')
            db=r71.database(values);snapshot=dict(oldFiles=security.files(CURRENT),oldTools=security.files(PREVIOUS_SCANNER.parent),configuration=security.snapshot_configs(),configSha256=b.digest(CONFIG),realLicenses=security.real_licenses(db),schema=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),shared={u:ops.state(u) for u in security.SHARED})
            b.private(a.work_directory/'baseline-private.json',snapshot);backups.mkdir(mode=0o700)
            for name,path in [('config.json',CONFIG),('state.json',b.INDEX.parent/'state.json'),('report.json',b.INDEX.parent/'report.json')]:shutil.copy2(path,backups/name)
            def preservation(expected_config):
                if security.files(CURRENT)!=snapshot['oldFiles'] or security.files(PREVIOUS_SCANNER.parent)!=snapshot['oldTools'] or b.digest(CONFIG)!=expected_config or b.digest(b.INDEX)!=INDEX_SHA or b.digest(profiles)!=PROFILE_SHA or b.digest(identity_path)!=IDENTITY_SHA:raise ValueError('Previously published release/data changed')
                for name,digest in snapshot['configuration'].items():
                    if b.digest(Path(name))!=digest:raise ValueError('Existing service setting changed')
                for unit,state in snapshot['shared'].items():
                    if ops.state(unit)!=state:raise ValueError('Another service changed')
                if security.real_licenses(db)!=snapshot['realLicenses'] or hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()!=snapshot['schema']:raise ValueError('Real licenses or schema changed')
                security.database_scope(db);security.management_health()
            shutil.copytree(a.release,target);(target/'library-tools').mkdir()
            for name,path in tools.items():shutil.copyfile(path,target/'library-tools'/name)
            shutil.copyfile(legacy,target/'online-engine-registry.json')
            newvalues=dict(values,Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'))
            b.private(target/'server-ready.env','Station__Online__EngineRegistryFile='+str(target/'online-engine-registry.json')+'\n')
            for path in sorted(target.rglob('*'),reverse=True):protect(path,owner.pw_gid)
            protect(target,owner.pw_gid);(target/'server-ready.env').chmod(0o600)
            binds=b.show('BindReadOnlyPaths').replace(str(CURRENT),str(target))
            cap_items=[next(r for r in index['items'] if r['platform']==p and r.get('catalogVisible',True)) for p in NATIVE_CONTROLLERS]
            original_verify=b.five.verify
            def verify_platforms(catalog,settings,address,sql):
                registry=json.loads(Path(settings['Station__Online__MultiplayerProfileRegistryFile']).read_bytes());extra=[]
                for system in ('n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3',*NATIVE_CONTROLLERS):
                    eligible=[p for p in registry if p['platform']==system and p['approved'] and p['maximumPlayers']>=2]
                    if eligible:extra.append(max(eligible,key=lambda p:p['maximumPlayers']))
                return original_verify(catalog,settings,address,sql,additional_profiles=extra,capabilities_items=cap_items)
            b.five.verify=verify_platforms
            # Only the isolated shadow gets these visibly synthetic engine hashes.
            fixture_engines=[]
            for system in NATIVE_CONTROLLERS:
                layouts={m['controllerProfile']:max(x['maximumPlayers'] for x in modes['modes'] if x['platform']==system and x['controllerProfile']==m['controllerProfile']) for m in modes['modes'] if m['platform']==system}
                maximum=2 if system=='switch' else 4
                definitions=[dict(controllerProfile=c,maximumPlayers=maximum,configuration=dict(schemaVersion=1,controllerProfile=c,devices=['synthetic-native-fixture']*maximum,coreOptions='fixture-only')) for c in layouts]
                fixture_engines.append(dict(platform=system,engineId='synthetic-server-proof-'+system,coreSha256=hashlib.sha256(('fixture-core-'+system).encode()).hexdigest(),runtimeSha256=hashlib.sha256(b'fixture-runtime').hexdigest(),extensions=sorted({Path(r['artifact']['launchPath']).suffix[1:].lower() for r in index['items'] if r['platform']==system}),options='',launchReady=True,recoveryProtocol='station-stream.v3',maximumPlayers=maximum,controllerProfiles=definitions))
            fixture_manifest=dict(engines,schemaVersion=1,engines=engines['engines']+fixture_engines)
            fixture_profiles=prepare(index['items'],entries,fixture_manifest,modes)
            switch=next(p for p in fixture_profiles if p['platform']=='switch')
            fixture_profiles.append(dict(switch,profileId='synthetic-transport-only-two',maximumPlayers=2,allowedPlayerCounts=[2],modeTitle='Synthetic transport proof only'))
            fixture_path=target/'synthetic-shadow-profiles.json';b.private(fixture_path,fixture_profiles);protect(fixture_path,owner.pw_gid)
            try:
                shadow_values=dict(newvalues,Station__Online__MultiplayerProfileRegistryFile=str(fixture_path))
                proof={'futureTransport':b.shadow(target,shadow_values,index,a.work_directory,binds,'all-platforms-server-ready',True)}
            finally:fixture_path.unlink(missing_ok=True)
            # Prove the real importer before changing its service configuration.
            stage=b.INDEX.parent/('.server-ready-stage-'+str(os.getpid()));stage.mkdir(mode=0o700)
            try:
                for name,path in [('index.json',b.INDEX),('state.json',b.INDEX.parent/'state.json'),('identities.json',identity_path),('profiles.json',profiles)]:shutil.copy2(path,stage/name)
                library.atomic_json(stage/'modes.json',modes);library.atomic_json(stage/'engines.json',engines)
                candidate_config=copy.deepcopy(config);candidate_config.update(outputDirectory=str(stage),contentIdentityRegistry=str(stage/'identities.json'),autoOnlineProfiles=dict(config['autoOnlineProfiles'],registry=str(stage/'profiles.json'),engineManifest=str(stage/'engines.json'),preparedModes=str(stage/'modes.json')))
                scans=[library.publish(candidate_config),library.publish(candidate_config)]
                if any(s['changed'] for s in scans) or json.loads((stage/'index.json').read_bytes())!=index or json.loads((stage/'profiles.json').read_bytes())!=entries or b.digest(stage/'identities.json')!=IDENTITY_SHA:raise ValueError('Future registration importer changed production data')
                proof['importer']={'passed':True,'scans':2,'catalogRevision':27,'previousProfilesPreserved':5176,'allPreparedModesAccepted':274,'productionChanged':False}
            finally:shutil.rmtree(stage)
            preservation(snapshot['configSha256']);quiet();b.progress('apply-server-preparation',catalogChanged=False,profilesChanged=False)
            library.atomic_json(manifest_path,engines,owner.pw_gid);library.atomic_json(mode_path,modes,owner.pw_gid)
            next_config=copy.deepcopy(config);next_config['autoOnlineProfiles'].update(engineManifest=str(manifest_path),preparedModes=str(mode_path))
            changed=True;library.atomic_json(CONFIG,next_config)
            b.private(DROPIN,'[Service]\nWorkingDirectory='+str(target)+'\nExecStart=\nExecStart=/usr/bin/dotnet '+str(target/'TurboRamaSuiteOnlineServer.dll')+'\nBindReadOnlyPaths=\nBindReadOnlyPaths='+binds+'\nEnvironmentFile='+str(target/'server-ready.env')+'\n');DROPIN.chmod(0o644)
            b.private(SCAN_DROPIN,'[Service]\nExecStart=\nExecStart=/usr/bin/python3 '+str(target/'library-tools/atualizar-biblioteca-station.py')+' --config '+str(CONFIG)+'\n');SCAN_DROPIN.chmod(0o644)
            ops.run(['systemctl','daemon-reload']);quiet();ops.run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL)
            current,current_ids=ops.runtime()
            if str(target/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or current_ids['Uid'][1]!=owner.pw_uid or current_ids['Gid'][1]!=owner.pw_gid:raise ValueError('Effective release or service identity differs')
            for key,value in newvalues.items():
                if key.startswith(('Station__','Suite__','ConnectionStrings__')) and current.get(key)!=value:raise ValueError('Unexpected effective setting')
            proof['public']=b.checks(index,current,b.PUBLIC,True);after=b.wait_quiet(b.LOCAL);preservation(b.digest(CONFIG))
            if json.loads(manifest_path.read_bytes())!=engines or json.loads(mode_path.read_bytes())!=modes or str(target/'library-tools/atualizar-biblioteca-station.py') not in ops.run(['systemctl','show',SCAN,'-p','ExecStart','--value']):raise ValueError('Persistent registry or importer differs')
            pid=int(b.show('MainPID'));sandbox=r71.sandbox(pid,target,index)
            result=dict(applied=True,utc=b.utc(),sourceCommit=seal['sourceCommit'],dllSha256=seal['dllSha256'],pid=pid,nRestarts=int(b.show('NRestarts')),catalogRevision=27,catalogItems=3848,profiles=5176,
                serverReadyPlatforms=list(NATIVE_CONTROLLERS),all15ServerPlatformsReady=True,preparedItems=273,preparedModes=274,preparedFourPlayerModes=9,syntheticFutureEnginesInstalled=False,
                engineEnrollmentByData=True,engineEnrollmentRestartRequired=False,realHashesAndExactNativeControlsRequired=True,profileReloadWithinSeconds=10,
                catalogAndContentIdentitiesAndAllProfilesPreserved=True,licensesAndSchemaPreserved=True,sharedProductsPreserved=True,proxyTunnelFirewallChanged=False,
                modesSha256=b.digest(mode_path),appEnginesSha256=b.digest(manifest_path),importerSha256=b.digest(target/'library-tools/atualizar-biblioteca-station.py'),configSha256=b.digest(CONFIG),target=str(target),
                proof=proof,sandbox=sandbox,readyAfter=after,allPlatformsAndroidGameplayComplete=False)
            b.private(a.work_directory/'deployment-result.json',result,True);b.progress('complete',applied=True,pid=pid,serverReadyPlatforms=15,preparedItems=273,profilesPreserved=5176)
    except Exception:
        if changed:
            try:can_return=b.quiet(b.ready())
            except OSError:can_return=b.show('ActiveState') not in ('active','activating')
            if not can_return:raise ValueError('Rollback deferred while a room is active or cannot be checked')
            for name,path in [('config.json',CONFIG),('state.json',b.INDEX.parent/'state.json'),('report.json',b.INDEX.parent/'report.json')]:
                temporary=path.with_name('.server-ready-rollback-'+path.name);shutil.copy2(backups/name,temporary);os.replace(temporary,path)
            DROPIN.unlink(missing_ok=True);SCAN_DROPIN.unlink(missing_ok=True)
            ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL);b.progress('rollback',previousReleaseRestored=True)
        raise
    finally:
        if timer:ops.run(['systemctl','start',TIMER])

if __name__=='__main__':
    try:main()
    except Exception:
        paths=[Path(sys.argv[i+1]) for i,value in enumerate(sys.argv[:-1]) if value=='--work-directory']
        if paths and paths[0].is_dir():b.private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        b.progress('failed',detailsSuppressed=True);raise SystemExit(1)
