# Auditoria do transporte da barreira inicial — R73

Escopo: leitura do Java efetivo R71 preservado na R72, do servidor no commit `c1e44a1225a101478ceb29f4e624885331872c0d` e da fonte nativa `E:/R71fixed/RetroArch-69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`. Nenhuma alteração de servidor, aparelho ou credencial foi executada por esta auditoria. Isto é revisão de código, não comprovação de gameplay.

## O significado dos estados observados

- `StationStreamSession.Receive` só promove WAITING (0) a SYNCHRONIZING (1) quando ambas as conexões têm HELLO, PAUSED e não estão suspensas.
- Portanto STATE epoch1/1 demonstra a confirmação de pausa dos dois motores. PONG só demonstra a atividade do canal WSS.
- `StationRecoveryTunnel.java:107` só envia READY se a confirmação JNI pertencer à mesma epoch, tiver bits `paused | connected`, e os bytes do Java estiverem aceitos/entregues. Na epoch1: `nativeStatus=9` significa pausado sem conexão nativa concluída; `11` significa pausado com conexão nativa concluída.
- O servidor exige READY dos dois e `Pending==0` das duas janelas para STATE2. Não há evidência nesta revisão para eliminar essa barreira ou forçar STATE2.

## Auditoria dos offsets

O contrato Java coincide com `StationRecoveryRelay.cs`:

| Mensagem | Java | Servidor |
| --- | --- | --- |
| HELLO | prefixo de envio aceito; prefixo de recepção entregue ao TCP | compara com janela própria e confirma janela de entrada |
| WELCOME/ACCEPTED | `accepted=offset`, libera retenção até `value` | `offset=own.Accepted`, `value=own.Delivered` |
| DATA | bytes lidos do TCP nativo, offset monotônico | acrescenta na janela da origem e disponibiliza ao outro lado |
| ACK | enviado depois do write no TCP nativo destinatário | confirma janela de entrada, limitado pelo máximo realmente enviado |
| READY | epoch e total `tx.next` | epoch atual, PAUSED e total igual ao prefixo aceito da origem |

Não foi identificada divergência de endian, opcode, sentido ou unidade desses campos. Os dois lados usam cabeçalho TSR2 de 24 bytes e blocos até 16.384 bytes. Bytes ainda retidos no `send_packet_buffer` do RetroArch são invisíveis a todos esses contadores Java/servidor.

## Envio nativo que a pausa deixou de executar

Na fonte efetiva R71:

1. `netplay_frontend.c:5021` enfileira a resposta MODE com `netplay_send_raw_cmd`.
2. `netplay_frontend.c:5749–5773` só muda o convidado para `NETPLAY_CONNECTION_PLAYING` após receber a mensagem MODE com a indicação YOU/PLAYING.
3. O flush comum de cada conexão ACTIVE está em `netplay_post_frame`, linhas 9075–9083.
4. `station_netplay_recovery_poll`, linhas 8967–8975, chama somente `netplay_sync_pre_frame` e consulta os estados. O hook de pausa do runloop retorna antes do post-frame.

Isso cria uma dependência circular demonstrável no código: o convidado espera MODE para confirmar conexão; MODE pode ficar no buffer nativo; o flush comum só rodaria depois de sair da pausa; sair da pausa exige READY do convidado.

A correção coerente com o transporte é fazer flush **não bloqueante** dos buffers das conexões ACTIVE durante a preparação/recuperação, sem executar `retro_run`, áudio, replay ou post-frame de emulação. Não é necessário mudar frames, offsets ou API do relay para permitir que esses bytes alcancem o Java.

### Tratamento obrigatório do flush

`netplay_send_flush(..., false)` retornar true significa ausência de erro de socket, não que todos os bytes saíram. A implementação upstream pode enviar zero ou um prefixo e conservar o restante. A confirmação nativa de conexão pronta deve permanecer falsa enquanto o buffer tiver bytes pendentes; o ciclo seguinte tenta novamente. Uma falha real de socket deve manter o caminho de erro explícito de recuperação. Não tratar EAGAIN/zero bytes como desligamento, nem reiniciar automaticamente o motor para resolver essa espera.

## Lacuna dos testes anteriores

`StationRecoveryTransportTest.java` documenta que a pausa nativa é sintética. Sua função `ready()` (linha50) já coloca `connected=true` quando o TCP local está conectado, e `nativeStatus()` (linha53) devolve esse valor artificialmente. O teste exercita Java/TLS/WSS/.NET e entrega exata de bytes, mas não passa pelo handshake RetroArch nem pelo buffer MODE. Os testes de wire/ledger também não executam esse handshake. Assim, o resultado anterior de testes não contradiz o bloqueio nativo encontrado.

Regressões necessárias para o novo hook: resposta MODE enfileirada durante pausa; envio total, parcial e zero; nova tentativa sem duplicação; buffer circular; conexão inativa; falha de socket; nenhuma execução de frame/audio/rollback; READY somente depois de handshake e buffers drenados. A confirmação final ainda requer os dois Android na mesma revisão.

## Registro obrigatório do novo binário

Recompilar `libstation_retroarch.so` altera seu SHA-256. Não se pode conservar o hash antigo no manifesto ou dispensar sua conferência:

- `StationOnlineGame.prepare`: compara engineId/core/runtime contra a lista assinada pelo servidor e depois calcula o SHA-256 dos arquivos locais.
- `StationOnlineGame.verifyRoom`: exige os mesmos IDs/hashes da sala.
- `StationOnline.cs:300`: criação compara os hashes ao registro de motores.
- `StationOnline.cs:313–315`: entrada exige core/runtime/content/options iguais à sala.
- `StationOnline.cs:374–375`: emissão e retomada de ticket verificam engineId e todos os hashes vinculados à sala.

A entrega R73 precisa de IDs novos para SNES e Mega e do hash integral do novo runtime no manifesto do APK e no registro **aditivo** do servidor, preservando as seis entradas já publicadas. Os hashes dos cores/opções só mudam se esses arquivos forem realmente alterados. O operador do servidor deve publicar as adições antes do teste de criação/entrada da R73. R71/R72 e R73 não devem ser misturadas na mesma sala; uma sala nova e os dois aparelhos atualizados são necessários. Não é necessária mudança do contrato TSR2 para o flush; não fazer implantação Linux nesta tarefa por consequência da revisão.

## Diagnóstico sem conteúdo pessoal

Se o bloqueio persistir, registrar somente ao mudar: epoch, state, nativeStatus, tx.next, accepted, rx.next, rx.delivered, cursor, localReady e readySent. Nenhuma ROM, payload, token, código de sala ou chave precisa aparecer. Isso distingue espera do nativo, bytes não enviados, bytes não entregues e READY ausente sem presumir falha da internet.
