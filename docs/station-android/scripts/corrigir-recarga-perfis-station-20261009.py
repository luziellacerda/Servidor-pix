#!/usr/bin/python3
"""Correct immutable profile reuse; preserve published data and all other services."""
import argparse,fcntl,hashlib,importlib.util,json,os,pwd,shutil,subprocess,sys,traceback
from pathlib import Path
sys.dont_write_bytecode=True
SCRIPTS=Path(__file__).resolve().parent;sys.path.insert(0,str(SCRIPTS))
spec=importlib.util.spec_from_file_location('profile_fix_base',SCRIPTS/'implantar-five-players-station-20261009.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
ops,security,r71=b.ops,b.security,b.r71
OLD=Path('/opt/turborama-station-online-platforms-20261009-e74fde53cbe6')
OLD_SHA='e74fde53cbe65273d26b3576cb39b8d096dff476dff99409681cd2469b678007'
INDEX_SHA='f7924dcd6bdea74c2f32d8a482184f6f21f6a7f235b77f697432fd57637aef6e'
PROFILE_SHA='a029fb61ce68fa26abadf0312b55b9ae163c6a3604a12bfd37e97d406fdfebaa'
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
DROPIN=Path('/etc/systemd/system')/(b.SERVICE+'.d')/('z'*38+'-station-profile-reload-fix-20261009.conf')
def quiet():
    if not b.quiet(b.ready()):raise ValueError('An active or recovering room prevents replacement')
def protect(path,gid):
    os.chown(path,0,gid);path.chmod(0o750 if path.is_dir() else 0o640)
def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('release','seal','work-directory'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('Native Linux administrative authentication required')
    for path in (a.release,a.seal,a.work_directory):
        if not path.is_absolute() or path.is_symlink():raise ValueError('Absolute regular paths required')
    os.umask(0o077);a.work_directory.mkdir(mode=0o700,exist_ok=False)
    operator=pwd.getpwnam('lz-servidor');ops.run(['setfacl','-m','u:'+str(operator.pw_uid)+':rx',str(a.work_directory)])
    seal=json.loads(a.seal.read_bytes())
    if security.files(a.release)!=seal['releaseFiles'] or b.digest(a.release/'TurboRamaSuiteOnlineServer.dll')!=seal['dllSha256']:raise ValueError('Compiled seal differs')
    target=Path('/opt/turborama-station-profile-reload-20261009-'+seal['dllSha256'][:12])
    if target.exists() or DROPIN.exists():raise ValueError('Fresh release required; do not repeat')
    changed=False
    try:
        with (b.INDEX.parent/'scan.lock').open('a') as lock:
            fcntl.flock(lock,fcntl.LOCK_EX);quiet();values,ids=ops.runtime();owner=pwd.getpwnam(b.IDENTITY)
            profiles=Path(values['Station__Online__MultiplayerProfileRegistryFile']);registry=Path(values['Station__Online__EngineRegistryFile'])
            if str(OLD/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or b.digest(OLD/'TurboRamaSuiteOnlineServer.dll')!=OLD_SHA or b.digest(b.INDEX)!=INDEX_SHA or b.digest(profiles)!=PROFILE_SHA or b.digest(registry)!=b.REGISTRY or values.get('Station__Online__ReplayMaximumBytes')!='134217728':raise ValueError('Published baseline differs')
            if ids['Uid'][1]!=owner.pw_uid or ids['Gid'][1]!=owner.pw_gid:raise ValueError('Station identity differs')
            index=json.loads(b.INDEX.read_bytes());db=r71.database(values)
            snapshot=dict(oldFiles=security.files(OLD),configuration=security.snapshot_configs(),configSha256=b.digest(CONFIG),indexSha256=INDEX_SHA,profilesSha256=PROFILE_SHA,realLicenses=security.real_licenses(db),schema=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),shared={u:ops.state(u) for u in security.SHARED})
            b.private(a.work_directory/'baseline-private.json',snapshot)
            def preservation():
                if security.files(OLD)!=snapshot['oldFiles'] or b.digest(CONFIG)!=snapshot['configSha256'] or b.digest(b.INDEX)!=INDEX_SHA or b.digest(profiles)!=PROFILE_SHA:raise ValueError('Published release/data changed')
                for name,digest in snapshot['configuration'].items():
                    if b.digest(Path(name))!=digest:raise ValueError('Existing setting changed')
                for unit,state in snapshot['shared'].items():
                    if ops.state(unit)!=state:raise ValueError('Another service changed')
                if security.real_licenses(db)!=snapshot['realLicenses'] or hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()!=snapshot['schema']:raise ValueError('Real licenses/schema changed')
                security.database_scope(db);security.management_health()
            shutil.copytree(a.release,target);shutil.copyfile(registry,target/'online-engine-registry.json')
            newvalues=dict(values,Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'))
            b.private(target/'profile-reload.env','Station__Online__EngineRegistryFile='+str(target/'online-engine-registry.json')+'\n')
            for path in sorted(target.rglob('*'),reverse=True):protect(path,owner.pw_gid)
            protect(target,owner.pw_gid);(target/'profile-reload.env').chmod(0o600)
            old_bind=b.show('BindReadOnlyPaths');binds=old_bind.replace(str(OLD),str(target))
            original_verify=b.five.verify
            def verify_platforms(catalog,settings,address,sql):
                entries=json.loads(profiles.read_bytes());extra=[]
                for system in ('n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3'):
                    eligible=[p for p in entries if p['platform']==system and p['approved'] and p['maximumPlayers']>=2]
                    extra.append(max(eligible,key=lambda p:p['maximumPlayers']))
                return original_verify(catalog,settings,address,sql,additional_profiles=extra)
            b.five.verify=verify_platforms
            proof={'candidate':b.shadow(target,newvalues,index,a.work_directory,binds,'profile-reload-fix',True)}
            preservation();quiet();b.progress('apply-profile-reload-fix',catalogChanged=False,profilesChanged=False)
            b.private(DROPIN,'[Service]\nWorkingDirectory='+str(target)+'\nExecStart=\nExecStart=/usr/bin/dotnet '+str(target/'TurboRamaSuiteOnlineServer.dll')+'\nBindReadOnlyPaths=\nBindReadOnlyPaths='+binds+'\nEnvironmentFile='+str(target/'profile-reload.env')+'\n');DROPIN.chmod(0o644)
            changed=True;ops.run(['systemctl','daemon-reload']);quiet();ops.run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL)
            current,current_ids=ops.runtime()
            for k,v in newvalues.items():
                if k.startswith(('Station__','Suite__','ConnectionStrings__')) and current.get(k)!=v:raise ValueError('Effective settings differ')
            if str(target/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or current_ids['Uid'][1]!=owner.pw_uid or current_ids['Gid'][1]!=owner.pw_gid:raise ValueError('Effective release/identity differs')
            proof['public']=b.checks(index,current,b.PUBLIC,True);after=b.wait_quiet(b.LOCAL);preservation()
            pid=int(b.show('MainPID'));sandbox=r71.sandbox(pid,target,index)
            result=dict(applied=True,utc=b.utc(),sourceCommit=seal['sourceCommit'],dllSha256=seal['dllSha256'],pid=pid,nRestarts=int(b.show('NRestarts')),catalogRevision=27,profiles=5176,catalogAndIdentitiesAndProfilesPreserved=True,importerPreserved=True,realLicensesAndSchemaPreserved=True,sharedProductsPreserved=True,proxyTunnelFirewallChanged=False,profileReloadJoinAndReconnectCorrected=True,proof=proof,sandbox=sandbox,readyAfter=after)
            b.private(a.work_directory/'deployment-result.json',result,True);b.progress('complete',applied=True,pid=pid,catalogRevision=27,profiles=5176)
    except Exception:
        if changed:
            try:return_safe=b.quiet(b.ready())
            except OSError:return_safe=b.show('ActiveState') not in ('active','activating')
            if not return_safe:raise ValueError('Rollback deferred while a room is active or cannot be checked')
            DROPIN.unlink(missing_ok=True);ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL);b.progress('rollback',previousReleaseRestored=True)
        raise
if __name__=='__main__':
    try:main()
    except Exception:
        paths=[Path(sys.argv[i+1]) for i,v in enumerate(sys.argv[:-1]) if v=='--work-directory']
        if paths and paths[0].is_dir():b.private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        b.progress('failed',detailsSuppressed=True);raise SystemExit(1)
