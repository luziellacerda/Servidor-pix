#!/usr/bin/env python3
"""Deploy one reviewed Station API binary; keep revision 4, media, keys and other services.

The candidate directory is pinned by commit plus SHA256 manifest. A private
backup is restored and verified before activation. Shadow and public HTTP
checks use disposable synthetic licenses. Rollback removes only this override.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import socket
import subprocess
import sys
import tempfile

from importlib.util import module_from_spec, spec_from_file_location

ROOT = Path(__file__).resolve().parents[3]
SERVICE = 'turborama-station-api.service'
OLD = Path('/opt/turborama-station-20261003-fd13c0d')
OLD_DLL_SHA = 'f305ae3763cb77a53a27b2c168c8de290191b3a7e88e612850856b6f7be7e639'
INDEX_SHA = 'c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a'
BACKUP = Path('/mnt/DADOS/station-speed-backup-20261003')
DROPIN = Path('/etc/systemd/system/turborama-station-api.service.d/zz-station-speed-20261003.conf')
RESULT = Path('/home/lz-servidor/station-speed-rollout-result-20261003.json')
DLL = 'TurboRamaSuiteOnlineServer.dll'
SHARED = ('turborama-pix.service', 'turborama-suite-api.service', 'turborama-suite-admin.service',
          'turborama-suite-content-gateway.service', 'nginx.service', 'cloudflared.service',
          'postgresql@16-main.service', 'redis-server.service', 'php8.3-fpm.service',
          'turbobox-php-fpm.service', 'turborama-station-management.service',
          'turborama-station-issue-admin.service')


def module(name):
    spec = spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    value = module_from_spec(spec); spec.loader.exec_module(value); return value


def command_path():
    pid = int(ops.run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value']).strip())
    argv = Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')
    return Path(argv[1].decode())


def files(directory):
    entries = {}
    for path in directory.rglob('*'):
        if path.is_symlink(): raise ValueError('release contains a link')
        if path.is_file(): entries[str(path.relative_to(directory))] = ops.digest(path)
    return entries


def saved_report(value):
    ops.private_text(RESULT, json.dumps(value, ensure_ascii=False, indent=2) + '\n')
    owner = pwd.getpwnam('lz-servidor'); os.chown(RESULT, owner.pw_uid, owner.pw_gid)


def rollback():
    state = json.loads((BACKUP / 'state.json').read_text())
    expected = Path(state['target']) / DLL
    pid = int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value']).strip())
    current = command_path() if pid else None
    configured = ops.run(['systemctl','show',SERVICE,'-p','ExecStart','--value'])
    if current not in {None, OLD / DLL, expected} or \
            not any(str(path) in configured for path in [OLD / DLL, expected]) or \
            ops.digest(OLD / DLL) != OLD_DLL_SHA or \
            ops.digest(Path(state['originalIndex'])) != INDEX_SHA:
        raise ValueError('rollback runtime differs from pinned API/content')
    for name, digest in state['configurationFiles'].items():
        if ops.digest(Path(name)) != digest: raise ValueError('prior Station configuration changed')
    if current == expected and ops.digest(expected) != state['dllSha256']:
        raise ValueError('rollback candidate binary changed')
    if DROPIN.exists():
        if DROPIN.is_symlink() or DROPIN.read_text() != state['override']:
            raise ValueError('Station speed override was changed by another operation')
        DROPIN.unlink()
    ops.run(['systemctl', 'daemon-reload'])
    ops.run(['systemctl', 'restart', SERVICE])
    ops.ready('http://127.0.0.1:5192')
    if command_path() != OLD / DLL: raise ValueError('prior Station API did not return')
    values, _ = ops.runtime()
    if ops.digest(Path(values['Station__LibraryIndexFile'])) != INDEX_SHA:
        raise ValueError('rollback changed revision 4 content')


def apply(revision):
    if not re.fullmatch('[0-9a-f]{40}', revision): raise ValueError('full source revision required')
    candidate = Path('/mnt/DADOS/station-api-speed-candidate-20261003-' + revision[:8])
    target = Path('/opt/turborama-station-speed-20261003-' + revision[:8])
    override = '[Service]\nWorkingDirectory=' + str(target) + '\nExecStart=\nExecStart=/usr/bin/dotnet ' + str(target / DLL) + '\n'
    report = dict(service=SERVICE, sourceRevision=revision, applied=False, catalogRevision=4)
    activated = False; stage = 'preflight'
    try:
        head = ops.run(['git', '-c', 'safe.directory=' + str(ROOT), 'rev-parse', 'HEAD'], cwd=ROOT).strip()
        if head != revision or ops.run(['git', '-c', 'safe.directory=' + str(ROOT), 'status', '--porcelain'], cwd=ROOT).strip():
            raise ValueError('deployment requires the exact clean source commit')
        values, ids = ops.runtime()
        index_path = Path(values['Station__LibraryIndexFile'])
        if command_path() != OLD / DLL or ops.digest(OLD / DLL) != OLD_DLL_SHA or \
                ops.digest(index_path) != INDEX_SHA or ids['Uid'][1] != 995:
            raise ValueError('Station API/content/service identity changed')
        for path in [BACKUP, DROPIN, target, RESULT]:
            if path.exists() or path.is_symlink(): raise ValueError('deployment target already exists')
        metadata = json.loads((candidate / 'release.json').read_text())
        manifest = files(candidate)
        manifest.pop('release.json')
        if metadata['sourceRevision'] != revision or manifest != metadata['files'] or \
                metadata['dllSha256'] != manifest[DLL]:
            raise ValueError('candidate commit or file manifest differs')
        report['dllSha256'] = metadata['dllSha256']
        baseline = {unit:ops.state(unit) for unit in SHARED}
        if any('ActiveState=active' not in status for status in baseline.values()):
            raise ValueError('a shared service is unhealthy')
        index = json.loads(index_path.read_text())
        if index['revision'] != 4 or len(index['items']) != 2071:
            raise ValueError('reviewed catalog counts changed')
        stage = 'backup_restore'
        BACKUP.mkdir(mode=0o700)
        shutil.copytree(OLD, BACKUP / 'previous-api')
        previous = files(OLD)
        configurations = {}
        for name in ['FragmentPath', 'DropInPaths']:
            for configured in ops.run(['systemctl', 'show', SERVICE, '-p', name, '--value']).strip().split():
                path = Path(configured)
                destination = BACKUP / 'configuration' / path.relative_to('/')
                destination.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
                shutil.copyfile(path, destination); destination.chmod(0o600)
                configurations[str(path)] = ops.digest(path)
                if ops.digest(destination) != configurations[str(path)]: raise ValueError('configuration backup differs')
        with tempfile.TemporaryDirectory(dir=BACKUP, prefix='restore-') as restored:
            destination = Path(restored) / 'api'; shutil.copytree(BACKUP / 'previous-api', destination)
            if files(destination) != previous: raise ValueError('API backup restore mismatch')
        state = dict(target=str(target), override=override, dllSha256=metadata['dllSha256'],
                     originalIndex=str(index_path), indexSha256=INDEX_SHA, sourceRevision=revision,
                     baseline=baseline, previousApiManifest=previous, configurationFiles=configurations)
        ops.private_text(BACKUP / 'state.json', json.dumps(state, indent=2) + '\n')
        report['backupRestoreVerified'] = True
        stage = 'materialize_candidate'
        shutil.copytree(candidate, target)
        for path in [target, *target.rglob('*')]:
            if path.is_symlink(): raise ValueError('installed candidate contains a link')
            os.chown(path, 0, ids['Gid'][1]); path.chmod(0o750 if path.is_dir() else 0o640)
        installed = files(target); installed.pop('release.json')
        if installed != manifest: raise ValueError('installed release manifest differs')
        stage = 'shadow_http'
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', 0)); port = probe.getsockname()[1]
        base = 'http://127.0.0.1:' + str(port)
        shadow = dict(values)
        for name in ['INVOCATION_ID', 'NOTIFY_SOCKET', 'LISTEN_FDS', 'LISTEN_PID', 'LISTEN_FDNAMES', 'JOURNAL_STREAM']:
            shadow.pop(name, None)
        with (BACKUP / 'candidate.log').open('xb') as log:
            os.fchmod(log.fileno(), 0o600)
            process = subprocess.Popen(['/usr/bin/dotnet', str(target / DLL), '--urls', base],
                env=shadow, cwd=target, stdout=log, stderr=log, user=ids['Uid'][1],
                group=ids['Gid'][1], extra_groups=ids['Groups'])
            try:
                ops.ready(base, process)
                report['candidateVerification'] = transfer.verify(index, shadow, base)
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
        print(json.dumps({'stage':'shadow_verified','covers':48,'downloads':9,'backupRestoreVerified':True}), flush=True)
        stage = 'activate'
        if command_path() != OLD / DLL or ops.digest(index_path) != INDEX_SHA or \
                any(ops.state(unit) != status for unit,status in baseline.items()):
            raise ValueError('production changed before activation')
        activated = True
        ops.private_text(DROPIN, override); DROPIN.chmod(0o644)
        ops.run(['systemctl', 'daemon-reload']); ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        running, _ = ops.runtime()
        if command_path() != target / DLL or ops.digest(command_path()) != metadata['dllSha256'] or \
                running['Station__LibraryIndexFile'] != str(index_path):
            raise ValueError('effective Station API does not match reviewed release')
        stage = 'public_https'
        report['publicHttpsVerification'] = transfer.verify(index, running, 'https://app.lzgames.com.br')
        if any(ops.state(unit) != status for unit,status in baseline.items()) or \
                files(OLD) != previous or ops.digest(index_path) != INDEX_SHA:
            raise ValueError('shared service, original release or content changed')
        report.update(applied=True, indexSha256=INDEX_SHA, sharedServicesPreserved=True,
                      schemaKeysMediaPreserved=True, completedAtUtc=datetime.now(timezone.utc).isoformat(),
                      pid=int(ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value']).strip()))
    except Exception as error:
        report.update(failedStage=stage, errorType=type(error).__name__)
        if activated:
            rollback(); report['rolledBack'] = True
        if BACKUP.exists(): ops.private_text(BACKUP / 'result.json', json.dumps(report, indent=2) + '\n')
        if not RESULT.exists(): saved_report(report)
        print(json.dumps({k:v for k,v in report.items() if k not in {'candidateVerification','publicHttpsVerification'}}), flush=True)
        raise SystemExit(1)
    ops.private_text(BACKUP / 'result.json', json.dumps(report, indent=2) + '\n')
    saved_report(report)
    print(json.dumps({k:v for k,v in report.items() if k not in {'candidateVerification','publicHttpsVerification'}}), flush=True)


def main():
    global ops, transfer
    os.umask(0o077); sys.dont_write_bytecode = True
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument('--apply', action='store_true'); modes.add_argument('--rollback', action='store_true')
    parser.add_argument('--source-revision')
    args = parser.parse_args()
    if os.geteuid() != 0: parser.error('authorized root access required')
    ops = module('implantar-station-20261003.py'); transfer = module('verificar-transferencia-station.py')
    if args.apply:
        if not args.source_revision: parser.error('--source-revision required')
        apply(args.source_revision)
    else:
        rollback(); print(json.dumps({'rolledBack':True,'catalogRevision':4,'coversPreserved':True}))


if __name__ == '__main__':
    main()
