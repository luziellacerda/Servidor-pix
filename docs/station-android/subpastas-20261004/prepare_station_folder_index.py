"""Produce a NEW index with presentation folders. Never write the input or guess platform roots."""
import argparse,hashlib,json,os
from pathlib import Path
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def unique_pairs(pairs):
 out={}
 for key,value in pairs:
  if key in out:raise ValueError('Duplicate JSON property')
  out[key]=value
 return out
def validate(parts):
 if not isinstance(parts,list):raise ValueError('folderPath must be an array')
 if len(parts)>8:raise ValueError('Maximum folder depth is 8')
 for part in parts:
  if not isinstance(part,str) or not part.strip() or len(part.encode('utf-16-le'))//2>80 or part in ('.','..') or any(ord(c)<32 or 127<=ord(c)<=159 or c in '/\\' for c in part):raise ValueError('Invalid folder name')
 return parts
def produce(source,output,mappings,replace=False):
 source=Path(source).resolve(strict=True);output=Path(output).resolve()
 if source==output or output.exists():raise ValueError('Choose a new output file; existing files are never replaced')
 roots={}
 for value in mappings:
  key,sep,value=value.partition('=')
  if not sep or not key or key in roots:raise ValueError('Use unique platform=absolute-root mappings')
  raw=Path(value)
  if not raw.is_absolute():raise ValueError('Platform root must be absolute')
  root=raw.resolve(strict=True)
  if not root.is_dir():raise ValueError('Platform root is not a directory')
  roots[key]=root
 source_bytes=source.read_bytes();source_sha=hashlib.sha256(source_bytes).hexdigest()
 data=json.loads(source_bytes.decode('utf8'),object_pairs_hook=unique_pairs);changes=0;matched={k:0 for k in roots}
 if not isinstance(data.get('items'),list):raise ValueError('Index has no items array')
 before=json.loads(json.dumps(data))
 for row in data['items']:
  if row['platform'] not in roots:continue
  matched[row['platform']]+=1
  if 'folderPath' in row and not replace:validate(row['folderPath']);continue
  raw=Path(row['filePath'])
  if not raw.is_absolute():raise ValueError('Indexed file path is not absolute')
  path=raw.resolve(strict=True)
  if not path.is_file():raise ValueError('Indexed file is not a regular file')
  relative=path.relative_to(roots[row['platform']]);parts=validate(list(relative.parent.parts))
  if row.get('folderPath',[])!=parts:row['folderPath']=parts;changes+=1
 if any(v==0 for v in matched.values()):raise ValueError('At least one platform mapping matched no items')
 if changes:
  if not isinstance(data.get('revision'),int) or isinstance(data['revision'],bool) or data['revision']<1:raise ValueError('Index revision must be a positive integer')
  data['revision']+=1
 for a,b in zip(before['items'],data['items']):
  assert {k:v for k,v in a.items() if k!='folderPath'}=={k:v for k,v in b.items() if k!='folderPath'}
 if digest(source)!=source_sha:raise ValueError('Input changed while preparing candidate')
 with output.open('x',encoding='utf8',newline='\n') as f:json.dump(data,f,ensure_ascii=False,indent=2);f.write('\n')
 return {'inputSha256':source_sha,'outputSha256':digest(output),'items':len(data['items']),'changedItems':changes,'matchedByPlatform':matched,'revisionBefore':before.get('revision'),'revisionAfter':data.get('revision'),'idsFilesCoversAndDescriptorsPreserved':True,'deployed':False}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--input',required=True);p.add_argument('--output',required=True);p.add_argument('--platform-root',action='append',required=True);p.add_argument('--replace-folder-path',action='store_true');p.add_argument('--receipt',required=True);a=p.parse_args()
 receipt=Path(a.receipt)
 if receipt.exists() or receipt.resolve() in {Path(a.input).resolve(),Path(a.output).resolve()}:p.error('Receipt must be a separate new file')
 result=produce(a.input,a.output,a.platform_root,a.replace_folder_path)
 with receipt.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
 print(json.dumps(result,indent=2))
