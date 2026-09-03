# HANDOFF ÚNICO — CORREÇÃO DO ERRO 500 NA ABERTURA DE SESSÃO TURBORAMA R25

Data local: 2026-09-02 (America/Fortaleza)
Destino: Codex responsável pelo servidor de produção
Prioridade: bloqueador de liberação do cliente
Escopo: corrigir o servidor; não recompilar, reautorizar nem alterar o protocolo do cliente

## 1. Resultado objetivo já confirmado no cliente real

Licença ativa usada:

- `LicenseId`: `TS-4F5E95A6D70CF60D322D9CF2915AC70F`
- perfil: `SOFTWARE_BOUND_ONLINE`
- `DeviceId`: `8e808a5b156b758aeea5aac573eaec94537778982f030ea16431a562ba6bc599`
- `HardwareFingerprint`: `3af8338625225ee6f90883a2e66ffd2aa02bdfb2b2bbfb20899e5e59a3daa24e`

Cliente executado:

- commit: `c2d41a96417454e715ee666a58c3c04727079c4d`
- EXE SHA-256: `dd0a83a695a7a20209335238c7e24d2e98a4e179f7dbf5336e0d047a1b652b02`
- endpoint incorporado: `https://app.lzgames.com.br/`
- autoridade de licenciamento válida e TLS pin validado contra o certificado vivo

Prova executada em 2026-09-03 01:40:38 UTC (2026-09-02 22:40:38 -03):

- correlação: `r25diag-20260903014038-6d5ca5ac`
- `POST /v1/suite/challenges` -> HTTP `200`
- `POST /v1/suite/sessions` -> HTTP `500`
- corpo sanitizado recebido: `CODE=INTERNAL_ERROR`

Conclusão: a requisição chega ao servidor. A licença e o dispositivo passam pela consulta que antecede a emissão do challenge. O erro está na segunda etapa, durante a criação/conclusão da sessão. Não é falha pré-rede, de TLS, da autoridade pública, do formato do LicenseId ou do campo OTP.

## 2. Comparação independente já feita

Teste direto, sem mutação de licença:

- licença atual `TS-4F5E...`, correlação `r25compare-20260903013333-4f5e`: `/v1/suite/challenges` respondeu HTTP `200` com challenge assinado;
- licença antiga `TS-5CC1...`, correlação `r25compare-20260903013334-5cc1`: respondeu HTTP `403 LICENSE_DENIED`, informando que a licença antiga não está ativa.

Isso comprova que o servidor distingue corretamente as licenças e reconhece a licença atual na primeira etapa.

Há ainda cache DPAPI local válido para exatamente a licença atual, o DeviceId atual e a autoridade online atual, com `acceptedAt=2026-09-02 15:38:40 -03`. Essa combinação já abriu sessão e teve inventário aceito antes. O defeito atual é uma regressão posterior, não ausência de primeiro vínculo.

## 3. Ponto provável da falha

No release de servidor R25.1 identificado pelo commit `bbefbdf3967c7a4cf900984b27e764d66e33c000`, `SuiteService.SessionAsync` chega a `PostgresSuiteStore.CompleteSessionAsync` depois de validar challenge, licença, dispositivo, fingerprint e assinatura.

Erros esperados nessas validações retornariam `403` ou `409` com código específico. O retorno `500 INTERNAL_ERROR` indica exceção não tratada dentro do fechamento transacional ou na assinatura posterior.

Dentro de `CompleteSessionAsync`, verificar nesta ordem:

1. leitura de `suite.suite_license_deliveries` quando `provisioning_origin='COMMERCE'`;
2. `INSERT ... ON CONFLICT` em `suite.suite_sessions`;
3. CTE que grava `suite.suite_device_presence`;
4. inserção em `suite.suite_connection_notification_outbox`;
5. consumo do challenge;
6. assinatura da assertion de sessão.

A migração `020_suite_device_inventory_r25` cria as duas tabelas de presença/notificação e concede `SELECT, INSERT, UPDATE` ao papel `turborama-suite`. Migração ausente, aplicada no banco errado, ou grants ausentes explicam precisamente challenge `200` seguido de sessão `500`. A migração `017_suite_commerce_session_permissions` também precisa estar aplicada para a leitura de `suite_license_deliveries`.

## 4. Execução obrigatória no servidor de produção

Executar tudo no banco e no serviço efetivamente usados por `app.lzgames.com.br`, sem imprimir senhas, chaves privadas, OTPs, cookies ou DSN.

### 4.1 Correlacionar a tentativa real

Procurar a correlação abaixo nos logs Cloudflare/origin, reverse proxy, systemd e aplicação:

`r25diag-20260903014038-6d5ca5ac`

Confirmar separadamente as rotas `/v1/suite/challenges` e `/v1/suite/sessions`. Não consultar somente logs/tabelas de ativação ou inventário.

O `catch (Exception)` do servidor atualmente registra apenas a correlação. Corrigir o log interno para receber também a exceção (`LogError(ex, ...)`), mantendo para o cliente somente o corpo sanitizado `INTERNAL_ERROR`. Não expor stack trace ou SQL pela API.

### 4.2 Conferir schema e grants no banco vivo

Rodar consultas equivalentes, somente leitura, usando o mesmo banco do serviço:

```sql
SELECT current_database(), current_user;

SELECT version
FROM suite.schema_migrations
WHERE version IN (
  '017_suite_commerce_session_permissions',
  '020_suite_device_inventory_r25'
)
ORDER BY version;

SELECT
  to_regclass('suite.suite_sessions') AS sessions,
  to_regclass('suite.suite_license_deliveries') AS deliveries,
  to_regclass('suite.suite_device_presence') AS presence,
  to_regclass('suite.suite_connection_notification_outbox') AS notification_outbox;

SELECT
  has_table_privilege('turborama-suite','suite.suite_license_deliveries','SELECT') AS delivery_select,
  has_table_privilege('turborama-suite','suite.suite_sessions','SELECT,INSERT,UPDATE') AS session_rw,
  has_table_privilege('turborama-suite','suite.suite_device_presence','SELECT,INSERT,UPDATE') AS presence_rw,
  has_table_privilege('turborama-suite','suite.suite_connection_notification_outbox','SELECT,INSERT,UPDATE') AS outbox_rw;
```

Confirmar também que o processo em produção usa realmente o papel esperado; não presumir que o nome do papel configurado é o mesmo do arquivo de migração.

### 4.3 Conferir os registros desta licença sem alterá-los

Consultar de forma sanitizada:

- `suite.suite_licenses`: `status`, `product_id`, `provisioning_origin`, `revocation_generation`;
- `suite.suite_license_deliveries`: `provisioning_state`, `financial_state`, versões de origem/retomada;
- `suite.suite_license_enrollments`: DeviceId vinculado;
- `suite.suite_devices`: DeviceId e `status`;
- challenge emitido no horário informado e se ficou não consumido após o rollback da sessão.

Os valores esperados são licença `ACTIVE`, produto `TURBORAMA_SUITE`, dispositivo acima vinculado e `ACTIVE`. Não recriar a licença e não trocar o vínculo: o challenge `200` e o cache aceito anterior já demonstram que a identidade é a correta.

### 4.4 Corrigir de modo idempotente

- Se `017` ou `020` não estiver aplicada no banco vivo, aplicar a migração oficial correspondente pelo procedimento de produção já existente.
- Se a migração constar como aplicada, mas tabela/coluna/grant estiver ausente, tratar como drift: produzir backup/metadados de evidência, reconciliar exatamente com a migração oficial e registrar a correção.
- Se schema e grants estiverem corretos, reproduzir uma abertura de sessão com log interno de exceção e corrigir a instrução exata que falhar, preservando a transação serializável e todas as validações criptográficas.
- Não remover a gravação de presença/outbox como atalho permanente.
- Não alterar bytes do proof, `contextHash`, domínios v1, ProductId, key IDs, políticas de dispositivo ou autoridades.

### 4.5 Publicar e verificar o reparo real

Depois da correção:

1. compilar/testar o serviço no mesmo commit que será implantado;
2. implantar pelo procedimento atômico existente;
3. confirmar health/readiness;
4. repetir o fluxo completo com a licença atual;
5. resultado obrigatório: `/v1/suite/challenges` HTTP `200` e `/v1/suite/sessions` HTTP `200`;
6. confirmar que a sessão ficou ativa e que presença/outbox foram gravadas uma única vez, sem duplicação;
7. confirmar que nenhuma chave privada ou segredo apareceu em logs.

## 5. Retorno único exigido

Entregar um único arquivo de retorno na mesma pasta, contendo:

- causa-raiz exata, incluindo SQLSTATE/objeto/permissão ou stack interno sanitizado;
- commit corrigido e commit efetivamente implantado;
- migrations detectadas/aplicadas e banco/role sanitizados;
- evidência das duas respostas finais `200/200`, com nova correlação e horários UTC/local;
- estado final da licença, dispositivo, sessão e presença;
- status de health/readiness;
- confirmação explícita de que a licença não foi recriada e o cliente não precisou ser recompilado;
- rollback disponível.

Nome sugerido:

`RETORNO-CORRECAO-ERRO-500-SESSAO-TURBORAMA-R25-20260902.md`

## 6. Restrições

- Não pedir nem registrar chave privada.
- Não gerar novo OTP sem evidência de necessidade.
- Não revogar, recriar ou transferir esta licença.
- Não enfraquecer fail-closed, assinatura, pin TLS ou vínculo de dispositivo.
- Não declarar sucesso apenas porque o health check responde; o critério é o fluxo real `challenges 200` + `sessions 200`.
