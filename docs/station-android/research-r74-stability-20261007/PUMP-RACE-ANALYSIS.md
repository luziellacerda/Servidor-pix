# R74: solicitação de envio perdida entre fila vazia e liberação do worker

## Resultado e limite

**Defeito de agendamento reproduzido no cliente.** Uma solicitação de envio pode ser coalescida com um worker que já decidiu encerrar. Os bytes continuam no buffer, mas nenhum novo worker fica responsável por eles. Outro evento ou o tick periódico volta a chamar `pump()` e libera o envio.

Não é perda de dados, prova de desconexão física nem comprovação de que todos os pacotes esperam 250 ms. A reprodução não usa Android, rede, servidor ou aparelhos. Não foi alterado nem compilado APK/runtime. A proposta de correção só existe no programa isolado de teste.

Fonte examinada: `E:\ESTUDO APK\work\station-session-lifecycle-r74-20261007\compiled-final\java\netplay-src\org\emulationstation\frontend\netplay\StationRecoveryTunnel.java`.

SHA-256: `8af5ceb585498512ff9ff4e1409a46ee2ed9dca9ee66bcb0c43bc11fa7817896`.

## Caminho real

| Linha | Comportamento |
| --- | --- |
| 25/27 | Executor de escrita único; `pumpPending` permite somente um agendamento pendente. |
| 49 | `tick` usa `scheduleWithFixedDelay`, com intervalo de 250 ms após a execução anterior. |
| 75 | Produtor publica em `tx` dentro de `gate`, libera o monitor e chama `pump()`. |
| 99 | O tick também chama `pump()`. |
| 123 | Se `pumpPending` já estiver marcado, a nova solicitação retorna sem registrar trabalho adicional. |
| 124–132 | Worker consulta estado e fila dentro de `gate`. |
| 134 | Após liberar o monitor, encerra caso `frame` seja nulo. |
| 136 | `finally` libera `pumpPending` sem reexaminar solicitações concorrentes. |

Intercalação suficiente para reproduzir:

1. Worker observa fila vazia e libera `gate`.
2. Produtor publica bytes em `tx` e pede envio.
3. `pumpPending` ainda é verdadeiro; o pedido retorna.
4. Worker encerra e deixa `pumpPending=false`.
5. Bytes permanecem em `tx`, `cursor` não avança e não há tarefa de escrita agendada.
6. Um próximo evento/tick chama `pump()` e envia os bytes íntegros.

O intervalo de 250 ms é um caminho de resgate, não um limite máximo garantido: ele é contado depois da execução anterior e depende do agendador. Uma nova mensagem remota ou novos bytes locais podem resgatar antes. A ocorrência/frequência desse interleaving em aparelhos continua sem medição.

## Reprodução determinística

Receita: `run_pump_probe.py`; fonte do teste: `PumpRaceProbe.java.in`; recibo: `pump-race-receipt.json`; saída: `pump-race-output.txt`.

A receita valida os hashes, extrai o método `pump()` e o trecho produtor da fonte real, e compila a classe `StationRecoveryWire.java` original. Não reescreve manualmente o algoritmo baseline. O único ponto de controle inserido nele fica depois de `frame==null`, antes do `return/finally`. Dois `CountDownLatch` obrigam o produtor a publicar exatamente nessa janela. Uma barreira do executor confirma que o worker encerrou antes das asserções; não depende de dormir e esperar uma corrida acontecer.

Resultado:

- 1.764 verificações passaram.
- 64/64 intercalações baseline deixaram DATA esperando outro sinal.
- 64/64 preservaram os bytes e offsets; novo `pump()` enviou um único quadro correto.
- 64/64 intercalações com o candidato isolado enviaram sem novo tick/evento.
- Publicação antes da seleção e depois do encerramento funcionou no baseline e no candidato.
- Confirmação do servidor não foi fabricada: `tx.delivered` permaneceu zero e os bytes não confirmados continuaram disponíveis.

Modelados explicitamente: destino WebSocket, status nativo válido constante e fila de socket vazia. Reais: threads, executor Java, latches, método extraído, produtor, buffer circular, cabeçalho/codec TSR2. O candidato não foi verificado contra todos os ramos de erro/fechamento/reconexão; não deve ser chamado de correção integrada.

## Correção mínima proposta, ainda não aplicada

Registrar um sinal separado de “houve pedido” antes de tentar adquirir `pumpPending`. O worker limpa esse sinal antes de examinar trabalho. Ao liberar a propriedade no `finally`, se algum pedido ficou registrado, agenda novamente. Isso preserva o executor único e o algoritmo atual de prioridade ACK/PAUSED/DATA/READY/PING, sem mexer no protocolo, tick, timeouts ou capacidade dos buffers.

O candidato do teste faz somente isso com `AtomicBoolean pumpRequested`. Não adiciona dependência externa. Antes de uso em produção, faltam regressões dirigidas para fechamento, rejeição de executor, socket substituído, terminal, backpressure e ACK/READY concorrentes. Uma implementação por contador de trabalho em andamento também é possível, mas demanda mais mudança no laço; não foi necessária para provar o defeito.

Uma liberação incondicional de flag seguida de saída, sem lembrar pedidos concorrentes, é o ponto defeituoso. Uma chamada incondicional a `pump()` no `finally` também seria incorreta, pois poderia criar um ciclo sem trabalho.

## Fontes oficiais e relação com o achado

O [QueueDrainHelper oficial do RxJava](https://raw.githubusercontent.com/ReactiveX/RxJava/3.x/src/main/java/io/reactivex/rxjava3/internal/util/QueueDrainHelper.java) usa um mecanismo de contagem de trabalho pendente: depois de drenar a fila, verifica solicitações acumuladas antes de encerrar. É referência do padrão, não uma biblioteca adicionada ou código copiado para o aplicativo.

A documentação oficial de [ExecutorService do Java 17](https://docs.oracle.com/en/java/javase/17/docs/api/java.base/java/util/concurrent/ExecutorService.html) define a ordenação de memória entre submissão, execução e `Future.get()`. Essa garantia fundamenta a barreira usada no teste para inspecionar o estado depois do encerramento do worker.

Consulta em 08/10/2026 UTC. O diagnóstico local vem do código R74 e da reprodução; as fontes externas explicam os mecanismos de concorrência. Nenhuma fonte externa demonstra a frequência ou o impacto nos aparelhos do mantenedor.
