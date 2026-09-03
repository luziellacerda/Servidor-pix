# ORDEM FINAL — CORRIGIR `catalog.read / ACTION_INVALID` E VALIDAR O FLUXO R25 COMPLETO

Data: 2026-09-03  
Repositório: `https://github.com/luziellacerda/Servidor-pix`  
Branch: `codex/turborama-suite-vendas-producao-20260828`

## 1. Objetivo e regra de execução

Corrigir o servidor de produção que já aceita a abertura da sessão, mas rejeita a primeira operação de conteúdo exigida pelo cliente Windows R25.

Esta é uma ordem única de implementação e aceite. Permanecer na mesma tarefa até concluir todos os itens. Não solicitar nova licença, novo OTP, nova autorização da máquina, nova chave ou recompilação do cliente. Não alterar PIX, vendas, painel, identidade do cliente, TLS, autoridades, links ou design.

## 2. Prova reproduzida no cliente real

Executável testado:

`Turborama-2.0.0-win-x64-c2d41a964174-PRODUCAO-FUNCIONAL-UNSIGNED/Turborama.exe`

SHA-256 do executável:

`dd0a83a695a7a20209335238c7e24d2e98a4e179f7dbf5336e0d047a1b652b02`

Commit do cliente:

`c2d41a96417454e715ee666a58c3c04727079c4d`

Correlação do teste completo:

`r25diag-20260903145236-0c153fb9`

Resultado objetivo, na ordem exata:

```text
CONTENT_METADATA_COUNTS=1,1,1
CONTENT_ENVELOPE_SHA256=56e7a1bd100e5b5a9cd1109c0b90edfcafaf6c1497ae3113551684d872a0ba07
CONTENT_ISSUER_SHA256=65631e0aaa9efb75991098f7a68dda462004e5bbc183ae6bf4ff9256397a8dc5
CONTENT_VERIFY=OK:2027-09-02T22:39:56Z
POST /v1/suite/challenges -> 200                 (session.open)
POST /v1/suite/sessions -> 200
SESSION_RESULT=AUTHORIZED
PUBLIC_CATALOG_ITEMS=902
POST /v1/suite/challenges -> 400                 (catalog.read)
CODE=ACTION_INVALID
STAGE=AUTHORIZED_CATALOG_READ
```

Conclusão incontornável:

- licença, dispositivo, identidade local, proof, autoridade de licenciamento e sessão estão corretos;
- a correção da FK da migration 021 resolveu o `500` anterior da sessão;
- o erro visual aparece somente depois da sessão autorizada, na leitura do catálogo;
- o endpoint de catálogo ainda nem é alcançado, pois a emissão do challenge `catalog.read` é recusada antes;
- a causa atual está no servidor ativo, que usa uma allowlist/contrato sem as ações de conteúdo R25.

## 3. Inconsistência do Git que deve ser corrigida antes do próximo deploy

O tree atual do branch remoto em `d93a66f893c164426f6ea7012f5be1f03a75ec1f` não é uma fonte R25 completa e reproduzível:

- contém a migration 021 e documentos;
- não contém as migrations 010–020 no histórico alcançável desse tree;
- seu `SuiteService.cs` aceita somente `session.open` e `session.heartbeat`;
- não contém os componentes completos de conteúdo, presença e inventário usados pelo release descrito nos relatórios.

O tree R25 conhecido em `bbefbdf3967c7a4cf900984b27e764d66e33c000` contém a implementação integrada necessária, incluindo:

- `ContentContracts.cs`;
- `ContentProtocol.cs`;
- `ContentService.cs`;
- `ContentStore.cs`;
- `SuiteService.cs` com `catalog.read` e `download.authorize`;
- `Protocol.cs` com domínios criptográficos próprios dessas ações;
- `RequestGuards.cs` com classificação e limites de conteúdo;
- `Program.cs` com registro das rotas `/v1/suite-content/*`;
- migrations 010–020;
- testes de conteúdo.

Não fazer uma correção parcial copiando apenas uma string para a allowlist. Primeiro tornar o tree R25 completo alcançável no branch de produção, preservando a migration 021 por cima dele. O commit final deve ser autossuficiente e permitir rebuild a partir de um clone limpo.

## 4. Comportamento obrigatório da API SUITE

O `POST /v1/suite/challenges` deve aceitar exatamente estas ações já previstas no contrato R25:

```text
session.open
session.heartbeat
catalog.read
download.authorize
```

As ações de conteúdo devem:

1. validar contrato, produto, licença e dispositivo;
2. exigir que `(license_id, device_id, session_id)` corresponda a uma sessão `ACTIVE` e ainda válida;
3. criar e persistir challenge de uso único com o `context_hash` recebido;
4. emitir assertion assinada com o kind e domínio específicos, sem reutilizar o domínio de heartbeat:
   - `catalog.read` -> `TURBORAMA_SUITE_CATALOG_READ_CHALLENGE`;
   - `download.authorize` -> `TURBORAMA_SUITE_DOWNLOAD_AUTHORIZE_CHALLENGE`;
5. manter replay, expiração, revogação, rate limit e consumo transacional existentes.

Também devem estar ativas as rotas:

```text
POST /v1/suite-content/catalog/current
POST /v1/suite-content/downloads/authorize
```

O catálogo deve ser assinado pela autoridade pública de conteúdo já implantada. Não trocar a autoridade, o envelope, o emissor ou os pins do cliente.

## 5. Banco de dados — verificar, não recriar

Confirmar no banco real, de forma somente leitura antes do deploy:

```sql
SELECT version
FROM suite.schema_migrations
WHERE version IN (
  '010_suite_content_catalog',
  '016_suite_content_catalog_902',
  '020_suite_device_inventory_r25',
  '021_suite_inventory_challenge_session_history'
)
ORDER BY version;

SELECT pg_get_constraintdef(oid)
FROM pg_constraint
WHERE conrelid = 'suite.suite_challenges'::regclass
  AND conname = 'suite_challenges_action_check';
```

A constraint de `suite.suite_challenges.action` precisa aceitar `catalog.read` e `download.authorize`. A FK histórica removida pela migration 021 deve continuar ausente. Se migrations 010–020 já estiverem registradas e os checksums coincidirem, não reaplicá-las nem alterar seus registros manualmente.

Não recriar licença, enrollment, dispositivo, sessão, catálogo, snapshot ou chaves.

## 6. Build e deploy corretos

1. Trabalhar a partir do tree R25 completo e limpo.
2. Integrar a migration 021 nesse tree sem perder nenhum componente R25.
3. Restaurar, compilar e executar todos os testes da API SUITE e conteúdo.
4. Criar commit funcional novo; não usar um commit apenas documental como fonte do binário.
5. Publicar em um diretório de release novo e imutável.
6. Gerar `RELEASE-METADATA` e `SHA256SUMS` com o SHA Git completo.
7. Verificar que nenhuma chave privada, DSN, senha, token, proof ou segredo entrou no release ou Git.
8. Preservar o release anterior e o backup do schema.
9. Trocar o apontamento da unidade de forma atômica e reiniciar somente `turborama-suite-api.service`.
10. Provar PID, `InvocationID`, caminho real da DLL, inode carregado e SHA-256 do binário novo.

Não copiar DLL por cima do processo ativo. Não publicar novamente o binário `d6678e3f...` se ele continuar retornando `ACTION_INVALID` para `catalog.read`.

## 7. Teste de aceite obrigatório, ponta a ponta

Usar o mesmo cliente/protocolo de produção e uma correlação nova. Não imprimir proof, assertion completa, bearer token, chave, DSN ou URL de origem.

### 7.1 Sessão

Exigir:

```text
POST /v1/suite/challenges [session.open] -> 200
POST /v1/suite/sessions -> 200
assertion de sessão validada pelo cliente
sessão ACTIVE
presença ONLINE
outbox idempotente, sem duplicação
```

### 7.2 Catálogo de 902 itens

Com a sessão recém-aberta, exigir:

```text
POST /v1/suite/challenges [catalog.read] -> 200
POST /v1/suite-content/catalog/current -> 200
assertion de conteúdo validada pelo cliente
paginação progride até nextCursor=null
READY + MAINTENANCE = 902
nenhum item ausente, extra ou repetido
```

Repetir as páginas com os context hashes e cursors canônicos. O primeiro challenge `catalog.read` não pode retornar `ACTION_INVALID`, `CONTENT_DISABLED` ou `CONTENT_NOT_READY` no ambiente declarado pronto.

### 7.3 Autorização e download

Selecionar um item `READY` real e exigir:

```text
POST /v1/suite/challenges [download.authorize] -> 200
POST /v1/suite-content/downloads/authorize -> 200
grant assinado validado pelo cliente
GET same-origin com Bearer -> 200 ou 206
Content-Length/Content-Range coerente
retomada por Range funcional
```

Baixar somente uma faixa pequena no smoke test, limpar o arquivo temporário e não registrar o bearer. Um item `MAINTENANCE` deve permanecer indisponível de forma controlada, sem derrubar o catálogo.

### 7.4 Estabilidade

Durante todos os testes, o PID, o `InvocationID` e `NRestarts` devem permanecer estáveis. Confirmar `/health=200` e `/ready=200`, mas não tratá-los como substitutos dos testes funcionais acima.

## 8. Resposta final obrigatória no mesmo branch

Somente declarar produção liberada após publicar um único retorno contendo:

- commit funcional completo usado no build;
- release e caminho da DLL;
- SHA-256 da DLL;
- PID e `InvocationID`;
- correlação do teste;
- status de cada rota das seções 7.1–7.3;
- total `READY`, `MAINTENANCE` e total final 902;
- resultado do Range GET;
- prova de sessão ACTIVE, presença ONLINE e outbox idempotente;
- confirmação de que licença, dispositivo, autoridades, TLS, PIX, vendas e links não foram alterados;
- caminho de rollback preservado.

Se qualquer item falhar, não escrever “tudo OK”. Informar a rota, o HTTP status, o código sanitizado, a correlação e o estágio exato, corrigir na mesma tarefa e repetir o fluxo completo.

