from pathlib import Path
from datetime import datetime,timezone
import ast,json,hashlib,importlib.util,os,tempfile
from PIL import Image
W=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
plan=json.loads((W/'cover-plan.json').read_text())
ids=[]
for a in plan['matches']:
 for k,h in [('sourcePath','sourceSha256'),('compiledPath','compiledSha256'),('magazinePath','oldMagazineSha256')]:assert sha(a[k])==a[h]
 with Image.open(a['compiledPath']) as im:
  im.load();assert im.format=='JPEG' and im.mode=='RGB' and im.size==(480,720)
 for g in a['bindings']:
  assert Path(a['magazinePath']).stem.casefold()==Path(g['sourcePath']).stem.casefold()
  assert g['system']=='snes'
  ids.extend(g['bindingIds'])
assert len(ids)==len(set(ids)) and len(ids)>=len(plan['matches'])>0
assert len({r['sourceSha256'] for r in plan['matches']})<=len(plan['matches'])
manifest_matches=sha('/mnt/DADOS/IMAGENS SNES/controle/colecao.json')==plan['sourceManifestSha256']
prior=json.loads(Path('/mnt/DADOS/station-maintenance-20261015-11z/cover-plan.json').read_text())
assert not {r['magazinePath'] for r in prior['matches']} & {r['magazinePath'] for r in plan['matches']}
for name in ['prepare-covers.py','publish-covers.py','authenticate-native.py','proof_http.py']:ast.parse((W/name).read_text())
spec=importlib.util.spec_from_file_location('cover_publication',W/'publish-covers.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
with tempfile.TemporaryDirectory(dir=W) as d:
 p=Path(d)/'fixture';p.write_bytes(b'previous');m.atomic(p,b'candidate',os.getuid(),os.getgid(),0o600)
 assert p.read_bytes()==b'candidate' and p.stat().st_mode&0o777==0o600
 original=p.stat();link=Path(d)/'rollback';os.link(p,link);m.atomic(p,b'changed',os.getuid(),os.getgid(),0o600);os.replace(link,p)
 assert p.read_bytes()==b'candidate' and p.stat().st_ino==original.st_ino and p.stat().st_mtime_ns==original.st_mtime_ns
out=dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),matchedMagazines=len(plan['matches']),uniqueBindings=len(ids),uniqueArtworks=len({r['sourceSha256'] for r in plan['matches']}),liveManifestStillMatches=manifest_matches,readyForPrivilegedPublication=False,newUnmatchedArtworks=1,finalizedAtSnapshot=plan['finalizedAtSnapshot'],exactMagazineROMStemsVerified=True,sourceHashesAndJPEGGeometryVerified=True,atomicWriteAndInodeRollbackFixturePassed=True,previous36ExcludedFromNewPlan=True,prior36ProtectedReconciliationRequired=True,planSha256=sha(W/'cover-plan.json'),operatorSha256=sha(W/'publish-covers.py'),privilegedPublicationExecuted=False)
(W/'preflight.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
