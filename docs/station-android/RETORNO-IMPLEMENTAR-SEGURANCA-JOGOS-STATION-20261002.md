# Retorno — implementar a segurança dos jogos Station

Preencha e faça push. Sem pepper, DSN, token, chave, senha, device id, ponto de montagem, lista de jogos ou URL.

## Resultado

- parou porque: indice-ausente
- catálogo assinado `catalog/v1` sem URL: nao
- se nao, motivo: indice-ausente
- quantidade de itens no índice, sem nomes: 0
- capas por `coverId` com bearer e sem URL: nao
- authorize devolve `grantId` sem URL: nao
- artifacts entrega bytes e não envia `Location`: nao
- segundo uso responde 404: nao-aplica
- nome do arquivo da chave AES Station: station-download.key
- chave diferente dos segredos Suite e do pepper de ativação: sim
- hash do binário da `5192`: `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`
- `5192` reiniciada: sim (PID 223970, 02/10/2026 10:15:59)
- `5190` reiniciada: nao
- `5191` reiniciada: nao
- PIX reiniciado: nao
- painel Cloudflare editado: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao

## O que subiu

Fonte `93efbba`, branch `feat/station-library-grant-20261002` commit `bbd07fd`. Testes `TurboRamaSuiteOnlineServer.Tests` OK, incluindo `StationLibraryChecks`. Só a unidade `turborama-station-api` (5192) foi reiniciada. APK não mudou.

`GET /v1/station/catalog` com sessão assina `TurboRamaStationAndroid/catalog/v1` (`itemId`, `name`, `platform`, `revision`, `coverId`) quando o índice existe. Sem caminho e sem URL. Índice ausente ou ilegível: 503 `STATION_CATALOG_NOT_READY`. Arquivo presente com `items: []`: 200 e zero itens. Nesta máquina o arquivo do índice ainda não existe; não foi inventado item.

`GET /v1/station/covers/{coverId}` com o mesmo bearer devolve bytes (`cache-control: no-store`). `coverId` não é caminho. Capa não autoriza o arquivo do jogo.

`POST /v1/station/downloads/authorize` recebe `StationDownloadRequest` (`ItemId`). A 5192 resolve no índice. Resposta assinada `TurboRamaStationAndroid/download-grant/v1` com `grantId` e `expiresInSeconds=60`. Sem URL. Migration aditiva `029_station_download_grants` aplicada; `028_station_android` intacta. Tabela de concessões com 0 linhas. Sem índice ou sem cifra: 503 `STATION_DOWNLOAD_NOT_READY`.

`GET /v1/station/artifacts/{grantId}` consome um uso e entrega bytes no processo. Sem `Location`, sem 302, sem 307. Segundo uso, outro aparelho ou prazo vencido: 404 `STATION_GRANT_NOT_FOUND`. Concessão inexistente sem sessão: 404, não 401. Nginx tem `location ^~ /v1/station/artifacts/` com `proxy_read_timeout 900s` e `proxy_buffering off`.

Chave AES-GCM da Station: arquivo `station-download.key`, 32 bytes, modo 600. SHA-256 diferente do pepper de ativação Station e do pepper Suite. Processo recusa subir se o arquivo for o mesmo path ou o mesmo segredo.

## Prova pública (sem sessão)

- `GET /v1/station/catalog` → 401 `STATION_SESSION_INVALID`
- `POST /v1/station/downloads/authorize` `{}` → 400 `JSON_INVALID`
- `POST /v1/station/downloads/authorize` JSON de download sem bearer → 401 `STATION_SESSION_INVALID`
- `GET /v1/station/covers/{coverId}` → 401 `STATION_SESSION_INVALID`
- `GET /v1/station/artifacts/{grantId}` inexistente → 404 `STATION_GRANT_NOT_FOUND`, sem `Location`
- `HEAD /v1/station/artifacts/{grantId}` → 405
- `GET /v1/station/me` → 401 `STATION_SESSION_INVALID`
- borda: `server: cloudflare`, `cf-cache-status: DYNAMIC`, `cache-control: no-store`
- loopback `127.0.0.1:5192/ready/station` 200, `/health` 200

## Processos que não foram tocados

- 5190 PID 2943 desde 01/10/2026 12:07:12, hash `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`
- gateway 5191 PID 2948 desde 01/10/2026 12:07:12
- PIX PID 2940 desde 01/10/2026 12:07:12
- site, SPA `/admin`, WhatsApp, helper 5194, licença de teste, senha

## Índice do operador (próximo passo humano)

O operador cria o JSON do índice no arquivo configurado (`Station__LibraryIndexFile`). Cada item: `itemId`, `name`, `platform`, `revision`, `coverId`, `filePath`, `coverPath`. Caminhos absolutos. Sem `..`. Até 4096 itens. Capa até 2 MiB. Revisão sobe quando a lista ou uma capa muda. Volumes novos entram como raízes nesse JSON; tamanho de disco não descobre jogo. O JSON do índice não vai para o Git. Exemplo vazio em `docs/station-android/ops/station-library.example.json`.

## Painel Cloudflare (segue com o operador)

Não foi aberto nem editado nesta rodada. A origem já manda `no-store` e a borda já está no caminho. Falta o operador confirmar no painel:

1. Proxy laranja em `app.lzgames.com.br`.
2. Bypass de cache em `/v1/station/*`. Sem Cache Everything. Sem R2 público com URL permanente de jogo ou capa.
3. Rate limit por IP em `/v1/station/*` (teto baixo em activations/challenges, um pouco maior em catalog/me, teto curto em authorize/artifacts).
4. WAF/managed rules **sem** JS Challenge, CAPTCHA, Turnstile ou Cloudflare Access em `/v1/station/*`.
5. Authenticated Origin Pulls.
6. TLS full (strict) no túnel.

Cloudflare esconde a origem e corta volume. Quem invalida link copiado é a concessão de um uso na 5192.
