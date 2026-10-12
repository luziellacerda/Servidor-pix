#!/usr/bin/python3
"""Fetch bounded Git updates; invoke authenticated Codex only on new relevant input."""
import argparse,datetime,fcntl,glob,hashlib,json,os,re,subprocess,sys,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent
CFG=json.loads((HERE/'config.json').read_text())
os.umask(0o077)
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def save(path,value):
 fd,tmp=tempfile.mkstemp(prefix='.state-',dir=path.parent)
 try:
  with os.fdopen(fd,'w') as f:json.dump(value,f,ensure_ascii=False,indent=2);f.write('\n');f.flush();os.fsync(f.fileno())
  os.replace(tmp,path)
 finally:
  if os.path.exists(tmp):os.unlink(tmp)
def git(repo,*args):
 p=subprocess.run(['git','-c','gc.auto=0',*args],cwd=repo['path'],stdin=subprocess.DEVNULL,capture_output=True,text=True,timeout=180)
 if p.returncode:
  known=('could not resolve','failed to connect','couldn\'t connect','timed out','authentication failed','cannot lock ref','permission denied','dubious ownership','unable to access')
  cause=next((message for message in known if message in p.stderr.lower()),'unclassified Git error')
  raise RuntimeError('Git operation failed for '+repo['name']+' (exit '+str(p.returncode)+'; '+cause+')')
 return p.stdout
def observe(fetch):
 result={}
 for repo in CFG['repositories']:
  if fetch:git(repo,'fetch','--no-tags',repo['url'],'+refs/heads/*:refs/remotes/origin/*')
  refs=git(repo,'for-each-ref','--format=%(refname:short)\t%(objectname)\t%(committerdate:unix)','refs/remotes/origin').splitlines()
  candidates=[]
  for line in refs:
   ref,sha,stamp=line.split('\t');stamp=int(stamp)
   if repo['name']=='covers':
    if ref in ('origin/main','origin/capas-super-nintendo'):result[repo['name']+':'+ref]=sha
    continue
   prefixes=repo.get('incomingPrefixes',[repo.get('incomingPrefix','origin/docs/station-')])
   if not any(ref.startswith(prefix) for prefix in prefixes):continue
   revision=re.search(r'(?:^|[-/])r(\d+)(?:[-/]|$)',ref,re.I)
   if revision:candidates.append((int(revision[1]),stamp,ref,sha))
  if candidates:
   # Each release supersedes its predecessors. Avoid older native-engine identities.
   newest=max(x[1] for x in candidates)
   for revision,stamp,ref,sha in candidates:
    if stamp==newest:result[repo['name']+':'+ref]=sha
 private=[]
 roots=list(CFG['privateInputs'])
 for pattern in CFG.get('privateKitGlobs',[]):roots.extend(glob.glob(pattern))
 for root in sorted(set(roots)):
  folder=Path(root)
  if folder.is_dir():
   for p in sorted(folder.rglob('*')):
    if p.is_file() and not p.is_symlink():
     s=p.stat();private.append([str(p.relative_to(folder)),s.st_size,s.st_mtime_ns])
  elif folder.is_file() and not folder.is_symlink():
   s=folder.stat();private.append([folder.name,s.st_size,s.st_mtime_ns])
 result['privateInputFingerprint']=hashlib.sha256(json.dumps(private,separators=(',',':')).encode()).hexdigest()
 return result
def main():
 a=argparse.ArgumentParser();a.add_argument('--check-only',action='store_true');a.add_argument('--no-fetch',action='store_true');args=a.parse_args()
 observations=observe(not args.no_fetch)
 fingerprint=hashlib.sha256(json.dumps(observations,sort_keys=True).encode()).hexdigest()
 statepath=HERE/'state.json';state=json.loads(statepath.read_text()) if statepath.exists() else {}
 event=dict(utc=now(),fingerprint=fingerprint,inputs=observations,newInput=fingerprint!=state.get('handledFingerprint'))
 save(HERE/'last-check.json',event)
 with (HERE/'maintenance.lock').open('a+') as lock:
  try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
  except BlockingIOError:print(json.dumps(dict(checked=True,newInput=event['newInput'],status='maintenance-already-running')));return
  if args.check_only or not event['newInput']:
   print(json.dumps(dict(checked=True,newInput=event['newInput'],agentStarted=False)));return
  run=HERE/'runs'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');run.mkdir(parents=True,mode=0o700)
  save(run/'event.json',event)
  context=(HERE/'CONTINUACAO.txt').read_text()
  prompt=(HERE/'prompt.txt').read_text()+'\nCONTINUACAO:\n'+context+'\nEVENTO:\n'+json.dumps(event,ensure_ascii=False)+'\nCONFIG:\n'+json.dumps(CFG,ensure_ascii=False)
  report=run/'result.json'
  cmd=[CFG['codex'],'exec','--ephemeral','--color','never','--json','--sandbox','danger-full-access','-c','approval_policy="never"','-C',CFG['workingDirectory'],'--output-schema',str(HERE/'result-schema.json'),'-o',str(report),'-']
  with (run/'events.jsonl').open('w') as stdout,(run/'stderr.log').open('w') as stderr:
   p=subprocess.run(cmd,input=prompt,text=True,stdout=stdout,stderr=stderr)
  if p.returncode!=0 or not report.is_file():
   save(HERE/'last-result.json',dict(utc=now(),status='failed',exitCode=p.returncode,reportDirectory=str(run)))
   raise RuntimeError('Codex failed; input remains pending')
  result=json.loads(report.read_text())
  if result.get('status') not in ('complete','partial','blocked'):raise ValueError('Invalid completion report')
  # Completed and blocked inputs are acknowledged separately. A new Git/kit input
  # resumes work; pending work is NEVER mislabeled as completed.
  state.update(handledFingerprint=fingerprint,lastAttemptUtc=now(),status=result['status'],pending=result['pending'],lastReport=str(report))
  if result['status']=='complete':state['completedFingerprint']=fingerprint
  save(statepath,state);save(HERE/'last-result.json',dict(utc=now(),**result,reportDirectory=str(run)))
  print(json.dumps(dict(checked=True,agentStarted=True,status=result['status'],pendingCount=len(result['pending']))))
if __name__=='__main__':
 try:main()
 except Exception as e:
  save(HERE/'last-error.json',dict(utc=now(),errorType=type(e).__name__,message=str(e)));print(str(e),file=sys.stderr);raise SystemExit(1)
