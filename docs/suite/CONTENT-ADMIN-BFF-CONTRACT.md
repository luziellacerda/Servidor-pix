# Contrato de produção — BFF do painel de conteúdo

## Fronteira implantada neste repositório

`TurboRamaSuiteAdminServer` expõe, no Unix socket administrativo já autenticado por
`X-Suite-Admin-Token`, todo o backend de catálogo necessário ao painel. A página real
`/admin/suite/content` e seu BFF estão implementados neste repositório em
`TurboRamaPixOnlineServer`, reutilizando o mesmo host administrativo, sessão, identidade visual e
isolamento Cloudflare do painel `/admin/suite`. Nenhuma resposta abaixo contém origem, hostname,
ciphertext, nonce, tag, chave ou qualquer derivação desses valores.

O BFF do `TurboRamaPixOnlineServer` é responsável por sessão Cloudflare/admin, autorização,
antiforgery e step-up.
Depois de validar esses controles, ele encaminha ao Unix socket:

- `X-Suite-Admin-Actor`: identidade administrativa normalizada;
- `X-Suite-Admin-Claims`: claims separados por espaço ou vírgula;
- `X-Suite-Client-Ip-Digest`: SHA-256 lowercase do IP já autenticado, nunca o IP em claro;
- em POST, `X-Suite-Csrf-Verified: 1` e `X-Suite-Step-Up-At` com instante Unix, máximo cinco minutos;
- em nova versão, um segundo `X-Suite-Version-Step-Up-At` e
  `X-Suite-Version-Confirmation: <itemId>:<artifactVersion atual>`.

O token interno não substitui esses cabeçalhos: as duas camadas são obrigatórias. Recusas são
auditadas por código genérico. O BFF não deve registrar body, header administrativo ou resposta
completa.

## Operações

- `GET /content/items?cursor=&limit=100&availability=&resultCode=&jobState=&item=&name=` exige
  `suite.content.read`. Retorna página por `itemId`, `ONLINE`/`EM_MANUTENCAO`, a
  título público sanitizado, `artifactVersion` atual quando o item está online, último check, código
  genérico e último job.
  Essa versão sanitizada existe para a confirmação explícita de nova versão; nenhum locator é exposto.
- `GET /content/jobs/{candidateId}` exige `suite.content.read`. Retorna estado e timestamps.
- `GET /content/audit?cursor=&limit=100&itemId=` exige `suite.content.read`. Retorna somente eventos
  allowlisted.
- `POST /content/items/{itemId}/origin-candidates` exige
  `suite.content.origin.replace`, CSRF e step-up. Body:
  `{ "candidateUrl": "<valor digitado>", "requestId": "<aleatório>", "actor": "<ator>" }`.
- `POST /content/items/{itemId}/versions` exige `suite.content.version.publish`, CSRF, dois step-ups
  e confirmação explícita. Body adicional:
  `{ "changeReason": "CONTENT_CORRECTION", "confirmedArtifactVersion": 1 }`.
  Reasons aceitos: `VENDOR_RELEASE`, `SECURITY_UPDATE`, `CONTENT_CORRECTION`, `PLATFORM_UPDATE`.
- `POST /content/items/{itemId}/checks` exige `suite.content.check`, CSRF e step-up. Body:
  `{ "requestId": "<aleatório>", "actor": "<ator>" }`.

O BFF deve aplicar Post/Redirect/Get, `autocomplete=off`, limpar o campo sensível após o POST e não
incluir o valor no histórico, flash message, query string, HTML re-renderizado ou telemetria. A CSP
não admite script inline. O formulário de nova versão é distinto do formulário de mirror.

## Credenciais e least privilege

Com `SUITE_CONTENT_ADMIN_ENABLED=1`, o processo administrativo abre uma segunda conexão a partir de
`SUITE_CONTENT_ADMIN_CONNECTION_FILE`, como role `turborama-suite-content-admin`. Essa role enxerga a
view sanitizada, health, auditoria e somente colunas não criptográficas dos jobs. A inserção passa por
funções `SECURITY DEFINER` com `search_path` fixo; ela não possui `SELECT` nas colunas cifradas.

O key ring de candidatos possui uma única fonte canônica em `/etc/credstore/candidate-url-keyring` e é
entregue separadamente ao admin e ao worker por `LoadCredential` pathless; cada processo recebe uma
montagem privada, somente leitura. O key ring de origem e a allowlist usam, respectivamente,
`/etc/credstore/content-url-keyring` e `/etc/credstore/content-allowed-hosts` com a mesma regra entre os
consumidores autorizados. Não se mantêm cópias manuais divergentes. Conexão e fontes de credenciais são
arquivos regulares privados, sem symlink. A migration 013 e `tests/check-suite-content-permissions.sql`
fixam a matriz de allow/deny.

A ativação do módulo de conteúdo no processo administrativo é um drop-in separado:
`turborama-suite-admin.service.d/content.conf`, acompanhado de `content.env`. A unit base de
commerce/OTP não declara essas credenciais. As credenciais pathless do drop-in são opcionais para o
systemd; a aplicação faz o fail-closed. Se o módulo estiver habilitado e sua conexão, key ring ou
allowlist estiver ausente/corrompida, somente `/content/*` responde `503`; o socket, commerce e OTP
continuam ativos, e `/readiness` sinaliza estado `degraded` com o check de conteúdo indisponível.
O runtime testa pela conexão restrita o usuário efetivo, markers/checksums, uma leitura mínima da view
sanitizada e `EXECUTE` nas quatro funções administrativas necessárias (`get context`, `submit`,
`request check` e `audit denial`), com timeout de três segundos. O gate específico
`/readiness/content` retorna `503` enquanto essa prova falhar; o readiness geral continua
`200/degraded` quando o núcleo comercial está saudável, para que uma credencial opcional de conteúdo
jamais reinicie ou retire OTP/comércio.

## Estados apresentados

`STAGED` é “aguardando validação”, `VALIDATING` é “validando”, `VERIFIED` é uma fase interna curta,
`PUBLISHED` é “publicado”, `REJECTED` é “origem recusada” e `SUPERSEDED` pede nova submissão porque o
catálogo mudou durante o trabalho. A interface não tenta explicar a falha com texto técnico: mostra o
código genérico retornado pelo backend.

## Worker e operação

`TurboRamaSuiteContentMonitor run-once` é disparado pelo timer 15 minutos depois do término do ciclo
anterior (`OnUnitInactiveSec`), com jitter e lock consultivo global. Um hash longo não perde o próximo
agendamento nem cria sobreposição. A validação integral usa a unit/timer independentes
`turborama-suite-content-candidate`, comando `candidate-once` e lock próprio; portanto um candidato de
até 512 GiB não bloqueia os checks periódicos. Ambos usam conexão e key rings próprios da role
`turborama-suite-content-monitor`.
Validações de candidato reutilizam diretamente as classes de política SSRF/TLS e assinatura do
publicador, fazem hash integral em streaming e não gravam artefato em disco.

Três falhas operacionais consecutivas promovem o item a manutenção. O guard global impede qualquer
snapshot resultante com mais de 25 itens em manutenção, somando os já indisponíveis e os promovidos no
ciclo, e preserva o snapshot ativo. Quando existe ETag ou Last-Modified esperado, valor ausente também
falha. Sem ETag forte vinculado ao corpo — inclusive quando existe apenas Last-Modified — até dois itens
vencidos por ciclo recebem revalidação integral de assinatura, tamanho e SHA-256 a cada sete dias;
Last-Modified é somente um sinal rápido. Falhas de segurança preservam o snapshot e geram alerta crítico.
Recuperação ocorre somente após candidato integralmente validado; um probe rápido nunca restaura item.
A publicação clona exatamente 850 IDs, recriptografa as origens READY com o novo AAD e troca o snapshot
por `suite.publish_suite_content_catalog` na mesma transação.

O candidato mantém heartbeat de lease durante todo probe/hash/publicação e cada transição usa CAS do
mesmo proprietário. Resultados de health usam CAS do catálogo, versão e `row_version`; promoções são
revalidadas sob o lock de publicação para que um resultado antigo não afete catálogo concorrente.

Imediatamente antes de cada mutação de snapshot, o monitor prova ao gateway loopback o conjunto
completo de versões do key ring de origem, sua versão ativa e o fingerprint canônico da allowlist.
Indisponibilidade ou qualquer divergência falha fechada antes da transação; nenhum material da prova é
registrado.

Um ciclo bloqueado por segurança ou mass-failure termina com código de processo não zero para o
systemd, nunca como sucesso. Alertas usam outbox deduplicada e são entregues ao journal operacional em
mensagens allowlisted (`severity`, `code`, contagem), sem item, candidato, hostname ou URL; só depois da
escrita o registro recebe `delivered_at`. Backlog remanescente é emitido apenas como contagem e uma falha
do ciclo também gera alerta crítico.

Aceite operacional requer o self-test do admin e do monitor, migrations 010–014/ledger íntegro, matriz
PostgreSQL 16, timers `turborama-suite-content-monitor` e `turborama-suite-content-candidate` ativos,
ciclo de health recente em readiness e dois canários: publicação válida e três falhas consecutivas
de um único item. A busca de vazamento deve cobrir HTML, JSON, logs, auditoria, alertas e executável.
