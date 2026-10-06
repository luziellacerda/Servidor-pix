# Retorno de produção — comunidade Station R41 — 06/10/2026

**Comunidade habilitada na API Station e verificada no domínio público.** Execução concluída em06/10 às13h01min38s (Maceió), `2026-10-06T16:01:38.111203+00:00`. O recibo Android posterior `5e40f7e` confirma R41 instalada no Samsung; gameplay real em dois telefones permanece pendente.

## Referências conciliadas

| Origem | Revisão completa |
|---|---|
| App R41 compilado | `cfa5ac2666d950fc1f8d9b58ec3e0a0c046a36e9` |
| Delta/handoff do servidor recebido | `32b12bc5654b28f6dc73b9f5c2de2ef6a64bb616` |
| Base Git do delta | `be2ba1f9c4d822c4c5d9731af375498d869184b2` |
| API anterior efetiva | `e4e557a985ac5bead24885149c8650a5bb2dfae8` |
| **Fonte da API publicada agora** | **`a2bb176530fd4d2dfa740da7e934fd84d097404e`** |
| Recibo de instalação Samsung recebido durante a execução | `5e40f7e0f09942aeb9c9f8caf57e8a3697973495` |

O delta foi conciliado com a API efetiva e testado em worktree próprio. O ajuste adicional de tamanho de mensagens e o procedimento de publicação estão na revisão implantada. As fontes congeladas, DEX e APK R41 do app foram preservados.

## Serviço e artefato efetivos

| Campo | Valor após publicação |
|---|---|
| Unidade | `turborama-station-api.service`, ativa |
| PID observado | `875574` |
| ExecStart | `/usr/bin/dotnet /opt/turborama-station-community-r41-20261006-a2bb176/TurboRamaSuiteOnlineServer.dll` |
| WorkingDirectory | `/opt/turborama-station-community-r41-20261006-a2bb176` |
| DLL SHA256 | `d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13` |
| Novo drop-in | `zzzzzzzzzzzzz-station-community-r41-20261006.conf` |
| Flag efetiva | `Station__Online__SocialEnabled=true` |
| Fonte StationOnline.cs SHA256 | `9b81b4c9ea817fb71876fc23bb868610c24d9d0c51d42eb5f8061c121d467f10` |
| Fonte StationOnlineRegistration.cs SHA256 | `98485475345729907ff5f52d15d84cca0b46f1245d2cf9e0293fe9b54e7b82df` |

O manifesto da release contém os hashes dos12 arquivos publicados. A normalização CRLF→LF preserva a lógica de DI recebida. Flags Online/Relay continuam ligadas, 512 salas/1.024 conexões configuradas. Após limpeza dos testes: zero salas/conexões de relay ativas e131.078 bytes encaminhados pelo processo novo. O contador foi reiniciado com a API; o processo anterior tinha43.592.958 bytes.

Catálogo preservado: revisão14, 2.212 itens visíveis e255 de compatibilidade. Índice SHA256 `07ad4c3fda41a19c23745436c4c45c97a70b2c7688eff788612fe22743823a92`; registro dos motores SHA256 `901c8f52eaadfc8d3ad41ed5cc2c30bcaeb5ea893550d0feab5729bbb4055a6a`. A regressão comparou todos os nomes, plataformas, coverIds, metadados e pastas com o índice, quatro capas simultâneas com bytes exatos e download real limitado de262.144 bytes. A licença POCO existente não foi ativada, reemitida ou alterada pelos testes.

## Como o app deve consumir

Autoridade/domínio/rotas/chaves continuam os existentes. Usar a sessão Station adquirida por `StationSessions.Lease`, validação do pin TLS, assinatura RSA-PSS e vínculos de produto/aplicação/licença/aparelho/sessão/requestId já presentes em `StationApi`.

| Operação | Rota e dados |
|---|---|
| Catálogo e metadados | `GET https://app.lzgames.com.br/v1/station/catalog?metadata=1`, Bearer Station |
| Capa | `GET /v1/station/covers/{coverId}` do item assinado; nunca derivar URL do nome do jogo |
| Download | `POST /v1/station/downloads/authorize` para o item assinado, depois o grant de uso único existente |
| Comunidade | `POST /v1/station/online/command`, Bearer Station, action/requestId UUID-D e campos da ação |
| Atualização | `POST /v1/station/online/events`, Bearer Station, requestId UUID-D, instance/revision/page do snapshot |
| Partida pela internet | `wss://app.lzgames.com.br/v1/station/online/relay`, protocolo `station-relay.v1`, ticket privado existente |

No snapshot assinado agora chegam `socialCapabilities: ["direct-chat-v1","join-request-v1"]`, `directMessages`, `joinRequests` e `sentJoinRequests`. Habilitar as funções pela presença dessas capacidades. Ausência mantém o comportamento anterior. A R41 já implementa essa leitura; esta publicação não exige recompilar o APK instalado para habilitar o servidor.

| Ação | Campos e efeito |
|---|---|
| `direct-chat` | `peerId`, `text`: destinatário online,1–500 caracteres sem controles, autor/nickname/utc atribuídos pelo servidor |
| `request-join` | `roomId`: cria pedido, não adiciona membro nem altera a sala atual do solicitante |
| `accept-request` | `text` recebe o `requestId` hex32 do pedido retornado ao anfitrião; gera o convite normal |
| `dismiss-request` | `text` recebe esse ID hex32; somente o anfitrião pode dispensar |

O `requestId` UUID-D da ação é diferente do ID hex32 do pedido. Não reutilizar um UUID para um corpo diferente. Repetição do mesmo corpo é idempotente.

Fluxo do segundo jogador: pedir entrada → anfitrião aceitar → receber convite → preparar a mesma edição → sair da própria sala, se houver → `join` com hashes de jogo/motor/runtime/opções → dois nomes juntos → ambos Pronto → anfitrião Iniciar → `host-listening` real → liberar o convidado. Pronto sozinho não entra em outra sala. A edição divergente continua rejeitada com409 `STATION_ONLINE_BUILD_MISMATCH`.

Presença expira após60s sem atualização; pedidos/convites têm TTL60s. A R41 coordena o poller entre catálogo, lobby e jogo; manter essa coordenação. Mensagens ficam somente na memória da presença de cada participante, sem prazo individual por mensagem ou armazenamento permanente. Até32 mensagens na fila e até65.536 bytes das mais recentes na resposta. O destinatário ativo pode manter sua própria fila após o outro sair; bloqueio remove o histórico entre os dois. Não há push para offline, leitura confirmada ou suporte a múltiplas instâncias.

## Correção adicional feita

Um teste com históricos/página/pedidos/convites máximos e32 motores com Unicode produziu envelope544.672 bytes; Android aceita524.288. A fila privada continua limitada a32; a seleção enviada também respeita65.536 bytes de JSON escapado e mantém mensagens inteiras, na ordem, incluindo a mais recente. Mensagens curtas continuam retornando32. O custo é calculado uma vez na aceitação e não aparece no JSON.

A mesma amostra passou com envelope486.212 bytes. O ajuste está apenas no servidor; assinatura, rotas, campos e limites de texto permanecem compatíveis com o APK R41.

## Testes e prova de produção

| Gate | Resultado observado |
|---|---|
| Build API .NET8 completo | 0 erros, 0 avisos |
| Núcleo social, limites, TTL e tamanho de resposta | 34 verificações |
| Núcleo das salas/convites/relay | 41 verificações |
| Kestrel com rotas e signer reais, autenticação sintética | 41 verificações |
| API/ativação/sessão/assinatura com PostgreSQL temporário | smoke completo aprovado;92 verificações online |
| Comunidade via API e PostgreSQL temporário | 171 com flag ligada,93 com flag desligada |
| DLL final empacotada | smoke autenticado aprovado |
| Candidato com ambiente/usuário reais em loopback | 93 desligada,171 ligada |
| Regressão do relay real no candidato | 2.554 verificações;2.097.269 bytes em cada direção,20 pacotes ordenados, revogação/poll/sockets/limpeza |
| Release publicada inicialmente com social desligado | 93 verificações de compatibilidade |
| **Domínio público após habilitar** | **186 verificações, três licenças independentes;65.539 bytes reais em cada direção por WSS, pin TLS conferido** |

A prova pública confirmou: assinatura e vínculos por requisição; terceiro sem acesso à mensagem; autor fornecido pelo servidor; retry sem duplicar; acesso anônimo401; pedido sem entrar; autoridade do anfitrião; rejeição de sala própria409; saída e entrada explícitas; edição divergente409; consumo do convite; dois membros; Iniciar exclusivo do anfitrião403 para o convidado; espera pelo host-listening; WSS bidirecional; encerramento dos sockets ao sair; limpeza por bloqueio; eventos autenticados e reconexão com novo peer. Throttle de1s e expiração usam testes de loopback/relógio controlado; latência pública não serve de relógio para esse gate.

Exemplos dos códigos/correlações efetivos, extraídos da evidência pública:

| Ação | HTTP | X-Correlation-ID |
|---|---|---|
| `direct-chat` | 200 | `a256c2b3d31c417892e09a348ecc6879` |
| heartbeat anônimo | 401 | `d6803c6efded4667a9c571c259c8a1f3` |

Os demais códigos/requestIds/correlações e trechos do snapshot estão em [COMUNIDADE-R41-PRODUCAO-EVIDENCIAS-20261006.json](COMUNIDADE-R41-PRODUCAO-EVIDENCIAS-20261006.json). Os trechos foram copiados **após** conferir a assinatura real e os vínculos; não são envelopes assinados completos. Licenças, aparelhos, sessões, bearers, tickets, senhas de sala, códigos de ativação e caminhos privados de jogos/capas foram omitidos. Todas as fixtures foram removidas.

## Publicação e retorno operacional

Backup privado da API/configurações e dump PostgreSQL restaurado em cluster temporário foram conferidos antes da ativação. Somente a unidade Station recebeu novo drop-in/reinício. Nenhuma migration foi aplicada. Hashes das configurações, índice/motores/artefato anterior, PIDs dos demais serviços e linhas das licenças existentes permaneceram iguais. Nginx, Cloudflare, firewall, chaves, scanner, mídias, comércio, painel e outros produtos foram preservados. Nenhuma mensagem externa foi enviada.

O inventário após a publicação confirmou PIX/Suite/Gateway com saúde HTTP200. `turborama-suite-content-monitor.service` continuou com a falha preexistente observada antes; esse serviço não foi alterado nesta publicação Station.

Com autenticação administrativa Linux, usar o script da revisão implantada:

```sh
pkexec /mnt/DADOS/station-relay-check-20261005/venv/bin/python /mnt/DADOS/servidor-pix-station-community-r41-20261006/docs/station-android/scripts/implantar-comunidade-station-r41-20261006.py --disable-social a2bb176530fd4d2dfa740da7e934fd84d097404e
```

Para retornar a `e4e557a`, trocar `--disable-social` por `--rollback`. O primeiro mantém a API nova e as salas/relay anteriores; o segundo remove só o drop-in R41. Ambos recusam uma release sucessora e partidas com conexões de relay ativas. Não restaurar o dump sobre o banco vivo: não houve migration a desfazer. O dump e o relatório integral estão privados no servidor. O script registra falhas e tenta retornar automaticamente após erro de ativação; essa publicação concluiu sem rollback.

## Estado Android e trabalho restante

O recibo `5e40f7e` informa Samsung SM_A566E atualizado R39→R41, APK SHA256 `b6b19321ec529945870dc919c3de5f0eba75aa54330d3672227b8e632302b28d`, assinatura original/dados preservados. Abriu catálogo sem login e comunidade Online com zero salas. USB saiu antes de conferir Pessoas online e Voltar. Isso é evidência enviada pelo operador do app; esta execução Linux verificou servidor, não o telefone.

Agora testar a R41 no Samsung e no POCO, preservando dados/assinatura: pessoas/presença, conversa privada real, pedir/aceitar convite, dois membros na mesma sala, Pronto/Iniciar e gameplay de ambos. Conferir também Voltar vindo das configurações e a correspondência do jogo selecionado: o recibo capturou Boogerman no catálogo e Battletoads no cabeçalho; a causa ainda não foi estabelecida. Não substituir a R41 pelo delta R34.

Capacidade configurada de512 salas e provas sintéticas não certificam gameplay para centenas. A latência externa histórica p95 acima de2s ainda exige diagnóstico e medição em dois Android. Nenhuma estabilidade geral ou solução dessa latência foi declarada.
