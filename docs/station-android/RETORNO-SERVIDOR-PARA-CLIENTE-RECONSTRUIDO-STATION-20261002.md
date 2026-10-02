# Retorno do servidor para a conexão do TurboStations Android

Data da apuração: 02/10/2026, aproximadamente 20h37 (America/Maceio).

## Referências e decisão

- Pedido atendido: [handoff do cliente reconstruído](https://github.com/luziellacerda/Servidor-pix/blob/7ac4fad9e0132db378f6e78e6494fedb08f614c3/docs/station-android/HANDOFF-CLIENTE-RECONSTRUIDO-STATION-20261002.md), branch `docs/cliente-reconstruido-station-20261002`, commit `7ac4fad9e0132db378f6e78e6494fedb08f614c3` do Servidor-pix.
- Cliente examinado: TurboElden, commit `0840028854034b03e5a1d3f2a162d66225932a6b`, branch `station-reconstrucao-20261002`. A biblioteca Java ainda não foi integrada ao APK distribuível nem consome o descritor novo.
- Código do servidor deste retorno: branch `feat/station-artifact-descriptor-20261002`, commits `96326aa0aeb164820cec26f8b5911fcdb47fc8ee` e `1bfb619c21becffe40aaa597e100fcb3719c2e72` (revisão final do código e testes), derivados de `b1159c9`.
- **Estado: código e contrato publicados para desenvolvimento; produção e APK ainda não liberados.** Não houve publicação de binário, alteração de índice real, migration, restart, mudança de porta ou alteração dos serviços PIX/Suite/ES. Não há prova de download autenticado completo na 5192.

## O que roda no Linux, separado do código novo

| Verificação em 02/10/2026 | Resultado observado |
| --- | --- |
| Serviço | `turborama-station-api.service` ativo; PID 281271; sem drop-ins. |
| Comando efetivo | `/usr/bin/dotnet /opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll`. |
| SHA256 do DLL efetivo | `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`. Este hash identifica o binário; não prova sozinho um commit de origem. |
| Escuta | `127.0.0.1:5192`, sem porta 5192 pública. O único endereço que o APK usa é `https://app.lzgames.com.br/v1`. |
| Sondas sem credencial | `/ready/station` local 200; `/v1/station/catalog` público 401; capa pública 401; grant inexistente 404. Essas respostas não comprovam conteúdo autenticado. |
| Logs desde o início do processo | `/me` 200 em 3 requisições e `/catalog` 200 em 3; `/covers` 404 em 48 e nenhum 200; nenhuma autorização/download 200. IDs e tokens não foram extraídos nem publicados. |
| Índice carregado | O arquivo configurado e seu conteúdo não puderam ser lidos nesta conta. Revisão, quantidade e valores de `platform` **não foram conferidos ao vivo**. |

O handoff anterior cita 996 jogos no índice e os identificadores `megadrive`, `snes`, `snesbr`, `gamegear`, `gb`, `sega32x`, `gbc`, `gba`; são dados documentais, não resultado de uma resposta autenticada capturada agora. O mesmo material cita 835 jogos SNES e 981 Mega Drive no disco; quantidade no disco não equivale ao catálogo. O limite atual da API é 4096 itens: a lista documental de 12.346 exige evolução explícita do contrato antes de caber no catálogo.

## Ativação, sessão e perfil: divergência resolvida no código

`STA-` é o prefixo do **licenseId** criado em `StationCommerceEndpoints.ProvisionAsync`: `STA-` seguido por 32 dígitos hexadecimais maiúsculos. Não é a senha que o comprador digita. `StationCommerceEndpoints` emite o `activationCode` com 32 bytes aleatórios codificados em Base64URL canônico sem `=` (43 caracteres). Exemplo **sintético e não válido comercialmente**: `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA`. O servidor verifica exatamente Base64URL canônico de 32 bytes em `StationService.Verifier`, tanto no pedido de challenge quanto no complete. Espaços, quebras, `=`, mudança de caixa e prefixo `STA-` não são normalizados. O cliente Java `token(code)` está alinhado com o formato do código emitido; o texto do handoff humano que chamava o código de `STA-` confundiu licença com código. Códigos comerciais já emitidos não foram alterados.

O challenge de ativação vale 60 s; o complete exige prova RSA-PSS do aparelho e o mesmo código/verificador. O challenge de sessão também vale 60 s; a sessão Bearer vale 180 s e está vinculada a licença, aparelho e revogação. `/v1/station/me` devolve payload assinado `profile/v1` com `displayName` (até 80 caracteres no retorno) e `profileVersion`, além de `licenseId`, `deviceId` e `sessionId`. Perfil ausente retorna 503 `STATION_PROFILE_NOT_READY`.

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

O `filePath` privado fica só no índice e no grant cifrado por AES-GCM; não aparece no catálogo, descritor ou cabeçalhos. O grant cifrado vincula caminho, revisão, hash, tamanho e data de modificação à licença, aparelho, **sessão**, item e grantId. O consumo é único. Ao consumir, o servidor compara o índice e a identidade do arquivo; mudança detectada nega com 404. O endpoint abre o arquivo antes do 200 e envia `application/octet-stream`, `Content-Length` exato, `Cache-Control: no-store`, sem URL, redirecionamento, `Content-Disposition` ou Range. Se a conexão cair depois do consumo, o cliente precisa pedir **nova autorização** e começar nova transferência; o grant anterior não é retomável.

Na carga do índice, o servidor verifica tamanho, assinatura binária e SHA256 de cada arquivo com descritor. Para ZIP também confere membros seguros, `launchPath`, total extraído e número de arquivos. A ferramenta `scripts/preparar-indice-artefatos.py` produz os descritores fora da requisição, inspeciona ZIP/RAR/7z, exige escolha explícita de `launchPath` quando há vários arquivos e escreve o novo índice com modo 0600. Exemplo de uso, com caminhos **locais e privados definidos pelo operador**, nunca pelo APK:

```text
python3 docs/station-android/scripts/preparar-indice-artefatos.py \
  --index INDICE_ATUAL.json --launch-manifest ESCOLHAS.json --output INDICE_NOVO.json
```

O manifesto é um objeto JSON `itemId` → `launchPath`, por exemplo `{"item-sintetico":"disc/game.cue"}`. Itens raw ou arquivos compactados de membro único dispensam escolha. RAR/7z têm membros conferidos pela ferramenta `7z` durante o preparo; a API confere assinatura, tamanho e SHA256 na carga, mas não reinspeciona membros desses dois formatos. O arquivo aprovado precisa permanecer imutável durante a validade do grant. Antes de implantação, validar que o armazenamento real aplica essa condição. A mudança impede ativar downloads novos em itens cujo índice ainda não tem descritor: eles retornam 503 `STATION_ARTIFACT_NOT_READY`. Por isso **não é seguro instalar este binário sobre o índice atual sem prepará-lo e validá-lo**.

Erros relevantes: 401 `STATION_SESSION_INVALID`; 403 `STATION_DEVICE_DENIED`; 404 `STATION_ITEM_NOT_FOUND` para ID fora do catálogo; 503 `STATION_ARTIFACT_NOT_READY` para metadados ausentes ou arquivo mudado; 404 `STATION_GRANT_NOT_FOUND` para grant inexistente, consumido, expirado, de outra sessão/aparelho/licença ou com vínculo divergente; 429 `STATION_RATE_LIMITED`; 400 para JSON/identidade malformados. Revogação/bloqueio continua governada pelo store de sessão e pelas regras comerciais existentes; falta prova com licença sintética no banco isolado.

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

`/ready/station` 200 confirma apenas a prontidão atualmente implementada (incluindo migration 028); não substitui essas provas. Não há motivo técnico demonstrado para declarar o servidor ou o APK prontos para produção agora. A próxima implantação precisa ter artefato de release identificado, índice preparado, validação das capas, migrações e rollback conferidos e testes ponta a ponta antes de trocar o processo efetivo. Este retorno não autoriza nem executa essa implantação.
