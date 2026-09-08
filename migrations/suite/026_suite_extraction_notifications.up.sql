BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:026_extraction_notifications',0));

-- Additive only. No license/session/grant/connection-notice row is changed.
CREATE VIEW suite.suite_extraction_notice_context WITH (security_barrier=true) AS
SELECT DISTINCT l.license_id,d.device_id,s.session_id,d.public_key_spki,
  g.item_id,g.artifact_id,g.artifact_version,g.manifest_identity,g.sha256,
  i.display_name,e.source_purchase_id
FROM suite.suite_licenses l
JOIN suite.suite_license_enrollments b ON b.license_id=l.license_id
JOIN suite.suite_devices d ON d.license_id=b.license_id AND d.device_id=b.device_id
  AND d.public_key_spki=b.public_key_spki AND d.status='ACTIVE'
JOIN suite.suite_sessions s ON s.license_id=l.license_id AND s.device_id=d.device_id
  AND s.status='ACTIVE' AND s.revocation_generation=l.revocation_generation
  AND s.authorized_until>clock_timestamp()
JOIN suite.suite_content_entitlements e ON e.license_id=l.license_id
  AND e.scope='FULL_CATALOG' AND e.status='ACTIVE' AND e.source_system='TURBOBOX_V1'
JOIN suite.suite_license_deliveries p ON p.license_id=l.license_id
  AND p.source_system=e.source_system AND p.source_purchase_id=e.source_purchase_id
  AND p.source_item_key=e.source_item_key AND p.product_id=e.product_id
  AND p.provisioning_state='PROVISIONED' AND p.financial_state='PAID'
JOIN suite.suite_content_grants g ON g.license_id=l.license_id AND g.device_id=d.device_id
  AND g.state='COMPLETED' AND g.revocation_generation=l.revocation_generation
  AND g.created_at>clock_timestamp()-interval '7 days'
JOIN suite.suite_content_items i ON i.catalog_identity=g.catalog_identity AND i.item_id=g.item_id
WHERE l.product_id='TURBORAMA_SUITE' AND l.status='ACTIVE'
  AND l.activation_consumed AND l.enrollment_state='BOUND'
  AND l.license_term='LIFETIME' AND l.expires_at IS NULL AND l.maximum_active_devices=1;

CREATE TABLE suite.suite_extraction_notification_outbox(
  event_id varchar(64) PRIMARY KEY CHECK(event_id ~ '^[0-9a-f]{64}$'),
  license_id varchar(64) NOT NULL,
  device_id char(64) NOT NULL,
  item_id char(32) NOT NULL,
  source_purchase_id varchar(64) NOT NULL,
  content_name varchar(512) NOT NULL,
  category_id varchar(48) NOT NULL,
  completed_at timestamptz NOT NULL,
  created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  template_variant smallint NOT NULL CHECK(template_variant BETWEEN 0 AND 9),
  status varchar(24) NOT NULL DEFAULT 'PENDING'
    CHECK(status IN('PENDING','LEASED','DISPATCHING','QUEUED','SKIPPED','UNCERTAIN','DEAD')),
  attempts integer NOT NULL DEFAULT 0 CHECK(attempts BETWEEN 0 AND 8),
  lease_token uuid,
  lease_until timestamptz,
  next_attempt_at timestamptz NOT NULL DEFAULT clock_timestamp(),
  finished_at timestamptz,
  last_error_code varchar(64),
  FOREIGN KEY(license_id,device_id) REFERENCES suite.suite_devices(license_id,device_id)
);
CREATE INDEX ix_suite_extraction_notices_pending
  ON suite.suite_extraction_notification_outbox(next_attempt_at,created_at)
  WHERE status IN('PENDING','LEASED');
CREATE INDEX ix_suite_extraction_notices_license_time
  ON suite.suite_extraction_notification_outbox(license_id,created_at);
REVOKE ALL ON suite.suite_extraction_notice_context,suite.suite_extraction_notification_outbox FROM PUBLIC;
GRANT SELECT ON suite.suite_extraction_notice_context TO "turborama-suite";
GRANT SELECT,INSERT ON suite.suite_extraction_notification_outbox TO "turborama-suite";
GRANT SELECT,UPDATE ON suite.suite_extraction_notification_outbox TO "turborama-suite-admin";
INSERT INTO suite.schema_migrations(version) VALUES('026_suite_extraction_notifications');
COMMIT;
