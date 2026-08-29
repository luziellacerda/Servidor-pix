BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:014_content_retention',0));

DO $$
BEGIN
  IF NOT EXISTS(
      SELECT 1 FROM pg_roles
      WHERE rolname='turborama-suite-content-maintenance') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_MAINTENANCE_ROLE_MISSING';
  END IF;
END $$;

ALTER TABLE suite.suite_content_snapshots
  ADD COLUMN origin_active_key_version integer,
  ADD COLUMN origin_key_set_fingerprint char(64),
  ADD COLUMN origin_allowlist_fingerprint char(64),
  ADD CONSTRAINT ck_suite_content_snapshot_origin_deployment CHECK(
    (origin_active_key_version IS NULL AND origin_key_set_fingerprint IS NULL AND
      origin_allowlist_fingerprint IS NULL) OR
    (origin_active_key_version>0 AND origin_key_set_fingerprint ~ '^[0-9a-f]{64}$' AND
      origin_allowlist_fingerprint ~ '^[0-9a-f]{64}$'));

DO $$
BEGIN
  IF EXISTS(SELECT 1 FROM suite.suite_content_artifact_origins
      WHERE octet_length(upstream_url_ciphertext) NOT BETWEEN 1 AND 4096) OR
     EXISTS(SELECT 1 FROM suite.suite_content_origin_candidates
      WHERE octet_length(upstream_url_ciphertext) NOT BETWEEN 1 AND 4096) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_URL_SIZE_PREFLIGHT_FAILED';
  END IF;
END $$;

ALTER TABLE suite.suite_content_artifact_origins
  ADD CONSTRAINT ck_suite_content_origin_url_size
    CHECK(octet_length(upstream_url_ciphertext) BETWEEN 1 AND 4096);

CREATE OR REPLACE FUNCTION suite.publish_suite_content_catalog(
  p_catalog_identity char(64))
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
DECLARE active_key_version integer;
DECLARE key_set_fingerprint char(64);
DECLARE allowlist_fingerprint char(64);
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-publish',0));
  SELECT s.item_count,s.ready_item_count,s.maintenance_item_count,s.status,
      s.origin_active_key_version,s.origin_key_set_fingerprint,
      s.origin_allowlist_fingerprint
    INTO expected_count,expected_ready_count,expected_maintenance_count,snapshot_status,
      active_key_version,key_set_fingerprint,allowlist_fingerprint
    FROM suite.suite_content_snapshots s
    WHERE s.catalog_identity=p_catalog_identity
    FOR UPDATE;
  IF expected_count IS NULL OR snapshot_status NOT IN('STAGING','PUBLISHED') OR
     active_key_version IS NULL OR key_set_fingerprint IS NULL OR
     allowlist_fingerprint IS NULL THEN
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
      SET active_catalog_identity=excluded.active_catalog_identity,
          updated_at=excluded.updated_at;
END $$;

ALTER TABLE suite.suite_content_grants
  ADD COLUMN last_authorized_at timestamptz;
UPDATE suite.suite_content_grants
SET last_authorized_at=claimed_at
WHERE claimed_at IS NOT NULL;

DO $$
BEGIN
  IF EXISTS(SELECT 1 FROM suite.suite_content_grants WHERE
      NOT ((state='ISSUED' AND claimed_at IS NULL AND completed_at IS NULL AND
              failure_code IS NULL) OR
           (state='CLAIMED' AND claimed_at IS NOT NULL AND completed_at IS NULL AND
              failure_code IS NULL) OR
           (state='COMPLETED' AND claimed_at IS NOT NULL AND completed_at IS NOT NULL AND
              failure_code IS NULL) OR
           (state IN('FAILED','REVOKED') AND completed_at IS NOT NULL AND
              failure_code IS NOT NULL))) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_GRANT_LIFECYCLE_PREFLIGHT_FAILED';
  END IF;
END $$;

DO $$
DECLARE v_constraint name;
BEGIN
  SELECT c.conname INTO v_constraint
  FROM pg_constraint c
  WHERE c.conrelid='suite.suite_content_grants'::regclass
    AND c.contype='c'
    AND pg_get_constraintdef(c.oid) LIKE '%claimed_at IS NULL%'
    AND pg_get_constraintdef(c.oid) LIKE '%completed_at IS NULL%'
    AND pg_get_constraintdef(c.oid) LIKE '%failure_code IS NULL%';
  IF v_constraint IS NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_GRANT_LIFECYCLE_CONSTRAINT_MISSING';
  END IF;
  EXECUTE format('ALTER TABLE suite.suite_content_grants DROP CONSTRAINT %I',
    v_constraint);
END $$;

ALTER TABLE suite.suite_content_grants
  DROP CONSTRAINT suite_content_grants_state_check,
  ADD CONSTRAINT ck_suite_content_grants_state
    CHECK(state IN('ISSUED','CLAIMED','COMPLETED','FAILED','REVOKED','EXPIRED')),
  ADD CONSTRAINT ck_suite_content_grants_lifecycle CHECK(
    (state='ISSUED' AND claimed_at IS NULL AND completed_at IS NULL AND
      failure_code IS NULL) OR
    (state='CLAIMED' AND claimed_at IS NOT NULL AND completed_at IS NULL AND
      failure_code IS NULL) OR
    (state='COMPLETED' AND claimed_at IS NOT NULL AND completed_at IS NOT NULL AND
      failure_code IS NULL) OR
    (state IN('FAILED','REVOKED') AND completed_at IS NOT NULL AND
      failure_code IS NOT NULL) OR
    (state='EXPIRED' AND claimed_at IS NULL AND completed_at IS NOT NULL AND
      failure_code='EXPIRED')),
  ADD CONSTRAINT ck_suite_content_grants_authorization_heartbeat CHECK(
    (claimed_at IS NULL AND last_authorized_at IS NULL) OR
    (claimed_at IS NOT NULL AND last_authorized_at IS NOT NULL AND
      last_authorized_at>=claimed_at));

CREATE INDEX ix_suite_content_grants_claim_heartbeat
  ON suite.suite_content_grants(last_authorized_at,grant_id)
  WHERE state='CLAIMED';
CREATE INDEX ix_suite_content_grants_terminal_retention
  ON suite.suite_content_grants(completed_at,grant_id)
  WHERE state IN('COMPLETED','FAILED','REVOKED','EXPIRED');

ALTER TABLE suite.suite_content_origin_candidates
  ALTER COLUMN upstream_url_ciphertext DROP NOT NULL,
  ALTER COLUMN upstream_url_nonce DROP NOT NULL,
  ALTER COLUMN upstream_url_tag DROP NOT NULL,
  ALTER COLUMN key_version DROP NOT NULL,
  ADD COLUMN secret_destroyed_at timestamptz,
  ADD CONSTRAINT ck_suite_content_candidate_url_size CHECK(
    upstream_url_ciphertext IS NULL OR
    octet_length(upstream_url_ciphertext) BETWEEN 1 AND 4096),
  ADD CONSTRAINT ck_suite_content_candidate_secret_lifecycle CHECK(
    (secret_destroyed_at IS NULL AND upstream_url_ciphertext IS NOT NULL AND
      upstream_url_nonce IS NOT NULL AND upstream_url_tag IS NOT NULL AND
      key_version IS NOT NULL) OR
    (secret_destroyed_at IS NOT NULL AND state IN('REJECTED','SUPERSEDED','PUBLISHED') AND
      upstream_url_ciphertext IS NULL AND upstream_url_nonce IS NULL AND
      upstream_url_tag IS NULL AND key_version IS NULL));

CREATE INDEX ix_suite_content_candidates_secret_retention
  ON suite.suite_content_origin_candidates(
    (coalesce(published_at,updated_at)),candidate_id)
  WHERE state IN('REJECTED','SUPERSEDED','PUBLISHED')
    AND secret_destroyed_at IS NULL;

CREATE INDEX ix_suite_challenges_retention
  ON suite.suite_challenges(expires_at,challenge_id)
  WHERE consumed_at IS NOT NULL OR invalidated_at IS NOT NULL;

CREATE INDEX ix_suite_challenges_content_quota
  ON suite.suite_challenges(license_id,device_id,created_at DESC)
  WHERE action IN('catalog.read','download.authorize');
CREATE INDEX ix_suite_content_grants_active_quota
  ON suite.suite_content_grants(license_id,device_id,created_at DESC)
  WHERE state IN('ISSUED','CLAIMED');

CREATE FUNCTION suite.enforce_suite_content_challenge_quota(
  p_license_id varchar(64),p_device_id varchar(64),p_session_id varchar(64))
RETURNS void LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,suite AS $$
DECLARE v_now timestamptz:=statement_timestamp();
DECLARE recent_count bigint;
DECLARE active_count bigint;
BEGIN
  IF p_license_id IS NULL OR p_device_id IS NULL OR p_session_id IS NULL OR
     length(p_license_id) NOT BETWEEN 6 AND 64 OR
     p_device_id !~ '^[0-9a-f]{64}$' OR p_session_id !~ '^[0-9a-f]{64}$' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_QUOTA_INPUT_INVALID';
  END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended(
    'suite:content-challenge-quota:'||p_license_id||':'||p_device_id,0));
  IF NOT EXISTS(
      SELECT 1 FROM suite.suite_sessions session
      JOIN suite.suite_licenses license ON license.license_id=session.license_id
      JOIN suite.suite_devices device ON device.license_id=session.license_id AND
        device.device_id=session.device_id
      WHERE session.license_id=p_license_id AND session.device_id=p_device_id AND
        session.session_id=p_session_id AND session.status='ACTIVE' AND
        session.authorized_until>v_now AND
        session.revocation_generation=license.revocation_generation AND
        license.status='ACTIVE' AND device.status='ACTIVE') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_SESSION_INVALID';
  END IF;
  SELECT count(*) INTO recent_count FROM suite.suite_challenges
  WHERE license_id=p_license_id AND device_id=p_device_id
    AND action IN('catalog.read','download.authorize')
    AND created_at>=v_now-interval '1 minute';
  SELECT count(*) INTO active_count FROM suite.suite_challenges
  WHERE license_id=p_license_id AND device_id=p_device_id
    AND action IN('catalog.read','download.authorize')
    AND consumed_at IS NULL AND invalidated_at IS NULL AND expires_at>v_now;
  IF recent_count>=120 OR active_count>=128 THEN
    RAISE EXCEPTION 'SUITE_CONTENT_CHALLENGE_QUOTA_EXCEEDED';
  END IF;
END $$;

CREATE FUNCTION suite.enforce_suite_content_grant_quota(
  p_license_id varchar(64),p_device_id char(64),p_session_id char(64))
RETURNS void LANGUAGE plpgsql SECURITY DEFINER
SET search_path=pg_catalog,suite AS $$
DECLARE v_now timestamptz:=statement_timestamp();
DECLARE recent_count bigint;
DECLARE active_count bigint;
BEGIN
  IF p_license_id IS NULL OR p_device_id IS NULL OR p_session_id IS NULL OR
     length(p_license_id) NOT BETWEEN 6 AND 64 OR
     p_device_id !~ '^[0-9a-f]{64}$' OR p_session_id !~ '^[0-9a-f]{64}$' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_QUOTA_INPUT_INVALID';
  END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended(
    'suite:content-grant-quota:'||p_license_id||':'||p_device_id,0));
  SELECT count(*) INTO recent_count FROM suite.suite_content_grants
  WHERE license_id=p_license_id AND device_id=p_device_id
    AND created_at>=v_now-interval '1 minute';
  SELECT count(*) INTO active_count FROM suite.suite_content_grants
  WHERE license_id=p_license_id AND device_id=p_device_id
    AND (state='CLAIMED' OR (state='ISSUED' AND expires_at>v_now));
  IF recent_count>=30 OR active_count>=16 THEN
    RAISE EXCEPTION 'SUITE_CONTENT_GRANT_QUOTA_EXCEEDED';
  END IF;
END $$;

CREATE OR REPLACE FUNCTION suite.guard_suite_content_candidate_mutation()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,suite AS $$
BEGIN
  IF TG_OP='UPDATE' AND OLD.state IN('REJECTED','SUPERSEDED','PUBLISHED') AND
     coalesce(OLD.published_at,OLD.updated_at)<statement_timestamp()-interval '30 days' AND
     OLD.secret_destroyed_at IS NULL AND NEW.secret_destroyed_at=statement_timestamp() AND
     OLD.upstream_url_ciphertext IS NOT NULL AND OLD.upstream_url_nonce IS NOT NULL AND
     OLD.upstream_url_tag IS NOT NULL AND OLD.key_version IS NOT NULL AND
     NEW.upstream_url_ciphertext IS NULL AND NEW.upstream_url_nonce IS NULL AND
     NEW.upstream_url_tag IS NULL AND NEW.key_version IS NULL AND
     (to_jsonb(NEW)-ARRAY['upstream_url_ciphertext','upstream_url_nonce',
       'upstream_url_tag','key_version','secret_destroyed_at']::text[])=
     (to_jsonb(OLD)-ARRAY['upstream_url_ciphertext','upstream_url_nonce',
       'upstream_url_tag','key_version','secret_destroyed_at']::text[]) THEN
    RETURN NEW;
  END IF;
  IF TG_OP='DELETE' OR OLD.candidate_id<>NEW.candidate_id OR OLD.product_id<>NEW.product_id OR
     OLD.item_id<>NEW.item_id OR OLD.base_catalog_identity<>NEW.base_catalog_identity OR
     OLD.request_id<>NEW.request_id OR OLD.upstream_url_ciphertext<>NEW.upstream_url_ciphertext OR
     OLD.upstream_url_nonce<>NEW.upstream_url_nonce OR OLD.upstream_url_tag<>NEW.upstream_url_tag OR
     OLD.key_version<>NEW.key_version OR OLD.change_intent<>NEW.change_intent OR
     OLD.change_reason IS DISTINCT FROM NEW.change_reason OR
     OLD.expected_content_length IS DISTINCT FROM NEW.expected_content_length OR
     OLD.expected_sha256 IS DISTINCT FROM NEW.expected_sha256 OR
     OLD.expected_artifact_version IS DISTINCT FROM NEW.expected_artifact_version OR
     OLD.expected_file_extension IS DISTINCT FROM NEW.expected_file_extension OR
     OLD.expected_extract_policy IS DISTINCT FROM NEW.expected_extract_policy OR
     OLD.submitted_by<>NEW.submitted_by OR OLD.submitted_at<>NEW.submitted_at OR
     OLD.secret_destroyed_at IS DISTINCT FROM NEW.secret_destroyed_at OR
     NEW.attempt_count<OLD.attempt_count THEN
    RAISE EXCEPTION 'SUITE_CONTENT_CANDIDATE_IMMUTABLE_FIELD';
  END IF;
  IF NOT ((OLD.state='STAGED' AND NEW.state='VALIDATING') OR
          (OLD.state='VALIDATING' AND NEW.state IN('VALIDATING','VERIFIED','REJECTED','SUPERSEDED')) OR
          (OLD.state='VERIFIED' AND NEW.state IN('VALIDATING','PUBLISHED','SUPERSEDED'))) THEN
    RAISE EXCEPTION 'SUITE_CONTENT_CANDIDATE_STATE_TRANSITION';
  END IF;
  RETURN NEW;
END $$;

CREATE FUNCTION suite.run_suite_content_retention(p_batch_size integer)
RETURNS TABLE(
  expired_issued_count bigint,
  stale_claim_count bigint,
  deleted_terminal_count bigint,
  shredded_candidate_count bigint,
  deleted_challenge_count bigint,
  remaining_grant_backlog bigint,
  remaining_candidate_backlog bigint,
  remaining_challenge_backlog bigint)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path=pg_catalog,suite
AS $$
DECLARE
  v_now timestamptz:=statement_timestamp();
  v_expired bigint:=0;
  v_stale bigint:=0;
  v_deleted bigint:=0;
  v_shredded bigint:=0;
  v_deleted_challenges bigint:=0;
  v_grant_backlog bigint:=0;
  v_candidate_backlog bigint:=0;
  v_challenge_backlog bigint:=0;
BEGIN
  IF p_batch_size IS NULL OR p_batch_size<1 OR p_batch_size>10000 THEN
    RAISE EXCEPTION 'SUITE_CONTENT_RETENTION_BATCH_INVALID';
  END IF;

  WITH selected AS(
    SELECT grant_id FROM suite.suite_content_grants
    WHERE state='ISSUED' AND expires_at<v_now
    ORDER BY expires_at,grant_id FOR UPDATE SKIP LOCKED LIMIT p_batch_size)
  UPDATE suite.suite_content_grants grant_row
  SET state='EXPIRED',completed_at=v_now,failure_code='EXPIRED'
  FROM selected WHERE grant_row.grant_id=selected.grant_id;
  GET DIAGNOSTICS v_expired=ROW_COUNT;

  WITH selected AS(
    SELECT grant_id FROM suite.suite_content_grants
    WHERE state='CLAIMED' AND
      last_authorized_at<v_now-interval '15 minutes'
    ORDER BY last_authorized_at,grant_id
    FOR UPDATE SKIP LOCKED LIMIT p_batch_size)
  UPDATE suite.suite_content_grants grant_row
  SET state='FAILED',completed_at=v_now,failure_code='STALE_CLAIM'
  FROM selected WHERE grant_row.grant_id=selected.grant_id;
  GET DIAGNOSTICS v_stale=ROW_COUNT;

  WITH selected AS(
    SELECT grant_id FROM suite.suite_content_grants
    WHERE state IN('COMPLETED','FAILED','REVOKED','EXPIRED') AND
      completed_at<v_now-interval '90 days'
    ORDER BY completed_at,grant_id
    FOR UPDATE SKIP LOCKED LIMIT p_batch_size)
  DELETE FROM suite.suite_content_grants grant_row USING selected
  WHERE grant_row.grant_id=selected.grant_id;
  GET DIAGNOSTICS v_deleted=ROW_COUNT;

  WITH selected AS(
    SELECT candidate_id FROM suite.suite_content_origin_candidates
    WHERE state IN('REJECTED','SUPERSEDED','PUBLISHED') AND
      secret_destroyed_at IS NULL AND
      coalesce(published_at,updated_at)<v_now-interval '30 days'
    ORDER BY coalesce(published_at,updated_at),candidate_id
    FOR UPDATE SKIP LOCKED LIMIT p_batch_size)
  UPDATE suite.suite_content_origin_candidates candidate
  SET upstream_url_ciphertext=NULL,upstream_url_nonce=NULL,
      upstream_url_tag=NULL,key_version=NULL,secret_destroyed_at=statement_timestamp()
  FROM selected WHERE candidate.candidate_id=selected.candidate_id;
  GET DIAGNOSTICS v_shredded=ROW_COUNT;

  -- Activation completions are permanent licensing audit records and retain
  -- their FK challenge. All other consumed/invalidated/expired challenges are
  -- bounded after a 30-day operational audit window.
  WITH selected AS(
    SELECT challenge.challenge_id
    FROM suite.suite_challenges challenge
    WHERE challenge.expires_at<v_now-interval '30 days'
      AND NOT EXISTS(SELECT 1 FROM suite.suite_activation_completions completion
        WHERE completion.challenge_id=challenge.challenge_id)
    ORDER BY challenge.expires_at,challenge.challenge_id
    FOR UPDATE OF challenge SKIP LOCKED LIMIT p_batch_size)
  DELETE FROM suite.suite_challenges challenge USING selected
  WHERE challenge.challenge_id=selected.challenge_id;
  GET DIAGNOSTICS v_deleted_challenges=ROW_COUNT;

  SELECT count(*) INTO v_grant_backlog
  FROM suite.suite_content_grants
  WHERE (state='ISSUED' AND expires_at<v_now) OR
    (state='CLAIMED' AND last_authorized_at<v_now-interval '15 minutes') OR
    (state IN('COMPLETED','FAILED','REVOKED','EXPIRED') AND
      completed_at<v_now-interval '90 days');
  SELECT count(*) INTO v_candidate_backlog
  FROM suite.suite_content_origin_candidates
  WHERE state IN('REJECTED','SUPERSEDED','PUBLISHED') AND
    secret_destroyed_at IS NULL AND
    coalesce(published_at,updated_at)<v_now-interval '30 days';
  SELECT count(*) INTO v_challenge_backlog
  FROM suite.suite_challenges challenge
  WHERE challenge.expires_at<v_now-interval '30 days'
    AND NOT EXISTS(SELECT 1 FROM suite.suite_activation_completions completion
      WHERE completion.challenge_id=challenge.challenge_id);

  RETURN QUERY SELECT v_expired,v_stale,v_deleted,v_shredded,
    v_deleted_challenges,v_grant_backlog,v_candidate_backlog,
    v_challenge_backlog;
END $$;

REVOKE ALL ON FUNCTION suite.run_suite_content_retention(integer) FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.enforce_suite_content_challenge_quota(
  varchar(64),varchar(64),varchar(64)) FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.enforce_suite_content_grant_quota(
  varchar(64),char(64),char(64)) FROM PUBLIC;
REVOKE ALL ON suite.suite_content_grants,
  suite.suite_content_origin_candidates,suite.suite_challenges
  FROM "turborama-suite-content-maintenance";
GRANT USAGE ON SCHEMA suite TO "turborama-suite-content-maintenance","turborama-suite";
GRANT SELECT ON suite.schema_migrations TO "turborama-suite-content-maintenance";
GRANT SELECT ON suite.schema_migration_checksums TO "turborama-suite-content-maintenance";
GRANT EXECUTE ON FUNCTION suite.run_suite_content_retention(integer)
  TO "turborama-suite-content-maintenance";
GRANT EXECUTE ON FUNCTION suite.enforce_suite_content_challenge_quota(
  varchar(64),varchar(64),varchar(64)) TO "turborama-suite";
GRANT EXECUTE ON FUNCTION suite.enforce_suite_content_grant_quota(
  varchar(64),char(64),char(64)) TO "turborama-suite-content-api";
GRANT UPDATE(last_authorized_at) ON suite.suite_content_grants
  TO "turborama-suite-gateway";

INSERT INTO suite.schema_migrations(version)
VALUES('014_suite_content_retention')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('014_suite_content_retention', :'migration_sha256');
COMMIT;
