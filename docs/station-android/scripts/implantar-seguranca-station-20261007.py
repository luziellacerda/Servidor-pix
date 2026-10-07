#!/usr/bin/env python3
"""Guarded Station-only rollout with verified backup and reversible host changes.

Keeps current Station keys, commercial licenses, media, runtimes and clients.
Never changes other product identities, keys, firewall rules or SSH access.
Run with native polkit authentication and the established operator Python venv.
"""
import argparse,base64,hashlib,hmac,importlib.util,json,os,pwd,re,secrets,shutil,socket,subprocess,time
from datetime import datetime,timezone
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import ProxyHandler,Request,build_opener,urlopen
from station_hardening_config import ORIGIN_MAP,ORIGIN_GUARD,station_locations,samba_guest,sandbox_properties,systemd_override

ROOT=Path(__file__).resolve().parents[3]
SERVICE='turborama-station-api.service';IDENTITY='turborama-station-api';DLL='TurboRamaSuiteOnlineServer.dll'
OLD=Path('/opt/turborama-station-short-invite-20261006-a3e83d9')
OLD_SHA='5fff55c11a7e85d97d0ba155ea0387584dbeea207a82be294bda958655bf5331'
INDEX=Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
INDEX_SHA='07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92'
REGISTRY=OLD/'online-engine-registry.json'
REGISTRY_SHA='a4412aa8139b865d62b1dc42cb4b08f7fc656b3a7efca6237df7854d2ef888cc'
MEDIA=[Path('/mnt/DADOS')/name for name in ['turbostation-library-auto-20261004',
    'turbostation-neogeo-content-20261005','turbostation-neogeocd-content-20261005','turbostation-releases']]
KEYS=Path('/etc/turborama-station-api-security-20261007')
DROPIN=Path('/etc/systemd/system')/(SERVICE+'.d')/'zzzzzzzzzzzzzzz-station-security-20261007.conf'
PROXY=[Path('/etc/nginx/snippets')/name for name in ['turborama-station.locations.conf',
    'turborama-station-online.locations.conf','turborama-station-relay.locations.conf']]
MAP=Path('/etc/nginx/conf.d/turborama-station-origin-20261007.conf')
GUARD=Path('/etc/nginx/snippets/turborama-station-origin-guard.conf')
SAMBA=Path('/etc/samba/smb.conf');SHARE_PATH='/home/lz-servidor/Imagens/ROMS PS1'
RESULT=Path('/home/lz-servidor/station-security-20261007/implantacao-privada.json')

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
ops=load('implantar-station-20261003.py');online=load('implantar-online-station-20261004.py');online.ops=ops
SHARED=tuple(dict.fromkeys((*online.SHARED,'mariadb.service','smbd.service','ssh.service','fail2ban.service')))

def paths(revision):
    if not re.fullmatch('[0-9a-f]{40}',revision):raise ValueError('Exact source revision required')
    suffix='20261007-'+revision[:7]
    return Path('/mnt/DADOS/station-security-backup-'+suffix),Path('/mnt/DADOS/station-security-candidate-'+suffix),Path('/opt/turborama-station-security-'+suffix)
def files(folder):
    result={}
    for p in folder.rglob('*'):
        if p.is_symlink():raise ValueError('Release link refused')
        if p.is_file():result[p.relative_to(folder).as_posix()]=ops.digest(p)
    return result
def report(value,path=RESULT):
    ops.private_text(path,json.dumps(value,indent=2)+'\n');owner=pwd.getpwnam('lz-servidor');os.chown(path,owner.pw_uid,owner.pw_gid)
def telemetry():
    with urlopen('http://127.0.0.1:5192/ready/station/online',timeout=5) as response:return json.load(response)
def idle():
    result=telemetry()
    if result['activeRooms'] or result['activeConnections']:raise ValueError('Active relay game; rollout deferred')
    return result
def real_licenses(db):
    data=ops.sql(db,"SELECT row_to_json(l)::text FROM suite.suite_licenses l WHERE product_id='TURBORAMA_STATION_ANDROID' "
        "AND NOT EXISTS(SELECT 1 FROM suite.suite_license_deliveries d WHERE d.license_id=l.license_id "
        "AND d.source_system='STATION_ROLLOUT_TEST') ORDER BY license_id")
    return hashlib.sha256(data.encode()).hexdigest()
def sql_stdin(db,text):
    result=subprocess.run(['runuser','-u','postgres','--','psql','-X','-q','-v','ON_ERROR_STOP=1','--dbname',db],
        input=text,text=True,capture_output=True,timeout=60)
    if result.returncode:raise ValueError('Protected database operation failed')
def scram(password):
    salt=secrets.token_bytes(16);derived=hashlib.pbkdf2_hmac('sha256',password.encode(),salt,4096)
    stored=hashlib.sha256(hmac.digest(derived,b'Client Key','sha256')).digest();server=hmac.digest(derived,b'Server Key','sha256')
    enc=lambda v:base64.b64encode(v).decode()
    return 'SCRAM-SHA-256$4096:'+enc(salt)+'$'+enc(stored)+':'+enc(server)
def groups():
    user=pwd.getpwnam(IDENTITY)
    if user.pw_uid==0 or user.pw_shell!='/usr/sbin/nologin':raise ValueError('Dedicated identity differs')
    return user
def pool_settings(connection):
    values={};entries=[]
    for name,value in re.findall(r'(?:^|;)\s*((?:Max(?:imum)?|Min(?:imum)?)\s*Pool\s*Size)\s*=\s*([0-9]+)',connection,re.I):
        key='max' if name.lower().startswith('max') else 'min'
        if key in values:raise ValueError('Duplicate production pool setting')
        values[key]=int(value);entries.append(name+'='+value)
    maximum=values.get('max',8)
    if not 1<=maximum<=128 or not 0<=values.get('min',0)<=maximum:raise ValueError('Unplanned production pool size')
    return entries,maximum*2
def sandbox_access(pid,media_files,target):
    # Enter the real service mount namespace and then drop every root capability.
    # Open one byte only: this is an access check, never a ROM integrity scan.
    payload=dict(read=[*media_files,str(target/'online-engine-registry.json')],
        denied=['/etc/turborama-suite/activation-pepper','/etc/cloudflared/config.yml',
            '/var/lib/postgresql/16/main/PG_VERSION',str(OLD/DLL)],readonly=[str(INDEX),str(target/DLL)])
    code="""import json,os,sys
p=json.load(sys.stdin)
for name in p['read']:
    with open(name,'rb') as f:f.read(1)
for name in p['denied']:
    try:f=open(name,'rb')
    except OSError:continue
    else:f.close();raise SystemExit('Protected path remained readable')
for name in p['readonly']:
    if os.access(name,os.W_OK):raise SystemExit('Published media or runtime remained writable')
print(json.dumps(dict(mediaFilesReadable=len(p['read'])-1,protectedPathsDenied=len(p['denied']),readOnlyMounts=True)))
"""
    checked=subprocess.run(['/usr/bin/nsenter','--target',str(pid),'--mount','--wd=/',
        '/usr/bin/setpriv','--reuid',IDENTITY,'--regid',IDENTITY,'--clear-groups',
        '--inh-caps=-all','--ambient-caps=-all','--bounding-set=-all','--no-new-privs',
        '/usr/bin/python3','-c',code],input=json.dumps(payload),text=True,capture_output=True,timeout=45)
    if checked.returncode:raise ValueError('Actual sandbox filesystem access gate failed')
    return json.loads(checked.stdout)
def snapshot_configs():
    config=[*PROXY,SAMBA,Path('/etc/nginx/conf.d/lzgames.conf'),Path('/etc/nginx/conf.d/lzgames-ssl.conf'),
        Path('/etc/cloudflared/config.yml'),Path('/etc/turborama-suite/station-5192.env'),
        Path('/etc/turborama-suite/station-rev3-20261003.env'),Path('/etc/turborama-suite/station-covers-revista-20261003-rev4.env'),
        Path('/opt/turborama-station-folders-20261004-931030b/station-library.env')]
    config.extend(DROPIN.parent.glob('*.conf'))
    config.append(Path(ops.run(['systemctl','show',SERVICE,'-p','FragmentPath','--value']).strip()))
    return {str(p):ops.digest(p) for p in config}
def unchanged(state,configuration=True):
    if ops.digest(INDEX)!=INDEX_SHA or ops.digest(REGISTRY)!=REGISTRY_SHA or files(OLD)!=state['oldFiles']:
        raise ValueError('Original release, index or runtime registry changed')
    for unit,old in state['shared'].items():
        if ops.state(unit)!=old:raise ValueError('Shared service changed')
    expected=dict(state['configuration'])
    if not configuration:expected.update(state.get('changedConfiguration',{}))
    for name,old in expected.items():
        if ops.digest(Path(name))!=old:raise ValueError('Unrelated configuration changed')

def restore_check(backup):
    if backup.parent!=Path('/mnt/DADOS') or not backup.name.startswith('station-security-backup-20261007-'):
        raise ValueError('Unexpected private backup')
    if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.') or not ops.run(
        ['psql','-X','-Atqc','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('Temporary PostgreSQL is required')
    ops.run(['createdb','station_security_restore'])
    ops.run(['pg_restore','--exit-on-error','--no-owner','--no-acl','--dbname','station_security_restore',str(backup/'database.dump')],timeout=300)
    count=ops.run(['psql','-X','--dbname','station_security_restore','-Atqc',
        "SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()
    if count!='3':raise ValueError('Restored ledger differs')
    print('Station security database backup restored',flush=True)

def rollback(revision,automatic=False):
    backup,_,target=paths(revision);state=json.loads((backup/'state.json').read_text())
    if online.command_path() not in (OLD/DLL,target/DLL):raise ValueError('Station release was superseded')
    idle();unchanged(state,configuration=False)
    DROPIN.unlink(missing_ok=True)
    for name in [*PROXY,SAMBA,Path(state['hba'])]:
        source=backup/'configuration'/state['saved'][str(name)]
        ops.replace_config(name,source.read_text())
    MAP.unlink(missing_ok=True);GUARD.unlink(missing_ok=True)
    ops.run(['/usr/sbin/nginx','-t']);ops.run(['systemctl','reload','nginx'])
    ops.run(['/usr/bin/testparm','-s']);ops.run(['/usr/bin/smbcontrol','smbd','reload-config'])
    ops.sql(state['database'],'SELECT pg_reload_conf()')
    if state.get('aclBackup'):
        ops.run(['/usr/bin/setfacl','--restore='+str(backup/'media.acl')],timeout=60)
    if ops.sql(state['database'],"SELECT count(*) FROM pg_roles WHERE rolname='turborama-station-api'")=='1':
        sql_stdin(state['database'],'ALTER ROLE "turborama-station-api" NOLOGIN;')
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
    if online.command_path()!=OLD/DLL:raise ValueError('Original Station did not return')
    unchanged(state)
    # These are only copies created by this rollout; original Station secrets remain.
    if KEYS.exists():shutil.rmtree(KEYS)
    try:
        groups();ops.run(['/usr/sbin/userdel',IDENTITY])
    except KeyError:pass
    ops.private_text(backup/'rollback-complete','Original configuration restored; additive migrations retained.\n')
    print('Original Station identity, proxy, Samba and media ACL restored; additive schema retained.',flush=True)

def apply(revision):
    backup,candidate,target=paths(revision);result=dict(applied=False,sourceRevision=revision);stage='preflight';changed=False
    try:
        if ops.run(['git','-c','safe.directory='+str(ROOT),'rev-parse','HEAD'],cwd=ROOT).strip()!=revision or ops.run(
            ['git','-c','safe.directory='+str(ROOT),'status','--porcelain'],cwd=ROOT).strip():raise ValueError('Exact clean source required')
        if online.command_path()!=OLD/DLL or ops.digest(OLD/DLL)!=OLD_SHA:raise ValueError('Inspected production changed')
        if any(p.exists() for p in [backup,target,KEYS,DROPIN,MAP,GUARD,RESULT]):raise ValueError('Rollout destination already exists')
        try:pwd.getpwnam(IDENTITY);raise ValueError('Dedicated identity already exists')
        except KeyError:pass
        values,ids=ops.runtime();db=ops.database_name(values)
        pool,connection_limit=pool_settings(values['ConnectionStrings__SuiteStore'])
        if values.get('Station__LibraryIndexFile')!=str(INDEX) or values.get('Station__Online__EngineRegistryFile')!=str(REGISTRY):
            raise ValueError('Index or engine configuration differs')
        for key,expected in [('Station__Online__Enabled','true'),('Station__Online__RelayEnabled','true'),
            ('Station__Online__SocialEnabled','true'),('Station__LibraryVerifyContentOnLoad','false'),('Station__LibraryAutoReload','true')]:
            if values.get(key,'').lower()!=expected:raise ValueError('Existing Station feature differs')
        meta=json.loads((candidate/'release.json').read_text());manifest=files(candidate);manifest.pop('release.json')
        if meta['sourceRevision']!=revision or manifest!=meta['files'] or meta['dllSha256']!=ops.digest(candidate/DLL):
            raise ValueError('Sealed candidate differs')
        if ops.digest(candidate/'online-engine-registry.json')!=REGISTRY_SHA:raise ValueError('Candidate engine registry differs')
        if ops.digest(INDEX)!=INDEX_SHA or ops.digest(REGISTRY)!=REGISTRY_SHA:raise ValueError('Inspected catalog differs')
        index=json.loads(INDEX.read_text())
        if index['revision']!=14 or sum(r.get('catalogVisible',True) for r in index['items'])!=2212:raise ValueError('Catalog totals differ')
        for row in index['items']:
            for field in ['filePath','coverPath']:
                if row.get(field) and not any(Path(row[field]).is_relative_to(root) for root in MEDIA):raise ValueError('Unplanned media root')
        result['relayBefore']=idle();shared={u:ops.state(u) for u in SHARED}
        if any('ActiveState=active' not in v for v in shared.values()):raise ValueError('Shared service unhealthy')
        hba=Path(ops.sql(db,'SHOW hba_file'))
        if hba.parent!=Path('/etc/postgresql/16/main') or hba.is_symlink():raise ValueError('Unexpected local PostgreSQL configuration')
        config=snapshot_configs();config[str(hba)]=ops.digest(hba)
        state=dict(database=db,target=str(target),oldFiles=files(OLD),shared=shared,configuration=config,changedConfiguration={},
            hba=str(hba),realLicenseSha256=real_licenses(db),saved={})
        stage='backup_restore';backup.mkdir(mode=0o700);(backup/'configuration').mkdir(mode=0o700)
        shutil.copytree(OLD,backup/'api')
        for i,name in enumerate(config):
            saved=str(i)+'.backup';shutil.copy2(name,backup/'configuration'/saved);state['saved'][name]=saved
        ops.private_text(backup/'state.json',json.dumps(state))
        with (backup/'database.dump').open('xb') as dump:
            os.fchmod(dump.fileno(),0o600)
            r=subprocess.run(['runuser','-u','postgres','--','pg_dump','--format=custom','--dbname',db],stdout=dump,stderr=subprocess.PIPE,timeout=300)
            if r.returncode:raise ValueError('Private database backup failed')
        if 'backup restored' not in ops.run(['pg_virtualenv','-t','python3',str(Path(__file__).resolve()),'--restore-backup',str(backup)],timeout=360):
            raise ValueError('Private backup restore proof failed')
        result['backupRestoreVerified']=True;unchanged(state)
        print('Private database/configuration backup restored and verified. Preparing isolated Station.',flush=True)
        stage='dedicated_identity';changed=True
        ops.run(['/usr/sbin/useradd','--system','--user-group','--no-create-home','--home-dir','/nonexistent','--shell','/usr/sbin/nologin',IDENTITY])
        user=groups()
        previous_return=any(p.is_file() and p.stat().st_uid==0 for p in Path('/mnt/DADOS').glob('station-security-backup-20261007-*/rollback-complete'))
        for number in ['031_station_api_isolation','032_station_request_proof']:
            migration=ROOT/'migrations/suite'/(number+'.up.sql')
            exists=ops.sql(db,"SELECT count(*) FROM suite.schema_migrations WHERE version='"+number+"'")!='0'
            if exists and not previous_return:raise ValueError('Migration already exists outside a proven rollback')
            if not exists:ops.run(['runuser','-u','postgres','--','psql','-X','-q','-v','ON_ERROR_STOP=1','--dbname',db,'-f',str(migration)],timeout=70)
        password=secrets.token_hex(32)
        sql_stdin(db,'ALTER ROLE "turborama-station-api" LOGIN CONNECTION LIMIT '+str(connection_limit)+' PASSWORD \''+scram(password)+"';")
        KEYS.mkdir(mode=0o750);os.chown(KEYS,0,user.pw_gid)
        def protected(name,data):
            p=KEYS/name;ops.private_text(p,data);os.chown(p,0,user.pw_gid);p.chmod(0o640);return str(p)
        def read(key,file_key):
            return values[key] if values.get(key) else Path(values[file_key]).read_text().strip()
        from cryptography.hazmat.primitives import serialization
        pepper=read('Suite__ActivationPepper','Suite__ActivationPepperFile')
        pem=read('Suite__OnlineAssertionPrivateKeyPem','Suite__OnlineAssertionPrivateKeyPemFile')
        suite_public=serialization.load_pem_private_key(pem.encode(),None).public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
        settings={k:v for k,v in values.items() if k.startswith('Station__') and k not in
            ['Station__ActivationPepper','Station__AssertionPrivateKeyPem','Station__ActivationPepperFile','Station__AssertionPrivateKeyPemFile','Station__DownloadKeyFile']}
        settings.update({k:v for k,v in values.items() if k.startswith(('DOTNET_','COMPlus_')) or k in
            ('ASPNETCORE_ENVIRONMENT','LANG','LC_ALL','TZ')})
        settings['ASPNETCORE_URLS']='http://127.0.0.1:5192'
        settings.update(Suite__Enabled='false',Station__IsolatedDatabase='true',Station__Security__RequireVerifiedApp='false',
            Station__DatabaseConnectionFile=protected('database-connection','Host=127.0.0.1;Database='+db+';Username='+IDENTITY+';Password='+password+';ApplicationName=turborama-station-api'),
            Station__ActivationPepperFile=protected('activation-pepper',read('Station__ActivationPepper','Station__ActivationPepperFile')),
            Station__AssertionPrivateKeyPemFile=protected('assertion.pem',read('Station__AssertionPrivateKeyPem','Station__AssertionPrivateKeyPemFile')),
            Station__Isolation__SuitePepperSha256=hashlib.sha256(base64.b64decode(pepper,validate=True)).hexdigest(),
            Station__Isolation__SuiteAssertionKeyId=hashlib.sha256(suite_public).hexdigest())
        download=KEYS/'download.key';download.write_bytes(Path(values['Station__DownloadKeyFile']).read_bytes());download.chmod(0o640);os.chown(download,0,user.pw_gid)
        settings['Station__DownloadKeyFile']=str(download)
        settings['Station__Online__EngineRegistryFile']=str(target/'online-engine-registry.json')
        # Preserve explicit production pool sizes without retaining Suite credentials.
        if pool:
            p=KEYS/'database-connection';p.write_text(p.read_text()+';'+';'.join(pool));p.chmod(0o640)
        hba_old=hba.read_text()
        if IDENTITY in hba_old:raise ValueError('Unexpected existing dedicated HBA rule')
        hba_new='# Station isolated identity, 2026-10-07\nhost '+db+' '+IDENTITY+' 127.0.0.1/32 scram-sha-256\n'+\
            'host all '+IDENTITY+' 0.0.0.0/0 reject\nhost all '+IDENTITY+' ::/0 reject\nlocal all '+IDENTITY+' reject\n'+hba_old
        ops.replace_config(hba,hba_new)
        state['changedConfiguration'][str(hba)]=ops.digest(hba)
        ops.replace_config(backup/'state.json',json.dumps(state))
        if ops.sql(db,"SELECT count(*) FROM pg_hba_file_rules WHERE error IS NOT NULL")!='0':raise ValueError('Dedicated HBA rule is invalid')
        ops.sql(db,'SELECT pg_reload_conf()')
        env_text='\n'.join(k+'='+json.dumps(v) for k,v in sorted(settings.items()))+'\n'
        env_file=Path(protected('station.env',env_text))
        shutil.copytree(candidate,target)
        for p in [target,*target.rglob('*')]:os.chown(p,0,user.pw_gid);p.chmod(0o750 if p.is_dir() else 0o640)
        if files(target)!=files(candidate):raise ValueError('Installed release differs')
        stage='readonly_media';media_files=sorted({str(INDEX),*(r[k] for r in index['items'] for k in ['filePath','coverPath'] if r.get(k))})
        directories={str(root) for root in MEDIA}
        for name in media_files:
            p=Path(name)
            if not p.is_file():raise ValueError('Published media is missing')
            for parent in p.parents:
                if any(parent.is_relative_to(root) for root in MEDIA):directories.add(str(parent))
        # All pre-existing directories receive a default read/traverse ACL for auto imports.
        for root in MEDIA:
            for current,dirs,_ in os.walk(root):
                if Path(current).is_symlink():raise ValueError('Media directory link refused')
                directories.add(current)
        objects=sorted(set(media_files)|directories)
        with (backup/'media.acl').open('xb') as output:
            os.fchmod(output.fileno(),0o600)
            for i in range(0,len(objects),100):
                r=subprocess.run(['/usr/bin/getfacl','-p','--',*objects[i:i+100]],stdout=output,stderr=subprocess.PIPE,timeout=30)
                if r.returncode:raise ValueError('Media ACL backup failed')
        state['aclBackup']=True
        # Persist rollback state atomically before the first ACL mutation.
        ops.replace_config(backup/'state.json',json.dumps(state))
        for collection,access in [(media_files,'g:'+IDENTITY+':r--'),(sorted(directories),'g:'+IDENTITY+':r-x,d:g:'+IDENTITY+':r-x')]:
            for i in range(0,len(collection),100):ops.run(['/usr/bin/setfacl','-m',access,'--',*collection[i:i+100]],timeout=30)
        verify=dict(settings,ConnectionStrings__SuiteStore=values['ConnectionStrings__SuiteStore'])
        checks=load('verificar-convite-curto-station-20261006.py');relay=load('verificar-relay-station.py')
        stage='sandbox_shadow'
        with socket.socket() as probe:probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
        base='http://127.0.0.1:'+str(port);unit='station-security-shadow-'+revision[:7]+'.service'
        arguments=['systemd-run','--quiet','--collect','--unit',unit]
        for k,v in sandbox_properties(target,IDENTITY,MEDIA).items():arguments+=['--property',k+'='+v]
        arguments+=['--property','EnvironmentFile='+str(env_file),'--property','WorkingDirectory='+str(target),
            '--property','StandardOutput=append:'+str(backup/'shadow.log'),'--property','StandardError=append:'+str(backup/'shadow.log'),
            '/usr/bin/dotnet',str(target/DLL),'--urls',base]
        ops.run(arguments)
        try:
            ops.ready(base)
            shadow_pid=int(ops.run(['systemctl','show',unit,'-p','MainPID','--value']))
            result['shadowFilesystem']=sandbox_access(shadow_pid,media_files,target)
            result['shadowSocial']=checks.verify(index,verify,base,True,True)
            result['shadowRelay']=relay.verify(index,verify,base,True)
            result['shadowSecurity']=load('verificar-seguranca-station-20261007.py').verify(index,verify,base)
        finally:
            stopped=subprocess.run(['systemctl','stop',unit],capture_output=True,timeout=30)
            if stopped.returncode and ops.run(['systemctl','show',unit,'-p','MainPID','--value']).strip() not in ('','0'):
                raise ValueError('Temporary shadow process did not stop')
        unchanged(state,configuration=False);idle()
        if real_licenses(db)!=state['realLicenseSha256']:raise ValueError('Real licenses changed before cutover')
        stage='station_origin_proxy'
        for i,p in enumerate(PROXY):
            ops.replace_config(p,station_locations(p.read_text(),authentication=i==0))
            state['changedConfiguration'][str(p)]=ops.digest(p);ops.replace_config(backup/'state.json',json.dumps(state))
        ops.private_text(MAP,ORIGIN_MAP);MAP.chmod(0o644);ops.private_text(GUARD,ORIGIN_GUARD);GUARD.chmod(0o644)
        ops.run(['/usr/sbin/nginx','-t']);ops.run(['systemctl','reload','nginx'])
        stage='samba_guest'
        smb=json.loads(ops.run(['/usr/bin/smbstatus','--json']))
        if smb.get('sessions') or smb.get('tcons') or smb.get('open_files'):raise ValueError('Samba is in use; guest change deferred')
        if not ops.run(['/usr/bin/pdbedit','-L','-u','lz-servidor']).strip():raise ValueError('Authenticated operator Samba account missing')
        new_samba,share=samba_guest(SAMBA.read_text(),SHARE_PATH)
        ops.replace_config(SAMBA,new_samba)
        state['changedConfiguration'][str(SAMBA)]=ops.digest(SAMBA);ops.replace_config(backup/'state.json',json.dumps(state))
        ops.run(['/usr/bin/testparm','-s']);ops.run(['/usr/bin/smbcontrol','smbd','reload-config'])
        anonymous=subprocess.run(['/usr/bin/smbclient','//127.0.0.1/'+share,'-N','-U','%','--use-kerberos=off',
            "--option=client min protocol=SMB2",'-c','pwd'],capture_output=True,text=True,timeout=15)
        if anonymous.returncode==0:raise ValueError('Anonymous obsolete share still accessible')
        stage='station_cutover';idle()
        override=systemd_override(target,IDENTITY,MEDIA,env_file);ops.private_text(DROPIN,override);DROPIN.chmod(0o644)
        ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
        running,new_ids=ops.runtime()
        if online.command_path()!=target/DLL or new_ids['Uid'][1]!=user.pw_uid or new_ids['Gid'][1]!=user.pw_gid:
            raise ValueError('Effective Station identity differs')
        if 'ConnectionStrings__SuiteStore' in running or any(k.startswith('Suite__') and k!='Suite__Enabled' for k in running):
            raise ValueError('Suite protected settings remained in Station')
        stage='public_proofs'
        result['publicSocial']=checks.verify(index,verify,'https://app.lzgames.com.br',True,True)
        result['publicSecurity']=load('verificar-seguranca-station-20261007.py').verify(index,verify,'https://app.lzgames.com.br')
        origin_addresses=json.loads(ops.run(['ip','-j','-4','addr','show']))
        address=next(item['local'] for nic in origin_addresses for item in nic.get('addr_info',[]) if item.get('scope')=='global')
        request=Request('http://'+address+'/v1/station/catalog',headers={'Host':'app.lzgames.com.br','CF-Connecting-IP':'127.0.0.1'})
        try:
            with build_opener(ProxyHandler({})).open(request,timeout=10) as reply:status=reply.status
        except HTTPError as reply:status=reply.code
        if status!=404:raise ValueError('Direct origin bypass was not blocked')
        unchanged(state,configuration=False)
        if real_licenses(db)!=state['realLicenseSha256']:raise ValueError('Real commercial license changed')
        for port in [5187,5190,5191]:
            with urlopen('http://127.0.0.1:'+str(port)+'/health',timeout=5) as reply:
                if reply.status!=200:raise ValueError('Other product health changed')
        result.update(applied=True,completedAtUtc=datetime.now(timezone.utc).isoformat(),backup=str(backup),
            dllSha256=meta['dllSha256'],pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value'])),
            dedicatedLinuxIdentity=True,dedicatedProductDatabaseViews=True,stationSecretsPreserved=True,
            readonlyMediaAndFutureImports=True,stationOnlyOriginGuard=True,anonymousObsoleteShareDisabled=True,
            directOriginStatus=status,requestProofNegotiation=True,requireVerifiedApp=False,
            hardwarePhoneVerified=False,legacyApkPreserved=True,androidApkInstalled=False,androidGameplay=False,
            firewallChanged=False,sshChanged=False,otherProductsPreserved=True,realLicensesPreserved=True,
            indexSha256=INDEX_SHA,indexRevision=14,visibleItems=2212,registrySha256=REGISTRY_SHA,relayAfter=telemetry())
        report(result);print(json.dumps({k:v for k,v in result.items() if not isinstance(v,dict)}),flush=True)
    except Exception as error:
        result.update(failedStage=stage,errorType=type(error).__name__)
        if isinstance(error,ValueError):result['reason']=str(error)
        if changed:
            try:rollback(revision,True);result['rolledBack']=True
            except Exception as failure:result['rollbackErrorType']=type(failure).__name__
        failure=RESULT.with_name('implantacao-falha-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
        report(result,failure);print(json.dumps({k:v for k,v in result.items() if not isinstance(v,dict)}),flush=True)
        raise SystemExit(1)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--revision');parser.add_argument('--apply',action='store_true');parser.add_argument('--rollback',action='store_true');parser.add_argument('--restore-backup',type=Path);args=parser.parse_args()
    if args.restore_backup:restore_check(args.restore_backup);return
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise SystemExit('Native operator authentication required')
    if args.apply==args.rollback or not args.revision:raise SystemExit('Choose the guarded apply or rollback')
    if args.rollback:rollback(args.revision)
    else:apply(args.revision)
if __name__=='__main__':main()
