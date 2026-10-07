# Native handshake audit — R72 black screen

Read-only audit of frozen R71 native runtime reused byte-for-byte by R72.
No production native source, phone, registry or server was changed by this audit.

## Proven source defect

Source root: `E:\R71fixed\RetroArch-69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576`.

`network/netplay/netplay_frontend.c` SHA-256:
`e221e40605aecce5f168fd68ab9f06fda1eb8297a97ba6c1aff3a6e660fa8d22`.

The recovery wait branch suppresses the normal output-buffer flush:

1. `runloop.c:7365–7370` calls `station_netplay_recovery_poll()`, confirms waiting,
   sleeps and returns before the regular emulation/post-frame path.
2. `netplay_frontend.c:8967–8973` only calls `netplay_sync_pre_frame()`, then tests
   `connected_players > 1` (host) or `self_mode == PLAYING` (guest).
3. `netplay_sync_pre_frame():3829–3832` polls input; it does not contain the
   unconditional output-buffer flush from `netplay_post_frame():9075–9083`.
4. The guest receives SYNC, remains SPECTATING and requests PLAY at
   `1996–2014`. The handshake wrapper sends current input and flushes at
   `2056–2059` / `4502–4504`, so the PLAY request can reach the host.
5. The host accepts PLAY, adds the player to its bitmask at `4965`, sets the
   connection to PLAYING at `5001`, then queues MODE at `5021–5022`.
   `netplay_send_raw_cmd():4515–4530` only queues these bytes.
6. The frozen frame already has local input, so `get_self_input_state():8028–8029`
   returns immediately on subsequent polls. It does not reach the regular
   `send_cur_input()` path at `8144–8150` to flush queued MODE.
7. Because no emulation frame/post-frame may run until both peers report native
   readiness, the guest cannot consume the host's queued MODE and remains
   SPECTATING. This is a circular wait in the native startup path. Healthy WSS
   PONGs do not flush this separate native application buffer.

This demonstrates an actual defect in the startup pump. It does not, alone,
prove that this is the only cause of the current devices' black screen. Native
mode/buffer diagnostics and real gameplay after a coordinated update remain
necessary. A connected transport is not proof of an emulating frame.

## Minimal candidate

`recovery_poll_candidate.h` replaces ONLY `station_netplay_recovery_poll()`.

After the existing pre-frame poll, it iterates active connections and calls:

```c
netplay_send_flush(&connection->send_packet_buffer, connection->fd, false)
```

- `false` means nonblocking, using RetroArch's existing send-ring implementation.
- Partial and zero-byte writes retain original bytes and offsets for the next poll.
- Readiness remains false while any active send ring still has pending bytes.
- A real native send error follows the existing terminal native-failure path.
- It does not call `netplay_send_flush_all()`: that helper uses blocking sends.
- It does not call `netplay_post_frame()`: that function advances frames and can
  invoke synchronization/replay work, violating the recovery pause invariant.
- No new packet, protocol version, frame, emulator preset or control overlay is
  introduced. Guest readiness still requires PLAYING; host still requires >1.

Function normalized LF/no-final-newline SHA-256:
`d3a42419124dda592fea217088ed4e4687bf63718f657299ac8d73ad9fa41e12`.

The shared file's exact bytes include Windows line endings; its artifact hash is
recorded separately by the native builder. Do not compare an LF text hash with
the raw artifact hash.

## Executable regression

`recovery_flush_probe.py` extracts verbatim the actual source implementations of
`buf_used`, `buf_remaining`, `netplay_send`, `netplay_send_flush`,
`netplay_send_raw_cmd`, `station_netplay_recovery_poll`, and the real
`socket_buffer` structure. Source hashes and the MODE opcode (`0x0026`) are gated.

The handshake producer and socket transport are explicitly modelled. A MODE
packet with a small sentinel payload is queued with the real raw-command function;
the test does not claim to implement the full MODE payload or a ROM handshake.

Executed in `E:\R73FlushProbe2` with local LLVM clang:

- Original pump: fails check 1 after 64 waiting polls; MODE remains buffered.
- Candidate pump: 22 checks pass.
- Cases: normal drain, exact byte order, single enqueue on frozen frame,
  partial writes, pending readiness, zero/backpressure, resumed writes,
  wrapped ring, real send failure, inactive connection, null session,
  failed pre-frame poll, unchanged host/guest readiness and no frame advancement.
- No socket/network, Android process, game ROM or physical multiplayer is used.

Receipt: `recovery-flush-probe.json`.

## Serialization bootstrap investigation

The recovery path also bypasses `netplay_pre_frame():8983–8984`, where upstream
retries initialization for cores declaring `NETPLAY_QUIRK_INITIALIZATION`.
That could matter for a future core requiring frames before serialization, but
it is not supported as the cause of this Battletoads run:

- bsnes-mercury `79d7f9de218b` C/C++ sources do not set
  `RETRO_ENVIRONMENT_SET_SERIALIZATION_QUIRKS` / `MUST_INITIALIZE`.
- Its `target-libretro/libretro.cpp:698–704` exposes serialization directly.
  Source hash `90bfb5826f9e2e85a05d11137eeeebdbc05359b7eba3632d4296e87c5dae007a`.
- clownmdemu `d43c2708b0a3` `source/libretro-interface.c:736–737` declares
  ENDIAN_DEPENDENT | PLATFORM_DEPENDENT only, not MUST_INITIALIZE.
  Source hash `9c9532db183528f28440a5f58226ec8bf81afff1ec26184db72da669e9cd4e0b`.
- Upstream maps MUST_INITIALIZE only at `netplay_frontend.c:9171–9177`;
  normal cores initialize serialization before startup in `7244–7267`.

Do not add speculative `core_run()` during waiting or manipulate a serializer
flag to address this unrelated possibility.

## Required device validation

After runtime identity/registry are correctly coordinated, use a new room with
both updated clients: host and guest native connection modes, pending send bytes,
native paused/ready acknowledgement, server READY/RUNNING state, first rendered
frame, both controls, and human exit. Reverse roles and repeat. Recovery under a
real network interruption must be checked separately from initial launch. Local
regression does not establish either gameplay or reconnection success.
