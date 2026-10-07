-- Additive permissions for the dedicated Station API. Existing roles are preserved.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:031_station_api_isolation',0));
DO $$ BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-station-api') THEN
    CREATE ROLE "turborama-station-api" NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
  END IF;
END $$;
CREATE SCHEMA station_api;
REVOKE ALL ON SCHEMA station_api FROM PUBLIC;
GRANT USAGE ON SCHEMA station_api TO "turborama-station-api";

CREATE VIEW station_api.suite_licenses WITH (security_barrier=true) AS
 SELECT * FROM suite.suite_licenses WHERE product_id='TURBORAMA_STATION_ANDROID'
 WITH LOCAL CHECK OPTION;
CREATE VIEW station_api.suite_license_deliveries WITH (security_barrier=true) AS
 SELECT * FROM suite.suite_license_deliveries WHERE product_id='TURBORAMA_STATION_ANDROID';
CREATE VIEW station_api.schema_migrations AS
 SELECT version,applied_at FROM suite.schema_migrations;

DO $$ DECLARE tab text; BEGIN
  FOREACH tab IN ARRAY ARRAY['station_devices','station_challenges','station_sessions',
                             'station_customer_projection','station_download_grants'] LOOP
    EXECUTE format('CREATE VIEW station_api.%I WITH (security_barrier=true) AS '
      'SELECT t.* FROM suite.%I t WHERE EXISTS (SELECT 1 FROM suite.suite_licenses l '
      'WHERE l.license_id=t.license_id AND l.product_id=''TURBORAMA_STATION_ANDROID'') '
      'WITH LOCAL CHECK OPTION',tab,tab);
  END LOOP;
END $$;
GRANT SELECT ON ALL TABLES IN SCHEMA station_api TO "turborama-station-api";
GRANT UPDATE(activation_consumed,enrollment_state,updated_at)
 ON station_api.suite_licenses TO "turborama-station-api";
GRANT INSERT,UPDATE ON station_api.station_challenges,station_api.station_sessions,
 station_api.station_download_grants TO "turborama-station-api";

-- ON CONFLICT is not supported for a PostgreSQL view. This narrow function
-- performs only the existing device bind; it never creates a commercial license.
CREATE FUNCTION station_api.bind_device(license text,device text,spki text,
 manufacturer text,model text,sdk integer,version text) RETURNS boolean
 LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
DECLARE changed integer;
BEGIN
 IF NOT EXISTS(SELECT 1 FROM suite.suite_licenses l WHERE l.license_id=license
               AND l.product_id='TURBORAMA_STATION_ANDROID') THEN
   RAISE EXCEPTION 'Station license required' USING ERRCODE='42501';
 END IF;
 INSERT INTO suite.station_devices(license_id,device_id,public_key_spki,manufacturer,
   model,android_sdk,client_version,status)
 VALUES(license,device,spki,manufacturer,model,sdk,version,'ACTIVE')
 ON CONFLICT(license_id,device_id) DO UPDATE SET
   public_key_spki=EXCLUDED.public_key_spki,manufacturer=EXCLUDED.manufacturer,
   model=EXCLUDED.model,android_sdk=EXCLUDED.android_sdk,client_version=EXCLUDED.client_version,
   status='ACTIVE',updated_at=clock_timestamp()
 WHERE suite.station_devices.status='REVOKED';
 GET DIAGNOSTICS changed=ROW_COUNT;
 RETURN changed=1;
END $$;
REVOKE ALL ON FUNCTION station_api.bind_device(text,text,text,text,text,integer,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION station_api.bind_device(text,text,text,text,text,integer,text)
 TO "turborama-station-api";
INSERT INTO suite.schema_migrations(version) VALUES('031_station_api_isolation');
COMMIT;
