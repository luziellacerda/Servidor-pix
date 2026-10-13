## R142 — candidato para 3/4 jogadores SNES, 13/10/2026

Relato atual: Top Gear 3000, Corrida VS, três participantes na mesma sala, opções 3/4 escuras e indisponíveis. O g52 registrou dois canais v3 (posições 2 e 3), ambos em estado 2. Capturas e logs privados preservados. A conexão da sala não comprova que o jogo recebeu o multitap nem que existe corrida humana jogável.

Foi confirmada uma falha de entrega no aplicativo: as chaves de tipo de dispositivo eram gravadas em retroarch.cfg, enquanto o leitor do runtime exato as consome em arquivos .rmp. A R142 entrega o vetor assinado `[1,257,1,1,1]` em um arquivo de controles privado da sala, somente para SNES com `snes-multitap-port2-v1`. Um teste host dos corpos reais do leitor/inicializador confirmou dispositivo257 na porta2 sem mudar botões/portas/turbo sob fixtures controladas; Android e gameplay ainda precisam teste. [Escopo, receita e limitações](https://github.com/luziellacerda/TurboElden/blob/9a38881f467ee533b4f8bcff3ea6769b98401175/integration/snes-multitap-delivery-r142-20261013/README.md).

APK R142 `f99b51a0…`, fonte congelada `9a38881f467ee533b4f8bcff3ea6769b98401175`, assinado original v2/v3/16KiB. Cinco substituições, zero adições/remoções, 13.855 entradas e 39 DEX preservados. DEX28 de downloads R141 exato; runtime RetroArch, core SNES, HLE R140, saída humana, FPS, transporte, READY/ACK, catálogo e perfis exatos. Campeonato em dupla e Super Mario Kart continuam dois; Top Gear Corrida VS `[2,3,4]`, nunca cinco.

Os nove descritores/oito IDs RetroArch permanecem: este ajuste de SNES não requer mudança no servidor. Três marcadores de composição e quatro IDs independentes foram renovados por vincularem DEX35; o kit privado R142 SHA `3163f54225860816be10fd4388534dfba655261f8946a82c4853c71eb2832cc2`, 12.160.430bytes, 38arquivos, seis bibliotecas/vinte dependências, está preparado para renovar somente os mesmos 78 modos revisados. Preservar todos os cadastros R141/anteriores; usar o preparador de lista fixa corrigido, sem duplicar a migração genérica. PS1 zero novas aprovações HLE. Kit preparado não é cadastro aplicado. Não deploy/restart por consequência. [Estado](STATUS.json).

Instalação aguarda g52 ocioso, pois ainda estava no jogo no último preflight. Manifesto do BAT de testes aponta explicitamente para R142, scripts preservados; ponteiros permanentes de recuperação intactos. Nenhuma prova de gameplay 3/4 é inferida da compilação ou do teste host. Binários/ROM/BIOS/chaves/seriais/capturas privadas foraGit.

## Histórico preservado

