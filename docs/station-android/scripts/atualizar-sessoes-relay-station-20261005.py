#!/usr/bin/env python3
"""Station-only update from the verified live 0bf8a0e relay; retains WSS and keys."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time
from urllib.request import urlopen

ROOT=Path(__file__).resolve().parents[3]
def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
rollout=load('implantar-relay-station-20261005.py')
ops=rollout.ops
OLD=Path('/opt/turborama-station-relay-20261005-0bf8a0e')
OLD_SHA='2df28bfe573f2272e2dbcbc0899a8848b080cbfbdf744d6451e51e8eb0e62a1a'
RESULT=Path('/home/lz-servidor/station-relay-session-update-result-20261005.json')
def backup_path(revision):return Path('/mnt/DADOS/station-relay-session-update-backup-20261005-'+revision[:7])
def healthy():
    with urlopen('http://127.0.0.1:5192/ready/station/online',timeout=5) as r:return json.load(r)
def unchanged(state):
    for name,sha in state['configurations'].items():
        if ops.digest(Path(name))!=sha:raise ValueError('Existing Station configuration changed')
    if rollout.files(OLD)!=state['oldFiles']:raise ValueError('Previous verified API changed')
    if any(ops.state(unit)!=value for unit,value in state['shared'].items()):raise ValueError('Shared service changed')
def rollback(revision):
    state=json.loads((backup_path(revision)/'state.json').read_text())
    if rollout.current() not in (OLD/rollout.DLL,Path(state['target'])/rollout.DLL):raise ValueError('API superseded')
    unchanged(state)
    if rollout.DROPIN.read_text() not in (state['oldOverride'],state['newOverride']):raise ValueError('API override superseded')
    ops.replace_config(rollout.DROPIN,state['oldOverride'])
    ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',rollout.SERVICE]);ops.ready('http://127.0.0.1:5192')
    if rollout.current()!=OLD/rollout.DLL:raise ValueError('Previous relay did not return')
    print('Previously verified relay API restored; POCO and catalog retained',flush=True)
def apply(revision):
    report=dict(applied=False,sourceRevision=revision,migrationsApplied=0)
    activated=False;stage='preflight'
    try:
        if ops.run(['git','-c','safe.directory='+str(ROOT),'rev-parse','HEAD'],cwd=ROOT).strip()!=revision or ops.run(['git','-c','safe.directory='+str(ROOT),'status','--porcelain'],cwd=ROOT).strip():raise ValueError('Exact clean source required')
        original=json.loads(rollout.RESULT.read_text())
        if not original['applied'] or not original['backupRestoreVerified'] or original['sourceRevision']!='0bf8a0e8bbe338026c2f78afeba5ae5b30392c77':raise ValueError('Verified relay receipt required')
        if rollout.current()!=OLD/rollout.DLL or ops.digest(OLD/rollout.DLL)!=OLD_SHA:raise ValueError('Inspected relay API changed')
        if healthy()['activeRooms']:raise ValueError('Active games must finish before this restart')
        values,ids=ops.runtime()
        if any(values.get(key)!=value for key,value in {
            'Station__Online__RelayEnabled':'true','Station__Online__RelayMaxRooms':'512',
            'Station__LibraryVerifyContentOnLoad':'false','Station__OriginRequestsPerMinute':'2048',
            'Station__MaximumRateWindows':'32768'}.items()):raise ValueError('Existing capacity flags differ')
        if ops.digest(rollout.INDEX)!=rollout.INDEX_SHA or ops.digest(rollout.REGISTRY)!=rollout.REGISTRY_SHA:raise ValueError('Inspected library changed')
        backup=backup_path(revision);target=Path('/opt/turborama-station-relay-20261005-'+revision[:7])
        if any(p.exists() for p in (backup,target,RESULT)):raise ValueError('Update already prepared')
        candidate=Path('/mnt/DADOS/station-api-relay-candidate-20261005-'+revision[:7])
        metadata=json.loads((candidate/'release.json').read_text());files=rollout.files(candidate);files.pop('release.json')
        if metadata['sourceRevision']!=revision or metadata['files']!=files or metadata['dllSha256']!=files[rollout.DLL]:raise ValueError('Candidate manifest differs')
        configs=[p for p in rollout.DROPIN.parent.glob('*.conf') if p!=rollout.DROPIN]
        configs.extend([rollout.INDEX,rollout.REGISTRY,rollout.ONLINE_PROXY,rollout.RELAY_PROXY,
            Path('/etc/nginx/snippets/turborama-station.locations.conf')])
        old_override=rollout.DROPIN.read_text()
        state=dict(target=str(target),oldOverride=old_override,newOverride=old_override.replace(str(OLD),str(target)),
            oldFiles=rollout.files(OLD),configurations={str(p):ops.digest(p) for p in configs},
            shared={unit:ops.state(unit) for unit in rollout.online.SHARED})
        if any('ActiveState=active' not in value for value in state['shared'].values()):raise ValueError('Shared service unhealthy')
        if state['oldOverride']==state['newOverride']:raise ValueError('Expected release path absent')
        backup.mkdir(mode=0o700);shutil.copytree(OLD,backup/'api')
        if rollout.files(backup/'api')!=state['oldFiles']:raise ValueError('Previous API backup differs')
        ops.private_text(backup/'state.json',json.dumps(state))
        shutil.copytree(candidate,target)
        for p in (target,*target.rglob('*')):
            if p.is_symlink():raise ValueError('Candidate contains link')
            os.chown(p,0,ids['Gid'][1]);p.chmod(0o750 if p.is_dir() else 0o640)
        if rollout.files(target)!=rollout.files(candidate):raise ValueError('Installed candidate hashes differ')
        stage='shadow_candidate';check=load('verificar-relay-station.py');index=json.loads(rollout.INDEX.read_text())
        with socket.socket() as probe:probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
        base='http://127.0.0.1:'+str(port);env=dict(values)
        for key in ('INVOCATION_ID','NOTIFY_SOCKET','LISTEN_FDS','LISTEN_PID','LISTEN_FDNAMES','JOURNAL_STREAM'):env.pop(key,None)
        with (backup/'candidate.log').open('xb') as log:
            os.fchmod(log.fileno(),0o600)
            process=subprocess.Popen(['/usr/bin/dotnet',str(target/rollout.DLL),'--urls',base],cwd=target,env=env,
                stdout=log,stderr=log,user=ids['Uid'][1],group=ids['Gid'][1],extra_groups=ids['Groups'])
            try:
                start=time.monotonic();ops.ready(base,process);report['candidateStartupSeconds']=time.monotonic()-start
                report['candidateVerification']=check.verify(index,env,base,True)
            finally:
                process.terminate()
                try:process.wait(timeout=15)
                except subprocess.TimeoutExpired:process.kill();process.wait()
        print('Session update candidate verified; existing WSS proxy retained',flush=True)
        unchanged(state)
        if healthy()['activeRooms'] or rollout.current()!=OLD/rollout.DLL:raise ValueError('Live relay changed before update')
        stage='publish';activated=True;ops.replace_config(rollout.DROPIN,state['newOverride'])
        ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','restart',rollout.SERVICE]);ops.ready('http://127.0.0.1:5192')
        if rollout.current()!=target/rollout.DLL:raise ValueError('New API was not selected')
        current,_=ops.runtime();stage='public_verification';report['publicVerification']=check.verify(index,current,'https://app.lzgames.com.br',True)
        unchanged(state)
        report.update(applied=True,service=rollout.SERVICE,dllSha256=metadata['dllSha256'],execStart=str(target/rollout.DLL),
            previousSourceRevision=original['sourceRevision'],previousDllSha256=OLD_SHA,backup=str(backup),
            priorDatabaseBackupRestored=True,proxyUnchanged=True,sharedServicesPreserved=True,
            indexRevision=14,indexSha256=rollout.INDEX_SHA,engineRegistrySha256=rollout.REGISTRY_SHA,
            relayMaximumRooms=512,relayMaximumConnections=1024,onlineEnabled=True,relayEnabled=True,
            libraryVerifyContentOnLoad=False,originRequestsPerMinute=2048,maximumRateWindows=32768,
            pid=int(ops.run(['systemctl','show',rollout.SERVICE,'-p','MainPID','--value']).strip()),
            completedAtUtc=datetime.now(timezone.utc).isoformat())
        rollout.private_report(RESULT,report);print(json.dumps(report,ensure_ascii=False,indent=2),flush=True)
    except Exception as error:
        report.update(failedStage=stage,errorType=type(error).__name__)
        if isinstance(error,ValueError):report['safeReason']=str(error)
        if activated:
            try:rollback(revision);report['rolledBack']=True
            except Exception as problem:report.update(rolledBack=False,rollbackErrorType=type(problem).__name__)
        rollout.private_report(RESULT.with_name(RESULT.stem+'-failed-'+revision[:7]+'.json'),report)
        print(json.dumps(report,ensure_ascii=False,indent=2),flush=True);raise SystemExit(1)
def main():
    parser=argparse.ArgumentParser();group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apply');group.add_argument('--rollback');args=parser.parse_args()
    if os.geteuid()!=0:raise ValueError('Native root authentication required')
    if args.rollback:rollback(args.rollback)
    else:apply(args.apply)
if __name__=='__main__':main()
