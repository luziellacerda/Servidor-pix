BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:023_es_contract_scope',0));

-- The two ES adapters share persistence, but a proof issued for the shared
-- contract must not be consumable through the legacy replacement policy.
-- The default preserves old binaries and outstanding dedicated-client proofs.
ALTER TABLE suite.suite_es_challenges ADD COLUMN client_contract varchar(16)
  NOT NULL DEFAULT 'DEDICATED_V1'
  CHECK(client_contract IN ('DEDICATED_V1','SHARED_V1'));

INSERT INTO suite.schema_migrations(version) VALUES('023_suite_es_contract_scope');
COMMIT;
