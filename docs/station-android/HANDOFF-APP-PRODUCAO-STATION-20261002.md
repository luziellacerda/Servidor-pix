# Handoff — aplicativo Turborama Station em produção

Data: 02/10/2026. Destino: **APK TESTE na máquina Windows**. Esta rodada **não mexe no servidor**. Catálogo, capas e download já estão abertos. Fecha o aplicativo numa implementação só.

Substitui o teto de capa de 2 MiB do `HANDOFF-APP-CATALOGO-STATION-20261002`: em produção a arte **revista** passa de 2 MiB. Use **5 MiB**.

Base do servidor: `feat/station-library-grant-20261002` commit `b1159c9`. Binário da `5192`: `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`. `/ready/station` 200. Prova pública sem bearer: catálogo/capa 401, artifacts 404 sem `Location` (`sem-link-direto`).

Telefone: Samsung SM-A566E serial `RQCY30751WY`. Licença de teste `STA-D7AE45616B415B2C7550315C0392C5D8` (BOUND, um aparelho, nome `Teste Station`). Não emita senha. Não ative desta máquina. Não cole pepper, DSN, token, chave, device id, ponto de montagem nem URL de jogo.

Pin TLS, key id e SPKI continuam os de `StationConfig.java`. Não copie esses valores para outro arquivo.

## 1. O que o servidor já entrega

- Base `https://app.lzgames.com.br/v1/station/`. Produto `TURBORAMA_STATION_ANDROID`. Domains `TurboRamaStationAndroid/`.
- Sessão 180 s, só memória. Desafio 60 s. Sem heartbeat. Um aparelho por licença. Keystore no telefone; copiar o APK não copia a chave.
- `GET /catalog` com Bearer → envelope `catalog/v1`: `revision`, `items[]` com `itemId`, `name`, `platform`, `revision`, `coverId`. Sem pasta, sem URL. Hoje revisão 1, **1816** itens (`snes` 644, `snesbr` 191, `megadrive` 887, `megadrivebr` 94).
- `GET /covers/{coverId}` com Bearer → bytes de imagem, até **5 MiB**, `cache-control: no-store`. Tipos: `image/png`, `image/jpeg`, `image/webp`, `image/gif`. A capa não libera o jogo.
- `POST /downloads/authorize` com Bearer e `itemId` no JSON de identidade, domínio `request-download`. Resposta `download-grant/v1`: `grantId`, `expiresInSeconds=60`. Sem URL.
- `GET /artifacts/{grantId}` com o mesmo Bearer → bytes `application/octet-stream`, um uso. Sem `Location`. 302/307 é falha. Segundo uso: 404 `STATION_GRANT_NOT_FOUND`.
- Sem sessão: catálogo e capa 401. Artifacts inexistente 404 sem redirect.
- `itemId` / `coverId`: 8–64 `[A-Za-z0-9_-]`, sem `..`. Hoje 64 hex.

O leitor atual de `StationClient` recusa corpo acima de 8192 bytes e trata 503 como sucesso só em `/me`. **Não use esse leitor** para lista, capa ou jogo.

## 2. O que o app faz nesta implementação

Depois que a sessão abre, o app pede a lista e as capas que faltam. 503, rede ou assinatura inválida: biblioteca abre com a última lista salva. O login **não** espera o download do jogo.

O arquivo do jogo só é pedido quando a pessoa inicia um jogo cujo arquivo local ainda não existe. A resposta são os bytes, uma vez. O app grava o arquivo no celular e **não grava endereço**.

## 3. Contrato no cliente

`HttpURLConnection.setInstanceFollowRedirects(false)` em todas. Bearer só no cabeçalho. Não grave bearer em disco. Não escreva token, nome da pessoa nem caminho de servidor no log. Pode registrar status HTTP e quantidade de itens.

Pedido com o mesmo JSON de identidade da sessão. Domínios novos (não mude os de ativação/sessão):

- lista: `TurboRamaStationAndroid/catalog/v1`
- pedido de jogo: `TurboRamaStationAndroid/request-download/v1`
- permissão: `TurboRamaStationAndroid/download-grant/v1`

JSON de authorize (camelCase): `schemaVersion`, `domain` (`…/request-download/v1`), `productId`, `applicationId`, `deviceId`, `clientVersion`, `deviceManufacturer`, `deviceModel`, `androidSdk`, `itemId`.

Limites:

- Lista: até 2 MiB de envelope, timeout de leitura 60 s. 503 devolve vazio e **não apaga** a lista salva. Só substitui a lista com assinatura válida e domínio `catalog/v1`.
- Capa: grave direto em arquivo, limite **5 MiB**, timeout 20 s. Tipo fora do contrato ou tamanho demais: apague o parcial.
- Jogo: arquivo temporário e só troque o nome no fim. Exija `Content-Length` e confira os bytes. Timeout longo (até 900 s no nginx). Se vier `Location`, 3xx ou bearer ausente, apague o parcial. Grant só na memória; comece em 60 s.

Não baixe as 1816 capas no login. Só as que faltam, chave `coverId` + revisão do item.

## 4. Arquivos Java

Fonte: `E:\ESTUDO APK\work\native-carousel\implementation\station-security-20261001\java\org\emulationstation\frontend\auth\`.

- `StationProtocol.java`: acrescente os três domínios. Não mude ativação e sessão.
- `StationProtocolPath.java`: acrescente catálogo, capa, authorize e artefato.
- `StationClient.java`: métodos da lista, da capa, da permissão e do arquivo, com os limites da seção 3.
- `StationAuth.java`: no `open` e no `pullName`, depois do perfil, atualize a lista. Falha nisso não muda o `callback`. `open` continua `ok`. `pullName` continua o nome salvo se a sessão falhar.
- Classe nova `StationLibrary`: cache e download sob demanda.

Não edite o campo de senha (`LengthFilter` 128, hint `Digite sua senha`, `inputType` 0x81). Não mude `LoginActivity` além do que já chama `StationAuth`. Não copie `LoginActivity.smali` de outro dex. Nome na biblioteca continua de `no_backup/station-display-name.txt` pelo hook nativo. Não altere `libturbo_carousel.so`.

Pacote `org.turboramastation.frontend`. Classes Java em `org.emulationstation.frontend`.

Título visível: `Bem-vindo, {displayName}`. Aposente `BEM-VINDO DE VOLTA`. 503 em `/me` não zera o último nome. Não peça senha se `station-license-id.txt` existe e a sessão abrir.

## 5. Cache no celular

Tudo em `context.getNoBackupFilesDir()`:

- `station-catalog.json`: só os itens verificados. Sem envelope, sem URL.
- `station-covers/<coverId>.<ext>`: bytes da capa.
- `station-games/<itemId>.part` e, no fim, `station-games/<itemId>`: bytes do jogo.
- `station-items.tsv`: `itemId`, `platform`, `name`, caminho local relativo. Sem URL.

Não apague ROM, save, gamelist ou capa em `/storage/emulated/0/EmulationStation`. Não dê `pm clear` e não desinstale.

A lista visível continua a gamelist local. Para cada item do catálogo:

- Ache o jogo local pelo `name` ou pelo nome do arquivo em `<path>`.
- Se achar, e a imagem local for caminho relativo dentro de `EmulationStation`, substitua só os bytes dessa imagem pela capa nova. Não troque o `<path>` por endereço.
- Se não achar, acrescente um `<game>` com `<name>`, imagem local e `<path>` `./station/<itemId>`. O arquivo real fica em `station-games/<itemId>`. O `<path>` não é URL.
- Não apague jogo local que o catálogo não citou.

Na hora de jogar, o ponto que já abre o arquivo do `<path>` consulta `station-items.tsv`. Se o arquivo local não existir e houver `itemId`, baixa e só então abre. Não crie outra tela. Não chame authorize nem artefato no login, no `openCommercial` nem no `pullName`.

## 6. Empacotar

O script `package_station_login.py` hoje recusa em `classes8.dex` as strings `/v1/station/catalog`, `/v1/station/downloads` e `/v1/station/artifacts`. Tire essa recusa. No lugar, recuse `miami`, `sambox`, `?e=` e `?s=`. A base `https://app.lzgames.com.br` continua obrigatória. Continuam obrigatórias: `stationLogin`, `openCommercial`, `TurboRamaStationAndroid/profile/v1`, `station-license-id.txt`.

Compile o Java para o dex da mesma forma da leva do nome. `apktool` grava `classes8`. O script enxerta só esse dex na cópia do APK grande. `zipalign -f -P 16 4`. Assine com o keystore de debug já usado, alinhamento preservado. Não use APK debuggable. Não troque `classes24`. O SHA-256 do Cemu estável `7ce3fab3d2d09c3bddfd002d0b9734e42aa5e27b102f36bb56e77b484e36562b` tem de continuar igual. Os três PUP de PS Vita da base são copiados, não acrescentados.

Instale com `adb install -r --no-incremental` no Samsung `RQCY30751WY`. Não apague os dados. Campo vazio, com `station-license-id.txt` presente, entra na sessão Station. Outro texto continua na senha local. Não digite de novo o código de ativação já usado. Não emita outro código.

## 7. Pronto quando

- Login vazio abre a biblioteca.
- Boas-vindas `Bem-vindo, Teste Station`.
- Hint da senha `Digite sua senha`.
- Com sessão, o log mostra lista 200 e quantidade (sem nomes). 503/rede abre a lista salva.
- Capas novas aparecem nos jogos que baterem pelo nome, sem URL na gamelist.
- Jogo Station ausente no disco só baixa ao iniciar, por grant de um uso, sem `Location`.
- `classes8` contém as quatro rotas e não contém `miami`, `sambox`, `?e=` nem `?s=`.
- Nenhuma gamelist com `http`. Nenhum cache com URL.
- Diff do APK, fora de `META-INF`, é só `classes8.dex`.
- Authorize e artefato não aparecem no log do login.

## 8. Fora desta leva

Não reinicie `5192`, `5190`, `5191` nem PIX. Não baixe lista de outro host. Não misture HMAC Sambox com Station. Não altere motor de emulador, vídeo, Cemu, chave Wii U nem o app 1.0. Não ponha pepper, DSN, chave privada, token, device id nem ponto de montagem no APK nem no retorno.

## 9. Retorno

Preencha `RETORNO-APP-PRODUCAO-STATION-20261002.md` no repositório do app (ou nesta branch) e faça push. Sem segredo, sem senha, sem device id, sem nome de jogo, sem URL.
