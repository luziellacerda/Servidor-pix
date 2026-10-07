#!/usr/bin/env python3
"""Add exactly the two received R73 engines; keep the deployed DLL and policies.

Native polkit authentication is required. Never restarts a live/recoverable relay.
Qualification uses an isolated Station process. Only the effective registry is
atomically replaced; environment, sandbox, schema and other products stay intact.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import re
import socket
import subprocess
import sys
import time
import traceback
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(Path(__file__).parent))
from station_hardening_config import sandbox_properties


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


r71 = load('implantar-recovery-station-r71-20261007.py')
security, ops, online = r71.security, r71.ops, r71.online
SERVICE = 'turborama-station-api.service'
IDENTITY = 'turborama-station-api'
TARGET = Path('/opt/turborama-station-recovery-r71-20261007-ab192bf')
DLL = TARGET / 'TurboRamaSuiteOnlineServer.dll'
DLL_SHA = '815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243'
REGISTRY = TARGET / 'online-engine-registry.json'
OLD_SHA = '310fefece80c336840882d0c91235b765d59f160246df947393802ea0211d289'
ENV = Path('/etc/turborama-station-api-security-20261007/station.env')
OVERLAY = ENV.with_name('recovery-r71-ab192bf.env')
ADDITIONS = ROOT / 'docs/station-android/entrega-app-r73-20261007/evidence/server-engine-registry-additions.json'
ADDITIONS_SHA = 'a3066af1ac72e1c93e919aecef61c3a954fe1a0d29dd5a77fa6073ffb9753e0b'
APP_COMMIT = '5657dce678609f25501321e307839a6e0c018d4e'
RUNTIME_SHA = '9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2'
NEW_IDS = ['bsnes-mercury-performance-79d7f9de-rs3-9af2778898e4', 'clownmdemu-d43c2708-rs3-9af2778898e4']
CHECK = Path('/mnt/DADOS/station-r73-registry-check-20261007')
QUALIFIED = Path('/mnt/DADOS/station-r73-qualified-registry-20261007')
CLOSED_TRIAL = None


def now():
    return datetime.now(timezone.utc).isoformat()


def report(label, value):
    if CHECK.is_symlink():
        raise ValueError('Report link refused')
    CHECK.mkdir(mode=0o700, exist_ok=True)
    owner = pwd.getpwnam('lz-servidor')
    os.chown(CHECK, owner.pw_uid, owner.pw_gid)
    CHECK.chmod(0o700)
    destination = CHECK / (label + '-' + datetime.now(timezone.utc).strftime('%H%M%S%f') + '.json')
    ops.private_text(destination, json.dumps(value, indent=2) + '\n')
    os.chown(destination, owner.pw_uid, owner.pw_gid)
    return str(destination)


def effective(registry_sha):
    if online.command_path() != DLL or ops.digest(DLL) != DLL_SHA or REGISTRY.is_symlink():
        raise ValueError('Deployed Station release changed')
    values, ids = ops.runtime()
    identity = pwd.getpwnam(IDENTITY)
    if ids['Uid'][1] != identity.pw_uid or ids['Gid'][1] != identity.pw_gid:
        raise ValueError('Station identity changed')
    if values.get('Station__Online__EngineRegistryFile') != str(REGISTRY) or ops.digest(REGISTRY) != registry_sha:
        raise ValueError('Effective engine registry changed')
    expected = {'Station__Online__RecoveryEnabled': 'true', 'Station__Online__RecoveryMaxRooms': '64',
                'Station__Online__RecoveryWindowBytes': '262144', 'Station__LibraryIndexFile': str(security.INDEX)}
    if any(values.get(k) != v for k, v in expected.items()):
        raise ValueError('Recovery/library settings changed')
    if values.get('Station__Security__RequireVerifiedApp', 'false').lower() != 'false':
        raise ValueError('App compatibility policy changed')
    if any(k.startswith('Suite__') and k != 'Suite__Enabled' for k in values) or 'ConnectionStrings__SuiteStore' in values:
        raise ValueError('Unrelated product credentials found')
    if ops.digest(security.INDEX) != security.INDEX_SHA:
        raise ValueError('Catalog changed')
    return values


def capture():
    values = effective(OLD_SHA)
    if ops.digest(ADDITIONS) != ADDITIONS_SHA:
        raise ValueError('Received R73 additions changed')
    old = json.loads(REGISTRY.read_text())
    new = json.loads(ADDITIONS.read_text())
    if len(old) != 6 or len({e['id'] for e in old}) != 6 or [e['id'] for e in new] != NEW_IDS:
        raise ValueError('Exact six plus two identities required')
    if any(e['runtimeSha256'] != RUNTIME_SHA or e['recoveryProtocol'] != 'station-stream.v2' for e in new):
        raise ValueError('Exact Windows runtime/protocol required')
    if set(NEW_IDS) & {e['id'] for e in old}:
        raise ValueError('Engine identity collision')
    db = r71.database(values)
    files = security.files(TARGET)
    state = dict(utc=now(), database=db, oldEngines=old, additions=new,
                 oldRegistrySha256=OLD_SHA, releaseFiles=files,
                 environmentSha256=hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest(),
                 configuration=security.snapshot_configs(), keys={str(p): ops.digest(p) for p in ENV.parent.iterdir() if p.is_file()},
                 shared={u: ops.state(u) for u in security.SHARED}, realLicenseSha256=security.real_licenses(db),
                 ledgerSha256=hashlib.sha256(security.scalar(db, 'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),
                 databaseScope=security.database_scope(db), management=security.management_health(), ready=security.telemetry())
    return state, values


def unchanged(state, registry_sha):
    values = effective(registry_sha)
    if hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest() != state['environmentSha256']:
        raise ValueError('Effective environment changed')
    files = security.files(TARGET)
    expected = dict(state['releaseFiles'], **{'online-engine-registry.json': registry_sha})
    if files != expected:
        raise ValueError('Other release bytes changed')
    for name, digest in {**state['configuration'], **state['keys']}.items():
        if ops.digest(Path(name)) != digest:
            raise ValueError('Original configuration/key changed')
    for unit, status in state['shared'].items():
        if ops.state(unit) != status:
            raise ValueError('Other product service changed')
    db = state['database']
    if security.real_licenses(db) != state['realLicenseSha256'] or hashlib.sha256(security.scalar(db,
            'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest() != state['ledgerSha256']:
        raise ValueError('License state or schema changed')
    security.database_scope(db)
    security.management_health()
    return values


def idle():
    value = security.telemetry()
    recovery = value['recovery']
    if value['activeRooms'] or value['activeConnections'] or recovery['activeConnections'] or recovery['pendingBytes']:
        raise ValueError('Live or retained session: activation deferred')
    if recovery['retainedRooms']:
        # The maintainer explicitly ended the stuck R72 trial. The current
        # implementation retains even terminal rooms. Permit only that exact
        # last trial, already unrecoverable and with zero attachments/bytes.
        # Never infer human exit just from an empty socket count or elapsed time.
        if recovery['retainedRooms'] != 1 or CLOSED_TRIAL != '2026-10-07T22:48:19.238974+00:00':
            raise ValueError('Recoverable retained session: activation deferred')
        raw = ops.run(['journalctl', '--unit', SERVICE, '--since', '2026-10-07 22:48:00 UTC',
                       '--no-pager', '-o', 'json', '-n', '10000'])
        if len(raw) > 16 * 1024 * 1024:
            raise ValueError('Terminal trial journal bound exceeded')
        starts, terminals, invalidating = [], [], []
        for line in raw.splitlines():
            entry = json.loads(line)
            message = entry.get('MESSAGE', '')
            if 'Station online control utc=' not in message:
                continue
            fields = dict(re.findall(r'(?:^|\s)(action|correlation)=([^\s]+)', message))
            utc = datetime.fromtimestamp(int(entry['__REALTIME_TIMESTAMP']) / 1e6, timezone.utc).isoformat()
            event = (utc, fields.get('correlation'))
            if fields.get('action') == 'start':
                starts.append(event)
            elif fields.get('action') == 'recovery-failed':
                terminals.append(event)
            elif fields.get('action') in ('create', 'resume-relay', 'relay-ticket'):
                invalidating.append(utc)
        if not starts or starts[-1][0] != CLOSED_TRIAL or not starts[-1][1] or starts[-1][1] == '(null)':
            raise ValueError('Exact explicitly closed last trial not identified')
        terminal = next((t for t in reversed(terminals) if t[1] == starts[-1][1] and t[0] > CLOSED_TRIAL), None)
        if terminal is None or any(t > terminal[0] for t in invalidating):
            raise ValueError('Retained trial not conclusively terminal')
        value['operatorDiscardedClosedTerminalTrial'] = dict(startUtc=CLOSED_TRIAL, terminalUtc=terminal[0],
                correlationSha256=hashlib.sha256(starts[-1][1].encode()).hexdigest(),
                humanExitAcknowledged=True, connections=0, pendingBytes=0)
    return value


def idle_period(seconds=3):
    until = time.monotonic() + seconds
    while time.monotonic() < until:
        idle()
        time.sleep(.5)
    return idle()


def qualify(state, values):
    identity = pwd.getpwnam(IDENTITY)
    if QUALIFIED.is_symlink():
        raise ValueError('Qualified directory link refused')
    QUALIFIED.mkdir(mode=0o750, exist_ok=True)
    os.chown(QUALIFIED, 0, identity.pw_gid)
    QUALIFIED.chmod(0o750)
    candidate = QUALIFIED / 'online-engine-registry-r73.json'
    expected = state['oldEngines'] + state['additions']
    if not candidate.exists():
        ops.run(['/usr/bin/python3', str(ROOT / 'tests/StationRecovery/merge_engine_registry.py'),
                 '--existing', str(REGISTRY), '--additions', str(ADDITIONS), '--output', str(candidate)])
    if candidate.is_symlink() or json.loads(candidate.read_text()) != expected:
        raise ValueError('Candidate is not exact additive registry')
    os.chown(candidate, 0, identity.pw_gid)
    candidate.chmod(0o640)
    CHECK.mkdir(mode=0o700, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime('%H%M%S%f')
    logfile = CHECK / ('shadow-' + stamp + '.log')
    envfile = CHECK / ('shadow-' + stamp + '.env')
    ops.private_text(logfile, '')
    ops.private_text(envfile, ENV.read_text() + '\n' + OVERLAY.read_text() + '\nStation__Online__EngineRegistryFile=' + str(candidate) + '\n')
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        port = probe.getsockname()[1]
    base = 'http://127.0.0.1:' + str(port)
    unit = 'station-r73-registry-shadow-' + stamp + '.service'
    arguments = ['systemd-run', '--quiet', '--collect', '--unit', unit]
    properties = sandbox_properties(TARGET, IDENTITY, security.MEDIA)
    properties['BindReadOnlyPaths'] += ' ' + str(QUALIFIED)
    for name, value in properties.items():
        arguments += ['--property', name + '=' + value]
    arguments += ['--property', 'EnvironmentFile=' + str(envfile), '--property', 'WorkingDirectory=' + str(TARGET),
                  '--property', 'StandardOutput=append:' + str(logfile), '--property', 'StandardError=append:' + str(logfile),
                  '/usr/bin/dotnet', str(DLL), '--urls', base]
    print(json.dumps(dict(stage='isolated_registry_qualification',productionChanged=False)), flush=True)
    started = False
    try:
        ops.run(arguments)
        started = True
        ready = r71.ready_recovery(base)
        probe_values = dict(values, Station__Online__EngineRegistryFile=str(candidate))
        proof = load('verificar-registro-station-r73.py').verify(json.loads(security.INDEX.read_text()), probe_values,
                base, lambda sql: ops.sql(state['database'], sql), state['additions'], [e['id'] for e in state['oldEngines']])
    finally:
        if started:
            result = subprocess.run(['systemctl', 'stop', unit], capture_output=True, timeout=30)
            if result.returncode and ops.run(['systemctl', 'show', unit, '-p', 'MainPID', '--value']).strip() not in ('', '0'):
                raise ValueError('Shadow did not stop')
        envfile.unlink(missing_ok=True)
    unchanged(state, OLD_SHA)
    value = dict(passed=True, registryPath=str(candidate), registrySha256=ops.digest(candidate), proof=proof,
                 ready=ready, oldSixPreserved=True, productionChanged=False, utc=now())
    record = report('qualified', value)
    print(json.dumps(dict(qualified=True,checks=proof['checks'],record=record)), flush=True)
    return value


def rollback(backup):
    if backup.parent != Path('/mnt/DADOS') or not re.fullmatch(r'station-r73-registry-backup-20261007-[0-9]{12}', backup.name) or backup.is_symlink():
        raise ValueError('Unexpected registry backup')
    state = json.loads((backup / 'state.json').read_text())
    if state.get('target') != str(TARGET) or state.get('dllSha256') != DLL_SHA or ops.digest(backup / 'old-engine-registry.json') != OLD_SHA:
        raise ValueError('Backup identity differs')
    unchanged(state, state['newRegistrySha256'])
    idle_period()
    ops.replace_config(REGISTRY, (backup / 'old-engine-registry.json').read_text())
    ops.run(['systemctl', 'restart', SERVICE])
    r71.ready_recovery('http://127.0.0.1:5192')
    unchanged(state, OLD_SHA)
    result = dict(passed=True, oldSixPreserved=True, registrySha256=OLD_SHA, databaseRestored=False,
                  dllSha256=DLL_SHA, utc=now())
    ops.private_text(backup / 'rollback.json', json.dumps(result, indent=2) + '\n')
    return result


def activate(revision):
    git = ['git', '-c', 'safe.directory=' + str(ROOT)]
    if not re.fullmatch('[0-9a-f]{40}', revision) or ops.run(git + ['rev-parse', 'HEAD'], cwd=ROOT).strip() != revision:
        raise ValueError('Exact reviewed operator revision required')
    receipt = 'docs/station-android/entrega-app-r71-20261007/NAVIGATION-AUDIT.md'
    for line in ops.run(git + ['status', '--porcelain'], cwd=ROOT).splitlines():
        if line != ' M ' + receipt or subprocess.check_output(git + ['show', 'HEAD:' + receipt], cwd=ROOT) != (ROOT / receipt).read_bytes():
            raise ValueError('Uncommitted operator source refused')
    state, values = capture()
    qualified = qualify(state, values)
    unchanged(state, OLD_SHA)
    idle_period()
    backup = Path('/mnt/DADOS/station-r73-registry-backup-20261007-' + datetime.now(timezone.utc).strftime('%H%M%S%f'))
    backup.mkdir(mode=0o700)
    ops.private_text(backup / 'old-engine-registry.json', REGISTRY.read_text())
    state.update(target=str(TARGET), dllSha256=DLL_SHA, operatorSourceRevision=revision, newRegistrySha256=qualified['registrySha256'])
    ops.private_text(backup / 'state.json', json.dumps(state, indent=2) + '\n')
    if ops.digest(backup / 'old-engine-registry.json') != OLD_SHA:
        raise ValueError('Registry backup did not reproduce original bytes')
    changed = False
    restarted = False
    try:
        unchanged(state, OLD_SHA)
        before = idle()
        candidate = Path(qualified['registryPath'])
        ops.replace_config(REGISTRY, candidate.read_text())
        changed = True
        print(json.dumps(dict(stage='idle_station_registry_reload',roomsBefore=before)), flush=True)
        # Refuse a restart if a new participant arrived during the atomic write.
        idle()
        ops.run(['systemctl', 'restart', SERVICE])
        restarted = True
        reload_utc = now()
        ready = r71.ready_recovery('http://127.0.0.1:5192')
        running = unchanged(state, qualified['registrySha256'])
        proof = load('verificar-registro-station-r73.py').verify(json.loads(security.INDEX.read_text()), running,
                'https://app.lzgames.com.br', lambda sql: ops.sql(state['database'], sql), state['additions'],
                [e['id'] for e in state['oldEngines']])
        unchanged(state, qualified['registrySha256'])
        after = r71.idle_recovery()
        result = dict(applied=True,utc=now(),reloadUtc=reload_utc,operatorSourceRevision=revision,
                      appSourceRevision=APP_COMMIT,deployedDllSourceRevision='ab192bf1585e30f303d041f13b36a1f9c96d2caa',
                      dllPath=str(DLL),dllSha256=DLL_SHA,registryPath=str(REGISTRY),registrySha256=qualified['registrySha256'],
                      engineIds=[e['id'] for e in state['oldEngines'] + state['additions']],engines=state['oldEngines']+state['additions'],
                      pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value'])),backup=str(backup),
                      qualified=qualified,publicProof=proof,readyAfter=after,onlyEffectiveRegistryChanged=True,
                      idleBeforeReload=before,
                      originalSixPreserved=True,dllChanged=False,environmentChanged=False,sandboxChanged=False,
                      databaseSchemaChanged=False,keysChanged=False,realLicensesChanged=False,otherProductsChanged=False,
                      nginxChanged=False,cloudflareChanged=False,requireVerifiedApp=False,androidGameplayVerified=False,
                      indexRevision=14,visibleItems=2212,indexSha256=security.INDEX_SHA)
        record = report('active', result)
        print(json.dumps(dict(applied=True,pid=result['pid'],engineCount=8,registrySha256=result['registrySha256'],
                              publicChecks=proof['checks'],record=record)), flush=True)
    except Exception as error:
        result = dict(applied=False,errorType=type(error).__name__,registryReplaced=changed,backup=str(backup))
        if changed:
            try:
                if not restarted and ops.digest(REGISTRY) == qualified['registrySha256'] and online.command_path() == DLL:
                    # A room may have arrived between the write and reload.
                    # The running singleton still uses the old registry; restore
                    # disk bytes without stopping that new session.
                    ops.replace_config(REGISTRY, (backup / 'old-engine-registry.json').read_text())
                    unchanged(state, OLD_SHA)
                    result['rollback'] = dict(passed=True,restarted=False,oldRegistryRestored=True)
                else:
                    result['rollback'] = rollback(backup)
            except Exception as failure:
                result['rollback'] = dict(passed=False,errorType=type(failure).__name__,liveSessionNeverInterrupted=True)
        record = report('failure', result)
        diagnostic = CHECK / ('diagnostic-' + datetime.now(timezone.utc).strftime('%H%M%S%f') + '.txt')
        ops.private_text(diagnostic, traceback.format_exc())
        print(json.dumps(dict(passed=False,errorType=type(error).__name__,record=record,rollback=result.get('rollback'))),flush=True)
        raise


def main():
    global CLOSED_TRIAL
    parser=argparse.ArgumentParser()
    parser.add_argument('--preflight',action='store_true')
    parser.add_argument('--apply')
    parser.add_argument('--rollback',type=Path)
    parser.add_argument('--closed-trial-start-utc',help='Exact terminal trial already explicitly ended by the maintainer')
    args=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':
        raise SystemExit('Native Linux operator authentication required')
    CLOSED_TRIAL=args.closed_trial_start_utc
    try:
        if args.preflight:
            state,_=capture()
            print(json.dumps(dict(preflightPassed=True,dllSha256=DLL_SHA,oldEngineIds=[e['id'] for e in state['oldEngines']],
                                  newEngineIds=NEW_IDS,ready=state['ready'],record=report('preflight',state))),flush=True)
        elif args.apply:
            activate(args.apply)
        elif args.rollback:
            print(json.dumps(rollback(args.rollback)),flush=True)
        else:
            raise ValueError('Registry action required')
    except Exception as error:
        print(json.dumps(dict(passed=False,errorType=type(error).__name__)),flush=True)
        raise SystemExit(1)


if __name__=='__main__':
    main()
