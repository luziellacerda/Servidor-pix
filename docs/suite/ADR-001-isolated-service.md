# ADR-001: isolated Suite authority

Status: accepted for candidate implementation; production deployment is not authorized.

Decision: use a separate process, PostgreSQL database/schema/role and key hierarchy. Product is
selected by `/v1/suite/*` and persisted as immutable `TURBORAMA_SUITE`. The legacy process, routes,
state, keys, sessions and activation codes remain inaccessible. Machine proof bytes retain the frozen
`TurboRamaOnlineMachineProof/v1\0` domain; product binding is inside the recomputed context hash.

Consequences: independent deploy/rollback and cross-product denial; additional operational cost.
PostgreSQL is authoritative. Redis, if later introduced, is never authoritative. Admin and download
gateway remain separate future services.
