"""Apply the proven Android controller fingerprint repair to the effective Station."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import time
import traceback
import urllib.request
import uuid

sys.dont_write_bytecode = True
W = Path(__file__).resolve().parent
DOCS = Path('/mnt/DADOS/servidor-pix-station-usb-salas-r157-20261014/docs/station-android')
FIX = DOCS/'r157/server-fingerprint-fix'
TOOLS = Path('/opt/turborama-station-library-quarantine-20261014-a10c78f53797/library-tools')
MODULE = TOOLS/'station_online_profiles.py'
SOURCE = DOCS/'online-plataformas-20261009/library-tools/station_online_profiles.py'
OLD_MODULE_SHA = '03100bd34af4eb4e16141b3dff25fe50991edfbfecf55a5b429d5f4f180dcc2a'
TIMER = 'turborama-station-library-scan.timer'
COMMAND = '/v1/station/online/multiplayer/command'
EID = 'mupen64plus-ae-33bf7021-n64-v3-fc7449c38d08'
ITEM = 'station_c16309c829ad81a41da0b4cb7236a4fb'


def need(value, reason):
    if not value:
        raise ValueError(reason)


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def read(path):
    return json.loads(Path(path).read_bytes())


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(name, data):
    path = W/name
    need(not path.exists(), 'Receipt exists; reconcile before retrying')
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n')
    path.chmod(0o600)
    os.chown(path, 1000, 1000)


def store_raw(name, raw):
    path = W/name
    need(not path.exists(), 'Private backup already exists')
    path.write_bytes(raw)
    path.chmod(0o600)
    os.chown(path, 1000, 1000)


def quiet():
    with urllib.request.urlopen('http://127.0.0.1:5192/ready/station/online/multiplayer-diagnostics', timeout=10) as response:
        d = json.load(response)
    need(d.get('activeRooms') == 0 and d.get('activeConnections') == 0,
         'An online room/connection is active; do not replace its binding')


def unit(name):
    r = subprocess.run(['systemctl','show',name,'-p','MainPID,ActiveState,NRestarts,ExecMainStartTimestamp'],
                       capture_output=True, text=True, check=True, timeout=15)
    return dict(line.split('=',1) for line in r.stdout.splitlines() if '=' in line)


def prove(index, expected):
    h = load('fingerprint_live_signed_probe', Path('/mnt/DADOS/station-checkup-20261010/checkup.py'))
    values,_ = h.b.ops.runtime()
    db = h.b.r71.database(values)
    real = h.b.security.real_licenses(db)
    results = []
    for label, address in (('local',h.b.LOCAL), ('public',h.b.PUBLIC)):
        probe = h.SignedProbe(index, values, address, lambda sql:h.b.ops.sql(db,sql))
        load('fingerprint_http_'+label,Path('/mnt/DADOS/station-r139-20261013/proof_http.py')).install(probe)
        entered = False
        room = None
        try:
            probe.start()
            probe.signed('POST','/v1/station/online/command','online',dict(
                action='enter',requestId=str(uuid.uuid4()),nickname='Station N64 hash'))
            entered = True
            def command(action, **fields):
                body = dict(action=action,requestId=str(uuid.uuid4()),clientMaximumPlayers=5,roomPageSize=32)
                body.update(fields)
                reply = probe.signed('POST',COMMAND,'online',body)
                need(reply['requestId']==body['requestId'] and reply['licenseId']==probe.client.license_id,
                     'Signed response identity differs')
                return reply['snapshot']
            deadline = time.monotonic()+35
            while True:
                view = command('capabilities',itemId=ITEM)
                rows = [p for p in view['profiles'] if p['engineId']==EID]
                if rows == expected or (len(rows)==len(expected) and all(p in rows for p in expected)):
                    break
                need(time.monotonic()<deadline, 'Corrected profiles did not reload')
                time.sleep(1)
            need(view['multiplayerVersion']==3 and view['capability']=='station-multiplayer.v3',
                 'Wrong multiplayer contract')
            wanted = next(p for p in rows if p['maximumPlayers']==4)
            binding = {k:wanted[k] for k in ('itemId','contentSha256','engineId','coreSha256',
                                            'runtimeSha256','profileId','profileSha256')}
            for capacity in (2,4):
                room = command('create',capacity=capacity,**binding)['room']
                need(room['profileSha256']==wanted['profileSha256'] and room['capacity']==capacity,
                     'Room admitted a different fingerprint/capacity')
                command('leave',roomId=room['roomId'],generation=room['generation'],**binding)
                room = None
            denied = []
            solo = next(p for p in rows if p['maximumPlayers']==1)
            for reason, fields in (
                ('old-incompatible-fingerprint',dict(binding,profileSha256='2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c')),
                ('wrong-runtime',dict(binding,runtimeSha256='0'*64)),
                ('solo', {k:solo[k] for k in binding})):
                body = dict(action='create',requestId=str(uuid.uuid4()),clientMaximumPlayers=5,capacity=2,**fields)
                status,_,raw = probe.request('POST',COMMAND,body)
                code = json.loads(raw).get('code')
                write('fingerprint-negative-'+label+'-'+reason+'-'+str(time.time_ns())+'.json',
                      dict(reason=reason,status=status,code=code))
                expected_code={'old-incompatible-fingerprint':'STATION_MULTIPLAYER_PROFILE_UNAPPROVED',
                               'wrong-runtime':'STATION_MULTIPLAYER_GAME_UNCLASSIFIED',
                               'solo':'STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED'}[reason]
                need(status==409 and code==expected_code,
                     'Negative response differed: '+reason+'/'+str(status)+'/'+str(code))
                denied.append(dict(reason=reason,status=status,code=code))
            write('fingerprint-'+label+'-007-profile-fixture.json',dict(
                multiplayerVersion=view['multiplayerVersion'],capability=view['capability'],profiles=rows))
            results.append(dict(route=label,signedIdentityVerified=True,exactProfiles=len(rows),
                                roomCapacitiesAdmitted=[2,4],unsafeAdmissionsRefused=denied))
        finally:
            if room:
                try: command('leave',roomId=room['roomId'],generation=room['generation'],**binding)
                except Exception: pass
            if entered:
                try: probe.request('POST','/v1/station/online/command',dict(action='offline',requestId=str(uuid.uuid4())))
                except Exception: pass
            need(probe.client.cleanup(), 'Own synthetic license cleanup failed')
    need(h.b.security.real_licenses(db)==real, 'Real licenses changed')
    return dict(passed=True,checks=results,ownSyntheticLicensesRemoved=True,realLicensesPreserved=True)


def main():
    need(os.geteuid()==0 and os.environ.get('PKEXEC_UID')=='1000',
         'Genuine native Linux administrative authentication required')
    os.umask(0o077)
    need(not (W/'fingerprint-published.json').exists(), 'Repair already published; reconcile instead of rerunning')
    seal = read(W/'fingerprint-preflight.json')
    need(seal['passed'] and all(sha(Path(p))==value for p,value in seal['sourceSha256'].items()),
         'Prepared source/test seal changed')
    need(read(W/'applied.json')['passed'], 'The exact R157 registration must already be applied')
    context = load('fingerprint_effective_context',DOCS/'scripts/quarentenar-wind-waker-station.py')
    operator = load('fingerprint_fixed_operator',SOURCE)
    repair = load('fingerprint_bounded_repair',FIX/'repair_n64_profile_hashes.py')
    config,pid,index_path,home = context.load_context()
    paths = dict(catalog=index_path, profiles=Path(config['autoOnlineProfiles']['registry']),
                 engines=Path(config['autoOnlineProfiles']['engineManifest']),
                 modes=Path(config['autoOnlineProfiles']['preparedModes']))
    values = dict(part.decode().split('=',1) for part in Path('/proc/'+str(pid)+'/environ').read_bytes().split(b'\0') if b'=' in part)
    need(Path(values['Station__Online__MultiplayerProfileRegistryFile'])==paths['profiles'],
         'The importer registry is not the effective API registry')
    need(sha(MODULE)==OLD_MODULE_SHA, 'Effective builder differs; obtain a fresh baseline')
    quiet()
    units = {name:unit(name) for name in ('turborama-station-api.service','nginx.service',
        'turborama-station-management.service','turborama-suite-api.service',
        'turborama-suite-content-gateway.service','turborama-suite-admin.service','station-network-priority.service')}
    timer_active = subprocess.run(['systemctl','is-active','--quiet',TIMER]).returncode==0
    subprocess.run(['systemctl','stop',TIMER],check=True)
    try:
        deadline = time.monotonic()+45
        while unit('turborama-station-library-scan.service')['ActiveState'] in ('active','activating'):
            need(time.monotonic()<deadline, 'Existing scanner did not finish'); time.sleep(1)
        with context.under_lock(home):
            quiet()
            baseline = {key:context.bounded(path,32*1024*1024) for key,path in paths.items()}
            data = {key:json.loads(pair[0]) for key,pair in baseline.items()}
            old_module, module_info = context.bounded(MODULE,256*1024)
            candidate, audit = repair.repair(data['profiles'],data['engines'],operator)
            need(audit['changedProfiles']==1425 and len(audit['changedByEngine'])==7,
                 'Fresh N64 repair scope differs from the reviewed preflight')
            need(operator.prepare(data['catalog']['items'],candidate,data['engines'],data['modes'])==candidate,
                 'The scanner would append duplicates or change a mode')
            for key,(raw,_) in baseline.items(): store_raw('fingerprint-baseline-'+key+'.json',raw)
            store_raw('fingerprint-baseline-module.py',old_module)
            hashes = {key:hashlib.sha256(raw).hexdigest() for key,(raw,_) in baseline.items()}
            try:
                context.atomic(MODULE,SOURCE.read_bytes(),module_info)
                context.atomic(paths['profiles'],context.encode(candidate),baseline['profiles'][1])
                need(sha(MODULE)==sha(SOURCE) and read(paths['profiles'])==candidate,
                     'Published bytes differ from the reviewed repair')
                need(all(sha(paths[key])==hashes[key] for key in ('catalog','engines','modes')),
                     'Unrelated catalog/engine/mode data changed')
            except BaseException:
                context.atomic(MODULE,old_module,module_info)
                context.atomic(paths['profiles'],baseline['profiles'][0],baseline['profiles'][1])
                raise
            write('fingerprint-after-export.json',dict(catalog=data['catalog'],profiles=candidate,
                engines=data['engines'],modes=data['modes']))
            write('fingerprint-published.json',dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),
                **audit,profileCount=len(candidate),engineCount=len(data['engines']['engines']),
                catalogRevision=data['catalog']['revision'],registrySha256=sha(paths['profiles']),
                installedBuilderSha256=sha(MODULE),baselineSha256=hashes,
                apiPid=pid,apiRestarted=False,catalogEnginesAndModesPreserved=True))
    finally:
        if timer_active: subprocess.run(['systemctl','start',TIMER],check=True)
    need(context.process_id()==pid and all(unit(name)==old for name,old in units.items()),
         'A shared service process/state changed')
    expected = [p for p in candidate if p['engineId']==EID and p['itemId']==ITEM]
    proof = prove(data['catalog'],expected)
    write('fingerprint-signed-proof.json',proof)
    write('fingerprint-applied.json',dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),
        correctedProfiles=1425,exactEngineRevisions=7,apiRestarted=False,
        signedLocalAndPublicProofPassed=True,ownSyntheticLicensesRemoved=True,
        realLicensesPreserved=True,physicalAndroidRoomCreated=False,
        scannerTimerRestored=timer_active))
    print(json.dumps(dict(passed=True,correctedProfiles=1425,engines=7,
        localAndPublicAdmissionPassed=True,apiRestarted=False)),flush=True)


def prove_published():
    need(os.geteuid()==0 and os.environ.get('PKEXEC_UID')=='1000',
         'Genuine native Linux administrative authentication required')
    os.umask(0o077)
    publication=read(W/'fingerprint-published.json')
    need(publication['passed'] and not (W/'fingerprint-applied.json').exists(),
         'Publication must exist and proof must be pending')
    need(sha(MODULE)==publication['installedBuilderSha256'], 'Installed fixed builder changed')
    context=load('fingerprint_proof_context',DOCS/'scripts/quarentenar-wind-waker-station.py')
    operator=load('fingerprint_proof_operator',MODULE)
    repair=load('fingerprint_proof_repair',FIX/'repair_n64_profile_hashes.py')
    config,pid,index_path,home=context.load_context()
    deadline=time.monotonic()+90
    while True:
        try:
            lock=context.under_lock(home)
            break
        except BlockingIOError:
            need(time.monotonic()<deadline, 'Scanner is still busy; resume the proof after its current scan')
            time.sleep(1)
    with lock:
        index=read(index_path)
        profiles=read(Path(config['autoOnlineProfiles']['registry']))
        manifest=read(Path(config['autoOnlineProfiles']['engineManifest']))
        checked,audit=repair.repair(profiles,manifest,operator)
        need(checked==profiles and audit['changedProfiles']==0 and audit['alreadyCorrectProfiles']==1425,
             'Live repaired fingerprints differ')
    expected=[p for p in profiles if p['engineId']==EID and p['itemId']==ITEM]
    proof=prove(index,expected)
    write('fingerprint-signed-proof.json',proof)
    write('fingerprint-applied.json',dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),
        correctedProfiles=1425,exactEngineRevisions=7,apiRestarted=False,
        signedLocalAndPublicProofPassed=True,ownSyntheticLicensesRemoved=True,
        realLicensesPreserved=True,physicalAndroidRoomCreated=False,
        scannerTimerRestored=subprocess.run(['systemctl','is-active','--quiet',TIMER]).returncode==0,
        proofResumedAfterPublication=True))
    print(json.dumps(dict(passed=True,correctedProfiles=1425,engines=7,
        localAndPublicAdmissionPassed=True,apiRestarted=False)),flush=True)


if __name__=='__main__':
    try:
        if sys.argv[1:]==['--prove-published']: prove_published()
        else:
            need(not sys.argv[1:], 'Unknown arguments')
            main()
    except BaseException as error:
        write('fingerprint-error-'+str(time.time_ns())+'.json',dict(type=type(error).__name__,
            trace=traceback.format_exc(),publicationExists=(W/'fingerprint-published.json').exists()))
        print('Repair/proof stopped; private diagnostic saved. Reconcile publication before retrying.',flush=True)
        raise SystemExit(1)
