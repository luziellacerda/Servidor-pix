# Estado de produção — comunidade R41 e painel Station — 06/10/2026

Leia [comunidade R41](RETORNO-COMUNIDADE-STATION-R41-20261006.md) e [códigos no painel](RETORNO-PAINEL-CODIGOS-STATION-20261006.md). APIa2bb176/DLLd181bf97/PID875574, SocialEnabled=true; SamsungR41 instalado segundo recibo5e40f7e. Catálogo14/2212 e relay512/1024 preservados. Site17e564a com Códigos Station/Gerar código/Trocar celular publicado e conferido; não exige novoAPK. Código30min/uso único/um aparelho por licença. Gameplay em dupla, POCO e latência externa continuam pendentes. Os estados de API/APK nos blocos abaixo são históricos.

# Segundo jogador — correção conciliada com R34 — 05/10/2026

Leia RETORNO-SEGUNDO-JOGADOR-SALAS-STATION-20261005.md. Pronto confirma a sala atual; a entrada precisa ocorrer primeiro. Fonte app f7f0561: três classes alteradas sobre a fonte exata R34,147 entradas Java/dependências compiladas,144 preservadas e213 verificações aprovadas. DEX39864bd1 pronto; montagem e instalação do APK desta correção pendentes. O retorno a8898a0 confirma R34/SHA513dd470 instalado no Samsung; a versão do POCO ainda não foi conferida. Mantidos recuperação de abertura, Voltar/manifesto e design R33. Servidor e4e557a inalterado. Uso imediato: POCO Sair da sala → CódigoTS1 do primeiro telefone → Entrar → dois nomes juntos → ambos Pronto → anfitrião Iniciar. Preservar dados/assinatura/saves e conciliar sucessoras antes de montar; DEX inicial R30 foi substituído. Gameplay em dupla e latência externa baixa continuam pendentes.

## Histórico anterior — consultar o retorno R34 acima

## Retorno final recebido — appR30

Retorno b4a9806 confirma R30 instalado/hash1768b7df em05/10 às18h39, Voltar/criação de sala/Pronto verificados em um aparelho. Usar R30 ou sucessora noPOCO, preservando dados/assinatura/saves; R27 abaixo é histórico. Downloads11be7f3/6f012a7 permanecem fora; doisaparelhos/gameplay e latência externa baixa continuam pendentes.

# Estado vigente — POCO, relay e capacidade — 05/10/2026

Leia [o retorno R12/POCO](RETORNO-SERVIDOR-NETPLAY-INTERNET-STATION-R12-20261005.md). API `e4e557a`, DLL `7ecb6c8d`, PID660598; relay privado publicado no mesmo domínio, 512 salas/1.024 conexões configuradas. Passaram 256 conexões reais, 256 renovações e 512 conexões TLS isoladas, com zero resíduos. **Latência externa alta permanece aberta:** p95 público2.291,82ms versus API0,83ms/Nginxlocal1,07ms. Gameplay de doisAndroid e partidas responsivas para centenas precisam de homologação.

Licença própria POCO vitalícia/um aparelho criada e auditada; código apenas no arquivo privado do operador, ativação até07/10 às17h11Maceió. Retornoapp4fd2231 confirma R27 instalado/hashc1191ce1: preservar assinatura, dados, saves, R26visual e R27salas. Catálogo14/2.212visíveis/50CD, importação, capas e downloads sem capMB/s preservados. LimpezaWS, coldboot, handshake e conflitos entre renovações foram corrigidos. Delta11be7f3/6f012a7 continua fora doAPK27.

Os blocos seguintes são históricos e não identificam aAPI ou instalação atual.

---

# Atualização05/10: downloads CHD diretos e ZIP sem CRC

Leia [o retorno de downloads](RETORNO-DOWNLOADS-SEM-VERIFICACOES-20261005.md). Fonte/DEX/ponte nativa preparados e testados; ainda precisa entrar em APK assinado/instalado sobreR20ou sucessora conciliada. Remove segunda cópia RAW/conferências redundantes, CRC de ZIP e GET do catálogo inteiro antes de cada sessão nova de download. 821 verificações Java/18suítes +14JNI reais Linux passaram. Módulos prontos e guard de base estão no retorno.

Produção mantém catálogo14/2212visíveis/255compat/50CD, API931030b/PID347227; sem deploy ou restart. Medição completa Metal Slug431,226MB: API311,329MB/s; Nginx311,232MB/s; HTTPS3,225MB/s/133,7035s. Caminho externo variável, separado da preparação local; aparelho ainda sem medição nova. Teste sintético limpo. A fonte APKvigente recebida éR20, nãoR18; preservar ajuste visual posterior de consoles apenas na sinopse e ponteCD separada.

## Retorno anterior da biblioteca CD

# Atualização vigente — Neo Geo CD publicado, catálogo14 — 05/10/2026

**2.212 jogos / 50 Neo Geo CD / 50 capas revista / 50 sinopses CD**, 255 IDs ocultos e todos os registros anteriores preservados. Pasta `neogeo/neogeocd` mantida. Originais`.img` são CHDv5 íntegros; entrega`.chd` tem bytes idênticos, sem recompressão. BIOS CD ausente; catálogo/downloads funcionam, abertura exige firmware.

API931030b/PID347227 mantida. Scanner9cff9b3, índice14 SHA07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92. Oito pares de capa/download HTTPS, contrato assinado/metadados/pastas/usoúnico e50capas com4workers passaram. Autoimport por minuto/reload10s; sem limite artificial de MB/s.

**Novo retorno R18 instalado incorporado**, APK a29151da; preserva filesystem/rompath MAME, navegaçãoR17, offline/downloadR16, N64 e salas. [DeltaCD5dea14c](https://github.com/luziellacerda/TurboElden/tree/5dea14c8b361ec6f0a8fb6c8a1ac92720d27542a/versions/station-neogeocd-20261005) acrescenta montagemCDZ/importaçãoBIOS, DEXcompilado e25checks; ainda sem APKnovo/instalaçãoCD. Não repetir overlayR11/R15 nem reintroduzir hash do corpo removido noR16.

[Retorno completo](RETORNO-SERVIDOR-NEOGEOCD-20261005.md), [catálogo completo](biblioteca-neogeocd-20261005/README.md), [50CD cruzados](biblioteca-neogeocd-20261005/neogeocd-jogos-capas-downloads.tsv), [guia de manutenção](BIBLIOTECA-AUTOMATICA-STATION-20261004.md). Backup vigente`/mnt/DADOS/station-neogeocd-backup-20261005`; rollback específico monotônico14→15, recusa descartar imports posteriores. As instruções de rollback/R11/R15 nos blocos abaixo são históricas.

---

# Histórico — Neo Geo, catálogo9 e velocidade, 05/10/2026

**Catálogo 9 / 2.162 jogos**, incluindo **189 Neo Geo** e 157 N64, com 255 IDs ocultos preservados. Há2.119 sinopses,43 sem fonte e374 jogos em subpastas. Neo Geo movido de SNES para a raiz do HD,826 arquivos preservados. Capas exatas da revista480×720; pacotes instalam ZIP fechado e BIOS ao lado. Alpha Mission II recebeu somente a BIOS exata no pacote de entrega; **Art of Fighting 2 aguarda ZIP íntegro**, pois o chip056-c7.c7 está corrompido.

**API 931030b/PID 347227 mantida, sem reinício**; DLL0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0. Scanner próprio selado na fonte cb4921431144228360a95193eeeac485cc3addc2. Índice9 SHAc5cc7944ce4ad224f85e2f4218c4c91915ac6dd2bda530368554822969d86492. Autoimport por minuto/reload10s/online ativos,12serviços compartilhados preservados,zero licenças sintéticas.189capas HTTPS verificadas com4workers,ZIP+BIOS/metadata/folderPath/grants passaram.

O usuário esclareceu que o contador avança de1 MB em 1 MB; esse contador não mede MB/s. Arquivo real autorizado mediu **316,35MB/s API local /259,99MB/s Nginx /até4,37MB/s HTTPS público**; controle de upload3,77MB/s ou4,34agregados com2conexões. Todos os jogos de todas as plataformas publicadas usam a mesma rota, sem restrição artificial de MB/s. Sem limitador de bytes/s encontrado. Não houve mudança global em Nginx/rede/Cloudflare. A velocidade do telefone continua sem medição.

**Cliente publicado c8e240a2c886122e79ca2105c0a719a9217ed7dc**, preservando os novos commitsR10/R11 do retorno6ef86c4: [delta de três arquivos, builder e instruções](https://github.com/luziellacerda/TurboElden/tree/c8e240a2c886122e79ca2105c0a719a9217ed7dc/versions/station-neogeo-rate-20261005). Mostra percentual e MB/s real durante Baixando;9checksC++ de taxa e5Java de ZIP/BIOS passaram,JNI compilada e sintaxe do rendererR11 conferida com imagens somente de teste. **R9 é o último instalado comprovado; R11 está compilado/assinado, USB pendente.** O ajuste de tela já voltou a0. O próximo APK deve incorporar somente este delta sobreR11, JNI/carousel juntos, preservando DEXR10/salas, assinatura original e dados; nenhum novo APK foi assinado/instalado no Linux.

Leia o [retorno completo](RETORNO-SERVIDOR-NEOGEO-VELOCIDADE-20261005.md), [catálogo2.162](biblioteca-20261005/catalogo-completo.tsv), [189NeoGeo](biblioteca-20261005/neogeo-jogos-capas-downloads.tsv), [43pendências](biblioteca-20261005/metadados-pendentes.tsv), [guia de pasta/sinopse](BIBLIOTECA-AUTOMATICA-STATION-20261004.md) e [prova final](biblioteca-20261005/evidencia-estado-final.json). Backup vigente `/mnt/DADOS/station-neogeo-backup-20261005`; retorno `scripts/implantar-neogeo-station-20261005.py --rollback`, índice monotonicamente maior9→10, API sem reinício, guardas recusam publicação posterior. **Rollbacks de04/10 abaixo são históricos.** Gameplay Neo Geo, visualR11/velocidade no aparelho e partida entre dois aparelhos ainda precisam de prova.

---

# Histórico — N64, biblioteca automática e pastas R9 publicados, 04/10/2026

**API `931030b`**, DLL `0b3f5da385216d216fb55220789f55c40b8eb304b7b1a4759cc154b1aa3f3ab0`, PID 347227/UID 995. Catálogo **8 / 1.973 jogos visíveis**: SNES 644, SNES BR 191, Mega 887, Mega BR 94, **N64 157**. Preservados 255 IDs ocultos e os 996 IDs originais; 2.228 entradas internas. Índice SHA `f56cf70267251ade518ae111be88627db83d592436b7fb3bf522c119e8fefb8c`. Há **1.957 sinopses** e 16 edições sem fonte. `folderPath` publicado nos dois contratos, com 313 jogos em subpastas.

N64 movido de `megadrive/n64` à raiz do HD, com 828 arquivos preservados. Capas exatas da revista, originais intactos, N64 compilado em 480×720. As 157 capas passaram pela conferência HTTPS com quatro workers em 13,85 segundos/24,4 MB. ROMs, descritores, bytes, SHA e concessões de uso único conferidos. Importação por minuto e reload da API a cada 10 segundos; acréscimo real 6→7 sem reiniciar a API. O código de pastas R9 foi publicado na release atual. Online ativo, 92 verificações candidato + 92 HTTPS, 12 PIDs/configurações compartilhadas preservados e zero licenças sintéticas remanescentes.

Leia o [retorno completo ao cliente](RETORNO-SERVIDOR-N64-BIBLIOTECA-20261004.md), [catálogo cruzado](biblioteca-20261004/catalogo-completo.tsv), [guia de jogos/pastas/sinopses](BIBLIOTECA-AUTOMATICA-STATION-20261004.md) e [estado final](biblioteca-20261004/evidencia-estado-final.json).

Cliente fonte **`ba669c27418341c7f232a644317881779ddd3bf3`**, conciliado com R9; [overlay e builder](https://github.com/luziellacerda/TurboElden/tree/ba669c27418341c7f232a644317881779ddd3bf3/versions/station-library-autodiscovery-20261004). Lê `catalog?metadata=1`, mantém cache/leases, acompanha novas revisões em primeiro plano e publica sinopses com a hierarquia. **433 verificações Java, 36 C++ de coleções, 429 C++ de navegação; DEX/JNI compilados.** R8 é o último instalado comprovado, R9 anterior é candidato no Windows. Esta integração ainda precisa ser incorporada ao novo APK, assinada e instalada; gameplay N64 e partida entre dois aparelhos continuam pendentes.

Backup vigente: `/mnt/DADOS/station-folders-backup-20261004`; retorno **implantar-pastas-station-20261004.py --rollback**, limitado aos overrides próprios, restaura a API 77d8d54 mantendo N64/importação/online/conteúdos. As rotinas anteriores foram superadas. Os blocos seguintes são evidências históricas; o estado vigente está neste início.

---

# Histórico — salas online publicadas em04/10/2026,11h29

**API atual77d1dfb**, DLL SHA256 `ff6362852635d4d18a01e85e46c89ad5cc2a7dad75d3b99793a124733beb6509`, serviço Station/PID321167. Presença, salas de dois jogadores, convites e chat habilitados nas duas rotas POST `/v1/station/online/command` e `/v1/station/online/events`, HTTPS200 com sessões sintéticas assinadas; anônimo401. **92 verificações candidato +92 HTTPS**, reinício em processos candidatos, flag desligada503 e registro de motores SNES/Mega coincidente com o APK. Proxy exato30s, backup restaurado/hash conferido;12 PIDs compartilhados preservados; nenhuma migration/chave/cliente real alterado. Catálogo **4/1.816**, revista, descritores e jogos iguais à publicação anterior.

Leia o [retorno ONL-01 a ONL-07](RETORNO-SERVIDOR-ONLINE-STATION-20261004.md), pedido [APP→servidor](HANDOFF-APP-PARA-SERVIDOR-ONLINE-20261004.md) e [evidência](online-20261004/evidencia-publicacao-linux.json). Artefato `/opt/turborama-station-online-20261004-77d1dfb`, override `zzzz-station-online-20261004.conf`, registro SHA256 `901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a`. Backup `/mnt/DADOS/station-online-backup-20261004`; retorno específico no script `scripts/implantar-online-station-20261004.py --rollback`, com guardas do estado atual. Não usar o rollback speed anterior para desfazer esta implantação.

**Retorno Android novo lido:** commitdd6aff172761f40c9b8eea2d3e2c1c34a0c79034; R4 `17e9b87b` instalado, catálogo1.816/8 jogos locais, capas/cache/quatro workers e sinopses observados. Atualização recebida no servidor e272bfc / app0cdc1f3 confirma **R7 `82772343` instalado por atualização/hash conferido**, sem limpeza; botão de sala/retorno sem login observados. Seu404 ocorreu antes da habilitação11h29; falta Reconectar e conferência pós-publicação no telefone. Células de vídeo azuis são pendência visual do cliente. A fonte anterior1dc8c381 e o APKfa3bc844 nos blocos03/10 abaixo são históricos. **Partida real entre dois aparelhos ainda não comprovada**; o servidor social publicado não constitui essa prova. R7 preserva carregamento de quatro capas sem pausa normal. Nenhum APK foi instalado neste Linux.

Pedido posterior autorizado: mover N64 para a raiz do mesmo HD e reconhecer jogos/capas/metadados automaticamente por pasta; execução segue depois da publicação deste retorno. N64 ainda não consta desta fotografia. **As seções abaixo registram datas e estados anteriores.**

---

# Handoff técnico único: servidor, conexão e instalação do TurboStations Android

Atualizado em 03/10/2026, 19h57 (America/Maceio). Preserva as evidências anteriores identificadas abaixo. **Manter as próximas atualizações neste arquivo**, com data e prova; a equipe Android precisa de um único retorno para concluir o APK.

**Estado operacional atual:** API Station **`4bb77ed2`**, publicada às19h48, aceita o carregamento rápido de **quatro capas simultâneas**. Catálogo revisão **4 / 1.816 jogos**, com **todas as capas originais da pasta `revista`**. A prova HTTPS entregou48 capas exatas em4,55s e nove downloads íntegros;2.071 capas internas continuam cruzadas com revista. Administração profissional de licenças/aparelhos **deda92c** publicada em **https://turbobox.lzgames.com.br/admin/station**. O retorno Android `2834e3b` comprova APK **fa3bc844** instalado, fonte02c09dd. **O novo fonte rápido é `1dc8c381`; precisa entrar na nova compilação/assinatura do APK no ambiente canônico E:.** Java e JNI novos foram compilados neste Linux, com os testes descritos abaixo; nenhum APK novo foi montado ou instalado aqui. A recuperação de licença às16h47 e seu código vencido às17h17 são históricos. O fluxo visual/jogo no aparelho após essas alterações ainda precisa de prova Android; consulte o painel e gere código somente quando necessário.

## Quatro capas por vez e downloads contínuos — produção03/10/2026, 19h48

### Diagnóstico: originais corretos, espera artificial no app

As capas são **cópias byte a byte de `media/revista`**, sem recompilar, redimensionar ou trocar por `media/images`. Todas as1.816 públicas são **480×720**:1.815 JPEG e um PNG. Mediana131.522bytes, p95143.025bytes; o PNG de Mario Paint tem805.241bytes. As dimensões não são um erro identificado. O PNG naturalmente transfere mais bytes que os JPEGs.

A medição autenticada anterior à otimização, feita neste Linux, deu mediana **5,10ms na API local** e **351,66ms pelo HTTPS público**, com hash de cada imagem conferido. Leitura quente do arquivo mediana0,09ms. O fonte Android anterior tinha **um trabalhador de imagens**, bloqueios globais durante a transferência e **intervalo fixo de2,1s** entre pedidos. Para48 capas sem cache, só esse espaçamento introduzia47×2,1=**98,7s**. Esse cálculo não é uma medição do telefone.

### Implementação entregue para a nova compilação Android

Fonte completo **[`1dc8c381e49e60ccbe0f84c97dcfe5be3e1d85f3`](https://github.com/luziellacerda/TurboElden/commit/1dc8c381e49e60ccbe0f84c97dcfe5be3e1d85f3)**, branch `feat/station-transfer-speed-20261003`, `clientVersion=1.0.8-station-speed-20261003.2`. A comparação com a integração2834e3b confirmou a mesma base de runtime antes destas mudanças. Esse commit pode ser aplicado à integração Android com `git cherry-pick 1dc8c381e49e60ccbe0f84c97dcfe5be3e1d85f3`.

- **Quatro trabalhadores reais de capas**, alinhados ao limite de quatro da ponte nativa. Pedidos seguem as prioridades visíveis da tela; cada vaga é reutilizada após a conclusão, sem esperar um lote inteiro e sem temporizador fixo entre imagens.
- Cache privado por **`coverId + items[].revision`**: acerto local imediato, uma transferência por capa/revisão mesmo com pedidos duplicados, validação e publicação atômica. Capas diferentes transferem em paralelo. O cache da revisão3 não atende uma capa4.
- O coordenador captura sessão/item sob seu bloqueio e **libera o bloqueio antes de transferir a capa**. Capas não atrasam a autorização do jogo. O mesmo Bearer continua protegido entre autorização e GET de artefato até os cabeçalhos; o corpo do jogo segue junto das capas.
- Resposta HTTP integralmente consumida permite reuso da conexão TLS pelo pool da plataforma. Corpo incompleto, erro de leitura ou cancelamento desconecta. Pin/certificado/hostname, proibição de redirect e conferência de tamanho/SHA256 continuam ativos. O efeito do reuso na rede do aparelho ainda deve ser medido.
- Arquivo local com nome exato é conferido por tamanho/hash antes de percorrer outros candidatos; isso evita a varredura quando os bytes autorizados já estão presentes. SHA256 continua obrigatório.
- Atualização de catálogo e saída da tela cancelam as capas pendentes; uma conclusão anterior não entra na nova publicação. A ponte limpa resultados e libera vagas antigas mesmo quando um download adia a troca do catálogo nativo. Licença, Keystore, dados, jogos/saves, renderer e motores são preservados.
- `404` mantém a espera de repetição somente daquela capa; `429` suspende novos pedidos de rede por60s, sem bloquear uma thread nem impedir capas já em cache. **Essa espera só ocorre após erro; não há pausa normal entre capas.**

**Build no ambiente E:** na raiz canônica `E:\ESTUDO APK\work\turbostations-reconstruction-20261002`, aplicar o commit acima e executar a receita existente: `prepare_test_dependency.py` → `run_tests.py` → `build_module.py`; `build_archive.py` → **`build_frontend.py`**; `prepare_dex_input.py` → `link_native_services.py` → `build_app_dex.py` → `package_apk.py`. **A nova compilação precisa incluir Java/DEX e a nova `libstation_frontend.so`**, porque há alterações dos dois lados. Usar a base APK/Keystore originais e atualizar sem desinstalar ou limpar dados. Registrar SHA256/assinatura/instalação do novo APK; o hashfa3bc844 identifica a versão anterior.

**Provas de fonte:**329 verificações Java no host, incluindo27 novas de concorrência/cache/cancelamento/revisão/429/denegação tardia;7 da política C++ no host. Todas as classes Java compilaram para bytecodeJava8 contra AndroidAPI36. Frontend e fixture nativa compilaram **arm64/API26 com NDK27.1 e alinhamento16KiB**. O build canônico continua SDK34/NDKr28c conforme sua receita; o build Linux não substitui montagem/assinatura nem execução no aparelho. [Evidência Android](https://github.com/luziellacerda/TurboElden/blob/1dc8c381e49e60ccbe0f84c97dcfe5be3e1d85f3/versions/station-reconstruction-20261002/evidence/transfer-speed-validation-20261003.json).

### Servidor publicado e limites efetivos

API fonte **[`4bb77ed2b8fb01fe967b90dc18ec3fbd1ee5d58b`](https://github.com/luziellacerda/Servidor-pix/commit/4bb77ed2b8fb01fe967b90dc18ec3fbd1ee5d58b)**, DLL SHA256 **`b08f8313651a10de008d35545ff13569c5a360fb42ee792ad37f2647bd9d107e`**, PID168174. `ExecStart` efetivo: `/usr/bin/dotnet /opt/turborama-station-speed-20261003-4bb77ed2/TurboRamaSuiteOnlineServer.dll`. Índice4 permanece SHA256 **`c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a`**; IDs, catálogo, capas e jogos não mudaram nesta otimização.

Somente capas passaram a admitir **4.096pedidos/minuto por licença/aparelho autenticados**, com limite agregado de16.384/minuto por origem. Renovar sessão não reinicia o orçamento do aparelho; clientes atrás do mesmo NAT têm orçamento próprio. Assim o catálogo público inteiro cabe no orçamento de uma leitura rápida. Ativação, sessão, autorização de download e artefatos continuam nos limites anteriores de30pedidos/minuto por rota/origem. Esses limites contam pedidos, **não bytes por segundo**. Todo pedido de capa ainda consulta/valida a sessão no banco; capa sem Bearer recebe401. Nenhuma capa ou jogo ficou em cache público.

### Medições reais e limite da conclusão

| Prova | Resultado |
| --- | --- |
| Candidato isolado sob UID995 |48capas em80,41ms, quatro pedidos concorrentes; todas iguais aos arquivos revista |
| Produção, HTTPS público a partir deste Linux | **48capas/6.458.397bytes em4.546,55ms**, quatro pedidos concorrentes,48respostas200 e bytes exatos |
| Catálogo/perfil/grants assinados |1.816itens/revisão4; quatro plataformas e um ID oculto conferidos |
| Jogos em cada verificação candidato/produção | **9downloads200 íntegros e9reusos404**: cinco pares do contrato mais os maiores artefatos das quatro plataformas |
| Sem autenticação | Capa401; painel302 para login |
| Saúde | Ready200; os12 PIDs compartilhados acompanhados permaneceram iguais/ativos |

Download do jogo já é **streaming contínuo**, com leitura/gravação em blocos, sem espera programada nem teto de bytes/s no aplicativo/API. O Nginx efetivo não tem diretiva `limit_rate`/`limit_rate_after`; a rota de artefatos usa `proxy_buffering off` e `proxy_cache off`. Nesta tarefa nenhuma configuração de Nginx/Cloudflare/rede foi alterada.

Os maiores arquivos testados tinham2,84–6,29MB. No candidato local foram15,6–31,0ms; a medição pública anterior variou21,9–28,1Mb/s. Na verificação depois do lote de capas variou7,6–22,9Mb/s; a conferência pública sem o lote de capas variou16,2–22,4Mb/s, com os mesmos hashes. **Não há prova de aumento da banda externa nem de velocidade no telefone.** A API local serviu os bytes rapidamente; a variação pública inclui TLS, túnel/Cloudflare, internet e cliente de teste. A fonte nova retira a espera de capas, libera o início dos downloads e permite reuso TLS; a velocidade real final também depende da rede, armazenamento e processamento do aparelho.

Antes de afirmar o resultado visual, instalar o APK novo e conferir catálogo4 da rede, primeira carga de capas com até quatro pedidos em trânsito, scroll/cache/reabertura, download/cancelamento/hash/instalação, atualização de catálogo durante transferência, abrir jogo e voltar. A nova compilação foi autorizada pelo mantenedor. **Servidor publicado e fontes disponíveis; a montagem/assinatura/aparelho pertencem ao ambiente privado Android.**

### Evidência, preservação e retorno desta API

[Evidência sanitizada completa](evidencia-transferencia-station-20261003.json) contém antes/depois, tempos, hashes públicos e escopo das provas. O teste .NET preservou regressões Suite/ES; o HTTP com PostgreSQL temporário passou antes da publicação, tanto compilado da fonte quanto com a DLL empacotada. Verificou ativação, sessão, perfil/catálogo/grant,48capas em quatro conexões, raw/ZIP, interrupção/novo grant, uso único, outro aparelho/sessão, expiração, revogação, quatro plataformas e compatibilidade. Processos candidato e produção usaram licenças sintéticas descartáveis; **nenhuma licença real foi usada nem mensagem enviada**. Todos os registros sintéticos foram removidos.

Só a API Station foi reiniciada. Os12 serviços acompanhados — PIX, Suite/API/admin, gateway, Nginx, Cloudflare, PostgreSQL, Redis, FPM principal/TurboBox, administração Station/helper — permaneceram nos mesmos PIDs. Não houve migration, troca de chave, alteração de licença de cliente, catálogo, ROM, imagem, site/painel ou rede.

Backup privado root0700 **`/mnt/DADOS/station-speed-backup-20261003`**, contendo API anterior e configurações salvas; restauração temporária da API e manifesto de todos os arquivos conferidos antes da ativação. Release anterior fd13c0d e índice4 preservados. Acrescentado apenas `zz-station-speed-20261003.conf` para selecionar a nova API. O [script limitado](scripts/implantar-transferencia-station-20261003.py) confere commit limpo, manifesto/SHA256, identidade Linux, saúde e executa teste candidato antes de ativar.

**Retorno somente desta otimização:** com autenticação nativa Linux/root, executar **`python3 docs/station-android/scripts/implantar-transferencia-station-20261003.py --rollback` na raiz da release de fonte4bb77ed2**. O script remove apenas seu override, reinicia somente Station e recupera fd13c0d **mantendo catálogo/capas revisão4**. Confere os artefatos/configurações antes de mudar e pode retornar mesmo se a API nova estiver parada. Esse retorno não foi acionado porque a implantação passou. Após retornar, o limite antigo de30capas/minuto volta; o app rápido precisa da API nova para evitar429. O retorno de capas/revisão5 descrito na seção histórica abaixo é uma operação diferente; não é necessário para desfazer esta API.

## Capas da pasta revista: causa e correção publicadas — 03/10/2026, 18h53; conferidas até19h05

### Por que o app recebia outras imagens

O preparador anterior selecionava primeiro a tag **`<image>` do `gamelist.xml`**, normalmente apontando para `media/images`; só procurava `media/revista` quando essa imagem faltava. O relatório privado confirma **1.783 capas selecionadas pelo XML e apenas33 pela alternativa revista**. A API copiava e servia corretamente os bytes escolhidos pelo índice, mas aquela escolha não atendia à fonte pedida pelo mantenedor.

O verificador anterior também aceitava `sourceCoverMatch=yes` quando a capa coincidia **com a imagem XML ou qualquer candidata revista**. Portanto a conferência anterior provava uma imagem ligada ao jogo, sem exigir a imagem de revista. Essa condição foi corrigida com `sourceRevistaMatch`, hash da revista selecionada e o modo obrigatório `--require-revista`.

O índice efetivo revisão3 foi exportado privadamente e comparado com a preparação: **2.071/2.071 capas tinham bytes idênticos**. Entre os1.816 jogos visíveis, 33 já coincidiam com revista, 1.779 eram diferentes e quatro tinham variantes em subpastas; depois de escolher a variante pela pasta do ROM, os quatro também eram diferentes. **A prova pública anterior à mudança serviu nove capas200: todas iguais ao índice antigo, nenhuma igual à revista selecionada.** Isso comprova a seleção errada no servidor.

### Seleção implementada e resultado completo

A origem obrigatória agora é **`media/revista` da mesma plataforma**, com nome de arquivo igual ao ROM sem a extensão. A ordem é: mesma subpasta relativa do ROM; raiz de revista se não existe nessa subpasta; cópias em outras subpastas somente quando todas têm o mesmo SHA256. Conteúdos diferentes na pasta escolhida, imagem inválida, ausência ou escape por link impedem publicar. O nome exibido do jogo não é usado para aproximação. A imagem XML permanece apenas como referência no inventário.

| Caso com versões diferentes | Revista selecionada |
| --- | --- |
| Mario Paint | Raiz, correspondente ao ROM na raiz |
| Tom and Jerry | Raiz, correspondente ao ROM na raiz |
| Rockman & Forte | Subpasta PT-BR correspondente ao ROM |
| Top Gear 3000 (PT-BR) | Subpasta PT-BR correspondente ao ROM |

As2.071 entradas usam2.070 escolhas por pasta exata e uma pela raiz — EarthBound BR, sem revista na subpasta. **Todas as1.816 capas públicas são480×720 e coincidem byte a byte com a revista selecionada.** Foram substituídas **1.783 capas visíveis +252 de compatibilidade =2.035**; 36 já eram iguais. Mantidos todos os2.071 `itemId`/`coverId`, inclusive os996 publicados originalmente, nomes, plataformas,255 linhas ocultas e **todos os descritores/hashes dos ROMs**. As contagens continuam SNES644, SNESBR191, Mega Drive887 e Mega DriveBR94.

Ferramentas: [seleção por origem](scripts/station_revista.py), [correção mantendo IDs/ROMs](scripts/corrigir-capas-revista.py), [inventário](scripts/gerar-catalogo-midia.py), [conferência obrigatória](scripts/cruzar-indice-catalogo.py). O preparador de novos catálogos também usa revista como fonte obrigatória. **15 testes de regressão** verificam XML diferente, subpasta BR, ambiguidade, ausência, imagem inválida, links, alteração após inventário, preservação de IDs/ROMs/linhas ocultas e revisão crescente no retorno; os13 casos de conciliação também passaram.

### Como o Android deve consumir a correção

1. Com a licença/Keystore preservados, obter sessão válida e fazer **`GET https://app.lzgames.com.br/v1/station/catalog` com Bearer**. Validar o envelope assinado e os vínculos; o payload atual tem `revision=4`,1.816 itens e **`items[].revision=4`**. Usar os IDs dessa resposta.
2. Executar a atualização de catálogo no fluxo real: **`StationFrontend.refresh()` → `StationCoordinator.refresh()`**, e publicar/aplicar o novo catálogo no carrossel. No fonte02c09dd, apenas retornar do segundo plano chama `reconcile()`, que republica o catálogo em memória; esse evento sozinho não comprova consulta nova. O login/refresh do coordenador consulta a rede; cache de catálogo é alternativa apenas ao503 específico de catálogo não pronto.
3. Para cada capa visível, chamar **`GET /v1/station/covers/{coverId}` com o Bearer da sessão**. Receber bytes JPEG/PNG conforme `Content-Type`, sem redirect; validar e guardar em cache privado por **`coverId + revisão do item`**. `StationCoverStore` já usa `coverId-4.img`; `ExistingCoverCache` só importa uma capa cuja revisão seja exatamente4. Não existe `coverRevision` separado nem hash de capa no payload HTTP do catálogo; os hashes deste TSV são a referência de QA.
4. A aplicação do catálogo fresco deve substituir as referências de capa/textura anteriores. O fonte nativo examinado recria os itens e limpa a fila/repetição de capas ao aplicar o catálogo. Conferir visualmente no APK que uma capa da revisão3 não continua ligada ao item4. Não limpar os dados, remover licença/Keystore, reinstalar ou apagar jogos/saves para trocar capas.
5. Downloads continuam pelas mesmas rotas de autorização/artefato. Conferir **`itemRevision=4`**, o mesmo item/sessão e o descritor assinado; os hashes/bytes dos jogos são os anteriores. Se a autorização acusar revisão diferente de um catálogo3 em memória, atualizar o catálogo antes de solicitar outro download. O grant continua de uso único.

**QA do aparelho:** registrar catálogo4 recebido da rede, IDs e revisões; abrir as quatro variantes da tabela e uma capa por plataforma; comparar com `indexCoverSha256`/`sourceRevistaSha256` do TSV. Confirmar seleção/renderização e cache após reiniciar a sessão. **Nenhum APK foi alterado neste Linux; a prova atual é servidor/HTTPS, não renderização no aparelho.**

### Arquivos atuais para comparar e implementar

- **[Catálogo completo vigente](catalogo-conciliado-jogos-capas-downloads-20261003.tsv)**:1.816 linhas revisão4, nomes/IDs/plataformas, capa/MIME/tamanho/hash, todos os campos do download e proveniência. Todas com `catalogMatch=exact`, `sourceRevistaMatch=yes` e hashes de capa iguais à revista selecionada. SHA256 **`3542803f0887a3595bfcae6f2c1bd5e860b5ab81df32f308c2f3ac1fbd071f12`**.
- **[Auditoria de todas as capas](auditoria-capas-revista-20261003.tsv)**:2.071 linhas, públicas e ocultas, hash anterior/atual, escolha de revista e indicação de substituição. SHA256 **`5a51318baa4fdcbf75e13c7220471ee92719191199b2b27a81a430aecd29de39`**.
- **[Compatibilidade dos996 IDs](compatibilidade-ids-publicados-20261003.tsv)**:identidades preservadas, revisão4, hash de capa e conferência revista. SHA256 **`8ce7c0cf3c0388af1b82a606c13371129b305fbef29ea6d84be481d9702fedef`**.
- **[Inventário do HD](catalogo-disco-snes-megadrive-20261003.tsv)**:1.824 entradas XML;1.816 com ROM e seleção revista resolvida, oito sem ROM excluídas. Distingue imagem XML, variantes revista e **hash da revista escolhida**. SHA256 **`8a55d82bca04216b1c78bce2c55ea906eda6e2c9768dc5da49aa8277920f4696`**.
- **[Evidência sanitizada antes/depois](evidencia-capas-revista-station-20261003.json)**:amostras HTTP/hash, verificação integral, implantação, preservação e limites do APK. As listas acima foram atualizadas no mesmo caminho; hashes de revisão3 nas seções datadas anteriores identificam aquelas versões históricas.

### Implantação e retorno

Preparação/publicação executada a partir de **[`48124888f0b20dd6029e27dffbb4fdee408d2e61`](https://github.com/luziellacerda/Servidor-pix/commit/48124888f0b20dd6029e27dffbb4fdee408d2e61)**. API continua **fd13c0d**, DLL SHA256 **f305ae3763cb77a53a27b2c168c8de290191b3a7e88e612850856b6f7be7e639**. Apenas a Station foi reiniciada para carregar o novo índice; PID **154043**, início18h52:56, publicação/prova concluída18h53:16. Índice efetivo revisão4 SHA256 **`c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a`**.

Nova release privada somente de conteúdo, propriedade root/grupo do serviço, diretórios750/arquivos640; API sem escrita. **4.142 arquivos foram abertos e hasheados sob UID995**. Processo candidato em HTTP loopback e produção em HTTPS passaram: catálogo4 completo, nove capas exatas de revista, cinco downloads íntegros — quatro plataformas e um ID oculto,2raw/3ZIP — e cinco reusos negados404. A conferência final compara2.071 capas com revista e1.816 linhas com a resposta assinada; nenhum ID faltante. Todas as licenças/sessões/grants sintéticos foram removidos; nenhum cliente real foi usado no teste.

**Preservados:** os12 PIDs acompanhados de PIX/Suite/admin compartilhado/gateway/Nginx/Cloudflare/PostgreSQL/Redis/administração Station/helper/FPM principal/FPM TurboBox; binários, painel publicado, schema, chaves, rotas, portas e HD original. Nginx não precisou de recarga. A release revisão3 e seu índice permanecem intactos.

Backup privado **`/mnt/DADOS/station-revista-backup-20261003-rev4`**, root0700; o índice salvo foi restaurado em diretório temporário e seu hash conferido. A operação só acrescentou os arquivos próprios `station-covers-revista-20261003-rev4.env` e `zz-station-rev4-covers-20261003.conf`, selecionando o novo índice. Não houve migration nem restauração do banco.

Procedimento de retorno no [script limitado](scripts/implantar-capas-revista-20261003.py), com autenticação nativa Linux: **`python3 scripts/implantar-capas-revista-20261003.py --rollback`**, executado a partir desta pasta. O procedimento restaura **os bytes/IDs/ROMs da revisão3 com catálogo/itens revisão5**, para preservar a regra Android que recusa diminuição de revisão. Confere os artefatos/configurações próprios, mantém as releases e acrescenta índice/override de retorno; só reinicia a API Station. Essa política possui teste de regressão; **o retorno não foi acionado em produção nesta correção concluída com sucesso**. Não repetir `--apply` sobre a publicação pronta nem usar o rollout antigo fd13c0d para desfazer apenas capas.

## Administração de licenças e aparelhos publicada — 03/10/2026, 18h15; conferida até18h20

### Uso por quem atende o cliente

Abra **[Administração › Station](https://turbobox.lzgames.com.br/admin/station)** com sua conta administrativa do site. Busque o nome, o pedido ou a licença e clique em **Abrir atendimento**. O painel mostra situação, aparelho, último contato e histórico, com busca/filtros e paginação de20 clientes. Em celular, usa cartões com o botão de atendimento visível. Antes de alterar, mostra o efeito, pede motivo e **senha administrativa do site**. Não pede senha Linux/sudo ao operador.

| Situação | Ação disponível | Efeito |
| --- | --- | --- |
| Primeiro acesso, código vencido ou perdido, substituir o código | **Gerar novo código** | Código novo de30 minutos, uso único; invalida o anterior. |
| Reinstalou/desinstalou o aplicativo | **Cliente reinstalou o aplicativo** | Revoga a instalação anterior e gera código para reativar a mesma licença. |
| Comprou outro celular | **Trocar de celular** | Revoga o aparelho/sessões anteriores e gera código para o novo. Continua um aparelho por licença. |
| Apenas remover a autorização anterior | **Liberar outra ativação** | Deixa a licença pendente, sem gerar código. Emita quando o cliente estiver pronto. |
| Código entregue indevidamente | **Cancelar código emitido** | Invalida o código sem emitir outro. |
| Aparelho perdido/roubado ou necessidade de suspensão | **Bloquear acesso** | Suspende novas sessões e encerra as existentes. |
| Bloqueio resolvido, pagamento confirmado | **Desbloquear acesso** | Restaura a licença, preservando o vínculo do aparelho. |
| Renovar a sessão com a chave já salva no app | **Reconectar aplicativo** | Encerra a sessão; o mesmo aparelho pode abrir outra. |

As ações seguem o estado atual: licença já ativada precisa da liberação de reinstalação/troca; licença pendente permite emissão/cancelamento. Pagamento/entrega não confirmados não são liberados por este painel. O desbloqueio preserva a chave vinculada; quando houve reinstalação, faça a liberação depois. O painel não cria uma segunda licença/venda e não apaga jogos/saves no telefone. Desinstalar/limpar o app por conta própria pode apagar seus dados locais.

O código aparece após a confirmação, pode ser copiado e desaparece ao fechar/sair/atualizar. **Não existe consulta do código antigo.** Entregue-o por canal privado; se vencer enquanto a licença continuar pendente, emita outro. A opção WhatsApp começa desmarcada; quando solicitada, a página informa inclusão na fila existente, sem afirmar entrega. Abrir cadastro não envia mensagem.

Se a transferência concluir e a emissão falhar, o painel informa a conclusão parcial e orienta atualizar o cadastro e usar **Gerar novo código**. Se a resposta se perder ou houver conflito, confira o histórico antes de repetir. O identificador da operação e as gerações esperadas impedem reemissão/revogação silenciosa por repetição ou cadastro desatualizado.

**APK:** o campo de login recebe o novo código, não o ID `STA-`. O app cria e prova sua chave no desafio/complete; o painel gerencia a autorização dessa chave, sem extrair a chave privada do Android. Depois de ativado, deve usar licença/Keystore salvos para abrir sessão, consultar `/me` e obter catálogo/capas/downloads com o mesmo Bearer, como o contrato abaixo. Não desinstalar para testar a reconexão.

### Fonte, implantação e escopo

- Fonte do painel/backend/helper: **[`deda92ca3c575f6f367053e9c8cbd6226128b879`](https://github.com/luziellacerda/Servidor-pix/commit/deda92ca3c575f6f367053e9c8cbd6226128b879)**. [Guia e publicação](../../ops/station-admin/README.md). Cinco arquivos Station do site foram publicados; autenticação, banco SQLite, router e demais páginas existentes foram preservados.
- Nova unidade **`turborama-station-management.service`**, PID140743, socket Unix privado, UID994. DLL em `/opt/turborama-station-management-20261003-deda92c/backend`, SHA256 **`15b5a2c94562f8a915c1cb46d7c2b4e3246febdc438473992931b93f88684260`**. `STATION_MANAGEMENT_ONLY=1`: rotas de outros produtos retornam404, mesmo com credencial interna válida. Prontidão200 conferida.
- Helper existente **`turborama-station-issue-admin.service`**, loopback5194, PID140751, passou a encaminhar gestão ao socket privado. SHA256 **`32f03a3a34e8be106fd91bd1b85dcdc5f45ff55cb719c4d93ab1932125b6c01b`**. Mantém `/licenses`, reemissão humana30min e emissão comercial48h. A unidade compartilhada Suite administrativa não foi substituída/reiniciada.
- Migration aditiva **030**, SHA256 **`631b6737b19804a26a5e5bc4fb95a62070ad72d2b7bd0fe70c71009d57636e6d`**: recibos de emissão sem código e view de histórico filtrada por produto Station. Concede leitura da view apenas ao role administrativo, sem conceder acesso à tabela geral. A produção já possuía leitura geral de auditoria no role administrativo; a migration não altera esse privilégio existente. Conferência real: role da API sem leitura da view e dos recibos; nenhum outro produto na view.
- Credencial de ativação entregue à nova unidade com **`LoadCredential`**, sem mudar chave/ACL da API. O backend valida leitura/formato antes de abrir o socket. Senha/CSRF, motivo, gerações, sessão alvo, rate limit e identificador da solicitação são conferidos; as alterações possuem transação, recibo e auditoria. PostgreSQL guarda verificador/HMAC, prazo e metadados, não o código recuperável; envio opcional preserva a fila privada existente.

### Provas e retorno

[Evidência sanitizada do painel](evidencia-painel-administrativo-station-20261003.json). A DLL final foi exercitada com PostgreSQL temporário, roles reais, helper, PHP protegido e Chrome desktop1440/mobile390. Passaram senha incorreta/CSRF negados, ações por estado, código sem armazenamento em `localStorage`/`sessionStorage`, código ocultado ao fechar, exibição do efeito, ausência de erro JavaScript e WhatsApp opt-in. Autotestes administrativos e de conteúdo também passaram. Não se publicou fixture de login.

**Produção:** licença sintética verificada pelo helper instalado e API **HTTPS real**. Geração/cancelamento, contrato30min/48h, transferência, reinstalação, bloqueio/desbloqueio/reconexão e histórico passaram. Foram ativadas **três chaves distintas na mesma licença**, com provas/assinaturas, recusa de chave e sessão anteriores e recusa de pedido repetido. Perfil200 conferido após nova ativação. Dados sintéticos de licença/aparelhos/desafios/sessões/auditoria/recibos foram removidos com marcador de propriedade. Nenhum comprador real alterado e nenhum WhatsApp enviado. Administrador anônimo redireciona ao login; CSS/JS públicos `v=20261003-2` coincidem em hash com os arquivos instalados.

O navegador autenticado foi testado em cópia privada com os mesmos arquivos. Não foi feita impersonação/login como um administrador real em produção. A integração de pagamento real do provedor e o aceite completo do APK ainda dependem de provas próprias; o teste de emissão48h usou entrega sintética.

**Backup/retorno:** dump completo0600 e arquivos originais em `/mnt/DADOS/station-admin-panel-backup-20261003`, diretório0700. Dump restaurado em PostgreSQL temporário antes da alteração. A primeira tentativa parou porque o usuário administrativo não lia o pepper original; acionou retorno da página/helper, preservou o schema aditivo e removeu seus testes. A correção com `LoadCredential` foi compilada/testada e publicada usando a preparação conferida, sem reaplicar030. O retorno agora também espera a saúde do helper anterior após reiniciar.

Retorno autorizado por autenticação nativa Linux: `python3 ops/station-admin/deploy.py --rollback`. Restaura os cinco arquivos Station e o helper anterior, encerra/remove a unidade/configuração própria. Preserva migrations aditivas, release, backup e ações já concluídas; não sobrescreve o banco inteiro nem revoga ações reais de operadores.

**Preservação comprovada:** os dez PIDs acompanhados de API Station/PIX/Suite/admin compartilhado/gateway/Nginx/Cloudflare/PostgreSQL/Redis/FPM permaneceram iguais; apenas o helper Station foi reiniciado e a nova unidade adicionada. FPM não precisou de recarga. API5192 continua PID88965/DLL **f305ae37**, índice SHA256 **5b460a6f**, revisão3/1816. APK, conteúdo, chaves da API, Nginx, portas públicas e serviços dos outros produtos não foram alterados pelo painel.

## Retorno ao app 2834e3b: ativação diagnosticada e transferência concluída — 03/10/2026, 16h47

### APK recebido e causa comprovada da recusa

Lido integralmente o [retorno de integração Android](https://github.com/luziellacerda/TurboElden/blob/2834e3b101ce4e957414bccd13154c8a70af2f01/docs/server/INTEGRACAO-APP-PRODUCAO-STATION-20261003.md) e suas evidências. O APK SHA256 **`fa3bc84425120803d7e90069be3c296a91dbe04146455063f12d9a6205bbf795`**, pacote `org.turboramastation.frontend`, foi instalado às16h17:03; o hash instalado foi conferido pela equipe Android. Fonte02c09dd, 302 verificações Java/API34, 28 da ponte JNI no Android e 7 da política de capas no Android. Essas provas vêm do retorno publicado; não houve build, instalação ou ADB neste Linux.

O journal da Station confirma `POST /v1/station/activations/challenge`403 na janela das **16h19:13**; o app identifica a correlação **`1354b75f239f4fd7be8761df5efd7f8e`**. A consulta privada leu a licença exata do handoff de ativação, sem copiar código/identificação para o Git. Antes de qualquer escrita, constatou:

- código anterior corresponde ao verificador armazenado, mas **`activation_consumed=true`**;
- licença **ACTIVE/LIFETIME**, sem expiração, entrega **PAID/PROVISIONED**, um aparelho permitido;
- estado **BOUND**, com uma chave ativa do Samsung SM-A566E cadastrada em01/10, anterior à desinstalação confirmada pelo mantenedor;
- prazo do código ainda era válido na hora da tentativa; o bloqueio não foi expiração nem suspensão financeira.

No código implantado, `FindActivationAsync` exige código não consumido e `PENDING_ENROLLMENT`; esses dois requisitos estavam violados e o serviço devolve **`STATION_ACTIVATION_INVALID`403**. O journal não contém o corpo/identidade da tentativa: a associação à correlação vem do retorno Android e a causa da inelegibilidade vem da consulta privada e do fonte efetivo. A reinstalação não recupera a chave apagada do Keystore; repetir o código antigo não resolve o vínculo.

### Ação administrativa executada e provas

O mantenedor autorizou executar a correção. Foi usado o procedimento oficial **`/station/licenses/{id}/actions/transfer`**, com geração esperada0, motivo, ator administrativo e recibo/auditoria. O código administrativo da branch foi publicado para uso pontual; DLL SHA256 **`dae8a97fe6453b965a52066f66a7475423af4bd30ad091c00c6794fd0ec93c7d`**. A execução ocorreu em processo temporário, **socket Unix privado**, usando o usuário/role PostgreSQL administrativo existente, após autenticação nativa do Linux. Acesso sem token e POST sem controle CSRF foram negados antes da ação. O processo/socket temporários foram encerrados/removidos; não foi instalado outro serviço nem atualizada a administração dos demais produtos.

**Resultado às16h47:36:** mesma licença **ACTIVE/PENDING_ENROLLMENT**, geração de revogação1; dispositivo anterior **REVOKED**, nenhum dispositivo ou sessão anterior ativo. `STATION_TRANSFER` e recibo `STATION_ANDROID/TRANSFER/SUCCESS` foram conferidos no banco. Não foi criada outra licença, alterado o pagamento ou removida a verificação de prova/aparelho.

O helper Station existente na5194 emitiu novo código de uso único pela ação administrativa **`issue-code`**, com auditoria. Geração de ativação7, código não consumido, validade30 minutos: **03/10/2026 17h17:36 America/Maceio / 20h17:36 UTC**. O segredo foi salvo em arquivo local privado0600, fora dos repositórios, e apresentado em janela privada do Linux ao mantenedor. **Nenhum código, token, identificador de licença/aparelho ou nome pessoal foi incluído neste retorno.**

[Evidência sanitizada da recuperação](evidencia-recuperacao-ativacao-station-20261003.json): estado, hashes, controles, auditoria e limites. Snapshot privado dos registros afetados foi feito antes da transferência. As tentativas anteriores pararam no preflight sem transferir ou emitir código; seus registros privados foram preservados. O processo final executou a transferência uma vez.

Conferência antes/depois: **outras licenças idênticas**, todos os dez PIDs de serviços acompanhados iguais, índice/hash/revisão de conteúdo inalterados. API Station continua fd13c0d; catálogo3/1816, migrations, Nginx e motores não foram alterados por esta recuperação.

### Próxima ação do Android

1. No APK **fa3bc844 já instalado**, preencher **o novo código recebido privadamente**, concluir desafio e prova com a chave atual do aparelho e conferir `/me`. Não usar código antigo nem identificação `STA-` como senha; não desinstalar ou limpar os dados.
2. Capturar status/correlação das duas etapas de ativação, sessão, perfil e catálogo; conferir revisão3 e contagens644/191/887/94. O servidor agora permite matrícula; **não há prova de ativação desse APK após a transferência** neste Linux.
3. Confirmar capa200/renderização/cache, autorização/GET com a mesma sessão, bytes/hash/recibo, cancelamento, abrir jogo e voltar. Registrar um raw e um ZIP. Preservar jogos/saves e assinatura.
4. Se o prazo terminar antes do uso e a licença continuar pendente, fazer **reemissão administrativa da mesma licença** pelo canal privado; não criar segunda licença nem repetir a transferência. Se já estiver BOUND, retomar por licença/Keystore salvo.

O fluxo administrativo foi concluído; o aceite no aparelho continua pendente. Não promover o APK a estável antes dessas provas.


## Produção corrigida e catálogo integral conciliado — 03/10/2026, 15h21

### Causa comprovada e correção aplicada

A exportação privada do índice efetivo confirmou revisão 1, 996 registros e SHA256 `5b3f881479a388694b4b34e33e406770885d0203fa588e5c50ff40d5496fbdf8`. Executar a leitura sob **UID 995, com os grupos reais da unidade**, produziu `PermissionError` em **996/996 ROMs e 996/996 capas**; root e o mantenedor conseguiam ler esses arquivos. Essa é a causa comprovada de conteúdo publicado e inacessível no binário antigo.

O mesmo cruzamento revelou 255 IDs extras para arquivos já representados, 357 registros com plataforma/classificação diferente da origem e 99 nomes diferentes da entrada XML. Os 99 registros publicados como Game Gear/GB/GBC/GBA/32X apontavam todos para arquivos das fontes SNES/Mega Drive; 163 registros SNES/BR também apontavam para Mega Drive. Portanto aqueles 99 não comprovavam acervo de outras plataformas. A correção usa **caminho de origem exato**, mapa de proveniência e artefatos/capas revalidados; não associa jogos por título aproximado nem usa hash entre consoles como fallback.

**IDs preservados:** 741 IDs publicados representam jogos canônicos, 255 permanecem como entradas privadas de compatibilidade (`catalogVisible=false`) e 1.075 jogos receberam IDs novos. São **2.071 entradas internas / 1.816 jogos na resposta assinada**. Todos os 996 `itemId` e `coverId` anteriores foram preservados. Compatibilidade oculta não aparece no carrossel e continua autorizável com licença/sessão válidas; seu grant mantém o ID solicitado e usa o descritor correto. `catalogVisible` pertence ao índice privado, não ao JSON enviado ao APK.

| Plataforma HTTP exata | Jogos / capas publicadas | Conferência integral |
| --- | ---: | --- |
| `snes` | 644 / 644 | XML, ROM, capa e catálogo assinado coincidem |
| `snesbr` | 191 / 191 | XML, ROM, capa e catálogo assinado coincidem |
| `megadrive` | 887 / 887 | XML, ROM, capa e catálogo assinado coincidem |
| `megadrivebr` | 94 / 94 | XML, ROM, capa e catálogo assinado coincidem |
| **Total** | **1.816 / 1.816** | **835 SNES + 981 Mega Drive**, incluindo BR |

Os 8 registros XML sem ROM ficam excluídos. A lista histórica de 12.346 nomes/36 rotas continua como referência, sem comprovar novas mídias. O catálogo v1 não foi truncado: 1.816 públicos e 2.071 internos cabem no limite de 4.096.

### Listas completas para implementar e comparar no APK

Este bloco descreve a publicação revisão3 das15h21. Os arquivos nos mesmos caminhos foram atualizados para revisão4; seus hashes e as capas de revista vigentes estão na seção inicial.

- **[Catálogo conciliado: nomes, IDs, capas e downloads](catalogo-conciliado-jogos-capas-downloads-20261003.tsv)** — 1.816 linhas, ordenadas por plataforma/nome. Inclui revisão, IDs, todos os campos do descritor, hashes da ROM/capa, MIME/tamanho, proveniência XML e tags de diagnóstico. Todas as linhas têm `catalogMatch=exact`, `sourceNameMatch=yes`, `sourceRomMatch=yes`, `sourceCoverMatch=yes`; zero IDs da resposta HTTPS faltantes. SHA256 **`bcd8bce18ac48f9eef4c506b071e559de1a3465d3e6892e9ff3538b7c1f9f39a`**.
- **[Compatibilidade dos 996 IDs publicados](compatibilidade-ids-publicados-20261003.tsv)** — nome/plataforma anteriores e corrigidos, revisão 3, `coverId` preservado, visibilidade, ID canônico, hashes e tags. Usar para compreender seleção/cache anteriores; a lista vigente vem de `/catalog`. SHA256 **`07736677fd290da1359be4d30057e2ccf004dc90e1286f58940588ce969c336d`**.
- **[Evidência sanitizada da implantação](evidencia-rollout-station-20261003.json)** — versão, hashes, permissões, provas HTTPS, preservação dos serviços e limites do teste Android. Não contém credenciais, caminhos privados de mídia ou payloads com identidade.

### Release, permissões, HTTPS e retorno

Código API **[fd13c0d27eaab6a4dcf931a9cd64c3dcd7dd50c4](https://github.com/luziellacerda/Servidor-pix/commit/fd13c0d27eaab6a4dcf931a9cd64c3dcd7dd50c4)**; DLL efetiva SHA256 **`f305ae3763cb77a53a27b2c168c8de290191b3a7e88e612850856b6f7be7e639`**; TAR SHA256 `518443c9f7c5e10289d99dc22b5ed98e4e29d1c541c65102b796ade8072648d4`. Metadado de origem conferido. Índice efetivo SHA256 **`5b460a6f9866e30a5a5b4dad24652c512b1187b3df01244e6af9308ae6b18342`**. A release privada é propriedade de root, com leitura para o grupo do serviço e sem escrita da API. **4.142 arquivos (2.071 ROMs + 2.071 capas, incluindo compatibilidade) foram abertos e verificados por SHA256 sob UID 995.** Os arquivos do HD original permanecem preservados.

Conferido no PostgreSQL real: migrations 028/029, tabela de grants e permissões SELECT/INSERT/UPDATE da API; chave de downloads já existia com 32 bytes e era legível pelo serviço. **Nenhuma migration/chave foi aplicada ou trocada.** Backup privado de índice/configuração/release e dump completo do banco foi conferido; o dump foi restaurado integralmente em PostgreSQL temporário, com guardas contra uso do cluster de produção.

O primeiro teste público detectou que o Nginx substituía `X-Correlation-ID` por `$request_id`; a API respondeu 200, mas a verificação do eco falhou. O retorno automático restaurou DLL/índice anteriores, comprovando esse caminho de retorno. Em seguida foram alteradas **duas linhas somente no snippet Station**, encaminhando `$http_x_correlation_id`; a API valida o header e gera um ID quando ausente/inválido. `nginx -t` e reload passaram. Roteamento, limites, autorização e demais produtos permaneceram com sua configuração anterior.

**Prova pública por `https://app.lzgames.com.br`:** as nove rotas passaram com licença/aparelho sintéticos; ativação, sessão, perfil e catálogo 200 com assinatura/identidade verificadas. Catálogo 3/1.816 foi comparado integralmente com o índice. Foram servidos **cinco pares capa/download**: um de cada plataforma e um ID antigo oculto; **2 raw e 3 ZIP**. MIME, tamanho, `Content-Length`, SHA256, descritor assinado, sessão e ID coincidiram. Cinco tentativas de reutilizar grants devolveram 404 `STATION_GRANT_NOT_FOUND`. O eco de correlação também passou em HTTPS. Todas as licenças/challenges/sessões/grants sintéticos foram removidos. A sonda sem Bearer continua 401 e `/ready/station` local 200; essas sondas adicionais não substituem o teste autenticado.

A operação reiniciou somente `turborama-station-api.service`; o reload do Nginx manteve seu processo principal. PIX, Suite 5190, gateway 5191, helper 5194, cloudflared, PostgreSQL e Redis permaneceram ativos com os mesmos PIDs principais. Script limitado ao artefato e alvo concretos: [implantar-station-20261003.py](scripts/implantar-station-20261003.py); verificador de licença temporária: [verificar-http-release-station.py](scripts/verificar-http-release-station.py). **Não repetir `--apply`/`--activate-prepared`: já executados.** Para retorno operacional autorizado, o comando `pkexec /usr/bin/python3 /mnt/DADOS/servidor-pix-station-artifact-descriptor-20261002/docs/station-android/scripts/implantar-station-20261003.py --rollback` remove o override Station, restaura as duas linhas do snippet e volta à DLL/índice anteriores; as releases anteriores e os backups privados foram preservados. Não há migration a desfazer.

### Ação restante do Android

Consumir **catálogo fresco revisão 3**, com as quatro plataformas acima, usando `itemId`, `coverId` e revisões exatos. Aplicar fonte **02c09dd** com as correções APP-01..14, mantendo host/pin/chave pública/Keystore/assinatura. Compilar no ambiente canônico, atualizar o APK preservando dados e comprovar: carrossel 1.816, capas visíveis, download e cancelamento, instalação/recibo, abrir jogo e voltar. O teste HTTP usa um cliente de protocolo sintético; **não comprova execução do APK, UI, instalador Android ou emulador**. Não há ADB neste ambiente Linux; nenhum APK novo foi gerado/instalado aqui.

## Revisão do retorno do app e correções de fonte — evidência anterior ao rollout

Foram lidos o retorno `RETORNO-APP-FECHAMENTO-STATION-20261003.md`, o pedido servidor `e3050210` e o handoff integral/appendice do Android `db68b613cda008052afef8152400b9c595dfcffa`; os fontes auditados são do runtime `629a55a8cf48722460007944cf0bb737e9f8fb75`. A revisão foi cruzada com a DLL efetiva da 5192, o código candidato e o volume indicado. O APK instalado é **f5b35419fcff4188b8e86690045371892c77933e7f8edfc07bce1d5bd25d418a**, 1.902.718.870 bytes, pacote `org.turboramastation.frontend`, versionCode 11, versionName `1.0.8-turboeden-unico`, instalado 12:27:01. A evidência 12:29:44 confirma sessão/perfil/catálogo 200, **996 itens frescos da rede**, capa 404 `STATION_COVER_NOT_FOUND`, autorização 404 `STATION_ITEM_NOT_FOUND` e nenhuma transferência iniciada. O alias Mega Drive BR já foi incorporado. Esses fatos substituem as dúvidas anteriores sobre cache e integração do alias.

**Divergência localizada entre versões:** o fonte identificado pelo metadado `bbd07fd` da DLL antiga permite publicar entradas sem ROM/capa acessível e devolver 404 ao autorizá-las. O código candidato comparado pelo Android distingue ID ausente 404 de artefato indisponível 503; a DLL antiga não usa essa distinção. No recorte anterior às 14h12 não estava localizado o primeiro **ID/caminho** divergente: o catálogo autenticado completo, o índice efetivo e os hashes de seleção/requisição ainda não foram obtidos juntos. O índice exportado posteriormente confirmou falha de leitura em todos os 996 registros; ver a prova do rollout acima. Não atribuir os404 à extração, ao emulador, à corrida de sessão ou somente ao texto do cliente.

### Entregas de fonte e prova

- **Android:** correções em [645e7c44b74d10fb0494a650242de88e64cdcca7](https://github.com/luziellacerda/TurboElden/commit/645e7c44b74d10fb0494a650242de88e64cdcca7), com [revisão final 02c09dd36fcfa6c69ceb481f0934e84eef01e5ae](https://github.com/luziellacerda/TurboElden/commit/02c09dd36fcfa6c69ceb481f0934e84eef01e5ae) na mesma branch. A revisão final registra somente a correlação UUID gerada no cliente, evitando copiar texto de header recebido, branch `feat/station-review-fixes-20261003`, derivada de `db68b613`. [Resultado com hashes](https://github.com/luziellacerda/TurboElden/blob/02c09dd36fcfa6c69ceb481f0934e84eef01e5ae/versions/station-reconstruction-20261002/evidence/review-fixes-validation-20261003.json): 302 verificações em seis testes Java no host e 7 da política de repetição de capa em C++. Os dois cenários concorrentes passaram: capa não renova enquanto o GET aguarda cabeçalhos; após 200/cabeçalhos válidos, pode renovar durante leitura do corpo e a instalação termina com hash correto. **Não foi compilada a ponte Android/NDK nem gerado/instalado APK novo**; a base privada/assinatura/build E: e o aparelho não estão disponíveis neste ambiente Linux. O hash f5b35419 identifica o APK anterior às correções.
- **Servidor/ferramentas:** [commit e1ac9039c3825de3685529485e387a3270bc43b1](https://github.com/luziellacerda/Servidor-pix/commit/e1ac9039c3825de3685529485e387a3270bc43b1). Compilação Release sem erros/avisos; suíte .NET e HTTP isolado aprovados. 10 casos do conciliador passaram, cobrindo BR com ID preservado, hash/caminho, ambiguidade, outra família, revisão regressiva, capa ausente e metadados antigos. Exercício com o acervo real preservou todos os 1.816 IDs candidatos ao reclassificar 191 SNES BR e 94 Mega Drive BR; **não usou o índice de produção**. A candidata atual adiciona traces sanitizados à release anterior; dados criptográficos, sessão/grant, produto e rotas continuam verificados.

### Release anterior e1ac903 e fechamento do teste administrativo isolado

A release API do commit e1ac903 está em `/mnt/DADOS/station-api-release-candidate-20261003-e1ac903/`: DLL SHA256 `39cf1f9220d0a6753cae8c9e833900c74dab825c9dfd766970eeb14417ff58f7`, TAR `352d4b6cd8499b7b2ad0f6740cc68d9a73ed31f2f9f7bb38c7b0b783b577e622`. Seu metadado de origem foi conferido. A execução com **essa DLL empacotada** e o índice materializado produziu catálogo assinado de 1.818 itens, capa/download/hash corretos nas quatro categorias e `STATION CORRELATION: OK` / `STATION HTTP SMOKE: OK`.

Foi acrescentado o teste opcional `STATION_HTTP_ADMIN_DLL`, usando helper candidato em socket Unix temporário e o **mesmo PostgreSQL isolado**, com roles próprias API/admin e credenciais sintéticas. Resultado `STATION ADMIN HTTP: OK`: evento comercial pago e repetição idempotente; emissão de código; ativação/sessão reais no fixture; negação de token/claim/CSRF; bloqueio e invalidação de sessão/grant; conflito de geração antiga; desbloqueio; revogação de sessão; transferência e revogação do aparelho antigo; reemissão/ativação de outro aparelho; suspensão financeira impedindo desbloqueio. Nenhuma ação atingiu comprador, banco ou helper de produção. O teste recusa execução fora do cluster temporário e essa recusa também foi verificada.

Helper candidato `/mnt/DADOS/station-admin-release-candidate-20261003-e1ac903/TurboRamaSuiteAdminServer.dll`, SHA256 `82de901357deab184aee850fd0de6176bbd2e17f6cf46640f597f808dbc04e0e`; não implantado na 5194. A validação pelo painel real/provedor e a versão/configuração protegida da 5194 continuam pendentes. TTL real 60/180 s foi provado anteriormente na DLL 45fe4df; a candidata e1ac903 preserva essa lógica, mas não repetiu a espera real.

```text
STATION_HTTP_API_DLL=/mnt/DADOS/station-api-release-candidate-20261003-e1ac903/TurboRamaSuiteOnlineServer.dll \
STATION_HTTP_ADMIN_DLL=/mnt/DADOS/station-admin-release-candidate-20261003-e1ac903/TurboRamaSuiteAdminServer.dll \
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

### Resposta individual aos 14 achados do app

Referências `A/` = `versions/station-reconstruction-20261002/src/java/org/emulationstation/frontend/station/`; `N/` = `versions/station-reconstruction-20261002/src/native/`, no commit Android 02c09dd. Referências servidor = e1ac903 para a revisão inicial, fd13c0d para produção.

| Achado | Comportamento cruzado e alteração | Prova / limite restante |
| --- | --- | --- |
| APP-01 | `N/station_frontend.cpp:72`/`station_cover_retry.hpp`: a falha de capa deixa de ser permanente. Reentra após 60 s, somente nas prioridades visíveis; falha JNI espera 5 s. Sucesso/republicação limpa o estado. `A/StationFrontend.java:82` devolve conclusão vazia também ao entrar em segundo plano, evitando `coverPending` preso. | 7 verificações da política C++ passaram. O ABI de `Item`/`Catalog` não foi alterado. A ponte transmite sucesso/caminho vazio, sem causa detalhada; testar prioridade, cancelamento, 404/429 e textura no Android. |
| APP-02 | `A/StationDownloads.java:33`, `StationCoordinator.java:47`, `StationCoverStore.java:46`, `StationApi.java:196`: mesma coordenação protege authorize→scanner→GET até cabeçalhos válidos. Capa usa a sessão fornecida; não a renova por conta própria. Corpo/instalação ficam fora do lock. | Dois testes com relógio controlado e barreiras HTTP passaram, inclusive renovação durante o corpo sem bloquear o download. Não foi causa demonstrada dos404 do APK; nenhuma transferência dele começou. |
| APP-03 | `A/StationPublication.java:15` e `StationFrontend.java:51`: catálogo assinado completo permanece na coordenação; itens com mapa verificado são publicados, desconhecidos são contados por plataforma e avisados na UI. Catálogo só de plataformas desconhecidas gera erro explícito. `StationPlatforms.java` adiciona `gamecube`, `psp`, `pspbr`, `ps2`, `ps2br`, `psvita` sobre pastas já documentadas. | Testes de catálogo misto, aviso/contagem e nenhuma pasta inferida passaram; 1.816 itens conhecidos continuam sem corte. `model2`/`sufami` precisam de mapa confirmado. Aviso Android ainda não exercitado; histograma HTTP de produção agora confirmado nas quatro plataformas do rollout. |
| APP-04 | `StationCatalog`/`StationProtocol`, `StationLibrary`:4096 itens e12 MiB continuam sendo limites do v1, sem paginação. | Produção tem 1.816 jogos públicos / 2.071 entradas internas, incluindo 255 IDs de compatibilidade;12.346 nomes históricos não podem ser publicados pelo v1. Paginação exige contrato/cliente novos e acervo real das demais plataformas; não foi implementada nem usada truncagem. |
| APP-05 | `A/StationDiagnostics.java:24`, `StationHttp.java`, `StationCoordinator.java` e `StationDownloads.java`; servidor `StationRequestDiagnostics.cs`/`StationEndpoints.cs`: cada HTTP usa `X-Correlation-ID`; logs associam SHA256 de `itemId`/`coverId` e revisão. Autorização e consumo registram o item efetivamente recebido/resolvido no servidor. | HTTP isolado confirmou eco do header, tags exatas e ausência de credenciais/caminhos nos traces. Java testou escopo, descarte de header inválido e observador que falha. TSV cruzado inclui as tags. Captura conjunta app/5192 ainda exige as duas revisões instaladas. |
| APP-06 | `A/StationConfig.java:12`: `clientVersion` agora `1.0.8-station-review-20261003.1`, dentro do limite 64 do servidor e dos bytes assinados. Pin/autoridade atuais continuam conferidos. | O APK f5 ainda envia 1. A identificação nova vale após build; não é o hash do APK. Rotação coordenada de certificado/autoridade permanece pendente de segunda chave/pin aprovados, sem aceitar chave enviada pelo servidor ou desabilitar TLS. |
| APP-07 | `conciliar-indices-station.py`: preserva ID na mesma família de console, revalida descritores/capas e exige revisão superior a todas as revisões globais/individuais; registra migrações BR. Cache continua por `coverId`+revisão. | 13 casos do conciliador passaram. O modo explícito de correção aceita outra classificação somente por caminho de origem verificado, preserva os 996 IDs e oculta duplicatas; produção usa revisão 3. Ainda é obrigatório incremento quando bytes mudarem; trocar bytes mantendo ID/revisão viola o cache. |
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
| POST `/v1/station/activations/challenge` | `StationApi.activate`: código Base64URL de32 bytes, SPKI, identidade; desafio assinado60 s. | HTTPS200 sintético no rollout; expiração passou isolada. APK f5 não repetiu ativação no recorte antigo. |
| POST `/v1/station/activations/complete` | Prova RSA-PSS do Keystore sobre desafio/nonce; guardar `licenseId` verificado. `STA-` identifica licença. | HTTPS200 no rollout, assinatura e challenge/nonce conferidos; preservar licença/Keystore do aparelho. |
| POST `/v1/station/challenges` | `openSession`: licença/aparelho; conferir nonce/challenge assinado60 s. | HTTPS200 no rollout; cliente retoma acesso salvo. |
| POST `/v1/station/sessions` | Prova do mesmo aparelho; sessão/Bearer180 s; renovar mantendo coerência com catálogo/grants. | HTTPS200 no rollout e200 no APK antes dele; TTL real comprovado isolado. |
| GET `/v1/station/me` | `profile` com Bearer; ler `displayName` do payload verificado. | HTTPS200 assinado no rollout; atualização/cache503 testados no fonte Android. |
| GET `/v1/station/catalog` | Ler `revision` e cada `itemId`, `name`, `platform`, `revision`, `coverId`; comparar a sessão assinada. | HTTPS200/revisão 3/1.816 conferido integralmente. O total996 do APK é anterior ao rollout. |
| GET `/v1/station/covers/{coverId}` | ID/revisão do catálogo; MIME/assinatura de imagem, até5 MiB; cache e repetição limitada. | Cinco HTTPS200 com MIME/bytes/hash conferidos;404 negativo isolado. UI do APK após rollout ainda pendente. |
| POST `/v1/station/downloads/authorize` | ID exato e identidade; conferir item/revisão e todos os campos de `artifact` no grant assinado60 s. | Cinco HTTPS200 com descritor e identidade exatos, incluindo ID oculto de compatibilidade. O404 do APK é do estado anterior. |
| GET `/v1/station/artifacts/{grantId}` | Mesmo Bearer do grant, uso único, sem Range/redirect; conferir `Content-Length`/SHA256 antes de instalar. | Cinco HTTPS200 completos, 2 raw/3 ZIP; cinco reusos404. Instalação/aparelho e cancelamento no APK após rollout pendentes. |

### Como cruzar a próxima tentativa com o índice real

1. Compilar a fonte Android 02c09dd; a API fd13c0d já está na 5192 com conteúdo conciliado e traces. A correlação não concede acesso nem muda assinatura; nenhum `grantId`, Bearer, licença, nome pessoal ou caminho é registrado por esse código.
2. Relacionar `correlation` do app e servidor. `itemTag = SHA256(UTF-8(itemId))`, `coverTag = SHA256(UTF-8(coverId))`, hex minúsculo 64 caracteres. O trace do servidor usa somente o ID recebido/resolvido; o app usa seleção do item do catálogo. Comparar com `diagnosticItemTag`/`diagnosticCoverTag` no TSV gerado a partir do **índice efetivo**. O TSV conciliado atual usa os IDs reais publicados; a tabela de compatibilidade preserva os antigos.
3. Comparar, na mesma sessão, resposta autenticada fresca, item selecionado, pedido emitido, registro carregado e leitura de ROM/capa pelo UID 995. Se a tag do pedido divergir da seleção, corrigir cliente; se não existir no índice usado para aquela resposta, corrigir publicação/versão; se coincidir e o caminho não for legível, corrigir conteúdo/permissão. Mudança entre captura e reinício precisa ser documentada.
4. A cópia privada foi obtida por autenticação do Linux, fora do chat; o índice foi conciliado e implantado com IDs preservados. O mapa corrigido e o catálogo HTTPS já foram comparados. Próxima captura necessária: seleção/trace do APK atualizado e instalação no aparelho.

## Diagnóstico histórico dos 404 — recorte até 14h12

O TurboStation usa componentes do backend Turborama Suite para banco, autenticação e licença. `Suite__Enabled=true` é uma dependência de registro dos serviços Station em `Program.cs`; `Station__Enabled=true`, produto `TURBORAMA_STATION_ANDROID` e rotas `/v1/station/*` mantêm a identidade própria do aplicativo. A conta Linux da API Station se chama `turborama-suite`. O trabalho desta rodada abrange a 5192, seu conteúdo e a compatibilidade do APK.

Às 12h24, o journal da **5192** desde 08h30 mostrou `/catalog` 200 em 8 chamadas, `/me` 200 em 6, `/sessions` 200 em 8, `/covers/{id}` 404 em 83 e `POST /downloads/authorize` 404 em 47. Todas as 47 autorizações registraram execução do endpoint; todos os 83 IDs de capa atendem ao formato aceito, distribuídos em 51 IDs distintos. Não houve 200 de capa, autorização ou transferência observado nesse período. O journal desse recorte não registra os corpos. O retorno Android posterior confirmou 404 `STATION_COVER_NOT_FOUND` e 404 `STATION_ITEM_NOT_FOUND`; o índice efetivo e a correlação individual continuam ausentes, portanto a causa de cada registro não está comprovada. Uma sonda pública com `curl` recebeu 401 JSON de `/catalog`, confirmando chegada ao serviço e exigência de Bearer. Uma sonda com User-Agent padrão do Python recebeu 403/1010 do Cloudflare; essa sonda não é prova de comportamento do APK.

A DLL efetiva contém o metadado `1.0.0+bbd07fda79e1c938f691ab874aa7c1fc84cc65e2`. No fonte identificado por esse metadado, o catálogo é montado a partir dos registros JSON sem conferir a existência das ROMs/capas. `ReadCover` devolve ausência para `coverId` desconhecido, arquivo para o qual `File.Exists` é falso ou tamanho fora de 1 byte a 5 MiB; `TryResolve` também exige `File.Exists` para autorizar jogo. `AuthorizeDownloadAsync` também devolve `STATION_ITEM_NOT_FOUND` quando esse `TryResolve` falha, incluindo ROM indisponível; na candidata nova, ID conhecido sem artefato pronto recebe 503 `STATION_ARTIFACT_NOT_READY`. Isso permite catálogo 200 acompanhado de capas/autorizações 404 no código antigo. A identificação pelo metadado não substitui a correlação do item nem prova ausência de alterações locais na compilação original. A verificação de ACL confirmou que `/media/lz-servidor` dá acesso a `root` e `lz-servidor`, mas **nenhum acesso** ao UID 995 (`turborama-suite`), cujos grupos tampouco permitem atravessar esse diretório. Se o índice efetivo aponta para o HD indicado, essa permissão bloqueia ROM e capa. Também é preciso comparar os IDs pedidos com um catálogo fresco e os caminhos configurados; o índice protegido impede fechar essa comparação agora.

A cópia candidata materializada recebeu ACL específica de leitura para UID 995: somente travessia no diretório pai e leitura/execução nos 1.819 diretórios, leitura nos 3.635 arquivos de conteúdo/índice/relatórios, sem escrita para a conta de serviço. As ACLs anteriores foram salvas privadamente para retorno. A unidade Station não tem `RootDirectory`, `RootImage`, `InaccessiblePaths` ou `PrivateMounts` que bloqueiem essa árvore; uma operação real sob UID 995 ainda precisa ser confirmada na implantação. Os 3.632 checksums de jogos/capas passaram novamente e os hashes de índice/manifesto permaneceram iguais. A ACL foi aplicada somente ao estágio preparado e ao acesso ao seu diretório pai; a 5192 continua com DLL/índice antigos.

Plano registrado antes do rollout (executado posteriormente, com prova no início): obter a cópia do índice efetivo; conciliar IDs publicados com os 1.816 itens preparados; conferir leitura de todos os arquivos no destino final; verificar migration 029/chave de grants e aplicar a release Station já identificada com backup/retorno. Em seguida, exigir catálogo/capa/download 200 autenticados por HTTPS e instalação no APK. O APK precisa usar `coverId` e `itemId` da resposta assinada atual e o mesmo Bearer de sessão para autorização/transferência; um grant expirado ou consumido exige autorização nova.

## Homologação complementar da release Station em 03/10/2026

`tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py` agora aceita `STATION_HTTP_REAL_TTL=1`. A execução usou a DLL publicada `45fe4df`, o índice materializado dos 1.816 jogos e PostgreSQL temporário; terminou com `STATION TTL: OK` e `STATION HTTP SMOKE: OK`, amostrando uma capa e um download completo por cada uma das quatro plataformas. Os desafios de ativação e sessão expiraram por relógio real com 409 `STATION_CHALLENGE_INVALID`; o grant expirado devolveu 404 e a sessão ainda válida permitiu uma autorização nova com download/hash corretos. Após 180 s, perfil e autorização devolveram 401 `STATION_SESSION_INVALID`; uma sessão nova restaurou perfil e download completo. A interrupção de transferência também foi seguida por grant novo, bytes completos e hash correto. A fase de relógio não alterou datas de expiração no banco. Isso comprova a candidata em isolamento; a 5192 pública e o APK ainda precisam dessas mesmas provas após a conciliação/implantação.

Comando para repetir, apontando apenas para arquivos candidatos e banco temporário; sem credenciais de produção:

```text
STATION_HTTP_REAL_TTL=1 \
STATION_HTTP_API_DLL=DLL_CANDIDATA \
STATION_HTTP_EXTRA_INDEX=INDICE_CANDIDATO_PRIVADO \
pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
```

## Retorno histórico ao pedido `d953998` — estado anterior às 14h12

Li integralmente `docs/server/HANDOFF-PEDIDO-FECHAMENTO-SERVIDOR-STATION-20261003.md` do TurboElden, commit `d953998`. Este retorno continua no documento único do servidor. **Entrega naquele recorte: candidata isolada; o rollout posterior e seu resultado estão no início deste mesmo arquivo.** O pedido exige índice conciliado, permissões da conta de serviço, migrations/backup, APK compatível e prova HTTPS real antes de declarar produção pronta.

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

## Inventário inicial: nomes, jogos e capas — preservado como referência

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

### Índice inicial candidato completo — IDs provisórios antes da conciliação

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

### Como o APK deve ler e comparar o catálogo verdadeiro (revisão4 vigente)

1. Abrir sessão Station e executar `GET https://app.lzgames.com.br/v1/station/catalog` com o Bearer vigente; não buscar listas antigas, CDN, nomes de arquivo ou diretórios para completar a tela. Registrar se a resposta veio da rede ou de `StationCatalogStore` (`Library.cached`). Cache assinado permite exibir o último catálogo, mas não prova a revisão atual da 5192.
2. Usar `StationApi.catalogSnapshot`: conferir `keyId`, RSA-PSS/SHA256 do `payload` original, `schemaVersion=1`, domínio `TurboRamaStationAndroid/catalog/v1`, produto/aplicação e identidade de licença/aparelho/sessão. Só então chamar `StationCatalog.fromVerifiedPayload`. O limite atual é 12 MiB para envelope e 4096 `items`; `revision` global e `revision` de cada item são inteiros positivos.
3. Para cada item assinado, usar **exatamente** `itemId`, `name`, `platform`, `revision` e `coverId`; agrupar pelo valor bruto de `platform`, antes do rótulo visual. `StationFrontend` deve publicar todas as linhas verificadas ao serviço nativo e informar contagem recebida, contagem publicada e `cached`. `StationPlatforms.resolve` precisa de mapeamento explícito para cada nova plataforma. No fonte 02c09dd, as quatro plataformas HTTP atuais têm mapeamento explícito. Dos 36 identificadores históricos, somente `model2` e `sufami` continuam sem mapa confirmado; os demais aliases foram acrescentados sobre pastas verificadas. Uma plataforma desconhecida deve ser contabilizada/avisada sem interromper os jogos conhecidos, conforme APP-03. A referência histórica não autoriza publicar jogos sem arquivos.
4. Para cada jogo visível, pedir `GET covers/{coverId}` na mesma sessão, conferir MIME e bytes e associar o cache a `coverId` + revisão do item. Se a capa retornar 404, mostrar placeholder e registrar esse `itemId`/`coverId` para correção do índice; não adivinhar uma imagem pelo nome. Um `coverId` compartilhado exige mesmo caminho e revisão no índice. Mudança de bytes exige nova revisão do item ou novo `coverId`.
5. Exportar o envelope **privadamente** e validar com `scripts/exportar-catalogo-assinado.py`, usando a chave pública SPKI Station do cliente. O script exige o `keyId` público esperado; sua saída contém somente `platform,itemId,name,itemRevision,coverId`, ordenados, mais revisão/contagens. Nunca publicar o envelope, Bearer ou identidades nele contidas. Cruzar com o índice efetivo por `itemId` e `coverId`; comparar SHA256 do arquivo de jogo com o volume **quando os bytes forem idênticos**. As capas atuais são copiadas de revista sem conversão: exigir `indexCoverSha256=sourceRevistaSha256`, além da comparação HTTP, com o mapa da entrada XML exata e `--require-revista`. Nomes iguais, sobretudo os repetidos de Mega Drive, não bastam para criar pares. As1.816 linhas da revisão4 têm cruzamento exato; todas as2.071 capas internas coincidem com a revista selecionada. Para futuras atualizações, manter esses mesmos vínculos e provas.

Exemplo de export depois de capturar a resposta autenticada **fora do Git** e salvar a SPKI pública DER obtida por decodificação Base64URL de `StationConfig.STATION_ASSERTION_SPKI_BASE64URL`; a ferramenta usa Python/cryptography, confere o `keyId` esperado, verifica a assinatura e cria a saída com modo 0600:

```text
python3 docs/station-android/scripts/exportar-catalogo-assinado.py \
  --envelope RESPOSTA_PRIVADA.json --public-key STATION_PUBLICA.der \
  --output CATALOGO_VERIFICADO.tsv
```

A cópia privada já foi obtida e conciliada no rollout acima. Para futuras revisões, usar `scripts/cruzar-indice-catalogo.py` (Python/Pillow) em diretório privado para exportar somente nomes, IDs, revisão, hashes, estado de leitura da capa, contagem de pares exatos por nome/hash e confronto por `itemId` com a resposta assinada. O script detecta `coverId` compartilhado com caminhos/revisões conflitantes e não copia `filePath`/`coverPath` para a saída; arquivo não legível permanece marcado como tal. Amostras HTTP e todos os hashes no destino do serviço foram conferidos neste rollout; repetir a comparação quando os bytes mudarem:

```text
python3 docs/station-android/scripts/cruzar-indice-catalogo.py \
  --index INDICE_PRIVADO.json \
  --disk-tsv docs/station-android/catalogo-disco-snes-megadrive-20261003.tsv \
  --catalog-tsv CATALOGO_VERIFICADO.tsv \
  --source-map MAPA_ORIGEM_PRIVADO.json --require-revista \
  --output CRUZAMENTO_PRIVADO.tsv
```

O contrato atual não pagina. A lista histórica de 12.346 excede 4096 itens; antes de publicar um catálogo desse porte, definir paginação versionada com revisão consistente e assinatura por página, atualizar o cliente e testar o carrossel. Para o subconjunto SNES/Mega Drive do volume, o candidato resolveu as capas e excluiu os 8 registros Mega Drive sem ROM. A conciliação, preservação de IDs e prova HTTPS da 5192 foram concluídas no rollout. Falta testar esse catálogo no APK e aparelho. Copiar um XML sem comprovar os arquivos e seus vínculos não atende esse critério.

## Referências e decisão

- Pedido atendido: [handoff do cliente reconstruído](https://github.com/luziellacerda/Servidor-pix/blob/7ac4fad9e0132db378f6e78e6494fedb08f614c3/docs/station-android/HANDOFF-CLIENTE-RECONSTRUIDO-STATION-20261002.md), branch `docs/cliente-reconstruido-station-20261002`, commit `7ac4fad9e0132db378f6e78e6494fedb08f614c3` do Servidor-pix.
- Cliente examinado originalmente: TurboElden, commit de código `0840028854034b03e5a1d3f2a162d66225932a6b` e revisão documental `f48399bc24691afe2073fac55f279b452c01343b`, branch `station-reconstrucao-20261002`. **Atualização:** o commit `7d5df08d922ef7c517979cf2263ff4bfe4be74ff` registra APK candidato integrado e instalado, com leitura tipada de `artifact`/`itemRevision`, mas sem prova de capa 200 e download/instalação contra a 5192 de produção.
- Código do servidor deste retorno: branch `feat/station-artifact-descriptor-20261002`, commits `96326aa0aeb164820cec26f8b5911fcdb47fc8ee`, `1bfb619c21becffe40aaa597e100fcb3719c2e72` e `de08858` (correção e homologação HTTP isolada), derivados de `b1159c9`.
- **Estado atual: API fd13c0d e catálogo revisão 4 implantados, com prova HTTPS autenticada.** O rollout de capas reiniciou apenas a5192 para carregar o índice4; os12 serviços acompanhados, binários, migrations, chaves, portas e Nginx foram preservados. O APK instalado ainda exige a conferência visual do catálogo fresco e o fluxo de jogo no aparelho.

## Rede e limites do aplicativo

O telefone usa somente `https://app.lzgames.com.br/v1/station/*` na porta 443: Cloudflare → túnel → nginx → API Station em `127.0.0.1:5192`. A porta 5190 é a API Suite Windows; 5191 é o gateway de conteúdo Suite; 5194 é o helper de emissão comercial Station. Nenhuma dessas portas, IP privado, caminho do disco ou URL de jogo entra no APK. Capa e jogo chegam como bytes autenticados da própria 5192, sem redirect. O APK não deve chamar `/v1/suite/*`, usar cookie, cache público ou gravar Bearer/grant no disco.

O cliente usa `productId=applicationId=TURBORAMA_STATION_ANDROID`, pacote `org.turboramastation.frontend`, `schemaVersion=1`, domains `TurboRamaStationAndroid/<ação>/v1` e `deviceId` Base64URL do SHA256 da chave pública SPKI RSA-2048 do Android Keystore. Respostas assinadas usam `{keyId,payload,signature}`; `payload` são bytes JSON UTF-8 em Base64URL sem padding, assinatura RSA-PSS/SHA256 sobre esses bytes. O `keyId` público configurado no cliente é `06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268`; confirmado nas respostas autenticadas do rollout. O pin TLS SPKI SHA256 configurado no cliente é `13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7` e **foi confirmado novamente no endpoint público em 03/10/2026**. Não duplicar essas constantes em outra classe Android.

## Estado efetivo no Linux — 03/10/2026, 18h53; conferência final19h05

| Verificação | Resultado observado |
| --- | --- |
| Serviço | `turborama-station-api.service`, ativo, PID 154043; drop-ins próprios revisão3 e `zz-station-rev4-covers-20261003.conf`. |
| Comando / diretório | `/usr/bin/dotnet /opt/turborama-station-20261003-fd13c0d/TurboRamaSuiteOnlineServer.dll`; WorkingDirectory da mesma release. |
| DLL / origem | SHA256 `f305ae3763cb77a53a27b2c168c8de290191b3a7e88e612850856b6f7be7e639`; metadado `fd13c0d27eaab6a4dcf931a9cd64c3dcd7dd50c4`. |
| Índice / catálogo | SHA256 `c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a`; revisão 4; 2.071 entradas privadas, 1.816 jogos públicos; histograma confirmado em HTTPS. |
| Conteúdo | 4.142 arquivos/hash abertos pela identidade real UID 995; leitura sem escrita da API. |
| HTTP autenticado | Nove rotas passaram; catálogo integral, nove capas iguais à revista e cinco downloads com assinatura/tamanho/hash; reuso dos cinco grants404. |
| Sondas adicionais | `/ready/station` local 200; catálogo público sem Bearer 401 `STATION_SESSION_INVALID`; correlação ecoada. |
| Rede | Loopback 5192 preservado; APK usa somente HTTPS443 `app.lzgames.com.br/v1/station/*`. |
| Serviços compartilhados | Os12 serviços acompanhados continuam ativos e com os mesmos PIDs; somente API Station reiniciada, sem reload de Nginx. |

O PID 2388, DLL 75c466c3, ausência de drop-ins e o catálogo 996/revisão 1 descrevem o estado anterior. O binário/índice antigos continuam disponíveis para rollback. O monitor de conteúdo não foi alterado; seu último estado conferido foi inactive/dead com Result=success. Nenhuma credencial ou caminho privado de mídia foi publicado.

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

## Contrato de download implantado na 5192 em fd13c0d

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

As primeiras provas com PNG sintético de1×1 e os recortes de48/83 respostas404 são históricos. Às15h21, a publicação fd13c0d corrigiu a leitura dos arquivos sob UID995. Às18h53, a revisão4 passou a servir a revista selecionada:2.071 capas cruzadas integralmente, nove amostras HTTPS200 iguais à revista e cinco downloads íntegros. A seção inicial registra a causa da seleção anterior e as instruções de cache; a renderização no aparelho continua pendente de prova.

## Evidências e pendências para liberar o APK

| Requisito | Comprovado | Trabalho restante |
| --- | --- | --- |
| Código / conciliação | DLL fd13c0d; suíte .NET; 13 casos de conciliação e 6 do exportador; HTTP do índice conciliado. Retorno2834e3b comprova build Android/NDK, 302 checks Java, 28 JNI e 7 retry Android. | Fluxo autenticado e jogo no APK instalado. |
| Catálogo / conteúdo | HTTPS assinado revisão 4/1.816; todos os nomes/ROMs/capas cruzados; 996 IDs antigos preservados; 4.142 arquivos/hash legíveis pela API. | Conferir seleção, quantidade e cache no aparelho. |
| Capas / downloads | Nove capas HTTPS200 iguais à revista; cinco downloads,2raw/3ZIP, incluindo ID antigo; MIME/tamanho/SHA256 e grant de um uso conferidos; inexistente404 passou em isolamento. | Renderizar capas e completar download/instalação no APK. |
| Sessão / erros | HTTPS sintético e cenários isolados aprovados; recuperação real16h47 registrada. Painel publicado18h15: mesma licença ativada com três chaves sintéticas, antigas negadas, bloqueio/desbloqueio/reconexão auditados. | Ativação/perfil/retomada/cancelamento no APK real; pagamento real pelo provedor. |
| Operação / retorno | Migrations028/029 existentes e030 administrativa aplicada; backups restaurados em cluster temporário, retorno da publicação administrativa executado, demais serviços preservados. | Guardar artefatos/backups e incrementar revisão em futuras trocas de bytes. |
| APK / emulador | APK fa3bc844 instalado e hash conferido pela equipe Android; assinatura, manifesto e motores preservados. Fonte02c09dd compilada. | Concluir nova ativação, catálogo4/1816, capa/download/instalação/abrir/voltar; legado residual permanece sem aceite. |

## Compatibilidade e ordem de liberação

| Combinação | Resultado / ação |
| --- | --- |
| API fd13c0d/revisão4 + APK instalado fa3bc844 | Correções compiladas e instalação comprovada em2834e3b. Conferir a licença no painel; para reinstalação/troca ou código vencido, usar a ação correspondente. Validar ativação, catálogo e instalação no aparelho. |
| API fd13c0d + fonte Android02c09dd | Build/ponte já comprovados pela equipe Android; fluxo de produção no aparelho aguarda ativação e provas de conteúdo. |
| Cliente com catálogo/cache revisão1 ou3 | Atualizar e verificar resposta assinada revisão 4; não instalar usando descritor com revisão diferente. Licença e Keystore são preservados. |
| Código novo + índice sem descritores | Autorizar item conhecido produz503 `STATION_ARTIFACT_NOT_READY`; combinação não usada em produção. |
| Retorno à DLL antiga | Voltar também ao índice/configuração anteriores, pelo script limitado; não misturar DLL antiga com índice corrigido de compatibilidade. |

## Plano único de execução, responsáveis e aceite

### 1. Backend e operação: concluído para catálogo/capas/downloads

1. Índice efetivo exportado, conciliação por origem comprovada, IDs/ROMs preservados; plataformas/nomes corrigidos e duplicatas ocultas. Revista é agora a origem obrigatória das capas;2.035 imagens anteriores foram substituídas com revisão4.
2. Todos os descritores e capas preparados; conteúdo copiado para release privada estável, somente leitura pelo serviço; 4.142 hashes conferidos como UID 995.
3. Ledger028/029 e chave de grants conferidos sem publicar valores; nenhuma migration/chave alterada. Backup de arquivos e dump do banco conferido/restaurado em ambiente temporário.
4. Release fd13c0d aplicada somente à Station; duas linhas de correlação corrigidas no snippet Station com `nginx -t`/reload. Serviços compartilhados permaneceram ativos com os mesmos PIDs principais.
5. Nove rotas de produção verificadas com licença/aparelho sintéticos, catálogo assinado completo, nove capas de revista, cinco downloads e negação de reuso; dados de teste removidos. Próximo aceite pertence ao APK e à operação administrativa real, conforme tabela.

### 2. Android: validar e concluir o APK candidato

O retorno `2834e3b` registra o APK instalado fa3bc844, com fonte02c09dd compilada, assinatura preservada e provas Android da ponte. A transferência/reemissão oficial das16h47 libera a matrícula da chave atual. A lista abaixo continua como critério de aceite do fluxo autenticado e download real; o APK f5b35419/runtime629a55a8 é histórico.

1. **Android:** conferir no APK candidato a leitura **obrigatória** de `itemRevision` e todos os campos `artifact` do payload assinado, já implementada em `StationApi.Grant`. Comparar item/revisão com o catálogo da mesma sessão; exigir hash e tamanho esperados em `StationFiles`. Rejeitar descritor ausente, inválido ou formato não suportado. Preservar `StationConfig` (host, pin TLS, autoridade pública) e o alias do Keystore. Não trocar o código de ativação por `STA-`: esse prefixo identifica a licença, não a senha emitida.
2. **Android/nativo:** validar os 94 jogos da categoria `megadrivebr`, cujo alias já está no APK fa3bc844 com fonte `02c09dd`, e validar a ligação já feita de `StationCoordinator` ao carrossel/catalog service e às telas reais, preservando seleção, texturas, jogos instalados, saves e emuladores. Usar `platform` somente por mapeamento explícito, inclusive edições BR; plataforma desconhecida não vira pasta por aproximação. Usar `coverId`/revisão no cache, carregar somente capas visíveis e tratar 404/429 sem tempestade de pedidos. Concluir a retirada das chamadas antigas de catálogo, licença, telemetria e URLs de jogo do frontend anterior, sem atingir redes internas dos emuladores.
3. **Android/instalador:** quando um jogo estiver ausente, autorizar uma vez, baixar sequencialmente para temporário privado com o **mesmo Bearer**, recusar 3xx/`Location` e Range, confrontar `Content-Length`, tamanho e SHA256, e só então processar `raw|zip|rar|7z`. Para compactados, limitar arquivos/tamanho extraído, rejeitar caminho absoluto, `..`, links e duplicatas, conferir `launchPath` e referências auxiliares como BIN/CUE. Usar publicação atômica e manifesto de arquivos: parcial, falha ou cancelamento nunca viram “instalado” nem substituem jogo/saves íntegros. Em queda após consumo, pedir outro grant; nunca repetir o GET consumido.
4. **Android/build:** o APK candidato completo já foi instalado com atualização e assinatura preservada; confirmar que DEX e bibliotecas usam somente o cliente Station novo nas rotas comerciais. Preservar pacote, dados privados, emuladores e saves. Completar no aparelho: ativação quando necessária, retomada por licença salva, `/me`, catálogo fresco, capa 200, download, cancelar, abrir jogo, voltar, apagar somente arquivos do jogo e relançar offline conforme política definida. Registrar hash e versão do APK aceito antes de promover.

### 3. Matriz atual de provas para fechar este mesmo handoff

| Prova | Situação atual / limite |
| --- | --- |
| Autenticação/perfil | HTTPS sintético e TTL/cenários isolados aprovados. Recuperação real16h47 auditada. Painel deda92c publicado18h15 com códigos30min/48h, três chaves na mesma licença, transferência/reinstalação/revogação/bloqueio/desbloqueio/reconexão e perfil HTTPS comprovados. Nova ativação/perfil do APK e pagamento real pelo provedor continuam sem prova. |
| Catálogo | HTTPS200, revisão 4, 1.816 itens, quatro plataformas exatas, assinatura/keyId/vínculo de sessão conferidos; TSV com 1.816 pares exatos e zero IDs faltantes. |
| Capas | Nove capas HTTPS200 com MIME e bytes/hash iguais à revista selecionada, incluindo um `coverId` antigo e os quatro casos de variantes. Todas as2.071 capas tiveram leitura/hash sob UID995 e conferência revista. Negativa de capa inexistente passou isolada; cache/repetição visual aguardam APK. |
| Autorização/bytes | Cinco grants assinados com revisão/descritor/identidades exatos; GET200, tamanho e SHA256 conferidos; 2 raw/3 ZIP. Cinco reutilizações404 comprovadas em produção. Expiração/outro aparelho/interrupção/novo grant passaram isolados. |
| Operação | API fd13c0d/índice4 preservados; administração deda92c publicada, migration030 aplicada e isolada por produto; backups restaurados, retorno executado e PIDs dos demais serviços preservados. |
| APK/emulador | Retorno2834e3b comprova APK fa3bc844 instalado/hash, 302 Java, 28 JNI e 7 retry Android. Falta nova ativação e catálogo4/1816/capas/download/instalação/abrir/voltar. O aparelho/build foram operados pela equipe Android; Linux realizou a recuperação administrativa. |

A API tem prova de conteúdo autenticado de produção além de `/ready/station`. O aceite completo do aplicativo continua condicionado à execução no aparelho. O corpo do handoff preserva evidências históricas com data; este estado operacional e a evidência JSON do rollout prevalecem sobre o recorte anterior. Segredos, identidades de compradores, URLs privadas e caminhos de mídia permanecem fora dos commits.
