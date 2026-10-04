# APP → SERVIDOR: implementação nova de salas Station, 04/10/2026

**Destinatário: mantenedor/IA do Servidor-pix. Origem: equipe do aplicativo TurboStations Android. Este documento NÃO é retorno do servidor, NÃO confirma implantação e NÃO é nova migração de CDN.**

## 1. Autoridades e escopo

Base de código do servidor lida/exportada: **54bba11c52f35695fd47eabc7145f42af9990426**. A branch de entrega é **feat/station-online-direct-20261004**, criada sobre essa base. Não confundir a base Git com o serviço Linux em execução.
App: branch **feat/station-capas-visuais-netplay-20261003**, pasta **versions/station-online-20261004** em TurboElden. APK R7 SHA256 **827723436ac618d3b1745a873813c7781ff10e043abc6033de416c01d774703d**, 1.982.721.652 bytes. Fontes completas das mudanças, pins, patches, hashes e recibos estão nessa pasta. Referência exata dos commits será registrada no recibo de publicação e no resumo de entrega.

Solicitação autorizada: adicionar presença, convites, salas de dois jogadores e chat entre clientes licenciados. Partida **direta entre aparelhos** usando protocolo RetroArch. Sem servidores públicos, sem relay público, sem transporte de ROMs pelo chat. Usuário autorizou servidor próprio para funções sociais. Não acrescentar compra/plano novo por suposição.

O código foi implementado e testado no Windows com identidades sintéticas. Não houve acesso ao Linux, execução de inventário vivo, deploy, alteração de Nginx/systemd/DB/firewall ou migração. O clone existente foi preservado. Ler AGENTS e guias obrigatórios antes de implantar.

## 2. Arquivos concretos entregues no servidor

- `src/TurboRamaSuiteOnlineServer/StationOnline.cs`: domínio em memória, validações, sala/presença/chat, paginação, limites e idempotência.
- `StationOnlineEndpoints.cs`: duas rotas, autenticação pelo `PostgresStationStore`, assinatura pelo `StationResponseSigner`, limites e revalidação.
- `StationOnlineRegistration.cs`: DI, registro de motores, ligação ao catálogo real em memória.
- `Program.cs`: apenas registro `builder.AddStationOnline(stationLibrary)` no bloco Station e `app.MapStationOnline(stationEnabled && ...Enabled)` no mapeamento. Nenhuma modificação de PIX/Suite/ES.
- `tests/StationOnline/`: testes isolados de domínio/HTTP; dados sintéticos. `Directory.Build.props` separa obj/bin para não executar o teste errado.
- `docs/station-android/online-20261004/engine-registry-candidate.json`: IDs/hashes dos dois motores realmente empacotados. Geolith não liberado.

Registro desligado por padrão: `Station:Online:Enabled=false` (equivalente de ambiente `Station__Online__Enabled=false`). Com false, as rotas devolvem **503 STATION_ONLINE_DISABLED** e não resolvem dependências do banco. Com true, exige `Station:Online:EngineRegistryFile` absoluto, existente, no máximo 32 KiB, 1–32 entradas. Não inventar caminho Linux: escolher no inventário real, copiar o JSON entregue e registrar SHA/permissão/serviço.

**Nenhuma migration nova.** O login e a licença são os atuais; estado social é efêmero em um único processo. Não habilitar em múltiplas instâncias independentes: salas/revisões não seriam compartilhadas. Redis, chat persistente e moderação administrativa são trabalho adicional, não recursos desta entrega.

## 3. Contrato HTTP exato

Base já conhecida do app: `https://app.lzgames.com.br`. Novas rotas:

| Método e caminho | Entrada | Saída |
|---|---|---|
| POST `/v1/station/online/command` | `OnlineCommand`, abaixo | envelope assinado com snapshot |
| POST `/v1/station/online/events` | requestId, instance, revision, page | snapshot imediato se revisão mudou; caso contrário espera mudança até 10 s |

`Authorization: Bearer` usa a sessão Station atual, Base64url canônico de 32 bytes. Não enviar código STA, licença Windows, token Miami, segredo do servidor ou sessão inventada. Identidade de sala é obtida da sessão validada, nunca do JSON enviado pelo app.

Requisição JSON ≤8.192 bytes. Deadline de rota 15 s; long poll ≤10 s, somente um pendente por identidade. Cliente limita envelope a 524.288 bytes. Respostas no-store/nosniff; 429 inclui Retry-After 1. Assinatura:

```json
{"keyId":"ID público já usado por Station","payload":"Base64url dos bytes UTF-8","signature":"RSA-PSS SHA-256 Base64url"}
```

O payload assinado usa exatamente:

```json
{"schemaVersion":1,"domain":"TurboRamaStationAndroid/online/v1","productId":"TURBORAMA_STATION_ANDROID","applicationId":"TURBORAMA_STATION_ANDROID","licenseId":"da sessão","deviceId":"da sessão","sessionId":"da sessão","requestId":"eco exato do UUID solicitado","snapshot":{}}
```

Exemplo estrutural, **não valores para produção**. Usar `StationResponseSigner` existente; não gerar nova autoridade nem trocar TLS pin. O Android verifica domínio, produto, aplicação, licença, aparelho, sessão, requestId e assinatura. Erros são JSON não assinado no formato atual `{schemaVersion:1,code,message}` e não contêm segredo. Não transformar 401/403 em fallback local.

## 4. Ações e campos reais

Toda command inclui `action` e `requestId` UUID canônico (novo para nova ação, mesmo corpo e ID para repetição). `page` opcional padrão 0, intervalo 0–40. Demais campos opcionais ausentes ficam null/0/false; fonte `OnlineCommand` é autoridade.

| action | Campos utilizados | Efeito |
|---|---|---|
| enter | nickname | cria/atualiza presença; apelido de 1–24 caracteres sem controle |
| heartbeat | page | renova presença e devolve snapshot; não cria sala |
| offline | nenhum | remove presença, sala e convites; repetição idempotente |
| create | itemId, engineId, contentSha256, optionsSha256, coreSha256, runtimeSha256 | item precisa existir no catálogo e plataforma corresponder ao motor permitido |
| join | roomId, contentSha256, optionsSha256, coreSha256, runtimeSha256 | exige sala waiting, vaga e hashes iguais aos do anfitrião |
| leave | nenhum | sai da sala atual; se host ou já iniciada, encerra para ambos |
| ready | roomId, value booleano | marca/desmarca pronto na sala waiting |
| start | roomId, address IP literal, port 1024–65535 | somente host; dois membros prontos; passa a starting |
| host-listening | roomId | somente host; confirmação originada no listen() do motor; passa a connecting |
| invite | roomId, peerId | host convida contato, sem bloqueio entre eles |
| dismiss-invite | text = inviteId | somente destinatário remove convite |
| chat | roomId, text | somente membro; 1–500 caracteres, uma mensagem/s |
| block | peerId | bloqueio temporário, remove convites/contato e encerra sala compartilhada |

SHA256: 64 caracteres hex minúsculos. `contentSha256` é do **arquivo de jogo instalado**, não hash do ZIP de transferência; o APK resolve recibo e calcula localmente. `optionsSha256` é SHA256 UTF-8 do texto exato de opções. Registro de motores usa propriedades `id,platform,coreSha256,runtimeSha256`; **id no registro**, **engineId no snapshot/command**. Não trocar nomes.

Motor SNES aceita sfc/smc; Mega bin/md/gen. Variantes BR convergem só para a família do motor. Aliases reais implementados: snesbr/super nintendo/super nintendo - br → snes; megadrivebr/megadrive - br → megadrive; neo geo → neogeo. Não renomear itemId/coverId nem reindexar biblioteca para isto.

## 5. Snapshot e privacidade

`snapshot`: schemaVersion, instance (ID32hex do processo), revision monotônica, selfId, heartbeatSeconds=20, expiresAfterSeconds=60, page, totalPeers, totalRooms, nextPage, peers[], rooms[], invites[], room ou null, engines[].

- peer: peerId, nickname, status online/in-room. Não contém nome civil, e-mail, licença, deviceId ou bearer.
- room pública: roomId, itemId, engineId, hostId, state, players, maximumPlayers=2.
- convite do destinatário: inviteId, fromPeerId, roomId, itemId. O itemId permite aceitar convites de uma sala que está fora da página visível.
- room privada somente aos membros: roomId/itemId/engineId, quatro hashes, hostId/state/generation, connectionPassword aleatória64hex, members[], ready[], messages[], directEndpoint (address/port) após start.
- mensagem: messageId, fromPeerId, nickname, text, utc. As últimas 50 permanecem apenas na sala viva.
- engine: engineId/platform/coreSha256/runtimeSha256.

Senha/endereço não são entregues a visitantes. A API revalida a sessão após a espera antes de assinar resposta. Revogação retira a presença/sala. A senha não é prova de hardware/atestado anti-tamper: quem obtiver o segredo do próprio cliente pode tentar reutilizá-lo. Não prometer exclusividade inviolável ou proteção contra engenharia reversa.

## 6. Sequência de partida e sessão única

1. App foreground usa enter, eventos e heartbeat com a sessão já controlada por `StationSessions`. Esconder catálogo cancela seu acompanhamento.
2. Tela de salas assume presença; consulta assinada não bloqueia UI. Comandos e eventos usam leases concorrentes protegidos contra renovação.
3. Dois apps baixam os respectivos jogos pelo fluxo Station existente; create/join conferem hashes. Ambos marcam pronto.
4. Host informa IP alcançável e porta (padrão app 55435). Não existe relay/UPnP/STUN público. O servidor só compartilha endpoint entre membros; não conecta nesse IP.
5. start produz **starting**. Apenas host abre o motor. O convidado permanece na sala.
6. RetroArch retorna listen() válido; JNI `onNetplayListening` no processo dedicado envia evento por Binder ao processo principal. `StationGameSession` envia host-listening pela sessão atual. O processo nativo **não abre outra sessão**, evitando invalidar autenticação do catálogo.
7. snapshot **connecting** libera o cliente para abrir o motor com a mesma senha/ROM/core/runtime/options. A sincronização e os inputs seguem RetroArch diretamente entre aparelhos. Não declarar partida ativa porque a API respondeu connecting.
8. Heartbeat do jogo é cancelado ao esconder Activity; o Android upstream pausa em perda de foco. Retorno encerra sala anterior, apaga configuração com segredo e retorna à tela de salas. Controles/saves locais não são alterados.

Host que não confirma socket em 30 s tem sala encerrada na próxima varredura. Presença sem sinal expira em 60 s. Processo reiniciado gera instance nova: cliente recusa misturar revisões e pede Reconectar. Não persistir snapshot com connectionPassword em logs/cache público.

## 7. Limites e operação

4.096 peers, 1.024 salas, dois membros, 100 peers/rooms por página, 30 comandos/10 s, 64 recibos por peer, 10 convites por remetente/20 por destinatário, convite60s, bloqueios128, chat50 mensagens/500 caracteres. Limites não são prova de carga nesses volumes. Contagem totalRooms inclui salas ocultas por bloqueio e pode produzir página adicional vazia; conteúdo bloqueado não aparece.

Nenhuma espera é adicionada ao carregamento de capas ou vídeos. Poll social espera eventos para evitar consumo; não confundir esse long poll com atraso entre capas. Serviço social ausente não impede catálogo/login/download. App tenta presença opcional ao foreground; se falhar, evita insistir por cinco minutos. A ação Reconectar da sala permite nova consulta explícita.

Capas continuam quatro simultâneas, sem pausa após sucesso, com cache local. Login/perfil/catálogo/capas/descritor assinado/authorize/artifacts permanecem iguais à base. R5/R7 não reintroduzem Miami/Sambox/drawers.

## 8. Testes reproduzíveis e resultado

Na raiz do repo servidor:

```text
dotnet run --project tests/StationOnline/StationOnline.Tests.csproj
dotnet run --project tests/StationOnline/HttpTests.csproj
dotnet build src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj
```

39 checks domínio; 37 HTTP/Kestrel com rotas reais e assinador real, identidade sintética, sem Postgres real. Cobrem acesso, isolamento de segredo/chat, assinatura/contexto/requestId, limite de corpo, um poll, revogação durante espera, flag off sem DB, socket-ready/host e timeout/paginação. Não são prova de produção. Teste real no telefone/par de aparelhos ausente; APK contém essa condição no BUILD-NOTICE.

## 9. O que o operador precisa entregar de volta

Publicar **RETORNO-SERVIDOR-ONLINE-STATION-20261004.md**, citando este pedido e o commit exato do app. Responder separadamente:

1. ONL-01: base/revisão de código analisadas, serviço real, ExecStart, drop-ins, caminho/hash da DLL e inventário atual; não inferir pelo checkout.
2. ONL-02: resultado dos 39+37 testes e regressões de licença/catálogo/capas/download e dos serviços compartilhados; banco/testes isolados.
3. ONL-03: flag inicialmente false; rota503 sem impacto nos produtos existentes. Registro JSON com hashes idênticos ao APK, caminho e leitura pelo usuário real.
4. ONL-04: proxy publica exatamente os dois POSTs sem cache e suporta janela >15 s; preserva bearer/correlação sem registrá-los. Não inventar config ou reiniciar Nginx sem plano autorizado.
5. ONL-05: com aprovação para homologar, duas identidades sintéticas/licenciadas válidas: assinaturas, lista/paginação, convite fora da página, chat, hashes divergentes recusados, revogação, desconexão e reinício.
6. ONL-06: registrar se a alteração está apenas no Git, homologada ou implantada. Para implantação: artefato/hash/serviço/alvo, backup e retorno concretos; nenhuma migration deste módulo.
7. ONL-07: disponibilidade HTTPS real das rotas com flag habilitada, sem publicar tokens/senha/IP de usuário. Partida P2P será homologada em dois aparelhos separadamente.

**Impedimentos atuais:** Linux/serviço novo ainda não implantados, USB ausente e segundo aparelho não disponibilizado, Neo Geo sem fluxo .neo/BIOS verificado, CPS/MAME/FBNeo não integrados ao motor direto. Não mascarar com lista de jogadores simulada, abertura de motor diferente ou botão que declare conexão sem tráfego real.

## Referências exatas publicadas

- App: **dd6aff172761f40c9b8eea2d3e2c1c34a0c79034** — [fontes e README completos](https://github.com/luziellacerda/TurboElden/tree/dd6aff172761f40c9b8eea2d3e2c1c34a0c79034/versions/station-online-20261004). Foram conferidos os hashes dos 211 arquivos publicados.
- Código do servidor: **308fda4124439110a34db9beba689c6f96535b39**, branch `feat/station-online-direct-20261004`. Este acréscimo é documental; não muda código/API/APK nem implanta serviço.
- Executar a revisão sobre essas referências, não sobre uma branch antiga, o R6 superado ou o APK estável de recuperação.
