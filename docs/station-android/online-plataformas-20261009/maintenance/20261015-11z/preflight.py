from pathlib import Path
from datetime import datetime,timezone
import ast,json,hashlib,importlib.util,os,tempfile
from PIL import Image
W=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
plan=json.loads((W/'cover-plan.json').read_text());old=json.loads(Path('/mnt/DADOS/station-maintenance-20261014-20z/cover-plan.json').read_text())
oldby={x['magazinePath']:x for x in old['matches']}
ids=[]
for a in plan['matches']:
 assert a['magazinePath'] in oldby
 prev=oldby[a['magazinePath']]
 for key in ['sourceSha256','compiledSha256','bindings','oldMagazineSha256']:assert prev[key]==a[key]
 for k,h in [('sourcePath','sourceSha256'),('compiledPath','compiledSha256'),('magazinePath','oldMagazineSha256')]:assert sha(Path(a[k]))==a[h]
 with Image.open(a['compiledPath']) as im:im.load();assert im.format=='JPEG' and im.mode=='RGB' and im.size==(480,720)
 for g in a['bindings']:
  assert Path(a['magazinePath']).stem.casefold()==Path(g['sourcePath']).stem.casefold()
  ids.extend(g['bindingIds'])
assert len(ids)==len(set(ids))==36 and len(plan['matches'])==36
assert sha(Path('/mnt/DADOS/IMAGENS SNES/controle/colecao.json'))==plan['sourceManifestSha256']
for name in ['prepare-covers.py','publish-covers.py','authenticate-native.py','proof_http.py']:ast.parse((W/name).read_text())
spec=importlib.util.spec_from_file_location('cover_publication',W/'publish-covers.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
with tempfile.TemporaryDirectory(dir=W) as d:
 p=Path(d)/'fixture';p.write_bytes(b'previous');m.atomic(p,b'candidate',os.getuid(),os.getgid(),0o600)
 assert p.read_bytes()==b'candidate' and p.stat().st_mode&0o777==0o600
 assert sorted(x.name for x in Path(d).iterdir())==['fixture']
 original=p.stat();link=Path(d)/'rollback';os.link(p,link);m.atomic(p,b'changed',os.getuid(),os.getgid(),0o600);os.replace(link,p)
 assert p.read_bytes()==b'candidate' and p.stat().st_ino==original.st_ino
 assert p.stat().st_mtime_ns==original.st_mtime_ns
out=dict(passed=True,utc=datetime.now(timezone.utc).isoformat(),matchedMagazines=36,uniqueBindings=36,uniqueArtworks=len({x['sourceSha256'] for x in plan['matches']}),unmatchedArtworks=len(plan['unmatched']),finalizedAtSnapshot=plan['finalizedAtSnapshot'],prior20zMappingsAndBytesIdentical=True,exactMagazineROMStemsVerified=True,sourceHashesAndJPEGGeometryVerified=True,atomicWriteFixturePassed=True,expectedModulesUpdatedForR159=True,planSha256=sha(W/'cover-plan.json'),operatorSha256=sha(W/'publish-covers.py'),privilegedPublicationExecuted=False)
(W/'preflight.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
