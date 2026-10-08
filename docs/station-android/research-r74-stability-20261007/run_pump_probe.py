"""Pinned R74 Java queue scheduling reproduction; not an APK/runtime build."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
DEFAULT_SOURCE = Path(r'E:\ESTUDO APK\work\station-session-lifecycle-r74-20261007\compiled-final\java\netplay-src\org\emulationstation\frontend\netplay')
DEFAULT_WORK = Path(r'E:\ESTUDO APK\work\station-neogeo-collection-map-r75-20261007\transport-probe')
JDK = Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
TUNNEL_SHA = '8af5ceb585498512ff9ff4e1409a46ee2ed9dca9ee66bcb0c43bc11fa7817896'
WIRE_SHA = 'cb7e83ee84c1297c91c23bc24c47f7eb34204ce8ffda933f71d4b27bbc418a20'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def extract(text, signature):
    begin = text.index(signature)
    start = text.index('{', begin)
    level = 0
    for at in range(start, len(text)):
        if text[at] == '{': level += 1
        elif text[at] == '}':
            level -= 1
            if level == 0: return text[begin:at+1]
    raise AssertionError('method not closed')

def once(text, old, new):
    assert text.count(old) == 1, repr(old)
    return text.replace(old, new)

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, default=DEFAULT_SOURCE)
    p.add_argument('--work', type=Path, default=DEFAULT_WORK)
    args = p.parse_args()
    tunnel = (args.source / 'StationRecoveryTunnel.java').read_bytes()
    wire = (args.source / 'StationRecoveryWire.java').read_bytes()
    assert sha(tunnel) == TUNNEL_SHA and sha(wire) == WIRE_SHA
    text = tunnel.decode('utf-8')
    original = extract(text, 'private void pump()')
    producer = 'synchronized(gate){tx.append(tx.next,Arrays.copyOf(bytes,count));readySent=-1;gate.notifyAll();}pump();'
    assert text.count(producer) == 1
    instrumented = once(original, 'if(frame==null)return;', 'if(frame==null){probeNoFrame();return;}')
    candidate = once(instrumented,
        'if(closed.get()||!pumpPending.compareAndSet(false,true))return;',
        'if(closed.get())return;pumpRequested.set(true);if(!pumpPending.compareAndSet(false,true))return;')
    candidate = once(candidate, 'for(;;){Remote current;', 'for(;;){pumpRequested.set(false);Remote current;')
    candidate = once(candidate,
        'finally{pumpPending.set(false);}',
        'finally{pumpPending.set(false);if(pumpRequested.get()&&!closed.get())pump();}')
    classes = []
    for name, method in [('BaselinePump', instrumented), ('CandidatePump', candidate)]:
        classes.append('final class '+name+' extends PumpProbeBase {\n'+method+'\n'
            '@Override void signal(){pump();}\n'
            '@Override void produce(byte[] bytes)throws Exception {int count=bytes.length;'+producer+'}\n}')
    template = (HERE / 'PumpRaceProbe.java.in').read_text(encoding='utf-8')
    generated = once(template, '@GENERATED_CLASSES@', '\n'.join(classes))
    args.work.mkdir(parents=True, exist_ok=True)
    package = args.work / 'src/org/emulationstation/frontend/netplay'
    package.mkdir(parents=True, exist_ok=True)
    (package / 'PumpRaceProbe.java').write_text(generated, encoding='utf-8', newline='\n')
    (package / 'StationRecoveryWire.java').write_bytes(wire)
    classes_dir = args.work / 'classes'
    classes_dir.mkdir(exist_ok=True)
    command = [str(JDK/'javac.exe'), '-encoding', 'UTF-8', '-d', str(classes_dir),
        str(package/'StationRecoveryWire.java'), str(package/'PumpRaceProbe.java')]
    compiled = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', check=True)
    ran = subprocess.run([str(JDK/'java.exe'), '-cp', str(classes_dir),
        'org.emulationstation.frontend.netplay.PumpRaceProbe'], capture_output=True,
        text=True, encoding='utf-8', check=True, timeout=40)
    (args.work/'compile.log').write_text(compiled.stdout+compiled.stderr, encoding='utf-8')
    (args.work/'probe.log').write_text(ran.stdout+ran.stderr, encoding='utf-8')
    metrics = json.loads(ran.stdout.strip().splitlines()[-1])
    receipt = {'utc':datetime.now(timezone.utc).isoformat(), 'sourceTunnelSHA256':sha(tunnel),
        'sourceWireSHA256':sha(wire), 'extractedPumpSHA256':sha(original.encode()),
        'extractedProducerSHA256':sha(producer.encode()),
        'generatedProbeSHA256':sha(generated.encode()),
        'recipeSHA256':sha(Path(__file__).read_bytes()),
        'templateSHA256':sha((HERE/'PumpRaceProbe.java.in').read_bytes()),
        'testOnlySchedulingHook':'after frame==null decision, before return/finally; CountDownLatch',
        'models':['WebSocket remote.send sink', 'nativeStatus=0', 'outQueue=0'],
        'realCode':['extracted pump()', 'tx producer fragment', 'StationRecoveryWire with Bytes'],
        'limitations':['No Android/network/server/phone', 'Does not measure real tick delay or prove physical disconnect causality', 'Candidate exists only inside generated host fixture'],
        'metrics':metrics, 'compiler':str(JDK/'javac.exe'), 'javaRuntime':str(JDK/'java.exe')}
    (args.work/'receipt.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(ran.stdout,end='')
    print('receipt='+str(args.work/'receipt.json'))

if __name__=='__main__': main()
