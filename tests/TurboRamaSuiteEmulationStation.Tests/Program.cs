using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TurboRamaSuiteOnlineServer;

using var machine = RSA.Create(2048);
using var online = RSA.Create(2048);
using var signer = new RsaAssertionSigner(online);
var publicKey = Convert.ToBase64String(machine.ExportSubjectPublicKeyInfo());
var deviceId = Convert.ToHexString(SHA256.HashData(machine.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
var identity = new Identity("TS-ES-SYNTHETIC-TEST", deviceId, publicKey, new string('a', 64));
var suiteStore = new MemoryStore(identity);
var esStore = new MemoryStore(identity);
var suite = new SuiteService(suiteStore, signer, TimeProvider.System, string.Empty);
var es = new EmulationStationService(esStore, signer, TimeProvider.System);

await RunContractChecks(identity, suite, es, suiteStore, esStore);
await DisabledRoutes();
var connection = Environment.GetEnvironmentVariable("SUITE_ES_TEST_CONNECTION");
if (!string.IsNullOrWhiteSpace(connection))
    await PostgresChecks(connection);
else if (args.Contains("--require-postgres", StringComparer.Ordinal))
    throw new InvalidOperationException("SUITE_ES_TEST_CONNECTION is required for PostgreSQL checks.");
else
    Console.WriteLine("PostgreSQL checks not run: no isolated test connection was supplied.");
Console.WriteLine("ES SUITE CONTRACT CHECKS PASSED (synthetic keys and license only).");

async Task RunContractChecks(Identity who, SuiteService original, EmulationStationService companion,
    ISuiteStore originalStore, IEmulationStationStore companionStore)
{
    var originalSession = Hex();
    var esSession = Hex();
    var originalProof = await Proof(original.ChallengeAsync, who, originalSession);
    await Expect(() => companion.SessionAsync(originalProof, default), "CHALLENGE_INVALID");
    Verify(await original.SessionAsync(originalProof, default));
    var esProof = await Proof(companion.ChallengeAsync, who, esSession);
    await Expect(() => original.SessionAsync(esProof, default), "CHALLENGE_INVALID");
    Verify(await companion.SessionAsync(esProof, default));
    await Expect(() => companion.SessionAsync(esProof, default), "CHALLENGE_INVALID");
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    Check(await originalStore.IsActiveSessionAsync(who.License, who.Device, originalSession, now, default),
        "Opening ES must preserve the current Suite session.");
    Check(await companionStore.IsActiveSessionAsync(who.License, who.Device, esSession, now, default),
        "ES must have its own active session.");
    Check(!await originalStore.IsActiveSessionAsync(who.License, who.Device, esSession, now, default),
        "ES session must not authorize Suite content.");
    Verify(await original.SessionAsync(await Proof(original.ChallengeAsync, who, originalSession,
        "session.heartbeat"), default));
    Verify(await companion.SessionAsync(await Proof(companion.ChallengeAsync, who, esSession,
        "session.heartbeat"), default));
    var parallelSuite = await Proof(original.ChallengeAsync, who, originalSession, "session.heartbeat");
    var parallelEs = await Proof(companion.ChallengeAsync, who, esSession, "session.heartbeat");
    foreach (var response in await Task.WhenAll(original.SessionAsync(parallelSuite, default),
        companion.SessionAsync(parallelEs, default))) Verify(response);
    await Expect(() => companion.ChallengeAsync(new(1, Protocol.ProductId, who.License,
        who.Device, esSession, ContentProtocol.CatalogReadAction, Hex()), default), "ACTION_INVALID");
    await Expect(() => companion.ChallengeAsync(new(1, Protocol.ProductId, who.License,
        who.Device, esSession, "device.activate", Hex()), default), "ACTION_INVALID");
    await Expect(() => original.ChallengeAsync(new(1, Protocol.ProductId, who.License,
        who.Device, esSession, ContentProtocol.CatalogReadAction, Hex()), default), "SESSION_INVALID");
    var badHardware = await Proof(companion.ChallengeAsync, who with { Fingerprint = new string('b', 64) }, Hex());
    await Expect(() => companion.SessionAsync(badHardware, default), "PROOF_INVALID");
    var forged = await Proof(companion.ChallengeAsync, who, Hex());
    forged = forged with { Proof = forged.Proof with { Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) } };
    await Expect(() => companion.SessionAsync(forged, default), "PROOF_INVALID");
    var reopened = Hex();
    Verify(await companion.SessionAsync(await Proof(companion.ChallengeAsync, who, reopened), default));
    await Expect(async () => await companion.SessionAsync(await Proof(companion.ChallengeAsync,
        who, esSession, "session.heartbeat"), default), "SESSION_INVALID");
    Verify(await original.SessionAsync(await Proof(original.ChallengeAsync, who, originalSession,
        "session.heartbeat"), default));
    await Expect(async () => await companion.SessionAsync(await Proof(companion.ChallengeAsync,
        who, Hex(), "session.heartbeat"), default), "SESSION_INVALID");
}

async Task DisabledRoutes()
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build();
    app.MapEmulationStation(false);
    await app.StartAsync();
    using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (var route in new[] { EmulationStationService.ChallengeRoute, EmulationStationService.SessionRoute })
    {
        using var response = await http.PostAsync(route, new StringContent("{}", Encoding.UTF8, "application/json"));
        Check(response.StatusCode == HttpStatusCode.ServiceUnavailable, "Disabled ES route must fail closed.");
    }
    await app.StopAsync();
}

async Task PostgresChecks(string testConnection)
{
    await using var data = NpgsqlDataSource.Create(testConnection);
    var who = identity with { License = "TS-ES-CI-" + Guid.NewGuid().ToString("N") };
    var originalStore = new PostgresSuiteStore(data);
    var companionStore = new PostgresEmulationStationStore(data);
    var original = new SuiteService(originalStore, signer, TimeProvider.System, string.Empty);
    var companion = new EmulationStationService(companionStore, signer, TimeProvider.System);
    await Execute("""
        INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,
          license_term,identity_policy,maximum_active_devices,enrollment_state)
        VALUES($1,'TURBORAMA_SUITE','ACTIVE',true,'LIFETIME','SOFTWARE_ONLY',1,'BOUND');
        INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,
          identity_policy,algorithm,public_key_spki,hardware_fingerprint)
        VALUES($1,$2,'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',$3,$4);
        INSERT INTO suite.suite_devices(license_id,device_id,binding_type,
          public_key_spki,hardware_fingerprint,status,algorithm)
        VALUES($1,$2,'SOFTWARE_BOUND_ONLINE',$3,$4,'ACTIVE','rsa-pss-sha256');
        """, who.License, who.Device, who.PublicKey, who.Fingerprint);
    try
    {
        await RunContractChecks(who, original, companion, originalStore, companionStore);
        var pending = await Proof(companion.ChallengeAsync, who, Hex());
        await Execute("UPDATE suite.suite_licenses SET status='SUSPENDED',revocation_generation=revocation_generation+1 WHERE license_id=$1", who.License);
        await Expect(() => companion.SessionAsync(pending, default), "LICENSE_DENIED");
        await Execute("UPDATE suite.suite_licenses SET status='ACTIVE' WHERE license_id=$1", who.License);
        await Expect(() => companion.SessionAsync(pending, default), "CHALLENGE_INVALID");
        pending = await Proof(companion.ChallengeAsync, who, Hex());
        await Execute("UPDATE suite.suite_devices SET status='REVOKED' WHERE license_id=$1", who.License);
        await Expect(() => companion.SessionAsync(pending, default), "DEVICE_DENIED");
        await Execute("UPDATE suite.suite_devices SET status='ACTIVE' WHERE license_id=$1", who.License);
        var fresh = Hex();
        Verify(await companion.SessionAsync(await Proof(companion.ChallengeAsync, who, fresh), default));
        await Execute("UPDATE suite.suite_es_sessions SET authorized_until=clock_timestamp()-interval '1 second' WHERE license_id=$1", who.License);
        await Expect(async () => await companion.SessionAsync(await Proof(companion.ChallengeAsync,
            who, fresh, "session.heartbeat"), default), "SESSION_INVALID");
        await Execute("UPDATE suite.suite_licenses SET activation_consumed=false WHERE license_id=$1", who.License);
        await Expect(() => companion.ChallengeAsync(new(1, Protocol.ProductId, who.License,
            who.Device, Hex(), "session.open", Hex()), default), "DEVICE_DENIED");
        await Execute("UPDATE suite.suite_licenses SET activation_consumed=true,provisioning_origin='COMMERCE' WHERE license_id=$1", who.License);
        await Expect(async () => await companion.SessionAsync(await Proof(companion.ChallengeAsync,
            who, Hex()), default), "LICENSE_DENIED");
        await Execute("""
            INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,
              source_item_key,source_product_sku,product_id,license_id,provisioning_state,
              financial_state,last_source_version)
            VALUES('ES_SYNTHETIC_CI',$1,'suite','SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE',
              $1,'PROVISIONED','SUSPENDED',1)
            """, who.License);
        await Expect(async () => await companion.SessionAsync(await Proof(companion.ChallengeAsync,
            who, Hex()), default), "LICENSE_DENIED");
        await Execute("UPDATE suite.suite_license_deliveries SET financial_state='PAID' WHERE license_id=$1", who.License);
        Verify(await companion.SessionAsync(await Proof(companion.ChallengeAsync, who, Hex()), default));
        Console.WriteLine("ES POSTGRES CHECKS PASSED (concurrent coexistence, scope/replay, revocation, expired heartbeat, activation and commercial eligibility).");
    }
    finally
    {
        // Only this synthetic fixture in the explicitly supplied isolated test DB.
        foreach (var table in new[] { "suite_es_challenges", "suite_es_sessions",
            "suite_connection_notification_outbox", "suite_device_presence", "suite_sessions",
            "suite_challenges", "suite_license_enrollments", "suite_devices",
            "suite_license_deliveries", "suite_licenses" })
            await Execute($"DELETE FROM suite.{table} WHERE license_id=$1", who.License);
    }

    async Task Execute(string sql, params string[] values)
    {
        // A command's positional parameters are reused in each SQL statement by
        // executing separately; Npgsql's extended protocol accepts one statement.
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = data.CreateCommand(statement);
            for (var index = 0; index < values.Length; index++)
                if (statement.Contains("$" + (index + 1), StringComparison.Ordinal))
                    command.Parameters.AddWithValue(values[index]);
            await command.ExecuteNonQueryAsync();
        }
    }
}

async Task<SessionProof> Proof(Func<ChallengeRequest, CancellationToken,
    Task<SignedAssertionEnvelope>> issue, Identity who, string session, string action = "session.open")
{
    var context = new SessionContext(1, Protocol.ProductId, who.License, who.Device,
        session, action, who.Fingerprint, "ES-Suite-1.0.0");
    var hash = Protocol.SessionContextHash(context);
    var envelope = await issue(new(1, Protocol.ProductId, who.License, who.Device,
        session, action, hash), default);
    var challenge = StrictJson.Parse<OperationChallengeAssertion>(Convert.FromBase64String(envelope.Payload));
    var proofBytes = Protocol.SigningMessage(new(1, challenge.ChallengeId, challenge.Nonce,
        challenge.ExpiresAtUnixSeconds), who.License, who.Device, session, action, hash);
    try
    {
        return new(new(1, Protocol.ProductId, who.License, who.Device, session, action,
            hash, challenge.ChallengeId, Convert.ToBase64String(machine.SignData(proofBytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), context);
    }
    finally { CryptographicOperations.ZeroMemory(proofBytes); }
}

void Verify(SignedAssertionEnvelope envelope)
{
    var assertion = StrictJson.Parse<SessionAssertion>(Convert.FromBase64String(envelope.Payload));
    var message = Encoding.ASCII.GetBytes(Protocol.AssertionDomain(assertion))
        .Concat(Convert.FromBase64String(envelope.Payload)).ToArray();
    Check(online.VerifyData(message, Convert.FromBase64String(envelope.Signature),
        HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "Server session signature must verify.");
    Check(assertion.ProductId == Protocol.ProductId && assertion.Status == "ACTIVE" &&
        assertion.HeartbeatAfterSeconds == 5 && assertion.AuthorizedUntilUnixSeconds > assertion.ServerTimeUnixSeconds,
        "Signed response must preserve the Suite product and bounded authorization.");
}

static async Task Expect(Func<Task> action, string code)
{
    try { await action(); }
    catch (SuiteException exception) when (exception.Code == code) { return; }
    throw new InvalidOperationException("Expected safe denial: " + code);
}
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static string Hex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

sealed record Identity(string License, string Device, string PublicKey, string Fingerprint);

sealed class MemoryStore(Identity identity) : IEmulationStationStore
{
    private readonly Dictionary<string, ChallengeRecord> _challenges = new(StringComparer.Ordinal);
    private SessionRecord? _session;
    public Task<LicenseRecord?> FindLicenseAsync(string licenseId, CancellationToken token) => Task.FromResult<LicenseRecord?>(
        licenseId == identity.License ? new(licenseId, Protocol.ProductId, "ACTIVE", null, null, true,
            EnrollmentState: "BOUND") : null);
    public Task<DeviceRecord?> FindDeviceAsync(string licenseId, string deviceId, CancellationToken token) =>
        Task.FromResult<DeviceRecord?>(licenseId == identity.License && deviceId == identity.Device
            ? new(licenseId, deviceId, "SOFTWARE_BOUND_ONLINE", identity.PublicKey, identity.Fingerprint, "ACTIVE") : null);
    public Task<EnrollmentRecord?> FindEnrollmentAsync(string licenseId, CancellationToken token) =>
        Task.FromResult<EnrollmentRecord?>(new(identity.License, identity.Device, "SOFTWARE_BOUND_ONLINE",
            "SOFTWARE_ONLY", Protocol.Algorithm, identity.PublicKey, identity.Fingerprint));
    public Task<bool> IsActiveSessionAsync(string licenseId, string deviceId, string sessionId, long now,
        CancellationToken token) => Task.FromResult(_session is { Status: "ACTIVE" } &&
            _session.LicenseId == licenseId && _session.DeviceId == deviceId &&
            _session.SessionId == sessionId && _session.AuthorizedUntil > now);
    public Task InsertChallengeAsync(ChallengeRecord challenge, CancellationToken token)
    { _challenges.Add(challenge.ChallengeId, challenge); return Task.CompletedTask; }
    public Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken token) =>
        Task.FromResult(_challenges.TryGetValue(id, out var challenge) &&
            challenge.Action == action && challenge.ExpiresAt > now ? challenge : null);
    public Task<SessionRecord> CompleteSessionAsync(ChallengeRecord challenge, SessionRecord session,
        string action, long now, CancellationToken token)
    {
        if (!_challenges.Remove(challenge.ChallengeId))
            throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired.");
        if (action != "session.open" && (_session is null || _session.SessionId != session.SessionId))
            throw new SuiteException(409, "SESSION_INVALID", "Session is not current.");
        _session = session with { LastServerTime = Math.Max(now, (_session?.LastServerTime ?? 0) + 1) };
        return Task.FromResult(_session);
    }
    public Task<CompletionRecord?> FindCompletionAsync(string challengeId, CancellationToken token) =>
        throw new InvalidOperationException("Activation must never be used.");
    public Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord challenge,
        string requestDigest, DeviceRecord device, SignedAssertionEnvelope result, CancellationToken token) =>
        throw new InvalidOperationException("Activation must never be used.");
}
