# Handoff de produção — administração e reconhecimento automático de origens

> Estado do repositório: migration 013, backend interno, worker, timer, isolamento de roles, testes e
> a página/BFF real `/admin/suite/content` do `TurboRamaPixOnlineServer` foram implementados. O contrato
> implantado está em `docs/suite/CONTENT-ADMIN-BFF-CONTRACT.md`.

## Objetivo e fronteira

Implementar no painel administrativo existente uma página para o catálogo do TurboRama Suite e um
worker periódico no servidor. A página mostra somente título público sanitizado, `itemId`,
`ONLINE`/`EM MANUTENÇÃO`, versão sanitizada, resultado genérico do último check, data UTC e estado de
uma eventual substituição. Ela permite enviar uma nova origem e solicitar novo check. Nenhuma rota de
leitura, HTML, JSON, CSV, log, auditoria, alerta ou métrica
pode conter URL, ciphertext, nonce, tag, token ou grant.

Não alterar checkout, PIX, webhooks, entregas, preço, licença ou OTP. O cliente continua recebendo o
mesmo catálogo `READY`/`MAINTENANCE`; o gateway continua emitindo bytes somente para `READY`.

Este contrato deve ser implantado como uma mudança única: migration forward-only, backend interno,
BFF/página, worker/timer, permissões e testes. Não criar uma tela que edite diretamente o snapshot
ativo. Os snapshots publicados são imutáveis.

O retorno final do servidor prova o baseline exato 001–009 e que 010–012 ainda não foram aplicadas.
Antes da primeira mutação, `apply-suite-content-migrations.sh` reconfirma esse conjunto, valida o
manifesto SHA-256 de 010–014 e bloqueia qualquer marker inesperado. Marker e checksum imutável são
gravados atomicamente dentro de cada migration; após o primeiro deploy, alterações exigem nova migration
forward-only, nunca edição retroativa.

## Modelo de dados obrigatório (migration 013)

Criar tabelas no schema `suite`, todas sem privilégios para `PUBLIC`, API de conteúdo e gateway:

1. `suite_content_item_health`
   - chave `(product_id,item_id)`, com produto fixo `TURBORAMA_SUITE` e `item_id` lowercase de 32 hex;
   - `observed_catalog_identity`, `observed_availability` (`READY|MAINTENANCE`);
   - `last_checked_at`, `last_success_at`, `last_terminal_failure_at`, `next_check_at`;
   - `consecutive_terminal_failures`, `consecutive_successes` e `last_result_code` de allowlist;
   - `updated_at` e versionamento otimista; nenhuma coluna de URL.
2. `suite_content_origin_candidates`
   - `candidate_id` aleatório de 64 hex, `item_id`, `base_catalog_identity` e `request_id` único;
   - estado `STAGED|VALIDATING|VERIFIED|REJECTED|SUPERSEDED|PUBLISHED`;
   - somente `upstream_url_ciphertext`, nonce de 12 bytes, tag de 16 bytes e `key_version`;
   - metadados verificados nulos até `VERIFIED`: tamanho positivo, SHA-256 lowercase, extensão segura,
     nome final <= 180 bytes UTF-8, política de extração, ETag/Last-Modified sanitizados;
   - `change_intent` obrigatório (`MIRROR_REPLACEMENT|INITIAL_RECOVERY|NEW_ARTIFACT_VERSION`) e, para mirror,
     `expected_content_length`, `expected_sha256` e `expected_artifact_version` copiados atomicamente
     do descriptor READY ativo no momento da submissão;
   - `submitted_by`, timestamps, `attempt_count`, `lease_owner`, `lease_expires_at` e código interno
     allowlisted. Nunca guardar URL em plaintext ou mensagem livre.
3. `suite_content_management_audit`
   - evento allowlisted, ator, item, candidate/job ID, correlation/request ID, resultado, código genérico,
     snapshot anterior/novo e data; append-only; nenhuma mensagem ou URL livre.
4. `suite_content_alert_outbox`
   - somente item, severidade, código genérico, contadores e timestamp; entrega idempotente. O payload
     enviado a e-mail/chat/monitoramento também não contém URL.

O AAD do candidato deve ser estável e separado do AAD da origem publicada:

```text
TurboRamaSuiteContentCandidateUrl/v1\0
{"candidateId":"<64hex>","itemId":"<32hex>","baseCatalogIdentity":"<64hex>","keyVersion":1}
```

JSON UTF-8 sem whitespace e nessa ordem. AES-256-GCM usa a mesma forma de key ring versionado. Existe
uma única fonte canônica root-owned para cada key ring; `systemd LoadCredential` entrega uma projeção
privada e somente leitura a cada processo autorizado. Não criar cópias persistentes por serviço. O
worker zera buffers de URL depois do uso.

Criar roles distintas `turborama-suite-content-admin` e `turborama-suite-content-monitor`. O admin pode
listar health/audit e inserir candidato por função estreita `SECURITY DEFINER` com `search_path` fixo;
não recebe `SELECT` nas colunas criptográficas. O monitor pode selecionar/atualizar candidatos, health,
staging snapshots e origens. O gateway não acessa candidatos/health/audit; o control-plane pode ler
health somente se houver necessidade explícita de readiness. Testar `has_table_privilege` e
`has_column_privilege` para todos os allow/deny.

## Endpoints internos e página

Backend Unix-socket autenticado pelo token interno existente:

- `GET /content/items?cursor=<opaque>&limit=100`: retorna apenas item, disponibilidade, último check,
  código genérico e estado do job; paginação obrigatória; nunca retorna origem nem sinal derivado dela.
- `POST /content/items/{itemId}/origin-candidates`: substituição de mirror; corpo
  `{candidateUrl,requestId,actor}`; máximo 4096 bytes. Para item READY, captura tamanho, SHA e versão do
  descriptor ativo. Para MAINTENANCE, busca o descriptor READY publicado mais recente e mantém a mesma
  identidade; somente quando o item nunca teve descriptor usa `INITIAL_RECOVERY` e cria a versão 1 após
  validação integral. Valida sintaxe HTTPS/443/host allowlisted antes de criptografar e responde `202` com
  `{candidateId,state:"STAGED"}`. O objeto de resposta não ecoa a URL.
- `POST /content/items/{itemId}/versions`: ação distinta para conteúdo realmente novo; exige
  `{candidateUrl,requestId,actor,changeReason}` com reason code allowlisted, novo step-up e claim
  `suite.content.version.publish`. Nunca reutilizar a ação de mirror para mudar bytes.
- `POST /content/items/{itemId}/checks`: corpo `{requestId,actor}`; enfileira check e responde `202`.
- `GET /content/jobs/{candidateId}`: retorna somente estado, timestamps e código genérico.

Na aplicação web existente:

- `GET /admin/suite/content` exige sessão Cloudflare/admin e claim `suite.content.read`;
- os POSTs exigem `suite.content.origin.replace` ou `suite.content.check`, antiforgery same-origin,
  senha administrativa de step-up, limite por ator/IP/item e request ID aleatório;
- criação de nova versão exige permissão separada, nova confirmação explícita do item e versão atual,
  segundo step-up e evento de auditoria `CONTENT_NEW_VERSION_REQUESTED`;
- formulário usa `autocomplete=off`; após POST, aplicar PRG e limpar histórico; CSP sem script inline;
- a URL existe somente no campo POST e na memória de backend até a criptografia. Nunca reexibir a URL,
  nem mascarada, nem como domínio.

Registrar recusas de autenticação/CSRF/rate limit sem chamar o worker. Erros externos são sempre
mensagens genéricas em português; detalhes técnicos ficam em códigos allowlisted sem URL.

## Worker automático e histerese

Executar health por `systemd timer` 15 minutos após o término do ciclo anterior, com jitter e lock
consultivo. Executar a fila de candidatos em unit/timer independentes, também com lock próprio, para um
hash de até 512 GiB nunca bloquear o SLA de health. Concorrência padrão 2, máximo 4. Reutilizar
exatamente a política SSRF/TLS/DNS pública, `UseProxy=false`, sem cookies, sem redirect, UA fixo, Range
`0-0` e HEAD seguro do publisher/gateway.

Para item `READY`:

1. Descriptografar sua origem somente em memória e fazer probe.
2. Um sucesso zera falhas terminais e atualiza `last_success_at`.
3. 404/410/416/zero, rede, resolução DNS pública, timeout, 408/425/429/5xx, leitura ociosa e divergência
   de tamanho, depois dos retries internos, incrementam o contador apenas uma vez por janela de 15 minutos.
   Se o descriptor possui ETag ou Last-Modified, o probe precisa devolver exatamente o mesmo valor;
   ausência também é divergência. Somente um ETag forte observado de forma idêntica no probe e no GET
   integral é identidade vinculada ao corpo. Toda origem sem esse ETag forte, inclusive a que possui apenas
   Last-Modified, recebe revalidação integral de tamanho, assinatura e SHA-256 no máximo a cada sete dias,
   limitada a dois hashes por ciclo para não reler o catálogo inteiro a cada 15 minutos. Last-Modified é
   apenas um sinal rápido de mudança e nunca substitui o hash periódico.
4. Somente três falhas operacionais consecutivas promovem a mudança para `MAINTENANCE`.
5. TLS/certificado, redirect, encoding, host/scheme/porta negado, DNS resolvendo para IP privado/NAT64
   ou qualquer violação de segurança abortam o ciclo global, preservam o snapshot e geram alerta
   sanitizado. Uma única falha nunca muda disponibilidade.
6. Se o snapshot resultante teria mais de 25 itens em manutenção, inclusive os que já estavam nesse
   estado antes do ciclo, o mass-failure guard não publica mudança alguma. Ele é reavaliado dentro da
   transação de publicação, preserva o snapshot ativo e abre alerta crítico para intervenção.

Ao confirmar manutenção, o worker clona os 850 IDs para um snapshot `STAGING`, marca somente o item
como `MAINTENANCE`, remove descriptor e origem desse item, recriptografa as demais origens porque o AAD
inclui o novo `catalogIdentity`, valida `READY + MAINTENANCE = 850` e `origins = READY`, e só então chama
a função de publicação atômica. O snapshot anterior permanece íntegro para rollback.

Para candidato `STAGED`:

1. Adquirir lease com `FOR UPDATE SKIP LOCKED`; marcar `VALIDATING`.
2. Validar URL/host/DNS/TLS/redirect e extensão. Falha terminal marca apenas o candidato `REJECTED` e
   mantém o catálogo atual.
3. Fazer download integral por streaming para SHA-256, com timeout de inatividade, sem salvar artefato.
4. No mesmo stream, aplicar a validação de Content-Type, assinaturas/offsets e XML definida no publisher.
5. Em `MIRROR_REPLACEMENT`, exigir igualdade exata do tamanho e SHA-256 com o descriptor READY capturado;
   qualquer diferença marca o job `REJECTED` e mantém o snapshot. `INITIAL_RECOVERY` só é válido quando
   nenhum snapshot publicado contém descriptor READY para o item. Em `NEW_ARTIFACT_VERSION`, exigir a
   autorização reforçada/auditoria e incrementar `artifactVersion`; nunca sobrescrever a versão atual.
6. Confirmar extensão, nome e política de extração; marcar `VERIFIED`.
7. Clonar 850 IDs, substituir somente o item, recalcular descriptor e identidade, recriptografar todas
   as origens com o novo AAD, publicar atomicamente e marcar candidato `PUBLISHED`.
8. Um item em manutenção volta a `READY` somente por esse fluxo integral; probes rápidos isolados não
   restauram disponibilidade. Isso é a histerese de recuperação e evita flapping.

O lease é renovado por heartbeat durante probe, download, hash, prova do gateway e publicação. Toda
transição exige simultaneamente `candidate_id`, estado e o mesmo `lease_owner`, com exatamente uma linha
alterada; um worker antigo jamais pode rejeitar, sobrescrever ou publicar o trabalho assumido por outro.
Resultados de health também usam CAS de catálogo, versão do artefato e `row_version`, e as promoções são
revalidadas sob o lock consultivo de publicação para não atingir um snapshot publicado concorrentemente.

Jobs abandonados após lease expirada podem ser retomados idempotentemente. Journal de hash contém
somente ID, tamanho, SHA e validadores; nunca URL. Se o conteúdo for alterado entre probe e hash, abortar
e recomeçar o item. Nunca inventar hash e nunca copiar o arquivo para disco.

## Operação e aceite

- Unit e timer sem segredos em `Environment=`; paths de connection/key/allowlist são absolutos e `0600`.
- `ProtectSystem=strict`, `PrivateTmp=true`, `NoNewPrivileges=true`, `RestrictSUIDSGID=true`, sem acesso
  ao webroot, Git ou diretórios do gateway/API.
- readiness exige migrations, worker sem lease vencida, total 850, soma de estados 850 e origem somente
  para READY. A indisponibilidade do worker não deve derrubar downloads READY já publicados.
- métricas permitidas: contagem por estado/código e idade do último check; labels nunca contêm item URL,
  token, candidate ID ou grant.
- testes: CSRF/claims/step-up/rate limit, URL nunca em GET/log/audit, SSRF IPv4/IPv6/NAT64, redirect,
  retry/limiar/histerese, dois workers concorrentes, crash/restart, hash/tamanho alterado, snapshot de 850,
  origem ausente em manutenção, rollback e matriz de privilégios PostgreSQL 16.

Aceite final exige uma substituição canário por item de teste: a página mostra `VALIDANDO`, o worker
calcula SHA integral, publica novo snapshot e mostra `ONLINE`; em seguida um cenário 404 repetido três
vezes publica apenas esse item `EM MANUTENÇÃO`. Em nenhum passo a busca por `http`, hostname real ou URL
submetida pode encontrar valor em HTML, JSON de leitura, CSV, logs, auditoria ou executável cliente.
