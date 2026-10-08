#!/usr/bin/env python3
"""Qualified, reversible Station-only metadata instrumentation; preserve wire/security."""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import socket
import subprocess
import sys
import time
import traceback
from urllib.error import HTTPError
from urllib.request import Request, urlopen

ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(Path(__file__).parent))
from station_hardening_config import sandbox_properties,systemd_override

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

registration=load('ativar-registro-station-r74-20261007.py')
r71,security,ops,online=registration.r71,registration.security,registration.ops,registration.online
OLD=registration.TARGET;REGISTRY_SHA='a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843'
SERVICE,IDENTITY=registration.SERVICE,registration.IDENTITY
QUALIFICATION=Path('/mnt/DADOS/station-r74-observability-qualification-20261007')
CHECK=Path('/mnt/DADOS/station-r74-observability-check-20261007')
DROPIN=Path('/etc/systemd/system')/(SERVICE+'.d')/'zzzzzzzzzzzzzzzzzz-station-observations-r74-20261007.conf'

def utc():return datetime.now(timezone.utc).isoformat()

def report(label,value):
    if CHECK.is_symlink():raise ValueError('Report directory link refused')
    CHECK.mkdir(mode=0o700,exist_ok=True);owner=pwd.getpwnam('lz-servidor')
    os.chown(CHECK,owner.pw_uid,owner.pw_gid);CHECK.chmod(0o700)
    path=CHECK/(label+'-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+'.json')
    ops.private_text(path,json.dumps(value,indent=2)+'\n');os.chown(path,owner.pw_uid,owner.pw_gid)
    return str(path)

def candidate(revision):
    git=['git','-c','safe.directory='+str(ROOT)]
    if not re.fullmatch('[0-9a-f]{40}',revision) or ops.run(git+['rev-parse','HEAD'],cwd=ROOT).strip()!=revision:
        raise ValueError('Exact reviewed source required')
    receipt='docs/station-android/entrega-app-r71-20261007/NAVIGATION-AUDIT.md'
    for line in ops.run(git+['status','--porcelain'],cwd=ROOT).splitlines():
        if line!=' M '+receipt or subprocess.check_output(git+['show','HEAD:'+receipt],cwd=ROOT)!=(ROOT/receipt).read_bytes():
            raise ValueError('Uncommitted operator/source refused')
    folder=QUALIFICATION/'server-publish';meta=json.loads((QUALIFICATION/'release.json').read_text())
    if meta['sourceRevision']!=revision or meta['files']!=security.files(folder) or meta['dllSha256']!=ops.digest(folder/'TurboRamaSuiteOnlineServer.dll'):
        raise ValueError('Sealed candidate differs')
    target=Path('/opt/turborama-station-observations-r74-20261007-'+revision[:7])
    return folder,meta,target

def baseline():
    values=registration.effective(REGISTRY_SHA);registration.environment_fingerprint(values)
    db=r71.database(values)
    state=dict(utc=utc(),database=db,configuration=security.snapshot_configs(),keys={str(p):ops.digest(p) for p in registration.ENV.parent.iterdir() if p.is_file()},
        releaseFiles=security.files(OLD),registry=json.loads(registration.REGISTRY.read_text()),shared={u:ops.state(u) for u in security.SHARED},
        realLicenseSha256=security.real_licenses(db),ledgerSha256=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),
        explicitValues={k:v for k,v in values.items() if k.startswith(('Station__','Suite__','ConnectionStrings__'))},
        pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value'])),ready=security.telemetry())
    if len(state['registry'])!=10 or len({e['id'] for e in state['registry']})!=10:raise ValueError('Exact ten engines required')
    security.database_scope(db);security.management_health()
    return state,values

def unchanged(state):
    if security.files(OLD)!=state['releaseFiles'] or ops.digest(security.INDEX)!=security.INDEX_SHA:
        raise ValueError('Original release/catalog changed')
    for name,digest in {**state['configuration'],**state['keys']}.items():
        if ops.digest(Path(name))!=digest:raise ValueError('Original configuration/key changed')
    for name,status in state['shared'].items():
        if ops.state(name)!=status:raise ValueError('Another product process changed')
    db=state['database']
    if security.real_licenses(db)!=state['realLicenseSha256'] or hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest()!=state['ledgerSha256']:
        raise ValueError('Real licenses or schema changed')
    security.database_scope(db);security.management_health()

def diagnostics(base):
    with urlopen(base+'/ready/station/online/diagnostics',timeout=8) as response:
        data=response.read(16*1024*1024+1)
        if len(data)>16*1024*1024 or response.headers.get('Cache-Control')!='no-store':raise ValueError('Diagnostics response bounds/cache differ')
        value=json.loads(data)
    if value.get('version')!=1 or value.get('enabled') is not True or value.get('persistent') is not False:
        raise ValueError('Expected metadata diagnostics unavailable')
    for headers in ({'Host':'app.lzgames.com.br'},{'X-Forwarded-For':'192.0.2.1'},{'Forwarded':'for=192.0.2.1'},{'CF-Connecting-IP':'192.0.2.1'}):
        try:
            with urlopen(Request(base+'/ready/station/online/diagnostics',headers=headers),timeout=5) as response:raise ValueError('Forwarded/public-host diagnostics accepted')
        except HTTPError as error:
            if error.code!=404:raise ValueError('Operator-only diagnostics guard differs')
    return value

def install_candidate(revision,state):
    folder,meta,target=candidate(revision);identity=pwd.getpwnam(IDENTITY)
    text='Station__Online__EngineRegistryFile='+str(target/'online-engine-registry.json')+'\nStation__Online__RecoveryDiagnosticsEnabled=true\n'
    expected=dict(meta['files'],**{'online-engine-registry.json':REGISTRY_SHA,'observations.env':hashlib.sha256(text.encode()).hexdigest()})
    if target.is_symlink():raise ValueError('Release directory link refused')
    if not target.exists():
        shutil.copytree(folder,target);shutil.copyfile(registration.REGISTRY,target/'online-engine-registry.json')
        ops.private_text(target/'observations.env',text)
        for path in [target,*target.rglob('*')]:os.chown(path,0,identity.pw_gid);path.chmod(0o750 if path.is_dir() else 0o640)
    if security.files(target)!=expected or json.loads((target/'online-engine-registry.json').read_text())!=state['registry']:
        raise ValueError('Installed candidate bytes/engines differ')
    return meta,target

def verify(state,values,base,target,observations_base):
    probe=dict(values,Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'))
    additions=json.loads(registration.ADDITIONS.read_text());ids=[e['id'] for e in state['registry'] if e['id'] not in registration.NEW_IDS]
    return load('verificar-registro-station-r74.py').verify(json.loads(security.INDEX.read_text()),probe,base,
        lambda sql:ops.sql(state['database'],sql),additions,ids,observe=lambda:diagnostics(observations_base))

def qualify(revision,state,values):
    meta,target=install_candidate(revision,state);CHECK.mkdir(mode=0o700,exist_ok=True)
    stamp=datetime.now(timezone.utc).strftime('%H%M%S%f');log=CHECK/('shadow-'+stamp+'.log');env=log.with_suffix('.env')
    ops.private_text(log,'');ops.private_text(env,registration.ENV.read_text()+'\n'+registration.OVERLAY.read_text()+'\n'+(target/'observations.env').read_text())
    with socket.socket() as probe:probe.bind(('127.0.0.1',0));port=probe.getsockname()[1]
    base='http://127.0.0.1:'+str(port);unit='station-r74-observations-shadow-'+stamp+'.service'
    command=['systemd-run','--quiet','--collect','--unit',unit]
    for name,value in sandbox_properties(target,IDENTITY,security.MEDIA).items():command+=['--property',name+'='+value]
    command+=['--property','EnvironmentFile='+str(env),'--property','WorkingDirectory='+str(target),'--property','StandardOutput=append:'+str(log),
              '--property','StandardError=append:'+str(log),'/usr/bin/dotnet',str(target/'TurboRamaSuiteOnlineServer.dll'),'--urls',base]
    print(json.dumps(dict(stage='isolated_observations_qualification',productionChanged=False)),flush=True)
    started=False
    try:
        ops.run(command);started=True;ready=r71.ready_recovery(base)
        pid=int(ops.run(['systemctl','show',unit,'-p','MainPID','--value']))
        sandbox=r71.sandbox(pid,target,json.loads(security.INDEX.read_text()))
        proof=verify(state,values,base,target,base)
    finally:
        if started:
            result=subprocess.run(['systemctl','stop',unit],capture_output=True,timeout=30)
            if result.returncode and ops.run(['systemctl','show',unit,'-p','MainPID','--value']).strip() not in ('','0'):raise ValueError('Shadow did not stop')
        env.unlink(missing_ok=True)
    unchanged(state)
    result=dict(passed=True,sourceRevision=revision,dllSha256=meta['dllSha256'],target=str(target),registrySha256=REGISTRY_SHA,
        ready=ready,sandbox=sandbox,proof=proof,utc=utc(),productionChanged=False)
    record=report('qualified',result);print(json.dumps(dict(qualified=True,checks=proof['checks'],record=record)),flush=True)
    return result

def restore(state,override_hash):
    registration.idle_period();unchanged(state)
    if DROPIN.is_symlink() or ops.digest(DROPIN)!=override_hash:raise ValueError('Observation drop-in was superseded')
    DROPIN.unlink();ops.run(['systemctl','daemon-reload']);registration.idle()
    ops.run(['systemctl','restart',SERVICE]);r71.ready_recovery('http://127.0.0.1:5192')
    registration.effective(REGISTRY_SHA);unchanged(state)
    return dict(passed=True,databaseRestored=False,previousDllSha256=registration.DLL_SHA)

def apply(revision):
    state,values=baseline();qualified=qualify(revision,state,values);meta,target=install_candidate(revision,state)
    if DROPIN.exists():raise ValueError('Observation override already exists')
    backup=Path('/mnt/DADOS/station-r74-observations-backup-20261007-'+datetime.now(timezone.utc).strftime('%H%M%S%f'))
    backup.mkdir(mode=0o700)
    override=systemd_override(target,IDENTITY,security.MEDIA,registration.ENV)+'EnvironmentFile='+str(registration.OVERLAY)+'\nEnvironmentFile='+str(target/'observations.env')+'\n'
    state.update(target=str(target),sourceRevision=revision,dropinSha256=hashlib.sha256(override.encode()).hexdigest())
    ops.private_text(backup/'state.json',json.dumps(state,indent=2)+'\n')
    changed=False;restarted=False
    try:
        unchanged(state);registration.effective(REGISTRY_SHA);before=registration.idle_period()
        print(json.dumps(dict(stage='idle_station_observations_reload',roomsBefore=before)),flush=True)
        ops.private_text(DROPIN,override);DROPIN.chmod(0o644);changed=True
        ops.run(['systemctl','daemon-reload']);registration.idle()
        ops.run(['systemctl','restart',SERVICE]);restarted=True;reloaded=utc();ready=r71.ready_recovery('http://127.0.0.1:5192')
        running,ids=ops.runtime();identity=pwd.getpwnam(IDENTITY)
        expected=dict(state['explicitValues'],Station__Online__EngineRegistryFile=str(target/'online-engine-registry.json'),Station__Online__RecoveryDiagnosticsEnabled='true')
        effective={k:v for k,v in running.items() if k.startswith(('Station__','Suite__','ConnectionStrings__'))}
        if effective!=expected or online.command_path()!=target/'TurboRamaSuiteOnlineServer.dll' or ids['Uid'][1]!=identity.pw_uid or ids['Gid'][1]!=identity.pw_gid:
            raise ValueError('Effective application identity/settings changed')
        proof=verify(state,running,'https://app.lzgames.com.br',target,'http://127.0.0.1:5192')
        pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value']));sandbox=r71.sandbox(pid,target,json.loads(security.INDEX.read_text()))
        unchanged(state);observations=diagnostics('http://127.0.0.1:5192')
        result=dict(applied=True,utc=utc(),reloadUtc=reloaded,sourceRevision=revision,dllSha256=meta['dllSha256'],dllPath=str(target/'TurboRamaSuiteOnlineServer.dll'),
            registrySha256=REGISTRY_SHA,engines=state['registry'],pid=pid,qualified=qualified,publicProof=proof,sandbox=sandbox,observations=observations,
            ready=ready,readyAfter=security.telemetry(),backup=str(backup),idleBeforeReload=before,oldTenEnginesPreserved=True,
            originalReleasePreserved=True,oldConfigurationPreserved=True,securityPolicyPreserved=True,originalKeysPreserved=True,realLicensesPreserved=True,
            databaseSchemaChanged=False,otherProductsChanged=False,nginxChanged=False,cloudflareChanged=False,firewallChanged=False,androidGameplayQualified=False,
            changes=['Versioned Station DLL with bounded monotonic diagnostics','Same ten-engine bytes at new release path','Own reversible Station service override'])
        record=report('active',result);print(json.dumps(dict(applied=True,pid=pid,checks=proof['checks'],sourceRevision=revision,record=record)),flush=True)
    except Exception as error:
        result=dict(applied=False,errorType=type(error).__name__,changed=changed,restarted=restarted,backup=str(backup))
        if changed:
            try:
                if not restarted and online.command_path()==registration.DLL and ops.digest(DROPIN)==state['dropinSha256']:
                    DROPIN.unlink();ops.run(['systemctl','daemon-reload']);unchanged(state);result['rollback']=dict(passed=True,restarted=False)
                else:result['rollback']=restore(state,state['dropinSha256'])
            except Exception as failure:result['rollback']=dict(passed=False,errorType=type(failure).__name__,liveSessionNeverForciblyEnded=True)
        record=report('failure',result);ops.private_text(CHECK/('diagnostic-'+datetime.now(timezone.utc).strftime('%H%M%S%f')+'.txt'),traceback.format_exc())
        print(json.dumps(dict(applied=False,errorType=type(error).__name__,rollback=result.get('rollback'),record=record)),flush=True);raise

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply');parser.add_argument('--qualify');parser.add_argument('--rollback',type=Path);args=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise SystemExit('Native Linux operator authentication required')
    try:
        if args.apply:apply(args.apply)
        elif args.qualify:
            state,values=baseline();qualify(args.qualify,state,values)
        elif args.rollback:
            if args.rollback.parent!=Path('/mnt/DADOS') or args.rollback.is_symlink() or not re.fullmatch('station-r74-observations-backup-20261007-[0-9]{12}',args.rollback.name):raise ValueError('Unexpected backup')
            state=json.loads((args.rollback/'state.json').read_text());print(json.dumps(restore(state,state['dropinSha256'])),flush=True)
        else:raise ValueError('Qualified action required')
    except Exception as error:
        print(json.dumps(dict(passed=False,errorType=type(error).__name__)),flush=True);raise SystemExit(1)

if __name__=='__main__':main()
