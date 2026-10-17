## R142 instalada no moto g52 — 13/10/2026

APK R142 `f99b51a04f4967c086533157835868dcc6a22a3eb06eb63706886f9f60453b83` instalado no moto g52 e hash integral conferido; UID, dados e primeira instalação preservados. Fonte executável congelada `9a38881f467ee533b4f8bcff3ea6769b98401175`. [Recibo](evidence/installation-moto-g52.json).

As duas primeiras tentativas recusaram o serviço residual antes de instalar. Após o mantenedor sair da partida, não havia emulador filho nem DownloadService: restava somente o ForegroundService próprio. O Android recusou parar diretamente esse serviço; o aplicativo já fora da partida foi encerrado, sem apagar dados, e as verificações originais de repouso passaram. A terceira tentativa instalou. O instalador não comprovou visualmente o frontend; a abertura e o teste real dependem do usuário. [Preparação sanitizada](evidence/installation-preparation-g52.json).

Teste solicitado: g52 como anfitrião, Top Gear 3000 Corrida VS com três participantes, opção3 e comando independente do terceiro carro. A56 e Edge30 permanecem R141 neste momento; SNES/RetroArch/core/perfil da sala são byte exatos entre R141/R142. Não inferir corrida válida ou sincronismo por essa igualdade; o primeiro teste físico está pendente. Atualizar outros aparelhos autorizados quando disponíveis, sem interromper partidas/downloads. Quatro IDs independentes R142 e seu kit continuam aguardando cadastro; este reparo de SNES conserva cadastro válido e não requer alteração no servidor. Downloads R141 e reparos anteriores preservados; fontes/recibos no Git, APK/kit/registros privados no backup F existente.

## Histórico preservado

## R142 — candidato para 3/4 jogadores SNES, 13/10/2026

Relato atual: Top Gear 3000, Corrida VS, três participantes na mesma sala, opções 3/4 escuras e indisponíveis. O g52 registrou dois canais v3 (posições 2 e 3), ambos em estado 2. Capturas e logs privados preservados. A conexão da sala não comprova que o jogo recebeu o multitap nem que existe corrida humana jogável.

Foi confirmada uma falha de entrega no aplicativo: as chaves de tipo de dispositivo eram gravadas em retroarch.cfg, enquanto o leitor do runtime exato as consome em arquivos .rmp. A R142 entrega o vetor assinado `[1,257,1,1,1]` em um arquivo de controles privado da sala, somente para SNES com `snes-multitap-port2-v1`. Um teste host dos corpos reais do leitor/inicializador confirmou dispositivo257 na porta2 sem mudar botões/portas/turbo sob fixtures controladas; Android e gameplay ainda precisam teste. [Escopo, receita e limitações](https://github.com/luziellacerda/TurboElden/blob/9a38881f467ee533b4f8bcff3ea6769b98401175/integration/snes-multitap-delivery-r142-20261013/README.md).

APK R142 `f99b51a0…`, fonte congelada `9a38881f467ee533b4f8bcff3ea6769b98401175`, assinado original v2/v3/16KiB. Cinco substituições, zero adições/remoções, 13.855 entradas e 39 DEX preservados. DEX28 de downloads R141 exato; runtime RetroArch, core SNES, HLE R140, saída humana, FPS, transporte, READY/ACK, catálogo e perfis exatos. Campeonato em dupla e Super Mario Kart continuam dois; Top Gear Corrida VS `[2,3,4]`, nunca cinco.

Os nove descritores/oito IDs RetroArch permanecem: este ajuste de SNES não requer mudança no servidor. Três marcadores de composição e quatro IDs independentes foram renovados por vincularem DEX35; o kit privado R142 SHA `3163f54225860816be10fd4388534dfba655261f8946a82c4853c71eb2832cc2`, 12.160.430bytes, 38arquivos, seis bibliotecas/vinte dependências, está preparado para renovar somente os mesmos 78 modos revisados. Preservar todos os cadastros R141/anteriores; usar o preparador de lista fixa corrigido, sem duplicar a migração genérica. PS1 zero novas aprovações HLE. Kit preparado não é cadastro aplicado. Não deploy/restart por consequência. [Estado](STATUS.json).

Instalação aguarda g52 ocioso, pois ainda estava no jogo no último preflight. Manifesto do BAT de testes aponta explicitamente para R142, scripts preservados; ponteiros permanentes de recuperação intactos. Nenhuma prova de gameplay 3/4 é inferida da compilação ou do teste host. Binários/ROM/BIOS/chaves/seriais/capturas privadas foraGit.

## Histórico preservado

