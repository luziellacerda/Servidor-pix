# PEDIDO AO SERVIDOR — analisar o aplicativo completo e localizar a falha da integração Station

**Solicitação expressa do mantenedor, 03/10/2026: criar uma nova branch com o app atual, explicar integralmente seu funcionamento e pedir ao servidor que analise onde o próprio app pode estar errado.**

Este arquivo é **um pedido da equipe Android, não um retorno nem uma prova de correção do servidor**. A branch é exclusivamente documental, baseada no retorno64912e1f. Não executar implantação por consequência da leitura.

## 1. Exatamente qual aplicativo analisar

| Referência | Valor fixado |
|---|---|
| Repositório | `luziellacerda/TurboElden` |
| Nova branch do app | `revisao-integracao-station-servidor-20261003` |
| Commit completo da entrega para revisão | `db68b613cda008052afef8152400b9c595dfcffa` |
| Commit de runtime incluído na entrega | `629a55a8cf48722460007944cf0bb737e9f8fb75` |
| Fontes | `versions/station-reconstruction-20261002/` |
| Pacote instalado | `org.turboramastation.frontend` |
| APK SHA256 | `f5b35419fcff4188b8e86690045371892c77933e7f8edfc07bce1d5bd25d418a` |
| APK tamanho/versionCode/versionName |1902718870 bytes /11 /1.0.8-turboeden-unico |
| Caminho do APK no Windows | `E:ESTUDO APKwork	urbostations-reconstruction-20261002uildapkTurboStations-Station-CANDIDATO-20261003.apk` |
| Fonte canônico Windows | `E:ESTUDO APKwork	urbostations-reconstruction-20261002` |
| Branch deste pedido | `docs/revisao-integral-app-station-20261003` |
| Base servidor deste pedido | `64912e1f2294b99967fc638d842f505cff7f629c`, branch `feat/station-artifact-descriptor-20261002` |

Não analisar o APK43670211 como se fosse atual. O alias megadrivebr e outros aliases já foram incorporados ao f5b35419. O transporte reconstruído não é o antigo StationTransfer/URL station.invalid.

## 2. Documentação completa disponível agora

1. **[Handoff integral — cópia neste repositório](revisao-app-20261003/HANDOFF-REVISAO-INTEGRAL-APP-STATION-20261003.md)**:549 linhas;14 seções; arquitetura, nove rotas/campos/assinaturas/TTLs, caches, seleção nativa, download, descritor, instalação, caminhos, build, evidências, erros do cliente e roteiro de diagnóstico.
2. **[Mapa e integridade — cópia local](revisao-app-20261003/APPENDICE-MAPA-E-INTEGRIDADE.md)**:72 mapeamentos exatos de plataforma;32 substituições nativas;51 arquivos inventariados;24/24 hashesJava dos testes correspondem ao commit publicado.
3. **[Manifesto de fontes](revisao-app-20261003/source-review-manifest.json)** e **[proveniência das cópias](revisao-app-20261003/copy-manifest.json)**.
4. **[Fonte exato do aplicativo](https://github.com/luziellacerda/TurboElden/tree/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002)**. É necessário ler esses arquivos, além do resumo.
5. **[Handoff canônico no app](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/docs/server/HANDOFF-REVISAO-INTEGRAL-APP-STATION-20261003.md)**.
6. **[Prova atual de instalação e chamadas](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/closure-validation.json)** e **[contagens e limites da verificação](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/route-count-crosscheck.json)**.

Conferência adicional nesta entrega: os51 arquivos de fonte/scripts/testes publicados foram comparados com a pasta canônica de montagem emE:, sem arquivo faltante nem divergência de conteúdo; somente finais de linha foram normalizados para comparação. As cópias preservam o texto; somente links relativos foram convertidos para links no commit fixo do app. O manifesto registra hashes de origem e cópia. Não há APK, senha, código de ativação, Bearer ou chave privada anexados. Se o acesso ao código do app estiver indisponível, informar esse impedimento antes de afirmar que ele foi analisado.

## 3. Estado que deve orientar a investigação

- Sessão, perfil e catálogo:200. Catálogo novo da rede:996 itens.
- Exportação nativa:176 Super Nintendo,28 Super Nintendo BR,693 MegaDrive e99 em outras cinco plataformas, total996.
- **Não há histograma bruto HTTP sanitizado capturado nessa execução**;176/28 vêm da exportação nativa. Obter correlação antes de concluir onde a lista perdeu itens.
- Capa:404 STATION_COVER_NOT_FOUND. Autorizar:404 STATION_ITEM_NOT_FOUND. Nenhuma transferência começou.
- Nenhuma capa200, instalação nova, jogo ou retorno foi comprovado pelo fluxo reconstruído.
- O candidato servidor1816 ainda está declarado não publicado no retorno64912e1f. São644snes+191snesbr+887megadrive+94megadrivebr. Conciliar com IDs efetivos e preservar demais plataformas.
- Os códigos404 não identificam sozinhos a causa. No fonte candidato, ITEM_NOT_FOUND é ID inválido/ausente no índice; artefato ausente usa outra falha. A DLL antiga publicada ainda precisa ser relacionada ao fonte correto.

## 4. Revise primeiro estes erros e limites do app

| ID | Verificação requerida |
|---|---|
| APP-01 | Caminho vazio da capa marca coverFailed nativo sem retry automático; cooldownJava não reprograma a fila |
| APP-02 | Renovação de sessão por worker de capa pode revogar a sessão do grant ainda não consumido |
| APP-03 | Platform desconhecida aborta todo catálogo; comparar valores brutos com mapa completo |
| APP-04 | Limite4096/12MiB sem paginação, incompatível com acervo histórico12346 |
| APP-05 | Diagnóstico não correlaciona IDs entre seleção, requisição e índice |
| APP-06 | Pin/chave pública únicos e clientVersion1 não distinguem cadaAPK |
| APP-07 | Revisões regressivas recusadas; mesma capa/revisão mantém cache antigo |
| APP-08 | Launcher preservado não validado com caminhos novos .station-v2 |
| APP-09 | Reuso antigo depende de authorize; gerações antigas acumulam |
| APP-10 | Legado nativo ainda existe; não houve recuperação integral do frontend |
| APP-11 | Texto do app atribui404 ao arquivo do servidor sem prova suficiente |
| APP-12 | Timeout pode aparecer como cancelamento |
| APP-13 | Atualizar catálogo não atualiza perfil; checkbox não revoga licença |
| APP-14 | ready() não compara sessão autorizada com objeto live; revisar concorrência |

O handoff integral informa métodos/linhas, classificação estática versus hipótese e cenário de reprodução. Não substituir essa revisão por mais um texto afirmando que o app está correto.

## 5. Entrega técnica solicitada ao servidor

### A. Identifique a primeira divergência

Rastrear na mesma sessão/item: payload assinado do catálogo → itemId/coverId antes/depois do mapeamentoJNI → seleção nativa → POST efetivamente emitido → ID recebido pelo processo → ContainsItem/ReadCover → arquivo/descritor → resposta.

Correlacionar de forma privada ou pseudonimizada, sem tokens/envelopes/dados pessoais no Git. Registrar método/rota, horário, contexto de teste, resultado e código-fonte responsável. Localizar onde o valor ou estado deixa de corresponder. Se não houver prova, dizer qual elo falta, em vez de atribuir a causa por suposição.

### B. Confira a implantação que realmente atende o app

Para as nove rotas, registrar origem/vhost/upstream, serviço e hash da DLL efetiva, fonte correspondente, revisão/hash do índice carregado e histograma bruto. A origem compilada é https://app.lzgames.com.br/v1/station/. Não assumir que catálogo/capa/autorizar chegam ao mesmo índice só porque compartilham o domínio.

A conta de serviço precisa ler os arquivos finais. Fonte compilado, /ready200 ou arquivo no disco do operador não comprovam isso. Ler ledger real/migrations antes de qualquer plano de publicação. Preservar os demais serviços e dados.

### C. Concilie a lista e demonstre os bytes

Preservar IDs publicados e outras plataformas ao juntar o candidato1816. Revisão superior à efetiva. Separar SNES644 de SNESBR191; os835 são a soma, não835 na célula comum. Não usar IDs candidatos soltos/nomes aproximados/XML antigo como resposta do telefone.

Escolher item do catálogo da mesma sessão e demonstrar capa200, authorize200 com itemRevision/descritor completos e artefato200 de uso único com tamanho/SHA256 corretos. Conferir replay, interrupção, renovação em165s e expiração de grant. No Android ainda será preciso instalar, jogar e retornar preservando dados.

### D. Devolva correções nos dois lados

Se o defeito estiver no app, especificar arquivo, método, linha, cenário de reprodução e diff/proposta. Se estiver no servidor/índice/proxy, apresentar a mesma precisão. Não recomendar desligar validações ou inventar campos para contornar incompatibilidade.

## 6. Onde responder e como evitar outro handoff ambíguo

**Atualizar o retorno canônico existente**:

`docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md`

Adicionar seção datada **“Revisão integral do app Station de03/10/2026”**, referenciando este pedido e o commitApp `db68b613cda008052afef8152400b9c595dfcffa`.

Retorno mínimo obrigatório:

1. Commits completos efetivamente lidos e APK/hash examinado.
2. Resultado individual de APP-01 aAPP-14: confirmado/refutado/não reproduzido, evidência e ação.
3. Tabela das nove rotas e binário/índice efetivos.
4. Primeira divergência comprovada ou elo ainda não observado.
5. Histograma/revisão do catálogo autenticado e conciliação deIDs.
6. Evidência sanitizada de capa/descritor/artefato por uma sessão correlacionada.
7. Separação entre código, teste isolado, produção e aparelho.
8. Mudanças necessárias no cliente e servidor, com responsáveis/sequência e impedimentos exatos.

Esta entrega documental não reinicia5192, não altera índice/banco/proxy, não instalaAPK e não promove a estável. O inventário Linux dos guias do repositório deverá ser executado pelo operador autorizado no servidor; não foi executado neste Windows. O mantenedor aguarda análise nova, específica e apoiada no código do aplicativo indicado.
