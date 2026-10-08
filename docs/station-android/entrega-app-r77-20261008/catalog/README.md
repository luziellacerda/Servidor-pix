# Inventário de compatibilidade online por jogo — R77

## O que esta entrega contém

Inventário de **todos os 2.467 IDs** da última exportação completa localizada nos ramos remotos existentes: **2.212 jogos visíveis e 255 IDs antigos de compatibilidade**. A exportação é a revisão 14, publicada em `0a5ccc4e6cb647f5fedafa6d400995fd62cd1bfe` em 05/10/2026 e preservada no retorno `bee3dcd5c2c0c0a228805957fe89b9a6023e8402`. Isso não equivale a uma consulta autenticada do catálogo em produção hoje.

Todos os IDs possuem decisão explícita de bloqueio e lista vazia de quantidades autorizadas. Este arquivo é uma fila de curadoria, **não um registro de autorização pronto para produção**. O campo descritivo `players` só aparece como pista para pesquisa. Não libera dois jogadores por padrão, não transforma jogadores alternados em simultâneos e não propaga autorização por semelhança do nome.

| Plataforma publicada | Jogos visíveis |
|---|---:|
| Mega Drive | 887 |
| Mega Drive BR | 94 |
| Nintendo 64 | 157 |
| Neo Geo | 189 |
| Neo Geo CD | 50 |
| SNES | 644 |
| SNES BR | 191 |
| **Total** | **2.212** |

## Identidade de conteúdo

Há 849 itens `raw` com hash publicado dos bytes do arquivo entregue. Para eles, o hash de conteúdo sem normalização é o hash do artefato. Nos 1.363 ZIPs visíveis e nos 255 registros de compatibilidade, a exportação não informa SHA256 da ROM interna: o hash do ZIP não pode substituí-lo.

`bind_content_hashes.py` permite ao operador gerar esses vínculos a partir de arquivos que já possui, sem download, extração ou publicação de ROMs. Verifica tamanho/hash do artefato, caminho exato de lançamento, unicidade de entradas, contagem e tamanho expandido. Produz somente ID, hashes e tamanho, sem caminhos locais. O vínculo continua sem autorizar a partida.

Os hashes são dos bytes completos do arquivo de lançamento. Não remove cabeçalho de 512 bytes, não troca ordem de bytes e não normaliza formatos. O servidor deve usar a mesma definição do contrato de conteúdo do aplicativo. Qualquer transformação requer identidade e evidência separadas.

## Pesquisa concluída e pendências

`mode-evidence.json` contém cinco fontes primárias consultadas e seis variantes selecionadas por ID **e** SHA do artefato. São candidatas para qualificação, não seis títulos liberados:

| Jogo original | Modalidade documentada | Limite | Pendência da variante do catálogo |
|---|---|---:|---|
| Battletoads in Battlemaniacs | 2 Players A/B | 2 simultâneos | O ZIP com nome USA contém lançamento `Battletoads in Battlemaniacs (ESP) (NTSC).smc`; falta hash interno. A outra variante é `snesbr` e precisa conferência. |
| Secret of Mana | Aventura cooperativa | 3 simultâneos | Só há variante `snesbr` na exportação, com arquivo de 2.097.664 bytes; precisa verificar a tradução e o cabeçalho. P2/P3 só controlam aliados após recrutá-los. |
| Super Bomberman 2 | Battle Single/Tag Team | 4 simultâneos | USA raw tem hash publicado; ainda precisa qualificar core, perfil e rede no conteúdo exato. Tradução BR não herda autorização automaticamente. Normal Game é de um jogador. |
| Mega Bomberman | Evidência de Multitap do autor do emulador | **Pendente** | Release oficial comprova mais de dois em termos gerais, mas ainda falta amarrar modo, máximo, protocolo e ROM interna desta variante. |

O manual original do jogo, mesmo disponível num arquivo de terceiros, é tratado como documento primário; descrição de loja, fórum ou número do XML não autoriza. Os links e limites de cada fonte estão no JSON. Não foi alegada pesquisa individual concluída para os demais 2.461 IDs; `allTitlesResearched=false` e cada pendência estão no recibo de cobertura. Falta de confirmação mantém bloqueio.

O core SNES corrigido está em `../core-snes`: Multitap na segunda porta, dispositivo 257. Sua validação isolada não prova suporte de um título. Mega Drive precisa de protocolo por jogo (`standard`, `sega` ou `ea`) e não pode receber uma opção global por causa de Mega Bomberman. N64/Neo Geo/Neo Geo CD também não ganham motor online qualificado apenas porque o catálogo declara multiplayer.

## Fluxo de conclusão pelo servidor e aplicativo

1. Confrontar estes IDs/revisões com o catálogo atual. Qualquer ID/hash novo começa bloqueado.
2. Para o artefato exato, fornecer o SHA256 do arquivo efetivamente iniciado; para ZIP, usar o binder ou procedimento equivalente conferido. Vínculo antigo precisa validação explícita, inclusive quando compartilha ZIP com ID visível.
3. Pesquisar o manual/mantenedor original para cada modo: simultâneo ou alternado, limites humanos válidos, restrições de progresso e adaptador. Registrar fonte e versão exata.
4. Qualificar o modo no core/hash/perfil de controles e transporte da versão atual. Não misturar hash de core antigo com binário alterado.
5. Somente então produzir a autorização assinada no formato acordado com o servidor. O app deve comparar item, hash, modo, perfil e limite; a ausência de autorização impede criar/entrar/iniciar.

## Reprodução e testes

`build_catalog_inventory.py` lê só os três documentos públicos fixados do clone já existente, verifica contagens/IDs/hashes e escreve os resultados em `E:\ESTUDO APK\work\station-multiplayer-r77-20261008\catalog`. Não muda checkout nem consulta sessões privadas.

```powershell
python -X utf8 -B build_catalog_inventory.py
python -X utf8 -B run_catalog_tests.py
```

`run_catalog_tests.py` passou **12.361 verificações**: cobertura dos 2.467 IDs, bloqueio por padrão, separação do hash ZIP/ROM, metadados sem autoridade e binder com arquivos sintéticos. Rejeita hash/tamanho divergentes, caminho ausente/inseguro, entradas duplicadas e descritor expandido incompatível. O binder também calcula SHA-1, MD5 e CRC32 dos bytes exatos de lançamento, para futura conferência com DATs; não são checksums do recipiente ZIP nem aprovação online. Não utiliza ROM, Android ou rede nos testes.

Para o operador calcular hashes, crie **fora do Git** um JSON privado com formato `{ "itemId": "caminho absoluto do artefato existente" }` e execute:

```powershell
python -X utf8 -B bind_content_hashes.py --inventory catalog-inventory.json --mapping mapa-privado.json --output content-bindings.json
```

O resultado não inclui o caminho privado e não libera online. Não publique o mapa privado, arquivos de jogos, BIOS, credenciais ou logs de usuários.

## Arquivos

- `catalog-inventory.json`: todos os IDs e suas pendências, SHA256 `5095eed365f94a6f2acf076cfa5d9abd6871499a8d459e285af1ca65fd805703`.
- `catalog-review-queue.tsv`: fila compacta para revisão de todos os registros.
- `catalog-coverage.json`: contagens, hashes das fontes, procedência e limites.
- `catalog-tests.json`: recibo das verificações sintéticas.
- `mode-evidence.json`: modos do jogo original, IDs/variantes candidatas e fontes. Não é uma allowlist de produção.
- `proposed-registry.json`: candidato revisável Super Bomberman 2 USA raw, modo Battle Single, quantidades `[2,3,4]`, core corrigido e `approved=false`. Runtime/engine/hash final do perfil ficam pendentes da integração; não é arquivo pronto para carregar em produção.
