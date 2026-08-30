BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:018_transfer_enrollment_lock',0));
GRANT UPDATE(license_id) ON suite.suite_license_enrollments TO "turborama-suite-admin";
INSERT INTO suite.schema_migrations(version) VALUES('018_suite_transfer_enrollment_lock') ON CONFLICT(version) DO NOTHING;
COMMIT;
