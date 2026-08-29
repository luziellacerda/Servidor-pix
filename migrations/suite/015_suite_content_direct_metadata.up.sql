\set ON_ERROR_STOP on
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('suite:015_content_direct_metadata',0));

-- Direct mode deliberately has no authoritative file size or file digest.
DO $$
DECLARE constraint_row record;
BEGIN
  FOR constraint_row IN
    SELECT DISTINCT constraint_definition.conrelid::regclass AS relation_name,
      constraint_definition.conname
    FROM pg_constraint constraint_definition
    JOIN LATERAL unnest(constraint_definition.conkey) constrained(attnum) ON true
    JOIN pg_attribute attribute
      ON attribute.attrelid=constraint_definition.conrelid
     AND attribute.attnum=constrained.attnum
    WHERE constraint_definition.contype='c'
      AND constraint_definition.conrelid IN ('suite.suite_content_items'::regclass,
                       'suite.suite_content_grants'::regclass,
                       'suite.suite_content_origin_candidates'::regclass)
      AND attribute.attname IN ('content_length','sha256','expected_content_length',
        'expected_sha256','verified_content_length','verified_sha256')
  LOOP
    EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I',
      constraint_row.relation_name,constraint_row.conname);
  END LOOP;
END $$;

ALTER TABLE suite.suite_content_grants
  ALTER COLUMN content_length DROP NOT NULL,
  ALTER COLUMN sha256 DROP NOT NULL;

ALTER TABLE suite.suite_content_items DISABLE TRIGGER tr_suite_content_item_immutable;
UPDATE suite.suite_content_items SET content_length=NULL,sha256=NULL
WHERE content_length IS NOT NULL OR sha256 IS NOT NULL;
ALTER TABLE suite.suite_content_items ENABLE TRIGGER tr_suite_content_item_immutable;
UPDATE suite.suite_content_grants SET content_length=NULL,sha256=NULL
WHERE content_length IS NOT NULL OR sha256 IS NOT NULL;
UPDATE suite.suite_content_origin_candidates
SET expected_content_length=NULL,expected_sha256=NULL,
    verified_content_length=NULL,verified_sha256=NULL
WHERE expected_content_length IS NOT NULL OR expected_sha256 IS NOT NULL
   OR verified_content_length IS NOT NULL OR verified_sha256 IS NOT NULL;

ALTER TABLE suite.suite_content_items
  ADD CONSTRAINT ck_suite_content_items_direct_no_size_digest
    CHECK(content_length IS NULL AND sha256 IS NULL),
  ADD CONSTRAINT ck_suite_content_items_direct_state CHECK(
    (status='READY' AND artifact_id=item_id AND artifact_version IS NOT NULL AND
      safe_file_name IS NOT NULL AND file_extension IS NOT NULL AND
      extract_policy=visual_extract_policy AND manifest_identity IS NOT NULL AND
      descriptor_hash IS NOT NULL AND content_type IS NOT NULL AND
      maintenance_reason IS NULL)
    OR
    (status='MAINTENANCE' AND artifact_id IS NULL AND artifact_version IS NULL AND
      safe_file_name IS NULL AND file_extension IS NULL AND extract_policy IS NULL AND
      manifest_identity IS NULL AND descriptor_hash IS NULL AND content_type IS NULL AND
      source_etag IS NULL AND source_last_modified IS NULL AND
      maintenance_reason='CONTENT_TEMPORARILY_UNAVAILABLE'));

ALTER TABLE suite.suite_content_grants
  ADD CONSTRAINT ck_suite_content_grants_direct_no_size_digest
    CHECK(content_length IS NULL AND sha256 IS NULL);

ALTER TABLE suite.suite_content_origin_candidates
  ADD CONSTRAINT ck_suite_content_candidates_direct_no_size_digest CHECK(
    expected_content_length IS NULL AND expected_sha256 IS NULL AND
    verified_content_length IS NULL AND verified_sha256 IS NULL),
  ADD CONSTRAINT ck_suite_content_candidates_direct_intent CHECK(
    (change_intent='INITIAL_RECOVERY' AND expected_artifact_version IS NULL AND
      change_reason IS NULL)
    OR (change_intent='MIRROR_REPLACEMENT' AND expected_artifact_version IS NOT NULL AND
      expected_file_extension IS NOT NULL AND expected_extract_policy IS NOT NULL AND
      change_reason IS NULL)
    OR (change_intent='NEW_ARTIFACT_VERSION' AND expected_artifact_version IS NOT NULL AND
      change_reason IS NOT NULL)),
  ADD CONSTRAINT ck_suite_content_candidates_direct_verified CHECK(
    (state IN('VERIFIED','PUBLISHED') AND verified_file_extension IS NOT NULL AND
      verified_safe_file_name IS NOT NULL AND verified_extract_policy IS NOT NULL AND
      verified_content_type IS NOT NULL AND verified_at IS NOT NULL)
    OR state NOT IN('VERIFIED','PUBLISHED'));

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
  IF expected_count<>850 OR expected_ready_count<>actual_count OR
     expected_maintenance_count<>actual_maintenance_count OR
     actual_count+actual_maintenance_count<>850 OR origin_count<>actual_count THEN
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

INSERT INTO suite.schema_migrations(version) VALUES('015_suite_content_direct_metadata')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('015_suite_content_direct_metadata', :'migration_sha256');
COMMIT;
