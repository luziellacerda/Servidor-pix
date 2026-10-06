#!/usr/bin/env python3
"""Publish only the Station code-copy UI, preserving API, keys and licenses."""
import argparse,fcntl,hashlib,json,os,re,shutil,stat,subprocess,tempfile,time
from datetime import datetime,timezone
from http.cookiejar import CookieJar
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request,build_opener,HTTPCookieProcessor
ROOT=Path(__file__).resolve().parents[2]
SITE=Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902')
NAMES=('station-admin.php','station-admin.js')
API=Path('/opt/turborama-station-community-r41-20261006-a2bb176/TurboRamaSuiteOnlineServer.dll')
API_SHA='d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13'
BACKUP=Path('/home/lz-servidor/station-clipboard-backup-20261006')
RESULT=Path('/home/lz-servidor/station-clipboard-result-20261006.json')
URL='https://turbobox.lzgames.com.br'
LOGIN=Path('/home/lz-servidor/turbobox-admin-reset-20261006-122727/credenciais.json')
UNITS=('turborama-station-api.service','turborama-station-management.service','turborama-station-issue-admin.service','turborama-pix.service','turborama-suite-api.service','turborama-suite-admin.service','turborama-suite-content-gateway.service','nginx.service','cloudflared.service','turbobox-php-fpm.service')

def run(args):
 r=subprocess.run(args,capture_output=True,text=True,timeout=15)
 if r.returncode:raise RuntimeError('Bounded command failed')
 return r.stdout.strip()
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def private(p,value):
 fd=os.open(p,os.O_WRONLY|os.O_CREAT|os.O_TRUNC,0o600)
 with os.fdopen(fd,'w') as f:json.dump(value,f,indent=2);f.write('\n')
def services():
 result={}
 for unit in UNITS:
  data=dict(x.split('=',1) for x in run(['systemctl','show',unit,'-p','MainPID','-p','ActiveState']).splitlines())
  result[unit]={'pid':int(data['MainPID']),'active':data['ActiveState']}
  if result[unit]['active']!='active' or result[unit]['pid']<=0:raise ValueError('Required service is unavailable')
 return result
def replace(target,data,mode):
 if target.is_symlink():raise ValueError('File links are not allowed')
 fd,name=tempfile.mkstemp(prefix='.station-copy-',dir=target.parent)
 try:
  os.fchmod(fd,mode)
  with os.fdopen(fd,'wb') as f:f.write(data);f.flush();os.fsync(f.fileno())
  os.replace(name,target)
 finally:Path(name).unlink(missing_ok=True)
def guard(saved):
 if sha(API)!=API_SHA or services()!=saved['services']:raise ValueError('An unrelated runtime changed')
 for name,value in saved['unchanged'].items():
  if sha(SITE/name)!=value:raise ValueError('An unrelated website file changed')
def rollback():
 saved=json.loads((BACKUP/'state.json').read_text());guard(saved)
 for name,meta in saved['files'].items():
  if sha(BACKUP/name)!=meta['before'] or sha(SITE/name) not in (meta['before'],meta['after']):raise ValueError('A later UI release blocks rollback')
 for name,meta in saved['files'].items():
  if sha(SITE/name)==meta['after']:replace(SITE/name,(BACKUP/name).read_bytes(),meta['mode'])
 saved.update(status='rolled-back',rolledBackAt=datetime.now(timezone.utc).isoformat());private(BACKUP/'state.json',saved)
 return {'status':'rolled-back','licensesPreserved':True,'servicesRestarted':0}
def verify_public(manifest):
 ua='Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36'
 browser=build_opener(HTTPCookieProcessor(CookieJar()));logged=False;token=None
 def web(path,fields=None):
  req=Request(URL+path,None if fields is None else urlencode(fields).encode(),{'User-Agent':ua,'Accept':'text/html,application/json'})
  with browser.open(req,timeout=20) as r:
   body=r.read(2*1024*1024+1)
   if len(body)>2*1024*1024:raise ValueError('Response exceeded the bound')
   return r.status,r.url,body
 try:
  status,_,body=web('/station-admin.js?v=20261006-3')
  if status!=200 or hashlib.sha256(body).hexdigest()!=manifest['files']['site/station-admin.js']:raise ValueError('Public copy handler differs')
  if web('/admin/login')[0]!=200:raise ValueError('Administrative login is unavailable')
  credentials=json.loads(LOGIN.read_text())
  status,_,body=web('/auth.php',{'login':credentials['login'],'password':credentials['password']})
  if status!=200 or not json.loads(body).get('ok'):raise ValueError('Known administrator login failed')
  logged=True
  for _ in range(15):
   status,url,body=web('/admin/station');page=body.decode()
   if status==200 and url==URL+'/admin/station' and '/station-admin.js?v=20261006-3' in page:break
   time.sleep(.3)
  else:raise ValueError('Published Station page was not reached')
  if 'confira se aparece Código copiado' not in page:raise ValueError('Copy guidance is absent')
  match=re.search(r'name="csrf" value="([a-f0-9]+)"',page)
  if not match:raise ValueError('Protected page CSRF is absent')
  token=match[1]
  return {'publicJavascriptHashVerified':True,'authenticatedStationPage':True,'copyGuidanceVisible':True}
 finally:
  if logged and token:web('/admin',{'action':'logout','csrf':token})
def apply(package):
 if BACKUP.exists():raise ValueError('Backup already exists; inspect the previous attempt')
 manifest=json.loads((package/'manifest.json').read_text())
 if manifest['site']!=str(SITE) or set(manifest['before'])!=set(NAMES):raise ValueError('Unexpected website scope')
 if run(['git','-C',str(ROOT),'rev-parse','HEAD'])!=manifest['sourceRevision'] or run(['git','-C',str(ROOT),'status','--porcelain']):raise ValueError('Exact clean committed source is required')
 if set(manifest['files'])!={'site/'+name for name in NAMES}|{'deploy-clipboard.py'}:raise ValueError('Unexpected package scope')
 actual={p.relative_to(package).as_posix() for p in package.rglob('*') if p.is_file() and p.name!='manifest.json'}
 if actual!=set(manifest['files']) or any(p.is_symlink() for p in package.rglob('*')):raise ValueError('Unexpected package contents')
 for name,value in manifest['files'].items():
  if sha(package/name)!=value:raise ValueError('Immutable artifact differs')
 if manifest['files']['deploy-clipboard.py']!=sha(__file__):raise ValueError('Publisher differs from the reviewed artifact')
 saved={'sourceRevision':manifest['sourceRevision'],'services':services(),'apiSha256':sha(API),'files':{},'unchanged':{},'status':'prepared'}
 if saved['apiSha256']!=API_SHA:raise ValueError('The inspected API changed')
 for name in ('station-registration.php','station-admin-policy.php','station-admin.css','station-lib.php','lib.php','router.php','auth.php','panel.php','notification-lib.php'):
  saved['unchanged'][name]=sha(SITE/name)
 for name in NAMES:
  target=SITE/name;s=target.stat()
  if target.is_symlink() or s.st_uid!=os.getuid() or s.st_gid!=os.getgid() or sha(target)!=manifest['before'][name]:raise ValueError('Current UI or ownership differs')
  saved['files'][name]={'before':sha(target),'after':manifest['files']['site/'+name],'mode':stat.S_IMODE(s.st_mode)}
 guard(saved);BACKUP.mkdir(mode=0o700)
 for name,meta in saved['files'].items():
  data=(SITE/name).read_bytes();(BACKUP/name).write_bytes(data);(BACKUP/name).chmod(0o600)
  restored=BACKUP/(name+'.restore-check');restored.write_bytes((BACKUP/name).read_bytes());restored.chmod(0o600)
  if sha(restored)!=meta['before']:raise ValueError('File backup restoration failed')
  restored.unlink()
 saved['backupRestoredVerified']=True;private(BACKUP/'state.json',saved)
 try:
  for name,meta in saved['files'].items():
   guard(saved)
   if sha(SITE/name)!=meta['before']:raise ValueError('UI changed during publication')
   replace(SITE/name,(package/'site'/name).read_bytes(),meta['mode'])
  guard(saved)
  if any(sha(SITE/name)!=meta['after'] for name,meta in saved['files'].items()):raise ValueError('Installed UI differs')
  checks=verify_public(manifest);guard(saved)
  saved.update(status='published',publishedAt=datetime.now(timezone.utc).isoformat(),checks=checks,servicesRestarted=0,licensesChanged=0)
  private(BACKUP/'state.json',saved);private(RESULT,saved)
  return {k:saved[k] for k in ('status','sourceRevision','publishedAt','checks','servicesRestarted','licensesChanged')}
 except Exception:
  rollback();raise

if __name__=='__main__':
 parser=argparse.ArgumentParser();group=parser.add_mutually_exclusive_group(required=True)
 group.add_argument('--apply',type=Path);group.add_argument('--rollback',action='store_true');args=parser.parse_args()
 try:
  with open('/home/lz-servidor/.station-clipboard-publish.lock','w') as lock:
   fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
   print(json.dumps(rollback() if args.rollback else apply(args.apply)),flush=True)
 except Exception as error:
  print(json.dumps({'status':'failed','type':type(error).__name__}),flush=True);raise SystemExit(1)
