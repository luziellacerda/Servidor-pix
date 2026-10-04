#!/usr/bin/env python3
"""Deploy the reviewed Station online module only; preserve revision 4 and existing keys.

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
OLD = Path('/opt/turborama-station-speed-20261003-4bb77ed2')
OLD_DLL_SHA = 'b08f8313651a10de008d35545ff13569c5a360fb42ee792ad37f2647bd9d107e'
INDEX_SHA = 'c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a'
BACKUP = Path('/mnt/DADOS/station-online-backup-20261004')
DROPIN = Path('/etc/systemd/system/turborama-station-api.service.d/zz-station-online-20261004.conf')
RESULT = Path('/home/lz-servidor/station-online-rollout-result-20261004.json')
DLL = 'TurboRamaSuiteOnlineServer.dll'
ARTIFACT_REVISION = '77d1dfb50a9982b01d8d649db477e6268dc7a5fb'
ARTIFACT_SHA = 'ff6362852635d4d18a01e85e46c89ad5cc2a7dad75d3b99793a124733beb6509'
PROXY = Path('/etc/nginx/snippets/turborama-station.locations.conf')
ONLINE_PROXY = Path('/etc/nginx/snippets/turborama-station-online.locations.conf')
PROXY_SHA = '786b956bd00aa32d95ef422391458306cb46bd06f76a81a4b93e181d1bbd50cb'
PROXY_INCLUDE = 'include ' + str(ONLINE_PROXY) + ';\n'
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


def replace_text(path, value):
    if path.is_symlink() or not path.is_file(): raise ValueError('proxy is not a regular file')
    fd, name = tempfile.mkstemp(prefix='.station-online-', dir=path.parent)
    temporary = Path(name)
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as destination:
            destination.write(value); destination.flush(); os.fsync(destination.fileno())
            os.fchmod(destination.fileno(), 0o644)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


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
    if PROXY.read_text() not in {state['proxyReplacement'],(BACKUP/'previous-proxy.conf').read_text()}:
        raise ValueError('proxy was changed by another operation')
    if ONLINE_PROXY.exists() and ops.digest(ONLINE_PROXY) != state['onlineProxySha256']:
        raise ValueError('online proxy was changed by another operation')
    if DROPIN.exists():
        if DROPIN.is_symlink() or DROPIN.read_text() != state['override']:
            raise ValueError('Station online override was changed by another operation')
        DROPIN.unlink()
    replace_text(PROXY,(BACKUP/'previous-proxy.conf').read_text())
    ONLINE_PROXY.unlink(missing_ok=True)
    ops.run(['nginx','-t']);ops.run(['systemctl','reload','nginx.service'])
    ops.run(['systemctl', 'daemon-reload'])
    ops.run(['systemctl', 'restart', SERVICE])
    ops.ready('http://127.0.0.1:5192')
    if command_path() != OLD / DLL: raise ValueError('prior Station API did not return')
    values, _ = ops.runtime()
    if ops.digest(Path(values['Station__LibraryIndexFile'])) != INDEX_SHA:
        raise ValueError('rollback changed revision 4 content')


def apply(revision, resume=False):
    if not re.fullmatch('[0-9a-f]{40}', revision): raise ValueError('full source revision required')
    candidate = Path('/mnt/DADOS/station-api-online-candidate-20261004-' + ARTIFACT_REVISION[:7])
    target = Path('/opt/turborama-station-online-20261004-' + ARTIFACT_REVISION[:7])
    override = '[Service]\nWorkingDirectory=' + str(target) + '\nExecStart=\nExecStart=/usr/bin/dotnet ' + str(target / DLL) + '\nEnvironment=Station__Online__Enabled=true\nEnvironment=Station__Online__EngineRegistryFile=' + str(target / 'online-engine-registry.json') + '\n'
    report = dict(service=SERVICE, toolRevision=revision, sourceRevision=ARTIFACT_REVISION, applied=False, catalogRevision=4)
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
        if ops.digest(PROXY) != PROXY_SHA: raise ValueError('Station proxy changed before review')
        for path in ([DROPIN, RESULT] if resume else [BACKUP, DROPIN, target, RESULT, ONLINE_PROXY]):
            if path.exists() or path.is_symlink(): raise ValueError('deployment target already exists')
        metadata = json.loads((candidate / 'release.json').read_text())
        manifest = files(candidate)
        manifest.pop('release.json')
        if metadata['sourceCommit'] != ARTIFACT_REVISION or metadata['dllSha256'] != ARTIFACT_SHA or manifest != metadata['files'] or \
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
        proxy_candidate = ROOT / 'docs/station-android/ops/nginx-v1-station-online.conf'
        proxy_original = PROXY.read_text()
        proxy_replacement = PROXY_INCLUDE + proxy_original
        if not resume:
            BACKUP.mkdir(mode=0o700)
            shutil.copytree(OLD, BACKUP / 'previous-api')
        previous = files(OLD)
        if not resume: shutil.copyfile(PROXY, BACKUP / 'previous-proxy.conf')
        if ops.digest(BACKUP / 'previous-proxy.conf') != PROXY_SHA: raise ValueError('proxy backup differs')
        configurations = {}
        for name in ['FragmentPath', 'DropInPaths']:
            for configured in ops.run(['systemctl', 'show', SERVICE, '-p', name, '--value']).strip().split():
                path = Path(configured)
                destination = BACKUP / 'configuration' / path.relative_to('/')
                if not resume:
                    destination.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
                    shutil.copyfile(path, destination); destination.chmod(0o600)
                configurations[str(path)] = ops.digest(path)
                if ops.digest(destination) != configurations[str(path)]: raise ValueError('configuration backup differs')
        with tempfile.TemporaryDirectory(dir=BACKUP, prefix='restore-') as restored:
            destination = Path(restored) / 'api'; shutil.copytree(BACKUP / 'previous-api', destination)
            if files(destination) != previous: raise ValueError('API backup restore mismatch')
        state = dict(target=str(target), override=override, dllSha256=metadata['dllSha256'],
                     originalIndex=str(index_path), indexSha256=INDEX_SHA, sourceRevision=revision,
                     baseline=baseline, previousApiManifest=previous, configurationFiles=configurations,
                     proxyOriginalSha256=PROXY_SHA, proxyReplacement=proxy_replacement,
                     onlineProxySha256=ops.digest(proxy_candidate))
        if resume:
            saved = json.loads((BACKUP / 'state.json').read_text())
            for field in ['target','override','dllSha256','originalIndex','indexSha256','baseline',
                          'previousApiManifest','configurationFiles','proxyOriginalSha256',
                          'proxyReplacement','onlineProxySha256']:
                if saved[field] != state[field]: raise ValueError('prepared rollout state changed')
        else:
            ops.private_text(BACKUP / 'state.json', json.dumps(state, indent=2) + '\n')
        report['backupRestoreVerified'] = True
        stage = 'materialize_candidate'
        if not resume: shutil.copytree(candidate, target)
        for path in [target, *target.rglob('*')]:
            if path.is_symlink(): raise ValueError('installed candidate contains a link')
            os.chown(path, 0, ids['Gid'][1]); path.chmod(0o750 if path.is_dir() else 0o640)
        installed = files(target); installed.pop('release.json')
        if installed != manifest: raise ValueError('installed release manifest differs')
        stage = 'shadow_http'
        registry = target / 'online-engine-registry.json'
        ops.run(['runuser','-u','turborama-suite','--','test','-r',str(registry)])
        report['registryReadByServiceVerified'] = True
        report['engineRegistrySha256'] = ops.digest(registry)
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', 0)); port = probe.getsockname()[1]
        base = 'http://127.0.0.1:' + str(port)
        shadow = dict(values)
        shadow['Station__Online__Enabled'] = 'false'
        shadow['Station__Online__EngineRegistryFile'] = str(registry)
        for name in ['INVOCATION_ID', 'NOTIFY_SOCKET', 'LISTEN_FDS', 'LISTEN_PID', 'LISTEN_FDNAMES', 'JOURNAL_STREAM']:
            shadow.pop(name, None)
        log_suffix = '-resume-' + revision[:8] if resume else ''
        with (BACKUP / ('candidate' + log_suffix + '.log')).open('xb') as log:
            os.fchmod(log.fileno(), 0o600)
            process = subprocess.Popen(['/usr/bin/dotnet', str(target / DLL), '--urls', base],
                env=shadow, cwd=target, stdout=log, stderr=log, user=ids['Uid'][1],
                group=ids['Gid'][1], extra_groups=ids['Groups'])
            try:
                ops.ready(base, process)
                from urllib.request import Request,urlopen
                from urllib.error import HTTPError
                for route in ['command','events']:
                    try: response=urlopen(Request(base+'/v1/station/online/'+route,data=b'{}',headers={'Content-Type':'application/json'}),timeout=20)
                    except HTTPError as error: response=error
                    with response:
                        if response.status!=503 or json.loads(response.read())['code']!='STATION_ONLINE_DISABLED':
                            raise ValueError('disabled online endpoint changed')
                report['disabledVerification'] = True
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
        shadow['Station__Online__Enabled'] = 'true'
        with (BACKUP / ('candidate-enabled' + log_suffix + '.log')).open('xb') as log:
            os.fchmod(log.fileno(),0o600)
            process=subprocess.Popen(['/usr/bin/dotnet',str(target/DLL),'--urls',base],env=shadow,cwd=target,stdout=log,stderr=log,user=ids['Uid'][1],group=ids['Gid'][1],extra_groups=ids['Groups'])
            try:
                ops.ready(base,process)
                report['candidateVerification']=transfer.verify(index,shadow,base)
            finally:
                process.terminate()
                try: process.wait(timeout=15)
                except subprocess.TimeoutExpired: process.kill();process.wait()
        print(json.dumps({'stage':'shadow_verified','checks':report['candidateVerification']['checks'],'backupRestoreVerified':True}),flush=True)
        stage = 'activate'
        if command_path() != OLD / DLL or ops.digest(index_path) != INDEX_SHA or \
                any(ops.state(unit) != status for unit,status in baseline.items()):
            raise ValueError('production changed before activation')
        activated = True
        if ONLINE_PROXY.exists():
            if ONLINE_PROXY.is_symlink() or ops.digest(ONLINE_PROXY)!=state['onlineProxySha256']:
                raise ValueError('prepared online proxy differs')
        else:
            shutil.copyfile(proxy_candidate,ONLINE_PROXY);ONLINE_PROXY.chmod(0o644)
        replace_text(PROXY,proxy_replacement)
        ops.run(['nginx','-t']);ops.run(['systemctl','reload','nginx.service'])
        ops.private_text(DROPIN, override); DROPIN.chmod(0o644)
        ops.run(['systemctl', 'daemon-reload']); ops.run(['systemctl', 'restart', SERVICE])
        ops.ready('http://127.0.0.1:5192')
        running, _ = ops.runtime()
        if command_path() != target / DLL or ops.digest(command_path()) != metadata['dllSha256'] or \
                running['Station__LibraryIndexFile'] != str(index_path):
            raise ValueError('effective Station API does not match reviewed release')
        if running.get('Station__Online__Enabled','').lower() != 'true': raise ValueError('online flag is not effective')
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
    modes.add_argument('--resume', action='store_true')
    parser.add_argument('--source-revision')
    args = parser.parse_args()
    if os.geteuid() != 0: parser.error('authorized root access required')
    ops = module('implantar-station-20261003.py'); transfer = module('verificar-online-station.py')
    if args.apply or args.resume:
        if not args.source_revision: parser.error('--source-revision required')
        apply(args.source_revision, args.resume)
    else:
        rollback(); print(json.dumps({'rolledBack':True,'catalogRevision':4,'coversPreserved':True}))


if __name__ == '__main__':
    main()
