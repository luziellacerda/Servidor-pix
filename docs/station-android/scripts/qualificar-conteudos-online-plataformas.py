#!/usr/bin/python3
"""Qualify exact launch-file hashes offline; never run this in a download request.

Reads Station artifacts and writes fresh candidate data. No service, live index,
importer configuration or game file is changed. Reports exclude private paths.
"""
from pathlib import Path, PurePosixPath
import argparse, concurrent.futures, datetime, hashlib, importlib.util, json, os, zipfile

SUPPORTED={'n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3'}
EXTENSIONS={'n64':{'.n64','.z64','.v64','.rom'},'neogeo':{'.zip'},'neogeocd':{'.chd','.cue'},
    'psx':{'.pbp','.chd','.cue','.bin','.img','.iso'},'fbneo':{'.zip'},'cps1':{'.zip'},'cps2':{'.zip'},'cps3':{'.zip'}}

def digest(handle):
    return hashlib.file_digest(handle,'sha256').hexdigest()

# Reuse the established R81 binder, including full archive verification,
# safe members, sizes, duplicate/symlink refusal and file replacement checks.
BINDER_PATH=Path(__file__).resolve().parents[1]/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py'
_spec=importlib.util.spec_from_file_location('station_identity_binder',BINDER_PATH)
binder=importlib.util.module_from_spec(_spec);_spec.loader.exec_module(binder)

def qualify(row):
    try:
        launch=row['artifact']['launchPath']
        binder.member_path(launch)
        if PurePosixPath(launch).suffix.lower() not in EXTENSIONS[row['platform']]:raise ValueError('unsupported-format')
        return binder.bind(row,row['filePath']),None
    except (binder.ValidationError,ValueError,KeyError,OSError,zipfile.BadZipFile,RuntimeError) as error:
        reason=str(error) if isinstance(error,(ValueError,binder.ValidationError)) else type(error).__name__
        return None,dict(itemId=row['itemId'],name=row['name'],platform=row['platform'],reason=reason)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--index',type=Path,required=True);parser.add_argument('--expected-index-sha256',required=True)
    parser.add_argument('--output',type=Path,required=True);parser.add_argument('--workers',type=int,default=2)
    args=parser.parse_args();assert 1<=args.workers<=4
    raw=args.index.read_bytes();assert hashlib.sha256(raw).hexdigest()==args.expected_index_sha256,'Index baseline changed'
    original=json.loads(raw);rows=original['items'];assert len(rows)<=4096
    args.output.mkdir(mode=0o700,parents=True,exist_ok=False)
    entries=[];issues=[];updates={};selected=[row for row in rows if row['platform'] in SUPPORTED and not row.get('contentSha256')]
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for n,(entry,issue) in enumerate(pool.map(qualify,selected),1):
            if entry:entries.append(entry);updates[entry['itemId']]=entry['contentSha256']
            if issue:issues.append(issue)
            if n%100==0:print(json.dumps(dict(processed=n,total=len(selected),qualified=len(updates))),flush=True)
    for row in rows:
        if row.get('contentSha256'):
            a=row['artifact'];entries.append(dict(itemId=row['itemId'],platform=row['platform'],artifactSha256=a['sha256'],launchPath=a['launchPath'],
                expandedSizeBytes=a['expandedSizeBytes'],fileCount=a['fileCount'],contentSha256=row['contentSha256']))
        elif row['itemId'] in updates:row['contentSha256']=updates[row['itemId']]
    assert len({entry['itemId'] for entry in entries})==len(entries)
    original['revision']+=1
    receipt=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),passed=not issues,indexBaselineSHA256=args.expected_index_sha256,
        candidateRevision=original['revision'],newIdentities=len(updates),totalIdentities=len(entries),issues=issues,productionChanged=False,physicalGameplayQualified=False)
    for name,value in [('content-identities.json',dict(schemaVersion=1,entries=sorted(entries,key=lambda e:e['itemId']))),('index-private.json',original),('qualification.json',receipt)]:
        target=args.output/name;target.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n');target.chmod(0o600)
    if hashlib.sha256(args.index.read_bytes()).hexdigest()!=args.expected_index_sha256:raise ValueError('Index changed during offline qualification')
    print(json.dumps({key:value for key,value in receipt.items() if key!='issues'}),flush=True)

if __name__=='__main__':main()
