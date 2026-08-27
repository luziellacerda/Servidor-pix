BEGIN;
ALTER TABLE suite.suite_licenses
  ADD COLUMN license_term varchar(16) NOT NULL DEFAULT 'LIFETIME',
  ADD COLUMN expires_at timestamptz NULL,
  ADD COLUMN identity_policy varchar(24) NOT NULL DEFAULT 'SOFTWARE_ONLY',
  ADD COLUMN maximum_active_devices smallint NOT NULL DEFAULT 1;
ALTER TABLE suite.suite_licenses
  ADD CONSTRAINT ck_suite_licenses_lifetime CHECK (license_term = 'LIFETIME' AND expires_at IS NULL),
  ADD CONSTRAINT ck_suite_licenses_identity_policy CHECK (identity_policy IN ('TPM_REQUIRED','TPM_PREFERRED','SOFTWARE_ONLY')),
  ADD CONSTRAINT ck_suite_licenses_single_device CHECK (maximum_active_devices = 1);
ALTER TABLE suite.suite_devices
  ADD COLUMN algorithm varchar(32) NOT NULL DEFAULT 'rsa-pss-sha256',
  ADD CONSTRAINT ck_suite_devices_algorithm CHECK (algorithm = 'rsa-pss-sha256');
CREATE UNIQUE INDEX ux_suite_devices_one_active_per_license ON suite.suite_devices(license_id) WHERE status = 'ACTIVE';
COMMIT;
