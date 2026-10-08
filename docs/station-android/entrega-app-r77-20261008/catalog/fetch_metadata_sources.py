"""Fetch public factual databases, never ROMs/media, from one pinned Libretro revision."""
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.parse import quote
from urllib.error import HTTPError
import argparse,hashlib,json,time
from datetime import datetime,timezone
COMMIT='fbeefcb46c2e1b20a7e2945f34a694a41b2d6f90'
ROOT='https://raw.githubusercontent.com/libretro/libretro-database/'+COMMIT+'/'
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog\libretro-sources')
SYSTEMS={'snes':'Nintendo - Super Nintendo Entertainment System','megadrive':'Sega - Mega Drive - Genesis','n64':'Nintendo - Nintendo 64','neogeo':'SNK - Neo Geo','neogeocd':'SNK - Neo Geo CD'}
FIELDS=['developer','publisher','genre','releaseyear','releasemonth','maxusers','franchise','esrb']
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,default=WORK);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
 entries=[];paths=[('documentation','README.md'),('license','LICENSE')]
 for platform,name in SYSTEMS.items():
  base=('dat/' if platform=='neogeo' else 'metadat/redump/' if platform=='neogeocd' else 'metadat/no-intro/')+name+'.dat'
  paths.append((platform,base));paths.extend((platform,'metadat/'+f+'/'+name+'.dat') for f in FIELDS)
 for platform,path in paths:
  dest=a.output/path;url=ROOT+quote(path)
  if dest.exists():data=dest.read_bytes()
  else:
   try:
    with urlopen(Request(url,headers={'User-Agent':'TurboStations-public-metadata-research/1.0'}),timeout=40) as response:data=response.read(20_000_001)
   except HTTPError as e:
    if e.code==404:entries.append({'platform':platform,'path':path,'url':url,'status':'not-published-at-pinned-revision'});continue
    raise
   if len(data)>20_000_000:raise ValueError('Unexpected database size')
   dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
  entries.append({'platform':platform,'path':path,'url':url,'status':'downloaded','sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)})
 manifest={'utc':datetime.now(timezone.utc).isoformat(),'upstream':'https://github.com/libretro/libretro-database','commit':COMMIT,'license':'CC-BY-SA-4.0 (repository); source headers retained for inherited dataset provenance','files':entries}
 (a.output/'source-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n','utf8')
 print(json.dumps({'downloaded':sum(r['status']=='downloaded' for r in entries),'notPublished':sum(r['status']!='downloaded' for r in entries),'bytes':sum(r.get('bytes',0) for r in entries),'commit':COMMIT}))
if __name__=='__main__':main()
