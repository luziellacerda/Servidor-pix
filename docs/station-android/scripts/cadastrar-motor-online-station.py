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
from station_online_profiles import prepare, validate_manifest, validate_modes

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

def stage(args):
    catalog=read(args.catalog,32*1024*1024);existing=read(args.profiles,16*1024*1024)
    current=validate_manifest(read(args.current,512*1024));modes=read(args.modes,8*1024*1024);validate_modes(modes)
    incoming=read(args.incoming,512*1024);verify_binaries(incoming,args.artifacts)
    old={e['engineId']:e for e in current['engines']}
    for engine in incoming['engines']:
        if engine['engineId'] in old and engine!=old[engine['engineId']]:raise ValueError('engine IDs are immutable; use a new ID for a new binary')
        old[engine['engineId']]=engine
    manifest=dict(current,engines=list(old.values()));validate_manifest(manifest)
    profiles=prepare(catalog['items'],existing,manifest,modes)
    if profiles[:len(existing)]!=existing:raise ValueError('existing profiles must remain exact')
    args.output.mkdir(mode=0o700,exist_ok=False);artifacts=args.output/'artifacts';artifacts.mkdir(mode=0o700)
    for engine in incoming['engines']:
        for name in ('library','runtimeLibrary'):
            destination=artifacts/engine[name];destination.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
            if destination.exists() and digest(destination)!=digest(binary(args.artifacts,engine[name])):raise ValueError('conflicting binary path in incoming delivery')
            shutil.copyfile(binary(args.artifacts,engine[name]),destination);destination.chmod(0o600)
    for name,value in [('incoming.json',incoming),('engines.json',manifest),('profiles.json',profiles)]:write(args.output/name,value)
    receipt=dict(schemaVersion=1,serverRebuildRequired=False,serverRestartRequired=False,
        baseline={key:digest(path) for key,path in [('catalog',args.catalog),('profiles',args.profiles),('current',args.current),('modes',args.modes)]},
        files={name:digest(args.output/name) for name in ('incoming.json','engines.json','profiles.json')},
        enginesAdded=len(old)-len(current['engines']),profilesAdded=len(profiles)-len(existing),previousProfilesPreserved=len(existing),
        realBinaryHashesVerified=True,androidGameplayValidated=False)
    write(args.output/'receipt.json',receipt)
    print(json.dumps({k:v for k,v in receipt.items() if k not in ('baseline','files')}),flush=True)

def apply(args):
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise ValueError('native Linux administrative authentication required')
    config=read(args.config,1024*1024);home=Path(config['outputDirectory']);online=config['autoOnlineProfiles']
    paths={'catalog':home/'index.json','profiles':Path(online['registry']),'current':Path(online['engineManifest']),'modes':Path(online['preparedModes'])}
    if any(path.parent!=home or path.is_symlink() for path in paths.values()):raise ValueError('protected persistent Station registries required')
    receipt=read(args.candidate/'receipt.json',16384);incoming=read(args.candidate/'incoming.json',512*1024)
    with (home/'scan.lock').open('a') as lock:
        fcntl.flock(lock,fcntl.LOCK_EX)
        if receipt['baseline']!={k:digest(p) for k,p in paths.items()}:raise ValueError('publication baseline changed; prepare again with the current catalog')
        if receipt['files']!={n:digest(args.candidate/n) for n in ('incoming.json','engines.json','profiles.json')}:raise ValueError('candidate seal changed')
        verify_binaries(incoming,args.candidate/'artifacts')
        current=validate_manifest(read(paths['current'],512*1024));old={e['engineId']:e for e in current['engines']}
        for engine in incoming['engines']:
            if engine['engineId'] in old and engine!=old[engine['engineId']]:raise ValueError('engine IDs are immutable')
            old[engine['engineId']]=engine
        manifest=dict(current,engines=list(old.values()));validate_manifest(manifest)
        existing=read(paths['profiles'],16*1024*1024)
        profiles=prepare(read(paths['catalog'],32*1024*1024)['items'],existing,manifest,read(paths['modes'],8*1024*1024))
        if manifest!=read(args.candidate/'engines.json',512*1024) or profiles!=read(args.candidate/'profiles.json',16*1024*1024):raise ValueError('candidate is not the exact recomputed binding')
        owner=pwd.getpwnam('turborama-station-api')
        backup=home/('.engine-enrollment-'+os.urandom(8).hex());backup.mkdir(mode=0o700)
        for k in ('current','profiles'):shutil.copy2(paths[k],backup/(k+'.json'))
        try:
            write(paths['current'],manifest,owner.pw_gid);write(paths['profiles'],profiles,owner.pw_gid)
        except Exception:
            for k in ('current','profiles'):write(paths[k],read(backup/(k+'.json'),16*1024*1024),owner.pw_gid)
            raise
        result=dict(applied=True,serverRestarted=False,existingProfilesPreserved=len(existing),profilesAdded=len(profiles)-len(existing),reloadWithinSeconds=10,
            enginesSha256=digest(paths['current']),profilesSha256=digest(paths['profiles']))
        write(backup/'receipt.json',result);print(json.dumps(result),flush=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__);sub=parser.add_subparsers(dest='action',required=True)
    prepare_parser=sub.add_parser('prepare')
    for name in ('catalog','profiles','current','modes','incoming','artifacts','output'):prepare_parser.add_argument('--'+name,type=Path,required=True)
    apply_parser=sub.add_parser('apply')
    for name in ('config','candidate'):apply_parser.add_argument('--'+name,type=Path,required=True)
    install_parser=sub.add_parser('install')
    for name in ('config','incoming','artifacts','output'):install_parser.add_argument('--'+name,type=Path,required=True)
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
