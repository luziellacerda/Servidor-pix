"""Emit bounded documentary findings; no ROM bytes or server mutation."""
import argparse
import json
import sys
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1] / "TurboElden-git" / "audits" / "station-r132-online-mode-completion-20261011"
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("--audit-dir",type=Path,default=ROOT)
parser.add_argument("--exclude-research",type=Path,action="append",default=[])
args=parser.parse_args()
ROOT=args.audit_dir
excluded_ordinals=set()
for external_path in args.exclude_research:
    external=json.loads(external_path.read_bytes())
    if external.get("schemaVersion")!=1 or not isinstance(external.get("entries"),list):
        raise ValueError("External documentary input must have schema1/entries")
    excluded_ordinals.update(e["catalogOrdinal"] for e in external["entries"])
catalog = {r["catalogOrdinal"]: r for r in json.loads((ROOT / "matrix.json").read_text(encoding="utf-8"))["rows"]}
entries = []
def entry(ordinal, classification, sources, finding, counts=(), modes=(), reason=""):
    row = catalog[ordinal]
    value = dict(catalogOrdinal=ordinal, name=row["name"], platform=row["platform"],
                 classification=classification, primarySources=sources, finding=finding,
                 documentarySimultaneousCounts=list(counts), modes=list(modes))
    if reason:
        value["reason"] = reason
    entries.append(value)
def mode(profile, kind, title, counts, instructions, requirements=(), condition=""):
    return dict(profileId=profile, maximumPlayers=max(counts), allowedPlayerCounts=list(counts),
                mode=kind, modeTitle=title, instructions=instructions,
                adapterCompatible=True, requiredEngineOptions=list(requirements), configuredInputCondition=condition)
def nintendo_gc(slug):
    return "https://www.nintendo.com/en-gb/Games/Nintendo-GameCube/" + slug
def nintendo_n64(slug):
    return "https://www.nintendo.com/en-gb/Games/Nintendo-64/" + slug
def nintendo_wii(slug):
    return "https://www.nintendo.com/en-gb/Games/Wii/" + slug
for ordinal, slug, finding in [
    (3735,"The-Legend-of-Zelda-The-Wind-Waker-269039.html","A ficha oficial de GameCube identifica um jogador. O Tingle Tuner opcional usa outro hardware e não converte a campanha em quatro portas locais."),
    (3754,"The-Legend-of-Zelda-The-Wind-Waker-269039.html","A ficha oficial de GameCube identifica um jogador; o título duplicado do catálogo recebe a mesma conclusão documental, sem duplicar uma sala."),
    (3736,"The-Legend-of-Zelda-Twilight-Princess-269050.html","A ficha oficial de GameCube identifica um jogador na campanha."),
    (3752,"The-Legend-of-Zelda-Twilight-Princess-269050.html","A ficha oficial de GameCube identifica um jogador; o item distinto mantém sua edição/conteúdo próprios."),
    (3737,"Luigi-s-Mansion-268236.html","A ficha oficial de GameCube identifica um jogador."),
    (3740,"Metroid-Prime-268423.html","A ficha oficial de GameCube identifica um jogador. Não transferir o modo multiplayer de Metroid Prime 2 para o primeiro jogo."),
    (3745,"Resident-Evil-4-268665.html","A ficha oficial de GameCube identifica um jogador. Disco1 e Disco2 não são dois jogadores."),
    (3746,"Resident-Evil-4-268665.html","A ficha oficial de GameCube identifica um jogador; a divisão em discos não acrescenta assentos.")]:
    entry(ordinal,"single-player",[nintendo_gc(slug)],finding)
entry(3742,"single-player",["https://manuals.plus/m/9974a1642a5548f3ec76ead9b03f57757ff082c8b1f9889c67e84f947fa2c485"],
      "Manual original da Ubisoft para GameCube: um jogador e controle na porta1. Não há decisão multiplayer nova.")
for ordinal, slug, finding, counts in [
    (3738,"Mario-Kart-Double-Dash--268269.html","A ficha oficial declara até quatro jogadores simultâneos; mantém o modo existente de portas GameCube independentes, sem ativar LAN entre consoles.",[2,3,4]),
    (3749,"Star-Fox-Assault-268874.html","A ficha oficial declara até quatro jogadores simultâneos no modo local; campanha não recebe assentos adicionais.",[2,3,4]),
    (3750,"Super-Smash-Bros-Melee-268951.html","A ficha oficial declara até quatro jogadores simultâneos; mantém o modo existente de batalha local.",[2,3,4])]:
    entry(ordinal,"simultaneous",[nintendo_gc(slug)],finding,counts)
for ordinal, slug, title, kind, instructions in [
    (3741,"Pikmin-2-268544.html","Pikmin2: multijogador local", "local-2p",["Escolha o modo local de dois jogadores; a campanha individual não ganha um segundo jogador.","Cada participante usa uma porta GameCube independente; ambos precisam da mesma edição e conteúdo."]),
    (3747,"Sonic-Heroes-268742.html","Sonic Heroes: dois jogadores", "local-2p",["Escolha um modo de competição para dois jogadores; não selecionar três ou quatro assentos.","A edição DUB permanece vinculada ao conteúdo exato do catálogo; esta evidência documental não é teste dessa edição no telefone."]),
    (3755,"Need for Speed Underground2: dois", "Corrida local", "local-2p",["Use a corrida local para dois jogadores; não confundir campanha individual ou recursos LAN com este modo."])]:
    if ordinal == 3755:
        slug = "Need-For-Speed-Underground-2-268478.html"
    entry(ordinal,"simultaneous",[nintendo_gc(slug)],"A ficha oficial de GameCube informa um ou dois jogadores simultâneos, sem suporte documental a três ou quatro.",[2],
          [mode("review-"+str(ordinal)+"-local2-v1",kind,title,[2],instructions)])
sa2 = [nintendo_gc("Sonic-Adventure-2-Battle-268709.html"),"https://manuals.plus/m/25fee396fca2f1e899f5d0b83bb30d2076fcba00c82cd2b735ff1ea35f4887ad"]
entry(3748,"simultaneous",sa2,
      "Manual original SEGA distingue Battle de dois jogadores e Party Race de Chao com até quatro controles; o quatro não se aplica à campanha nem a todos os Battle. Importação de Chao por GBA é opcional.",[2,3,4],[
      mode("review-sa2-battle2-v1","battle-2p","Sonic Adventure2: Battle para dois",[2],["Escolha 2P Battle; ação, caça, tiro, kart e Chao Karate deste modo usam dois jogadores.","Três ou quatro jogadores pertencem ao modo Chao Party Race, não a este Battle."]),
      mode("review-sa2-chaorace4-v1","chao-party-race","Chao Party Race: até quatro",[2,3,4],["Escolha Chao Race em 2P Battle e o tipo Party Race, com dados de Chao disponíveis no Memory Card.","Conecte uma porta por participante; os jogadores impulsionam seu Chao com A.","Beginner Race é individual; Party Race não habilita campanha ou Battle de ação para quatro."])])
mp7 = [nintendo_gc("Mario-Party-7-268313.html"),"https://www.scribd.com/document/733198408/Mario-Party-7"]
entry(3739,"simultaneous",mp7,
      "Manual original Nintendo/Hudson: Party Cruise Battle Royale e Minigame Cruise aceitam até quatro, com um controle por pessoa; Control Room oferece Mic desligado. Oito e 4-Team Battle compartilham controles e continuam recusados.",[2,3,4],[
      mode("review-mp7-standard4-v1","party-cruise-4p","Mario Party7: Battle Royale até quatro",[2,3,4],["Use Party Cruise em Battle Royale ou Minigame Cruise comum; cada pessoa ocupa uma porta GameCube.","No Control Room, deixe Mic desligado: esse modo não depende de microfone.","Não selecione 4-Team Battle, Deluxe Cruise ou minigames de oito jogadores: o adaptador não divide um controle entre duas pessoas."])])
for ordinal in (3743,3744):
    entry(ordinal,"pending-primary-evidence",["https://www.gamesdatabase.org/Media/SYSTEM/Nintendo_GameCube//Manual/formated/Resident_Evil_-_Code_Veronica_X_-_2003_-_Capcom.pdf"],
          "Manual original Capcom para GameCube localizado, mas a ferramenta não permitiu extrair a página de quantidade. A política histórica genérica não é prova, e não foi criada sala para esta edição DUB.",
          reason="exact-GameCube-primary-player-page-not-readable-DUB-edition-not-reviewed")
entry(3751,"pending-primary-evidence",["https://worthplaying.com/article/2004/8/17/news/18671-terminator-3-the-redemption-ngcps2xbox-official-website-launched/"],
      "Comunicado original Atari reproduzido anuncia modo cooperativo também para GameCube. A classificação genérica solo é contradita, mas faltou ler fonte primária da contagem exata/portas desta edição; não inferir assentos de outras plataformas.",
      reason="GameCube-coop-announced-exact-primary-seat-count-not-extracted")
entry(3753,"unsupported-special-controller",[nintendo_gc("The-Legend-of-Zelda-Four-Swords-Adventures-269028.html")],
      "O multijogador usa Game Boy Advance e cabos de conexão; quatro controles GameCube comuns não implementam esse requisito.",[2,3,4],reason="gamecube-gba-link-v1-not-implemented-by-current-native-port-profile")
entry(3841,"single-player",[nintendo_wii("The-Legend-of-Zelda-Skyward-Sword-283531.html")],
      "Ficha oficial Wii: um jogador e Wii MotionPlus. Não há modo local de dois, três ou quatro para transformar em sala online.")
for ordinal, slug, finding in [
    (3842,"Mario-Kart-Wii-281848.html","Ficha oficial Wii: até quatro jogadores simultâneos no multiplayer local. Não usa o serviço Nintendo WFC encerrado; mantém o modo local já documentado."),
    (3843,"New-Super-Mario-Bros-Wii-282376.html","Ficha oficial Wii: até quatro jogadores na cooperação local. Mantém o modo local já documentado e os controles configurados pelo owner.")]:
    entry(ordinal,"simultaneous",[nintendo_wii(slug)],finding,[2,3,4])
entry(3839,"simultaneous",[nintendo_wii("Donkey-Kong-Country-Returns-280968.html"),"https://www.nintendo.com/en-gb/News/2010/Donkey-Kong-Country-Returns-a-rip-roaring-banana-scoring-treat-251997.html"],
      "Nintendo documenta cooperação local de Donkey Kong e Diddy para dois. Gestos de Wii Remote fazem parte do controle; o owner serializa estados configurados de movimento, sem comprovar os bindings físicos de cada telefone.",[2],[
      mode("review-dkreturns-coop2-v1","local-coop-2p","Donkey Kong Country Returns: dupla",[2],["Entre no modo cooperativo de Donkey Kong e Diddy: somente dois jogadores.","Use Wii Remote horizontal com os comandos de movimento configurados; a ação de sacudir é necessária para bater, rolar e soprar.","Esta aprovação documental de modo não comprova os bindings ou a partida física em cada aparelho."],condition="Wii Remote horizontal e gestos configurados no owner normal; estado de movimento transportado por WiimoteData")])
entry(3840,"pending-primary-evidence",["https://thewaltdisneycompany.com/news/creative-inspiration-behind-disney-epic-mickey-2-power-two/"],
      "Disney confirma cooperação e lançamento Wii. Não foi extraída nesta auditoria a página primária de controles/contagem da edição Wii. Não transferir o manual Xbox/PS3 ou o modo solo do port PC para o item Wii.",
      reason="exact-Wii-primary-mode-and-controller-review-not-extracted")
for ordinal, url in [(3844,nintendo_wii("Super-Mario-Galaxy-283322.html")),(3845,"https://m1.nintendo.net/docvc/RVL/EUR/SB4P/SB4P_E.pdf")]:
    entry(ordinal,"unsupported-special-controller",[url],
          "Dois participantes documentados pertencem ao Co-Star de assistente/pointer. Esse papel não é equivalente a duas portas comuns; o perfil Co-Star distinto não está homologado pelo owner atual.",[2],reason="wii-co-star-v1-role-and-input-profile-not-implemented")
# N64 primary findings supplied by the root reviewer, bound to exact ordinals.
entry(2095,"simultaneous",[nintendo_n64("Diddy-Kong-Racing-269448.html")],
      "Nintendo documenta Versus local de dois a quatro jogadores; não incluir aventura cooperativa ou recursos de versões posteriores.",[2,3,4],[
      mode("review-diddy-versus4-v1","versus","Diddy Kong Racing: Versus até quatro",[2,3,4],["Escolha Versus para dois, três ou quatro jogadores com controles N64 padrão independentes.","Não selecionar aventura cooperativa por inferência de outro lançamento."])])
entry(2097,"simultaneous",["https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_DonkeyKong64_EN.pdf"],
      "Manual Nintendo: Battle para dois a quatro controles; é desbloqueado após resgatar o primeiro Kong. Expansion Pak obrigatório, e a canonical do owner fornecido precisa conter expansionPak=true.",[2,3,4],[
      mode("review-dk64-battle4-v1","battle","Donkey Kong64: Battle até quatro",[2,3,4],["Resgate o primeiro Kong para desbloquear Battle; a aventura principal continua individual.","Use Battle com dois, três ou quatro controles N64 padrão.","Expansion Pak obrigatório; a preparação recusa owner cuja opção expansionPak=true esteja ausente."],requirements=["expansionPak=true"])])
entry(2114,"simultaneous",["https://www.nintendo.com/us/whatsnew/goldeneye-007-returns-for-nintendo-switch-online-expansion-pack-members/"],
      "Nintendo descreve o multiplayer original para dois a quatro. A decisão é para o jogo original catalogado e controles N64 padrão; não habilita configurações dual-controller.",[2,3,4],[
      mode("review-goldeneye-multi4-v1","multiplayer","GoldenEye007: multiplayer até quatro",[2,3,4],["Escolha o multiplayer original com dois, três ou quatro participantes.","Use o esquema padrão de um controle N64 por pessoa; não escolher configurações com dois controles por jogador."])])
entry(2125,"simultaneous",["https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_Kirby64TheCrystalShards_EN.pdf"],
      "Manual Nintendo: MINI-GAMES aceita controles2,3,4 e completa participantes com CPU; o modo de história é solo.",[2,3,4],[
      mode("review-kirby64-minigames4-v1","mini-games","Kirby64: minigames até quatro",[2,3,4],["Escolha MINI-GAMES com dois, três ou quatro controles; CPU pode completar vagas.","A aventura de história permanece individual."])])
for ordinal, slug in [(2140,"Mario-Party-269569.html"),(2138,"Mario-Party-2-269580.html"),(2139,"Mario-Party-3-269591.html")]:
    urls = [nintendo_n64(slug)]
    if ordinal == 2138:
        urls.append("https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioParty2_EN.pdf")
    modes = [mode("review-mp"+str(ordinal)+"-board4-v1","board","Mario Party: tabuleiro até quatro",[2,3,4],["Escolha o tabuleiro clássico com dois, três ou quatro humanos; CPU completa vagas.","Minigames seguem as equipes e controles do jogo; uma quantidade de participantes do tabuleiro não muda modos individuais ou Duel."])]
    if ordinal in (2138,2139):
        modes.append(mode("review-mp"+str(ordinal)+"-duel2-v1","duel-2p","Mario Party: Duel para dois",[2],["Escolha o modo de duelo para dois participantes; não aplicar três ou quatro ao duelo."]))
    entry(ordinal,"simultaneous",urls,"Nintendo documenta o tabuleiro clássico até quatro. Em Mario Party2/3 o duelo é separado para dois; a contagem de um modo não homologa todos os outros.",[2,3,4],modes)
entry(2165,"simultaneous",[nintendo_n64("Perfect-Dark-269635.html")],
      "Nintendo documenta Deathmatch de dois a quatro e exige Expansion Pak para três ou quatro. A decisão não inclui cooperação, Counter-Operative ou dual-controller sem evidência própria.",[2,3,4],[
      mode("review-perfectdark-dm4-v1","deathmatch","Perfect Dark: Deathmatch até quatro",[2,3,4],["Escolha Deathmatch com controles N64 padrão independentes.","Três ou quatro jogadores exigem Expansion Pak; a preparação verifica expansionPak=true.","Não habilitar cooperação ou configurações dual-controller por esta decisão."],requirements=["expansionPak=true"])])
entry(2170,"pending-primary-evidence",[nintendo_n64("Pokemon-Stadium-269679.html")],
      "Nintendo informa até quatro simultâneos, mas esta auditoria não extraiu o manual por modo. Não aplicar a mesma contagem a batalha, mini-games e Game Boy/Transfer Pak.",[2,3,4],reason="PokemonStadium-per-mode-primary-controller-and-seat-review-required")
entry(2169,"pending-primary-evidence",[],
      "Pocket Monsters Stadium2 japonês é outro item. Nome parecido não prova equivalência ao Pokémon Stadium ocidental ou aos mesmos modos/acessórios.",
      reason="Japanese-Stadium-edition-not-equated-to-western-PokemonStadium")
for ordinal, urls, title, counts, finding, instructions in [
    (2073,["https://www.nintendo.com/es-es/Juegos/Nintendo-64/1080-Snowboarding-269404.html"],"1080 Snowboarding: corrida para dois",[2],"Nintendo informa um ou dois simultâneos; o modo local de corrida não aceita três ou quatro.",["Escolha a corrida local 2P com um controle N64 padrão por jogador; modalidades de treino e manobras individuais continuam solo."]),
    (2103,[nintendo_n64("Excitebike-64-269470.html"),"https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_Excitebike64_EN.pdf"],"Excitebike64: corrida até quatro",[2,3,4],"Ficha e manual Nintendo confirmam corrida local e controles nas portas1,2,3,4; Expansion Pak é compatível, não obrigatório.",["Escolha a corrida para dois, três ou quatro jogadores; cada um ocupa uma porta N64 independente.","Não aplicar assentos adicionais a desafios individuais ou edição de pistas."]),
    (2174,[nintendo_n64("Ridge-Racer-64-269701.html")],"Ridge Racer64: corrida até quatro",[2,3,4],"Nintendo identifica modo simultâneo de um a quatro no lançamento Nintendo64 original.",["Use a corrida multiplayer local de dois, três ou quatro; a campanha individual permanece solo."]),
    (2167,[nintendo_n64("Pokemon-Puzzle-League-269657.html")],"Pokemon Puzzle League: batalha para dois",[2],"Nintendo descreve batalha de quebra-cabeça para dois simultâneos; não há suporte documental a três ou quatro.",["Escolha a batalha local contra outro jogador com duas portas N64 padrão.","Não confundir com Pokémon Stadium, Transfer Pak ou versões de Game Boy."]),
    (2143,[nintendo_n64("Mickey-s-Speedway-USA-269613.html")],"Mickey Speedway USA: Contest até quatro",[2,3,4],"Nintendo descreve Contest Mode para até quatro amigos simultâneos; Adventure é individual.",["Escolha Contest Mode para dois, três ou quatro participantes; Adventure não ganha jogadores adicionais."]),
    (2104,["https://throwbackentertainment.com/game/extreme-g/"],"Extreme-G: corrida até quatro",[2,3,4],"O atual titular Throwback identifica o lançamento N64 de1997 e corrida simultânea de dois a quatro; não aplicar esse modo a outra edição da série.",["Escolha a corrida multiplayer local de dois, três ou quatro usando portas N64 independentes.","Esta decisão cobre corrida; não homologa por inferência outros modos ou sequelas."]),
    (2105,["https://throwbackentertainment.com/game/extreme-g/"],"Extreme-G: corrida até quatro",[2,3,4],"Segundo item de Extreme-G conserva seu conteúdo exato do catálogo. A fonte do titular descreve o mesmo lançamento original; a decisão documental não comprova esta edição no telefone.",["Escolha a corrida multiplayer local de dois, três ou quatro com portas N64 independentes.","A edição deste item continua vinculada ao conteúdo catalogado; não é aprovação de gameplay físico."])]:
    entry(ordinal,"simultaneous",urls,finding,counts,[mode("review-"+str(ordinal)+"-local-v1","local-multiplayer",title,counts,instructions)])
golf_modes=[]
for key, title, counts in [
    ("stroke","Stroke",[2,3,4]),("skins","Skins Match",[2,3,4]),
    ("clubslots","Club Slots",[2,3,4]),("minigolf","Mini-Golf",[2,3,4]),("match","Match Game",[2])]:
    golf_modes.append(mode("review-mariogolf-"+key+"-v1","turn-based-"+key,"Mario Golf: "+title+" em turnos",counts,
        ["Escolha "+title+" com "+("dois participantes." if counts==[2] else "dois, três ou quatro participantes."),
         "Cada humano usa seu controle N64 independente; as tacadas são alternadas, não ação simultânea.",
         "Não selecionar Tournament, Get Character, Ring Shot, Speed Golf ou Training: são individuais."]))
entry(2136,"alternating-independent-ports",["https://www.nintendo.com/eu/media/downloads/games_8/emanuals/nintendo_8/Manual_Nintendo64_MarioGolf_EN.pdf"],
      "Manual Nintendo p.2 permite2,3,4 controles; p.3 distingue os modos. Stroke/Skins/ClubSlots/MiniGolf aceitam2–4 e Match Game somente2. As tacadas alternam entre controles independentes, compatíveis com as quatro portas nativas; não foram declaradas simultâneas.",modes=golf_modes)
entries[-1]["documentaryAlternatingIndependentPortCounts"]=[2,3,4]
for ordinal, slug in [(2197,"Super-Mario-64-269745.html"),(2129,"The-Legend-of-Zelda-Majora-s-Mask-269525.html"),(2227,"Yoshi-s-Story-269800.html")]:
    entry(ordinal,"single-player",[nintendo_n64(slug)],"A ficha oficial identifica um jogador para este jogo Nintendo64 original. Compartilhar uma sessão de tela não transforma a aventura em multijogador local.")
entry(2166,"pending-primary-evidence",[nintendo_n64("Pilotwings-64-269646.html")],
      "A ficha Nintendo Europe exibe1–4 simultâneos, sem nomear modo local, enquanto o catálogo histórico identifica aventura individual. Esse conflito impede transformar o campo isolado em aprovação; precisa de manual original por modo.",
      reason="primary-product-player-field-conflicts-with-existing-solo-classification-mode-manual-required")
entry(2198,"pending-primary-evidence",[nintendo_n64("Super-Smash-Bros-269756.html")],
      "Nintendo documenta até quatro para Super Smash Bros original, mas o item instalado é o mod Smash Remix. A fonte do jogo original não é evidência suficiente da edição modificada nem dos modos/limites que ela altera.",
      reason="SmashRemix-mod-specific-mode-and-edition-review-not-provided-by-original-game-source")
entry(2124,"simultaneous",["https://manuals.plus/m/f64ee64136eb8177ed9265521c37f63c63c5fc54bd191b1c9fb025a2685848f8.pdf"],
      "Manual original Nintendo/Rare p.8: Arcade é iniciado por START nos controles1 e2. Tournament de2–8 alterna participantes nos combates; não são oito portas ou oito lutadores simultâneos.",[2],[
      mode("review-kigold-arcade2-v1","arcade-2p","Killer Instinct Gold: Arcade para dois",[2],["Escolha Arcade e pressione START nos controles1 e2 para dois humanos.","Não criar sala de três ou quatro por causa de Tournament: os participantes desse torneio se revezam nos combates de dois."])])
entry(2082,"pending-primary-evidence",["https://www.nintendo.co.jp/n01/n64/software/nus_p_nb7j/index.html","https://news.xbox.com/en-us/2015/06/16/xbox-rare-celebrates-its-30th-anniversary-with-a-massive-30-game-collection/amp/"],
      "Nintendo identifica a edição japonesa Banjo to Kazooie no Daibouken2 e1–4; o titular confirma multiplayer no original Banjo-Tooie. Sem discriminar minigames/controles por modo, a aventura e os minigames não recebem uma aprovação comum.",
      reason="Japanese-BanjoTooie-primary-per-minigame-port-and-count-review-required")
entries[-1]["documentaryMaximumHumanPlayers"]=4
entry(2091,"pending-primary-evidence",["https://news.xbox.com/en-us/2015/06/16/xbox-rare-celebrates-its-30th-anniversary-with-a-massive-30-game-collection/amp/"],
      "O titular descreve o lançamento original de2001 com1–4 jogadores. Essa contagem geral não distingue os modos multiplayer nem seus esquemas de equipes/controles; a campanha individual não recebe quatro assentos.",
      reason="Conker-primary-per-multiplayer-mode-port-and-count-review-required")
entries[-1]["documentaryMaximumHumanPlayers"]=4
stadium_url="https://manualzz.com/doc/24022192/nintendo-64-game-pak-pok%C3%A9mon-stadium-instruction-booklet"
stadium_modes=[]
for key,title,counts,instruction,participation in [
    ("kidsclub-single","Kids Club: minigame avulso",[2,3,4],"Escolha um minigame avulso; cada humano entra com A no próprio controle. Bots não contam como humanos.","simultaneous"),
    ("kidsclub-series","Kids Club: sequência de minigames",[2,3,4],"Escolha a sequência de minigames até o número de vitórias definido; cada humano entra com A no próprio controle.","simultaneous"),
    ("freebattle-rental2","Free Battle: duelo Rental Only",[2],"Escolha Free Battle com dois humanos e Rental Only obrigatório; nenhum Pokémon importado de Game Boy.","alternating-independent-ports"),
    ("freebattle-tag-rental","Free Battle: Tag-Team Rental Only",[3,4],"Escolha Tag-Team/Anything Goes com três ou quatro humanos e Rental Only obrigatório; cada treinador comanda seu próprio time.","alternating-independent-ports")]:
    value=mode("review-stadium-"+key+"-v1",key,"Pokémon Stadium: "+title,counts,[instruction,"Use uma porta N64 independente por humano; Transfer Pak, GB Tower e troca de Pokémon ficam fora deste modo."],condition="Standard N64 independent ports; Rental Only mandatory in both Free Battle decisions; no Game Boy/Transfer Pak state")
    value["participation"]=participation
    stadium_modes.append(value)
for index,value in enumerate(entries):
    if value["catalogOrdinal"]==2170:
        entries.pop(index)
        break
entry(2170,"simultaneous",[stadium_url,"https://pkproject.net/juegos/manual/manual-N64-Pokemon-Stadium-EN.pdf"],
      "Manual Nintendo original, pp.4/14–15/28–32: controles2/3/4 explícitos; Kids Club sem Transfer Pak; Free Battle usa Rental Only do cartucho. Duelo2 e Tag-Team3/4 são decisões separadas. Batalhas alternam comandos de portas independentes; os minigames são simultâneos. Não alcança a edição japonesa2169.",[2,3,4],stadium_modes)
entries[-1]["documentaryParticipationByMode"]={value["mode"]:value["participation"] for value in stadium_modes}
entry(2086,"single-player",[nintendo_n64("Bomberman-Hero-276445.html")],
      "A ficha oficial Nintendo/Hudson identifica um jogador. Bomberman Hero não herda o Battle multiplayer de Bomberman64 ou de outro título da série.")
rage_url="https://www.ultra64.net/pdf/N64%20-%20Turok%20-%20Rage%20Wars%20%28Francais%29.pdf"
rage_modes=[]
for key,title in [("bloodlust","Bloodlust"),("frag-tag","Frag Tag"),("team-bloodlust","Team Bloodlust"),("capture-flag","Capture the Flag")]:
    rage_modes.append(mode("review-turokrage-"+key+"-v1",key,"Turok Rage Wars: "+title,[2,3,4],
        ["Escolha Multiplayer Mode / "+title+" para dois, três ou quatro humanos, um controle N64 por pessoa.",
         "Bots completam até quatro vagas totais; não contam como jogadores humanos."]))
rage_modes.append(mode("review-turokrage-trials2-v1","2-player-trials","Turok Rage Wars: Trials para dois",[2],
    ["Escolha 2 Player Trials: duas pessoas, controles independentes e missões próprias; não ampliar para três ou quatro.",
     "A documentação do modo não comprova a progressão desta edição em teste físico."]))
entry(2213,"simultaneous",[rage_url],
      "Manual original Acclaim, páginas digitais3/11–14: uma porta por jogador; Multiplayer aceita até quatro e distingue Bloodlust, Frag Tag, Team Bloodlust e Capture the Flag. 2 Player Trials usa somente dois. Bots compartilham o teto de quatro.",[2,3,4],rage_modes)
for ordinal in (2214,2215):
    entry(ordinal,"pending-primary-evidence",["https://www.gamesdatabase.org/Media/SYSTEM/Nintendo_N64//Manual/formated/Turok_2-_Seeds_of_Evil_-_1998_-_Acclaim_Entertainment.pdf"] if ordinal==2214 else [],
          "A revisão por modo Nintendo64 não foi concluída: o manual original localizado de Turok2 retornou404/403 e o de Turok3 não pôde ser lido. Não transferir modos/contagens dos remasters PC ou Switch para estas edições do catálogo.",
          reason="original-N64-manual-per-mode-count-and-controller-pages-not-readable")

entry(2092,"simultaneous",[nintendo_n64("Cruis-n-USA--276775.html")],
      "A ficha original Nintendo/Midway distingue corrida individual e tela dividida para dois. As quatro portas do console não ampliam esse modo para três ou quatro.",[2],[
      mode("review-cruisnusa-split2-v1","split-screen-2p","Cruis'n USA: corrida para dois",[2],["Escolha a corrida em tela dividida para dois jogadores, com um controle N64 por pessoa.","Não criar sala de três ou quatro: o modo documentado aceita somente dois."])])
for ordinal,url,finding in [
    (2081,"https://assets.topics.apps-jp.nintendo.com/uploads/files/2022/01/18/07/56/21/754200/41442_1999_02.pdf","Arquivo original Nintendo de fevereiro1999 identifica a edição japonesa de Banjo to Kazooie no Daibouken como jogo para uma pessoa. Não herda os minigames de Banjo-Tooie."),
    (2163,"https://assets.topics.apps-jp.nintendo.com/uploads/files/2022/01/27/02/26/12/869107/38704_2000_09.pdf","Arquivo original Nintendo de setembro2000 identifica Mario Story para Nintendo64 como jogo para uma pessoa. Os companheiros da aventura não são controles de outros humanos."),
    (2168,"https://assets.topics.apps-jp.nintendo.com/uploads/files/2022/06/08/07/17/36/786757/49246_1999_07.pdf","Arquivo original Nintendo de julho1999 identifica Pocket Monster Snap como jogo para uma pessoa. Compartilhar fotografias não implementa uma sala de duas ou mais pessoas.")]:
    entry(ordinal,"single-player",[url],finding)

entries=[r for r in entries if r["catalogOrdinal"] not in excluded_ordinals]
ROOT.mkdir(parents=True,exist_ok=True)
(ROOT / "web-mode-research.json").write_text(json.dumps(dict(schemaVersion=1,researchDate="2026-10-11",entries=entries,
    documentaryApprovalIsPhysicalGameplay=False,playerCountsInferredFromCeiling=False),ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(json.dumps(dict(entries=len(entries),modes=sum(len(r["modes"]) for r in entries))))
