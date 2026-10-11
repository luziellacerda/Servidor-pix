# Entrega única APP → SERVIDOR — R133

**Entrega final do app preparada e instalada no Motorola; cadastro/ativação do servidor ainda não confirmados.** Fonte `5966a8ad23ae9040c570d359b999c8a31de904b4`; APK SHA `945f7ded35e551d638135a05140c32218eb38277d10eaf21e3f83087d71da112`, 2.714.971.151 bytes, assinatura original v2/v3 e alinhamento 16 KiB. A instalação terminou em `2026-10-11T21:41:18.825876+00:00`; o frontend oficial foi observado às `21:41:21.187453+00:00`, com hash integral, UID e dados preservados. O kit privado e os 78 modos/44 jogos revisados estão preparados. [STATUS](STATUS.json) e [índice dos recibos](evidence/PUBLIC-EVIDENCE.json) mantêm fonte, pacote, instalação, preparação e aplicação separados. Autenticação atual, aparência das novas artes, controles, gameplay e temperatura não foram validados.

## Pedido único

Cadastrar os quatro motores atuais PSX/N64/GameCube/Wii e os modos revisados por edição/conteúdo no contrato v3 existente, conservar os registros anteriores e devolver aprovados/recusados com razões e capacidade assinada fresca. Concluir também a publicação de capas pendente, com revisão/hash/contagens efetivamente publicadas. PS3/PS4 não recebem online; PSP não ganha engine online por receber uma fotografia de hardware.

A R133 acrescenta três fotos ao painel já existente, com 19 artes antigas preservadas, cinco substituições/zero adições e 13.855 entradas preservadas conferidas no APK. Todos os 40 DEX e frontend são byte idênticos. O carousel compartilhado renova os quatro IDs; não altera cores, opções, perfil/controller, transporte, saída humana, autoridade ou economia. Não reaplicar IDs R129/R132 ao bundle R133.

## Identidades finais conferidas

| Plataforma | ID final | Core SHA256 | Runtime SHA256 |
| --- | --- | --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-42e4ca5f2acc` | `e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9` | `42e4ca5f2acc86f7b9c15584bb0f583b9fe07d24c88f955eebd0bcbde30f03d4` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-6dd47d4e0092` | `fe6e810a1c7fc50a494b50b1dccf581b84385667191c0cbcd235d585a0703dd8` | `6dd47d4e00923f27e71523cdfa6c9d3d37d45f0d5ab9cb00225acbfeed45987b` |
| GameCube | `dolphin-5102a033-gamecube-v3-5912af1feeef` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `5912af1feeef31abd75095b169b919a6737637c6d070a6b7f69cdfccf5f74737` |
| Wii | `dolphin-5102a033-wii-v3-5912af1feeef` | `f7ebe048e030a029c0667cf071ab4a49d702f77daf50d40ad40ed1f493ded37d` | `5912af1feeef31abd75095b169b919a6737637c6d070a6b7f69cdfccf5f74737` |

SHA da identidade original `d1eb9bfe78be4c513daedde1ff1f624f653b5353db5a64b8a07d9df213dfb34e`; source-freeze SHA `66eca3d4c8cbe74cfcd2e3c8f8ede6908c1da1e6df94c0d9ff0b494ab27bc877`. Os mapas completos e hashes de origem estão em [evidence/PUBLIC-EVIDENCE.json](evidence/PUBLIC-EVIDENCE.json); essas cópias sanitizadas não são o par de implantação.

## Kit privado e modos prontos para a mesma publicação

O [manifesto público de hashes](evidence/server-kit-hash-manifest.json) confronta os 16 arquivos privados e comprova o par original identidade/pacote concordante. Hash do recibo privado de preparação: `7bd90b84ffb065b738c275befa9b4a1650716fa2ab7549b7e8c2a82ef83126f2`. A folha `server-kit` contém seis bibliotecas e três JSONs técnicos; `server-kit-original-receipts` conserva identity/package/source-freeze/engines/profile originais; `server-kit-tools` contém os dois extratores/geradores pinados. O merge de modos usa o helper servidor exato indicado abaixo.

As pastas atuais no PC são `G:\StationNormalOwnersR127\console-panel-r133\server-kit`, `...\server-kit-original-receipts` e `...\server-kit-tools`. O operador recebe/transporta essas três pastas privadamente e aponta `ARTIFACTS`, `DELIVERY` e `TOOLS` para seus diretórios reais recebidos no servidor. Não presumir que caminhos G: existem no Linux. O backup único em `F:\TurboStations-Backup-Completo-20261011` deduplica os bytes em `desenvolvimento/reconstrucao-atual/console-panel-r133/inputs`; `INPUT-MAP-R133.json` resolve `originalPath/role` para `backupRelativePath`, permitindo reconstruir essa estrutura exata sem segundo snapshot ou pasta flat inventada.

O ledger privado, incoming manifest e resultados do merge são vinculados às identidades finais e aos baselines reais abaixo. Transferir os arquivos originais, não as cópias sanitizadas do Git. Não transmitir APK, firmware, BIOS, RSA, ROMs, saves, licenças, sessão, capturas ou logs pessoais. Kit preparado localmente não é kit recebido/ativado no servidor.

O [manifesto público do preparo de modos](evidence/mode-preparation-hash-manifest.json) indexa 22 arquivos por hash/tamanho; recibo privado original SHA `05f31f7aa6cf76b75fe76ef11f0e4b0391e14f216e05527e4306e2d2947bc6f8`. Originais obrigatórios para revisão/reprodução: `reviewed-decisions.json` SHA `1cde1bfb3e591108263abb3c0bf4d15bd91e22c9f9ffbb4ade41abad8c59aa8b`; `mode-additions.json` SHA `719a61a2158756f58646720ca202bad2a381c79e8380af345e11562e06e942ce`; `prepared-new-profiles.json` SHA `8477fc12a39cc48e3b77d12f260cc7b8f055beaffab78f7709ba3e9643ca38fc`; `official-merge-receipt.json` SHA `d4ec7e7a2e63a8eee98c147841b011897dcda1576436119fb0620c0399d42ed5`. `REVIEW_LEDGER` aponta ao primeiro original recebido privadamente; o operador reexecuta o merge sobre seus baselines ativos. As prévias não substituem o registrador/administração real e não devem ser copiadas diretamente sobre os perfis ativos. O hash-only público preserva o inventário sem divulgar os itemIds/contentSHA individuais do ledger.

## Receita administrativa exata do código examinado

Os comandos abaixo são orientação para o operador humano autenticado. Não foram executados neste acompanhamento. Foram conferidos diretamente nos blobs do commit servidor `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861`; não introduzem endpoint, script de validação ou argumento inexistente.

### Arquivos e configuração efetivos

| Fonte no commit servidor | SHA256 dos bytes Git |
| --- | --- |
| `docs/station-android/r128/operator/build_incoming_manifest.py` | `362bcae9d8915f7f43aac4c48d06b35bdf57efc88d6acb9199aa06e3a0231426` |
| `docs/station-android/r128/operator/extract_private_server_artifacts.py` | `27053b843d763adcf570e859b2c8658e216dbf6f80963b600fa5326958f70ef4` |
| `docs/station-android/r128/operator/merge_scoped_modes.py` | `ddac598ca1671e6d682b626866a3b1984eb88c5ff7d677c7b4cd563a6a677783` |
| `docs/station-android/online-plataformas-20261009/library-tools/station_online_profiles.py` | `3d5d5b5563e2a6825ab9e798da80313cdbf2640522bc4dfd6b47e25c7f69eae1` |
| `docs/station-android/online-plataformas-20261009/library-tools/cadastrar-motor-online-station.py` | `a3c81e0c379984d6b7736f79ce963c73a3f3250883f6304a32b5c6cfda73a096` |

O operador confirma os caminhos ativos e os hashes publicados, inclusive o módulo Python realmente importado. `SERVER_OPERATOR` aponta para o registrador ativo; `PROFILES_MODULE` para o módulo ativo; `TOOLS` para os helpers acima. `CONFIG` é a configuração administrativa ativa. Dela saem `CATALOG=outputDirectory/index.json`, `PROFILES=autoOnlineProfiles.registry`, `ENGINES=autoOnlineProfiles.engineManifest` e `MODES=autoOnlineProfiles.preparedModes`. Os quatro registros protegidos devem estar diretamente dentro do mesmo `outputDirectory`, conforme guarda do registrador. Não adivinhar caminhos Linux nem alterar permissões.

`DELIVERY` é o diretório recebido privadamente com `runtime-final.json` e `package-final.json` ORIGINAIS, e `ARTIFACTS` a folha privada das seis bibliotecas com `artifact-kit-receipt.json`, `incoming-engine-manifest.json` e `profile-canonical-receipt.json`. O [inventário público de hashes](evidence/server-kit-hash-manifest.json) confronta os 16 arquivos do kit preparado; recibo privado de preparação SHA `7bd90b84ffb065b738c275befa9b4a1650716fa2ab7549b7e8c2a82ef83126f2`. O kit fica no backup completo único do PC; seu local de transferência será o confirmado pelo mantenedor, sem novo snapshot. A disponibilidade local no PC não comprova recebimento no servidor.

`REVIEW_LEDGER` é a revisão privada final por item/edição/conteúdo, nunca a matriz pública sem identidades ou fixtures de teste. `PREPARATION` guarda os resultados intermediários, fora dos registros ativos; `CANDIDATE` deve ser absoluto e ainda inexistente. Os comandos recebem os caminhos reais resolvidos pelo operador.

### 1. Conferir manifesto/canonical do mesmo par final

O kit R133 já foi extraído do APK final: seis core/runtime, 20 dependências conferidas, sem transferir APK, firmware, jogos, BIOS, RSA ou dados pessoais. Não reextrair de outra revisão. O gerador exato pode conferir/reproduzir os dois JSONs administrativos a partir do par original recebido:

```sh
python3 -B "$TOOLS/build_incoming_manifest.py" \
  --runtime-receipt "$DELIVERY/runtime-final.json" \
  --package-receipt "$DELIVERY/package-final.json" \
  --output "$PREPARATION/incoming-engine-manifest.json" \
  --profile-receipt "$PREPARATION/profile-canonical-receipt.json"
```

O gerador conserva canonização UTF-8 compacta, ordem `schemaVersion/controllerProfile/devices/coreOptions`, sem BOM ou LF após o JSON e com LF final das OPTIONS dentro da string. `requireExplicitEngineModes:true` limita os quatro IDs novos a modos revisados e escopados; `controllerProfiles` administrativo contém objetos/configuração, não o array de nomes do asset Android. O registrador valida `launchReady`, `station-stream.v3` e hashes dos bytes reais das seis bibliotecas.

### 2. Mesclar o ledger sem modificar registros ativos

```sh
python3 -B "$TOOLS/merge_scoped_modes.py" \
  --catalog "$CATALOG" --profiles "$PROFILES" --modes "$MODES" \
  --runtime-receipt "$DELIVERY/runtime-final.json" \
  --profiles-module "$PROFILES_MODULE" \
  --decisions "$REVIEW_LEDGER" \
  --output "$PREPARATION/prepared-modes-merged.json" \
  --audit-json "$PREPARATION/scoped-mode-audit.json"
```

O helper chama as funções reais `validate_modes`/`prepare` do `station_online_profiles.py`; `prepare` chama `validate_manifest` e revalida configurações/portas. Não existe necessidade de inventar um CLI separado de validator. O registrador repete essas validações na preparação e aplicação.

Preservar integralmente os 5.176 perfis e o prefixo dos 321 modos históricos do baseline recebido, além de todos os engines anteriores, SNES/Mega e recusas reais. Se os baselines ativos já mudaram, usar seus bytes/hash/revisão atuais e registrar a diferença; não sobrescrevê-los com o snapshot antigo. A migração automática admite apenas aprovações N64/GC/Wii com core/controller/canonical e conteúdo exatos. PSX é excluído dessa migração: os dez achados documentais não preenchem `biosPolicy:HLE-per-game-approved` nem `reviewEvidence` da edição atual. Nenhuma aprovação HLE foi acrescentada pelo app.

### 3. Selar e aplicar a mesma candidata por administração nativa

```sh
python3 -B "$SERVER_OPERATOR" prepare \
  --catalog "$CATALOG" --profiles "$PROFILES" --current "$ENGINES" \
  --modes "$MODES" \
  --mode-additions "$PREPARATION/prepared-modes-merged.json" \
  --incoming "$PREPARATION/incoming-engine-manifest.json" \
  --artifacts "$ARTIFACTS" --output "$CANDIDATE"

pkexec python3 -B "$SERVER_OPERATOR" apply \
  --config "$CONFIG" --candidate "$CANDIDATE"
```

`--modes` é sempre o registro ativo original; o merge entra exclusivamente em `--mode-additions`. O registrador sela `incoming.json`, `engines.json`, `profiles.json` e `modes.json`, confere binários e copia-os para a candidata. A aplicação exige autenticação Linux nativa (`geteuid()==0` e `PKEXEC_UID` original do operador esperado), sem forjar variável/credencial. Sob `scan.lock`, revalida baselines, selos, bytes reais e recomputa os vínculos antes de publicar modos → engines → perfis. Uma falha restaura os três arquivos byte a byte; baseline mudado interrompe a aplicação. Não editar `preparedModes` antes dessa transação.

Se preferir a rota administrativa integrada, os argumentos reais são `install --config --incoming --artifacts --mode-additions --output`, sob a mesma autenticação. Essa alternativa executa `stage` e `apply` sobre a mesma candidata; não executá-la depois de uma publicação já concluída por `prepare/apply`.

### 4. Observar hotreload e assinatura v3 fresca

`StationMultiplayerProfileMonitor` SHA `a9533df2f88d8ca04eb0491892d0cedfb4a407b73c05d40e2fb36b04d1ffc728` verifica a cada dez segundos, lê um arquivo completo limitado a 16 MiB, chama `ReplaceProfiles` e mantém o último registro válido se rejeitar a atualização. Salas existentes mantêm perfis/vínculos congelados. Confirmar recarga e revisar o recibo/hash final; não reiniciar a API ou cobrar recompilação Android por esse cadastro de dados.

O scanner Python é oneshot e importa os módulos a cada execução: a recarga C# de perfis não publica código Python. Se os scripts ativos divergirem, o operador coordena o timer existente, espera a execução terminar e confere `Type/ExecStart/module.__file__/SHA` no processo novo antes de restaurar o timer. Nenhum scanner, serviço ou timer foi operado pelo app nesta entrega.

Confirmar presença autenticada `enter` em `POST /v1/station/online/command`, depois solicitar `capabilities` ou `snapshot` em `POST /v1/station/online/multiplayer/command` com `requestId` UUID novo, item/conteúdo e engine finais. O endpoint real SHA `cc610e2f8473bbe997af5a3b3b64f4825253ca8a3bf2e5049a12737986cb32f2` autentica sessão/prova, reautentica antes de devolver e assina o envelope com `licenseId/deviceId/sessionId/requestId/snapshot`.

Validar criptografia e esses vínculos privadamente antes de consumir: `multiplayerVersion3`, capability `station-multiplayer.v3`, instance/revision atuais, item/content/engine/core/runtime/profileId/profileSha/controller/modo e `allowedPlayerCounts` exatos. `domain ... online/v1` do envelope não muda a authority multiplayer v3. Não substituir essa confirmação por `snapshot.engines` legado, v2, `serverReady`, cache, HTTP 200 ou PONG. Nenhuma resposta autenticada fresca R133 foi obtida nesta documentação.

## Quantidade real, ledger final e recusas específicas

Todos os 3.940 itens foram confrontados: 3.685 visíveis e 255 aliases, catálogo 33. Os 5.176 perfis e 321 modos históricos são preservados integralmente. O ledger R133 prepara 78 modos em 44 jogos/edições e 78 novos perfis na prévia do operador, com os IDs finais:

| Plataforma | Modos revisados preparados | Jogos/edições preparados |
| --- | ---: | ---: |
| Nintendo 64 | 66 | 33 |
| GameCube | 9 | 8 |
| Wii | 3 | 3 |
| PSX | 0 | 0 |
| **Total** | **78** | **44** |

O teto PSX 2/N64-GC-Wii 4 não autoriza jogo, modo ou número de participantes. A sala utiliza apenas `allowedPlayerCounts` do vínculo edição/conteúdo/engine/core/runtime/controller/canonical aprovado. Modos alternados por portas independentes são descritos como alternados; personagens de equipe, bots, acessórios e funções de assistente não são contados como humanos simultâneos. Ver [matriz completa](catalog/matrix.json), [resumo/hash de entradas](catalog/summary.json) e [proveniência de modos](catalog/reviewed-mode-provenance.json).

Permanecem 110 seleções N64 apenas na política genérica em teste, 17 jogos nativos documentados individuais, 14 entradas com bloqueio documental/controle e 337 entradas sem engine online no cliente. Auditoria completa não equivale a homologação completa. Os bloqueios identificados são:

| Ordinal | Plataforma | Edição/título | Motivo exato da matriz |
| --- | --- | --- | --- |
| 2082 | n64 | Banjo to Kazooie no Daibouken 2 | `Japanese-BanjoTooie-primary-per-minigame-port-and-count-review-required` |
| 2150 | n64 | Mortal Kombat Mythologies: Sub-Zero | `original-n64-manual-mode-or-player-count-not-visually-verified` |
| 2166 | n64 | Pilotwings 64 | `primary-product-player-field-conflicts-with-existing-solo-classification-mode-manual-required` |
| 2169 | n64 | Pocket Monsters Stadium 2 | `Japanese-Stadium-edition-not-equated-to-western-PokemonStadium` |
| 2198 | n64 | Smash Remix | `SmashRemix-mod-specific-mode-and-edition-review-not-provided-by-original-game-source` |
| 2214 | n64 | Turok 2: Seeds of Evil | `original-N64-manual-per-mode-count-and-controller-pages-not-readable` |
| 2215 | n64 | Turok 3: Shadow of Oblivion | `original-N64-manual-per-mode-count-and-controller-pages-not-readable` |
| 3743 | gamecube | Resident Evil - Code - Veronica X (PT-BR DUB) - (Disc1) | `exact-GameCube-primary-player-page-not-readable-DUB-edition-not-reviewed` |
| 3744 | gamecube | Resident Evil - Code - Veronica X (PT-BR DUB) - (Disc2) | `exact-GameCube-primary-player-page-not-readable-DUB-edition-not-reviewed` |
| 3751 | gamecube | Terminator 3 - The Redemption | `GameCube-coop-announced-exact-primary-seat-count-not-extracted` |
| 3753 | gamecube | The Legend of Zelda Four Swords Adventures | `gamecube-gba-link-v1-not-implemented-by-current-native-port-profile` |
| 3840 | wii | Epic Mickey 2 The Power of Two | `exact-Wii-primary-mode-and-controller-review-not-extracted` |
| 3844 | wii | Super Mario Galaxy (USA) (En,Fr,Es) | `wii-co-star-v1-role-and-input-profile-not-implemented` |
| 3845 | wii | Super Mario Galaxy 2 (USA) (En,Fr,Es) | `wii-co-star-v1-role-and-input-profile-not-implemented` |

PSX: dez dos 26 históricos multiplayer têm fontes primárias lidas; os 84 itens atuais ainda exigem revisão HLE por edição/conteúdo, estado inicial/saves e duas portas humanas Standard, com zero aprovação nova. Quatro portas, multitap, analog ou lightgun não são suportados por esse owner. Fontes ausentes/ilegíveis nesta janela entre os 26: 3760 Capcom vs. SNK - Millennium Fight 2000 Pro; 3772 Digimon Rumble Arena; 3774 Dragon Ball Z - Idainaru Dragon Ball Densetsu; 3775 Dragonball GT - Final Bout; 3776 Dragonball Z - Ultimate Battle 22; 3786 Gran Turismo; 3802 Metal Slug X; 3803 Mortal Kombat 3; 3804 Mortal Kombat Trilogy; 3805 Need for Speed III - Hot Pursuit; 3819 Street Fighter EX Plus Alpha; 3825 Tekken; 3833 Tony Hawk's Pro Skater 4; 3835 Wipeout 3; 3836 World Soccer Winning Eleven 2002 (Japan); 3837 Worms Armageddon. Os dez achados documentais também não substituem `biosPolicy=HLE-per-game-approved` e `reviewEvidence` real. Worms Armageddon já tinha recusa; compartilhar controle/turnos não prova duas entradas independentes.

As fontes de revisão reais estão em [web-mode-research.json](catalog/web-mode-research.json), [n64-root-mode-decisions.json](catalog/n64-root-mode-decisions.json), [n64-additional-primary-review.json](catalog/n64-additional-primary-review.json) e [psx-primary-consolidated.json](catalog/psx-primary-consolidated.json). Os motivos não são substituídos por regex de nome, teto, testes de ABI ou aprovação RetroArch antiga. PS3/PS4 permanecem offline e não integram esse cadastro.

A recusa física R132 em Criar sala manteve bloqueio antes de sala/jogo após atualizar capabilities; não houve captura de HTTP/envelope bruto. Esta entrega R133 não capturou resposta autenticada atual. O operador conclui os vínculos preparados mantendo a recusa segura das lacunas, devolve a matriz assinada fresca e informa os bloqueios específicos restantes; gameplay R133 requer sala nova com participantes atualizados.

## Capas

O último retorno real `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861` declarou `covers.published=false`, incluindo 157 vínculos N64 preparados; `latestProductionBindings` enumerou apenas Mega Drive/SNES/PSX. Concluir esse batch com hashes/revisões dos itens reais e preservar capas corretas/ambíguas. Isso não significa ausência de todas as capas locais: uma capa N64 foi observada na R132. Publicação de imagens e aprovação de modos são independentes.

## Fechamento esperado

Devolver, em uma única resposta, recebimento/selos finais, scripts e baselines efetivos, autenticação administrativa, receipt da candidata/aplicação, hotreload, matriz assinada fresca e capas publicadas. Informar bloqueios específicos de cada edição/modo e pendências físicas. Não declarar todos os jogos aprovados pela capacidade genérica nem solicitar novo handoff quando o impedimento concreto for kit ausente, autenticação ou edição sem revisão.

A fonte e o APK finais permanecem os mesmos após cadastro de dados. Aplicação administrativa, resposta v3 assinada fresca e partida física são evidências distintas; nenhum desses resultados foi presumido pelo app.
