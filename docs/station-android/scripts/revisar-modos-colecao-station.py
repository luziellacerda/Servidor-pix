"""Build the complete collection review and exact Bomberman mode data offline.

Inputs are public catalog metadata and published profiles. No ROM, private
configuration, service restart or unsupported player slot is created here.
"""
from pathlib import Path
from collections import Counter
import argparse, copy, csv, hashlib, json, re

OLD_RUNTIME='351cee4540e916468a504788911e4c5fcb4fe141e3b544b1b1255d32e1d04a26'
SB1='station_6e4c23a9931b4c2cf3449662605f43d7'
SB2='station_df50d575815ab105084a79d68e0c8fb3'
SB2BR='station_80b7594ba22922d87fb43d720ba8be5a'
SB3='station_46fe7356ab5cc63a1438f720b9c7ce2d'
M1='https://www.retrogames.cz/manualy/SNES/Super_Bomberman_-_SNES_-_Manual.pdf'
M2='https://www.videogamemanual.com/snes/Super%20Bomberman%202%20(USA).pdf'
M3='https://www.retrogames.cz/manualy/SNES/Super_Bomberman_3_-_SNES_-_Manual.pdf'

def read(path):return json.loads(Path(path).read_text())
def write(path,value):Path(path).write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','previous-review','bundle'):
        parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    catalog=read(args.catalog);items=catalog['items'];by_id={x['itemId']:x for x in items}
    previous={x['itemId']:x for x in read(args.previous_review)['items']}
    primary=read(args.bundle/'primary-mode-review.json')
    documented={};leads={x['itemId']:x for x in primary['researchLeads']}
    for row in primary['items']:
        if row['itemId'] not in by_id:raise ValueError('Documented mode does not match a catalog identity')
        documented.setdefault(row['itemId'],[]).append(row)
    current=read(args.bundle/'profiles.json');base=copy.deepcopy(current[:1816])
    old=[copy.deepcopy(p) for p in current if p['runtimeSha256']==OLD_RUNTIME]
    manifest=read(args.bundle/'engines-app.json');engines={e['platform']:e for e in manifest['engines']}
    runtime=engines['snes']['runtimeSha256']
    identity=lambda p:tuple(p[k] for k in ('itemId','engineId','profileId'))
    def add(item,mode,count,controller,title,instructions,source):
        key=(item,engines['snes']['engineId'].replace('-mp5-'+runtime[:12],'-mp3-'+OLD_RUNTIME[:12]),'snes-'+mode+'-'+item[-8:]+'-20261009')
        if any(identity(p)==key for p in old):return
        engine=engines['snes'];devices=[1,1] if controller=='standard-2p-v1' else [1,257,1,1,1]
        config=dict(schemaVersion=1,controllerProfile=controller,devices=devices,coreOptions=engine['options'])
        digest=hashlib.sha256(json.dumps(config,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()
        old.append(dict(itemId=item,contentSha256=by_id[item]['contentSha256'],platform='snes',engineId=key[1],
            coreSha256=engine['coreSha256'],runtimeSha256=OLD_RUNTIME,profileId=key[2],profileSha256=digest,
            maximumPlayers=count,approved=True,controllerProfile=controller,mode=mode,allowedPlayerCounts=list(range(2,count+1)),
            modeTitle=title,instructions=instructions,sources=[source]))
    add(SB1,'normal',2,'standard-2p-v1','Campanha em dupla',
        ['NORMAL MODE: campanha para uma ou duas pessoas. Na sala com dois, o segundo jogador pressiona Start após começar.',
         'Para três ou quatro pessoas, escolha o modo Batalha; a campanha não tem essas vagas.'],M1)
    add(SB1,'battle-single',4,'snes-multitap-port2-v1','Batalha: todos contra todos',
        ['BATTLE MODE: escolha MAN nas posições dos participantes P1 a P4; outros ficam COM.',
         'Três ou quatro pessoas funcionam somente na Batalha. A campanha permite no máximo duas.'],M1)
    add(SB3,'normal',2,'standard-2p-v1','Campanha em dupla',
        ['NORMAL GAME: escolha 2PLAYERS GAME para a campanha em dupla.',
         'Três, quatro ou cinco pessoas funcionam somente na Batalha. A campanha permite no máximo duas.'],M3)
    # Preserve the first 1816 published identities/values. Supplementary help can
    # be updated in the remaining data, and all new-runtime profiles carry it.
    for p in old[1816:]:
        if p['itemId'] in (SB2,SB2BR) and p['mode'].startswith('battle-'):
            notice='Neste jogo, o multiplayer funciona somente no modo Batalha. NORMAL GAME é para uma pessoa.'
            p['instructions']=list(dict.fromkeys((p.get('instructions') or [])+[notice]))
    new=[]
    help_by_key={(p['itemId'],p['mode']):p for p in current if p['runtimeSha256']==runtime}
    for p in old:
        clone=copy.deepcopy(p);engine=engines[p['platform']]
        clone['engineId']=engine['engineId'];clone['runtimeSha256']=runtime
        prior=help_by_key.get((p['itemId'],p['mode']))
        if prior:
            for key in ('modeTitle','instructions','sources'):
                if key in prior:clone[key]=copy.deepcopy(prior[key])
        if p['itemId']==SB3 and p['mode'].startswith('battle-'):
            clone.update(maximumPlayers=5,allowedPlayerCounts=[2,3,4,5])
            clone['profileId']=p['profileId'].replace('20261008','5p-20261009')
            clone['instructions']=[s.replace('P1 a P4','P1 a P5').replace('quinto pad COM/OFF','demais pads COM/OFF') for s in clone.get('instructions',[])]
            notice='Três, quatro ou cinco pessoas funcionam somente na Batalha. NORMAL GAME permite uma ou duas.'
            clone['instructions']=list(dict.fromkeys(clone.get('instructions',[])+[notice]))
        if p['itemId'] in (SB2,SB2BR):
            notice='Neste jogo, o multiplayer funciona somente no modo Batalha. NORMAL GAME é para uma pessoa.'
            clone['instructions']=list(dict.fromkeys((clone.get('instructions') or [])+[notice]))
        new.append(clone)
    profiles=old+new
    assert profiles[:1816]==base
    write(args.bundle/'profiles.json',profiles)
    write(args.bundle/'profiles-added-old-client.json',old[1816:])
    write(args.bundle/'profiles-new-runtime.json',new)
    inventory=[];review=[]
    for item in items:
        iid=item['itemId'];modes=[p for p in new if p['itemId']==iid]
        mode_views=[{k:p.get(k) for k in ('profileId','mode','modeTitle','allowedPlayerCounts','maximumPlayers','sources','instructions')} for p in modes]
        support=dict(itemId=iid,name=item['name'],platform=item['platform'],contentSha256=item.get('contentSha256'),
            catalogPlayerLabel=item.get('metadata',{}).get('players',''),onlineStatus='profiles-available' if modes else 'no-compatible-online-engine-profile',
            maximumOnlinePlayers=max((p['maximumPlayers'] for p in modes),default=0),modes=mode_views)
        inventory.append(support)
        prior=previous.get(iid,{})
        originals=documented.get(iid,prior.get('documentedOriginalModes',[]))
        numbers=[int(n) for n in re.findall(r'\d+',support['catalogPlayerLabel'])]
        maximum_hint=max(numbers,default=0)
        reasons=[]
        if maximum_hint>2:reasons.append('CATALOG_LABEL_MORE_THAN_TWO')
        if prior.get('referenceAdapters'):reasons.append('PRIMARY_EMULATOR_ADAPTER_REFERENCE')
        if any(p['maximumPlayers']>2 for p in modes):reasons.append('EXACT_STATION_MODE_MORE_THAN_TWO')
        if any(p.get('nativeMaximum',0)>2 for p in originals):reasons.append('DOCUMENTED_ORIGINAL_MODE_MORE_THAN_TWO')
        if iid in leads:reasons.append(leads[iid]['reason'])
        # Empty labels can hide candidates. The review retains such wording as
        # a research lead, never as a controller-count authorization.
        synopsis=item.get('metadata',{}).get('description','')
        if re.search(r'(?:up to|hasta|até|jusqu.{0,2}|[3-9])\s*(?:[3-9]|ten|dez|diez)?\s*(?:players|jogadores|jugadores|joueurs)',synopsis,re.I):
            reasons.append('SYNOPSIS_MULTIPLAYER_HINT_REQUIRES_VERIFICATION')
        flags=list(prior.get('reviewFlags',[]))
        if not numbers:flags.append('PLAYER_LABEL_MISSING_OR_NONNUMERIC')
        if not originals:flags.append('EXACT_ORIGINAL_MODES_NOT_YET_DOCUMENTED')
        if reasons and not originals and not any(p.get('sources') for p in modes):flags.append('EXACT_SIMULTANEOUS_MODES_NOT_YET_DOCUMENTED')
        if originals and maximum_hint>max(p.get('nativeMaximum',0) for p in originals):flags.append('CATALOG_PLAYER_HINT_CONFLICTS_WITH_DOCUMENTED_LOCAL_MODES')
        review.append(dict(**support,catalogVisible=item.get('catalogVisible',True),coverId=item.get('coverId'),
            metadataMaximumHint=maximum_hint or None,candidateMoreThanTwo=bool(reasons),candidateReasons=reasons,
            documentedOriginalModes=originals,referenceAdapters=prior.get('referenceAdapters',[]),researchLead=leads.get(iid),
            referenceJoin=prior.get('referenceJoin'),reviewFlags=sorted(set(flags)),physicalPhoneGameplayVerified=False))
    write(args.bundle/'game-support-completo.json',dict(revision=24,items=inventory))
    write(args.bundle/'collection-review-completo.json',dict(schemaVersion=1,catalogRevision=24,items=review,
        limits=['All catalog identities reviewed; candidate hints are not verified simultaneous modes.',
                'Unknown labels/modes remain explicit; no additional online controller is granted from them.']))
    with (args.bundle/'game-support-completo.tsv').open('w',newline='') as f:
        out=csv.writer(f,delimiter='\t',lineterminator='\n');out.writerow(['itemId','name','platform','maximumOnlinePlayers','onlineStatus','modes'])
        for x in inventory:out.writerow([x[k] for k in ('itemId','name','platform','maximumOnlinePlayers','onlineStatus')]+[json.dumps(x['modes'],ensure_ascii=False)])
    with (args.bundle/'candidates-more-than-two.tsv').open('w',newline='') as f:
        out=csv.writer(f,delimiter='\t',lineterminator='\n');out.writerow(['itemId','name','platform','catalogVisible','catalogPlayerLabel','maximumOnlinePlayers','candidateReasons','reviewFlags'])
        for x in review:
            if x['candidateMoreThanTwo']:out.writerow([x[k] for k in ('itemId','name','platform','catalogVisible','catalogPlayerLabel','maximumOnlinePlayers')]+[','.join(x['candidateReasons']),','.join(x['reviewFlags'])])
    platforms=[]
    for platform in sorted({x['platform'] for x in review}):
        group=[x for x in review if x['platform']==platform]
        platforms.append(dict(platform=platform,items=len(group),visible=sum(x['catalogVisible'] for x in group),
            candidatesMoreThanTwo=sum(x['candidateMoreThanTwo'] for x in group),
            visibleCandidatesMoreThanTwo=sum(x['candidateMoreThanTwo'] and x['catalogVisible'] for x in group),
            exactOnlineMoreThanTwo=sum(x['maximumOnlinePlayers']>2 for x in group),
            missingPlayerLabels=sum(x['metadataMaximumHint'] is None for x in group),
            unknownOriginalModes=sum(not x['documentedOriginalModes'] for x in group),
            unknownSimultaneousModes=sum('EXACT_SIMULTANEOUS_MODES_NOT_YET_DOCUMENTED' in x['reviewFlags'] for x in group)))
    summary=dict(catalogItems=len(items),platforms=platforms,candidatesMoreThanTwo=sum(x['candidateMoreThanTwo'] for x in review),
        visibleCandidatesMoreThanTwo=sum(x['candidateMoreThanTwo'] and x['catalogVisible'] for x in review),
        documentedOriginalModeItems=sum(bool(x['documentedOriginalModes']) for x in review),
        candidateCountsAreResearchLeadsNotGameplayQualification=True,
        profiles=len(profiles),previousProfilesPreserved=1816,oldRuntimeAdditions=len(old)-1816,newRuntimeProfiles=len(new),
        maximumProfilesPerGame=max(Counter(p['itemId'] for p in profiles).values()),physicalFivePhoneGameplayVerified=False)
    write(args.bundle/'collection-review-summary.json',summary);print(json.dumps(summary))

if __name__=='__main__':main()
