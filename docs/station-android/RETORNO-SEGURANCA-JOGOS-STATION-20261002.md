# Retorno — segurança dos jogos Station

Preencha e faça push nesta branch. Não cole pepper, DSN, token, chave privada, senha, device id, ponto de montagem, lista de arquivos nem URL de jogo. Não implemente. Não reinicie. Não ative. Não emita senha.

## Medição

- parou porque: concluiu
- M.2 de cerca de 500 GB, diferente da raiz e de `/mnt/DADOS`: sim
- esse disco está montado: sim
- algum HTTP público expõe esse disco: nao
- se expõe, só o nome do server/location, sem alias e sem caminho: nao-aplica
- `5192` em loopback e Nginx só em `/v1/station/`: sim (`127.0.0.1:5192`, `location ^~ /v1/station/` em app.lzgames.com.br)
- token interno da Suite ausente na `5192`: sim
- catálogo e authorize sem sessão: 401 sim (`STATION_SESSION_INVALID`)
- artifacts inexistente: 404 sim
- gateway de conteúdo decifra o endereço só dentro do processo: sim (`ContentUrlKeyRing.Decrypt` no consumo da concessão)
- concessão Station pode ter chave própria, sem segredo Suite: sim

## Plano da próxima rodada

Não execute.

- resolução de `ItemId` no M.2 de 500 GB: o telefone manda só `ItemId`. A `5192` resolve no índice da biblioteca Station, que aponta para um ou mais volumes configurados pelo operador. O tamanho do disco não entra na descoberta. Com o tempo entram jogos novos e HDs novos: cada volume extra entra na mesma lista de raízes; o `ItemId` continua sem caminho. O cliente nunca escolhe arquivo.
- concessão curta, um uso, endereço cifrado até a entrega: `POST /v1/station/downloads/authorize` com sessão daquele aparelho gera concessão Station própria (chave AES da Station, fora do pepper Suite, da chave de conteúdo Suite e da sessão Suite). O endereço do arquivo fica cifrado em repouso. `GET /v1/station/artifacts/{grantId}` consome um uso, prazo curto, e só então decifra dentro do processo para entregar o bytes. Sem 307 para origem permanente.
- rotas que permanecem fechadas: catálogo e authorize continuam 503 até o handoff posterior mandar abrir. Sem sessão, 401. Sem `GET /v1/station/artifacts/{grantId}` público até essa entrega. Sem host de capa Sambox.
- por que um APK copiado não baixa jogo: não tem a chave do Keystore, não prova o `deviceId`, não abre sessão Station, não recebe concessão. Sem concessão não há arquivo. Capa não autoriza jogo.
- o que permanece intocado: `5190`, `5191`, PIX, `/mnt/DADOS`, site, SPA `/admin`, WhatsApp, migration `028`, senha e a licença de teste.
- lista e capas no login, sem atualizar o APK e sem URL permanente: `GET /v1/station/catalog` com sessão devolve corpo assinado `catalog/v1` (identificador, nome, plataforma, revisão, capa). Se a revisão mudou, o app baixa só as capas novas pela mesma sessão. Jogo novo no índice aparece no próximo login, sem atualizar o APK. A capa não autoriza o arquivo. O arquivo continua na concessão curta.

## Confirmação

- código alterado: nao
- migration aplicada: nao
- serviço reiniciado: nao
- catálogo ou download aberto: nao
- senha nova emitida: nao
- licença ativada desta máquina: nao
- WhatsApp enviado: nao

