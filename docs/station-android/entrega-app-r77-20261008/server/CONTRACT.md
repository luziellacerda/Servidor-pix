# Candidate Station multiplayer v3 — 8 October 2026

This directory is a **local, opt-in server candidate**, based on executable commit
`ab192bf1585e30f303d041f13b36a1f9c96d2caa`. It does not describe an active service.
The later diagnostic candidate `6f27c6ca` was not used as a source base. No Linux
deployment, service restart, live profile registration or database migration was
performed. `CONTRACT.json` defines the versioned API fields.

## Authentication and identity

The client first enters the existing social API, then uses
`POST /v1/station/online/multiplayer/command`. The HTTP bearer, existing request
proof format, device/license authorizer and RSA-PSS signed `online/v1` envelope
remain in use. Unknown/duplicate JSON properties, queries and bodies above8192
bytes are rejected. The proof target is the exact new path, not the v2 path.

The server supplies the same public peer ID and nickname as the social authority.
The client cannot choose a slot, peer identity, room generation or link owner.
Rooms/slots become immutable on start. Before start, membership changes compact
slots1..N, increment generation and clear everyone's ready status. A start freezes
the current2..4 participants, not every unoccupied capacity slot.

WebSocket route: `GET /v1/station/online/multiplayer/relay`, no query or body,
`Authorization: StationRelay <ticket>`, subprotocol `station-stream.v3`.
The proof canonical target is exactly this new route. Tickets last60seconds,
are single-use, and bind device/license, room, generation, link, role and slot.
The host receives one distinct ticket for each guest link; a guest can receive
only its own. Ticket/resume requires all exact profile fields and protected
RSA-PSS/P-256 proof mode. Tickets and room passwords are never in public room lists.

## Catalog authority and controller profile

The explicit registry starts as `[]`. **That means no game is enabled.** No
classification in this candidate was invented from a title, folder or descriptive
`players` field. A profile binds itemId, exact content hash, normalized platform,
engine ID, core/runtime hashes, profile ID/hash, controller profile, game mode,
maximumPlayers and sorted `allowedPlayerCounts`. The latter represents verified
simultaneous counts for that game mode. A single-player classification has
maximumPlayers1 and allowedPlayerCounts[]. Unknown/unapproved/missing items and
unsupported counts fail closed, including requests for only two people.

Creation checks the requested capacity. Joining checks the selected capacity and
available seat, permitting transient3 members in a capacity4 room whose verified
mode supports2or4. Start additionally checks the actual frozen count. Every create,
join, ready, start and ticket path rechecks the relevant authoritative profile.
There is no hot reload: changed registry bytes require an operator-controlled
new service instance. Public capabilities returns only the requested item's
profiles (or the own-room item), at most32, to avoid copying the whole catalog.

The registry is an operator-reviewed authorization artifact. `profileSha256`
identifies the canonical controller/core-options document used by the client;
the registry must be generated from the same reviewed canonical bytes. The server
does not inspect ROM contents or emulate the game to infer compatibility.
Profile/engine registration and real3/4-player validation remain prerequisites.

## Relay, pause and recovery

For N players the host opens N−1 independent native TCP connections and WS links.
Each link owns two262144-byte FIFO replay windows, reusing `StationStreamWindow`.
The native host must bind each accepted native connection to the authenticated
guest slot; arrival order is not an identity. The server cannot inspect or repair
an incorrect native controller mapping inside the opaque byte stream.

TSR3 uses the24-byte TSR2 header shape, magic`TSR3`, the same13 control types and
maximum16384-byte DATA. Offsets and credits are per link/direction. Epoch and pause
barrier are global for the frozen room. Any detached endpoint pauses all links;
only when all required endpoints acknowledge pause/current epoch, report ready
and all replay windows drain may the server publish Playing. Delayed ACK/READY
from an older epoch never releases the current barrier. Repeated host Suspend on
several links is coalesced by owner. Native pause/resume must also be global.
New DATA invalidates readiness only for the two endpoints of its link when the
accepted offset advances. Untouched links retain readiness for their unchanged
watermarks; duplicate replay does not revoke it. Resume still requires every
endpoint ready in the shared epoch and every replay window drained.

Logical membership and windows survive transport loss. Watchers stop a stale
transport on absent authenticated heartbeat, idle connection or failed validation;
that does not delete the room. Explicit leave ends the frozen room; license
revocation is still enforced. Native `failed` marks unrecoverable until leave.
Process death is not recoverable from these in-memory windows.

The optional shared budget is33554432bytes across retained v2 and v3 rings. A room
with4 participants reserves1572864bytes before stream allocation. Reservations
remain during loss and unrecoverable state, and are released only after owner
retirement and final writer detach. Admission fails if the remaining budget is
insufficient. One send loop owns each WS; close output waits for its completion.

## Operator configuration and legacy migration

New flags default false:

```text
Station:Online:MultiplayerEnabled
Station:Online:MultiplayerLegacyCapacityGate
Station:Online:MultiplayerProfileRegistryFile=<absolute bounded reviewed JSON>
```

MultiplayerEnabled requires RecoveryEnabled and the legacy gate. Profiles load
once, from a maximum16MiB JSON registry. Existing v2 clients receive the old wire
shape. With the gate disabled the old production behavior is preserved. With it
enabled, new legacy create/join/start is allowed only for the exact approved
`standard-2p-v1` profile supporting2. A multitap profile cannot authorize legacy
play. Existing started v2 rooms retain their recovery/heartbeat/ticket behavior.
Do not claim catalog-wide authoritative enforcement while leaving this gate off.

Other products, legacy public relay, activation, signing keys, payments and
databases are not changed. Deployment/restart/profile approval is the operator's
separate coordinated step because active sessions are in RAM.

## Supported UI surface and limits

`capabilities`, `snapshot` and `heartbeat` all return the public room list and
complete own roster, with signed names, slots, ready IDs, profile and state.
`chat` is private to authenticated current members (500characters,1persecond,
32messages/64KiB). `failed` is member-only for the exact generation.
Room invitations/join requests for v3 are not implemented in this first candidate;
the client must not send v3 room IDs to old v2 actions or leave misleading buttons.
The existing social direct conversations remain a separate surface.

Existing social blocks apply bidirectionally to v3. A public snapshot hides rooms
containing a blocked participant, and join checks every member, including guests.
Guessing a hidden room ID does not bypass this check. A successful human social
block leaves the blocking user from a shared waiting room; in a frozen/started
room it ends the shared room using the existing human-leave semantics. Other rooms
are preserved. Social commands and v3 admission share one outer gate; a copied
block set prevents nested acquisition of the social lock by the v3 hub.

Tests here use synthetic fixtures and loopback TLS. They do not demonstrate
Android gameplay, production latency, Internet stability, genuine catalog
approval, four physical devices or a published engine.
