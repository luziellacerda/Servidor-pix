# Entrega única: Station online com até cinco jogadores

**SERVIDOR → PRODUÇÃO DO APK · 09/10/2026.** Pedido implementado: permitir até cinco pessoas conforme o jogo e o modo, informar o limite e manter os clientes existentes. Este é o documento de execução completo; os JSON, fontes, binários compilados e receitas são anexos desta mesma entrega.

## Estado atual e o que executar no PC do APK

**Servidor de cinco aplicado às 12:05:52 UTC; modos finais aplicados às 13:00:35 UTC / 10:00:35 de Maceió**, conferência pública concluída às 13:02:20 UTC. Serviço Station ativo, PID1722044/NRestarts0, `maximumPlayers:5`, **3.672 vínculos de perfil/motor** carregados. Os 1.816 perfis originais permanecem iguais; todos os 3.666 vínculos da primeira etapa conservam sua configuração de jogo. Catálogo revisão24, 3.848 IDs, 3.593 visíveis e 255 de compatibilidade preservados. Estão ativas as 17 adições de modos anteriores e três adições de campanha/batalha, em ambos os runtimes compatíveis.

**App implementado e compilado:** conjunto completo de 209 Java, DEX de salas e runtime Android arm64/API26/16KiB. Fonte nova em TurboElden, ramo `feat/station-five-players-client-20261009`, diretório `versions/station-five-player-support-20261009/`. O conjunto R81 congelado permanece a referência para montar o APK completo; esta sucessora contém todos os 209 fontes necessários.

**Executar agora no PC do APK:** usar `recipes/package_candidate.py` sobre o APK completo R81 do backup G:, com a assinatura original, verificar o recibo, atualizar os aparelhos sem limpar dados e testar os controles P1–P5. O Linux não possui o APK privado completo nem o keystore do PC. Por isso o APK integral assinado e a instalação nos celulares ainda estão pendentes; DEX e biblioteca nativa já estão prontos nesta entrega. Não solicitar nova autorização genérica para a integração pedida pelo mantenedor.

Os dois canais existentes continuam R76 de referência e R81 de teste. A montagem nova substitui a candidata do canal de teste depois da conferência; a receita não altera os canais nem instala automaticamente. O último recibo físico conhecido é Motorola R81/Samsung R78. Não inferir que já receberam a versão de cinco.

## Jogos, modos e número de pessoas

| Jogo/edição presente | Modo cadastrado | Humanos simultâneos nesta integração | Configuração no jogo |
|---|---|---:|---|
| Super Bomberman, SNES USA | Normal / Battle | Campanha **2**; batalha 2, 3 ou **4** | Normal: segundo pressiona Start depois do início. Battle: escolher humanos `MAN`, outros `COM`. |
| Super Bomberman 3, SNES Europe | Battle Single / Battle Tag | 2, 3, 4 ou **5** | `BATTLE GAME` → `SINGLE MATCH` ou `TAG MATCH`; participantes em `MAN`, outros em `COM`/`OFF`; Tag exige duas equipes com participantes. |
| Super Bomberman 3, SNES Europe | Normal | Campanha **2** | Escolher `2PLAYERS GAME`; três a cinco somente na batalha. |
| Super Bomberman 2, SNES USA e PT-BR | Battle Single / equipes | 2, 3 ou **4** | Battle Game, Multitap; marcar humanos nas posições correspondentes. Normal Game é solo. |
| NBA Jam, SNES | Partida com Multitap | 2, 3 ou **4** | Cada humano confirma sua posição no jogo. |
| Top Gear 3000, SNES e PT-BR | VS / campeonato | VS: 2, 3 ou **4**; campeonato: **2** | Usar VS para três/quatro. Outros carros podem ser CPU. |
| Secret of Mana, SNES PT-BR | Cooperação | 2 ou **3** | Recrutar os aliados; Start permite participar. |
| Battletoads in Battlemaniacs, SNES e PT-BR | Dupla A / dupla B | **2** | `2PLAYERS A` permite golpes entre parceiros; B os desativa. |

Super Bomberman1 tem campanha até dois e batalha até quatro, conforme o [manual Hudson, páginas impressas4–7](https://www.retrogames.cz/manualy/SNES/Super_Bomberman_-_SNES_-_Manual.pdf). O quinto jogador de Super Bomberman3 pertence à batalha; sua campanha permite dois, conforme o [manual Hudson, páginas impressas4–5 e8–9](https://www.retrogames.cz/manualy/SNES/Super_Bomberman_3_-_SNES_-_Manual.pdf). O adaptador na segunda porta fornece cinco pads, conforme a [documentação oficial bsnes Mercury](https://docs.libretro.com/library/bsnes_mercury_performance/#multitap-support). Super Bomberman2 tem campanha solo e batalha Single/Tag até quatro, descritas no [manual Hudson](https://www.videogamemanual.com/snes/Super%20Bomberman%202%20(USA).pdf). Os demais manuais/fontes e os localizadores estão em [sources.json](online-1a5-20261009/sources.json).

**Todos os 3.848 IDs foram cruzados:** [game-support-completo.json](online-1a5-20261009/game-support-completo.json) e [TSV para consulta](online-1a5-20261009/game-support-completo.tsv). Há um jogo com dois modos de cinco, seis edições com máximo quatro, uma com máximo três, 1.808 com admissão anterior de duas preservada e 2.032 sem perfil online compatível, incluindo os aliases. Perfis genéricos anteriores são autorização de admissão; vários jogos individuais ainda não tiveram seus modos/controles conferidos fisicamente. Não apresentar esses 1.808 como jogos simultâneos já testados.

## Forma de participação implementada

**Sala visível, avisos claros, controles somente nas vagas válidas.** Toda sala listada pode ser consultada por **Ver detalhes**, inclusive cheia ou em andamento. Essa ação abre uma ficha local do snapshot assinado e não executa Join, Ready, ticket, nativo ou nova conexão. Para jogar, o botão Entrar aparece quando há vaga; o servidor também confere limite, perfil, edição e capacidade do cliente. Os avisos da campanha/batalha aparecem na escolha do modo e nos detalhes da sala.

Quando há modos documentados, a seleção usa campanha/batalha específicos e omite a opção genérica ambígua. O registro genérico continua preservado para leitura/compatibilidade. Jogos ainda sem modos documentados mantêm a admissão anterior; não conceder vagas adicionais por esse motivo. Um perfil solo continua no fluxo local.

**Ver detalhes não transmite a partida.** O protocolo Station atual não possui papel de espectador com seu próprio ticket/relay/bootstrap. Um participante extra colocado como jogador sem pad entraria indevidamente no roster e na barreira, podendo travar quem joga. A documentação [RetroArch Netplay](https://docs.libretro.com/development/retroarch/netplay/) descreve observadores sem input e transições próprias de PLAY/MODE. A escolha implementada é uma conclusão técnica para este wrapper: preservar a consulta e a admissão válida; gameplay para espectadores requer integração separada de observação, sem controlar pads nem bloquear a barreira dos jogadores. Não inventar P6 nem compartilhar a licença/ticket do anfitrião.

## Revisão da coleção inteira e Bomberman futuros

[collection-review-completo.json](online-1a5-20261009/collection-review-completo.json) cobre todas as 17 plataformas, todos os nomes/IDs, visibilidade, `coverId`, rótulo de jogadores, referências, modos e lacunas. [candidates-more-than-two.tsv](online-1a5-20261009/candidates-more-than-two.tsv) contém **518 registros candidatos, 475 visíveis**; 43 são compatibilidade. **São pistas de revisão, não 475 jogos já homologados.** O cruzamento distingue rótulos, adaptadores de outros emuladores, sinopses, modos originais documentados e motores Station ativos.

| Plataforma | IDs/visíveis | Candidatos visíveis para revisão >2 | Edições com perfil Station >2 |
|---|---:|---:|---:|
| CPS1 | 36/36 | 6 | 0 |
| CPS2 | 64/64 | 10 | 0 |
| CPS3 | 11/11 | 0 | 0 |
| Dreamcast | 243/243 | 8 | 0 |
| FBNeo | 913/913 | 101 | 0 |
| GameCube | 21/21 | 5 | 0 |
| Mega Drive | 1.133/887 | 124 | 0 |
| Mega Drive PT-BR | 94/94 | 4 | 0 |
| N64 | 157/157 | 77 | 0 |
| Neo Geo | 189/189 | 4 | 0 |
| Neo Geo CD | 50/50 | 1 | 0 |
| PSX | 84/84 | 6 | 0 |
| SNES | 653/644 | 104 | 5 |
| SNES PT-BR | 191/191 | 22 | 3 |
| Switch | 1/1 | 0 | 0 |
| Wii | 7/7 | 2 | 0 |
| Wii U | 1/1 | 1 | 0 |

Rótulos vazios não significam jogo solo: os 114 novos itens foram revistos também por referências dos jogos presentes. A lista inclui Double Dash, Mario Party7, Star Fox Assault, Melee, Four Swords, Crash Bash/CTR/Winning Eleven/Worms/Wipeout, Mario Kart Wii/New Super Mario Wii e Mario Kart8. Alguns ainda são pistas de manual, e Worms/link de consoles/pads compartilhados exigem regras diferentes de humanos simultâneos. Oito humanos compartilhando quatro pads do [Mario Party7](https://www.nintendo.com/en-gb/Games/Nintendo-GameCube/Mario-Party-7-268313.html) e GBAs do [Four Swords](https://www.nintendo.com/en-gb/Games/Nintendo-GameCube/The-Legend-of-Zelda-Four-Swords-Adventures-269028.html) não se tornam cinco controles independentes apenas alterando um número. Zero candidatos numa plataforma significa nenhum identificado pelos insumos atuais; modos sem prova continuam explicitamente desconhecidos.

| Bomberman presente ou solicitado | Referência de humanos | Estado Station |
|---|---|---|
| Super Bomberman1 | Normal2; Battle4 | Modos exatos ativos nos dois runtimes |
| Super Bomberman2 USA/PT-BR | Normal1; Battle Single/Tag4 | Modos ativos; aviso Batalha obrigatório |
| Super Bomberman3 Europe | Normal2; Battle Single/Tag5 | Modos ativos; cinco exige runtime novo |
| Mega Bomberman | Normal1; Battle4 | Referência original documentada; correção de mapeamento TeamPlayer ainda necessária para perfis >2 |
| Bomberman Online, Dreamcast | Normal1; Battle local4 | Manual original documentado; motor Station online ainda ausente |
| Bomberman64 | Referência Nintendo1–4 | N64 online ainda não integrado; detalhar modos da edição |
| Bomberman Hero | **Individual** | Não presumir Batalha pelo nome da família |
| Neo Bomberman / Panic Bomber | Driver arcade define duas entradas humanas | Sinopse de quatro personagens não autoriza quatro pads |
| Super Bomberman4 e5 | Solicitados para futura importação | Não estão como ROM no catálogo atual; artes/vídeos existem. Plano preparado, sem IDs/hashes inventados |

Bomberman Hero é individual na [página da Nintendo](https://www.nintendo.com/en-gb/Games/Nintendo-64/Bomberman-Hero-276445.html). As duas entradas humanas NeoGeo são verificáveis nos drivers [FBNeo fixados nesta revisão](https://github.com/finalburnneo/FBNeo/blob/ef832a87e8fee6a252bd25ffb2c03851f483b850/src/burn/drv/neogeo/d_neogeo.cpp); a conclusão sobre não conceder pads por personagens/sinopse é técnica, sem homologação de modo Station. Fontes/modes e pistas sem manual estão em `primary-mode-review.json`.

Para4/5, [future-bomberman4-5.json](online-1a5-20261009/future-bomberman4-5.json) registra o pedido, controlador candidato e etapas de importação/edição. O importador já reconhecerá o arquivo real e sua capa; depois os perfis usam o hash/motor exatos e os limites do manual. Não ativar pelo nome ou pela arte nem afirmar que as duas ROMs já foram incluídas.

Mega Drive mantém seus perfis existentes. Os adaptadores Sega/EA e os demais títulos multiusuário continuam com as tarefas de mapeamento identificadas no pacote anterior. Nenhum perfil de cinco foi atribuído a Mega Drive. Catálogo/capas/download de N64, Neo Geo, CD, Dreamcast, arcade, GameCube, PSX, Wii, WiiU e Switch continuam disponíveis; a presença desses jogos no catálogo não fornece um motor online v3 compatível. O manifesto preserva `launchReady:false` do Geolith.

## Como o app sabe o limite e lê o servidor

1. Ler `GET /v1/station/catalog?metadata=1` usando a sessão/prova Station e conferir o envelope assinado. Revisão global atual24. Associar capa pelo `coverId` do próprio `itemId`; pedir download pelo mesmo `itemId` e usar o descritor assinado/`launchPath`. O DEX de autenticação, catálogo, capas e download foi reproduzido sem alterações. Não acrescentar rehash/testzip ao caminho do download.
2. Ao escolher um jogo, enviar `POST /v1/station/online/multiplayer/command`, ação `capabilities`, `itemId` e `clientMaximumPlayers:5`, com UUID novo. Incluir essa capacidade **antes** de serializar o corpo e gerar a prova HTTP. O novo cliente faz isso em todos os comandos multiplayer; o arquivo modificado é `StationOnlineClient.java`.
3. Conferir o envelope e `multiplayerVersion:3`/`capability:station-multiplayer.v3`. Cruzar cada perfil com o motor local exato e com `itemId`, `contentSha256`, `engineId`, `coreSha256`, `runtimeSha256`, `profileId` e `profileSha256`. O hash do recipiente ZIP/CHD não substitui a identidade do arquivo que o motor abre.
4. Mostrar `modeTitle`, `maximumPlayers` e **somente** as capacidades de `allowedPlayerCounts`. Perfil solo tem máximo1 e lista vazia: usar o fluxo local. Para online, a sala inicia com pelo menos dois. Exibir `instructions` e as referências HTTPS de `sources` na ficha do modo. O novo `StationGamePlayerInfo` e a Activity já fazem essa composição; `StationMultiplayerProfile.choices` escolhe os modos documentados. A consulta de uma sala não cria um espectador nem abre o motor.
5. Criar a sala com a identidade do perfil e a `capacity` escolhida; entrar com a mesma identidade e `roomId`/`generation` atuais. Pronto ocorre após a entrada. Mostrar os nomes e posições P1–P5 recebidos. O host inicia quando todos estão prontos.
6. Obter ticket/resume e abrir `/v1/station/online/multiplayer/relay`, WSS/subprotocolo `station-stream.v3`, com a credencial de uso único `StationRelay` e a prova existente. Usar `room.connectionPassword` automaticamente no nativo. O convite curto e a licença do app continuam distintos da senha interna da partida.
7. Host de cinco usa quatro links independentes, um para cada convidado P2–P5. Cada link tem dois endpoints; são oito WSS na sala cheia. Preservar offsets/ACK, epoch e a barreira de todos na retomada. Queda temporária não é Leave; saída voluntária deve limpar apenas os recursos da própria sala.

Exemplo do corpo do primeiro comando, cujos bytes devem ser usados na prova:

```json
{"action":"capabilities","requestId":"UUID-novo","itemId":"station_46fe7356ab5cc63a1438f720b9c7ce2d","clientMaximumPlayers":5}
```

**Compatibilidade:** clientes que omitem `clientMaximumPlayers` são tratados como máximo4 e não recebem perfis/novas salas de cinco. Tentativa incompatível recebe HTTP409/`STATION_MULTIPLAYER_CLIENT_UPDATE_REQUIRED`. Não baixar o limite dessa proteção: o runtime antigo só comporta três convidados. R76/v1/v2 e o registro legado de dez engines permanecem disponíveis com os mesmos vínculos.

## Identidades e arquivos para a produção

| Componente | Identidade exata |
|---|---|
| Fonte executável servidor | `d8ebd6572d7a3a82a95b09dc6c8dd4e2737334b5` |
| DLL ativa | `ad45a4f0f4c7aef3dd4191b453da384be008e2b06aaaeb8b1ca359f3cb83570a` |
| Registro multiplayer, 3.672 entradas | `244d98e76c4b5cc00f1af75abebe595688d1700cef6d2f8bc777dc84c3b388ad` |
| Índice atual, sem modificação | `6b8acfa4ca419ec705f48f53e2063633ff8f0a30a36cb8f4d651a108cbb31137` |
| Runtime Android entregue | `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc` |
| `classes35.dex`, salas | `82609bdcbf8c36f942944486cbfb28061c5523e4c19c06618d149b154fbb0756` |
| `classes28.dex`, client preservado | `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63` |
| Core SNES preservado R77 | `0a3ac7b4fa5da318b1c993d8867aa944eaa2d012680b49e9929bf7a4a398f527` |
| Core Mega preservado | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |
| APK privado completo de entrada R81 | `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6` |
| Certificado original obrigatório | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |

Super Bomberman3 Europe desta entrega: item `station_46fe7356ab5cc63a1438f720b9c7ce2d`, conteúdo `3c665283c14f050b60fe9693b12e437288bcd4aa3e56ef25106d503c44e0fda5`, 1.572.864 bytes, `Super Bomberman 3 (Europe).sfc`. Perfis `snes-sbomberman3-battle-single-5p-20261009` e `snes-sbomberman3-battle-team-5p-20261009`, motor `bsnes-mercury-performance-79d7f9de-mt1-mp5-81b3daa38fb9`.

Anexos de dados: `profiles.json` é o arquivo efetivo completo; `profiles-added-old-client.json` contém as 20 adições para o runtime anterior; `profiles-new-runtime.json` contém os 1.836 vínculos desta sucessora; `engines-app.json` corresponde ao asset do app. Não inserir engines v3 no registro legado v1/v2.

## Montagem única do APK completo

No TurboElden, abrir `versions/station-five-player-support-20261009/README.md`. A receita usa os binários compilados entregues e substitui **três entradas**: `classes35.dex`, `lib/arm64-v8a/libstation_retroarch.so` e `assets/station-online/engines.json`. Confere o certificado, todos os hashes, o inventário de 13.226 entradas não relacionadas à assinatura, a compressão e a preservação dos demais arquivos. Mantém manifesto Android, identidade, login, BIOS/assets, vídeos, outros motores, jogos, saves e DEX do client. Nenhum desses conteúdos privados está publicado nesta entrega.

Entrada do PC: `G:\BAKUP SISTEMA APP 03-10-2026\ATUAL-2P-E-TESTE-4P-20261008\test-up-to-4-players-r81\TurboStations-Premium-R81-20261008.apk`. Saída/temporários apenas E:. Informar explicitamente JDK17, build-tools35, keystore original e ambiente privado de assinatura. Não executar empacotadores históricos de R41/R57/R67, não selecionar a maior revisão e não limpar dados dos telefones.

O código completo correspondente do runtime GPL está em `native/retroarch-corresponding-source.tar.gz`, junto ao COPYING e manifesto de 11.149 fontes; o archive tem SHA `b8f7649a5edacbe9e913d5e50f66482c0aa082fbd2237f5fd7800d00723b735a`. A receita opcional `build_native.py` recompila com NDKr28c e devolve um recibo. Saída recompilada com SHA diferente exige atualizar os vínculos correspondentes em conjunto; a montagem desta entrega usa o ELF já registrado. A receita Java recompila o conjunto inteiro com API34/D8 fixados. As receitas foram conferidas em sintaxe; a receita de montagem ainda não foi executada sobre o APK privado neste Linux.

## Verificação realizada e teste nos aparelhos

| Verificação desta rodada | Resultado | Limite da evidência |
|---|---:|---|
| C# multiplayer, slots, capacidades, provas, epochs, reconexão | 433 checks | Sem core/frames Android |
| C# HTTP e oito WSS reais em TLS loopback | 172 checks | Cinco identidades sintéticas |
| Compilação Java/DEX | 209 fontes; client R81 reproduzido | Não é APK integral instalado |
| Classificação de perfis Java | 166 checks | JVM, incluindo campanha solo/dupla e modos explícitos |
| Fonte C real extraída, mutex/threads, P1–P5/ownership/epochs | 1.176 checks | Nativo host, sem partida emulada |
| Catálogo público autenticado/assinado, metadados, capa/download | 28.776 checks | Fixture própria removida |
| Clientes legados pelo domínio público, dados finais | 189 checks | v1/v2, provas, grant/download; etapa anterior190 |
| Multiplayer v3 público, dados finais | 826 checks | 2,4,5; ambos modos de cinco, reconexão P5; 2.304 bytes exatos; campanha2/Battle4 e recusa de cinco em modos incompatíveis |
| Inventário/registro/core/runtime/DEX/fontes | Passou | Todos os 3.848 IDs, nenhum omitido |

Também qualificadas a versão anterior e a candidata em processos sombra, com fixtures independentes e cleanup. O recibo herdado do core R77 contém 6.609 checks de entrada/P1–P5; foi preservado como evidência anterior, não repetido nesta rodada. **Gameplay de cinco Androids, estabilidade longa, FPS, latência WAN e capacidade de centenas não estão homologados por esses testes.**

Teste físico único para concluir: instalar a mesma candidata nos participantes, criar sala nova Super Bomberman3/Battle Single, conferir cinco nomes/Pronto, configurar MAN P1–P5 e exercitar cada controle isoladamente. Repetir Tag com duas equipes, interromper e retomar apenas P5, sair voluntariamente e confirmar limpeza. Conferir regressão de Battletoads2/Super Bomberman2 até4/Secret of Mana3. Registrar APK/runtime/core/perfil/slots, imagem/áudio, inputs exclusivos, epochs e primeira causa de erro nos cinco. Atualizar o mesmo recibo desta entrega; não criar um handoff por jogo.

## Como cadastrar novos jogos e saber o suporte

O importador automático existente continua reconhecendo arquivos e publicando catálogo/capas/artefatos. A regra online é um registro de dados por **conteúdo exato, motor e modo**, consumido pelo servidor/app; não exige programar cada nome de jogo. Para adicionar um modo com um controlador já implementado:

1. Usar a identidade qualificada publicada pelo importador, com `itemId`/`contentSha256` da edição presente. Conferir manual do modo e o número de humanos simultâneos; diferenciar solo, turnos, CPU e multiplayer simultâneo.
2. Criar uma entrada no registro com o mesmo esquema de `profiles.json`: motor/core/runtime exatos, perfil/modo novo, controller compatível, máximo1–5, lista ordenada das quantidades permitidas, título, instruções e URLsHTTPS. O `profileSha256` é o SHA256 UTF8 da configuração canônica `schemaVersion/controllerProfile/devices/coreOptions`, exatamente como `StationMultiplayerProfile.canonical`; modo e vagas são verificados nos campos assinados separados.
3. Conferir a leitura Java/C# e os pads/ownership antes de anunciar o modo como jogável. Aplicar a atualização de dados com backup e recarga Station sem salas/conexões ativas, preservando os perfis atuais e a referência do catálogo. Um controlador/core/plataforma ainda ausente exige implementação do motor, não apenas mudar `players`.

O rótulo `metadata.players` permanece informativo. Nunca conceder cinco vagas por nome, adaptação de capa, classificação XML “1–5/1–8” ou por encontrar um adaptador: a **interseção jogo/modo/controlador/runtime** determina o limite real. O servidor não informa descoberta automática de toda ROM desconhecida. O novo app apresenta o modo e as vagas exatas aprovadas que recebeu.

O script offline [verificar-pacote-online-1a5.py](scripts/verificar-pacote-online-1a5.py) confere este pacote selado, todos os IDs, o prefixo histórico de 1.816 entradas e os binários Java/nativo entregues. Não é um operador de recarga nem deve ser usado para alterar o índice.

## Operação, segurança e retorno

Somente Station foi recarregado. Release atual `/opt/turborama-station-five-players-20261009-ad45a4f0f4c7`; registro final em `/opt/turborama-station-five-players-modes-20261009-244d98e76c4b/profiles.json`, vinculado por EnvironmentFile posterior e bind somente leitura. Fonte executável/dll/perfis acima. Índice/importador/timer e mídias permanecem os anteriores. Flags `MultiplayerEnabled=true`/`MultiplayerLegacyCapacityGate=false`; registro legado de dez engines SHA `a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843`. Licenças reais, chave de assinatura, banco/esquema, usuário/grupo Station, sandbox e montagens somente leitura foram preservados. Nenhuma alteração em PIX/Suite/site/painel/WhatsApp, Nginx, túnel, portas ou firewall.

A reserva compartilhada de replay permanece **32MiB**. Sala cheia de cinco reserva2MiB; o orçamento permitiria16 dessas salas/80 participantes sem outras reservas v2. Esse cálculo não é homologação de 80 pessoas nem garantia de latência. Não aumentar esse orçamento ou atribuir os engasgos anteriores a CPU/túnel sem medições dos aparelhos.

Houve uma tentativa inicial de publicação que retornou automaticamente à release anterior porque a ferramenta consultou a saúde local pelo endereço público, recebendo o403 previsto. Os fluxos autenticados do app já tinham passado. A ferramenta passou a consultar saúde local e a publicação concluiu. A atualização final de modos encontrou em sombra uma pasta sem travessia para o usuário do serviço por causa da umask077; a permissão0750 foi explicitada, mantendo arquivo0640 e EnvironmentFile privado. Essa falha foi corrigida antes de tocar o serviço real. Não houve sala real interrompida; zero sessões antes das trocas. O resultado final registra zero salas/conexões/replay e NRestarts0. Não expor a saúde nem afrouxar o sandbox.

Rollback preservado em área privada. Para voltar somente os dados de modos, remover o drop-in `z`×34 + `-station-modes-20261009.conf`, recarregar systemd e reiniciar somente Station sem salas/conexões/recuperação; retorna aos3.666 vínculos anteriores de cinco. Para voltar à R81, remover também `z`×32 + `-station-five-players-20261009.conf`, com a mesma guarda de ausência de atividade. Arquivos antigos permanecem intactos. Os dois operadores são específicos dessas baselines seladas e **já foram executados**; não reaplicá-los pelo PC do APK ou contra sucessoras.

Recibo público sanitizado: [server-deployment.json](online-1a5-20261009/server-deployment.json). Diagnósticos privados, backups, chaves, licenças, ROMs e APK permanecem fora do Git e do pacote público. As pendências anteriores de BIOS NeoGeoCD, `aof2.zip` e referências XML continuam no retorno de catálogo anterior; esta integração de cinco não as modifica.
