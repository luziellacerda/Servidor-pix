\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:016_content_catalog_902',0));

DO $$
DECLARE constraint_name name;
BEGIN
  SELECT c.conname INTO constraint_name
  FROM pg_constraint c
  JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=ANY(c.conkey)
  WHERE c.conrelid='suite.suite_content_snapshots'::regclass
    AND c.contype='c' AND a.attname='item_count'
    AND pg_get_constraintdef(c.oid) LIKE '%item_count = 850%';
  IF constraint_name IS NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_850_CONSTRAINT_MISSING';
  END IF;
  EXECUTE format('ALTER TABLE suite.suite_content_snapshots DROP CONSTRAINT %I',
    constraint_name);
END $$;

ALTER TABLE suite.suite_content_snapshots
  ADD CONSTRAINT ck_suite_content_snapshots_item_count_902 CHECK(item_count=902) NOT VALID;

CREATE OR REPLACE FUNCTION suite.publish_suite_content_catalog(p_catalog_identity char(64))
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
DECLARE expected_count integer; DECLARE expected_ready_count integer;
DECLARE expected_maintenance_count integer; DECLARE actual_count integer;
DECLARE actual_maintenance_count integer; DECLARE origin_count integer;
DECLARE snapshot_status varchar(16); DECLARE active_key_version integer;
DECLARE key_set_fingerprint char(64); DECLARE allowlist_fingerprint char(64);
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-publish',0));
  SELECT s.item_count,s.ready_item_count,s.maintenance_item_count,s.status,
      s.origin_active_key_version,s.origin_key_set_fingerprint,s.origin_allowlist_fingerprint
    INTO expected_count,expected_ready_count,expected_maintenance_count,snapshot_status,
      active_key_version,key_set_fingerprint,allowlist_fingerprint
    FROM suite.suite_content_snapshots s WHERE s.catalog_identity=p_catalog_identity FOR UPDATE;
  IF expected_count IS NULL OR snapshot_status NOT IN('STAGING','PUBLISHED') OR
     active_key_version IS NULL OR key_set_fingerprint IS NULL OR allowlist_fingerprint IS NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SNAPSHOT_INVALID';
  END IF;
  SELECT count(*) INTO actual_count FROM suite.suite_content_items i
    WHERE i.catalog_identity=p_catalog_identity AND i.status='READY'
      AND i.content_length IS NULL AND i.sha256 IS NULL;
  SELECT count(*) INTO actual_maintenance_count FROM suite.suite_content_items i
    WHERE i.catalog_identity=p_catalog_identity AND i.status='MAINTENANCE'
      AND i.maintenance_reason='CONTENT_TEMPORARILY_UNAVAILABLE';
  SELECT count(*) INTO origin_count FROM suite.suite_content_artifact_origins o
    WHERE o.catalog_identity=p_catalog_identity;
  IF expected_count<>902 OR expected_ready_count<>actual_count OR
     expected_maintenance_count<>actual_maintenance_count OR
     actual_count+actual_maintenance_count<>902 OR origin_count<>actual_count THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ITEM_COUNT_MISMATCH';
  END IF;
  IF snapshot_status='STAGING' THEN
    UPDATE suite.suite_content_snapshots SET status='PUBLISHED',published_at=clock_timestamp()
      WHERE catalog_identity=p_catalog_identity;
  END IF;
  INSERT INTO suite.suite_content_catalog_state(product_id,active_catalog_identity,updated_at)
    VALUES('TURBORAMA_SUITE',p_catalog_identity,clock_timestamp())
    ON CONFLICT(product_id) DO UPDATE SET active_catalog_identity=excluded.active_catalog_identity,
      updated_at=excluded.updated_at;
END $$;

REVOKE ALL ON FUNCTION suite.publish_suite_content_catalog(char(64)) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION suite.publish_suite_content_catalog(char(64))
  TO "turborama-suite-publisher","turborama-suite-content-monitor";

INSERT INTO suite.schema_migrations(version) VALUES('016_suite_content_catalog_902');
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('016_suite_content_catalog_902', :'migration_sha256');
COMMIT;
