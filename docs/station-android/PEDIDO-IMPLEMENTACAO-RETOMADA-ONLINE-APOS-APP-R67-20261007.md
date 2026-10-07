# APP → SERVIDOR — R67 e implementação da espera/retomada online

> **Destino: implementador do Servidor-pix.** Os caminhos `versions/station-collection-videos-r67-20261007/...` deste texto pertencem ao repositório **TurboElden**; não são pastas de produção Linux. [Entrega completa do app](https://github.com/luziellacerda/TurboElden/tree/589d678612880c10e40ef42d23e73451ac2397d9/versions/station-collection-videos-r67-20261007). Evidências copiadas nesta branch: `docs/station-android/entrega-app-r67-20261007/`. Esta publicação solicita implementação; não executa deploy nem declara recuperação concluída.

Data de preparação: 07/10/2026. Destinatário: implementador e operador do **Servidor-pix / Station Android**. Remetente: implementação **TurboStations Android**, repositório TurboElden.

**Entrega R67 instalada e conferida no Samsung A56 em 07/10/2026. APP → SERVIDOR: pedido de implementação, não resposta do servidor.** Publicado após a instalação conforme solicitado. A recuperação de partidas ainda não está implementada nesta versão.

## 1. Identidade da entrega efetivamente instalada

| Evidência | Resultado |
|---|---|
| Branch do app | `fix/station-r67-media-security-20261007` |
| Fonte exata | `0d7a44f371e846a9821426a9082405836e250b0e` — [fonte R67](https://github.com/luziellacerda/TurboElden/tree/0d7a44f371e846a9821426a9082405836e250b0e/versions/station-collection-videos-r67-20261007). Recibo de publicação em `docs/server/RECIBO-FONTE-R67-20261007.json`. |
| APK | `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R67-20261007.apk` |
| Bytes / SHA-256 | 2110287552 / `d746cc02b602162b19509cd44ad7cf751e320de3b52199e7d48e86e9e897228f` |
| `classes28.dex` | `4e912015b3daac63cf48f4621ee0022448e917927a41990e9bd22e96feecb810` |
| `classes35.dex` | `b3a6e8c3fe7ba30665a2c7058216f51c1ec7e39025e30de502c96466f89904bb` |
| `libturbo_carousel.so` | `ea4d4ee4b519b646dc367f66bbbc410be1365d4344458cd6cc8a7a859da90712` |
| Certificado | `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`; alinhamento 16 KiB conferido |
| Android | Samsung SM-A566E; instalação concluída `2026-10-07T18:35:51.173063+00:00`; SHA integral coincide |
| Preservação | Mesmo UID e data original; atualização `-r`, sem desinstalar/limpar dados ou mudar ajustes do aparelho. Não houve auditoria individual de todos os saves. |
| Abertura | ESActivity nas plataformas, com acesso salvo; sessão, perfil e catálogo HTTP200, 2.212 itens; zero FATAL EXCEPTION no processo observado |
| Segurança | Fonte de prova integrada; negociação/atestação exata ainda não capturada no Android. HTTP200 não demonstra sozinho o modo da prova. |
| Renderização | Registro nativo `MENU power target=30 fps; context=platforms`. Economia térmica ainda não medida. |
| Limites | Sem partida real em dupla/recuperação, gameplay CD ou inspeção visual individual das nove coleções nesta instalação. |

Recibos no snapshot: `evidence/package.json`, `evidence/java-dex-build.json`, `evidence/native-media-build.json`, `evidence/installation-samsung.json`. Os 13.210 arquivos preservados foram comparados byte a byte; somente nove MP4, dois DEX e o carrossel nativo mudaram. Três MP4 são novas entradas; 55 vídeos finais conferidos em 720×720/30fps/sem áudio.

O processo novo emitiu também `REQUEST_FAILED status=200` e `CATALOG_PUBLISHED status=503` antes de perfil/catálogo de rede200. São eventos do cliente; não inferir resposta HTTP503 do servidor ou causa sem correlação. O catálogo e o acesso abriram; registrar no diagnóstico de inicialização caso se repita.

O outro aparelho permanece sem recibo R67; não generalizar esta instalação. O runtime online não mudou. Nenhum serviço Linux foi implantado por esta entrega.

### Base preservada e comprovada antes desta entrega

- Pacote: `org.turboramastation.frontend`; classes principais continuam em `org.emulationstation.frontend`.
- Última base Android recebida pelo servidor: fonte R66 `291f3949760c0d77030e4872520efc3e1c8f928b`, documentação `7d0d3da5c979b8ed3c7fb235cd6eddb0dcba2e65`.
- APK **base R66**, não hash da nova R67: `e4397fd743c410ea060f17446706d9475b5568caad379e1a6df37672c60398d1`, 2.093.413.965 bytes.
- Certificado original esperado: `7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825`.
- `classes30.dex` R66/BIOS automática a preservar: `1dcfa1e69686a326e22c54b9adc7c6013644e69d14642664e2ab7b5e2eee4ff4`.
- Runtime online anterior: `899e35279cf046242a7ae31f502078080bd6a94404770fe99e1e2e90be58a1ef`. Não reutilizar essa identidade para um runtime modificado.
- R66 instalada e hash conferido no Samsung A56; catálogo observado. Último recibo anterior do Motorola: R63. Isso é histórico de base, não prova de instalação R67 ou execução CD.

## 2. Retornos efetivamente lidos

1. Servidor-pix, branch `docs/station-retornos-r66-20261007`, commit **`b37c873b70bf361388b3a18fd58eed3b5452565a`**: [RETORNO-ANALISE-HANDOFFS-R62-R66-STATION-20261007.md](https://github.com/luziellacerda/Servidor-pix/blob/b37c873b70bf361388b3a18fd58eed3b5452565a/docs/station-android/RETORNO-ANALISE-HANDOFFS-R62-R66-STATION-20261007.md).
2. Retorno de segurança: **`112d6b03d6ce00ccb340ed400806791f0fe7263d`**, [RETORNO-SEGURANCA-STATION-20261007.md](https://github.com/luziellacerda/Servidor-pix/blob/112d6b03d6ce00ccb340ed400806791f0fe7263d/docs/station-android/RETORNO-SEGURANCA-STATION-20261007.md). Fonte de publicação declarada: **`da073551428c4b1320a3abebc257b3c3c933b3ca`**.
3. Pedido original de retomada, ainda sem implementação entregue: **`553a26c8045cd2fd324ec871ef79950faa316728`**, [PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md](https://github.com/luziellacerda/Servidor-pix/blob/553a26c8045cd2fd324ec871ef79950faa316728/docs/station-android/PEDIDO-QUEDA-RETOMADA-PARTIDA-STATION-20261007.md).

Na consulta remota desta preparação, `docs/station-retornos-r66-20261007` e `feat/station-artifact-descriptor-20261002` apontavam para b37c873. Os dois ramos de segurança apontavam para 112d6b03. O inventário completo retornou 75 branches, sem nova branch de recuperação. A primeira leitura usou o conector GitHub após recusa local; o fetch explícito foi posteriormente concluído com a autorização adequada e confirmou b37c873 como último retorno, preservando o checkout do servidor.

O retorno b37c873 é **análise/documentação**, não novo protocolo de recuperação. A segurança já declarada em produção não significa que espera/retomada tenham sido implementadas. Este pedido solicita agora **código, contrato, testes e entrega operacional da recuperação**, além da análise já recebida.

## 3. Escopo do app R67 instalado

### 3.1 Segurança conciliada com a composição atual

A origem do delta de segurança é TurboElden `250a3e51a2876175155d148235af1fc402b84d08`, implementação funcional `213cfce61f5b537e9056ee598e9bd436b32f5ecd`, pasta `versions/station-security-r57-20261007`. A receita histórica exigia a R57 e **não deve ser aplicada sobre a R66 por mera troca de hash**.

A R67 concilia a composição R55 → R57 → R62 → R63 → R64 e preserva o DEX30 R66. `evidence/security-merge-provenance.json` relaciona os cinco arquivos do cliente e os quatro arquivos online conciliados:

- Cliente: `StationAndroid`, `StationApi`, `StationCrypto`, `StationHttp`, `StationRequestProof`.
- Online: `StationOnlineClient`, `StationRetroLaunch`, `StationRetroActivity`, `StationRelayTunnel`.
- Preservar também `StationGameSession` R64, controles próprios, diagnóstico, Binder, saída idempotente, `StationFlowPanel`, `StationRoomArtwork` e `StationRoomCreation`.

A prova por pedido deve continuar incluindo os componentes definidos no código efetivo e seus vetores, com nonce e chave do aparelho. Não inventar nomes de campos a partir de texto histórico: na implementação conferida, os campos são `keyAttestationChallenge` e `keySignature`. DEX28 e DEX35 devem usar o mesmo `station-client.jar` correspondente à nova fonte.

Não apagar/recriar alias Keystore nem vínculo/licença para adaptar a segurança. `RequireVerifiedApp=false` no último retorno preservava clientes antigos; não exigir atestação global antes da atualização e qualificação dos aparelhos. Vínculo que aderiu à prova pode recusar APK antigo: registrar essa restrição no plano de retorno, sem contornar a proteção.

Esta fonte entrou nos DEX28/35 e no APK conferido na seção 1. A abertura e os HTTP200 foram observados; o modo concreto de prova/atestação ainda precisa ser correlacionado no servidor.

### 3.2 Menu e processamento

O limitador preparado atua no desenho nativo `GuiStore`: teto de **30 fps**, inclusive na navegação; diálogo parado conserva **15 fps**. Não é limite de frames dos jogos, nem redução de velocidade da emulação. Não afirmar que todo componente Android passou a 30 fps apenas por esse hook.

Manter vídeo somente na célula em evidência, prévias estáticas dos vizinhos e suspensão quando o menu estiver oculto, coberto ou quando entrar no emulador. Não congelar clipes após 2,5 segundos, não criar célula preta e não alterar o relógio das animações para fazê-las andar pela metade. A renderização das salas Android tem ciclo de vida próprio e deve ser conferida separadamente.

Reduzir quadros desenhados não comprova redução de 50% de bateria/temperatura. Medição de consumo deve distinguir carrossel, configurações, tela inicial Android, emulador e duas telas ocultas. Não atribuir a queda da partida ao limitador de menu sem evidência.

### 3.3 Vídeos das coleções

Origem fornecida pelo mantenedor: `G:\TURBORAMA\RetroBat\emulationstation\.emulationstation\themes\TURBORAMAx\_theme_inc\images\caratulas\Sele;'ao`.

`mapping.json` identifica nove vídeos: Art of Fighting, Bomberman, Donkey Kong, Fatal Fury, Metal Slug, Samurai Shodown (cópia nova das 15h12), KOF Hacks, KOF e Top Gear (novo arquivo recebido). Os nove foram convertidos e decodificados integralmente; o pacote final contém 55 vídeos. As rotas foram cruzadas com o catálogo publicado, incluindo `## TOP GEAR ##`.

Formato solicitado: H.264, 720 × 720, 30 fps, sem áudio, loop na velocidade original, sem cortes ou deformação. A origem pode conter JPEG anexado; selecionar o stream de vídeo em movimento. MP4 deve permanecer ZIP_STORED e alinhado para `AssetManager.openFd`. Os quadros de prévia devem corresponder à nova mídia. A atualização não cria jogos, altera catálogo ou exige novas chamadas no servidor.

Conferência anterior do APK R66: 52 vídeos com esses metadados. Isso não equivale à validação dos novos arquivos. Vincular ao recibo final conversão, decodificação, hashes e inventário do APK realmente montado. Não publicar mídias privadas no Git.

## 4. Queda registrada: o que foi comprovado

O retorno do servidor leu 648 registros entre 12h20 e 12h26 UTC de 07/10/2026 e relacionou:

| Horário UTC | Evidência recebida |
|---|---|
| 12:21:45.330 | Primeiro GET do relay iniciado |
| 12:21:46.341 | Segundo GET do relay iniciado |
| 12:23:46.378 | Renovação de sessão HTTP200; duração 12,7359 ms |
| 12:24:07.346–.347 | Aplicação abortou ambas as conexões; duração aproximada 142 e 141 segundos |
| 12:24:07.422 | Aplicativo registrou `relay-failed reason=RELAY` |

Na janela, 34 comandos online e três renovações terminaram em HTTP200. Sem corpo registrado, não se sabe se todos eram heartbeat, nem a última presença de cada participante. Ausência de 401/403/500 nesse recorte não prova ausência de todo problema de sessão.

O **gatilho inicial permanece desconhecido**: faltam lado, primeiro close/exception, causa da invalidação e última presença individual. O abort conjunto é consequência comprovada; não identifica sozinho a primeira falha. Os 142 segundos não comprovam timeout Nginx de 120 segundos. A queda precedeu a publicação de segurança; não atribuí-la à nova proteção. Os 55 avisos JNI `StackOverflowError` sem stacktrace precisam de investigação própria, sem tratá-los como causa provada.

## 5. Mapa de encerramento conferido no código

As linhas do servidor abaixo se referem ao commit b37c873. As linhas Java se referem à fonte R67 preparada nesta pasta; revalidar após mudanças. São limitações e caminhos reais, não prova do primeiro gatilho daquele evento.

| Componente | Ponto | Comportamento atual |
|---|---|---|
| `src/TurboRamaSuiteOnlineServer/StationRelay.cs` | 43 | Espera inicial pelo par limitada a 60 segundos |
| Mesmo arquivo | 58–68 | Qualquer fim/falha entra no `finally`, cancela `pair.Stop`, aborta os dois sockets e chama `CloseRelay` |
| Mesmo arquivo | 58 | Exceções previstas são absorvidas sem diagnóstico individual da primeira causa |
| Mesmo arquivo | 77–80 | Watch consulta validade da lease a cada 2 segundos |
| `src/TurboRamaSuiteOnlineServer/StationOnline.cs` | 125–133, 160–162 | Sweep remove presença sem renovação há 60 segundos; remover chama Leave |
| Mesmo arquivo | 301–307, 390–403 | Ticket individual de uso único; `usedRelayPeers` impede reanexação |
| Mesmo arquivo | 406–408 | Lease depende de presença/geração/sala; `CloseRelay` chama Leave |
| R67 `StationRelayTunnel.java` | 42–46 | Callback WSS escreve de forma bloqueante no TCP do motor; consumidor parado pode atrasar processamento de pong |
| Mesmo arquivo | 51 | Watchdog de conexão configurado em 20 segundos; não significa fechamento exato aos 20 segundos |
| Mesmo arquivo | 48–49, 84–90 | Falha/close termina o túnel local e remoto, sem protocolo de recuperação |
| R67 `StationRetroActivity.java` | 51–53 | `onStop` fecha relay; ao voltar informa partida encerrada |
| `StationGameSession.java` herdado da R64 | 26–33 | Heartbeat com fixed delay de 20 segundos; tempo da requisição soma ao intervalo; evento2 suspende heartbeat |
| Mesmo arquivo | 39–42 | Registra duração/sameRoom e falha de sessão, sem credenciais |
| Runtime identificado no pedido Q01–Q08 | `netplay_private.h` / `netplay_sync_pre_frame` | Limites de stall de servidor/cliente ainda exigem conciliação com pausa real; mudar um número não cria retomada |

A sala e os pares do servidor estão em memória. TCP/WSS atuais não têm offset/replay persistido. Um novo socket não autoriza reenviar bytes às cegas. Não basta suprimir a mensagem do cliente, retirar todos os timers ou trocar `Leave` por um estado inventado.

## 6. Implementação solicitada ao servidor e contrato para o Android

### REC-01 — primeiro evento e causalidade

Instrumentar o primeiro motivo de término, uma única vez por geração/lado, e as consequências em separado. Registrar UTC, correlação pseudônima, geração, função, close code, categoria de exceção, idade do último heartbeat de cada lado, latência e contadores por direção. Diferenciar cancelamento do request, invalidação de lease, socket fechado, parser, backpressure, revogação e saída voluntária.

Não registrar tokens, tickets, códigos de ativação, mensagens privadas, caminho do cliente ou identidade pessoal. A correlação deve atravessar API/relay/app e permitir distinguir quem encerrou primeiro. Capturar stacktrace nativo sanitizado para os avisos JNI em fluxo separado.

### REC-02 — sessão lógica separada do transporte

Conservar sala, membros, geração e estado recuperável quando cair o socket. Liberar os recursos físicos de rede sem converter perda em `Leave`. Separar presença social da participação em partida em espera. Definir explicitamente comportamento com host ausente, convidado ausente e ambos ausentes.

Não expulsar por simples contagem regressiva de rede. Definir recursos limitados de memória/armazenamento e backpressure para não manter filas crescentes ou CPU em laço. Se houver limite operacional que impeça espera ilimitada, declarar esse limite e devolver erro honesto; não escondê-lo como saída humana nem apagar estado silenciosamente.

### REC-03 — protocolo autenticado e versionado de retomada

Entregar contrato executável: OpenAPI, esquemas, estados/transições, erros, exemplos e vetores de assinatura. Os nomes de campos/rotas devem vir da implementação publicada; este documento não presume que já existam rotas v2.

Negociar capacidade de recuperação mantendo v1 para clientes antigos. Revalidar dispositivo, licença, sala, geração, papel e motor/conteúdo. Emitir nova credencial individual autenticada de uso único; impedir reutilização e conexão concorrente indevida sem bloquear retomada legítima. Explicitar rotação/consumo das credenciais e tratamento de replay de requisição. Conservar prova por pedido, TLS e pinning.

### REC-04 — integridade dos bytes e sincronismo

Escolher e implementar uma estratégia documentada:

1. Continuidade de transporte com sequência/offset, confirmação, replay limitado, deduplicação e backpressure; ou
2. Reinício sincronizado do netplay com estado/hash aceitos pelos dois participantes.

Em ambos os casos, impedir perda/duplicação de inputs e confirmar estado antes de declarar retomada. Não fazer reenvio cego, não apenas reabrir WSS e não forçar o convidado para `connecting` sem prontidão real do host.

### REC-05 — pausa real e runtime

Pausar efetivamente ambos os motores durante a espera, conservar estado necessário e retomar de forma coordenada. Identificar precisamente os hooks/comandos nativos e sua confirmação; não simular pausa apenas cobrindo a tela.

Se alterar runtime, entregar patch, fonte base, receita reproduzível, novos SHA-256/engineIds, negociação de compatibilidade e registro aditivo. O runtime anterior não pode identificar o novo binário. Preservar controles, configurações, saída e saves. Explicar efeitos nos motores SNES, Mega e demais motores online suportados; não assumir determinismo/compatibilidade de todos a partir de um teste.

### REC-06 — ciclo de vida Android e consumo

Definir contrato para bloqueio de tela, perda de foco, troca temporária de app e volta. Hoje `StationRetroActivity.onStop` fecha o relay e `StationGameSession` suspende heartbeat. A implementação nova precisa conservar sessão recuperável sem `Leave` silencioso.

Não manter renderização/vídeos/emulação desnecessária em tela escondida nem iniciar outra cópia do motor. Explicitar quais tarefas de presença/recuperação continuam, por quanto tempo, e sua compatibilidade com o ciclo de vida Android. A interrupção/eliminação do processo pelo sistema não equivale a uma pausa com memória preservada; recuperação persistida deve ser tratada separadamente.

### REC-07 — autenticação, disponibilidade e proxy

Renovar sessão durante a espera quando houver rede. Diferenciar indisponibilidade temporária de credencial expirada, licença revogada e saída humana. Preservar validações de assinatura, nonce, ticket, geração, ROM, runtime e opções.

Conferir serviço, DLL, upstream e Nginx realmente carregados. O timeout de IO não é duração máxima da partida. Medir ping/pong e tráfego de manutenção para esclarecer o primeiro fechamento. Não usar HTTP200 em `/health` público como prova da API: b37c873 documenta que essa rota retorna HTML do portal; usar prontidão local e rotas reais Station autenticadas.

### REC-08 — interface de espera e saída

Estado visível: **“Aguardando conexão…”**, indicador indeterminado de baixo custo e ação **“Sair da partida”** com confirmação humana. Sem porcentagem fictícia, prazo prometido de reconexão ou contagem regressiva de expulsão.

Estados funcionais exigidos, ainda não nomes de campos publicados: jogando → esperando transporte → sincronizando → jogando. Ter internet novamente não basta: ambos os participantes e estado da partida precisam estar confirmados. Não retornar às salas automaticamente por simples perda de rede. Revogação ou estado irrecuperável exige explicação honesta, preservação local quando possível e saída humana; não continuar chamando isso de espera recuperável.

### REC-09 — implantação e compatibilidade

Entregar separadamente fonte candidata, teste isolado e publicação real. Preservar clientes v1 e serviços compartilhados. Migrations, se necessárias, devem ser aditivas e documentadas a partir do ledger efetivo. Não inferir autorização para modificar PIX, Suite Windows, EmulationStation Windows, WhatsApp, licenças reais ou outros serviços.

O operador do servidor realiza a publicação autorizada e devolve recibo com serviço/DLL/configuração/contrato ativos. O implementador Android só habilita o protocolo quando houver contrato e compatibilidade comprovados. Não ativar recurso parcialmente com endpoints inexistentes ou respostas vazias.

## 7. Matriz de aceite obrigatória

| Cenário | Verificação exigida |
|---|---|
| Atrasos de 1/3/6/10/20 segundos sem romper socket | Primeiro ponto de espera, pausa dos motores e retorno sem perda/duplicação |
| Perda somente do host | Sala lógica preservada, convidado esperando e credencial de retomada válida |
| Perda somente do convidado | Host não encerra automaticamente; retorno do mesmo participante autenticado |
| Perda dos dois | Estado preservado conforme contrato e reconciliação quando ambos retornarem |
| Modo avião / Wi-Fi para dados / dados para Wi-Fi | Nova conexão real, sem depender do socket antigo ou endereço anterior |
| Perdas maiores que 60 e 120 segundos | Nenhum `Leave` implícito pela presença ou timeout antigo; consumo limitado |
| Heartbeat lento/concorrência/renovação | Sem expulsão indevida; diagnóstico identifica duração, sessão e geração |
| Expiração e revogação | Expiração recuperável tratada; revogação real continua impedindo uso |
| Bloqueio/background/retorno | Sem encerramento oculto; sem duplicar motor nem renderização escondida |
| Saída humana durante espera | Encerra uma única vez, informa o outro lado e libera recursos |
| Reinício do relay/processo Android | Resultado documentado: recuperação persistida comprovada ou limite explícito |
| Bytes repetidos/fora de ordem/ack perdido | Deduplicação e integridade verificadas; nada reenviado às cegas |
| Dois clientes com versões distintas | Negociação e recusa/compatibilidade documentadas; v1 preservado |

Para cada linha devolver: contexto, versões/hashes, primeiro evento, estado da sala, ponto de pausa, sequência/bytes ou hash do estado, confirmação de sincronismo, memória/CPU durante espera e resultado final. Teste sintético de TCP não substitui gameplay real em dois Android. As provas físicas devem incluir comandos dos dois jogadores, som/imagem, saída voluntária e retorno ao menu com login preservado.

Critério de fechamento: não declarar a queda específica corrigida sem nova evidência correlacionada. A retomada deve ser demonstrada mesmo quando o primeiro gatilho da ocorrência antiga permanecer impossível de reconstruir; deixar as duas conclusões separadas.

## 8. Neo Geo permanece uma frente separada

O inventário de 189 ZIPs no retorno b37c873 leu tabelas centrais dos originais, não verificou bytes de cada chip nem igualdade com o artefato ativo/instalado. NG-01 a NG-07 ainda precisam de export autenticado do índice efetivo, hashes/CRC reais, compatibilidade por driver e prova de execução.

- Não renomear os 13 nomes não registrados por tentativa.
- `kof98.zip` original contém chips e BIOS, mas causa gráfica não foi comprovada; comparar pacote servido/instalado, seleção de BIOS e motor.
- R66 já prepara automaticamente BIOS CD que existia no APK. Preservar `MameEntryActivity`/`NeoCdSupport`; não pedir outro firmware como solução dessa omissão já corrigida.
- Conferir os 50 CHDs ativos, parentes/dependências/launchPath e execução Android.
- Corrigir classificação de outros hardwares somente com plano que preserve IDs, capas, instalações e saves.

Vídeos de coleções não alteram formato de ROM, conteúdo do ZIP/CHD nem protocolo online. Não apresentar atualização visual ou segurança como correção de cartuchos/CD.

## 9. Retorno esperado e publicação deste pedido

Arquivo sugerido ao servidor: `docs/station-android/RETORNO-IMPLEMENTACAO-RETOMADA-ONLINE-STATION-20261007.md`. Responder REC-01 a REC-09 e a matriz, vinculando Q01–Q08 anteriores.

Informar obrigatoriamente:

- Branch e commit completos dos códigos servidor/Android/runtime, arquivos e funções alterados.
- Fonte Android exata lida, correspondente à seção 1 preenchida; conciliar sucessoras em vez de restaurar R57/R63.
- Contrato, exemplos, vetores, erros, limites e negociação efetivamente implementados.
- Receitas reproduzíveis, dependências, manifestos/hashes e resultados de testes.
- Estado separado de candidato, homologação isolada, produção e aparelhos.
- Serviço/DLL/proxy/configurações efetivos, horário e critérios de saúde reais.
- Plano de implantação/retorno compatível com vínculos que aderiram à segurança; sem rebaixamento silencioso da prova.
- Dependências ainda abertas, responsável por cada lado e evidência necessária para fechar.

Não publicar APKs, ROMs, BIOS, mídias privadas, chaves, códigos de ativação, tokens, tickets, saves ou logs pessoais. Esta entrega documental será publicada **depois da instalação solicitada e do preenchimento dos recibos**. Até lá, permanece preparação local; nenhum resultado de produção ou gameplay é presumido.
