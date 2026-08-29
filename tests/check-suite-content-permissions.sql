\set ON_ERROR_STOP on
BEGIN;
DO $$
BEGIN
  IF current_database() !~ '(_ci|_test)$' THEN
    RAISE EXCEPTION 'CONTENT_PERMISSION_TEST_DATABASE_REQUIRED';
  END IF;
END $$;

CREATE ROLE "suite-content-membership-probe" NOLOGIN;
GRANT "turborama-suite-content-api" TO "suite-content-membership-probe";
DO $$
DECLARE role_names text[]:=ARRAY[
  'turborama-suite-content-api','turborama-suite-gateway','turborama-suite-publisher',
  'turborama-suite-content-admin','turborama-suite-content-monitor',
  'turborama-suite-content-maintenance'];
BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_auth_members membership
      JOIN pg_roles member_role ON member_role.oid=membership.member
      JOIN pg_roles granted_role ON granted_role.oid=membership.roleid
      WHERE member_role.rolname=ANY(role_names) OR
        granted_role.rolname=ANY(role_names)) THEN
    RAISE EXCEPTION 'CONTENT_GRANTED_ROLE_MEMBERSHIP_NOT_DETECTED';
  END IF;
END $$;
REVOKE "turborama-suite-content-api" FROM "suite-content-membership-probe";
GRANT "suite-content-membership-probe" TO "turborama-suite-content-api";
DO $$
DECLARE role_names text[]:=ARRAY[
  'turborama-suite-content-api','turborama-suite-gateway','turborama-suite-publisher',
  'turborama-suite-content-admin','turborama-suite-content-monitor',
  'turborama-suite-content-maintenance'];
BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_auth_members membership
      JOIN pg_roles member_role ON member_role.oid=membership.member
      JOIN pg_roles granted_role ON granted_role.oid=membership.roleid
      WHERE member_role.rolname=ANY(role_names) OR
        granted_role.rolname=ANY(role_names)) THEN
    RAISE EXCEPTION 'CONTENT_MEMBER_ROLE_MEMBERSHIP_NOT_DETECTED';
  END IF;
END $$;
REVOKE "suite-content-membership-probe" FROM "turborama-suite-content-api";
DROP ROLE "suite-content-membership-probe";

DO $$
BEGIN
  IF NOT EXISTS(
      SELECT 1 FROM pg_constraint constraint_row
      WHERE constraint_row.conrelid='suite.suite_content_grants'::regclass AND
        constraint_row.contype='f' AND
        constraint_row.confrelid='suite.suite_devices'::regclass) OR
     EXISTS(
      SELECT 1 FROM pg_constraint constraint_row
      WHERE constraint_row.conrelid='suite.suite_content_grants'::regclass AND
        constraint_row.contype='f' AND
        constraint_row.confrelid='suite.suite_sessions'::regclass) OR
     NOT EXISTS(SELECT 1 FROM pg_trigger trigger_row
       WHERE trigger_row.tgrelid='suite.suite_sessions'::regclass AND
         trigger_row.tgname='tr_suite_content_session_mutation_lock' AND
         NOT trigger_row.tgisinternal) OR
     NOT EXISTS(SELECT 1 FROM pg_trigger trigger_row
       WHERE trigger_row.tgrelid='suite.suite_content_grants'::regclass AND
         trigger_row.tgname='tr_suite_content_grant_session_guard' AND
         NOT trigger_row.tgisinternal) OR
     pg_get_functiondef('suite.lock_suite_content_session_mutation()'::regprocedure)
       NOT LIKE '%pg_advisory_xact_lock%' OR
     pg_get_functiondef('suite.lock_suite_content_session_mutation()'::regprocedure)
       NOT LIKE '%suite:content-session:%' OR
     pg_get_functiondef('suite.guard_suite_content_grant_session()'::regprocedure)
       NOT LIKE '%pg_advisory_xact_lock%' OR
     pg_get_functiondef('suite.guard_suite_content_grant_session()'::regprocedure)
       NOT LIKE '%suite:content-session:%' THEN
    RAISE EXCEPTION 'CONTENT_GRANT_STABLE_SESSION_BINDING_FAILED';
  END IF;

  IF NOT EXISTS(
    SELECT 1 FROM pg_constraint c
    WHERE c.conrelid='suite.suite_content_grants'::regclass
      AND c.conname='ck_suite_content_grants_maximum_ttl'
      AND pg_get_constraintdef(c.oid) LIKE '%00:01:00%') THEN
    RAISE EXCEPTION 'CONTENT_GRANT_TTL_CONSTRAINT_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-content-api','suite.suite_content_items','SELECT') OR
     NOT has_table_privilege('turborama-suite-content-api','suite.schema_migration_checksums','SELECT') OR
     has_table_privilege('turborama-suite-content-api','suite.suite_content_artifact_origins','SELECT') OR
     NOT has_table_privilege('turborama-suite-content-api','suite.suite_content_grants','INSERT') OR
     NOT has_column_privilege('turborama-suite-content-api','suite.suite_challenges','consumed_at','UPDATE') OR
     has_column_privilege('turborama-suite-content-api','suite.suite_content_grants','state','UPDATE') THEN
    RAISE EXCEPTION 'CONTENT_API_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-gateway','suite.suite_content_artifact_origins','SELECT') OR
     NOT has_table_privilege('turborama-suite-gateway','suite.schema_migration_checksums','SELECT') OR
     NOT has_column_privilege('turborama-suite-gateway','suite.suite_content_grants','state','UPDATE') OR
     has_column_privilege('turborama-suite-gateway','suite.suite_content_grants','token_digest','UPDATE') OR
     has_table_privilege('turborama-suite-gateway','suite.suite_challenges','INSERT') THEN
    RAISE EXCEPTION 'CONTENT_GATEWAY_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-publisher','suite.suite_content_artifact_origins','INSERT') OR
     NOT has_table_privilege('turborama-suite-publisher','suite.schema_migration_checksums','SELECT') OR
     NOT has_table_privilege('turborama-suite-publisher','suite.suite_content_artifact_origins','SELECT') OR
     has_table_privilege('turborama-suite-publisher','suite.suite_sessions','SELECT') OR
     NOT has_function_privilege('turborama-suite-publisher',
       'suite.publish_suite_content_catalog(character)'::regprocedure,'EXECUTE') THEN
    RAISE EXCEPTION 'CONTENT_PUBLISHER_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-admin','suite.suite_content_entitlements','INSERT') OR
     NOT has_column_privilege('turborama-suite-admin','suite.suite_content_entitlements','status','UPDATE') OR
     has_table_privilege('turborama-suite','suite.suite_content_artifact_origins','SELECT') THEN
    RAISE EXCEPTION 'CONTENT_ISOLATION_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-content-admin',
       'suite.schema_migration_checksums','SELECT') OR
     NOT has_table_privilege('turborama-suite-content-monitor',
       'suite.schema_migration_checksums','SELECT') OR
     NOT has_table_privilege('turborama-suite-content-maintenance',
       'suite.schema_migration_checksums','SELECT') OR
     has_table_privilege('turborama-suite','suite.schema_migration_checksums','SELECT') THEN
    RAISE EXCEPTION 'CONTENT_MIGRATION_LEDGER_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-content-admin',
       'suite.suite_content_management_items','SELECT') OR
     NOT has_table_privilege('turborama-suite-content-admin',
       'suite.suite_content_item_health','SELECT') OR
     NOT has_column_privilege('turborama-suite-content-admin',
       'suite.suite_content_origin_candidates','state','SELECT') OR
     has_table_privilege('turborama-suite-content-admin',
       'suite.suite_content_origin_candidates','SELECT') OR
     has_column_privilege('turborama-suite-content-admin',
       'suite.suite_content_origin_candidates','upstream_url_ciphertext','SELECT') OR
     NOT has_function_privilege('turborama-suite-content-admin',
       'suite.get_suite_content_candidate_context(character,character varying)'::regprocedure,
       'EXECUTE') OR
     NOT has_function_privilege('turborama-suite-content-admin',
       'suite.submit_suite_content_origin_candidate(character,character,character,character varying,bytea,bytea,bytea,integer,character varying,character varying,character varying)'::regprocedure,'EXECUTE') OR
     NOT has_function_privilege('turborama-suite-content-admin',
       'suite.request_suite_content_check(character,character varying,character varying)'::regprocedure,
       'EXECUTE') OR
     NOT has_function_privilege('turborama-suite-content-admin',
       'suite.audit_suite_content_management_denial(character varying,character varying,character,character varying,character varying)'::regprocedure,
       'EXECUTE') OR
     has_table_privilege('turborama-suite-content-admin',
       'suite.suite_content_artifact_origins','SELECT') THEN
    RAISE EXCEPTION 'CONTENT_MANAGEMENT_ADMIN_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF EXISTS(
      SELECT 1
      FROM unnest(ARRAY[
        'turborama-suite','turborama-suite-admin','turborama-suite-content-api',
        'turborama-suite-gateway','turborama-suite-publisher',
        'turborama-suite-content-monitor','turborama-suite-content-maintenance']) AS denied(role_name)
      CROSS JOIN unnest(ARRAY[
        'suite.get_suite_content_candidate_context(character,character varying)'::regprocedure,
        'suite.submit_suite_content_origin_candidate(character,character,character,character varying,bytea,bytea,bytea,integer,character varying,character varying,character varying)'::regprocedure,
        'suite.request_suite_content_check(character,character varying,character varying)'::regprocedure,
        'suite.audit_suite_content_management_denial(character varying,character varying,character,character varying,character varying)'::regprocedure]) AS protected(function_oid)
      WHERE has_function_privilege(denied.role_name,protected.function_oid,'EXECUTE')) THEN
    RAISE EXCEPTION 'CONTENT_MANAGEMENT_FUNCTION_ISOLATION_FAILED';
  END IF;

  IF NOT has_table_privilege('turborama-suite-content-monitor',
       'suite.suite_content_origin_candidates','SELECT') OR
     NOT has_column_privilege('turborama-suite-content-monitor',
       'suite.suite_content_origin_candidates','upstream_url_ciphertext','UPDATE') OR
     NOT has_table_privilege('turborama-suite-content-monitor',
       'suite.suite_content_artifact_origins','INSERT') OR
     NOT has_function_privilege('turborama-suite-content-monitor',
       'suite.publish_suite_content_catalog(character)'::regprocedure,'EXECUTE') OR
     has_table_privilege('turborama-suite-gateway',
       'suite.suite_content_origin_candidates','SELECT') OR
     has_table_privilege('turborama-suite-content-api',
       'suite.suite_content_item_health','SELECT') OR
     has_table_privilege('turborama-suite',
       'suite.suite_content_management_audit','SELECT') THEN
    RAISE EXCEPTION 'CONTENT_MANAGEMENT_MONITOR_PRIVILEGE_MATRIX_FAILED';
  END IF;

  IF NOT has_function_privilege('turborama-suite-content-maintenance',
       'suite.run_suite_content_retention(integer)'::regprocedure,'EXECUTE') OR
     has_table_privilege('turborama-suite-content-maintenance',
       'suite.suite_content_grants','SELECT') OR
     has_table_privilege('turborama-suite-content-maintenance',
       'suite.suite_content_grants','DELETE') OR
     has_table_privilege('turborama-suite-content-maintenance',
       'suite.suite_content_origin_candidates','UPDATE') THEN
    RAISE EXCEPTION 'CONTENT_RETENTION_PRIVILEGE_MATRIX_FAILED';
  END IF;
  IF NOT has_function_privilege('turborama-suite',
       'suite.enforce_suite_content_challenge_quota(character varying,character varying,character varying)'::regprocedure,
       'EXECUTE') OR
     NOT has_function_privilege('turborama-suite-content-api',
       'suite.enforce_suite_content_grant_quota(character varying,character,character)'::regprocedure,
       'EXECUTE') OR
     pg_get_functiondef(
       'suite.enforce_suite_content_challenge_quota(character varying,character varying,character varying)'::regprocedure)
       NOT LIKE '%pg_advisory_xact_lock%' OR
     pg_get_functiondef(
       'suite.enforce_suite_content_grant_quota(character varying,character,character)'::regprocedure)
       NOT LIKE '%pg_advisory_xact_lock%' OR
     pg_get_functiondef(
       'suite.enforce_suite_content_grant_quota(character varying,character,character)'::regprocedure)
       NOT LIKE '%device_id = p_device_id%' OR
     pg_get_functiondef(
       'suite.enforce_suite_content_grant_quota(character varying,character,character)'::regprocedure)
       LIKE '%session_id = p_session_id%' THEN
    RAISE EXCEPTION 'CONTENT_PERSISTENT_QUOTA_PRIVILEGE_OR_LOCK_FAILED';
  END IF;
END $$;

-- An audited administrative resume is deliberately eligible even while the
-- commerce delivery retains financial_state=SUSPENDED.  A version marker
-- without the full audit tuple must remain fail-closed.
INSERT INTO suite.suite_licenses(
  license_id,product_id,status,activation_consumed,license_term,expires_at,
  identity_policy,maximum_active_devices,provisioning_origin,enrollment_state,claim_mode)
VALUES
  ('TS-CONTENT-RESUME-POS','TURBORAMA_SUITE','ACTIVE',true,'LIFETIME',NULL,
   'SOFTWARE_ONLY',1,'COMMERCE','BOUND','PREBOUND'),
  ('TS-CONTENT-RESUME-NEG','TURBORAMA_SUITE','ACTIVE',true,'LIFETIME',NULL,
   'SOFTWARE_ONLY',1,'COMMERCE','BOUND','PREBOUND'),
  ('TS-CONTENT-PAID-POS','TURBORAMA_SUITE','ACTIVE',true,'LIFETIME',NULL,
   'SOFTWARE_ONLY',1,'COMMERCE','BOUND','PREBOUND');

INSERT INTO suite.suite_license_deliveries(
  source_system,source_purchase_id,source_item_key,source_product_sku,product_id,
  license_id,provisioning_state,financial_state,last_source_version,
  administrative_resume_source_version,administrative_resume_actor,
  administrative_resume_reason,administrative_resume_request_id)
VALUES
  ('TURBOBOX_V1','content-resume-positive','resume-positive',
   'SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE','TS-CONTENT-RESUME-POS',
   'PROVISIONED','SUSPENDED',7,7,'content-admin',
   'Customer entitlement restored after audited review.','resume-request-positive'),
  ('TURBOBOX_V1','content-resume-negative','resume-negative',
   'SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE','TS-CONTENT-RESUME-NEG',
   'PROVISIONED','SUSPENDED',7,7,NULL,NULL,NULL),
  ('TURBOBOX_V1','content-paid-positive','paid-positive',
   'SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE','TS-CONTENT-PAID-POS',
   'PROVISIONED','PAID',1,NULL,NULL,NULL,NULL);

SELECT suite.reconcile_suite_content_entitlements();

INSERT INTO suite.suite_devices(
  license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status)
VALUES('TS-CONTENT-RESUME-POS',repeat('d',64),'SOFTWARE_BOUND_ONLINE','AA==',
  repeat('e',64),'ACTIVE');
INSERT INTO suite.suite_sessions(
  license_id,device_id,session_id,status,authorized_until,last_server_time,
  revocation_generation)
VALUES('TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),'ACTIVE',
  clock_timestamp()+interval '2 days',extract(epoch from clock_timestamp())::bigint,0);

DO $$
DECLARE rejected boolean := false;
BEGIN
  IF NOT EXISTS(
    SELECT 1 FROM suite.suite_content_entitlements
    WHERE license_id='TS-CONTENT-RESUME-POS' AND scope='FULL_CATALOG'
      AND status='ACTIVE') THEN
    RAISE EXCEPTION 'CONTENT_ADMINISTRATIVE_RESUME_POSITIVE_FAILED';
  END IF;
  IF EXISTS(
    SELECT 1 FROM suite.suite_content_entitlements
    WHERE license_id='TS-CONTENT-RESUME-NEG' AND scope='FULL_CATALOG'
      AND status='ACTIVE') THEN
    RAISE EXCEPTION 'CONTENT_ADMINISTRATIVE_RESUME_NEGATIVE_FAILED';
  END IF;

  BEGIN
    INSERT INTO suite.suite_content_entitlements(
      license_id,scope,status,source_system,source_purchase_id,source_item_key,product_id)
    VALUES('TS-CONTENT-RESUME-NEG','FULL_CATALOG','ACTIVE','TURBOBOX_V1',
      'content-resume-negative','resume-negative','TURBORAMA_SUITE');
  EXCEPTION WHEN raise_exception THEN
    IF SQLERRM='SUITE_CONTENT_ENTITLEMENT_NOT_COMMERCE_ELIGIBLE' THEN
      rejected := true;
    ELSE
      RAISE;
    END IF;
  END;
  IF NOT rejected THEN
    RAISE EXCEPTION 'CONTENT_ADMINISTRATIVE_RESUME_INCOMPLETE_AUDIT_ACCEPTED';
  END IF;
END $$;

DO $$
DECLARE duplicate_rejected boolean:=false;
BEGIN
  BEGIN
    INSERT INTO suite.suite_content_entitlements(
      license_id,scope,status,source_system,source_purchase_id,source_item_key,product_id)
    VALUES('TS-CONTENT-RESUME-POS','FULL_CATALOG','ACTIVE','TURBOBOX_V1',
      'content-resume-positive','resume-positive','TURBORAMA_SUITE');
  EXCEPTION WHEN unique_violation THEN
    duplicate_rejected:=true;
  END;
  IF NOT duplicate_rejected THEN
    RAISE EXCEPTION 'CONTENT_ENTITLEMENT_DUPLICATE_ACCEPTED';
  END IF;
END $$;

DELETE FROM suite.suite_content_entitlements
WHERE license_id='TS-CONTENT-RESUME-POS' AND scope='FULL_CATALOG';
DO $$
DECLARE missing_count bigint;
BEGIN
  SELECT count(*) INTO missing_count
  FROM suite.suite_license_deliveries d
  JOIN suite.suite_licenses l ON l.license_id=d.license_id
  WHERE d.source_system='TURBOBOX_V1' AND
    d.source_product_sku='SUITE_LIFETIME_1_DEVICE' AND
    d.product_id='TURBORAMA_SUITE' AND d.provisioning_state='PROVISIONED' AND
    l.provisioning_origin='COMMERCE' AND l.product_id='TURBORAMA_SUITE' AND
    l.status='ACTIVE' AND d.financial_state='SUSPENDED' AND
    d.administrative_resume_source_version=d.last_source_version AND
    d.administrative_resume_actor IS NOT NULL AND
    d.administrative_resume_reason IS NOT NULL AND
    d.administrative_resume_request_id IS NOT NULL AND
    (SELECT count(*) FROM suite.suite_content_entitlements e
     WHERE e.license_id=d.license_id AND e.scope='FULL_CATALOG' AND
       e.status='ACTIVE' AND e.source_system=d.source_system AND
       e.source_purchase_id=d.source_purchase_id AND
       e.source_item_key=d.source_item_key AND e.product_id=d.product_id)<>1;
  IF missing_count<>1 THEN
    RAISE EXCEPTION 'CONTENT_ENTITLEMENT_MISSING_REVERSE_INVARIANT_FAILED';
  END IF;
END $$;
SELECT suite.reconcile_suite_content_entitlements();

DO $$
BEGIN
  IF (SELECT count(*) FROM suite.suite_content_entitlements e
      WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
        e.product_id='TURBORAMA_SUITE')<>2 OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_entitlements e
       WHERE e.license_id='TS-CONTENT-RESUME-POS' AND e.scope='FULL_CATALOG' AND
         e.status='ACTIVE' AND e.source_system='TURBOBOX_V1' AND
         e.source_purchase_id='content-resume-positive' AND
         e.source_item_key='resume-positive') OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_entitlements e
       WHERE e.license_id='TS-CONTENT-PAID-POS' AND e.scope='FULL_CATALOG' AND
         e.status='ACTIVE' AND e.source_system='TURBOBOX_V1' AND
         e.source_purchase_id='content-paid-positive' AND
         e.source_item_key='paid-positive') OR
     EXISTS(SELECT 1 FROM suite.suite_content_entitlements e
       WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
         e.license_id NOT IN('TS-CONTENT-RESUME-POS','TS-CONTENT-PAID-POS')) OR
     EXISTS(SELECT 1 FROM suite.suite_content_entitlements e
       JOIN suite.suite_licenses l ON l.license_id=e.license_id
       WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
         l.provisioning_origin='LEGACY_ADMIN') OR
     EXISTS(
       SELECT e.license_id,e.scope FROM suite.suite_content_entitlements e
       WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE'
       GROUP BY e.license_id,e.scope HAVING count(*)>1) THEN
    RAISE EXCEPTION 'CONTENT_ENTITLEMENT_POSITIVE_BIJECTION_FAILED';
  END IF;
END $$;

-- Continuous readiness is a positive bijection, not a hard-coded sale count.
-- A third legitimate commerce sale reconciles atomically to 3/3 and remains ready.
INSERT INTO suite.suite_licenses(
  license_id,product_id,status,activation_consumed,license_term,expires_at,
  identity_policy,maximum_active_devices,provisioning_origin,enrollment_state,claim_mode)
VALUES('TS-CONTENT-PAID-THIRD','TURBORAMA_SUITE','ACTIVE',true,'LIFETIME',NULL,
  'SOFTWARE_ONLY',1,'COMMERCE','BOUND','PREBOUND');
INSERT INTO suite.suite_license_deliveries(
  source_system,source_purchase_id,source_item_key,source_product_sku,product_id,
  license_id,provisioning_state,financial_state,last_source_version)
VALUES('TURBOBOX_V1','content-paid-third','paid-third',
  'SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE','TS-CONTENT-PAID-THIRD',
  'PROVISIONED','PAID',1);
SELECT suite.reconcile_suite_content_entitlements();
DO $$
DECLARE eligible_count bigint;
DECLARE entitlement_count bigint;
BEGIN
  SELECT count(*) INTO eligible_count
  FROM suite.suite_license_deliveries delivery
  JOIN suite.suite_licenses license ON license.license_id=delivery.license_id
  WHERE delivery.source_system='TURBOBOX_V1' AND
    delivery.source_product_sku='SUITE_LIFETIME_1_DEVICE' AND
    delivery.product_id='TURBORAMA_SUITE' AND
    delivery.provisioning_state='PROVISIONED' AND
    (delivery.financial_state='PAID' OR
     (delivery.financial_state='SUSPENDED' AND
      delivery.administrative_resume_source_version=delivery.last_source_version AND
      delivery.administrative_resume_actor IS NOT NULL AND
      delivery.administrative_resume_reason IS NOT NULL AND
      delivery.administrative_resume_request_id IS NOT NULL)) AND
    license.provisioning_origin='COMMERCE' AND license.status='ACTIVE';
  SELECT count(*) INTO entitlement_count
  FROM suite.suite_content_entitlements entitlement
  WHERE entitlement.scope='FULL_CATALOG' AND entitlement.status='ACTIVE' AND
    entitlement.product_id='TURBORAMA_SUITE';
  IF eligible_count<>3 OR entitlement_count<>3 OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_entitlements
       WHERE license_id='TS-CONTENT-PAID-THIRD' AND scope='FULL_CATALOG' AND
         status='ACTIVE') THEN
    RAISE EXCEPTION 'CONTENT_THIRD_COMMERCE_SALE_NOT_READY';
  END IF;
END $$;
DELETE FROM suite.suite_content_entitlements
WHERE license_id='TS-CONTENT-PAID-THIRD' AND scope='FULL_CATALOG';
DELETE FROM suite.suite_license_deliveries
WHERE license_id='TS-CONTENT-PAID-THIRD';
DELETE FROM suite.suite_licenses WHERE license_id='TS-CONTENT-PAID-THIRD';

INSERT INTO suite.suite_challenges(
  challenge_id,product_id,license_id,device_id,session_id,action,context_hash,
  nonce,expires_at,created_at)
SELECT md5('quota-challenge-'||position)||md5('quota-challenge-b-'||position),
  'TURBORAMA_SUITE','TS-CONTENT-RESUME-POS',repeat('d',64),
  md5('quota-session-'||position)||md5('quota-session-b-'||position),
  'catalog.read',repeat('1',64),'quota-'||position,
  clock_timestamp()+interval '1 minute',clock_timestamp()
FROM generate_series(1,120) AS generated(position);
SET ROLE "turborama-suite";
DO $$
DECLARE rejected boolean:=false;
BEGIN
  BEGIN
    PERFORM suite.enforce_suite_content_challenge_quota(
      'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('f',64));
  EXCEPTION WHEN raise_exception THEN
    rejected:=SQLERRM='SUITE_CONTENT_SESSION_INVALID';
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'CONTENT_CHALLENGE_SESSION_NOT_ENFORCED'; END IF;
  rejected:=false;
  BEGIN
    PERFORM suite.enforce_suite_content_challenge_quota(
      'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64));
  EXCEPTION WHEN raise_exception THEN
    rejected:=SQLERRM='SUITE_CONTENT_CHALLENGE_QUOTA_EXCEEDED';
  END;
  IF NOT rejected THEN RAISE EXCEPTION 'CONTENT_CHALLENGE_QUOTA_FAILED'; END IF;
END $$;
RESET ROLE;
DELETE FROM suite.suite_challenges
WHERE license_id='TS-CONTENT-RESUME-POS' AND nonce LIKE 'quota-%';

-- Exercise the production invariant with one unavailable object.  A published
-- catalog is complete at 850 IDs, but only READY rows have descriptors/origins.
INSERT INTO suite.suite_content_snapshots(
  catalog_identity,catalog_sequence,inventory_sha256,visual_catalog_sha256,
  item_count,ready_item_count,maintenance_item_count,status,
  origin_active_key_version,origin_key_set_fingerprint,origin_allowlist_fingerprint)
VALUES(repeat('c',64),1,repeat('d',64),repeat('e',64),850,849,1,'STAGING',
  1,repeat('1',64),repeat('2',64));

INSERT INTO suite.suite_content_items(
  catalog_identity,item_id,display_order,display_name,visual_extract_policy,artifact_id,artifact_version,content_length,sha256,
  safe_file_name,file_extension,extract_policy,manifest_identity,descriptor_hash,content_type,status)
SELECT repeat('c',64),md5(position::text),position,'Jogo '||position,'NONE',md5(position::text),1,position,
  repeat('a',64),md5(position::text)||'.bin','.bin','NONE',repeat('c',64),repeat('b',64),
  'application/octet-stream','READY'
FROM generate_series(1,849) AS generated(position);

INSERT INTO suite.suite_content_items(
  catalog_identity,item_id,display_order,display_name,visual_extract_policy,status,maintenance_reason)
VALUES(repeat('c',64),md5('850'),850,'Jogo 850','NONE','MAINTENANCE','CONTENT_TEMPORARILY_UNAVAILABLE');

DO $$
BEGIN
  BEGIN
    INSERT INTO suite.suite_content_items(
      catalog_identity,item_id,display_order,display_name,visual_extract_policy,artifact_id,
      artifact_version,content_length,sha256,safe_file_name,file_extension,
      extract_policy,manifest_identity,descriptor_hash,content_type,status)
    VALUES(repeat('c',64),md5('policy-mismatch'),999,'Teste de política','NONE',md5('policy-mismatch'),
      1,1,repeat('a',64),'policy.zip','.zip','EXTRACT_ARCHIVE',repeat('c',64),
      repeat('b',64),'application/octet-stream','READY');
    RAISE EXCEPTION 'CONTENT_EXTRACT_POLICY_DIVERGENCE_ACCEPTED';
  EXCEPTION WHEN check_violation THEN
    NULL;
  END;
END $$;

INSERT INTO suite.suite_content_artifact_origins(
  catalog_identity,item_id,upstream_url_ciphertext,upstream_url_nonce,upstream_url_tag,key_version)
SELECT catalog_identity,item_id,decode('01','hex'),decode(repeat('00',12),'hex'),
  decode(repeat('00',16),'hex'),1
FROM suite.suite_content_items
WHERE catalog_identity=repeat('c',64) AND status='READY';

SELECT suite.publish_suite_content_catalog(repeat('c',64));

-- A mirror after an operational rollback must bind to the active descriptor,
-- not to a newer historical READY snapshot. Different bytes/version remain a
-- NEW_ARTIFACT_VERSION operation with its separate authorization path.
INSERT INTO suite.suite_content_snapshots(
  catalog_identity,catalog_sequence,inventory_sha256,visual_catalog_sha256,
  item_count,ready_item_count,maintenance_item_count,status,published_at,
  origin_active_key_version,origin_key_set_fingerprint,origin_allowlist_fingerprint)
VALUES(repeat('f',64),2,repeat('d',64),repeat('e',64),850,850,0,'PUBLISHED',
  clock_timestamp(),1,repeat('1',64),repeat('2',64));
INSERT INTO suite.suite_content_items(
  catalog_identity,item_id,display_order,display_name,visual_extract_policy,
  artifact_id,artifact_version,content_length,sha256,safe_file_name,file_extension,
  extract_policy,manifest_identity,descriptor_hash,content_type,status)
VALUES(repeat('f',64),md5('1'),1,'Jogo 1 versão 2','NONE',md5('1'),2,999,
  repeat('9',64),md5('1')||'.bin','.bin','NONE',repeat('f',64),repeat('8',64),
  'application/octet-stream','READY');
UPDATE suite.suite_content_catalog_state SET active_catalog_identity=repeat('f',64),
  updated_at=clock_timestamp() WHERE product_id='TURBORAMA_SUITE';
UPDATE suite.suite_content_catalog_state SET active_catalog_identity=repeat('c',64),
  updated_at=clock_timestamp() WHERE product_id='TURBORAMA_SUITE';
DO $$
DECLARE context record;
BEGIN
  SELECT * INTO context FROM suite.get_suite_content_candidate_context(
    md5('1')::char(32),'MIRROR_REPLACEMENT');
  IF context.base_catalog_identity<>repeat('c',64) OR
     context.expected_artifact_version<>1 OR context.expected_content_length<>1 OR
     context.expected_sha256<>repeat('a',64) OR
     context.resolved_change_intent<>'MIRROR_REPLACEMENT' THEN
    RAISE EXCEPTION 'CONTENT_ROLLBACK_MIRROR_BOUND_TO_HISTORICAL_VERSION';
  END IF;
END $$;
DO $$
DECLARE expected_policy varchar(24);
BEGIN
  SELECT expected_extract_policy INTO expected_policy
  FROM suite.get_suite_content_candidate_context(md5('850')::char(32),'AUTO_REPLACEMENT');
  IF expected_policy<>'NONE' THEN
    RAISE EXCEPTION 'CONTENT_INITIAL_RECOVERY_VISUAL_POLICY_NOT_BOUND';
  END IF;
END $$;
DO $$
DECLARE total_count bigint;
DECLARE ready_count bigint;
DECLARE maintenance_count bigint;
DECLARE origin_count bigint;
BEGIN
  SELECT count(*),count(*) FILTER(WHERE i.status='READY'),
    count(*) FILTER(WHERE i.status='MAINTENANCE')
    INTO total_count,ready_count,maintenance_count
  FROM suite.suite_content_items i
  WHERE i.catalog_identity=repeat('c',64);
  SELECT count(*) INTO origin_count FROM suite.suite_content_artifact_origins o
    WHERE o.catalog_identity=repeat('c',64);
  IF total_count<>850 OR ready_count<>849 OR maintenance_count<>1 OR origin_count<>ready_count OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_catalog_state
       WHERE product_id='TURBORAMA_SUITE' AND active_catalog_identity=repeat('c',64)) THEN
    RAISE EXCEPTION 'CONTENT_MAINTENANCE_PUBLICATION_FAILED';
  END IF;
END $$;

-- Retention uses a heartbeat rather than the 60-second issuance TTL for
-- CLAIMED grants.  Active streams remain untouched, and only terminal rows
-- cross the formal 90-day deletion boundary.
INSERT INTO suite.suite_content_grants(
  grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,
  manifest_identity,descriptor_hash,range_start,content_length,sha256,state,
  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,
  completed_at,failure_code)
VALUES
  (repeat('1',64),repeat('2',64),'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
   clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
   repeat('c',64),repeat('b',64),0,1,repeat('a',64),'COMPLETED',
   'retention-old-terminal',clock_timestamp()-interval '100 days',
   clock_timestamp()-interval '100 days'+interval '30 seconds',
   clock_timestamp()-interval '99 days',clock_timestamp()-interval '99 days',
   clock_timestamp()-interval '91 days',NULL),
  (repeat('3',64),repeat('4',64),'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
   clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
   repeat('c',64),repeat('b',64),0,1,repeat('a',64),'ISSUED',
   'retention-expired-issued',clock_timestamp()-interval '2 days',
   clock_timestamp()-interval '2 days'+interval '30 seconds',NULL,NULL,NULL,NULL),
  (repeat('5',64),repeat('6',64),'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
   clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
   repeat('c',64),repeat('b',64),0,1,repeat('a',64),'CLAIMED',
   'retention-stale-claim',clock_timestamp()-interval '2 days',
   clock_timestamp()-interval '2 days'+interval '30 seconds',
   clock_timestamp()-interval '1 day',clock_timestamp()-interval '1 hour',NULL,NULL),
  (repeat('7',64),repeat('8',64),'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
   clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
   repeat('c',64),repeat('b',64),0,1,repeat('a',64),'CLAIMED',
   'retention-active-claim',clock_timestamp()-interval '1 hour',
   clock_timestamp()-interval '1 hour'+interval '30 seconds',
   clock_timestamp()-interval '59 minutes',clock_timestamp(),NULL,NULL),
  (repeat('9',64),repeat('0',64),'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
   clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
   repeat('c',64),repeat('b',64),0,1,repeat('a',64),'COMPLETED',
   'retention-recent-terminal',clock_timestamp()-interval '10 days',
   clock_timestamp()-interval '10 days'+interval '30 seconds',
   clock_timestamp()-interval '9 days',clock_timestamp()-interval '9 days',
   clock_timestamp()-interval '1 day',NULL);

-- The persistent grant quota is serialized in PostgreSQL, so it applies across
-- API replicas.  Fifteen active grants are allowed; the sixteenth exhausts the
-- per-license/device active budget until a stream reaches a terminal state.
INSERT INTO suite.suite_content_grants(
  grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,
  manifest_identity,descriptor_hash,range_start,content_length,sha256,state,
  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,
  completed_at,failure_code)
SELECT
  md5('quota-grant-'||position::text)||md5('quota-grant-b-'||position::text),
  md5('quota-token-'||position::text)||md5('quota-token-b-'||position::text),
  'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
  clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
  repeat('c',64),repeat('b',64),0,1,repeat('a',64),'CLAIMED',
  'quota-grant-'||position::text,clock_timestamp(),
  clock_timestamp()+interval '30 seconds',clock_timestamp(),clock_timestamp(),NULL,NULL
FROM generate_series(1,13) AS position;

SET ROLE "turborama-suite-content-api";
SELECT suite.enforce_suite_content_grant_quota(
  'TS-CONTENT-RESUME-POS',repeat('d',64)::char(64),repeat('e',64)::char(64));
RESET ROLE;

INSERT INTO suite.suite_content_grants(
  grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,
  manifest_identity,descriptor_hash,range_start,content_length,sha256,state,
  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,
  completed_at,failure_code)
VALUES(
  md5('quota-grant-14')||md5('quota-grant-b-14'),
  md5('quota-token-14')||md5('quota-token-b-14'),
  'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
  clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
  repeat('c',64),repeat('b',64),0,1,repeat('a',64),'CLAIMED',
  'quota-grant-14',clock_timestamp(),clock_timestamp()+interval '30 seconds',
  clock_timestamp(),clock_timestamp(),NULL,NULL);

SET ROLE "turborama-suite-content-api";
DO $$
BEGIN
  PERFORM suite.enforce_suite_content_grant_quota(
    'TS-CONTENT-RESUME-POS',repeat('d',64)::char(64),repeat('e',64)::char(64));
  RAISE EXCEPTION 'CONTENT_GRANT_QUOTA_NOT_ENFORCED';
EXCEPTION
  WHEN raise_exception THEN
    IF SQLERRM<>'SUITE_CONTENT_GRANT_QUOTA_EXCEEDED' THEN
      RAISE;
    END IF;
END $$;
RESET ROLE;

INSERT INTO suite.suite_challenges(
  challenge_id,product_id,license_id,device_id,session_id,action,context_hash,
  nonce,expires_at,consumed_at,created_at)
VALUES(repeat('f',64),'TURBORAMA_SUITE','TS-CONTENT-RESUME-POS',repeat('d',64),
  repeat('e',64),'catalog.read',repeat('1',64),repeat('n',32),
  clock_timestamp()-interval '40 days',clock_timestamp()-interval '40 days',
  clock_timestamp()-interval '41 days');

INSERT INTO suite.suite_content_origin_candidates(
  candidate_id,item_id,base_catalog_identity,request_id,state,
  upstream_url_ciphertext,upstream_url_nonce,upstream_url_tag,key_version,
  change_intent,expected_extract_policy,submitted_by,submitted_at,updated_at,
  internal_result_code)
VALUES
  (repeat('a',64),md5('850'),repeat('c',64),'retention-candidate-old-0001','REJECTED',
   decode('01','hex'),decode(repeat('00',12),'hex'),decode(repeat('00',16),'hex'),1,
   'INITIAL_RECOVERY','NONE','content-admin',clock_timestamp()-interval '40 days',
   clock_timestamp()-interval '31 days','TERMINAL_UNAVAILABLE'),
  (repeat('b',64),md5('850'),repeat('c',64),'retention-candidate-live-001','STAGED',
   decode('01','hex'),decode(repeat('00',12),'hex'),decode(repeat('00',16),'hex'),1,
   'INITIAL_RECOVERY','NONE','content-admin',clock_timestamp()-interval '40 days',
   clock_timestamp()-interval '31 days','STAGED');

SET ROLE "turborama-suite-content-maintenance";
SELECT * FROM suite.run_suite_content_retention(100);
RESET ROLE;

-- Retained grants keep their original session identity but no longer hold a
-- mutable FK to the current-session pointer. A new session for the same bound
-- device succeeds, old grants fail exact-session authorization, and the quota
-- still sees old-session live grants through its license/device key.
INSERT INTO suite.suite_content_grants(
  grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,
  manifest_identity,descriptor_hash,range_start,content_length,sha256,state,
  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,
  completed_at,failure_code)
VALUES(
  md5('rotation-old-grant')||md5('rotation-old-grant-b'),
  md5('rotation-old-token')||md5('rotation-old-token-b'),
  'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('e',64),0,
  clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
  repeat('c',64),repeat('b',64),0,1,repeat('a',64),'ISSUED',
  'rotation-old-grant',clock_timestamp(),clock_timestamp()+interval '30 seconds',
  NULL,NULL,NULL,NULL);

UPDATE suite.suite_sessions
SET session_id=repeat('f',64),status='ACTIVE',
    authorized_until=clock_timestamp()+interval '2 days',
    last_server_time=last_server_time+1,updated_at=clock_timestamp()
WHERE license_id='TS-CONTENT-RESUME-POS' AND device_id=repeat('d',64);

SET ROLE "turborama-suite-content-api";
DO $$
BEGIN
  PERFORM suite.enforce_suite_content_grant_quota(
    'TS-CONTENT-RESUME-POS',repeat('d',64)::char(64),repeat('f',64)::char(64));
  RAISE EXCEPTION 'CONTENT_GRANT_QUOTA_SESSION_ROTATION_BYPASS';
EXCEPTION WHEN raise_exception THEN
  IF SQLERRM<>'SUITE_CONTENT_GRANT_QUOTA_EXCEEDED' THEN RAISE; END IF;
END $$;
RESET ROLE;

DO $$
DECLARE claimed_count bigint;
BEGIN
  WITH claimed AS(
    UPDATE suite.suite_content_grants grant_row
    SET state='CLAIMED',claimed_at=clock_timestamp(),
        last_authorized_at=clock_timestamp()
    WHERE grant_row.grant_id=
        md5('rotation-old-grant')||md5('rotation-old-grant-b') AND
      grant_row.state='ISSUED' AND EXISTS(
        SELECT 1 FROM suite.suite_sessions session
        WHERE session.license_id=grant_row.license_id AND
          session.device_id=grant_row.device_id AND
          session.session_id=grant_row.session_id AND session.status='ACTIVE' AND
          session.authorized_until>clock_timestamp())
    RETURNING 1)
  SELECT count(*) INTO claimed_count FROM claimed;
  IF claimed_count<>0 THEN
    RAISE EXCEPTION 'CONTENT_OLD_SESSION_GRANT_CLAIMED';
  END IF;
END $$;

DELETE FROM suite.suite_content_grants WHERE correlation_id LIKE 'quota-grant-%';
DELETE FROM suite.suite_content_grants WHERE correlation_id='rotation-old-grant';

INSERT INTO suite.suite_content_grants(
  grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,
  manifest_identity,descriptor_hash,range_start,content_length,sha256,state,
  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,
  completed_at,failure_code)
VALUES(
  md5('rotation-new-grant')||md5('rotation-new-grant-b'),
  md5('rotation-new-token')||md5('rotation-new-token-b'),
  'TS-CONTENT-RESUME-POS',repeat('d',64),repeat('f',64),0,
  clock_timestamp()+interval '1 day',repeat('c',64),md5('1'),md5('1'),1,
  repeat('c',64),repeat('b',64),0,1,repeat('a',64),'ISSUED',
  'rotation-new-grant',clock_timestamp(),clock_timestamp()+interval '30 seconds',
  NULL,NULL,NULL,NULL);

DO $$
DECLARE claimed_count bigint;
BEGIN
  WITH claimed AS(
    UPDATE suite.suite_content_grants grant_row
    SET state='CLAIMED',claimed_at=clock_timestamp(),
        last_authorized_at=clock_timestamp()
    WHERE grant_row.grant_id=
        md5('rotation-new-grant')||md5('rotation-new-grant-b') AND
      grant_row.state='ISSUED' AND EXISTS(
        SELECT 1 FROM suite.suite_sessions session
        WHERE session.license_id=grant_row.license_id AND
          session.device_id=grant_row.device_id AND
          session.session_id=grant_row.session_id AND session.status='ACTIVE' AND
          session.authorized_until>clock_timestamp())
    RETURNING 1)
  SELECT count(*) INTO claimed_count FROM claimed;
  IF claimed_count<>1 THEN
    RAISE EXCEPTION 'CONTENT_CURRENT_SESSION_GRANT_NOT_CLAIMED';
  END IF;
END $$;
DELETE FROM suite.suite_content_grants WHERE correlation_id='rotation-new-grant';

DO $$
BEGIN
  IF EXISTS(SELECT 1 FROM suite.suite_content_grants WHERE grant_id=repeat('1',64)) OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_grants
       WHERE grant_id=repeat('3',64) AND state='EXPIRED' AND claimed_at IS NULL AND
         completed_at IS NOT NULL AND failure_code='EXPIRED') OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_grants
       WHERE grant_id=repeat('5',64) AND state='FAILED' AND failure_code='STALE_CLAIM') OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_grants
       WHERE grant_id=repeat('7',64) AND state='CLAIMED' AND completed_at IS NULL) OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_grants
       WHERE grant_id=repeat('9',64) AND state='COMPLETED') THEN
    RAISE EXCEPTION 'CONTENT_GRANT_RETENTION_POLICY_FAILED';
  END IF;
  IF EXISTS(SELECT 1 FROM suite.suite_challenges
      WHERE challenge_id=repeat('f',64)) THEN
    RAISE EXCEPTION 'CONTENT_CHALLENGE_RETENTION_POLICY_FAILED';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM suite.suite_content_origin_candidates
       WHERE candidate_id=repeat('a',64) AND state='REJECTED' AND
         secret_destroyed_at IS NOT NULL AND upstream_url_ciphertext IS NULL AND
         upstream_url_nonce IS NULL AND upstream_url_tag IS NULL AND key_version IS NULL) OR
     NOT EXISTS(SELECT 1 FROM suite.suite_content_origin_candidates
       WHERE candidate_id=repeat('b',64) AND state='STAGED' AND
         secret_destroyed_at IS NULL AND upstream_url_ciphertext IS NOT NULL) THEN
    RAISE EXCEPTION 'CONTENT_CANDIDATE_CRYPTO_SHRED_POLICY_FAILED';
  END IF;
END $$;
ROLLBACK;
SELECT 'SUITE CONTENT PERMISSION MATRIX: OK';
