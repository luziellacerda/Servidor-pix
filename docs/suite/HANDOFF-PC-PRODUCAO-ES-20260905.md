# Handoff para o PC Windows de produção — EmulationStation Suite 1.1.2

Solicitado pelo operador em 05/09/2026. Este documento orienta a execução no PC
Windows; a matriz de entrega continua no
[handoff principal](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md#04-matriz-unica-de-execucao).

## Objetivo e estado do servidor

Validar o EmulationStation no PC que já possui a Suite ativada, na mesma conta
Windows, com sessões separadas. A Suite não precisa estar aberta; sua ativação
e a chave CNG existentes continuam obrigatórias.

**Candidato atual do cliente: 1.1.2**, commit
`187b72686888580e75c5edcef52f5c031601cf5b`,
[compilação GitHub 33994557511](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33994557511).
**CI concluída com sucesso.** Testes de contrato, encerramento, cancelamento,
preservação, build x64 e empacotamento passaram. Os hashes estão na seção abaixo.
Cancelar o login deve sair silenciosamente, sem pedir para abrir a Suite. O
helper tem encerramento limitado e o ícone existente foi integrado. A mensagem
de perda de autorização durante o uso também não pede mais para abrir a Suite.
A CI anterior `33994292849` foi substituída por este último ajuste; seu
cancelamento automático não é falha da compilação atual. A release geral não
foi publicada: o pacote continua candidato até o teste real com o servidor atualizado.

**Correção posterior, publicada no Git mas ainda não implantada (05/09/2026):** após o primeiro
acesso, a reabertura imediata encontrou a sessão anterior ainda vigente. O usuário
determinou seguir a Suite: nova abertura validada substitui somente a sessão ES
da mesma licença/dispositivo, sem pedir encerramento no painel. O código publicado
retira a trava de ocupação preservando prova CNG, SHARED_V1, assinatura, TTL e
isolamento. Não há nova ativação, ação `session.close` ou migration. O commit é
`efaf1d3cd3dfd2a807e9d5a0e7295328ff081c4a`,
[CI da integração 33994510188](https://github.com/luziellacerda/Servidor-pix/actions/runs/33994510188).
**CI concluída com sucesso:** protocolo, HTTP/PostgreSQL, banco original da Suite,
painel Chromium e carga 500/1000 passaram. Artifact servidor `9977821782`,
SHA-256 do arquivo completo
`ab017ad8313fc0c50e702c4d6aa7ae7f8348276376a8ea850f04a19ac0c1cf86`.
A implantação desta correção e o teste Windows real continuam pendentes. O servidor `34e31f2`
e os hashes abaixo são o estado anterior confirmado,
não evidência de implantação dessa correção. Não repetir download grande apenas
para tentar mudar uma política que ainda está no servidor antigo.

**Registro anterior: servidor implantado e verificado em 05/09/2026 às 18:23:58
(America/Maceio, UTC−3), com orientação de testar o cliente 1.1.1.**
API Suite, backend administrativo e PIX/painel executam o commit
`34e31f26b6a864a7aa5d701b94fe29ad166e86ac`. Migrations 022–025 aplicadas; integração
ES/rede habilitada. Provas reais de abertura, heartbeat, conflito, revogação
isolada e coexistência passaram em loopback e no endereço público. Esses testes
usaram identidades sintéticas; a chave CNG deste PC ainda precisa ser homologada.
O pacote `cbdcda9` foi substituído. As correções WhatsApp já usadas em produção
estão preservadas no commit implantado.

Destino autorizado: `https://app.lzgames.com.br/`.
[Abrir painel administrativo existente](https://painelpix.lzgames.com.br/admin).
O operador relatou no PC a mensagem **“O servidor não retornou uma confirmação
válida para o EmulationStation. A integração precisa ser verificada; isso não
significa que sua licença foi desativada.”** O código `ServerUnconfirmed` da tela
1.1.1 cobre resposta/assinatura/contrato não confirmados; o texto não atesta
revogação da licença. Na observação inicial, a API ainda executava o release
anterior às rotas compartilhadas. A implantação agora está confirmada; repetir
o teste sem refazer a ativação. A causa no PC só pode ser confirmada pelo retorno.

CI histórica do servidor implantado: https://github.com/luziellacerda/Servidor-pix/actions/runs/33989933344

## Pacote exato para o PC

- Repositório: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama
- Branch: `CLIENTE-SUITE-ATIVADO-v1.0.0-20260905`.
- Commit: `187b72686888580e75c5edcef52f5c031601cf5b`.
- Versão do cliente: **1.1.2**, edição Suite.
- Build Windows aprovado: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33994557511
- Artifact: `es-suite-candidate-187b72686888580e75c5edcef52f5c031601cf5b`, ID `9977794150`.
- Download autenticado: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33994557511/artifacts/9977794150
- Artifact completo: `2535078185` bytes; SHA-256 informado pela API GitHub:
  `e1511f51198a704623127b44fa9d2734b2092a1639df88f975bc74c79876c031`.

O artifact contém o EXE, o pacote portátil e o pacote de atualização. Para uma
instalação ES existente, usar `Turborama-ES-Suite-v1.1.2-Atualizacao.zip`; ele contém
somente `emulationstation.exe` e `LEIA-ME-SUITE.md`.

| Arquivo | SHA-256 |
| --- | --- |
| `emulationstation.exe` | `9feebb133fbf81ce9fe55e3bce7b4408ab7e1ca39958287fef62b106af7bbbab` |
| `TurboramaEmulationStation-Suite-v1.1.2-Windows-x64.zip` | `98e93ab5a22027e3c939243b337cc1f18fedf66436559b80f8fee3c50feebf02` |

Os hashes identificam os arquivos internos do artifact, depois de extraí-lo.
O hash do ZIP de atualização não foi impresso na CI; não confundi-lo com o do
ZIP completo. Conferir o EXE extraído pelo hash acima antes de executá-lo. Nenhum
novo binário foi baixado, instalado ou executado no PC nesta verificação. A
release geral depende do resultado no PC real e da implantação do servidor.

O operador usava o 1.1.1, commit `ada4555`, hash do EXE
`43dbd0402274d32da1dfd4ea6ffb60432b4d9fef07ccf9902ca9f498b4df33ee`.
Esse binário anterior não contém as correções de cancelamento/encerramento/ícone
do 1.1.2. Preservá-lo como rollback, sem atribuir a ele os testes novos.

### Avisos de compilação, sem ocultação

A CI aprovada não é uma compilação sem warnings. O trecho enviado pelo operador
contém 70 ocorrências em 22 posições de 6 arquivos, todos idênticos à base
`5a356172013a620a1a0ecf151c00c9238ea21a24`. Inclui conversões numéricas em vetores,
renderer/animação, índices de grade/lista e informações de espaço em disco.
Esses avisos não demonstram regressão de RAM, som ou ativação. O log completo tem
outros avisos; as 11 annotations visíveis no GitHub não representam seu total.
Os warnings não foram desativados nem removidos com casts em massa.

Análise pontual encontrou questões preexistentes para revisão separada: precedência
do teste de flag em `external/id3v2lib/src/header.c:78`; aritmética signed/unsigned
em `ImageGridComponent.h:258`; limite do formatter `FileSystemUtil.cpp:1539–1550`
para volumes extremos. Não foram corrigidas nesta entrega nem atribuídas ao
problema de ativação. Também permanece o aviso de atualização de Node das Actions.

## Execução no Windows

1. Identificar a pasta realmente usada pelo EmulationStation e a conta Windows
   em que a Suite já funciona. Registrar versão e caminho do EXE anterior.
   Se houver código local de interface ainda não publicado, preservar esse
   trabalho; não sobrescrever o checkout para instalar o binário.
2. Usar a mesma conta Windows já ativada. A Suite pode permanecer fechada:
   isso deve fazer parte do teste de independência do ES. Usar o TS já ativado
   se solicitado; não gerar outro TS, outra ativação ou chave CNG. Abrir a Suite
   em outro teste apenas para conferir coexistência, não como pré-requisito.
3. Fechar o EmulationStation anterior. Copiar seu EXE e sua configuração para uma
   pasta de backup com data. Preservar ROMs, temas, jogos, áudio e dados da Suite.
   Não exportar a chave CNG nem copiar material de ativação para o relatório.
4. Baixar o artifact e extrair o ZIP de atualização em uma pasta temporária.
   Conferir os hashes antes de copiar o EXE para a pasta identificada no passo 1.
   Exemplo em PowerShell, executado na pasta de download:

   ```powershell
   Expand-Archive -LiteralPath '.\Turborama-ES-Suite-v1.1.2-Atualizacao.zip' -DestinationPath '.\ES-1.1.2-conferencia'
   Get-FileHash -Algorithm SHA256 -LiteralPath '.\ES-1.1.2-conferencia\emulationstation.exe'
   ```

5. Depois de confirmar a implantação do servidor `efaf1d3` ou posterior revisado,
   abrir o novo EXE normalmente.
   Confirmar o acesso pelo vínculo existente. Indisponibilidade, conflito ou
   rejeição de contrato devem ser registrados; não contornar TLS, assinatura,
   expiração online ou exigência da chave já ativada.
6. Executar os testes abaixo e registrar o resultado na mesma entrega.

## Testes e resultados esperados

| Teste | Resultado esperado |
| --- | --- |
| Abrir ES 1.1.2 com a Suite fechada | A ativação CNG existente valida diretamente no servidor, sem pedir para executar a Suite. |
| Cancelar o login pelo X ou Sair | Fecha sem mensagem secundária de ativação e sem helper órfão; ícone existente visível na janela. |
| Abrir Suite e ES 1.1.2 na mesma conta Windows | Ambas permanecem autorizadas; a abertura do ES preserva a sessão Suite. |
| Manter ambas abertas por pelo menos dois minutos | Heartbeats seguem funcionando; o painel mostra sessões separadas e contato recente. |
| Conferir `/admin`, cliente selecionado e `/admin/suite` no painel existente | As linhas indicam a aplicação correta; filtros, paginação, detalhes e confirmação continuam utilizáveis. |
| Conferir telemetria de rede do ES | O painel autorizado recebe interfaces reais e IP mascarados após a coleta; não há alteração de fingerprint ou novo vínculo. |
| Abrir e retornar de jogos, navegar pelo tema e reproduzir áudio | Jogos, áudio e navegação continuam funcionando; registrar memória antes e depois de ciclos de abertura/retorno. |
| Depois de confirmar a implantação da correção, fechar e reabrir ES imediatamente | Nova prova com a chave já ativada abre outra sessão sem esperar o TTL anterior nem usar o painel; a sessão antiga não renova. Suite e outro cliente permanecem autorizados. Não remover a trava local de instância para forçar o teste. No `34e31f2` anterior ainda é esperado conflito por ocupação. |
| Encerrar somente a sessão ES de teste pelo painel | O administrador confirma o alvo e digita sua senha no próprio painel; somente o ES selecionado perde acesso. Suite e outras licenças permanecem funcionando. |
| Reabrir ES após o encerramento confirmado | O mesmo vínculo e a mesma chave existente abrem uma nova sessão autorizada. |
| Conferir ES 1.0.1 legado, se instalado para compatibilidade | Rotas dedicadas continuam disponíveis. O legado conserva sua política anterior de substituição de sessão ES. |

A perda de rede e a mudança de interface podem ser verificadas em uma janela de
teste adequada ao PC. A autorização deve expirar sem acesso offline, e uma mudança
isolada de IP/MAC não deve revogar o vínculo nem pedir reativação. Registrar esses
casos como não executados quando não forem testados; não declarar aprovação por
suposição.

## Retorno esperado e reversão no PC

Devolver versão do Windows, horário com fuso, caminho do EXE, versão/hash
conferidos, resultados por teste e eventual código de erro. Usar identificação
mascarada da licença e imagens sem senhas, tokens, TS completo, dados de rede
brutos ou material CNG. Informar se houve mudanças de áudio, memória, jogos ou
interface. Os testes automatizados do servidor não substituem esta execução.

Em caso de falha, fechar o novo ES e restaurar o EXE e a configuração copiados no
passo 3. Preservar a ativação existente da Suite. Registrar a falha antes de qualquer
alteração de código; correções devem ter commit, testes e novo artifact identificados.

A homologação conjunta e a publicação geral só podem ser marcadas como concluídas
depois de receber e avaliar esse retorno do PC Windows.
