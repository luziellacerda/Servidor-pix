# R161 — entrega privada ao servidor: N64 online com Dynarec

**Estado em 15/10/2026:** candidata assinada, kit privado conferido e APK instalado no A56 com hash integral idêntico. `serverApplied=false`; `physicalGameplayValidated=false`. Não promover como estável nem substituir a R159 em produção antes do cadastro e da partida física. A [resposta R159 do servidor](https://github.com/luziellacerda/Servidor-pix/blob/docs/station-online-exit-r135-20261012/docs/station-android/r159/server-return.json) confirma a aplicação **da R159**, não desta revisão.

O [recibo de instalação do A56](a56-install-receipt.json) registra atualização por cima sem limpar dados, `Success` e SHA-256 do `base.apk` instalado. O processo abriu a `LoginActivity`; login, sala e gameplay ainda não foram confirmados. Não iniciar uma sala R161 até o servidor cadastrar as identidades exatas e os demais aparelhos de teste receberem o mesmo APK.

## Arquivos exatos

| Arquivo privado | Caminho local | SHA-256 |
| --- | --- | --- |
| APK assinado R161 (2.715.122.703 bytes) | `E:\StationN64OwnerAudit-20261014\final-r161\TurboStations-Premium-R161-n64-online-dynarec.apk` | `66f429d47af725a6767064c3623ee10c505171a85fbc8c0a50dc9e5bc4f80081` |
| Kit independente R161 (12.189.409 bytes) | `E:\StationN64OwnerAudit-20261014\kit-r161-dynarec-only\Kit-Servidor-TurboStations-R161-Independentes.zip` | `bcfbe393e6e560cd04f8994de3267fd7ff76487c3e9f234aa5bd20abd7f0eadf` |

Conferir os hashes externos antes de abrir o ZIP. O kit contém `incoming.json`, `engines-apk.json`, `profile-policies.json`, seis ELF, vinte dependências, manifesto com hashes e recibos; não contém APK, ROM, BIOS, chave, serial ou log bruto. A [receita do kit](../../../integration/n64-online-dynarec-r161/README.md) está no repositório; o recibo completo fica ao lado do ZIP, em `server-kit-receipt-r161.json`. A prova do APK fica em `E:\StationN64OwnerAudit-20261014\package-r161-dynarec-only\final-apk-proof.json`. Publicar no Git somente documentação e receitas revisadas, nunca o APK, o ZIP privado, chaves ou dados de aparelhos.

O [recibo de entrega USB](usb-delivery-receipt.json) confirma cópia e leitura de volta dos hashes do APK e do ZIP em `I:\SERVIDOR LER\R161-N64-ONLINE-DYNAREC-20261015`. A letra I: é apenas a identificação observada neste PC. O pen drive foi preparado para o servidor, mas ainda não há recibo de recebimento ou aplicação pelo servidor.

## Mudança delimitada

A R159 usava `r4300Emulator=1` (interpretador em cache) nas sessões N64 online. A R161 usa `r4300Emulator=2` (Dynarec) no cliente e o aceita no servidor N64 **local do telefone anfitrião**. O registro N64 passa a declarar `cpu=dynarec`. O núcleo `libstation_mupen64plus_core.so`, o vídeo Glide64mk2, RSP HLE, `inputDelay=2`, transporte, saída, ciclo de vida dos serviços, emulação offline e regras de jogos não foram alterados. Portanto a mudança de CPU é uma hipótese técnica delimitada para a lentidão online observada; ela não comprova por si só que a espera de entradas pela rede ou a tela preta cessaram.

No APK assinado, apenas cinco de 13.860 entradas mudaram: `classes35.dex`, `assets/station-online/engines.json` e os marcadores de runtime PSX, N64 e Dolphin. Dentro de `classes35.dex`, somente `StationN64Bridge$Session`, `StationN64Launch` e `StationN64Server` mudaram; as outras 605 classes foram comparadas sem diferença. Os outros 39 DEX, manifesto, versão Android, certificado original (SHA-256 `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`), assinatura v2/v3 e alinhamento de 16 KiB foram preservados ou conferidos. Os marcadores PSX e Dolphin receberam identidades novas porque são vinculados ao DEX global; suas opções, núcleos e aprovações permanecem iguais.

## Identidades a cadastrar a partir deste kit

| Sistema | `engineId` R161 | `runtimeSha256` R161 |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-3c5f696fa30e` | `3c5f696fa30ee9a7760b3925acf49693456a5a988f6362062658a3604e4a1939` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-656d6d7c017f` | `656d6d7c017f17b55c3da06a5ee5ed11a60072f1b159790c78c4a8ab4606c9e3` |
| GameCube | `dolphin-5102a033-gamecube-v3-8b181081428b` | `8b181081428b9012eb8bcb291010018d1b0c0d3217e5e555f6db306b582b5256` |
| Wii | `dolphin-5102a033-wii-v3-8b181081428b` | `8b181081428b9012eb8bcb291010018d1b0c0d3217e5e555f6db306b582b5256` |

O núcleo N64 permanece `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8`. O perfil N64 `direct-four-ports-v1` tem `maximumPlayers=4` e SHA-256 canônico do Android **`ef8bfd6e6287a205da4f73422a194c7fc05b9246363a8311501a6efa3bca2b63`**. Suas opções estão em `incoming.json`; a diferença intencional para R159 é somente `cpu=cached-interpreter` → `cpu=dynarec`, mantendo `wire=SN64/1`, Glide64mk2 e `inputDelay=2`. A serialização canônica Android escapa a barra em `SN64/1` como `SN64\/1`; não recalcular `profileSha256` com a representação padrão de JSON do Python. A [prova da correção R157](../r157/server-fingerprint-fix/README.md) documenta esse requisito. Os fingerprints de perfil PSX, GameCube e Wii do kit são preservados das políticas aprovadas; as novas identidades de runtime ainda precisam de vínculo exato com elas.

## Aplicação pelo operador vigente

1. Ler uma baseline **fresca** do catálogo, motores, perfis e modos, junto dos recibos já aplicados da R157 e R159. A R159 já está cadastrada; não repetir sua importação. Não usar o helper histórico R128 para gerar o hash do perfil N64.
2. Conferir SHA-256 do ZIP e do APK assinado, os 38 arquivos manifestados, `incoming.json`, `engines-apk.json`, `profile-policies.json`, os seis ELF e as vinte dependências contra os bytes reais do APK. Recusar misturas de arquivos R159 e R161 ou divergência de `engineId`, core, runtime, opções e perfil.
3. Pelo registrador vigente, sob `scan.lock`, backup fresco e rollback do operador, adicionar as quatro identidades R161 e vincular os **78 modos revisados** da primeira transação sem alterar aprovações PSX HLE ou modos de outros donos. Registrar o recibo, ler outra baseline fresca e então vincular ao novo N64 as **321 decisões** revisadas das **157 edições** da [revisão completa](../n64-completo-20261014/README.md). Essa revisão não foi duplicada dentro do ZIP. Manter as 37 edições individuais sem vaga online e `allowedPlayerCounts` por modo. Os totais efetivos devem vir dos recibos de publicação.
4. Confirmar capabilities autenticadas e assinadas para as tuplas exatas `itemId`/conteúdo/`engineId`/core/runtime/`profileId`/`profileSha256`; provar recusas de identidade, edição, modo e número de jogadores incompatíveis. Verificar criação, entrada, READY, transporte público e recuperação para clientes de prova, preservando licenças reais e removendo somente as licenças sintéticas do operador.
5. Publicar um retorno R161 com hashes aceitos, baseline, transações, contagens, testes e `serverApplied` real. Testes sintéticos de transporte e capabilities não equivalem a partida física. Não reiniciar API, alterar DLL, scanner, catálogo, relay ou regras de replay sem evidência separada.

## Validação física e retorno

Após a confirmação do servidor, instalar **o mesmo APK assinado** em dois aparelhos de teste, preservando dados, e comparar o SHA-256 dos `base.apk` instalados. Não misturar R159 e R161 na sala. Com a mesma edição de N64, testar anfitrião e convidado: abertura até o primeiro quadro, imagem, áudio, fluidez, controles independentes, espera de entradas, reconexão e saída. Registrar também o comportamento offline como referência. Se ainda houver travamento, correlacionar VI/FPS e tempos de entrada no telefone com RTT/jitter e eventos de transporte; `inputDelay=2` permaneceu igual nesta revisão. Não chamar a R161 de estável até gameplay Android real sem regressão.

Se a publicação ou a partida falhar, usar o backup/rollback do operador vigente para desfazer **somente** os vínculos R161 incompletos, mantendo R159 e os demais donos. Reinstalar o APK R159 exato nos aparelhos de retorno e usar suas identidades já cadastradas. O [handoff R159](../r159/N64-OWNER-HANDOFF.md) e seu retorno do servidor preservam os hashes e o estado anterior. Não usar o candidato R160 de remoção de tarefa como parte deste reparo; ele foi rejeitado na revisão de escopo.
