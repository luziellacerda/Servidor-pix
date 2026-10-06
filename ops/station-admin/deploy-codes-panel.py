#!/usr/bin/env python3
"""Publish only the Station code UI and exact navigation changes; no service or DB writes."""
import argparse
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / 'ops/station-admin/codes-panel-20261006.json'
SITE = Path('/home/lz-servidor/releases/turbobox/coupons-v1-20260902')
NAMES = {'station-admin.php', 'station-admin-policy.php', 'station-admin.js',
         'station-admin.css', 'station-access-link.css', 'panel.php', 'admin-product-edit.php',
         'admin-customer.php', 'admin-payments.php'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def current_sha(path):
    try:
        return sha(path.read_bytes())
    except FileNotFoundError:
        return None


def render_navigation(name, data, manifest):
    entry = manifest['navigation'][name]
    if sha(data) == entry['afterSha256']:
        return data
    if sha(data) != entry['beforeSha256']:
        raise RuntimeError('Navigation changed since inspection: ' + name)
    text = data.decode('utf-8')
    for patch in entry['replacements']:
        if text.count(patch['before']) != 1:
            raise RuntimeError('Ambiguous navigation patch: ' + name)
        text = text.replace(patch['before'], patch['after'], 1)
    result = text.encode('utf-8')
    if sha(result) != entry['afterSha256']:
        raise RuntimeError('Navigation output mismatch: ' + name)
    return result


def atomic(path, data, attributes):
    fd, temporary = tempfile.mkstemp(prefix='.station-codes-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as stream:
            os.fchown(stream.fileno(), attributes['uid'], attributes['gid'])
            os.fchmod(stream.fileno(), attributes['mode'])
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def save_state(folder, state):
    attributes = dict(uid=os.getuid(), gid=os.getgid(), mode=0o600)
    atomic(folder / 'state.json', (json.dumps(state, indent=2) + '\n').encode(), attributes)


def syntax(files):
    with tempfile.TemporaryDirectory(prefix='station-codes-syntax-') as temporary:
        for name, data in files.items():
            if not name.endswith(('.php', '.js')):
                continue
            candidate = Path(temporary) / name
            candidate.write_bytes(data)
            cmd = ['php', '-l', str(candidate)] if name.endswith('.php') else ['node', '--check', str(candidate)]
            result = subprocess.run(cmd, capture_output=True)
            if result.returncode:
                raise RuntimeError('Syntax check failed: ' + name)


def restore(folder, state):
    originals = {}
    for name, entry in state['files'].items():
        if name not in NAMES:
            raise RuntimeError('Unexpected backup target')
        original = (folder / name).read_bytes() if entry['beforeSha256'] is not None else None
        if (sha(original) if original is not None else None) != entry['beforeSha256']:
            raise RuntimeError('Backup integrity mismatch: ' + name)
        current = current_sha(SITE / name)
        if current not in (entry['beforeSha256'], entry['afterSha256']):
            raise RuntimeError('A later edit prevents rollback: ' + name)
        originals[name] = original
    for name, data in originals.items():
        entry = state['files'][name]
        if current_sha(SITE / name) == entry['afterSha256']:
            if data is None:
                (SITE / name).unlink()
            else:
                atomic(SITE / name, data, entry)
    if not all(current_sha(SITE / n) == e['beforeSha256'] for n, e in state['files'].items()):
        raise RuntimeError('Rollback verification failed')
    state['status'] = 'rolled-back'
    state['rolledBackAt'] = datetime.now(timezone.utc).isoformat()
    save_state(folder, state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--apply', action='store_true')
    modes.add_argument('--rollback', type=Path)
    args = parser.parse_args()
    if args.rollback:
        folder = args.rollback.resolve()
        if folder.parent != Path('/home/lz-servidor') or not folder.name.startswith('station-codes-panel-backup-20261006-'):
            raise RuntimeError('Unexpected backup directory')
        state = json.loads((folder / 'state.json').read_text())
        if state['site'] != str(SITE) or set(state['files']) != NAMES:
            raise RuntimeError('Unexpected rollback scope')
        restore(folder, state)
        print(json.dumps(dict(status='rolled-back', backup=str(folder))))
        return
    manifest = json.loads(MANIFEST.read_text())
    if manifest['version'] != 1 or manifest['targetSite'] != str(SITE):
        raise RuntimeError('Unexpected target site')
    entries = manifest['ui'] | manifest['navigation']
    if set(entries) != NAMES:
        raise RuntimeError('Unexpected file scope')
    files, originals, attributes = {}, {}, {}
    installed = all(current_sha(SITE / n) == e['afterSha256'] for n, e in entries.items())
    if installed:
        if args.apply:
            raise RuntimeError('This UI is already installed')
        print(json.dumps(dict(status='installed', files=len(entries))))
        return
    for name, entry in entries.items():
        target = SITE / name
        if entry['beforeSha256'] is None:
            if target.exists() or target.is_symlink():
                raise RuntimeError('New asset already exists: ' + name)
            data = None
            attributes[name] = dict(uid=os.getuid(), gid=os.getgid(), mode=0o664)
        else:
            s = target.lstat()
            if not stat.S_ISREG(s.st_mode):
                raise RuntimeError('Target must be a regular file: ' + name)
            data = target.read_bytes()
            attributes[name] = dict(uid=s.st_uid, gid=s.st_gid, mode=stat.S_IMODE(s.st_mode))
        if (sha(data) if data is not None else None) != entry['beforeSha256']:
            raise RuntimeError('Production file changed since inspection: ' + name)
        originals[name] = data
        files[name] = (ROOT / 'ops/station-admin/site' / name).read_bytes() if name in manifest['ui'] else render_navigation(name, data, manifest)
        if sha(files[name]) != entry['afterSha256']:
            raise RuntimeError('Source output mismatch: ' + name)
    syntax(files)
    if not args.apply:
        print(json.dumps(dict(status='ready', files=len(files), databaseChanges=False, serviceRestarts=False)))
        return
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT).strip():
        raise RuntimeError('Commit the reviewed source before publishing')
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    folder = Path(tempfile.mkdtemp(prefix='station-codes-panel-backup-20261006-', dir='/home/lz-servidor'))
    state = dict(site=str(SITE), sourceCommit=commit, status='prepared',
                 createdAt=datetime.now(timezone.utc).isoformat(),
                 files={n: entries[n] | attributes[n] for n in files})
    for name, data in originals.items():
        if data is not None:
            atomic(folder / name, data, dict(uid=os.getuid(), gid=os.getgid(), mode=0o600))
    save_state(folder, state)
    try:
        for name, data in files.items():
            if current_sha(SITE / name) != entries[name]['beforeSha256']:
                raise RuntimeError('Production changed during publication: ' + name)
            atomic(SITE / name, data, attributes[name])
        if not all(sha((SITE / n).read_bytes()) == e['afterSha256'] for n, e in entries.items()):
            raise RuntimeError('Publication verification failed')
        state['status'] = 'applied'
        state['appliedAt'] = datetime.now(timezone.utc).isoformat()
        save_state(folder, state)
    except Exception:
        restore(folder, state)
        raise
    print(json.dumps(dict(status='applied', sourceCommit=commit, backup=str(folder), files=len(files),
                         databaseChanges=False, serviceRestarts=False)))


if __name__ == '__main__':
    with open('/home/lz-servidor/.station-codes-panel-deploy.lock', 'a') as lock:
        os.fchmod(lock.fileno(), 0o600)
        fcntl.flock(lock, fcntl.LOCK_EX)
        main()
