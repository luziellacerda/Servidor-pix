# Auditoria e cobertura de sinopses existentes — R78

Foram lidos os 2.467 IDs do catálogo publicado revisão 14, os dados completos/editoriais R37, os 72 XMLs R39 e 11 gamelists atuais pertinentes do RetroBat em G:. Não houve leitura de ROM, consulta de sessão, pesquisa web nova, testes ou compilação nesta auditoria.

## Disponibilidade encontrada

| Plataforma | IDs visíveis | Total com compatibilidade | Textos presentes antes da R78 | Vazios |
|---|---:|---:|---:|---:|
| Mega Drive | 887 | 1.133 | 1.131 | 2 |
| Mega Drive BR | 94 | 94 | 94 | 0 |
| Nintendo 64 | 157 | 157 | 157 | 0 |
| Neo Geo | 189 | 189 | 189 | 0 |
| Neo Geo CD | 50 | 50 | 50 | 0 |
| Super Nintendo | 644 | 653 | 649 | 4 |
| Super Nintendo BR | 191 | 191 | 191 | 0 |
| **Total** | **2.212** | **2.467** | **2.461** | **6** |

Todos os 2.212 itens visíveis já possuíam prosa no índice R37: 2.168 descrições preservadas do servidor, 27 recuperações XML e 17 resumos editoriais. Os IDs antigos possuem 249 descrições próprias publicadas. Os seis vazios são exclusivamente IDs antigos.

R39 tem **72.033 registros em 72 XMLs e zero campos de sinopse**: essa exportação preparou nomes, jogadores e avaliações. Ela não supre os textos que faltam. Os 11 gamelists atuais tiveram 2.412 correspondências candidatas por ID derivado de caminho ou nome completo de arquivo/plataforma; nenhum candidato foi herdado por aproximação. As pastas BR são explícitas: `## 1 -PT-BR ##` no SNES e `# PT-BR #` no Mega Drive.

## Seis vínculos exatos somente para descrição

O critério adotado nesta correção é reaproveitar a descrição do ID visível somente quando coincidem exatamente **plataforma, SHA-256 do artefato, tamanho, membro de lançamento, tamanho expandido e contagem de arquivos**. Os seis pares satisfazem essa condição no catálogo congelado:

| Plataforma | Entrada antiga | ID antigo | ID visível fonte |
|---|---|---|---|
| Mega Drive | Ultimate Mortal Kombat 3 (USA) | `233fcab3a55317536be7001984690ca2` | `714f46fd69f37faaf2169f8e78428fb7` |
| Mega Drive | Doom Troopers (USA) | `eeecd9dd7b6acf96d7cabfe174676fb7` | `e307e854203a98bb3174a6e885786edc` |
| SNES | Frank Thomas Big Hurt Baseball | `1e4d341cdfe848ce28f4649e1c2856e5` | `04de9a95cd4715235e87ce10c94cef85` |
| SNES | Battletoads in Battlemaniacs (USA) | `ac79b8fc351cb0811a7e9b51a099ba9c` | `826da6daebe9edbebffb3721f83abf12` |
| SNES | WWF WrestleMania - The Arcade Game | `c7c7b5780bc10427af69cf042bc9ebdd` | `6c1d370746d1def9a3364e6c760501b0` |
| SNES | Fatal Fury Special | `de85a88485d206651fa0bfde244cfc82` | `5092f2b508e4476924e033353f0d5292` |

`../data/exact-alias-bindings.json` registra todos os valores/hash/procedência. Isto não resolve a divergência USA versus ESP/NTSC declarada no arquivo de Battletoads, nem aprova uma partida online. Só reaproveita a prosa já associada ao mesmo artefato/membro publicado. Não se remove região, hack, tradução ou parte do título para aproximar uma correspondência.

## Qualidade e limites

O critério estrutural não encontrou placeholder explícito, título isolado ou caracteres inválidos nos 2.461 textos selecionados. **Texto presente não significa que todos os fatos foram conferidos.** Há 19 textos menores que 180 caracteres, disponíveis em `short-descriptions-review.json` para revisão editorial. Algumas descrições são extremamente curtas, como a entrada homebrew Neo No Panepon e o cancelado Congo; não foram substituídas por um texto genérico.

Foram identificados 311 grupos de texto repetido; 134 atravessam nomes/plataformas diferentes. Ports, aliases e variantes podem legitimamente repetir o texto, por isso a repetição é uma fila de revisão, não prova automática de placeholder. Há 412 entradas BR/hack/bootleg/protótipo/variante nomeadas; seus textos existentes permanecem evidências do catálogo, sem inventar alterações específicas da edição.

A exportação é a revisão 14 publicada, não uma consulta autenticada atual do servidor. Os dados de proveniência antigos podem conter fontes secundárias ou XML sem licença/procedência editorial detalhada; não foram reclassificados como pesquisa primária verificada. Sinopse, contagem de jogadores, modo simultâneo e autorização online continuam campos separados.

## Continuação dos textos cortados

Foram encontrados 17 textos de exatamente 2.000 caracteres terminando sem pontuação. Todos possuem um texto maior no XML atual do mesmo arquivo/plataforma, preservando integralmente os 2.000 caracteres como prefixo. As continuações foram recuperadas sem reescrita, com SHA do XML, ordinal e caminho exatos; o maior texto chega a 4.084 caracteres. Os três casos BR foram associados à pasta BR real, sem herança da edição USA.

`../data/exact-prefix-restorations.json` registra os 17 casos. O header gerado contém overrides por ID e **texto anterior exato** nas formas original e paginada UTF-8 do renderer. Uma descrição nova diferente recebida do servidor prevalece. O conteúdo completo não é cortado na geração; sua apresentação continua sendo responsabilidade da área com rolagem.

O contrato Java existente aceita no máximo 2.000 unidades UTF-16 em `metadata.description`. Por isso essas 17 descrições longas ficam no fallback nativo. A R78 não aumenta o limite Java nem envia esses textos ao servidor. A receita `build_server_synopsis_candidates.py` prepara apenas propostas documentais dentro do limite; cada uma exige comparação literal do texto anterior, além de itemId, plataforma, revisão e SHA do artefato. Mudança em qualquer condição impede a substituição. Não há chamada HTTP nem rota inventada.

## Resultado final dos dados R78

Os 2.467 IDs agora possuem texto: 2.430 descrições preservadas, seis vínculos exatos, 17 continuações recuperadas e 14 resumos editoriais ampliados. Dos 19 textos curtos revisados, **14 receberam novo resumo e cinco permanecem com a descrição anterior**, pois a pesquisa não encontrou fonte primária suficiente: Neo No Panepon, Neo Pong, Jonas Indiana and the Lost Temple of RA, Bakatonosama Mahjong Manyuuki e Chao Ji Poker. Não foram preenchidos artificialmente. O arquivo editorial registra a fonte e o limite de cada conclusão.

O gerador produziu 32 regras de substituição exata: 17 textos cortados, 14 editoriais e a regra antiga de Old Towers cujo valor recebido era somente o título. As regras incluem o texto anterior original e sua forma paginada UTF-8. O JSON de descrições conserva o texto completo, sem os separadores visuais de página.

Há **64 propostas documentais de atualização do servidor**, todas até 2.000 unidades UTF-16 e condicionadas à identidade e ao texto anterior literais. O total inclui 44 descrições R37 que já diferiam do texto publicado, além dos seis vínculos e 14 novos editoriais. As 17 descrições longas estão somente na lista nativa separada. Nenhuma proposta foi aplicada, nenhum contrato alterado e nenhuma capacidade online foi aprovada.

Uma conferência estrutural dedicada passou em **40.242 verificações**: cobertura/identidades, texto não vazio e não reduzido ao título, igualdade JSON/header, seis descritores completos, 17 prefixos e seus XMLs, 14 editoriais aplicados/cinco pendentes preservados, 32 overrides e 64 propostas CAS. A evidência está em `../evidence/synopses-structure.json`. Esses números são verificações de dados; não representam testes Android nem validação histórica individual de todos os fatos.

## Arquivos e reprodução

- `audit_existing_synopses.py`: lê as fontes existentes e gera auditoria/contagem em E:; não altera o aplicativo.
- `existing-synopses-audit.json`: todos os IDs, descrição existente, classificação estrutural e candidatos locais.
- `coverage.json`: contagens, seis faltas originais, XMLs/fontes/SHA e limites.
- `duplicate-text-groups.json`: grupos para revisão, sem classificação automática como erro.
- `collect_prefix_restorations.py`: recupera os 17 textos inteiros mediante arquivo/plataforma/prefixo exatos, sem download ou conteúdo de jogo.
- `build_synopses.py`: gera `data/synopses-complete.json`, `data/exact-alias-bindings.json`, `data/exact-server-overrides.json` e `native/station_game_infos.h` para os **2.467 IDs**, preservando textos válidos e aplicando somente vínculos exatos e alterações editoriais identificadas.
- `build_server_synopsis_candidates.py`: gera propostas CAS para revisão do operador, somente até 2.000 unidades UTF-16, e uma lista separada dos textos que ficam apenas no nativo. Não altera servidor, autorização ou contrato.
- `check_synopsis_structure.py`: confere somente estrutura, identidade, proveniência e correspondência dos bytes gerados; lê XML de metadados, nunca ROM. Salva evidência em E:.
- `../evidence/synopses-generation.json`: fontes/hashes e recibo de geração; `compiled=false`, `testsExecuted=false`, `installed=false`.

O gerador aceita `../data/editorial-synopses.json`. Cada atualização deve ter `apply:true`, ID/plataforma/nome exatos, SHA-256 e texto anterior literais, resumo original em pt-BR e fontes explícitas. Os registros `apply:false` preservam a prosa anterior. Nenhuma atualização é aplicada por similaridade.

```powershell
python -X utf8 -B audit_existing_synopses.py
python -X utf8 -B collect_prefix_restorations.py
python -X utf8 -B build_synopses.py
python -X utf8 -B build_server_synopsis_candidates.py
```

Os comandos produzem dados/fontes em E:. A conciliação do output para o snapshot R78 é uma etapa explícita. R37/R39/R77 permanecem intactos.
