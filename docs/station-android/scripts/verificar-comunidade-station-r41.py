#!/usr/bin/env python3
"""R41 HTTP integration with three disposable, independently activated licenses.

The caller supplies an isolated database or explicitly authorized production
credentials in memory. Returned evidence excludes activation codes, bearers,
license/device/session IDs, relay tickets, room passwords and private paths.
"""
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[3]


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def verify(index, values, base, enabled=True, relay_probe=False):
    from cryptography.hazmat.primitives import serialization
    ops = load('implantar-station-20261003.py')
    public = serialization.load_pem_private_key(
        Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), password=None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_text().strip(), validate=True)
    db = ops.database_name(values)
    return verify_credentials(index, lambda q: ops.sql(db, q), pepper, public, base, enabled, relay_probe)


def verify_credentials(index, execute_sql, pepper, public, base, enabled=True, relay_probe=False):
    helper = load('verificar-http-release-station.py')
    clients = [helper.StationReleaseVerification(execute_sql, pepper, public, index) for _ in range(3)]
    sessions, evidence, sockets = {}, [], []
    checks = 0
    excerpts = {}

    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            raise ValueError('Station R41 verification failed: ' + label)

    def authenticate(client):
        client.create()
        challenge = client.signed(client.request(base, 'POST', '/v1/station/activations/challenge', dict(
            client.identity, domain=helper.PREFIX + 'request-activation-challenge/v1',
            activationCode=client.code, devicePublicKey=client.spki)), 'activation-challenge')
        client.signed(client.request(base, 'POST', '/v1/station/activations/complete', client.proof(dict(
            client.identity, domain=helper.PREFIX + 'activate/v1', activationCode=client.code,
            devicePublicKey=client.spki, challengeId=challenge['challengeId'], nonce=challenge['nonce']))), 'activated')
        challenge = client.signed(client.request(base, 'POST', '/v1/station/challenges', dict(
            client.identity, domain=helper.PREFIX + 'request-session-challenge/v1',
            licenseId=client.license_id)), 'session-challenge')
        session = client.signed(client.request(base, 'POST', '/v1/station/sessions', client.proof(dict(
            client.identity, domain=helper.PREFIX + 'open-session/v1', licenseId=client.license_id,
            challengeId=challenge['challengeId'], nonce=challenge['nonce']))), 'session')
        sessions[client.license_id] = session

    def body(action, **extra):
        return dict(action=action, requestId=str(uuid.uuid4()), **extra)

    def request(client, command, route='command', bearer=True):
        response = client.request(base, 'POST', '/v1/station/online/' + route, command,
            sessions[client.license_id]['accessToken'] if bearer else None)
        evidence.append(dict(action=command.get('action', 'events'), status=response[0],
            requestId=command['requestId'], correlationId=response[1].get('X-Correlation-ID')))
        return response

    def call(client, command, route='command'):
        response = request(client, command, route)
        code = json.loads(response[2]).get('code', '') if response[0] != 200 else ''
        # Codes/status only, never a body or credential in an exception.
        check(response[0] == 200, 'authenticated ' + command.get('action', 'events') +
              ' status=' + str(response[0]) + ' code=' + code)
        check(response[1].get('Cache-Control') == 'no-store' and
              response[1].get('X-Content-Type-Options') == 'nosniff', 'private headers')
        check(len(response[2]) <= 524288, 'Android envelope bound')
        payload = client.signed(response, 'online')
        check(payload['sessionId'] == sessions[client.license_id]['sessionId'] and
              payload['requestId'] == command['requestId'], 'signed session/request binding')
        return payload['snapshot']

    def command(client, action, **extra):
        return call(client, body(action, **extra))

    def denied(client, command, status, code, bearer=True):
        response = request(client, command, bearer=bearer)
        check(response[0] == status and json.loads(response[2])['code'] == code, code)

    def excerpt(state):
        # Only ephemeral public IDs and synthetic texts; no room or auth secret.
        return {field: state[field] for field in ('schemaVersion', 'socialCapabilities',
            'directMessages', 'joinRequests', 'sentJoinRequests')}

    completed = False
    relay_bytes = 0
    try:
        for client in clients:
            authenticate(client)
        host, guest, outsider = clients
        h = command(host, 'enter', nickname='R41 synthetic host')
        g = command(guest, 'enter', nickname='R41 synthetic guest')
        c = command(outsider, 'enter', nickname='R41 synthetic outsider')
        hid, gid = h['selfId'], g['selfId']
        check(len({hid, gid, c['selfId']}) == 3, 'independent authenticated peers')
        for state in (h, g, c):
            check(state['socialCapabilities'] == (['direct-chat-v1', 'join-request-v1'] if enabled else []),
                  'effective SocialEnabled through DI')
        denied(host, body('heartbeat'), 401, 'STATION_SESSION_INVALID', bearer=False)
        engine = next(e for e in h['engines'] if e['platform'] == 'snes')
        row = next(r for r in index['items'] if r['platform'] == 'snes' and r.get('catalogVisible', True))
        hashes = dict(contentSha256=row['artifact']['sha256'], optionsSha256='a' * 64,
                      coreSha256=engine['coreSha256'], runtimeSha256=engine['runtimeSha256'])
        create = dict(itemId=row['itemId'], engineId=engine['engineId'], **hashes)
        rid = command(host, 'create', **create)['room']['roomId']
        own = command(guest, 'create', **create)['room']['roomId']
        if not enabled:
            for action, extra in (('direct-chat', dict(peerId=gid, text='disabled')),
                                  ('request-join', dict(roomId=rid)),
                                  ('accept-request', dict(text='0' * 32)),
                                  ('dismiss-request', dict(text='0' * 32))):
                denied(host, body(action, **extra), 503, 'STATION_ONLINE_SOCIAL_DISABLED')
            excerpts['flagOff'] = excerpt(command(host, 'heartbeat'))
        else:
            dm = body('direct-chat', peerId=gid, text='Olá! Teste privado R41 — duas licenças.')
            command_snapshot = call(host, dm)
            received = command(guest, 'heartbeat')
            check(received['directMessages'] == command_snapshot['directMessages'] and
                  len(received['directMessages']) == 1, 'sender/recipient same message')
            message = received['directMessages'][0]
            check(set(message) == {'messageId', 'fromPeerId', 'toPeerId', 'nickname', 'text', 'utc'} and
                  message['fromPeerId'] == hid and message['toPeerId'] == gid and
                  message['nickname'] == 'R41 synthetic host' and message['text'] == dm['text'],
                  'server-assigned author, target and text')
            excerpts['privateConversation'] = excerpt(received)
            check(not command(outsider, 'heartbeat')['directMessages'], 'third peer cannot read direct message')
            call(host, dm)
            check(len(command(guest, 'heartbeat')['directMessages']) == 1, 'idempotent retry')
            denied(host, dict(dm, text='different'), 409, 'STATION_ONLINE_REQUEST_REUSED')
            if not base.startswith('https:'):
                # Public proxy latency may exceed the one-second rate window.
                denied(host, body('direct-chat', peerId=gid, text='fast'), 429, 'STATION_ONLINE_CHAT_LIMIT')
            denied(host, body('direct-chat', peerId=hid, text='self'), 404, 'STATION_ONLINE_PEER_NOT_FOUND')
            denied(host, body('direct-chat', peerId=gid, text='x' * 501), 400, 'STATION_ONLINE_TEXT_INVALID')
            denied(host, body('direct-chat', peerId=gid, text='control\ntext'), 400, 'STATION_ONLINE_TEXT_INVALID')
            command(outsider, 'request-join', roomId=rid)
            dismiss = command(host, 'heartbeat')['joinRequests'][0]['requestId']
            denied(guest, body('dismiss-request', text=dismiss), 404, 'STATION_ONLINE_ROOM_NOT_FOUND')
            command(host, 'dismiss-request', text=dismiss)
            check(not command(outsider, 'heartbeat')['sentJoinRequests'], 'dismissal clears sender request')
            sent = command(guest, 'request-join', roomId=rid)
            pending = command(host, 'heartbeat')
            request_id = pending['joinRequests'][0]['requestId']
            check(len(request_id) == 32 and sent['room']['roomId'] == own and
                  pending['room']['members'] == [hid], 'request distinct from action UUID; no implicit join')
            excerpts['hostPendingRequest'] = excerpt(pending)
            excerpts['guestSentRequest'] = excerpt(sent)
            check(not command(outsider, 'heartbeat')['joinRequests'], 'only actual host sees request')
            denied(guest, body('request-join', roomId=rid), 409, 'STATION_ONLINE_REQUEST_EXISTS')
            denied(outsider, body('accept-request', text=request_id), 404, 'STATION_ONLINE_ROOM_NOT_FOUND')
            command(host, 'accept-request', text=request_id)
            invited = command(guest, 'heartbeat')
            check(any(i['roomId'] == rid and i['itemId'] == row['itemId'] for i in invited['invites']),
                  'approval emits existing invitation')
        denied(guest, body('join', roomId=rid, **hashes), 409, 'STATION_ONLINE_ALREADY_IN_ROOM')
        command(guest, 'leave')
        wrong = dict(hashes, contentSha256='0' * 64)
        denied(guest, body('join', roomId=rid, **wrong), 409, 'STATION_ONLINE_BUILD_MISMATCH')
        joined = command(guest, 'join', roomId=rid, **hashes)
        check(joined['room']['members'] == [hid, gid] and not joined['sentJoinRequests'] and
              not any(i['roomId'] == rid for i in joined['invites']), 'explicit leave/join consumes request/invite')
        command(host, 'ready', roomId=rid, value=True)
        command(guest, 'ready', roomId=rid, value=True)
        denied(guest, body('start', roomId=rid, transport='relay-wss-v1'), 403, 'STATION_ONLINE_HOST_REQUIRED')
        check(command(host, 'start', roomId=rid, transport='relay-wss-v1')['room']['state'] == 'starting',
              'guest held until host listening')
        if relay_probe:
            relay = load('verificar-relay-station.py')
            uri = base.replace('https:', 'wss:').replace('http:', 'ws:') + '/v1/station/online/relay'

            def attach(client):
                ticket = command(client, 'relay-ticket', roomId=rid)['room']['relay']['ticket']
                stream = relay.connect(uri, subprotocols=['station-relay.v1'],
                    additional_headers={'Authorization': 'StationRelay ' + ticket}, compression=None,
                    user_agent_header='Dalvik/2.1.0 Station operator community check',
                    proxy=None, open_timeout=15, close_timeout=2, max_size=65536, max_queue=8)
                sockets.append(stream)
                if base.startswith('https:'):
                    certificate = relay.x509.load_der_x509_certificate(stream.socket.getpeercert(binary_form=True))
                    pin = hashlib.sha256(certificate.public_key().public_bytes(
                        relay.serialization.Encoding.DER, relay.serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
                    check(pin == relay.PIN, 'unchanged public TLS pin')
                check(stream.subprotocol == 'station-relay.v1', 'actual WS subprotocol')
                return stream

            hs = attach(host)
        check(command(host, 'host-listening', roomId=rid)['room']['state'] == 'connecting',
              'host signal releases guest')
        if relay_probe:
            gs = attach(guest)
            payload = bytes(range(256)) * 256 + b'R41'

            def transfer(sender, receiver):
                def send():
                    for start in range(0, len(payload), 16384):
                        sender.send(payload[start:start + 16384])
                with ThreadPoolExecutor(max_workers=1) as pool:
                    pending = pool.submit(send)
                    received = bytearray()
                    while len(received) < len(payload):
                        received.extend(receiver.recv(timeout=15))
                    pending.result(timeout=15)
                check(bytes(received) == payload, 'approved-request real relay bytes')
            transfer(hs, gs)
            transfer(gs, hs)
            relay_bytes = len(payload)
        command(host, 'leave')
        check(command(guest, 'heartbeat')['room'] is None, 'host exit releases second player')
        if relay_probe:
            for stream in (hs, gs):
                try:
                    stream.recv(timeout=6)
                except relay.ConnectionClosed:
                    check(True, 'room exit closes public socket')
                else:
                    raise ValueError('Station R41 relay survived room exit')
        if enabled:
            command(guest, 'block', peerId=hid)
            check(not command(host, 'heartbeat')['directMessages'] and
                  not command(guest, 'heartbeat')['directMessages'], 'block removes both histories')
            denied(host, body('direct-chat', peerId=gid, text='blocked'), 403, 'STATION_ONLINE_BLOCKED')
        cursor = command(outsider, 'heartbeat')
        event = dict(requestId=str(uuid.uuid4()), instance=cursor['instance'], revision=cursor['revision'] - 1, page=0)
        check(call(outsider, event, 'events')['instance'] == cursor['instance'], 'same signed events contract')
        command(host, 'offline')
        check(command(host, 'enter', nickname='R41 reconnected')['selfId'] != hid,
              'reconnect uses fresh ephemeral peer')
        completed = True
    finally:
        for stream in sockets:
            stream.close()
        for client in clients:
            if client.license_id in sessions:
                try:
                    request(client, body('offline'))
                except Exception:
                    pass
            check(client.cleanup(), 'disposable fixture cleanup')
    return dict(passed=completed, checks=checks, socialEnabled=enabled,
        threeIndependentLicenses=True, signedSessionRequestBinding=True,
        actualApprovedJoinRelayBytesEachDirection=relay_bytes,
        publicTlsPinVerified=relay_probe and base.startswith('https:'),
        snapshotExcerpts=excerpts, httpEvidence=evidence,
        syntheticRowsRemoved=True, pocoActivationConsumedByProbe=False, androidGameplay=False)
