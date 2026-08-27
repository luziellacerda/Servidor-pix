BEGIN;
DELETE FROM suite.schema_migrations WHERE version='003_admin_otp_audit';
DROP INDEX IF EXISTS suite.ux_suite_audit_admin_request;
ALTER TABLE suite.suite_audit_events DROP COLUMN IF EXISTS otp_expires_at;
ALTER TABLE suite.suite_audit_events DROP COLUMN IF EXISTS request_id;
ALTER TABLE suite.suite_audit_events DROP COLUMN IF EXISTS admin_actor;
COMMIT;
