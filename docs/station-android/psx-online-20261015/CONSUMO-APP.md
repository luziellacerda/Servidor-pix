# Como o app deve consumir o PSX publicado

## Catálogo, capas e downloads

Usar o fluxo de autenticação Station já implementado, TLS e prova de dispositivo.
As rotas abaixo exigem a autorização correspondente; um caminho local do HD não
é uma URL para o app.

1. `GET /v1/station/catalog?metadata=1`: verificar envelope assinado de domínio
   `catalog`; selecionar `platform=psx`. Relacionar pelo `itemId`, preservando
   `contentSha256`, `contentIdentityScheme`, `revision`, `coverId` e metadados.
   `metadata.description` é a sinopse; `metadata.players` não aprova online.
2. `GET /v1/station/covers/{coverId}` autenticado: usar a capa desse item. A chave
   do cache é `coverId` + `item.revision`. Não procurar capa por nome aproximado
   nem usar o número da revisão global como revisão de todos os itens.
3. `POST /v1/station/downloads/authorize`: enviar `itemId` e a identidade/prova
   de dispositivo já exigidas. Verificar o envelope `download-grant` e ler o
   `artifact` recebido. Ele define nome, formato, tamanho e arquivo de lançamento.
4. Consumir uma vez `GET /v1/station/artifacts/{grantId}`. Para uma nova tentativa
   obter nova autorização, conforme o cliente existente. Não persistir uma URL
   de grant como endereço permanente do jogo.
5. O catálogo tem 76 PBP, cinco CHD e três conjuntos CUE. PBP/CHD são artefatos
   `raw`; CUE usa `zip` com todas as faixas. O CUE publicado já normaliza as
   referências de Harvest Moon e Jackie Chan. Não lançar uma faixa isolada.

O SHA do artefato ZIP e o `contentSha256` do conjunto CUE têm finalidades distintas.
Preservar os campos do servidor. O mapeamento desta entrega não introduz outra
verificação de arquivo no caminho de download do app.

## Selecionar o modo do motor instalado

Entrar na presença por `POST /v1/station/online/command`, ação `enter`, antes
das ações v3. Depois consultar `POST /v1/station/online/multiplayer/command`,
ação `capabilities`, com `itemId`, `requestId` novo e `clientMaximumPlayers=2`.
Verificar envelope assinado `online` e identidade da resposta.

A seleção deve satisfazer todos os campos abaixo, usando o fingerprint real
do pacote instalado e o conteúdo exato do catálogo:

```text
profile.itemId          == catalogItem.itemId
profile.contentSha256   == catalogItem.contentSha256
profile.engineId        == installedEngine.engineId
profile.coreSha256      == installedEngine.coreSha256
profile.runtimeSha256   == installedEngine.runtimeSha256
profile.profileSha256  == computedControllerProfileSha256
profile.approved       == true
2 in profile.allowedPlayerCounts
```

Para o owner desta entrega, os valores fixos estão no README. `profileId` é a
identidade do modo escolhido devolvida pela API. Não selecionar só pelo título,
por `platformPolicy.onlineAvailable` ou pelo primeiro perfil histórico PSX.
`serverReady` indica infraestrutura cadastrada; o perfil exato determina o modo
que o aparelho pode abrir. Classificação local com `approved=true`, máximo 1 e
`allowedPlayerCounts=[]` concede zero vagas online.

Exibir `modeTitle` e `instructions` do modo, junto da sinopse do catálogo. Se o
perfil do pacote não estiver aprovado, mostrar o impedimento; não substituir por
um perfil de outra engine nem abrir e repetir tentativas indefinidamente.

## Sala e partida

Na ação `create`, enviar `capacity=2` e os sete campos do vínculo selecionado:
`itemId`, `contentSha256`, `engineId`, `coreSha256`, `runtimeSha256`, `profileId`,
`profileSha256`. Nas ações seguintes preservar `roomId`, `generation` e vínculo
da sala devolvidos pelo servidor. Cada aparelho precisa da mesma edição, motor,
runtime e perfil. Não substituir esses campos por rótulos da interface.

O anfitrião ocupa slot 1, o convidado slot 2. Convidado precisa efetivamente
executar `join`; `ready` não faz a entrada. Depois os dois marcam `ready` e o
anfitrião executa `start`. Usar o `clientMaximumPlayers` real de dois nessa rota.
Multitap e salas de três, quatro ou cinco pessoas não estão implementados neste
owner PSX; os limites N64/SNES não mudam esse fato.

Usar tickets e provas automáticos já implementados para WSS, subprotocolo
`station-stream.v3`. O PXL1 nativo e a entrada original PSX administram estado,
entradas digitais, pausas e retomada. Confirmar execução exige o retorno real
de `retro_run`; ACK de rede ou sala em `playing` não prova primeiro quadro.
Manter a política de presença/heartbeat e apenas um poll por aparelho.

Não repetir automaticamente ações de criar, entrar ou iniciar depois de erro
ambíguo: primeiro conciliar o estado. Respeitar backoff/Retry-After para leituras.
A saída segue a política humana já implementada; não acrescentar reinício
automático do emulador para contornar um modo recusado.

## Aceite físico

Os recibos desta pasta distinguem prova de API/relay, execução diagnóstica do
core e gameplay Android. Para aprovar a montagem do app, a produção ainda deve
registrar os fingerprints dos dois aparelhos, primeiro quadro/áudio, entradas
distintas, partida, pausa/retomada e saída. Nenhum teste sintético substitui isso.
