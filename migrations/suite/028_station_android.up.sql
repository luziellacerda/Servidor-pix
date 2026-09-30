-- Candidate only. Apply after independent review, backup and release preflight.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:028_station_android',0));

ALTER TABLE suite.suite_licenses DROP CONSTRAINT suite_licenses_product_id_check;
ALTER TABLE suite.suite_licenses ADD CONSTRAINT suite_licenses_product_id_check
  CHECK (product_id IN ('TURBORAMA_SUITE','TURBORAMA_STATION_ANDROID'));
ALTER TABLE suite.suite_commerce_inbox DROP CONSTRAINT suite_commerce_inbox_source_product_sku_check;
ALTER TABLE suite.suite_commerce_inbox ADD CONSTRAINT suite_commerce_inbox_source_product_sku_check
  CHECK (source_product_sku IN ('SUITE_LIFETIME_1_DEVICE','STATION_ANDROID_LIFETIME_1_DEVICE'));
ALTER TABLE suite.suite_license_deliveries DROP CONSTRAINT suite_license_deliveries_product_id_check;
ALTER TABLE suite.suite_license_deliveries ADD CONSTRAINT suite_license_deliveries_product_id_check
  CHECK (product_id IN ('TURBORAMA_SUITE','TURBORAMA_STATION_ANDROID'));
ALTER TABLE suite.suite_license_deliveries DROP CONSTRAINT suite_license_deliveries_source_product_sku_check;
ALTER TABLE suite.suite_license_deliveries ADD CONSTRAINT suite_license_deliveries_source_product_sku_check
  CHECK (source_product_sku IN ('SUITE_LIFETIME_1_DEVICE','STATION_ANDROID_LIFETIME_1_DEVICE'));
ALTER TABLE suite.suite_license_deliveries ADD CONSTRAINT ck_suite_delivery_product_sku_pair
  CHECK ((product_id='TURBORAMA_SUITE' AND source_product_sku='SUITE_LIFETIME_1_DEVICE') OR
         (product_id='TURBORAMA_STATION_ANDROID' AND source_product_sku='STATION_ANDROID_LIFETIME_1_DEVICE'));
CREATE UNIQUE INDEX ux_station_activation_verifier
  ON suite.suite_licenses(activation_verifier)
  WHERE product_id='TURBORAMA_STATION_ANDROID' AND activation_verifier IS NOT NULL;

CREATE TABLE suite.station_devices (
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  device_id varchar(43) NOT NULL,
  public_key_spki text NOT NULL,
  manufacturer varchar(100), model varchar(100), android_sdk integer,
  client_version varchar(64),
  status varchar(16) NOT NULL CHECK (status IN ('ACTIVE','REVOKED')),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY (license_id,device_id)
);
CREATE UNIQUE INDEX ux_station_one_active_device
  ON suite.station_devices(license_id) WHERE status='ACTIVE';

CREATE TABLE suite.station_challenges (
  challenge_id char(64) PRIMARY KEY,
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  device_id varchar(43) NOT NULL,
  action varchar(24) NOT NULL CHECK (action IN ('ACTIVATE','SESSION')),
  nonce varchar(43) NOT NULL,
  public_key_spki text,
  activation_verifier char(64),
  activation_generation bigint,
  revocation_generation bigint NOT NULL,
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK ((action='ACTIVATE' AND public_key_spki IS NOT NULL AND activation_verifier IS NOT NULL
          AND activation_generation IS NOT NULL) OR
         (action='SESSION' AND public_key_spki IS NULL AND activation_verifier IS NULL
          AND activation_generation IS NULL))
);
CREATE INDEX ix_station_challenge_expiry ON suite.station_challenges(expires_at)
  WHERE consumed_at IS NULL;
CREATE INDEX ix_station_challenge_license ON suite.station_challenges(license_id,action)
  WHERE consumed_at IS NULL;

CREATE TABLE suite.station_sessions (
  session_id char(64) PRIMARY KEY,
  license_id varchar(64) NOT NULL,
  device_id varchar(43) NOT NULL,
  token_digest char(64) NOT NULL UNIQUE,
  revocation_generation bigint NOT NULL,
  status varchar(16) NOT NULL CHECK (status IN ('ACTIVE','REVOKED')),
  authorized_until timestamptz NOT NULL,
  last_contact_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  FOREIGN KEY (license_id,device_id) REFERENCES suite.station_devices(license_id,device_id)
);
CREATE INDEX ix_station_session_device ON suite.station_sessions(license_id,device_id,authorized_until DESC);
CREATE INDEX ix_station_session_expiry ON suite.station_sessions(authorized_until)
  WHERE status='ACTIVE';

-- A projection of the existing trusted buyer/order relationship, never a new account.
CREATE TABLE suite.station_customer_projection (
  license_id varchar(64) PRIMARY KEY REFERENCES suite.suite_licenses(license_id),
  source_system varchar(48) NOT NULL,
  source_purchase_id varchar(64) NOT NULL,
  source_item_key varchar(32) NOT NULL,
  customer_ref varchar(128) NOT NULL,
  display_name varchar(256) NOT NULL,
  profile_version bigint NOT NULL DEFAULT 1 CHECK (profile_version>0),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

GRANT USAGE ON SCHEMA suite TO "turborama-suite","turborama-suite-admin";
GRANT SELECT ON suite.schema_migrations TO "turborama-suite";
GRANT SELECT ON suite.suite_licenses,suite.suite_license_deliveries
  TO "turborama-suite";
GRANT SELECT ON suite.station_devices,suite.station_challenges,suite.station_sessions,
  suite.station_customer_projection TO "turborama-suite";
GRANT INSERT,UPDATE ON suite.station_devices,suite.station_challenges,suite.station_sessions
  TO "turborama-suite";
GRANT SELECT,INSERT,UPDATE ON suite.station_devices,suite.station_challenges,
  suite.station_sessions,suite.station_customer_projection TO "turborama-suite-admin";
GRANT UPDATE(activation_consumed,enrollment_state,updated_at)
  ON suite.suite_licenses TO "turborama-suite";

INSERT INTO suite.schema_migrations(version) VALUES('028_station_android');
COMMIT;
