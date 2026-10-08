# Pesquisa APP → SERVIDOR — estabilidade R74

Fonte integral e evidências no app `f3f2cc9d63fe4261921b07754d3f1255b5e31484`, branch `fix/station-neogeo-collection-map-r75-20261007`.
[Documento original](https://github.com/luziellacerda/TurboElden/blob/f3f2cc9d63fe4261921b07754d3f1255b5e31484/docs/server/PESQUISA-ESTABILIDADE-R74-20261007.md).

Esta é uma solicitação ao operador, NÃO resposta do servidor ou implantação.
R74 está instalada nos dois aparelhos. R75 é uma candidata visual que corrige
cinco nomes de vídeos; seu motor online é idêntico à R74. Não cadastrar engines
novas por causa da R75 nem instalar Activities antigas. O pedido R74 8d48252
continua como autoridade dos IDs rs4/runtime804b2acfea4c e das oito questões.

O mantenedor relata menos travamentos. A pesquisa distingue prova isolada,
revisão de código, hipóteses e medições ainda necessárias. Responder SRV-01/02/03
e cruzar APP-01/EXP-01 com o app; não executar a prova isolada contra produção.

---

# APP → SERVIDOR — pesquisa de estabilidade e medições da R74

## Destinatário e estado exato

**Destinatário: operador do Servidor-pix, somente serviço Station.** Este documento é uma solicitação do aplicativo. Não é retorno do servidor, autorização para reinício durante partidas ou afirmação de implantação. Preservar os demais serviços/produtos e dados.

Pedido do mantenedor: pesquisar soluções existentes na web, comparar com o código e melhorar a estabilidade sem adivinhar. Fontes primárias consultadas em 07/10/2026 local / 08/10/2026 UTC. O mantenedor relatou que a versão atual apresenta melhora grande e menos travamentos. Isso é relato de uso; não equivale a homologação prolongada.

- App executável R74: `557014b4ff5ec5c3c0162847d922c0587f68b0e9`; recibos em `02b78919862ac40ea08ddff284e65c604b6d7f9f`.
- APK R74 instalado nos dois aparelhos: `e56896f28b16645653bfd28311cd0a8a9a5458e6d443849916a4dede6dc7fafe`.
- Runtime: `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`; protocolo `station-stream.v2`.
- Último retorno real encontrado após fetch de todos os ramos: `815ceaca49baa0feb1e2d61726c0346ddad2bd37`, branch `fix/station-r73-engine-registry-20261007`.
- Código de produção declarado nesse retorno: `ab192bf1585e30f303d041f13b36a1f9c96d2caa`. Não houve nova consulta direta ao Linux nesta pesquisa.
- Pedido anterior R74: `8d48252fb7e91af83b6138afa411d5c2607edcc8`, `docs/station-android/ENTREGA-APP-R74-LIFECYCLE-LATENCIA-20261007.md`. Suas oito questões continuam válidas.
- R75 corrige somente nomes de vídeos das coleções Neo Geo. Mantém exatamente o motor, DEX, manifesto e registro R74; **não exige novos IDs de engine**. Não confundir número visual do APK com revisão do motor.

A tentativa de ler registros atuais terminou por desconexão USB; a última enumeração não mostrava aparelhos. Não há medição física nova de RTT, FPS, CPU ou queda nesta rodada. Nenhuma partida foi interrompida.

## Separação de responsabilidades

| Referência | Responsável | Evidência / ação |
|---|---|---|
| SRV-01 | Servidor | Capturar primeira causa e epoch antes de `Detach`; valores atuais são posteriores. |
| SRV-02 | Servidor | Completar handshake Close normal no escritor único; distinguir transporte quebrado. |
| SRV-03 | Servidor | Medir DATA/PONG no caminho completo e filas, com relógio monotônico. |
| APP-01 | Aplicativo | Perda de sinal imediato na fila reproduzida em64/64 intercalações; candidato isolado corrige64/64. Ainda não aplicado ao APK. |
| EXP-01 | Ambos | Ensaio controlado do atraso de entrada oficial, somente após medidas; sem preset arbitrário. |

## SRV-01 — instante do primeiro erro e classificação de epoch

Código examinado: [StationRecoveryRelay.cs no commit exato](https://github.com/luziellacerda/Servidor-pix/blob/815ceaca49baa0feb1e2d61726c0346ddad2bd37/src/TurboRamaSuiteOnlineServer/StationRecoveryRelay.cs#L198).

`First()` nas linhas200–201 retém horário/causa/tipo/código. No `finally`, linha222, `Stream.Detach` modifica estado e epoch; o snapshot usado no evento `first-transport-end` é obtido nas linhas223–226, depois dessa alteração. Portanto `streamEpoch` desse evento não é garantia do epoch no instante da primeira causa. Offsets também podem representar estado posterior.

Implementar campos distintos e documentados: `epochAtFirstCause` e `epochAfterDetach`, papel da conexão, estado, causa tipada, offsets/bytes pendentes no instante de `First`, timestamp UTC e duração monotônica. Não reinterpretar silenciosamente os logs antigos. A captura deve ser limitada, sem conteúdo dos jogos, tickets, tokens, IDs pessoais ou caminhos privados. Não manter lock durante I/O de logging.

Teste requerido: provocar separadamente EOF, Close1000, cancelamento do request, falha de leitura, falha de envio e saída humana. Conferir uma única primeira causa de término, epoch correto antes/depois, geração estável e preservação do stream recuperável. Testar também Suspend11 e NeedSync13 como transições de recuperação; quando não houver término WSS, não fabricar um evento `first-transport-end`.

## SRV-02 — fechamento WebSocket normal

No mesmo arquivo, linha209 recebe `Close` e retorna; linha222 sempre termina com `Abort()`. Na implementação .NET8, receber Close muda o estado para `CloseReceived`, mas não envia a resposta automaticamente. A RFC exige resposta Close quando ainda não foi enviada. Isso pode tornar um encerramento voluntário indistinguível de uma falha abrupta para a outra ponta; **não prova a causa dos travamentos durante jogo ativo**.

Tratar encerramento voluntário pelo mesmo escritor serializado, com finalização limitada; preservar Abort para transporte quebrado. Não executar dois envios simultâneos nem transformar a perda de WSS em descarte da partida retida. Reconciliar esse comportamento também com o cliente antes de afirmar fechamento gracioso ponta a ponta.

Testes: Close1000 com envio em andamento; Close remoto simultâneo ao cancelamento; transporte cortado; cliente que não termina handshake; reconexão autorizada após cada caso. Exigir ausência de dupla finalização e de corrida de escritores.

Fontes: [RFC6455 §5.5.1](https://www.rfc-editor.org/rfc/rfc6455.html#section-5.5.1), [ManagedWebSocket .NET8](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/libraries/System.Net.WebSockets/src/System/Net/WebSockets/ManagedWebSocket.cs), [concorrência de SendAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.websockets.websocket.sendasync?view=net-8.0).

## SRV-03 — medir o percurso real dos comandos

O p95 de0,1756ms publicado mede `StationOnline.Command`; não mede WSS, proxy, fila de envio, rede ou recepção no outro aparelho. RTT de PONG no app é ida e volta app↔servidor; também não é atraso do botão até o core remoto.

Medir separadamente, por direção e tipo DATA/PONG:

1. Recebimento completo → aquisição do lock.
2. Entrada na janela → seleção em `Next()`.
3. Início → término de `SendAsync()`.
4. Contagem/bytes, p50/p95/p99/máximo, bytes pendentes, maior idade do DATA e controles consecutivos antes dele.
5. Pausas GC, fila/threads do ThreadPool, retransmissões TCP, erros e configuração efetiva dos proxies no mesmo intervalo.

Agregar métricas em memória com limites e amostragem; não registrar payload nem criar um log síncrono por frame. Correlacionar janelas com os eventos sanitizados da sessão. Medir monotonicamente; tempos UTC de aparelhos diferentes não substituem relógio sincronizado para atraso de uma direção.

Hipóteses a testar, ainda sem prova de impacto físico:

- `Next()` prioriza State → Accepted → Pong → Data. Quantidade elevada de controles pode atrasar DATA. Medir antes de adotar justiça entre filas; ACK/controle continuam necessários para o crédito e a barreira.
- `PublishState()` entra no lock global após cada frame mesmo sem mudança de fase. Medir contenção e só publicar transições se a semântica dos consumidores permitir.
- Pressão de alocações/GC e espera no ThreadPool. CPU total baixa não elimina essas hipóteses. [Diagnóstico Microsoft](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-threadpool-starvation).

## Padrões que já estão corretos e devem ser mantidos

Na base examinada há um escritor e uma leitura por WebSocket, `SendAsync` fora do lock, endpoint aguardando `Attach()` e sinalização por `TaskCompletionSource` com continuações assíncronas. Não existe espera fixa de100/250ms entre DATA/PONG no servidor. Preservar essas propriedades. [ASP.NET WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-8.0).

O proxy versionado possui UpgradeHTTP/1.1, buffering/cache desligados e timeout120s. É preciso conferir o arquivo efetivo no Linux. `proxy_read_timeout` mede intervalo entre leituras, não duração total da partida; PONG/DATA já ativos afastam a conclusão automática de que uma partida ultrapassou o limite. [Nginx WebSocket](https://nginx.org/en/docs/http/websocket.html), [proxy_read_timeout](https://nginx.org/en/docs/http/ngx_http_proxy_module.html#proxy_read_timeout).

O projeto usa .NET8; não copiar propriedades de versões posteriores sem conferir compatibilidade. Migrar para Channels/Pipelines/SignalR, aumentar filas ou remover timeouts não constitui correção demonstrada. As filas precisam preservar todos os bytes e offsets; descarte quebra o TCP emulado. [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels), [Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines).

## APP-01 — sinal perdido entre o esvaziamento e a liberação do escritor

Fonte R74: `StationRecoveryTunnel.java`, SHA256 `8af5ceb585498512ff9ff4e1409a46ee2ed9dca9ee66bcb0c43bc11fa7817896`. `pump()` usa `pumpPending.compareAndSet(false,true)`. Ao decidir que não existe frame, sai do lock, retorna e só libera `pumpPending` no `finally`. Se um produtor acrescenta DATA e chama `pump()` nesse intervalo, encontra a marca ainda ativa e não agenda outro trabalho. O escritor termina sem enviar os bytes novos.

**Prova isolada executada:**1.764 verificações;64/64 intercalações controladas reproduziram a espera por um próximo sinal. Os bytes ficaram preservados, sem perda. Um candidato que retém o pedido de trabalho durante a drenagem enviou em64/64 sem depender de um novo tick. O teste extrai o método `pump()` e o trecho produtor reais, utiliza `StationRecoveryWire.Bytes`/encode/decode reais e insere uma barreira determinística apenas na janela de agendamento. Socket/JNI são simulados; não houve tráfego, Android ou servidor.

Receita e evidências: `research-r74-stability-20261007/run_pump_probe.py`, `PumpRaceProbe.java.in`, `pump-race-receipt.json`, `pump-race-output.txt`. No app estão sob `docs/server/`; na cópia do servidor, sob `docs/station-android/`. A implementação candidata existe só na fixture gerada emE:; **não integra R74 nem R75**.

Classificação: erro de agendamento com atraso evitável. Um próximo evento pode resgatar a fila antes do tick configurado a250ms; essa cadência não é uma medida de atraso real nem limite absoluto se o executor estiver congestionado. Não prova a causa das quedas físicas ou de todo engasgo. Não reduzir o tick para esconder a corrida.

Próxima implementação do app: preservar escritor único, registrar pedido pendente e reavaliá-lo ao liberar o escritor. Conferir chegada antes/durante/depois da saída, rejeição do executor, fechamento humano, transporte substituído, ACK, crédito esgotado, fila cheia e retomada. Executar as suítes reais de TLS/WSS/TCP e sessão além do probe antes de compilar outroDEX. Nenhum novo endpoint, timeout menor ou bypass de autenticação é necessário. A sinalização deve respeitar as garantias atômicas e do executor: [Java AtomicInteger](https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/util/concurrent/atomic/AtomicInteger.html).

## EXP-01 — custo de replay e atraso de entrada

O RetroArch usa os estados dos controles de cada quadro; quando os dados tardios diferem da previsão, restaura um estado e reexecuta quadros. Entrada constante pode custar menos que mudanças frequentes. Essa é uma explicação possível para o relato do mantenedor, não diagnóstico físico concluído. O transporte precisa manter entrega íntegra e ordenada. [Documentação oficial](https://docs.libretro.com/development/retroarch/netplay/).

Na R74, `StationRetroLaunch.java:34` define `netplay_input_latency_frames_min=0` e `range=0`; esse também é o padrão upstream. Não é erro por si só. O código oficial permite trocar um pouco de resposta imediata por menor trabalho de replay. O anfitrião transmite limites ao convidado; alterar só o convidado não testa a opção. `range` é adicional ao mínimo:2/2 significa2–4quadros.

Base upstream: [netplay_frontend.c, commit69a4f0ea](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/network/netplay/netplay_frontend.c), envio1295–1311, recepção6058–6075, limites6831–6838 e adaptação7566–7595. A árvore local R74 contém alterações adicionais e outra numeração: `E:\R74fixed\RetroArch-69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576\network\netplay\netplay_frontend.c`; previsão3127–3160, decisão3898–3929, replay3960–4029, envio1428–1444, recepção6584–6603, limites7417–7426 e adaptação8211–8242. [Ajuda oficial das opções](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/intl/msg_hash_us.h).

Ensaio proposto, **não aplicado**: A=0/0; B=2/0 (~33ms em60fps); só depois considerar0/2. Mesma cena/jogo/core/runtime, aparelhos e rede, controle de temperatura/carga/brilho, alternando anfitrião. Registrar episódios/profundidade/tempo de replay, quadros normais, áudio, apresentação e filas. FPS sozinho não conta o trabalho adicional. Não prometer redução de aquecimento pela metade.

`optionsSha256` atual cobre `game.options`/`core-options.cfg`, não esses dois parâmetros em `retroarch.cfg`. Esse limite deve ficar explícito; uma futura opção de sala precisa contrato/UI documentado. Instrumentação que altere a biblioteca nativa produz novo runtime e exige registro exato correspondente. Não reutilizar identidades antigas.

Perfetto pode distinguir CPU de espera; Simpleperf requer app depurável ou profileable. Não reduzir segurança só para capturar. Não usar FPS da interface como FPS da superfície nativa: FrameTimeline declara limitação para SurfaceView. [Perfetto](https://perfetto.dev/docs/getting-started/system-tracing), [Simpleperf](https://android.googlesource.com/platform/system/extras/+/refs/heads/main/simpleperf/doc/android_application_profiling.md), [FrameTimeline](https://perfetto.dev/docs/data-sources/frametimeline), [Android ANRs](https://developer.android.com/topic/performance/anrs/diagnose-and-fix-anrs).

## Retorno esperado do operador

Responder SRV-01/02/03 individualmente, com commit de código, testes, versão .NET, DLL/registro/PID/horário realmente ativos e estado de implantação. Informar quais correções são candidatas e quais chegaram à produção. Manter a resposta às oito perguntas R74 e confirmar seus IDs/hashes no registro efetivo.

Não reiniciar com partidas abertas: estado recuperável permanece emRAM. Coordenar a janela com o mantenedor. Homologação: dois aparelhos na mesma engine, sala nova, ambos papéis, comandos reais, saída/retorno, interrupção de rede controlada e sessão prolongada com métricas. A melhora relatada não elimina essa etapa.
