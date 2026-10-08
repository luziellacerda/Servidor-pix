"""Recover complete existing XML prose only when exact-file text extends the whole published prefix."""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROM_ROOT = Path(r'G:\TURBORAMA\RetroBat\roms')
OUTPUT = Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\prefix-restorations')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    audit_raw = (HERE / 'existing-synopses-audit.json').read_bytes()
    audit = json.loads(audit_raw)
    coverage = json.loads((HERE / 'coverage.json').read_text('utf8'))
    sources = {row['sourceId']: row for row in coverage['sources']}
    xml = {}
    restorations = []
    unresolved = []
    for row in audit['records']:
        old = row['selectedDescription']
        if len(old) != 2000:
            continue
        options = {}
        for candidate in row['localCandidates']:
            source_id = candidate['sourceId']
            if source_id not in xml:
                path = (ROM_ROOT / source_id.removeprefix('retrobat/')).resolve()
                if not path.is_relative_to(ROM_ROOT.resolve()):
                    raise ValueError('XML path escapes known root')
                raw = path.read_bytes()
                if sha(raw) != sources[source_id]['sha256'] or b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
                    raise ValueError('Audited XML changed')
                xml[source_id] = ET.fromstring(raw).findall('game')
            game = xml[source_id][candidate['ordinal'] - 1]
            text = game.findtext('desc', '')
            if sha(text.encode('utf8')) != candidate['descriptionSha256']:
                raise ValueError('Exact candidate prose changed')
            if len(text) > len(old) and text.startswith(old):
                proof = dict(candidate, sourceFileSHA256=sources[source_id]['sha256'])
                options.setdefault(text, []).append(proof)
        if len(options) != 1:
            unresolved.append({'itemId': row['itemId'], 'name': row['name'], 'choices': len(options)})
            continue
        text, proofs = next(iter(options.items()))
        restorations.append({'itemId': row['itemId'], 'platform': row['platform'], 'name': row['name'],
                             'itemRevision': row['itemRevision'], 'artifactSha256': row['artifactSha256'],
                             'artifactFileName': row['artifactFileName'], 'artifactLaunchPath': row['artifactLaunchPath'],
                             'replacesDescriptionSha256': sha(old.encode('utf8')), 'oldCharacters': len(old),
                             'description': text, 'descriptionSha256': sha(text.encode('utf8')),
                             'newCharacters': len(text), 'method': 'exact-full-file-identity-and-integral-published-prefix',
                             'sources': proofs, 'onlineAuthorizationChanged': False,
                             'historicalFactsIndividuallyVerified': False})
    if len(restorations) != 17 or unresolved:
        raise ValueError('The reviewed set of 17 exact restorations changed; review before extending')
    args.output.mkdir(parents=True, exist_ok=True)
    output = args.output / 'exact-prefix-restorations.json'
    output.write_text(json.dumps(restorations, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    report = {'utc': datetime.now(timezone.utc).isoformat(), 'restoredIds': len(restorations),
              'oldCharactersEach': 2000, 'maximumNewCharacters': max(row['newCharacters'] for row in restorations),
              'unresolved': unresolved, 'auditSHA256': sha(audit_raw), 'outputSHA256': sha(output.read_bytes()),
              'recipeSHA256': sha(Path(__file__).read_bytes()), 'romRead': False,
              'webUsed': False, 'testsExecuted': False, 'compiled': False}
    (args.output / 'prefix-restorations.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
