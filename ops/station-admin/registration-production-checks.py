"""Disposable Station customers through the protected website; no notifications."""
import hashlib,html,importlib.util,json,re,sqlite3,time,uuid
from http.cookiejar import CookieJar
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request,build_opener,HTTPCookieProcessor

ROOT=Path(__file__).resolve().parents[2]
URL='https://turbobox.lzgames.com.br'
APP='https://app.lzgames.com.br'
PREFIX='TurboRamaStationAndroid/'
LOGIN=Path('/home/lz-servidor/turbobox-admin-reset-20261006-122727/credenciais.json')
SITE_DB=Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902/.data/turbobox.sqlite')


def load(path,name):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


def verify(database,sql,environment,backup,manifest):
    from cryptography.hazmat.primitives import serialization
    browser=build_opener(HTTPCookieProcessor(CookieJar()))
    ua='Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36'
    def web(path,fields=None):
        req=Request(URL+path,None if fields is None else urlencode(fields).encode(),{'User-Agent':ua,'Accept':'application/json,text/html'})
        try:response=browser.open(req,timeout=30)
        except HTTPError as error:response=error
        with response:
            data=response.read(2*1024*1024+1)
            if len(data)>2*1024*1024:raise ValueError('bounded website response exceeded')
            return response.status,response.url,response.headers,data
    def post(fields):
        r=web('/admin/station?format=json',fields)
        return r[0],json.loads(r[3]) if 'application/json' in r[2].get('Content-Type','') else {}
    def csrf(page):
        match=re.search(r'name="csrf" value="([a-f0-9]+)"',page)
        if not match:raise ValueError('protected registration CSRF missing')
        return match.group(1)
    creds=json.loads(LOGIN.read_text());marker=uuid.uuid4().hex
    name='Synthetic Station customer '+marker[:8];email='station-validation-'+marker+'@example.invalid'
    requests=[uuid.uuid4().hex for _ in range(3)]
    plan={'marker':marker,'name':name,'email':email,'requests':requests,'customerId':None,'licenses':[]}
    (backup/'synthetic-cleanup.json').write_text(json.dumps(plan));(backup/'synthetic-cleanup.json').chmod(0o600)
    licenses=[];customer_id=None;logged_in=False
    try:
        if web('/admin/login')[0]!=200:raise ValueError('panel login page unavailable')
        login=web('/auth.php',{'login':creds['login'],'password':creds['password']})
        if login[0]!=200 or not json.loads(login[3]).get('ok'):raise ValueError('known administrator login failed')
        logged_in=True
        page=web('/admin/station')
        if page[0]!=200 or page[1]!=URL+'/admin/station':raise ValueError('protected Station page not reached')
        text=page[3].decode();token=csrf(text)
        if 'data-register-customer' not in text or 'station-registration-form' not in text:raise ValueError('new registration form absent')
        for asset in ('station-admin.js','station-admin.css'):
            asset_response=web('/'+asset+'?v=20261006-2')
            if asset_response[0]!=200 or hashlib.sha256(asset_response[3]).hexdigest()!=manifest['files']['site/'+asset]:
                raise ValueError('public registration asset hash differs')
        body=dict(action='register-customer',request_id=requests[0],customer_mode='new',customer_name=name,
            customer_email=email,customer_phone='',grant_kind='test',confirm_grant='1',
            reason='Synthetic Station registration publication verification.',csrf=token,password='synthetic-wrong-password')
        if post(body)[0]!=403:raise ValueError('registration password gate failed')
        with sqlite3.connect(SITE_DB) as site:
            if site.execute('SELECT count(*) FROM users WHERE email=?',(email,)).fetchone()[0]:raise ValueError('wrong password created a customer')
        body['password']=creds['password'];status,result=post(body)
        if status!=200 or not result.get('issued'):raise ValueError('protected customer registration failed')
        first=result['licenseId'];code=result['issued']['code'];licenses.append(first)
        with sqlite3.connect(SITE_DB) as site:
            row=site.execute("SELECT id FROM users WHERE email=? AND name=? AND role='customer' AND status='active'",(email,name)).fetchone()
            if not row:raise ValueError('new customer mapping missing')
            customer_id=row[0]
        plan.update(customerId=customer_id,licenses=licenses);(backup/'synthetic-cleanup.json').write_text(json.dumps(plan))
        repeated=post(body)
        if repeated[0]!=409 or repeated[1].get('licenseId')!=first or 'issued' in repeated[1]:raise ValueError('registration replay generated another code')
        customer=dict(action='register-customer',request_id=requests[1],customer_mode='existing',customer_id=str(customer_id),
            grant_kind='test',confirm_grant='1',reason=body['reason'],csrf=token,password=creds['password'])
        if post(customer)[0]!=409:raise ValueError('duplicate customer registration was not rejected')
        customer.update(request_id=requests[2],allow_additional='1')
        status,additional=post(customer)
        if status!=200 or not additional.get('issued') or additional.get('licenseId')==first:raise ValueError('additional Station license failed')
        second=additional['licenseId'];licenses.append(second)
        plan['licenses']=licenses;(backup/'synthetic-cleanup.json').write_text(json.dumps(plan))
        # Verify the unchanged R41 API accepts both panel-issued licenses and returns the correct buyer.
        env=environment('turborama-station-api.service')
        public=serialization.load_pem_private_key(Path(env['Station__AssertionPrivateKeyPemFile']).read_bytes(),password=None).public_key()
        keyid=hashlib.sha256(public.public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
        if keyid!='06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268':raise ValueError('Station signing identity changed')
        verify_module=load(ROOT/'docs/station-android/scripts/verificar-http-release-station.py','station_registration_device')
        devices=[]
        for license_id,activation_code in ((first,code),(second,additional['issued']['code'])):
            device=verify_module.StationReleaseVerification(lambda statement:sql(database,statement),b'',public,{})
            device.license_id=license_id
            challenge=device.signed(device.request(APP,'POST','/v1/station/activations/challenge',dict(device.identity,
                domain=PREFIX+'request-activation-challenge/v1',activationCode=activation_code,devicePublicKey=device.spki)),'activation-challenge')
            device.signed(device.request(APP,'POST','/v1/station/activations/complete',device.proof(dict(device.identity,
                domain=PREFIX+'activate/v1',activationCode=activation_code,devicePublicKey=device.spki,
                challengeId=challenge['challengeId'],nonce=challenge['nonce']))),'activated')
            challenge=device.signed(device.request(APP,'POST','/v1/station/challenges',dict(device.identity,
                domain=PREFIX+'request-session-challenge/v1',licenseId=license_id)),'session-challenge')
            session=device.signed(device.request(APP,'POST','/v1/station/sessions',device.proof(dict(device.identity,
                domain=PREFIX+'open-session/v1',licenseId=license_id,challengeId=challenge['challengeId'],nonce=challenge['nonce']))),'session')
            devices.append((device,session))
        for device,session in devices:
            profile=device.signed(device.request(APP,'GET','/v1/station/me',bearer=session['accessToken']),'profile')
            if profile['displayName']!=name:raise ValueError('app buyer name differs from website customer')
        with sqlite3.connect(SITE_DB) as site:
            if site.execute('SELECT count(*) FROM notification_jobs WHERE user_id=?',(customer_id,)).fetchone()[0]:raise ValueError('synthetic notification was queued')
            if site.execute('SELECT count(*) FROM purchases WHERE user_id=?',(customer_id,)).fetchone()[0]:raise ValueError('manual flow changed website purchases')
        result={'protectedPage':True,'publicAssetsHashVerified':True,'wrongPasswordNoCustomer':True,
            'newCustomerAndCode':True,'existingCustomerAndAdditionalLicense':True,'duplicateAndReplayRejected':True,
            'signedActivationAndProfile':True,'twoIndependentDeviceSessions':True,'notificationsQueued':0,
            'websitePurchasesCreated':0,'signingKeyPreserved':True,'syntheticLicenses':2}
    finally:
        # The exact nonce, admin receipt and private customer mapping prove ownership before removal.
        with sqlite3.connect(SITE_DB) as site:
            row=site.execute("SELECT id FROM users WHERE email=? AND name=? AND role='customer'",(email,name)).fetchone()
            if row:customer_id=row[0]
            for req in requests:
                raw=sql(database,"SELECT result_json::text FROM suite.suite_commerce_inbox WHERE source_system='STATION_ADMIN_V1' AND source_event_id='"+req+"'").strip()
                if not raw:continue
                receipt=json.loads(raw);lid=receipt['licenseId']
                if customer_id is None or receipt['customerRef']!='TBX-USER-'+str(customer_id) or receipt['displayName']!=name or receipt['grantKind']!='test':
                    raise ValueError('synthetic registration ownership marker changed')
                if not re.fullmatch(r'STA-[A-F0-9]{32}',lid):raise ValueError('unexpected disposable license identifier')
                statement="BEGIN; SET LOCAL lock_timeout='5s'; "
                for table in ('station_download_grants','station_sessions','station_challenges','station_admin_code_issues',
                    'suite_lifecycle_commands','suite_audit_events','station_customer_projection','station_devices',
                    'suite_license_deliveries','suite_licenses'):
                    statement+="DELETE FROM suite."+table+" WHERE license_id='"+lid+"'; "
                statement+="DELETE FROM suite.suite_commerce_inbox WHERE source_system='STATION_ADMIN_V1' AND source_event_id='"+req+"'; COMMIT;"
                sql(database,statement)
                site.execute('DELETE FROM station_contacts WHERE license_id=? AND user_id=?',(lid,customer_id))
                site.execute("DELETE FROM audit_log WHERE action='station_customer_registered' AND detail=?",(lid+'; test',))
            if customer_id is not None:
                for req in requests:site.execute('DELETE FROM station_registrations WHERE request_id=? AND customer_user_id=?',(req,customer_id))
                site.execute("DELETE FROM users WHERE id=? AND email=? AND name=? AND role='customer'",(customer_id,email,name))
            if site.execute('SELECT count(*) FROM users WHERE email=?',(email,)).fetchone()[0]:raise ValueError('synthetic website customer remains')
        for req in requests:
            if sql(database,"SELECT count(*) FROM suite.suite_commerce_inbox WHERE source_system='STATION_ADMIN_V1' AND source_event_id='"+req+"'").strip()!='0':raise ValueError('synthetic admin receipt remains')
        if logged_in:
            page=web('/admin')
            web('/admin',{'action':'logout','csrf':csrf(page[3].decode())})
        plan['cleaned']=True;(backup/'synthetic-cleanup.json').write_text(json.dumps(plan))
    result['syntheticCleanupVerified']=True
    return result
