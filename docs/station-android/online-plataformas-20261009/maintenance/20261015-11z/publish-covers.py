"""One reviewed cover-only publication, native authentication, fresh baseline, no ROM rebuild."""
from pathlib import Path
from datetime import datetime, timezone
from concurrent.futures import ThreadPoolExecutor
import copy, fcntl, hashlib, importlib.util, io, json, os, shutil, subprocess, sys, tempfile, time
from PIL import Image
sys.dont_write_bytecode=True
sys.path.insert(0,'/mnt/DADOS/station-relay-check-20261005/venv/lib/python3.12/site-packages')
W=Path(__file__).resolve().parent
DOC=Path('/mnt/DADOS/servidor-pix-station-manutencao-20261015-11z/docs/station-android')
TOOLS=Path('/opt/turborama-station-library-quarantine-20261014-a10c78f53797/library-tools')
DLL=Path('/opt/turborama-station-n64-ready-20261014-b3473cc01601/TurboRamaSuiteOnlineServer.dll')
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
TIMER='turborama-station-library-scan.timer';SCANNER='turborama-station-library-scan.service'
def need(x,s):
 if not x:raise ValueError(s)
def read(p):return json.loads(Path(p).read_bytes())
def raw(j):return (json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode()
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def stamp(p):
 s=Path(p).stat();return [s.st_dev,s.st_ino,s.st_size,s.st_mtime_ns]
def write(n,j):
 p=W/n;p.write_bytes(raw(j));p.chmod(0o600);os.chown(p,1000,1000)
def load(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);sys.modules[n]=m;s.loader.exec_module(m);return m
def run(a):return subprocess.check_output(a,text=True,stderr=subprocess.DEVNULL,timeout=60).strip()
def unit(n):return run(['systemctl','show',n,'-p','MainPID,ActiveState,NRestarts,ExecMainStartTimestamp'])
def atomic(p,data,uid,gid,mode):
 fd,t=tempfile.mkstemp(prefix='.station-hourly-cover-',dir=p.parent)
 try:
  with os.fdopen(fd,'wb') as f:
   os.fchown(f.fileno(),uid,gid);os.fchmod(f.fileno(),mode);f.write(data);f.flush();os.fsync(f.fileno())
  os.replace(t,p)
  d=os.open(p.parent,os.O_RDONLY|os.O_DIRECTORY)
  try:os.fsync(d)
  finally:os.close(d)
 finally:
  if os.path.exists(t):os.unlink(t)
def proof(index,values,desired):
 helpers=load('hourly_signed_probe','/mnt/DADOS/station-checkup-20261010/checkup.py');b=helpers.b
 checks={};byid={r['itemId']:r for r in index['items']}
 for label,address in [('local',b.LOCAL),('public',b.PUBLIC)]:
  probe=helpers.SignedProbe(index,values,address,lambda q:b.ops.sql(b.r71.database(values),q))
  if label=='public':load('hourly_origin_proof',W/'proof_http.py').install(probe)
  try:
   probe.start();deadline=time.monotonic()+45
   while True:
    cat=probe.signed('GET','/v1/station/catalog?metadata=1&maximumItems=20000','catalog')
    if cat['revision']==index['revision']:break
    need(cat['revision']<index['revision'] and time.monotonic()<deadline,'Signed catalog reload timeout');time.sleep(1)
   remote={r['itemId']:r for r in cat['items']};need(set(remote)=={i for i,r in byid.items() if r.get('catalogVisible',True)},'Signed catalog IDs differ')
   for item,row in byid.items():
    if item not in remote:continue
    for field in ('name','platform','revision','coverId','metadata','contentSha256','contentIdentityScheme','folderPath'):need(remote[item].get(field)==row.get(field),'Signed catalog field differs')
   def check(item):
    row=byid[item];status,headers,body=probe.request('GET','/v1/station/covers/'+row['coverId'])
    need(status==200 and hashlib.sha256(body).hexdigest()==desired[item],'Authenticated cover body differs')
    need(headers.get('Content-Type','').startswith('image/jpeg'),'Cover MIME differs')
    with Image.open(io.BytesIO(body)) as im:im.load();need(im.mode=='RGB' and im.size==(480,720),'Cover geometry differs')
    return dict(itemId=item,coverId=row['coverId'],revision=row['revision'],sha256=desired[item],bytes=len(body))
   with ThreadPoolExecutor(max_workers=3) as pool:rows=list(pool.map(check,desired))
   checks[label]=dict(passed=True,signedCatalogExact=True,checkedCovers=len(rows),covers=rows)
  finally:need(probe.client.cleanup(),'Own synthetic license cleanup failed')
 return checks

def main():
 need(os.geteuid()==0 and os.environ.get('PKEXEC_UID')=='1000','Native Linux authentication required')
 os.umask(0o077);write('native-started.json',dict(utc=datetime.now(timezone.utc).isoformat()))
 need(not (W/'published.json').exists() and not (W/'applied.json').exists(),'Publication exists; reconcile before rerun')
 plan=read(W/'cover-plan.json');need(plan['matches'],'No exact cover match')
 context=load('hourly_effective_context',DOC/'scripts/quarentenar-wind-waker-station.py')
 cfg,pid,index_path,home=context.load_context();root=Path(cfg['volumeRoot']);folder=root/cfg['platforms']['snes'].get('folder','snes')
 need(str(DLL) in run(['systemctl','show','turborama-station-api.service','-p','ExecStart','--value']),'Effective API release differs')
 need(sha(DLL)=='b3473cc0160169f686501af914cdbf8ebfdf8dcaa4dff768e1106ee8461c9179','Effective DLL differs')
 need(sha(TOOLS/'atualizar-biblioteca-station.py')==plan['compilerSha256'],'Effective importer differs from prepared compiler')
 for name,expected in read(W/'expected-tools.json').items():need(sha(TOOLS/name)==expected,'Effective library module differs from reviewed worktree')
 need(sha('/mnt/DADOS/IMAGENS SNES/controle/colecao.json')==plan['sourceManifestSha256'],'Source manifest changed; prepare a new plan')
 sys.path.insert(0,str(TOOLS));compiler=load('hourly_effective_importer',TOOLS/'atualizar-biblioteca-station.py')
 load('station_online_profiles',TOOLS/'station_online_profiles.py')
 load('station_cartridge',TOOLS/'station_cartridge.py')
 helpers=load('hourly_runtime_helpers','/mnt/DADOS/station-checkup-20261010/checkup.py');values,_=helpers.b.ops.runtime()
 sys.path.insert(0,str(TOOLS))
 need(Path(sys.modules['station_online_profiles'].__file__)==TOOLS/'station_online_profiles.py','Cached profile validator differs from installed module')
 need(sys.modules['station_online_profiles'].MAXIMUM_TRUSTED_ENGINES==128,'Cached profile validator capacity differs')
 protected=[CONFIG,Path(cfg['contentIdentityRegistry'])]+[Path(cfg['autoOnlineProfiles'][k]) for k in ['registry','engineManifest','preparedModes']]
 before={str(p):sha(p) for p in protected};services={n:unit(n) for n in ['turborama-station-api.service','nginx.service','station-network-priority.service','turborama-station-management.service','turborama-suite-api.service','turborama-suite-content-gateway.service','turborama-suite-admin.service']}
 timer_active=subprocess.run(['systemctl','is-active','--quiet',TIMER]).returncode==0;need(timer_active,'Scanner timer must initially be active')
 touched=[];backups={};rollback_links={};committed=False;lock=None;changed=[]
 run(['systemctl','stop',TIMER])
 try:
  deadline=time.monotonic()+60
  while run(['systemctl','show',SCANNER,'-p','ActiveState','--value']) in {'active','activating'}:
   need(time.monotonic()<deadline,'Current scanner did not finish');time.sleep(1)
  lock=(home/'scan.lock').open('a+');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
  index=read(index_path);state=read(home/'state.json');candidate=copy.deepcopy(index);candidate['revision']+=1;byid={r['itemId']:r for r in candidate['items']}
  write('baseline-index.json',index)
  write('baseline.json',dict(utc=datetime.now(timezone.utc).isoformat(),catalogRevision=index['revision'],pid=pid,dllSha256=sha(DLL),protectedSha256=before,services=services,authentication='native Linux pkexec'))
  seed_path=folder/cfg['platforms']['snes']['catalogSeed'] if cfg['platforms']['snes'].get('catalogSeed') else None
  seed=read(seed_path) if seed_path else None;desired={};changes={};stems=set()
  for a in plan['matches']:
   mag=Path(a['magazinePath']);compiled=Path(a['compiledPath']);source=Path(a['sourcePath'])
   need(mag.is_relative_to(folder/'media/revista') and not mag.is_symlink(),'Magazine leaves SNES folder')
   need(sha(mag)==a['oldMagazineSha256'] and sha(compiled)==a['compiledSha256'] and sha(source)==a['sourceSha256'],'Cover input changed since preparation')
   target=home/'covers'/(a['compiledSha256']+'.jpg');need(not target.is_symlink(),'Unsafe immutable cover')
   if not target.exists():atomic(target,compiled.read_bytes(),0,cfg['serviceGid'],0o640)
   need(sha(target)==a['compiledSha256'],'Immutable JPEG differs')
   changes[mag]=compiled.read_bytes();stems.add(mag.stem)
   for g in a['bindings']:
    key=g['sourcePath'];binding=state['sources'][key]
    need(binding['ids']==g['bindingIds'] and stamp(key)==binding['fingerprint'][0],'ROM source binding or stat changed')
    for item in binding['ids']:
     r=byid[item];need(r['platform'] in {'snes','snesbr'} and sha(r['coverPath'])==a['oldMagazineSha256'],'Fresh catalog cover mapping differs')
     r['coverPath']=str(target);r['revision']=candidate['revision'];desired[item]=a['compiledSha256']
    if seed is not None:
     entry=seed['games'].setdefault(compiler.catalog_key(Path(key).stem),{});entry.update(cover=mag.relative_to(folder).as_posix(),coverSha256=a['compiledSha256'])
  if seed_path:changes[seed_path]=raw(seed)
  backup=W/'backup';backup.mkdir(mode=0o700)
  for n,p in enumerate([index_path,home/'state.json']+list(changes)):
   st=p.stat();dest=backup/str(n);dest.write_bytes(p.read_bytes());need(sha(dest)==sha(p),'Backup differs');backups[p]=(dest,st.st_uid,st.st_gid,st.st_mode&0o777,st.st_atime_ns,st.st_mtime_ns)
  write('backup-manifest.json',[dict(path=str(p),backup=str(v[0]),sha256=sha(v[0])) for p,v in backups.items()])
  # Keep original inodes available so rollback also preserves scanner fingerprints.
  for p in backups:
   fd,name=tempfile.mkstemp(prefix='.station-cover-rollback-',suffix='.tmp',dir=p.parent);os.close(fd);os.unlink(name)
   os.link(p,name,follow_symlinks=False);rollback_links[p]=Path(name)
  for p,data in changes.items():
   info=backups[p];touched.append(p);atomic(p,data,info[1],info[2],info[3])
  # Refresh all candidate fingerprints affected by the changed stems, preserving ROM stamps.
  magazines={s:sorted(p for p in (folder/'media/revista').rglob('*') if p.is_file() and not p.is_symlink() and p.stem==s and p.suffix.lower() in {'.jpg','.jpeg','.png','.webp'}) for s in stems}
  for key,binding in state['sources'].items():
   p=Path(key)
   if not p.is_relative_to(folder) or p.stem not in stems or not p.exists():continue
   need(stamp(p)==binding['fingerprint'][0],'Existing ROM stat differs')
   fp=copy.deepcopy(binding['fingerprint']);fp[1]=[[x.relative_to(folder).as_posix(),stamp(x)] for x in magazines[p.stem]]
   if fp!=binding['fingerprint']:binding['fingerprint']=fp;state['observations'][key]=copy.deepcopy(fp);changed.append(key)
  q=W/'scanner-qualification';q.mkdir(mode=0o700);qc=copy.deepcopy(cfg);qc['outputDirectory']=str(q)
  for p in home.glob('*.json'):shutil.copy2(p,q/p.name)
  (q/'index.json').write_bytes(raw(candidate));(q/'state.json').write_bytes(raw(state))
  for key in ['contentIdentityRegistry']:
   p=Path(cfg[key]);shutil.copy2(p,q/p.name);qc[key]=str(q/p.name)
  for key in ['registry','engineManifest','preparedModes']:
   p=Path(cfg['autoOnlineProfiles'][key]);shutil.copy2(p,q/p.name);qc['autoOnlineProfiles'][key]=str(q/p.name)
  for v in qc['platforms'].values():v['artifactDirectory']=str(q/'artifacts')
  deferred=[]
  def refuse_rebuild(*args,**kwargs):
   source=str(Path(args[0]).resolve())
   deferred.append(dict(source=source,existing=source in state['sources']))
   write('deferred-rom-preparation.json',deferred)
   if source in state['sources']:raise RuntimeError('Cover-only operation attempted preparation of existing source; inspect deferred-rom-preparation.json')
   # Leave unrelated unimported inputs pending, without reading their ROM bytes.
   raise compiler.PackagePending('deferred_during_cover_only_publication')
  for n in ['prepare_artifact','prepare_arcade_artifact','prepare_disc_artifact','prepare_raw','prepare_package','prepare_package_archive']:setattr(compiler,n,refuse_rebuild)
  result=compiler.publish(qc)
  need(not result['changed'] and not result['added'] and not result['updated'] and read(q/'index.json')==candidate,'Isolated scanner changed candidate')
  need(all(sha(p)==h for p,h in before.items()),'Protected configuration or online data changed')
  for p,data in [(home/'state.json',raw(state)),(index_path,raw(candidate))]:
   info=backups[p];touched.append(p);atomic(p,data,info[1],info[2],info[3])
  committed=True;write('published.json',dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),previousCatalogRevision=index['revision'],catalogRevision=candidate['revision'],changedMagazines=len(plan['matches']),changedBindings=len(desired),ROMsReadOrCopiedByPublisher=0,apiRestarted=False))
  fcntl.flock(lock,fcntl.LOCK_UN);lock.close();lock=None
  actual=compiler.publish(cfg);need(not actual['changed'] and not actual['added'] and not actual['updated'] and read(index_path)==candidate,'Effective scanner changed published candidate')
  checks=proof(candidate,values,desired)
  need(all(sha(p)==h for p,h in before.items()) and all(unit(n)==v for n,v in services.items()),'Protected data or services changed')
  for old,new in zip(index['items'],candidate['items']):need({k:v for k,v in old.items() if k not in {'coverPath','revision'}}=={k:v for k,v in new.items() if k not in {'coverPath','revision'}},'Non-cover catalog field changed')
  write('applied.json',dict(passed=True,appliedUtc=datetime.now(timezone.utc).isoformat(),previousCatalogRevision=index['revision'],catalogRevision=candidate['revision'],changedMagazines=len(plan['matches']),changedBindings=len(desired),updatedFingerprints=len(changed),local=checks['local'],public=checks['public'],onlineRegistriesAndConfigurationPreserved=True,otherServicesPreserved=True,romCopies=0,romHashes=0,apiRestarted=False,physicalAndroidVisualVerified=False,ownSyntheticLicensesRemoved=True))
  print(json.dumps(dict(passed=True,catalogRevision=candidate['revision'],changedBindings=len(desired))),flush=True)
 except Exception:
  if not committed:
   for p in reversed(touched):
    b,uid,gid,mode,atime_ns,mtime_ns=backups[p]
    link=rollback_links.pop(p);os.replace(link,p);os.utime(p,ns=(atime_ns,mtime_ns))
    directory=os.open(p.parent,os.O_RDONLY|os.O_DIRECTORY)
    try:os.fsync(directory)
    finally:os.close(directory)
  raise
 finally:
  for link in rollback_links.values():
   if link.exists():link.unlink()
  if lock is not None:fcntl.flock(lock,fcntl.LOCK_UN);lock.close()
  if timer_active:run(['systemctl','start',TIMER])
if __name__=='__main__':
 try:main()
 except Exception as e:
  if os.geteuid()==0:write('operation-error.json',dict(utc=datetime.now(timezone.utc).isoformat(),errorType=type(e).__name__,reason=str(e)[:180],publicationExists=(W/'published.json').exists()))
  print(json.dumps(dict(passed=False,errorType=type(e).__name__)),flush=True);raise SystemExit(1)
