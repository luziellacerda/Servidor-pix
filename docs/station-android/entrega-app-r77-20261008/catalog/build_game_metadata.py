"""Research every exported catalog ID against pinned maintained DATs without fuzzy approval.

An exact title/edition/size match supplies a descriptive candidate, not verification
of the installed bytes. CRC links only join records inside the upstream database.
"""
from pathlib import Path,PurePosixPath
import argparse,collections,hashlib,json,re,unicodedata
from datetime import datetime,timezone
HERE=Path(__file__).resolve().parent
SOURCES=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog\libretro-sources')
OUT=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog\metadata')
TOKEN=re.compile(r'"(?:\\.|[^"\\])*"|[()]|[^\s()]+')
FIELDS={'developer':'developer','publisher':'publisher','genre':'genre','releaseyear':'releaseYear','releasemonth':'releaseMonth','users':'playersMaximumDescriptive','franchise':'franchise','esrb_rating':'ageRating'}
ALIASES={'snesbr':'snes','megadrivebr':'megadrive'}
def sha(data):return hashlib.sha256(data).hexdigest()
def scalar(token):
 if token.startswith('"'):return token[1:-1].replace('\\"','"').replace('\\\\','\\')
 return token
def parse_dat(text):
 tokens=[(m.group(),m.start()) for m in TOKEN.finditer(text)];at=0
 def obj():
  nonlocal at
  result={}
  while at<len(tokens):
   key=tokens[at][0];at+=1
   if key==')':return result
   if key=='(' or at>=len(tokens):raise ValueError('Invalid DAT field')
   value=tokens[at][0];at+=1
   value=obj() if value=='(' else scalar(value)
   result.setdefault(key,[]).append(value)
  raise ValueError('Unterminated DAT')
 result=[]
 while at<len(tokens):
  kind,offset=tokens[at];at+=1
  if at>=len(tokens) or tokens[at][0]!='(':raise ValueError('Invalid DAT section')
  at+=1;record=obj()
  if kind=='game':record['_line']=text.count('\n',0,offset)+1;result.append(record)
 return result
def first(row,key):return row.get(key,[None])[0]
def basename(name):return PurePosixPath(name.replace('\\','/')).name
def stem(name):return unicodedata.normalize('NFC',PurePosixPath(basename(name)).stem).casefold()
def dump(path,data):path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8')
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--sources',type=Path,default=SOURCES);p.add_argument('--output',type=Path,default=OUT);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
 inventory=json.loads((HERE/'catalog-inventory.json').read_text('utf8'));manifest=json.loads((a.sources/'source-manifest.json').read_text('utf8'))
 bases=collections.defaultdict(list);metadata=collections.defaultdict(lambda:collections.defaultdict(list));parse_counts={};source_index={}
 for source in manifest['files']:
  if source['status']!='downloaded' or not source['path'].endswith('.dat'):continue
  raw=(a.sources/source['path']).read_bytes();assert sha(raw)==source['sha256'],'Source hash changed'
  games=parse_dat(raw.decode('utf-8-sig'));parse_counts[source['path']]=len(games);platform=source['platform'];source_id=source['path'];source_index[source_id]=source
  for game in games:
   game['_source']=source_id;game['_platform']=platform
   roms=game.get('rom',[])
   if '/no-intro/' in source_id or '/redump/' in source_id or source_id.startswith('dat/'):
    bases[platform].append(game)
   for rom in roms:
    crc=first(rom,'crc')
    if crc:metadata[(platform,crc.upper())]['entries'].append(game)
 indexes={}
 for platform,games in bases.items():
  index=collections.defaultdict(list)
  for game in games:
   names=set([str(first(game,'name') or '')]+[stem(str(first(rom,'name') or '')) for rom in game.get('rom',[])])
   for name in names:
    key=unicodedata.normalize('NFC',name).casefold()
    if key:index[key].append(game)
  indexes[platform]=index
 curation=json.loads((HERE/'mode-evidence.json').read_text('utf8'));curated={v['itemId']:(t,v) for t in curation['titles'] for v in t['variants']}
 records=[];counts=collections.Counter();field_counts=collections.Counter()
 for item in inventory['records']:
  platform=ALIASES.get(item['platform'],item['platform']);launch=item['artifactLaunchPath'] or item['artifactFileName'];identity_name=stem(launch);matches=indexes.get(platform,{}).get(identity_name,[])
  unique={json.dumps({k:v for k,v in game.items() if not k.startswith('_')},sort_keys=True):game for game in matches};matches=list(unique.values())
  expected_size=item['artifactExpandedSizeBytes'] if item['artifactFormat']!='raw' else item['artifactSizeBytes']
  size_compatible=[]
  for game in matches:
   roms=game.get('rom',[]);sizes=[int(first(rom,'size') or 0) for rom in roms]
   if len(roms)==1 and sizes[0]==expected_size:size_compatible.append(game)
  status='unmatched';chosen=None
  if len(size_compatible)==1:chosen=size_compatible[0];status='exact-edition-and-size-candidate'
  elif len(size_compatible)>1:status='ambiguous-exact-edition'
  elif matches:status='size-or-container-mismatch'
  fields={v:{'value':None,'status':'unknown','sourceRefs':[]} for v in FIELDS.values()}
  fields['synopsis']={'value':None,'status':'unknown-no-synopsis-in-these-dat-sources','sourceRefs':[]}
  fields['rating']={'value':None,'status':'unknown-no-rating-source','sourceRefs':[]}
  refs=[];identity=None
  if chosen:
   rom=chosen['rom'][0];crc=first(rom,'crc');identity={'databaseName':first(chosen,'name'),'romName':first(rom,'name'),'sizeBytes':int(first(rom,'size')),'crc32':crc,'md5':first(rom,'md5'),'sha1':first(rom,'sha1'),'region':first(chosen,'region'),'contentHashVerified':False,'matchUsesTitleEditionAndSize':True}
   entries=[chosen]+(metadata[(platform,crc.upper())]['entries'] if crc else [])
   for upstream_key,field in FIELDS.items():
    values={};value_refs=collections.defaultdict(list)
    for entry in entries:
     for value in entry.get(upstream_key,[]):
      if not isinstance(value,str):continue
      normalized=int(value) if upstream_key in ('users','releaseyear','releasemonth') and value.isdigit() else value
      key=json.dumps(normalized,ensure_ascii=False);values[key]=normalized
      ref={'sourceId':entry['_source'],'line':entry['_line'],'databaseCRC32':crc}
      if ref not in value_refs[key]:value_refs[key].append(ref)
    if len(values)==1:
     key=next(iter(values));fields[field]={'value':values[key],'status':'descriptive-database-candidate','sourceRefs':value_refs[key]};field_counts[field]+=1
    elif len(values)>1:fields[field]={'value':None,'status':'conflicting-source-values','candidates':[{'value':v,'sourceRefs':value_refs[k]} for k,v in values.items()],'sourceRefs':[]}
   refs=[{'sourceId':chosen['_source'],'line':chosen['_line']}]
  blockers=[]
  if item['contentSha256'] is None:blockers.append('CONTENT_SHA256_MISSING')
  blockers.append('PUBLISHED_CONTENT_NOT_HASH_MATCHED_TO_DAT')
  if status!='exact-edition-and-size-candidate':blockers.append('EDITION_OR_CONTAINER_IDENTITY_REQUIRES_REVIEW')
  unknown=[k for k,v in fields.items() if v['value'] is None]
  if unknown:blockers.append('FACTUAL_FIELDS_INCOMPLETE')
  original_modes=[];mode_sources=[]
  if item['itemId'] in curated:
   title,variant=curated[item['itemId']];assert variant['artifactSha256']==item['artifactSha256'];original_modes=title['modes'];mode_sources=title['sourceIds']
  rec={'itemId':item['itemId'],'platform':item['platform'],'catalogName':item['name'],'catalogVisible':item['catalogVisible'],'artifactSha256':item['artifactSha256'],'contentSha256':item['contentSha256'],'editionMatch':status,'evidenceStatus':'research-only-not-verified-exact-content','databaseIdentity':identity,'identitySourceRefs':refs,'fields':fields,'documentedOriginalModes':original_modes,'modeSourceIds':mode_sources,'playersSimultaneous':None,'playersMode':None,'playersEvidenceStatus':'pending-exact-content-and-mode-verification','onlineApproved':False,'unknownFields':unknown,'blockers':blockers}
  records.append(rec);counts[status]+=1
 document={'schemaVersion':1,'purpose':'per-game-descriptive-research-not-production-authorization','catalogRevision':inventory['catalogRevision'],'sourceCommit':manifest['commit'],'sources':source_index,'metadataLicense':'CC-BY-SA-4.0; preserve attribution and upstream source headers; this derived metadata portion has the same license','records':records}
 dump(a.output/'catalog-game-metadata.json',document)
 report={'utc':datetime.now(timezone.utc).isoformat(),'catalogIds':len(records),'visibleIds':sum(r['catalogVisible'] for r in records),'editionMatches':dict(counts),'fieldCandidates':dict(field_counts),'upstreamRecordCounts':parse_counts,'sourceCommit':manifest['commit'],'sourceManifestSHA256':sha((a.sources/'source-manifest.json').read_bytes()),'inventorySHA256':sha((HERE/'catalog-inventory.json').read_bytes()),'outputSHA256':sha((a.output/'catalog-game-metadata.json').read_bytes()),'recipeSHA256':sha(Path(__file__).read_bytes()),'contentVerifiedIds':0,'allFactsComplete':False,'allIdsVisited':True,'onlineApprovedIds':0,'limits':['Exact filename/edition/size matches are candidate metadata; the installed bytes were not read.','CRC only links records within the pinned upstream dataset; it is not claimed as a verified hash of the catalog download.','Users/maxusers does not identify simultaneous versus alternating modes and is never used to approve seats.','DAT description is commonly the title and is not passed off as a synopsis. Missing synopsis/rating remain explicit.','Translations, hacks, region changes, archive variants and size mismatches remain unresolved, with no fuzzy propagation.']}
 dump(a.output/'metadata-coverage.json',report);print(json.dumps({k:report[k] for k in ['catalogIds','visibleIds','editionMatches','fieldCandidates','contentVerifiedIds','allFactsComplete','outputSHA256']},ensure_ascii=False))
if __name__=='__main__':main()
