#!/usr/bin/env python3
"""Bounded Station relay rollout from the inspected 931030b release.

No migration, key, scanner, index or customer-license changes. Only the Station
API is restarted; nginx is reloaded for one exact WSS route. Rollback removes
only these overrides and retains the POCO test license and the additive backup.
Run with native root and the operator venv containing websockets==15.0.1.
"""
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
import tempfile
import time
from urllib.error import HTTPError
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[3]
SERVICE = 'turborama-station-api.service'
OLD = Path('/opt/turborama-station-folders-20261004-931030b')
OLD_SHA = '0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0'
INDEX = Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
INDEX_SHA = '07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92'
REGISTRY = Path('/opt/turborama-station-online-20261004-77d1dfb/online-engine-registry.json')
REGISTRY_SHA = '901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a'
ONLINE_PROXY = Path('/etc/nginx/snippets/turborama-station-online.locations.conf')
ONLINE_SHA = '3ff72526838e0c769d731c921045411853ec621f478c1c7b959110976b6f55d2'
RELAY_PROXY = Path('/etc/nginx/snippets/turborama-station-relay.locations.conf')
DROPIN = Path('/etc/systemd/system/turborama-station-api.service.d/zzzzzzzzzzzz-station-relay-20261005.conf')
RESULT = Path('/home/lz-servidor/station-relay-rollout-result-20261005.json')
DLL = 'TurboRamaSuiteOnlineServer.dll'


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ops = load('implantar-station-20261003.py')
online = load('implantar-online-station-20261004.py')
online.ops = ops


def files(folder):
    result = {}
    for path in folder.rglob('*'):
        if path.is_symlink():
            raise ValueError('Release contains a link')
        if path.is_file():
            result[str(path.relative_to(folder))] = ops.digest(path)
    return result


def current():
    return online.command_path()


def private_report(path, report):
    ops.private_text(path, json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    owner = pwd.getpwnam('lz-servidor')
    os.chown(path, owner.pw_uid, owner.pw_gid)


def backup_path(revision):
    return Path('/mnt/DADOS/station-relay-backup-20261005-' + revision[:7])


def restore_check(path):
    if path.parent != Path('/mnt/DADOS') or not path.name.startswith('station-relay-backup-20261005-'):
        raise ValueError('Unexpected private backup')
    if not os.environ.get('PG_CLUSTER_CONF_ROOT', '').startswith('/tmp/pg_virtualenv.') or not ops.run(
        ['psql', '-X', '-Atqc', 'SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('Temporary cluster required')
    ops.run(['createdb', 'station_relay_restore'])
    ops.run(['pg_restore', '--exit-on-error', '--no-owner', '--no-acl', '--dbname', 'station_relay_restore',
             str(path / 'database.dump')], timeout=300)
    ledger = ops.run(['psql', '-X', '--dbname', 'station_relay_restore', '-Atqc',
        "SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()
    if ledger != '3':
        raise ValueError('Restored ledger differs')
    print('Station relay database backup restored', flush=True)


def assert_unchanged(state):
    for name, sha in state['unchangedConfigurations'].items():
        if ops.digest(Path(name)) != sha:
            raise ValueError('Other Station configuration changed')
    if ops.digest(INDEX) != INDEX_SHA or ops.digest(REGISTRY) != REGISTRY_SHA or files(OLD) != state['oldFiles']:
        raise ValueError('Original API, index or engine registry changed')
    if any(ops.state(unit) != value for unit, value in state['shared'].items()):
        raise ValueError('Shared service changed')


def rollback(revision):
    backup = backup_path(revision)
    state = json.loads((backup / 'state.json').read_text())
    if current() not in (OLD / DLL, Path(state['target']) / DLL):
        raise ValueError('Station was superseded; rollback refused')
    assert_unchanged(state)
    if ONLINE_PROXY.read_text() not in ((backup / 'online-before.conf').read_text(), state['onlineAfter']):
        raise ValueError('Online proxy changed outside this rollout')
    if DROPIN.exists() and DROPIN.read_text() not in (state['disabledOverride'], state['enabledOverride']):
        raise ValueError('Relay unit override changed')
    if RELAY_PROXY.exists() and RELAY_PROXY.read_text() != state['relayProxy']:
        raise ValueError('Relay proxy changed')
    DROPIN.unlink(missing_ok=True)
    ops.replace_config(ONLINE_PROXY, (backup / 'online-before.conf').read_text())
    RELAY_PROXY.unlink(missing_ok=True)
    ops.run(['nginx', '-t'])
    ops.run(['systemctl', 'reload', 'nginx.service'])
    ops.run(['systemctl', 'daemon-reload'])
    ops.run(['systemctl', 'restart', SERVICE])
    ops.ready('http://127.0.0.1:5192')
    if current() != OLD / DLL:
        raise ValueError('Original Station API did not return')
    print('Original Station API restored; POCO license and library retained', flush=True)


def apply(revision):
    relay_check = load('verificar-relay-station.py')
    backup = backup_path(revision)
    candidate = Path('/mnt/DADOS/station-api-relay-candidate-20261005-' + revision[:7])
    target = Path('/opt/turborama-station-relay-20261005-' + revision[:7])
    report = dict(applied=False, service=SERVICE, sourceRevision=revision)
    activated = False
    stage = 'preflight'
    try:
        if ops.run(['git', '-c', 'safe.directory=' + str(ROOT), 'rev-parse', 'HEAD'], cwd=ROOT).strip() != revision or ops.run(
            ['git', '-c', 'safe.directory=' + str(ROOT), 'status', '--porcelain'], cwd=ROOT).strip():
            raise ValueError('Exact clean source required')
        if current() != OLD / DLL or ops.digest(OLD / DLL) != OLD_SHA or ops.digest(INDEX) != INDEX_SHA or ops.digest(ONLINE_PROXY) != ONLINE_SHA:
            raise ValueError('Inspected production changed')
        if any(path.exists() for path in (backup, target, RESULT, DROPIN, RELAY_PROXY)):
            raise ValueError('Deployment already prepared')
        values, ids = ops.runtime()
        if values.get('Station__Online__Enabled', '').lower() != 'true' or values.get('Station__Online__RelayEnabled', 'false').lower() != 'false':
            raise ValueError('Existing online flags differ')
        if values.get('Station__LibraryIndexFile') != str(INDEX) or values.get('Station__Online__EngineRegistryFile') != str(REGISTRY):
            raise ValueError('Current index or engines differ')
        metadata = json.loads((candidate / 'release.json').read_text())
        manifest = files(candidate)
        manifest.pop('release.json')
        if metadata['sourceRevision'] != revision or manifest != metadata['files']:
            raise ValueError('Candidate hashes differ')
        shared = {unit: ops.state(unit) for unit in online.SHARED}
        if any('ActiveState=active' not in value for value in shared.values()):
            raise ValueError('Shared service unhealthy')
        index = json.loads(INDEX.read_text())
        if index['revision'] != 14 or sum(row.get('catalogVisible', True) for row in index['items']) != 2212:
            raise ValueError('Inspected catalog differs')
        unchanged = {str(path): ops.digest(path) for path in DROPIN.parent.glob('*.conf')}
        unchanged['/etc/nginx/snippets/turborama-station.locations.conf'] = ops.digest(Path('/etc/nginx/snippets/turborama-station.locations.conf'))
        override = '[Service]\nWorkingDirectory=' + str(target) + '\nExecStart=\nExecStart=/usr/bin/dotnet ' + str(target / DLL) + '\nEnvironment=Station__LibraryVerifyContentOnLoad=false\nEnvironment=Station__Online__RelayMaxRooms=512\nEnvironment=Station__Online__RelayEnabled='
        relay_proxy = (ROOT / 'ops/nginx-v1-station-relay.conf').read_text()
        state = dict(target=str(target), oldFiles=files(OLD), unchangedConfigurations=unchanged, shared=shared,
            disabledOverride=override + 'false\n', enabledOverride=override + 'true\n', relayProxy=relay_proxy,
            onlineAfter='include ' + str(RELAY_PROXY) + ';\n' + ONLINE_PROXY.read_text())
        stage = 'backup_restore'
        backup.mkdir(mode=0o700)
        shutil.copytree(OLD, backup / 'api')
        shutil.copytree(backup / 'api', backup / 'restored-api')
        if files(backup / 'restored-api') != state['oldFiles']:
            raise ValueError('Restored API backup differs')
        shutil.rmtree(backup / 'restored-api')
        shutil.copyfile(ONLINE_PROXY, backup / 'online-before.conf')
        ops.private_text(backup / 'state.json', json.dumps(state))
        with (backup / 'database.dump').open('xb') as dump:
            os.fchmod(dump.fileno(), 0o600)
            result = subprocess.run(['runuser', '-u', 'postgres', '--', 'pg_dump', '--format=custom', '--dbname', ops.database_name(values)],
                stdout=dump, stderr=subprocess.PIPE, timeout=300)
            if result.returncode:
                raise ValueError('Private database backup failed')
        if 'backup restored' not in ops.run(['pg_virtualenv', '-t', 'python3', str(Path(__file__).resolve()), '--restore-backup', str(backup)], timeout=360):
            raise ValueError('Private database restore check failed')
        report['backupRestoreVerified'] = True
        shutil.copytree(candidate, target)
        for path in (target, *target.rglob('*')):
            if path.is_symlink():
                raise ValueError('Installed release contains a link')
            os.chown(path, 0, ids['Gid'][1])
            path.chmod(0o750 if path.is_dir() else 0o640)
        if files(target) != files(candidate):
            raise ValueError('Installed artifact hashes differ')
        stage = 'shadow_candidate'
        shadow = dict(values)
        shadow['Station__Online__RelayMaxRooms'] = '512'
        shadow['Station__LibraryVerifyContentOnLoad'] = 'false'
        for key in ('INVOCATION_ID', 'NOTIFY_SOCKET', 'LISTEN_FDS', 'LISTEN_PID', 'LISTEN_FDNAMES', 'JOURNAL_STREAM'):
            shadow.pop(key, None)
        for enabled in (False, True):
            with socket.socket() as probe:
                probe.bind(('127.0.0.1', 0))
                port = probe.getsockname()[1]
            base = 'http://127.0.0.1:' + str(port)
            shadow['Station__Online__RelayEnabled'] = str(enabled).lower()
            with (backup / ('candidate-' + str(enabled).lower() + '.log')).open('xb') as log:
                os.fchmod(log.fileno(), 0o600)
                process = subprocess.Popen(['/usr/bin/dotnet', str(target / DLL), '--urls', base], cwd=target,
                    env=shadow, stdout=log, stderr=log, user=ids['Uid'][1], group=ids['Gid'][1], extra_groups=ids['Groups'])
                try:
                    started = time.monotonic()
                    ops.ready(base, process)
                    report['candidateStartupSeconds' + str(enabled)] = time.monotonic() - started
                    report['candidateRelay' + str(enabled)] = relay_check.verify(index, shadow, base, enabled)
                finally:
                    process.terminate()
                    try:
                        process.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
        print(json.dumps({'stage': 'candidate_verified', 'backupRestoreVerified': True}), flush=True)
        assert_unchanged(state)
        if current() != OLD / DLL:
            raise ValueError('Production changed before activation')
        stage = 'publish_disabled'
        activated = True
        ops.private_text(DROPIN, state['disabledOverride'])
        DROPIN.chmod(0o644)
        ops.run(['systemctl', 'daemon-reload'])
        ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        if current() != target / DLL:
            raise ValueError('New override did not select reviewed binary')
        running, _ = ops.runtime()
        report['publishedDisabled'] = relay_check.verify(index, running, 'http://127.0.0.1:5192', False)
        stage = 'publish_relay'
        ops.private_text(RELAY_PROXY, relay_proxy)
        RELAY_PROXY.chmod(0o644)
        ops.replace_config(ONLINE_PROXY, state['onlineAfter'])
        ops.run(['nginx', '-t'])
        ops.run(['systemctl', 'reload', 'nginx.service'])
        ops.replace_config(DROPIN, state['enabledOverride'])
        ops.run(['systemctl', 'daemon-reload'])
        ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        running, _ = ops.runtime()
        if running.get('Station__Online__RelayEnabled') != 'true' or running.get('Station__Online__RelayMaxRooms') != '512' or current() != target / DLL:
            raise ValueError('Effective relay flag or binary differs')
        stage = 'public_wss'
        report['publicVerification'] = relay_check.verify(index, running, 'https://app.lzgames.com.br', True)
        assert_unchanged(state)
        report.update(applied=True, dllSha256=metadata['dllSha256'], execStart=str(target / DLL),
            indexRevision=14, indexSha256=INDEX_SHA, engineRegistrySha256=REGISTRY_SHA,
            onlineEnabled=True, relayEnabled=True, relayMaximumRooms=512, relayMaximumConnections=1024,
            libraryVerifyContentOnLoad=False,
            migrationsApplied=0, customerNotificationsSent=False,
            originalLicenseChanged=False, sharedServicesPreserved=True, indexKeysScannerMediaPreserved=True,
            pid=int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value']).strip()),
            completedAtUtc=datetime.now(timezone.utc).isoformat(), backup=str(backup))
        private_report(RESULT, report)
        print(json.dumps(report, ensure_ascii=False, indent=2), flush=True)
    except Exception as error:
        report.update(failedStage=stage, errorType=type(error).__name__)
        # Verification errors contain only fixed check names. Other exception
        # text can include ephemeral credentials; do not print it.
        if isinstance(error, ValueError):
            report['safeReason'] = str(error)
        if activated:
            rollback(revision)
            report['rolledBack'] = True
        private_report(RESULT.with_name(RESULT.stem + '-failed-' + revision[:7] + '.json'), report)
        print(json.dumps(report, ensure_ascii=False, indent=2), flush=True)
        raise SystemExit(1)


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apply')
    group.add_argument('--rollback')
    group.add_argument('--restore-backup', type=Path)
    args = parser.parse_args()
    if args.restore_backup:
        restore_check(args.restore_backup)
        return
    if os.geteuid() != 0:
        raise ValueError('Native root authentication required')
    if args.rollback:
        rollback(args.rollback)
    else:
        apply(args.apply)


if __name__ == '__main__':
    main()
