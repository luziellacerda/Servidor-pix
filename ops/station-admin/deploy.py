#!/usr/bin/env python3
"""Publish only Station management, with a restored backup and bounded rollback.

Run through native Linux authentication. Never print credentials or customer rows.
The additive Station migration stays in place when rolling back the application.
"""
import argparse,base64,hashlib,http.client,json,os,pwd,re,shutil,socket,subprocess,tempfile,time
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.error import HTTPError

ROOT=Path(__file__).resolve().parents[2]
SITE=Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902')
FILES=('station-admin.php','station-admin-policy.php','station-admin.css','station-admin.js','station-lib.php')
SERVICE='turborama-station-management.service'
HELPER='turborama-station-issue-admin.service'
BACKUP=Path('/mnt/DADOS/station-admin-panel-backup-20261003')
UNIT=Path('/etc/systemd/system')/SERVICE
DROPIN=Path('/etc/systemd/system/turborama-station-issue-admin.service.d/zz-station-management-20261003.conf')
CONFIG=Path('/etc/turborama-suite/station-management.env')
TOKEN=Path('/etc/turborama-suite/station-management.token')
IPC='/run/turborama-station-management/admin.sock'
MIGRATION='030_station_management_audit'
SHARED=('turborama-station-api.service','turborama-pix.service','turborama-suite-api.service',
        'turborama-suite-admin.service','turborama-suite-content-gateway.service','nginx.service',
        'cloudflared.service','postgresql@16-main.service','redis-server.service','turbobox-php-fpm.service')
INDEX=Path('/mnt/DADOS/turbostation-releases/station-20261003-fd13c0d/content/index.json')
INDEX_SHA='5b460a6f9866e30a5a5b4dad24652c512b1187b3df01244e6af9308ae6b18342'

def run(args,timeout=60,**options):
    result=subprocess.run(args,capture_output=True,text=True,timeout=timeout,**options)
    if result.returncode:raise RuntimeError('bounded command failed: '+Path(args[0]).name)
    return result.stdout
def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for b in iter(lambda:f.read(1048576),b''):h.update(b)
    return h.hexdigest()
def write(path,value,mode=0o600,uid=0,gid=0):
    if path.is_symlink():raise ValueError('refusing a symbolic link')
    fd,name=tempfile.mkstemp(prefix='.station-publish-',dir=path.parent)
    try:
        os.fchmod(fd,mode);os.fchown(fd,uid,gid)
        with os.fdopen(fd,'w') as f:f.write(value);f.flush();os.fsync(f.fileno())
        os.replace(name,path)
    finally:Path(name).unlink(missing_ok=True)
def state(unit):
    fields=run(['systemctl','show',unit,'-p','MainPID','-p','ActiveState']).strip()
    data=dict(x.split('=',1) for x in fields.splitlines())
    return {'pid':int(data['MainPID']),'active':data['ActiveState']}
def environment(unit):
    pid=state(unit)['pid']
    if pid<=0:raise ValueError('required service is stopped')
    return dict(x.decode().split('=',1) for x in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0') if b'=' in x)
def db_identity():
    connection=environment('turborama-suite-admin.service')['SUITE_ADMIN_CONNECTION']
    parts={k.strip().lower():v.strip() for k,v in (p.split('=',1) for p in connection.split(';') if '=' in p)}
    api=environment('turborama-station-api.service')['ConnectionStrings__SuiteStore']
    api_parts={k.strip().lower():v.strip() for k,v in (p.split('=',1) for p in api.split(';') if '=' in p)}
    db=parts.get('database','')
    if not re.fullmatch('[A-Za-z0-9_-]+',db) or db!=api_parts.get('database') or parts.get('username')!='turborama-suite-admin' or parts.get('host')!='/var/run/postgresql' or parts.get('password') or parts.get('port','5432')!='5432':
        raise ValueError('database identity differs from the peer-authenticated target')
    return db,connection
def sql(db,statement):
    env={k:v for k,v in os.environ.items() if not k.startswith('PG')}
    return run(['runuser','-u','postgres','--','psql','-X','-qAt','-h','/var/run/postgresql','-p','5432',
                '-v','ON_ERROR_STOP=1','-d',db,'-c',statement],env=env)
def management(method,path,body=None,claim='station.licenses.read',controls=False):
    class Unix(http.client.HTTPConnection):
        def connect(self):self.sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM);self.sock.settimeout(5);self.sock.connect(IPC)
    headers={'X-Suite-Admin-Token':TOKEN.read_text().strip(),'X-Suite-Admin-Actor':'station-publish-check',
             'X-Suite-Admin-Claims':claim,'X-Suite-Client-Ip-Digest':hashlib.sha256(b'station-publish-check').hexdigest(),
             'Content-Type':'application/json'}
    if controls:headers.update({'X-Suite-Csrf-Verified':'1','X-Suite-Step-Up-At':str(int(time.time()))})
    c=Unix('localhost')
    try:
        c.request(method,path,None if body is None else json.dumps(body),headers)
        r=c.getresponse();data=r.read(1048576);return r.status,json.loads(data) if data else {}
    finally:c.close()
def health():
    until=time.monotonic()+30
    while time.monotonic()<until:
        try:
            if management('GET','/readiness')[0]==200:
                with urlopen('http://127.0.0.1:5194/health',timeout=2) as r:
                    if r.status==200:return
        except Exception:pass
        time.sleep(.3)
    raise ValueError('Station management readiness did not pass')
def restore_check():
    if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.'):
        raise ValueError('backup verification requires a temporary cluster')
    if not run(['psql','-X','-qAt','-c','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('refusing a restore into production')
    run(['createdb','station_panel_restore'])
    run(['pg_restore','--exit-on-error','--no-owner','--no-acl','-d','station_panel_restore',str(BACKUP/'database.dump')],timeout=300)
    count=run(['psql','-X','-qAt','-d','station_panel_restore','-c',
      "SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants')"]).strip()
    if count!='2':raise ValueError('restored ledger differs')
    print(json.dumps({'databaseRestoreVerified':True}),flush=True)
def rollback():
    saved=json.loads((BACKUP/'state.json').read_text())
    for name,meta in saved['site'].items():
        target=SITE/name
        if target.exists() and digest(target) not in {meta['sha256'],saved['newSite'][name]}:
            raise ValueError('Station page changed after publication')
        if meta['exists']:
            write(target,(BACKUP/'site'/name).read_text(),meta['mode'],meta['uid'],meta['gid'])
        else:target.unlink(missing_ok=True)
    for path in (DROPIN,UNIT,CONFIG):
        if path.exists():
            expected=saved.get('installedConfigs',{}).get(str(path))
            if not expected or digest(path)!=expected:raise ValueError('Station configuration changed after publication')
    run(['systemctl','disable','--now',SERVICE])
    for path in (DROPIN,UNIT,CONFIG):path.unlink(missing_ok=True)
    run(['systemctl','daemon-reload']);run(['systemctl','restart',HELPER])
    if saved.get('fpmReload'):run(['systemctl','kill','--kill-who=main','--signal=USR2','turbobox-php-fpm.service'])
    until=time.monotonic()+20
    while time.monotonic()<until:
        try:
            with urlopen('http://127.0.0.1:5194/health',timeout=2) as r:
                if r.status==200:break
        except Exception:pass
        time.sleep(.2)
    else:raise ValueError('original Station helper failed health')
    write(BACKUP/'rollback.json',json.dumps({'rolledBack':True,'additiveSchemaPreserved':True})+'\n')
    print(json.dumps({'rolledBack':True,'additiveSchemaPreserved':True}),flush=True)
def validate_package(package):
    manifest=json.loads((package/'manifest.json').read_text())
    revision=manifest['sourceRevision']
    if not re.fullmatch('[0-9a-f]{40}',revision) or run(['runuser','-u','lz-servidor','--','git','-C',str(ROOT),'rev-parse','HEAD']).strip()!=revision:
        raise ValueError('source revision differs from the prepared artifact')
    if any(p.is_symlink() for p in package.rglob('*')):raise ValueError('candidate contains a symbolic link')
    actual_files={p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file() and p.name!='manifest.json'}
    if actual_files!=set(manifest['files']):raise ValueError('candidate file manifest differs')
    for path,sha in manifest['files'].items():
        candidate=package/path
        if Path(path).is_absolute() or '..' in Path(path).parts or candidate.is_symlink() or digest(candidate)!=sha:raise ValueError('candidate hash differs')
    if set(p.name for p in (package/'site').iterdir())!=set(FILES):raise ValueError('site file allowlist differs')
    return manifest
def copy_release(package,target):
    if target.exists():raise ValueError('immutable release already exists')
    target.mkdir(mode=0o755);target.chmod(0o755)
    shutil.copytree(package/'backend',target/'backend');shutil.copytree(package/'site',target/'site')
    for name in ('station-issue-admin.py','030_station_management_audit.up.sql','manifest.json'):shutil.copy2(package/name,target/name)
    for p in target.rglob('*'):os.chown(p,0,0);p.chmod(0o755 if p.is_dir() else 0o644)
def resume(package):
    manifest=validate_package(package);saved=json.loads((BACKUP/'state.json').read_text())
    if not saved.get('databaseRestoreVerified') or not saved.get('migrationApplied'):
        raise ValueError('a restored and migrated preparation is required')
    if (BACKUP/'result.json').exists() and json.loads((BACKUP/'result.json').read_text()).get('applied'):
        raise ValueError('publication already completed; do not repeat it')
    if any(p.exists() for p in (UNIT,DROPIN,CONFIG)) or state(SERVICE)['pid']!=0:
        raise ValueError('the previous application must be fully rolled back')
    for name,meta in saved['site'].items():
        target=SITE/name
        if target.exists()!=meta['exists'] or target.exists() and digest(target)!=meta['sha256']:
            raise ValueError('original Station page changed after rollback')
    db,connection=db_identity()
    checksum=sql(db,"SELECT script_sha256 FROM suite.schema_migration_checksums WHERE version='030_station_management_audit'").strip()
    if checksum!=manifest['files']['030_station_management_audit.up.sql']:raise ValueError('installed Station migration checksum differs')
    if any(state(u)!=s for u,s in saved['baseline'].items()) or digest(INDEX)!=INDEX_SHA:
        raise ValueError('shared runtime changed after preparation')
    saved.setdefault('initialPreparationRevision',saved['sourceRevision'])
    saved['sourceRevision']=manifest['sourceRevision'];saved['newSite']={n:manifest['files']['site/'+n] for n in FILES}
    target=Path('/opt/turborama-station-management-20261003-'+manifest['sourceRevision'][:7]);saved['release']=str(target)
    copy_release(package,target)
    publish_prepared(package,saved,db,connection)
def apply(package):
    manifest=validate_package(package);revision=manifest['sourceRevision']
    db,connection=db_identity()
    if any(state(u)['active']!='active' or state(u)['pid']<=0 for u in SHARED+(HELPER,)):
        raise ValueError('a required existing service is not healthy')
    if digest(INDEX)!=INDEX_SHA:raise ValueError('Station content index changed')
    if sql(db,"SELECT count(*) FROM suite.schema_migrations WHERE version='030_station_management_audit'").strip()!='0':
        raise ValueError('management migration is already installed; do not repeat publication')
    for path in (BACKUP,UNIT,DROPIN,CONFIG,TOKEN):
        if path.exists():raise ValueError('a publication destination already exists')
    target=Path('/opt/turborama-station-management-20261003-'+revision[:7])
    if target.exists():raise ValueError('immutable release already exists')
    old_helper=Path('/opt/turborama-station-20261001/ops/station-issue-admin.py')
    actual=run(['systemctl','show',HELPER,'-p','ExecStart','--value'])
    if str(old_helper) not in actual:raise ValueError('existing helper differs from the expected release')
    for name in FILES:
        if (SITE/name).is_symlink():raise ValueError('Station site target is a link')
    print(json.dumps({'stage':'preflight_passed','sourceRevision':revision}),flush=True)
    BACKUP.mkdir(mode=0o700);(BACKUP/'site').mkdir(mode=0o700)
    saved={'sourceRevision':revision,'baseline':{u:state(u) for u in SHARED},'helperBefore':state(HELPER),
           'site':{},'newSite':{},'release':str(target),'indexSha256':INDEX_SHA,'installedConfigs':{}}
    for name in FILES:
        p=SITE/name;exists=p.exists();s=p.stat() if exists else (SITE/'station-admin.php').stat()
        saved['site'][name]={'exists':exists,'sha256':digest(p) if exists else None,'mode':s.st_mode&0o777,'uid':s.st_uid,'gid':s.st_gid}
        saved['newSite'][name]=digest(package/'site'/name)
        if exists:shutil.copy2(p,BACKUP/'site'/name)
    shutil.copy2(old_helper,BACKUP/'original-helper.py')
    shutil.copy2(Path('/etc/systemd/system')/HELPER,BACKUP/'original-helper.service')
    write(BACKUP/'state.json',json.dumps(saved,indent=2)+'\n')
    with (BACKUP/'database.dump').open('xb') as f:
        os.fchmod(f.fileno(),0o600)
        r=subprocess.run(['runuser','-u','postgres','--','pg_dump','-h','/var/run/postgresql','-p','5432','--format=custom','-d',db],
          stdout=f,stderr=subprocess.PIPE,timeout=300,env={k:v for k,v in os.environ.items() if not k.startswith('PG')})
        if r.returncode:raise ValueError('database backup failed')
        f.flush();os.fsync(f.fileno())
    restored=run(['pg_virtualenv','-t','/usr/bin/python3',str(Path(__file__).resolve()),'--restore-backup'],timeout=360)
    if '"databaseRestoreVerified": true' not in restored:raise ValueError('database restore gate failed')
    saved['databaseRestoreVerified']=True;write(BACKUP/'state.json',json.dumps(saved,indent=2)+'\n')
    print(json.dumps({'stage':'backup_restored','databaseRestoreVerified':True}),flush=True)
    # Privately inspect opcode cache policy; never copy environment or passwords to output.
    fpm_paths=list(Path('/etc/php/8.3/turbobox-fpm').rglob('*.conf'))+[Path('/etc/php/8.3/fpm/php.ini')]
    saved['fpmReload']=any(re.search(r'(?m)^\s*(?:php_admin_(?:value|flag)\[)?opcache\.validate_timestamps\]?\s*=\s*(?:0|off|false)\s*$',p.read_text(),re.I) for p in fpm_paths if p.is_file())
    copy_release(package,target)
    migration=target/'030_station_management_audit.up.sql'
    run(['runuser','-u','postgres','--','psql','-X','-q','-h','/var/run/postgresql','-p','5432','-v','ON_ERROR_STOP=1',
         '-v','migration_sha256='+digest(migration),'-d',db,'-f',str(migration)])
    saved['migrationApplied']=True
    publish_prepared(package,saved,db,connection)
def publish_prepared(package,saved,db,connection):
    target=Path(saved['release']);revision=saved['sourceRevision'];migration=target/'030_station_management_audit.up.sql'
    admin=pwd.getpwnam('turborama-suite-admin');helper=pwd.getpwnam('turborama-suite')
    if not TOKEN.exists():write(TOKEN,base64.b64encode(os.urandom(32)).decode()+'\n',0o640,admin.pw_uid,helper.pw_gid)
    else:
        if TOKEN.is_symlink() or TOKEN.stat().st_uid!=admin.pw_uid or TOKEN.stat().st_gid!=helper.pw_gid or TOKEN.stat().st_mode&0o777!=0o640:
            raise ValueError('private management token permissions differ')
    credential='/run/credentials/'+SERVICE+'/station-activation-pepper'
    config='\n'.join(['SUITE_ADMIN_SOCKET='+IPC,'SUITE_ADMIN_TOKEN_FILE='+str(TOKEN),'SUITE_COMMERCE_TOKEN_FILE='+str(TOKEN),
      'SUITE_ADMIN_CONNECTION="'+connection+'"','SUITE_ADMIN_PEPPER_FILE='+credential,
      'STATION_ADMIN_PEPPER_FILE='+credential,'SUITE_COMMERCE_ENABLED=1',
      'STATION_COMMERCE_ENABLED=1','STATION_MANAGEMENT_ONLY=1','SUITE_CONTENT_ADMIN_ENABLED=0'])+'\n'
    write(CONFIG,config,0o640,0,helper.pw_gid)
    unit=f'''[Unit]
Description=TurboRama Station license and device management (private Unix socket)
After=postgresql.service
[Service]
Type=simple
User=turborama-suite-admin
Group=turborama-suite
SupplementaryGroups=turborama-suite-pepper
WorkingDirectory={target}/backend
EnvironmentFile={CONFIG}
LoadCredential=station-activation-pepper:/etc/turborama-suite/station-activation-pepper
ExecStart=/usr/bin/dotnet {target}/backend/TurboRamaSuiteAdminServer.dll
RuntimeDirectory=turborama-station-management
RuntimeDirectoryMode=0750
UMask=0077
Restart=on-failure
RestartSec=3s
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
RestrictAddressFamilies=AF_UNIX
[Install]
WantedBy=multi-user.target
'''
    write(UNIT,unit,0o644)
    DROPIN.parent.mkdir(mode=0o755,exist_ok=True)
    write(DROPIN,f'[Unit]\nAfter={SERVICE}\nWants={SERVICE}\n[Service]\nExecStart=\nExecStart=/usr/bin/python3 {target}/station-issue-admin.py\nEnvironment=STATION_MANAGEMENT_SOCKET={IPC}\nEnvironment=STATION_MANAGEMENT_TOKEN_FILE={TOKEN}\n',0o644)
    saved['installedConfigs']={str(p):digest(p) for p in (CONFIG,UNIT,DROPIN)}
    write(BACKUP/'state.json',json.dumps(saved,indent=2)+'\n')
    activated=False
    try:
        if any(state(u)!=s for u,s in saved['baseline'].items()) or digest(INDEX)!=INDEX_SHA:
            raise ValueError('shared runtime changed before activation')
        run(['systemd-analyze','verify',str(UNIT)],timeout=30)
        activated=True
        run(['systemctl','daemon-reload']);run(['systemctl','enable','--now',SERVICE]);run(['systemctl','restart',HELPER])
        health()
        print(json.dumps({'stage':'management_ready','migrationApplied':True}),flush=True)
        # Install PHP policy/library before rendering the new page; this does not touch shared site files.
        for name in ('station-admin-policy.php','station-lib.php','station-admin.css','station-admin.js','station-admin.php'):
            meta=saved['site'][name];write(SITE/name,(package/'site'/name).read_text(),meta['mode'],meta['uid'],meta['gid'])
        if saved['fpmReload']:run(['systemctl','kill','--kill-who=main','--signal=USR2','turbobox-php-fpm.service'])
        time.sleep(3)
        from production_checks import verify
        tests=verify(db,sql,environment)
        after={u:state(u) for u in SHARED}
        if any(after[u]!=saved['baseline'][u] for u in SHARED) or digest(INDEX)!=INDEX_SHA:
            raise ValueError('an unrelated runtime changed during publication')
        result={'applied':True,'sourceRevision':revision,'dllSha256':digest(target/'backend/TurboRamaSuiteAdminServer.dll'),
                'siteSha256':saved['newSite'],'helperSha256':digest(target/'station-issue-admin.py'),
                'migrationSha256':digest(migration),'databaseRestoreVerified':True,'migrationApplied':True,
                'deploymentScriptSha256':digest(Path(__file__)),
                'sharedPidsUnchanged':True,'contentIndexUnchanged':True,'fpmReload':saved['fpmReload'],
                'management':state(SERVICE),'helper':state(HELPER),'productionChecks':tests}
        write(BACKUP/'result.json',json.dumps(result,indent=2)+'\n')
        public=Path('/home/lz-servidor/station-admin-panel-result-20261003.json')
        owner=pwd.getpwnam('lz-servidor');write(public,json.dumps(result,indent=2)+'\n',0o600,owner.pw_uid,owner.pw_gid)
        print(json.dumps({'stage':'published','site':'https://turbobox.lzgames.com.br/admin/station','verified':True}),flush=True)
    except Exception as failure:
        write(BACKUP/'failure.json',json.dumps({'errorType':type(failure).__name__,
             'reason':str(failure) if isinstance(failure,ValueError) else 'private production verification failed'})+'\n')
        if activated:rollback()
        raise

def main():
    p=argparse.ArgumentParser();m=p.add_mutually_exclusive_group(required=True)
    m.add_argument('--package',type=Path);m.add_argument('--resume',type=Path);m.add_argument('--rollback',action='store_true');m.add_argument('--restore-backup',action='store_true')
    a=p.parse_args()
    if os.geteuid()!=0:raise ValueError('native Linux authorization is required')
    if a.restore_backup:restore_check()
    elif a.rollback:rollback()
    elif a.resume:resume(a.resume.resolve())
    else:apply(a.package.resolve())
if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'completed':False,'errorType':type(error).__name__,'reason':str(error) if isinstance(error,ValueError) else 'bounded operation failed; inspect private backup'}),flush=True)
        raise SystemExit(1)
