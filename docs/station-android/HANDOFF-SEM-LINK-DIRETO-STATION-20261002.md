# Handoff — jogo Station sem link direto

Data: 02/10/2026. Base: retorno `de83824` na branch `docs/rotas-xml-station-20261002`. O aplicativo não muda nesta rodada. Quem copiar o app não pode levar um endereço reutilizável do jogo.

## 1. O que o retorno deixou

Não refaça a busca de pares e não crie o índice.

- O disco de jogos está montado. Não é a raiz e não é `/mnt/DADOS`. Não havia par jogo+capa reconhecível. O arquivo do índice não foi criado. Quantidade de itens: 0. As 38 rotas da tabela ficaram em 0. A contagem não foi inventada.
- A `5192` segue saudável, PID 223970, binário `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`. Não foi reiniciada nesta rodada. `/ready/station` estava 200.
- `5190` PID 2943, gateway `5191` PID 2948 e PIX PID 2940 ficaram nos processos antigos.
- Sem sessão, `GET /v1/station/catalog` responde 401 `STATION_SESSION_INVALID`.
- Senha, ativação, WhatsApp e painel Cloudflare não foram mexidos.

A lista completa continua em `docs/station-android/cruzamento-nomes-xml.tsv`. O cruzamento com os arquivos do disco fica para quando esses arquivos existirem. Não rode esse cruzamento agora.

## 2. Proteção que já está no código

Fonte `bbd07fd`, já na `5192`. Não reescreva `StationService` nem `StationEndpoints` nesta rodada.

- `GET /v1/station/catalog` exige sessão. A resposta assinada `TurboRamaStationAndroid/catalog/v1` leva `itemId`, `name`, `platform`, `revision` e `coverId`. O `filePath` do índice não entra nessa resposta.
- `GET /v1/station/covers/{coverId}` exige a mesma sessão e devolve os bytes da imagem, com `cache-control: no-store`. A capa não libera o arquivo do jogo.
- `POST /v1/station/downloads/authorize` recebe `ItemId`. A resposta assinada `TurboRamaStationAndroid/download-grant/v1` leva `grantId` e `expiresInSeconds=60`. Não leva URL. O caminho fica cifrado, amarrado à licença, ao aparelho, ao item e ao `grantId`.
- `GET /v1/station/artifacts/{grantId}` exige o mesmo bearer, a mesma licença e o mesmo aparelho. Consome a permissão uma vez e copia os bytes para a resposta, com `cache-control: no-store` e `content-type: application/octet-stream`. Não há cabeçalho `Location` e não há redirect 302 ou 307. Segunda vez, outro aparelho, sessão ausente ou prazo vencido: 404 `STATION_GRANT_NOT_FOUND`.
- A sessão dura 180 segundos. A chave do aparelho fica no Android Keystore e não está no APK. Uma licença tem um aparelho ativo. Copiar o APK não copia essa chave.

O disco de jogos não fica em HTTP público. Quando um índice futuro for criado, o `filePath` continua só dentro da concessão cifrada. Catálogo, capa e artefato não passam a devolver URL.

## 3. O que fazer

Não altere código, Nginx, Cloudflare, systemd, banco nem o arquivo de índice. Não monte disco. Não renomeie pasta. Não abra sessão. Não ative. Não emita senha. Não crie concessão. Não envie WhatsApp.

Confira só as rotas públicas, sem bearer:

- `GET /v1/station/catalog` responde 401 e o corpo não contém URL.
- `GET /v1/station/covers/` com um id inexistente responde 401 e não contém URL.
- `POST /v1/station/downloads/authorize` sem bearer responde 401, ou 400 se o JSON for inválido. O corpo não contém URL nem `grantId` utilizável.
- `GET /v1/station/artifacts/` com um id desconhecido responde 404, sem cabeçalho `Location` e sem status 302 ou 307.
- `HEAD` nessa rota de artefato responde 405.

Se alguma resposta trouxer URL, `Location` ou redirect, pare. Não corrija o código nesta rodada. Escreva `achou-link` e o status HTTP, sem copiar o corpo.

Se todas permanecerem como acima, escreva `sem-link-direto`.

## 4. Subida

Não reinicie `5192`, `5190`, `5191` nem PIX. O binário conferido continua `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`.

## 5. Retorno

Preencha `RETORNO-SEM-LINK-DIRETO-STATION-20261002.md` e faça push.

- parou porque: `sem-link-direto` ou `achou-link` ou o erro real
- catálogo sem sessão: status HTTP
- capa sem sessão: status HTTP
- authorize sem sessão: status HTTP
- artefato desconhecido: status HTTP
- artefato devolveu `Location` ou redirect: sim ou nao
- corpo com URL: sim ou nao
- índice criado nesta rodada: nao
- `5192` saudável: sim ou nao
- hash do binário da `5192`:
- `5192` reiniciada: nao
- `5190`, `5191` e PIX reiniciados: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
- caminho de disco no retorno: nao
