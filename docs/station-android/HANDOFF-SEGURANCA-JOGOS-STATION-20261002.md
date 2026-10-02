# Handoff — segurança dos jogos Station, análise para implementação

Data: 02/10/2026. Base: `docs/perfil-nome-station-20261002` commit `52622d7`. O telefone de teste já está ligado à licença e o nome `Teste Station` sai de `GET /v1/station/me`. Esta rodada não muda aplicativo, banco, Nginx nem serviço. Ela mede o servidor e devolve o plano da próxima implementação.

## 1. O que já está feito

Não refaça.

- Produto `TURBORAMA_STATION_ANDROID`, SKU `STATION_ANDROID_LIFETIME_1_DEVICE`, R$ 99,90, vitalícia, um aparelho ativo por licença. Licença de teste `STA-D7AE45616B415B2C7550315C0392C5D8`, compra `station-teste-20261001`, item `android-1`, cliente `teste-station`, nome `Teste Station`. Estado medido: `BOUND`, ativação consumida, um aparelho ativo, projeção com `display_name` e `profile_version` 1.
- A `5192` publica só `/v1/station/` em `127.0.0.1:5192`. A migration `028` já está aplicada. Não aplique outra. Suite `5190`, admin/gateway `5191` e PIX ficam como estão.
- Sessão 180 segundos. Desafio 60 segundos. Sem heartbeat. A chave privada do aparelho fica no Android Keystore. O `deviceId` é o hash da chave pública. Copiar o APK não copia essa chave.
- `GET /v1/station/catalog` com sessão válida responde 503 `STATION_CATALOG_NOT_READY`. Sem sessão, 401 `STATION_SESSION_INVALID`. `POST /v1/station/downloads/authorize` lê `StationDownloadRequest` e responde 503 `STATION_DOWNLOAD_NOT_READY`. O corpo já tem `ItemId`. Não tem caminho de arquivo. `GET /v1/station/artifacts/{grantId}` não existe.
- O gateway de conteúdo da Suite, `ContentGatewayService`, só decifra o endereço de origem dentro do processo, no momento de consumir a concessão. A Station não usa sessão Suite, pepper Suite nem chave de conteúdo da Suite.
- O disco raiz e `/mnt/DADOS` não são a biblioteca de jogos. `/mnt/DADOS` é área de compilar e testar. Os jogos ficam num M.2 de 500 GB, separado desses dois. O ponto de montagem não entra neste documento.

## 2. O que a segurança precisa garantir

Um APK copiado não baixa jogo. O servidor não publica o HD de jogos.

- O aplicativo Station não guarda URL de jogo, caminho de disco, segredo de assinatura de link, pepper nem token interno.
- A linha Sambox, com `drawers.json` e host de capas, é outro produto. Não misture com a Station e não copie o segredo dela para cá.
- O telefone manda `ItemId` dentro da sessão da licença e daquele aparelho. Quem escolhe o arquivo é o servidor, no M.2 de 500 GB.
- A resposta de catálogo, quando um dia existir, leva identificador, nome, plataforma e referência de capa. Sem URL de arquivo.
- A concessão é curta, de um uso, presa ao aparelho e ao item. O endereço de origem só existe na memória do processo que entrega o arquivo, no mesmo modelo do gateway de conteúdo. Não vai para log, Git, Nginx nem APK.
- Se o M.2 de 500 GB não estiver montado, ou o `ItemId` não existir, a resposta é falha fechada. Não há atalho público.
- Um aparelho ativo por licença. Outro telefone não herda a cópia do APK.
- Depois da sessão Station válida, o aplicativo pede o catálogo e as capas. Um jogo novo no servidor aparece no próximo login, sem atualizar o APK. A capa é imagem, não é o arquivo do jogo. O login e a biblioteca abrem mesmo se o catálogo ainda responder 503: o aplicativo usa a última lista salva. Abrir o aplicativo não depende dessa chamada.

## 3. O que medir

Somente leitura. Não crie arquivo, não monte disco, não edite Nginx, não reinicie, não abra sessão e não chame ativação.

1. Existe um M.2 de cerca de 500 GB que não é a raiz e não é `/mnt/DADOS`: sim ou nao. Está montado: sim ou nao. Não escreva o ponto de montagem, o nome do dispositivo, a lista de pastas, nomes de jogo nem o tamanho ocupado da biblioteca.
2. Alguma location pública do Nginx, ou outro serviço HTTP neste host, expõe esse disco: sim ou nao. Se sim, diga só o nome do server/location, sem o alias e sem o caminho.
3. A `5192` continua em loopback, e o Nginx público manda apenas `^~ /v1/station/` para `127.0.0.1:5192`: sim ou nao. O token interno da Suite não está no ambiente da `5192`: sim ou nao. Não imprima o ambiente.
4. Sem sessão, `GET /v1/station/catalog` e `POST /v1/station/downloads/authorize` seguem 401. `GET /v1/station/artifacts/inexistente` segue 404. Não abra sessão para repetir o 503.
5. O gateway de conteúdo decifra o endereço só dentro do processo, ao consumir a concessão: sim ou nao, pelo código em execução, não por uma cópia antiga. Diga se uma concessão Station pode nascer ao lado, com chave própria, sem usar pepper Suite, chave de conteúdo Suite ou sessão Suite. Se a única forma for reusar esses segredos, responda nao e pare.

## 4. O plano que o retorno deve trazer

Escreva o plano da rodada seguinte. Não o execute.

- Onde o código Station passa a resolver `ItemId` para um arquivo do M.2 de 500 GB, sem o cliente enviar caminho.
- Como a concessão Station fica curta e de um uso, com o endereço cifrado em repouso e decifrado só na entrega.
- Quais rotas continuam 503 até um handoff posterior dizer para abrir.
- Como um APK copiado falha: sem chave de aparelho, sem licença, sem concessão.
- O que não muda: `5190`, `5191`, PIX, `/mnt/DADOS`, site, SPA `/admin`, WhatsApp, migration `028`, senha e a licença de teste.
- Como o catálogo Station entrega lista e capas no login: corpo assinado, com revisão. Se a revisão mudou, o aplicativo baixa só as capas novas pela mesma sessão. Sem host de capa da linha Sambox, sem URL permanente e sem caminho do M.2 de 500 GB. A capa não autoriza o arquivo do jogo. O arquivo continua na concessão curta.

Não implemente esse plano agora. Não compile, não troque DLL, não reinicie `5190`, `5192`, admin, gateway nem PIX. Não crie migration. Não abra catálogo nem download. Não envie WhatsApp. Não emita senha. Não ative.

## 5. Retorno

Preencha `RETORNO-SEGURANCA-JOGOS-STATION-20261002.md` e faça push nesta branch em seguida. Sem pepper, DSN, token, chave privada, senha, device id, ponto de montagem, lista de arquivos ou URL de jogo.
