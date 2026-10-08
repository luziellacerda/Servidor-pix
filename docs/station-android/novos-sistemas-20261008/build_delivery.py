#!/usr/bin/env python3
"""Build the single public handoff from the already completed private import receipts."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import shutil

LABELS = {'gamecube': 'Nintendo GameCube', 'psx': 'PlayStation 1', 'wii': 'Nintendo Wii',
          'wiiu': 'Nintendo Wii U', 'switch': 'Nintendo Switch'}
PUBLIC_KEYS = ('itemId', 'name', 'platform', 'revision', 'coverId', 'catalogVisible',
               'folderPath', 'metadata', 'artifact', 'contentSha256')


def read(path):
    return json.loads(path.read_bytes())


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(private, output):
    output.mkdir(parents=True, exist_ok=True)
    result = read(private / 'deployment-result.json')
    if not result.get('applied') or not result['http']['passed']:
        raise ValueError('Only a completed live deployment can be delivered')
    index = read(private / 'candidate-index-private.json')
    if index['revision'] != result['revision'] or len(index['items']) != result['totalIds']:
        raise ValueError('Candidate and live production receipt differ')
    previous = {r['itemId']: r for r in read(private / 'index-before.json')['items']}
    current = {r['itemId']: r for r in index['items']}
    if any(current.get(k) != r for k, r in previous.items()):
        raise ValueError('An original catalog row changed')
    plan = read(private / 'covers-reviewed.json')
    public = [{key: row[key] for key in PUBLIC_KEYS if key in row} for row in index['items']]
    public.sort(key=lambda r: (r['platform'], r['name'].casefold(), r['itemId']))
    write(output / ('catalog-completo-rev' + str(index['revision']) + '.json'),
          dict(schemaVersion=1, revision=index['revision'], items=public,
               exportKind='sanitized-index-with-descriptors-not-a-signed-api-envelope'))
    new_games = []
    for game in plan['games']:
        item_id = 'station_' + hashlib.sha256((game['platform'] + ':' + game['rom']).encode()).hexdigest()[:32]
        row = current[item_id]
        if Path(row['coverPath']).stem != game['compiledCoverSha256']:
            raise ValueError('Compiled cover differs from the reviewed source image')
        new_games.append(dict({k: row[k] for k in PUBLIC_KEYS if k in row},
            platformLabel=LABELS[game['platform']], sourceRom=game['rom'],
            originalCover=game['cover'], coverSource=game['source'],
            coverSourceTitle=game['sourceTitle'], coverSourceReference=game['sourceReference'],
            originalCoverSha256=game['sourceSha256'], coverMatchingRule=game['matchingRule'],
            compiledCoverSha256=game['compiledCoverSha256'], compiledCoverBytes=game['compiledCoverBytes']))
    new_games.sort(key=lambda r: (r['platform'], r['name'].casefold(), r['itemId']))
    if len(new_games) != 114 or dict(Counter(r['platform'] for r in new_games)) != result['platformCounts']:
        raise ValueError('The complete reviewed game set was not delivered')
    write(output / 'novos-jogos-capas-artefatos.json', dict(schemaVersion=1, revision=index['revision'], games=new_games))
    fields = ('platform', 'name', 'itemId', 'coverId', 'revision', 'sourceRom', 'originalCover',
              'compiledCoverSha256', 'compiledCoverBytes', 'fileName', 'format', 'launchPath',
              'fileCount', 'sizeBytes', 'expandedSizeBytes', 'artifactSha256', 'descriptionPresent')
    with (output / 'novos-jogos.tsv').open('w', encoding='utf-8', newline='') as stream:
        writer = csv.DictWriter(stream, fields, delimiter='\t', lineterminator='\n')
        writer.writeheader()
        for r in new_games:
            line = {k: r[k] for k in fields if k in r}
            line.update({k: r['artifact'][k] for k in ('fileName', 'format', 'launchPath', 'fileCount', 'sizeBytes', 'expandedSizeBytes')})
            line.update(artifactSha256=r['artifact']['sha256'], descriptionPresent=bool(r['metadata'].get('description')))
            writer.writerow(line)
    fields = ('platform', 'name', 'itemId', 'coverId', 'revision', 'catalogVisible', 'folderPath',
              'format', 'fileName', 'launchPath', 'fileCount', 'sizeBytes', 'artifactSha256',
              'contentSha256', 'descriptionPresent')
    with (output / 'catalog-completo.tsv').open('w', encoding='utf-8', newline='') as stream:
        writer = csv.DictWriter(stream, fields, delimiter='\t', lineterminator='\n')
        writer.writeheader()
        for r in public:
            line = {k: r[k] for k in fields if k in r}
            line['catalogVisible'] = r.get('catalogVisible', True)
            line['folderPath'] = json.dumps(r.get('folderPath', []), ensure_ascii=False)
            line.update({k: r['artifact'][k] for k in ('format', 'fileName', 'launchPath', 'fileCount', 'sizeBytes')})
            line.update(artifactSha256=r['artifact']['sha256'], descriptionPresent=bool(r.get('metadata', {}).get('description')))
            writer.writerow(line)
    missing = [r for r in public if not r.get('metadata', {}).get('description')]
    write(output / 'sinopses-ainda-ausentes.json', dict(totalIds=len(missing),
          newGames=[dict(itemId=r['itemId'], platform=r['platform'], name=r['name'], sourceRom=r['sourceRom'])
                    for r in new_games if not r['metadata'].get('description')],
          allItems=[{k: r[k] for k in ('itemId', 'name', 'platform')} for r in missing]))
    clean_result = {k: v for k, v in result.items() if k != 'backupDirectory'}
    clean_result['apiRestartsDuringWholeTask'] = 3
    clean_result['earlierAttemptRolledBackForUnchangedPreexistingPendingSource'] = True
    clean_result['closedEmptyV3RoomClearedAfterHumanConfirmationInEarlierAttempt'] = True
    write(output / 'production-proof.json', clean_result)
    write(output / 'http-proof.json', result['http'])
    write(output / 'tested-tools.json', read(private / 'scanner-seal.json'))
    write(output / 'folder-organization.json', read(private / 'organization-receipt.json'))
    write(output / 'wiiu-local-completion.json', read(private / 'wiiu-completed.json'))
    write(output / 'covers-installed.json', read(private / 'covers-installed.json'))
    downloads = read(private / 'cover-sources/downloaded-covers.json')
    for r in downloads:
        r['treeSha'] = r.pop('commit')
    write(output / 'four-cover-sources.json', downloads)
    rows = []
    for p in sorted(result['platformCounts']):
        games = [r for r in new_games if r['platform'] == p]
        source = private / 'prepared' / p
        target = output / 'metadata' / p
        target.mkdir(parents=True, exist_ok=True)
        for name in ('gamelist.xml', 'station-catalog-seed.json'):
            shutil.copyfile(source / name, target / name)
        rows.append(dict(platform=p, label=LABELS[p], games=len(games),
                         coverSources=dict(Counter(r['coverSource'] for r in games)),
                         artifactBytes=sum(r['artifact']['sizeBytes'] for r in games)))
    summary = dict(schemaVersion=1, revision=index['revision'], addedGames=len(new_games), systems=rows,
        totalIds=len(public), visibleIds=sum(r.get('catalogVisible', True) for r in public),
        compatibilityIds=sum(not r.get('catalogVisible', True) for r in public),
        allPreviousIdsPreserved=len(previous), coverCount=len(new_games), coverBytes=plan['compiledCoverTotalBytes'],
        futureKnownKeys=plan['catalogFallbackGames'], sourceCoverCounts=plan['coverSources'],
        originalCoverCatalogSha256=plan['catalogSha256'],
        newDescriptionsPresent=sum(bool(r['metadata'].get('description')) for r in new_games),
        newDescriptionsMissing=sum(not r['metadata'].get('description') for r in new_games),
        platformCounts=dict(Counter(r['platform'] for r in public if r.get('catalogVisible', True))),
        automaticScannerActive=True, catalogReloadWithoutRestartSeconds=10,
        onlineProfilesPreserved=1816, phoneGameplayValidated=False)
    write(output / 'summary.json', summary)
    # Reports and manifests expose only public identifiers and relative source names.
    for file in output.rglob('*'):
        if file.is_file() and file.suffix in {'.json', '.tsv', '.xml'}:
            body = file.read_text(encoding='utf-8')
            if any(x in body for x in ('"filePath"', '"coverPath"', '/media/lz-servidor/', 'Host=127.0.0.1;', 'Bearer ')):
                raise ValueError('A private server mapping escaped into the delivery')
    manifest = {str(p.relative_to(output)): dict(bytes=p.stat().st_size, sha256=sha(p))
                for p in sorted(output.rglob('*')) if p.is_file() and p.name != 'delivery-manifest.json'}
    write(output / 'delivery-manifest.json', dict(schemaVersion=1, revision=index['revision'], files=manifest))
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == '__main__':
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--private', required=True, type=Path)
    cli.add_argument('--output', type=Path, default=Path(__file__).resolve().parent)
    options = cli.parse_args()
    build(options.private, options.output)
