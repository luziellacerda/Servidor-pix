#!/usr/bin/env python3
"""Roll out the bounded five-player Station release with shadow proof and rollback.

Only Station binaries, its profile registry and one service drop-in change.
Real licenses, schema, catalog, signing keys, importer, proxy and shared products
are checked for preservation. No connected/recovering game can be discarded.
"""
import argparse
from datetime import datetime, timezone
import fcntl, hashlib, importlib.util, json, os
from pathlib import Path
import pwd, shlex, shutil, socket, subprocess, sys, time, traceback
from urllib.request import urlopen

SCRIPTS=Path(__file__).resolve().parent
sys.path.insert(0,str(SCRIPTS))
sys.dont_write_bytecode=True

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),SCRIPTS/name)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

base=load('implantar-base-station-r81-20261008.py')
ops,security,r71,legacy=base.ops,base.security,base.r71,base.legacy
catalog=load('verificar-catalogo-identidades-r81.py')
five=load('verificar-online-station-five-players.py')
v2=load('verificar-registro-station-r74.py')
from station_async_socket_check import SocketChecks

SERVICE='turborama-station-api.service'
IDENTITY='turborama-station-api'
OLD=Path('/opt/turborama-station-online-r81-20261008-db50a98')
OLD_DLL='cdf14b8067de415413c503de787c6d621c6e8f0466eb2cdd7c0eced8b5a619af'
OLD_PROFILES='f3eb13fcb474edb5a1b549a3509aa47505765210b38d1f1dd81bc157b4ec555c'
REGISTRY='a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843'
INDEX=Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
DROPIN=Path('/etc/systemd/system')/(SERVICE+'.d')/('z'*32+'-station-five-players-20261009.conf')
LOCAL='http://127.0.0.1:5192'
PUBLIC='https://app.lzgames.com.br'

def utc():return datetime.now(timezone.utc).isoformat()
def digest(path):return ops.digest(Path(path))
def show(name):return ops.run(['systemctl','show',SERVICE,'-p',name,'--value']).strip()
def ready(address=LOCAL):
    with urlopen(address+'/ready/station/online',timeout=10) as response:return json.load(response)
def quiet(value,closed=False):
    legacy_quiet=value['activeRooms']==value['activeConnections']==0
    recovery=value.get('recovery',{});mp=value.get('multiplayer',{})
    return legacy_quiet and all(recovery.get(k,0)==0 for k in ('retainedRooms','activeConnections','pendingBytes')) and all(mp.get(k,0)==0 for k in ('activeConnections','retainedBytes')) and (mp.get('activeRooms',0)==0 or closed and mp.get('activeRooms')==1)
def wait_quiet(address):
    for _ in range(50):
        value=ready(address)
        if quiet(value):return value
        time.sleep(.1)
    raise ValueError('Owned fixture did not detach cleanly')
def private(path,data,readable=False):
    ops.private_text(path,json.dumps(data,indent=2)+'\n' if not isinstance(data,str) else data)
    if readable:
        owner=pwd.getpwnam('lz-servidor');os.chown(path,owner.pw_uid,owner.pw_gid)
def progress(stage,**fields):print(json.dumps(dict(stage=stage,**fields)),flush=True)
def checks(index,values,address,with_five):
    db=r71.database(values)
    result={'catalog':catalog.verify(index,values,address,lambda s:ops.sql(db,s),True,'enabled')}
    engines=json.loads(Path(values['Station__Online__EngineRegistryFile']).read_text())
    additions=json.loads(legacy.ADDITIONS.read_text())
    adapter=SocketChecks();v2.connect=adapter.connect
    try:
        result['legacy']=v2.verify(index,values,address,lambda s:ops.sql(db,s),additions,[e['id'] for e in engines if e['id'] not in legacy.NEW_IDS])
    finally:adapter.close()
    if with_five:result['multiplayer']=five.verify(index,values,address,lambda s:ops.sql(db,s))
    else:result['multiplayer']=load('verificar-online-station-r81.py').verify(index,values,address,lambda s:ops.sql(db,s))
    result['quietAfter']=wait_quiet(LOCAL if address.startswith('https:') else address)
    return result
def shadow(target,values,index,work,binds,label,with_five):
    env=work/(label+'.env');log=work/(label+'.log')
    # Only explicit application settings are copied; no invocation/authentication GUI environment.
    explicit={k:v for k,v in values.items() if k.startswith(('Station__','Suite__','ConnectionStrings__','ASPNETCORE_','DOTNET_'))}
    private(env,'\n'.join(k+'='+shlex.quote(v) for k,v in sorted(explicit.items()))+'\n')
    private(log,'')
    with socket.socket() as probe:probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
    address='http://127.0.0.1:'+str(port)
    unit='station-five-'+label+'-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+'.service'
    command=['systemd-run','--quiet','--collect','--unit',unit]
    # Reuse every effective sandbox setting; only release bindings differ.
    names=['User','Group','SupplementaryGroups','NoNewPrivileges','ProtectSystem','ProtectHome','PrivateTmp','PrivateDevices',
        'ProtectKernelTunables','ProtectKernelModules','ProtectKernelLogs','ProtectControlGroups','ProtectClock','ProtectHostname',
        'ProtectProc','RestrictSUIDSGID','RestrictNamespaces','RestrictRealtime','LockPersonality','RemoveIPC','UMask',
        'CapabilityBoundingSet','AmbientCapabilities','RestrictAddressFamilies','TemporaryFileSystem','InaccessiblePaths','LimitCORE','CoredumpFilter']
    for name in names:command+=['--property',name+'='+show(name)]
    command+=['--property','BindReadOnlyPaths='+binds,'--property','EnvironmentFile='+str(env),
        '--property','WorkingDirectory='+str(target),'--property','StandardOutput=append:'+str(log),
        '--property','StandardError=append:'+str(log),'/usr/bin/dotnet',str(target/'TurboRamaSuiteOnlineServer.dll'),'--urls',address]
    progress(label,productionChanged=False)
    try:
        ops.run(command);r71.ready_recovery(address)
        return checks(index,values,address,with_five)
    finally:
        stopped=subprocess.run(['systemctl','stop',unit],check=False,capture_output=True,timeout=30)
        env.unlink(missing_ok=True)
        if stopped.returncode not in (0,5):raise ValueError('Owned shadow unit cleanup failed')
def verify_preservation(state):
    if digest(INDEX)!=state['indexSha256'] or security.files(OLD)!=state['oldFiles']:
        raise ValueError('Original release or catalog changed')
    for unit,old in state['shared'].items():
        if ops.state(unit)!=old:raise ValueError('Shared service changed')
    for path,old in state['configuration'].items():
        if digest(path)!=old:raise ValueError('Existing service configuration changed')
    if security.real_licenses(state['database'])!=state['realLicenseSha256']:
        raise ValueError('Real license state changed')
    ledger=hashlib.sha256(security.scalar(state['database'],'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()
    if ledger!=state['ledgerSha256']:raise ValueError('Schema ledger changed')
    security.database_scope(state['database']);security.management_health()
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('release-directory','seal','profiles','work-directory'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--closed-empty-room-confirmed',action='store_true')
    args=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('Native Linux administrative authentication required')
    os.umask(0o077)
    for path in (args.release_directory,args.seal,args.profiles,args.work_directory):
        if not path.is_absolute() or path.is_symlink():raise ValueError('Absolute regular owned inputs required')
    args.work_directory.mkdir(mode=0o700,exist_ok=False)
    seal=json.loads(args.seal.read_text());target=Path('/opt/turborama-station-five-players-20261009-'+seal['dllSha256'][:12])
    if DROPIN.exists():raise ValueError('Release already exists; do not repeat a successful rollout')
    lock=INDEX.parent/'scan.lock'
    with lock.open('a') as held:
        fcntl.flock(held,fcntl.LOCK_EX)
        values,ids=ops.runtime();owner=pwd.getpwnam(IDENTITY)
        if str(OLD/'TurboRamaSuiteOnlineServer.dll') not in show('ExecStart') or digest(OLD/'TurboRamaSuiteOnlineServer.dll')!=OLD_DLL:
            raise ValueError('Effective Station release was superseded')
        if ids['Uid'][1]!=owner.pw_uid or ids['Gid'][1]!=owner.pw_gid:raise ValueError('Station identity changed')
        original=Path(values['Station__Online__MultiplayerProfileRegistryFile'])
        registry=Path(values['Station__Online__EngineRegistryFile'])
        if digest(original)!=OLD_PROFILES or digest(registry)!=REGISTRY:raise ValueError('Reviewed registries were superseded')
        if values['Station__Online__MultiplayerEnabled'].lower()!='true' or values['Station__Online__MultiplayerLegacyCapacityGate'].lower()!='false':raise ValueError('Online policy changed')
        profiles=json.loads(args.profiles.read_text());active=json.loads(original.read_text());index=json.loads(INDEX.read_text())
        if profiles[:len(active)]!=active or len(active)!=1816 or len(profiles)!=3666:raise ValueError('Existing profiles must be preserved exactly')
        if security.files(args.release_directory)!=seal['files'] or digest(args.profiles)!=seal['profilesSha256']:raise ValueError('Sealed inputs changed')
        db=r71.database(values)
        state=dict(utc=utc(),indexSha256=digest(INDEX),oldFiles=security.files(OLD),configuration=security.snapshot_configs(),
            database=db,realLicenseSha256=security.real_licenses(db),ledgerSha256=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),
            shared={unit:ops.state(unit) for unit in security.SHARED},beforePid=show('MainPID'),beforeReady=ready())
        if not quiet(state['beforeReady'],args.closed_empty_room_confirmed):raise ValueError('Active game: rollout deferred')
        private(args.work_directory/'baseline-private.json',state)
        backup=args.work_directory/'rollback';backup.mkdir(mode=0o700)
        for n,path in enumerate(state['configuration']):shutil.copy2(path,backup/(str(n)+'.conf'))
        private(backup/'configuration-manifest.json',state['configuration'])
        newvalues=dict(values);newvalues.update(Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'),Station__Online__MultiplayerProfileRegistryFile=str(target/'online-profiles-authorized.json'))
        envtext='\n'.join(k+'='+v for k,v in newvalues.items() if k in ('Station__Online__EngineRegistryFile','Station__Online__MultiplayerProfileRegistryFile'))+'\n'
        if target.exists():
            # An unpublished root-owned candidate from a rolled-back attempt may
            # be reused only when every byte still equals this exact input seal.
            expected=dict(seal['files']);expected.update({'online-engine-registry.json':REGISTRY,
                'online-profiles-authorized.json':seal['profilesSha256'],'five.env':hashlib.sha256(envtext.encode()).hexdigest()})
            if target.is_symlink() or target.stat().st_uid!=0 or security.files(target)!=expected:
                raise ValueError('Existing candidate differs from the exact reviewed seal')
        else:
            shutil.copytree(args.release_directory,target)
            shutil.copyfile(registry,target/'online-engine-registry.json')
            shutil.copyfile(args.profiles,target/'online-profiles-authorized.json')
            for p in target.rglob('*'):
                if p.is_file():os.chown(p,0,owner.pw_gid);p.chmod(0o640)
                else:os.chown(p,0,owner.pw_gid);p.chmod(0o750)
            os.chown(target,0,owner.pw_gid);target.chmod(0o750)
            private(target/'five.env',envtext)
        oldbinds=show('BindReadOnlyPaths');binds=oldbinds.replace(str(OLD),str(target))
        proof={}
        proof['rollbackOld']=shadow(OLD,values,index,args.work_directory,oldbinds,'rollback-old',False)
        verify_preservation(state)
        proof['candidate']=shadow(target,newvalues,index,args.work_directory,binds,'candidate',True)
        private(args.work_directory/'qualification.json',proof,True)
        verify_preservation(state)
        before=ready()
        if not quiet(before,args.closed_empty_room_confirmed):raise ValueError('A room connected during qualification: rollout deferred')
        content='[Service]\nWorkingDirectory='+str(target)+'\nExecStart=\nExecStart=/usr/bin/dotnet '+str(target/'TurboRamaSuiteOnlineServer.dll')+'\nBindReadOnlyPaths=\nBindReadOnlyPaths='+binds+'\nEnvironmentFile='+str(target/'five.env')+'\n'
        changed=False
        try:
            progress('apply',maximumPlayers=5,profiles=len(profiles))
            private(DROPIN,content);DROPIN.chmod(0o644);changed=True
            ops.run(['systemctl','daemon-reload'])
            if not quiet(ready(),args.closed_empty_room_confirmed):raise ValueError('A room connected before restart')
            ops.run(['systemctl','restart',SERVICE]);reloaded=utc();r71.ready_recovery(LOCAL)
            current,current_ids=ops.runtime()
            for k,v in newvalues.items():
                if k.startswith(('Station__','Suite__','ConnectionStrings__')) and current.get(k)!=v:raise ValueError('Unexpected effective application setting')
            if str(target/'TurboRamaSuiteOnlineServer.dll') not in show('ExecStart') or current_ids['Uid'][1]!=owner.pw_uid:raise ValueError('Unexpected running identity/release')
            proof['public']=checks(index,current,PUBLIC,True)
            after=wait_quiet(LOCAL);verify_preservation(state)
            pid=int(show('MainPID'));sandbox=r71.sandbox(pid,target,index)
            result=dict(applied=True,utc=utc(),reloadUtc=reloaded,pid=pid,sourceCommit=seal['sourceCommit'],dllSha256=seal['dllSha256'],
                maximumPlayers=5,approvedProfiles=len(profiles),fivePlayerProfiles=2,catalogRevision=index['revision'],catalogItems=len(index['items']),
                indexSha256=state['indexSha256'],profilesSha256=digest(target/'online-profiles-authorized.json'),legacyEngineRegistrySha256=digest(target/'online-engine-registry.json'),
                legacyEnginesPreserved=10,previousProfilesPreserved=1816,physicalFivePhoneGameplayQualified=False,
                proof=proof,sandbox=sandbox,readyAfter=after,readyBefore=before,humanConfirmedClosedEmptyRoom=args.closed_empty_room_confirmed,
                coordinatedEmptyLobbyDiscarded=before['multiplayer']['activeRooms']==1,
                realLicensesPreserved=True,schemaPreserved=True,catalogAndCoversPreserved=True,sharedProductsPreserved=True,
                proxyTunnelFirewallChanged=False,rollbackDirectory=str(backup),target=str(target),dropinSha256=digest(DROPIN))
            private(args.work_directory/'deployment-result.json',result,True)
            progress('complete',applied=True,pid=pid,maximumPlayers=5,profiles=len(profiles),catalogRevision=index['revision'])
        except Exception:
            if changed:
                if not quiet(ready()):raise ValueError('Rollback deferred: another room is active')
                DROPIN.unlink(missing_ok=True);ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);r71.ready_recovery(LOCAL)
                progress('rollback',oldReleaseRestored=True)
            raise

if __name__=='__main__':
    try:main()
    except Exception:
        # Error details stay local and private; never print a DB connection, key or proof.
        paths=[Path(sys.argv[i+1]) for i,v in enumerate(sys.argv[:-1]) if v=='--work-directory']
        if paths and paths[0].is_dir():private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        progress('failed',detailsSuppressed=True)
        raise SystemExit(1)
