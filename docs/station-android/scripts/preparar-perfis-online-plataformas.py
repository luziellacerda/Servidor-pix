#!/usr/bin/env python3
"""Prepare declarative online profiles without inferring approval from player labels.

Preserves every existing profile. New games need an exact launch identity and
engine binding. An optional mode file supplies explicit reviewed game modes;
otherwise a two-seat unapproved draft is emitted. No production file is changed.
"""
from pathlib import Path
import argparse, hashlib, json

NEW={'n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3'}
CEILINGS={'snes':5,'megadrive':2,'dreamcast':4,'n64':4,'gamecube':4,'wii':4,'wiiu':4}
def load(path,limit=32*1024*1024):
    raw=path.read_bytes()
    if len(raw)>limit:raise ValueError('Bounded JSON input required')
    return json.loads(raw)
def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','engines','existing','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--modes',type=Path);args=p.parse_args()
    catalog=load(args.catalog);engines=load(args.engines);old=load(args.existing)
    if not isinstance(old,list) or len(old)>16000 or len(catalog['items'])>4096:raise ValueError('Profile/catalog bounds exceeded')
    modes=load(args.modes) if args.modes else [];byid={}
    for mode in modes:byid.setdefault(mode['itemId'],[]).append(mode)
    prepared=[];missing=[]
    for row in catalog['items']:
        system=row['platform']
        if system not in NEW:continue
        candidates=[e for e in engines['engines'] if e['platform']==system and e.get('launchReady') and e.get('recoveryProtocol')=='station-stream.v3']
        if len(candidates)!=1:missing.append({'itemId':row['itemId'],'reason':'engine-not-implemented'});continue
        engine=candidates[0]
        if not row.get('contentSha256'):missing.append({'itemId':row['itemId'],'reason':'exact-content-identity-required'});continue
        ext=Path(row['artifact']['launchPath']).suffix[1:].lower()
        if ext not in engine['extensions']:missing.append({'itemId':row['itemId'],'reason':'format-not-supported-by-online-engine'});continue
        defaults=[dict(profileId='online-mode-draft-v1',maximumPlayers=2,allowedPlayerCounts=[2],approved=False,mode='two-player',modeTitle='Modo multijogador a confirmar',instructions=['Escolha o modo multijogador local do jogo. A campanha pode aceitar menos pessoas.','Vagas da sala são controles de jogo; Ver detalhes não transmite a partida.'],sources=[])]
        for mode in byid.get(row['itemId'],defaults):
            maximum=mode['maximumPlayers'];counts=mode['allowedPlayerCounts']
            if type(maximum)!=int or not 1<=maximum<=CEILINGS.get(system,2):raise ValueError('Mode exceeds platform ceiling')
            if not isinstance(counts,list) or counts!=sorted(set(counts)) or any(type(n)!=int or not 2<=n<=maximum for n in counts) or (maximum==1 and counts) or (maximum>1 and (not counts or counts[-1]!=maximum)):raise ValueError('Invalid exact mode counts')
            if type(mode['approved'])!=bool:raise ValueError('Boolean approval required')
            if mode['approved'] and any(mode.get(k)!=v for k,v in dict(contentSha256=row['contentSha256'],engineId=engine['engineId'],coreSha256=engine['coreSha256'],runtimeSha256=engine['runtimeSha256']).items()):raise ValueError('Approval must bind the exact content and engine')
            controller=mode.get('controllerProfile','direct-four-ports-v1' if system=='n64' else 'standard-2p-v1')
            if controller=='direct-four-ports-v1' and system=='n64':devices=[1,1,1,1]
            elif controller=='psx-dualshock-2p-v1' and system=='psx' and maximum<=2:devices=[517,517]
            elif controller=='standard-2p-v1' and maximum<=2:devices=[1,1]
            else:raise ValueError('Unsupported controller/mode pairing')
            canonical=json.dumps(dict(schemaVersion=1,controllerProfile=controller,devices=devices,coreOptions=engine['options']),ensure_ascii=False,separators=(',',':'))
            result={k:engine[k] for k in ('engineId','coreSha256','runtimeSha256')}
            result.update(itemId=row['itemId'],contentSha256=row['contentSha256'],platform=system,controllerProfile=controller,profileSha256=hashlib.sha256(canonical.encode()).hexdigest())
            for key in ('profileId','maximumPlayers','allowedPlayerCounts','approved','mode','modeTitle','instructions','sources'):result[key]=mode[key]
            prepared.append(result)
    keys=set()
    for profile in old+prepared:
        key=tuple(profile[k] for k in ('itemId','contentSha256','engineId','coreSha256','runtimeSha256','profileId','profileSha256'))
        if key in keys:raise ValueError('Profile already present; do not append twice')
        keys.add(key)
    args.output.mkdir(mode=0o700,exist_ok=False)
    for name,value in [('profiles-candidate.json',old+prepared),('new-profiles.json',prepared),('missing.json',missing),('receipt.json',dict(previousProfilesPreserved=len(old),newProfiles=len(prepared),newApproved=sum(p['approved'] for p in prepared),missing=len(missing),metadataNeverAuthorizesSeats=True,productionChanged=False))]:
        (args.output/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(previousProfilesPreserved=len(old),newProfiles=len(prepared),newApproved=sum(p['approved'] for p in prepared),missing=len(missing),productionChanged=False)))

if __name__=='__main__':main()
