# R133 — imagens de consoles e modos online revisados

**Compilada, assinada, conferida e instalada no Motorola; ativação no servidor ainda não confirmada.** Fonte executável `5966a8ad23ae9040c570d359b999c8a31de904b4`; APK SHA `945f7ded35e551d638135a05140c32218eb38277d10eaf21e3f83087d71da112`, 2.714.971.151 bytes. [STATUS](STATUS.json) separa os estados e [evidence/PUBLIC-EVIDENCE.json](evidence/PUBLIC-EVIDENCE.json) indexa os recibos completos com hashes. O [handoff único](HANDOFF-APP-R133-PARA-SERVIDOR-20261011.md) reúne as identidades finais, o kit privado, o ledger reconciliado e o procedimento exato que falta ao operador.

## Alteração do app

PSP, PlayStation 3 e PlayStation 4 recebem fotografias oficiais de hardware ao lado da sinopse, pelo mesmo painel usado pelo Super Nintendo. As 19 artes anteriores e seus índices 0–18 são preservados; as três novas famílias ocupam 19/20/21. Sete aliases são acrescentados sem alterar os 69 anteriores. As imagens mantêm transparência e proporção; a receita apenas redimensiona e acrescenta área transparente, sem redesenhar aparelho/logos ou remover fundo.

A composição final conferida contém cinco substituições, zero adições e 13.855 entradas preservadas: `libturbo_carousel.so`, três marcadores e `engines.json`. Todos os 40 DEX, Manifest/resources, biblioteca frontend, owners Sony, firmware/importador, controles, cores, opções, vídeos/loop, LEDs, fundo e lógica de economia/carregamento permanecem byte idênticos. A base é a R132 assinada SHA `47a619a194c095e70738a4d191fb8491ee410d868e4c11742530ffcaaf844f20`, 2.711.825.423 bytes, fonte `56ba83b7fcc5c243ae50052fec9b6f4141e1b5f1`.

As inversões independentes do CPP, consumer e aliases foram conferidas; os bytes dos 19 arrays e linhas anteriores permanecem iguais. A compilação reproduziu a biblioteca carousel R132 byte exatamente antes de aplicar o overlay. A conferência final independente verificou todos os mapas de entradas, o certificado original, assinatura v2/v3 e alinhamento 16 KiB. Isso comprova o pacote exato, sem comprovar desempenho ou gameplay.

## Custo e carregamento

As novas imagens acrescentam 3 MiB de dados RGBA estáticos. O cache existente cria uma textura por família visitada: nominalmente 1 MiB por nova família e até 3 MiB quando as três foram usadas no mesmo contexto. Permanecem a primeira varredura de transparência e o upload; o layout usa o conteúdo visível para ajustar a fotografia ao espaço disponível. O custo real do driver e a memória residente não foram medidos.

Não há shader, timer, decoder, thread ou download de imagem em runtime novo. O mesmo programa GL, contexto/restauração, criação lazy, regras de visibilidade e loading são mantidos por inversão exata. Isso não equivale a custo zero, menor aquecimento, tempo total de abertura menor ou ausência universal de vazamento. Não há medição física de desempenho, memória residente ou temperatura R133 nesta documentação.

## Modos online e servidor

O compartilhamento do carousel obriga renovar os quatro IDs existentes de PSX, N64, GameCube e Wii. Core, perfil de controle, canonical, protocolo e tetos de capacidade permanecem iguais: PSX até dois; N64/GameCube/Wii até quatro. Esses tetos não autorizam todo jogo ou todas as quantidades. Sony PS3/PS4 permanece exclusivamente offline.

| Plataforma | ID final R133 | Teto do motor |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-42e4ca5f2acc` | 2 |
| Nintendo 64 | `mupen64plus-ae-33bf7021-n64-v3-6dd47d4e0092` | 4 |
| GameCube | `dolphin-5102a033-gamecube-v3-5912af1feeef` | 4 |
| Wii | `dolphin-5102a033-wii-v3-5912af1feeef` | 4 |

A [matriz final do catálogo](catalog/summary.json) confronta todas as **3.940 entradas** do catálogo 33, das quais 3.685 visíveis e 255 aliases, com os 5.176 perfis e 321 modos históricos preservados. O ledger privado final é vinculado aos quatro IDs R133 exatos e prepara **78 modos em 44 jogos/edições**, sem aplicar o cadastro no servidor:

| Plataforma | Modos revisados preparados | Jogos/edições preparados |
| --- | ---: | ---: |
| Nintendo 64 | 66 | 33 |
| GameCube | 9 | 8 |
| Wii | 3 | 3 |
| PSX | 0 | 0 |
| **Total** | **78** | **44** |

O [recibo de preparo de modos](evidence/mode-preparation-hash-manifest.json) conserva o inventário de 22 inputs/outputs por hash/tamanho, os 321 modos antigos como prefixo exato e os 78 novos perfis vinculados aos IDs finais. O ledger e os recibos originais ficam no canal privado; o manifesto público não é entrada de implantação.

Cobertura da auditoria não significa aprovação universal: 110 jogos N64 ainda conservam apenas propostas genéricas em teste; 14 entradas N64/GC/Wii permanecem bloqueadas por evidência documental insuficiente ou perfil de controle/acessório incompatível; 17 jogos foram documentados como individuais. Há 337 entradas de sistemas sem engine online no cliente atual. Os 84 títulos PSX continuam pendentes de HLE por edição: dez dos 26 históricos multiplayer têm achados primários e 16 ainda carecem de fonte primária legível nesta janela, com **zero aprovação nova do owner PSX**. A matriz registra motivos por ordinal/título e separa compatibilidade histórica, preparação revisada, single-player e recusa.

As fontes reais estão em [web-mode-research.json](catalog/web-mode-research.json), [n64-root-mode-decisions.json](catalog/n64-root-mode-decisions.json), [n64-additional-primary-review.json](catalog/n64-additional-primary-review.json) e [psx-primary-consolidated.json](catalog/psx-primary-consolidated.json). São manuais/fichas do titular e decisões documentais identificadas por jogo/edição; não comprovam gameplay físico, HLE ou estabilidade dos dois aparelhos.

O último retorno real examinado, `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861`, ainda revisa o kit R129, informa kit privado não recebido/motores não ativados e `covers.published=false`. A recusa física R132 em Criar sala ocorreu antes de sala/jogo; não há código HTTP ou envelope bruto capturado. Os fatos históricos não comprovam o estado de produção R133.

## Reprodução e instalação

Usar somente `integration/console-panel-r133/recipe` sobre o donor exato e o backup completo existente. Todas as sete receitas públicas integram o congelamento; fotos oficiais, atlas RGBA e ferramentas ficam privados, com hashes no recibo. Não criar novos snapshots nem publicar APK, mídia, firmware, material RSA, logs pessoais ou recibos brutos privados.

`tooling/release` aceita a R133 somente com evidência final, assembly explícita, fontes/freeze/runtime/engines exatos e três ponteiros concordantes. Mantém a assinatura original, instalação direta por streaming e controles contra partida/download ativo. O par privado de identidade/pacote é necessário para o kit do servidor; recibos públicos sanitizados não substituem esses bytes. O instalador atual está no backup local, fora do Git. A instalação por streaming no Motorola Edge 30 terminou em `2026-10-11T21:41:18.825876+00:00`; a entrada oficial do frontend foi observada às `21:41:21.187453+00:00`, com o hash integral do APK conferido, assinatura original aceita, UID, data original de instalação e dados preservados, sem cópia extra de APK no telefone. O [recibo de instalação](evidence/installation.json) comprova somente essa atualização e abertura: sessão autenticada, aspecto das novas imagens, controle físico, gameplay, térmica e ativação do servidor não foram validados. Não há comprovação de instalação R133 em outro aparelho.
