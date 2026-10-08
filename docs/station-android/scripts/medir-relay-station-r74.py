#!/usr/bin/env python3
"""Private, bounded Station diagnostics; no packet payloads, addresses or credentials."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time
from datetime import datetime, timezone
from urllib.request import urlopen
from urllib.error import HTTPError, URLError

SERVICE = 'turborama-station-api.service'

def run(command):
    value = subprocess.run(command, capture_output=True, text=True, timeout=15)
    if value.returncode:
        raise ValueError('Read-only command failed')
    return value.stdout

def utc():
    return datetime.now(timezone.utc).isoformat()

def request(path):
    try:
        with urlopen('http://127.0.0.1:5192' + path, timeout=5) as response:
            data = response.read(16 * 1024 * 1024 + 1)
            if len(data) > 16 * 1024 * 1024:
                raise ValueError('Bounded diagnostics response exceeded')
            return json.loads(data)
    except (HTTPError, URLError) as error:
        return dict(available=False, errorType=type(error).__name__)

def os_sample():
    pid = int(run(['systemctl', 'show', SERVICE, '-p', 'MainPID', '--value']))
    process = Path('/proc/' + str(pid) + '/stat').read_text().rsplit(')', 1)[1].split()
    fields = dict(line.split(':', 1) for line in Path('/proc/' + str(pid) + '/status').read_text().splitlines() if ':' in line)
    cpu = [int(v) for v in Path('/proc/stat').read_text().splitlines()[0].split()[1:9]]
    memory = dict(line.split(':', 1) for line in Path('/proc/meminfo').read_text().splitlines())
    nic = Path('/sys/class/net/enp6s0/statistics')
    counters = {name: int((nic / name).read_text()) for name in ('rx_bytes','tx_bytes','rx_errors','tx_errors','rx_dropped','tx_dropped')}
    lines = Path('/proc/net/snmp').read_text().splitlines()
    tcp = {}
    for i, line in enumerate(lines[:-1]):
        if line.startswith('Tcp:') and 'RetransSegs' in line:
            tcp = dict(zip(line.split()[1:], map(int, lines[i+1].split()[1:])))
    return dict(utc=utc(), monotonicNs=time.monotonic_ns(), pid=pid,processTicks=int(process[11])+int(process[12]),
                hostTicks=sum(cpu), hostIdleTicks=cpu[3]+cpu[4], rssKiB=int(fields['VmRSS'].split()[0]),
                threads=int(fields['Threads']), memAvailableKiB=int(memory['MemAvailable'].split()[0]),nic=counters,
                tcpRetransSegsSystem=tcp.get('RetransSegs'),tcpEstablishedSystem=tcp.get('CurrEstab'))

def sample(previous=None):
    value = os_sample()
    if previous and value['pid'] == previous['pid']:
        seconds = (value['monotonicNs']-previous['monotonicNs'])/1e9
        host_ticks = value['hostTicks']-previous['hostTicks']
        value['interval'] = dict(seconds=seconds,stationCpuPercentOfOneCore=(value['processTicks']-previous['processTicks'])/os.sysconf('SC_CLK_TCK')/seconds*100,
            hostIdlePercent=(value['hostIdleTicks']-previous['hostIdleTicks'])/host_ticks*100 if host_ticks else None,
            nicRxMbps=(value['nic']['rx_bytes']-previous['nic']['rx_bytes'])*8/seconds/1e6,
            nicTxMbps=(value['nic']['tx_bytes']-previous['nic']['tx_bytes'])*8/seconds/1e6,
            nicErrorsAndDropsDelta=sum(value['nic'][k]-previous['nic'][k] for k in ('rx_errors','tx_errors','rx_dropped','tx_dropped')),
            tcpRetransmittedSegmentsSystemDelta=value['tcpRetransSegsSystem']-previous['tcpRetransSegsSystem'])
    sockets = run(['ss','-Htin','( sport = :5192 or dport = :5192 )'])
    value['stationLoopbackTcp'] = dict(rttMs=[float(n) for n in re.findall(r'\brtt:([0-9.]+)',sockets)],
        sendQueueBytes=sum(int(line.split()[2]) for line in sockets.splitlines() if line.startswith('ESTAB')),
        receiveQueueBytes=sum(int(line.split()[1]) for line in sockets.splitlines() if line.startswith('ESTAB')),
        retransCounters=[int(n) for n in re.findall(r'\bretrans:\d+/(\d+)',sockets)],
        scope='Only local Station TCP hops; not phone or Cloudflare WAN RTT')
    value['ready'] = request('/ready/station/online')
    value['relay'] = request('/ready/station/online/diagnostics')
    return value

def timeline(since):
    datetime.fromisoformat(since.replace(' UTC','+00:00'))
    raw = run(['journalctl','--unit',SERVICE,'--since',since,'--no-pager','-o','json','-n','10000'])
    if len(raw)>16*1024*1024:
        raise ValueError('Journal bound exceeded')
    controls, ends, errors = [], [], []
    for line in raw.splitlines():
        entry=json.loads(line);message=entry.get('MESSAGE','')
        stamp=datetime.fromtimestamp(int(entry['__REALTIME_TIMESTAMP'])/1e6,timezone.utc).isoformat()
        fields=dict(re.findall(r'(?:^|\s)([A-Za-z]+)=([^\s]+)',message))
        if 'Station online control utc=' in message:
            controls.append((stamp,fields))
        if 'Station recovery event=first-transport-end' in message:
            ends.append(dict(utc=stamp,**{k:fields.get(k) for k in ('utcMs','generation','streamEpoch','role','cause','closeCode','exceptionType',
                'hostHeartbeatAgeMs','clientHeartbeatAgeMs','acceptedBytes','deliveredBytes','pendingBytes','state',
                'hostAcceptedBytes','hostDeliveredBytes','clientAcceptedBytes','clientDeliveredBytes')}))
        if any(marker in message for marker in ('OutOfMemoryException','Unhandled exception','Application is shutting down','fail:')):
            errors.append(dict(utc=stamp,markers=[marker for marker in ('OutOfMemoryException','Unhandled exception','Application is shutting down','fail:') if marker in message]))
    starts=[item for item in controls if item[1].get('action')=='start']
    selected=starts[-1] if starts else None
    correlation=selected[1].get('correlation') if selected else None
    chosen=[dict(utc=stamp,**{k:fields.get(k) for k in ('generation','role','action','proofMode','hostHeartbeatAgeMs','clientHeartbeatAgeMs','commandElapsedMs')})
        for stamp,fields in controls if correlation is not None and fields.get('correlation')==correlation]
    return dict(sinceUtc=since,roomStarts=len(starts),selectedTrialStartUtc=selected[0] if selected else None,
        selectedTrialControls=chosen[-300:],controlsTruncated=len(chosen)>300,transportEndsAllRooms=ends[-100:],runtimeMarkers=errors[-100:],
        serverJournalHasPerFrameHistory=False,fullRoomAndDeviceIdentitiesOmitted=True)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,required=True);parser.add_argument('--since')
    parser.add_argument('--samples',type=int,default=1);parser.add_argument('--interval',type=float,default=5)
    args=parser.parse_args()
    if not args.output.is_absolute() or args.output.exists() or args.output.is_symlink() or not 1<=args.samples<=120 or not 1<=args.interval<=10:
        raise ValueError('New private path and bounded sampling required')
    values=[]
    for _ in range(args.samples):
        if values:time.sleep(args.interval)
        values.append(sample(values[-1] if values else None))
    result=dict(utc=utc(),readOnly=True,scope='Passive server observations; OS/NIC counters shared with all products; not physical gameplay qualification',
        samples=values,processChanged=len({v['pid'] for v in values})!=1,timeline=timeline(args.since) if args.since else None)
    with args.output.open('x') as destination:
        os.fchmod(destination.fileno(),0o600);json.dump(result,destination,indent=2);destination.write('\n')
    print(json.dumps(dict(passed=True,record=str(args.output),pid=values[-1]['pid'],samples=len(values),diagnosticsAvailable=values[-1]['relay'].get('available',True))))

if __name__=='__main__':main()
