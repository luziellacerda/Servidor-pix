# Handoff único: fechar jogos, modos e controles de 1 a 4 pessoas no Station

**SERVIDOR → PRODUÇÃO DO APP · 08/10/2026.** Pedido do mantenedor: entregar o conjunto completo para concluir, sem um novo handoff por jogo. Este documento é a entrada única; os JSON/TSV vinculados são seus anexos executáveis. O escopo é TurboRamaStation e suas rotas Station.

## 1. Resultado entregue e ordem de execução

- Inventário dos **3.734 IDs**, com **3.479 jogos visíveis e 255 IDs de compatibilidade**, distribuídos pelas 12 plataformas do servidor. Nenhum ID foi omitido. Cada registro contém nome, plataforma, capa do próprio item, artefato, arquivo de lançamento, hashes disponíveis, admissão atual, referências e próxima ação.
- Lista completa de **444 candidatos a três/quatro pessoas**, sendo **248 em SNES/Mega Drive**. Inclui títulos por turnos e rótulos conflitantes, explicitamente marcados. Esses números não significam 444 jogos simultâneos homologados.
- Snapshot exato dos **1.816 perfis ativos**, mais **17 adições preparadas** com configurações que a R81 já entende. O arquivo combinado tem 1.833 perfis para revisão e publicação em lote. **As 17 adições deste pacote ainda não estão carregadas em produção.**
- Modos originais documentados para 12 itens/edições presentes, referências técnicas de adaptadores de 114 edições Mega e 245 SNES, e seis propostas de modos Mega que exigem trabalho no app/core.
- Reprodução real, usando fonte C fixada, de índices duplicados entre as duas portas do adaptador Sega. Patch isolado e teste estão anexos. Não é um core Android final e não demonstra a causa dos engasgos de Battletoads no SNES.

**A autorização do mantenedor para liberar os jogos compatíveis já existe.** Não solicitar nova aprovação genérica, não pedir que ele encontre hashes ou recibos técnicos e não desativar a liberação atual. O responsável pelo APK e o operador do servidor devem executar as tarefas abaixo, consolidar as evidências e devolver um único resultado final.

O inventário está completo. A classificação factual de todos os modos e o gameplay de quatro Androids ainda exigem as tarefas identificadas por item; este documento não declara esses trabalhos executados.

## 2. Outros jogos: nomes, número de pessoas e situação real

### Modos com manual original conferido

| Jogo presente | Pessoas no modo | Controle / configuração | Execução para concluir |
|---|---|---|---|
| Secret of Mana, SNES BR | 2 ou 3; solo também existe | Multitap SNES na porta 2 | Perfil de cooperação preparado; recrutar aliados e usar Start para entrar. Conferir a tradução/header na edição entregue. [Manual Nintendo, seção 12](https://www.nintendo.com/es-es/games/oms/snes-classic/manuals/secret-of-mana/manual.pdf). |
| Top Gear 3000, SNES e BR | VS: 2, 3 ou 4; campeonato: 2 | VS com Multitap; campeonato com dois pads | Ambos os modos preparados para as duas edições. Entrar em VS para três/quatro. [Manual original](https://www.gamingalexandria.com/snes/Top%20Gear%203000/Top%20Gear%203000%20-%20Manual.pdf). |
| NBA Jam, SNES | 2, 3 ou 4 na partida | Super Multitap para três/quatro | Perfil de partida preparado; cada humano confirma sua posição. [Manual original, páginas impressas 2–3](https://www.retrogames.cz/manualy/SNES/NBA_Jam_-_SNES_-_Manual.pdf). |
| Super Bomberman 3, SNES | Battle permite até 5 nativos; Station: 2, 3 ou 4 | Multitap SNES porta 2 | Single e Tag preparados; configurar humanos MAN e quinto pad COM/OFF. [Manual original, páginas 8–9](https://www.retrogames.cz/manualy/SNES/Super_Bomberman_3_-_SNES_-_Manual.pdf). |
| Super Bomberman 2, SNES e BR | Battle: 2, 3 ou 4; Normal: solo | Multitap no Battle | USA Battle Single já ativo; Tag e classificação solo preparados; BR tem vínculos separados. [Manual Hudson](https://www.videogamemanual.com/snes/Super%20Bomberman%202%20(USA).pdf), [documentação libretro](https://docs.libretro.com/guides/netplay-multiple-controllers/). |
| Mega Bomberman, Mega Drive | Battle Normal/Tag: até 4; campanha individual | Sega Team Player | Modos registrados; concluir layout de porta e controles do core antes de publicar estes novos perfis. [Manual Sega, páginas 3 e 13–14](https://www.gamesdatabase.org/Media/SYSTEM/Sega_Genesis//Manual/formated/Mega_Bomberman_-_1994_-_Sega.pdf). |
| General Chaos, Mega Drive | 2 em duelo ou cooperação; modos de 3 e 4 | EA 4-Way Play, pad de 3 botões | Modos registrados; atender à exigência de três botões e confirmar entrada por pad. [Manual EA, páginas 7–9](https://www.gamesdatabase.org/Media/SYSTEM/Sega_Genesis//Manual/formated/General_Chaos_-_1993_-_Electronic_Arts.pdf). |
| Battletoads in Battlemaniacs, SNES e BR | 2; modos A/B | Dois pads padrão | Dois modos preparados por edição, diferenciando golpes entre parceiros. Não oferece três/quatro. [Manual Tradewest](https://www.retrogames.cz/manualy/SNES/Battletoads_in_Battlemaniacs_-_SNES_-_Manual.pdf). |

### Mais títulos já localizados no catálogo e nas referências de adaptadores

**SNES/SNES BR:** NBA Jam Tournament Edition, NBA Hang Time, Looney Tunes B-Ball, Saturday Night Slam Masters, Smash Tennis, International Superstar Soccer Deluxe, Capcom's Soccer Shootout, Barkley Shut Up and Jam!, FIFA International Soccer, FIFA Soccer 96/97, NBA Live, Madden NFL, NHL, WWF Raw, Super Bomberman, Micro Machines, Street Racer, The Peace Keepers, FireStriker, Tiny Toon Adventures: Wacky Sports Challenge e outros constantes da lista completa. O modo e a quantidade de humanos de cada edição precisam ser confrontados; adaptador encontrado não determina sozinho as vagas. [Fonte do mantenedor do core SNES9x/OpenEmu, fixada](https://github.com/OpenEmu/SNES9x-Core/blob/6b1495d48bdef85e04cb96f5050601b9c6be3d88/SNESGameCore.mm).

**Mega Drive/Mega BR:** NBA Jam/TE, NBA Hang Time, Gauntlet IV, The Lost Vikings, Yu Yu Hakusho, Dragon: The Bruce Lee Story, Columns III, Tiny Toon Adventures: Acme All-Stars, FIFA, General Chaos, Mega Bomberman, College Slam, Double Dribble, Mutant League Hockey, NFL, NBA Live, NHL, Wimbledon, ATP Tour e outros. As referências distinguem Team Player na porta 1, pad na porta 1/adaptador na porta 2, dois adaptadores e EA 4-Way. As variantes J-Cart possuem tarefa própria. [Fonte do mantenedor do Genesis Plus/OpenEmu, fixada](https://github.com/OpenEmu/GenesisPlus-Core/blob/1d652d0593453a8d477214ba6a53a12d2c15e9e3/GenPlusGameCore.m).

**Todos os nomes e IDs, sem “outros” omitidos:** [games-3a4.md](online-1a4-completo-20261008/games-3a4.md), [games-3a4.tsv](online-1a4-completo-20261008/games-3a4.tsv). Para todos os jogos, incluindo os que só têm rótulo 1/2 e os aliases, use [inventory.tsv](online-1a4-completo-20261008/inventory.tsv) e [inventory.json](online-1a4-completo-20261008/inventory.json).

## 3. Plataformas cobertas, sem confundir catálogo com motor online

| Plataforma no catálogo | Visíveis | Motor online do APK R81 |
|---|---:|---|
| SNES | 644 | bsnes Mercury corrigido; perfis v3 presentes |
| SNES BR | 191 | Mesmo motor SNES; identidade e perfil por edição |
| Mega Drive | 887 | ClownMDEmu; padrão 2p e protocolos Sega/EA, com limites abaixo |
| Mega Drive BR | 94 | Mesmo motor Mega; identidade e perfil por edição |
| Nintendo 64 | 157 | Ausente do manifesto online; integração necessária |
| Neo Geo | 189 | Geolith aparece, mas `launchReady:false`; catálogo ZIP e motor `.neo` ainda precisam de conversão/BIOS/identidade |
| Neo Geo CD | 50 | Ausente do manifesto online; integração necessária |
| Dreamcast | 243 | Ausente do manifesto online; motor local não comprova suporte nesta sala |
| CPS1 | 36 | Ausente do manifesto online; definir core, ROM set e variante de controles |
| CPS2 | 64 | Idem |
| CPS3 | 11 | Idem |
| FBNeo | 913 | Idem; confirmar drivers e variantes 2p/4p |
| **Total** | **3.479** | **1.816 visíveis com admissão SNES/Mega ativa; 1.663 de outras plataformas com tarefa específica** |

Os 255 IDs adicionais de compatibilidade são mantidos para clientes/downloads antigos; não devem reaparecer como salas duplicadas. Todas as plataformas têm linhas no inventário, mesmo quando falta motor online. Não extrapolar que um arcade de nome parecido é a mesma edição: o inventário marca, por exemplo, variantes que dizem “2 Players” mas receberam rótulo 1–4.

## 4. Baseline de produção para executar sobre a versão correta

| Componente | Estado conferido nesta rodada |
|---|---|
| Serviço | `turborama-station-api.service`, ativo, PID1536467, NRestarts0 |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-online-r81-20261008-db50a98/TurboRamaSuiteOnlineServer.dll` |
| Fonte DLL | `db50a980fcfe01a9b0fee65316f67f43c63ac11c` |
| DLL SHA256 | `cdf14b8067de415413c503de787c6d621c6e8f0466eb2cdd7c0eced8b5a619af` |
| Perfis efetivos | `/opt/turborama-station-online-r81-20261008-db50a98/online-profiles-authorized.json` |
| SHA256 dos perfis | `f3eb13fcb474edb5a1b549a3509aa47505765210b38d1f1dd81bc157b4ec555c`, 1.816 aprovados/carregados |
| Flags | `MultiplayerEnabled=true`, `MultiplayerLegacyCapacityGate=false` |
| Registro legado | Dez engines, SHA256 `a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843` |
| Índice privado efetivo | revisão20; SHA256 `b3d44224a0264f3a14104f031d417492017b1d7756d7256607a51833c243348c` |
| Export público deste pacote | `catalog-rev20.json`, SHA256 `f72aa52f53c15cc7f8ebc07eb7a456eae39a7b43fd7b7a435f1d7901aa77581b`; difere do índice privado por sanitização |
| Importador ativo/agendado | `/opt/turborama-station-library-r81-20261008-b472d8a/atualizar-biblioteca-station.py`; identidades persistentes |
| Domínio | `https://app.lzgames.com.br` |
| APK R81 de referência | fonte funcional `759d7ab4d479bbb9dca0426ad286ca1b4f0ce1e0`; conjunto Java completo `ad07c24dcded0f17933e19f45b7ffaafbce314a0` |
| APK SHA256 | `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6` |

O snapshot de perfis deste pacote tem os mesmos bytes do arquivo efetivo. O estado ativo está no [retorno R81](RETORNO-EXECUCAO-HANDOFF-R81-20261008.md). O documento antigo e scripts que esperam `b472d8a`/flags desligadas são históricos; não devem ser executados diretamente sobre esta sucessora.

A admissão genérica 2p foi autorizada pelo mantenedor para todos os títulos compatíveis. **Ela não comprova que um jogo individual tem dois personagens humanos.** Atualmente, somente o vínculo USA de Super Bomberman2 oferece três/quatro no registro carregado. A lista preparada não substitui esse fato.

## 5. Como o app deve obter as informações corretas

1. **Catálogo:** ler `GET /v1/station/catalog?metadata=1` pela sessão Station e prova HTTP já existentes; verificar o envelope assinado. Usar revisão global20; reconstruir metadados ao mudar a revisão global, mesmo se a revisão individual do item não mudou. Sem `metadata=1`, sinopse é omitida por contrato.
2. **Capa:** resolver exatamente o `coverId` recebido naquele `itemId`, usando a rota/grant de capa existente. Não juntar pela ordem da lista, pelo nome simplificado ou pelo diretório do HD. IDs iguais de capa podem representar compartilhamento intencional; IDs de jogo iguais nunca devem virar jogos diferentes.
3. **Download:** pedir grant pelo `itemId`; usar o descritor assinado para `fileName`, formato, `launchPath`, tamanho e recipiente. Manter retomada e acesso protegido. O SHA do ZIP/CHD não é o SHA do arquivo que o motor abre.
4. **Identidade online:** ler `contentSha256` do item/descritor qualificado. SNES/Mega têm 2.071 vínculos, 1.816 visíveis. Usar todos os bytes do arquivo de lançamento; não remover header, alterar região ou escolher o primeiro arquivo de uma pasta. Reutilizar a identidade qualificada/cache vigente; não recolocar validações completas e `testzip` no caminho do download.
5. **Capacidades:** fazer `POST /v1/station/online/multiplayer/command` com sessão, prova, `action:capabilities`, UUID novo e `itemId` exato. Conferir o envelope, `multiplayerVersion:3` e `capability:station-multiplayer.v3`. O servidor entrega somente os perfis daquele item, até 32; não chamar uma vez por jogo para carregar todo o catálogo.
6. **Perfil:** escolher explicitamente o `profileId` do modo e os campos assinados. Não derivar vagas de `metadata.players`, descrição, gênero ou adaptador sozinho. O contrato inclui item/content/engine/core/runtime/profile/hash, controller, mode, maximumPlayers e allowedPlayerCounts.
7. **Criar/entrar:** `create` inclui identidade completa e `capacity` permitida; `join` inclui os mesmos campos, `roomId` e `generation` atuais. Pronto é posterior à entrada; mostrar todos os nomes e slots recebidos. Start só do host, depois de todos prontos. Uma pessoa joga localmente; sala v3 exige ao menos duas para iniciar.
8. **Relay:** obter `ticket` ou `resume` com identidade completa, sala/generation/linkId. Abrir WSS em `/v1/station/online/multiplayer/relay`, subprotocolo `station-stream.v3`, `Authorization: StationRelay <ticket>` e `X-Station-Request-Proof` conforme cliente vigente. O ticket é de uso único; usar a credencial retornada, não a licença como ticket. Preservar pin, assinatura e vínculo do aparelho.
9. **Senha da partida:** usar `room.connectionPassword`, retornada somente ao membro, automaticamente no caminho nativo já existente. Não mostrar uma senha longa nem pedir para digitar palavra-passe. Isso não é o código de ativação do app.
10. **Reconexão:** preservar sala, geração, links, offsets, ACK e a barreira de todos. Perda de rede não é `leave`. Nunca forçar estado playing com membro sem sincronismo; sair voluntariamente precisa limpar UI/JNI/relay sem matar outros produtos.

O sidecar [mode-metadata-proposed.json](online-1a4-completo-20261008/mode-metadata-proposed.json) contém nomes em português e instruções por modo. **A API e a R81 ainda não publicam/consomem esse sidecar.** Integrar como informação assinada, vinculada ao item/content/mode e ao perfil selecionado; ele não pode conceder vagas. A autorização continua no perfil exato.

## 6. Trabalho completo no app, em uma integração

### APP-01 — Modos, quantidades e ajuda orientados por dados

Basear-se nas 209 fontes atuais da R81, não restaurar Activity/DEX antigos. Arquivos em `versions/station-online-readiness-r81-20261008/java/netplay-src/org/emulationstation/frontend/netplay/`:

- `StationMultiplayerProfile.java`: manter validação de engine/core/runtime/hash, múltiplos perfis e `profileId` explícito. Associar ajuda assinada ao modo certo. Classificação solo não concede sala; uma classificação factual não deve ser apresentada ao usuário como quatro só porque o adaptador contém cinco/oito pads.
- `StationRoomsActivity.java`: já possui escolha entre perfis e lista de vagas; completar labels/instruções vindos do servidor. Mostrar Corrida VS, Aventura cooperativa, Battle/equipes etc. `roomExplanation` atual só trata o caso Battle Single; expandir leitura de dados sem hardcode por jogo. Não exibir hashes nem nomes técnicos de adaptadores na navegação comum.
- `StationOnlineGame.java`: manter preparação pela edição/hash exatos e usar o perfil escolhido. Separar rótulo de catálogo, modos documentados e vagas autorizadas.
- `StationMultiplayerSession.java` e `StationRetroLaunch.java`: preservar slots/roster/links/controle próprio. `classifications` com solo e multiplayer juntos já existe; não fazê-la abortar todos os modos quando encontrar solo.

Publicar os 17 perfis de dados é compatível com a R81 e, isoladamente, não exige novo APK. Ajuda genérica, novos layouts de controles, convites e novos motores requerem integração e a compilação correspondente.

### APP-02 — Mega Drive: completar controles, não só aumentar vagas

Fonte atual do wrapper `Clownacy/clownmdemu-libretro@d43c2708b0a31c285ce16724b6c4a2e92af07346`; common `347ea31f9971eeaaa28c607e75f7156dd17db7bd`; core `88ef45a6585556e2247dd26b6c937d6b9fb4a12d`.

**Defeito reproduzido:** `ControllerManager_Read` envia cada porta física Sega a um adaptador cujo callback usa IDs 0–3; não acrescenta a base da porta. O wrapper passa esse ID diretamente a libretro. Na fixture da fonte original, 48 de 96 casos de botões consultam o pad da outra porta; com o patch isolado, zero. Standard2 e EA 4 preservam a propriedade dos controles nos casos exercitados. [Fonte core](https://github.com/Clownacy/clownmdemu-core/blob/88ef45a6585556e2247dd26b6c937d6b9fb4a12d/source/controller-manager.c), [wrapper](https://github.com/Clownacy/clownmdemu-libretro/blob/d43c2708b0a31c285ce16724b6c4a2e92af07346/source/libretro-interface.c), [recibo do teste](online-1a4-completo-20261008/mega-port-mapping-proof.json).

Concluir conjuntamente:

1. Corrigir índices por porta física. O [patch anexado](online-1a4-completo-20261008/clown-sega-port-offset.patch) resolve apenas a duplicação, em cópia isolada.
2. Implementar layouts distintos: Team Player porta1; pad porta1 + Team Player porta2; dois Team Players; EA 4-Way; J-Cart quando aplicável. `clownmdemu_input_protocol=sega` atual é global; não representa sozinho todos esses layouts.
3. Remapear os pads físicos para slots lógicos 0–3 exclusivos antes de `input_state`. Hoje Java/nativo aceitam apenas máscara baixa de quatro bits e slot→port direto. Para manter esse contrato, criar presets de mapeamento no core. Outra abordagem exige evolução assinada coordenada do contrato Java/JNI/servidor; não usar uma máscara com bits4–7 na v3 atual.
4. As referências OpenEmu de portas não são um mapa validado do ClownMDEmu. Determinar ordem por edição/modo, incluindo distribuição entre equipes e jogadores da segunda porta. Mega Bomberman tem divergência entre diagrama do manual e tabela técnica; conferir ambos os layouts na edição atual.
5. General Chaos pede três botões. O wrapper atual não oferece seleção 3/6 e o core emula extensão de seis botões. Implementar seleção de pad ou demonstrar equivalência serial real nesta edição; não transferir esse ajuste ao jogador por senha/botão Mode na inicialização.
6. J-Cart requer leitura do hardware no barramento do cartucho; não é sinônimo de Team Player. Não anunciar Micro Machines/Pete Sampras/Super Skidmarks como suportados por somente alterar as vagas.
7. Compilar o core completo ARM64, publicar novo engineId/coreSha256 e perfis canônicos, preservar R81/legado. Rodar serial/pads por porta, todos os botões, limites inválidos e ausência de vazamento entre participantes. O patch parcial não deve substituir o binário ativo.

Arquivos nativos de referência no app: `versions/station-multiplayer-r77-20261008/native/source/frontend/drivers/station_multiplayer.h`, `platform_unix.c`, netplay frontend e wrappers dos cores. A correção SNES Mercury já foi integrada e deve ser preservada.

### APP-03 — Jogos por turnos, convites e plataformas restantes

- Para Monopoly, Clue, Aerobiz, Worms, golfe, quizzes e torneios, determinar modo por modo se os humanos usam pads independentes, um pad compartilhado ou alternância. O protocolo atual rejeita controle compartilhado. Um rótulo 1–8/1–16 não autoriza quatro pessoas simultâneas. Se o modo exigir compartilhamento, implementar dono do turno/controle de forma explícita ou descrevê-lo como alternado/local; nunca usar OR de inputs de todos.
- Completar convite/código v3 sobre membership/prova/expiração atuais. O pacote v3 atual tem lista/chat, mas não um fluxo completo de convite curto v3. Entrada aceita deve atualizar roster, gerar a senha privada automaticamente e manter bloqueios sociais. Não reaproveitar convites v1 para criar sala v3 silenciosamente.
- Para as oito plataformas sem lançamento online pronto, produzir engine/core/runtime/perfil e identidade de conteúdo apropriados. Neo Geo precisa também do conjunto `.neo`/BIOS compatível; Neo Geo CD/CHD/arcade exigem identidade do conjunto, não SHA do ZIP arbitrário. Emitir dados versionados e conferir determinismo/controles/retomada por motor. Os títulos de N64/arcade/Dreamcast já estão no inventário, não precisam de nova lista do mantenedor.

## 7. Trabalho completo no servidor e cadastro automático

### SRV-01 — Publicar dados em lote sobre a liberação atual

1. Revalidar baseline efetivo, índice/hash e ausência de salas/conexões **v1, v2 e v3**, inclusive recuperação retida. `/ready/station/online` agora contém o agregado multiplayer; conferir esse campo também.
2. Validar [profiles-combined-for-review.json](online-1a4-completo-20261008/profiles-combined-for-review.json) contra índice corrente. Comparação deve ser pelos campos/bytes corretos; se índice mudou, regenerar vínculos sem perderIDs. O início do arquivo combinado preserva todos os 1.816 perfis originais, em ordem.
3. Aplicar as 17 adições de dados em uma publicação Station, com cópia anterior, operação CAS e rollback identificado. `MultiplayerEnabled` permanece true e gate legado false; não inserir engines v3 no registro legado. A flag false do gate preserva os dez motores antigos.
4. O processo lê o registro na inicialização. Escrever JSON no Git ou no HD não basta. Ativar em janela sem sessões e comprovar capacidades assinadas pelo domínio público. Preservar a DLL atual quando a mudança for só de dados; não fazer migration ou trocar chaves.
5. Os seis modos Mega em [modes-needing-app-work.json](online-1a4-completo-20261008/modes-needing-app-work.json) estão `approved:false` porque a configuração técnica correspondente ainda não foi entregue. A aprovação de uso pelo mantenedor não falta. Quando o core/perfil correto chegar, vincular seus novos hashes e incluir todos os modos resolvidos no mesmo lote.

### SRV-02 — Perfis/ajuda persistentes para novos jogos, sem programar cada jogo

- Arquivo de dados por jogo/edição e modo, ou editor administrativo equivalente: itemId, contentSha256, engineId/core/runtime, mode, controllerProfile/hash, allowedPlayerCounts, displayName, instruções, origem da informação e status factual da edição.
- O importador continua descobrindo arquivos e capas automaticamente. Ao detectar conteúdo já conhecido, reaproveitar somente regras compatíveis com a identidade/edição. Novo nome ou nova tradução não herda controle por similaridade textual. Manter fontes/regras e overrides fora de alterações descartáveis do catálogo.
- Usar o [gerador](online-1a4-completo-20261008/build_inventory.py) para a conciliação completa. `mode-rules.json` contém dados, não código específico no APK. As referências por nome só formam fila de revisão; nunca concedem vagas automaticamente.
- Para conferir MD5/CRC de referência, usar [qualify_reference_checksums.py](online-1a4-completo-20261008/qualify_reference_checksums.py) uma vez, **offline no servidor**, sobre o mapeamento privado já existente. O script conserva o SHA integral; CRC sem512 bytes de header é marcado como pesquisa e não substitui identidade online. Não introduzir essa etapa nos downloads.
- Integrar ajuda opcional ao envelope assinado de capabilities/room, por exemplo `modeMetadataRevision` e `modeMetadata`, com vínculo exato ao item/content/mode/perfil. Carregador `StationOnlineRegistration.cs` e resposta `StationMultiplayer.cs::View` precisam desse sidecar. Nome/instrução não pode alterar allowedPlayerCounts. Implementar limites de tamanho e ausência/duplicidade de dados; clientes atuais continuam aceitando os campos antigos.
- Atualizar cache pelo revision/envelope recebido. Publicar somente campos públicos: nunca caminhos absolutos de ROM, BIOS, configuração privada, tokens ou códigos de licença.

### SRV-03 — Capacidade e estabilidade medidas no serviço certo

O readiness legado anuncia512 salas/1.024 conexões. **Isso não é a capacidade v3.** O hub v3 tem limite de100 salas e budget compartilhado v2/v3 de32 MiB. Cada link retém duas janelas de256 KiB:

| Pessoas numa sala v3 | Links | Reserva por sala | Limite teórico só por32 MiB, sem uso v2 |
|---:|---:|---:|---:|
| 2 | 1 | 512 KiB | 64 salas /128 pessoas |
| 3 | 2 | 1 MiB | 32 salas /96 pessoas |
| 4 | 3 | 1,5 MiB | 21 salas /84 pessoas, com512 KiB restantes |

Não prometer centenas simultâneas na v3 com a configuração atual. Fazer ensaio de carga representativo de frames/ACK/replay, medir memória/CPU/banda/filas/RTT e só então ajustar código/configuração do budget. Preservar guardas de sessão/prova/ticket; limites de HTTP não são limite de bytes dos downloads. Reinício perde recuperação mantida em RAM.

Para engasgos: correlacionar por sala/link/epoch e horário UTC os FPS/tempo de quadro/JNI/NeedSync/filas de ambos os Androids com envio/ACK/RTT/perda/CPU do relay. A fonte do problema ainda não está provada como servidor, Cloudflare ou app. A separação de portas Mega não é diagnóstico do SNES. Não remover túnel ou ajustar input delay sem comparação controlada e plano reversível.

## 8. Reprodução e validação do pacote

Python 3.11+, compilador C para a fixture nativa; leitura/escrita somente no checkout/cópias de teste:

```bash
python3 docs/station-android/online-1a4-completo-20261008/build_inventory.py
python3 docs/station-android/online-1a4-completo-20261008/check_bundle.py
```

No espelho do app, trocar o prefixo por `docs/server/`. Saída conferida: **29.595 verificações** de cobertura, IDs, capa/artefato, identidades, hashes canônicos, limites de vagas e preservação dos 1.816 perfis. [Recibo](online-1a4-completo-20261008/package-validation.json).

Para reproduzir a falha de portas, em fonte fixada e submódulo clowncommon disponível:

```bash
python3 docs/station-android/online-1a4-completo-20261008/reproduce_mega_port_mapping.py \
  --core-source /copia-isolada/clownmdemu-libretro/common/core \
  --output /diretorio-de-evidencias
```

96 combinações pad/botão, 48 incorretas na base/zero no patch; propriedade padrão 2p e EA 4 preservada na fixture. O script valida hashes das fontes e nunca modifica a árvore original. Não compila nem instala APK/core de produção.

O leitor offline de checksums passou em arquivos sintéticos raw/headered/ZIP e recusa de identidade integral divergente, sem expor caminhos privados: [recibo](online-1a4-completo-20261008/offline-checksum-tool-validation.json). Nenhum arquivo de jogo nem BIOS está no pacote.

## 9. Critério único de fechamento e resultado a devolver

| Verificação | Resultado exigido |
|---|---|
| Catálogo e capa | Os 3.479 visíveis conciliados,255 aliases preservados; capa/grant/launchfile corretos por ID; novo arquivo descoberto pelo importador; sinopses/metadados lidos com `metadata=1` |
| Uma pessoa | Ação local, sem sala v3 fictícia; modo solo identificado quando há evidência |
| Duas pessoas | Battletoads A/B e padrões SNES/Mega; entrada, dois nomes, Pronto, abertura, controle próprio, saída/retorno e reconexão sem senha digitada |
| Três pessoas | Secret of Mana com aliados disponíveis, VS/Battle aplicáveis; somente três donos de controle; nenhum controle move outra pessoa |
| Quatro pessoas | Bomberman2/3 Battle, NBA Jam, Top Gear VS e cada outro modo incluído; quatro membros/slots/links; input separado e jogo realmente com quatro humanos |
| Mega completo | Layouts/EA/pad de 3 botões/J-Cart testados por fonte/edição; novo core/hash/perfis publicados de forma coordenada; regressão padrão 2p |
| Turnos | Alternância/pad compartilhado identificados; sem anúncio falso de simultaneidade ou inputs misturados |
| Plataformas restantes | Cada item tem disposição explícita e motor/identidade adequado para ser anunciado online; nunca inferir do motor local |
| Perfil errado | Recusa de item/content/engine/runtime/profile/geração/ticket divergentes; não retirar autenticação para fazer entrar |
| Falha de rede | Pausa real/barreira/retomada dos membros; primeiro erro identificado; saída humana limpa; sem falso playing ou tela esperando indefinidamente |
| Desempenho | Partida contínua com movimento de todos, medição de frames/RTT/filas; não substituir gameplay por soma de checks sintéticos |
| Publicação | Hashes de APK/core/DLL/registro, capabilities assinadas e smoke pelo domínio público; licenças, saves, assinatura, dados e demais serviços preservados |

Três/quatro pessoas exigem teste com três/quatro entradas Android reais; simular quatro sockets comprova relay, não quatro jogadores no motor. Não registrar resultado não observado. Executar toda a matriz disponível e consolidar lacunas reais no mesmo resultado, sem exigir um novo pedido do mantenedor para cada jogo.

**Devolver um único fechamento:** quais linhas/modos do inventário foram concluídos, versão e hashes efetivos, games/quantidades/controles realmente exercitados, comportamento de falha/retomada, medições de desempenho e qualquer pendência técnica ainda concreta. Não responder só “JSON publicado”, não remontar o histórico em vários novos handoffs e não repetir a implantação R81 já ativa.

## Anexos da mesma entrega

- [Catálogo completo público](online-1a4-completo-20261008/catalog-rev20.json) · [Inventário JSON](online-1a4-completo-20261008/inventory.json) · [Inventário TSV](online-1a4-completo-20261008/inventory.tsv)
- [Todos os candidatos 3/4](online-1a4-completo-20261008/games-3a4.md) · [TSV](online-1a4-completo-20261008/games-3a4.tsv) · [Cobertura](online-1a4-completo-20261008/coverage.json)
- [Perfis ativos exatos](online-1a4-completo-20261008/profiles-active-r81.json) · [17 adições](online-1a4-completo-20261008/profiles-additions-r81.json) · [Combinado 1.833](online-1a4-completo-20261008/profiles-combined-for-review.json)
- [Vínculos/fontes/instruções dos modos](online-1a4-completo-20261008/mode-proposals.json) · [Regras editáveis](online-1a4-completo-20261008/mode-rules.json) · [Seis modos Mega com tarefa no app](online-1a4-completo-20261008/modes-needing-app-work.json)
- [Sete configurações canônicas](online-1a4-completo-20261008/controller-profiles-r81.json) · [Engines atuais](online-1a4-completo-20261008/engines-r81.json) · [Ajuda proposta](online-1a4-completo-20261008/mode-metadata-proposed.json)
- [Fontes primárias fixadas](online-1a4-completo-20261008/sources.json) · [Mapas de referência](online-1a4-completo-20261008/reference-adapter-mappings.json) · [Gerador](online-1a4-completo-20261008/build_inventory.py) · [Validador](online-1a4-completo-20261008/check_bundle.py)
- [Falha Mega reproduzida](online-1a4-completo-20261008/mega-port-mapping-proof.json) · [Patch isolado](online-1a4-completo-20261008/clown-sega-port-offset.patch) · [Reprodução](online-1a4-completo-20261008/reproduce_mega_port_mapping.py)

Esta rodada preparou e verificou a entrega completa. Serviço, flags, registro ativo, índice, licenças, jogos, capas, saves, APKs, Nginx, túnel e demais produtos não foram alterados pela geração deste pacote.
