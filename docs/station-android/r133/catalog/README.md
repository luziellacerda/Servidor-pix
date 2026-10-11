# Classificação integral de salas e modos — R133, 11/10/2026

A matriz contém **3.940 entradas: 3.685 seleções visíveis e 255 aliases**. Cruza jogos, identidades de conteúdo, **5.176 perfis e 321 modos originais** do servidor `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861` com o recibo final R133. O nome histórico R132 desta pasta foi preservado; a revisão efetiva dos resultados é R133.

A fonte executável é `5966a8ad23ae9040c570d359b999c8a31de904b4`; SHA do recibo final: `d1eb9bfe78be4c513daedde1ff1f624f653b5353db5a64b8a07d9df213dfb34e`. IDs, hashes de inputs e contagens completas estão em [summary.json](summary.json). Nenhum APK, motor, opção de consumo ou arquivo de jogo foi modificado por esta auditoria.

## Resultado fechado

**78 modos em 44 jogos** produziram 78 perfis novos pelas funções puras dos helpers oficiais do servidor: nove modos específicos já documentados no servidor e 69 decisões adicionais. Os prefixos completos dos 5.176 perfis e 321 modos originais continuam idênticos; o arquivo privado de modos resultante tem 399 registros. Esta é preparação documental e técnica, sem validação física de gameplay.

| Plataforma | Modos / jogos preparados | Demais seleções |
| --- | --- | --- |
| Nintendo 64 | **66 / 33** | 110 têm somente política genérica histórica; sete solo por fonte primária; sete com impedimento documental específico |
| GameCube | **9 / 8** | Nove solo documentados; quatro impedimentos documentais ou de adaptador especial |
| Wii | **3 / 3** | Skyward Sword solo; Galaxy 1/2 dependem de Co-Star; falta revisão dos comandos/modo da edição Wii de Epic Mickey 2 |
| PSX | **0 / 0** | Todas as 84 edições exigem revisão HLE por conteúdo; 26 entradas multiplayer históricas examinadas, dez com fonte primária e 16 sem manual primário concluído |

Os 97 resultados documentais adicionais constam dos quatro inputs listados em `summary.json`. Cada revisão distingue modo, acessórios, portas e edição. Contagem geral não aprova todos os modos de um título.

### Todas as seleções visíveis

| Estado | Seleções | Interpretação |
| --- | ---: | --- |
| Vínculo histórico correspondente ao asset atual | 2.996 | Motor/runtime/canonical existentes; produção fresca ainda não conferida |
| Novo vínculo preparado para R133 | 44 | 78 modos aguardam aplicação administrativa e confirmação assinada |
| Política genérica histórica sem revisão específica | 110 | Sem aprovação nova ou contagem por modo presumida |
| Solo confirmado nesta revisão | 17 | Nenhuma sala multiplayer nova |
| Classificação solo herdada | 77 | Política original preservada; não é nova leitura de manual de cada título |
| Bloqueio documental ou adaptador especial | 14 | Motivo por item na matriz |
| Edição sem vínculo aprovado atual | 6 | Falta vínculo verificável de conteúdo/motor/perfil |
| Sem owner Android online atual | 337 | Política de servidor não cria emulador online no cliente |
| PSX com HLE por edição não aprovado | 84 | Nenhuma aprovação antiga migrada |

Os 255 aliases são entradas ocultas, não outras salas. PS3/PS4 permanecem offline. Lista vazia de contagens **não significa automaticamente solo**: leia estado e razão.

## Regras por modo

- **Mario Tennis 64:** dois ou quatro, sem sala de três inventada.
- **Mario Golf:** Stroke, Skins, Club Slots e Mini Golf de dois a quatro; Match para dois. Turnos com controles independentes, distintos de ação simultânea. O contrato admite essas portas e não exige ação no mesmo quadro.
- **Pokémon Stadium ocidental:** Kids Club avulso/seqüência de dois a quatro; Free Battle **Rental Only** para dois; Tag-Team **Rental Only** para três ou quatro. Comandos de batalha alternados. Transfer Pak, GB Tower e Pokémon importados excluídos; o item japonês continua separado.
- **Conker:** oito decisões do manual original, incluindo Tank/Turret somente para quatro e variantes próprias de Heist/Deathmatch/Race. Não empresta regras de Live & Reloaded.
- **Mortal Kombat 4:** quatro modos de dois humanos; Trilogy, modo comum para dois. Mythologies permanece sem aprovação por falta de páginas primárias verificadas, sem inferência de solo.
- **Quake II:** quatro modos locais de dois a quatro; Snowboard Kids 1/2 usam Battle Race/Battle Mode com portas independentes.
- **The New Tetris:** Marathon, Ultra e Lines multiplayer; Sprint excluído. **Star Wars Racer:** modo de dois. **WinBack:** Death Match de dois a quatro, quatro modos de dois e Point Match de três/quatro.
- **Killer Instinct Gold:** Arcade para dois; Tournament de dois a oito não representa oito controles simultâneos.
- **Donkey Kong 64:** Battle desbloqueado após resgatar o primeiro Kong; **Perfect Dark:** Deathmatch. Ambos exigem `expansionPak=true` na canonical real para as regras preparadas, sem alteração de opções.
- **Sonic Adventure 2 Battle:** Battle para dois; Chao Party Race de dois a quatro, com dados/participantes disponíveis. História e Beginner Race não recebem quatro.
- **Mario Party 7:** modos comuns até quatro com Mic desligado; perfil compartilhado de oito não substituído por quatro portas comuns.
- **Donkey Kong Country Returns:** dupla Donkey/Diddy, Wii Remote horizontal e gestos configurados. Transporte de movimento/extensões/pointer não prova bindings físicos de cada telefone.

Fontes, páginas, condições e contagens por modo estão em [reviewed-mode-provenance.json](reviewed-mode-provenance.json), [web-mode-research.json](web-mode-research.json), [n64-root-mode-decisions.json](n64-root-mode-decisions.json) e [n64-additional-primary-review.json](n64-additional-primary-review.json). Instruções são paráfrases curtas de uso.

## Recusas explícitas

N64: Banjo-Tooie japonês sem discriminação primária por minigame; Mythologies sem páginas verificadas; Pilotwings com campo oficial conflitante e modo não identificado; Stadium japonês distinto; Smash Remix não herda homologação do original; Turok 2/3 sem páginas originais legíveis por modo. Tetrisphere tem transcrição sem digitalização original e não recebeu aprovação nova. Recursos dos remasters PC/Switch não foram transferidos.

GameCube: dois discos DUB de Code Veronica e Terminator sem revisão primária suficiente; Four Swords exige GBA/link. Wii: Co-Star é papel específico, não quatro remotes genéricos; Epic Mickey 2 não foi aprovado por manual de outro port.

PSX: owner atual com duas portas digitais, sem Multitap/analógico. As dez entradas com fontes primárias abrangem CTR, duas edições Crash Bash, GT2, Tekken 2/3, Twisted Metal, Fighting Force, Legend of Mana e Street Fighter EX2 Plus. Documentação do original não comprova HLE da edição instalada. Nenhuma política multiplayer antiga foi migrada como aprovação.

## Guardas e reprodução

[prepare.py](prepare.py) lê oito blobs crus do commit exato do servidor, cada um com SHA fixo, ou suas cópias pinadas do backup. Executa somente funções puras de `build_incoming_manifest.py`, `merge_scoped_modes.py` e `station_online_profiles.py`. Verifica:

1. Ordinal, nome e plataforma exatos; sem associação por regex.
2. Conteúdo original já registrado e formato de lançamento aceito.
3. Assentos documentados por modo dentro das portas implementadas.
4. Canonical/core/runtime/engine IDs do recibo final e opções obrigatórias literais.
5. Fontes específicas por jogo; política genérica GitHub não prova jogadores.
6. Um perfil para cada decisão, sem identidade de conteúdo inventada ou aprovação HLE implícita.
7. Prefixos completos dos 5.176 perfis e 321 modos preservados.

O privado `existing-test-policy-proposal.json` conserva 5.436 decisões genéricas históricas e 172 candidatos dos novos owners como proveniência. Declara `reviewOnly=true` e `importableApprovalLedger=false`; **não é ledger de aprovação**. Permanecem 110 seleções N64 genéricas com razão explícita.

A restauração isolada recebe `--server-input-dir`, `--runtime-receipt`, `--local-engines`, `--audit-dir`, `--private-dir` e quatro `--documentary-decisions`. Os inputs exatos, helpers e resultados estão no inventário privado `station.r133.private-mode-preparation.v1`. A execução restaurada reproduziu bytes idênticos dos ledgers e recibos; não recompilou APK nem aplicou produção.

[build_research.py](build_research.py) recebe `--audit-dir` e dois `--exclude-research` para os inputs dos revisores N64. Evita conclusões duplicadas de Conker, mantendo as guardas de conteúdo e nomes. `.gitattributes` preserva bytes crus nesta pasta.

## Arquivos

| Arquivo | Uso |
| --- | --- |
| [matrix.json](matrix.json) / [matrix.csv](matrix.csv) | Todas as entradas, estados, contagens e motivos |
| [summary.json](summary.json) | Contagens finais, IDs e proveniência |
| [reviewed-mode-provenance.json](reviewed-mode-provenance.json) | 78 modos e recusas de perfis especiais |
| [documented-modes.json](documented-modes.json) | Modos específicos históricos e adicionais |
| [web-mode-research.json](web-mode-research.json) | Fichas primárias, modos, solo e bloqueios |
| [n64-root-primary-review-original.json](n64-root-primary-review-original.json) / [n64-root-mode-decisions.json](n64-root-mode-decisions.json) | Três manuais e decisões com nomes exatos do catálogo |
| [n64-additional-primary-review.json](n64-additional-primary-review.json) | Seis manuais, 19 modos e recusa de Mythologies |
| [psx-primary-consolidated.json](psx-primary-consolidated.json) | 26 entradas PSX, dez fontes primárias, zero aprovação HLE |

`descriptivePlayerLabel` é apresentação herdada; `historicalApprovedCounts` pode conter política genérica. `documentarySimultaneousCounts` e `documentaryAlternatingIndependentPortCounts` distinguem participação; proveniência detalha casos mistos por modo. `preparedFinalOwnerCounts` reúne apenas os modos produzidos para R133.

## Operação única no servidor

Último retorno real conhecido: `privateKitReceived=false`, `newFinalEnginesActivated=false`, autenticação administrativa nativa obrigatória. O operador deve validar/aplicar **o kit original privado e seu recibo R133**, preservar registros existentes e aplicar os 78 modos com perfis/canonical derivados. Registrar só o motor não cria vínculos por jogo/modo.

O retorno deve identificar modos efetivamente aceitos/recusados. Confirmação vem de resposta autenticada fresca StationMultiplayer v3 com IDs/canonical/modos R133; cache antigo, PONG ou HTTP 200 isolado não confirmam liberação. Código no Git não prova ativação do serviço em RAM.

**Estado:** preparação isolada validada e restaurável; nenhuma implantação, reinício, importação de jogo, alteração de APK, sala real ou gameplay de dois/quatro aparelhos executado por esta auditoria. Confirmação assinada de produção permanece separada.
