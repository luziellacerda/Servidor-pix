# Retorno — jogo Station sem link direto

Preencha e faça push. Sem pepper, DSN, token, chave, senha, device id, ponto de montagem, caminho, nome de jogo, corpo de resposta ou URL.

## Resultado

- parou porque: sem-link-direto
- catálogo sem sessão: status HTTP 401
- capa sem sessão: status HTTP 401
- authorize sem sessão: status HTTP 401 (JSON de identidade sem bearer); `{}` 400
- artefato desconhecido: status HTTP 404
- artefato devolveu `Location` ou redirect: nao
- corpo com URL: nao
- índice criado nesta rodada: nao
- `5192` saudável: sim
- hash do binário da `5192`: `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`
- `5192` reiniciada: nao
- `5190` reiniciada: nao
- `5191` reiniciada: nao
- PIX reiniciado: nao
- senha emitida: nao
- ativação feita desta máquina: nao
- WhatsApp enviado: nao
- painel Cloudflare editado: nao
- caminho de disco no retorno: nao

## Observação

Provas públicas sem bearer: catálogo e capa 401 `STATION_SESSION_INVALID`; authorize JSON 401; artifacts inexistente 404 `STATION_GRANT_NOT_FOUND` sem `Location` e sem 302/307; HEAD artifacts 405. Nenhum corpo tinha URL, `filePath` ou `coverPath`. PID `5192` 247645, `5190` 2943, gateway 2948, PIX 2940. `/ready/station` 200. Índice já existente desta carga anterior (1816 itens) não foi reescrito nesta rodada.
