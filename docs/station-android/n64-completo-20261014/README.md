# N64 — revisão completa da coleção

O levantamento abrange as **157 edições realmente servidas**, identificadas pelo
itemId, conteúdo e cabeçalho da ROM. São **120 edições com multiplayer documentado**
e **37 individuais**. Dentre as multiplayer: 85 permitem até quatro humanos,
duas até três e 33 até dois, conforme a modalidade.

- [Lista completa, modos, orientações e manuais](LISTA-COMPLETA.md).
- [Decisões por edição, com hashes dos manuais originais](complete-review.json).
- [Preparador para vincular as decisões ao motor exato recebido](prepare_complete_n64.py).

São 321 decisões: 157 classificações individuais e 164 modos multiplayer.
Os números de humanos dizem respeito a controles independentes. As instruções
distinguem aventura, batalha, minijogos, turnos e tabuleiros independentes.
O servidor não cria um modo multiplayer em jogos que não o possuem.

## Publicação e comprovação

O estado aplicado e as provas atuais ficam em `server-return.json`, após a
publicação protegida e a verificação assinada. Os recibos históricos das
R151/R152 e o ledger R133 continuam preservados. Este levantamento amplia a
cobertura; não altera os binários dos motores, os jogos ou as capas.

Uma prova com participantes sintéticos verifica licenças, capabilities,
admissão, portas, links, barreira e recuperação. Não executa o emulador Android
nem homologa o desempenho dos 157 jogos em aparelhos físicos.

## Próximo kit aceito pela produção

1. Receber o kit da versão final autorizada, verificando os arquivos e hashes
   exatos do APK. Não cadastrar revisões cujo kit esteja retido pela produção.
2. Usar o preparador R139 e o registrador vigentes para o cadastro original
   de quatro motores/78 modos. Essa etapa mantém também os nove modos GameCube
   e três Wii. Não transportar aprovações PS1 HLE de outro motor.
3. Obter novamente o catálogo, perfis, motores e modos efetivos, com seus bytes
   originais e a trava `scan.lock`. Usar `prepare_complete_n64.py` com a revisão
   completa e o `engines-apk.json` do kit recebido. Ele prepara somente decisões
   N64 e rejeita ROM diferente, edição nova sem revisão ou política de controles
   divergente. O diretório de saída deve ser novo.
4. Passar o `incoming.json` e o `reviewed-modes.json` resultantes ao comando
   `prepare` do registrador local, com as bibliotecas reais do kit; conferir o
   candidato e executar `apply` contra a mesma configuração efetiva. O registrador
   sela a baseline, preserva os registros anteriores e publica sem reiniciar a API.
5. Conferir capabilities assinadas para o novo engineId, conteúdo e hashes.
   Atualizar o recibo de aplicação e testar o app na mesma revisão em todos os
   participantes. O leitor Android já usa esses campos e as quantidades permitidas.

Os argumentos do preparador são `--catalog`, `--profiles`, `--modes`,
`--runtime-receipt`, `--review-ledger`, `--profiles-module`,
`--r128-operator-directory` e `--output`. Todos são caminhos locais para os
arquivos efetivos ou para os auxiliares revisados; nenhuma chave do app permite
publicar motores pela API.

## Novas versões visuais recebidas durante a revisão

O retorno `6775e8ea` confirma a R156 instalada no A56 para teste visual,
APK `a072af70c04f0b9f9455b22a856bcdf0995b42da5650c2f9ee87243d17f5d5a6`,
fonte `4d2c6b1c73c63204e7928b4aa556b668035fa6f3`. A aprovação visual permanece
pendente e o kit R156 ainda não foi gerado. Os kits R153/R154/R155 estão retidos.
Este trabalho usa os kits R152/R151 recebidos e autorizados; seus IDs diferem
dos runtimes das novas revisões. A revisão completa está disponível para o
próximo kit aceito. As fontes e os recibos visuais permanecem intactos, sem
aplicação dos kits retidos ou atribuição de homologação física pelo servidor.
