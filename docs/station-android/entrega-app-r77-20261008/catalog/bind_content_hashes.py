"""Hash operator-supplied artifacts and exact launch payloads without extraction.

Input mapping is private JSON: {"itemId": "absolute path to owned artifact"}.
The output deliberately contains no local path and does not authorize gameplay.
"""
from pathlib import Path,PurePosixPath
import argparse,hashlib,json,zipfile,zlib
from datetime import datetime,timezone

def digest_file(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def safe_member(name):
    parts=PurePosixPath(name).parts
    return bool(parts) and not name.startswith(('/','\\')) and '\\' not in name and all(p not in ('.','..') and ':' not in p for p in parts)

def payload_hashes(stream,expected_size):
    """Checksums identify database entries; SHA-256 remains the catalog binding."""
    hashes={name:hashlib.new(name) for name in ('sha256','sha1','md5')};crc=0;size=0
    while True:
        block=stream.read(min(1048576,expected_size-size+1))
        if not block:break
        size+=len(block);assert size<=expected_size,'payload exceeds descriptor'
        for h in hashes.values():h.update(block)
        crc=zlib.crc32(block,crc)
    assert size==expected_size,'truncated payload'
    return size,{name:h.hexdigest() for name,h in hashes.items()}|{'crc32':f'{crc&0xffffffff:08x}'}

def bind(record,path):
    path=Path(path);assert path.is_file(),'artifact absent'
    assert path.stat().st_size==record['artifactSizeBytes'],'artifact size mismatch'
    assert digest_file(path)==record['artifactSha256'],'artifact digest mismatch'
    assert record['artifactFileCount']>=1 and record['artifactExpandedSizeBytes']>0,'invalid descriptor bounds'
    if record['artifactFormat']=='raw':
        assert record['artifactFileCount']==1 and record['artifactExpandedSizeBytes']==record['artifactSizeBytes'],'raw descriptor mismatch'
        with path.open('rb') as f:size,hashes=payload_hashes(f,record['artifactSizeBytes'])
        content_hash=hashes['sha256'];assert content_hash==record['artifactSha256'],'raw changed during binding'
    elif record['artifactFormat']=='zip':
        launch=record['artifactLaunchPath'];assert safe_member(launch),'unsafe launch member'
        with zipfile.ZipFile(path) as archive:
            files=[m for m in archive.infolist() if not m.is_dir()]
            assert all(safe_member(m.filename) for m in files),'unsafe archive member'
            assert len(files)==record['artifactFileCount'],'archive file count mismatch'
            assert sum(m.file_size for m in files)==record['artifactExpandedSizeBytes'],'expanded size mismatch'
            assert len({m.filename for m in files})==len(files),'duplicate archive member'
            entries=[m for m in files if m.filename==launch];assert len(entries)==1,'exact launch member missing'
            entry=entries[0]
            with archive.open(entry) as f:size,hashes=payload_hashes(f,entry.file_size)
            content_hash=hashes['sha256']
        assert digest_file(path)==record['artifactSha256'],'archive changed during binding'
    else:raise AssertionError('unsupported artifact format')
    return {'itemId':record['itemId'],'platform':record['platform'],'artifactSha256':record['artifactSha256'],
      'contentSha256':content_hash,'contentSizeBytes':size,'databaseChecksums':{'sha1':hashes['sha1'],'md5':hashes['md5'],'crc32':hashes['crc32']},'normalization':'none-full-launch-file',
      'source':'operator-owned-artifact-exact-launch-path','authorizesOnline':False,
      'qualificationRequired':['exact game variant','mode and simultaneous player counts','core/controller profile','network/physical test']}

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--inventory',type=Path,required=True);p.add_argument('--mapping',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    doc=json.loads(a.inventory.read_text(encoding='utf-8'));records={r['itemId']:r for r in doc['records']}
    assert len(records)==len(doc['records']),'duplicate inventory ID';mapping=json.loads(a.mapping.read_text(encoding='utf-8'));results=[]
    for item_id,path in mapping.items():
        assert item_id in records,'unrecognized item ID';assert Path(path).is_absolute(),'absolute path required'
        try:results.append(bind(records[item_id],path))
        except Exception as exc:raise RuntimeError('Content binding failed for '+item_id+' ('+type(exc).__name__+'); inspect the private artifact locally.') from None
    result={'schemaVersion':1,'utc':datetime.now(timezone.utc).isoformat(),'inventorySHA256':digest_file(a.inventory),'bindings':results,'authorizationRegistry':False}
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({'bindings':len(results),'authorized':0}))

if __name__=='__main__':main()
