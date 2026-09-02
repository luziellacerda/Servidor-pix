BEGIN;
DROP TABLE IF EXISTS suite.suite_connection_notification_outbox;
DROP TABLE IF EXISTS suite.suite_device_presence;
DROP TABLE IF EXISTS suite.suite_machine_change_reviews;
DROP TABLE IF EXISTS suite.suite_device_inventory_events;
DROP TABLE IF EXISTS suite.suite_device_inventory;
DROP TABLE IF EXISTS suite.suite_device_inventory_challenges;
DELETE FROM suite.schema_migrations WHERE version='020_suite_device_inventory_r25';
COMMIT;
