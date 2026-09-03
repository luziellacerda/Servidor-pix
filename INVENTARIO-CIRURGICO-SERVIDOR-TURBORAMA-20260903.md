# INVENTÁRIO CIRÚRGICO DO SERVIDOR TURBORAMA SUITE

Data: 2026-09-03

## Git

- Repositório: `luziellacerda/Servidor-pix`
- Estado local inventariado antes do novo commit: `c12604e`
- Working tree sem alterações pendentes.
- Nenhuma chave privada ou segredo versionado.

## Serviço ativo

- Unidade: `turborama-suite-api.service`
- Estado: `active/running`
- PID no momento do inventário: `1328687`
- Release: `r25-5-session-fix-20260903`
- DLL: `/opt/turborama-suite-r5-releases/r25-5-session-fix-20260903/api/TurboRamaSuiteOnlineServer.dll`
- SHA-256 do DLL: `d6678e3ff26f0b5879450dfedfafeda0489fb8fc6131d386a0d0b644578be78d`
- WorkingDirectory corresponde ao release ativo.

## Banco e schema

Migrations mais recentes registradas:

- `021_suite_inventory_challenge_session_history`
- `020_suite_device_inventory_r25`
- `019_suite_admin_customer_activity`
- `018_suite_transfer_enrollment_lock`
- `017_suite_commerce_session_permissions`

Tabelas críticas presentes:

- `suite.suite_licenses`
- `suite.suite_devices`
- `suite.suite_sessions`
- `suite.suite_device_presence`
- `suite.suite_connection_notification_outbox`

A migration 021 removeu somente a FK histórica entre challenges de inventário e `suite_sessions.session_id`, preservando o histórico e as validações de licença/dispositivo.

## Segurança verificada

- nenhuma licença recriada ou alterada;
- nenhum DeviceId ou fingerprint alterado;
- nenhuma autoridade, chave ou pin TLS alterado;
- nenhum serviço PIX, vendas, catálogo ou gateway alterado;
- backup do schema anterior preservado em `/var/backups/turborama-suite/schema-before-021-20260903.sql`;
- release anterior preservado para rollback;
- respostas públicas continuam sanitizadas.

## Saúde

- `GET /health`: aprovado anteriormente com status 200;
- `GET /ready`: aprovado anteriormente com status 200;
- serviço ativo após reinício e apontando para o release R25.5.

## Pendência controlada

O inventário não fabrica uma prova de cliente. O aceite funcional final ainda requer uma tentativa real do cliente para confirmar `POST /v1/suite/challenges=200` e `POST /v1/suite/sessions=200`, além de presença ONLINE e outbox idempotente.

Este relatório é somente inventário; não contém segredos e não altera produção.

