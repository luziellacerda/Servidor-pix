## Retorno do servidor aplicado em 12/10/2026

Ver [a matriz administrativa e provas reais](server-return.json) e [o mesmo handoff consolidado](../HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md). O kit RetroArch R138 recebido foi aplicado; o estado pendente do texto original abaixo era anterior ao retorno. Seus recibos de APK/fontes permanecem intactos.

# R138 — correção do início SNES e encerramento nativo

Entrega APP → SERVIDOR, 12/10/2026. **APK R138 final assinado SHA-256 `9183462e4d45cfcf2d2599d9e682b81890fee7b094505bf3073ab8183dd1dd10`, 2,714,991,631 bytes**, certificado original v2/v3 e alinhamento 16 KiB. Instalado no moto g52 e no Edge 30: hash integral, UID e dados preservados; frontend g52 observado e Edge 30 conforme [recibo](evidence/instalacao-edge30.json). Cadastro produtivo, saída física sem crash e partida em rede continuam pendentes nesta entrega. Conferir [STATUS.json](STATUS.json) antes de usar o kit. Este documento e seus metadados não executam publicação de registros ou reinício do serviço.

## Causa comprovada do carregamento nos dois jogos

Super Mario Kart precisa DSP1 e Top Gear 3000 precisa DSP4. A configuração anterior do bsnes Mercury selecionava LLE, que procura firmware `dsp1b.program.rom`/`dsp1b.data.rom` e `dsp4.program.rom`/`dsp4.data.rom`. O APK e a pasta privada de preparação online não forneciam esses arquivos. A prova isolada no moto g52 usando o core ARM64 exato confirmou `loaded=false`, dois erros de firmware e retorno de erro para ambos os títulos. Trocar apenas `bsnes_chip_hle` para HLE também falhou, porque `bsnes_violate_accuracy=disabled` faz o core ignorar essa seleção.

Com as duas opções fixas `bsnes_violate_accuracy=enabled` e `bsnes_chip_hle=HLE`, os dois conteúdos carregaram sem erro de firmware. A prova seguinte executou 120 chamadas do core, com 120 quadros de vídeo 256×224, áudio e estado serializado para cada título. Os controles permaneceram em zero na fixture. Ver [comparação de carga](evidence/load-probes.json) e [120 quadros](evidence/frame-probes.json). Essas provas demonstram a causa da rejeição de carga e o início do núcleo; não validam corrida, jogadores, sincronização, WAN ou saída da NativeActivity.

A contagem acima de dois não é a causa desse erro: Super Mario Kart admite dois e apresentou a mesma dependência DSP. Top Gear 3000 continua limitado aos modos aprovados até quatro. A sala já tinha autorização e transporte respondendo PONG, mas nenhum fluxo local do core e nenhuma READY. Conservar a barreira global: três jogadores exigem dois links/quatro anexos; quatro exigem três links/seis anexos; cinco, quando permitido pelo modo, exige quatro links/oito anexos. PONG não substitui HELLO/PAUSED/READY, ACK/replay ou confirmação do core.

## Correção nativa e preservação do pacote

O runtime R138 altera somente `frontend/drivers/platform_unix.c` da fonte exata correspondente ao runtime instalado `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc`. A correção encerra o lifecycle quando `rarch_main` volta antes do teardown normal e impede uso de looper inválido depois do término da thread. Testes das funções produtivas extraídas reproduziram retorno precoce, destruição dupla e corrida com quit na baseline; a candidata passou os cenários, incluindo 300 corridas. São testes de host com fixtures para Android/JNI/looper, não aceite físico da saída. Ver [teste de lifecycle](evidence/native-lifecycle-tests.json).

Runtime R138: SHA-256 `463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f`, 10.674.272 bytes, alinhamento 16 KiB e capacidade nativa de cinco preservada. [Recibo de compilação](evidence/native-build-candidate.json). O asset final de motores tem SHA-256 `76b3cb61149293200cb8853784e6b6e4023f4e4c33b6b7cfcad8a954e1f9f519`.

A montagem final substitui exclusivamente `lib/arm64-v8a/libstation_retroarch.so` e `assets/station-online/engines.json`. Todos os DEX, Manifest, frontend, cores, wrappers, dependências e mídia permanecem exatos da R137. Os três marcadores PSX/N64/Dolphin não incluem essas duas entradas nos fingerprints; seus 345 payloads e demais mapas foram conferidos contra a R137 e não são renovados. Assim, as quatro identidades independentes PSX/N64/GameCube/Wii permanecem R137. A assinatura e os hashes de todas as entradas foram conferidos no APK final: duas entradas alteradas, 13.858 preservadas. Ver [recibo final](evidence/package-final.json) e [instalação](evidence/instalacao-moto-g52.json).

## Identidades de RetroArch

[engines.json](engines.json) e [runtime-identity.json](runtime-identity.json) são metadados reais da composição R138, copiados sem alteração. Nove descritores mudam runtime e sufixo de ID; SNES também recebe as duas opções HLE. Geolith permanece `launchReady=false`, sem protocolo v3, portanto o novo descritor não libera esse motor. O [manifesto administrativo candidato](incoming-engine-manifest-candidate.json) contém somente as oito engines já marcadas prontas para v3 e inclui o nome da biblioteca de runtime para verificação do kit privado. Não é recibo de aplicação.

| Plataforma/owner | Engine ID R138 |
| --- | --- |
| SNES | `bsnes-mercury-performance-79d7f9de-mt1-mp5-hle-463bdb0b409e` |
| Mega Drive | `clownmdemu-d43c2708-mp5-463bdb0b409e` |
| Neo Geo/Geolith — inativo | `geolith-19402493-mp5-463bdb0b409e` |
| Neo Geo CD | `neocd-b1e04c73-aslr1-v3-463bdb0b409e` |
| Neo Geo/FBNeo | `fbneo-95153da1-neogeo-v3-463bdb0b409e` |
| FBNeo | `fbneo-95153da1-fbneo-v3-463bdb0b409e` |
| CPS1 | `fbneo-95153da1-cps1-v3-463bdb0b409e` |
| CPS2 | `fbneo-95153da1-cps2-v3-463bdb0b409e` |
| CPS3 | `fbneo-95153da1-cps3-v3-463bdb0b409e` |

PSX conserva `pcsx-rearmed-ff81ed17-player-v3-c26d8836df25`; N64, `mupen64plus-ae-33bf7021-n64-v3-2f248cfcdc9e`; GameCube, `dolphin-5102a033-gamecube-v3-2e0b36222097`; Wii, `dolphin-5102a033-wii-v3-2e0b36222097`. Seus cadastros R137 ainda precisam estar confirmados para uso, porém esta revisão não muda suas identidades.

## Procedimento de registro v3

1. Receber o kit privado dos bytes do APK final assinado, seu recibo e o SHA-256 final. Comparar os cores de cada engine e `libstation_retroarch.so` com o manifesto candidato. Manter assinatura original, dados dos telefones, identidades LLE antigas e jogos/catalogação existentes. O Git público contém apenas fontes, metadados e evidência saneada; não contém o kit binário.
2. Preparar contra o catálogo, manifesto, perfis e ledger de modos **atuais** persistidos no servidor. O operador vigente é `docs/station-android/online-plataformas-20261009/library-tools/cadastrar-motor-online-station.py`, ações `prepare` e, após validação administrativa, `apply`/`install`. Usar `autoOnlineProfiles.engineManifest`, `.registry` e `.preparedModes`, lock e rename atômicos. O hot reload existente lê os dados em até dez segundos. O procedimento não pede nova DLL ou reinício; o registro v2 `Station__Online__EngineRegistryFile` não aceita estas identidades v3.
3. Registrar IDs novos imutáveis para as oito engines v3 que dependem de RetroArch. As sete engines não SNES conservam opções, cores e regras de controle/modo anteriores; a identidade muda por runtime. Geolith não entra nessa publicação de perfis online e continua inativo. PSX/N64/Dolphin ficam com seus cadastros R137 exatos.
4. Para SNES, usar a string completa de opções HLE do manifesto, idêntica em todos os participantes. `standard-2p-v1` conserva dispositivos `[1,1]`, configuração SHA-256 `8a79b81e1797bedf4c00058e004af7bbe7a00014d7b6041ef2b2a0c0bd39c793`; `snes-multitap-port2-v1` conserva `[1,257,1,1,1]`, configuração SHA-256 `88568b97800afa017e3c75684effe23f3aad8e93f5b2ac947a0638a4268bd8f3`. O hash canônico usa, nesta ordem, `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`, JSON compacto UTF-8 e nenhum newline final. O [teste com o gerador real](evidence/profile-contract-checks.json) conferiu essas configurações e preservação de perfis LLE.
5. Vincular cada edição pelo `itemId` + `contentSha256` atual ao core/runtime/engine e modo próprios. A engine SNES nova exige `requireExplicitEngineModes=true`; isso evita aprovação genérica por `metadata.players`. Preservar modos/instruções/fontes/contagens já revisados: Super Mario Kart `[2]`; Top Gear 3000 Corrida VS `[2,3,4]`, Campeonato em dupla `[2]`; cada edição PT-BR recebe seu vínculo próprio. Para acrescentar uma variante HLE scoped sem alterar os modos antigos, dar um novo `profileId` à variante: o ledger recusa duplicação de edição/conteúdo/profileId. Não sobrescrever um profileSha LLE com HLE.
6. Comparar o candidato recomputado ao baseline atual e publicar somente os dados revisados pela administração nativa existente. Preservar todos os perfis antigos byte exatos. Devolver recibo com engine/perfis/modos adicionados, hashes finais, motivo de recusas e baseline preservado. Nenhuma publicação produtiva foi executada por esta entrega.
7. Conferir resposta assinada fresca de `POST /v1/station/online/multiplayer/command` para cada edição alvo. Verificar `engineId`, `coreSha256`, `runtimeSha256`, `profileId`, `profileSha256`, controlador, modo, contagens e `onlineAvailable`. Sem os novos perfis HLE, o APK corretamente recusa a sala dessa identidade; não remover essa conferência para tirar o carregamento.
8. Após o mesmo APK/identidade registrada em todos os participantes, testar Super Mario Kart com dois e Top Gear 3000 com dois, três e quatro. Registrar início do core, TCP local, epoch, HELLO/PAUSED/READY, ausência de replay pendente, vídeo/áudio e controles P1–P4; repetir queda/retorno de sinal e saída humana de host/convidado sem crash. Cinco é teste separado para um modo que admita cinco. Instalação, registro e prova isolada de 120 quadros não substituem gameplay em rede.

## Errata de capacidade R136/R137

As notas anteriores atribuíram limite nativo quatro aos APKs atuais com base no header histórico R77. O binário realmente instalado é o runtime `81b3daa3...` da entrega de cinco jogadores de 09/10, cuja fonte/compilação define cinco posições, quatro bindings e máscara 31. Essa capacidade já estava nos pacotes R136/R137; não precisa de nova expansão em R138. As notas R136/R137 foram corrigidas. A capacidade compilada não homologa cinco aparelhos nem aumenta o limite de um título.

Fontes e receitas Android: `TurboElden/integration/online-snes-startup-r138/`, provas saneadas em `TurboElden/audits/station-r138-snes-dsp-startup-20261012/`. As receitas de probe apenas examinam o core; nenhuma ROM/firmware/credencial/identidade pessoal foi adicionada aos repositórios.
