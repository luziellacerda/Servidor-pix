# Error catalog v1

Errors are strict UTF-8 JSON `{schemaVersion,code,message}` and never include identifiers or internal
diagnostics. Stable codes: `SUITE_DISABLED`, `BODY_INVALID`, `JSON_INVALID`, `CONTRACT_INVALID`,
`PRODUCT_DENIED`, `LICENSE_NOT_FOUND`, `LICENSE_DENIED`, `ACTIVATION_INVALID`, `ACTIVATION_REPLAY`,
`DEVICE_DENIED`, `ACTION_INVALID`, `CONTEXT_INVALID`, `CHALLENGE_INVALID`, `CHALLENGE_MISMATCH`,
`PROOF_INVALID`, `REPLAY_DENIED`, `SESSION_INVALID`, `INTERNAL_ERROR`.

The edge must preserve JSON, status, content type and body without redirect, HTML or compression.
Core request budgets allow 30 requests/minute per origin, license, device, route and app;
five-second heartbeats use separate challenge/proof windows. A NAT may track 4096 such
windows in each bucket. Pool defaults reserve database capacity for other services.
The fixed client treats 4xx as authoritative denial; consult the measured capacity report
before increasing production concurrency.


## ES compartilhado e rede complementar 1.1.0

- `CLIENT_SCOPE_INVALID` (400): cabeçalho ES inválido, repetido ou fora de rota/método.
- `EMULATIONSTATION_DISABLED` (503): extensão desabilitada, sem fallback Suite.
- `CONFLICT` na assertion ES assinada (HTTP 200) descreve a política anterior do
  release implantado `34e31f2`: sessão anterior vigente, validade vazia e nenhuma
  autorização; cliente apresenta `ES_SESSION_CONFLICT` (409 local). A correção
  local de 05/09/2026, ainda não implantada, passa a substituir a sessão ES por
  nova abertura validada, como a Suite, e não emite conflito por mera ocupação.
  Heartbeat do identificador anterior recebe `SESSION_INVALID`; o cliente continua
  obrigado a negar qualquer resposta sem autorização válida, inclusive do servidor antigo.
- `NETWORK_INVENTORY_DISABLED` (503), `NETWORK_CONTRACT_INVALID` (400),
  `NETWORK_UNAVAILABLE` (503): complemento indisponível ou inválido, sem cancelar licença.
- `CHALLENGE_INVALID`, `PROOF_INVALID`, `SESSION_INVALID`: provas e sessões inválidas
  também se aplicam ao contrato de rede, sempre com vínculo de aplicação.
- `RATE_LIMITED` (429), `REQUEST_TIMEOUT` (504): limites de tráfego/tempo; não são decisão
  de titularidade baseada em IP. Mudança isolada de MAC/IP não revoga autorização.

O backend administrativo usa respostas internas `{code}`: `PERMISSION_DENIED`,
`TARGET_INVALID`, `SESSION_CHANGED`, `REQUEST_CONFLICT`, `SESSIONS_UNAVAILABLE`,
`REVOKED` e `ALREADY_REVOKED`. Somente ações com CSRF, senha recente e alvo protegido
chegam ao CAS. Erros do BFF são apresentados sem detalhes privados do banco.
