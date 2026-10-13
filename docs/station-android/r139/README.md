## Retorno atual do servidor — 13/10/2026

Os oito motores RetroArch R139 foram recebidos/aplicados e suas capabilities assinadas foram conferidas na revisão46. Consulte [server-return.json](server-return.json), [server-signed-bindings.json](server-signed-bindings.json), [server-matrix.json](server-matrix.json) e o [mesmo handoff consolidado](../HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md). O diagnóstico USB confirma runtime/cartucho do moto g52 e captura SIGSEGV/mutex destruído; a falha inicial de Top Gear ainda exige captura atual. Seis bibliotecas independentes foram obtidas/conferidas, quatro cadastros/78 modos preparados, mas a autenticação Linux foi cancelada: publicação adicional pendente. Recebimento/cadastro não comprovam gameplay. O envio e os recibos históricos abaixo são preservados.

## Histórico da produção

# R139 recursos nativos e cadastro dos motores

Entrega APP → SERVIDOR de 13/10/2026. A R139 está compilada, assinada e instalada no moto g52, com hash integral, UID, dados e primeira instalação preservados; frontend observado. O Edge 30 permanece R138 e o A56 R135 nas últimas conferências. O runtime corrige falhas de alocação/início e a propriedade da chave de dados por thread usada pelo JNI. O cadastro dos oito motores R139 e o recebimento do seu kit privado permanecem pendentes. O [handoff canônico](../HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md) continua sendo o ponto único de repasse.

## Identidades do pacote

| Artefato | Identidade conferida |
| --- | --- |
| Fonte produtiva | `42da152e54bbbb4a6d569ca26fe90835503bcccb` |
| `frontend/drivers/platform_unix.c` | `24420118e5ce94376b077051084a84d4c52f152d00b9e2330333896bced798fb` |
| APK R139 | `19acdeceef86bc66c2475300cbd142da783850c02f5a9e2068d8652ef8523164` / 2.714.991.631 bytes |
| Runtime `libstation_retroarch.so` | `ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378` / 10.673.192 bytes |
| APK base R138 | `9183462e4d45cfcf2d2599d9e682b81890fee7b094505bf3073ab8183dd1dd10` |
| Runtime base R138 | `463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f` |
| Certificado original | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` / v2 e v3 / alinhamento 16 KiB |

O [recibo do pacote](evidence/package-final.json) e a [verificação independente de todas as entradas](evidence/independent-package-summary.json) conferem duas substituições: `lib/arm64-v8a/libstation_retroarch.so` e `assets/station-online/engines.json`; 13.858 outras entradas e todas as 40 partições DEX estão preservadas. A [instalação no moto g52](evidence/instalacao-moto-g52.json) terminou em `2026-10-13T12:57:48.425144+00:00`, preservando a instalação/dados e abrindo o frontend. A [compilação nativa](evidence/native-build-candidate.json) identifica somente um arquivo produtivo alterado entre 11.149 fontes. O [freeze de fontes](evidence/source-freeze.json) registra o momento anterior à compilação, por isso seus campos históricos `nativeCompiled`/`apkCreated` permanecem falsos; a compilação e a assinatura são comprovadas pelos recibos posteriores.

## Correção e desempenho

Falhas de mutex, condição, estado salvo, pipe ou criação de worker agora encerram o início sem esperar uma thread inexistente e liberam os recursos já adquiridos. A falha de criação da chave JNI impede o worker de começar; uma tentativa posterior pode repetir essa preparação. O handshake normal e a limpeza idempotente R138 continuam iguais.

Uma única chave de dados por thread fica vinculada à VM do processo. `GetEnv` preserva as threads já anexadas por Java; somente uma anexação nova do helper recebe obrigação de detach. O destructor usa `JavaVM*` estável, mesmo após limpar `g_android`, sem usar Activity liberada. O getter continua exigindo contexto de app ativo. Seu mutex próprio é liberado antes das chamadas à VM, preservando a ordem dos locks da Activity e do app. Essa chave TLS é armazenamento local por thread, não uma alteração do TLS/HTTPS do relay.

Os [24 cenários no host](evidence/native-resource-tests.json) incluem falhas de recursos/chave, quatro estados salvos, criação concorrente, 100 lançamentos e propriedade JNI. Na base, 100 lançamentos criavam 100 chaves; na R139, uma. [O probe no moto g52](evidence/android-jni-resource-probe.json) executou os três helpers produtivos com JavaVM e destructors pthread reais: uma chave em 100 inicializações, threads Java preservadas e oito threads nativas desanexadas na própria thread após remover o contexto de app. O probe usa uma biblioteca isolada, distinta do runtime completo; não inicia a Activity do produto, renderer, partida ou pressão real de memória.

A R139 conserva runloop, áudio/vídeo, 30 fps dos menus, fila/cache de capas, timers, sockets, READY/ACK, geração/epoch, recuperação, opções, controles e capacidade nativa de cinco posições. Os cores também ficam exatos. Os waits normais continuam dependendo da conclusão do core/driver; esta revisão não prova recuperação de todo travamento de driver nem redução de CPU/temperatura. O [escopo de fontes](evidence/source-scope.json) e [oito recusas de caminhos inseguros](evidence/recipe-path-guards-sanitized.json) delimitam a reprodução.

## Motores R139 e preservação dos cadastros atuais

O [registry do APK](engines.json) renova nove descritores por causa do novo runtime. O [manifesto administrativo candidato](incoming-engine-manifest-candidate.json) contém as oito engines prontas para v3, mantendo todos os campos de core, opções, portas, capacidades e política da entrega R138. Geolith continua inativo.

| Plataforma | Engine R139 | Estado desta entrega |
| --- | --- | --- |
| snes | `bsnes-mercury-performance-79d7f9de-mt1-mp5-hle-ae72f1371e8e` | Cadastro v3 pendente |
| megadrive | `clownmdemu-d43c2708-mp5-ae72f1371e8e` | Cadastro v3 pendente |
| neogeo / Geolith inativo | `geolith-19402493-mp5-ae72f1371e8e` | Inativo |
| neogeocd | `neocd-b1e04c73-aslr1-v3-ae72f1371e8e` | Cadastro v3 pendente |
| neogeo | `fbneo-95153da1-neogeo-v3-ae72f1371e8e` | Cadastro v3 pendente |
| fbneo | `fbneo-95153da1-fbneo-v3-ae72f1371e8e` | Cadastro v3 pendente |
| cps1 | `fbneo-95153da1-cps1-v3-ae72f1371e8e` | Cadastro v3 pendente |
| cps2 | `fbneo-95153da1-cps2-v3-ae72f1371e8e` | Cadastro v3 pendente |
| cps3 | `fbneo-95153da1-cps3-v3-ae72f1371e8e` | Cadastro v3 pendente |

PSX `pcsx-rearmed-ff81ed17-player-v3-c26d8836df25`, N64 `mupen64plus-ae-33bf7021-n64-v3-2f248cfcdc9e`, GameCube `dolphin-5102a033-gamecube-v3-2e0b36222097` e Wii `dolphin-5102a033-wii-v3-2e0b36222097` conservam exatamente os quatro owners R137. O kit de seis bibliotecas e o ledger desses owners permanecem pendentes conforme o [retorno R138](../r138/server-return.json); a R139 não renova nem resolve esse cadastro independente. PS3/PS4 continuam offline.

O estado confirmado do servidor é o retorno já aplicado R138 e a atualização NES do Git `08e0031`: catálogo 44, 4.690 IDs/4.435 visíveis/255 aliases; 8.267 perfis, 8.254 aprovados e 3.412 modos. Usar os arquivos persistidos atuais como baseline ao preparar uma aplicação nova, preservando as engines/perfis/modos R138 e anteriores. Contagens documentadas são a referência desse retorno, não uma nova leitura produtiva realizada por este repasse.

## Recebimento e aplicação administrativa

1. Receber o kit privado correspondente ao APK final acima e conferir o runtime `ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378` e os quatro cores compartilhados exatos do manifesto. O kit R138 antigo contém runtime `463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f` e não comprova o binário novo. O Git entrega fontes, metadados e recibos; o recebimento privado R139 precisa de seu próprio SHA e recibo.
2. Usar o operador vigente `online-plataformas-20261009/library-tools/cadastrar-motor-online-station.py`, com os dados atuais `autoOnlineProfiles.engineManifest`, `.registry` e `.preparedModes`. Executar `prepare` contra o baseline fresco e conferir o delta; a aplicação pelo fluxo administrativo existente acrescenta as oito identidades novas. Não substituir o registro v3 por registry v2 nem reaplicar workflows históricos. Esta documentação não aplica dados nem reinicia o servidor.
3. Preservar os registros R138 byte exatos. Vincular novos `profileId`/engine IDs às mesmas edições e identidades de conteúdo já aprovadas, mantendo controladores, opções, instruções e modos. Recusas continuam recusas; `metadata.players` não autoriza uma sala. A quantidade de perfis novos deve vir do recibo real do operador, sem copiar automaticamente a contagem histórica de 3.091.
4. Para SNES, conservar HLE R138 com `bsnes_violate_accuracy=enabled`, `bsnes_chip_hle=HLE`, SuperFX 100% e os hashes canônicos: padrão `[1,1]` → `8a79b81e1797bedf4c00058e004af7bbe7a00014d7b6041ef2b2a0c0bd39c793`; multitap `[1,257,1,1,1]` → `88568b97800afa017e3c75684effe23f3aad8e93f5b2ac947a0638a4268bd8f3`. Super Mario Kart continua `[2]`; Top Gear 3000 Corrida VS `[2,3,4]` e Campeonato em dupla `[2]`, por edição comum/PT-BR, com `requireExplicitEngineModes=true`. Cinco depende de outro modo que efetivamente admita cinco.
5. Devolver recibo administrativo e resposta assinada fresca de `POST /v1/station/online/multiplayer/command`/`capabilities`, com engine/core/runtime/profile/controlador/modo/contagens exatos. PONG não substitui READY. Depois do mesmo APK/identidade registrado em todos os participantes, validar início, controles, queda/retorno de sinal e saída humana de host/convidado sem crash. Instalação, probe JNI e aprovação administrativa são evidências distintas.

O hot reload v3 existente continua sendo o mecanismo de cadastro; este delta não exige nova DLL, alteração de endpoint ou reinício por causa do APK. Licenças, conteúdo, capas, IDs antigos, perfis/recusas, serviços, scanner e segurança existentes devem permanecer preservados.

## Catálogo NES e escopo desta correção

O servidor conserva o NES publicado em `08e0031`. A R139 corrige recursos e mantém os DEX/JNI de catálogo da R138: máximo de 4.096 itens e consulta anterior, portanto ainda recebe os 3.685 itens anteriores e 411 NES quando o servidor usa esse limite compatível. O overlay `maximumItems=20000` continua separado e não integra esta compilação. Sua futura composição deve aplicar somente o patch de capacidade à fonte corrente com exports R132; não substituir o frontend por um snapshot R124 antigo. NES online continua dependendo de motor próprio.

[STATUS](STATUS.json) separa assinatura, instalação, kit recebido, cadastro e gameplay. Assinatura, conferência independente e instalação no g52 estão comprovadas; saída física/perda de sinal/gameplay ainda dependem dos testes próprios. O recebimento e cadastro R139 continuam pendentes até retorno confirmado do servidor.

## Tentativa de criar sala após a instalação

O usuário relatou não conseguir criar uma sala. Na captura do moto g52 entre 09:58:45 e 09:58:49 de 13/10, quatro chamadas terminaram em `StationOnlineGame.Unavailable` durante a seleção de perfis/modos, antes da preparação de ROM/core e do comando efetivo de criação. O Java permanece idêntico à R138. Seu filtro exige perfil aprovado e correspondência exata de jogo e engine; perfis R138 não vinculam as engines RetroArch R139. Isso é compatível com cadastro R139 pendente, mas o título, a mensagem visível e o payload fresco de capabilities ainda não foram confirmados nesta captura. Ver [observação sanitizada](evidence/room-create-observation.json). `REQUEST_FAILED` com HTTP200 é uma categoria de fallback do diagnóstico e não demonstra erro de rede. Nenhum ANR do app foi observado no trecho posterior à instalação.

## Kit privado final preparado

`Kit-Servidor-TurboStations-R139.zip`: 106499932 bytes; SHA256 `1449d2f93d3aa345809529a93bcdfb143381383574c79f507181a1b8c48f8fdd`. Cinco bibliotecas, oito engines, fontes completas do runtime, overlays R138/R139 congelados, helpers históricos e proveniência de compilação. O manifesto interno confere todas as 60 entradas. [Recibo externo do ZIP](evidence/private-kit-prepared.json). A documentação dentro do kit é o snapshot usado na criação; este recibo externo posterior registra o SHA do ZIP final. Preparação não confirma recebimento, aplicação administrativa ou gameplay.

## Tela confirmada: Battletoads

Uma segunda captura USB mostrou **Battletoads in Battlemaniacs (USA), SNES**, com “Não há modo aprovado para esta edição e o motor instalado. Atualize o aplicativo e o catálogo nos participantes.” O retorno assinado documentado R138 já aprova essa edição em modos de dois jogadores, mas vinculados ao engine `bsnes-mercury-performance-79d7f9de-mt1-mp5-hle-463bdb0b409e`. O APK R139 usa `bsnes-mercury-performance-79d7f9de-mt1-mp5-hle-ae72f1371e8e`; o filtro conserva identidade exata e não trata os perfis antigos como autorização do motor novo. O operador precisa conservar os modos/edições e registrar os vínculos R139 reais. A captura confirma título/mensagem, sem fornecer o payload fresco de capabilities. A tela bruta e o nome da conta permanecem privados; [o recibo sanitizado](evidence/room-create-observation.json) registra a evidência e os perfis anteriores.

## Edge 30 atualizado em 13/10/2026

R139 instalada também no Edge 30, SHA integral `19acdeceef86bc66c2475300cbd142da783850c02f5a9e2068d8652ef8523164`, dados/UID/primeira instalação preservados. Frontend oficial observado: true. [Recibo](evidence/instalacao-edge30.json). Moto g52 e Edge 30 estão na mesma R139; Samsung A56 última R135 conferida. Apenas instalação e inventário foram atualizados; fonte executável42da e APK permanecem os mesmos. Este recibo não valida uma partida ou cadastro do servidor.
