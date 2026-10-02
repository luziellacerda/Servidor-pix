# Handoff — aplicativo Turborama Station em produção

Data: 02/10/2026. Destino: **APK / repositório do aplicativo Android**. Esta rodada **não mexe no servidor**. Catálogo, capas e download já estão abertos na `5192`.

Base do servidor: `feat/station-library-grant-20261002` commit `b1159c9`. Binário da `5192`: `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`. `/ready/station` 200.

Telefone de referência: Samsung `RQCY30751WY`. Licença de teste `STA-D7AE45616B415B2C7550315C0392C5D8` (BOUND, um aparelho, nome `Teste Station`). Não emita senha. Não ative desta máquina. Não cole pepper, DSN, token, chave privada, device id, ponto de montagem nem URL de jogo.

## 1. O que o servidor já faz (não refaça lá)

- Base pública: `https://app.lzgames.com.br/v1/station/`. Processo em `127.0.0.1:5192`. Borda Cloudflare, `cf-cache-status: DYNAMIC`, `cache-control: no-store`. Túnel `cloudflared` ativo.
- Produto e aplicação: `TURBORAMA_STATION_ANDROID`. Prefixo dos domains: `TurboRamaStationAndroid/`.
- keyId Station: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`. Pin TLS SPKI SHA-256 de `app.lzgames.com.br`: `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`.
- Assinatura: RSA-PSS SHA-256, MGF1-SHA-256, salt 32, chave 2048. Envelope do servidor `{ keyId, payload, signature }` em Base64URL sem padding. Envelope do aparelho `{ payload, signature }`.
- `deviceId` = Base64URL do SHA-256 da SPKI pública do Keystore. Copiar o APK não copia essa chave.
- Desafio 60 s. Sessão 180 s. Sem heartbeat. Um aparelho ativo por licença.
- Primeira senha da compra: 48 horas. Reemissão humana em TurboBox `/admin/station`: 30 minutos e mata a anterior. WhatsApp é do servidor. O app não envia WhatsApp.
- `GET /me` com bearer devolve `profile/v1` com `displayName` (máx. 80) e `profileVersion`.
- Índice de produção carregado: revisão 1, **1816** itens. Capas vêm da arte **revista** no host (o telefone só vê `coverId`). Teto de capa: 5 MiB.
- Contagem por `platform`, sem nomes: `snes` 644, `snesbr` 191, `megadrive` 887, `megadrivebr` 94.
- Sem sessão: catálogo, capas e `/me` → 401 `STATION_SESSION_INVALID`. Authorize com JSON válido sem bearer → 401. Artifacts sem concessão → 404 `STATION_GRANT_NOT_FOUND`, sem `Location`. `HEAD` artifacts → 405.

O telefone **nunca** vê caminho de disco, pasta `revista`, pasta `images` nem URL de arquivo.

## 2. Contrato das rotas (produção)

Host único: `https://app.lzgames.com.br`. Só `/v1/station/`. Sem `/v1/suite/`. Sem Sambox. Sem IP. Sem HTTP claro. Corpo JSON máximo 8 k. Sem cookie. Sem query de caminho.

Toda resposta 200 de negócio JSON vem no envelope assinado. Verifique `keyId`, depois o `payload` UTF-8 exato, depois RSA-PSS. `schemaVersion` = 1. `productId` e `applicationId` = `TURBORAMA_STATION_ANDROID`. Campos JSON em camelCase.

| Rota | Auth | Pedido | Resposta | Status em produção |
|---|---|---|---|---|
| `POST /activations/challenge` | não | `…/request-activation-challenge/v1` | `…/activation-challenge/v1` | aberto |
| `POST /activations/complete` | envelope PSS | `…/activate/v1` | `…/activated/v1` | aberto, uma vez por aparelho |
| `POST /challenges` | não | `…/request-session-challenge/v1` | `…/session-challenge/v1` | aberto |
| `POST /sessions` | envelope PSS | `…/open-session/v1` | `…/session/v1` (`accessToken`, 180 s) | aberto; bearer só na memória |
| `GET /me` | Bearer | — | `…/profile/v1` | aberto |
| `GET /catalog` | Bearer | — | `…/catalog/v1` | **aberto** |
| `GET /covers/{coverId}` | Bearer | — | bytes (`image/png`, `image/jpeg`, `image/webp` ou `image/gif`), `cache-control: no-store` | **aberto**, até 5 MiB |
| `POST /downloads/authorize` | Bearer + JSON | `…/request-download/v1` | `…/download-grant/v1` | **aberto** |
| `GET /artifacts/{grantId}` | Bearer | — | bytes `application/octet-stream`, um uso, sem `Location` | **aberto**, timeout 900 s |

`itemId` e `coverId`: 8–64 caracteres `[A-Za-z0-9_-]`. Hoje são 64 hex. Não coloque `..` no identificador.

Códigos de UI: `STATION_ACTIVATION_INVALID`, `STATION_CHALLENGE_INVALID`, `STATION_CHALLENGE_MISMATCH`, `STATION_DEVICE_DENIED`, `STATION_SESSION_INVALID`, `STATION_PROFILE_NOT_READY`, `STATION_CATALOG_NOT_READY`, `STATION_DOWNLOAD_NOT_READY`, `STATION_ITEM_NOT_FOUND`, `STATION_COVER_NOT_FOUND`, `STATION_GRANT_NOT_FOUND`, `STATION_RATE_LIMITED` (30 pedidos por IP por rota por minuto).

## 3. Nome do consumidor

1. Em toda abertura da biblioteca (ícone, volta ao app, depois do login): se o token de 180 s expirou, abra sessão; chame `GET /v1/station/me`.
2. Título: `Bem-vindo, {displayName}` quando o nome vier. Sem nome: `Bem-vindo`. Aposente o texto fixo `BEM-VINDO DE VOLTA`.
3. 200 `profile/v1`: grave `displayName` e `profileVersion` no armazenamento privado. Não grave o bearer.
4. 503 `STATION_PROFILE_NOT_READY`: mantenha o último nome. Não zere.
5. 401: recrie sessão e tente `/me` de novo. Sem licença local, volte à senha.
6. Não peça senha se `station-license-id.txt` existe e a sessão abrir.

## 4. Catálogo — agora é 200 `catalog/v1`

Depois de `/me`, `GET /v1/station/catalog` com o mesmo bearer.

Payload assinado (além da identidade):

- `revision` — revisão do índice (hoje `1`)
- `items[]` — `itemId`, `name`, `platform`, `revision`, `coverId`

Não há URL, host de capa nem caminho.

`platform` nesta carga: `snes`, `snesbr`, `megadrive`, `megadrivebr`. Agrupe a UI por esses tokens. Jogo novo no servidor aparece no próximo login, sem atualizar o APK.

Comportamento:

1. 200: substitua a lista local se `revision` mudou. Persista a lista no armazenamento privado (sem URL).
2. 503 `STATION_CATALOG_NOT_READY` ou rede: abra com a **última lista salva**. Não bloqueie a biblioteca.
3. 401: nova sessão e tente de novo.

Não baixe as 1816 capas de uma vez. Capas só das que faltam, chave `itemId` + `revision` do item (ou `coverId` + revisão).

## 5. Capas — `GET /covers/{coverId}`

A arte que o XML chamava de imagens, no TurboStation, é a **revista**. O app pede só o `coverId` do catálogo.

1. `GET /v1/station/covers/{coverId}` com Bearer.
2. 200: bytes da imagem, `Content-Type` de imagem, `cache-control: no-store`. Grave no cache privado do app. Não publique em galeria.
3. 404 `STATION_COVER_NOT_FOUND`: placeholder. Não autorize download por causa da capa.
4. Capa não autoriza o arquivo do jogo.
5. Tamanho até 5 MiB. Não use WebView. Não siga redirect.

## 6. Download — concessão de um uso

1. `POST /v1/station/downloads/authorize` com Bearer e JSON:

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

2. 200 `download-grant/v1`: `itemId`, `grantId`, `expiresInSeconds` (60). Sem URL. O `grantId` fica **só na memória**. Comece o download dentro desses 60 s.
3. `GET /v1/station/artifacts/{grantId}` **uma vez**, com o mesmo Bearer, para arquivo temporário privado. `Content-Type: application/octet-stream`. Sem `Location`, sem 302, sem 307. Não siga redirect.
4. Segundo GET, outro aparelho, grant vencido ou Bearer errado: 404 `STATION_GRANT_NOT_FOUND`. Isso invalida link clonado. Cloudflare não consome o uso; a `5192` consome.
5. Proibido: guardar URL, gravar `grantId` em disco/log/crash, mandar grant no WhatsApp, copiar Location para a galeria, R2 público.
6. APK copiado em outro telefone: Keystore diferente → sem sessão → sem grant → sem arquivo.
7. Super Nintendo e Mega Drive são arquivos pequenos. A origem mede cerca de 30 Mbps de subida; um ROM de 4 MB leva cerca de 1 s por aparelho. Mostre progresso. Não abra vários artifacts em paralelo no mesmo aparelho.

## 7. Cloudflare e ataque

- Fale só com `https://app.lzgames.com.br`. Pin SPKI. Recuse certificado que não bata.
- Não use WebView para a API. A rota Station não pode ganhar JS Challenge / CAPTCHA / Access (quebra o APK nativo).
- 429: espere; não martelar `activations/*`.
- Sem cookie. Sem token Suite. Sem host de conteúdo Windows.
- O app nunca mostra `artifacts/{grantId}` na UI.

## 8. O que não fazer

- Não altere 5190, 5191, 5192, PIX, Nginx, Cloudflare, WhatsApp, site, SPA `/admin`.
- Não peça para o servidor “abrir” catálogo: já está aberto.
- Não descubra disco por tamanho. Não peça ponto de montagem. Não misture Sambox.
- Não desinstale o APK de teste só para limpar. Não troque o certificado de assinatura.
- Não misture `TurboRamaSuite/` com Station.
- Não imprima senha, bearer, nonce, grant, device id ou SPKI privada no retorno.

## 9. Ordem no APK

1. Sessão em toda abertura + `GET /me` + título `Bem-vindo, {nome}`.
2. Cache do perfil; 503 não zera o nome.
3. `GET /catalog` → lista `catalog/v1`; 503 usa lista local.
4. Capas por `coverId`, cache por revisão, sem baixar tudo no login.
5. Authorize + artifacts de um uso, grant só na memória, arquivo só no armazenamento privado.

## 10. Retorno

Preencha `RETORNO-APP-PRODUCAO-STATION-20261002.md` no repositório do app (ou nesta branch se o trabalho do APK for registrado aqui) e faça push. Sem segredo, sem senha, sem device id, sem nome de jogo, sem URL.
