# INVENTÁRIO TÉCNICO COMPLETO E RASTREÁVEL — TURBORAMA SUITE

Data: 2026-09-03
Base comparada: `prod-baseline-20260828T144059Z` / `c67b089`
Estado Git analisado: branch de produção e commits R25

## 1. Cadeia de execução

Cliente Windows -> HTTPS/Nginx -> `turborama-suite-api` ou `turborama-pix` -> PostgreSQL/estado protegido -> assertions assinadas -> cliente.

O painel SUITE passa pelo BFF do PIX e pelo serviço admin isolado. Conteúdo passa pela API/gateway e workers de grants/outbox. Nenhum segredo deve atravessar Git, cliente ou resposta pública.

## 2. Projetos rastreados

### TurboRamaSuiteOnlineServer

Arquivos principais:

- `Program.cs`: host, DI, middleware, timeout/rate-limit, health/readiness e rotas SUITE.
- `Contracts.cs`: records de requests/responses, JSON estrito e `SuiteException`.
- `Protocol.cs`: ProductId, versões, hashes de contexto, canonicalização e verificação de proof.
- `RequestGuards.cs`: limites, timeout e controle de requisições.
- `Signing.cs`: assinatura de assertions emitidas pelo servidor.
- `Store.cs`: acesso PostgreSQL, challenges, ativação, sessões e transações.
- `SuiteService.cs`: regras de ativação, challenge, prova, sessão e heartbeat.
- `appsettings*.json`: defaults/exemplos, sem segredos.

Funções críticas:

- `ActivationChallengeAsync`: valida contrato, licença, enrollment e código.
- `CompleteActivationAsync`: valida challenge/proof, grava dispositivo e consome ativação.
- `ChallengeAsync`: autoriza operação e emite challenge.
- `SessionAsync`: valida proof, licença, dispositivo, fingerprint e fecha sessão.
- `ActiveLicense`: exige produto, ACTIVE, termo e limite corretos.
- `RequireEnrollment`: compara identidade cadastrada campo a campo.
- `CompleteSessionAsync`: transação serializável, sessão, presença, outbox e challenge.
- `Consume`: consumo único e expiração do challenge.
- `Protocol.Verify/SigningMessage/ContextHash`: domínio criptográfico e proof.

### TurboRamaPixOnlineServer

- `Program.cs`: rotas PIX, configuração e integração dos painéis.
- `ServerCore.cs`: estado protegido, licenças, dispositivos, preços, auditoria, sessões e Mercado Pago.
- `OnlineLicenseProtocol.cs`: protocolo online legado, DeviceId/SPKI, context hashes e proof.
- `AdminPanel.cs`: login, logout, CSRF, dashboard, ações, auditoria e assets.
- `SuiteAdminBff.cs`: ponte autorizada para o admin SUITE.
- `SuiteAdminPanel.cs`: painel de licença/OTP, confirmação e auditoria.

Funções operacionais do estado protegido:

- criação/listagem de licença e dispositivos;
- alteração de status;
- force reauthentication;
- emissão de código de ativação;
- transferência controlada;
- configuração de preços;
- auditoria e retenção;
- conexão Mercado Pago criptografada.

### TurboRamaSuiteAdminServer

- `Program.cs`: health, consulta de status, emissão/negação de OTP e exportação de auditoria.
- `SuiteEligibility.cs`: decisão de elegibilidade com política fail-closed.

## 3. Endpoints

### API SUITE

- `GET /health`: health local da API SUITE.
- `GET /ready`: readiness e dependências.
- `POST /v1/suite/challenges`: challenge de sessão/operação.
- `POST /v1/suite/sessions`: proof e abertura/renovação de sessão.
- demais rotas de catálogo, inventário e conteúdo são registradas por mapeamentos no `Program.cs`/release correspondente.

### API PIX/online

- `GET /v1/health`
- `POST /v1/activations/challenge`
- `POST /v1/activations/complete`
- `POST /v1/challenges`
- `POST /v1/sessions`
- `POST /v1/orders`
- `POST /v1/orders/status`
- `POST /v1/configuration/read`
- `POST /v1/configuration/write`

### Admin

- login/logout e assets em `/admin`;
- dashboard e auditoria PIX;
- `/admin/suite`;
- `/admin/suite/actions/issue-otp`;
- `/admin/suite/export/audit.csv`;
- BFF local para `/status`, `/issue`, `/deny`, `/audit.csv`.

## 4. Banco e tabelas

Schema principal: `suite`.

Entidades:

- `suite_licenses`: produto, status, termo, pagamento, entrega, gerações e limite.
- `suite_license_enrollments`: identidade originalmente vinculada.
- `suite_devices`: dispositivo ativo/revogado, SPKI, fingerprint e algoritmo.
- `suite_challenges`: nonce, action, contexto, validade e consumo.
- `suite_activation_completions`: idempotência/replay de ativação.
- `suite_sessions`: sessão corrente por licença/dispositivo.
- `suite_device_inventory_challenges`: histórico de inventário.
- `suite_device_presence`: ONLINE/OFFLINE e cooldown.
- `suite_connection_notification_outbox`: eventos WhatsApp idempotentes.
- auditoria, catálogo, grants, entregas, transferências e permissões de conteúdo.

Migrations conhecidas: fundação, licença vitalícia/dispositivo único, OTP/auditoria, conteúdo e permissões, commerce/session, inventário R25 e `021_suite_inventory_challenge_session_history`.

## 5. Fluxo completo de uma licença

1. venda/pedido é registrado;
2. pagamento e entrega são conferidos;
3. licença é criada/ativada;
4. admin emite código/OTP elegível;
5. cliente gera/recupera identidade local;
6. challenge de ativação é pedido;
7. servidor valida LicenseId, ProductId, enrollment e descriptor;
8. cliente assina proof;
9. servidor grava dispositivo e consome challenge;
10. cliente abre sessão;
11. servidor valida proof e fingerprint;
12. sessão, presença e outbox são gravadas;
13. cliente usa catálogo/assertions e grants temporários;
14. heartbeats renovam presença/sessão;
15. revogação/status encerra acesso.

## 6. Segurança criptográfica

- DeviceId é derivado do SPKI da máquina.
- HardwareFingerprint é comparado no servidor.
- Proof usa domínio fixo e hashes canônicos.
- Assertions são assinadas pelo emissor online.
- Autoridade de conteúdo é separada da autoridade de licenciamento.
- TLS é restringido por pin configurado.
- Chaves privadas permanecem offline.
- Respostas de erro são sanitizadas; logs usam correlação.
- Replay, challenge expirado, duplicidade JSON e campos nulos são rejeitados.

## 7. Painel, OTP e permissões

O painel exige autenticação, claims de permissão, antiforgery e senha de elevação para emitir OTP. OTP é de uso único, expira e é auditado. Negação, emissão, reautenticação e alterações administrativas devem gerar evento. Exportação CSV é protegida.

## 8. Inventário, presença e WhatsApp

O cliente publica inventário de placa-mãe, BIOS, Windows, programa e fingerprint por operação autorizada. O servidor persiste o inventário cifrado/validado conforme release.

Sessão válida atualiza presença. A notificação WhatsApp é apenas uma consequência assíncrona via outbox; falha do worker não pode negar licença ou sessão. Cooldown e chave de evento impedem duplicação.

## 9. Correção R25 aplicada

Causa: FK `suite_device_inventory_challenges_session_id_fkey` com NO ACTION apontava histórico consumido para `suite_sessions.session_id`. Nova sessão substituía o ID pai e gerava SQLSTATE 23503, convertido em 500.

Correção:

- backup do schema;
- remoção somente dessa FK;
- migration `021_suite_inventory_challenge_session_history`;
- preservação da coluna histórica e demais FKs;
- release `r25-5-session-fix-20260903`;
- permissões corrigidas;
- serviço reiniciado.

## 10. Serviços e operação

- `turborama-pix.service`
- `turborama-suite-api.service`
- `turborama-suite-admin.service`
- `turborama-suite-content-gateway.service`
- workers de presença/WhatsApp
- `nginx.service`

O serviço ativo confirmado usa o release R25.5 e o DLL SHA-256 `d6678e3ff26f0b5879450dfedfafeda0489fb8fc6131d386a0d0b644578be78d`.

## 11. Diagnóstico rastreável

Registrar unidade, PID, InvocationID, DLL resolvido, inode, hash, release, commit, correlação, endpoint e status. Para 500, registrar internamente tipo de exceção e SQLSTATE/schema/constraint, sem parâmetros ou segredos.

Resultados:

- challenge 200 + session 200: fluxo aprovado;
- challenge 403: licença/enrollment/identidade;
- session 409: challenge/replay/conflito;
- session 500: exceção interna;
- health 200 sozinho não é aceite funcional.

## 12. Backup e rollback

Backup de schema: `/var/backups/turborama-suite/schema-before-021-20260903.sql`.

Manter release anterior. Rollback: apontar drop-in ao release anterior, `systemctl daemon-reload`, reiniciar serviço e validar `/health`, `/ready` e fluxo seguro. Nunca apagar histórico para mascarar erro.

## 13. Commits rastreados

Baseline e evolução incluem autoridade isolada, schema SUITE, políticas de licença/dispositivo, OTP, concorrência, conteúdo, gateway, catálogo, inventário, presença, WhatsApp, correção da FK e documentação. Commits de correção real:

- `63c112b`: migration da FK histórica;
- `42884d5`: inventário cirúrgico;
- commits posteriores: documentação de retorno e relação baseline/atual.

## 14. Aceite final

Confirmar com cliente real: challenge 200, session 200, assertion válida, sessão ACTIVE, presença ONLINE, outbox sem duplicação, heartbeat, catálogo assinado e ausência de segredos em logs/Git.

