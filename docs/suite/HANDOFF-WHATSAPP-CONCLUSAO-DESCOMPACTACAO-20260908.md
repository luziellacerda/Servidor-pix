# Handoff único — WhatsApp na conclusão da descompactação

Data: 08/09/2026. Destino: responsável pelo servidor Linux do TurboRama Suite.

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

## Retorno Linux — qualificação de 08/09/2026

Fonte recebida e lida integralmente: `099905a10da70e37ce08555b42b4e3c9044285e1`.
O trabalho Linux segue em `codex/emulationstation-suite-extraction-linux-20260908`,
partindo dessa versão. Este registro será atualizado ao concluir a qualificação.

### Correção encontrada na execução nativa

O primeiro evento válido, assinado por uma identidade sintética e processado com
as permissões da API em PostgreSQL 16, retornava `503 NOTICE_UNAVAILABLE`.
A causa era a concatenação do `INSERT`: a string C# terminava em `WHERE` e a
constante seguinte começava com `license_id`, formando `WHERElicense_id`.
O PostgreSQL rejeitava o comando com `42601`. Foi acrescentado um separador
explícito. O teste PGlite anterior mantinha as quebras de linha externas da
string C# e escondia esse defeito; sua extração também foi corrigida.

O worker agora verifica o bloqueio de destinatários da biblioteca instalada
antes de iniciar o despacho. A consulta mantém compra paga nas duas tabelas,
usuário ativo e exatamente um proprietário. Não há mudança na política de
bloqueio existente nem inclusão de destinatários no código.

### Evidência local concluída

- Backup completo do PostgreSQL em formato custom, com manifesto SHA-256;
  restauração integral em PostgreSQL 16 isolado concluída sem erro.
- Migration 026 livre na produção; aplicada somente na homologação, tanto em
  banco vazio com 001–025 quanto na restauração do schema real.
- View, permissões mínimas e plano da consulta revisados no schema restaurado.
- Novo teste nativo usa duas identidades RSA, conta paga e ativa, grant concluído
  e as roles da API/Admin. HTTP `202 ACCEPTED`, `200 ALREADY_ACCEPTED`, assinatura
  inválida, sessão de outra conta, compra suspensa, dispositivo revogado e
  sessão/evento vencidos: PASS.
- 24 repetições concorrentes mantêm uma linha; 20 eventos distintos adicionais
  e um evento da segunda conta completam 22 registros sem perdas: PASS.
- Admin completo pelo socket autenticado: 30 requisições concorrentes obtêm
  22 leases distintos; token antigo não confirma novo lease; despacho expirado
  vira `UNCERTAIN`, sem reciclagem, e ACK tardio confirmado é aceito: PASS.
- Regressões existentes Suite e ES em PostgreSQL 16: PASS.
- PHP com fila falsa e SQLite PDO nativo: duas contas, isolamento, compra/conta,
  telefone, bloqueio e proprietário ambíguo: PASS.
- Contrato da biblioteca realmente instalada exercitado separadamente com
  SQLite em memória, sem carregar provedor ou conectar à fila real: PASS.

Os testes nativos acessam apenas bancos locais com nomes de CI permitidos.
Os dados de clientes restaurados não são usados pelos aplicativos de teste.

### Significado da fila instalada

`tb_queue_whatsapp` aceita os quatro argumentos e o tipo novo. Retorna `true`
quando insere em `notification_jobs` ou encontra o mesmo tipo, telefone e texto
nos dez minutos anteriores. O processo existente `turbobox-notifications`
entrega ao provedor e registra `sent`, identificador e horário quando há sucesso.

A proteção `UNCERTAIN` da nova outbox cobre o trecho até a entrada na fila.
O processador legado ainda repete erros/timeouts do provedor. Portanto não há
comprovação de entrega nem garantia de envio externo único com base apenas em
`QUEUED`; é necessário conciliar o resultado com o provedor antes de reenviar.

### Situação de publicação neste ponto

A CI recebida da fonte falhou na carga prolongada de 1.000 sessões: 80 respostas
504 em 72.000 requisições na fase soak. O upload de diagnóstico também informou
cota de armazenamento de artefatos excedida. A correção não reduz a carga nem
relaxa limites ou critérios. A nova execução será registrada abaixo.

Produção ainda não alterada neste ponto: API em
`es-reopen-efaf1d3-20260905`, Admin em `es-suite-34e31f2-20260905`, migration 026
não aplicada, timer novo não instalado. Nenhuma mensagem real foi disparada.
As duas contas e os destinatários autorizados para o teste Windows/WhatsApp
foram solicitados ao responsável; a confirmação continua pendente.
