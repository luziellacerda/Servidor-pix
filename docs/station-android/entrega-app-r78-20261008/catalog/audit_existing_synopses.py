"""Inventory existing synopsis prose without title guessing, ROM reads or tests.

Reads the R77 frozen catalog, R37 exact-ID descriptions, R39 metadata XML and
current pertinent RetroBat gamelists. Outputs an audit, not an app update or a
claim that each historical statement has been verified.
"""
from pathlib import Path, PurePosixPath
from collections import Counter, defaultdict
from datetime import datetime, timezone
import argparse
import csv
import hashlib
import io
import json
import posixpath
import re
import subprocess
import unicodedata
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
R37 = REPO / 'versions/station-final-details-r37-20261005'
R39 = REPO / 'versions/station-theme-collections-r39-20261006'
R77 = REPO / 'versions/station-multiplayer-r77-20261008'
RETROBAT = Path(r'G:\TURBORAMA\RetroBat\roms')
SERVER = Path(r'E:\ESTUDO APK\work\server-auth-handoff\Servidor-pix-implementar-faltas-20261002')
SERVER_COMMIT = 'bee3dcd5c2c0c0a228805957fe89b9a6023e8402'
SERVER_FOLDER = 'docs/station-android/biblioteca-neogeocd-20261005/'
OUTPUT = Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\catalog')
PLACEHOLDER = re.compile(r'(?i)\b(?:sinopse\s+(?:ainda\s+)?(?:não|nao)\s+(?:disponível|disponivel|localizada)|'
                         r'(?:descrição|descricao)\s+(?:não|nao)\s+(?:disponível|disponivel)|'
                         r'no description(?: available)?|description unavailable|lorem ipsum|sinopse em breve)\b')
VARIANT = re.compile(r'(?i)\b(?:hack|bootleg|prototype|proto|beta|demo|unlicensed|unl|pirate)\b')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def dump(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def norm(text):
    return unicodedata.normalize('NFC', text).casefold().strip()


def title_key(text):
    return ''.join(c for c in norm(text) if c.isalnum())


def text_quality(text, name, launch):
    if not text.strip():
        return 'empty'
    if '\ufffd' in text or any(ord(c) < 32 and c not in '\r\n\t' for c in text):
        return 'invalid-characters'
    if PLACEHOLDER.search(text):
        return 'explicit-placeholder'
    if title_key(text) in {title_key(name), title_key(PurePosixPath(launch.replace('\\', '/')).stem)}:
        return 'title-only'
    if len(text.strip()) < 30:
        return 'fragment-under-30-characters'
    return 'prose-present-not-fact-checked'


def xml_document(raw):
    if b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
        raise ValueError('XML entities/doctype are not accepted')
    return ET.fromstring(raw)


def classify_platform(base, relative):
    # These are explicit local folders, not broad BR/title/region normalization.
    parts = PurePosixPath(relative).parts
    if base == 'snes' and parts and parts[0] == '## 1 -PT-BR ##':
        return 'snesbr'
    if base == 'megadrive' and parts and parts[0] == '# PT-BR #':
        return 'megadrivebr'
    return base


def source_record(source_id, raw, kind, **extra):
    return {'sourceId': source_id, 'kind': kind, 'bytes': len(raw), 'sha256': sha(raw), **extra}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    sources = []
    raw_inventory = (R77 / 'catalog/catalog-inventory.json').read_bytes()
    inventory = json.loads(raw_inventory)
    descriptor_fields = ('platform', 'artifactSha256', 'artifactSizeBytes', 'artifactLaunchPath',
                         'artifactExpandedSizeBytes', 'artifactFileCount')
    inventory_by_id = {row['itemId']: row for row in inventory['records']}
    same_descriptor = defaultdict(list)
    for row in inventory['records']:
        same_descriptor[tuple(row[key] for key in descriptor_fields)].append(row)
    sources.append(source_record('r77/catalog-inventory.json', raw_inventory, 'frozen-catalog-inventory'))
    raw_r37 = (R37 / 'data/synopses-complete.json').read_bytes()
    r37 = {row['itemId']: row for row in json.loads(raw_r37)}
    sources.append(source_record('r37/synopses-complete.json', raw_r37, 'exact-id-existing-descriptions'))
    raw_editorial = (R37 / 'data/synopses-editorial.json').read_bytes()
    editorial = json.loads(raw_editorial)
    sources.append(source_record('r37/synopses-editorial.json', raw_editorial, 'existing-editorial-provenance'))
    server_rows = {}
    for filename in ('catalogo-completo.tsv', 'compatibilidade-ids.tsv'):
        raw = subprocess.check_output(['git', '-c', 'safe.directory=' + SERVER.as_posix(), '-C', str(SERVER),
                                       'show', SERVER_COMMIT + ':' + SERVER_FOLDER + filename])
        source_id = 'server/' + filename
        sources.append(source_record(source_id, raw, 'published-catalog-description', commit=SERVER_COMMIT,
                                     repositoryPath=SERVER_FOLDER + filename))
        for ordinal, row in enumerate(csv.DictReader(io.StringIO(raw.decode('utf-8-sig')), delimiter='\t'), 2):
            row['_sourceId'] = source_id
            row['_line'] = ordinal
            if row['itemId'] in server_rows:
                raise ValueError('Duplicate published item identity')
            server_rows[row['itemId']] = row
    r39_stats = []
    for path in sorted((R39 / 'metadata-xml').glob('*.xml')):
        raw = path.read_bytes()
        root = xml_document(raw)
        games = root.findall('game')
        description_fields = Counter(tag for game in games for tag in ('desc', 'description', 'overview', 'synopsis')
                                     if game.findtext(tag, '').strip())
        source_id = 'r39/metadata-xml/' + path.name
        sources.append(source_record(source_id, raw, 'legacy-players-rating-metadata'))
        r39_stats.append({'platform': root.get('platform', path.stem), 'records': len(games),
                          'synopsisFieldsPresent': dict(description_fields), 'sourceId': source_id})
    xml_records = []
    xml_stats = []
    # Read only current gamelist.xml; old backup/broken XMLs cannot override current sources.
    for base in ('snes', 'megadrive', 'n64', 'neogeo', 'neogeocd'):
        system_root = RETROBAT / base
        for path in sorted(system_root.rglob('gamelist.xml')):
            if '_arquivo_codex' in path.parts:
                continue
            raw = path.read_bytes()
            source_id = 'retrobat/' + path.relative_to(RETROBAT).as_posix()
            sources.append(source_record(source_id, raw, 'current-local-gamelist'))
            try:
                games = xml_document(raw).findall('game')
            except ET.ParseError as exc:
                xml_stats.append({'sourceId': source_id, 'parseError': str(exc), 'records': None})
                continue
            stats = Counter()
            for ordinal, game in enumerate(games, 1):
                game_path = game.findtext('path', '').replace('\\', '/')
                local_folder = path.parent.relative_to(system_root).as_posix()
                relative = posixpath.normpath(posixpath.join(local_folder, game_path))
                if relative.startswith('../') or relative.startswith('/') or ':' in relative:
                    stats['unsafe-or-external-path'] += 1
                    continue
                platform = classify_platform(base, relative)
                name = game.findtext('name', '')
                desc = game.findtext('desc', '')
                quality = text_quality(desc, name, game_path)
                stats[quality] += 1
                xml_records.append({'platform': platform, 'sourceId': source_id, 'ordinal': ordinal,
                                    'path': relative, 'basename': PurePosixPath(relative).name,
                                    'name': name, 'description': desc, 'quality': quality,
                                    'descriptionSha256': sha(desc.encode('utf8')),
                                    'stableId': 'station_' + sha((platform + ':' + relative).encode('utf8'))[:32],
                                    'provider': game.get('source', ''), 'providerId': game.get('id', ''),
                                    'declaredMD5': game.findtext('md5', '') or None,
                                    'declaredCRC32': game.findtext('hash', '') or None})
            xml_stats.append({'sourceId': source_id, 'records': len(games), 'qualityCounts': dict(stats)})
    by_id = defaultdict(list)
    by_file = defaultdict(list)
    for row in xml_records:
        by_id[row['stableId']].append(row)
        by_file[(row['platform'], norm(row['basename']))].append(row)
    records = []
    groups = defaultdict(list)
    for item in inventory['records']:
        published = server_rows[item['itemId']]
        for field in ('platform', 'name', 'artifactSha256', 'artifactFileName', 'artifactLaunchPath'):
            if published[field] != item[field]:
                raise ValueError('Frozen catalog identity changed: ' + item['itemId'])
        old = r37.get(item['itemId'])
        old_exact = (old is not None and old['platform'] == item['platform'] and old['name'] == item['name']
                     and old['catalogItemRevision'] == item['itemRevision'])
        selected = old['description'] if old_exact else published['description']
        origin = ('r37/' + old['sourceKind']) if old_exact else 'server/compatibility-own-description'
        quality = text_quality(selected, item['name'], item['artifactLaunchPath'])
        alias_candidates = []
        if quality != 'prose-present-not-fact-checked':
            for equivalent in same_descriptor[tuple(item[key] for key in descriptor_fields)]:
                if equivalent['itemId'] == item['itemId'] or not equivalent['catalogVisible']:
                    continue
                prose = r37.get(equivalent['itemId'])
                if (prose is not None and prose['platform'] == equivalent['platform']
                        and prose['name'] == equivalent['name'] and prose['catalogItemRevision'] == equivalent['itemRevision']
                        and text_quality(prose['description'], prose['name'], equivalent['artifactLaunchPath']) == 'prose-present-not-fact-checked'):
                    alias_candidates.append({'sourceItemId': equivalent['itemId'], 'sourceName': equivalent['name'],
                                             'sourceItemRevision': equivalent['itemRevision'],
                                             'descriptionSha256': sha(prose['description'].encode('utf8')),
                                             'sourceKind': prose['sourceKind'], 'identity': 'same-exact-published-artifact-and-launch-descriptor',
                                             'descriptor': {key: item[key] for key in descriptor_fields},
                                             'automaticallyApplied': False, 'gameFactsVerified': False})
        exact_local = [row for row in by_id[item['itemId']] if row['platform'] == item['platform']]
        lookup = exact_local
        method = 'exact-platform-path-derived-itemId'
        if not lookup:
            # Exact full filename INCLUDING extension/region tags only; no region/hack stripping.
            lookup = by_file[(item['platform'], norm(PurePosixPath(item['artifactLaunchPath']).name))]
            method = 'exact-platform-full-launch-filename-candidate'
        if not lookup:
            lookup = by_file[(item['platform'], norm(item['artifactFileName']))]
            method = 'exact-platform-full-artifact-filename-candidate'
        local_candidates = []
        for row in lookup:
            candidate = {k: row[k] for k in ('sourceId', 'ordinal', 'path', 'name', 'quality', 'descriptionSha256',
                                             'provider', 'providerId', 'declaredMD5', 'declaredCRC32')}
            candidate['matchMethod'] = method
            candidate['sameTextAsSelected'] = row['description'] == selected
            local_candidates.append(candidate)
        reasons = ['historical-facts-not-individually-verified']
        if old and not old_exact:
            reasons.append('r37-identity-mismatch')
        if quality != 'prose-present-not-fact-checked':
            reasons.append('existing-synopsis-needs-completion')
        if selected.strip() and len(selected.strip()) < 180:
            reasons.append('short-editorial-depth')
        variant = item['platform'].endswith('br') or bool(VARIANT.search(item['name'] + ' ' + item['artifactFileName']))
        if variant:
            reasons.append('named-translation-hack-or-variant-needs-specific-review')
        if len({row['descriptionSha256'] for row in lookup if row['quality'] == 'prose-present-not-fact-checked'}) > 1:
            reasons.append('different-local-descriptions-for-exact-file')
        record = {key: item[key] for key in ('itemId', 'platform', 'name', 'catalogVisible', 'itemRevision',
                                            'folderPath', 'artifactFileName', 'artifactLaunchPath', 'artifactSha256', 'contentSha256')}
        record.update({'selectedDescription': selected, 'selectedDescriptionSha256': sha(selected.encode('utf8')),
                       'publishedServerDescription': published['description'],
                       'publishedServerDescriptionSha256': sha(published['description'].encode('utf8')),
                       'selectionOrigin': origin, 'quality': quality, 'r37ExactIdentity': old_exact,
                       'catalogSource': {'sourceId': published['_sourceId'], 'line': published['_line']},
                       'existingProvenance': old.get('provenance') if old_exact else None,
                       'localCandidates': local_candidates, 'localCandidateAutoApplied': False,
                       'exactDescriptorAliasCandidates': alias_candidates,
                       'contentBytesRead': False, 'factChecked': False, 'onlineAuthorization': False,
                       'reviewReasons': reasons})
        records.append(record)
        if selected.strip():
            groups[record['selectedDescriptionSha256']].append(record)
    duplicate_groups = []
    for digest, members in groups.items():
        if len(members) < 2:
            continue
        distinct = sorted({(r['platform'], r['name']) for r in members})
        duplicate_groups.append({'descriptionSha256': digest, 'itemIds': [r['itemId'] for r in members],
                                 'distinctPlatformTitles': distinct,
                                 'requiresReviewNotAutomaticallyPlaceholder': len(distinct) > 1})
    platform_stats = {}
    for platform in sorted({r['platform'] for r in records}):
        subset = [r for r in records if r['platform'] == platform]
        platform_stats[platform] = {'ids': len(subset), 'visibleIds': sum(r['catalogVisible'] for r in subset),
                                   'qualityCounts': dict(Counter(r['quality'] for r in subset)),
                                   'sources': dict(Counter(r['selectionOrigin'] for r in subset)),
                                   'exactFileLocalCandidates': sum(bool(r['localCandidates']) for r in subset)}
    result = {'schemaVersion': 1, 'purpose': 'existing-description-audit-not-factual-certification',
              'catalogRevision': inventory['catalogRevision'], 'records': records}
    dump(args.output / 'existing-synopses-audit.json', result)
    coverage = {'utc': datetime.now(timezone.utc).isoformat(), 'catalogIds': len(records),
                'visibleIds': sum(r['catalogVisible'] for r in records),
                'qualityCounts': dict(Counter(r['quality'] for r in records)),
                'visibleQualityCounts': dict(Counter(r['quality'] for r in records if r['catalogVisible'])),
                'selectionOrigins': dict(Counter(r['selectionOrigin'] for r in records)),
                'platforms': platform_stats, 'shortUnder180Ids': sum('short-editorial-depth' in r['reviewReasons'] for r in records),
                'variantReviewIds': sum('named-translation-hack-or-variant-needs-specific-review' in r['reviewReasons'] for r in records),
                'missingItems': [{k: r[k] for k in ('itemId', 'platform', 'name', 'catalogVisible', 'artifactFileName', 'artifactLaunchPath', 'quality', 'localCandidates', 'exactDescriptorAliasCandidates')}
                                 for r in records if r['quality'] != 'prose-present-not-fact-checked'],
                'shortDescriptions': [{k: r[k] for k in ('itemId', 'platform', 'name', 'selectedDescription', 'selectionOrigin')}
                                      for r in records if 'short-editorial-depth' in r['reviewReasons']],
                'missingWithOneExactDescriptorAlias': sum(len(r['exactDescriptorAliasCandidates']) == 1 for r in records),
                'r39XmlFiles': len(r39_stats), 'r39MetadataRecords': sum(x['records'] for x in r39_stats),
                'r39SynopsisFields': sum(sum(x['synopsisFieldsPresent'].values()) for x in r39_stats),
                'r39Stats': r39_stats, 'localXmlStats': xml_stats,
                'localCandidateIds': sum(bool(r['localCandidates']) for r in records),
                'duplicateTextGroups': len(duplicate_groups),
                'duplicateTextGroupsAcrossDifferentPlatformTitles': sum(g['requiresReviewNotAutomaticallyPlaceholder'] for g in duplicate_groups),
                'editorialEntries': len(editorial), 'editorialWebUrls': sorted({s['url'] for e in editorial for s in e['sources'] if s['kind'] == 'web'}),
                'sources': sources, 'outputSHA256': sha((args.output / 'existing-synopses-audit.json').read_bytes()),
                'recipeSHA256': sha(Path(__file__).read_bytes()),
                'romRead': False, 'webResearchPerformed': False, 'testsExecuted': False,
                'appChanged': False, 'allFactsVerified': False,
                'limits': ['Prose present is a structural finding, not individual factual validation.',
                           'Existing exact catalog-ID descriptions are retained as evidence, including own legacy-ID descriptions.',
                           'Local XML matches preserve full filename, platform and edition; candidates never automatically fill another ID.',
                           'Same prose in aliases/ports is recorded, not labelled fabricated solely because it repeats.',
                           'No legacy BR/base-title inheritance, fuzzy matching, ROM content reading or online approval.']}
    dump(args.output / 'coverage.json', coverage)
    dump(args.output / 'duplicate-text-groups.json', duplicate_groups)
    print(json.dumps({key: coverage[key] for key in ('catalogIds', 'visibleIds', 'qualityCounts', 'visibleQualityCounts',
                     'selectionOrigins', 'shortUnder180Ids', 'r39XmlFiles', 'r39MetadataRecords', 'r39SynopsisFields',
                     'localCandidateIds', 'duplicateTextGroups', 'outputSHA256')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
