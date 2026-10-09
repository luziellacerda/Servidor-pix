#!/usr/bin/env python3
"""Prepare every remaining catalog item before its APK engine exists.

Reuse documented original modes; two seats are the maintainer's default policy,
not a claim of original-game or Android compatibility. Special controls have
separate layouts and can never bind to ordinary pads by accident.
"""
import argparse
import json
from pathlib import Path
from station_online_profiles import NATIVE_CONTROLLERS, validate_modes

POLICY='https://github.com/luziellacerda/Servidor-pix/blob/feat/station-online-all-platforms-20261009/docs/station-android/HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md'
MELEE='https://www.nintendo.com/en-gb/Games/Nintendo-GameCube/Super-Smash-Bros-Melee-268951.html'
GALAXY='https://www.nintendo.com/en-gb/Games/Wii/Super-Mario-Galaxy-283322.html'
GALAXY2='https://m1.nintendo.net/docvc/RVL/EUR/SB4P/SB4P_E.pdf'
POKEMON='https://app-pcm.pokemon-support.com/hc/en-us/articles/360053180271--How-many-players-can-play-the-game'

def prepare(catalog,review,sources):
    source_urls={s['id']:s['url'] for s in sources['sources']}
    reviewed={}
    for mode in review['items']:
        if mode.get('primaryConfirmed'):reviewed.setdefault(mode['itemId'],[]).append(mode)
    output=[]
    for item in catalog['items']:
        system=item['platform']
        if system not in NATIVE_CONTROLLERS or not item.get('catalogVisible',True):continue
        base=dict(itemId=item['itemId'],name=item['name'],platform=system,coverId=item['coverId'],contentSha256=item['contentSha256'],
                  controllerProfile=NATIVE_CONTROLLERS[system],approved=True,engineBinding='awaiting-real-apk-manifest',androidGameplayValidated=False)
        plans=reviewed.get(item['itemId'],[])
        if item['itemId']=='station_2dc88daf495305000e730a205a1f06b6':
            plans=[dict(mode='Versus',nativeMaximum=4,conditions=['Escolha VS MODE para duas a quatro pessoas. Os modos de aventura permanecem individuais.'],urls=[MELEE])]
        if item['itemId'] in ('station_474055661521b99e82e6d0b6aee72435','station_11800c292110c10da2354418e3483b29'):
            plans=[dict(mode='Co-Star',nativeMaximum=2,conditions=['O anfitrião controla Mario; o convidado controla o ponteiro/Co-Star. O segundo participante não é outro Mario.'],urls=[GALAXY if item['itemId'].startswith('station_474') else GALAXY2],controllerProfile='wii-co-star-v1')]
        if system=='switch':
            plans=[dict(mode='Individual',nativeMaximum=1,conditions=['Pokémon Café Mix/ReMix é individual. Esta edição não ganha modo de duas pessoas por abrir uma sala.'],urls=[POKEMON])]
        # Preserve known individual editions without advertising two controllers.
        name=item['name'].lower().replace('_',' ')
        individual=system=='gamecube' and any(title in name for title in ('wind waker','windwaker','twilight princess','twilightprincess',"luigi's mansion",'metroid prime','prince of persia','resident evil','terminator 3','needforspeedunderground2'))
        if not plans and (individual or system=='wii' and 'skyward' in name):
            plans=[dict(mode='Jogo local',nativeMaximum=1,conditions=['Nenhum modo com controles humanos independentes foi cadastrado para esta edição. O jogo local permanece disponível; um modo adicional pode ser cadastrado por dados.'],urls=[POLICY])]
        if not plans:
            plans=[dict(mode='Multiplayer local',nativeMaximum=2,conditions=['Use o modo multijogador local que ofereça controles independentes. Uma campanha individual não recebe outro personagem.',
                'Duas vagas são a política de uso autorizada pelo mantenedor. Não representam homologação deste jogo; modos adicionais podem ser cadastrados por dados.'],urls=[POLICY])]
        for position,plan in enumerate(plans):
            maximum=min(plan['nativeMaximum'],4 if system!='switch' else 2)
            controller=plan.get('controllerProfile',base['controllerProfile'])
            notes=list(plan['conditions'])
            # Shared eight-player and GBA-link modes have distinct native layouts;
            # never silently turn them into four ordinary gamepads.
            if plan.get('playStyle')=='shared-controller':
                maximum=4;controller='gamecube-shared-controller-v1';notes=['Esta sala limita o modo compartilhado a quatro participantes. O app deve distribuir os lados do controle explicitamente; não anunciar oito vagas.']+notes
            if item['itemId']=='station_211df505e8c566432c4ffeda71c84a82' and maximum>1:controller='gamecube-gba-link-v1'
            urls=plan.get('urls',[source_urls[key] for key in plan.get('sourceIds',[])])
            notes=[n for n in notes if not n.startswith('Plataforma sem motor online')]
            mode=dict(base,profileId='prepared-local-'+str(position+1)+'-20261009',controllerProfile=controller,maximumPlayers=maximum,
                allowedPlayerCounts=list(range(2,maximum+1)) if maximum>1 else [],mode='solo' if maximum==1 else 'local-multiplayer',
                modeTitle=plan['mode'],instructions=notes+['Cada vaga é uma entrada de controle. Ver detalhes não transmite gameplay.'],sources=urls)
            output.append(mode)
    document=dict(schemaVersion=1,maintainerPolicy='all-platforms-server-first-20261009',modes=output)
    validate_modes(document);return document

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','review','sources','output'):parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    result=prepare(*[json.loads(getattr(args,key).read_bytes()) for key in ('catalog','review','sources')])
    if args.output.exists():raise ValueError('fresh output required')
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');args.output.chmod(0o600)
    print(json.dumps(dict(items=len({m['itemId'] for m in result['modes']}),modes=len(result['modes']),fourPlayerModes=sum(m['maximumPlayers']==4 for m in result['modes']),productionChanged=False)))

if __name__=='__main__':main()
