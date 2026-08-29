#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

production_entitlements_ready() {
  [[ "$1" == 0 && "$2" == 0 && "$3" =~ ^[1-9][0-9]*$ && "$4" == "$3" &&
     "$5" == 0 && "$6" == 0 ]]
}

if [[ $# -eq 1 && "$1" == --self-test ]]; then
  if production_entitlements_ready 0 0 0 0 0 0; then
    echo 'SELF-TEST FAILED: zero/zero entitlement state was accepted.' >&2
    exit 1
  fi
  production_entitlements_ready 0 0 2 2 0 0 || {
    echo 'SELF-TEST FAILED: exact commerce entitlement bijection was rejected.' >&2
    exit 1
  }
  production_entitlements_ready 0 0 3 3 0 0 || {
    echo 'SELF-TEST FAILED: third legitimate commerce sale was rejected.' >&2
    exit 1
  }
  if production_entitlements_ready 0 0 3 2 0 0 ||
     production_entitlements_ready 0 0 2 3 0 0; then
    echo 'SELF-TEST FAILED: non-bijective entitlement state was accepted.' >&2
    exit 1
  fi
  echo 'SUITE CONTENT READINESS SELF-TEST: OK'
  exit 0
fi

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo 'usage: check-suite-content-readiness.sh DATABASE prepublish EXPECTED_COUNT | DATABASE production [EXPECTED_COUNT]' >&2
  exit 2
fi
database=$1
mode=$2
expected_count=${3:-}
case "$database" in (*[!A-Za-z0-9_-]*|'') echo 'BLOCKED: invalid database name.' >&2; exit 2;; esac
[[ "$mode" == prepublish || "$mode" == production ]] || exit 2
if [[ -n "$expected_count" && ! "$expected_count" =~ ^[1-9][0-9]*$ ]]; then
  echo 'BLOCKED: invalid expected commerce count.' >&2
  exit 2
fi
if [[ "$mode" == prepublish && -z "$expected_count" ]]; then
  echo 'BLOCKED: prepublish requires the reviewed expected commerce count.' >&2
  exit 2
fi

read -r -d '' query <<'SQL' || :
SELECT concat_ws('|',
  (SELECT count(*) FROM suite.schema_migrations WHERE version IN
    ('010_suite_content_catalog','011_suite_content_publish_integrity','012_suite_content_permissions','013_suite_content_management','014_suite_content_retention','015_suite_content_direct_metadata','016_suite_content_catalog_902')),
  (SELECT count(*) FROM suite.schema_migration_checksums WHERE version IN
    ('010_suite_content_catalog','011_suite_content_publish_integrity','012_suite_content_permissions','013_suite_content_management','014_suite_content_retention','015_suite_content_direct_metadata','016_suite_content_catalog_902')),
  (SELECT count(*) FROM suite.suite_content_snapshots WHERE status='PUBLISHED'),
  (SELECT count(*) FROM suite.suite_content_catalog_state cs
    JOIN suite.suite_content_snapshots s ON s.catalog_identity=cs.active_catalog_identity
    WHERE cs.product_id='TURBORAMA_SUITE' AND s.origin_active_key_version IS NOT NULL AND
      s.origin_key_set_fingerprint ~ '^[0-9a-f]{64}$' AND
      s.origin_allowlist_fingerprint ~ '^[0-9a-f]{64}$'),
  (SELECT count(*) FROM suite.suite_content_items i JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=i.catalog_identity WHERE s.product_id='TURBORAMA_SUITE'),
  (SELECT count(*) FROM suite.suite_content_items i JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=i.catalog_identity WHERE s.product_id='TURBORAMA_SUITE' AND i.status='READY'),
  (SELECT count(*) FROM suite.suite_content_items i JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=i.catalog_identity WHERE s.product_id='TURBORAMA_SUITE' AND i.status='MAINTENANCE'),
  (SELECT count(*) FROM suite.suite_content_artifact_origins o JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=o.catalog_identity WHERE s.product_id='TURBORAMA_SUITE'),
  (SELECT count(*) FROM suite.suite_content_artifact_origins o JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=o.catalog_identity WHERE s.product_id='TURBORAMA_SUITE' AND
      (octet_length(o.upstream_url_ciphertext) NOT BETWEEN 1 AND 4096 OR
       octet_length(o.upstream_url_nonce)<>12 OR octet_length(o.upstream_url_tag)<>16)),
  (SELECT count(*) FROM suite.suite_content_items i JOIN suite.suite_content_catalog_state s
    ON s.active_catalog_identity=i.catalog_identity WHERE s.product_id='TURBORAMA_SUITE' AND ((
      (i.status='READY' AND i.content_length IS NULL AND i.sha256 IS NULL AND
       i.descriptor_hash ~ '^[0-9a-f]{64}$' AND i.maintenance_reason IS NULL)
      OR (i.status='MAINTENANCE' AND i.content_length IS NULL AND i.sha256 IS NULL AND
          i.descriptor_hash IS NULL AND i.maintenance_reason='CONTENT_TEMPORARILY_UNAVAILABLE')) IS NOT TRUE)),
  (SELECT count(*) FROM suite.suite_content_entitlements e
    LEFT JOIN suite.suite_license_deliveries d
      ON d.license_id=e.license_id AND d.source_system=e.source_system
     AND d.source_purchase_id=e.source_purchase_id
     AND d.source_item_key=e.source_item_key AND d.product_id=e.product_id
    LEFT JOIN suite.suite_licenses l ON l.license_id=e.license_id
    WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
      (d.source_system='TURBOBOX_V1' AND
       d.source_product_sku='SUITE_LIFETIME_1_DEVICE' AND
       d.product_id='TURBORAMA_SUITE' AND d.provisioning_state='PROVISIONED' AND
        (d.financial_state='PAID' OR
          (d.financial_state='SUSPENDED' AND
           d.administrative_resume_source_version IS NOT NULL AND
           d.administrative_resume_source_version=d.last_source_version AND
           d.administrative_resume_actor IS NOT NULL AND
           d.administrative_resume_reason IS NOT NULL AND
           d.administrative_resume_request_id IS NOT NULL)) AND
       l.provisioning_origin='COMMERCE' AND l.product_id='TURBORAMA_SUITE' AND
       l.status='ACTIVE') IS NOT TRUE),
  (SELECT count(*) FROM suite.suite_license_deliveries d
    JOIN suite.suite_licenses l ON l.license_id=d.license_id
    WHERE d.source_system='TURBOBOX_V1' AND
      d.source_product_sku='SUITE_LIFETIME_1_DEVICE' AND
      d.product_id='TURBORAMA_SUITE' AND d.provisioning_state='PROVISIONED' AND
      l.provisioning_origin='COMMERCE' AND l.product_id='TURBORAMA_SUITE' AND
      l.status='ACTIVE' AND
      (d.financial_state='PAID' OR
       (d.financial_state='SUSPENDED' AND
        d.administrative_resume_source_version IS NOT NULL AND
        d.administrative_resume_source_version=d.last_source_version AND
        d.administrative_resume_actor IS NOT NULL AND
        d.administrative_resume_reason IS NOT NULL AND
        d.administrative_resume_request_id IS NOT NULL)) AND
      (SELECT count(*) FROM suite.suite_content_entitlements e
       WHERE e.license_id=d.license_id AND e.scope='FULL_CATALOG' AND
         e.status='ACTIVE' AND e.source_system=d.source_system AND
         e.source_purchase_id=d.source_purchase_id AND
         e.source_item_key=d.source_item_key AND e.product_id=d.product_id)<>1),
  (SELECT count(*) FROM suite.suite_license_deliveries d
    JOIN suite.suite_licenses l ON l.license_id=d.license_id
    WHERE d.source_system='TURBOBOX_V1' AND
      d.source_product_sku='SUITE_LIFETIME_1_DEVICE' AND
      d.product_id='TURBORAMA_SUITE' AND d.provisioning_state='PROVISIONED' AND
      l.provisioning_origin='COMMERCE' AND l.product_id='TURBORAMA_SUITE' AND
      l.status='ACTIVE' AND
      (d.financial_state='PAID' OR
       (d.financial_state='SUSPENDED' AND
        d.administrative_resume_source_version IS NOT NULL AND
        d.administrative_resume_source_version=d.last_source_version AND
        d.administrative_resume_actor IS NOT NULL AND
        d.administrative_resume_reason IS NOT NULL AND
        d.administrative_resume_request_id IS NOT NULL))),
  (SELECT count(*) FROM suite.suite_content_entitlements e
    JOIN suite.suite_licenses l ON l.license_id=e.license_id
    WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
      e.product_id='TURBORAMA_SUITE' AND l.product_id='TURBORAMA_SUITE'),
  (SELECT count(*) FROM suite.suite_content_entitlements e
    JOIN suite.suite_licenses l ON l.license_id=e.license_id
    WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
      e.product_id='TURBORAMA_SUITE' AND l.provisioning_origin='LEGACY_ADMIN'),
  (SELECT count(*) FROM(
    SELECT e.license_id,e.scope FROM suite.suite_content_entitlements e
    WHERE e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND
      e.product_id='TURBORAMA_SUITE'
    GROUP BY e.license_id,e.scope HAVING count(*)>1) duplicates),
  (SELECT count(*) FROM suite.suite_content_origin_candidates
    WHERE state IN('VALIDATING','VERIFIED') AND lease_expires_at<clock_timestamp()),
  (SELECT count(*) FROM suite.suite_content_monitor_state
    WHERE product_id='TURBORAMA_SUITE' AND last_outcome='SUCCESS' AND
      last_completed_at>=clock_timestamp()-interval '45 minutes')
);
SQL
state=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --tuples-only --no-align \
  --dbname="$database" --command="$query")

IFS='|' read -r migrations checksums snapshots active total ready maintenance origins invalid_origins invalid invalid_entitlements missing_or_duplicate_entitlements eligible_deliveries active_entitlements legacy_entitlements duplicate_entitlements expired_leases worker_fresh <<<"$state"

if [[ "$mode" == prepublish ]]; then
  [[ "$migrations" == 7 && "$checksums" == 7 && "$snapshots" == 0 &&
     "$active" == 0 && "$total" == 0 && "$ready" == 0 && "$maintenance" == 0 &&
     "$origins" == 0 && "$invalid_origins" == 0 && "$invalid" == 0 &&
     "$invalid_entitlements" == 0 &&
     "$missing_or_duplicate_entitlements" == "$eligible_deliveries" &&
     "$eligible_deliveries" == "$expected_count" && "$active_entitlements" == 0 &&
     "$legacy_entitlements" == 0 && "$duplicate_entitlements" == 0 &&
     "$expired_leases" == 0 && "$worker_fresh" == 0 ]] || {
    echo "BLOCKED: unexpected prepublish state $state" >&2
    exit 1
  }
  echo 'OK: schema ready and fail-closed; no catalog exposed.'
else
  [[ "$migrations" == 7 && "$checksums" == 7 && "$snapshots" -ge 1 && "$active" == 1 && "$total" == 902 &&
     $((ready + maintenance)) == 902 && "$origins" == "$ready" && "$invalid_origins" == 0 &&
     "$invalid" == 0 ]] &&
    production_entitlements_ready "$invalid_entitlements" \
      "$missing_or_duplicate_entitlements" "$eligible_deliveries" \
      "$active_entitlements" "$legacy_entitlements" "$duplicate_entitlements" &&
    [[ "$expired_leases" == 0 && "$worker_fresh" == 1 &&
       ( -z "$expected_count" || "$eligible_deliveries" == "$expected_count" ) ]] || {
    echo "BLOCKED: production content state $state" >&2
    exit 1
  }
  echo "OK: active immutable catalog has 902 items ($ready READY, $maintenance MAINTENANCE), one private origin per READY item, a complete commerce-entitlement bijection ($active_entitlements active), and a current monitor cycle."
fi
