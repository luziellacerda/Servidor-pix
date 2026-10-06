"""Manual Station entitlements, checked only against the guarded temporary cluster."""
import hashlib,json,time,uuid
from concurrent.futures import ThreadPoolExecutor
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from station_http_smoke import PRODUCT,PREFIX,b64,proof,request,signed_payload,sql_value


def run(base,public_key,call,internal):
    assert sql_value('SHOW data_directory').startswith('/tmp/pg_virtualenv.')
    def payload(ref,kind='courtesy'):
        return dict(actor='fixture-registration',requestId=uuid.uuid4().hex,customerRef=ref,
            displayName='Synthetic registration',grantKind=kind,allowAdditional=False,
            reason='Synthetic administrator authorization only',stepUpAt=int(time.time()),
            clientIpDigest='b'*64,csrfVerified=True)
    route='/management/registrations'
    one=payload('TBX-USER-90001')
    assert call('POST',route,one,auth=False)[0]==401
    assert call('POST',route,dict(one,csrfVerified=False))[0]==400
    assert call('POST',route,dict(one,stepUpAt=0))[0]==400
    assert call('POST',route,dict(one,grantKind='unknown'))[0]==400
    assert call('POST',route,dict(one,customerRef='1'))[0]==400
    fields=['requestId','customerRef','displayName','grantKind','reason','allowAdditional']
    body={k:one[k] for k in fields}
    assert internal('POST','/station/registrations',body,claim='station.licenses.read')[0]==403
    assert internal('POST','/station/registrations',body,claim='station.licenses.manage',controls=False)[0]==403
    assert internal('POST','/station/registrations',dict(body,amountCents=0),claim='station.licenses.manage')[0]==400
    codes=[]
    for number,kind in enumerate(['courtesy','test','paid'],90001):
        item=one if number==90001 else payload('TBX-USER-'+str(number),kind)
        status,result=call('POST',route,item);assert status==200,'registration failed: '+str(status)
        lid=result['licenseId'];assert result['grantKind']==kind and result['amountCents']==(9990 if kind=='paid' else 0)
        replay=call('POST',route,item);assert replay[0]==200 and replay[1]['replayed'] and replay[1]['licenseId']==lid
        assert call('POST',route,dict(item,displayName='Changed registration'))[0]==409
        row=call('GET','/management/licenses/'+lid+'/support')[1]
        assert row['license']['eligibleDelivery'] and row['license']['grantKind']==kind
        assert row['license']['isTest']==(kind=='test')
        assert any(e['event']=='STATION_ADMIN_LICENSE_CREATED' and e['reason']==item['reason'] for e in row['history'])
        issued=call('POST','/management/licenses/'+lid+'/issue-code',dict(item,
            requestId=uuid.uuid4().hex,expectedGeneration=row['license']['revocationGeneration'],
            expectedActivationGeneration=row['license']['activationGeneration']))
        assert issued[0]==200 and issued[1]['ttlMinutes']==30
        code=issued[1]['activationCode'];codes.append(code)
        assert code not in json.dumps(row)
        if kind=='courtesy':activate(base,public_key,lid,code,'Synthetic registration')
    assert sql_value("SELECT count(*) FROM suite.suite_commerce_inbox WHERE source_system='STATION_ADMIN_V1'")=='3'
    receipt=sql_value("SELECT result_json::text FROM suite.suite_commerce_inbox WHERE source_system='STATION_ADMIN_V1'")
    assert all(code not in receipt for code in codes)
    duplicate=payload('TBX-USER-90001')
    refused=call('POST',route,duplicate);assert refused[0]==409 and refused[1]['code']=='STATION_CUSTOMER_ALREADY_LICENSED'
    assert call('POST',route,dict(duplicate,allowAdditional=True))[0]==200
    concurrent=[payload('TBX-USER-90004'),payload('TBX-USER-90004')]
    with ThreadPoolExecutor(2) as pool:statuses=list(pool.map(lambda item:call('POST',route,item)[0],concurrent))
    assert sorted(statuses)==[200,409],'two requests created a duplicate license'
    print('STATION REGISTRATION: OK (claims/CSRF/step-up, explicit paid/free receipts, replay, changed request rejection, duplicate/race protection, additional device consent, real API activation/profile/session)')


def activate(base,public_key,license_id,code,display_name):
    key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
    spki=key.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
    who=dict(schemaVersion=1,productId=PRODUCT,applicationId=PRODUCT,deviceId=b64(hashlib.sha256(spki).digest()),
        clientVersion='registration-fixture',deviceManufacturer='Synthetic',deviceModel='Registration Fixture',androidSdk=35)
    c=signed_payload(request(base,'POST','/v1/station/activations/challenge',dict(who,
        domain=PREFIX+'request-activation-challenge/v1',activationCode=code,devicePublicKey=b64(spki))),public_key,'activation-challenge')
    active=signed_payload(request(base,'POST','/v1/station/activations/complete',proof(key,dict(who,
        domain=PREFIX+'activate/v1',activationCode=code,devicePublicKey=b64(spki),challengeId=c['challengeId'],nonce=c['nonce']))),public_key,'activated')
    assert active['licenseId']==license_id
    c=signed_payload(request(base,'POST','/v1/station/challenges',dict(who,
        domain=PREFIX+'request-session-challenge/v1',licenseId=license_id)),public_key,'session-challenge')
    session=signed_payload(request(base,'POST','/v1/station/sessions',proof(key,dict(who,
        domain=PREFIX+'open-session/v1',licenseId=license_id,challengeId=c['challengeId'],nonce=c['nonce']))),public_key,'session')
    me=signed_payload(request(base,'GET','/v1/station/me',bearer=session['accessToken']),public_key,'profile')
    assert me['licenseId']==license_id and me['displayName']==display_name
