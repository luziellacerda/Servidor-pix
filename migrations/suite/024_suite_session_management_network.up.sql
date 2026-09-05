BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:024_session_management_network',0));

-- Null means no trustworthy timestamp is available for a historical session.
ALTER TABLE suite.suite_sessions ADD COLUMN last_contact_at timestamptz;
ALTER TABLE suite.suite_es_sessions ADD COLUMN last_contact_at timestamptz;
CREATE FUNCTION suite.stamp_session_contact() RETURNS trigger LANGUAGE plpgsql
SET search_path=pg_catalog,suite AS $$
BEGIN
  IF NEW.status='ACTIVE' THEN NEW.last_contact_at=clock_timestamp(); END IF;
  RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION suite.stamp_session_contact() FROM PUBLIC;
CREATE TRIGGER suite_session_contact BEFORE INSERT OR UPDATE OF authorized_until,last_server_time
ON suite.suite_sessions FOR EACH ROW EXECUTE FUNCTION suite.stamp_session_contact();
CREATE TRIGGER suite_es_session_contact BEFORE INSERT OR UPDATE OF authorized_until,last_server_time
ON suite.suite_es_sessions FOR EACH ROW EXECUTE FUNCTION suite.stamp_session_contact();

CREATE TABLE suite.suite_network_challenges (
  challenge_id char(64) PRIMARY KEY CHECK(challenge_id ~ '^[0-9a-f]{64}$'),
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL,
  session_id char(64) NOT NULL CHECK(session_id ~ '^[0-9a-f]{64}$'),
  app_scope varchar(24) NOT NULL CHECK(app_scope IN ('SUITE','EMULATIONSTATION')),
  context_hash char(64) NOT NULL CHECK(context_hash ~ '^[0-9a-f]{64}$'),
  nonce varchar(128) NOT NULL,
  revocation_generation bigint NOT NULL,
  expires_at timestamptz NOT NULL,
  consumed_at timestamptz,
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);
CREATE INDEX ix_suite_network_challenge_expiry ON suite.suite_network_challenges(expires_at);
CREATE INDEX ix_suite_network_challenge_scope ON suite.suite_network_challenges(license_id,device_id,app_scope,expires_at);

-- Only the latest bounded report is retained for each application/device. Raw
-- network data is AES-GCM protected by the existing protected inventory key.
CREATE TABLE suite.suite_network_inventory (
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL,
  app_scope varchar(24) NOT NULL CHECK(app_scope IN ('SUITE','EMULATIONSTATION')),
  session_id char(64) NOT NULL,
  protected_payload bytea NOT NULL CHECK(octet_length(protected_payload) BETWEEN 29 AND 16384),
  ip_masked varchar(64) NOT NULL,
  interfaces_masked jsonb NOT NULL CHECK(jsonb_typeof(interfaces_masked)='array' AND jsonb_array_length(interfaces_masked)<=8),
  collected_at timestamptz NOT NULL,
  received_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  expires_at timestamptz NOT NULL,
  PRIMARY KEY(license_id,device_id,app_scope),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);
CREATE INDEX ix_suite_network_inventory_expiry ON suite.suite_network_inventory(expires_at);

CREATE TABLE suite.suite_es_session_revocations (
  request_id varchar(64) PRIMARY KEY,
  actor varchar(64) NOT NULL,
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL,
  target_session_id char(64) NOT NULL,
  revoked_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);

CREATE VIEW suite.suite_application_sessions AS
WITH sessions AS (
  SELECT 'SUITE'::text app_scope,license_id,device_id,session_id,status,
    authorized_until,revocation_generation,last_contact_at FROM suite.suite_sessions
  UNION ALL
  SELECT 'EMULATIONSTATION'::text,license_id,device_id,session_id,status,
    authorized_until,revocation_generation,last_contact_at FROM suite.suite_es_sessions
)
SELECT s.app_scope,s.license_id,s.device_id,s.session_id,
  CASE WHEN s.status='REVOKED' OR l.status<>'ACTIVE' OR d.status<>'ACTIVE'
      OR s.revocation_generation<>l.revocation_generation OR l.enrollment_state<>'BOUND'
      OR e.device_id IS DISTINCT FROM s.device_id THEN 'REVOKED'
    WHEN s.authorized_until<=clock_timestamp() OR s.status='EXPIRED' THEN 'EXPIRED'
    WHEN s.last_contact_at>=clock_timestamp()-interval '15 seconds' THEN 'ONLINE'
    ELSE 'NO_RECENT_CONTACT' END AS state,
  s.authorized_until,s.last_contact_at
FROM sessions s JOIN suite.suite_licenses l USING(license_id)
JOIN suite.suite_devices d ON d.license_id=s.license_id AND d.device_id=s.device_id
LEFT JOIN suite.suite_license_enrollments e ON e.license_id=s.license_id;

REVOKE ALL ON suite.suite_network_challenges,suite.suite_network_inventory,
  suite.suite_es_session_revocations,suite.suite_application_sessions FROM PUBLIC;
GRANT SELECT,INSERT,UPDATE,DELETE ON suite.suite_network_challenges,suite.suite_network_inventory TO "turborama-suite";
GRANT SELECT ON suite.suite_application_sessions TO "turborama-suite-admin";
GRANT SELECT(license_id,device_id,app_scope,session_id,ip_masked,interfaces_masked,collected_at,received_at,expires_at)
  ON suite.suite_network_inventory TO "turborama-suite-admin";
GRANT SELECT,UPDATE ON suite.suite_es_sessions,suite.suite_es_challenges TO "turborama-suite-admin";
GRANT SELECT,INSERT ON suite.suite_es_session_revocations TO "turborama-suite-admin";

INSERT INTO suite.schema_migrations(version) VALUES('024_suite_session_management_network');
COMMIT;
