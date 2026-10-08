#!/usr/bin/env python3
"""Activate every current compatible R81 game by explicit operator instruction.

Uses the already published base safeguards, native polkit, an isolated shadow,
idle-only publication and compare-and-swap rollback. Never approves physical
gameplay or changes the catalog, licenses, database schema or shared products.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import shlex
import shutil
import socket
import subprocess
import time
import traceback


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


b = load('implantar-base-station-r81-20261008.py')
v3 = load('verificar-online-station-r81.py')
b.OLD = b.TARGET
b.OLD_DLL = b.NEW_DLL
b.SOURCE = 'db50a980fcfe01a9b0fee65316f67f43c63ac11c'
b.TARGET = Path('/opt/turborama-station-online-r81-20261008-db50a98')
b.DROPIN = b.DROPIN.with_name('z'*25+'-station-online-r81-20261008.conf')
PROFILE_SHA = 'f3eb13fcb474edb5a1b549a3509aa47505765210b38d1f1dd81bc157b4ec555c'
PROXY = Path('/etc/nginx/snippets/turborama-station-online.locations.conf')
RELAY_PROXY = PROXY.with_name('turborama-station-relay.locations.conf')
original_checks = b.checks


def overlay():
    return ('Station__Online__EngineRegistryFile='+str(b.TARGET/'online-engine-registry.json')+'\n'
        'Station__Online__RecoveryDiagnosticsEnabled=true\n'
        'Station__Online__MultiplayerEnabled=true\n'
        'Station__Online__MultiplayerLegacyCapacityGate=false\n'
        'Station__Online__MultiplayerProfileRegistryFile='+str(b.TARGET/'online-profiles-authorized.json')+'\n')


def fingerprint(values):
    expected = {}
    for path in (b.ENV, b.RECOVERY_ENV, b.OLD/'base.env'):
        for line in path.read_text().splitlines():
            if not line.strip() or line.lstrip().startswith('#'):
                continue
            name, separator, raw = line.partition('=')
            if not separator:
                raise ValueError('Unexpected environment format')
            parts = shlex.split(raw, posix=True)
            if len(parts) > 1:
                raise ValueError('Unexpected environment value')
            expected[name] = parts[0] if parts else ''
    scoped = lambda name: name.startswith(('Station__', 'Suite__', 'ConnectionStrings__'))
    if any(values.get(k) != v for k, v in expected.items()) or {k for k in expected if scoped(k)} != {k for k in values if scoped(k)}:
        raise ValueError('Reviewed effective settings changed')
    return hashlib.sha256(json.dumps(expected, sort_keys=True).encode()).hexdigest()


b.overlay = overlay
b.legacy.environment_fingerprint = fingerprint


def install(folder, seal, profiles):
    meta = json.loads(seal.read_text())
    if meta['sourceCommit'] != b.SOURCE or meta['indexSha256'] != b.INDEX_SHA or meta['profileRegistrySha256'] != PROFILE_SHA:
        raise ValueError('Sealed source/catalog/profile differs')
    if b.security.files(folder) != meta['files'] or b.ops.digest(folder/'TurboRamaSuiteOnlineServer.dll') != meta['dllSha256'] or b.ops.digest(profiles) != PROFILE_SHA:
        raise ValueError('Sealed binary/profile bytes differ')
    for entry in meta['sourceFiles']:
        if b.ops.digest(b.ROOT/entry['path']) != entry['sha256']:
            raise ValueError('Sealed source file changed')
    b.NEW_DLL = meta['dllSha256']
    entries = json.loads(profiles.read_text())
    index = json.loads(b.security.INDEX.read_text())
    expected_items = {r['itemId']: r for r in index['items'] if r.get('catalogVisible', True) and r['platform'] in ('snes', 'snesbr', 'megadrive', 'megadrivebr')}
    if len(entries) != 1816 or {p['itemId'] for p in entries} != expected_items.keys() or len({p['itemId'] for p in entries}) != len(entries):
        raise ValueError('Every current compatible game required exactly once')
    for p in entries:
        row = expected_items[p['itemId']]
        if p['approved'] is not True or p['contentSha256'] != row.get('contentSha256'):
            raise ValueError('Authorized profile must bind current catalog content')
    expected = dict(meta['files'], **{'online-engine-registry.json': b.REGISTRY_SHA,
        'online-profiles-authorized.json': PROFILE_SHA, 'base.env': hashlib.sha256(overlay().encode()).hexdigest()})
    if b.TARGET.is_symlink():
        raise ValueError('Release link refused')
    if not b.TARGET.exists():
        shutil.copytree(folder, b.TARGET)
        shutil.copyfile(b.OLD/'online-engine-registry.json', b.TARGET/'online-engine-registry.json')
        shutil.copyfile(profiles, b.TARGET/'online-profiles-authorized.json')
        b.ops.private_text(b.TARGET/'base.env', overlay())
        identity = pwd.getpwnam(b.IDENTITY)
        for path in [b.TARGET, *b.TARGET.rglob('*')]:
            os.chown(path, 0, identity.pw_gid)
            path.chmod(0o750 if path.is_dir() else 0o640)
    if b.security.files(b.TARGET) != expected:
        raise ValueError('Installed immutable release differs')


def checks(state, values, base):
    enabled = values.get('Station__Online__MultiplayerEnabled', 'false').lower() == 'true'
    result = original_checks(state, values, base, multiplayer='enabled' if enabled else 'disabled')
    if enabled:
        result['multiplayer'] = v3.verify(json.loads(b.security.INDEX.read_text()), values, base, lambda s: b.ops.sql(state['database'], s))
    return result


def shadow(state, values, work):
    logfile = work/'shadow-private.log'
    envfile = work/'shadow-private.env'
    b.ops.private_text(logfile, '')
    b.ops.private_text(envfile, b.ENV.read_text()+'\n'+b.RECOVERY_ENV.read_text()+'\n'+overlay())
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        port = probe.getsockname()[1]
    base = 'http://127.0.0.1:'+str(port)
    unit = 'station-r81-online-check-'+str(port)+'.service'
    args = ['systemd-run', '--quiet', '--collect', '--unit', unit]
    for name, value in b.sandbox_properties(b.TARGET, b.IDENTITY, b.security.MEDIA).items():
        args += ['--property', name+'='+value]
    args += ['--property', 'EnvironmentFile='+str(envfile), '--property', 'WorkingDirectory='+str(b.TARGET),
        '--property', 'StandardOutput=append:'+str(logfile), '--property', 'StandardError=append:'+str(logfile),
        '/usr/bin/dotnet', str(b.TARGET/'TurboRamaSuiteOnlineServer.dll'), '--urls', base]
    started = False
    try:
        print(json.dumps(dict(stage='checking-enabled-shadow', productionChanged=False)), flush=True)
        b.ops.run(args); started = True
        ready = b.r71.ready_recovery(base)
        configured = dict(values, **dict(line.split('=', 1) for line in overlay().splitlines()))
        proof = checks(state, configured, base)
        return dict(passed=True, ready=ready, proof=proof, physicalGameplayQualified=False)
    finally:
        if started:
            b.ops.run(['systemctl', 'stop', unit])
        envfile.unlink(missing_ok=True)
        b.unchanged(state)


def idle():
    state = b.legacy.idle()
    multi = state.get('multiplayer', {})
    if multi.get('activeRooms', 0) or multi.get('activeConnections', 0) or multi.get('retainedBytes', 0):
        raise ValueError('A v3 room is in progress; no restart permitted')
    return state


def proxy_routes(original):
    command = '/v1/station/online/command'
    start = original.index('location = '+command+' {')
    end = original.index('\n}\n', start)+3
    relay = RELAY_PROXY.read_text()
    relay = relay[relay.index('location = /v1/station/online/relay {'):]
    added = (original[start:end].replace(command, '/v1/station/online/multiplayer/command')+'\n'+
        relay.replace('/v1/station/online/relay', '/v1/station/online/multiplayer/relay'))
    if '/v1/station/online/multiplayer/' in original or added.count('include /etc/nginx/snippets/turborama-station-origin-guard.conf;') != 2 or added.count('X-Station-Request-Proof') != 2:
        raise ValueError('Exact additive protected v3 routes required')
    return original+'\n# R81 authenticated commands and protected WSS.\n'+added


def apply(state, values, work, qualified):
    if b.DROPIN.exists() or b.DROPIN.name <= max(p.name for p in b.DROPIN.parent.glob('*.conf')):
        raise ValueError('Own service override superseded')
    b.unchanged(state)
    before = b.wait_idle(); idle()
    if int(b.ops.run(['systemctl', 'show', b.SERVICE, '-p', 'MainPID', '--value'])) != state['pid']:
        raise ValueError('Production changed during qualification')
    backup = work/'rollback-private'
    backup.mkdir(mode=0o700)
    for path in b.DROPIN.parent.glob('*.conf'):
        shutil.copy2(path, backup/path.name)
    content = b.systemd_override(b.TARGET, b.IDENTITY, b.security.MEDIA, b.ENV)+'EnvironmentFile='+str(b.RECOVERY_ENV)+'\nEnvironmentFile='+str(b.TARGET/'base.env')+'\n'
    digest = hashlib.sha256(content.encode()).hexdigest()
    proxy_original = PROXY.read_text()
    proxy_updated = proxy_routes(proxy_original)
    proxy_old_sha = b.ops.digest(PROXY)
    proxy_new_sha = hashlib.sha256(proxy_updated.encode()).hexdigest()
    b.write_private(backup/'station-online-proxy.conf', proxy_original)
    proxy_changed = False
    changed = False
    try:
        b.ops.private_text(b.DROPIN, content); b.DROPIN.chmod(0o644); changed = True
        b.ops.run(['systemctl', 'daemon-reload']); idle()
        b.ops.run(['systemctl', 'restart', b.SERVICE])
        reload_utc = b.utc()
        ready = b.r71.ready_recovery(b.BASE)
        current, identities = b.ops.runtime()
        expected = dict(state['explicitSettings'], **dict(line.split('=', 1) for line in overlay().splitlines()))
        if {k: v for k, v in current.items() if k.startswith(('Station__', 'Suite__', 'ConnectionStrings__'))} != expected or b.online.command_path() != b.TARGET/'TurboRamaSuiteOnlineServer.dll':
            raise ValueError('Effective release/settings differ')
        identity = pwd.getpwnam(b.IDENTITY)
        if identities['Uid'][1] != identity.pw_uid or identities['Gid'][1] != identity.pw_gid:
            raise ValueError('Dedicated Station identity changed')
        if ready.get('multiplayer', {}).get('approvedProfiles') != 1816:
            raise ValueError('All compatible profiles must be active')
        if b.ops.digest(PROXY) != proxy_old_sha:
            raise ValueError('Station proxy was superseded')
        b.ops.replace_config(PROXY, proxy_updated); proxy_changed = True
        b.ops.run(['/usr/sbin/nginx', '-t'])
        b.ops.run(['systemctl', 'reload', 'nginx.service'])
        state['originalConfiguration'][str(PROXY)] = proxy_new_sha
        proof = checks(state, current, b.PUBLIC)
        b.unchanged(state)
        pid = int(b.ops.run(['systemctl', 'show', b.SERVICE, '-p', 'MainPID', '--value']))
        result = dict(applied=True, utc=b.utc(), reloadUtc=reload_utc, pid=pid, sourceCommit=b.SOURCE, dllSha256=b.NEW_DLL,
            multiplayerEnabled=True, multiplayerLegacyCapacityGate=False, approvedProfiles=1816, snes=835, megadrive=981,
            profileRegistrySha256=PROFILE_SHA, catalogRevision=20, indexSha256=b.INDEX_SHA, engineRegistrySha256=b.REGISTRY_SHA,
            publicProof=proof, shadow=qualified, ready=b.security.telemetry(), idleBefore=before,
            sandbox=b.r71.sandbox(pid, b.TARGET, json.loads(b.security.INDEX.read_text())),
            originalReleasePreserved=True, originalKeysPreserved=True, realLicensesPreserved=True,
            databaseSchemaChanged=False, otherProductsChanged=False, cloudflareChanged=False,
            stationV3ProxyRoutesPublished=True, proxyChangesLimitedToTwoStationRoutes=True,
            physicalAndroidGameplayQualified=False, approvalMeaning='User-authorized online access for all compatible games; gameplay qualification not asserted')
        b.write_private(work/'online-active.json', result)
        print(json.dumps(dict(applied=True, pid=pid, approvedProfiles=1816, v3Enabled=True, legacyPreserved=True)), flush=True)
    except Exception:
        if proxy_changed:
            if b.ops.digest(PROXY) != proxy_new_sha:
                raise ValueError('Station proxy was superseded during rollback')
            b.ops.replace_config(PROXY, proxy_original)
            b.ops.run(['/usr/sbin/nginx', '-t']); b.ops.run(['systemctl', 'reload', 'nginx.service'])
            state['originalConfiguration'][str(PROXY)] = proxy_old_sha
        if changed:
            if b.ops.digest(b.DROPIN) != digest:
                raise ValueError('Own override was superseded')
            idle(); b.unchanged(state)
            b.DROPIN.unlink(); b.ops.run(['systemctl', 'daemon-reload']); b.ops.run(['systemctl', 'restart', b.SERVICE])
            b.r71.ready_recovery(b.BASE)
            if b.online.command_path() != b.OLD/'TurboRamaSuiteOnlineServer.dll':
                raise ValueError('Rollback release differs')
            b.unchanged(state)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release-directory', type=Path, required=True)
    parser.add_argument('--seal', type=Path, required=True)
    parser.add_argument('--profiles', type=Path, required=True)
    parser.add_argument('--work-directory', type=Path, required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or os.environ.get('PKEXEC_UID') != '1000':
        raise ValueError('Native Linux authentication required')
    for path in (args.release_directory, args.seal, args.profiles, args.work_directory):
        if not path.is_absolute() or path.is_symlink():
            raise ValueError('Absolute regular private paths required')
    if args.work_directory.exists():
        raise ValueError('Fresh private qualification directory required')
    args.work_directory.mkdir(mode=0o700)
    owner = pwd.getpwnam('lz-servidor')
    os.chown(args.work_directory, owner.pw_uid, owner.pw_gid)
    try:
        state, values = b.baseline()
        b.write_private(args.work_directory/'baseline-private.json', state)
        install(args.release_directory, args.seal, args.profiles)
        qualified = shadow(state, values, args.work_directory)
        apply(state, values, args.work_directory, qualified)
    except Exception as error:
        b.write_private(args.work_directory/'failure-private.txt', traceback.format_exc())
        print(json.dumps(dict(passed=False, errorType=type(error).__name__, detailsSuppressed=True)), flush=True)
        raise SystemExit(1)


if __name__ == '__main__':
    main()
