# R165 urgente: motor não cadastrado no A56 e modos ausentes

**Data:** 15/10/2026. **Estado:** solicitação ao operador; nenhum cadastro R165 nem implantação de servidor é afirmado por este documento.

## Incidente observado

O Galaxy A56 recebeu o APK R165 combinado com instalação preservando dados. O produtor conferiu o SHA-256 do APK instalado contra o arquivo assinado. Ao tentar jogar online, o usuário relatou que o app reclama de motor não cadastrado e de ausência de modos aprovados. Este é um relato físico da interface, não uma captura de resposta assinada, e não prova defeito do core nem gameplay Android.

O retorno PSX publicado no commit [`3f55caa`](https://github.com/luziellacerda/Servidor-pix/commit/3f55caa582255ad87b508e95d24880d775bb9fe7) informa `engineCount=79` e `currentEngineId=pcsx-rearmed-ff81ed17-player-v3-1b315b86b4a7` (R164). Seus 23 modos PSX aprovados em 19 edições pertencem a essa identidade. O APK R165 assinado anuncia outro `engineId` e outro `runtimeSha256`. Não há recibo de aplicação dos quatro owners R165 no Git remoto nesta data. O erro do A56 é coerente com a falta do cadastro e do vínculo dos modos para os IDs novos; a resposta pública exata ainda deve ser registrada pelo operador.

## Entrega já disponível, sem APK no Git

- [Handoff do produtor com os seis arquivos alterados, prova de assinatura e exigências](https://github.com/luziellacerda/TurboElden/blob/build/station-r165-dex-recipe-20261015/docs/server/r165/R165-APK-ASSINADO-REGISTRO-PENDENTE-20261015.md).
- [Kit independente no repositório privado, commit `bbe57e5`](https://github.com/luziellacerda/TurboramaEmulationStation/commit/bbe57e5b8761f60a4e264ac97fe410e5f3a712b5), ramo `handoff/station-r165-combined-20261015`, pasta `station-server-handoffs/r165/`.
- ZIP privado do kit: SHA-256 `2e260ba80f93a0a9f65c1e2dca2f0632ce29b321dd895ad84d737e5a5628179b`, 12.985.386 bytes, 43 arquivos. Conferir o ZIP, `MANIFESTO-ARQUIVOS.json`, `incoming.json`, `engines-apk.json` e provas antes de registrar.
- APK assinado instalado no A56: SHA-256 `9e7ab2441e9d75568d5d2b741b4a0ed42e9a2d9f823698c0f39dccdcb7c19145`. **Não solicitar, copiar ou publicar o APK no Git.** O servidor não precisa dele para este cadastro.

| Plataforma | `engineId` R165 exato | `runtimeSha256` exato |
| --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-0a01a6ef6e99` | `0a01a6ef6e99f0bb9b62f04b7cc17642e11f0aef83b691a8c308fc1722952e4b` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-a74636835393` | `a746368353933cb7786897bf5a3f57635bd8456711878f3c3cf72872dbbc9772` |
| GameCube | `dolphin-5102a033-gamecube-v3-85dda51eaeda` | `85dda51eaeda8e5b2bcc1f3ee4ed560390ad1705037c0151818a028c5738474a` |
| Wii | `dolphin-5102a033-wii-v3-85dda51eaeda` | `85dda51eaeda8e5b2bcc1f3ee4ed560390ad1705037c0151818a028c5738474a` |

Os cores permaneceram os mesmos da R164: PSX `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9`; N64 `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8`; Dolphin `3438ce727ab3a0d6f6fb4aa2a1b8068be5d17360e6c7aa0b8ff3594dbfde7c29`. A identidade de runtime mudou no pacote R165 e deve ser tratada literalmente.

## Ação necessária do servidor

1. Receber o kit **já entregue no Git privado**, conferir ZIP/entradas/hashes e assinatura do produtor. Ler a baseline protegida **atual**, `scan.lock` e recibos de R164/PSX. O total 79 é o retrato do retorno PSX, não uma baseline a fixar se o servidor já avançou.
2. Pelo registrador vigente, cadastrar **aditivamente** os quatro owners R165 exatos. Preservar todos os owners, perfis, modos, catálogo, capas, downloads, licenças e aprovações R164 anteriores. Não reaplicar operadores históricos, alterar DLL por consequência nem reiniciar a API sem necessidade comprovada.
3. Qualificar o vínculo dos **23 modos PSX de duas portas**, das 19 edições aprovadas, para o owner PSX R165 pela identidade exata de edição/conteúdo, core, runtime, perfil de controle e capacidade. O perfil publicado R164 é `standard-2p-v1`, SHA-256 `81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a`. Comparar com a prova R165 recebida; **não copiar aprovações por nome de jogo nem liberar automaticamente os 17 modos pendentes ou 53 classificações locais**. Registrar decisões admitidas e recusadas com motivo.
4. Qualificar N64 para a identidade R165 usando as decisões por edição e proveniência do kit; conferir `cpu=dynarec`, `video=glide64mk2`, `inputDelay=2`. Preservar a política vigente de GameCube e Wii; registrar apenas modos comprovados por owner exato.
5. Resolver separadamente a saída do anfitrião N64: o [handoff do conserto `220bc64`](https://github.com/luziellacerda/Servidor-pix/blob/fix/station-r164-confirmed-host-exit/docs/station-android/r164/HOST-EXIT-HANDOFF.md) está preparado, mas não possui recibo de implantação. Compilar, implantar e provar pelo fluxo próprio se autorizado; devolver estado explícito, hash do binário e resultado. A mudança do DEX35 do app não substitui essa parte.
6. Verificar localmente **e pela consulta pública assinada** que os quatro IDs R165 são reconhecidos e que o PSX R165 oferece somente os modos qualificados; testar admissão de sala para uma edição PSX de dois participantes aprovada e recusas pertinentes. Testes sintéticos de TLS/sala não são gameplay em dois Android.
7. Devolver um recibo novo com horário, baseline/contagens anterior e posterior, quatro IDs e hashes, kit verificado, quantidade de modos PSX/N64 admitidos e recusados, prova da consulta pública, PID/reinício se ocorrer, estado do host-exit e pendências. Somente depois disso o produtor deve repetir o teste físico com dois telefones na **mesma R165 e mesma edição aprovada**, observando primeiro quadro, áudio, controles independentes e saída do anfitrião.

Não remontar nem reassinar a R165 por este incidente. A R164 continua sendo o retorno estável até prova física da R165. Este documento registra o bloqueio e o trabalho pendente, não promove aprovação de gameplay.
