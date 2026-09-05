#!/usr/bin/python3
"""Reviewed API-only replacement of 34e31f2 with CI artifact efaf1d3.

Host-specific, run through native pkexec authentication with --apply. Preserves
the existing drop-ins, schema, credentials and all other service processes.
Historical deployment scripts/reports are never imported, executed or rewritten.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import socket
import stat
import subprocess
import sys
import tarfile
import time
import urllib.error
import urllib.request
import zipfile

ROOT=Path('/home/lz-servidor/worktrees/servidor-pix-es-handoff-retorno-20260905')
OUTPUT=ROOT/'outputs/es-reopening'
ARTIFACT=OUTPUT/'servidor-efaf1d3.zip'
ARTIFACT_SHA='ab017ad8313fc0c50e702c4d6aa7ae7f8348276376a8ea850f04a19ac0c1cf86'
COMMIT='efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a'
RELEASE=Path('/opt/turborama-suite-r5-releases/es-reopen-efaf1d3-20260905')
API='turborama-suite-api.service'
OTHER=['turborama-suite-admin.service','turborama-pix.service']
OLD_DLL=Path('/opt/turborama-suite-r5-releases/es-suite-34e31f2-20260905/server/TurboRamaSuiteOnlineServer.dll')
OLD_SHA='8a8a90c9a623c155dabeeb3ba88ef0c983964bd603aa3b0240dd1df5fee4c568'
NEW_DLL=RELEASE/'server/TurboRamaSuiteOnlineServer.dll'
NEW_SHA='e10bcf191c7b1c4b030427713b848a8e89af51517483d319b5979cd9ea7b07ef'
UNIT_FILE=Path('/etc/systemd/system')/API
DROP_DIR=UNIT_FILE.with_name(API+'.d')
OLD_DROP=DROP_DIR/'zzzz-es-suite-20260905.conf'
NEW_DROP=DROP_DIR/'zzzzz-es-reopen-20260905.conf'
REPORT={'runtime_commit':COMMIT,'artifact_id':9977821782,'ci_run':33994510188,
        'artifact_sha256':ARTIFACT_SHA,'release':str(RELEASE),
        'scope':'API only; no migrations; no admin/PIX restart',
        'status':'preparing','stages':[]}
BACKUP=None

def require(condition,message):
    if not condition: raise RuntimeError(message)

def digest(path):
    with open(path,'rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def run(args,timeout=40):
    p=subprocess.run(args,text=True,capture_output=True,timeout=timeout,cwd='/')
    if p.returncode:
        if '--production-smoke' in args:
            for line in p.stdout.splitlines():
                if line.startswith('{'):
                    try: REPORT['smoke']=json.loads(line)
                    except json.JSONDecodeError: pass
        if BACKUP:
            with open(BACKUP/'command-errors.log','a') as f:
                f.write(args[0]+'\n'+p.stdout+'\n'+p.stderr+'\n')
        raise RuntimeError('Command failed: '+args[0]+' (exit '+str(p.returncode)+')')
    return p.stdout.strip()

def save():
    path=OUTPUT/'deployment-result.json'
    path.write_text(json.dumps(REPORT,indent=2)+'\n')
    os.chmod(path,0o600);os.chown(path,1000,1000)
    if BACKUP: (BACKUP/'deployment-result.json').write_text(json.dumps(REPORT,indent=2)+'\n')

def stage(name,**details):
    REPORT['stages'].append({'stage':name,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),**details})
    save();print(name,flush=True)

def process(unit):
    props=['Id','MainPID','ActiveState','WorkingDirectory','User','Group','DropInPaths','ExecMainStartTimestamp']
    result=dict(x.split('=',1) for x in run(['/usr/bin/systemctl','show',unit,*['-p'+p for p in props]]).splitlines())
    require(result['ActiveState']=='active' and int(result['MainPID'])>0,'Service not active: '+unit)
    args=Path('/proc',result['MainPID'],'cmdline').read_text().rstrip('\0').split('\0')
    binaries=[x for x in args if x.endswith('.dll')]
    require(len(binaries)==1,'Unexpected managed process: '+unit)
    result['binary']=binaries[0];result['binary_sha256']=digest(binaries[0])
    return result,args

def environment(pid):
    return dict(x.split('=',1) for x in Path('/proc',pid,'environ').read_text().split('\0') if '=' in x)

def health():
    for route in ['/health','/ready','/ready/content']:
        with urllib.request.urlopen('http://127.0.0.1:5190'+route,timeout=10) as r:
            require(r.status==200,'API readiness failed: '+route)

def wait_health():
    for attempt in range(15):
        try: process(API);health();return
        except Exception:
            if attempt==14: raise
            time.sleep(1)

def schema():
    raw=run(['/usr/sbin/runuser','-u','postgres','--','/usr/bin/psql','-X','-qAt','-v','ON_ERROR_STOP=1','-d','postgres','-c',
             'SELECT version FROM suite.schema_migrations ORDER BY version'])
    values=raw.splitlines()
    require([int(x[:3]) for x in values]==list(range(1,26)),'Expected schema 001–025; inspect drift')
    return values

def protected_files():
    paths=[UNIT_FILE,*DROP_DIR.rglob('*')]
    for directory in Path('/etc').glob('turborama-suite*'):
        if directory.is_dir(): paths.extend(directory.rglob('*'))
    return {str(p):(digest(p),stat.S_IMODE(p.stat().st_mode),p.stat().st_uid,p.stat().st_gid)
            for p in paths if p.is_file() and p!=NEW_DROP}

def check_others(before):
    for unit,info in before.items():
        current,_=process(unit)
        require(current==info,'Unrelated service changed during API rollout: '+unit)

def public_routes():
    for base in ['http://127.0.0.1:5190','https://app.lzgames.com.br']:
        for scope,expected in [('INVALID','CLIENT_SCOPE_INVALID'),('EMULATIONSTATION','JSON_INVALID'),(None,'JSON_INVALID')]:
            headers={'Content-Type':'application/json','User-Agent':'TurboRama-Deployment-Check/1.0'}
            if scope: headers['X-TurboRama-Client']=scope
            request=urllib.request.Request(base+'/v1/suite/challenges',data=b'{}',headers=headers,method='POST')
            try:
                with urllib.request.urlopen(request,timeout=10) as r: code=r.status;body=r.read(4096)
            except urllib.error.HTTPError as e: code=e.code;body=e.read(4096)
            require(code==400 and json.loads(body).get('code')==expected,'Unexpected route contract: '+base)

def main():
    global BACKUP
    require(os.geteuid()==0 and sys.argv[1:]==['--apply'],'Use native system authentication with --apply')
    require(socket.gethostname()=='lz-servidor-A520M-S2H','Unexpected production host')
    require(not NEW_DROP.exists() and not RELEASE.exists(),'Release/drop-in already exists; inspect prior execution')
    require(not (OUTPUT/'deployment-result.json').exists(),'Preserve prior reopening evidence before another execution')
    os.umask(0o077)
    require(digest(ARTIFACT)==ARTIFACT_SHA,'Approved artifact checksum mismatch')
    before,args=process(API)
    require(args==['/usr/bin/dotnet',str(OLD_DLL)] and before['binary_sha256']==OLD_SHA,
            'API baseline changed; review before deployment')
    require(OLD_DROP.is_file(),'Existing ES feature drop-in is absent')
    other={u:process(u)[0] for u in OTHER}
    env=environment(before['MainPID'])
    for key in ['Suite__Enabled','Suite__EmulationStation__Enabled','Suite__Inventory__Enabled','Suite__NetworkInventory__Enabled']:
        require(env.get(key)=='true','Required existing feature is not enabled: '+key)
    require(digest('/etc/turborama-suite/online-spki.der')=='2d8987dc740a47cba0a8ee7c3e0fbffaff057e2325c46ea2324ee335de69da50','Online authority changed')
    protected=protected_files();versions=schema();health()
    require(digest(OUTPUT/'smoke/TurboRamaSuiteOnlineServer.dll')==NEW_SHA,'Smoke must reference the approved new API assembly')
    run(['/usr/bin/dotnet',str(OUTPUT/'smoke/EsProductionSmoke.dll'),'--tls-only'])
    REPORT['before_api']=before;REPORT['unchanged_services']=other
    REPORT['schema_versions']=versions
    stage('baseline-authority-tls-and-schema-verified')
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    BACKUP=Path('/var/backups/turborama-suite')/('es-reopen-efaf1d3-'+stamp)
    BACKUP.mkdir(parents=True,mode=0o700)
    REPORT['backup']=str(BACKUP)
    shutil.copy2(ARTIFACT,BACKUP/'reviewed-artifact.zip')
    require(digest(BACKUP/'reviewed-artifact.zip')==ARTIFACT_SHA,'Protected artifact copy mismatch')
    with tarfile.open(BACKUP/'api-configuration-before.tar.gz','w:gz') as archive:
        for path in [UNIT_FILE,DROP_DIR,*sorted(Path('/etc').glob('turborama-suite*'))]:
            archive.add(path,arcname=str(path).lstrip('/'))
    with tarfile.open(BACKUP/'api-configuration-before.tar.gz','r:gz') as archive:
        names={item.name for item in archive.getmembers()}
        require(str(UNIT_FILE).lstrip('/') in names and str(OLD_DROP).lstrip('/') in names,'API backup is incomplete')
    REPORT['configuration_backup_sha256']=digest(BACKUP/'api-configuration-before.tar.gz')
    (BACKUP/'before-api.json').write_text(json.dumps(before,indent=2)+'\n')
    RELEASE.mkdir(mode=0o755)
    with zipfile.ZipFile(BACKUP/'reviewed-artifact.zip') as archive:
        items=archive.infolist()
        require(len(items)<=100 and sum(x.file_size for x in items)<50*1024*1024,'Unexpected artifact shape')
        for item in items:
            require(not Path(item.filename).is_absolute() and '..' not in Path(item.filename).parts and not stat.S_ISLNK(item.external_attr>>16),'Unsafe archive entry')
        archive.extractall(RELEASE)
    for p in [RELEASE,*RELEASE.rglob('*')]: os.chmod(p,0o755 if p.is_dir() else 0o644)
    manifest=set()
    for line in (RELEASE/'SHA256SUMS.txt').read_text().splitlines():
        sha,name=line.split(maxsplit=1);name=name.removeprefix('./')
        require(not Path(name).is_absolute() and '..' not in Path(name).parts,'Unsafe manifest entry')
        require(digest(RELEASE/name)==sha,'Internal artifact checksum mismatch: '+name)
        manifest.add(name)
    actual={str(p.relative_to(RELEASE)) for p in RELEASE.rglob('*') if p.is_file()}
    require(len(manifest)==58 and actual==manifest|{'SHA256SUMS.txt'},'Artifact manifest is incomplete')
    require((RELEASE/'COMMIT.txt').read_text().strip()==COMMIT and digest(NEW_DLL)==NEW_SHA,'Published runtime commit/hash mismatch')
    checks=RELEASE/'deployment-checks';shutil.copytree(OUTPUT/'smoke',checks)
    shutil.copyfile('/etc/turborama-suite/online-spki.der',checks/'approved-online-spki.der')
    for p in [checks,*checks.rglob('*')]: os.chmod(p,0o755 if p.is_dir() else 0o644)
    drop='[Service]\nExecStart=\nExecStart=/usr/bin/dotnet '+str(NEW_DLL)+'\n'
    drop_sha=hashlib.sha256(drop.encode()).hexdigest()
    rollback=BACKUP/'rollback-api.sh'
    rollback.write_text('#!/usr/bin/env bash\nset -Eeuo pipefail\n[[ $EUID -eq 0 ]]\n'+
        '[[ "$(sha256sum '+shlex.quote(str(NEW_DROP))+')" == '+shlex.quote(drop_sha+'  '+str(NEW_DROP))+' ]]\n'+
        '[[ "$(systemctl show '+API+' -pExecStart --value)" == *'+shlex.quote(str(NEW_DLL))+'* ]]\n'+
        'rm -- '+shlex.quote(str(NEW_DROP))+'\nsystemctl daemon-reload\nsystemctl restart '+API+'\n'+
        'systemctl is-active '+API+'\ncurl --fail --silent --show-error http://127.0.0.1:5190/health\n')
    os.chmod(rollback,0o700)
    REPORT['rollback_command']='pkexec '+str(rollback)
    REPORT['operator_source_sha256']=digest(Path(__file__))
    REPORT['smoke_source_sha256']={str(p.relative_to(ROOT)):digest(p) for p in [
        ROOT/'ops/production/es-smoke/Program.cs',ROOT/'ops/production/es-smoke/EsProductionSmoke.csproj',
        ROOT/'tests/TurboRamaSuiteEmulationStation.Tests/SharedIntegrationChecks.cs']}
    stage('api-release-backup-and-rollback-ready',manifest_files_verified=58)
    switched=False
    try:
        # Recheck after staging, before the only service mutation.
        require(process(API)[0]==before and protected_files()==protected,'API/configuration drift during staging')
        check_others(other)
        NEW_DROP.write_text(drop);switched=True;os.chmod(NEW_DROP,0o644)
        run(['/usr/bin/systemctl','daemon-reload'])
        run(['/usr/bin/systemctl','restart',API]);wait_health()
        after,new_args=process(API)
        require(new_args==['/usr/bin/dotnet',str(NEW_DLL)] and after['binary_sha256']==NEW_SHA,'New API executable not loaded')
        require(after['MainPID']!=before['MainPID'],'API PID did not change')
        for key in ['WorkingDirectory','User','Group']:
            require(after[key]==before[key],'API service identity/content root changed: '+key)
        loaded=environment(after['MainPID'])
        relevant=lambda values:{k:v for k,v in values.items() if k.startswith(('Suite__','ConnectionStrings__','SUITE_','ASPNETCORE_','DOTNET_','TURBORAMA_'))}
        require(relevant(loaded)==relevant(env),'Effective protected API configuration changed')
        require(protected_files()==protected and schema()==versions,'Existing credentials/configuration/schema changed')
        check_others(other);REPORT['after_api']=after
        stage('new-api-healthy-other-processes-unchanged')
        public_routes();stage('origin-and-public-routes-verified')
        smoke=run(['/usr/bin/prlimit','--core=0','--','/usr/sbin/runuser','-u','postgres','-g','turborama-suite-bff','--',
                   '/usr/bin/dotnet',str(checks/'EsProductionSmoke.dll'),'--production-smoke'],timeout=90)
        REPORT['smoke']=json.loads(smoke.splitlines()[-1])
        require(REPORT['smoke']['status']=='passed' and REPORT['smoke']['customerNotifications']==0,'Production reopening smoke failed')
        health();check_others(other)
        require(protected_files()==protected and schema()==versions,'Configuration/schema changed after smoke')
        REPORT['status']='deployed';REPORT['windows_reopening']='pending-same-installed-1.1.2'
        stage('api-only-deployment-and-reopening-verified',other_service_pids_unchanged=True,protected_configuration_unchanged=True,migrations_applied=0)
    except BaseException:
        if switched:
            REPORT['status']='rolling-back';save()
            try:
                require(NEW_DROP.is_file() and digest(NEW_DROP)==drop_sha,'Additional API drop-in changed; review rollback')
                NEW_DROP.unlink()
                run(['/usr/bin/systemctl','daemon-reload']);run(['/usr/bin/systemctl','restart',API]);wait_health()
                restored,restored_args=process(API)
                require(restored_args==args and restored['binary_sha256']==OLD_SHA,'Previous API was not restored')
                check_others(other)
                REPORT['status']='rolled-back';stage('previous-api-restored-other-services-unchanged')
            except Exception:
                REPORT['status']='rollback-needs-attention';save()
        raise

if __name__=='__main__':
    try: main()
    except BaseException as error:
        REPORT['error_type']=type(error).__name__;REPORT['error']=str(error)
        if REPORT['status']=='preparing': REPORT['status']='stopped-before-api-switch'
        # Never overwrite an earlier deployment record on a refused rerun.
        if BACKUP or not (OUTPUT/'deployment-result.json').exists(): save()
        print('API DEPLOYMENT STOPPED: '+str(error),file=sys.stderr,flush=True);sys.exit(1)
