#!/usr/bin/python3
"""Publish the exact Station platform release, persistent identities and profiles.

Native Linux authentication, quiet-only replacement, isolated/public proofs,
preservation of customer licenses and other services, and automatic rollback.
This operator never changes a ROM, cover, password, proxy or firewall.
"""
import argparse, copy, fcntl, hashlib, json, os, pwd, shutil, subprocess, sys, time, traceback
from pathlib import Path
sys.dont_write_bytecode=True
SCRIPTS=Path(__file__).resolve().parent;sys.path.insert(0,str(SCRIPTS))
import importlib.util
def load(path,name):
    spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
b=load(SCRIPTS/'implantar-five-players-station-20261009.py','platform_rollout_base')
ops,security,r71=b.ops,b.security,b.r71
library=load(SCRIPTS/'atualizar-biblioteca-station.py','platform_library')
binder=load(SCRIPTS.parent/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py','platform_binder')
from station_content_sets import bind_set

CURRENT=Path('/opt/turborama-station-five-players-20261009-ad45a4f0f4c7')
DLL_SHA='ad45a4f0f4c7aef3dd4191b453da384be008e2b06aaaeb8b1ca359f3cb83570a'
PROFILES_SHA='244d98e76c4b5cc00f1af75abebe595688d1700cef6d2f8bc777dc84c3b388ad'
INDEX_SHA='6b8acfa4ca419ec705f48f53e2063633ff8f0a30a36cb8f4d651a108cbb31137'
SCANNER=Path('/opt/turborama-station-library-new-systems-20261008-12f0475e90/atualizar-biblioteca-station.py')
SCANNER_SHA='4ac58964a9683d41e5a35275a20dbd4c40a666b87469de3253c74def37a3f4fe'
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
SCAN='turborama-station-library-scan.service';TIMER='turborama-station-library-scan.timer'
API_DROPIN=Path('/etc/systemd/system')/(b.SERVICE+'.d')/('z'*36+'-station-platforms-20261009.conf')
SCAN_DROPIN=Path('/etc/systemd/system')/(SCAN+'.d')/('z'*36+'-station-platforms-20261009.conf')

def no_sessions():
    if not b.quiet(b.ready()):raise ValueError('An active or recovering room prevents replacement')
def sha(path):return b.digest(path)
def regular(path):
    if not path.is_absolute() or path.is_symlink() or not path.is_file():raise ValueError('Absolute regular input required')
def private(path,data,readable=False):b.private(path,data,readable)
def protect(path,group):
    os.chown(path,0,group);path.chmod(0o750 if path.is_dir() else 0o640)
def run(command):return ops.run(list(map(str,command)))
def check_row_preservation(old,new):
    if len(new['items'])!=len(old['items']) or len(old['items'])!=3848:raise ValueError('Catalog IDs/count changed')
    def rest(row):return {k:v for k,v in row.items() if k not in ('contentSha256','contentIdentityScheme')}
    for a,z in zip(old['items'],new['items']):
        if rest(a)!=rest(z) or a.get('contentSha256') and a['contentSha256']!=z.get('contentSha256'):raise ValueError('A published catalog field changed')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('seal','release','candidate-index','identities','modes','engines','work-directory'):parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('Native Linux administrative authentication required')
    os.umask(0o077)
    for file in (args.seal,args.candidate_index,args.identities,args.modes,args.engines):regular(file)
    if not args.release.is_absolute() or args.release.is_symlink() or not args.release.is_dir() or not args.work_directory.is_absolute() or args.work_directory.is_symlink():raise ValueError('Absolute fresh workspace required')
    args.work_directory.mkdir(mode=0o700,exist_ok=False)
    operator=pwd.getpwnam('lz-servidor');run(['setfacl','-m','u:'+str(operator.pw_uid)+':rx',args.work_directory])
    seal=json.loads(args.seal.read_bytes())
    if security.files(args.release)!=seal['releaseFiles'] or sha(args.release/'TurboRamaSuiteOnlineServer.dll')!=seal['dllSha256']:raise ValueError('Compiled release seal changed')
    for name,path in [('candidateIndex',args.candidate_index),('identities',args.identities),('modes',args.modes),('engines',args.engines)]:
        if sha(path)!=seal[name+'Sha256']:raise ValueError('Prepared data seal changed')
    tools={name:SCRIPTS/name for name in ('atualizar-biblioteca-station.py','station_packages.py','station_disc.py','station_revista.py','preparar-indice-artefatos.py','station_content_sets.py','station_online_profiles.py')}
    tools['prepare_content_identity_registry.py']=SCRIPTS.parent/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py'
    if {name:sha(path) for name,path in tools.items()}!=seal['libraryFiles']:raise ValueError('Importer seal changed')
    if API_DROPIN.exists() or SCAN_DROPIN.exists():raise ValueError('Already applied or superseded; do not repeat')
    target=Path('/opt/turborama-station-online-platforms-20261009-'+seal['dllSha256'][:12])
    if target.exists():raise ValueError('Fresh immutable target required')
    was_timer=subprocess.run(['systemctl','is-active','--quiet',TIMER]).returncode==0
    changed=False;before=None;backups=args.work_directory/'rollback'
    try:
        if was_timer:run(['systemctl','stop',TIMER])
        for _ in range(120):
            state=run(['systemctl','show',SCAN,'-p','ActiveState','--value']).strip()
            if state not in ('active','activating','deactivating'):break
            time.sleep(.25)
        else:raise ValueError('Importer still running; no replacement performed')
        with (b.INDEX.parent/'scan.lock').open('a') as lock:
            fcntl.flock(lock,fcntl.LOCK_EX)
            values,ids=ops.runtime();owner=pwd.getpwnam(b.IDENTITY)
            original_profiles=Path(values['Station__Online__MultiplayerProfileRegistryFile']);registry=Path(values['Station__Online__EngineRegistryFile'])
            if str(CURRENT/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or sha(CURRENT/'TurboRamaSuiteOnlineServer.dll')!=DLL_SHA or sha(original_profiles)!=PROFILES_SHA or sha(registry)!=b.REGISTRY or sha(b.INDEX)!=INDEX_SHA or sha(SCANNER)!=SCANNER_SHA or sha(CONFIG)!=seal['importerConfigSha256']:
                raise ValueError('Reviewed production baseline was superseded')
            if str(SCANNER) not in run(['systemctl','show',SCAN,'-p','ExecStart','--value']):raise ValueError('Effective importer changed')
            if ids['Uid'][1]!=owner.pw_uid or ids['Gid'][1]!=owner.pw_gid or values.get('Station__Online__MultiplayerEnabled','').lower()!='true' or values.get('Station__Online__MultiplayerLegacyCapacityGate','').lower()!='false':raise ValueError('Identity or admission policy changed')
            no_sessions()
            live=json.loads(b.INDEX.read_bytes());candidate=json.loads(args.candidate_index.read_bytes());identities=json.loads(args.identities.read_bytes());modes=json.loads(args.modes.read_bytes());engine_doc=json.loads(args.engines.read_bytes());config=json.loads(CONFIG.read_bytes())
            check_row_preservation(live,candidate)
            if candidate['revision']!=26 or identities['schemaVersion']!=1 or len(identities['entries'])!=3848 or len(modes)!=1504:raise ValueError('Qualified full collection required')
            entries={e['itemId']:e for e in identities['entries']};rows={r['itemId']:r for r in candidate['items']}
            b.progress('qualify-content-sets',productionChanged=False)
            changed_sets=[]
            for row in candidate['items']:
                if row['artifact']['launchPath'].lower().endswith('.cue') or row['platform']=='wiiu':
                    identity=bind_set(row,binder.bind(row,row['filePath']))
                    entries[row['itemId']]=identity;row['contentSha256']=identity['contentSha256'];row['contentIdentityScheme']=identity['contentIdentityScheme'];changed_sets.append(row['itemId'])
            if len(changed_sets)!=4:raise ValueError('Expected three CUE sets and one complete Wii U set')
            candidate['revision']=27;identities=dict(schemaVersion=2,entries=sorted(entries.values(),key=lambda e:e['itemId']))
            for mode in modes:mode['contentSha256']=rows[mode['itemId']]['contentSha256']
            private(args.work_directory/'index-final-private.json',candidate,True);private(args.work_directory/'identities-final.json',identities,True);private(args.work_directory/'modes-final.json',modes,True)
            prepared=args.work_directory/'prepared-profiles'
            run(['/usr/bin/python3',SCRIPTS/'preparar-perfis-online-plataformas.py','--catalog',args.work_directory/'index-final-private.json','--engines',args.engines,'--existing',original_profiles,'--modes',args.work_directory/'modes-final.json','--output',prepared])
            profile_file=prepared/'profiles-candidate.json';profiles=json.loads(profile_file.read_bytes());active=json.loads(original_profiles.read_bytes())
            if len(active)!=3672 or profiles[:3672]!=active or len(profiles)!=5176:raise ValueError('Existing profile preservation failed')
            db=r71.database(values)
            before=dict(utc=b.utc(),oldFiles=security.files(CURRENT),configuration=security.snapshot_configs(),database=db,realLicenseSha256=security.real_licenses(db),ledgerSha256=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),shared={unit:ops.state(unit) for unit in security.SHARED},beforePid=b.show('MainPID'),readyBefore=b.ready())
            private(args.work_directory/'baseline-private.json',before);backups.mkdir(mode=0o700)
            for name,path in [('index.json',b.INDEX),('config.json',CONFIG),('state.json',b.INDEX.parent/'state.json'),('report.json',b.INDEX.parent/'report.json')]:shutil.copy2(path,backups/name)
            for n,path in enumerate(before['configuration']):shutil.copy2(path,backups/(str(n)+'.conf'))
            shutil.copytree(args.release,target);(target/'library-tools').mkdir()
            for name,path in tools.items():shutil.copyfile(path,target/'library-tools'/name)
            shutil.copyfile(registry,target/'online-engine-registry.json');shutil.copyfile(args.engines,target/'online-engines-app.json')
            private(target/'catalog-candidate.json',candidate)
            identity_path=b.INDEX.parent/'content-identities-online-platforms-20261009.json';profile_path=b.INDEX.parent/'online-profiles-platforms-20261009.json'
            if identity_path.exists() or profile_path.exists():raise ValueError('Fresh persistent registries required')
            private(identity_path,identities);shutil.copyfile(profile_file,profile_path)
            protect(identity_path,owner.pw_gid);protect(profile_path,owner.pw_gid)
            newvalues=dict(values);newvalues.update(Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'),Station__Online__MultiplayerProfileRegistryFile=str(profile_path),Station__Online__ReplayMaximumBytes='134217728')
            envtext='\n'.join(k+'='+newvalues[k] for k in ('Station__Online__EngineRegistryFile','Station__Online__MultiplayerProfileRegistryFile','Station__Online__ReplayMaximumBytes'))+'\n'
            private(target/'platforms.env',envtext)
            for path in sorted(target.rglob('*'),reverse=True):protect(path,owner.pw_gid)
            protect(target,owner.pw_gid);(target/'platforms.env').chmod(0o600)
            original_bind=b.show('BindReadOnlyPaths');binds=original_bind.replace(str(CURRENT),str(target))
            def preservation(expected_index,expected_config):
                if sha(b.INDEX)!=expected_index or sha(CONFIG)!=expected_config or security.files(CURRENT)!=before['oldFiles'] or sha(original_profiles)!=PROFILES_SHA or sha(SCANNER)!=SCANNER_SHA:raise ValueError('Original catalog/release/importer preservation failed')
                for path,digest in before['configuration'].items():
                    if sha(path)!=digest:raise ValueError('An existing protected setting changed')
                for unit,state in before['shared'].items():
                    if ops.state(unit)!=state:raise ValueError('Another product changed')
                if security.real_licenses(db)!=before['realLicenseSha256'] or hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()!=before['ledgerSha256']:raise ValueError('Real licenses or schema changed')
                security.database_scope(db);security.management_health()
            original_verify=b.five.verify
            def platform_verify(index,settings,address,sql):
                registry_profiles=json.loads(Path(settings['Station__Online__MultiplayerProfileRegistryFile']).read_bytes());extra=[]
                for system in ('n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3'):
                    choices=[p for p in registry_profiles if p['platform']==system and p['approved'] and p['maximumPlayers']>=2]
                    if choices:extra.append(max(choices,key=lambda p:p['maximumPlayers']))
                return original_verify(index,settings,address,sql,additional_profiles=extra)
            b.five.verify=platform_verify
            proof={'rollbackOld':b.shadow(CURRENT,values,live,args.work_directory,original_bind,'platforms-rollback-old',True)}
            preservation(INDEX_SHA,seal['importerConfigSha256'])
            shadow_values=dict(newvalues,Station__LibraryIndexFile=str(target/'catalog-candidate.json'))
            proof['candidate']=b.shadow(target,shadow_values,candidate,args.work_directory,binds,'platforms-candidate',True)
            # Prove the new importer retains every qualified field on two scans
            # using a private copy of the state and registry outputs.
            stage=b.INDEX.parent/('.online-platforms-stage-'+str(os.getpid()));stage.mkdir(mode=0o700)
            try:
                shutil.copy2(backups/'state.json',stage/'state.json');shutil.copy2(target/'catalog-candidate.json',stage/'index.json')
                shutil.copy2(identity_path,stage/'identities.json');shutil.copy2(profile_path,stage/'profiles.json')
                stage_config=copy.deepcopy(config);stage_config.update(outputDirectory=str(stage),contentIdentityRegistry=str(stage/'identities.json'),autoContentIdentity=True,autoOnlineProfiles=dict(registry=str(stage/'profiles.json'),engineManifest=str(target/'online-engines-app.json'),maintainerAuthorizedTwoSeats=True))
                scans=[library.publish(stage_config),library.publish(stage_config)]
                if any(scan['changed'] for scan in scans) or json.loads((stage/'index.json').read_bytes())!=candidate or json.loads((stage/'profiles.json').read_bytes())!=profiles:raise ValueError('Persistent importer qualification changed prepared data')
                proof['importer']={'passed':True,'scans':len(scans),'revision':candidate['revision'],'identities':3848,'profilesPreserved':len(profiles),'productionChanged':False}
            finally:shutil.rmtree(stage)
            private(args.work_directory/'qualification.json',proof,True);preservation(INDEX_SHA,seal['importerConfigSha256']);no_sessions()
            next_config=copy.deepcopy(config);next_config.update(contentIdentityRegistry=str(identity_path),autoContentIdentity=True,autoOnlineProfiles=dict(registry=str(profile_path),engineManifest=str(target/'online-engines-app.json'),maintainerAuthorizedTwoSeats=True))
            b.progress('apply-platforms',profiles=len(profiles),catalogRevision=candidate['revision'])
            changed=True
            library.atomic_json(b.INDEX,candidate,owner.pw_gid);library.atomic_json(CONFIG,next_config)
            private(API_DROPIN,'[Service]\nWorkingDirectory='+str(target)+'\nExecStart=\nExecStart=/usr/bin/dotnet '+str(target/'TurboRamaSuiteOnlineServer.dll')+'\nBindReadOnlyPaths=\nBindReadOnlyPaths='+binds+'\nEnvironmentFile='+str(target/'platforms.env')+'\n');API_DROPIN.chmod(0o644)
            private(SCAN_DROPIN,'[Service]\nExecStart=\nExecStart=/usr/bin/python3 '+str(target/'library-tools/atualizar-biblioteca-station.py')+' --config '+str(CONFIG)+'\n');SCAN_DROPIN.chmod(0o644)
            run(['systemctl','daemon-reload']);no_sessions();run(['systemctl','restart',b.SERVICE]);reloaded=b.utc();r71.ready_recovery(b.LOCAL)
            current,current_ids=ops.runtime()
            for k,v in newvalues.items():
                if k.startswith(('Station__','Suite__','ConnectionStrings__')) and current.get(k)!=v:raise ValueError('Unexpected effective setting')
            if str(target/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or current_ids['Uid'][1]!=owner.pw_uid or current_ids['Gid'][1]!=owner.pw_gid:raise ValueError('Unexpected running release/identity')
            proof['public']=b.checks(candidate,current,b.PUBLIC,True);after=b.wait_quiet(b.LOCAL)
            final_index_sha=sha(b.INDEX);final_config_sha=sha(CONFIG);preservation(final_index_sha,final_config_sha)
            pid=int(b.show('MainPID'));sandbox=r71.sandbox(pid,target,candidate)
            if sha(identity_path)!=sha(args.work_directory/'identities-final.json'):raise ValueError('Published content registry differs')
            if json.loads(profile_path.read_bytes())!=profiles:raise ValueError('Published profile registry differs')
            result=dict(applied=True,utc=b.utc(),reloadUtc=reloaded,pid=pid,nRestarts=int(b.show('NRestarts')),sourceCommit=seal['sourceCommit'],dllSha256=seal['dllSha256'],
                catalogRevision=27,catalogItems=3848,catalogVisible=sum(r.get('catalogVisible',True) for r in candidate['items']),contentIdentities=3848,multiFileContentSets=changed_sets,
                profiles=len(profiles),approvedProfiles=sum(p['approved'] for p in profiles),previousProfilesPreserved=3672,newProfiles=1504,
                maximumPlayers=5,maximumRooms=100,maximumReplayBytes=134217728,syntheticCapacityParticipants=320,
                activePlatforms=sorted({p['platform'] for p in profiles if p['approved'] and p['maximumPlayers']>=2}),
                nativeClientPlatformsPending=['dreamcast','gamecube','wii','wiiu','switch'],allPlatformsAndroidGameplayComplete=False,
                indexSha256=final_index_sha,identitiesSha256=sha(identity_path),profilesSha256=sha(profile_path),legacyEngineRegistrySha256=sha(target/'online-engine-registry.json'),
                importerSha256=sha(target/'library-tools/atualizar-biblioteca-station.py'),autoContentIdentities=True,autoProfiles=True,profileReloadWithoutRestart=True,
                realLicensesPreserved=True,schemaPreserved=True,romsCoversAndPriorMetadataPreserved=True,sharedProductsPreserved=True,proxyTunnelFirewallChanged=False,
                proof=proof,sandbox=sandbox,readyAfter=after,target=str(target),rollbackDirectory=str(backups))
            private(args.work_directory/'deployment-result.json',result,True);private(args.work_directory/'profiles-final.json',profiles,True)
            b.progress('complete',applied=True,pid=pid,catalogRevision=27,profiles=len(profiles),onlinePlatforms=len(result['activePlatforms']))
    except Exception:
        if changed:
            try:can_return=b.quiet(b.ready())
            except OSError:can_return=b.show('ActiveState') not in ('active','activating')
            if not can_return:raise ValueError('Rollback deferred: a real room is active or cannot be checked')
            for name,path in [('index.json',b.INDEX),('config.json',CONFIG),('state.json',b.INDEX.parent/'state.json'),('report.json',b.INDEX.parent/'report.json')]:
                temporary=path.with_name('.platforms-rollback-'+path.name);shutil.copy2(backups/name,temporary);os.replace(temporary,path)
            API_DROPIN.unlink(missing_ok=True);SCAN_DROPIN.unlink(missing_ok=True);run(['systemctl','daemon-reload']);run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL)
            b.progress('rollback',previousReleaseRestored=True)
        raise
    finally:
        if was_timer:run(['systemctl','start',TIMER])

if __name__=='__main__':
    try:main()
    except Exception:
        paths=[Path(sys.argv[i+1]) for i,v in enumerate(sys.argv[:-1]) if v=='--work-directory']
        if paths and paths[0].is_dir():private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        b.progress('failed',detailsSuppressed=True);raise SystemExit(1)
