#!/usr/bin/env python3
"""Discover ROM files, reconcile stable IDs, compile exact revista covers, publish atomically.

Private config/state/content stay outside Git. No credentials, network or service restart.
The caller owns a dedicated directory. Existing immutable releases are never deleted.
"""
import argparse
from collections import defaultdict, Counter
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile
import time
import re
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from PIL import Image, ImageOps, ImageDraw
from station_revista import select_revista_cover
from station_disc import DiscPending, prepare_disc_artifact, support_sources, bios_status
from station_packages import PackagePending, cue_reference_paths, package_layout, prepare_package, prepare_raw


def load_module(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name + '.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''): value.update(block)
    return value.hexdigest()


def stamp(path):
    s = path.stat()
    return [s.st_dev, s.st_ino, s.st_size, s.st_mtime_ns]


def atomic_json(path, data, gid=None):
    fd, name = tempfile.mkstemp(prefix='.station-', dir=path.parent)
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as f:
            os.fchmod(f.fileno(), 0o640 if gid is not None else 0o600)
            if gid is not None: os.fchown(f.fileno(), -1, gid)
            json.dump(data, f, ensure_ascii=False, separators=(',', ':'))
            f.write('\n'); f.flush(); os.fsync(f.fileno())
        os.replace(name, path)
        d = os.open(path.parent, os.O_DIRECTORY); os.fsync(d); os.close(d)
    finally:
        if os.path.exists(name): os.unlink(name)


def text(value, maximum):
    return ''.join(c for c in (value or '') if ord(c) >= 32 or c in '\n\t').strip()[:maximum]


def metadata(game=None, override=None):
    keys = {'description': ('desc', 2000), 'developer': ('developer', 80),
            'publisher': ('publisher', 80), 'genre': ('genre', 80),
            'players': ('players', 40), 'releaseDate': ('releasedate', 40)}
    result = {key: text((override or {}).get(key, game.findtext(xml, '') if game is not None else ''), size)
              for key, (xml, size) in keys.items()}
    # Same escaped JSON budget used by the signed .NET catalog, including non-ASCII.
    while len(json.dumps(result, ensure_ascii=True).encode()) > 7600:
        result['description'] = result['description'][:-80]
    return result


def safe_roms(root, extensions, excluded=()):
    excluded = {Path(p).resolve() for p in excluded}
    for directory, dirs, files in os.walk(root, followlinks=False):
        dirs[:] = sorted(d for d in dirs if d not in {'media', 'bios', '.station', '.git'} and
                          (Path(directory) / d).resolve() not in excluded and
                          not (Path(directory) / d).is_symlink())
        for name in sorted(files):
            path = Path(directory) / name
            if path.suffix.lower() in extensions | {'.zip', '.7z', '.rar'}:
                if path.is_symlink() or not path.resolve().is_relative_to(root):
                    raise ValueError('ROM link leaves platform root')
                yield path


def xml_games(root):
    file = root / 'gamelist.xml'
    if not file.exists(): return {}, 0
    if file.stat().st_size > 32 * 1024 * 1024: raise ValueError('gamelist is too large')
    raw = file.read_bytes()
    if b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper(): raise ValueError('XML entities forbidden')
    rows, missing = {}, 0
    for game in ET.fromstring(raw).findall('.//game'):
        path = (root / game.findtext('path', '')).resolve()
        if not path.is_relative_to(root) or path == root: raise ValueError('XML path escapes root')
        if str(path) in rows: raise ValueError('duplicate XML game path')
        rows[str(path)] = game
        missing += not path.is_file()
    return rows, missing


def compile_cover(source, temporary):
    if source is None:
        image = Image.new('RGB', (480, 720), '#17251d')
        ImageDraw.Draw(image).text((150, 345), 'CAPA PENDENTE', fill='#ffffff')
    else:
        with Image.open(source) as original:
            if original.width * original.height > 40_000_000: raise ValueError('cover dimensions too large')
            image = ImageOps.exif_transpose(original).convert('RGB')
            if image.size != (480, 720):
                # Fit the complete image; never crop a game cover or change its proportions.
                scaled = ImageOps.contain(image, (480, 720), Image.Resampling.LANCZOS)
                image = Image.new('RGB', (480, 720), '#17251d')
                image.paste(scaled, ((480-scaled.width)//2, (720-scaled.height)//2))
    image.save(temporary, format='JPEG', quality=90, optimize=True)


def catalog_key(value):
    return re.sub('[^a-z0-9]', '', unicodedata.normalize('NFKD', value).encode('ascii', 'ignore').decode().lower())


def catalog_seed(folder, spec):
    if not spec.get('catalogSeed'):
        return {}
    path = folder / spec['catalogSeed']
    if path.is_symlink() or not path.resolve().is_relative_to(folder.resolve()) or path.stat().st_size > 16 * 1024 * 1024:
        raise ValueError('unsafe catalog seed')
    document = json.loads(path.read_bytes())
    if document.get('schemaVersion') != 1 or not isinstance(document.get('games'), dict):
        raise ValueError('invalid catalog seed')
    return document['games']


def prepare_package_archive(source, extensions, explicit, mode):
    """Preserve archive contents when a CUE/RPX needs neighboring files."""
    module = load_module('preparar-indice-artefatos')
    members = module.archive_members(source, source.suffix.lower()[1:])
    names = [name for name, _ in members]
    primary = '.cue' if mode == 'cue-disc' else '.rpx'
    choices = [name for name in names if Path(name).suffix.lower() == primary]
    if not choices:
        choices = [name for name in names if Path(name).suffix.lower() in extensions]
    launch = explicit or (choices[0] if len(choices) == 1 else None)
    if launch not in choices:
        raise PackagePending('archive_requires_one_game_or_explicit_launch')
    if mode == 'wiiu-folder' and Path(launch).suffix.lower() == '.rpx':
        root = Path(launch).parent.parent
        if Path(launch).parent.name.casefold() != 'code' or not all(
                any(Path(n).is_relative_to(root / key) for n in names) for key in ('code', 'content', 'meta')):
            raise PackagePending('wiiu_archive_requires_code_content_meta')
    return source, module.describe({'filePath': str(source)}, launch)


def prepare_artifact(source, temporary, extensions, explicit=None):
    module = load_module('preparar-indice-artefatos')
    if source.suffix.lower() == '.zip':
        members = module.archive_members(source, 'zip')
        playable = [name for name, _ in members if Path(name).suffix.lower() in extensions]
        launch = explicit or (playable[0] if len(playable) == 1 else None)
        if launch not in playable: raise ValueError('archive requires an unambiguous playable member')
        with zipfile.ZipFile(source) as archive:
            if archive.testzip() is not None: raise ValueError('ZIP CRC failed')
            if len(members) > 1:
                # Same curation used for SNES/Mega: only the unique ROM goes to Android.
                with archive.open(launch) as src, zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED) as dest:
                    with dest.open(Path(launch).name, 'w', force_zip64=True) as out: shutil.copyfileobj(src, out, 1024*1024)
                return temporary, module.describe({'filePath': str(temporary)}, Path(launch).name)
        return source, module.describe({'filePath': str(source)}, launch)
    if source.suffix.lower() in {'.7z', '.rar'}:
        members = module.archive_members(source, source.suffix[1:])
        playable = [name for name, _ in members if Path(name).suffix.lower() in extensions]
        launch = explicit or (playable[0] if len(playable) == 1 else None)
        if launch not in playable: raise ValueError('archive requires an unambiguous playable member')
        return source, module.describe({'filePath': str(source)}, launch)
    return source, module.describe({'filePath': str(source)}, None)


def arcade_companions(folder, spec):
    module = load_module('preparar-indice-artefatos')
    result = []
    for name in spec.get('companions', []):
        module.safe_member(name)
        path = folder / name
        if path.is_symlink() or not path.resolve().is_relative_to(folder.resolve()) or not path.is_file():
            raise ValueError('arcade companion unavailable or unsafe')
        result.append(path)
    if len({p.name.casefold() for p in result}) != len(result):
        raise ValueError('duplicate arcade companion filename')
    return result


def prepare_arcade_artifact(source, temporary, companions):
    """Install intact emulator ZIPs through the existing one-level ZIP contract."""
    module = load_module('preparar-indice-artefatos')
    files = [source, *companions]
    if source.suffix.lower() != '.zip' or len({p.name.casefold() for p in files}) != len(files):
        raise ValueError('arcade game requires a unique ZIP filename')
    repairs = {}
    donors = {}
    for file in [*companions, source]:
        members = module.archive_members(file, 'zip')
        if not members or len(members) > module.MAX_FILES or sum(size for _, size in members) > module.MAX_EXPANDED:
            raise ValueError('arcade ZIP exceeds supported limits')
        if len({name.casefold() for name, _ in members}) != len(members):
            raise ValueError('duplicate arcade ROM member')
        with zipfile.ZipFile(file) as archive:
            if any(row.flag_bits & 1 for row in archive.infolist()):
                raise ValueError('arcade ZIP encrypted')
            for entry in archive.infolist():
                identity = (entry.filename.casefold(), entry.file_size, entry.CRC)
                try:
                    with archive.open(entry) as stream:
                        while stream.read(1024 * 1024):
                            pass  # Reading to EOF verifies the declared CRC.
                except zipfile.BadZipFile as error:
                    if file != source or identity not in donors:
                        raise ValueError('arcade ROM member CRC failed') from error
                    donor_path, donor_name = donors[identity]
                    with zipfile.ZipFile(donor_path) as donor:
                        repairs[entry.filename] = donor.read(donor_name)
                if file != source:
                    donors[identity] = (file, entry.filename)
    payload = source
    if repairs:
        # Restore only an exact filename/size/CRC found in a validated companion.
        # Keep the source archive untouched; all other chip bytes stay unchanged.
        payload = temporary.with_name('.repaired-' + source.name)
        import copy
        with zipfile.ZipFile(source) as archive, zipfile.ZipFile(payload, 'w') as rebuilt:
            for entry in archive.infolist():
                if entry.filename in repairs:
                    rebuilt.writestr(copy.copy(entry), repairs[entry.filename])
                else:
                    with archive.open(entry) as src, rebuilt.open(copy.copy(entry), 'w', force_zip64=True) as dest:
                        shutil.copyfileobj(src, dest, 1024 * 1024)
    # Already compressed emulator sets stay byte-identical, including all chip ROMs.
    # Fixed headers make unchanged input reproducible; no second decompression in app.
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_STORED) as package:
        for file, name in [(payload, source.name), *((p, p.name) for p in companions)]:
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.external_attr = 0o100644 << 16
            with file.open('rb') as src, package.open(info, 'w', force_zip64=True) as dest:
                shutil.copyfileobj(src, dest, 1024 * 1024)
    return temporary, module.describe({'filePath': str(temporary)}, source.name)


def content_identities(path):
    """Load identities qualified offline; never infer the payload SHA from its ZIP."""
    if path is None:
        return {}
    source = Path(path)
    if not source.is_absolute() or source.is_symlink() or not source.is_file() or source.stat().st_size > 16 * 1024 * 1024:
        raise ValueError('invalid content identity registry')
    def unique(pairs):
        value = {}
        for key, entry in pairs:
            if key in value: raise ValueError('duplicate content identity member')
            value[key] = entry
        return value
    document = json.loads(source.read_bytes(), object_pairs_hook=unique)
    if not isinstance(document, dict) or set(document) != {'schemaVersion', 'entries'} or type(document['schemaVersion']) is not int or document['schemaVersion'] not in (1,2):
        raise ValueError('invalid content identity schema')
    entries = document['entries']
    if not isinstance(entries, list) or len(entries) > 4096:
        raise ValueError('invalid content identity count')
    keys = {'itemId', 'platform', 'artifactSha256', 'launchPath', 'expandedSizeBytes', 'fileCount', 'contentSha256'}
    def hash_value(value):
        return isinstance(value, str) and len(value) == 64 and all(c in '0123456789abcdef' for c in value)
    result = {}
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) not in (keys,keys|{'contentIdentityScheme'}) or not hash_value(entry['artifactSha256']) or not hash_value(entry['contentSha256']):
            raise ValueError('invalid content identity entry')
        if 'contentIdentityScheme' in entry and (document['schemaVersion']!=2 or entry['contentIdentityScheme'] not in ('cue-set-v1','wiiu-set-v1') or (entry['contentIdentityScheme']=='cue-set-v1' and not entry['launchPath'].lower().endswith('.cue')) or (entry['contentIdentityScheme']=='wiiu-set-v1' and entry['platform']!='wiiu')):
            raise ValueError('invalid content identity scheme')
        if any(not isinstance(entry[key], str) or not entry[key] or len(entry[key]) > limit or any(ord(c) < 32 for c in entry[key]) for key, limit in [('itemId', 256), ('platform', 64), ('launchPath', 4096)]):
            raise ValueError('invalid content identity binding')
        if type(entry['expandedSizeBytes']) is not int or not 0 < entry['expandedSizeBytes'] <= 4 * (1 << 40) or type(entry['fileCount']) is not int or not 0 < entry['fileCount'] <= 100000:
            raise ValueError('invalid content identity bounds')
        if entry['itemId'] in result: raise ValueError('duplicate content item identity')
        result[entry['itemId']] = entry
    return result


def bind_content_identities(items, identities):
    for row in items:
        # Unknown or replaced payloads have no public hash; existing metadata is preserved.
        row.pop('contentSha256', None)
        row.pop('contentIdentityScheme', None)
        entry = identities.get(row['itemId'])
        artifact = row.get('artifact', {})
        if entry and entry['platform'] == row['platform'] and entry['artifactSha256'] == artifact.get('sha256') and all(entry[key] == artifact.get(key) for key in ('launchPath', 'expandedSizeBytes', 'fileCount')):
            row['contentSha256'] = entry['contentSha256']
            if entry.get('contentIdentityScheme'):row['contentIdentityScheme']=entry['contentIdentityScheme']


def publish(config, bootstrap=False, on_progress=None):
    root = Path(config['volumeRoot']).resolve()
    if not root.is_dir() or root.is_symlink() or root.stat().st_dev != config['volumeDevice']:
        raise ValueError('expected media volume is unavailable')
    home = Path(config['outputDirectory']).resolve()
    home.mkdir(mode=0o750, parents=True, exist_ok=True)
    gid = config.get('serviceGid')
    if gid is not None: os.chown(home, -1, gid)
    with (home / 'scan.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        index_path, state_path = home / 'index.json', home / 'state.json'
        previous = json.loads((index_path if index_path.exists() else Path(config['baseIndex'])).read_text())
        identities = content_identities(config.get('contentIdentityRegistry'))
        rows = {row['itemId']: row for row in previous['items']}
        state = json.loads(state_path.read_text()) if state_path.exists() else {'sources': {}, 'observations': {}}
        if not state['sources']:
            for entry in json.loads(Path(config['seedSourceMap']).read_text())['items']:
                if entry['itemId'] in rows:
                    key = entry['sourcePath']; value = state['sources'].setdefault(key, {'ids': [], 'fingerprint': None})
                    value['ids'].append(entry['itemId'])
        override_file = Path(config['metadataOverrides'])
        overrides = json.loads(override_file.read_text()) if override_file.exists() else {}
        seed_file = config.get('metadataSeed')
        metadata_seed = {r['itemId']: r for r in json.loads(Path(seed_file).read_text())} if seed_file else {}
        report = {'platforms': {}, 'pending': [], 'added': 0, 'updated': 0, 'metadataMissing': 0,
                  'missingXmlRoms': 0, 'placeholderCovers': 0}
        original = json.dumps(previous['items'], sort_keys=True)
        for platform, spec in config['platforms'].items():
            folder = root / spec.get('folder', platform)
            if not folder.is_dir(): continue
            if folder.is_symlink() or not folder.resolve().is_relative_to(root): raise ValueError('platform folder is unsafe')
            games, missing = xml_games(folder)
            catalog_games = catalog_seed(folder, spec)
            report['missingXmlRoms'] += missing
            mode = spec.get('artifactMode', 'single-rom')
            if mode not in {'single-rom', 'arcade-set', 'chd-disc', 'cue-disc', 'wiiu-folder'}:
                raise ValueError('unknown platform artifact mode')
            game_home = Path(spec.get('artifactDirectory', str(home / 'games')))
            if not game_home.is_absolute() or game_home.resolve() != game_home or game_home.is_symlink():
                raise ValueError('unsafe artifact directory')
            if not (game_home.is_relative_to(home) or game_home.is_relative_to(root)):
                raise ValueError('artifact directory leaves configured volumes')
            game_home.mkdir(mode=0o750, parents=True, exist_ok=True)
            companions = (arcade_companions(folder, spec) if mode == 'arcade-set' else
                          support_sources(folder, root, spec) if mode == 'chd-disc' else [])
            companion_base = root if mode == 'chd-disc' else folder
            companion_stamp = [[p.relative_to(companion_base).as_posix(), stamp(p)] for p in companions]
            if mode == 'chd-disc':
                report.setdefault('runtimeRequirements', {})[platform] = bios_status(companions)
            nested_platforms = [root / other.get('folder', name) for name, other in config['platforms'].items()
                                if name != platform and (root / other.get('folder', name)).resolve().is_relative_to(folder.resolve())]
            magazine = defaultdict(list)
            revista = folder / 'media' / 'revista'
            if revista.exists():
                for p in revista.rglob('*'):
                    if p.is_file() and p.suffix.lower() in {'.jpg', '.jpeg', '.png', '.webp', '.gif'}:
                        magazine[p.stem].append(p)
            roms = list(safe_roms(folder.resolve(), set(spec['extensions']), nested_platforms))
            cue_dependencies = set()
            if mode == 'cue-disc':
                for cue in roms:
                    if cue.suffix.lower() == '.cue':
                        cue_dependencies.update(p.resolve() for p in cue_reference_paths(cue))
            wiiu_roots = [p.parent.parent for p in roms if p.suffix.lower() == '.rpx' and p.parent.name.casefold() == 'code'] if mode == 'wiiu-folder' else []
            for rom in roms:
                if rom.resolve() in cue_dependencies:
                    continue
                if rom.suffix.lower() != '.rpx' and any(rom.is_relative_to(p) for p in wiiu_roots):
                    continue  # Archives inside game content are dependencies, not games.
                if rom in companions:
                    continue  # BIOS/support archives are dependencies, never listed as games.
                if mode == 'chd-disc' and rom.suffix.lower() not in spec['extensions']:
                    continue  # Firmware ZIPs are support, not discs.
                folder_path = list(rom.relative_to(folder).parent.parts)
                if mode == 'wiiu-folder' and rom.suffix.lower() == '.rpx':
                    folder_path = list(rom.parent.parent.relative_to(folder).parent.parts)
                if len(folder_path)>8 or any(not n.strip() or n in {'.','..'} or len(n.encode('utf-16-le'))//2>80 or any(ord(c)<32 or c in '/\\' for c in n) for n in folder_path):
                    report['pending'].append({'platform':platform,'rom':rom.relative_to(folder).as_posix(),'reason':'invalid_folder_path'});continue
                key = str(rom.resolve()); existing = state['sources'].get(key)
                relative = rom.relative_to(folder).as_posix()
                game = games.get(key)
                catalog_game = catalog_games.get(catalog_key(rom.parent.parent.name if mode == 'wiiu-folder' and rom.suffix.lower() == '.rpx' else rom.stem), {})
                xml_fields = {'description': 'desc', 'releaseDate': 'releasedate'}
                override = {k: v for k, v in catalog_game.get('metadata', {}).items()
                            if game is None or not game.findtext(xml_fields.get(k, k), '').strip()}
                override.update(overrides.get(platform + ':' + relative, {}))
                # Existing platform aliases (including BR) remain unchanged.
                actual_platform = spec.get('regionalPlatform', platform+'br') if 'pt-br' in rom.relative_to(folder).parts else platform
                stamp_rom = stamp(rom)
                candidates = magazine.get(rom.stem, [])
                catalog_cover = None
                if not candidates and catalog_game.get('cover'):
                    catalog_cover = folder / catalog_game['cover']
                    if catalog_cover.is_symlink() or not catalog_cover.resolve().is_relative_to((folder / 'media').resolve()):
                        raise ValueError('catalog cover leaves media directory')
                    candidates = [catalog_cover]
                cover_stamp = [[str(p.relative_to(folder)), stamp(p)] for p in sorted(candidates)]
                fingerprint = [stamp_rom, cover_stamp]
                package_stamp = []
                if mode in {'cue-disc', 'wiiu-folder'} and rom.suffix.lower() not in {'.zip', '.rar', '.7z'}:
                    try:
                        members, _ = package_layout(rom, mode)
                        package_stamp = [[p.relative_to(folder).as_posix(), stamp(p)] for _, p, _ in members]
                        fingerprint.append([mode, package_stamp])
                    except PackagePending as error:
                        report['pending'].append({'platform': platform, 'rom': relative, 'reason': str(error)})
                        continue
                if mode in {'arcade-set', 'chd-disc'}:
                    fingerprint.append([mode, companion_stamp])
                unchanged = existing and existing['fingerprint'] == fingerprint
                seeded = existing and existing['fingerprint'] is None
                if seeded or unchanged:
                    for item_id in existing['ids']:
                        row = rows[item_id]
                        row['folderPath'] = folder_path
                        new_metadata = metadata(game, override)
                        seed = metadata_seed.get(item_id)
                        if not new_metadata['description'] and seed and seed['platform'] == row['platform'] and seed['name'] == row['name']:
                            new_metadata['description'] = metadata(override={'description':seed['description']})['description']
                        if row.get('metadata') != new_metadata: row['metadata'] = new_metadata
                        if override.get('name'): row['name'] = text(override['name'], 120)
                    existing['fingerprint'] = fingerprint
                    report['metadataMissing'] += not metadata(game, override)['description']
                    continue
                seen = state['observations'].get(key)
                state['observations'][key] = fingerprint
                recent = any(time.time_ns() - entry[1][3] < 20_000_000_000 for entry in package_stamp)
                if not bootstrap and (seen != fingerprint or recent or time.time_ns()-stamp_rom[3] < 20_000_000_000):
                    report['pending'].append({'platform': platform, 'rom': relative, 'reason': 'copy_not_stable'}); continue
                try:
                    source_cover = None
                    if catalog_cover is not None:
                        if digest(catalog_cover) != catalog_game['coverSha256']:
                            raise PackagePending('catalog_cover_changed')
                        source_cover = catalog_cover
                    elif candidates: source_cover, _, _ = select_revista_cover(folder, rom, magazine)
                    with tempfile.TemporaryDirectory(prefix='.build-', dir=game_home) as tmp:
                        tmp = Path(tmp)
                        if mode == 'arcade-set':
                            game_source, descriptor = prepare_arcade_artifact(rom, tmp / rom.name, companions)
                        elif mode == 'chd-disc':
                            game_source, descriptor = prepare_disc_artifact(rom, tmp / (rom.stem+'.zip'), companions,
                                                                           spec, load_module('preparar-indice-artefatos').describe)
                        elif mode in {'cue-disc', 'wiiu-folder'} and rom.suffix.lower() in {'.zip', '.rar', '.7z'}:
                            game_source, descriptor = prepare_package_archive(rom, set(spec['extensions']), override.get('launchPath'), mode)
                        elif mode in {'cue-disc', 'wiiu-folder'}:
                            temporary_name = (rom.parent.parent.name + '.zip' if mode == 'wiiu-folder' and rom.suffix.lower() == '.rpx' else
                                              rom.stem + '.zip' if rom.suffix.lower() == '.cue' else rom.name)
                            game_source, descriptor = prepare_package(rom, tmp / temporary_name, mode,
                                                                      load_module('preparar-indice-artefatos').describe)
                        elif spec.get('copyRawOnce') and rom.suffix.lower() not in {'.zip', '.7z', '.rar'}:
                            game_source, descriptor = prepare_raw(rom, tmp / rom.name)
                        else:
                            game_source, descriptor = prepare_artifact(rom, tmp / rom.name, set(spec['extensions']), override.get('launchPath'))
                        cover_tmp = tmp / 'cover.jpg'; compile_cover(source_cover, cover_tmp)
                        game_dir = game_home / descriptor['sha256']; game_dir.mkdir(mode=0o750, parents=True, exist_ok=True)
                        target = game_dir / descriptor['fileName']
                        if not target.exists():
                            if game_source.is_relative_to(tmp):
                                # Freshly built and hashed package: publish on the same
                                # filesystem without copying or hashing its body again.
                                with game_source.open('rb') as built: os.fsync(built.fileno())
                                os.replace(game_source, target)
                            else:
                                copied_hash = hashlib.sha256()
                                with game_source.open('rb') as src, target.open('xb') as dest:
                                    for block in iter(lambda: src.read(1024*1024), b''):
                                        copied_hash.update(block); dest.write(block)
                                    dest.flush(); os.fsync(dest.fileno())
                                if copied_hash.hexdigest() != descriptor['sha256']: target.unlink(); raise ValueError('copied ROM hash mismatch')
                        elif digest(target) != descriptor['sha256']: raise ValueError('existing immutable ROM hash mismatch')
                        if target.stat().st_size != descriptor['sizeBytes']: raise ValueError('immutable ROM size mismatch')
                        cover_dir = home / 'covers'; cover_dir.mkdir(mode=0o750, exist_ok=True)
                        cover_hash = digest(cover_tmp); cover_target = cover_dir / (cover_hash+'.jpg')
                        if not cover_target.exists(): shutil.copyfile(cover_tmp, cover_target)
                        for p in (game_dir.parent, game_dir, target, cover_dir, cover_target):
                            p.chmod(0o750 if p.is_dir() else 0o640)
                            if gid is not None: os.chown(p, -1, gid)
                        if stamp(rom) != stamp_rom or [[str(p.relative_to(folder)), stamp(p)] for p in sorted(candidates)] != cover_stamp:
                            raise ValueError('source changed while compiling')
                        if [[p.relative_to(companion_base).as_posix(), stamp(p)] for p in companions] != companion_stamp:
                            raise ValueError('arcade companion changed while compiling')
                        if package_stamp and [[p.relative_to(folder).as_posix(), stamp(p)] for _, p, _ in members] != package_stamp:
                            raise ValueError('package input changed while compiling')
                        name = text(override.get('name', game.findtext('name', '') if game is not None else ''), 120) or text(rom.stem, 120)
                        ids = existing['ids'] if existing else ['station_' + hashlib.sha256((actual_platform+':'+relative).encode()).hexdigest()[:32]]
                        for item_id in ids:
                            old = rows.get(item_id)
                            token = item_id.removeprefix('station_')
                            row = dict(itemId=item_id, name=name, platform=old['platform'] if old else actual_platform,
                                       revision=(old['revision']+1) if old else previous['revision']+1,
                                       coverId=old['coverId'] if old else 'cover_'+token, filePath=str(target),
                                       coverPath=str(cover_target), artifact=descriptor, catalogVisible=old.get('catalogVisible', True) if old else True,
                                       metadata=metadata(game, override), folderPath=folder_path)
                            rows[item_id] = row
                            report['updated' if old else 'added'] += 1
                        state['sources'][key] = {'ids': ids, 'fingerprint': fingerprint}
                        report['placeholderCovers'] += source_cover is None
                        report['metadataMissing'] += not row['metadata']['description']
                        if on_progress is not None:
                            on_progress(report['added'], report['updated'])
                except (ValueError, OSError, zipfile.BadZipFile, ET.ParseError) as error:
                    reason = str(error) if isinstance(error, (DiscPending, PackagePending)) else type(error).__name__
                    report['pending'].append({'platform': platform, 'rom': relative, 'reason': reason})
        for row in rows.values():
            seed = metadata_seed.get(row['itemId'])
            if seed and seed['platform'] == row['platform'] and seed['name'] == row['name'] and not row.get('metadata', {}).get('description'):
                row['metadata'] = dict(row.get('metadata', metadata()), description=metadata(override={'description':seed['description']})['description'])
        if len(rows) > 4096: raise ValueError('Station library capacity exceeded')
        items = list(rows.values())
        if config.get('autoContentIdentity', False):
            registry_path=Path(config['contentIdentityRegistry'])
            if type(config['autoContentIdentity']) is not bool or registry_path.parent!=home or registry_path.is_symlink():
                raise ValueError('automatic identity output must be inside the private library')
            binder_path=Path(__file__).with_name('prepare_content_identity_registry.py')
            if not binder_path.exists():binder_path=Path(__file__).resolve().parents[1]/'entrega-app-r81-20261008/server-tools/prepare_content_identity_registry.py'
            binder_spec=importlib.util.spec_from_file_location('station_auto_identity_binder',binder_path)
            binder=importlib.util.module_from_spec(binder_spec);binder_spec.loader.exec_module(binder)
            from station_content_sets import bind_set
            for row in items:
                artifact=row.get('artifact',{});entry=identities.get(row['itemId'])
                if entry and entry['platform']==row['platform'] and entry['artifactSha256']==artifact.get('sha256') and all(entry[k]==artifact.get(k) for k in ('launchPath','expandedSizeBytes','fileCount')):continue
                try:
                    identities[row['itemId']]=bind_set(row,binder.bind(row,row['filePath']))
                except (binder.ValidationError,ValueError,OSError,zipfile.BadZipFile) as error:
                    report.setdefault('onlineIdentityPending',[]).append({'itemId':row['itemId'],'reason':type(error).__name__})
            document=dict(schemaVersion=2,entries=sorted(identities.values(),key=lambda row:row['itemId']))
            previous_identities=json.loads(registry_path.read_bytes())
            if document!=previous_identities:atomic_json(registry_path,document,gid)
        bind_content_identities(items, identities)
        online=config.get('autoOnlineProfiles')
        if online is not None:
            if not isinstance(online,dict) or set(online)!={'registry','engineManifest','maintainerAuthorizedTwoSeats'} or online['maintainerAuthorizedTwoSeats'] is not True:
                raise ValueError('explicit automatic room policy required')
            output=Path(online['registry']);manifest=Path(online['engineManifest'])
            if output.parent!=home or output.is_symlink() or not manifest.is_absolute() or manifest.is_symlink():raise ValueError('unsafe automatic room inputs')
            if output.stat().st_size>16*1024*1024 or manifest.stat().st_size>32768:raise ValueError('automatic room input too large')
            from station_online_profiles import prepare
            current_profiles=json.loads(output.read_bytes())
            updated_profiles=prepare(items,current_profiles,json.loads(manifest.read_bytes()))
            if updated_profiles!=current_profiles:atomic_json(output,updated_profiles,gid)
        changed = original != json.dumps(items, sort_keys=True)
        result = dict(previous, revision=previous['revision']+1 if changed else previous['revision'], items=items)
        report['metadataMissing'] = sum(not r.get('metadata', {}).get('description') for r in items if r.get('catalogVisible', True))
        report.update(revision=result['revision'], changed=changed, items=len(items),
                      visible=sum(x.get('catalogVisible', True) for x in items),
                      platforms=dict(Counter(x['platform'] for x in items if x.get('catalogVisible', True))))
        if changed or not index_path.exists(): atomic_json(index_path, result, gid)
        atomic_json(state_path, state)
        atomic_json(home / 'report.json', report)
        return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--bootstrap', action='store_true', help='Initial reviewed import; skip two-scan observation wait')
    args = parser.parse_args()
    try:
        report = publish(json.loads(args.config.read_text()), args.bootstrap)
        print(json.dumps({key: report[key] for key in ('revision', 'changed', 'added', 'updated', 'visible', 'platforms')}))
    except BlockingIOError: print('Station scan already running')
    except Exception as error:
        print('Station scan rejected; existing index preserved ('+type(error).__name__+')')
        raise SystemExit(1)


if __name__ == '__main__': main()
