#!/usr/bin/env python3
"""256 real WSS connections on the production Station route, synthetic accounts.

Fixtures are prebound in isolated, marker-owned database rows. This does not
benchmark enrollment or bypass the live session/ticket/signature checks. No
retained POCO/customer license is used. Tokens exist only in process memory.
Run after the bounded relay rollout with its venv, native root and --run.
"""
import asyncio
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import time
import uuid
from cryptography import x509
from cryptography.hazmat.primitives import serialization
from websockets.asyncio.client import connect

ROOT = Path(__file__).resolve().parents[3]
BASE = 'https://app.lzgames.com.br'
PAIR_COUNT = 128
FRAMES = 1200
RESULT = Path('/home/lz-servidor/station-relay-public-load-20261005.json')


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), Path(__file__).with_name(name))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    if os.geteuid() != 0 or sys.argv[1:] != ['--run'] or RESULT.exists():
        raise ValueError('Native root, --run and absent result required')
    ops = load('implantar-station-20261003.py')
    helper = load('verificar-http-release-station.py')
    values, _ = ops.runtime()
    if values.get('Station__Online__RelayEnabled') != 'true' or values.get('Station__Online__RelayMaxRooms') != '512':
        raise ValueError('Reviewed relay must already be published')
    db = ops.database_name(values)
    public = serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), password=None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_text().strip(), validate=True)
    index = json.loads(Path(values['Station__LibraryIndexFile']).read_text())
    sample = next(row for row in index['items'] if row['platform'] == 'snes' and row.get('catalogVisible', True))
    registry = json.loads(Path(values['Station__Online__EngineRegistryFile']).read_text())
    engine = next(row for row in registry if row['platform'] == 'snes')
    sql = lambda statement: ops.sql(db, statement).strip()
    marker = uuid.uuid4().hex
    clients = []
    created = []
    streams = []
    signed_commands = 0
    report = dict(passed=False,players=PAIR_COUNT*2,rooms=PAIR_COUNT,scope='Production HTTPS/WSS with temporary prebound synthetic accounts; enrollment and Android gameplay excluded')
    baseline = {unit: ops.state(unit) for unit in ops.SHARED}
    fingerprint_query = "SELECT md5(coalesce(string_agg(to_jsonb(l)::text,E'\\n' ORDER BY license_id),'')) FROM suite.suite_licenses l WHERE NOT EXISTS(SELECT 1 FROM suite.suite_license_deliveries d WHERE d.license_id=l.license_id AND d.source_system='STATION_RELAY_LOAD_TEST');"
    fingerprint = sql(fingerprint_query)
    stage = 'fixtures'
    started = time.monotonic()
    pid = int(ops.run(['systemctl','show',ops.SERVICE,'-p','MainPID','--value']).strip())
    cpu = lambda: sum(int(value) for value in Path('/proc/'+str(pid)+'/stat').read_text().split()[13:15]) / os.sysconf('SC_CLK_TCK')
    rss = lambda: int(next(line.split()[1] for line in Path('/proc/'+str(pid)+'/status').read_text().splitlines() if line.startswith('VmRSS:'))) * 1024
    cpu_before = cpu()
    memory_before = rss()
    try:
        # Batch <=16: keep the bounded psql -c argument below Linux's per-argument
        # size limit. The same marker protects every cleanup transaction.
        with ThreadPoolExecutor(max_workers=8) as pool:
            clients = list(pool.map(lambda _: helper.StationReleaseVerification(lambda q: sql(q), pepper, public, {}), range(PAIR_COUNT*2)))
        for offset in range(0,len(clients),16):
            statements = ["BEGIN; SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='30s';"]
            batch = clients[offset:offset+16]
            for client in batch:
                client.token = helper.b64(os.urandom(32))
                client.session = uuid.uuid4().hex + uuid.uuid4().hex
                token_digest = hashlib.sha256(client.token.encode('ascii')).hexdigest()
                lid, device, spki = client.license_id, client.identity['deviceId'], client.spki
                statements.append("INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices,provisioning_origin,enrollment_state,claim_mode) VALUES('"+lid+"','TURBORAMA_STATION_ANDROID','ACTIVE',true,'LIFETIME',NULL,'SOFTWARE_ONLY',1,'LEGACY_ADMIN','BOUND','FIRST_CLAIM');")
                statements.append("INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,source_product_sku,product_id,license_id,provisioning_state,financial_state,last_source_version) VALUES('STATION_RELAY_LOAD_TEST','"+marker+"','"+lid[-20:]+"','STATION_ANDROID_LIFETIME_1_DEVICE','TURBORAMA_STATION_ANDROID','"+lid+"','PROVISIONED','PAID',1);")
                statements.append("INSERT INTO suite.station_devices(license_id,device_id,public_key_spki,manufacturer,model,android_sdk,client_version,status) VALUES('"+lid+"','"+device+"','"+spki+"','Synthetic','Relay capacity test',35,'station-relay-load-20261005','ACTIVE');")
                statements.append("INSERT INTO suite.station_sessions(session_id,license_id,device_id,token_digest,revocation_generation,status,authorized_until) VALUES('"+client.session+"','"+lid+"','"+device+"','"+token_digest+"',0,'ACTIVE',clock_timestamp()+interval '180 seconds');")
            statements.append('COMMIT;')
            sql(''.join(statements))
            created.extend(batch)

        def command(client, action, **extra):
            nonlocal signed_commands
            body = dict(action=action,requestId=str(uuid.uuid4()),**extra)
            response = client.request(BASE,'POST','/v1/station/online/command',body,client.token)
            payload = client.signed(response,'online')
            if payload['requestId'] != body['requestId'] or payload['sessionId'] != client.session:
                raise ValueError('Signed load response binding failed')
            signed_commands += 1
            return payload['snapshot']

        descriptor = dict(itemId=sample['itemId'],engineId=engine['id'],contentSha256=sample['artifact']['sha256'],
            optionsSha256='a'*64,coreSha256=engine['coreSha256'],runtimeSha256=engine['runtimeSha256'])
        hashes = {key:value for key,value in descriptor.items() if key.endswith('Sha256')}
        def prepare(pair):
            host, guest = clients[pair*2:pair*2+2]
            command(host,'enter',nickname='Load host '+str(pair))
            command(guest,'enter',nickname='Load guest '+str(pair))
            room = command(host,'create',**descriptor)['room']['roomId']
            command(guest,'join',roomId=room,**hashes)
            command(host,'ready',roomId=room,value=True)
            command(guest,'ready',roomId=room,value=True)
            command(host,'start',roomId=room,transport='relay-wss-v1')
            ht = command(host,'relay-ticket',roomId=room)['room']['relay']['ticket']
            command(host,'host-listening',roomId=room)
            gt = command(guest,'relay-ticket',roomId=room)['room']['relay']['ticket']
            return ht, gt
        stage = 'signed_rooms'
        with ThreadPoolExecutor(max_workers=16) as pool:
            tickets = list(pool.map(prepare,range(PAIR_COUNT)))
        stage = 'public_load'

        async def exercise():
            semaphore = asyncio.Semaphore(32)
            async def open_socket(ticket):
                async with semaphore:
                    stream = await connect(BASE.replace('https:','wss:')+'/v1/station/online/relay',
                        subprotocols=['station-relay.v1'],additional_headers={'Authorization':'StationRelay '+ticket},
                        user_agent_header='Dalvik/2.1.0 Station synthetic capacity check',compression=None,proxy=None,
                        open_timeout=20,close_timeout=2,max_size=65536,max_queue=16)
                    streams.append(stream)
                    ssl = stream.transport.get_extra_info('ssl_object')
                    certificate = x509.load_der_x509_certificate(ssl.getpeercert(binary_form=True))
                    actual = hashlib.sha256(certificate.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
                    if actual != ops.PIN or stream.subprotocol != 'station-relay.v1':
                        raise ValueError('Production load TLS pin or protocol changed')
                    return stream
            pair_streams = await asyncio.gather(*(asyncio.gather(open_socket(host),open_socket(guest)) for host,guest in tickets))
            timings = []
            peak_memory = rss()
            healthy = True
            active = True
            async def monitor():
                nonlocal peak_memory,healthy
                while active:
                    peak_memory = max(peak_memory,rss())
                    try:
                        await asyncio.to_thread(ops.ready,'http://127.0.0.1:5192')
                    except Exception:
                        healthy=False
                    await asyncio.sleep(2)
            monitor_task = asyncio.create_task(monitor())
            duration = time.monotonic()
            async def exchange(pair_index, sockets):
                host,guest=sockets
                origin=time.monotonic()
                async def sender():
                    for frame in range(FRAMES):
                        await host.send(struct.pack('!dII16s',time.monotonic(),pair_index,frame,bytes(16)))
                        delay=origin+(frame+1)/60-time.monotonic()
                        if delay>0:
                            await asyncio.sleep(delay)
                async def echo():
                    for frame in range(FRAMES):
                        payload=await asyncio.wait_for(guest.recv(),15)
                        if not isinstance(payload,bytes) or len(payload)!=32 or struct.unpack('!dII16s',payload)[1:3]!=(pair_index,frame):
                            raise ValueError('Dropped or corrupt forward load packet')
                        await guest.send(payload)
                async def receiver():
                    for frame in range(FRAMES):
                        payload=await asyncio.wait_for(host.recv(),15)
                        stamp,pair_number,sequence,padding=struct.unpack('!dII16s',payload)
                        if pair_number!=pair_index or sequence!=frame or padding!=bytes(16):
                            raise ValueError('Dropped or corrupt reverse load packet')
                        timings.append((time.monotonic()-stamp)*1000)
                await asyncio.gather(sender(),echo(),receiver())
            try:
                with ThreadPoolExecutor(max_workers=16) as pool:
                    def heartbeat():
                        list(pool.map(lambda client:command(client,'heartbeat'),clients))
                    # Refresh before the 20s data phase; ordinary licensed heartbeats
                    # also exercise PostgreSQL and signed snapshot generation.
                    await asyncio.to_thread(heartbeat)
                    await asyncio.wait_for(asyncio.gather(*(exchange(pair,sockets) for pair,sockets in enumerate(pair_streams))),75)
            finally:
                active=False
                monitor_task.cancel()
                try:await monitor_task
                except asyncio.CancelledError:pass
                await asyncio.gather(*(stream.close() for stream in streams),return_exceptions=True)
            timings.sort()
            quantile=lambda p:timings[min(len(timings)-1,int(len(timings)*p))]
            return dict(publicConnections=len(streams),framesPerSecondTarget=60,framesPerPair=FRAMES,
                orderedRoundTrips=len(timings),payloadBytes=len(timings)*32*2,loadSeconds=time.monotonic()-duration,
                roundTripP50Ms=quantile(.50),roundTripP95Ms=quantile(.95),roundTripP99Ms=quantile(.99),
                droppedOrCorruptPackets=0,stationReadyDuringLoad=healthy,apiMemoryBefore=memory_before,apiPeakMemory=peak_memory)

        report.update(asyncio.run(exercise()))
        report['passed']=True
    except Exception as error:
        report.update(failedStage=stage,errorType=type(error).__name__)
        if isinstance(error,ValueError):report['safeReason']=str(error)
    finally:
        # Remove online presences using the existing command before marker-owned
        # database cleanup. No code, device or session of a customer is touched.
        with ThreadPoolExecutor(max_workers=16) as pool:
            def offline(client):
                try:client.request(BASE,'POST','/v1/station/online/command',dict(action='offline',requestId=str(uuid.uuid4())),client.token)
                except Exception:pass
            list(pool.map(offline,created))
        if created:
            ids=','.join("'"+client.license_id+"'" for client in created)
            guard="DO $$BEGIN IF (SELECT count(*) FROM suite.suite_license_deliveries WHERE license_id IN ("+ids+") AND source_system='STATION_RELAY_LOAD_TEST' AND source_purchase_id='"+marker+"')<>"+str(len(created))+" THEN RAISE EXCEPTION 'Synthetic load ownership changed'; END IF; END$$;"
            statements=['BEGIN;',guard]
            for table in ('station_download_grants','station_sessions','station_challenges','station_customer_projection','station_devices','suite_license_deliveries','suite_licenses'):
                statements.append('DELETE FROM suite.'+table+' WHERE license_id IN ('+ids+');')
            statements.append('COMMIT;')
            sql(''.join(statements))
        report.update(syntheticAccountsRemoved=len(created),syntheticRowsRemaining=int(sql("SELECT count(*) FROM suite.suite_license_deliveries WHERE source_system='STATION_RELAY_LOAD_TEST' AND source_purchase_id='"+marker+"';")),
            existingLicensesUnchanged=sql(fingerprint_query)==fingerprint,sharedServicesPreserved=all(ops.state(unit)==state for unit,state in baseline.items()),
            signedCommands=signed_commands,totalSeconds=time.monotonic()-started,apiCpuSeconds=cpu()-cpu_before,
            apiMemoryAfter=rss(),androidGameplay=False,pocoLicenseUsed=False)
        ops.private_text(RESULT,json.dumps(report,ensure_ascii=False,indent=2)+'\n')
        import pwd
        owner=pwd.getpwnam('lz-servidor');os.chown(RESULT,owner.pw_uid,owner.pw_gid)
        print(json.dumps(report,ensure_ascii=False,indent=2),flush=True)
    if not report['passed'] or report['syntheticRowsRemaining'] or not report['existingLicensesUnchanged'] or not report['sharedServicesPreserved']:
        raise SystemExit(1)


if __name__=='__main__':
    main()
