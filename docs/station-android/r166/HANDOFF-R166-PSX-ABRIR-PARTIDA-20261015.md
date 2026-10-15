# R166 — correção da abertura PSX online e cadastro das novas identidades

**Estado em 15/10/2026:** o APK R166 foi montado, assinado e conferido; o kit independente foi gerado e conferido. Cadastro no servidor e partida física R166 ainda estão pendentes. Este documento não é recibo de implantação.

O [kit privado sem APK](https://github.com/luziellacerda/TurboramaEmulationStation/tree/0b4dc002/station-server-handoffs/r166) está no ramo `handoff/station-r166-psx-open-20261015`; a [fonte e receita do app](https://github.com/luziellacerda/TurboElden/tree/build/station-r166-psx-open-20261015) estão em ramo separado. Este ramo de handoff não efetua o cadastro: aplicar o registrador do servidor e devolver recibo comprovado.

## Incidente comprovado em dois telefones R165

O Galaxy A56 e o moto g52 estavam na R165. Na edição aprovada **Capcom vs. SNK — Millennium Fight 2000 Pro**, modo **VS Mode** para duas pessoas, os participantes chegaram à mesma sala. No g52, tocar em **Abrir partida** não abriu o emulador. A captura mostrou o anfitrião e o convidado na sala, com a opção de reconectar após a falha. O registro do aparelho chegou a `StationRooms: launch stage=prepare` e terminou em `java.io.IOException` em `StationPsxLaunch.verifyBinaries:142`, antes de iniciar o processo PSX. Um HTTP 409 de geração anterior foi sucedido pela preparação da geração atual; ele não explica esta falha.

A dependência real `libc++_shared.so` estava instalada, e o SHA-256 no telefone correspondia ao manifesto assinado. A validação de nomes de `StationPsxLaunch` aceitava apenas `lib[A-Za-z0-9_-]+\.so`: o caractere `+` do nome oficial era recusado antes da comparação do hash. Logo, a mensagem genérica de conexão ocultava uma recusa **local do aplicativo**, não uma ausência de modo aprovado nem uma falha de cadastro R165. A [publicação R165 do servidor](../r165/README.md) já reconhece os quatro motores e os 23 modos PSX aprovados.

## Mudança cirúrgica no aplicativo

A única alteração funcional é admitir **exatamente** `libc++_shared.so` como exceção à validação de nome em `StationPsxLaunch.verifyBinaries`, mantendo a exigência de SHA-256 correto e a rejeição de qualquer outro nome inválido. O teste da sessão PSX agora inclui essa dependência e também exige recusa quando seu hash é adulterado. Não foi criado timer, thread, consulta ou desenho adicional por quadro.

O recibo local de DEX `E:\StationPsxDependencyR166-20261015\dex\dex-receipt.json` registra 608 classes na partição `classes35.dex`: apenas `StationPsxLaunch` mudou; as outras 607 classes, inclusive as rotas N64, preservaram a projeção anterior. O recibo de montagem sem assinatura `D:\StationPsxDependencyR166-20261015\package\unsigned-package.json` registra 13.861 entradas no APK, sendo **cinco substituídas** e **13.856 preservadas** byte a byte contra a R165 assinada, SHA-256 `9e7ab2441e9d75568d5d2b741b4a0ed42e9a2d9f823698c0f39dccdcb7c19145`:

1. `classes35.dex`;
2. `assets/station-online/engines.json`;
3. `lib/arm64-v8a/libstation_psx_online.so`;
4. `lib/arm64-v8a/libstation_mupen64plus_runtime.so`;
5. `lib/arm64-v8a/libstation_dolphin_runtime.so`.

As três bibliotecas de marcador mudam por causa do vínculo criptográfico com o DEX; isso **não altera a lógica de jogo N64, GameCube ou Wii**. Núcleos, 21 dependências, opções, perfis de controle, `classes36.dex`, ponte online, carrossel e as outras 39 partições DEX permanecem iguais. A saída confirmada do anfitrião N64 já está implantada no servidor R165, DLL SHA-256 `53d4adc9c1143f09c6b136ee085cb599dbe58a286b582107cc88a640483b2eb2`; não há pedido de nova implantação dessa lógica.

## Identidades exatas da candidata R166

As identidades abaixo vêm do `unsigned-package.json` da montagem R166, fonte congelada no commit `e9dbad1e6c0477aee81a4040450ce4038877d98c`, e foram conferidas no APK assinado e no kit. O `engineId` é o prefixo da plataforma seguido dos 12 primeiros caracteres do SHA-256 do respectivo marcador ELF.

| Plataforma | `engineId` R166 | `runtimeSha256` R166 |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-3794feb8bb10` | `3794feb8bb10dec2319bf9c367242fc2bdcf482b4d5eee1e7e6cfba8688699aa` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-803a0bee5c5d` | `803a0bee5c5deb7314696d2a63424cf6c3de995befece526ab774eb6fdbf830d` |
| GameCube | `dolphin-5102a033-gamecube-v3-bf11f4e2851c` | `bf11f4e2851c5186d0318fa11130af6079a22ea1c660d34a07e0b346ded00df8` |
| Wii | `dolphin-5102a033-wii-v3-bf11f4e2851c` | `bf11f4e2851c5186d0318fa11130af6079a22ea1c660d34a07e0b346ded00df8` |

**APK R166 assinado, SHA-256:** `4872f6dd0fef24930568c00c535a63afb66253f20a9c6a636627cadc52e71d6d`, 2.715.466.850 bytes, certificado original, v2/v3 e alinhamento 16 KiB conferidos.

**Kit privado R166 sem APK, SHA-256:** `b2b4eb8432c8dcb19327716aca3a20e56f84e1834a08d1b683e2a27ad5920b87`, 12.988.932 bytes, 45 entradas conferidas.
**Commit de entrega do kit privado:** `e8ef43287f1cb80d305d418ba0314f58b9f7da70` no ramo `handoff/station-r166-psx-open-20261015` de `luziellacerda/TurboramaEmulationStation`.

O SHA-256 `1be17880602894e5458f91d0119bb534cdd36fcb0a7bb84e1f3ea533a9c71c76` identifica apenas o **APK sem assinatura**, não o arquivo instalável final. Não usar esse hash para instalação ou recibo do servidor. A entrega ao servidor deve ocorrer por handoff no Git privado, sem APK, ROM, BIOS, chave, serial ou registros brutos dos telefones.

## Ação delimitada do servidor, após receber o kit conferido

1. Verificar o SHA-256 do ZIP privado e de cada entrada do manifesto; comparar `engines-apk.json`, `incoming.json`, políticas, seis ELF e 21 dependências com a prova do APK assinado. Conferir os quatro pares `engineId`/`runtimeSha256` acima e todos os campos imutáveis de core, opções e controlador. O servidor não precisa receber o APK.
2. Usar o registrador vigente com baseline atual, proteção `scan.lock`, backup e recibo para cadastrar **aditivamente** os quatro owners R166. Preservar registros R164/R165, catálogo, capas, licenças, saves, downloads e demais serviços. Não reaplicar a alteração do host-exit N64 nem reiniciar a API sem necessidade operacional comprovada.
3. Requalificar as decisões por **itemId, conteúdo, core, runtime, perfil e capacidade exatos**. Na R165 havia 84 edições PSX analisadas, 23 modos online de duas portas em 19 edições aprovados, 17 modos impedidos e 53 classificações locais sem vagas. Não aprovar os modos impedidos nem inferir elegibilidade pelo nome do jogo. Para os novos IDs das demais plataformas, preservar as decisões comprovadas da R165: N64 com 230 modos multiplayer em 157 edições examinadas, GameCube com nove modos e Wii com três; a identidade nova exige vínculo novo, mesmo com lógica N64 intacta.
4. Registrar recusas pertinentes e conferir consulta **local e pública autenticada**, `POST /v1/station/online/multiplayer/command` com `action=capabilities`, `requestId` novo, `itemId` exato e `clientMaximumPlayers=5`. Validar assinatura, `snapshot.multiplayerVersion=3`, `station-multiplayer.v3` e a tupla completa `itemId`/conteúdo/engine/core/runtime/profileId/profileSha256/modo/`allowedPlayerCounts`. HTTP 200, `serverReady` genérico ou sala criada não comprovam liberação do owner R166.
5. Devolver recibo **aplicado**, com baseline e contagens antes/depois, quatro IDs e hashes, SHA do kit aceito, decisões PSX admitidas/recusadas, prova da consulta pública, estado preservado do host-exit N64 e qualquer reinício efetivo. Código preparado, QEMU e transporte sintético não equivalem a gameplay Android.

### Piloto físico após o recibo aplicado

Usar a **mesma R166 assinada nos dois telefones**, com a mesma edição `station_767e5e92f2f353479e6120f1c28f96a7`, conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`, modo `versus`/VS Mode e duas vagas aprovadas. Conferir que **Abrir partida** sai da sala, o emulador exibe o primeiro quadro com som e cada controle opera seu jogador; depois conferir saída humana e retorno do convidado. Registrar separadamente o hash instalado em cada telefone e a observação real. Até isso ocorrer, a R166 é candidata, não versão estável de PSX online.
