BEGIN;
REVOKE SELECT ON suite.suite_content_grants,suite.suite_content_items FROM "turborama-suite-admin";
REVOKE DELETE ON suite.suite_content_grants FROM "turborama-suite-admin";
DELETE FROM suite.schema_migrations WHERE version='019_suite_admin_customer_activity';
COMMIT;
