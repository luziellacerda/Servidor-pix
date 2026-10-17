## R144 ativada por relato humano; refinamento visual R145 solicitado

O código fornecido pelo mantenedor foi preenchido no campo protegido e o botão Entrar foi acionado no A56. Depois o USB foi desconectado; a confirmação posterior foi humana: “Abriu a tela inicial”. Nenhum código é copiado ao Git. A instalação R144 permanece comprovada pelo SHA do APK e identidade do novo pacote. A abertura pós-ativação não foi observada por USB.

O mantenedor considerou o fundo azul ainda escuro: pediu azul céu em todas as telas que usam azul. A R145 deverá conservar fundos neutros/preto/verde e alterar os fundos azuis e as faixas solicitadas: SNES vermelho/vermelho/cinza; NES roxo/vermelho/cinza; N64 amarelo fino/verde/azul/vermelho/amarelo fino; GameCube amarelo/verde/roxo/cinza; PSX e PS2 vermelho/azul/amarelo; Dreamcast verde/azul/vermelho/amarelo/laranja; FinalBurn Neo verde/amarelo/laranja. Demais paletas R144 continuam. A aprovação visual da R144 é negativa para o fundo; não promovê-la a estável.

Após o visual e a comparação do handoff, foi solicitado investigar e corrigir a operação online de N64, PS1, GameCube e Wii. Cruzar erros reais, motores entregues e kit registrado, preservando outros comportamentos. Essa investigação ainda não foi implementada na fonte R144, que segue congelada em 1ac3f7d. A tag/backup estável R143 continua intacta.

## Histórico preservado

## R144 no A56: acesso aguardando código novo

A instalação nova está conferida pelo SHA do APK e identidade do pacote. A hierarquia do próprio aplicativo confirmou o campo protegido de senha de acesso e o botão Entrar. A captura preta é compatível com o FLAG_SECURE já existente em LoginActivity; não alterar essa proteção nem atribuir à paleta um travamento. O código atual do A56 não foi encontrado nos handoffs: o painel documenta códigos curtos, de uso único, sem recuperação do texto anterior. O acesso ao navegador falhou após uma reinicialização e tentativa de recuperação. Foi solicitado ao mantenedor o código novo da licença do A56 para preenchê-lo no aplicativo. Nenhum código foi digitado, nenhuma licença emitida/trocada e nenhuma mensagem enviada. Teste visual do carrossel e das salas ainda pendente.

## Histórico preservado

## Sexta tentativa R144 — instalação nova autorizada

A sexta tentativa instalou a R144 como instalação nova autorizada no Samsung A56. O SHA integral do APK e a identidade do novo pacote foram conferidos. A entrada oficial não foi observada automaticamente; a abertura da interface e a aprovação visual ainda precisam de confirmação humana.

A quarta tentativa registrou INSTALL_FAILED_INSUFFICIENT_STORAGE. Depois, havia aproximadamente 15,15 GB disponíveis. A quinta tentativa parou na pré-validação, antes de instalar: o pacote estava ausente globalmente e para o usuário 0. O diagnóstico foi somente leitura. A inspeção de cópias de instalador apagou zero arquivos e não desinstalou nem limpou o aplicativo. A causa da remoção prévia não foi estabelecida. Por isso a sexta operação usou instalação nova, autorizada por “instale”; preservação do UID/dados anteriores não se aplica.

Mesma fonte congelada 1ac3f7d e APK 01427f4e. Teste visual físico pendente, rede v3 não integrada, helpers antigos exatos, tag e backup completos R143 preservados. Nenhuma recompilação, lógica, deploy/restart ou cadastro aplicado por este registro. O checkpoint candidato inicial foi validado com 2.047 arquivos, 158 inputs nativos e dois bundles; depende do ponto completo R143. As novas tentativas serão anexadas em diretório distinto, sem substituir o checkpoint inicial.

[Recibo da instalação nova](evidence/instalacao-r144-samsung-a56-tentativa6-nova.json).

## Histórico preservado

## Quarta tentativa R144 — resultado conferido

A quarta tentativa não confirmou a instalação da R144. O recibo registra o erro Android INSTALL_FAILED_INSUFFICIENT_STORAGE. Não atribuir outra causa nem declarar a versão instalada. Antes desta tentativa, o A56 estava ocioso com 7.564.333.056 bytes disponíveis. O teste visual físico permanece pendente. A fonte congelada 1ac3f7d e o APK 01427f4e são os mesmos; nenhuma recompilação, lógica ou cadastro foi alterado por este registro.

O checkpoint candidato anterior foi validado com 2.047 arquivos, 158 inputs nativos e dois bundles Git. A recuperação depende do backup completo estável R143. A quarta tentativa será anexada em atualização distinta; o ponto inicial e a estável R143 permanecem preservados.

[Recibo da quarta tentativa](evidence/instalacao-r144-samsung-a56-tentativa4.json). [Resumo sanitizado do backup](evidence/candidate-backup-summary.json).

## Histórico preservado

## Terceira tentativa R144: armazenamento ainda insuficiente no A56

Antes da terceira tentativa, o A56 estava ocioso e dispunha de aproximadamente 6,88 GB livres. O Android voltou a recusar a atualização com INSTALL_FAILED_INSUFFICIENT_STORAGE. Não houve confirmação de instalação da R144, desinstalação ou limpeza de dados. O último APK conferido antes desta tentativa era a R143. É necessário liberar mais espaço interno ou usar outro telefone ocioso autorizado.

Carrossel: paletas solicitadas por sistema e ondas mais visíveis. Salas online: somente brilho, mantendo RGB, tema e desenho. O teste visual físico ainda não foi aprovado pelo mantenedor. A R143 permanece como ponto estável de retorno, com tag, backup e ponteiros permanentes preservados. O kit independente R144 foi preparado, sem aplicação no servidor.

[Recibo da terceira tentativa](evidence/instalacao-r144-samsung-a56-tentativa3.json).

## Histórico preservado

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
