# Handoff integral — revisão conjunta do app TurboStations e do servidor Station

**Destinatário: equipe/IA que mantém o Servidor-pix. Pedido do mantenedor em 03/10/2026: analisar o código do aplicativo, encontrar erros do cliente e do servidor e devolver uma conclusão comprovada.**

Este documento substitui os resumos anteriores como entrada desta revisão. Não declara o cliente correto nem atribui os erros ao servidor sem rastrear a requisição. A quantidade incompleta de jogos, as capas ausentes e a autorização que falha continuam sem resolução ponta a ponta.

## 1. Identificação inequívoca do trabalho

| Referência | Valor |
|---|---|
| Repositório do aplicativo | https://github.com/luziellacerda/TurboElden |
| Nova branch para esta revisão | `revisao-integracao-station-servidor-20261003` |
| Commit de código auditado, incluído na nova branch | `629a55a8cf48722460007944cf0bb737e9f8fb75` |
| Pasta de fontes publicada | `versions/station-reconstruction-20261002/` |
| Repositório do servidor | https://github.com/luziellacerda/Servidor-pix |
| Branch do último retorno lido | `feat/station-artifact-descriptor-20261002` |
| Commit completo do retorno e código servidor comparado | `64912e1f2294b99967fc638d842f505cff7f629c` |
| Documento daquele retorno | `docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md` |
| Aplicativo em escopo | TESTE, pacote `org.turboramastation.frontend` |
| Namespace Java | `org.emulationstation.frontend` — não confundir com applicationId Android |
| Produto/applicationId do protocolo Station | ambos `TURBORAMA_STATION_ANDROID` |
| Estado | candidato instalado; sessão/perfil/catálogo confirmados; capas e downloads falham; não estável |

A branch de revisão contém por ancestralidade todas as alterações de código do commit acima. A entrega desta revisão adiciona documentação, inventário e evidências existentes; não contém uma nova correção de runtime. O pedido no Servidor-pix identificará o commit completo desta entrega documental, evitando dependência de uma branch que possa avançar.

Fontes verificáveis: [árvore do cliente auditado](https://github.com/luziellacerda/TurboElden/tree/629a55a8cf48722460007944cf0bb737e9f8fb75/versions/station-reconstruction-20261002) e [retorno do servidor](https://github.com/luziellacerda/Servidor-pix/blob/64912e1f2294b99967fc638d842f505cff7f629c/docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md).

### 1.1 Pastas locais e artefatos

| Uso | Caminho Windows exato |
|---|---|
| Fonte canônico de montagem | `E:\ESTUDO APK\work\turbostations-reconstruction-20261002` |
| APK candidato atual | `E:\ESTUDO APK\work\turbostations-reconstruction-20261002\build\apk\TurboStations-Station-CANDIDATO-20261003.apk` |
| Base imutável utilizada | `E:\ESTUDO APK\work\native-carousel\implementation\side-by-side-turborama-20261001\TurboramaStation-TESTE-lado-a-lado.apk` |
| Clone app | `C:\Users\Admin\Documents\Codex\2026-09-28\turboramaemutestes-apk-e-estudo-apk-work\work\TurboElden-git` |
| Clone servidor existente | `E:\ESTUDO APK\work\server-auth-handoff\Servidor-pix-implementar-faltas-20261002` |
| Área histórica de handoffs | `E:\ESTUDO APK\work\server-auth-handoff` |

Compilação e temporários ficam em E:. O APK de aproximadamente 1,9 GB e as credenciais de assinatura não são anexados a este Git. O código, scripts e evidências estão publicados. Se o revisor no Linux precisar executar o APK, solicitar transferência autorizada do arquivo pelo hash abaixo; não reconstruir a partir de outro APK chamado “TESTE”.

### 1.2 Identidade do instalado

| Campo | Valor |
|---|---|
| SHA256 APK | `f5b35419fcff4188b8e86690045371892c77933e7f8edfc07bce1d5bd25d418a` |
| Tamanho | `1902718870` bytes |
| versionCode / versionName | `11` / `1.0.8-turboeden-unico` |
| SHA256 certificado | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |
| SHA256 APK base | `d810434352f7ad2e7d1d08efe44f31eb76d132ffc43ba3b81b9ffc9e64efae1b` |
| Instalação conferida | 03/10/2026 12:27:01, horário do registro local |
| Conferência no aparelho | hash do base.apk instalado igual ao candidato |

Os hashes históricos `43670211…`, `eaebf48b…`, `38e78fde…`, `325dff61…` e a estável Cemu de 30/09 **não identificam o instalado atual**. O campo `clientVersion="1"` no protocolo também não distingue essas revisões. Usar o SHA256, pacote e evidência de instalação.

## 2. Sintomas, provas e lacunas

### 2.1 O que foi observado no aparelho

Os registros atuais estão em [closure-validation.json](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/closure-validation.json), [closure-station-log.txt](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/closure-station-log.txt), [apk-report.json](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/apk-report.json) e [route-count-crosscheck.json](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/route-count-crosscheck.json).

| Operação | Observação | O que ela não demonstra |
|---|---|---|
| Sessão | HTTP 200, acesso salvo retomado | nova ativação de comprador novo nesta última instalação |
| Perfil | HTTP 200 | atualização comercial de todos os campos do painel |
| Catálogo | HTTP 200; `CATALOG_NETWORK count=996`, rede nova | revisão/histograma bruto por slug, índice Linux efetivo ou acervo completo |
| Publicação ao nativo | recebido/aplicado/publicado 996 | capacidade de todo catálogo histórico de 12 mil |
| Capa | 404 `STATION_COVER_NOT_FOUND` | causa exata: ID, índice, caminho, bytes ou serviço |
| Autorizar download | 404 `STATION_ITEM_NOT_FOUND` | qual item exato ou DLL respondeu; ROM necessariamente ausente |
| Transferência | não iniciou | GET do artefato, SHA256 real, instalação e jogo |
| Manter conectado | compilado/instalado, acesso salvo retomado | alternância individual do checkbox na UI, ainda não conferida |

`authenticatedFlowVerified=false` no relatório significa que o fluxo completo não passou; não contradiz os HTTP 200 de sessão/perfil/catálogo. Nenhuma capa 200, autorização 200 real, instalação nova ou abertura de jogo pelo fluxo reconstruído foi comprovada.

### 2.2 Contagens — distinguir rede de exportação nativa

O total **996** foi registrado na rede e na publicação Java/nativa. O histograma abaixo foi contado no arquivo exportado pelo frontend nativo, `/storage/emulated/0/EmulationStation/native-catalog.tsv`, modificado em `2026-10-03 12:29:42.459253777 -0300`.

| Nome no frontend | Itens |
|---|---:|
| Gameboy | 22 |
| Gameboy Color | 7 |
| Gba | 5 |
| MegaDrive | 693 |
| Super Nintendo | 176 |
| Super Nintendo - BR | 28 |
| gamegear | 58 |
| sega32x | 7 |
| Total | 996 |

**Não há neste pacote de evidências um payload autenticado sanitizado com o histograma por slug antes do mapeamento.** Não escrever “o HTTP registrou 176 SNES”: esse número veio da exportação nativa. A leitura do código mostra publicação de todos os itens aceitos; isso precisa ser cruzado com o payload exato da mesma sessão e com o índice que o processo Linux carregou.

O mantenedor espera mais de 800 jogos de SNES. No retorno do servidor, o candidato tem 644 `snes` + 191 `snesbr` = **835 somando os dois grupos**. Não prometer 835 só na célula SNES comum. Perguntar ao dado/indexação, não alterar números no aplicativo para parecer correto.

### 2.3 Instrumentação insuficiente que precisa ser reconhecida

O logger `StationDiagnostics` registra categorias fixas, status e contagem; não registra itemId, coverId, request-id, payload ou token. Isso evita exposição de dados, mas impede correlacionar sozinho os 404 com um item específico. Os próximos testes devem produzir correlação controlada, com IDs pseudonimizados ou mantidos em evidência privada, nunca Bearer/código/dispositivo/URL privada no Git.

## 3. Arquitetura real: partes novas e partes preservadas

Fluxo atual:

1. `LoginActivity` → `StationLogin` → `StationAndroid` cria API, sessões, caches e coordenador.
2. Ativação, se necessária → sessão assinada → `/me` → `/catalog`.
3. `StationFrontend` prepara todos os itens e chama JNI de `libstation_frontend.so`.
4. `libmain.so` tem 32 entradas de serviço substituídas para delegar ao módulo novo.
5. `libturbo_carousel.so` e a interface nativa existente continuam renderizando células e controles.
6. Baixar: seleção nativa → itemId → coordenador → autorização → arquivo verificado → instalação transacional.
7. Jogar: recibo/caminho instalado → launcher nativo preservado → emulador já integrado.

**A interface nativa inteira não foi reconstruída de código-fonte original.** Há uma ponte C++ nova e substituição binária de funções específicas de `libmain.so`. Renderer, launcher e bibliotecas dos emuladores continuam da base. Isso atende parte da integração, mas não equivale a remover fisicamente todo o legado ou a uma compilação limpa de todo o app.

### 3.1 Alterações presentes no APK

| Entrada | Alteração |
|---|---|
| classes5.dex | remove HttpBridge/StationTransfer/DownloadService antigos, integra serviço atual e estado foreground; ordem SDL2 → station_frontend → main → turbo_carousel |
| classes6.dex | gate de SDL usa `StationLogin.ready` em vez de AuthSession antigo |
| classes8.dex | remove classes antigas de autenticação/catálogo |
| classes28.dex | módulo Station novo no lugar do slot do GameDownload antigo |
| libmain.so | 32 corpos de funções de serviços substituídos; 4409 endereços de símbolos preservados |
| libstation_frontend.so | ponte JNI e implementação de serviços do catálogo/download |
| libstation_archive.so | extração ZIP/RAR/7z baseada em libarchive |
| assets/turboretro/catalog.json | removido |
| avisos de licenças | adicionados os de libarchive/xz |

10.803 entradas foram preservadas por hash; manifesto e certificado iguais; sem novas classes duplicadas. Consulte [apk-report](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/apk-report.json), [dex-removal-manifest](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/dex-removal-manifest.json) e [native-link-manifest](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/versions/station-reconstruction-20261002/evidence/native-link-manifest.json).

Ainda existem strings e rotinas legadas em bibliotecas preservadas. O inventário estático de domínios não prova que estejam sendo chamadas nem que todas estejam inativas. A auditoria final precisa conferir caminhos de execução. Não apresentar esta entrega como “zero código antigo”.

### 3.2 Bibliotecas de referência

| Biblioteca | SHA256 |
|---|---|
| libmain.so integrada | `62ad07ba8e62e3227d488f9075eb15167c2e95b721c43e480e2535b4319467f6` |
| libstation_frontend.so | `437d0a0a48038b7419148606a176482a458b6a43824eb5a50c232dd7f1c64864` |
| libstation_archive.so | `9b6576b07b3e44dc48e5ec478cfb8db16852aa1aca0a7895cb567e7b57ff8c7a` |

`frontend-manifest.json` conserva `apkIntegrated=false` porque é o registro da fase de compilação da biblioteca. O empacotamento e a instalação posteriores são demonstrados por apk-report/closure-validation. Não apagar o registro histórico nem interpretá-lo sozinho como estado final.

## 4. Contrato HTTP completo usado pelo app

Fontes principais: `StationConfig.java`, `StationProtocol.java`, `StationApi.java` e `StationHttp.java`, dentro de `src/java/org/emulationstation/frontend/station/` na pasta publicada. [StationApi fixada no commit](https://github.com/luziellacerda/TurboElden/blob/629a55a8cf48722460007944cf0bb737e9f8fb75/versions/station-reconstruction-20261002/src/java/org/emulationstation/frontend/station/StationApi.java#L99).

### 4.1 Origem, transporte e chaves

- Única origem do transporte reconstruído: `https://app.lzgames.com.br`.
- Prefixo: `/v1/station/`. Lista permitida de nove rotas, sem URLs de conteúdo recebidas do servidor.
- `ENABLED=true`; produto e aplicação `TURBORAMA_STATION_ANDROID`; `clientVersion="1"`.
- TLS: confiança do Android, hostname e pin da SPKI do certificado folha. Pin: `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7`.
- Autoridade de assinatura pública fixa no StationConfig; keyId `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`.
- Sem redirects: rejeita 3xx ou cabeçalho Location. Cache HTTP desativado; o módulo não adiciona Cookie explicitamente. O código não configura um CookieHandler próprio, portanto a ausência global de cookies precisa de conferência do processo. `Accept-Encoding: identity`; outra codificação é rejeitada.
- Timeout de conexão 10 s; leitura 30 s. Sucesso esperado 200; download 206/Range não é implementado no transporte novo.
- Alterar TLS/chave de assinatura exige migração coordenada com o APK. Não desativar a verificação para encobrir erro de infraestrutura.

`/drawers`, Miami, Sambox, squareweb e links `?e=/?s=` não fazem parte deste transporte. O handoff antigo de StationTransfer e URL artificial `station.invalid` descreve outra implementação: **não é a implementação reconstruída auditada aqui**. O novo Java usa comandos e IDs diretos; o cache devolve caminho local de imagem.

### 4.2 Identidade de aparelho e assinaturas

Identidade em cada corpo aplicável: `schemaVersion=1`, `domain`, `productId`, `applicationId`, `deviceId`, `clientVersion`, `deviceManufacturer`, `deviceModel`, `androidSdk`.

- Alias Android Keystore: `turborama.station.device.v1`, RSA 2048, chave privada não exportável.
- deviceId: Base64URL sem padding do SHA256 da SPKI pública. Fabricante/modelo limitados a 100 caracteres. Não coleta MAC ou IMEI.
- Assinatura: RSA-PSS SHA256, MGF1 SHA256, salt32, trailer1.
- Envelope de resposta: `{keyId,payload,signature}`, payload/signature em Base64URL canônico. Interpreta o JSON do envelope e verifica a assinatura dos bytes antes de interpretar o JSON do payload.
- Envelope de prova do aparelho: `{payload,signature}`.
- Prefixo de domínio assinado: `TurboRamaStationAndroid/`; sufixo `/v1`.
- Respostas conferem schema/domínio/produto/aplicação/aparelho; quando vinculadas à sessão, licença e sessionId também.
- Código de ativação, nonce, Bearer e grantId: Base64URL canônico de 32 bytes, 43 caracteres sem padding. `STA-...` é licenseId, não é a senha de ativação.
- challengeId/sessionId: 64 hexadecimais minúsculos. TTL exigido: desafio60 s, sessão180 s, grant60 s.

### 4.3 Nove operações — entrada e saída

Os domínios abaixo omitem apenas o prefixo `TurboRamaStationAndroid/` e o sufixo `/v1`, comuns a todos.

| Método/rota | Entrada do app | Saída que o app valida |
|---|---|---|
| POST `/v1/station/activations/challenge` | Identidade `request-activation-challenge`, activationCode, devicePublicKey; sem Bearer | envelope `activation-challenge`, challengeId, nonce, expiresInSeconds=60 |
| POST `/v1/station/activations/complete` | envelope do aparelho; payload `activate`, identidade, código, chave pública, challengeId e nonce | envelope `activated`; desafio/nonce correspondentes e licenseId |
| POST `/v1/station/challenges` | identidade `request-session-challenge` + licenseId; sem Bearer | envelope `session-challenge`, licença/desafio/nonce, expiresInSeconds=60 |
| POST `/v1/station/sessions` | envelope do aparelho; payload `open-session`, identidade, licença/desafio/nonce | envelope `session`, licenseId, sessionId, accessToken, expiresInSeconds=180 |
| GET `/v1/station/me` | Authorization Bearer | envelope `profile`, contexto de sessão, displayName, profileVersion |
| GET `/v1/station/catalog` | Authorization Bearer | envelope `catalog`, contexto, revision, items[] |
| GET `/v1/station/covers/{coverId}` | Bearer e coverId do catálogo | imagem200, MIME e bytes válidos |
| POST `/v1/station/downloads/authorize` | Bearer + identidade `request-download` + itemId | envelope `download-grant`, contexto, itemId, itemRevision, grantId, artifact, expiresInSeconds=60 |
| GET `/v1/station/artifacts/{grantId}` | o mesmo Bearer que obteve o grant | 200 application/octet-stream, Content-Length exato, bytes e SHA256 assinados |

Autorização envia **itemId**, não nome, pasta, plataforma, URL, extensão ou revisão solicitada. O app recebe `itemRevision` no grant e compara com a revisão esperada do catálogo. Não inventar um campo request `expectedRevision` que não existe.

### 4.4 Formatos e limites do catálogo

Cada item contém `itemId`, `name`, `platform`, `revision`, `coverId`. A foto não vem como URL. Nome/plataforma: 1–120 caracteres sem controles; IDs: 8–64 caracteres de `[A-Za-z0-9_-]`; revisão positiva; itemId único.

- Corpo HTTP JSON normal de requisição/resposta, incluindo envelope quando aplicável, até8 KiB; envelope HTTP do catálogo até12 MiB, incluindo a representação Base64URL.
- Até4096 itens. Não há paginação neste cliente nem na biblioteca candidata do servidor comparada.
- Perfil: displayName1–80 caracteres e profileVersion positivo.
- Capas: até5 MiB, MIME/imagem coerentes, dimensões1–8192; validação por decode Android com inSampleSize em potência de dois e alvo de aproximadamente720 pixels; os bytes originais são mantidos no cache, não uma imagem persistida redimensionada.
- Catálogo de12.346 não pode ser publicado inteiro neste contrato. Planejar paginação/versionamento nos dois lados, com testes; não cortar silenciosamente.

### 4.5 Descritor obrigatório do artefato

`artifact` tem os seguintes campos obrigatórios consumidos (propriedades adicionais não são rejeitadas pelo parser): `fileName`, `sizeBytes`, `sha256`, `format`, `launchPath`, `expandedSizeBytes`, `fileCount`. O grant também precisa de `itemRevision`.

Formatos aceitos: `raw`, `zip`, `rar`, `7z`. SHA256 minúsculo; tamanho real deve corresponder. Limites: artefato1 TiB; expandido4 TiB;100.000 arquivos. São limites de validação, não garantia de espaço ou suporte a qualquer jogo.

Caminhos relativos estritos: sem `..`, barra invertida, dois-pontos, componente vazio ou caminho absoluto. Para raw: launchPath=fileName, expandedSizeBytes=sizeBytes, fileCount=1. Em arquivo compactado, o launchPath é o membro real que o emulador precisa abrir; não deduzir de `.rar`/nome comercial. CUE/M3U têm referências internas verificadas pelo instalador.

Grant incompleto é recusado. Segundo uso do grant/retomada de bytes não são uma estratégia de retry do app; nova tentativa exige nova autorização.

## 5. Login, sessão, nome do comprador e caches

### 5.1 Ordem e persistência

- Primeira ativação usa o código de uso único e a chave do aparelho. A licença verificada é gravada imediatamente após ativar, antes de abrir a sessão, para não perder a ativação se a conexão seguinte falhar.
- Próximas entradas usam licença salva e prova Keystore; o código não é persistido. Bearer permanece somente em memória.
- Login executa sessão → perfil → catálogo. O nome da saudação vem de `/me.displayName`; não é derivado do ID da licença.
- Sessão de180 s renova sob demanda quando restam15 s ou menos. Não há polling periódico de sessão ociosa.
- `Manter conectado` grava somente a preferência privada `station-login-ui/rememberAccess`, padrão true. Desmarcar exige toque em Entrar nas próximas aberturas sem sessão válida; conserva a licença. Não é logout, PIN local ou revogação de aparelho.
- Sessão já válida pula o login independentemente da preferência. `StationCoordinator.logout()` limpa estado em memória, não apaga automaticamente o arquivo de licença nem a chave do Keystore.
- `refresh()` atualiza o catálogo e mantém o displayName conhecido; não faz nova consulta de perfil. Mudança de nome durante sessão precisa de nova entrada/fluxo explícito para refletir.
- Tela de login limpa código após captura e desabilita salvamento/autofill desse campo; FLAG_SECURE. Não publicar capturas de credenciais.

Fontes: StationSessions:16–37; StationCoordinator:31–50,82–118; auth/StationLogin:21–26; auth/LoginActivity:113–139,274–284,339–350.

### 5.2 Arquivos persistentes

`no_backup` abaixo é a pasta privada retornada pelo Android, resolvida por `getNoBackupFilesDir().toRealPath()`, não um caminho escolhido pelo servidor.

| Dado | Local / política |
|---|---|
| Licença | `no_backup/station-license-id.txt` |
| Catálogo assinado | `no_backup/station-v2/catalog/<hash-do-dono>.json`; hash de licença+quebra de linha+deviceId |
| Perfil | `no_backup/station-v2/profiles/<hash-do-dono>.json` |
| Nome para cabeçalho existente | `no_backup/station-display-name.txt`; string visual, não autorização |
| Capas atuais | `no_backup/station-v2/covers/<coverId>-<revision-do-item>.img` |
| Capas anteriores elegíveis para migração | `no_backup/station-covers/revisions.tsv` e arquivos correspondentes |
| Recibos de instalações | `no_backup/station-v2/installs/<itemId>.json` |
| Preferência Manter conectado | SharedPreferences privadas `station-login-ui` |
| Chave privada | Android Keystore; não existe arquivo exportável neste pacote |

Não limpar dados/desinstalar para “forçar catálogo”: isso destrói identidade local e pode exigir reemissão/transferência de licença, além de impedir análise da causa.

### 5.3 Regras exatas do cache de catálogo

`StationCatalogStore` verifica assinatura e dono. Catálogo em cache pode pertencer à sessão anterior, mas só é lido com sessão atual válida da mesma licença/aparelho. Revisão menor que a armazenada é recusada. Não há bloqueio explícito de alteração de conteúdo com revisão igual; o servidor deve garantir revisão crescente quando o conteúdo muda.

`StationCoordinator` usa cache de catálogo somente diante de **503 STATION_CATALOG_NOT_READY**. Cache de nome somente diante de **503 STATION_PROFILE_NOT_READY**. Falha de rede genérica não cria modo offline; HTTP200 novo não é silenciosamente substituído por cache antigo. As respostas atuais registraram CATALOG_NETWORK, e não CATALOG_CACHE.

### 5.4 Capas: fluxo, persistência e defeito de retry

O renderer pede o item; Java resolve o `coverId` daquele item do catálogo autenticado. Não escolhe capa por nome, extensão ou URL. Reaproveita imagem em disco com ID/revisão correspondente após validação; arquivo válido não é baixado de novo a cada entrada. Cache anterior só é importado com correspondência explícita de ID/revisão.

Fila nativa: priorização de até64 itens, no máximo4 pendentes. Java:1 worker e fila32. Intervalo mínimo entre redes novas2100 ms. 404 e429 geram cooldown60 s;429 também adia a próxima consulta global. Leituras de cache exigem sessão válida. A validação de imagem protege contra resposta HTML/bytes inválidos.

**Defeito estático do cliente:** caminho vazio publicado por falha, cancelamento ou fila cheia marca `coverFailed=true` no nativo. A fila nativa pula itens com essa flag. Expirar os60 s do Java não reprograma a tentativa. Republicar o catálogo/retornar ao foreground pode resetar o estado; ficar na mesma tela pode manter a capa vazia após o servidor voltar. Isso prolonga uma falha; não explica a origem do404 que foi recebido.

Referências para correção: `StationFrontend.java:70–79`; `station_frontend.cpp:69–89`; `StationCoverStore.java:42–92`. Pedir revisão explícita da política de retry e separar falha temporária, cancelamento, fila cheia e inexistência confirmada.

## 6. Do catálogo ao botão Baixar e ao emulador

### 6.1 Mapeamento de plataformas

[Apêndice de mapas e integridade](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/docs/server/revisao-app-20261003/APPENDICE-MAPA-E-INTEGRIDADE.md) contém **todas** as entradas do `StationPlatforms.java`: valor exato recebido, nome exibido, pasta e linha. Inclui maiúsculas, espaços finais e pastas com hífen duplo. São dados históricos do frontend e não devem ser “normalizados” por aproximação durante a revisão.

Exemplos: `snes` → `Super Nintendo` → `super-nintendo`; `snesbr` → `Super Nintendo - BR` → `super-nintendo--br`; `megadrivebr` → `MegaDrive - BR` → `megadrive--br`. `Psp - BR` usa pasta `psp`, embora o nome do grupo seja separado.

O lookup é exato, sensível a maiúsculas. Não há aliases crus `psp`, `ps2`, `ps2br`, `pspbr`, `psvita`, `gamecube`, `model2`, `sufami` no código atual. Há outros rótulos dessas famílias, como `PSP`, `Playstation 2`, `GameCube`. **Conferir o valor realmente emitido antes de afirmar que há erro em produção.**

Uma plataforma desconhecida lança `UnsupportedPlatform` dentro do loop e aborta a publicação inteira, não apenas aquele item. Receber um catálogo ampliado pode expor esse defeito. A presença de um nome neste mapa não significa que a célula esteja visível no tema nem que o motor tenha sido validado. Sistemas ocultos e regras do carrossel preservado precisam ser conferidos separadamente.

### 6.2 Ponte JNI e ABI nativa

`StationFrontend.publishCurrent` transforma cada item em seis campos UTF8 separados por NUL:

`itemId | name | platform.label | platform.folder | coverId | installed.launchPath`

Não serializa URL. Publica somente após preparar todos os itens. Erro em recibo de um jogo é isolado e registrado; aquele jogo fica sem indicação de instalado, preservando seus arquivos. Erro de plataforma ainda aborta o conjunto.

`station_catalog_abi.hpp:8–36`: item0xe8 bytes; installed+0xa8; localPath+0xb0; coverPath+0xc8; coverFailed/Ready/Pending+0xe0/e1/e2. Catálogo pendente em+0xd0, depois aplicado ao catálogo real. Confira todas as32 substituições de serviços no apêndice e `native-link-manifest.json`; esses endereços valem somente para a base identificada.

`CatalogService::startDownload` no endereço0x1961ec delega a `StationCatalog_start`. `station_frontend.cpp:180–183` resolve o índice real de `catalog->items[index]`, obtém seu ID e chama Java `StationFrontend.start(itemId)`. Esta passagem precisa ser incluída na correlação do defeito, em vez de verificar só a URL configurada.

### 6.3 Sequência de download e reuso

1. `StationDownloads.start` exige itemId existente no catálogo autorizado; evita segundo job simultâneo do mesmo ID.
2. Worker único, fila de até4 jobs, chama `StationCoordinator.authorize`.
3. Coordenador obtém sessão; se mudou, recarrega catálogo; confirma item atual; chama POST authorize.
4. Confere `itemRevision` do grant contra catálogo. Mudança exige atualização, sem adivinhar arquivo.
5. Procura artefato antigo na pasta local mapeada **após obter grant válido**, por tamanho e SHA256 assinados.
6. Scanner limitado: profundidade6,4096 entradas,8 candidatos, orçamento de hashing limitado; busca acima de5 s exige reautorizar antes de baixar.
7. Encontrando artefato íntegro, reutiliza como entrada para instalação; não altera/apaga o arquivo antigo. Arquivos extraídos de ZIP anterior não são reconhecidos por semelhança de nome.
8. Sem artefato elegível, confere espaço (`sizeBytes + expandedSizeBytes +256 MiB`), cria staging exclusivo e consome GET de uso único.
9. `StationFiles` grava parcial, confere tamanho/SHA256 e sincroniza antes de movimentação atômica. Não publica arquivo incompleto como jogo.
10. Instalador valida descritor, formato real, arquivos expandidos e launchPath; cria nova geração e só então atualiza recibo privado.
11. Notifica o nativo com instalado/path; botão Jogar passa a usar o launcher preservado.

**Situação atual:** para no passo3 com404. Nenhuma transferência começou. Esse404 também impede o reuso de uma ROM antiga que estaria íntegra, pois o reuso depende de autorização/descritivo assinado. Não afirmar que todos os jogos antigos já aparecem como instalados nesta reconstrução.

### 6.4 Instalação, exclusão e preservação

Raiz de ROMs é entregue pelo aplicativo nativo e resolvida para caminho real. Não é escolhida pelo catálogo remoto.

- Staging: `<romsRoot>/.station-v2/staging/transfer-*`.
- Geração: `<romsRoot>/.station-v2/<folder>/<itemId>/install-*/content/<launchPath>`.
- Recibo: `no_backup/station-v2/installs/<itemId>.json`.
- Extrator recusa escape de diretório, symlink, caminhos duplicados/inválidos e limites excedidos. CUE/M3U precisam apontar para membros instalados.
- Cancelamento/falha não publica a nova geração como instalada. Rollback mantém a instalação anterior.
- `find()` confere recibo, caminhos e tamanhos; não recalcula hash de cada arquivo a cada abertura.
- Atualização bem-sucedida deixa gerações anteriores no disco; política de coleta é pendência real.
- Apagar usa recibo e marca transição `removing`; exclui somente arquivos conhecidos, preservando desconhecidos/saves. Não apaga toda pasta por nome.
- Exclusão sem recibo válido ou dos jogos antigos ainda não importados não equivale à exclusão do acervo anterior. Tratar esse caso explicitamente na homologação.

Fontes: StationFiles; StationExistingArtifact:10–35; StationInstaller:34–121; StationDownloads:19–76.

### 6.5 Launcher e limite da validação

`GuiStore::launchItem`, entrada nativa preservada0x232e00, continua encaminhando a `AndroidLauncher::launch`. A integração nova preenche `installed`, `localPath` e `fileName` após sucesso. A análise do binário mostra o consumo do caminho, mas **execução de um jogo sob `.station-v2` ainda não passou no aparelho**. Não considerar garantida a descoberta de BIOS, arquivos irmãos, saves ou diretórios especiais de todos os emuladores nesse novo caminho sem teste real.

Os emuladores/design/vídeos vêm da base; esta migração não atualiza PS2 ou outros motores. O serviço comercial deve preservar a seleção de motor já existente.

## 7. Loading, threads e consumo

- Login realiza IO fora da UI. Cancelamento de login ao sair da tela; nenhuma senha em estado salvo.
- Comandos de catálogo: worker1/fila8. Imagens: worker1/fila32. Instalação: worker1/fila4. Não são32 downloads simultâneos.
- `setForeground(false)` cancela pedidos de capa; retomada reconcilia/publica catálogo novamente. Trabalho de download é separado e acompanhado por foreground service quando ativo.
- DownloadService atualiza notificação aproximadamente1 s; wakelock só durante trabalho, limitado a6 h; sem polling ocioso/WiFi lock no módulo novo. Não equivale a medição de GPU dos vídeos nativos preservados.
- Loading de preparação mede itens e bytes UTF8 preparados para JNI. **Esses bytes não são bytes de rede baixados do servidor.** Não apresentar esse contador como progresso do download HTTP do catálogo.
- Progresso de instalação usa soma de tamanho do artefato e tamanho expandido. Etapas de sessão/autorização/verificação têm total indeterminado. Não inventar percentual contínuo0–100 de toda a autenticação.
- Timeouts e mensagens precisam revisão: `StationDownloads.message` trata InterruptedIOException como “Cancelado”; SocketTimeoutException também pertence a essa família. Um timeout pode ser rotulado como cancelamento sem ação do usuário.

O código preservado da interface/emuladores pode executar outras rotinas; reduzir consumo ou provar ausência de requisições legadas exige medição, não só ler as filas novas.

## 8. Achados do cliente que o servidor deve revisar explicitamente

Estes pontos são parte central do pedido. Não responder apenas “publiquei os arquivos no servidor”. Para cada ponto, devolver arquivo/linha, comportamento, impacto e prova ou reprodução.

| ID | Constatação ou risco | Classificação / revisão pedida |
|---|---|---|
| APP-01 | Falha de capa vira `coverFailed` até republicação | constatado no código; corrigir retry por causa/tempo sem loop agressivo; não causa o404 original |
| APP-02 | Worker de capa pode renovar sessão enquanto download guarda grant da anterior | risco estático concreto, não reproduzido; sessão nova revoga anterior no candidato servidor |
| APP-03 | Um platform desconhecido aborta toda publicação | constatado; confrontar mapa completo com histograma real, não supor slug |
| APP-04 |4096 itens e12 MiB, sem paginação | constatado nos contratos; impeditivo para12.346, não explica sozinho176SNES |
| APP-05 | Logs não correlacionam item/cover selecionado e enviado | limitação comprovada; instrumentação sanitizada precisa fechar rastreio |
| APP-06 | Pin TLS/autoridade únicos e clientVersion constante1 | contrato real; planejar rotação e identificação de build; não explica200/404 atuais |
| APP-07 | Revisão de catálogo regressiva é recusada; capa mantém cache por ID/revisão | garantia/limite; servidor deve preservarIDs e incrementar revisões coerentes |
| APP-08 | Launcher preservado não validado com novos caminhos | lacuna real; executar instalação→jogo→retorno em aparelho antes de estabilidade |
| APP-09 | Reuso de arquivos antigos exige autorização; gerações antigas acumulam | limite real; medir espaço e validar migração/exclusão sem apagar saves |
| APP-10 | Legado nativo ainda existe fisicamente | não alegar reconstrução integral; rastrear chamadas antigas antes de afirmar desativação total |
| APP-11 | Mensagem de404 atribui erro ao arquivo/catálogo do servidor | texto do cliente é conclusivo demais para evidência disponível; corrigir após rastreio ou torná-lo preciso |
| APP-12 | Timeout pode aparecer como “Cancelado” | ramo estático de InterruptedIOException; distinguir cancelamento explícito e timeout |
| APP-13 | Atualizar catálogo não atualiza perfil; checkbox não é revogação | semântica atual precisa corresponder ao produto; testes de nome/sessão/UI pendentes |
| APP-14 | `ready()` aceita live válida sem comparar identidade de objeto com authorized | revisar junto da concorrência de sessão; não afirmar autorização coerente só pela flag |

### 8.1 Concorrência de sessão — cenário exato a reproduzir

`StationSessions.get` sincroniza obtenção/renovação, mas o lock não cobre toda operação HTTP após devolver Session. `StationCoordinator.authorize` é sincronizado; `cover()` não usa o mesmo lock de ciclo completo. Downloads e capas têm executores separados.

No servidor comparado, `StationStore.cs:288–289` revoga sessões ACTIVE anteriores do mesmo aparelho ao abrir outra. Cenário: authorize obtém grant sob S1 → scanner local/espera → capa detecta proximidade de165 s e abre S2 → S1 revogada → artefato é solicitado com Bearer de S1. A exigência de mesmo Bearer do grant torna a corrida relevante.

Reproduzir em ambiente autorizado/isolado com clock controlado ou espera medida, incluindo scanner acima/abaixo de5 s. Medir resposta efetiva. Não culpar esse risco pelos404 atuais sem prova; a previsão usual é falha de sessão/grant, dependente da DLL real.

### 8.2 Texto de erro não é diagnóstico

`StationDownloads.java`, método `message`, exibe para404 STATION_ITEM_NOT_FOUND: “O servidor não disponibilizou o arquivo deste jogo. A equipe precisa corrigir o catálogo.” Essa frase vai além do que os registros atuais demonstram: um ID errado no cliente, divergência de roteamento ou índice também precisam ser descartados. A equipe deve examinar o item selecionado/requisitado e o servidor antes de atribuir a causa. A mensagem não pode ser usada como prova independente.

## 9. Código servidor comparado e o que os404 realmente significam

As referências abaixo são do commit **64912e1f2294b99967fc638d842f505cff7f629c**, pasta `src/TurboRamaSuiteOnlineServer/`. Elas descrevem o código candidato comparado; ainda falta provar que a DLL que respondeu ao telefone implementa exatamente essas regras.

- [StationService.cs](https://github.com/luziellacerda/Servidor-pix/blob/64912e1f2294b99967fc638d842f505cff7f629c/src/TurboRamaSuiteOnlineServer/StationService.cs#L196)
- [StationLibrary.cs](https://github.com/luziellacerda/Servidor-pix/blob/64912e1f2294b99967fc638d842f505cff7f629c/src/TurboRamaSuiteOnlineServer/StationLibrary.cs#L143)
- [StationStore.cs](https://github.com/luziellacerda/Servidor-pix/blob/64912e1f2294b99967fc638d842f505cff7f629c/src/TurboRamaSuiteOnlineServer/StationStore.cs#L288)
- [StationEndpoints.cs](https://github.com/luziellacerda/Servidor-pix/blob/64912e1f2294b99967fc638d842f505cff7f629c/src/TurboRamaSuiteOnlineServer/StationEndpoints.cs#L217)

### 9.1 Capa

StationService:196–208 devolve STATION_COVER_NOT_FOUND para ID inválido ou `ReadCover` sem resultado. StationLibrary:143–154 pode falhar por ID ausente no índice, arquivo inexistente, tamanho menor que1 byte ou maior que5 MiB, leitura com tamanho divergente, MIME/extensão não suportados ou assinatura binária incompatível.

Assim,404 não significa necessariamente “foto ausente no disco”. Conferir ID exato, índice em memória, caminho resolvido, acesso da conta de serviço, bytes, tamanho, extensão e MIME. Se leitura de arquivo lançar exceção, analisar o tratamento real correspondente; não inventar que toda falta de permissão vira404.

### 9.2 Autorizar

StationService:223–232 no candidato:

| Condição | Resposta |
|---|---|
| Biblioteca/chave de grants indisponível |503 STATION_DOWNLOAD_NOT_READY |
| itemId inválido ou ausente em ContainsItem |404 STATION_ITEM_NOT_FOUND |
| Item conhecido, artefato não resolvível |503 STATION_ARTIFACT_NOT_READY |
| Item e artefato resolvidos | grant assinado200 |

Logo, a mensagem do app não é a semântica exata do candidato: o404 indica consulta de identidade/índice, enquanto a indisponibilidade do artefato tem outro código. **Não transportar essa conclusão para a DLL antiga sem localizar seu fonte correspondente.**

### 9.3 Limitação por IP

StationEndpoints:217–227 usa30 requisições/minuto de capa por RemoteIpAddress+rota. Os2100 ms respeitam individualmente esse limite; clientes atrás de NAT/proxy podem compartilhá-lo. Conferir forwarded headers e IP observado, sem registrar publicamente IP de comprador.429 coletivo é hipótese de infraestrutura, não fato observado nesta rodada.

## 10. Estado do servidor relatado — não confundir candidato e produção

O retorno64912e1f declara explicitamente **não implantado na5192**. A atualização das referências remotas durante esta revisão continuou apontando a esse commit.

| Objeto | Evidência relatada pelo servidor | Lacuna |
|---|---|---|
| Serviço Station | `turborama-station-api.service`, PID2388 na coleta do servidor | PID é histórico; obter estado no momento da reprodução |
| DLL antiga efetiva | SHA256 `75c466c3f33d64d89229c70610f40b4bd781f5f8fece6f021259f7be8bcfa6d2` | relacionar à implementação exata das rotas |
| Código candidato | commit `45fe4dfa71c0b15a6a805791a33b3b89f9e141c3` | não equivale à DLL antiga publicada |
| DLL candidata | `faa015c47df0ace9cd225517da3fb103b8522b2b353a24a9d3c1ed9070f52630` | publicação ainda não comprovada |
| Catálogo candidato |1816 jogos/capas/descritores; raw798,zip1018 | conciliar IDs/revisão com índice efetivo; preservar outras plataformas |
| Distribuição candidata |snes644,snesbr191,megadrive887,megadrivebr94 | não é contagem de produção |
| Outras plataformas do app |99 itens em5 grupos | preservar no merge; não substituir tudo pelos1816 |
| Jogos excluídos do candidato |8 entradas XML Mega Drive sem ROM | exclusão precisa constar da conciliação |
| Três ZIPs preparados | cópias filtradas; origem preservada | hash servido pode diferir da origem; manter proveniência |
| Migrações | código novo exige028 e029 | ledger029 de produção não comprovado no retorno |
| /ready/station antigo200 | serviço/banco conforme critérios antigos | não demonstra arquivo de capa, ROM ou descritor completo |
| Conta de serviço | `turborama-suite` | não alcança estágio/volume privado segundo o retorno |
| HTTP isolado | licença/chaves sintéticas, catálogo de1818 incluindo2 sintéticos; amostras reais por4 grupos | não é HTTPS público nem download Android |

O retorno descreve a cadeia `app.lzgames.com.br:443 → Cloudflare/túnel → nginx →127.0.0.1:5192`. O Windows confirmou a configuração de origem do cliente, **não rastreou cada upstream efetivo**. Configuração protegida, índice real e permissões precisam ser conferidos pela equipe com acesso Linux autorizado. Nenhum acesso Linux efetivo foi usado nesta rodada Android.

Não usar `catalogo-candidato-cruzado-20261003.tsv` como catálogo do telefone: contém IDs candidatos, `catalogMatch=not_supplied`, não demonstra correspondência com IDs publicados. Não preencher itens ausentes a partir do XML antigo/CDN.

### 10.1 Correção das premissas antigas do retorno servidor

O retorno64912e1f ainda menciona APK43670211 e alias megadrivebr não incorporado. Isso ficou desatualizado após a instalação f5b35419:

- alias megadrivebr e outros aliases explícitos já estão no código instalado atual;
- a rede registrou996 itens novos no candidato atual; não é somente uma possibilidade de cache da observação antiga;
- teste1816 assinado passou no cliente com quatro grupos sintéticos; não valida todas as36 plataformas nem o renderer inteiro;
- model2/sufami e outras formas não mapeadas continuam sendo lacunas, conforme o apêndice;
- as provas200 de capas/downloads no aparelho continuam ausentes.

O servidor deve revisar o commit atual indicado, não concluir a partir dos fontes0840028/7d5df08 ou do APK43670211 mencionados anteriormente.

## 11. Roteiro de revisão solicitado ao servidor

### Etapa A — ler o cliente antes de propor correção

1. Abrir a nova branch e registrar seu commit completo, o commit de runtime629a55a e SHA256 f5b35419 do APK.
2. Ler esta documentação e o apêndice; verificar fontes e hashes, não apenas resumos.
3. Seguir LoginActivity → StationApi/Http → catálogo → Platform.resolve → JNI → índice selecionado → itemId → authorize → descriptor → installer → launcher.
4. Conferir que nenhuma mudança proposta depende de campo/rota ainda inexistente.
5. Responder a APP-01…APP-14, com evidência estática ou reprodução. Pode haver falhas em ambos os lados.

### Etapa B — identificar a origem efetiva de cada requisição

Para as nove rotas, registrar método, path, vhost, upstream, serviço/PID, ExecStart/drop-ins, SHA256 da DLL e fonte correspondente. Dados internos sensíveis ficam em registro privado; no retorno público do Git bastam identidade de release, hash e conclusão verificável.

Identificar o índice carregado **pelo mesmo processo**, revisão, hash, momento de leitura e histogramas. Um arquivo de código, uma branch atualizada ou um /ready200 não substitui isso. Verificar se catálogo, capa e autorização chegam à mesma instância/configuração de índice. Evitar afirmar divergência de proxy antes dessa leitura.

### Etapa C — rastrear um item do telefone ao disco

Usar uma conta de teste autorizada. Para o mesmo item, obter em evidência controlada:

1. Envelope de catálogo recebido pelo telefone, validado com autoridade pública; origem rede/cache, revisão e histograma dos `platform` brutos.
2. itemId e coverId do item antes e depois de JNI e seleção da célula. Publicar apenas pseudônimos/assinaturas de correlação, se necessário.
3. POST authorize efetivamente emitido: método/rota, ID correspondente, sessão pseudonimizada; sem token/código/deviceId no relatório.
4. ID recebido no serviço, resultado de ContainsItem, revisão do índice e serviço que respondeu.
5. CoverID recebido, entrada resolvida e resultado das validações de arquivo/MIME; caminho privado não vai ao Git.
6. Respostas correlacionadas e horários; se divergirem, localizar primeira etapa divergente e citar linha do cliente/servidor.

**Não repetir apenas “404 capa” e “404 download”.** O objetivo é localizar a primeira divergência verificável entre app, API e índice.

### Etapa D — completar conteúdo sem quebrar IDs

Confrontar índice efetivo com o candidato1816 e fonte de proveniência. Preservar IDs já publicados e itens de outras plataformas; definir revisão estritamente superior. Não mesclar por título aproximado. Registrar contagem antiga/nova por slug, colisões/ambiguidades, exclusões e IDs preservados.

Confirmar acesso a todos os artefatos/capas pela identidade real da API no destino final. As ferramentas de conciliação do servidor já existem, mas ainda precisam de entrada efetiva e permissões. Se faltar acesso, informar **qual operação read-only falhou e qual dado exato falta**, sem declarar implantação feita.

### Etapa E — demonstrar uma transferência completa

Para um item vindo do próprio catálogo autenticado:

- capa200 com MIME, tamanho e hash;
- authorize200 com itemId/revisão correspondentes e todos os campos artifact;
- artefato200 com mesmo Bearer do grant, Content-Length/tamanho/SHA256 iguais;
- segundo GET falha como definido; interrupção não vira instalado; nova tentativa requer novo grant;
- testes de sessão próxima de165 s, grant60 s e revogação sem trocar IDs;
- no aparelho: baixar → preparar → instalado → jogar → sair → plataformas sem login indevido;
- repetir com jogo raw e arquivo compactado; testar arquivo incompleto, cancelamento e espaço insuficiente sem perda de instalação anterior;
- manter foto em cache ao reabrir e testar recuperação da falha de capa sem limpar dados;
- conferir pelo menos cada slug novo antes de abrir o acervo ampliado.

Se o cliente tiver que mudar primeiro, devolver diff/proposta com arquivo/linha e teste que reproduz. Uma prova com curl/teste.NET não substitui validação Android.

### Etapa F — plano de mudança e retorno

Leitura e este pedido não executam deploy. Para eventual implantação posterior, devolver artefato/commit exatos, migrations aditivas necessárias, backup restaurável, serviço alvo, índice conciliado, permissões finais, teste antes/depois e retorno possível. Preservar PIX/Suite/Windows/site/WhatsApp/painel e outros serviços. Não contornar erro desativando assinaturas, pin, validação de licença ou liberando URLs públicas de ROM.

## 12. Build do app e evidências disponíveis

### 12.1 Receita e dependências

Executar somente na área de montagem E: indicada, com a base privada de hash fixo. Scripts estão em `versions/station-reconstruction-20261002` no Git; no canônico E ficam na raiz do projeto.

1. `prepare_test_dependency.py` → dependência JSON identificada em tools/json-dependency.json.
2. `run_tests.py` → verificações locais de protocolo/armazenamento/instalador; `build_module.py` → módulo Java.
3. `build_archive.py` → libarchive3.8.9 e xz5.8.3 com hashes oficiais fixados e licenças; `build_frontend.py` → ponte real arm64.
4. `prepare_dex_input.py` → insumos da base imutável; `link_native_services.py` → libmain com32 substituições; `build_app_dex.py` → quatro DEX preparados.
5. `package_apk.py` → verifica base, preserva entradas, integra DEX/libs, alinha16 KiB, assina e verifica manifesto/certificado/duplicatas.
6. Saída esperada nesta linha: `build/apk/TurboStations-Station-CANDIDATO-20261003.apk`; qualquer rebuild pode produzir hash novo e exige nova evidência.

Ferramentas: Python3, JDK17, AndroidSDK/API34, NDKr28c, CMake/Ninja, apktool3.0.3, build-tools35.0.0, LIEF. Java de Android8/API34; testes de núcleo usam toolchain indicada em run_tests.py. Nativo arm64 com target Android26, alinhamento16 KiB. Caminhos exatos e variáveis de substituição estão nos scripts e tools-manifest.

O empacotamento atual reutiliza a keystore original local, referida como debug.keystore no script. Isso preserva atualização do TESTE; não constitui uma política de assinatura/release comercial aprovada. Não trocar a assinatura do instalado em nome da revisão. Distribuição ao consumidor precisa de planejamento separado da identidade/certificado.

**Não é build autossuficiente somente com checkout Git:** depende da base privada, ferramentas e material de assinatura existente. Não usar fonte decompilado parcial como prova de reconstrução integral. Não executar scripts históricos de edição única como se fossem fases obrigatórias. Não inserir biblioteca das fixtures `build/device-frontend` no APK: ela é diferente da ponte de produção.

### 12.2 O que os testes existentes cobrem

| Evidência existente | Cobertura declarada | Limite |
|---|---|---|
| test-results.json |255 verificações locais:19 arquivos,68 protocolo/catálogo,36 caches/sessão,20 coordenador,61 instalador,51 fechamento | dados sintéticos e IO controlado, não servidor publicado |
| StationClosureTest |1816 itens assinados de4 grupos | parser/mapeamentoJava; não todas36 rotas nem renderização/disco de produção |
| Testes nativos |28 verificações da ponte no Android conforme registro | não jogo baixado do novo servidor |
| apk-report |10.803 entradas preservadas; manifesto/certificado iguais | preservação binária não prova funcionalidade ponta a ponta |
| closure-validation atual |instalação/hash + sessão/perfil/catalogo200 +404s | não há capa/download/instalação bem-sucedidos |
| catalog-count-audit/route-count-crosscheck |total996 e histograma nativo | sem payload bruto/histogramaHTTP atual sanitizado |

Esta revisão documental **não executou testes novos, não compilou, não instalou APK e não alterou o telefone/servidor**. As contagens acima pertencem às execuções já registradas. O apêndice recalcula somente hashes de arquivos publicados e confere sua correspondência com o manifesto de testes.

### 12.3 Inventário de fontes e integridade

[APPENDICE-MAPA-E-INTEGRIDADE.md](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/docs/server/revisao-app-20261003/APPENDICE-MAPA-E-INTEGRIDADE.md) lista arquivos, linhas, hashes, todas as plataformas e todas as entradas nativas substituídas. [source-review-manifest.json](https://github.com/luziellacerda/TurboElden/blob/db68b613cda008052afef8152400b9c595dfcffa/docs/server/revisao-app-20261003/source-review-manifest.json) permite comparação automatizada. As evidências são registros históricos; não editar resultados para aparentar execução atual.

Prioridade de leitura: novo handoff integral → apk-report/closure-validation atuais → fonte629a55a → manifestos de build → históricos. Os topos de AGENTS/README da branch recebem ponteiro para este documento para evitar que um hash antigo seja tomado pelo atual.

## 13. Formato obrigatório do retorno solicitado

Atualizar o retorno canônico já mantido no Servidor-pix, `docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md`, acrescentando seção datada **“Revisão integral do app Station de03/10/2026”** e referência explícita ao novo pedido. Pode anexar evidências sanitizadas, ligadas a esse único retorno.

O retorno precisa conter:

1. Commit completo do pedido lido, commit completo do app analisado, APK/hash usado e commit do servidor; não apenas nomes de branches.
2. Tabela das nove rotas: contrato do cliente, implementação/código do servidor, resposta real e serviço efetivo.
3. Resultado de APP-01…APP-14: confirmado/refutado/não reproduzido, arquivo/linha, evidência e correção proposta.
4. Primeira divergência encontrada na cadeia catálogo→seleção→itemId→authorize→índice. Se ainda não localizada, dizer exatamente qual elo não foi observado.
5. Histograma bruto autenticado, revisão/hash do índice real, distinção SNES/SNESBR, conciliação com1816 e preservação dos99 itens restantes, sem somas especulativas.
6. Capa e download: status, MIME, tamanhos/hashes esperados/recebidos, descritor completo sanitizado e correlação de sessão sem segredos.
7. Separação explícita entre fonte pronto, compilado, homologação isolada, publicado em produção e validado no aparelho.
8. Correções necessárias **no app** e **no servidor**, responsável e sequência; não declarar app correto só porque o contrato parece igual.
9. Impedimentos reais de acesso/dados, sem inventar caminho, token, endpoint ou retorno vazio para fechar tarefa.
10. Próximo passo operacional concreto e critério de aceite; alterações feitas com seus commits e retorno possível, se autorizadas e executadas posteriormente.

Não copiar senhas, activationCode, Bearer, envelopes com identificadores de aparelho, dados do comprador, DSN, chaves privadas ou URLs privadas ao retorno Git. Identidades sintéticas servem aos exemplos. Chaves públicas/hashes de artefatos e estatísticas podem ser usados para verificação.

## 14. Condições para encerrar a integração

Continuar marcando a entrega como candidata até que haja prova de: origem/serviço/índice corretos; catálogo conciliado e mapeado; capa200 e cache persistente recuperável; grant e transferência íntegra; instalação/abertura/retorno reais; renovação/revogação coerentes; preservação dos jogos/saves e identidade; problemas APP identificados resolvidos ou com limitação aceita explicitamente.

Uma resposta futura pode provar que parte do problema está no app, no índice, no proxy ou em mais de um componente. Este handoff fornece o código e as lacunas para localizar a causa, sem antecipar a conclusão. O objetivo imediato é a revisão técnica completa pedida pelo mantenedor, não promover o candidato a estável.
