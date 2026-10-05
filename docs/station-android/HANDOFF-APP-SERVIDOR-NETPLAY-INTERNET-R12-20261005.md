# APP → SERVIDOR — Netplay pela internet no mesmo Station — R12, 05/10/2026

Destinatário: operador do **Servidor-pix**. Este é código e pedido de publicação do APP, não retorno do servidor nem prova de produção. Pedido vigente do mantenedor: jogadores em redes distintas, usando o mesmo servidor que já atende a Station. Substitui a restrição anterior de conexão direta. Não usar lobby, relay ou conta públicos do RetroArch.

## Estado e autoridade

- App parte de R11 / TurboElden `6ef86c4cfd8302c0673c784856f25cf673d261ca`; preserva coleções, nomes, sinopses, catálogo, capas e emuladores locais.
- Servidor parte exatamente de `4e623bcbeaed3ee03d8a1767f66319e67659463d`, branch `feat/station-online-direct-20261004`. Preserva `StationLibraryMonitor` e consulta ao catálogo vigente; não substituir por fonte anterior sem monitor.
- APK novo: `E:\ESTUDO APK\work\station-netplay-20261004\TurboStations-Netplay-Internet-R12-20261005.apk`. Hash, tamanho, certificado e comparação integral em `evidence/build-result.json` no app.
- Fonte/build canônicos: `E:\ESTUDO APK\work\station-netplay-20261004\internet-r12`. App Java também promovido para `netplay\src`, com backup anterior em `internet-r12\before-netplay-src`. Novas dependências estão em `internet-r12\dependency-src`.
- Servidor completo compilado isoladamente em `internet-r12\server-full`. Nenhuma alteração foi implantada em Linux. Checkout do clone do servidor preservado; a nova branch contém quatro arquivos de produção e documentação/testes aditivos.
- R9 é o último instalado comprovado. O aparelho voltou à USB nas plataformas (84% de bateria, ~12 GiB livres), mas desconectou durante a instalação R12; ADB devolveu falha e a conferência seguinte não encontrou dispositivo. Estado do pacote após a tentativa e hash no aparelho ainda precisam ser conferidos. Não foi alterada a configuração de tela. R10/R11/R12 não foram validados em Android nesta rodada. R11 foi arquivado em G: com SHA256 integral antes de remover a cópia idêntica de E:. Tag estável preservada.

## Desenho implementado

Dois celulares iniciam conexões **WSS na porta HTTPS do mesmo domínio Station**, usando o mesmo certificado/pin do app. O servidor associa somente dois integrantes da mesma sala. O fluxo TCP do RetroArch passa por essas conexões em ordem. Não há IP público do jogador, encaminhamento de porta no roteador, descoberta LAN obrigatória, UPnP ou retransmissor público.

Isso permite atravessar redes com NAT/CGNAT quando ambas permitem saída WSS para o domínio Station. Não promete acesso em rede que bloqueia esse domínio/WebSocket, nem ausência de latência, compatibilidade de todos os motores, ou continuação transparente ao trocar Wi-Fi por dados móveis. Troca/perda de conexão encerra a partida; criar uma nova sala para reconectar. Reinício do servidor também encerra salas, pois o estado continua em memória.

## Contrato exato e sequência

As rotas existentes `/v1/station/online/command` e `/v1/station/online/events` continuam autenticadas por Bearer, assinadas RSA-PSS, vinculadas à sessão/aparelho/licença/requestId. As verificações existentes de núcleo/runtime/conteúdo/opções permanecem obrigatórias.

1. `snapshot.transports` acrescenta `relay-wss-v1` somente quando a configuração permite. Flag ausente/desligada publica apenas `direct`. App novo informa atualização pendente quando não há relay; não pede IP nem anuncia sucesso fictício.
2. Criar/entrar/confirmar presença seguem os comandos atuais. Ambos precisam da mesma ROM instalada, núcleo, runtime e opções aceitos pelo registro.
3. Host envia `start` com `roomId` e `transport: "relay-wss-v1"`. Endereço/porta do telefone não são solicitados. O servidor valida dois integrantes prontos, passa a `starting` e incrementa generation.
4. Host envia `relay-ticket` com `roomId`. A resposta assinada contém `snapshot.room.relay = {path:"/v1/station/online/relay",protocol:"station-relay.v1",ticket:...,expiresInSeconds:...}`. Token aleatório de 32 bytes, base64url sem padding (43 caracteres), 60 s e uso único, reservado ao próprio integrante. Não aparece para o colega ou visitante. Refazer o comando com novo requestId antes do uso substitui o ticket anterior.
5. Host prepara o runtime nativo. Seu bridge inicia WSS e aguarda callback real `onNetplayListening`. Depois conecta TCP local `127.0.0.1:55435`. O sinal `host-listening` segue via Binder para o processo principal autenticado.
6. Servidor passa a `connecting`, **sem incrementar generation no modo relay**, preservando o ticket do host. Convidado solicita seu próprio ticket e abre runtime. O bridge convidado aceita conexão nativa em porta local efêmera e encaminha os bytes por WSS.
7. Rota nova `GET /v1/station/online/relay`, upgrade WebSocket, subprotocolo `station-relay.v1`, header `Authorization: StationRelay <ticket>`. Sem query string. Não recebe URL, host ou porta de destino do usuário. Consome ticket antes de aceitar e associa sala, geração, participante e papel. Só aceita mensagens binárias.
8. A partida usa o protocolo TCP original RetroArch, senha aleatória original da sala e validações do motor. Servidor não interpreta comandos da ROM nem executa emulação. `connecting` continua significando conexão em preparação; não é prova de sincronismo/gameplay.
9. `StationGameSession` conserva heartbeat autenticado a cada 20 s enquanto o jogo está visível, usando a sessão do processo principal. `onStop` fecha o túnel; `onDestroy` encerra a participação. Falha/saída de um lado aborta os dois sockets e remove a sala. Não existe conexão silenciosa infinita no fundo.

## Arquivos e funções

| Arquivo | Responsabilidade |
|---|---|
| StationRoomsActivity.java | Descobre transportes, confirma jogo pela internet, start e solicitação do ticket; mantém visual/salas/chat existentes |
| StationRetroLaunch.java | Valida descritor assinado, prepara arquivo privado de lançamento de uso único, overlays e configuração nativa |
| StationRetroActivity.java | Cria/fecha bridge; repassa porta local ao runtime antes de iniciar; callback real do host e avisos de conexão |
| StationRelayTunnel.java | WebSocket upstream, TCP local, preservação da ordem, backpressure, fechamento e limites; sem APIs Android na implementação do fluxo |
| StationRelayTls.java | Mesmo BASE_URL HTTPS → WSS; trust store do Android, hostname e SPKI SHA256 existentes; sem trust-all |
| StationOnlineClient.java | Mensagens dos novos erros do servidor |
| StationOnline.cs | Flag, transportes, tickets, vínculo à sala/geração, consumo único, heartbeat e revogação |
| StationRelay.cs | Pareamento exclusivamente de dois membros; cópia binária com buffers limitados e encerramento conjunto |
| StationOnlineEndpoints.cs | Upgrade autenticado por ticket, subprotocolo, rejeições, duração do request e liberação da sala |
| StationOnlineRegistration.cs | DI/flag; preserva catálogo atualizado pelo monitor e autenticação existentes |

Não modificar manifesto, assinatura, base da API, pin, chave de assinatura de respostas, IDs de jogos, banco ou catálogo para ativar relay. Não trocar os motores locais SNES/Mega. O registro online existente deve continuar com os hashes exatos atuais: nenhum núcleo/runtime nativo mudou neste APK.

## Limites e segurança operacionais

- Até 128 pares simultâneos nesta implementação, dois sockets por sala; capacidade real depende de CPU/rede/proxy e precisa de teste de carga do operador.
- Buffer servidor de 32 KiB por direção, escrita aguardada; sem fila ilimitada. Limite de 8 MiB/s por direção por sala com backpressure, sem descartar bytes nem derrubar a conexão por uma rajada de estado inicial.
- Fila cliente limitada por backpressure em torno de 256 KiB mais um bloco; leitura local de 16 KiB; WebSocket com limite de mensagem 64 KiB e payload esperado até 32 KiB.
- Pareamento aguarda no máximo 60 s. `starting` relay no hub expira em 90 s; conexão ao upstream/endpoint local tem limites próprios. Ping cliente 20 s e keepalive servidor 20 s.
- Watch de vínculo à sala a cada 2 s. Revogação explícita no hub interrompe no próximo watch. Revogação na base impede novos comandos/heartbeats válidos; presença expira em até 60 s desde o último heartbeat válido, mais o watch. Não alegar bloqueio instantâneo de um socket já aberto por alteração isolada no banco.
- Token não vai na URL nem nos logs. Configurar proxy para não registrar Authorization ou corpo da resposta assinada. Não publicar arquivos de fixture com tokens, certificados privados, sessões ou códigos reais.
- Continua uma única instância autoritativa das salas. Múltiplos workers/réplicas sem afinidade/estado compartilhado não são suportados; não ativar escalonamento horizontal aleatório.
- Transporte WSS tem TLS até o servidor. Não afirmar criptografia ponta a ponta entre os telefones; o relay vê os bytes transitórios, não os grava.

## Publicação pelo operador, no serviço já existente

1. Comparar a branch com o serviço realmente ativo e conciliar quatro arquivos com quaisquer mudanças posteriores a 4e623bc. Não trocar todo o servidor por este snapshot antigo. Confirmar chamadas existentes de AddStationOnline/MapStationOnline no Program e preservar monitor/biblioteca/serviços compartilhados.
2. Compilar/testar em ambiente isolado .NET 8. Não há migration, banco novo, credencial pública ou serviço adicional requerido. Manter registro de motores e configurações atuais.
3. A configuração nova é `Station:Online:RelayEnabled` (variável de ambiente `Station__Online__RelayEnabled`). **Padrão false**. `Station:Online:Enabled` também precisa estar true. Publicar primeiro desativado e validar regressões do serviço.
4. No mesmo domínio HTTPS do app e upstream atualmente usado pelas rotas Station, permitir upgrade WebSocket **somente** em `/v1/station/online/relay`; preservar Authorization e Sec-WebSocket-Protocol, HTTP/1.1 upstream, Upgrade/Connection adequados; não cachear/bufferizar. Timeout inativo deve comportar keepalive de 20 s, por exemplo acima de 60 s. Não presumir caminho de arquivo nginx, nome de container/unidade ou endereço upstream: usar os efetivamente ativos documentados pelo operador.
5. Se existir Cloudflare na rota, habilitar WebSockets e conferir regras WAF sem retirar proteção das outras rotas. Não mudar DNS, pin/certificado ou expor uma porta do emulador na internet. O app usa o mesmo host/porta configurado no Station.
6. Habilitar RelayEnabled e reiniciar controladamente **o serviço correto** segundo o procedimento operacional vigente. Flag é lida também na construção do hub: trocar somente o valor em arquivo sem recriar processo não atualiza capacidades do hub.
7. Verificar via sessão de homologação: transports, resposta assinada relay-ticket, upgrade101, binário nos dois sentidos, rejeição de token expirado/reusado/integrante externo, saída, revogação e zero pares após encerrar. Não incluir segredos no retorno.
8. Instalar o APK por atualização preservando dados, licença, saves e assinatura. Testar dois celulares com o mesmo jogo/núcleo, um Wi-Fi e outro rede móvel; depois Wi-Fi de operadoras distintas. Confirmar interação dos dois jogadores, tempo/áudio/sincronismo, controles e retorno às salas/plataformas. Não basta ver sala/lista/101.
9. Rollback: desligar RelayEnabled e reiniciar o serviço (as partidas ativas encerram); app informa indisponibilidade, jogo local continua. Não remover catálogo, dados, licenças ou publicar a revisão como estável antes da homologação.

## Provas locais entregues

100 verificações: 39 regressões de salas/chat/convites; 37 HTTP com assinatura RSA-PSS real e autenticação sintética; 17 integração de rotas TLS/tickets/flags/limpeza/revogação; 7 bridge Java real contra relay C# real. Transferidos 12.583.029 bytes em cada sentido e 50 comandos pequenos ordenados, conferidos byte a byte. Teste de pin incorreto rejeitado. Grande rajada não encerra mais a sessão. Testes rodam em loopback com certificado confiado só pela fixture e identidades artificiais, sem banco ou usuários de produção.

Android Java API34/Java8 e DEX mínimo26 compilados; servidor completo Release net8.0 compilado. Comparação integral do APK em evidence. Isso não comprova GPU/consumo Android, partida determinística, latência externa, proxy vivo ou redes móveis.

Motores habilitados nesta linha: SNES bsnes-mercury Performance e Mega ClownMDEmu. Neo Geo permanece bloqueado por preparação .neo/BIOS pendente; CPS/MAME/FBNeo e outros arcades não se tornam compatíveis por existir relay. Manter licenças/fontes correspondentes já documentadas. Não prometer todos os sistemas.

## Dependências e reprodução

Java-WebSocket 1.6.0 e SLF4J API 2.0.13, licenças MIT incluídas no APK. Fontes upstream e hashes estão em dependencies/sources.json. Os 124 fontes foram realocados para `org.emulationstation.frontend.relay.ws` e `.relay.log` para evitar colisões com os emuladores integrados. Sem outra mudança algorítmica upstream. Fontes realocados completos acompanham esta entrega, dispensando busca incerta de versões.

No workspace canônico E:, `build_station_internet_app.py` compila os 141 fontes usando station-client.jar produzido pelo R10, SDK34, JDK17 e D8. `run_station_relay_tests.py` compila servidor e testes isolados; requer as fixtures prévias server/Program.cs e HttpTests.cs, também copiadas na entrega. `package_station_internet_r12.py` usa R11 exato, altera somente classes35.dex, inclui três arquivos de licença/proveniência, alinha 16 KiB, assina com certificado original e compara todas as entradas. Não reaplicar scripts one-shot em artefato existente: revisar guardas e recibos; não apagar o APK estável.

Referências primárias: [protocolo RetroArch](https://docs.libretro.com/development/retroarch/netplay/), [conectividade Netplay](https://docs.libretro.com/guides/netplay-getting-started/), [ASP.NET WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-8.0), [Cloudflare WebSockets](https://developers.cloudflare.com/network/websockets/), [Java-WebSocket](https://github.com/TooTallNate/Java-WebSocket).

## Retorno solicitado ao servidor

Publicar **RETORNO-SERVIDOR-NETPLAY-INTERNET-STATION-R12-20261005.md** respondendo: commit/DLL efetivos e hashes; flags; instância/serviço/upstream/proxy efetivos (sem segredos); confirmação de contratos/catálogo preservados; resultados dos testes HTTPS/WSS; capacidade/limites/telemetria; decisão de publicação ou impedimento comprovado; prova de dois aparelhos/redes quando disponível. Distinguir teste local, homologação, produção e aparelho. Não chamar este próprio handoff de retorno nem preencher dados não medidos.

## Artefato exato desta entrega

APK SHA256 `7684c6eee87985d8259becca9a22a9f4c7e3203f7c097df37da6998975596514`, 1.982.967.774 bytes. Somente `classes35.dex` alterado, três arquivos de licença/proveniência adicionados e 11.102 entradas preservadas. Certificado original e alinhamento16KiB conferidos.

## Fontes imutáveis do aplicativo para esta publicação

App commit **823787e9938029d64ffb0f4b0c7e69834066936c**, branch `feat/station-capas-visuais-netplay-20261003`. [Handoff e mapas](https://github.com/luziellacerda/TurboElden/blob/823787e9938029d64ffb0f4b0c7e69834066936c/versions/station-internet-r12-20261005/README.md), [fontes Java](https://github.com/luziellacerda/TurboElden/blob/823787e9938029d64ffb0f4b0c7e69834066936c/versions/station-internet-r12-20261005/netplay-src), [dependências e licenças](https://github.com/luziellacerda/TurboElden/blob/823787e9938029d64ffb0f4b0c7e69834066936c/versions/station-internet-r12-20261005/dependencies), [recibo do APK](https://github.com/luziellacerda/TurboElden/blob/823787e9938029d64ffb0f4b0c7e69834066936c/versions/station-internet-r12-20261005/evidence/build-result.json), [testes](https://github.com/luziellacerda/TurboElden/blob/823787e9938029d64ffb0f4b0c7e69834066936c/versions/station-internet-r12-20261005/run_station_relay_tests.py). Esta branch adiciona quatro arquivos de produção ao servidor e documentação/testes. Não foi implantada. Estado atualizado de instalação no telefone deve ser lido no próximo recibo do app; não presumir instalação a partir do APK compilado.
