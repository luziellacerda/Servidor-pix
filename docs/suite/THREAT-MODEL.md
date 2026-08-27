# Suite threat model

| Threat | Control in candidate | Remaining gate |
|---|---|---|
| Cross-product replay | route-selected product, immutable DB check, product in context hash | E2E with legacy client |
| Challenge replay | atomic conditional consume; request/context binding | PostgreSQL two-node/restore tests |
| Lost activation response | completion digest and stored signed result | fault injection |
| Key confusion | assertion domain, exact kind, SPKI-derived keyId | KMS/HSM and rotation ceremony |
| JSON ambiguity | duplicate/unknown/case/trailing/comment rejection | HTTP fuzzing |
| Device cloning | SPKI-derived deviceId, fingerprint and PSS proof | hardware profile review |
| Session theft | proof per open/heartbeat and current-session compare | revocation generation implementation |
| Secret disclosure | templates only; sanitized errors/logging | approved secret manager and log review |
| Host/resource exhaustion | 64 KiB hard body limit, kill switch | rate limiter and load/soak |
| Restore resurrection | schema carries revocation generation | restore epoch procedure/test |

No claim of production readiness is made while any remaining gate is open.
