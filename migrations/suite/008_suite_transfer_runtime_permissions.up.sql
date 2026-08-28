BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:008_transfer_runtime_permissions',0));

GRANT SELECT ON suite.suite_lifecycle_commands,suite.suite_license_deliveries,
  suite.suite_licenses,suite.suite_license_enrollments TO "turborama-suite-admin";
GRANT INSERT ON suite.suite_lifecycle_commands,suite.suite_transfer_history,
  suite.suite_audit_events TO "turborama-suite-admin";
GRANT USAGE,SELECT ON SEQUENCE suite.suite_transfer_history_transfer_id_seq,
  suite.suite_audit_events_event_id_seq TO "turborama-suite-admin";

INSERT INTO suite.schema_migrations(version)
VALUES('008_suite_transfer_runtime_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
