# HANDOFF — TUTORIAL COMPLETO DO SERVIDOR TURBORAMA SUITE

Data: 2026-09-03

Documento operacional ponta a ponta. Nunca inclua senhas, tokens, OTPs, DSNs completos ou chaves privadas.

## Arquitetura

- `turborama-pix.service`: API PIX, licenciamento e painel PIX.
- `turborama-suite-api.service`: API online SUITE, licenças, sessões, inventário e catálogo.
- `turborama-suite-admin.service`: painel SUITE, permissões, OTP e auditoria.
- `turborama-suite-content-gateway.service`: gateway de conteúdo protegido.
- workers de presença/WhatsApp: consomem a outbox de notificações.
- Nginx: HTTPS, proxy e domínios públicos.
- PostgreSQL: fonte transacional de licenças, máquinas, sessões e vendas.
- GitHub: código e documentação; segredos ficam fora.

## Repositório

`https://github.com/luziellacerda/Servidor-pix`

Estrutura:

- `src/TurboRamaPixOnlineServer`: PIX/painel.
- `src/TurboRamaSuiteOnlineServer`: API SUITE.
- `src/TurboRamaSuiteAdminServer`: BFF/painel isolado.
- `migrations/suite`: schema e grants.
- `ops/production`: serviços e exemplos.
- `tests`: protocolos, banco e integração.

Comandos: `git fetch --all --prune`, `git log -1 --oneline`, `git status --short`.

## Serviços e releases

Use `systemctl cat <serviço>` para descobrir EnvironmentFile, WorkingDirectory, ExecStart e drop-ins. O drop-in pode sobrescrever o release padrão.

Verifique: `systemctl status turborama-pix turborama-suite-api turborama-suite-admin turborama-suite-content-gateway`.

Antes de publicar, registre commit e diretório exatos; não presuma que o código-fonte e o DLL ativo são iguais.

## Segredos

Ambientes ficam em `/etc/turborama-suite/` e `/etc/turborama-pix/`, com permissões restritas. Nunca imprima env completo, DSN, pepper, OTP ou chaves. Chaves privadas de autoridade permanecem offline; o servidor recebe apenas artefatos públicos.

## PostgreSQL e migrations

Confirme o banco real usado pelo serviço, sem mostrar o DSN. Confira migrations e tabelas:

```sql
SELECT current_database(), current_user;
SELECT version FROM suite.schema_migrations ORDER BY version;
SELECT to_regclass('suite.suite_licenses'), to_regclass('suite.suite_devices'),
       to_regclass('suite.suite_sessions'),
       to_regclass('suite.suite_device_presence'),
       to_regclass('suite.suite_connection_notification_outbox');
```

Migrations devem ser aplicadas no banco efetivo. Em caso de drift, faça backup/metadados, aplique a migration oficial e registre; nunca contorne autorização editando tabelas sem controle.

## Licenciamento

Fluxo: venda/pedido -> licença -> pagamento/entrega -> emissão de código/OTP -> descriptor da máquina -> challenge -> prova criptográfica -> dispositivo ativo -> sessão/heartbeat.

Uma licença válida mantém produto correto, status ACTIVE, termo/política compatíveis, limite de dispositivos e estados financeiro/entrega coerentes. Troca da autoridade de conteúdo não exige recriar licenças.

## Identidade da máquina

O DeviceId deriva da chave pública da máquina. O HardwareFingerprint inclui o inventário local, como placa-mãe e BIOS. O servidor compara LicenseId, DeviceId, PublicKeySpki, HardwareFingerprint, BindingType e Algorithm. Atualizações devem preservar o armazenamento local; não gerar nova identidade para a mesma instalação.

## Sessão

O cliente chama `POST /v1/suite/challenges` e depois `POST /v1/suite/sessions`.

- challenge válido: 200;
- sessão válida: 200;
- licença/dispositivo recusado: 403;
- challenge expirado/conflito: 409;
- exceção interna: 500 sanitizado, com detalhes apenas no log e correlação.

O fechamento grava sessão, presença online, outbox de conexão e consumo do challenge dentro de transação serializável. Não remover etapas para mascarar erros.

## Conteúdo e autoridades

A autoridade de conteúdo é independente da autoridade de licenciamento. A chave privada do emissor fica offline. O cliente valida envelope, assinatura, validade, domínio HTTPS e pin TLS. Downloads usam grants/links temporários emitidos após autorização. Rotação ocorre uma vez por emissor, não por PC; os quatro artefatos públicos podem ser distribuídos ao cliente e servidor conforme o pacote.

## Painéis

PIX e SUITE são painéis separados. O SUITE usa claims/permissões, antiforgery, senha de elevação, OTP de uso único e auditoria. Operações sensíveis (emitir OTP, mudar licença/dispositivo, forçar reautenticação, exportar auditoria) devem ser auditadas e nunca exibir OTP novamente.

## Presença e WhatsApp

Sessão válida atualiza `suite_device_presence` e, respeitando cooldown/idempotência, cria `device.connected` em `suite_connection_notification_outbox`. O worker envia a notificação. Falha de WhatsApp não deve invalidar a licença nem a sessão.

## Deploy seguro

1. revisar diff, branch e commit;
2. criar backup/manifesto do release atual;
3. compilar em diretório novo;
4. executar testes unitários, integração e E2E;
5. instalar atomicamente sem apagar o anterior;
6. atualizar drop-in, fazer daemon-reload e reiniciar;
7. verificar health/readiness;
8. testar challenge 200 + sessão 200;
9. confirmar presença/outbox sem duplicação;
10. registrar release, hashes e rollback.

Health sozinho não comprova licenciamento.

## Diagnóstico

Use `systemctl status` e `journalctl -u <serviço>`. Interpretação: sem request = proxy/rede; challenge 403 = licença/enrollment/identidade; challenge 200 + sessão 500 = exceção interna, banco, trigger ou release divergente; sessão 409 = replay/expiração/conflito; sessão 200 sem presença = fluxo de presença/outbox.

Guarde correlação, horário, endpoint e status; nunca corpos com segredos. O log interno deve usar `LogError(ex, ...)`, mantendo resposta sanitizada.

## Backup e rollback

Antes do deploy salve commit, hashes, drop-ins, migrations, backup do banco e health. Para rollback, aponte ao release anterior, recarregue systemd, reinicie e repita health e teste seguro. Mantenha o release anterior até o aceite.

## Aceite

Testar health, licença ativa/negada, DeviceId correto/incorreto, activation, sessão/heartbeat, replay, presença idempotente, outbox, catálogo assinado, download temporário, painel/OTP/auditoria e ausência de segredos em logs.

## Restrições

Não recriar licença por erro de sessão; não trocar fingerprint; não desabilitar validações; não colocar chave privada no servidor/Git/chat; não publicar links permanentes; não retornar SQL/stack trace ao cliente; não declarar sucesso apenas pelo status ACTIVE.

## Retorno obrigatório

Entregar um único relatório com causa-raiz, commit, release implantado, migrations/grants, correlações, testes 200/200, estado de sessão/presença, hashes, backup e rollback — sem segredos.

