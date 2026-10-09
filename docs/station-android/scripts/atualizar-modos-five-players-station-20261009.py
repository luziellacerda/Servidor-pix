#!/usr/bin/env python3
"""Apply the exact additive Bomberman mode data to the already deployed 5P server.

The DLL, existing protected settings and original profile files stay byte exact.
Qualification uses owned fixtures, then a quiet-only Station reload with rollback.
"""
import argparse, fcntl, hashlib, json, os
from pathlib import Path
import pwd, shutil, sys, traceback

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import importlib.util
spec = importlib.util.spec_from_file_location('station_five_rollout',
    Path(__file__).with_name('implantar-five-players-station-20261009.py'))
b = importlib.util.module_from_spec(spec); spec.loader.exec_module(b)
ops, security, r71 = b.ops, b.security, b.r71

CURRENT = Path('/opt/turborama-station-five-players-20261009-ad45a4f0f4c7')
DLL_SHA = 'ad45a4f0f4c7aef3dd4191b453da384be008e2b06aaaeb8b1ca359f3cb83570a'
PREVIOUS_SHA = '5f358c59d5d21966b23420f01807cf8b8391bb4715ee1dc75708541ab5c03d9b'
PROFILES_SHA = '244d98e76c4b5cc00f1af75abebe595688d1700cef6d2f8bc777dc84c3b388ad'
INDEX_SHA = '6b8acfa4ca419ec705f48f53e2063633ff8f0a30a36cb8f4d651a108cbb31137'
TARGET = Path('/opt/turborama-station-five-players-modes-20261009-'+PROFILES_SHA[:12])
DROPIN = b.DROPIN.with_name('z'*34+'-station-modes-20261009.conf')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('profiles', 'work-directory'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or os.environ.get('PKEXEC_UID') != '1000':
        raise ValueError('Native Linux administrative authentication required')
    os.umask(0o077)
    for path in (args.profiles, args.work_directory):
        if not path.is_absolute() or path.is_symlink():
            raise ValueError('Absolute regular inputs required')
    if DROPIN.exists():
        raise ValueError('Already applied or superseded; do not repeat this operator')
    if b.digest(args.profiles) != PROFILES_SHA:
        raise ValueError('Reviewed input seal changed')
    args.work_directory.mkdir(mode=0o700, exist_ok=False)
    operator = pwd.getpwnam('lz-servidor')
    # Only receipts are owned by the operator; root-only subdirectories stay private.
    ops.run(['setfacl', '-m', 'u:'+str(operator.pw_uid)+':rx', str(args.work_directory)])
    with (b.INDEX.parent/'scan.lock').open('a') as held:
        fcntl.flock(held, fcntl.LOCK_EX)
        values, ids = ops.runtime(); owner = pwd.getpwnam(b.IDENTITY)
        registry = Path(values['Station__Online__EngineRegistryFile'])
        original = Path(values['Station__Online__MultiplayerProfileRegistryFile'])
        if str(CURRENT/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart') or b.digest(CURRENT/'TurboRamaSuiteOnlineServer.dll') != DLL_SHA:
            raise ValueError('Effective release was superseded')
        if b.digest(original) != PREVIOUS_SHA or b.digest(registry) != b.REGISTRY or b.digest(b.INDEX) != INDEX_SHA:
            raise ValueError('Reviewed baseline was superseded')
        if ids['Uid'][1] != owner.pw_uid or ids['Gid'][1] != owner.pw_gid:
            raise ValueError('Effective service identity changed')
        if values.get('Station__Online__MultiplayerEnabled', '').lower() != 'true' or values.get('Station__Online__MultiplayerLegacyCapacityGate', '').lower() != 'false':
            raise ValueError('Effective online policy changed')
        profiles = json.loads(args.profiles.read_text()); active = json.loads(original.read_text())
        if len(active) != 3666 or len(profiles) != 3672 or profiles[:1816] != active[:1816]:
            raise ValueError('Historical profiles were not preserved')
        identity = lambda p:tuple(p[k] for k in ('itemId', 'engineId', 'profileId'))
        new_by_id = {identity(p):p for p in profiles}
        def gameplay(p):return {k:v for k,v in p.items() if k not in ('instructions', 'modeTitle', 'sources')}
        if len(new_by_id) != len(profiles) or any(identity(p) not in new_by_id or gameplay(p) != gameplay(new_by_id[identity(p)]) for p in active):
            raise ValueError('A previously published gameplay identity was changed')
        index = json.loads(b.INDEX.read_text()); db = r71.database(values)
        state = dict(utc=b.utc(), indexSha256=b.digest(b.INDEX), oldFiles=security.files(b.OLD),
            configuration=security.snapshot_configs(), database=db, realLicenseSha256=security.real_licenses(db),
            ledgerSha256=hashlib.sha256(security.scalar(db,'SELECT version FROM suite.schema_migrations ORDER BY version').encode()).hexdigest(),
            shared={unit:ops.state(unit) for unit in security.SHARED}, beforePid=b.show('MainPID'), beforeReady=b.ready())
        if not b.quiet(state['beforeReady']):
            raise ValueError('Active or recovering room: apply deferred')
        b.private(args.work_directory/'baseline-private.json', state)
        before_files = security.files(CURRENT)
        backup = args.work_directory/'rollback'; backup.mkdir(mode=0o700)
        for n,path in enumerate(state['configuration']):
            shutil.copy2(path,backup/(str(n)+'.conf'))
        b.private(backup/'configuration-manifest.json',state['configuration'])
        destination = TARGET/'profiles.json'
        newvalues = dict(values); newvalues['Station__Online__MultiplayerProfileRegistryFile'] = str(destination)
        envtext='Station__Online__MultiplayerProfileRegistryFile='+str(destination)+'\n'
        if TARGET.exists():
            expected={'profiles.json':PROFILES_SHA,'modes.env':hashlib.sha256(envtext.encode()).hexdigest()}
            if TARGET.is_symlink() or TARGET.stat().st_uid != 0 or security.files(TARGET) != expected:
                raise ValueError('Unpublished candidate differs from the exact input seal')
        else:
            TARGET.mkdir(mode=0o750); os.chown(TARGET,0,owner.pw_gid)
            shutil.copyfile(args.profiles,destination); os.chown(destination,0,owner.pw_gid); destination.chmod(0o640)
            b.private(TARGET/'modes.env',envtext)
        # Explicit chmod is required because the process umask is intentionally077.
        TARGET.chmod(0o750)
        binds = b.show('BindReadOnlyPaths')+' '+str(TARGET)
        def preservation():
            b.verify_preservation(state)
            if security.files(CURRENT) != before_files or b.digest(destination) != PROFILES_SHA:
                raise ValueError('Immutable release or candidate profile bytes changed')
        proof = {'candidate':b.shadow(CURRENT,newvalues,index,args.work_directory,binds,'modes-candidate',True)}
        b.private(args.work_directory/'qualification.json',proof,True); preservation()
        if not b.quiet(b.ready()):
            raise ValueError('A room connected during qualification: apply deferred')
        changed = False
        try:
            b.progress('apply-modes', profiles=len(profiles), maximumPlayers=5)
            b.private(DROPIN,'[Service]\nEnvironmentFile='+str(TARGET/'modes.env')+'\nBindReadOnlyPaths='+str(TARGET)+'\n')
            DROPIN.chmod(0o644); changed=True
            ops.run(['systemctl','daemon-reload'])
            if not b.quiet(b.ready()):
                raise ValueError('A room connected before restart')
            ops.run(['systemctl','restart',b.SERVICE]); reloaded=b.utc(); r71.ready_recovery(b.LOCAL)
            current,current_ids=ops.runtime()
            if current_ids['Uid'][1] != owner.pw_uid or current_ids['Gid'][1] != owner.pw_gid:
                raise ValueError('Unexpected running identity')
            for k,v in newvalues.items():
                if k.startswith(('Station__','Suite__','ConnectionStrings__')) and current.get(k) != v:
                    raise ValueError('Unexpected effective application setting')
            if str(CURRENT/'TurboRamaSuiteOnlineServer.dll') not in b.show('ExecStart'):
                raise ValueError('Unexpected running release')
            proof['public']=b.checks(index,current,b.PUBLIC,True)
            after=b.wait_quiet(b.LOCAL); preservation()
            pid=int(b.show('MainPID')); sandbox=r71.sandbox(pid,CURRENT,index)
            result=dict(applied=True,utc=b.utc(),reloadUtc=reloaded,pid=pid,dllSha256=DLL_SHA,
                maximumPlayers=5,approvedProfiles=len(profiles),fivePlayerProfiles=2,
                previousProfileBindingsPreserved=3666,originalProfilesPreserved=1816,addedBindings=6,
                priorProfilesSha256=PREVIOUS_SHA,profilesSha256=b.digest(destination),indexSha256=INDEX_SHA,
                catalogRevision=index['revision'],catalogItems=len(index['items']),
                legacyEngineRegistrySha256=b.digest(registry),legacyEnginesPreserved=10,
                realLicensesPreserved=True,schemaPreserved=True,catalogAndCoversPreserved=True,
                sharedProductsPreserved=True,proxyTunnelFirewallChanged=False,physicalFivePhoneGameplayQualified=False,
                proof=proof,sandbox=sandbox,readyBefore=state['beforeReady'],readyAfter=after,
                target=str(TARGET),dropinSha256=b.digest(DROPIN))
            b.private(args.work_directory/'deployment-result.json',result,True)
            b.progress('complete-modes',applied=True,pid=pid,maximumPlayers=5,profiles=len(profiles))
        except Exception:
            if changed:
                if not b.quiet(b.ready()):
                    raise ValueError('Rollback deferred: another room is active')
                DROPIN.unlink(missing_ok=True);ops.run(['systemctl','daemon-reload'])
                ops.run(['systemctl','restart',b.SERVICE]);r71.ready_recovery(b.LOCAL)
                b.progress('rollback-modes',previousDataRestored=True)
            raise

if __name__ == '__main__':
    try:
        main()
    except Exception:
        paths=[Path(sys.argv[i+1]) for i,v in enumerate(sys.argv[:-1]) if v=='--work-directory']
        if paths and paths[0].is_dir():
            b.private(paths[0]/'diagnostic-private.txt',traceback.format_exc(),True)
        b.progress('failed',detailsSuppressed=True)
        raise SystemExit(1)
