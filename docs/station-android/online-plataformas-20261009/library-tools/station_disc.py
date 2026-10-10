"""Validate CD CHDs and assemble deterministic game/BIOS packages without changing sources."""
import hashlib
import os
from pathlib import Path
import shutil
import struct
import subprocess
import zipfile

# MAME's neocdz driver. These identities select supplied firmware, not downloaded ROMs.
FIRMWARE = (
    ('neocd.bin', 524288, 0xdf9de490, '7bb26d1e5d1e930515219cb18bcde5b7b23e2eda'),
    ('uni-bioscd33.rom', 524288, 0xff3abc59, '5142f205912869b673a71480c5828b1eaed782a8'),
    ('uni-bioscd32.rom', 524288, 0x0ffb3127, '5158b728e62b391fb69493743dcf7abbc62abc82'),
)
ZOOM = ('000-lo.lo', 131072, 0x5a86cff2, '5992277debadeb64d1c1c64b0a92d9293eaf7e4a')


class DiscPending(ValueError):
    pass


def support_sources(folder, volume, spec):
    folder, volume = folder.resolve(), volume.resolve()
    names = ['neocdz.zip', 'neocd.zip', 'neocd.bin', 'uni-bioscd33.rom', 'uni-bioscd32.rom', '000-lo.lo']
    candidates = [folder / name for name in names]
    bios = folder / 'bios'
    if bios.is_symlink():
        raise DiscPending('unsafe_bios_directory')
    if bios.is_dir():
        candidates.extend(p for p in bios.rglob('*') if p.is_file() and p.suffix.lower() in {'.zip', '.bin', '.rom', '.lo'})
    for relative in spec.get('biosDonors', []):
        path = volume / relative
        if path.is_symlink() or not path.resolve().is_relative_to(volume):
            raise DiscPending('unsafe_bios_donor')
        candidates.append(path)
    result = []
    for path in candidates:
        if path.is_file():
            if path.is_symlink() or not path.resolve().is_relative_to(volume):
                raise DiscPending('unsafe_bios_source')
            result.append(path.resolve())
    return sorted(set(result))


def bios_members(sources):
    """Accept only exact known firmware/zoom identities, even when a source is mislabeled."""
    wanted = {sha: (name, size, crc) for name, size, crc, sha in (*FIRMWARE, ZOOM)}
    found = {}
    for path in sources:
        if path.suffix.lower() == '.zip':
            try:
                with zipfile.ZipFile(path) as archive:
                    for member in archive.infolist():
                        if member.is_dir() or member.file_size not in {131072, 524288}:
                            continue
                        with archive.open(member) as stream:
                            data = stream.read(524289)
                        identity = hashlib.sha1(data).hexdigest()
                        if identity in wanted:
                            name, size, crc = wanted[identity]
                            if len(data) == size and zipfile.crc32(data) == crc:
                                found[name] = data
            except (OSError, zipfile.BadZipFile, RuntimeError):
                continue
        elif path.stat().st_size in {131072, 524288}:
            data = path.read_bytes()
            identity = hashlib.sha1(data).hexdigest()
            if identity in wanted:
                name, size, crc = wanted[identity]
                if len(data) == size and zipfile.crc32(data) == crc:
                    found[name] = data
    selected = next((name for name, _, _, _ in FIRMWARE if name in found), None)
    if selected is None:
        raise DiscPending('neocdz_firmware_missing_or_invalid')
    if ZOOM[0] not in found:
        raise DiscPending('neocdz_zoom_rom_missing_or_invalid')
    return [(selected, found[selected]), (ZOOM[0], found[ZOOM[0]])]


def verifier_environment(executable):
    env = dict(os.environ)
    # A sealed private tool may bring its own libraries; no system package is installed.
    private_lib = executable.parent.parent / 'lib'
    if private_lib.is_dir():
        env['LD_LIBRARY_PATH'] = str(private_lib)
    return env


def bios_status(sources):
    try:
        members = bios_members(sources)
        return dict(biosAvailable=True, firmware=members[0][0], driver='neocdz')
    except DiscPending as error:
        return dict(biosAvailable=False, reason=str(error), driver='neocdz')


def validate_chd(source, spec, actual_sha256=None):
    with source.open('rb') as stream:
        header = stream.read(124)
    if len(header) != 124 or header[:8] != b'MComprHD' or struct.unpack('>II', header[8:16]) != (124, 5):
        raise DiscPending('requires_complete_chd_v5_not_raw_img')
    if header[104:124] != bytes(20):
        raise DiscPending('parent_chd_required')
    executable = Path(spec.get('chdVerifier', ''))
    if not executable.is_absolute() or not executable.is_file():
        raise DiscPending('chd_verifier_unavailable')
    env = verifier_environment(executable)
    try:
        info = subprocess.run([str(executable), 'info', '-i', str(source)], env=env,
                              capture_output=True, text=True, timeout=60)
    except subprocess.TimeoutExpired as error:
        raise DiscPending('chd_info_timeout') from error
    if info.returncode or not any(tag in info.stdout for tag in ("Tag='CHT2'", "Tag='CHTR'", "Tag='CHGT'")):
        raise DiscPending('chd_is_not_a_readable_cd')
    # Only the reviewed initial import passes this in memory, after checking a
    # sealed receipt from full chdman verification. It is never saved in the
    # live configuration. Full source SHA256 must match, not just size/mtime.
    reviewed = spec.get('_reviewedChdSha256', {}).get(source.name)
    if actual_sha256 is not None and actual_sha256 == reviewed:
        return
    try:
        check = subprocess.run([str(executable), 'verify', '-i', str(source)], env=env,
                               capture_output=True, text=True, timeout=600)
    except subprocess.TimeoutExpired as error:
        raise DiscPending('chd_verify_timeout') from error
    if check.returncode:
        raise DiscPending('chd_integrity_failed')


def zip_entry(name):
    entry = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
    entry.compress_type = zipfile.ZIP_STORED
    entry.external_attr = 0o100644 << 16
    return entry


def prepare_disc_artifact(source, temporary, supports, spec, describe):
    raw_descriptor = describe({'filePath': str(source)}, None)
    validate_chd(source, spec, raw_descriptor['sha256'])
    launch = source.stem + '.chd'
    if any(ord(c) < 32 or c in '/\\:' for c in launch):
        raise DiscPending('unsafe_chd_filename')
    try:
        members = bios_members(supports)
    except DiscPending:
        # The catalog and download remain available without firmware. Deliver
        # identical CHD bytes with the correct extension; the app explains the
        # missing runtime BIOS and lets the owner import it separately.
        descriptor = raw_descriptor
        descriptor.update(fileName=launch, launchPath=launch)
        return source, descriptor
    bios = temporary.parent / 'neocdz.zip'
    with zipfile.ZipFile(bios, 'w') as archive:
        for name, data in members:
            archive.writestr(zip_entry(name), data)
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_STORED) as package:
        for file, name in [(source, launch), (bios, bios.name)]:
            with file.open('rb') as stream, package.open(zip_entry(name), 'w', force_zip64=True) as target:
                shutil.copyfileobj(stream, target, 1024 * 1024)
    return temporary, describe({'filePath': str(temporary)}, launch)
