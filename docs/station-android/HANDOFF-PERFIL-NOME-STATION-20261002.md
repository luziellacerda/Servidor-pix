# Handoff — nome do perfil Station no Bem-vindo

Data: 02/10/2026. Base: `docs/senha-station-48h-20261002` commit `01c391b`. O telefone já ativou a licença de teste. O aplicativo abre o Bem-vindo sem o nome da pessoa. Esta rodada só faz o servidor conseguir entregar esse nome. Não muda o aplicativo.

## 1. Conclusão do que já está feito

Isto é fato das rodadas anteriores e do teste no Samsung `RQCY30751WY`. Não refaça.

- Produto `TURBORAMA_STATION_ANDROID`, SKU `STATION_ANDROID_LIFETIME_1_DEVICE`, R$ 99,90, vitalícia, um aparelho ativo por licença. A licença de teste é `STA-D7AE45616B415B2C7550315C0392C5D8`, compra `station-teste-20261001`, item `android-1`, cliente `teste-station`, nome sintético `Teste Station`.
- A `5192` publica `/v1/station/`. A migration `028` já está aplicada. Não aplique outra. Suite `5190`, admin/gateway `5191` e PIX ficaram nos processos antigos.
- A primeira senha de compra paga dura 48 horas. A reemissão humana em `https://turbobox.lzgames.com.br/admin/station`, pelo helper `127.0.0.1:5194`, dura 30 minutos e substitui a senha ainda válida. O OTP de 15 minutos da Suite Windows não muda. Sessão 180 segundos. Desafio 60 segundos. Sem heartbeat.
- WhatsApp de primeira senha, de reemissão confirmada e de ativação está no código. Nesta licença de teste nenhuma mensagem foi enviada.
- O Samsung recebeu o APK com o extra `stationLogin`, sem apagar a sessão local. A senha de 48 horas foi digitada no aparelho. O aplicativo saiu do login e abriu a biblioteca. Uma entrada seguinte com o campo vazio também abriu a biblioteca, o que indica que `station-license-id.txt` ficou gravado nesse telefone. Não ative de novo e não emita outra senha.
- Catálogo, download e `GET /v1/station/artifacts/{grantId}` continuam fechados. O site não vende este SKU. O cartão Android não está no SPA Suite `/admin`. O backup da licença e o plano de isolar a `5192` sem down migration já foram escritos. Não execute o rollback.

## 2. Por que o nome não aparece

O título da tela de abertura é o texto fixo `BEM-VINDO DE VOLTA`. Ele não consulta o servidor.

O nome só pode entrar na frase `Bem-vindo, {displayName}`. O aplicativo pede isso em `GET /v1/station/me`, com o bearer da sessão Station. O corpo assinado `TurboRamaStationAndroid/profile/v1` traz `displayName`. Se a resposta é 503 `STATION_PROFILE_NOT_READY`, o aplicativo grava nome vazio e mostra só `Bem-vindo`.

No código da `5192`, o perfil lê `suite.station_customer_projection` junto da entrega Station. `display_name` nulo ou `profile_version` nulo viram 503. A tabela tem `profile_version bigint NOT NULL DEFAULT 1`. O script da licença de teste insere `display_name` com o nome sintético `Teste Station`. Se a linha existe e casa com a entrega, o servidor já consegue entregar o nome e esta rodada para na medição.

O aplicativo, mesmo recebendo o nome, hoje só escreve a frase na linha de status e abre a biblioteca por cima. A abertura pelo ícone ainda usa a senha local antiga e não chama `/v1/station/me`. Isso é correção do APK, fora desta rodada. Não altere o contrato para compensar.

## 3. O que fazer

Meça só a licença `STA-D7AE45616B415B2C7550315C0392C5D8`. Use o mesmo acesso `sudo -u postgres` das rodadas anteriores. Não imprima device id, chave pública, verifier, pepper, DSN nem token.

Anote:

- `enrollment_state`
- `activation_consumed`
- existe uma linha em `station_devices` com status ativo: sim ou nao. Não cole o identificador do aparelho.
- existe linha em `station_customer_projection`: sim ou nao
- `display_name` é `Teste Station`: sim ou nao
- `profile_version`
- a entrega Station casa com a projeção em sistema, compra e item: sim ou nao
- com esses dados, o mesmo critério do `FindSessionAsync` devolveria `display_name`: sim ou nao

Se o nome sairia, não mude banco nem binário. Escreva `nome-disponivel` e pare.

Se a projeção não existe, o nome está vazio, `profile_version` está nulo, ou a entrega não casa, corrija somente a projeção desta licença. O nome continua `Teste Station`. `profile_version` fica 1 se estiver nulo. A ligação com a entrega já gravada deve fazer o SELECT do perfil enxergar o nome. Não crie compra, não mude outra licença, não mude `enrollment_state` e não gere senha.

Não reinicie `5190`, `5192`, admin, gateway nem PIX para uma correção só de linha. A `5192` lê o banco na hora do pedido. Se o nome continuar invisível por um erro do binário, não troque a `5192` nesta rodada: descreva o erro e pare.

Não chame ativação, não abra sessão a partir do servidor e não faça `GET /v1/station/me` com um aparelho falso. A prova é o SQL do critério acima.

Não envie WhatsApp. Não abra catálogo nem download. Não crie `GET /v1/station/artifacts/{grantId}`. Não edite site nem o SPA `/admin`.

## 4. Retorno

Preencha `RETORNO-PERFIL-NOME-STATION-20261002.md` e faça push nesta branch em seguida. Sem pepper, DSN, token, chave privada, senha, device id ou URL de jogo.
