#!/usr/bin/env python3
"""Cross games with the local Turborama cover catalog; stage exact revista names."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import shutil
import unicodedata
import xml.etree.ElementTree as ET
from PIL import Image

CATEGORIES = {'gamecube': 'gamecube', 'psx': 'playstation-1', 'wii': 'nintendo-wii',
              'wiiu': 'nintendo-wii-u', 'switch': 'nintendo-switch'}
EXTENSIONS = {'gamecube': {'.iso', '.gcm', '.rvz'}, 'psx': {'.pbp', '.chd', '.cue'},
              'wii': {'.iso', '.rvz'}, 'wiiu': {'.rpx'}, 'switch': {'.nsp', '.xci'}}
ALIASES = {
 'gamecube': {
  'Legend of Zelda, The - The Wind Waker (Europe) (En,Fr,De,Es,It)': 'the Legend of Zelda, The - The Wind Waker (Europe) (En,Fr,De,Es,It)',
  'Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)': 'the Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)',
  'The Legend of Zelda The Wind Waker': 'the Legend of Zelda, The - The Wind Waker (Europe) (En,Fr,De,Es,It)',
  'The Legend of Zelda - Twilight Princess': 'the Legend of Zelda, The - Twilight Princess (Europe) (En,Fr,De,Es,It)',
  'Resident Evil - Code - Veronica X (PT-BR DUB) - (Disc1)': 'Resident Evil - Code - Veronica X',
  'Resident Evil - Code - Veronica X (PT-BR DUB) - (Disc2)': 'Resident Evil - Code - Veronica X',
  'Resident Evil 4 (Disco 1)': 'Resident Evil 4',
  'Resident Evil 4 (Disco 2)': 'Resident Evil 4',
  'Sonic Heroes (DUB)': 'Sonic Heroes'},
 'wii': {'Donkey Kong Country Returns.side': 'Donkey Kong Country Returns'}}
FALLBACKS = {
 ('psx', 'Harvest Moon Back To Nature'): 'Harvest Moon - Back to Nature (USA).png',
 ('psx', 'Jackie Chan Stuntmaster BR'): 'Jackie Chan Stuntmaster (USA).png',
 ('psx', 'World Soccer Winning Eleven 2002 (Japan)'): 'World Soccer Winning Eleven 2002 (Japan).png',
 ('wii', 'Epic Mickey 2 The Power of Two'): 'Disney Epic Mickey 2 - The Power of Two (USA) (En,Fr,Es,Pt).png'}


def normalized(value):
    ascii_text = unicodedata.normalize('NFKD', value).encode('ascii', 'ignore').decode().lower()
    return re.sub('[^a-z0-9]', '', ascii_text)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(volume, catalog, downloads, output):
    document = json.loads((catalog / 'catalog.json').read_bytes())
    remote = {r['file']: r for r in json.loads((downloads / 'downloaded-covers.json').read_bytes())}
    report = {'schemaVersion': 1, 'games': [], 'platforms': {}, 'missing': []}
    output.mkdir(mode=0o700, parents=True, exist_ok=True)
    for platform, category in CATEGORIES.items():
        items = [r for r in document['items'] if r['categoryId'] == category]
        by_title = {}
        for r in items:
            by_title.setdefault(normalized(r['title']), []).append(r)
        descriptions = {g.get('id'): g.findtext('description', '') for g in
                        ET.parse(catalog / 'GameDescriptions' / (category + '.xml')).getroot().findall('game')}
        cover_index = json.loads((catalog.parent.parent / 'Capas-Turborama-por-Sistema' / category / 'index.json').read_bytes())
        published = {r['id']: r for r in cover_index['items']}
        destination = output / platform
        (destination / 'media/revista').mkdir(parents=True, exist_ok=True)
        (destination / 'media/catalogo').mkdir(parents=True, exist_ok=True)
        seeds = {}
        for row in items:
            cover = catalog / row['image']
            cover_sha = sha(cover)
            if cover_sha != published[row['id']]['sha256']:
                raise ValueError('Seed cover differs from published index')
            target = 'media/catalogo/' + cover.name
            shutil.copy2(cover, destination / target)
            key = normalized(row['title'])
            if key in seeds:
                raise ValueError('Ambiguous automatic catalog seed')
            seeds[key] = {'cover': target, 'coverSha256': cover_sha,
                          'metadata': {'description': descriptions.get(row['id'], '')}}
        for alias, target in ALIASES.get(platform, {}).items():
            seeds[normalized(alias)] = seeds[normalized(target)]
        (destination / 'station-catalog-seed.json').write_text(json.dumps({'schemaVersion': 1, 'games': seeds}, ensure_ascii=False, indent=2) + '\n')
        gamelist = ET.Element('gameList')
        count = 0
        for rom in sorted((volume / platform).rglob('*')):
            if not rom.is_file() or rom.suffix.lower() not in EXTENSIONS[platform]:
                continue
            title = rom.parent.parent.name if platform == 'wiiu' else rom.stem
            lookup = ALIASES.get(platform, {}).get(title, title)
            candidates = by_title.get(normalized(lookup), [])
            if len(candidates) > 1:
                raise ValueError('Ambiguous cover catalog title: ' + title)
            relative = rom.relative_to(volume / platform).as_posix()
            description = ''
            if candidates:
                row = candidates[0]
                source = catalog / row['image']
                if not source.resolve().is_relative_to(catalog.resolve()) or source.is_symlink():
                    raise ValueError('Catalog image leaves its directory')
                original_sha = sha(source)
                if original_sha != published[row['id']]['sha256']:
                    raise ValueError('Catalog cover differs from its published system index')
                origin = 'catalogo-turborama-local'
                source_title = row['title']
                source_ref = row['id']
                description = descriptions.get(row['id'], '')
                rule = 'catalog_title' if lookup == title else 'reviewed_title_alias'
            elif (platform, title) in FALLBACKS:
                file = FALLBACKS[(platform, title)]
                proof = remote[file]
                source = downloads / file
                original_sha = sha(source)
                if original_sha != proof['sha256'] or proof['system'] != platform:
                    raise ValueError('Fallback cover proof differs')
                origin = 'libretro-thumbnails'
                source_title = Path(proof['path']).stem
                source_ref = proof['url']
                rule = 'exact_title_same_platform'
            else:
                report['missing'].append({'platform': platform, 'rom': relative})
                continue
            with Image.open(source) as image:
                image.verify()
            # Retain original cover bytes in revista. The importer compiles the
            # separate 480x720 phone image, keeping artwork and aspect ratio.
            cover_name = rom.stem + source.suffix.lower()
            shutil.copy2(source, destination / 'media/revista' / cover_name)
            game = ET.SubElement(gamelist, 'game')
            for key, value in [('path', './' + relative), ('name', title), ('desc', description),
                               ('image', './media/revista/' + cover_name)]:
                ET.SubElement(game, key).text = value
            report['games'].append({'platform': platform, 'rom': relative, 'name': title,
                'cover': 'media/revista/' + cover_name, 'source': origin, 'sourceTitle': source_title,
                'sourceReference': source_ref, 'sourceSha256': original_sha,
                'matchingRule': rule, 'descriptionPresent': bool(description)})
            count += 1
        ET.indent(gamelist, space='  ')
        ET.ElementTree(gamelist).write(destination / 'gamelist.xml', encoding='utf-8', xml_declaration=True)
        report['platforms'][platform] = count
    report['catalogFallbackGames'] = sum(len(json.loads((output / p / 'station-catalog-seed.json').read_text())['games']) for p in CATEGORIES)
    report['totalGames'] = len(report['games'])
    report['coverSources'] = dict(Counter(r['source'] for r in report['games']))
    report['catalogSha256'] = sha(catalog / 'catalog.json')
    (output / 'cover-plan.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    if report['missing']:
        raise ValueError('Unmatched games remain; do not publish a partial cover plan')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--volume', required=True, type=Path)
    parser.add_argument('--catalog', required=True, type=Path)
    parser.add_argument('--downloads', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    report = prepare(args.volume, args.catalog, args.downloads, args.output)
    print(json.dumps({k: report[k] for k in ('totalGames', 'platforms', 'coverSources', 'missing')}))


if __name__ == '__main__':
    main()
