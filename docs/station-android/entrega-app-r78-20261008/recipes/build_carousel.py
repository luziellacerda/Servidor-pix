"""Reproduce frozen R77, then add exact synopsis coverage and conservative text selection.

Run only after the R78 editorial data and OVERLAY-MANIFEST.json are frozen.
No phone, APK packaging, engine, netplay, artwork, frame pacing or media changes.
"""
from pathlib import Path
import argparse,datetime,hashlib,json,os,re,shutil,subprocess
HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
BASE=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\carousel-build-02')
BASE_RECEIPT='05a393b69d1b55a12453cf406f8844ae5fcba8210e0b43ec701fe115218edc59'
BASE_NATIVE='98e951b2aad45d63ebe563de95d7a9299339928e54ce082915a147a992ab0302'
OVERLAY=ROOT/'native'
def require(value,message):
 if not value:raise RuntimeError(message)
def sha(p):
 with Path(p).open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest()
def dependencies(source,includes):
 result={}
 def visit(p):
  p=p.resolve()
  if str(p)in result:return
  result[str(p)]=sha(p)
  for name in re.findall(r'^\s*#\s*include\s*"([^"\n]+)"',p.read_text('utf8'),re.M):
   child=next((d/name for d in [p.parent,*includes] if(d/name).is_file()),None)
   require(child is not None,'Missing quoted include '+name);visit(child)
 visit(source);return result
def main():
 p=argparse.ArgumentParser();p.add_argument('--output',type=Path,default=Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008\carousel-build'));a=p.parse_args();work=a.output.resolve()
 require(work.drive.upper()=='E:' and not work.exists(),'Use a new E: output directory')
 require(sha(BASE/'evidence/build.json')==BASE_RECEIPT,'R77 receipt changed')
 receipt=json.loads((BASE/'evidence/build.json').read_text('utf8'))
 require(sha(BASE/'libturbo_carousel.so')==receipt['nativeSHA256']==BASE_NATIVE,'R77 library changed')
 require(len(receipt['nativeSources'])==67 and len(receipt['objects'])==7,'R77 composition changed')
 for name,h in receipt['nativeSources'].items():require(sha(BASE/'native'/name)==h,'R77 source changed '+name)
 for path,h in receipt['objects'].items():require(sha(path)==h,'R77 object changed '+path)
 overlay=json.loads((ROOT/'OVERLAY-MANIFEST.json').read_text('utf8'))
 require(set(overlay['sources'])=={'native_info.h','collection_presentation.h','station_game_infos.h','station_synopsis_selection.h','station_collection_editorial_aliases.h'},'Overlay scope')
 for name,h in overlay['sources'].items():require(sha(OVERLAY/name)==h,'Overlay changed '+name)
 for name,h in overlay['dataSources'].items():require(sha(ROOT/name)==h,'Frozen data changed '+name)
 command=list(receipt['command']);source=str(BASE/'native/native_carousel.cpp');output=str(BASE/'libturbo_carousel.so')
 require(command.count(source)==command.count(output)==1,'Source/output arguments')
 includes=[Path(command[i+1]) for i,v in enumerate(command) if v=='-I']
 original=dependencies(Path(source),includes)
 require(original==receipt['quotedDependencies'],'R77 external includes changed')
 compiler=Path(command[0]);host=Path(r'C:\Program Files\LLVM\bin\clang++.exe')
 recipe_hash=sha(__file__);manifest_hash=sha(ROOT/'OVERLAY-MANIFEST.json')
 for name in ('native','tests','temp','evidence'):(work/name).mkdir(parents=True)
 env=dict(os.environ,TEMP=str(work/'temp'),TMP=str(work/'temp'));executions=[]
 def run(argv,label):
  argv=list(map(str,argv));r=subprocess.run(argv,capture_output=True,env=env,timeout=300)
  log=work/'evidence'/(label+'.log');log.write_bytes(r.stdout+r.stderr)
  executions.append({'label':label,'command':argv,'exitCode':r.returncode,'logSHA256':sha(log)})
  require(r.returncode==0,label+': '+r.stderr.decode('utf8','replace')[-2400:]);return r.stdout.decode('utf8','replace').strip()
 baseline=list(command);baseline[baseline.index(output)]=str(work/'baseline-r77.so')
 run(baseline,'baseline-reproduction');require(sha(work/'baseline-r77.so')==BASE_NATIVE,'R77 byte reproduction failed; stop')
 for name in receipt['nativeSources']:shutil.copyfile(BASE/'native'/name,work/'native'/name)
 for name in overlay['sources']:shutil.copyfile(OVERLAY/name,work/'native'/name)
 sources={f.name:sha(f) for f in (work/'native').iterdir() if f.is_file()}
 changed=sorted(n for n,h in receipt['nativeSources'].items() if sources[n]!=h)
 added=sorted(set(sources)-set(receipt['nativeSources']))
 require(changed==['collection_presentation.h','native_info.h','station_game_infos.h'] and added==['station_collection_editorial_aliases.h','station_synopsis_selection.h'],'Native change scope')
 common=[host,'-std=c++17','-Wall','-Wextra','-Werror','-finput-charset=UTF-8','-fexec-charset=UTF-8','-I',work/'native']
 tests={
  'player-label':ROOT.parent/'station-multiplayer-r77-20261008/tests/catalog_player_label.cpp',
  'selection':ROOT/'tests/synopsis_selection.cpp',
  'editorial':ROOT/'tests/collection_editorial.cpp',
  'scroll':ROOT.parent/'station-synopsis-console-r21-20261005/synopsis-tests/test_synopsis_scroll.cpp',
  'corners':ROOT.parent/'station-collection-media-r69-20261007/tests/collection_corners.cpp',
  'decoder':ROOT.parent/'station-current-r55-20261006/tests/native/test_video_policy.cpp',
  'navigation':ROOT.parent/'station-collection-navigation-r70-20261007/tests/navigation.cpp',
  'routes':ROOT.parent/'station-online-layout-r71-20261007/tests/collection_all_video.cpp',
  'settings':ROOT.parent/'station-collection-navigation-r70-20261007/tests/collection_settings.cpp'}
 test_hashes={n:sha(f) for n,f in tests.items()};results={}
 # Compile the existing scroll cases against the current, hash-pinned headers.
 scroll=tests['scroll'].read_text('utf8')
 for header in ('station_info_layout.h','station_synopsis_scroll.h'):
  anchor='#include "../native/'+header+'"'
  require(scroll.count(anchor)==1,'Scroll include anchor')
  scroll=scroll.replace(anchor,'#include "'+header+'"')
 staged_scroll=work/'tests/scroll.cpp';staged_scroll.write_text(scroll,'utf8')
 # The baseline selector expression is copied verbatim, not rewritten as a model.
 line=next(line for line in (BASE/'native/native_info.h').read_text('utf8').splitlines() if 'body=(*serverDescription' in line)
 expr=line.strip().removeprefix('body=').removesuffix(';')
 baseline_header=work/'tests/production_baseline_selector.h'
 baseline_header.write_text('static const char*productionBaselineSynopsis(const char*id,const char*serverDescription,const GameInfo*gameInfo){return '+expr+';}\n','utf8')
 lookup=next(Path(path) for path in original if Path(path).name=='station_game_lookup.h')
 shutil.copyfile(lookup,work/'tests/station_game_lookup.h')
 paging=ROOT.parent/'station-current-r55-20261006/client/src/native/station_synopsis_pages.hpp'
 paging_hash=sha(paging)
 require(paging_hash=='db770934587c73a627d0c333249d7e6cd93c11b4e98abda8cf524dc77b7194e0','Frozen runtime paging changed')
 shutil.copyfile(paging,work/'tests/station_synopsis_pages.hpp')
 r37data=ROOT.parent/'station-final-details-r37-20261005/data/synopses-complete.json'
 require(sha(r37data)=='74fea17341097e47ec30fb543bb6b67d4a3d44d2fc1ca6a82b5989fff3d06010','R37 raw published values changed')
 published={row['itemId']:row['serverDescription'] for row in json.loads(r37data.read_text('utf8'))}
 audit=json.loads((ROOT/'catalog/existing-synopses-audit.json').read_text('utf8'))['records']
 for row in audit:
  if row['itemId'] not in published:published[row['itemId']]=row['selectedDescription']
 data=json.loads((ROOT/'data/synopses-complete.json').read_text('utf8'))
 require(len(data)==len(published)==2467 and {r['itemId'] for r in data}==set(published),'Synopsis fixture coverage')
 cases=['struct PublishedSynopsisCase {const char*id;const char*platform;const char*published;const char*expected;};','static const PublishedSynopsisCase publishedSynopsisCases[]={']
 for row in data:
  values=[row['itemId'],row['label'],published[row['itemId']],row['description']]
  cases.append('{'+','.join(json.dumps(value,ensure_ascii=False).replace('?',r'\?') for value in values)+'},')
 cases.append('};');case_header=work/'tests/generated_synopsis_cases.h';case_header.write_text('\n'.join(cases)+'\n','utf8')
 synopsis_common=common+['-Wno-unused-function','-Wno-misleading-indentation','-I',work/'tests']
 for label,source_file in [('selection',tests['selection']),('editorial',tests['editorial']),('scroll',staged_scroll)]:
  run(synopsis_common+[source_file,'-o',work/'tests'/(label+'.exe')],label+'-compile')
  results[label]=run([work/'tests'/(label+'.exe')],label+'-test')
  require(('0 failures' in results[label] if label=='scroll' else 'PASS' in results[label]) and 'FAIL' not in results[label],label+' result')
 for name in ['player-label','corners','decoder']:
  run(common+[tests[name],'-o',work/'tests'/(name+'.exe')],name+'-compile');results[name]=run([work/'tests'/(name+'.exe')],name+'-test');require(results[name].startswith('PASS'),name+' result')
 run([host,'-std=c++17','-O2','-finput-charset=UTF-8','-fexec-charset=UTF-8','-I',work/'native',tests['navigation'],'-o',work/'tests/navigation.exe'],'navigation-compile')
 results['navigation']=run([work/'tests/navigation.exe'],'navigation-test');require(results['navigation'].startswith('PASS 3584 '),'Navigation regression')
 symbols=re.findall(r'extern const unsigned char (\w+)\[\];',(work/'native/video720_posters.h').read_text('utf8'))
 stubs=work/'tests/poster_symbols.cpp';stubs.write_text('extern "C" {\n'+'\n'.join('extern const unsigned char '+s+'[]={0};' for s in symbols)+'\n}\n','utf8')
 run(common+[tests['routes'],stubs,'-o',work/'tests/routes.exe'],'routes-compile');results['routes']=run([work/'tests/routes.exe'],'routes-test');require(results['routes'].startswith('PASS 3444 '),'Routes regression')
 run(common+['-Wno-unused-function','-fsyntax-only',tests['settings']],'settings-test')
 command[command.index(source)]=str(work/'native/native_carousel.cpp');command[command.index(output)]=str(work/'libturbo_carousel.so')
 candidate_dependencies=dependencies(work/'native/native_carousel.cpp',includes)
 expected={}
 for path,h in original.items():
  old=Path(path);relative=old.relative_to(BASE/'native') if old.is_relative_to(BASE/'native') else None
  equivalent=work/'native'/relative if relative else old
  expected[str(equivalent.resolve())]=overlay['sources'].get(str(relative),h)
 for name,h in overlay['sources'].items():expected[str((work/'native'/name).resolve())]=h
 require(candidate_dependencies==expected,'External include or unexpected dependency changed')
 run(command,'carousel-build')
 elf=run([compiler.parent/'llvm-readelf.exe','-h','-l',work/'libturbo_carousel.so'],'elf-check')
 aligns=[int(line.split()[-1],16) for line in elf.splitlines() if line.strip().startswith('LOAD ')]
 require('AArch64' in elf and aligns and min(aligns)>=16384,'ELF alignment')
 require(sha(__file__)==recipe_hash and sha(ROOT/'OVERLAY-MANIFEST.json')==manifest_hash,'Recipe changed during compilation')
 require(sources=={f.name:sha(f) for f in (work/'native').iterdir() if f.is_file()},'Staged sources changed')
 require(candidate_dependencies==dependencies(work/'native/native_carousel.cpp',includes),'Includes changed during compilation')
 for n,h in test_hashes.items():require(sha(tests[n])==h,'Test changed '+n)
 for name,h in overlay['dataSources'].items():require(sha(ROOT/name)==h,'Frozen data changed during build '+name)
 require(sha(paging)==paging_hash,'Paging function changed during build')
 for path,h in receipt['objects'].items():require(sha(path)==h,'Object changed '+path)
 record={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'compiled':True,'baseNativeSHA256':BASE_NATIVE,'baselineReproductionSHA256':sha(work/'baseline-r77.so'),'nativeSHA256':sha(work/'libturbo_carousel.so'),'nativeBytes':(work/'libturbo_carousel.so').stat().st_size,'nativeSources':sources,'changedNativeSources':changed,'addedNativeSources':added,'objects':receipt['objects'],'baseQuotedDependencies':original,'quotedDependencies':candidate_dependencies,'command':command,'baselineCommand':baseline,'overlayManifestSHA256':manifest_hash,'recipeSHA256':recipe_hash,'compilerSHA256':sha(compiler),'testSources':{n:{'path':str(tests[n]),'sha256':h} for n,h in test_hashes.items()},'results':results,'executions':executions,'minimumLoadAlignment':min(aligns),'mediaChanged':False,'geometryChanged':False,'ratingsChanged':False,'emulatorChanged':False,'displayBinding':'Exact game ID/platform synopsis fallback; only blank, exact placeholder/title or reviewed replacement loses published prose; 5 exact editorial-path aliases', 'playerEvidenceBindingChanged':False, 'synopsisOnly':True, 'baselineSelectorExpressionSHA256':hashlib.sha256(expr.encode('utf8')).hexdigest(), 'scrollTestStagedSHA256':sha(staged_scroll), 'lookupHeaderSHA256':sha(lookup), 'pagingHeaderSHA256':paging_hash, 'publishedCasesSHA256':sha(case_header), 'dataSources':overlay['dataSources'],'packaged':False,'installed':False,'physicalDisplayVerified':False}
 (work/'evidence/build.json').write_text(json.dumps(record,indent=2)+'\n','utf8');(ROOT/'evidence/carousel-build.json').write_text(json.dumps(record,indent=2)+'\n','utf8')
 print(json.dumps({'compiled':True,'nativeSHA256':record['nativeSHA256'],'results':results,'path':str(work/'libturbo_carousel.so')},indent=2))
if __name__=='__main__':main()
