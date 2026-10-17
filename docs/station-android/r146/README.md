# R146 — visual preservado e conexão v3 direta

Fonte congelada `bfea454d04ef297e9dfd1b397e0bf25df3937e93`; APK privado SHA256 `04930201ad2f1c016f3385b3f08248ed4fa112d482a98761d00494b7a937468a`, 2,715,073,551 bytes. Certificado original v2/v3 e 16 KiB conferidos. Candidata ainda sem instalação/validação física: nenhum telefone estava conectado na última leitura. R143 continua estável; R145 possui backup completo verificado com 3.632 arquivos, APKs final/donor e dois bundles Git completos.

## Alteração limitada

`StationMultiplayerSession.Channel` usa agora a origem `wss://downloads-station.lzgames.com.br/v1/station/online/multiplayer/relay` e `StationRelayTls.createMultiplayer()`. Fábrica própria mantém a CA do sistema, identificação de hostname HTTPS do cliente real e SPKI `bcd134d59fcdbb35e0343c86d31a666cc95cf158d70b27aceceefd19158d072f`. API/login/comandos/catalogo/downloads, `endpoint()`/`create()` e relay legado continuam na origem/pin anteriores.

Ticket/prova continuam no mesmo caminho, protocolo `station-stream.v3`, janela 262.144, roles/slots/expiração, READY/ACK/barreiras, reconexão e saída. Apenas duas classes: um endpoint, uma invocação de fábrica e um método novo. Outros 606 tipos do DEX35, 39 DEX, todo carrossel/azul céu/faixas, cores reais, opções, controles, g52/HLE/multitap/downloads permanecem. Inversão integral real após a montagem passou. Cinco substituições, 13.855 entradas intactas, zero adições/remoções.

## Verificações

Vinte testes da fonte TLS e cinco guardas negativas passaram. Handshake real TLSv1.3 na origem direta verificou CA/hostname/pin; fábrica API antiga recusou o novo host por pin incorreto, como previsto. Sem HTTP/WSS/login/tickets nesse teste. Os 396 ms medem um handshake nesse ensaio; não representam RTT, FPS ou melhoria de partidas. Ainda faltam sessões Android autenticadas e inputs simultâneos.

## Cadastro e handoff do servidor

Kit exato R146 SHA256 `96d057769b4ac420fb01e6896ae3738a31a92efe19529d4592b72f71ee9817af`, 12,175,118 bytes: seis bibliotecas, vinte dependências, quatro IDs atuais e mesmos 78 modos revisados (66 N64, nove GameCube, três Wii). PS1 continua com zero novas aprovações HLE por edição neste owner. Preparado não significa aplicado. Último retorno: catálogo 47 de capas N64, registros online ainda R141. Capas usam corretamente `coverId + item.revision` no DEX28 real; nenhuma alteração de cache necessária.

Aplicar somente o kit do APK final efetivamente escolhido/instalado, usando preparador atual, `migrate_existing_native_profiles=False`, baseline protegido atual e `scan.lock` Linux. Preservar licenças/cadastros/perfis/modos/R141; não reaplicar QoS/R141, não executar `main` histórico ou promover HEAD inteiro. Nenhuma API reiniciada ou implantação feita por esta tarefa. A R147 com reparos Dolphin ainda em preparação terá outro kit; este não a substitui.

## Dois defeitos Dolphin comprovados em testes de código

O trecho nativo original retorna quando BootSessionData é nulo sem limpar o flag de inicialização, prendendo SurfaceDestroyed mesmo após Stop. Rascunho de uma linha limpa o flag antes do fechamento; teste extraído passou, incluindo 256 falhas repetidas. Separadamente, consume/release do objeto BootSessionData não protegiam a transferência: dois consumers ou cancelamento podiam receber/liberar o mesmo endereço. Rascunho em quatro métodos, monitor curto da classe sem JNI/callback/join dentro, passou em onze verificações. `isClosed` é volatile; closeBlocking mantém seu monitor próprio.

Essas correções NÃO estão na R146. A R147 exige reconstrução nativa completa reproduzindo primeiro o motor original f7ebe048, mais montagem DEX10 com 4.990 classes/59 métodos preservados. Ensaios extraídos não executaram ART/core/Android nem provam a ocorrência no relato específico do telefone. CPU/JIT/FPS/controles e jogo em andamento ficam fora dos reparos.

## Próximos testes necessários

Atualizar os participantes para o mesmo APK final, cadastrar o kit correspondente e obter capabilities assinadas atuais. Testar criação, entrada, boot, dois controles distintos, perda de sinal, cancelamento e retorno nas quatro plataformas. Para PS1, selecionar edição/modo e qualificar HLE com conteúdo e dois inputs reais antes de aprovar. Não declarar todos os jogos liberados por haver biblioteca no APK. APKs/chaves/ROM/BIOS/logs brutos/seriais/códigos de ativação continuam privados. Atualizações não desinstalam nem limpam dados.
