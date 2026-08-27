BEGIN;
ALTER TABLE suite.suite_audit_events
  ADD COLUMN admin_actor varchar(128) NULL,
  ADD COLUMN request_id varchar(128) NULL,
  ADD COLUMN otp_expires_at timestamptz NULL;
CREATE UNIQUE INDEX ux_suite_audit_admin_request ON suite.suite_audit_events(request_id) WHERE request_id IS NOT NULL;
INSERT INTO suite.schema_migrations(version) VALUES('003_admin_otp_audit') ON CONFLICT(version) DO NOTHING;
COMMIT;
