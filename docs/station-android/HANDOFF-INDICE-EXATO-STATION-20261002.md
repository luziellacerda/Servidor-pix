# Handoff — índice exato da biblioteca Station

Data: 02/10/2026. Base: retorno `de83824` e código `bbd07fd` já em execução. Esta é a tarefa inteira do servidor. Não peça outro handoff para completar regra. O aplicativo Android não entra nesta rodada.

## 1. O que já está feito

Não reescreva `StationService`, `StationEndpoints`, `StationLibrary` nem `StationGrantCipher`. Não troque o binário.

- `5192`: PID 223970 na última leitura, binário `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`. `/ready/station` estava 200.
- O índice não existe. `StationLibrary.TryLoad` só roda na subida do processo. Arquivo ausente deixa o catálogo em 503 `STATION_CATALOG_NOT_READY`. Arquivo inválido derruba a `5192`.
- `5190` PID 2943, gateway `5191` PID 2948 e PIX PID 2940 ficam como estão.
- A lista completa já está em `docs/station-android/cruzamento-nomes-xml.tsv`. São 12346 linhas de jogo. Não baixe outra lista. Não use host antigo. A tabela não tem URL.

O catálogo público só pode levar `itemId`, `name`, `platform`, `revision` e `coverId`. A permissão de download dura 60 segundos, um uso, a mesma licença e o mesmo aparelho, e a resposta do artefato são os bytes, sem `Location` e sem redirect. Não mude esse contrato.

## 2. Onde estão os arquivos

Use o disco de cerca de 500 GB que já está montado. Não é a raiz e não é `/mnt/DADOS`. É o mesmo disco da rodada `de83824`. Não monte outro disco. Não mova jogo. Não renomeie pasta. Não procure na raiz nem em `/mnt/DADOS`.

Se esse disco não estiver montado, pare com `disco-ausente`. Não crie o índice e não reinicie.

O caminho do índice é o `Station:LibraryIndexFile` já configurado na `5192`. Não escreva esse caminho no Git nem no retorno. Não use `docs/station-android/ops/station-library.example.json` como arquivo real. Aquele exemplo fica vazio.

## 3. Como casar cada jogo

Leia o TSV em UTF-8. A primeira linha é cabeçalho: `rota`, `plataforma`, `nome`, `arquivo`, `caminho`, `xml`, `id`, `md5`.

Percorra só arquivos regulares desse disco. Não siga symlink que saia dele.

Uma linha casa com um arquivo quando o nome do arquivo no disco é igual a `arquivo`, comparação ordinal. Se o `caminho` tiver pasta, o sufixo do caminho no disco, com `/`, tem de ser igual a `caminho`.

Se o mesmo `arquivo` aparecer em mais de um lugar:

- fique com o arquivo cujo caminho tem um diretório com o nome exato da `rota`;
- se ainda sobrar mais de um, não escolha. Conte `ambiguo` e pule a linha.

Há nomes de arquivo repetidos na mesma rota, com títulos diferentes. Se duas linhas tiverem a mesma `rota` e o mesmo `arquivo`, pule as duas e conte `arquivo-repetido`. Não chute o título.

A capa do arquivo casado é o primeiro arquivo que existir, nesta ordem, com tamanho de 1 byte a 2097152 bytes:

1. na mesma pasta: o mesmo nome sem extensão, com `.png`, depois `.jpg`, depois `.jpeg`, depois `.webp`, depois `.gif`;
2. na subpasta `images` da mesma pasta, com esses mesmos nomes;
3. na pasta `images` do diretório pai, com esses mesmos nomes.

A extensão pode variar maiúscula e minúscula. Se o candidato passar de 2097152 bytes, não use outro no lugar. Pule a linha e conte `sem-capa`. Sem nenhuma capa, pule e conte `sem-capa`. Não invente capa e não baixe capa.

`platform` é a coluna `rota`. Só entram estas rotas: `3ds`, `psx`, `switch`, `arcade`, `atari2600`, `atari7800`, `atomiswave`, `colecovision`, `cps1`, `cps2`, `cps3`, `dreamcast`, `fds`, `gameandwatch`, `gamegear`, `gb`, `gba`, `gbc`, `jaguar`, `mame`, `mastersystem`, `megadrive`, `megadrivebr`, `model2`, `n64`, `n64br`, `nds`, `neogeo`, `neogeocd`, `nes`, `o2em`, `pcengine`, `pcenginecd`, `sega32x`, `snes`, `snesbr`, `sufami`, `supergrafx`. Rota fora dessa lista não entra.

`name` é a coluna `nome`, sem caractere de controle. Se passar de 120 caracteres, corte em 120. Se ficar vazio, use o `arquivo` sem extensão, também sem controle e cortado em 120. Se ainda ficar vazio, pule a linha.

`itemId` é os primeiros 32 caracteres hexadecimais minúsculos do SHA-256 dos bytes UTF-8 de `rota`, um `\n`, e `arquivo`. `coverId` é o mesmo cálculo com um `\n` e o texto `cover` no fim. Os dois têm de ser únicos. Se colidir, pule a linha nova e conte `colisao`.

`revision` do arquivo e de cada item é `1`. `filePath` e `coverPath` são os caminhos absolutos dos arquivos casados. Cada um tem de ter de 1 a 1024 caracteres, ser absoluto, e não pode conter `..`, NUL nem quebra de linha. Fora disso, pule a linha.

Ordene por `rota` e depois por `arquivo`, comparação ordinal. O máximo é 4096 itens. Se passar, grave só os primeiros 4096 e marque `truncado=sim`. Não grave 4097: o processo recusa o arquivo.

Se nenhuma linha sobrar, não crie o arquivo. Pare com `indice-sem-pares`. Não reinicie.

## 4. Forma do arquivo

UTF-8 sem BOM. JSON sem comentário e sem vírgula sobrando. Os nomes dos campos são estes:

```json
{
  "revision": 1,
  "items": [
    {
      "itemId": "0123456789abcdef0123456789abcdef",
      "name": "Nome ate 120",
      "platform": "snes",
      "revision": 1,
      "coverId": "fedcba9876543210fedcba9876543210",
      "filePath": "/absoluto/do/jogo",
      "coverPath": "/absoluto/da/capa.png"
    }
  ]
}
```

Os caminhos desse exemplo não são o disco. Use o caminho absoluto do arquivo que casou.

Grave um temporário na mesma pasta do índice, modo `600`. Confira num processo que não é a `5192`, com as mesmas regras de `StationLibrary.TryLoad`: objeto raiz, `items` array, no máximo 4096, `itemId` e `coverId` de 8 a 64 com letra, dígito, hífen ou sublinhado, sem `..`, `name` e `platform` de 1 a 120 sem controle, caminhos absolutos de 1 a 1024 sem `..`, NUL nem quebra de linha, `itemId` único. Se a conferência falhar, apague o temporário e não reinicie.

Se passar, mova o temporário para o caminho configurado. Não copie o índice para o Git.

## 5. Subida

Reinicie só `turborama-station-api`. Não reinicie `5190`, `5191` nem PIX. Não edite Nginx, Cloudflare, systemd, banco nem a chave `station-download.key`.

Espere `/health` e `/ready/station` em `127.0.0.1:5192` responderem 200. O hash do binário tem de continuar `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`. Se a `5192` não voltar saudável, apague o índice do caminho configurado e suba só a `5192` de novo. Catálogo em 503 é melhor do que a API parada.

Sem bearer, `GET /v1/station/catalog` responde 401 e o corpo não tem URL. `GET /v1/station/artifacts/` com id desconhecido responde 404, sem `Location` e sem 302 ou 307. Não abra sessão. Não ative. Não emita senha. Não crie concessão. Não envie WhatsApp.

## 6. Retorno

Preencha `RETORNO-INDICE-EXATO-STATION-20261002.md` e faça push. Sem caminho, sem nome de jogo, sem nome de arquivo, sem ponto de montagem, sem URL, sem pepper, sem DSN, sem token e sem chave.

- parou porque: `indice-pronto` ou `indice-sem-pares` ou `disco-ausente` ou o erro real
- disco da rodada anterior montado: sim ou nao
- arquivos vistos no disco, só a quantidade:
- linhas da tabela: 12346
- itens gravados:
- pulados sem capa:
- pulados ambiguos:
- pulados por arquivo repetido na mesma rota:
- pulados por colisão de id:
- truncado: sim ou nao
- revision: 1
- uma linha por rota, `rota=<token> itens=<numero>`, mesmo quando for 0
- `5192` saudável: sim ou nao
- hash do binário da `5192`:
- `5192` reiniciada: sim ou nao
- índice removido porque a `5192` não subiu: sim ou nao
- `5190`, `5191` e PIX reiniciados: nao
- catálogo sem sessão: 401
- artefato desconhecido sem `Location`: sim ou nao
- índice foi para o Git: nao
- nome de jogo ou caminho no retorno: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
