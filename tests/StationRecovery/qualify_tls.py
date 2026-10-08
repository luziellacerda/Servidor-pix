#!/usr/bin/env python3
"""Real loopback TLS v2 qualification, using the production socket feature path.

Compare sync-client and decorated-fixture controls separately. Uses synthetic identities/keys only. Never reads a real ROM or production database.
The harness keeps its original mode for historical lab/drop consumers; the optional
decorated control records a failure separately and never changes server logging.
Requires cryptography and websockets 15. Credentials stay in a private temporary file.
"""
import argparse
import asyncio
import base64
import hashlib
import json
import os
from pathlib import Path
import secrets
import signal
import ssl
import struct
import subprocess
import time
import uuid
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from websockets.asyncio.client import connect
from websockets.sync.client import connect as sync_connect


async def qualify(fixture, synchronous_client=False):
    values=dict(line.split('=',1) for line in fixture.read_text().splitlines())
    certificate=base64.b64decode(values['cert'])
    context=ssl.create_default_context(cadata=ssl.DER_cert_to_PEM_cert(certificate))
    server=serialization.load_der_public_key(base64.urlsafe_b64decode(values['responseSPKI']+'==='))
    keys={role:serialization.load_der_private_key(base64.b64decode(values[role+'.key']),None) for role in ('host','client')}
    checks=0;phase='begin';sockets=[]
    def b64(data):return base64.urlsafe_b64encode(data).rstrip(b'=').decode()
    def check(ok,label):
        nonlocal checks
        checks+=1
        if not ok:raise ValueError(label)
    def proof(role,credential,method,path,body=b''):
        stamp=int(time.time());nonce=b64(secrets.token_bytes(16))
        canonical=('TurboRamaStationAndroid/request/v1\n'+method+'\n'+path+'\n'+hashlib.sha256(body).hexdigest()+'\n'+hashlib.sha256(credential.encode()).hexdigest()+'\n'+str(stamp)+'\n'+nonce+'\n').encode()
        signature=keys[role].sign(canonical,padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        return 'v1.'+str(stamp)+'.'+nonce+'.'+b64(signature)
    def command(role,action,**more):
        body=json.dumps(dict(action=action,requestId=str(uuid.uuid4()),**more),separators=(',',':')).encode()
        route='/v1/station/online/command';credential=values[role+'.token']
        with urlopen(Request(values['url']+route,body,headers={'Authorization':'Bearer '+credential,'Content-Type':'application/json','X-Station-Request-Proof':proof(role,credential,'POST',route,body)}),context=context,timeout=5) as response:
            envelope=json.load(response)
        payload=base64.urlsafe_b64decode(envelope['payload']+'===')
        server.verify(base64.urlsafe_b64decode(envelope['signature']+'==='),payload,padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        value=json.loads(payload);check(value['licenseId']==values[role+'.license'],'signed identity');return value['snapshot']
    async def websocket(role,action='relay-ticket'):
        nonlocal phase
        phase=role+'-ticket'
        snapshot=command(role,action,roomId=values['room'],generation=int(values['generation']),engineId=values['engine'],contentSha256=values['hash'],optionsSha256=values['hash'],coreSha256=values['hash'],runtimeSha256=values['hash'],recoveryProtocol='station-stream.v2')
        ticket=snapshot['room']['relay']['ticket'];route='/v1/station/online/relay';phase=role+'-upgrade'
        connector=sync_connect if synchronous_client else connect
        pending=connector(values['url'].replace('https:','wss:')+route,ssl=context,subprotocols=['station-stream.v2'],additional_headers={'Authorization':'StationRelay '+ticket,'X-Station-Request-Proof':proof(role,ticket,'GET',route)},compression=None,proxy=None,open_timeout=5,close_timeout=2)
        raw=pending if synchronous_client else await pending
        class Socket:
            subprotocol=raw.subprotocol
            def certificate(self):return raw.socket.getpeercert(binary_form=True) if synchronous_client else raw.transport.get_extra_info('ssl_object').getpeercert(binary_form=True)
            async def send(self,data):
                if synchronous_client:raw.send(data)
                else:await raw.send(data)
            async def recv(self):
                if synchronous_client:return raw.recv(timeout=5)
                return await raw.recv()
            async def close(self):
                if synchronous_client:raw.close()
                else:await raw.close()
            @property
            def close_code(self):return raw.close_code
        ws=Socket()
        sockets.append(ws);check(ws.subprotocol=='station-stream.v2','negotiated v2');check(ws.certificate()==certificate,'exact synthetic certificate');return ws
    def frame(kind,offset=0,value=0,data=b''):return b'TSR2'+bytes([kind,0,0,0])+struct.pack('>qq',offset,value)+data
    async def receive(ws,kind,value=None):
        deadline=time.monotonic()+5
        for _ in range(100):
            data=await asyncio.wait_for(ws.recv(),timeout=max(.01,deadline-time.monotonic()))
            check(isinstance(data,bytes) and len(data)>=24 and data[:4]==b'TSR2','binary frame')
            offset,number=struct.unpack('>qq',data[8:24])
            if data[4]==kind and (value is None or value==number):return offset,number,data[24:]
            if time.monotonic()>deadline:break
        raise TimeoutError('bounded frame wait')
    def diagnostics(headers=None):
        with urlopen(Request(values['url']+'/ready/station/online/diagnostics',headers=headers or {}),context=context,timeout=5) as response:return json.load(response)
    try:
        host=await websocket('host');guest=await websocket('client');phase='hello-and-playing'
        for ws in (host,guest):await ws.send(frame(1));await receive(ws,2)
        for ws in (host,guest):await ws.send(frame(7,1));await ws.send(frame(8,1))
        for ws in (host,guest):await receive(ws,6,2)
        payload=secrets.token_bytes(2048);await host.send(frame(3,data=payload));check((await receive(guest,3))[2]==payload,'exact DATA');await guest.send(frame(4,len(payload)));await receive(host,5,len(payload))
        await guest.send(frame(9,99));check((await receive(guest,10))[0]==99,'PONG')
        phase='normal-close';await guest.close();check(guest.close_code==1000,'normal Close1000 round trip')
        deadline=time.monotonic()+2
        while True:
            diagnostic=diagnostics();ends=[x['termination'] for x in diagnostic['recentTerminations']]
            if ends or time.monotonic()>deadline:break
            time.sleep(.02)
        check(len(ends)==1,'one physical termination');end=ends[0]
        check(end['epochAtFirstCause']==1 and end['epochAfterDetach']==2 and end['stateAtFirstCause']==2 and end['gracefulClose'],'first cause before detach')
        check(diagnostic['active'][0]['stream']['hostPending']==0,'ACK drains pending')
        guest=await websocket('client','resume-relay');await guest.send(frame(1,0,len(payload)));await receive(guest,2)
        check(command('host','heartbeat')['room']['generation']==int(values['generation']),'same logical generation on resume')
        phase='resume-playing'
        for ws in (host,guest):await ws.send(frame(7,2));await ws.send(frame(8,2,len(payload) if ws==host else 0))
        for ws in (host,guest):await receive(ws,6,2)
        resumed=secrets.token_bytes(64);await host.send(frame(3,len(payload),data=resumed));received=await receive(guest,3)
        check(received[0]==len(payload) and received[2]==resumed,'resumed offset and exact bytes');await guest.send(frame(4,len(payload)+len(resumed)));await receive(host,5,len(payload)+len(resumed))
        reverse=secrets.token_bytes(64);await guest.send(frame(3,data=reverse));received=await receive(host,3)
        check(received[0]==0 and received[2]==reverse,'reverse exact bytes');await host.send(frame(4,len(reverse)));await receive(guest,5,len(reverse))
        phase='operator-guards'
        for headers in ({'Host':'app.lzgames.com.br'},{'Forwarded':'for=192.0.2.1'},{'X-Forwarded-For':'192.0.2.1'},{'CF-Connecting-IP':'192.0.2.1'}):
            try:diagnostics(headers);raise ValueError('operator route exposed')
            except HTTPError as error:check(error.code==404,'forward/public host guard')
        command('host','leave');phase='completed'
        return dict(passed=True,checks=checks,exactForwardedBytes=len(payload)+len(resumed)+len(reverse),normalClose=True,firstCauseBeforeDetach=True,authorizedResume=True,resumedPlaying=True)
    except Exception as error:
        return dict(passed=False,checks=checks,phase=phase,errorType=type(error).__name__,detailsSuppressed=True)
    finally:
        for ws in sockets:
            try:await ws.close()
            except Exception:pass


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--harness',type=Path,required=True)
    parser.add_argument('--work-dir',type=Path,required=True)
    parser.add_argument('--iterations',type=int,default=3)
    parser.add_argument('--decorated-control',action='store_true')
    parser.add_argument('--sync-client-control',action='store_true')
    args=parser.parse_args();os.umask(0o077)
    if not args.harness.is_absolute() or not args.harness.is_file() or not args.work_dir.is_absolute() or args.work_dir.exists() or not 1<=args.iterations<=10:parser.error('absolute harness, fresh private work directory and bounded iterations required')
    args.work_dir.mkdir(mode=0o700);results=[]
    for trial in range(args.iterations):
        fixture=args.work_dir/f'trial-{trial}.env';log=args.work_dir/f'trial-{trial}.log'
        with log.open('xb') as output:
            command=['dotnet',str(args.harness),str(fixture)]
            if not args.decorated_control:command.append('--without-test-socket-decoration')
            process=subprocess.Popen(command,stdout=output,stderr=output,start_new_session=True)
            try:
                deadline=time.monotonic()+20
                while not fixture.exists():
                    if process.poll() is not None:raise RuntimeError('harness exit')
                    if time.monotonic()>deadline:raise TimeoutError('harness startup')
                    time.sleep(.05)
                results.append(asyncio.run(qualify(fixture,args.sync_client_control)))
            except Exception as error:results.append(dict(passed=False,phase='startup',errorType=type(error).__name__,detailsSuppressed=True))
            finally:
                if process.poll() is None:process.send_signal(signal.SIGTERM)
                try:process.wait(timeout=8)
                except subprocess.TimeoutExpired:os.killpg(process.pid,signal.SIGKILL);process.wait(timeout=5)
                fixture.unlink(missing_ok=True)
    receipt=dict(client='websockets.sync' if args.sync_client_control else 'websockets.asyncio',passed=all(r['passed'] for r in results),loggingEnabled=False,testSocketDecoration=args.decorated_control,harnessSocketFeatureReplaced=args.decorated_control,iterations=len(results),results=results,syntheticOnly=True,productionChanged=False,androidGameplayQualified=False)
    (args.work_dir/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(json.dumps(receipt),flush=True);return 0 if receipt['passed'] else 1

if __name__=='__main__':raise SystemExit(main())
