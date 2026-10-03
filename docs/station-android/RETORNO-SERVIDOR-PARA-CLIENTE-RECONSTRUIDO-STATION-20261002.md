# Handoff técnico único: servidor, conexão e instalação do TurboStations Android

Atualizado em 03/10/2026, 08h11 (America/Maceio). Preserva as evidências de 02/10/2026 identificadas abaixo. **Manter as próximas atualizações neste arquivo**, com data e prova; a equipe Android precisa de um único retorno para concluir o APK.

**Decisão operacional:** ainda não liberar o APK como conectado e não implantar o código novo na 5192. Falta preparar o índice real, resolver as capas 404, executar o fluxo autenticado de download e integrar/instalar o APK. Código no Git, build e `/ready/station` 200 não substituem essas provas.

## Referências e decisão

- Pedido atendido: [handoff do cliente reconstruído](https://github.com/luziellacerda/Servidor-pix/blob/7ac4fad9e0132db378f6e78e6494fedb08f614c3/docs/station-android/HANDOFF-CLIENTE-RECONSTRUIDO-STATION-20261002.md), branch `docs/cliente-reconstruido-station-20261002`, commit `7ac4fad9e0132db378f6e78e6494fedb08f614c3` do Servidor-pix.
- Cliente examinado: TurboElden, commit de código `0840028854034b03e5a1d3f2a162d66225932a6b` e revisão documental mais recente `f48399bc24691afe2073fac55f279b452c01343b`, branch `station-reconstrucao-20261002`. A revisão nova não alterou Java. A biblioteca Java ainda não foi integrada ao APK distribuível nem consome o descritor novo.
- Código do servidor deste retorno: branch `feat/station-artifact-descriptor-20261002`, commits `96326aa0aeb164820cec26f8b5911fcdb47fc8ee` e `1bfb619c21becffe40aaa597e100fcb3719c2e72` (revisão final do código e testes), derivados de `b1159c9`.
- **Estado: código e contrato publicados para desenvolvimento; produção e APK ainda não liberados.** Não houve publicação de binário, alteração de índice real, migration, restart, mudança de porta ou alteração dos serviços PIX/Suite/ES. Não há prova de download autenticado completo na 5192.

## Rede e limites do aplicativo

O telefone usa somente `https://app.lzgames.com.br/v1/station/*` na porta 443: Cloudflare → túnel → nginx → API Station em `127.0.0.1:5192`. A porta 5190 é a API Suite Windows; 5191 é o gateway de conteúdo Suite; 5194 é o helper de emissão comercial Station. Nenhuma dessas portas, IP privado, caminho do disco ou URL de jogo entra no APK. Capa e jogo chegam como bytes autenticados da própria 5192, sem redirect. O APK não deve chamar `/v1/suite/*`, usar cookie, cache público ou gravar Bearer/grant no disco.

O cliente usa `productId=applicationId=TURBORAMA_STATION_ANDROID`, pacote `org.turboramastation.frontend`, `schemaVersion=1`, domains `TurboRamaStationAndroid/<ação>/v1` e `deviceId` Base64URL do SHA256 da chave pública SPKI RSA-2048 do Android Keystore. Respostas assinadas usam `{keyId,payload,signature}`; `payload` são bytes JSON UTF-8 em Base64URL sem padding, assinatura RSA-PSS/SHA256 sobre esses bytes. O `keyId` público configurado no cliente é `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`; comparar com uma resposta autenticada após implantação. O pin TLS SPKI SHA256 configurado no cliente é `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` e **foi confirmado novamente no endpoint público em 03/10/2026**. Não duplicar essas constantes em outra classe Android.

## O que roda no Linux, separado do código novo

| Verificação em 03/10/2026 | Resultado observado |
| --- | --- |
| Serviço | `turborama-station-api.service` ativo; PID 281271; sem drop-ins. |
| Comando efetivo | `/usr/bin/dotnet /opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll`. |
| SHA256 do DLL efetivo | `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`. Este hash identifica o binário; não prova sozinho um commit de origem. |
| Escuta | `127.0.0.1:5192`, sem porta 5192 pública. O único endereço que o APK usa é `https://app.lzgames.com.br/v1`. |
| Sondas sem credencial | `/ready/station` local 200; `/v1/station/catalog` público 401; capa pública 401; grant inexistente 404. Essas respostas não comprovam conteúdo autenticado. |
| Logs desde o início do processo | `/me` 200 em 3 requisições e `/catalog` 200 em 3; `/covers` 404 em 48 e nenhum 200; nenhuma autorização/download 200. IDs e tokens não foram extraídos nem publicados. |
| Índice carregado | O arquivo configurado e seu conteúdo não puderam ser lidos nesta conta. Revisão, quantidade e valores de `platform` **não foram conferidos ao vivo**. |
| Inventário geral somente leitura | nginx, cloudflared, PostgreSQL, PIX, Suite 5190 e gateway 5191 ativos. `turborama-suite-content-monitor.service` continua `failed`; é uma pendência independente a avaliar no gate operacional antes de uma implantação, sem alterar outros serviços nesta tarefa. |

O handoff anterior cita 996 jogos no índice e os identificadores `megadrive`, `snes`, `snesbr`, `gamegear`, `gb`, `sega32x`, `gbc`, `gba`; são dados documentais, não resultado de uma resposta autenticada capturada agora. O mesmo material cita 835 jogos SNES e 981 Mega Drive no disco; quantidade no disco não equivale ao catálogo. O limite atual da API é 4096 itens: a lista documental de 12.346 exige evolução explícita do contrato antes de caber no catálogo.

## Ativação, sessão e perfil: divergência resolvida no código

`STA-` é o prefixo do **licenseId** criado em `StationCommerceEndpoints.ProvisionAsync`: `STA-` seguido por 32 dígitos hexadecimais maiúsculos. Não é a senha que o comprador digita. `StationCommerceEndpoints` emite o `activationCode` com 32 bytes aleatórios codificados em Base64URL canônico sem `=` (43 caracteres). Exemplo **sintético e não válido comercialmente**: `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA`. O servidor verifica exatamente Base64URL canônico de 32 bytes em `StationService.Verifier`, tanto no pedido de challenge quanto no complete. Espaços, quebras, `=`, mudança de caixa e prefixo `STA-` não são normalizados. O cliente Java `token(code)` está alinhado com o formato do código emitido; o texto do handoff humano que chamava o código de `STA-` confundiu licença com código. Códigos comerciais já emitidos não foram alterados.

O challenge de ativação vale 60 s; o complete exige prova RSA-PSS do aparelho e o mesmo código/verificador. O challenge de sessão também vale 60 s; a sessão Bearer vale 180 s e está vinculada a licença, aparelho e revogação. `/v1/station/me` devolve payload assinado `profile/v1` com `displayName` (até 80 caracteres no retorno) e `profileVersion`, além de `licenseId`, `deviceId` e `sessionId`. Perfil ausente retorna 503 `STATION_PROFILE_NOT_READY`.

## Sequência HTTP exata para o APK

Todos os caminhos abaixo começam em `https://app.lzgames.com.br/v1/station/`. Pedidos JSON de identidade usam `schemaVersion`, `domain`, `productId`, `applicationId`, `deviceId`, `clientVersion`, `deviceManufacturer`, `deviceModel`, `androidSdk` em camelCase; `domain` é o valor completo mostrado na tabela. Os dois pedidos de prova usam envelope `{payload,signature}` assinado pelo aparelho. Respostas de controle são envelopes assinados pelo servidor; imagens e jogos são bytes, não JSON. Verificar `keyId`, assinatura, domain, produto, aplicação, licença, aparelho e sessão antes de usar o payload. Desativar redirects em todas as chamadas.

| Ordem | Método e rota | Pedido adicional / domain completo | Resposta e decisão do APK |
| --- | --- | --- | --- |
| 1, somente na primeira ativação | `POST activations/challenge` | `activationCode`, `devicePublicKey`; `TurboRamaStationAndroid/request-activation-challenge/v1` | `activation-challenge/v1`, `challengeId`, `nonce`, 60 s. |
| 2 | `POST activations/complete` | Prova PSS com `activationCode`, `challengeId`, `nonce`, chave pública; `TurboRamaStationAndroid/activate/v1` | `activated/v1`, `licenseId`; salvar somente licença em armazenamento privado sem backup. |
| 3, a cada sessão | `POST challenges` | `licenseId`; `TurboRamaStationAndroid/request-session-challenge/v1` | `session-challenge/v1`, `challengeId`, `nonce`, 60 s. |
| 4 | `POST sessions` | Prova PSS; `TurboRamaStationAndroid/open-session/v1` | `session/v1`, `accessToken`, 180 s; manter Bearer só em memória. |
| 5 | `GET me` | `Authorization: Bearer <accessToken>` | `profile/v1`, `displayName`, `profileVersion`; 503 preserva o último nome confiável. |
| 6 | `GET catalog` | Mesmo Bearer | `catalog/v1`, `revision`, `items[]` de `itemId`, `name`, `platform`, `revision`, `coverId`; cache privado do envelope assinado. |
| 7, somente capas visíveis/ausentes | `GET covers/{coverId}` | Mesmo Bearer; `coverId` do catálogo | Bytes PNG/JPEG/WebP/GIF; cache por `coverId` + revisão do item. |
| 8, quando o usuário abre jogo ausente | `POST downloads/authorize` | JSON de identidade + `itemId`; `TurboRamaStationAndroid/request-download/v1`; mesmo Bearer | `download-grant/v1` assinado com `grantId`, `itemRevision`, `artifact`, 60 s. |
| 9, uma vez por grant | `GET artifacts/{grantId}` | Mesmo Bearer e sessão; sem Range | Bytes `application/octet-stream`, `Content-Length` obrigatório; conferir hash antes de instalar. |

`itemId` e `coverId` aceitos pelo servidor têm 8–64 caracteres ASCII `[A-Za-z0-9_-]`; usar exatamente os valores assinados do catálogo. Não usar o nome exibido como pasta, URL ou identificador. `challengeId` e `sessionId` são 64 caracteres hexadecimais minúsculos; `nonce`, `accessToken` e `grantId` são Base64URL canônicos de 32 bytes. O cliente já limita o catálogo a 4096 itens e o envelope a 12 MiB; não reduzir esse leitor ao limite de 8 KiB dos pedidos JSON.

## Contrato novo de download, ainda não implantado

Rotas e métodos permanecem `POST /v1/station/downloads/authorize` e `GET /v1/station/artifacts/{grantId}`. Pedido de autorização continua com schema 1, domínio `request-download`, identidade Station e `itemId`, sob Bearer válido. A resposta é o envelope `keyId`, `payload`, `signature`: payload JSON UTF-8 Base64URL canônico, domínio `download-grant/v1`, assinado com RSA-PSS/SHA256; `keyId` é SHA256 hexadecimal minúsculo da chave pública SPKI. O cliente deve verificar a assinatura antes de usar qualquer campo. A resposta agora acrescenta `itemRevision` e `artifact` dentro desse payload, preservando `itemId`, `grantId`, `expiresInSeconds` (60), licença, aparelho e sessão.

| Campo no payload assinado | Tipo e regra |
| --- | --- |
| `itemRevision` | inteiro positivo; igual à revisão do item no catálogo carregado. |
| `artifact.fileName` | string de 1 a 255 caracteres; nome base real, sem `/`, `\`, `:`, controles ou `.`/`..`. |
| `artifact.sizeBytes` | inteiro positivo até 1 TiB; tamanho do arquivo servido. |
| `artifact.sha256` | 64 hexadecimais minúsculos; SHA256 do arquivo servido. |
| `artifact.format` | `raw`, `zip`, `rar` ou `7z`; extensão e assinatura binária conferidas. |
| `artifact.launchPath` | string de 1 a 512 caracteres; caminho relativo dentro do item, com `/`, sem componente vazio, `.`/`..`, `\`, `:` ou controles. Para `raw`, igual a `fileName`. |
| `artifact.expandedSizeBytes` | inteiro positivo até 4 TiB; para `raw`, igual a `sizeBytes`. |
| `artifact.fileCount` | inteiro de 1 a 100.000; para `raw`, 1. |

Exemplo **sintético** do conteúdo decodificado de `payload`, para mostrar os nomes e tipos. O envelope assinado real contém esses bytes em Base64URL; os valores abaixo não são licença, sessão ou grant utilizável:

```json
{
  "schemaVersion": 1,
  "domain": "TurboRamaStationAndroid/download-grant/v1",
  "productId": "TURBORAMA_STATION_ANDROID",
  "applicationId": "TURBORAMA_STATION_ANDROID",
  "licenseId": "STA-00000000000000000000000000000000",
  "deviceId": "<Base64URL do aparelho>",
  "sessionId": "<sessão do aparelho>",
  "itemId": "item-sintetico-01",
  "itemRevision": 1,
  "artifact": {
    "fileName": "item.bin",
    "sizeBytes": 4,
    "sha256": "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
    "format": "raw",
    "launchPath": "item.bin",
    "expandedSizeBytes": 4,
    "fileCount": 1
  },
  "grantId": "<Base64URL de 32 bytes>",
  "expiresInSeconds": 60
}
```

O cliente Java do commit `0840028` **ainda ignora `itemRevision` e `artifact`** em `StationApi.authorize`; seu `downloadToStaging` transporta bytes e não instala. A equipe Android deve acrescentar um modelo tipado do descritor, conferir limites e revisão contra o item do catálogo e recusar autorização antiga sem descritor. É um acréscimo ao payload da mesma rota e do mesmo domain; não criar endpoint ou link de jogo. `fileName` e `launchPath` são metadados assinados, jamais vindos da URL nem deduzidos da plataforma.

O `filePath` privado fica só no índice e no grant cifrado por AES-GCM; não aparece no catálogo, descritor ou cabeçalhos. O grant cifrado vincula caminho, revisão, hash, tamanho e data de modificação à licença, aparelho, **sessão**, item e grantId. O consumo é único. Ao consumir, o servidor compara o índice e a identidade do arquivo; mudança detectada nega com 404. O endpoint abre o arquivo antes do 200 e envia `application/octet-stream`, `Content-Length` exato, `Cache-Control: no-store`, sem URL, redirecionamento, `Content-Disposition` ou Range. Se a conexão cair depois do consumo, o cliente precisa pedir **nova autorização** e começar nova transferência; o grant anterior não é retomável.

Na carga do índice, o servidor verifica tamanho, assinatura binária e SHA256 de cada arquivo com descritor. Para ZIP também confere membros seguros, `launchPath`, total extraído e número de arquivos. A ferramenta `scripts/preparar-indice-artefatos.py` produz os descritores fora da requisição, inspeciona ZIP/RAR/7z, exige escolha explícita de `launchPath` quando há vários arquivos e escreve o novo índice com modo 0600. Exemplo de uso, com caminhos **locais e privados definidos pelo operador**, nunca pelo APK:

```text
python3 docs/station-android/scripts/preparar-indice-artefatos.py \
  --index INDICE_ATUAL.json --launch-manifest ESCOLHAS.json --output INDICE_NOVO.json
```

O manifesto é um objeto JSON `itemId` → `launchPath`, por exemplo `{"item-sintetico":"disc/game.cue"}`. Itens raw ou arquivos compactados de membro único dispensam escolha. RAR/7z têm membros conferidos pela ferramenta `7z` durante o preparo; a API confere assinatura, tamanho e SHA256 na carga, mas não reinspeciona membros desses dois formatos. O arquivo aprovado precisa permanecer imutável durante a validade do grant. Antes de implantação, validar que o armazenamento real aplica essa condição. A mudança impede ativar downloads novos em itens cujo índice ainda não tem descritor: eles retornam 503 `STATION_ARTIFACT_NOT_READY`. Por isso **não é seguro instalar este binário sobre o índice atual sem prepará-lo e validá-lo**.

Erros relevantes: 401 `STATION_SESSION_INVALID`; 403 `STATION_DEVICE_DENIED`; 404 `STATION_ITEM_NOT_FOUND` para ID fora do catálogo; 503 `STATION_ARTIFACT_NOT_READY` para metadados ausentes ou arquivo mudado; 404 `STATION_GRANT_NOT_FOUND` para grant inexistente, consumido, expirado, de outra sessão/aparelho/licença ou com vínculo divergente; 429 `STATION_RATE_LIMITED` (limite atual de 30 requisições por IP/rota/minuto); 400 para JSON/identidade malformados. Revogação/bloqueio continua governada pelo store de sessão e pelas regras comerciais existentes; falta prova com licença sintética no banco isolado.

Erros JSON usam `schemaVersion`, `code`, `message`; o app toma decisão por HTTP e `code`, não pelo texto de `message`. 401 pede renovação de sessão; 404 de grant pede nova autorização se a licença continuar válida; 429 exige espera; 503 de índice/artefato não autoriza inventar caminho local. Não repetir automaticamente a ativação comercial.

### Exemplos de instalação, todos sintéticos

| Caso | `format` e `fileName` | `launchPath` escolhido | `fileCount` e `expandedSizeBytes` | Estado da prova |
| --- | --- | --- | --- | --- |
| ROM crua | `raw`, `item.bin` | `item.bin` | 1 e 4 | Teste local: bytes `01 02 03 04`, SHA256 `9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a`. |
| Contêiner com BIN/CUE | `zip`, `multi.zip` | `disc/game.cue` | 2 e 5 | ZIP sintético validado no teste local; o SHA256 é calculado no preparo, não é um jogo real. |
| Wii U com diretórios | `7z`, `wiiu-exemplo.7z` | `code/game.rpx` | Depende da inspeção do pacote; exemplo de três arquivos em `code/`, `content/`, `meta/`. | Estrutura ilustrativa; não há pacote Wii U de produção inspecionado nem hash real a informar. |

Nenhuma dessas linhas configura plataforma ou arquivo real de produção. O instalador Android deve conferir assinatura, item/revisão, `Content-Length`, SHA256, formato, limites de extração e `launchPath` antes de registrar o jogo como instalado.

## Capas e cache

O código novo mantém `coverId` → `coverPath` do índice, confere tamanho de 1 byte a 5 MiB e assinatura PNG/JPEG/WebP/GIF compatível com o MIME. IDs de capa compartilhados só são aceitos se caminho **e revisão do item** forem iguais; ambiguidade falha na carga do índice. A revisão de capa continua sendo a revisão do item, pois não há `coverRevision` separado. Ao trocar bytes, publicar nova revisão do item ou novo `coverId`; o cache local do APK usa ID e revisão. A resposta HTTP usa `no-store`, e ID inexistente gera 404 depois da autenticação.

Teste local de `StationLibrary.ReadCover`: PNG sintético válido de 1×1 pixel, 70 bytes, `image/png`, SHA256 `c2153f77e11087fcb078ae38527fa83bef29791e3700e30cc87fec4405a66d0f`. Isso **não é uma resposta HTTP autenticada 200**. Produção ainda mostra 48 respostas 404 para capas e nenhum 200 observado desde o início do processo. A causa precisa ser identificada no índice efetivamente carregado, permissões/arquivos e IDs pedidos pelo APK, sem expor caminhos privados. O 404 autenticado de ID inexistente também precisa ser registrado com uma sessão sintética válida.

## Evidências e pendências para liberar o APK

| Requisito do pedido | Comprovado aqui | Falta, responsável |
| --- | --- | --- |
| Código do descritor e teste | Commits acima; `dotnet run --project tests/TurboRamaSuiteOnlineServer.Tests/TurboRamaSuiteOnlineServer.Tests.csproj` passou. Testes locais cobrem raw, ZIP com dois arquivos, membro ZIP duplicado, hash divergente, `launchPath` ausente, revisão de capa conflitante e PNG. A ferramenta de preparo foi exercitada com raw, ZIP e 7z sintéticos. | Backend: revisão de código e validação de carga no índice real. |
| Catálogo autenticado: revisão, total, plataformas | Logs mostram três HTTP 200 de `/catalog`, sem corpo. | Operação/backend: sessão sintética e captura sanitizada de `revision`, quantidade e `platform` exatos da 5192. |
| Capa válida 200 e ID inexistente 404 | Unidade local de capa acima; público sem Bearer responde 401. | Operação/backend: corrigir os 404 atuais e registrar 200 autenticado com MIME/tamanho/hash e 404 autenticado de ID inexistente. |
| Autorização assinada e bytes completos | Código e testes locais do índice passaram; público sem sessão não prova o fluxo. | Backend: banco isolado ou homologação com licença/aparelho sintéticos; capturar `itemRevision`, descritor, assinatura, Content-Length e hash dos bytes transferidos. Produção: repetir somente depois da implantação validada. |
| Reuso, expiração, bloqueio e outro aparelho | Código de grant único e vínculo foi revisado; não há execução autenticada desses casos neste retorno. | Backend/QA: executar os quatro casos com contas sintéticas e registrar status/códigos, inclusive queda de conexão e nova autorização. |
| Índice/arquivos real e imutabilidade | O índice efetivo não ficou acessível à conta desta apuração. | Operação/backend: preparar descritores, validar todos os itens e capas, tempo/custo de leitura SHA256 na inicialização, permissões e imutabilidade durante 60 s. |
| APK instalado e conexão ponta a ponta | Cliente Java do commit citado existe, mas não consome os campos novos no APK. | Android: integrar `artifact`/`itemRevision`, carrossel/capas, instalador seguro, empacotar e testar APK em aparelho. |

## Compatibilidade e ordem de liberação

| Combinação | Resultado esperado agora |
| --- | --- |
| 5192 atual + APK instalado atual | API responde, mas capas mostram 404 nos logs e não há download completo provado. Não é aceite. |
| 5192 atual + cliente reconstruído | O servidor atual não inclui `artifact`/`itemRevision`; o instalador novo deve recusar, sem inventar extensão ou marcar instalado. |
| Código novo + índice antigo sem descritores | Catálogo pode abrir, mas `POST downloads/authorize` de item existente devolve 503 `STATION_ARTIFACT_NOT_READY`. Não implantar nessa combinação. |
| Código novo + índice preparado + APK antigo | O contrato acrescenta campos, mas o APK antigo não sabe instalar por eles; não declarar sucesso. |
| Código novo + índice/capas validados + APK novo integrado | Candidato de homologação; liberar somente após as provas autenticadas e no aparelho abaixo. |

## Plano único de execução, responsáveis e aceite

### 1. Backend e operação: preparar o conteúdo real antes de trocar a 5192

1. **Operação/backend:** com acesso autorizado ao arquivo `Station:LibraryIndexFile` efetivamente configurado, registrar somente revisão, contagem e histograma exato de `platform` em relatório sanitizado. Conferir `Station:Enabled`, `Station:DownloadKeyFile`, chave pública/`keyId` e migrations 028 e 029 no banco **sem copiar segredos**. O índice é privado e não entra no Git. Se houver mais de 4096 itens, planejar paginação/versão nova do catálogo antes de publicar os excedentes; não truncar silenciosamente.
2. **Operação/backend:** diagnosticar os 48 retornos 404 de capa com um `coverId` **obtido do catálogo autenticado**, existência/permissão do `coverPath` correspondente, MIME real, tamanho e revisão. Corrigir índice/arquivos/permissões sem expor caminhos. Se o mesmo `coverId` for compartilhado, caminho e revisão precisam coincidir; ao mudar bytes, incrementar revisão do item ou emitir novo `coverId`.
3. **Operação/backend:** preparar `artifact` para cada item a liberar com `preparar-indice-artefatos.py` e escolhas explícitas de `launchPath` para pacotes de múltiplos arquivos; validar em cópia privada antes de ativar. Conferir arquivos raw, BIN/CUE, RAR/7z e diretórios Wii U reais; registrar hashes e contagens agregadas, sem nomes de jogos ou caminhos no handoff. Garantir que os arquivos publicados não mudem durante 60 s de grant. A carga do código novo lê e verifica SHA256 de cada arquivo com descritor; medir tempo de inicialização e I/O antes do rollout.
4. **Backend/QA:** rodar a 5192 candidata em ambiente isolado com banco, licença, aparelho, capa e arquivo sintéticos. Exercitar assinatura, sessão, catálogo, `coverId` válido/inválido, raw, ZIP múltiplo, grant de um uso, expiração, revogação/bloqueio, outro aparelho, outra sessão e queda de conexão. Registrar status, `code`, revisão, MIME, tamanho, SHA256 esperado/obtido, sem Bearer ou grant no documento. Os testes .NET de unidade/regressão passaram em 03/10/2026, mas não substituem este teste HTTP com banco.
5. **Operação:** preparar release imutável do código `1bfb619c21becffe40aaa597e100fcb3719c2e72`, registrar SHA256 do DLL candidato, backup restaurável de índice/configuração/banco afetado, migration necessária, alvo exato `turborama-station-api.service` e rollback para o DLL/índice anteriores. Validar os serviços compartilhados e o monitor de conteúdo já falho antes da janela. Implantar somente em tarefa de mudança de produção autorizada e conferida; não tocar 5190, 5191, 5194, PIX ou outros produtos. Depois, repetir as provas autenticadas na 5192 pública via HTTPS, não apenas `/ready/station`.

### 2. Android: integrar o cliente e instalar com segurança

1. **Android:** partir do código Java `0840028` (documento mais novo `f48399b`) e acrescentar a `StationApi.Grant` leitura **obrigatória** de `itemRevision` e todos os campos `artifact` do payload assinado. Comparar item/revisão com o catálogo da mesma sessão; exigir hash e tamanho esperados em `StationFiles`. Rejeitar descritor ausente, inválido ou formato não suportado. Preservar `StationConfig` (host, pin TLS, autoridade pública) e o alias do Keystore. Não trocar o código de ativação por `STA-`: esse prefixo identifica a licença, não a senha emitida.
2. **Android/nativo:** ligar `StationCoordinator` ao carrossel/catalog service e às telas reais, preservando seleção, texturas, jogos instalados, saves e emuladores. Usar `platform` somente por mapeamento explícito, inclusive edições BR; plataforma desconhecida não vira pasta por aproximação. Usar `coverId`/revisão no cache, carregar somente capas visíveis e tratar 404/429 sem tempestade de pedidos. Remover do APK final as chamadas antigas de catálogo, licença, telemetria e URLs de jogo que pertenciam ao frontend antigo, sem atingir redes internas dos emuladores.
3. **Android/instalador:** quando um jogo estiver ausente, autorizar uma vez, baixar sequencialmente para temporário privado com o **mesmo Bearer**, recusar 3xx/`Location` e Range, confrontar `Content-Length`, tamanho e SHA256, e só então processar `raw|zip|rar|7z`. Para compactados, limitar arquivos/tamanho extraído, rejeitar caminho absoluto, `..`, links e duplicatas, conferir `launchPath` e referências auxiliares como BIN/CUE. Usar publicação atômica e manifesto de arquivos: parcial, falha ou cancelamento nunca viram “instalado” nem substituem jogo/saves íntegros. Em queda após consumo, pedir outro grant; nunca repetir o GET consumido.
4. **Android/build:** substituir as classes antigas no APK completo; um AAR ou DEX isolado não é um APK pronto. Preservar pacote, assinatura, dados privados, emuladores e saves; inspecionar DEX/bibliotecas para garantir que só o cliente Station novo usa essas rotas. Testar com `adb install -r --no-incremental`, sem desinstalar/limpar dados, em aparelho de teste: ativação quando necessária, retomada por licença salva, `/me`, catálogo, capa, download, cancelar, abrir jogo, voltar, apagar somente arquivos do jogo e relançar offline conforme política definida. Registrar hash e versão do APK testado antes de promover.

### 3. Matriz mínima de provas para fechar este mesmo handoff

| Prova | Resultado de aceite | Situação em 03/10/2026 |
| --- | --- | --- |
| Autenticação | Challenge/complete com código sintético emitido, sessão 180 s, `/me` com nome, sessão vencida 401, aparelho/licença bloqueados negados. | Código revisado; não executado neste retorno com conta sintética. |
| Catálogo | HTTPS 200 autenticado, assinatura/keyId verificados, revisão, total e plataformas exatos; sem URL/caminho de jogo. | Logs têm três 200, sem payload sanitizado. |
| Capas | `coverId` real 200, MIME decodificável e SHA256 conferido; ID inexistente 404; mudança de revisão invalida cache. | Teste de biblioteca local; produção tem 48 respostas 404 e nenhum 200 observado. |
| Autorização | Payload `download-grant/v1` assinado inclui todos os campos da tabela; `itemRevision` coincide com catálogo; grant 60 s. | Implementado no Git, não implantado. |
| Bytes e erros | GET único 200 com Content-Length e SHA256 iguais ao descritor; segundo GET, expirado, outro aparelho/sessão e bloqueio negados; queda exige novo grant. | Não comprovado por HTTP autenticado. |
| APK | APK completo assinado e instalado sem perda de dados, interface nativa ligada, instalação segura e jogo abre; hash/versão documentados. | Biblioteca AAR/DEX e 142 checks do cliente; APK integrado pendente. |

Aceite final exige evidência separada de **desenvolvimento/homologação** e **produção** em cada linha. Atualizar esta tabela e os dados observados, sem criar outro handoff de retorno. Não publicar tokens, códigos comerciais, dados do comprador, URLs privadas, caminhos do armazenamento, nomes de jogos ou chaves privadas.

`/ready/station` 200 confirma apenas a prontidão atualmente implementada (incluindo migration 028); não verifica o índice inteiro, capas, chave de grants ou o fluxo de bytes. Não há evidência para declarar servidor ou APK prontos para produção agora. Este documento descreve a mudança necessária; não executa nem autoriza implantação ou restart.
