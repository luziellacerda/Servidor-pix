# PEDIDO DO APP AO SERVIDOR — espera e retomada de partidas

**APP → SERVIDOR. Este é um pedido novo do mantenedor, não uma resposta nem uma correção publicada em produção.**

Fonte exata disponível para análise: [8812bacf8154f25fd7739f95a2db0729f36e4c72](https://github.com/luziellacerda/TurboElden/tree/8812bacf8154f25fd7739f95a2db0729f36e4c72/versions/station-online-controls-r64-20261007), branch `fix/station-r64-controls-drop-diagnostics-20261007`. Ler as quatro classes alteradas, a composição das 161 fontes e os recibos. O DEX restaurado a partir do snapshot conferiu byte a byte com o candidato. A R64 ainda NÃO foi instalada e NÃO implementa recuperação de rede. R63 segue instalada nos dois aparelhos; o mantenedor confirmou partida online real e controles respondendo antes da queda.

## Ação pedida ao implementador do servidor

1. Correlacionar a queda UTC 2026-10-07 12:24:07.422 e responder Q01–Q08 abaixo com a primeira causa comprovada; não atribuir por suposição ao Android ou à internet.
2. Entregar contrato e implementação versionados de manutenção/retomada da sessão lógica após rompimento do transporte. A partida deve aguardar a volta da conexão, sem encerramento automático por inatividade. O loading no aplicativo deve corresponder à pausa real e ao estado recuperável dos dois jogadores.
3. Devolver fontes exatas do cliente e do motor necessárias à mudança, patches/receita/hashes e registro aditivo de compatibilidade. Conciliar com a R64, preservando controles/HUD/Binder/saída; não restaurar Activities de revisões antigas. A identidade do runtime muda se o binário mudar.
4. Responder em `RETORNO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md`, citando o commit do app acima e este pedido. Separar código, testes isolados, produção e teste em dois Android. Mudança Linux cabe ao operador seguindo o processo autorizado; este envio documental não é implantação.

Anexos sanitizados: `station-r64-recovery-request-20261007/`. Código completo e receitas estão no link do app. Não há APK, ROM, BIOS, credencial, serial ou log pessoal nestes anexos.

---

# APP → SERVIDOR: queda de partida online R63 — 07/10/2026

## Destinatário e ação solicitada

Operador/implementador do Servidor-pix: correlacionar o primeiro encerramento abaixo com os dois participantes, API, relay e proxy. O mantenedor confirmou que conseguiu jogar online, os comandos respondem, mas depois apareceu encerramento espontâneo. Quer que interrupções temporárias aguardem e recuperem sem perder a sincronização. Este documento NÃO é retorno do servidor, NÃO atesta correção e NÃO autoriza deploy Linux por este agente Windows.

## ORDEM ATUAL DO MANTENEDOR — posterior ao diagnóstico

Ele pediu explicitamente: desativar encerramentos automáticos por espera de rede; nenhum motor deve encerrar a partida sem ordem humana; quando cair a internet, apresentar loading **Aguardando conexão…** até voltar.

Comportamento de aceite: interromper avanço da emulação dos dois participantes no ponto sincronizado, mostrar loading sem percentual inventado, conservar estado/input e sessão lógica, renovar o transporte de forma autenticada, confirmar sincronização dos dois lados e retirar loading automaticamente. Nenhum timeout de inatividade deve ordenar fechamento da sala ou saída do emulador. Menu com ação humana **Sair da partida** continua acessível. Reabertura de socket não equivale a retomada confirmada.

Este pedido exige rever a expiração de presença60s/CloseRelay/usedRelayPeers enquanto a partida estiver em espera, com armazenamento e buffers limitados e sem ocupar um slot de socket morto. Presença social pode ficar offline sem destruir a partida. Falhas de autenticação ou licença não autorizam continuar tráfego sem validação: conservar a tela/estado e informar a condição. O proxy/sistema operacional podem romper o transporte físico; a sessão lógica precisa sobreviver. Definir no retorno como retomar após troca de rede e reinício do serviço. Não inventar isso no cliente sobre o protocolo v1.

R64 é apenas a base preparada de controles/diagnóstico. NÃO afirma cumprir essa retomada e não deve ser apresentada como correção da queda. A próxima alteração funcional precisa da resposta contratual do servidor e de runtime compatível registrado, com fontes exatas. A base R63 instalada permanece preservada enquanto o pedido é conciliado.

## Base exata

AppR63: branch fix/station-r63-auto-access-20261006, commit8613d88d4f553e72e5fedcd1d3ea470010301734, APKd9a35602ddc1141617df70d4b2b1f202d8646c538d4508f5fb3e53ef6b7abc4c, instalado/hash integral igual no SamsungA56 e MotorolaEdge30. Runtime899e35279cf046242a7ae31f502078080bd6a94404770fe99e1e2e90be58a1ef. SNES bsnes-mercury-performance-79d7f9de-autopass1. Jogo observado Battletoads & Double Dragon; anfitrião Samsung conectado à USB. O nome completo/edição da ROM não foi inferido da imagem. O último retorno integrado foi8d9c670ba813fb970a61ff9bf329e6286b51ad07.

Código do servidor examinado: a3e83d96b8025cde058e267eaa74e4505f654b15, conforme último recibo de produção. Não consultamos processo/DLL/logs Linux ao vivo; o operador deve confirmar se continuam sendo a versão efetiva. Fetch de todos os ramos não trouxe correção posterior de retomada.

## Primeira evidência e o que falta

Horário localUTC-03, em07/10/2026:

- 09:21:45.366 /12:21:45.366Z: native-listening host.
- 09:21:45.408 /12:21:45.408Z: local-stream-ready host.
- 09:21:45.549 /12:21:45.549Z: host-listening-ack.
- 09:24:07.422 /12:24:07.422Z: relay-failed reason=RELAY host. Cerca142segundos após a prontidão.
- A Activity nativa continuava retomada na captura posterior. Não há prova de saída voluntária/local nem log suficiente para excluir uma transição anterior de ciclo de vida.
- Também existem avisos JNI StackOverflowError entre09:22 e09:23; contagem/primeiro/último constam em evidence/drop-observation.json. Sem stacktrace útil, NÃO atribuir a eles a causa da queda nem descartar este achado. Capturar a origem com diagnóstico nativo antes de modificar o runtime.

O códigoR63 colapsa onClose(código,motivo,origem) e onError(exceção) em RELAY. Portanto os dados atuais NÃO distinguem fechamento do outro telefone, watchdog de pong, servidor, proxy, TLS ou perda de transporte. Ausência de session-sync erro não prova heartbeats recebidos: só os erros eram registrados. Nenhum raw log/serial/segredo foi publicado.

## Mapa comprovado do encerramento

1. App StationRelayTunnel: connect timeout10s; host listener45s; client accept60s; esperaWSS15s; setConnectionLostTimeout20s. A biblioteca Java-WebSocket compara lastPong com1,5×20s nos ticks de20s — NÃO descrever como desconexão exata aos20s. onMessage escreve de forma bloqueante no TCP nativo; se esse consumidor parar, também pode impedir a thread de processar pong. É risco de código, não causa provada neste evento.
2. RetroArch69a4f0ea: network/netplay/netplay_private.h MAX_SERVER_STALL_TIME_USEC5s e MAX_CLIENT_STALL_TIME_USEC10s; netplay_frontend.c netplay_sync_pre_frame considera hangup para stall com remoto não pausado. Não é timeout genérico de toda operação. Pausa remota renova stall_time. PatchesStation já publicados não alteram esses limites.
3. App StationGameSession: heartbeat via sessão assinada a cada20s em scheduleWithFixedDelay, portanto duração da requisição soma ao intervalo. API compartilha lease de sessão. R64 passa a registrar duração/sucesso/sameRoom, sem credenciais.
4. Servidor StationOnline.Sweep/RelayCurrentLocked: remove presença aos60s sem Seen; RelayCurrent/Watch consultam a cada2s. Confirmar últimoheartbeat dos DOIS participantes e causa que invalidou a lease.
5. Servidor StationRelay.Attach: quando um participante termina/falha, finally cancela pair.Stop e aborta ambos os WebSockets; CloseRelay chama Leave. Isto encerra a participação/sala. Não há grace period de transporte nesse contrato.
6. Proxy publicado emops/nginx-v1-station-relay.conf tem send/read timeout120s; é intervalo de inatividade de IO, não duração máxima de partida. Conferir configuração realmente carregada; os142s observados não provam timeout120s.
7. TakeRelayTicket consome ticket, e usedRelayPeers impede reanexação. Abrir outroWSS reutilizando ticket ou continuar bytes em socket novo SEM protocolo de retomada é incorreto: pode perder/duplicar comandos e dessicronizar a emulação.

## Responder Q01–Q08 com evidência

Q01. Serviço/DLL/commit/proxy efetivos e relógios; correlacionarUTC12:21:45–12:24:08, os dois lados e geração. Não publicar credenciais ou identificadores pessoais.
Q02. Qual evento veio primeiro: ReceiveAsync Close/exception, aborted, pair.Stop, Watch lease invalid, heartbeat expired, client close ou upstream/proxy? Informar código WebSocket, tipo de exceção e lado, lastSeenAge e bytes por direção; não usar apenas total agregado do serviço.
Q03. Últimos heartbeats aceitos/rejeitados de cada participante, duração, status e erro, preservando autenticação. Houve token expirado/renovação, espera de lease, mudança de processo ou reinício?
Q04. Confirmar se a sessão sobrevive a atrasos sintéticos de1/3/6/10/20s com sockets ainda vivos; registrar primeiro ponto em que o nativo/ponte/servidor encerra. Não substituir isso por teste só de transferência contínua.
Q05. Propor contrato explícito, versionado e autenticado para recuperação após rompimento real: espera sem encerramento por tempo de inatividade, conforme ordem explícita do mantenedor, identidade room/generation/peer preservada, nova credencial individual/usoúnico, autorização dos membros e revogação/Leave imediatos. Registrar comportamento com ambos ausentes, troca de rede e reinício. Nenhuma chave fixa; tentativas de reconexão com espera progressiva e sem laço ocupado. Não consumir CPU continuamente enquanto offline.
Q06. Para conservar streamTCP, exigir offsets/sequências e acknowledgements, replay limitado, deduplicação e backpressure; se a opção for reinício sincronizado do netplay, explicitar state/hash e aceitação dos dois. Não reenviar bytes às cegas e não forçar connecting. Manter v1/R63 compatível aditivamente até atualização dos dois aparelhos.
Q07. Se mudar limites no runtime, devolver política negociada e registro aditivo para os NOVOS hashes/engineIds; o app não pode lançar um .so modificado com hash899e3527 ou IDautopass1. Publicar fontes/patches/receita e testes com os limites, pausa real, espera sem encerramento automático, perda de rede e encerramento voluntário. Não basta aumentar um número no cliente.
Q08. Devolver quais alterações são cliente/servidor/proxy, testes isolados versus produção e o que depende de doisAndroid. Arquivo de retorno sugerido: RETORNO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md; citar a revisãoR64 quando disponível, preservando controles/saída/Binder/R63.

## R64 preparada no Windows

Nova apresentação dos controles e diagnóstico; não modifica runtime, protocolo, timers ou servidor. Veja README e BUILD-RECEIPT da revisão. Quatro Java alterados sobre os161R63;157 preservados. Novo trace registra code/peer/pongAge/localWriteMs/sentBytes/receivedBytes e tipo de erro, descartando reason recebido e dados sensíveis. Heartbeat e onStop ganham evidência. Snes/Mega preservam bindings do core; desenho vem dos controles locais. Capturar nova queda nos dois telefones em sequência se só houver uma USB; não interromper partida para instalar.

## Critérios de fechamento

- Mostrar "Aguardando conexão" somente enquanto a sessão puder ser recuperada; ocultar ao recuperar, preservando posições/inputs dos dois.
- Diferenciar atraso recuperável, partida encerrada pelo participante, licença negada e transporte ainda indisponível.
- Nenhuma credencial/ROM/save em logs ou Git; sem desinstalação/limpeza de dados.
- Demonstrar pausa/retomada com conteúdo legal de teste e gameplayAndroid separado. Não declarar a queda específica corrigida sem correlação do primeiro evento e nova prova.


## Aceite específico do loading e retomada — pedido mais recente

Tela de jogo em espera: mensagem **Aguardando conexão…**, indicador indeterminado de baixo custo e ação **Sair da partida** com confirmação humana. Sem porcentagem, contagem regressiva para expulsão, navegação automática às salas ou promessa de tempo para reconectar. Ao recuperar a rede, continuar mostrando a espera até o outro participante e o estado sincronizado estarem confirmados; apenas ter internet não basta.

Estados aqui são REQUISITOS, não campos novos já implementados no servidor: jogando → espera de conexão → ressincronização → jogando. Sair voluntariamente encerra. Uma sessão inacessível ou licença recusada mantém aviso claro e o estado local conservado; não simular que a partida pode continuar sem validação. Desligamento do aparelho/processo elimina memória volátil: definir recuperação persistida separadamente, sem prometer recuperação após qualquer crash.

Matriz obrigatória para o retorno: perda só do host, só do convidado, ambos, atraso sem romper socket, modo avião, troca Wi-Fi/dados, desconexão acima de 60 e 120 segundos, heartbeat lento, expiração/renovação de autenticação, saída humana enquanto aguarda e reinício do relay. Para cada caso informar se a sala sobreviveu, ponto de pausa dos dois lados, integridade/sequência dos bytes, confirmação de sincronismo, memória/CPU e resultado ao voltar. Teste sintético não substitui gameplay em dois aparelhos.

Nenhuma alteração deve retirar validação de TLS, ticket, licença, geração, conteúdo ou compatibilidade do motor. Inatividade de rede não deve ser convertida em intenção de sair. Comandos inválidos e falhas reais precisam ser distinguídos de atraso para evitar travamento indefinido disfarçado.
