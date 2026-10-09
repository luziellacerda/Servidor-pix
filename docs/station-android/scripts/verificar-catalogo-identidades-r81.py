"""Authenticated signed catalog and unapproved-profile checks using an owned fixture.

No real customer session/code, local game path or signed envelope is returned.
The caller supplies the already reviewed index and the private database executor.
"""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import secrets
import time
import uuid
from urllib.error import HTTPError
from urllib.request import Request
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding


def verify(index, values, base, sql, identities=True, multiplayer='disabled'):
    path = Path(__file__).with_name('verificar-http-release-station.py')
    spec = importlib.util.spec_from_file_location('station_owned_fixture_r81', path)
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    public = serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes(), validate=True)
    client = helper.StationReleaseVerification(sql, pepper, public, index)
    token = None
    checks = 0

    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            raise ValueError('R81 signed catalog: '+label)

    def request(method, route, body=None):
        if token is None:
            return client.request(base, method, route, body)
        encoded = b'' if body is None else json.dumps(body, separators=(',', ':')).encode()
        stamp = int(time.time())
        nonce = helper.b64(secrets.token_bytes(16))
        canonical = ('TurboRamaStationAndroid/request/v1\n'+method+'\n'+route+'\n'+hashlib.sha256(encoded).hexdigest()+'\n'+hashlib.sha256(token.encode()).hexdigest()+'\n'+str(stamp)+'\n'+nonce+'\n').encode()
        signature = client.device.sign(canonical, padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
        headers = {'Authorization': 'Bearer '+token, 'X-Station-Request-Proof': 'v1.'+str(stamp)+'.'+nonce+'.'+helper.b64(signature), 'User-Agent': 'Dalvik/2.1.0 Station R81 authorized qualification'}
        if body is not None:
            headers['Content-Type'] = 'application/json'
        try:
            response = client.opener.open(Request(base+route, encoded if body is not None else None, headers, method=method), timeout=15)
        except HTTPError as error:
            response = error
        with response:
            data = response.read(16*1024*1024+1)
            check(response.url == base+route and len(data) <= 16*1024*1024, 'redirect and bounds')
            return response.code, response.headers, data

    def signed(response, domain):
        if response[0] != 200:
            code = json.loads(response[2]).get('code', 'UNKNOWN')
            if not isinstance(code, str) or len(code) > 96 or any(c not in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789' for c in code):
                code = 'SUPPRESSED'
            raise ValueError('Signed '+domain+' HTTP'+str(response[0])+' '+code)
        result = client.signed(response, domain)
        check(True, 'server signature and identity')
        return result

    def command(action, route='/v1/station/online/command', **extra):
        return request('POST', route, dict(action=action, requestId=str(uuid.uuid4()), **extra))

    result = None
    try:
        client.create()
        challenge = signed(request('POST', '/v1/station/activations/challenge', dict(client.identity, domain=helper.PREFIX+'request-activation-challenge/v1', activationCode=client.code, devicePublicKey=client.spki)), 'activation-challenge')
        signed(request('POST', '/v1/station/activations/complete', client.proof(dict(client.identity, domain=helper.PREFIX+'activate/v1', activationCode=client.code, devicePublicKey=client.spki, challengeId=challenge['challengeId'], nonce=challenge['nonce'], requestProof='rsa-pss-v1'))), 'activated')
        challenge = signed(request('POST', '/v1/station/challenges', dict(client.identity, domain=helper.PREFIX+'request-session-challenge/v1', licenseId=client.license_id)), 'session-challenge')
        session = signed(request('POST', '/v1/station/sessions', client.proof(dict(client.identity, domain=helper.PREFIX+'open-session/v1', licenseId=client.license_id, challengeId=challenge['challengeId'], nonce=challenge['nonce'], requestProof='rsa-pss-v1'))), 'session')
        token = session['accessToken']
        expected = {r['itemId']: r for r in index['items'] if r.get('catalogVisible', True)}
        metadata_keys = ('description', 'developer', 'publisher', 'genre', 'players', 'releaseDate')
        for include_metadata in (False, True):
            route = '/v1/station/catalog'+('?metadata=1' if include_metadata else '')
            catalog = signed(request('GET', route), 'catalog')
            rows = {r['itemId']: r for r in catalog['items']}
            check(catalog['revision'] == index['revision'] and rows.keys() == expected.keys() and len(rows) == len(catalog['items']), 'catalog revision/count/IDs')
            for item_id, row in expected.items():
                received = rows[item_id]
                check(all(received[k] == row[k] for k in ('itemId', 'name', 'platform', 'revision', 'coverId')), 'item identity')
                check(received.get('contentSha256') == (row.get('contentSha256') if identities else None), 'exact content identity')
                check(('contentSha256' in received) == bool(identities and row.get('contentSha256')), 'unknown content not fabricated')
                if include_metadata:
                    check(received['metadata'] == {k: row.get('metadata', {}).get(k, '') for k in metadata_keys} and received['folderPath'] == row.get('folderPath', []), 'all metadata and folders')
                else:
                    check('metadata' not in received, 'metadata opt-in')
            check(all('filePath' not in r and 'coverPath' not in r for r in rows.values()), 'private paths absent')
        pilot_ids = ('826da6daebe9edbebffb3721f83abf12', 'ac79b8fc351cb0811a7e9b51a099ba9c', 'station_df50d575815ab105084a79d68e0c8fb3')
        signed(command('enter', nickname='R81 profile check'), 'online')
        capabilities = []
        if multiplayer != 'absent':
            route = '/v1/station/online/multiplayer/command'
            for item_id in pilot_ids:
                response = command('capabilities', route, itemId=item_id)
                if multiplayer == 'disabled':
                    value = json.loads(response[2])
                    check(response[0] == 503 and value.get('code') == 'STATION_MULTIPLAYER_DISABLED', 'honest v3 disabled response')
                    capabilities.append(dict(itemId=item_id, status=response[0], code=value['code'], signedProfileApproved=False))
                else:
                    value = signed(response, 'online')['snapshot']
                    check(value['multiplayerVersion'] == 3 and value['capability'] == 'station-multiplayer.v3', 'v3 capability version')
                    check(bool(value['profiles']) == (item_id in expected), 'compatibility alias not admitted')
                    if multiplayer == 'enabled':
                        active = json.loads(Path(values['Station__Online__MultiplayerProfileRegistryFile']).read_text())
                        def comparable(p):
                            return dict(dict(modeTitle=None, instructions=None, sources=None), **p)
                        check([comparable(p) for p in value['profiles']] == [comparable(p) for p in active if p['itemId'] == item_id and item_id in expected and p['maximumPlayers'] <= 4], 'exact compatible active profiles signed')
                        check(all(p['approved'] for p in value['profiles']), 'profiles released by operator')
                    else:
                        check(not any(p['approved'] for p in value['profiles']), 'unapproved profiles stay unapproved')
                    capabilities.append(dict(itemId=item_id, status=200, multiplayerVersion=3, capability=value['capability'], approvedProfiles=sum(bool(p['approved']) for p in value['profiles']), profiles=value['profiles']))
            if multiplayer == 'unapproved':
                row = next(r for r in index['items'] if r['itemId'] == pilot_ids[0])
                engine = next(e for e in json.loads(Path(values['Station__Online__EngineRegistryFile']).read_text()) if e['id'].endswith('rs4-804b2acfea4c') and e['platform'] == 'snes')
                response = command('create', itemId=row['itemId'], contentSha256=row['contentSha256'], engineId=engine['id'], coreSha256=engine['coreSha256'], runtimeSha256=engine['runtimeSha256'], optionsSha256='945bb950e94c95eed556387c92f0a1b18ae186f36061b459d76f6a634d4590ec', recoveryProtocol='station-stream.v2')
                value = json.loads(response[2])
                check(response[0] == 409 and value.get('code') == 'STATION_MULTIPLAYER_PROFILE_REQUIRED', 'global gate with pending profiles would reject R76')
        result = dict(passed=True, checks=checks, catalogRevision=index['revision'], visibleItems=len(expected), contentIdentityFields=sum(bool(r.get('contentSha256')) for r in expected.values()) if identities else 0, metadataOptInVerified=True, allMetadataMatched=True, requestAndResponseSignaturesVerified=True, capabilities=capabilities, public=base.startswith('https:'), physicalGameplayQualified=False)
    finally:
        if not client.cleanup():
            raise ValueError('Owned R81 fixture cleanup failed')
    result['ownedSyntheticFixtureRemoved'] = True
    return result
