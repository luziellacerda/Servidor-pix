# ORDEM ÚNICA DE EXECUÇÃO — REPARAR A SESSÃO R25 SEM NOVOS HANDOFFS

Data: 2026-09-03
Responsável pela execução: Codex que opera o servidor de produção
Serviço autorizado para reparo: `turborama-suite-api.service`
Repositório: `https://github.com/luziellacerda/Servidor-pix`
Branch de comunicação: `codex/turborama-suite-vendas-producao-20260828`

## 1. Esta é uma ordem de execução, não outro pedido de análise

Ao receber este arquivo:

1. não criar outro tutorial, plano, solicitação ou handoff;
2. não devolver o trabalho para “outra IA”;
3. permanecer na mesma tarefa até localizar a exceção, corrigir o código/schema necessário, testar, implantar e validar o fluxo real;
4. responder somente com o resultado final executado ou com um bloqueio humano específico que esteja fora da autorização abaixo.

Os commits `f187af67443593d5c341ca3db83b4d9d89328a53` e `5096ad09bdb3129c41d013cf37fe27529c5169dd` adicionaram apenas documentação. Eles não corrigiram o serviço. Não repetir esse comportamento.

## 2. Limite de autorização humana

O proprietário informa que houve troca de código e/ou chaves sem autorização humana. A partir desta ordem, fica expressamente determinado:

### Está autorizado

- fazer backup recuperável do release e das referências de configuração atuais;
- inspecionar logs, metadados de banco, schema, grants, triggers, constraints, unit files, drop-ins, hashes e commits;
- corrigir exclusivamente o erro de sessão do `turborama-suite-api.service`;
- adicionar diagnóstico interno sanitizado;
- corrigir código, teste e migration idempotente se a evidência mostrar necessidade;
- compilar um release novo, implantar atomicamente, reiniciar somente o serviço necessário e executar rollback se a validação falhar;
- criar commit contendo a correção real, os testes e, quando indispensável, a migration — não apenas documentação.

### Não está autorizado

- gerar, substituir, rotacionar, importar, remover ou publicar qualquer chave privada ou pública;
- alterar `authority.json`, envelopes públicos, key IDs, TLS pins, certificados, peppers ou arquivos de cerimônia;
- alterar domínio, bytes do proof, `contextHash`, ProductId ou contratos `/v1`;
- recriar, revogar, transferir ou editar a licença ativa;
- trocar DeviceId, fingerprint, chave CNG ou perfil da máquina;
- gerar novo OTP, alterar preço/venda/pagamento ou mexer nos serviços PIX, painel, catálogo ou gateway sem causa técnica comprovada;
- copiar segredos para Git, relatório, terminal compartilhado ou chat.

Qualquer possível rotação/restauração de chave exige autorização humana nova e explícita. Ela não faz parte deste reparo. As chaves atuais já passaram pelo challenge, portanto não devem ser tocadas para corrigir o `500`.

## 3. Fatos técnicos já comprovados

Cliente:

- `LicenseId`: `TS-4F5E95A6D70CF60D322D9CF2915AC70F`;
- perfil: `SOFTWARE_BOUND_ONLINE`;
- `DeviceId`: `8e808a5b156b758aeea5aac573eaec94537778982f030ea16431a562ba6bc599`;
- `HardwareFingerprint`: `3af8338625225ee6f90883a2e66ffd2aa02bdfb2b2bbfb20899e5e59a3daa24e`;
- commit do cliente: `c2d41a96417454e715ee666a58c3c04727079c4d`;
- SHA-256 do EXE: `dd0a83a695a7a20209335238c7e24d2e98a4e179f7dbf5336e0d047a1b652b02`;
- autoridade pública e pin TLS foram validados;
- a mesma licença/máquina já teve sessão e inventário aceitos em 2026-09-02 15:38:40 -03.

Falha atual reproduzida:

- correlação do cliente: `r25diag-20260903014038-6d5ca5ac`;
- `POST /v1/suite/challenges` -> HTTP `200`;
- `POST /v1/suite/sessions` -> HTTP `500 INTERNAL_ERROR`.

Falha reproduzida novamente depois de o servidor declarar implantado o release
`r25-5-session-fix-20260903`:

- horário: 2026-09-03 10:56:09 UTC / 2026-09-03 07:56:09 -03;
- correlação: `r25diag-20260903105609-be2aace4`;
- autoridade de conteúdo: válida;
- runtime de licenciamento do cliente: disponível;
- `POST /v1/suite/challenges` -> HTTP `200`;
- `POST /v1/suite/sessions` -> HTTP `500 INTERNAL_ERROR`.

Portanto, o release declarado no retorno `2677dbad495bf0808e34bcf87a27d3db683b85c3`
não passou no aceite real. `active (running)`, `/health=200` e `/ready=200` não
comprovam o conserto da sessão.

Correlação informada pelo próprio servidor:

- `6f408eba0eed94a1e7dd5316e28cd278`;
- mesmo resultado: challenge `200`, session `500`.

Estado informado pelo servidor:

- release ativo: `r25-1-inventory-whatsapp-20260902`;
- licença e dispositivo `ACTIVE`;
- migrations `017_suite_commerce_session_permissions` e `020_suite_device_inventory_r25` registradas como aplicadas;
- tabelas de sessões, entregas, presença e outbox existentes;
- grants declarados como presentes;
- existe uma sessão antiga expirada, que deve ser preservada e usada no teste de regressão.

Conclusão obrigatória: TLS, autoridade, licença e dispositivo passam pela primeira etapa. A falha está no processamento de `/v1/suite/sessions`. Não recompilar o cliente e não reautorizar a máquina.

## 4. Reparar diretamente o serviço correto

Trabalhar somente no caminho efetivamente carregado por `turborama-suite-api.service`.

### Etapa A — congelar e identificar o runtime real

1. Registrar, sem exibir valores secretos:
   - `systemctl cat turborama-suite-api.service`;
   - `systemctl show` para `ExecStart`, `WorkingDirectory`, unit/drop-ins e caminhos de EnvironmentFile;
   - caminho resolvido do DLL ativo;
   - SHA-256 do DLL ativo;
   - release/diretório ativo;
   - commit-fonte usado para compilá-lo, se houver manifesto.
2. Fazer backup do release ativo e das referências de configuração, mantendo segredos fora do Git e do relatório.
3. Comparar o DLL/release implantado com o código-fonte que será corrigido. O branch de documentação não deve ser presumido como fonte do DLL ativo.
4. Se houver divergência entre fonte e runtime, corrigir a partir da fonte exata do release ativo ou incorporar essa fonte ao Git antes de editar. Não implantar código antigo sobre o R25.

### Etapa B — tornar a exceção observável internamente

No mapeamento de `/v1/suite/sessions` em `src/TurboRamaSuiteOnlineServer/Program.cs`, o `catch (Exception)` atual descarta a exceção e registra apenas a correlação. Corrigir isso antes de tentar adivinhar a causa.

Implementar log interno restrito e estruturado:

- correlação;
- método e rota;
- tipo da exceção;
- para `PostgresException`: `SqlState`, schema, tabela, coluna, constraint e routine quando disponíveis;
- stack trace no journal interno restrito;
- nenhuma senha, DSN, chave, OTP, proof, cookie, corpo integral da requisição ou valor de parâmetro SQL.

Manter a resposta pública exatamente sanitizada: HTTP `500`, código `INTERNAL_ERROR`, sem SQL ou stack para o cliente.

Adicionar teste que garanta simultaneamente:

- o detalhe interno é registrado;
- a resposta externa continua sanitizada;
- nenhum segredo é incluído no log.

Somente melhorar o log não conclui esta ordem. Usar o log para encontrar e reparar a causa real.

### Etapa C — reproduzir e isolar a instrução exata

Reproduzir `/v1/suite/sessions` no release instrumentado e acompanhar a correlação. Inspecionar, na ordem real de `PostgresSuiteStore.CompleteSessionAsync`:

1. lock/leitura de `suite.suite_licenses` e `provisioning_origin`;
2. leitura/lock de `suite.suite_license_deliveries` quando a origem for `COMMERCE`;
3. lock de `suite.suite_license_enrollments`;
4. lock de `suite.suite_devices`;
5. upsert em `suite.suite_sessions`;
6. CTE/upsert de `suite.suite_device_presence`;
7. inserção idempotente em `suite.suite_connection_notification_outbox`;
8. consumo do challenge;
9. commit da transação serializável;
10. assinatura da assertion de sessão.

Executar a verificação com o papel e o banco realmente usados pelo processo, não apenas com `postgres` ou com o papel que se imagina estar configurado.

Além do registro em `schema_migrations`, comparar o schema vivo com a migration oficial:

- nomes, tipos, nulabilidade e defaults das colunas;
- índices e alvos de `ON CONFLICT`;
- PKs, FKs e checks;
- triggers e funções acionadas;
- owner e grants efetivos, inclusive privilégios de sequência;
- `search_path` e banco/schema reais do serviço.

Usar a sessão antiga expirada no teste. O caso obrigatório é: licença e dispositivo ativos, linha anterior expirada para o mesmo par `(license_id, device_id)`, seguida por `session.open`. O upsert deve renovar a linha e retornar `200`, sem colisão da constraint única de `session_id`.

Também testar presença inexistente e já existente, outbox vazia e evento duplicado. Falha posterior do worker WhatsApp não pode invalidar nem impedir a sessão; não realizar chamada externa ao WhatsApp dentro da requisição de sessão.

### Etapa D — aplicar a correção baseada na evidência

Aplicar exatamente uma destas classes de reparo conforme o erro capturado:

- schema drift: migration idempotente e revisada que reconcilie somente o objeto divergente;
- papel runtime sem permissão: grant mínimo e explícito por migration;
- SQL incompatível com constraint/coluna: corrigir a instrução e cobrir com teste PostgreSQL;
- nulo/estado legado não tratado: compatibilizar o dado legado sem relaxar autorização;
- colisão no upsert da sessão expirada: corrigir a semântica de conflito preservando uma sessão ativa por dispositivo;
- erro na presença/outbox: corrigir a operação transacional/idempotente; não remover presença/outbox como atalho;
- DLL divergente: compilar e implantar a fonte R25 correta com a correção;
- erro de assinatura após o commit: corrigir apenas carregamento/uso da autoridade já aprovada, sem criar ou trocar chave.

Não editar manualmente a licença para fazer o teste passar. Não apagar a sessão antiga sem demonstrar que ela é corrupção e sem backup.

## 5. Testes obrigatórios antes do deploy

Adicionar e executar testes automatizados para:

1. sessão nova;
2. renovação sobre sessão expirada existente;
3. heartbeat;
4. retry/serialização e deadlock;
5. presença criada e atualizada;
6. outbox idempotente sem duplicação;
7. licença/dispositivo inválidos retornando `403`;
8. challenge consumido/repetido retornando `409`;
9. falha interna retornando `500` sanitizado e log interno útil;
10. nenhuma regressão nos endpoints de ativação e inventário.

Executar também os testes PostgreSQL/integrados contra schema equivalente ao de produção. Teste unitário sem banco não basta para este defeito.

## 6. Commit, deploy e validação final

1. Revisar o diff e confirmar que não há mudança de chave, autoridade, licença, protocolo, preço ou serviço fora do escopo.
2. Criar commit com código/testes/migration reais. Um commit contendo apenas `.md` não é aceite.
3. Compilar Release reproduzível em diretório novo.
4. Registrar commit e SHA-256 dos binários.
5. Implantar atomicamente em novo release, preservando o anterior para rollback.
6. Reiniciar `turborama-suite-api.service` e verificar health/readiness.
7. Executar o fluxo assinado real com a licença e o dispositivo atuais.
8. Se for indispensável uma ação no PC cliente, pedir apenas uma vez, na própria tarefa, para clicar com OTP vazio enquanto o servidor monitora. Não criar novo handoff.

Critério obrigatório de aceite:

- `/v1/suite/challenges` -> `200`;
- `/v1/suite/sessions` -> `200`;
- assertion de sessão válida no cliente;
- sessão `ACTIVE` para o dispositivo correto;
- challenge consumido uma vez;
- presença `ONLINE` gravada corretamente;
- no máximo um evento idempotente na outbox;
- tentativa repetida/replay tratada pelo código previsto;
- health/readiness saudáveis;
- nenhuma chave/autoridade/licença alterada;
- nenhum segredo exposto em Git ou logs compartilhados.

Se qualquer item falhar, executar rollback imediatamente, corrigir na mesma tarefa e repetir. Não declarar conclusão parcial.

## 7. Forma do retorno — sem novo handoff

Não criar outro arquivo `.md` de encaminhamento. Responder na própria tarefa do servidor com:

- causa-raiz exata e o ponto que falhava;
- SQLSTATE/constraint/tabela sanitizados, quando aplicável;
- commit da correção real;
- release e hash efetivamente implantados;
- correlação e horários do teste final;
- prova `challenges=200` e `sessions=200`;
- estado final da sessão, presença e outbox;
- confirmação de que chaves, autoridades, licença e DeviceId permaneceram inalterados;
- localização do backup e comando de rollback, sem segredos.

Não responder com “recomenda-se”, “a outra IA deve”, “falta investigar” ou outro tutorial. Esta ordem só termina com o serviço reparado e validado, ou com um bloqueio humano específico fora do escopo autorizado.

## 8. Causa provável já localizada no código — verificar antes de qualquer outra hipótese

Há uma incompatibilidade objetiva no código R25.1 do commit
`bbefbdf3967c7a4cf900984b27e764d66e33c000`:

1. `src/TurboRamaSuiteOnlineServer/Store.cs`, em `CompleteSessionAsync`, executa
   um upsert em `suite.suite_sessions` e substitui `session_id` quando recebe
   uma nova ação `session.open` para o mesmo `(license_id, device_id)`;
2. `migrations/suite/020_suite_device_inventory_r25.up.sql` criou
   `suite.suite_device_inventory_challenges.session_id` com uma FK para
   `suite.suite_sessions(session_id)`, sem `ON UPDATE`;
3. `DeviceInventoryService` consome o challenge de inventário atualizando
   `consumed_at`, mas mantém a linha como histórico;
4. esta máquina já abriu uma sessão, enviou inventário e agora possui uma sessão
   antiga expirada;
5. ao abrir uma sessão nova, o upsert tenta trocar o `session_id` da linha pai;
6. a FK histórica impede a troca porque ainda referencia o ID anterior;
7. o resultado esperado é PostgreSQL `SQLSTATE 23503`, provavelmente na constraint
   `suite_device_inventory_challenges_session_id_fkey`;
8. `CompleteSessionAsync` converte somente `40001` e `40P01`. O `23503` escapa,
   e o handler genérico o transforma em HTTP `500 INTERNAL_ERROR`.

Esse encadeamento coincide exatamente com o comportamento observado:

- primeira sessão e inventário funcionaram;
- a sessão anterior expirou;
- um novo challenge de sessão é emitido com `200`;
- a conclusão da nova sessão falha com `500`;
- a licença e o dispositivo continuam ativos.

Não declarar essa hipótese como causa confirmada sem consultar a FK e as referências
no banco vivo. Porém, ela deve ser verificada primeiro, antes de trocar qualquer
configuração ou chave.

### 8.1 Consulta decisiva, somente leitura

Executar no banco efetivamente usado pelo processo e, para a conferência de grants,
com o mesmo papel runtime. Não usar um banco de homologação por engano.

```sql
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';
SET LOCAL lock_timeout = '2s';

SELECT current_database(), current_user, session_user,
       current_setting('search_path'), current_setting('server_version_num');

SELECT c.conname,
       c.conrelid::regclass AS source_table,
       c.confrelid::regclass AS referenced_table,
       CASE c.confupdtype
         WHEN 'a' THEN 'NO ACTION'
         WHEN 'r' THEN 'RESTRICT'
         WHEN 'c' THEN 'CASCADE'
         WHEN 'n' THEN 'SET NULL'
         WHEN 'd' THEN 'SET DEFAULT'
       END AS update_action,
       pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c
WHERE c.contype = 'f'
  AND c.conrelid = 'suite.suite_device_inventory_challenges'::regclass
  AND c.confrelid = 'suite.suite_sessions'::regclass;

SELECT s.status,
       s.authorized_until,
       s.authorized_until <= clock_timestamp() AS session_expired,
       s.revocation_generation,
       count(ic.challenge_id) AS inventory_challenge_references,
       count(ic.challenge_id) FILTER (WHERE ic.consumed_at IS NOT NULL)
         AS consumed_references,
       count(ic.challenge_id) FILTER (WHERE ic.consumed_at IS NULL)
         AS unconsumed_references
FROM suite.suite_sessions s
LEFT JOIN suite.suite_device_inventory_challenges ic
  ON ic.session_id = s.session_id
WHERE s.license_id = 'TS-4F5E95A6D70CF60D322D9CF2915AC70F'
  AND s.device_id = '8e808a5b156b758aeea5aac573eaec94537778982f030ea16431a562ba6bc599'
GROUP BY s.status, s.authorized_until, s.revocation_generation;

COMMIT;
```

A causa fica materialmente confirmada se os quatro fatos coexistirem:

- a FK acima existe;
- `update_action` é `NO ACTION` ou `RESTRICT`;
- a sessão atual está expirada;
- existe ao menos um challenge de inventário histórico referenciando o
  `session_id` atual da linha pai.

Não fazer um `UPDATE` experimental nessa licença em produção. O próximo proof real
já reproduz o problema, e o log deve confirmar `23503`.

### 8.2 Correção mínima se `23503` for confirmado

A relação de inventário é histórica. O `session_id` nela deve preservar o ID usado
quando o inventário foi coletado. Ele não deve seguir o ponteiro mutável da sessão
corrente.

Criar uma migration forward, por exemplo
`021_suite_inventory_challenge_session_history.up.sql`, que remova somente a FK
entre `suite_device_inventory_challenges.session_id` e
`suite_sessions.session_id`.

Modelo a adaptar ao nome de constraint confirmado pela consulta:

```sql
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(
  hashtextextended('suite:021_inventory_challenge_session_history', 0)
);

ALTER TABLE suite.suite_device_inventory_challenges
  DROP CONSTRAINT IF EXISTS
  suite_device_inventory_challenges_session_id_fkey;

INSERT INTO suite.schema_migrations(version)
VALUES ('021_suite_inventory_challenge_session_history')
ON CONFLICT (version) DO NOTHING;

COMMIT;
```

Antes de aplicar, revisar o diff e conferir que o nome encontrado no banco é o
mesmo. Se o banco tiver nome divergente, a migration deve localizar/remover apenas
a FK exata entre essas duas colunas; não remover qualquer outra FK por padrão amplo.

Depois da migration, exigir:

```sql
SELECT count(*) AS forbidden_historical_session_fk
FROM pg_constraint c
WHERE c.contype = 'f'
  AND c.conrelid = 'suite.suite_device_inventory_challenges'::regclass
  AND c.confrelid = 'suite.suite_sessions'::regclass;
```

O resultado esperado é `0`.

Não fazer estas falsas correções:

- não usar `ON UPDATE CASCADE`, porque isso reescreveria o `session_id` histórico
  e faria parecer que o inventário antigo pertence à sessão nova;
- não apagar challenges de inventário consumidos;
- não apagar a sessão antiga para mascarar o erro;
- não conservar artificialmente o mesmo `session_id` entre execuções do cliente;
- não remover validação de sessão no serviço;
- não desativar inventário, presença ou outbox.

A coluna histórica `session_id` permanece. A FK do inventário para
`(license_id, device_id)` e todas as validações criptográficas permanecem. O serviço
já valida a sessão ativa antes de emitir/aceitar inventário; isso deve continuar.

Uma migration de rollback que tente recriar essa FK deve falhar de forma explícita
se já existirem IDs históricos que não correspondem à sessão corrente. Não apagar
histórico para permitir rollback. O rollback binário pode manter a migration forward,
porque remover essa FK é compatível com o comportamento anterior do serviço.

## 9. Procedimento exato de captura da falha atual

### 9.1 Provar qual DLL está realmente executando

O nome `r25-5-session-fix-20260903` e o SHA informado em um relatório não provam o
runtime. A cadeia de prova obrigatória é:

`InvocationID/PID -> argumento DLL -> arquivo resolvido -> inode mapeado -> SHA-256 -> manifesto -> commit Git`

Executar como root em Bash, sem `set -x`:

```bash
set -Eeuo pipefail
set +x
umask 077

UNIT='turborama-suite-api.service'
AUDIT_BASE='/root/turborama-suite-api-audit'
install -d -o root -g root -m 0700 "$AUDIT_BASE"
AUDIT="$(mktemp -d "$AUDIT_BASE/r25-session-20260903.XXXXXX")"
chmod 0700 "$AUDIT"

systemctl show "$UNIT" \
  -p Id -p LoadState -p ActiveState -p SubState \
  -p FragmentPath -p DropInPaths -p User -p Group \
  -p WorkingDirectory -p MainPID -p InvocationID \
  -p ExecMainStartTimestamp -p NRestarts -p Result \
  > "$AUDIT/systemctl-safe-properties.txt"

systemctl is-active --quiet "$UNIT"
PID="$(systemctl show "$UNIT" -p MainPID --value)"
INVOCATION_ID="$(systemctl show "$UNIT" -p InvocationID --value)"
START_TEXT="$(LC_ALL=C systemctl show "$UNIT" -p ExecMainStartTimestamp --value)"

[[ "$PID" =~ ^[1-9][0-9]*$ ]]
[[ "$INVOCATION_ID" =~ ^[0-9a-f]{32}$ ]]
[[ -r "/proc/$PID/cmdline" ]]

declare -a ARGV=()
while IFS= read -r -d '' ARG; do ARGV+=("$ARG"); done < "/proc/$PID/cmdline"

declare -a DLL_CANDIDATES=()
for ARG in "${ARGV[@]:1}"; do
  [[ "$ARG" == *.dll ]] && DLL_CANDIDATES+=("$ARG")
done
[[ "${#DLL_CANDIDATES[@]}" -eq 1 ]]
DLL_ARG="${DLL_CANDIDATES[0]}"
unset ARGV ARG DLL_CANDIDATES

if [[ "$DLL_ARG" == /* ]]; then
  DLL_VIEW="/proc/$PID/root/${DLL_ARG#/}"
else
  DLL_VIEW="/proc/$PID/cwd/$DLL_ARG"
fi

[[ -f "$DLL_VIEW" ]]
DLL_REAL="$(readlink -e -- "$DLL_VIEW")"
DLL_SHA256="$(sha256sum -- "$DLL_VIEW" | awk '{print $1}')"
DLL_DEV_INODE="$(stat -Lc '%d:%i' -- "$DLL_VIEW")"
RELEASE_DIR="$(dirname -- "$DLL_REAL")"

printf '%s\n' \
  "unit=$UNIT" \
  "pid=$PID" \
  "invocation_id=$INVOCATION_ID" \
  "dll_argument=$DLL_ARG" \
  "dll_resolved=$DLL_REAL" \
  "release_dir=$RELEASE_DIR" \
  "dll_sha256=$DLL_SHA256" \
  "device_inode=$DLL_DEV_INODE" \
  "process_start=$START_TEXT" \
  > "$AUDIT/runtime.safe.txt"
```

O SHA alegado para o release `r25-5` foi
`d6678e3ff26f0b5879450dfedfafeda0489fb8fc6131d386a0d0b644578be78d`.
Se o hash calculado do arquivo carregado não for exatamente esse, o release alegado
não estava ativo. Mesmo que coincida, ainda é necessário provar o commit-fonte.

O release novo deve conter `RELEASE-METADATA` e `SHA256SUMS` com:

- `source_commit` completo de 40 caracteres;
- `dll_sha256` completo;
- verificação `sha256sum --check --quiet --strict SHA256SUMS` bem-sucedida.

O commit `bbefbdf...` está como objeto Git fora da ancestralidade do branch atual.
Não recompilar a árvore antiga do branch documental no lugar da fonte R25. A fonte
exata do release deve primeiro ficar rastreável por um commit real.

### 9.2 Capturar a correlação que já falhou depois do suposto reparo

```bash
CID='r25diag-20260903105609-be2aace4'
SINCE='2026-09-03 10:55:30 UTC'
UNTIL='2026-09-03 10:57:00 UTC'

journalctl --utc --unit 'turborama-suite-api.service' \
  --since "$SINCE" --until "$UNTIL" \
  --no-pager --output=json \
  > "$AUDIT/journal-window.raw.jsonl"

jq -c --arg cid "$CID" \
  'select((.MESSAGE // "") | contains($cid))' \
  "$AUDIT/journal-window.raw.jsonl" \
  > "$AUDIT/journal-correlation.raw.jsonl"

test "$(wc -l < "$AUDIT/journal-correlation.raw.jsonl")" -gt 0
```

Não commitar nem copiar o journal bruto. No resultado compartilhado, permitir
somente:

- correlação;
- `InvocationID`, PID e horário;
- estágio interno;
- tipo da exceção;
- SQLSTATE;
- schema, tabela, coluna, constraint e routine;
- frames pertencentes a `TurboRamaSuiteOnlineServer`;
- nunca mensagem SQL bruta, parâmetros, request body, proof, assinatura ou segredo.

Se o journal mostrar apenas `Suite request failed. Correlation ...`, então o código
de diagnóstico não está no DLL carregado ou ainda descarta a exceção. Nesse caso,
o retorno que declarou o reparo deve ser rejeitado e o handler deve ser realmente
corrigido.

### 9.3 Instrumentação interna obrigatória

Adicionar log estruturado interno para `PostgresException` antes do catch genérico.
O formato precisa registrar no mínimo:

```text
correlation_id
route
stage
exception_type
sql_state
schema_name
table_name
column_name
constraint_name
routine
```

Adicionar marcadores internos em ordem:

```text
proof_verified
store_started
license_locked
delivery_locked
enrollment_locked
device_locked
session_upserted
presence_outbox_applied
challenge_consumed
commit_succeeded
session_sign_started
session_sign_succeeded
```

Não registrar `MessageText`, `Detail`, `Where`, `InternalQuery`, SQL, parâmetros,
corpos HTTP, proof, assinatura, chave, DSN, token ou conteúdo de arquivos de ambiente.
O cliente continua recebendo somente `500 INTERNAL_ERROR` em falha imprevista.

Interpretação exata:

- sem `commit_succeeded`, challenge não consumido e sessão antiga intacta: falha
  dentro da transação;
- `SQLSTATE 23503`, estágio `session_upserted` e constraint de inventory challenge:
  causa da seção 8 confirmada;
- `commit_succeeded` e erro depois de `session_sign_started`: transação funcionou e
  a falha é na assinatura/serialização da resposta;
- banco confirmado, mas sem `commit_succeeded` devido a perda de conexão: resultado
  de commit indeterminado; consultar o banco antes de repetir;
- servidor retorna `200`, mas cliente rejeita: assertion, assinatura ou contexto da
  resposta; não trocar autoridade sem provar incompatibilidade.

## 10. Teste PostgreSQL obrigatório que faltou no CI

O CI do R25 encontrado não cobre esse defeito:

- aplica migrations somente até `014`, não até `020/021`;
- não executa adequadamente `TurboRamaSuitePostgres.Tests`;
- usa `Username=postgres`, escondendo erros de grants do papel runtime;
- não testa nova sessão depois de sessão expirada com inventário histórico;
- não testa presença/outbox no fluxo completo.

Corrigir o CI. Usar PostgreSQL 16 descartável. O setup pode usar o owner, mas a API
e os testes de operação devem conectar como `turborama-suite`.

Caso de regressão obrigatório:

1. aplicar migrations `001` até `021` em ordem;
2. criar licença, entrega, enrollment e dispositivo sintéticos ativos;
3. gerar chave RSA efêmera apenas para o teste;
4. abrir a primeira sessão pelo serviço, com proof válido;
5. emitir e consumir um challenge de inventário nessa sessão;
6. confirmar que o challenge histórico permanece armazenado;
7. expirar somente `authorized_until` no banco descartável;
8. gerar um novo `session_id`;
9. emitir novo challenge `session.open`;
10. enviar o novo proof válido antes de 60 segundos;
11. exigir HTTP `200` em `/v1/suite/sessions`;
12. exigir que `suite_sessions` contenha o novo `session_id`;
13. exigir que o challenge histórico de inventário continue contendo o ID antigo;
14. fazer o teste falhar se o histórico for reescrito por `ON UPDATE CASCADE`;
15. exigir que o challenge novo esteja consumido;
16. exigir presença `ONLINE`;
17. exigir outbox conforme as regras de transição/cooldown;
18. validar a assertion final com a chave pública efêmera.

Adicionar variante com challenge de inventário antigo ainda não consumido. A sessão
nova deve abrir, mas a tentativa posterior de aceitar o challenge preso à sessão
antiga deve retornar `409 CHALLENGE_INVALID`.

Matriz de presença/outbox:

- sem presença anterior: criar `ONLINE`, gerar um evento e preencher
  `last_notified_at`;
- presença expirada e fora do cooldown: transicionar e gerar exatamente um evento;
- presença ainda online: estender `online_until`, preservar a transição e não
  duplicar evento;
- presença expirada, porém dentro do cooldown: ficar online sem novo evento;
- heartbeat válido: `200`, tempo monotônico e nenhuma duplicação;
- replay do mesmo challenge: `409` e nenhuma segunda gravação;
- worker WhatsApp parado: sessão continua em `200` e evento fica pendente na outbox.

Teste de atomicidade:

1. em banco descartável, criar um trigger de teste que falhe no insert de presença;
2. executar a sessão;
3. exigir `500` sanitizado;
4. exigir rollback de sessão, presença, outbox e consumo do challenge;
5. repetir com falha no insert da outbox;
6. remover o trigger de teste;
7. usar um signer decorador que assine o challenge e falhe somente ao assinar a
   `SessionAssertion`;
8. nesse último caso, exigir `500` sanitizado, mas sessão/challenge/presença já
   confirmados, provando a separação entre transação e assinatura pós-commit.

Testes negativos adicionais:

- proof inválido -> `403 PROOF_INVALID`;
- dispositivo inválido -> `403 DEVICE_DENIED`;
- challenge expirado ou consumido -> `409 CHALLENGE_INVALID`;
- serialização `40001` e deadlock `40P01` -> retry limitado e depois `409
  TRANSACTION_CONFLICT`, nunca `500` genérico;
- falha interna inesperada -> `500` sanitizado e log interno correlacionável.

## 11. Teste real obrigatório no PC autorizado

Depois de corrigir, testar com a identidade real já existente. Não digitar OTP e
não ativar novamente.

No servidor, antes do clique/probe:

1. registrar PID, InvocationID, NRestarts, DLL resolvido e SHA-256;
2. abrir acompanhamento do journal de `turborama-suite-api.service`;
3. não imprimir request/response assinados;
4. aguardar a correlação informada pelo cliente.

Neste PC cliente, executar exatamente:

```powershell
& 'D:\CodexTemp\dotnet-sdk-10.0.400\dotnet.exe' `
  'C:\Users\Admin\Documents\Codex\2026-08-25\https-github-com-luziellacerda-truborama-suite\outputs\R25ClientProbe\bin\Release\net10.0-windows\win-x64\R25ClientProbe.dll' `
  'TS-4F5E95A6D70CF60D322D9CF2915AC70F'
```

Saída mínima obrigatória:

```text
CONTENT_VERIFY=OK:<validade>
AVAILABLE=True
CORRELATION=r25diag-<timestamp>-<nonce>
HTTP_START=POST:/v1/suite/challenges
HTTP_STOP=200:/v1/suite/challenges
HTTP_START=POST:/v1/suite/sessions
HTTP_STOP=200:/v1/suite/sessions
RESULT=AUTHORIZED
```

Qualquer `RESULT=SUITE_API_ERROR`, `STATUS=500` ou ausência de
`HTTP_STOP=200:/v1/suite/sessions` reprova o release.

Executar o probe duas vezes consecutivas. As duas execuções devem retornar
`AUTHORIZED`. A segunda deve renovar a sessão sem duplicar indevidamente a outbox.

Antes e depois de cada execução, o servidor deve confirmar que PID, InvocationID e
NRestarts não mudaram. Isso evita confundir sucesso com reinício ou troca automática
de processo.

### 11.1 Consulta pós-teste

Com os IDs do challenge e da sessão obtidos internamente pela correlação, executar
consulta parametrizada, somente leitura:

```sql
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';

SELECT
  EXISTS (
    SELECT 1
    FROM suite.suite_sessions
    WHERE license_id = $1
      AND device_id = $2
      AND session_id = $3
      AND status = 'ACTIVE'
      AND authorized_until > clock_timestamp()
  ) AS new_session_active,
  (
    SELECT consumed_at IS NOT NULL
    FROM suite.suite_challenges
    WHERE challenge_id = $4
  ) AS challenge_consumed,
  (
    SELECT state = 'ONLINE' AND online_until > clock_timestamp()
    FROM suite.suite_device_presence
    WHERE license_id = $1 AND device_id = $2
  ) AS presence_online,
  (
    SELECT count(*)
    FROM suite.suite_connection_notification_outbox
    WHERE event_key = 'device.connected:' || $4
  ) AS outbox_events;

COMMIT;
```

Valores esperados:

- `new_session_active = true`;
- `challenge_consumed = true`;
- `presence_online = true`;
- `outbox_events` igual a `0` ou `1` conforme transição e cooldown, nunca maior
  que `1` para o mesmo challenge;
- `last_server_time` monotônico;
- inventário histórico ainda vinculado ao ID histórico original.

Não selecionar SPKI, fingerprint, ciphertext, JSON de dispositivo ou segredo para
provar esse resultado.

## 12. Árvore de decisão sem improvisação

Ao repetir o teste, agir exatamente conforme o resultado:

- `challenges=200`, `sessions=500`, `23503` na FK histórica: aplicar a correção da
  seção 8, testar, implantar e repetir;
- `challenges=200`, `sessions=500`, `42501`: corrigir grant mínimo via migration e
  repetir como o papel runtime;
- `challenges=200`, `sessions=500`, `42P01` ou `42703`: schema drift; reconciliar o
  objeto exato por migration idempotente;
- `challenges=200`, `sessions=500`, `23505`: identificar a constraint única e
  corrigir a semântica do upsert, sem apagar histórico;
- `challenges=200`, `sessions=500`, `23514` ou `23502`: corrigir valor/compatibilidade
  de coluna no código e adicionar regressão;
- falha antes de `commit_succeeded`: transação/banco;
- falha depois de `commit_succeeded`: assinatura ou serialização da assertion;
- `sessions=403`: registrar o código específico; não tratar como o mesmo `500`;
- `sessions=409`: gerar challenge novo e repetir; não editar licença;
- servidor `200`, cliente rejeita assertion: comparar campos assinados e key ID já
  aprovado, sem rotacionar chave;
- correlação ausente no serviço: proxy apontando para outro upstream ou release;
- correlação presente, mas apenas log genérico: DLL incorreto ou instrumentação não
  implantada;
- health `200` e session `500`: serviço continua reprovado.

## 13. Prova do novo deploy e rollback

O release corrigido deve ser imutável e incluir:

```text
RELEASE-METADATA
  release_id=<identificador>
  source_commit=<SHA Git completo de 40 caracteres>
  dll_sha256=<SHA-256 completo>
  created_utc=<ISO-8601>

SHA256SUMS
  <hash de cada arquivo publicado>
```

Regras:

1. worktree limpo antes do build;
2. commit contém código/teste/migration real, não somente `.md`;
3. `dotnet publish --configuration Release` em diretório novo;
4. nenhuma extensão `.pem`, `.key`, `.pfx` ou `.p12` no release;
5. nenhuma chave privada ou segredo incorporado;
6. `sha256sum --check --quiet --strict SHA256SUMS` passa;
7. troca por symlink/drop-in atômico, sem copiar sobre release em execução;
8. novo PID e InvocationID depois do restart;
9. inode mapeado pelo processo igual ao inode da DLL calculada;
10. porta loopback pertencente ao novo PID;
11. release anterior preservado;
12. rollback imediato se o teste real não produzir `200/200`.

Não executar `daemon-reload` quando apenas o symlink do release for alterado. Não
executar migration `down` automaticamente. O rollback binário deve ser compatível
com migrations forward aditivas/corretivas já aplicadas.

O retorno final na mesma tarefa precisa apresentar, sem novo handoff:

- causa confirmada, inclusive SQLSTATE e constraint;
- diff de código/migration/teste;
- SHA Git completo da correção;
- release, DLL SHA-256 e InvocationID;
- correlação das duas execuções reais;
- saída `200/200` e `RESULT=AUTHORIZED` nas duas;
- estado pós-teste da sessão, challenge, presença e outbox;
- confirmação de que nenhuma chave, autoridade, licença ou identidade foi alterada;
- caminho do backup e rollback testável, sem revelar segredos.

Sem esses itens, o servidor não está reparado.
