"""Build/test the checked-in candidate locally; never checkout, deploy or restart a service."""
import argparse,hashlib,json,os,pathlib,shutil,subprocess

ROOT=pathlib.Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument('--output',default=r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\server')
parser.add_argument('--dotnet',default=r'C:\Program Files\dotnet\dotnet.exe')
parser.add_argument('--skip-tests',action='store_true')
args=parser.parse_args()
out=pathlib.Path(args.output).resolve()
allowed=pathlib.Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008').resolve()
if out==allowed or not out.is_relative_to(allowed):raise SystemExit('Output must be a child of the explicit local R77 workspace.')
manifest=json.loads((ROOT/'SOURCE-MANIFEST.json').read_text(encoding='utf8'))
if manifest['baseCommit']!='ab192bf1585e30f303d041f13b36a1f9c96d2caa':raise SystemExit('Wrong source base')
for entry in manifest['files']:
    source=(ROOT/entry['path']).resolve()
    if not source.is_relative_to(ROOT):raise SystemExit('Escaping source path')
    if hashlib.sha256(source.read_bytes()).hexdigest()!=entry['sha256']:raise SystemExit('Source hash mismatch: '+entry['path'])
for entry in manifest['files']:
    target=(out/entry['path']).resolve()
    if not target.is_relative_to(out):raise SystemExit('Escaping target path')
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(ROOT/entry['path'],target)
out.mkdir(parents=True,exist_ok=True)
nuget=out/'NuGet.Config'
nuget.write_text('<configuration><packageSources><clear/><add key="cached" value="C:\\Users\\Admin\\.nuget\\packages"/></packageSources></configuration>\n',encoding='utf8')
env=dict(os.environ,DOTNET_CLI_HOME=str(out/'.dotnet'),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',NUGET_PACKAGES=str(out/'.nuget-packages'))
def run(arguments):
    result=subprocess.run([args.dotnet,*arguments],cwd=out,env=env,text=True,encoding='utf8',errors='replace',capture_output=True)
    if result.returncode:
        print(result.stdout);print(result.stderr);raise SystemExit(result.returncode)
    return result.stdout
run(['build','src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj','--configfile',str(nuget),'--nologo','-v','minimal'])
checks=[]
if not args.skip_tests:
    for project in ['tests/StationMultiplayer.Tests.csproj','tests/StationMultiplayer.Http.Tests.csproj','tests/StationRecovery/StationRecovery.Tests.csproj']:
        output=run(['run','--project',project,'--no-launch-profile','--property:RestoreConfigFile='+str(nuget),'-v','minimal'])
        rows=[json.loads(line) for line in output.splitlines() if line.startswith('{"passed":')]
        if len(rows)!=1 or rows[0]['passed'] is not True:raise SystemExit('Missing success receipt: '+project)
        checks.append(dict(project=project,**rows[0]))
receipt={'baseCommit':manifest['baseCommit'],'sourceManifestSha256':hashlib.sha256((ROOT/'SOURCE-MANIFEST.json').read_bytes()).hexdigest(),'buildPassed':True,'tests':checks,'deployed':False,'productionTouched':False,'androidGameplayTested':False}
(out/'candidate-checks.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf8')
print(json.dumps(receipt))
