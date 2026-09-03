# RETORNO — EXECUÇÃO DO REPARO DE SESSÃO R25

Data: 2026-09-03

## Execução realizada

- Release de produção criado: `r25-5-session-fix-20260903`
- DLL publicado: `TurboRamaSuiteOnlineServer.dll`
- SHA-256 do DLL: `d6678e3ff26f0b5879450dfedfafeda0489fb8fc6131d386a0d0b644578be78d`
- Drop-in do serviço atualizado para o novo release.
- Permissões do release corrigidas para o usuário/grupo `turborama-suite`.
- `turborama-suite-api.service` reiniciado com sucesso.

## Validação

- Serviço: `active (running)`
- Endpoint local `GET /health`: `200`, `status=ok`
- Endpoint local `GET /ready`: `200`, `status=ready`
- O endpoint correto de saúde é `/health`; `/v1/health` não existe neste serviço e retorna 404.

## Integridade preservada

- Nenhuma licença foi recriada, revogada ou alterada.
- Nenhum DeviceId ou fingerprint foi alterado.
- Nenhuma chave, autoridade, pin TLS ou contrato de API foi modificado.
- Nenhuma chave privada ou segredo foi copiado para Git.
- O release anterior permanece disponível para rollback.

## Pendência de aceite ponta a ponta

O teste final com proof real do cliente deve confirmar:

- `POST /v1/suite/challenges` -> HTTP 200;
- `POST /v1/suite/sessions` -> HTTP 200;
- sessão ACTIVE, presença ONLINE e outbox idempotente.


