"""Authenticated relay checks with disposable licenses; never activate POCO.

Test dependency: websockets==15.0.1 (sync client), in an isolated operator venv.
TLS uses the system trust store, hostname and the unchanged Station SPKI pin.
No code, token, ticket, private path or signed envelope is returned in reports.
"""
import base64
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import time
import uuid

from cryptography import x509
from cryptography.hazmat.primitives import serialization
from websockets.sync.client import connect
from websockets.exceptions import ConnectionClosed, InvalidStatus

PIN = '13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7'
ROOT = Path(__file__).resolve().parents[3]


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def verify(index, values, base, enabled=True):
    ops = load('implantar-station-20261003.py')
    helper = load('verificar-http-release-station.py')
    public = serialization.load_pem_private_key(
        Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), password=None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_text().strip(), validate=True)
    db = ops.database_name(values)
    clients = [helper.StationReleaseVerification(lambda q: ops.sql(db, q), pepper, public, index) for _ in range(3)]
    sockets = []
    sessions = {}
    checks = 0
    completed = False

    def check(ok, name):
        nonlocal checks
        checks += 1
        if not ok:
            raise ValueError('Station relay verification failed: ' + name)

    def authenticate(client):
        client.create()
        payload = dict(client.identity, domain=helper.PREFIX + 'request-activation-challenge/v1',
                       activationCode=client.code, devicePublicKey=client.spki)
        challenge = client.signed(client.request(base, 'POST', '/v1/station/activations/challenge', payload), 'activation-challenge')
        result = client.signed(client.request(base, 'POST', '/v1/station/activations/complete', client.proof(dict(
            client.identity, domain=helper.PREFIX + 'activate/v1', activationCode=client.code,
            devicePublicKey=client.spki, challengeId=challenge['challengeId'], nonce=challenge['nonce']))), 'activated')
        check(result['challengeId'] == challenge['challengeId'] and result['nonce'] == challenge['nonce'], 'activation binding')
        client.activated = True
        challenge = client.signed(client.request(base, 'POST', '/v1/station/challenges', dict(
            client.identity, domain=helper.PREFIX + 'request-session-challenge/v1', licenseId=client.license_id)), 'session-challenge')
        session = client.signed(client.request(base, 'POST', '/v1/station/sessions', client.proof(dict(
            client.identity, domain=helper.PREFIX + 'open-session/v1', licenseId=client.license_id,
            challengeId=challenge['challengeId'], nonce=challenge['nonce']))), 'session')
        sessions[client.license_id] = session
        profile = client.signed(client.request(base, 'GET', '/v1/station/me', bearer=session['accessToken']), 'profile')
        check(profile['sessionId'] == session['sessionId'], 'profile session')

    def request(client, method, route, payload=None):
        return client.request(base, method, route, payload, sessions[client.license_id]['accessToken'])

    def command(client, action, **extra):
        body = dict(action=action, requestId=str(uuid.uuid4()), **extra)
        result = client.signed(request(client, 'POST', '/v1/station/online/command', body), 'online')
        check(result['requestId'] == body['requestId'] and result['sessionId'] == sessions[client.license_id]['sessionId'], 'online request binding')
        return result['snapshot']

    def denied(client, action, status, code, **extra):
        response = request(client, 'POST', '/v1/station/online/command', dict(action=action, requestId=str(uuid.uuid4()), **extra))
        check(response[0] == status and json.loads(response[2])['code'] == code, code)

    uri = base.replace('https:', 'wss:').replace('http:', 'ws:') + '/v1/station/online/relay'

    def websocket(ticket, path=None, protocols=None):
        connection = connect(path or uri, subprotocols=protocols or ['station-relay.v1'],
            additional_headers={'Authorization': 'StationRelay ' + ticket}, compression=None,
            user_agent_header='Dalvik/2.1.0 Station operator relay check', proxy=None,
            open_timeout=15, close_timeout=2, max_size=65536, max_queue=8)
        sockets.append(connection)
        if uri.startswith('wss:'):
            certificate = x509.load_der_x509_certificate(connection.socket.getpeercert(binary_form=True))
            pin = hashlib.sha256(certificate.public_key().public_bytes(
                serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
            check(pin == PIN, 'public WSS TLS pin')
        check(connection.subprotocol == 'station-relay.v1', 'WS subprotocol')
        return connection

    def rejected(ticket, status=401, **extra):
        try:
            websocket(ticket, **extra)
        except InvalidStatus as failure:
            check(failure.response.status_code == status, 'rejected WS upgrade')
        else:
            raise ValueError('Unexpected accepted WS upgrade')

    try:
        for client in clients:
            authenticate(client)
        host, guest, outsider = clients
        visible = [row for row in index['items'] if row.get('catalogVisible', True)]
        catalog = host.signed(request(host, 'GET', '/v1/station/catalog?metadata=1'), 'catalog')
        expected = {row['itemId']: row for row in visible}
        published = {row['itemId']: row for row in catalog['items']}
        check(catalog['revision'] == index['revision'] and set(published) == set(expected), 'complete catalog and revision')
        check(len(catalog['items']) == len(expected), 'no duplicate catalog items')
        for key, row in expected.items():
            check(all(published[key].get(field) == row.get(field) for field in ('name', 'platform', 'coverId', 'metadata'))
                  and published[key].get('folderPath', []) == row.get('folderPath', []), 'catalog metadata and folders')
        check('filePath' not in json.dumps(catalog) and 'coverPath' not in json.dumps(catalog), 'private paths absent')
        sample = min((row for row in visible if row['platform'] == 'snes'), key=lambda row: row['artifact']['sizeBytes'])
        covers = visible[:4]
        with ThreadPoolExecutor(max_workers=4) as pool:
            responses = list(pool.map(lambda row: request(host, 'GET', '/v1/station/covers/' + row['coverId']), covers))
        check(all(response[0] == 200 and response[2] == Path(row['coverPath']).read_bytes()
                  for row, response in zip(covers, responses)), 'four concurrent exact covers')
        grant = host.signed(request(host, 'POST', '/v1/station/downloads/authorize', dict(
            host.identity, domain=helper.PREFIX + 'request-download/v1', itemId=sample['itemId'])), 'download-grant')
        check(grant['artifact'] == sample['artifact'], 'download descriptor preserved')
        artifact = request(host, 'GET', '/v1/station/artifacts/' + grant['grantId'])
        check(artifact[0] == 200 and len(artifact[2]) == sample['artifact']['sizeBytes'] and
              hashlib.sha256(artifact[2]).hexdigest() == sample['artifact']['sha256'], 'bounded actual game transfer')
        check(request(host, 'GET', '/v1/station/artifacts/' + grant['grantId'])[0] == 404, 'grant remains single use')
        for client, nickname in zip(clients, ('Relay check host', 'Relay check guest', 'Relay check outsider')):
            state = command(client, 'enter', nickname=nickname)
            check(('relay-wss-v1' in state['transports']) == enabled, 'effective transport flag')
        engine = next(row for row in state['engines'] if row['platform'] == 'snes')
        descriptor = dict(itemId=sample['itemId'], engineId=engine['engineId'],
            contentSha256=sample['artifact']['sha256'], optionsSha256='a' * 64,
            coreSha256=engine['coreSha256'], runtimeSha256=engine['runtimeSha256'])
        state = command(host, 'create', **descriptor)
        room = state['room']['roomId']
        hashes = {key: value for key, value in descriptor.items() if key.endswith('Sha256')}
        command(guest, 'join', roomId=room, **hashes)
        command(host, 'ready', roomId=room, value=True)
        command(guest, 'ready', roomId=room, value=True)
        if not enabled:
            denied(host, 'start', 503, 'STATION_ONLINE_RELAY_DISABLED', roomId=room, transport='relay-wss-v1')
            rejected('z' * 43, 503)
            command(host, 'leave')
        else:
            state = command(host, 'start', roomId=room, transport='relay-wss-v1')
            check(state['room']['directEndpoint'] is None, 'no player IP')
            generation = state['room']['generation']
            denied(outsider, 'relay-ticket', 404, 'STATION_ONLINE_ROOM_NOT_FOUND', roomId=room)
            old = command(host, 'relay-ticket', roomId=room)['room']['relay']['ticket']
            ticket = command(host, 'relay-ticket', roomId=room)['room']['relay']['ticket']
            rejected(old)
            rejected('z' * 43)
            rejected(ticket, 400, path=uri + '?ticket=unused')
            rejected(ticket, 400, protocols=['invalid.protocol'])
            h = websocket(ticket)
            rejected(ticket)
            state = command(host, 'host-listening', roomId=room)
            check(state['room']['generation'] == generation, 'generation retained')
            state = command(guest, 'heartbeat')
            check(ticket not in json.dumps(state), 'host ticket private')
            guest_ticket = command(guest, 'relay-ticket', roomId=room)['room']['relay']['ticket']
            g = websocket(guest_ticket)
            payload = os.urandom(2 * 1024 * 1024 + 117)

            def send(stream):
                for start in range(0, len(payload), 16384):
                    stream.send(payload[start:start + 16384])

            def receive(stream):
                chunks = bytearray()
                while len(chunks) < len(payload):
                    data = stream.recv(timeout=20)
                    check(isinstance(data, bytes), 'binary relay message')
                    chunks.extend(data)
                return bytes(chunks)

            with ThreadPoolExecutor(max_workers=2) as pool:
                sent = pool.submit(send, h)
                actual = receive(g)
                sent.result(timeout=20)
                check(actual == payload, 'all forward bytes')
                sent = pool.submit(send, g)
                actual = receive(h)
                sent.result(timeout=20)
                check(actual == payload, 'all reverse bytes')
            for value in range(20):
                g.send(bytes([value]))
                check(h.recv(timeout=10) == bytes([value]), 'ordered input packet')
            command(host, 'leave')
            for stream in (h, g):
                try:
                    stream.recv(timeout=6)
                except ConnectionClosed:
                    check(True, 'room departure closes socket')
                else:
                    raise ValueError('Relay survived room departure')
            check(command(guest, 'heartbeat')['room'] is None, 'guest released after host exit')
            # A pending authenticated poll must revalidate after revocation and
            # remove its hub identity. A database write alone expires presence
            # within 60 seconds; it does not promise an immediate socket close.
            time.sleep(10.05)  # create rate is one per ten seconds
            state = command(host, 'create', **descriptor)
            room = state['room']['roomId']
            command(guest, 'join', roomId=room, **hashes)
            command(host, 'ready', roomId=room, value=True)
            command(guest, 'ready', roomId=room, value=True)
            command(host, 'start', roomId=room, transport='relay-wss-v1')
            ht = command(host, 'relay-ticket', roomId=room)['room']['relay']['ticket']
            h2 = websocket(ht)
            command(host, 'host-listening', roomId=room)
            gt = command(guest, 'relay-ticket', roomId=room)['room']['relay']['ticket']
            g2 = websocket(gt)
            denied(outsider, 'relay-ticket', 404, 'STATION_ONLINE_ROOM_NOT_FOUND', roomId=room)
            cursor = command(host, 'heartbeat')
            with ThreadPoolExecutor(max_workers=1) as pool:
                poll = pool.submit(request, host, 'POST', '/v1/station/online/events',
                    dict(requestId=str(uuid.uuid4()), instance=cursor['instance'], revision=cursor['revision'], page=0))
                time.sleep(0.3)
                ops.sql(db, "UPDATE suite.suite_licenses SET status='REVOKED',revocation_generation=revocation_generation+1 WHERE license_id='" + host.license_id + "';")
                response = poll.result(timeout=15)
                check(response[0] == 401, 'revoked session revalidated after poll')
            for stream in (h2, g2):
                try:
                    stream.recv(timeout=6)
                except ConnectionClosed:
                    check(True, 'revoked lease closed')
                else:
                    raise ValueError('Relay survived authenticated revocation')
            check(command(guest, 'heartbeat')['room'] is None, 'revoked room removed')
        completed = True
    finally:
        for stream in sockets:
            stream.close()
        for client in clients:
            if client.license_id in sessions:
                try:
                    request(client, 'POST', '/v1/station/online/command', dict(action='offline', requestId=str(uuid.uuid4())))
                except Exception:
                    pass
            if not client.cleanup():
                raise ValueError('Synthetic Station cleanup failed')
    return dict(passed=completed, checks=checks, transport='public_wss' if base.startswith('https:') else 'loopback_ws',
        relayEnabled=enabled, catalogRevision=index['revision'], catalogItems=len(visible),
        platformCounts=dict(Counter(row['platform'] for row in visible)), exactCatalogMetadata=True,
        simultaneousCovers=4, boundedDownloadBytes=sample['artifact']['sizeBytes'],
        bytesEachDirection=2097269 if enabled else 0, orderedInputPackets=20 if enabled else 0,
        signedSessionRequestBinding=True, publicTlsPinVerified=base.startswith('https:') and enabled,
        ownRoomsClosed=True, syntheticRowsRemoved=True, pocoActivationConsumedByProbe=False, androidGameplay=False)
