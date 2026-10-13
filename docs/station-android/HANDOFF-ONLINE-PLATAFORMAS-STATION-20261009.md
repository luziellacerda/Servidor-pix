## Downloads diretos protegidos — 13/10/2026

Servidor `downloads-station.lzgames.com.br` somente DNS e HTTPS aplicado; atualizadores de IPv4 e certificado ativos. APK direto sobre R139 assinado com a chave original, SHA256 `e69616ee6377920f808f3b5a267f5592ffff9ae70c2bbe2063c6902cd903c02a`, v2/v3/16 KiB e 13.860 entradas finais conferidas. Quatro IDs renovados e 78 vínculos de modos já aprovados cadastrados; 31 motores, 11.448 perfis e 6.587 modos anteriores preservados. Respostas assinadas locais/públicas e recusas de admissão passaram. Totais: 35 motores, 11.526 perfis e 6.665 modos, catálogo46; API/DLL/configurações/licenças preservadas, sem restart. Instalador USB atualizado para esse APK. O operador testou downloads e relatou melhora maior que duas vezes, chegando perto de 10 MB/s. Resultado prático informado na conversa, sem comparação controlada gravada ou SHA instalado adquirido por ADB. [Escopo e evidências desta configuração](../../ops/station-direct-download-20261013/README.md).

## Inventário atualizado em 13/10: Edge30 na R139

R139 instalada também no Edge30, SHA integral/UID/dados/primeira instalação preservados; frontend oficial observado: true. Moto g52 e Edge30 estão na mesma R139; A56 última R135. [Recibo Edge30](r139/evidence/instalacao-edge30.json). APK/fonte e contratos permanecem os da entrega abaixo; esta atualização documental não confirma cadastro ou partida.

## R139 assinada com correção de recursos nativos

Entrega APP → SERVIDOR de 13/10/2026. **APK R139 compilado, assinado e instalado no moto g52; kit privado e cadastro R139 ainda não recebidos/confirmados pelo servidor.** Fonte produtiva `42da152e54bbbb4a6d569ca26fe90835503bcccb`, arquivo nativo `24420118e5ce94376b077051084a84d4c52f152d00b9e2330333896bced798fb`, runtime `ae72f1371e8e5a2d35f9250793ce2870f3feab73a3e17c83c9fbd48a9d15f378` / 10.673.192 bytes, APK `19acdeceef86bc66c2475300cbd142da783850c02f5a9e2068d8652ef8523164` / 2.714.991.631 bytes. Certificado original v2/v3 e alinhamento 16 KiB. Hash integral instalado, UID/dados/primeira instalação preservados e frontend observado; Edge 30 última R138, A56 última R135. [Estado e procedimento](r139/README.md) · [STATUS](r139/STATUS.json) · [Recibo de instalação](r139/evidence/instalacao-moto-g52.json).

O pacote altera somente `libstation_retroarch.so` e `engines.json`, preservando 13.858 entradas e todas as 40 partições DEX. Corrige falhas de alocação/pipe/worker e a propriedade JNI de uma única chave por processo; preserva a saída/cleanup R138 e toda lógica de quadros, core/opções, cache, 30 fps, timers, READY/ACK, recuperação e controles. A [qualificação no host](r139/evidence/native-resource-tests.json) passou 24 cenários. O [probe Android no moto g52](r139/evidence/android-jni-resource-probe.json) validou os três helpers JNI com VM/pthread reais; não executou a NativeActivity ou uma partida do produto nem mediu ganho de desempenho.

O [manifesto administrativo candidato](r139/incoming-engine-manifest-candidate.json) renova **oito engines v3** para o runtime novo; o registry do app renova nove descritores com Geolith ainda inativo. Preservar os oito cadastros R138 já aplicados e todos os dados atuais do servidor `08e0031`. Os quatro owners PSX/N64/GameCube/Wii R137 ficam exatos e continuam aguardando seu kit independente de seis bibliotecas/ledger. SNES conserva HLE e os mesmos controladores/modos por edição: Mario Kart dois; Top Gear VS dois/três/quatro, Campeonato em dupla dois. Nenhum registro novo ou homologação de gameplay é inferido dos hashes.

Receber os bytes R139 exatos e preparar a adição pelo operador v3 vigente contra o baseline fresco; publicar somente os dados conferidos pelo fluxo administrativo existente e devolver recibo com vínculos assinados novos. Não substituir perfis R138, aprovar recusas, relaxar identidade/READY/ACK/geração ou reiniciar serviços por ler este repasse. O runtime antigo do kit R138 não representa a R139.

O catálogo NES/revisão44 publicado abaixo continua preservado. **Esta compilação de correção não inclui o overlay de capacidade NES:** mantém o limite/consulta R138 de 4.096 itens e os 411 NES recebidos no modo compatível. O ajuste de catálogo e sua conferência de preservação são uma composição separada; não copiar fontes antigas sobre a R139. Saída física, pressão real de memória e partidas após cadastro ainda precisam de seus recibos próprios.

Kit R139 preparado: **106.499.932 bytes**, SHA256 `1449d2f93d3aa345809529a93bcdfb143381383574c79f507181a1b8c48f8fdd`; [recibo](r139/evidence/private-kit-prepared.json), cinco bibliotecas/oito motores. Recebimento privado e registro v3 ainda dependem de retorno real. A tentativa observada no g52 parou na seleção de perfis/modos, antes do motor; segunda captura confirmou Battletoads in Battlemaniacs (USA), SNES, e ausência de modo aprovado para o motor instalado; capabilities fresco ainda pendente ([observação](r139/evidence/room-create-observation.json)).

## Histórico preservado

## NES e capas novas publicadas — 12/10/2026

**Estado atual: catálogo44 / 4.690 IDs / 4.435 visíveis / 255 aliases.** NES foi movido de `snes/nes` para a raiz `nes`: 750 arquivos, 748 NES + um FDS + um UNIF, bytes preservados. O arquivo de Game Boy recebido no conjunto foi conservado e excluído do catálogo NES. [Recibo consolidado](online-plataformas-20261009/nes-publicado-20261012.json) · [Todos os vínculos NES/capa/Git](online-plataformas-20261009/nes-capas-20261012.json) · [Catálogo integral](online-plataformas-20261009/catalogo-cruzado-completo.json).

### Capas e publicação automática

**469 capas novas de `capas-em-producao/NES/Recriadas`** foram comprimidas e colocadas em `nes/media/revista`, com associação por ROM/título. Corrigida a primeira seleção, que usara a pasta de imagens originais do Git. 280 jogos sem correspondência segura nas artes do repositório conservam a capa frontal recebida; Dancing Blocks tem uma capa personalizada criada no modelo Turborama NES. Todos os750 têm capa JPEG RGB480×720/q90. O conjunto de origens escolhido tem1.216GB e foi reduzido a95.254MB. Gitmain5c6cc32c06e6944b9258df36ffdb846baa368a7f.

A troca foi publicada **pelo importador automático existente**: revisão44, `updated:469`, `added:0`, 4.435visíveis, seguida de varreduras `changed:false`. A pasta de imagens/seed pertence ao operador e permite esse fluxo; autenticação manual adicional foi dispensada. Cache novo nos469 itens: revisão44, coverId preservado. Colocar uma ROM suportada em `nes` e a capa com o mesmo nome-base em `nes/media/revista` permite descoberta automática, sem programar jogo individual. Sinopse vem do XML/seed; cadastro complementar atende dados ausentes.

**Nintendo64 conferido novamente:** 157 jogos/capas, todas as revistas e corpos do servidor coincidem com as imagens compiladas do Git atual;157 respostas autenticadas locais e16 amostras HTTPS confirmadas na revisão43. Mantida revisão42 desses itens e cache por coverId+item.revision. As imagens N64 já são as personalizadas Turborama; não houve nova arte diferente no Git. A correção44 altera somente o NES.

### Leitura completa pelo app

1. Usar **`GET /v1/station/catalog?metadata=1&maximumItems=20000`**, incluindo a query na prova por pedido. Validar envelope/assinatura/domínio/produto/licença/aparelho/sessão. Payload: `totalItems:4435`, `hasMore:false`.
2. Sem `maximumItems`, a resposta é limitada a4.096 para preservar os aparelhos atuais: todos os3.685 anteriores +411NES. **R138 precisa do ajuste Java e JNI para mostrar os750NES.** Overlay/patch e recibo de compilação em `integration/station-nes-catalogo-20261012` no Git do app. Limites de parser/evidência/prefetch e os dois limites nativos de catálogo/preparação foram conciliados; ABI e agendamento atual das capas preservados. Java8/API34, D8/API26, parser4435/750, limite20000 e ELFarm64/API26/16KiB passaram. Novo APK completo/assinatura/instalação são a próxima etapa do PC de produção.
3. `platform:"nes"` já mapeia Nintendinho. Usar os IDs, revisão, pasta e metadados do payload. Capas: `GET /v1/station/covers/{coverId}`, cache `coverId+item.revision`. Downloads: `POST /v1/station/downloads/authorize`, depois GET do grant em `/v1/station/artifacts/{grantId}` uma vez, preservando o descritor `raw`/launchPath. Licença/saves/dados podem ser preservados na atualização.

### Prova e preservação

A revisão43 foi autenticada/assinada localmente e pelo HTTPS:4435campos de catálogo,750capas locais,16amostras públicas e downloads completos dos três formatos, com recusa de replay e de limites inválidos. A correção44 foi confirmada pelos750JPEGs de entrada conferidos e pelo scanner real:469atualizados/zeroadicionados e estabilização; os corpos corrigidos não foram submetidos a uma segunda prova autenticada. Catálogo cruzado documenta essa distinção. Sem alegar teste visual/gameplay Android.

API ativa DLL25c49dd9327f14c0dbbd0d605b0f33e51398682bb20a6e7cb3d1e3ef002ab2c7/PID2637019/NRestarts0. Compilada da fonte efetiva7eb0e009 mais somente os três arquivos de capacidade/consulta; diagnóstico R134 preparado não foi promovido. Um reinício somente do Station na publicação inicial, com zero salas/conexões; troca de capas sem reinício. 3.940IDs antigos,8.267perfis/8.254aprovados, licenças/esquema/configs/outros serviços preservados; túnel/proxy/firewall intactos. Testes:44catálogoC#, seis descobertaPython, quatro identidade, oito parserJava. Backup anterior publicado nos doisGits: backup/station-antes-nes-20261012/tag equivalente, servera338153/app9d9700b.

NES está publicado para catálogo/capas/downloads; motor online próprio continua dependente da integração do APK. Os oito motores R138 aplicados e as pendências dos quatro independentes abaixo permanecem. Manutenção horária continua e prioriza as versões personalizadas/recriadas fornecidas; não reaplicar operadores históricos.

## Histórico preservado

## R138 aplicada e capas publicadas — 12/10/2026

**Aplicação administrativa concluída para o kit R138 recebido.** Oito motores RetroArch novos foram registrados, com 3.091 perfis/modos novos; os 5.176 perfis e 321 modos anteriores foram preservados. Total: 8.267 perfis, 8.254 aprovados e 3.412 modos persistidos. API continua PID2136303/DLL2c0d03447696, sem reinício ou nova DLL. [Matriz única e recibo real](r138/server-return.json) · [Respostas assinadas local/pública](r138/server-signed-bindings.json) · [Catálogo completo atualizado](online-plataformas-20261009/catalogo-cruzado-completo.json).

| Plataforma/owner recebido | Perfis R138 acrescentados | Registro e resposta assinada |
|---|---:|---|
| SNES/BR | 850 | Aplicado; HLE e controles canônicos conferidos |
| Mega Drive/BR | 978 | Aplicado; controles/opções anteriores preservados |
| Neo Geo CD | 50 | Aplicado; firmware de novos conteúdos continua pendência separada |
| Neo Geo/FBNeo | 189 | Aplicado |
| FBNeo | 913 | Aplicado |
| CPS1 | 36 | Aplicado |
| CPS2 | 64 | Aplicado |
| CPS3 | 11 | Aplicado |

São 3.085 novas aprovações e seis recusas preservadas; novo cadastro não transforma recusa em aprovação. SNES usa `requireExplicitEngineModes=true`: copiados somente os vínculos já aprovados de conteúdo/core/controle, sem aprovar jogo desconhecido por `metadata.players`. Novos IDs de perfil são específicos da engine, mantendo os hashes LLE antigos. Geolith permanece inativo; PS3/PS4 offline.

**Super Mario Kart:** dois jogadores, perfil HLE `8a79b81e1797bedf4c00058e004af7bbe7a00014d7b6041ef2b2a0c0bd39c793`. **Top Gear 3000, edição comum e PT-BR:** Corrida VS `[2,3,4]`, perfil multitap HLE `88568b97800afa017e3c75684effe23f3aad8e93f5b2ac947a0638a4268bd8f3`; Campeonato em dupla `[2]`, perfil padrão acima. As cinco combinações constam com itemId/content/core/runtime/profile exatos no recibo. Os dois participantes precisam da mesma R138; três/quatro precisam de todos na mesma composição. READY, ACK, epoch/geração, replay e autorização foram preservados. PONG não confirma carga do core.

**Quatro motores independentes do APK atual continuam sem cadastro correspondente:** PSX `pcsx-rearmed-ff81ed17-player-v3-c26d8836df25`; N64 `mupen64plus-ae-33bf7021-n64-v3-2f248cfcdc9e`; GameCube/Wii `dolphin-5102a033-…-v3-2e0b36222097`. O ZIP privado R138 entregue tem somente as cinco bibliotecas RetroArch; não contém as seis bibliotecas independentes R137. Seus bytes e ledger qualificado são necessários para registrar esses quatro IDs. Cadastro/política antiga permanece preservado, mas não prova disponibilidade desses IDs novos. Não misturar identidade anterior nem inventar hashes ou aprovação HLE PSX.

### Capas e leitura pelo app

**Catálogo publicado: revisão42 / 3.940 IDs / 3.685 visíveis / 255 aliases.** Git `Capas-TurboRama`, main `5c6cc32c06e6944b9258df36ffdb846baa368a7f`, SNES `9f85e9df1ec0c82d77156ed072b997855f719288`. Primeiro lote: 2.255 jogos cruzados, 2.150 imagens distintas, 2.353 vínculos de catálogo substituídos na revisão40. Segundo lote: 295 novas artes Mega e27 PSX, 322 imagens, 412 vínculos substituídos na revisão41. Os aliases entram nesses números de vínculos. A revisão42 atualiza os carimbos de todas as capas SNES/BR, Mega/BR, PSX e N64: 2.057 itens visíveis voltam a ser buscados pelo cache existente.

As **157 capas N64 já coincidiam com os bytes compilados do Git**; foram conferidas e suas revisões atualizadas para o app baixar novamente. Não foram inventadas artes diferentes. SNES/Mega/PSX receberam os lotes novos onde a associação era única. JPEG RGB480×720, qualidade90, nos respectivos `media/revista` e seeds `media/catalogo`; não há PNG grande novo servindo o app. 278 correspondências sem arte única mantêm a correspondente existente; PS2/PSP/Wii/WiiU conservam as artes corrigidas do site. [Mapeamento por jogo/capa/Git](online-plataformas-20261009/capas-git-20261012.json).

1. Reabrir/atualizar a leitura autenticada de `GET /v1/station/catalog?metadata=1` e validar o envelope assinado. Exigir revisão42 ou superior para esta entrega.
2. Usar `itemId`, `platform`, `folderPath`, `metadata`, `coverId` e **a revisão do item** recebidos. Buscar `/v1/station/covers/{coverId}` com sessão/prova e os quatro workers existentes; cache por `coverId + item.revision`. Não reutilizar o cache antigo só porque o coverId ficou igual. Não precisa limpar dados, licença ou saves.
3. Para download, `POST /v1/station/downloads/authorize` e GET do grant uma vez em `/v1/station/artifacts/{grantId}`. Conservar formato e `launchPath` do descritor assinado; não construir URL pelo nome do jogo.
4. Para sala R138, consultar `POST /v1/station/online/multiplayer/command`, ação `capabilities`, com itemId e `clientMaximumPlayers:5`. Usar a lista assinada correspondente ao engine/core/runtime/profile/modo da R138, incluindo contagens permitidas. O catálogo público no Git é comparação documental, não autorização de sala.

A prova pública assinada conferiu todos os campos de catálogo; 2.255+322 corpos de capas foram conferidos pela API local autenticada e52 amostras pela rota HTTPS. A revisão42 repetiu os157 corpos N64 e quatro amostras públicas. Quatro workers em cada lote. Provas dos oito motores: 11 consultas assinadas locais e11 públicas; engine desconhecida, conteúdo errado, perfil desconhecido e Top Gear cinco recusados com409. DNS/TLS intermitentes deste PC foram contornados somente no cliente de verificação, usando endereços observados e mantendo hostname/certificado/prova; DNS/túnel/proxy/firewall do servidor não foram alterados.

As varreduras isolada/efetiva passaram sem reconstruir ROMs, alterar IDs, descritores, sinopses, identidades de conteúdo, licenças, esquema ou outros serviços. A recuperação corrigiu carimbos de arquivos após um rollback e preservou evidência privada. Nenhuma NativeActivity/ELF Android foi executada no servidor. Suíte de domínio:1.452 checks,29 novos3p, capacidade isolada320p/80salas; estes testes não homologam gameplay/WAN/Android. O scanner ainda informa firmware NeoCD ausente/inválido e uma cópia AOF2 não catalogada; não foi criada BIOS nem apagado o jogo publicado correspondente.

### Retorno à produção do APK e manutenção

APK esperado SHA `9183462e4d45cfcf2d2599d9e682b81890fee7b094505bf3073ab8183dd1dd10`; runtime `463bdb0b409e028aa34f6c9a09d291a3715ffb3eacb8f18ee28843e7373eb80f`. Recibos recebidos comprovam instalação no g52 e Edge30; A56 última R135. Aqui não houve montagem/instalação de APK ou USB. Testar Mario Kart2p, Top Gear2/3/4p, retorno de sinal, saída de host/convidado sem crash e cinco em modo que permita cinco. Registro/carga de120 quadros não é aceite físico.

Backup anterior publicado nos dois Gits: `backup/station-antes-r138-20261012` e tag `backup-station-antes-r138-20261012`, servidor a6b58e9/app a773988. Manutenção horária `turbostation-retornos.timer` mantida; acompanha branches de retorno docs/build/fix, novos kits em `/mnt/DADOS` e capas, escolhendo o commit mais recente mesmo quando o nome da branch é reutilizado. Usa lock para serializar trabalho e autenticação Linux nativa para arquivos protegidos. Não reaplicar workflows datados: consultar os recibos aplicados e o estado atual. Esta é a consolidação no mesmo handoff, com histórico abaixo preservado.

## Histórico preservado

## Retorno R129, capas novas e manutenção horária — 11/10/2026

A checagem das 14h recebeu **c433052**, variante R129 do mesmo handoff R128. Fonte Android `cf623c7`; APK Final-Modal `89dd5439`, 2.237.505.749 bytes. O produtor apresentou recibo de instalação integral no Motorola; autenticação e gameplay continuam sem confirmação. Os scripts administrativos e o contrato permanecem iguais; os quatro IDs foram renovados para este pacote. O kit R128 não corresponde aos runtimes R129.

Os dois operadores foram incorporados ao `library-tools` da entrega. **68 testes executados no Linux com os recibos R129 passaram**, os 18 selos públicos conferem e os quatro perfis canônicos foram regenerados. Os recibos históricos entregues foram preservados byte a byte. **A aplicação nos arquivos protegidos aguarda a autenticação nativa do Linux; os quatro motores novos ainda não foram ativados.** Faltam as seis bibliotecas privadas R129 e os modos por edição exigidos pelo handoff. PSX exige evidência HLE. Não fabricar bytes nem promover perfis genéricos anteriores. PS3/PS4 permanecem offline.

O Git correto é `Capas-TurboRama`, main `9f598e2` e pacote SNES `9f85e9d`. Foram cruzados **2.255 vínculos / 2.150 imagens distintas**, incluindo SNES/BR, Mega/BR, PSX, N64, Neo Geo, Dreamcast, GameCube, Saturn e Switch. As artes nomeadas recentes têm prioridade: **671 vínculos SNES, 331 Mega e 28 PSX**. Arquivos UUID e arquivos de gerações sem associação segura foram excluídos. Os 278 casos sem correspondência única mantêm a capa correta existente; PS2/PSP/Wii/WiiU conservam as correções do site. Os originais antigos terminaram de baixar; novos lotes estão chegando. A preparação compila JPEG RGB 480×720, qualidade 90, sem reconstruir ROMs. **Catálogo publicado: revisão 33 até existir recibo real da aplicação.**

A manutenção `turbostation-retornos.timer` está ativa, de hora em hora, com Linger habilitado. As execuções das 13h e 14h terminaram com status 0 e detectaram novos retornos/capas. Ela consulta os três Gits, evita publicações concorrentes, implementa novidades recebidas e registra pendências reais. Operações protegidas continuam exigindo autenticação Linux; bytes privados não disponíveis não são inventados. O próximo ciclo é às 15h. Não há reinício da API nesta preparação.

A janela nativa já está aguardando a senha neste servidor. O workflow preparado toma snapshot fresco, publica os operadores auditados, qualifica a troca de capas sem cópia de jogos, confere catálogo/capas por HTTPS assinado e registra a matriz v3. A conclusão produz `applied.json`, `r128-administration.json`, `r128-signed-matrix.json` e um marcador para o monitor atualizar este mesmo retorno. Estado privado: `/mnt/DADOS/station-r128-20261011`; kit de recebimento: `kit-privado` nessa pasta. O APK completo R129 também permite extrair as seis bibliotecas pela receita selada, sem chaves ou BIOS no Git.

Recibo consolidado: `online-plataformas-20261009/r128-retorno-servidor-20261011.json`. Backup estável publicado antes das mudanças: branch `backup/station-antes-r128-capas-20261011` e tag correspondente nos dois repositórios. Código preparado, dados aplicados e gameplay devem ser relatados conforme seus recibos reais. Não executar operadores históricos; atualizar este mesmo retorno quando a autenticação/kit chegarem.

# Station: catálogo, capas, downloads e servidor online — 10/10/2026

## Estado atual confirmado na produção

**Servidor concluído para as 17 plataformas solicitadas**, incluindo PS2 e Sega Saturn. A produção do APK pode implementar os motores usando esta mesma entrega. Servidor pronto significa transporte, autenticação, perfis e cadastro; os motores reais e o gameplay nos aparelhos continuam com as dependências indicadas abaixo.

- API ativa: PID **2136303**, NRestarts **0**; release `/opt/turborama-station-endpoints-20261010-2c0d03447696`.
- DLL SHA-256 `2c0d034476969cbed58b519b744e841111373f89a4bcd6abb9cd14543abcde35`; fonte do servidor e importador `7eb0e009d40ce01db86aea9c489937e9bd2fd00a`; scanner SHA-256 `7f6e9ebb6e1ccc71c184e7d3e07cac856f1c0ceee4e281e272061406619e36bd`.
- Catálogo **revisão 33 / 3.940 IDs / 3.685 visíveis / 255 aliases**, SHA `47087b1f35ae8cca418a108a606195adeb5bdb222bed351cfb206751dc56e986`. São 21 rótulos / 19 plataformas normalizadas no catálogo; 17 políticas online permanecem vigentes.
- **3.940 identidades**, **5.176 perfis / 5.169 aprovados**, preservando os perfis e os 3.895 vínculos de conteúdo anteriores. **320 jogos restantes / 321 modos preparados**.
- Revisão de endpoints publicada em `2026-10-10T21:49:22.368044+00:00`: somente Station reiniciado após confirmar zero salas e conexões. HTTPS autenticado confirmou os 3.640 itens e as 17 políticas, 19 capas com quatro workers e 19 amostras de download. Timer/importador ativo; varredura real sem mudanças. Licenças sintéticas removidas; configurações anteriores, clientes, licenças reais, esquema e outros serviços preservados. Túnel/proxy/firewall inalterados.

[Recibo anterior da revisão e ativação](online-plataformas-20261009/endpoint-review-20261010.json). [Organização Saturn, capas, descritores e cabeçalhos](online-plataformas-20261009/ps2-saturn-servidor-20261010.json). Este é o mesmo handoff; os recibos anteriores preservam seus números e horários históricos.

## Capas conferidas com o site hospedado — 10/10/2026

Correção aplicada em `2026-10-10T23:38:41.105858+00:00`. A integração anterior reutilizou **uma versão antiga das artes locais Turborama**. Os nomes/IDs eram do jogo correspondente, mas a arte publicada no site havia mudado. Comparação visual de Black e God of War II confirmou a diferença de modelo. O conjunto atual do site é a referência desta correção; o site não foi alterado.

Foram substituídas **36 capas: PS2 25/25, PSP 4/9, Wii 6/7 e Wii U 1/1**, com conferência de título e plataforma. PS2 também inclui a categoria BR para Resident Evil 4 dublado e a capa Bomba-Patch-2026 agora existente no site. Nenhuma arte nova foi gerada. Todas seguem JPEG RGB 480×720, qualidade90 e enquadramento completo, usando o compilador já instalado. O site oferece WebP360×540; o tamanho final480×720 mantém o padrão do Station, sem alegar resolução original superior.

Os **seis jogos sem capa nesse catálogo hospedado** mantêm a imagem correspondente já existente: Wii Epic Mickey2; PSP Crash: Mind Over Mutant, GTA: Vice City Stories, Moto GP, Soulcalibur: Broken Destiny e Toy Story3. Não usar GTA Chinatown Wars como capa de Vice City Stories nem versões de outras plataformas. Para ter exatamente a arte personalizada do site nesses seis, ela precisa primeiro existir no catálogo da plataforma correta.

Também foram preparadas **160 artes do site em `media/catalogo`**, incluindo todas as páginas PS2/BR e os catálogos PSP/Wii/WiiU. Os seeds existentes mantêm suas sinopses e aliases; God of War (USA) comum/BR recebe chaves distintas. As capas dos jogos atuais estão em `media/revista`. Em Wii U, a capa genérica `Turbo.jpg` foi substituída por `MARIO KART 8 [AMKE01].jpg` e a referência XML foi atualizada; o scanner seleciona pelo seed da pasta do jogo, evitando reutilizar a capa de Mario Kart8 em outro pacote com `code/Turbo.rpx`. Novos jogos usam os nomes/chaves do seed da plataforma, ou uma capa correspondente em revista.

**Leitura pelo app:** obter novamente `GET /v1/station/catalog?metadata=1` pela sessão e prova existentes. O catálogo agora é **33**; os36 itens alterados têm `item.revision=33`, mantendo `itemId` e `coverId`. Buscar `/v1/station/covers/{coverId}` com os quatro workers atuais e gravar o cache por **`coverId + item.revision`**. Não reaproveitar a capa da revisão anterior; não montar URLs de imagem pelo nome do jogo nem copiar artes embutidas na compilação. Client/RoomsDEX continuam os mesmos; o cache por revisão já está implementado. Atualizar a consulta do catálogo permite receber as capas novas, sem limpar dados ou saves.

**Conferência real:** envelope assinado, todos os3.685 itens públicos e os42 corpos de capa conferidos por bytes/identidade; quatro requisições de capa simultâneas. O parser do cliente já compilado leu o catálogo33. Scanner isolado e scanner efetivo retornaram `changed=false`, `added=0`, `updated=0`; zero tentativa de reconstruir esses jogos. Os3.940 IDs/identidades, descritores, metadados,5.176 perfis/5.169 aprovados,17 políticas online e arquivos de jogos permaneceram intactos. API mantém PID2136303/DLL2c0d03447696, sem restart; sandbox conferido e licença sintética removida. Nenhum teste visual nos celulares ou novo APK foi feito por esta alteração de dados.

Fontes: [PS2](https://turbobox.lzgames.com.br/catalogo.php?plataforma=playstation-2), [PS2 BR](https://turbobox.lzgames.com.br/catalogo.php?plataforma=playstation-2-br), [PSP](https://turbobox.lzgames.com.br/catalogo.php?plataforma=psp), [Wii](https://turbobox.lzgames.com.br/catalogo.php?plataforma=nintendo-wii), [Wii U](https://turbobox.lzgames.com.br/catalogo.php?plataforma=nintendo-wii-u). [Cruzamento individual, origem e provas](online-plataformas-20261009/capas-site-ps2-psp-wii-wiiu-20261010.json). Cópia estável anterior publicada nos doisGits: branch/tag `backup/station-antes-capas-site-20261010` / `backup-station-antes-capas-site-20261010`, server5f9c019/app2b4a6dd. Mesmo handoff; recibos anteriores continuam históricos.

## Sega 32X e PSP — catálogo, capas e downloads publicados

Aplicado em `2026-10-10T22:40:20.495314+00:00`, sem reiniciar a API: **36 jogos de Sega 32X e nove de PSP**. Pastas inteiras movidas de `snes/sega32x` e `snes/psp` para `sega32x` e `psp` na raiz do HD. Os 611 arquivos originais recebidos, XML, imagens, vídeos e demais dados permaneceram no conjunto.

**Capas:** 45 JPEG 480×720 em `media/revista`; nenhuma arte nova gerada. Sega 32X reutiliza suas 36 imagens recebidas. PSP reutiliza quatro artes correspondentes do catálogo Turborama e cinco imagens recebidas. As 21 artes PSP do catálogo e as demais artes XML foram preparadas em `media/catalogo`; os seeds registram 464 chaves PSP e 43 de 32X, excluindo associações ambíguas. Os 235 registros XML de PSP não são 235 ROMs: somente os nove arquivos presentes foram publicados.

**Downloads:** PSP oferece CSO bruto, com nome/extensão e descritor assinados. Armazenamento `readonly-hardlink` mantém o mesmo inode da ROM, root/0444, sem duplicar os 6,53 GB de jogos; a verificação inicial é offline, sem nova passagem de integridade em cada download do app. Sega 32X oferece ZIP com `launchPath` exato para `.32x`. Índice, identidades e configuração foram qualificados antes da publicação; todos os 3.895 itens anteriores e os registros online permaneceram iguais. Segunda varredura isolada e scanner real: `changed=false`, `added=0`, `updated=0`.

**Como o app lê:** abrir sessão/prova de aparelho pelo contrato existente e buscar `GET /v1/station/catalog?metadata=1`, verificando envelope e domínio. Usar `itemId`, plataforma explícita, revisão, `coverId`, metadados e `folderPath` da resposta. Buscar capas por `/v1/station/covers/{coverId}`, mantendo os quatro workers atuais e cache `coverId + revisão`. Autorizar cada download por `POST /v1/station/downloads/authorize`; usar o `grantId` uma vez em `/v1/station/artifacts/{grantId}` e executar o `launchPath` do descritor assinado. Nunca construir caminhos pela grafia do nome ou converter CSO para ISO. A recarga do índice publicado pode levar até dez segundos; atualizar a leitura do catálogo nesse intervalo.

O parser do **cliente já compilado** leu os 3.685 itens reais e validou as 45 associações de plataformas/descritores/sinopses; mapping `psp`/`sega32x` já existe, sem alteração de Java/DEX por estes dados. API pública assinada confirmou todos os campos, as 45 capas com quatro workers e 45 amostras de download de até 64 KiB, com grants de uso único. O sandbox efetivo leu 7759 arquivos e negou os quatro caminhos protegidos. São provas de catálogo/transporte; teste físico do APK não foi executado aqui.

Novos jogos dessas pastas entram pelo importador/timer existente, após estabilidade dos arquivos. Sinopses vêm de XML, seed ou override privado; capas correspondem por stem exato/seed sem associação ambígua. `metadata.players` é informativo: PSP pode indicar ad hoc e 32X pode indicar turnos. Esses números não aprovam vagas online. Esta inclusão entrega catálogo/capas/downloads; suas políticas/motores online exigem integração nativa própria. As 17 políticas anteriormente implementadas permanecem intactas.

[Recibo, cruzamento das artes, descritores e provas](online-plataformas-20261009/sega32x-psp-publicados-20261010.json). Cópia estável anterior já publicada nos dois Gits: branch/tag `backup/station-antes-sega32x-psp-20261010` / `backup-station-antes-sega32x-psp-20261010`, servidor `6dbc9e2`, app `0d51ce1`.

## Limpeza do HD DADOS — concluída

Removidos **117 caminhos de compilações antigas, cópias de qualificação e arquivos intermediários**, recuperando **138.97 GiB** reais. Espaço disponível: **62.43 → 201.40 GiB**. Última entrega Station do servidor/app e as 22 saídas do último conjunto completo Turborama foram preservadas e conferidas por hash. Fontes, SDKs usados pela entrega Station, histórico Git, worktrees com alterações, arquivos privados, ROMs/capas em uso, licenças, clientes, bancos e backups das artes originais foram mantidos. Pastas históricas removidas continuam recuperáveis pelo Git; os arquivos de compilação podem ser reconstruídos.

Pós-limpeza: mesmo PID/DLL, catálogo 32 assinado e 3.685 visíveis, todas as mídias publicadas legíveis, registros online e configurações preservados, timer ativo. Nenhum serviço foi reiniciado. Caminhos intermediários datados removidos não devem ser usados por operadores históricos; usar a entrega atual e suas fontes/receitas. [Inventário da remoção e conferência final](online-plataformas-20261009/limpeza-compilacoes-20261010.json).

## Revisão de endpoints, ações e recebimentos — aplicada em 10/10/2026

A revisão inventariou **14 rotas da API Station, três rotas de prontidão local, 15 rotas administrativas Station, 21 ações v1/social e 12 ações v3**. O recibo lista os caminhos, o código verificado, testes e escopo. As rotas do painel foram examinadas e o self-test administrativo passou; ações comerciais não foram repetidas sobre clientes reais.

| Falha encontrada | Correção aplicada |
|---|---|
| Publicação do índice entre carga inicial e criação do monitor podia deixar o catálogo antigo em uso; recargas paralelas podiam competir. | A versão carregada guarda seu próprio carimbo, valida antes/depois da leitura e serializa recargas; snapshot válido e grants anteriores continuam preservados. |
| Quebras de linha/tabulação em campos de uma linha podiam provocar recusa do parser Android. | Normalização em leitura/importação; a sinopse mantém as quebras permitidas. O índice atual não precisou de alteração. |
| Cancelamento da autenticação do relay antes do upgrade podia terminar como HTTP200 vazio. | Timeout recebe JSON504 com código v1/v3 correspondente; anexos são limpos e cancelamento do cliente não inicia outra resposta. |
| Falhas de autenticação/tempo/arquivo podiam devolver erro vazio ou escapar do tratamento esperado. | Erros de online, prova de pedido e capas seguem status/código JSON finito e Retry-After quando aplicável. |
| O app exibia somente as primeiras 32 salas v3; o cursor compartilhado de pessoas/salas impedia percorrer as demais corretamente. | Paginação opcional no servidor; cursores separados na composição do app, própria sala sempre incluída e presença v3 calculada independentemente da página. |
| Uma falha transitória na consulta podia encerrar o acompanhamento da sala. | O app compilado repete somente consultas/heartbeat após recuo e respeita Retry-After; falhas de identidade/assinatura/acesso continuam terminais. |

**Leitura das salas pelo APK:** enviar `page` de 0 a 40 e `roomPageSize:32` em v3. Sem os campos, o servidor mantém página 0/lote 100 para clientes anteriores. Usar `nextPage` da resposta v3 como próximo cursor de salas; ele é `null` no fim. `totalRooms` conta as salas visíveis antes do recorte. `room` própria independe da página. Em social, manter `nextPeerPage` para Pessoas e `nextRoomPage` para salas antigas; `nextPage` anterior continua disponível. Não sobrescrever esses cursores ao combinar as respostas assinadas. Navegar entre Pessoas/Salas reinicia em página 0, e uma página ainda não recebida mostra consulta em andamento.

**Recuperação das consultas:** HTTP 408/429/500/502/503/504 ou Offline/SocketTimeout/SocketException usam 1, 2, 4 e no máximo 8 segundos de recuo, ampliados por Retry-After até 60 s. POLL_EXISTS usa 1 s. Apenas um poll fica pendente por aparelho. Cancelamento explícito, acesso 401/403, serviço desativado e falhas TLS/JSON/assinatura/identidade encerram a tentativa. Criar, entrar, marcar Pronto, iniciar e outras ações não têm repetição automática. A espera de 10 s e o limite de 15 s já passaram antes da revisão; não foi necessário aumentar o timeout.

**Validação da candidata e da publicação:** dez projetos de testes do servidor passaram, incluindo 1.423 checks v3, 179 checks TLS v3, 62 HTTP v1, 318 convites/códigos, 91 recuperação + 589 observações, 88 falhas de fechamento e regressão da Suite/provas/transferência. TLS v2 separado passou 49 checks. Os 212 fontes Java/D8 foram recompilados; 36 checks do lobby e 200 de perfis passaram sobre a compilação correspondente. HTTPS real conferiu os itens pelo itemId e todos os campos/metadados publicados; 19 capas e 19 downloads tiveram bytes comparados, cabeçalhos e uso único dos grants. Downloads grandes tiveram amostra de 256 KiB por rótulo; isso não verifica o corpo integral.

O sandbox efetivo do novo PID leu **7.670 arquivos de mídia/índice/registros/binário**, negou quatro caminhos protegidos e manteve montagens sem escrita. Timer ativo, scanner status 0 e revisão 31/changed=false/added=0/updated=0. No intervalo da ativação até a conferência às 21:52 UTC, os 1.300 registros Station coletados foram informativos, sem códigos de erro ou exceções; esse intervalo não comprova estabilidade indefinida ou gameplay Android. Túnel compartilhado e demais produtos permaneceram intactos.

Servidor já atualizado **antes** do novo APK, pois o parser anterior não aceitava os campos novos. A produção APK deve consumir esta mesma candidata/receitas, montar com a base privada e certificado original e atualizar os aparelhos preservando dados. Rooms DEX atual: `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`; Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`. APK completo, instalação, aparência e gameplay físico ainda não foram comprovados nesta revisão; as dependências nativas das plataformas continuam listadas abaixo.

Cópia anterior publicada antes das mudanças: branch `backup/station-antes-revisao-endpoints-20261010` e tag `backup-station-antes-revisao-endpoints-20261010` nos dois repositórios; servidor 919d31a/app 941b761. A versão de retorno foi qualificada em sombra antes da ativação. Recibos locais em `/mnt/DADOS/station-endpoint-review-20261010/`; não repetir operadores datados. O operador atual já tem marcador applied.json. Salas/conexões são dinâmicas; uma próxima implantação deve consultar o estado atual.

## Plataformas e limites do servidor

| Plataforma | Máximo por sala | Dependência do app |
|---|---:|---|
| SNES / BR | 5 | Modos cadastrados, multitap para cinco e runtime correspondente. |
| Mega Drive / BR | 2 | Motores/controles anteriores preservados. |
| N64 | 4 | Perfis de duas/quatro portas e modo exato; não transformar campanha solo em multiplayer. |
| Dreamcast | 4 | Adaptador nativo Station; o GGPO de duas entradas não comprova quatro. |
| GameCube / Wii / Wii U | 4 | Adaptador nativo; GBA Link, controles compartilhados e Co-Star usam layouts próprios. |
| PS2 / Sega Saturn | 2 | Motores reais, sincronização, duas entradas e BIOS quando exigida. |
| Switch | 2 | Motor/adaptador real e modo local da edição. |
| Neo Geo / Neo Geo CD / PSX / FBNeo / CPS1 / CPS2 / CPS3 | 2 | Driver, BIOS e perfil exatos; exceções conhecidas preservadas. |

O limite da plataforma não substitui `allowedPlayerCounts` do perfil. Não abrir sala com mais entradas do que o modo oferece. `Ver detalhes` não transmite gameplay nem cria espectador. Sete vínculos continuam sem aprovação: seis conjuntos arcade sem associação exata e Worms Armageddon por turnos. O catálogo ainda registra aof2 incompleto, 65 referências XML sem ROM, 148 sinopses ausentes e o requisito de BIOS NeoCD; não foram fabricados arquivos/compatibilidade.

## PS2 resolvido no servidor

Os 25 ISO/CSO estão na raiz `ps2`, com revista, artes de cadastro, XML, seed, downloads raw e **política online de duas pessoas**. `ps2br`/PlayStation 2 BR normalizam para `ps2`. Layout: `ps2-two-controllers-v1`. Seus 25 modos foram preparados: 13 entradas individuais e 12 de duas vagas por política de uso; estas não são homologação de modo original ou Android. O motor real precisa respeitar cada edição/modo. Nenhum jogo foi associado ao motor PSX.

Os cores [Play!](https://docs.libretro.com/library/play/) e [LRPS2](https://docs.libretro.com/library/lrps2/) documentam ausência de netplay. Portanto o APK precisa fornecer um motor/adaptador capaz de estado sincronizado e controles remotos; somente cadastrar o nome PS2 não cria essa capacidade. A API responde `serverReady:true` e `online-engine-pending` até existir o vínculo real.

## Sega Saturn organizado e publicado

`snes/saturn` foi recortada para **`saturn` na raiz do HD**, ao lado de SNES/PS2/Mega. Pasta completa: `/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc/saturn`.

- **19 jogos**: 17 CHD servidos raw pelo mesmo inode, sem segunda cópia, e Bug!/Rayman originalmente em 7z, preparados como ZIP completo. Bug! inclui CUE + 73 faixas; Rayman, CUE + 51 faixas. Originais preservados. Ambos possuem identidade `cue-set-v1`; o hash do CUE sozinho não representa o disco.
- **19 capas de revista**, 16 reutilizadas do catálogo Sega Saturn e três criadas com a ferramenta imagegen: Bomberman, Bug! e Rayman. Todas JPEG RGB 480×720; **34 artes de catálogo / 37 chaves** no seed. Prompts e caminhos finais no recibo. As imagens geradas originais ficam no backup privado; JPEG final em `saturn/media/revista`.
- Os 17 cabeçalhos CHD confirmaram Sega Saturn e os títulos correspondentes. `Bomberman.chd` identifica SATURN BOMBERMAN/MK-81070; Castlevania identifica DRACULA-X. Essa leitura de 4 KiB não é teste completo de disco ou emulação.
- Policy online pronta, teto dois, aliases Sega Saturn/segasaturn, layout `saturn-two-controllers-v1`. Há 19 modos preparados, respeitando as entradas individuais cadastradas; todos aguardam o motor real do APK.
- O core [Beetle Saturn](https://docs.libretro.com/library/beetle_saturn/) documenta netplay e formatos CUE/CHD, mas o APK ainda precisa integrar o adaptador Station, controles e BIOS legítima. A pasta `saturn/bios` está preparada; nenhuma BIOS proprietária foi baixada ou fornecida nesta entrega.

## SNES: mesmo cadastro automático

SNES mantém jogos, grupos, IDs e perfis online anteriores, inclusive até cinco nos modos cadastrados. `snes/station-catalog-seed.json` agora oferece **1.366 chaves não ambíguas**, reutilizando as **1.619 capas já comprimidas de revista**. Doze chaves conflitantes foram excluídas da escolha automática. XML/overrides têm prioridade; o seed preenche dados ausentes. Dois registros/aliases de Battletoads receberam campos de metadata que estavam vazios; descrição, ROM, capa e vínculo de partida foram preservados.

Para novo jogo, colocar a ROM na pasta da plataforma. O timer exige duas observações estáveis e idade mínima de 20 segundos, então publica nome, ID, capa, descritor e identidade. Capas conhecidas usam a associação por título; arte própria fica em `media/revista` com o nome exato da ROM. Dados complementares vêm de XML/seed/overrides. Título desconhecido usa a capa padrão existente até receber arte própria; uma sinopse não é inventada. Não é necessário programar cada jogo.

PS2 aceita ISO/CSO; Saturn, CHD/CUE/ISO e arquivos ZIP/7z/RAR com um CUE completo ou `launchPath` explícito. ISO é formato de publicação; o motor que vier precisa declarar o que realmente abre. Não inventar CUE/faixas. CHD/ISO raw de Saturn usa armazenamento protegido no mesmo volume, root/0444 e um inode. Substituir ROM significa copiar um arquivo novo e substituir o nome, preservando os artefatos de downloads anteriores. As outras plataformas mantêm suas configurações.

## Como o aplicativo deve ler o servidor

1. Usar `https://app.lzgames.com.br`, licença original, bearer e provas vinculadas ao aparelho. Ler e validar `GET /v1/station/catalog?metadata=1`. Usar a revisão recebida, `itemId`, `platform`, `name`, `metadata`, `coverId`, `folderPath`, `contentSha256` e eventual `contentIdentityScheme`; caminhos do HD não são URLs.
2. Buscar `/v1/station/covers/{coverId}` pela mesma linha do catálogo, quatro workers e cache `coverId + revisão do item`. As capas servidas são JPEG de 480×720. Não tentar casar capas pelo índice de outra lista.
3. Autorizar `/v1/station/downloads/authorize` com o itemId e validar seu descritor assinado `artifact`. Consumir `/v1/station/artifacts/{grantId}`. Raw preserva arquivo/extension/launchPath; ZIP extrai o pacote completo para um diretório exclusivo. Seguir tamanhos `long`, formato, fileCount e caminho de lançamento. Nenhuma espera, limite de velocidade ou verificação extra foi adicionada ao downloader.
4. Enviar `enter` autenticado em `/v1/station/online/command` antes de v3. Manter somente um poll de `online/events` por aparelho e respeitar `Retry-After`. Declarar `clientMaximumPlayers` conforme o runtime real.
5. Em `/v1/station/online/multiplayer/command`, usar `capabilities`/`snapshot`. `serverPlatforms` lista as 17 políticas. `platformPolicy.serverReady:true` é preparação do servidor; `onlineAvailable` requer perfil aprovado de conteúdo/motor/runtime/controles. `online-engine-pending` indica o motor real ainda ausente. Tratar também `single-player`, `mode-pending`, `content-identity-pending` e `select-game`.
6. Selecionar perfil exato e quantidade em `allowedPlayerCounts`; criar/entrar/pronto/iniciar. Convites e tickets são automáticos. Conectar WSS `/v1/station/online/multiplayer/relay`, protocolo `station-stream.v3`, um link duplex anfitrião↔cada convidado.
7. Respeitar TSR3, HELLO, PAUSED, READY, ACK e época global. ACK confirma escrita no motor. Preservar sessão nativa na troca do WSS. Identidade CUE/CHD é calculada na preparação/importação, sem varredura do corpo em cada download do servidor; o app compara a identidade para conectar versões iguais na partida.

## Sinopses na sala — ajuste para a próxima compilação do APK

Títulos, botões e estados são escritos pelo app. `modeTitle` e `instructions` vêm dos perfis assinados do servidor. A **sinopse vem de `metadata.description` do catálogo assinado**, já publicado em `GET /v1/station/catalog?metadata=1`. O app deve procurar pelo `itemId` exato da sala, inclusive para o convidado; não associar pelo nome ou posição da lista.

O fonte `StationRoomsActivity` desta mesma candidata foi ajustado: **Criar sala e Sua sala mostram a sinopse em quatro linhas, com toque para ler o texto inteiro**. Outras salas mostram duas linhas e a íntegra em Ver detalhes. Os limites permanecem compactos perto dos controles; instruções dos modos, controles e posições ficam acessíveis em **Como jogar**. Sem sinopse cadastrada, mostrar “Sinopse ainda não cadastrada.”. O catálogo revisionado é consultado em memória; nenhuma requisição nova por sala.

O catálogo31 contém **3.492 sinopses entre 3.640 jogos visíveis**; os demais precisam de texto cadastrado. Java212/D8 recompilados e selados. **O APK completo assinado e a aparência nos celulares ainda precisam da compilação/instalação no PCAPK**; não há APK instalado por esta alteração. A apresentação das sinopses não exigiu alteração da API ou das regras de admissão; a revisão de endpoints acima foi aplicada posteriormente. Comparar `roomPresentation` no contrato e `evidence/java-build.json` no app.

Cópia anterior publicada nos dois Gits antes deste ajuste: branch `backup/station-antes-sinopse-sala-20261010`, tag `backup-station-antes-sinopse-sala-20261010`; servidor `f478d13`, app `45586f6`. Preservar este mesmo handoff.

## Cadastro do motor real sem nova DLL

O manifesto recebido do APK deve declarar engineId imutável, plataforma, formatos reais, `library`, `runtimeLibrary`, seus hashes SHA-256, `launchReady:true`, `recoveryProtocol:station-stream.v3`, `maximumPlayers` e `controllerProfiles`. Para PS2/Saturn, declarar os layouts acima explicitamente. Cada layout contém `configuration` com `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. O motor/decoder do app deve implementar essa configuração; nomes de controles por si só não representam sua implementação.

`profileSha256` é SHA-256 do JSON compacto UTF-8, sem BOM/quebra final, Unicode sem escapes e campos na ordem `schemaVersion`, `controllerProfile`, `devices`, `coreOptions`. Exemplos do contrato não são recibos nativos. Não copiar hashes ilustrativos para a produção.

Operador instalado: `/opt/turborama-station-endpoints-20261010-2c0d03447696/library-tools/cadastrar-motor-online-station.py install`, com `--config` da configuração privada efetiva, `--incoming` do manifesto real, `--artifacts` dos binários e `--output` de uma pasta privada nova. Exige administração local, confere ambos os binários e preserva perfis anteriores. `prepare` permite revisão offline; `apply` publica a candidata ainda correspondente ao catálogo. **Recarga em até dez segundos, sem recompilar ou reiniciar o servidor.** Credenciais do app não cadastram motores pela API.

## Testes, capacidade e entrega ao APK

Na publicação histórica PS2/Saturn passaram **1.292 verificações C#**, **172 TLS loopback**, **16 testes Python** de cadastro/importação/permissões e **855 verificações v3 assinadas na sombra**: início, dados nos dois sentidos, retomada e perfis SNES de duas/quatro/cinco pessoas. As 17 políticas e o catálogo/mídia também foram conferidos pelo HTTPS público após ativação. Testes sintéticos não homologam latência, controles ou gameplay Android.

Capacidade configurada: v3 até 100 salas, cinco pessoas conforme modo, janela 256 KiB por direção e replay compartilhado 128 MiB; v1/v2 preservados. Teste sintético de 320 participantes/80 salas de quatro/7.864.320 bytes passou. O orçamento limita admissões; não significa 100 salas de cinco simultâneas ou medição da internet.

O pacote `versions/station-all-platforms-online-20261009` foi recompilado: **212 Java/D8**, alias PS2 BR corrigido e sinopses da sala integradas. Client DEX permanece `e7207a89517b168cc476e30a823bed3a1b6ad57339e1042f08a4660184543d63`; Rooms DEX agora `c87d3efc19ff760015b355a00a45177bb43345ac9bb7a98fb53e61b87cd920e7`. Runtime `81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc` e motores reais anteriores preservados. Nenhum APK completo foi assinado/instalado nesta sessão; base/keystore continuam no PC do APK. Próxima etapa reúne Dreamcast, GameCube, Wii, Wii U, Switch, PS2 e Saturn, assinatura/instalação e testes físicos.

## Arquivos únicos para comparar e retomar

- `catalogo-cruzado-completo.json`: todos os 3.895 IDs, nomes, plataformas, capas, metadata, artefatos, identidades, perfis e modos preparados; sem caminhos privados de mídia.
- `platform-summary.json`, `profiles-missing.json`: contagens por plataforma e dependências reais.
- `content-identities.json`: 3.895 vínculos/schema2; `profiles-complete.json`: todos os 5.176 perfis. Não substituir por `profiles-new-profiles.json`, que contém só adições históricas.
- `server-integration-contract.json`, `server-prepared-modes.json`: contrato v3 completo, 320 jogos/321 modos, layouts, hashes e receita de cadastro.
- `engines-app.json`: motores reais atuais e políticas do cliente; nenhum engine PS2/Saturn fictício.
- `library-tools/`: scanner, todas as dependências, operador de cadastro e testes correspondentes.
- `endpoint-review-20261010.json`: revisão atual, inventário de rotas/ações, regressões, compilação, ativação e verificação efetiva. `ps2-saturn-servidor-20261010.json`: organização e publicação histórica PS2/Saturn. `ps2-switch-publicados-20261010.json`, `capas-padronizadas-20261010.json`, `checkup-completo-20261010.json`, `server-ready-deployment.json`, `production-applied.json` e `profile-reload-fix.json` preservam as provas históricas. ZIP de 09/10 continua histórico e não representa o catálogo31.

Cópia estável anterior publicada antes das mudanças nos dois Gits: branch `backup/station-antes-online-ps2-saturn-20261010`, tag `backup-station-antes-online-ps2-saturn-20261010`, servidor `26c5302`, app `52ba50d`. Backup privado atual: `/mnt/DADOS/station-ps2-saturn-20261010/backup`. Não repetir `apply.py` nem operadores datados. Consultar o estado dinâmico das salas antes de agir sobre serviços. Os dois arquivos CRLF das entregas R71/R78 anteriores ficaram fora desta alteração.
