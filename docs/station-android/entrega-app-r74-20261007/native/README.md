# R74: ciclo de vida, fila de entrada e espera normal do netplay

## Base e limite da alteração

Base somente leitura: `E:\R73fixed\RetroArch-69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`.
Biblioteca R73: SHA-256 `9af2778898e4ba026d65d9b5c74ef3d8089e58bdbdf9be40f8e28f0eedcb14c2`.
Os seis arquivos e seus hashes estão em `native-delta-manifest.json` / `OVERLAY-MANIFEST.json`.
O consumidor da entrega aplica `source/<caminho>` sobre sua cópia da R73 e recompila.
O teste e o relatório não equivalem a instalação ou teste de partida no Android.

Não há alteração de protocolo, framing, buffers de recuperação, core, controles, ROM,
credenciais, renderizador ou temporizadores. O frontend muda somente pela retirada
da escalada indevida de um stall normal para uma ressincronização externa.
A função completa `station_netplay_recovery_poll()` continua byte a byte igual à R73,
SHA-256 `d3a42419124dda592fea217088ed4e4687bf63718f657299ac8d73ad9fa41e12`.
O frontend completo passa de `3dea62f27a197f2f4fa7c689a0ed09bbe8840ff1e00bf74edb299a2f40d40afa`
para `b1d0f5ddea0c1fb2d675fd08b73503fd80b03c71a6a60c8dea7084a96950d4fe`.

## Defeitos tratados

1. Na R73, `android_app_free` adquiria o mutex e fazia `join` do thread que precisava
   desse mesmo mutex para finalizar. Também faltava pedir encerramento ao motor
   quando a saída começava no Java. A R74 pede saída e aguarda a condição com o
   mutex liberado durante a espera; somente então faz `join`, fora do mutex.
2. As esperas de mudança de Activity, janela, entrada e estado salvo agora também
   terminam se o motor já finalizou. Não aguardam uma resposta de thread encerrado.
3. O estado estático de recuperação é reinicializado ANTES do novo thread nativo.
   `configure(enabled)` continua alterando apenas a habilitação: não apaga um epoch
   recebido cedo pelo transporte. Reconexões da mesma partida NÃO reinicializam o estado.
4. A espera da recuperação retornava antes de `core_run`, omitindo o polling da
   fila Android. A R74 chama `input_driver_poll()` antes da checagem do runloop,
   somente enquanto está esperando recuperação. Isso drena/confirma toques e
   processa o encerramento explícito, sem executar `retro_run`, rollback ou frames.
5. Removidos os dois hooks legados síncronos de shell externo, `/sdcard/switch` e
   `/sdcard/reset`, exclusivamente desta biblioteca online. Nenhum script foi lido
   para execução, executado ou modificado. Não há dependência de contrato Station
   nesses caminhos; eram chamadas soltas cujo resultado sequer era consumido.
6. O ramo `NETPLAY_STALL_RUNNING_FAST` marcava um booleano persistente. Mesmo
   após os dados do outro jogador chegarem e o stall acabar, esse booleano ainda
   fazia o tick Java enviar SYNC13 e pausar a partida novamente. Removida somente
   a chamada dessa marcação e seus dois delimitadores `#ifdef/#endif` (três linhas).
   Permanecem os mesmos limites de quadros, o polling, a histerese, a chamada
   `netplay_sync_input_post_frame(netplay, true)` e o retorno de espera normal.
   A espera após perda real do WSS, seus offsets e o protocolo de recuperação
   permanecem inalterados. O JNI antigo de status é preservado por compatibilidade.

## Fundamentação do ajuste de pacing

A [documentação oficial de implementação do netplay](https://docs.libretro.com/development/retroarch/netplay/#implementation)
explica que, quando a entrada remota fica muito atrás, o motor pausa o avanço de
quadros para permitir a recuperação do outro jogador, sem bloquear a interface.
O [frontend oficial v1.22.2](https://github.com/libretro/RetroArch/blob/v1.22.2/network/netplay/netplay_frontend.c)
mantém a criação/limpeza normal de `NETPLAY_STALL_RUNNING_FAST`; a marcação externa
Station removida não faz parte desse comportamento.

Na fonte R73, a transição começa em `netplay_frontend.c:8323`, é desfeita em
`:8248`, mas `:9067` marcava `station_recovery_stalled`. O cabeçalho
`station_recovery.h:58` só limpa essa marca por um novo pedido de espera, não
pelo catch-up normal. `StationRecoveryTunnel.java:96-97` lê a marca e emite
SYNC13. O probe abaixo executa esses trechos extraídos, inclusive os métodos
Java reais, confirmando o encadeamento sem depender de suposição sobre o servidor.
Isso comprova o defeito no cliente. O teste isolado não atribui todos os episódios
físicos de queda a essa causa; correlação completa ainda exige registros do relay.

## Evidência física do ANR

Os dumps privados de 07/10/2026 às 20:52 locais mostram, nos dois aparelhos, o thread
do motor em `nanosleep`, PC relativo `0x771248`, BuildId
`9e40349e088938de9a97e92fced501dac9a0089f`.
Simbolização usando o ELF R73 não reduzido resolve `runloop.c:7370`, precisamente
a espera de recuperação de 50 ms. A fila Java principal estava em `nativePollOnce`.
O motivo do ANR era falta de resposta a MotionEvent. A fila nativa Android era
normalmente drenada pelo caminho de `core_run`, saltado nessa espera.

Os arquivos brutos ficam somente na pasta privada de evidências do mantenedor.
Não publicar dumps, nomes de usuários, tokens, IDs de salas ou dados pessoais.

## Contrato Java/JNI

Adicionar na mesma `StationRetroActivity`:

```java
public native boolean stationRequestQuit();
```

- Chamar no thread da interface somente após decisão humana de sair.
- `true`: saída aceita ou já pendente; manter a Activity e o serviço ligado enquanto
  o motor encerra normalmente. Não chamar `finish()` nem fechar o transporte primeiro.
- `false`: não há instância correspondente ativa, ou ela já finalizou. O Java pode
  finalizar sua Activity. A comparação de identidade impede uma Activity antiga
  de encerrar um novo motor.
- Registrar/enviar ao proprietário da sessão a intenção humana de sair ANTES do
  JNI. O comportamento upstream `frontend_android_shutdown -> exit(0)` foi mantido:
  o processo isolado pode terminar após o cleanup antes de executar callbacks Java
  pendentes. Não depender de um callback assíncrono final para notificar o servidor.
- `onRetroArchExit()` pode então concluir `finish()`. Manter idempotência.
- Nenhuma queda de rede, timeout, pausa ou perda de visibilidade chama esse JNI.
- A chamada usa flag atômica release/acquire e `ALooper_wake`; não escreve em pipe
  potencialmente cheio nem modifica diretamente o runloop a partir da UI.
- Deve haver apenas um motor/NativeActivity por processo. Uma reserva nativa,
  protegida por mutex, rejeita a segunda Activity com `IllegalStateException`
  antes de resetar o estado ou criar outro thread. Só libera a reserva depois do
  `join` anterior. A Activity rejeitada pode finalizar sem tocar no motor ativo.
  Preservar a deduplicação Java e testar reabertura, sem reutilizar uma Activity antiga.

O `onDestroy` nativo tem o mesmo pedido de saída como fallback do ciclo de vida
Android, seguido da espera correta. Não introduz temporizador nem kill automático.

## Testes executados no PC

`tests/lifecycle_probe.py` extrai funções reais dos arquivos antigos/novos e usa
threads, mutexes e condições reais do Windows. Looper e término do core são modelos.

- R73 reproduz três bloqueios: liberar motor ainda ativo; `join` segurando mutex;
  callback aguardando resposta após motor encerrado. Cada processo de teste isolado
  é encerrado pelo timeout do próprio teste; isso não existe no aplicativo.
- R73 também reproduz estado terminal/epoch antigo sobrevivendo a nova configuração.
- R74 passa 100 ciclos de solicitação/encerramento, ambas as ordens de término,
  pedido repetido, pedido sem instância, identidade JNI antiga, janela inicial,
  callback aguardando término e reset com reconexões da mesma sessão preservadas.
- A reserva também foi testada contra abertura sobreposta e liberação indevida por
  uma Activity antiga/rejeitada; a ordem reserva -> reset e join -> libera é conferida.
- `tests/recovery_input_probe.py` executa fragmentos exatos do runloop com uma fila
  Android modelada: 12 segundos simulados acumulam 2.400 eventos sem ACK na R73;
  R74 confirma todos, executa zero frames enquanto espera e mantém o fluxo normal.
- `tests/pacing_probe.py` extrai os ramos reais de stall/unstall/pre-frame e o
  cabeçalho de recuperação R73. Com estruturas/entradas de teste, executa 100
  ciclos de anfitrião e 100 de convidado. O catch-up acaba nos 200 casos, mas a
  R73 conserva a marca antiga em 200/200; a R74 em zero. Os mesmos 200 post-frames
  e a mesma histerese são preservados. O teste compila os métodos Java reais
  `tick()` e `lost()` com socket/listener simulados: após catch-up, SYNC13 aparece
  somente na R73. Perda real simulada de WSS continua pedindo pausa, preservando
  offsets e ignorando callbacks repetidos da conexão antiga.
- O probe também confere a diferença exata de três linhas no frontend, hashes
  idênticos do stall/unstall e da função completa de polling/flush R73. Nenhum
  socket, servidor, jogo ou telefone é usado por esse teste.
- Recibos completos nos três JSON da pasta `tests`. Executáveis/temporários em E:.

## Verificações restantes em Android

Recompilar ARM64 e conferir símbolos/hashes; registrar o novo runtime no contrato
do servidor. Em dois aparelhos: iniciar, esperar recuperação por mais de 10 segundos
tocando na tela, verificar ausência de ANR, voltar a jogar, sair pelo comando humano,
reabrir outra sala e conferir retorno ao menu sem perda de autenticação.
Essas verificações físicas ainda não foram feitas nesta entrega nativa.
