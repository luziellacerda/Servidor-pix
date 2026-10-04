#!/usr/bin/env python3
"""Publish the concurrent R9 folderPath contract over the active automatic N64 library."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import pwd
import shutil
import socket
import subprocess
import traceback

ROOT=Path(__file__).resolve().parents[3]
SERVICE='turborama-station-api.service'
OLD=Path('/opt/turborama-station-library-20261004-77d8d54')
OLD_SHA='14500ad850f41f6d361a8c29dd3a64ddd9b9642ad55133d14f814cbb0288e207'
INDEX=Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
OLD_INDEX_SHA='a9aaaf115604cf80f659348e5ef38b5ca7d153f552bf549a6e70398fe1bedb90'
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
BACKUP=Path('/mnt/DADOS/station-folders-backup-20261004')
DROPIN=Path('/etc/systemd/system/'+SERVICE+'.d/zzzzz-station-library-20261004.conf')
SCAN=Path('/etc/systemd/system/turborama-station-library-scan.service')
TIMER='turborama-station-library-scan.timer'
RESULT=Path('/home/lz-servidor/station-folders-rollout-result-20261004.json')
DLL='TurboRamaSuiteOnlineServer.dll'

def module(name):
    s=importlib.util.spec_from_file_location(name,Path(__file__).with_name(name+'.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
ops=module('implantar-station-20261003');online=module('implantar-online-station-20261004');online.ops=ops
verification=module('verificar-online-station')
def files(root):return {str(p.relative_to(root)):ops.digest(p) for p in root.rglob('*') if p.is_file()}
def write(path,value):ops.private_text(path,json.dumps(value,indent=2)+'\n')

def restore():
    if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.') or not ops.run(['psql','-Atqc','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('temporary cluster required')
    ops.run(['createdb','station_folders_restore']);ops.run(['pg_restore','--exit-on-error','--no-owner','--no-acl','--dbname','station_folders_restore',str(BACKUP/'database.dump')],timeout=300)
    if ops.run(['psql','--dbname','station_folders_restore','-Atqc',"SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()!='3':raise ValueError('restored ledger differs')
    print('Station folder database backup restored')

def rollback():
    state=json.loads((BACKUP/'state.json').read_text())
    if online.command_path() not in {OLD/DLL,Path(state['target'])/DLL} or ops.digest(OLD/DLL)!=OLD_SHA:
        raise ValueError('runtime superseded')
    for file,sha in state['unchangedConfigurations'].items():
        if ops.digest(Path(file))!=sha:raise ValueError('other Station configuration changed')
    for path,key in [(DROPIN,'override'),(SCAN,'scan')]:
        if path.read_text() not in {state[key],(BACKUP/path.name).read_text()}:raise ValueError('own configuration changed')
    ops.run(['systemctl','stop',TIMER]);ops.run(['systemctl','stop',SCAN.stem])
    ops.replace_config(DROPIN,(BACKUP/DROPIN.name).read_text());ops.replace_config(SCAN,(BACKUP/SCAN.name).read_text())
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
    ops.run(['systemctl','start',TIMER])
    if online.command_path()!=OLD/DLL:raise ValueError('automatic library API did not return')
    print('Previous automatic library API restored; N64 and online retained')

def apply(revision):
    report=dict(applied=False,sourceRevision=revision,service=SERVICE);activated=False;stage='preflight'
    try:
        if ops.run(['git','-c','safe.directory='+str(ROOT),'rev-parse','HEAD'],cwd=ROOT).strip()!=revision or ops.run(['git','-c','safe.directory='+str(ROOT),'status','--porcelain'],cwd=ROOT).strip():raise ValueError('exact clean source required')
        values,ids=ops.runtime()
        if online.command_path()!=OLD/DLL or ops.digest(OLD/DLL)!=OLD_SHA or ops.digest(INDEX)!=OLD_INDEX_SHA:raise ValueError('reviewed production changed')
        if BACKUP.exists() or RESULT.exists():raise ValueError('deployment already prepared')
        candidate=Path('/mnt/DADOS/station-api-folders-candidate-20261004-'+revision[:7]);target=Path('/opt/turborama-station-folders-20261004-'+revision[:7])
        meta=json.loads((candidate/'release.json').read_text());manifest=files(candidate);manifest.pop('release.json')
        if meta['sourceRevision']!=revision or meta['files']!=manifest or target.exists():raise ValueError('candidate differs')
        baseline={u:ops.state(u) for u in online.SHARED}
        if any('ActiveState=active' not in v for v in baseline.values()):raise ValueError('shared service is unhealthy')
        original=json.loads(INDEX.read_text());unchanged={str(p):ops.digest(p) for p in DROPIN.parent.glob('*.conf') if p!=DROPIN}
        for p in [Path('/etc/nginx/snippets/turborama-station.locations.conf'),Path('/etc/nginx/snippets/turborama-station-online.locations.conf')]:unchanged[str(p)]=ops.digest(p)
        override=DROPIN.read_text().replace(str(OLD),str(target));scan=SCAN.read_text().replace(str(OLD),str(target))
        stage='backup_restore';BACKUP.mkdir(mode=0o700)
        shutil.copytree(OLD,BACKUP/'api');shutil.copytree(BACKUP/'api',BACKUP/'restored-api')
        if files(BACKUP/'restored-api')!=files(OLD):raise ValueError('restored API differs')
        shutil.rmtree(BACKUP/'restored-api');shutil.copyfile(INDEX,BACKUP/'index7.json')
        for p in [DROPIN,SCAN]:shutil.copyfile(p,BACKUP/p.name)
        write(BACKUP/'state.json',dict(target=str(target),override=override,scan=scan,unchangedConfigurations=unchanged,sharedBaseline=baseline))
        with (BACKUP/'database.dump').open('xb') as f:
            p=subprocess.run(['runuser','-u','postgres','--','pg_dump','--format=custom','--dbname',ops.database_name(values)],stdout=f,stderr=subprocess.PIPE,timeout=300)
            if p.returncode:raise ValueError('backup failed')
        if 'backup restored' not in ops.run(['pg_virtualenv','-t','python3',str(Path(__file__).resolve()),'--restore-backup'],timeout=360):raise ValueError('restore failed')
        report['backupRestoreVerified']=True
        stage='candidate';shutil.copytree(candidate,target)
        for p in [target,*target.rglob('*')]:
            if p.is_symlink():raise ValueError('release link forbidden')
            os.chown(p,0,ids['Gid'][1]);p.chmod(0o750 if p.is_dir() else 0o640)
        if files(target)!=files(candidate):raise ValueError('installed hash differs')
        with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
        base='http://127.0.0.1:'+str(port);shadow=dict(values)
        for key in ['INVOCATION_ID','NOTIFY_SOCKET','LISTEN_FDS','LISTEN_PID','JOURNAL_STREAM']:shadow.pop(key,None)
        with (BACKUP/'candidate.log').open('xb') as f:
            p=subprocess.Popen(['/usr/bin/dotnet',str(target/DLL),'--urls',base],env=shadow,cwd=target,stdout=f,stderr=f,user=ids['Uid'][1],group=ids['Gid'][1],extra_groups=ids['Groups'])
            try:ops.ready(base,p);report['candidateVerification']=verification.verify(original,shadow,base,metadata_check=True)
            finally:
                p.terminate()
                try:p.wait(timeout=15)
                except subprocess.TimeoutExpired:p.kill();p.wait()
        print('R9 folder candidate and existing contracts verified',flush=True)
        stage='activate'
        if online.command_path()!=OLD/DLL or ops.digest(INDEX)!=OLD_INDEX_SHA or any(ops.state(u)!=v for u,v in baseline.items()):raise ValueError('production changed')
        activated=True;ops.run(['systemctl','stop',TIMER]);ops.run(['systemctl','stop',SCAN.stem])
        ops.replace_config(DROPIN,override);ops.replace_config(SCAN,scan);ops.run(['systemctl','daemon-reload'])
        if str(target/DLL) not in ops.run(['systemctl','show',SERVICE,'-p','ExecStart','--value']):raise ValueError('override precedence differs')
        ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
        ops.run(['systemctl','start',SCAN.stem],timeout=180)
        current=json.loads(INDEX.read_text());previous={r['itemId']:r for r in original['items']}
        for row in current['items']:
            if {k:v for k,v in row.items() if k!='folderPath'}!=previous[row['itemId']]:raise ValueError('content changed during folder import')
        # Give only the hosted 10-second index reader its real opportunity to publish.
        import time;time.sleep(11)
        running,_=ops.runtime()
        report['publicVerification']=verification.verify(current,running,'https://app.lzgames.com.br',metadata_check=True)
        ops.run(['systemctl','start',TIMER])
        if any(ops.state(u)!=v for u,v in baseline.items()) or any(ops.digest(Path(p))!=h for p,h in unchanged.items()):raise ValueError('shared state changed')
        report.update(applied=True,catalogRevision=current['revision'],visible=1973,n64=157,dllSha256=meta['dllSha256'],indexSha256=ops.digest(INDEX),execStart=str(target/DLL),folderPathPublished=True,autoReload=True,timerActive=True,sharedServicesUnchanged=True)
    except Exception as error:
        report.update(stage=stage,errorType=type(error).__name__)
        if BACKUP.exists():(BACKUP/'failure-diagnostic.txt').write_text(traceback.format_exc())
        if activated:
            try:rollback();report['rolledBack']=True
            except Exception as e:report['rollbackErrorType']=type(e).__name__
        write(RESULT,report);os.chown(RESULT,pwd.getpwnam('lz-servidor').pw_uid,pwd.getpwnam('lz-servidor').pw_gid);print(json.dumps(report));return 1
    write(BACKUP/'result.json',report);write(RESULT,report);os.chown(RESULT,pwd.getpwnam('lz-servidor').pw_uid,pwd.getpwnam('lz-servidor').pw_gid);print(json.dumps(report));return 0

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);g=p.add_mutually_exclusive_group(required=True);g.add_argument('--apply');g.add_argument('--rollback',action='store_true');g.add_argument('--restore-backup',action='store_true');a=p.parse_args()
    if os.geteuid()!=0:raise SystemExit('Native Linux administrator authentication required')
    os.umask(0o077);os.environ['GIT_OPTIONAL_LOCKS']='0'
    if a.restore_backup:restore()
    elif a.rollback:rollback()
    else:raise SystemExit(apply(a.apply))
