# Incidente físico R168 PSX — reter publicação

**Estado em 15/10/2026, 19h34 (Fortaleza):** um teste real com o APK R168 assinado (`SHA-256 875fb0696c4fb50ce4af953bccd62fcd68dfdecb247f65d2fc027b71aa330b3d`) reproduziu a saída involuntária da partida PSX para a tela de salas. Não há partida PSX R168 homologada.

## Evidência sanitizada

- O Android registrou três exceções fatais consecutivas na thread `WebSocketConnectReadThread`, às 19h34min27s, 30s e 34s.
- Exceção: `java.lang.NoSuchMethodError` ao resolver `StationSinglePassVideo720$2$$ExternalSyntheticBackportWithForwarding0.m(AtomicReference,Object,Object)Z`, chamada de `StationPsxSession$Session.nativeControl` (linha 99).
- O sistema fechou `StationPsxOnlineActivity` e devolveu o usuário a `StationRoomsActivity`. Isto explica a sala parecer entrar e sair. A falha foi observada no processo Android do app; não é evidência de que o servidor expulsou o participante.
- Este documento não inclui logcat bruto, credenciais, identificadores de telefone, sala ou usuário.

## Situação independente do servidor

O último recibo remoto de `Servidor-pix`, `docs/station-android/r168/server-return.json` no ramo `fix/station-r168-capacity-20261015` (commit `660ed436`), registra `serverApplied=false`, `serverRegistrationConfirmed=false` e `serverConsumerReady=false`. O pré-teste de cadastro foi recusado pelo tamanho do perfil WinBack (137.167 B projetados, limite 131.072 B). Houve alteração posterior de código para o orçamento (`b1d5364`), mas não há recibo de aplicação nem consulta pública assinada após essa alteração. O registro R167 continua ativo. A falha Android acima é concreta e independente desse bloqueio de cadastro.

## Ação de publicação

**Reter a aplicação da R168 e qualquer declaração de prontidão PSX.** Corrigir cirurgicamente a chamada ausente no APK, comprovar a presença da classe/método no artefato assinado e repetir o teste Android real. Depois gerar o kit privado da nova revisão, sem APK no Git, e só então retomar o cadastro aditivo, pré-teste completo, consulta pública autenticada e partida física de dois jogadores. Preservar N64 e os perfis históricos. O teste de saída do anfitrião e retorno do convidado permanece pendente.

Este é um diagnóstico para coordenação; não é recibo de implantação nem autorização para contornar os limites do servidor.
