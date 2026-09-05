BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:022_emulationstation_sessions',0));

CREATE TABLE suite.suite_es_challenges (
  challenge_id char(64) PRIMARY KEY CHECK(challenge_id ~ '^[0-9a-f]{64}$'),
  product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL CHECK(device_id ~ '^[0-9a-f]{64}$'),
  session_id char(64) NOT NULL CHECK(session_id ~ '^[0-9a-f]{64}$'),
  action varchar(32) NOT NULL CHECK(action IN ('session.open','session.heartbeat')),
  context_hash char(64) NOT NULL CHECK(context_hash ~ '^[0-9a-f]{64}$'),
  nonce varchar(128) NOT NULL,
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz,
  revocation_generation bigint NOT NULL CHECK(revocation_generation>=0),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);
CREATE INDEX ix_suite_es_challenges_expiry ON suite.suite_es_challenges(expires_at);
CREATE INDEX ix_suite_es_challenges_device ON suite.suite_es_challenges(license_id,device_id,expires_at);

CREATE TABLE suite.suite_es_sessions (
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL CHECK(device_id ~ '^[0-9a-f]{64}$'),
  session_id char(64) NOT NULL UNIQUE CHECK(session_id ~ '^[0-9a-f]{64}$'),
  status varchar(24) NOT NULL CHECK(status IN ('ACTIVE','REVOKED','EXPIRED')),
  authorized_until timestamptz NOT NULL,
  last_server_time bigint NOT NULL,
  revocation_generation bigint NOT NULL CHECK(revocation_generation>=0),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(license_id,device_id),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);

REVOKE ALL ON suite.suite_es_challenges,suite.suite_es_sessions FROM PUBLIC;
GRANT SELECT,INSERT,UPDATE,DELETE ON suite.suite_es_challenges TO "turborama-suite";
GRANT SELECT,INSERT,UPDATE ON suite.suite_es_sessions TO "turborama-suite";
INSERT INTO suite.schema_migrations(version) VALUES('022_suite_emulationstation_sessions');
COMMIT;
