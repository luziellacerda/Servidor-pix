## Android recusou atualização R144 no A56 por armazenamento

O instalador conferiu o APK assinado, ociosidade e baseline R143 real no A56. Duas tentativas não confirmaram instalação; a segunda registrou `INSTALL_FAILED_INSUFFICIENT_STORAGE`. A primeira leitura posterior confirmou APK R143 ainda instalado e cerca de 6,42 GB disponíveis em /data, sem jogo/download ativo. Esses dados não garantem espaço temporário suficiente para atualizar o APK grande. O mantenedor informou que irá liberar espaço; não remover jogos/saves/app nem enfraquecer guards por hipótese. Instalação e aparência R144 permanecem pendentes até recibo posterior concluído.

## Histórico preservado

## R144 visual pronta para teste em 13/10/2026

O mantenedor pediu ondas mais visíveis e cores por sistema no menu principal; depois especificou que as salas online devem conservar a aparência atual e receber apenas mais brilho. A R144 atende à correção: paletas somente no carrossel; salas mantêm RGB/tema/desenho e alteram apenas 12 transparências.

APK R144 `01427f4e73141b7d6a462c2420a9abeb018a8716102e7fb959bf9a1d69eccee0`, 2.715.024.399 bytes, fonte congelada `1ac3f7d2fdd7cd349a5b37127b66712834e844fd`, certificado original v2/v3 e alinhamento 16 KiB. Seis substituições, zero adições/remoções, 13.854 entradas preservadas e 39 DEX exatos. DEX35 inteiro mantém gameplay, saída, g52 e multitap R143; a família HTTP de downloads R141 é preservada em DEX28. Fonte nova `integration/wave-palette-r144-20261013-final`; preparo 81e4ec0 ficou histórico sem APK.

O carrossel R133 foi recompilado byte exatamente e os dois rótulos R134 foram reaplicados para recuperar o carrossel R143 real antes do overlay. Apenas o seletor de cores das ondas mudou, com cache curto de chave completa. Três faixas/66 vértices, tempo, curvas, máscara diagonal, nuvens, shaders e LEDs permanecem exatos. Nativa R144 `67180bdd145cd03f456249b31b9163ee0baa3e66fb10b909e20831823accea10`, 160.555.568 bytes; 33 exports, dependências/SONAME e LOAD 16 KiB conferidos. Java mantém 34 ms, 24 amostras, Paint/Path, lifecycle, tema e pausa fora de foco; 271 outras classes exatas e inversão integral do método confirmada.

| Sistema | Ondas do menu principal |
| --- | --- |
| SNES e BR | Três tons de vermelho |
| Nintendo 64 e BR | Verde, azul, vermelho |
| GameCube | Três tons de roxo |
| Wii | Branco, azul, branco |
| Wii U | Azul, branco, azul |
| Switch | Vermelho, azul, branco |
| PSX | Vermelho, cinza, verde |
| PS2 e BR | Azul, branco, verde |
| PSP e BR | Azul, branco, cinza |
| Mega Drive e BR | Azul, vermelho, cinza |
| 32X | Vermelho, azul, laranja |
| Saturn | Cinza, azul, branco |
| Dreamcast | Laranja, branco, amarelo |
| Naomi e Naomi 2 | Azul, branco, laranja |
| Neo Geo | Amarelo, azul, branco |
| CPS 1, 2 e 3 | Azul, amarelo, branco |

No carrossel, opacidades B0/A0/90. Sistemas não citados mantêm RGB do tema e também recebem essas opacidades. Salas online: topos 20/18/18→60/58/50; rodapés 08/06/05→28/24/20, apenas alpha em hex. Não há novas cores de plataforma na sala nem alteração de StationRoomsActivity/StationHyperspaceView ou preferência. Ganho físico de CPU/FPS não foi medido; o desenho e a agenda antigos são preservados e a consulta de aliases só é refeita quando a chave muda.

A R144 é candidata visual para teste. A tag/backup estáveis R143 e ponteiros permanentes permanecem intactos. Instalador USB explícito aponta a R144 com BAT/PS byte exatos. Há instalação A56 em acompanhamento; publicar conclusão apenas com recibo de hash e dados. Não usar estado de montagem como prova visual. Última fonte instalada observada antes da tentativa A56 é R143, SHA508480e3, apesar do recibo anterior ter R141 como último conhecido.

Quatro IDs independentes foram renovados pelo vínculo obrigatório com DEX28/carrossel, sem mudar os owners/cores/opções/controles ou os nove descritores RetroArch. Kit R144 `7b1cdb6c68a1ddcd05af2e4703d2900061f7f2c4082a3f3c80c6d4c428d18739`, 12.164.472 bytes, 38 arquivos, seis bibliotecas/vinte dependências, apenas mesmos 78 modos revisados. Kit preparado, não aplicado; SNES conserva cadastro existente. Não reaplicar R141, migrar modos duas vezes, alterar licenças ou implantar/reiniciar API. PS1 zero novas aprovações HLE. [Provas e estado](r144/STATUS.json).

## Histórico preservado

## R143 promovida a estável pelo proprietário em 13/10/2026

Tag e ramo `estavel-station-r143-20261013`. [Checkpoint completo](estavel-station-r143-20261013/README.md) fixa fonte/APK, histórico, recibos e retorno. Top Gear VS3 e Bomberman confirmados por relatos; quatro/cinco telefones e kit independente R143 permanecem pendentes. Backup completo do ponto na raiz F existente; ponteiros permanentes promovidos pelo procedimento verificado. Nenhum novo código/APK/deploy por esta promoção.

## Histórico preservado

## R143 — Bomberman também funcionou no teste relatado, 13/10/2026

O mantenedor confirmou “bom ber man tabem ok” após o sucesso de Top Gear 3000 com três jogadores. Registrar um segundo resultado positivo de jogo, agora para Bomberman. A captura anterior mostrava Super Bomberman 3 selecionado na sala; o relato atual não repetiu o título exato, modo ou quantidade de participantes. Não atribuir automaticamente três/quatro/cinco jogadores a este novo resultado. [Recibo do relato e limites](r143/evidence/bomberman-human-result.json).

Mesmo APK R143 instalado no g52, mesma fonte congelada e mesmos motores. Esta atualização registra evidência humana; não modifica código, gera APK ou aplica cadastro no servidor. Top Gear 3000 Corrida VS com três participantes continua confirmado pelo mantenedor. Testes físicos com quatro/cinco participantes, controles instrumentados e estabilidade prolongada continuam pendentes.

## Histórico preservado

## R143 — teste humano com três jogadores concluído, 13/10/2026

Após o teste solicitado de Top Gear 3000 em Corrida VS, o mantenedor respondeu primeiro “entrou 4 jogadores” e depois confirmou “jogou 3 jogadore”. Registrar sucesso relatado de jogo com **três participantes** e desbloqueio da seleção. A primeira resposta não comprova quatro telefones humanos. [Resultado e limites](r143/evidence/three-player-human-result.json).

A R143 permanece instalada no g52, com APK/fonte e preservação de dados conferidos. Na leitura passiva posterior, o g52 já estava no frontend em uma sala de Super Bomberman 3, sem processo station_netplay ativo; não houve captura da corrida ou medição dos controles independentes. Captura privada preservada. Resultado humano não equivale a instrumentação Android nem homologa outros jogos, quatro/cinco jogadores físicos, estabilidade prolongada ou queda de sinal. Avisos JNI anteriores continuam sem causa isolada.

A correção localizada remove a duplicidade da opção que impedia a leitura do remapeamento multitap SNES. Motores, downloads, saída, FPS, READY/ACK e demais métodos permanecem conforme os recibos. Kit dos quatro marcadores independentes preparado e ainda não aplicado; SNES mantém cadastro existente. APK de testes e instalador continuam R143. Nenhuma recompilação para registrar este resultado.

## Histórico preservado

## R143 instalada e frontend observado no g52 — 13/10/2026

APK R143 `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, fonte `26257869b59af8ecea44932ab287705990a9acbe`, instalado no moto g52. Hash integral/UID/dados/primeira instalação preservados e frontend oficial observado. Aparelho ocioso, sem jogo/download/serviço antes da atualização; nenhum force-stop ou limpeza de dados nesta instalação. [Recibo](r143/evidence/installation-moto-g52.json).

O leitor real confirmou que a opção duplicada introduzida naR142 manteve a primeira ocorrência false. A R143 substitui essa linha por true uma vez, apenas para o perfil multitapSNES aprovado. DownloadsR141/HLE/saída/motores/FPS/READY e demais métodos preservados. A pesquisa web e testes de parser estão no registro abaixo; não atribuir causa por analogia com o issue externo.

Teste humano solicitado: g52 anfitrião, Top Gear3000 Corrida VS3, seleção3 e comando independente do terceiro carro. Resultado ainda pendente; quatro jogadores não testados. A56/Edge30 permanecemR141, core/runtime/perfilSNES exatos; não inferir gameplay por igualdade de hashes. Kit independenteR143a4ba2105 preparado e aguardando cadastro dos quatro marcadores renovados; SNES não requer registro novo. Não deploy/restart, nem promover recuperação permanente. BAT de testes R143, scripts preservados; fonte/recibos noGit e APK/registros/kit privados no backup F existente.

## Histórico preservado

## R143 — pesquisa web e correção da flag duplicada, 13/10/2026

**R142 falhou no teste físico de Top Gear 3000 Corrida VS3.** O mantenedor confirmou que não consegue selecionar3; captura e logs privados preservados. Não homologar a R142 com base no teste host anterior. A pesquisa encontrou [relato semelhante RetroArch10424](https://github.com/libretro/RetroArch/issues/10424), [multitap na porta2 no core](https://docs.libretro.com/library/bsnes_mercury_performance/) e [distinção entre dispositivos locais e solicitações da rede](https://docs.libretro.com/guides/netplay-multiple-controllers/). Relato histórico e workaround não comprovam a causa deste aparelho nem justificam mudar servidor/READY/motor.

O cruzamento encontrou um erro introduzido pela R142: a configuração continha auto_remaps_enable=false e uma segunda ocorrência true. O parser do runtime preserva a primeira chave. Seis corpos reais do parser e sua tabela hash executaram três cenários: false→true continua false; true único habilita; false único permanece false. Headers/fonte conferidos contra o manifesto correspondente. Inclusões/referências foram stubs que falham, nunca chamados; tokenizer adaptado ao CRT Windows no fixture. Teste não leu configuração privada Android nem executou corrida.

A R143 substitui a linha false existente por true uma vez, somente no helper SNES/perfil multitap assinado. Ausência da política recusa a preparação. Arquivo .rmp, vetor[1,257,1,1,1], guardas, modos e métodos originais preservados. Fonte final congelada `26257869b59af8ecea44932ab287705990a9acbe`, APK `508480e3671ae924f037bd32f6d71efda7a9d905c45c7b2571bd9f219add83d4`, 2.715.024.399bytes, assinatura originalv2/v3/16KiB. [Estado e recibos](r143/STATUS.json).

Cinco substituições, zero adições/remoções,39DEX e13.855entradas preservados. DownloadsR141, opçõesHLER140, saída, core/runtime, FPS, botões, transporte, READY/ACK e perfis exatos. Preparações d6479ab/b02a080 e a montagem recusada permanecem para auditoria: o assembler gerou nomes de branch/goto no mesmo endereço; o guard final unifica somente rótulos consecutivos sem instrução antes de comparar todas as instruções/destinos. Nenhum APK anterior da R143 foi instalado.

SNES conserva seus IDs/perfil e não requer cadastro novo. Três marcadores/quatro IDs independentes foram renovados por vincularem DEX35; kit privadoR143 `a4ba210580ea68beb0086c2c986c6a884e888a1e9ad599bd7e9d9ae33364f606`,12.161.998bytes,38arquivos,6bibliotecas/20dependências, renova apenas os mesmos78modos. Preservar cadastrosR141/anteriores e usar a lista fixa corrigida, sem migração duplicada. PS1 zero novas aprovaçõesHLE. Kit preparado não é cadastro aplicado; nenhuma API deploy/restart.

Instalação R143 no g52 em andamento; publicar estado concluído somente com SHA/UID/dados/primeira instalação conferidos. A56/Edge30 continuamR141. Teste humano3/4 permanece necessário. Os logs atuais também contêm avisos JNI StackOverflowError na impressão de exceções, sem stack causal; investigação separada, sem atribuir à configuração ou alegar ANR corrigido. Capturas/logs/seriais/chaves/ROMs/BIOS/binários privados foraGit. BAT de testes aponta R143 com scripts byte exatos; ponteiros permanentes intactos.

## Histórico preservado

## R142 instalada no moto g52 — 13/10/2026

APK R142 `f99b51a04f4967c086533157835868dcc6a22a3eb06eb63706886f9f60453b83` instalado no moto g52 e hash integral conferido; UID, dados e primeira instalação preservados. Fonte executável congelada `9a38881f467ee533b4f8bcff3ea6769b98401175`. [Recibo](r142/evidence/installation-moto-g52.json).

As duas primeiras tentativas recusaram o serviço residual antes de instalar. Após o mantenedor sair da partida, não havia emulador filho nem DownloadService: restava somente o ForegroundService próprio. O Android recusou parar diretamente esse serviço; o aplicativo já fora da partida foi encerrado, sem apagar dados, e as verificações originais de repouso passaram. A terceira tentativa instalou. O instalador não comprovou visualmente o frontend; a abertura e o teste real dependem do usuário. [Preparação sanitizada](r142/evidence/installation-preparation-g52.json).

Teste solicitado: g52 como anfitrião, Top Gear 3000 Corrida VS com três participantes, opção3 e comando independente do terceiro carro. A56 e Edge30 permanecem R141 neste momento; SNES/RetroArch/core/perfil da sala são byte exatos entre R141/R142. Não inferir corrida válida ou sincronismo por essa igualdade; o primeiro teste físico está pendente. Atualizar outros aparelhos autorizados quando disponíveis, sem interromper partidas/downloads. Quatro IDs independentes R142 e seu kit continuam aguardando cadastro; este reparo de SNES conserva cadastro válido e não requer alteração no servidor. Downloads R141 e reparos anteriores preservados; fontes/recibos no Git, APK/kit/registros privados no backup F existente.

## Histórico preservado

## R142 — candidato para 3/4 jogadores SNES, 13/10/2026

Relato atual: Top Gear 3000, Corrida VS, três participantes na mesma sala, opções 3/4 escuras e indisponíveis. O g52 registrou dois canais v3 (posições 2 e 3), ambos em estado 2. Capturas e logs privados preservados. A conexão da sala não comprova que o jogo recebeu o multitap nem que existe corrida humana jogável.

Foi confirmada uma falha de entrega no aplicativo: as chaves de tipo de dispositivo eram gravadas em retroarch.cfg, enquanto o leitor do runtime exato as consome em arquivos .rmp. A R142 entrega o vetor assinado `[1,257,1,1,1]` em um arquivo de controles privado da sala, somente para SNES com `snes-multitap-port2-v1`. Um teste host dos corpos reais do leitor/inicializador confirmou dispositivo257 na porta2 sem mudar botões/portas/turbo sob fixtures controladas; Android e gameplay ainda precisam teste. [Escopo, receita e limitações](https://github.com/luziellacerda/TurboElden/blob/9a38881f467ee533b4f8bcff3ea6769b98401175/integration/snes-multitap-delivery-r142-20261013/README.md).

APK R142 `f99b51a0…`, fonte congelada `9a38881f467ee533b4f8bcff3ea6769b98401175`, assinado original v2/v3/16KiB. Cinco substituições, zero adições/remoções, 13.855 entradas e 39 DEX preservados. DEX28 de downloads R141 exato; runtime RetroArch, core SNES, HLE R140, saída humana, FPS, transporte, READY/ACK, catálogo e perfis exatos. Campeonato em dupla e Super Mario Kart continuam dois; Top Gear Corrida VS `[2,3,4]`, nunca cinco.

Os nove descritores/oito IDs RetroArch permanecem: este ajuste de SNES não requer mudança no servidor. Três marcadores de composição e quatro IDs independentes foram renovados por vincularem DEX35; o kit privado R142 SHA `3163f54225860816be10fd4388534dfba655261f8946a82c4853c71eb2832cc2`, 12.160.430bytes, 38arquivos, seis bibliotecas/vinte dependências, está preparado para renovar somente os mesmos 78 modos revisados. Preservar todos os cadastros R141/anteriores; usar o preparador de lista fixa corrigido, sem duplicar a migração genérica. PS1 zero novas aprovações HLE. Kit preparado não é cadastro aplicado. Não deploy/restart por consequência. [Estado](r142/STATUS.json).

Instalação aguarda g52 ocioso, pois ainda estava no jogo no último preflight. Manifesto do BAT de testes aponta explicitamente para R142, scripts preservados; ponteiros permanentes de recuperação intactos. Nenhuma prova de gameplay 3/4 é inferida da compilação ou do teste host. Binários/ROM/BIOS/chaves/seriais/capturas privadas foraGit.

## Histórico preservado

## Edge 30 atualizado: três aparelhos na mesma R141 — 13/10/2026

Motorola Edge30/Android14 atualizado da versão temporária R139-DIAG para o mesmo APK R141 `b5a76023…`, fonte executável `240b7c05…`. SHA integral instalado, assinatura original, UID, dados e primeira instalação preservados. Logger/sidecar temporários removidos pela composição final já conferida. Nenhuma recompilação, mudança adicional de lógica ou limpeza de dados. Moto g52, Samsung A56 e Edge30 agora compartilham exatamente a R141. [Recibo Edge30](r141/evidence/installation-edge30.json). Cadastro administrativo dos quatro IDs e gameplay dependem das provas correspondentes; instalação não equivale a uma partida validada.

## Histórico preservado

## R141 recebida e cadastrada no servidor — 13/10/2026, 17h19

**Cadastro aplicado aos bytes reais da R141 `b5a76023…`.** O kit privado `d8d9746d…` tem 12.842.618 bytes e 38 arquivos; seis bibliotecas ELF64 Aarch64 e vinte dependências conferidas. Foram acrescentados quatro motores e **78 vínculos dos modos já revisados em 44 jogos: 66 N64, nove GameCube e três Wii**. PS1 recebeu o motor e conserva zero aprovações HLE novas por edição. [Recibo aplicado, preservação e provas assinadas](r141/server-return.json).

Totais atuais: **39 registros de motor, 11.604 perfis, 11.583 aprovações administrativas, 11.571 aprovações vinculadas ao conteúdo atual e 6.743 modos**. Os 35 motores, 11.526 perfis e 6.665 modos anteriores foram preservados como prefixos exatos. Catálogo 46 continua com 4.690 IDs, 4.435 visíveis e 255 aliases. Os oito IDs ativos RetroArch e seus nove descritores permanecem válidos. DLL `ef5270b3…`, PID 2867363 e licenças preservados; a API permaneceu ativa durante o cadastro. Três plataformas tiveram bindings assinados conferidos em loopback e HTTPS público; as seis recusas de conteúdo/motor/contagem incorretos passaram. Identidades de teste foram removidas.

A validação detectou que o preparador de renovação adicionava os modos das aprovações nativas existentes junto com a mesma lista documental, criando duplicações. O gate recusou a prévia antes de qualquer gravação. O preparador de lista fixa agora usa somente os 78 modos revisados e mantém todos os registros antigos. A migração genérica conserva o comportamento anterior. Dezessete testes passaram, incluindo uma renovação após cadastro anterior; preparação com dados atuais confirmou exatamente 78 modos e 78 perfis novos. Essa correção é administrativa e não altera código de gameplay.

O recibo da produção comprova no moto g52 **546.776.387 bytes baixados em 75.288 ms: média 7,26 MB/s decimal**, preparação de 5.862 ms, instalação concluída e serviço de download ausente na leitura posterior. O contador final soma transferência e expansão; não representa outra transferência do mesmo tamanho. A observação de velocidade anterior não teve comparação pareada. CA/hostname/pin e contrato de download seguem os recibos publicados; o destino de socket não foi observado separadamente na captura Android.

Somente o GET canônico de artefato usa a origem de downloads. Autorização, login, catálogo, capas e comandos seguem a origem anterior. A R141 conserva o DEX35 e os dois ajustes de opções da R140; os hashes por download Android continuam desativados conforme a política vigente. O cadastro está concluído, mas **gameplay de Top Gear e outros jogos não foi homologado por estes testes**. Os recibos agora confirmam moto g52 e Samsung A56 na mesma R141 `b5a76023…`; o A56 foi atualizado da R138 efetivamente encontrada, com SHA, UID, dados e primeira instalação preservados. O frontend do A56 não foi observado pelo instalador, e gameplay permanece sem prova. Edge 30 conserva a última R139 de diagnóstico comprovada. Cancelamento e nova autorização no Android, teste online com composição igual e diagnóstico dos dois relay409/falha antiga de saída continuam pendentes nos respectivos setores.

## Histórico preservado

## Samsung A56 também atualizado para R141 — 13/10/2026

Samsung SM-A566E/Android16 atualizado da R138 realmente encontrada para o mesmo APK R141 `b5a76023…`, fonte executável `240b7c05…`. SHA integral instalado, assinatura original, UID, dados e primeira instalação preservados. Nenhuma recompilação, alteração de código/configuração ou limpeza de dados. Moto g52 e A56 agora compartilham a R141; Edge30 permanece na última versão temporária de diagnóstico conhecida. [Recibo A56](r141/evidence/installation-samsung-a56.json). A primeira tentativa recusou o baseline histórico R135 sem instalar; a segunda confirmou a R138 atual e atualizou com as mesmas proteções. Instalação não comprova gameplay ou cadastro dos quatro novos IDs.

## Histórico preservado

## R141 concluída no app: downloads diretos com reparos R140 preservados — 13/10/2026

Fonte240b7c05, APKb5a76023/2.715.008.015 bytes, assinatura original v2/v3/16 KiB e todas13.860 entradas conferidas. Moto g52 atualizado, SHA integral/UID/dados/primeira instalação preservados. Download Android autorizado200/artefato200 concluiu:546.776.387bytes/75.288ms (~7,26MB/s), preparação5.862ms, INSTALL_FINISHED e serviço de download ausente depois. Operador percebeu mais que o dobro da velocidade; comparação controlada antiga/nova não foi executada. Fonte seleciona o host direto; Host/socket independente não foi observado. Leia [resultado/contratos/evidências](r141/README.md).

Cinco substituições,13.855entradas/39DEX preservadas; DEX35 R14049f381 e as duas flags, runtimeae72/noveRetro, HLE/cores/cache/FPS/controles/READY exatos. BAT/PowerShell preservados e manifestoR141. Kit privado entregue no USB SERVIDOR LER:12.842.618bytes/SHAd8d9746d,6libs/20deps/4novosIDs/incomingv3/referências78modos. Esses quatro IDs aguardam cadastro real próprio, preservando todos os prefixos e aprovações existentes; não usar recibo e696 para esta combinação. Oito IDs RetroArch/SNES existentes mantidos. Nenhuma API reiniciada/deploy ou cadastro aplicado por este PC. Edge30 últimaDIAG, A56R135; R139 segue referência de recuperação.

Não foi ampliada a lógica de partidas neste setor. Relay409 sem código conhecido e testes de gameplay/perda de sinal continuam nas pendências anteriores, para retomada posterior conforme o mantenedor. Download Android cancelamento/regrant e ratio pareado de velocidade continuam sem teste nesta tentativa. Capturas brutas/binários/chaves/seriais fora do Git; documentação e receitas atuais nos dois repositórios.

## Histórico preservado

## R141 preparada: downloads diretos conservando os reparos R140 — 13/10/2026

Retorno de downloads app4478e49/servidorc8817a2 lido junto ao retorno online app73a39fc/servidordceb9a4. APK USB e69616ee parte da R139 e retiraria DEX35 corrigida da R140; não foi instalado. A composição solicitada usa somente a origem HTTP revisada sobre o donor R14061e873e7, com DEX35 49f381dc exata. R140 está instalada no moto g52 e sua reconstrução foi preservada no backup F existente; Edge30 última R139-DIAG conhecida, A56 R135. Nova fonte R141 ainda na preparação, sem assinatura/instalação/cadastro. Cinco substituições/13.855 entradas preservadas, fila/cache/FPS/cores/HLE/READY exatos. Quatro novos IDs independentes da combinação exigem bytes e recibos próprios; cadastro do e696 não se aplica à R141. Leia [estado, contratos, provas e pendências](r141/README.md). Download Android e velocidade pendentes. Relay409 permanece sem motivo; proposta de captura segura separada, sem mudança de proteção ou logging nesta composição.

## Histórico preservado

## Diagnóstico USB de Top Gear recebido e conferido — 13/10/2026

O pacote `Diagnostico-TopGear3000-R139-20261013.zip` foi recebido: 53.620 bytes, SHA256 `43ed435a1d0540d440f28b53de1403e305819ddf3e8e424f0dba156ab4387e9a`. Os onze arquivos listados conferem com o manifesto. Moto g52 e Edge 30 têm capturas de VS e Campeonato: quatro tentativas separadas, cada aparelho como anfitrião, sem captura simultânea de uma mesma rodada. [Diagnóstico e cruzamento com a API](r139/server-phone-diagnostic.json) · [Retorno consolidado](r139/server-return.json).

| Aparelho / modo | Erro de carga observado, UTC | Resultado |
| --- | --- | --- |
| moto g52 / VS | 18:18:47.088 | Retorno nativo 1 às 18:18:47.104 |
| moto g52 / Campeonato | 18:20:58.226 | Retorno nativo 1 às 18:20:58.239 |
| Edge 30 / VS | 18:32:53.434 | Retorno nativo 1 às 18:32:53.445 |
| Edge 30 / Campeonato | 18:33:56.777 | Depois, erro de menu; sem READY e sem bytes nativos nos registros |

Em todos os quatro testes, o bsnes pede `dsp4.program.rom` e `dsp4.data.rom`, informa que faltam e falha ao carregar o conteúdo. O [código do core no pin recebido](https://github.com/libretro/bsnes-mercury/blob/79d7f9de218b/target-libretro/libretro.cpp) confirma que a ausência de arquivos solicitados marca `load_request_error` e faz a carga retornar falsa. Isso explica a falha imediata do jogo. Não houve novo crash nesses quatro recortes; a ocorrência anterior de RenderThread continua separada.

A composição declara `bsnes_violate_accuracy="enabled"` e `bsnes_chip_hle="HLE"`. O launcher escreve o arquivo de opções da sala; o [resolvedor do runtime no pin recebido](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/runloop.c) permite selecionar opções anteriores de jogo, pasta ou core. A correção mínima candidata relatada pela produção é definir, apenas na configuração privada online:

```ini
global_core_options = "true"
game_specific_options = "false"
```

A revisão estática confirma esse caminho de seleção. O arquivo legado específico e os valores efetivos no core do telefone não foram lidos, portanto a origem particular da sobreposição ainda não está comprovada. Durante esta leitura chegou o recibo dos [vinte casos do resolvedor no host](r139/startup-logging-diagnostic/evidence/host-resolver-qualification.json), SHA256 `42db9aa7537486f15b555987ac05d0d4850de7b34af9ffa5063a014438bfc821`, que confere com o gate da R140. Os três trechos extraídos correspondem aos corpos do pin upstream; o arquivo produtivo completo tem hash diferente. Nos cinco estados testados, as duas chaves selecionam diretamente as opções da sala. A descoberta e o gerenciador foram controlados; a produção executou os vinte casos, este servidor leu e conferiu o recibo, e eles não executam bsnes, Android ou gameplay. Confirmar a candidata nos aparelhos preservando HLE, cores, controladores, perfis, READY/ACK, gerações e FPS.

O journal correspondente tem seis upgrades HTTP101, duas recusas HTTP409 do relay (18:18:46.957 e 18:32:53.185 UTC), uma recusa de geração antes do segundo início, duas recusas por estado irrecuperável e um HTTP429 do lobby fora dos quatro inícios. Os motivos das duas recusas do relay não foram registrados; obter somente o código seguro da resposta no próximo teste. Não inferir causa ou ordenar eventos entre computadores por milissegundos: as calibrações fornecem intervalos. O PID continua 2867363, NRestarts 0; não há marcador de falha fatal não tratada no recorte. Os registros não comprovam ausência de todo problema de transporte nem uma partida funcional.

APK de diagnóstico `5aaa6db5…` é temporário; runtime `ae72f137…` e descritores RetroArch permanecem os da R139, conforme recibos recebidos. Os quatro marcadores independentes temporários não foram cadastrados. Logs privados foram preservados; nenhum firmware, APK, perfil, configuração do telefone ou serviço foi modificado nesta leitura. A candidata e o gameplay continuam pendentes. O recebimento atual supersede as pendências históricas de envio do Edge 30 abaixo.

### Candidata R140 recebida durante a leitura

A [fonte e receitas R140 `b93623b`](https://github.com/luziellacerda/TurboElden/tree/b93623b708bafe151441d3e4920acad51718e118/integration/core-options-binding-r140-20261013) usam a R139 de release como base, sem incluir o logger temporário. A mudança prevista é adicionar as duas chaves no launcher privado, preservando os nove descritores RetroArch e os oito IDs ativos; SNES continua com seu motor já cadastrado. Os três marcadores vinculados ao DEX renovarão os quatro IDs independentes PS1/N64/GameCube/Wii: só cadastrar esses novos IDs após receber os bytes compilados exatos e seus recibos. A fonte preparatória não entrega um APK compilado, assinado ou instalado. Nenhuma dessas quatro identidades foi inventada ou liberada nesta análise.

## Histórico preservado

## Diagnóstico temporário R139 coleta e entrega concluídas 13/10/2026

**Quatro tentativas de Top Gear 3000 capturadas, com a falha de carga registrada nos dois modelos. A correção ainda está em preparo.** O APK temporário `5aaa6db5199a450894a7ac2b40776732d9da5f42b606bcf1faf73ed14671076d` / 2.715.028.584 bytes, [fonte congelada `7ca5e5c8`](https://github.com/luziellacerda/TurboElden/tree/7ca5e5c8f8c93de4451830413318a0172a30ac91/integration/startup-logging-diagnostic-r139-20261013), foi instalado e conferido no moto g52 e no Edge 30. Hash integral, UID, dados e primeira instalação preservados; frontend observado. Ambos continuam com essa versão de diagnóstico como última instalação comprovada; a restauração da R139 original `19acdece…` está pendente. [STATUS](r139/startup-logging-diagnostic/STATUS.json) · [Instalação g52](r139/startup-logging-diagnostic/evidence/instalacao-moto-g52.json) · [Instalação Edge 30](r139/startup-logging-diagnostic/evidence/instalacao-edge30.json).

RetroArch `ae72f137…`, cores, HLE, controladores, READY/ACK/FPS e os nove descritores RetroArch permanecem exatos; os oito IDs ativos continuam os cadastrados. Os quatro IDs independentes temporários PSX/N64/GameCube/Wii não foram cadastrados. A release R139 e seus ponteiros estão preservados. [Assinatura original v2/v3 e alinhamento 16 KiB](r139/startup-logging-diagnostic/evidence/signed-apk-proof.json) · [Probe Android de sete casos](r139/startup-logging-diagnostic/evidence/android-sidecar-probe.json) · [Preservação do pacote](r139/startup-logging-diagnostic/evidence/package-review.json).

| Aparelho e modo | Falha DSP4 em UTC | Resultado observado |
|---|---|---|
| moto g52 VS | 18:18:46.980 | Carga falhou; runloop retornou 1 às 18:18:47.104 |
| moto g52 Campeonato em dupla | 18:20:58.122 | Carga falhou; runloop retornou 1 às 18:20:58.239 |
| Edge 30 VS | 18:32:53.379 | Carga falhou; runloop retornou 1 às 18:32:53.445 |
| Edge 30 Campeonato em dupla | 18:33:56.724 | Carga falhou; erro adicional de inicialização do menu; nenhum marcador de retorno nativo observado |

As quatro tentativas informaram ausência de `dsp4.program.rom` e `dsp4.data.rom`; não houve novo crash nas capturas. No Campeonato do Edge, a segunda Activity reutilizou o mesmo processo: registrou `Unable to init menu driver`, `nativeStatus=-1`, `localReady=false`, `readySent=-1`, TX/RX nativos zero e PONG do relay. A tela continuou esperando; após tocar em Sair, o usuário confirmou retorno às salas. Esse retorno particular não homologa toda a saída online. As quatro capturas foram separadas por aparelho/modo e **não constituem uma captura simultânea do par**; horários usam os intervalos de calibração registrados no STATUS.

O Java do APK exato escreve as opções HLE corretas no arquivo da sala, mas fixa somente `core_options_path`. A [qualificação do resolutor em 20 casos no host](r139/startup-logging-diagnostic/evidence/host-resolver-qualification.json) confirmou que opções existentes de jogo, pasta ou core podem anteceder esse caminho. O arquivo `.opt` particular dos telefones não foi lido; a origem efetiva da opção LLE nesses aparelhos ainda não foi atribuída. Está em preparo uma candidata que fixa `global_core_options=true` e `game_specific_options=false` para vincular o arquivo da sala, preservando os valores HLE e perfis atuais. **Essa candidata não foi compilada, instalada ou validada em partida.**

**Entrega privada concluída em `SERVIDOR LER`:** `Diagnostico-TopGear3000-R139-20261013.zip`, 53.620 bytes, SHA256 `43ed435a1d0540d440f28b53de1403e305819ddf3e8e424f0dba156ab4387e9a`, cópia conferida às 18:42:41.734 UTC. Contém os três arquivos solicitados por modo/aparelho, com apenas registros relacionados ao app; logs brutos de outros aplicativos, APKs, ROMs, BIOS, chaves e seriais ficaram fora da entrega pública. [Recibo sanitizado de entrega](r139/startup-logging-diagnostic/evidence/entrega-diagnostico-topgear-r139.json). A API, os cadastros e o runtime nativo não foram alterados nesta etapa. Resta reconectar os telefones para a restauração ou para uma compilação corretiva qualificada, conforme a operação autorizada; gameplay continua sem validação.

## Histórico preservado

## Quatro motores independentes publicados — 13/10/2026, 14h06

**Autenticação Linux concluída e cadastro aplicado**, sem reiniciar a API. Seis bibliotecas e vinte dependências exatas obtidas pelo USB do moto g52 permitiram cadastrar os motores atuais PS1/N64/GameCube/Wii. Foram acrescentados **78 perfis e modos revisados em 44 jogos: 66 N64, nove GameCube e três Wii**. PS1 recebeu o motor, mas conserva zero aprovações HLE por edição nesta etapa. [Recibo aplicado e provas](r139/native-owner-usb-receipt.json) · [Retorno consolidado](r139/server-return.json).

Produção: catálogo 46/4690 IDs/4435 visíveis/255 aliases; **31 motores, 11448 perfis, 11427 com aprovação administrativa, 11415 vinculados ao conteúdo atual e 6587 modos**. Prefixos anteriores de 11.370 perfis e 6.509 modos preservados. Capabilities assinadas verificadas localmente e por HTTPS público para N64/GameCube/Wii; seis recusas por conteúdo/motor incorretos ou cinco jogadores no N64 foram confirmadas. API continua PID2867363/DLLef5270b3…/NRestarts0; licenças reais preservadas. Homologação física e o erro de início de Top Gear continuam pendentes.

O bloqueio inicial foi apenas alteração de formatação do manifesto pelo scanner, com conteúdo lógico idêntico; o candidato foi regenerado e qualificado contra o estado atual antes de gravar. A rotina local também foi corrigida para aceitar o timer parado durante a transação. Os registros abaixo de cancelamento/preparo descrevem tentativas anteriores, superadas por este recibo.

### Arquivos solicitados à produção do APK

As bibliotecas anteriormente ausentes já foram recebidas e cadastradas. Para localizar o erro interno de carga de Top Gear, solicitar registros dos dois celulares desde “Iniciar partida” até falhar, com estes **nomes sugeridos de entrega**: `logcat-topgear3000-vs.txt`, `logcat-topgear3000-campeonato.txt` e `erro-inicializacao-retroarch.txt`. Incluir o erro real do carregamento RetroArch/bsnes/core/driver que explica `runloop-return result=1`, preservando controles/HLE/READY/FPS. Transferência privada pelo pen drive em `SERVIDOR LER`; nenhum dado pessoal/log bruto no Git.

A raiz do pen drive contém `content-authority-envelope.json`, `content-authority-issuer.spki.der`, `suite-authority-envelope.json` e `suite-authority-issuer.spki.der`: identificam TURBORAMA_SUITE e não foram aplicados ao produto TURBORAMA_STATION_ANDROID. O kit R139 em `SERVIDOR LER` coincide byte exato com o já recebido. Esses arquivos de autoridade não são catálogos, BIOS ou bibliotecas de emulação.

## Histórico preservado

## Retorno do servidor e diagnóstico USB — 13/10/2026

**R139 RetroArch recebida, cadastrada e conferida em produção.** O kit privado enviado no USB foi verificado: cinco bibliotecas e oito motores. O cadastro preservou os registros anteriores e acrescentou 3.091 perfis; assinaturas novas confirmaram SNES, Mega Drive, Neo Geo/CD, FBNeo e CPS1/2/3, incluindo Battletoads, Mario Kart e Top Gear 3000. [Retorno aplicado](r139/server-return.json) · [Vínculos assinados](r139/server-signed-bindings.json) · [Matriz completa dos 4.690 registros](r139/server-matrix.json).

O serviço ativo é `turborama-station-api.service`, PID **2867363**, DLL SHA256 `ef5270b3d47bcc38407cf4821a9b33a13935409f54835874ab9926f31f73ca4a`. Catálogo **46: 4.435 jogos visíveis e 255 aliases**; 27 descritores de motor, 11.370 perfis, 11.349 com aprovação administrativa e 6.509 registros de modos. Há 11.337 perfis aprovados que ainda correspondem ao conteúdo atual; os vínculos históricos de seis cartuchos continuam preservados. Essas contagens não homologam partidas Android.

A auditoria aplicada acrescentou diagnóstico de salas somente em loopback, preservando autenticação, READY/ACK, gerações e recuperação. Seis cartuchos Mega/SNES foram normalizados uma vez na importação; originais e artefatos antigos foram mantidos, sem verificações novas a cada download. Jelly Boy 2 teve título/capa corrigidos e não recebeu modo multiplayer inventado. Catálogo completo, seis downloads integrais, capa corrigida e duas licenças sintéticas criando/entrando/prontas/iniciando foram conferidos por HTTPS público. A qualificação registrou 1.452 verificações de domínio, 29 adicionais de três jogadores, 320 participantes/80 salas e 192 verificações HTTP. O scanner estabilizou; licenças e serviços compartilhados foram preservados. Instalações antigas de Phantasy Star II/IV, Streets of Rage, DKC3 PT-BR, Jelly Boy 2 e Mickey Mania ainda podem conter o artefato anterior; esses seis precisam ser baixados novamente para consumir o cartucho normalizado. Top Gear 3000 não faz parte dessa alteração.

### Resultado do telefone conectado

O **moto g52 foi reconhecido por ADB autorizado neste Linux**. Seu runtime corresponde à R139; o cartucho instalado de Top Gear 3000 coincide com o conteúdo atual do servidor. O novo recibo da produção também confirma a R139 no Edge 30. Em Top Gear VS, o servidor aceitou dois jogadores prontos/início; houve duas conexões HTTP101 breves, mas a barreira nativa terminou sem chegar a Playing. Em outra abertura, o Android registrou **SIGSEGV na RenderThread às 11:37:23 locais**, seguido de mutex destruído em duas threads e morte do processo `station_netplay`. As recusas404 posteriores indicam sala já encerrada.

[Sequência de evidências sanitizada](r139/server-phone-diagnostic.json). A duração registrada no fechamento HTTP101 é o tempo de vida da conexão, não a latência do handshake. Os tombstones de 11/12 de outubro não explicam automaticamente a falha atual. O buffer principal já havia passado do horário do erro; não há backtrace completo atual. A captura USB seguinte de dez minutos não recebeu outra tentativa. **A causa do carregamento inicial e a função que provoca o erro ao sair permanecem abertas; não foi aplicada correção Android por hipótese.** Logs brutos, serial e dados privados ficaram fora do Git.

**Nova tentativa USB às12:34/12:35 locais:** no VS, hooks e owner iniciaram, mas `StationNative: runloop-return result=1` foi registrado às12:34:21.193 e a Activity retornou ao frontend. Não houve novo SIGSEGV nessa tentativa. Na segunda abertura/Campeonato2p, hooks/owner iniciaram e o relay respondeu PONG (amostras119/178/160/165/518ms), mas `nativeStatus=-1`, `localReady=false`, `readySent=-1` e todos os contadores de dados nativos permaneceram zero até encerrar. O `rarch_main` Android da fonte correspondente retorna1 quando `task_push_load_content_from_cli` falha; o log atual não informa qual etapa de core/conteúdo/driver falhou. A produção do APK precisa capturar esse erro de carga na mesma composição, usando registros limitados ao início, sem relaxar READY ou alterar HLE/controladores/FPS por hipótese. O pedido de stack nativa por `debuggerd` foi recusado pelo Android porque exige root; não houve desbloqueio ou contorno. Relógios não foram calibrados para atribuir causalidade pela diferença de dezenas de milissegundos entre os fechamentos. O servidor permaneceu ativo/PID inalterado; isso não homologa partidas.

### Motores independentes: bytes obtidos; publicação pendente

O USB forneceu as **seis bibliotecas e vinte dependências exatas** de PS1/N64/GameCube/Wii que não vieram no kit RetroArch. Os hashes coincidem com o manifesto instalado. Foram preparados quatro cadastros e **78 modos já revisados: 66 N64, nove GameCube e três Wii**, com controles/opções e edições conferidos. [Recibo e estado](r139/native-owner-usb-receipt.json). A janela de autenticação Linux foi cancelada (`Request dismissed`); **esses quatro cadastros ainda não foram aplicados**. O operador preparado usa os dados v3 existentes e dispensa reconstrução/reinício do serviço. PS1 conserva zero aprovações HLE por edição nesta revisão.

O APK R139 ainda contém o limite de catálogo4096: o servidor entrega os4.435 visíveis por `GET /v1/station/catalog?metadata=1&maximumItems=20000`; o overlay NES precisa entrar na composição seguinte do APK. Dreamcast/PS2/Saturn/WiiU/Switch/PSP/32X/NES continuam com catálogo/downloads, mas sem motor online correspondente na composição recebida. **Não declarar todas as plataformas prontas para gameplay nem remover as recusas para simular prontidão.** Usar este mesmo handoff; os blocos abaixo são o histórico de envio da produção.

## Histórico preservado

## Inventário atualizado em 13/10: Edge30 na R139

R139 instalada também no Edge30, SHA integral/UID/dados/primeira instalação preservados; frontend oficial observado: true. Moto g52 e Edge30 estão na mesma R139; A56 última R135. [Recibo Edge30](r139/evidence/instalacao-edge30.json). APK/fonte e contratos permanecem os da entrega abaixo; esta atualização documental não confirma cadastro ou partida.

## R139 assinada com correção de recursos nativos

Entrega APP → SERVIDOR de 13/10/2026. **APK R139 compilado, assinado e instalado no moto g52; kit privado e cadastro R139 ainda não recebidos/confirmados pelo servidor.** Fonte produtiva `42da152e54bbbb4a6d569ca26fe90835503bcccb`, arquivo nativo `24420118e5ce94376b077051084a84d4c52f152d00b9e2330333896bced798fb`, runtime `ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378` / 10.673.192 bytes, APK `19acdeceef86bc66c2475300cbd142da783850c02f5a9e2068d8652ef8523164` / 2.714.991.631 bytes. Certificado original v2/v3 e alinhamento 16 KiB. Hash integral instalado, UID/dados/primeira instalação preservados e frontend observado; Edge 30 última R138, A56 última R135. [Estado e procedimento](r139/README.md) · [STATUS](r139/STATUS.json) · [Recibo de instalação](r139/evidence/instalacao-moto-g52.json).

O pacote altera somente `libstation_retroarch.so` e `engines.json`, preservando 13.858 entradas e todas as 40 partições DEX. Corrige falhas de alocação/pipe/worker e a propriedade JNI de uma única chave por processo; preserva a saída/cleanup R138 e toda lógica de quadros, core/opções, cache, 30 fps, timers, READY/ACK, recuperação e controles. A [qualificação no host](r139/evidence/native-resource-tests.json) passou 24 cenários. O [probe Android no moto g52](r139/evidence/android-jni-resource-probe.json) validou os três helpers JNI com VM/pthread reais; não executou a NativeActivity ou uma partida do produto nem mediu ganho de desempenho.

O [manifesto administrativo candidato](r139/incoming-engine-manifest-candidate.json) renova **oito engines v3** para o runtime novo; o registry do app renova nove descritores com Geolith ainda inativo. Preservar os oito cadastros R138 já aplicados e todos os dados atuais do servidor `08e0031`. Os quatro owners PSX/N64/GameCube/Wii R137 ficam exatos e continuam aguardando seu kit independente de seis bibliotecas/ledger. SNES conserva HLE e os mesmos controladores/modos por edição: Mario Kart dois; Top Gear VS dois/três/quatro, Campeonato em dupla dois. Nenhum registro novo ou homologação de gameplay é inferido dos hashes.

Receber os bytes R139 exatos e preparar a adição pelo operador v3 vigente contra o baseline fresco; publicar somente os dados conferidos pelo fluxo administrativo existente e devolver recibo com vínculos assinados novos. Não substituir perfis R138, aprovar recusas, relaxar identidade/READY/ACK/geração ou reiniciar serviços por ler este repasse. O runtime antigo do kit R138 não representa a R139.

O catálogo NES/revisão44 publicado abaixo continua preservado. **Esta compilação de correção não inclui o overlay de capacidade NES:** mantém o limite/consulta R138 de 4.096 itens e os 411 NES recebidos no modo compatível. O ajuste de catálogo e sua conferência de preservação são uma composição separada; não copiar fontes antigas sobre a R139. Saída física, pressão real de memória e partidas após cadastro ainda precisam de seus recibos próprios.

Kit R139 preparado: **106.499.932 bytes**, SHA256 `1449d2f93d3aa345809529a93bcdfb143381383574c79f507181a1b8c48f8fdd`; [recibo](r139/evidence/private-kit-prepared.json), cinco bibliotecas/oito motores. Recebimento privado e registro v3 ainda dependem de retorno real. A tentativa observada no g52 parou na seleção de perfis/modos, antes do motor; segunda captura confirmou Battletoads in Battlemaniacs (USA), SNES, e ausência de modo aprovado para o motor instalado; capabilities fresco ainda pendente ([observação](r139/evidence/room-create-observation.json)).

## Histórico preservado

## NES e capas novas publicadas — 12/10/2026

**Estado atual: catálogo44 / 4.690 IDs / 4.435 visíveis / 255 aliases.** NES foi movido de `snes/nes` para a raiz `nes`: 750 arquivos, 748 NES + um FDS + um UNIF, bytes preservados. O arquivo de Game Boy recebido no conjunto foi conservado e excluído do catálogo NES. [Recibo consolidado](online-plataformas-20261009/nes-publicado-20261012.json) · [Todos os vínculos NES/capa/Git](online-plataformas-20261009/nes-capas-20261012.json) · [Catálogo integral](online-plataformas-20261009/catalogo-cruzado-completo.json).

### Capas e publicação automática

**469 capas novas de `capas-em-producao/NES/Recriadas`** foram comprimidas e colocadas em `nes/media/revista`, com associação por ROM/título. Corrigida a primeira seleção, que usara a pasta de imagens originais do Git. 280 jogos sem correspondência segura nas artes do repositório conservam a capa frontal recebida; Dancing Blocks tem uma capa personalizada criada no modelo Turborama NES. Todos os750 têm capa JPEG RGB480×720/q90. O conjunto de origens escolhido tem1.216GB e foi reduzido a95.254MB. Gitmain5c6cc32c06e6944b9258df36ffdb846baa368a7f.

A troca foi publicada **pelo importador automático existente**: revisão44, `updated:469`, `added:0`, 4.435visíveis, seguida de varreduras `changed:false`. A pasta de imagens/seed pertence ao operador e permite esse fluxo; autenticação manual adicional foi dispensada. Cache novo nos469 itens: revisão44, coverId preservado. Colocar uma ROM suportada em `nes` e a capa com o mesmo nome-base em `nes/media/revista` permite descoberta automática, sem programar jogo individual. Sinopse vem do XML/seed; cadastro complementar atende dados ausentes.

**Nintendo64 conferido novamente:** 157 jogos/capas, todas as revistas e corpos do servidor coincidem com as imagens compiladas do Git atual;157 respostas autenticadas locais e16 amostras HTTPS confirmadas na revisão43. Mantida revisão42 desses itens e cache por coverId+item.revision. As imagens N64 já são as personalizadas Turborama; não houve nova arte diferente no Git. A correção44 altera somente o NES.

### Leitura completa pelo app

1. Usar **`GET /v1/station/catalog?metadata=1&maximumItems=20000`**, incluindo a query na prova por pedido. Validar envelope/assinatura/domínio/produto/licença/aparelho/sessão. Payload: `totalItems:4435`, `hasMore:false`.
2. Sem `maximumItems`, a resposta é limitada a4.096 para preservar os aparelhos atuais: todos os3.685 anteriores +411NES. **R138 precisa do ajuste Java e JNI para mostrar os750NES.** Overlay/patch e recibo de compilação em `integration/station-nes-catalogo-20261012` no Git do app. Limites de parser/evidência/prefetch e os dois limites nativos de catálogo/preparação foram conciliados; ABI e agendamento atual das capas preservados. Java8/API34, D8/API26, parser4435/750, limite20000 e ELFarm64/API26/16KiB passaram. Novo APK completo/assinatura/instalação são a próxima etapa do PC de produção.
3. `platform:"nes"` já mapeia Nintendinho. Usar os IDs, revisão, pasta e metadados do payload. Capas: `GET /v1/station/covers/{coverId}`, cache `coverId+item.revision`. Downloads: `POST /v1/station/downloads/authorize`, depois GET do grant em `/v1/station/artifacts/{grantId}` uma vez, preservando o descritor `raw`/launchPath. Licença/saves/dados podem ser preservados na atualização.

### Prova e preservação

A revisão43 foi autenticada/assinada localmente e pelo HTTPS:4435campos de catálogo,750capas locais,16amostras públicas e downloads completos dos três formatos, com recusa de replay e de limites inválidos. A correção44 foi confirmada pelos750JPEGs de entrada conferidos e pelo scanner real:469atualizados/zeroadicionados e estabilização; os corpos corrigidos não foram submetidos a uma segunda prova autenticada. Catálogo cruzado documenta essa distinção. Sem alegar teste visual/gameplay Android.

API ativa DLL25c49dd9327f14c0dbbd0d605b0f33e51398682bb20a6e7cb3d1e3ef002ab2c7/PID2637019/NRestarts0. Compilada da fonte efetiva7eb0e009 mais somente os três arquivos de capacidade/consulta; diagnóstico R134 preparado não foi promovido. Um reinício somente do Station na publicação inicial, com zero salas/conexões; troca de capas sem reinício. 3.940IDs antigos,8.267perfis/8.254aprovados, licenças/esquema/configs/outros serviços preservados; túnel/proxy/firewall intactos. Testes:44catálogoC#, seis descobertaPython, quatro identidade, oito parserJava. Backup anterior publicado nos doisGits: backup/station-antes-nes-20261012/tag equivalente, servera338153/app9d9700b.

NES está publicado para catálogo/capas/downloads; motor online próprio continua dependente da integração do APK. Os oito motores R138 aplicados e as pendências dos quatro independentes abaixo permanecem. Manutenção horária continua e prioriza as versões personalizadas/recriadas fornecidas; não reaplicar operadores históricos.

## Histórico preservado

## R138 aplicada e capas publicadas — 12/10/2026

**Aplicação administrativa concluída para o kit R138 recebido.** Oito motores RetroArch novos foram registrados, com 3.091 perfis/modos novos; os 5.176 perfis e 321 modos anteriores foram preservados. Total: 8.267 perfis, 8.254 aprovados e 3.412 modos persistidos. API continua PID2136303/DLL2c0d03447696, sem reinício ou nova DLL. [Matriz única e recibo real](r138/server-return.json) · [Respostas assinadas local/pública](r138/server-signed-bindings.json) · [Catálogo completo atualizado](online-plataformas-20261009/catalogo-cruzado-completo.json).

| Plataforma/owner recebido | Perfis R138 acrescentados | Registro e resposta assinada |
|---|---:|---|
| SNES/BR | 850 | Aplicado; HLE e controles canônicos conferidos |
| Mega Drive/BR | 978 | Aplicado; controles/opções anteriores preservados |
| Neo Geo CD | 50 | Aplicado; firmware de novos conteúdos continua pendência separada |
| Neo Geo/FBNeo | 189 | Aplicado |
| FBNeo | 913 | Aplicado |
| CPS1 | 36 | Aplicado |
| CPS2 | 64 | Aplicado |
| CPS3 | 11 | Aplicado |

São 3.085 novas aprovações e seis recusas preservadas; novo cadastro não transforma recusa em aprovação. SNES usa `requireExplicitEngineModes=true`: copiados somente os vínculos já aprovados de conteúdo/core/controle, sem aprovar jogo desconhecido por `metadata.players`. Novos IDs de perfil são específicos da engine, mantendo os hashes LLE antigos. Geolith permanece inativo; PS3/PS4 offline.

**Super Mario Kart:** dois jogadores, perfil HLE `8a79b81e1797bedf4c00058e004af7bbe7a00014d7b6041ef2b2a0c0bd39c793`. **Top Gear 3000, edição comum e PT-BR:** Corrida VS `[2,3,4]`, perfil multitap HLE `88568b97800afa017e3c75684effe23f3aad8e93f5b2ac947a0638a4268bd8f3`; Campeonato em dupla `[2]`, perfil padrão acima. As cinco combinações constam com itemId/content/core/runtime/profile exatos no recibo. Os dois participantes precisam da mesma R138; três/quatro precisam de todos na mesma composição. READY, ACK, epoch/geração, replay e autorização foram preservados. PONG não confirma carga do core.

**Quatro motores independentes do APK atual continuam sem cadastro correspondente:** PSX `pcsx-rearmed-ff81ed17-player-v3-c26d8836df25`; N64 `mupen64plus-ae-33bf7021-n64-v3-2f248cfcdc9e`; GameCube/Wii `dolphin-5102a033-…-v3-2e0b36222097`. O ZIP privado R138 entregue tem somente as cinco bibliotecas RetroArch; não contém as seis bibliotecas independentes R137. Seus bytes e ledger qualificado são necessários para registrar esses quatro IDs. Cadastro/política antiga permanece preservado, mas não prova disponibilidade desses IDs novos. Não misturar identidade anterior nem inventar hashes ou aprovação HLE PSX.

### Capas e leitura pelo app

**Catálogo publicado: revisão42 / 3.940 IDs / 3.685 visíveis / 255 aliases.** Git `Capas-TurboRama`, main `5c6cc32c06e6944b9258df36ffdb846baa368a7f`, SNES `9f85e9df1ec0c82d77156ed072b997855f719288`. Primeiro lote: 2.255 jogos cruzados, 2.150 imagens distintas, 2.353 vínculos de catálogo substituídos na revisão40. Segundo lote: 295 novas artes Mega e27 PSX, 322 imagens, 412 vínculos substituídos na revisão41. Os aliases entram nesses números de vínculos. A revisão42 atualiza os carimbos de todas as capas SNES/BR, Mega/BR, PSX e N64: 2.057 itens visíveis voltam a ser buscados pelo cache existente.

As **157 capas N64 já coincidiam com os bytes compilados do Git**; foram conferidas e suas revisões atualizadas para o app baixar novamente. Não foram inventadas artes diferentes. SNES/Mega/PSX receberam os lotes novos onde a associação era única. JPEG RGB480×720, qualidade90, nos respectivos `media/revista` e seeds `media/catalogo`; não há PNG grande novo servindo o app. 278 correspondências sem arte única mantêm a correspondente existente; PS2/PSP/Wii/WiiU conservam as artes corrigidas do site. [Mapeamento por jogo/capa/Git](online-plataformas-20261009/capas-git-20261012.json).

1. Reabrir/atualizar a leitura autenticada de `GET /v1/station/catalog?metadata=1` e validar o envelope assinado. Exigir revisão42 ou superior para esta entrega.
2. Usar `itemId`, `platform`, `folderPath`, `metadata`, `coverId` e **a revisão do item** recebidos. Buscar `/v1/station/covers/{coverId}` com sessão/prova e os quatro workers existentes; cache por `coverId + item.revision`. Não reutilizar o cache antigo só porque o coverId ficou igual. Não precisa limpar dados, licença ou saves.
3. Para download, `POST /v1/station/downloads/authorize` e GET do grant uma vez em `/v1/station/artifacts/{grantId}`. Conservar formato e `launchPath` do descritor assinado; não construir URL pelo nome do jogo.
4. Para sala R138, consultar `POST /v1/station/online/multiplayer/command`, ação `capabilities`, com itemId e `clientMaximumPlayers:5`. Usar a lista assinada correspondente ao engine/core/runtime/profile/modo da R138, incluindo contagens permitidas. O catálogo público no Git é comparação documental, não autorização de sala.

A prova pública assinada conferiu todos os campos de catálogo; 2.255+322 corpos de capas foram conferidos pela API local autenticada e52 amostras pela rota HTTPS. A revisão42 repetiu os157 corpos N64 e quatro amostras públicas. Quatro workers em cada lote. Provas dos oito motores: 11 consultas assinadas locais e11 públicas; engine desconhecida, conteúdo errado, perfil desconhecido e Top Gear cinco recusados com409. DNS/TLS intermitentes deste PC foram contornados somente no cliente de verificação, usando endereços observados e mantendo hostname/certificado/prova; DNS/túnel/proxy/firewall do servidor não foram alterados.

As varreduras isolada/efetiva passaram sem reconstruir ROMs, alterar IDs, descritores, sinopses, identidades de conteúdo, licenças, esquema ou outros serviços. A recuperação corrigiu carimbos de arquivos após um rollback e preservou evidência privada. Nenhuma NativeActivity/ELF Android foi executada no servidor. Suíte de domínio:1.452 checks,29 novos3p, capacidade isolada320p/80salas; estes testes não homologam gameplay/WAN/Android. O scanner ainda informa firmware NeoCD ausente/inválido e uma cópia AOF2 não catalogada; não foi criada BIOS nem apagado o jogo publicado correspondente.

### Retorno à produção do APK e manutenção

APK esperado SHA `9183462e4d45cfcf2d2599d9e682b81890fee7b094505bf3073ab8183dd1dd10`; runtime `463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f`. Recibos recebidos comprovam instalação no g52 e Edge30; A56 última R135. Aqui não houve montagem/instalação de APK ou USB. Testar Mario Kart2p, Top Gear2/3/4p, retorno de sinal, saída de host/convidado sem crash e cinco em modo que permita cinco. Registro/carga de120 quadros não é aceite físico.

Backup anterior publicado nos dois Gits: `backup/station-antes-r138-20261012` e tag `backup-station-antes-r138-20261012`, servidor a6b58e9/app a773988. Manutenção horária `turbostation-retornos.timer` mantida; acompanha branches de retorno docs/build/fix, novos kits em `/mnt/DADOS` e capas, escolhendo o commit mais recente mesmo quando o nome da branch é reutilizado. Usa lock para serializar trabalho e autenticação Linux nativa para arquivos protegidos. Não reaplicar workflows datados: consultar os recibos aplicados e o estado atual. Esta é a consolidação no mesmo handoff, com histórico abaixo preservado.

## Histórico preservado

## Retorno R129, capas novas e manutenção horária — 11/10/2026

A checagem das 14h recebeu **c433052**, variante R129 do mesmo handoff R128. Fonte Android `cf623c7`; APK Final-Modal `89dd5439`, 2.237.505.749 bytes. O produtor apresentou recibo de instalação integral no Motorola; autenticação e gameplay continuam sem confirmação. Os scripts administrativos e o contrato permanecem iguais; os quatro IDs foram renovados para este pacote. O kit R128 não corresponde aos runtimes R129.

Os dois operadores foram incorporados ao `library-tools` da entrega. **68 testes executados no Linux com os recibos R129 passaram**, os 18 selos públicos conferem e os quatro perfis canônicos foram regenerados. Os recibos históricos entregues foram preservados byte a byte. **A aplicação nos arquivos protegidos aguarda a autenticação nativa do Linux; os quatro motores novos ainda não foram ativados.** Faltam as seis bibliotecas privadas R129 e os modos por edição exigidos pelo handoff. PSX exige evidência HLE. Não fabricar bytes nem promover perfis genéricos anteriores. PS3/PS4 permanecem offline.

O Git correto é `Capas-TurboRama`, main `9f598e2` e pacote SNES `9f85e9d`. Foram cruzados **2.255 vínculos / 2.150 imagens distintas**, incluindo SNES/BR, Mega/BR, PSX, N64, Neo Geo, Dreamcast, GameCube, Saturn e Switch. As artes nomeadas recentes têm prioridade: **671 vínculos SNES, 331 Mega e 28 PSX**. Arquivos UUID e arquivos de gerações sem associação segura foram excluídos. Os 278 casos sem correspondência única mantêm a capa correta existente; PS2/PSP/Wii/WiiU conservam as correções do site. Os originais antigos terminaram de baixar; novos lotes estão chegando. A preparação compila JPEG RGB 480×720, qualidade 90, sem reconstruir ROMs. **Catálogo publicado: revisão 33 até existir recibo real da aplicação.**

A manutenção `turbostation-retornos.timer` está ativa, de hora em hora, com Linger habilitado. As execuções das 13h e 14h terminaram com status 0 e detectaram novos retornos/capas. Ela consulta os três Gits, evita publicações concorrentes, implementa novidades recebidas e registra pendências reais. Operações protegidas continuam exigindo autenticação Linux; bytes privados não disponíveis não são inventados. O próximo ciclo é às 15h. Não há reinício da API nesta preparação.

A janela nativa já está aguardando a senha neste servidor. O workflow preparado toma snapshot fresco, publica os operadores auditados, qualifica a troca de capas sem cópia de jogos, confere catálogo/capas por HTTPS assinado e registra a matriz v3. A conclusão produz `applied.json`, `r128-administration.json`, `r128-signed-matrix.json` e um marcador para o monitor atualizar este mesmo retorno. Estado privado: `/mnt/DADOS/station-r128-20261011`; kit de recebimento: `kit-privado` nessa pasta. O APK completo R129 também permite extrair as seis bibliotecas pela receita selada, sem chaves ou BIOS no Git.

Recibo consolidado: `online-plataformas-20261009/r128-retorno-servidor-20261011.json`. Backup estável publicado antes das mudanças: branch `backup/station-antes-r128-capas-20261011` e tag correspondente nos dois repositórios. Código preparado, dados aplicados e gameplay devem ser relatados conforme seus recibos reais. Não executar operadores históricos; atualizar este mesmo retorno quando a autenticação/kit chegarem.

# Station: catálogo, capas, downloads e servidor online — 10/10/2026

## Estado atual confirmado na produção

**Servidor concluído para as 17 plataformas solicitadas**, incluindo PS2 e Sega Saturn. A produção do APK pode implementar os motores usando esta mesma entrega. Servidor pronto significa transporte, autenticação, perfis e cadastro; os motores reais e o gameplay nos aparelhos continuam com as dependências indicadas abaixo.

- API ativa: PID **2136303**, NRestarts **0**; release `/opt/turborama-station-endpoints-20261010-2c0d03447696`.
- DLL SHA-256 `2c0d034476969cbed58b519b744e841111373f89a4bcd6abb9cd14543abcde35`; fonte do servidor e importador `7eb0e009d40ce01db86aea9c489937e9bd2fd00a`; scanner SHA-256 `7f6e9ebb6e1ccc71c184e7d3e07cac856f1c0ceee4e281e272061406619e36bd`.
- Catálogo **revisão 33 / 3.940 IDs / 3.685 visíveis / 255 aliases**, SHA `47087b1f35ae8cca418a108a606195adeb5bdb222bed351cfb206751dc56e986`. São 21 rótulos / 19 plataformas normalizadas no catálogo; 17 políticas online permanecem vigentes.
- **3.940 identidades**, **5.176 perfis / 5.169 aprovados**, preservando os perfis e os 3.895 vínculos de conteúdo anteriores. **320 jogos restantes / 321 modos preparados**.
- Revisão de endpoints publicada em `2026-10-10T21:49:22.368044+00:00`: somente Station reiniciado após confirmar zero salas e conexões. HTTPS autenticado confirmou os 3.640 itens e as 17 políticas, 19 capas com quatro workers e 19 amostras de download. Timer/importador ativo; varredura real sem mudanças. Licenças sintéticas removidas; configurações anteriores, clientes, licenças reais, esquema e outros serviços preservados. Túnel/proxy/firewall inalterados.

[Recibo anterior da revisão e ativação](online-plataformas-20261009/endpoint-review-20261010.json). [Organização Saturn, capas, descritores e cabeçalhos](online-plataformas-20261009/ps2-saturn-servidor-20261010.json). Este é o mesmo handoff; os recibos anteriores preservam seus números e horários históricos.

## Capas conferidas com o site hospedado — 10/10/2026

Correção aplicada em `2026-10-10T23:38:41.105858+00:00`. A integração anterior reutilizou **uma versão antiga das artes locais Turborama**. Os nomes/IDs eram do jogo correspondente, mas a arte publicada no site havia mudado. Comparação visual de Black e God of War II confirmou a diferença de modelo. O conjunto atual do site é a referência desta correção; o site não foi alterado.

Foram substituídas **36 capas: PS2 25/25, PSP 4/9, Wii 6/7 e Wii U 1/1**, com conferência de título e plataforma. PS2 também inclui a categoria BR para Resident Evil 4 dublado e a capa Bomba-Patch-2026 agora existente no site. Nenhuma arte nova foi gerada. Todas seguem JPEG RGB 480×720, qualidade90 e enquadramento completo, usando o compilador já instalado. O site oferece WebP360×540; o tamanho final480×720 mantém o padrão do Station, sem alegar resolução original superior.

Os **seis jogos sem capa nesse catálogo hospedado** mantêm a imagem correspondente já existente: Wii Epic Mickey2; PSP Crash: Mind Over Mutant, GTA: Vice City Stories, Moto GP, Soulcalibur: Broken Destiny e Toy Story3. Não usar GTA Chinatown Wars como capa de Vice City Stories nem versões de outras plataformas. Para ter exatamente a arte personalizada do site nesses seis, ela precisa primeiro existir no catálogo da plataforma correta.

Também foram preparadas **160 artes do site em `media/catalogo`**, incluindo todas as páginas PS2/BR e os catálogos PSP/Wii/WiiU. Os seeds existentes mantêm suas sinopses e aliases; God of War (USA) comum/BR recebe chaves distintas. As capas dos jogos atuais estão em `media/revista`. Em Wii U, a capa genérica `Turbo.jpg` foi substituída por `MARIO KART 8 [AMKE01].jpg` e a referência XML foi atualizada; o scanner seleciona pelo seed da pasta do jogo, evitando reutilizar a capa de Mario Kart8 em outro pacote com `code/Turbo.rpx`. Novos jogos usam os nomes/chaves do seed da plataforma, ou uma capa correspondente em revista.

**Leitura pelo app:** obter novamente `GET /v1/station/catalog?metadata=1` pela sessão e prova existentes. O catálogo agora é **33**; os36 itens alterados têm `item.revision=33`, mantendo `itemId` e `coverId`. Buscar `/v1/station/covers/{coverId}` com os quatro workers atuais e gravar o cache por **`coverId + item.revision`**. Não reaproveitar a capa da revisão anterior; não montar URLs de imagem pelo nome do jogo nem copiar artes embutidas na compilação. Client/RoomsDEX continuam os mesmos; o cache por revisão já está implementado. Atualizar a consulta do catálogo permite receber as capas novas, sem limpar dados ou saves.

**Conferência real:** envelope assinado, todos os3.685 itens públicos e os42 corpos de capa conferidos por bytes/identidade; quatro requisições de capa simultâneas. O parser do cliente já compilado leu o catálogo33. Scanner isolado e scanner efetivo retornaram `changed=false`, `added=0`, `updated=0`; zero tentativa de reconstruir esses jogos. Os3.940 IDs/identidades, descritores, metadados,5.176 perfis/5.169 aprovados,17 políticas online e arquivos de jogos permaneceram intactos. API mantém PID2136303/DLL2c0d03447696, sem restart; sandbox conferido e licença sintética removida. Nenhum teste visual nos celulares ou novo APK foi feito por esta alteração de dados.

Fontes: [PS2](https://turbobox.lzgames.com.br/catalogo.php?plataforma=playstation-2), [PS2 BR](https://turbobox.lzgames.com.br/catalogo.php?plataforma=playstation-2-br), [PSP](https://turbobox.lzgames.com.br/catalogo.php?plataforma=psp), [Wii](https://turbobox.lzgames.com.br/catalogo.php?plataforma=nintendo-wii), [Wii U](https://turbobox.lzgames.com.br/catalogo.php?plataforma=nintendo-wii-u). [Cruzamento individual, origem e provas](online-plataformas-20261009/capas-site-ps2-psp-wii-wiiu-20261010.json). Cópia estável anterior publicada nos doisGits: branch/tag `backup/station-antes-capas-site-20261010` / `backup-station-antes-capas-site-20261010`, server5f9c019/app2b4a6dd. Mesmo handoff; recibos anteriores continuam históricos.

## Sega 32X e PSP — catálogo, capas e downloads publicados

Aplicado em `2026-10-10T22:40:20.495314+00:00`, sem reiniciar a API: **36 jogos de Sega 32X e nove de PSP**. Pastas inteiras movidas de `snes/sega32x` e `snes/psp` para `sega32x` e `psp` na raiz do HD. Os 611 arquivos originais recebidos, XML, imagens, vídeos e demais dados permaneceram no conjunto.

**Capas:** 45 JPEG 480×720 em `media/revista`; nenhuma arte nova gerada. Sega 32X reutiliza suas 36 imagens recebidas. PSP reutiliza quatro artes correspondentes do catálogo Turborama e cinco imagens recebidas. As 21 artes PSP do catálogo e as demais artes XML foram preparadas em `media/catalogo`; os seeds registram 464 chaves PSP e 43 de 32X, excluindo associações ambíguas. Os 235 registros XML de PSP não são 235 ROMs: somente os nove arquivos presentes foram publicados.

**Downloads:** PSP oferece CSO bruto, com nome/extensão e descritor assinados. Armazenamento `readonly-hardlink` mantém o mesmo inode da ROM, root/0444, sem duplicar os 6,53 GB de jogos; a verificação inicial é offline, sem nova passagem de integridade em cada download do app. Sega 32X oferece ZIP com `launchPath` exato para `.32x`. Índice, identidades e configuração foram qualificados antes da publicação; todos os 3.895 itens anteriores e os registros online permaneceram iguais. Segunda varredura isolada e scanner real: `changed=false`, `added=0`, `updated=0`.

**Como o app lê:** abrir sessão/prova de aparelho pelo contrato existente e buscar `GET /v1/station/catalog?metadata=1`, verificando envelope e domínio. Usar `itemId`, plataforma explícita, revisão, `coverId`, metadados e `folderPath` da resposta. Buscar capas por `/v1/station/covers/{coverId}`, mantendo os quatro workers atuais e cache `coverId + revisão`. Autorizar cada download por `POST /v1/station/downloads/authorize`; usar o `grantId` uma vez em `/v1/station/artifacts/{grantId}` e executar o `launchPath` do descritor assinado. Nunca construir caminhos pela grafia do nome ou converter CSO para ISO. A recarga do índice publicado pode levar até dez segundos; atualizar a leitura do catálogo nesse intervalo.

O parser do **cliente já compilado** leu os 3.685 itens reais e validou as 45 associações de plataformas/descritores/sinopses; mapping `psp`/`sega32x` já existe, sem alteração de Java/DEX por estes dados. API pública assinada confirmou todos os campos, as 45 capas com quatro workers e 45 amostras de download de até 64 KiB, com grants de uso único. O sandbox efetivo leu 7759 arquivos e negou os quatro caminhos protegidos. São provas de catálogo/transporte; teste físico do APK não foi executado aqui.

Novos jogos dessas pastas entram pelo importador/timer existente, após estabilidade dos arquivos. Sinopses vêm de XML, seed ou override privado; capas correspondem por stem exato/seed sem associação ambígua. `metadata.players` é informativo: PSP pode indicar ad hoc e 32X pode indicar turnos. Esses números não aprovam vagas online. Esta inclusão entrega catálogo/capas/downloads; suas políticas/motores online exigem integração nativa própria. As 17 políticas anteriormente implementadas permanecem intactas.

[Recibo, cruzamento das artes, descritores e provas](online-plataformas-20261009/sega32x-psp-publicados-20261010.json). Cópia estável anterior já publicada nos dois Gits: branch/tag `backup/station-antes-sega32x-psp-20261010` / `backup-station-antes-sega32x-psp-20261010`, servidor `6dbc9e2`, app `0d51ce1`.

## Limpeza do HD DADOS — concluída

Removidos **117 caminhos de compilações antigas, cópias de qualificação e arquivos intermediários**, recuperando **138.97 GiB** reais. Espaço disponível: **62.43 → 201.40 GiB**. Última entrega Station do servidor/app e as 22 saídas do último conjunto completo Turborama foram preservadas e conferidas por hash. Fontes, SDKs usados pela entrega Station, histórico Git, worktrees com alterações, arquivos privados, ROMs/capas em uso, licenças, clientes, bancos e backups das artes originais foram mantidos. Pastas históricas removidas continuam recuperáveis pelo Git; os arquivos de compilação podem ser reconstruídos.

Pós-limpeza: mesmo PID/DLL, catálogo 32 assinado e 3.685 visíveis, todas as mídias publicadas legíveis, registros online e configurações preservados, timer ativo. Nenhum serviço foi reiniciado. Caminhos intermediários datados removidos não devem ser usados por operadores históricos; usar a entrega atual e suas fontes/receitas. [Inventário da remoção e conferência final](online-plataformas-20261009/limpeza-compilacoes-20261010.json).

## Revisão de endpoints, ações e recebimentos — aplicada em 10/10/2026

A revisão inventariou **14 rotas da API Station, três rotas de prontidão local, 15 rotas administrativas Station, 21 ações v1/social e 12 ações v3**. O recibo lista os caminhos, o código verificado, testes e escopo. As rotas do painel foram examinadas e o self-test administrativo passou; ações comerciais não foram repetidas sobre clientes reais.

| Falha encontrada | Correção aplicada |
|---|---|
| Publicação do índice entre carga inicial e criação do monitor podia deixar o catálogo antigo em uso; recargas paralelas podiam competir. | A versão carregada guarda seu próprio carimbo, valida antes/depois da leitura e serializa recargas; snapshot válido e grants anteriores continuam preservados. |
| Quebras de linha/tabulação em campos de uma linha podiam provocar recusa do parser Android. | Normalização em leitura/importação; a sinopse mantém as quebras permitidas. O índice atual não precisou de alteração. |
| Cancelamento da autenticação do relay antes do upgrade podia terminar como HTTP200 vazio. | Timeout recebe JSON504 com código v1/v3 correspondente; anexos são limpos e cancelamento do cliente não inicia outra resposta. |
| Falhas de autenticação/tempo/arquivo podiam devolver erro vazio ou escapar do tratamento esperado. | Erros de online, prova de pedido e capas seguem status/código JSON finito e Retry-After quando aplicável. |
| O app exibia somente as primeiras 32 salas v3; o cursor compartilhado de pessoas/salas impedia percorrer as demais corretamente. | Paginação opcional no servidor; cursores separados na composição do app, própria sala sempre incluída e presença v3 calculada independentemente da página. |
| Uma falha transitória na consulta podia encerrar o acompanhamento da sala. | O app compilado repete somente consultas/heartbeat após recuo e respeita Retry-After; falhas de identidade/assinatura/acesso continuam terminais. |

**Leitura das salas pelo APK:** enviar `page` de 0 a 40 e `roomPageSize:32` em v3. Sem os campos, o servidor mantém página 0/lote 100 para clientes anteriores. Usar `nextPage` da resposta v3 como próximo cursor de salas; ele é `null` no fim. `totalRooms` conta as salas visíveis antes do recorte. `room` própria independe da página. Em social, manter `nextPeerPage` para Pessoas e `nextRoomPage` para salas antigas; `nextPage` anterior continua disponível. Não sobrescrever esses cursores ao combinar as respostas assinadas. Navegar entre Pessoas/Salas reinicia em página 0, e uma página ainda não recebida mostra consulta em andamento.

**Recuperação das consultas:** HTTP 408/429/500/502/503/504 ou Offline/SocketTimeout/SocketException usam 1, 2, 4 e no máximo 8 segundos de recuo, ampliados por Retry-After até 60 s. POLL_EXISTS usa 1 s. Apenas um poll fica pendente por aparelho. Cancelamento explícito, acesso 401/403, serviço desativado e falhas TLS/JSON/assinatura/identidade encerram a tentativa. Criar, entrar, marcar Pronto, iniciar e outras ações não têm repetição automática. A espera de 10 s e o limite de 15 s já passaram antes da revisão; não foi necessário aumentar o timeout.

**Validação da candidata e da publicação:** dez projetos de testes do servidor passaram, incluindo 1.423 checks v3, 179 checks TLS v3, 62 HTTP v1, 318 convites/códigos, 91 recuperação + 589 observações, 88 falhas de fechamento e regressão da Suite/provas/transferência. TLS v2 separado passou 49 checks. Os 212 fontes Java/D8 foram recompilados; 36 checks do lobby e 200 de perfis passaram sobre a compilação correspondente. HTTPS real conferiu os itens pelo itemId e todos os campos/metadados publicados; 19 capas e 19 downloads tiveram bytes comparados, cabeçalhos e uso único dos grants. Downloads grandes tiveram amostra de 256 KiB por rótulo; isso não verifica o corpo integral.

O sandbox efetivo do novo PID leu **7.670 arquivos de mídia/índice/registros/binário**, negou quatro caminhos protegidos e manteve montagens sem escrita. Timer ativo, scanner status 0 e revisão 31/changed=false/added=0/updated=0. No intervalo da ativação até a conferência às 21:52 UTC, os 1.300 registros Station coletados foram informativos, sem códigos de erro ou exceções; esse intervalo não comprova estabilidade indefinida ou gameplay Android. Túnel compartilhado e demais produtos permaneceram intactos.

Servidor já atualizado **antes** do novo APK, pois o parser anterior não aceitava os campos novos. A produção APK deve consumir esta mesma candidata/receitas, montar com a base privada e certificado original e atualizar os aparelhos preservando dados. Rooms DEX atual: `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`; Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`. APK completo, instalação, aparência e gameplay físico ainda não foram comprovados nesta revisão; as dependências nativas das plataformas continuam listadas abaixo.

Cópia anterior publicada antes das mudanças: branch `backup/station-antes-revisao-endpoints-20261010` e tag `backup-station-antes-revisao-endpoints-20261010` nos dois repositórios; servidor 919d31a/app 941b761. A versão de retorno foi qualificada em sombra antes da ativação. Recibos locais em `/mnt/DADOS/station-endpoint-review-20261010/`; não repetir operadores datados. O operador atual já tem marcador applied.json. Salas/conexões são dinâmicas; uma próxima implantação deve consultar o estado atual.

## Plataformas e limites do servidor

| Plataforma | Máximo por sala | Dependência do app |
|---|---:|---|
| SNES / BR | 5 | Modos cadastrados, multitap para cinco e runtime correspondente. |
| Mega Drive / BR | 2 | Motores/controles anteriores preservados. |
| N64 | 4 | Perfis de duas/quatro portas e modo exato; não transformar campanha solo em multiplayer. |
| Dreamcast | 4 | Adaptador nativo Station; o GGPO de duas entradas não comprova quatro. |
| GameCube / Wii / Wii U | 4 | Adaptador nativo; GBA Link, controles compartilhados e Co-Star usam layouts próprios. |
| PS2 / Sega Saturn | 2 | Motores reais, sincronização, duas entradas e BIOS quando exigida. |
| Switch | 2 | Motor/adaptador real e modo local da edição. |
| Neo Geo / Neo Geo CD / PSX / FBNeo / CPS1 / CPS2 / CPS3 | 2 | Driver, BIOS e perfil exatos; exceções conhecidas preservadas. |

O limite da plataforma não substitui `allowedPlayerCounts` do perfil. Não abrir sala com mais entradas do que o modo oferece. `Ver detalhes` não transmite gameplay nem cria espectador. Sete vínculos continuam sem aprovação: seis conjuntos arcade sem associação exata e Worms Armageddon por turnos. O catálogo ainda registra aof2 incompleto, 65 referências XML sem ROM, 148 sinopses ausentes e o requisito de BIOS NeoCD; não foram fabricados arquivos/compatibilidade.

## PS2 resolvido no servidor

Os 25 ISO/CSO estão na raiz `ps2`, com revista, artes de cadastro, XML, seed, downloads raw e **política online de duas pessoas**. `ps2br`/PlayStation 2 BR normalizam para `ps2`. Layout: `ps2-two-controllers-v1`. Seus 25 modos foram preparados: 13 entradas individuais e 12 de duas vagas por política de uso; estas não são homologação de modo original ou Android. O motor real precisa respeitar cada edição/modo. Nenhum jogo foi associado ao motor PSX.

Os cores [Play!](https://docs.libretro.com/library/play/) e [LRPS2](https://docs.libretro.com/library/lrps2/) documentam ausência de netplay. Portanto o APK precisa fornecer um motor/adaptador capaz de estado sincronizado e controles remotos; somente cadastrar o nome PS2 não cria essa capacidade. A API responde `serverReady:true` e `online-engine-pending` até existir o vínculo real.

## Sega Saturn organizado e publicado

`snes/saturn` foi recortada para **`saturn` na raiz do HD**, ao lado de SNES/PS2/Mega. Pasta completa: `/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc/saturn`.

- **19 jogos**: 17 CHD servidos raw pelo mesmo inode, sem segunda cópia, e Bug!/Rayman originalmente em 7z, preparados como ZIP completo. Bug! inclui CUE + 73 faixas; Rayman, CUE + 51 faixas. Originais preservados. Ambos possuem identidade `cue-set-v1`; o hash do CUE sozinho não representa o disco.
- **19 capas de revista**, 16 reutilizadas do catálogo Sega Saturn e três criadas com a ferramenta imagegen: Bomberman, Bug! e Rayman. Todas JPEG RGB 480×720; **34 artes de catálogo / 37 chaves** no seed. Prompts e caminhos finais no recibo. As imagens geradas originais ficam no backup privado; JPEG final em `saturn/media/revista`.
- Os 17 cabeçalhos CHD confirmaram Sega Saturn e os títulos correspondentes. `Bomberman.chd` identifica SATURN BOMBERMAN/MK-81070; Castlevania identifica DRACULA-X. Essa leitura de 4 KiB não é teste completo de disco ou emulação.
- Policy online pronta, teto dois, aliases Sega Saturn/segasaturn, layout `saturn-two-controllers-v1`. Há 19 modos preparados, respeitando as entradas individuais cadastradas; todos aguardam o motor real do APK.
- O core [Beetle Saturn](https://docs.libretro.com/library/beetle_saturn/) documenta netplay e formatos CUE/CHD, mas o APK ainda precisa integrar o adaptador Station, controles e BIOS legítima. A pasta `saturn/bios` está preparada; nenhuma BIOS proprietária foi baixada ou fornecida nesta entrega.

## SNES: mesmo cadastro automático

SNES mantém jogos, grupos, IDs e perfis online anteriores, inclusive até cinco nos modos cadastrados. `snes/station-catalog-seed.json` agora oferece **1.366 chaves não ambíguas**, reutilizando as **1.619 capas já comprimidas de revista**. Doze chaves conflitantes foram excluídas da escolha automática. XML/overrides têm prioridade; o seed preenche dados ausentes. Dois registros/aliases de Battletoads receberam campos de metadata que estavam vazios; descrição, ROM, capa e vínculo de partida foram preservados.

Para novo jogo, colocar a ROM na pasta da plataforma. O timer exige duas observações estáveis e idade mínima de 20 segundos, então publica nome, ID, capa, descritor e identidade. Capas conhecidas usam a associação por título; arte própria fica em `media/revista` com o nome exato da ROM. Dados complementares vêm de XML/seed/overrides. Título desconhecido usa a capa padrão existente até receber arte própria; uma sinopse não é inventada. Não é necessário programar cada jogo.

PS2 aceita ISO/CSO; Saturn, CHD/CUE/ISO e arquivos ZIP/7z/RAR com um CUE completo ou `launchPath` explícito. ISO é formato de publicação; o motor que vier precisa declarar o que realmente abre. Não inventar CUE/faixas. CHD/ISO raw de Saturn usa armazenamento protegido no mesmo volume, root/0444 e um inode. Substituir ROM significa copiar um arquivo novo e substituir o nome, preservando os artefatos de downloads anteriores. As outras plataformas mantêm suas configurações.

## Como o aplicativo deve ler o servidor

1. Usar `https://app.lzgames.com.br`, licença original, bearer e provas vinculadas ao aparelho. Ler e validar `GET /v1/station/catalog?metadata=1`. Usar a revisão recebida, `itemId`, `platform`, `name`, `metadata`, `coverId`, `folderPath`, `contentSha256` e eventual `contentIdentityScheme`; caminhos do HD não são URLs.
2. Buscar `/v1/station/covers/{coverId}` pela mesma linha do catálogo, quatro workers e cache `coverId + revisão do item`. As capas servidas são JPEG de 480×720. Não tentar casar capas pelo índice de outra lista.
3. Autorizar `/v1/station/downloads/authorize` com o itemId e validar seu descritor assinado `artifact`. Consumir `/v1/station/artifacts/{grantId}`. Raw preserva arquivo/extension/launchPath; ZIP extrai o pacote completo para um diretório exclusivo. Seguir tamanhos `long`, formato, fileCount e caminho de lançamento. Nenhuma espera, limite de velocidade ou verificação extra foi adicionada ao downloader.
4. Enviar `enter` autenticado em `/v1/station/online/command` antes de v3. Manter somente um poll de `online/events` por aparelho e respeitar `Retry-After`. Declarar `clientMaximumPlayers` conforme o runtime real.
5. Em `/v1/station/online/multiplayer/command`, usar `capabilities`/`snapshot`. `serverPlatforms` lista as 17 políticas. `platformPolicy.serverReady:true` é preparação do servidor; `onlineAvailable` requer perfil aprovado de conteúdo/motor/runtime/controles. `online-engine-pending` indica o motor real ainda ausente. Tratar também `single-player`, `mode-pending`, `content-identity-pending` e `select-game`.
6. Selecionar perfil exato e quantidade em `allowedPlayerCounts`; criar/entrar/pronto/iniciar. Convites e tickets são automáticos. Conectar WSS `/v1/station/online/multiplayer/relay`, protocolo `station-stream.v3`, um link duplex anfitrião↔cada convidado.
7. Respeitar TSR3, HELLO, PAUSED, READY, ACK e época global. ACK confirma escrita no motor. Preservar sessão nativa na troca do WSS. Identidade CUE/CHD é calculada na preparação/importação, sem varredura do corpo em cada download do servidor; o app compara a identidade para conectar versões iguais na partida.

## Sinopses na sala — ajuste para a próxima compilação do APK

Títulos, botões e estados são escritos pelo app. `modeTitle` e `instructions` vêm dos perfis assinados do servidor. A **sinopse vem de `metadata.description` do catálogo assinado**, já publicado em `GET /v1/station/catalog?metadata=1`. O app deve procurar pelo `itemId` exato da sala, inclusive para o convidado; não associar pelo nome ou posição da lista.

O fonte `StationRoomsActivity` desta mesma candidata foi ajustado: **Criar sala e Sua sala mostram a sinopse em quatro linhas, com toque para ler o texto inteiro**. Outras salas mostram duas linhas e a íntegra em Ver detalhes. Os limites permanecem compactos perto dos controles; instruções dos modos, controles e posições ficam acessíveis em **Como jogar**. Sem sinopse cadastrada, mostrar “Sinopse ainda não cadastrada.”. O catálogo revisionado é consultado em memória; nenhuma requisição nova por sala.

O catálogo31 contém **3.492 sinopses entre 3.640 jogos visíveis**; os demais precisam de texto cadastrado. Java212/D8 recompilados e selados. **O APK completo assinado e a aparência nos celulares ainda precisam da compilação/instalação no PCAPK**; não há APK instalado por esta alteração. A apresentação das sinopses não exigiu alteração da API ou das regras de admissão; a revisão de endpoints acima foi aplicada posteriormente. Comparar `roomPresentation` no contrato e `evidence/java-build.json` no app.

Cópia anterior publicada nos dois Gits antes deste ajuste: branch `backup/station-antes-sinopse-sala-20261010`, tag `backup-station-antes-sinopse-sala-20261010`; servidor `f478d13`, app `45586f6`. Preservar este mesmo handoff.

## Cadastro do motor real sem nova DLL

O manifesto recebido do APK deve declarar engineId imutável, plataforma, formatos reais, `library`, `runtimeLibrary`, seus hashes SHA-256, `launchReady:true`, `recoveryProtocol:station-stream.v3`, `maximumPlayers` e `controllerProfiles`. Para PS2/Saturn, declarar os layouts acima explicitamente. Cada layout contém `configuration` com `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. O motor/decoder do app deve implementar essa configuração; nomes de controles por si só não representam sua implementação.

`profileSha256` é SHA-256 do JSON compacto UTF-8, sem BOM/quebra final, Unicode sem escapes e campos na ordem `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. Exemplos do contrato não são recibos nativos. Não copiar hashes ilustrativos para a produção.

Operador instalado: `/opt/turborama-station-endpoints-20261010-2c0d03447696/library-tools/cadastrar-motor-online-station.py install`, com `--config` da configuração privada efetiva, `--incoming` do manifesto real, `--artifacts` dos binários e `--output` de uma pasta privada nova. Exige administração local, confere ambos os binários e preserva perfis anteriores. `prepare` permite revisão offline; `apply` publica a candidata ainda correspondente ao catálogo. **Recarga em até dez segundos, sem recompilar ou reiniciar o servidor.** Credenciais do app não cadastram motores pela API.

## Testes, capacidade e entrega ao APK

Na publicação histórica PS2/Saturn passaram **1.292 verificações C#**, **172 TLS loopback**, **16 testes Python** de cadastro/importação/permissões e **855 verificações v3 assinadas na sombra**: início, dados nos dois sentidos, retomada e perfis SNES de duas/quatro/cinco pessoas. As 17 políticas e o catálogo/mídia também foram conferidos pelo HTTPS público após ativação. Testes sintéticos não homologam latência, controles ou gameplay Android.

Capacidade configurada: v3 até 100 salas, cinco pessoas conforme modo, janela 256 KiB por direção e replay compartilhado 128 MiB; v1/v2 preservados. Teste sintético de 320 participantes/80 salas de quatro/7.864.320 bytes passou. O orçamento limita admissões; não significa 100 salas de cinco simultâneas ou medição da internet.

O pacote `versions/station-all-platforms-online-20261009` foi recompilado: **212 Java/D8**, alias PS2 BR corrigido e sinopses da sala integradas. Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`; Rooms DEX agora `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`. Runtime `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc` e motores reais anteriores preservados. Nenhum APK completo foi assinado/instalado nesta sessão; base/keystore continuam no PC do APK. Próxima etapa reúne Dreamcast, GameCube, Wii, Wii U, Switch, PS2 e Saturn, assinatura/instalação e testes físicos.

## Arquivos únicos para comparar e retomar

- `catalogo-cruzado-completo.json`: todos os 3.895 IDs, nomes, plataformas, capas, metadata, artefatos, identidades, perfis e modos preparados; sem caminhos privados de mídia.
- `platform-summary.json`, `profiles-missing.json`: contagens por plataforma e dependências reais.
- `content-identities.json`: 3.895 vínculos/schema2; `profiles-complete.json`: todos os 5.176 perfis. Não substituir por `profiles-new-profiles.json`, que contém só adições históricas.
- `server-integration-contract.json`, `server-prepared-modes.json`: contrato v3 completo, 320 jogos/321 modos, layouts, hashes e receita de cadastro.
- `engines-app.json`: motores reais atuais e políticas do cliente; nenhum engine PS2/Saturn fictício.
- `library-tools/`: scanner, todas as dependências, operador de cadastro e testes correspondentes.
- `endpoint-review-20261010.json`: revisão atual, inventário de rotas/ações, regressões, compilação, ativação e verificação efetiva. `ps2-saturn-servidor-20261010.json`: organização e publicação histórica PS2/Saturn. `ps2-switch-publicados-20261010.json`, `capas-padronizadas-20261010.json`, `checkup-completo-20261010.json`, `server-ready-deployment.json`, `production-applied.json` e `profile-reload-fix.json` preservam as provas históricas. ZIP de 09/10 continua histórico e não representa o catálogo31.

Cópia estável anterior publicada antes das mudanças nos dois Gits: branch `backup/station-antes-online-ps2-saturn-20261010`, tag `backup-station-antes-online-ps2-saturn-20261010`, servidor `26c5302`, app `52ba50d`. Backup privado atual: `/mnt/DADOS/station-ps2-saturn-20261010/backup`. Não repetir `apply.py` nem operadores datados. Consultar o estado dinâmico das salas antes de agir sobre serviços. Os dois arquivos CRLF das entregas R71/R78 anteriores ficaram fora desta alteração.
