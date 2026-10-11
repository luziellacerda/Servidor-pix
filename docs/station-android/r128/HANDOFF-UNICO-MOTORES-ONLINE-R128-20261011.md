# Handoff único dos motores online da TurboStations R129 — variante da entrega R128

Destinatário: mantenedor do Servidor-pix. Aplicar nesta rodada o cadastro v3 dos quatro motores abaixo, publicar os modos comprovados do catálogo atual e devolver uma matriz única de liberação. O app candidato já contém as rotas reais e consome os perfis assinados pelo contrato existente. PS3 e PS4 são exclusivamente offline e não fazem parte deste cadastro.

Fonte executável Android: `cf623c7c5cf34d4aa59ad278dc98fa6d7a50721b` no TurboElden. Instalador `TurboStations-Premium-R129-20261011-Final-Modal.apk`. APK final SHA256 `89dd543986fdcb0c4b7ea53e82de87907f3b41ee0fe892479d8a63f52e4b7df0`, 2237505749bytes, assinatura original v2/v3 e alinhamento16KiB conferidos. Código servidor auditado: `141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc`. A instalação integral R129 foi comprovada no Motorola Edge 30, com UID/dados preservados. Catálogo, listas DEMO Sony, opções de configuração e retorno ao carrossel foram observados neste pacote final; autenticação, servidor, gameplay e temperatura permanecem não validados. Esta entrega Git não modifica produção nem comprova ativação. As instalações das variantes anteriores estão registradas no histórico; não comprovam instalação, autenticação, visual ou gameplay deste pacote final.

## Atualização de pacote da mesma entrega

Esta é a R129 corrigida, variante do handoff já publicado no commit `7b8c0a7e68b1b0bac5fdcebec62233611533394b`, no mesmo documento e branch. Não há alteração de API, relay, segurança, modo de jogo ou política de aprovação. Os scripts administrativos e os quatro canonicais permanecem byte a byte. PS3/PS4 recebem duas amostras locais identificadas como DEMO, mídias locais e acesso às configurações dos emuladores externos offline. As DEMOs ficam fora do catálogo assinado; não são jogos, downloads nem engines online. Não cadastrar PS3/PS4.

Os emuladores Sony não estão embutidos no APK Station. Foram instalados separadamente no Motorola; o recibo independente `integration/local-sony-demos/evidence/sony-official-installation.json` continua vinculado à fonte af3b/e701 observada naquele momento. Isso não comprova firmware, jogos ou gameplay nem acrescenta requisito de cadastro online. Este pacote corrige somente a exclusão dos dois textos Sony do desenho persistente fora do modal de configurações, com inversão exata conferida no recibo do carousel. A conferência física final no Motorola observou o catálogo e as listas DEMO dos dois sistemas, com capa, nome, sinopse/aviso e contador1/1; ambos os marcadores exatos tinham83bytes. Os heróis, miniaturas e frames/posters das mídias Sony apareceram; movimento dos vídeos não foi validado por imagens estáticas. Os dois botões de configurações abriram, por toque humano, aPS3e Welcome e Bachata S4 0.2.4 com seu aviso inicial de notificações; nenhuma permissão foi respondida nem setup concluído. Após retornar pelo Login público à ESActivity, os textos Sony não vazaram no carrossel. O recibo público agregado do app está em `integration/local-sony-demos/evidence/physical-observation-final.json`. Autenticação, ativação de servidor, gameplay e melhora térmica continuam não validados.

A primeira R129, fonte `83173d43737efa29e4e8e26309ec06b6e545909a` e APK `82bea84df62172214eedab6f52440e990ca599af75397cbbaebe800ac0da4279`, foi instalada no Motorola, mas não representa o pacote final: caminhos das capas DEMO fora do cache privado eram recusados pelo parser nativo e interrompiam o catálogo. A fonte corrigida grava somente essas capas no cache privado já permitido. A mídia Sony usa o motor de vídeo existente; as regras de economia, os emuladores internos e o contrato online permanecem iguais. Usar exclusivamente a identidade corrigida dos recibos atuais, sem cadastrar os IDs intermediários da primeira tentativa.

Os IDs da tabela e os recibos nesta entrega passam a corresponder ao APK R129 final. Conservar todos os registros e vínculos anteriores, gerar as quatro novas entradas com os mesmos helpers e aplicar o mesmo candidato administrativo selado. O kit privado R128 não representa os runtimes R129: extrair as seis bibliotecas do APK R129 pela receita existente e conferir seu recebimento efetivo. A recarga v3, os modos exatos e a confirmação assinada fresca seguem o procedimento abaixo. Esta atualização não acrescenta perguntas nem muda o contrato da rodada existente.

Histórico das variantes intermediárias: fonte `71b5b50868f1e49befbd7ee342ab5493717c844a`, APK `98849416b65401f3ff552a5f5be93408e8104410e866cf253925e2c369a976c9`, instalação integral comprovada; fonte `af3b7e23dea8ffe5b6e83af56513f2ee1bb65dfd`, APK `e7011f0def73160f1cd8ac7c757c9cfec76b3b7bd59da87f3329441b2ad249d9`, instalação integral comprovada. Os recibos dessas instalações permanecem históricos, sem substituir a identidade final desta tabela. Na variante71b5/9884, as capas/nomes e1/1 das duas DEMOs e o aviso PS3 foram observados, sem emulador externo; os dois marcadores tinham83bytes. Hero/miniatura das plataformas continuavam azuis, motivando o ajuste final do fallback estático persistente. Autenticação e gameplay não foram validados. Na variante af3b/e701, o acesso humano às configurações PS3 abriu o emulador externo, mas os dois textos Sony permaneceram sobre a sinopse ao retornar. Ela é intermediária visual; o recibo de instalação integral não comprova a correção do modal deste novo pacote. 

## Correção do cadastro anterior

Esta entrega substitui o procedimento de cadastro da entrega R127 `5696ed9bed01fdfa8cbd3414197c2eea4f3fc898`. O JSON legado de quatro campos OnlineEngine e `Station:Online:EngineRegistryFile` não autorizam estes motores v3. Usar `autoOnlineProfiles.engineManifest`, `.registry` e `.preparedModes`. Não exigir os novos motores em `snapshot.engines` do endpoint legado nem reiniciar o serviço para uma atualização de dados v3.

O monitor de perfis v3 recarrega o registro em até dez segundos e preserva os vínculos das salas existentes. Essa recarga é do JSON C#, não do código Python. No código auditado, o scanner é Type=oneshot e importa prepare a cada execução; não há worker Python contínuo desse módulo. O operador deve confirmar Type/ExecStart/estado/PID atuais e publicar os scripts no library-tools efetivamente indicado por ExecStart, conferindo module.__file__ e SHA. Impedir novos disparos do timer, aguardar o scanner atual terminar sem matá-lo, publicar/verificar os scripts em processo Python novo e restaurar o timer. Não reiniciar a API por consequência dessa atualização. Os recibos de serviço existentes são históricos; não descrevem produção observada hoje.

Se o featureflag v3 não estiver efetivo, o operador deve registrar a configuração e o procedimento real dessa ativação; isso é distinto do falso requisito de restart do registro legado.

## Identidades do APK final

| Sistema | Engine | Core SHA256 | Runtime SHA256 | Controller | Canonical SHA256 | Máximo do motor |
| --- | --- | --- | --- | --- | --- | --- |
| PlayStation1 | `pcsx-rearmed-ff81ed17-player-v3-f31f0a3dbfc9` | `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9` | `f31f0a3dbfc90163e1d12591facb26e66380a02e4c53ae92274ae7ebc01c4597` | `standard-2p-v1` | `81c0e2323a37130dbdbdb9c2efefca8837a9bb6e7169304b4e5731b2ec56290a` | 2 |
| Nintendo64 | `mupen64plus-ae-33bf7021-n64-v3-6868eddfca6c` | `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8` | `6868eddfca6c5380b8191fe9c5ad7671faa3ffb1a0215c0fcc9b73236e685ef8` | `direct-four-ports-v1` | `2966309729f8a19c03b564f0178f263170e1843172f6719002f35fb54e313f9c` | 4 |
| GameCube | `dolphin-5102a033-gamecube-v3-9765756f70fd` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `9765756f70fda14d8103ce7ab7055a4e0fd92e1ed0edef09930ed38e013d9031` | `dolphin-gamecube-four-ports-v1` | `89dc579c37afab7731a2d7322ab7fcf70ed0afa86ba7e491a605c45cd5443fc1` | 4 |
| Wii | `dolphin-5102a033-wii-v3-9765756f70fd` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `9765756f70fda14d8103ce7ab7055a4e0fd92e1ed0edef09930ed38e013d9031` | `dolphin-wii-remotes-four-v1` | `cf3b214d63ce324a82b331fc96a95c6f35e8cc1c7b6bc2bcd659cf35743ceaed` | 4 |

O máximo da engine é apenas teto. A capacidade da sala vem de item/edição/modo aprovado, não do teto da plataforma. N64/GameCube/Wii mantêm cores e opções R126/R127; PSX mantém PCSX-ReARMed normal e LibretroPlayer original. A R129 renova somente as identidades dos quatro motores existentes para vincular o bundle das demonstrações, mídias locais e acesso às configurações Sony. Cores, opções, capacidades, perfis e transporte são idênticos à R128. Manifest/DEX6/DEX35 permanecem iguais; o delta está em DEX28/DEX39, na arte PS3/PS4 do carousel e nas duas mídias locais adicionadas ao APK. Não substituir identidades históricas ou trocar os binários normais.

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

São 68 métodos de teste isolados: 24 seleção/legado,16 merge conservador,20 candidato/publicação/rollback e8 privacidade/integridade do kit. O original141b é lido pelo Git local, sem fetch/checkout/mutação. Os testes usam arquivos temporários e valores sintéticos; não aprovam jogos. Nos testes de publicação, credenciais Linux/grupo/flock/fsync são substituídos para conferir a lógica em Windows: não validam ACL Linux, lock concorrente, produção ou gameplay. Os testes ZIP não validam criptograficamente assinatura APK. O ensaio inicial R127 e os 68 métodos reexecutados com o runtime final R128 são históricos e seus recibos permanecem intactos. Não foram reexecutados para a R129 e não são alegados como validação dessa variante. A compilação/preservação/assinatura R129 usa os recibos finais novos; a canonização da variante é conferida pelo gerador existente com o runtime R129 real.

## Confirmação assinada e matriz de conclusão

Usar `POST /v1/station/online/multiplayer/command`, action capabilities/snapshot, itemId real, clientMaximumPlayers5 e requestId UUID novo. Validar assinatura e vínculos license/device/session/requestId antes de consumir payload. Verificar multiplayerVersion3, capability station-multiplayer.v3, instance/revision atuais e perfil aprovado com item/content/engine/core/runtime/profileId/profileSha/modo/capacidades exatos. HTTP200, PONG, cache, serverReady ou onlineAvailable de outro engine não comprovam esta liberação.

Sala nova precisa usar os mesmos vínculos e capacidade permitida, roster/slots únicos, geração atual e links host-star. Transport relay-wss-v3; recoveryProtocol station-stream.v3; ticket assinado por link/ownerSlot/hostSide, validade máxima60s, window262144 por direção/link. Upgrade WSS preserva prova própria, subprotocol station-stream.v3, sessão, leases, replay, ACK de consumo real, pause/epoch/barreira e orçamento. Não inventar GETsnapshot, credencial nova ou relaxar segurança.

Devolver **uma resposta técnica única** com:

- Commit e hashes dos scripts efetivamente aplicados; release/DLL/PID/horário e featureflag v3 do serviço observado.
- Hash/revisão atuais de catálogo, manifesto, perfis e modos; recibos prepare/apply e recarga sem restart; contagens antigas preservadas.
- Por engine final, quantidade e relação agregada de edições/modos aprovados ou recusados com motivo; nenhuma capacidade presumida.
- Resposta assinada fresca conferida por sistema e teste negativo de conteúdo, engine, perfil e capacidade não aprovados; importação posterior de jogo desconhecido sem aprovação automática.
- Confirmação de que PS3/PS4 continuam offline, nenhum runtime/segurança/app/economia/render foi alterado pelo cadastro.

O app R129 consome essas aprovações sem recompilar por cadastro de dados. O mantenedor instala o candidato e testa salas novas. Código publicado, dados ativados e gameplay são conclusões separadas; esta entrega comprova compilação/preservação/assinatura, não estabilidade prolongada ou redução térmica.
