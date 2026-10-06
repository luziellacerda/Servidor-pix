# APP → SERVIDOR: comunidade TurboStations R41 — 06/10/2026

DESTINATÁRIO: operador do Servidor-pix / API Station. Este é um pedido de integração e homologação, NÃO um retorno dizendo que o servidor já foi publicado.

## Escopo e autoridade

Produto TurboStations Android (`org.turboramastation.frontend`, namespace Java `org.emulationstation.frontend`). Somente Station. Preservar Suite Windows, Turborama, PIX, licenças, catálogo, capas, download, proxy, relay e as demais aplicações.

Base remota lida: `be2ba1f9c4d822c4c5d9731af375498d869184b2`, branch `feat/station-online-direct-20261004` (também retornada nos ramos Station equivalentes). O clone existente foi atualizado sem trocar seu checkout. A leitura dos arquivos não comprova o binário em produção. O retorno anterior identifica API e4e557a; conferir ExecStart, release efetiva, flags e hash da DLL antes de transportar o delta.

O usuário pediu navegação lateral Ver salas / Pessoas online / Criar sala, pessoas visíveis com o app aberto, convite e conversa no próprio aplicativo. Corrigiu expressamente “não é para chamar no WhatsApp real”. Depois informou que há MenuIA. A pergunta sobre canal continua pendente: NÃO habilitar disparos externos por inferência. Nesta revisão não há integração MenuIA, número de telefone, appkey ou envio externo.

## Publicação vinculada e arquivos exatos

App publicado: TurboElden, branch `feat/station-capas-visuais-netplay-20261003`, commit `cfa5ac2666d950fc1f8d9b58ec3e0a0c046a36e9`. [Fonte e receita exatas](https://github.com/luziellacerda/TurboElden/tree/cfa5ac2666d950fc1f8d9b58ec3e0a0c046a36e9/versions/station-community-r41-20261006). Esta branch de servidor é `feat/station-community-r41-20261006`, descendente direto de `be2ba1f9c4d822c4c5d9731af375498d869184b2`. Não representa uma implantação.

| Arquivo | SHA256 antes | SHA256 entregue |
|---|---|---|
| `src/TurboRamaSuiteOnlineServer/StationOnline.cs` | `cb61c3504e60a252f5ca803e06d660bf10ab6b5f06d37ee2ed38b7f8f0c66352` | `476eaee22c4e0870f221aa4f7870bf1f47f25848d621d0f6a11c992e3a2a9449` |
| `src/TurboRamaSuiteOnlineServer/StationOnlineRegistration.cs` | `8f8ce5fcad008ec55c63342060dddfc1cbcec194ffc9463410beb88844ab687a` | `6fa41b5b46d5081350f6e293861e0037c20789394773b32b676302535e614249` |

O delta inclui somente duas fontes da API, testes sociais isolados e este handoff/evidência. Os arquivos `.base` estão no snapshot do app. Se a release efetiva divergir, conciliar o delta: não copiar arquivos sobre modificações do operador sem análise.

Comandos na raiz do repositório em ambiente isolado:

```sh
dotnet run --project tests/StationOnlineSocial/StationOnlineSocial.Tests.csproj -c Release
dotnet run --project tests/StationOnline/StationOnline.Tests.csproj -c Release
dotnet build src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj -c Release
dotnet run --project tests/StationOnline/HttpTests.csproj -c Release
```

As duas primeiras suítes tiveram 25+39 verificações aprovadas no PC. Build completo da API e testes HTTP são exigências de homologação do operador; não foram declarados aprovados nesta entrega. Nenhum acesso à máquina Linux, restart ou alteração de configuração foi realizado aqui. O inventário Linux datado no Git não substitui conferir o serviço em execução.

## Artefato Android pronto

APK: `G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Comunidade-R41-20261006.apk`.

SHA256: `b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d`.

Base R40: `da803b3eb7c0fb24b272795449c5527d752f71e8809d132cae3300071a3c5678`. Só `classes35.dex` foi alterado nesta montagem: `42c1fc51402e6e464e97ad0561f2ffca4bdb5908189932f545a2e625e4333ad1`. Todas as 13.179 demais entradas foram conferidas integralmente. Inclui R40 (céu azul/nuvens claras, interface preta, seletor à direita, robô em ciclo normal), por herança da base. R40/R41 ainda NÃO instaladas: USB ausente. R39 é a última instalação comprovada. Não promover a estável nem afirmar partida entre dois Android.

Fonte completa: `E:\ESTUDO APK\work\station-community-r41-20261006`, snapshot `versions/station-community-r41-20261006` no TurboElden. Não usar receitas R30/R34 antigas sobre o APK atual. O delta do segundo jogador f7f0561 foi conciliado antes da nova UI. Preservados exatamente: classes de abertura/relay/runtime, identidade dos jogos, controles, Voltar do motor, TLS, licença, saves e todos os emuladores locais.

## O que já funciona com o contrato anterior

- Página Android dedicada com navegação lateral e listas paginadas de pessoas/salas; sem pessoas ou salas simuladas.
- Lista pública continua disponível quando o usuário já está em outra sala. Troca de sala própria individual pede confirmação e confere o jogo antes de sair.
- Criar sala permite selecionar um jogo do catálogo compatível com os motores anunciados. A validação de arquivo instalado, hashes, versão e opções continua no worker antes do create/join.
- Convites, código TS1, confirmação Pronto, início somente pelo anfitrião, chat da sala e relay existente mantidos.
- Presença ligada ao catálogo e telas do processo principal em primeiro plano; entrar em Salas cede a consulta ao controlador das salas; gameplay online continua com StationGameSession. Sem polling duplicado intencional. Pausar/fechar tela cancela requests/worker. A presença expira pelo TTL existente de 60s; não é presença instantânea de um app fechado.
- Queda transitória reconecta presença em 2,4,8,16,30s, somente em primeiro plano; catálogo/login não dependem da presença.
- Convite recebido no catálogo abre aviso clicável dentro da Activity. Não usa overlay de outras aplicações, WhatsApp, SMS ou notificação externa.
- Voltar ao catálogo não depende de resposta de rede: marca leaveOnCatalog, fecha a página, e o proprietário de presença em primeiro plano conclui leave autenticado antes de ouvir eventos. A marca só é removida após sucesso. Não desloga.

## Extensão nova entregue: NÃO supor já existente em produção

Duas fontes completas e os arquivos `.base` correspondentes são entregues em `server/`. Em Servidor-pix, integrar SOMENTE:

1. `src/TurboRamaSuiteOnlineServer/StationOnline.cs`
2. `src/TurboRamaSuiteOnlineServer/StationOnlineRegistration.cs`

O construtor aceita novo `socialEnabled=false`. DI lê `Station:Online:SocialEnabled`, padrão false. Sem nova rota, porta, migration, produto ou autoridade. Rotas já autenticadas/assinadas permanecem:

- POST `/v1/station/online/command`
- POST `/v1/station/online/events`

Envelope e `schemaVersion=1`, assinatura e vinculação sessão/licença/aparelho/requestId seguem intactos. O app só usa as ações novas se o snapshot ASSINADO anunciar a capacidade correspondente. HTTP200 sozinho não habilita nada.

### Campos novos do snapshot

| Campo | Tipo/semântica |
|---|---|
| socialCapabilities | [] com flag desligada; [`direct-chat-v1`,`join-request-v1`] com flag ligada |
| directMessages | até32 mensagens que envolvem EXATAMENTE o peer autenticado, em ordem; vazio com flag desligada |
| joinRequests | pedidos recebidos somente pelo anfitrião autenticado; `{requestId,fromPeerId,roomId,itemId}` |
| sentJoinRequests | pedidos do próprio peer; `{requestId,roomId}` para impedir botão duplicado |

Cada mensagem: `{messageId,fromPeerId,toPeerId,nickname,text,utc}`. `nickname` é o nome do remetente registrado no hub, não texto fornecido na ação. `utc` é DateTimeOffset do servidor. Não há marca de leitura: a UI NÃO mostra “lida” ou “entregue no aparelho” sem confirmação. Mensagem exibida após snapshot assinado significa aceita pelo servidor.

### Ações novas, no body OnlineCommand já existente

Todas têm `action`, UUID canônico `requestId` e `page`0..40. Reuso do mesmo requestId com mesmo corpo é idempotente; com corpo diferente é409.

| action | Campos adicionais | Efeito |
|---|---|---|
| direct-chat | peerId, text | mensagem privada, mesmo sem sala; remetente vem da sessão |
| request-join | roomId | pede vaga ao anfitrião; NÃO entra automaticamente |
| accept-request | text=requestId do pedido recebido | anfitrião emite convite normal para o solicitante; NÃO pula hashes/join |
| dismiss-request | text=requestId do pedido recebido | anfitrião remove pedido sem criar convite |

`requestId` da ação é UUID diferente do ID de pedido recebido em `text` (ID hexadecimal de32caracteres). Explicitar essa diferença evita reutilizar o identificador errado.

Fluxo completo: B vê sala A → request-join → A recebe joinRequests → accept-request → B recebe invites → Aceitar e entrar → prepara jogo → leave se necessário → join → dois participantes → ambos ready → A start → host-listening → convidado abre automaticamente. Não alterar start/relay nesta integração.

### Limites e proteção

Texto1..500 caracteres, sem caracteres de controle; uma mensagem/s por remetente compartilhando limite de chat existente. Não é permitido falar consigo, peer ausente ou bloqueado. Inbox32/peer; no máximo5 pedidos enviados/peer e20 por sala; pedido60s; convites e quotas anteriores preservados. Não retorna licença, telefone, MAC, IMEI ou chave no snapshot público.

Pedido não concede vaga nem permite start. Aceite exige anfitrião/membership e sala waiting com vaga. Bloqueio remove mensagens entre os dois e pedidos/convites relacionados. Revogar presença continua cancelando sala/relay pelos caminhos anteriores. O join bem-sucedido limpa convite consumido e pedidos enviados antigos. A fila em memória é limitada; sem tráfego em segundo plano adicionado.

**Retenção explícita:** conversas são efêmeras,32 mensagens por presença. O servidor atual é single-instance e mantém salas em memória. Reinício/expiração da presença perde esse histórico; a UI informa “enquanto a presença estiver ativa”. Não alegar histórico durável, mensagens para aparelhos offline, confirmação de leitura, múltiplas instâncias ou push externo. Para ampliar esses requisitos, precisa contrato persistente próprio; não criar tabelas por suposição.

## Testes realizados no PC

255 verificações Java (113 social;20 segundo jogador;107 códigos/perfis/snapshot;15 abertura host/guest).64 C# (25 social novas;39 regressões existentes).149 fontes Java/dependências compiladas API34/D8 min26. Assinatura original,16KiB e preservação integral do APK conferidas. Mensagens isoladas entre A/B e invisíveis para C, idempotência, flood, bloqueio, capacidade32, flagoff, pedidos/expiração/autoridade, dois membros/prontos e transição de início foram testados com identidades sintéticas.

Não houve mensagem externa real, API produtiva autenticada de dois clientes, build da release Linux inteira, homologação de DI em produção, prova visual Android R41 nem partida em dupla. Os testes são lógica real C#/Java no host, não uma simulação declarada como aparelho.

## Aplicação pelo operador e retorno obrigatório

1. Ler este documento e comparar hashes `.base` com fontes efetivas. Preservar trabalho local e os retornos be2ba1f/f7f0561.
2. Integrar os dois arquivos no ramo compatível com a release em uso. Não substituir a árvore do servidor por um snapshot antigo. Build completo da API, testes existentes HTTP/sessão/assinatura/relay e os novos testes sociais em ambiente isolado.
3. Publicar pelo procedimento atual do serviço Station, com release/backup/rollback identificados. Registrar o nome real do serviço e DLL em execução; não adivinhar pelo checkout. A chave `Station__Online__SocialEnabled=true` habilita a extensão nessa API. Não habilita MenuIA nem muda outro produto.
4. Com duas licenças sintéticas/autorizadas, provar: presença só com catálogo aberto, DM A/B sem sala e invisível C, aviso no app, criação/pedido/aceite/join, hash divergente recusado, dois prontos, start/relay, saída, bloqueio, expiração e reconexão. Nunca publicar tokens/códigos de ativação.
5. Devolver `RETORNO-COMUNIDADE-STATION-R41-20261006.md`: commit completo, release/serviço/DLL hash, flag, snapshot sanitizado com capacidades, comandos+status+código/correlação, testes, estado de rollout, rollback, pendências. Código pronto e produção publicada são estados distintos.
6. Se algo falhar, desligar SOMENTE SocialEnabled para manter salas/convites anteriores. O APK detecta ausência das capacidades e mostra a conversa privada como indisponível sem chamar ações inexistentes.

## MenuIA: constatação, não autorização de disparo

O repo contém `TurboRamaWhatsAppNotifier.cs` e o fluxo Suite → outbox → worker → `tb_queue_whatsapp(customerId,tipo,telefone,mensagem)` → MenuIA, documentado em `src/TurboRamaSuiteNotifications/README.md`. Isso é um canal de WhatsApp; não transporta automaticamente mensagens para a tela Android. A biblioteca instalada `notification-lib.php` não é incluída neste Git. Sua existência não prova novo tipo Station aceito nem entrega ao destinatário. Esta revisão usa exclusivamente eventos autenticados do próprio Station. Não copiar credenciais MenuIA ao APK nem reutilizar produto Suite para convites Station.
