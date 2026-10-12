# Incidente: três jogadores, espera de sincronização e saída humana

## Status e evidência

O relato de uma partida com três jogadores que não passou do loading e de dificuldade de saída permanece sem jogo/plataforma, trace Android e identificação pública do APK do terceiro participante. A auditoria reproduziu condições do domínio que podem explicar esses sintomas. Não foi feita consulta a produção, teste físico novo, operação USB ou deployment.

Base do runtime: `a6b58e9beb12eb564c1b58d9d4c32ed3e7513861`. Base documental do checkout: `524d2ebff08353fda62aacbc5f05f9248ba69b7c`. As 117 branches foram consultadas; não foi localizado recibo público posterior de ativação do kit R134.

- [Auditoria e matriz detalhada de contratos](SERVER-THREE-PLAYER-AUDIT.json).
- [Recibo da execução local](SERVER-THREE-PLAYER-VALIDATION.json).
- [Pré-requisitos do kit R134 e aplicação administrativa](HANDOFF-APP-R134-PARA-SERVIDOR-20261011.md).

## Condição reproduzida: uma READY ausente

Três jogadores formam um host e dois convidados: dois links de topologia host-star e **quatro attachments**. A barreira exige todos anexados, HELLO/PAUSED válidos e sem participante suspenso. A transição para Playing exige as quatro READY da mesma epoch e todas as janelas de replay com pending zero.

No teste com as classes reais `StationMultiplayer`, `StationMultiplayerStream` e janelas de replay, quatro attachments enviaram HELLO/PAUSED; apenas três enviaram READY. Pending ficou zero e o estado continuou Synchronizing. Avançar cinco minutos no relógio **sintético** do hub, com heartbeat dos três peers e PING dos quatro endpoints, não fabricou a confirmação ausente. A quarta READY válida permitiu Playing.

O código não impõe deadline global de início/sincronização. O Watch verifica lease, heartbeat, frames recebidos e autorização; ele não substitui READY. Com tráfego de vida saudável, uma READY ausente ou replay que não drena pode manter a espera. Isso é uma condição comprovada, não a atribuição da falha física a um aparelho ou núcleo específico.

Fontes: `StationMultiplayer.cs:185-192`; `StationMultiplayerStream.cs:59-95,149-159` na base a6b; `BarrierDiagnosticsChecks.cs` nesta proposta.

## Saída: generation antiga é recusada

`Own` verifica sala, membership e geração exata antes de executar leave. Start aumenta generation; epoch do stream é outro contador. Leave com generation anterior retorna `409 STATION_MULTIPLAYER_GENERATION_MISMATCH` sem retirar membership. O novo teste confirma que ownRoom permanece presente depois desse erro.

O caminho herdado de retorno do cliente enviava uma tentativa com a generation do binding e ignorava exceções. O handoff de cliente prepara um helper de saída humana que, **somente após GENERATION_MISMATCH**, obtém snapshot assinado fresco, confirma a mesma sala, protocolo, item, engine, core/runtime/content e profile/hash, e usa a nova generation. Se a própria sala já estiver null, pode confirmar saída. Não deve sair de outra sala nem repetir mutação por erro de rede/500 ambíguo sem reconciliação.

A proposta de servidor conserva a validação. Em sala iniciada, leave humano de qualquer membro aposenta a sala inteira; perda de rede não escolhe saída lógica. A reserva só é liberada depois do último detach. Nenhuma remoção automática por tempo foi acrescentada.

Fontes: `StationMultiplayer.cs:193,216-225,270-287`; cliente `StationGameSession.java` R124 herdado e helper separado da publicação Android.

## Patch proposto: diagnóstico por membro

`room.barrierDiagnostics` é aditivo e aparece apenas na própria sala dos membros. Salas na listagem pública recebem null. O campo é opcional até existir deployment confirmado.

Contém contagens, flags e offsets: schemaVersion, epoch, state, hasPlayed, linkCount, expectedAttachments, attachedConnections, helloCount, pausedCount, readyCount, pendingBytes, suspendedParticipants, waitingReasons e uma lista por ordinal do link/lado host. Não inclui IDs de sala/link/peer/licença/dispositivo, tickets, senha, token, conteúdo de jogo ou payload.

Razões: `missing-attachment`, `awaiting-hello`, `participant-suspended`, `awaiting-core-pause`, `awaiting-ready`, `pending-replay`, `unrecoverable`. A razão de pausa indica frame PAUSED ausente, não uma medição de CPU. Várias razões podem coexistir.

Em Playing, waitingReasons fica vazio por definição: DATA durante tráfego normal pode invalidar as flags de READY do par tocado sem criar uma nova pausa. Contagens abaixo de quatro em Playing não devem ser interpretadas como uma barreira bloqueada.

`SnapshotWithDiagnostics` captura o estado, epoch, hasPlayed e diagnóstico sob o mesmo lock; evita mistura entre épocas de leituras concorrentes. O patch não muda Attach/Detach/Receive/Next/End/RetireRoom, comandos, autorização, geração, perfil, timeout ou reserva.

## Testes executados e limites

A suite real de domínio `tests/StationMultiplayer/StationMultiplayer.Tests.csproj` em Release passou:

- **1452 checks totais do domínio**, incluindo **29 novos** para três jogadores/diagnóstico.
- Capacidade sintética: 320 participantes, 80 salas, 7864320 bytes bidirecionais; teto de replay configurado nesse fixture de 134217728 bytes.
- Fixture de três: 3 jogadores, 2 links, 4 attachments; reserva de 1048576 bytes (1 MiB); avanço de 300000 ms no relógio sintético.
- `git diff --check` passou.

Os 29 checks cobrem ausência de attachment/READY, isolamento do não membro, epoch coerente, ausência de avanço automático por PING, último READY, generation antiga, leave humano, liberação tardia da reserva, replay de três bytes, invalidação só do par tocado, READY com watermark antigo recusada, ACK insuficiente sem READY final e Unrecoverable explícito.

O restore inicial foi bloqueado pela rede do sandbox. Foi resolvido com três pacotes públicos que já existiam no cache local, por feed temporário fora do checkout. O csproj conserva target net8.0 e dependências originais; somente inclui o teste novo.

**Não foi executada nesta validação a suite HTTP `HttpChecks.cs`, API geral, TLS/WAN, banco produtivo, ativação real, USB ou gameplay Android.** Os cinco minutos são relógio de fixture, não uma observação física de jogo por esse tempo.

## Requisitos para fechar o incidente

1. Aplicar o kit privado R134 correto pelo administrador Linux e guardar recibo nativo, preservando baseline e validações. IDs do handoff R133 são anteriores aos quatro IDs finais R134.
2. Obter resposta fresca assinada de multiplayerVersion3/capability station-multiplayer.v3 e perfil exato de conteúdo/core/runtime/modo. Para testar três, `allowedPlayerCounts` deve conter 3: maximumPlayers4 com [2,4] recusa 3 corretamente.
3. Em três aparelhos, observar 4 attachments, HELLO/PAUSED/READY da mesma epoch, pending zero e gameplay/vídeo/áudio/inputs nos três.
4. Reproduzir ausência controlada de READY com tráfego de vida e observar a razão; testar interrupção de rede, background/foreground e replay sem remover participante automaticamente.
5. Testar saída humana de host e convidado, generation antiga e snapshot fresco, ownRoom null para todos, retorno ao lobby sem ANR e reserva zero após detach.
6. Guardar apenas contadores, categorias e hashes públicos. Não publicar tokens, senhas, IDs pessoais, serial USB, conteúdo ou caminho privado de ROM.

Publicação de Git e deployment são etapas distintas. Esta entrega é fonte, teste local e handoff; não declara ativação produtiva nem homologação física de três jogadores.
