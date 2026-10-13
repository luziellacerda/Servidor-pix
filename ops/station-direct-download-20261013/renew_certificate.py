#!/usr/bin/env python3
"""Renew only the pinned Station download certificate using its existing private key."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path('/etc/station-direct-download')
SCRIPTS = Path('/opt/station-direct-download')
HOST = 'downloads-station.lzgames.com.br'


def run(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, **kwargs)


def verify_certificate(leaf, chain, fullchain, identity):
    run(['/usr/bin/openssl', 'verify', '-CApath', '/etc/ssl/certs', '-untrusted', str(chain), str(leaf)])
    run(['/usr/bin/openssl', 'x509', '-in', str(leaf), '-noout', '-checkhost', HOST])
    san = run(['/usr/bin/openssl', 'x509', '-in', str(leaf), '-noout', '-ext', 'subjectAltName']).stdout.decode()
    if san.count('DNS:') != 1 or 'DNS:' + HOST not in san:
        raise ValueError('The certificate must identify only the Station download origin')
    pem = run(['/usr/bin/openssl', 'x509', '-in', str(leaf), '-pubkey', '-noout']).stdout
    der = run(['/usr/bin/openssl', 'pkey', '-pubin', '-outform', 'DER'], input=pem).stdout
    if hashlib.sha256(der).hexdigest() != identity['spkiSha256']:
        raise ValueError('Refusing a certificate whose pinned public key changed')
    key = run(['/usr/bin/openssl', 'pkey', '-in', str(ROOT / 'tls/private-key.pem'),
               '-pubout', '-outform', 'DER']).stdout
    if hashlib.sha256(key).hexdigest() != identity['spkiSha256']:
        raise ValueError('Station TLS private key and pin differ')
    if fullchain.read_bytes() != leaf.read_bytes() + chain.read_bytes():
        raise ValueError('Certificate chain composition differs')


def renew():
    identity = json.loads((ROOT / 'tls-public-identity.json').read_text())
    if identity.get('host') != HOST:
        raise ValueError('Unexpected certificate identity')
    leaf = ROOT / 'tls/certificate.pem'
    current = subprocess.run(['/usr/bin/openssl', 'x509', '-in', str(leaf), '-noout',
                              '-checkend', str(30 * 86400)], capture_output=True)
    if current.returncode == 0:
        print(json.dumps({'hostname': HOST, 'renewalNeeded': False}))
        return
    with tempfile.TemporaryDirectory(prefix='renew-', dir=ROOT) as directory:
        temporary = Path(directory)
        auth = f'/usr/bin/python3 {SCRIPTS}/cloudflare_dns.py --credentials {ROOT}/cloudflare-credentials.json auth'
        cleanup = f'/usr/bin/python3 {SCRIPTS}/cloudflare_dns.py --credentials {ROOT}/cloudflare-credentials.json cleanup'
        run(['/usr/bin/certbot', 'certonly', '--manual', '--preferred-challenges', 'dns',
             '--non-interactive', '--agree-tos', '--register-unsafely-without-email',
             '--csr', str(ROOT / 'tls/request.csr'),
             '--cert-path', str(temporary / 'certificate.pem'),
             '--chain-path', str(temporary / 'chain.pem'),
             '--fullchain-path', str(temporary / 'fullchain.pem'),
             '--config-dir', str(ROOT / 'acme-config'),
             '--work-dir', str(ROOT / 'acme-work'), '--logs-dir', str(ROOT / 'acme-logs'),
             '--manual-auth-hook', auth, '--manual-cleanup-hook', cleanup])
        names = ('certificate.pem', 'chain.pem', 'fullchain.pem')
        verify_certificate(*(temporary / name for name in names), identity)
        backups = {name: (ROOT / 'tls' / name).read_bytes() for name in names}
        try:
            for name in names:
                source = temporary / name
                source.chmod(0o644)
                os.replace(source, ROOT / 'tls' / name)
            run(['/usr/sbin/nginx', '-t'])
            run(['/usr/bin/systemctl', 'reload', 'nginx.service'])
        except Exception:
            for name, data in backups.items():
                target = temporary / name
                target.write_bytes(data)
                target.chmod(0o644)
                os.replace(target, ROOT / 'tls' / name)
            # A failed reload leaves the previous workers running. Restore their files too.
            raise
    print(json.dumps({'hostname': HOST, 'renewalNeeded': True, 'renewed': True, 'publicKeyPreserved': True}))


if __name__ == '__main__':
    try:
        renew()
    except Exception as error:
        # Certificate hook output and credentials remain private.
        raise SystemExit('Station certificate renewal failed: ' + type(error).__name__)
