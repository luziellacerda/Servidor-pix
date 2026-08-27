# Error catalog v1

Errors are strict UTF-8 JSON `{schemaVersion,code,message}` and never include identifiers or internal
diagnostics. Stable codes: `SUITE_DISABLED`, `BODY_INVALID`, `JSON_INVALID`, `CONTRACT_INVALID`,
`PRODUCT_DENIED`, `LICENSE_NOT_FOUND`, `LICENSE_DENIED`, `ACTIVATION_INVALID`, `ACTIVATION_REPLAY`,
`DEVICE_DENIED`, `ACTION_INVALID`, `CONTEXT_INVALID`, `CHALLENGE_INVALID`, `CHALLENGE_MISMATCH`,
`PROOF_INVALID`, `REPLAY_DENIED`, `SESSION_INVALID`, `INTERNAL_ERROR`.

The edge must preserve JSON, status, content type and body without redirect, HTML or compression.
Heartbeat-aware throttling must be designed before production because the fixed client treats 4xx as
authoritative denial.
