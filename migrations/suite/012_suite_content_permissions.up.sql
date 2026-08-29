BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:012_content_permissions',0));

DO $$
BEGIN
  IF NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-gateway') OR
     NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-content-api') OR
     NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname='turborama-suite-publisher') THEN
    RAISE EXCEPTION 'SUITE_CONTENT_ROLES_MISSING';
  END IF;
END $$;

REVOKE ALL ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_artifact_origins,
  suite.suite_content_catalog_state,suite.suite_content_entitlements,
  suite.suite_content_grants,suite.suite_content_publish_runs FROM PUBLIC;

GRANT USAGE ON SCHEMA suite TO "turborama-suite-content-api","turborama-suite-gateway","turborama-suite-publisher";
GRANT SELECT ON suite.schema_migrations TO "turborama-suite-content-api","turborama-suite-gateway","turborama-suite-publisher";
GRANT SELECT ON suite.schema_migration_checksums TO "turborama-suite-content-api","turborama-suite-gateway","turborama-suite-publisher";

GRANT SELECT ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_catalog_state,suite.suite_content_entitlements,
  suite.suite_licenses,suite.suite_devices,suite.suite_sessions,
  suite.suite_license_deliveries TO "turborama-suite-content-api";
GRANT SELECT,INSERT ON suite.suite_challenges TO "turborama-suite-content-api";
GRANT UPDATE(consumed_at,invalidated_at,invalidation_reason)
  ON suite.suite_challenges TO "turborama-suite-content-api";
GRANT SELECT,INSERT ON suite.suite_content_grants TO "turborama-suite-content-api";

GRANT SELECT ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_artifact_origins,suite.suite_content_catalog_state,
  suite.suite_content_entitlements,suite.suite_licenses,suite.suite_devices,
  suite.suite_sessions,suite.suite_license_deliveries,
  suite.suite_content_grants TO "turborama-suite-gateway";
GRANT UPDATE(state,claimed_at,completed_at,failure_code)
  ON suite.suite_content_grants TO "turborama-suite-gateway";

GRANT SELECT,INSERT ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_artifact_origins,
  suite.suite_content_publish_runs TO "turborama-suite-publisher";
GRANT EXECUTE ON FUNCTION suite.publish_suite_content_catalog(char(64)),
  suite.reconcile_suite_content_entitlements() TO "turborama-suite-publisher";

GRANT SELECT,INSERT ON suite.suite_content_entitlements TO "turborama-suite-admin";
GRANT UPDATE(status,source_system,source_purchase_id,source_item_key,updated_at)
  ON suite.suite_content_entitlements TO "turborama-suite-admin";

REVOKE ALL ON suite.suite_content_snapshots,suite.suite_content_items,
  suite.suite_content_artifact_origins,
  suite.suite_content_catalog_state,suite.suite_content_entitlements,
  suite.suite_content_grants,suite.suite_content_publish_runs FROM "turborama-suite";

INSERT INTO suite.schema_migrations(version)
VALUES('012_suite_content_permissions')
ON CONFLICT(version) DO NOTHING;
INSERT INTO suite.schema_migration_checksums(version,script_sha256)
VALUES('012_suite_content_permissions', :'migration_sha256');
COMMIT;
