from pathlib import Path
import subprocess,json,hashlib,datetime
ROOT=Path(__file__).resolve().parent.parent
WORK=Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008\http-tests')
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
BASE=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\java-build-01\java\build\client.jar')
NEW=Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008\java-build-01\java\build\client.jar')
WORK.mkdir(parents=True,exist_ok=True)
rows=[]
for label,jar in [('baseline',BASE),('fixed',NEW)]:
 out=WORK/label;out.mkdir(exist_ok=True)
 c=subprocess.run([str(JDK/'javac.exe'),'-encoding','UTF-8','-cp',str(jar),'-d',str(out),str(ROOT/'tests/HttpRouteProbe.java')],capture_output=True)
 if c.returncode:raise RuntimeError(c.stderr.decode())
 r=subprocess.run([str(JDK/'java.exe'),'-cp',str(jar)+';'+str(out),'org.emulationstation.frontend.station.HttpRouteProbe',label],capture_output=True,text=True,encoding='utf8')
 if r.returncode:raise RuntimeError(r.stderr)
 rows.append(dict(label=label,result=r.stdout.strip(),jarSHA256=hashlib.sha256(jar.read_bytes()).hexdigest()));print(r.stdout,end='')
result=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),passed=True,tests=rows,scope='Actual StationHttp and Cancellation classes from compiled client JAR; no network/Android/server test')
(ROOT/'evidence/http-tests.json').write_text(json.dumps(result,indent=2)+'\n','utf8')
