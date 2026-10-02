# Retorno — continuar a Station no aparelho

Preencha e faça push nesta branch logo depois de emitir o código. O código vence em 15 minutos. Não ative a licença daí. Não cole pepper, DSN, token admin, chave privada nem URL de jogo. Não crie segunda licença.

## Licença

- parou porque:
- conferiu a mesma licença `STA-D7AE45616B415B2C7550315C0392C5D8`: sim
- enrollment_state antes de emitir: PENDING_ENROLLMENT
- activation_consumed antes de emitir: false
- licenseId impresso pelo script: `STA-D7AE45616B415B2C7550315C0392C5D8`
- activationCode impresso agora pelo script: `h8Lvbet6HNehHU29Z_100fMemN24x32J8lVGsoqLoP0`
- expiresAt UTC: `2026-10-02T00:34:52Z`
- o script disse `JA_ATIVO_NAO_REIMPRIMIVEL`: nao
- segunda licença criada: nao (continua 1 linha `TURBORAMA_STATION_ANDROID`)
- licença ativada desta máquina: nao
- displayName: Teste Station

## Processo que ficou no ar

- PID e hash da DLL 5190: PID 2943, `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`
- PID e hash da DLL 5192: PID 86341, `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`
- admin 5191 foi substituído: nao (PID 2948)
- PIX 5187 foi alterado: nao (PID 2940)
- `GET 127.0.0.1:5192/health`: HTTP 200 `{"status":"ok","service":"turborama-suite-api"}`
- `GET 127.0.0.1:5192/ready/station`: HTTP 200 `{"status":"ready"}`
- `POST 127.0.0.1:5190/v1/suite/challenges` com `{}`: HTTP 400 `JSON_INVALID`
- migration nova aplicada nesta rodada: nao

## Rotas públicas sem sessão

- `POST /v1/station/activations/challenge`: HTTP 400 `JSON_INVALID`
- `POST /v1/station/activations/complete`: HTTP 400 `JSON_INVALID`
- `POST /v1/station/challenges`: HTTP 400 `JSON_INVALID`
- `POST /v1/station/sessions`: HTTP 400 `JSON_INVALID`
- `GET /v1/station/me`: HTTP 401 `STATION_SESSION_INVALID`
- `GET /v1/station/catalog`: HTTP 401 `STATION_SESSION_INVALID`
- `POST /v1/station/downloads/authorize`: HTTP 401 `STATION_SESSION_INVALID`

## O que ainda falta, medido agora

- `/admin` mostra esta licença Android: nao
- TurboBox ou o site vende `STATION_ANDROID_LIFETIME_1_DEVICE`: nao
- catálogo privado Station aberto: nao
- `GET /v1/station/artifacts/{grantId}` existe: nao
- backup restaurável desta licença existe: nao
- prova dos papéis `turborama-suite` e `turborama-suite-admin` existe: nao
- compra sintética pelo site existe: nao
- plano de rollback da `028` existe: nao
- vetor de assinatura do Android Keystore foi feito desta máquina: nao

## Nota

- 5190, admin e PIX foram reiniciados: nao
- o que o script imprimiu além do código, sem segredo: `licenseId=STA-D7AE45616B415B2C7550315C0392C5D8` `expiresAt=2026-10-02T00:34:52Z` `displayName=Teste Station` `sourcePurchaseId=station-teste-20261001` `sourceItemKey=android-1` `customerRef=teste-station`
