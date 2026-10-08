"""Validate research coverage and provenance without turning display metadata into permission."""
from pathlib import Path
import argparse,hashlib,importlib.util,json
HERE=Path(__file__).resolve().parent
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 spec=importlib.util.spec_from_file_location('metadata',HERE/'build_game_metadata.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
 count=0
 def check(value,label):
  nonlocal count
  count+=1
  if not value:raise AssertionError(label)
 fixture='clrmamepro ( name "fixture" )\ngame ( name "Edition (USA)" developer "A \\"Quoted\\" Studio" rom ( name "Edition (USA).sfc" size 1024 crc ABCDEF12 ) )'
 # The literal DAT escape is backslash-quote, not JSON serialized text.
 fixture=fixture.replace('\\\\"','\\"')
 parsed=m.parse_dat(fixture);check(len(parsed)==1,'one nested game');check(m.first(parsed[0],'name')=='Edition (USA)','parentheses inside strings');check(m.first(parsed[0]['rom'][0],'size')=='1024','nested ROM fields')
 for bad in ['game ( name','game ( name "unfinished"','game name "x"','game ( ( ) )']:
  try:m.parse_dat(bad)
  except ValueError:check(True,'invalid DAT rejected')
  else:check(False,'invalid DAT accepted')
 check(m.stem('x/y/Game (USA) (Rev 1).sfc')=='game (usa) (rev 1)','edition preserved')
 check(m.stem('Game (USA).sfc')!=m.stem('Game (Europe).sfc'),'regions never collapsed')
 check(m.stem('Game (USA) [T-Br].sfc')!=m.stem('Game (USA).sfc'),'translation not propagated')
 source=json.loads((HERE/'catalog-inventory.json').read_text('utf8'));output=json.loads((HERE/'catalog-game-metadata.json').read_text('utf8'));report=json.loads((HERE/'metadata-coverage.json').read_text('utf8'));manifest=json.loads((HERE/'metadata-source-manifest.json').read_text('utf8'))
 source_ids={r['itemId']:r for r in source['records']};records=output['records'];check({r['itemId'] for r in records}==set(source_ids),'all IDs covered');check(len(records)==len(source_ids),'no duplicates');check(sum(r['catalogVisible'] for r in records)==2212,'all visible games')
 check(report['outputSHA256']==sha(HERE/'catalog-game-metadata.json'),'output hash pinned');check(report['inventorySHA256']==sha(HERE/'catalog-inventory.json'),'catalog hash pinned');check(report['sourceManifestSHA256']==sha(HERE/'metadata-source-manifest.json'),'source manifest pinned')
 check(not report['allFactsComplete'] and report['contentVerifiedIds']==0,'limits explicit')
 sources=output['sources'];refs=0
 for row in records:
  original=source_ids[row['itemId']]
  check(row['artifactSha256']==original['artifactSha256'],'artifact binding preserved');check(row['contentSha256']==original['contentSha256'],'no invented content hash');check(row['onlineApproved'] is False,'no factual entry authorizes online')
  check(row['playersSimultaneous'] is None and row['playersMode'] is None,'maxusers never becomes simultaneous mode');check(row['evidenceStatus']=='research-only-not-verified-exact-content','title/size candidate never becomes hash verification')
  check(row['fields']['synopsis']['value'] is None,'title is not a synopsis');check(row['fields']['rating']['value'] is None,'no fake ratings')
  for name,field in row['fields'].items():
   check((field['value'] is None)==(name in row['unknownFields']),'unknown coverage complete')
   for ref in field['sourceRefs']+row['identitySourceRefs']:
    refs+=1;check(ref['sourceId'] in sources and ref['line']>0,'exact source/line exists')
   if field['value'] is not None:check(row['editionMatch']=='exact-edition-and-size-candidate' and bool(field['sourceRefs']),'known candidate has edition and provenance')
 check(all(not row['onlineApproved'] for row in records),'fail closed all IDs')
 result={'passed':True,'checks':count,'referencedEntries':refs,'ids':len(records),'visibleIds':2212,'metadataSHA256':sha(HERE/'catalog-game-metadata.json'),'recipeSHA256':sha(__file__),'builderSHA256':sha(HERE/'build_game_metadata.py'),'scope':'parser, hashes, provenance, full inventory coverage and fail-closed assertions; no factual/ROM/gameplay validation'}
 (HERE/'metadata-tests.json').write_text(json.dumps(result,indent=2)+'\n','utf8');print(json.dumps(result))
if __name__=='__main__':main()
