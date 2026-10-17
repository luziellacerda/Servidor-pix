#!/usr/bin/env python3
"""Prepare/publish trusted v3 engines by data, with no server rebuild or restart.

prepare is offline and reads an APK delivery's real binary files. apply requires
native Linux administration and a current, unchanged baseline. App credentials
cannot invoke this local operator or add engines through the public API.
"""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pwd
import shutil
import sys
import tempfile
from station_online_profiles import (MAXIMUM_PREPARED_MODE_BYTES,
    MAXIMUM_TRUSTED_MANIFEST_BYTES,
    MAXIMUM_PROFILE_REGISTRY_BYTES, platform, prepare, profile_configuration,
    validate_manifest, validate_modes)

MAX_BINARY=256*1024*1024

def read(path,limit):
    if path.is_symlink() or not path.is_file() or path.stat().st_size>limit:raise ValueError('bounded regular file required')
    return json.loads(path.read_bytes())

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for data in iter(lambda:stream.read(1024*1024),b''):h.update(data)
    return h.hexdigest()

def binary(root,name):
    path=Path(name)
    if path.is_absolute() or not path.parts or any(p in ('.','..') for p in path.parts):raise ValueError('safe relative binary path required')
    target=root/path
    if any((root/Path(*path.parts[:i])).is_symlink() for i in range(1,len(path.parts)+1)) or not target.is_file() or not 0<target.stat().st_size<=MAX_BINARY:raise ValueError('bounded regular engine binary required')
    return target

def write(path,value,gid=None):
    handle,name=tempfile.mkstemp(prefix='.engine-registration-',dir=path.parent)
    try:
        with os.fdopen(handle,'w',encoding='utf-8') as stream:
            os.fchmod(stream.fileno(),0o640 if gid is not None else 0o600)
            if gid is not None:os.fchown(stream.fileno(),-1,gid)
            json.dump(value,stream,ensure_ascii=False,separators=(',',':'));stream.write('\n');stream.flush();os.fsync(stream.fileno())
        os.replace(name,path)
        directory=os.open(path.parent,os.O_DIRECTORY)
        try:os.fsync(directory)
        finally:os.close(directory)
    finally:
        if os.path.exists(name):os.unlink(name)

def verify_binaries(incoming,root):
    validate_manifest(incoming)
    for engine in incoming['engines']:
        if not engine['launchReady'] or engine.get('recoveryProtocol')!='station-stream.v3':raise ValueError('incoming engine must implement Station v3')
        for name,key in (('library','coreSha256'),('runtimeLibrary','runtimeSha256')):
            if not isinstance(engine.get(name),str) or digest(binary(root,engine[name]))!=engine[key]:raise ValueError('actual APK binary differs from engine hash')

def restore_bytes(path,source,gid):
    # Restore the exact baseline bytes, including formatting, under scan.lock.
    handle,name=tempfile.mkstemp(prefix='.engine-registration-',dir=path.parent)
    try:
        with os.fdopen(handle,'wb') as stream:
            os.fchmod(stream.fileno(),0o640);os.fchown(stream.fileno(),-1,gid)
            with source.open('rb') as original:shutil.copyfileobj(original,stream)
            stream.flush();os.fsync(stream.fileno())
        os.replace(name,path)
        directory=os.open(path.parent,os.O_DIRECTORY)
        try:os.fsync(directory)
        finally:os.close(directory)
    finally:
        if os.path.exists(name):os.unlink(name)

def reviewed_modes(existing,candidate,incoming):
    validate_modes(existing);validate_modes(candidate);validate_manifest(incoming)
    old=existing['modes'];next_modes=candidate['modes']
    if candidate.get('schemaVersion')!=existing.get('schemaVersion') or len(next_modes)<len(old):
        raise ValueError('existing prepared modes must remain exact')
    identities={e['engineId']:e for e in incoming['engines']}
    def targets(mode,scoped):
        if not scoped or not set(scoped)<=set(identities):
            raise ValueError('new prepared mode bindings must target only incoming engines')
        for identity in scoped:
            engine=identities[identity]
            if platform(engine['platform'])!=platform(mode['platform']) or mode['maximumPlayers']>engine.get('maximumPlayers',5) or profile_configuration(engine,mode['controllerProfile'],mode['maximumPlayers']) is None:
                raise ValueError('prepared mode binding needs matching platform and implemented controller')
    for before,after in zip(old,next_modes):
        if before==after:continue
        # A revision may reuse a reviewed decision without duplicating its text.
        # Preserve every decision field and every prior owner, in exact order.
        if {k:v for k,v in before.items() if k!='engineIds'}!={k:v for k,v in after.items() if k!='engineIds'}:
            raise ValueError('existing prepared mode decisions must remain exact')
        previous=before.get('engineIds');following=after.get('engineIds')
        if not previous or not following or following[:len(previous)]!=previous or len(following)<=len(previous):
            raise ValueError('existing prepared mode engine bindings must remain exact')
        targets(after,following[len(previous):])
    for mode in next_modes[len(old):]:
        targets(mode,mode.get('engineIds'))
    return candidate

def extended_bindings(existing,candidate):
    return sum(len(after.get('engineIds',[]))-len(before.get('engineIds',[]))
               for before,after in zip(existing['modes'],candidate['modes']))

def stage(args):
    catalog=read(args.catalog,32*1024*1024);existing=read(args.profiles,MAXIMUM_PROFILE_REGISTRY_BYTES)
    current=validate_manifest(read(args.current,MAXIMUM_TRUSTED_MANIFEST_BYTES));modes=read(args.modes,MAXIMUM_PREPARED_MODE_BYTES);validate_modes(modes)
    incoming=read(args.incoming,MAXIMUM_TRUSTED_MANIFEST_BYTES);verify_binaries(incoming,args.artifacts)
    next_modes=reviewed_modes(modes,read(args.mode_additions,MAXIMUM_PREPARED_MODE_BYTES),incoming) if getattr(args,'mode_additions',None) else modes
    old={e['engineId']:e for e in current['engines']}
    for engine in incoming['engines']:
        if engine['engineId'] in old and engine!=old[engine['engineId']]:raise ValueError('engine IDs are immutable; use a new ID for a new binary')
        old[engine['engineId']]=engine
    manifest=dict(current,engines=list(old.values()));validate_manifest(manifest)
    profiles=prepare(catalog['items'],existing,manifest,next_modes)
    if profiles[:len(existing)]!=existing:raise ValueError('existing profiles must remain exact')
    args.output.mkdir(mode=0o700,exist_ok=False);artifacts=args.output/'artifacts';artifacts.mkdir(mode=0o700)
    for engine in incoming['engines']:
        for name in ('library','runtimeLibrary'):
            destination=artifacts/engine[name];destination.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
            if destination.exists() and digest(destination)!=digest(binary(args.artifacts,engine[name])):raise ValueError('conflicting binary path in incoming delivery')
            shutil.copyfile(binary(args.artifacts,engine[name]),destination);destination.chmod(0o600)
    for name,value in [('incoming.json',incoming),('engines.json',manifest),('profiles.json',profiles),('modes.json',next_modes)]:write(args.output/name,value)
    receipt=dict(schemaVersion=1,serverRebuildRequired=False,serverRestartRequired=False,
        baseline={key:digest(path) for key,path in [('catalog',args.catalog),('profiles',args.profiles),('current',args.current),('modes',args.modes)]},
        files={name:digest(args.output/name) for name in ('incoming.json','engines.json','profiles.json','modes.json')},
        preparedModesAdded=len(next_modes['modes'])-len(modes['modes']),
        preparedModeEngineBindingsAdded=extended_bindings(modes,next_modes),
        enginesAdded=len(old)-len(current['engines']),profilesAdded=len(profiles)-len(existing),previousProfilesPreserved=len(existing),
        realBinaryHashesVerified=True,androidGameplayValidated=False)
    write(args.output/'receipt.json',receipt)
    print(json.dumps({k:v for k,v in receipt.items() if k not in ('baseline','files')}),flush=True)

def apply(args):
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('native Linux administrative authentication required')
    config=read(args.config,1024*1024);home=Path(config['outputDirectory']);online=config['autoOnlineProfiles']
    paths={'catalog':home/'index.json','profiles':Path(online['registry']),'current':Path(online['engineManifest']),'modes':Path(online['preparedModes'])}
    if any(path.parent!=home or path.is_symlink() for path in paths.values()):raise ValueError('protected persistent Station registries required')
    receipt=read(args.candidate/'receipt.json',16384);incoming=read(args.candidate/'incoming.json',MAXIMUM_TRUSTED_MANIFEST_BYTES)
    with (home/'scan.lock').open('a') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX)
        if receipt['baseline']!={k:digest(p) for k,p in paths.items()}:raise ValueError('publication baseline changed; prepare again with the current catalog')
        names=('incoming.json','engines.json','profiles.json','modes.json') if 'modes.json' in receipt['files'] else ('incoming.json','engines.json','profiles.json')
        if receipt['files']!={n:digest(args.candidate/n) for n in names}:raise ValueError('candidate seal changed')
        verify_binaries(incoming,args.candidate/'artifacts')
        current=validate_manifest(read(paths['current'],MAXIMUM_TRUSTED_MANIFEST_BYTES));old={e['engineId']:e for e in current['engines']}
        for engine in incoming['engines']:
            if engine['engineId'] in old and engine!=old[engine['engineId']]:raise ValueError('engine IDs are immutable')
            old[engine['engineId']]=engine
        manifest=dict(current,engines=list(old.values()));validate_manifest(manifest)
        existing=read(paths['profiles'],MAXIMUM_PROFILE_REGISTRY_BYTES)
        old_modes=read(paths['modes'],MAXIMUM_PREPARED_MODE_BYTES)
        next_modes=reviewed_modes(old_modes,read(args.candidate/'modes.json',MAXIMUM_PREPARED_MODE_BYTES),incoming) if 'modes.json' in receipt['files'] else old_modes
        profiles=prepare(read(paths['catalog'],32*1024*1024)['items'],existing,manifest,next_modes)
        if manifest!=read(args.candidate/'engines.json',MAXIMUM_TRUSTED_MANIFEST_BYTES) or profiles!=read(args.candidate/'profiles.json',MAXIMUM_PROFILE_REGISTRY_BYTES):raise ValueError('candidate is not the exact recomputed binding')
        owner=pwd.getpwnam('turborama-station-api')
        backup=home/('.engine-enrollment-'+os.urandom(8).hex());backup.mkdir(mode=0o700)
        mode_changed=next_modes!=old_modes
        backup_keys=('current','profiles','modes') if mode_changed else ('current','profiles')
        for k in backup_keys:shutil.copy2(paths[k],backup/(k+'.json'))
        try:
            # Importers share scan.lock. Publish profiles last so the HTTP monitor
            # only observes the completed approved binding set.
            if mode_changed:write(paths['modes'],next_modes,owner.pw_gid)
            write(paths['current'],manifest,owner.pw_gid);write(paths['profiles'],profiles,owner.pw_gid)
        except Exception:
            rollback_keys=('modes','current','profiles') if mode_changed else ('current','profiles')
            rollback_errors=[]
            for k in rollback_keys:
                try:restore_bytes(paths[k],backup/(k+'.json'),owner.pw_gid)
                except Exception as error:rollback_errors.append((k,type(error).__name__))
            if rollback_errors:raise ValueError('registry rollback requires operator recovery: '+repr(rollback_errors))
            raise
        result=dict(applied=True,serverRestarted=False,existingProfilesPreserved=len(existing),profilesAdded=len(profiles)-len(existing),reloadWithinSeconds=10,
            enginesSha256=digest(paths['current']),profilesSha256=digest(paths['profiles']),preparedModesSha256=digest(paths['modes']),preparedModesAdded=len(next_modes['modes'])-len(old_modes['modes']),preparedModeEngineBindingsAdded=extended_bindings(old_modes,next_modes))
        write(backup/'receipt.json',result);print(json.dumps(result),flush=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='action',required=True)
    prepare_parser=sub.add_parser('prepare')
    for name in ('catalog','profiles','current','modes','incoming','artifacts','output'):prepare_parser.add_argument('--'+name,type=Path,required=True)
    prepare_parser.add_argument('--mode-additions',type=Path)
    apply_parser=sub.add_parser('apply')
    for name in ('config','candidate'):apply_parser.add_argument('--'+name,type=Path,required=True)
    install_parser=sub.add_parser('install')
    for name in ('config','incoming','artifacts','output'):install_parser.add_argument('--'+name,type=Path,required=True)
    install_parser.add_argument('--mode-additions',type=Path)
    args=parser.parse_args()
    for value in vars(args).values():
        if isinstance(value,Path) and (not value.is_absolute() or value.is_symlink()):raise ValueError('absolute regular paths required')
    os.umask(0o077)
    if args.action=='install':
        if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('native Linux administrative authentication required')
        config=read(args.config,1024*1024);online=config['autoOnlineProfiles']
        args.catalog=Path(config['outputDirectory'])/'index.json'
        args.profiles=Path(online['registry']);args.current=Path(online['engineManifest']);args.modes=Path(online['preparedModes'])
        stage(args);args.candidate=args.output;apply(args)
    elif args.action=='prepare':stage(args)
    else:apply(args)

if __name__=='__main__':
    try:main()
    except (ValueError,KeyError,OSError,TypeError,json.JSONDecodeError) as error:
        print(json.dumps(dict(applied=False,errorType=type(error).__name__,message=str(error))),file=sys.stderr);raise SystemExit(1)
