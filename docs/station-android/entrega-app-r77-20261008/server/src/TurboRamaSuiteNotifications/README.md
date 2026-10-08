# WhatsApp após descompactação — integração e implantação segura

Estado em 08/09/2026: **cliente Windows compilado; integração de servidor implementada e testada localmente; não implantada em produção**. Este documento substitui a pendência anterior que entregava somente o formatador. Nenhuma mensagem real foi enviada e nenhum serviço de produção foi reiniciado nesta etapa. O candidato é versionado apenas na branch de handoff; isso não equivale a implantação.

## Causa e caminho reaproveitado

O EXE anterior tinha a limpeza do cache, mas não comunicava a conclusão à API. O formatador isolado não executava envio. Portanto, sua presença não ativava o WhatsApp.

O aviso de login já usa este caminho:

1. O serviço Suite registra o acesso em `suite.suite_connection_notification_outbox`.
2. O worker `ops/production/turborama-suite-connection-whatsapp.php` consome as rotas administrativas de conexão pelo socket Unix autenticado.
3. Ele resolve o proprietário pela compra paga: `payment_orders.public_id → purchases.user_id → users`, exigindo pedido/compra pagos e usuário ativo.
4. Ele chama a biblioteca TurboBox existente: `tb_queue_whatsapp(customerId, tipo, telefone, mensagem)`.

A nova integração reutiliza esse contrato de QUATRO argumentos e a mesma resolução de conta. Não envia diretamente pela API MenuIA, não coloca token administrativo no EXE e não altera o worker de login. A implementação de `notification-lib.php` não está neste repositório: seu suporte ao tipo novo deve ser confirmado na homologação. Não foi presumido argumento adicional de idempotência.

## Fluxo implementado

`Extração + publicação local verificadas → aviso local protegido → API Suite autenticada → outbox do servidor → worker → fila WhatsApp existente`.

- Cliente: na transição `CanOpen: false → true` que disponibiliza o botão existente `ABRIR PASTA ✓`, durante `CompleteExtraction` no destino final. O observador é inscrito somente no bloco de conclusão verificada, exige `Extracting → Completed` e se desinscreve antes do callback. Não há listener de clique, comparação do texto ou dependência da limpeza. Percentual 100%, autorização/redirect, restauração de item pronto e arquivo bruto mantido sem extração não disparam este aviso.
- A prova usa a chave RSA-PSS/SHA-256 já registrada no login, a sessão atual, identidade do conteúdo, hash e horário. Há um domínio criptográfico próprio; os bytes dos protocolos existentes não foram alterados.
- Rota pública: `POST /v1/suite/notifications/extraction-completed`, na MESMA autoridade HTTPS configurada e com os pins existentes. Nada de novo IP fixo, exceção de certificado, mudança de túnel ou configuração individual de cliente.
- O aviso local fica protegido por DPAPI CurrentUser em `%LocalAppData%/Turborama/Suite/notifications/<hash-conta-dispositivo>`, fora de `.turborama-downloads`. O envio HTTP não bloqueia a conclusão ou o login. A limpeza do cache continua independente.
- API valida a assinatura com a chave registrada, sessão vigente, licença/dispositivo ativos, entrega paga provisionada, direito de catálogo e concessão de conteúdo concluída recente. Nome do conteúdo vem do catálogo; categoria é um identificador assinado do cliente traduzido por lista fixa do servidor, não texto livre.
- No modo DIRECT, desde a migration 015, o hash da concessão é intencionalmente NULL. A validação respeita esse contrato. O servidor autentica uma declaração de conclusão feita pelo cliente; não inspeciona remotamente os arquivos do Windows.
- O servidor escolhe e persiste uma das dez aberturas. O worker resolve nome/telefone pelo dono da compra; o EXE não escolhe destinatário.
- Rotas administrativas novas: `/extraction-notifications/lease`, `/begin-dispatch` e `/complete`, sob o mesmo prefixo `/extraction-notifications`, middleware, token protegido e socket existentes.

## Garantias e limites explícitos

- Sem IP, MAC, telefone, licença completa, identificador do dispositivo ou caminho local no TEXTO da mensagem. A API recebe os identificadores necessários à autenticação; telefone fica apenas no adaptador de entrega.
- Saudação calculada na preparação para envio em UTC−3: bom dia 05:00–11:59, boa tarde 12:00–17:59, boa noite nos demais horários. A data de conclusão exibida é separada. Se a fila do provedor atrasar, a saudação não é recalculada na entrega.
- Marca `LZ GAMES | TURBORAMA SUITE`, corpo técnico, emojis e assinatura `Equipe LZ Games` preservados.
- Deduplicação atual: um evento por licença + dispositivo + item/artefato/versão + identidade do manifesto + hash. Repetir o mesmo conteúdo, inclusive em novo download da mesma versão, não produz outro aviso enquanto o evento existir no servidor.
- Janela de aceitação e manutenção de aviso local pendente: sete dias; concessão do conteúdo também deve estar dentro de sete dias. Após esse prazo, somente o arquivo local reconhecido/protegido do evento é removido; arquivos desconhecidos não são apagados.
- Até 60 novos eventos/hora por licença e oito tentativas de obtenção de trabalho antes de esgotamento. Eventos pendentes dependem de sessão autorizada e programa aberto para transmissão; retornam no próximo login dentro da janela.
- `ACCEPTED/ALREADY_ACCEPTED` significam registro durável no servidor; `QUEUED` significa aceitação pela função da fila. Nenhum desses estados comprova entrega ou leitura no WhatsApp.
- O servidor persiste `DISPATCHING` antes da chamada de fila. Se houver resposta perdida, exceção ou resultado ambíguo após essa fronteira, o evento fica `UNCERTAIN`, sem reenvio automático. Isso evita duplicação automática, mas exige conciliação operacional e pode deixar um aviso sem envio. Não há promessa de exactly-once no provedor.
- Tokens de lease impedem confirmação por worker antigo. Somente falha anterior ao despacho pode voltar automaticamente para `PENDING`.

## Arquivos e preservação

Servidor candidato: worktree separado, base `eb522526547be876982f3fabb79f59fefb8fb702`, linha Suite/ES. **Não substituir a main PIX legada nem a árvore atual de produção por esta base antiga.** Transportar apenas este delta após comparar com a versão realmente instalada.

- `src/TurboRamaSuiteNotifications/`: protocolo e formatação.
- `src/TurboRamaSuiteOnlineServer/ExtractionNotificationEndpoints.cs`: recepção autenticada/outbox.
- `src/TurboRamaSuiteAdminServer/ExtractionNotificationAdminEndpoints.cs`: lease/despacho/confirmação.
- Uma chamada de registro por aplicação, referências do módulo e respectivos lockfiles.
- `migrations/suite/026_suite_extraction_notifications.up.sql`: nova view, tabela, índices, permissões mínimas e marcador. Não modifica linhas de licenças, sessões, concessões ou da outbox de login.
- `ops/production/turborama-suite-extraction-whatsapp.php` e dois exemplos de unit/timer: consumidor independente.
- Novos testes de texto/prova, worker PHP e SQL.

Não foram alterados outros produtos, o worker de conexão, a API interna MenuIA, o túnel, identidades/chaves, a política de licenciamento ou as regras anteriores de download. Lockfiles de consumidores da API incluem a nova dependência transitiva; isso não é autorização para implantar todos os consumidores.

## Implantação pelo responsável do servidor — ordem obrigatória

1. Registrar commit/release realmente instalado, units, flags, migrations e estado de login/Suite/PIX/ES/WhatsApp. Fazer backup verificável do banco e guardar a release anterior e configurações protegidas. Não copiar segredos para o Git ou para o Windows.
2. Comparar a base instalada com o candidato e integrar somente o delta. Se o número 026 já estiver ocupado por outra migration, parar e reconciliar a numeração antes da implantação; não sobrescrever ou reaplicar migrations.
3. Homologar primeiro em banco PostgreSQL isolado com o schema REAL. Confirmar colunas, views, roles `turborama-suite` e `turborama-suite-admin`, permissões existentes, plano de consulta, concessões DIRECT e retenção. Os testes locais PGlite não substituem esta etapa.
4. Conferir a biblioteca TurboBox instalada, suporte ao tipo `suite_extraction_completed`, significado do retorno de `tb_queue_whatsapp`, fila/worker do provedor e mecanismo real de confirmação. Validar PHP da instalação e extensões cURL/PDO; resolver permissões do socket/token e diretório de dados com o usuário de serviço existente. Não conceder acesso amplo para contornar falha.
5. Compilar e publicar a API Suite e o Admin completos a partir da árvore integrada, incluindo `TurboRamaSuiteNotifications.dll`, `.deps.json` e demais dependências. Não copiar somente uma DLL para dentro da release atual. Preservar as flags de todos os módulos já ativos.
6. Aplicar a migration nova uma única vez, com execução SQL fail-fast, banco explicitamente confirmado, backup e hash do arquivo anexados à mudança. A transação tem lock/statement timeout. Ela registra `schema_migrations`, seguindo o padrão da 025; não altera o ledger das migrations de conteúdo 010–016. **Não executar o antigo `apply-suite-content-migrations.sh` como instalador desta feature**: ele exige uma baseline histórica exata e não contém a 026.
7. Implantar a API/Admin novos em release separada mantendo inicialmente a feature desligada. Validar login, heartbeat, licenciamento, conteúdo, PIX/ES e o aviso de login antes de habilitar a novidade. Não executar migration DOWN, reset, drop ou recriar cadastros.
8. Confirmar que o prefixo público `/v1/suite/` já chega à API pelo túnel/proxy. Se houver allowlist de rotas, adicionar somente a nova rota POST conforme a configuração existente. Não expor socket Admin, token ou banco pela rede pública.
9. Habilitar somente os novos controles: `Suite__ExtractionNotifications__Enabled=true` na API (o `Suite__Enabled` existente deve continuar correto) e `SUITE_EXTRACTION_NOTICES_ENABLED=1` no Admin. Fazer a ativação/reinício pelo procedimento controlado da instalação; verificar saúde antes de prosseguir.
10. Instalar o novo worker e adaptar os exemplos `.service.example`/`.timer.example` para uma release verificada. Substituir o placeholder `/opt/VERIFIED-EXTRACTION-NOTICES`; conferir o ambiente protegido já existente. Validar as units antes de habilitar o timer novo. Não substituir nem desativar o timer de login.
11. Usar o EXE novo em uma conta de teste autorizada. Concluir uma extração real e verificar destino/cache, evento aceito, outbox, destinatário resolvido e retorno da fila. Somente com autorização para aquele destinatário, acompanhar a entrega real no WhatsApp. Registrar evidência sem publicar telefone, credenciais ou dados da licença.
12. Repetir com uma segunda conta, falha de conexão, reabertura do programa e repetição da operação. Validar isolamento entre contas e a regra de deduplicação descrita acima. Só então liberar gradualmente para os demais clientes; este EXE local é SEM assinatura e não é pacote de distribuição pública.

### Reversão sem destruir o existente

Desabilitar/parar apenas o timer de extração novo e desligar as duas flags novas. Se necessário, retornar API/Admin à release anterior completa pelo procedimento da instalação, preservando todas as configurações e flags prévias. **Manter a tabela, view, eventos e marcador da migration nova** para diagnóstico/retomada; não apagar banco, licenças, sessões, compras, catálogo, identidade local ou cache de operações incompletas.

Antes de reativar, reconciliar eventos `UNCERTAIN` com a fila/provedor. Não transformá-los em `PENDING` indiscriminadamente: a mensagem pode já ter sido enviada.

## Diagnóstico sem disparar mensagens

Consultar apenas contagens operacionais, sem conteúdo de cliente:

```sql
SELECT status, count(*) FROM suite.suite_extraction_notification_outbox GROUP BY status ORDER BY status;
SELECT last_error_code, count(*) FROM suite.suite_extraction_notification_outbox
WHERE last_error_code IS NOT NULL GROUP BY last_error_code ORDER BY last_error_code;
```

- Nenhum evento: confirmar EXE correto, extração real, sessão autorizada, arquivo local pendente, rota/flags e concessão recente; não mudar IP do cliente.
- 404: release/flag/roteamento ainda sem suporte; 400: payload inválido; 403: prova rejeitada; 409: vínculo/autorização/concessão indisponível; 429: limite; 503: dependência/timeout.
- `PENDING/LEASED`: conferir timer, API Admin, socket/token e lookup da compra.
- `SKIPPED`: dono indisponível/ambíguo, conta/pagamento ou telefone inválido.
- `UNCERTAIN`: consultar fila/provedor antes de qualquer nova tentativa.
- `QUEUED` sem recebimento: investigar a fila existente e o provedor; não interpretar como entrega confirmada.

## Verificações locais concluídas

- Windows: build Release e pipeline de pacote passaram; zero erros/avisos, provas/JSON estrito, isolamento, caixa local DPAPI, transporte HTTP simulado, ACK vinculado ao evento, limpeza independente, retomadas/publicação e SHA-256/manifesto. Teste físico entre volumes passou; testes de symlink e diretório case-sensitive foram pulados por permissões do Windows.
- API/Admin e testes .NET 8: zero erros/avisos; provas, dez mensagens, fuso, sanitização e privacidade passaram. Regressões Suite (golden vectors, replay, heartbeat concorrente) e ES em memória passaram. Teste HTTP real em loopback confirmou feature desligada, JSON inválido, campo proibido e limite de corpo com/sem Content-Length; sem banco ou provedor.
- PHP: sintaxe e cenários com fila falsa passaram, incluindo lookup, resultado desconhecido e resposta perdida. Executados em PHP 8.5.10 local; ainda requer qualificação na versão PHP real do servidor.
- SQL: migration, view, INSERT e consultas reais extraídas do C# passaram em PGlite 0.5.8/PostgreSQL 18.3 WASM, incluindo permissões, hash NULL DIRECT, rejeição de vínculo inválido, deduplicação, expiração e fencing de leases.
- **Não executados**: migrations na instância real, testes PostgreSQL nativos Suite/ES (nenhuma conexão isolada fornecida), contrato interno da biblioteca TurboBox/provedor real, carga concorrente da nova fila e entrega WhatsApp ponta a ponta.
