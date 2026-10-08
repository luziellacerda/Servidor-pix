"""Run exact R77 snapshot merge methods without Android/API mocks or network access."""
from pathlib import Path
import argparse,hashlib,json,subprocess
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parent.parent
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
JSON=Path(r'E:\ESTUDO APK\work\turbostations-reconstruction-20261002\tools\json-20250517.jar')
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\snapshot-merge-tests')
def sha(data):return hashlib.sha256(data).hexdigest()
def extract(text,signature):
 start=text.index(signature);level=0
 for i in range(text.index('{',start),len(text)):
  if text[i]=='{':level+=1
  elif text[i]=='}':
   level-=1
   if level==0:return text[start:i+1]
 raise ValueError(signature)
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,default=WORK);a=p.parse_args();a.work.mkdir(parents=True,exist_ok=True)
 path=ROOT/'java/netplay-src/org/emulationstation/frontend/netplay/StationOnlineClient.java';raw=path.read_bytes();text=raw.decode('utf8')
 methods=[extract(text,s) for s in ['private synchronized void acceptMultiplayer(JSONObject next)','synchronized JSONObject compose(JSONObject social)','synchronized JSONObject multiplayerRoom(String id)']]
 source='import org.json.*;class Merge {boolean multiplayerEnabled=true;int page;private JSONObject socialSnapshot,multiplayerSnapshot;private long combinedRevision;private String presentationKey="";void offer(JSONObject value)throws Exception{acceptMultiplayer(value);}\n'+'\n'.join(methods)+'\n}\n'+(ROOT/'tests/StationSnapshotMergeTest.java.in').read_text('utf8')
 generated=a.work/'StationSnapshotMergeTest.java';generated.write_text(source,'utf8');classes=a.work/'classes';classes.mkdir(exist_ok=True)
 compile=subprocess.run([str(JDK/'javac.exe'),'--release','17','-encoding','UTF-8','-cp',str(JSON),'-d',str(classes),str(generated)],capture_output=True,text=True,encoding='utf8');(a.work/'compile.log').write_text(compile.stdout+compile.stderr,'utf8')
 if compile.returncode:raise RuntimeError(compile.stdout+compile.stderr)
 run=subprocess.run([str(JDK/'java.exe'),'-cp',str(classes)+';'+str(JSON),'StationSnapshotMergeTest'],capture_output=True,text=True,encoding='utf8',timeout=30);(a.work/'test.log').write_text(run.stdout+run.stderr,'utf8')
 if run.returncode:raise RuntimeError(run.stdout+run.stderr)
 result=json.loads(run.stdout.strip().splitlines()[-1]);receipt={'utc':datetime.now(timezone.utc).isoformat(),'passed':True,'sourceSHA256':sha(raw),'methodSHA256':[sha(s.encode()) for s in methods],'generatedSHA256':sha(source.encode()),'recipeSHA256':sha(Path(__file__).read_bytes()),'metrics':result,'scope':'three actual snapshot merge/revision methods, JSON-only fixtures; no signed HTTP or Android in this suite'};(a.work/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8');print(run.stdout,end='')
if __name__=='__main__':main()
