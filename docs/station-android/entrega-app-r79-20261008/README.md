# R79 Corrigir abertura das salas e recuperar textos de Dreamcast

A falha observada no Motorola ocorre no aplicativo antes de enviar a consulta de capacidades ao servidor. `StationApi.multiplayer()` usa `POST /v1/station/online/multiplayer/command`, mas o `StationHttp` incluído na R77/R78 não permite essa rota. A exceção interrompe a abertura do lobby antes de apresentar o snapshot social. A capa e os dados locais também esperavam esse snapshot.

A R79 inclui a rota POST exata, mantém o limite de 8.192 bytes nessa consulta e preserva TLS, pin, autenticação, provas de requisição e assinatura das respostas. O snapshot social é apresentado antes da consulta opcional, que passa a executar separadamente. Capa, título e plataforma carregam do catálogo local independentemente do resultado dessa consulta. O nome e a plataforma também não dependem do arquivo auxiliar de avaliações.

O carrossel recupera descrições de 461 títulos distintos de Dreamcast do XML local. A correspondência exige plataforma `Dreamcast` e título completo, permitindo apenas diferenças de caixa ASCII e espaços. Cinco títulos com descrições conflitantes foram excluídos. Textos válidos novos do servidor prevalecem. Essa consulta serve exclusivamente à descrição e não autoriza vagas, controles, avaliações ou motores.

## Cobertura e limites

A tela capturada tem 243 jogos de Dreamcast. A lista completa atual ainda precisa ser fornecida pelo servidor para medir cobertura; os 461 títulos da base de referência não equivalem a 461 jogos do catálogo atual. O XML de origem contém textos em mais de um idioma e sua recuperação não representa revisão factual integral nem tradução para português. O primeiro caso observado é `102 Dalmatians - Puppies to the Rescue`.

A R78 foi preparada sobre um export antigo de sete sistemas e 2.467 IDs, que não incluía Dreamcast. O XML R39 tinha nomes e metadados, sem descrições. A ausência não deve ser atribuída à lentidão de rede.

O último retorno real do servidor continua `ae77b9cca7fc881771bdb809a308e40bafaad1c5`: produção v2, perfis v3 e conteúdo exato pendentes. R77/R78 passaram a exigir perfis para novas salas, inclusive de dois participantes. R79 corrige a apresentação e a consulta, mas não libera partidas ignorando essa exigência. Não afirmar que somente salas de três/quatro jogadores estão pendentes.

## Artefato e validação

APK local em `E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008\package-01\TurboStations-Premium-R79-20261008.apk`, SHA-256 `c2aeee4443dedc2862b29dde1f972574bc25464d6993abf01f34d43531558f0a`, 2.123.656.192 bytes. Apenas `classes28.dex`, `classes35.dex` e `libturbo_carousel.so` mudam sobre R78; 13.223 entradas e os 59 vídeos foram conferidos. Runtime, cores, controles, identidade de engines e política térmica permanecem os mesmos.

Foram compiladas 209 fontes Java, com duas alterações e 207 preservadas. A biblioteca nativa R78 foi reproduzida byte a byte antes da correção. Testes locais: 22 verificações na rota antiga e 22 na corrigida, 29 cenários/assertivas do método de descoberta com callbacks controlados, cinco guardas de integração e 4.156 verificações de busca/precedência de descrições. Não são testes de partida real.

`evidence/package.json` descreve o pacote no momento da geração. Recibos `evidence/installation-*-r79.json` e `evidence/physical-check.json`, quando presentes, são posteriores e distinguem instalação de conferência visual. Fontes, receitas e hashes ficam neste snapshot; capturas e logs privados permanecem somente em E:.

## Conferência no Motorola

R79 instalada em 08/10/2026 às 15:12:38 UTC, com hash integral e identidade original conferidos. Abertura autenticada confirmada na ESActivity. Em Dreamcast, Bust-A-Move 4 passou a mostrar a sinopse do XML em espanhol. Em Criar sala, Battletoads voltou a mostrar capa, nome, plataforma e avaliação, com indicador Online. A tela informa que a modalidade aguarda ativação; nenhuma partida foi iniciada e nenhuma sala foi criada nesta conferência. O Samsung continua na R78.

## Retorno recebido antes da publicação

Servidor `af58034595614098ea4df4d959e50947670c080f` confirma revisão 18, 243 Dreamcast sem descrição no servidor. O cruzamento exato com o fallback R79 encontra 142 títulos e deixa 101 sem correspondência; consulte `evidence/dreamcast-revision18-audit.json`. Não representa tradução integral, validação factual de todos os textos ou conferência de todos os jogos no telefone. Produção segue v2 e as salas R79 continuam dependentes da integração v3.
