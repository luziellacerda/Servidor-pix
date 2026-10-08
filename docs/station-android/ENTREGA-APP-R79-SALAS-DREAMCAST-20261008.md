# R79 Correção do app e pendências do servidor

Fonte imutável: `b377db3981c96374fbe5591700db91dfbe297994`, ramo `fix/station-room-bootstrap-r79-20261008`.
[Fontes, receitas e evidências](https://github.com/luziellacerda/TurboElden/tree/b377db3981c96374fbe5591700db91dfbe297994/versions/station-room-bootstrap-r79-20261008).

Pai `525f03735cbec194e942c91417d38a4d799c8910` é a entrega anterior do app. Último retorno real: `af58034595614098ea4df4d959e50947670c080f`.
Esta publicação não implanta Linux nem ativa salas. A falha local de rota foi corrigida; a autorização das novas salas, inclusive duas pessoas, continua exigindo os perfis e o contrato previstos.

# R79 Diagnóstico das salas e cobertura de Dreamcast

## Falha identificada no aplicativo

No Motorola com R78, a pilha passa por `StationRoomsActivity.connect` → `StationOnlineClient.discoverMultiplayer` → `StationApi.multiplayer` → `StationHttp.exchange:59`. A linha rejeita a rota antes de abrir a conexão. O método `allowed` aceita os endpoints antigos `online/command` e `online/events`, mas não aceita `online/multiplayer/command`.

A tela mostra “Indisponível”, “Preparando seu jogo…” e “Capa indisponível”. O título de Battletoads já existe no catálogo. A reprodução em Java com as classes compiladas demonstra o bloqueio local; não atribuir essa consulta ausente a atraso ou recusa do servidor.

A R79 permite somente a nova rota POST contratada, limita seu corpo a 8.192 bytes, mantém todos os controles de segurança e apresenta o snapshot social antes de investigar a modalidade opcional. Falha nessa investigação não apaga pessoas/conversas, capa ou metadados disponíveis. Consulta e polling são cancelados ao sair/trocar a geração da tela. Nome/plataforma/capa carregam independentemente da autorização para criar partida.

## Novas salas ainda exigem publicação coordenada

Último retorno real lido: `af58034595614098ea4df4d959e50947670c080f`, branch `docs/station-r78-server-review-20261008`, recebido antes da publicação R79. O commit `525f037` é a entrega anterior do app, não uma resposta do servidor. O retorno confirmou a revisão 18 do índice efetivo: 3.734 IDs, 3.479 visíveis e 335 descrições vazias, incluindo todos os 243 Dreamcast. Produção continua v2; nenhuma proposta editorial ou ativação v3 foi aplicada.

O app R77/R78 exige perfis aprovados para criar e entrar em novas salas. Isso afeta também duas pessoas. A R79 mantém essa validação e as identidades nativas R77; corrigir o POST não ativa v3 nem torna os engines rs4 compatíveis por renomeação. Os motores e perfis propostos não devem ser inseridos no registro legado v2. Preservar sessões e engines existentes e coordenar qualquer recarga com o mantenedor.

O operador deve devolver:

1. Contrato v3 efetivo, caminhos registrados, capabilities assinadas e perfis exatos aprovados por jogo/modo/quantidade, incluindo a continuidade dos títulos de duas pessoas.
2. Catálogo assinado com `contentSha256` do conteúdo realmente aberto, conforme os requisitos anteriores; acrescentar o valor apenas ao JSON privado não o expõe pelo modelo atual.
3. Evidência de ativação com commit/DLL/PID/horário, testes de migração e plano de retorno. Não basta dizer que o arquivo de perfis foi copiado.
4. Resposta de `capabilities` para Battletoads e para o piloto Bomberman, sem tokens, chaves, dados pessoais ou licenças no Git. Até a resposta, a interface deve informar a pendência e não liberar vagas por `metadata.players`.

## Dreamcast precisa do catálogo atual

A captura do telefone tem 243 jogos de Dreamcast. O primeiro, `102 Dalmatians - Puppies to the Rescue`, não mostra descrição. O export revisão14 usado na R78 não contém esse sistema. O retorno R78 agora entrega os 1.267 IDs novos e as 335 lacunas da revisão 18, incluindo os 243 Dreamcast; não repetir o pedido desse mesmo export. Preservar o catálogo anterior e incorporar somente propostas revisadas por ID/revisão/texto anterior, após pesquisa dos ausentes.

R79 recupera 461 títulos distintos do XML local `metadata-sources/catalog-xml/11-dreamcast.xml`. São descrições vinculadas ao título completo e à plataforma, sem aproximação, sem remover região/pontuação e sem usar essa correspondência como prova de quantidade de jogadores. Cinco títulos conflitantes foram excluídos. O XML contém idiomas variados; não alegar tradução integral ou revisão factual de todos os textos.

O cruzamento documental da revisão 18 encontrou 142 dos 243 nomes pelo normalizador exato da R79 e 101 sem correspondência; lista em `evidence/dreamcast-revision18-audit.json`. Não altera o APK instalado nem comprova todas as fichas no aparelho. Pesquisar/revisar os ausentes e converter propostas em vínculos de ID/revisão antes de publicação no servidor. Alterações propostas ao servidor devem usar comparação com ID, revisão e texto anterior, respeitando o limite atual de 2.000 unidades UTF-16 de `metadata.description`. Texto novo válido do servidor prevalece no app; a base local não substitui publicação correta.

## Entrega verificável

APK SHA-256 `c2aeee4443dedc2862b29dde1f972574bc25464d6993abf01f34d43531558f0a`, 2.123.656.192 bytes. Classes28 `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`; classes35 `ed7fe3c352de9f9032dd6581f356ae405570d510915aca138a82a82f5e8f3615`; carrossel `b77f5c06effe7750f526ef06b8515c7eb4954437495f65e4d5bec25c1bd36808`.

As 13.223 entradas restantes, inclusive os 59 vídeos, motores, cores, engines e assinatura, foram preservadas. Não há novo cadastro de engine por esta correção Java/apresentação. Consulte `evidence` para testes, recibos de instalação e conferência física posterior. Esta entrega não implanta Linux, não reinicia serviços e não comprova partidas de dois/três/quatro jogadores.

Conferência posterior no Motorola: instalação e abertura autenticada confirmadas; capa/nome/plataforma/avaliação de Battletoads visíveis, indicador Online e aviso de ativação pendente. Sinopse de Bust-A-Move 4 visível no Dreamcast. Sem criação de sala nem gameplay. Não interpretar os registros genéricos REQUEST_FAILED com status200 como falha HTTP: o classificador histórico de diagnóstico não distingue todas as rotas online.
