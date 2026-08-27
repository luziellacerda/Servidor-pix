# Staging, backup, restore and rollback

1. Use an isolated host/account, loopback port, empty dedicated PostgreSQL database/role and test-only
   secret-manager keys. Confirm the service identity cannot read legacy paths or schemas.
2. Apply `001_suite_foundation.up.sql`; record migration, image and source hashes. Seed only synthetic
   licenses through a future audited admin tool.
3. Run golden, negative, concurrency, restart, two-instance, load and client E2E tests. Validate JSON
   through the real staging edge without redirects/compression.
4. Back up the Suite database and secret metadata separately; restore into another isolated database.
   Increment restore epoch before accepting traffic and prove old challenges/sessions fail closed.
5. Rollback application by disabling the Suite route/kill switch and running the previous schema-
   compatible image. Do not run destructive down migrations. Roll forward schema corrections.
6. Never stop, restart, migrate or route the legacy service as part of Suite rollback.

Production remains a no-go until edge, TLS pin rotation, KMS/HSM, RPO/RTO, observability, security
review and owner approval are complete.
