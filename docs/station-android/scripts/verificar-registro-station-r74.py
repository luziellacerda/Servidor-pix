"""Bounded R74/v1 live checks with two disposable licenses; never logs credentials."""
import base64, hashlib, importlib.util, json, secrets, struct, time, uuid
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request
from cryptography import x509
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import padding
from websockets.sync.client import connect
from websockets.exceptions import InvalidStatus

def load(name):
    spec=importlib.util.spec_from_file_location(name.replace('-','_'),Path(__file__).with_name(name))
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def verify(index,values,base,execute_sql,additions,old_ids,observe=None):
    helper=load('verificar-http-release-station.py');checks=0;clients=[];sockets=[];sessions={};snapshots={}
    public=serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(),None).public_key()
    pepper=base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes(),validate=True)
    def check(ok,label):
        nonlocal checks
        checks+=1
        if not ok:raise ValueError('R74 registry gate: '+label)
    def proof(client,credential,method,path,encoded=b''):
        nonce=helper.b64(secrets.token_bytes(16));stamp=int(time.time())
        canonical=('TurboRamaStationAndroid/request/v1\n'+method+'\n'+path+'\n'+hashlib.sha256(encoded).hexdigest()+'\n'+hashlib.sha256(credential.encode()).hexdigest()+'\n'+str(stamp)+'\n'+nonce+'\n').encode()
        sig=client.device.sign(canonical,padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        return 'v1.'+str(stamp)+'.'+nonce+'.'+helper.b64(sig)
    def request(client,method,path,body=None,credential=None,header=None):
        correlation=uuid.uuid4().hex;encoded=None if body is None else json.dumps(body,separators=(',',':')).encode()
        headers={'Accept':'application/json','X-Correlation-ID':correlation,'User-Agent':'Dalvik/2.1.0 Station R74 authorized qualification'}
        if body is not None:headers['Content-Type']='application/json'
        if credential:
            headers['Authorization']='Bearer '+credential
            headers['X-Station-Request-Proof']=header or proof(client,credential,method,path,encoded or b'')
        try:r=client.opener.open(Request(base+path,encoded,headers,method=method),timeout=20)
        except HTTPError as e:r=e
        with r:
            check(r.url==base+path and r.headers.get('X-Correlation-ID')==correlation,'redirect/correlation')
            data=r.read(16*1024*1024+1);check(len(data)<=16*1024*1024,'bounded HTTP response')
            return r.code,r.headers,data
    def signed(client,response,domain):return client.signed(response,domain)
    def authenticate(client,activation=True):
        if activation:
            client.create()
            ch=signed(client,request(client,'POST','/v1/station/activations/challenge',dict(client.identity,domain=helper.PREFIX+'request-activation-challenge/v1',activationCode=client.code,devicePublicKey=client.spki)),'activation-challenge')
            signed(client,request(client,'POST','/v1/station/activations/complete',client.proof(dict(client.identity,domain=helper.PREFIX+'activate/v1',activationCode=client.code,devicePublicKey=client.spki,challengeId=ch['challengeId'],nonce=ch['nonce'],requestProof='rsa-pss-v1'))),'activated')
        ch=signed(client,request(client,'POST','/v1/station/challenges',dict(client.identity,domain=helper.PREFIX+'request-session-challenge/v1',licenseId=client.license_id)),'session-challenge')
        result=signed(client,request(client,'POST','/v1/station/sessions',client.proof(dict(client.identity,domain=helper.PREFIX+'open-session/v1',licenseId=client.license_id,challengeId=ch['challengeId'],nonce=ch['nonce'],requestProof='rsa-pss-v1'))),'session')
        check(result.get('requestProof')=='rsa-pss-v1','protected authenticated session');sessions[id(client)]=result
    def token(client):return sessions[id(client)]['accessToken']
    def command(client,action,**extra):
        body=dict(action=action,requestId=str(uuid.uuid4()),**extra)
        result=signed(client,request(client,'POST','/v1/station/online/command',body,token(client)),'online')
        check(result['requestId']==body['requestId'] and result['sessionId']==sessions[id(client)]['sessionId'],'signed online binding')
        snapshots[id(client)]=result['snapshot'];return result['snapshot']
    uri=base.replace('https:','wss:').replace('http:','ws:')+'/v1/station/online/relay'
    def websocket(client,ticket,protocol,include_proof=True):
        headers={'Authorization':'StationRelay '+ticket}
        if include_proof:headers['X-Station-Request-Proof']=proof(client,ticket,'GET','/v1/station/online/relay')
        ws=connect(uri,subprotocols=[protocol],additional_headers=headers,compression=None,proxy=None,open_timeout=15,close_timeout=1,max_size=65536,max_queue=32,user_agent_header='Dalvik/2.1.0 Station R74 authorized qualification')
        sockets.append(ws);check(ws.subprotocol==protocol,'negotiated relay protocol')
        if uri.startswith('wss:'):
            cert=x509.load_der_x509_certificate(ws.socket.getpeercert(binary_form=True))
            pin=hashlib.sha256(cert.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
            check(pin=='13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7','unchanged public TLS pin')
        return ws
    def rejected(client,ticket,protocol,include_proof=True):
        try:ws=websocket(client,ticket,protocol,include_proof);ws.close();raise ValueError('Accepted copied/used relay ticket')
        except InvalidStatus as e:check(e.response.status_code==401,'copied/used relay ticket denied')
    def frame(kind,offset=0,value=0,data=b''):return b'TSR2'+bytes([kind,0,0,0])+struct.pack('>qq',offset,value)+data
    def receive(ws,kind,offset=None,value=None):
        until=time.monotonic()+15
        while time.monotonic()<until:
            data=ws.recv(timeout=max(.05,until-time.monotonic()))
            check(isinstance(data,bytes) and len(data)>=24 and data[:4]==b'TSR2','binary recovery header')
            n,at,v=data[4],*struct.unpack('>qq',data[8:24])
            if n==kind and (offset is None or offset==at) and (value is None or value==v):return at,v,data[24:]
        raise ValueError('Recovery frame not received')
    def paired(host,guest,engine,protocol):
        row=next(r for r in index['items'] if r.get('catalogVisible',True) and r['platform']==engine['platform'])
        fields=dict(contentSha256=row['artifact']['sha256'],optionsSha256='a'*64,coreSha256=engine['coreSha256'],runtimeSha256=engine['runtimeSha256'])
        if protocol=='station-stream.v2':fields['recoveryProtocol']=protocol
        room=command(host,'create',itemId=row['itemId'],engineId=engine['engineId'],**fields)['room'];rid=room['roomId']
        command(guest,'join',roomId=rid,**fields)
        for client in (host,guest):command(client,'ready',roomId=rid,value=True)
        room=command(host,'start',roomId=rid,transport='relay-wss-v2' if protocol=='station-stream.v2' else 'relay-wss-v1')['room']
        command(host,'host-listening',roomId=rid)
        for client in (host,guest):
            own=command(client,'heartbeat',page=40)['room']
            check(len(own['memberProfiles'])==2 and {p['peerId'] for p in own['memberProfiles']}==set(own['members']),'signed own-room profiles outside social page')
            check(len(own['ready'])==2,'both members ready')
        binding=dict(roomId=rid,engineId=engine['engineId'],generation=room['generation'],**fields)
        def ticket(client,action='relay-ticket'):
            extra=binding if protocol=='station-stream.v2' else dict(roomId=rid)
            descriptor=command(client,action,**extra)['room']['relay'];check(descriptor['protocol']==protocol,'signed relay descriptor')
            return descriptor['ticket']
        return rid,room['generation'],ticket
    completed=False;forwarded=0;latency=[];observations=None
    try:
        for _ in range(2):
            client=helper.StationReleaseVerification(execute_sql,pepper,public,index);clients.append(client);authenticate(client)
        host,guest=clients
        for client,name in [(host,'R74 synthetic host'),(guest,'R74 synthetic guest')]:command(client,'enter',nickname=name)
        state=snapshots[id(host)];registry={e['engineId']:e for e in state['engines']}
        check(set(registry)==set(old_ids)|{e['id'] for e in additions},'exact ten signed engine IDs')
        expected=json.loads(Path(values['Station__Online__EngineRegistryFile']).read_text())
        for engine in expected:
            published=registry[engine['id']]
            check(all(published.get(k)==engine.get(k) for k in ('platform','coreSha256','runtimeSha256','recoveryProtocol')),'all signed registry fields match exact private registry')
        check('station-stream.v2' in state['recoveryCapabilities'] and 'relay-wss-v2' in state['transports'],'signed v2 capability')
        check('relay-wss-v1' in state['transports'],'legacy relay transport preserved')
        for engine in additions:
            published=registry[engine['id']]
            check(all(published[k]==engine[k] for k in ('platform','coreSha256','runtimeSha256','recoveryProtocol')),'exact R74 Windows engine hashes')
        me='/v1/station/me';header=proof(host,token(host),'GET',me)
        signed(host,request(host,'GET',me,credential=token(host),header=header),'profile')
        replay=request(host,'GET',me,credential=token(host),header=header)
        check(replay[0]==409 and json.loads(replay[2]).get('code')=='STATION_REQUEST_PROOF_REPLAY','proof replay denied')
        path='/v1/station/catalog?metadata=1';catalog=signed(host,request(host,'GET',path,credential=token(host)),'catalog')
        check(catalog['revision']==index['revision'] and {r['itemId'] for r in catalog['items']}=={r['itemId'] for r in index['items'] if r.get('catalogVisible',True)},'full authenticated catalog preserved')
        row=min((r for r in index['items'] if r.get('catalogVisible',True) and r['platform']=='snes'),key=lambda r:r['artifact']['sizeBytes'])
        cover=request(host,'GET','/v1/station/covers/'+row['coverId'],credential=token(host))
        check(cover[0]==200 and cover[2]==Path(row['coverPath']).read_bytes(),'protected cover exact bytes')
        grant=signed(host,request(host,'POST','/v1/station/downloads/authorize',
            dict(host.identity,domain=helper.PREFIX+'request-download/v1',itemId=row['itemId']),token(host)),'download-grant')
        check(grant['artifact']==row['artifact'] and grant['sessionId']==sessions[id(host)]['sessionId'],'signed download grant binding')
        artifact=request(host,'GET','/v1/station/artifacts/'+grant['grantId'],credential=token(host))
        check(artifact[0]==200 and len(artifact[2])==row['artifact']['sizeBytes'] and
            hashlib.sha256(artifact[2]).hexdigest()==row['artifact']['sha256'],'real protected game download')
        engine=registry[additions[0]['id']];rid,generation,ticket=paired(host,guest,engine,'station-stream.v2')
        ht=ticket(host);rejected(host,ht,'station-stream.v2',False)
        hw=websocket(host,ht,'station-stream.v2');rejected(host,ht,'station-stream.v2')
        gw=websocket(guest,ticket(guest),'station-stream.v2')
        for ws in (hw,gw):ws.send(frame(1));receive(ws,2)
        epoch=1
        for ws in (hw,gw):ws.send(frame(7,epoch))
        for ws in (hw,gw):ws.send(frame(8,epoch))
        for ws in (hw,gw):receive(ws,6,value=2)
        data=secrets.token_bytes(64);hw.send(frame(3,data=data));check(receive(gw,3,offset=0)[2]==data,'exact host stream')
        gw.send(frame(4,64));receive(hw,5,offset=64,value=64);forwarded+=64
        reverse=secrets.token_bytes(64);gw.send(frame(3,data=reverse));check(receive(hw,3,offset=0)[2]==reverse,'exact guest stream')
        hw.send(frame(4,64));receive(gw,5,offset=64,value=64);forwarded+=64
        gw.close();time.sleep(.5)
        if observe is not None:check(gw.close_code==1000,'real WSS Close1000 response completed')
        preserved=command(host,'heartbeat')['room']
        check(preserved['roomId']==rid and preserved['generation']==generation and len(preserved['members'])==2,'room/generation survive physical guest detach')
        authenticate(guest,False)
        gw=websocket(guest,ticket(guest,'resume-relay'),'station-stream.v2');gw.send(frame(1,64,64));receive(gw,2)
        epoch=receive(hw,6,value=0)[0];check(epoch>1,'fresh recovery barrier after reconnect')
        for ws in (hw,gw):ws.send(frame(7,epoch))
        for ws in (hw,gw):ws.send(frame(8,epoch,64))
        for ws in (hw,gw):receive(ws,6,offset=epoch,value=2)
        resumed=secrets.token_bytes(64);hw.send(frame(3,64,data=resumed));check(receive(gw,3,offset=64)[2]==resumed,'resume continues offsets without duplication')
        gw.send(frame(4,128));receive(hw,5,offset=128,value=128);forwarded+=64
        if observe is not None:
            offsets={id(hw):128,id(gw):64}
            for sender,recipient,client,role in ((hw,gw,host,'host'),(gw,hw,guest,'client')):
                command(client,'heartbeat')
                for size in (64,2048,8192):
                    for sample in range(4):
                        payload=secrets.token_bytes(size);at=offsets[id(sender)];started=time.perf_counter_ns()
                        sender.send(frame(3,at,data=payload));check(receive(recipient,3,offset=at)[2]==payload,'measured DATA exact bytes')
                        latency.append(dict(role=role,kind='DATA',bytes=size,receiveMs=(time.perf_counter_ns()-started)/1e6))
                        recipient.send(frame(4,at+size));receive(sender,5,offset=at+size,value=at+size);offsets[id(sender)]+=size;forwarded+=size
                for sample in range(6):
                    started=time.perf_counter_ns();sender.send(frame(9,sample));receive(sender,10,offset=sample)
                    latency.append(dict(role=role,kind='PONG',bytes=0,roundTripMs=(time.perf_counter_ns()-started)/1e6))
            observations=observe()
            check(observations.get('version')==1 and observations.get('enabled') is True,'bounded local diagnostics available')
            active=observations.get('active',[])
            check(any(m['stream']['hostAccepted']==offsets[id(hw)] and m['stream']['clientAccepted']==offsets[id(gw)] for m in active),'measured stream matched by exact offsets')
            ends=[r['termination'] for r in observations.get('recentTerminations',[])]
            check(any(e.get('cause')=='PEER_CLOSE' and e.get('closeCode')==1000 and e.get('gracefulClose') is True and
                e.get('epochAtFirstCause')==1 and e.get('epochAfterDetach')==2 and e.get('stateAtFirstCause')==2 for e in ends),
                'actual Close classified before detach and stream retained')
        command(host,'leave');hw.close();gw.close()
        check(command(guest,'heartbeat')['room'] is None,'human exit still ends room')
        legacy=next(registry[eid] for eid in reversed(old_ids) if registry[eid].get('recoveryProtocol') is None and registry[eid]['platform']=='snes');rid,_,ticket=paired(host,guest,legacy,'station-relay.v1')
        hw=websocket(host,ticket(host),'station-relay.v1');gw=websocket(guest,ticket(guest),'station-relay.v1')
        legacy_data=secrets.token_bytes(64);hw.send(legacy_data);check(gw.recv(timeout=10)==legacy_data,'legacy byte stream preserved');forwarded+=64
        command(host,'leave');hw.close();gw.close();completed=True
        return dict(passed=True,checks=checks,syntheticLicenses=2,signedWindowsR74Engines=True,signedEngineIds=sorted(registry),signedEngines=list(registry.values()),signedTransports=state['transports'],signedRecoveryCapabilities=state['recoveryCapabilities'],signedRoomCapabilities=state['roomCapabilities'],legacyEngineIdsPreserved=True,
            exactForwardedBytes=forwarded,protectedV2Public=base.startswith('https:'),protectedResume=True,oneUseTickets=True,
            proofReplayDenied=True,ownRoomProfilesSigned=True,legacyV1=True,catalogCoverDownloadAuthorizationPreserved=True,gameDownloadVerified=True,
            androidGameplay=False,nativePauseSimulated=True,latencySamples=latency,observations=observations,
            latencyScope='Synthetic two clients on this server; not phone RTT or core FPS')
    finally:
        for ws in sockets:
            try:ws.close()
            except Exception:pass
        for client in clients:
            try:
                if id(client) in sessions:command(client,'offline')
            except Exception:pass
        cleaned=True
        for client in clients:
            try:cleaned=client.cleanup() and cleaned
            except Exception:cleaned=False
        if not cleaned:raise ValueError('R74 synthetic cleanup failed')
