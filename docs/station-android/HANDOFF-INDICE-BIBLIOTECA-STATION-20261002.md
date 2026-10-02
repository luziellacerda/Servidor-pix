# Handoff — índice da biblioteca Station

Data: 02/10/2026. Base: retorno `d681f3a` na branch `docs/implementar-seguranca-jogos-station-20261002`. O código já subiu. Esta rodada só cria o índice que falta. Não muda o aplicativo.

## 1. O que o retorno deixou pronto

Não refaça.

- Binário da `5192`: `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`. Fonte `bbd07fd`. Só a unidade `turborama-station-api` foi reiniciada. `5190`, `5191` e PIX ficaram nos processos antigos.
- Com sessão, `GET /v1/station/catalog` assina `TurboRamaStationAndroid/catalog/v1` com `itemId`, `name`, `platform`, `revision` e `coverId`. Sem caminho e sem URL.
- `GET /v1/station/covers/{coverId}` devolve a imagem com o mesmo bearer. `cache-control: no-store`.
- `POST /v1/station/downloads/authorize` assina `TurboRamaStationAndroid/download-grant/v1` com `grantId` e `expiresInSeconds=60`. Sem URL.
- `GET /v1/station/artifacts/{grantId}` entrega bytes, sem `Location` e sem redirect. O segundo uso responde 404 `STATION_GRANT_NOT_FOUND`.
- Migration `029_station_download_grants` aplicada. A `028` ficou intacta. Chave `station-download.key`, diferente dos segredos Suite e do pepper de ativação.
- O índice não existe. Quantidade de itens: 0. Catálogo cai em 503 `STATION_CATALOG_NOT_READY`. O processo lê o arquivo só na subida (`StationLibrary.TryLoad`).

## 2. O que fazer

O arquivo configurado em `Station:LibraryIndexFile` recebe o JSON da biblioteca. O formato vazio de referência está em `docs/station-android/ops/station-library.example.json`. Cada item precisa de `itemId`, `name`, `platform`, `revision`, `coverId`, `filePath` e `coverPath`.

Regras que o processo já exige:

- No máximo 4096 itens. `itemId` e `coverId` têm de 8 a 64 caracteres, só letras, dígitos, hífen e sublinhado.
- `name` e `platform` têm de 1 a 120 caracteres, sem caractere de controle.
- `filePath` e `coverPath` são absolutos, até 1024 caracteres, sem `..` e sem quebra de linha.
- A capa tem de existir e ter no máximo 2 MiB. Extensões aceitas na entrega: png, jpg, jpeg, webp, gif.
- O JSON não entra no Git. O retorno não leva ponto de montagem, caminho, nome de jogo nem nome de capa.

Origem dos arquivos: o M.2 de cerca de 500 GB que já está montado. Não é a raiz e não é `/mnt/DADOS`. Não monte outro disco. Não mova jogo para `/mnt/DADOS`.

Para cada par jogo+capa já existente nesse M.2, gere `itemId` e `coverId` com hash, sem gravar o caminho dentro do identificador. `name` sai do nome do arquivo, cortado em 120. `platform` sai da pasta, também cortada em 120. `revision` fica 1 nesta primeira carga. A revisão do arquivo fica 1.

Pule item sem capa, capa acima de 2 MiB ou extensão fora da lista. Se passar de 4096 pares, grave os primeiros 4096 em ordem estável e marque truncado. Se não houver par reconhecível, não invente item, não escreva o arquivo e pare com `indice-sem-pares`.

Grave primeiro num arquivo temporário. Confira o JSON com as regras acima. Só então mova para o caminho configurado. Se a conferência falhar, apague o temporário e não reinicie.

## 3. Subida

Reinicie só `turborama-station-api` depois que o arquivo válido estiver no lugar. Não reinicie `5190`, `5191` nem PIX.

Se a `5192` não voltar saudável, tire o índice do caminho configurado e suba a `5192` de novo. Catálogo em 503 é melhor do que a API parada.

Não abra sessão. Não ative. Não emita senha. Não envie WhatsApp. Não edite o painel Cloudflare. Sem sessão, o catálogo continua 401.

## 4. Retorno

Preencha `RETORNO-INDICE-BIBLIOTECA-STATION-20261002.md` e faça push.

- parou porque: `indice-pronto` ou `indice-sem-pares` ou o erro real
- quantidade de itens, sem nomes:
- quantidade pulada por falta de capa ou capa inválida:
- truncado em 4096: sim ou nao
- revisão do índice:
- `5192` saudável depois da subida: sim ou nao
- hash do binário da `5192`: o mesmo `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6` ou o novo
- `5190`, `5191` e PIX reiniciados: nao
- índice foi para o Git: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
