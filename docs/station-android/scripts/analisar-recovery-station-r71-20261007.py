#!/usr/bin/env python3
"""Read-only Station R71 analysis. Output is aggregate, with no identities/secrets."""
import argparse,hashlib,json,os,pwd,re,statistics,subprocess,time
from collections import Counter
from datetime import datetime,timezone
from pathlib import Path
from urllib.request import urlopen

EXPECTED='/opt/turborama-station-recovery-r71-20261007-ab192bf/TurboRamaSuiteOnlineServer.dll'
SHA='815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243'
SERVICE='turborama-station-api.service'

def run(args):
    r=subprocess.run(args,text=True,capture_output=True,timeout=15)
    if r.returncode:raise ValueError('Read-only observation failed')
    return r.stdout

def snapshot():
    with urlopen('http://127.0.0.1:5192/ready/station/online',timeout=5) as r:return json.load(r)

def analyze(since):
    if not re.fullmatch(r'2026-10-07 [0-9]{2}:[0-9]{2}:[0-9]{2} UTC',since):raise ValueError('Bounded UTC interval required')
    pid=int(run(['systemctl','show',SERVICE,'-p','MainPID','--value']))
    command=Path('/proc/'+str(pid)+'/cmdline').read_bytes().split(b'\0')
    if EXPECTED.encode() not in command or hashlib.sha256(Path(EXPECTED).read_bytes()).hexdigest()!=SHA:
        raise ValueError('Qualified R71 release was superseded')
    status={k:v.strip() for line in Path('/proc/'+str(pid)+'/status').read_text().splitlines()
        for k,_,v in [line.partition(':')] if k in ['VmRSS','Threads']}
    def cpu():
        process=Path('/proc/'+str(pid)+'/stat').read_text().rsplit(')',1)[1].split()
        machine=[int(x) for x in Path('/proc/stat').read_text().splitlines()[0].split()[1:]]
        return int(process[11])+int(process[12]),machine
    before=snapshot();start=time.monotonic();p0,c0=cpu();time.sleep(1);p1,c1=cpu();elapsed=time.monotonic()-start
    total=sum(c1)-sum(c0);idle=(c1[3]+c1[4])-(c0[3]+c0[4])
    cpu_percent=(p1-p0)/os.sysconf('SC_CLK_TCK')/elapsed*100
    memory={k:v.strip() for line in Path('/proc/meminfo').read_text().splitlines()
        for k,_,v in [line.partition(':')] if k in ['MemTotal','MemAvailable']}
    raw=run(['journalctl','--unit',SERVICE,'--since',since,'--no-pager','-o','json','-n','10000'])
    if len(raw)>16*1024*1024:raise ValueError('Journal observation exceeded bound')
    actions=Counter();roles=Counter();proofs=Counter();times=[];events=[];errors=Counter();messages=0;commands=[];relay_http=[]
    for line in raw.splitlines():
        entry=json.loads(line);message=entry.get('MESSAGE','');messages+=1
        for code in re.findall(r'STATION_[A-Z_]+',message):errors[code]+=1
        if 'Station online control utc=' in message:
            def field(name):
                m=re.search(r'(?:^|\s)'+name+r'=([^\s]+)',message);return m[1] if m else None
            action=field('action');role=field('role');proof=field('proofMode');latency=field('commandElapsedMs')
            if action:actions[action]+=1
            if role:roles[role]+=1
            if proof:proofs[proof]+=1
            if latency:times.append(float(latency))
            if action not in ['heartbeat','enter']:
                item={name:field(name) for name in ['generation','role','action','proofMode','hostHeartbeatAgeMs','clientHeartbeatAgeMs','commandElapsedMs']}
                item['utc']=datetime.fromtimestamp(int(entry['__REALTIME_TIMESTAMP'])/1e6,timezone.utc).isoformat();commands.append(item)
        if 'Request finished' in message and '/v1/station/online/relay' in message:
            m=re.search(r'/v1/station/online/relay\s+-\s+([0-9]{3}).*?([0-9.]+)ms',message)
            if m:relay_http.append(dict(status=int(m[1]),elapsedMs=float(m[2]),utc=datetime.fromtimestamp(int(entry['__REALTIME_TIMESTAMP'])/1e6,timezone.utc).isoformat()))
        if 'Station recovery event=first-transport-end' in message:
            event={}
            for name in ['utcMs','generation','streamEpoch','role','cause','closeCode','exceptionType','hostHeartbeatAgeMs',
                'clientHeartbeatAgeMs','acceptedBytes','deliveredBytes','pendingBytes','state',
                'hostAcceptedBytes','hostDeliveredBytes','clientAcceptedBytes','clientDeliveredBytes']:
                m=re.search(r'(?:^|\s)'+name+r'=([^\s]+)',message)
                if m:event[name]=m[1]
            events.append(event)
    after=snapshot()
    if int(run(['systemctl','show',SERVICE,'-p','MainPID','--value']))!=pid:raise ValueError('Process changed during observation')
    metrics={}
    if times:
        ordered=sorted(times);metrics=dict(count=len(times),p50Ms=statistics.median(times),
            p95Ms=ordered[max(0,int(len(times)*.95)-1)],maximumMs=max(times))
    return dict(utc=datetime.now(timezone.utc).isoformat(),sinceUtc=since,pid=pid,activeDllSha256=SHA,
        readOnly=True,roomsBefore=before,roomsAfter=after,stationMemory=status,machineMemory=memory,
        oneSecondSample=dict(stationCpuPercentOfOneCore=round(cpu_percent,2),machineIdlePercent=round(idle/total*100,2) if total else None),
        journalMessages=messages,commandActions=dict(actions),commandRoles=dict(roles),proofModes=dict(proofs),
        commandServerTime=metrics,commandSequence=commands,relayHttpResponses=relay_http,errorCodes=dict(errors),firstTransportEnds=events,
        nativeEmulatorFramesMeasured=False,endToEndNetworkLatencyMeasured=False)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--since',required=True);args=parser.parse_args()
    if os.geteuid()!=0 or os.environ.get('PKEXEC_UID')!='1000':raise SystemExit('Native Linux operator authentication required')
    value=analyze(args.since);folder=Path('/mnt/DADOS/station-r71-rollout-check-20261007')
    name=folder/('analysis-'+datetime.now(timezone.utc).strftime('%H%M%S')+'.json')
    with name.open('x') as f:
        os.fchmod(f.fileno(),0o600);json.dump(value,f,indent=2);f.write('\n')
    owner=pwd.getpwnam('lz-servidor');os.chown(name,owner.pw_uid,owner.pw_gid)
    print(json.dumps(value),flush=True);print(json.dumps(dict(record=str(name))),flush=True)
