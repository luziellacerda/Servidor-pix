# R77 — APP → SERVIDOR: candidata para salas de 2, 3 e 4 jogadores

Fonte imutável do aplicativo: `ba4fee556ec0010bbde1287746c68a2882cd1e39`, ramo `feat/station-multiplayer-r77-20261008`.
[Snapshot completo, receitas e evidências](https://github.com/luziellacerda/TurboElden/tree/ba4fee556ec0010bbde1287746c68a2882cd1e39/versions/station-multiplayer-r77-20261008).
Último retorno real do servidor analisado: `bee3dcd5c2c0c0a228805957fe89b9a6023e8402`.

Esta entrega é documental. As fontes C# são uma **candidata para revisão**, mantidas
sob `docs/`; não substituem `src/`, não implantam Linux nem reiniciam serviços.
Perfis continuam `approved:false`. Estado do APK/aparelhos e limites dos testes
devem ser lidos no handoff e nos recibos do commit acima.

Árvore documental nesta entrega: `docs/station-android/entrega-app-r77-20261008/`. O manifesto `DELIVERY-FILES.json`
amarra cada arquivo ao commit do aplicativo e ao pai do servidor. O operador deve
conciliar o contrato, cadastro e fontes com sua versão atual antes de ativar.
As receitas cruzadas Java dependem do snapshot completo do aplicativo referenciado
acima; esta cópia documental não é um projeto Android nem uma instalação do servidor.

---

# APP → SERVIDOR: candidata R77 — 08/10/2026

Este é um envio do app ao operador, não retorno do servidor nem comprovação de implantação.

## Base e coexistência

Último retorno real consultado: `bee3dcd5c2c0c0a228805957fe89b9a6023e8402`, branch `docs/station-r76-server-review-20261008`. A leitura do remoto durante a implementação não identificou resposta posterior. A produção declarada usa fonte `ab192bf1585e30f303d041f13b36a1f9c96d2caa`. A candidata 6f27, presente em árvores documentais mais recentes, ainda não foi qualificada pelo operador; a R77 não a incorpora por consequência.

A R76 permanece instalada nos dois aparelhos. Seus rs4/runtime `804b2acfea4c…` continuam válidos no caminho existente. Não substituir/remover os dez registros já publicados. A R77 muda runtime e core SNES e recebe novas identidades. Não instalar a candidata nos aparelhos em uso antes de preparar e conferir sua ativação coordenada.

## Contrato novo

`server/CONTRACT.json` e os fontes são a referência exata. Novas rotas fixas:

- `POST /v1/station/online/multiplayer/command`, com o mesmo domínio de assinatura do envelope, `requestId` vinculado e prova da sessão existente.
- `GET /v1/station/online/multiplayer/relay`, ticket de uso único/prova por participante, link, posição e geração; WebSocket `station-stream.v3`.

Comandos estritos: capabilities, snapshot, heartbeat, create, join, ready, start, leave, ticket, resume, failed e chat. Nenhum campo novo é enviado ao endpoint v2. Presença mantém a mesma identidade social; o serviço vinculado do app renova heartbeat v3 durante a partida. Bloqueios sociais existentes também impedem ingresso/exposição da sala v3; o bloqueio humano em sala compartilhada mantém a semântica de saída.

A capacidade é escolhida entre `allowedPlayerCounts`. `maximumPlayers` é o máximo daquele perfil online; `capacity` é a lotação escolhida; `players`/`roster` são os presentes reais. Cada membro tem posição explícita. Geração, perfil e roster são revistos antes de aceitar/iniciar. O conjunto de membros congela no início: não há ingresso tardio silencioso durante a partida.

## Identidades da candidata

- Runtime: `351cee4540e916468a504788911e4c5fcb4fe141e3b544b1b1255d32e1d04a26`.
- SNES core: `0a3ac7b4fa5da318b1c993d8867aa944eaa2d012680b49e9929bf7a4a398f527`.
- Mega Drive core preservado: `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69`.
- SNES ID: `bsnes-mercury-performance-79d7f9de-mt1-mp3-351cee4540e9`.
- Mega Drive ID: `clownmdemu-d43c2708-mp3-351cee4540e9`.
- Neo Geo continua `launchReady=false`; não há habilitação por esta entrega.

`assets/station-online/engines.json` registra opções/overlays. `PROFILE-IDENTITY.json` define SHA-256 do JSON canônico UTF-8 do perfil de controles, com ordem exata das chaves e sem newline/BOM. Conteúdo/core/runtime/item/modo são vinculados separadamente no perfil assinado. Não aceitar apenas um nome de perfil.

## Arquitetura e funções

| Componente | Responsabilidade |
|---|---|
| StationMultiplayerProfile | Perfil exato, hash canônico, modos, conjuntos de capacidades, slots e verificação da sala |
| StationOnlineGame.prepare/profiles | Catálogo, seleção de modo, SHA do arquivo realmente aberto e dos binários |
| StationOnlineClient | Endpoint versionado, identidade, instância/revisão, geração de ações e combinação com presença/conversa |
| StationRoomsActivity | Capacidade confirmada, nomes/posições, informações antes de aceitar/iniciar, confirmação invalidada por mudanças |
| StationGamePlayerInfo | Textos sem inferir quantidade, simultaneidade ou autorização a partir de metadado antigo |
| StationGameSession | Autoridade HTTP no serviço privado vinculado, heartbeat, tickets por link e saída humana |
| StationMultiplayerSession | Uma barreira nativa para N−1 canais; geração/época e identidade por ticket |
| StationMultiplayerTunnel/Wire | Escritor único, filas limitadas, bytes ordenados, crédito/replay, suspensão/retomada e rejeição de callbacks antigos |
| StationRetroLaunch/Activity | Controle solicitado explicitamente por posição, configuração privada online e JNI antes do início |
| station_multiplayer.h/netplay_frontend.c | Liga porta TCP local ao slot autorizado e exige máscara completa; sem duas pessoas no mesmo controle |
| Core SNES inputPoll | Mapeia os controles adicionais do Multitap antes do antigo limite id>11 |
| StationMultiplayer/Stream/Endpoints C# | Aprovação, lotação, roster, tickets, links isolados, barreira global, orçamento e autenticação |
| StationCatalogPlayerEvidence/ponte do carrossel | Contagem factual da edição; sem confirmação mostra —, sem autorizar rede |

Todos os aparelhos emulam o jogo. O anfitrião distribui entradas e coordena estado; não executa o jogo sozinho pelos convidados. Três/quatro aparelhos aumentam tráfego e custo de sincronização. Preservados os parâmetros de latência existentes; não foi prometido zero atraso nem aplicada alteração experimental de input latency.

## Catálogo: entrega concreta e pendências

`catalog/catalog-inventory.json` cobre os 2.467 IDs da revisão publicada 14 (2.212 visíveis). `catalog/catalog-game-metadata.json` cruza bases primárias mantidas e guarda fonte, linha, hash e estado de cada campo. São candidatos descritivos: 1.454 correspondências de edição/tamanho, 734 sem correspondência e 279 divergentes. Nenhuma sinopse, avaliação ou quantidade simultânea foi inventada para preencher ausência.

O operador deve publicar/confirmar `contentSha256` do arquivo efetivamente entregue ao emulador, separadamente do hash do ZIP/container. O cliente aceita o campo opcional do catálogo assinado/cache; a exibição factual exige o mesmo itemId, revisão e hash da evidência. Não propagar metadados para tradução/hack/região por similaridade de nome. `bind_content_hashes.py` é a receita de vinculação, não deve publicar ROMs nem BIOS.

O piloto documental `Super Bomberman 2 (USA)` tem item `station_df50d575815ab105084a79d68e0c8fb3`, SHA raw `0a4b4a783a7faf6ada3e1326ecf85de77e8c2a171659b42a78a1fae43f806ca6`. Batalha individual aceita 2/3/4 humanos; o modo Normal é individual. `server/proposed-game-profiles-r77.json` permanece **approved:false**. Confirmar artefato atual, quatro posições exclusivas e comportamento do modo antes de aprovar. Não usar o rótulo genérico Bomberman para os outros títulos.

Battletoads continua com divergência USA versus ESP/NTSC interno documentada; requer SHA do conteúdo descompactado. Secret of Mana requer aliados ingressados na aventura; o começo de um save não fornece imediatamente três personagens. Mega Bomberman ainda precisa fixar conteúdo/modo/adaptador. Neo Geo/Neo Geo CD não são liberados por este trabalho.

## Operação e qualificação necessárias

1. Revisar/compilar a candidata sobre a base indicada e conciliar separadamente mudanças do operador. Não substituir a árvore em produção por uma árvore documental antiga.
2. Testar em ambiente isolado as rotas assinadas, canais, modo/slots e recuperação; verificar todos os recibos e hashes da entrega.
3. Preencher os perfis e evidências por jogo/edição. Perfil descritivo não substitui aprovação de conteúdo, controles e determinismo.
4. Coordenar ativação com os jogadores. O estado é em RAM; recarga/restart depende do operador, nunca automática pelo app.
5. Confirmar capabilities v3/perfis na resposta autenticada fresca; não tratar HTTP200/cache/PONG como aprovação.
6. Validar sala nova 2/3/4: todos nomes/slots, ordem de entrada invertida, controles exclusivos, bloquear lotação não permitida, perda de rede de cada papel, segundo plano, retomada e saída humana. Depois fazer partida prolongada com FPS/áudio/latência/temperatura observados.
7. Devolver branch/commit/código ativo, registro efetivo, horário/PID/DLL e resultados separados de testes locais e gameplay físico.

As salas v3 deste candidato entram pela lista de salas. Convites/códigos v2 não são reutilizados com IDs v3. A preservação completa desse fluxo social é uma pendência declarada antes de substituir a versão em uso.

## Referências primárias

- [Libretro — múltiplos controles e exemplo Super Bomberman 2](https://docs.libretro.com/guides/netplay-multiple-controllers/).
- [Libretro — arquitetura, determinismo e protocolo netplay](https://docs.libretro.com/development/retroarch/netplay/).
- Manuais originais e fontes por título em `catalog/mode-evidence.json`.
- [Microsoft — SendAsync e concorrência por WebSocket](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket.sendasync?view=net-10.0).

Fontes e recibos entram no Git. APK, ROMs, BIOS, mídia privada, credenciais, sessões e logs pessoais ficam fora. O APK candidato em G: será identificado no recibo final; nenhuma instalação Android nem alteração Linux foi feita nesta entrega.

## Recibo final da candidata

APK compilado e assinado: **`14450f3aa52ca2c795b50afba7e5a75c5bd4f71aa0007d2c43c6542051bdb737`**, 2,122,993,964 bytes. Local: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R77-20261008.apk`. Certificado original preservado. Não instalada; nenhum servidor Linux alterado.

Java: 209 fontes (24 novas/alteradas, 185 preservadas). DEX28 `846f7f8556c42816a9465bf1338f8ebf0a29a57162b04e8e5645e1fc9b5c330a`; DEX35 `e301764df29325128695e087181091647648a20a6f7ad8c86589a12818e4496d`. Foram alteradas exatamente seis entradas do APK e acrescentado apenas `assets/station-catalog/player-evidence-v1.json`; 13,219 entradas e os 59 vídeos permaneceram idênticos. O asset de contagens revisadas permanece vazio: a implementação está pronta para receber vínculos comprovados, mas a pesquisa não foi promovida em massa a fato.

| Verificação local | Resultado |
|---|---:|
| Contrato/perfis/coordenador Java | 1.612 |
| Concorrência do túnel | 148 |
| Combinação de snapshots | 90 |
| Saída humana vinculada à sala/geração | 227 |
| Apresentação de jogadores | 188 |
| Métodos de interface extraídos | 87 |
| Evidência factual do carrossel | 175, mais 4.000 publicações concorrentes |
| Runtime nativo | 758 e 12 guardas |
| Mapeamento Multitap SNES | 6.609 |
| Servidor C#/HTTPS-WSS/v2 | 253 + 152 + 91 |
| Java ↔ C# HTTPS/WSS, 2/3/4 participantes | 9 sessões, 351 verificações, 25.560.000 bytes exatos |
| Inventário / metadados pesquisados | 12.361 / 73.525 |

A integração usa rede TLS local real e prontidão nativa simulada. Não executa jogos em Android nem mede latência de Internet. Consulte `tests/README.md`, `evidence/package.json`, `evidence/package-gates.json` e `STATUS.json`.
