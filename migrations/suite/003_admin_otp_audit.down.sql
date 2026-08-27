-- Intentionally non-destructive. Migration 003 is expand-forward and its audit
-- columns must not be removed in production. Roll back application binaries and
-- leave this compatible schema in place.
SELECT '003_admin_otp_audit is expand-forward; no schema changes performed' AS notice;
