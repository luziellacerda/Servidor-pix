# Retorno da licença de teste e do que falta

Preencha tudo. Faça push nesta branch logo depois de emitir o código. O código vence em 15 minutos. Não ative a licença daí. Não cole pepper, DSN, token admin, chave privada nem URL de jogo.

## Licença

- parou porque: (vazio — concluiu)
- banco usado, só o nome: postgres
- licenseId: `STA-D7AE45616B415B2C7550315C0392C5D8`
- activationCode: `7AsiOBuA7Ly2hi2LzAyui_vfwvK35N41kG-696HcMxE`
- expiresAt UTC: `2026-10-02T00:02:14Z`
- displayName: Teste Station
- sourceSystem: TURBOBOX_V1
- sourcePurchaseId: station-teste-20261001
- sourceItemKey: android-1
- customerRef: teste-station
- productId: TURBORAMA_STATION_ANDROID
- sku: STATION_ANDROID_LIFETIME_1_DEVICE
- amountCents: 9990
- currency: BRL
- licenseTerm: LIFETIME
- expires_at da licença comercial: NULL
- aparelhos ativos permitidos: 1
- enrollment_state depois da emissão: PENDING_ENROLLMENT
- código já estava ativo e não foi reimpresso: nao

## Processo que ficou no ar

- PID e hash da DLL 5190: PID 2943, `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`
- PID e hash da DLL 5192: PID 86341, `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`
- admin 5191 foi substituído: nao (PID 2948 permanece o gateway de conteúdo `turborama-suite-content-gateway`; unit `turborama-suite-admin.service` active, sem troca nesta rodada)
- PIX 5187 foi alterado: nao (PID 2940; `GET /v1/health` HTTP 200)
- `GET 127.0.0.1:5192/health`: HTTP 200 `{"status":"ok","service":"turborama-suite-api"}`
- `GET 127.0.0.1:5192/ready/station`: HTTP 200 `{"status":"ready"}`
- `POST 127.0.0.1:5190/v1/suite/challenges` com `{}`: HTTP 400 `JSON_INVALID`
- keyId Station: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`
- spkiBase64Url: `MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAo6sEDAwUywSCV0Yh9HY6PssYu8JH5ghGtIag-lIHqVVLNU7tZoRvc3_y0uPojGMpLAk3EtY8JmYLzC1klwQw2dujbTZop8k9nHnqVj6QwjHntUFIeNzDoDdF0XdxO2nBU8op0OqVp0xPg31Zcik69Lt-2YJcfkQrINjR3Ss6d4mRm3u9RcUfksv6A9fzt-KAin4dhreICH6qn892W_Xq0X5jFFzI9w135rY3ijgYB5cyhd19i0-Y1vXFNyNVpoyk01UCbWxrL11PYBHEjbqs78eS7hZmWjyqNQLcy9n7wlwlsesq7QsK3SchzaQSCAJpXDLHHh8xz1pP_EzjUscItwIDAQAB`

## O que o app precisa para implementar

- URL pública: `https://app.lzgames.com.br`
- rotas que existem e o status sem sessão, uma por linha:
  - `POST /v1/station/activations/challenge` HTTP 400 `JSON_INVALID`
  - `POST /v1/station/activations/complete` HTTP 400 `JSON_INVALID`
  - `POST /v1/station/challenges` HTTP 400 `JSON_INVALID`
  - `POST /v1/station/sessions` HTTP 400 `JSON_INVALID`
  - `GET /v1/station/me` HTTP 401 `STATION_SESSION_INVALID`
  - `GET /v1/station/catalog` HTTP 401 `STATION_SESSION_INVALID`
  - `POST /v1/station/downloads/authorize` HTTP 401 `STATION_SESSION_INVALID`
- catálogo continua 503: sim por contrato depois de sessão válida (`STATION_CATALOG_NOT_READY`); sem sessão o GET público responde 401 `STATION_SESSION_INVALID`. Esta rodada não abriu sessão.
- download continua 503: sim por contrato depois de sessão válida (`STATION_DOWNLOAD_NOT_READY` / authorize fechado); sem sessão o POST público responde 401 `STATION_SESSION_INVALID`. Esta rodada não abriu sessão.
- TTL do código em minutos: 15
- TTL do desafio em segundos: 60
- TTL da sessão em segundos: 180
- existe heartbeat: nao
- prefixo do id da licença Station: `STA-`
- um id `TS-` da Suite entra na Station: nao (contrato inválido na rota Station)
- nome mostrado quando o perfil existe: `Teste Station`
- nome quando o perfil responde 503: `Bem-vindo`
- o painel visual em `/admin` mostra esta licença: nao
- o site ou TurboBox já vende `STATION_ANDROID_LIFETIME_1_DEVICE`: nao
- onde o código seria entregue ao comprador real: painel interno / área do cliente ainda sem cartão Android; nesta rodada o código foi só neste retorno
- o que ainda não existe no servidor, em lista:
  - cartão/páginas `/admin` e `/admin/clientes/{licenseId}` falando rota Station
  - checkout TurboBox do SKU Android a R$ 99,90 e entrega do código na área do cliente
  - catálogo privado (~18 mil itens) e adapter de gateway Station
  - `GET /v1/station/artifacts/{grantId}`
  - backup restaurável desta licença de teste, prova de papéis `turborama-suite` / `turborama-suite-admin`, compra sintética pelo site e plano de rollback da `028`
  - vetor de assinatura com chave do Android Keystore em aparelho real
- o que quebra se o app ligar a flag hoje sem código: `ready()` pode passar a falar Station (SPKI/keyId já estão no app preparado), o campo deixa a senha local, `POST /v1/station/activations/challenge` com código vazio/errado responde 400 `JSON_INVALID` ou 403 `STATION_ACTIVATION_INVALID`, sessão sem aparelho vinculado responde 403 `STATION_DEVICE_DENIED`, perfil sem bearer 401 `STATION_SESSION_INVALID`. O APK instalado de 1,58 GB permanece sem essas classes até reempacotar.

## Nota de execução

O script `emitir-licenca-teste.py` precisou de dois ajustes para gravar: `l.license_id` / `l.activation_expires_at` no SELECT inicial (coluna ambígua) e SQL via stdin (arquivo 600 em `/tmp` o papel `postgres` não lê). A licença não foi ativada. Não foi criada segunda licença. Suite 5190, PIX 5187 e admin/gateway 5191 não foram reiniciados.
