# Aplicativo → servidor: cadastro R162 para teste físico N64

**Estado:** recebido como pedido de preparação, não como cadastro aplicado. A R161 continua cadastrada. O APK R162 está instalado no moto g52 e no A56; no A56, a instalação por cima preservou os dados e o `firstInstallTime` de `2026-10-13 20:22:47`, e o hash do APK instalado foi conferido como `7ec742c4cae7b663f2198e347253986b4c4c5d0d0461052dd75df56ca6335dd7`. O teste físico online R162 ainda não ocorreu. Nenhum APK integra esta entrega ao Git ou ao servidor, conforme orientação do proprietário.

## Arquivo necessário

`Kit-Servidor-TurboStations-R162-Independentes.zip`, **12.338.080 bytes**, SHA-256 **`a777e90a1c5add265aed328e13c884af1277a8dee6770be4ca109d431aeec942`**. O produtor o guardou em `E:\StationN64OwnerAudit-20261014\kit-r162-video\`. O ZIP possui 40 entradas com manifesto conferido: seis bibliotecas de motor, 21 dependências (uma nova para N64), configuração das quatro identidades e recibos de construção. Não contém APK, ROM, BIOS, serial ou log bruto. O hash do APK assinado e instalado nos dois aparelhos consta apenas para correlação: `7ec742c4cae7b663f2198e347253986b4c4c5d0d0461052dd75df56ca6335dd7`.

**Entrega pelo GitHub privado:** o proprietário determinou handoff pelo GitHub, sem pen drive e sem envio do APK. O ZIP integral está na [branch privada de entrega R162](https://github.com/luziellacerda/TurboramaEmulationStation/tree/handoff/station-r162-n64-video/station-server-handoffs/r162), commit `67532fd61f26c3a975c4be53733a9967f04918cd`, junto do [recibo do kit](https://github.com/luziellacerda/TurboramaEmulationStation/blob/handoff/station-r162-n64-video/station-server-handoffs/r162/server-kit-receipt-r162.json) e das [instruções privadas](https://github.com/luziellacerda/TurboramaEmulationStation/blob/handoff/station-r162-n64-video/station-server-handoffs/r162/README.md). O [arquivo ZIP exato](https://github.com/luziellacerda/TurboramaEmulationStation/blob/handoff/station-r162-n64-video/station-server-handoffs/r162/Kit-Servidor-TurboStations-R162-Independentes.zip) foi conferido byte a byte antes do envio e possui o SHA-256 acima. Não foi publicado no repositório público porque se declara kit privado e seu aviso de licenças não cobre redistribuição pública dos 27 ELF. O operador precisa confirmar acesso autorizado ao repositório privado; a entrega não comprova cadastro aplicado.

| Sistema | Motor R162 | SHA-256 do runtime |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-bf682d6798e7` | `bf682d6798e74cb46cd221ecceb10475e4f7fb2d11e1da45a84e2f25ac7ad4ab` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-57155617f470` | `57155617f470e39144ba050169bdf5801f0392f62995a95d0116a4497ce0fd1e` |
| GameCube | `dolphin-5102a033-gamecube-v3-3bfbe6be43a7` | `3bfbe6be43a7c507a381b4d5ef9a0f7571737031453eb7254835687199972796` |
| Wii | `dolphin-5102a033-wii-v3-3bfbe6be43a7` | `3bfbe6be43a7c507a381b4d5ef9a0f7571737031453eb7254835687199972796` |

## Causa física e escopo

No R161, 007: The World Is Not Enough exibiu imagem offline no g52 e ficou preto online com áudio lento. O log online chegou a `Netplay is ready`/`PLAYING`, depois registrou `SDL_SetVideoMode failed`. A fonte oficial do Mupen64Plus AE registra `CoreOverrideVidExt` na biblioteca offline; o app online usa outro core. R162 acrescenta uma ponte Android específica para registrar o vídeo no core online. O core N64, o plugin Glide64mk2, Dynarec, RSP HLE, `inputDelay=2`, controle, máximo de quatro jogadores, transporte, perfil e motor offline não mudaram. O app assinado preserva 13.854 das 13.860 entradas anteriores; muda dois DEX, `engines.json`, três marcadores e acrescenta uma biblioteca. A ponte offline mantém SHA-256 `ba5a7582295215f159e197f805dbfe85851aa912462996870f61ccd752ecb360`.

Os marcadores PSX e Dolphin mudam apenas pelo vínculo criptográfico global ao APK. As opções e políticas desses motores não foram ampliadas. A fonte e a prova detalhada estão no handoff do repositório TurboElden, `docs/server/r162/README.md`, e no diagnóstico R161, `docs/server/r161/physical-n64-video-failure-20261015.md`.

## Procedimento do operador

1. Confirmar acesso autorizado à branch privada, obter baseline fresca e recibos aplicados R161. Baixar o ZIP integral R162, conferir SHA-256 e manifesto interno, e disponibilizar os 27 ELF reais em diretório de preparo: o registrador exige os arquivos físicos `library` e `runtimeLibrary` de cada motor no diretório `--artifacts`. Conferir seis bibliotecas de motor e 21 dependências; não reaplicar R161 nem substituir a nova ponte por `libae-bridge.so` offline. Se o acesso privado ou qualquer byte exigido faltar, devolver a exigência sem cadastrar.
2. Sob o registrador vigente, `scan.lock`, snapshot e rollback próprios, cadastrar aditivamente os quatro motores exatos R162. Manter o perfil N64 `direct-four-ports-v1`, SHA canônico Android `ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63`, `cpu=dynarec`, `video=glide64mk2`, `rsp=hle` e `inputDelay=2`. Preservar PSX HLE sem aprovação nova.
3. Vincular os 78 modos revisados, gravar recibo, obter outra baseline fresca e vincular as 321 decisões N64 das 157 edições revisadas. Conferir capacidades assinadas, admissões e recusas para os identificadores R162. Não transformar teste sintético em prova de gameplay Android.
4. Devolver recibo efetivo: hashes do pacote recebido e dos artefatos, IDs, baseline anterior e posterior, transações, contagens, capacidade assinada, testes e `serverApplied`. Se o operador atual exigir o APK completo como entrada obrigatória, **não** usar R161 nem declarar R162 aplicada: devolver essa exigência; o proprietário orientou não enviar APK.

Não há alteração requerida de API, DLL, scanner, catálogo, banda, licença ou dados de jogos para a correção de vídeo. A partida real deve ser testada com R162 nos dois telefones após o cadastro. O convidado não foi avisado imediatamente quando o anfitrião saiu da partida R161; é um defeito separado e não foi misturado neste APK. A prova do convidado e a correção delimitada constam de `integration/n64-online-video-extension-r162/HOST_EXIT_AUDIT.md` no repositório do app.
