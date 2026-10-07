#!/usr/bin/env python3
"""Bounded authorized rollout checks with a disposable, ownership-marked license.
Only redacted aggregate evidence is returned. No real activation code is used.
"""
import base64,hashlib,importlib.util,json,secrets,time,uuid
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def verify(index,values,base,execute_sql=None):
    ops=load('implantar-station-20261003.py');helper=load('verificar-http-release-station.py')
    if execute_sql is None:
        db=ops.database_name(values);execute_sql=lambda text:ops.sql(db,text)
    public=serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(),None).public_key()
    pepper=base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes(),validate=True)
    client=helper.StationReleaseVerification(execute_sql,pepper,public,index);checks=0
    def expect(response,status,code=None):
        nonlocal checks
        if response[0]!=status or code and json.loads(response[2]).get('code')!=code:
            raise ValueError('Station security HTTP gate failed')
        checks+=1;return response
    def call(method,path,body=None,credential=None,proof=None):
        correlation=uuid.uuid4().hex;headers={'X-Correlation-ID':correlation,'Accept':'application/json',
            'User-Agent':'Dalvik/2.1.0 (Linux; U; Android 13; Station Release Verification)'}
        if credential:headers['Authorization']='Bearer '+credential
        if proof:headers['X-Station-Request-Proof']=proof
        encoded=json.dumps(body,separators=(',',':')).encode() if body is not None else None
        if encoded is not None:headers['Content-Type']='application/json'
        try:response=client.opener.open(Request(base+path,encoded,headers,method=method),timeout=20)
        except HTTPError as error:response=error
        with response:
            if response.url!=base+path or response.headers.get('X-Correlation-ID')!=correlation:
                raise ValueError('Redirect/correlation changed: '+method+' '+path+' HTTP '+str(response.code)+
                    ' cache='+response.headers.get('CF-Cache-Status','none')+
                    ' correlation='+('present' if response.headers.get('X-Correlation-ID') else 'absent'))
            maximum=(16 if path.startswith('/v1/station/catalog') else 2)*1024*1024
            data=response.read(maximum+1)
            if len(data)>maximum:raise ValueError('Bounded security response exceeded limit')
            return response.code,response.headers,data
    def signed(response,domain):return client.signed(expect(response,200),domain)
    def proof(credential,method,path,body=None,nonce=None):
        nonce=helper.b64(secrets.token_bytes(16)) if nonce is None else nonce;timestamp=int(time.time())
        data=json.dumps(body,separators=(',',':')).encode() if body is not None else b''
        canonical=('TurboRamaStationAndroid/request/v1\n'+method+'\n'+path+'\n'+hashlib.sha256(data).hexdigest()+
            '\n'+hashlib.sha256(credential.encode()).hexdigest()+'\n'+str(timestamp)+'\n'+nonce+'\n').encode()
        signature=client.device.sign(canonical,padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        return 'v1.'+str(timestamp)+'.'+nonce+'.'+helper.b64(signature)
    try:
        client.create()
        challenge=signed(call('POST','/v1/station/activations/challenge',dict(client.identity,
            domain=helper.PREFIX+'request-activation-challenge/v1',activationCode=client.code,devicePublicKey=client.spki)),'activation-challenge')
        if challenge['security']['requestProofVersion']!=1 or challenge['security']['requireVerifiedApp']:
            raise ValueError('Compatibility policy differs')
        signed(call('POST','/v1/station/activations/complete',client.proof(dict(client.identity,
            domain=helper.PREFIX+'activate/v1',activationCode=client.code,devicePublicKey=client.spki,
            challengeId=challenge['challengeId'],nonce=challenge['nonce'],requestProof='rsa-pss-v1'))),'activated')
        def open_session(protected):
            challenge=signed(call('POST','/v1/station/challenges',dict(client.identity,
                domain=helper.PREFIX+'request-session-challenge/v1',licenseId=client.license_id)),'session-challenge')
            data=dict(client.identity,domain=helper.PREFIX+'open-session/v1',licenseId=client.license_id,
                challengeId=challenge['challengeId'],nonce=challenge['nonce'])
            if protected:data['requestProof']='rsa-pss-v1'
            return call('POST','/v1/station/sessions',client.proof(data))
        session=signed(open_session(True),'session');credential=session['accessToken']
        if session['requestProof']!='rsa-pss-v1' or session['verifiedApp']:raise ValueError('Protected mode differs')
        expect(call('GET','/v1/station/me',credential=credential),401,'STATION_REQUEST_PROOF_REQUIRED')
        path='/v1/station/catalog?metadata=1';header=proof(credential,'GET',path)
        catalog=signed(call('GET',path,credential=credential,proof=header),'catalog')
        expected={r['itemId'] for r in index['items'] if r.get('catalogVisible',True)}
        if catalog['revision']!=index['revision'] or {r['itemId'] for r in catalog['items']}!=expected:
            raise ValueError('Full catalog differs')
        if any(not isinstance(r.get('metadata'),dict) for r in catalog['items']):raise ValueError('Optional metadata contract differs')
        expect(call('GET',path,credential=credential,proof=header),409,'STATION_REQUEST_PROOF_REPLAY')
        expect(call('GET','/v1/station/me',credential=credential,proof=header),401,'STATION_REQUEST_PROOF_INVALID')
        expect(open_session(False),403,'STATION_SECURITY_DOWNGRADE_DENIED')
        signed(call('GET','/v1/station/me',credential=credential,proof=proof(credential,'GET','/v1/station/me')),'profile')
        rows=[r for r in index['items'] if r.get('catalogVisible',True)][:4]
        def cover(row):
            path='/v1/station/covers/'+row['coverId'];response=call('GET',path,credential=credential,proof=proof(credential,'GET',path))
            if response[0]!=200 or response[2]!=Path(row['coverPath']).read_bytes():raise ValueError('Protected cover differs')
        with ThreadPoolExecutor(max_workers=4) as executor:list(executor.map(cover,rows))
        checks+=len(rows)
        return dict(passed=True,checks=checks,copiedTokenDenied=True,requestReplayDenied=True,
            changedPathDenied=True,signedDowngradeDenied=True,completeCatalogPreserved=True,
            fourConcurrentProtectedCovers=True,hardwarePhoneVerified=False,strictPolicyEnforced=False)
    finally:
        if not client.cleanup():raise ValueError('Synthetic security license cleanup failed')
