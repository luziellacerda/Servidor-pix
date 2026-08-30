BEGIN;
REVOKE UPDATE(license_id) ON suite.suite_license_deliveries FROM "turborama-suite";
REVOKE SELECT ON suite.suite_license_deliveries FROM "turborama-suite";
DELETE FROM suite.schema_migrations WHERE version='017_suite_commerce_session_permissions';
COMMIT;
