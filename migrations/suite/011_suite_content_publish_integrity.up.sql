BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:011_content_publish_integrity',0));

CREATE TABLE suite.suite_content_publish_runs(
  run_id char(64) PRIMARY KEY CHECK(run_id ~ '^[0-9a-f]{64}$'),
  catalog_identity char(64) NOT NULL REFERENCES suite.suite_content_snapshots(catalog_identity),
  inventory_sha256 char(64) NOT NULL CHECK(inventory_sha256 ~ '^[0-9a-f]{64}$'),
  expected_item_count integer NOT NULL CHECK(expected_item_count>0),
  published_item_count integer NOT NULL CHECK(published_item_count>=0),
  ready_item_count integer NOT NULL CHECK(ready_item_count>=0),
  maintenance_item_count integer NOT NULL CHECK(maintenance_item_count>=0),
  outcome varchar(16) NOT NULL CHECK(outcome='PUBLISHED'),
  detail_code varchar(64) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  UNIQUE(catalog_identity,inventory_sha256),
  CHECK(published_item_count=expected_item_count),
  CHECK(ready_item_count+maintenance_item_count=published_item_count)
);

CREATE FUNCTION suite.guard_suite_content_snapshot_mutation()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,suite
AS $$
BEGIN
  IF TG_OP='DELETE' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SNAPSHOT_IMMUTABLE';
  END IF;
  IF OLD.status<>'STAGING' OR NEW.status<>'PUBLISHED'
     OR OLD.catalog_identity<>NEW.catalog_identity
     OR OLD.catalog_sequence<>NEW.catalog_sequence
     OR OLD.inventory_sha256<>NEW.inventory_sha256
     OR OLD.visual_catalog_sha256<>NEW.visual_catalog_sha256
     OR OLD.item_count<>NEW.item_count
     OR OLD.ready_item_count<>NEW.ready_item_count
     OR OLD.maintenance_item_count<>NEW.maintenance_item_count
     OR OLD.created_at<>NEW.created_at
     OR NEW.published_at IS NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SNAPSHOT_IMMUTABLE';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER tr_suite_content_snapshot_immutable
BEFORE UPDATE OR DELETE ON suite.suite_content_snapshots
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_snapshot_mutation();

CREATE FUNCTION suite.guard_suite_content_item_mutation()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,suite
AS $$
DECLARE parent_status varchar(16);
BEGIN
  IF TG_OP='DELETE' THEN
    SELECT s.status INTO parent_status FROM suite.suite_content_snapshots s
      WHERE s.catalog_identity=OLD.catalog_identity;
  ELSE
    SELECT s.status INTO parent_status FROM suite.suite_content_snapshots s
      WHERE s.catalog_identity=NEW.catalog_identity;
  END IF;
  IF parent_status IS DISTINCT FROM 'STAGING' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ITEM_IMMUTABLE';
  END IF;
  IF TG_OP='DELETE' THEN RETURN OLD; END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER tr_suite_content_item_immutable
BEFORE INSERT OR UPDATE OR DELETE ON suite.suite_content_items
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_item_mutation();

CREATE TRIGGER tr_suite_content_origin_immutable
BEFORE INSERT OR UPDATE OR DELETE ON suite.suite_content_artifact_origins
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_item_mutation();

CREATE FUNCTION suite.guard_suite_content_origin_ready()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,suite
AS $$
BEGIN
  IF NOT EXISTS(
    SELECT 1 FROM suite.suite_content_items i
    WHERE i.catalog_identity=NEW.catalog_identity AND i.item_id=NEW.item_id AND i.status='READY'
  ) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ORIGIN_REQUIRES_READY_ITEM';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER tr_suite_content_origin_requires_ready
BEFORE INSERT OR UPDATE ON suite.suite_content_artifact_origins
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_origin_ready();

CREATE FUNCTION suite.guard_suite_content_entitlement_commerce()
RETURNS trigger
LANGUAGE plpgsql
SET search_path=pg_catalog,suite
AS $$
BEGIN
  IF NOT EXISTS(
    SELECT 1
    FROM suite.suite_license_deliveries d
    JOIN suite.suite_licenses l ON l.license_id=d.license_id
    WHERE d.source_system=NEW.source_system
      AND d.source_purchase_id=NEW.source_purchase_id
      AND d.source_item_key=NEW.source_item_key
      AND d.product_id=NEW.product_id
      AND d.license_id=NEW.license_id
      AND d.source_system='TURBOBOX_V1'
      AND d.source_product_sku='SUITE_LIFETIME_1_DEVICE'
      AND l.provisioning_origin='COMMERCE'
      AND l.product_id='TURBORAMA_SUITE'
      AND ((NEW.status='ACTIVE' AND d.provisioning_state='PROVISIONED'
            AND l.status='ACTIVE' AND (d.financial_state='PAID' OR
              (d.financial_state='SUSPENDED'
               AND d.administrative_resume_source_version=d.last_source_version
               AND d.administrative_resume_actor IS NOT NULL
               AND d.administrative_resume_reason IS NOT NULL
               AND d.administrative_resume_request_id IS NOT NULL)))
        OR (NEW.status='SUSPENDED' AND d.provisioning_state='SUSPENDED'
            AND d.financial_state='SUSPENDED')
        OR (NEW.status='REVOKED' AND d.provisioning_state='REVOKED'))
  ) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ENTITLEMENT_NOT_COMMERCE_ELIGIBLE';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER tr_suite_content_entitlement_commerce
BEFORE INSERT OR UPDATE ON suite.suite_content_entitlements
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_entitlement_commerce();

CREATE FUNCTION suite.publish_suite_content_catalog(p_catalog_identity char(64))
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,suite
AS $$
DECLARE expected_count integer;
DECLARE expected_ready_count integer;
DECLARE expected_maintenance_count integer;
DECLARE actual_count integer;
DECLARE actual_maintenance_count integer;
DECLARE origin_count integer;
DECLARE snapshot_status varchar(16);
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-publish',0));
  SELECT s.item_count,s.ready_item_count,s.maintenance_item_count,s.status
    INTO expected_count,expected_ready_count,expected_maintenance_count,snapshot_status
    FROM suite.suite_content_snapshots s
    WHERE s.catalog_identity=p_catalog_identity
    FOR UPDATE;
  IF expected_count IS NULL OR snapshot_status NOT IN('STAGING','PUBLISHED') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SNAPSHOT_INVALID';
  END IF;
  SELECT count(*) INTO actual_count FROM suite.suite_content_items i
    WHERE i.catalog_identity=p_catalog_identity AND i.status='READY'
      AND i.content_length BETWEEN 1 AND 549755813888 AND i.sha256 ~ '^[0-9a-f]{64}$';
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
    UPDATE suite.suite_content_snapshots
      SET status='PUBLISHED',published_at=clock_timestamp()
      WHERE catalog_identity=p_catalog_identity;
  END IF;
  INSERT INTO suite.suite_content_catalog_state(product_id,active_catalog_identity,updated_at)
    VALUES('TURBORAMA_SUITE',p_catalog_identity,clock_timestamp())
    ON CONFLICT(product_id) DO UPDATE
      SET active_catalog_identity=excluded.active_catalog_identity,updated_at=excluded.updated_at;
END $$;

CREATE FUNCTION suite.reconcile_suite_content_entitlements()
RETURNS bigint
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,suite
AS $$
DECLARE affected bigint;
BEGIN
  INSERT INTO suite.suite_content_entitlements(
    license_id,scope,status,source_system,source_purchase_id,source_item_key,product_id,created_at,updated_at)
  SELECT d.license_id,'FULL_CATALOG','ACTIVE',d.source_system,d.source_purchase_id,d.source_item_key,
    'TURBORAMA_SUITE',clock_timestamp(),clock_timestamp()
  FROM suite.suite_license_deliveries d
  JOIN suite.suite_licenses l ON l.license_id=d.license_id
  WHERE d.source_system='TURBOBOX_V1'
    AND d.product_id='TURBORAMA_SUITE'
    AND d.source_product_sku='SUITE_LIFETIME_1_DEVICE'
    AND d.provisioning_state='PROVISIONED'
    AND (d.financial_state='PAID' OR
      (d.financial_state='SUSPENDED'
       AND d.administrative_resume_source_version=d.last_source_version
       AND d.administrative_resume_actor IS NOT NULL
       AND d.administrative_resume_reason IS NOT NULL
       AND d.administrative_resume_request_id IS NOT NULL))
    AND l.provisioning_origin='COMMERCE'
    AND l.product_id='TURBORAMA_SUITE'
    AND l.status='ACTIVE'
  ON CONFLICT(license_id,scope) DO UPDATE
    SET status='ACTIVE',source_system=excluded.source_system,
        source_purchase_id=excluded.source_purchase_id,source_item_key=excluded.source_item_key,
        updated_at=clock_timestamp()
  WHERE suite_content_entitlements.source_system='TURBOBOX_V1'
    AND suite_content_entitlements.source_purchase_id=excluded.source_purchase_id
    AND suite_content_entitlements.source_item_key=excluded.source_item_key;
  GET DIAGNOSTICS affected = ROW_COUNT;
  RETURN affected;
END $$;

REVOKE ALL ON FUNCTION suite.publish_suite_content_catalog(char(64)) FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.reconcile_suite_content_entitlements() FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.guard_suite_content_entitlement_commerce() FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.guard_suite_content_origin_ready() FROM PUBLIC;

INSERT INTO suite.schema_migrations(version)
VALUES('011_suite_content_publish_integrity')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('011_suite_content_publish_integrity', :'migration_sha256');
COMMIT;
