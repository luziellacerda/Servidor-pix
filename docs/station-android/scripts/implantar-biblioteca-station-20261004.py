#!/usr/bin/env python3
"""Bounded Station-only automatic library rollout; backup, restored DB, shadow, HTTPS, rollback."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import pwd
import shutil
import socket
import subprocess

ROOT=Path(__file__).resolve().parents[3]
SERVICE='turborama-station-api.service'
OLD=Path('/opt/turborama-station-online-20261004-77d1dfb')
OLD_SHA='ff6362852635d4d18a01e85e46c89ad5cc2a7dad75d3b99793a124733beb6509'
OLD_INDEX_SHA='c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a'
CONTENT=Path('/mnt/DADOS/turbostation-library-auto-20261004')
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
BACKUP=Path('/mnt/DADOS/station-library-auto-backup-20261004')
DROPIN=Path('/etc/systemd/system/'+SERVICE+'.d/zzzzz-station-library-20261004.conf')
SCAN='turborama-station-library-scan'
SCAN_SERVICE=Path('/etc/systemd/system/'+SCAN+'.service')
SCAN_TIMER=Path('/etc/systemd/system/'+SCAN+'.timer')
DLL='TurboRamaSuiteOnlineServer.dll'
RESULT=Path('/home/lz-servidor/station-library-auto-rollout-result-20261004.json')

def module(file):
    spec=importlib.util.spec_from_file_location(file.replace('-','_'),Path(__file__).with_name(file+'.py'))
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value

ops=module('implantar-station-20261003')
online=module('implantar-online-station-20261004')
online.ops=ops
verification=module('verificar-online-station')

def manifest(directory):
    return {str(p.relative_to(directory)):ops.digest(p) for p in directory.rglob('*') if p.is_file()}

def write(path,data): ops.private_text(path,json.dumps(data,ensure_ascii=False,indent=2)+'\n')

def restore_database():
    location=os.environ.get('PG_CLUSTER_CONF_ROOT','')
    if not location.startswith('/tmp/pg_virtualenv.') or not Path(location).is_dir(): raise ValueError('isolated restore required')
    if not ops.run(['psql','-Atqc','SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'): raise ValueError('temporary database required')
    ops.run(['createdb','station_auto_restore'])
    ops.run(['pg_restore','--exit-on-error','--no-owner','--no-acl','--dbname','station_auto_restore',str(BACKUP/'database.dump')],timeout=300)
    count=ops.run(['psql','--dbname','station_auto_restore','-Atqc',"SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()
    if count!='3': raise ValueError('restored ledger differs')
    print('Station automatic library database restore verified',flush=True)

def rollback():
    state=json.loads((BACKUP/'state.json').read_text())
    current=online.command_path()
    if current not in {OLD/DLL,Path(state['target'])/DLL} or ops.digest(OLD/DLL)!=OLD_SHA:
        raise ValueError('Station runtime was superseded')
    for file,sha in state['baselineFiles'].items():
        if ops.digest(Path(file))!=sha: raise ValueError('existing Station configuration changed')
    for path,text in [(DROPIN,state['override']),(SCAN_SERVICE,state['scanService']),(SCAN_TIMER,state['scanTimer'])]:
        if path.exists() and (path.is_symlink() or path.read_text()!=text): raise ValueError('own configuration was changed')
    ops.run(['systemctl','daemon-reload'])
    if SCAN_TIMER.exists():
        ops.run(['systemctl','stop',SCAN+'.timer'])
        ops.run(['systemctl','disable',SCAN+'.timer'])
    if SCAN_SERVICE.exists():ops.run(['systemctl','stop',SCAN+'.service'])
    for path in [DROPIN,SCAN_SERVICE,SCAN_TIMER]:path.unlink(missing_ok=True)
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
    values,_=ops.runtime()
    if online.command_path()!=OLD/DLL or ops.digest(Path(values['Station__LibraryIndexFile']))!=OLD_INDEX_SHA:
        raise ValueError('previous Station API/catalog did not return')
    print('Station online API and revision 4 restored; N64 source files retained',flush=True)

def apply(revision):
    report=dict(applied=False,service=SERVICE,sourceRevision=revision,utc=datetime.now(timezone.utc).isoformat())
    activated=False;stage='preflight'
    try:
        if ops.run(['git','-c','safe.directory='+str(ROOT),'rev-parse','HEAD'],cwd=ROOT).strip()!=revision or \
            ops.run(['git','-c','safe.directory='+str(ROOT),'status','--porcelain'],cwd=ROOT).strip():
            raise ValueError('exact clean source commit required')
        values,ids=ops.runtime();index_file=Path(values['Station__LibraryIndexFile'])
        if online.command_path()!=OLD/DLL or ops.digest(OLD/DLL)!=OLD_SHA or ops.digest(index_file)!=OLD_INDEX_SHA:
            raise ValueError('live Station differs from reviewed baseline')
        if RESULT.exists():
            prior=json.loads(RESULT.read_text())
            if prior.get('applied') is not False:raise ValueError('previous successful rollout cannot be repeated')
            archive=RESULT.with_name(RESULT.stem+'-failed-'+ops.digest(RESULT)[:12]+'.json')
            RESULT.rename(archive)
        candidate=Path('/mnt/DADOS/station-api-library-candidate-20261004-'+revision[:7])
        release=json.loads((candidate/'release.json').read_text());expected=manifest(candidate);expected.pop('release.json')
        if release['sourceRevision']!=revision or release['files']!=expected: raise ValueError('candidate hash manifest differs')
        target=Path('/opt/turborama-station-library-20261004-'+revision[:7])
        if target.exists() or BACKUP.exists() or any(p.exists() for p in [DROPIN,SCAN_SERVICE,SCAN_TIMER]): raise ValueError('rollout target already exists')
        baseline={unit:ops.state(unit) for unit in online.SHARED}
        if any('ActiveState=active' not in state for state in baseline.values()): raise ValueError('shared service is unhealthy')
        index=json.loads((CONTENT/'index.json').read_text());old=json.loads(index_file.read_text());new_ids={r['itemId']:r for r in index['items']}
        for row in old['items']:
            actual=new_ids[row['itemId']]
            if any(actual[k]!=row[k] for k in ['itemId','coverId','revision','platform','name','artifact','filePath','coverPath','catalogVisible']):
                raise ValueError('existing game, cover or descriptor changed')
        if len(index['items'])!=2227 or sum(r.get('catalogVisible',True) for r in index['items'])!=1972:
            raise ValueError('reviewed catalog counts differ')
        index_sha=ops.digest(CONTENT/'index.json')
        baseline_files={str(p):ops.digest(p) for p in Path('/etc/systemd/system/'+SERVICE+'.d').glob('*.conf')}
        for p in [Path('/etc/nginx/snippets/turborama-station.locations.conf'),Path('/etc/nginx/snippets/turborama-station-online.locations.conf')]:baseline_files[str(p)]=ops.digest(p)
        override='[Service]\nWorkingDirectory='+str(target)+'\nExecStart=\nExecStart=/usr/bin/dotnet '+str(target/DLL)+'\nEnvironment=Station__LibraryIndexFile='+str(CONTENT/'index.json')+'\nEnvironment=Station__LibraryAutoReload=true\n'
        scan_service='[Unit]\nDescription=TurboStation automatic ROM and revista catalog\nAfter=local-fs.target\n[Service]\nType=oneshot\nUser=root\nGroup=root\nUMask=0077\nExecStart=/usr/bin/python3 '+str(target/'library-tools/atualizar-biblioteca-station.py')+' --config '+str(CONFIG)+'\nTimeoutStartSec=20min\nNice=10\nIOSchedulingClass=best-effort\nIOSchedulingPriority=7\nNoNewPrivileges=true\nProtectSystem=strict\nProtectHome=true\nPrivateTmp=true\nReadWritePaths='+str(CONTENT)+'\n'
        scan_timer='[Unit]\nDescription=Check TurboStation game folders each minute\n[Timer]\nOnBootSec=1min\nOnUnitInactiveSec=1min\nAccuracySec=5s\nUnit='+SCAN+'.service\n[Install]\nWantedBy=timers.target\n'
        stage='backup_restore';BACKUP.mkdir(mode=0o700)
        previous_manifest=manifest(OLD);shutil.copytree(OLD,BACKUP/'previous-api');shutil.copytree(BACKUP/'previous-api',BACKUP/'restore-check')
        if manifest(BACKUP/'restore-check')!=previous_manifest:raise ValueError('restored API backup hash differs')
        shutil.rmtree(BACKUP/'restore-check');shutil.copyfile(index_file,BACKUP/'previous-index.json')
        if ops.digest(BACKUP/'previous-index.json')!=OLD_INDEX_SHA:raise ValueError('index backup differs')
        for i,path in enumerate(baseline_files):shutil.copyfile(path,BACKUP/('config-'+str(i)+'.conf'))
        state=dict(target=str(target),override=override,scanService=scan_service,scanTimer=scan_timer,baselineFiles=baseline_files,sharedBaseline=baseline,
                   originalIndex=str(index_file),dllSha256=release['dllSha256'],initialIndexSha256=index_sha)
        write(BACKUP/'state.json',state)
        with (BACKUP/'database.dump').open('xb') as dump:
            os.fchmod(dump.fileno(),0o600)
            process=subprocess.run(['runuser','-u','postgres','--','pg_dump','--format=custom','--dbname',ops.database_name(values)],stdout=dump,stderr=subprocess.PIPE,timeout=300)
            if process.returncode:raise ValueError('database backup failed')
        restored=ops.run(['pg_virtualenv','-t','python3',str(Path(__file__).resolve()),'--restore-database'],timeout=360)
        if 'database restore verified' not in restored:raise ValueError('database restore failed')
        report['backupRestoreVerified']=True;print('Station backup and restore verified',flush=True)
        stage='candidate';shutil.copytree(candidate,target)
        for path in [target,*target.rglob('*')]:
            if path.is_symlink():raise ValueError('release links forbidden')
            os.chown(path,0,ids['Gid'][1]);path.chmod(0o750 if path.is_dir() else 0o640)
        if manifest(target)!=manifest(candidate):raise ValueError('installed release hash differs')
        with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        base='http://127.0.0.1:'+str(port);shadow=dict(values,Station__LibraryIndexFile=str(CONTENT/'index.json'),Station__LibraryAutoReload='true')
        for name in ['INVOCATION_ID','NOTIFY_SOCKET','LISTEN_FDS','LISTEN_PID','LISTEN_FDNAMES','JOURNAL_STREAM']:shadow.pop(name,None)
        with (BACKUP/'candidate.log').open('xb') as log:
            os.fchmod(log.fileno(),0o600)
            process=subprocess.Popen(['/usr/bin/dotnet',str(target/DLL),'--urls',base],env=shadow,cwd=target,stdout=log,stderr=log,
                                     user=ids['Uid'][1],group=ids['Gid'][1],extra_groups=ids['Groups'])
            try:ops.ready(base,process);report['candidateVerification']=verification.verify(index,shadow,base,metadata_check=True)
            finally:
                process.terminate()
                try:process.wait(timeout=15)
                except subprocess.TimeoutExpired:process.kill();process.wait()
        print('Station candidate under service UID verified',flush=True)
        stage='activate'
        if online.command_path()!=OLD/DLL or ops.digest(CONTENT/'index.json')!=index_sha or any(ops.state(u)!=v for u,v in baseline.items()):
            raise ValueError('production changed during candidate tests')
        for p,text in [(DROPIN,override),(SCAN_SERVICE,scan_service),(SCAN_TIMER,scan_timer)]:ops.private_text(p,text);p.chmod(0o644)
        activated=True;ops.run(['systemctl','daemon-reload'])
        if str(target/DLL) not in ops.run(['systemctl','show',SERVICE,'-p','ExecStart','--value']):raise ValueError('Station override order differs')
        ops.run(['systemctl','restart',SERVICE]);ops.ready('http://127.0.0.1:5192')
        running,_=ops.runtime()
        if online.command_path()!=target/DLL or running.get('Station__LibraryAutoReload')!='true':raise ValueError('wrong API enabled')
        stage='public_https';report['publicVerification']=verification.verify(index,running,'https://app.lzgames.com.br',metadata_check=True)
        ops.run(['systemctl','enable','--now',SCAN+'.timer']);ops.run(['systemctl','start',SCAN+'.service'],timeout=180)
        if ops.digest(CONTENT/'index.json')!=index_sha:raise ValueError('unchanged rescan republished the index')
        if any(ops.state(u)!=v for u,v in baseline.items()):raise ValueError('shared services changed')
        for file,sha in baseline_files.items():
            if ops.digest(Path(file))!=sha:raise ValueError('existing Station configuration changed')
        report.update(applied=True,rolledBack=False,catalogRevision=index['revision'],visibleGames=1972,n64Games=156,
                      dllSha256=release['dllSha256'],indexSha256=index_sha,sharedServicesUnchanged=True,unchangedScanVerified=True,
                      autoReload=True,timerActive=True,execStart=str(target/DLL),p2pGameVerified=False)
    except Exception as error:
        report.update(stage=stage,errorType=type(error).__name__)
        if activated:
            try:rollback();report['rolledBack']=True
            except Exception as rollback_error:report['rollbackErrorType']=type(rollback_error).__name__
        if BACKUP.exists():write(BACKUP/'failure.json',report)
        write(RESULT,report);os.chown(RESULT,pwd.getpwnam('lz-servidor').pw_uid,pwd.getpwnam('lz-servidor').pw_gid)
        print(json.dumps(report));return 1
    write(BACKUP/'result.json',report);write(RESULT,report);os.chown(RESULT,pwd.getpwnam('lz-servidor').pw_uid,pwd.getpwnam('lz-servidor').pw_gid)
    print(json.dumps(report));return 0

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apply',metavar='FULL_COMMIT');group.add_argument('--rollback',action='store_true');group.add_argument('--restore-database',action='store_true')
    args=parser.parse_args()
    if os.geteuid()!=0:raise SystemExit('Native Linux administrator authentication required')
    os.umask(0o077)
    os.environ['GIT_OPTIONAL_LOCKS']='0'
    if args.restore_database:restore_database()
    elif args.rollback:rollback()
    else:raise SystemExit(apply(args.apply))
