"""Run the actual R77 profile/wire/start/coordinator classes with explicit transport fixtures."""
from pathlib import Path
import argparse, hashlib, json, shutil, subprocess
from datetime import datetime, timezone

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\java-contract-tests')
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
JSON=Path(r'E:\ESTUDO APK\work\turbostations-reconstruction-20261002\tools\json-20250517.jar')
PACKAGE=Path('org/emulationstation/frontend/netplay')
PRODUCTION=['StationMultiplayerProfile.java','StationMultiplayerWire.java','StationMultiplayerSession.java','StationRoomStartState.java']

def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work',type=Path,default=WORK)
    a=parser.parse_args(); src=a.work/'src'/PACKAGE; src.mkdir(parents=True,exist_ok=True)
    hashes={}
    for name in PRODUCTION:
        original=ROOT/'java/netplay-src'/PACKAGE/name
        hashes[str(original.relative_to(ROOT))]=digest(original)
        shutil.copyfile(original,src/name)
    for template in HERE.glob('*.java.in'):
        if template.name not in ['StationMultiplayerContractTest.java.in','StationMultiplayerFixture.java.in']:continue
        shutil.copyfile(template,src/template.name[:-3])
        hashes['tests/'+template.name]=digest(template)
    classes=a.work/'classes'; classes.mkdir(exist_ok=True)
    compile=subprocess.run([str(JDK/'javac.exe'),'--release','17','-encoding','UTF-8','-cp',str(JSON),'-d',str(classes),*[str(p) for p in src.glob('*.java')]],capture_output=True,text=True,encoding='utf8')
    (a.work/'compile.log').write_text(compile.stdout+compile.stderr,'utf8')
    if compile.returncode:raise RuntimeError(compile.stdout+compile.stderr)
    run=subprocess.run([str(JDK/'java.exe'),'-ea','-cp',str(classes)+';'+str(JSON),'org.emulationstation.frontend.netplay.StationMultiplayerContractTest'],capture_output=True,text=True,encoding='utf8',timeout=90)
    (a.work/'test.log').write_text(run.stdout+run.stderr,'utf8')
    if run.returncode:raise RuntimeError(run.stdout+run.stderr)
    metrics=json.loads(run.stdout.strip().splitlines()[-1])
    result={'utc':datetime.now(timezone.utc).isoformat(),'passed':True,'sources':hashes,'recipeSHA256':digest(__file__),'jsonJarSHA256':digest(JSON),'javaSHA256':digest(JDK/'java.exe'),'metrics':metrics,
        'realCode':PRODUCTION,'models':['StationMultiplayerTunnel lifecycle/availability/callbacks and socket binding','TLS factory and OnlineGame error/hash utility'],
        'limitations':['No Android, network, native core or physical gameplay in this suite','Transport coordinator callbacks use a controlled fake tunnel; transport mutex safety requires separate real-tunnel tests','No invented profile is an approval of any catalog game']}
    (a.work/'receipt.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf8')
    print(run.stdout,end='');print('receipt='+str(a.work/'receipt.json'))
if __name__=='__main__':main()
