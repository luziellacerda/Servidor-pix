# Handoff único — WhatsApp na conclusão da descompactação

Data: 08/09/2026. Destino: responsável pelo servidor Linux do TurboRama Suite.

> **Retorno Linux de 08/09/2026:** API/Admin instalados, migration 026 aplicada e
> rota pública ativa. Timer de extração instalado e parado. Entrega real de
> WhatsApp depende das duas contas/destinatários autorizados. O estado atual e as
> evidências estão em [Retorno Linux](#retorno-linux--estado-atual).

## Resultado esperado

Quando o programa Windows concluir a extração, verificar/publicar os arquivos no
destino e mudar o estado existente para **ABRIR PASTA ✓**, ele registra um evento
local protegido e o envia à API Suite. O servidor valida conta, dispositivo,
sessão, compra e conteúdo; depois resolve o proprietário e usa a mesma fila
WhatsApp do aviso de login.

O clique em **ABRIR PASTA** não é o gatilho. A limpeza de
`.turborama-downloads` também não é o gatilho. A transição interna
`Extracting → Completed`, que disponibiliza `CanOpen`, é o momento definido pelo
proprietário.

## Diagnóstico confirmado no PC

- EXE testado: TurboRama 2.0.2, candidato local sem Authenticode.
- Pacote: `TESTE-GATILHO-20260908-1218`.
- SHA-256 do EXE: `F71D9C7C193476F84359198B07649FFE4A91E66F2981CF64476EC0375CA634E7`.
- O 3DS chegou a `Download e extração concluídos` e `ABRIR PASTA ✓`.
- Existe aviso de 3DS/Emuladores protegido por DPAPI e pendente fora do cache de
  download. A limpeza não o apagou.
- Em duas verificações sem credenciais, com corpo deliberadamente inválido `{}`,
  `POST https://app.lzgames.com.br/v1/suite/sessions` respondeu
  `400 JSON_INVALID`, enquanto
  `POST https://app.lzgames.com.br/v1/suite/notifications/extraction-completed`
  respondeu `404`. O certificado TLS foi validado; não houve bypass ou redirect.
- Nenhum evento válido foi enviado nesses diagnósticos e nenhuma mensagem real
  foi disparada. A resposta 404, isoladamente, pode significar release antiga,
  feature desligada ou rota ausente no proxy. Confirmar no servidor nessa ordem.

Conclusão: o cliente alcança o estado solicitado e mantém o aviso pendente. O
bloqueio atual para WhatsApp é a indisponibilidade pública da integração de
servidor. Não corrigir alterando IP do cliente, apagando identidade/licença,
clicando no botão ou desativando a limpeza.

## Base e escopo desta branch

Esta branch parte de
`eb522526547be876982f3fabb79f59fefb8fb702`, linha
`codex/emulationstation-suite-v1-20260905`. A árvore `main` PIX legada estava em
`a52b9237642ae9150d8030e9fa12513193bad8ee` durante a preparação.

**Não substituir a árvore de produção inteira por esta base.** Primeiro registrar
o commit realmente instalado e integrar somente o delta desta branch. Preservar
correções posteriores, Suite, PIX, EmulationStation, conteúdo, painel, login,
túnel, certificados, chaves, bancos, workers e flags já ativos.

Mudanças do servidor:

- `src/TurboRamaSuiteNotifications/`: protocolo assinado e dez modelos de texto.
- `src/TurboRamaSuiteOnlineServer/ExtractionNotificationEndpoints.cs`: rota
  pública autenticada e inserção idempotente na outbox.
- `src/TurboRamaSuiteAdminServer/ExtractionNotificationAdminEndpoints.cs`:
  lease, fronteira de despacho e resultado pelo socket Admin já autenticado.
- `migrations/suite/026_suite_extraction_notifications.up.sql`: view, outbox,
  índices, permissões mínimas e marcador, somente expand-forward.
- `ops/production/turborama-suite-extraction-whatsapp.php`: worker separado que
  reutiliza `tb_queue_whatsapp` com quatro argumentos.
- exemplos `.service`/`.timer`; não instalar sem substituir o caminho placeholder.
- testes .NET, HTTP loopback, PHP/fila falsa e SQL; workflow ampliado para executá-los.

O worker de conexão existente não foi alterado. A API interna MenuIA não é usada
por esse fluxo. Nenhum segredo ou número de telefone foi incluído no Git.

## Contrato de segurança

1. O EXE não envia nome nem telefone. Assina o evento com a mesma identidade RSA
   registrada e com a sessão atual, usando domínio criptográfico próprio.
2. A API exige licença/dispositivo/sessão ativos, direito de catálogo, entrega
   paga/provisionada e grant `COMPLETED` recente do item/artefato.
3. O nome do conteúdo vem do catálogo. A categoria usa lista fixa. O proprietário
   vem da compra paga: `payment_orders → purchases → users`.
4. O texto não contém IP, MAC, telefone, licença completa, identificador completo
   do dispositivo nem caminho local.
5. A mesma identidade de conteúdo/versão/manifesto/hash é deduplicada. Rebaixar o
   mesmo conteúdo não cria outro aviso enquanto o evento existir.
6. `ACCEPTED` significa evento persistido; `QUEUED` significa aceito pela função
   da fila. Nenhum dos dois comprova entrega ou leitura no WhatsApp.
7. Antes da chamada de fila, o servidor grava `DISPATCHING`. Uma resposta perdida
   ou resultado ambíguo vira `UNCERTAIN` e não é reenviado automaticamente. Isso
   reduz duplicação, mas exige conciliação operacional.
8. Falha de notificação nunca desfaz o download concluído, o login ou a limpeza.

## Dependência que deve ser confirmada no Linux

O login já chama:

```php
tb_queue_whatsapp($customerId, 'suite_device_connected', $phone, $message)
```

O worker novo usa o mesmo contrato:

```php
tb_queue_whatsapp($customerId, 'suite_extraction_completed', $phone, $message)
```

A implementação de `notification-lib.php` não está neste repositório. Antes de
ativar, confirmar na cópia instalada:

- se aceita o tipo `suite_extraction_completed`;
- o significado exato do retorno booleano;
- onde a fila é persistida e qual worker entrega ao provedor;
- como conciliar jobs já aceitos pelo provedor após timeout;
- PHP, cURL, PDO, permissões e diretório de dados usados pelo serviço atual.

Não inventar quinto argumento de idempotência e não copiar tokens para o EXE.

## Ordem única de implantação

1. Registrar commit/release, hashes, units, migrations e flags atualmente em
   produção. Fazer backup verificável do banco e preservar a release anterior.
2. Conferir se `026_suite_extraction_notifications` ainda está livre. Se houver
   outro 026, parar e renumerar/revisar; nunca sobrescrever migration aplicada.
3. Integrar somente este delta sobre a árvore instalada e aguardar a CI desta
   branch. O push não autoriza implantação automática.
4. Em homologação com PostgreSQL 16 e schema real, aplicar todas as migrations
   anteriores e a 026. Conferir a view, roles e plano de consultas. A migration é
   expand-forward e não altera linhas de licenças, sessões, grants ou avisos de login.
5. Conferir `notification-lib.php` e executar o worker novo com fila/provedor
   falsos. Depois usar somente um destinatário explicitamente autorizado.
6. Publicar API Suite e Admin completos em release imutável, incluindo
   `TurboRamaSuiteNotifications.dll`, `.deps.json` e dependências. Não copiar só
   uma DLL para a release atual.
7. Manter a feature desligada e validar `/health`, `/ready`, login, heartbeat,
   download, PIX, ES, painel e o aviso de conexão já existente.
8. Confirmar que o túnel/proxy encaminha a nova rota POST dentro do prefixo
   `/v1/suite/`. Não expor socket Admin, token ou banco publicamente.
9. Habilitar na API `Suite__ExtractionNotifications__Enabled=true` e no Admin
   `SUITE_EXTRACTION_NOTICES_ENABLED=1`, preservando todas as outras flags.
10. Adaptar e validar as units de exemplo. O caminho
    `/opt/VERIFIED-EXTRACTION-NOTICES` é placeholder obrigatório. Instalar o novo
    timer separadamente; não substituir/desativar o timer de conexão.
11. Verificar que o POST público deixa de retornar 404. Corpo inválido deve
    retornar 400; evento válido autorizado deve retornar 202/200 e criar uma linha.
12. Com uma conta de teste autorizada, concluir uma extração nova, confirmar o
    destinatário resolvido pelo servidor e acompanhar fila/provedor/WhatsApp.
13. Repetir com segunda conta, reabertura do programa, falha simulada e mesmo
    artefato. Confirmar isolamento, retomada e deduplicação antes de liberar.

Não usar `apply-suite-content-migrations.sh` antigo como instalador desta feature:
ele foi criado para a baseline 010–016 e não contém a 026.

## Critérios de aceite

- Login, heartbeat, conteúdo, PIX e ES continuam com o comportamento anterior.
- Rota desligada retorna 404; ligada rejeita JSON inválido, campo desconhecido,
  corpo acima de 16 KiB e prova inválida.
- Somente contexto ativo e pago cria evento; outra conta/dispositivo não acessa.
- Um evento repetido retorna `ALREADY_ACCEPTED`, sem nova linha/mensagem.
- Worker identifica exatamente um proprietário ativo e telefone válido.
- Mensagem tem `LZ GAMES | TURBORAMA SUITE`, saudação UTC−3, conteúdo,
  categoria, integridade, gravação final, horário e protocolo; sem IP/MAC.
- Falha antes do despacho pode tentar novamente; estado ambíguo depois do início
  fica `UNCERTAIN` e não volta automaticamente à fila.
- A entrega real é confirmada no provedor, não inferida de HTTP 2xx/`QUEUED`.

## Diagnóstico após implantação

```sql
SELECT status, count(*)
FROM suite.suite_extraction_notification_outbox
GROUP BY status ORDER BY status;

SELECT last_error_code, count(*)
FROM suite.suite_extraction_notification_outbox
WHERE last_error_code IS NOT NULL
GROUP BY last_error_code ORDER BY last_error_code;
```

- Sem evento: verificar rota/flags, sessão, grant recente e logs sem dados pessoais.
- `PENDING`/`LEASED`: timer, Admin, socket/token e lookup da compra.
- `SKIPPED`: dono ausente/ambíguo, compra/conta ou telefone inválido.
- `UNCERTAIN`: consultar fila/provedor antes de qualquer ação.
- `QUEUED` sem entrega: investigar worker/provedor; não reenfileirar cegamente.

## Reversão sem destruir o existente

1. Desabilitar/parar somente o timer novo.
2. Desligar as duas flags novas.
3. Se necessário, restaurar API/Admin para a release anterior completa pelo
   procedimento existente, preservando todas as configurações anteriores.
4. Manter view, tabela, eventos e marcador da migration para diagnóstico e futura
   retomada. Não executar `DROP`, migration down, reset de banco, exclusão de
   licenças/sessões/compras/grants nem recriação da identidade do cliente.
5. Conciliar `UNCERTAIN` com a fila/provedor antes de reativar. A mensagem pode já
   ter sido aceita externamente.

## Evidência antes deste push

- API/Admin e testes .NET 8: zero erros e zero avisos.
- Protocolo, dez mensagens, horários UTC−3, sanitização e ausência de IP/MAC: PASS.
- HTTP loopback: feature off, JSON/campo proibido e corpo fixo/chunked: PASS.
- Regressões Suite/login/replay/heartbeat concorrente e ES em memória: PASS.
- PHP: sintaxe e cenários com fila falsa, incluindo resposta perdida: PASS.
- SQL: migration e consultas extraídas do C# em PGlite, incluindo roles,
  deduplicação, hash DIRECT nulo e fencing de leases: PASS.
- Cliente Windows: pipeline completo, gatilho `ABRIR PASTA ✓`, DPAPI, limpeza
  independente, retomada/publicação e manifesto/SHA-256: PASS.

Pendências deliberadas: CI remota desta branch, PostgreSQL 16 do workflow, schema
real de homologação, contrato da biblioteca instalada, carga concorrente da nova
fila e entrega WhatsApp ponta a ponta.

## Retorno Linux — estado atual

Fonte lida integralmente: `099905a10da70e37ce08555b42b4e3c9044285e1`.
Retorno no [PR #2](https://github.com/luziellacerda/Servidor-pix/pull/2), branch
`codex/emulationstation-suite-extraction-linux-20260908`.
A seção original acima registra o que foi recebido; esta seção informa o que
foi executado e o que ainda precisa de validação externa.

### O que está instalado

- Release completa: `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`.
- Commit operacional: `353ab1d729ad625a986c96f85f3afa4a306cc1dd`.
- API e Admin incluem `TurboRamaSuiteNotifications.dll`, `.deps.json`, runtime
  config e todas as dependências publicadas. Não foi substituída DLL avulsa.
- API: `Suite__ExtractionNotifications__Enabled=true`.
- Admin: `SUITE_EXTRACTION_NOTICES_ENABLED=1`.
- Migration `026_suite_extraction_notifications`: aplicada, um marcador,
  view/outbox/índices presentes e permissões das duas roles conferidas.
- Timer `turborama-suite-extraction-whatsapp.timer`: instalado, **inativo e sem
  habilitação automática**. O timer de aviso de conexão continua ativo.
- Nenhuma mensagem real de extração foi enviada por esta intervenção. Na
  conferência final a outbox de extração estava vazia.

A nova versão foi iniciada primeiro com as duas flags desligadas. Após verificar
saúde, readiness, conteúdo e contratos Suite/ES existentes, foram habilitadas
somente a recepção dos eventos e as operações Admin. O envio permanece pendente
do teste com destinatários autorizados. Isso não é aceite ponta a ponta completo.

### Por que a notificação não funcionava

Havia dois problemas distintos:

1. A versão instalada anteriormente não disponibilizava a integração nova:
   `POST /v1/suite/notifications/extraction-completed` respondia 404, enquanto a
   rota de sessões já era alcançável. Após a instalação e ativação, o mesmo POST
   público com `{}` passou a responder 400. O caminho do túnel/proxy já atendia
   ao prefixo; não foi necessário mudar DNS, IP ou porta.
2. O teste nativo encontrou um defeito no código recebido: o `INSERT` concatenava
   `WHERE` e `license_id` sem espaço, formando `WHERElicense_id`. Uma prova
   válida resultava em `503 NOTICE_UNAVAILABLE`, com erro PostgreSQL `42601`.
   Foi acrescentada uma quebra de linha explícita. O teste PGlite anterior
   preservava quebras externas que o C# remove e escondia o erro; essa extração
   também foi corrigida. O novo teste executa o C# real com PostgreSQL 16.

Esses fatos explicam a indisponibilidade desta integração de extração. Não são
prova de mudança de chave, banco, DNS ou porta causada pela troca da placa-mãe,
nem explicam retroativamente todos os incidentes anteriores de licenciamento.

### Como o fluxo foi integrado

```text
Windows: Extracting → Completed / ABRIR PASTA ✓
  → evento local DPAPI, fora do cache de download
  → prova assinada enviada por HTTPS à API Suite
  → validação de identidade, sessão, compra e grant COMPLETED
  → outbox PostgreSQL, com identificação determinística do evento
  → worker PHP obtém lease no socket Admin autenticado
  → compra paga → proprietário ativo → telefone normalizado
  → Admin persiste DISPATCHING e monta o texto
  → tb_queue_whatsapp grava na fila TurboBox existente
  → processo turbobox-notifications entrega ao provedor
```

O worker foi ajustado para reconhecer a política de destinatários bloqueados da
biblioteca instalada antes de iniciar o despacho. A consulta exige compra paga
nas duas tabelas, usuário ativo e exatamente um proprietário. Não foi alterada
a política de bloqueio e nenhum telefone foi incluído no Git.

O tipo novo é aceito por `tb_queue_whatsapp`, usando exatamente quatro argumentos.
O retorno `true` significa inserção em `notification_jobs` ou detecção do mesmo
tipo, telefone e texto nos dez minutos anteriores. Não significa entrega.
O processo existente registra `sent`, identificador do provedor e horário quando
recebe confirmação. A biblioteca original permaneceu com o mesmo SHA-256.

A proteção `UNCERTAIN` da nova outbox cobre a fronteira até a fila. O processador
legado ainda repete erros/timeouts do provedor. Portanto, após resultado externo
ambíguo, consultar fila e provedor antes de reenviar; não há garantia demonstrada
de entrega externa exatamente uma vez. O hash do arquivo no evento é uma
afirmação assinada do cliente; em modo DIRECT o grant não contém hash autoritativo
para o servidor conferir independentemente.

### Portas e serviços neste fluxo

| Componente | Acesso utilizado | Situação |
|---|---|---|
| Aplicativo Windows | HTTPS `app.lzgames.com.br:443` | Mesmo domínio e túnel |
| API Suite | `127.0.0.1:5190` | Nova release; saúde/readiness 200 |
| Admin Suite | Socket `/run/turborama-suite-admin/admin.sock` | Autenticado; sem porta pública nova |
| PIX | `127.0.0.1:5187` | Mesmo processo; saúde 200 |
| Gateway de conteúdo | `127.0.0.1:5191` | Mesmo processo; saúde 200 |
| Dados da nova outbox | PostgreSQL 16 local, banco `postgres`, schema `suite` | Somente migration 026 adicionada |
| Fila WhatsApp | SQLite TurboBox existente | Mesmo contrato e processador |

Não houve alteração de DNS, túnel, Nginx, certificados, senhas, chaves,
identidades do cliente ou configurações anteriores. Foram comparados conteúdo,
permissões e proprietários de 29 arquivos protegidos. PIX, gateway, Nginx e
Cloudflared mantiveram os mesmos InvocationIDs. API/Admin tiveram reinício
controlado e ficaram ativos, sem reinícios automáticos por falha.

### Testes e limites das evidências

| Verificação | Resultado |
|---|---|
| Protocolo, RSA, JSON estrito, horários, dez textos e privacidade | PASS |
| Rota desligada, JSON/campo inválido e limite de 16 KiB fixo/chunked | PASS |
| Migration 001–026 em banco vazio PostgreSQL 16 | PASS |
| Backup real restaurado em PG16 e migration 026 sobre o schema restaurado | PASS |
| Roles, view e plano de consulta no schema restaurado | PASS |
| Duas identidades sintéticas, provas válidas, compra/sessão/dispositivo e isolamento | PASS |
| HTTP nativo `202 ACCEPTED`, repetição `200 ALREADY_ACCEPTED` | PASS |
| 24 repetições concorrentes e 22 eventos distintos no total | PASS |
| Admin real pelo socket; 30 consumidores, 22 leases distintos | PASS |
| Lease antigo, retomada antes do despacho, UNCERTAIN e ACK tardio | PASS |
| PHP com fila falsa, SQLite nativo, dono ausente/ambíguo e bloqueios | PASS |
| Funções reais da biblioteca instalada com SQLite em memória, sem provedor | PASS |
| Regressões Suite e ES em PostgreSQL 16 | PASS |
| Unidades systemd e manifesto da release instalada | PASS |
| Produção: 15 verificações de saúde/contratos com flags OFF e novamente ON | PASS |
| Produção: Admin autenticado com health/readiness/conteúdo 200 | PASS |
| Produção: POST público inválido da nova rota muda de 404 para 400 | PASS |
| Windows real + duas contas + provedor/WhatsApp + reinício do aplicativo | PENDENTE |

Os testes de aplicativo usam somente bancos de CI com identidades sintéticas.
Os dados de clientes do backup restaurado não foram usados por aplicativos ou
workers de teste. O plano no schema restaurado usa o índice do catálogo e
varreduras em tabelas pequenas; essa inspeção não é prova de capacidade em escala.

A carga local passou com 500 e 1.000 sessões: 72.000 HTTP 200 no soak, duração
180,3 s, p95 505 ms, p99 920 ms, máximo 1.297 ms. API e gerador compartilham um
container limitado a quatro CPUs, com pool explícito de 20 conexões. É HTTP
loopback, sem túnel/TLS público, Windows ou provedor WhatsApp.

A [CI do candidato operacional](https://github.com/luziellacerda/Servidor-pix/actions/runs/34258424808)
passou por todos os testes e pela compilação/publicação de `dist` no runner.
Sua carga manteve o pool padrão de 8 conexões: 72.000 HTTP 200, p95 5.708 ms e
máximo 6.890 ms. A etapa nominal de 180 s demorou 347 s; portanto não demonstra
sustentar 400 requisições/s nesse ambiente. As configurações local e remota não
são equivalentes. Nenhum pool de produção foi alterado.

O resultado geral dessa CI ficou **vermelho exclusivamente nos uploads**, por
`Artifact storage quota has been hit`. O pacote não foi armazenado no GitHub.
A implantação usou publicação local completa, verificada por manifesto.
Não foram apagados artefatos de outros trabalhos. A CI `validar-servidor` do
mesmo commit também passou. A execução recebida em `099905a` tivera 80 respostas
504 no soak; a causa específica dessa variação entre runners não foi comprovada.

### Correções complementares de CI

O workflow `suite-candidate`, acionado pelo PR, também precisava de atualização:
três espaços ausentes no gateway foram corrigidos; seu schema de teste parava
na migration 014, embora monitor e catálogo atuais exijam 902 itens (016).
Além disso, fixtures antigas ainda gravavam tamanho/hash, proibidos no modo
DIRECT desde a 015.

O workflow agora aplica 001–026, mantendo a verificação dos hashes fixados das
migrations de conteúdo. As fixtures de monitor, catálogo e grants usam NULL
para tamanho/hash, e a verificação de contexto exige explicitamente esse
contrato. Monitor nativo, matriz de permissões, quotas, retenção e rotação de
sessão passaram em banco PG16 vazio separado. As regras do banco não foram
relaxadas. Esses ajustes afetam a qualificação; monitor e gateway de produção
não foram substituídos. As execuções complementares podem ser acompanhadas nos
checks do PR #2.

### Hashes, backup e retomada

| Artefato instalado | SHA-256 |
|---|---|
| API `TurboRamaSuiteOnlineServer.dll` | `9ca5b96ace51bec8abe8d2efdf2b00e68dbfd70d9f1a77cd2e58b73d3588577b` |
| Admin `TurboRamaSuiteAdminServer.dll` | `f8b616555887a5c20f8326ab148011fcdfcc346461e03d0ce3e574d4aaca3ae9` |
| `SHA256SUMS.txt` da release | `f822ee278de21b4d1b9704a3ea3bb5c660dc40efefca329da90b5226e265f285` |
| Migration 026 | `96512c3bc4631bd47290ce42ff50378b70d353f4febd924eda5ea26af3e3a7bf` |

Evidência privada no servidor:
`/home/lz-servidor/evidence/extraction-whatsapp-20260908/` (diretório 0700).
Contém backup custom do PostgreSQL, restauração verificada, backup novo imediatamente
antes da implantação, configurações protegidas, planos, logs e manifestos.
Dumps, credenciais, conteúdo de env e dados pessoais não foram enviados ao Git.

Arquivos principais: `production-final.json`, `production-migration-026.json`,
`production-notification-state.json`, `ci-qualification.json`,
`public-smoke-feature-off.json`, `public-smoke-feature-on.json`,
`candidate-manifest.json` e `load/es-session-load.json`.
Retomada local: `/home/lz-servidor/RETOMAR-REPARO-TURBORAMA-PIX-20260908.md`.

### Reversão preparada

Os drop-ins novos são `zzzzzz-extraction-20260908.conf` (release/flags OFF) e
`zzzzzzz-extraction-enable-20260908.conf` (flags ON), nas pastas dos dois serviços.
Os anteriores foram preservados, inclusive configuração e credencial do pepper.
A reversão com verificações de conteúdo/caminhos está no arquivo privado
`deploy-control.py`; executar como root no próprio servidor:

```bash
python3 /home/lz-servidor/evidence/extraction-whatsapp-20260908/deploy-control.py rollback
```

Ela para/desabilita somente o timer novo, remove somente os drop-ins novos
conferidos e reinicia API/Admin nas releases anteriores completas:
API `es-reopen-efaf1d3-20260905` e Admin `es-suite-34e31f2-20260905`.
Mantém a release nova para evidência, a migration 026, a view, a tabela e eventos.
Não executa DOWN, DROP, reset, exclusão de licença ou recriação de identidade.
A rotina foi preparada; não foi provocado rollback em produção só para testá-la.

### O que falta para o aceite completo

1. Informar duas contas de teste e os destinatários WhatsApp autorizados; não
   enviar senha, OTP ou chave. Essa informação foi solicitada e não recebida.
2. No Windows, concluir extração nova com o pacote identificado na origem deste
   handoff, confirmar 202/200 e a entrada correspondente na outbox.
3. Conferir que o proprietário resolvido e os eventos pendentes pertencem ao
   escopo autorizado antes de iniciar o novo timer. Acompanhar a mesma fila e
   obter confirmação do provedor e do recebimento no WhatsApp.
4. Repetir com a segunda conta, reabertura, falha simulada e mesmo artefato.
   Só então concluir aceite e habilitar o timer para operação contínua.

Não tratar o 400 da sondagem inválida, os testes sintéticos, `ACCEPTED` ou
`QUEUED` como comprovação dessa entrega real. O estado atual é integração de
servidor instalada e verificada, com despacho e aceite externo ainda pendentes.
