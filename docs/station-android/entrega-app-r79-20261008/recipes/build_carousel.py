"""Reproduce R78 and add description-only, unambiguous Dreamcast title lookup."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,datetime
ROOT=Path(__file__).resolve().parent.parent
BASE=Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\carousel-build')
WORK=Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008\carousel-build')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def need(ok,msg):
 if not ok:raise RuntimeError(msg)
def main():
 need(not WORK.exists(),'Fresh build directory required')
 record=json.loads((BASE/'evidence/build.json').read_text('utf8'))
 need(sha(BASE/'libturbo_carousel.so')==record['nativeSHA256']=='f13c09dcd74aa21aabaf5d966324975de555840f7e6d754bf1aa79d29f50f16a','R78 identity')
 for p,h in record['quotedDependencies'].items():need(sha(p)==h,'R78 dependency drift '+p)
 for p,h in record['objects'].items():need(sha(p)==h,'R78 object drift '+p)
 for d in ['native','tests','evidence','temp']:(WORK/d).mkdir(parents=True)
 env=dict(os.environ,TEMP=str(WORK/'temp'),TMP=str(WORK/'temp'))
 executions=[]
 def run(cmd,label):
  r=subprocess.run(list(map(str,cmd)),capture_output=True,env=env,timeout=300);(WORK/'evidence'/(label+'.log')).write_bytes(r.stdout+r.stderr)
  need(r.returncode==0,label+': '+r.stderr.decode('utf8','replace')[-3000:]);executions.append(dict(label=label,exitCode=r.returncode));return r.stdout.decode('utf8','replace').strip()
 command=record['command'];baseline=list(command);baseline[baseline.index(str(BASE/'libturbo_carousel.so'))]=str(WORK/'baseline-r78.so')
 run(baseline,'baseline');need(sha(WORK/'baseline-r78.so')==record['nativeSHA256'],'R78 reproduction mismatch')
 for name,h in record['nativeSources'].items():need(sha(BASE/'native'/name)==h,'Source changed');shutil.copyfile(BASE/'native'/name,WORK/'native'/name)
 for p in (ROOT/'native').glob('*'):shutil.copyfile(p,WORK/'native'/p.name)
 compiler=Path(r'C:\Program Files\LLVM\bin\clang++.exe')
 run([compiler,'-std=c++17','-Wall','-Wextra','-Werror','-Wno-misleading-indentation','-finput-charset=UTF-8','-fexec-charset=UTF-8','-I',WORK/'native',ROOT/'tests/dreamcast.cpp','-o',WORK/'tests/dreamcast.exe'],'test-compile')
 result=run([WORK/'tests/dreamcast.exe'],'test-run');need(result.startswith('PASS'),'Dreamcast tests')
 command=[str(WORK/'native/native_carousel.cpp') if v==str(BASE/'native/native_carousel.cpp') else str(WORK/'libturbo_carousel.so') if v==str(BASE/'libturbo_carousel.so') else v for v in command]
 run(command,'carousel')
 out=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),compiled=True,baseNativeSHA256=record['nativeSHA256'],baselineReproduced=True,nativeSHA256=sha(WORK/'libturbo_carousel.so'),nativeSources={p.name:sha(p) for p in (WORK/'native').iterdir()},overlays={p.name:sha(p) for p in (ROOT/'native').iterdir()},recipeSHA256=sha(__file__),command=command,tests=result,executions=executions,baseReceiptSHA256=sha(BASE/'evidence/build.json'),physicalChecked=False)
 (WORK/'evidence/build.json').write_text(json.dumps(out,indent=2)+'\n','utf8');print(json.dumps({k:v for k,v in out.items() if k not in ['nativeSources','command']},indent=2))
if __name__=='__main__':main()
