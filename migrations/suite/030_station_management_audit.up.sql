-- Read only Station history; do not widen access to the shared audit table.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:030_station_management_audit',0));

CREATE TABLE suite.station_admin_code_issues (
  request_id varchar(64) PRIMARY KEY,
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  activation_generation bigint NOT NULL,
  actor varchar(128) NOT NULL,
  kind varchar(16) NOT NULL CHECK (kind IN ('human','purchase')),
  reason varchar(256) NOT NULL CHECK (length(reason) BETWEEN 10 AND 256),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
REVOKE ALL ON suite.station_admin_code_issues FROM PUBLIC;
GRANT SELECT,INSERT ON suite.station_admin_code_issues TO "turborama-suite-admin";

CREATE VIEW suite.station_management_audit WITH (security_barrier=true) AS
SELECT a.event_id,a.event_type,a.license_id,a.correlation_id,a.outcome,
       a.detail_code,a.admin_actor,a.request_id,a.otp_expires_at,a.occurred_at,
       coalesce(i.reason,'') AS reason
FROM suite.suite_audit_events a
JOIN suite.suite_licenses l ON l.license_id=a.license_id
LEFT JOIN suite.station_admin_code_issues i ON i.license_id=a.license_id AND i.request_id=a.request_id
WHERE l.product_id='TURBORAMA_STATION_ANDROID';
REVOKE ALL ON suite.station_management_audit FROM PUBLIC;
GRANT SELECT ON suite.station_management_audit TO "turborama-suite-admin";

INSERT INTO suite.schema_migrations(version) VALUES('030_station_management_audit');
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('030_station_management_audit', :'migration_sha256');
COMMIT;
