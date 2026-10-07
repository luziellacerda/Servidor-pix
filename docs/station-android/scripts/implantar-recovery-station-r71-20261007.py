#!/usr/bin/env python3
"""Station-only recovery rollout; native operator authentication, no schema/key changes."""
import argparse, hashlib, importlib.util, json, os, pwd, re, shutil, socket, subprocess, sys, time, traceback
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlopen

ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(Path(__file__).parent))
from station_hardening_config import systemd_override, sandbox_properties

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

security=load('implantar-seguranca-station-20261007.py');ops=security.ops;online=security.online
SERVICE='turborama-station-api.service';IDENTITY='turborama-station-api';DLL='TurboRamaSuiteOnlineServer.dll'
OLD=Path('/opt/turborama-station-security-20261007-da07355')
OLD_SHA='83c8d2b3da68c85da7402165a7fa915fa7a084125511ec2df5541ec36ecaf682'
ENV=Path('/etc/turborama-station-api-security-20261007/station.env')
CHECK=Path('/mnt/DADOS/station-r71-rollout-check-20261007')
ADDITIONS=ROOT/'docs/station-android/entrega-app-r71-20261007/evidence/server-engine-registry-additions.json'
ADDITIONS_SHA='24385de0a3d3c492a5b9543512b3d535a3fdf7ed8192f2be37549fe80ae682a4'
WINDOW=262144;ROOMS=64
QUALIFICATION=Path('/mnt/DADOS/station-r71-qualification-20261007')
DROPIN=Path('/etc/systemd/system')/(SERVICE+'.d')/'zzzzzzzzzzzzzzzz-station-recovery-r71-20261007.conf'

def report(path,value):
    path.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
    if path.parent!=CHECK or CHECK.is_symlink():raise ValueError('Unexpected public report destination')
    owner=pwd.getpwnam('lz-servidor');os.chown(CHECK,owner.pw_uid,owner.pw_gid);CHECK.chmod(0o700)
    ops.private_text(path,json.dumps(value,indent=2)+'\n')
    os.chown(path,owner.pw_uid,owner.pw_gid)

def database(values):
    connection=Path(values['Station__DatabaseConnectionFile']).read_text().strip()
    return ops.database_name({'ConnectionStrings__SuiteStore':connection})

def baseline():
    if online.command_path()!=OLD/DLL or ops.digest(OLD/DLL)!=OLD_SHA:
        raise ValueError('Reviewed Station release changed')
    values,ids=ops.runtime();user=pwd.getpwnam(IDENTITY)
    if ids['Uid'][1]!=user.pw_uid or ids['Gid'][1]!=user.pw_gid:
        raise ValueError('Dedicated Station identity changed')
    if values.get('Station__Security__RequireVerifiedApp','false').lower()!='false':
        raise ValueError('Legacy app policy changed')
    if any(k.startswith('Suite__') and k!='Suite__Enabled' for k in values) or 'ConnectionStrings__SuiteStore' in values:
        raise ValueError('Unrelated product credentials present')
    registry=Path(values['Station__Online__EngineRegistryFile']);index=Path(values['Station__LibraryIndexFile'])
    if registry!=OLD/'online-engine-registry.json' or index!=security.INDEX:
        raise ValueError('Effective registry/index changed')
    if ops.digest(registry)!='a4412aa8139b865d62b1dc42cb4b08f7fc656b3a7efca6237df7854d2ef888cc':
        raise ValueError('Legacy registry changed')
    if ops.digest(index)!=security.INDEX_SHA or ops.digest(ADDITIONS)!=ADDITIONS_SHA:
        raise ValueError('Catalog or R71 runtime receipt changed')
    db=database(values)
    ledger=security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version')
    state=dict(utc=datetime.now(timezone.utc).isoformat(),sourceBase='e03a87217df9e51908b97bb775ecac889c22088c',
        oldDllSha256=OLD_SHA,indexSha256=ops.digest(index),indexRevision=14,
        oldRegistrySha256=ops.digest(registry),oldEngineIds=[e['id'] for e in json.loads(registry.read_text())],
        additionsSha256=ADDITIONS_SHA,newEngineIds=[e['id'] for e in json.loads(ADDITIONS.read_text())],
        environmentSha256=ops.digest(ENV),database=db,ledgerSha256=hashlib.sha256(ledger.encode()).hexdigest(),
        databaseScope=security.database_scope(db),shared={u:ops.state(u) for u in security.SHARED},
        configuration=security.snapshot_configs(),keyFingerprints={str(p):ops.digest(p) for p in ENV.parent.iterdir() if p.is_file()},
        realLicenseSha256=security.real_licenses(db),management=security.management_health(),relay=security.idle())
    return state,values

def preflight():
    state,_=baseline();destination=CHECK/('preflight-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
    report(destination,state)
    print(json.dumps(dict(preflightPassed=True,effectiveOldDllSha256=state['oldDllSha256'],
        oldEngineIds=state['oldEngineIds'],newEngineIds=state['newEngineIds'],relay=state['relay'],
        databaseIsolationVerified=True,managementHealthy=True,record=str(destination))),flush=True)

def paths(revision):
    if not re.fullmatch('[0-9a-f]{40}',revision):raise ValueError('Exact revision required')
    target=Path('/opt/turborama-station-recovery-r71-20261007-'+revision[:7])
    overlay=ENV.with_name('recovery-r71-'+revision[:7]+'.env')
    return target,overlay

def candidate(revision):
    git=['git','-c','safe.directory='+str(ROOT)]
    if ops.run(git+['rev-parse','HEAD'],cwd=ROOT).strip()!=revision:raise ValueError('Source revision changed')
    # The received Windows document has CRLF in its committed blob and text=auto.
    # Git may mark that receipt dirty even when its original bytes are identical.
    dirty=ops.run(git+['status','--porcelain'],cwd=ROOT).splitlines()
    receipt='docs/station-android/entrega-app-r71-20261007/NAVIGATION-AUDIT.md'
    for line in dirty:
        if line!=' M '+receipt or subprocess.check_output(git+['show','HEAD:'+receipt],cwd=ROOT)!=(ROOT/receipt).read_bytes():
            raise ValueError('Uncommitted source refused')
    folder=QUALIFICATION/'server-publish'
    meta=json.loads((QUALIFICATION/'release.json').read_text())
    if meta['sourceRevision']!=revision or meta['files']!=security.files(folder) or meta['dllSha256']!=ops.digest(folder/DLL):
        raise ValueError('Sealed build differs')
    return folder,meta

def unchanged(state):
    for name,digest in {**state['configuration'],**state['keyFingerprints']}.items():
        if ops.digest(Path(name))!=digest:raise ValueError('An original configuration/key changed')
    if ops.digest(security.INDEX)!=state['indexSha256'] or ops.digest(OLD/'online-engine-registry.json')!=state['oldRegistrySha256']:
        raise ValueError('Original catalog/registry changed')
    for unit,status in state['shared'].items():
        if ops.state(unit)!=status:raise ValueError('Another product service changed')
    db=state['database'];ledger=security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version')
    if hashlib.sha256(ledger.encode()).hexdigest()!=state['ledgerSha256']:raise ValueError('Database schema changed')
    if security.real_licenses(db)!=state['realLicenseSha256']:raise ValueError('Real license state changed')
    security.database_scope(db);security.management_health()

def sandbox(pid,target,index):
    rows=[r for r in index['items'] if r.get('catalogVisible',True)]
    representatives=[next(r for r in rows if r['platform']==platform) for platform in sorted({r['platform'] for r in rows})]
    media=[str(security.INDEX),*[r[k] for r in representatives for k in ['filePath','coverPath']]]
    payload=dict(read=media+[str(target/'online-engine-registry.json')],
        denied=['/etc/turborama-suite/activation-pepper','/etc/cloudflared/config.yml',
            '/var/lib/postgresql/16/main/PG_VERSION',str(OLD/DLL)],readonly=[str(security.INDEX),str(target/DLL)])
    code="""import json,os,sys
p=json.load(sys.stdin)
for name in p['read']:
    with open(name,'rb') as f:f.read(1)
for name in p['denied']:
    try:f=open(name,'rb')
    except OSError:continue
    else:f.close();raise SystemExit(1)
for name in p['readonly']:
    if os.access(name,os.W_OK):raise SystemExit(2)
print(json.dumps(dict(mediaFilesReadable=len(p['read'])-1,protectedPathsDenied=len(p['denied']),readOnlyMounts=True)))
"""
    r=subprocess.run(['/usr/bin/nsenter','--target',str(pid),'--mount','--wd=/',
        '/usr/bin/setpriv','--reuid',IDENTITY,'--regid',IDENTITY,'--clear-groups',
        '--inh-caps=-all','--ambient-caps=-all','--bounding-set=-all','--no-new-privs',
        '/usr/bin/python3','-c',code],input=json.dumps(payload),text=True,capture_output=True,timeout=30)
    if r.returncode:raise ValueError('Actual sandbox access gate failed')
    return json.loads(r.stdout)

def ready_recovery(base):
    ops.ready(base)
    with urlopen(base+'/ready/station/online',timeout=5) as response:value=json.load(response)
    recovery=value['recovery']
    if recovery['protocol']!='station-stream.v2' or recovery['maximumRooms']!=ROOMS or recovery['windowBytes']!=WINDOW:
        raise ValueError('Effective v2 settings differ')
    return value

def idle_recovery():
    until=time.monotonic()+20
    while True:
        value=security.telemetry();recovery=value.get('recovery',{})
        if not value['activeRooms'] and not value['activeConnections'] and not recovery.get('activeConnections',0) and not recovery.get('retainedRooms',0):
            return value
        if time.monotonic()>=until:raise ValueError('A relay room remains active; restart deferred')
        time.sleep(.5)

def qualify(revision):
    state,values=baseline();folder,meta=candidate(revision);target,overlay=paths(revision)
    user=pwd.getpwnam(IDENTITY);additions=json.loads(ADDITIONS.read_text());index=json.loads(security.INDEX.read_text())
    if not target.exists():
        shutil.copytree(folder,target)
        ops.run(['/usr/bin/python3',str(ROOT/'tests/StationRecovery/merge_engine_registry.py'),
            '--existing',str(OLD/'online-engine-registry.json'),'--additions',str(ADDITIONS),
            '--output',str(target/'online-engine-registry.json')])
        for p in [target,*target.rglob('*')]:os.chown(p,0,user.pw_gid);p.chmod(0o750 if p.is_dir() else 0o640)
    installed=security.files(target);registry_hash=installed.pop('online-engine-registry.json')
    if installed!=meta['files']:raise ValueError('Installed qualified binary differs')
    expected=json.loads((OLD/'online-engine-registry.json').read_text())+additions
    if json.loads((target/'online-engine-registry.json').read_text())!=expected:raise ValueError('Additive engine registry differs')
    text=('Station__Online__EngineRegistryFile='+str(target/'online-engine-registry.json')+'\n'
        'Station__Online__RecoveryEnabled=true\nStation__Online__RecoveryMaxRooms='+str(ROOMS)+'\n'
        'Station__Online__RecoveryWindowBytes='+str(WINDOW)+'\n')
    if not overlay.exists():ops.private_text(overlay,text);os.chown(overlay,0,user.pw_gid);overlay.chmod(0o640)
    if overlay.is_symlink() or overlay.read_text()!=text:raise ValueError('Recovery overlay changed')
    with socket.socket() as probe:probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
    base='http://127.0.0.1:'+str(port);unit='station-r71-shadow-'+revision[:7]+'.service'
    logfile=CHECK/('shadow-'+revision[:7]+'-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.log')
    CHECK.mkdir(mode=0o700,exist_ok=True)
    # Root-only logs never become part of the public receipt.
    ops.private_text(logfile,'')
    shadow_env=logfile.with_suffix('.env')
    ops.private_text(shadow_env,ENV.read_text()+'\n'+text)
    arguments=['systemd-run','--quiet','--collect','--unit',unit]
    for k,v in sandbox_properties(target,IDENTITY,security.MEDIA).items():arguments+=['--property',k+'='+v]
    arguments+=['--property','EnvironmentFile='+str(shadow_env),
        '--property','WorkingDirectory='+str(target),'--property','StandardOutput=append:'+str(logfile),
        '--property','StandardError=append:'+str(logfile),'/usr/bin/dotnet',str(target/DLL),'--urls',base]
    print(json.dumps(dict(stage='isolated_shadow',sourceRevision=revision)),flush=True)
    ops.run(arguments)
    try:
        ready=ready_recovery(base);pid=int(ops.run(['systemctl','show',unit,'-p','MainPID','--value']))
        fs=sandbox(pid,target,index)
        probe_values=dict(values,Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'))
        proof=load('verificar-recovery-station-r71.py').verify(index,probe_values,base,
            lambda statement:ops.sql(state['database'],statement),additions,state['oldEngineIds'])
    finally:
        stopped=subprocess.run(['systemctl','stop',unit],capture_output=True,timeout=30)
        if stopped.returncode and ops.run(['systemctl','show',unit,'-p','MainPID','--value']).strip() not in ('','0'):
            raise ValueError('Temporary shadow did not stop')
        shadow_env.unlink()
    unchanged(state);security.idle()
    result=dict(passed=True,stage='qualified_shadow',sourceRevision=revision,dllSha256=meta['dllSha256'],
        registrySha256=registry_hash,target=str(target),overlaySha256=ops.digest(overlay),proof=proof,
        sandbox=fs,ready=ready,completedAtUtc=datetime.now(timezone.utc).isoformat(),productionChanged=False)
    destination=CHECK/('qualified-'+revision[:7]+'-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
    report(destination,result);print(json.dumps(dict(qualified=True,checks=proof['checks'],record=str(destination))),flush=True)
    return result

def backup_create(state,revision):
    backup=Path('/mnt/DADOS/station-r71-backup-20261007-'+revision[:7]+'-'+datetime.now(timezone.utc).strftime('%H%M%S'))
    backup.mkdir(mode=0o700);(backup/'configuration').mkdir(mode=0o700)
    shutil.copytree(OLD,backup/'old-api')
    for i,name in enumerate({**state['configuration'],**state['keyFingerprints']}):
        shutil.copy2(name,backup/'configuration'/str(i))
    ops.private_text(backup/'state.json',json.dumps(state,indent=2)+'\n')
    with (backup/'database.dump').open('xb') as dump:
        os.fchmod(dump.fileno(),0o600)
        r=subprocess.run(['runuser','-u','postgres','--','pg_dump','--format=custom','--dbname',state['database']],
            stdout=dump,stderr=subprocess.PIPE,timeout=300)
        if r.returncode:raise ValueError('Private database backup failed')
    output=ops.run(['pg_virtualenv','-t',sys.executable,'-B',str(Path(__file__).resolve()),'--restore-backup',str(backup)],timeout=360)
    if 'databaseRestoreVerified' not in output:raise ValueError('Isolated database restore proof failed')
    return backup

def restore_check(backup):
    if backup.parent!=Path('/mnt/DADOS') or not re.fullmatch(r'station-r71-backup-20261007-[a-f0-9]{7}-[0-9]{6}',backup.name):
        raise ValueError('Unexpected private backup')
    if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.') or not ops.run(
        ['psql','-X','-Atqc','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('Temporary PostgreSQL required')
    state=json.loads((backup/'state.json').read_text());db='station_r71_restore'
    ops.run(['createdb',db]);ops.run(['pg_restore','--exit-on-error','--no-owner','--no-acl','--dbname',db,str(backup/'database.dump')],timeout=300)
    ledger=ops.run(['psql','-X','-qAt','--dbname',db,'-c','SELECT version FROM suite.schema_migrations ORDER BY version']).strip()
    if hashlib.sha256(ledger.encode()).hexdigest()!=state['ledgerSha256']:raise ValueError('Restored migration ledger differs')
    print(json.dumps(dict(databaseRestoreVerified=True,temporaryCluster=True)),flush=True)

def rollback(backup):
    state=json.loads((backup/'state.json').read_text());target=Path(state['target'])
    if online.command_path() not in [OLD/DLL,target/DLL]:raise ValueError('Station release was superseded')
    idle_recovery();unchanged(state)
    if DROPIN.exists():
        if DROPIN.is_symlink() or ops.digest(DROPIN)!=state['dropinSha256']:raise ValueError('Recovery drop-in changed')
        DROPIN.unlink()
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
    if online.command_path()!=OLD/DLL or ops.digest(OLD/DLL)!=OLD_SHA:raise ValueError('Previous Station did not return')
    unchanged(state);ops.private_text(backup/'rollback-complete','Previous Station restored; no production database restore.\n')
    return dict(passed=True,oldDllSha256=OLD_SHA,databaseRestored=False)

def apply(revision):
    result=dict(applied=False,sourceRevision=revision);stage='qualification';changed=False;backup=None
    try:
        qualified=qualify(revision);state,values=baseline();_,meta=candidate(revision);target,overlay=paths(revision)
        if DROPIN.exists():raise ValueError('Recovery drop-in already exists')
        stage='backup_restore';print(json.dumps(dict(stage=stage)),flush=True)
        backup=backup_create(state,revision)
        override=systemd_override(target,IDENTITY,security.MEDIA,ENV)+'EnvironmentFile='+str(overlay)+'\n'
        state.update(target=str(target),dropinSha256=hashlib.sha256(override.encode()).hexdigest(),sourceRevision=revision)
        ops.replace_config(backup/'state.json',json.dumps(state,indent=2)+'\n')
        unchanged(state);security.idle()
        stage='station_cutover';print(json.dumps(dict(stage=stage,backupRestoreVerified=True)),flush=True)
        ops.private_text(DROPIN,override);DROPIN.chmod(0o644);changed=True
        ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ready_recovery('http://127.0.0.1:5192')
        running,ids=ops.runtime();user=pwd.getpwnam(IDENTITY)
        if online.command_path()!=target/DLL or ids['Uid'][1]!=user.pw_uid or ids['Gid'][1]!=user.pw_gid:
            raise ValueError('Effective release identity differs')
        if running['Station__Online__EngineRegistryFile']!=str(target/'online-engine-registry.json') or running['Station__Online__RecoveryEnabled']!='true':
            raise ValueError('Effective overlay differs')
        stage='public_https_wss_proofs';print(json.dumps(dict(stage=stage)),flush=True)
        index=json.loads(security.INDEX.read_text())
        proof=load('verificar-recovery-station-r71.py').verify(index,running,'https://app.lzgames.com.br',
            lambda statement:ops.sql(state['database'],statement),json.loads(ADDITIONS.read_text()),state['oldEngineIds'])
        unchanged(state);idle_recovery();fs=sandbox(int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value'])),target,index)
        for port in [5190,5191]:
            with urlopen('http://127.0.0.1:'+str(port)+'/health',timeout=5) as response:
                if response.status!=200:raise ValueError('Other product health failed')
        result.update(applied=True,completedAtUtc=datetime.now(timezone.utc).isoformat(),
            dllSha256=meta['dllSha256'],target=str(target),registrySha256=qualified['registrySha256'],
            engines=state['oldEngineIds']+state['newEngineIds'],shadow=qualified,publicProof=proof,sandbox=fs,
            pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value'])),backup=str(backup),backupRestoreVerified=True,
            recoveryEnabled=True,recoveryMaximumRooms=ROOMS,recoveryWindowBytes=WINDOW,maximumRetainedBytes=ROOMS*WINDOW*2,
            requireVerifiedApp=False,oldKeysPreserved=True,realLicensesPreserved=True,otherProductsPreserved=True,
            oldConfigurationPreserved=True,nginxChanged=False,cloudflareChanged=False,firewallChanged=False,
            databaseSchemaChanged=False,indexRevision=14,visibleItems=2212,indexSha256=state['indexSha256'],
            relayAfter=ready_recovery('http://127.0.0.1:5192'),management=security.management_health(),androidGameplay=False)
        destination=CHECK/('active-'+revision[:7]+'-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
        report(destination,result);print(json.dumps(dict(applied=True,sourceRevision=revision,dllSha256=meta['dllSha256'],
            pid=result['pid'],publicChecks=proof['checks'],record=str(destination))),flush=True)
    except Exception as error:
        result.update(failedStage=stage,errorType=type(error).__name__)
        if changed:
            try:result['rollback']=rollback(backup)
            except Exception as failure:result['rollback']=dict(passed=False,errorType=type(failure).__name__)
        destination=CHECK/('failure-'+revision[:7]+'-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
        report(destination,result)
        # Traceback is local and protected; tokens and signed bodies are not logged.
        diagnostic=CHECK/('diagnostic-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+'.txt')
        ops.private_text(diagnostic,traceback.format_exc())
        print(json.dumps(dict(applied=False,failedStage=stage,errorType=type(error).__name__,rollback=result.get('rollback'),record=str(destination))),flush=True)
        raise

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--preflight',action='store_true')
    parser.add_argument('--apply');parser.add_argument('--qualify');parser.add_argument('--rollback',type=Path);parser.add_argument('--restore-backup',type=Path)
    args=parser.parse_args()
    if os.geteuid()!=0 or (not args.restore_backup and os.environ.get('PKEXEC_UID')!='1000'):
        raise SystemExit('Native Linux operator authentication required')
    try:
        if args.restore_backup:restore_check(args.restore_backup)
        elif args.preflight:preflight()
        elif args.qualify:qualify(args.qualify)
        elif args.apply:apply(args.apply)
        elif args.rollback:print(json.dumps(rollback(args.rollback)),flush=True)
        else:raise ValueError('A rollout action is required')
    except Exception as error:
        # Never echo HTTP bodies, command arguments, credentials or private exception strings.
        print(json.dumps(dict(passed=False,errorType=type(error).__name__)),flush=True);raise SystemExit(1)

if __name__=='__main__':main()
