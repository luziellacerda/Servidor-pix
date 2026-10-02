# Handoff — app Station puxa lista, capa e jogo sem link

Data: 02/10/2026. Este documento fecha o aplicativo. Não há outra rodada de servidor. A implementação é uma só, na máquina Windows, no APK TESTE já instalado.

O servidor não executa este handoff. A `5192` já tem o código `bbd07fd`, binário `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`, PID 223970. O índice não existe: o disco não tinha par jogo+capa. Catálogo com sessão responde 503 `STATION_CATALOG_NOT_READY`. Sem sessão responde 401. Isso não bloqueia o app.

## 1. O que o app faz nesta implementação

Depois que a sessão Station abre, o app pede a lista e as capas. Se o servidor responder 503, erro de rede ou assinatura inválida, a biblioteca abre com a última lista salva. O login não espera o download do jogo.

O arquivo do jogo só é pedido quando a pessoa inicia um jogo cujo arquivo local ainda não existe. A resposta são os bytes, uma vez. O app grava o arquivo no celular e não grava endereço.

Quem copiar o APK não leva a chave do Android Keystore nem a licença. A sessão dura 180 segundos e fica só na memória. A permissão do jogo dura 60 segundos, um uso, a mesma licença e o mesmo aparelho.

## 2. Contrato que o app consome

Base `https://app.lzgames.com.br`. Pin TLS, key id e SPKI continuam os que já estão em `StationConfig.java`. Não copie esses valores para outro arquivo.

Pedido com o mesmo JSON de identidade já usado na sessão. Domínios novos:

- lista: `TurboRamaStationAndroid/catalog/v1`
- pedido de jogo: `TurboRamaStationAndroid/request-download/v1`
- permissão: `TurboRamaStationAndroid/download-grant/v1`

Rotas:

- `GET /v1/station/catalog` com `Authorization: Bearer`. Resposta no envelope já usado (`keyId`, `payload`, `signature`). O payload traz `itemId`, `name`, `platform`, `revision`, `coverId`. Não traz pasta nem URL.
- `GET /v1/station/covers/{coverId}` com o mesmo bearer. Corpo é a imagem, no máximo 2 MiB. `cache-control: no-store`. Tipos aceitos: `image/png`, `image/jpeg`, `image/webp`, `image/gif`.
- `POST /v1/station/downloads/authorize` com o bearer e o campo `itemId` no JSON de identidade. Domínio do pedido: `request-download`. A resposta assinada traz `grantId` e `expiresInSeconds` igual a 60. Não traz URL.
- `GET /v1/station/artifacts/{grantId}` com o mesmo bearer. Corpo é o arquivo, `application/octet-stream`. Sem `Location`. Status 302 ou 307 é falha: apague o parcial e não siga o redirect.

`itemId` e `coverId` têm 8 a 64 caracteres, só letra ASCII, dígito, hífen e sublinhado, sem `..`. Fora disso, ignore o item.

`HttpURLConnection.setInstanceFollowRedirects(false)` em todas. Bearer só no cabeçalho. Não grave o bearer em disco. Não escreva o token, o nome da pessoa nem o caminho do servidor no log. Pode registrar status HTTP e quantidade de itens.

O leitor atual de `StationClient` recusa corpo acima de 8192 bytes e trata 503 como sucesso só em `/v1/station/me`. Não use esse leitor para lista, capa ou jogo.

- Lista: limite 2 MiB, timeout de leitura 60 segundos. Status 503 devolve vazio e não apaga a lista salva. Assinatura e domínio `catalog/v1` são obrigatórios para substituir a lista.
- Capa: grave direto em arquivo, limite 2 MiB, timeout 20 segundos. Confira o tipo. Tamanho ou tipo fora do contrato apaga o parcial.
- Jogo: grave em arquivo temporário e só troque o nome no fim. Exija `Content-Length` e confira a quantidade de bytes. Timeout de leitura longo. Se vier `Location`, status 3xx ou bearer ausente, apague o parcial.

## 3. Arquivos do app

Fonte Java em `E:\ESTUDO APK\work\native-carousel\implementation\station-security-20261001\java\org\emulationstation\frontend\auth\`.

- `StationProtocol.java`: acrescente os três domínios. Não mude os domínios que já entram na ativação e na sessão.
- `StationProtocolPath.java`: acrescente catálogo, capa, authorize e artefato.
- `StationClient.java`: métodos da lista, da capa, da permissão e do arquivo, com os limites da seção 2.
- `StationAuth.java`: no `open` e no `pullName`, depois do perfil, chame a atualização da lista. Falha nessa atualização não muda o `callback`. `open` continua chamando `ok`. `pullName` continua devolvendo o nome salvo quando a sessão falha.
- Classe nova `StationLibrary`: grava o cache e baixa o jogo sob demanda.

Não edite o campo de senha. Ele permanece com `LengthFilter` 128, hint `Digite sua senha` e `inputType` 0x81. Não mude `LoginActivity` além do que já chama `StationAuth`. Não copie `LoginActivity.smali` de outro dex. O nome na biblioteca continua vindo de `no_backup/station-display-name.txt` pelo hook nativo já instalado. Não altere `libturbo_carousel.so` nesta leva.

O pacote é `org.turboramastation.frontend`. As classes Java continuam em `org.emulationstation.frontend`.

## 4. Cache no celular

Tudo em `context.getNoBackupFilesDir()`:

- `station-catalog.json`: só os itens verificados. Sem envelope, sem URL.
- `station-covers/<coverId>.<ext>`: bytes da capa.
- `station-games/<itemId>.part` e, no fim, `station-games/<itemId>`: bytes do jogo.
- `station-items.tsv`: `itemId`, `platform`, `name`, caminho local relativo. Sem URL.

Não apague ROM, save, gamelist ou capa que já estejam em `/storage/emulated/0/EmulationStation`. Não dê `pm clear` e não desinstale.

A lista visível continua a gamelist local. Para cada item do catálogo:

- Ache o jogo local pelo `name` ou pelo nome do arquivo em `<path>`.
- Se achar, e a imagem local for caminho relativo dentro de `EmulationStation`, substitua só os bytes dessa imagem pela capa nova. Não troque o `<path>` por endereço.
- Se não achar, acrescente um `<game>` com `<name>`, imagem local e `<path>` `./station/<itemId>`. O arquivo real fica em `station-games/<itemId>`. O `<path>` não é URL.
- Não apague jogo local que o catálogo não citou.

Enquanto o índice do servidor tiver 0 itens, essa escrita não muda a biblioteca. O app abre como está hoje.

Na hora de jogar, o ponto que já abre o arquivo do `<path>` consulta `station-items.tsv`. Se o arquivo local não existir e houver `itemId`, baixa e só então abre. Não crie outra tela. Não chame authorize nem artefato no login, no `openCommercial` nem no `pullName`.

## 5. Empacotar

O script `package_station_login.py` hoje recusa estas três strings em `classes8.dex`:

- `/v1/station/catalog`
- `/v1/station/downloads`
- `/v1/station/artifacts`

Tire essa recusa. No lugar, recuse em `classes8.dex` as strings `miami`, `sambox`, `?e=` e `?s=`. A base `https://app.lzgames.com.br` continua obrigatória. As agulhas já exigidas continuam obrigatórias, inclusive `stationLogin`, `openCommercial`, `TurboRamaStationAndroid/profile/v1` e `station-license-id.txt`.

Compile o Java para o dex da mesma forma da leva do nome. O `apktool` grava `classes8`. O script enxerta só esse dex na cópia do APK grande. `zipalign -f -P 16 4`. Assine com o keystore de debug já usado, alinhamento preservado. Não use o APK debuggable. Não troque `classes24`. O SHA-256 do Cemu estável `7ce3fab3d2d09c3bddfd002d0b9734e42aa5e27b102f36bb56e77b484e36562b` tem de continuar igual. Os três PUP de PS Vita que já estão na base são copiados, não acrescentados. O conjunto de PUP depois da assinatura é o mesmo da base.

Instale com `adb install -r --no-incremental` no Samsung SM-A566E, serial `RQCY30751WY`. Não apague os dados. O campo vazio, com `station-license-id.txt` presente, entra na sessão Station. Outro texto continua na senha local. Não digite de novo o código de ativação já usado. Não emita outro código.

## 6. Pronto quando

- O login vazio abre a biblioteca.
- As duas linhas de boas-vindas seguem `Bem-vindo, Teste Station`. O cabeçalho ao lado do avatar segue o nome do servidor.
- O hint da senha segue `Digite sua senha`.
- Com o índice ausente, o log mostra a lista em 503 ou falha de atualização, e a biblioteca abre com o que já estava no celular.
- `classes8` contém as quatro rotas da seção 2 e não contém `miami`, `sambox`, `?e=` nem `?s=`.
- Nenhuma gamelist ficou com `http`. Nenhum arquivo de cache ficou com URL.
- O diff do APK, fora de `META-INF`, é só `classes8.dex`.
- Authorize e artefato não aparecem no log do login. Aparecem só quando um jogo Station local está ausente e a pessoa o inicia.

Quando o índice passar a ter itens, o mesmo APK mostra nome e capa novos no login seguinte, sem outra compilação. O jogo novo só entra no disco do celular na hora de jogar, pelos bytes da permissão.

## 7. Fora desta leva

Não reinicie `5192`, `5190`, `5191` nem PIX. Não crie o índice. Não renomeie pasta no M.2. Não baixe lista de outro host. Não misture HMAC da loja Sambox com a Station. Não altere motor de emulador, vídeo, Cemu, chave Wii U nem o app 1.0. Não faça commit do C# local sujo. Não ponha pepper, DSN, chave privada, token, device id nem ponto de montagem neste documento nem no APK.
