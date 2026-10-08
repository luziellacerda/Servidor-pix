# Pesquisa de informações por jogo — R77

## Escopo e resultado

O cruzamento visita todos os **2.467 IDs** do catálogo publicado revisão 14: **2.212 visíveis e 255 IDs de compatibilidade**. A base do catálogo não é uma consulta autenticada atual à produção.

`catalog-game-metadata.json` registra informação campo a campo, sua origem e o que continua desconhecido. Foram consultados 31 arquivos públicos de dados/documentação na revisão `fbeefcb46c2e1b20a7e2945f34a694a41b2d6f90` de [Libretro Database](https://github.com/libretro/libretro-database/tree/fbeefcb46c2e1b20a7e2945f34a694a41b2d6f90), incluindo as importações mantidas No-Intro e Redump. Outros 16 caminhos não existem nessa revisão; essa ausência consta do manifesto.

| Resultado de identificação | IDs |
|---|---:|
| Nome de edição e tamanho exatos, como candidato descritivo | 1.454 |
| Sem correspondência exata | 734 |
| Nome encontrado, tamanho ou recipiente diferente | 279 |

Campos encontrados nas correspondências candidatas: desenvolvedor em 1.439, editora em 1.442, gênero em 1.446, ano em 1.155, mês em 700, máximo descritivo de jogadores em 1.435, franquia em 489 e classificação etária em 408. São contagens dos IDs internos, incluindo aliases, e não jogos distintos.

**A pesquisa factual completa ainda não terminou.** A correspondência de arquivo, edição e tamanho não é uma comparação do conteúdo instalado com o checksum da base. Nenhuma ROM foi lida ou obtida nesta etapa. Traduções, hacks, diferenças regionais e arquivos de tamanhos diferentes continuam sem associação automática. Não se usa aproximação de nomes para aprovar a identidade.

## Limites de uso

- O CRC associa os registros de metadados dentro da base Libretro. Não foi apresentado como CRC calculado do download da Station.
- `users`/`maxusers` pode representar alternância, antologias ou modos diferentes. Não informa sozinho o número de jogadores simultâneos e não autoriza uma sala de 2, 3 ou 4 pessoas.
- `description` nessas bases frequentemente repete o título. Esse campo não foi utilizado como sinopse.
- Sinopses e avaliações não existentes nas fontes ficam explicitamente desconhecidas. Não se atribuiu uma nota padrão aos jogos.
- Os modos descritos nos manuais originais permanecem como pesquisa separada em `mode-evidence.json`; variações traduzidas e hashes do conteúdo ainda exigem conferência própria.
- Todos os registros desta pesquisa têm `onlineApproved: false`. Os perfis assinados do servidor e os testes do motor constituem uma etapa diferente.

## Proveniência e licença

Cada campo possui `sourceRefs` com caminho e linha; `sources` informa URL pública, SHA-256 e revisão. A licença publicada pelo repositório é [CC BY-SA 4.0](https://github.com/libretro/libretro-database/blob/fbeefcb46c2e1b20a7e2945f34a694a41b2d6f90/LICENSE), preservada em `LIBRETRO-DATABASE-LICENSE.txt`. A parte derivada desses metadados mantém essa licença e a atribuição ao Libretro Database e às origens declaradas nos cabeçalhos dos DATs. Isto não muda a licença do código da aplicação.

O manifesto completo está em `metadata-source-manifest.json`; a cobertura e hashes em `metadata-coverage.json`. Os arquivos DAT originais permanecem na área de pesquisa em E:, sem ROMs ou mídia.

## Reprodução

1. Executar `fetch_metadata_sources.py` para obter os arquivos da revisão fixa em E:.
2. Executar `build_game_metadata.py` usando o inventário publicado.
3. Conciliar os dois JSON produzidos com este snapshot e executar `run_metadata_tests.py`.

Os testes conferem parser, cobertura, proveniência e a ausência de aprovações implícitas. Não certificam os fatos históricos nem a compatibilidade dos jogos no telefone.

## Próximo vínculo de identidade

`bind_content_hashes.py` foi ampliado para produzir SHA-256, SHA-1, MD5 e CRC32 do arquivo efetivamente iniciado, após conferir o artefato publicado. Para ZIP, lê apenas o membro exato, valida os limites do descritor e confere novamente o hash do ZIP; para raw, confere que os bytes não mudaram. Não extrai nem publica arquivos e não remove cabeçalhos ou normaliza a ROM.

Esses recibos permitem uma próxima conciliação exata com as bases. Ainda não existem recibos de conteúdo dos jogos nesta pesquisa, e o pipeline de metadados não promove candidatos por nome/tamanho a `verified-exact-content`. A ausência continua explícita; os 1.435 valores `maxusers` encontrados permanecem dados descritivos de pesquisa, sem preencher o selo factual ou autorizar salas.
