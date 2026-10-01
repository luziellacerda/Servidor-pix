# Retorno da execução 5192

Preencha este arquivo no servidor e faça push nesta mesma branch. Sem pepper, sem DSN, sem chave privada, sem código de ativação e sem URL de jogo.

- parou porque:
- sha256 da DLL em 5190: `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`
- sha256 da DLL em 5192: `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`
- ledger tem `027_suite_download_notifications`: sim
- ledger tem `028_station_android`: sim
- `028` foi aplicada nesta rodada: sim
- `GET 127.0.0.1:5192/health`: HTTP 200 `{"status":"ok","service":"turborama-suite-api"}`
- `GET 127.0.0.1:5192/ready/station`: HTTP 200 `{"status":"ready"}`
- `POST 127.0.0.1:5190/v1/suite/challenges` com `{}`: HTTP 400 `JSON_INVALID` (PID 5190 permaneceu 2943)
- `POST https://app.lzgames.com.br/v1/station/challenges` com `{}`: HTTP 400 JSON `JSON_INVALID` (antes era 405 nginx)
- `GET https://app.lzgames.com.br/v1/station/me` status e se o corpo é JSON: HTTP 401 JSON `STATION_SESSION_INVALID` (não é HTML do portal)
- keyId: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`
- spkiBase64Url: `MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAo6sEDAwUywSCV0Yh9HY6PssYu8JH5ghGtIag-lIHqVVLNU7tZoRvc3_y0uPojGMpLAk3EtY8JmYLzC1klwQw2dujbTZop8k9nHnqVj6QwjHntUFIeNzDoDdF0XdxO2nBU8op0OqVp0xPg31Zcik69Lt-2YJcfkQrINjR3Ss6d4mRm3u9RcUfksv6A9fzt-KAin4dhreICH6qn892W_Xq0X5jFFzI9w135rY3ijgYB5cyhd19i0-Y1vXFNyNVpoyk01UCbWxrL11PYBHEjbqs78eS7hZmWjyqNQLcy9n7wlwlsesq7QsK3SchzaQSCAJpXDLHHh8xz1pP_EzjUscItwIDAQAB`

## Observado nesta rodada

- Unit nova `turborama-station-api.service` ativa em `127.0.0.1:5192`, DLL `/opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll`.
- Suite `turborama-suite-api` em `127.0.0.1:5190` não reiniciou (PID 2943). Gateway 5191 PID 2948 e PIX 5187 PID 2940 intactos.
- Nginx HTTP e HTTPS de `app.lzgames.com.br` passaram a incluir `/etc/nginx/snippets/turborama-station.locations.conf`. `location ^~ /v1/suite/` permanece apontando para 5190.
- `Station:Enabled=true` só no processo 5192. Conteúdo, EmulationStation, inventário e avisos ficaram false nesse processo.
- Sem evento de comércio e sem código `STA-…`, o app prova contrato/conexão. Compra completa continua no painel interno.
