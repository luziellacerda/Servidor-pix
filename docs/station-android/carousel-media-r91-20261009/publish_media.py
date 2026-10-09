"""Prepare an immutable menu-video directory locally; never deploy or restart a service.

Use --apk for initial export, or --source with 720-*.mp4 for later operator updates.
Only after all files validate is index.json atomically replaced. Old files remain for active readers.
"""
from pathlib import Path
import argparse,hashlib,json,os,re,shutil,subprocess,tempfile,zipfile

def main():
 p=argparse.ArgumentParser();g=p.add_mutually_exclusive_group(required=True);g.add_argument('--apk',type=Path);g.add_argument('--source',type=Path)
 p.add_argument('--output',type=Path,required=True);p.add_argument('--revision',type=int,required=True);p.add_argument('--ffprobe',default='ffprobe');p.add_argument('--replace-all',action='store_true');a=p.parse_args()
 assert 1<=a.revision<=9007199254740991
 a.output.mkdir(parents=True,exist_ok=True);index=a.output/'index.json'
 previous=json.loads(index.read_text('utf8')) if index.exists() else None
 if previous:assert previous['revision']<a.revision,'Increment the revision'
 archive=zipfile.ZipFile(a.apk) if a.apk else None
 names=sorted(n for n in archive.namelist() if n.startswith('assets/turbo-system-videos/720-') and n.endswith('.mp4')) if archive else sorted(f.name for f in a.source.glob('720-*.mp4'))
 assert 1<=len(names)<=128
 saved={} if not previous or a.replace_all else {e['asset']:e for e in previous['items']}
 for e in saved.values():
  assert re.fullmatch(r'turbo-system-videos/720-[a-z0-9][a-z0-9_-]{0,95}\.mp4',e['asset']) and re.fullmatch('[0-9a-f]{64}',e['sha256'])
  file=a.output/(e['sha256']+'.mp4');assert file.stat().st_size==e['sizeBytes']
  with file.open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==e['sha256']
 rows=[];total=0
 try:
  for name in names:
   asset=name.removeprefix('assets/') if archive else 'turbo-system-videos/'+name
   assert re.fullmatch(r'turbo-system-videos/720-[a-z0-9][a-z0-9_-]{0,95}\.mp4',asset),asset
   handle,temporary=tempfile.mkstemp(prefix='.media-',suffix='.mp4',dir=a.output);os.close(handle);temporary=Path(temporary)
   try:
    with (archive.open(name) if archive else (a.source/name).open('rb')) as src,temporary.open('wb') as dst:
     shutil.copyfileobj(src,dst,65536)
    size=temporary.stat().st_size;assert 16<=size<=32*1024*1024
    info=subprocess.run([a.ffprobe,'-v','error','-show_streams','-of','json',str(temporary)],capture_output=True,check=True)
    streams=json.loads(info.stdout)['streams'];assert len(streams)==1
    stream=streams[0];assert stream['codec_type']=='video' and stream['codec_name']=='h264' and stream['width']==720 and stream['height']==720
    from fractions import Fraction
    assert Fraction(stream['avg_frame_rate'])==30 and Fraction(stream['r_frame_rate'])==30
    with temporary.open('rb') as f:hash=hashlib.file_digest(f,'sha256').hexdigest()
    target=a.output/(hash+'.mp4')
    if target.exists():
     with target.open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==hash
     temporary.unlink()
    else:os.replace(temporary,target)
    total+=size;assert total<=256*1024*1024
    rows.append(dict(asset=asset,sha256=hash,sizeBytes=size,contentType='video/mp4',width=720,height=720,fps=30,audioTracks=0))
   finally:
    if temporary.exists():temporary.unlink()
 finally:
  if archive:archive.close()
 for e in rows:saved[e['asset']]=e
 rows=[saved[k] for k in sorted(saved)];total=sum(e['sizeBytes'] for e in rows)
 assert len(rows)<=128 and total<=256*1024*1024
 manifest=dict(schemaVersion=1,revision=a.revision,items=rows)
 handle,temporary=tempfile.mkstemp(prefix='.index-',suffix='.json',dir=a.output)
 with os.fdopen(handle,'w',encoding='utf8',newline='\n') as f:json.dump(manifest,f,indent=2);f.write('\n');f.flush();os.fsync(f.fileno())
 os.replace(temporary,index)
 print(json.dumps(dict(publishedLocally=True,files=len(rows),bytes=total,revision=a.revision,serverDeployed=False)))
if __name__=='__main__':main()
