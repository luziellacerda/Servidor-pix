"""Signed v3 release checks with owned synthetic devices; no Android gameplay claim."""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import secrets
import struct
import time
import uuid
from urllib.error import HTTPError
from urllib.request import Request
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from websockets.exceptions import InvalidStatus
from station_async_socket_check import SocketChecks


def verify(index, values, base, sql):
    spec = importlib.util.spec_from_file_location('station_v3_owned', Path(__file__).with_name('verificar-http-release-station.py'))
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    public = serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes(), validate=True)
    profiles = json.loads(Path(values['Station__Online__MultiplayerProfileRegistryFile']).read_text())
    selected = [next(p for p in profiles if p['itemId'] == '826da6daebe9edbebffb3721f83abf12'),
                next(p for p in profiles if p['platform'] == 'megadrive'),
                next(p for p in profiles if p['itemId'] == 'station_df50d575815ab105084a79d68e0c8fb3' and p['maximumPlayers'] == 4),
                *[p for p in profiles if p['maximumPlayers'] == 5]]
    path = '/v1/station/online/multiplayer/command'
    relay = '/v1/station/online/multiplayer/relay'
    clients, sessions, rooms = [], {}, []
    adapter = SocketChecks()
    checks = 0
    forwarded = 0

    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            raise ValueError('R81 v3: '+label)

    def proof(client, credential, method, route, body=b''):
        stamp = int(time.time())
        nonce = helper.b64(secrets.token_bytes(16))
        canonical = ('TurboRamaStationAndroid/request/v1\n'+method+'\n'+route+'\n'+hashlib.sha256(body).hexdigest()+'\n'+hashlib.sha256(credential.encode()).hexdigest()+'\n'+str(stamp)+'\n'+nonce+'\n').encode()
        signature = client.device.sign(canonical, padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
        return 'v1.'+str(stamp)+'.'+nonce+'.'+helper.b64(signature)

    def request(client, route, payload, authenticated=True):
        body = json.dumps(payload, separators=(',', ':')).encode()
        headers = {'Content-Type': 'application/json', 'User-Agent': 'Dalvik/2.1.0 Station R81 release verification'}
        if authenticated:
            token = sessions[id(client)]['accessToken']
            headers.update(Authorization='Bearer '+token)
            headers['X-Station-Request-Proof'] = proof(client, token, 'POST', route, body)
        try:
            response = client.opener.open(Request(base+route, body, headers, method='POST'), timeout=20)
        except HTTPError as error:
            response = error
        with response:
            data = response.read(2*1024*1024+1)
            check(response.url == base+route and len(data) <= 2*1024*1024, 'HTTP bounds and redirect')
            return response.code, response.headers, data

    def signed(client, response, domain):
        if response[0] != 200:
            code = json.loads(response[2]).get('code', 'UNKNOWN')
            if not isinstance(code, str) or len(code) > 96 or not all(c in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789' for c in code):
                code = 'SUPPRESSED'
            raise ValueError('R81 v3 HTTP'+str(response[0])+' '+code)
        result = client.signed(response, domain)
        check(True, 'signed response identity')
        return result

    def command(client, action, profile=None, room=None, route=path, **extra):
        payload = dict(action=action, requestId=str(uuid.uuid4()), **extra)
        if route == path:
            payload['clientMaximumPlayers'] = 5
        if profile:
            payload.update({k: profile[k] for k in ('itemId', 'contentSha256', 'engineId', 'coreSha256', 'runtimeSha256', 'profileId', 'profileSha256')})
        if room:
            payload.update(roomId=room['roomId'], generation=room['generation'])
        result = signed(client, request(client, route, payload), 'online')
        check(result['requestId'] == payload['requestId'] and result['sessionId'] == sessions[id(client)]['sessionId'], 'HTTP room/session binding')
        return result['snapshot']

    def frame(kind, offset=0, value=0, data=b''):
        return b'TSR3'+bytes([kind, 0, 0, 0])+struct.pack('>qq', offset, value)+data

    def receive(ws, kind, offset=None, value=None):
        deadline = time.monotonic()+15
        while time.monotonic() < deadline:
            data = ws.recv(timeout=max(.05, deadline-time.monotonic()))
            check(isinstance(data, bytes) and len(data) >= 24 and data[:4] == b'TSR3', 'TSR3 binary frame')
            at, number = struct.unpack('>qq', data[8:24])
            if data[4] == kind and (offset is None or at == offset) and (value is None or number == value):
                return at, number, data[24:]
        raise ValueError('R81 v3 receive deadline')

    def socket(client, room, link, action='ticket'):
        ticket = command(client, action, profile=room, room=room, linkId=link)['ticket']['ticket']
        headers = {'Authorization': 'StationRelay '+ticket, 'X-Station-Request-Proof': proof(client, ticket, 'GET', relay)}
        ws = adapter.connect(base.replace('https:', 'wss:').replace('http:', 'ws:')+relay,
            subprotocols=['station-stream.v3'], additional_headers=headers, compression=None, proxy=None,
            open_timeout=15, close_timeout=1, max_size=65536, max_queue=32)
        check(ws.subprotocol == 'station-stream.v3', 'negotiated v3')
        if base.startswith('https:'):
            certificate = x509.load_der_x509_certificate(ws.socket.getpeercert(binary_form=True))
            pin = hashlib.sha256(certificate.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
            check(pin == '13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7', 'public TLS pin unchanged')
        return ws

    try:
        for number in range(5):
            client = helper.StationReleaseVerification(sql, pepper, public, index)
            clients.append(client)
            client.create()
            challenge = signed(client, request(client, '/v1/station/activations/challenge', dict(client.identity, domain=helper.PREFIX+'request-activation-challenge/v1', activationCode=client.code, devicePublicKey=client.spki), False), 'activation-challenge')
            signed(client, request(client, '/v1/station/activations/complete', client.proof(dict(client.identity, domain=helper.PREFIX+'activate/v1', activationCode=client.code, devicePublicKey=client.spki, challengeId=challenge['challengeId'], nonce=challenge['nonce'], requestProof='rsa-pss-v1')), False), 'activated')
            challenge = signed(client, request(client, '/v1/station/challenges', dict(client.identity, domain=helper.PREFIX+'request-session-challenge/v1', licenseId=client.license_id), False), 'session-challenge')
            sessions[id(client)] = signed(client, request(client, '/v1/station/sessions', client.proof(dict(client.identity, domain=helper.PREFIX+'open-session/v1', licenseId=client.license_id, challengeId=challenge['challengeId'], nonce=challenge['nonce'], requestProof='rsa-pss-v1')), False), 'session')
            command(client, 'enter', route='/v1/station/online/command', nickname='R81 check '+str(number))
        for profile in selected:
            count = profile['maximumPlayers']
            room = command(clients[0], 'create', profile, capacity=count)['room']
            rooms.append(room)
            for client in clients[1:count]:
                room = command(client, 'join', profile, room)['room']
            for client in clients[:count]:
                room = command(client, 'ready', room=room, value=True)['room']
            room = command(clients[0], 'start', room=room)['room']
            rooms[-1] = room
            check(room['state'] == 'starting' and len(room['links']) == count-1, 'ready/start exact roster')
            peers = []
            for position, link in enumerate(room['links']):
                peers.append(socket(clients[0], room, link['linkId']))
                peers.append(socket(clients[position+1], room, link['linkId']))
            for ws in peers:
                ws.send(frame(1)); ws.send(frame(7, 1)); ws.send(frame(8, 1))
            for ws in peers:
                receive(ws, 6, 1, 2)
            check(command(clients[0], 'heartbeat', room=room)['room']['state'] == 'playing', 'global playing barrier')
            for position in range(count-1):
                host, guest = peers[position*2:position*2+2]
                for sender, recipient in ((host, guest), (guest, host)):
                    data = secrets.token_bytes(64)
                    sender.send(frame(3, data=data))
                    check(receive(recipient, 3, 0)[2] == data, 'exact independent bidirectional stream')
                    recipient.send(frame(4, 64)); receive(sender, 5, 64, 64)
                    forwarded += len(data)
            if profile == selected[0] or count == 5:
                drop = (count-2)*2+1 if count == 5 else 1
                guest = (drop+1)//2
                peers[drop].close()
                epoch = receive(peers[0], 6, value=0)[0]
                check(epoch > 1, 'disconnect advances recovery epoch')
                peers[drop] = socket(clients[guest], room, room['links'][guest-1]['linkId'], 'resume')
                peers[drop].send(frame(1, 64, 64))
                for ws in peers:
                    ws.send(frame(7, epoch)); ws.send(frame(8, epoch, 64))
                for ws in peers:
                    receive(ws, 6, epoch, 2)
                check(command(clients[1], 'heartbeat', room=room)['room']['state'] == 'playing', 'authorized reconnect resumes globally')
            command(clients[0], 'leave', room=room)
            rooms.pop()
            for ws in peers:
                ws.close()
            time.sleep(1.1)
        return dict(passed=True, checks=checks, approvedProfiles=len(profiles), signedV3=True, createJoinReadyStart=True,
            bidirectionalBytes=forwarded, countsVerified=[2,4,5], snesAndMegaDrive=True, protectedResume=True,
            syntheticDevices=5, public=base.startswith('https:'), physicalAndroidGameplayQualified=False)
    finally:
        for room in rooms:
            try:
                current = command(clients[0], 'heartbeat')['room']
                if current and current['roomId'] == room['roomId']:
                    command(clients[0], 'leave', room=current)
            except Exception:
                pass
        adapter.close()
        for client in clients:
            try:
                if id(client) in sessions:
                    command(client, 'offline', route='/v1/station/online/command')
            except Exception:
                pass
        cleaned = True
        for client in clients:
            try:
                cleaned = client.cleanup() and cleaned
            except Exception:
                cleaned = False
        if not cleaned:
            raise ValueError('Owned R81 v3 fixture cleanup failed')
