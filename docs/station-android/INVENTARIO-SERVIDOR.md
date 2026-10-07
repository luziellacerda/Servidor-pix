# Retornos R62–R66 analisados — 07/10/2026

Leia `docs/station-android/RETORNO-ANALISE-HANDOFFS-R62-R66-STATION-20261007.md` e seu recibo. APP→SERVIDOR recebido em7a5db185; app até7d0d3da/implementaçãoR66 291f3949. R63 foi instalada nos dois aparelhos; Samsung depoisR66/e4397fd7 com catálogo aberto, Motorola últimaR63/d9a35602. BIOS CD já existia no APK e a R66 liga a preparação automática aos assets. README/STATUS de preparação conservam história; recibos posteriores identificam instalação. Gameplay CD não capturado. Convite curto/senha automática já integrados.

Queda12:24:07UTC correlacionada: duas conexões abortadas pelo servidor,142s após início, antes do aviso do app; renovação200 às12:23:46. Gatilho inicial/heartbeat individual não provados. v1 ainda cancela ambos/Leave e não permite retomada; Q01–Q08 continuam exigindo implementação coordenada. Inventário das tabelas centrais de189ZIPs originais cruzado com TSV14/IDs/capas; bytes dos chips/pacotes ativos não verificados. NG-01–NG-07 seguem com essas limitações.

API da07355/PID1147382/isolamento preservados. ProteçãoAndroid213cfce derivaR57: conciliar comR62/R63/R64/R65/R66 antes de novo build; não executar empacotadorR57 sobre a sucessora. Preservar DEX30R66, salas/capas, controles/diagnóstico, runtime899e, alias/licença/saves/assinatura. RequireVerifiedApp=false. Análise/documentação apenas, sem nova implantação.

## Histórico da publicação de segurança

# Segurança Station publicada — 07/10/2026

Leia `docs/station-android/RETORNO-SEGURANCA-STATION-20261007.md` e seu recibo. APIda07355/DLL83c8d2b3/PID1147382; usuário e papel próprios, visões Station, arquivos/mídias somente para leitura, outros segredos inacessíveis, rotas Station só pelo túnel local. Backup/restauração,4921arquivos,198checksHTTPS+15segurança e2554relay passaram. Catálogo14/2212, licenças/chaves/quatro motores/outros serviços preservados. Origemdireta404; firewall/SSH/Cloudflare intactos. Migrations031/032 aditivas. Chave simétrica com acesso apenas ao proprietário preservada; gestão validada no socket privado,5187/health sem token404intencional.

App213cfce em versions/station-security-r57-20261007: prova por pedido/ticket, atestação opcional, R55+visualR57+prontidão+senha automática preservados;190Java8/API34 compilaram. NovoDEX28+35/APK/assinatura/instalação/hardware/gameplay pendentes no PC. RequireVerifiedApp=false preserva clientes antigos; não declarar acesso exclusivo ao APK nem segurança absoluta. Usar receitas novas sobreAPKR57/e6159fa3/cert7b16; atualizar ambos sem limpar dados. Retorno guardado exato da07355; scripts antigos recusam/ não abrangem a nova identidade.

## Histórico anterior

# Convites curtos e palavra passe automática — 06/10/2026

Leia `docs/station-android/RETORNO-CONVITE-CURTO-SENHA-AUTOMATICA-STATION-20261006.md`. APIa3e83d96/DLL5fff55c1/PID970425 publicada20h04Maceió. Convite8caracteres, resolução autenticada/assinada sem senha ou entrada implícita; quatro motores no registro, dois originais e dois -autopass1. Runtime899e3527 arm64/API26/16KiB compilado, cores/opções/controles preservados. Backup restaurado, provas sombra/HTTPS/relay e limpeza passaram; catálogo14/2212, licenças, chaves, serviços e mídias preservados. Sem migration/mensagens.

App1e0f862 em versions/station-auto-room-access-r57-20261006 inclui R55+visualR57+prontidãof8. Senha privada usada automaticamente só no cliente Android tipado, NICK antes de PASSWORD e verificador original preservado. 318códigos,278salas/convitesJVM,66nativo,39TCP/TLS/relay,158JavaAPI34 passaram. APK/DEX novo, assinatura/instalação e gameplay físico pendentes no PC; usar receitas novas sobre APKR57/e6159fa3/cert7b16 e atualizar ambos. Não executar receitas R41/R55 nem declarar o diálogo do APK antigo corrigido pelo servidor.

## Histórico anterior

# Cadastro de clientes Station publicado — 06/10/2026

Leia RETORNO-CADASTRO-CLIENTES-STATION-20261006.md. Fontefe4b631 publicada às15h45Maceió: Códigos Station → Novo cliente e código, cliente novo/existente, venda paga/cortesia/teste e licença adicional para dois aparelhos. Código30min/uso único; confirmação com senha administrativa. PostgreSQL/SQLite restaurados, testes isolados e dois acessos sintéticos independentes na API pública passaram, limpeza confirmada, zero mensagens/compras. ManagementPID910766/helperPID910776; APIa2bb176/PID875574/catálogo14/2212 e APKR41 preservados. Sem migrationPG; tabelaSQLite aditiva station_registrations. Nova página cadastra; orientação antiga somente de busca/Vendas foi substituída. Usar retorno específico da sucessora; gameplay físico/POCO/latência continuam no retornoR41.

## Histórico anterior — consultar o cadastro publicado acima

# Códigos Station no painel — 06/10/2026

Leia RETORNO-PAINEL-CODIGOS-STATION-20261006.md. Interface17e564a publicada às14h04Maceió: menu Códigos Station, atalho Gerar código do app Station, guia e botões Gerar código/Trocar celular. HTTPS autenticado e hashes dos nove arquivos conferidos; testes sintéticos de emissão/senha/CSRF/troca/cancelamento passaram. Código30min/uso único, um aparelho por licença; novo aparelho simultâneo exige licença própria. Nenhuma licença real alterada, zero WhatsApp, migrations e reinícios. API/comunidadeR41 e APK permanecem os do retorno abaixo.

# Comunidade R41 publicada — 06/10/2026

Leia RETORNO-COMUNIDADE-STATION-R41-20261006.md. API a2bb176530fd4d2dfa740da7e934fd84d097404e, DLL d181bf97d5b39a334e95144267d6ece3f11d4e659a314d7d16cd2746e1999e13, PID875574; SocialEnabled=true, conversas privadas/pedidos de entrada verificados por três licenças sintéticas no domínio público, 186 checks e WSS com pin. Catálogo14/2212, relay512/1024 e licenças/chaves/outros serviços preservados. Histórico privado até32 e64KiB na resposta. Backup restaurado, nenhuma migration. Retorno Android 5e40f7e confirma R41 instalada no Samsung, hashb6b19321 e dados preservados. POCO, gameplay em dupla, Pessoas/Voltar/correspondência Boogerman–Battletoads e latência externa continuam pendentes. Preservar APK R41; nunca retomar delta R34 sobre essa fonte. Scripts históricos recusam sucessoras; usar retorno/rollback R41.

## Histórico anterior — os blocos abaixo não identificam a publicação atual

# Inventário do servidor para integração Android

Levantamento somente de leitura de 30/09/2026, aproximadamente 18h45, fuso America/Maceio. Este documento registra o que foi observado no Linux e o que continua sem confirmação. Use o script de inventário e confira novamente antes de qualquer alteração.

## Funções e dependências compartilhadas

O servidor mantém vários sistemas. `nginx`, `cloudflared`, PostgreSQL 16, Redis, PHP-FPM, o serviço dedicado `turbobox-php-fpm`, MariaDB, serviços LZ Games IA e os serviços Turborama aparecem no inventário. Nginx atende também domínios de suporte, ponto, API e site; por isso uma edição global de proxy ou reinício geral tem alcance maior que o produto Android. O PostgreSQL 16 está online em `127.0.0.1:5432`; Redis escuta em loopback na porta `6379`. O serviço PIX usa também estado e configuração próprios, distintos das tabelas Suite.

O disco raiz ext4 tinha 21 GiB livres, 90% ocupado. `/mnt/DADOS`, também ext4, tinha 115 GiB livres, 71% ocupado. Use armazenamento de trabalho em `/mnt/DADOS` para compilar e testar, sem confundir espaço livre com autorização para apagar caches ou backups existentes.

## Serviços Turborama observados

`systemctl show` foi usado para ler o `ExecStart` e o `WorkingDirectory` efetivos, incluindo drop-ins. O diretório de trabalho e o arquivo executado não são necessariamente da mesma release. Os hashes abaixo são dos DLLs apontados pelo `ExecStart`, não dos arquivos homônimos nos caminhos base em `/opt`.

| Serviço | Estado em 30/09 | DLL efetiva do ExecStart | WorkingDirectory efetivo |
| --- | --- | --- | --- |
| `turborama-pix.service` | ativo | `/opt/turborama-suite-r5-releases/es-suite-34e31f2-20260905/pix-admin/TurboRamaPixOnlineServer.dll` | `/opt/turborama-suite-r5-releases/r25-1-inventory-whatsapp-20260902/pix` |
| `turborama-suite-api.service` | ativo | `/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908/server/TurboRamaSuiteOnlineServer.dll` | `/opt/turborama-suite-r5-releases/r5-916-presence-15s-20260902/api` |
| `turborama-suite-admin.service` | ativo | `/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908/admin-backend/TurboRamaSuiteAdminServer.dll` | `/opt/turborama-suite-r5-releases/r25-2-admin-reader-fix-20260902/admin` |
| `turborama-suite-content-gateway.service` | ativo | `/opt/turborama-suite-r5-releases/r5-908-gateway-policy-20260830/gateway/TurboRamaSuiteContentGateway.dll` | Mesmo diretório da DLL |

SHA256 das quatro DLLs efetivas, na ordem da tabela:

```text
PIX:     f84f3d288f94acb95d957b78e47c71f2903ef1edf5fd840f85deb5a67c4e0b06
API:     93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1
Admin:   2588bcd29f7605c96199ed05bf7d9e057fd097a3087c56c94a52077bebf0a729
Gateway: 34be13155cff5b81ad09b6c0c6c4be6afa8a40d20095bbc053667a7f7ad6dc2a
```

As consultas GET locais a `127.0.0.1:5187/v1/health`, `127.0.0.1:5190/health` e `127.0.0.1:5191/health` responderam HTTP `200`. Não foi feita compra, ativação, sessão autenticada ou chamada de download real. `turborama-suite-content-monitor.service` já aparecia como `failed` antes deste trabalho; a falha não foi diagnosticada ou corrigida aqui. Timers de extração, projeção de clientes, notificações, candidatos, limpeza e monitoramento estavam presentes.

## Código e migrations

O [handoff Android original](https://github.com/luziellacerda/TurboElden/blob/f7887438e41107e73ed32ddeaf634f998b708ded/docs/server/HANDOFF-TURBORAMASTATION-ANDROID-20260930.md) analisou `Servidor-pix` no commit `17af26cc8e1aa88edfaef0a4e25ab598e4f682e6` e migrations até `026`. O repositório `Servidor-pix` é privado; a branch `main` histórica não contém necessariamente toda a integração Suite. Esse commit é uma referência de estudo, não uma identificação do binário ativo.

O worktree local `/home/lz-servidor/worktrees/servidor-pix-content-prod` estava em `bbefbdf3967c7a4cf900984b27e764d66e33c000`. Quatro arquivos rastreados tinham modificações locais: `AdminPanel.cs`, `SuiteAdminBff.cs`, `SuiteAdminPanel.cs` e `TurboRamaSuiteAdminServer/Program.cs`. Seu diretório `migrations/suite` contém arquivos até `020`.

O worktree `/home/lz-servidor/worktrees/servidor-pix-all-downloads-20260908` estava em `bb87d18`, com arquivos de migration até `027`. Um terceiro worktree, `/home/lz-servidor/worktrees/turborama-suite-producao-real-r2`, contém dois arquivos `010_suite_transfer_completion_permissions` não rastreados. Esses estados pertencem aos projetos em andamento; não os descarte nem misture automaticamente.

**Pendente:** ler, por conexão autorizada e sem expor o DSN, o ledger real `suite.schema_migrations`, as constraints atuais de produto/SKU e as grants. A existência de `027` no código não informa se `027` foi aplicada. Também falta vincular cada DLL efetiva a seu commit de fonte ou manifesto de build e inventariar o produtor confiável do cadastro comercial usado pelo painel.

## Estado do aplicativo Android

O repositório público [TurboElden](https://github.com/luziellacerda/TurboElden) tinha a branch `versao-funcional` em `40d9adc53d4a738cbd2210826a51c6e3f1aab1cb` neste levantamento. A [última atualização documentada](https://github.com/luziellacerda/TurboElden/blob/versao-funcional/versions/atualizacao-2026-09-30-videos-arcade/HANDOFF.md) é o APK de vídeos Arcade/FBNeo/MAME, SHA256 `1189899e780cd372e8e0efdbea7dad2926ecccf896542c3349d1b79caa8ac2c7`, instalado com hash conferido no telefone segundo aquele handoff. Seu login remoto ainda não está implementado. A tag estável anterior `estavel-2026-09-30-plataformas-emuladores` aponta para `05dd34b0ec067e8951670422fc0cbf2fa851a3d0`; o APK estável de recuperação tem hash `78accf4c2e0c7b5c786acb0ea7a187754a53253beb2c51a01fc40f5a18c649f2`.

## Fontes a ler antes de construir

- Handoff Android: contrato proposto, comercial, sessão e limites de aparelho.
- `src/TurboRamaSuiteOnlineServer/`: ativação, prova, sessão, catálogo e autorização de download existentes.
- `src/TurboRamaSuiteAdminServer/`: comércio, emissão, transferências, permissões e auditoria.
- `src/TurboRamaPixOnlineServer/`: painel e BFF que atendem clientes existentes.
- `src/TurboRamaSuiteContentGateway/`: concessões e entrega de conteúdo.
- `migrations/suite/`: schema, CHECKs de produto/SKU, índices e grants.
- `tests/`: testes de protocolo, PostgreSQL, comércio, EmulationStation e conteúdo.
- `ops/production/`: exemplos e scripts históricos; conferir cada caminho antes de usar.

## O que este levantamento não confirmou

Não foi lido o conteúdo de `.env`, estado de clientes, banco de produção, chaves, catálogo privado, pedidos, sessões ou logs com dados pessoais. Não foi estabelecida correspondência completa entre os quatro DLLs ativos e um único commit Git. Não foi confirmado SKU, preço, prazo, limite comercial de aparelhos, domínio final ou política de revogação Android. Não foi feita verificação funcional de todos os sistemas deste servidor; os estados acima são a linha de base para evitar impacto involuntário.
