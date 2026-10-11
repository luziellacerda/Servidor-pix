# Handoff único dos motores online da TurboStations R128

Destinatário: mantenedor do Servidor-pix. Aplicar nesta rodada o cadastro v3 dos quatro motores abaixo, publicar os modos comprovados do catálogo atual e devolver uma matriz única de liberação. O app candidato já contém as rotas reais e consome os perfis assinados pelo contrato existente. PS3 e PS4 são exclusivamente offline e não fazem parte deste cadastro.

Fonte executável Android: `3ea94ade312640ab69ca09e4092f60607996ccba` no TurboElden. APK candidato SHA256 `0189527c7537845562a2be4be15c0fab976c85f4f9c7f8ab884915c8bc3052dc`, 2234617901bytes, assinatura original v2/v3 e alinhamento16KiB conferidos. Código servidor auditado: `141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc`. A entrega não instala o app, modifica produção ou comprova gameplay.

## Correção do cadastro anterior

Esta entrega substitui o procedimento de cadastro da entrega R127 `5696ed9bed01fdfa8cbd3414197c2eea4f3fc898`. O JSON legado de quatro campos OnlineEngine e `Station:Online:EngineRegistryFile` não autorizam estes motores v3. Usar `autoOnlineProfiles.engineManifest`, `.registry` e `.preparedModes`. Não exigir os novos motores em `snapshot.engines` do endpoint legado nem reiniciar o serviço para uma atualização de dados v3.

O monitor de perfis v3 recarrega o registro em até dez segundos e preserva os vínculos das salas existentes. Essa recarga é do JSON C#, não do código Python. No código auditado, o scanner é Type=oneshot e importa prepare a cada execução; não há worker Python contínuo desse módulo. O operador deve confirmar Type/ExecStart/estado/PID atuais e publicar os scripts no library-tools efetivamente indicado por ExecStart, conferindo module.__file__ e SHA. Impedir novos disparos do timer, aguardar o scanner atual terminar sem matá-lo, publicar/verificar os scripts em processo Python novo e restaurar o timer. Não reiniciar a API por consequência dessa atualização. Os recibos de serviço existentes são históricos; não descrevem produção observada hoje.

Se o featureflag v3 não estiver efetivo, o operador deve registrar a configuração e o procedimento real dessa ativação; isso é distinto do falso requisito de restart do registro legado.

## Identidades do APK final

| Sistema | Engine | Core SHA256 | Runtime SHA256 | Controller | Canonical SHA256 | Máximo do motor |
| --- | --- | --- | --- | --- | --- | --- |
| PlayStation1 | `pcsx-rearmed-ff81ed17-player-v3-bdd07e7085ca` | `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9` | `bdd07e7085caa79cb8e041e4f943869812f59639650c559dac8afb9f3392e9c5` | `standard-2p-v1` | `81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a` | 2 |
| Nintendo64 | `mupen64plus-ae-33bf7021-n64-v3-bceb1e9a55af` | `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8` | `bceb1e9a55af41c2363b518ee09c1e45d171b7ba2f6a5a5628ea24553cc126bd` | `direct-four-ports-v1` | `2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c` | 4 |
| GameCube | `dolphin-5102a033-gamecube-v3-ff6109c31583` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `ff6109c31583368928f796094cf3429e86471f71336ecfe0373ad55af433ad3d` | `dolphin-gamecube-four-ports-v1` | `89dc579c37afab7731a2d7322ab7fcf70ed0afa86ba7e491a605c45cd5443fc1` | 4 |
| Wii | `dolphin-5102a033-wii-v3-ff6109c31583` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `ff6109c31583368928f796094cf3429e86471f71336ecfe0373ad55af433ad3d` | `dolphin-wii-remotes-four-v1` | `cf3b214d63ce324a82b331fc96a95c6f35e8cc1c7b6bc2bcd659cf35743ceaed` | 4 |

O máximo da engine é apenas teto. A capacidade da sala vem de item/edição/modo aprovado, não do teto da plataforma. N64/GameCube/Wii mantêm cores e opções R126/R127; PSX mantém PCSX-ReARMed normal e LibretroPlayer original. A R128 renova apenas os marcadores que vinculam Manifest/DEX/biblioteca compartilhados às adições offline. Não substituir identidades históricas ou trocar os binários normais.

Os registros completos estão em [incoming-engine-manifest.json](incoming-engine-manifest.json); [runtime-final.json](runtime-final.json) e [package-final.json](package-final.json) comprovam o pacote correspondente. `controllerProfiles` do manifesto administrativo são objetos com configuration, e não o array de nomes do asset Android. `library`/`runtimeLibrary` são basenames reais. As configurações são JSON UTF8 compacto, ordem schemaVersion/controllerProfile/devices/coreOptions, sem BOM ou LF final; o texto das opções conserva seu LF final escapado. Não aparar ou reordenar.

## Implementações Android que o servidor atende

N64 executa Mupen64Plus AE com seus controles/render originais, transportando SN64. GameCube e Wii executam Dolphin e seu RunNetPlay original, transportando DPL1. PSX executa o Player normal e o core normal PCSX-ReARMed ff81ed17, transportando PXL1. Esses quatro caminhos não abrem StationRetroActivity. O relay transporta os bytes opacos TSR3; não executa emuladores.

PSX usa processo privado, ABI/GOT validada para libmain exata, SDL/render/input originais, duas portas Standard, HLE e delay4. Fontes originais do Player não foram recuperadas; o adaptador foi compilado e guarda os hashes do owner binário. Não extrapolar a integração para multitap, analog ou lightgun. Conteúdo, Binder, tickets, COMMIT após execução real, pausa por epoch/serial e encerramento somente humano permanecem preservados.

SNES/Mega Drive foram preservados por pedido do mantenedor. Outros sistemas não são declarados migrados por este handoff. As adições PS3/PS4 offline não geram registro online, credencial ou modalidade de sala.

## Patch administrativo entregue

O pacote [operator/](operator/) inclui os dois patches contra `141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc`, geradores de manifesto/modos, extrator privado e testes isolados. A mudança fica em `docs/station-android/scripts/station_online_profiles.py` e `cadastrar-motor-online-station.py`; não altera API, relay ou segurança do serviço. Quando os dois arquivos já vierem atualizados no commit entregue, não reaplicar os patches.

Primeiro, `requireExplicitEngineModes:true` nos quatro engines exige modo preparado com `engineIds` que contenha a engine exata. Sem modo explícito, nenhum vínculo é criado para um jogo novo. O campo opcional ausente mantém a política dos engines históricos. Um modo novo escopado não modifica o modo histórico nem é copiado ao perfil público/wire.

Segundo, o operador aceita `--mode-additions` com a sequência antiga integral seguida das adições escopadas. O candidato sela engines, profiles e modes, preservando os hashes de baseline antigos. No apply, revalida catálogo, baselines, selo, bytes reais, scopes e resultado recalculado sob `scan.lock`; publica modos e manifesto antes dos perfis. Em falha, restaura os três baselines byte a byte. Não editar preparedModes antes de preparar o candidato, pois isso invalidaria o baseline e exigiria outra rodada.

Aplicar e conferir o patch sobre os arquivos exatos auditados; não substituir um arquivo remoto que mudou depois sem comparar o diff. Os testes entregues cobrem preservação da política antiga, recusa de engine/mode desconhecidos, idempotência, hashes, escopo, selo/baseline e rollback nas três posições de falha. São testes administrativos isolados, não partidas Android.

## Jogos e modos permitidos

`merge_scoped_modes.py` conserva as linhas antigas e copia para os IDs finais somente aprovações N64/GameCube/Wii com item, conteúdo, core, canonical controller, capacidade e fontes HTTPS exatos. Cada cópia usa profileId determinístico próprio e o modo/capacidades documentados do original. Não assumir que a R126 foi ativada; se não houver perfil nativo equivalente, usar a revisão explícita do operador. Nomes padrão genéricos GameCube/Wii não são aliases dos perfis `dolphin-*` exigidos pelo app.

PSX requer ledger explícito por edição/content/mode, HLE compatível e dois controles humanos simultâneos. `approved:true` exige `biosPolicy=HLE-per-game-approved`, fontes e reviewEvidence HTTPS, maximum2 e allowed[2]. Aprovação de um core RetroArch antigo não é aprovação do Player/HLE atual. Jogos por turnos compartilhando um controle não viram duas portas simultâneas. Não fabricar itemId, hash, teste HLE ou fonte para concluir o cadastro. Devolver nesta rodada a lista dos aprovados e dos não aprovados com motivo.

CHD/PBP/N64/Dolphin usam a identidade do arquivo real de lançamento. CUE exige `cue-set-v1` com CUE e todas as faixas; SHA do ZIP ou do CUE isolado não serve. `station_content_sets.py` e StationContentIdentity já compartilham essa canonização. Não precisa endpoint novo.

## Procedimento único do operador

## Valores que o operador obtém da configuração ativa

Os nomes de variável abaixo são parâmetros de receita; não representam caminhos de produção inventados:

- `SERVER_OPERATOR`: caminho absoluto do operador atualizado `cadastrar-motor-online-station.py`.
- `PROFILES_MODULE`: caminho absoluto do módulo atualizado `station_online_profiles.py`.
- `TOOLS`: diretório absoluto dos três helpers e dos testes publicados.
- `DELIVERY`: diretório absoluto dos recibos finais reais `runtime-final.json` e `package-final.json`.
- `CONFIG`: arquivo absoluto de configuração administrativa ativa.
- `CATALOG`: `outputDirectory/index.json` dessa configuração.
- `PROFILES`: `autoOnlineProfiles.registry` da mesma configuração.
- `ENGINES`: `autoOnlineProfiles.engineManifest` da mesma configuração.
- `MODES`: `autoOnlineProfiles.preparedModes` da mesma configuração.
- `PREPARATION`: diretório administrativo de trabalho; arquivos de revisão não substituem os ativos.
- `CANDIDATE`: diretório absoluto novo, ainda inexistente, para um candidato desta publicação.
- `ARTIFACTS`: diretório privado, efetivamente disponível ao operador, contendo as seis bibliotecas do APK final e seu recibo de extração.
- `REVIEW_LEDGER`: revisão real dos modos/edições identificados no catálogo atual. Não usar o ledger de teste.

Os registros protegidos ativos permanecem dentro de `outputDirectory`, conforme validação do operador existente. A receita não fornece credenciais nem troca permissões.

## 1. Gerar dados administrativos dos recibos finais

```sh
python3 "$TOOLS/build_incoming_manifest.py" \
  --runtime-receipt "$DELIVERY/runtime-final.json" \
  --package-receipt "$DELIVERY/package-final.json" \
  --output "$PREPARATION/incoming-engine-manifest.json" \
  --profile-receipt "$PREPARATION/profile-canonical-receipt.json"
```

O helper exige exatamente N64, GameCube, Wii e PSX, as quatro políticas/canonicais existentes, `station-stream.v3`, `launchReady:true`, capacidade 4/4/4/2 e IDs vinculados ao hash final de runtime. `controllerProfiles` vira definição de objeto com configuração exata; `requireExplicitEngineModes:true` fica persistido. Nenhum jogo recebe aprovação nesta etapa.

Canonização: UTF-8 compacto, ordem `schemaVersion/controllerProfile/devices/coreOptions`, sem BOM ou LF depois do JSON; a string OPTIONS mantém seu LF final. Não aparar, reordenar ou aliasar os nomes de controle.

## 2. Preparar o kit privado de bytes reais

Esta etapa ocorre onde o APK final assinado está disponível. `APK_FINAL` é seu caminho real. A extração não instala, executa ou envia binários:

```sh
python3 "$TOOLS/extract_private_server_artifacts.py" \
  --apk "$APK_FINAL" \
  --package-receipt "$DELIVERY/package-final.json" \
  --runtime-receipt "$DELIVERY/runtime-final.json" \
  --output "$KIT_PRIVADO_ATUAL"
```

O helper verifica tamanho/SHA integral do APK, selo do recibo de runtime quando presente, recibos de assinatura v2/v3 e alinhamento 16KiB, seis core/runtime e todas as dependências do mesmo APK. Copia somente as seis bibliotecas; não copia dependências, jogos, BIOS, saves, logs ou licenças. Antes de substituir um kit existente, todos os bytes e hashes precisam passar. A validação criptográfica da assinatura é a do recibo final; o helper não substitui o apksigner.

O kit contém as seis basenames declaradas por `library/runtimeLibrary` e `artifact-kit-receipt.json`; pode receber também os dois JSONs administrativos acima. O operador deve confirmar recebimento privado e disponibilidade local de `ARTIFACTS` antes de preparar o cadastro. Não declarar que o servidor já tem os binários por eles constarem no Git: não constam.

## 3. Mesclar modos atuais e decisões explícitas

```sh
python3 "$TOOLS/merge_scoped_modes.py" \
  --catalog "$CATALOG" --profiles "$PROFILES" --modes "$MODES" \
  --runtime-receipt "$DELIVERY/runtime-final.json" \
  --profiles-module "$PROFILES_MODULE" \
  --decisions "$REVIEW_LEDGER" \
  --output "$PREPARATION/prepared-modes-merged.json" \
  --audit-json "$PREPARATION/scoped-mode-audit.json"
```

O helper conserva o prefixo integral dos modos atuais e não edita o catálogo/perfis/manifesto ativos. Só migra automaticamente aprovações N64/GC/Wii com item/conteúdo/plataforma, core, controller e canonical de configuração exatamente iguais, capacidade compatível e fontes HTTPS. Preserva modo e contagens, inclusive `[2,4]` quando essa for a aprovação existente. Cria apenas novo ID de vínculo, determinístico e limitado; não inventa um modo jogável.

PSX nunca herda aprovação antiga por inferência. O ledger é `{"schemaVersion":1,"modes":[...]}`. Cada decisão precisa dos campos reais `itemId/contentSha256/platform/profileId/maximumPlayers/allowedPlayerCounts/approved/controllerProfile/mode/modeTitle/instructions/sources`. Para uma aprovação PSX, exigir também `biosPolicy:"HLE-per-game-approved"`, `reviewEvidence` HTTPS, máximo2 e contagens `[2]`. Se o ledger declarar `engineId/coreSha256/runtimeSha256`, devem coincidir com o motor final. Registrar os dois controles humanos simultâneos e a compatibilidade HLE da edição; sem multitap, analog ou lightgun. CUE precisa de identidade completa `cue-set-v1` no catálogo qualificado.

Sem ledger, pode omitir `--decisions`; PSX permanece pendente. Isso não é ativação PSX concluída. O relatório precisa listar as edições liberadas/recusadas e as lacunas reais nesta mesma resposta, com IDs/hashes reais extraídos pelo operador. Desconhecidos futuros não recebem aprovação genérica.

## 4. Preparar e aplicar o MESMO candidato

```sh
python3 "$SERVER_OPERATOR" prepare \
  --catalog "$CATALOG" --profiles "$PROFILES" --current "$ENGINES" \
  --modes "$MODES" \
  --mode-additions "$PREPARATION/prepared-modes-merged.json" \
  --incoming "$PREPARATION/incoming-engine-manifest.json" \
  --artifacts "$ARTIFACTS" --output "$CANDIDATE"

pkexec python3 "$SERVER_OPERATOR" apply \
  --config "$CONFIG" --candidate "$CANDIDATE"
```

`--modes` é SEMPRE o registro ativo usado no baseline. Não passar o arquivo merged como `--modes`: ele entra em `--mode-additions` e fica selado em `modes.json` no candidato. A preparação sela `incoming.json/engines.json/profiles.json/modes.json`, copia/verifica os core/runtime reais e preserva os perfis existentes.

A aplicação revalida os quatro baselines ativos, os quatro selos do candidato, os bytes reais e a recomputação completa. Modos appended só podem escopar IDs do incoming desta entrega; prefixos anteriores não podem ser alterados. Sob o `scan.lock` existente, publica modos, manifesto e perfis por último. Falha de publicação restaura os três arquivos byte a byte; tentativa de rollback falha exige recuperação administrativa explícita. Não ignora baseline mudado: preparar novamente com o catálogo atual.

A autenticação local nativa do operador é preservada (`geteuid()==0` e `PKEXEC_UID` real esperado). Não forjar essa variável. `pkexec` é executado pelo operador humano autorizado, fora deste acompanhamento. Se leitura dos registros protegidos também exigir esse acesso, o operador usa a mesma administração nativa para a preparação.

Alternativa administrativa em um comando, depois de revisão dos dados:

```sh
pkexec python3 "$SERVER_OPERATOR" install \
  --config "$CONFIG" \
  --incoming "$PREPARATION/incoming-engine-manifest.json" \
  --artifacts "$ARTIFACTS" \
  --mode-additions "$PREPARATION/prepared-modes-merged.json" \
  --output "$CANDIDATE"
```

Não recompila API, reinicia o serviço nem modifica o registro legado `EngineRegistryFile`. O monitor v3 já existente recarrega o registro de perfis em até dez segundos. Isto pressupõe que a implantação v3 esteja ativa; Git/recibo de aplicação sozinho não prova a resposta que o app recebe.

## Reproduzir os testes isolados

```sh
export STATION_SERVER_REPO="$REPO_SERVIDOR_EXISTENTE"
export STATION_FINAL_RUNTIME_RECEIPT="$DELIVERY/runtime-final.json"
export STATION_PROFILES_MODULE="$PROFILES_MODULE"
export STATION_OPERATOR_SCRIPT="$SERVER_OPERATOR"
python3 "$TOOLS/test_explicit_engine_modes.py"
python3 "$TOOLS/test_scoped_mode_merge.py"
python3 "$TOOLS/test_operator_candidate_transaction.py"
python3 "$TOOLS/test_private_artifact_kit.py"
```

São 68 métodos de teste isolados: 24 seleção/legado,16 merge conservador,20 candidato/publicação/rollback e8 privacidade/integridade do kit. O original141b é lido pelo Git local, sem fetch/checkout/mutação. Os testes usam arquivos temporários e valores sintéticos; não aprovam jogos. Nos testes de publicação, credenciais Linux/grupo/flock/fsync são substituídos para conferir a lógica em Windows: não validam ACL Linux, lock concorrente, produção ou gameplay. Os testes ZIP não validam criptograficamente assinatura APK. O recibo final R128 precisa ser fornecido para reexecutar a canonização da entrega final; o ensaio inicial R127 é histórico; os 68 métodos foram reexecutados com o runtime final R128 desta entrega e passaram.

## Confirmação assinada e matriz de conclusão

Usar `POST /v1/station/online/multiplayer/command`, action capabilities/snapshot, itemId real, clientMaximumPlayers5 e requestId UUID novo. Validar assinatura e vínculos license/device/session/requestId antes de consumir payload. Verificar multiplayerVersion3, capability station-multiplayer.v3, instance/revision atuais e perfil aprovado com item/content/engine/core/runtime/profileId/profileSha/modo/capacidades exatos. HTTP200, PONG, cache, serverReady ou onlineAvailable de outro engine não comprovam esta liberação.

Sala nova precisa usar os mesmos vínculos e capacidade permitida, roster/slots únicos, geração atual e links host-star. Transport relay-wss-v3; recoveryProtocol station-stream.v3; ticket assinado por link/ownerSlot/hostSide, validade máxima60s, window262144 por direção/link. Upgrade WSS preserva prova própria, subprotocol station-stream.v3, sessão, leases, replay, ACK de consumo real, pause/epoch/barreira e orçamento. Não inventar GETsnapshot, credencial nova ou relaxar segurança.

Devolver **uma resposta técnica única** com:

- Commit e hashes dos scripts efetivamente aplicados; release/DLL/PID/horário e featureflag v3 do serviço observado.
- Hash/revisão atuais de catálogo, manifesto, perfis e modos; recibos prepare/apply e recarga sem restart; contagens antigas preservadas.
- Por engine final, quantidade e relação agregada de edições/modos aprovados ou recusados com motivo; nenhuma capacidade presumida.
- Resposta assinada fresca conferida por sistema e teste negativo de conteúdo, engine, perfil e capacidade não aprovados; importação posterior de jogo desconhecido sem aprovação automática.
- Confirmação de que PS3/PS4 continuam offline, nenhum runtime/segurança/app/economia/render foi alterado pelo cadastro.

O app R128 consome essas aprovações sem recompilar por cadastro de dados. O mantenedor instala o candidato e testa salas novas. Código publicado, dados ativados e gameplay são conclusões separadas; esta entrega comprova compilação/preservação/assinatura, não estabilidade prolongada ou redução térmica.
