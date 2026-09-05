BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('suite:025_challenge_text_lookup',0));

-- Npgsql's existing string parameters are text; comparing them with char(64)
-- casts challenge_id to text. The raw bpchar primary key cannot serve that
-- expression. Sequential scans caused database-wide SSI predicate conflicts
-- between independent licenses during the 500-session load test.
-- Expression indexes preserve all existing protocol bytes and transaction rules.
CREATE INDEX ix_suite_challenges_text_lookup ON suite.suite_challenges ((challenge_id::text));
CREATE INDEX ix_suite_es_challenges_text_lookup ON suite.suite_es_challenges ((challenge_id::text));
CREATE INDEX ix_suite_network_challenges_text_lookup ON suite.suite_network_challenges ((challenge_id::text));
ANALYZE suite.suite_challenges;
ANALYZE suite.suite_es_challenges;
ANALYZE suite.suite_network_challenges;

INSERT INTO suite.schema_migrations(version) VALUES('025_suite_challenge_text_lookup');
COMMIT;
