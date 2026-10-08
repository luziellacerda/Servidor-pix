"""Compile and run only the pure human-departure policy; no APK or phone operations."""
from pathlib import Path
import argparse, datetime, hashlib, json, os, subprocess

ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument('--output',default=r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\presence-tests-01')
args=parser.parse_args();work=Path(args.output).resolve()
allowed=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008').resolve()
if not work.is_relative_to(allowed) or work==allowed or work.exists():raise SystemExit('New child of the R77 E: directory required')
source=ROOT/'java/netplay-src/org/emulationstation/frontend/netplay/StationRoomLeaveIntent.java'
test=ROOT/'tests/StationRoomLeaveIntentTest.java'
rooms=ROOT/'java/netplay-src/org/emulationstation/frontend/netplay/StationRoomsActivity.java'
presence=ROOT/'java/netplay-src/org/emulationstation/frontend/netplay/StationPresence.java'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
hashes={p.relative_to(ROOT).as_posix():sha(p) for p in (source,test,rooms,presence)}
(work/'classes').mkdir(parents=True);(work/'temp').mkdir()
env=dict(os.environ,TEMP=str(work/'temp'),TMP=str(work/'temp'))
jdk=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
def run(command):
    result=subprocess.run(list(map(str,command)),capture_output=True,text=True,encoding='utf8',env=env,timeout=30)
    if result.returncode:raise SystemExit(result.stdout+result.stderr)
    return result.stdout
run([jdk/'javac.exe','-J-Djava.io.tmpdir='+str(work/'temp'),'-encoding','UTF-8','--release','8','-d',work/'classes',source,test])
result=json.loads(run([jdk/'java.exe','-Djava.io.tmpdir='+str(work/'temp'),'-cp',work/'classes','org.emulationstation.frontend.netplay.StationRoomLeaveIntentTest']))
if result.get('passed') is not True:raise SystemExit('Missing success receipt')
current={p.relative_to(ROOT).as_posix():sha(p) for p in (source,test,rooms,presence)}
if current!=hashes:raise SystemExit('Source changed while checking')
result.update(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceHashes=hashes,apkBuilt=False,installed=False)
(work/'result.json').write_text(json.dumps(result,indent=2)+'\n','utf8')
print(json.dumps(result))
