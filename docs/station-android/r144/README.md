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

Quatro IDs independentes foram renovados pelo vínculo obrigatório com DEX28/carrossel, sem mudar os owners/cores/opções/controles ou os nove descritores RetroArch. Kit R144 `7b1cdb6c68a1ddcd05af2e4703d2900061f7f2c4082a3f3c80c6d4c428d18739`, 12.164.472 bytes, 38 arquivos, seis bibliotecas/vinte dependências, apenas mesmos 78 modos revisados. Kit preparado, não aplicado; SNES conserva cadastro existente. Não reaplicar R141, migrar modos duas vezes, alterar licenças ou implantar/reiniciar API. PS1 zero novas aprovações HLE. [Provas e estado](STATUS.json).

## Histórico preservado

