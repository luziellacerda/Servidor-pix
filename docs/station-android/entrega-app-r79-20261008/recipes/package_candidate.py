"""Package only the two compiled DEX slots and Dreamcast synopsis carousel over R78."""
from pathlib import Path
import copy,datetime,hashlib,importlib.util,json,os,re,shutil,struct,subprocess,zipfile
ROOT=Path(__file__).resolve().parent.parent
R77=ROOT.parent/'station-multiplayer-r77-20261008'
spec=importlib.util.spec_from_file_location('package_helpers',R77/'recipes/package_candidate.py');h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
need,sha,zsha=h.require,h.sha,h.zsha
BASE=Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\package-01\TurboStations-Premium-R78-20261008.apk')
BASE_SHA='e6c6609df32bf419954083297eadbafa75f13328a1516594cc010044b2eb62a5'
WORK=Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008')
def main():
 java=WORK/'java-build-02';native=WORK/'carousel-build';work=WORK/'package-01'
 need(not work.exists(),'Fresh package directory required');need(sha(BASE)==BASE_SHA,'Exact R78 base required')
 jr=json.loads((java/'evidence/build.json').read_text('utf8'));nr=json.loads((native/'evidence/build.json').read_text('utf8'))
 need(jr['compiled'] and nr['compiled'] and nr['baselineReproduced'],'Verified compiled inputs required')
 need(jr['overlayManifestSHA256']==sha(ROOT/'SOURCE-MANIFEST.json'),'Java manifest drift')
 need(jr['buildRecipeSHA256']==sha(ROOT/'recipes/build_java.py') and nr['recipeSHA256']==sha(ROOT/'recipes/build_carousel.py'),'Recipe drift')
 for name,digest in jr['sourceHashes'].items():need(sha(java/'java'/name)==digest,'Staged Java source drift')
 for p in (ROOT/'java').rglob('*.java'):need(sha(p)==jr['sourceHashes'][p.relative_to(ROOT/'java').as_posix()],'Overlay drift')
 for name,digest in nr['overlays'].items():need(sha(ROOT/'native'/name)==digest,'Native overlay drift')
 for name,digest in nr['nativeSources'].items():need(sha(native/'native'/name)==digest,'Staged native drift')
 files={'classes28.dex':java/'java/build/client-dex/classes.dex','classes35.dex':java/'java/build/rooms-dex/classes.dex','lib/arm64-v8a/libturbo_carousel.so':native/'libturbo_carousel.so'}
 expected={'classes28.dex':jr['clientDexSHA256'],'classes35.dex':jr['roomsDexSHA256'],'lib/arm64-v8a/libturbo_carousel.so':nr['nativeSHA256']}
 for name,path in files.items():need(sha(path)==expected[name],'Compiled byte mismatch')
 for test in ['http-tests','bootstrap-tests']:need(json.loads((ROOT/'evidence'/(test+'.json')).read_text('utf8'))['passed'],'Tests required')
 need(shutil.disk_usage(WORK).free>2*BASE.stat().st_size+256*1024**2,'Space required without deleting backups')
 (work/'evidence').mkdir(parents=True);(work/'temp').mkdir();env=dict(os.environ,TEMP=str(work/'temp'),TMP=str(work/'temp'))
 for key in ['STATION_KEYSTORE','STATION_KEY_ALIAS','STATION_KS_PASS','STATION_KEY_PASS']:need(bool(env.get(key)),'Existing signing environment required')
 need(Path(env['STATION_KEYSTORE']).resolve()==h.AUTHORIZED_KEY.resolve(),'Only original authorized key')
 jdk=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin');tools=Path(r'E:\ESTUDO APK\TurboRetroEmu-build\android-build-tools\35.0.0\android-15')
 cert=subprocess.run([str(jdk/'keytool.exe'),'-exportcert','-keystore',env['STATION_KEYSTORE'],'-alias',env['STATION_KEY_ALIAS'],'-storepass:env','STATION_KS_PASS'],env=env,capture_output=True)
 need(cert.returncode==0 and hashlib.sha256(cert.stdout).hexdigest()==h.CERT,'Original certificate required')
 def run(command,label):
  r=subprocess.run(list(map(str,command)),env=env,capture_output=True);(work/'evidence'/(label+'.private.log')).write_bytes(r.stdout+r.stderr);need(r.returncode==0,'Packaging failed '+label);return (r.stdout+r.stderr).decode('utf8','replace')
 def signer(p,label):
  s=run([jdk/'java.exe','-jar',tools/'lib/apksigner.jar','verify','--print-certs',p],label)
  need(re.findall(r'^Signer #\d+ certificate SHA-256 digest: ([a-f0-9]{64})\s*$',s,re.M)==[h.CERT],'Certificate mismatch')
 signer(BASE,'base-cert');h.elf(files['lib/arm64-v8a/libturbo_carousel.so'])
 unsigned=work/'unsigned-r79.apk';signed=work/'TurboStations-Premium-R79-20261008.apk'
 with zipfile.ZipFile(BASE) as old,zipfile.ZipFile(unsigned,'w',allowZip64=True) as new:
  names=[n for n in old.namelist() if not h.signature(n)];need(len(names)==len(set(names))==13226,'Frozen package inventory')
  for info in old.infolist():
   if h.signature(info.filename):continue
   item=copy.copy(info);item.extra=b''
   if item.filename in files:item.file_size=files[item.filename].stat().st_size
   if item.compress_type==zipfile.ZIP_STORED:
    align=16384 if item.filename.endswith('.so') else 4;offset=new.fp.tell()+30+len(item.filename.encode())
    if offset%align:pad=(-(offset+4))%align;item.extra=struct.pack('<HH',0xffff,pad)+bytes(pad)
   with new.open(item,'w') as output,(files[item.filename].open('rb') if item.filename in files else old.open(info)) as input:shutil.copyfileobj(input,output,1024*1024)
 run([tools/'zipalign.exe','-c','-P','16','4',unsigned],'align-unsigned')
 run([jdk/'java.exe','-Djava.io.tmpdir='+str(work/'temp'),'-jar',tools/'lib/apksigner.jar','sign','--alignment-preserved','true','--v4-signing-enabled','false','--ks',env['STATION_KEYSTORE'],'--ks-key-alias',env['STATION_KEY_ALIAS'],'--ks-pass','env:STATION_KS_PASS','--key-pass','env:STATION_KEY_PASS','--out',signed,unsigned],'sign')
 signer(signed,'signed-cert');run([tools/'zipalign.exe','-c','-P','16','4',signed],'align-signed')
 changes=[];videos={};preserved=0
 with zipfile.ZipFile(BASE) as old,zipfile.ZipFile(signed) as new:
  final=[n for n in new.namelist() if not h.signature(n)];need(len(final)==len(set(final)) and set(final)==set(names),'Entry set drift')
  for name in names:
   before,after=zsha(old,name),zsha(new,name);need(after==expected.get(name,before),'Unexpected byte change '+name)
   need(old.getinfo(name).compress_type==new.getinfo(name).compress_type,'Compression drift')
   if before==after:preserved+=1
   else:changes.append(name)
   if name.endswith('.mp4'):videos[name]=after
 need(set(changes)==set(files) and preserved==13223 and len(videos)==59,'Scope mismatch')
 receipt=dict(version='R79',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),temporaryApk=str(signed),sha256=sha(signed),bytes=signed.stat().st_size,baseSHA256=BASE_SHA,certificateSHA256=h.CERT,changes=changes,changedEntrySHA256=expected,preservedEntries=preserved,totalVideos=len(videos),videoHashes=videos,allPackageEntriesVerified=True,alignment16KiB=True,javaReceiptSHA256=sha(java/'evidence/build.json'),nativeReceiptSHA256=sha(native/'evidence/build.json'),recipeSHA256=sha(__file__),runtimeCoresEnginesUnchangedFromR78=True,serverV3ActivationStillRequired=True,installed=False)
 for p in [work/'evidence/package.json',ROOT/'evidence/package.json']:p.write_text(json.dumps(receipt,indent=2)+'\n','utf8')
 # Remove only this recipe's exact temporary unsigned artifact after full verification.
 need(unsigned.resolve().parent==work.resolve() and unsigned.name=='unsigned-r79.apk','Cleanup target');unsigned.unlink()
 print(json.dumps({k:v for k,v in receipt.items() if k!='videoHashes'},indent=2))
if __name__=='__main__':main()
