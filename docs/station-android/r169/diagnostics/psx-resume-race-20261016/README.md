# PSX R169: falha de início e ritmo online medidos — 16/10/2026

**Correção pronta no código; nenhum APK corrigido foi instalado.** O servidor
R169 permanece aplicado. Este diagnóstico complementa o mesmo handoff.

## Evidência dos aparelhos

Foram lidos no Moto g52 e Samsung A56 o DEX35 `e2c7ef47475d` e o runtime PSX
`cddba501a25c`; ambos coincidem com a R169. O APK completo e seu certificado
não foram relidos nesta investigação. Logs, imagens, binários e traces ficam
privados. O jogo observado é **Capcom vs. SNK — Millennium Fight 2000 Pro**.

| Observação física | Resultado |
| --- | --- |
| Offline no Motorola | Título visível, áudio médio 44.099,6 quadros de áudio/s; esperado 44.100; zero descartes. |
| Online às 21:13, Motorola anfitrião | Samsung registra `NATIVE_DATAGRAM` após sincronizar, sem áudio; conexão do convidado termina primeiro; anfitrião depois recebe 1006 e recusa de autoridade. |
| Tentativa seguinte com A56 anfitrião | Os dois contadores de áudio ficam em zero; tentativa encerrada antes da próxima. |
| Partida às 21:20:50, Motorola anfitrião | Os dois avançam, áudio ~37.650/s, ~85% da taxa offline. Bytes do protocolo em 110 s indicam ~50,71 quadros executados/s em ambos. |
| Perfetto durante a partida | Em 30 s, worker de saída dorme 24–27 ms 1.159 vezes no Moto e 1.157 no A56; médias 25,013/25,036 ms. SDL usa ~47%/~29% de um núcleo, com tempo dormindo. |

A inferência de quadros usa a estrutura constante **78 B/quadro do anfitrião**
(FRAME40 + COMMIT38) e **76 B/quadro do convidado** (INPUT38 + COMMIT38), em
estado e época estáveis após o snapshot. Não é FPS de tela nem um contador
instrumentado do APK. O áudio confirma a redução de ritmo.

O servidor manteve o PID, zero reinícios e duas conexões na partida que
avançou. Na janela de 110 s, usou ~17,74% de **um** núcleo, com backlog final
zero. Os pongs medem cada telefone até o relay; não representam o percurso
completo entre os aparelhos. Não foi constatada saturação do serviço nessa
janela. Rede, Cloudflare, QoS100/800 e demais serviços não foram alterados.

## Três pontos corrigidos no módulo do app

1. **Retomada bloqueada pela própria fila.** Depois de PLAYING, um participante
   mais rápido pode entregar INPUT antes do próximo tick Java do outro. O
   segundo esperava `DRAINED` para retomar, mas precisava retomar para consumir
   esse INPUT. Retomada usa ACK real da época/serial e snapshot aplicado;
   `DRAINED` continua obrigatório no READY e em seu estado numérico em cache.
2. **Primeiro quadro válido rejeitado.** O anfitrião pode emitir o FRAME neutro
   inicial antes de o convidado executar o prime autorizado. A admissão agora
   responde Wait somente nessa janela neutra exata, mantendo o pacote limitado
   no transporte sem ACK/cópia fictícia. Controles divergentes, outras janelas,
   estado/época e slots continuam validados. A categoria física de rejeição foi
   capturada; o pacote original não foi registrado, portanto sua causa exata
   não é afirmada exclusivamente pelo log genérico `NATIVE_DATAGRAM`.
3. **Espera periódica de 25 ms no envio.** A leitura PSX era não bloqueante e o
   worker usava um semáforo que a produção de dados do core não sinalizava.
   A espera foi medida nos dois celulares. Agora o worker PSX aguarda uma
   `condition_variable`, acordada por fila, snapshot e fechamento. Limite100ms,
   sem consumo nessa espera e com guarda que impede bloquear o dono SDL.
   A espera ocorre **fora** de `endpointReading`, preservando READY. O endpoint
   comum mantém o fallback anterior quando não opta pelo novo método.

Há diagnóstico numérico de rejeição: resultado nativo, tipo PXL e tamanho.
Nunca são registrados IDs, tickets, controles ou conteúdo de estado.

## Verificação já executada

- Mesma harness com Java e protocolo C++ reais: baseline falha nas quatro
  combinações anfitrião/convidado primeiro e épocas1/3; candidata passa62.
- Protocolo:202.916 verificações; adaptador original:16.090/513 execuções
  **sintéticas**; Session/Launch:64; espera por eventos:61.
- Endpoint PSX:192; regressões Dolphin/TCP:470 passaram.
- Link ARM64 API28:16 exports JNI esperados, LOAD16KiB. É apenas prova de link,
  sem identidade produtiva; **não implantar** o `entry-link-probe.so`.
- Modelo com protocolo real e 48 combinações de relógio/trabalho: polling25ms
  dá ~52–60FPS; notificação imediata dá ~60FPS na matriz definida. Os valores
  de rede/trabalho do modelo são hipóteses declaradas, não uma medição do APK.

Os testes com donos sintéticos não executam o emulador Android. A melhora
física da candidata, inclusive taxa alvo e estabilidade, continua pendente.
Não declarar todos os engasgos da internet resolvidos nem extrapolar ao N64.

## Entrega completa para a produção do APK

Usar os oito arquivos de produção listados com hashes em `incident.json`,
no Git TurboElden, diretório `integration/psx-online`. Os testes ficam fora do
APK. Preservar as correções XML e CAS da R169, cores originais, controles,
opções, atraso assinado4, saves e assinatura original.

1. Compilar os cinco fontes Java alterados, incluindo `StationPsxSession$Session`,
   e o runtime PSX com Protocol/Entry/cabeçalho atualizados. Há o novo JNI
   `nativeAwaitOutgoing(int)`. A receita R169 que troca apenas uma instrução
   CAS **não cobre** esta alteração. Não reutilizar o compilador/identificador
   antigo com bytes novos. Preservar as demais classes e partições DEX.
2. Regenerar a identidade do runtime e o vínculo com o DEX compartilhado final,
   manifesto, hashes, políticas, recibo final do APK e kit privado exato da
   sucessora, com a assinatura original. Renovar outros marcadores somente
   conforme o fingerprint efetivo; lógica N64/Dolphin permanece preservada.
3. Publicar o kit privado e o retorno no fluxo existente. O Station fará o
   cadastro aditivo da identidade recebida, com baseline/auditoria e limites
   efetivos conferidos. Não remover histórico nem aprovar títulos por nome.
   O runtime novo não foi cadastrado antes de existir o kit exato do APK.
4. Atualizar o instalador permanente conforme o procedimento vigente; instalação
   nos telefones permanece manual. Nenhum APK foi compilado, assinado ou
   instalado neste Linux durante este diagnóstico.
5. Testar dois aparelhos com a mesma sucessora: ambos os papéis de anfitrião,
   primeira imagem/áudio, controles, pelo menos60s avançando, pausa/retomada e
   saída do anfitrião observando o convidado25s. Comparar o avanço real do
   protocolo/áudio e coletar `datagram-rejected` se ocorrer. Não usar FPS de UI
   como prova de execução do core.

## Pesquisa técnica e limites

A [documentação Libretro de netplay](https://github.com/libretro/docs/blob/master/docs/development/retroarch/netplay.md)
explica a troca contínua de entradas por quadro e as esperas de sincronização.
O Station usa seu próprio protocolo; não foi substituído por RetroArch nem
foram copiadas configurações genéricas. A [documentação PCSX](https://docs.libretro.com/library/pcsx_rearmed/)
registra limitações da BIOS HLE. Neste caso ambos os modos usam HLE e o offline
avança; o aviso e a leitura LBA-150 durante aplicação do snapshot não provam
arquivo ausente. A aplicação de estado concluiu a barreira antes da rejeição.

A [documentação Perfetto](https://perfetto.dev/docs/data-sources/cpu-scheduling)
fundamenta a leitura dos tempos rodando/dormindo. As [orientações JNI do Android](https://developer.android.com/ndk/guides/jni-tips)
recomendam reduzir travessias JNI e permitem chamadas bloqueantes em workers.
A espera usa o [contrato de condition_variable do C++](https://eel.is/c%2B%2Bdraft/thread.condition.condvar).
A [documentação Android sobre sessões lentas](https://developer.android.com/topic/performance/issues/slow-session)
mede superfícies; a UI pode renderizar enquanto a emulação está parada.

O relato anterior “funcioou em testes” permanece histórico. A observação atual
é de falhas intermitentes de início e ritmo reduzido na R169, com correção
preparada e testada em código, aguardando APK corrigido e piloto físico.

## Observação autônoma solicitada

Coleta privada ativa neste Linux até **17/10/2026, 08:00 America/Maceio**:
logs do UID Station nos dois USBs e barreira do relay a cada30s, com arquivos
limitados e resumo atualizado. Unidade de usuário
`station-psx-observacao-20261016.service`. Não instala APK, não interage com a
UI, não encerra salas e não altera serviços ou rede. A checagem horária de
novos retornos Git permanece no fluxo existente. A coleta observa o APK R169
atual; seu resultado não será apresentado como teste da candidata.
