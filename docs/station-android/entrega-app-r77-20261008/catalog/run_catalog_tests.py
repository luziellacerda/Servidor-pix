"""Verify complete coverage/default denial and exact payload binding with synthetic data."""
from pathlib import Path
import copy,hashlib,json,sys,tempfile,zipfile,warnings,zlib
from datetime import datetime,timezone
from bind_content_hashes import bind,digest_file,safe_member

HERE=Path(__file__).resolve().parent
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog')
checks=0
def check(condition,message):
    global checks;checks+=1
    if not condition:raise AssertionError(message)
def reject(fn):
    try:fn()
    except (AssertionError,ValueError,zipfile.BadZipFile):check(True,'rejected');return
    check(False,'unexpected acceptance')

def main():
    inv=json.loads((WORK/'catalog-inventory.json').read_text(encoding='utf-8'));coverage=json.loads((WORK/'catalog-coverage.json').read_text(encoding='utf-8'))
    records=inv['records'];check(len(records)==2467,'complete inventory');check(len({r['itemId'] for r in records})==2467,'unique IDs')
    check(sum(r['catalogVisible'] for r in records)==2212,'visible coverage');check(coverage['authorizedIds']==0,'research cannot authorize')
    for r in records:
        check(r['onlineDecision']=='blocked' and r['authorizedHumanCounts']==[],'no inferred authorization')
        check(r['playersHintIsAuthorization'] is False,'metadata is never authority')
        check('SIGNED_PROFILE_AUTHORIZATION_MISSING' in r['blockers'],'explicit authorization dependency')
        check(bool(r['contentSha256'])==(r['artifactFormat']=='raw' and r['artifactFileCount']==1),'archive digest not reused as content digest')
        check(bool(r['candidateModes']) or ('GAME_MODE_EVIDENCE_MISSING' in r['blockers'] or 'GAME_MODE_OR_ADAPTER_EVIDENCE_INCOMPLETE' in r['blockers']),'pending mode accounted')
    with tempfile.TemporaryDirectory(prefix='synthetic-content-',dir=WORK) as tmp:
        root=Path(tmp);raw=root/'sample.raw';raw.write_bytes(b'synthetic input only\x00\x01')
        record={'itemId':'fixture','platform':'snes','artifactSizeBytes':raw.stat().st_size,'artifactSha256':digest_file(raw),'artifactFormat':'raw','artifactFileCount':1,'artifactExpandedSizeBytes':raw.stat().st_size,'artifactLaunchPath':'sample.raw'}
        result=bind(record,raw);check(result['contentSha256']==digest_file(raw),'raw hash');check(result['authorizesOnline'] is False,'binding not authority');check(all(str(raw) not in str(v) and str(root) not in str(v) for v in result.values()),'no local paths')
        check(result['databaseChecksums']=={'sha1':hashlib.sha1(raw.read_bytes()).hexdigest(),'md5':hashlib.md5(raw.read_bytes()).hexdigest(),'crc32':f'{zlib.crc32(raw.read_bytes()):08x}'},'database identity checksums use exact payload')
        wrong=copy.deepcopy(record);wrong['artifactSha256']='0'*64;reject(lambda:bind(wrong,raw))
        wrong=copy.deepcopy(record);wrong['artifactSizeBytes']+=1;reject(lambda:bind(wrong,raw))
        wrong=copy.deepcopy(record);wrong['artifactFileCount']=2;reject(lambda:bind(wrong,raw))
        z=root/'sample.zip'
        with zipfile.ZipFile(z,'w',compression=zipfile.ZIP_DEFLATED) as f:f.writestr('dir/game.sfc',raw.read_bytes())
        zr=dict(record,artifactFormat='zip',artifactSizeBytes=z.stat().st_size,artifactSha256=digest_file(z),artifactLaunchPath='dir/game.sfc')
        result=bind(zr,z);check(result['contentSha256']==digest_file(raw),'zip payload hash');check(result['contentSha256']!=zr['artifactSha256'],'archive/content separated')
        check(result['databaseChecksums']['sha1']==hashlib.sha1(raw.read_bytes()).hexdigest(),'zip database SHA-1 is decompressed payload')
        wrong=dict(zr,artifactLaunchPath='dir/other.sfc');reject(lambda:bind(wrong,z))
        wrong=dict(zr,artifactExpandedSizeBytes=1);reject(lambda:bind(wrong,z))
        wrong=dict(zr,artifactFileCount=2);reject(lambda:bind(wrong,z))
        for bad in ['../game.sfc','/game.sfc','C:/game.sfc','dir\\game.sfc']:
            check(not safe_member(bad),'unsafe path');wrong=dict(zr,artifactLaunchPath=bad);reject(lambda:bind(wrong,z))
        with warnings.catch_warnings():
            warnings.simplefilter('ignore')
            with zipfile.ZipFile(z,'w') as f:f.writestr('dir/game.sfc',b'A');f.writestr('dir/game.sfc',b'B')
        wrong=dict(zr,artifactSha256=digest_file(z),artifactSizeBytes=z.stat().st_size,artifactFileCount=2,artifactExpandedSizeBytes=2);reject(lambda:bind(wrong,z))
    receipt={'utc':datetime.now(timezone.utc).isoformat(),'checks':checks,'inventoryRows':len(records),'syntheticPayloadsOnly':True,'romUsed':False,'androidExecuted':False,'networkExecuted':False,'inventorySHA256':digest_file(WORK/'catalog-inventory.json'),'recipeSHA256':digest_file(Path(__file__)),'bindingRecipeSHA256':digest_file(HERE/'bind_content_hashes.py')}
    (WORK/'catalog-tests.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8');print(json.dumps(receipt))

if __name__=='__main__':main()
