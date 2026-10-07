"""Prepare a bounded R71 JNI registration candidate. Does not build/install an APK."""
from pathlib import Path
import argparse,hashlib,json,os
SOURCE_SHA256='fd3c65fbdf928125e77ec3211bddc4ffedf9d3feb085a0ad743b1ec9d2a4a50e'
parser=argparse.ArgumentParser();parser.add_argument('--source',required=True,type=Path);parser.add_argument('--output',required=True,type=Path)
args=parser.parse_args();data=args.source.read_bytes()
if hashlib.sha256(data).hexdigest()!=SOURCE_SHA256:raise SystemExit('Use the exact received R71 StationRetroActivity source')
changes=[
 (b'try{super.onCreate(state);}catch(RuntimeException|LinkageError error)',
  b'try{super.onCreate(state);if(recovery!=null){System.loadLibrary("station_retroarch");stationRecoveryStatus();stationRecoveryStalled();android.util.Log.i("StationRooms","game stage=native-hooks-loaded");}}catch(RuntimeException|LinkageError error)'),
 (b'catch(LinkageError error){unrecoverable("NATIVE_HOOK");}',
  b'catch(LinkageError error){android.util.Log.e("StationRooms","game stage=native-hook-failed type="+error.getClass().getSimpleName());unrecoverable("NATIVE_HOOK");}')]
for old,new in changes:
 if data.count(old)!=1:raise SystemExit('R71 source guard changed')
 data=data.replace(old,new)
args.output.parent.mkdir(parents=True,exist_ok=True)
with args.output.open('xb') as stream:stream.write(data)
print(json.dumps(dict(prepared=True,sourceSHA256=SOURCE_SHA256,candidateSHA256=hashlib.sha256(data).hexdigest(),
 runtimeChanged=False,apkBuilt=False,apkInstalled=False,androidGameplay=False)))
