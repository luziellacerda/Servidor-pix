#!/usr/bin/env python3
"""Authenticated Station transfer checks and bounded timing with a disposable license.

Reports contain operation names, sizes, hashes and elapsed times only. No
credentials, customer data, grants or private media paths enter the report.
The measured client runs on Linux; this is not an Android rendering benchmark.
"""
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
from pathlib import Path
import statistics
import threading
import time

from cryptography.hazmat.primitives import serialization


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def verify(index, values, base, burst_count=48):
    operations = module('implantar-station-20261003.py')
    helper = module('verificar-http-release-station.py')
    public = serialization.load_pem_private_key(
        Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), password=None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes().strip(), validate=True)
    database = operations.database_name(values)

    class TimedCheck(helper.StationReleaseVerification):
        def __init__(self):
            super().__init__(lambda query: operations.sql(database, query), pepper, public, index)
            self.samples = []
            self.sample_lock = threading.Lock()
            self.bearer = None

        def request(self, address, method, route, payload=None, bearer=None):
            start = time.perf_counter()
            response = super().request(address, method, route, payload, bearer)
            if route.startswith('/v1/station/covers/') or route.startswith('/v1/station/artifacts/'):
                sample = dict(operation='cover' if '/covers/' in route else 'artifact',
                              status=response[0], bytes=len(response[2]),
                              totalMs=round((time.perf_counter() - start) * 1000, 3))
                with self.sample_lock:
                    self.samples.append(sample)
            return response

        def signed(self, response, domain):
            data = super().signed(response, domain)
            if domain == 'session':
                self.bearer = data['accessToken']
            return data

    check = TimedCheck()
    report = dict(transport='public_https' if base.startswith('https://') else 'api_loopback',
                  probeLocation='Linux server, not Android device', customerLicenseUsed=False)
    try:
        check.create()
        report['contractVerification'] = check.check(base)
        rows = [r for r in index['items'] if r.get('catalogVisible') is not False]
        if burst_count:
            if burst_count < 31 or burst_count > 64:
                raise ValueError('cover burst must contain 31 to 64 requests')
            selected = [rows[i % len(rows)] for i in range(burst_count)]
            def cover(row):
                response = check.request(base, 'GET', '/v1/station/covers/' + row['coverId'], bearer=check.bearer)
                expected = Path(row['coverPath']).read_bytes()
                if response[0] != 200 or response[2] != expected or response[1].get('Cache-Control') != 'no-store':
                    raise ValueError('authenticated burst changed cover bytes/cache policy')
                return dict(bytes=len(expected), sha256=hashlib.sha256(expected).hexdigest(), exactIndexedBytes=True)
            start = time.perf_counter()
            with ThreadPoolExecutor(max_workers=4) as workers:
                images = list(workers.map(cover, selected))
            report['coverBurst'] = dict(requests=len(images), concurrency=4, statuses=[200],
                totalMs=round((time.perf_counter() - start) * 1000, 3),
                bytes=sum(r['bytes'] for r in images), allIndexedBytesMatch=True)

        # Time meaningful payloads, not only the smallest compatibility fixtures.
        downloads = []
        for platform in sorted({r['platform'] for r in rows}):
            row = max((r for r in rows if r['platform'] == platform), key=lambda r: r['artifact']['sizeBytes'])
            if row['artifact']['sizeBytes'] > 64 * 1024 * 1024:
                raise ValueError('bounded download timing supports artifacts up to 64 MiB')
            grant = check.signed(check.request(base, 'POST', '/v1/station/downloads/authorize',
                dict(check.identity, domain=helper.PREFIX + 'request-download/v1', itemId=row['itemId']), check.bearer),
                'download-grant')
            if grant['artifact'] != row['artifact'] or grant['itemRevision'] != row['revision']:
                raise ValueError('download descriptor changed')
            start = time.perf_counter()
            response = check.request(base, 'GET', '/v1/station/artifacts/' + grant['grantId'], bearer=check.bearer)
            elapsed = time.perf_counter() - start
            if response[0] != 200 or len(response[2]) != row['artifact']['sizeBytes'] or \
                    hashlib.sha256(response[2]).hexdigest() != row['artifact']['sha256'] or \
                    int(response[1].get('Content-Length', '-1')) != len(response[2]) or \
                    response[1].get('Cache-Control') != 'no-store':
                raise ValueError('timed download status/bytes/hash/header mismatch')
            downloads.append(dict(platform=platform, bytes=len(response[2]), sha256=row['artifact']['sha256'],
                                  totalMs=round(elapsed * 1000, 3),
                                  megabitsPerSecond=round(len(response[2]) * 8 / elapsed / 1_000_000, 3)))
            repeated = check.request(base, 'GET', '/v1/station/artifacts/' + grant['grantId'], bearer=check.bearer)
            if repeated[0] != 404:
                raise ValueError('timed download grant was reusable')
        report['largestPlatformDownloads'] = downloads
        anonymous = check.request(base, 'GET', '/v1/station/covers/' + rows[0]['coverId'])
        if anonymous[0] != 401:
            raise ValueError('anonymous cover was not denied')
        report['anonymousCoverStatus'] = 401
        report['samples'] = check.samples
        for operation in ['cover', 'artifact']:
            times = [r['totalMs'] for r in check.samples if r['operation'] == operation and r['status'] == 200]
            report[operation + 'MedianMs'] = statistics.median(times)
    finally:
        if not check.cleanup():
            raise ValueError('synthetic transfer fixture was not removed')
    report['syntheticRowsRemoved'] = True
    report['syntheticDeliveryRowsRemaining'] = int(operations.sql(database,
        "SELECT count(*) FROM suite.suite_license_deliveries WHERE source_system='STATION_ROLLOUT_TEST'"))
    return report
