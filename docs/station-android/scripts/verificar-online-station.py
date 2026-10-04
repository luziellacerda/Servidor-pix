"""Bounded authenticated room checks using disposable synthetic licenses only."""
import base64
import importlib.util
import json
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def verify(index, values, base):
    root = Path(__file__).resolve().parents[3]
    scripts = Path(__file__).parent
    ops = load(scripts / 'implantar-station-20261003.py', 'online_ops')
    helper = load(scripts / 'verificar-http-release-station.py', 'online_helper')
    checks = load(root / 'tests/TurboRamaSuiteOnlineServer.Tests/station_online_http_checks.py', 'online_checks')
    public = serialization.load_pem_private_key(
        Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), password=None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes().strip(), validate=True)
    database = ops.database_name(values)

    class Client(helper.StationReleaseVerification):
        session = None
        def signed(self, response, domain):
            data = super().signed(response, domain)
            if domain == 'session': self.session = data
            return data

    primary = Client(lambda query: ops.sql(database, query), pepper, public, index)
    def signed(response, server_public, domain):
        if response[0] != 200: raise ValueError('online signed endpoint failed')
        envelope = json.loads(response[2])
        payload = base64.urlsafe_b64decode(envelope['payload'] + '===')
        signature = base64.urlsafe_b64decode(envelope['signature'] + '===')
        public.verify(signature, payload, padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
        data = json.loads(payload)
        if data['domain'] != helper.PREFIX + domain + '/v1' or data['productId'] != helper.PRODUCT or \
                data['applicationId'] != helper.PRODUCT or data['schemaVersion'] != 1:
            raise ValueError('online authority context changed')
        return data
    try:
        primary.create()
        contract = primary.check(base)
        report = checks.run(base, root, index, pepper, public, primary.execute_sql,
            primary.identity, primary.session, primary.request, signed, exclusive=False)
        report['existingStationContract'] = contract
        report['transport'] = 'public_https' if base.startswith('https://') else 'api_loopback'
    finally:
        if not primary.cleanup(): raise ValueError('synthetic primary cleanup failed')
    report['syntheticRowsRemoved'] = True
    return report
