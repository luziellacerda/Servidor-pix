"""Security/compatibility checks, called only by the guarded pg_virtualenv fixture."""
import base64,hashlib,hmac,json,os,secrets,socket,subprocess,time,uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.error import HTTPError,URLError
from urllib.request import Request,urlopen
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import ec,padding,rsa
import station_http_smoke as fixture

def run(base,folder,env,server_public,api_command):
    assert os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.')
    assert fixture.sql_value('SHOW data_directory').startswith('/tmp/pg_virtualenv.')
    checks=[]
    def call(endpoint,method,path,body=None,credential=None,proof=None):
        headers={'Content-Type':'application/json','X-Correlation-ID':uuid.uuid4().hex}
        if credential:headers['Authorization']='Bearer '+credential
        if proof:headers['X-Station-Request-Proof']=proof
        encoded=json.dumps(body,separators=(',',':')).encode() if body is not None else None
        try:
            with urlopen(Request(endpoint+path,encoded,headers,method=method),timeout=15) as reply:
                return reply.status,dict(reply.headers),reply.read()
        except HTTPError as reply:return reply.code,dict(reply.headers),reply.read()
    def expect(name,response,status,code=None):
        assert response[0]==status,(name,response[0],status)
        if code:assert json.loads(response[2])['code']==code,(name,'error code')
        checks.append(name);return response
    def payload(response,domain):return fixture.signed_payload(response,server_public,domain)
    pepper=base64.b64decode(Path(env['Station__ActivationPepperFile']).read_bytes())
    def customer(label):
        key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        spki=key.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
        device=fixture.b64(hashlib.sha256(spki).digest());license='STA-'+secrets.token_hex(16).upper()
        code=fixture.b64(secrets.token_bytes(32));verifier=hmac.digest(pepper,code.encode(),'sha256').hex()
        fixture.sql("INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,"
          "activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices,"
          "provisioning_origin,enrollment_state,claim_mode) VALUES('"+license+"','"+fixture.PRODUCT+"','ACTIVE','"+
          verifier+"',clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')")
        fixture.sql("INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,"
          "source_product_sku,product_id,license_id,provisioning_state,financial_state,last_source_version) VALUES("
          "'STATION_SECURITY_TEST','"+license+"','fixture','STATION_ANDROID_LIFETIME_1_DEVICE','"+fixture.PRODUCT+"','"+
          license+"','PROVISIONED','PAID',1)")
        fixture.sql("INSERT INTO suite.station_customer_projection(license_id,source_system,source_purchase_id,source_item_key,"
          "customer_ref,display_name) VALUES('"+license+"','STATION_SECURITY_TEST','"+license+"','fixture','synthetic','Security fixture')")
        identity=dict(schemaVersion=1,productId=fixture.PRODUCT,applicationId=fixture.PRODUCT,deviceId=device,
            clientVersion='security-fixture',deviceManufacturer='Synthetic',deviceModel=label,androidSdk=35)
        return dict(key=key,spki=fixture.b64(spki),license=license,code=code,identity=identity)
    def activation(client,endpoint=base):
        return payload(call(endpoint,'POST','/v1/station/activations/challenge',dict(client['identity'],
            domain=fixture.PREFIX+'request-activation-challenge/v1',activationCode=client['code'],devicePublicKey=client['spki'])), 'activation-challenge')
    def complete(client,challenge,mode='rsa-pss-v1',endpoint=base,extra=None):
        data=dict(client['identity'],domain=fixture.PREFIX+'activate/v1',activationCode=client['code'],
            devicePublicKey=client['spki'],challengeId=challenge['challengeId'],nonce=challenge['nonce'],requestProof=mode)
        envelope=fixture.proof(client['key'],dict(data,**(extra or {})))
        return call(endpoint,'POST','/v1/station/activations/complete',envelope)
    def session(client,mode='rsa-pss-v1',include_mode=True):
        ch=payload(call(base,'POST','/v1/station/challenges',dict(client['identity'],
            domain=fixture.PREFIX+'request-session-challenge/v1',licenseId=client['license'])),'session-challenge')
        data=dict(client['identity'],domain=fixture.PREFIX+'open-session/v1',licenseId=client['license'],
            challengeId=ch['challengeId'],nonce=ch['nonce'])
        if include_mode:data['requestProof']=mode
        return call(base,'POST','/v1/station/sessions',fixture.proof(client['key'],data))
    def sign(client,credential,method,path,body=None,timestamp=None,nonce=None,key=None):
        timestamp=int(time.time()) if timestamp is None else timestamp
        nonce=fixture.b64(secrets.token_bytes(16)) if nonce is None else nonce
        data=json.dumps(body,separators=(',',':')).encode() if body is not None else b''
        canonical=('TurboRamaStationAndroid/request/v1\n'+method+'\n'+path+'\n'+hashlib.sha256(data).hexdigest()+
            '\n'+hashlib.sha256(credential.encode('ascii')).hexdigest()+'\n'+str(timestamp)+'\n'+nonce+'\n').encode()
        signature=(key or client['key']).sign(canonical,padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        return 'v1.'+str(timestamp)+'.'+nonce+'.'+fixture.b64(signature)

    client=customer('protected');ch=activation(client)
    assert ch['security']['requestProofVersion']==1 and not ch['security']['requireVerifiedApp']
    payload(expect('protected activation',complete(client,ch),200),'activated')
    opened=payload(expect('protected session',session(client),200),'session');token=opened['accessToken']
    assert opened['requestProof']=='rsa-pss-v1' and opened['verifiedApp'] is False
    for path in ['/v1/station/me','/v1/station/catalog','/v1/station/covers/cover-synthetic-01']:
        expect('copied token '+path,call(base,'GET',path,credential=token),401,'STATION_REQUEST_PROOF_REQUIRED')
    path='/v1/station/catalog?metadata=1';header=sign(client,token,'GET',path)
    payload(expect('valid signed catalog',call(base,'GET',path,credential=token,proof=header),200),'catalog')
    expect('request replay',call(base,'GET',path,credential=token,proof=header),409,'STATION_REQUEST_PROOF_REPLAY')
    expect('changed query',call(base,'GET','/v1/station/catalog?metadata=0',credential=token,proof=header),401,'STATION_REQUEST_PROOF_INVALID')
    expect('changed route',call(base,'GET','/v1/station/me',credential=token,proof=header),401,'STATION_REQUEST_PROOF_INVALID')
    foreign=rsa.generate_private_key(public_exponent=65537,key_size=2048)
    expect('foreign key',call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path,key=foreign)),401,'STATION_REQUEST_PROOF_INVALID')
    expect('expired request',call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path,timestamp=int(time.time())-100)),401,'STATION_REQUEST_PROOF_EXPIRED')
    expect('future request',call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path,timestamp=int(time.time())+100)),401,'STATION_REQUEST_PROOF_EXPIRED')
    path='/v1/station/downloads/authorize'
    body=dict(client['identity'],domain=fixture.PREFIX+'request-download/v1',itemId='item-synthetic-01')
    expect('changed body',call(base,'POST',path,dict(body,itemId='item-synthetic-02'),token,sign(client,token,'POST',path,body)),401,'STATION_REQUEST_PROOF_INVALID')
    grant=payload(expect('protected grant',call(base,'POST',path,body,token,sign(client,token,'POST',path,body)),200),'download-grant')
    # The grant is still available after a missing request proof, then is consumed once.
    path='/v1/station/artifacts/'+grant['grantId']
    expect('copied token and grant',call(base,'GET',path,credential=token),401,'STATION_REQUEST_PROOF_REQUIRED')
    result=expect('protected artifact bytes',call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path)),200)
    assert result[2]==(folder/'item.bin').read_bytes()
    expect('grant remains single use',call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path)),404,'STATION_GRANT_NOT_FOUND')
    def cover(_):
        path='/v1/station/covers/cover-synthetic-01'
        reply=call(base,'GET',path,credential=token,proof=sign(client,token,'GET',path))
        assert reply[0]==200 and reply[2]==fixture.PNG
    with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(cover,range(16)))
    checks.append('16 protected covers with four concurrent requests')
    expect('signed legacy downgrade denied',session(client,include_mode=False),403,'STATION_SECURITY_DOWNGRADE_DENIED')
    expect('current protected session remains valid',call(base,'GET','/v1/station/me',credential=token,
        proof=sign(client,token,'GET','/v1/station/me')),200)
    expect('app token cannot enter Suite',call(base,'POST','/v1/suite/sessions',{},token),503,'SUITE_DISABLED')
    if env.get('Station__Online__RelayEnabled')=='true':
        from websockets.sync.client import connect
        from websockets.exceptions import InvalidStatus
        peer=customer('protected-relay');payload(complete(peer,activation(peer)),'activated')
        peer_token=payload(session(peer),'session')['accessToken']
        def command(who,credential,action,**extra):
            body=dict(action=action,requestId=str(uuid.uuid4()),**extra);route='/v1/station/online/command'
            return payload(call(base,'POST',route,body,credential,sign(who,credential,'POST',route,body)),'online')['snapshot']
        try:
            view=command(client,token,'enter',nickname='Protected host');command(peer,peer_token,'enter',nickname='Protected guest')
            engine=view['engines'][0]
            game_hashes=dict(contentSha256=hashlib.sha256((folder/'item.bin').read_bytes()).hexdigest(),
                optionsSha256=hashlib.sha256(b'security-options').hexdigest(),coreSha256=engine['coreSha256'],runtimeSha256=engine['runtimeSha256'])
            room=command(client,token,'create',itemId='item-synthetic-01',engineId=engine['engineId'],**game_hashes)['room']['roomId']
            command(peer,peer_token,'join',roomId=room,**game_hashes)
            command(client,token,'ready',roomId=room,value=True);command(peer,peer_token,'ready',roomId=room,value=True)
            command(client,token,'start',roomId=room,transport='relay-wss-v1')
            ticket=command(client,token,'relay-ticket',roomId=room)['room']['relay']['ticket'];route='/v1/station/online/relay'
            uri=base.replace('http://','ws://')+route
            def websocket(credential,proof=None):
                headers={'Authorization':'StationRelay '+credential}
                if proof:headers['X-Station-Request-Proof']=proof
                return connect(uri,subprotocols=['station-relay.v1'],additional_headers=headers,compression=None,
                    proxy=None,open_timeout=10,close_timeout=1,max_size=65536)
            try:
                stream=websocket(ticket);stream.close();raise AssertionError('Copied protected relay ticket accepted')
            except InvalidStatus as denied:assert denied.response.status_code==401
            checks.append('copied protected relay ticket denied')
            with websocket(ticket,sign(client,ticket,'GET',route)) as host:
                command(client,token,'host-listening',roomId=room)
                other_ticket=command(peer,peer_token,'relay-ticket',roomId=room)['room']['relay']['ticket']
                with websocket(other_ticket,sign(peer,other_ticket,'GET',route)) as guest:
                    data=b'controlled protected relay';host.send(data);assert guest.recv(timeout=10)==data
                    guest.send(data[::-1]);assert host.recv(timeout=10)==data[::-1]
            checks.append('protected relay exact bytes in both directions')
        finally:
            command(client,token,'offline');command(peer,peer_token,'offline')

    # A forged certificate cannot claim verified APK status. Compatibility fallback
    # still uses the primary RSA key, not that untrusted EC key.
    other=customer('untrusted-EC');ch=activation(other)
    ec_key=ec.generate_private_key(ec.SECP256R1())
    extra=dict(requestProof='ec-p256-v1',requestProofKey=fixture.b64(ec_key.public_key().public_bytes(
        serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)),allowUnverifiedApp=False)
    data=dict(other['identity'],domain=fixture.PREFIX+'activate/v1',activationCode=other['code'],devicePublicKey=other['spki'],
        challengeId=ch['challengeId'],nonce=ch['nonce'],**extra)
    def ec_envelope(data):
        encoded=json.dumps(data,separators=(',',':')).encode();envelope=fixture.proof(other['key'],data)
        envelope.update(attestationChain=[fixture.b64(b'not a certificate')]*2,
            keySignature=fixture.b64(ec_key.sign(encoded,ec.ECDSA(hashes.SHA256()))));return envelope
    expect('forged app attestation denied',call(base,'POST','/v1/station/activations/complete',ec_envelope(data)),403,'STATION_APP_ATTESTATION_INVALID')
    data['allowUnverifiedApp']=True
    payload(expect('explicit proof-only compatibility',call(base,'POST','/v1/station/activations/complete',ec_envelope(data)),200),'activated')
    opened=payload(session(other),'session');assert opened['requestProof']=='rsa-pss-v1' and not opened['verifiedApp']
    expect('other token invalidates request signature',call(base,'GET','/v1/station/me',credential=opened['accessToken'],
        proof=sign(client,token,'GET','/v1/station/me')),401,'STATION_REQUEST_PROOF_INVALID')
    # Stage the strict policy on a second temporary listener, never the running API.
    with socket.socket() as listener:listener.bind(('127.0.0.1',0));port=listener.getsockname()[1]
    strict='http://127.0.0.1:'+str(port);strict_env=dict(env,Station__Security__RequireVerifiedApp='true')
    command=list(api_command);command[-1]=strict
    log=(folder/'strict-security.log').open('wb');process=subprocess.Popen(command,env=strict_env,cwd=fixture.ROOT,stdout=log,stderr=subprocess.STDOUT)
    try:
        for _ in range(80):
            if process.poll() is not None:raise AssertionError('Strict policy candidate stopped')
            try:
                if call(strict,'GET','/ready/station')[0]==200:break
            except URLError:pass
            time.sleep(.1)
        else:raise AssertionError('Strict candidate not ready')
        fresh=customer('strict');challenge=activation(fresh,strict)
        assert challenge['security']['requireVerifiedApp'] is True
        expect('strict policy blocks software activation',complete(fresh,challenge,endpoint=strict),403,'STATION_VERIFIED_APP_REQUIRED')
        assert fixture.sql_value("SELECT activation_consumed FROM suite.suite_licenses WHERE license_id='"+fresh['license']+"'")=='f'
    finally:process.terminate();process.wait(timeout=15);log.close()
    if os.environ.get('STATION_HTTP_SECURITY_JAVA_CLASSPATH'):
        java=customer('JVM');config=folder/'java-security-fixture.json'
        config.write_text(json.dumps(dict(origin=base,deviceSpki=java['spki'],
            devicePrivate=fixture.b64(java['key'].private_bytes(serialization.Encoding.DER,
                serialization.PrivateFormat.PKCS8,serialization.NoEncryption())),
            authoritySpki=fixture.b64(server_public.public_bytes(serialization.Encoding.DER,
                serialization.PublicFormat.SubjectPublicKeyInfo)),code=java['code'],license=java['license'],coverBytes=len(fixture.PNG))))
        config.chmod(0o600)
        try:
            result=subprocess.run(['java','-cp',os.environ['STATION_HTTP_SECURITY_JAVA_CLASSPATH'],
                'org.emulationstation.frontend.station.StationSecurityHttpTest',str(config)],
                capture_output=True,text=True,timeout=45)
            if result.returncode:raise AssertionError('Java security integration failed: '+result.stderr[-1600:])
            print(result.stdout.strip());checks.append('actual Java/backend protected integration')
        finally:config.unlink(missing_ok=True)
    print('STATION SECURITY HTTP: '+json.dumps(dict(passed=True,checks=len(checks),isolated=True,
        legacyClientsPreserved=True,strictPolicyStaged=True,hardwarePhoneVerified=False)))
