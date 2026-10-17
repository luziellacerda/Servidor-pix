from pathlib import Path
from datetime import datetime, timezone
import hashlib, importlib.util, json, re, sys
from PIL import Image
sys.dont_write_bytecode=True
W=Path(__file__).resolve().parent
DOC=Path('/mnt/DADOS/servidor-pix-station-manutencao-20261015-11z/docs/station-android')
TOOLS=DOC/'online-plataformas-20261009/library-tools'
sys.path.insert(0,str(TOOLS))
spec=importlib.util.spec_from_file_location('current_cover_compiler',TOOLS/'atualizar-biblioteca-station.py');compiler=importlib.util.module_from_spec(spec);spec.loader.exec_module(compiler)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(n,j):(W/n).write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n')
source=Path('/mnt/DADOS/IMAGENS SNES'); raw=(source/'controle/colecao.json').read_bytes(); catalog=json.loads(raw)
old=json.loads(Path('/mnt/DADOS/station-snes-capas-locais-20261014/plan.json').read_bytes())
known={x['sourceDeclaredSha256'] for x in old['matches']}; unlinked={x['sourceFile'] for x in old['unmatchedDelivered']}
ready=[x for x in catalog if x.get('status') in {'completed-4k-composite','provided-original-4k'} and x.get('outputSha256') and x['outputSha256'] not in known and Path(x['outputFile']).name not in unlinked]
# Remove only revision/language tags; retain geographic and edition tags.
def release(s):
 s=re.sub(r'\((?:Rev [0-9A-Z]+|En(?:,[A-Za-z]{2})*)\)','',s)
 return re.sub(r'\s+',' ',s).strip().casefold()
assets={}
for a in ready:
 for t in [a['primaryRelease']]+[v['name'] for v in a.get('variants',[])]:assets.setdefault(release(t),[]).append(a)
groups=json.loads(Path('/mnt/DADOS/station-r128-20261011/plan.json').read_bytes())['games']; selected={};used=set()
for g in groups:
 if g['system']!='snes':continue
 matches={x['outputSha256']:x for x in assets.get(release(Path(g['sourcePath']).stem),[])}
 if len(matches)>1:raise ValueError('Ambiguous exact release')
 if not matches:continue
 a=next(iter(matches.values())); target=Path(g['magazinePath']);path=source/a['outputFile']
 assert target.is_relative_to(Path('/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc/snes/media/revista'))
 assert path.parent==source/'capas' and not path.is_symlink() and not target.is_symlink()
 assert sha(path)==a['outputSha256']
 dest=W/'compiled'/a['outputSha256'];dest.parent.mkdir(exist_ok=True,mode=0o700)
 if not dest.exists():
  prior=Path('/mnt/DADOS/station-maintenance-20261014-18z/compiled')/a['outputSha256']
  if prior.exists():
   import shutil
   shutil.copy2(prior,dest)
  else:compiler.compile_cover(path,dest)
 with Image.open(dest) as im:assert im.format=='JPEG' and im.mode=='RGB' and im.size==(480,720)
 key=str(target)
 if key not in selected:
  selected[key]=dict(magazinePath=key,sourcePath=str(path),sourceSha256=a['outputSha256'],sourceTitle=a['title'],primaryRelease=a['primaryRelease'],sourceBytes=path.stat().st_size,compiledPath=str(dest),compiledSha256=sha(dest),compiledBytes=dest.stat().st_size,oldMagazineSha256=sha(target),bindings=[],matchRule='exact-ROM-release-region-and-edition; revision/language-tags-only-normalized')
 assert selected[key]['sourceSha256']==a['outputSha256']
 selected[key]['bindings'].append({k:g[k] for k in ['sourcePath','bindingIds','itemId','system']});used.add(a['outputSha256'])
plan=dict(preparedUtc=datetime.now(timezone.utc).isoformat(),sourceManifestSha256=hashlib.sha256(raw).hexdigest(),finalizedAtSnapshot=sum(x.get('status') in {'completed-4k-composite','provided-original-4k'} for x in catalog),newFinalArtworks=len(ready),matches=list(selected.values()),unmatched=[dict(title=a['title'],primaryRelease=a['primaryRelease'],sourceFile=a['outputFile'],reason='no exact installed ROM release/region/edition') for a in ready if a['outputSha256'] not in used],compilerPath=str(TOOLS/'atualizar-biblioteca-station.py'),compilerSha256=sha(TOOLS/'atualizar-biblioteca-station.py'),productionChanged=False)
write('cover-plan.json',plan)
print(json.dumps(dict(newFinalArtworks=len(ready),changedMagazines=len(selected),bindings=sum(len(b['bindingIds']) for r in selected.values() for b in r['bindings']),unmatched=len(plan['unmatched']))))
print('\n'.join(r['primaryRelease']+' => '+Path(r['magazinePath']).name for r in selected.values()))
