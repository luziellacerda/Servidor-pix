BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:019_admin_customer_activity',0));
GRANT SELECT ON suite.suite_content_grants,suite.suite_content_items TO "turborama-suite-admin";
GRANT DELETE ON suite.suite_content_grants TO "turborama-suite-admin";
INSERT INTO suite.schema_migrations(version) VALUES('019_suite_admin_customer_activity')
ON CONFLICT(version) DO NOTHING;
COMMIT;
