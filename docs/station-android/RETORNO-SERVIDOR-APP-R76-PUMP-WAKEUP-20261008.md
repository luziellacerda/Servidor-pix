# R76: entrega conferida e teste online acompanhado — 08/10/2026

**SERVIDOR → APP.** Entrega recebida: servidor
`a9f7f41b243f62e7ff5c3240d65eb7704034184c`, app
`7d903e64d7d059080cfc5102b36e1b68d7293bf5`; fonte executável
`2a8adce752b7778c90b2e70ecd67d1bc1fc62a9d`.

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

Produção observada nesta leitura: `turborama-station-api.service`, PID1278094,
zero reinícios automáticos, mesma ativação às 00:39:18 UTC de 08/10.
O recibo de implantação existente identifica DLL ativa
`ab192bf1585e30f303d041f13b36a1f9c96d2caa`, SHA256
`815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243`;
registro de dez engines SHA256
`a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843`.
Não houve nova implantação ou leitura privilegiada dos hashes nesta revisão.
V2 permanece 64 salas/128 participantes,256KiB por direção,32MiB de rings em RAM.

## Conferência independente da entrega

- 31 arquivos declarados em DELIVERY-FILES tiveram tamanho e SHA256 conferidos;
  o manifesto é o32º arquivo da publicação.
- 39 fontes/receitas/evidências do manifesto do app conferidas por hash,
  incluindo a fonte corrigida;26 cópias do servidor idênticas byte a byte.
- Receita do pump repetida uma vez no Linux/JDK17 com a fonte R76 exata,
  Wire e baseline fixadas por SHA.143 cenários passaram: baseline adiou64/64,
  R76 enviou64/64 sem novo tick. A execução somou1.488 checks; a contagem
  varia com o agrupamento concorrente dos produtores, sem ampliar a cobertura
  dos 143 cenários. Método corrigido extraído tem o mesmo hash do recibo Windows.
- Socket/JNI são modelados nesse probe: não executa Android, ROM ou rede.
- Recebidos e vinculados por hash:1.206 regressões,101 checks de sessão/42
  guardas e seis execuções TLS/WSS/TCP com 30.817.216 bytes exatos no PCAPK.
  Essas suítes não foram repetidas integralmente neste Linux.

## Aparelhos e janela de observação

Recibo do PC confirma APK integral no Samsung A56 em 11:25:32.883956 UTC,
com UID/data original preservados; entrada oficial foi conferida depois.
O mantenedor confirmou nesta conversa que **os dois aparelhos estão na R76**.
A versão do segundo é confirmação humana; seu APK não foi rehashado aqui.

Coleta passiva iniciada nesta janela: prontidão, CPU/memória, NIC/TCP locais e
journal sanitizado. Houve entradas autenticadas na área online às 11:39:34 e
11:44:16 UTC. Até a captura concluída em torno de 11:45 não havia início de
partida registrado; não tratar essa preparação como jogatina homologada.
A coleta foi renovada às 11:48 UTC por aproximadamente dez minutos.

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

## Próximo retorno físico

Comparar sala nova com os dois na R76: ambos papéis, movimentos simultâneos,
imagem/áudio, saída/reabertura e interrupção controlada de rede. Correlacionar
primeiro sintoma no app com presença/transportes do servidor na mesma janela.
Resultado de partida e recuperação permanece pendente; não declarar estabilidade
prolongada por compilação, instalação, entrada online ou probe JVM.

Nenhum serviço, licença, banco, rota, Cloudflare, firewall ou preset foi alterado
por esta revisão. As partidas permanecem preservadas.
