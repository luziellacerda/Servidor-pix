#!/usr/bin/python3
"""Qualify exact launch-file hashes offline; never run this in a download request.

Reads Station artifacts and writes fresh candidate data. No service, live index,
importer configuration or game file is changed. Reports exclude private paths.
"""
from pathlib import Path, PurePosixPath
import argparse, concurrent.futures, datetime, hashlib, importlib.util, json, os, zipfile
from station_content_sets import bind_set

SUPPORTED={'n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3','dreamcast','gamecube','wii','wiiu','switch'}
EXTENSIONS={'n64':{'.n64','.z64','.v64','.rom'},'neogeo':{'.zip'},'neogeocd':{'.chd','.cue'},
    'psx':{'.pbp','.chd','.cue','.bin','.img','.iso'},'fbneo':{'.zip'},'cps1':{'.zip'},'cps2':{'.zip'},'cps3':{'.zip'},
    'dreamcast':{'.chd','.gdi','.cdi'},'gamecube':{'.iso','.gcm','.rvz'},'wii':{'.iso','.rvz','.wbfs'},
    'wiiu':{'.rpx','.wud','.wux','.wua'},'switch':{'.nsp','.xci','.nro'}}

def digest(handle):
    return hashlib.file_digest(handle,'sha256').hexdigest()

# Reuse the established R81 binder, including full archive verification,
# safe members, sizes, duplicate/symlink refusal and file replacement checks.
BINDER_PATH=Path(__file__).resolve().parents[1]/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py'
_spec=importlib.util.spec_from_file_location('station_identity_binder',BINDER_PATH)
binder=importlib.util.module_from_spec(_spec);_spec.loader.exec_module(binder)

def qualify(row,content_sets=False):
    try:
        launch=row['artifact']['launchPath']
        binder.member_path(launch)
        if PurePosixPath(launch).suffix.lower() not in EXTENSIONS[row['platform']]:raise ValueError('unsupported-format')
        entry=binder.bind(row,row['filePath'])
        return bind_set(row,entry) if content_sets else entry,None
    except (binder.ValidationError,ValueError,KeyError,OSError,zipfile.BadZipFile,RuntimeError) as error:
        reason=str(error) if isinstance(error,(ValueError,binder.ValidationError)) else type(error).__name__
        return None,dict(itemId=row['itemId'],name=row['name'],platform=row['platform'],reason=reason)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--index',type=Path,required=True);parser.add_argument('--expected-index-sha256',required=True)
    parser.add_argument('--output',type=Path,required=True);parser.add_argument('--workers',type=int,default=2)
    parser.add_argument('--content-sets',action='store_true',help='Bind CUE tracks and the entire Wii U set instead of only its launch file')
    args=parser.parse_args()
    if not 1<=args.workers<=4:raise ValueError('Worker bound exceeded')
    raw=args.index.read_bytes()
    if hashlib.sha256(raw).hexdigest()!=args.expected_index_sha256:raise ValueError('Index baseline changed')
    original=json.loads(raw);rows=original['items']
    if len(rows)>4096:raise ValueError('Catalog bound exceeded')
    args.output.mkdir(mode=0o700,parents=True,exist_ok=False)
    entries=[];issues=[];updates={};selected=[row for row in rows if row['platform'] in SUPPORTED and (not row.get('contentSha256') or args.content_sets and (row['artifact']['launchPath'].lower().endswith('.cue') or row['platform']=='wiiu') and not row.get('contentIdentityScheme'))]
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for n,(entry,issue) in enumerate(pool.map(lambda row:qualify(row,args.content_sets),selected),1):
            if entry:entries.append(entry);updates[entry['itemId']]=entry
            if issue:issues.append(issue)
            if n%100==0:print(json.dumps(dict(processed=n,total=len(selected),qualified=len(updates))),flush=True)
    for row in rows:
        if row['itemId'] in updates:
            row['contentSha256']=updates[row['itemId']]['contentSha256']
            if updates[row['itemId']].get('contentIdentityScheme'):row['contentIdentityScheme']=updates[row['itemId']]['contentIdentityScheme']
        elif row.get('contentSha256'):
            a=row['artifact'];entries.append(dict(itemId=row['itemId'],platform=row['platform'],artifactSha256=a['sha256'],launchPath=a['launchPath'],
                expandedSizeBytes=a['expandedSizeBytes'],fileCount=a['fileCount'],contentSha256=row['contentSha256'],**({'contentIdentityScheme':row['contentIdentityScheme']} if row.get('contentIdentityScheme') else {})))
    if len({entry['itemId'] for entry in entries})!=len(entries):raise ValueError('Ambiguous content identity')
    original['revision']+=1
    receipt=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),passed=not issues,indexBaselineSHA256=args.expected_index_sha256,
        candidateRevision=original['revision'],newIdentities=len(updates),totalIdentities=len(entries),issues=issues,productionChanged=False,physicalGameplayQualified=False)
    for name,value in [('content-identities.json',dict(schemaVersion=2 if any(e.get('contentIdentityScheme') for e in entries) else 1,entries=sorted(entries,key=lambda e:e['itemId']))),('index-private.json',original),('qualification.json',receipt)]:
        target=args.output/name;target.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n');target.chmod(0o600)
    if hashlib.sha256(args.index.read_bytes()).hexdigest()!=args.expected_index_sha256:raise ValueError('Index changed during offline qualification')
    print(json.dumps({key:value for key,value in receipt.items() if key!='issues'}),flush=True)

if __name__=='__main__':main()
