"""Compile the actual presentation helper and execute plain Java tests; no Android fixtures."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,subprocess
ROOT=Path(__file__).resolve().parent.parent
PACKAGE=Path('org/emulationstation/frontend/netplay')
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--work',type=Path,default=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\player-info-tests'));a=parser.parse_args()
    if a.work.drive.upper()!='E:':raise ValueError('Build output belongs on E:')
    src=a.work/'src'/PACKAGE;src.mkdir(parents=True,exist_ok=True);classes=a.work/'classes';classes.mkdir(exist_ok=True)
    production=ROOT/'java/netplay-src'/PACKAGE/'StationGamePlayerInfo.java';test=ROOT/'tests/StationGamePlayerInfoTest.java.in'
    shutil.copyfile(production,src/production.name);shutil.copyfile(test,src/'StationGamePlayerInfoTest.java')
    result=subprocess.run([str(JDK/'javac.exe'),'--release','8','-encoding','UTF-8','-d',str(classes),str(src/production.name),str(src/'StationGamePlayerInfoTest.java')],capture_output=True,text=True,encoding='utf8')
    (a.work/'compile.log').write_text(result.stdout+result.stderr,'utf8')
    if result.returncode:raise RuntimeError(result.stderr)
    run=subprocess.run([str(JDK/'java.exe'),'-ea','-cp',str(classes),'org.emulationstation.frontend.netplay.StationGamePlayerInfoTest'],capture_output=True,text=True,encoding='utf8',timeout=30)
    (a.work/'test.log').write_text(run.stdout+run.stderr,'utf8')
    if run.returncode:raise RuntimeError(run.stdout+run.stderr)
    receipt={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':True,'metrics':json.loads(run.stdout.strip()),'sourceSHA256':sha(production),'testSHA256':sha(test),'recipeSHA256':sha(__file__),'scope':'Actual pure Java presentation helper. No catalog/network/Android/gameplay qualification or authorization grants.'}
    (a.work/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8')
    (ROOT/'tests/player-info-result.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8')
    print(json.dumps(receipt,indent=2))
if __name__=='__main__':main()
