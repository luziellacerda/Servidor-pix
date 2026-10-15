from pathlib import Path
from datetime import datetime,timezone
import json,subprocess,time,os,shutil
W=Path(__file__).resolve().parent
os.umask(0o077)
def save(n,v):
 p=W/n;p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n');p.chmod(0o600)
def pending(p):
 try:
  comm=Path(f'/proc/{p.pid}/comm').read_text().strip()
  argv=Path(f'/proc/{p.pid}/cmdline').read_bytes().split(b'\0')
  return comm=='pkexec' and argv[0] in (b'/usr/bin/pkexec',b'pkexec') and not (W/'native-started.json').exists()
 except (FileNotFoundError,PermissionError):return False
if any((W/n).exists() for n in ['native-started.json','published.json','applied.json']):
 raise SystemExit('Protected operation evidence exists; reconcile and prepare a new attempt directory')
if (W/'authentication-request.json').exists():
 archive=W/'authentication-history'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
 archive.mkdir(parents=True,mode=0o700)
 for name in ['authentication-request.json','authentication-result.json','authentication-progress.json','native-output.log']:
  if (W/name).exists():shutil.copy2(W/name,archive/name)
started=time.monotonic()
with (W/'native-output.log').open('wb') as log:
 p=subprocess.Popen(['/usr/bin/pkexec','/usr/bin/python3','-B',str(W/'publish-covers.py')],stdout=log,stderr=log)
 save('authentication-request.json',dict(utc=datetime.now(timezone.utc).isoformat(),pid=p.pid,method='native pkexec',maximumAuthenticationWaitSeconds=115))
 while p.poll() is None:
  elapsed=time.monotonic()-started
  if (W/'native-started.json').exists():
   save('authentication-progress.json',dict(nativeOperationStarted=True,authenticationWaitSeconds=round(elapsed,2)))
   break
  if elapsed>=115:
   if pending(p):
    p.terminate()
    try:p.wait(timeout=2)
    except subprocess.TimeoutExpired:
     if pending(p):p.kill();p.wait(timeout=2)
    save('authentication-result.json',dict(authenticated=False,waitSeconds=round(time.monotonic()-started,2),ownUnauthenticatedRequestCancelled=True,rootOperationInterrupted=False,exitCode=p.poll()))
    print(json.dumps(dict(authenticated=False,requestCancelled=True)),flush=True)
    raise SystemExit(0)
   save('authentication-progress.json',dict(authenticationPhaseEnded=True,ownRequestNotCancelled=True,processCommandChanged=True))
   break
  time.sleep(0.2)
 rc=p.wait()
 save('authentication-result.json',dict(authenticated=(W/'native-started.json').exists(),elapsedSeconds=round(time.monotonic()-started,2),ownUnauthenticatedRequestCancelled=False,rootOperationInterrupted=False,exitCode=rc,publicationExists=(W/'published.json').exists(),appliedExists=(W/'applied.json').exists()))
 print(json.dumps(dict(authenticated=(W/'native-started.json').exists(),exitCode=rc,publicationExists=(W/'published.json').exists(),appliedExists=(W/'applied.json').exists())),flush=True)
