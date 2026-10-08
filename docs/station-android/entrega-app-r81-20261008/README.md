# R81 — cliente completo e fechamento da ativação online

APK completo compilado e assinado, ainda não instalado. Substitui a R79 no canal experimental; a referência R76 de dois jogadores permanece preservada. A fonte parte das **209 classes Java completas** do backup canônico R79. As fontes usadas nesta compilação estão integralmente em `java/`, com `JAVA-SOURCE-MANIFEST.json`.

## Correções comprovadas

- O contrato do servidor `b472d8a` admite classificações individuais (`maximumPlayers:1`, `allowedPlayerCounts:[]`). A R79 rejeitava esse perfil e abortava a lista, inclusive quando existia outro modo multiplayer válido do mesmo jogo. R81 separa **classificação exibida** de **autorização para criar/entrar/iniciar salas**. Individual aparece como “1 jogador”, com explicação própria, e não concede vagas. A seleção multiplayer continua exata, aprovada e vinculada a conteúdo/motor/controle.
- Consulta HTTP 404/503 não é mais convertida indiscriminadamente em “aguarda ativação”. A mensagem de ativação depende do código explícito `STATION_MULTIPLAYER_DISABLED`; erros genéricos e de classificação conservam suas causas. A descoberta opcional apresenta o erro sem impedir nome/capa/dados locais. Reconectar executa nova consulta pelo fluxo existente.
- Perfil inválido, hash diferente, modo ambíguo ou capacidade não autorizada continuam bloqueados. Quantidade descritiva do catálogo não libera vagas.

O runtime, os cores e os IDs v3 são os mesmos da R79/R77. Esta correção **não exige cadastrar outro motor**. O efeito Dreamcast R80 continua arquivado, sem incorporação.

## Evidências

`evidence/profile-classification-tests.json` reproduz o defeito na R79 e registra **154 verificações** da R81. `evidence/readiness-tests.json` registra **43** verificações de consulta/erro, **36** de descoberta/cancelamento e **8** guardas de integração. As 209 fontes compilaram; `classes28.dex` foi reproduzido idêntico. O APK muda somente `classes35.dex`, preservando **13.225 entradas e 59 vídeos**; certificado original e alinhamento 16 KiB conferidos. Ver `evidence/java-build.json` e `evidence/package.json`.

Os testes são locais, com respostas controladas e validação de bytes. Não demonstram gameplay em Android, estabilidade WAN ou quatro jogadores. Nenhum telefone foi modificado nesta entrega.

## Arquivos atuais e reprodução

Instalador final: `G:\BAKUP SISTEMA APP 03-10-2026\ATUAL-2P-E-TESTE-4P-20261008\test-up-to-4-players-r81\TurboStations-Premium-R81-20261008.apk`.

SHA256: `85fac8f51daa3a370eabb14d98832f75c2f30ff81422c549538ee715627032c6` — 2.123.655.964 bytes.

Para próximas mudanças/reprodução, começar por `release-channels/ACTIVE.json` e `release-channels/rebuild_verified.py`. As receitas de montagem desta pasta registram a geração a partir da R79; o instalador R79 é aposentado após validar a substituição. Não recuperar fontes por sobreposições históricas. O backup atual inclui Java, carrossel/dependências, DEX, fontes de motores, histórico Git e recibos.

## Única pendência operacional consolidada

Leia **`FECHAMENTO-SERVIDOR-R81-20261008.md`**. O retorno real do servidor é `a4fd0d73a7eaaf43fafbae1c42e9b83580985433`; a candidata é `b472d8a653e065cccb00dfb15dbea3d56c03db98`. As sinopses da revisão 19 foram publicadas. O serviço v3, as identidades reais de conteúdo e os perfis aprovados permanecem sem ativação no retorno recebido.

`server-tools/` entrega uma ferramenta offline para produzir o registro estrito a partir dos artefatos reais. Ela verifica bytes e preserva identidades anteriores qualificadas; não aprova jogos ou quantidades de jogadores. A aprovação de modos/controles exige a qualificação indicada no fechamento. Esta entrega não implanta Linux nem reinicia serviços.
