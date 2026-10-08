# Auditoria da exibição de sinopses — R77 → R78

Data: 08/10/2026. Escopo inicial somente leitura, seguido da preparação de um overlay R78 autorizado pelo mantenedor. Não altera o snapshot R77 nem o contrato de netplay. Nenhum acesso ao aparelho, leitura de ROM/BIOS ou consulta autenticada ao servidor foi realizado nesta auditoria.

## Base e limites da evidência

- App R77 congelado: `ba4fee556ec0010bbde1287746c68a2882cd1e39`.
- Carrossel R77: `98e951b2aad45d63ebe563de95d7a9299339928e54ce082915a147a992ab0302`; recibo local `carousel-build-02/evidence/build.json`, SHA `05a393b69d1b55a12453cf406f8844ae5fcba8210e0b43ec701fe115218edc59`.
- Exportação pública do catálogo lida no clone existente do servidor, commit `bee3dcd5c2c0c0a228805957fe89b9a6023e8402`, diretório `docs/station-android/biblioteca-neogeocd-20261005`.
- `catalogo-completo.tsv`: SHA `3dae0cfa3877fba31b9a347b58795fe0df95333aea628b9ea8be597b7ccfed6e`; é o mesmo arquivo utilizado pela R37.
- `compatibilidade-ids.tsv`: SHA `1773f431c00b59cb0920e1f335e14f5cc88968401015cd0d238a5d795d222a51`.

O total de 2.467 IDs reúne **2.212 visíveis e 255 de compatibilidade ocultos**; não houve crescimento demonstrado para 2.467 jogos visíveis. O cache do Motorola não estava acessível ao coordenador por `run-as`, pois a instalação não é depurável. Portanto, a presente análise não identifica qual valor efetivo estava na tela relatada pelo mantenedor e não atribui a falha física a espaços, placeholders ou IDs antigos sem essa evidência.

## Caminho real do texto

| Etapa | Fonte exata e linhas | Comportamento |
|---|---|---|
| Catálogo Java | `versions/station-multiplayer-r77-20261008/java/client/src/java/org/emulationstation/frontend/station/StationCatalog.java:105` | Lê `metadata.description`; ausência resulta em string vazia. |
| Limite de entrada | Mesmo arquivo, linhas 109–115 | Rejeita mais de 2.000 unidades UTF-16 e caracteres inválidos. Não trunca. A exceção impede aceitar aquele catálogo inteiro. Espaços e quebras de linha válidas são permitidos. |
| Publicação | `StationFrontend.java:90`, especialmente linha 102, no mesmo diretório | Publica oito campos separados por NUL; o último contém `item.description`. Não lê os XMLs R39 para obter sinopses. |
| Ponte nativa | `versions/station-current-r55-20261006/client/src/native/station_frontend.cpp:154` | Limita catálogo a 4.096 itens, linha a 16.384 bytes e descrição a 8.000 bytes. Em 170, grava `station::synopsisPages(description)` em `Item.unusedGameUrl`, offset `+0x30`. |
| Paginação | `versions/station-current-r55-20261006/client/src/native/station_synopsis_pages.hpp:7` | Divide por palavras perto de 800 bytes, preservando limites UTF-8; palavra longa pode chegar a 1.200 bytes. Separa com form-feed. Esse tamanho não é um corte final da sinopse. |
| Índice local | `versions/station-current-r55-20261006/native-dependencies/r16/station_game_lookup.h:3` | Busca binária por ID, seguida de igualdade exata da plataforma nativa. Não associa sinopses por semelhança de título. |
| Escolha do texto | `versions/station-multiplayer-r77-20261008/carousel/native_info.h:160–163` | Texto do servidor com primeiro byte diferente de zero vence a tabela, salvo a exceção exata de Old Towers. Sem ele, usa o índice Station e depois o legado apenas para IDs sem prefixo `station_`. |
| Exibição completa | Mesmo arquivo, linha 114 | `setSynopsisText` converte separadores form-feed em quebras de linha e entrega todo o texto ao componente nativo. |
| Rolagem | Mesmo arquivo, linhas 190–212; `versions/station-current-r55-20261006/native/station_synopsis_scroll.h:26` | Mede altura real, reserva a barra quando necessário, limita deslocamento e reinicia a posição ao mudar seleção ou texto. |

O SHA da função de paginação preservada é `db770934587c73a627d0c333249d7e6cd93c11b4e98abda8cf524dc77b7194e0`. A implementação de rolagem preservada tem SHA `f3d4de5ed22053324cff231eb1568ccc4be761603d3d30603b22689e1e2bb6ea`.

## Cobertura que a R37 realmente entregou

A tabela compilada `station_game_infos.h`, ainda idêntica na R77, tem SHA `537f8ade06f24549f1f176708add5d5aebdfa1f550fd99d72a45140152653191`. São 2.212 IDs ordenados e cobertos: 2.168 descrições publicadas preservadas, 27 recuperações de XML por identidade exata e 17 resumos editoriais. A auditoria de dados detalha a qualidade e procedência; presença de prosa não equivale à verificação de todos os fatos históricos.

| Plataforma visível | IDs | Descrição vazia no TSV | Fallback R37 exato |
|---|---:|---:|---:|
| Super Nintendo | 644 | 6 | 644 |
| Super Nintendo BR | 191 | 0 | 191 |
| Mega Drive | 887 | 5 | 887 |
| Mega Drive BR | 94 | 1 | 94 |
| Nintendo 64 | 157 | 4 | 157 |
| Neo Geo | 189 | 27 | 189 |
| Neo Geo CD | 50 | 0 | 50 |
| **Total** | **2.212** | **43** | **2.212** |

Os 43 vazios publicados têm fallback. Uma entrada apenas com “Old Towers” recebe o override específico existente. Na exportação visível analisada não havia descrições compostas somente por espaços, placeholders conhecidos nem textos acima de 2.000 caracteres. As 2.212 descrições publicadas e plataformas não diferem das entradas usadas pela R37.

Os 255 IDs de compatibilidade não constavam do índice R37. Destes, 249 possuem prosa própria no TSV e seis são vazios: Doom Troopers, Ultimate Mortal Kombat 3, Battletoads in Battlemaniacs, Fatal Fury Special, Frank Thomas Big Hurt Baseball e WWF WrestleMania — The Arcade Game. A tabela legada `game_infos.h` possui 7.211 entradas em 36 plataformas, todas com texto, mas **nenhum dos 255 IDs casa por plataforma + ID**. Os seis vazios, caso entrem no catálogo consumido pelo app, caem no aviso “Sinopse ainda não localizada para esta edição.”.

SHA da tabela legada: `e48f3d25d08a09797825b89992f693400a2d02219c86ae54bffa4552078858e5`.

## Por que o XML R39 não resolve essa lacuna

Os 72 XMLs em `versions/station-theme-collections-r39-20261006/metadata-xml` contêm 72.033 jogos, mas **zero campos `desc` ou `description`**. A contagem de elementos é: 72.033 nomes, URLs de fonte e status; 56.901 avaliações; 54.860 máximos descritivos de jogadores; 67.892 tipos de lançamento. O índice futuro R39 serve a metadados de jogadores/avaliações, não a sinopses. Além disso, não publica jogos nem substitui o catálogo assinado.

## Defeito de seleção demonstrável por leitura

O predicado `*serverDescription` trata qualquer conteúdo não vazio como sinopse utilizável. Assim, um valor só com espaços/quebras, um placeholder ou o próprio título suprime a prosa local exata. A validação Java admite os dois primeiros casos como strings válidas. O defeito está presente no código, mas esses valores não foram observados na exportação visível examinada. O teste isolado preparado extrai a expressão R77 literalmente; ele deve ser executado pela receita antes da compilação final, sem ser apresentado como reprodução no aparelho.

O seletor R78 preserva o texto publicado integralmente quando ele é prosa, inclusive textos curtos válidos. Só usa fallback já resolvido por ID + plataforma nos seguintes casos: branco, placeholder de uma lista fechada com correspondência integral, título integral ou substituição editorial com texto anterior exato. Um ID desconhecido continua explicitamente pendente; não recebe a descrição de outro jogo pelo nome.

Para substituições revisadas, a comparação precisa considerar a forma exata retornada por `station::synopsisPages`. Comparar apenas o texto bruto de 2.000 caracteres falharia depois da inserção de form-feeds. A receita R78 testa a função C++ real contra cada descrição publicada, além de confirmar que prosa nova do servidor continua prevalecendo. A autorização de override exige também `gameInfo` resolvido para a plataforma correta.

O binding da sinopse em execução continua sendo **ID + plataforma**, como na R37. Revisão e hashes do artefato documentam a procedência da pesquisa; o campo nativo da sinopse não os recebe. Portanto, esse mecanismo não deve ser descrito como nova verificação do conteúdo do jogo no telefone. Uma política para reutilização futura de IDs com mudança de edição precisaria ampliar a ponte de metadados e fica fora deste delta de uma biblioteca.

## Coleções

Coleções usam `describeFolder` em `native_folders.h:54` e `collectionSynopsis` em `collection_presentation.h:35`; não passam pela tabela de jogos. Existem 14 caminhos explícitos no TSV e 14 editoriais. A sinopse da coleção inclui contagem e até três exemplos provenientes da seleção atual. O buffer tem 6.144 bytes, com escrita que respeita caracteres UTF-8. Para uma coleção desconhecida há texto genérico, não uma associação adivinhada a uma franquia.

Cinco nomes Neo Geo na exportação TSV contêm dois caracteres de substituição (`COLE��O`), enquanto os editoriais possuem `COLEÇÃO`. Esse desencontro leva ao texto genérico. A fixture independente R67 preservada na R75 contém os nomes corretamente codificados; por isso a divergência do TSV **não prova** que o catálogo autenticado atual esteja corrompido.

R78 adiciona somente cinco aliases integrais, restritos à plataforma Neo Geo, para selecionar os respectivos editoriais. Não renomeia a pasta, muda seu ID, altera jogos, capas ou mapa de vídeos. Nomes desconhecidos, substrings e caminhos filhos não são capturados pelos aliases.

## Delta R78 e qualificação necessária

1. Ampliar `station_game_infos.h` para os 2.467 IDs publicados, preservando a prosa própria dos aliases e usando apenas os seis vínculos por descritor de artefato/launch explicitamente revisados.
2. Restaurar continuações comprovadas e resumos editoriais com igualdade exata do texto anterior; registrar todos os vínculos e hashes nos dados. As 17 continuações completas são fallback nativo. **Não enviar descrições maiores que 2.000 unidades UTF-16 ao Java atual.**
3. Aplicar o seletor conservador e os cinco aliases editoriais. Preservar geometria, rolagem, contador de jogadores, avaliações, 30 fps do menu, vídeos, controles e netplay.
4. Reproduzir o carrossel R77 byte a byte antes da candidata. Rodar busca de todos os IDs, paginação/overrides, atualização futura do servidor, placeholders, aliases, rolagem e regressões do carrossel.
5. Empacotar apenas a biblioteca do carrossel, conferir todas as outras entradas do APK e manter explícita a ausência de conferência visual física.

Fontes preparadas: `native/station_synopsis_selection.h`, `native/station_collection_editorial_aliases.h`, overlays `native/native_info.h` e `native/collection_presentation.h`; receitas `recipes/prepare_synopsis_overlay.py` e `recipes/build_carousel.py`. Os resultados executados, quando disponíveis, são publicados em `evidence/carousel-build.json`; a preparação destas fontes, isoladamente, não confirma compilação, instalação ou correção visual no telefone.
