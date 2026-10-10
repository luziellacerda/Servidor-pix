"""Bundle local CUE discs and extracted Wii U games without dropping dependencies."""
import hashlib
import os
from pathlib import Path, PureWindowsPath
import re
import shutil
import stat
import zipfile


class PackagePending(ValueError):
    pass


def safe_name(name):
    if not name or len(name) > 512 or '\\' in name or ':' in name or name.startswith('/'):
        raise PackagePending('unsafe_package_member')
    if any(p in {'', '.', '..'} for p in name.split('/')) or any(ord(c) < 32 for c in name):
        raise PackagePending('unsafe_package_member')
    return name


def local_cue_reference(source, name, single_file):
    name = PureWindowsPath(name).name if re.match(r'^[A-Za-z]:[\\/]', name) else name.replace('\\', '/')
    safe_name(name)
    path = source.parent / name
    if not path.is_file() and single_file:
        # A common local rename leaves the old FILE token inside a one-file
        # CUE. Accept only the unique exact CUE basename, never a nearby title.
        same_stem = [p for p in source.parent.iterdir() if p.is_file() and not p.is_symlink() and
                     p.stem == source.stem and p.suffix.lower() in {'.bin', '.img', '.iso'}]
        if len(same_stem) == 1:
            name = same_stem[0].name
            path = same_stem[0]
    return name, path


def cue_reference_paths(source):
    """Recognize all safely named track files even while a CUE copy is incomplete."""
    raw = source.read_bytes()
    if len(raw) > 1024 * 1024:
        return []
    text = raw.decode('utf-8-sig', errors='replace')
    result = []
    references = re.findall(r'(?mi)\bFILE\s+"([^"]+)"', text)
    for name in references:
        try:
            name, path = local_cue_reference(source, name, len(set(references)) == 1)
        except PackagePending:
            continue
        if path.resolve().is_relative_to(source.parent.resolve()):
            result.append(path)
    return result


def cue_layout(source):
    raw = source.read_bytes()
    if len(raw) > 1024 * 1024:
        raise PackagePending('cue_too_large')
    try:
        text = raw.decode('utf-8-sig')
    except UnicodeDecodeError:
        text = raw.decode('cp1252')
    references = []
    original_names = re.findall(r'(?mi)\bFILE\s+"([^"]+)"', text)

    def local_file(match):
        name, p = local_cue_reference(source, match.group(1), len(set(original_names)) == 1)
        if p.is_symlink() or not p.resolve().is_relative_to(source.parent.resolve()) or not p.is_file():
            raise PackagePending('cue_dependency_missing_or_unsafe')
        references.append((name, p, None))
        return 'FILE "' + name + '"'

    text = re.sub(r'(?mi)\bFILE\s+"([^"]+)"', local_file, text)
    if not references:
        raise PackagePending('cue_has_no_files')
    unique = {name: (name, p, data) for name, p, data in references}
    members = [(source.name, source, text.encode('utf-8')), *unique.values()]
    if len({n.casefold() for n, _, _ in members}) != len(members):
        raise PackagePending('duplicate_package_member')
    return members, source.name


def wiiu_layout(source):
    if source.parent.name.casefold() != 'code':
        raise PackagePending('wiiu_rpx_requires_code_directory')
    root = source.parent.parent
    children = {p.name.casefold(): p for p in root.iterdir() if p.is_dir()}
    if not all(name in children for name in ('code', 'content', 'meta')):
        raise PackagePending('wiiu_requires_code_content_meta')
    if not (children['meta'] / 'meta.xml').is_file():
        raise PackagePending('wiiu_meta_xml_missing')
    members = []
    for p in sorted(root.rglob('*')):
        if p.is_symlink():
            raise PackagePending('package_contains_symlink')
        if p.is_file():
            members.append((safe_name(p.relative_to(root).as_posix()), p, None))
    if not 1 <= len(members) <= 100_000:
        raise PackagePending('package_file_count_out_of_range')
    if len({n.casefold() for n, _, _ in members}) != len(members):
        raise PackagePending('duplicate_package_member')
    return members, source.relative_to(root).as_posix()


def package_layout(source, mode):
    if mode == 'cue-disc' and source.suffix.lower() == '.cue':
        return cue_layout(source)
    if mode == 'wiiu-folder' and source.suffix.lower() == '.rpx':
        return wiiu_layout(source)
    return [(source.name, source, None)], source.name


def prepare_package(source, temporary, mode, describe, compression=zipfile.ZIP_STORED):
    members, launch = package_layout(source, mode)
    if len(members) == 1 and members[0][2] is None:
        return prepare_raw(source, temporary)
    with zipfile.ZipFile(temporary, 'w', compression, allowZip64=True) as archive:
        for name, path, content in members:
            entry = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            entry.compress_type = compression
            entry.external_attr = 0o100644 << 16
            with archive.open(entry, 'w', force_zip64=True) as dest:
                if content is None:
                    with path.open('rb') as src:
                        shutil.copyfileobj(src, dest, 1024 * 1024)
                else:
                    dest.write(content)
    return temporary, describe({'filePath': str(temporary)}, launch)


def writable_handles(device, inode):
    """Reject writers opened before the source was made read only."""
    for process in Path('/proc').iterdir():
        if not process.name.isdecimal():
            continue
        try:
            handles = list((process / 'fdinfo').iterdir())
        except (FileNotFoundError, ProcessLookupError):
            continue
        for handle in handles:
            try:
                fields = dict(line.split(':', 1) for line in handle.read_text().splitlines() if ':' in line)
                if int(fields['flags'].strip(), 8) & os.O_ACCMODE not in (os.O_WRONLY, os.O_RDWR):
                    continue
                opened = (process / 'fd' / handle.name).stat()
                if (opened.st_dev, opened.st_ino) == (device, inode):
                    return True
            except (FileNotFoundError, ProcessLookupError):
                continue
    return False


def prepare_readonly_raw(source, temporary):
    """Publish one inode on the same volume, protected from in-place user edits.

    The root importer freezes a finished source, rejects open writers and hashes
    once. Users can replace the source pathname in its writable directory; an
    existing grant keeps the old read-only inode in the private artifact store.
    No writable aliases, symbolic links, cross-volume links or non-root callers.
    """
    if os.geteuid() != 0:
        raise PackagePending('readonly_raw_requires_root_importer')
    if source.is_symlink() or temporary.exists() or temporary.is_symlink():
        raise PackagePending('readonly_raw_unsafe_path')
    handle = os.open(source, os.O_RDONLY | os.O_NOFOLLOW)
    before = os.fstat(handle)
    linked = False
    frozen = False
    try:
        if not stat.S_ISREG(before.st_mode) or not 0 < before.st_size <= 1 << 40:
            raise PackagePending('raw_artifact_size_out_of_range')
        if before.st_dev != temporary.parent.stat().st_dev:
            raise PackagePending('readonly_raw_requires_same_volume')
        if before.st_nlink != 1 and (before.st_uid != 0 or before.st_mode & 0o222):
            raise PackagePending('readonly_raw_writable_alias')
        os.fchown(handle, 0, before.st_gid)
        os.fchmod(handle, 0o444)
        frozen = True
        if writable_handles(before.st_dev, before.st_ino):
            raise PackagePending('readonly_raw_source_writer_active')
        os.link(source, temporary, follow_symlinks=False)
        linked = True
        expected = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
        def unchanged(path):
            current = path.stat(follow_symlinks=False)
            return stat.S_ISREG(current.st_mode) and (current.st_dev, current.st_ino, current.st_size, current.st_mtime_ns) == expected
        if not unchanged(source) or not unchanged(temporary):
            raise PackagePending('readonly_raw_source_changed')
        digest = hashlib.sha256()
        total = 0
        with os.fdopen(os.dup(handle), 'rb') as inp:
            for block in iter(lambda: inp.read(1024 * 1024), b''):
                digest.update(block)
                total += len(block)
        if total != before.st_size or not unchanged(source) or not unchanged(temporary):
            raise PackagePending('readonly_raw_source_changed')
        os.fsync(handle)
        return temporary, dict(fileName=source.name, sizeBytes=total, sha256=digest.hexdigest(),
                               format='raw', launchPath=source.name, expandedSizeBytes=total, fileCount=1)
    except BaseException:
        if linked:
            temporary.unlink(missing_ok=True)
        if frozen:
            os.fchown(handle, before.st_uid, before.st_gid)
            os.fchmod(handle, stat.S_IMODE(before.st_mode))
        raise
    finally:
        os.close(handle)


def prepare_raw(source, temporary, readonly_hardlink=False):
    """One copy/hash pass in the immutable store, independent of download requests."""
    if readonly_hardlink:
        return prepare_readonly_raw(source, temporary)
    digest = hashlib.sha256()
    size = 0
    with source.open('rb') as inp, temporary.open('xb') as out:
        for block in iter(lambda: inp.read(1024 * 1024), b''):
            digest.update(block)
            size += len(block)
            out.write(block)
        out.flush()
    if not 0 < size <= 1 << 40:
        raise PackagePending('raw_artifact_size_out_of_range')
    return temporary, dict(fileName=source.name, sizeBytes=size, sha256=digest.hexdigest(),
                          format='raw', launchPath=source.name, expandedSizeBytes=size, fileCount=1)
