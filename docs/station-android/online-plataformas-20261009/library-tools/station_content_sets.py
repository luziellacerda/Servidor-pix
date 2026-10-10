"""Portable offline identity for CUE tracks and complete Wii U packages.

This runs during preparation/publication, never during an HTTP download. The
container is independently verified by the existing R81 binder before this call.
"""
import base64, hashlib, re, stat, zipfile
from pathlib import PurePosixPath

SCHEMES={'cue-set-v1','wiiu-set-v1'}
MAX_CUE_BYTES=512*1024

def safe_path(value):
    if not isinstance(value,str) or not value or len(value)>4096 or any(ord(c)<32 for c in value) or ':' in value or '\\' in value or value.startswith('/') or any(p in ('','..','.') for p in value.split('/')):
        raise ValueError('unsafe content-set member')
    return value

def cue_references(raw):
    if len(raw)>MAX_CUE_BYTES:raise ValueError('content-set CUE too large')
    text=raw.decode('utf-8-sig')
    lines=[line for line in text.splitlines() if re.match(r'^\s*FILE\s+',line,re.I)]
    matches=[]
    for line in lines:
        match=re.fullmatch(r'\s*FILE\s+(?:"([^"\r\n]+)"|([^\s"\r\n]+))\s+\S+\s*',line,re.I)
        if not match:raise ValueError('unsupported CUE FILE statement')
        matches.append(safe_path(match.group(1) or match.group(2)))
    if not matches:raise ValueError('content-set CUE has no tracks')
    return sorted(set(matches),key=lambda name:name.encode('utf8'))

def canonical(launch,files):
    safe_path(launch)
    def encode(value):return base64.urlsafe_b64encode(value.encode('utf8')).decode().rstrip('=')
    lines=['TurboRamaStation/content-set/v1', 'launch='+encode(launch)]
    names=set()
    for name,size,sha in sorted(files,key=lambda row:row[0].encode('utf8')):
        safe_path(name)
        if name in names or type(size)!=int or size<0 or not re.fullmatch('[a-f0-9]{64}',sha):raise ValueError('invalid content-set file')
        names.add(name);lines.append(encode(name)+'\t'+str(size)+'\t'+sha)
    if launch not in names:raise ValueError('content-set launch missing')
    return ('\n'.join(lines)+'\n').encode('utf8')

def bind_set(row,entry):
    artifact=row['artifact'];launch=safe_path(artifact['launchPath'])
    scheme='wiiu-set-v1' if row['platform']=='wiiu' and launch.lower().endswith('.rpx') else 'cue-set-v1' if launch.lower().endswith('.cue') else None
    if scheme is None:return dict(entry)
    if artifact['format']!='zip':raise ValueError('multi-file identity requires a complete package')
    files=[]
    with zipfile.ZipFile(row['filePath']) as archive:
        members={}
        for member in archive.infolist():
            path=safe_path(member.filename[:-1] if member.is_dir() else member.filename)
            if path in members or stat.S_ISLNK(member.external_attr>>16) or member.flag_bits&1:raise ValueError('invalid content-set member')
            members[path]=member
        if launch not in members:raise ValueError('content-set launch missing')
        if scheme=='cue-set-v1':
            info=members[launch]
            if info.file_size>MAX_CUE_BYTES:raise ValueError('content-set CUE too large')
            references=cue_references(archive.read(info))
            parent=PurePosixPath(launch).parent
            selected=[(PurePosixPath(launch).name,launch)]+[(name,(parent/name).as_posix()) for name in references]
            canonical_launch=PurePosixPath(launch).name
        else:
            selected=[(name,name) for name,info in members.items() if not info.is_dir()]
            canonical_launch=launch
            if not all(any(name.startswith(prefix+'/') for name,_ in selected) for prefix in ('code','content','meta')):raise ValueError('incomplete Wii U content set')
        for name,path in selected:
            info=members.get(path)
            if info is None or info.is_dir():raise ValueError('content-set dependency missing')
            sha=hashlib.sha256();size=0
            with archive.open(info) as stream:
                while True:
                    block=stream.read(1024*1024)
                    if not block:break
                    size+=len(block);sha.update(block)
            if size!=info.file_size:raise ValueError('content-set dependency size mismatch')
            files.append((name,size,sha.hexdigest()))
    return dict(entry,contentSha256=hashlib.sha256(canonical(canonical_launch,files)).hexdigest(),contentIdentityScheme=scheme)
