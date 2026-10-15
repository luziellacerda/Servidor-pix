# PSX: catálogo completo e revisão por edição — 15/10/2026

## Abrangência

As **84 edições PSX publicadas** estão em [catalogo-psx-completo.csv](catalogo-psx-completo.csv)
e [reviewed-library.json](reviewed-library.json). O JSON liga cada item ao nome,
serial observado, identidade do conteúdo, revisão, capa, artefato de download,
sinopse/metadados e decisões de modos. Não contém caminhos privados do servidor.
Nenhuma edição foi omitida por ser individual ou por apresentar falha.

O [inventário dos discos](disc-inventory.json) registra **93 discos em 84 edições**,
incluindo todos os discos internos dos sete PBP multidisco. A tabela CSV distingue
PBP/CHD/CUE do formato de entrega raw/zip e lista os seriais internos. Isso não
constitui aprovação de troca de disco durante uma partida online.

Esta rodada cobre PSX. O mapeamento integral das demais plataformas será a
próxima rodada solicitada pelo mantenedor. O mesmo handoff principal permanece
em uso; este diretório reúne dados e evidências dessa rodada.

## Situação verificável

**Publicação e provas do servidor concluídas.** Ler [server-return.json](server-return.json)
e os [recibos finais](evidence/completed.json). As 84 edições passaram na leitura
pública de catálogo, nos 84 corpos de capa e nos 84 grants/amostras de download.
Capabilities assinadas passaram local e publicamente para todos os itens;
as 23 modalidades aprovadas criaram sala, e 167 admissões inválidas foram recusadas.
Dois participantes sintéticos passaram por TLS/SPKI, slots 1/2, READY, transporte
do estado real de 4.456.448 bytes e pausa/retomada. Isso não é gameplay Android.

| Cadastro publicado para o motor atual | Quantidade |
| --- | ---: |
| Edições com mapa completo de catálogo, capa e download | 84 |
| Perfis/modos novos, específicos desse motor | 93 |
| Edições com algum modo de duas portas aprovado no ensaio | 19 |
| Modos online de duas pessoas aprovados no ensaio | 23 |
| Modos registrados com impedimento explícito | 17 |
| Edições sem modo de duas portas aprovado, mantidas para jogo local | 53 |

As 53 classificações locais concedem **zero vagas online**. Essa classificação
significa ausência de modo de duas portas aprovado nesta revisão; não afirma
que toda versão histórica do título seja individual. `metadata.players` é
informação descritiva, não autorização de sala.

## Identidade do motor recebido

| Campo | Valor |
| --- | --- |
| engineId | `pcsx-rearmed-ff81ed17-player-v3-1b315b86b4a7` |
| coreSha256 | `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9` |
| runtimeSha256 | `1b315b86b4a789247a355f09eb04bf41027e1cb8e5941019da6ade486835dd36` |
| controllerProfile | `standard-2p-v1` |
| profileSha256 | `81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a` |
| Portas | Standard digital, `[1, 1]`, dois participantes |
| Sincronização nativa | PXL1 sobre o transporte autenticado `station-stream.v3` |
| Inicialização | HLE, opções fixadas pelo runtime recebido, saves novos por sessão |

O Player original continua responsável por emulação, vídeo, áudio e controles.
O servidor fornece conteúdo, autorização, salas e transporte. A API não executa
os jogos. `memcard1=libretro` e `memcard2=shared` são os defaults reais; os arquivos
da sessão ficam no diretório privado novo, sem importar cartões/saves offline.

## Resultados e impedimentos

O **core ARM64 real recebido** foi executado para as 84 edições, em dois processos
QEMU com Bionic AOSP genuíno e um frontend diagnóstico independente. As identidades
de conteúdo corresponderam às publicadas em 84/84; a execução inicial com entradas
iguais coincidiu em 82/84; o ensaio após restaurar estado coincidiu em RAM em 62/84.
Os candidatos online tiveram 2.760 quadros iniciais comparados, dois controles
digitais com entradas diferentes e transferência/restauração do estado real.

Esses testes executam o core; não executam o APK, os hooks GOT/SDL do LibretroPlayer
ou uma partida entre telefones. Não certificam desempenho, todos os níveis,
multidisco durante uma partida ou controles físicos. A validação Android permanece
identificada separadamente nos recibos.

Tekken 3 contém o executável `TEKKEN3\SLUS_004.02`. O core relata `TEKKEN3SL`
porque monta seu identificador com os nove primeiros caracteres alfanuméricos
do caminho; o serial do executável é `SLUS00402`. A diferença foi registrada,
sem renomear o conteúdo nem deduzir que seja uma modificação da ROM. A regra
está em [CheckCdrom do upstream exato](https://github.com/notaz/pcsx_rearmed/blob/ff81ed17a15241d2f3730cdd7585b38e172532ca/libpcsxcore/misc.c#L495).

| Edição/modo | Resultado que impede aprovação deste modo atual |
| --- | --- |
| CTR; Crash Bash USA e PAL; Gran Turismo 1/2; Metal Slug X; Tekken 3; Twisted Metal | RAM divergiu após restauração do mesmo estado no segundo processo. Os resultados originais estão preservados. |
| Tony Hawk's Pro Skater 4 | Divergência já no ensaio inicial com entradas iguais. |
| Final Fantasy IX | O manual consultado remete à ajuda interna; a atribuição das personagens a duas portas ainda não foi comprovada na edição instalada. |
| Worms Armageddon / Quick Multiplayer | Duas equipes humanas foram vistas. No turno da segunda equipe observado, a porta 2 não moveu o personagem; a porta 1 moveu a mira. Esse caminho não comprovou dois controles independentes. |
| Yu-Gi-Oh! Forbidden Memories / 2P Duel | Exige dois cartões com decks previamente preparados, ausentes na sessão nova; também houve divergência de RAM após restaurar estado. |

Spider-Man 2: Enter Electro também divergiu no ensaio inicial, mas já tem
classificação local nesta revisão, sem sala de duas portas oferecida. O ensaio
diagnóstico registra essa falha sem impedir leitura, capa ou download.

Não se deve copiar aprovação de uma engine PSX antiga para este motor. Alterar
opções, BIOS, portas ou binário exige nova identidade e nova verificação. Para
resolver divergências de core, a produção do APK precisa conferir o comportamento
do owner real; uma alteração de cadastro no servidor não corrige esse resultado.

## Modos e instruções

Os modos aprovados têm instruções próprias, incluindo Driver 2 (quatro modalidades),
Final Fantasy IV (Multi), Legend of Mana (2P Control com companheiro), Medal of
Honor (confronto), Densetsu (VS Battle / 1P vs 2P) e Winning Eleven 2002 japonês
(seleção independente dos lados). Não transformar campanha individual em co-op
nem confundir quantidade de lutadores/equipes com celulares conectados.

Ler [CONSUMO-APP.md](CONSUMO-APP.md). A disponibilidade geral da plataforma pode
incluir perfis históricos de outros motores. O app deve selecionar o perfil do
**motor efetivamente instalado**, usando a identidade completa, antes de liberar
o botão Criar sala.

## Evidências e preservação

- [Ensaios das 84 edições](evidence/core-qualification.json).
- [Manuais originais e achados por modo](evidence/research-sources.json).
- [Revisão do programa original e dos controles](evidence/software-mode-review.json).
- [Preparação pelo registrador real e seus selos](evidence/preparation.json).
- [Publicação protegida](evidence/publication.json), [capabilities locais](evidence/signed-local.json)
  e [públicas](evidence/signed-public.json), [mídia pública](evidence/catalog-covers-downloads-public.json)
  e [transporte de dois participantes](evidence/two-participant-public.json).
- [Disponibilidade dos campos de catálogo](catalog-field-completeness.json): três
  sinopses estão vazias; developer/publisher/genre/players/releaseDate continuam
  vazios nos 84 metadados recebidos. Foram preservados, sem inventar informações.
  Esse enriquecimento pertence à próxima rodada de catálogo solicitada.
- [Frontend diagnóstico e método de reprodução](diagnostics/README.md).
- [Preparador específico de PSX](prepare_psx_online.py) e [regressões](test_review_policy.py).

A publicação é aditiva pelo registrador vigente: preserva motores, perfis e modos
anteriores, catálogo, IDs, capas, ROMs, saves, licenças reais e demais serviços.
Não requer trocar DLL ou reiniciar a API. O registrador administrativo exige
autenticação nativa do Linux, baseline fresca, `scan.lock`, selos e backup.
As cópias estáveis anteriores foram publicadas nos dois Gits antes do trabalho.
ROMs, estados, screenshots, manuais, ELF, chaves e contas de teste ficam privados.
