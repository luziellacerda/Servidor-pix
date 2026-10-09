"""Apply the maintainer's explicit room policy to exact new engine bindings.

This authorizes use, not physical Android gameplay. Official FBNeo driver input
counts and documented game modes override the default two-seat policy. Known
solo games and unsupported sets do not receive multiplayer seats. Every result
is data consumed by preparar-perfis-online-plataformas.py; no per-game code.
"""
import argparse, json, re
from pathlib import Path

POLICY_SOURCE='https://github.com/luziellacerda/Servidor-pix/blob/feat/station-online-all-platforms-20261009/docs/station-android/HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md'
NEW={'n64','neogeo','neogeocd','psx','fbneo','cps1','cps2','cps3'}
PSX_TWO_CONTROLLER_TITLES={
    'CTR - Crash Team Racing','Capcom vs. SNK - Millennium Fight 2000 Pro','Crash Bash','Crash Bash (USA)',
    'Digimon Rumble Arena','Dragon Ball Z - Idainaru Dragon Ball Densetsu','Dragonball GT - Final Bout','Dragonball Z - Ultimate Battle 22',
    'Fighting Force','Gran Turismo','Gran Turismo 2','Legend of Mana','Metal Slug X','Mortal Kombat 3','Mortal Kombat Trilogy',
    'Need for Speed III - Hot Pursuit','Street Fighter EX Plus Alpha','Street Fighter EX2 Plus','Tekken','Tekken 2','Tekken 3',
    "Tony Hawk's Pro Skater 4",'Twisted Metal','Wipeout 3','World Soccer Winning Eleven 2002 (Japan)',
}
N64_FOUR={
    'station_bb41e3a086a00c5a06b27ea6c82a336a':('Batalha', 'https://www.nintendo.com/es-es/Juegos/Nintendo-64/Bomberman-64-1204884.html', 'Escolha BATTLE. A campanha é individual; configure MAN nas vagas humanas e COM nas restantes.'),
    'station_84aeaba085789f28f1dff0de5b693aea':('VS ou Batalha','https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioKart64_EN.pdf','Para três ou quatro pessoas escolha VS ou BATTLE. MARIO GP permite no máximo duas e TIME TRIALS é individual.'),
    'station_de6b7625634abd137bcd0b0fad208e0c':('VS Battle','https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_FZeroX_EN.pdf','Escolha VS BATTLE para duas, três ou quatro pessoas. O GP é individual.'),
    'station_26b8097e976f990ca701b08d1fd413f9':('Exhibition em duplas','https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioTennis_EN.pdf','Escolha EXHIBITION e DOUBLES para quatro controles. TOURNAMENT é individual.'),
}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('catalog','engines','drivers','output'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--maintainer-authorized-two-seats',action='store_true',required=True)
    args=parser.parse_args();catalog=json.loads(args.catalog.read_bytes());engines=json.loads(args.engines.read_bytes());driver_doc=json.loads(args.drivers.read_bytes())
    if driver_doc['sourceCommit']!='95153da1f113c56735bd9a818171f908df628421':raise ValueError('Pinned driver source required')
    driver_candidates={}
    for row in driver_doc['drivers']:driver_candidates.setdefault(row['driver'],[]).append(row)
    drivers={name:rows[0] for name,rows in driver_candidates.items() if len({(r['players'],r['flags']) for r in rows})==1}
    result=[];summary={'authorizedUseIsNotPhysicalGameplay':True,'newModes':0,'documentedFourSeatModes':0,'soloClassifications':0,'unapprovedSets':[]}
    for item in catalog['items']:
        platform=item['platform']
        if platform not in NEW:continue
        matched=[e for e in engines['engines'] if e['platform']==platform and e.get('launchReady') and e.get('recoveryProtocol')=='station-stream.v3']
        if len(matched)!=1 or not item.get('contentSha256'):raise ValueError('Qualified unique engine/content binding required')
        engine=matched[0];maximum=2;approved=True;title='Multiplayer local: até dois controles';sources=[POLICY_SOURCE]
        notes=['Escolha o modo multijogador local dentro do jogo. Uma campanha individual não ganha um segundo personagem por abrir uma sala.',
               'Cada vaga controla uma entrada humana. Ver detalhes mostra a sala e não transmite a partida.',
               'Cadastro autorizado para testes; jogatina nos aparelhos ainda não homologada.']
        if item.get('metadata',{}).get('players')=='1':
            maximum=1;title='Modo individual';notes=['O cadastro desta edição indica uma pessoa. Ela permanece disponível para jogar localmente.'];summary['soloClassifications']+=1
        if platform=='psx' and item['name'] not in PSX_TWO_CONTROLLER_TITLES:
            maximum=1;title='Jogo local: sem modo de dois controles cadastrado';notes=['Esta edição permanece disponível para jogo local. A liberação de sala depende de um modo com duas entradas humanas distintas.'];summary['soloClassifications']+=1
        if platform=='psx' and item['name']=='Worms Armageddon':
            maximum=2;approved=False;title='Turnos: mapeamento de controle pendente';notes=['Modos por turnos podem compartilhar um controle. É necessário definir a passagem do controle antes da sala online.']
        if platform=='psx' and item['name'] in ('Gran Turismo','Gran Turismo 2'):
            notes[0]='Escolha ARCADE e 2 PLAYER BATTLE. No Gran Turismo 2, a edição/disco ARCADE é necessária; SIMULATION não oferece este modo.'
        if platform in ('neogeo','fbneo','cps1','cps2','cps3'):
            driver=drivers.get(Path(item['artifact']['launchPath']).stem)
            if not driver or 'BDF_GAME_WORKING' not in driver['flags']:
                approved=False;notes=['Este conjunto precisa de um driver compatível com o arquivo instalado. O download e o modo local permanecem disponíveis.'];summary['unapprovedSets'].append(item['itemId'])
            else:
                maximum=min(maximum,driver['players'],2)
                sources=['https://github.com/finalburnneo/FBNeo/blob/'+driver_doc['sourceCommit']+'/'+driver['source']]
                notes[0]='Use Start/Coin de cada participante e escolha o modo do arcade. O Station libera no máximo duas entradas nesta plataforma.'
                if maximum==1:title='Arcade com uma entrada humana';notes=['O driver desta edição possui somente uma entrada humana.'];summary['soloClassifications']+=1
        if item['itemId'] in N64_FOUR:
            title,source,note=N64_FOUR[item['itemId']];maximum=4;sources=[source];notes=[note]+notes[1:];summary['documentedFourSeatModes']+=1
        # A previously observed incomplete private set is retained for download,
        # but must not advertise a working native online bootstrap.
        if platform=='neogeo' and Path(item['artifact']['launchPath']).name=='aof2.zip':
            approved=False;notes=['O conjunto existente está incompleto para este driver. Substitua-o por sua cópia completa; o importador conserva o item e as capas.'];summary['unapprovedSets'].append(item['itemId'])
        counts=list(range(2,maximum+1)) if maximum>1 else []
        if item['itemId']=='station_26b8097e976f990ca701b08d1fd413f9':counts=[2,4]
        mode={k:engine[k] for k in ('engineId','coreSha256','runtimeSha256')}
        mode.update(itemId=item['itemId'],contentSha256=item['contentSha256'],profileId='local-mode-20261009',maximumPlayers=maximum,allowedPlayerCounts=counts,approved=approved,
                    mode='solo' if maximum==1 else 'local-multiplayer',modeTitle=title,instructions=notes,sources=sources)
        result.append(mode)
    summary['newModes']=len(result)
    if args.output.exists():raise ValueError('Fresh mode output required')
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');args.output.chmod(0o600)
    print(json.dumps(summary),flush=True)

if __name__=='__main__':main()
