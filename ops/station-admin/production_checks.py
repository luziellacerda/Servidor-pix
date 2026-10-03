"""Station-only synthetic production checks; never send a customer notification."""
import base64,hashlib,importlib.util,json,os,time,uuid
from pathlib import Path
from urllib.request import Request,build_opener,HTTPRedirectHandler
from urllib.error import HTTPError
from cryptography.hazmat.primitives import serialization

ROOT=Path(__file__).resolve().parents[2]
BASE='https://app.lzgames.com.br'
SITE='https://turbobox.lzgames.com.br'
PREFIX='TurboRamaStationAndroid/'
class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None

def verify(db,sql,environment):
    spec=importlib.util.spec_from_file_location('station_release_verification',ROOT/'docs/station-android/scripts/verificar-http-release-station.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    env=environment('turborama-station-api.service')
    public=serialization.load_pem_private_key(Path(env['Station__AssertionPrivateKeyPemFile']).read_bytes(),password=None).public_key()
    key_id=hashlib.sha256(public.public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
    if key_id!='06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268':raise ValueError('production signing identity changed')
    pepper=base64.b64decode(Path(env['Station__ActivationPepperFile']).read_text().strip(),validate=True)
    first=module.StationReleaseVerification(lambda s:sql(db,s),pepper,public,{})
    opener=build_opener(NoRedirect)
    # Obtain only the known helper token path. Never expose the token or environments.
    helper_env=environment('turborama-station-issue-admin.service')
    token=Path(helper_env['STATION_ISSUE_TOKEN_FILE']).read_text().strip()
    def call(method,path,body=None,authenticated=True):
        headers={'Content-Type':'application/json'}
        if authenticated:headers['X-Station-Admin-Token']=token
        req=Request('http://127.0.0.1:5194'+path,None if body is None else json.dumps(body).encode(),headers,method=method)
        try:
            with opener.open(req,timeout=15) as r:return r.status,json.loads(r.read())
        except HTTPError as e:return e.code,json.loads(e.read())
    def support():
        status,data=call('GET','/management/licenses/'+first.license_id+'/support')
        if status!=200:raise ValueError('synthetic support lookup failed')
        return data
    def action(name):
        row=support()['license']
        body={'actor':'station-publish-synthetic','requestId':uuid.uuid4().hex,
          'expectedGeneration':row['revocationGeneration'],'expectedActivationGeneration':row['activationGeneration'],
          'reason':'Synthetic Station panel deployment verification.','stepUpAt':int(time.time()),
          'clientIpDigest':hashlib.sha256(b'station-panel-private-probe').hexdigest(),'csrfVerified':True,
          'targetSessionId':row.get('sessionId') or None}
        status,data=call('POST','/management/licenses/'+first.license_id+'/'+name,body)
        if status!=200:raise ValueError('synthetic management action failed: '+name)
        return data,body
    def activate(device,code):
        device.code=code
        c=device.signed(device.request(BASE,'POST','/v1/station/activations/challenge',dict(device.identity,
          domain=PREFIX+'request-activation-challenge/v1',activationCode=code,devicePublicKey=device.spki)),'activation-challenge')
        device.signed(device.request(BASE,'POST','/v1/station/activations/complete',device.proof(dict(device.identity,
          domain=PREFIX+'activate/v1',activationCode=code,devicePublicKey=device.spki,challengeId=c['challengeId'],nonce=c['nonce']))),'activated')
        return session(device)
    def session(device):
        c=device.signed(device.request(BASE,'POST','/v1/station/challenges',dict(device.identity,
          domain=PREFIX+'request-session-challenge/v1',licenseId=first.license_id)),'session-challenge')
        s=device.signed(device.request(BASE,'POST','/v1/station/sessions',device.proof(dict(device.identity,
          domain=PREFIX+'open-session/v1',licenseId=first.license_id,challengeId=c['challengeId'],nonce=c['nonce']))),'session')
        device.signed(device.request(BASE,'GET','/v1/station/me',bearer=s['accessToken']),'profile')
        return s
    def next_device():
        device=module.StationReleaseVerification(lambda s:sql(db,s),pepper,public,{})
        device.license_id=first.license_id;return device
    def denied(response,status,code):
        if response[0]!=status or json.loads(response[2]).get('code')!=code:raise ValueError('old authorization remained valid')
    result={'customerNotificationsSent':False,'realCustomerWrites':False}
    try:
        first.create()
        sql(db,"UPDATE suite.station_customer_projection SET customer_ref='teste-station' WHERE license_id='"+first.license_id+"'")
        if call('GET','/management/licenses',authenticated=False)[0]!=401:raise ValueError('helper authentication gate failed')
        issued,body=action('issue-code')
        if issued['ttlMinutes']!=30:raise ValueError('support code expiry changed')
        if call('POST','/management/licenses/'+first.license_id+'/issue-code',body)[0]!=409:raise ValueError('duplicate code rotated twice')
        action('cancel-code')
        denied(first.request(BASE,'POST','/v1/station/activations/challenge',dict(first.identity,
          domain=PREFIX+'request-activation-challenge/v1',activationCode=issued['activationCode'],devicePublicKey=first.spki)),403,'STATION_ACTIVATION_INVALID')
        status,purchase=call('POST','/licenses/'+first.license_id+'/issue-purchase',{'actor':'station-purchase-synthetic','requestId':uuid.uuid4().hex})
        if status!=200 or purchase['ttlMinutes']!=2880:raise ValueError('commerce issuance contract changed')
        old=activate(first,purchase['activationCode'])
        recovered,replay=action('new-device')
        denied(first.request(BASE,'GET','/v1/station/me',bearer=old['accessToken']),401,'STATION_SESSION_INVALID')
        denied(first.request(BASE,'POST','/v1/station/challenges',dict(first.identity,
          domain=PREFIX+'request-session-challenge/v1',licenseId=first.license_id)),403,'STATION_DEVICE_DENIED')
        second=next_device();current=activate(second,recovered['activationCode'])
        if call('POST','/management/licenses/'+first.license_id+'/new-device',replay)[0]!=409:raise ValueError('replayed recovery changed binding')
        action('block');denied(second.request(BASE,'GET','/v1/station/me',bearer=current['accessToken']),401,'STATION_SESSION_INVALID')
        action('unblock');current=session(second)
        action('revoke-session');denied(second.request(BASE,'GET','/v1/station/me',bearer=current['accessToken']),401,'STATION_SESSION_INVALID')
        session(second)
        reinstalled,_=action('reinstall');third=next_device();activate(third,reinstalled['activationCode'])
        action('transfer')
        pending=support()['license']
        if pending['deviceLinked'] or pending['codeIssued'] or pending['activationConsumed']:raise ValueError('transfer did not leave a pending license')
        action('issue-code');action('cancel-code')
        history=support()
        if len(history['devices'])!=3 or len(history['history'])<10:raise ValueError('support history is incomplete')
        if any(raw in json.dumps(history) for raw in (issued['activationCode'],purchase['activationCode'],recovered['activationCode'],reinstalled['activationCode'])):
            raise ValueError('history exposed an activation code')
        matched=call('GET','/management/licenses?q='+first.license_id+'&limit=1')
        if matched[0]!=200 or matched[1]['total']!=1:raise ValueError('production search missed the fixture')
        result.update(syntheticManagementActions=True,sameLicenseAcrossThreeKeys=True,code30m=True,purchase48h=True,
          codeCancelDenied=True,repeatedRequestDenied=True,oldDeviceDenied=True,oldSessionDenied=True,
          blockUnblockVerified=True,reinstallationVerified=True,reconnectVerified=True,historyVerified=True)
    finally:
        if first.created:
            id=first.license_id
            # Guard ownership before deleting even synthetic audit/receipt rows.
            guard="DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM suite.suite_license_deliveries WHERE license_id='"+id+"' AND source_system='STATION_ROLLOUT_TEST' AND source_purchase_id='"+first.marker+"') THEN RAISE EXCEPTION 'synthetic ownership changed'; END IF; END $$; "
            sql(db,"BEGIN; SET LOCAL lock_timeout='5s'; "+guard+"DELETE FROM suite.station_admin_code_issues WHERE license_id='"+id+"'; DELETE FROM suite.suite_lifecycle_commands WHERE scope='STATION_ANDROID' AND license_id='"+id+"'; DELETE FROM suite.suite_audit_events WHERE license_id='"+id+"'; COMMIT;")
            if not first.cleanup():raise ValueError('synthetic Station records remain')
            result['syntheticRowsRemoved']=True
    # Public site authentication and exact installed CSS/JS, without signing in as a real operator.
    headers={'User-Agent':'Mozilla/5.0 StationPanelDeploymentVerification','Cache-Control':'no-cache'}
    try:
        with opener.open(Request(SITE+'/admin/station',headers=headers),timeout=15) as r:status=r.status;location=''
    except HTTPError as e:status=e.code;location=e.headers.get('Location','')
    if status not in (301,302,303) or location not in ('/login.php','/login','/admin/login'):raise ValueError('Station site did not require operator authentication')
    result['publicAdminRequiresLogin']=True
    for name in ('station-admin.css','station-admin.js'):
        with opener.open(Request(SITE+'/'+name+'?v=20261003-2',headers=headers),timeout=15) as r:
            if r.status!=200 or hashlib.sha256(r.read()).hexdigest()!=hashlib.sha256((ROOT/'ops/station-admin/site'/name).read_bytes()).hexdigest():
                raise ValueError('public Station asset differs: '+name)
    result['publicAssetsHashVerified']=True
    return result
