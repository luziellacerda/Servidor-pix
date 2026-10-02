# Handoff — rotas XML da biblioteca Station

Data: 02/10/2026. Base: `7219794` na branch `docs/indice-biblioteca-station-20261002`. O aplicativo não muda nesta rodada. A lista completa está em `docs/station-android/cruzamento-nomes-xml.tsv`. Esta rodada não troca rota e não renomeia pasta. O cruzamento futuro usa essa lista contra os arquivos que estiverem no M.2.

## 1. O que continua valendo

Não refaça o código de catálogo, capa e concessão. Ele já está na `5192`, binário `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`, fonte `bbd07fd`.

Se `RETORNO-INDICE-BIBLIOTECA-STATION-20261002.md` ainda estiver vazio, faça primeiro o handoff `HANDOFF-INDICE-BIBLIOTECA-STATION-20261002.md`: crie o índice no caminho já configurado, a partir do M.2 de cerca de 500 GB, e preencha aquele retorno. Não é a raiz e não é `/mnt/DADOS`. Não monte outro disco. Não mova jogo para `/mnt/DADOS`.

Se o índice válido já estiver carregado, não escreva outro por cima e não reinicie a `5192`.

## 2. Rotas já medidas no Windows

A lista completa tem 36 XML e 12346 jogos. Duas rotas existem no mapa antigo e não têm XML local. A rota BR fica separada da rota principal. O arquivo `cruzamento-nomes-xml.tsv` traz uma linha por jogo, com estas colunas:

- `rota` — pasta da plataforma. É a rota que pode ser trocada numa rodada futura.
- `plataforma` — nome do sistema na lista.
- `nome` — título que o XML mostra.
- `arquivo` — nome do arquivo do jogo, sem pasta. É a chave principal para cruzar com o arquivo posto no servidor.
- `caminho` — caminho relativo dentro da rota. Na maior parte é igual a `arquivo`. Em 7 jogos há uma subpasta; nesses, cruze pelo `caminho`.
- `xml` — arquivo de origem da lista.
- `id` — identificador que o XML trouxe, quando existe. Há 5770 jogos com id diferente de zero.
- `md5` — hash que o XML trouxe, quando existe. Há 3146 jogos com md5. Use só se `rota` + `arquivo` + `nome` não bastarem.

Em 11124 jogos o `nome` é diferente do `arquivo`. Os dois ficam na linha. Há 110 nomes de arquivo repetidos dentro da mesma rota; nesses, a linha se distingue pelo `nome` e, quando existir, por `id` ou `md5`. A tabela não tem URL. Não busque outra lista para completar o cruzamento.

| Rota | Jogos no XML local |
|---|---|
| 3ds | 260 |
| psx | 448 |
| switch | 61 |
| arcade | 721 |
| atari2600 | 0 |
| atari7800 | 0 |
| atomiswave | 27 |
| colecovision | 30 |
| cps1 | 22 |
| cps2 | 22 |
| cps3 | 6 |
| dreamcast | 686 |
| fds | 238 |
| gameandwatch | 56 |
| gamegear | 313 |
| gb | 325 |
| gba | 1169 |
| gbc | 654 |
| jaguar | 58 |
| mame | 1730 |
| mastersystem | 360 |
| megadrive | 870 |
| megadrivebr | 83 |
| model2 | 54 |
| n64 | 213 |
| n64br | 22 |
| nds | 1693 |
| neogeo | 140 |
| neogeocd | 22 |
| nes | 796 |
| o2em | 133 |
| pcengine | 62 |
| pcenginecd | 2 |
| sega32x | 36 |
| snes | 785 |
| snesbr | 231 |
| sufami | 13 |
| supergrafx | 5 |

Sete jogos têm uma subpasta no caminho relativo do XML. A rota deles continua sendo a rota da plataforma. Não crie rota nova por causa dessa subpasta.

## 3. O que fazer

Não baixe lista de outro host. Não grave URL. Não altere XML. Não renomeie pasta do M.2 nesta rodada. A lista que vale para o cruzamento é só `cruzamento-nomes-xml.tsv`. O cruzamento em si fica para depois: cada arquivo posto no M.2 entra pela `rota` e pelo `arquivo`; se o `caminho` tiver subpasta, entra pelo `caminho`. Não aplique esse cruzamento agora e não troque o `name` do índice pelo `nome` desta tabela. `platform` no índice continua saindo da pasta já existente, cortada em 120, como no handoff anterior.

Depois do índice carregado, conte os itens por `platform`. No retorno, escreva só as rotas da tabela acima e a quantidade de cada uma. Se algum item usar `platform` fora dessa lista, escreva só a quantidade desses itens. Não escreva o nome dessa pasta.

Sem índice, não invente contagem. Pare com `indice-ausente`.

Não abra sessão. Não ative. Não emita senha. Não envie WhatsApp. Não edite o painel Cloudflare. Sem sessão, o catálogo continua 401.

## 4. Subida

Reinicie só `turborama-station-api` se esta rodada criar o índice e o arquivo passar na validação. Se o índice já estava carregado, não reinicie. Não reinicie `5190`, `5191` nem PIX.

Se a `5192` não voltar saudável depois de um índice novo, tire o índice do caminho configurado e suba a `5192` de novo. Catálogo em 503 é melhor do que a API parada.

## 5. Retorno

Preencha `RETORNO-ROTAS-XML-STATION-20261002.md` e faça push. Se o índice foi criado nesta rodada, preencha também `RETORNO-INDICE-BIBLIOTECA-STATION-20261002.md`.

- parou porque: `rotas-contadas` ou `indice-ausente` ou o erro real
- índice já existia: sim ou nao
- quantidade de itens, sem nomes:
- quantidade de rotas da tabela com pelo menos um item:
- quantidade de itens com platform fora da tabela, sem nome da pasta:
- uma linha por rota da tabela, no formato `rota=<token> itens=<numero>`
- `5192` saudável: sim ou nao
- hash do binário da `5192`:
- `5192` reiniciada nesta rodada: sim ou nao
- `5190`, `5191` e PIX reiniciados: nao
- índice foi para o Git: nao
- lista completa usada no cruzamento: `cruzamento-nomes-xml.tsv`
- retorno repetiu nome de jogo: nao
- caminho de disco no retorno: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
