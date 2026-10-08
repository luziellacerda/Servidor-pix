#!/usr/bin/env python3
"""Publish the qualified R81 Station base while real capacity approval is pending.

This phase publishes exact catalog content identities and v2 observations, keeps
the ten existing engines, and explicitly leaves v3 and its global legacy gate
disabled. It cannot approve profiles. Uses native Linux polkit authentication,
isolated old/new/old checks, owned disposable fixtures and an idle-only restart.
Private evidence and sealed inputs stay outside Git. No database/schema restore,
shared-product deployment, proxy/tunnel/firewall change or ROM scan is performed.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import shutil
import socket
import subprocess
import sys
import time
import traceback
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = Path(__file__).parent
sys.path.insert(0, str(SCRIPTS))
from station_hardening_config import sandbox_properties, systemd_override
from station_async_socket_check import SocketChecks


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), SCRIPTS/name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


r71 = load('implantar-recovery-station-r71-20261007.py')
security, ops, online = r71.security, r71.ops, r71.online
legacy = load('ativar-registro-station-r74-20261007.py')
observation = load('implantar-observacoes-station-r74-20261007.py')
catalog_check = load('verificar-catalogo-identidades-r81.py')
SERVICE = 'turborama-station-api.service'
IDENTITY = 'turborama-station-api'
OLD = Path('/opt/turborama-station-recovery-r71-20261007-ab192bf')
OLD_DLL = '815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243'
NEW_DLL = '9878ae9caea55bb6834745caa3a60140616d3e0a5f2ea71055fe9e9df813fe51'
SOURCE = 'b472d8a653e065cccb00dfb15dbea3d56c03db98'
TARGET = Path('/opt/turborama-station-base-r81-20261008-b472d8a')
REGISTRY_SHA = 'a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843'
INDEX_SHA = 'b3d44224a0264f3a14104f031d417492017b1d7756d7256607a51833c243348c'
ENV = Path('/etc/turborama-station-api-security-20261007/station.env')
RECOVERY_ENV = ENV.with_name('recovery-r71-ab192bf.env')
DROPIN = Path('/etc/systemd/system')/(SERVICE+'.d')/'zzzzzzzzzzzzzzzzzzzzzzzz-station-base-r81-20261008.conf'
BASE = 'http://127.0.0.1:5192'
PUBLIC = 'https://app.lzgames.com.br'


def utc():
    return datetime.now(timezone.utc).isoformat()


def write_private(path, value):
    text = value if isinstance(value, str) else json.dumps(value, indent=2)+'\n'
    ops.private_text(path, text)
    owner = pwd.getpwnam('lz-servidor')
    os.chown(path, owner.pw_uid, owner.pw_gid)


def baseline():
    values, ids = ops.runtime()
    if online.command_path() != OLD/'TurboRamaSuiteOnlineServer.dll' or ops.digest(online.command_path()) != OLD_DLL:
        raise ValueError('Reviewed production DLL changed')
    if ops.digest(security.INDEX) != INDEX_SHA or json.loads(security.INDEX.read_text())['revision'] != 20:
        raise ValueError('Reviewed catalog revision20 changed')
    if values.get('Station__Online__EngineRegistryFile') != str(OLD/'online-engine-registry.json') or ops.digest(OLD/'online-engine-registry.json') != REGISTRY_SHA:
        raise ValueError('Reviewed ten-engine registry changed')
    identity = pwd.getpwnam(IDENTITY)
    if ids['Uid'][1] != identity.pw_uid or ids['Gid'][1] != identity.pw_gid:
        raise ValueError('Dedicated Station identity changed')
    if values.get('Station__Online__MultiplayerEnabled', 'false').lower() != 'false' or values.get('Station__Online__MultiplayerLegacyCapacityGate', 'false').lower() != 'false':
        raise ValueError('Multiplayer policy was superseded')
    legacy.environment_fingerprint(values)
    db = r71.database(values)
    state = dict(utc=utc(), pid=int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value'])),
        database=db, originalReleaseFiles=security.files(OLD), originalConfiguration=security.snapshot_configs(),
        protectedFiles={str(p): ops.digest(p) for p in ENV.parent.iterdir() if p.is_file()},
        shared={u: ops.state(u) for u in security.SHARED}, realLicenseSha256=security.real_licenses(db),
        schemaLedgerSha256=hashlib.sha256(security.scalar(db, 'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),
        explicitSettings={k: v for k, v in values.items() if k.startswith(('Station__', 'Suite__', 'ConnectionStrings__'))})
    security.database_scope(db)
    security.management_health()
    return state, values


def unchanged(state):
    if security.files(OLD) != state['originalReleaseFiles'] or ops.digest(security.INDEX) != INDEX_SHA:
        raise ValueError('Original release or catalog changed')
    for name, digest in {**state['originalConfiguration'], **state['protectedFiles']}.items():
        if ops.digest(Path(name)) != digest:
            raise ValueError('Original configuration or key changed')
    for unit, status in state['shared'].items():
        if ops.state(unit) != status:
            raise ValueError('Shared product process changed')
    db = state['database']
    if security.real_licenses(db) != state['realLicenseSha256'] or hashlib.sha256(security.scalar(db, 'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest() != state['schemaLedgerSha256']:
        raise ValueError('Real license or schema changed')
    security.database_scope(db)
    security.management_health()


def overlay():
    return ('Station__Online__EngineRegistryFile='+str(TARGET/'online-engine-registry.json')+'\n'
        'Station__Online__RecoveryDiagnosticsEnabled=true\n'
        'Station__Online__MultiplayerEnabled=false\n'
        'Station__Online__MultiplayerLegacyCapacityGate=false\n'
        'Station__Online__MultiplayerProfileRegistryFile='+str(TARGET/'multiplayer-profiles-unapproved.json')+'\n')


def install(folder, seal, profiles):
    meta = json.loads(seal.read_text())
    if meta['sourceCommit'] != SOURCE or meta['dllSha256'] != NEW_DLL or meta['indexSha256'] != INDEX_SHA or meta['catalogRevision'] != 20:
        raise ValueError('Sealed source/catalog differs')
    if security.files(folder) != meta['files'] or len(meta['files']) != 12 or ops.digest(folder/'TurboRamaSuiteOnlineServer.dll') != NEW_DLL or ops.digest(profiles) != meta['draftRegistrySha256']:
        raise ValueError('Sealed release/profile bytes differ')
    entries = json.loads(profiles.read_text())
    if len(entries) != 2074 or any(p['approved'] is not False for p in entries):
        raise ValueError('Only reviewed unapproved drafts allowed')
    source_manifest = json.loads((ROOT/'docs/station-android/recovery-r79-20261008/QUALIFICACAO-CANDIDATA.json').read_text())
    if any(ops.digest(ROOT/e['path']) != e['sha256'] for e in source_manifest['sourceFiles']):
        raise ValueError('Reviewed candidate source differs')
    expected = dict(meta['files'], **{'online-engine-registry.json': REGISTRY_SHA,
        'multiplayer-profiles-unapproved.json': meta['draftRegistrySha256'], 'base.env': hashlib.sha256(overlay().encode()).hexdigest()})
    if TARGET.is_symlink():
        raise ValueError('Release directory link refused')
    if not TARGET.exists():
        shutil.copytree(folder, TARGET)
        shutil.copyfile(OLD/'online-engine-registry.json', TARGET/'online-engine-registry.json')
        shutil.copyfile(profiles, TARGET/'multiplayer-profiles-unapproved.json')
        ops.private_text(TARGET/'base.env', overlay())
        identity = pwd.getpwnam(IDENTITY)
        for path in [TARGET, *TARGET.rglob('*')]:
            os.chown(path, 0, identity.pw_gid)
            path.chmod(0o750 if path.is_dir() else 0o640)
    if security.files(TARGET) != expected:
        raise ValueError('Installed immutable release differs')
    return meta


def checks(state, values, base, identities=True, observations=True, multiplayer='disabled'):
    index = json.loads(security.INDEX.read_text())
    result = dict(catalog=catalog_check.verify(index, values, base, lambda s: ops.sql(state['database'], s), identities, multiplayer))
    if multiplayer != 'unapproved':
        verifier = load('verificar-registro-station-r74.py')
        adapter = SocketChecks()
        verifier.connect = adapter.connect
        engines = json.loads(Path(values['Station__Online__EngineRegistryFile']).read_text())
        additions = json.loads(legacy.ADDITIONS.read_text())
        ids = [e['id'] for e in engines if e['id'] not in legacy.NEW_IDS]
        try:
            result['online'] = verifier.verify(index, values, base, lambda s: ops.sql(state['database'], s), additions, ids,
                observe=(lambda: observation.diagnostics(base if not base.startswith('https:') else BASE)) if observations else None)
        finally:
            adapter.close()
    return result


def shadow(state, values, work, label, new=True, gate=False):
    target = TARGET if new else OLD
    stamp = datetime.now(timezone.utc).strftime('%H%M%S%f')
    log = work/(label+'-'+stamp+'.log')
    env = log.with_suffix('.env')
    text = ENV.read_text()+'\n'+RECOVERY_ENV.read_text()+'\n'+(overlay() if new else '')
    if gate:
        text = text.replace('Station__Online__MultiplayerEnabled=false', 'Station__Online__MultiplayerEnabled=true').replace('Station__Online__MultiplayerLegacyCapacityGate=false', 'Station__Online__MultiplayerLegacyCapacityGate=true')
    ops.private_text(log, '')
    ops.private_text(env, text)
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        port = probe.getsockname()[1]
    base = 'http://127.0.0.1:'+str(port)
    unit = 'station-r81-'+label+'-'+stamp+'.service'
    arguments = ['systemd-run', '--quiet', '--collect', '--unit', unit]
    for name, value in sandbox_properties(target, IDENTITY, security.MEDIA).items():
        arguments += ['--property', name+'='+value]
    arguments += ['--property', 'EnvironmentFile='+str(env), '--property', 'WorkingDirectory='+str(target),
        '--property', 'StandardOutput=append:'+str(log), '--property', 'StandardError=append:'+str(log),
        '/usr/bin/dotnet', str(target/'TurboRamaSuiteOnlineServer.dll'), '--urls', base]
    started = False
    print(json.dumps(dict(stage=label, productionChanged=False)), flush=True)
    try:
        ops.run(arguments)
        started = True
        ready = r71.ready_recovery(base)
        pid = int(ops.run(['systemctl', 'show', unit, '-p', 'MainPID', '--value']))
        sandbox = r71.sandbox(pid, target, json.loads(security.INDEX.read_text())) if new else None
        configured = dict(values)
        if new:
            configured['Station__Online__EngineRegistryFile'] = str(TARGET/'online-engine-registry.json')
        proof = checks(state, configured, base, new, new, 'unapproved' if gate else 'disabled' if new else 'absent')
        return dict(passed=True, dllSha256=NEW_DLL if new else OLD_DLL, ready=ready, sandbox=sandbox, proof=proof, globalGateEnabledInShadow=gate)
    finally:
        if started:
            subprocess.run(['systemctl', 'stop', unit], check=True, capture_output=True, timeout=30)
        env.unlink(missing_ok=True)
        unchanged(state)


def qualify(state, values, work):
    results = {}
    for label, new, gate in [('old', False, False), ('new', True, False), ('unapproved-gate', True, True), ('rollback-old', False, False)]:
        results[label] = shadow(state, values, work, label, new, gate)
    if int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value'])) != state['pid']:
        raise ValueError('Production changed during qualification')
    write_private(work/'qualified-base.json', dict(passed=True, utc=utc(), sourceCommit=SOURCE, dllSha256=NEW_DLL,
        baselinePid=state['pid'], indexSha256=INDEX_SHA, profilesApproved=0, qualification=results, productionChanged=False))
    return results


def wait_idle(seconds=25):
    deadline = time.monotonic()+seconds
    while True:
        try:
            return legacy.idle_period()
        except ValueError:
            if time.monotonic() >= deadline:
                raise
            time.sleep(.5)


def apply(state, values, work, qualified):
    if DROPIN.exists() or DROPIN.name <= max(p.name for p in DROPIN.parent.glob('*.conf')):
        raise ValueError('Own final service override is unavailable')
    unchanged(state)
    before = wait_idle()
    if int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value'])) != state['pid']:
        raise ValueError('Production PID was superseded')
    backup = Path('/mnt/DADOS/station-r81-base-backup-20261008-'+datetime.now(timezone.utc).strftime('%H%M%S%f'))
    backup.mkdir(mode=0o700)
    for path in DROPIN.parent.glob('*.conf'):
        shutil.copy2(path, backup/path.name)
    write_private(backup/'state.json', state)
    content = systemd_override(TARGET, IDENTITY, security.MEDIA, ENV)+'EnvironmentFile='+str(RECOVERY_ENV)+'\nEnvironmentFile='+str(TARGET/'base.env')+'\n'
    override_sha = hashlib.sha256(content.encode()).hexdigest()
    changed = False
    try:
        ops.private_text(DROPIN, content)
        DROPIN.chmod(0o644)
        changed = True
        ops.run(['systemctl', 'daemon-reload'])
        legacy.idle()
        ops.run(['systemctl', 'restart', SERVICE])
        reload_utc = utc()
        ready = r71.ready_recovery(BASE)
        current, ids = ops.runtime()
        expected = dict(state['explicitSettings'])
        expected.update(dict(line.split('=', 1) for line in overlay().splitlines()))
        if {k: v for k, v in current.items() if k.startswith(('Station__', 'Suite__', 'ConnectionStrings__'))} != expected or online.command_path() != TARGET/'TurboRamaSuiteOnlineServer.dll':
            raise ValueError('Effective release/settings differ')
        identity = pwd.getpwnam(IDENTITY)
        if ids['Uid'][1] != identity.pw_uid or ids['Gid'][1] != identity.pw_gid:
            raise ValueError('Effective identity changed')
        proof = checks(state, current, PUBLIC)
        after = wait_idle()
        unchanged(state)
        pid = int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value']))
        sandbox = r71.sandbox(pid, TARGET, json.loads(security.INDEX.read_text()))
        result = dict(applied=True, utc=utc(), reloadUtc=reload_utc, pid=pid, sourceCommit=SOURCE, dllSha256=NEW_DLL,
            catalogRevision=20, indexSha256=INDEX_SHA, engineRegistrySha256=REGISTRY_SHA, legacyEngineCount=10,
            approvedRealGameProfiles=0, multiplayerEnabled=False, multiplayerLegacyCapacityGate=False,
            publicProof=proof, sandbox=sandbox, ready=ready, idleAfter=after, idleBefore=before, qualification=qualified,
            originalReleasePreserved=True, originalKeysPreserved=True, realLicensesPreserved=True,
            databaseSchemaChanged=False, otherProductsChanged=False, firewallChanged=False, cloudflareChanged=False,
            rollbackVerifiedWithOldShadow=True, backupDirectory=str(backup), dropinSha256=override_sha,
            physicalAndroidGameplayQualified=False, v3ActivationComplete=False)
        write_private(work/'active-base-private.json', result)
        print(json.dumps(dict(applied=True, pid=pid, catalogRevision=20, legacyEngines=10, approvedProfiles=0,
            v3Enabled=False, dllSha256=NEW_DLL)), flush=True)
    except Exception as error:
        result = dict(applied=False, utc=utc(), errorType=type(error).__name__, overrideInstalled=changed)
        if changed:
            try:
                if ops.digest(DROPIN) != override_sha:
                    raise ValueError('Own override was superseded')
                if int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value'])) > 0:
                    wait_idle()
                unchanged(state)
                DROPIN.unlink()
                ops.run(['systemctl', 'daemon-reload'])
                ops.run(['systemctl', 'restart', SERVICE])
                r71.ready_recovery(BASE)
                if online.command_path() != OLD/'TurboRamaSuiteOnlineServer.dll':
                    raise ValueError('Original release was not restored')
                unchanged(state)
                result['rollback'] = dict(passed=True, oldDllSha256=OLD_DLL, databaseRestored=False)
            except Exception as rollback_error:
                result['rollback'] = dict(passed=False, errorType=type(rollback_error).__name__, liveSessionsNeverForciblyEnded=True)
        write_private(work/'failure-base-private.json', result)
        print(json.dumps(result), flush=True)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release-directory', type=Path, required=True)
    parser.add_argument('--seal', type=Path, required=True)
    parser.add_argument('--unapproved-profiles', type=Path, required=True)
    parser.add_argument('--work-directory', type=Path, required=True)
    parser.add_argument('--apply-base', action='store_true')
    args = parser.parse_args()
    if os.geteuid() != 0 or os.environ.get('PKEXEC_UID') != '1000':
        raise ValueError('Native Linux operator authentication required')
    for path in (args.release_directory, args.seal, args.unapproved_profiles, args.work_directory):
        if not path.is_absolute() or path.is_symlink():
            raise ValueError('Absolute regular private inputs required')
    if args.work_directory.exists():
        raise ValueError('Fresh private qualification directory required')
    args.work_directory.mkdir(mode=0o700)
    owner = pwd.getpwnam('lz-servidor')
    os.chown(args.work_directory, owner.pw_uid, owner.pw_gid)
    try:
        state, values = baseline()
        write_private(args.work_directory/'baseline-private.json', state)
        install(args.release_directory, args.seal, args.unapproved_profiles)
        qualified = qualify(state, values, args.work_directory)
        if args.apply_base:
            apply(state, values, args.work_directory, qualified)
        else:
            print(json.dumps(dict(qualified=True, productionChanged=False, approvedProfiles=0)), flush=True)
    except Exception as error:
        write_private(args.work_directory/'diagnostic-private.txt', traceback.format_exc())
        print(json.dumps(dict(passed=False, errorType=type(error).__name__, detailsSuppressed=True)), flush=True)
        raise SystemExit(1)


if __name__ == '__main__':
    main()
