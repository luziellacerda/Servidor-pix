# Suite content authority: offline issuance and release gate

This procedure creates the public trust artifact consumed by the signed Windows build. It does not
change the licensing/machine-proof v1 contract, commerce, PIX, catalog availability or download
authorization contracts.

## Non-negotiable key separation

There are three different key roles:

1. The **offline content-authority issuer** signs only the content-authority configuration. Its
   private key remains on an offline, encrypted owner-controlled station. It is never copied to the
   server, Windows client, deployment USB, CI, Git, chat, logs or a secret manager connected to the
   server.
2. The **online content assertion key** signs catalog pages and download grants. Only this private key
   is installed on the server, in the protected file configured by
   `Suite__ContentAssertionPrivateKeyPemFile`. The offline station receives only its canonical public
   RSA SubjectPublicKeyInfo (SPKI) DER file.
3. The existing licensing **online assertion key** remains separate and unchanged. Do not reuse it as
   either key above and do not modify machine-proof v1 bytes.

The offline issuer and online content assertion RSA keys must each be 2048 to 4096 bits and must be
different. A 3072-bit issuer on a dedicated encrypted offline volume is the recommended starting
point. The generator accepts an unencrypted PKCS#8 or PKCS#1 PEM only from an owner-only file
(`0400` or `0600` on Unix, or a protected non-inherited Windows DACL that grants only the current
owner). It fails closed when it cannot inspect those permissions. Keep that plaintext file inside the
encrypted offline volume, not on the transfer media, and power down the station after issuance.

## Build the tool before going offline

Build and test from a reviewed clean commit in a controlled connected environment, then transfer the
published tool and its independently recorded SHA-256 to the offline station. The private issuer key
must not exist while restoring or compiling dependencies.

```text
dotnet restore src/TurboRamaSuiteContentAuthorityTool/TurboRamaSuiteContentAuthorityTool.csproj --locked-mode
dotnet build src/TurboRamaSuiteContentAuthorityTool/TurboRamaSuiteContentAuthorityTool.csproj -c Release --no-restore
dotnet run --project tests/TurboRamaSuiteContentAuthorityTool.Tests/TurboRamaSuiteContentAuthorityTool.Tests.csproj -c Release
```

## Prepare public inputs

Prepare, through independently reviewed operations:

- the canonical HTTPS content base URL, including its final `/`, with no explicit default port,
  credentials, query or fragment;
- the canonical DER SPKI exported from the server's **online content assertion** public key;
- the lowercase SHA-256 pin of the current TLS server SPKI;
- optionally, a distinct lowercase SHA-256 pin for the next TLS key already staged for rotation;
- explicit issue and expiration Unix seconds. Expiration must be in the future and the interval may
  not exceed 366 days.

Public-key and pin derivation must happen in the controlled server/key ceremony. Do not move the
online private key to the offline station. Transfer only the public DER and independently compare its
SHA-256 before use.

## Generate once on the offline station

Use placeholders locally; do not paste real values, private paths or hashes into chat or tickets.

```text
dotnet TurboRamaSuiteContentAuthorityTool.dll generate \
  --base-url <CANONICAL_HTTPS_BASE_URL/> \
  --content-assertion-public-spki <ONLINE_CONTENT_ASSERTION_PUBLIC_SPKI_DER> \
  --tls-pin-current <CURRENT_TLS_SPKI_SHA256_LOWER_HEX> \
  --tls-pin-next <NEXT_TLS_SPKI_SHA256_LOWER_HEX> \
  --issued-at-unix <ISSUED_AT_UNIX_SECONDS> \
  --expires-at-unix <EXPIRES_AT_UNIX_SECONDS> \
  --issuer-private-key-pem <OFFLINE_ISSUER_PRIVATE_PEM> \
  --output-directory <NEW_APPROVED_OUTPUT_DIRECTORY>
```

Omit `--tls-pin-next` when no next TLS pin has been approved. The output directory must not already
exist and must be outside every Git work tree. The tool stages all files beside the destination,
flushes them, applies owner-only permissions (`0700`/`0600` on Unix or a protected current-owner-only
DACL on Windows), verifies those permissions and commits the directory by rename. It never prints
PEM, key bytes, signature, URL or hashes.

The immutable output contains exactly:

- `content-authority-envelope.json`: exact canonical UTF-8 envelope bytes, without BOM or newline;
- `content-authority-issuer.spki.der`: canonical public SPKI of the offline issuer;
- `content-authority-envelope.json.sha256`: lowercase sha256sum sidecar for the exact envelope bytes;
- `content-authority-issuer.spki.der.sha256`: lowercase sha256sum sidecar for the exact issuer DER.

Canonical payload and envelope serialization are deterministic and covered by a golden test. RSA-PSS
correctly uses fresh cryptographic salt during the first signature, so two independent first-time
ceremonies are not expected to have the same signature. To prevent accidental churn, rerunning the
same inputs against the same completed output directory verifies every byte, sidecar and signature and
reuses the set unchanged; different inputs or damaged files fail closed and are never overwritten.

## Independent verification and signed Windows build

An approver obtains the two lowercase 64-hex hashes through a channel independent from the artifact
transfer. Verification does not read any private key:

```text
dotnet TurboRamaSuiteContentAuthorityTool.dll verify \
  --envelope <content-authority-envelope.json> \
  --issuer-public-spki <content-authority-issuer.spki.der> \
  --envelope-sha256 <INDEPENDENT_ENVELOPE_SHA256> \
  --issuer-spki-sha256 <INDEPENDENT_ISSUER_SPKI_SHA256>
```

The command rejects hash drift, noncanonical JSON/Base64/DER/URL/pins, unknown or duplicate JSON
fields, wrong key identifiers, an expired or overlong window, issuer/content key reuse, invalid RSA
sizes and any RSA-PSS tampering.

Pass only the two public files and independently approved hashes to the existing signed client build:

```text
-ContentAuthorityConfigurationPath <content-authority-envelope.json>
-ContentAuthorityConfigurationSha256 <INDEPENDENT_ENVELOPE_SHA256>
-ContentAuthorityIssuerSpkiPath <content-authority-issuer.spki.der>
-ContentAuthorityIssuerSpkiSha256 <INDEPENDENT_ISSUER_SPKI_SHA256>
```

The current `Build-Production.ps1` gate already hashes both exact files, verifies the embedded public
metadata and refuses a signed build when any one of these four inputs is absent or inconsistent. No
private key is a client build input.

Before public release, the canary must additionally verify that the server's configured content
assertion `KeyId` equals the `contentAssertionKeyId` in this envelope, validate the TLS pin, read all
850 signed catalog identities and authorize/stream a READY artifact. A mismatch disables content
routes; it must never fall back to the licensing key.

## Rotation and recovery

- Stage a distinct next TLS pin before certificate-key rotation. Issue and independently approve a
  new envelope; never edit an approved JSON file.
- Rotate the online content assertion key by first exporting only its new public SPKI and issuing a
  new envelope. Deploy server key and signed client release as one controlled compatibility window.
- If the offline issuer might be exposed, revoke the ceremony, retain evidence, create a new offline
  issuer and require a new signed client trust root. Do not copy the suspect private key for analysis.
- Preserve encrypted owner backups of the offline issuer under dual control. The online server backup
  contains only the online content assertion private key and its public expected `KeyId`.
