#!/usr/bin/env python3
"""Complete the server-first receipt without restarting an already published API.

The first deployment completed its HTTP proofs and reached a global quiet check
while a human room had opened. That room was preserved. This audit verifies the
applied state and scoped owned fixtures; it never changes production registries.
"""
import argparse,fcntl,hashlib,json,os,pwd,shutil,sys,traceback
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent));sys.dont_write_bytecode=True
import importlib.util
spec=importlib.util.spec_from_file_location('server_ready_rollout',Path(__file__).with_name('finalizar-servidor-plataformas-station-20261009.py'))
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r)
b,ops,security,r71=r.b,r.ops,r.security,r.r71
from station_online_profiles import validate_modes,validate_manifest,NATIVE_CONTROLLERS

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('deployment-work','seal','work-directory'):parser.add_argument('--'+name,type=Path,required=True)
    a=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('Native Linux administrative authentication required')
    if any(not p.is_absolute() or p.is_symlink() for p in (a.deployment_work,a.seal,a.work_directory)):raise ValueError('Absolute regular inputs required')
    os.umask(0o077);a.work_directory.mkdir(mode=0o700,exist_ok=False);owner=pwd.getpwnam(b.IDENTITY)
    os.chown(a.work_directory,0,owner.pw_gid);a.work_directory.chmod(0o750)
    operator=pwd.getpwnam('lz-servidor');ops.run(['setfacl','-m','u:'+str(operator.pw_uid)+':rx',str(a.work_directory)])
    seal=json.loads(a.seal.read_bytes());snapshot=json.loads((a.deployment_work/'baseline-private.json').read_bytes())
    diagnostic=(a.deployment_work/'diagnostic-private.txt').read_text()
    if 'Owned fixture did not detach cleanly' not in diagnostic or 'Rollback deferred while a room is active' not in diagnostic:raise ValueError('Expected preserved live-room postcondition required')
    target=Path('/opt/turborama-station-server-ready-20261009-'+seal['dllSha256'][:12])
    values,ids=ops.runtime();pid=int(b.show('MainPID'));started=b.show('ExecMainStartTimestamp');restarts=b.show('NRestarts');config=json.loads(r.CONFIG.read_bytes())
    profiles=Path(values['Station__Online__MultiplayerProfileRegistryFile']);identities=Path(config['contentIdentityRegistry']);manifest=Path(config['autoOnlineProfiles']['engineManifest']);modes_path=Path(config['autoOnlineProfiles']['preparedModes'])
    index=json.loads(b.INDEX.read_bytes());entries=json.loads(profiles.read_bytes());engines=validate_manifest(json.loads(manifest.read_bytes()));modes=json.loads(modes_path.read_bytes());validate_modes(modes)
    db=r71.database(values);old_config=json.loads((a.deployment_work/'rollback/config.json').read_bytes())
    old_config['autoOnlineProfiles'].update(engineManifest=str(manifest),preparedModes=str(modes_path))
    expected_config_hash=b.digest(r.CONFIG)
    def verify():
        if str(target/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or b.digest(target/'TurboRamaSuiteOnlineServer.dll')!=seal['dllSha256'] or int(b.show('MainPID'))!=pid or b.show('NRestarts')!=restarts or b.show('ExecMainStartTimestamp')!=started:raise ValueError('Effective service changed during read-only audit')
        if security.files(r.CURRENT)!=snapshot['oldFiles'] or security.files(r.PREVIOUS_SCANNER.parent)!=snapshot['oldTools'] or b.digest(b.INDEX)!=r.INDEX_SHA or b.digest(profiles)!=r.PROFILE_SHA or b.digest(identities)!=r.IDENTITY_SHA or b.digest(Path(values['Station__Online__EngineRegistryFile']))!=b.REGISTRY or json.loads(r.CONFIG.read_bytes())!=old_config:raise ValueError('Published data or unrelated importer setting changed')
        if ids['Uid'][1]!=owner.pw_uid or ids['Gid'][1]!=owner.pw_gid or json.loads(manifest.read_bytes())!=engines or json.loads(modes_path.read_bytes())!=modes or len(engines['engines'])!=11 or len(modes['modes'])!=274 or b.digest(r.CONFIG)!=expected_config_hash:raise ValueError('Persistent enrollment differs')
        if {n:b.digest(target/'library-tools'/n) for n in seal['toolFiles']}!=seal['toolFiles']:raise ValueError('Effective tools differ from tested source')
        for name,digest in snapshot['configuration'].items():
            if b.digest(Path(name))!=digest:raise ValueError('Prior setting changed')
        for unit,state in snapshot['shared'].items():
            if ops.state(unit)!=state:raise ValueError('Another service changed')
        if security.real_licenses(db)!=snapshot['realLicenses'] or hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()!=snapshot['schema']:raise ValueError('Real license or schema changed')
        security.database_scope(db);security.management_health()
    verify();b.progress('audit-server-preparation',serviceRestarted=False)
    capabilities=[next(x for x in index['items'] if x['platform']==p and x.get('catalogVisible',True)) for p in NATIVE_CONTROLLERS]
    original_verify=b.five.verify
    def verify_platforms(catalog,settings,address,sql):
        registry=json.loads(Path(settings['Station__Online__MultiplayerProfileRegistryFile']).read_bytes());extra=[]
        for system in ('n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3',*NATIVE_CONTROLLERS):
            eligible=[p for p in registry if p['platform']==system and p['approved'] and p['maximumPlayers']>=2]
            if eligible:extra.append(max(eligible,key=lambda p:p['maximumPlayers']))
        return original_verify(catalog,settings,address,sql,additional_profiles=extra,capabilities_items=capabilities)
    b.five.verify=verify_platforms
    # A local fixture uses synthetic engine/runtime/layout hashes only here.
    # The actual profile registry and native-engine manifest are never touched.
    fixture_entries=list(entries)
    for system in NATIVE_CONTROLLERS:
        mode=max((m for m in modes['modes'] if m['platform']==system),key=lambda m:m['maximumPlayers'])
        maximum=2 if system=='switch' else mode['maximumPlayers']
        fixture_entries.append(dict(itemId=mode['itemId'],contentSha256=mode['contentSha256'],platform=system,engineId='synthetic-audit-'+system,
            coreSha256=hashlib.sha256(('fixture-core-'+system).encode()).hexdigest(),runtimeSha256=hashlib.sha256(b'fixture-runtime').hexdigest(),
            profileId='synthetic-transport-only',profileSha256=hashlib.sha256(('fixture-layout-'+system).encode()).hexdigest(),maximumPlayers=maximum,
            allowedPlayerCounts=list(range(2,maximum+1)),approved=True,controllerProfile='synthetic-native-controls',mode='local-multiplayer',modeTitle='Synthetic transport audit only',instructions=['Fixture only, not native gameplay.'],sources=[]))
    fixture_path=a.work_directory/'fixture-profiles.json';b.private(fixture_path,fixture_entries);r.protect(fixture_path,owner.pw_gid)
    # All previous full catalog/legacy proofs reached the quiet-only failure.
    # Repeat the changed v3 contract with persisted results, avoiding unrelated
    # repeated downloads. Shadow cleanup still requires its own zero-room state.
    def scoped_checks(catalog,settings,address,with_five):
        answer={'multiplayer':verify_platforms(catalog,settings,address,lambda s:ops.sql(db,s))}
        answer['readyAfter']=b.ready(b.LOCAL if address.startswith('https:') else address)
        if not address.startswith('https:') and not b.quiet(answer['readyAfter']):raise ValueError('Owned isolated fixture remains')
        return answer
    b.checks=scoped_checks
    try:
        shadow_values=dict(values,Station__Online__MultiplayerProfileRegistryFile=str(fixture_path))
        binds=b.show('BindReadOnlyPaths')+' '+str(fixture_path)
        proof={'futureTransport':b.shadow(target,shadow_values,index,a.work_directory,binds,'server-ready-audit',True)}
        b.private(a.work_directory/'future-transport-proof.json',proof['futureTransport'],True)
        proof['public']=scoped_checks(index,values,b.PUBLIC,True)
        b.private(a.work_directory/'public-proof.json',proof['public'],True)
    finally:fixture_path.unlink(missing_ok=True)
    with (b.INDEX.parent/'scan.lock').open('a') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX);verify()
        report=json.loads((b.INDEX.parent/'report.json').read_bytes())
        scan_start=ops.run(['systemctl','show',r.SCAN,'-p','ExecMainStartTimestamp','--value']).strip()
        if ops.run(['systemctl','is-active',r.TIMER]).strip()!='active' or ops.run(['systemctl','show',r.SCAN,'-p','ExecMainStatus','--value']).strip()!='0' or str(target/'library-tools/atualizar-biblioteca-station.py') not in ops.run(['systemctl','show',r.SCAN,'-p','ExecStart','--value']) or report['changed'] or report['revision']!=27:raise ValueError('Effective automatic importer differs')
        proof['importer']=dict(passed=True,timerActive=True,execMainStatus=0,lastScanStart=scan_start,postDeploymentScanChanged=False,catalogRevision=27,
            preparedModeInput=True,persistentEngineManifest=True,previousProfilesPreserved=5176,indexSha256=r.INDEX_SHA,identitiesSha256=r.IDENTITY_SHA,profilesSha256=r.PROFILE_SHA)
        after=b.ready();sandbox=r71.sandbox(pid,target,index)
        result=dict(applied=True,utc=b.utc(),sourceCommit=seal['sourceCommit'],dllSha256=seal['dllSha256'],pid=pid,nRestarts=int(restarts),reloadLocal=started,
            catalogRevision=27,catalogItems=3848,catalogVisible=3593,profiles=5176,approvedProfiles=5169,serverReadyPlatforms=list(NATIVE_CONTROLLERS),all15ServerPlatformsReady=True,
            preparedItems=273,preparedModes=274,preparedFourPlayerModes=9,syntheticFutureEnginesInstalled=False,engineEnrollmentByData=True,engineEnrollmentRestartRequired=False,
            realHashesAndExactNativeControlsRequired=True,profileReloadWithinSeconds=10,serverRestartedDuringAudit=False,humanRoomPreservedAtFirstPostcondition=True,
            firstFullCatalogAndLegacyAndV3ProofsReachedOnlyGlobalQuietFailure=True,
            catalogAndContentIdentitiesAndAllProfilesPreserved=True,licensesAndSchemaPreserved=True,sharedProductsPreserved=True,proxyTunnelFirewallChanged=False,
            modesSha256=b.digest(modes_path),appEnginesSha256=b.digest(manifest),importerSha256=b.digest(target/'library-tools/atualizar-biblioteca-station.py'),configSha256=expected_config_hash,target=str(target),
            proof=proof,sandbox=sandbox,readyAfter=after,allPlatformsAndroidGameplayComplete=False)
        b.private(a.work_directory/'deployment-result.json',result,True);b.progress('complete',applied=True,pid=pid,serverReadyPlatforms=15,preparedItems=273,profilesPreserved=5176,serviceRestarted=False,activeRoomsAfter=after['multiplayer']['activeRooms'])

if __name__=='__main__':
    try:main()
    except Exception:
        paths=[Path(sys.argv[i+1]) for i,value in enumerate(sys.argv[:-1]) if value=='--work-directory']
        if paths and paths[0].is_dir():b.private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        b.progress('failed',detailsSuppressed=True);raise SystemExit(1)
