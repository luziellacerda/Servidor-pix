# Contrato candidato de retomada após R67 — station-stream.v2

Fonte executável: `StationOnline.cs`, `StationOnlineEndpoints.cs`, `StationRecoveryRelay.cs`; cliente Java e patch RetroArch em TurboElden, `versions/station-online-recovery-r67-20261007`. Este contrato descreve o candidato, **não o serviço publicado**. OpenAPI 3.1 em `openapi.json`; esquemas e vetores públicos em `contract-vectors.json`. Os dados dos vetores são sintéticos e nenhuma chave privada foi publicada.

## 1. Rotas e negociação

Mantidas as rotas `POST /v1/station/online/command`, `POST /v1/station/online/events`, `GET /v1/station/online/relay`. Não existe rota `/v2` criada por esta entrega. REST mantém envelope RSA-PSS `keyId/payload/signature` e payload de domínio `TurboRamaStationAndroid/online/v1`; identidade, sessão e `requestId` continuam vinculados à resposta. O Android valida assinatura, domínio, identificação e requisição antes de usar o snapshot.

`RecoveryEnabled=false` é o padrão. Quando habilitado, o snapshot anuncia `recoveryCapabilities:["station-stream.v2"]` e transporte `relay-wss-v2`. A engine aprovada precisa declarar `recoveryProtocol:"station-stream.v2"`, além dos hashes exatos de core e runtime. `create` e `join` enviam esse protocolo; os dois membros, quatro hashes e engine precisam concordar. `start` solicita `transport:"relay-wss-v2"`. Clientes antigos continuam com `relay-wss-v1`/`station-relay.v1` e os IDs antigos preservados no registro. Uma sala de versões/motores diferentes é recusada; o runtime novo não se identifica como 899e.

O novo APK precisa do registro e do recurso habilitados antes de iniciar v2. Não força fallback de identidade para entrar com um runtime incompatível. O servidor mantém v1 para os APKs existentes.

## 2. Credencial e sessão

Todo comando usa Bearer da sessão e, quando protegida, `X-Station-Request-Proof`. V2 exige sessão protegida RSA ou EC e prova também no upgrade WSS. Sessões/identidade são renovadas pelo fluxo existente, sem substituir alias ou licença. `RequireVerifiedApp` global continua inalterado; o candidato não presume atestação de hardware comprovada na R67.

`relay-ticket` inicial e `resume-relay` exigem:

```json
{
  "action": "resume-relay",
  "requestId": "00000000-0000-4000-8000-000000000002",
  "roomId": "synthetic-room",
  "generation": 2,
  "recoveryProtocol": "station-stream.v2",
  "engineId": "synthetic-runtime",
  "contentSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "optionsSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "coreSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
  "runtimeSha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
}
```

O exemplo é um vetor de assinatura, não uma sala utilizável. A sala real fornece geração/engine/hashes. Não reler a ROM durante cada retomada: o dono da sessão guarda somente esses metadados em RAM. A verificação inicial de edição do jogo ao criar/entrar continua a existente; nenhum hash de ROM foi acrescentado ao download.

Resposta privada do membro: `room.relay={path,protocol,ticket,windowBytes,expiresInSeconds}`. Ticket aleatório 32 bytes/base64url43, individual, uso único, até60s. Nova emissão substitui credencial ainda não consumida. Após consumo/attachment, emissão concorrente retorna409; queda libera somente esse attachment. Finalizador antigo não invalida attachment novo da mesma geração. Não persistir ticket/prova em histórico ou logs. O launcher privado de uso único é apagado pelo consumidor; a renovação passa por Binder privado.

Upgrade usa `Authorization: StationRelay <ticket>`, `Sec-WebSocket-Protocol: station-stream.v2`, prova fresca de `GET /v1/station/online/relay`/corpo vazio e TLS com pin existente. Query string proibida. A prova é verificada antes do consumo; ticket copiado sem a chave do aparelho não é suficiente. Falha após consumo exige novo ticket e nonce, não repetição da credencial antiga.

Prova permanece `v1.<UnixSeconds>.<nonce16bytes-base64url>.<signature-base64url>`; janela±90s, nonce único por chave, corpo de controle≤8192. Bytes UTF-8, com LF final:

```text
TurboRamaStationAndroid/request/v1
METHOD
/v1/station/online/target
sha256(corpo_exato)
sha256(credencial_ASCII)
UnixSeconds
nonce
```

RSA2048/PSS-SHA256/salt32 ou EC P-256/SHA256 conforme sessão. O formato EC existente usa a codificação definida na implementação de segurança, não deve ser substituído pelo formato RSA dos vetores novos. Vetores de segurança EC anteriores continuam testados. Os três vetores novos RSA têm somente SPKI pública e assinaturas.

`requestId` repetido com mesmo comando não repete seu efeito; retorna snapshot atual, não cópia histórica contendo credenciais antigas. Mesmo UUID com conteúdo diferente retorna409. Para nova emissão use UUID e prova/nonce novos. Rate de comandos permanece30/10s por peer; `Retry-After:1` em429. Eventos não são renovação de presença e têm uma espera por identidade, long poll até10s.

## 3. Estados e gerações

A geração **da partida** permanece a mesma quando o WSS muda. `streamEpoch` inicia1 e cresce quando um attachment cai, suspende, solicita sincronização ou encerra definitivamente. Ambos devem reconhecer a pausa da época atual. IDs de attachment são opacos e distintos das credenciais.

| Evento | Estado de recuperação |
|---|---|
| Inicio/par incompleto | `waiting-reconnect` |
| Dois attachments HELLO, motores pausados e visíveis | `synchronizing` |
| Ambos READY da época; filas pendentes vazias | `playing` |
| Socket caiu / telefone oculto / stall nativo | `waiting-reconnect` |
| Parser/offset/bytes contraditórios ou estado nativo perdido | `unrecoverable` |
| Saída humana / licença revogada | Participação encerrada; recursos liberados |

Antes da primeira execução, `room.state` mantém `starting/connecting` para o convidado ainda poder abrir seu motor depois de `host-listening` real. `recoveryState` é independente. `recoveryStarted=true` impede reiniciar outra cópia do motor ao voltar às salas. Depois da primeira execução, estado público acompanha recuperação. `hostListening` só é confirmado após TCP local pronto e WSS estabelecido; não força o convidado para conectar.

A presença social continua60s; os membros de uma partida v2 iniciada são retidos quando ficam ausentes, inclusive ambos. Ficam fora da lista de pessoas ativas após a presença expirar. O desaparecimento do peer da lista social não é `Leave` da partida. Criação de uma nova sala/integridade/licença continuam sujeitas às validações existentes.

## 4. Framing binário

Uma mensagem binária WSS = um frame; fragmentos WSS são reunidos dentro do limite. Inteiros big-endian, não negativos, signed64. Header24 bytes:

| Bytes | Conteúdo |
|---|---|
| 0–3 | ASCII `TSR2` |
| 4 | opcode1–13 |
| 5–7 | zero; reservados |
| 8–15 | `offset` |
| 16–23 | `value` |
| 24… | payload só para DATA;1–16384 bytes |

| Opcode | Nome | offset / value |
|---|---|---|
| 1 | HELLO | Último upload aceito conhecido / bytes recebidos já escritos no TCP nativo |
| 2 | WELCOME | Upload aceito no servidor / upload entregue ao TCP do outro lado |
| 3 | DATA | Offset inicial do fluxo / zero; payload1–16384 |
| 4 | ACK | Prefixo recebido escrito no TCP nativo / zero |
| 5 | ACCEPTED | Upload aceito / upload entregue |
| 6 | STATE | streamEpoch / 0espera,1sincroniza,2joga,3irrecuperável |
| 7 | PAUSED | streamEpoch confirmado pelo thread de emulação / zero |
| 8 | READY | streamEpoch / offset de upload aceito correspondente |
| 9 | PING | Número monotônico do cliente / zero |
| 10 | PONG | Mesmo número / zero |
| 11 | SUSPEND | Época observada / zero; servidor exige nova pausa de ambos |
| 12 | FOREGROUND | Época observada / zero |
| 13 | NEED_SYNC | Época de `playing` / zero; stall sem romper WSS |

WELCOME/ACCEPTED nunca dão crédito só por receber WSS: o limite de crédito depende do ACK de escrita no TCP oposto. HELLO reconcilia ACK perdido com a confirmação que o Android conservou. ACK não pode ultrapassar bytes efetivamente enviados ao destinatário. Controles com campos inválidos, texto ou payload indevido falham; sem ressincronização cega.

Cada direção tem ring fixo. Append exige próximo offset ou repetição de prefixo já aceito. Repetição do trecho ainda retido é comparada; nunca duplica entrega. Trecho já entregue não é executado novamente. Buraco/sobreposição parcial/divergência/overflow tornam estado irrecuperável. Byte aceito é retido até a confirmação do TCP nativo, então pode ser reclamado. Buffer não cresce quando a rede para.

A estratégia é **continuidade dos dois TCP nativos**, com substituição só de WSS. NativeActivity/core ficam em RAM; netplay RetroArch continua o protocolo/rollback existente. READY comprova pausa, conexão nativa e escoamento das filas desta ponte; não é um hash de savestate nem prova de determinismo de todo core. A homologação dos frames/inputs reais continua obrigatória.

## 5. Pausa, ciclo de vida e limites

JNI `stationRecoveryControl`, `stationRecoveryStatus`, `stationRecoveryStalled`; mutex separa pedido Android de confirmação do thread de emulação. Status `(epoch<<3)|paused1|connected2|failed4`; -1 quando pedido ainda não confirmado. Apenas esse thread confirma PAUSED. Hook antes de `core_run`: poll/flush do protocolo nativo; sem `retro_run`/rollback/render_cached nesse caminho; sleep50ms. Timers antigos de stall ficam desativados **somente para launch Station v2 tipado**; clientes v1 e emuladores locais mantêm comportamento anterior.

O WSS é lido sem escrita bloqueante no TCP. Escritor TCP separado e limitado, backpressure; controles/pong não ficam presos na emulação parada. Java faz tick250ms e ping10s; reconexão autenticada com backoff limitado a10s e sem máximo de espera. Presença/renovação no processo principal a cada20s com fixed delay, inclusive oculto enquanto processo existir; duração da requisição pode somar ao intervalo e aparece no log. Background pausa, fecha attachment físico e conserva TCP/RAM; retorno pede credencial e nova barreira. Menu/vídeos herdados R67 continuam suspensos pelas políticas existentes.

Watch físico do servidor a cada10s: autorização de licença/aparelho com prazo5s, heartbeat autenticado≤60s e tráfego de manutenção≤30s. Falta de heartbeat/IO/DB temporária cancela o attachment e **não remove a sala v2**. Sem comprovação de acesso, não mantém fluxo físico para sempre. Expiração de sessão provoca renovação existente; licença/aparelho negados geram estado honesto sem tentativa de contornar prova. Revogação encerra participação autorizada. Não existe prazo de expulsão por mera perda de rede.

Padrão64 salas,256KiB/direção:32MiB de rings no servidor; mais objetos/transportes. Cada Android mantém dois rings do mesmo tamanho. Configuração aceita1–512 salas,32KiB–1MiB/direção e agregado≤128MiB; outras combinações falham na inicialização. Ex.:256 salas×256KiB×2=128MiB. Isso é teto de buffers, **não memória total medida nem garantia de capacidade/latência no Android ou túnel público**.

Sala em espera retém slot indefinidamente enquanto o processo existir; não é despejada por idade. Capacidade cheia rejeita novos ingressos; nunca expulsa automaticamente uma partida em espera para abrir vaga. Limpeza de salas encerradas é periódica15s; liberação física acompanha encerramento. Metadados do hub também têm limites globais anteriores. Não há gravação persistente: reinício do servidor ou perda do processo nativo destrói continuidade e exige nova partida, com explicação e saída humana. Não chama isso de recuperação persistida.

## 6. Erros e diagnóstico

| Código | Efeito |
|---|---|
| `STATION_RECOVERY_BUILD_REQUIRED`409 | Versão/engine/protocolo incompatível ou não habilitado |
| `STATION_RECOVERY_PROTOCOL_INVALID`400 | Valor de negociação desconhecido |
| `STATION_RECOVERY_PROTOCOL_MISMATCH`409 | Upgrade não corresponde ao ticket |
| `STATION_RECOVERY_GENERATION_MISMATCH`409 | Não pode anexar estado a outra partida |
| `STATION_ONLINE_BUILD_MISMATCH`409 | Edição/core/runtime/opções divergentes |
| `STATION_REQUEST_PROOF_REQUIRED/INVALID/EXPIRED/REPLAY`401 | Proteção existente; nonce/ticket novos quando cabível |
| `STATION_ONLINE_RELAY_TICKET_INVALID`401 | Ausente, expirado, consumido ou inválido |
| `STATION_ONLINE_RELAY_ALREADY_ATTACHED`409 | Uma ligação física por membro |
| `STATION_RECOVERY_FULL`503 | Teto físico/retido atingido; estado dos outros preservado |
| `STATION_RECOVERY_DISABLED`503 | Recurso desativado |
| `STATION_RECOVERY_UNRECOVERABLE`409 | Estado não pode ser continuado |
| `STATION_RECOVERY_FRAME_INVALID`400 | Framing inválido; sala irrecuperável após upgrade |
| `STATION_RECOVERY_HELLO_REQUIRED`409 | Reconciliação inicial ausente/inválida |
| `STATION_RECOVERY_OFFSET_INVALID/BYTES_MISMATCH/WINDOW_EXCEEDED`409 | Quebra de continuidade; não repetir execução |

Erros antes do101 são JSON finito; depois do101, STATE/fechamento são eventos WSS, não respostas HTTP409. Erro de capacidade detectado após upgrade fecha aquele attachment; a emissão seguinte continua sujeita ao teto e não deve resetar a partida para driblar o limite.

Controle registra UTC/UUID/correlação opaca/geração/função/modo de prova/idade de heartbeat e duração interna do comando. V1 registra primeiro fim por par antes de cancelamento conjunto e consequências. V2 registra primeiro fim por attachment, streamEpoch, categoria/close/exceptionTYPE e contadores; recuperações da mesma geração terão novos attachments. Java usa a mesma correlação/geração/função e registra estado/close/type e RTT do PONG. Não registra motivo textual arbitrário, segredo, mensagem, identidade pessoal ou caminhos. HTTP já tem duração da rota; `commandElapsedMs` não é RTT público.

A causa antiga de07/10às12:24:07 continua desconhecida. Instrumentação nova só produz evidência quando publicada e executada. Stacktrace JNI sanitizado dos `StackOverflowError` antigos e modo concreto de atestação dos aparelhos continuam pendentes de captura própria.
