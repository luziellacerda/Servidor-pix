# Handoff do servidor para concluir a conexão do TurboStations Android

## Destinatário e identificação exata

Este pedido é para a equipe ou IA responsável pelo **backend Station no repositório Servidor-pix**. Implementar e comprovar as pendências do servidor descritas abaixo e devolver o contrato final para a equipe do aplicativo.

| Identificação | Valor conferido |
| --- | --- |
| Repositório do servidor | https://github.com/luziellacerda/Servidor-pix |
| Arquivo deste pedido | docs/station-android/HANDOFF-CLIENTE-RECONSTRUIDO-STATION-20261002.md |
| Branch deste documento | docs/cliente-reconstruido-station-20261002 |
| Aplicativo atendido | TurboStations Android TESTE |
| Pacote Android | org.turboramastation.frontend |
| productId e applicationId do protocolo | TURBORAMA_STATION_ANDROID, em ambos os campos |
| Base HTTPS configurada no cliente | https://app.lzgames.com.br |
| Prefixo das rotas | /v1/station/ |
| Serviço descrito pelo handoff do servidor | turborama-station-api, loopback 127.0.0.1:5192; confirmar no ambiente antes de qualquer implantação |
| Código do aplicativo analisado | TurboElden 0840028854034b03e5a1d3f2a162d66225932a6b, versions/station-reconstruction-20261002 |

A entrega deste pedido é código/contrato/evidência do canal Station. O escopo exclui os outros produtos Turborama, Suite Windows, PIX, seus serviços e dados. As pastas E: citadas neste documento identificam o trabalho Windows do aplicativo; não são destinos de implantação Linux. Esta revisão é documental e não autoriza reiniciar ou implantar serviços.

## Ordem de execução e responsáveis

1. **Servidor — confirmar a base.** Ler AGENTS.md desta pasta e o HANDOFF-HUMANO-COMPLETO-STATION-20261002.md. Conferir código efetivo, índice carregado e versão em execução. Os commits abaixo são as referências já examinadas, não prova do estado atual do Linux. Registrar diferenças encontradas.
2. **Servidor — confirmar a ativação e o perfil.** Devolver o formato real do código emitido ao comprador, o contrato dos desafios/sessões e o campo do nome em /me. Conferir a divergência específica de ativação registrada abaixo. Preservar as regras comerciais já estabelecidas.
3. **Servidor — reparar e comprovar as capas.** Resolver coverId para o arquivo do índice carregado. Entregar resultado autenticado 200 e validar a regra de revisão/cache; a existência da rota não encerra esta tarefa.
4. **Servidor — implementar ou confirmar o descritor do jogo.** Usar a seção de extensão proposta como requisitos a fechar. Devolver os nomes finais, tipos, limites, assinatura, erros e exemplos. Metadados devem corresponder exatamente aos bytes autorizados.
5. **Servidor — comprovar o fluxo completo.** Executar os casos da seção de evidências e informar separadamente o resultado local/homologação e o resultado de produção, caso exista. Publicar o retorno no arquivo indicado ao final.
6. **Aplicativo — depois do contrato confirmado.** Corrigir a validação de ativação, ligar o catálogo ao carrossel nativo, implementar o instalador, retirar os fluxos antigos do APK e verificar no aparelho. Essas tarefas continuam sendo responsabilidade da equipe Android.

Se faltar um dado, registrar qual campo, arquivo ou evidência falta. Não preencher com valor inventado, extensão genérica, rota vazia ou exemplo tratado como configuração real.

## Divergência de ativação que precisa constar no retorno

O handoff humano do servidor descreve códigos com prefixo STA-. O cliente publicado em 0840028 chama token(code) em StationApi.activate, que exige Base64URL canônico de 32 bytes. Esses formatos não são equivalentes; um código STA- no formato documentado pode ser rejeitado pelo cliente antes da requisição.

No servidor b1159c9, CompleteActivationAsync lê activationCode com RequireString e máximo de 256 caracteres; essa validação não exige o formato Base64URL usado pelo cliente. O retorno deve confirmar o formato emitido e as regras de espaços, maiúsculas e prefixo, usando exemplos sintéticos e indicando o código responsável. A correção correspondente é no cliente Android para obedecer ao contrato real; não alterar códigos já emitidos para acomodar a validação incorreta do cliente.

As 142 verificações locais anteriores não comprovam a aceitação do código comercial STA-. Essa divergência permanece pendente nesta revisão documental.

## Objetivo e estado comprovado

Destinatário: manutenção do Servidor-pix. Objetivo: fechar os dados necessários para o TurboStations baixar e abrir jogos usando exclusivamente `/v1/station/*`, com licença do comprador e sem divulgar o armazenamento interno.

O aplicativo possui agora um cliente independente escrito em Java e compilado para Android. Ele segue o código de `StationService.cs`, `StationEndpoints.cs` e `StationLibrary.cs` no commit `b1159c9`. O cliente ainda não foi integrado ao APK e não é uma versão liberada ao consumidor.

A leitura realizada na etapa anterior, em 02/10/2026, usou `07b4cab` como referência da auditoria de produção. Esta revisão documental não executou uma nova auditoria do ambiente. Essa auditoria informa falhas 404 nas capas e não comprova uma transferência completa de jogo. Este documento não solicita alterar serviços de outros produtos nem reiniciar a API antes de validar as alterações.

## Rotas existentes consumidas pelo cliente

| Método e rota | Dados e validações usados pelo cliente |
| --- | --- |
| POST `/v1/station/activations/challenge` | Identidade do aparelho, código de ativação e chave pública; resposta assinada vinculada ao aparelho; validade de 60 s |
| POST `/v1/station/activations/complete` | Prova RSA PSS com challengeId e nonce; licença devolvida deve corresponder ao challenge |
| POST `/v1/station/challenges` | Licença e identidade; challenge com a mesma licença e aparelho |
| POST `/v1/station/sessions` | Prova do aparelho; sessão assinada com validade de 180 s |
| GET `/v1/station/me` | Nome do comprador e versão do perfil; licença, aparelho e sessionId conferidos |
| GET `/v1/station/catalog` | Catálogo assinado, até 4096 itens, campos itemId, name, platform, revision e coverId |
| GET `/v1/station/covers/{coverId}` | Sessão válida; bytes da imagem, até 5 MiB; arquivo mantido no cache local por ID e revisão |
| POST `/v1/station/downloads/authorize` | itemId e identidade; concessão assinada vinculada ao item, licença, aparelho e sessão; validade de 60 s |
| GET `/v1/station/artifacts/{grantId}` | Uma tentativa por concessão; transferência sequencial, Content-Length obrigatório, sem redirects e sem Range |

Nenhum bearer ou grant é gravado em disco pelo cliente novo. O cliente reutiliza o alias de chave Android Keystore e o identificador de licença já presentes na instalação. MAC e IMEI não fazem parte do protocolo. As respostas assinadas são validadas contra a autoridade pública Station já configurada; os pins não foram trocados nesta reconstrução.

## Lacuna confirmada no artefato

Em `AuthorizeDownloadAsync`, a resposta assinada atual termina em itemId, grantId e expiresInSeconds. Em `ConsumeArtifactPathAsync` e no endpoint de bytes, o caminho permanece privado. O endpoint transmite `application/octet-stream` e Content-Length, sem Content-Disposition, extensão, hash ou seleção do arquivo inicial.

Isso permite transportar bytes, mas não informa ao instalador se são ROM crua, ISO, ZIP, RAR, 7z ou outro contêiner. Um arquivo compactado pode conter múltiplos BIN/CUE, diretórios de Wii U ou mais de um executável. O cliente não deve fabricar `.zip` nem escolher o primeiro arquivo encontrado.

## Extensão proposta para confirmação

**Esta seção é uma proposta ainda ausente no servidor e ainda não consumida pelo cliente compilado.** Confirmar o contrato antes de ligar o instalador. Manter as rotas atuais e acrescentar à resposta assinada de autorização:

| Campo proposto | Significado obrigatório |
| --- | --- |
| `artifact.fileName` | Nome base real do arquivo servido, com extensão; sem caminhos, separadores, NUL ou componentes `.` e `..` |
| `artifact.sizeBytes` | Tamanho inteiro positivo exato dos bytes que serão transmitidos |
| `artifact.sha256` | SHA256 desses mesmos bytes, em 64 caracteres hexadecimais minúsculos |
| `artifact.format` | `raw`, `zip`, `rar` ou `7z`, conforme artefato real; não inferido pela plataforma |
| `artifact.launchPath` | Para raw, o próprio fileName; para arquivo compactado, caminho relativo exato do jogo inicial após extração, sem escapar da pasta do item |
| `artifact.expandedSizeBytes` | Para compactados, limite conhecido do tamanho total extraído; para raw, igual a sizeBytes |
| `artifact.fileCount` | Número de arquivos regulares esperados; raw usa 1 |
| `itemRevision` | Revisão do item autorizada, correspondente ao catálogo entregue |

Não enviar URL do CDN, caminho Linux, credencial ou segredo. O aplicativo verificará assinatura, item e revisão, confrontará o tamanho com o cabeçalho, validará SHA256 antes da instalação e verificará o formato dos bytes. A publicação como instalado ocorrerá somente depois da extração segura e confirmação do launchPath.

O servidor deve obter esses dados do índice/arquivo efetivamente autorizado. Calcular hashes e inspecionar contêineres na preparação do índice, evitando recalcular arquivos grandes a cada clique do comprador. O artefato concedido deve permanecer imutável durante sua validade, ou o servidor deve rejeitar a concessão quando sua identidade mudar. Não servir bytes diferentes com o mesmo descritor assinado.

O servidor pode propor outro esquema equivalente. Nesse caso, devolver os nomes e regras exatos, exemplos sanitizados e a revisão do código. O aplicativo ainda não inventa esses campos como se já existissem.

## Capas e revisão

Restaurar a correspondência entre coverId e coverPath do índice efetivamente carregado pela 5192. Demonstrar pelo menos uma resposta autenticada 200 com MIME e bytes corretos, além do 404 para um ID inexistente. O cache do cliente mantém as capas já baixadas; trocar o conteúdo de uma capa requer nova revisão ou novo coverId para que o telefone saiba renová-la.

Confirmar também se a revisão de capa é a revisão do item, como foi adotado nos handoffs anteriores. Caso dois itens compartilhem coverId com revisões diferentes, documentar qual revisão é a autoridade. A API atual não traz coverRevision separado.

## Plataformas e jogos já instalados

O catálogo documentado em ac86942 usa megadrive, snes, snesbr, gamegear, gb, sega32x, gbc e gba. Esses oito identificadores foram mapeados a pastas observadas no aplicativo. Foram preservadas, por exemplo, `super-nintendo--br`, `megadrive--br` e `nintendo-64--br`, com dois hífens.

Antes de publicar outras plataformas, devolver a lista exata dos valores de `platform`, distinguindo edições BR. Não assumir que o nome de exibição é caminho de pasta. O cliente possui 50 mapeamentos locais comprovados, mas não inventará equivalência para identificadores novos. O servidor atualmente limita o índice a 4096 itens; a lista documental de 12346 jogos não cabe nesse limite sem uma evolução explícita do contrato.

## Evidências necessárias para o retorno

1. Commit e branch com o contrato confirmado do descritor; documentar tipos, limites e códigos de erro.
2. Catálogo autenticado de teste com revisão, quantidade e valores de platform, sem tokens no handoff.
3. Uma capa válida 200 e um coverId inexistente 404.
4. Autorização de um artefato de teste permitido, resposta assinada com metadados e transferência completa cujo tamanho/hash confiram.
5. Segunda utilização da mesma concessão negada; concessão expirada negada; licença bloqueada negada; sessão de outro aparelho negada.
6. Comportamento documentado quando a conexão cai após consumir a concessão. O cliente solicitará nova autorização para reiniciar; não retomará um grant já usado.
7. Exemplos para uma ROM crua, um contêiner com múltiplos arquivos e um jogo de Wii U com diretórios. Informar o arquivo inicial de cada um.

Não usar `/ready/station` 200 como prova de que catálogo, capa e jogo funcionam. Essa prontidão confirma um conjunto menor de condições.

## Arquivos entregues pelo aplicativo

- Raiz da reconstrução: `E:\ESTUDO APK\work\turbostations-reconstruction-20261002`.
- Fonte do protocolo: `src\java\org\emulationstation\frontend\station\StationApi.java`.
- Biblioteca compilada: `build\station-client.aar`.
- Testes e hashes: `build\test-results.json` e `build\module-manifest.json`.
- Continuação do aplicativo: `HANDOFF-RECONSTRUCAO-TURBOSTATIONS.md`.

Foram concluídas 142 verificações locais, incluindo a integração do login nos fontes, e a geração do módulo Android. Ainda faltam a integração com a interface nativa, a instalação orientada pelos metadados confirmados e a verificação autenticada no aparelho. Este handoff não declara o APK como estável.

## Retorno publicado do aplicativo

A integração do login nos fontes foi publicada em TurboElden, commit `0840028854034b03e5a1d3f2a162d66225932a6b`, ramo `station-reconstrucao-20261002`.

[Fontes, testes e documentação da integração](https://github.com/luziellacerda/TurboElden/tree/0840028854034b03e5a1d3f2a162d66225932a6b/versions/station-reconstruction-20261002).

O layout de login atual passou a usar StationCoordinator para sessão, perfil e catálogo, com capas por itemId. A senha local foi retirada desses fontes. O controlador invalida a autorização quando o servidor nega a licença, reaproveita a licença salva e o cache verificável, e cancela consultas quando a tela de login fica escondida.

A etapa passou em **142 verificações locais**, com compilação Android e geração de DEX. Os exemplos são testes sintéticos, não credenciais de produção. A ligação ao catálogo nativo, o instalador orientado pelo descritor e o APK completo continuam pendentes; nenhum APK desta reconstrução foi instalado ou promovido a estável. A proposta de metadados acima permanece aguardando confirmação/implementação do servidor.

## Arquivo obrigatório de retorno do servidor

Publicar no repositório Servidor-pix:

docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md

Se esse arquivo já existir, atualizá-lo preservando evidências anteriores identificadas por data. Entregar à equipe Android o link com commit completo e informar a branch. O documento deve apontar explicitamente para este pedido e para o commit do cliente analisado.

Preencher todos os itens abaixo com evidência ou com a pendência exata:

| Item obrigatório | Conteúdo esperado |
| --- | --- |
| Revisões | Branch e SHA completo do código alterado, do contrato e dos testes; informar se houve implantação |
| Ambiente | Data, ambiente testado, serviço efetivo e hash do artefato em execução; separar desenvolvimento de produção |
| Ativação | Formato emitido ao comprador, normalização permitida, validação real e exemplo sintético |
| Perfil | Nome exato do campo do comprador, assinatura e comportamento quando o perfil ainda não estiver pronto |
| Contrato | Rotas, métodos, schemas e nomes finais dos campos; tipos, limites, códigos de erro e versão de assinatura |
| Capas | Resultado autenticado 200 e 404, tamanho/MIME/hash de exemplo permitido, regra de revisão e invalidação do cache |
| Catálogo | Revisão, total, valores exatos de platform e solução explícita caso exceda 4096 itens |
| Artefatos | Descritores sanitizados de raw, arquivo com múltiplos membros e diretórios Wii U; launchPath de cada exemplo |
| Transferência | Tamanho e SHA256 esperado/obtido; concessão usada, expirada, bloqueio e queda de conexão |
| Compatibilidade | O que continua compatível e o que requer mudança no cliente; nenhum campo proposto deve ser apresentado como já consumido pelo APK |
| Pendências | Responsável, arquivo/rota afetada e evidência faltante para cada ponto não concluído |

Não anexar credenciais, tokens, códigos reais de ativação, dados pessoais, URLs privadas nem caminhos de armazenamento de produção. Exemplos devem ser sintéticos ou sanitizados.

O resultado “servidor pronto” exige contrato confirmado e evidências dos fluxos acima. “Publicado no Git”, “compilou”, “health 200” e “ready 200” são estados distintos e devem ser identificados como tais. O APK integrado continuará pendente até a conclusão e a verificação da etapa Android.
