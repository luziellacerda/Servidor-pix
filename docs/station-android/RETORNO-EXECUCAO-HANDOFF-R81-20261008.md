# R81: catálogo e base do servidor publicados; aprovação dos controles pendente

**SERVIDOR → APP**, execução solicitada do handoff `2cd3571919f54126f2bfe9576864a8d8329b9db3`. Este retorno contém implantação efetiva, catálogo completo, vínculos preparados e resultados sanitizados em [recovery-r81-20261008](recovery-r81-20261008/). O serviço atualizado conserva a admissão atual da R76. **A ativação online da R81 ainda está incompleta, inclusive para duas pessoas:** faltam perfis reais aprovados e continuidade qualificada antes de ligar o gate global. Nenhum perfil foi aprovado por ensaio sintético.

## Explicação para o mantenedor

“Qualificar os controles” significa abrir o jogo e conferir que cada pessoa movimenta somente seu próprio jogador. Para Battletoads são dois jogadores; para o modo Battle do Bomberman são dois, três e quatro. É essa confirmação prática que o pacote do app declara pendente. O PC que produz o APK deve preparar esse teste e registrar o resultado; o mantenedor só precisa operar os controles quando o teste estiver pronto.

Até essa aprovação, a referência R76 permanece com suas dez engines. A R81 usa outro core/runtime e exige outro vínculo de autorização. Atualizar o servidor ou instalar a R81, isoladamente, não comprova esses controles nem libera suas salas.

## Resultado dos cinco passos recebidos

| Passo | Execução e limite |
| --- | --- |
| Identidades persistentes | **Aplicado** às 19:50:57 UTC: 2.071 IDs SNES/Mega, incluindo compatibilidade, vinculados ao recipiente real e ao arquivo de lançamento exato. Revisão global20. Importador agendado substituído; duas varreduras produziram o mesmo índice. |
| Perfis exatos | **Preparados** 2.071 rascunhos da R76 e três vínculos dos pilotos R81; todos `approved:false`. Sete hashes canônicos conferidos preservando a ordem original das chaves. Capacidades propostas não são capacidades homologadas. |
| Continuidade R76 | **Preservada em produção** com gate desligado, mesmas dez engines e novas salas v2/v1 verificadas. Ativar o gate com esses rascunhos recusou concretamente uma sala R76 na sombra, HTTP409 `STATION_MULTIPLAYER_PROFILE_REQUIRED`. |
| Publicação Station | **Aplicada** a DLL selada de `b472d8a` às 20:22:03 UTC, após provas antiga/nova/retorno antigo e ausência de sessões. Novos campos de conteúdo e observabilidade v2 ativos; flags v3/gate continuam `false`. |
| Domínio público e partida | **Passaram** catálogo autenticado/assinado, metadados, identidade, capa, download, provas/tickets, salas v2/v1 e reconexão. Capabilities v3 em produção retorna **HTTP503 `STATION_MULTIPLAYER_DISABLED`**. Gameplay físico R81/3p/4p permanece pendente. |

## Produção efetiva

- Serviço `turborama-station-api.service`, PID **1518810**, `NRestarts=0` na conferência posterior. Recibo concluído **2026-10-08T20:22:39.593284Z**.
- Fonte do binário **`b472d8a653e065cccb00dfb15dbea3d56c03db98`**; DLL SHA256 **`9878ae9caea55bb6834745caa3a60140616d3e0a5f2ea71055fe9e9df813fe51`**. Não atribuir a DLL a este commit documental de retorno.
- Release `/opt/turborama-station-base-r81-20261008-b472d8a`; override próprio e reversível. Release `ab192bf` e seus arquivos/configurações originais preservados.
- Registro legado SHA256 **`a5f9de948ab3fcf15b2657061d540dcbbafda98e1dd7a68137e617b58a894843`**, dez engines, mesmos bytes. Engines v3 estão somente nos rascunhos de perfis.
- Índice **revisão20**, **3.734 IDs / 3.479 visíveis / 255 de compatibilidade**, SHA256 **`b3d44224a0264f3a14104f031d417492017b1d7756d7256607a51833c243348c`**. As 206 sinopses da revisão19 e todos os demais dados foram conservados; ainda há 144 descrições vazias.
- Identidades: **2.071 no índice / 1.816 no catálogo visível**, registro SHA256 **`3ac983815a88c5bc0a434aa9c2b67e6ee2114b8f061547198a0ca7815af9e364`**. Campos ausentes nos outros sistemas permanecem ausentes.
- Importador ativo/agendado `/opt/turborama-station-library-r81-20261008-b472d8a/atualizar-biblioteca-station.py`, SHA256 **`fdce74d82478b5a6688bcb67baad55193580aad139603c1d02b379610c868b05`**. Timer restaurado; configuração privada aponta para o registro persistente.
- `MultiplayerEnabled=false`, `MultiplayerLegacyCapacityGate=false`, Recovery/Online/Relay mantidos. Registro preparado de 2.074 rascunhos, SHA256 **`c3dae72ccd2472a36d92655fa5401023c120ce1768d550309f43befd8f05658b`**, **zero aprovados / zero carregados para admissão**. Editar o arquivo sozinho não ativa o processo.

## Como o app deve ler os dados

1. Usar a sessão/prova HTTP e o envelope assinado vigentes em **`https://app.lzgames.com.br/v1/station/catalog?metadata=1`**. Conferir revisão global20 e reconstruir os campos recebidos mesmo quando a revisão individual do jogo ficou igual. Sem `metadata=1`, a sinopse é omitida por contrato; `contentSha256` é publicado nas duas formas quando existe.
2. Vincular capa pelo **`coverId` do próprio item**, e autorização de download pelo **`itemId` exato**. Usar o descritor do grant para recipiente/formato/arquivo de lançamento. A rota de artefato continua protegida; não inferir URLs pelo nome ou pelo caminho do HD.
3. Para online, usar **`contentSha256` do payload exato**, distinto do SHA do ZIP. Battletoads visível `826da6daebe9edbebffb3721f83abf12`: ZIP `cd3a1292fdb5953ca414dd6a74ac0aa1886b2d132fce4c804e79d5e25ea029f6`, payload **`5d04756017d5999e91c017d3ce1b144f5030de82e9350fcf4c690a28551e9bb6`**. O membro real é `Battletoads in Battlemaniacs (ESP) (NTSC).smc`, embora o rótulo histórico seja USA; nenhum arquivo/ID foi renomeado. O ID oculto `ac79b8fc351cb0811a7e9b51a099ba9c` conserva download compatível, mas não aparece na consulta de plataformas online; não herdar aprovação entre esses IDs.
4. Bomberman USA `station_df50d575815ab105084a79d68e0c8fb3`: raw/payload **`0a4b4a783a7faf6ada3e1326ecf85de77e8c2a171659b42a78a1fae43f806ca6`**. Proposta somente **Battle → Single Match**, pads MAN, controlador `snes-multitap-port2-v1`, perfil SHA **`f59408b49f6e110a569fe9498af433b9f67c9586cbb8648238b8e571bb8b0143`**, contagens `[2,3,4]`, ainda sem aprovação. Normal Game não concede vagas.
5. Consultar capabilities em `POST /v1/station/online/multiplayer/command`, com `action:"capabilities"`, `requestId` novo e `itemId`. O **primeiro bloqueio atual é HTTP503 `STATION_MULTIPLAYER_DISABLED`**, comprovado nos três IDs às 20:22 UTC. Essa resposta é falha de disponibilidade do modo, não código de licença incorreto. Na sombra habilitada, os envelopes assinados informaram `multiplayerVersion:3` e `capability:"station-multiplayer.v3"`, com perfis visíveis não aprovados; o alias oculto permaneceu sem perfil admitido. Isso não é prova pública de v3 ativo.

[CATALOGO-REV20.json](recovery-r81-20261008/CATALOGO-REV20.json) entrega todos os IDs, nomes, capas, metadados, recipientes e vínculos factuais para comparação. É projeção sanitizada do índice: 255 aliases adicionais são explicitamente ocultos; as consultas assinadas comprovaram os 3.479 visíveis. Nenhum caminho absoluto de ROM/capa, sessão ou envelope de usuário está no pacote.

## Provas da publicação e divergência TLS

O operador novo usou baselines atuais, não executou implantação histórica R74. Processos de sombra com a mesma identidade isolada e mounts somente de leitura verificaram: DLL antiga **187 checks online + 27.846 catálogo**, nova **303 + 27.852**, gate não aprovado **27.860 catálogo/capabilities**, retorno à antiga **187 + 27.846**. Prova pública após troca: **306 checks online + 27.852 catálogo**, incluindo Close1000, primeira causa anterior ao Detach, mesma geração na retomada, bytes exatos nas duas direções, pin TLS, prova/ticket de uso único e legado v1. Todas as licenças de ensaio foram removidas. Zero salas/conexões/pendências retidas no fim.

No ensaio TLS antigo sem logging, o cliente Python síncrono apresentou timeouts intermitentes mesmo sem a decoração de sockets do fixture. O cliente independente assíncrono passou cinco rodadas do build e **três rodadas com a DLL selada**, repetidas **três com o fixture decorado**; DATA/ACK/PONG/Close/reconexão foram conferidos. A decoração isolada não explica a falha. A causa exata do cliente síncrono ainda não foi demonstrada; os controles falhos permanecem no recibo. As provas independentes e a sombra/domínio público qualificaram a publicação da base sem mudar logging de produção como remendo. **Não comprovam correção dos engasgos, RTT/FPS dos telefones ou estabilidade WAN.**

Backup privado guarda o estado e overrides anteriores; antigo/novo/antigo foi verificado isoladamente. Retorno só pode retirar o override próprio, com conferência literal do seu SHA e ausência de salas/conexões/sessões retidas, e reiniciar somente Station. Não restaurar banco nem usar operadores históricos com baselines antigos. O índice20 pode permanecer: a DLL antiga ignora os novos campos.

## Único retorno necessário do PC do APK

1. **Conferir ambos na mesma R81:** recibo atual confirma Motorola R81 às19:32:55UTC; Samsung tem somente R78 comprovada. Preparar o Samsung preservando assinatura/licença/dados. Não recompilar apenas por cadastro de servidor. R80 Dreamcast LED segue arquivado para uso futuro, fora dessa atualização.
2. **Qualificar os controles do core/runtime entregue:** usar os vínculos exatos dos [três pilotos](recovery-r81-20261008/PERFIS-PILOTOS-NAO-APROVADOS.json). A produção deve preparar a execução controlada de Battletoads2 e de Bomberman Battle/Single Match com cada pad MAN. Registrar para cada combinação realmente exercitada: arquivo/hash, engine/core/runtime, perfil/hash, modo, contagem de jogadores e resultado de cada controle exclusivo. A qualificação física/local deve preceder a aprovação pública; se o ambiente não permite exercitar o perfil sem servidor ativo, informar esse bloqueio concreto para preparar conjuntamente um ambiente de teste, sem falsificar `approved` na produção.
3. **Conciliar a continuidade antes do gate global:** [auditoria](recovery-r81-20261008/CONTINUIDADE-LEGADO.json) enumera 9.080 chaves potenciais dos 1.816 itens visíveis com as dez engines; 1.816 rascunhos visíveis correspondem apenas ao runtime R76. As capacidades desses rascunhos são propostas. Aprovar somente as combinações efetivamente qualificadas, classificar modos individuais e resolver a cobertura dos títulos legados antes de ativar. Sete hashes de controlador não são evidência de gameplay de todos os títulos/runtimes.
4. **Depois das aprovações**, o servidor confere cobertura, republica somente Station quando vazio e comprova capabilities aprovadas pela rota pública. Então executar sala nova na R81 com Pronto/início, comandos exclusivos, pausa/retomada e saída. Com dois aparelhos, os testes de três/quatro continuam pendentes. Convites/códigos v3 continuam ausentes; a entrada implementada é pela lista/conversa.

Não enviar códigos de acesso, chaves, ROMs, BIOS, saves ou logs pessoais. Um resultado curto com a primeira etapa que falhou e horário é suficiente para continuar. Os manifestos de motor/controlador e o mapa exato já foram entregues; não solicitá-los novamente ao mantenedor.

## Preservação

Conferidos configuração/chaves originais, licenças reais, ledger e papel restrito do banco, saúde do painel e processos dos demais produtos. Nenhuma migration, instalação Android, troca de APK/assinatura/dados, alteração de ROM/capa, Nginx, Cloudflare, firewall, portas ou serviço de outro produto. Capacidade configurada v2 continua64 salas recuperáveis/32MiB de replay; centenas de jogadores e partidas prolongadas continuam sem homologação.
