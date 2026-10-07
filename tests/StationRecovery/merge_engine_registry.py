"""Prepare an additive private registry file. Does not change the service or activate v2."""
from pathlib import Path
import argparse, json, os, re

p=argparse.ArgumentParser()
for name in ('existing','additions','output'):p.add_argument('--'+name,required=True)
a=p.parse_args()
paths=[Path(getattr(a,name)).resolve() for name in ('existing','additions','output')]
existing, additions, output=paths
if output.exists():raise SystemExit('Use a new output file')
documents=[]
for source in (existing,additions):
    if source.stat().st_size>32768:raise SystemExit('Bounded registry required')
    data=json.loads(source.read_text())
    if not isinstance(data,list):raise SystemExit('Expected engine array')
    documents.append(data)
registry,delta=documents
if len(registry)!=len({row['id'] for row in registry}):raise SystemExit('Duplicate original engine identity')
allowed={'id','platform','coreSha256','runtimeSha256','recoveryProtocol'}
for row in delta:
    if set(row)!=allowed or row['recoveryProtocol']!='station-stream.v2' or not 1<=len(row['id'])<=64:
        raise SystemExit('Invalid recovery registry addition')
    if any(not re.fullmatch('[0-9a-f]{64}',row[name]) for name in ('coreSha256','runtimeSha256')):
        raise SystemExit('Invalid engine digest')
    previous=next((old for old in registry if old['id']==row['id']),None)
    if previous is not None:
        if previous!=row:raise SystemExit('Engine ID collision; never overwrite the old identity')
    else:registry.append(row)
if not 1<=len(registry)<=32:raise SystemExit('Too many engines')
encoded=(json.dumps(registry,indent=2)+'\n').encode()
if len(encoded)>32768:raise SystemExit('Registry too large')
with output.open('xb') as stream:stream.write(encoded)
if os.name!='nt':output.chmod(0o600)
print(json.dumps(dict(prepared=True,engines=len(registry),productionActivated=False)))
