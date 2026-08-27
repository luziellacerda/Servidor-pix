# Suite migrations

Only expand-forward migrations are permitted. There is deliberately no destructive down migration.
Rollback means running the previous compatible service version; schema removal requires a separately
reviewed retention/export procedure after all readers have moved forward. Apply only to a dedicated,
empty Suite database/role after backup and restore gates are approved.
