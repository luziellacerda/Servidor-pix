# R76: entrega conferida; dupla jogou com engasgos — 08/10/2026

**SERVIDOR → APP.** Entrega recebida: servidor
`a9f7f41b243f62e7ff5c3240d65eb7704034184c`, app
`7d903e64d7d059080cfc5102b36e1b68d7293bf5`; fonte executável
`2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`.

Atualização documental recebida durante a coleta:
`ec935a2231d1ca2230426ccdd39edc3692632b22`, contendo o recibo integral
do Motorola e uma proposta separada de salas para quatro pessoas. A proposta
servidor `6d40e26fc132d68ca482421bc29f10cf33d5d699` foi lida; quatro
participantes permanecem fora da produção e do APK R76.

## Compatibilidade confirmada

APP-01 já está integrada no `StationRecoveryTunnel.java`, SHA256
`442c50840a280d4a5605e32a06873fa14a879285a959a6f2a0b5231e2204ede9`.
Não solicitar novamente sua implementação. O APK R76 tem SHA256
`d7145db3511a4056b16fdde10e4c07709a16b7f445596801b3535c1b28b06a51`,
DEX35 `c03ea2f4aa30c5e32654c575115583f72815b9701c16791c4f94c6ade753f2ff`.
R75 visual, certificado, runtime, cores, controles e contratos preservados
conforme os recibos conferidos. Não houve montagem/instalação de APK neste Linux.

Runtime continua `804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`;
engines `bsnes-mercury-performance-79d7f9de-rs4-804b2acfea4c` e
`clownmdemu-d43c2708-rs4-804b2acfea4c`, protocolo `station-stream.v2`.
**A R76 não exige outro cadastro nem reinício do Station.**

Produção observada nesta leitura: `turborama-station-api.service`, PID 1278094,
zero reinícios automáticos, mesma ativação às 00:39:18 UTC de 08/10.
O recibo de implantação existente identifica DLL ativa
`ab192bf1585e30f303d041f13b36a1f9c96d2caa`, SHA256
`815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`;
registro de dez engines SHA256
`a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843`.
Não houve nova implantação ou leitura privilegiada dos hashes nesta revisão.
V2 permanece 64 salas/128 participantes, 256 KiB por direção, 32 MiB de rings em RAM.

## Conferência independente da entrega

- 31 arquivos declarados em DELIVERY-FILES tiveram tamanho e SHA256 conferidos;
  o manifesto é o 32º arquivo da publicação.
- 39 fontes/receitas/evidências do manifesto do app conferidas por hash,
  incluindo a fonte corrigida; 26 cópias do servidor idênticas byte a byte.
- Receita do pump repetida uma vez no Linux/JDK17 com a fonte R76 exata,
  Wire e baseline fixadas por SHA. 143 cenários passaram: baseline adiou 64/64,
  R76 enviou 64/64 sem novo tick. A execução somou 1.488 checks; a contagem
  varia com o agrupamento concorrente dos produtores, sem ampliar a cobertura
  dos 143 cenários. Método corrigido extraído tem o mesmo hash do recibo Windows.
- Socket/JNI são modelados nesse probe: não executa Android, ROM ou rede.
- Recebidos e vinculados por hash: 1.206 regressões, 101 checks de sessão/42
  guardas e seis execuções TLS/WSS/TCP com 30.817.216 bytes exatos no PCAPK.
  Essas suítes não foram repetidas integralmente neste Linux.

## Aparelhos e janela de observação

Recibo do PC confirma APK integral no Samsung A56 em 11:25:32.883956 UTC,
com UID/data original preservados; entrada oficial foi conferida depois.
O mantenedor confirmou nesta conversa que **os dois aparelhos estão na R76**.
Depois chegou o recibo do Motorola Edge 30: instalação concluída às
11:44:14.642435 UTC, mesmo SHA integral do APK R76, UID/data original
preservados; entrada oficial em ESActivity às 11:45:47 UTC. Os hashes dos
telefones foram medidos pelo PCAPK e recebidos por Git, sem rehash neste Linux.

Coleta passiva iniciada nesta janela: prontidão, CPU/memória, NIC/TCP locais e
journal sanitizado. Houve entradas autenticadas na área online às 11:39:34 e
11:44:16 UTC. Até a captura concluída em torno de 11:45 não havia início de
partida registrado; não tratar essa preparação como jogatina homologada.
A coleta seguinte foi concluída: 120 amostras de 11:48:33 a 11:58:33 UTC.

### Resultado observado desta tentativa

Partida iniciada às **11:50:28.851 UTC (08:50:28 Maceió)**, geração 3.
O mantenedor informou que ambos jogaram, **mas houve engasgos**, e depois
confirmou que fechou os jogos. Não declarar estabilidade visual ou de áudio.

- Primeiro término de transporte às 11:56:06.490 UTC; `leave` do anfitrião
  às 11:56:06.677 UTC; término do convidado às 11:56:08.897 UTC.
  A sequência coincide com a saída humana. `REQUEST_ABORT` neste trecho
  não prova queda espontânea; epoch/estado do journal legado são após Detach.
- Antes da saída não houve término de transporte registrado. Heartbeats dos
  dois continuaram; maior intervalo observado foi 20,53 s no anfitrião e
  20,87 s no convidado. Sem marcador de presença vencida ou reinício.
- **791.027 bytes aceitos = 791.027 entregues**, pendência zero no término.
  Nas 67 amostras durante a janela da partida, pendência entre 0 e 400 bytes.
  Contadores pequenos/zerados não medem a espera de cada pacote.
- Station: CPU mediana 15,27% de um núcleo; máximo amostrado 19,83%.
  Máquina: mínimo de 93,06% ociosa; pelo menos 37,60 GiB de RAM disponível.
  Sem indicação de saturação de CPU/RAM nas amostras de aproximadamente 5 s;
  elas podem perder pausas curtas de GC, agendamento ou rede.
- NIC compartilhada: nenhum erro/drop adicional na janela; nove retransmissões
  TCP do sistema inteiro, sem atribuição à partida. A coleta local não mede
  o percurso dos celulares/Cloudflare nem FPS/temperatura dos aparelhos.
- Às 11:57 e 11:58: zero salas retidas, conexões e bytes pendentes.
  Mesmo PID e zero reinícios automáticos. Coleta encerrada normalmente.

Após a saída, às 11:56:30.409 UTC, houve um HTTP429 em
`POST /v1/station/online/events`. A fonte ativa pode recusar polls simultâneos
da mesma identidade (`STATION_ONLINE_POLL_EXISTS`); o corpo dessa resposta
não está no journal, portanto esse código não foi comprovado para o evento.
Não atribuir os engasgos anteriores a essa resposta posterior nem remover
guardas por hipótese. Os HTTP404 da rota de diagnóstico são esperados na DLL
ativa e foram provocados pela coleta, não por download/login do app.

Resumo sanitizado: `recovery-r76-20261008/TESTE-FISICO-SERVIDOR.json`.

Registros completos ficam privados em
`/mnt/DADOS/station-r76-trial-check-20261008`. Não publicar tokens, identidades,
payloads ou logs brutos. A amostragem de SO/NIC é compartilhada com os outros
produtos e não mede FPS, áudio ou RTT dos telefones.

## Estado das correções candidatas do servidor

SRV-01/02/03 continuam na fonte candidata
`6f27c6ca176b80da734a0000d6f07f72a480e5ed`, DLL
`71ba30b8ca4b363344facc6ff750be6886183d576ef0a0456b57086d3b83d14e`.
**Não ativadas.** A divergência TLS local dessa candidata não está resolvida.
Os passes R76 contra `ab192bf` e `32ce9d2` não qualificam `6f27`.
A adaptação de certificado SChannel registrada pelo PC é de fixture Windows;
não explica automaticamente o timeout Linux relatado.

O endpoint de observações novas não está disponível na DLL ativa. O journal
legado registra epoch/estado de término depois de Detach; não existe histórico
por frame que permita inventar tipos ou tempos anteriores. A análise do teste
deve conservar esse limite. Qualificar/ativar a candidata exige isolar a
divergência, completar os gates e uma janela sem sessões em RAM.

## Diagnóstico dos engasgos a conciliar com o PCAPK

Usar a janela física acima para conferir os registros dos dois aparelhos:
tempo de quadro/áudio e NeedSync/catch-up, RTT e variação DATA/PONG, espera da
fila do pump e do socket local, pausas do processo e temperatura. Identificar
o instante do engasgo antes de escolher mudança no app, relay ou rota.
A R76 corrigiu a corrida de sinalização reproduzida; este teste ainda não
prova a causa dos engasgos restantes. A DLL ativa não possui histórico por
frame/SendAsync/GC que permita eliminar hipóteses de pausa curta do servidor.
Conferir também reabertura/recuperação em ensaio separado; não afirmar que
saída voluntária é teste de retomada após perda de rede.

Não mudar timers, input delay, Cloudflare ou controles pela baixa CPU média.
O retorno é documentação e evidência da tentativa, sem novo DEX/APK ou deploy.

Nenhum serviço, licença, banco, rota, Cloudflare, firewall ou preset foi alterado
por esta revisão. As partidas permanecem preservadas.
