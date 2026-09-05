using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TurboRamaSuiteOnlineServer;

internal static class SharedIntegrationChecks
{
    public static async Task RunAsync(string connection)
    {
        Check(new NpgsqlConnectionStringBuilder(SuiteDatabasePoolPolicy.ApplyDefaults("Host=localhost;Database=synthetic")).MaxPoolSize==8,
            "The default licensing pool must reserve database capacity for other services.");
        Check(new NpgsqlConnectionStringBuilder(SuiteDatabasePoolPolicy.ApplyDefaults("Host=localhost;Maximum Pool Size=16")).MaxPoolSize==16,
            "Explicit operator connection budgets must be preserved.");
        Check(new NpgsqlConnectionStringBuilder(SuiteDatabasePoolPolicy.ApplyDefaults("Host=localhost;Minimum Pool Size=40")).MaxPoolSize==40,
            "Defaults must not invalidate an existing explicit minimum pool size.");
        await using var db = NpgsqlDataSource.Create(connection);
        using var online = RSA.Create(2048);
        using var signer = new RsaAssertionSigner(online);
        using var a = new SyntheticClient();
        using var b = new SyntheticClient();
        await a.Seed(db); await b.Seed(db);
        await using var app = CreateApp(connection, signer, true);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        foreach (var bad in new[] { "", "emulationstation", "UNKNOWN", "EMULATIONSTATION,EMULATIONSTATION" })
            await ExpectStatus(http, "/v1/suite/challenges", new[] { bad }, 400);
        await ExpectStatus(http, "/v1/suite/challenges", new[] { "EMULATIONSTATION", "EMULATIONSTATION" }, 400);
        foreach (var route in new[] { "/v1/suite/activations/complete", "/v1/suite/devices/inventory",
            "/v1/suite-content/catalog/current", EmulationStationService.ChallengeRoute,
            EmulationStationService.SessionRoute })
            await ExpectStatus(http, route, new[] { "EMULATIONSTATION" }, 400);

        var suiteA = SyntheticClient.Hex(); var esA = SyntheticClient.Hex();
        var suiteB = SyntheticClient.Hex(); var esB = SyntheticClient.Hex();
        foreach (var (who, id, scope) in new[] { (a,suiteA,"SUITE"), (a,esA,"SHARED"),
            (b,suiteB,"SUITE"), (b,esB,"SHARED") })
            await Exchange(http, online, who, id, scope);
        await Task.WhenAll(Exchange(http, online, a, suiteA, "SUITE", true),
            Exchange(http, online, a, esA, "SHARED", true),
            Exchange(http, online, b, suiteB, "SUITE", true),
            Exchange(http, online, b, esB, "SHARED", true));

        var pendingOldHeartbeat = await Proof(http, online, a, esA, "SHARED", true);
        var candidate = SyntheticClient.Hex();
        var reopenProof = await Proof(http, online, a, candidate, "SHARED");
        // Merely asking for a fresh challenge must not disturb the current session.
        await Exchange(http, online, a, esA, "SHARED", true);
        var reopened = Verify(online, await Submit(http, reopenProof, "SHARED"),
            EmulationStationAssertionSigner.OpenKind);
        Check(reopened.Status == "ACTIVE" && reopened.SessionId == candidate &&
            reopened.LicenseId == a.License && reopened.DeviceId == a.Device &&
            reopened.AuthorizedUntilUnixSeconds > reopened.ServerTimeUnixSeconds &&
            reopened.HeartbeatAfterSeconds == 5,
            "A freshly validated open must replace the previous ES session exactly as Suite does.");
        await ExpectError(http, reopenProof, "SHARED", "CHALLENGE_INVALID");
        await ExpectError(http, pendingOldHeartbeat, "SHARED", "SESSION_INVALID");
        await ExpectError(http, await Proof(http, online, a, esA, "SHARED", true),
            "SHARED", "SESSION_INVALID");
        esA = candidate;
        await Exchange(http, online, a, esA, "SHARED", true);

        // A failed attempt cannot replace either this ES session or another tenant.
        var forged = await Proof(http, online, a, SyntheticClient.Hex(), "SHARED", signingKey: b.Key);
        await ExpectError(http, forged, "SHARED", "PROOF_INVALID");
        var badHardware = await Proof(http, online, a, SyntheticClient.Hex(), "SHARED",
            fingerprint: SyntheticClient.Hex());
        await ExpectError(http, badHardware, "SHARED", "PROOF_INVALID");
        foreach (var (license, device) in new[] { (a.License, b.Device), (b.License, a.Device) })
        {
            using var wrongIdentity = await Send(http, EmulationStationScope.ChallengeRoute,
                new ChallengeRequest(1, Protocol.ProductId, license, device, SyntheticClient.Hex(),
                    "session.open", SyntheticClient.Hex()), "SHARED");
            Check(wrongIdentity.StatusCode == HttpStatusCode.Forbidden &&
                (await wrongIdentity.Content.ReadFromJsonAsync<ErrorResponse>())?.Code == "DEVICE_DENIED",
                "A different device or license/device pairing must not obtain a replacement challenge.");
        }
        await Task.WhenAll(Exchange(http, online, a, suiteA, "SUITE", true),
            Exchange(http, online, a, esA, "SHARED", true),
            Exchange(http, online, b, suiteB, "SUITE", true),
            Exchange(http, online, b, esB, "SHARED", true));

        // Keep a direct regression reference for the unchanged original Suite policy.
        var oldSuite = suiteA;
        suiteA = SyntheticClient.Hex();
        await Exchange(http, online, a, suiteA, "SUITE");
        await ExpectError(http, await Proof(http, online, a, oldSuite, "SUITE", true),
            "SUITE", "SESSION_INVALID");
        await Exchange(http, online, a, esA, "SHARED", true);

        var shared = await Proof(http, online, a, SyntheticClient.Hex(), "SHARED");
        await ExpectError(http, shared, "SUITE", "CHALLENGE_INVALID");
        await ExpectError(http, shared, "DEDICATED", "CHALLENGE_INVALID");
        var legacy = await Proof(http, online, a, SyntheticClient.Hex(), "DEDICATED");
        await ExpectError(http, legacy, "SHARED", "CHALLENGE_INVALID");
        Verify(online, await Submit(http, legacy, "DEDICATED"), Protocol.SessionOpenKind);
        await Exchange(http, online, a, suiteA, "SUITE", true);
        await Exchange(http, online, b, suiteB, "SUITE", true);
        await Exchange(http, online, b, esB, "SHARED", true);

        var original = await Proof(http, online, b, SyntheticClient.Hex(), "SUITE");
        await ExpectError(http, original, "SHARED", "CHALLENGE_INVALID");
        var wrong = await Proof(http, online, a, SyntheticClient.Hex(), "SHARED");
        wrong = wrong with { Proof = wrong.Proof with { Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) } };
        await ExpectError(http, wrong, "SHARED", "PROOF_INVALID");

        await app.StopAsync();
        await using var disabled = CreateApp(connection, signer, false);
        await disabled.StartAsync();
        using var off = new HttpClient { BaseAddress = new Uri(disabled.Urls.Single()) };
        await ExpectStatus(off, "/v1/suite/challenges", new[] { "EMULATIONSTATION" }, 503);
        await Exchange(off, online, b, suiteB, "SUITE", true);
        await disabled.StopAsync();
        Console.WriteLine("SHARED ES HTTP/POSTGRES PASSED: strict header, four signed kinds, validated replacement, stale heartbeat denial, rejected foreign proof/device, scope/replay, legacy, A/B, simultaneous heartbeat and disabled flag.");
    }

    internal static WebApplication CreateApp(string connection, IAssertionSigner signer, bool enabled, bool untrustedOrigin = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = Protocol.MaximumBodyBytes);
        builder.Services.AddSingleton<NpgsqlDataSource>(_=>NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(SuiteDatabasePoolPolicy.ApplyDefaults(connection))
            {Options="-c role=turborama-suite"}.ConnectionString));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(signer);
        builder.Services.AddSingleton<ISuiteStore, PostgresSuiteStore>();
        builder.Services.AddSingleton<IEmulationStationStore, PostgresEmulationStationStore>();
        builder.Services.AddSingleton<ISharedEmulationStationStore, PostgresSharedEmulationStationStore>();
        builder.Services.AddSingleton<EmulationStationService>();
        builder.Services.AddSingleton<SharedEmulationStationService>();
        builder.Services.AddSingleton<SuiteRateLimiter>();
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(SuiteTrustedProxyPolicy.Configure);
        builder.Services.AddSingleton(new InventorySensitiveProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
        builder.Services.AddSingleton(new NetworkInventoryOptions(1));
        builder.Services.AddSingleton<NetworkInventoryService>();
        builder.Services.AddSingleton(sp => new SuiteService(sp.GetRequiredService<ISuiteStore>(),
            signer, TimeProvider.System, "synthetic-only"));
        var app = builder.Build();
        using(var databaseProbe=app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnection())
        using(var databaseCommand=databaseProbe.CreateCommand())
        { databaseCommand.CommandText="SELECT count(*) FROM suite.suite_licenses";_=databaseCommand.ExecuteScalar(); }
        if(untrustedOrigin)app.Use((context,next)=>{context.Connection.RemoteIpAddress=IPAddress.Parse("203.0.113.20");return next(context);});
        app.UseForwardedHeaders();
        app.Use(EmulationStationScope.Guard);
        SuiteSessionEndpoints.Map<ChallengeRequest>(app, true, enabled, "/v1/suite/challenges", (s,r,c) => s.ChallengeAsync(r,c));
        SuiteSessionEndpoints.Map<SessionProof>(app, true, enabled, "/v1/suite/sessions", (s,r,c) => s.SessionAsync(r,c));
        app.MapEmulationStation(enabled);
        app.MapNetworkInventory(enabled);
        return app;
    }

    internal static async Task<SessionAssertion> Exchange(HttpClient http, RSA online, SyntheticClient who,
        string session, string scope, bool heartbeat = false)
    {
        var proof = await Proof(http, online, who, session, scope, heartbeat);
        var result = Verify(online, await Submit(http, proof, scope), scope == "SHARED"
            ? heartbeat ? EmulationStationAssertionSigner.HeartbeatKind : EmulationStationAssertionSigner.OpenKind
            : heartbeat ? Protocol.SessionHeartbeatKind : Protocol.SessionOpenKind);
        Check(result.Status == "ACTIVE" && result.SessionId == session && result.LicenseId == who.License,
            "A signed grant must match this client and instance.");
        return result;
    }

    internal static async Task<SessionProof> Proof(HttpClient http, RSA online, SyntheticClient who,
        string session, string scope, bool heartbeat = false, string? fingerprint = null, RSA? signingKey = null)
    {
        var context = new SessionContext(1, Protocol.ProductId, who.License, who.Device,
            session, heartbeat ? "session.heartbeat" : "session.open", fingerprint ?? who.Fingerprint, "ES-Suite-1.1.0-test");
        var hash = Protocol.SessionContextHash(context);
        using var response = await Send(http, Route(scope, true), new ChallengeRequest(1, Protocol.ProductId,
            who.License, who.Device, session, context.Action, hash), scope);
        response.EnsureSuccessStatusCode();
        var envelope = (await response.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
        var challenge = StrictJson.Parse<OperationChallengeAssertion>(Convert.FromBase64String(envelope.Payload));
        var kind = scope == "SHARED"
            ? heartbeat ? EmulationStationAssertionSigner.HeartbeatChallengeKind : EmulationStationAssertionSigner.OpenChallengeKind
            : heartbeat ? Protocol.SessionHeartbeatChallengeKind : Protocol.SessionOpenChallengeKind;
        Check(envelope.Kind == kind && challenge.Kind == kind && challenge.ContextHash == hash,
            "Client must check the signed ES kind before invoking its machine signer.");
        VerifySignature(online, envelope, challenge);
        var bytes = Protocol.SigningMessage(new(1, challenge.ChallengeId, challenge.Nonce,
            challenge.ExpiresAtUnixSeconds), who.License, who.Device, session, context.Action, hash);
        return new(new(1, Protocol.ProductId, who.License, who.Device, session, context.Action,
            hash, challenge.ChallengeId, Convert.ToBase64String((signingKey ?? who.Key).SignData(bytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss))), context);
    }

    internal static async Task<SignedAssertionEnvelope> Submit(HttpClient http, SessionProof proof, string scope)
    {
        using var response = await Send(http, Route(scope, false), proof, scope);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
    }

    private static string Route(string scope, bool challenge) => scope switch
    {
        "DEDICATED" => challenge ? EmulationStationService.ChallengeRoute : EmulationStationService.SessionRoute,
        "SUITE" or "SHARED" => challenge ? EmulationStationScope.ChallengeRoute : EmulationStationScope.SessionRoute,
        _ => throw new InvalidOperationException("Unknown synthetic application scope.")
    };

    internal static Task<HttpResponseMessage> Send(HttpClient http, string route, object body, string scope)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        { Content = new StringContent(JsonSerializer.Serialize(body, StrictJson.Options), Encoding.UTF8, "application/json") };
        if (scope == "SHARED") request.Headers.Add(EmulationStationScope.Header, EmulationStationScope.Value);
        return http.SendAsync(request);
    }

    private static async Task ExpectError(HttpClient http, SessionProof proof, string scope, string code)
    {
        using var response = await Send(http, Route(scope, false), proof, scope);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Check(!response.IsSuccessStatusCode && error?.Code == code, "Expected denial: " + code);
    }

    private static async Task ExpectStatus(HttpClient http, string route, string[] headers, int status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation(EmulationStationScope.Header, headers);
        using var response = await http.SendAsync(request);
        Check((int)response.StatusCode == status, "Strict header or scope must be rejected: " + route);
    }

    private static SessionAssertion Verify(RSA online, SignedAssertionEnvelope envelope, string kind)
    {
        var assertion = StrictJson.Parse<SessionAssertion>(Convert.FromBase64String(envelope.Payload));
        Check(envelope.Kind == kind && assertion.Kind == kind, "Both signed kinds must agree.");
        VerifySignature(online, envelope, assertion);
        return assertion;
    }

    private static void VerifySignature(RSA online, SignedAssertionEnvelope envelope, object assertion)
    {
        var payload = Convert.FromBase64String(envelope.Payload);
        Check(payload.AsSpan().SequenceEqual(Protocol.CanonicalAssertion(assertion)), "Canonical assertion bytes must match.");
        var message = Encoding.ASCII.GetBytes(Protocol.AssertionDomain(assertion)).Concat(payload).ToArray();
        Check(online.VerifyData(message, Convert.FromBase64String(envelope.Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "The assertion signature must verify.");
        message[^1] ^= 1;
        Check(!online.VerifyData(message, Convert.FromBase64String(envelope.Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "Payload tampering must invalidate the signature.");
    }

    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

internal sealed class SyntheticClient : IDisposable
{
    public async Task PublishNetworkAsync(HttpClient http,string session)
    {
        var context=new TurboRamaSuite.Network.NetworkInventoryContext(1,Protocol.ProductId,License,Device,session,
            "EMULATIONSTATION",TurboRamaSuite.Network.NetworkInventoryContract.Action,Fingerprint,"ES-1.1.0",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),[new("02:11:22:33:44:55","WIRELESS",true,false)]);
        var hash=TurboRamaSuite.Network.NetworkInventoryContract.Hash(context);
        using var issued=await SharedIntegrationChecks.Send(http,TurboRamaSuite.Network.NetworkInventoryContract.ChallengeRoute,
            new TurboRamaSuite.Network.NetworkChallengeRequest(1,Protocol.ProductId,License,Device,session,context.AppScope,context.Action,hash),"NETWORK");
        issued.EnsureSuccessStatusCode();var envelope=(await issued.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
        var challenge=StrictJson.Parse<TurboRamaSuite.Network.NetworkAssertion>(Convert.FromBase64String(envelope.Payload));
        var bytes=TurboRamaSuite.Network.NetworkInventoryContract.SigningMessage(new(1,Protocol.ProductId,License,Device,session,context.AppScope,context.Action,hash,
            challenge.ChallengeId,challenge.Nonce,challenge.ExpiresAtUnixSeconds));
        using var accepted=await SharedIntegrationChecks.Send(http,TurboRamaSuite.Network.NetworkInventoryContract.InventoryRoute,
            new TurboRamaSuite.Network.NetworkInventoryProof(context,challenge.ChallengeId,Convert.ToBase64String(Key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))),"NETWORK");
        accepted.EnsureSuccessStatusCode();
    }
    public RSA Key { get; } = RSA.Create(2048);
    public string License { get; } = "TS-ES-" + Guid.NewGuid().ToString("N");
    public string Device => Convert.ToHexString(SHA256.HashData(Key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    public string Fingerprint { get; } = Hex();
    public static string Hex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public void Dispose() => Key.Dispose();
    public async Task Seed(NpgsqlDataSource db)
    {
        foreach (var sql in new[]
        {
            "INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,license_term,identity_policy,maximum_active_devices,enrollment_state) VALUES($1,'TURBORAMA_SUITE','ACTIVE',true,'LIFETIME','SOFTWARE_ONLY',1,'BOUND')",
            "INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint) VALUES($1,$2,'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',$3,$4)",
            "INSERT INTO suite.suite_devices(license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm) VALUES($1,$2,'SOFTWARE_BOUND_ONLINE',$3,$4,'ACTIVE','rsa-pss-sha256')"
        })
        {
            await using var cmd = db.CreateCommand(sql);
            cmd.Parameters.AddWithValue(License);
            if (sql.Contains("$2", StringComparison.Ordinal))
            {
                cmd.Parameters.AddWithValue(Device);
                cmd.Parameters.AddWithValue(Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo()));
                cmd.Parameters.AddWithValue(Fingerprint);
            }
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
