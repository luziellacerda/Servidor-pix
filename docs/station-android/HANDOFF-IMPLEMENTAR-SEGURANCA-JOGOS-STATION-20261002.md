# Handoff — implementar a segurança dos jogos Station

Data: 02/10/2026. Base: `docs/seguranca-jogos-station-20261002` commit `1f9506a`. O retorno já mediu o servidor. Esta rodada executa o plano dele. Não muda o aplicativo Android ainda.

## 1. O que o retorno provou

- O M.2 de cerca de 500 GB existe, está montado, e não é a raiz nem `/mnt/DADOS`. Nenhum HTTP público expõe esse disco.
- A `5192` escuta em `127.0.0.1:5192`. O Nginx de `app.lzgames.com.br` manda só `^~ /v1/station/` para ela. O token interno da Suite não está nessa porta.
- Sem sessão, catálogo e authorize respondem 401 `STATION_SESSION_INVALID`. Artifacts responde 404. Com sessão, catálogo e authorize continuam 503. Isso está certo até a resposta segura existir.
- O gateway de conteúdo da Suite decifra o endereço só dentro do processo. A Station pode ter chave AES própria, sem pepper Suite, sem chave de conteúdo Suite e sem sessão Suite.
- O painel Cloudflare não foi editado. A borda já está no caminho (`server: cloudflare`, `cf-cache-status: DYNAMIC`, `cache-control: no-store`).

Não repita a medição. Não imprima ponto de montagem, nome de dispositivo, lista de jogos, pepper, DSN, token, chave nem URL.

## 2. O que implementar

Trabalhe na árvore que corresponde ao binário da `5192`. Não use o checkout Windows solto, nem arquivos locais não commitados de outra branch.

### Catálogo e capas no login

`GET /v1/station/catalog`, com o bearer da sessão Station, passa a responder o envelope assinado de sempre. O domínio interno é `TurboRamaStationAndroid/catalog/v1`. Cada item tem identificador, nome, plataforma, revisão e identificador de capa. Sem caminho, sem URL, sem host Sambox.

A revisão sobe quando a lista ou uma capa muda. O aplicativo, numa rodada seguinte, baixa só o que mudou. Jogo novo aparece no próximo login, sem atualizar o APK.

`GET /v1/station/covers/{coverId}`, com o mesmo bearer, devolve os bytes da imagem. `cache-control: no-store`. O `coverId` não é caminho. Capa não autoriza o arquivo do jogo.

Se não houver índice da biblioteca Station, os dois continuam 503 (`STATION_CATALOG_NOT_READY` no catálogo). Não invente item. Não faça listing do M.2 no retorno. Pode informar só a quantidade.

### Concessão de um uso

`POST /v1/station/downloads/authorize` continua recebendo `StationDownloadRequest`. O cliente manda `ItemId`. A `5192` resolve o arquivo no índice. O cliente não escolhe arquivo.

A concessão usa chave AES só da Station, arquivo novo em `/etc/turborama-suite/`, modo 600. Essa chave é diferente do pepper de ativação Station, do pepper Suite e da chave de conteúdo Suite. Se for igual a alguma dessas, a `5192` não sobe. O conteúdo da chave não entra no Git nem no retorno. Informe só o nome do arquivo.

A resposta assinada usa o domínio `TurboRamaStationAndroid/download-grant/v1` e leva `grantId` e `expiresInSeconds`. Sem URL. Prazo curto. Um uso. Presa à licença, ao aparelho e ao item.

`GET /v1/station/artifacts/{grantId}` consome esse uso e entrega os bytes. Sem cabeçalho `Location`. Sem 307 e sem 302. O segundo pedido, outro aparelho ou prazo vencido responde 404. O endereço do arquivo só existe na memória do processo, no momento da entrega.

Se o índice ou a chave ainda não existir, authorize continua 503 `STATION_DOWNLOAD_NOT_READY` e artifacts continua 404. Não publique um redirect no lugar.

### O que não muda

- `5190`, admin/gateway `5191`, PIX, site, SPA `/admin`, WhatsApp, migration `028`, senha e a licença de teste.
- `/mnt/DADOS` continua área de compilar e testar.
- Flags de conteúdo, EmulationStation, inventário e extração na `5192` continuam falsas.
- Sem JS Challenge, CAPTCHA, Turnstile ou Cloudflare Access em `/v1/station/*`.
- Não edite o painel Cloudflare nesta rodada. A origem já manda `no-store`. Anote no retorno que o painel segue para o operador: proxy laranja, bypass de cache em `/v1/station/*`, rate limit por IP, WAF sem desafio HTML, Authenticated Origin Pulls, TLS full strict. Não bloqueie a implementação por causa do painel.

## 3. Como subir

Migration nova só se for aditiva e só para a concessão Station. Não altere a `028`. Não apague tabela.

Rode o teste Station que já existe. Se a rota nova não passar, não reinicie.

Reinicie só `turborama-station` da `5192`, e só se o binário mudou e o teste passou. Não reinicie `5190`, `5191` nem PIX.

Não abra sessão a partir do servidor. Não ative. Não emita senha. Não envie WhatsApp. A prova pública sem sessão continua 401 no catálogo e no authorize. Artifacts sem concessão continua 404.

## 4. Retorno

Preencha `RETORNO-IMPLEMENTAR-SEGURANCA-JOGOS-STATION-20261002.md` e faça push nesta branch.

- catálogo com sessão devolveria `catalog/v1` sem URL: sim ou nao. Se nao, o motivo é `indice-ausente` ou o erro real.
- quantidade de itens no índice, sem nomes:
- capas por `coverId` com bearer, sem URL: sim ou nao
- authorize gera `grantId` sem URL: sim ou nao
- artifacts entrega bytes, sem `Location`: sim ou nao
- segundo uso da mesma concessão: 404 sim ou nao
- arquivo da chave AES Station, só o nome:
- chave diferente dos segredos Suite e do pepper de ativação: sim ou nao
- `5192` reiniciada: sim ou nao, com o hash do binário
- `5190`, `5191` e PIX reiniciados: nao
- painel Cloudflare editado: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
