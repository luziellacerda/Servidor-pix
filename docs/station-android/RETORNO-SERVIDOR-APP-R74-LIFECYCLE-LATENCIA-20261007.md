# Retorno do servidor: R74 ativa e correções de estabilidade — 07/10/2026

**SERVIDOR → APP.** Responde à entrega `8d48252fb7e91af83b6138afa411d5c2607edcc8`
e à pesquisa `7ad4fb05a69d8f95030f029f3670333a0c7bd6c2`.
A fonte executável do app R74 é `557014b4ff5ec5c3c0162847d922c0587f68b0e9`;
o recibo `02b78919862ac40ea08ddff284e65c604b6d7f9f` confirma sua instalação nos dois aparelhos.
A sucessora documental/visual R75 `151ef4afb491e06494d6e0475457330e612f0950`
preserva DEX, runtime e engines R74; não exige outro cadastro de motor.

## 1. O que realmente está ativo

| Item | Estado confirmado |
|---|---|
| Serviço | `turborama-station-api.service`, PID `1278094`, zero reinícios automáticos na leitura final |
| Recarga do cadastro R74 | `2026-10-08T00:39:18.786312Z` / 07/10 às 21:39:18 Maceió |
| Recibo de conclusão | `2026-10-08T00:39:43.042700Z`; não confundir com horário da recarga |
| Fonte da DLL ativa | `ab192bf1585e30f303d041f13b36a1f9c96d2caa` |
| SHA256 da DLL ativa | `815fc8bc99a9d16247488a1797d2726b928eb3e592fd8a19700371e8d57c3243` |
| SHA256 do registro ativo | `a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843` |
| Registro | Dez engines; as oito anteriores preservadas integralmente |
| Procedimento do cadastro | `cd4fa9c4b27b67c55c0436e3fb059663e776ed02` |
| Catálogo | Revisão14, 2.212 itens, sem alteração de mídia |
| Provas do cadastro | 187 verificações isoladas; 191 por HTTPS/WSS público, com identidades sintéticas e limpeza |

O caminho da DLL continua
`/opt/turborama-station-recovery-r71-20261007-ab192bf/TurboRamaSuiteOnlineServer.dll`.
**A DLL nova de observações/Close NÃO está ativa.** As provas do cadastro
não qualificam a DLL candidata nem homologam jogatina física prolongada.

Adições efetivas, com `recoveryProtocol=station-stream.v2`:

| Plataforma | ID | SHA256 do core |
|---|---|---|
| snes | `bsnes-mercury-performance-79d7f9de-rs4-804b2acfea4c` | `cc14438319709f3b7868b3e652c731f6df472ff21bb01e4a65234ba2cc6f368b` |
| megadrive | `clownmdemu-d43c2708-rs4-804b2acfea4c` | `109b62ac11b2f59572de0666bcb02b3701676896b91cb32529bbe5c5032b6b69` |

Runtime dos dois:
`804b2acfea4c6d615bf40dba30e1777015098bf7375d0745db8d117555eb2516`.
APK instalado R74:
`e56896f28b16645653bfd28311cd0a8a9a5458e6d443849916a4dede6dc7fafe`.

V2 tem 64 salas/128 participantes, janela de 256KiB por direção e até 32MiB de
rings. Os512/1024 da seção v1 não ampliam a capacidade v2. Não houve ensaio
que homologue centenas de jogadores v2. Sessões e tickets residem em RAM:
reiniciar ou duplicar o processo não preserva essa autoridade.

## 2. Partida R74 observada nesta execução

Battletoads, Samsung anfitrião/Motorola convidado, geração3:

- Início aceito em `00:41:31.361986Z`.
- Heartbeats autenticados dos dois continuaram em intervalos próximos de 20s.
- Convidado terminou em `00:44:28.292325Z`, `REQUEST_ABORT`,
  `OperationCanceledException`. Na captura antiga, após Detach: epoch2,
  aceitos=entregues385.806 bytes, pendentes0; heartbeat host14.166ms/guest12.773ms.
- Novo heartbeat do convidado em `00:44:28.311708Z`, saída humana aceita em
  `00:44:28.517484Z`.
- Anfitrião terminou em `00:44:31.190166Z`, `REQUEST_ABORT`,
  `WebSocketException`; após Detach epoch3, aceitos385.846/entregues385.806,
  pendentes40 após a saída do outro membro.
- Não foram encontrados marcadores de falta de heartbeat, idle de transporte,
  erro fatal ou reinício do serviço nessa janela. O mantenedor confirmou sair.

Isso comprova transporte e presença nessa tentativa. Não mede FPS, áudio,
atraso visual, temperatura, replay nativo ou recuperação durante partida longa.
`REQUEST_ABORT` junto de saída humana não demonstra uma queda provocada pelo servidor.

## 3. Respostas às oito perguntas da entrega R74

| Pedido | Evidência e limite |
|---|---|
| 1. Primeiro evento às 23:52:14 R73 | App registrou espera às 23:52:13.842/14.014 e Close1006 às 14.188. Primeiro término registrado no servidor: convidado `TRANSPORT_IDLE` em 23:52:49.097925, epoch3 **após** Detach; heartbeat host15.873ms/guest9.281ms, aceitos336.804/entregues335.604/pendentes1.200. Não existe registro anterior suficiente para afirmar que esse foi o primeiro evento físico. |
| 2. Epochs2–19 | A DLL antiga não registra cada Suspend11/NeedSync13/Detach. Classificação individual retroativa indisponível. O candidato registra tipo, papel, UTC, relógio monotônico, epoch e estado antes/depois; não inferir causa pelo número. |
| 3. NeedSync às 23:52:52–23:53:15 | Não existe contagem histórica por tipo/papel. Retomada foi aceita às 23:52:50.085583. Epoch94 posterior não equivale a94 pedidos NeedSync. Em23:55:01.188259 houve `AUTH_HEARTBEAT_MISSING` do convidado,69.765ms sem sua presença,21.141ms do host, aceitos=entregues341.360/pendentes0. Essa falha é distinta da primeira queda por freezer Android. |
| 4. DATA/PONG | Implementados oito cronômetros monotônicos, por conexão/direção e grupos DATA≤1KiB/≤4KiB/≤16KiB/PONG. Não há histogramas físicos retroativos. O candidato precisa ser qualificado/ativado e capturar outra sessão. |
| 5. Filas | Candidato informa aceitos/entregues/pendentes, crédito do escritor, envio em andamento0/1, controles consecutivos antes de DATA e idade **amostrada**.256KiB limita retenção; não é espera fixa ou tamanho mínimo de envio. |
| 6. Serviço/SO | Candidato expõe GC/ThreadPool/CPU/RSS; coletor passivo mede NIC/TCP/filas/RTT locais no mesmo intervalo. Amostras de CPU ociosa após a partida não excluem uma pausa durante ela. Retransmissões sistêmicas incluem todos os produtos; não são atribuídas automaticamente ao Station. |
| 7. Proxy efetivo | Caminho atual: celular→Cloudflare→cloudflared→Nginx→`127.0.0.1:5192`. Rota relay com UpgradeHTTP/1.1, Authorization/proof/subprotocol preservados, buffering/cache desligados, read/send120s/connect5s, `tcp_nodelay on`. Não foi encontrada divergência que justifique mudar proxy ou timeouts. |
| 8. Cadastro R74 | Concluído e verificado em produção, conforme seção1 e `recovery-r74-20261007/PRODUCAO-EFETIVA.json`. R75 visual usa o mesmo registro. |

## 4. SRV-01 — primeira causa antes de Detach

Implementado em `ac88e2d7ab7773f0fddfbeb179e04989c0f7e3b5`.
A primeira causa é registrada uma única vez por conexão, com UTC e dois
instantes monotônicos distintos: detecção e captura atômica dos contadores.
Inclui `epochAtFirstCause`, `stateAtFirstCause`, offsets por direção e pendência,
antes de o próprio fluxo executar Detach. `epochAfterDetach` é separado.
O campo legado `streamEpoch` continua sendo o valor posterior, para compatibilidade.
Outro ator pode mudar o estado antes da aquisição do lock; a captura não promete
um instante físico impossível de observar retroativamente.

Categorias distinguem Close remoto, recepção/envio quebrado, cancelamento do
pedido, encerramento da sala, presença vencida e autorização indisponível/revogada.
Sem payloads, tokens ou logs síncronos por frame. Há 16 términos recentes limitados.

## 5. SRV-02 — responder Close pelo mesmo escritor

Implementado no mesmo commit. Close recebido solicita `CloseOutputAsync` ao
escritor que já transmite DATA/PONG. Não cria um segundo envio concorrente.
Prazo de 2s existe somente para finalizar o fechamento; não altera o timeout
de jogatina. Close completado não chama Abort. Transporte quebrado/cancelado
continua abortável; saída humana encerra a sala; perda de WSS conserva a
retomada autenticada quando a autoridade da sala permanece válida.

A resposta ao Close é exigida pelo [RFC6455 §5.5.1](https://www.rfc-editor.org/rfc/rfc6455.html#section-5.5.1).
A leitura de Close do [.NET8](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/libraries/System.Net.WebSockets/src/System/Net/WebSockets/ManagedWebSocket.cs)
não substitui a finalização pela aplicação. Isso corrige encerramento/reconexão;
não demonstra a causa de todo engasgo durante uma partida aberta.

## 6. SRV-03 — observações limitadas do caminho real

Implementado em `ed096d21f3d8d6b9eaa528d7b068c6c9289b2ccf`, preservado em
`6f27c6ca176b80da734a0000d6f07f72a480e5ed`:

- Recebimento completo→lock; tempo dentro do lock de Receive; espera/tempo
  do lock de Next; seleção→início de envio; duração de SendAsync.
- Recebimento→seleção/envio para um em cada16 DATA novos, com no máximo256
  metadados. Replay duplicado não entra como nova amostra.
- P50/p95/p99 são limites superiores dos buckets, com contagem/soma/máximo.
  Snapshots concorrentes são aproximados. Idade amostrada não é idade exata
  do byte mais antigo quando a cobertura foi descartada; há contadores disso.
- Oito conexões observadas por sala;128 transições; oito salas concluídas,
  mantendo apenas metadados; referência aleatória sem identidade de jogador.
- Publicação de estado e controles antes de DATA são medidos antes de mudar
  justiça/prioridade/locks. Ordenação, ACK, crédito e barreiras foram preservados.

Endpoint novo `/ready/station/online/diagnostics`: somente TCP loopback com
Host local, sem cabeçalhos de proxy/Cloudflare; público recebe404. Não é uma
rota para o APK. Desativação pela configuração
`Station:Online:RecoveryDiagnosticsEnabled=false` conserva o protocolo.
Coletor: `scripts/medir-relay-station-r74.py`, passivo e com duração/saída limitadas.

## 7. APP-01 — melhoria comprovada que ainda falta no APK

Reproduzi no Linux a receita publicada pelo PC de produção, com JDK17 e as
fontes reais conferidas por SHA256, sem alterar a receita nem a biblioteca Bytes.
**1.764 verificações:64/64 intercalações retiveram DATA sem um escritor agendado;
64/64 no candidato enviaram sem outro tick, preservando todos os bytes.**
Recibo saneado: `recovery-r74-20261007/PUMP-PROBE-LINUX.json`.

O defeito está em `StationRecoveryTunnel.pump()`: produtor sinaliza trabalho
entre a decisão de fila vazia e a liberação de `pumpPending`; encontra a marca
ocupada e o escritor termina sem lembrar do pedido. O candidato registra o
pedido pendente e o reavalia ao liberar o único escritor. A cadência250ms do
tick não mede o atraso físico nem é seu limite absoluto.

**Prioridade do PCAPK:** integrar a correção sobre a fonte R74/R75 atual,
incluindo fechamento, substituição de transporte, rejeição do executor,
ACK/crédito, fila cheia e reconexão nas suítes completas TLS/WSS/TCP/sessão.
Depois compilar DEX/APK e confirmar hashes nos dois celulares preservando dados.
O probe usa Socket/JNI simulados: não é uma correção já instalada nem prova de
que todas as quedas tinham essa causa. Reduzir o tick não resolve a corrida.
O padrão de drenagem é conferível no
[QueueDrainHelper do RxJava](https://raw.githubusercontent.com/ReactiveX/RxJava/3.x/src/main/java/io/reactivex/rxjava3/internal/util/QueueDrainHelper.java);
não exige adicionar RxJava ao projeto.

## 8. Qualificação da DLL candidata e limite que impede ativação

Fonte selada `6f27c6ca176b80da734a0000d6f07f72a480e5ed`, .NET8.0.31/SDK8.0.131,
Release sem warnings, SHA256:
`71ba30b8ca4b363344facc6ff750be6886183d576ef0a0456b57086d3b83d14e`.

- 91 verificações de estado/contrato v2 e589 de observações, incluindo igualdade
  dos bytes/ACK/replay/barreiras com observações ativadas/desativadas.
- 88 verificações de término, nove cenários com sockets simulados: Close1000,
  Close durante envio, prazo expirado, EOF, erro de recepção/envio, cancelamento,
  Close+cancelamento e saída humana; escritor único e primeira causa única.
- No fixture com TLS real e tracing/logging,duas execuções passaram, com 35 e 36 verificações:
  Close1000 completo, epoch1 antes/2 depois, estado Playing2, ACK e retomada
  autorizada da mesma geração, guards do endpoint local.
- **O fixture com logging/tracing desligados apresentou timeout do handshake
  no mesmo ambiente, inclusive iniciado e consumido imediatamente. A causa
  dessa diferença não foi isolada. Os passes anteriores não apagam essa falha.**

Por isso a DLL permanece candidata. Não houve qualificação da instância sombra
configurada nem prova pública dessa DLL. A tentativa de autenticação Linux foi
cancelada antes de executar o operador; não houve instalação em /opt ou novo
reinício. A produção mantém a DLL anterior, cujo cadastro R74 já foi aprovado.

Pacote/operador guardados no worktree imutável
`/mnt/DADOS/servidor-pix-station-r74-relay-observability-20261007`, HEAD6f27c6c,
e em `/mnt/DADOS/station-r74-observability-qualification-20261007/server-publish`.
`scripts/implantar-observacoes-station-r74-20261007.py` valida fonte/hashes,
registro de dez engines, instância sombra, identidades sintéticas/limpeza e
preservação dos outros produtos. A ativação exige resolver a discrepância,
concluir o gate e autenticação nativa Linux; não apenas mudar um ponteiro Git.
Recuperação em RAM exige zero salas/conexões/pendências antes da recarga.

## 9. Rota direta e próximos testes

Estudo e fontes oficiais:
[PESQUISA-SERVIDOR-LATENCIA-20261007.md](PESQUISA-SERVIDOR-LATENCIA-20261007.md).
Nenhuma porta/DNS/firewall/rota foi aberta, nenhum TLS/proof foi reduzido.
O pin atual do APK rejeita o certificado da origem direta. Nova rota exige
mudança coordenada do app e prova de acessibilidade externa; remover Cloudflare
agora não constitui uma solução comprovada.

EXP-01 continua **proposto**: comparar0/0 com2/0 quadros de atraso de entrada
(aproximadamente33ms a60fps), mesma cena/core/runtime/rede, papéis alternados,
temperatura controlada e tempo/profundidade de replay medidos. Não aplicado.
`optionsSha256` atual não cobre esses parâmetros de `retroarch.cfg`.

Homologação física após correções qualificadas: sala nova, ambos papéis,
movimentos simultâneos, áudio/imagem, saída/reabertura, interrupção controlada
de rede e partida prolongada; medir servidor e celulares no mesmo intervalo.
Ainda não existe resultado que autorize declarar estabilidade geral ou
capacidade de centenas de jogadores.
