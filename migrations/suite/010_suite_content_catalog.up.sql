BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:010_content_catalog',0));

CREATE TABLE suite.schema_migration_checksums(
  version varchar(128) PRIMARY KEY REFERENCES suite.schema_migrations(version),
  script_sha256 char(64) NOT NULL CHECK(script_sha256 ~ '^[0-9a-f]{64}$'),
  recorded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
REVOKE ALL ON suite.schema_migration_checksums FROM PUBLIC;

CREATE FUNCTION suite.guard_schema_migration_checksum()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,suite AS $$
BEGIN
  RAISE EXCEPTION 'SUITE_SCHEMA_MIGRATION_CHECKSUM_IMMUTABLE';
END $$;
CREATE TRIGGER tr_suite_schema_migration_checksum_immutable
BEFORE UPDATE OR DELETE ON suite.schema_migration_checksums
FOR EACH ROW EXECUTE FUNCTION suite.guard_schema_migration_checksum();

ALTER TABLE suite.suite_challenges
  DROP CONSTRAINT suite_challenges_action_check;
ALTER TABLE suite.suite_challenges
  ADD CONSTRAINT suite_challenges_action_check CHECK(action IN(
    'device.activate','session.open','session.heartbeat','catalog.read','download.authorize'));

CREATE TABLE suite.suite_content_snapshots(
  catalog_identity char(64) PRIMARY KEY,
  catalog_sequence bigint NOT NULL UNIQUE CHECK(catalog_sequence>0),
  inventory_sha256 char(64) NOT NULL,
  visual_catalog_sha256 char(64) NOT NULL,
  item_count integer NOT NULL CHECK(item_count=850),
  ready_item_count integer NOT NULL CHECK(ready_item_count>=0),
  maintenance_item_count integer NOT NULL CHECK(maintenance_item_count>=0),
  status varchar(16) NOT NULL CHECK(status IN('STAGING','PUBLISHED')),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  published_at timestamptz,
  CHECK(catalog_identity ~ '^[0-9a-f]{64}$'),
  CHECK(inventory_sha256 ~ '^[0-9a-f]{64}$'),
  CHECK(visual_catalog_sha256 ~ '^[0-9a-f]{64}$'),
  CHECK(ready_item_count+maintenance_item_count=item_count),
  CHECK((status='STAGING' AND published_at IS NULL) OR
        (status='PUBLISHED' AND published_at IS NOT NULL))
);

CREATE TABLE suite.suite_content_items(
  catalog_identity char(64) NOT NULL REFERENCES suite.suite_content_snapshots(catalog_identity),
  item_id char(32) NOT NULL CHECK(item_id ~ '^[0-9a-f]{32}$'),
  display_order integer NOT NULL CHECK(display_order>=0),
  display_name varchar(512) NOT NULL CHECK(display_name<>'' AND display_name !~ '[[:cntrl:]]'
    AND octet_length(display_name)<=2048),
  visual_extract_policy varchar(24) NOT NULL CHECK(visual_extract_policy IN('NONE','EXTRACT_ARCHIVE')),
  artifact_id char(32) CHECK(artifact_id IS NULL OR artifact_id ~ '^[0-9a-f]{32}$'),
  artifact_version integer CHECK(artifact_version IS NULL OR artifact_version>0),
  content_length bigint CHECK(content_length IS NULL OR content_length BETWEEN 1 AND 549755813888),
  sha256 char(64) CHECK(sha256 IS NULL OR sha256 ~ '^[0-9a-f]{64}$'),
  safe_file_name varchar(180),
  file_extension varchar(12) CHECK(file_extension IS NULL OR file_extension ~ '^\.[a-z0-9]{1,10}$'),
  extract_policy varchar(24) CHECK(extract_policy IS NULL OR extract_policy IN('NONE','EXTRACT_ARCHIVE')),
  manifest_identity char(64) CHECK(manifest_identity IS NULL OR manifest_identity ~ '^[0-9a-f]{64}$'),
  descriptor_hash char(64) CHECK(descriptor_hash IS NULL OR descriptor_hash ~ '^[0-9a-f]{64}$'),
  content_type varchar(128),
  source_etag varchar(512),
  source_last_modified varchar(128),
  status varchar(16) NOT NULL CHECK(status IN('READY','MAINTENANCE')),
  maintenance_reason varchar(64),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(catalog_identity,item_id),
  UNIQUE(catalog_identity,artifact_id,artifact_version),
  CHECK(safe_file_name !~ '[[:cntrl:]]' AND
        position('/' in safe_file_name)=0 AND position(chr(92) in safe_file_name)=0 AND
        position(':' in safe_file_name)=0 AND position('*' in safe_file_name)=0 AND
        position('?' in safe_file_name)=0 AND position('"' in safe_file_name)=0 AND
        position('<' in safe_file_name)=0 AND position('>' in safe_file_name)=0 AND
        position('|' in safe_file_name)=0 AND right(safe_file_name,1) NOT IN(' ','.')),
  CHECK(octet_length(safe_file_name)<=180),
  CHECK(lower(split_part(safe_file_name,'.',1)) NOT IN('con','prn','aux','nul') AND
        lower(split_part(safe_file_name,'.',1)) !~ '^(com|lpt)[1-9]$'),
  CHECK(manifest_identity IS NULL OR manifest_identity=catalog_identity),
  CHECK((status='READY' AND artifact_id=item_id AND artifact_version IS NOT NULL AND
         content_length IS NOT NULL AND sha256 IS NOT NULL AND safe_file_name IS NOT NULL AND
         file_extension IS NOT NULL AND extract_policy=visual_extract_policy AND manifest_identity IS NOT NULL AND
         descriptor_hash IS NOT NULL AND content_type IS NOT NULL AND maintenance_reason IS NULL)
     OR (status='MAINTENANCE' AND artifact_id IS NULL AND artifact_version IS NULL AND
         content_length IS NULL AND sha256 IS NULL AND safe_file_name IS NULL AND
         file_extension IS NULL AND extract_policy IS NULL AND manifest_identity IS NULL AND
         descriptor_hash IS NULL AND content_type IS NULL AND source_etag IS NULL AND
         source_last_modified IS NULL AND maintenance_reason='CONTENT_TEMPORARILY_UNAVAILABLE'))
);
CREATE INDEX ix_suite_content_items_catalog_order
  ON suite.suite_content_items(catalog_identity,display_order,item_id);

-- This private table is the only database object containing upstream locator
-- material.  The URL is always AES-256-GCM ciphertext, never plaintext.
CREATE TABLE suite.suite_content_artifact_origins(
  catalog_identity char(64) NOT NULL,
  item_id char(32) NOT NULL,
  upstream_url_ciphertext bytea NOT NULL CHECK(octet_length(upstream_url_ciphertext)>0),
  upstream_url_nonce bytea NOT NULL CHECK(octet_length(upstream_url_nonce)=12),
  upstream_url_tag bytea NOT NULL CHECK(octet_length(upstream_url_tag)=16),
  key_version integer NOT NULL CHECK(key_version>0),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(catalog_identity,item_id),
  FOREIGN KEY(catalog_identity,item_id)
    REFERENCES suite.suite_content_items(catalog_identity,item_id)
);

CREATE TABLE suite.suite_content_catalog_state(
  product_id varchar(64) PRIMARY KEY CHECK(product_id='TURBORAMA_SUITE'),
  active_catalog_identity char(64) NOT NULL REFERENCES suite.suite_content_snapshots(catalog_identity),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  CHECK(active_catalog_identity ~ '^[0-9a-f]{64}$')
);

ALTER TABLE suite.suite_license_deliveries
  ADD CONSTRAINT uq_suite_content_delivery_identity
  UNIQUE(source_system,source_purchase_id,source_item_key,product_id,license_id);

CREATE TABLE suite.suite_content_entitlements(
  license_id varchar(64) NOT NULL REFERENCES suite.suite_licenses(license_id),
  scope varchar(32) NOT NULL CHECK(scope='FULL_CATALOG'),
  status varchar(16) NOT NULL CHECK(status IN('ACTIVE','SUSPENDED','REVOKED')),
  source_system varchar(48) NOT NULL CHECK(source_system='TURBOBOX_V1'),
  source_purchase_id varchar(64) NOT NULL,
  source_item_key varchar(32) NOT NULL,
  product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(license_id,scope),
  FOREIGN KEY(source_system,source_purchase_id,source_item_key,product_id,license_id)
    REFERENCES suite.suite_license_deliveries(
      source_system,source_purchase_id,source_item_key,product_id,license_id)
);
CREATE UNIQUE INDEX ux_suite_content_entitlement_commerce_source
  ON suite.suite_content_entitlements(source_system,source_purchase_id,source_item_key)
  WHERE source_system='TURBOBOX_V1';

CREATE UNIQUE INDEX ux_suite_sessions_content_identity
  ON suite.suite_sessions(license_id,device_id,session_id);

CREATE TABLE suite.suite_content_grants(
  grant_id char(64) PRIMARY KEY CHECK(grant_id ~ '^[0-9a-f]{64}$'),
  token_digest char(64) NOT NULL UNIQUE CHECK(token_digest ~ '^[0-9a-f]{64}$'),
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL,
  session_id char(64) NOT NULL,
  revocation_generation bigint NOT NULL CHECK(revocation_generation>=0),
  authorized_until timestamptz NOT NULL,
  catalog_identity char(64) NOT NULL,
  item_id char(32) NOT NULL CHECK(item_id ~ '^[0-9a-f]{32}$'),
  artifact_id char(32) NOT NULL CHECK(artifact_id ~ '^[0-9a-f]{32}$'),
  artifact_version integer NOT NULL CHECK(artifact_version>0),
  manifest_identity char(64) NOT NULL,
  descriptor_hash char(64) NOT NULL CHECK(descriptor_hash ~ '^[0-9a-f]{64}$'),
  range_start bigint NOT NULL DEFAULT 0 CHECK(range_start>=0),
  content_length bigint NOT NULL CHECK(content_length BETWEEN 1 AND 549755813888),
  sha256 char(64) NOT NULL CHECK(sha256 ~ '^[0-9a-f]{64}$'),
  source_etag varchar(512),
  source_last_modified varchar(128),
  state varchar(16) NOT NULL CHECK(state IN('ISSUED','CLAIMED','COMPLETED','FAILED','REVOKED')),
  correlation_id varchar(64) NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  expires_at timestamptz NOT NULL,
  claimed_at timestamptz,
  completed_at timestamptz,
  failure_code varchar(64),
  FOREIGN KEY(license_id,device_id)
    REFERENCES suite.suite_devices(license_id,device_id),
  FOREIGN KEY(catalog_identity,item_id)
    REFERENCES suite.suite_content_items(catalog_identity,item_id),
  CHECK(manifest_identity=catalog_identity),
  CHECK(range_start<content_length),
  CHECK(expires_at>created_at),
  CONSTRAINT ck_suite_content_grants_maximum_ttl
    CHECK(expires_at<=created_at+interval '60 seconds'),
  CHECK(authorized_until>=created_at),
  CHECK((state='ISSUED' AND claimed_at IS NULL AND completed_at IS NULL AND failure_code IS NULL) OR
        (state='CLAIMED' AND claimed_at IS NOT NULL AND completed_at IS NULL AND failure_code IS NULL) OR
        (state='COMPLETED' AND claimed_at IS NOT NULL AND completed_at IS NOT NULL AND failure_code IS NULL) OR
        (state IN('FAILED','REVOKED') AND completed_at IS NOT NULL AND failure_code IS NOT NULL))
);

-- A session row is the mutable current-session pointer for one bound device,
-- while a grant must retain the exact session identity it was issued under.
-- Serialize pointer rotation and grant insertion with the same transaction
-- lock; never couple retained grant history to the mutable session-row key.
CREATE FUNCTION suite.lock_suite_content_session_mutation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,suite
AS $$
DECLARE bound_license_id varchar(64);
DECLARE bound_device_id char(64);
BEGIN
  IF TG_OP='DELETE' THEN
    bound_license_id:=OLD.license_id;
    bound_device_id:=OLD.device_id;
  ELSE
    bound_license_id:=NEW.license_id;
    bound_device_id:=NEW.device_id;
  END IF;
  IF TG_OP='UPDATE' AND
     (OLD.license_id<>NEW.license_id OR OLD.device_id<>NEW.device_id) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SESSION_IDENTITY_IMMUTABLE';
  END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended(
    'suite:content-session:'||bound_license_id||':'||bound_device_id,0));
  IF TG_OP='DELETE' THEN RETURN OLD; END IF;
  RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION suite.lock_suite_content_session_mutation() FROM PUBLIC;
CREATE TRIGGER tr_suite_content_session_mutation_lock
BEFORE INSERT OR UPDATE OR DELETE ON suite.suite_sessions
FOR EACH ROW EXECUTE FUNCTION suite.lock_suite_content_session_mutation();

CREATE FUNCTION suite.guard_suite_content_grant_session()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,suite
AS $$
BEGIN
  IF TG_OP='UPDATE' THEN
    IF (to_jsonb(NEW)-ARRAY['state','claimed_at','last_authorized_at',
         'completed_at','failure_code']::text[]) IS DISTINCT FROM
       (to_jsonb(OLD)-ARRAY['state','claimed_at','last_authorized_at',
         'completed_at','failure_code']::text[]) THEN
      RAISE EXCEPTION 'SUITE_CONTENT_GRANT_BINDING_IMMUTABLE';
    END IF;
    RETURN NEW;
  END IF;

  PERFORM pg_advisory_xact_lock(hashtextextended(
    'suite:content-session:'||NEW.license_id||':'||NEW.device_id,0));
  IF NOT EXISTS(
      SELECT 1 FROM suite.suite_sessions session
      JOIN suite.suite_licenses license ON license.license_id=session.license_id
      JOIN suite.suite_devices device ON device.license_id=session.license_id AND
        device.device_id=session.device_id
      WHERE session.license_id=NEW.license_id AND session.device_id=NEW.device_id AND
        session.session_id=NEW.session_id AND session.status='ACTIVE' AND
        session.revoked_at IS NULL AND session.authorized_until>statement_timestamp() AND
        NEW.authorized_until<=session.authorized_until AND
        session.revocation_generation=license.revocation_generation AND
        NEW.revocation_generation=license.revocation_generation AND
        license.status='ACTIVE' AND device.status='ACTIVE') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_GRANT_SESSION_INVALID';
  END IF;
  RETURN NEW;
END $$;
REVOKE ALL ON FUNCTION suite.guard_suite_content_grant_session() FROM PUBLIC;
CREATE TRIGGER tr_suite_content_grant_session_guard
BEFORE INSERT OR UPDATE ON suite.suite_content_grants
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_grant_session();

CREATE INDEX ix_suite_content_grants_expiry
  ON suite.suite_content_grants(expires_at) WHERE state IN('ISSUED','CLAIMED');
CREATE INDEX ix_suite_content_grants_session
  ON suite.suite_content_grants(license_id,device_id,session_id,created_at DESC);

INSERT INTO suite.schema_migrations(version)
VALUES('010_suite_content_catalog')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('010_suite_content_catalog', :'migration_sha256');
COMMIT;
