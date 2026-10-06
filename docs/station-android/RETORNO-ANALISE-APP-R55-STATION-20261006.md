# SERVIDOR → APP: R55 analisada e prontidão conciliada

Data: 06/10/2026. Resposta ao pedido publicado no Servidor-pix `5722e50b19a19202bc01051277efb5fb7d712b89`.

**Fonte atual recebida:** TurboElden `9d3d45f048aa44bb2ee9c41f567e985901628daa`. **Implementação conciliada:** `8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1`, em [station-relay-readiness-r55-20261006](https://github.com/luziellacerda/TurboElden/tree/8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1/versions/station-relay-readiness-r55-20261006).

Resultado: delta implementado sobre R55, preservando o canal e a saída R54; 39 verificações de transporte e 255 de regras de salas passaram. 157 fontes Java compilaram em Java8/API34, somente **api-check-only**. **Sem alteração de servidor necessária para este delta. Não há novo DEX/APK compilado ou instalado nesta entrega Linux; gameplay físico continua pendente.**

## R55-01 — Base e arquivos conferidos

A R41 foi abandonada como base de integração atual. O snapshot R55 recebido permanece intacto. Conferidos tamanho/SHA-256 dos **346 arquivos** do manifesto; os 156 fontes Java de salas/dependências correspondem à exportação. Foram lidos handoff, estado, receitas, manifestos de dependências, comparação integral R41→R55, inventário dos módulos APK, erro/resultado Android R54 e os componentes de sessão, sala, lançamento, API, segurança e saída.

APK de referência recebido: `4c8de4f899af291becdf22c0b551df5e03a813f7ecf536fb360436a5d24d7b39`, 2.093.278.660 bytes, instalado no **Motorola Edge 30** segundo o recibo. DEX35: `dfb7cd00e64a1cd9dd801f36573f8a737d80d7289acf654f9054b765dce7d414`; DEX28: `14fb0ecb30b6aaf0bd4321dc07e94926546280470515fa7bbec89c714cb5c154`. Este Linux não recebeu esses binários integrais nem refez seus hashes. A versão do Samsung/POCO/outro participante não pode ser inferida.

Hashes dos arquivos **recebidos**, antes do delta:

| Arquivo | SHA-256 |
| --- | --- |
| `StationSessionChannel.java` | `678037ceb5f34245c2a5a816727e0c3f81c10d9b6703fecb8a0041267b137ac8` |
| `StationGameSession.java` | `8d7d269389abb257d7d50e374d5cbf3584c4cc78f299c3b8c4351ad3d0fcaac7` |
| `StationRetroActivity.java` | `aae6c8f7273c9aac55cd3507a84ffa616e61b33a3b102c4327ad898738b04eb2` |
| `StationRelayTunnel.java` | `d2e1cecc9948a4143905a7a44bbea6a3096c01c6494b75abeb0d2437465fb801` |
| `StationLaunchPolicy.java` | `283b5d5f1da0b9660d45ce730efc9205a649a6046df4cda33421fcf67a5419c4` |
| `StationExitPanel.java` | `a443148b772163c7ef99c2fb66d19027cc80ec4ffdf973a17d250e510d2e4058` |

`app-r55-analysis-20261006/received-inputs.json` contém os demais arquivos lidos. `source-preservation.json` lista os **154 fontes Java preservados**, os dois alterados e o novo conector. O [diff aplicável sobre R55](https://github.com/luziellacerda/TurboElden/blob/8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1/versions/station-relay-readiness-r55-20261006/R55.patch) foi aplicado em uma cópia privada da fonte exata; todos os 157 arquivos resultantes coincidiram com o overlay testado. Bytes do Git também foram conferidos contra o manifesto, incluindo terminações de linha.

## R55-02 — Binder nos dois sentidos

`StationSessionChannel` e `StationGameSession` ficam **byte a byte iguais à R55**. A Activity conciliada continua lendo o canal inicial por `StationSessionChannel.read` e envia a resposta do evento1 por `StationSessionChannel.transport`. O proprietário também continua normalizando seu receptor e lendo a resposta com o helper.

Assim, os receptores locais podem continuar sendo subclasses anônimas, mas a representação que atravessa Parcel é recriada por `ResultReceiver.CREATOR`, da classe do framework. O classloader, validação de tipo e tratamento de leitura inválida permanecem. Não foi reintroduzido `getParcelableExtra` direto da proposta R41.

Os **454 checks Android** de Parcel/callback são prova recebida do build R54. Não foram executados novamente neste Linux, que não tem um aparelho Android conectado. A preservação é verificável pelos hashes e pela compilação; a próxima instalação deve repetir o teste do canal e o fluxo físico.

## R55-03 — TCP, WSS, evento3 e confirmação

Arquivos entregues: `StationHostConnector.java` novo; `StationRelayTunnel.java` e `StationRetroActivity.java` conciliados. O restante vem da R55 atual.

Sequência:

1. `start200` publica `starting`; o anfitrião obtém seu ticket e abre o motor/túnel.
2. O conector tenta a porta real `127.0.0.1:55435`, conforme o lançador atual, por até45s. Nova tentativa a cada50ms; tentativa individual limitada a250ms e ao prazo restante. **A primeira conexão útil é conservada**, sem socket descartável.
3. O aviso JNI libera uma dica para acelerar a tentativa. Sua ausência deixa de impedir uma porta TCP efetivamente aberta.
4. O túnel exige conexão TCP real e WSS aberto. Mantidos TLS/pin, host, protocolo `station-relay.v1`, rota, ticket, limite de mensagem32KiB, encaminhamento16KiB e fila256KiB. A espera adicional por abertura WSS é limitada a15s; o timeout de conexão WSS existente é10s. O convidado continua usando listener local com prazo60s.
5. `Listener.ready` agenda a confirmação na UI. A Activity exige túnel disponível, sessão não encerrada, Activity não finalizada/destruída e ausência de parada anterior. Marca `listening`; envia3 quando o anfitrião está visível. Se a conexão ficou pronta antes de `onStart`, só reenvia3 se o túnel ainda estiver disponível.
6. O proprietário R55 recebe3 e envia `host-listening`. Somente após aceitação do servidor o convidado recebe `connecting` e passa a ser elegível. No relay, a API mantém a geração dos tickets nessa transição.

Cancelamento fecha o socket pendente, sinaliza a espera, fecha WSS/listener local e encerra o executor. Falhas são distinguidas em `NATIVE_LISTENER`, `RELAY`, `LOCAL_STREAM` e `PROTOCOL`. Não há sucesso artificial em timeout. Um aviso anterior à visibilidade fica pendente; prontidão atrasada após fechamento/parada é descartada. A abertura do motor também registra falhas Java/LinkageError por tipo, sem incluir mensagens com segredos.

O TCP/WSS estabelecido comprova a etapa de transporte. Não comprova sozinho que o protocolo do motor concluiu sua negociação nem que os dois jogadores controlam a partida.

## R55-04 — Convidado e compatibilidade

`StationLaunchPolicy`, `StationRoomStartState`, `StationOnlineGame`, `StationRetroLaunch`, `StationRoomsActivity` e modelos de sala/entrada foram preservados.

- Anfitrião elegível em `starting` ou `connecting`; convidado só em `connecting`.
- Dois membros distintos, ambos Pronto, anfitrião autorizado e transporte disponível continuam necessários.
- O jogo da sala, `contentSha256`, `engineId`, `coreSha256`, `runtimeSha256` e `optionsSha256` continuam conferidos. O delta não troca motor, ROM ou opções.
- SNES online mantém bsnes-mercury; Mega online mantém ClownMDEmu; runtime recebido `22ee3f67e4a5abf4625c2776928a8011a14c5ae0568d74d9576f4b49a9514905`. Neo Geo permanece `launchReady=false` no manifesto atual.

Foram executados os quatro testes Java atuais:107 de lobby/códigos/revisão,20 de entrada,15 de elegibilidade/geração e113 de comunidade. Isso mantém as regras e evita lançar o convidado para contornar falta de prontidão.

## R55-05 — Saída, rede e retorno

A Activity mantém `StationExitPanel`, Voltar por tecla/callbackAndroid33, saída nativa e `closeSession` chamado por `finish/onDestroy`. O fechamento continua idempotente: fecha túnel, envia4 uma vez e limpa o segredo; o delta também limpa `listening` e impede callbacks posteriores de anunciar prontidão.

O proprietário atual cancela heartbeat/trabalho pendente, consulta a sala e solicita `leave` somente se ainda corresponde à sessão encerrada. Se a rede falha, encerra o executor; a tela de salas conserva a tentativa de `leave` ao retornar, antes de `enter`. O servidor fecha a sala ao perder o relay ou expirar a presença. A licença e o catálogo não são apagados nem reativados.

`onStop` continua fechando o relay. Se abrir configurações/segundo plano provocar `onStop`, a participação é encerrada; no retorno aparece o aviso e é necessária outra partida. Não foi implantada retomada transparente de uma conexão já encerrada. O fluxo exato de configurações e saída até o catálogo **ainda precisa de teste físico**; as regras de código/compilação não são essa prova.

## R55-06 — Divergência comprovada e correlação

A observação anterior do servidor, em **06/10 às21:02:53Z**, mostrou Battletoads in Battlemaniacs (USA), dois membros/ambosPronto, start200/geração3, ticket200 e WSS do anfitrião, encerrado após60.001,8673ms. Não houve `host-listening` observado nem bytes de jogo novos; o convidado permaneceu `starting`. Essa é a primeira divergência comprovada entre o início aceito e a confirmação esperada.

A captura do PC antes da R54 demonstrou `BadParcelableException / ClassNotFoundException` no retorno `StationRetroActivity$1`, corrigida pelo canal R54. **Não existe correlação suficiente para afirmar que foi a mesma tentativa das21:02:53Z**, nem que era o mesmo APK/aparelho/papel. O problema da dependência exclusiva de JNI existe no túnel recebido, mas também não foi demonstrado como causa única daquela execução humana.

Para a próxima tentativa, o PC deve registrar privadamente:

| Campo | Uso |
| --- | --- |
| HorárioUTC e alias aparelhoA/B | Alinhar os logs sem publicar nome/serial |
| SHA integral APK e DEX35 em cada telefone | Identificar a implementação instalada |
| `instance`, `roomId`, `generation` de início, papel | Ligar a mesma sala/execução entre dispositivos e servidor |
| `requestId`, ação, status/código e etapa | Distinguir transporte, sessão, motor e confirmação |
| Hashes públicos de motor/runtime e identidade do item | Conferir compatibilidade sem copiar ROM/segredos |
| Estado recebido pelo convidado e bytes/inputs reais | Confirmar entrada efetiva e gameplay |

Etapas Android existentes/novas: `launch stage=prepare`, `relay-ticket-received`, `activity`, `native-listening` quando houver, `local-stream-ready`, `host-listening-ack`, `relay-failed`, `native-start-failed`, `session-sync`, `session-ended`. A ausência de JNI é admissível no novo fluxo; a conexão TCP real e WSS continuam obrigatórias.

Logs brutos devem permanecer privados. Relatório público usa alias de sala/aparelho e geração; não incluir códigos, senha da sala, tickets, bearer, grants de arquivo, compradores ou nicknames. No agregado publicado aqui, caminhos dinâmicos foram substituídos por modelos de rota.

## R55-07 — Contrato e produção

**Sem alteração de servidor necessária para este delta.** O projeto da API recebido em `5722e50` não tem diferenças de fonte frente à API publicada `a2bb176530fd4d2dfa740da7e934fd84d097404e`. Foram comparados projeto e componentes de endpoints, sala, registro e relay. O cliente R55 mantém:

| Operação | Contrato esperado |
| --- | --- |
| Comandos | `POST https://app.lzgames.com.br/v1/station/online/command`, sessãoBearer e corpo até8192bytes |
| Eventos | `POST .../v1/station/online/events`, requestId/instance/revision/page e sessãoBearer |
| Resposta | Envelope assinado `TurboRamaStationAndroid/online/v1`, ligado a produto/aplicação/licença/aparelho/sessão/requestId |
| Relay | `GET` com upgradeWSS em `/v1/station/online/relay`, subprotocolo e `Authorization: StationRelay <ticket>` |
| Prontidão | ação `host-listening` do anfitrião, estado `starting → connecting` |

Um GET comum em comando/eventos ou um GET sem upgrade no relay não implementa esse fluxo. Não colocar ticket em querystring, não usar bearer fixo e não trocar a autoridade/pin. O cliente continua verificando a resposta assinada e invalidando a sessão apenas quando a falha corresponde a negação da sessão.

Nova inspeção de leitura às**22:03:33Z**: API ativa/PID875574/ExecStart releasea2bb176; managementPID910766 e helperPID910776; relay512salas/1024conexões, zero ativos e131078bytes históricos do teste sintético anterior. Não houve bytes humanos novos comprovados por esse agregado.

DLL ativa: hash `d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13` **herdado da publicação protegida às20:59:30Z**, conferida no retorno0820fd0. Este trabalho não refez leitura root da DLL; confirmou PID/ExecStart e fonte correspondente. Não apresentar o hash herdado como nova leitura do binário.

Janela21:20–22:04:56Z:638respostas200, incluindo213capas,28catálogos,2artefatos,119comandos e217eventos; nove499 em eventos. Não observado404/500 nessa janela.499 é registro de cancelamento do longpoll; esse agregado não exclui erro nativo e não prova gameplay. Evidências: `app-r55-analysis-20261006/production-readonly.json` e `http-status.json`.

Nenhum serviço foi reiniciado/implantado, nenhuma migration ou licença real alterada nesta revisão. O inventário dos outros produtos foi conferido; a falha histórica do monitor de conteúdo foi apenas registrada.

## R55-08 — Entrega e validação restante

| Evidência nova neste Linux | Resultado e limite |
| --- | --- |
| Fonte recebida | 346arquivos por tamanho/SHA; snapshot intacto |
| Conciliação | 154Java preservados,2alterados,1novo; patch aplicado à fonte exata |
| TCP/TLS/relay | 39checks passaram,12.583.029bytes por direção,50inputs ordenados; endpoints TCP sintéticos |
| Regras de salas/comunidade | 255checks Java atuais passaram |
| JavaAndroid | 157fontes Java8/API34 compiladas; classpath derivado, api-check-only; sem DEX |
| Empacotamento | Sintaxe e recusas de reciboR41/api-check-only verificadas; pacote assinado completo ainda não executado |
| BinderAndroid | 454checks recebidos da R54; código preservado, não repetidos aqui |
| Baseline semJNI | Evidência isolada anterior, componente R55 idêntico por hash; não é captura Android nova |

As provas estão também no [diretório de evidências do delta](https://github.com/luziellacerda/TurboElden/tree/8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1/versions/station-relay-readiness-r55-20261006/evidence). Recursos e fontes nativas externos não foram inventados ou substituídos. O delta compila o módulo de salas; não recompila o carrossel ou os motores.

### Próxima ação no PC do APK

1. Capturar versão instalada de ambos e logs privados da falha atual antes de substituir o APK.
2. Buscar a implementação `8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1` e o retorno publicado nesta branch. Executar **as novas receitas R55** no [README do delta](https://github.com/luziellacerda/TurboElden/blob/8e62ed2e40c360ba9c325ed04ccec4f3ae54aaa1/versions/station-relay-readiness-r55-20261006/README.md).
3. `build_candidate.py` usa as dependências originais pelos hashes exatos e um workspace novo; `package_candidate.py` aceita somente o APK R55 integral e substitui apenasDEX35. Chave original/senhas permanecem privadas no PC. Conferir certificado original, alinhamento16KiB e todas as demais entradas do pacote; emitir reciboSHA do candidato.
4. Atualizar os dois aparelhos preservando dados/assinatura, sem desinstalar/limpar; conferir SHA instalado por aparelho e canalParcel.
5. Testar sala nova/Battletoads/ambosPronto/Iniciar, confirmação e launch do convidado, bytes reais e controle dos dois jogadores. Depois testar Voltar/menu nativo/configurações/rede/segundo plano/retorno ao catálogo.
6. Publicar retorno com etapas e hashes, distinguindo instalação, controles/gameplay e saída. Nenhuma R56 foi declarada compilada por esta entrega.

Continuam dependentes de dois celulares: gameplay físico, saída online completa, versão do segundo aparelho e latência externa. Controles próprios no online e aquecimento medido também continuam pendentes; o delta não resolve esses itens por uma configuração de servidor. Não promover estabilidade geral com base nos testes isolados.
