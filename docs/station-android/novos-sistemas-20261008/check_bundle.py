#!/usr/bin/env python3
"""Check the delivered inventory and byte-preserving transfer manifest."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parent


def read(name):
    return json.loads((ROOT / name).read_bytes())


def main():
    manifest = read('delivery-manifest.json')
    checked = 0
    for name, proof in manifest['files'].items():
        file = ROOT / name
        if file.is_symlink() or not file.resolve().is_relative_to(ROOT) or not file.is_file():
            raise ValueError('Unsafe or absent delivery file: ' + name)
        body = file.read_bytes()
        if len(body) != proof['bytes'] or hashlib.sha256(body).hexdigest() != proof['sha256']:
            raise ValueError('Delivery checksum differs: ' + name)
        checked += 1
    summary = read('summary.json')
    catalog = read('catalog-completo-rev' + str(summary['revision']) + '.json')
    new = read('novos-jogos-capas-artefatos.json')
    all_rows = {r['itemId']: r for r in catalog['items']}
    if len(all_rows) != len(catalog['items']) or len(all_rows) != 3848:
        raise ValueError('Catalog IDs differ')
    if catalog['revision'] != summary['revision'] or new['revision'] != summary['revision']:
        raise ValueError('Catalog revisions differ')
    if sum(r.get('catalogVisible', True) for r in all_rows.values()) != 3593:
        raise ValueError('Visible/compatibility counts differ')
    counts = Counter(r['platform'] for r in new['games'])
    if dict(counts) != {'gamecube': 21, 'psx': 84, 'wii': 7, 'wiiu': 1, 'switch': 1}:
        raise ValueError('The complete new platform set is absent')
    if len({r['itemId'] for r in new['games']}) != 114:
        raise ValueError('New game identities differ')
    for row in new['games']:
        for key in ('name', 'platform', 'revision', 'coverId', 'artifact', 'metadata'):
            if row[key] != all_rows[row['itemId']][key]:
                raise ValueError('A new game differs from the full catalog')
        if not re.fullmatch('[a-f0-9]{64}', row['compiledCoverSha256']):
            raise ValueError('Invalid compiled cover identity')
        artifact = row['artifact']
        launch = artifact['launchPath']
        if launch.startswith('/') or any(p in {'', '.', '..'} for p in launch.split('/')):
            raise ValueError('Invalid launch path')
        if row['platform'] == 'wiiu' and (launch != 'code/Turbo.rpx' or artifact['fileCount'] != 3050):
            raise ValueError('Complete Wii U game structure differs')
    http = read('http-proof.json')
    if not http['passed'] or http['coversVerified'] != 114 or http['coverWorkers'] != 4 or not http['ownedSyntheticFixtureRemoved']:
        raise ValueError('Completed public verification is absent')
    if len(http['downloadStreams']) != 5 or not all(g['streamPrefixMatched'] and g['oneUseVerified'] for g in http['downloadStreams']):
        raise ValueError('Public download proofs differ')
    tools = read('tested-tools.json')
    if sum(tools['tests'].values()) != 30:
        raise ValueError('Test evidence differs')
    print(json.dumps(dict(passed=True, deliveryFilesVerified=checked, revision=summary['revision'],
        catalogIds=len(all_rows), visibleIds=3593, newGames=114, matchedGameCovers=114,
        fiveSystemsPresent=True, completeWiiuStructure=True), ensure_ascii=False))


if __name__ == '__main__':
    main()
