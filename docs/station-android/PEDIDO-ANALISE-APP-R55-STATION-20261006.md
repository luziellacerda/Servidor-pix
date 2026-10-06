# Pedido do mantenedor: analisar o aplicativo R55 atual

**APP → SERVIDOR. Este arquivo é um pedido de revisão, não é retorno do servidor nem uma implantação.**

O mantenedor perguntou por que o servidor ainda trabalhava com a versão antiga e mandou: “mande nova para ele analisar”. A defasagem era real: a última fonte disponibilizada era a R41, enquanto R42–R55 estavam no PC/telefone. A R55 foi agora publicada e conferida no Git remoto.

## Fonte obrigatória desta revisão

- Repositório: `luziellacerda/TurboElden`.
- Branch de revisão: `review/station-r55-server-20261006`.
- Linha atual do app também atualizada: `feat/station-capas-visuais-netplay-20261003`.
- **Commit completo: `9d3d45f048aa44bb2ee9c41f567e985901628daa`.**
- [Handoff integral APP R55](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/HANDOFF-APP-R55-PARA-SERVIDOR-20261006.md).
- [Mapa do snapshot](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/README.md), [manifesto de fontes](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/SOURCE-MANIFEST.json), [estado atual](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/STATUS.json).
- [Diferenças reais entre APK R41 e R55](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/evidence/APK-DELTA-R41-R55.json).

APK local instalado no Motorola Edge 30: `TurboStations-Premium-R55-20261006.apk`, 2.093.278.660 bytes, SHA-256 `4c8de4f899af291becdf22c0b551df5e03a813f7ecf536fb360436a5d24d7b39`. DEX salas atual: `dfb7cd00e64a1cd9dd801f36573f8a737d80d7289acf654f9054b765dce7d414`. Assinatura e dados preservados. Não há APK/binários privados no Git. Não assumir que o segundo aparelho usa essa versão.

Uma cópia integral do handoff está em [HANDOFF-APP-R55-RECEBIDO-20261006.md](HANDOFF-APP-R55-RECEBIDO-20261006.md). Os caminhos relativos dentro dessa cópia se referem ao snapshot do app acima. Manifestos e resultado da prova Android estão em `app-r55-review-20261006/`.

## Conflito concreto a resolver antes de montar a próxima versão

Seu retorno `b56eea5991ea8eb1bea3231ae26b8d043cf622f1` e o candidato app `d1b535cc35c926172182c47686b6b44333a529de` foram lidos. O candidato de prontidão foi feito sobre a **R41**. Ele não está integrado na R55, e não existe R56 compilada nesta entrega.

No Android foi capturado antes da R54 `BadParcelableException / ClassNotFoundException when unmarshalling StationRetroActivity$1`, em `StationGameSession.event → Bundle.getParcelable`. A R54 adicionou normalização dos ResultReceivers nos dois sentidos e encerramento idempotente, preservados na R55. Foram454 verificações isoladas no Android; isso não equivale a gameplay em dupla.

**Não copiar sua `StationRetroActivity` da R41 por cima da atual.** Isso perderia `StationSessionChannel`, a resposta normalizada, `closeSession` em finish/onDestroy e o HUD novo. Analisar/conciliar o conector TCP efetivo + prontidão WSS sobre os arquivos atuais:

- [StationSessionChannel.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationSessionChannel.java).
- [StationGameSession.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationGameSession.java).
- [StationRetroActivity.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationRetroActivity.java).
- [StationRelayTunnel.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationRelayTunnel.java).
- [StationRoomsActivity.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationRoomsActivity.java).
- [StationOnlineGame.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationOnlineGame.java).
- [StationExitPanel.java](https://github.com/luziellacerda/TurboElden/blob/9d3d45f048aa44bb2ee9c41f567e985901628daa/versions/station-current-r55-20261006/netplay-src/org/emulationstation/frontend/netplay/StationExitPanel.java).

O registro do servidor às21:02:53Z localiza ausência de host-listening após start/ticket200 e WSS aberto, com convidado starting. Não identificou sozinho a causa JNI/Binder/socket. Não tratar a captura Binder como prova automática de ser a mesma tentativa; correlacionar geração, etapa e execução. Não forçar connecting, descartar a primeira conexão TCP útil ou regenerar licença como contorno.

## Entrega pedida ao servidor

Publicar **`docs/station-android/RETORNO-ANALISE-APP-R55-STATION-20261006.md`**, citando o commit completo `9d3d45f048aa44bb2ee9c41f567e985901628daa`, e responder os itens do handoff:

1. **R55-01:** fonte atual e arquivos/hashes efetivamente lidos.
2. **R55-02:** preservação do canal Binder nos dois sentidos, sem subclasse anônima atravessando Parcel.
3. **R55-03:** sequência TCP real/WSS/evento3/ack, cancelamento e timeout; diff sobre R55 se necessário.
4. **R55-04:** convidado entra e inicia só no estado correto, com jogo/motor/opções compatíveis.
5. **R55-05:** Voltar, saída nativa, finish/onDestroy, leave e retorno ao catálogo sem perder licença.
6. **R55-06:** primeira divergência comprovada e identificadores não secretos de correlação que o PC deve capturar.
7. **R55-07:** necessidade real de alteração na API ativa, distinguindo fonte, homologação e produção.
8. **R55-08:** pendências físicas de dois aparelhos e limites dos testes isolados; controles online próprios e aquecimento ainda não resolvidos por esta entrega.

Se não houver alteração de backend necessária para esse delta, registrar isso explicitamente. A base do app agora é R55. Os retornos R41 continuam válidos como histórico do contrato e da produção registrada, não como APK atual.

## Limites e evidências

Preservar catálogo, capas, download, licença, assinatura, saves, emuladores locais e demais produtos. Não implantar serviços, fazer migration ou instalar APK por consequência deste pedido. O app online continua usando o runtime RetroArch; SNES/Mega locais usam seus frontends próprios. A diferença de controles reclamada pelo usuário não foi resolvida por uma configuração do servidor.

Foi publicada fonte e documentação com hashes verificados; **não** foi declarada estabilidade geral, partida em dupla corrigida, ganho térmico medido ou aprovação visual definitiva. O mantenedor precisa do retorno sobre o código atual para evitar nova integração sobre uma base antiga.
