BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:017_commerce_session_permissions',0));

GRANT SELECT ON suite.suite_license_deliveries TO "turborama-suite";
GRANT UPDATE(license_id) ON suite.suite_license_deliveries TO "turborama-suite";

INSERT INTO suite.schema_migrations(version)
VALUES('017_suite_commerce_session_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
