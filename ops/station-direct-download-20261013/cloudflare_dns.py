#!/usr/bin/env python3
"""Update one Station origin record and manage its owned ACME challenges."""
import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import time
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

HOST = 'downloads-station.lzgames.com.br'
BASE = 'https://api.cloudflare.com/client/v4'
ACME_COMMENT = 'station-direct-download-acme'


def credentials(path):
    source = Path(path)
    info = source.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_mode & 0o077 or info.st_size > 8192:
        raise ValueError('DNS credentials require a private regular file')
    value = json.loads(source.read_text())
    if not isinstance(value, dict):
        raise ValueError('Invalid DNS credentials file')
    if value.get('hostname') != HOST or not re.fullmatch('[0-9a-f]{32}', value.get('zone_id', '')):
        raise ValueError('Unexpected Station DNS identity')
    if not re.fullmatch('[A-Za-z0-9_-]{32,128}', value.get('token', '')):
        raise ValueError('Invalid scoped DNS credential')
    if not re.fullmatch('[0-9a-f]{32}', value.get('record_id', '')):
        raise ValueError('The owned Station DNS record identity is required')
    return value


class Cloudflare:
    def __init__(self, settings):
        self.settings = settings

    def request(self, method, path, body=None):
        data = json.dumps(body).encode() if body is not None else None
        req = Request(BASE + '/zones/' + self.settings['zone_id'] + path, data=data,
            method=method, headers={'Authorization': 'Bearer ' + self.settings['token'],
                'Content-Type': 'application/json', 'Accept': 'application/json'})
        try:
            with urlopen(req, timeout=15) as response:
                value = json.load(response)
        except HTTPError as error:
            raise RuntimeError('Cloudflare request rejected: HTTP ' + str(error.code)) from None
        if value.get('success') is not True:
            raise RuntimeError('Cloudflare rejected the scoped DNS operation')
        return value.get('result')

    def origin(self):
        records = self.request('GET', '/dns_records?' + urlencode({'type': 'A', 'name': HOST}))
        if not isinstance(records, list) or len(records) != 1:
            raise ValueError('Exactly one Station origin A record is required')
        record = records[0]
        if record.get('name') != HOST or record.get('type') != 'A' or record.get('proxied') is not False:
            raise ValueError('Station download origin must remain DNS-only')
        if not re.fullmatch('[0-9a-f]{32}', record.get('id', '')):
            raise ValueError('Invalid Station DNS record identity')
        expected = self.settings.get('record_id')
        if expected and record['id'] != expected:
            raise ValueError('The owned Station DNS record was replaced')
        return record


def public_ipv4():
    result = subprocess.run(['/usr/bin/curl', '--ipv4', '--fail', '--silent',
        '--connect-timeout', '5', '--max-time', '10', 'https://www.cloudflare.com/cdn-cgi/trace'],
        capture_output=True, text=True, check=True)
    values = [line[3:] for line in result.stdout.splitlines() if line.startswith('ip=')]
    if len(values) != 1 or not ipaddress.IPv4Address(values[0]).is_global:
        raise ValueError('A verified public IPv4 address is unavailable')
    return values[0]


def update(client, address=None):
    record = client.origin()
    address = public_ipv4() if address is None else address
    if not ipaddress.IPv4Address(address).is_global:
        raise ValueError('Private or reserved addresses cannot be published')
    if record.get('content') == address:
        return {'changed': False, 'hostname': HOST}
    changed = client.request('PATCH', '/dns_records/' + record['id'], {'content': address})
    if changed.get('id') != record['id'] or changed.get('name') != HOST or \
            changed.get('content') != address or changed.get('proxied') is not False:
        raise RuntimeError('The Station DNS update was not confirmed')
    return {'changed': True, 'hostname': HOST}


def acme_values(environment):
    if environment.get('CERTBOT_DOMAIN') != HOST:
        raise ValueError('ACME is restricted to the Station download hostname')
    validation = environment.get('CERTBOT_VALIDATION', '')
    if not re.fullmatch('[A-Za-z0-9_-]{16,256}', validation):
        raise ValueError('Invalid ACME challenge')
    return '_acme-challenge.' + HOST, validation


def challenge_visible(name, validation):
    req = Request('https://cloudflare-dns.com/dns-query?' + urlencode({'name': name, 'type': 'TXT'}),
                  headers={'Accept': 'application/dns-json'})
    with urlopen(req, timeout=10) as response:
        result = json.load(response)
    return any(row.get('type') == 16 and row.get('data', '').strip('"') == validation
               for row in result.get('Answer', []))


def auth(client, environment):
    name, validation = acme_values(environment)
    record = client.request('POST', '/dns_records', {'type': 'TXT', 'name': name,
        'content': validation, 'ttl': 60, 'comment': ACME_COMMENT})
    identifier = record.get('id', '')
    if not re.fullmatch('[0-9a-f]{32}', identifier) or record.get('name') != name or \
            record.get('type') != 'TXT' or record.get('content') != validation or \
            record.get('comment') != ACME_COMMENT:
        raise RuntimeError('ACME challenge creation was not confirmed')
    try:
        deadline = time.monotonic() + 150
        while time.monotonic() < deadline:
            try:
                if challenge_visible(name, validation):
                    return identifier
            except (OSError, ValueError):
                pass
            time.sleep(3)
        raise RuntimeError('ACME challenge propagation timed out')
    except Exception:
        # A failed authentication hook may not be followed by Certbot's cleanup hook.
        owned = dict(environment, CERTBOT_AUTH_OUTPUT=identifier)
        cleanup(client, owned)
        raise


def cleanup(client, environment):
    name, validation = acme_values(environment)
    identifier = environment.get('CERTBOT_AUTH_OUTPUT', '').strip()
    if not re.fullmatch('[0-9a-f]{32}', identifier):
        raise ValueError('The owned ACME record identifier is unavailable')
    record = client.request('GET', '/dns_records/' + identifier)
    if record.get('id') != identifier or record.get('name') != name or record.get('type') != 'TXT' or \
            record.get('content') != validation or record.get('comment') != ACME_COMMENT:
        raise ValueError('Refusing to delete a challenge whose ownership changed')
    deleted = client.request('DELETE', '/dns_records/' + identifier)
    if deleted.get('id') != identifier:
        raise RuntimeError('ACME challenge removal was not confirmed')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--credentials', type=Path, required=True)
    parser.add_argument('action', choices=('update', 'auth', 'cleanup'))
    args = parser.parse_args()
    client = Cloudflare(credentials(args.credentials))
    if args.action == 'update':
        print(json.dumps(update(client)))
    elif args.action == 'auth':
        print(auth(client, os.environ))
    else:
        cleanup(client, os.environ)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        # Never print HTTP bodies, credentials, headers or challenge values.
        raise SystemExit(type(error).__name__ + ': ' + str(error))
