#!/usr/bin/env python3
"""Install the new direct origin without replacing existing sites or restarting the API."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

CONFIG = Path('/etc/station-direct-download')
SCRIPTS = Path('/opt/station-direct-download')
NGINX = Path('/etc/nginx/conf.d/station-direct-download.conf')
UNITS = ('station-direct-download-dns.service', 'station-direct-download-dns.timer',
         'station-direct-download-certificate.service', 'station-direct-download-certificate.timer')
TIMERS = tuple(unit for unit in UNITS if unit.endswith('.timer'))
UNITS_ROOT = Path('/etc/systemd/system')
API = 'turborama-station-api.service'


def run(args):
    return subprocess.run(args, check=True, capture_output=True).stdout.decode().strip()


def pid(unit):
    return run(['/usr/bin/systemctl', 'show', unit, '--property', 'MainPID', '--value'])


def install(source, target, mode):
    if source.is_symlink() or not source.is_file():
        raise ValueError('Deployment requires exact regular files')
    target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    shutil.copyfile(source, target)
    target.chmod(mode)


def deploy(stage):
    if os.geteuid() != 0:
        raise PermissionError('Linux administrator authentication is required')
    stage = stage.resolve(strict=True)
    if str(stage) != '/home/lz-servidor/artifacts/station-direct-download-test-20261013/deploy':
        raise ValueError('Unexpected deployment source')
    for target in (CONFIG, SCRIPTS, NGINX, *(UNITS_ROOT / name for name in UNITS)):
        if target.exists() or target.is_symlink():
            raise ValueError('Deployment target already exists; inspect before replacing anything')
    manifest = json.loads((stage / 'manifest.json').read_text())
    expected = {'cloudflare_dns.py', 'renew_certificate.py', 'station-downloads.nginx.conf',
                'cloudflare-credentials.json', 'tls-public-identity.json',
                'tls/private-key.pem', 'tls/request.csr', 'tls/certificate.pem',
                'tls/chain.pem', 'tls/fullchain.pem', *UNITS}
    account_files = set(manifest['files']) - expected
    if not account_files or any(not name.startswith('acme-config/accounts/') or '..' in name
                                for name in account_files):
        raise ValueError('Unexpected ACME account inputs')
    if set(manifest['files']) != expected | account_files:
        raise ValueError('Missing deployment inputs')
    for name, digest in manifest['files'].items():
        source = stage / name
        if source.is_symlink() or not source.is_file() or \
                hashlib.sha256(source.read_bytes()).hexdigest() != digest:
            raise ValueError('Deployment input identity changed')
    run(['/usr/sbin/nginx', '-t'])
    before_api, before_nginx = pid(API), pid('nginx.service')
    if not before_api.isdigit() or int(before_api) <= 0 or int(before_nginx) <= 0:
        raise ValueError('Existing API and nginx must be running')
    created = []
    reloaded = False
    try:
        CONFIG.mkdir(mode=0o700)
        created.append(CONFIG)
        SCRIPTS.mkdir(mode=0o755)
        created.append(SCRIPTS)
        for name in ('cloudflare_dns.py', 'renew_certificate.py'):
            install(stage / name, SCRIPTS / name, 0o755)
        for name in ('cloudflare-credentials.json', 'tls-public-identity.json', *account_files):
            install(stage / name, CONFIG / name, 0o600)
        for name in ('private-key.pem', 'request.csr', 'certificate.pem', 'chain.pem', 'fullchain.pem'):
            install(stage / 'tls' / name, CONFIG / 'tls' / name,
                    0o600 if name == 'private-key.pem' else 0o644)
        # Verify the installed chain, SAN, stable pin and private key before changing nginx.
        sys.path.insert(0, str(SCRIPTS))
        import renew_certificate
        identity = json.loads((CONFIG / 'tls-public-identity.json').read_text())
        renew_certificate.verify_certificate(CONFIG / 'tls/certificate.pem', CONFIG / 'tls/chain.pem',
                                             CONFIG / 'tls/fullchain.pem', identity)
        install(stage / 'station-downloads.nginx.conf', NGINX, 0o644)
        created.append(NGINX)
        for name in UNITS:
            target = UNITS_ROOT / name
            install(stage / name, target, 0o644)
            created.append(target)
        run(['/usr/sbin/nginx', '-t'])
        run(['/usr/bin/systemctl', 'daemon-reload'])
        run(['/usr/bin/systemctl', 'reload', 'nginx.service'])
        reloaded = True
        run(['/usr/bin/systemctl', 'start', 'station-direct-download-dns.service'])
        run(['/usr/bin/systemctl', 'start', 'station-direct-download-certificate.service'])
        run(['/usr/bin/systemctl', 'enable', '--now', *TIMERS])
        if pid(API) != before_api or pid('nginx.service') != before_nginx:
            raise RuntimeError('Unexpected process restart during direct origin deployment')
        receipt = {'hostname': 'downloads-station.lzgames.com.br', 'httpsConfigured': True,
                   'dynamicDnsTimerEnabled': True, 'certificateTimerEnabled': True,
                   'apiPidPreserved': True, 'nginxMasterPidPreserved': True,
                   'deploymentManifestSha256': hashlib.sha256((stage / 'manifest.json').read_bytes()).hexdigest()}
        (CONFIG / 'deployment-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print(json.dumps(receipt))
    except Exception:
        for timer in TIMERS:
            subprocess.run(['/usr/bin/systemctl', 'disable', '--now', timer], capture_output=True)
        for target in reversed(created):
            if target.is_dir():
                shutil.rmtree(target)
            else:
                target.unlink(missing_ok=True)
        subprocess.run(['/usr/bin/systemctl', 'daemon-reload'], capture_output=True)
        if reloaded:
            subprocess.run(['/usr/sbin/nginx', '-t'], capture_output=True, check=True)
            subprocess.run(['/usr/bin/systemctl', 'reload', 'nginx.service'], capture_output=True, check=True)
        raise


if __name__ == '__main__':
    try:
        if len(sys.argv) != 2:
            raise ValueError('Exactly one verified deployment directory is required')
        deploy(Path(sys.argv[1]))
    except Exception as error:
        raise SystemExit('Station direct origin deployment failed: ' + type(error).__name__)
