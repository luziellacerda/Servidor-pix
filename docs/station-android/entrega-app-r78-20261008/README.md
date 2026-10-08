# R78 — cobertura e exibição das sinopses

Correção de apresentação sobre a candidata R77. A versão instalada nos aparelhos continua R76; esta entrega não instala nem ativa o protocolo de salas da R77.

APK assinado e conferido: `e6c6609df32bf419954083297eadbafa75f13328a1516594cc010044b2eb62a5`, 2.123.356.860 bytes. Por escolha expressa do mantenedor, permanece pronto em `E:\ESTUDO APK\work\station-synopses-r78-20261008\package-01\TurboStations-Premium-R78-20261008.apk`. G: estava sem espaço para outra cópia; nenhum backup foi apagado. Consulte `BUILD-RESULT.md` e `evidence/storage-decision.json`.

## Escopo e evidências

O catálogo publicado examinado contém 2.467 identificadores em sete plataformas, dos quais 2.212 são visíveis. Todos os visíveis já tinham prosa no índice R37. A auditoria encontrou seis IDs de compatibilidade sem texto, 17 textos cortados em 2.000 caracteres e 19 descrições curtas para revisão. A ausência percebida no telefone não foi reproduzida neste trabalho: a leitura do cache privado pelo mecanismo oficial de depuração não foi disponibilizada pelo pacote. Não se concluiu que o cache estava vazio.

Os seis IDs antigos recebem a sinopse do item visível somente quando plataforma, hash/tamanho do artefato, membro de lançamento, tamanho expandido e número de arquivos são idênticos. As 17 continuações são recuperadas dos XMLs da mesma plataforma/arquivo cujo texto começa pelo prefixo integral publicado. As ampliações editoriais têm fontes, texto anterior e identificação exatos. Veja os totais finais em `evidence/synopses-generation.json` e a revisão em `data/editorial-synopses.json`.

## Correção no aplicativo

O seletor anterior aceitava qualquer primeiro byte como descrição válida. Assim, espaços ou um aviso de indisponibilidade podiam esconder uma sinopse conhecida. A R78 usa o índice por ID e plataforma para preencher esses casos e textos que contêm somente o título. Prosa nova publicada continua tendo preferência. Melhorias editoriais e continuações só substituem a versão anterior exata, incluindo sua forma paginada; não sobrescrevem uma nova redação do servidor.

Cinco nomes de coleções Neo Geo com codificação corrompida na exportação publicada passam a localizar o texto editorial correto. Esse ajuste serve somente à sinopse: não renomeia pastas, altera membros, caminhos de download ou vídeos.

O limite de `metadata.description` no cliente permanece em **2.000 unidades UTF-16**. As descrições completas maiores ficam no índice nativo do aplicativo. O servidor não deve copiar esses textos longos para o campo atual: isso seria rejeitado pelo cliente. A paginação interna preserva o conteúdo e a rolagem continua ativa.

## Integridade

Somente `lib/arm64-v8a/libturbo_carousel.so` é substituída no APK R77. A compilação primeiro reproduz a biblioteca R77 byte a byte, depois aplica três arquivos alterados e dois cabeçalhos novos. Java, DEX, motores, controles, limite de quadros, vídeos, geometria, faixa INSTALADO, autenticação, jogos e saves não são modificados por esta correção.

Esta atualização de texto não cria identidades novas de motor. A base R77 continua dependendo da qualificação e ativação de seu contrato v3 pelo operador, conforme o handoff anterior. Não confundir preservação da R77 com liberação para substituir a R76 em produção.

## Reprodução

1. `catalog/audit_existing_synopses.py` registra o catálogo e as fontes existentes.
2. `catalog/build_synopses.py` gera os textos e o índice por vínculos exatos; copiar os resultados revisados para o snapshot é uma etapa explícita.
3. `recipes/prepare_synopsis_overlay.py` gera o seletor/aliases sobre a fonte congelada R77.
4. Congelar `OVERLAY-MANIFEST.json`; executar `recipes/build_carousel.py` em um diretório novo de E:.
5. Executar `recipes/package_candidate.py --build <diretório>` com a chave local já autorizada e certificado idêntico. O APK assinado permanece como artefato temporário em E: até a cópia final, sem sobrescrever outro APK.
6. A opção `--publish-final` exige espaço suficiente em G: e cria um arquivo novo. Nenhuma instalação é executada pelas receitas.

Consulte `STATUS.json`, `evidence/carousel-build.json`, `evidence/package.json`, `catalog/README.md` e `docs/ANALISE-EXIBICAO.md` para resultado, hashes e limites.

## Limites de qualidade

Cobertura sem texto vazio é diferente de revisão factual de todas as edições. A maioria da prosa foi preservada das fontes existentes; não foi declarada como pesquisa primária nova. Variantes, hacks, traduções e textos repetidos têm fila de revisão. Não se inventam detalhes de uma edição, avaliações ou quantidades de jogadores para completar campos.

Os testes de seleção, paginação e rolagem são locais. Não comprovam apresentação física em cada aparelho nem gameplay. O alcance é o catálogo publicado examinado, e não todos os jogos que futuramente forem adicionados ao servidor.
