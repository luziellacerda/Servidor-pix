# Handoff do aplicativo para completar a instalação Station no servidor

## Objetivo e estado comprovado

Destinatário: manutenção do Servidor-pix. Objetivo: fechar os dados necessários para o TurboStations baixar e abrir jogos usando exclusivamente `/v1/station/*`, com licença do comprador e sem divulgar o armazenamento interno.

O aplicativo possui agora um cliente independente escrito em Java e compilado para Android. Ele segue o código de `StationService.cs`, `StationEndpoints.cs` e `StationLibrary.cs` no commit `b1159c9`. O cliente ainda não foi integrado ao APK e não é uma versão liberada ao consumidor.

O último fetch de todos os ramos confirmou `07b4cab` como auditoria de produção mais recente. Essa auditoria informa falhas 404 nas capas e não comprova uma transferência completa de jogo. Este documento não solicita alterar serviços de outros produtos nem reiniciar a API antes de validar as alterações.

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

Foram concluídas 123 verificações locais e a geração do módulo Android. Ainda faltam a integração com a interface nativa, a instalação orientada pelos metadados confirmados e a verificação autenticada no aparelho. Este handoff não declara o APK como estável.
