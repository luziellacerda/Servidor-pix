BEGIN;
ALTER TABLE suite.suite_licenses
  ALTER COLUMN activation_verifier DROP NOT NULL,
  ALTER COLUMN activation_expires_at DROP NOT NULL,
  ADD COLUMN license_term varchar(16) NOT NULL DEFAULT 'LIFETIME',
  ADD COLUMN expires_at timestamptz NULL,
  ADD COLUMN identity_policy varchar(24) NOT NULL DEFAULT 'SOFTWARE_ONLY',
  ADD COLUMN maximum_active_devices smallint NOT NULL DEFAULT 1;
ALTER TABLE suite.suite_licenses
  ADD CONSTRAINT ck_suite_activation_pair CHECK ((activation_verifier IS NULL) = (activation_expires_at IS NULL)),
  ADD CONSTRAINT ck_suite_licenses_lifetime CHECK (license_term = 'LIFETIME' AND expires_at IS NULL),
  ADD CONSTRAINT ck_suite_licenses_identity_policy CHECK (identity_policy IN ('TPM_REQUIRED','TPM_PREFERRED','SOFTWARE_ONLY')),
  ADD CONSTRAINT ck_suite_licenses_single_device CHECK (maximum_active_devices = 1);
ALTER TABLE suite.suite_devices
  ADD COLUMN algorithm varchar(32) NOT NULL DEFAULT 'rsa-pss-sha256',
  ADD CONSTRAINT ck_suite_devices_algorithm CHECK (algorithm = 'rsa-pss-sha256');
CREATE UNIQUE INDEX ux_suite_devices_one_active_per_license ON suite.suite_devices(license_id) WHERE status = 'ACTIVE';
CREATE TABLE suite.suite_license_enrollments(
  license_id varchar(64) PRIMARY KEY REFERENCES suite.suite_licenses(license_id),
  device_id char(64) NOT NULL UNIQUE,
  binding_type varchar(32) NOT NULL CHECK(binding_type IN('TPM_BOUND','SOFTWARE_BOUND_ONLINE')),
  identity_policy varchar(24) NOT NULL CHECK(identity_policy IN('TPM_REQUIRED','TPM_PREFERRED','SOFTWARE_ONLY')),
  algorithm varchar(32) NOT NULL CHECK(algorithm='rsa-pss-sha256'),
  public_key_spki text NOT NULL,
  hardware_fingerprint char(64) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
COMMIT;
