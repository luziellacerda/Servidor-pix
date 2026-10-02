# Retorno — estado do servidor Station para o aplicativo

Data: 02/10/2026. Branch `docs/estado-servidor-app-station-20261002`. Este arquivo descreve o que a `5192` **já entrega** para o APK buscar lista, capa, jogo e conexão. Sem pepper, DSN, token, chave, senha, device id, ponto de montagem, nome de jogo ou URL de arquivo.

O APK TESTE empacota na máquina Windows. Este retorno não substitui o empacote; diz ao app o que chamar e o que o servidor responde hoje.

## 1. Conexão

| Item | Valor em produção |
|---|---|
| Único host | `https://app.lzgames.com.br` |
| Prefixo das rotas | `/v1/station/` |
| Processo | `127.0.0.1:5192`, unidade `turborama-station-api`, **active** |
| PID `5192` | 257112 |
| Hash da DLL | `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2` |
| Código | `feat/station-library-grant-20261002` `b1159c9` |
| `/ready/station` | 200 |
| `/health` | 200 |
| Borda | Cloudflare, `cf-cache-status: DYNAMIC`, `cache-control: no-store` |
| Túnel | `cloudflared` ativo |
| Pin TLS SPKI SHA-256 | `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` (já em `StationConfig.java`) |
| keyId Station | `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268` |
| Produto / aplicação | `TURBORAMA_STATION_ANDROID` |
| Domains | prefixo `TurboRamaStationAndroid/` |
| Assinatura | RSA-PSS SHA-256, MGF1-SHA-256, salt 32, chave 2048 |
| Envelope servidor | `{ keyId, payload, signature }` Base64URL sem padding |
| Envelope aparelho | `{ payload, signature }` |
| `deviceId` | Base64URL do SHA-256 da SPKI do Keystore |
| Corpo JSON máximo | 8 k (pedidos JSON; capa e jogo são bytes) |
| Cookie | não |
| `/v1/suite/` | não usar (`PRODUCT_DENIED`) |
| Sambox / IP / HTTP claro | não |

`5190` PID 2943, gateway `5191` PID 2948, PIX PID 2940: **não** foram reiniciados nesta carga. Suite Windows e PIX continuam isolados.

## 2. Funções que o app deve chamar (ordem)

### 2.1 Abrir a biblioteca (conexão + nome)

1. Se o token de 180 s expirou: `POST /v1/station/challenges` → `POST /v1/station/sessions` (envelope PSS do aparelho).
2. `GET /v1/station/me` com `Authorization: Bearer <accessToken>`.
3. 200 `profile/v1`: `displayName` (máx. 80), `profileVersion`. Título: `Bem-vindo, {displayName}`.
4. 503 `STATION_PROFILE_NOT_READY`: manter o último nome; mostrar `Bem-vindo`.
5. 401: nova sessão e tentar `/me` de novo.
6. Não pedir senha se `station-license-id.txt` existe e a sessão abrir.
7. Bearer **só na memória**. Sem heartbeat.

Desafio de ativação: 60 s. Sessão: 180 s. Um aparelho ativo por licença. Prefixo de senha: `STA-`.

### 2.2 Buscar a lista de jogos

`GET /v1/station/catalog` com o mesmo Bearer.

Resposta 200, envelope assinado, domínio `TurboRamaStationAndroid/catalog/v1`:

- `revision` — hoje `1`
- `items[]` — `itemId`, `name`, `platform`, `revision`, `coverId`

**Não há URL, host de capa nem caminho de disco.**

Índice ao vivo, revisão 1, **996** itens (capa ≤ 2 MiB, lado maior ≤ 720 px):

| `platform` | itens |
|---|---|
| megadrive | 693 |
| snes | 176 |
| snesbr | 28 |
| gamegear | 58 |
| gb | 22 |
| sega32x | 7 |
| gbc | 7 |
| gba | 5 |
| **total** | **996** |

`itemId` e `coverId`: 32 hex (`[a-f0-9]`, 8–64 permitidos no contrato). Sem `..`.

Comportamento no app:

- 200: gravar a lista no armazenamento privado (sem URL). Substituir se `revision` mudou.
- 503 `STATION_CATALOG_NOT_READY` ou rede: abrir a **última lista salva**.
- 401: nova sessão.
- Não baixar as 996 capas no login. Só as que faltam (`coverId` + revisão).

O leitor de 8192 bytes de `StationClient` **não** serve para o catálogo.

### 2.3 Buscar a capa

`GET /v1/station/covers/{coverId}` com Bearer.

- 200: bytes da imagem (`image/jpeg` ou `image/png` / webp / gif), `cache-control: no-store`.
- Teto no servidor: 5 MiB. Nesta carga todas as capas do índice estão **abaixo de 2 MiB** (mín. ~97 KiB, máx. ~786 KiB), lado maior **720 px**.
- 404 `STATION_COVER_NOT_FOUND`: placeholder. Capa **não** autoriza o jogo.
- Origem no host: arte da pasta **revista**, compactada. O telefone só vê `coverId`.

### 2.4 Baixar o jogo (quando a pessoa inicia e o arquivo local não existe)

Não chamar no login.

1. `POST /v1/station/downloads/authorize` com Bearer e JSON camelCase:

```json
{
  "schemaVersion": 1,
  "domain": "TurboRamaStationAndroid/request-download/v1",
  "productId": "TURBORAMA_STATION_ANDROID",
  "applicationId": "TURBORAMA_STATION_ANDROID",
  "deviceId": "<deviceId deste aparelho>",
  "clientVersion": "<versão do APK>",
  "deviceManufacturer": "<fabricante>",
  "deviceModel": "<modelo>",
  "androidSdk": 33,
  "itemId": "<itemId do catálogo>"
}
```

2. 200 `download-grant/v1`: `itemId`, `grantId`, `expiresInSeconds=60`. **Sem URL.** `grantId` só na memória. Começar em 60 s.
3. `GET /v1/station/artifacts/{grantId}` **uma vez**, mesmo Bearer. Bytes `application/octet-stream`, `Content-Length`. **Sem `Location`, sem 302, sem 307.** Timeout nginx 900 s. `setInstanceFollowRedirects(false)`.
4. Segundo GET, outro aparelho, prazo ou Bearer errado: 404 `STATION_GRANT_NOT_FOUND`.
5. 404 `STATION_ITEM_NOT_FOUND` se o `itemId` não está no índice.
6. 503 `STATION_DOWNLOAD_NOT_READY` se o índice ou a cifra não estiver carregada.

O servidor consome o uso no GET. Cloudflare não pode cachear artifacts.

## 3. Prova pública (sem sessão) — 02/10/2026

| Rota | HTTP | Location | URL no corpo |
|---|---|---|---|
| `GET /catalog` | 401 `STATION_SESSION_INVALID` | nao | nao |
| `GET /covers/{id}` | 401 `STATION_SESSION_INVALID` | nao | nao |
| `GET /me` | 401 `STATION_SESSION_INVALID` | nao | nao |
| `POST /downloads/authorize` JSON sem bearer | 401 `STATION_SESSION_INVALID` | nao | nao |
| `GET /artifacts/{id}` inexistente | 404 `STATION_GRANT_NOT_FOUND` | nao | nao |

`cf-cache-status: DYNAMIC`, `cache-control: no-store` em todas.

## 4. Limites e erros

- 30 pedidos por IP, por rota, por minuto → `STATION_RATE_LIMITED`.
- JSON inválido → 400 `JSON_INVALID`.
- Identidade / domain errado → 400 `STATION_IDENTITY_INVALID`.
- Aparelho diferente da sessão → 403 `STATION_DEVICE_DENIED`.
- Ativação: `STATION_ACTIVATION_INVALID`, `STATION_CHALLENGE_INVALID`, `STATION_CHALLENGE_MISMATCH`.
- HEAD em artifacts → 405.

Subida medida da origem ~30 Mbps. ROM de Super Nintendo / Mega Drive cabe em cerca de 1 s por aparelho nessa amostra. Mostrar progresso. Não abrir vários artifacts em paralelo no mesmo aparelho.

## 5. O que o app não deve fazer

- Falar com outro host, IP, `/v1/suite/`, Sambox, `miami`, `?e=`, `?s=`.
- Guardar URL, `grantId`, bearer ou caminho de disco.
- Seguir redirect no download.
- Baixar o jogo no `open` / `pullName` / login.
- WebView / JS Challenge nas rotas Station.
- Digitar de novo a senha `STA-D7AE45616B415B2C7550315C0392C5D8` já BOUND. Telefone de teste: Samsung SM-A566E `RQCY30751WY`.

## 6. Estado desta máquina (confirmação)

- parou porque: estado-pronto-para-o-app
- índice carregado: sim, revisão 1, 996 itens
- capas 720 px e ≤ 2 MiB: sim
- catálogo com sessão assina `catalog/v1`: sim (código no ar)
- download de um uso sem URL: sim
- `5192` saudável: sim
- `5192` reiniciada nesta carga do índice: sim (PID 257112)
- `5190` / `5191` / PIX reiniciados: nao
- senha emitida: nao
- ativação desta máquina: nao
- WhatsApp: nao
- Cloudflare editado: nao
- índice no Git: nao
- APK empacotado neste servidor: nao (Windows)

O handoff do APK permanece `HANDOFF-APP-PRODUCAO-STATION-20261002.md`. Depois do `adb install -r --no-incremental` no Samsung, preencher as linhas do aparelho em `RETORNO-APP-PRODUCAO-STATION-20261002.md`.
