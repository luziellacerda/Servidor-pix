# Handoff — rotas XML da biblioteca Station

Data: 02/10/2026. Base: `7219794` na branch `docs/indice-biblioteca-station-20261002`. O aplicativo não muda nesta rodada. As rotas abaixo já foram lidas nos XML locais. Esta rodada não troca rota, não renomeia pasta e não publica nome de jogo.

## 1. O que continua valendo

Não refaça o código de catálogo, capa e concessão. Ele já está na `5192`, binário `386deb36a62beb5c111688e3bff1f46b77f196dc82702b9905d8bf0d393ef7d6`, fonte `bbd07fd`.

Se `RETORNO-INDICE-BIBLIOTECA-STATION-20261002.md` ainda estiver vazio, faça primeiro o handoff `HANDOFF-INDICE-BIBLIOTECA-STATION-20261002.md`: crie o índice no caminho já configurado, a partir do M.2 de cerca de 500 GB, e preencha aquele retorno. Não é a raiz e não é `/mnt/DADOS`. Não monte outro disco. Não mova jogo para `/mnt/DADOS`.

Se o índice válido já estiver carregado, não escreva outro por cima e não reinicie a `5192`.

## 2. Rotas já medidas no Windows

A lista local tem 36 XML e 12346 jogos. Duas rotas existem no mapa antigo e não têm XML local. O nome do jogo fica fora deste documento. A rota BR fica separada da rota principal.

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

Não baixe lista antiga. Não grave URL. Não altere XML. Não renomeie pasta do M.2 para estas rotas. O cruzamento de nomes fica com o responsável, que ainda vai enviar os arquivos. `platform` no índice continua saindo da pasta já existente, cortada em 120, como no handoff anterior.

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
- nome de jogo no Git ou no retorno: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
