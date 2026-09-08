# Handoff único de encerramento — disparos WhatsApp após descompactação

Data: 08/09/2026 (UTC−3).

Destino: operador do servidor Linux do TurboRama Suite.

Natureza: **execução operacional e aceite ponta a ponta**.
Documento anterior: `HANDOFF-WHATSAPP-CONCLUSAO-DESCOMPACTACAO-20260908.md`.

## Objetivo final

Encerrar esta implantação em uma única sequência controlada:

1. confirmar o evento real já recebido do aplicativo Windows;
2. processá-lo uma vez pelo worker novo;
3. comprovar a entrada na fila TurboBox e o resultado do provedor;
4. confirmar o recebimento da mensagem no WhatsApp autorizado;
5. validar isolamento e deduplicação com duas contas;
6. habilitar o timer somente depois de todos os critérios obrigatórios;
7. acrescentar o retorno final **neste mesmo arquivo**, sem gerar outro handoff.

Não há autorização para redesenhar o fluxo, criar outro worker, alterar o texto,
recompilar o Windows ou trocar integração, túnel, domínio, banco ou provedor.

## Fonte única e estado confirmado

Executar a partir da linha Linux já devolvida:

- branch-base: `codex/emulationstation-suite-extraction-linux-20260908`;
- commit de retorno: `17af26cc8e1aa88edfaef0a4e25ab598e4f682e6`;
- commit operacional instalado: `353ab1d729ad625a986c96f85f3afa4a306cc1dd`;
- release: `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`;
- API: `Suite__ExtractionNotifications__Enabled=true`;
- Admin: `SUITE_EXTRACTION_NOTICES_ENABLED=1`;
- migration `026_suite_extraction_notifications`: aplicada;
- unit de processamento: `turborama-suite-extraction-whatsapp.service`;
- timer: `turborama-suite-extraction-whatsapp.timer`, instalado e parado por
  decisão de segurança antes do primeiro destinatário real.

Estado observado pelo PC depois do retorno Linux:

- o aplicativo de teste está aberto e responsivo;
- o evento é de **3DS**, categoria **Emuladores**, concluído inicialmente por
  volta de 12:10 e reenviado depois da abertura do programa por volta de 15:20;
- a caixa DPAPI local ficou vazia depois que a rota foi implantada;
- o cliente só remove um aviso local após resposta vinculada `ACCEPTED` ou
  `ALREADY_ACCEPTED`;
- o servidor informou ao proprietário que a solicitação chegou;
- a sondagem pública com `{}` mudou de 404 para
  `400 NOTICE_INVALID`, sem redirecionamento;
- o timer continua parado e, portanto, receber o evento ainda não comprova
  enfileiramento nem entrega no WhatsApp.

Não repetir download antes de localizar o evento atual. Uma repetição do mesmo
artefato é deduplicada e pode confundir o diagnóstico sem criar nova linha.

## Limites que não podem ser ultrapassados

- Não alterar DNS, Cloudflare, Nginx, IP público, portas ou certificados.
- Não mudar licenças, sessões, compras, grants, dispositivos ou identidades.
- Não substituir API/Admin, migration, biblioteca TurboBox ou worker de login.
- Não executar `DROP`, `TRUNCATE`, `DELETE`, migration down ou reset de banco.
- Não apagar ou editar jobs existentes para obter resultado visual.
- Não cadastrar telefone manualmente no código ou no Git.
- Não imprimir telefone completo, nome completo, licença, dispositivo, tokens,
  chaves, conteúdo de arquivos `env` ou corpo integral da mensagem nos logs.
- Não iniciar o timer contínuo antes do disparo unitário e da confirmação real.
- Não reenviar automaticamente um evento `UNCERTAIN`.
- Não interpretar `ACCEPTED` ou `QUEUED` como mensagem entregue.
- Não relaxar validação, quota, compra paga, usuário ativo ou bloqueio de
  destinatário para fazer o teste passar.

Se qualquer etapa exigir mudança de código ou de dados de cliente, parar e
registrar a evidência na seção de retorno. Não improvisar uma correção em
produção dentro deste fechamento.

## Ordem única de execução

### 1. Abrir registro privado desta rodada

Criar os registros dentro do diretório privado já usado pelo retorno Linux:

`/home/lz-servidor/evidence/extraction-whatsapp-20260908/`

Manter permissões `0700`. Registrar horário inicial, operador, commit instalado,
release ativa, estados das units e hashes. Não copiar dumps, credenciais, dados
pessoais ou arquivos de ambiente para o Git.

Antes de qualquer disparo, conferir:

```bash
systemctl is-active turborama-suite-api.service
systemctl is-active turborama-suite-admin.service
systemctl is-active turbobox-notifications.service
systemctl is-active turborama-suite-extraction-whatsapp.timer
systemctl is-enabled turborama-suite-extraction-whatsapp.timer
systemctl show turborama-suite-extraction-whatsapp.service \
  -p FragmentPath -p User -p Group -p ExecStart -p Result -p ExecMainStatus
```

Resultado esperado: API, Admin e processador TurboBox ativos; timer de extração
inativo/desabilitado; `ExecStart` apontando para a release verificada, nunca para
`/opt/VERIFIED-EXTRACTION-NOTICES`.

Parar se API/Admin/TurboBox não estiverem saudáveis, se o caminho ainda for o
placeholder ou se outra unit desconhecida já estiver consumindo essa outbox.

### 2. Revalidar os contratos sem criar outro evento

```bash
curl --silent --show-error --max-time 20 \
  --header 'Content-Type: application/json' \
  --data '{}' \
  https://app.lzgames.com.br/v1/suite/notifications/extraction-completed
```

Resultado obrigatório: HTTP 400 com `NOTICE_INVALID`. Essa prova apenas confirma
rota/feature; `{}` não possui identidade válida e não deve criar evento.

Conferir também `/health`, `/ready`, `/ready/content`, login, heartbeat, PIX,
catálogo e EmulationStation pelos procedimentos já existentes. Não reiniciar
serviços saudáveis somente para obter novos timestamps.

### 3. Localizar a solicitação recebida

Executar a consulta no PostgreSQL 16 local, banco `postgres`, sem expor
identificadores completos:

```sql
SELECT left(event_id,12) AS event_prefix,
       content_name,
       category_id,
       completed_at AT TIME ZONE 'America/Fortaleza' AS completed_fortaleza,
       created_at AT TIME ZONE 'America/Fortaleza' AS received_fortaleza,
       status,
       attempts,
       last_error_code
FROM suite.suite_extraction_notification_outbox
WHERE category_id = 'emulators'
  AND created_at >= timestamptz '2026-09-08 15:00:00-03'
ORDER BY created_at DESC;
```

O alvo deve corresponder ao 3DS recebido nesta rodada. Registrar privadamente o
`event_id` completo para conciliação, mas publicar no retorno apenas os primeiros
12 caracteres.

Antes do worker, o resultado aceitável é `PENDING`, sem `last_error_code`. Se for:

- `QUEUED`: não executar novamente; ir direto à conciliação da fila/provedor;
- `DISPATCHING` ou `UNCERTAIN`: não reenviar; conciliar primeiro;
- `SKIPPED`: corrigir somente o cadastro legítimo fora deste handoff, depois de
  identificar a causa; não forçar destinatário;
- `DEAD`: registrar tentativas/erro e parar;
- inexistente: comparar horário, categoria e logs da API; não limpar a fila do
  cliente nem gerar compras/grants artificiais;
- mais de um candidato: selecionar pelo horário, conteúdo e contexto autorizado;
  não processar em massa durante o teste unitário.

Registrar também o total por estado:

```sql
SELECT status, count(*)
FROM suite.suite_extraction_notification_outbox
GROUP BY status
ORDER BY status;
```

### 4. Confirmar destinatário autorizado sem revelar dados

Antes de cruzar a fronteira `DISPATCHING`, confirmar pelo relacionamento já
existente:

`source_purchase_id → payment_orders(paid) → purchases(paid) → users(active)`.

Critérios obrigatórios:

- exatamente um proprietário;
- telefone normalizado por `tb_phone_e164`;
- destinatário não bloqueado pela política instalada;
- proprietário confirma que esse número pode receber a mensagem de teste.

No registro de retorno, usar somente identificador interno mascarado e últimos
quatro dígitos do telefone. Se não houver autorização inequívoca, não executar o
worker real; registrar `AGUARDANDO_DESTINATARIO_AUTORIZADO`.

### 5. Executar exatamente um disparo controlado

Com o timer ainda parado, executar uma única vez a unit oneshot:

```bash
sudo systemctl start turborama-suite-extraction-whatsapp.service
systemctl show turborama-suite-extraction-whatsapp.service \
  -p Result -p ExecMainCode -p ExecMainStatus -p ActiveState -p InactiveExitTimestamp
journalctl -u turborama-suite-extraction-whatsapp.service \
  --since '10 minutes ago' --no-pager
```

`Type=oneshot` pode terminar como `inactive` após sucesso; avaliar `Result=success`
e `ExecMainStatus=0`, não apenas `ActiveState`.

Não executar a unit uma segunda vez enquanto o primeiro evento não estiver
conciliado. Consultar novamente a linha da outbox. O resultado esperado é
`QUEUED`, com `finished_at` preenchido e sem erro.

Tratamento obrigatório:

| Estado | Ação |
|---|---|
| `PENDING` | Ler `last_error_code`; aguardar a política de retry, sem loop manual |
| `LEASED` | Aguardar expiração normal; não editar token/tempo |
| `DISPATCHING` | Não executar de novo; verificar fila antes de qualquer ação |
| `QUEUED` | Seguir para fila/provedor; ainda não é entrega |
| `SKIPPED` | Corrigir cadastro/autorização legítima; timer permanece parado |
| `UNCERTAIN` | Conciliar fila e provedor; proibição de reenvio cego |
| `DEAD` | Registrar causa e parar |

### 6. Comprovar fila TurboBox, provedor e WhatsApp

Localizar o job pelo tipo `suite_extraction_completed`, horário, cliente interno
e protocolo `TS-<prefixo>`. Não usar telefone completo como chave de pesquisa em
um relatório público.

Confirmar, nesta ordem:

1. uma única entrada correspondente em `notification_jobs`;
2. estado processado pelo `turbobox-notifications`;
3. retorno do provedor com identificador e horário;
4. estado final `sent` na fila existente;
5. recebimento no aparelho autorizado;
6. texto com `LZ GAMES | TURBORAMA SUITE`, saudação adequada ao horário UTC−3,
   nome do conteúdo, categoria, horário e protocolo;
7. ausência de IP, MAC, telefone, licença, dispositivo, caminho local e segredo.

Se a fila estiver `QUEUED` e o provedor falhar, diagnosticar o processador
TurboBox existente sem alterar a outbox ou o worker de login. Se houver timeout
ambíguo, consultar o provedor antes de qualquer repetição.

### 7. Testes obrigatórios antes de habilitar o timer

Executar todos os seguintes sem criar dados falsos em produção:

#### Conta A — evento atual

- evento recebido uma vez;
- outbox passa pelo fluxo esperado;
- exatamente um job;
- exatamente uma mensagem recebida;
- reiniciar/reabrir o aplicativo não cria segundo job para o mesmo evento;
- resposta do cliente para repetição deve ser `ALREADY_ACCEPTED`.

#### Conta B — isolamento real

Com uma segunda conta paga e destinatário explicitamente autorizado:

- concluir uma nova extração que realmente chegue a `ABRIR PASTA ✓`;
- confirmar evento separado, proprietário correto e telefone correto;
- executar novamente a unit oneshot apenas depois de encerrar a Conta A;
- confirmar uma única mensagem na Conta B;
- confirmar que nenhuma informação ou mensagem cruzou entre A e B.

#### Casos negativos

- `{}` continua em 400 e não cria linha;
- reabertura do programa não duplica o mesmo evento;
- item mantido como arquivo bruto, sem extração, não gera este aviso;
- falha opcional de notificação não desfaz download concluído;
- limpeza de `.turborama-downloads` não é gatilho nem apaga aviso pendente;
- destinatário ausente, ambíguo ou bloqueado não é substituído manualmente;
- simulação de timeout/resultado ambíguo deve usar homologação/dados sintéticos,
  nunca provocar incerteza deliberada no provedor real.

O aceite multiusuário exige as duas contas. Se a Conta B ainda não estiver
disponível, a primeira entrega pode ser comprovada, mas o timer não deve ser
declarado liberado para todos os clientes.

### 8. Habilitar operação contínua

Somente depois de todos os critérios anteriores, habilitar o timer existente:

```bash
sudo systemctl enable --now turborama-suite-extraction-whatsapp.timer
systemctl is-enabled turborama-suite-extraction-whatsapp.timer
systemctl is-active turborama-suite-extraction-whatsapp.timer
systemctl list-timers --all | grep turborama-suite-extraction-whatsapp
```

Resultado obrigatório: `enabled`, `active` e próxima execução agendada. Não
alterar o intervalo de 15 segundos nesta rodada.

Observar pelo menos quatro execuções e confirmar:

- serviço termina com sucesso quando não há evento;
- nenhum job duplicado aparece;
- API/Admin/PIX/gateway/Cloudflared continuam ativos;
- login e aviso de conexão continuam funcionando;
- outbox não acumula `PENDING`, `DISPATCHING` expirado ou `UNCERTAIN`;
- logs não contêm dados pessoais ou segredos.

Depois, conferir novamente em 15 minutos e em 1 hora. O timer somente pode ser
considerado permanente após essas duas janelas sem regressão.

## Critérios finais de aceite

Marcar concluído apenas quando todos estiverem verdadeiros:

- [x] rota pública ativa e rejeitando corpo inválido com 400;
- [x] evento real do 3DS identificado na outbox;
- [x] destinatário da Conta A autorizado e resolvido pelo servidor;
- [x] outbox da Conta A conciliada;
- [ ] um único job da Conta A confirmado como `sent` pelo provedor;
- [ ] recebimento real da Conta A confirmado;
- [ ] deduplicação após reabertura confirmada;
- [ ] Conta B paga/autorizada testada sem cruzamento de dados;
- [ ] um único job da Conta B confirmado como `sent` e recebido;
- [ ] casos negativos obrigatórios conferidos;
- [ ] timer `enabled` e `active`;
- [ ] quatro ciclos, 15 minutos e 1 hora sem regressão;
- [ ] login, heartbeat, PIX, conteúdo, ES e aviso de conexão preservados;
- [x] nenhuma credencial, telefone completo ou dado pessoal publicado;
- [x] evidência privada e retorno resumido registrados.

`ACCEPTED`, `ALREADY_ACCEPTED`, `QUEUED` ou HTTP 2xx isoladamente não satisfazem
o aceite. A entrega exige `sent` no processador/provedor e confirmação do aparelho.

## Reversão segura se houver regressão

Se o problema aparecer depois de habilitar o timer:

```bash
sudo systemctl disable --now turborama-suite-extraction-whatsapp.timer
```

Isso é a primeira e preferida contenção. Não apagar eventos. Não desfazer a
migration 026. Não remover a release. Não alterar o worker de conexão.

Se API/Admin apresentarem regressão, usar somente o rollback verificado já
preparado no servidor:

```bash
python3 /home/lz-servidor/evidence/extraction-whatsapp-20260908/deploy-control.py rollback
```

Antes de executar, conferir caminho, proprietário e hash conforme a evidência
privada. A rotina preserva migration, outbox e diagnóstico. Depois, registrar o
motivo e o estado de cada serviço neste documento.

Eventos `UNCERTAIN` devem permanecer preservados até a conciliação com a fila e
o provedor. Não reenfileirar por SQL e não alterar seu status manualmente.

## Retorno obrigatório — preencher aqui, sem novo handoff

O operador Linux deve acrescentar abaixo um único retorno e fazer push na mesma
linha de trabalho. Não criar outro documento para esta rodada.

```text
Início/fim UTC−3:
Commit/release realmente ativos:
API/Admin/PIX/gateway/Cloudflared:
Rota pública inválida (HTTP/código):
Evento A (prefixo, conteúdo, recebido, estado inicial/final):
Destinatário A autorizado (ID mascarado/últimos 4):
Job A (um único registro, estado, horário, ID do provedor mascarado):
Recebimento A confirmado por:
Deduplicação/reabertura A:
Evento B (prefixo, conteúdo, recebido, estado inicial/final):
Destinatário B autorizado (ID mascarado/últimos 4):
Job B (um único registro, estado, horário, ID do provedor mascarado):
Recebimento B confirmado por:
Isolamento A/B:
Casos negativos:
Timer antes/depois:
Quatro ciclos:
Verificação de 15 minutos:
Verificação de 1 hora:
Regressões dos serviços existentes:
Estados PENDING/LEASED/DISPATCHING/QUEUED/SKIPPED/UNCERTAIN/DEAD:
Arquivos privados de evidência:
Segredos/dados pessoais no Git: NÃO
Resultado final: APROVADO | BLOQUEADO | REVERTIDO
Bloqueio ou reversão, se houver:
```

O resultado somente pode ser `APROVADO` com todas as caixas da seção de aceite
marcadas. Se faltar segunda conta, confirmação do aparelho ou resultado do
provedor, registrar `BLOQUEADO` com o item exato; não gerar novo handoff e não
declarar o sistema finalizado.

## Retorno Linux — verificação de 08/09/2026

**Autorização geral registrada; 3DS processado uma vez, job 73 em `sent`.
Resultado do aceite completo: BLOQUEADO, pelos itens de comprovação abaixo.**

O bloqueio anterior `AGUARDANDO_DESTINATARIO_AUTORIZADO`, registrado no commit
`ed21880`, foi resolvido na conversa. O usuário autorizou o disparo e esclareceu
que a autorização vale para todas as solicitações válidas recebidas. **Não pedir
nova autorização por evento.** Permanecem as validações de compra paga, usuário
ativo, proprietário único, telefone válido e política de bloqueio. A autorização
operacional geral está registrada privadamente em `authorization-a.json`.

Esta continuação executou a oneshot existente uma única vez para o 3DS. O timer
continua parado enquanto faltam confirmação no aparelho, reabertura, segunda
conta e demais critérios das etapas 7 e 8. Nenhuma confirmação de recebimento
foi fornecida até o encerramento deste registro.

### Fonte e produção conferidas

- Handoff recebido: `dfa08b937f9f2353446c0f58b2b0ab4ab750f406`, branch
  `codex/fechamento-disparos-whatsapp-20260908`; retorno anterior `ed21880`.
  A consulta ao Git nesta continuação não encontrou outro commit nessa branch.
- Commit operacional: `353ab1d729ad625a986c96f85f3afa4a306cc1dd`;
  release `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`.
- Os 27 arquivos do manifesto instalado conferem. Nome correto do manifesto:
  `SHA256SUMS.txt`; SHA-256:
  `f822ee278de21b4d1b9704a3ea3bb5c660dc40efefca329da90b5226e265f285`.
- API, Admin, PIX, gateway, Cloudflared e Nginx ativos, sem reinício nesta rodada.
  Comparação após o disparo: 29 arquivos protegidos e biblioteca intactos;
  PIX, gateway, Cloudflared e Nginx preservam suas invocações anteriores.
- Correção operacional da etapa 1: o processador existente é **PM2
  `turbobox-notifications`**, executando `process-notifications.php`, estado
  `online`, PID 2729, zero reinicializações. A unit systemd
  `turbobox-notifications.service` não existe; nenhum substituto foi criado.
- Timer de aviso de conexão permanece ativo/habilitado. Timer de extração
  permanece inativo/desabilitado. API/Admin, worker, biblioteca e configurações
  de produção foram preservados; ocorreram apenas as gravações normais do fluxo
  autorizado em outbox e fila.

### Registro de execução e conciliação

```text
Primeira verificação UTC−3: 08/09/2026 15:34:00–15:43:34
Continuação autorizada UTC−3: 08/09/2026 15:50:50–16:01:13
Commit/release realmente ativos: 353ab1d / extraction-353ab1d-20260908
API/Admin/PIX/gateway/Cloudflared: ativos, sem reinício nesta rodada
Rota pública inválida (HTTP/código): 400 NOTICE_INVALID, TLS válido, sem redirecionamento
Evento A: 29c1f7bc6e83; 3DS; categoria emulators
Evento A concluído/recebido UTC−3: 08/09/2026 12:10:19 / 15:20:43
Evento A estado inicial/final: PENDING / QUEUED; attempts=1; sem erro
Evento A finished_at UTC−3: 08/09/2026 15:54:52.694419
Destinatário A: proprietário ***7, últimos 4: 3513; AUTORIZADO
Elegibilidade A: 1 proprietário; pedido e compra paid; usuário active
Telefone A: normalização válida; destinatário não bloqueado; cadastro preservado
Oneshot A: exatamente 1 chamada de start; journal confirma início e conclusão com sucesso
Oneshot A InvocationID: 0774e4f4c9144819bcb696509f422b79
Job A: exatamente 1 registro, ID 73, sent, attempts=1, last_error vazio
Job A criado/enviado UTC−3: 08/09/2026 15:54:52 / 15:54:58
ID do provedor A: NULL no registro existente; resposta HTTP original não persistida
Recebimento A confirmado por: pendente; solicitado ao usuário na conversa
Deduplicação/reabertura A: 1 job nas consultas; reabertura real ainda não comprovada
Evento B: não disponibilizado; o segundo evento observado pertence à MESMA Conta A
Destinatário B autorizado: autorização geral vigente; segunda identidade não disponibilizada
Job B: teste B não executado
Recebimento B confirmado por: pendente
Isolamento A/B: aceite real pendente
Casos negativos: corpo {} rejeitado, sem evento adicional; demais casos reais pendentes
Timer antes/depois: disabled/inactive -> disabled/inactive
Quatro ciclos: não iniciados; dependem do aceite A/B e da habilitação pelo fluxo previsto
Verificação de 15 minutos: não iniciada; janela exigida após habilitação do timer
Verificação de 1 hora: não iniciada; janela exigida após habilitação do timer
Regressões dos serviços existentes: nenhuma observada nas verificações desta rodada
Estados finais: PENDING=1; LEASED=0; DISPATCHING=0; QUEUED=1; SKIPPED=0; UNCERTAIN=0; DEAD=0
Segredos/dados pessoais no Git: NÃO; somente os identificadores mascarados previstos no handoff
Resultado final: BLOQUEADO
Bloqueio atual: recibo no aparelho A, ID/conciliação externa, reabertura A, Conta B e demais critérios de aceite
Bloqueio por autorização: RESOLVIDO; não solicitar confirmação por evento
Reversão: não necessária
```

Os horários de `notification_jobs` são UTC; foram convertidos para UTC−3 acima.
O journal registra início às 15:54:52 e conclusão bem-sucedida da mesma invocação.
Após a conclusão, `systemctl show` retornou timestamps vazios; a prova da execução
usa o journal e a correlação com outbox/job, sem considerar somente o código de
saída do comando.

### Segundo evento recebido e seleção do disparo

Antes do envio, foi encontrado também o evento `6dbeb98929e7`, categoria
`ps-vita`, conteúdo `accel world vs sword art [PCSE01071].psvita`, recebido às
**15:49:05 UTC−3**. O relacionamento de compra resolve o mesmo proprietário
(`***7`) e WhatsApp final **3513**. Esse evento não satisfaz o teste de Conta B.

A primeira conferência parou ao detectar duas linhas. A leitura do Admin
confirmou `ORDER BY created_at LIMIT 1`, e o worker executa uma única obtenção
por invocação. Revalidado o 3DS como primeiro elegível, foi executada uma única
oneshot. Resultado conciliado: 3DS `QUEUED`, PS Vita `PENDING`, zero tentativas
e zero jobs para o protocolo `TS-6DBEB98929E7`. Nenhuma edição manual de linha,
lease ou telefone foi utilizada para selecionar o alvo.

O PS Vita está coberto pela autorização geral. Seu processamento permanece
pendente da conclusão da conciliação A prevista na etapa 5; não se deve reenviar
o 3DS para tentar obter um identificador externo.

### Limite encontrado na comprovação do provedor

A biblioteca instalada marca sucesso quando recebe HTTP 2xx sem erro de cURL
ou rejeição explícita. Ela captura somente `messageId` ou `id` no primeiro nível
JSON; o processador grava esse retorno em `api_message_id`. No job 73, o resultado
é `sent`, uma tentativa, erro vazio e **`api_message_id=NULL`**. O corpo original
não é persistido por esse processador, portanto não pode ser reconstruído desta
linha.

A documentação oficial de [envio de texto da MenuIA](https://docs.menuia.com/api-reference/create-message/mensagemTexto)
apresenta resposta de sucesso com `status` e `message`, sem um identificador.
Isso é compatível com o ID ausente observado, mas não prova qual foi o corpo da
resposta real. A documentação de [webhook de texto](https://docs.menuia.com/webhook-reference/events/texto)
prevê `idMessagem`; não foi localizada evidência desse webhook para o protocolo
nas pastas de logs existentes examinadas. Configuração de webhook e integração
foram preservadas.

A ausência de ID não foi tratada como motivo para reenviar. A confirmação no
aparelho continua necessária. Recuperar um ID exigirá evidência já existente no
provedor/webhook; alterar a biblioteca para ampliar o registro está fora dos
limites desta rodada. O aceite com identificador externo permanece em aberto.

### Testes concluídos e limites do aceite

As **15 verificações HTTP passaram novamente após o disparo**: saúde/prontidão/
conteúdo da API, saúde do PIX e gateway, contratos de sessão e extração locais e
públicos, desafios Suite/EmulationStation/escopo inválido. Pelo socket Admin,
`/health`, `/readiness` e `/readiness/content` responderam 200;
`/extraction-notifications/begin-dispatch` com `{}` respondeu 400.

A outbox conserva os dois eventos reais; os corpos inválidos não criaram outro.
O job A foi conciliado por tipo `suite_extraction_completed`, proprietário e
protocolo `TS-29C1F7BC6E83`. A mensagem contém 3DS e o protocolo esperado; as
checagens não encontraram IP, MAC, telefone completo, caminho local ou marcador
de credencial. O texto integral permanece privado.

Login autenticado, heartbeat real, reabertura Windows com `ALREADY_ACCEPTED`,
arquivo bruto sem extração, falha opcional preservando download e limpeza de
`.turborama-downloads` ainda não foram exercitados pelo cliente nesta rodada.
Os testes sintéticos anteriores permanecem no handoff-base. Não houve simulação
de falha no provedor real nem alteração de cadastro para fabricar casos negativos.
Ausência/ambiguidade/bloqueio de destinatário como testes e isolamento real com
duas contas continuam pendentes.

### Evidência privada e ponto de retomada

Diretório privado, permissão `0700`:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/closeout-20260908T183400Z/`.

Além dos registros da primeira verificação, esta continuação produziu:
`authorization-a.json`, `oneshot-a-invocation.json`,
`oneshot-a-journal-masked.json`, `reconcile-a.json`, `event-owners-masked.json`,
`message-a-validation.json`, `provider-id-diagnosis.json`,
`public-smoke-after-dispatch.json` e `authorized-final-check.json`.
Identificadores integrais e registros privados de conciliação não foram
publicados no Git.

Retomar na **etapa 6**, com a autorização geral já concedida. Confirmar no aparelho
uma única mensagem 3DS, conciliar eventual evidência externa e realizar a
reabertura A solicitada na conversa. O usuário também foi orientado a realizar
uma extração em segunda conta paga e informar conteúdo/horário. Revalidar o
estado antes de qualquer próxima oneshot. Habilitar o timer existente somente
após os critérios das etapas 7 e 8 e observar quatro ciclos, 15 minutos e 1 hora.
Atualizar este mesmo retorno conforme chegarem as comprovações.
