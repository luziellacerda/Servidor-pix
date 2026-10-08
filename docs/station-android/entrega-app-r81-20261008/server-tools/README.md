# Preparar identidades de conteúdo R81

`prepare_content_identity_registry.py` é uma ferramenta offline, sem dependências externas, rede, extração de ZIP ou alteração do artefato. Reproduz a lógica de conferência de `bind_content_hashes.py` da R77 e fecha a conversão para o contrato estrito do importador servidor `b472d8a653e065cccb00dfb15dbea3d56c03db98`.

Aceita Python 3.10 ou posterior. Não gera perfis de multiplayer, não aprova jogadores e não publica o registro. O arquivo produzido só contém `schemaVersion: 1` e `entries`, com exatamente `itemId`, `platform`, `artifactSha256`, `launchPath`, `expandedSizeBytes`, `fileCount` e `contentSha256`. O nome relativo de lançamento integra o contrato; caminhos locais absolutos do mapa não são copiados ao resultado nem aos diagnósticos.

## Entradas e comando

Catálogos aceitos:

- Export público R19: `items[]`, cada item com `itemId`, `platform` e `artifact` (`format`, `sha256`, `sizeBytes`, `launchPath`, `expandedSizeBytes`, `fileCount`). A ferramenta usa o item exato, incluindo IDs de compatibilidade explicitamente mapeados.
- Export `records[]` com o mesmo objeto `artifact`.
- Inventário `records[]` com `artifactFormat`, `artifactSha256`, `artifactSizeBytes`, `artifactLaunchPath`, `artifactExpandedSizeBytes` e `artifactFileCount`.

O mapa privado usa `{ "itemId exato": "/caminho/absoluto/do/artefato" }`. Ele precisa apontar ao ZIP/raw que corresponde ao descritor, não a uma ROM extraída quando o catálogo descreve ZIP. O SHA256 da ROM instalada pode ser comparado com o resultado, mas sozinho não comprova o vínculo com o recipiente do servidor. Guarde o mapa fora do Git.

Na raiz do repositório do app, atribua os três caminhos reais antes de executar:

```sh
python3 -B versions/station-online-readiness-r81-20261008/server-tools/prepare_content_identity_registry.py \
  --catalog "$CATALOG_EXPORT" --mapping "$PRIVATE_ARTIFACT_MAP" \
  --existing "$CURRENT_CONTENT_IDENTITY_REGISTRY" --output "$NEW_CONTENT_IDENTITY_REGISTRY"
```

Use `--existing` sempre que já houver um registro qualificado. Omitir essa opção é somente para a primeira preparação, quando não há identidades anteriores. A ferramenta valida estritamente schema, campos, tipos, hashes e IDs únicos do registro recebido. Conserva as entradas não mapeadas apenas quando item/plataforma/SHA do artefato/launchPath/tamanho expandido/quantidade de arquivos ainda coincidem com o catálogo atual; para raw, o hash de conteúdo também precisa ser o do artefato. Item ausente ou vínculo incompatível interrompe a saída, sem apagar silenciosamente a identidade. Para substituir um vínculo antigo, inclua explicitamente o item no mapa privado com seu artefato atual: os bytes serão verificados novamente. Não é necessário mesclar JSON manualmente. Há um limite de 4.096 entradas no registro recebido e no resultado combinado.

O catálogo entregue nesta revisão está em `docs/server/recovery-r79-20261008/CATALOGO-REV19.json`. O operador deve primeiro confirmar o descritor contra o índice efetivo atual. Ao transportar a ferramenta para o repositório servidor, conserve seu caminho exato no pacote de entrega e ajuste somente o prefixo do comando.

`--output` precisa apontar a **um arquivo novo**, diferente do catálogo, mapa, registro existente e artefatos. Nenhum arquivo existente é sobrescrito. O resultado é preparado integralmente e publicado atomicamente com um link no mesmo diretório; se outro processo criar o destino durante o trabalho, a operação falha e conserva esse arquivo. O sistema de arquivos precisa suportar hard links, caso contrário a publicação falha sem alterar o destino. A ferramenta confere tamanho/hash do recipiente, descriptor raw, membro ZIP de lançamento exato, contagem/tamanho expandido, caminhos, duplicatas e formatos. Os limites são 4.096 itens, 4 TiB de tamanho expandido, 100.000 arquivos e registro de até 16 MiB; ZIPs só usam Store/Deflate, sem criptografia ou links simbólicos. Os bytes de lançamento não recebem remoção de cabeçalho nem outra normalização.

## Aplicação pelo operador do servidor

O comando comprovado no candidato é:

```sh
python3 -B docs/station-android/scripts/atualizar-biblioteca-station.py --config "$CURRENT_IMPORTER_CONFIG"
```

Antes dessa execução, revisar o arquivo novo e atualizar a configuração privada efetiva `contentIdentityRegistry` para seu caminho absoluto. A ferramenta de preparação não faz essa troca. O importador candidato precisa substituir o importador usado também nas varreduras agendadas. A atualização normal não usa `--bootstrap`. O importador usa `scan.lock`, exige correspondência literal de item/plataforma/SHA do artefato/launchPath/tamanho expandido/quantidade de arquivos, grava o índice atomicamente e avança a revisão global quando há alteração. Ele remove `contentSha256` quando o registro correspondente falta ou deixa de coincidir. Portanto, preparar a atualização com `--existing` antes de aplicar preserva as identidades anteriores que ainda correspondem ao catálogo e recusa as incompatíveis para revisão explícita.

O caminho histórico da configuração é `/mnt/DADOS/station-library-auto-private-20261004/config.json`, e o scanner é `turborama-station-library-scan.service`. Esses valores aparecem no instalador original, mas o operador precisa confirmar o `ExecStart` e a configuração efetivos; este pacote não afirma tê-los relido na produção atual.

A DLL `b472d8a` precisa estar implantada para assinar/publicar `contentSha256` no catálogo e expor v3. Conferir o campo pelo fluxo autenticado vigente, com e sem `metadata=1`, depois da publicação e de nova varredura.

## Perfis e ativação: dependências separadas

O candidato lê `Station:Online:MultiplayerProfileRegistryFile` como array JSON absoluto de até 16 MiB. Os perfis são carregados na inicialização; não há recarga de perfis implementada nesse código. As flags exatas para a ativação são `Station__Online__MultiplayerEnabled=true` e `Station__Online__MultiplayerLegacyCapacityGate=true`, com `Station__Online__RecoveryEnabled=true`. Online/Relay e o registro legado continuam necessários. Não existe neste candidato uma ferramenta completa específica para implantar v3 ou aprovar perfis; os operadores antigos R74 possuem baselines fixos e não são uma receita de implantação R81.

Preservar `Station__Online__EngineRegistryFile` e suas dez engines v2. Esse registro rejeita protocolos diferentes de `station-stream.v2`; motores R77/v3 entram apenas no registro separado de perfis. A R81 Java conserva as identidades R77: SNES engine `bsnes-mercury-performance-79d7f9de-mt1-mp3-351cee4540e9`, core `0a3ac7b4fa5da318b1c993d8867aa944eaa2d012680b49e9929bf7a4a398f527`, runtime `351cee4540e916468a504788911e4c5fcb4fe141e3b544b1b1255d32e1d04a26`.

O gate legado exige exatamente um perfil aprovado `standard-2p-v1`, permitindo dois participantes, por chave item/conteúdo/engine/core/runtime R76. Duplicar modos standard nessa chave também falha. Conservar as dez engines, sem preparar os perfis legados, não conserva a admissão de novas salas R76. Não ativar globalmente só com os pilotos, pois os outros títulos sem perfil seriam recusados.

Pilotos documentados:

- Battletoads in Battlemaniacs: a edição SNES ZIP USA contém `Battletoads in Battlemaniacs (ESP) (NTSC).smc`. Fixar o ID realmente selecionado, conferir o hash interno e qualificar o modo de dois jogadores. Os IDs `826da6daebe9edbebffb3721f83abf12` e `ac79b8fc351cb0811a7e9b51a099ba9c` compartilham o descritor ZIP no export R19; isso não autoriza herança automática entre IDs.
- Super Bomberman 2 USA: `station_df50d575815ab105084a79d68e0c8fb3`, raw `Super Bomberman 2 (USA).sfc`, SHA256 `0a4b4a783a7faf6ada3e1326ecf85de77e8c2a171659b42a78a1fae43f806ca6`. Perfil proposto `snes-sbomberman2-usa-battle-single-r77`, controle `snes-multitap-port2-v1`, hash de perfil `f59408b49f6e110a569fe9498af433b9f67c9586cbb8648238b8e571bb8b0143`, contagens propostas `[2,3,4]`, ainda `approved:false`. Confirmar Battle/Single Match com cada pad em MAN e P1–P4 exclusivos; Normal Game não integra esse perfil.

O PC pode preparar hashes e comparar os binários exatos do APK e a ROM instalada. O operador precisa conferir o artefato efetivo, publicar identidades persistentes, preparar a continuidade v2 e qualificar os perfis. A aprovação dos modos depende de gameplay com os aparelhos e controles correspondentes; os testes sintéticos não substituem isso. Antes de trocar somente Station, concluir a divergência TLS v2 pendente, validar sombra/domínio público e backup/retorno, e confirmar ausência de salas/conexões/sessões de recuperação retidas. Convites v3 permanecem uma lacuna distinta no candidato recebido.

## Testes locais sintéticos

```powershell
python -B versions/station-online-readiness-r81-20261008/server-tools/tests/test_content_identity_registry.py --work-dir 'E:\ESTUDO APK\work\station-online-readiness-r81-20261008\server-tools-tests' -v
```

Somente fixtures sintéticas. Nenhum telefone, artefato real ou serviço é operado por essa suíte.
