#!/usr/bin/env python3
"""Publish reviewed Neo Geo data and the isolated scanner; keep the API running."""
import argparse,base64,hashlib,importlib.util,json,os,re,shutil,subprocess,sys,time,zipfile
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from cryptography.hazmat.primitives import serialization
ROOT=Path(__file__).resolve().parents[3];SCRIPTS=Path(__file__).resolve().parent
SERVICE='turborama-station-api.service';SCAN='turborama-station-library-scan.service';TIMER='turborama-station-library-scan.timer'
HOME=Path('/mnt/DADOS/turbostation-library-auto-20261004')
CONFIG=Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
UNIT=Path('/etc/systemd/system')/SCAN
BACKUP=Path('/mnt/DADOS/station-neogeo-backup-20261005')
CONTENT=Path('/mnt/DADOS/turbostation-neogeo-content-20261005')
RESULT=Path('/home/lz-servidor/station-neogeo-rollout-result-20261005.json')
API=Path('/opt/turborama-station-folders-20261004-931030b/TurboRamaSuiteOnlineServer.dll')
API_SHA='0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0'
INDEX_SHA='f56cf70267251ade518ae111be88627db83d592436b7fb3bf522c119e8fefb8c'
SHARED=['turborama-pix.service','turborama-suite-api.service','turborama-suite-admin.service',
 'turborama-suite-content-gateway.service','nginx.service','cloudflared.service',
 'postgresql@16-main.service','redis-server.service','php8.3-fpm.service','turbobox-php-fpm.service',
 'turborama-station-management.service','turborama-station-issue-admin.service']
def module(name):
 spec=importlib.util.spec_from_file_location(name,SCRIPTS/(name+'.py'));obj=importlib.util.module_from_spec(spec);spec.loader.exec_module(obj);return obj
sys.dont_write_bytecode = True
sys.path.insert(0,str(SCRIPTS))
ops=module('implantar-station-20261003');library=module('atualizar-biblioteca-station');helper=module('verificar-http-release-station')
def result(data):
 ops.replace_config(RESULT,json.dumps(data,indent=2,ensure_ascii=False)+'\n');os.chown(RESULT,1000,1000);RESULT.chmod(0o600)
def validate_api():
 pid=ops.run(['systemctl','show',SERVICE,'-p','MainPID','--value']).strip()
 assert str(API).encode() in Path('/proc/'+pid+'/cmdline').read_bytes() and ops.digest(API)==API_SHA,'API release changed'
 return int(pid)
def restore_data():
 meta=json.loads((BACKUP/'backup.json').read_text())
 current=json.loads((HOME/'index.json').read_text());old=json.loads((BACKUP/'index.json').read_text())
 # Monotonic catalog revision allows live rollback without restarting the API.
 old['revision']=max(old['revision'],current['revision'])+1
 for name,path in [('config.json',CONFIG),('scan.service',UNIT)]:
  ops.replace_config(path,(BACKUP/name).read_text());path.chmod(meta['modes'][name])
 library.atomic_json(HOME/'state.json',json.loads((BACKUP/'library-state.json').read_text()))
 library.atomic_json(HOME/'index.json',old,meta['gid'])
 ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','start',TIMER])
 return old['revision']
def verify_public(index,values):
 public=serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(),password=None).public_key()
 pepper=base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes().strip(),validate=True)
 db=ops.database_name(values)
 class Client(helper.StationReleaseVerification):
  session=None
  def request(self,base,method,route,payload=None,bearer=None):
   if method=='GET' and route=='/v1/station/catalog' and bearer:
    deadline=time.monotonic()+90
    while True:
     response=super().request('http://127.0.0.1:5192',method,route,bearer=bearer)
     if self.signed(response,'catalog')['revision']==index['revision']:break
     if time.monotonic()>=deadline:raise TimeoutError('live catalog reload timed out')
     time.sleep(1)
   return super().request(base,method,route,payload,bearer)
  def signed(self,response,domain):
   payload=super().signed(response,domain)
   if domain=='session':self.session=payload
   return payload
 client=Client(lambda statement:ops.sql(db,statement),pepper,public,index);base='https://app.lzgames.com.br'
 try:
  client.create();contract=client.check(base);bearer=client.session['accessToken']
  meta=client.signed(client.request(base,'GET','/v1/station/catalog?metadata=1',bearer=bearer),'catalog')
  expected={r['itemId']:r for r in index['items']}
  assert meta['revision']==index['revision']
  assert all(r['metadata']==expected[r['itemId']]['metadata'] and r['folderPath']==expected[r['itemId']].get('folderPath',[]) for r in meta['items'])
  rows=[r for r in index['items'] if r['platform']=='neogeo']
  def cover(row):
   response=client.request(base,'GET','/v1/station/covers/'+row['coverId'],bearer=bearer)
   assert response[0]==200 and response[2]==Path(row['coverPath']).read_bytes()
   return len(response[2])
  start=time.monotonic()
  with ThreadPoolExecutor(4) as workers:sizes=list(workers.map(cover,rows))
  covers={'verified':len(rows),'workers':4,'bytes':sum(sizes),'elapsedMs':round((time.monotonic()-start)*1000,2)}
  chosen=min(rows,key=lambda r:r['artifact']['sizeBytes'])
  grant=client.signed(client.request(base,'POST','/v1/station/downloads/authorize',dict(client.identity,domain=helper.PREFIX+'request-download/v1',itemId=chosen['itemId']),bearer),'download-grant')
  response=client.request(base,'GET','/v1/station/artifacts/'+grant['grantId'],bearer=bearer)
  assert response[0]==200 and hashlib.sha256(response[2]).hexdigest()==chosen['artifact']['sha256']
  import io
  with zipfile.ZipFile(io.BytesIO(response[2])) as z:assert z.namelist()==[chosen['artifact']['launchPath'],'neogeo.zip']
  return dict(contract=contract,metadataAndFoldersVerified=True,covers=covers,closedZipAndBiosDownloadVerified=True)
 finally:
  assert client.cleanup(),'Synthetic ownership cleanup failed'
def apply(revision):
 report={'applied':False,'sourceRevision':revision,'apiRestarted':False}
 published=False;timer_stopped=False
 try:
  assert os.geteuid()==0,'root required'
  env=dict(os.environ,GIT_OPTIONAL_LOCKS='0')
  git=['git','-c','safe.directory='+str(ROOT)]
  assert subprocess.check_output(git+['rev-parse','HEAD'],cwd=ROOT,env=env,text=True).strip()==revision
  assert not subprocess.check_output(git+['status','--porcelain'],cwd=ROOT,env=env,text=True).strip(),'clean exact source required'
  pid=validate_api();values,ids=ops.runtime();gid=ids['Gid'][1]
  assert values['Station__LibraryIndexFile']==str(HOME/'index.json') and ops.digest(HOME/'index.json')==INDEX_SHA
  assert not BACKUP.exists() and not CONTENT.exists() and not RESULT.exists(),'already prepared'
  baseline={u:ops.state(u) for u in SHARED}
  assert all('ActiveState=active' in s for s in baseline.values()),'shared service unhealthy'
  scanner=UNIT.read_text();assert '/opt/turborama-station-folders-20261004-931030b/' in scanner
  ops.run(['systemctl','stop',TIMER]);timer_stopped=True;ops.run(['systemctl','stop',SCAN])
  BACKUP.mkdir(mode=0o700)
  files={'index.json':HOME/'index.json','library-state.json':HOME/'state.json','config.json':CONFIG,'scan.service':UNIT}
  modes={}
  for name,path in files.items():
   shutil.copyfile(path,BACKUP/name);(BACKUP/name).chmod(0o600);modes[name]=path.stat().st_mode&0o777
  restored=BACKUP/'restored';restored.mkdir(mode=0o700)
  for name,path in files.items():
   shutil.copyfile(BACKUP/name,restored/name);assert ops.digest(restored/name)==ops.digest(path)
  meta=dict(gid=gid,modes=modes,apiPid=pid,shared=baseline,oldIndexSha256=INDEX_SHA)
  ops.private_text(BACKUP/'backup.json',json.dumps(meta))
  report['backupRestoreVerified']=True
  original=json.loads((BACKUP/'index.json').read_text());config=json.loads(CONFIG.read_text())
  root=Path(config['volumeRoot']);neogeo=root/'neogeo'
  assert neogeo.is_dir() and not (root/'snes/neogeo').exists()
  games=[p for p in neogeo.rglob('*.zip') if p.name!='neogeo.zip']
  assert len(games)==190 and (neogeo/'neogeo.zip').is_file(),'reviewed collection changed'
  config['platforms']['neogeo']={'extensions':['.zip'],'artifactMode':'arcade-set','companions':['neogeo.zip']}
  CONTENT.mkdir(mode=0o750);os.chown(CONTENT,0,gid)
  for name in ['index.json','state.json']:
   library.atomic_json(CONTENT/name,json.loads((HOME/name).read_text()),gid)
  preview=dict(config,outputDirectory=str(CONTENT))
  import_report=library.publish(preview,bootstrap=True);index=json.loads((CONTENT/'index.json').read_text())
  before={r['itemId']:r for r in original['items']};after={r['itemId']:r for r in index['items']}
  assert all(after[k]==v for k,v in before.items()),'existing game changed'
  additions=[r for r in index['items'] if r['itemId'] not in before]
  assert len(additions)==190 and all(r['platform']=='neogeo' for r in additions) and import_report['placeholderCovers']==0
  assert not any(r['platform']=='neogeo' for r in import_report['pending']),'Neo Geo pending'
  assert len(index['items'])==len(original['items'])+190
  checker=Path('/mnt/DADOS/station-neogeo-check-20261005/StationLibraryAuto.Tests.dll')
  validation=subprocess.run(['/usr/bin/dotnet',str(checker),str(CONTENT/'index.json')],user=ids['Uid'][1],group=gid,extra_groups=ids['Groups'],capture_output=True,text=True,timeout=180)
  assert validation.returncode==0 and 'VALIDATED revision=9 visible=2163 compatibility=255' in validation.stdout,'real UID parser validation failed'
  report['realServiceIdentityValidated']=True
  target=Path('/opt/turborama-station-library-neogeo-20261005-'+revision[:7]);target.mkdir(mode=0o750);os.chown(target,0,gid)
  manifest={}
  for name in ['atualizar-biblioteca-station.py','station_revista.py','preparar-indice-artefatos.py']:
   dest=target/name;shutil.copyfile(SCRIPTS/name,dest);dest.chmod(0o640);os.chown(dest,0,gid);manifest[name]=ops.digest(dest)
   assert manifest[name]==ops.digest(SCRIPTS/name)
  ops.private_text(target/'release.json',json.dumps({'sourceRevision':revision,'files':manifest}))
  report['scannerManifest']=manifest
  config_text=json.dumps(config,ensure_ascii=False,separators=(',',':'))+'\n'
  ops.replace_config(CONFIG,config_text);CONFIG.chmod(modes['config.json'])
  updated,n=re.subn(r'^ExecStart=.*$', 'ExecStart=/usr/bin/python3 '+str(target/'atualizar-biblioteca-station.py')+' --config '+str(CONFIG),scanner,flags=re.M)
  assert n==1
  ops.replace_config(UNIT,updated);UNIT.chmod(modes['scan.service'])
  library.atomic_json(HOME/'state.json',json.loads((CONTENT/'state.json').read_text()))
  library.atomic_json(HOME/'index.json',index,gid);published=True
  ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','start',SCAN],timeout=180)
  assert json.loads((HOME/'report.json').read_text())['changed'] is False,'unchanged scan changed catalog'
  ops.run(['systemctl','start',TIMER])
  # Signed local polling waits for checksum validation, then HTTPS proves publication.
  report['publicVerification']=verify_public(index,values)
  assert validate_api()==pid and {u:ops.state(u) for u in SHARED}==baseline,'process changed'
  report.update(applied=True,catalogRevision=index['revision'],visible=2163,neogeo=190,existingIdsAndArtifactsPreserved=True,
                apiPidPreserved=True,sharedServicesUnchanged=True,timerActive=True,indexSha256=ops.digest(HOME/'index.json'),
                synopses=sum(bool(r.get('metadata',{}).get('description')) for r in index['items'] if r.get('catalogVisible',True)),
                syntheticRowsRemoved=True)
  result(report);print(json.dumps(report))
 except Exception as failure:
  report['failureType']=type(failure).__name__
  if published:
   ops.run(['systemctl','stop',TIMER]);ops.run(['systemctl','stop',SCAN])
   report['rollbackCatalogRevision']=restore_data();report['rolledBack']=True
  elif BACKUP.exists() and (BACKUP/'backup.json').exists():
   meta=json.loads((BACKUP/'backup.json').read_text())
   for name,path in [('config.json',CONFIG),('scan.service',UNIT)]:
    ops.replace_config(path,(BACKUP/name).read_text());path.chmod(meta['modes'][name])
   ops.run(['systemctl','daemon-reload']);ops.run(['systemctl','start',TIMER])
  elif timer_stopped:ops.run(['systemctl','start',TIMER])
  result(report);raise
def rollback():
 validate_api();state=json.loads(RESULT.read_text());assert state.get('applied') and not state.get('rolledBack')
 assert ops.digest(HOME/'index.json')==state['indexSha256'],'catalog has advanced; preserve later imports'
 ops.run(['systemctl','stop',TIMER]);ops.run(['systemctl','stop',SCAN]);rev=restore_data()
 state.update(rolledBack=True,rollbackCatalogRevision=rev);result(state)
 print('Own scanner/configuration restored; catalog revision '+str(rev)+'; API remained running.')
if __name__=='__main__':
 parser=argparse.ArgumentParser();action=parser.add_mutually_exclusive_group(required=True);action.add_argument('--apply',action='store_true');action.add_argument('--rollback',action='store_true');parser.add_argument('--source-revision')
 args=parser.parse_args()
 if args.apply:apply(args.source_revision)
 else:rollback()
