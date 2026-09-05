-- Disposable integration databases only. Migrations 001-003 predate explicit
-- role grants and production used the runtime owner for these foundation tables.
-- Reproduce that baseline without granting access to any new telemetry column.
DO $$ BEGIN
  IF current_database() NOT LIKE '%integration%' AND current_database() NOT LIKE '%\_ci' THEN
    RAISE EXCEPTION 'A disposable integration/CI database is required';
  END IF;
END $$;
GRANT USAGE ON SCHEMA suite TO "turborama-suite";
GRANT SELECT,INSERT,UPDATE,DELETE ON suite.suite_licenses,suite.suite_devices,
  suite.suite_challenges,suite.suite_activation_completions,suite.suite_sessions,
  suite.suite_license_enrollments,suite.suite_audit_events,suite.suite_outbox TO "turborama-suite";
GRANT SELECT ON suite.suite_licenses,suite.suite_devices,suite.suite_challenges,
  suite.suite_sessions,suite.suite_license_enrollments,suite.suite_audit_events TO "turborama-suite-admin";
GRANT INSERT ON suite.suite_audit_events TO "turborama-suite-admin";
GRANT USAGE,SELECT ON SEQUENCE suite.suite_audit_events_event_id_seq TO "turborama-suite","turborama-suite-admin";
GRANT USAGE,SELECT ON SEQUENCE suite.suite_outbox_outbox_id_seq TO "turborama-suite";
