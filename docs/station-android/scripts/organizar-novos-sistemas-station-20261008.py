#!/usr/bin/env python3
"""Move only the five requested system directories; export private local inputs."""
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time

VOLUME = Path('/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc')
NAMES = ('gamecube', 'wiiu', 'psx', 'wii', 'switch')
PRIVATE = Path('/mnt/DADOS/station-new-systems-private-20261008')
CONFIG = Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
HOME = Path('/mnt/DADOS/turbostation-library-auto-20261004')
TIMER = 'turborama-station-library-scan.timer'
SCAN = 'turborama-station-library-scan.service'


def run(*args):
    return subprocess.check_output(args, text=True).strip()


def write_private(name, value):
    path = PRIVATE / name
    with path.open('x', encoding='utf-8') as out:
        json.dump(value, out, ensure_ascii=False, indent=2)
        out.write('\n')
    os.chown(path, 1000, 1000)
    path.chmod(0o600)


def listing(folder):
    files = {}
    for p in folder.rglob('*'):
        if p.is_symlink():
            raise ValueError('System directory contains a symlink')
        if p.is_file():
            s = p.stat()
            files[p.relative_to(folder).as_posix()] = [s.st_dev, s.st_ino, s.st_size, s.st_mtime_ns]
    return files


def main():
    if os.geteuid() != 0:
        raise SystemExit('Linux administrative authentication is required')
    os.umask(0o077)
    PRIVATE.mkdir(mode=0o700)
    os.chown(PRIVATE, 1000, 1000)
    config = json.loads(CONFIG.read_bytes())
    if Path(config['volumeRoot']).resolve() != VOLUME or VOLUME.stat().st_dev != config['volumeDevice']:
        raise ValueError('Configured game volume changed')
    before = {name: listing(VOLUME / 'snes' / name) for name in NAMES}
    if any((VOLUME / name).exists() for name in NAMES):
        raise ValueError('A destination system directory already exists')
    index = (HOME / 'index.json').read_bytes()
    pid = run('systemctl', 'show', 'turborama-station-api.service', '-p', 'MainPID', '--value')
    was_active = run('systemctl', 'is-active', TIMER) == 'active'
    moved = []
    write_private('move-before.json', before)
    subprocess.run(['systemctl', 'stop', TIMER], check=True)
    try:
        # Let an already running import finish; never kill an in-progress publication.
        deadline = time.monotonic() + 120
        while run('systemctl', 'show', SCAN, '-p', 'ActiveState', '--value') == 'activating':
            if time.monotonic() > deadline:
                raise TimeoutError('Existing library scan has not finished')
            time.sleep(1)
        with (HOME / 'scan.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            if (HOME / 'index.json').read_bytes() != index:
                raise ValueError('Catalog changed before folder transaction')
            for name in NAMES:
                source = VOLUME / 'snes' / name
                if listing(source) != before[name]:
                    raise ValueError('Game copy is still changing')
            for name in NAMES:
                (VOLUME / 'snes' / name).rename(VOLUME / name)
                moved.append(name)
                if listing(VOLUME / name) != before[name]:
                    raise ValueError('Moved file identity changed')
            # Scanner does not yet have these platforms configured. Existing source
            # entries must not be silently changed or orphaned by the move.
            state = json.loads((HOME / 'state.json').read_bytes())
            if any(any(key.startswith(str(VOLUME / 'snes' / name) + '/') for name in NAMES)
                   for key in state['sources']):
                raise ValueError('An existing catalog source needs explicit reconciliation')
            write_private('library-config.json', config)
            write_private('index-before.json', json.loads(index))
            write_private('state-before.json', state)
            for name in ('metadataOverrides', 'metadataSeed', 'contentIdentityRegistry'):
                if config.get(name):
                    write_private(name + '.json', json.loads(Path(config[name]).read_bytes()))
            if (HOME / 'index.json').read_bytes() != index:
                raise ValueError('Catalog changed during folder transaction')
            report = {'moved': [{ 'system': name, 'files': len(before[name]),
                        'bytes': sum(row[2] for row in before[name].values()),
                        'fileIdentityPreserved': True} for name in NAMES],
                      'catalogRevisionPreserved': json.loads(index)['revision'],
                      'indexSha256': hashlib.sha256(index).hexdigest(), 'apiPid': int(pid),
                      'apiRestarted': False}
            write_private('organization-receipt.json', report)
    except BaseException:
        for name in reversed(moved):
            (VOLUME / name).rename(VOLUME / 'snes' / name)
        raise
    finally:
        if was_active:
            subprocess.run(['systemctl', 'start', TIMER], check=True)
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
