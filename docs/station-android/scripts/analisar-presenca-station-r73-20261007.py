#!/usr/bin/env python3
"""Read-only R73 presence timeline; never emits peer IDs or credentials."""
import argparse
import importlib.util
import json
import os
import pwd
import re
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path


def observe(since, trial_start=None):
    path = Path(__file__).with_name('analisar-recovery-station-r71-20261007.py')
    spec = importlib.util.spec_from_file_location('station_read_only', path)
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    value = base.analyze(since)
    raw = base.run(['journalctl', '--unit', base.SERVICE, '--since', since,
                    '--no-pager', '-o', 'json', '-n', '10000'])
    if len(raw) > 16 * 1024 * 1024:
        raise ValueError('Journal observation exceeded bound')
    controls, statuses, http = [], Counter(), []
    for line in raw.splitlines():
        entry = json.loads(line)
        message = entry.get('MESSAGE', '')
        utc = datetime.fromtimestamp(int(entry['__REALTIME_TIMESTAMP']) / 1e6,
                                     timezone.utc).isoformat()
        if 'Station online control utc=' in message:
            fields = dict(re.findall(r'(?:^|\s)([A-Za-z]+)=([^\s]+)', message))
            controls.append((utc, fields))
        if 'Request finished' in message:
            match = re.search(r'\s(POST|GET)\s+\S*?(/v1/station/(?:challenges|sessions|online/command|online/events))\s+-\s+([0-9]{3}).*?([0-9.]+)ms', message)
            if match:
                method, route, status, elapsed = match.groups()
                statuses[(route, int(status))] += 1
                if route in ('/v1/station/challenges', '/v1/station/sessions') or int(status) >= 400:
                    http.append(dict(utc=utc, method=method, path=route,
                                     status=int(status), elapsedMs=float(elapsed)))
    starts = [(utc, fields) for utc, fields in controls if fields.get('action') == 'start']
    chosen = starts[-1] if starts else None
    if trial_start is not None:
        matches = [entry for entry in starts if entry[0] == trial_start]
        if len(matches) != 1:
            raise ValueError('Requested trial start is not unique in the bounded interval')
        chosen = matches[0]
    correlation = chosen[1].get('correlation') if chosen else None
    selected = []
    for utc, fields in controls:
        if correlation is not None and fields.get('correlation') == correlation:
            selected.append(dict(utc=utc, **{key: fields.get(key) for key in
                ('generation', 'role', 'action', 'proofMode', 'hostHeartbeatAgeMs',
                 'clientHeartbeatAgeMs', 'commandElapsedMs')}))
    last_heartbeat = {}
    for entry in selected:
        if entry['action'] == 'heartbeat':
            last_heartbeat[entry['role']] = entry['utc']
    if int(base.run(['systemctl', 'show', base.SERVICE, '-p', 'MainPID', '--value'])) != value['pid']:
        raise ValueError('Process changed during observation')
    value.update(startedRoomCount=len(starts),
                 selectedTrialStartUtc=chosen[0] if chosen else None,
                 selectedTrialCommandTimeline=selected[-300:],
                 timelineTruncated=len(selected) > 300,
                 selectedTrialLastHeartbeatUtc=last_heartbeat,
                 httpStatusCounts=[dict(path=route, status=status, count=count)
                     for (route, status), count in sorted(statuses.items())],
                 sessionAndFailedRequests=http[-300:],
                 sessionRenewalOwnerIdentified=False,
                 androidFirstCauseIdentified=False)
    return value


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--since', required=True)
    parser.add_argument('--trial-start')
    args = parser.parse_args()
    if os.geteuid() != 0 or os.environ.get('PKEXEC_UID') != '1000':
        raise SystemExit('Native Linux operator authentication required')
    value = observe(args.since, args.trial_start)
    folder = Path('/mnt/DADOS/station-r73-registry-check-20261007')
    name = folder / ('presence-' + datetime.now(timezone.utc).strftime('%H%M%S%f') + '.json')
    with name.open('x') as stream:
        os.fchmod(stream.fileno(), 0o600)
        json.dump(value, stream, indent=2)
        stream.write('\n')
    owner = pwd.getpwnam('lz-servidor')
    os.chown(name, owner.pw_uid, owner.pw_gid)
    print(json.dumps(dict(record=str(name), pid=value['pid'],
        rooms=value['roomsAfter']['recovery'],
        trialStartUtc=value['selectedTrialStartUtc'],
        lastHeartbeatUtc=value['selectedTrialLastHeartbeatUtc'],
        firstTransportEnds=value['firstTransportEnds'],
        sessionRenewalOwnerIdentified=False, androidFirstCauseIdentified=False)), flush=True)
