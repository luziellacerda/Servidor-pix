BEGIN;
REVOKE UPDATE(license_id) ON suite.suite_license_enrollments FROM "turborama-suite-admin";
DELETE FROM suite.schema_migrations WHERE version='018_suite_transfer_enrollment_lock';
COMMIT;
