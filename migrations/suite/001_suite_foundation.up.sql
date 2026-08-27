BEGIN;
CREATE SCHEMA IF NOT EXISTS suite;
CREATE TABLE suite.suite_licenses(
  license_id varchar(64) PRIMARY KEY,
  product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  status varchar(24) NOT NULL CHECK(status IN('ACTIVE','SUSPENDED','REVOKED','EXPIRED')),
  activation_verifier char(64) NOT NULL,
  activation_expires_at timestamptz NOT NULL,
  activation_consumed boolean NOT NULL DEFAULT false,
  revocation_generation bigint NOT NULL DEFAULT 0 CHECK(revocation_generation>=0),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(), updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE suite.suite_devices(
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  device_id char(64) NOT NULL, binding_type varchar(32) NOT NULL,
  public_key_spki text NOT NULL, hardware_fingerprint char(64) NOT NULL,
  status varchar(24) NOT NULL CHECK(status IN('ACTIVE','SUSPENDED','REVOKED')),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(), updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(license_id,device_id)
);
CREATE TABLE suite.suite_challenges(
  challenge_id char(64) PRIMARY KEY, product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  license_id varchar(64) NOT NULL, device_id char(64) NOT NULL, session_id varchar(64) NOT NULL,
  action varchar(32) NOT NULL CHECK(action IN('device.activate','session.open','session.heartbeat')),
  context_hash char(64) NOT NULL, nonce varchar(128) NOT NULL, expires_at timestamptz NOT NULL,
  consumed_at timestamptz NULL, activation_verifier char(64) NULL, device_json jsonb NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX ix_suite_challenges_expiry ON suite.suite_challenges(expires_at) WHERE consumed_at IS NULL;
CREATE TABLE suite.suite_activation_completions(
  challenge_id char(64) PRIMARY KEY REFERENCES suite.suite_challenges(challenge_id),
  request_digest char(64) NOT NULL, result_json jsonb NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE TABLE suite.suite_sessions(
  license_id varchar(64) NOT NULL, device_id char(64) NOT NULL, session_id char(64) NOT NULL,
  status varchar(24) NOT NULL CHECK(status IN('ACTIVE','REVOKED','EXPIRED')),
  authorized_until timestamptz NOT NULL, last_server_time bigint NOT NULL,
  revocation_generation bigint NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(), updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(license_id,device_id), UNIQUE(session_id),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);
CREATE TABLE suite.suite_audit_events(
  event_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, occurred_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  event_type varchar(64) NOT NULL, license_id varchar(64) NULL, device_id char(64) NULL,
  correlation_id varchar(64) NOT NULL, outcome varchar(24) NOT NULL, detail_code varchar(64) NOT NULL
);
CREATE INDEX ix_suite_audit_time ON suite.suite_audit_events(occurred_at DESC);
CREATE TABLE suite.suite_outbox(
  outbox_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, event_type varchar(64) NOT NULL,
  payload jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT clock_timestamp(), published_at timestamptz NULL
);
COMMIT;
