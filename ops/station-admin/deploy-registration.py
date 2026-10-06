#!/usr/bin/env python3
"""Guarded additive Station registration rollout; native Linux root authentication.

Only the Station management/helper restart. No PostgreSQL migration, payment,
API, proxy, credential, APK or media change. Rollback preserves issued licenses
and the additive SQLite receipt table; it never restores a live database.
"""
import argparse,fcntl,hashlib,importlib.util,json,os,pwd,re,shutil,sqlite3,stat,subprocess,tempfile,time
from datetime import datetime,timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
SITE=Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902')
NAMES={'station-admin.php','station-admin.js','station-admin.css','station-registration.php'}
SERVICE='turborama-station-management.service'
HELPER='turborama-station-issue-admin.service'
OLD=Path('/opt/turborama-station-management-20261003-deda92c')
OLD_DLL='15b5a2c94562f8a915c1cb46d7c2b4e3246febdc438473992931b93f88684260'
OLD_HELPER='32f03a3a34e8be106fd91bd1b85dcdc5f45ff55cb719c4d93ab1932125b6c01b'
API=Path('/opt/turborama-station-community-r41-20261006-a2bb176/TurboRamaSuiteOnlineServer.dll')
API_SHA='d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13'
INDEX=Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
INDEX_SHA='07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92'
DROPINS={u:Path('/etc/systemd/system')/(u+'.d')/'zzzz-station-registration-20261006.conf' for u in (SERVICE,HELPER)}
RESULT=Path('/home/lz-servidor/station-registration-result-20261006.json')


def load(path,name):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


ops=load(Path(__file__).with_name('deploy.py'),'station_registration_ops')


def sha(path):return ops.digest(path)
def current(path):return sha(path) if path.exists() else None
def files(folder):return {p.relative_to(folder).as_posix():sha(p) for p in folder.rglob('*') if p.is_file()}
def private(path,value):ops.write(path,json.dumps(value,indent=2)+'\n')


def snapshot(database,ids=None):
    # Volatile session last-contact fields are excluded; all real entitlement/verifier rows are retained.
    if ids is not None and any(not re.fullmatch(r'STA-[A-Z0-9_-]{6,64}',value) for value in ids):raise ValueError('Unexpected baseline license identifier')
    scope="l.product_id='TURBORAMA_STATION_ANDROID'"+(' AND l.license_id IN ('+','.join("'"+value+"'" for value in ids)+')' if ids else (' AND false' if ids is not None else ''))
    rows=ops.sql(database,"SELECT row_to_json(l)::text FROM suite.suite_licenses l WHERE "+scope+" ORDER BY license_id")
    return dict(sha256=hashlib.sha256(rows.encode()).hexdigest(),ids=[json.loads(row)['license_id'] for row in rows.splitlines()])


def users_snapshot(ids=None):
    with sqlite3.connect('file:'+str(SITE/'.data/turbobox.sqlite')+'?mode=ro',uri=True) as db:
        rows=db.execute('SELECT * FROM users'+(' WHERE id IN ('+','.join('?' for _ in ids)+')' if ids else (' WHERE false' if ids is not None else ''))+' ORDER BY id',ids or []).fetchall()
    return dict(sha256=hashlib.sha256(json.dumps(rows).encode()).hexdigest(),ids=[row[0] for row in rows])


def guard(state):
    if any(ops.state(u)!=value for u,value in state['shared'].items()):raise ValueError('An unrelated runtime changed')
    if sha(API)!=API_SHA or sha(INDEX)!=INDEX_SHA:raise ValueError('Station API or catalogue changed')
    if any(sha(Path(name))!=value for name,value in state['unchanged'].items()):raise ValueError('An unrelated file changed')


def validate(package):
    manifest=json.loads((package/'manifest.json').read_text());revision=manifest['sourceRevision']
    if not re.fullmatch('[a-f0-9]{40}',revision):raise ValueError('Exact committed source required')
    if ops.run(['runuser','-u','lz-servidor','--','git','-C',str(ROOT),'rev-parse','HEAD']).strip()!=revision:
        raise ValueError('Source no longer matches the artifact')
    if ops.run(['runuser','-u','lz-servidor','--','git','-C',str(ROOT),'status','--porcelain']).strip():raise ValueError('Source is not clean')
    if any(p.is_symlink() for p in package.rglob('*')):raise ValueError('Package contains a link')
    actual={p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file() and p!=package/'manifest.json'}
    if actual!=set(manifest['files']):raise ValueError('Artifact file list differs')
    if set(p.name for p in (package/'site').iterdir())!=NAMES:raise ValueError('Site scope differs')
    if manifest['targetSite']!=str(SITE) or set(manifest['beforeSite'])!=NAMES:raise ValueError('Site target differs')
    for name,value in manifest['files'].items():
        if Path(name).is_absolute() or '..' in Path(name).parts or sha(package/name)!=value:raise ValueError('Artifact hash differs')
    if manifest['files']['deploy-registration.py']!=sha(Path(__file__)):raise ValueError('Installer differs from reviewed source')
    for name in NAMES:
        target=SITE/name
        if target.is_symlink() or current(target)!=manifest['beforeSite'][name]:raise ValueError('The installed Station UI changed')
    if sha(OLD/'backend/TurboRamaSuiteAdminServer.dll')!=OLD_DLL or sha(OLD/'station-issue-admin.py')!=OLD_HELPER:
        raise ValueError('The old administration release changed')
    for unit in (SERVICE,HELPER):
        start=ops.run(['systemctl','show',unit,'-p','ExecStart','--value'])
        if str(OLD) not in start or DROPINS[unit].exists():raise ValueError('A successor administration release is installed')
    if sha(API)!=API_SHA or sha(INDEX)!=INDEX_SHA:raise ValueError('The inspected R41 API differs')
    return manifest


def restore_check(backup):
    if backup.parent!=Path('/mnt/DADOS') or not backup.name.startswith('station-registration-backup-20261006-'):
        raise ValueError('Unexpected backup path')
    if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.') or not ops.run(
        ['psql','-X','-Atqc','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('Temporary PostgreSQL cluster required')
    ops.run(['createdb','station_registration_restore'])
    ops.run(['pg_restore','--exit-on-error','--no-owner','--no-acl','-d','station_registration_restore',str(backup/'database.dump')],timeout=300)
    ledger=ops.run(['psql','-X','-qAt','-d','station_registration_restore','-c',
        "SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()
    if ledger!='3':raise ValueError('Restored Station ledger differs')
    print('STATION REGISTRATION BACKUP RESTORED',flush=True)


def rollback(backup):
    saved=json.loads((backup/'state.json').read_text())
    if saved['site']!=str(SITE) or set(saved['files'])!=NAMES:raise ValueError('Unexpected rollback scope')
    # Verify the entire rollback before mutating any file; reject later releases.
    for name,meta in saved['files'].items():
        if current(SITE/name) not in {meta['before'],meta['after']}:raise ValueError('Later Station UI blocks rollback')
        if meta['before'] and sha(backup/'site'/name)!=meta['before']:raise ValueError('Backup file changed')
    for unit,path in DROPINS.items():
        if path.exists() and sha(path)!=saved['dropinSha'][unit]:raise ValueError('Later administration config blocks rollback')
    for name in ('station-admin.php','station-admin.js','station-admin.css','station-registration.php'):
        meta=saved['files'][name]
        if current(SITE/name)==meta['after']:
            if meta['before'] is None:(SITE/name).unlink()
            else:ops.write(SITE/name,(backup/'site'/name).read_text(),meta['mode'],meta['uid'],meta['gid'])
    for path in DROPINS.values():path.unlink(missing_ok=True)
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.run(['systemctl','restart',HELPER]);ops.health()
    guard(saved)
    saved['status']='rolled-back';saved['rolledBackAt']=datetime.now(timezone.utc).isoformat();private(backup/'state.json',saved)
    print(json.dumps({'status':'rolled-back','licensesPreserved':True,'sqliteReceiptsPreserved':True}),flush=True)


def apply(package):
    manifest=validate(package);revision=manifest['sourceRevision']
    target=Path('/opt/turborama-station-management-20261006-'+revision[:7])
    backup=Path('/mnt/DADOS/station-registration-backup-20261006-'+revision[:7])
    if target.exists() or backup.exists():raise ValueError('Release or backup already exists; inspect the previous attempt')
    database,_=ops.db_identity();shared={u:ops.state(u) for u in ops.SHARED}
    if any(value['active']!='active' or value['pid']<=0 for value in shared.values()):raise ValueError('Required shared service not active')
    unchanged={str(SITE/name):sha(SITE/name) for name in ('lib.php','notification-lib.php','router.php','auth.php','panel.php',
        'station-lib.php','station-admin-policy.php','admin-payments.php','admin-customer.php','admin-product-edit.php','station-access-link.css')}
    for unit in (SERVICE,HELPER,'turborama-station-api.service'):
        for prop in ('FragmentPath','DropInPaths'):
            for name in ops.run(['systemctl','show',unit,'-p',prop,'--value']).split():unchanged[name]=sha(Path(name))
    for name in ('/etc/turborama-suite/station-management.env','/etc/turborama-suite/station-management.token',
        '/etc/turborama-suite/station-activation-pepper','/etc/turborama-suite/station-5192.env',
        '/opt/turborama-station-20261001/secrets/station-admin.token',
        '/opt/turborama-station-online-20261004-77d1dfb/online-engine-registry.json'):
        unchanged[name]=sha(Path(name))
    saved=dict(site=str(SITE),sourceRevision=revision,release=str(target),status='prepared',
        createdAt=datetime.now(timezone.utc).isoformat(),shared=shared,unchanged=unchanged,files={},
        licensesBefore=snapshot(database),usersBefore=users_snapshot(),dropinSha={})
    guard(saved);backup.mkdir(mode=0o700);(backup/'site').mkdir(mode=0o700)
    for name in NAMES:
        path=SITE/name;s=(path if path.exists() else SITE/'station-admin.php').stat()
        saved['files'][name]=dict(before=current(path),after=manifest['files']['site/'+name],uid=s.st_uid,gid=s.st_gid,mode=stat.S_IMODE(s.st_mode))
        if path.exists():ops.write(backup/'site'/name,path.read_text())
    shutil.copytree(OLD/'backend',backup/'old-backend');shutil.copytree(backup/'old-backend',backup/'restored-backend')
    if files(backup/'old-backend')!=files(backup/'restored-backend'):raise ValueError('Restored backend backup differs')
    shutil.rmtree(backup/'restored-backend');ops.write(backup/'old-helper.py',(OLD/'station-issue-admin.py').read_text())
    with sqlite3.connect('file:'+str(SITE/'.data/turbobox.sqlite')+'?mode=ro',uri=True) as source:
        with sqlite3.connect(backup/'website.sqlite') as destination:source.backup(destination)
    (backup/'website.sqlite').chmod(0o600);shutil.copy2(backup/'website.sqlite',backup/'restored-website.sqlite')
    with sqlite3.connect(backup/'restored-website.sqlite') as restored:
        if restored.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or restored.execute('PRAGMA foreign_key_check').fetchall():raise ValueError('SQLite restore gate failed')
    (backup/'restored-website.sqlite').unlink()
    private(backup/'state.json',saved)
    with (backup/'database.dump').open('xb') as stream:
        os.fchmod(stream.fileno(),0o600)
        result=subprocess.run(['runuser','-u','postgres','--','pg_dump','-h','/var/run/postgresql','-p','5432','--format=custom','-d',database],
            stdout=stream,stderr=subprocess.PIPE,timeout=300,env={k:v for k,v in os.environ.items() if not k.startswith('PG')})
        if result.returncode:raise ValueError('Private PostgreSQL backup failed')
    restored=ops.run(['pg_virtualenv','-t','/usr/bin/python3',str(Path(__file__)),'--restore-backup',str(backup)],timeout=360)
    if 'STATION REGISTRATION BACKUP RESTORED' not in restored:raise ValueError('PostgreSQL restore gate failed')
    saved['backupRestoreVerified']=True;private(backup/'state.json',saved)
    print(json.dumps({'stage':'backups-restored','sqlite':True,'postgresql':True}),flush=True)
    guard(saved);shutil.copytree(package,target)
    for path in (target,*target.rglob('*')):os.chown(path,0,0);path.chmod(0o755 if path.is_dir() else 0o644)
    if files(target)!=files(package):raise ValueError('Installed immutable artifact differs')
    overrides={SERVICE:f'[Service]\nWorkingDirectory={target}/backend\nExecStart=\nExecStart=/usr/bin/dotnet {target}/backend/TurboRamaSuiteAdminServer.dll\n',
        HELPER:f'[Service]\nExecStart=\nExecStart=/usr/bin/python3 {target}/station-issue-admin.py\n'}
    for unit,text in overrides.items():saved['dropinSha'][unit]=hashlib.sha256(text.encode()).hexdigest()
    private(backup/'state.json',saved)
    touched=False
    try:
        for unit,text in overrides.items():
            path=DROPINS[unit];path.parent.mkdir(exist_ok=True,mode=0o755)
            if path.exists():raise ValueError('Station override appeared during preparation')
            ops.write(path,text,0o644);touched=True
        ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.run(['systemctl','restart',HELPER]);ops.health()
        print(json.dumps({'stage':'Station administration ready','apiRestarted':False}),flush=True)
        for name in ('station-registration.php','station-admin.css','station-admin.js','station-admin.php'):
            meta=saved['files'][name]
            if current(SITE/name)!=meta['before']:raise ValueError('Station UI changed during preparation')
            ops.write(SITE/name,(package/'site'/name).read_text(),meta['mode'],meta['uid'],meta['gid'])
        saved['status']='installed';private(backup/'state.json',saved)
        checks=load(Path(__file__).with_name('registration-production-checks.py'),'station_registration_production').verify(database,ops.sql,ops.environment,backup,manifest)
        guard(saved)
        if snapshot(database,saved['licensesBefore']['ids'])!=saved['licensesBefore'] or users_snapshot(saved['usersBefore']['ids'])!=saved['usersBefore']:raise ValueError('Existing customers or entitlements changed')
        saved['status']='published';saved['appliedAt']=datetime.now(timezone.utc).isoformat();private(backup/'state.json',saved)
        result=dict(published=True,sourceRevision=revision,release=str(target),backup=str(backup),appliedAt=saved['appliedAt'],
            dllSha256=sha(target/'backend/TurboRamaSuiteAdminServer.dll'),helperSha256=sha(target/'station-issue-admin.py'),
            siteSha256={name:meta['after'] for name,meta in saved['files'].items()},
            backupRestoreVerified=True,sqliteAdditiveTable='station_registrations',postgresqlMigrations=0,
            apiUnchanged=True,apiDllSha256=API_SHA,api=shared['turborama-station-api.service'],
            sharedPidsUnchanged=True,existingUsersAndLicensesUnchanged=True,contentIndexUnchanged=True,
            management=ops.state(SERVICE),helper=ops.state(HELPER),productionChecks=checks)
        private(backup/'result.json',result);owner=pwd.getpwnam('lz-servidor');ops.write(RESULT,json.dumps(result,indent=2)+'\n',0o600,owner.pw_uid,owner.pw_gid)
        print(json.dumps({'stage':'published','verified':True,'url':'https://turbobox.lzgames.com.br/admin/station'}),flush=True)
    except Exception:
        if touched:rollback(backup)
        raise


def main():
    parser=argparse.ArgumentParser(description=__doc__);group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--package',type=Path);group.add_argument('--rollback',type=Path);group.add_argument('--restore-backup',type=Path)
    args=parser.parse_args()
    if os.geteuid()!=0:raise ValueError('Native Linux authentication required')
    os.umask(0o077)
    if args.restore_backup:restore_check(args.restore_backup.resolve());return
    lock=os.open('/run/station-registration-publish.lock',os.O_WRONLY|os.O_CREAT|os.O_NOFOLLOW,0o600)
    fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    if args.rollback:
        folder=args.rollback.resolve()
        if folder.parent!=Path('/mnt/DADOS') or not folder.name.startswith('station-registration-backup-20261006-'):raise ValueError('Unexpected rollback directory')
        rollback(folder)
    else:apply(args.package.resolve())


if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'published':False,'errorType':type(error).__name__,'message':str(error) if isinstance(error,ValueError) else 'Bounded publication failed; inspect private backup'}),flush=True)
        raise SystemExit(1)
