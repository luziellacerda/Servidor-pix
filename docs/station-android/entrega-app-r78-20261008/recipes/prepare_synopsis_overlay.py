"""Prepare the narrow R78 synopsis renderer overlay; never build or alter R77."""
from pathlib import Path
import argparse,hashlib,json

SNAPSHOT=Path(__file__).resolve().parents[1]
REPO=SNAPSHOT.parents[1]
BASE=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\carousel-build-02\native')
PINS={'native_info.h':'ca2699815af30fbfc38c17040a6fcf67717e8110f727c92d979bd71a7256d2f6',
      'collection_presentation.h':'dec291e81f6dedafbc2a034e62834af29b44ed55554e766dd418bdde10d1a9e8'}
def sha(data):return hashlib.sha256(data).hexdigest()
def replace_once(value,old,new):
 if value.count(old)!=1:raise ValueError('Expected exactly one source anchor: '+old[:70])
 return value.replace(old,new,1)
def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--freeze',action='store_true');args=parser.parse_args()
 output=SNAPSHOT/'native';output.mkdir(parents=True,exist_ok=True);report={}
 for name,expected in PINS.items():
  raw=(BASE/name).read_bytes()
  if sha(raw)!=expected:raise ValueError('Frozen R77 input changed: '+name)
  source=raw.decode('utf8')
  if name=='native_info.h':
   source=replace_once(source,'#include "station_synopsis_scroll.h"','#include "station_synopsis_scroll.h"\n#include "station_synopsis_selection.h"')
   source=replace_once(source,'  body=(*serverDescription&&!stationSynopsisNeedsOverride(id,serverDescription))?serverDescription:gameInfo?gameInfo->pages:"Sinopse ainda não localizada para esta edição.";',
    '  body=stationSynopsisChoose(serverDescription,heading,gameInfo?gameInfo->name:nullptr,gameInfo?gameInfo->pages:nullptr,gameInfo&&stationSynopsisNeedsOverride(id,serverDescription),"Sinopse ainda não localizada para esta edição.").text;')
  else:
   source=replace_once(source,'static const char*collectionEditorial(const char*platform,const char*path){',
    '#include "station_collection_editorial_aliases.h"\nstatic const char*collectionEditorial(const char*platform,const char*path){\n path=stationCollectionEditorialPath(platform,path);')
  result=source.encode('utf8');(output/name).write_bytes(result)
  report[name]={'baseSHA256':expected,'overlaySHA256':sha(result)}
 for name in ['station_synopsis_selection.h','station_collection_editorial_aliases.h']:
  report[name]={'overlaySHA256':sha((output/name).read_bytes())}
 report['scope']='Selection and exact editorial-path aliases only. Layout, scroll, videos, catalogue IDs, controls and netplay unchanged. No APK or native library built.'
 evidence=SNAPSHOT/'evidence';evidence.mkdir(exist_ok=True)
 (evidence/'synopsis-overlay.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 if args.freeze:
  required={'native_info.h','collection_presentation.h','station_game_infos.h','station_synopsis_selection.h','station_collection_editorial_aliases.h'}
  records=json.loads((SNAPSHOT/'data/synopses-complete.json').read_text('utf8'))
  if len(records)!=2467 or len({r['itemId'] for r in records})!=2467:raise ValueError('Final coverage must include every exact ID')
  data_paths=['data/synopses-complete.json','catalog/existing-synopses-audit.json','data/exact-server-overrides.json','data/editorial-synopses.json']
  manifest={'base':'R77','baseNativeSHA256':'98e951b2aad45d63ebe563de95d7a9299339928e54ce082915a147a992ab0302',
   'scope':report['scope'],'sources':{name:sha((output/name).read_bytes()) for name in sorted(required)},
   'dataSources':{name:sha((SNAPSHOT/name).read_bytes()) for name in data_paths}}
  (SNAPSHOT/'OVERLAY-MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 print(json.dumps(report,ensure_ascii=False))
if __name__=='__main__':main()
