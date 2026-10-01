# Retorno — Station no servidor de serviços (produção)

Data: **01/10/2026**. Servidor: `lz-servidor` (API Suite `127.0.0.1:5190`, Nginx `app.lzgames.com.br`).
Pedido: analisar o que falta, fazer o que o handoff 20261001 pediu, e devolver situação para o app testar conexões.

Este arquivo **não contém** pepper, chave privada, DSN, token nem código de ativação.

## 1. O que foi lido

- `HANDOFF-APP-PARA-SERVIDOR-STATION-20261001.md` (fio vigente = seção 0, commit app `93efbba`)
- `HANDOFF-PROGRAMA-PRODUCAO-LICENSE-NOT-FOUND-20260914.md`
- `RETORNO-IMPLEMENTACAO-CANDIDATA-20260930.md`
- `AGENTS.md` desta pasta (não destruir PIX/Suite/ES/gateway)

## 2. Situação de produção **agora** (medida)

| Item | Valor observado |
|---|---|
| Processo Suite | `turborama-suite-api.service` ativo, PID 2943 |
| DLL efetiva | `/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908/server/TurboRamaSuiteOnlineServer.dll` |
| SHA-256 | `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1` (bate com o handoff de 14/09) |
| `/health` local | HTTP 200 `turborama-suite-api` |
| `POST /v1/suite/challenges` `{}` | HTTP 400 `JSON_INVALID` (rota Suite viva) |
| `POST /v1/station/*` local `:5190` | HTTP 404 vazio (rota **não existe** neste binário) |
| `GET https://app.lzgames.com.br/v1/station/me` | HTTP **200 HTML** do portal LZ Games (armadilha: não é perfil Station) |
| `POST https://app.lzgames.com.br/v1/station/challenges` | HTTP **405 nginx** (não há `location /v1/station/`) |
| Pin TLS `app.lzgames.com.br` SPKI SHA-256 | `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` (bate com o handoff) |
| Nginx | só `^~ /v1/suite/` e `suite-content`; **não** há Station |
| PIX / `lzgames-api` 8083 | não alterados nesta rodada |

Conclusão: o servidor de serviços **libera Suite**, não Station. O app `TURBORAMA_STATION_ANDROID` **ainda não consegue** falar o contrato da seção 0 contra produção.

## 3. O que esta rodada fez (sem substituir a Suite no ar)

Não houve `sudo`. Por isso **não** foi possível: gravar Nginx, systemd, `/opt` root, PostgreSQL `runuser`, nem ligar a flag no processo 5190. PIX, Suite 5190, gateway 5191 e `lzgames-api` permaneceram.

Feito neste usuário:

1. Clone `feat/station-android-contract-20260930` @ `93efbba`.
2. Compilação Release **êxito, 0 erro**: `TurboRamaSuiteOnlineServer.dll`
   SHA-256 `862323d20072d5c461c2228f0af8ef04af1a276f0e743649520a312e67cd2226`
   pasta ` /home/lz-servidor/turborama-station-20261001/server/`
3. Cópia da migration candidata `028_station_android.up.sql` (não aplicada).
4. Chave RSA Station **independente** da Suite gerada em `.../secrets/` (não vai para o Git).
   keyId público: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`
5. Snippet Nginx e unit `5192` em `.../ops/` (não instalados).

O processo Station **separado** em `127.0.0.1:5192` existe para não substituir o binário `bd82bc3` da Suite em `5190`.

## 4. O que ainda falta (operador com sudo) para o app testar de verdade

Ordem segura, uma vez só, com backup:

1. Conferir ledger: `SELECT version FROM suite.schema_migrations ORDER BY 1;`
   - Se `027_suite_download_notifications` já está, aplicar **`028_station_android`**.
   - Se `028_station_android` já está, não reaplicar.
   - Não usar um arquivo local chamado `027_suite_station_android`.
2. Copiar o release compilado para `/opt/turborama-station-20261001/` (owner `turborama-suite`).
3. Instalar pepper e PEM Station em `/etc/turborama-suite/` (mode 600, **não** reutilizar pepper/chave da Suite).
4. Instalar `ops/turborama-station-api.service` e subir **só** esse unit. Conferir `GET :5192/health` 200 e `GET :5192/ready/station` 200 **depois** da 028.
5. Incluir `ops/nginx-v1-station.conf` nos server blocks de `app.lzgames.com.br` (HTTP e HTTPS). **Não** editar `location ^~ /v1/suite/`.
6. `nginx -t` e reload. Suite `POST /v1/suite/challenges` deve continuar 400 `JSON_INVALID` com `{}`.
7. Só então o app testa contra `https://app.lzgames.com.br` (sem barra no fim), produto `TURBORAMA_STATION_ANDROID`.

Com a flag `Station:Enabled=true` no processo **5192**:

| Rota | Esperado para teste |
|---|---|
| `POST /v1/station/activations/challenge` | JSON assinado ou 403 `STATION_ACTIVATION_INVALID` (código/licença) |
| `POST /v1/station/activations/complete` | envelope `activated/v1` se o código for válido |
| `POST /v1/station/challenges` | `session-challenge/v1`, 60 s |
| `POST /v1/station/sessions` | `session/v1`, `expiresInSeconds` 180, token 32 bytes |
| `GET /v1/station/me` | `profile/v1` **ou** 503 `STATION_PROFILE_NOT_READY` se o nome do comprador ainda não foi projetado |
| `GET /v1/station/catalog` | 503 `STATION_CATALOG_NOT_READY` (proposital) |
| `POST /v1/suite/challenges` com produto Station | continua **403 PRODUCT_DENIED** na Suite 5190 |

Sem evento de comércio `/commerce/station/events` e sem código de ativação emitido, o app **não completa** a compra; só prova contrato/conexão. Emissão de código é painel interno, não este retorno.

## 5. Devolver ao app (pode testar depois do sudo)

Enquanto os passos da seção 4 não rodarem:

- Manter `StationConfig.ENABLED=false`.
- Não tratar HTML 200 em `/v1/station/me` como perfil.
- Pin TLS já conferido: `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`.
- URL: `https://app.lzgames.com.br`
- keyId Station **deste pacote** (quando o 5192 subir): `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`
- Catálogo/download ainda 503 por contrato.

Quando o Nginx 5192 estiver no ar, o app pode ligar a flag **só em APK de teste** e bater nas rotas da tabela. Licença `STA-…`; id `TS-` na rota Station é inválido.

## 6. O que não foi feito de propósito

- Não se substituiu a DLL `93939be3…` da Suite.
- Não se aplicou SQL.
- Não se ligou Station no processo 5190.
- Não se girou chave online/conteúdo da Suite.
- Não se mexeu em PIX, Cloudflare, túnel, `lzgames-api`.
