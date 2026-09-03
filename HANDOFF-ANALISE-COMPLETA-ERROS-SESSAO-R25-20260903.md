# HANDOFF ÚNICO — ANÁLISE COMPLETA DOS ERROS R25

Data: 2026-09-03

## Sintoma observado

O cliente exibe `LICENÇA, CÓDIGO OU DISPOSITIVO NÃO AUTORIZADO`.

## Evidência HTTP real

No serviço `turborama-suite-api`:

- `POST /v1/suite/challenges` retorna HTTP `200`.
- `POST /v1/suite/sessions` retorna HTTP `500` com `INTERNAL_ERROR`.
- Correlação mais recente: `6f408eba0eed94a1e7dd5316e28cd278`.

Isso prova que TLS, autoridade pública, licença e dispositivo passam pela primeira etapa. A falha ocorre no fechamento da sessão no servidor.

## Estado confirmado

- Release ativo: `r25-1-inventory-whatsapp-20260902`.
- Licença: `TS-4F5E95A6D70CF60D322D9CF2915AC70F`.
- Dispositivo cadastrado: `8e808a5b156b758aeea5aac573eaec94537778982f030ea16431a562ba6bc599`.
- Licença e dispositivo estão `ACTIVE`.
- Migrations `017_suite_commerce_session_permissions` e `020_suite_device_inventory_r25` estão aplicadas.
- Tabelas `suite_sessions`, `suite_license_deliveries`, `suite_device_presence` e `suite_connection_notification_outbox` existem.
- Papel `turborama-suite` possui os grants necessários.
- Existe uma sessão antiga expirada, que não deve ser apagada sem evidência.

## Erros/regressões identificados

1. O cliente converte qualquer `SuiteApiException`, `SuiteAuthorizationException` ou `SecurityException` na mensagem genérica de não autorizado.
2. O servidor devolve `500 INTERNAL_ERROR` em `/v1/suite/sessions`, ocultando a causa real para o cliente.
3. O release em produção não imprime a exceção SQL/stack no journal, apesar de o handoff exigir log interno seguro.
4. O fechamento de sessão é a área crítica: transação serializável, upsert de `suite_sessions`, presença online, outbox WhatsApp e consumo do challenge.
5. O código/release em produção pode estar divergente do código-fonte mais recente e precisa ser comparado antes de novo deploy.
6. Não há evidência de erro nas novas chaves, no TLS ou na licença ativa.

## Ações para a outra IA

1. Comparar o DLL implantado com o commit-fonte utilizado no build.
2. Adicionar `LogError(ex, ...)` com correlação, sem expor segredos na resposta HTTP.
3. Reproduzir uma sessão com a licença atual e capturar SQLSTATE, objeto e permissão que falharem.
4. Verificar, sem mutação, `suite_license_deliveries`, `suite_sessions`, `suite_device_presence`, `suite_connection_notification_outbox` e constraints/triggers.
5. Corrigir somente a instrução/fluxo que falhar, mantendo transação serializável, validações criptográficas, presença e outbox.
6. Compilar e implantar atomicamente o servidor corrigido.
7. Validar obrigatoriamente `challenges=200` e `sessions=200`, presença gravada uma vez e health/readiness.

## Restrições

- Não recriar ou revogar a licença.
- Não trocar DeviceId, fingerprint ou chaves da máquina.
- Não desabilitar validações.
- Não remover presença/outbox como atalho.
- Não enviar/copiar chaves privadas.
- Não declarar sucesso apenas pelo health check.
