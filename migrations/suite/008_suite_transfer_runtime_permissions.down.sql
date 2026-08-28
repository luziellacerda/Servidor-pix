BEGIN;
REVOKE INSERT ON suite.suite_lifecycle_commands,suite.suite_transfer_history FROM "turborama-suite-admin";
REVOKE USAGE,SELECT ON SEQUENCE suite.suite_transfer_history_transfer_id_seq FROM "turborama-suite-admin";
DELETE FROM suite.schema_migrations WHERE version='008_suite_transfer_runtime_permissions';
COMMIT;
