# Handoff para o PC Windows de produção — EmulationStation Suite 1.1.1

Solicitado pelo operador em 05/09/2026. Este documento orienta a execução no PC
Windows; a matriz de entrega continua no
[handoff principal](HANDOFF-EMULATIONSTATION-ROTAS-COMPARTILHADAS-20260905.md#04-matriz-unica-de-execucao).

## Objetivo e estado do servidor

Validar o EmulationStation no PC que já possui a Suite ativada, na mesma conta
Windows, com sessões separadas. A Suite não precisa estar aberta; sua ativação
e a chave CNG existentes continuam obrigatórias.

**Candidato atual do cliente: 1.1.2**, commit
`efa3ae9e169f9c03f56b6f4eb0b773f8d856e215`,
[compilação GitHub 33994292849](https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33994292849).
Os testes locais de contrato, encerramento, cancelamento e preservação passaram;
a compilação completa e os hashes do novo pacote ainda precisam ser confirmados.
Cancelar o login deve sair silenciosamente, sem pedir para abrir a Suite. O
helper tem encerramento limitado e o ícone existente foi integrado. Não publicar
como homologado nem substituir o EXE instalado antes de conferir o resultado.

**Correção local posterior, ainda não implantada (05/09/2026):** após o primeiro
acesso, a reabertura imediata encontrou a sessão anterior ainda vigente. O usuário
determinou seguir a Suite: nova abertura validada substitui somente a sessão ES
da mesma licença/dispositivo, sem pedir encerramento no painel. O código local
retira a trava de ocupação preservando prova CNG, SHARED_V1, assinatura, TTL e
isolamento. Não há nova ativação, ação `session.close` ou migration. Build/testes
locais de contrato passaram; HTTP/PostgreSQL, CI e este teste real ainda estão
pendentes. O servidor `34e31f2` e os hashes abaixo são o estado anterior confirmado,
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

CI final do servidor: https://github.com/luziellacerda/Servidor-pix/actions/runs/33989933344

## Pacote exato para o PC

- Repositório: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama
- Branch: `CLIENTE-SUITE-ATIVADO-v1.0.0-20260905`.
- Commit: `ada45558611bdd98ca0a5ed9053fdd97ff85a067`.
- Versão do cliente: **1.1.1**, edição Suite.
- Build Windows aprovado: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33989089244
- Artifact: `es-suite-candidate-ada45558611bdd98ca0a5ed9053fdd97ff85a067`, ID `9976171533`.
- Download autenticado: https://github.com/luziellacerda/Backup-Instaladores-Compiladores-Turborama/actions/runs/33989089244/artifacts/9976171533

O artifact contém o EXE, o pacote portátil e o pacote de atualização. Para uma
instalação ES existente, usar `Turborama-ES-Suite-v1.1.1-Atualizacao.zip`; ele contém
somente `emulationstation.exe` e `LEIA-ME-SUITE.md`.

| Arquivo | SHA-256 |
| --- | --- |
| `Turborama-ES-Suite-v1.1.1-Atualizacao.zip` | `b1621e3e467650a1d808ea09003025d97672a6fa7b76e61a59a5014fc674bc00` |
| `emulationstation.exe` | `43dbd0402274d32da1dfd4ea6ffb60432b4d9fef07ccf9902ca9f498b4df33ee` |
| `TurboramaEmulationStation-Suite-v1.1.1-Windows-x64.zip` | `259be5f6545e49428ce6f6fb4465f5bc060c5ff24f97cb37102233d5c28d13da` |

Os hashes identificam os arquivos internos do artifact, depois de extraí-lo.
O artifact completo tem aproximadamente 2,5 GB; a atualização interna tem
831.995.160 bytes. O pacote é candidato para esta homologação. A release geral
depende do resultado no PC real.

O operador já está usando o 1.1.1. Se o hash do EXE instalado coincidir com o
acima, manter esse binário e passar aos testes com o servidor já confirmado.
A mensagem relatada está registrada neste handoff para repetir exatamente o caso.

## Execução no Windows

1. Identificar a pasta realmente usada pelo EmulationStation e a conta Windows
   em que a Suite já funciona. Registrar versão e caminho do EXE anterior.
   Se houver código local de interface ainda não publicado, preservar esse
   trabalho; não sobrescrever o checkout para instalar o binário.
2. Abrir a Suite normalmente e confirmar que a ativação existente funciona.
   Manter a mesma conta Windows durante todo o teste. Usar o TS já ativado se o
   cliente o solicitar; não gerar outro TS, outra ativação ou chave CNG.
3. Fechar o EmulationStation anterior. Copiar seu EXE e sua configuração para uma
   pasta de backup com data. Preservar ROMs, temas, jogos, áudio e dados da Suite.
   Não exportar a chave CNG nem copiar material de ativação para o relatório.
4. Baixar o artifact e extrair o ZIP de atualização em uma pasta temporária.
   Conferir os hashes antes de copiar o EXE para a pasta identificada no passo 1.
   Exemplo em PowerShell, executado na pasta de download:

   ```powershell
   Get-FileHash -Algorithm SHA256 -LiteralPath '.\Turborama-ES-Suite-v1.1.1-Atualizacao.zip'
   Expand-Archive -LiteralPath '.\Turborama-ES-Suite-v1.1.1-Atualizacao.zip' -DestinationPath '.\ES-1.1.1-conferencia'
   Get-FileHash -Algorithm SHA256 -LiteralPath '.\ES-1.1.1-conferencia\emulationstation.exe'
   ```

5. Com o servidor confirmado neste documento, abrir o novo EXE normalmente.
   Confirmar o acesso pelo vínculo existente. Indisponibilidade, conflito ou
   rejeição de contrato devem ser registrados; não contornar TLS, assinatura,
   expiração online ou exigência da chave já ativada.
6. Executar os testes abaixo e registrar o resultado na mesma entrega.

## Testes e resultados esperados

| Teste | Resultado esperado |
| --- | --- |
| Abrir Suite e ES 1.1.1 na mesma conta Windows | Ambas permanecem autorizadas; a abertura do ES preserva a sessão Suite. |
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
