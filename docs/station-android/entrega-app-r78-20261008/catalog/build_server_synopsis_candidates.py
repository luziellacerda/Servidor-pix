"""Prepare review-only compare-and-swap synopsis proposals; never contact/change the server.

The existing Java catalog contract accepts at most 2,000 UTF-16 code units.
Long native fallback descriptions are explicitly excluded from server proposals.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
OUTPUT = Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\server-proposals')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def utf16_units(text):
    return len(text.encode('utf-16-le')) // 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=OUTPUT)
    args = parser.parse_args()
    raw_audit = (HERE / 'existing-synopses-audit.json').read_bytes()
    raw_completed = (ROOT / 'data/synopses-complete.json').read_bytes()
    audit = json.loads(raw_audit)
    before = {row['itemId']: row for row in audit['records']}
    after = json.loads(raw_completed)
    proposals = []
    native_only = []
    unchanged = 0
    for row in after:
        original = before[row['itemId']]
        if any(row[key] != original[key] for key in ('platform', 'artifactSha256', 'name')):
            raise ValueError('Identity changed before preparing CAS proposal')
        if row['catalogItemRevision'] != original['itemRevision']:
            raise ValueError('Item revision changed')
        old = original['publishedServerDescription']
        if sha(old.encode('utf8')) != original['publishedServerDescriptionSha256']:
            raise ValueError('Published original description changed')
        text = row['description']
        if sha(text.encode('utf8')) != row['descriptionSha256']:
            raise ValueError('Completed description hash mismatch')
        if text == old:
            unchanged += 1
            continue
        count = utf16_units(text)
        if count > 2000:
            native_only.append({'itemId': row['itemId'], 'platform': row['platform'], 'name': row['name'],
                                'itemRevision': row['catalogItemRevision'], 'artifactSha256': row['artifactSha256'],
                                'previousDescriptionSha256': original['publishedServerDescriptionSha256'],
                                'nativeDescriptionSha256': row['descriptionSha256'], 'utf16CodeUnits': count,
                                'sourceKind': row['sourceKind'], 'reason': 'EXCEEDS_EXISTING_JAVA_CATALOG_DESCRIPTION_LIMIT_2000_UTF16',
                                'delivery': 'native-fallback-only', 'serverUpdateProposed': False})
            continue
        proposals.append({'precondition': {'itemId': row['itemId'], 'platform': row['platform'],
                                           'itemRevision': row['catalogItemRevision'],
                                           'artifactSha256': row['artifactSha256'], 'previousDescription': old,
                                           'previousDescriptionSha256': original['publishedServerDescriptionSha256']},
                          'proposedDescription': text, 'proposedDescriptionSha256': row['descriptionSha256'],
                          'utf16CodeUnits': count, 'name': row['name'], 'catalogVisible': row['catalogVisible'],
                          'sourceKind': row['sourceKind'], 'provenance': row['provenance'],
                          'applied': False, 'changesOnlineAuthorization': False})
    result = {'schemaVersion': 1, 'purpose': 'operator-review-only-not-an-existing-API-request',
              'sourceCatalogRevision': audit['catalogRevision'], 'descriptionLimitUtf16CodeUnits': 2000,
              'applyPolicy': 'Operator must require every precondition including literal previousDescription; mismatch rejects that item.',
              'changesJavaContract': False, 'applied': False, 'proposals': proposals}
    args.output.mkdir(parents=True, exist_ok=True)
    target = args.output / 'server-synopsis-candidates.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    (args.output / 'native-only-descriptions.json').write_text(json.dumps(native_only, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    receipt = {'utc': datetime.now(timezone.utc).isoformat(), 'catalogIds': len(after), 'unchangedIds': unchanged,
               'serverProposalIds': len(proposals), 'nativeOnlyIds': len(native_only),
               'auditSHA256': sha(raw_audit), 'completedDataSHA256': sha(raw_completed),
               'proposalsSHA256': sha(target.read_bytes()), 'recipeSHA256': sha(Path(__file__).read_bytes()),
               'serverChanged': False, 'javaChanged': False, 'testsExecuted': False,
               'limits': ['These are document proposals, not calls to an invented server route.',
                          'The operator must compare every identity and the full prior text before updating.',
                          'Texts over 2000 UTF-16 units remain native-only; the Java contract is preserved.',
                          'Description availability does not authorize players, profiles or game content.']}
    (args.output / 'server-synopsis-candidates-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf8')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
