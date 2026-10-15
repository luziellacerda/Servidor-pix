# R164 — handoff da candidata de seleção de jogos e capa N64

**Estado em 15/10/2026:** o APK R164 está assinado e o `base.apk` instalado no Samsung A56 confere com o SHA-256 da candidata por `adb sha256sum`. A seleção de jogos foi capturada e exercitada no A56; o usuário aceitou o desenho com a proporção de 10% mantida. **O cadastro R164 no servidor, a publicação da nova arte de 1080° Snowboarding e o gameplay N64 online R164 ainda estão pendentes.** R164 não está homologada como estável. [Recibo físico sanitizado](evidence/a56-selection-qa.json).

## Identidades e limite da entrega

| Artefato | Identidade conferida |
| --- | --- |
| APK assinado R164 | SHA-256 `903700d5d6dcd63779df636cfb11bf94e2fadbeece462d2cb7a6cdf1dfbd5210`; 2.715.466.850 bytes |
| Certificado Android | SHA-256 `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |
| `libturbo_carousel.so` R164 | SHA-256 `bd3222558a43f8998d51b0b72010ecb8a94c7ae62a14d34b39863d13737a10d0` |
| Kit privado `Kit-Servidor-TurboStations-R164-Independentes.zip` | SHA-256 `37e3b9dd5b8efaae1a222b27661cb7a838689fa2b484b16268e2efa26d11457a`; 40 entradas |

O APK parte da R162 e troca exatamente cinco entradas: `assets/station-online/engines.json`, `libturbo_carousel.so` e os três marcadores de runtime independentes PSX, N64 e Dolphin. As outras **13.856 de 13.861 entradas** permanecem iguais. As 40 partições DEX, inclusive `classes35.dex` e `classes36.dex`, e a ponte online `libstation_ae_bridge.so` permanecem da R162. Os quatro `engineId` PSX, N64, GameCube e Wii ficam vinculados aos novos marcadores; conferir os valores completos em `engines-apk.json` do kit exato, sem copiar IDs de outra revisão. O kit contém seis ELF de motor e 21 dependências distintas, sem APK, ROM, BIOS, chave, serial ou log bruto.

Este repositório público leva [receita e escopo do app](../../../integration/game-selection-n64-cover-r164/README.md), código e documentação. **Não colocar APK, ZIP privado nem PNG/JPEG de capa neste Git.** O operador autorizado encontra [o kit e a arte na entrega privada R164](https://github.com/luziellacerda/TurboramaEmulationStation/blob/handoff/station-r164-selection-art-20261015/station-server-handoffs/r164/README.md). O handoff não envia APK.

## Cadastro delimitado no servidor

1. Obter uma baseline fresca e os recibos já aplicados; verificar o SHA-256 do ZIP, suas 40 entradas/manifesto, `engines-apk.json`, seis ELF e 21 dependências contra o APK R164 indicado. Não repetir cadastros R161/R162 nem aceitar um APK antigo no lugar da R164.
2. Sob `scan.lock`, com backup e rollback do operador vigente, cadastrar **aditivamente** os quatro `engineId` R164. Preservar owner, core, dependências, opções, controles e perfil aprovados da R162; N64 continua `cpu=dynarec`, `video=glide64mk2` e `inputDelay=2`. Não ampliar aprovação PSX HLE.
3. Vincular os 78 modos revisados e, após recibo e nova baseline, as 321 decisões N64 para 157 edições pelo fluxo vigente. Conferir capabilities assinadas, admissões e recusas com os IDs e hashes **R164**. Devolver recibo com baseline, transações, contagens, hashes e `serverApplied` efetivo. Até esse recibo, `serverApplied=false`.

O cadastro e os testes sintéticos do servidor não demonstram gameplay Android. Não alterar DLL, API, scanner, ROM, licenças reais, rede ou banda por esta mudança de seleção visual. Se alguma etapa falhar, usar o backup/rollback da transação correspondente e manter os owners anteriores; não apagar registros históricos para forçar a R164.

## Arte de 1080° Snowboarding: publicação separada

O app R164 reconhece a **nova** imagem PNG RGB 1024 × 1536, SHA-256 `2f8ee5b6d4c91979a7e75658def15f3daabc04b299aa44145f2c9a5efb3fdcc1`. A publicação N64 documentada no servidor aponta outra fonte, SHA-256 `b555c0f6ac036e5ce5375b49247db70b97968f6f8471341e1a337b19adaf234a`, e JPEG servido SHA-256 `ec46d4a1a1edc5ba10bae89e5451db5584bc67f8c16b7efc7f1e2c23eda5da59`. A captura física R164 ainda mostra a arte anterior de 1080° Snowboarding; a nova imagem **não foi publicada** neste handoff.

O alvo exato é `itemId=station_c13a5bbc179ccbe871697999c62ba13d` e `coverId=cover_c13a5bbc179ccbe871697999c62ba13d`. Antes de publicar, conferir catálogo e corpo servido atuais, fonte nova exata, edição e vínculo; preparar novo plano/baseline/backup e usar o publicador de capas vigente sob o lock próprio. Após aplicar, validar revisão do item, `coverId`, MIME, dimensões, bytes e SHA do corpo autenticado local e público. Se houver divergência, fazer rollback **somente desta alteração de capa** pelo backup do plano. Não reutilizar o ciclo de capas anterior nem substituir arte pelo nome parecido. [Registro da fonte/arte anterior](../online-plataformas-20261009/n64-capas-usb-20261013.json).

## Aceite físico e desempenho

A seleção R164 foi conferida em 2340 × 1080 no A56: capa grande 10% mais estreita, cards menores, faixa escura de ações alinhada à base da capa, atualização separada da busca e os cinco botões `JOGAR`, `JOGAR ONLINE`, `SAVES`, `APAGAR`, `VOLTAR` visíveis. Tocar a primeira minicapa N64 selecionou 1080° Snowboarding; atualização voltou à seleção 007/lista; buscar `1080` retornou `1/1` com o campo livre das estrelas. O usuário registrou: “MANTENHA PROPORÇÃO 10% COMO ESTAVA TODO RESTO DESIGN ESTA OK”. O [recibo](evidence/a56-selection-qa.json) contém somente checks e hashes das três capturas; nenhuma imagem foi colocada no Git.

Depois do cadastro e da publicação separados, conferir a **nova** capa pela revisão do catálogo e testar a mesma edição N64 online em dois aparelhos com a mesma revisão. Registrar primeiro quadro, vídeo, áudio, controles, velocidade e saída do anfitrião; tratar aviso ao convidado pela [auditoria de saída](../../../integration/n64-online-video-extension-r162/HOST_EXIT_AUDIT.md). O gameplay físico N64 online R164 não foi repetido após esta seleção.

O usuário relatou imagem online N64 e ausência de engasgos significativos na **R162** no A56. Isso é relato visual, sem medição instrumentada de quadros/latência, e não qualifica desempenho da R164. A receita R164 não acrescenta timers, threads, workers ou desenho por quadro, mas essa prova de escopo não substitui teste de desempenho no aparelho.
