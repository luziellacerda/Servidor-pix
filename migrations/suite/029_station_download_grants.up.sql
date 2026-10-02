-- Additive Station download grants. Do not change 028_station_android.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:029_station_download_grants',0));

CREATE TABLE suite.station_download_grants (
  grant_id varchar(43) PRIMARY KEY,
  license_id varchar(64) NOT NULL,
  device_id varchar(43) NOT NULL,
  item_id varchar(64) NOT NULL,
  key_version integer NOT NULL,
  nonce bytea NOT NULL,
  ciphertext bytea NOT NULL,
  tag bytea NOT NULL,
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE INDEX ix_station_grant_expiry ON suite.station_download_grants(expires_at)
  WHERE consumed_at IS NULL;

GRANT SELECT,INSERT,UPDATE ON suite.station_download_grants
  TO "turborama-suite","turborama-suite-admin";

INSERT INTO suite.schema_migrations(version) VALUES('029_station_download_grants');
COMMIT;
