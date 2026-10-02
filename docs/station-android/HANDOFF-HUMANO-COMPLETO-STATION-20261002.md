# Handoff humano — Turborama Station (servidor + app)

Data: 02/10/2026. Leia este arquivo do começo ao fim. Não precisa caçar outro documento para operar ou para o aplicativo falar com o servidor.

**O que isto é.** Turborama Station é o aplicativo Android de jogos. O telefone **não** abre pasta de disco e **não** recebe link de arquivo. Ele fala só com uma API HTTPS. O servidor libera lista, capa e jogo **depois** de senha + aparelho + sessão.

**O que está pronto hoje.** O servidor já consome. Catálogo, capas e download de um uso estão no ar. Falta o APK TESTE (máquina Windows) chamar as rotas descritas aqui.

Não cole neste texto (nem no chat, nem no Git): pepper, DSN, token, chave privada, senha nova, device id de cliente, ponto de montagem, lista de nomes de jogos, URL de arquivo.

---

## 1. Mapa em uma página

Pessoa compra → TurboBox gera senha `STA-…` (WhatsApp) → no telefone a pessoa cola a senha **uma vez** → o aparelho fica BOUND (preso a esse telefone) → toda vez que abre o app, o telefone pede um desafio, assina, ganha um token de 180 segundos → com esse token pede:

1. o **nome** (`GET /me`)
2. a **lista** (`GET /catalog`)
3. as **capas** que faltam (`GET /covers/{coverId}`)
4. o **jogo**, só quando a pessoa toca para jogar e o arquivo ainda não está no celular (`POST /downloads/authorize` e em seguida `GET /artifacts/{grantId}`)

Nada disso devolve `http://…` de jogo ou capa. A resposta da lista é JSON assinado. Capa e jogo são **bytes**.

Caminho de rede:

```
Samsung
  → HTTPS 443  https://app.lzgames.com.br
  → Cloudflare (laranja, sem cache da API)
  → túnel cloudflared
  → nginx nesta máquina (80/443 só na origem; o mundo não fala com a porta 5192)
  → 127.0.0.1:5192   processo turborama-station-api
  → índice + arquivos no disco de jogos + Postgres suite
```

O telefone **nunca** conecta em `5190`, `5191`, `5192`, `5194`, IP da casa, Sambox, nem `/v1/suite/`.

---

## 2. Portas (o que escuta e quem pode usar)

Tudo que o **aplicativo** usa na internet:

| Onde | Porta | Quem usa |
|---|---|---|
| `app.lzgames.com.br` | **443** HTTPS | o APK. Único endereço. |

Nesta máquina, **só loopback** (127.0.0.1). O celular não alcança:

| Porta | Processo | Função |
|---|---|---|
| **5192** | `turborama-station-api` | API Station (catálogo, capa, download, sessão, ativação) |
| **5190** | `turborama-suite-api` | Suite Windows. **Não** misturar com Station |
| **5191** | `turborama-suite-content-gateway` | Conteúdo Suite. Station **não** baixa jogo por aqui |
| **5194** | helper `turborama-station-issue-admin` | TurboBox emitir senha. O app **não** chama |
| 80 / 443 | nginx | recebe o túnel e encaminha `/v1/station/` para 5192 |

Estado ao gerar este handoff:

| Unidade | Estado | PID |
|---|---|---|
| `turborama-station-api` (5192) | active | 281271 |
| `turborama-suite-api` (5190) | active | 2943 |
| content-gateway (5191) | active | 2948 |
| PIX | active | 2940 |
| nginx | active | 3095 |
| cloudflared | active | 5781 |

`GET http://127.0.0.1:5192/ready/station` → 200  
`GET http://127.0.0.1:5192/health` → 200  

Hash da DLL da 5192: `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`  
Código: branch `feat/station-library-grant-20261002` commit `b1159c9`.

Nginx: `location ^~ /v1/station/artifacts/` timeout **900 s**, `proxy_buffering off`, `proxy_cache off`. Demais `/v1/station/` timeout 15 s. Sem cookie. Corpo JSON até 8 k.

---

## 3. Como o servidor **libera** o acesso

Há três portas de entrada, nesta ordem. Sem a anterior, a seguinte recusa.

### 3.1 Senha (uma vez por aparelho)

- Prefixo **`STA-`**.
- Primeira senha da compra: vale **48 horas**.
- Reemissão humana no TurboBox `/admin/station`: vale **30 minutos** e **invalida** a anterior.
- WhatsApp sai do **servidor** (helper 5194 + Menuia). O app não envia WhatsApp.
- Um aparelho ativo por licença. Copiar o APK para outro telefone **não** copia a chave do Keystore: o outro aparelho não entra.

Licença de teste já BOUND (não emitir outra, não ativar de novo desta máquina):

- código: `STA-D7AE45616B415B2C7550315C0392C5D8`
- nome no perfil: `Teste Station`
- telefone: Samsung SM-A566E, serial `RQCY30751WY`

Ativação no app:

1. `POST /v1/station/activations/challenge` — manda a senha + chave pública do aparelho. Domínio `TurboRamaStationAndroid/request-activation-challenge/v1`.
2. Servidor devolve desafio (vale **60 s**).
3. `POST /v1/station/activations/complete` — envelope `{ payload, signature }` assinado no Keystore (RSA-PSS). Domínio `TurboRamaStationAndroid/activate/v1`.
4. Resposta `activated/v1` com `licenseId`. O app grava `licenseId` em `station-license-id.txt` (privado). **Não** grave a senha.

### 3.2 Sessão (toda vez que o token caiu)

Token vive **180 segundos**, só na memória. Sem heartbeat. Se expirou, o app refaz:

1. `POST /v1/station/challenges` — domínio `request-session-challenge/v1`, com `licenseId`.
2. `POST /v1/station/sessions` — envelope PSS. Domínio `open-session/v1`.
3. Resposta `session/v1` com `accessToken`.

Todas as rotas da biblioteca usam cabeçalho:

`Authorization: Bearer <accessToken>`

### 3.3 O que o servidor exige em **todo** JSON de identidade

Campos camelCase:

- `schemaVersion`: `1`
- `domain`: exatamente o domínio daquela rota (tabela abaixo)
- `productId`: `TURBORAMA_STATION_ANDROID`
- `applicationId`: `TURBORAMA_STATION_ANDROID`
- `deviceId`: Base64URL do SHA-256 da SPKI pública RSA-2048 do Keystore (32 bytes)
- `clientVersion`, `deviceManufacturer`, `deviceModel`, `androidSdk`

Envelope que o **servidor** devolve: `{ keyId, payload, signature }` em Base64URL **sem** padding.  
Assinatura: RSA-PSS SHA-256, MGF1-SHA-256, salt 32, chave 2048.  
O app confere `keyId`, depois a assinatura no `payload` UTF-8 **exato**.

- keyId Station: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`
- Pin TLS SPKI SHA-256 de `app.lzgames.com.br`: `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`  
  (já estão em `StationConfig.java`; não copie para outro arquivo)

`HttpURLConnection.setInstanceFollowRedirects(false)` em **todas** as chamadas. Cookie: não. Query `?e=` / `?s=`: não.

---

## 4. Rotas que o app chama (contrato fechado)

Host: `https://app.lzgames.com.br`  
Prefixo: `/v1/station/`  
Produto: `TURBORAMA_STATION_ANDROID`  
Prefixo dos domains: `TurboRamaStationAndroid/`

| # | Método e caminho | Precisa Bearer? | Pedido | Resposta 200 | Para que serve |
|---|---|---|---|---|---|
| 1 | `POST /activations/challenge` | não | JSON identidade + `activationCode` + `devicePublicKey`, domain `request-activation-challenge` | `activation-challenge/v1` | começar a prender o aparelho |
| 2 | `POST /activations/complete` | envelope PSS | domain `activate` | `activated/v1` (`licenseId`) | terminar a ativação |
| 3 | `POST /challenges` | não | identidade + `licenseId`, domain `request-session-challenge` | `session-challenge/v1` | desafio da sessão |
| 4 | `POST /sessions` | envelope PSS | domain `open-session` | `session/v1` (`accessToken`, 180 s) | token |
| 5 | `GET /me` | sim | — | `profile/v1` (`displayName`, `profileVersion`) | título `Bem-vindo, {nome}` |
| 6 | `GET /catalog` | sim | — | `catalog/v1` (`revision`, `items[]`) | lista de jogos **sem URL** |
| 7 | `GET /covers/{coverId}` | sim | — | **bytes** de imagem | capa na tela |
| 8 | `POST /downloads/authorize` | sim | identidade + `itemId`, domain `request-download` | `download-grant/v1` (`grantId`, `expiresInSeconds=60`) | permissão de **um** uso |
| 9 | `GET /artifacts/{grantId}` | sim | — | **bytes** `application/octet-stream` | o arquivo do jogo |

`itemId` e `coverId`: 8–64 caracteres `[A-Za-z0-9_-]`, sem `..`. Hoje são **32 hex**.

JSON de authorize (copie este formato):

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
  "itemId": "<itemId vindo do catálogo>"
}
```

### 4.1 Lista (`catalog/v1`)

Cada item: `itemId`, `name`, `platform`, `revision`, `coverId`.  
**Não** vem pasta, **não** vem URL, **não** vem `filePath`.

Índice ao vivo, revisão **1**, **996** itens:

| platform | itens |
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

No disco de jogos (o que o operador colocou), Super Nintendo **835** + Mega Drive **981** = **1816** arquivos. O app só vê os **996** que casaram com a tabela XML **e** tinham capa válida.

503 `STATION_CATALOG_NOT_READY`: o app abre a **última lista salva**. Não trava a biblioteca.

O leitor JSON de 8 kB do `StationClient` **não** serve para o catálogo (o envelope passa de 8 k). Use leitor de até **2 MiB** e timeout 60 s.

### 4.2 Capa

O XML antigo tinha pasta de imagens. No Station a arte vem da **revista**, já redimensionada para o celular: lado maior **720 px**, JPEG ~85, todas as 996 **abaixo de 2 MiB** (~97 KiB a ~786 KiB).

O telefone pede só o `coverId` do catálogo:

`GET /v1/station/covers/{coverId}` + Bearer.

Resposta: bytes, `Content-Type` `image/jpeg` (ou png/webp/gif), `Cache-Control: no-store`, timeout 10 s. Teto no código: 5 MiB.

A capa **não** autoriza o download do jogo. Não baixe as 996 no login: só as que faltam no cache (`coverId` + revisão).

Sem sessão: **401**. `coverId` inventado: **404**. **Não existe link público de capa.** Abrir capa no Chrome sem login quebraria a proteção.

### 4.3 Jogo (um uso)

Só quando a pessoa inicia um jogo **e** o arquivo ainda não está no celular. **Não** no login.

1. `POST /downloads/authorize` → `grantId` vale **60 s** para **começar**. Só memória. Sem URL.
2. `GET /artifacts/{grantId}` **uma vez**, mesmo Bearer, arquivo temporário, depois troca o nome. `Content-Length` obrigatório. Timeout longo (nginx 900 s).
3. Segundo GET, outro aparelho, grant vencido ou Bearer errado: **404** `STATION_GRANT_NOT_FOUND`. Isso mata link copiado.
4. Se a resposta tiver `Location` ou 302/307: **falha**. Apague o parcial.

Tabela `suite.station_download_grants` (migration 029). Cifra AES-GCM Station no arquivo `station-download.key` (32 bytes, modo 600), **diferente** dos peppers Suite e de ativação.

Subida medida da origem ~30 Mbps. Um ROM de 4 MB leva cerca de 1 s por aparelho. Não abrir vários artifacts em paralelo no mesmo telefone.

---

## 5. O que o servidor espera do app (checklist)

1. Só `https://app.lzgames.com.br/v1/station/…`
2. Pin TLS SPKI; recusar certificado que não bata
3. Sem WebView nessa API (WAF com JS Challenge quebra o APK nativo)
4. Sem cookie, sem token Suite, sem Sambox, sem `miami`, sem `?e=` / `?s=`
5. `setInstanceFollowRedirects(false)`
6. Bearer só na memória; 180 s; renovar com challenges + sessions
7. Título `Bem-vindo, {displayName}`; aposentar `BEM-VINDO DE VOLTA`
8. 503 em `/me` **não** apaga o último nome
9. Com `station-license-id.txt` e sessão ok: **não** pedir senha de novo
10. Lista e capas em cache privado (`getNoBackupFilesDir()`), **sem** URL
11. Jogo em `station-games/<itemId>`, grant só memória
12. Gamelist local **sem** `http`
13. 30 pedidos por IP por rota por minuto → `STATION_RATE_LIMITED`; esperar, não martelar `activations/*`

---

## 6. Códigos de erro (o app mostra / trata)

| Código | HTTP típico | Significado |
|---|---|---|
| `STATION_SESSION_INVALID` | 401 | sem Bearer, token morto ou sessão errada |
| `STATION_GRANT_NOT_FOUND` | 404 | grant inexistente, já usado, vencido ou de outro aparelho |
| `STATION_ITEM_NOT_FOUND` | 404 | `itemId` fora do índice |
| `STATION_COVER_NOT_FOUND` | 404 | `coverId` fora do índice |
| `STATION_CATALOG_NOT_READY` | 503 | índice ausente; usar lista salva |
| `STATION_DOWNLOAD_NOT_READY` | 503 | índice ou cifra de grant ausente |
| `STATION_PROFILE_NOT_READY` | 503 | nome ainda não no perfil; manter o último |
| `STATION_RATE_LIMITED` | 429 | 30/min naquela rota |
| `STATION_IDENTITY_INVALID` | 400 | domain/produto/deviceId errados |
| `JSON_INVALID` | 400 | corpo JSON inválido |
| `STATION_ACTIVATION_INVALID` | 401/403 | senha errada, vencida ou já usada noutro aparelho |
| `STATION_CHALLENGE_INVALID` / `_MISMATCH` | 401 | desafio morto ou resposta não bate |
| `STATION_DEVICE_DENIED` | 403 | aparelho não é o da sessão |
| `PRODUCT_DENIED` | 403 | tentou `/v1/suite/` com produto Station |

Prova pública **sem sessão** (02/10/2026 e reteste depois):

- `GET /catalog`, `/covers/…`, `/me`, `POST /downloads/authorize` → **401** `STATION_SESSION_INVALID`
- `GET /artifacts/{id}` inexistente → **404** `STATION_GRANT_NOT_FOUND`, **sem** `Location`
- `cf-cache-status: DYNAMIC`, `cache-control: no-store`

HEAD em artifacts → 405.

---

## 7. Como um humano testa

**Neste servidor (sem celular):**

```bash
curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5192/ready/station
# esperado: 200

curl -sS https://app.lzgames.com.br/v1/station/catalog
# esperado: 401 STATION_SESSION_INVALID
```

Isso prova que a API está no ar e que **não** há lista solta na internet.

**No Samsung (teste de verdade de capa e jogo):**

1. APK TESTE instalado com `adb install -r --no-incremental` (não apagar dados).
2. Campo de senha vazio, com `station-license-id.txt` já existente → entra na sessão Station. **Não** digitar de novo o `STA-D7AE…`.
3. Título: `Bem-vindo, Teste Station`.
4. Biblioteca lista os itens do catálogo; capas por `coverId`.
5. Ao iniciar um jogo que ainda não está no disco do telefone: authorize + artifacts, **sem** URL na gamelist.

Não existe página web para “ver as capas”. Capa sem Bearer seria link clonável.

---

## 8. O que **não** fazer

- Não apontar o app para IP, porta 5192, 5190, 5191, 5194, Sambox, Suite.
- Não criar URL pública de jogo ou capa (R2, nginx, Cloudflare cache).
- Não seguir redirect no download.
- Não gravar `grantId`, bearer ou senha em disco/log.
- Não emitir senha nem ativar desta máquina.
- Não reiniciar 5190 / 5191 / PIX para mexer em Station.
- Não misturar HMAC Sambox com RSA-PSS Station.
- Não desinstalar o APK de teste só para “limpar”.
- Não alterar `jb.js` T4S, DLL da Suite 5190, PIX, gateway 5191, site, SPA `/admin`.

---

## 9. Onde está o código e o que falta

| Peça | Onde | Estado |
|---|---|---|
| API Station | `feat/station-library-grant-20261002` `b1159c9`, DLL na 5192 | no ar |
| Índice | arquivo configurado `Station__LibraryIndexFile`, **não** vai para o Git | 996 itens carregados |
| Handoff do APK (Java, dex, empacote) | `HANDOFF-APP-PRODUCAO-STATION-20261002.md` branch `docs/app-producao-station-20261002` | **falta empacotar no Windows** |
| Fonte Java | `E:\ESTUDO APK\work\native-carousel\implementation\station-security-20261001\java\org\emulationstation\frontend\auth\` | máquina Windows |
| Pacote do APK | `org.turboramastation.frontend` | — |
| Empacote | `package_station_login.py` → só `classes8.dex`; recusar `miami`/`sambox`; `zipalign`; keystore de debug já usado | Windows |
| Retorno do APK depois do install | `RETORNO-APP-PRODUCAO-STATION-20261002.md` | preencher no Windows |

Classes Java a completar no Windows: `StationProtocol` (domains catalog / request-download / download-grant), `StationProtocolPath` (catalog, covers, authorize, artifacts), `StationClient` (leitor grande + bytes), `StationAuth` (depois do perfil, atualizar lista), classe nova `StationLibrary` (cache e download sob demanda). Não editar o campo de senha. Não alterar `libturbo_carousel.so`. Diff do APK, fora `META-INF`, só `classes8.dex`. SHA-256 do Cemu estável `7ce3fab3d2d09c3bddfd002d0b9734e42aa5e27b102f36bb56e77b484e36562b` permanece igual.

---

## 10. Confirmação deste handoff

- parou porque: handoff-humano-completo
- servidor pronto para o app consumir: sim
- lista / capa / download de um uso no ar: sim
- porta pública do app: 443 em `app.lzgames.com.br`
- porta da API no host: 5192 só em 127.0.0.1
- senha emitida nesta rodada: nao
- ativação desta máquina: nao
- WhatsApp: nao
- Cloudflare editado: nao
- 5190 / 5191 / PIX reiniciados: nao
- índice no Git: nao
- APK empacotado neste Linux: nao
