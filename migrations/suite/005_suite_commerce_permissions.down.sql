BEGIN;
REVOKE SELECT,INSERT ON suite.suite_commerce_inbox,suite.suite_license_deliveries,
  suite.suite_lifecycle_commands,suite.suite_transfer_history FROM "turborama-suite-admin";
REVOKE UPDATE(provisioning_state,financial_state,last_source_version,
  administrative_resume_source_version,administrative_resume_actor,
  administrative_resume_reason,administrative_resume_request_id,updated_at)
  ON suite.suite_license_deliveries FROM "turborama-suite-admin";
REVOKE USAGE,SELECT ON SEQUENCE suite.suite_transfer_history_transfer_id_seq FROM "turborama-suite-admin";
REVOKE UPDATE(status,revocation_generation,activation_generation,activation_verifier,
  activation_expires_at,activation_consumed,updated_at)
  ON suite.suite_licenses FROM "turborama-suite-admin";
REVOKE UPDATE(invalidated_at,invalidation_reason,consumed_at)
  ON suite.suite_challenges FROM "turborama-suite-admin";
REVOKE UPDATE(status,revoked_at,revocation_reason,authorized_until)
  ON suite.suite_sessions FROM "turborama-suite-admin";
REVOKE UPDATE(status,updated_at) ON suite.suite_devices FROM "turborama-suite-admin";
REVOKE SELECT ON suite.suite_license_deliveries FROM "turborama-suite";
DELETE FROM suite.schema_migrations WHERE version='005_suite_commerce_permissions';
COMMIT;
