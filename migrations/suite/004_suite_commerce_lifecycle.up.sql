BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:004_commerce_lifecycle',0));

DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM suite.suite_licenses l
    WHERE (EXISTS (SELECT 1 FROM suite.suite_devices d WHERE d.license_id=l.license_id AND d.status='ACTIVE')
       AND NOT EXISTS (SELECT 1 FROM suite.suite_license_enrollments e WHERE e.license_id=l.license_id))
       OR (SELECT count(*) FROM suite.suite_devices d WHERE d.license_id=l.license_id AND d.status='ACTIVE') > 1
  ) THEN
    RAISE EXCEPTION 'SUITE_LEGACY_STATE_INCONSISTENT';
  END IF;
END $$;

ALTER TABLE suite.suite_licenses
  ADD COLUMN provisioning_origin varchar(24) DEFAULT 'LEGACY_ADMIN',
  ADD COLUMN enrollment_state varchar(40) DEFAULT 'PENDING_ENROLLMENT',
  ADD COLUMN claim_mode varchar(24) DEFAULT 'PREBOUND',
  ADD COLUMN activation_generation bigint NOT NULL DEFAULT 0,
  ADD COLUMN applied_journal_sequence bigint NOT NULL DEFAULT 0;

UPDATE suite.suite_licenses l SET
  provisioning_origin='LEGACY_ADMIN',
  claim_mode='PREBOUND',
  enrollment_state=CASE
    WHEN EXISTS (SELECT 1 FROM suite.suite_devices d WHERE d.license_id=l.license_id AND d.status='ACTIVE') THEN 'BOUND'
    WHEN EXISTS (SELECT 1 FROM suite.suite_license_enrollments e WHERE e.license_id=l.license_id) THEN 'PREBOUND_PENDING_ACTIVATION'
    ELSE 'PENDING_ENROLLMENT'
  END;

ALTER TABLE suite.suite_licenses
  ALTER COLUMN provisioning_origin SET NOT NULL,
  ALTER COLUMN enrollment_state SET NOT NULL,
  ALTER COLUMN claim_mode SET NOT NULL,
  ADD CONSTRAINT ck_suite_provisioning_origin CHECK(provisioning_origin IN('COMMERCE','LEGACY_ADMIN')),
  ADD CONSTRAINT ck_suite_enrollment_state CHECK(enrollment_state IN('PENDING_ENROLLMENT','PREBOUND_PENDING_ACTIVATION','BOUND')),
  ADD CONSTRAINT ck_suite_claim_mode CHECK(claim_mode IN('FIRST_CLAIM','PREBOUND')),
  ADD CONSTRAINT ck_suite_activation_generation CHECK(activation_generation>=0),
  ADD CONSTRAINT ck_suite_applied_journal_sequence CHECK(applied_journal_sequence>=0);

CREATE TABLE suite.suite_commerce_inbox(
  source_system varchar(48) NOT NULL,
  source_event_id char(32) NOT NULL,
  source_purchase_id varchar(64) NOT NULL,
  source_item_key varchar(32) NOT NULL,
  source_version bigint NOT NULL CHECK(source_version>0),
  source_product_sku varchar(64) NOT NULL CHECK(source_product_sku='SUITE_LIFETIME_1_DEVICE'),
  event_type varchar(32) NOT NULL CHECK(event_type IN('PURCHASE_PAID','PURCHASE_SUSPENDED')),
  payload_digest char(64) NOT NULL,
  received_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  processed_at timestamptz,
  outcome varchar(24),
  detail_code varchar(64),
  result_json jsonb,
  PRIMARY KEY(source_system,source_event_id),
  UNIQUE(source_system,source_purchase_id,source_item_key,source_product_sku,source_version)
);

CREATE TABLE suite.suite_license_deliveries(
  source_system varchar(48) NOT NULL,
  source_purchase_id varchar(64) NOT NULL,
  source_item_key varchar(32) NOT NULL,
  source_product_sku varchar(64) NOT NULL CHECK(source_product_sku='SUITE_LIFETIME_1_DEVICE'),
  product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  license_id varchar(64) UNIQUE REFERENCES suite.suite_licenses(license_id),
  provisioning_state varchar(32) NOT NULL CHECK(provisioning_state IN('TOMBSTONE','PROVISIONED','SUSPENDED','REVOKED')),
  financial_state varchar(24) NOT NULL CHECK(financial_state IN('PAID','SUSPENDED')),
  last_source_version bigint NOT NULL CHECK(last_source_version>0),
  administrative_resume_source_version bigint,
  administrative_resume_actor varchar(128),
  administrative_resume_reason varchar(256),
  administrative_resume_request_id varchar(128),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(source_system,source_purchase_id,source_item_key,product_id),
  CHECK((financial_state='SUSPENDED') OR administrative_resume_source_version IS NULL),
  CHECK(administrative_resume_source_version IS NULL OR administrative_resume_source_version=last_source_version)
);
CREATE INDEX ix_suite_deliveries_state ON suite.suite_license_deliveries(provisioning_state,financial_state,updated_at DESC);

CREATE TABLE suite.suite_lifecycle_commands(
  scope varchar(32) NOT NULL,
  request_id varchar(128) NOT NULL,
  request_digest char(64) NOT NULL,
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  action varchar(32) NOT NULL CHECK(action IN('SUSPEND','RESUME','REVOKE','TRANSFER','FORCE_REAUTH')),
  expected_generation bigint NOT NULL,
  resulting_generation bigint,
  actor varchar(128) NOT NULL,
  reason varchar(256) NOT NULL,
  outcome varchar(24) NOT NULL,
  result_json jsonb,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(scope,request_id)
);

CREATE TABLE suite.suite_transfer_history(
  transfer_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  request_id varchar(128) NOT NULL UNIQUE,
  request_digest char(64) NOT NULL,
  previous_device_id char(64),
  previous_enrollment_json jsonb,
  revocation_generation bigint NOT NULL,
  activation_generation bigint NOT NULL,
  status varchar(24) NOT NULL CHECK(status IN('PENDING','COMPLETED','CANCELLED')),
  actor varchar(128) NOT NULL,
  reason varchar(256) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  completed_at timestamptz
);

ALTER TABLE suite.suite_challenges
  ADD COLUMN revocation_generation bigint NOT NULL DEFAULT 0,
  ADD COLUMN activation_generation bigint,
  ADD COLUMN invalidated_at timestamptz,
  ADD COLUMN invalidation_reason varchar(64);

UPDATE suite.suite_challenges c
SET activation_generation=l.activation_generation
FROM suite.suite_licenses l
WHERE c.license_id=l.license_id AND c.action='device.activate';

ALTER TABLE suite.suite_challenges
  ADD CONSTRAINT ck_suite_challenge_activation_generation CHECK(
    (action='device.activate' AND activation_generation IS NOT NULL) OR
    (action<>'device.activate' AND activation_generation IS NULL));

ALTER TABLE suite.suite_activation_completions
  ADD COLUMN revocation_generation bigint NOT NULL DEFAULT 0,
  ADD COLUMN activation_generation bigint NOT NULL DEFAULT 0,
  ADD COLUMN invalidated_at timestamptz,
  ADD COLUMN invalidation_reason varchar(64);

ALTER TABLE suite.suite_sessions
  ADD COLUMN revoked_at timestamptz,
  ADD COLUMN revocation_reason varchar(64);

INSERT INTO suite.schema_migrations(version)
VALUES('004_suite_commerce_lifecycle')
ON CONFLICT(version) DO NOTHING;
COMMIT;
