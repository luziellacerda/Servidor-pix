"""Exercise the actual pure Java catalogue evidence evaluator; no game files or network."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,subprocess
ROOT=Path(__file__).resolve().parent.parent
PACKAGE=Path('org/emulationstation/frontend/station')
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 p=argparse.ArgumentParser();p.add_argument('--work',type=Path,default=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog-evidence-tests'));a=p.parse_args()
 assert a.work.drive.upper()=='E:'
 src=a.work/'src'/PACKAGE;src.mkdir(parents=True,exist_ok=True);classes=a.work/'classes';classes.mkdir(exist_ok=True)
 actual=ROOT/'java/client/src/java'/PACKAGE/'StationCatalogPlayerEvidence.java';test=ROOT/'tests/StationCatalogPlayerEvidenceTest.java.in'
 shutil.copyfile(actual,src/actual.name);shutil.copyfile(test,src/'StationCatalogPlayerEvidenceTest.java')
 def run(argv,label):
  result=subprocess.run(list(map(str,argv)),capture_output=True,text=True,encoding='utf8',timeout=60)
  (a.work/(label+'.log')).write_text(result.stdout+result.stderr,'utf8')
  if result.returncode:raise RuntimeError(result.stdout+result.stderr)
  return result.stdout
 run([JDK/'javac.exe','--release','8','-encoding','UTF-8','-d',classes,src/actual.name,src/'StationCatalogPlayerEvidenceTest.java'],'compile')
 metrics=json.loads(run([JDK/'java.exe','-ea','-cp',classes,'org.emulationstation.frontend.station.StationCatalogPlayerEvidenceTest'],'tests'))
 record={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':True,'metrics':metrics,'sourceSHA256':sha(actual),'testSHA256':sha(test),'recipeSHA256':sha(__file__),'scope':'Actual pure Java display evidence helper. No source claims or game-content qualification.'}
 (a.work/'receipt.json').write_text(json.dumps(record,indent=2)+'\n','utf8');(ROOT/'tests/catalog-evidence-result.json').write_text(json.dumps(record,indent=2)+'\n','utf8');print(json.dumps(record,indent=2))
if __name__=='__main__':main()
