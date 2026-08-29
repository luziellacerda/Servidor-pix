# Private content publisher runbook

> Administration and periodic origin recognition are deployed by migration 013,
> `TurboRamaSuiteContentMonitor`, and `docs/suite/CONTENT-ADMIN-BFF-CONTRACT.md`.
> This initial publisher remains responsible only for the private import and first snapshot.

The publisher imports the external `catalog.full.json` without copying any upstream URL into Git,
logs, service responses, grants or the client executable. It downloads each origin only to a hashing
stream, records resumable non-secret verification metadata, encrypts each URL with AES-256-GCM and
atomically publishes exactly 902 catalog IDs. Each ID is either READY, with verified descriptor and
private origin, or MAINTENANCE, without descriptor or origin. It never stores an artifact body.

## Fail-closed prerequisites

1. Stop if backup and restore evidence for the dedicated Suite database is absent.
2. Build candidate binaries from a pinned commit; run every self-test and vulnerability scan.
3. Create dedicated OS users `turborama-suite-gateway` and `turborama-suite-publisher` without login.
4. Create the matching PostgreSQL roles with `prepare-suite-content-roles.sql`, then apply migrations
   010 through 014 in order. The application roles must not own schema objects.
   The verified server return establishes exactly migrations 001–009 before this rollout. The installer
   rechecks that exact baseline and `suite-content-migrations.sha256`; any unexpected 010+ marker,
   unknown marker or checksum divergence blocks instead of guessing an upgrade. Each content migration
   receives its manifest hash as a psql variable and commits its marker plus immutable checksum ledger
   row in the same transaction. After this release, never edit 010–014; add a new forward migration.
5. Install the root-owned canonical credential sources outside Git/web roots, all mode `0400`, as
   `/etc/credstore/content-url-keyring`, `/etc/credstore/candidate-url-keyring`,
   `/etc/credstore/content-allowed-hosts` and `/etc/credstore/content-grant-token-pepper`.
   systemd pathless `LoadCredential`
   materializes private per-service copies from these canonical sources; never maintain independent
   key or pepper copies in environment files. Key versions are immutable: rotation adds a new version
   and changes `activeKeyVersion`, never replaces bytes under an existing version.
   Store the PostgreSQL connection string in a publisher-owned `0600` file as well; never pass it as a
   command argument or environment value where it can appear in a process listing.
6. Store the origin-host allowlist once at `/etc/credstore/content-allowed-hosts`, root-owned
   mode `0400`, as one canonical hostname per line. Gateway, publisher and monitor receive isolated
   snapshots of this same source through `LoadCredential`; never maintain service-owned copies.
   Never put complete URLs in an environment file or repository. A canonical URL is limited to 4096
   UTF-8 bytes before encryption; the database and every producer/consumer enforce the same limit.
7. Copy the URL-bearing input into `/var/lib/turborama-suite-content/incoming` mode `0600`. Do not
   place it under `/opt`, a web root, a repository, a build artifact or a backup shared with clients.
8. Copy the exact URL-free visual catalog into the publisher release as `catalog.visual.json` and
   pin its SHA-256 on the command line during the real import.

## Controlled sequence

Run `check-suite-content-readiness.sh --self-test`, then run it with `DATABASE prepublish 2` before
publishing. The explicit final argument is the reviewed cutover count recorded with deployment evidence;
for this first rollout, prepublish requires the two existing eligible COMMERCE
deliveries, zero content entitlements, and exactly two reported missing bindings; zero deliveries/zero
entitlements is not an acceptable empty success. Reconciliation after publication must change that
state to an exact delivery/entitlement bijection. Run `DATABASE production 2` once as the initial
cutover proof, then use `DATABASE production` for continuous checks.

Run `validate` first. It must report 902 selected items and exactly two rejected source-only extras.
It performs no origin request. Run `probe` next; it checks every origin with Range `0-0`/HEAD and
reports only item IDs, result codes and sizes. The real `publish` repeats the complete probe before
hashing. Terminal per-item failures (invalid extension, non-retryable unavailable response or zero-byte
object) become MAINTENANCE and are excluded from hashing. Network, public-DNS resolution, timeout,
408/425/429/5xx, idle read, length drift and stream mismatch become MAINTENANCE only after their bounded
retries are exhausted. TLS/certificate failure, redirect, content encoding, disallowed host/scheme/port,
private/NAT64 DNS result or any other security-policy failure aborts the run and preserves the prior
active snapshot. A hard mass-failure guard aborts instead of converting more than 25 items, preventing
a provider outage from publishing dozens or all items as unavailable. Then start the publisher oneshot
manually. Hashing is intentionally slow:
the READY subset currently represents terabytes, concurrency defaults to two and is capped at four.
The publisher and monitor oneshots intentionally use `TimeoutStartSec=infinity`; their network, idle,
retry and validation operations retain explicit bounded timeouts inside the applications. Do not replace
that service-level value with systemd's short default, which would kill a healthy multi-terabyte hash run.
The journal contains hashes and sizes but no URLs, and permits restart after interruption. A checkpoint
is reused only when the same pinned inventory is being processed and the origin returns the identical
   strong ETag and length. That ETag is checkpoint-eligible only when the probe and the full hashing GET
   both return the identical strong value; a missing/different full-response validator is never borrowed
   from the probe. Weak/missing ETag, including Last-Modified-only identity, always triggers a full
   stream/hash again. A failure in
the middle of a file restarts only that file; successfully completed files are not downloaded again while
   their identical strong ETag and length remain unchanged. Last-Modified alone is never a checkpoint
   identity and always causes a complete re-hash.

The full hashing pass also validates the object type without a second download. It rejects HTML, JSON
and incompatible XML media types before reading the body, hashes the single response stream, and retains
at most the first 64 KiB only in a zeroed memory buffer for signature checks. RAR, 7z, PBP, ZIP, NSP/PFS0,
CHD, CSO, RVZ, WUX and WBFS use fixed header signatures; 3DS/NCSD and XCI use their magic at offset
`0x100`; ISO/UDF descriptors are checked in sectors 16–31. The sole `.xml` catalog object is parsed to
EOF with DTD/external resolution disabled and must be a well-formed, namespace-free `gameList`, never
HTML. Generic `.bin` has no dependable universal magic, so it is accepted only at 4 KiB or larger after
text/HTML/JSON/XML signatures, Unicode text BOMs and incompatible content types are denied. These checks
are type-sanity gates, while the streamed SHA-256 remains the immutable content identity.
Journal schema version 2 records only objects that passed these gates. A version 1 journal is rejected
and must never be converted or reused, because its hashes predate signature/XML validation.

Never delete an item or invent a checksum. An origin that is broken, zero-byte, unverifiable or missing
safe metadata is published as MAINTENANCE with no descriptor, no descriptor hash and no origin row.
An HTTP 200 carrying the wrong media type, malformed XML or a file whose signature does not match its
declared extension is `ORIGIN_CONTENT_INVALID` and follows the same per-item MAINTENANCE path.
Only a fully verified object can be READY. The public assertion maps every internal maintenance reason
to the single generic code `CONTENT_TEMPORARILY_UNAVAILABLE`; it never exposes an origin status or URL.
The database transaction changes the active pointer only after the union READY + MAINTENANCE contains
exactly 902 unique IDs and every READY row has exactly one encrypted origin. Before that, both planes
remain fail-closed.

The 2026-08-29 preflight found five selected origins returning 404 and one returning zero-byte/416.
Those six IDs remain visible as MAINTENANCE. They do not receive grants and the gateway cannot read an
origin for them. Correcting them requires a new immutable snapshot and a newly pinned inventory digest.

After a successful publish, run the entitlement reconciler once. It only adds ACTIVE full-catalog
entitlements for `TURBOBOX_V1` Suite deliveries that are COMMERCE, PROVISIONED and backed by an ACTIVE
core license. The normal financial state is PAID. The existing administrative-resume path is also
eligible while financial state remains SUSPENDED only when its source version exactly matches the
delivery version and actor, reason and request ID are all recorded. An incomplete/stale resume remains
fail-closed. It does not revive a core license, device or session: those must already be valid, and every
catalog/grant/stream authorization revalidates them. New purchases are maintained in the original
commerce transaction; no PIX or sales semantics are changed by content reconciliation.

Run `check-suite-content-readiness.sh DATABASE production`. It must prove total items = 902, every item
is READY or MAINTENANCE, and origin count = READY count with no origin attached to MAINTENANCE. It
also proves the entitlement relation in both directions: every ACTIVE full-catalog entitlement is
backed by exactly its eligible COMMERCE delivery and every eligible PROVISIONED delivery (PAID or fully
audited administrative resume) has exactly one matching ACTIVE entitlement. The initial reviewed
cutover baseline is exactly two eligible COMMERCE deliveries and two matching active `FULL_CATALOG`
entitlements. Continuous readiness requires a positive eligible-delivery count, exact equality with
active entitlements and zero missing, duplicate, extra or `LEGACY_ADMIN` entitlements; zero deliveries
plus zero entitlements therefore fails, while a third atomically reconciled sale remains ready.
LEGACY_ADMIN licenses remain deliberately outside this commerce set.
Only then deploy/restart the gateway and control plane. Content configuration failure is isolated: activation,
session and heartbeat v1 remain available, while `/ready/content` and all content actions return 503
until the dedicated connection, assertion key, grant pepper and gateway proof validate. Verify that READY catalog entries contain descriptors, MAINTENANCE
entries contain only the generic reason, no response has a URL-like property, downloads never redirect
and no `Location` header is emitted. Keep the previous binaries available for rollback; the schema is
expand-forward and is not removed during application rollback.

An existing installation that already ran 010–013 needs an explicit content-only cutover for 014. First
close all three public `/v1/suite-content/` ingress routes while leaving activation/session/heartbeat v1
online. Verify the migration ledger adoption gate against the exact legacy schema, take the approved
backup, and apply 014. Legacy active snapshots intentionally have no deployment provenance, so both
content readiness endpoints must return 503 at this point. Run the publisher to create a new immutable
902-ID snapshot; its identity binds active origin key version, complete key-set fingerprint and allowlist
fingerprint, so it cannot collide with the legacy snapshot. Restart gateway and API, require both
`127.0.0.1:5191/ready` and `127.0.0.1:5190/ready/content` to return 200, run the authorized canary, and
only then reopen the content ingress. Never backfill a published legacy snapshot or bypass this 503 gate.

Finally, remove the plaintext input from the incoming directory using the server's approved secure
retention procedure. Preserve only the protected journal, the encrypted database and audit evidence.
The upstream files delivered to an authorized customer can still be copied by that customer; this
design protects the origin addresses and server-side entitlement decision, not impossible DRM.

## Administrative origin maintenance boundary

Origin repair belongs to the existing Cloudflare-protected Suite admin plane, never to the public
control API or byte gateway. The admin view may show item ID, ONLINE/EM MANUTENCAO, generic check
result and last-check timestamp. It must not render, return, log or embed the decrypted origin URL in
HTML/JSON after submission. Existing PIX, sales, checkout, webhook, delivery and license behavior must
remain unchanged.

A replacement is a state-changing admin operation and therefore requires the existing authenticated
administrator session, the existing origin/host allowlist, same-origin CSRF validation, bounded URL
input and an append-only audit record containing actor, item ID, timestamp, correlation ID and outcome
but no URL or bearer. Test the candidate through the same SSRF-safe resolver/TLS/redirect policy used
by the publisher. Encrypt the accepted URL before persistence. Write only a staging snapshot; promote
the complete 902-ID snapshot atomically after its invariants pass. A failed test leaves the active
snapshot untouched and the item MAINTENANCE. Runtime roles remain unable to decrypt or select origins:
only the publisher/admin repair role writes staging origins and only the gateway role reads an encrypted
origin while atomically claiming a READY grant.

Do not bolt this operation onto the commerce/PIX routes. The production implementation contract is in
`CONTENT-ADMIN-AUTOMATION-HANDOFF.md`; until its dedicated migration, worker and admin endpoints are
deployed together, origin replacement remains an offline publisher operation. A UI that merely changes
the active row or saves plaintext is not an acceptable interim implementation.

Availability detection runs on the server through the restricted content reconciler. A single timeout
or transient HTTP error must not mutate customer-visible state. The worker records bounded, URL-free
check evidence and changes an item to MAINTENANCE only after the configured confirmation policy is met.
It never updates an active immutable snapshot in place: it clones all 902 IDs into staging, removes the
descriptor and origin for the affected item, validates the union and atomically promotes the new
snapshot. Returning MAINTENANCE to READY is stricter: the replacement origin must pass SSRF-safe TLS
resolution, extension and filename rules, exact content length and a complete SHA-256 verification
before its encrypted origin and descriptor enter another atomic snapshot. The admin page reads the
last-check time/state from the audit/check store; the catalog assertion continues exposing only ONLINE
as READY or EM MANUTENCAO with the generic public reason.

## Content authority release artifact

Production also requires an offline-generated content-authority envelope. This is separate from and
must not modify the existing authority/machine-proof v1. Use an offline issuer key that is distinct from
the online session key and content assertion key. The signed payload is canonical JSON in this exact
order: `schemaVersion`, `kind`, `productId`, `baseUrl`, `contentAssertionAlgorithm`,
`contentAssertionKeyId`, `contentAssertionPublicKeySpki`, `tlsServerSpkiSha256Current`,
`tlsServerSpkiSha256Next`, `issuedAtUnixSeconds`, `expiresAtUnixSeconds`. Its kind is
`TURBORAMA_SUITE_CONTENT_AUTHORITY`, algorithm is `rsa-pss-sha256`, and the signing domain is
`TurboRamaSuiteContentAuthorityConfiguration/v1\0`.

The release inputs are the exact UTF-8 JSON envelope, its 64-hex SHA-256, the canonical DER RSA issuer
SubjectPublicKeyInfo and that DER file's independent 64-hex SHA-256. The client embeds canonical Base64
of the JSON bytes as `SuiteContentAuthorityConfigurationBase64`, lowercase hash as
`SuiteContentAuthorityConfigurationSha256`, and canonical Base64 DER as
`SuiteContentAuthorityIssuerSpkiBase64`. Their final assembly metadata names are respectively
`TurboRama.Suite.ContentAuthorityConfigurationBase64`,
`TurboRama.Suite.ContentAuthorityConfigurationSha256` and
`TurboRama.Suite.ContentAuthorityIssuerSpkiBase64`.

Do not generate this envelope inside the online API and do not place the offline private key on the
server or client. Use the reviewed generator and independent verification procedure in
`CONTENT-AUTHORITY-OFFLINE-RUNBOOK.md`. Release remains blocked until the resulting public files and
their independently approved exact hashes pass the signed Windows build gate and a canary client
validates both the authority envelope and a content assertion under its embedded content key.

## Production gateway and ingress

The existing `turborama-suite-api.service` remains the control plane on `127.0.0.1:5190` and serves
`POST /v1/suite/challenges`, `POST /v1/suite-content/catalog/current` and
`POST /v1/suite-content/downloads/authorize`. The new
`turborama-suite-content-gateway.service` is the data plane on `127.0.0.1:5191` and serves only
`GET /v1/suite-content/artifacts/{grantId}` plus local health checks. Do not create a second public
content API and do not expose either loopback listener directly through Cloudflare.

Before installation, confirm port 5191 is unused and create the no-login user
`turborama-suite-gateway`. Publish the gateway into
`/opt/turborama-suite-content-gateway`, install the supplied unit in `/etc/systemd/system`, and copy
the environment example to `/etc/turborama-suite-content/gateway.env`. The unit hard-codes the
loopback URL, so the environment file must not define `ASPNETCORE_URLS`.

Create the gateway-owned PostgreSQL connection file outside Git and web roots, as an absolute path and
mode `0600` (mode `0400` is also accepted):

- `gateway.connection`: PostgreSQL connection for the restricted gateway role, with error-detail and
  connection logging disabled.

The allowlist is not gateway-owned. It is the canonical root-owned `content-allowed-hosts` source described
above, projected into each service through `LoadCredential`.

The 32-byte Base64 grant pepper exists once as the root-owned credential
`/etc/credstore/content-grant-token-pepper`; never copy or retype it into service-owned files.
Install `turborama-suite-api-content-credentials.conf.example` as the existing control-plane unit's
drop-in. Both that drop-in and the gateway unit use pathless
`LoadCredential=content-grant-token-pepper`, so systemd resolves the same credential-store value and
materializes isolated per-service copies. Pathless loading is deliberate: a missing credential is not a
systemd startup failure. The shared API still starts licensing v1, while its content subsystem returns
503. The dedicated gateway fails its own startup validation. The control plane sends a fresh domain-separated HMAC proof
to the loopback-only `/ready/grant-pepper/prove` endpoint. The gateway compares it in fixed time without
returning/logging the nonce, MAC, pepper or its hash. The same loopback verification also requires the
gateway deployment `/ready` check (active-snapshot keyring/allowlist provenance) to pass. `/ready/content`,
catalog reads and grant issuance fail closed when either check is unavailable or mismatched. The artifact
endpoint independently executes the same deployment gate before atomically claiming a one-use grant, so
a stale gateway cannot consume a grant and fail only afterward.
Rotate the canonical pepper atomically during a maintenance window, restart the gateway first and the
control plane immediately afterward, then require `/ready/content` to recover before reopening ingress.
Any grant issued under the previous pepper expires within 60 seconds and must not be migrated.

The origin AES-256-GCM keyring is injected into gateway, publisher and monitor by pathless `LoadCredential` from
the same canonical root-owned source. The candidate keyring is likewise injected into admin and monitor
from its one canonical source. The isolated files materialized below each service's credentials directory
are ephemeral projections, not independently provisioned copies. Every loader executes an in-memory
encrypt/decrypt canary without logging key material or hashes. Before the long hash run and
again immediately before publication, the publisher sends a fresh 256-bit nonce plus a domain-separated
HMAC proof to the loopback-only `/ready/keyring/prove` endpoint. The proof binds the active version,
the complete ordered set of key versions/material and the normalized, sorted allowlist snapshot. The
gateway compares all three in fixed time without returning or logging secret material. Each immutable
snapshot persists only their deployment fingerprints and active version; gateway readiness compares its
loaded credential snapshots to those values and verifies that every active origin uses a loaded key
version. Thus a stale gateway restart remains 503 rather than appearing ready. A mismatch or failed
canary preserves the active snapshot. For rotation, update the matching `/etc/credstore` source
atomically, restart every consumer, verify `/ready/keyring` and one successful monitor
cycle, and only then start the publisher. Never copy/retype either keyring into a service-owned persistent
file or environment variable.

The control plane likewise reads its new PostgreSQL connection from
`ConnectionStrings__SuiteContentApiStoreFile`, its independent content assertion RSA key from
`Suite__ContentAssertionPrivateKeyPemFile`, and resolves the systemd credential named
`content-grant-token-pepper`. Keep the pre-existing machine-proof/online assertion key and protocol
unchanged. The content assertion key must be a private RSA key from 2048 through 4096 bits,
distinct from the online assertion key. Production also requires its lowercase SHA-256 SPKI pin in
`Suite__ContentAssertionExpectedKeyId`; startup compares it in fixed time and `/ready/content` returns
only the validated public key ID, never PEM material.

Run `systemd-analyze verify /etc/systemd/system/turborama-suite-content-gateway.service`, then
`systemd-analyze verify /etc/systemd/system/turborama-suite-api.service`, then
`systemctl daemon-reload` and restart the gateway and control plane without enabling the public route.
Verify locally:

```text
ss -lntp | grep -E '127\.0\.0\.1:(5190|5191)'
curl --fail --silent http://127.0.0.1:5190/ready/content
curl --fail --silent http://127.0.0.1:5191/ready/keyring
curl --fail --silent http://127.0.0.1:5191/ready
```

Both listeners must show only `127.0.0.1`. Restart the gateway and control plane before the release gate,
then require both `/ready/content` and gateway `/ready` to return 200. A 503 is expected until a compliant
active snapshot and the exact credential snapshots are present; never bypass readiness.

Include `nginx-turborama-suite-content.locations.conf.example` inside the existing Suite TLS server
block. The Cloudflare tunnel/edge continues to target Nginx, not ports 5190/5191. The supplied snippet
trusts `CF-Connecting-IP` only when the immediate peer is `127.0.0.1` or `::1`; direct callers cannot
supply a trusted real-IP header. Nginx overwrites X-Forwarded-For with the resulting `$remote_addr`;
never change this to `$proxy_add_x_forwarded_for` or trust arbitrary ingress ranges. The locations preserve
Authorization/Range/If-Range for artifact bytes, disable proxy buffering/cache/temp files and suppress
artifact access logs so neither bearer nor grant ID is recorded. Do not add request/response body,
Authorization, Cookie, query-string or debug logging at Nginx, Cloudflare, APM or application layers.
Application content error logs contain only a fixed event and correlation ID; never enable exception
object/message logging on gateway, monitor, publisher or content-control failure paths because DNS,
HTTP and database exceptions can embed a private hostname, URL, credential path or connection detail.
The gateway applies independent fixed-size rate-limit buckets to the trusted client address and a
SHA-256 digest of the grant ID, so a shared proxy address is not the only abuse boundary and raw grant
IDs are never retained as limiter keys.
Declare the three supplied `limit_req_zone`/`limit_conn_zone` definitions once in Nginx `http {}` and
keep the per-location request/connection limits enabled. Enforce equivalent Cloudflare IP+route limits at
the edge. The in-process bounded dictionaries evict the least-recent entry at their fixed cap rather than
turning a distributed identity/grant flood into a process-wide denial. The gateway unit also enforces
`MemoryHigh`, `MemoryMax`, `CPUQuota`, `TasksMax` and `LimitNOFILE`; changes require a measured load test.
While bytes are flowing, the gateway revalidates license, device, session, entitlement and revocation
generation at the first of five elapsed seconds or eight transferred MiB. A denial, database error or
five-second validation timeout cancels/aborts the active response; it never continues on stale authority.

The control plane uses the same loopback-only forwarded-header trust boundary. Core activation/session
limits remain 30 requests per identity and minute; content challenge/catalog/grant limits are 120.
Limiter keys are fixed-size SHA-256 digests, and each trusted client address can create at most 256 new
identity windows per core/content bucket and minute. This prevents one address from exhausting global
cardinality while preserving the 36-page catalog flow. At the fixed global cap the oldest bucket entry is
evicted, so distributed syntactically valid IDs do not poison every later customer. Nginx must overwrite both `X-Forwarded-For` and
`X-Forwarded-Proto`; never expose port 5190 or accept caller-supplied forwarding headers directly.

Migration 014 adds the authoritative cross-process budgets in PostgreSQL. Content challenge insertion
is serialized by license and bound active device (never by caller-selectable session ID), permits at most
120 new challenges per minute and 128 simultaneously unconsumed challenges, and first verifies that a
content action names the exact current active session. Download-grant insertion is serialized and counted
by license and bound device (never by a rotatable session ID), permits at most 30 new grants per minute
and 16 live ISSUED/CLAIMED grants; the session remains an exact authorization/grant binding, while expired
unclaimed grants do not hold the live budget. These `SECURITY DEFINER` gates have fixed search paths,
PUBLIC execution revoked and narrow role grants. Keep the in-process and edge limits as independent
abuse layers; they do not replace the transactional database quotas used by multiple API replicas.

The grant schema deliberately references the stable `(license_id, device_id)` device identity, not the
mutable current-session row. A grant-insert trigger and every session-row mutation acquire the same
transaction-scoped advisory lock. The insert then revalidates the exact active session, authorization
deadline and revocation generation; all grant binding fields remain immutable. Consequently a retained
terminal or old-session grant never blocks `session.open`, but it immediately fails redemption after the
current session rotates. Do not replace this with `ON UPDATE CASCADE`, which would rewrite the audited
session identity inside an already issued grant.

Validate the merged configuration with `nginx -t` before reload. Make one authorized canary download
through the public hostname and confirm 200 for a full transfer and exact 206 for a supported resume.
An upstream 200 response to an offset greater than zero is a protocol failure: the gateway must abort,
mark that one-use grant failed and never forward the full body as a resumed transfer. The client obtains
a new grant and starts from offset zero only as a separate request. Confirm no `Location`, upstream URL
or bearer value appears in headers, logs or traces. The 120-second Nginx read/send timeouts are inactivity
timeouts and therefore permit multi-hour transfers while data continues flowing.

Rollback public routing first, then stop the gateway and restore the previous binary. Do not roll back
the active snapshot pointer unless the prior snapshot independently satisfies the 902-ID invariant.

## Retention janitor

Migration 014 installs only the narrow `suite.run_suite_content_retention(integer)` SECURITY DEFINER
function for the no-login application owner and grants execution to the dedicated
`turborama-suite-content-maintenance` role; PUBLIC has no access and the role has no direct table write.
Publish `TurboRamaSuiteContentJanitor`, create its owner-only `janitor.connection`, install the supplied
service/timer and enable `turborama-suite-content-janitor.timer`. Use a local PostgreSQL socket or permit
only the required local address family in the unit. The job has a five-minute internal database timeout
and drains up to 100 bounded batches per invocation before reporting a backlog. The timer runs every ten
minutes, so cleanup capacity exceeds the admitted per-session issuance rate. It emits aggregate
counts/backlogs only—never grant IDs, bearer values, URLs or ciphertext.

Each active stream updates `last_authorized_at` after its normal authorization revalidation (at most five
seconds or 8 MiB). The janitor changes an unclaimed expired grant to EXPIRED; it never expires a CLAIMED
stream by the original 60-second issuance TTL. A CLAIMED row is failed only after 15 minutes without a
successful authorization heartbeat. Terminal grants are deleted after 90 days in bounded `SKIP LOCKED`
batches. Candidate URL ciphertext is crypto-shredded after 30 days only in REJECTED, SUPERSEDED or
PUBLISHED state; VALIDATING, VERIFIED and STAGED candidates are never shredded. Metadata/audit rows stay
available. Challenges without a retained activation-completion audit row are deleted after their expiry
plus a 30-day operational audit window, also in bounded `SKIP LOCKED` batches. Activation completion
challenges remain through their foreign key. `suite_outbox` currently has no producer; it must remain
empty until a reviewed dispatcher/retention contract is deployed. Alert on a nonzero retained backlog,
but do not make serving readiness depend on janitor
backlog. After installation run the janitor self-test, start the service once, inspect the aggregate
result, then enable and verify the ten-minute timer with `systemctl list-timers`.
