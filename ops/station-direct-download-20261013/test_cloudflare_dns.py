"""Exercise record ownership, address failures and ACME cleanup without live DNS writes."""
import copy
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import cloudflare_dns as dns

ID = 'a' * 32
OTHER = 'b' * 32
PUBLIC = '187.21.52.192'
ENV = {'CERTBOT_DOMAIN': dns.HOST, 'CERTBOT_VALIDATION': 'c' * 43}


class Fake:
    def __init__(self, records):
        self.records = records
        self.calls = []

    def origin(self):
        return dns.Cloudflare.origin(self)

    @property
    def settings(self):
        return {'record_id': ID}

    def request(self, method, path, body=None):
        self.calls.append((method, path, body))
        if method == 'GET' and '?' in path:
            return copy.deepcopy(self.records)
        if method == 'GET':
            return copy.deepcopy(self.records[0])
        if method == 'POST':
            self.records = [dict(body, id=ID)]
            return copy.deepcopy(self.records[0])
        if method == 'PATCH':
            self.records[0].update(body)
            return copy.deepcopy(self.records[0])
        if method == 'DELETE':
            return {'id': ID}
        raise AssertionError('Unexpected operation')


def origin(**overrides):
    return dict({'id': ID, 'type': 'A', 'name': dns.HOST, 'proxied': False,
                 'content': PUBLIC}, **overrides)


class DnsContracts(unittest.TestCase):
    def test_unchanged_address_is_read_only(self):
        client = Fake([origin()])
        self.assertFalse(dns.update(client, PUBLIC)['changed'])
        self.assertEqual(['GET'], [call[0] for call in client.calls])

    def test_changed_address_updates_only_owned_a_content(self):
        client = Fake([origin(content='8.8.8.8')])
        self.assertTrue(dns.update(client, PUBLIC)['changed'])
        self.assertEqual(('PATCH', '/dns_records/' + ID, {'content': PUBLIC}), client.calls[-1])

    def test_missing_duplicate_replaced_proxied_or_foreign_records_are_never_written(self):
        cases = [[], [origin(), origin()], [origin(id=OTHER)], [origin(proxied=True)],
                 [origin(type='AAAA')], [origin(name='app.lzgames.com.br')]]
        for records in cases:
            with self.subTest(records=records):
                client = Fake(records)
                with self.assertRaises(ValueError):
                    dns.update(client, PUBLIC)
                self.assertEqual(['GET'], [call[0] for call in client.calls])

    def test_non_public_and_ipv6_addresses_are_never_published(self):
        for address in ('192.168.0.141', '127.0.0.1', '100.64.0.1', '0.0.0.0', '::1'):
            client = Fake([origin()])
            with self.assertRaises(ValueError):
                dns.update(client, address)
            self.assertEqual(['GET'], [call[0] for call in client.calls])

    def test_public_ip_detection_rejects_ambiguous_or_private_response(self):
        for response in ('ip=192.168.0.141\n', 'ip=8.8.8.8\nip=1.1.1.1\n', ''):
            with patch.object(dns.subprocess, 'run') as run:
                run.return_value.stdout = response
                with self.assertRaises(ValueError):
                    dns.public_ipv4()

    def test_unrelated_acme_domains_are_rejected_before_api_call(self):
        client = Fake([])
        with self.assertRaises(ValueError):
            dns.auth(client, dict(ENV, CERTBOT_DOMAIN='app.lzgames.com.br'))
        self.assertEqual([], client.calls)

    def test_acme_visible_returns_only_owned_cleanup_id(self):
        client = Fake([])
        with patch.object(dns, 'challenge_visible', return_value=True):
            self.assertEqual(ID, dns.auth(client, ENV))
        dns.cleanup(client, dict(ENV, CERTBOT_AUTH_OUTPUT=ID))
        self.assertEqual(('DELETE', '/dns_records/' + ID, None), client.calls[-1])

    def test_failed_acme_auth_cleans_its_own_challenge(self):
        client = Fake([])
        with patch.object(dns.time, 'monotonic', side_effect=[0, 151]):
            with self.assertRaisesRegex(RuntimeError, 'timed out'):
                dns.auth(client, ENV)
        self.assertEqual(('DELETE', '/dns_records/' + ID, None), client.calls[-1])

    def test_cleanup_cannot_delete_foreign_or_changed_record(self):
        challenge = dict(type='TXT', name='_acme-challenge.' + dns.HOST,
                         content=ENV['CERTBOT_VALIDATION'], comment=dns.ACME_COMMENT, id=ID)
        for changed in ({'name': 'app.lzgames.com.br'}, {'type': 'A'}, {'content': 'other'},
                        {'comment': 'other'}, {'id': OTHER}):
            client = Fake([dict(challenge, **changed)])
            with self.assertRaises(ValueError):
                dns.cleanup(client, dict(ENV, CERTBOT_AUTH_OUTPUT=ID))
            self.assertFalse(any(call[0] == 'DELETE' for call in client.calls))

    def test_credentials_must_be_private_regular_file(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'credentials'
            source.write_text('{}')
            source.chmod(0o644)
            with self.assertRaises(ValueError):
                dns.credentials(source)
            link = Path(directory) / 'link'
            link.symlink_to(source)
            with self.assertRaises(ValueError):
                dns.credentials(link)


if __name__ == '__main__':
    unittest.main()
