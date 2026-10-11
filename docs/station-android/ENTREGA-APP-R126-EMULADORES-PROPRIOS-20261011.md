# R126 — entrega única: Nintendo 64 / GameCube / Wii com emuladores próprios

## 1. Resultado e ação necessária

A R126 implementa a conexão Station `station-stream.v3` e mantém o gameplay no emulador próprio de cada família: Mupen64Plus AE para Nintendo 64; Dolphin para GameCube e Wii. A UI, o renderer e a entrada de controles desses emuladores permanecem seus. O servidor transporta os dados entre os participantes; não emula o jogo e não precisa interpretar SN64/DPL1.

**Ação do servidor:** cadastrar exatamente os três registros abaixo, associar cada edição/modo real aos perfis de controles correspondentes e confirmar a resposta assinada v3. O operador de cadastro já existe no código examinado. Não é necessária uma nova API pública nem um reinício para publicar os perfis v3 nesta implementação.

Fonte executável publicada: [`efd9878df3e87ecf22d8fbddeddd30baccb8d3dc`](https://github.com/luziellacerda/TurboElden/commit/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc), branch `fix/station-carousel-diagonal-online-compact-r123-20261011`. Os dois deltas R125/R126 estão nesse commit; o alvo atual é o APK integrado R126.

APK final `TurboStations-Premium-R126-20261011.apk`: **SHA-256 `f2924c7b49ea4e40f67c41e0f2997ea4dcd5aee6f2d495517b3c5dfa3c664a9d`, 2.233.970.789 bytes**. Assinatura v2/v3 com o certificado original e alinhamento 16 KiB verificados pelas ferramentas reais; 13.544 entradas preservadas por hash. Recibo: [package-final-r126.json](https://github.com/luziellacerda/TurboElden/blob/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc/versions/station-dolphin-online-r126-20261011/evidence/package-final-r126.json). Esta entrega não confirma instalação, ativação em produção nem gameplay físico. O APK não é publicado no Git.

Base examinada do servidor: `141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc`; a existência de código no Git não comprova a versão do processo ativo.

## 2. Identidades finais, sem aliases

| Sistema | `engineId` final | `controllerProfile` | Teto técnico |
|---|---|---|---|
| Nintendo 64 | `mupen64plus-ae-33bf7021-n64-v3-1aafea201355` | `direct-four-ports-v1` | 4 |
| GameCube | `dolphin-5102a033-gamecube-v3-ca43a338d799` | `dolphin-gamecube-four-ports-v1` | 4 |
| Wii | `dolphin-5102a033-wii-v3-ca43a338d799` | `dolphin-wii-remotes-four-v1` | 4 |

Todos usam `recoveryProtocol=station-stream.v3` e `transport=relay-wss-v3`. O teto do motor não concede quatro vagas a todo jogo: `allowedPlayerCounts` e `maximumPlayers` continuam sendo os do modo aprovado daquela edição.

Nintendo 64:

- Core `libstation_mupen64plus_core.so`: `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8`.
- Runtime `libstation_mupen64plus_runtime.so`: `1aafea201355b58909ada9958bbe5bb5407e7ace7eb3848ade7ba04d235becac`.
- `profileSha256`: `2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c`.
- Formatos: `z64`, `n64`, `v64`, `rom`.
- O ID R125 `mupen64plus-ae-33bf7021-n64-v3-500d48cd10f3` foi supersedido: o core/plugins são iguais, mas o marcador agora amarra o conjunto produtivo R126, incluindo Java/DEX/manifesto. Não reutilizar aquele ID para estes bytes. Registros antigos podem ser preservados para versões/salas antigas; o cliente R126 não os seleciona como alias.

GameCube e Wii compartilham os bytes abaixo; seus IDs, plataformas e perfis são distintos:

- Core `libstation_dolphin_online.so`: `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d`.
- Runtime `libstation_dolphin_runtime.so`: `ca43a338d7992e2d9ef298387822c860945fbb16dc11dff03ee5b1d1fcd27656`.
- GameCube `profileSha256`: `89dc579c37afab7731a2d7322ab7fcf70ed0afa86ba7e491a605c45cd5443fc1`.
- Wii `profileSha256`: `cf3b214d63ce324a82b331fc96a95c6f35e8cc1c7b6bc2bcd659cf35743ceaed`.
- GameCube: `iso`, `gcm`, `gcz`, `ciso`, `wia`, `rvz`; Wii: `iso`, `gcz`, `ciso`, `wbfs`, `wia`, `rvz`. Não conceder esses motores a `elf`, `dol`, `wad`, `zip`, `nsp` ou `xci`.

Dados completos e dependências exatas: [runtime-identity-r126.json](https://github.com/luziellacerda/TurboElden/blob/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc/versions/station-dolphin-online-r126-20261011/evidence/runtime-identity-r126.json) e [engines.json do APK](https://github.com/luziellacerda/TurboElden/blob/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc/versions/station-dolphin-online-r126-20261011/assets/station-online/engines.json). O asset completo tem SHA-256 `d80d8c6c7448488b8e11917cfeb0681d50b48b4dd81266b0182c67bd85f91f48`, 17.482 bytes. O manifesto de cadastro servidor está separado em [server-engine-additions-r126.json](https://github.com/luziellacerda/TurboElden/blob/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc/versions/station-dolphin-online-r126-20261011/evidence/server-engine-additions-r126.json).

## 3. Canonização obrigatória dos controles

`profileSha256` é SHA-256 dos bytes UTF-8 do JSON compacto, nesta ordem, **sem newline final**:

```text
{"schemaVersion":1,"controllerProfile":"<nome exato>","devices":[1,1,1,1],"coreOptions":"<OPTIONS exatas, LF escapados em JSON>"}
```

As strings OPTIONS estão completas no JSON de cadastro e no recibo. Cada linha `key=value` termina com LF, inclusive a última. Não reordenar, aparar ou substituir opções para obter outro hash.

O manifesto do **cliente** usa `controllerProfiles` como lista de nomes. O operador do **servidor** exige definições com `controllerProfile`, `maximumPlayers` e `configuration`; por isso deve receber o JSON de cadastro dedicado, não o asset completo do APK. As configurações incluídas nesse JSON produzem exatamente os três hashes desta entrega.

O perfil Wii representa quatro controles emulados Dolphin e os estados oficiais de acessórios configurados. O transporte oficial serializa botões, aceleração, câmera, estado opcional MotionPlus e tipo/estado da extensão. Não foi aplicada uma restrição arbitrária a `Extension=None`; isso não homologa todos os jogos, modos ou controles físicos. A aprovação continua por edição e modo.

## 4. Cadastro de edições/modos: ajuste concreto

O código atual do servidor já aceita esses nomes de perfil: valida identidade textual, capacidades, hashes e vínculo exato; não possui whitelist que obrigue recompilar para os nomes Dolphin.

Há, porém, uma diferença de dados a resolver antes do cadastro:

- Os padrões preparados no servidor usam `gamecube-four-ports-v1` e `wii-four-remotes-v1`.
- Esta implementação usa **`dolphin-gamecube-four-ports-v1`** e **`dolphin-wii-remotes-four-v1`**.
- `profile_configuration()` procura a configuração pelo nome exato. Um modo com o nome antigo não cria vínculo para o novo motor; não substitua os nomes finais por aliases.

Para cada edição GameCube/Wii a liberar, preparar um registro explícito em `preparedModes` com `itemId`, `contentSha256`, `platform`, `profileId`, `mode`, `modeTitle`, `instructions`, `sources`, `maximumPlayers`, `allowedPlayerCounts`, `approved` e o nome Dolphin correspondente. Preservar a capacidade e o modo realmente documentados. Não copiar cegamente `[2,3,4]` para todos os títulos e não trocar uma recusa por aprovação automática. Jogos de um jogador continuam com `maximumPlayers=1`, `allowedPlayerCounts=[]`; um modo de duas entradas continua com `[2]`.

Os modos Nintendo 64 existentes podem ser vinculados ao novo ID com o mesmo `direct-four-ports-v1` e as OPTIONS N64 finais. O código examinado já contém aprovações específicas de quatro controles para Bomberman 64/BATTLE, Mario Kart 64/VS ou BATTLE, F-Zero X/VS BATTLE e Mario Tennis/EXHIBITION em duplas. Confirmar a edição/hash atual antes de reaproveitar essas aprovações.

Uma alteração de conteúdo exige um novo vínculo explícito. O importador verifica extensão do artefato, identidade, motor e nome de perfil; uma descrição `players=4` no catálogo não substitui aprovação de modo. Os perfis anteriores devem permanecer byte-equivalentes; o operador adiciona novos vínculos sem modificar salas existentes.

Fontes exatas examinadas:

- [station_online_profiles.py:44–59, 118–155](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/docs/station-android/scripts/station_online_profiles.py#L44): configurações por nome, canonização e vínculo a edição/modo.
- [StationMultiplayer.cs:76–109](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/src/TurboRamaSuiteOnlineServer/StationMultiplayer.cs#L76): validação completa e troca preservando perfis congelados das salas.
- [autorizar-modos-online-plataformas.py:20–24](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/docs/station-android/scripts/autorizar-modos-online-plataformas.py#L20): modos N64 específicos já classificados.

## 5. Operador existente: sintaxe conferida, execução somente no servidor

Arquivo: [cadastrar-motor-online-station.py](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/docs/station-android/scripts/cadastrar-motor-online-station.py#L117).

O operador deve resolver os caminhos absolutos usando sua configuração ativa: `outputDirectory/index.json`, `autoOnlineProfiles.registry`, `autoOnlineProfiles.engineManifest` e `autoOnlineProfiles.preparedModes`. Não há caminhos privados ou credenciais nesta entrega. As chamadas seguintes mostram apenas a sintaxe; os marcadores de caminho precisam ser substituídos pelos caminhos absolutos efetivos.

Preparação sem publicação:

```bash
python3 /CAMINHO/cadastrar-motor-online-station.py prepare \
  --catalog /CAMINHO/index.json \
  --profiles /CAMINHO/perfis-v3.json \
  --current /CAMINHO/manifesto-motores-v3.json \
  --modes /CAMINHO/modos-preparados.json \
  --incoming /CAMINHO/server-engine-additions-r126.json \
  --artifacts /CAMINHO/artefatos-r126-arm64 \
  --output /CAMINHO/candidato-r126-novo
```

Publicação administrativa do candidato conferido:

```bash
pkexec python3 /CAMINHO/cadastrar-motor-online-station.py apply \
  --config /CAMINHO/configuracao-importador-ativa.json \
  --candidate /CAMINHO/candidato-r126-novo
```

Alternativa atômica existente, preparação + publicação na mesma chamada:

```bash
pkexec python3 /CAMINHO/cadastrar-motor-online-station.py install \
  --config /CAMINHO/configuracao-importador-ativa.json \
  --incoming /CAMINHO/server-engine-additions-r126.json \
  --artifacts /CAMINHO/artefatos-r126-arm64 \
  --output /CAMINHO/candidato-r126-novo
```

`output` deve ser uma pasta nova. `artifacts` deve conter os basenames das bibliotecas declaradas, extraídos do **mesmo APK assinado final**; os dois registros Dolphin compartilham seus dois arquivos. O script exige arquivos regulares limitados, verifica core/runtime reais, imutabilidade dos IDs, baseline de catálogo/perfis/modos, selo e recomputação do candidato. Somente estes três motores entram em `incoming`: o manifesto completo inclui outros motores e uma entrada inativa, que não são uma nova entrega deste cadastro.

O script valida core/runtime, não todos os `dependencySha256`; conferir também as dependências declaradas contra o recibo do APK final antes da publicação. Não modificar opções, plugins ou bibliotecas após calcular esses hashes. O servidor não executa esses binários; eles são prova administrativa do conjunto entregue.

O `apply` exige administração Linux local autenticada (`geteuid=0`, `PKEXEC_UID=1000`), usa `scan.lock`, salva o registro anterior, publica por arquivo temporário/fsync/rename e rejeita baseline que mudou. Recibo esperado: `applied=true`, `serverRestarted=false`, `reloadWithinSeconds=10`, hashes dos arquivos e quantidade de perfis adicionados/preservados. Se a baseline mudou, preparar novamente a partir dos arquivos correntes.

O monitor lê o registro v3 a cada dez segundos e mantém o último registro válido se a nova publicação for inválida: [StationMultiplayerProfileMonitor.cs:20–64](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/src/TurboRamaSuiteOnlineServer/StationMultiplayerProfileMonitor.cs#L20). Não introduzir os IDs v3 no registro legado `Station:Online:EngineRegistryFile`: o código legado admite `station-stream.v2` e captura seus motores na inicialização. O cadastro v3 funciona pelos perfis exatos e não precisa desse reinício legado.

## 6. Prova de liberação correta: resposta assinada v3

Depois de publicar, obter pelo fluxo autenticado do aplicativo uma resposta **nova**, com prova de requisição e assinatura de resposta verificada, de:

```text
POST /v1/station/online/multiplayer/command
action=capabilities ou snapshot
itemId=<edição em conferência>
clientMaximumPlayers=5
```

O envelope deve ter o `requestId` novo solicitado e `snapshot.multiplayerVersion=3`, `snapshot.capability=station-multiplayer.v3`. Em `snapshot.profiles`, o vínculo da edição deve trazer **cada valor exato**: `itemId`, `contentSha256`, `platform`, `engineId`, `coreSha256`, `runtimeSha256`, `profileId`, `profileSha256`, `controllerProfile`, `approved`, `maximumPlayers`, `allowedPlayerCounts` e o modo correspondente. `platformPolicy.serverReady=true` sozinho não concede admissão; `onlineAvailable` deve corresponder a um perfil aprovado desta edição.

Uma **sala nova** desse vínculo deve apresentar `room.transport=relay-wss-v3`, `room.recoveryProtocol=station-stream.v3`, identidade/hash/perfil iguais, `capacity` permitida, roster com slots distintos e os links host-star próprios da geração. O ticket usa `/v1/station/online/multiplayer/relay` e subprotocolo WebSocket `station-stream.v3`, preservando autenticação/assinatura/leases.

Não usar `HTTP200`, PONG, cache, um documento no Git ou o teto da plataforma como prova de cadastro. Não existe GET snapshot público a inventar. A resposta do endpoint **legado** `POST /v1/station/online/command` contém `snapshot.engines` e `recoveryCapabilities` legados/v2 no código examinado; não exigir que ela apresente estes motores v3. A prova que este app consome para os novos motores é `snapshot.profiles` do endpoint multiplayer assinado.

Código dos endpoints: [StationMultiplayerEndpoints.cs:10–36 e 42–55](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/src/TurboRamaSuiteOnlineServer/StationMultiplayerEndpoints.cs#L10). Conteúdo v3 e roster: [StationMultiplayer.cs:236–268](https://github.com/luziellacerda/Servidor-pix/blob/141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc/src/TurboRamaSuiteOnlineServer/StationMultiplayer.cs#L236).

Retorno único solicitado: commit/código do operador efetivamente usado; SHA dos registros publicados; recibo de publicação/reload; vínculo sanitizado dos três motores e pelo menos uma edição/modo aprovado por sistema; resposta assinada fresca conferida sem sessão/tokens/identificadores pessoais; horário e identificação da release/processo ativo. Se faltar conteúdo/modo real para um sistema, informar exatamente o item e o vínculo ausente, mantendo-o pendente.

## 7. Recuperação, proteção e limites reais

Station transporta os protocolos nativos por links v3 existentes: N64 multiplexa TCP/UDP SN64; Dolphin encapsula datagramas ENet DPL1 com confirmação. Pausa/ACK são feitos no motor real, com epoch/revision e barreira depois de `CORE_RUNNING`; o READY anterior ao boot não autoriza rodar quadros. Não existe um segundo loop RetroArch para estes três motores.

Na perda de rede, a sessão pausa e aguarda recuperação. Retomar exige geração/epoch/revision atuais e todos os vínculos exigidos. Shutdown é cancelável e saída normal é autorizada por ação humana; retorno de Activity ou erro nativo não simula esse comando. Um desync real permanece como falha/pausa para análise. Essa recuperação de transporte **não é rollback** e não garante resolver uma divergência de emulação.

Dolphin mantém opções fixas e sincronização oficial host→clients, controles da UI original, saves em diretório privado de sessão e lib online separada. N64 mantém plugins/core offline originais e sua variante online isolada. Bibliotecas offline, mídia, lógica do carrossel e as preservações de consumo não foram modificadas por esta R126. O validador Android também verifica GameCube nativo `platform=0` e Wii `platform=2`, rejeitando catálogo que rotule um disco como o sistema errado.

O conjunto preserva **dez registros de outros motores** byte-equivalentes; não migra todos os emuladores para essa arquitetura. SNES/MD/PSX/arcades/NeoGeo CD continuam nos caminhos online anteriores. Dreamcast, PS2, Saturn, Switch, Wii U e demais famílias ainda precisam de adaptação nativa própria; um flag do servidor não cria essa integração. Ver [OTHER-SYSTEM-EMULATORS-AUDIT.md](https://github.com/luziellacerda/TurboElden/blob/efd9878df3e87ecf22d8fbddeddd30baccb8d3dc/versions/station-dolphin-online-r126-20261011/docs/OTHER-SYSTEM-EMULATORS-AUDIT.md).

As evidências são de compilação, validação de identidade/preservação e provas isoladas. Não há alegação de sala real com dois/quatro aparelhos, recuperação física prolongada, compatibilidade universal ou redução térmica medida desta R126. Após cadastro confirmado, o mantenedor testa **sala nova**, primeiro dois aparelhos, depois três/quatro somente em modos aprovados.

## Cadastro anexo neste repositório

`r126/server-engine-additions-r126.json` contém somente os três registros finais prontos para o operador. SHA-256 literal: `7dd1fad9acb1d17a50a9004964e7d620d261f025de8c5e2bda35a6f9d33f844f`. Não contém jogos, binários ou dados de sessão. Usar as bibliotecas extraídas privadamente do APK final como prova administrativa dos hashes.
