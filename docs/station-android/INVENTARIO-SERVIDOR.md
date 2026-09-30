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
