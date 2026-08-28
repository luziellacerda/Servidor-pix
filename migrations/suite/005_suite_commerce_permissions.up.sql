BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:005_commerce_permissions',0));

GRANT SELECT,INSERT ON suite.suite_commerce_inbox TO "turborama-suite-admin";
GRANT SELECT,INSERT ON suite.suite_license_deliveries TO "turborama-suite-admin";
GRANT UPDATE(provisioning_state,financial_state,last_source_version,
  administrative_resume_source_version,administrative_resume_actor,
  administrative_resume_reason,administrative_resume_request_id,updated_at)
  ON suite.suite_license_deliveries TO "turborama-suite-admin";
GRANT SELECT,INSERT ON suite.suite_lifecycle_commands TO "turborama-suite-admin";
GRANT SELECT,INSERT ON suite.suite_transfer_history TO "turborama-suite-admin";
GRANT USAGE,SELECT ON SEQUENCE suite.suite_transfer_history_transfer_id_seq TO "turborama-suite-admin";

GRANT UPDATE(status,revocation_generation,activation_generation,activation_verifier,
  activation_expires_at,activation_consumed,updated_at)
  ON suite.suite_licenses TO "turborama-suite-admin";
GRANT UPDATE(invalidated_at,invalidation_reason,consumed_at)
  ON suite.suite_challenges TO "turborama-suite-admin";
GRANT UPDATE(status,revoked_at,revocation_reason,authorized_until)
  ON suite.suite_sessions TO "turborama-suite-admin";
GRANT UPDATE(status,updated_at) ON suite.suite_devices TO "turborama-suite-admin";

GRANT SELECT ON suite.suite_license_deliveries TO "turborama-suite";

INSERT INTO suite.schema_migrations(version)
VALUES('005_suite_commerce_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
