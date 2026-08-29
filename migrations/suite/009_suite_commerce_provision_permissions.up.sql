BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:009_commerce_provision_permissions',0));

-- SELECT ... FOR UPDATE on the immutable inbox requires UPDATE at table level.
-- Runtime code never issues an UPDATE against this table.
GRANT UPDATE ON suite.suite_commerce_inbox TO "turborama-suite-admin";

-- The commerce receiver creates a new license only after an idempotent paid event.
GRANT INSERT(license_id,product_id,status,activation_verifier,activation_expires_at,
  activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices,
  provisioning_origin,enrollment_state,claim_mode)
  ON suite.suite_licenses TO "turborama-suite-admin";

INSERT INTO suite.schema_migrations(version)
VALUES('009_suite_commerce_provision_permissions')
ON CONFLICT(version) DO NOTHING;
COMMIT;
