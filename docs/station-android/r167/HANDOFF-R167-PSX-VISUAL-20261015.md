# R167 — capas vizinhas e continuidade do piloto PSX online

**Estado em 15/10/2026:** APK R167 assinado e kit independente privado conferidos localmente. O recibo do kit ainda indica `serverApplied=false`; a prova final indica `installed=false` e `stable=false`. Esta entrega não comprova cadastro no servidor, instalação em telefone, resultado visual físico ou partida PSX.

O [kit privado R167 sem APK](https://github.com/luziellacerda/TurboramaEmulationStation/tree/8a5fae55/station-server-handoffs/r167) e a [fonte/receita R167 do app](https://github.com/luziellacerda/TurboElden/tree/build/station-r167-neighbor-width-20261015) estão publicados. Este ramo é apenas o handoff; aplicar o cadastro e devolver o recibo é uma etapa distinta. Se a R166 ainda não foi cadastrada, use diretamente a R167 para evitar dois cadastros sucessivos.

## Escopo e herança

A R167 parte **somente** do APK R166 assinado, SHA-256 `4872f6dd0fef24930568c00c535a63afb66253f20a9c6a636627cadc52e71d6d`. Ela amplia em **10% a largura das capas vizinhas** na seleção de jogos, nos estilos normal, diagonal e projetado. A capa principal em evidência conserva tamanho e posição; a altura das vizinhas, os botões, a tela de sistemas, o toque e o mapeamento da arte permanecem como na R166. O desenho e a área tocável continuam usando o mesmo `coverSlot`. O recibo nativo de `E:\StationNeighborWidthR167-20261015\native\native-receipt.json` registra zero temporizadores, threads, workers, alocações ou desenhos adicionais por quadro; os testes de geometria mantêm separação positiva entre as capas em 2456×1080, 2340×1080, 1920×1080 e 1280×720.

A correção PSX da R166 está **herdada byte a byte**: `StationPsxLaunch.verifyBinaries` aceita exatamente a dependência oficial `libc++_shared.so`, continua exigindo o SHA-256 correto e recusa nomes inválidos. O código de sessão PSX, as rotas N64, os núcleos executáveis, as dependências, os perfis, as opções e as políticas de modo aprovado não foram alterados pela R167. Não há pedido de alteração da lógica N64 nem de reaplicação da saída do anfitrião.

## Prova da montagem e identidade

Fonte produtiva congelada no commit `9262776f6a5a0faf96b46cad2e9ca1d801606d6b`, ramo `build/station-r167-neighbor-width-20261015`. O recibo de montagem `F:\StationNeighborWidthR167-20261015\package\unsigned-package.json` e a prova final `F:\StationNeighborWidthR167-20261015\package\final-apk-proof.json` registram **13.861 entradas**, sendo **13.856 preservadas** contra a R166 e apenas estas cinco substituídas:

1. `lib/arm64-v8a/libturbo_carousel.so` — largura visual das capas vizinhas;
2. `assets/station-online/engines.json` — identidades vinculadas ao novo payload;
3. `lib/arm64-v8a/libstation_psx_online.so` — marcador PSX;
4. `lib/arm64-v8a/libstation_mupen64plus_runtime.so` — marcador N64;
5. `lib/arm64-v8a/libstation_dolphin_runtime.so` — marcador GameCube/Wii.

As **40 partições DEX foram preservadas**, inclusive `classes35.dex` SHA-256 `5eccedadc2d5fe22a4d9aac9f23620ce9c5e3749d101831e0d42fd1972c13580` e `classes36.dex` SHA-256 `a1c12b34c5eba4060707235c86dea3bbdfa59ef52264afc7ebaaa5d73231287c`. A ponte online `libstation_ae_bridge.so` conserva SHA-256 `3d0a9e9dc5ca31629f478023b849fe2037b4a68b9ac3689ba413a5f542191986`. O carrossel novo `libturbo_carousel.so` tem SHA-256 `82017952a6bf44a7a8c9098a3ef568fb0d2b809d99211be0e17785a77559d22a`; o anterior tinha `3f20f862bed0d73668fdbc0fa06e7009011cbf8ea32c2cfff96029c2b8f23fc8`.

| Plataforma | `engineId` exato R167 | SHA-256 do marcador runtime |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-4d5c4cb8f609` | `4d5c4cb8f609f5d1eb49a002d137d68172d03f59505443071d02903e25630991` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-cf820ae1f2ca` | `cf820ae1f2ca667c6b66019b64ed42f036780c7f5fbbcdbf766e153233a906fb` |
| GameCube | `dolphin-5102a033-gamecube-v3-5ce990f91db1` | `5ce990f91db164d970222f0230fe988f9d11ff071837e6b0b4b57fa15713fe60` |
| Wii | `dolphin-5102a033-wii-v3-5ce990f91db1` | `5ce990f91db164d970222f0230fe988f9d11ff071837e6b0b4b57fa15713fe60` |

O vínculo da identidade inclui o carrossel, portanto os quatro IDs mudam mesmo sem mudança funcional nos motores. **APK R167 assinado:** `F:\StationNeighborWidthR167-20261015\final\TurboStations-Premium-R167-combined.apk`, SHA-256 `dd6f8f8131cf5ee55d450c60e0b08963a662dc6cad092588f0db50153badd978`, 2.715.483.234 bytes. Certificado original SHA-256 `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`, assinatura v2/v3 e alinhamento 16 KiB conferidos. O APK não deve ser enviado ao Git.

**Kit privado sem APK:** `E:\StationNeighborWidthR167-20261015\server-kit\Kit-Servidor-TurboStations-R167-Independentes.zip`, SHA-256 `82cf0bff9f4ec3083fc395cd6cf38d6d9f227df6aac06b09b6d97882c9947477`, 13.632.995 bytes. Seu recibo `server-kit-receipt-r167.json` registra 48 arquivos, manifesto conferido, seis binários de motores, 21 dependências distintas, ausência de APK, ROM, BIOS, chave, serial e logs brutos. **Commit do kit privado:** `22ad0164404c6a78a9681d3c72ca7d508d727c04`, ramo `handoff/station-r167-combined-20261015` de `luziellacerda/TurboramaEmulationStation`. **Recibo aplicado do servidor: pendente.** Não interpretar o commit da receita do app como implantação do servidor.

## Ação delimitada do servidor

1. Receber o kit **pelo Git privado**, conferir o SHA-256 do ZIP, o manifesto e as entradas contra a prova do APK assinado. Não solicitar nem publicar o APK.
2. Conferir o estado real da R166 antes do cadastro. Se a R166 ainda não foi aplicada, cadastrar a R167 como entrega sucessora; se foi aplicada, adicionar os quatro owners R167 sem apagar os anteriores. Usar o registrador vigente, proteção de concorrência, backup e recibo aplicado. Preservar catálogo, licenças, capas, saves, downloads e as decisões existentes.
3. Requalificar os modos **por `itemId`, hash do conteúdo, motor, core, runtime, perfil e capacidade exatos**. O kit traz 78 referências de modos aprovados para comparação, não autorização para aprovar automaticamente. Manter os modos impedidos impedidos. O PSX continua com no máximo duas portas e as opções herdadas; as opções N64 permanecem idênticas às da R166. Não mexer na lógica nem no host-exit N64.
4. Verificar em consulta pública autenticada `POST /v1/station/online/multiplayer/command`, `action=capabilities`, `requestId` novo, `itemId` exato e `clientMaximumPlayers=5`: conferir os quatro IDs R167, a tupla completa de cada modo aprovado e as recusas esperadas. Sala criada, HTTP 200 genérico, código preparado ou teste sintético não substituem esse recibo.
5. Devolver recibo **aplicado** com baseline, contagens antes/depois, quatro IDs e hashes, SHA do kit aceito, decisões requalificadas, prova da consulta pública e estado preservado do host-exit N64.

## Pilotos físicos ainda necessários

Depois do recibo aplicado, instalar o **mesmo APK R167** nos dois telefones preservando dados. Primeiro verificar na seleção de jogos que apenas as capas vizinhas estão 10% mais largas e que a capa principal e a interação mantiveram sua geometria. Depois retomar o piloto PSX da [R166](../r166/HANDOFF-R166-PSX-ABRIR-PARTIDA-20261015.md) na edição aprovada **Capcom vs. SNK — Millennium Fight 2000 Pro**, `itemId=station_767e5e92f2f353479e6120f1c28f96a7`, conteúdo SHA-256 `6782ccca63a19cbc6760ca928b3c36d0e6ac4b3e479c722083c96111bd25cc73`, modo **VS Mode**, duas vagas. Confirmar que **Abrir partida** sai da sala, o primeiro quadro e o som aparecem em ambos, cada controle opera seu jogador e a saída humana retorna sem deixar o convidado preso. Registrar separadamente o hash instalado e a observação de cada aparelho. Até essa prova, **PSX online e R167 não devem ser declarados estáveis**.
