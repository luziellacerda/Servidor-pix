BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:007_device_transfer_permissions',0));

GRANT DELETE ON suite.suite_license_enrollments TO "turborama-suite-admin";
GRANT UPDATE(enrollment_state) ON suite.suite_licenses TO "turborama-suite-admin";
GRANT UPDATE(status,completed_at) ON suite.suite_transfer_history TO "turborama-suite";

INSERT INTO suite.schema_migrations(version)
VALUES('007_suite_device_transfer_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
