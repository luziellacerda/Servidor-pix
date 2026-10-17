#!/usr/bin/env python3
"""Publish a verified Station APK into private immutable storage, then activate atomically.

Never signs APKs, installs applications, changes keys, or restarts services.
Use the effective protected profile registry for --registered-profiles.
"""
import argparse
import fcntl
import grp
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import tempfile
import unicodedata
import zipfile

PACKAGE = 'org.turboramastation.frontend'
MAXIMUM = 8 * 1024**3

def load_json(value):
    def pairs(items):
        result = {}
        for key, item in items:
            if key in result:
                raise ValueError('Duplicate JSON member')
            result[key] = item
        return result
    return json.loads(value, object_pairs_hook=pairs)

def units(value):
    return len(value.encode('utf-16-le')) // 2

def regular(path):
    path = Path(path).absolute()
    for parent in (path, *path.parents):
        if parent.is_symlink():
            raise ValueError('Symbolic links are not permitted in update storage')
    return path

def command(args):
    result = subprocess.run(args, check=True, capture_output=True, text=True, timeout=600)
    return result.stdout + result.stderr

def require_updater(apk, aapt):
    components = ('StationAppUpdateActivity', 'StationAppUpdateService', 'StationAppUpdateReceiver')
    xml = command([aapt, 'dump', 'xmltree', str(apk), '--file', 'AndroidManifest.xml'])
    for name in ('android.permission.REQUEST_INSTALL_PACKAGES', 'android.permission.FOREGROUND_SERVICE',
                 'android.permission.FOREGROUND_SERVICE_DATA_SYNC', *("org.emulationstation.frontend.station." + c for c in components)):
        if '"' + name + '"' not in xml:
            raise ValueError('The final APK is missing an updater manifest declaration')
    expected = {'Lorg/emulationstation/frontend/station/' + name + ';' for name in
                (*components, 'StationAppUpdateUi', 'StationAppUpdater', 'StationAppRelease', 'StationAppUpdateFiles')}
    found = set()
    with zipfile.ZipFile(apk) as archive:
        entries = archive.namelist()
        if len(entries) != len(set(entries)):
            raise ValueError('The final APK contains duplicate ZIP entries')
        for entry in archive.infolist():
            if not re.fullmatch(r'classes(?:[1-9][0-9]*)?\.dex', entry.filename):
                continue
            if entry.file_size > 64 * 1024**2:
                raise ValueError('DEX partition is excessive')
            data = archive.read(entry)
            def u32(at):
                return struct.unpack_from('<I', data, at)[0]
            if not data.startswith(b'dex\n') or len(data) < 112:
                raise ValueError('Unsupported DEX partition')
            string_count, strings = u32(56), u32(60)
            type_count, types = u32(64), u32(68)
            count, definitions = u32(96), u32(100)
            if max(count, type_count, string_count) > 1000000 or definitions + count * 32 > len(data):
                raise ValueError('Invalid DEX tables')
            for ordinal in range(count):
                type_index = u32(definitions + ordinal * 32)
                if type_index >= type_count:
                    raise ValueError('Invalid DEX class index')
                string_index = u32(types + type_index * 4)
                if string_index >= string_count:
                    raise ValueError('Invalid DEX type index')
                position = u32(strings + string_index * 4)
                for _ in range(5):
                    value = data[position]
                    position += 1
                    if value < 128:
                        break
                end = data.find(b'\0', position, position + 512)
                if end < 0:
                    continue
                descriptor = data[position:end].decode('ascii', errors='replace')
                if descriptor in expected:
                    found.add(descriptor)
    if found != expected:
        raise ValueError('The final APK is missing compiled updater classes')

def inspect(apk, aapt, signer, require_module=False, zipalign=None):
    output = command([signer, 'verify', '--verbose', '--print-certs', str(apk)])
    if not re.search(r'Verified using v[23](?:\.\d+)? scheme.*: true', output):
        raise ValueError('APK requires a verified v2/v3 signature')
    certs = re.findall(r'^Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]{64})$', output, re.M)
    if len(certs) != 1:
        raise ValueError('Exactly one original APK signer is required')
    badging = command([aapt, 'dump', 'badging', str(apk)])
    package = re.search(r"^package: name='([^']+)' versionCode='(\d+)' versionName='([^']*)'", badging, re.M)
    sdk = re.search(r"^(?:minSdkVersion|sdkVersion):'(\d+)'", badging, re.M)
    native = re.search(r'^native-code: (.*)$', badging, re.M)
    if not package or package[1] != PACKAGE or not sdk or not native or "'arm64-v8a'" not in native[1]:
        raise ValueError('Not a complete ARM64 Station APK')
    if require_module:
        command([zipalign, '-c', '-P', '16', '4', str(apk)])
        require_updater(apk, aapt)
    return dict(versionCode=int(package[2]), versionName=package[3], minimumSdk=int(sdk[1]), certificateSha256=certs[0].lower())

def registered(apk, registry):
    registry = regular(registry)
    if not registry.is_file() or registry.stat().st_size > 17 * 1024**2:
        raise ValueError('Effective profile registry is missing or excessive')
    profiles = load_json(registry.read_text())
    if not isinstance(profiles, list):
        raise ValueError('Expected the effective Station multiplayer profile array')
    identities = {(p.get('engineId'), p.get('coreSha256'), p.get('runtimeSha256')) for p in profiles}
    with zipfile.ZipFile(apk) as archive:
        asset = archive.getinfo('assets/station-online/engines.json')
        if asset.file_size > 1024 * 1024:
            raise ValueError('Engine manifest is excessive')
        engines = load_json(archive.read(asset))['engines']
        selected = [e for e in engines if e.get('launchReady') and e.get('recoveryProtocol') == 'station-stream.v3']
        if any((e.get('engineId'), e.get('coreSha256'), e.get('runtimeSha256')) not in identities for e in selected):
            raise ValueError('Register the exact final APK engines before publishing this update')
        hashes = {}
        def library(name, expected):
            if not isinstance(name, str) or not re.fullmatch(r'lib[A-Za-z0-9_.+\-]+\.so', name) or not isinstance(expected, str) or not re.fullmatch(r'[0-9a-f]{64}', expected):
                raise ValueError('Invalid native identity in the APK manifest')
            if name not in hashes:
                try:
                    entry = archive.getinfo('lib/arm64-v8a/' + name)
                except KeyError:
                    raise ValueError('A declared engine library is absent from the final APK') from None
                if not 64 <= entry.file_size <= 256 * 1024**2:
                    raise ValueError('Declared native library size is invalid')
                with archive.open(entry) as binary:
                    hashes[name] = hashlib.file_digest(binary, 'sha256').hexdigest()
            if hashes[name] != expected:
                raise ValueError('Actual APK engine/dependency bytes differ from their declared identity')
        for engine in selected:
            library(engine.get('library'), engine.get('coreSha256'))
            # Verified default in StationRetroActivity: System.loadLibrary("station_retroarch").
            library(engine.get('runtimeLibrary', 'libstation_retroarch.so'), engine.get('runtimeSha256'))
            for name, expected in engine.get('dependencySha256', {}).items():
                library(name, expected)
    return len(selected)

def publish(args):
    os.umask(0o077)
    root = regular(args.directory)
    root.mkdir(mode=0o750, parents=True, exist_ok=True)
    regular(root / 'apks').mkdir(mode=0o750, exist_ok=True)
    root.chmod(0o750)
    (root / 'apks').chmod(0o750)
    read_group = grp.getgrnam(args.read_group).gr_gid if args.read_group else None
    publisher_owner = 0 if os.geteuid() == 0 else -1
    if os.geteuid() == 0 and read_group is None:
        raise ValueError('Root publication requires --read-group for the Station service')
    if read_group is not None:
        for folder in (root, root / 'apks'):
            os.chown(folder, publisher_owner, read_group)
            folder.chmod(0o750)
    lock_path = regular(root / '.publish.lock')
    with lock_path.open('a+') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        source, trusted = regular(args.apk), regular(args.trusted_apk)
        before = source.stat()
        if not source.is_file() or not 1024 <= before.st_size <= MAXIMUM:
            raise ValueError('APK size is invalid')
        base = inspect(trusted, args.aapt2, args.apksigner)
        release = inspect(source, args.aapt2, args.apksigner, require_module=True, zipalign=args.zipalign)
        if release['certificateSha256'] != base['certificateSha256'] or release['versionCode'] <= base['versionCode']:
            raise ValueError('The update must retain the original signer and increase versionCode')
        current_path = regular(root / 'current.json')
        if current_path.exists():
            current = load_json(current_path.read_text())
            if release['certificateSha256'] != current['certificateSha256'] or release['versionCode'] <= current['versionCode']:
                raise ValueError('Update would change signer or roll back the published versionCode')
        engine_count = registered(source, args.registered_profiles)
        notes = args.notes_file.read_text() if args.notes_file else ''
        if units(notes) > 2000 or any(unicodedata.category(c) == 'Cc' and c not in '\r\n\t' for c in notes):
            raise ValueError('Release notes are invalid or too long')
        descriptor = dict(schemaVersion=1, packageName=PACKAGE, **release,
                          abi='arm64-v8a', notes=notes, sizeBytes=before.st_size)
        if not 1 <= descriptor['versionCode'] <= 2100000000 or not 26 <= descriptor['minimumSdk'] <= 1000 or not 1 <= units(descriptor['versionName']) <= 100 or any(unicodedata.category(c)=='Cc' for c in descriptor['versionName']):
            raise ValueError('APK version metadata is invalid')
        fd, temporary_name = tempfile.mkstemp(prefix='.incoming-', dir=root / 'apks')
        temporary = Path(temporary_name)
        try:
            digest = hashlib.sha256()
            with source.open('rb') as incoming, os.fdopen(fd, 'wb') as outgoing:
                while data := incoming.read(1024 * 1024):
                    digest.update(data)
                    outgoing.write(data)
                outgoing.flush()
                os.fsync(outgoing.fileno())
            after = source.stat()
            if (before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns):
                raise ValueError('APK changed during verification/copy')
            descriptor['sha256'] = descriptor['releaseId'] = digest.hexdigest()
            destination = regular(root / 'apks' / (digest.hexdigest() + '.apk'))
            if destination.exists():
                with destination.open('rb') as existing:
                    matches = hashlib.file_digest(existing, 'sha256').hexdigest() == digest.hexdigest()
                if destination.stat().st_size != before.st_size or not matches:
                    raise ValueError('An immutable APK destination has different bytes')
                destination.chmod(0o440)
                if read_group is not None:
                    os.chown(destination, publisher_owner, read_group)
            else:
                temporary.chmod(0o440)
                if read_group is not None:
                    os.chown(temporary, publisher_owner, read_group)
                os.link(temporary, destination)
            # The public API reads only this atomic pointer; old APKs/metadata
            # remain private for rollback investigation, never auto selected.
            fd, name = tempfile.mkstemp(prefix='.release-', dir=root)
            with os.fdopen(fd, 'w') as output:
                json.dump(descriptor, output, ensure_ascii=False, indent=2)
                output.write('\n')
                output.flush()
                os.fsync(output.fileno())
            os.chmod(name, 0o640)
            if read_group is not None:
                os.chown(name, publisher_owner, read_group)
            os.replace(name, current_path)
            directory_fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY)
            try:
                os.fsync(directory_fd)
            finally:
                os.close(directory_fd)
            return dict(published=True, releaseId=descriptor['releaseId'], versionCode=release['versionCode'], registeredV3Engines=engine_count,
                        signatureVerified=True, originalSignerPreserved=True, privateStorage=True)
        finally:
            temporary.unlink(missing_ok=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apk', required=True, type=Path)
    parser.add_argument('--trusted-apk', required=True, type=Path)
    parser.add_argument('--registered-profiles', required=True, type=Path)
    parser.add_argument('--directory', required=True, type=Path)
    parser.add_argument('--apksigner', required=True)
    parser.add_argument('--aapt2', required=True)
    parser.add_argument('--zipalign', required=True)
    parser.add_argument('--notes-file', type=Path)
    parser.add_argument('--read-group', help='Group of the effective Station service; mandatory when publishing as root')
    args = parser.parse_args()
    print(json.dumps(publish(args), ensure_ascii=False))

if __name__ == '__main__':
    main()
