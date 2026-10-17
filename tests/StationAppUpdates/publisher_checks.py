#!/usr/bin/env python3
"""Real Android tooling, disposable synthetic APK/key. Never install the lab APK."""
import argparse
import importlib.util
import hashlib
import json
from pathlib import Path
import subprocess
import struct
import tempfile
from types import SimpleNamespace
import zipfile

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('station_update_publish',ROOT/'ops/station-app-updates/publish.py')
publisher=importlib.util.module_from_spec(spec);spec.loader.exec_module(publisher)

def run(args):
    p=subprocess.run(list(map(str,args)),check=True,capture_output=True,text=True)
    return p.stdout+p.stderr

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk',type=Path,required=True)
    parser.add_argument('--build-tools',default='36.0.0')
    parser.add_argument('--platform',default='android-36')
    args=parser.parse_args();tools=args.sdk/'build-tools'/args.build_tools
    android=args.sdk/'platforms'/args.platform/'android.jar'
    checks=0
    def check(value,name):
        nonlocal checks
        checks+=1
        if not value:raise AssertionError(name)
    with tempfile.TemporaryDirectory(prefix='station-update-publisher-lab-') as temporary:
        root=Path(temporary);keys=[]
        for number in (1,2):
            key=root/('synthetic-'+str(number)+'.p12')
            run(['keytool','-genkeypair','-keystore',key,'-storepass','StationSyntheticOnly','-keypass','StationSyntheticOnly',
                 '-alias','lab','-keyalg','RSA','-keysize','2048','-validity','2','-dname','CN=Disposable Station Update Lab','-noprompt'])
            keys.append(key)
        package=root/'java/org/emulationstation/frontend/station';package.mkdir(parents=True)
        names=('StationAppUpdateActivity','StationAppUpdateService','StationAppUpdateReceiver','StationAppUpdateUi','StationAppUpdater','StationAppRelease','StationAppUpdateFiles')
        for name in names:(package/(name+'.java')).write_text('package org.emulationstation.frontend.station; public class '+name+' {}\n')
        classes=root/'classes';classes.mkdir()
        run(['javac','--release','8','-Xlint:-options','-d',classes,*package.glob('*.java')])
        jar=root/'classes.jar'
        with zipfile.ZipFile(jar,'w') as archive:
            for p in classes.rglob('*.class'):archive.write(p,p.relative_to(classes).as_posix())
        dex=root/'dex';dex.mkdir()
        run(['java','-cp',tools/'lib/d8.jar','com.android.tools.r8.D8','--release','--min-api','26','--lib',android,'--output',dex,jar])
        elf=bytearray(64);elf[:7]=b'\x7fELF\x02\x01\x01';struct.pack_into('<HHI',elf,16,3,183,1);struct.pack_into('<H',elf,52,64)
        core, runtime=bytes(elf)+b'synthetic core',bytes(elf)+b'synthetic runtime'
        identity=dict(engineId='synthetic-v3-engine',library='libsynthetic_core.so',runtimeLibrary='libsynthetic_runtime.so',coreSha256=hashlib.sha256(core).hexdigest(),runtimeSha256=hashlib.sha256(runtime).hexdigest(),recoveryProtocol='station-stream.v3',launchReady=True)
        registry=root/'registered.json';registry.write_text(json.dumps([identity]))
        def apk(version,key=0,components=True):
            manifest=root/('manifest-'+str(version)+'-'+str(key)+'.xml')
            declarations=''.join('<'+tag+' android:name="org.emulationstation.frontend.station.'+name+'" android:exported="false" />' for tag,name in zip(('activity','service','receiver'),names[:3])) if components else ''
            manifest.write_text('<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="'+publisher.PACKAGE+'" android:versionCode="'+str(version)+'" android:versionName="Lab '+str(version)+'"><uses-sdk android:minSdkVersion="26" android:targetSdkVersion="28"/>'+
                ''.join('<uses-permission android:name="android.permission.'+p+'"/>' for p in ('REQUEST_INSTALL_PACKAGES','FOREGROUND_SERVICE','FOREGROUND_SERVICE_DATA_SYNC'))+
                '<application android:label="Do not install synthetic lab">'+declarations+'</application></manifest>')
            unsigned=root/('unsigned-'+str(version)+'-'+str(key)+'.apk')
            run([tools/'aapt2','link','-I',android,'--manifest',manifest,'-o',unsigned])
            with zipfile.ZipFile(unsigned,'a') as archive:
                archive.writestr('assets/station-online/engines.json',json.dumps(dict(engines=[identity])))
                archive.writestr('lib/arm64-v8a/libsynthetic_core.so',core)
                archive.writestr('lib/arm64-v8a/libsynthetic_runtime.so',runtime)
                archive.write(dex/'classes.dex','classes.dex')
            aligned=root/('aligned-'+str(version)+'-'+str(key)+'.apk')
            run([tools/'zipalign','-P','16','4',unsigned,aligned])
            signed=root/('signed-'+str(version)+'-'+str(key)+'.apk')
            run([tools/'apksigner','sign','--ks',keys[key],'--ks-pass','pass:StationSyntheticOnly','--key-pass','pass:StationSyntheticOnly','--out',signed,aligned])
            return signed
        base=apk(1);candidate=apk(2);destination=root/'private-updates'
        destination.mkdir();(destination/'apks').mkdir();destination.chmod(0o777);(destination/'apks').chmod(0o777)
        staged=destination/'apks'/(hashlib.sha256(candidate.read_bytes()).hexdigest()+'.apk');staged.write_bytes(candidate.read_bytes());staged.chmod(0o666)
        options=SimpleNamespace(apk=candidate,trusted_apk=base,registered_profiles=registry,directory=destination,
            apksigner=str(tools/'apksigner'),aapt2=str(tools/'aapt2'),zipalign=str(tools/'zipalign'),notes_file=None,read_group=None)
        try:result=publisher.publish(options)
        except ValueError:
            print(run([tools/'aapt2','dump','badging',candidate]))
            raise
        check(result['published'] and result['signatureVerified'],'signed candidate published')
        current=json.loads((destination/'current.json').read_text())
        check(current['versionCode']==2,'increasing version pointer')
        check((destination/'apks'/(current['sha256']+'.apk')).read_bytes()==candidate.read_bytes(),'immutable bytes complete')
        check((destination/'apks'/(current['sha256']+'.apk')).stat().st_mode&0o777==0o440,'APK private reader mode')
        check((destination/'current.json').stat().st_mode&0o777==0o640,'index private reader mode')
        check(destination.stat().st_mode&0o777==0o750,'existing unsafe parent permissions corrected')
        check((destination/'apks').stat().st_mode&0o777==0o750,'existing unsafe APK directory permissions corrected')
        def reject(name):
            try:publisher.publish(options)
            except (ValueError,subprocess.CalledProcessError):check(True,name);return
            raise AssertionError('accepted '+name)
        reject('same version/downgrade refused')
        options.apk=apk(3,key=1);reject('foreign signer refused')
        options.apk=apk(4,components=False);reject('missing update components refused')
        options.apk=apk(5);registry.write_text('[]');reject('unregistered exact engines refused');registry.write_text(json.dumps([identity]))
        original=(destination/'current.json').read_bytes()
        options.directory=root/'linked';options.directory.symlink_to(destination,target_is_directory=True);reject('symlink storage refused')
        check((destination/'current.json').read_bytes()==original,'failed publication preserves active release')
    print(json.dumps(dict(result='passed',checks=checks,realApksignerAaptD8Zipalign=True,syntheticApksOnly=True,installed=False)))

if __name__=='__main__':main()
