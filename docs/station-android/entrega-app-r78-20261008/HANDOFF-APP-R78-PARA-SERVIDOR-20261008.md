# APP → SERVIDOR — R78: sinopses — 08/10/2026

## Pedido e base

O mantenedor pediu eliminar jogos sem sinopse. A R78 corrige a apresentação sobre a candidata R77, cujo commit executável é `ba4fee556ec0010bbde1287746c68a2882cd1e39`, recibo `04a58b7`. A entrega R77 ao servidor é `6c52830f121ff5f3fae403c64a49ace11153cc0e`. O último retorno real analisado é `ae77b9cca7fc881771bdb809a308e40bafaad1c5`, ramo `docs/station-r77-handoff-review-20261008`; este documento é nosso envio, não confirmação do operador.

O novo retorno foi recebido durante esta compilação. Confirma a leitura da candidata, sem ativação v3. Os perfis atuais não aprovados recusariam novas salas v2/v3 se os flags fossem ligados; o catálogo ainda precisa publicar `contentSha256`, e os IDs v3 não devem ser inseridos no registro legado. São dependências da R77 preservadas pela R78, sem relação com o preenchimento das sinopses. Leia a cópia do retorno e a análise em `docs/server/`.

## O que foi encontrado

- Catálogo publicado revisão 14: 2.467 IDs, 2.212 visíveis e 255 de compatibilidade, em sete plataformas. A exportação é a mesma usada pela R37; não é uma resposta autenticada atual dos aparelhos.
- Todos os 2.212 visíveis já possuíam prosa no índice R37. Seis IDs antigos sem descrição também não constavam do fallback legado: agora recebem o texto do mesmo artefato/membro por correspondência exata.
- 17 descrições terminavam no limite de 2.000 caracteres. Os XMLs correspondentes contêm o prefixo integral e a continuação. A R78 recupera a prosa completa no índice nativo.
- 19 textos curtos revisados: 14 ampliações/correções com referências e cinco preservados por evidência insuficiente. Cool World/SNES passa a descrever Jack Deebs conforme o manual dessa plataforma. S.P.Y. tem possível classificação de plataforma incorreta, registrada para revisão separada.
- O seletor antigo priorizava qualquer string não vazia: espaços e avisos podiam ocultar prosa conhecida. A correção cobre texto vazio, espaço, placeholder exato, título isolado e substituição editorial de uma redação anterior exata.
- Cinco caminhos corrompidos de coleções Neo Geo da exportação agora localizam a sinopse editorial. Não houve alteração de rotas, IDs, associação de jogos ou vídeos.

## Contrato que o operador deve preservar

`StationCatalog` aceita `metadata.description` com **no máximo 2.000 unidades UTF-16**. Ele rejeita textos maiores; não os trunca. **Não copiar as 17 descrições longas para esse campo.** Elas são fallback nativo, listadas em `data/native-only-descriptions.json`.

`data/server-synopsis-candidates.json` contém propostas documentais que cabem no contrato. Não é uma nova API e nenhuma proposta foi aplicada. Antes de incorporar, comparar literalmente todos os campos de `precondition`: ID, plataforma, revisão do item, SHA do artefato, descrição anterior e hash do texto. Qualquer divergência exige nova análise, sem troca por nome aproximado. Preservar os demais metadados, ROMs e autorizações e usar o fluxo de assinatura/versionamento já existente no servidor.

O renderer recebe ID, plataforma e descrição. O fallback de sinopse confere **ID + plataforma**, e as substituições editoriais conferem o texto anterior exato, tanto bruto quanto paginado. A revisão/hash do artefato constam da proveniência de geração; não são uma nova validação de conteúdo no renderer. Uma prosa nova válida enviada pelo servidor continua prevalecendo. A política de jogadores e perfis online da R77 permanece independente.

## Resultado e reprodução

O índice R78 contempla os 2.467 IDs sem texto vazio. Isso comprova cobertura do catálogo publicado, não revisão factual integral de todos os textos e variantes. Leia `catalog/README.md`, `data/editorial-synopses.json` e `docs/ANALISE-EXIBICAO.md`.

`evidence/synopses-generation.json` registra os dados; `evidence/carousel-build.json` registra reprodução da base e testes; `evidence/package.json` e `STATUS.json` identificam APK, certificado, local, alteração e preservação integral das entradas. A receita substitui somente `lib/arm64-v8a/libturbo_carousel.so` sobre o APK R77 exato. Java, DEX, runtime, cores, engines, 59 vídeos, controles e dados não mudam nesta revisão.

Os testes locais incluem catálogo inteiro, prosa nova com prioridade, textos antigos paginados, coleções, rolagem e regressões de navegação/vídeo/geometria. Não houve instalação ou conferência visual física desta versão. O acesso oficial ao cache privado do Motorola não foi disponibilizado; isso não prova cache vazio nem identifica sozinho a causa do relato na tela.

## Pedido ao servidor

1. Comparar o catálogo atual com a revisão 14 aqui examinada e informar IDs novos ou alterados sem sinopse.
2. Revisar/incorporar somente propostas cujas precondições coincidam; devolver quantidade aplicada/rejeitada, revisão e hashes, preservando o limite de 2.000 UTF-16.
3. Informar se há descrições vazias, espaços, placeholders ou títulos isolados nos dados atuais.
4. Revisar separadamente a classificação de S.P.Y.; não mudar plataforma por efeito deste pacote editorial.
5. Manter separado o retorno sobre contrato, perfis e ativação v3 da R77. R78 não exige novos motores apenas por esta mudança de texto.

Nenhum deploy, reinício Linux, alteração de catálogo em produção ou operação nos aparelhos foi executado. Samsung e Motorola permanecem na R76; a candidata R77/R78 ainda requer a qualificação descrita no handoff anterior antes de substituir a versão de uso.
