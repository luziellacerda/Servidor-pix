# HANDOFF — RELAÇÃO EXATA DO BACKUP FUNCIONAL ATÉ O ESTADO ATUAL

Data: 2026-09-03

## Referências

- Backup/baseline: tag `prod-baseline-20260828T144059Z`, commit `c67b08963a70be7f3f7f5a64288467b4114a9cbf`.
- Backup histórico de documentação e painel: commit `724fa95acc7891a59c3ec6fb3a6c0939176e4901`.
- Estado atual do branch: commit `9f138d8` local; o último commit funcional de correção publicado é `63c112b`.
- Release atualmente executado: `r25-5-session-fix-20260903`.
- DLL publicado: SHA-256 `d6678e3ff26f0b5879450dfedfafeda0489fb8fc6131d386a0d0b644578be78d`.

## Mudanças do backup funcional até o baseline c67b089

1. Preservação de designs/scripts e baseline local.
2. Criação da autoridade isolada do TurboRama Suite.
3. Criação do schema transacional dedicado do Suite.
4. Testes de concorrência de heartbeat.
5. Licença vitalícia e vínculo único por máquina.
6. Rate limit e timeout de requisições.
7. Separação de provisionamento e emissão de OTP.
8. Leitura de segredos por arquivos protegidos.
9. Vinculação da ativação à identidade cadastrada.
10. Registro/verificação de migrations.
11. Rejeição de membros JSON obrigatórios ausentes.
12. Criação do painel/admin SUITE isolado.
13. Endurecimento do daemon administrativo e do socket Unix.
14. Serialização e proteção contra corridas na ativação/OTP.
15. Testes de indisponibilidade, replay e concorrência.
16. Proteção de cache/bfcache no painel.
17. Testes de integração, OpenAPI, threat model e runbook.

## Arquivos/camadas acrescentados ou modificados

- API SUITE: `Contracts.cs`, `Program.cs`, `Protocol.cs`, `RequestGuards.cs`, `Signing.cs`, `Store.cs`, `SuiteService.cs`.
- API PIX/BFF: `AdminPanel.cs`, `SuiteAdminBff.cs`, `SuiteAdminPanel.cs`.
- Admin isolado: `TurboRamaSuiteAdminServer/Program.cs`, `SuiteEligibility.cs` e projeto.
- Banco: migrations Suite 001–003 no baseline.
- Operação: unidades systemd, scripts de instalação/upgrade/rollback e exemplos de ambiente.
- Testes: Suite online, PostgreSQL, integração administrativa e browser.
- Design: `design-minimal`, `design-v3` e snapshots de painel.

## Mudanças posteriores ao baseline que impactaram o R25

No branch de servidor R25 foram acrescentados:

- catálogo/conteúdo protegido e permissões;
- gateway e grants de download;
- painel de clientes e projeções;
- suporte a conteúdo 902;
- presença online automática e polling;
- notificações WhatsApp por outbox;
- inventário de placa-mãe/BIOS/Windows;
- controle de versões e rate limit de inventário;
- aliases de configuração de autoridade/BFF;
- worker fixado no release imutável R25.1.

Essas mudanças ocorreram principalmente nos commits `e775aea`, `c1683a5`, `cb34249`, `9f5324f`, `8b3c91f`, `e6788ea`, `7f8dc4e` e `bbefbdf`.

## Estado atual e correções de 2026-09-03

Foram adicionados somente documentos de diagnóstico/retorno e a correção real:

- `021_suite_inventory_challenge_session_history.up.sql`;
- remoção da FK histórica `suite_device_inventory_challenges_session_id_fkey`;
- backup do schema antes da migration;
- release `r25-5-session-fix-20260903`;
- correção de permissões e atualização do drop-in do serviço.

A FK impedia a troca de `session_id` da sessão corrente porque challenges de inventário consumidos ainda referenciavam o ID histórico. Isso causava `POST /v1/suite/sessions -> 500` após `challenges -> 200`.

## O que não mudou

- autoridade/chaves públicas e privadas;
- TLS pins e certificados;
- ProductId e contratos `/v1`;
- licenças, pedidos, pagamentos e fingerprints;
- DeviceId e vínculo da máquina;
- validações criptográficas;
- lógica de OTP;
- serviços PIX, catálogo e gateway fora do reparo de sessão.

## Como comparar/reverter

Comparar o estado funcional com:

`git diff prod-baseline-20260828T144059Z..63c112b`

Para rollback binário, apontar o drop-in de `turborama-suite-api.service` ao release anterior, fazer `systemctl daemon-reload`, reiniciar e validar `/health` e `/ready`. Não remover o backup do schema.

## Aceite restante

O servidor está ativo, com health/readiness aprovados. O aceite final requer uma tentativa real do cliente para confirmar `challenges=200`, `sessions=200`, sessão ACTIVE, presença ONLINE e outbox idempotente.

