"""Cross-language real HTTPS/WSS/TCP tests of R77 Java Session/Tunnel against candidate C# routes.

The fixture uses generated identities/certificates on loopback. No phone, public
server, game or production secret is accessed. Native readiness is modeled.
"""
from pathlib import Path
import argparse,datetime,hashlib,json,os,shutil,subprocess,time
ROOT=Path(__file__).resolve().parent.parent
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
JSON=Path(r'E:\ESTUDO APK\work\turbostations-reconstruction-20261002\tools\json-20250517.jar')
ANDROID=Path(r'G:\Android\Sdk\platforms\android-34\android.jar')
R76=Path(r'E:\ESTUDO APK\work\station-pump-wakeup-r76-20261008\compiled-final\java\build')
DOTNET=Path(r'C:\Program Files\dotnet\dotnet.exe')
PKG=Path('org/emulationstation/frontend/netplay')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--players',type=int,nargs='+',default=[2,3,4]);a=parser.parse_args();out=a.output.resolve()
 assert out.drive.upper()=='E:' and not out.exists(),'Use a new E: test directory';assert all(n in (2,3,4) for n in a.players)
 out.mkdir(parents=True)
 for folder in ['temp','classes','server-bin','dotnet-home','packages','http-cache']:(out/folder).mkdir()
 env=dict(os.environ,TEMP=str(out/'temp'),TMP=str(out/'temp'),DOTNET_CLI_HOME=str(out/'dotnet-home'),DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',NUGET_PACKAGES=str(out/'packages'),NUGET_HTTP_CACHE_PATH=str(out/'http-cache'))
 flags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0
 def run(cmd,label,timeout=180):
  r=subprocess.run(list(map(str,cmd)),cwd=out,env=env,capture_output=True,text=True,encoding='utf8',errors='replace',timeout=timeout,creationflags=flags);(out/(label+'.log')).write_text(r.stdout+r.stderr,'utf8')
  if r.returncode:raise RuntimeError(label+' failed: '+r.stdout[-1000:]+r.stderr[-2000:])
  return r.stdout
 server=out/'server';shutil.copytree(ROOT/'server/src',server/'src',ignore=shutil.ignore_patterns('obj','bin'))
 tests=server/'tests';tests.mkdir();shutil.copyfile(ROOT/'tests/MultiplayerJavaLab.cs',tests/'MultiplayerJavaLab.cs');shutil.copyfile(ROOT/'server/tests/Directory.Build.props',tests/'Directory.Build.props')
 project=tests/'MultiplayerJavaLab.csproj';project.write_text('<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="MultiplayerJavaLab.cs"/><ProjectReference Include="../src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj"/></ItemGroup></Project>','utf8')
 config=out/'NuGet.Config';config.write_text('<configuration><packageSources><clear /></packageSources></configuration>','utf8')
 server_hashes={str(p.relative_to(server)).replace('\\','/'):sha(p) for p in sorted((server/'src').rglob('*')) if p.is_file()}
 run([DOTNET,'restore',project,'--configfile',config,'--source',r'C:\Users\Admin\.nuget\packages','--packages',out/'packages','-p:NuGetAudit=false'],'server-restore')
 run([DOTNET,'build',project,'--no-restore','-c','Release','-o',out/'server-bin','-p:NuGetAudit=false'],'server-build')
 src=out/'java'/PKG;src.mkdir(parents=True);production={}
 for name in ['StationMultiplayerTunnel.java','StationMultiplayerSession.java','StationMultiplayerWire.java']:
  original=ROOT/'java/netplay-src'/PKG/name;shutil.copyfile(original,src/name);production[str(original.relative_to(ROOT))]=sha(original)
 shutil.copyfile(ROOT/'tests/StationMultiplayerTransportTest.java',src/'StationMultiplayerTransportTest.java')
 proof=ROOT.parent/'station-online-recovery-r67-20261007/tests/StationRecoveryProofTest.java';shutil.copyfile(proof,src/'StationRecoveryProofTest.java')
 jars=[R76/'client.jar',R76/'rooms.jar'];classpath=os.pathsep.join(map(str,[JSON,ANDROID,*jars]))
 run([JDK/'javac.exe','--release','17','-encoding','UTF-8','-cp',classpath,'-d',out/'classes',*src.glob('*.java')],'java-build')
 results=[]
 for count in a.players:
  fixture=out/('private-'+str(count)+'.properties');process=None
  try:
   with (out/('server-'+str(count)+'.log')).open('w',encoding='utf8') as log:
    process=subprocess.Popen([str(DOTNET),str(out/'server-bin/MultiplayerJavaLab.dll'),str(fixture)],cwd=out,env=env,stdout=log,stderr=subprocess.STDOUT,stdin=subprocess.PIPE,creationflags=flags)
    deadline=time.monotonic()+45
    while not fixture.exists():
     if process.poll() is not None or time.monotonic()>deadline:raise RuntimeError('Synthetic HTTPS listener unavailable; inspect private server log')
     time.sleep(.1)
    text=run([JDK/'java.exe','-Djava.io.tmpdir='+str(out/'temp'),'-cp',str(out/'classes')+os.pathsep+classpath,'org.emulationstation.frontend.netplay.StationMultiplayerTransportTest',fixture,count],'transport-'+str(count),180)
    result=json.loads(next(line for line in reversed(text.splitlines()) if line.startswith('{')));assert result['passed'];results.append(result)
  finally:
   if process is not None and process.poll() is None:
    try:process.communicate(input=b'\n',timeout=15)
    except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
   # Only a generated synthetic credential file in this owned test directory.
   if fixture.exists():fixture.unlink()
 receipt={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':True,'results':results,'javaSources':production,'serverSources':server_hashes,'serverDLLSHA256':sha(out/'server-bin/TurboRamaSuiteOnlineServer.dll'),'fixtureSources':{p.name:sha(p) for p in [ROOT/'tests/StationMultiplayerTransportTest.java',ROOT/'tests/MultiplayerJavaLab.cs',proof]},'dependencyJars':{p.name:sha(p) for p in [*jars,JSON,ANDROID]},'recipeSHA256':sha(__file__),'limits':['Native ACK/readiness and sockets simulate emulator behavior; no Android/ROM gameplay','TLS trusts only a generated lab certificate; production pins/endpoints unchanged','Loopback latency is not public Internet latency','Actual production Java Session/Tunnel and vendored WebSocket transport execute; only StationRelayTls dependency points to synthetic listener']}
 (out/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8');print(json.dumps({'passed':True,'players':a.players,'checks':sum(v['checks'] for v in results),'tcpBytesVerified':sum(v['tcpBytesVerified'] for v in results),'receipt':str(out/'receipt.json')}))
if __name__=='__main__':main()
