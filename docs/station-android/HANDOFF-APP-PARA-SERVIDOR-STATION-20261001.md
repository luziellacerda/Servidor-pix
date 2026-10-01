# Handoff — app TurboramaStation para o servidor

Data: 01/10/2026. Este texto é a base para analisar o servidor e implementar só o que o app Station precisa. Se o app pedir uma coisa que o servidor já entrega de outro jeito, o ajuste volta para o app. O que já está no ar permanece, porque Suite Windows, EmulationStation, conteúdo, gateway, inventário e PIX já consomem essas rotas.

Nenhum segredo entra aqui: pepper, chave privada, senha, token admin, `sambox.key`, segredo HMAC e URL assinada de jogo ficam fora do documento, do Git e do chat.

## 0. Fio vigente do app — início de produção, 01/10/2026

As seções 2, 3, 6, 7, 8 e 9 descrevem o candidato local anterior: `deviceId` em hex, Base64 com padding, prova com byte nulo, heartbeat de 60 s e catálogo POST. Esse candidato não é o início de produção. O código em `station-security-20261001` foi reescrito para o commit `93efbba` de `origin/feat/station-android-contract-20260930`. `javac --release 17` contra `android-34` terminou com saída 0, inclusive `LoginActivity` no mesmo classpath. O APK TESTE não foi recompilado nem instalado. `StationConfig.ENABLED` permanece `false`. A chave de asserção Station está vazia, então `ready()` permanece falso e o campo da tela continua a senha local. Produção, SQL e o checkout do servidor permanecem como estavam.

Contrato que o app fala quando a flag e a chave Station existirem:

- Produto e aplicação `TURBORAMA_STATION_ANDROID`. URL `https://app.lzgames.com.br`, sem barra no fim. O pin TLS é o SHA-256 da SPKI da folha já publicado no envelope R25. A chave que assina a resposta Station é outra e ainda não foi emitida. As chaves online e de conteúdo da Suite ficam fora deste cliente.
- Aparelho: RSA 2048, alias `turborama.station.device.v1`, `SHA256withRSA/PSS`, MGF1-SHA256, salt 32. `deviceId` é Base64URL sem padding do SHA-256 da SPKI, 43 caracteres.
- Identidade em JSON camelCase: `schemaVersion` 1, `domain`, `productId`, `applicationId`, `deviceId`, `clientVersion` `1`, `deviceManufacturer`, `deviceModel`, `androidSdk`. O domínio não leva byte nulo. Não há `hardwareFingerprint`.
- Ativação: o campo digitado é só o código, Base64URL canônico de 32 bytes. `POST /v1/station/activations/challenge` usa o domínio `TurboRamaStationAndroid/request-activation-challenge/v1`, mais `activationCode` e `devicePublicKey`. A resposta assinada usa `TurboRamaStationAndroid/activation-challenge/v1` e `expiresInSeconds` 60. O completo é `POST /v1/station/activations/complete`, envelope só com `payload` e `signature`. O payload assinado usa `TurboRamaStationAndroid/activate/v1`. A assinatura cobre os bytes UTF-8 desse payload. A resposta usa `TurboRamaStationAndroid/activated/v1`. `licenseId` tem de 6 a 64 caracteres e só aceita letra ASCII, dígito, hífen e sublinhado. O id fica em `station-license-id.txt`, no diretório sem backup. O código não fica gravado.
- Sessão: campo vazio e arquivo de licença existente abre `POST /v1/station/challenges`, domínio `TurboRamaStationAndroid/request-session-challenge/v1`. A resposta usa `TurboRamaStationAndroid/session-challenge/v1`, 60 segundos. `POST /v1/station/sessions` assina `TurboRamaStationAndroid/open-session/v1`. A resposta `TurboRamaStationAndroid/session/v1` exige `expiresInSeconds` 180 e `accessToken` Base64URL de 32 bytes. O token fica só em memória. Não há heartbeat.
- Perfil: `GET /v1/station/me`, cabeçalho `Authorization: Bearer` mais o token. Domínio `TurboRamaStationAndroid/profile/v1`. HTTP 503 nesse GET vira nome vazio e a tela mostra `Bem-vindo`. O nome é truncado em 80 caracteres.
- Catálogo e download não são chamados. No candidato do Git essas rotas respondem 503 depois de uma sessão válida.
- Corpo máximo 8192 bytes. Sem redirecionamento, sem cookie, só HTTPS, prazo de 10 segundos. A resposta é conferida pelo `keyId` da chave Station, RSA-PSS sobre os bytes do payload, e em seguida pelo campo `domain`.

O código Station local do checkout `17af26c` continua sem commit e não é este fio. A migration local com nome 027 permanece sem aplicação. A migration candidata do Git é `028_station_android`. O número real sai do ledger do banco alvo.

Fontes lidas nesta rodada:

- App preparado: `E:\ESTUDO APK\work\native-carousel\implementation\station-security-20261001\java\org\emulationstation\frontend\auth\`
- Login: `E:\ESTUDO APK\work\native-carousel\implementation\brand-login\java\org\emulationstation\frontend\auth\LoginActivity.java`
- Clone do servidor, HEAD destacado `17af26c`: `E:\ESTUDO APK\work\server-auth-handoff\Servidor-pix`
- Chaves públicas conferidas: `H:\TurboramaAuthorityPublic-R25-20260902-NEW2`
- Handoff anterior, que continua valendo para o congelamento: `HANDOFF-SEGURANCA-DADOS-TURBORAMASTATION-ANTES-DE-IMPLEMENTAR.md`

O clone local tem código Station que não está no Git. A primeira redação deste arquivo não tinha lido os handoffs mais novos das outras branches. A seção 1.1 registra essa leitura.

Produção Linux não foi lida de novo nesta rodada. Antes de qualquer cópia para `/opt`, registrar o hash do DLL que está rodando.

---

## 1.1 O que o Git contém, lido depois

O checkout `Servidor-pix` é raso: o objeto local só tem o commit `17af26c` (08/09/2026 15:11, branch `codex/emulationstation-suite-extraction-linux-20260908`). Esse commit não tem `/v1/station`. As classes Station do disco são alteração local, fora do Git.

Os dois handoffs mais novos que esse checkout, já no remoto, foram lidos agora:

| Handoff | Onde está | O que fica de pé |
|---|---|---|
| `HANDOFF-PROGRAMA-PRODUCAO-LICENSE-NOT-FOUND-20260914.md` | `origin/main` `26486ef`, 14/09/2026 11:46. É o handoff com data mais recente do repositório | `main` é a linha PIX/legado e não traz a árvore Suite/ES. O incidente foi licença ausente no banco da API, com `POST /v1/suite/challenges` em 404 `LICENSE_NOT_FOUND`. DNS, TLS, Cloudflare e Nginx estavam de pé. `lzgames-api` na porta 8083 é outro processo. A DLL anotada na auditoria foi `/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908/server/TurboRamaSuiteOnlineServer.dll`, SHA-256 `93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1` |
| `docs/suite/HANDOFF-WHATSAPP-TODOS-DOWNLOADS-20260908.md` | `origin/codex/all-download-notifications-20260908` `bb87d18`, 08/09/2026 19:07. É o handoff Suite mais novo que o checkout | Todo download concluído pode gerar aviso. O aviso não dispara só porque o grant ficou `COMPLETED`. A rota nova é `POST /v1/suite/notifications/download-completed`. A migration desse commit é `027_suite_download_notifications` e dá `ALTER` em `suite.suite_extraction_notification_outbox` |

O handoff imediatamente anterior a esse de downloads, `docs/suite/HANDOFF-FECHAMENTO-DISPAROS-WHATSAPP-20260908.md`, no mesmo `bb87d18`, também foi lido. Ele manda partir de `17af26c`, registra o commit operacional instalado `353ab1d729ad625a986c96f85f3afa4a306cc1dd` em `/opt/turborama-suite-r5-releases/extraction-353ab1d-20260908`, e proíbe mexer em DNS, Cloudflare, Nginx, certificado, licença, sessão, grant, dispositivo, worker e banco existente. `353ab1d` e o SHA `93939be3…` são observações de dias diferentes. Nenhum dos dois autoriza copiar o clone por cima do que está rodando.

`bb87d18` não está no objeto pai do checkout raso. O diff dele contra `17af26c` acrescenta essa migration 027, `DownloadNotificationEndpoints.cs` e os dois handoffs de WhatsApp. A rota de extração que já está em `17af26c` permanece: `POST /v1/suite/notifications/extraction-completed`.

Consequência para a Station: o arquivo local `migrations/suite/027_suite_station_android.up.sql` não é o próximo número do Git. O número 027 já nomeia `027_suite_download_notifications` em `bb87d18`. Esse arquivo local não será aplicado. O número e o `version` da Station saem do `schema_migrations` do banco alvo, depois dessa 027 de avisos, se ela já estiver lá.

Os handoffs locais de 01/10 também foram relidos: `HANDOFF-SERVIDOR-ULTIMO-COMMIT-20261001.md` e `HANDOFF-SEGURANCA-DADOS-TURBORAMASTATION-ANTES-DE-IMPLEMENTAR.md`. O de último commit ainda lista `GET /v1/station/me` e `GET /v1/station/catalog` como proposta de 30/09. A seção 4 do handoff de segurança ainda cita domínios antigos (`activate/v1`, `session-challenge/v1`, `open-session/v1`) e um `GET /v1/station/me`. O fio vigente do app está na seção 0 e é o commit `93efbba`. O `StationProtocol.cs` local, sem commit neste checkout, é outro candidato e não descreve o que o app envia. O que já está no ar continua o da tabela da seção 4.

O cliente Windows no Git de produto está em `a1a9ba9` (14/09/2026, “ui: ocultar contagem de jogos no menu lateral”), no clone `H:\TURBORAMA SUITE COMPILAÇÃO\TRUBORAMA-SUITE-v2.0.2-clone`. O commit não mexe em licença nem em servidor. O snapshot `E:\ESTUDO APK\work\server-auth-handoff\TRUBORAMA-SUITE` está em `main` `86e7ae1` (04/09) e é mais antigo.

## 1. Regra deste handoff

1. Rota, produto, tabela, CHECK, header, prazo e assinatura que um serviço já consome permanecem byte a byte.
2. O app Station fala o fio da seção 0, o mesmo do commit `93efbba`. As seções 2, 3, 6, 7, 8 e 9 descrevem o candidato local anterior e não são pedido de implementação.
3. Onde o app e o commit `93efbba` discordam, o ajuste volta para o app. As rotas que Suite, EmulationStation, conteúdo, gateway, inventário e PIX já consomem permanecem.
4. O APK instalado no telefone ainda não fala com `/v1/station`. A flag permanece desligada até uma rodada própria de empacotamento.

---

## 2. Como o app está

Histórico do candidato local anterior. O estado vigente do app está na seção 0.

### 2.1 O que o telefone executa hoje

O APK de trabalho continua `E:\ESTUDO APK\work\native-carousel\implementation\side-by-side-turborama-20261001\TurboramaStation-TESTE-lado-a-lado.apk`. Ele não foi reempacotado nesta rodada. O manifesto usa `org.turboramastation.frontend`. As classes Java continuam `org.emulationstation.frontend`. A porta exportada é `LoginActivity`. `ESActivity` abre depois do login.

O login que o aparelho executa é senha local. `AuthSession.authenticate` chama `LocalPassword.matches` e, se passar, grava `noBackupFilesDir/authenticated-session-v1.bin` com AES-GCM no alias `turborama.auth.session.v1`. Esse arquivo é um marcador de tela, não uma licença, não um `sessionId` e não uma prova de compra.

A loja que o aparelho executa é a linha Sambox, em `GameDownload.java`: host `samboxmanager.squareweb.app` com HMAC `X-App-*` e AppId `sambox-pc`, e host `miami.sambox.buzz` com `?e=&s=` vindo de `drawers-signed.json`. Essa linha permanece no app e fora do servidor Suite. O servidor não passa a emitir `drawers.json` nem a validar esse HMAC.

### 2.2 O que as fontes já preparam e o telefone ainda não roda

As classes `StationConfig`, `StationAuth`, `StationClient`, `StationCrypto`, `StationProtocol` e `StationProtocolPath` compilam contra `android-34`. `StationConfig.ENABLED` está `false`. `ready()` exige a flag, URL HTTPS sem barra no fim, pin TLS de 64 hex e as duas SPKI. Com a flag falsa, `ready()` fica falso. `LoginActivity.submit` só entra em `StationAuth` quando `ready()` é verdadeiro. No estado atual o campo continua a senha local.

Quando a flag for ligada, o campo único funciona assim:

- Texto com espaço: o trecho antes do primeiro espaço é `licenseId`, o resto é o código de ativação. O app chama ativação e grava só o id em `noBackupFilesDir/station-license-id.txt`.
- Texto sem espaço e com esse arquivo presente: o app abre sessão com o id gravado.
- Sem espaço e sem arquivo: a mensagem é `Informe a licenca e o codigo de acesso.`
- Qualquer falha de rede ou de assinatura vira `Nao foi possivel autorizar este aparelho.` O app não mostra o código cru do servidor.

A identidade do aparelho é RSA 2048 no Android Keystore, alias `turborama.station.device.v1`, `PURPOSE_SIGN`, SHA-256, PSS com salt 32. A privada não sai do aparelho. `deviceId` é SHA-256, em hex minúsculo, dos bytes SPKI da pública. `hardwareFingerprint` é SHA-256 do `Settings.Secure.ANDROID_ID` em UTF-8. Isso é identificador do aparelho, não atestação de hardware. `agentVersion` e `clientVersion` vão como `1`.

O app fala só HTTPS, sem cookie, sem proxy próprio, sem seguir redirect, timeout de 10 segundos. O pin TLS compara SHA-256 da SPKI da folha do certificado com `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`. Esse valor saiu dos dois envelopes R25, válidos de 02/09/2026 a 02/09/2027. Esta rodada não fez handshake novo contra o host vivo. Antes de ligar o app, comparar o pin da folha que está no ar com esse valor. Se for outro, o pin do app é que se atualiza. O certificado que a Suite já confia permanece.

---

## 3. O que o app vai pedir ao servidor

Histórico. O app não pede este fio. O pedido vigente está na seção 0.

Base `https://app.lzgames.com.br`, sem barra final. Produto e application `TURBORAMA_STATION_ANDROID`. Algoritmo `rsa-pss-sha256`. Binding `ANDROID_KEYSTORE_ONLINE`. O app não envia `X-TurboRama-Client`, não envia cookie e não manda a prova no domínio da Suite.

A prova do aparelho é RSA-PSS-SHA256 sobre:

```text
TurboRamaStationAndroid/machine-proof/v1\0
+ JSON canônico, nesta ordem:
  schemaVersion, challengeId, nonce, expiresAtUnixSeconds,
  licenseId, deviceId, sessionId, action, contextHash
```

O `\0` é o byte nulo. `sessionId` vai vazio na ativação. Hex é minúsculo. Base64 da assinatura e da SPKI é o padrão, com padding.

O app escolhe a chave pública que verifica a resposta:

| Resposta | keyId que o app exige |
|---|---|
| Ativação, sessão, perfil e os desafios dessas ações | `2d8987dc740a47cba0a8ee7c3e0fbffaff057e2325c46ea2324ee335de69da50` |
| Catálogo, concessão e os desafios dessas ações | `20d22a5e84efe7fb10c235131d92890778137fb10d441fa7706932baa2f8150f` |

Esses ids são as chaves pública online e de conteúdo já assinadas no pacote R25. Os DER emissores da pasta só autenticam o envelope. Eles não assinam o tráfego. A sessão que o app aceita traz `heartbeatAfterSeconds` igual a 60. Outro número é rejeitado no cliente.

### 3.1 Chamadas que o fonte já faz

`POST /v1/station/activations/challenge`

```json
{
  "schemaVersion": 1,
  "productId": "TURBORAMA_STATION_ANDROID",
  "applicationId": "TURBORAMA_STATION_ANDROID",
  "licenseId": "STA-...",
  "activationCode": "...",
  "device": {
    "schemaVersion": 1,
    "deviceId": "<64 hex>",
    "bindingType": "ANDROID_KEYSTORE_ONLINE",
    "algorithm": "rsa-pss-sha256",
    "publicKeySpki": "<base64>",
    "hardwareFingerprint": "<64 hex>",
    "agentVersion": "1"
  }
}
```

A resposta é um envelope. O app exige `kind` `TURBORAMA_STATION_ANDROID_ACTIVATION_CHALLENGE`, o keyId online e assinatura sobre `TurboRamaStationAndroid/activation-challenge/v1\0` mais o payload. Do payload o app lê `challengeId`, `nonce` e `expiresAtUnixSeconds`.

`POST /v1/station/activations/complete`

O corpo é plano: `schemaVersion`, `productId`, `applicationId`, `licenseId`, `challengeId`, `device` e `signature`. O app exige o kind `TURBORAMA_STATION_ANDROID_ACTIVATION_RESULT` no domínio `TurboRamaStationAndroid/activated/v1\0`.

`POST /v1/station/challenges`

O app gera `sessionId` de 32 bytes, hex minúsculo de 64 caracteres. O corpo é plano: `schemaVersion`, `productId`, `applicationId`, `licenseId`, `deviceId`, `sessionId`, `action` `session.open`, `contextHash`. O hash cobre, nesta ordem: `schemaVersion`, `productId`, `applicationId`, `licenseId`, `deviceId`, `sessionId`, `action`, `hardwareFingerprint`, `clientVersion`. A resposta exige kind `TURBORAMA_STATION_ANDROID_SESSION_OPEN_CHALLENGE` e domínio `TurboRamaStationAndroid/session-open-challenge/v1\0`.

`POST /v1/station/sessions`

```json
{
  "proof": {
    "schemaVersion": 1,
    "productId": "TURBORAMA_STATION_ANDROID",
    "applicationId": "TURBORAMA_STATION_ANDROID",
    "licenseId": "STA-...",
    "deviceId": "<64 hex>",
    "sessionId": "<64 hex>",
    "action": "session.open",
    "contextHash": "<64 hex>",
    "challengeId": "<64 hex>",
    "signature": "<base64>"
  },
  "context": {
    "schemaVersion": 1,
    "productId": "TURBORAMA_STATION_ANDROID",
    "applicationId": "TURBORAMA_STATION_ANDROID",
    "licenseId": "STA-...",
    "deviceId": "<64 hex>",
    "sessionId": "<64 hex>",
    "action": "session.open",
    "hardwareFingerprint": "<64 hex>",
    "clientVersion": "1"
  }
}
```

A resposta exige kind `TURBORAMA_STATION_ANDROID_SESSION_OPEN`, domínio `TurboRamaStationAndroid/session-open/v1\0` e `heartbeatAfterSeconds` igual a 60.

### 3.2 Chamadas que o contrato do servidor já tem e o app ainda não faz

Estas rotas já estão desenhadas no clone. O app ainda não tem método para elas. Quando o app for completado, ele fala neste formato. O servidor não inventa outro.

| Método e rota | Ação | Kind da resposta | Domínio da assinatura |
|---|---|---|---|
| `POST /v1/station/challenges` | `session.heartbeat`, `catalog.read`, `profile.read`, `download.authorize` | kind de desafio correspondente | `session-heartbeat-challenge`, `catalog-read-challenge`, `profile-read-challenge`, `download-authorize-challenge` |
| `POST /v1/station/sessions` | `session.heartbeat` | `TURBORAMA_STATION_ANDROID_SESSION_HEARTBEAT` | `TurboRamaStationAndroid/session/v1\0` |
| `POST /v1/station/me` | `profile.read` | `TURBORAMA_STATION_ANDROID_PROFILE` | `TurboRamaStationAndroid/profile/v1\0` |
| `POST /v1/station/catalog` | `catalog.read` | `TURBORAMA_STATION_ANDROID_CATALOG_PAGE` | `TurboRamaStationAndroid/catalog/v1\0` |
| `POST /v1/station/downloads/authorize` | `download.authorize` | `TURBORAMA_STATION_ANDROID_DOWNLOAD_GRANT` | `TurboRamaStationAndroid/download/v1\0` |

Catálogo é POST com prova. O corpo é `proof` mais `context` com `cursor` e `pageSize`. Página vazia ou identidade `empty` responde `503 CATALOG_NOT_READY`. Item `READY` traz descriptor sem URL. Item `MAINTENANCE` traz `reasonCode` `CONTENT_TEMPORARILY_UNAVAILABLE`. Se o payload contiver `http`, o servidor falha fechado.

A concessão traz `contentPath` `/v1/station/artifacts/{grantId}` e `bearerToken`. Não traz URL de origem. O GET desse path ainda não existe no clone. Ele é rota nova, no gateway, separada de `/v1/suite-content/artifacts/{grantId}`. O comportamento a copiar, sem alterar a rota antiga, é: um uso, bearer conferido pelo hash, resposta `307`, `Cache-Control: no-store`, sem cookie.

Prazos do contrato Station, já no clone: desafio 60 s, sessão 180 s, heartbeat 60 s, concessão 60 s, página até 64 itens, corpo até 64 KiB. Licença com prefixo `STA-`, tamanho 8 a 64, um aparelho. Binding aceito: só `ANDROID_KEYSTORE_ONLINE`. RSA 2048, SPKI canônico, `deviceId` igual ao SHA-256 dessa SPKI. SPKI ou assinatura em Base64 inválido responde `400 CONTRACT_INVALID`.

---

## 4. O que o servidor já tem e permanece

Clone conferido em `17af26c`, mais as rotas Station locais ainda desligadas. Produção real pode estar num binário anterior. A lista abaixo é o contrato que os serviços já consomem neste fonte. Ela não muda para caber no app.

| Já consumido | O que permanece |
|---|---|
| `POST /v1/suite/activations/challenge` e `/complete` | Produto `TURBORAMA_SUITE`. Licenças `TS-`. Binding `TPM_BOUND` ou `SOFTWARE_BOUND_ONLINE` |
| `POST /v1/suite/challenges` e `POST /v1/suite/sessions` | Desafio 60 s, sessão 180 s, heartbeat 5 s. O header `X-TurboRama-Client: EMULATIONSTATION` vale só nestes dois POST |
| `POST /v1/suite/emulationstation/challenges` e `/sessions` | Sessão ES própria |
| `POST /v1/suite-content/catalog/current` e `/downloads/authorize` | Catálogo Suite, contagem de produção 902, sem URL permanente no descriptor |
| `GET /v1/suite-content/artifacts/{grantId}` | Gateway `307` de uso único. Esta rota não ganha item da Station |
| `POST /v1/suite/devices/inventory/challenge` e `/inventory` | Inventário de aparelho da Suite |
| `POST /v1/suite/network/challenges` e `/inventory` | Inventário de rede |
| PIX em `TurboRamaPixOnlineServer` | Linha própria. `Issue()` continua emitindo `TURBORAMA_SUITE` |
| `POST /v1/suite/notifications/extraction-completed` | Aviso de extração já em `17af26c`. Outbox e worker permanecem |
| `POST /v1/suite/notifications/download-completed` | Aviso de todo download concluído, em `bb87d18`. Não dispara só com grant `COMPLETED` |
| `GET /health`, `/ready`, `/ready/content` | Saúde da API que já está no ar |
| PIX `POST /v1/activations/*`, `/v1/challenges`, `/v1/sessions`, `/v1/orders`, `/v1/configuration/*` | Outro processo. A Station não entra nesses caminhos |
| `lzgames-api` porta 8083 e `turbobox.lzgames.com.br` | Outro produto. Reinício desse processo não substitui a API Suite |
| Admin em `TurboRamaSuiteAdminServer` | Painel separado. Hostname diferente do app |
| CHECKs SQL `product_id='TURBORAMA_SUITE'` nas migrations 001–026 | Permanecem. Station usa tabelas novas |
| `027_suite_download_notifications` | Já existe em `bb87d18` e altera `suite_extraction_notification_outbox`. A Station não reutiliza esse version nem esse `ALTER` |
| Domínio de prova `TurboRamaOnlineMachineProof/v1\0` | Continua da Suite e do PIX. Station usa `TurboRamaStationAndroid/machine-proof/v1\0` |
| Chaves online e de conteúdo | Continuam independentes. O processo já recusa subir se as duas SPKI forem a mesma |

Produto diferente de `TURBORAMA_SUITE` nessas rotas continua `403 PRODUCT_DENIED`. O app Station apontado para `/v1/suite` leva essa recusa. O servidor não abre o produto Android dentro da rota da Suite.

---

## 5. O que o clone já tem de Station e ainda não está em produção

Arquivos locais, sem commit e sem deploy:

- `StationProtocol.cs`, `StationService.cs`, `StationStore.cs`, `StationEndpoints.cs`, `StationSelfTest.cs`
- `Program.cs` chama `app.MapStation(false)` fixo. Com a flag falsa as rotas existem e respondem `404 NOT_FOUND`
- `Suite:Station:Enabled` default `false` em `appsettings.json`. Se a flag ficar verdadeira, o processo recusa subir enquanto a migration não for conferida no ledger vivo
- `migrations\suite\027_suite_station_android.up.sql` está no disco e não foi aplicada. O nome 027 só vale dentro do checkout raso. No Git, `bb87d18` já usa `027_suite_download_notifications`. Não há `PostgresStationStore`. `MemoryStationStore.SaveGrantAsync` não grava o hash do bearer
- `dotnet run -- --station-self-test` passou neste clone, com chave efêmera. Essa chave não é a do pacote R25

Self-test coberto: ativação, replay da mesma assinatura, código já usado, produto Suite recusado, sessão com heartbeat 60, nome de perfil escapado, catálogo sem `http`, concessão sem URL de origem, item em manutenção recusado, assinatura do servidor válida, domínio de prova diferente do da Suite, Base64 inválido como `400 CONTRACT_INVALID`.

---

## 6. O que implementar no servidor

Não implemente esta seção. Ela descreve o candidato local anterior. O candidato do Git já está no commit `93efbba`. O que ainda falta para venda e implantação continua no retorno de 30/09.

Só isto, e só com a flag ainda desligada até o ledger e o pin vivo serem conferidos.

1. Ler `suite.schema_migrations` no banco alvo. A SQL local com nome 027 não é aplicada. Se `027_suite_download_notifications` já estiver no ledger, a Station recebe o próximo número livre e um `version` novo. Não editar as migrations 001–026, não repetir o `ALTER` do outbox de avisos e não alterar CHECK de `TURBORAMA_SUITE`.
2. Gravar licença, aparelho, desafio, sessão, item e concessão nas tabelas `suite_station_*`. A concessão guarda o hash do bearer, não o bearer. Um aparelho por licença. Código de ativação de uso único, no mesmo modelo de verifier com pepper que a Suite já usa, com pepper próprio da Station. O pepper da Suite não é reaproveitado no documento nem no código do app.
3. Assinar ativação, sessão, perfil e os desafios dessas ações com a chave privada online que já produz o keyId `2d8987dc740a47cba0a8ee7c3e0fbffaff057e2325c46ea2324ee335de69da50`. Assinar catálogo, concessão e os desafios dessas ações com a chave de conteúdo que já produz `20d22a5e84efe7fb10c235131d92890778137fb10d441fa7706932baa2f8150f`. Cada assinatura sai por um signer Station, com kind e domínio da seção 3. O `switch` do signer da Suite não ganha kind Android.
4. Antes de ligar a flag, conferir que o processo em execução tem esses dois keyIds. Se o binário vivo tiver outro par, parar. O par da Suite não é rotacionado para agradar o app.
5. Acrescentar `GET /v1/station/artifacts/{grantId}` no gateway, ao lado da rota de conteúdo que já existe. Um uso, `307`, `no-store`. A rota `/v1/suite-content/artifacts/{grantId}` permanece.
6. Manter `Suite:Station:Enabled` falso. Ligada cedo demais, a rota responde como ausente (`404`), no mesmo critério já codificado. `503` deixaria o cliente distinguir “existe e está off”.
7. Subir primeiro no clone, com o self-test ainda verde e com as duas chaves reais de verificação. Deploy em Linux fica para outra ordem, depois do hash do `/opt` registrado.

O catálogo Station é uma lista própria. Ele não é a página de 902 itens da Suite e não é o `drawers.json` da Sambox.

---

## 7. Conferência antes de ligar a flag

Histórico do candidato local anterior. A conferência vigente é a seção 0 mais o retorno do commit `93efbba`.

| Conferência | Resultado que autoriza seguir | Resultado que devolve o trabalho |
|---|---|---|
| Ledger `schema_migrations` | Número livre escolhido e SQL aplicado nesse número | Número `027` ocupado e alguém editou migration antiga |
| KeyId online e de conteúdo do processo vivo | Iguais aos dois ids da seção 3 | Ids diferentes: para e o pin volta para o app |
| SHA-256 da SPKI da folha TLS de `app.lzgames.com.br` | Igual a `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` | Pin diferente: o app atualiza o pin. O certificado permanece |
| `POST /v1/suite/challenges` com produto Station | Continua `403 PRODUCT_DENIED` | Qualquer `200` nessa rota |
| `POST /v1/station/sessions` de sessão válida | `heartbeatAfterSeconds` 60 | Valor 5, que é o da Suite |
| Payload de catálogo e de concessão | Sem `http` | URL de origem dentro da assertion |

---

## 8. Retorno para o app

Histórico. Os ajustes desta seção já foram absorvidos pelo fio da seção 0.

O servidor não muda para cobrir estes pontos. Eles voltam para uma rodada do app.

1. O APK de 1,58 GB não contém essas classes. `ENABLED` continua falso até URL, pins e servidor Station estarem conferidos. Reempacotar e instalar é outra ordem. O candidato de 30/09, que pedia `GET /v1/station/catalog`, não é instalado.
2. `openFrontend()` ainda exige `AuthSession.isAuthorized()`. Uma ativação Station bem-sucedida não abre `ESActivity`, porque o marcador AES da senha local não foi gravado. O app passa a abrir a frente a partir do callback Station. O marcador AES não vira licença.
3. O app chama quatro rotas e para. Faltam no app: `session.heartbeat` a cada 60 s, `POST /v1/station/me`, `POST /v1/station/catalog`, `POST /v1/station/downloads/authorize` e o GET único do `contentPath`. A sessão de 180 s permanece. O servidor não estica a sessão porque o app esqueceu o heartbeat.
4. O nome na tela sai de `displayName` em `/v1/station/me`. Sem perfil, o app mostra `Bem-vindo`. O servidor não devolve o nome dentro da assertion de sessão para compensar.
5. Licença deste aparelho é `STA-`. Um id `TS-` da Suite é contrato inválido na rota Station e `PRODUCT_DENIED` na rota Suite. A senha local antiga não prova compra. Quem já usa o TESTE precisa de licença nova, emitida no servidor.
6. O hash de contexto tem de bater com o JSON canônico do servidor (`Utf8JsonWriter`, `JavaScriptEncoder.Default`, ordem fixa). Se um vetor real divergir, o ajuste é no Java. O servidor não aceita um segundo formato.
7. O app não segue redirect sozinho. No GET do artefato ele faz uma única leitura do `307`, descarta o bearer antes desse GET e não guarda a `Location`.
8. A linha Sambox permanece no app como está. Ela não autentica o catálogo Station e o servidor Station não emite o HMAC dela.

---

## 9. Ordem

Histórico do candidato local anterior. A ordem vigente está na seção 0: o app já fala o contrato `93efbba`, com a flag desligada.

1. Ler o ledger e o keyId do processo vivo. Sem isso, a flag permanece falsa.
2. Store Postgres e resgate do artefato no namespace Station, com as duas chaves que já existem.
3. Self-test verde com essas chaves públicas.
4. Só então uma rodada de app: heartbeat, perfil, catálogo, download, abertura da `ESActivity` e, por último, `ENABLED=true` num APK novo.

Esta rodada não aplica SQL, não copia binário para Linux, não liga flag e não gera APK.
