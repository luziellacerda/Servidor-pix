> Cópia do handoff do app no commit `9d3d45f048aa44bb2ee9c41f567e985901628daa`. Os caminhos relativos abaixo pertencem a `TurboElden/versions/station-current-r55-20261006/`. [Fonte canônica](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/HANDOFF-APP-R55-PARA-SERVIDOR-20261006.md). Este documento é pedido APP → SERVIDOR, não retorno.

# APP → SERVIDOR: analisar a TurboStations R55 atual

Data: 06/10/2026. Destinatário: mantenedor/implementador do **Servidor-pix**.

## 1. Pedido e correção da referência

O mantenedor pediu expressamente: **“mande nova para ele analisar”**. A referência enviada anteriormente ao servidor parou na R41. As alterações R42–R55 existiam no PC e a R55 foi instalada, mas ainda não estavam publicadas. Esta entrega corrige essa defasagem.

**Analisar esta R55 antes de propor nova correção baseada na R41.** O delta de prontidão publicado pelo servidor em `d1b535cc35c926172182c47686b6b44333a529de` está preservado no histórico e em `versions/station-relay-readiness-20261006`. Ainda não foi incorporado no APK R55. Não existe R56 compilada por esta entrega.

O objetivo é conciliar a prontidão do anfitrião com a correção de comunicação entre processos e de encerramento que já está no aplicativo. Esta publicação não implanta serviços nem instala outro APK.

## 2. Identidade exata

| Artefato | Identificação |
|---|---|
| Pacote Android | `org.turboramastation.frontend` |
| Classes Java | `org.emulationstation.frontend.*` |
| Versão de trabalho | R55, candidata instalada; não estável geral |
| APK | `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R55-20261006.apk` |
| Tamanho | 2.093.278.660 bytes |
| SHA-256 APK | `4c8de4f899af291becdf22c0b551df5e03a813f7ecf536fb360436a5d24d7b39` |
| Certificado SHA-256 | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825` |
| `classes35.dex` — salas/online | `dfb7cd00e64a1cd9dd801f36573f8a737d80d7289acf654f9054b765dce7d414` |
| `classes28.dex` — cliente Station/login | `14fb0ecb30b6aaf0bd4321dc07e94926546280470515fa7bbec89c714cb5c154` |
| `libturbo_carousel.so` | `e275f4f1a8ea21bb1113547aa29582a90c3c60fce2cbf3d05698f8f3f7a59c46` |
| Runtime nativo online | `22ee3f67e4a5abf4625c2776928a8011a14c5ae0568d74d9576f4b49a9514905` |

Instalação confirmada por hash integral em **Motorola Edge 30**, preservando assinatura e dados. Não inferir que o outro participante/Samsung/POCO tem a mesma R55: a versão de cada participante precisa ser coletada separadamente. Serial, nome de comprador, código e tokens foram removidos desta publicação.

## 3. O que mudou desde a R41

`evidence/APK-DELTA-R41-R55.json` foi produzido lendo e calculando SHA-256 de **todas** as entradas descompactadas dos dois APKs, além do hash integral de cada arquivo. Não é uma lista inferida pelos nomes das pastas.

Mudanças funcionais: `classes28.dex`, `classes35.dex`, `libmdemu_station.so`, `libsnes9x_explus.so`, `libturbo_carousel.so`. Também mudaram os três registros de assinatura e entraram 14 recursos de interface; nenhuma entrada foi removida. Os hashes completos estão no relatório.

- R42 em diante: HUD e organização das salas, metadados/animações de avaliação, ajustes visuais e de botões. O HUD de SNES/Mega está em `exit-menus/StationHudView.cc`; as duas bibliotecas locais mudaram por essa integração. Não afirmar que todos os menus de todos os emuladores foram redesenhados.
- R53: login com painel compacto; controle do único vídeo em foco e do ciclo de pausa/parada. Plataforma, coleção e jogos são modos do mesmo `GuiStore`; não são três Activities de vídeo independentes. Reserva de parada é mantida se o JNI falha, evitando abrir outro decoder sobre uma parada incompleta. Transição para jogos/modal/loading/vazio/segundo plano para o vídeo; mesmo vídeo plataforma/coleção pode ser reaproveitado.
- R54: canal `ResultReceiver` seguro nos dois sentidos e encerramento idempotente da sessão online. Esta é a mudança crítica para conciliar com o candidato do servidor.
- R55: posição da faixa vermelha INSTALADO relativa ao retângulo real da textura de cada capa. Só o carrossel nativo mudou em relação à R54; 13.193 outras entradas foram preservadas. A dobra superior ainda é limitada pelo cabeçalho existente; aprovação visual final do usuário não foi obtida.

Não houve medição nova que demonstre redução de aquecimento no aparelho. Testes de política de vídeo não equivalem a medição de CPU/GPU.

## 4. Onde está cada fonte efetiva

| Publicação relativa a esta pasta | Origem de compilação no PC | Uso |
|---|---|---|
| `netplay-src/` + `dependency-src/` | `E:\ESTUDO APK\work\station-online-return-r54-20261006` | 156 fontes Java, DEX35 preservado na R55 |
| `client/src/` | `E:\ESTUDO APK\work\station-video-login-r53-20261006\client` | Licença, sessão, catálogo, capas, downloads e login; DEX28 |
| `native/` | `E:\ESTUDO APK\work\station-ribbon-anchor-r55-20261006\native` | Carrossel R55 |
| `native-dependencies/` | R45 e `station-download-performance-20261005\frontend-native` | Dependências de código resolvidas pelo comando nativo |
| `exit-menus/` | `E:\ESTUDO APK\work\station-buttons-rooms-r42-20261006\exit-menus` | HUD próprio de Snes9x EX+ e MD.emu |

Os 156 hashes de entrada Java coincidem com o recibo original de compilação; todos os fontes atuais do cliente coincidem com o ZIP de fontes emitido pelo seu build; os headers do overlay coincidem com o recibo nativo. Isso comprova a correspondência das entradas exportadas, não uma recompilação independente completa nesta publicação.

Entradas privadas/geradas e base binária estão fora do Git: mídia, arte da faixa, atlas Lottie, metadados gerados, objetos de recursos e APKs. `EXTERNAL-BUILD-INPUTS.json` identifica os arquivos existentes por caminho, tamanho e hash. O frontend C++ original completo não foi recuperado por esta entrega. Não inventar fontes ausentes, substituir por stubs nem declarar build completo apenas porque Java compila contra interfaces.

JDK usado: 17.0.20.101; Java `--release 8`; API34; D8 minAPI26. Nativo: NDK r28c, `aarch64-linux-android26`, C++17, alinhamento 16KiB. Receitas originais de Java/nativo estão em `recipes/original/`; caminhos precisam ser restaurados conscientemente. Receitas privadas de assinatura não foram copiadas. Não executar a receita R41 do candidato do servidor sobre o APK atual.

## 5. Fluxo online atual, sem suposições

Diretório Java de referência: `netplay-src/org/emulationstation/frontend/netplay/`.

1. `StationRoomsActivity` apresenta comunidade/salas, seleção, entrada, Pronto e início. `StationRoomState`, `StationRoomStartState` e `StationLaunchPolicy` mantêm as condições de anfitrião/convidado. Revisar os estados atuais; não antecipar o convidado para contornar ausência de prontidão.
2. `StationOnlineClient` usa `StationAndroid`, adquire `StationSessions.Lease`, chama `StationApi.online` com a sessão e invalida a sessão somente quando a falha corresponde à negação de sessão. Não há bearer fixo no novo snapshot.
3. `StationOnlineGame` e `StationRetroLaunch` preparam identidade de jogo/motor/opções e lançamento privado. Os hashes dos motores estão em `evidence/r54/engines.json`; as bibliotecas APK estão inventariadas em `evidence/APK-MODULES-R55.json`.
4. `StationGameSession.create` mantém o proprietário no processo principal e passa um `ResultReceiver` normalizado. `StationRetroActivity` roda no processo separado `:station_netplay` e usa o runtime RetroArch.
5. `StationRetroActivity.onStart` envia evento **1** com o canal de resposta também normalizado; evento **2** indica parada/ocultação; **3** indica prontidão do anfitrião; **4** encerra a sessão. Respostas de aviso usam código **100**.
6. `StationGameSession` recebe 3, marca `listening` e, quando visível, envia `host-listening`. Depois mantém heartbeat. Ao evento4 cancela o trabalho pendente, verifica se ainda está na mesma sala e solicita `leave`; encerra o executor.
7. Na R55, `StationRetroActivity.onNetplayListening` ainda depende do aviso nativo para `relay.listening()` e envio de3. O candidato do servidor pretende condicionar essa etapa à conexão TCP efetiva + WSS; **isso ainda não está nesta R55**.
8. Voltar do Android chama `StationExitPanel`. `finish` e `onDestroy` chamam `closeSession`, protegido contra repetição; fecha túnel, envia4 e remove o segredo de lançamento. Isso evita depender exclusivamente de `onDestroy` para notificar a saída.

SNES local usa Snes9x EX+; Mega local usa MD.emu. Online usa, respectivamente, bsnes-mercury e ClownMDEmu no runtime RetroArch. **Os controles online continuam sendo do RetroArch**, diferença reclamada pelo usuário ainda não resolvida. O servidor não troca esse frontend por uma rota. Neo Geo tem `launchReady=false` no manifesto atual; não anunciar todos os sistemas como habilitados.

## 6. Erro realmente observado no aparelho e correção R54

Capturado antes da R54:

```text
BadParcelableException: ClassNotFoundException when unmarshalling
org.emulationstation.frontend.netplay.StationRetroActivity$1
StationGameSession.event -> Bundle.getParcelable
```

Isso matou o processo principal do catálogo durante a comunicação da sessão online. Os logs originais permanecem privados no PC, em `station-online-return-r54-20261006\device-evidence\crash-before.txt`. A captura não deve ser publicada inteira com identificadores do cliente.

Correção efetiva:

- `StationSessionChannel.transport`: serializa com `Parcel` e recria com `ResultReceiver.CREATOR`, para transportar a classe do framework, não a subclasse anônima.
- `StationSessionChannel.read`: usa classloader do framework, valida o tipo e trata falhas de leitura sem derrubar o catálogo.
- `StationGameSession.create/event` e `StationRetroActivity.onCreate/onStart`: usam esses helpers em ambos os sentidos.
- `StationRetroActivity.closeSession/finish/onDestroy`: saída idempotente, notificação4 e limpeza do túnel/segredo.

454 verificações isoladas em Android reproduziram a falha antiga e conferiram callbacks nos dois sentidos. **Não são uma partida de dois aparelhos nem validação completa do retorno da emulação.** Não atribuir automaticamente ao mesmo erro Binder a tentativa registrada pelo servidor às21:02:53Z; falta correlação completa de execução/geração/logs.

## 7. Cruzamento com o retorno do servidor e conflito de integração

Retorno lido: Servidor-pix `b56eea5991ea8eb1bea3231ae26b8d043cf622f1`, `docs/station-android/RETORNO-BATTLETOADS-CONEXAO-HOST-STATION-20261006.md`.

O retorno relata, em06/10 às21:02:53Z: dois membros, ambos Pronto, start200, geração3, transporte `relay-wss-v1`, relay-ticket200 do anfitrião, WSS aberto e encerrado após60.001,8673ms; nenhum `host-listening` observado e nenhum novo byte de jogo. O convidado ficou `starting`. Isso delimita uma etapa; não demonstra isoladamente qual falha nativa/JNI/Binder/socket ocorreu.

Produção relatada pelo servidor: API fonte `a2bb176530fd4d2dfa740da7e934fd84d097404e`, DLL `d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13`, PID875574, catálogo revisão14/2.212, SocialEnabled=true. São evidências do retorno, não uma nova inspeção de produção feita neste PC. A correção de clipboard do site é independente do APK.

Candidato do servidor: commit de implementação `30e7770e97b56f60c092fae856d316e81c437c69`, fonte final `d1b535cc35c926172182c47686b6b44333a529de`. Adiciona `StationHostConnector` (conserva a primeira conexão TCP real, até45s, nova tentativa a cada50ms), altera `StationRelayTunnel` e `StationRetroActivity`. Mantém pin, ticket, limites de frame32KiB, encaminhamento16KiB e fila256KiB. Os 39 checks e12.583.029bytes por direção relatados são homologação isolada; não houve APK/dupla física desse candidato.

**Conflito concreto:** a Activity do candidato foi derivada da R41 e lê `getParcelableExtra` diretamente e cria resposta anônima sem a normalização da R54. Copiá-la inteira por cima da R55 perderia a correção do erro capturado e o novo fechamento/HUD.

Conciliação solicitada:

| Arquivo | O que preservar da R55 | O que analisar/incorporar do candidato |
|---|---|---|
| `StationSessionChannel.java` | Integralmente; API privada de transporte/leitura | Conferir uso nos dois sentidos e em todas as saídas |
| `StationGameSession.java` | Canal normalizado e ciclo de heartbeat/leave | Conferir apenas a origem e sequência correta de evento3 |
| `StationRetroActivity.java` | Leitura normalizada, resposta normalizada, `closeSession` idempotente, Voltar, `StationExitPanel`, limpeza | Listener de prontidão TCP+WSS e avisos pendentes, sem copiar a versão antiga inteira |
| `StationRelayTunnel.java` | Identidade/segurança e compatibilidade com a Activity atual | Delta de prontidão e encerramento, sem emitir sucesso antes de TCP+WSS |
| `StationHostConnector.java` | Arquivo novo, inexistente na R55 instalada | Incorporar se os testes conciliados confirmarem retenção da conexão útil |

Não usar uma conexão de teste descartável que consuma o socket do primeiro jogador. Não forçar `connecting`, não remover verificação de motor/jogo/opções, não gerar outra licença para contornar falha de partida.

## 8. Evidências e lacunas

Já comprovado: hashes do APK e módulos, instalação preservando dados, abertura do catálogo com sessão, inspeção da faixa em uma capa SNES; 454 checks Android do Parcel; testes Java locais113/20/107/15 e testes nativos de faixa10.841 e política de vídeo199.592. Os logs individuais estão em `evidence/`. Não foram repetidos como uma partida nesta publicação.

Ainda **não** comprovado: dupla física host/convidado na R55; `host-listening` e bytes reais de Battletoads após conciliação; saída de uma partida online R55 voltando à sala/catálogo; latência externa; consumo térmico depois dos ajustes; versão do segundo aparelho; controles próprios no online.

Não declarar a partida corrigida com base apenas em start200, túnel WSS aberto, testes isolados ou compilação Java.

## 9. Retorno técnico solicitado ao servidor

Publicar `docs/station-android/RETORNO-ANALISE-APP-R55-STATION-20261006.md`, citando **o commit completo desta fonte R55**, e responder:

1. **R55-01 Base:** quais arquivos/hashes desta entrega foram lidos; confirmar abandono da R41 como base atual.
2. **R55-02 Binder:** verificar que a proposta mantém `StationSessionChannel` nos dois sentidos e não reintroduz a subclasse anônima no Parcel.
3. **R55-03 Prontidão:** explicar a sequência TCP/WSS/evento3/ack e o tratamento de falha/timeout/cancelamento; fornecer diff conciliado sobre a R55 caso altere código.
4. **R55-04 Convidado:** confirmar elegibilidade e transições sem antecipar a partida e sem mudar silenciosamente o motor/ROM/opções.
5. **R55-05 Saída:** revisar Voltar, fechamento nativo, finish/onDestroy e leave, inclusive falha de rede e retorno das configurações; preservar catálogo e licença.
6. **R55-06 Correlação:** indicar os identificadores não secretos necessários para cruzar tentativa/room/startGeneration/papel/etapa com o Android e apontar a primeira divergência comprovada. Não publicar IDs pessoais ou tickets.
7. **R55-07 Contrato:** dizer objetivamente se a API efetivamente ativa exige mudança. Se não exige, registrar “sem alteração de servidor necessária para este delta”. Separar fonte, binário publicado e testes.
8. **R55-08 Pendências:** listar o que precisa de dois celulares e o que pode ser concluído no código/ambiente isolado; não transformar ausência de prova em diagnóstico.

Análise e testes devem preservar outros produtos, autenticação, catálogo, download e controles locais. Não executar implantação nem trocar APKs por consequência da leitura deste pedido. Retornar caminhos, commits completos, resultados e limitações; não responder apenas “pronto”.
