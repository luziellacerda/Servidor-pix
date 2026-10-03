# Handoff técnico único: servidor, conexão e instalação do TurboStations Android

Atualizado em 03/10/2026, 14h12 (America/Maceio). Preserva as evidências de 02/10/2026 identificadas abaixo. **Manter as próximas atualizações neste arquivo**, com data e prova; a equipe Android precisa de um único retorno para concluir o APK.

**Estado operacional:** fonte corrigido e release candidata preparados; a 5192 continua no binário antigo e o APK instalado continua na revisão anterior às correções desta rodada. A implantação depende do índice conciliado, ledger/backup/retorno e testes autenticados. Falta resolver os 404 de produção e validar download/instalação no aparelho. O fluxo HTTP candidato passou em banco temporário com itens sintéticos e, depois, com os 1.816 arquivos reais do volume acessível; isso ainda não comprova a 5192 de produção nem o download no aparelho.

## Revisão do retorno de produção e correções implementadas — 03/10/2026

Foram lidos o retorno `RETORNO-APP-FECHAMENTO-STATION-20261003.md`, o pedido servidor `e3050210` e o handoff integral/appendice do Android `db68b613cda008052afef8152400b9c595dfcffa`; os fontes auditados são do runtime `629a55a8cf48722460007944cf0bb737e9f8fb75`. A revisão foi cruzada com a DLL efetiva da 5192, o código candidato e o volume indicado. O APK instalado é **f5b35419fcff4188b8e86690045371892c77933e7f8edfc07bce1d5bd25d418a**, 1.902.718.870 bytes, pacote `org.turboramastation.frontend`, versionCode 11, versionName `1.0.8-turboeden-unico`, instalado 12:27:01. A evidência 12:29:44 confirma sessão/perfil/catálogo 200, **996 itens frescos da rede**, capa 404 `STATION_COVER_NOT_FOUND`, autorização 404 `STATION_ITEM_NOT_FOUND` e nenhuma transferência iniciada. O alias Mega Drive BR já foi incorporado. Esses fatos substituem as dúvidas anteriores sobre cache e integração do alias.

**Divergência localizada entre versões:** o fonte identificado pelo metadado `bbd07fd` da DLL antiga permite publicar entradas sem ROM/capa acessível e devolver 404 ao autorizá-las. O código candidato comparado pelo Android distingue ID ausente 404 de artefato indisponível 503; a DLL antiga não usa essa distinção. Não foi localizado ainda o primeiro **ID/caminho** divergente: o catálogo autenticado completo, o índice efetivo e os hashes de seleção/requisição ainda não foram obtidos juntos. A falta de travessia do UID 995 no HD é comprovada; relacioná-la a um item específico depende do índice real. Não atribuir os404 à extração, ao emulador, à corrida de sessão ou somente ao texto do cliente.

### Entregas de fonte e prova

- **Android:** correções em [645e7c44b74d10fb0494a650242de88e64cdcca7](https://github.com/luziellacerda/TurboElden/commit/645e7c44b74d10fb0494a650242de88e64cdcca7), com [revisão final 02c09dd36fcfa6c69ceb481f0934e84eef01e5ae](https://github.com/luziellacerda/TurboElden/commit/02c09dd36fcfa6c69ceb481f0934e84eef01e5ae) na mesma branch. A revisão final registra somente a correlação UUID gerada no cliente, evitando copiar texto de header recebido, branch `feat/station-review-fixes-20261003`, derivada de `db68b613`. [Resultado com hashes](https://github.com/luziellacerda/TurboElden/blob/02c09dd36fcfa6c69ceb481f0934e84eef01e5ae/versions/station-reconstruction-20261002/evidence/review-fixes-validation-20261003.json): 302 verificações em seis testes Java no host e 7 da política de repetição de capa em C++. Os dois cenários concorrentes passaram: capa não renova enquanto o GET aguarda cabeçalhos; após 200/cabeçalhos válidos, pode renovar durante leitura do corpo e a instalação termina com hash correto. **Não foi compilada a ponte Android/NDK nem gerado/instalado APK novo**; a base privada/assinatura/build E: e o aparelho não estão disponíveis neste ambiente Linux. O hash f5b35419 identifica o APK anterior às correções.
- **Servidor/ferramentas:** [commit e1ac9039c3825de3685529485e387a3270bc43b1](https://github.com/luziellacerda/Servidor-pix/commit/e1ac9039c3825de3685529485e387a3270bc43b1). Compilação Release sem erros/avisos; suíte .NET e HTTP isolado aprovados. 10 casos do conciliador passaram, cobrindo BR com ID preservado, hash/caminho, ambiguidade, outra família, revisão regressiva, capa ausente e metadados antigos. Exercício com o acervo real preservou todos os 1.816 IDs candidatos ao reclassificar 191 SNES BR e 94 Mega Drive BR; **não usou o índice de produção**. A candidata atual adiciona traces sanitizados à release anterior; dados criptográficos, sessão/grant, produto e rotas continuam verificados.

### Release atual e fechamento do teste administrativo

A release API do commit e1ac903 está em `/mnt/DADOS/station-api-release-candidate-20261003-e1ac903/`: DLL SHA256 `39cf1f9220d0a6753cae8c9e833900c74dab825c9dfd766970eeb14417ff58f7`, TAR `352d4b6cd8499b7b2ad0f6740cc68d9a73ed31f2f9f7bb38c7b0b783b577e622`. Seu metadado de origem foi conferido. A execução com **essa DLL empacotada** e o índice materializado produziu catálogo assinado de 1.818 itens, capa/download/hash corretos nas quatro categorias e `STATION CORRELATION: OK` / `STATION HTTP SMOKE: OK`.

Foi acrescentado o teste opcional `STATION_HTTP_ADMIN_DLL`, usando helper candidato em socket Unix temporário e o **mesmo PostgreSQL isolado**, com roles próprias API/admin e credenciais sintéticas. Resultado `STATION ADMIN HTTP: OK`: evento comercial pago e repetição idempotente; emissão de código; ativação/sessão reais no fixture; negação de token/claim/CSRF; bloqueio e invalidação de sessão/grant; conflito de geração antiga; desbloqueio; revogação de sessão; transferência e revogação do aparelho antigo; reemissão/ativação de outro aparelho; suspensão financeira impedindo desbloqueio. Nenhuma ação atingiu comprador, banco ou helper de produção. O teste recusa execução fora do cluster temporário e essa recusa também foi verificada.

Helper candidato `/mnt/DADOS/station-admin-release-candidate-20261003-e1ac903/TurboRamaSuiteAdminServer.dll`, SHA256 `82de901357deab184aee850fd0de6176bbd2e17f6cf46640f597f808dbc04e0e`; não implantado na 5194. A validação pelo painel real/provedor e a versão/configuração protegida da 5194 continuam pendentes. TTL real 60/180 s foi provado anteriormente na DLL 45fe4df; a candidata e1ac903 preserva essa lógica, mas não repetiu a espera real.

```text
STATION_HTTP_API_DLL=/mnt/DADOS/station-api-release-candidate-20261003-e1ac903/TurboRamaSuiteOnlineServer.dll \
STATION_HTTP_ADMIN_DLL=/mnt/DADOS/station-admin-release-candidate-20261003-e1ac903/TurboRamaSuiteAdminServer.dll \
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

### Resposta individual aos 14 achados do app

Referências `A/` = `versions/station-reconstruction-20261002/src/java/org/emulationstation/frontend/station/`; `N/` = `versions/station-reconstruction-20261002/src/native/`, no commit Android 02c09dd. Referências servidor = commit e1ac903.

| Achado | Comportamento cruzado e alteração | Prova / limite restante |
| --- | --- | --- |
| APP-01 | `N/station_frontend.cpp:72`/`station_cover_retry.hpp`: a falha de capa deixa de ser permanente. Reentra após 60 s, somente nas prioridades visíveis; falha JNI espera 5 s. Sucesso/republicação limpa o estado. `A/StationFrontend.java:82` devolve conclusão vazia também ao entrar em segundo plano, evitando `coverPending` preso. | 7 verificações da política C++ passaram. O ABI de `Item`/`Catalog` não foi alterado. A ponte transmite sucesso/caminho vazio, sem causa detalhada; testar prioridade, cancelamento, 404/429 e textura no Android. |
| APP-02 | `A/StationDownloads.java:33`, `StationCoordinator.java:47`, `StationCoverStore.java:46`, `StationApi.java:196`: mesma coordenação protege authorize→scanner→GET até cabeçalhos válidos. Capa usa a sessão fornecida; não a renova por conta própria. Corpo/instalação ficam fora do lock. | Dois testes com relógio controlado e barreiras HTTP passaram, inclusive renovação durante o corpo sem bloquear o download. Não foi causa demonstrada dos404 do APK; nenhuma transferência dele começou. |
| APP-03 | `A/StationPublication.java:15` e `StationFrontend.java:51`: catálogo assinado completo permanece na coordenação; itens com mapa verificado são publicados, desconhecidos são contados por plataforma e avisados na UI. Catálogo só de plataformas desconhecidas gera erro explícito. `StationPlatforms.java` adiciona `gamecube`, `psp`, `pspbr`, `ps2`, `ps2br`, `psvita` sobre pastas já documentadas. | Testes de catálogo misto, aviso/contagem e nenhuma pasta inferida passaram; 1.816 itens conhecidos continuam sem corte. `model2`/`sufami` precisam de mapa confirmado. Aviso Android ainda não exercitado; histograma HTTP bruto de produção pendente. |
| APP-04 | `StationCatalog`/`StationProtocol`, `StationLibrary`:4096 itens e12 MiB continuam sendo limites do v1, sem paginação. | 1.816 candidatos + 99 outras entradas documentadas cabem;12.346 nomes históricos não podem ser publicados pelo v1. Paginação exige contrato/cliente novos e acervo real das demais plataformas; não foi implementada nem usada truncagem. |
| APP-05 | `A/StationDiagnostics.java:24`, `StationHttp.java`, `StationCoordinator.java` e `StationDownloads.java`; servidor `StationRequestDiagnostics.cs`/`StationEndpoints.cs`: cada HTTP usa `X-Correlation-ID`; logs associam SHA256 de `itemId`/`coverId` e revisão. Autorização e consumo registram o item efetivamente recebido/resolvido no servidor. | HTTP isolado confirmou eco do header, tags exatas e ausência de credenciais/caminhos nos traces. Java testou escopo, descarte de header inválido e observador que falha. TSV cruzado inclui as tags. Captura conjunta app/5192 ainda exige as duas revisões instaladas. |
| APP-06 | `A/StationConfig.java:12`: `clientVersion` agora `1.0.8-station-review-20261003.1`, dentro do limite 64 do servidor e dos bytes assinados. Pin/autoridade atuais continuam conferidos. | O APK f5 ainda envia 1. A identificação nova vale após build; não é o hash do APK. Rotação coordenada de certificado/autoridade permanece pendente de segunda chave/pin aprovados, sem aceitar chave enviada pelo servidor ou desabilitar TLS. |
| APP-07 | `conciliar-indices-station.py`: preserva ID na mesma família de console, revalida descritores/capas e exige revisão superior a todas as revisões globais/individuais; registra migrações BR. Cache continua por `coverId`+revisão. | 10 casos de falha/preservação e exercício completo 1.816 passaram. Produção precisa do índice real e de incremento quando bytes mudarem; trocar bytes mantendo ID/revisão viola o cache. |
| APP-08 | `StationInstaller` verifica/pública `launchPath` transacional; o launcher/emuladores binários preservados não foram executados sobre instalação Station nesta rodada. | Testes raw/ZIP e recibos passaram. Faltam download→abrir jogo→voltar em aparelho e pastas reais; não há prova nova de emulador. |
| APP-09 | `StationExistingArtifact`/`StationInstaller`: reuso exige autorização, tamanho/hash assinados e conferência transacional; originais permanecem. Gerações substituídas continuam preservadas. | Testes de reuso e original preservado passaram. Limpeza precisa política de retenção e identificação de jogo em execução; não foi adicionada exclusão de pastas/saves para reduzir espaço por suposição. |
| APP-10 | Ponte comercial nova convive com `libmain.so`/renderer/motores preservados. Nenhuma alteração física no APK instalado foi feita aqui. | Remoção integral do legado e medição de chamadas antigas dependem da base nativa/aparelho. Não se declarou reconstrução integral nem ausência de tráfego residual. |
| APP-11 | `A/StationDownloads.java`, `message`:404 de item informa jogo indisponível no catálogo atual e orienta atualizar, sem atribuir arquivo ausente ou culpa da equipe. | Teste rejeita texto que afirme causa de filesystem. A explicação interna deve vir da correlação, índice e DLL, não da mensagem exibida. |
| APP-12 | `StationDownloads.message`: cancelamento explícito tem prioridade; `SocketTimeoutException` informa demora de conexão e outra `InterruptedIOException` informa interrupção. | Três casos passaram; timeout não aparece como cancelamento do usuário. Repetição precisa grant novo, nunca mesmo GET. |
| APP-13 | `StationCoordinator.java:40`/`loadName`: atualizar consulta `/me`, salva nome privado/header e usa nome em cache só no 503 tipado após sessão válida. “Manter conectado” continua preferência de entrada automática; não solicita revogação. | Testes de nome atualizado, header e 503 passaram. Alternância individual do checkbox e mudança de nome no aparelho ainda pendentes; bloqueio/revogação remotos são outro fluxo. |
| APP-14 | `StationCoordinator.java:75`, `ready`: `live==authorized` é obrigatório, além do catálogo e prazo. Renovação em capa revalida catálogo antes de publicar autorização. | Teste de sessão renovada independentemente recusa ready, e cover/refresh restauram coerência. Integrado aos cenários APP-02; APK instalado ainda usa regra anterior. |

### As 9 rotas e a leitura correta do servidor

Origem do cliente: `https://app.lzgames.com.br`; caminhos literais abaixo, sem prefixo de outro produto. `StationHttp.allowed` aceita apenas esses métodos/caminhos; `StationApi` verifica assinatura RSA-PSS, keyId, domínio, produto/aplicação, aparelho, licença e sessão conforme a operação. No catálogo/grant, usar IDs exatos, nunca título/pasta como ID.

| Método/caminho | Leitor do app / dados obrigatórios | Produção observada e prova isolada |
| --- | --- | --- |
| POST `/v1/station/activations/challenge` | `StationApi.activate`: código de ativação Base64URL32 bytes, SPKI, identidade; desafio assinado 60 s. | Retomada f5 não repetiu ativação. Fluxo/desafio/expiração passou isolado. |
| POST `/v1/station/activations/complete` | Prova RSA-PSS do Keystore sobre desafio/nonce; guardar `licenseId` verificado. `STA-` identifica licença. | Completar ativação passou isolado; preservar licença e Keystore do aparelho. |
| POST `/v1/station/challenges` | `openSession`: licença/aparelho; conferir nonce/challenge assinado 60 s. | Cliente retoma acesso salvo; fluxo isolado passou. |
| POST `/v1/station/sessions` | Prova do mesmo aparelho; sessão/Bearer 180 s; renovar mantendo coerência com catálogo/grants. | 200 no aparelho; TTL real passou na release anterior e regra foi preservada. |
| GET `/v1/station/me` | `profile` com Bearer válido; ler `displayName` do payload verificado, nunca de input local de autorização. | 200 no aparelho; atualização/cache503 testados no cliente. |
| GET `/v1/station/catalog` | `catalog`: ler `revision` e cada `itemId`, `name`, `platform`, `revision`, `coverId`; separar rede de cache assinado. | 200/996 **da rede** confirmado pelo Android. Distribuição bruta/IDs completos da resposta ainda não capturados. |
| GET `/v1/station/covers/{coverId}` | `cover`/cache: ID do mesmo item assinado, mesma revisão, MIME/assinatura de imagem, até 5 MiB; placeholder e repetição limitada após falha. | 404 tipado no aparelho.200/404 e bytes/hash passaram isolado, com amostra real por plataforma. |
| POST `/v1/station/downloads/authorize` | `authorize`: ID exato e identidade; conferir item/revisão e todos os campos de `artifact` no grant assinado 60 s. | 404 `STATION_ITEM_NOT_FOUND` no APK, antes de transferência. Raw/ZIP e descritor passaram isolado. |
| GET `/v1/station/artifacts/{grantId}` | `openArtifact`→`copyToStaging`: mesmo Bearer do grant, uso único, sem Range/redirect; `Content-Length` e SHA256 assinados; só depois instalar/recibo/launchPath. | Nenhum GET do fluxo f5 iniciado. 200, reuso 404, interrupção, sessão/aparelho distintos, hash e grant novo passaram isolado. |

### Como cruzar a próxima tentativa com o índice real

1. Compilar a fonte Android 02c09dd e usar API e1ac903 com conteúdo conciliado para emitir traces. A correlação não concede acesso nem muda assinatura; nenhum `grantId`, Bearer, licença, nome pessoal ou caminho é registrado por esse código.
2. Relacionar `correlation` do app e servidor. `itemTag = SHA256(UTF-8(itemId))`, `coverTag = SHA256(UTF-8(coverId))`, hex minúsculo 64 caracteres. O trace do servidor usa somente o ID recebido/resolvido; o app usa seleção do item do catálogo. Comparar com `diagnosticItemTag`/`diagnosticCoverTag` no TSV gerado a partir do **índice efetivo**. As tags candidatas atuais não substituem os IDs antigos da 5192.
3. Comparar, na mesma sessão, resposta autenticada fresca, item selecionado, pedido emitido, registro carregado e leitura de ROM/capa pelo UID 995. Se a tag do pedido divergir da seleção, corrigir cliente; se não existir no índice usado para aquela resposta, corrigir publicação/versão; se coincidir e o caminho não for legível, corrigir conteúdo/permissão. Mudança entre captura e reinício precisa ser documentada.
4. A cópia `/home/lz-servidor/indice-station-5192-privado.json` ainda não está disponível. O mantenedor autorizou digitar a senha no terminal; foi fornecido o comando de exportação restrita abaixo. A sessão de ferramenta não tem `sudo -n` válido e não recebeu senha. Quando a cópia surgir, executar conciliação preservando as outras plataformas e validar cada arquivo antes da troca.

## Diagnóstico dos 404 da Station em 03/10/2026

O TurboStation usa componentes do backend Turborama Suite para banco, autenticação e licença. `Suite__Enabled=true` é uma dependência de registro dos serviços Station em `Program.cs`; `Station__Enabled=true`, produto `TURBORAMA_STATION_ANDROID` e rotas `/v1/station/*` mantêm a identidade própria do aplicativo. A conta Linux da API Station se chama `turborama-suite`. O trabalho desta rodada abrange a 5192, seu conteúdo e a compatibilidade do APK.

Às 12h24, o journal da **5192** desde 08h30 mostrou `/catalog` 200 em 8 chamadas, `/me` 200 em 6, `/sessions` 200 em 8, `/covers/{id}` 404 em 83 e `POST /downloads/authorize` 404 em 47. Todas as 47 autorizações registraram execução do endpoint; todos os 83 IDs de capa atendem ao formato aceito, distribuídos em 51 IDs distintos. Não houve 200 de capa, autorização ou transferência observado nesse período. O journal desse recorte não registra os corpos. O retorno Android posterior confirmou 404 `STATION_COVER_NOT_FOUND` e 404 `STATION_ITEM_NOT_FOUND`; o índice efetivo e a correlação individual continuam ausentes, portanto a causa de cada registro não está comprovada. Uma sonda pública com `curl` recebeu 401 JSON de `/catalog`, confirmando chegada ao serviço e exigência de Bearer. Uma sonda com User-Agent padrão do Python recebeu 403/1010 do Cloudflare; essa sonda não é prova de comportamento do APK.

A DLL efetiva contém o metadado `1.0.0+bbd07fda79e1c938f691ab874aa7c1fc84cc65e2`. No fonte identificado por esse metadado, o catálogo é montado a partir dos registros JSON sem conferir a existência das ROMs/capas. `ReadCover` devolve ausência para `coverId` desconhecido, arquivo para o qual `File.Exists` é falso ou tamanho fora de 1 byte a 5 MiB; `TryResolve` também exige `File.Exists` para autorizar jogo. `AuthorizeDownloadAsync` também devolve `STATION_ITEM_NOT_FOUND` quando esse `TryResolve` falha, incluindo ROM indisponível; na candidata nova, ID conhecido sem artefato pronto recebe 503 `STATION_ARTIFACT_NOT_READY`. Isso permite catálogo 200 acompanhado de capas/autorizações 404 no código antigo. A identificação pelo metadado não substitui a correlação do item nem prova ausência de alterações locais na compilação original. A verificação de ACL confirmou que `/media/lz-servidor` dá acesso a `root` e `lz-servidor`, mas **nenhum acesso** ao UID 995 (`turborama-suite`), cujos grupos tampouco permitem atravessar esse diretório. Se o índice efetivo aponta para o HD indicado, essa permissão bloqueia ROM e capa. Também é preciso comparar os IDs pedidos com um catálogo fresco e os caminhos configurados; o índice protegido impede fechar essa comparação agora.

A cópia candidata materializada recebeu ACL específica de leitura para UID 995: somente travessia no diretório pai e leitura/execução nos 1.819 diretórios, leitura nos 3.635 arquivos de conteúdo/índice/relatórios, sem escrita para a conta de serviço. As ACLs anteriores foram salvas privadamente para retorno. A unidade Station não tem `RootDirectory`, `RootImage`, `InaccessiblePaths` ou `PrivateMounts` que bloqueiem essa árvore; uma operação real sob UID 995 ainda precisa ser confirmada na implantação. Os 3.632 checksums de jogos/capas passaram novamente e os hashes de índice/manifesto permaneceram iguais. A ACL foi aplicada somente ao estágio preparado e ao acesso ao seu diretório pai; a 5192 continua com DLL/índice antigos.

Para fechar: obter a cópia do índice efetivo; conciliar IDs publicados com os 1.816 itens preparados; conferir leitura de todos os arquivos no destino final; verificar migration 029/chave de grants e aplicar a release Station já identificada com backup/retorno. Em seguida, exigir catálogo/capa/download 200 autenticados por HTTPS e instalação no APK. O APK precisa usar `coverId` e `itemId` da resposta assinada atual e o mesmo Bearer de sessão para autorização/transferência; um grant expirado ou consumido exige autorização nova.

## Homologação complementar da release Station em 03/10/2026

`tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py` agora aceita `STATION_HTTP_REAL_TTL=1`. A execução usou a DLL publicada `45fe4df`, o índice materializado dos 1.816 jogos e PostgreSQL temporário; terminou com `STATION TTL: OK` e `STATION HTTP SMOKE: OK`, amostrando uma capa e um download completo por cada uma das quatro plataformas. Os desafios de ativação e sessão expiraram por relógio real com 409 `STATION_CHALLENGE_INVALID`; o grant expirado devolveu 404 e a sessão ainda válida permitiu uma autorização nova com download/hash corretos. Após 180 s, perfil e autorização devolveram 401 `STATION_SESSION_INVALID`; uma sessão nova restaurou perfil e download completo. A interrupção de transferência também foi seguida por grant novo, bytes completos e hash correto. A fase de relógio não alterou datas de expiração no banco. Isso comprova a candidata em isolamento; a 5192 pública e o APK ainda precisam dessas mesmas provas após a conciliação/implantação.

Comando para repetir, apontando apenas para arquivos candidatos e banco temporário; sem credenciais de produção:

```text
STATION_HTTP_REAL_TTL=1 \
STATION_HTTP_API_DLL=DLL_CANDIDATA \
STATION_HTTP_EXTRA_INDEX=INDICE_CANDIDATO_PRIVADO \
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

## Retorno ao pedido de fechamento do Android, commit `d953998`, em 03/10/2026

Li integralmente `docs/server/HANDOFF-PEDIDO-FECHAMENTO-SERVIDOR-STATION-20261003.md` do TurboElden, commit `d953998`. Este retorno continua no documento único do servidor. **Entrega atual: candidata validada em isolamento, ainda não implantada na 5192.** O pedido exige índice conciliado, permissões da conta de serviço, migrations/backup, APK compatível e prova HTTPS real antes de declarar produção pronta.

| Frente pedida | Implementado/conferido | Falta para produção |
| --- | --- | --- |
| Identificar 5192 | `turborama-station-api.service` ativo, PID 2388, `ExecStart` da release antiga, sem drop-ins; SHA256 da DLL efetiva `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`; `/ready/station` local 200. | Configuração protegida continua ilegível; revisão/IDs do índice e migration 029 no ledger real não foram lidos. O 200 da DLL antiga só confirma migration 028/banco, não capas ou downloads. |
| Conteúdo | 1.816 jogos/capas/descritores SNES/Mega Drive já validados. `scripts/materializar-conteudo-station.py` copiou somente os arquivos escolhidos para árvore privada de release: 1.887.922.991 bytes de jogos, 1.039.395.104 bytes de capas, 3.632 hashes em `files.sha256`, manifesto SHA256 `6867471b46d8dbe1a774214b3ffb2cd6fae116651688c6ff0c3433c4af7722dd`. Índice reescrito SHA256 `cc2802f6f33c04deafc40b040c2ff6d04ed621f8c84e1072ec5c8ac26a9cbd52`; carregador e HTTP isolado passaram novamente. | Conciliar com índice efetivo para preservar IDs e outras plataformas; instalar árvore em local estável legível por `turborama-suite`. O diretório pai do HD original continua bloqueado para a conta da 5192. A cópia materializada agora tem ACL de leitura para essa conta, conferida em toda a árvore; confirmar abertura real no destino final antes da implantação. |
| Código/release | Candidata atual: commit `e1ac9039c3825de3685529485e387a3270bc43b1`; DLL Release SHA256 `39cf1f9220d0a6753cae8c9e833900c74dab825c9dfd766970eeb14417ff58f7`, pacote TAR SHA256 `352d4b6cd8499b7b2ad0f6740cc68d9a73ed31f2f9f7bb38c7b0b783b577e622`. Inclui os ajustes da release anterior `45fe4df` e diagnóstico por correlação/hash. O código novo exige migrations 028 **e 029** em `/ready/station`; o teste temporário confirmou 503 sem a 029 e 200 com ela. Suíte .NET e HTTP com a DLL publicada passaram em banco temporário. | Verificar ledger real, backups restauráveis e caminho/permissões finais antes da troca da unidade. Não executar o binário novo contra índice antigo sem `artifact`. |
| Acesso/perfil/admin | HTTP isolado confirmou ativação, sessão, `/me` assinado com nome e grant; testes .NET cobrem provas, concorrência, revogação e transferência. `StationCommerceEndpoints` e `StationAdminEndpoints` do código candidato oferecem emissão, bloqueio, desbloqueio, transferência e revogação com escopo Station; projeto Admin compila em Release. | Conferir configuração/versão efetiva do helper 5194, fluxo comercial autorizado e respostas de produção com conta sintética. TTL real de 60/180 s passou na API isolada com a DLL publicada; fluxo administrativo/comercial também passou por HTTP em isolamento; repetir pelo painel/provedor e produção continua pendente. |
| APK | Retorno `629a55a8` confirma APK instalado `f5b35419...`, com alias `megadrivebr`, 996 itens frescos da rede e 404 tipados. Correções adicionais publicadas no fonte 645e7c4/02c09dd, ainda sem APK novo. | Android: compilar/assinar a revisão final 02c09dd no ambiente E:, instalar por atualização e provar capa, download, instalação, abertura, retorno e reuso. O APK instalado não foi alterado nesta tarefa. |

**Inventário do HD indicado:** a raiz contém apenas `snes` e `megadrive` como plataformas; a busca na árvore acessível encontrou zero arquivos `.json` (`lost+found` está protegido pelo sistema). As 36 rotas e 12.346 nomes da tabela histórica não comprovam ROM/capa acessível das demais plataformas. A 5192 aponta para um arquivo privado configurado em `/etc/turborama-suite/station-5192.env`, inacessível à conta `lz-servidor`; `sudo -n` informa que precisa de senha. Para obter uma cópia sem expor o `.env`, um operador com acesso root pode executar a ferramenta de leitura abaixo. Ela extrai **somente** `Station__LibraryIndexFile`, grava a cópia com modo 0600 para `lz-servidor` e imprime apenas revisão, contagens e hash; não modifica a 5192:

**Lista cruzada para comparação com o aplicativo:** [catalogo-candidato-cruzado-20261003.tsv](catalogo-candidato-cruzado-20261003.tsv), SHA256 `6ca242d8c192b3021a30bdd2af60bef70b40f086fd4693ff7dbcb9414f903589`. São 1.816 linhas ordenadas por `platform`, `name` e `itemId`, sem caminhos privados. As colunas `diagnosticItemTag` e `diagnosticCoverTag` são SHA256 UTF-8 dos respectivos IDs para cruzar os novos logs. Cada linha liga `itemId` e `coverId` candidatos a nome, plataforma, revisão, hash/MIME/tamanho da capa, `artifact` completo para download e entrada XML original (`sourcePlatform`, `sourceXmlEntry`, coleção e hash da ROM). A conferência encontrou 1.816/1.816 nomes, ROMs e capas correspondentes à **mesma** entrada XML; todos os arquivos preparados são legíveis e todas as capas válidas. Os três ZIPs filtrados têm hash do artefato servido diferente do ZIP original; `sourceGameSha256` mantém o elo com a ROM de origem. Há 1.018 downloads ZIP e 798 `raw`. Os IDs/revisão desse arquivo são **provisórios do candidato**, não os IDs assinados ou publicados pela 5192; `catalogMatch=not_supplied` em todas as linhas até obter o catálogo autenticado. Após a conciliação com o índice efetivo, gerar nova lista com `--catalog-tsv` da resposta assinada e exigir `catalogMatch=exact` antes de comparar o APK.

```text
sudo python3 docs/station-android/scripts/exportar-indice-efetivo.py \
  --output /home/lz-servidor/indice-station-5192-privado.json
```

Após essa cópia, conciliar com `scripts/conciliar-indices-station.py` usando o índice candidato materializado e `source-map.json`, escolher revisão superior a **todas** as revisões de catálogo e item das duas entradas e validar o índice resultante pelo carregador real. A cópia e o índice mesclado ficam fora do Git. O operador precisa confirmar leitura de **cada** jogo/capa pela identidade `turborama-suite` no destino final, não só por `lz-servidor`; depois conferir a migration 029 no banco real, backup/retorno e compatibilidade do APK. O novo código só deve entrar na 5192 com esses dados concretos.

| Plataforma do candidato | Itens/capas validados no estágio | Download HTTP isolado | Itens vistos no APK antigo | Exclusões conhecidas no HD |
| --- | ---: | --- | ---: | --- |
| `snes` | 644/644 | 1 jogo completo, SHA256 conferido | 176 | 0 ROM ausente |
| `snesbr` | 191/191 | 1 jogo completo, SHA256 conferido | 28 | 0 ROM ausente |
| `megadrive` | 887/887 | 1 jogo completo, SHA256 conferido | 693 | 0 ROM ausente |
| `megadrivebr` | 94/94 | 1 jogo completo, SHA256 conferido | 0 na exportação documentada; alias já integrado no f5b35419 | 8 entradas XML sem ROM excluídas |
| Outras plataformas | Sem acervo de arquivos/capas neste HD | Não testadas nesta rodada | 99 no APK antigo, distribuídos em cinco plataformas | Quantidade real indisponível sem índice/fontes |

O retorno Android `629a55a8` confirma total de 996 vindo da rede no aparelho. A distribuição da tabela foi contada após o mapeamento nativo; ainda falta histograma bruto autenticado por `platform`. Nenhuma capa 200 ou transferência de produção foi comprovada nesta rodada. O teste HTTP isolado usa licença/chaves sintéticas e amostra uma transferência por plataforma, não todas. Não declarar publicação ou sucesso no aparelho a partir dele.

## Atualização: homologação HTTP isolada em 03/10/2026

O commit `de08858` acrescentou `tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py` e corrigiu a ordem de consumo do grant em `StationService.ConsumeArtifactAsync`. O teste abre PostgreSQL 16 temporário por `pg_virtualenv`, aplica as 29 migrations da branch, inicia a API candidata somente em loopback e usa licença e chaves sintéticas. Por padrão usa índice, capa e arquivos sintéticos; com `STATION_HTTP_EXTRA_INDEX` também inclui um índice privado de arquivos reais na API isolada. Ele recusa execução fora do cluster temporário. Comando executado na raiz do worktree:

```text
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

Resultado: `STATION HTTP SMOKE: OK`. Foram verificados por HTTP ativação e sessão com prova RSA-PSS, assinatura e `keyId` dos envelopes de perfil/catálogo/grant, catálogo de dois itens, capa PNG 200 e ID inexistente 404, descritor `raw` e ZIP de dois membros com `launchPath` explícito, `Content-Length`, bytes e SHA256, segundo GET 404, transferência interrompida seguida de 404, grant expirado 404, aparelho não vinculado 403 e sessão revogada 404. Uma sessão nova recebe 404 ao tentar grant emitido para a sessão anterior; o teste confirma no banco temporário que essa tentativa não marca o grant como consumido. A sessão nova revoga a antiga por regra do servidor, portanto o grant antigo permanece inutilizável. A execução da suíte .NET local também passou. Nenhum serviço, banco ou arquivo de produção foi alterado por esses testes.

Falha descoberta antes da correção: a implementação consumia o grant antes de conferir o vínculo criptográfico da sessão. A correção confere o vínculo e o descritor primeiro; a operação atômica de consumo continua imediatamente antes de servir o arquivo. O diff foi testado com as releases empacotadas 45fe4df e e1ac903 em isolamento. A execução inicial provocou a expiração do grant no banco temporário. A homologação posterior com `STATION_HTTP_REAL_TTL=1`, descrita acima, confirmou 60/180 segundos por relógio real e nova autorização após interrupção; restauração após queda de processo continua sem prova. Testes de produção e aparelho continuam pendentes.

## Atualização: nomes, jogos e capas por plataforma em 03/10/2026

O Android publicou [nova evidência](https://github.com/luziellacerda/TurboElden/blob/7d5df08d922ef7c517979cf2263ff4bfe4be74ff/docs/server/HANDOFF-SERVIDOR-CATALOGO-INCOMPLETO-STATION-20261003.md) no commit `7d5df08d922ef7c517979cf2263ff4bfe4be74ff`: o APK de teste instalado abriu 996 itens. A contagem entregue ao renderer foi `megadrive=693`, `snes=176`, `snesbr=28`, `gamegear=58`, `gb=22`, `sega32x=7`, `gbc=7`, `gba=5`. Essas contagens repetem o catálogo de revisão 1 documentado em `ac869429eba3fd3dcc41f3bd9a55a08cd4985659`; naquela evidência anterior a origem rede/cache não estava fechada; o retorno posterior `629a55a8` confirmou **996 frescos da rede**. Continua faltando o histograma bruto do payload HTTP. Não interpretar 176 como filtro visual do carrossel nem declarar que a 5192 publicou hoje os demais jogos.

Por pedido do mantenedor, há **duas listas de nomes completas para as fontes acessíveis**, ordenadas por plataforma e nome, como arquivos de dados ligados a este mesmo handoff:

| Lista | Conteúdo e origem | O que cada linha permite conferir |
| --- | --- | --- |
| [Jogos e capas do volume SNES/Mega Drive](catalogo-disco-snes-megadrive-20261003.tsv) | 1.824 entradas dos dois `gamelist.xml` principais no volume indicado pelo mantenedor; SHA256 do TSV `f116caecc7656dd6fef0f3f2f988793b1f2d4964bd507fc1007f4308b48e3c19`. | `platform`, posição no XML, coleção geral/PT-BR, nome, presença/tamanho/SHA256 da ROM, capa `<image>` com MIME/tamanho/dimensões/SHA256 e quantidade/hashes distintos dos candidatos de revista cujo nome base coincide **exatamente** com o da ROM. Sem caminhos nem URLs. |
| [Lista histórica de referência por plataforma](catalogo-referencia-xml-por-plataforma-20261003.tsv) | 12.346 linhas, 36 rotas, extraídas somente das colunas `rota` e `nome` de `cruzamento-nomes-xml.tsv` no commit `1ac9dd8e9cf9913e2ea753ea3d5cb02736faef9a`; SHA256 do TSV `44d32027dcfe9a4021cf9fdbc1190aa772a53c1801aeac315f70d009de5aa63a`. | Todos os nomes da tabela histórica, com `sourceRow` para distinguir nomes repetidos. **Não comprova** arquivo, capa, `itemId` ou inclusão na API atual. |

Totais da **referência histórica**, não da API nem do volume montado:

| Rota | Linhas | Rota | Linhas |
| --- | ---: | --- | ---: |
| `3ds` | 260 | `megadrivebr` | 83 |
| `arcade` | 721 | `model2` | 54 |
| `atomiswave` | 27 | `n64` | 213 |
| `colecovision` | 30 | `n64br` | 22 |
| `cps1` | 22 | `nds` | 1693 |
| `cps2` | 22 | `neogeo` | 140 |
| `cps3` | 6 | `neogeocd` | 22 |
| `dreamcast` | 686 | `nes` | 796 |
| `fds` | 238 | `o2em` | 133 |
| `gameandwatch` | 56 | `pcengine` | 62 |
| `gamegear` | 313 | `pcenginecd` | 2 |
| `gb` | 325 | `psx` | 448 |
| `gba` | 1169 | `sega32x` | 36 |
| `gbc` | 654 | `snes` | 785 |
| `jaguar` | 58 | `snesbr` | 231 |
| `mame` | 1730 | `sufami` | 13 |
| `mastersystem` | 360 | `supergrafx` | 5 |
| `megadrive` | 870 | `switch` | 61 |

O cruzamento jogo/capa do volume foi reproduzido com `scripts/gerar-catalogo-midia.py` (Python/Pillow). Ele segue somente o `<path>` e `<image>` da mesma entrada XML e, para capas de `media/revista`, aceita somente igualdade exata do nome base; não aproxima títulos. Verificou decodificação da imagem, MIME reconhecido, dimensões positivas e 1 byte a 5 MiB. SHA256 identifica cada ROM e cada capa candidata sem expor o caminho. Se houver mais de uma capa com o mesmo nome base, a linha mantém todos os hashes, `revistaCandidateCount` e `revistaDistinctHashCount`; **não escolhe** uma delas silenciosamente.

| Fonte do volume | Entradas XML | ROM presente | Capa `<image>` válida | Candidatos de revista por nome exato |
| --- | ---: | ---: | ---: | --- |
| SNES | 835 | 835 | 834; 1 entrada não declara imagem | 614 com 1 candidato; 197 com 2; 24 com 3. Todos os candidatos encontrados passaram nas verificações de imagem. |
| Mega Drive | 989 | 981; 8 entradas apontam para arquivo ausente | 953; 28 não declaram imagem; 8 apontam para imagem ausente | 979 com 1 candidato; 2 com 2; 8 sem candidato. Todos os candidatos encontrados passaram nas verificações de imagem. |

As 835 ROMs SNES existem e são referenciadas uma vez cada pelo XML principal. A coleção PT-BR é um **subconjunto** de 191 dessas 835, não mais 191 jogos a somar. Mega Drive tem 981 ROMs existentes e todas aparecem no XML; 94 estão na subcoleção PT-BR. Os 8 registros sem ROM não devem virar item baixável. O XML Mega Drive contém 87 nomes repetidos; `xmlEntry` e SHA256 diferenciam registros, mas nenhum deles é `itemId` Station. O volume montado contém apenas SNES e Mega Drive; as outras plataformas da tabela histórica não tiveram arquivo/capa verificado nesta rodada.

Uma imagem `<image>` ausente no XML não significa ausência de toda capa: cada ROM existente deste volume tem ao menos um candidato válido de revista por nome exato. O índice candidato descrito abaixo já escolhe uma capa explícita para cada ROM; o índice efetivo da 5192 ainda não foi conferido.

Das 223 linhas com múltiplos arquivos de revista, 219 têm **um único hash de imagem** entre os candidatos; só 4 linhas SNES apresentam dois conteúdos diferentes. O índice candidato escolhe `<image>` válido da mesma entrada XML primeiro. Nas 33 ROMs sem essa imagem válida, usa a capa de revista por nome base exato somente quando todos os candidatos têm o mesmo SHA256; portanto nenhum dos quatro conteúdos distintos é escolhido por aproximação.

| Elo jogo → capa | Chave que deve unir os dados | Estado nesta atualização |
| --- | --- | --- |
| XML do volume → ROM e imagem | Mesma `xmlEntry`; `<path>` e `<image>` explícitos. | Conferido para 1.824 linhas, com presença, MIME, dimensões, tamanho e hashes no TSV do volume. |
| ROM → capa de revista | Nome base **idêntico** de ROM e imagem, sem aproximação. | 1.593 linhas têm candidato único; 223 têm 2 ou 3 arquivos candidatos, dos quais só 4 diferem em bytes; 8 registros sem ROM/candidato. O TSV guarda todos os hashes candidatos; o gerador de índice candidato faz a escolha explícita e verificável descrita abaixo. |
| Índice Station → ROM e capa servidas | `itemId` → `filePath` e `coverId` → `coverPath`, com revisão. | Pendente: índice protegido não está legível para esta conta. Hash da capa redimensionada pode diferir do arquivo original do volume; exigir mapeamento explícito de proveniência. |
| Resposta assinada → capa HTTP → APK | `itemId`, `coverId`, revisão do item e SHA256 dos bytes realmente entregues por `GET covers/{coverId}`. | Pendente em produção: falta export autenticado dos itens, 200 de capa válida e comparação do hash recebido com o arquivo selecionado no índice. |

Comparação de escala, **sem presumir pares um a um**: o APK exibiu 176 `snes` + 28 `snesbr` = 204 itens, enquanto o volume tem 835 ROMs SNES; exibiu 693 `megadrive`, enquanto o volume tem 981 ROMs Mega Drive. A lista histórica tem 785 `snes` + 231 `snesbr` e 870 `megadrive` + 83 `megadrivebr`, mas vem de outra fonte e não deve ser somada à lista do volume. Nenhuma dessas diferenças, sozinha, identifica quais `itemId` faltam ou qual `coverId` cada jogo deve receber.

### Índice candidato completo para o volume acessível

[scripts/preparar-catalogo-volume.py](scripts/preparar-catalogo-volume.py) gerou **em área privada fora do Git** um índice Station autônomo de revisão 2, com 1.816 itens, `artifact` de todos os jogos e `coverPath` válido para cada `coverId`. Resultado: `snes=644`, `snesbr=191`, `megadrive=887`, `megadrivebr=94`; 8 entradas XML Mega Drive sem ROM foram excluídas. Foram escolhidas 1.783 capas pelo `<image>` da própria entrada XML e 33 por revista de nome base idêntico. Os arquivos e capas foram conferidos contra os SHA256 do inventário do disco. O índice gerado tem SHA256 `0b2be0d98939783ddde0d2887ff82f65536b7c15446acc03783901bb69421b56`; a pasta tem modo 0700 e `index.json`/`source-map.json` têm modo 0600. O mapa privado conserva caminho e hash da ROM original para conciliar IDs antigos, inclusive quando o artefato servido for uma cópia preparada.

Três ZIPs de origem continham membros estranhos ao jogo: **WWF WrestleMania - The Arcade Game**, **Alien 3** e **Streets of Rage**. O gerador criou, em área privada, uma cópia ZIP com somente a ROM jogável de cada um; a origem permaneceu intacta. Para os outros 1.813 itens, o descritor aponta para a ROM original. A ferramenta `preparar-indice-artefatos.py` calculou descritores `raw`/`zip`; o carregador real `StationLibrary.TryLoad` aceitou o índice inteiro e `TryResolveArtifact`/`ReadCover` passaram para os **1.816 jogos e 1.816 capas**, com as quatro contagens por plataforma acima. Além disso, `STATION_HTTP_EXTRA_INDEX=... pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py` passou em PostgreSQL 16 temporário e API local isolada: catálogo assinado de **1.818 itens** (1.816 deste volume + 2 sintéticos), com uma capa HTTP 200 e um download completo, autorizado e conferido por SHA256 para cada uma das quatro categorias. Essa prova não consultou a 5192 de produção nem instalou jogo no APK.

Comando reproduzível, escolhendo uma pasta **nova e privada fora do repositório**; substitua apenas os dois caminhos locais de entrada/saída na máquina de operação:

```text
python3 docs/station-android/scripts/preparar-catalogo-volume.py \
  --volume-root VOLUME_PRIVADO \
  --disk-tsv docs/station-android/catalogo-disco-snes-megadrive-20261003.tsv \
  --output-dir DIRETORIO_PRIVADO_NOVO --revision 2
```

**Não trocar o índice atual por esse arquivo isolado.** O retorno do APK da 5192 registra itens de outras plataformas e IDs já entregues ao APK; substituir apagaria esses itens e poderia romper o vínculo de instalações/cache. A cópia privada do índice atual deve ser conciliada por caminho e SHA256 da ROM com `source-map.json`, preservando `itemId` já publicado, mantendo outras plataformas, escolhendo revisão superior à efetiva e regenerando descritores das entradas herdadas. A publicação só é válida se todos os itens resultantes tiverem ROM, capa e `artifact` legíveis, se o total couber no limite de 4096 e se o APK reconhecer **todos** os identificadores de `platform`. A revisão `2` acima é do candidato isolado, não da produção.

A ferramenta [scripts/conciliar-indices-station.py](scripts/conciliar-indices-station.py) está pronta para a cópia privada do índice atual. Ela preserva o `itemId` publicado quando encontra a mesma ROM por caminho original exato ou SHA256 na mesma família de console (`snes`/`snesbr` ou `megadrive`/`megadrivebr`), mantém os itens das outras plataformas, acrescenta os jogos novos e usa revisão superior a todas as revisões globais e individuais. Valida novamente cada artefato e capa candidato, reconhece hashes de arquivo original ou preparado e rejeita metadados herdados desatualizados. O relatório contabiliza mudanças de coleção com ID preservado. Se um jogo publicado não tiver correspondência única, a capa herdada estiver ilegível, faltar artefato nas outras plataformas, houver colisão de IDs ou o total ultrapassar 4096, falha sem criar o índice mesclado. `--launch-manifest` fornece a escolha privada de `launchPath` para pacotes herdados com vários arquivos. A saída tem modo 0600. Foi exercitada com base sintética contendo um ID antigo de SNES e um item Game Gear herdado: mesclou 1.817 jogos, preservou o ID antigo e `StationLibrary.TryLoad`/`TryResolveArtifact`/`ReadCover` passaram para todos os 1.817. **Ainda não foi executada sobre o índice efetivo.**

```text
python3 docs/station-android/scripts/conciliar-indices-station.py \
  --base-index INDICE_EFETIVO_PRIVADO.json \
  --candidate-index CANDIDATO_PRIVADO/index.json \
  --source-map CANDIDATO_PRIVADO/source-map.json \
  --revision REVISAO_SUPERIOR --output INDICE_MESCLADO_PRIVADO.json
```

O retorno Android `629a55a8` confirma que o APK instalado `f5b35419` já incorporou o alias `megadrivebr` publicado em `5988343`; esse bloqueio de fonte foi resolvido. O teste sintético assinado de 1.816 itens não comprova catálogo publicado nem os 94 jogos BR no aparelho. A revisão corretiva `645e7c4` acrescenta seis aliases de pastas já verificadas e isolamento com aviso de plataformas desconhecidas; ainda precisa de build e teste Android. O app deve medir `items` recebidos, publicados, capas 200/404 e instalações por plataforma antes de declarar cobertura completa.

### Como o APK deve ler e comparar o catálogo verdadeiro

1. Abrir sessão Station e executar `GET https://app.lzgames.com.br/v1/station/catalog` com o Bearer vigente; não buscar listas antigas, CDN, nomes de arquivo ou diretórios para completar a tela. Registrar se a resposta veio da rede ou de `StationCatalogStore` (`Library.cached`). Cache assinado permite exibir o último catálogo, mas não prova a revisão atual da 5192.
2. Usar `StationApi.catalogSnapshot`: conferir `keyId`, RSA-PSS/SHA256 do `payload` original, `schemaVersion=1`, domínio `TurboRamaStationAndroid/catalog/v1`, produto/aplicação e identidade de licença/aparelho/sessão. Só então chamar `StationCatalog.fromVerifiedPayload`. O limite atual é 12 MiB para envelope e 4096 `items`; `revision` global e `revision` de cada item são inteiros positivos.
3. Para cada item assinado, usar **exatamente** `itemId`, `name`, `platform`, `revision` e `coverId`; agrupar pelo valor bruto de `platform`, antes do rótulo visual. `StationFrontend` deve publicar todas as linhas verificadas ao serviço nativo e informar contagem recebida, contagem publicada e `cached`. `StationPlatforms.resolve` precisa de mapeamento explícito para cada nova plataforma. Dos 36 identificadores da lista histórica, 18 não são reconhecidos literalmente pelo código atual: `arcade`, `atomiswave`, `dreamcast`, `mastersystem`, `megadrivebr`, `model2`, `n64`, `n64br`, `nds`, `neogeo`, `neogeocd`, `nes`, `o2em`, `pcengine`, `pcenginecd`, `psx`, `sufami`, `switch`. Há rótulos visuais parecidos, mas o valor recebido da API deve ter correspondência explícita; publicar essas rotas sem atualizar o APK interromperia a preparação.
4. Para cada jogo visível, pedir `GET covers/{coverId}` na mesma sessão, conferir MIME e bytes e associar o cache a `coverId` + revisão do item. Se a capa retornar 404, mostrar placeholder e registrar esse `itemId`/`coverId` para correção do índice; não adivinhar uma imagem pelo nome. Um `coverId` compartilhado exige mesmo caminho e revisão no índice. Mudança de bytes exige nova revisão do item ou novo `coverId`.
5. Exportar o envelope **privadamente** e validar com `scripts/exportar-catalogo-assinado.py`, usando a chave pública SPKI Station do cliente. O script exige o `keyId` público esperado e foi exercitado com assinatura sintética; sua saída contém somente `platform,itemId,name,itemRevision,coverId`, ordenados, mais revisão/contagens. Nunca publicar o envelope, Bearer ou identidades nele contidas. Cruzar com o índice efetivo por `itemId` e `coverId`; comparar SHA256 do arquivo de jogo com o volume **quando os bytes forem idênticos**. Para capa redimensionada, usar a proveniência explícita do índice e comparar o SHA256 da capa efetivamente servida por HTTP. Nomes iguais, sobretudo os repetidos de Mega Drive, não bastam para criar pares. Sem o índice protegido ou um export autenticado com mapeamento de capas, **o cruzamento servidor ↔ jogo ↔ capa ainda não está comprovado**.

Exemplo de export depois de capturar a resposta autenticada **fora do Git** e salvar a SPKI pública DER obtida por decodificação Base64URL de `StationConfig.STATION_ASSERTION_SPKI_BASE64URL`; a ferramenta usa Python/cryptography, confere o `keyId` esperado, verifica a assinatura e cria a saída com modo 0600:

```text
python3 docs/station-android/scripts/exportar-catalogo-assinado.py \
  --envelope RESPOSTA_PRIVADA.json --public-key STATION_PUBLICA.der \
  --output CATALOGO_VERIFICADO.tsv
```

O mantenedor informou que disponibilizará uma cópia privada do índice efetivo. Quando ela chegar, usar `scripts/cruzar-indice-catalogo.py` (Python/Pillow) em diretório privado para exportar somente nomes, IDs, revisão, hashes, estado de leitura da capa, contagem de pares exatos por nome/hash e confronto por `itemId` com a resposta assinada. O script detecta `coverId` compartilhado com caminhos/revisões conflitantes e não copia `filePath`/`coverPath` para a saída; arquivo não legível permanece marcado como tal. Ainda será necessário conferir os bytes da capa HTTP contra o índice:

```text
python3 docs/station-android/scripts/cruzar-indice-catalogo.py \
  --index INDICE_PRIVADO.json \
  --disk-tsv docs/station-android/catalogo-disco-snes-megadrive-20261003.tsv \
  --catalog-tsv CATALOGO_VERIFICADO.tsv \
  --output CRUZAMENTO_PRIVADO.tsv
```

O contrato atual não pagina. A lista histórica de 12.346 excede 4096 itens; antes de publicar um catálogo desse porte, definir paginação versionada com revisão consistente e assinatura por página, atualizar o cliente e testar o carrossel. Para o subconjunto SNES/Mega Drive do volume, o candidato resolveu as capas e excluiu os 8 registros Mega Drive sem ROM. Ainda faltam conciliação com o índice efetivo, preservação dos IDs já publicados e provas HTTP na 5192/APK no aparelho. Não alterar a 5192 nem prometer catálogo completo por copiar o XML para a API.

## Referências e decisão

- Pedido atendido: [handoff do cliente reconstruído](https://github.com/luziellacerda/Servidor-pix/blob/7ac4fad9e0132db378f6e78e6494fedb08f614c3/docs/station-android/HANDOFF-CLIENTE-RECONSTRUIDO-STATION-20261002.md), branch `docs/cliente-reconstruido-station-20261002`, commit `7ac4fad9e0132db378f6e78e6494fedb08f614c3` do Servidor-pix.
- Cliente examinado originalmente: TurboElden, commit de código `0840028854034b03e5a1d3f2a162d66225932a6b` e revisão documental `f48399bc24691afe2073fac55f279b452c01343b`, branch `station-reconstrucao-20261002`. **Atualização:** o commit `7d5df08d922ef7c517979cf2263ff4bfe4be74ff` registra APK candidato integrado e instalado, com leitura tipada de `artifact`/`itemRevision`, mas sem prova de capa 200 e download/instalação contra a 5192 de produção.
- Código do servidor deste retorno: branch `feat/station-artifact-descriptor-20261002`, commits `96326aa0aeb164820cec26f8b5911fcdb47fc8ee`, `1bfb619c21becffe40aaa597e100fcb3719c2e72` e `de08858` (correção e homologação HTTP isolada), derivados de `b1159c9`.
- **Estado: código e contrato publicados para desenvolvimento; produção e APK ainda não liberados.** Não houve publicação de binário, alteração de índice real, migration de produção, restart da 5192, mudança de porta ou alteração dos serviços PIX/Suite/ES. O download HTTP autenticado passou somente na API temporária; não há prova dele na 5192 de produção.

## Rede e limites do aplicativo

O telefone usa somente `https://app.lzgames.com.br/v1/station/*` na porta 443: Cloudflare → túnel → nginx → API Station em `127.0.0.1:5192`. A porta 5190 é a API Suite Windows; 5191 é o gateway de conteúdo Suite; 5194 é o helper de emissão comercial Station. Nenhuma dessas portas, IP privado, caminho do disco ou URL de jogo entra no APK. Capa e jogo chegam como bytes autenticados da própria 5192, sem redirect. O APK não deve chamar `/v1/suite/*`, usar cookie, cache público ou gravar Bearer/grant no disco.

O cliente usa `productId=applicationId=TURBORAMA_STATION_ANDROID`, pacote `org.turboramastation.frontend`, `schemaVersion=1`, domains `TurboRamaStationAndroid/<ação>/v1` e `deviceId` Base64URL do SHA256 da chave pública SPKI RSA-2048 do Android Keystore. Respostas assinadas usam `{keyId,payload,signature}`; `payload` são bytes JSON UTF-8 em Base64URL sem padding, assinatura RSA-PSS/SHA256 sobre esses bytes. O `keyId` público configurado no cliente é `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`; comparar com uma resposta autenticada após implantação. O pin TLS SPKI SHA256 configurado no cliente é `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` e **foi confirmado novamente no endpoint público em 03/10/2026**. Não duplicar essas constantes em outra classe Android.

## O que roda no Linux, separado do código novo

| Verificação em 03/10/2026 | Resultado observado |
| --- | --- |
| Serviço | `turborama-station-api.service` ativo; PID 281271; sem drop-ins. |
| Comando efetivo | `/usr/bin/dotnet /opt/turborama-station-20261001/TurboRamaSuiteOnlineServer.dll`. |
| SHA256 do DLL efetivo | `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2`. Este hash identifica o binário; não prova sozinho um commit de origem. |
| Escuta | `127.0.0.1:5192`, sem porta 5192 pública. O único endereço que o APK usa é `https://app.lzgames.com.br/v1`. |
| Sondas sem credencial | `/ready/station` local 200; `/v1/station/catalog` público 401; capa pública 401; grant inexistente 404. Essas respostas não comprovam conteúdo autenticado. |
| Logs desde o início do processo | `/me` 200 em 3 requisições e `/catalog` 200 em 3; `/covers` 404 em 48 e nenhum 200; nenhuma autorização/download 200. IDs e tokens não foram extraídos nem publicados. |
| Índice carregado | O arquivo configurado e seu conteúdo não puderam ser lidos nesta conta. Revisão, quantidade e valores de `platform` **não foram conferidos ao vivo**. |
| Inventário geral somente leitura | nginx, cloudflared, PostgreSQL, PIX, Suite 5190 e gateway 5191 ativos. `turborama-suite-content-monitor.service` estava failed no checkpoint inicial; a nova leitura às14h12 mostra inactive/dead, Result=success, ExecMainStatus=0. Não foi alterado nesta tarefa. Registrar esse estado e a política do timer no gate operacional. |

O handoff anterior cita 996 jogos no índice e os identificadores `megadrive`, `snes`, `snesbr`, `gamegear`, `gb`, `sega32x`, `gbc`, `gba`; são dados documentais, não resultado de uma resposta autenticada capturada agora. O mesmo material cita 835 jogos SNES e 981 Mega Drive no disco; quantidade no disco não equivale ao catálogo. O limite atual da API é 4096 itens: a lista documental de 12.346 exige evolução explícita do contrato antes de caber no catálogo.

Após o reinício do PC, às 08h37 de 03/10/2026, a 5192 voltou ativa com PID 2388, mesmo `ExecStart`, sem drop-ins e mesmo SHA256 de DLL acima; `/ready/station` local respondeu 200. O inventário de leitura encontrou PIX, Suite, gateway, nginx, cloudflared e PostgreSQL ativos. O monitor de conteúdo estava `inactive` com último `Result=success`, estado diferente do `failed` visto antes do reinício; a causa e a política de execução do monitor não foram investigadas nesta rodada.

A configuração protegida da 5192 continuou ilegível para esta conta; `sudo -n` pediu senha. Portanto o caminho configurado do índice e seu conteúdo seguem sem verificação direta. Não foram lidos segredos nem caminhos de jogos.

## Ativação, sessão e perfil: divergência resolvida no código

`STA-` é o prefixo do **licenseId** criado em `StationCommerceEndpoints.ProvisionAsync`: `STA-` seguido por 32 dígitos hexadecimais maiúsculos. Não é a senha que o comprador digita. `StationCommerceEndpoints` emite o `activationCode` com 32 bytes aleatórios codificados em Base64URL canônico sem `=` (43 caracteres). Exemplo **sintético e não válido comercialmente**: `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA`. O servidor verifica exatamente Base64URL canônico de 32 bytes em `StationService.Verifier`, tanto no pedido de challenge quanto no complete. Espaços, quebras, `=`, mudança de caixa e prefixo `STA-` não são normalizados. O cliente Java `token(code)` está alinhado com o formato do código emitido; o texto do handoff humano que chamava o código de `STA-` confundiu licença com código. Códigos comerciais já emitidos não foram alterados.

O challenge de ativação vale 60 s; o complete exige prova RSA-PSS do aparelho e o mesmo código/verificador. O challenge de sessão também vale 60 s; a sessão Bearer vale 180 s e está vinculada a licença, aparelho e revogação. `/v1/station/me` devolve payload assinado `profile/v1` com `displayName` (até 80 caracteres no retorno) e `profileVersion`, além de `licenseId`, `deviceId` e `sessionId`. Perfil ausente retorna 503 `STATION_PROFILE_NOT_READY`.

## Sequência HTTP exata para o APK

Todos os caminhos abaixo começam em `https://app.lzgames.com.br/v1/station/`. Pedidos JSON de identidade usam `schemaVersion`, `domain`, `productId`, `applicationId`, `deviceId`, `clientVersion`, `deviceManufacturer`, `deviceModel`, `androidSdk` em camelCase; `domain` é o valor completo mostrado na tabela. Os dois pedidos de prova usam envelope `{payload,signature}` assinado pelo aparelho. Respostas de controle são envelopes assinados pelo servidor; imagens e jogos são bytes, não JSON. Verificar `keyId`, assinatura, domain, produto, aplicação, licença, aparelho e sessão antes de usar o payload. Desativar redirects em todas as chamadas.

| Ordem | Método e rota | Pedido adicional / domain completo | Resposta e decisão do APK |
| --- | --- | --- | --- |
| 1, somente na primeira ativação | `POST activations/challenge` | `activationCode`, `devicePublicKey`; `TurboRamaStationAndroid/request-activation-challenge/v1` | `activation-challenge/v1`, `challengeId`, `nonce`, 60 s. |
| 2 | `POST activations/complete` | Prova PSS com `activationCode`, `challengeId`, `nonce`, chave pública; `TurboRamaStationAndroid/activate/v1` | `activated/v1`, `licenseId`; salvar somente licença em armazenamento privado sem backup. |
| 3, a cada sessão | `POST challenges` | `licenseId`; `TurboRamaStationAndroid/request-session-challenge/v1` | `session-challenge/v1`, `challengeId`, `nonce`, 60 s. |
| 4 | `POST sessions` | Prova PSS; `TurboRamaStationAndroid/open-session/v1` | `session/v1`, `accessToken`, 180 s; manter Bearer só em memória. |
| 5 | `GET me` | `Authorization: Bearer <accessToken>` | `profile/v1`, `displayName`, `profileVersion`; 503 preserva o último nome confiável. |
| 6 | `GET catalog` | Mesmo Bearer | `catalog/v1`, `revision`, `items[]` de `itemId`, `name`, `platform`, `revision`, `coverId`; cache privado do envelope assinado. |
| 7, somente capas visíveis/ausentes | `GET covers/{coverId}` | Mesmo Bearer; `coverId` do catálogo | Bytes PNG/JPEG/WebP/GIF; cache por `coverId` + revisão do item. |
| 8, quando o usuário abre jogo ausente | `POST downloads/authorize` | JSON de identidade + `itemId`; `TurboRamaStationAndroid/request-download/v1`; mesmo Bearer | `download-grant/v1` assinado com `grantId`, `itemRevision`, `artifact`, 60 s. |
| 9, uma vez por grant | `GET artifacts/{grantId}` | Mesmo Bearer e sessão; sem Range | Bytes `application/octet-stream`, `Content-Length` obrigatório; conferir hash antes de instalar. |

`itemId` e `coverId` aceitos pelo servidor têm 8–64 caracteres ASCII `[A-Za-z0-9_-]`; usar exatamente os valores assinados do catálogo. Não usar o nome exibido como pasta, URL ou identificador. `challengeId` e `sessionId` são 64 caracteres hexadecimais minúsculos; `nonce`, `accessToken` e `grantId` são Base64URL canônicos de 32 bytes. O cliente já limita o catálogo a 4096 itens e o envelope a 12 MiB; não reduzir esse leitor ao limite de 8 KiB dos pedidos JSON.

## Contrato novo de download, ainda não implantado

Rotas e métodos permanecem `POST /v1/station/downloads/authorize` e `GET /v1/station/artifacts/{grantId}`. Pedido de autorização continua com schema 1, domínio `request-download`, identidade Station e `itemId`, sob Bearer válido. A resposta é o envelope `keyId`, `payload`, `signature`: payload JSON UTF-8 Base64URL canônico, domínio `download-grant/v1`, assinado com RSA-PSS/SHA256; `keyId` é SHA256 hexadecimal minúsculo da chave pública SPKI. O cliente deve verificar a assinatura antes de usar qualquer campo. A resposta agora acrescenta `itemRevision` e `artifact` dentro desse payload, preservando `itemId`, `grantId`, `expiresInSeconds` (60), licença, aparelho e sessão.

| Campo no payload assinado | Tipo e regra |
| --- | --- |
| `itemRevision` | inteiro positivo; igual à revisão do item no catálogo carregado. |
| `artifact.fileName` | string de 1 a 255 caracteres; nome base real, sem `/`, `\`, `:`, controles ou `.`/`..`. |
| `artifact.sizeBytes` | inteiro positivo até 1 TiB; tamanho do arquivo servido. |
| `artifact.sha256` | 64 hexadecimais minúsculos; SHA256 do arquivo servido. |
| `artifact.format` | `raw`, `zip`, `rar` ou `7z`; extensão e assinatura binária conferidas. |
| `artifact.launchPath` | string de 1 a 512 caracteres; caminho relativo dentro do item, com `/`, sem componente vazio, `.`/`..`, `\`, `:` ou controles. Para `raw`, igual a `fileName`. |
| `artifact.expandedSizeBytes` | inteiro positivo até 4 TiB; para `raw`, igual a `sizeBytes`. |
| `artifact.fileCount` | inteiro de 1 a 100.000; para `raw`, 1. |

Exemplo **sintético** do conteúdo decodificado de `payload`, para mostrar os nomes e tipos. O envelope assinado real contém esses bytes em Base64URL; os valores abaixo não são licença, sessão ou grant utilizável:

```json
{
  "schemaVersion": 1,
  "domain": "TurboRamaStationAndroid/download-grant/v1",
  "productId": "TURBORAMA_STATION_ANDROID",
  "applicationId": "TURBORAMA_STATION_ANDROID",
  "licenseId": "STA-00000000000000000000000000000000",
  "deviceId": "<Base64URL do aparelho>",
  "sessionId": "<sessão do aparelho>",
  "itemId": "item-sintetico-01",
  "itemRevision": 1,
  "artifact": {
    "fileName": "item.bin",
    "sizeBytes": 4,
    "sha256": "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
    "format": "raw",
    "launchPath": "item.bin",
    "expandedSizeBytes": 4,
    "fileCount": 1
  },
  "grantId": "<Base64URL de 32 bytes>",
  "expiresInSeconds": 60
}
```

Historicamente, o cliente Java do commit `0840028` ignorava `itemRevision` e `artifact`. No APK candidato documentado em `7d5df08`, `StationApi.authorize` lê o descritor tipado e compara a revisão com o catálogo; `StationFiles`/instalador foram integrados, mas o teste real de download na 5192 ainda falta. O instalador deve continuar recusando grant sem descritor. Os campos pertencem ao payload assinado da rota existente; não criar endpoint ou link de jogo. `fileName` e `launchPath` são metadados assinados, jamais vindos da URL nem deduzidos da plataforma.

O `filePath` privado fica só no índice e no grant cifrado por AES-GCM; não aparece no catálogo, descritor ou cabeçalhos. O grant cifrado vincula caminho, revisão, hash, tamanho e data de modificação à licença, aparelho, **sessão**, item e grantId. O consumo é único. Ao consumir, o servidor compara o índice e a identidade do arquivo; mudança detectada nega com 404. O endpoint abre o arquivo antes do 200 e envia `application/octet-stream`, `Content-Length` exato, `Cache-Control: no-store`, sem URL, redirecionamento, `Content-Disposition` ou Range. Se a conexão cair depois do consumo, o cliente precisa pedir **nova autorização** e começar nova transferência; o grant anterior não é retomável.

Na carga do índice, o servidor verifica tamanho, assinatura binária e SHA256 de cada arquivo com descritor. Para ZIP também confere membros seguros, `launchPath`, total extraído e número de arquivos. A ferramenta `scripts/preparar-indice-artefatos.py` produz os descritores fora da requisição, inspeciona ZIP/RAR/7z, exige escolha explícita de `launchPath` quando há vários arquivos e escreve o novo índice com modo 0600. Exemplo de uso, com caminhos **locais e privados definidos pelo operador**, nunca pelo APK:

```text
python3 docs/station-android/scripts/preparar-indice-artefatos.py \
  --index INDICE_ATUAL.json --launch-manifest ESCOLHAS.json --output INDICE_NOVO.json
```

O manifesto é um objeto JSON `itemId` → `launchPath`, por exemplo `{"item-sintetico":"disc/game.cue"}`. Itens raw ou arquivos compactados de membro único dispensam escolha. RAR/7z têm membros conferidos pela ferramenta `7z` durante o preparo; a API confere assinatura, tamanho e SHA256 na carga, mas não reinspeciona membros desses dois formatos. O arquivo aprovado precisa permanecer imutável durante a validade do grant. Antes de implantação, validar que o armazenamento real aplica essa condição. A mudança impede ativar downloads novos em itens cujo índice ainda não tem descritor: eles retornam 503 `STATION_ARTIFACT_NOT_READY`. Por isso **não é seguro instalar este binário sobre o índice atual sem prepará-lo e validá-lo**.

Erros relevantes: 401 `STATION_SESSION_INVALID`; 403 `STATION_DEVICE_DENIED`; 404 `STATION_ITEM_NOT_FOUND` para ID fora do catálogo; 503 `STATION_ARTIFACT_NOT_READY` para metadados ausentes ou arquivo mudado; 404 `STATION_GRANT_NOT_FOUND` para grant inexistente, consumido, expirado, de outra sessão/aparelho/licença ou com vínculo divergente; 429 `STATION_RATE_LIMITED` (limite atual de 30 requisições por IP/rota/minuto); 400 para JSON/identidade malformados. Revogação de licença e outro aparelho passaram no banco temporário; bloqueio comercial/suspensão/admin passaram no helper HTTP isolado; produção ainda precisa de prova específica.

Erros JSON usam `schemaVersion`, `code`, `message`; o app toma decisão por HTTP e `code`, não pelo texto de `message`. 401 pede renovação de sessão; 404 de grant pede nova autorização se a licença continuar válida; 429 exige espera; 503 de índice/artefato não autoriza inventar caminho local. Não repetir automaticamente a ativação comercial.

### Exemplos de instalação, todos sintéticos

| Caso | `format` e `fileName` | `launchPath` escolhido | `fileCount` e `expandedSizeBytes` | Estado da prova |
| --- | --- | --- | --- | --- |
| ROM crua | `raw`, `item.bin` | `item.bin` | 1 e 4 | Teste local: bytes `01 02 03 04`, SHA256 `9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a`. |
| Contêiner com BIN/CUE | `zip`, `multi.zip` | `disc/game.cue` | 2 e 5 | ZIP sintético validado no teste local; o SHA256 é calculado no preparo, não é um jogo real. |
| Wii U com diretórios | `7z`, `wiiu-exemplo.7z` | `code/game.rpx` | Depende da inspeção do pacote; exemplo de três arquivos em `code/`, `content/`, `meta/`. | Estrutura ilustrativa; não há pacote Wii U de produção inspecionado nem hash real a informar. |

Nenhuma dessas linhas configura plataforma ou arquivo real de produção. O instalador Android deve conferir assinatura, item/revisão, `Content-Length`, SHA256, formato, limites de extração e `launchPath` antes de registrar o jogo como instalado.

## Capas e cache

O código novo mantém `coverId` → `coverPath` do índice, confere tamanho de 1 byte a 5 MiB e assinatura PNG/JPEG/WebP/GIF compatível com o MIME. IDs de capa compartilhados só são aceitos se caminho **e revisão do item** forem iguais; ambiguidade falha na carga do índice. A revisão de capa continua sendo a revisão do item, pois não há `coverRevision` separado. Ao trocar bytes, publicar nova revisão do item ou novo `coverId`; o cache local do APK usa ID e revisão. A resposta HTTP usa `no-store`, e ID inexistente gera 404 depois da autenticação.

Teste local de `StationLibrary.ReadCover`: PNG sintético válido de 1×1 pixel, 70 bytes, `image/png`, SHA256 `c2153f77e11087fcb078ae38527fa83bef29791e3700e30cc87fec4405a66d0f`. Isso **não é uma resposta HTTP autenticada 200**. O recorte inicial tinha 48 respostas 404; às 12h24 foram observadas 83 no período descrito acima, sem capa 200. A causa precisa ser identificada no índice efetivamente carregado, permissões/arquivos e IDs pedidos pelo APK, sem expor caminhos privados. O 404 autenticado de ID inexistente também precisa ser registrado com uma sessão sintética válida.

## Evidências e pendências para liberar o APK

| Requisito do pedido | Comprovado aqui | Falta, responsável |
| --- | --- | --- |
| Código do descritor e teste | Commits acima; `dotnet run --project tests/TurboRamaSuiteOnlineServer.Tests/TurboRamaSuiteOnlineServer.Tests.csproj` passou. Testes locais cobrem raw, ZIP com dois arquivos, membro ZIP duplicado, hash divergente, `launchPath` ausente, revisão de capa conflitante e PNG. A ferramenta de preparo foi exercitada com raw, ZIP e 7z sintéticos. O teste HTTP isolado passou em 03/10/2026. | Backend: revisão do diff `de08858` e validação de carga no índice real. |
| Catálogo autenticado: revisão, total, plataformas | Logs mostram três HTTP 200 de `/catalog`, sem corpo. | Operação/backend: sessão sintética e captura sanitizada de `revision`, quantidade e `platform` exatos da 5192. |
| Capa válida 200 e ID inexistente 404 | Unidade local e HTTP isolado: PNG 200 com `image/png`, bytes conferidos; ID inexistente 404 `STATION_COVER_NOT_FOUND`. Público sem Bearer responde 401. | Operação/backend: corrigir os 404 atuais e repetir 200/404 autenticados na 5192 implantada com MIME/tamanho/hash. |
| Autorização assinada e bytes completos | HTTP isolado: `itemRevision=3`, descritores raw/ZIP assinados com `keyId` verificado, `Content-Length`, bytes e SHA256 conferidos. | Produção: repetir somente depois da implantação validada e do índice real preparado. |
| Reuso, expiração, bloqueio e outro aparelho | HTTP isolado: segundo GET 404, expiração forçada 404, revogação 404, outro aparelho 403, outra sessão 404 sem consumo e queda de conexão seguida de 404. | TTL real de 60/180 s, renovação de sessão e nova autorização após queda passaram com a DLL candidata em isolamento; bloqueio/desbloqueio/revogação/transferência e suspensão financeira passaram por HTTP no helper isolado; produção continua pendente. |
| Índice/arquivos real e imutabilidade | O índice efetivo não ficou acessível à conta desta apuração. | Operação/backend: preparar descritores, validar todos os itens e capas, tempo/custo de leitura SHA256 na inicialização, permissões e imutabilidade durante 60 s. |
| APK instalado e conexão ponta a ponta | APK candidato SHA256 `f5b35419fcff4188b8e86690045371892c77933e7f8edfc07bce1d5bd25d418a` instalado em 03/10/2026 às 12:27:01; licença salva reutilizada, interface abriu e recebeu 996 itens. O cliente novo lê o descritor, mas não obteve capa 200 nem download real comprovado. | Android/backend: total fresco de 996 confirmado; obter histograma/IDs privados, completar jogos/capas no servidor, provar download e instalação no aparelho, remover legado residual e promover só após aceite. |

## Compatibilidade e ordem de liberação

| Combinação | Resultado esperado agora |
| --- | --- |
| 5192 atual + APK instalado atual | API responde, mas capas mostram 404 nos logs e não há download completo provado. Não é aceite. |
| 5192 atual + cliente reconstruído | O servidor atual não inclui `artifact`/`itemRevision`; o instalador novo deve recusar, sem inventar extensão ou marcar instalado. |
| Código novo + índice antigo sem descritores | Catálogo pode abrir, mas `POST downloads/authorize` de item existente devolve 503 `STATION_ARTIFACT_NOT_READY`. Não implantar nessa combinação. |
| Código novo + índice preparado + APK antigo | O contrato acrescenta campos, mas o APK antigo não sabe instalar por eles; não declarar sucesso. |
| Código novo + índice/capas validados + APK novo integrado | Candidato de homologação; liberar somente após as provas autenticadas e no aparelho abaixo. |

## Plano único de execução, responsáveis e aceite

### 1. Backend e operação: preparar o conteúdo real antes de trocar a 5192

1. **Operação/backend:** com acesso autorizado ao arquivo `Station:LibraryIndexFile` efetivamente configurado, registrar somente revisão, contagem e histograma exato de `platform` em relatório sanitizado. Conciliar o índice privado com o candidato SNES/Mega Drive e seu `source-map.json` por ROM, preservando IDs já publicados e as demais plataformas; rejeitar pares ambíguos. Conferir `Station:Enabled`, `Station:DownloadKeyFile`, chave pública/`keyId` e migrations 028 e 029 no banco **sem copiar segredos**. O índice é privado e não entra no Git. Se houver mais de 4096 itens, planejar paginação/versão nova do catálogo antes de publicar os excedentes; não truncar silenciosamente.
2. **Operação/backend:** diagnosticar os 83 retornos 404 de capa do recorte 12h24 com um `coverId` **obtido do catálogo autenticado**, existência/permissão do `coverPath` correspondente, MIME real, tamanho e revisão. Corrigir índice/arquivos/permissões sem expor caminhos. Se o mesmo `coverId` for compartilhado, caminho e revisão precisam coincidir; ao mudar bytes, incrementar revisão do item ou emitir novo `coverId`.
3. **Operação/backend:** preparar `artifact` para cada item a liberar com `preparar-indice-artefatos.py` e escolhas explícitas de `launchPath` para pacotes de múltiplos arquivos; validar em cópia privada antes de ativar. Conferir arquivos raw, BIN/CUE, RAR/7z e diretórios Wii U reais; registrar hashes e contagens agregadas sem caminhos privados no handoff. Garantir que os arquivos publicados não mudem durante 60 s de grant. A carga do código novo lê e verifica SHA256 de cada arquivo com descritor; medir tempo de inicialização e I/O antes do rollout.
4. **Backend/QA:** o teste HTTP isolado do commit `de08858` cobriu assinatura, sessão, catálogo, capa válida/inválida, raw, ZIP múltiplo, grant de um uso, expiração forçada, revogação, outro aparelho, outra sessão e queda de conexão. TTL real de desafios/grant/sessão e nova autorização após queda passaram com a release empacotada em isolamento; bloqueio comercial/admin também passou por HTTP no fixture; completar provas do painel/provedor e produção; registrar status, `code`, revisão, MIME, tamanho e SHA256 sem Bearer ou grant no documento.
5. **Operação:** preparar release imutável do código que inclui `de08858`, registrar SHA256 do DLL candidato, backup restaurável de índice/configuração/banco afetado, migration necessária, alvo exato `turborama-station-api.service` e rollback para o DLL/índice anteriores. Validar os serviços compartilhados e esclarecer o estado do monitor de conteúdo antes da janela. Implantar somente em tarefa de mudança de produção autorizada e conferida; não tocar 5190, 5191, 5194, PIX ou outros produtos. Depois, repetir as provas autenticadas na 5192 pública via HTTPS, não apenas `/ready/station`.

### 2. Android: validar e concluir o APK candidato

O runtime `629a55a8` e sua revisão documental `db68b613` registram o APK instalado f5b35419; a fonte corretiva `645e7c4` ainda aguarda build/instalação; a lista é o critério de aceite a comprovar, especialmente com catálogo completo e download real.

1. **Android:** conferir no APK candidato a leitura **obrigatória** de `itemRevision` e todos os campos `artifact` do payload assinado, já implementada em `StationApi.Grant`. Comparar item/revisão com o catálogo da mesma sessão; exigir hash e tamanho esperados em `StationFiles`. Rejeitar descritor ausente, inválido ou formato não suportado. Preservar `StationConfig` (host, pin TLS, autoridade pública) e o alias do Keystore. Não trocar o código de ativação por `STA-`: esse prefixo identifica a licença, não a senha emitida.
2. **Android/nativo:** validar os 94 jogos da categoria `megadrivebr`, cujo alias já está no APK f5b35419, e integrar a revisão final `02c09dd` e validar a ligação já feita de `StationCoordinator` ao carrossel/catalog service e às telas reais, preservando seleção, texturas, jogos instalados, saves e emuladores. Usar `platform` somente por mapeamento explícito, inclusive edições BR; plataforma desconhecida não vira pasta por aproximação. Usar `coverId`/revisão no cache, carregar somente capas visíveis e tratar 404/429 sem tempestade de pedidos. Concluir a retirada das chamadas antigas de catálogo, licença, telemetria e URLs de jogo do frontend anterior, sem atingir redes internas dos emuladores.
3. **Android/instalador:** quando um jogo estiver ausente, autorizar uma vez, baixar sequencialmente para temporário privado com o **mesmo Bearer**, recusar 3xx/`Location` e Range, confrontar `Content-Length`, tamanho e SHA256, e só então processar `raw|zip|rar|7z`. Para compactados, limitar arquivos/tamanho extraído, rejeitar caminho absoluto, `..`, links e duplicatas, conferir `launchPath` e referências auxiliares como BIN/CUE. Usar publicação atômica e manifesto de arquivos: parcial, falha ou cancelamento nunca viram “instalado” nem substituem jogo/saves íntegros. Em queda após consumo, pedir outro grant; nunca repetir o GET consumido.
4. **Android/build:** o APK candidato completo já foi instalado com atualização e assinatura preservada; confirmar que DEX e bibliotecas usam somente o cliente Station novo nas rotas comerciais. Preservar pacote, dados privados, emuladores e saves. Completar no aparelho: ativação quando necessária, retomada por licença salva, `/me`, catálogo fresco, capa 200, download, cancelar, abrir jogo, voltar, apagar somente arquivos do jogo e relançar offline conforme política definida. Registrar hash e versão do APK aceito antes de promover.

### 3. Matriz mínima de provas para fechar este mesmo handoff

| Prova | Resultado de aceite | Situação em 03/10/2026 |
| --- | --- | --- |
| Autenticação | Challenge/complete com código sintético emitido, sessão 180 s, `/me` com nome, sessão vencida 401, aparelho/licença bloqueados negados. | HTTP isolado passou para ativação, sessão, `/me`, aparelho estranho 403 e revogação 404 no grant; TTL de desafios 60 s e sessão 180 s passou por relógio real na DLL empacotada; produção pendente. |
| Catálogo | HTTPS 200 autenticado, assinatura/keyId verificados, revisão, total e plataformas exatos; sem URL/caminho de jogo. | HTTP isolado passou com 1.818 itens assinados (1.816 do volume + 2 sintéticos); carregador real aceitou os 1.816 em disco. Produção tem três 200 em logs, sem payload sanitizado. |
| Capas | `coverId` real 200, MIME decodificável e SHA256 conferido; ID inexistente 404; mudança de revisão invalida cache. | HTTP isolado 200/404 passou, inclusive uma capa real por categoria; o índice candidato teve 1.816 capas legíveis no carregador. O recorte 12h24 registrou 83 capas 404, sem 200 observado; o APK f5 confirmou o code tipado. |
| Autorização | Payload `download-grant/v1` assinado inclui todos os campos da tabela; `itemRevision` coincide com catálogo; grant 60 s. | HTTP isolado validou assinatura, campos e revisão para os itens sintéticos e um jogo real de cada categoria; duração real de 60 s passou na DLL empacotada em isolamento; produção pendente. |
| Bytes e erros | GET único 200 com Content-Length e SHA256 iguais ao descritor; segundo GET, expirado, outro aparelho/sessão e bloqueio negados; queda exige novo grant. | HTTP isolado passou para raw/ZIP sintéticos e bytes/hash de um jogo real por categoria, reuso, expiração forçada, outra sessão, aparelho estranho, revogação e queda seguida de negação. Nova autorização depois da queda e download completo com hash correto passaram em isolamento; produção pendente. |
| APK | APK completo assinado e instalado sem perda de dados, interface nativa ligada, instalação segura e jogo abre; hash/versão documentados. | Candidato `f5b35419...` instalado e carrossel com 996 itens; login por licença salva passou. Capa 200, download/instalação real, jogo aberto, cobertura completa do catálogo e remoção do legado ainda pendentes. |

Aceite final exige evidência separada de **desenvolvimento/homologação** e **produção** em cada linha. Atualizar esta tabela e os dados observados, sem criar outro handoff de retorno. Os nomes de jogos foram incluídos nas duas listas de comparação por pedido do mantenedor; não publicar tokens, códigos comerciais, dados do comprador, URLs privadas, caminhos do armazenamento ou chaves privadas.

Na DLL antiga implantada, `/ready/station` 200 confirma apenas a migration 028/banco. Na candidata `45fe4df`, exige também a 029; nenhuma das duas sondas verifica o índice inteiro, capas, chave de grants ou fluxo de bytes. Não há evidência para declarar servidor ou APK prontos para produção agora. Este documento descreve a mudança necessária; não executa nem autoriza implantação ou restart.
