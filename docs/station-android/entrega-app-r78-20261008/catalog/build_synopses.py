"""Generate exact-ID R78 synopsis data/header; no compilation, tests or network.

Preserves R37 descriptions and each legacy ID's own published prose. Only six
explicitly reviewed aliases share text through equality of the full published
artifact/launch descriptor. Optional editorial updates are bound to old hashes.
"""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone
import argparse
import hashlib
import json
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
INVENTORY = ROOT.parent / 'station-multiplayer-r77-20261008/catalog/catalog-inventory.json'
PAGING_REFERENCE = ROOT.parent / 'station-final-details-r37-20261005/frontend/station_synopsis_pages.hpp'
OUTPUT = Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\generated')
LABELS = {'snes': 'Super Nintendo', 'snesbr': 'Super Nintendo - BR', 'megadrive': 'MegaDrive',
          'megadrivebr': 'MegaDrive - BR', 'n64': 'Nintendo 64', 'neogeo': 'Neo Geo', 'neogeocd': 'Neo Geo CD'}
AUTHORIZED = {
    '233fcab3a55317536be7001984690ca2': '714f46fd69f37faaf2169f8e78428fb7',
    'eeecd9dd7b6acf96d7cabfe174676fb7': 'e307e854203a98bb3174a6e885786edc',
    '1e4d341cdfe848ce28f4649e1c2856e5': '04de9a95cd4715235e87ce10c94cef85',
    'ac79b8fc351cb0811a7e9b51a099ba9c': '826da6daebe9edbebffb3721f83abf12',
    'c7c7b5780bc10427af69cf042bc9ebdd': '6c1d370746d1def9a3364e6c760501b0',
    'de85a88485d206651fa0bfde244cfc82': '5092f2b508e4476924e033353f0d5292',
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def dump(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def synopsis_pages(text):
    """Byte-exact translation of the retained UTF-8 synopsisPages 800/1200 policy."""
    raw = text.encode('utf8')
    result = bytearray()
    start = 0
    while start < len(raw):
        end = min(start + 800, len(raw))
        if end < len(raw):
            while end > start and raw[end] & 0xc0 == 0x80:
                end -= 1
            word = max(raw.rfind(mark, 0, end + 1) for mark in (b' ', b'\n', b'\t'))
            if word != -1 and word > start + 400:
                end = word
            else:
                following = [index for mark in (b' ', b'\n', b'\t') if (index := raw.find(mark, end)) != -1]
                next_word = min(following) if following else len(raw)
                end = min(next_word, start + 1200, len(raw))
                while end < len(raw) and end > start and raw[end] & 0xc0 == 0x80:
                    end -= 1
        result.extend(raw[start:end])
        start = end
        while start < len(raw) and raw[start] in (32, 10, 9):
            start += 1
        if start < len(raw):
            result.append(12)
    return result.decode('utf8')


def literal(value):
    return json.dumps(value, ensure_ascii=False).replace('?', r'\?')


def header(records, overrides):
    lines = ['#pragma once', '// R78 generated exact-ID synopses: 2212 visible IDs and 255 compatibility IDs.',
             '// Existing prose is preserved; historical facts are not all individually verified.',
             '// See data/synopses-complete.json for source and exact descriptor alias bindings.',
             'static const GameInfo stationGameInfos[]={']
    for row in records:
        source = 'station-synopsis:r78:' + row['sourceKind'] + ':' + row['descriptionSha256']
        fields = [row['label'], row['itemId'], row['name'], row['description'], source]
        lines.append('{' + ','.join(literal(value) for value in fields) + ',1},')
    lines.extend(['};', 'static constexpr int NSTATIONGAMEINFOS=sizeof(stationGameInfos)/sizeof(stationGameInfos[0]);',
                  '// Only previously reviewed exact text is replaced. Fresh different server prose wins.',
                  '// Raw and retained synopsisPages UTF-8 forms are both explicit; no trimming/fuzzy comparison.',
                  'struct StationSynopsisReviewedOverride { const char* id; const char* raw; const char* paged; };',
                  'static const StationSynopsisReviewedOverride stationSynopsisReviewedOverrides[]={'])
    for row in overrides:
        lines.append('{' + ','.join(literal(row[field]) for field in ('itemId', 'previousRaw', 'previousPaged')) + '},')
    lines.extend(['};', 'static bool stationSynopsisNeedsOverride(const char*id,const char*serverDescription){',
                  ' if(!id||!serverDescription)return false;',
                  ' for(const auto& row:stationSynopsisReviewedOverrides){',
                  '  if(strcmp(id,row.id)==0&&(strcmp(serverDescription,row.raw)==0||strcmp(serverDescription,row.paged)==0))return true;',
                  ' }', ' return false;', '}'])
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--audit', type=Path, default=HERE / 'existing-synopses-audit.json')
    parser.add_argument('--editorial', type=Path, default=ROOT / 'data/editorial-synopses.json')
    parser.add_argument('--restorations', type=Path, default=ROOT / 'data/exact-prefix-restorations.json')
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    raw = args.audit.read_bytes()
    document = json.loads(raw)
    rows = document['records']
    original = {row['itemId']: row for row in rows}
    inventory_raw = INVENTORY.read_bytes()
    inventory = {row['itemId']: row for row in json.loads(inventory_raw)['records']}
    require(len(original) == len(rows) == 2467, 'Expected every exact catalog ID once')
    require(sum(row['catalogVisible'] for row in rows) == 2212, 'Visible catalog coverage changed')
    edits = json.loads(args.editorial.read_text('utf8')) if args.editorial.exists() else []
    require(isinstance(edits, list), 'Editorial file must contain an explicit record list')
    all_editorial = {row['itemId']: row for row in edits}
    require(len(all_editorial) == len(edits), 'Duplicate editorial ID')
    require(all(row.get('apply') in (True, False) for row in edits), 'Editorial needs explicit apply decision')
    editorial = {identity: row for identity, row in all_editorial.items() if row.get('apply') is True}
    pending_editorial = {identity: row for identity, row in all_editorial.items() if row.get('apply') is False}
    restores = json.loads(args.restorations.read_text('utf8')) if args.restorations.exists() else []
    restorations = {row['itemId']: row for row in restores}
    require(len(restorations) == len(restores), 'Duplicate restoration ID')
    require(not set(restorations) & set(editorial), 'Editorial/restoration overlap requires explicit review')
    unknown = set(all_editorial) - set(original)
    require(not unknown, 'Editorial references unknown IDs: ' + ','.join(sorted(unknown)))
    for identity, pending in pending_editorial.items():
        previous = original[identity]
        require(pending['platform'] == previous['platform'] and pending['name'] == previous['name']
                and pending['description'] == previous['selectedDescription']
                and pending['replacesDescriptionSha256'] == previous['selectedDescriptionSha256'],
                'Pending editorial must preserve exact original prose')
    records = []
    bindings = []
    used_editorial = []
    used_restorations = []
    overrides = [{'itemId': 'ab9773189dbfc1571ca0d56adb13ff32', 'previousRaw': 'Old Towers',
                  'previousPaged': 'Old Towers', 'reason': 'preserved-r37-title-only-override'}]
    for item in sorted(rows, key=lambda r: r['itemId']):
        identity = item['itemId']
        description = item['selectedDescription']
        require(sha(description.encode('utf8')) == item['selectedDescriptionSha256'], 'Audit description changed')
        source_kind = item['selectionOrigin'].replace('/', '-')
        provenance = {'auditSource': args.audit.name, 'selectionOrigin': item['selectionOrigin'],
                      'catalogSource': item['catalogSource'], 'existingProvenance': item['existingProvenance']}
        if identity in AUTHORIZED:
            require(not description.strip(), 'Alias already has prose: review rather than overwrite')
            candidates = item['exactDescriptorAliasCandidates']
            require(len(candidates) == 1 and candidates[0]['sourceItemId'] == AUTHORIZED[identity],
                    'Authorized exact descriptor alias no longer matches')
            binding = candidates[0]
            base = original[binding['sourceItemId']]
            require(all(inventory[identity][key] == inventory[binding['sourceItemId']][key] == value
                        for key, value in binding['descriptor'].items()), 'Published alias descriptor changed')
            require(base['catalogVisible'] and base['quality'] == 'prose-present-not-fact-checked', 'Missing source prose')
            require(base['platform'] == item['platform'] and binding['descriptionSha256'] == base['selectedDescriptionSha256'],
                    'Alias platform/text differs from reviewed evidence')
            description = base['selectedDescription']
            source_kind = 'exact-artifact-descriptor-alias'
            provenance['binding'] = dict(binding, promotedForDescriptionOnly=True,
                                         originalAliasDescriptionWasEmpty=True,
                                         implementationDecision='reviewed-exact-descriptor-binding-for-requested-synopsis-correction',
                                         onlineAuthorizationChanged=False)
            bindings.append({'targetItemId': identity, 'targetName': item['name'], **provenance['binding']})
        if identity in restorations:
            restoration = restorations[identity]
            require(all(restoration[key] == item[key] for key in ('platform', 'name', 'itemRevision',
                        'artifactSha256', 'artifactFileName', 'artifactLaunchPath')), 'Restoration identity changed')
            require(restoration['replacesDescriptionSha256'] == sha(description.encode('utf8'))
                    and len(description) == restoration['oldCharacters'] == 2000, 'Restoration old prefix changed')
            full_text = restoration['description']
            require(full_text.startswith(description) and len(full_text) > len(description)
                    and sha(full_text.encode('utf8')) == restoration['descriptionSha256'], 'Restoration is not exact continuation')
            overrides.append({'itemId': identity, 'previousRaw': description, 'previousPaged': synopsis_pages(description),
                              'reason': 'exact-xml-continuation-of-whole-published-prefix'})
            description = full_text
            source_kind = 'exact-xml-prefix-restoration'
            provenance['restoration'] = {key: value for key, value in restoration.items() if key != 'description'}
            used_restorations.append(identity)
        if identity in editorial:
            edit = editorial[identity]
            require(edit['platform'] == item['platform'] and edit['name'] == item['name'], 'Editorial title/edition mismatch')
            require(edit.get('replacesDescriptionSha256') == sha(description.encode('utf8')), 'Editorial old text hash mismatch')
            require(edit.get('replaceDescriptionExact') == description, 'Editorial previous text does not match literally')
            require(edit.get('language') == 'pt-BR' and edit.get('method') == 'original-editorial-summary', 'Editorial language/method missing')
            require(edit.get('sources') and all(source.get('url', '').startswith('https://') for source in edit['sources']),
                    'Editorial primary sources missing')
            overrides.append({'itemId': identity, 'previousRaw': description, 'previousPaged': synopsis_pages(description),
                              'reason': 'reviewed-original-editorial-replaces-only-exact-old-prose'})
            description = edit['description']
            source_kind = 'r78-original-editorial'
            provenance['editorial'] = {key: value for key, value in edit.items() if key != 'description'}
            used_editorial.append(identity)
        require(len(description.strip()) >= 30, 'No substantive text for ' + identity)
        require('\ufffd' not in description and not any(ord(c) < 32 and c not in '\r\n\t' for c in description),
                'Invalid description characters for ' + identity)
        require(not re.search(r'(?i)sinopse ainda|sinopse n.o dispon.vel|no description available|lorem ipsum', description),
                'Placeholder cannot be promoted: ' + identity)
        records.append({'itemId': identity, 'platform': item['platform'], 'label': LABELS[item['platform']],
                        'name': item['name'], 'catalogVisible': item['catalogVisible'], 'catalogRevision': document['catalogRevision'],
                        'catalogItemRevision': item['itemRevision'], 'artifactSha256': item['artifactSha256'],
                        'artifactFileName': item['artifactFileName'], 'artifactLaunchPath': item['artifactLaunchPath'],
                        'contentSha256': item['contentSha256'], 'description': description,
                        'descriptionSha256': sha(description.encode('utf8')), 'sourceKind': source_kind,
                        'provenance': provenance, 'historicalFactsIndividuallyVerified': False,
                        'variantSpecificBehaviorInvented': False, 'onlineAuthorizationChanged': False})
    require(len(bindings) == len(AUTHORIZED), 'Unused explicit alias binding')
    require(len(used_restorations) == len(restorations), 'Unknown restoration ID')
    args.output.mkdir(parents=True, exist_ok=True)
    dump(args.output / 'data/synopses-complete.json', records)
    dump(args.output / 'data/exact-alias-bindings.json', bindings)
    dump(args.output / 'data/exact-server-overrides.json', overrides)
    destination = args.output / 'native/station_game_infos.h'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(header(records, overrides), encoding='utf8')
    receipt = {'utc': datetime.now(timezone.utc).isoformat(), 'catalogRevision': document['catalogRevision'],
               'ids': len(records), 'visibleIds': sum(row['catalogVisible'] for row in records),
               'compatibilityIds': sum(not row['catalogVisible'] for row in records),
               'missing': 0, 'descriptionOnlyAliasBindings': len(bindings),
               'existingDescriptionsPreserved': len(records) - len(bindings) - len(used_editorial) - len(used_restorations),
               'editorialUpdates': len(used_editorial), 'editorialItemIds': used_editorial,
               'editorialPending': len(pending_editorial), 'editorialPendingItemIds': sorted(pending_editorial),
               'exactPrefixRestorations': len(used_restorations), 'restoredItemIds': used_restorations,
               'exactServerOverrideIds': len(overrides),
               'sourceCounts': dict(Counter(row['sourceKind'] for row in records)),
               'auditSHA256': sha(raw), 'recipeSHA256': sha(Path(__file__).read_bytes()),
               'inventorySHA256': sha(inventory_raw),
               'editorialSHA256': sha(args.editorial.read_bytes()) if args.editorial.exists() else None,
               'restorationsSHA256': sha(args.restorations.read_bytes()) if args.restorations.exists() else None,
               'pagingReferenceSHA256': sha(PAGING_REFERENCE.read_bytes()),
               'dataSHA256': sha((args.output / 'data/synopses-complete.json').read_bytes()),
               'headerSHA256': sha(destination.read_bytes()), 'testsExecuted': False,
               'compiled': False, 'installed': False, 'serverChanged': False, 'allHistoricalFactsVerified': False,
               'limits': ['Every frozen catalog ID has prose; this is not proof all facts/variants have been individually checked.',
                          'Only the six explicit aliases share descriptions by identical published artifact/launch descriptors.',
                          'Existing translation/hack descriptions remain existing evidence; no patch-specific behavior is invented.',
                          'The package/profile/player authorization system is unchanged.']}
    dump(args.output / 'evidence/synopses-generation.json', receipt)
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == '__main__':
    main()
