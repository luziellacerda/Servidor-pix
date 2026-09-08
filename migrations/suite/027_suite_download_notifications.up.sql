BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:027_download_notifications',0));

-- Keep the existing outbox and worker. Legacy notices retain their identities,
-- states and extraction text; new clients identify a distinct download operation.
ALTER TABLE suite.suite_extraction_notification_outbox
  ADD COLUMN download_id char(32),
  ADD COLUMN completion_kind varchar(16) NOT NULL DEFAULT 'EXTRACTED',
  ADD CONSTRAINT ck_suite_notice_download_id
    CHECK(download_id IS NULL OR download_id ~ '^[0-9a-f]{32}$'),
  ADD CONSTRAINT ck_suite_notice_completion_kind
    CHECK(completion_kind IN('FILE_READY','EXTRACTED')),
  ADD CONSTRAINT ck_suite_notice_legacy_kind
    CHECK(download_id IS NOT NULL OR completion_kind='EXTRACTED');

CREATE UNIQUE INDEX ux_suite_notice_download_operation
  ON suite.suite_extraction_notification_outbox(license_id,device_id,download_id)
  WHERE download_id IS NOT NULL;

INSERT INTO suite.schema_migrations(version) VALUES('027_suite_download_notifications');
COMMIT;
