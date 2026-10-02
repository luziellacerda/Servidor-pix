# Handoff — implementação no aplicativo Turborama Station

Data: 02/10/2026. Destino: **APK / repositório do aplicativo Android**. Base do servidor: `docs/seguranca-jogos-station-20261002` commit `1f9506a`. O servidor já ativa, abre sessão e entrega o nome `Teste Station`. Esta rodada **não mexe no servidor**. Catálogo, download e artifacts continuam fechados até outro handoff mandar abrir.

Telefone de referência: Samsung `RQCY30751WY`. Licença de teste `STA-D7AE45616B415B2C7550315C0392C5D8` (BOUND, um aparelho). Não emita senha. Não ative de novo. Não cole pepper, DSN, token, chave privada, device id, ponto de montagem nem URL de jogo.

## 1. O que o servidor já faz (não refaça lá)

- Base pública: `https://app.lzgames.com.br/v1/station/`. Processo em `127.0.0.1:5192`. Borda Cloudflare, `cf-cache-status: DYNAMIC`, `cache-control: no-store`. Túnel `cloudflared` ativo.
- Produto e aplicação: `TURBORAMA_STATION_ANDROID`. Prefixo dos domains: `TurboRamaStationAndroid/`.
- keyId Station: `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`. Pin TLS SPKI SHA-256 de `app.lzgames.com.br`: `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`.
- Assinatura do servidor e do aparelho: RSA-PSS SHA-256, MGF1-SHA-256, salt 32, chave 2048. Envelope `{ keyId, payload, signature }` em Base64URL sem padding. Envelope do aparelho: `{ payload, signature }`.
- `deviceId` = Base64URL do SHA-256 da SPKI pública do Keystore. Copiar o APK **não** copia essa chave.
- Desafio 60 s. Sessão 180 s. Sem heartbeat. Um aparelho ativo por licença.
- Primeira senha da compra: 48 horas. Reemissão humana em TurboBox `/admin/station`: 30 minutos e mata a anterior. WhatsApp é do servidor (compra, reemissão confirmada, ativação). O app **não** envia WhatsApp.
- `GET /v1/station/me` com bearer devolve `profile/v1` com `displayName` (máx. 80) e `profileVersion`. Na licença de teste o nome é `Teste Station`, versão 1.
- Sem sessão: catálogo e authorize 401 `STATION_SESSION_INVALID`. Com sessão: 503 `STATION_CATALOG_NOT_READY` e `STATION_DOWNLOAD_NOT_READY`. `GET /v1/station/artifacts/{grantId}` 404.
- Biblioteca de jogos no servidor: volumes que o operador apontar. Jogos e HDs novos entram no índice. O telefone **nunca** vê caminho de disco.

## 2. O que o APK já faz e o que falha no uso

Já funciona no Samsung:

- Extra `stationLogin`, senha digitada, sai do login, abre a biblioteca.
- Gravou `station-license-id.txt`. Entrada seguinte com campo vazio também abriu a biblioteca.
- Keystore, desafio, ativação e sessão no contrato atual.

Falha de produto (corrigir no APK):

1. O título da abertura é o texto fixo `BEM-VINDO DE VOLTA`. **Não consulta o servidor.**
2. O nome só entra na frase `Bem-vindo, {displayName}` da linha de status. Se `GET /me` não roda, a pessoa não aparece.
3. Abrir pelo ícone usa a senha local antiga e **não chama** `/v1/station/me`.
4. Se `/me` responde 503 `STATION_PROFILE_NOT_READY`, o app grava nome vazio e mostra só `Bem-vindo`, mesmo que o servidor já tenha o nome (hoje tem).
5. Catálogo 503: a biblioteca precisa continuar com a última lista salva. Não travar a abertura.
6. Não há cliente de capa por revisão nem cliente de concessão de um uso. Quando o servidor abrir, o APK desatualizado não pode passar a guardar URL de arquivo.

## 3. Contrato que o app deve cumprir

Host único: `https://app.lzgames.com.br`. Só o prefixo `/v1/station/`. Sem `/v1/suite/`. Sem host Sambox. Sem IP da máquina. Sem HTTP claro.

Toda resposta 200 de negócio vem no envelope assinado. Verifique `keyId`, depois `payload` UTF-8 exato, depois RSA-PSS. `schemaVersion` = 1. `productId` e `applicationId` = `TURBORAMA_STATION_ANDROID`.

| Rota | Auth | Domain do pedido | Domain da resposta | Hoje | App |
|---|---|---|---|---|---|
| `POST /activations/challenge` | não | `…/request-activation-challenge/v1` | `…/activation-challenge/v1` | 200/403 | senha `STA-…` |
| `POST /activations/complete` | envelope PSS | `…/activate/v1` | `…/activated/v1` | 200 | uma vez por aparelho |
| `POST /challenges` | não | `…/request-session-challenge/v1` | `…/session-challenge/v1` | 200 | a cada sessão |
| `POST /sessions` | envelope PSS | `…/open-session/v1` | `…/session/v1` | 200, `accessToken`, 180 s | bearer só memória |
| `GET /me` | `Authorization: Bearer` | — | `…/profile/v1` | 200 com nome | ver secção 4 |
| `GET /catalog` | Bearer | — | futuro `…/catalog/v1` | 503 | ver secção 5 |
| `POST /downloads/authorize` | Bearer, corpo com `itemId` | `…/request-download/v1` (quando abrir) | futuro grant | 503 | ver secção 6 |
| `GET /artifacts/{grantId}` | futuro, um uso | — | bytes | 404 | ver secção 6 |

Corpo máximo 8 k. Sem cookie. Sem query de caminho de arquivo. `itemId` só identificador.

Códigos que o UI precisa tratar: `STATION_ACTIVATION_INVALID` (senha errada/vencida — pedir nova na loja; 48 h na compra, 30 min na reemissão), `STATION_CHALLENGE_INVALID` / `STATION_CHALLENGE_MISMATCH` (refazer desafio), `STATION_DEVICE_DENIED` (outro aparelho ou licença), `STATION_SESSION_INVALID` (nova sessão), `STATION_PROFILE_NOT_READY` (não apagar o último nome bom), `STATION_CATALOG_NOT_READY` / `STATION_DOWNLOAD_NOT_READY` (usar cache, biblioteca abre), `RATE_LIMITED`.

## 4. Nome do consumidor — implementar agora

O servidor **já entrega** `displayName`. O APK é que não mostra.

1. Em **toda** abertura da biblioteca (ícone, volta ao app, depois do login), abra sessão se o token de 180 s expirou e chame `GET /v1/station/me`.
2. Título visível: `Bem-vindo, {displayName}` quando o nome vier. Sem nome: `Bem-vindo`. Aposente `BEM-VINDO DE VOLTA` como título fixo.
3. 200 `profile/v1`: grave `displayName` e `profileVersion` no armazenamento privado do app. Não grave o bearer.
4. 503 `STATION_PROFILE_NOT_READY`: **mantenha** o último nome gravado. Não zere.
5. 401: recrie sessão e tente `/me` de novo. Se a licença local sumiu, volte à tela da senha.
6. Não peça senha de novo se `station-license-id.txt` existe e a sessão abrir. Senha só no primeiro vínculo ou se o servidor recusar o aparelho.

## 5. Lista e capas no login — preparar o cliente agora, sem URL permanente

O servidor ainda responde 503 no catálogo. O app precisa se comportar assim **já**, para não depender de atualizar o APK quando o índice abrir e quando o operador colocar jogo ou HD novo.

1. Depois de `/me`, chame `GET /v1/station/catalog` com o mesmo bearer.
2. 503 ou rede: abra a biblioteca com a **última lista salva**. Não bloqueie o login. Não mostre caminho de servidor.
3. 200 futuro `catalog/v1`: corpo assinado com revisão de catálogo, itens (`itemId`, nome, plataforma, revisão de capa). **Sem URL de arquivo, sem host de capa Sambox, sem caminho de HD.**
4. Capas: cache local chaveado por `itemId` + revisão de capa. Se a revisão mudou, baixe **só** as capas novas, com a mesma sessão (bytes ou concessão curta de capa — nunca URL permanente). Capa não autoriza o jogo.
5. Jogo novo no servidor aparece no **próximo** login, sem atualizar o APK. O cliente só substitui a lista quando a revisão do catálogo muda.

Não implemente um downloader de arquivo nesta tela.

## 6. Download do jogo — só quando o servidor abrir; o APK já deve recusar link clonado

Não chame authorize em loop enquanto for 503. Quando o handoff de abertura existir:

1. `POST /v1/station/downloads/authorize` com `itemId` e identidade do aparelho. Sem path.
2. A resposta futura traz `grantId` curto, um uso, preso a licença + device + item. Grave o grant só na memória, prazo curto.
3. `GET /v1/station/artifacts/{grantId}` **uma vez**, para arquivo temporário privado. Segundo GET, outro app ou link compartilhado: falha. Isso é o antclone: Cloudflare não invalida o uso; o servidor consome.
4. Proibido: guardar URL de jogo, seguir 307 para origem, publicar o `grantId` em log/crash, R2 público, link no WhatsApp do app, copiar Location para a galeria.
5. APK copiado em outro telefone: Keystore diferente → `deviceId` diferente → sem sessão → sem grant → sem arquivo.

## 7. Cloudflare e ataque — o que o app faz e o que não faz

Já na borda: proxy, sem cache Station, túnel, origem escondida. O app:

- Fala só com `https://app.lzgames.com.br`. Pin SPKI. Recuse certificado que não bata.
- Não use WebView para a API. Não resolva Challenge/CAPTCHA/Access — a API Station **não** pode ganhar JS Challenge (quebra o APK).
- Trate 429 `RATE_LIMITED` com espera e uma nova sessão, sem martelar `activations/*`.
- Não envie cookie. Não anexe token Suite. Não use o host de conteúdo Windows.
- Link clonado: o app nunca mostra `artifacts/{grantId}` ao usuário. Download só pelo fluxo interno.

## 8. O que não fazer

- Não peça para o servidor abrir catálogo nesta rodada.
- Não altere 5190, 5191, 5192, PIX, Nginx, Cloudflare, WhatsApp, site, SPA `/admin`.
- Não apague dados do Samsung de teste, não desinstale o APK só para “limpar”, não troque o certificado de assinatura.
- Não misture contrato Suite (`TurboRamaSuite/`) com Station.
- Não imprima senha, bearer, nonce, grant, device id ou SPKI privada no retorno.

## 9. Ordem de implementação no APK

1. Sessão em toda abertura + `GET /me` + título `Bem-vindo, {nome}`.
2. Cache do último perfil; 503 não zera o nome.
3. `GET /catalog` após `/me`; 503 usa lista local; biblioteca sempre abre.
4. Cache de capas por revisão (cliente pronto; 503/404 é normal hoje).
5. Cliente de authorize + artifacts de um uso, **desligado** até o servidor abrir, mas já sem armazenar URL.

## 10. Retorno

Preencha `RETORNO-APP-IMPLEMENTAR-STATION-20261002.md` no repositório do app (ou nesta branch se o trabalho do APK for registrado aqui) e faça push. Sem segredo, sem senha, sem device id.
