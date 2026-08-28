BEGIN;
REVOKE SELECT ON suite.schema_migrations FROM "turborama-suite-admin";
REVOKE USAGE ON SCHEMA suite FROM "turborama-suite-admin";
DELETE FROM suite.schema_migrations WHERE version='006_suite_readiness_permissions';
COMMIT;
