#!/usr/bin/python3
"""Concrete deployment of the reviewed 34e31f2 artifact on the authorized host.

Run through native pkexec authentication. Private inputs stay on this host.
Only additive migrations and three new systemd drop-ins are installed. Rollback
removes those exact drop-ins and keeps the additive schema and security audit.
"""
import base64
import datetime
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
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
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

REPO=Path('/home/lz-servidor/worktrees/servidor-pix-es-handoff-retorno-20260905')
OUTPUT=REPO/'outputs/es-deployment'
ARTIFACT=REPO/'outputs/servidor-suite-emulationstation-34e31f2.zip'
ARTIFACT_SHA='1a299b048ccf654ca673f2c0ba3e123e0b5587073476938b05aade3c98d769e1'
COMMIT='34e31f26b6a864a7aa5d701b94fe29ad166e86ac'
RELEASE=Path('/opt/turborama-suite-r5-releases/es-suite-34e31f2-20260905')
PRIOR_DEPLOYMENT=Path('/var/backups/turborama-suite/es-suite-34e31f2-20260905T210238Z/deployment-result.json')
UNITS=['turborama-suite-admin.service','turborama-suite-api.service','turborama-pix.service']
OLD={
 'turborama-suite-api.service':('/opt/turborama-suite-r5-releases/r25-7-whatsapp-session-open-20260903/api/TurboRamaSuiteOnlineServer.dll','18a3f6ea09e95cfe47d56653a230df0396de3c11fc5f8ea47cf0f26c66a4e7bc'),
 'turborama-suite-admin.service':('/opt/turborama-suite-r5-releases/r25-2-admin-reader-fix-20260902/admin/TurboRamaSuiteAdminServer.dll','cdcbd8783fe19626bfc9a6da8e92209093a0f0365f7cb4535da28fe2250c191c'),
 'turborama-pix.service':('/opt/turborama-suite-r5-releases/r25-1-inventory-whatsapp-20260902/pix/TurboRamaPixOnlineServer.dll','1a225de8584a528d4f0e69b30a51d09be576f17684199a4d9afa8c3bcce18188')}
NEW={
 'turborama-suite-api.service':RELEASE/'server/TurboRamaSuiteOnlineServer.dll',
 'turborama-suite-admin.service':RELEASE/'admin-backend/TurboRamaSuiteAdminServer.dll',
 'turborama-pix.service':RELEASE/'pix-admin/TurboRamaPixOnlineServer.dll'}
DROPINS={u:Path('/etc/systemd/system')/(u+'.d')/'zzzz-es-suite-20260905.conf' for u in UNITS}
REPORT={'commit':COMMIT,'artifact_sha256':ARTIFACT_SHA,'release':str(RELEASE),'stages':[],'status':'preparing','ci_run':33989933344,'ci_attempt':2}
BACKUP=None

def require(condition,message):
    if not condition: raise RuntimeError(message)

def digest(path):
    h=hashlib.sha256()
    with open(path,'rb') as file:
        for data in iter(lambda:file.read(1024*1024),b''): h.update(data)
    return h.hexdigest()

def run(args,input=None,timeout=40):
    p=subprocess.run(args,input=input,text=True,capture_output=True,timeout=timeout,check=False,cwd='/')
    if p.returncode:
        if '--production-smoke' in args:
            for line in p.stdout.splitlines():
                if line.startswith('{'):
                    try: REPORT['smoke']=json.loads(line)
                    except json.JSONDecodeError: pass
        if BACKUP:
            with open(BACKUP/'command-errors.log','a') as log:
                log.write(args[0]+'\n'+p.stdout+'\n'+p.stderr+'\n')
        raise RuntimeError('Command failed: '+args[0]+' (exit '+str(p.returncode)+')')
    return p.stdout.strip()

def query(sql):
    return run(['/usr/sbin/runuser','-u','postgres','--','/usr/bin/psql','-X','-qAt','-v','ON_ERROR_STOP=1','-d','postgres','-c',sql])

def process(unit):
    raw=run(['/usr/bin/systemctl','show',unit,'-pMainPID','-pWorkingDirectory','-pActiveState','-pDropInPaths'])
    result=dict(line.split('=',1) for line in raw.splitlines())
    require(result['ActiveState']=='active','Required service is not active: '+unit)
    pid=result['MainPID']
    result['argv']=Path('/proc',pid,'cmdline').read_text().split('\0')
    env=dict(item.split('=',1) for item in Path('/proc',pid,'environ').read_text().split('\0') if '=' in item)
    return result,env

def save_report():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    path=OUTPUT/'deployment-result.json'
    path.write_text(json.dumps(REPORT,indent=2)+'\n');os.chmod(path,0o600);os.chown(path,1000,1000)
    if BACKUP: (BACKUP/'deployment-result.json').write_text(json.dumps(REPORT,indent=2)+'\n')

def stage(name,**details):
    REPORT['stages'].append({'stage':name,'time_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),**details})
    save_report();print(name,flush=True)

class UnixConnection(http.client.HTTPConnection):
    def connect(self):
        self.sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM);self.sock.settimeout(10)
        self.sock.connect('/run/turborama-suite-admin/admin.sock')

def health(unit):
    if unit=='turborama-suite-admin.service':
        token=Path('/etc/turborama-suite-bff/token').read_text().strip()
        for path in ['/health','/readiness','/readiness/content']:
            c=UnixConnection('localhost',timeout=10);c.request('GET',path,headers={'X-Suite-Admin-Token':token})
            r=c.getresponse();r.read();require(r.status==200,'Administrative readiness failed: '+path);c.close()
    else:
        paths=['http://127.0.0.1:5190/health','http://127.0.0.1:5190/ready','http://127.0.0.1:5190/ready/content'] if unit=='turborama-suite-api.service' else ['http://127.0.0.1:5187/v1/health']
        for url in paths:
            with urllib.request.urlopen(url,timeout=10) as r: require(r.status==200,'Readiness failed')

def wait_healthy(unit):
    last=None
    for _ in range(15):
        try: process(unit);health(unit);return
        except Exception as ex: last=ex;time.sleep(1)
    raise RuntimeError('Service did not become healthy: '+unit) from last

def probe(base,path,scope,expected):
    headers={'Content-Type':'application/json','User-Agent':'TurboRama-Deployment-Check/1.0'}
    if scope is not None: headers['X-TurboRama-Client']=scope
    request=urllib.request.Request(base+path,data=b'{}',headers=headers,method='POST')
    try:
        with urllib.request.urlopen(request,timeout=10) as r: code=r.status;body=r.read(4096)
    except urllib.error.HTTPError as ex: code=ex.code;body=ex.read(4096)
    try: value=json.loads(body)
    except json.JSONDecodeError as error:
        raise RuntimeError('Non-JSON diagnostic response: '+base+path+' HTTP '+str(code)) from error
    require(code==400 and value.get('code')==expected,'Unexpected public/origin contract: '+base+path+' HTTP '+str(code))

def main():
    global BACKUP
    require(os.geteuid()==0,'Use native system authentication')
    require(sys.argv[1:]==['--apply'],'Explicit --apply is required')
    require(socket.gethostname()=='lz-servidor-A520M-S2H','Unexpected production host')
    os.umask(0o077)
    previous=json.loads(PRIOR_DEPLOYMENT.read_text()) if RELEASE.exists() and PRIOR_DEPLOYMENT.is_file() else None
    require(not RELEASE.exists() or previous and previous.get('status')=='rolled-back' and previous.get('artifact_sha256')==ARTIFACT_SHA,
        'An existing release requires the protected record of the reviewed rollback')
    if previous: REPORT['previous_attempt_backup']=str(PRIOR_DEPLOYMENT.parent)
    require(all(not p.exists() for p in DROPINS.values()),'A deployment drop-in already exists')
    require(digest(ARTIFACT)==ARTIFACT_SHA,'Reviewed artifact checksum does not match')
    before={};before_env={}
    for unit,(binary,sha) in OLD.items():
        info,env=process(unit);require(binary in info['argv'] and digest(binary)==sha,'Production binary changed: '+unit)
        before[unit]=info;before_env[unit]=env;health(unit)
    api_env=before_env['turborama-suite-api.service']
    panel_host=before_env['turborama-pix.service'].get('TURBORAMA_ADMIN_PUBLIC_HOST','')
    require(re.fullmatch(r'[A-Za-z0-9.-]+',panel_host) is not None,'Unexpected protected admin host configuration')
    REPORT['admin_url']='https://'+panel_host+'/admin'
    require(api_env.get('Suite__Enabled')=='true','Original Suite must remain enabled')
    require(re.search(r'(?:^|;)Database=postgres(?:;|$)',api_env.get('ConnectionStrings__SuiteStore',''),re.I) is not None,'Suite database mismatch')
    require(digest('/etc/turborama-suite/online-spki.der')=='2d8987dc740a47cba0a8ee7c3e0fbffaff057e2325c46ea2324ee335de69da50','Client online authority mismatch')
    key=base64.b64decode(Path('/etc/turborama-suite/inventory-encryption-key').read_text().strip(),validate=True)
    require(len(key)==32,'Existing inventory key is invalid')
    cipher_rows=query("SELECT encode(baseboard_serial_cipher,'hex')||':'||encode(system_uuid_cipher,'hex') FROM suite.suite_device_inventory").splitlines()
    for row in cipher_rows:
        for part in row.split(':'):
            value=bytes.fromhex(part);require(len(value)>=29 and value[0]==1,'Unexpected existing inventory format')
            AESGCM(key).decrypt(value[1:13],value[29:]+value[13:29],None)
    del key,cipher_rows
    run(['/usr/bin/dotnet',str(OUTPUT/'smoke/EsProductionSmoke.dll'),'--tls-only'])
    stage('preconditions-passed',existing_inventory_decryptable=True,approved_authority_and_tls=True)
    stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    BACKUP=Path('/var/backups/turborama-suite')/('es-suite-34e31f2-'+stamp)
    BACKUP.mkdir(parents=True,mode=0o700);os.chmod(BACKUP,0o700)
    REPORT['backup']=str(BACKUP)
    shutil.copy2(ARTIFACT,BACKUP/'reviewed-artifact.zip');os.chmod(BACKUP/'reviewed-artifact.zip',0o600)
    require(digest(BACKUP/'reviewed-artifact.zip')==ARTIFACT_SHA,'Protected artifact copy failed checksum')
    if not RELEASE.exists():
        RELEASE.mkdir(mode=0o755);os.chmod(RELEASE,0o755)
        with zipfile.ZipFile(BACKUP/'reviewed-artifact.zip') as archive:
            require(len(archive.infolist())<=100 and sum(x.file_size for x in archive.infolist())<50*1024*1024,'Unexpected artifact shape')
            for item in archive.infolist():
                require(not Path(item.filename).is_absolute() and '..' not in Path(item.filename).parts and not stat.S_ISLNK(item.external_attr>>16),'Unsafe artifact entry')
                archive.extract(item,RELEASE)
    actual=set()
    for path in RELEASE.rglob('*'):
        if path.relative_to(RELEASE).parts[0]=='deployment-checks':
            require(previous is not None and not path.is_symlink(),'Unexpected retained operator check')
            continue
        require(not path.is_symlink(),'Unexpected release symlink')
        os.chmod(path,0o755 if path.is_dir() else 0o644)
        if path.is_file(): actual.add(path.relative_to(RELEASE).as_posix())
    manifest=set()
    for line in (RELEASE/'SHA256SUMS.txt').read_text().splitlines():
        sha,name=line.split(maxsplit=1);name=name.removeprefix('./');manifest.add(name)
        require(digest(RELEASE/name)==sha,'Internal artifact checksum mismatch: '+name)
    require(actual==manifest|{'SHA256SUMS.txt'} and len(manifest)==58,'Internal artifact manifest is incomplete')
    require((RELEASE/'COMMIT.txt').read_text().strip()==COMMIT,'Artifact commit mismatch')
    expected_baseline=sorted(p.name.removesuffix('.up.sql') for p in (RELEASE/'migrations').glob('*.up.sql') if int(p.name[:3])<=21)
    applied=query('SELECT version FROM suite.schema_migrations ORDER BY version').splitlines()
    new_versions=sorted(p.name.removesuffix('.up.sql') for p in (RELEASE/'migrations').glob('*.up.sql') if 22<=int(p.name[:3])<=25)
    require(21<=len(applied)<=25 and applied==expected_baseline+new_versions[:len(applied)-21],
        'Expected baseline 001–021 plus a contiguous prefix of reviewed migrations; inspect schema drift')
    with tarfile.open(BACKUP/'configuration-and-pix-state.tar.gz','w:gz') as tar:
        for path in sorted(Path('/etc').glob('turborama*')):
            if path.is_dir(): tar.add(path,arcname=str(path).lstrip('/'))
        for unit in UNITS:
            for path in [Path('/etc/systemd/system')/unit,Path('/etc/systemd/system')/(unit+'.d')]:
                tar.add(path,arcname=str(path).lstrip('/'))
        tar.add('/var/lib/turborama-pix',arcname='var/lib/turborama-pix')
    with open(BACKUP/'suite-before.dump','xb') as dump:
        p=subprocess.run(['/usr/sbin/runuser','-u','postgres','--','/usr/bin/pg_dump','-Fc','--schema=suite','--lock-wait-timeout=5000','--dbname=postgres'],stdout=dump,stderr=subprocess.PIPE,timeout=120)
        require(p.returncode==0,'Consistent Suite backup failed')
    require(len(run(['/usr/bin/pg_restore','--list',str(BACKUP/'suite-before.dump')]).splitlines())>20,'Backup catalog is incomplete')
    snapshots={name:digest(BACKUP/name) for name in ['suite-before.dump','configuration-and-pix-state.tar.gz']}
    (BACKUP/'SHA256SUMS.txt').write_text(''.join(sha+'  '+name+'\n' for name,sha in snapshots.items()))
    (BACKUP/'before-units.json').write_text(json.dumps(before,indent=2)+'\n')
    REPORT['backup_sha256']=snapshots
    rollback=BACKUP/'rollback.sh'
    rollback.write_text('#!/usr/bin/env bash\nset -Eeuo pipefail\n[[ $EUID -eq 0 ]]\n'+''.join('rm -f -- '+str(p)+'\n' for p in DROPINS.values())+'systemctl daemon-reload\nsystemctl restart '+' '.join(UNITS)+'\nsystemctl is-active '+' '.join(UNITS)+'\n')
    os.chmod(rollback,0o700);REPORT['rollback_command']='pkexec '+str(rollback)
    stage('backup-and-rollback-ready',backup_verified=True)
    migrations=[];already_present=[]
    for path in sorted((RELEASE/'migrations').glob('*.up.sql')):
        if int(path.name[:3])<22: continue
        version=path.name.removesuffix('.up.sql')
        if version in applied:
            prior_hashes={item['version']:item['sha256'] for item in previous.get('migrations_applied',[])} if previous else {}
            require(prior_hashes.get(version)==digest(path),'Applied migration lacks matching protected deployment evidence: '+version)
            already_present.append({'version':version,'sha256':digest(path)})
            stage('reviewed-migration-preserved',version=version)
            continue
        run(['/usr/sbin/runuser','-u','postgres','--','/usr/bin/psql','-X','-v','ON_ERROR_STOP=1','-d','postgres','-f','-'],input=path.read_text(),timeout=45)
        migrations.append({'version':path.name.removesuffix('.up.sql'),'sha256':digest(path)})
        stage('migration-applied',version=migrations[-1]['version'])
    REPORT['migrations_applied']=migrations
    REPORT['migrations_already_present']=already_present
    require(len(query('SELECT version FROM suite.schema_migrations').splitlines())==25,'Final migration verification failed')
    try:
        for unit in UNITS:
            prefix='/usr/bin/env SUITE_ADMIN_PEPPER_FILE=/run/credentials/turborama-suite-admin.service/activation-pepper ' if unit=='turborama-suite-admin.service' else ''
            content='[Service]\nExecStart=\nExecStart='+prefix+'/usr/bin/dotnet '+str(NEW[unit])+'\n'
            if unit=='turborama-suite-api.service':
                content+='Environment=Suite__EmulationStation__Enabled=true\nEnvironment=Suite__Inventory__Enabled=true\nEnvironment=Suite__Inventory__EncryptionKeyFile=/run/credentials/turborama-suite-api.service/inventory-encryption-key\nLoadCredential=inventory-encryption-key:/etc/turborama-suite/inventory-encryption-key\nEnvironment=Suite__NetworkInventory__Enabled=true\nEnvironment=Suite__NetworkInventory__RetentionDays=30\n'
            DROPINS[unit].write_text(content);os.chmod(DROPINS[unit],0o644)
        run(['/usr/bin/systemctl','daemon-reload'])
        for unit in UNITS:
            run(['/usr/bin/systemctl','restart',unit]);wait_healthy(unit)
            info,env=process(unit);require(str(NEW[unit]) in info['argv'],'New executable was not loaded: '+unit)
            require(info['WorkingDirectory']==before[unit]['WorkingDirectory'],'Existing content root changed')
            for name,value in before_env[unit].items():
                if name.startswith(('Suite__','ConnectionStrings__','SUITE_','TURBORAMA_')) and name not in ('Suite__Inventory__Enabled','Suite__EmulationStation__Enabled','Suite__Inventory__EncryptionKeyFile','Suite__NetworkInventory__Enabled','Suite__NetworkInventory__RetentionDays'):
                    require(env.get(name)==value,'Existing protected configuration changed: '+name)
            if unit=='turborama-suite-api.service':
                for name in ['Suite__Enabled','Suite__EmulationStation__Enabled','Suite__Inventory__Enabled','Suite__NetworkInventory__Enabled']:
                    require(env.get(name)=='true','Feature flag was not loaded: '+name)
            stage('component-healthy',unit=unit,binary_sha256=digest(NEW[unit]))
        for base in ['http://127.0.0.1:5190','https://app.lzgames.com.br']:
            probe(base,'/v1/suite/challenges','INVALID','CLIENT_SCOPE_INVALID')
            probe(base,'/v1/suite/challenges','EMULATIONSTATION','JSON_INVALID')
            probe(base,'/v1/suite/challenges',None,'JSON_INVALID')
            probe(base,'/v1/suite/emulationstation/challenges',None,'JSON_INVALID')
            probe(base,'/v1/suite/network/challenges','EMULATIONSTATION','CLIENT_SCOPE_INVALID')
        stage('origin-and-public-routes-verified',proxy_changed=False)
        checks=RELEASE/'deployment-checks'
        if checks.exists():
            require(previous is not None and checks.is_dir() and not checks.is_symlink() and checks.stat().st_uid==0,'Unexpected operator check directory')
            shutil.rmtree(checks)
        shutil.copytree(OUTPUT/'smoke',checks)
        shutil.copyfile('/etc/turborama-suite/online-spki.der',checks/'approved-online-spki.der')
        for path in [checks,*checks.rglob('*')]: os.chmod(path,0o755 if path.is_dir() else 0o644)
        smoke=run(['/usr/bin/prlimit','--core=0','--','/usr/sbin/runuser','-u','postgres','-g','turborama-suite-bff','--','/usr/bin/dotnet',str(checks/'EsProductionSmoke.dll'),'--production-smoke'],timeout=90)
        result=json.loads(smoke.splitlines()[-1]);require(result['status']=='passed','Production synthetic smoke failed')
        REPORT['smoke']=result;stage('production-cryptographic-smoke-passed')
        for unit in UNITS: health(unit)
        REPORT['status']='deployed';stage('deployment-complete',windows_homologation='pending')
    except BaseException:
        REPORT['status']='rolling-back';save_report()
        try:
            run(['/usr/bin/bash',str(rollback)],timeout=60)
            for unit in UNITS: wait_healthy(unit)
            REPORT['status']='rolled-back';stage('previous-services-restored')
        except Exception:
            REPORT['status']='rollback-needs-attention';save_report()
        raise

if __name__=='__main__':
    try: main()
    except BaseException as error:
        REPORT['error_type']=type(error).__name__;REPORT['error']=str(error)
        if REPORT['status']=='preparing': REPORT['status']='stopped-before-service-switch'
        save_report();print('DEPLOYMENT STOPPED: '+str(error),file=sys.stderr,flush=True);sys.exit(1)
