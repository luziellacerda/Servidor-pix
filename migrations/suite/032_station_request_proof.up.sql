-- Existing clients and licenses retain their current access until their own
-- signed activation/session explicitly opts into request proofs.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:032_station_request_proof',0));
ALTER TABLE suite.station_devices
 ADD COLUMN request_proof_required boolean NOT NULL DEFAULT false,
 ADD COLUMN verified_app_required boolean NOT NULL DEFAULT false;
ALTER TABLE suite.station_sessions
 ADD COLUMN request_proof_mode varchar(16) NOT NULL DEFAULT 'none',
 ADD COLUMN request_proof_key_spki text,
 ADD CONSTRAINT station_session_request_proof CHECK(
   (request_proof_mode='none' AND request_proof_key_spki IS NULL) OR
   (request_proof_mode IN ('rsa-pss-v1','ec-p256-v1') AND
    request_proof_key_spki IS NOT NULL AND length(request_proof_key_spki) BETWEEN 64 AND 6000));
CREATE OR REPLACE VIEW station_api.station_devices WITH (security_barrier=true) AS
 SELECT t.* FROM suite.station_devices t WHERE EXISTS(SELECT 1 FROM suite.suite_licenses l
 WHERE l.license_id=t.license_id AND l.product_id='TURBORAMA_STATION_ANDROID') WITH LOCAL CHECK OPTION;
CREATE OR REPLACE VIEW station_api.station_sessions WITH (security_barrier=true) AS
 SELECT t.* FROM suite.station_sessions t WHERE EXISTS(SELECT 1 FROM suite.suite_licenses l
 WHERE l.license_id=t.license_id AND l.product_id='TURBORAMA_STATION_ANDROID') WITH LOCAL CHECK OPTION;
GRANT UPDATE(request_proof_required,verified_app_required) ON station_api.station_devices
 TO "turborama-station-api";
INSERT INTO suite.schema_migrations(version) VALUES('032_station_request_proof');
COMMIT;
