BEGIN;
REVOKE DELETE ON suite.suite_license_enrollments FROM "turborama-suite-admin";
REVOKE UPDATE(enrollment_state) ON suite.suite_licenses FROM "turborama-suite-admin";
REVOKE UPDATE(status,completed_at) ON suite.suite_transfer_history FROM "turborama-suite";
DELETE FROM suite.schema_migrations WHERE version='007_suite_device_transfer_permissions';
COMMIT;
