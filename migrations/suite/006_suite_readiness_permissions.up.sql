BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:006_readiness_permissions',0));

GRANT USAGE ON SCHEMA suite TO "turborama-suite-admin";
GRANT SELECT ON suite.schema_migrations TO "turborama-suite-admin";

INSERT INTO suite.schema_migrations(version)
VALUES('006_suite_readiness_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
