# TurboRama Suite server candidate

This is an isolated .NET 8 service for `TURBORAMA_SUITE`. It does not reference the legacy
`TurboRamaPixOnlineServer`, its routes, or its JSON state. The kill switch defaults to disabled.

Implemented candidate scope: four signed licensing/session routes, strict JSON, RSA-PSS machine
proof verification, signed Suite assertions, PostgreSQL transactional store, expand-only migration,
correlation IDs, sanitized errors, health/readiness, cross-client golden vectors and synthetic tests.

Not production-ready: no production keys, database, edge route, service unit, admin UI, download
gateway, KMS/HSM integration, load test, pentest, TLS pin rotation, staging or deployment exists.

Local isolated validation:

```text
docker run --rm -v <repo>:/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet run --project tests/TurboRamaSuiteOnlineServer.Tests -c Release
```

Never put connection strings, activation peppers or private keys in appsettings or Git. Resolve the
three protected settings from the approved secret manager. Production provisioning remains blocked.
