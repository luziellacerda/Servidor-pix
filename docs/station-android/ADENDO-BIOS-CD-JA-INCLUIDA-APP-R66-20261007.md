# APP → SERVIDOR: a BIOS CD já estava no APK; preparação corrigida na R66

07/10/2026. Este adendo corrige a interpretação da dependência de BIOS no pedido [NG-01–NG-07](PEDIDO-AUDITORIA-NEOGEO-CARTUCHO-CD-R65-20261007.md). Não é retorno do servidor nem implantação Linux.

## Evidência e causa

Os três assets abaixo **já estavam no APK R65 e no telefone**, com identidades válidas. O lançador R65 não lia os assets; procurava apenas a pasta privada de importação e o arquivo `neocdz.zip` ao lado do disco. Essa omissão do cliente causava a mensagem de BIOS ausente.

| Asset do APK | SHA-1 também conferido no telefone |
|---|---|
| `assets/bios/neocd/neocd.bin` | `7bb26d1e5d1e930515219cb18bcde5b7b23e2eda` |
| `assets/bios/neocd/000-lo.lo` | `5992277debadeb64d1c1c64b0a92d9293eaf7e4a` |
| `assets/bios/neocd/uni-bioscd.rom` | `5142f205912869b673a71480c5828b1eaed782a8` |

No telefone estavam em `EmulationStation/.emulationstation/bios/neocd/`. Não era necessário obter firmware adicional nem pedir ao usuário que importasse um arquivo que o app já possuía. Nenhum firmware foi baixado, acrescentado ao APK ou publicado no Git nesta correção.

## Cliente que deve orientar a próxima análise

- TurboElden, branch `fix/station-neogeocd-bundled-bios-r66-20261007`.
- Fonte exata **`291f3949760c0d77030e4872520efc3e1c8f928b`**.
- [Relatório completo e reprodução](https://github.com/luziellacerda/TurboElden/blob/291f3949760c0d77030e4872520efc3e1c8f928b/versions/station-neogeocd-bundled-bios-r66-20261007/README.md).
- APK R66 SHA **`e4397fd743c410ea060f17446706d9475b5568caad379e1a6df37672c60398d1`**, 2.093.413.965 bytes.
- Base R65 SHA `1858459b62a38a85cdd9fbeaafc68d3560008f154f99dd1431752aa0594eb081`.
- Apenas `classes30.dex` alterado; SHA **`1dcfa1e69686a326e22c54b9adc7c6013644e69d14642664e2ab7b5e2eee4ff4`**. Todas as 13.218 outras entradas preservadas/conferidas, incluindo BIOS, motores, dados do layout e DEX de salas.
- 66 verificações locais passaram, das quais29 usando os assets reais do APK com disco sintético para testar a preparação; DEX reproduzido idêntico. Esses testes não são gameplay.
- **R66 instalada no Samsung A56**, SHA integral lido no Android e igual ao candidato. UID/data original preservados, sem desinstalação, limpeza ou partida interrompida. Atualização reportada pelo Android:07/10/2026 às12:39:43. Outro telefone não atualizado nesta etapa.
- A tela bloqueada impediu validar abertura do jogo após instalar. Conferência física solicitada ao mantenedor; não declarar gameplay CD ou estabilidade geral.

## Alteração funcional

`MameEntryActivity` fornece `getAssets().open` a `NeoCdSupport.prepare` no worker. O helper preserva primeiro BIOS válida já importada ou presente no driver local. Para dependências ausentes, lê os assets conhecidos, valida tamanho/CRC/SHA-1 e grava atomicamente na pasta privada. Com isso, uma instalação nova funciona sem cópia manual de arquivos. A segunda abertura com dependências válidas não relê nem regrava os assets.

O driver CDZ, o disco CHD e o caminho real da instalação continuam os da R65. Os controles/configurações nativos permanecem. Cartuchos, KOF, bibliotecas de motores e protocolo/timers de salas não mudaram.

## Ajuste das pendências do servidor

- Na NG-05, **não adicionar entrega de BIOS ao artefato como solução para este defeito**: a origem necessária já existe no aplicativo e agora é usada automaticamente.
- Ainda conferir integridade dos CHDs e estrutura/launchPath dos artefatos realmente ativos. Formatos além do CHD v5 autônomo não foram implementados por suposição.
- NG-01–NG-04 e NG-06–NG-07 continuam válidas:13nomes não registrados, seis da coleção KOF, dependências/CRC e classificação dos itens. KOF98 padrão permanece sem causa gráfica comprovada.
- O pedido Q01–Q08 de recuperação da conexão permanece independente. R66 não altera runtime, motores/IDs online, contrato ou timeouts.

Responder citando esta fonte atual e separando teste local, estado de produção e execução Android. Não restaurar a R65/Activity antiga para integrar outro retorno; conciliar a leitura de assets da R66. Sem mudança de outros produtos ou implantação Linux por consequência deste adendo.
