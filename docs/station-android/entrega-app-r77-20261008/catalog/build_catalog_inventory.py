"""Export every published game ID, with fail-closed multiplayer research status.

This is not a production capability/profile registry and enables no games.
No ROM, secret, synopsis or personal data is copied into the output.
"""
from pathlib import Path
import argparse,collections,csv,hashlib,io,json,re,subprocess
from datetime import datetime,timezone

HERE=Path(__file__).resolve().parent
SERVER=Path(r'E:\ESTUDO APK\work\server-auth-handoff\Servidor-pix-implementar-faltas-20261002')
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog')
REV='bee3dcd5c2c0c0a228805957fe89b9a6023e8402'
FOLDER='docs/station-android/biblioteca-neogeocd-20261005/'
FILES=['catalogo-completo.tsv','compatibilidade-ids.tsv','resumo-catalogo.json']
def sha(data):return hashlib.sha256(data).hexdigest()
def git(*args):return subprocess.check_output(['git','-c','safe.directory='+SERVER.as_posix(),'-C',str(SERVER),*args])
def dump(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,default=WORK);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
    blobs={name:git('show',REV+':'+FOLDER+name) for name in FILES}
    summary=json.loads(blobs['resumo-catalogo.json']);curation=json.loads((HERE/'mode-evidence.json').read_text(encoding='utf-8'))
    evidence={v['itemId']:(title,v) for title in curation['titles'] for v in title['variants']}
    rows=[]
    for file in FILES[:2]:
        for row in csv.DictReader(io.StringIO(blobs[file].decode('utf-8-sig')),delimiter='\t'):
            row['_source']=file;rows.append(row)
    assert len(rows)==summary['internal'] and len({r['itemId'] for r in rows})==len(rows)
    assert sum(r['catalogVisible']=='yes' for r in rows)==summary['visible']
    records=[];seen_evidence=set()
    for r in sorted(rows,key=lambda r:(r['platform'],r['itemId'])):
        assert re.fullmatch('[0-9a-f]{64}',r['artifactSha256']),r['itemId']
        visible=r['catalogVisible']=='yes';raw=r['artifactFormat']=='raw' and r['artifactFileCount']=='1'
        title,variant=evidence.get(r['itemId'],(None,None));modes=[];source_ids=[]
        blockers=['SIGNED_PROFILE_AUTHORIZATION_MISSING']
        if not raw:blockers.append('CONTENT_SHA256_MISSING')
        if r['platform'] not in ('snes','snesbr','megadrive','megadrivebr'):blockers.append('ONLINE_ENGINE_NOT_QUALIFIED')
        if not visible:blockers.append('LEGACY_ID_REQUIRES_EXPLICIT_BINDING')
        if title:
            assert variant['artifactSha256']==r['artifactSha256'],'curation hash mismatch for '+r['itemId']
            seen_evidence.add(r['itemId']);modes=title['modes'];source_ids=title['sourceIds'];blockers.append('EXACT_VARIANT_AND_MODE_GAMEPLAY_NOT_QUALIFIED')
            if not modes:blockers.append('GAME_MODE_OR_ADAPTER_EVIDENCE_INCOMPLETE')
        else:blockers.append('GAME_MODE_EVIDENCE_MISSING')
        rec={'itemId':r['itemId'],'platform':r['platform'],'name':r['name'],'catalogVisible':visible,
          'catalogRevision':summary['revision'],'itemRevision':int(r['itemRevision']),
          'folderPath':json.loads(r['folderPath']),'artifactFileName':r['artifactFileName'],
          'artifactFormat':r['artifactFormat'],'artifactSha256':r['artifactSha256'],
          'artifactSizeBytes':int(r['artifactSizeBytes']),'artifactLaunchPath':r['artifactLaunchPath'],
          'artifactExpandedSizeBytes':int(r['artifactExpandedSizeBytes']),
          'artifactFileCount':int(r['artifactFileCount']),
          'contentSha256':r['artifactSha256'] if raw else None,
          'contentHashEvidence':'published-raw-artifact-no-normalization' if raw else 'not-published-archive-hash-is-not-content-hash',
          'playersDescriptionHint':r['players'] or None,'playersHintIsAuthorization':False,
          'titleEvidenceId':title['id'] if title else None,'variantReview':variant['variantStatus'] if variant else 'unreviewed',
          'candidateModes':modes,'sourceIds':source_ids,
          'onlineDecision':'blocked','authorizedHumanCounts':[],'blockers':blockers}
        records.append(rec)
    assert seen_evidence==set(evidence),'curated item missing from pinned catalog'
    document={'schemaVersion':1,'purpose':'complete-inventory-and-research-not-authorization','sourceCommit':REV,'catalogRevision':summary['revision'],
      'defaultPolicy':'deny-unreviewed-or-mismatched-item-content-mode-profile','records':records}
    dump(a.output/'catalog-inventory.json',document)
    with (a.output/'catalog-review-queue.tsv').open('w',encoding='utf-8',newline='') as f:
        fields=['itemId','platform','name','catalogVisible','artifactSha256','contentSha256','playersDescriptionHint','titleEvidenceId','variantReview','onlineDecision','blockers']
        writer=csv.DictWriter(f,fieldnames=fields,delimiter='\t');writer.writeheader()
        for r in records:
            out={k:r[k] for k in fields};out['blockers']=';'.join(out['blockers']);writer.writerow(out)
    stats={'utc':datetime.now(timezone.utc).isoformat(),'sourceCommit':REV,'catalogRevision':summary['revision'],
      'latestPublishedCatalogChange':git('log','-1','--format=%H %aI',REV,'--',FOLDER+'catalogo-completo.tsv').decode().strip(),
      'inventoryRows':len(records),'visibleRows':sum(r['catalogVisible'] for r in records),'compatibilityIds':sum(not r['catalogVisible'] for r in records),
      'platformVisibleCounts':dict(collections.Counter(r['platform'] for r in records if r['catalogVisible'])),
      'publishedRawContentHashes':sum(r['contentSha256'] is not None for r in records),'missingContentHashes':sum(r['contentSha256'] is None for r in records),
      'visiblePublishedRawContentHashes':sum(r['contentSha256'] is not None for r in records if r['catalogVisible']),
      'visibleMissingContentHashes':sum(r['contentSha256'] is None for r in records if r['catalogVisible']),
      'researchItems':len(seen_evidence),'itemsWithDocumentedOriginalModes':sum(bool(r['candidateModes']) for r in records),
      'unreviewedIds':sum(r['variantReview']=='unreviewed' for r in records),'authorizedIds':0,
      'blockerCounts':dict(collections.Counter(b for r in records for b in r['blockers'])),
      'sourceFiles':{FOLDER+n:sha(b) for n,b in blobs.items()},'recipeSHA256':sha(Path(__file__).read_bytes()),
      'modeEvidenceSHA256':sha((HERE/'mode-evidence.json').read_bytes()),
      'inventorySHA256':sha((a.output/'catalog-inventory.json').read_bytes()),
      'liveCatalogVerified':False,'allTitlesResearched':False,'productionModified':False,
      'limitations':['Inventory covers every ID in the latest complete export found in existing remote refs, not a fresh authenticated live catalog.',
        'Raw hashes are published transfer bytes; no current disk or normalized core payload was read.',
        'Descriptive player counts are research hints and never authorize online rooms.',
        'All candidates remain blocked until exact content, modes, profile, core and transport are qualified and authorized.']}
    dump(a.output/'catalog-coverage.json',stats);print(json.dumps(stats,ensure_ascii=False))

if __name__=='__main__':main()
