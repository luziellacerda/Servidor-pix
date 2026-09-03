BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:021_inventory_challenge_session_history', 0));

ALTER TABLE suite.suite_device_inventory_challenges
  DROP CONSTRAINT IF EXISTS suite_device_inventory_challenges_session_id_fkey;

INSERT INTO suite.schema_migrations(version)
VALUES ('021_suite_inventory_challenge_session_history')
ON CONFLICT (version) DO NOTHING;
COMMIT;
