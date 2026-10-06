#!/usr/bin/env python3
"""Guarded R41 rollout from the inspected e4e557a Station release only.

No migration, proxy, license, key, media or other-service changes. Native root
authentication is required. Use the operator venv with websockets==15.0.1.
--disable-social retains the new release and existing rooms/relay. --rollback
restores e4e557a by removing only this release's unit override.
"""
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
import time
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[3]
SERVICE = 'turborama-station-api.service'
DLL = 'TurboRamaSuiteOnlineServer.dll'
OLD = Path('/opt/turborama-station-relay-20261005-e4e557a')
OLD_SHA = '7ecb6c8d94c5ab42c26638b5f9bdf7ffd0e70f99e047463ee5293af34f7bdcf5'
INDEX = Path('/mnt/DADOS/turbostation-library-auto-20261004/index.json')
INDEX_SHA = '07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92'
REGISTRY = Path('/opt/turborama-station-online-20261004-77d1dfb/online-engine-registry.json')
REGISTRY_SHA = '901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a'
DROPIN = Path('/etc/systemd/system/turborama-station-api.service.d/zzzzzzzzzzzzz-station-community-r41-20261006.conf')
RESULT = Path('/home/lz-servidor/station-community-r41-rollout-result-20261006.json')


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


def paths(revision):
    if not re.fullmatch(r'[0-9a-f]{40}', revision):
        raise ValueError('Exact source revision required')
    suffix = '20261006-' + revision[:7]
    return (Path('/mnt/DADOS/station-community-r41-backup-' + suffix),
            Path('/mnt/DADOS/station-community-r41-candidate-' + suffix),
            Path('/opt/turborama-station-community-r41-' + suffix))


def private_report(path, report):
    ops.private_text(path, json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    owner = pwd.getpwnam('lz-servidor')
    os.chown(path, owner.pw_uid, owner.pw_gid)


def telemetry():
    with urlopen('http://127.0.0.1:5192/ready/station/online', timeout=5) as response:
        return json.load(response)


def no_active_games():
    state = telemetry()
    if state['activeRooms'] or state['activeConnections']:
        raise ValueError('Active Station relay game; retry rollout after it finishes')
    return state


def real_licenses(db):
    rows = ops.sql(db, "SELECT row_to_json(l)::text FROM suite.suite_licenses l "
        "WHERE product_id='TURBORAMA_STATION_ANDROID' AND NOT EXISTS (SELECT 1 FROM "
        "suite.suite_license_deliveries d WHERE d.license_id=l.license_id AND "
        "d.source_system='STATION_ROLLOUT_TEST') ORDER BY license_id")
    return hashlib.sha256(rows.encode()).hexdigest()


def unchanged(state, check_shared=True):
    for name, sha in state['unchangedConfigurations'].items():
        if ops.digest(Path(name)) != sha:
            raise ValueError('Inspected configuration changed')
    if ops.digest(INDEX) != INDEX_SHA or ops.digest(REGISTRY) != REGISTRY_SHA or files(OLD) != state['oldFiles']:
        raise ValueError('Original API, index or engine registry changed')
    if check_shared and any(ops.state(unit) != value for unit, value in state['shared'].items()):
        raise ValueError('Shared service changed during rollout')


def restore_check(backup):
    if backup.parent != Path('/mnt/DADOS') or not backup.name.startswith('station-community-r41-backup-20261006-'):
        raise ValueError('Unexpected backup')
    if not os.environ.get('PG_CLUSTER_CONF_ROOT', '').startswith('/tmp/pg_virtualenv.') or not ops.run(
            ['psql', '-X', '-Atqc', 'SHOW data_directory']).strip().startswith('/tmp/pg_virtualenv.'):
        raise ValueError('Temporary PostgreSQL cluster required')
    ops.run(['createdb', 'station_community_restore'])
    ops.run(['pg_restore', '--exit-on-error', '--no-owner', '--no-acl', '--dbname',
             'station_community_restore', str(backup / 'database.dump')], timeout=300)
    count = ops.run(['psql', '-X', '--dbname', 'station_community_restore', '-Atqc',
        "SELECT count(*) FROM suite.schema_migrations WHERE version IN ('028_station_android','029_station_download_grants','030_station_management_audit')"]).strip()
    if count != '3':
        raise ValueError('Restored Station ledger differs')
    print('Station community database backup restored', flush=True)


def switch_back(revision, disable_only=False):
    backup, _, target = paths(revision)
    state = json.loads((backup / 'state.json').read_text())
    unchanged(state, check_shared=False)
    if online.command_path() not in (OLD / DLL, target / DLL):
        raise ValueError('Station superseded; return refused')
    if DROPIN.exists() and DROPIN.read_text() not in (state['disabledOverride'], state['enabledOverride']):
        raise ValueError('Community override changed outside this rollout')
    shared = {unit: ops.state(unit) for unit in online.SHARED}
    no_active_games()
    if disable_only:
        if online.command_path() != target / DLL or not DROPIN.exists():
            raise ValueError('Community release required to disable social')
        ops.replace_config(DROPIN, state['disabledOverride'])
    else:
        DROPIN.unlink(missing_ok=True)
    ops.run(['systemctl', 'daemon-reload'])
    ops.run(['systemctl', 'restart', SERVICE])
    ops.ready('http://127.0.0.1:5192')
    expected = target / DLL if disable_only else OLD / DLL
    values, _ = ops.runtime()
    if online.command_path() != expected or values.get('Station__Online__SocialEnabled', 'false').lower() != 'false':
        raise ValueError('Expected release or disabled social flag did not return')
    if any(ops.state(unit) != previous for unit, previous in shared.items()):
        raise ValueError('Shared service changed during return')
    print('Social disabled; rooms/relay retained' if disable_only else 'Original e4e557a Station release restored', flush=True)


def apply(revision):
    backup, candidate, target = paths(revision)
    report = dict(applied=False, service=SERVICE, sourceRevision=revision)
    activated, stage = False, 'preflight'
    try:
        if ops.run(['git', '-c', 'safe.directory=' + str(ROOT), 'rev-parse', 'HEAD'], cwd=ROOT).strip() != revision or ops.run(
                ['git', '-c', 'safe.directory=' + str(ROOT), 'status', '--porcelain'], cwd=ROOT).strip():
            raise ValueError('Exact clean source required')
        if online.command_path() != OLD / DLL or ops.digest(OLD / DLL) != OLD_SHA:
            raise ValueError('Inspected production release changed')
        if any(p.exists() for p in (backup, target, RESULT, DROPIN)):
            raise ValueError('Deployment already prepared')
        values, ids = ops.runtime()
        if any(values.get(k, '').lower() != v for k, v in {
                'Station__Online__Enabled': 'true', 'Station__Online__RelayEnabled': 'true',
                'Station__LibraryVerifyContentOnLoad': 'false', 'Station__Online__RelayMaxRooms': '512'}.items()):
            raise ValueError('Existing Station flags differ')
        if values.get('Station__Online__SocialEnabled', 'false').lower() != 'false' or \
                values.get('Station__LibraryIndexFile') != str(INDEX) or \
                values.get('Station__Online__EngineRegistryFile') != str(REGISTRY):
            raise ValueError('Existing social flag, index or engine registry differs')
        metadata = json.loads((candidate / 'release.json').read_text())
        manifest = files(candidate)
        manifest.pop('release.json')
        if metadata['sourceRevision'] != revision or manifest != metadata['files'] or \
                metadata['dllSha256'] != ops.digest(candidate / DLL):
            raise ValueError('Candidate manifest differs')
        shared = {unit: ops.state(unit) for unit in online.SHARED}
        if any('ActiveState=active' not in value for value in shared.values()):
            raise ValueError('Shared service unhealthy')
        index = json.loads(INDEX.read_text())
        if index['revision'] != 14 or sum(r.get('catalogVisible', True) for r in index['items']) != 2212:
            raise ValueError('Inspected catalog differs')
        config_paths = list(DROPIN.parent.glob('*.conf')) + [
            Path(ops.run(['systemctl', 'show', SERVICE, '-p', 'FragmentPath', '--value']).strip()),
            Path('/etc/turborama-suite/station-5192.env'),
            Path('/etc/turborama-suite/station-rev3-20261003.env'),
            Path('/etc/turborama-suite/station-covers-revista-20261003-rev4.env'),
            Path('/opt/turborama-station-folders-20261004-931030b/station-library.env'),
            Path('/etc/nginx/snippets/turborama-station.locations.conf'),
            Path('/etc/nginx/snippets/turborama-station-online.locations.conf'),
            Path('/etc/nginx/snippets/turborama-station-relay.locations.conf')]
        override = '[Service]\nWorkingDirectory=' + str(target) + '\nExecStart=\nExecStart=/usr/bin/dotnet ' + str(target / DLL) + '\nEnvironment=Station__Online__SocialEnabled='
        state = dict(target=str(target), oldFiles=files(OLD), shared=shared,
            unchangedConfigurations={str(p): ops.digest(p) for p in config_paths},
            disabledOverride=override + 'false\n', enabledOverride=override + 'true\n')
        unchanged(state)
        report['relayBefore'] = no_active_games()
        db = ops.database_name(values)
        license_sha = real_licenses(db)
        stage = 'backup_restore'
        backup.mkdir(mode=0o700)
        shutil.copytree(OLD, backup / 'api')
        shutil.copytree(backup / 'api', backup / 'restored-api')
        if files(backup / 'restored-api') != state['oldFiles']:
            raise ValueError('Restored API backup differs')
        shutil.rmtree(backup / 'restored-api')
        configurations = backup / 'configuration'
        configurations.mkdir(mode=0o700)
        for i, name in enumerate(state['unchangedConfigurations']):
            shutil.copy2(name, configurations / (str(i) + '.backup'))
        ops.private_text(backup / 'state.json', json.dumps(state))
        with (backup / 'database.dump').open('xb') as dump:
            os.fchmod(dump.fileno(), 0o600)
            outcome = subprocess.run(['runuser', '-u', 'postgres', '--', 'pg_dump', '--format=custom', '--dbname', db],
                stdout=dump, stderr=subprocess.PIPE, timeout=300)
            if outcome.returncode:
                raise ValueError('Private database backup failed')
        if 'backup restored' not in ops.run(['pg_virtualenv', '-t', 'python3', str(Path(__file__).resolve()),
                                            '--restore-backup', str(backup)], timeout=360):
            raise ValueError('Private database restore check failed')
        report['backupRestoreVerified'] = True
        shutil.copytree(candidate, target)
        for p in (target, *target.rglob('*')):
            os.chown(p, 0, ids['Gid'][1])
            p.chmod(0o750 if p.is_dir() else 0o640)
        if files(target) != files(candidate):
            raise ValueError('Installed artifact differs')
        shadow = dict(values)
        for k in ('INVOCATION_ID', 'NOTIFY_SOCKET', 'LISTEN_FDS', 'LISTEN_PID', 'LISTEN_FDNAMES', 'JOURNAL_STREAM'):
            shadow.pop(k, None)
        social = load('verificar-comunidade-station-r41.py')
        relay = load('verificar-relay-station.py')
        for enabled in (False, True):
            stage = 'shadow_social_' + str(enabled).lower()
            with socket.socket() as probe:
                probe.bind(('127.0.0.1', 0))
                port = probe.getsockname()[1]
            base = 'http://127.0.0.1:' + str(port)
            shadow['Station__Online__SocialEnabled'] = str(enabled).lower()
            with (backup / ('candidate-' + str(enabled).lower() + '.log')).open('xb') as log:
                os.fchmod(log.fileno(), 0o600)
                process = subprocess.Popen(['/usr/bin/dotnet', str(target / DLL), '--urls', base], cwd=target,
                    env=shadow, stdout=log, stderr=log, user=ids['Uid'][1], group=ids['Gid'][1], extra_groups=ids['Groups'])
                try:
                    ops.ready(base, process)
                    report['candidateSocial' + str(enabled)] = social.verify(index, shadow, base, enabled)
                    if enabled:
                        report['candidateRelayRegression'] = relay.verify(index, shadow, base, True)
                finally:
                    process.terminate()
                    try:
                        process.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
        print(json.dumps(dict(stage='candidate_verified', backupRestoreVerified=True)), flush=True)
        unchanged(state)
        no_active_games()
        if online.command_path() != OLD / DLL or real_licenses(db) != license_sha:
            raise ValueError('Production release or customer licenses changed before activation')
        stage = 'publish_disabled'
        activated = True
        ops.private_text(DROPIN, state['disabledOverride'])
        DROPIN.chmod(0o644)
        ops.run(['systemctl', 'daemon-reload'])
        ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        if online.command_path() != target / DLL:
            raise ValueError('Override did not select the reviewed binary')
        running, _ = ops.runtime()
        report['publishedDisabled'] = social.verify(index, running, 'http://127.0.0.1:5192', False)
        stage = 'publish_social'
        ops.replace_config(DROPIN, state['enabledOverride'])
        ops.run(['systemctl', 'daemon-reload'])
        ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        running, _ = ops.runtime()
        if online.command_path() != target / DLL or running.get('Station__Online__SocialEnabled') != 'true':
            raise ValueError('Effective social flag or binary differs')
        stage = 'public_social_and_relay'
        report['publicVerification'] = social.verify(index, running, 'https://app.lzgames.com.br', True, True)
        unchanged(state)
        if real_licenses(db) != license_sha:
            raise ValueError('Customer license rows changed during rollout')
        report.update(applied=True, dllSha256=metadata['dllSha256'], execStart=str(target / DLL),
            socialEnabled=True, onlineEnabled=True, relayEnabled=True, relayMaximumRooms=512,
            relayMaximumConnections=1024, directHistoryMaximumMessages=32, directHistoryMaximumBytes=65536,
            indexRevision=14, indexSha256=INDEX_SHA, engineRegistrySha256=REGISTRY_SHA,
            migrationsApplied=0, customerLicenseRowsPreserved=True, pocoLicenseChanged=False,
            sharedServicesPreserved=True, proxyKeysScannerMediaPreserved=True, customerNotificationsSent=False,
            androidApkInstalledByThisRollout=False, androidGameplay=False,
            relayAfter=telemetry(), pid=int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value']).strip()),
            completedAtUtc=datetime.now(timezone.utc).isoformat(), backup=str(backup))
        private_report(RESULT, report)
        print(json.dumps({k: v for k, v in report.items() if k not in (
            'candidateSocialFalse', 'candidateSocialTrue', 'candidateRelayRegression', 'publishedDisabled', 'publicVerification')},
            ensure_ascii=False, indent=2), flush=True)
    except Exception as error:
        report.update(failedStage=stage, errorType=type(error).__name__)
        if isinstance(error, ValueError):
            report['safeReason'] = str(error)
        if activated:
            try:
                switch_back(revision)
                report['rolledBack'] = True
            except Exception as failure:
                report.update(rolledBack=False, rollbackErrorType=type(failure).__name__)
        failed = RESULT.with_name(RESULT.stem + '-failed-' + revision[:7] + '-' + str(os.getpid()) + '.json')
        private_report(failed, report)
        print(json.dumps({k: v for k, v in report.items() if not isinstance(v, dict)}, ensure_ascii=False), flush=True)
        raise SystemExit(1)


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--apply')
    group.add_argument('--disable-social')
    group.add_argument('--rollback')
    group.add_argument('--restore-backup', type=Path)
    args = parser.parse_args()
    if args.restore_backup:
        restore_check(args.restore_backup)
        return
    if os.geteuid() != 0:
        raise ValueError('Native Linux administrator authentication required')
    if args.apply:
        apply(args.apply)
    else:
        switch_back(args.disable_social or args.rollback, bool(args.disable_social))


if __name__ == '__main__':
    main()
