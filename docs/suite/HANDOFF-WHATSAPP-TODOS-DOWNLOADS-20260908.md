# Handoff — WhatsApp para todos os downloads concluídos

Data: 08/09/2026, UTC−3.
Destino: responsáveis pelo servidor Linux e pelo programa Windows TurboRama Suite.
Estado atual: **servidor ampliado, instalado e com processamento automático ativo**.
O retorno executado ao final deste documento prevalece sobre o diagnóstico e o
estado histórico abaixo. A integração do EXE Windows para arquivos sem extração
continua pendente: seu código local mais recente não está no Git consultado.

## Pedido vigente do proprietário

**Todo download que concluir com sucesso deve gerar uma notificação automática
para o proprietário correto da compra, em todas as categorias e formatos,
tenha ocorrido descompactação ou não.**

O usuário pediu expressamente este handoff após informar que um jogo de Xbox foi
baixado sem mensagem. Este requisito substitui a limitação anterior a
`Extracting → Completed` e a regra antiga de que arquivo bruto não gera aviso.
O fechamento anterior fica como histórico da implantação e das evidências.

**A autorização geral dos disparos já foi concedida na conversa. Não pedir nova
autorização por arquivo, categoria, download ou destinatário legitimamente
resolvido pelo servidor.** Continuam obrigatórias compra paga, usuário ativo,
proprietário único, telefone válido e política de bloqueio. A autorização não
permite inventar destinatário, ignorar bloqueios ou enviar aviso de operação
incompleta.

## Diagnóstico que motivou a mudança

Na mesma conta do teste 3DS, foi encontrado **Halo 3 (Brazil)** em formato `.iso`,
com acesso autorizado às **16:02:25 UTC−3**. O catálogo apresenta
`visual_extract_policy=NONE` e `extract_policy=NONE`. Até a conferência das
16:12:21, havia **zero eventos de Xbox/Halo 3 e zero jobs correspondentes**.

O protocolo já aceita as categorias Xbox. O contexto de notificação desse item
está disponível e API/Admin/túnel estão ativos. O fluxo documentado do cliente,
porém, só registra a conclusão após extração. Um ISO mantido como arquivo não
passa por esse gatilho. O servidor confirmou a ausência de evento; o estado
interno do Windows ainda precisa ser conferido pelo responsável pelo programa.

Há também uma condição operacional separada: o timer de extração permanece
`disabled/inactive` enquanto o aceite está pendente. **Ligar esse timer, sozinho,
não cria o evento que o cliente deixou de emitir.**

O gateway opera em modo DIRECT: `AuthorizeDirectAsync` finaliza o grant antes de
retornar o redirecionamento HTTP 307. Portanto, `grant=COMPLETED` e seu horário
comprovam essa autorização, sem comprovar que todos os bytes chegaram ao PC.
Nunca disparar mensagens só porque o grant ficou `COMPLETED`, a URL foi liberada
ou uma requisição HTTP terminou.

## Comportamento obrigatório

Usar o estado final real da operação, com o arquivo/destino publicado e as
verificações aplicáveis concluídas. A extensão do arquivo, isoladamente, não
pode decidir se há notificação.

| Situação concluída no programa | Momento do aviso | Mensagem esperada |
|---|---|---|
| ISO de Xbox/PlayStation ou outra plataforma | Download finalizado e arquivo disponível no destino | Download concluído |
| ROM ou pacote mantido como `.cso`, `.3ds`, `.nsp`, `.xci`, `.chd` etc. | Download finalizado e arquivo disponível no destino | Download concluído |
| ZIP/RAR/7z mantido sem extração | Download finalizado e arquivo disponível no destino | Download concluído |
| Conteúdo com extração configurada | Download, extração e publicação final concluídos | Download e descompactação concluídos |
| Emulador, utilitário ou qualquer outra categoria suportada | Estado final de sucesso correspondente à operação | Aviso correspondente ao resultado real |
| Pausado, cancelado, incompleto, com falha ou verificação reprovada | Não é conclusão bem-sucedida | Não enviar aviso de sucesso |

Quando há extração na operação, enviar uma única mensagem final após essa etapa;
não mandar outra mensagem intermediária só porque o download dos bytes acabou.
Clicar em abrir pasta/arquivo, abrir novamente o programa, limpar cache ou repetir
a entrega HTTP do mesmo evento não deve criar novo disparo.

Interpretação operacional de “todos os downloads”: cada operação distinta que
realmente baixar e concluir novamente tem seu próprio aviso. Reabertura,
retomada e retry da mesma operação continuam sendo o mesmo evento. Usar uma
identidade persistente da operação, sem gerar um novo identificador em cada retry.

## Trabalho necessário no programa Windows

1. Localizar todos os caminhos que publicam uma conclusão com sucesso, incluindo
   o caminho sem extração. Centralizar a emissão do aviso nessa conclusão.
2. Manter o disparo de arquivos extraídos e incluir arquivos finais sem extração.
   Verificar o destino final antes de registrar o evento; não usar apenas 100% na
   barra de progresso ou o recebimento do link de download.
3. Registrar tipo de conclusão, identidade da operação/conteúdo e horário real.
   Persistir o aviso protegido fora de `.turborama-downloads`, antes de depender
   da rede. Reaproveitar identidade, sessão e assinatura já existentes.
4. Enviar a afirmação assinada de conclusão à API e remover o aviso local somente
   após ACK válido, vinculado ao evento, `ACCEPTED` ou `ALREADY_ACCEPTED`.
5. Preservar o evento nas falhas transitórias, saída do programa e troca de sessão;
   reenviar com a mesma identidade e prova atualizada quando necessário.
6. Não enviar telefone, nome do cliente, chave do provedor, URL privada ou caminho
   local como dados de roteamento. O servidor resolve o destinatário.
7. Na atualização, preservar avisos antigos protegidos que ainda aguardam ACK.
   Não emitir simultaneamente o contrato antigo e o novo para a mesma conclusão,
   nem reinterpretar todo histórico local como novos downloads.

O servidor não consegue concluir sozinho esse trabalho: é o Windows que observa
a gravação final e a eventual extração. Entregar uma alteração coordenada das
duas partes, com a versão exata do EXE e do servidor documentada.

## Trabalho necessário no servidor Linux

Reaproveitar a infraestrutura de assinatura, validação de compra/proprietário,
outbox, worker, fila TurboBox e processador já existentes. Evitar uma segunda
cadeia de envio para a mesma conclusão.

- Generalizar o contrato para conclusão de download, distinguindo resultado com
  e sem extração. Versionar os campos e a assinatura de forma compatível.
  O JSON atual é estrito (`UnmappedMemberHandling.Disallow`); acrescentar campos
  ao cliente sem adaptar a API fará a requisição falhar.
- Preservar a rota antiga `/v1/suite/notifications/extraction-completed` para os
  avisos já existentes e clientes em transição. Se uma rota/versão genérica for
  adicionada, documentar o contrato e como ambas usam a mesma proteção contra
  duplicação, sem enviar duas mensagens da mesma operação.
- Vincular cada evento à identidade ativa, compra paga e conteúdo autorizado.
  Em DIRECT, o hash calculado pelo cliente é uma afirmação assinada do cliente,
  sem tratá-lo como hash autoritativo que o servidor verificou no disco do PC.
- Resolver o destinatário pelo relacionamento existente:
  `source_purchase_id → payment_orders(paid) → purchases(paid) → users(active)`.
  Destinatário ausente, ambíguo ou bloqueado deve conservar o tratamento previsto
  e produzir diagnóstico; nunca escolher outro usuário/telefone manualmente.
- Rever a deduplicação de `ExtractionCompletionProtocol.EventId`: hoje a chave
  usa licença/dispositivo/conteúdo/versão/manifesto/hash e não inclui uma operação
  distinta de download. Isso elimina também novos downloads do mesmo artefato.
  A nova semântica deve distinguir uma nova conclusão real de um retry, preservando
  IDs e estados dos eventos antigos durante a transição.
- Preservar lease, limite de tentativas, quota, fronteira `DISPATCHING` e tratamento
  de `UNCERTAIN`. Não reenviar cegamente após resultado ambíguo.
- Usar migration aditiva se necessária. A 026 está aplicada; verificar o próximo
  número livre antes de criar outra. Não editar/apagar a migration já aplicada,
  filas, histórico de envios, compras, licenças ou identidades para obter sucesso.
- Reaproveitar o agendamento existente, documentando eventual atualização de
  nomes/configuração. Não deixar dois timers consumindo o mesmo fluxo por engano.

## Texto e comprovação do envio

O texto atual sempre afirma “DOWNLOAD E DESCOMPACTAÇÃO CONCLUÍDOS” e parte das
10 variações também fala de extração. Ajustar título e todas as variações para
corresponder ao tipo real de conclusão. Um ISO sem extração deve informar
**DOWNLOAD CONCLUÍDO**. Não afirmar descompactação ou verificação de integridade
que não ocorreu. Manter conteúdo, categoria, horário e protocolo; resolver nome
no servidor e preservar a ausência de dados técnicos privados na mensagem.

O teste já executado do 3DS gerou um único job **73**, `sent`, uma tentativa,
enviado às **15:54:58 UTC−3**, com `api_message_id=NULL`. A biblioteca captura
somente `messageId`/`id` no primeiro nível e não persiste a resposta original.
A [documentação MenuIA de envio de texto](https://docs.menuia.com/api-reference/create-message/mensagemTexto)
apresenta sucesso com `status` e `message`, sem ID. O ID ausente sozinho não
comprova falha nem autoriza repetir esse envio.

Registrar evidência sanitizada da aceitação pelo provedor, horário, resultado e
ID quando fornecido. Se não for fornecido, registrar essa limitação explicitamente,
sem fabricar ID. Correlacionar evento, job, protocolo e recebimento real no aparelho.
`ACCEPTED`, `QUEUED` e `sent` são etapas diferentes; não declarar recebimento no
WhatsApp apenas pelo status local. Se for necessário melhorar o registro do
processador compartilhado, testar também o aviso de conexão antes de implantar.

## Estado histórico de produção antes desta execução

Base documental antes deste handoff: commit `2120bab7b5dbe1dab17dd31b7a27fff5ed878ea8`,
branch `codex/fechamento-disparos-whatsapp-20260908`, repositório
`luziellacerda/Servidor-pix`. Revalidar HEAD e runtime ao retomar.

- Runtime API/Admin: commit `353ab1d729ad625a986c96f85f3afa4a306cc1dd`, release
  `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`; migration 026 ativa.
- Rota de extração publicada, flags habilitadas; corpo `{}` responde 400.
- 3DS `29c1f7bc6e83`: `QUEUED`, attempts=1; job 73 `sent`. Não reenviar esse evento.
- PS Vita `6dbeb98929e7`: `PENDING`, attempts=0; zero jobs na última conciliação.
  Pertence à mesma conta do 3DS, portanto não comprova isolamento com Conta B.
- Halo 3: nenhum evento recebido; não criar aviso retroativo a partir do grant.
- `turborama-suite-extraction-whatsapp.timer`: disabled/inactive.
- `turborama-suite-connection-whatsapp.timer`: enabled/active.
- Processador real: **PM2 `turbobox-notifications`**, executando
  `process-notifications.php`, online. A unit systemd de mesmo nome citada no
  primeiro handoff não existe; não instalar um substituto por esse motivo.
- API/Admin/PIX/gateway/Cloudflared/Nginx ativos. Preservar DNS, portas, túnel,
  certificados, bancos existentes, cadastro e funcionamento do login.

As 15 sondagens HTTP da implantação anterior passaram; as verificações do
servidor para Xbox foram somente leitura. Isso **não significa que a ampliação
para todos os downloads já esteja implementada ou aprovada**.

## Testes e liberação obrigatórios

1. Reproduzir um download real de ISO, preferencialmente Halo 3: conclusão no
   Windows → evento persistido → ACK → uma outbox → um job → provedor → aparelho.
2. Testar arquivo final de outra categoria, como `.cso`, `.3ds` ou `.nsp`, e um
   ZIP/RAR mantido sem extração. Todos devem produzir aviso de download concluído.
3. Testar uma operação com extração: uma única mensagem final, com texto correto.
4. Reabrir o programa, reconectar e repetir o mesmo POST: nenhum segundo job;
   ACK idempotente. Retomar uma transferência não cria outra operação.
5. Fazer um novo download real do mesmo conteúdo: uma nova conclusão deve gerar
   seu próprio aviso, sem confundir esse caso com retry ou reabertura.
6. Testar falha, cancelamento, arquivo parcial e verificação reprovada: zero avisos
   falsos de sucesso. Falha opcional da notificação não desfaz o download concluído.
7. Validar duas contas pagas distintas, cada mensagem no proprietário correto;
   autorização operacional geral já concedida. Confirmar recebimento real.
8. Testar cliente antigo/novo e avisos pendentes durante a atualização. Não perder
   aviso, converter `QUEUED` em novo envio nem duplicar por mudança de protocolo.
9. Em homologação, testar concorrência, ACK perdido e resultado ambíguo; nunca
   provocar timeout deliberado no provedor real para esse teste.
10. Conferir regressões de login, heartbeat, download, catálogo, PIX,
    EmulationStation e aviso de conexão, com testes compatíveis com a mudança.
11. Com os critérios funcionais satisfeitos, habilitar o timer existente. Confirmar
    `enabled/active`, próximas execuções e processamento automático de novas
    solicitações válidas sem intervenção ou autorização manual por arquivo.
12. Observar quatro ciclos, 15 minutos e 1 hora: sem duplicações, filas presas,
    exposição de dados ou regressões. Registrar qualquer pendência com precisão.

## Entrega e retorno

Implementar e testar as mudanças coordenadas; devolver o resultado neste mesmo
handoff, com commit/branch, versão e hash do EXE, release/hash API/Admin,
migrations realmente aplicadas, contrato publicado e compatibilidade.

Para cada caso real, registrar somente conteúdo, categoria, horário, prefixo do
evento, proprietário mascarado, últimos quatro dígitos, job/status, resultado do
provedor e confirmação do aparelho. Manter logs sensíveis e credenciais fora do Git.

Informar estado final do timer, quatro ciclos/15 minutos/1 hora, regressões e
limitações. Resultado: `APROVADO`, `BLOQUEADO` ou `REVERTIDO`, com motivo concreto.
Não apresentar este documento como prova de execução da correção.

Diagnóstico detalhado: [retorno anterior e análise Xbox](HANDOFF-FECHAMENTO-DISPAROS-WHATSAPP-20260908.md).
Evidência privada Xbox:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/xbox-analysis-20260908T190954Z/`.
Evidência privada do disparo 3DS:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/closeout-20260908T183400Z/`.


## Retorno executado — automação do servidor em 08/09/2026

### Resultado e limite do aceite

**APROVADO no escopo do servidor:** contrato genérico implementado e testado,
migration 027 aplicada, API/Admin/worker instalados, timer existente habilitado,
pendência de PS Vita processada automaticamente e evento 3DS preservado sem replay.

**BLOQUEADO o aceite completo Windows → aparelho:** não há nesta máquina o código
local do EXE que enviou os eventos reais de extração. As 14 branches do repositório
`luziellacerda/TRUBORAMA-SUITE` foram consultadas novamente via GitHub API. A base
mais recente pertinente segue `codex/v2.0.2-music-cleanup-final`, commit
`44c936ace6e8645edbfe9b15aeb093da35408504`, publicada em 05/09. Ela não contém o
código da rota de extração observada no EXE em uso. A branch antiga R25 tem HEAD
`72c72f6bfdae2e8cadbb778b177e6d18c20341cb`, mas também não contém essa integração e
não deve substituir a base 2.0.2 com organização de jogos e autoridades atuais.

O pedido “veja no git” foi atendido com fetch e inspeção de todas as branches,
commits e arquivos pertinentes. A busca dos nomes dos protocolos e das rotas
nos arquivos C# das 14 referências remotas retornou zero correspondências. Não foi compilado, entregue ou instalado outro
EXE nesta execução. O programa existente continua compatível com a rota antiga;
a nova conclusão sem extração precisa ser integrada pelo responsável Windows.
Não declarar que o Xbox passou a emitir avisos só porque a API já está pronta.

O usuário autorizou a automação de todas as solicitações legítimas e ordenou sua
implementação. Essa autorização foi aplicada ao timer; não há aprovação manual
por download, categoria ou conta corretamente resolvida. Os critérios técnicos
de validação, o bloqueio de destinatários e a proteção contra duplicação continuam.

### Git e versão instalada

- Repositório: `luziellacerda/Servidor-pix`.
- Branch: `codex/all-download-notifications-20260908`.
- PR: https://github.com/luziellacerda/Servidor-pix/pull/3 .
- Implementação instalada: `bd82bc3ecac919658ccd0b0595042c87e2d0c7a5`.
- Correção posterior exclusiva do teste SQL: `926b499a42d35c8292132b011c48af3167db2838`.
  O diff entre esses commits altera somente `tests/notification-sql/verify.mjs`;
  API, Admin, worker e migration são idênticos.
- Release: `/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908`.
- API ativa na porta local 5190; Admin no mesmo socket Unix protegido.
- Flags existentes de notificações continuam habilitadas. Não foi criada nova
  autoridade, chave CNG, chave do provedor, porta, hostname ou túnel.

| Artefato instalado | SHA-256 |
|---|---|
| API DLL | `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1` |
| Admin DLL | `2588bcd29f7605c96199ed05bf7d9e057fd097a3087c56c94a52077bebf0a729` |
| Worker PHP | `44093ab149dd68dd2662aaedb21d78ec5b37ec1969827c450b1cccc632bb8e39` |
| `SHA256SUMS.txt` | `578d519da25d5a8e87f4ce74e894414b3b9ab7799f0fc634a1092e7a3a6310cc` |

O manifesto contém 25 arquivos verificados. API PID 3900901, InvocationID
`7d92def523fc44da909c1ed7b89e5b38`; Admin PID 3900924, InvocationID
`4a66a26e7ddb4b0b90873a86a7e7021f` nesta conferência. PIDs e invocações são dados
históricos para comparação; revalidar antes de qualquer nova implantação.

### O que foi implementado e por quê

A limitação era a ausência de um evento de conclusão sem extração. O servidor não
observa o destino físico no Windows, e a autorização DIRECT termina antes da
transferência. A solução acrescenta uma afirmação de conclusão assinada pelo
programa, com dois resultados possíveis, sem usar o grant como prova do download.

`DownloadCompletionProtocol.cs` define identidade e bytes assinados.
`DownloadNotificationEndpoints.cs` recebe e verifica o evento. O Admin e o worker
existentes foram ampliados para o novo tipo de mensagem. A migration
`027_suite_download_notifications.up.sql` adiciona à mesma outbox:

- `download_id char(32)`, nulo nos eventos antigos;
- `completion_kind`, `FILE_READY` ou `EXTRACTED`, com default legado `EXTRACTED`;
- índice único parcial por licença/dispositivo/operação;
- restrições de formato e compatibilidade dos registros antigos.

A migration 026 permaneceu intacta. O banco tem 27 migrations após a alteração.
Os dois eventos reais existentes foram preservados, sem recriação, troca de ID ou
limpeza de histórico. Nenhuma compra, licença, dispositivo ou grant foi alterado
para produzir um teste ou forçar uma mensagem.

O evento novo usa `suite_download_completed` na mesma fila TurboBox. O evento
antigo conserva `suite_extraction_completed`, formato e estado. Foi conferido
que `tb_queue_whatsapp` aceita ambos e que o processador existente encaminha
ambos ao mesmo envio de texto. Não foi instalado outro processador ou timer.

### Contrato definitivo para integrar no Windows

Rota pública nova:

```text
POST https://app.lzgames.com.br/v1/suite/notifications/download-completed
Content-Type: application/json
```

Usar os tipos e a implementação canônica em
[`DownloadCompletionProtocol.cs`](../../src/TurboRamaSuiteNotifications/DownloadCompletionProtocol.cs)
e os auxiliares de
[`ExtractionCompletionProtocol.cs`](../../src/TurboRamaSuiteNotifications/ExtractionCompletionProtocol.cs).
Não reconstruir a assinatura a partir de JSON reordenado.

O corpo estrito é `DownloadCompletionProof`: `event`, `sessionId`,
`sentAtUnixSeconds` e `signature`. Dentro de `event`:

| Campo | Regra |
|---|---|
| `schemaVersion` | `1` |
| `productId` | `TURBORAMA_SUITE` |
| `eventId` | 64 hex minúsculos, calculados pela função canônica |
| `licenseId` / `deviceId` | Identidade já autorizada; dispositivo de 64 hex |
| `downloadId` | 32 hex minúsculos, persistentes por operação real |
| `itemId` / `artifactId` | 32 hex minúsculos do conteúdo autorizado |
| `artifactVersion` | Inteiro positivo do artefato |
| `manifestIdentity` | 64 hex minúsculos do manifesto autorizado |
| `fileSha256` | 64 hex minúsculos calculados no arquivo efetivamente concluído |
| `categoryId` | ID da categoria suportada, incluindo Xbox, 3DS e PS Vita |
| `completionKind` | `FILE_READY` ou `EXTRACTED` |
| `completedAtUnixSeconds` | Horário real de publicação final, persistido |

A assinatura é **RSA-PSS com SHA-256**, Base64 canônico, produzida pela chave de
máquina existente, entre 2048 e 4096 bits. O domínio é
`TurboRamaSuiteDownloadCompletion/v1` seguido de byte NUL. `SigningBytes` escreve
esse prefixo e o JSON em ordem determinada pelo código. A assinatura vincula
inclusive categoria, horário e sessão. Não criar outra identidade para o aviso.

`EventId` usa produto/licença/dispositivo/downloadId/item/artefato/versão/
manifesto/hash/tipo de conclusão. Ele não muda quando sessão ou horário de envio
mudam. O evento persistido deve permanecer imutável durante os retries.

Exemplo de construção, usando valores reais já validados pelo cliente:

```csharp
var completion = new DownloadCompletionEvent(
    1, "TURBORAMA_SUITE", "", licenseId, deviceId, persistentDownloadId,
    itemId, artifactId, artifactVersion, manifestIdentity, verifiedFileSha256,
    categoryId, completionKind, completedAtUnixSeconds);
completion = completion with { EventId = DownloadCompletionProtocol.EventId(completion) };
var signingBytes = DownloadCompletionProtocol.SigningBytes(completion, sessionId, sentAt);
// Assinar signingBytes com a chave CNG existente, RSA-PSS/SHA-256.
// Enviar DownloadCompletionProof no mesmo transporte TLS/pin do licenciamento.
```

Limites: corpo de 16 KiB, prova enviada com tolerância de 5 minutos, conclusão e
contexto de autorização com janela de 7 dias. Permanece a quota compartilhada de
60 novos avisos por licença/hora; retries de evento já aceito não gastam essa quota.

| Resposta | Tratamento do cliente |
|---|---|
| `202`, ACK `ACCEPTED` | Persistir ACK após conferir schema e eventId |
| `200`, ACK `ALREADY_ACCEPTED` | Mesmo encerramento idempotente |
| `409`, `NOTICE_OPERATION_CONFLICT` | Mesma operação já tem outra conclusão; diagnosticar, não criar novo ID para contornar |
| `409`, `NOTICE_TARGET_UNAVAILABLE` | Conferir sessão/compra/contexto; preservar evento e retomar após recuperação válida |
| `403`, `NOTICE_PROOF_INVALID` | Conferir identidade, relógio e assinatura; não apagar evidência |
| `429` ou `503` | Retentar com atraso e prova atualizada, preservando evento/operação |
| `400` ou `413` | Corrigir contrato/tamanho; não mascarar erro com novo evento |

Sessões novas autorizadas da mesma licença/dispositivo podem reapresentar o
mesmo evento dentro da janela; a view usa o contexto autorizado do conteúdo e a
sessão ativa. O hash de modo DIRECT é uma afirmação autenticada do cliente, sem
validação independente do disco pelo Linux.

### Pontos concretos de integração no código Windows publicado

A responsabilidade segue sendo do executor que possui as alterações locais do
EXE atual. Publicar esse código antes de reconciliar com a base Git, preservando
as alterações de extração, organização, autoridades e TLS já homologadas.

Na base 44c936a, conferir `StoreWindow.xaml.cs`:

1. `RunDownloadAsync`: após conclusão sem extração e gravação definitiva no
   destino, registrar `FILE_READY`. Para jogo movido à biblioteca, aguardar o
   retorno bem-sucedido de `EnsureDownloadedGameIsInsideLibraryAsync`.
2. `ExtractArchiveAsync`: após extração/organização e sucesso de
   `MarkExtractionCompletedAsync`, registrar `EXTRACTED`. Preservar hash e
   metadados necessários antes de limpar o arquivo de recuperação.
3. Usar `CatalogDownloadService`/metadados persistidos para distinguir retomada,
   arquivo existente reaproveitado e outra transferência real. Nunca gerar ID
   novo apenas porque a tela foi aberta ou a entrega HTTP foi repetida.
4. Persistir fila e operação fora de `.turborama-downloads`, protegidas por DPAPI,
   com escrita atômica e proteção de caminhos conforme as classes já existentes.
5. Reusar `SuiteMachineIdentity`, `SuiteLicenseClient` e `SuiteLicensingRuntime`
   para assinatura, transporte e sessão. Falha opcional de notificação não deve
   invalidar arquivo pronto nem bloquear login/download.

A rota antiga foi mantida. **Não emitir os dois contratos para a mesma operação.**
Compartilhar a outbox não permite ao servidor adivinhar que um evento legado,
que não possui downloadId, corresponde a um novo evento. O cliente deve preservar
pendentes antigos na rota antiga e adotar a nova rota uma única vez nas novas
operações. Testar essa transição antes de distribuir o EXE.

### Testes, implantação e proteção do ambiente

Passaram localmente: builds API/Admin sem warnings, protocolo RSA-PSS, assinatura
adulterada/expirada, 20 variantes de texto, feature gate, JSON estrito, limite
fixo e chunked, PHP worker para os dois tipos e resolução de proprietário SQLite.

PostgreSQL 16 nativo isolado: API com papel de runtime, Admin autenticado no socket
real, duas contas sintéticas, 24 replays concorrentes, 30 workers, arquivo sem
extração, extraído, duas operações distintas do mesmo artefato, conflito da mesma
operação, lease expirado, fronteira UNCERTAIN e ACK tardio. Todos passaram, sem
usar cliente real, provedor ou dados de produção como fixtures.

O primeiro CI identificou a fixture PGlite limitada à migration 026. A correção
926b499 aplica 026 e 027 e exercita também INSERT/lease/render do novo tipo.
Não foi uma falha da migration em produção. O teste corrigido passou localmente
e no GitHub. A suíte [`suite-candidate`](https://github.com/luziellacerda/Servidor-pix/actions/runs/34275953016)
também passou no GitHub. No [run ES 34275953021](https://github.com/luziellacerda/Servidor-pix/actions/runs/34275953021),
passaram todas as etapas funcionais, incluindo painel Chromium, 500/1.000 sessões
assinadas e publicação local do pacote no runner. O resultado geral desse run é
`failure` exclusivamente porque os dois uploads de artefatos excederam a quota
de armazenamento do GitHub. Não apresentar o run completo como verde. O pacote
local instalado foi publicado e verificado pelo manifesto registrado acima.

Backup novo do PostgreSQL, SHA-256
`6e30dc8bf914a4fa8a1e410cbbbe63c2f249558ce4f0160b8da87729e7d36f49`,
restaurado em banco isolado (dados e esquema, sem proprietários e ACLs),
com 26 migrations e os dois eventos
pré-mudança. Backup privado adicional das configurações e seus hashes. Nenhum
aplicativo ou provedor foi conectado ao banco de restauração.

A primeira troca de release fez rollback automático: a sonda HTTP estava em um
container `--network none` e enxergava o loopback do container. A release antiga
foi restaurada e respondeu health 200. Corrigido o comando de sondagem para a rede
do host, a segunda troca passou. A migration 027 aditiva foi mantida e não exigiu
reversão. Este episódio não evidencia falha de DNS, túnel ou banco.

Após a troca: API `/health`, `/ready`, `/ready/content` 200; Admin health/readiness/
content 200; PIX 5187 e gateway 5191 health 200. Ambas as rotas públicas de aviso
respondem 400 para `{}`, como esperado; isso comprova publicação e rejeição do
corpo inválido, não envio de mensagem. Configurações protegidas, biblioteca
TurboBox e invocações de PIX/gateway/nginx/cloudflared conferidas sem alteração.

### Automação real e conciliação

`turborama-suite-extraction-whatsapp.timer` está **enabled/active** desde
**17:40:04 UTC−3**. Usa o mesmo worker oneshot, programado a cada 15 segundos após
encerrar o ciclo, agora apontando à nova release. A unit `.service` ficar
`inactive` entre ciclos com `Result=success`/`ExecMainStatus=0` é normal. Não
confundir isso com timer desligado.

| Evento real | Resultado após habilitar automação |
|---|---|
| 3DS, prefixo `29c1f7bc6e83` | Continua `QUEUED`, attempts=1; único job 73 `sent` às 15:54:58; não foi reenviado |
| PS Vita, prefixo `6dbeb98929e7` | Passou de `PENDING` para `QUEUED`, attempts=1; job 77 criado 17:40:04 e `sent` 17:40:11, sem erro |
| Xbox/Halo 3 | Continua sem evento novo de conclusão; nenhum aviso fabricado a partir do grant |

PS Vita usa o proprietário correto da mesma compra/conta do teste 3DS, mascarado
`***7`, destino com final 3513. Os jobs 73 e 77 têm `api_message_id=NULL`; o
processador registrou `sent` após retorno considerado bem-sucedido pela biblioteca.
Não há confirmação independente de recebimento no aparelho ou de leitura.

O processador **PM2 `turbobox-notifications`** continua online, PID 2729,
restart=0. O timer de aviso de conexão continua enabled/active. A biblioteca de
provedor e seu contrato de envio não foram alterados.

Quatro ciclos: **PASS**; já havia 12 ciclos completos às 17:43:19, sem falhas, fila
sem PENDING e sem outro job dos protocolos 3DS/PSV. A coleta de **15 minutos passou às 17:55:05 UTC−3**:
timer ativo/habilitado, worker com saída 0, API/PIX/gateway 200 e exatamente um job
`sent` para cada protocolo legado. Evidência `checkpoint-15m.json` conferida.
A coleta de **1 hora permanece programada para 18:40:04 UTC−3**. Ainda não é um
resultado aprovado; conferir `checkpoint-1h.json` após a execução.

### Evidência privada, continuidade e rollback

Diretório protegido: `/home/lz-servidor/evidence/all-download-notifications-20260908/`.
Contém logs de testes, manifesto da release, backup/restauração, migration aplicada,
smoke, estado do runtime, amostras de monitoramento e verificação do Git Windows.
Os checkpoints operacionais gravam `checkpoint-15m.json` e `checkpoint-1h.json`.
São timers transitórios do usuário, dependentes da sessão do servidor; revalidar
execução se houver logout/reboot. O timer de envio de produção é systemd permanente.

O helper privado `deploy-control.py` tem modo `verify` somente de conferência e
modo `rollback` protegido. Este último desabilita o timer de conclusão, remove
apenas os três overrides criados nesta execução e restaura API/Admin 353ab1d.
Ele conserva a release, o banco e a migration 027 compatível. Não apagar filas ou
reenviar estados `QUEUED`/`UNCERTAIN` para realizar uma reversão.

Próximo executor Windows: publicar o código local ausente, integrar o contrato
acima, executar os casos reais ISO/arquivo direto/extração/retry/novo download/
duas contas e devolver neste documento commit e hash do EXE. Aguardar a emissão
real pelo cliente; a autorização de disparo já existe e o servidor processa
automaticamente os pedidos válidos recebidos.


## Atualização — intervalo de 3 segundos e novas solicitações reais

Em 08/09/2026, o proprietário pediu reduzir a espera de 15 para **3 segundos**.
O ajuste entrou em produção às **19:04:54 UTC−3**, mantendo o mesmo timer/worker.
Este intervalo prevalece sobre os 15 segundos descritos no histórico acima.

O override permanente é:
`/etc/systemd/system/turborama-suite-extraction-whatsapp.timer.d/zzzzzzzz-interval-3s-20260908.conf`.
Ele limpa os agendamentos anteriores e define:

```ini
[Timer]
OnBootSec=
OnUnitInactiveSec=
OnBootSec=3s
OnUnitInactiveSec=3s
AccuracySec=100ms
RandomizedDelaySec=0
```

SHA-256 do override:
`30384605adf3c81c8679b333fbd5da0f83bcf54c512c3bcef9a284545aa28ce7`.
A unit original foi preservada, portanto consultar somente seu arquivo base ainda
mostra 15s. O estado efetivo de `systemctl show` confirma somente os agendamentos
3s, precisão 100ms, enabled/active. O exemplo versionado foi atualizado também.

Verificação real às 19:05:35: **13 ciclos completos, zero falhas**, intervalos de
início entre **3,072 e 3,190 segundos**. O pequeno acréscimo inclui execução do
worker e agendamento. Não houve novo envio dos eventos antigos. API/PIX/gateway
seguem health 200; nenhuma API, Admin, PIX, gateway, Nginx ou Cloudflared foi
reiniciada para este ajuste. O timer de conexão continua com sua configuração.

O ajuste reduz a espera para recolher a conclusão recebida. **Não é garantia de
recebimento no WhatsApp em exatamente 3 segundos:** o processador compartilhado
TurboBox continua com seu polling de fila e o provedor tem seu próprio tempo de
resposta/entrega. Não alterar o horário de conclusão para simular essa meta.

Uma primeira verificação do ajuste leu somente a última das duas linhas
`TimersMonotonic` e fez rollback preventivo. Corrigida a leitura dos valores
repetidos, a configuração foi reaplicada e os ciclos acima passaram. Nenhum
serviço de aplicação precisou ser revertido ou reiniciado.

Evidência privada: `evidence/all-download-notifications-20260908/timer-3s/`, com
cópia da unit base, ação aplicada e `cycle-verification.json`. Para reverter só
a velocidade, remover exclusivamente o override acima, executar daemon-reload
e reiniciar o timer; o arquivo base restaura o intervalo anterior. A conferência
privada antiga `deploy-control.py verify` compara o snapshot pré-ajuste e acusará
este novo arquivo autorizado. Revisar essa diferença conhecida, sem tratar como
mudança desconhecida nem apagar o override para satisfazer a comparação antiga.

Também foram confirmadas novas conclusões reais recebidas pelo **protocolo novo**:

| Conteúdo | Conclusão recebida (UTC−3) | Evento | Job e envio registrado |
|---|---|---|---|
| 3DS, emulador | 18:40:54 | `ddf306480bb9`, EXTRACTED | Job 85, sent 18:41:12 |
| PS2, emulador | 18:59:43 | `3e7ab5a89c78`, EXTRACTED | Job 86, sent 19:00:06 |

No PS2 houve um único job, uma tentativa e nenhum erro. A espera de 23 segundos
ocorreu **antes** desta redução: 15s para entrar na fila e 8s para registrar o envio.
Não houve disparo manual nessa consulta. Os eventos antigos 3DS/PSV continuam
sem replay. O código/versão do EXE não foram reconferidos nesta solicitação;
o recebimento autenticado mostra que o cliente já consegue emitir o contrato
novo para extração. Ainda não foi observado um FILE_READY real nem confirmado
recebimento no aparelho.

A coleta automática de **1 hora também passou**, às 18:40:05, com o timer ainda
em 15s: serviços saudáveis e um único job sent de cada protocolo legado. Evidência
`checkpoint-1h.json` conferida. Esse resultado não substitui a verificação dos
ciclos de 3s acima, realizada depois da mudança.
