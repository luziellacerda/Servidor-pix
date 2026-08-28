# Recovery Gate, PITR e Journal Externo — configuração preparada, não aplicada

Status: `OFF` por padrão. Este documento não autoriza alteração de produção.

## Interlocks obrigatórios

As APIs, o admin e os workers devem negar autorização quando `SUITE_RESTORE_GATE_ENABLED` não for `1`, quando o marker externo estiver ausente/ilegível, quando `database_incarnation` ou `restore_epoch` divergirem, ou quando `applied_journal_sequence` não for igual ao high-water externo linearizável. O banco restaurado nunca abre o gate.

Configuração externa esperada: `SUITE_RESTORE_GATE_ENABLED`, `SUITE_RESTORE_GATE_URI`, `SUITE_RESTORE_GATE_PUBLIC_KEY_FILE`, `SUITE_EXTERNAL_JOURNAL_ENABLED`, `SUITE_EXTERNAL_JOURNAL_URI` e `SUITE_EXTERNAL_JOURNAL_CREDENTIAL_FILE`. Valores e credenciais ficam fora do Git.

## PITR do cluster inteiro

Preparar armazenamento off-host criptografado autenticado para o cluster PostgreSQL completo, incluindo globals, todos os databases, tablespaces, extensões e WAL de toda a retenção. Capacidade mínima: `1,25 × (soma das gerações de base backup retidas + 2 × pico WAL observado × retenção)`. Monitorar `pg_stat_archiver`, idade do último WAL arquivado e crescimento de `pg_wal`.

Mudança futura, em janela aprovada: habilitar `archive_mode=on`, configurar `archive_command` para o sink autenticado, reiniciar o cluster compartilhado, verificar continuidade e executar restore em outro host/porta/PGDATA/rede. Rollback: fechar gate antes de qualquer ação; reverter config somente após confirmar que nenhum WAL necessário ficará órfão. Nunca substituir o PGDATA produtivo, usar `pg_rewind` ou promover o clone de ensaio.

## Ensaio de restore

1. Fechar externamente o gate e rotacionar `restore_epoch`.
2. Restaurar em host/porta/PGDATA isolados com proteção equivalente à produção.
3. Reproduzir journal e outbox; conferir sequências, gerações e cadeia.
4. Invalidar OTPs/challenges e revogar sessões; reconciliar devices, enrollments e financeiro.
5. Persistir o novo epoch no banco ainda com gate fechado.
6. Executar invariantes e provar zero divergência.
7. Abrir manualmente o interlock externo com registro auditado.

## Journal externo

O backend deve ser append-only/WORM, fora do domínio de falha PostgreSQL, com ACL própria, unicidade `(license_id, sequence)` e `(scope, request_id)`, conditional append por sequência/geração, hash-chain/checkpoint assinado, replicação, backup e restore ensaiado. Timeout incerto é resolvido por consulta de `request_id`; nunca por repetição cega. Append falho impede mutação; append confirmado e banco falho deixa replay pendente e bloqueia ALLOW. OTP, verifier, proof, token, SPKI, fingerprint, PII e valores são proibidos no journal.

## Decisões pendentes do proprietário

Aprovar backend off-host, retenção/RPO/RTO, chave pública de backup, custódios das chaves privadas, monitoramento, janela de restart e teste de restore. Até isso ocorrer, backend do journal, restore gate, worker, SKU e implantação permanecem OFF.
