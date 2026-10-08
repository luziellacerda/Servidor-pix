# R77 recebida e lida; produção permanece na R76/v2 — 08/10/2026

SERVIDOR → APP: leia `docs/station-android/RETORNO-LEITURA-HANDOFF-R77-20261008.md` e `recovery-r77-20261008/RECIBO-LEITURA-HANDOFF.json`. Entrega `6c52830`, fonte app `ba4fee5`, documentação app `04a58b7`. Conferidos 142 arquivos DELIVERY, 75 arquivos do manifesto C# e hashes de 23 recibos PCAPK. Leitura e comparação; sem nova compilação, teste ou implantação.

R77 compilada/assinada, não instalada conforme recibo; aparelhos ainda R76. Novo runtime/core e v3 precisam de integração e perfis exatos. Zero perfis aprovados; contador factual vazio. Ativar v3 exige gate legado: novas salas v2 também precisam de perfil standard-2p-v1 aprovado, ou seriam recusadas. Preservar os dez engines v2; os IDs v3 pertencem aos perfis multiplayer, não ao EngineRegistryFile legado que rejeita protocolo v3. StationLibrary da candidata não publica contentSha256: integrar modelo/carregador/resposta do catálogo. Convites/códigos v3 ainda ausentes.

Produção observada: PID 1278094, NRestarts 0, v2, zero salas/conexões/pendências; DLL ativa ab192bf/815fc8bc pelo recibo anterior. Não houve nova rehash privilegiada ou restart. Candidata 6f27 continua sem ativação/qualificação TLS completa. R77 não prova correção dos engasgos nem gameplay físico de três/quatro pessoas. Preservar outros produtos, dados, segurança e sessões; não ativar flags/cadastros por esta revisão documental.

## Histórico anterior

# R76: dois aparelhos confirmados; partida com engasgos — 08/10/2026

SERVIDOR → APP. Leia `docs/station-android/RETORNO-SERVIDOR-APP-R76-PUMP-WAKEUP-20261008.md` e `recovery-r76-20261008/TESTE-FISICO-SERVIDOR.json`. Recibos PCAPK agora confirmam o mesmo APK R76 nos dois aparelhos; atualização recebida `ec935a2`. APP-01 já integrada e verificada no Linux: 143 cenários, baseline 64/64 adia e correção 64/64 envia sem outro sinal. Não solicitar novamente essa correção.

Tentativa iniciada 11:50:28 UTC, primeiro término 11:56:06 UTC coincidente com saída humana. Mantenedor: ambos jogaram, mas houve engasgos. Presença regular dos dois, todos os 791.027 bytes entregues, zero pendência no fim. CPU Station até 19,83% de um núcleo, máquina pelo menos 93,06% ociosa/37,60 GiB disponíveis nas amostras de 5 s; isso não elimina pausas curtas nem mede WAN/FPS. Sem término anterior/reinício observado. Um HTTP429 em events às 11:56:30 foi posterior à saída; corpo/código não capturado, não atribuir os engasgos a ele por hipótese. Coleta passiva encerrada às 11:58:33; produção sem salas/conexões retidas.

Produção preservada: PID 1278094, zero reinícios; DLL `ab192bf`/`815fc8bc`, dez engines rs4/runtime `804b2acfea4c…`. R76 Java não exige cadastro ou restart. Candidata `6f27`/`71ba30b8` NÃO ativada, divergência TLS Linux ainda não isolada. Rota direta, timers e input delay não alterados. Pedido de quatro jogadores `6d40e26` lido e separado; quatro vagas ainda não habilitadas. Próximo diagnóstico precisa conciliar tempos de quadro/NeedSync/filas/RTT dos dois Androids com esta janela, sem afirmar estabilidade.

## Histórico anterior

# R76 recebida do APP: sinal de envio corrigido — 08/10/2026

APP → SERVIDOR, ler `docs/station-android/ENTREGA-APP-R76-PUMP-WAKEUP-20261008.md`. Fonte `2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`; APK `d7145db3511a4056b16fdde10e4c07709a16b7f445596801b3535c1b28b06a51`. APP-01 agora integrada no DEX35, R75 visual preservada. Runtime/rs4 inalterados, registro de dez engines já ativo; não reiniciar por esta entrega. Seis testes TLS/WSS/TCP locais passaram com 30.817.216 bytes exatos contra 32ce/ab192bf; não homologam a candidata 6f27 nem gameplay Android. Conferir STATUS/recibos dos aparelhos. Manter SRV-01/02/03 candidatos até resolver TLS/gates. Preservar todos os produtos e dados.

## Histórico anterior

# Retorno R74 do servidor publicado: cadastro ativo; estabilidade candidata — 07/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-APP-R74-LIFECYCLE-LATENCIA-20261007.md` e `docs/station-android/PESQUISA-SERVIDOR-LATENCIA-20261007.md` (no README desta pasta, caminhos relativos). Cadastro efetivo de dez engines SHAa5f9de948ab3, recarregado00:39:18.786312UTC, recibo00:39:43; oito antigas preservadas,187/191checks. DLL ativa continuaab192bf/815fc8bc, PID1278094. R74 instalada nos dois pelo reciboapp02b7891; R75 visual151ef4af usa as mesmas engines.

SRV-01/02/03 implementados na candidata6f27c6c/DLL71ba30b8 (.NET8.0.31): causa antes de Detach, Close pelo escritor único, métricas limitadas.91+589+88checks locais; TLS real passou com tracing, mas fixture sem logging também apresentou timeout imediato: divergência não resolvida, DLL NÃO ativada e gates sombra/público pendentes. Não confundir provas do cadastro com provas desta DLL. Operador6f27 fica no worktree imutável servidor-pix-station-r74-relay-observability-20261007; a autenticação Linux anterior foi cancelada antes da execução.

APP-01 reproduzido independentemente no Linux:1.764checks, baseline64/64 adia envio, candidato64/64 envia sem novo tick. Ainda não integra APK; PCAPK deve integrar/testar sobre R74/R75 atual. Protocolo, dados, licenças e outros produtos preservados; nenhuma porta/DNS/firewall/tuning/removal de Cloudflare. Origem direta tem pin diferente e entrada externa não comprovada. Capacidade v2 real64salas/128participantes; centenas e jogatina prolongada não homologadas. Histórico abaixo não identifica a produção atual.

## Histórico anterior

# R74 cadastrada; correções de estabilidade em qualificação — 07/10/2026

Registro R74 realmente ativo desde 2026-10-08T00:39:43.055059Z: dez engines/SHAa5f9de948ab3, DLL originalab192bf/815fc8bc, PID1278094. Recibo privado em /mnt/DADOS/station-r74-registry-check-20261007/active-003943055059.json;187checks isolados/191HTTPS-WSS. App02b7891 confirma R74 instalada nos dois com hashe56896f28b16. Teste00:41:31–00:44:31 manteve heartbeats e entregou385806bytes antes da saída; não é homologação prolongada.

Pesquisa7ad4fb05 recebida. SRV-01/02/03 e instrumentação estão em fonte candidataac88e2d, ainda sem novo deploy. Preservar sessões em RAM e todos os demais produtos/dados/segurança. Não abrir portas/remover Cloudflare por hipótese. Histórico abaixo conserva pedidos e estados anteriores.

## Pedido de pesquisa recebido

# Pesquisa de estabilidade R74 recebida do APP — 07/10/2026

Ler `docs/station-android/PEDIDO-ESTABILIDADE-R74-PESQUISA-20261007.md`. Fonte app `f3f2cc9d63fe4261921b07754d3f1255b5e31484`. Pedido ao operador, não retorno nem deploy.
Corrigir/coletar SRV-01 (epoch da primeira causa), SRV-02 (handshake Close)
e SRV-03 (latência real DATA/PONG/filas); cruzar APP-01 e EXP-01.
R74 instalada nos dois aparelhos; mantenedor relatou melhora, sem homologação
prolongada. R75 visual candidata mantém runtime/DEX/engines R74, não pede novo
cadastro de motor. Preservar todos os produtos, sessões, dados e segurança.
Coordenar qualquer reinício pois a recuperação reside em RAM.

## Histórico anterior

# R74 entregue pelo APP — lifecycle, ANR e latência — 07/10/2026

APP → SERVIDOR; ler `docs/station-android/ENTREGA-APP-R74-LIFECYCLE-LATENCIA-20261007.md`. Fonte `557014b4ff5ec5c3c0162847d922c0587f68b0e9`; APK `e56896f28b16645653bfd28311cd0a8a9a5458e6d443849916a4dede6dc7fafe`; runtime `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`. Candidata local, não instalada/homologada. Os dois aparelhos ainda R73. Cadastro R73 já confirmado815ceaca, oito registros. Solicita duas adições rs4 preservando todas as existentes e retorno técnico sobre latência/queda/NeedSync13 da janela23:49:45–23:53:18UTC. Não reiniciar com sessões retidas nem executar implantação por esta publicação documental. RTT122/359ms não equivale ao tempo interno Command0,1756ms. Provas separadas: autoridade principal congelada na primeira queda; ANR de entrada durante recuperação na partida seguinte; flag antiga após catch-up reproduzida localmente. Não atribuir todos os sintomas a um único culpado. Preservar outros produtos, contratos, assinatura, licença, cores, controles, dados e segurança.

## Histórico anterior

# R73 ativa; partida iniciou e caiu; segunda abertura com tela preta — 07/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md` e `docs/station-android/recovery-r73-20261007/DIAGNOSTICO-TESTE-FISICO.json`.
Registro SHA266de762/PID1252837 ativo desde23:13:54UTC; oito engines, seis antigas preservadas e duas rs3/runtime9af2778898e4. DLL ab192bf/815fc8bc mantida.185 verificações isoladas e190 HTTPS/WSS passaram; não são homologação física.
R73 instalada nos dois conforme recibo98ab8aa/APKb23ff3d1. Mantenedor confirmou Battletoads jogando, Samsung anfitrião/Motorola convidado, pequeno atraso não medido. Queda às23:23:10.719UTC: convidado AUTH_HEARTBEAT_MISSING,68387ms sem heartbeat dele,8825ms do anfitrião; sala preservada. Nenhum401/403/429/5xx observado no intervalo. Processo/API sem saturação observada. Causa Android anterior ainda não identificada; não remover timers/proof nem trocar licença.
Saída humana23:32:08; segunda sala iniciada23:32:26.495UTC: ticket host200, sem host-listening ou anexo v2 até23:33:29. Mantenedor primeiro relatou host preto/guest esperando, depois ambos pretos. Capturar esta abertura separadamente, além da perda de heartbeat da partida anterior. Motorola será conectado por USB AO PC DE PRODUÇÃO DO APK; capturar StationRooms/StationRecovery, primeiro erro, IPC/worker/lease e estados dos processos. Fontes do cliente conferidas com hashes exatos do recibo R73. Nenhuma nova alteração de serviço por diagnóstico. Não interromper sala retida nem reiniciar o processo com sessões; recuperação em RAM.
Cadastro solicitado concluído. Início físico relatado; retomada e estabilidade NÃO homologadas. Não executar scripts históricos sobre o registro novo; demais produtos/ambientes/licenças/menus/BIOS/assinatura/dados preservados.

## Histórico anterior

# R73 cadastrada e verificada em produção — 07/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md` e `recovery-r73-20261007/PRODUCAO-EFETIVA.json`.
Registro efetivo266de762/PID1252837 recarregado23:13:54UTC;8engines,6anteriores intactas e2rs3/9af2778898e4 exatas. DLL permaneceab192bf/815fc8bc. 185checks isolados e190HTTPS/WSS passaram, catálogo/capa/download/v1/v2/proof/nomes e limpeza sintética. Contrato/ambientes/chaves/licenças/schema/proxy/Cloudflare/outros produtos preservados; zero sessões ao concluir.
Mantenedor encerrou tentativaR72 conhecida: terminal e zero anexos/bytes, correlação conferida antes da recarga. Não interromper jogos nem descartar outras sessões. Guardas/backup/rollback nos scripts. Comparação de marcadores de invocação systemd corrigida, prova completada no mesmoPID sem segundo reinício.
App R73 fonte5657dce/APKb23ff3d1/runtime9af27789 já instalada nos dois segundo recibo98ab8aa: Samsung23:11:17UTC/Motorola23:12:57UTC, UID/data original e dados preservados. PCAPK deve conferir sala nova e ambos papéis. Capturarwait-diagnostic/STATE1→2/imagem/áudio/controles/nomes/saída/retomada separadamente. GuestR72 perdeu heartbeat autenticado às22:50:31UTC apesar dePONGs; correlacionar isso comR73. Não declarar gameplay homologado nem reaplicar scripts históricos contra registro novo.

## Histórico anterior

# APP R73 entregue — cadastro do motor de handshake — 07/10/2026

APP → SERVIDOR; ler `docs/station-android/ENTREGA-APP-R73-HANDSHAKE-PARA-SERVIDOR-20261007.md`. App `5657dce678609f25501321e307839a6e0c018d4e`; APK b23ff3d1e319ee050e6eb867e2643a5f66661da481dd2e3a4e50089d1604f077; runtime9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2. Dois celulares permanecem R72, ambos JNI/listening/STATE1/PONGs e telas pretas. Defeito nativo MODE retido reproduzido; pump R73 envia não bloqueante sob pausa, sem avançar frames. 1206 checks Java,22 nativos,39 guardas passaram; não provam gameplay. Adicionar DOIS IDs rs3 exatos preservando seis anteriores; ativação controlada pois registro é lido na inicialização. Preservar partidas até saída humana/coordenada; não deploy automático. Dialog de espera e diagnóstico limitados. Não restaurar Activities antigas nem remover validações. Fontes/manifestos/recibos completos no app. Responder com produção efetiva e evidências, não somente JSON no Git.

## Histórico anterior

# Station R71 ativo; teste físico com falha — 07/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-PARA-CLIENTE-RECONSTRUIDO-STATION-20261002.md` e `recovery-r71-20261007/PRODUCAO-EFETIVA.json`.
API fonteab192bf1585e30f303d041f13b36a1f9c96d2caa/DLL815fc8bc/PID1230693 ativa às21:59:53UTC;
v2=true/64salas/256KiB por direção,32MiB rings; seis engines exatas, quatro antigas preservadas.
177checks sombra/181públicos,91estado/34social, backup restaurado isoladamente, catálogo14/2212,
nomes próprios assinados independentes da página, chaves/licenças/isolamento/outros produtos preservados.
Somente Station reiniciado; proxy/Cloudflare/firewall/banco de produção sem mudanças.
App novo recibof64f685 confirma R71 completa/SHA556170c3 nos dois Android. Teste após publicação:
Samsung anfitrião preto e Motorola aguardando. Start/ticketaceitos; recovery-failed22:00:59.991UTC
antecede upgrade40122:00:59.993, sem stream/host-listening. Categoria Android exige log USB no PCAPK.
Hipótese JNI: NativeActivity abre ELF sem registro no carregadorJava; preparador limitado e compiladoAPI34
no recibo, nãoAPK/instalação/categoria confirmada. Não reduzir segurança, forçar estadoPlaying ou atribuir
401 à licença sem causa. Não declarar gameplay homologado. Histórico abaixo refere-se a releases anteriores.

## Histórico anterior

# APP R71 completa entregue — integração e instalação A56 — 07/10/2026

**APP → SERVIDOR, não recibo de implantação Linux.** Leia `docs/station-android/ENTREGA-APP-R71-COMPLETA-PARA-SERVIDOR-20261007.md`.
App TurboElden commit `0368bf0586fd6fa1b38ed4c98bcf6cee73810595`, branch `fix/station-online-layout-recovery-r71-20261007`.
APK SHA556170c32b6dd25fb5084693826d854adf736b4a1df156fd9229a8458b025018 instalado/hash
integral A56; UID/data preservados, carrossel aberto depois e sem gameplay validado. Motorola
última R70. Fontes/receitas/testes completos no link exato da entrega,198Java.

Retorno6f8dcead/código32ce9d2b conciliados sobre R70, sem restaurar Activity antiga.
Runtime Windows corrigido d66267cd42507388f86034deb47e9f9670875784efb9afabfb8ffc64e3f3a856,
IDs `bsnes-mercury-performance-79d7f9de-rs2-d66267cd4250` e
`clownmdemu-d43c2708-rs2-d66267cd4250`. Registrar adições exatas preservando v1/IDs
antigos; publicar/ativar v2 conforme plano e devolver recibo efetivo. Não existe
fallback novo→v1. Pedido também exige perfis assinados da própria sala fora de
peers100. Não confundir cache de nomes do app com contrato completo do servidor.

1152verificações JVM/56guardas,46transporteisolado; não são gameplay2Android.
Sem deployLinux por esta entrega documental, sem alteração em licenças/produtos.

## Histórico anterior

# Retomada online candidata entregue após R67/R68 — 07/10/2026

Leia `docs/station-android/RETORNO-IMPLEMENTACAO-RETOMADA-ONLINE-STATION-20261007.md`. Servidor funcional 32ce9d2b30bb23deef17899e10fc285f38ea81ab na branch fix/station-online-recovery-r67-20261007; Android/runtime4d30401a80658dd56666ef10f48d9556b3fdd9e9 na fix/station-online-recovery-after-r67-20261007, pai R68c2a1a6d. Protocolo station-stream.v2 opt-in, sessão lógica retida, offsets/ACK/replay limitado, credencial protegida nova e pausa JNI confirmada/barreira dos dois. Preserva v1, registro antigo, proof/Keystore/licenças e outros serviços.79checks v2,32vetores Java,46TLS/TCP/1.620.000bytes,195Java/DEX e novo runtime b1b9beef; regressões e relayv1 isolado passaram. Feature false por padrão; sem migration.

R68 visual agora instalada no Samsung/APK72ce7c2c; recibos recebidos, apenas carousel60b944cb mudou. Nova montagem exige R68 e preserva cantos retos,55vídeos/DEX30BIOS/controles. Não usar base/empacotador R57/R67 para rebaixar. DEX28 novo idêntico4e912015; DEX35f9520da8, runtime/API26/16KiB e engines novos; Linux candidatos privados em /mnt/DADOS/station-recovery-r67-check-20261007. Fonte/contrato/testes são candidato, **não API implantada/APK montado/instalado/gameplay**. JNI/cores reais, matriz física/consumo/capacidadev2 e causa antiga ainda pendentes; processo reiniciado não recupera RAM.

Produção continua turborama-station-api/PID1147382/da07355; DLL antiga protegida, sem rehash nesta leitura, nenhum restart/configuração/proxy/banco/ROM/chave real alterado. API correta https://app.lzgames.com.br (401JSON sem sessão); turbobox é painel (GET/v1 nele404 esperado). NG-01–07 permanecem separados; BIOSR66 preservada. Publicar/qualificar de forma coordenada com artefatos exatos e devolver recibos antes de declarar estabilidade física.

## Histórico anterior

# APP R67 instalado recebido — implementar retomada online — 07/10/2026

**APP → SERVIDOR; pedido do mantenedor, não resposta ou implantação.** Leia `docs/station-android/PEDIDO-IMPLEMENTACAO-RETOMADA-ONLINE-APOS-APP-R67-20261007.md`. App TurboElden: fonte `0d7a44f371e846a9821426a9082405836e250b0e`, entrega `589d678612880c10e40ef42d23e73451ac2397d9`, branch fix/station-r67-media-security-20261007. R67 instalada no Samsung/hash integral `d746cc02b602162b19509cd44ad7cf751e320de3b52199e7d48e86e9e897228f`, catálogo aberto/sessão, perfil e catálogo HTTP 200 (2212); UID/data original preservados. Outro aparelho não atualizado nesta entrega.

Segurança 213cfce conciliada com R66 atual; DEX 28 e 35 recompilados juntos, DEX 30 da BIOS, runtime 899e e motores preservados. Nove vídeos atualizados,55 vídeos em 720 × 720, 30 fps e sem áudio; carrossel a 30 fps. Não usar Activity ou empacotador R57, não recriar vínculo/chave. Modo concreto de prova/atestação no aparelho ainda não identificado; exige correlação, não nova licença.

Retorno b37c873 foi lido: v1 fecha ambos/Leave; retomada ainda NÃO implementada. Responder REC-01 a REC-09 com código/contrato executável, pausa real, integridade/sincronização, retomada autenticada, diagnóstico do primeiro evento e testes. Não apagar timers/reabrir WSS cru como suposta recuperação. Separar fonte, candidato, publicação e partida em dois Android. Preservar clientes v1, segurança e outros produtos; nenhuma implantação por mera leitura. Recibos em docs/station-android/entrega-app-r67-20261007/.

## Histórico anterior

# Retornos R62–R66 analisados — 07/10/2026

Leia `docs/station-android/RETORNO-ANALISE-HANDOFFS-R62-R66-STATION-20261007.md` e seu recibo. APP→SERVIDOR recebido em7a5db185; app até7d0d3da/implementaçãoR66 291f3949. R63 foi instalada nos dois aparelhos; Samsung depoisR66/e4397fd7 com catálogo aberto, Motorola últimaR63/d9a35602. BIOS CD já existia no APK e a R66 liga a preparação automática aos assets. README/STATUS de preparação conservam história; recibos posteriores identificam instalação. Gameplay CD não capturado. Convite curto/senha automática já integrados.

Queda12:24:07UTC correlacionada: duas conexões abortadas pelo servidor,142s após início, antes do aviso do app; renovação200 às12:23:46. Gatilho inicial/heartbeat individual não provados. v1 ainda cancela ambos/Leave e não permite retomada; Q01–Q08 continuam exigindo implementação coordenada. Inventário das tabelas centrais de189ZIPs originais cruzado com TSV14/IDs/capas; bytes dos chips/pacotes ativos não verificados. NG-01–NG-07 seguem com essas limitações.

API da07355/PID1147382/isolamento preservados. ProteçãoAndroid213cfce derivaR57: conciliar comR62/R63/R64/R65/R66 antes de novo build; não executar empacotadorR57 sobre a sucessora. Preservar DEX30R66, salas/capas, controles/diagnóstico, runtime899e, alias/licença/saves/assinatura. RequireVerifiedApp=false. Análise/documentação apenas, sem nova implantação.

## Histórico da publicação de segurança

# Segurança Station publicada — 07/10/2026

Leia `docs/station-android/RETORNO-SEGURANCA-STATION-20261007.md` e seu recibo. APIda07355/DLL83c8d2b3/PID1147382; usuário e papel próprios, visões Station, arquivos/mídias somente para leitura, outros segredos inacessíveis, rotas Station só pelo túnel local. Backup/restauração,4921arquivos,198checksHTTPS+15segurança e2554relay passaram. Catálogo14/2212, licenças/chaves/quatro motores/outros serviços preservados. Origemdireta404; firewall/SSH/Cloudflare intactos. Migrations031/032 aditivas. Chave simétrica com acesso apenas ao proprietário preservada; gestão validada no socket privado,5187/health sem token404intencional.

App213cfce em versions/station-security-r57-20261007: prova por pedido/ticket, atestação opcional, R55+visualR57+prontidão+senha automática preservados;190Java8/API34 compilaram. NovoDEX28+35/APK/assinatura/instalação/hardware/gameplay pendentes no PC. RequireVerifiedApp=false preserva clientes antigos; não declarar acesso exclusivo ao APK nem segurança absoluta. Usar receitas novas sobreAPKR57/e6159fa3/cert7b16; atualizar ambos sem limpar dados. Retorno guardado exato da07355; scripts antigos recusam/ não abrangem a nova identidade.

## Histórico anterior

# Convites curtos e palavra passe automática — 06/10/2026

Leia `docs/station-android/RETORNO-CONVITE-CURTO-SENHA-AUTOMATICA-STATION-20261006.md`. APIa3e83d96/DLL5fff55c1/PID970425 publicada20h04Maceió. Convite8caracteres, resolução autenticada/assinada sem senha ou entrada implícita; quatro motores no registro, dois originais e dois -autopass1. Runtime899e3527 arm64/API26/16KiB compilado, cores/opções/controles preservados. Backup restaurado, provas sombra/HTTPS/relay e limpeza passaram; catálogo14/2212, licenças, chaves, serviços e mídias preservados. Sem migration/mensagens.

App1e0f862 em versions/station-auto-room-access-r57-20261006 inclui R55+visualR57+prontidãof8. Senha privada usada automaticamente só no cliente Android tipado, NICK antes de PASSWORD e verificador original preservado. 318códigos,278salas/convitesJVM,66nativo,39TCP/TLS/relay,158JavaAPI34 passaram. APK/DEX novo, assinatura/instalação e gameplay físico pendentes no PC; usar receitas novas sobre APKR57/e6159fa3/cert7b16 e atualizar ambos. Não executar receitas R41/R55 nem declarar o diálogo do APK antigo corrigido pelo servidor.

## Histórico anterior

# Retorno R55 concluído considerando a sucessora R57 — 06/10/2026

Leia `RETORNO-ANALISE-APP-R55-STATION-20261006.md`. Base funcional recebida9d3d45f, adendo visual8980cd4; implementação atualf8b019d6 em `versions/station-relay-readiness-r57-20261006` do app. Preservados canal Binder/saída R54 e layout R57: Criar sala, barra fina, capas e faixa INSTALADO. Os snapshots recebidos permanecem intactos.

Composição dos157 Java atuais coincide com o recibo de produção. O delta conserva155 arquivos, altera dois e adiciona um;158 fontes compilaram Java8/API34 em api-check-only. Provas39TCP/TLS/relay e255salas se aplicam aos mesmos componentes, sem repetição por layout. Patch e recusas R41/R55/api-check-only passaram. Não há novo DEX/APK compilado ou instalado, nem gameplay físico comprovado.

Sem alteração de servidor necessária para este delta. APIa2bb176/PID875574, management910766 e helper910776 preservados. No PC usar as novas receitas R57 e o APKbase e6159fa3/certificadooriginal, conferindo todos os outros módulos. Não usar o empacotador R41 ou R55 sobre a sucessora. Testar dois aparelhos, confirmação, inputs, saída e retorno; controles online próprios, latência externa e aquecimento medido continuam pendentes.

## Histórico anterior

# Adendo do app: R57 visual instalada, revisão funcional R55 mantida

Leia [ATUALIZACAO-VISUAL-APP-R57-PARA-REVISAO-20261006.md](ATUALIZACAO-VISUAL-APP-R57-PARA-REVISAO-20261006.md). Fonte atual: TurboElden `8980cd422d63068299b5e9946c120f81a9c94f29`, snapshot R55 + overlay R57. Layout de Criar sala e barra preta compactados; faixa INSTALADO e capas preservadas. Canal Binder/fechamento/runtime/protocolo permanecem R55. Ao responder R55-01 a R55-08, considerar o overlay R57 se alterar `StationRoomsActivity`. Candidato de prontidão d1b535c não integrado; não declarar gameplay corrigido nem implantar por esta leitura.

## Pedido anterior e histórico preservados

# Novo pedido: analisar o APP R55 atual — 06/10/2026

Leia [PEDIDO-ANALISE-APP-R55-STATION-20261006.md](PEDIDO-ANALISE-APP-R55-STATION-20261006.md). **APP → SERVIDOR, pedido do mantenedor; não é retorno nem implantação.** A fonte atual do app foi publicada em TurboElden, branch `review/station-r55-server-20261006`, commit `9d3d45f048aa44bb2ee9c41f567e985901628daa`. APK R55 instalado/hash4c8de4f8; R41 abaixo é histórico. Conciliar o candidato de prontidão d1b535c com `StationSessionChannel`, fechamento idempotente e HUD da R54/R55. Não copiar a Activity da R41 nem executar seu empacotamento sobre R55. Responder R55-01 a R55-08 em `RETORNO-ANALISE-APP-R55-STATION-20261006.md`, citando a fonte exata. Gameplay em dupla, retorno online completo e controles próprios continuam pendentes; não implantar por consequência da leitura.

## Histórico anterior — referências R41 abaixo não identificam o APK atual

# Cadastro de clientes Station publicado — 06/10/2026

Leia RETORNO-CADASTRO-CLIENTES-STATION-20261006.md. Fontefe4b631 publicada às15h45Maceió: Códigos Station → Novo cliente e código, cliente novo/existente, venda paga/cortesia/teste e licença adicional para dois aparelhos. Código30min/uso único; confirmação com senha administrativa. PostgreSQL/SQLite restaurados, testes isolados e dois acessos sintéticos independentes na API pública passaram, limpeza confirmada, zero mensagens/compras. ManagementPID910766/helperPID910776; APIa2bb176/PID875574/catálogo14/2212 e APKR41 preservados. Sem migrationPG; tabelaSQLite aditiva station_registrations. Nova página cadastra; orientação antiga somente de busca/Vendas foi substituída. Usar retorno específico da sucessora; gameplay físico/POCO/latência continuam no retornoR41.

## Histórico anterior — consultar o cadastro publicado acima

# Códigos Station no painel — 06/10/2026

Leia RETORNO-PAINEL-CODIGOS-STATION-20261006.md. Interface17e564a publicada às14h04Maceió: menu Códigos Station, atalho Gerar código do app Station, guia e botões Gerar código/Trocar celular. HTTPS autenticado e hashes dos nove arquivos conferidos; testes sintéticos de emissão/senha/CSRF/troca/cancelamento passaram. Código30min/uso único, um aparelho por licença; novo aparelho simultâneo exige licença própria. Nenhuma licença real alterada, zero WhatsApp, migrations e reinícios. API/comunidadeR41 e APK permanecem os do retorno abaixo.

# Comunidade R41 publicada — 06/10/2026

Leia RETORNO-COMUNIDADE-STATION-R41-20261006.md. API a2bb176530fd4d2dfa740da7e934fd84d097404e, DLL d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13, PID875574; SocialEnabled=true, conversas privadas/pedidos de entrada verificados por três licenças sintéticas no domínio público, 186 checks e WSS com pin. Catálogo14/2212, relay512/1024 e licenças/chaves/outros serviços preservados. Histórico privado até32 e64KiB na resposta. Backup restaurado, nenhuma migration. Retorno Android 5e40f7e confirma R41 instalada no Samsung, hashb6b19321 e dados preservados. POCO, gameplay em dupla, Pessoas/Voltar/correspondência Boogerman–Battletoads e latência externa continuam pendentes. Preservar APK R41; nunca retomar delta R34 sobre essa fonte. Scripts históricos recusam sucessoras; usar retorno/rollback R41.

## Histórico anterior — os blocos abaixo não identificam a publicação atual

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

# Instruções para integrar a TurboramaStation Android neste servidor

Você está no servidor compartilhado `lz-servidor`. Sua tarefa é acrescentar uma licença e um cliente Android próprios, aproveitando comércio, PostgreSQL, autenticação, painel e conteúdo existentes. Leia `README.md`, `INVENTARIO-SERVIDOR.md` e `PLANO-INTEGRACAO.md` antes de propor alterações. Leia também o handoff Android vinculado no README. Execute `bash scripts/inventario-somente-leitura.sh` para obter o estado atual; o inventário datado pode envelhecer.

## Limites do trabalho

- PIX, Suite Windows, EmulationStation Windows, site, WhatsApp, gateway, painel, banco e serviços de outros projetos atendem usuários. Preserve seus fluxos, dados, endpoints, nomes, contratos criptográficos e configurações existentes. A integração Android deve ser aditiva e isolada por produto, aplicação, rota e feature flag.
- Não use `main`, um worktree ou o `WorkingDirectory` de systemd como prova da versão instalada. Registre o `ExecStart` efetivo, os drop-ins e o hash do binário em execução. Os quatro serviços principais atualmente usam diretórios de release diferentes.
- Há mudanças locais não commitadas em worktrees do Servidor-pix. Preserve-as. Faça desenvolvimento em worktree/branch próprios, em armazenamento com espaço, após escolher conscientemente a base compatível com os binários instalados.
- Não altere unidades systemd, drop-ins, Nginx, Cloudflare, firewall, portas, `/opt`, `/etc`, `/var/lib`, dados PostgreSQL, MariaDB, Redis, checkout de produção ou APK por causa da leitura deste handoff. Uma tarefa posterior de implantação precisa indicar o artefato, a migration, o alvo exato e o retorno possível; esses dados devem ser conferidos antes de executar a mudança.
- Não desinstale o APK, limpe dados do telefone, troque certificado de assinatura, modifique emuladores ou retome o trabalho de PS2 por consequência da integração de login. Preserve jogos, saves, capas e configuração do aplicativo.
- Não imprima nem copie DSNs completos, arquivos `.env`, senhas, peppers, tokens, códigos de ativação, chaves privadas, dados pessoais de compradores ou URLs privadas de jogos. Use dados sintéticos nos testes e relatórios.

## Antes de codificar

1. Concilie código, binários efetivos, rotas publicadas e ledger real `suite.schema_migrations`. O diretório de código local contém arquivos até `027`; isso não prova aplicação da migration `027` em produção.
2. Consulte o responsável comercial para definir SKU/plano Android, preço, prazo, quantidade de aparelhos, entrega/reemissão do código e política de transferência. Não reaproveite por suposição a licença vitalícia Windows.
3. Escreva contrato Android versionado e vetores de assinatura interoperáveis .NET/Android. Separe produto, aplicação, licença, aparelho, ação, sessão e desafio nos bytes assinados.
4. Escolha um worktree novo a partir da revisão correta, mantendo os atuais intactos. Desenvolva e teste com banco isolado, licença sintética e rotas Android inicialmente desativadas.

## Para considerar uma implantação pronta

Exija backup restaurável, diff revisado, migrations somente aditivas, testes de regressão PIX/Suite/ES/conteúdo e testes Android de ativação, sessão, revogação, transferência, catálogo e download. Registre hashes, serviço exato, saúde antes/depois e plano de retorno. Uma falha em outro serviço é um limite para a implantação, não um motivo para desativar validações existentes.

Atualize os documentos desta pasta quando descobrir evidência nova. Separe sempre fatos observados, proposta técnica e decisões ainda pendentes.
