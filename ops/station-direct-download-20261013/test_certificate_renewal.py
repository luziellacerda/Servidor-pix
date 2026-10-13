"""Fault injection for renewal ordering and rollback; no root, DNS or service writes."""
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import renew_certificate as renewal


class RenewalTransaction(unittest.TestCase):
    def exercise(self, near_expiry=True, rejected=False, reload_failure=False):
        calls = []
        names = ('certificate.pem', 'chain.pem', 'fullchain.pem')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'tls').mkdir()
            (root / 'tls-public-identity.json').write_text(json.dumps({'host': renewal.HOST, 'spkiSha256': 'a' * 64}))
            before = {name: ('old-' + name).encode() for name in names}
            for name, data in before.items():
                (root / 'tls' / name).write_bytes(data)

            def process(args, **kwargs):
                calls.append(args)
                if args[0] == '/usr/bin/openssl' and '-checkend' in args:
                    return subprocess.CompletedProcess(args, 1 if near_expiry else 0, b'', b'')
                if args[0] == '/usr/bin/certbot':
                    for flag, name in zip(('--cert-path', '--chain-path', '--fullchain-path'), names):
                        Path(args[args.index(flag) + 1]).write_bytes(('new-' + name).encode())
                elif args[0] == '/usr/bin/systemctl':
                    if reload_failure:
                        raise subprocess.CalledProcessError(1, args, b'', b'reload-failure-fixture')
                elif args[0] != '/usr/sbin/nginx':
                    raise AssertionError('Unexpected system operation')
                return subprocess.CompletedProcess(args, 0, b'', b'')

            def verify(leaf, chain, fullchain, identity):
                self.assertEqual(calls[-1][0], '/usr/bin/certbot')
                for name in names:
                    self.assertEqual((root / 'tls' / name).read_bytes(), before[name])
                if rejected:
                    raise ValueError('certificate-pin-rejection-fixture')

            with patch.object(renewal, 'ROOT', root), patch.object(renewal.subprocess, 'run', side_effect=process), \
                    patch.object(renewal, 'verify_certificate', side_effect=verify), redirect_stdout(io.StringIO()):
                if rejected or reload_failure:
                    with self.assertRaises((ValueError, subprocess.CalledProcessError)):
                        renewal.renew()
                else:
                    renewal.renew()
            for name in names:
                expected = ('new-' + name).encode() if near_expiry and not rejected and not reload_failure else before[name]
                self.assertEqual((root / 'tls' / name).read_bytes(), expected)
            return calls

    def test_valid_certificate_is_read_only(self):
        self.assertEqual(len(self.exercise(near_expiry=False)), 1)

    def test_wrong_pin_stops_before_replacing_certificate(self):
        calls = self.exercise(rejected=True)
        self.assertFalse(any(args[0] == '/usr/sbin/nginx' for args in calls))

    def test_reload_failure_restores_all_previous_certificate_files(self):
        self.exercise(reload_failure=True)

    def test_success_only_reloads_nginx_and_never_restarts_the_api(self):
        calls = self.exercise()
        services = [args for args in calls if args[0] == '/usr/bin/systemctl']
        self.assertEqual(services, [['/usr/bin/systemctl', 'reload', 'nginx.service']])


if __name__ == '__main__':
    unittest.main()
