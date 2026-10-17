# GameCube — desempenho R174 em comparação

R174/16 instalada e conferida no Moto g52 e Samsung A56, mesmo APK6ab94ff0.
Cadastro e provas local/pública/relay4P concluídos sem restart;115owners,
19893perfis/13472modos, catálogo102/4844 preservado. UID e dados mantidos.
A entrada R173 foi confirmada pelo proprietário, mas houve lentidão.

A candidata testa Core.CPUThread=false somente no GameCube online via camada
CurrentRun, com leitura nativa Active confirmada. A [observação R173](performance-observation.json)
localiza a maior espera medida do anfitrião no vídeo. O [Dolphin oficial](https://github.com/dolphin-emu/dolphin/blob/5102a0339c2177575378107b76541e47cc52122d/Source/Android/app/src/main/res/values/strings.xml)
alerta para problemas de Dual Core no netplay; isso fundamenta a comparação,
sem provar melhora antes da partida. [Recibo atual](server-return.json).

Falta a comparação física R174 de Mario Kart com sala nova e ambos na mesma
revisão. PSXinput8/N64/Wii/núcleos/renderer/resolução/buffer/clock preservados.
R172 permanece estável. Não repetir cadastro/instalação; não publicar oferta geral.

## Histórico da R173

# GameCube — revisão completa e correção de entrada

## Estado aplicado e instalado

R173/15 cadastrada e instalada no Moto g52 e Samsung A56; ambos os APKs lidos
conferem com7e15d21c. Provas local/pública das20edições e relay sintético4P
passaram, com3links/6conexões e recuperação. Consultar o [recibo atual](server-return.json).
Dados anteriores preservados; um restart do mesmo binário limpou um registro
sintético deixado por uma falha da prova inicial. Não repetir o cadastro.
O piloto físico de Mario Kart ainda deve confirmar imagem, som, controles e saída.


Base estável R172 preservada. Em 17/10, o proprietário pediu o GameCube e
relatou Mario Kart como “sala completa” com apenas um jogador.

## Diagnóstico confirmado

O serviço registrava Mario Kart com capacidade 2, um jogador e nenhuma conexão
de jogo. A R172 não reconhecia o perfil de apresentação
`dolphin-gamecube-four-ports-v1`. O probe da classe efetivamente usada retornou
`onlineApproved=false`, `confirmed=false`, `acceptingPlayers=false` e `vacancies=-1`.
A lista usava esse resultado para esconder Entrar e exibir a mensagem genérica
de sala em andamento/completa. A correção R173 reconhece somente esse perfil
exato, mantendo as validações de aprovação, jogadores e elenco.

82 verificações passaram na JVM e no DEX35 candidato executado no ART do Moto
g52 e A56. Outras 672 comparações de apresentação ficaram idênticas. Isso não
executou o emulador nem comprova gameplay. O teste de transporte nativo isolado
passou em 2.085 verificações com o ENet fixado. Os recibos finais distinguem
montagem, implantação, instalação e teste físico.

## Coleção e modos

- [Catálogo legível](CATALOGO.md) e [21 registros por edição](catalog-review.json).
- [Conferência dos arquivos](media-audit.json): 20 visíveis conferidos integralmente,
  um oculto preservado, formatos ISO/GCM/RVZ reconhecidos.
- [15 decisões adicionadas](mode-additions.json): Tech-Com Force de Terminator 3
  para dois, 12 modos individuais e duas recusas explícitas de modos com GBA.
- Total do motor atual: 24 decisões, dez modos multiplayer em nove jogos, com
  20 combinações de modo/quantidade. Capacidade de dois a quatro conforme o modo.

As 15 decisões foram aplicadas ao owner R172 sem reinício. A primeira prova HTTP
desse operador parou antes das salas: o apelido do próprio teste excedia os 24
caracteres permitidos. Os dados aplicados foram conferidos; esse operador não
deve ser repetido. A sucessora R173 verifica o conjunto completo e renova os
quatro owners cujo fingerprint cobre o DEX35 compartilhado.

Os modos que exigem GBA não são aprovados como quatro controles comuns.
Four Swords continua individual com controle GameCube; multiplayer e Tingle
Tuner exigem uma integração de telas/controles GBA que este perfil não oferece.
O motivo é explícito no perfil e não representa indisponibilidade da API.

## Consumo pelo aplicativo

1. Ler o catálogo autenticado e conservar `itemId`, revisão, conteúdo, formato
   e descritor de download. Nomes e capas não são identidades da ROM.
2. Ler `assets/station-online/engines.json` do APK instalado e selecionar o
   motor GameCube real, seu core/runtime e política de controles.
3. Pedir `capabilities` em `/v1/station/online/multiplayer/command` com request
   proof e validar a resposta assinada. Filtrar o perfil por edição **e motor
   exatos**. A resposta pode conter owners históricos.
4. Oferecer somente `approved=true` com a quantidade em `allowedPlayerCounts`.
   `maximumPlayers` do perfil pode ser 4 enquanto a sala escolhida tem 2 vagas.
   Perfil individual não concede vagas. Mostrar a explicação de recusas GBA.
5. Enviar os sete campos do vínculo: `itemId`, `contentSha256`, `engineId`,
   `coreSha256`, `runtimeSha256`, `profileId`, `profileSha256`. Entrar/iniciar
   com a geração atual recebida. Cada aparelho ocupa uma posição independente.
6. Manter presença e a autoridade da sala. O Dolphin nativo transporta ENet em
   datagramas DPL1 sobre os links autenticados `station-stream.v3`. A sala em
   `playing` e o HTTP 200 não comprovam quadro, som ou controles do jogo.

Depois da atualização de ambos, criar **sala nova**: os IDs R173 e R172 são
distintos e não devem ser misturados. O servidor e o APK não devem transformar
um desencontro de motor em aprovação por título. A assinatura e a prova de
requisição existentes continuam obrigatórias; nenhuma rota nova foi aberta.

## Escopo e piloto

Os núcleos, BIOS, bibliotecas reais, opções de emulação, transporte, rede,
downloads e PSX inputDelay8 permanecem preservados. Não há ajuste de FPS,
frameskip, renderer, clock ou buffer do Dolphin nesta revisão. O guia oficial
explica sincronização determinística e ajustes de buffer; não foi assumido um
defeito de desempenho antes de medir GameCube nestes aparelhos.

Piloto: mesma R173 comprovada nos dois, Mario Kart Double Dash, sala nova para
dois, ambos prontos, modo local VS. Observar primeiro quadro/som, controles
independentes e saída pelo menu. Somente esse resultado pode confirmar gameplay.
Até o recibo físico, a R172 permanece o ponto estável, sem oferta geral do APK.

Fontes primárias: [Dolphin NetPlay](https://dolphin-emu.org/docs/guides/netplay-guide/),
[NetPlay Android na 2609](https://dolphin-emu.org/blog/2026/09/24/dolphin-progress-report-release-2609/),
[Dolphin fixado 5102a033](https://github.com/dolphin-emu/dolphin/tree/5102a0339c2177575378107b76541e47cc52122d).
As fontes dos modos estão vinculadas a cada decisão. Nenhum manual integral,
jogo, BIOS, credencial, serial, APK ou log privado foi incluído no Git.
