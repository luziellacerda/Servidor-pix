BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:013_content_management',0));

DO $$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-admin') OR
     NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-monitor') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_MANAGEMENT_ROLES_MISSING';
  END IF;
END $$;

CREATE TABLE suite.suite_content_item_health(
  product_id varchar(64) NOT NULL CHECK(product_id='TURBORAMA_SUITE'),
  item_id char(32) NOT NULL CHECK(item_id ~ '^[0-9a-f]{32}$'),
  observed_catalog_identity char(64) NOT NULL CHECK(observed_catalog_identity ~ '^[0-9a-f]{64}$'),
  observed_availability varchar(16) NOT NULL CHECK(observed_availability IN('READY','MAINTENANCE')),
  last_checked_at timestamptz,
  last_success_at timestamptz,
  last_full_validation_at timestamptz,
  last_terminal_failure_at timestamptz,
  next_check_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  consecutive_terminal_failures integer NOT NULL DEFAULT 0 CHECK(consecutive_terminal_failures BETWEEN 0 AND 1000000),
  consecutive_successes integer NOT NULL DEFAULT 0 CHECK(consecutive_successes BETWEEN 0 AND 1000000),
  last_result_code varchar(64) NOT NULL DEFAULT 'NOT_CHECKED' CHECK(last_result_code IN(
    'NOT_CHECKED','CHECK_OK','ORIGIN_TERMINAL_UNAVAILABLE','ORIGIN_TERMINAL_EMPTY',
    'ORIGIN_REQUEST_FAILED','ORIGIN_RETRYABLE_STATUS','ORIGIN_LENGTH_MISMATCH',
    'ORIGIN_LENGTH_CHANGED','ORIGIN_HEADER_TIMEOUT','ORIGIN_READ_IDLE_TIMEOUT',
    'ORIGIN_TOTAL_TIMEOUT','SECURITY_CYCLE_ABORTED','NO_ACTIVE_ORIGIN','CANDIDATE_PUBLISHED')),
  row_version bigint NOT NULL DEFAULT 1 CHECK(row_version>0),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  PRIMARY KEY(product_id,item_id)
);
CREATE INDEX ix_suite_content_health_due
  ON suite.suite_content_item_health(next_check_at,item_id);

CREATE TABLE suite.suite_content_origin_candidates(
  candidate_id char(64) PRIMARY KEY CHECK(candidate_id ~ '^[0-9a-f]{64}$'),
  product_id varchar(64) NOT NULL DEFAULT 'TURBORAMA_SUITE' CHECK(product_id='TURBORAMA_SUITE'),
  item_id char(32) NOT NULL CHECK(item_id ~ '^[0-9a-f]{32}$'),
  base_catalog_identity char(64) NOT NULL REFERENCES suite.suite_content_snapshots(catalog_identity),
  request_id varchar(128) NOT NULL UNIQUE CHECK(request_id ~ '^[A-Za-z0-9_-]{16,128}$'),
  state varchar(16) NOT NULL CHECK(state IN('STAGED','VALIDATING','VERIFIED','REJECTED','SUPERSEDED','PUBLISHED')),
  upstream_url_ciphertext bytea NOT NULL CHECK(octet_length(upstream_url_ciphertext) BETWEEN 1 AND 8192),
  upstream_url_nonce bytea NOT NULL CHECK(octet_length(upstream_url_nonce)=12),
  upstream_url_tag bytea NOT NULL CHECK(octet_length(upstream_url_tag)=16),
  key_version integer NOT NULL CHECK(key_version>0),
  change_intent varchar(32) NOT NULL CHECK(change_intent IN(
    'MIRROR_REPLACEMENT','INITIAL_RECOVERY','NEW_ARTIFACT_VERSION')),
  change_reason varchar(48) CHECK(change_reason IS NULL OR change_reason IN(
    'VENDOR_RELEASE','SECURITY_UPDATE','CONTENT_CORRECTION','PLATFORM_UPDATE')),
  expected_content_length bigint CHECK(expected_content_length IS NULL OR expected_content_length BETWEEN 1 AND 549755813888),
  expected_sha256 char(64) CHECK(expected_sha256 IS NULL OR expected_sha256 ~ '^[0-9a-f]{64}$'),
  expected_artifact_version integer CHECK(expected_artifact_version IS NULL OR expected_artifact_version>0),
  expected_file_extension varchar(12) CHECK(expected_file_extension IS NULL OR expected_file_extension ~ '^\.[a-z0-9]{1,10}$'),
  expected_extract_policy varchar(24) NOT NULL CHECK(expected_extract_policy IN('NONE','EXTRACT_ARCHIVE')),
  verified_content_length bigint CHECK(verified_content_length IS NULL OR verified_content_length BETWEEN 1 AND 549755813888),
  verified_sha256 char(64) CHECK(verified_sha256 IS NULL OR verified_sha256 ~ '^[0-9a-f]{64}$'),
  verified_file_extension varchar(12) CHECK(verified_file_extension IS NULL OR verified_file_extension ~ '^\.[a-z0-9]{1,10}$'),
  verified_safe_file_name varchar(180),
  verified_extract_policy varchar(24) CHECK(verified_extract_policy IS NULL OR verified_extract_policy IN('NONE','EXTRACT_ARCHIVE')),
  verified_content_type varchar(128),
  verified_source_etag varchar(512),
  verified_source_last_modified varchar(128),
  published_catalog_identity char(64),
  submitted_by varchar(64) NOT NULL CHECK(submitted_by ~ '^[A-Za-z0-9@._-]{1,64}$'),
  submitted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  verified_at timestamptz,
  published_at timestamptz,
  attempt_count integer NOT NULL DEFAULT 0 CHECK(attempt_count BETWEEN 0 AND 1000000),
  lease_owner char(64) CHECK(lease_owner IS NULL OR lease_owner ~ '^[0-9a-f]{64}$'),
  lease_expires_at timestamptz,
  internal_result_code varchar(64) NOT NULL DEFAULT 'STAGED' CHECK(internal_result_code IN(
    'STAGED','VALIDATION_OK','URL_POLICY_DENIED','HOST_DENIED','DNS_ADDRESS_DENIED',
    'TLS_SECURITY_FAILURE','REDIRECT_DENIED','CONTENT_ENCODING_DENIED','TERMINAL_UNAVAILABLE',
    'TERMINAL_EMPTY','REQUEST_FAILED','RETRYABLE_STATUS','HEADER_TIMEOUT','READ_IDLE_TIMEOUT',
    'TOTAL_TIMEOUT','LENGTH_CHANGED','LENGTH_MISMATCH','CONTENT_INVALID','MIRROR_LENGTH_MISMATCH',
    'MIRROR_SHA256_MISMATCH','MIRROR_EXTENSION_MISMATCH','SOURCE_CHANGED','LEASE_RECOVERED',
    'PUBLISH_FAILED','SUPERSEDED')),
  CHECK((lease_owner IS NULL)=(lease_expires_at IS NULL)),
  CHECK((change_intent='INITIAL_RECOVERY' AND expected_content_length IS NULL AND
         expected_sha256 IS NULL AND expected_artifact_version IS NULL AND change_reason IS NULL)
     OR (change_intent='MIRROR_REPLACEMENT' AND expected_content_length IS NOT NULL AND
         expected_sha256 IS NOT NULL AND expected_artifact_version IS NOT NULL AND
         expected_file_extension IS NOT NULL AND expected_extract_policy IS NOT NULL AND change_reason IS NULL)
     OR (change_intent='NEW_ARTIFACT_VERSION' AND expected_artifact_version IS NOT NULL AND
         change_reason IS NOT NULL)),
  CHECK((state IN('VERIFIED','PUBLISHED') AND verified_content_length IS NOT NULL AND
         verified_sha256 IS NOT NULL AND verified_file_extension IS NOT NULL AND
         verified_safe_file_name IS NOT NULL AND verified_extract_policy IS NOT NULL AND
         verified_content_type IS NOT NULL AND verified_at IS NOT NULL)
     OR (state NOT IN('VERIFIED','PUBLISHED'))),
  CHECK(verified_extract_policy IS NULL OR verified_extract_policy=expected_extract_policy),
  CHECK((state='PUBLISHED' AND published_catalog_identity IS NOT NULL AND published_at IS NOT NULL)
     OR (state<>'PUBLISHED' AND published_catalog_identity IS NULL AND published_at IS NULL)),
  CHECK(verified_safe_file_name IS NULL OR (
    octet_length(verified_safe_file_name)<=180 AND verified_safe_file_name !~ '[[:cntrl:]]' AND
    position('/' in verified_safe_file_name)=0 AND position(chr(92) in verified_safe_file_name)=0 AND
    position(':' in verified_safe_file_name)=0 AND position('*' in verified_safe_file_name)=0 AND
    position('?' in verified_safe_file_name)=0 AND position('"' in verified_safe_file_name)=0 AND
    position('<' in verified_safe_file_name)=0 AND position('>' in verified_safe_file_name)=0 AND
    position('|' in verified_safe_file_name)=0 AND right(verified_safe_file_name,1) NOT IN(' ','.')))
);
CREATE INDEX ix_suite_content_candidates_work
  ON suite.suite_content_origin_candidates(state,lease_expires_at,submitted_at,candidate_id)
  WHERE state IN('STAGED','VALIDATING','VERIFIED');
CREATE INDEX ix_suite_content_candidates_item
  ON suite.suite_content_origin_candidates(item_id,submitted_at DESC,candidate_id DESC);

CREATE TABLE suite.suite_content_management_audit(
  audit_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  event_type varchar(64) NOT NULL CHECK(event_type IN(
    'CONTENT_CANDIDATE_STAGED','CONTENT_NEW_VERSION_REQUESTED','CONTENT_CHECK_REQUESTED',
    'CONTENT_CANDIDATE_VALIDATING','CONTENT_CANDIDATE_REJECTED','CONTENT_CANDIDATE_VERIFIED',
    'CONTENT_CANDIDATE_PUBLISHED','CONTENT_ITEM_MAINTENANCE','CONTENT_MASS_FAILURE_GUARD',
    'CONTENT_SECURITY_CYCLE_ABORTED','CONTENT_AUTH_DENIED','CONTENT_CSRF_DENIED',
    'CONTENT_STEP_UP_DENIED','CONTENT_RATE_LIMITED','CONTENT_WORKER_CYCLE_STARTED',
    'CONTENT_WORKER_CYCLE_COMPLETED','CONTENT_WORKER_CYCLE_FAILED')),
  actor varchar(64) NOT NULL CHECK(actor ~ '^[A-Za-z0-9@._-]{1,64}$'),
  item_id char(32) CHECK(item_id IS NULL OR item_id ~ '^[0-9a-f]{32}$'),
  candidate_id char(64) CHECK(candidate_id IS NULL OR candidate_id ~ '^[0-9a-f]{64}$'),
  job_id char(64) CHECK(job_id IS NULL OR job_id ~ '^[0-9a-f]{64}$'),
  correlation_id varchar(128) NOT NULL CHECK(correlation_id ~ '^[A-Za-z0-9_-]{16,128}$'),
  request_id varchar(128) CHECK(request_id IS NULL OR request_id ~ '^[A-Za-z0-9_-]{16,128}$'),
  outcome varchar(16) NOT NULL CHECK(outcome IN('ACCEPTED','SUCCESS','REJECTED','DENIED','BLOCKED','FAILED')),
  detail_code varchar(64) NOT NULL CHECK(detail_code IN(
    'CANDIDATE_STAGED','CHECK_QUEUED','VALIDATION_STARTED','CANDIDATE_REJECTED',
    'CANDIDATE_VERIFIED','CANDIDATE_PUBLISHED','ITEM_MAINTENANCE','MASS_FAILURE_GUARD',
    'SECURITY_CYCLE_ABORTED','AUTH_REQUIRED','CLAIM_DENIED','CSRF_DENIED',
    'STEP_UP_REQUIRED','RATE_LIMITED','CYCLE_STARTED','CYCLE_COMPLETED','CYCLE_FAILED')),
  previous_catalog_identity char(64) CHECK(previous_catalog_identity IS NULL OR previous_catalog_identity ~ '^[0-9a-f]{64}$'),
  new_catalog_identity char(64) CHECK(new_catalog_identity IS NULL OR new_catalog_identity ~ '^[0-9a-f]{64}$'),
  occurred_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
CREATE UNIQUE INDEX ux_suite_content_audit_request_event
  ON suite.suite_content_management_audit(event_type,request_id) WHERE request_id IS NOT NULL;
CREATE INDEX ix_suite_content_audit_recent
  ON suite.suite_content_management_audit(occurred_at DESC,audit_id DESC);
CREATE INDEX ix_suite_content_audit_item
  ON suite.suite_content_management_audit(item_id,occurred_at DESC,audit_id DESC)
  WHERE item_id IS NOT NULL;

CREATE TABLE suite.suite_content_alert_outbox(
  alert_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  deduplication_key char(64) NOT NULL UNIQUE CHECK(deduplication_key ~ '^[0-9a-f]{64}$'),
  item_id char(32) CHECK(item_id IS NULL OR item_id ~ '^[0-9a-f]{32}$'),
  severity varchar(16) NOT NULL CHECK(severity IN('INFO','WARNING','CRITICAL')),
  alert_code varchar(64) NOT NULL CHECK(alert_code IN(
    'CONTENT_CANDIDATE_REJECTED','CONTENT_ITEM_MAINTENANCE','CONTENT_MASS_FAILURE_GUARD',
    'CONTENT_SECURITY_CYCLE_ABORTED','CONTENT_WORKER_CYCLE_FAILED')),
  occurrence_count integer NOT NULL DEFAULT 1 CHECK(occurrence_count>0),
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  delivered_at timestamptz,
  delivery_attempts integer NOT NULL DEFAULT 0 CHECK(delivery_attempts>=0)
);
CREATE INDEX ix_suite_content_alert_pending
  ON suite.suite_content_alert_outbox(created_at,alert_id) WHERE delivered_at IS NULL;

CREATE TABLE suite.suite_content_monitor_state(
  product_id varchar(64) PRIMARY KEY CHECK(product_id='TURBORAMA_SUITE'),
  last_cycle_id char(64) CHECK(last_cycle_id IS NULL OR last_cycle_id ~ '^[0-9a-f]{64}$'),
  last_started_at timestamptz,
  last_completed_at timestamptz,
  last_outcome varchar(16) NOT NULL DEFAULT 'NEVER' CHECK(last_outcome IN('NEVER','RUNNING','SUCCESS','FAILED','BLOCKED')),
  last_result_code varchar(64) NOT NULL DEFAULT 'NOT_RUN' CHECK(last_result_code ~ '^[A-Z0-9_]{2,64}$'),
  updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);
INSERT INTO suite.suite_content_monitor_state(product_id) VALUES('TURBORAMA_SUITE');

CREATE VIEW suite.suite_content_management_items
WITH (security_barrier=true) AS
SELECT i.item_id,i.display_name,i.status AS availability,i.artifact_version,h.last_checked_at,
  coalesce(h.last_result_code,'NOT_CHECKED') AS last_result_code,
  c.state AS job_state,c.candidate_id,c.updated_at AS job_updated_at
FROM suite.suite_content_catalog_state cs
JOIN suite.suite_content_items i ON i.catalog_identity=cs.active_catalog_identity
LEFT JOIN suite.suite_content_item_health h
  ON h.product_id=cs.product_id AND h.item_id=i.item_id
LEFT JOIN LATERAL(
  SELECT candidate_id,state,updated_at
  FROM suite.suite_content_origin_candidates candidate
  WHERE candidate.product_id=cs.product_id AND candidate.item_id=i.item_id
  ORDER BY submitted_at DESC,candidate_id DESC LIMIT 1
) c ON true
WHERE cs.product_id='TURBORAMA_SUITE';

CREATE FUNCTION suite.guard_suite_content_management_audit()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,suite AS $$
BEGIN
  RAISE EXCEPTION 'SUITE_CONTENT_AUDIT_APPEND_ONLY';
END $$;
CREATE TRIGGER tr_suite_content_management_audit_append_only
BEFORE UPDATE OR DELETE ON suite.suite_content_management_audit
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_management_audit();

CREATE FUNCTION suite.guard_suite_content_candidate_mutation()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,suite AS $$
BEGIN
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
CREATE TRIGGER tr_suite_content_candidate_guard
BEFORE UPDATE OR DELETE ON suite.suite_content_origin_candidates
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_candidate_mutation();

CREATE FUNCTION suite.guard_suite_content_health_mutation()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,suite AS $$
BEGIN
  IF TG_OP='DELETE' OR OLD.product_id<>NEW.product_id OR OLD.item_id<>NEW.item_id OR
     NEW.row_version<>OLD.row_version+1 THEN
    RAISE EXCEPTION 'SUITE_CONTENT_HEALTH_VERSION_CONFLICT';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER tr_suite_content_health_guard
BEFORE UPDATE OR DELETE ON suite.suite_content_item_health
FOR EACH ROW EXECUTE FUNCTION suite.guard_suite_content_health_mutation();

CREATE FUNCTION suite.get_suite_content_candidate_context(
  p_item_id char(32), p_change_intent varchar(32))
RETURNS TABLE(
  base_catalog_identity char(64), current_availability varchar(16),
  expected_content_length bigint, expected_sha256 char(64), expected_artifact_version integer,
  expected_file_extension varchar(12), expected_extract_policy varchar(24),
  resolved_change_intent varchar(32))
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
DECLARE v_base char(64); v_current varchar(16); v_ready suite.suite_content_items%ROWTYPE;
DECLARE v_visual_extract_policy varchar(24);
DECLARE v_intent varchar(32);
BEGIN
  IF p_item_id !~ '^[0-9a-f]{32}$' OR p_change_intent NOT IN(
      'AUTO_REPLACEMENT','MIRROR_REPLACEMENT','INITIAL_RECOVERY','NEW_ARTIFACT_VERSION') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_CANDIDATE_INVALID';
  END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-management',0));
  SELECT s.active_catalog_identity INTO v_base
    FROM suite.suite_content_catalog_state s WHERE s.product_id='TURBORAMA_SUITE' FOR SHARE;
  IF v_base IS NULL THEN RAISE EXCEPTION 'SUITE_CONTENT_CATALOG_NOT_PUBLISHED'; END IF;
  SELECT i.status,i.visual_extract_policy INTO v_current,v_visual_extract_policy
    FROM suite.suite_content_items i
    WHERE i.catalog_identity=v_base AND i.item_id=p_item_id;
  IF v_current IS NULL THEN RAISE EXCEPTION 'SUITE_CONTENT_ITEM_NOT_FOUND'; END IF;
  IF v_current='READY' THEN
    SELECT i.* INTO v_ready FROM suite.suite_content_items i
      WHERE i.catalog_identity=v_base AND i.item_id=p_item_id AND i.status='READY';
  ELSE
    SELECT i.* INTO v_ready FROM suite.suite_content_items i
      JOIN suite.suite_content_snapshots s ON s.catalog_identity=i.catalog_identity
      WHERE i.item_id=p_item_id AND i.status='READY' AND s.status='PUBLISHED'
      ORDER BY s.catalog_sequence DESC LIMIT 1;
  END IF;
  v_intent=CASE WHEN p_change_intent='AUTO_REPLACEMENT' AND v_ready.item_id IS NULL
    THEN 'INITIAL_RECOVERY' WHEN p_change_intent='AUTO_REPLACEMENT'
    THEN 'MIRROR_REPLACEMENT' ELSE p_change_intent END;
  IF v_intent='INITIAL_RECOVERY' AND v_ready.item_id IS NOT NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_INITIAL_RECOVERY_DENIED';
  ELSIF v_intent='INITIAL_RECOVERY' AND v_current<>'MAINTENANCE' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_INITIAL_RECOVERY_DENIED';
  ELSIF v_intent='MIRROR_REPLACEMENT' AND v_ready.item_id IS NULL THEN
    RAISE EXCEPTION 'SUITE_CONTENT_MIRROR_REQUIRES_DESCRIPTOR';
  ELSIF v_intent='NEW_ARTIFACT_VERSION' AND v_current<>'READY' THEN
    RAISE EXCEPTION 'SUITE_CONTENT_NEW_VERSION_REQUIRES_READY';
  END IF;
  RETURN QUERY SELECT v_base,v_current,v_ready.content_length,v_ready.sha256,
    v_ready.artifact_version,v_ready.file_extension,v_visual_extract_policy,v_intent;
END $$;

CREATE FUNCTION suite.submit_suite_content_origin_candidate(
  p_candidate_id char(64), p_item_id char(32), p_base_catalog_identity char(64),
  p_request_id varchar(128), p_ciphertext bytea, p_nonce bytea, p_tag bytea,
  p_key_version integer, p_change_intent varchar(32), p_change_reason varchar(48),
  p_submitted_by varchar(64))
RETURNS TABLE(candidate_id char(64), state varchar(16))
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
DECLARE v_context record; v_existing suite.suite_content_origin_candidates%ROWTYPE;
DECLARE v_event varchar(64);
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-management',0));
  SELECT * INTO v_context FROM suite.get_suite_content_candidate_context(p_item_id,p_change_intent);
  IF v_context.base_catalog_identity<>p_base_catalog_identity THEN
    RAISE EXCEPTION 'SUITE_CONTENT_CATALOG_CHANGED';
  END IF;
  SELECT * INTO v_existing FROM suite.suite_content_origin_candidates c WHERE c.request_id=p_request_id;
  IF v_existing.candidate_id IS NOT NULL THEN
    IF v_existing.item_id<>p_item_id OR v_existing.base_catalog_identity<>p_base_catalog_identity OR
       v_existing.change_intent<>p_change_intent OR v_existing.submitted_by<>p_submitted_by THEN
      RAISE EXCEPTION 'SUITE_CONTENT_REQUEST_REPLAY';
    END IF;
    RETURN QUERY SELECT v_existing.candidate_id,v_existing.state;
    RETURN;
  END IF;
  INSERT INTO suite.suite_content_origin_candidates(
    candidate_id,item_id,base_catalog_identity,request_id,state,
    upstream_url_ciphertext,upstream_url_nonce,upstream_url_tag,key_version,
    change_intent,change_reason,expected_content_length,expected_sha256,
    expected_artifact_version,expected_file_extension,expected_extract_policy,submitted_by)
  VALUES(p_candidate_id,p_item_id,p_base_catalog_identity,p_request_id,'STAGED',
    p_ciphertext,p_nonce,p_tag,p_key_version,p_change_intent,p_change_reason,
    CASE WHEN p_change_intent='INITIAL_RECOVERY' THEN NULL ELSE v_context.expected_content_length END,
    CASE WHEN p_change_intent='INITIAL_RECOVERY' THEN NULL ELSE v_context.expected_sha256 END,
    CASE WHEN p_change_intent='INITIAL_RECOVERY' THEN NULL ELSE v_context.expected_artifact_version END,
    CASE WHEN p_change_intent='INITIAL_RECOVERY' THEN NULL ELSE v_context.expected_file_extension END,
    v_context.expected_extract_policy,
    p_submitted_by);
  v_event=CASE WHEN p_change_intent='NEW_ARTIFACT_VERSION'
    THEN 'CONTENT_NEW_VERSION_REQUESTED' ELSE 'CONTENT_CANDIDATE_STAGED' END;
  INSERT INTO suite.suite_content_management_audit(
    event_type,actor,item_id,candidate_id,correlation_id,request_id,outcome,detail_code,
    previous_catalog_identity)
  VALUES(v_event,p_submitted_by,p_item_id,p_candidate_id,p_request_id,p_request_id,
    'ACCEPTED','CANDIDATE_STAGED',p_base_catalog_identity);
  RETURN QUERY SELECT p_candidate_id,'STAGED'::varchar(16);
END $$;

CREATE FUNCTION suite.request_suite_content_check(
  p_item_id char(32), p_request_id varchar(128), p_actor varchar(64))
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
DECLARE v_base char(64); v_status varchar(16);
BEGIN
  PERFORM pg_advisory_xact_lock(hashtextextended('suite:content-management',0));
  IF EXISTS(SELECT 1 FROM suite.suite_content_management_audit a
      WHERE a.event_type='CONTENT_CHECK_REQUESTED' AND a.request_id=p_request_id) THEN
    RETURN;
  END IF;
  SELECT cs.active_catalog_identity,i.status INTO v_base,v_status
    FROM suite.suite_content_catalog_state cs JOIN suite.suite_content_items i
      ON i.catalog_identity=cs.active_catalog_identity
    WHERE cs.product_id='TURBORAMA_SUITE' AND i.item_id=p_item_id;
  IF v_base IS NULL THEN RAISE EXCEPTION 'SUITE_CONTENT_ITEM_NOT_FOUND'; END IF;
  INSERT INTO suite.suite_content_item_health(
    product_id,item_id,observed_catalog_identity,observed_availability,next_check_at)
  VALUES('TURBORAMA_SUITE',p_item_id,v_base,v_status,clock_timestamp())
  ON CONFLICT(product_id,item_id) DO UPDATE SET next_check_at=clock_timestamp(),
    last_checked_at=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN NULL ELSE suite_content_item_health.last_checked_at END,
    last_success_at=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN NULL ELSE suite_content_item_health.last_success_at END,
    last_full_validation_at=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN NULL ELSE suite_content_item_health.last_full_validation_at END,
    last_terminal_failure_at=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN NULL ELSE suite_content_item_health.last_terminal_failure_at END,
    consecutive_terminal_failures=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN 0 ELSE suite_content_item_health.consecutive_terminal_failures END,
    consecutive_successes=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN 0 ELSE suite_content_item_health.consecutive_successes END,
    last_result_code=CASE WHEN
      suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity OR
      suite_content_item_health.observed_availability<>excluded.observed_availability
      THEN 'NOT_CHECKED' ELSE suite_content_item_health.last_result_code END,
    observed_catalog_identity=excluded.observed_catalog_identity,
    observed_availability=excluded.observed_availability,
    row_version=suite_content_item_health.row_version+1,updated_at=clock_timestamp();
  INSERT INTO suite.suite_content_management_audit(
    event_type,actor,item_id,job_id,correlation_id,request_id,outcome,detail_code,
    previous_catalog_identity)
  VALUES('CONTENT_CHECK_REQUESTED',p_actor,p_item_id,
    md5(p_request_id)||md5(reverse(p_request_id)),p_request_id,p_request_id,
    'ACCEPTED','CHECK_QUEUED',v_base);
END $$;

CREATE FUNCTION suite.audit_suite_content_management_denial(
  p_event_type varchar(64), p_actor varchar(64), p_item_id char(32),
  p_correlation_id varchar(128), p_detail_code varchar(64))
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,suite AS $$
BEGIN
  IF p_event_type NOT IN('CONTENT_AUTH_DENIED','CONTENT_CSRF_DENIED',
      'CONTENT_STEP_UP_DENIED','CONTENT_RATE_LIMITED') OR
     p_detail_code NOT IN('AUTH_REQUIRED','CLAIM_DENIED','CSRF_DENIED',
      'STEP_UP_REQUIRED','RATE_LIMITED') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_AUDIT_CODE_INVALID';
  END IF;
  INSERT INTO suite.suite_content_management_audit(
    event_type,actor,item_id,correlation_id,outcome,detail_code)
  VALUES(p_event_type,p_actor,p_item_id,p_correlation_id,'DENIED',p_detail_code);
END $$;

REVOKE ALL ON suite.suite_content_item_health,suite.suite_content_origin_candidates,
  suite.suite_content_management_audit,suite.suite_content_alert_outbox,
  suite.suite_content_monitor_state,suite.suite_content_management_items FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.get_suite_content_candidate_context(char(32),varchar(32)),
  suite.submit_suite_content_origin_candidate(char(64),char(32),char(64),varchar(128),bytea,bytea,bytea,integer,varchar(32),varchar(48),varchar(64)),
  suite.request_suite_content_check(char(32),varchar(128),varchar(64)),
  suite.audit_suite_content_management_denial(varchar(64),varchar(64),char(32),varchar(128),varchar(64))
  FROM PUBLIC;
REVOKE ALL ON FUNCTION suite.guard_suite_content_management_audit(),
  suite.guard_suite_content_candidate_mutation(),suite.guard_suite_content_health_mutation()
  FROM PUBLIC;

GRANT USAGE ON SCHEMA suite TO "turborama-suite-content-admin","turborama-suite-content-monitor";
GRANT SELECT ON suite.schema_migrations TO "turborama-suite-content-admin","turborama-suite-content-monitor";
GRANT SELECT ON suite.schema_migration_checksums TO "turborama-suite-content-admin","turborama-suite-content-monitor";
GRANT SELECT ON suite.suite_content_item_health,suite.suite_content_management_audit
  TO "turborama-suite-content-admin";
GRANT SELECT ON suite.suite_content_management_items TO "turborama-suite-content-admin";
GRANT SELECT(candidate_id,item_id,base_catalog_identity,request_id,state,change_intent,change_reason,
  submitted_by,submitted_at,updated_at,verified_at,published_at,attempt_count,
  internal_result_code,published_catalog_identity)
  ON suite.suite_content_origin_candidates TO "turborama-suite-content-admin";
GRANT EXECUTE ON FUNCTION suite.get_suite_content_candidate_context(char(32),varchar(32)),
  suite.submit_suite_content_origin_candidate(char(64),char(32),char(64),varchar(128),bytea,bytea,bytea,integer,varchar(32),varchar(48),varchar(64)),
  suite.request_suite_content_check(char(32),varchar(128),varchar(64)),
  suite.audit_suite_content_management_denial(varchar(64),varchar(64),char(32),varchar(128),varchar(64))
  TO "turborama-suite-content-admin";

GRANT SELECT,INSERT,UPDATE ON suite.suite_content_item_health,
  suite.suite_content_origin_candidates,suite.suite_content_alert_outbox,
  suite.suite_content_monitor_state TO "turborama-suite-content-monitor";
GRANT SELECT,INSERT ON suite.suite_content_management_audit TO "turborama-suite-content-monitor";
GRANT USAGE,SELECT ON SEQUENCE suite.suite_content_management_audit_audit_id_seq,
  suite.suite_content_alert_outbox_alert_id_seq TO "turborama-suite-content-monitor";
GRANT SELECT,INSERT ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_artifact_origins,suite.suite_content_publish_runs
  TO "turborama-suite-content-monitor";
GRANT SELECT ON suite.suite_content_catalog_state TO "turborama-suite-content-monitor";
GRANT EXECUTE ON FUNCTION suite.publish_suite_content_catalog(char(64))
  TO "turborama-suite-content-monitor";

REVOKE ALL ON suite.suite_content_origin_candidates,suite.suite_content_item_health,
  suite.suite_content_management_audit,suite.suite_content_alert_outbox,
  suite.suite_content_monitor_state,suite.suite_content_management_items
  FROM "turborama-suite-content-api","turborama-suite-gateway",
  "turborama-suite","turborama-suite-admin";

INSERT INTO suite.schema_migrations(version)
VALUES('013_suite_content_management')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('013_suite_content_management', :'migration_sha256');
COMMIT;
