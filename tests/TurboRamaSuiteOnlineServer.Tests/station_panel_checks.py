"""Exercise Station management with real roles in station_http_smoke's temporary cluster."""
import base64,hashlib,json,os,socket,subprocess,time,uuid
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.error import HTTPError
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from station_http_smoke import ROOT,PRODUCT,PREFIX,b64,proof,request,signed_payload,assert_error,sql_value


def port():
    with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]

def run(base,folder,api_env,public_key,dll,admin_env,internal):
    assert os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.')
    assert sql_value("SELECT current_setting('data_directory')").startswith('/tmp/pg_virtualenv.')
    assert sql_value("SELECT has_table_privilege('turborama-suite-admin','suite.suite_audit_events','SELECT')")=='f'
    assert sql_value("SELECT has_table_privilege('turborama-suite','suite.station_management_audit','SELECT')")=='f'
    assert sql_value("SET ROLE \"turborama-suite-admin\"; SELECT count(*) FROM suite.station_management_audit a JOIN suite.suite_licenses l USING(license_id) WHERE l.product_id<>'TURBORAMA_STATION_ANDROID'").splitlines()[-1]=='0'
    def create(name='Cliente de demonstração'):
        e=dict(sourceSystem='TURBOBOX_V1',sourceEventId=uuid.uuid4().hex,sourcePurchaseId='panel-'+uuid.uuid4().hex,
          sourceItemKey='station',sourceVersion=1,sourceProductSku='STATION_ANDROID_LIFETIME_1_DEVICE',
          eventType='PURCHASE_PAID',amountCents=9990,currency='BRL',customerRef='fixture-panel',displayName=name)
        keys=['sourceSystem','sourceEventId','sourcePurchaseId','sourceItemKey','sourceVersion','sourceProductSku','eventType','amountCents','currency','customerRef','displayName']
        e['payloadDigest']=hashlib.sha256('\n'.join(str(e[k]) for k in keys).encode()).hexdigest()
        response=internal('POST','/commerce/station/events',e)
        assert response[0]==200,'fixture commerce failed'
        return json.loads(response[2])['licenseId']
    license_id=create()
    private=folder/'panel';private.mkdir(mode=0o700)
    socket_path=private/'admin.sock'
    env=admin_env.copy();env.update(SUITE_ADMIN_SOCKET=str(socket_path),STATION_MANAGEMENT_ONLY='1')
    bridge_token=private/'bridge.token';bridge_token.write_text(base64.b64encode(os.urandom(32)).decode());bridge_token.chmod(0o600)
    bridge_env=api_env.copy();bridge_env.update(STATION_ISSUE_PORT=str(port()),STATION_ISSUE_TOKEN_FILE=str(bridge_token),
      STATION_ISSUE_PEPPER_FILE=api_env['Station__ActivationPepperFile'],STATION_MANAGEMENT_SOCKET=str(socket_path),
      STATION_MANAGEMENT_TOKEN_FILE=admin_env['SUITE_ADMIN_TOKEN_FILE'])
    processes=[];logs=[]
    def process(args,environment,name):
        log=(private/name).open('wb');logs.append(log)
        p=subprocess.Popen(args,env=environment,stdout=log,stderr=subprocess.STDOUT);processes.append(p);return p
    process(['dotnet',dll],env,'admin.log')
    process(['python3',str(ROOT/'ops/station-admin/station-issue-admin.py')],bridge_env,'bridge.log')
    bridge='http://127.0.0.1:'+bridge_env['STATION_ISSUE_PORT']
    token=bridge_token.read_text()
    def call(method,path,body=None,auth=True):
        headers={'Content-Type':'application/json'}
        if auth:headers['X-Station-Admin-Token']=token
        r=Request(bridge+path,data=None if body is None else json.dumps(body).encode(),headers=headers,method=method)
        try:
            with urlopen(r,timeout=15) as resp:return resp.status,json.loads(resp.read())
        except HTTPError as e:return e.code,json.loads(e.read())
    def support():
        status,data=call('GET','/management/licenses/'+license_id+'/support');assert status==200,'support unavailable';return data
    def action(name,data=None):
        row=support()['license']
        payload=dict(actor='fixture-admin',requestId=uuid.uuid4().hex,expectedGeneration=row['revocationGeneration'],
          expectedActivationGeneration=row['activationGeneration'],reason='Synthetic management test with temporary data',
          stepUpAt=int(time.time()),clientIpDigest='a'*64,csrfVerified=True,targetSessionId=row.get('sessionId') or None)
        if data:payload.update(data)
        return call('POST','/management/licenses/'+license_id+'/'+name,payload),payload
    def device():
        key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
        spki=key.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)
        who=dict(schemaVersion=1,productId=PRODUCT,applicationId=PRODUCT,deviceId=b64(hashlib.sha256(spki).digest()),
                 clientVersion='panel-fixture',deviceManufacturer='Synthetic',deviceModel='Android Fixture',androidSdk=35)
        return key,spki,who
    def activate(who,code):
        key,spki,identity=who
        c=signed_payload(request(base,'POST','/v1/station/activations/challenge',dict(identity,
          domain=PREFIX+'request-activation-challenge/v1',activationCode=code,devicePublicKey=b64(spki))),public_key,'activation-challenge')
        result=signed_payload(request(base,'POST','/v1/station/activations/complete',proof(key,dict(identity,
          domain=PREFIX+'activate/v1',activationCode=code,devicePublicKey=b64(spki),challengeId=c['challengeId'],nonce=c['nonce']))),public_key,'activated')
        assert result['licenseId']==license_id
    def session(who):
        c=signed_payload(request(base,'POST','/v1/station/challenges',dict(who[2],domain=PREFIX+'request-session-challenge/v1',licenseId=license_id)),public_key,'session-challenge')
        return signed_payload(request(base,'POST','/v1/station/sessions',proof(who[0],dict(who[2],domain=PREFIX+'open-session/v1',licenseId=license_id,challengeId=c['challengeId'],nonce=c['nonce']))),public_key,'session')
    try:
        for _ in range(100):
            try:
                if call('GET','/management/licenses?limit=1')[0]==200:break
            except OSError:pass
            time.sleep(.1)
        else:raise RuntimeError('temporary Station management not ready')
        assert call('GET','/management/licenses',auth=False)[0]==401
        # Private Station instance rejects commerce even with valid internal credentials.
        import http.client
        class Unix(http.client.HTTPConnection):
            def connect(self):self.sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM);self.sock.connect(str(socket_path))
        c=Unix('localhost');c.request('POST','/commerce/station/events','{}',{'Content-Type':'application/json','X-Suite-Commerce-Token':Path(env['SUITE_COMMERCE_TOKEN_FILE']).read_text()});assert c.getresponse().status==404;c.close()
        assert action('issue-code',{'csrfVerified':False})[0][0]==400
        assert action('issue-code',{'stepUpAt':0})[0][0]==400
        first,payload=action('issue-code');assert first[0]==200,'issue failed';assert first[1]['ttlMinutes']==30
        secret=first[1]['activationCode']
        repeat=call('POST','/management/licenses/'+license_id+'/issue-code',payload);assert repeat==(409,{'code':'STATION_REQUEST_ALREADY_COMPLETED'})
        assert action('issue-code',{'expectedActivationGeneration':0})[0][0]==409
        assert action('cancel-code')[0][0]==200
        one=device()
        assert_error(request(base,'POST','/v1/station/activations/challenge',dict(one[2],domain=PREFIX+'request-activation-challenge/v1',activationCode=secret,devicePublicKey=b64(one[1]))),403,'STATION_ACTIVATION_INVALID')
        purchased=call('POST','/licenses/'+license_id+'/issue-purchase',{'actor':'fixture-commerce','requestId':uuid.uuid4().hex})
        assert purchased[0]==200 and purchased[1]['ttlMinutes']==2880,'legacy purchase issue regressed'
        activate(one,purchased[1]['activationCode']);old_session=session(one)
        assert call('POST','/licenses/'+license_id+'/issue-code',{'actor':'fixture-operator','requestId':uuid.uuid4().hex})[0]==409
        recovered,recovery_payload=action('new-device');assert recovered[0]==200 and recovered[1]['transferCompleted'] and recovered[1]['ttlMinutes']==30
        assert_error(request(base,'GET','/v1/station/me',bearer=old_session['accessToken']),401,'STATION_SESSION_INVALID')
        assert_error(request(base,'POST','/v1/station/challenges',dict(one[2],domain=PREFIX+'request-session-challenge/v1',licenseId=license_id)),403,'STATION_DEVICE_DENIED')
        two=device();activate(two,recovered[1]['activationCode']);current=session(two)
        repeated=call('POST','/management/licenses/'+license_id+'/new-device',recovery_payload);assert repeated[0]==409
        assert request(base,'GET','/v1/station/me',bearer=current['accessToken'])[0]==200,'replay changed the new binding'
        assert action('block')[0][0]==200
        assert_error(request(base,'GET','/v1/station/me',bearer=current['accessToken']),401,'STATION_SESSION_INVALID')
        assert action('unblock')[0][0]==200;current=session(two)
        assert action('revoke-session')[0][0]==200
        assert_error(request(base,'GET','/v1/station/me',bearer=current['accessToken']),401,'STATION_SESSION_INVALID')
        assert session(two)['licenseId']==license_id,'saved key cannot reconnect'
        data=support();assert len(data['devices'])==2 and len(data['history'])>=6
        assert any(e['event']=='STATION_CODE_ISSUED' and e['reason']=='Synthetic management test with temporary data' for e in data['history'])
        history=json.dumps(data['history']);assert all(x not in history for x in [secret,recovered[1]['activationCode'],purchased[1]['activationCode']])
        matched=call('GET','/management/licenses?q='+license_id+'&limit=1');assert matched[0]==200 and matched[1]['total']==1
        assert call('GET','/management/licenses?q='+license_id+'&limit=1&offset=1')[1]['licenses']==[]
        assert call('GET','/management/licenses?limit=101')[0]==400
        from station_registration_checks import run as registrations
        registrations(base,public_key,call,internal)
        for i in range(23):create('Cliente de demonstração '+str(i+1).zfill(2))
        browser_id=create('Atendimento de demonstração')
        browser_checks(private,bridge,bridge_token,browser_id,license_id,process)
        from station_registration_checks import activate as activate_registration
        for item in json.loads((private/'registration-secrets.json').read_text()):
            activate_registration(base,public_key,item['licenseId'],item['code'],item['name'])
        (private/'registration-secrets.json').unlink()
        print('STATION PANEL: OK (private scope, code replay/stale checks, cancellation, purchase48h, same-license recovery, old-key/session denial, block/unblock/reconnect, history, pagination, PHP/browser gates)')
    finally:
        for p in reversed(processes):
            p.terminate()
            try:p.wait(timeout=5)
            except subprocess.TimeoutExpired:p.kill();p.wait()
        for log in logs:log.close()


def browser_checks(private,bridge,token_file,license_id,bound_license_id,process):
    import importlib.util,shutil
    site=private/'site';site.mkdir()
    for p in (ROOT/'ops/station-admin/site').iterdir():shutil.copy2(p,site/p.name)
    deployed=Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902')
    for name in ['lib.php','notification-lib.php','payments.css','router.php','login.php',
                 'admin.php','admin-render.php','panel.php','panel.css','panel.js','admin-clean.css']:
        shutil.copy2(deployed/name,site/name)
    spec=importlib.util.spec_from_file_location('station_codes_deploy',ROOT/'ops/station-admin/deploy-codes-panel.py')
    deploy=importlib.util.module_from_spec(spec);spec.loader.exec_module(deploy)
    manifest=json.loads((ROOT/'ops/station-admin/codes-panel-20261006.json').read_text())
    panel=site/'panel.php';panel.write_bytes(deploy.render_navigation('panel.php',panel.read_bytes(),manifest))
    (site/'fixture-router.php').write_text("""<?php
+if(parse_url($_SERVER['REQUEST_URI'],PHP_URL_PATH)==='/fixture-login'){
+require __DIR__.'/lib.php';$db=tb_db();$db->prepare(\"INSERT OR IGNORE INTO users(id,name,email,password_hash,role,status) VALUES(1,'Fixture admin','fixture@example.invalid',?,'admin','active')\")->execute([password_hash('fixture-password',PASSWORD_DEFAULT)]);
+$db->prepare(\"INSERT OR IGNORE INTO users(id,name,email,password_hash,role,status) VALUES(2,'Existing fixture customer','existing@example.invalid',?,'customer','active'),(3,'Archived fixture customer','archived@example.invalid',?,'customer','archived')\")->execute([password_hash('fixture-password',PASSWORD_DEFAULT),password_hash('fixture-password',PASSWORD_DEFAULT)]);
+tb_session();$_SESSION['user']=['id'=>1,'role'=>'admin','name'=>'Fixture admin','email'=>'fixture@example.invalid'];$_SESSION['auth_version']=1;
+header('Location: /admin/station');exit;}return require __DIR__.'/router.php';
+""".replace('\n+','\n'))
    env=os.environ.copy();env.update(TURBOBOX_STATION_ISSUE_URL=bridge,TURBOBOX_STATION_TOKEN_FILE=str(token_file))
    php_port=port();process(['php','-S','127.0.0.1:'+str(php_port),'-t',str(site),str(site/'fixture-router.php')],env,'php.log')
    url='http://127.0.0.1:'+str(php_port)
    for _ in range(60):
        try:
            with urlopen(url+'/admin/station',timeout=2) as r:pass
            break
        except (OSError,HTTPError):time.sleep(.1)
    chrome_port=port();profile=private/'chrome'
    process(['google-chrome','--headless=new','--no-sandbox','--disable-dev-shm-usage','--disable-gpu',
      '--remote-debugging-address=127.0.0.1','--remote-debugging-port='+str(chrome_port),'--user-data-dir='+str(profile),'about:blank'],os.environ.copy(),'chrome.log')
    command=['node',str(ROOT/'tests/TurboRamaSuiteOnlineServer.Tests/station_panel_browser.mjs'),str(chrome_port),url,license_id,str(private),bound_license_id]
    result=subprocess.run(command,capture_output=True,text=True,timeout=120)
    artifacts=Path(os.environ.get('STATION_PANEL_ARTIFACTS','/mnt/DADOS/station-admin-panel-validation-20261003'))
    artifacts.mkdir(mode=0o700,parents=True,exist_ok=True)
    for name in ['php.log','admin.log','bridge.log','chrome.log','station-panel-desktop.png','station-panel-mobile.png','station-panel-support.png',
                 'station-dashboard-desktop.png','station-dashboard-mobile.png','station-registration-mobile.png','browser-validation.json']:
        source=private/name
        if source.exists():shutil.copy2(source,artifacts/name)
    if result.returncode:raise RuntimeError('Station browser checks failed: '+result.stdout[-1200:]+result.stderr[-1200:])
    import sqlite3
    with sqlite3.connect(site/'.data/turbobox.sqlite') as database:
        assert database.execute("SELECT count(*) FROM users WHERE email='new-station@example.invalid'").fetchone()[0]==1
        assert database.execute("SELECT count(*) FROM station_registrations WHERE state='COMPLETED'").fetchone()[0]==3
        assert database.execute('SELECT count(*) FROM notification_jobs').fetchone()[0]==0
        assert database.execute('SELECT count(*) FROM purchases').fetchone()[0]==0
        assert database.execute('PRAGMA foreign_key_check').fetchall()==[]
    print(result.stdout.strip())
