"""Prepare supported raw cartridges from legacy copier containers, once at publication.

Original archives/files and previous immutable artifacts are retained. Downloads read
the prepared body directly; this module never runs in the HTTP download handler.
"""
import hashlib
import os
from pathlib import Path, PurePosixPath
import tempfile
import zipfile

MAX_BYTES = 16 * 1024 * 1024

def normalize(data, platform, extension):
    if not data or len(data) > MAX_BYTES:
        raise ValueError('cartridge input exceeds supported size')
    if platform == 'megadrive' and extension == '.smd':
        if len(data) >= 0x104 and data[0x100:0x104] == b'SEGA':
            return data, '.bin', 'raw-md-container'
        if len(data) % 16384 != 512 or data[8:10] != b'\xaa\xbb':
            raise ValueError('unrecognized SMD copier container')
        result = bytearray(len(data) - 512)
        for offset in range(0, len(result), 16384):
            block = data[512 + offset:512 + offset + 16384]
            result[offset:offset + 16384:2] = block[8192:]
            result[offset + 1:offset + 16384:2] = block[:8192]
        if result[0x100:0x104] != b'SEGA':
            raise ValueError('converted SMD has no Genesis header')
        return bytes(result), '.bin', 'smd-copier-deinterleave-v1'
    if platform == 'snes' and extension in {'.fig', '.swc'}:
        if len(data) % 1024 not in {0, 512}:
            raise ValueError('unsupported SNES copier size')
        stripped = len(data) % 1024 == 512
        result = data[512:] if stripped else data
        headers = []
        for offset in (0x7fc0, 0xffc0, 0x40ffc0):
            header = result[offset:offset + 32]
            if len(header) != 32:
                continue
            checksum = int.from_bytes(header[30:32], 'little')
            complement = int.from_bytes(header[28:30], 'little')
            if checksum ^ complement == 65535 and header[21] in {0x20, 0x21, 0x22, 0x23, 0x25, 0x30, 0x31, 0x32, 0x35}:
                headers.append(header)
        if not headers:
            raise ValueError('SNES copier input has no supported raw header')
        return result, '.sfc', 'snes-copier-header-v1' if stripped else 'raw-snes-container'
    raise ValueError('unsupported cartridge transformation')

def prepare(rows, directory, gid):
    directory = Path(directory)
    if not directory.is_absolute() or directory.resolve() != directory or directory.is_symlink():
        raise ValueError('unsafe normalized cartridge directory')
    directory.mkdir(mode=0o750, parents=True, exist_ok=True)
    directory.chmod(0o750)
    if gid is not None: os.chown(directory, -1, gid)
    receipts, checked, pending = [], {}, []
    for row in rows:
        platform = {'snesbr': 'snes', 'megadrivebr': 'megadrive'}.get(row['platform'], row['platform'])
        artifact = row['artifact']
        extension = PurePosixPath(artifact['launchPath']).suffix.lower()
        if not ((platform == 'megadrive' and extension == '.smd') or (platform == 'snes' and extension in {'.fig', '.swc'})):
            continue
        if artifact['fileCount'] != 1 or artifact['format'] not in {'raw', 'zip'}:
            raise ValueError('cartridge conversion requires a single launch payload')
        source = Path(row['filePath'])
        if artifact['format'] == 'zip':
            with zipfile.ZipFile(source) as archive:
                info = archive.getinfo(artifact['launchPath'])
                if info.file_size > MAX_BYTES or info.flag_bits & 1:
                    raise ValueError('unsupported cartridge archive payload')
                with archive.open(info) as stream:
                    data = stream.read(MAX_BYTES + 1)
        else:
            with source.open('rb') as stream:
                data = stream.read(MAX_BYTES + 1)
        before = hashlib.sha256(data).hexdigest()
        if row.get('contentSha256') is not None and before != row['contentSha256']:
            raise ValueError('legacy cartridge differs from its approved content identity')
        if row.get('contentSha256') is None:
            with source.open('rb') as stream:
                source_hash = hashlib.file_digest(stream, 'sha256').hexdigest()
            if source_hash != artifact['sha256']:
                raise ValueError('new cartridge artifact differs from the prepared descriptor')
        try:
            normalized, suffix, algorithm = normalize(data, platform, extension)
        except ValueError as error:
            pending.append(dict(itemId=row['itemId'], reason=str(error)))
            continue
        after = hashlib.sha256(normalized).hexdigest()
        name = PurePosixPath(artifact['launchPath']).stem + suffix
        if any(c in name for c in '/\\\x00') or len(name.encode('utf-16-le')) // 2 > 200:
            raise ValueError('unsafe normalized cartridge filename')
        game_dir = directory / after
        game_dir.mkdir(mode=0o750, parents=True, exist_ok=True)
        if game_dir.is_symlink():
            raise ValueError('unsafe normalized cartridge output')
        game_dir.chmod(0o750)
        target = game_dir / name
        if target.is_symlink():
            raise ValueError('unsafe normalized cartridge target')
        if target.exists():
            if target.read_bytes() != normalized:
                raise ValueError('immutable normalized cartridge differs')
        else:
            fd, temporary = tempfile.mkstemp(prefix='.cartridge-', dir=game_dir)
            try:
                with os.fdopen(fd, 'wb') as out:
                    out.write(normalized); out.flush(); os.fsync(out.fileno())
                    os.fchmod(out.fileno(), 0o444)
                    if gid is not None: os.fchown(out.fileno(), -1, gid)
                os.replace(temporary, target)
            finally:
                if os.path.exists(temporary): os.unlink(temporary)
        if gid is not None:
            os.chown(game_dir, -1, gid); os.chown(directory, -1, gid)
        descriptor = dict(fileName=name, sizeBytes=len(normalized), sha256=after, format='raw',
                          expandedSizeBytes=len(normalized), fileCount=1, launchPath=name)
        row.update(filePath=str(target), artifact=descriptor)
        row['revision'] += 1
        checked[row['itemId']] = dict(itemId=row['itemId'], platform=row['platform'], artifactSha256=after,
                launchPath=name, expandedSizeBytes=len(normalized), fileCount=1, contentSha256=after)
        receipts.append(dict(itemId=row['itemId'], name=row['name'], algorithm=algorithm,
                originalContentSha256=before, normalizedContentSha256=after,
                originalLaunchPath=artifact['launchPath'], launchPath=name,
                originalBytes=len(data), bytes=len(normalized)))
    return receipts, checked, pending
