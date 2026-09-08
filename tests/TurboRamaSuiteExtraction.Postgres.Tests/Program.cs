using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TurboRamaSuiteNotifications;
using TurboRamaSuiteOnlineServer;

var connection = Environment.GetEnvironmentVariable("SUITE_EXTRACTION_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Explicit isolated test connection required.");
var settings = new NpgsqlConnectionStringBuilder(connection);
Check(settings.Host is "127.0.0.1" or "localhost"
    && settings.Database is "turborama_extraction_ci" or "turborama_es_ci",
    "Only the named loopback CI databases are allowed; never use production.");
await using var db = NpgsqlDataSource.Create(connection);
Check((string?)await Scalar("SELECT current_database()") == settings.Database, "Unexpected database.");
Check((long)(await Scalar("SELECT count(*) FROM suite.suite_extraction_notification_outbox") ?? -1L) == 0,
    "Test database must start with an empty extraction outbox.");

var temp = Path.Combine(Path.GetTempPath(), "trb-extraction-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
if (OperatingSystem.IsLinux()) File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
using var keyA = RSA.Create(2048);
using var keyB = RSA.Create(2048);
var catalog = Hex();
var fixtures = new[] { Identity(keyA, "A"), Identity(keyB, "B") };
Process? admin = null;
Task<string>? adminOutput = null;
Task<string>? adminErrors = null;
WebApplication? api = null;
try
{
    await Seed();
    var apiSettings = new NpgsqlConnectionStringBuilder(connection) { Options = "-c role=turborama-suite", MaxPoolSize = 16 };
    await using var apiDb = NpgsqlDataSource.Create(apiSettings.ConnectionString);
    await using (var c = apiDb.CreateCommand("SELECT current_user"))
        Check((string?)await c.ExecuteScalarAsync() == "turborama-suite", "API role was not applied.");
    var builder = WebApplication.CreateSlimBuilder();
    builder.Logging.ClearProviders();
    builder.Services.AddSingleton(apiDb);
    api = builder.Build();
    api.Urls.Add("http://127.0.0.1:0");
    ExtractionNotificationEndpoints.Map(api, true);
    await api.StartAsync();
    using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    { BaseAddress = new Uri(api.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
    var first = Proof(fixtures[0], 0);
    await Expect(first, 202, "ACCEPTED");
    await Expect(first, 200, "ALREADY_ACCEPTED");
    var repeated = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ =>
        new ExtractionNotificationStore(apiDb).AcceptAsync(first, default)));
    Check(repeated.All(x => x.Status == "ALREADY_ACCEPTED"), "Concurrent replay was not idempotent.");
    Check(await Count() == 1, "Replay inserted duplicate events.");

    var second = Proof(fixtures[1], 0);
    await Expect(second with { Signature = first.Signature }, 403);
    await Expect(first with { SessionId = fixtures[1].Session }, 409);
    await Sql("UPDATE suite.suite_license_deliveries SET financial_state='SUSPENDED' WHERE license_id=$1", fixtures[1].License);
    await Expect(second, 409);
    await Sql("UPDATE suite.suite_license_deliveries SET financial_state='PAID' WHERE license_id=$1", fixtures[1].License);
    await Sql("UPDATE suite.suite_devices SET status='REVOKED' WHERE license_id=$1", fixtures[1].License);
    await Expect(second, 409);
    await Sql("UPDATE suite.suite_devices SET status='ACTIVE' WHERE license_id=$1", fixtures[1].License);
    await Sql("UPDATE suite.suite_sessions SET authorized_until=clock_timestamp()-interval '1 second' WHERE license_id=$1", fixtures[1].License);
    await Expect(second, 409);
    await Sql("UPDATE suite.suite_sessions SET authorized_until=clock_timestamp()+interval '1 hour' WHERE license_id=$1", fixtures[1].License);
    await Expect(Proof(fixtures[1], 0, DateTimeOffset.UtcNow.AddDays(-8).ToUnixTimeSeconds()), 403);
    await Expect(second, 202, "ACCEPTED");
    await Expect(second, 200, "ALREADY_ACCEPTED");
    var accepted = await Task.WhenAll(Enumerable.Range(1, 20).Select(i =>
        new ExtractionNotificationStore(apiDb).AcceptAsync(Proof(fixtures[0], i), default)));
    Check(accepted.All(x => x.Status == "ACCEPTED") && await Count() == 22,
        "Concurrent distinct events were lost or duplicated.");
    Console.WriteLine("PASS: PG16 runtime API role, real RSA proofs, paid/active context, cross-session isolation, expiry, HTTP ACKs and concurrent deduplication.");

    var token = Hex();
    var tokenPath = Path.Combine(temp, "admin-token");
    var pepperPath = Path.Combine(temp, "pepper");
    File.WriteAllText(tokenPath, token);
    File.WriteAllBytes(pepperPath, RandomNumberGenerator.GetBytes(32));
    if (OperatingSystem.IsLinux())
    {
        File.SetUnixFileMode(tokenPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.SetUnixFileMode(pepperPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    var socketPath = Path.Combine(temp, "admin.sock");
    var adminDll = Path.GetFullPath("src/TurboRamaSuiteAdminServer/bin/Release/net8.0/TurboRamaSuiteAdminServer.dll");
    Check(File.Exists(adminDll), "Build the complete Admin project before the native test.");
    var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(adminDll);
    foreach (var name in start.Environment.Keys.Where(x => x.StartsWith("SUITE_", StringComparison.Ordinal)).ToArray()) start.Environment.Remove(name);
    start.Environment["SUITE_ADMIN_SOCKET"] = socketPath;
    start.Environment["SUITE_ADMIN_TOKEN_FILE"] = tokenPath;
    start.Environment["SUITE_ADMIN_PEPPER_FILE"] = pepperPath;
    start.Environment["SUITE_ADMIN_CONNECTION"] = new NpgsqlConnectionStringBuilder(connection)
        { Options = "-c role=turborama-suite-admin", MaxPoolSize = 8 }.ConnectionString;
    start.Environment["SUITE_EXTRACTION_NOTICES_ENABLED"] = "1";
    start.Environment["Logging__LogLevel__Default"] = "Warning";
    admin = Process.Start(start) ?? throw new InvalidOperationException("Admin fixture did not start.");
    adminOutput = admin.StandardOutput.ReadToEndAsync();
    adminErrors = admin.StandardError.ReadToEndAsync();
    using var handler = new SocketsHttpHandler
    {
        UseProxy = false, AllowAutoRedirect = false,
        ConnectCallback = async (_, ct) =>
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        }
    };
    using var bridge = new HttpClient(handler) { BaseAddress = new Uri("http://localhost"), Timeout = TimeSpan.FromSeconds(15) };
    for (var attempt = 0; attempt < 100 && !File.Exists(socketPath) && !admin.HasExited; attempt++) await Task.Delay(50);
    Check(!admin.HasExited && File.Exists(socketPath), "Admin fixture socket unavailable.");
    using (var denied = await bridge.PostAsJsonAsync("/extraction-notifications/lease", new { }))
        Check(denied.StatusCode == HttpStatusCode.NotFound, "Unauthenticated Admin access was accepted.");
    bridge.DefaultRequestHeaders.Add("X-Suite-Admin-Token", token);
    var leases = await Task.WhenAll(Enumerable.Range(0, 30).Select(async _ =>
    {
        using var response = await bridge.PostAsJsonAsync("/extraction-notifications/lease", new { });
        if (response.StatusCode == HttpStatusCode.NoContent) return (JsonElement?)null;
        Check(response.IsSuccessStatusCode, "Lease HTTP failure.");
        return (JsonElement?)JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }));
    var jobs = leases.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
    Check(jobs.Length == 22 && jobs.Select(x => x.GetProperty("eventId").GetString()).Distinct().Count() == 22,
        "Concurrent workers leased one event twice or lost an event.");
    var dispatch = jobs[0];
    var eventId = dispatch.GetProperty("eventId").GetString()!;
    var lease = dispatch.GetProperty("leaseToken").GetString()!;
    using (var wrong = await bridge.PostAsJsonAsync("/extraction-notifications/begin-dispatch",
        new { eventId, leaseToken = Guid.NewGuid(), customerName = "Pessoa Teste" }))
        Check(wrong.StatusCode == HttpStatusCode.Conflict, "Stale/foreign lease could dispatch.");
    using (var begin = await bridge.PostAsJsonAsync("/extraction-notifications/begin-dispatch",
        new { eventId, leaseToken = lease, customerName = "Pessoa Teste" }))
    {
        Check(begin.IsSuccessStatusCode, "Dispatch failed for a valid active paid fixture.");
        var text = (await begin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()!;
        Check(text.Contains("LZ GAMES | TURBORAMA SUITE", StringComparison.Ordinal)
            && !text.Contains(fixtures[0].License, StringComparison.Ordinal)
            && !text.Contains(fixtures[0].Device, StringComparison.Ordinal), "Message contract/privacy regression.");
    }
    using (var retry = await bridge.PostAsJsonAsync("/extraction-notifications/complete",
        new { eventId, leaseToken = lease, outcome = "RETRY", errorCode = "FIXTURE" }))
        Check(retry.StatusCode == HttpStatusCode.Conflict, "Ambiguous dispatched event could be retried.");
    await Sql("UPDATE suite.suite_extraction_notification_outbox SET lease_until=clock_timestamp()-interval '1 second' WHERE event_id=$1", eventId);
    using (var sweep = await bridge.PostAsJsonAsync("/extraction-notifications/lease", new { }))
        Check(sweep.StatusCode == HttpStatusCode.NoContent, "Ambiguous dispatch was recycled.");
    Check((string?)await Scalar("SELECT status FROM suite.suite_extraction_notification_outbox WHERE event_id=$1", eventId) == "UNCERTAIN",
        "Expired dispatch did not enter UNCERTAIN.");
    using (var lateAck = await bridge.PostAsJsonAsync("/extraction-notifications/complete",
        new { eventId, leaseToken = lease, outcome = "QUEUED", errorCode = "" }))
        Check(lateAck.IsSuccessStatusCode, "A matching late confirmed queue ACK was rejected.");
    var beforeDispatch = jobs[1];
    var reusable = beforeDispatch.GetProperty("eventId").GetString()!;
    var oldLease = beforeDispatch.GetProperty("leaseToken").GetString()!;
    await Sql("UPDATE suite.suite_extraction_notification_outbox SET lease_until=clock_timestamp()-interval '1 second' WHERE event_id=$1", reusable);
    using (var renewed = await bridge.PostAsJsonAsync("/extraction-notifications/lease", new { }))
    {
        var current = await renewed.Content.ReadFromJsonAsync<JsonElement>();
        Check(current.GetProperty("eventId").GetString() == reusable && current.GetProperty("leaseToken").GetString() != oldLease,
            "Expired lease before dispatch did not receive a new owner token.");
    }
    using (var staleAck = await bridge.PostAsJsonAsync("/extraction-notifications/complete",
        new { eventId = reusable, leaseToken = oldLease, outcome = "SKIPPED", errorCode = "FIXTURE" }))
        Check(staleAck.StatusCode == HttpStatusCode.Conflict, "Old worker acknowledged a new lease.");
    Console.WriteLine("PASS: real authenticated Admin socket, runtime role, 30 concurrent workers, lease fencing, UNCERTAIN boundary and late queue ACK; no provider or real recipient.");

    async Task Expect(ExtractionCompletionProof proof, int expected, string? ack = null)
    {
        using var result = await client.PostAsJsonAsync(ExtractionCompletionProtocol.Route, proof, ExtractionCompletionProtocol.JsonOptions);
        // In this synthetic isolated fixture only, expose the native SQL failure
        // hidden by the public endpoint's intentionally sanitized 503 response.
        if (result.StatusCode == HttpStatusCode.ServiceUnavailable)
            await new ExtractionNotificationStore(apiDb).AcceptAsync(proof, default);
        Check((int)result.StatusCode == expected, "Extraction response: expected " + expected + ", received " + (int)result.StatusCode);
        if (ack is not null)
        {
            var value = await result.Content.ReadFromJsonAsync<ExtractionCompletionAck>(ExtractionCompletionProtocol.JsonOptions);
            Check(value?.Status == ack && value.EventId == proof.Event.EventId, "Wrong event acknowledgement.");
        }
    }
}
finally
{
    if (api is not null) { await api.StopAsync(); await api.DisposeAsync(); }
    if (admin is not null)
    {
        if (!admin.HasExited) admin.Kill(true);
        await admin.WaitForExitAsync();
        if (adminOutput is not null) File.WriteAllText(Path.Combine(temp, "admin.log"), await adminOutput);
        if (adminErrors is not null) File.WriteAllText(Path.Combine(temp, "admin-errors.log"), await adminErrors);
        admin.Dispose();
    }
    foreach (var table in new[] { "suite_extraction_notification_outbox", "suite_content_grants", "suite_content_entitlements", "suite_sessions",
        "suite_license_enrollments", "suite_devices", "suite_license_deliveries", "suite_licenses" })
        await Sql($"DELETE FROM suite.{table} WHERE license_id=ANY($1)", (object)fixtures.Select(x => x.License).ToArray());
    // Keep the synthetic catalog in the disposable CI database; immutable published snapshots are never edited/deleted.
    Console.WriteLine("Fixture customer rows removed from the named isolated database; no production data used.");
}

Fixture Identity(RSA key, string suffix)
{
    var bytes = key.ExportSubjectPublicKeyInfo();
    return new("TS-EXTRACTION-CI-" + suffix + "-" + Guid.NewGuid().ToString("N"),
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Convert.ToBase64String(bytes), Hex(), key);
}
ExtractionCompletionProof Proof(Fixture who, int item, long? completed = null)
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var value = new ExtractionCompletionEvent(1, "TURBORAMA_SUITE", "", who.License, who.Device,
        Item(item), Item(item), 1, catalog, new string('c', 64), "emulators", completed ?? now - 1);
    value = value with { EventId = ExtractionCompletionProtocol.EventId(value) };
    return new(value, who.Session, now, Convert.ToBase64String(who.Key.SignData(
        ExtractionCompletionProtocol.SigningBytes(value, who.Session, now), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
}
async Task Seed()
{
    await Sql("""
        INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,inventory_sha256,visual_catalog_sha256,
          item_count,ready_item_count,maintenance_item_count,status)
        SELECT $1,COALESCE(max(catalog_sequence),0)+1,$1,$1,902,902,0,'STAGING' FROM suite.suite_content_snapshots
        """, catalog);
    for (var i = 0; i < 902; i++)
        await Sql("""
            INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,display_name,visual_extract_policy,status,
              artifact_id,artifact_version,safe_file_name,file_extension,extract_policy,manifest_identity,descriptor_hash,content_type)
            VALUES($1,$2,$3,'Synthetic extraction fixture','EXTRACT_ARCHIVE','READY',$2,1,'fixture.zip','.zip','EXTRACT_ARCHIVE',$1,$1,'application/zip')
            """, catalog, Item(i), i);
    await Sql("UPDATE suite.suite_content_snapshots SET status='PUBLISHED',published_at=clock_timestamp() WHERE catalog_identity=$1", catalog);
    foreach (var who in fixtures)
    {
        await Sql("""
            INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,license_term,identity_policy,maximum_active_devices,enrollment_state,provisioning_origin)
            VALUES($1,'TURBORAMA_SUITE','ACTIVE',true,'LIFETIME','SOFTWARE_ONLY',1,'BOUND','COMMERCE');
            INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint)
            VALUES($1,$2,'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',$3,$4);
            INSERT INTO suite.suite_devices(license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm)
            VALUES($1,$2,'SOFTWARE_BOUND_ONLINE',$3,$4,'ACTIVE','rsa-pss-sha256');
            INSERT INTO suite.suite_sessions(license_id,device_id,session_id,status,authorized_until,last_server_time)
            VALUES($1,$2,$5,'ACTIVE',clock_timestamp()+interval '1 hour',extract(epoch from clock_timestamp())::bigint);
            INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,source_product_sku,product_id,license_id,provisioning_state,financial_state,last_source_version)
            VALUES('TURBOBOX_V1',$1,'suite','SUITE_LIFETIME_1_DEVICE','TURBORAMA_SUITE',$1,'PROVISIONED','PAID',1);
            INSERT INTO suite.suite_content_entitlements(license_id,scope,status,source_system,source_purchase_id,source_item_key,product_id)
            VALUES($1,'FULL_CATALOG','ACTIVE','TURBOBOX_V1',$1,'suite','TURBORAMA_SUITE')
            """, who.License, who.Device, who.PublicKey, Hex(), who.Session);
        for (var i = 0; i < 21; i++)
            await Sql("""
                INSERT INTO suite.suite_content_grants(grant_id,token_digest,license_id,device_id,session_id,revocation_generation,
                  authorized_until,catalog_identity,item_id,artifact_id,artifact_version,manifest_identity,descriptor_hash,state,
                  correlation_id,created_at,expires_at,claimed_at,last_authorized_at,completed_at)
                VALUES($1,$1,$2,$3,$4,0,clock_timestamp()+interval '55 minutes',$5,$6,$6,1,$5,$5,'COMPLETED',
                  'extraction-native-ci',clock_timestamp()-interval '20 seconds',clock_timestamp()+interval '20 seconds',
                  clock_timestamp()-interval '10 seconds',clock_timestamp()-interval '5 seconds',clock_timestamp()-interval '1 second')
                """, Hex(), who.License, who.Device, who.Session, catalog, Item(i));
    }
}
async Task Sql(string sql, params object[] values)
{
    // Named parameters let Npgsql parse the fixture's multi-statement batches.
    // PostgreSQL's native positional protocol accepts only one prepared statement.
    var namedSql = System.Text.RegularExpressions.Regex.Replace(sql, @"\$(\d+)", "@p$1");
    await using var command = db.CreateCommand(namedSql);
    for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("p" + (i + 1), values[i]);
    await command.ExecuteNonQueryAsync();
}
async Task<object?> Scalar(string sql, params object[] values)
{
    await using var command = db.CreateCommand(sql);
    foreach (var value in values) command.Parameters.AddWithValue(value);
    return await command.ExecuteScalarAsync();
}
async Task<long> Count() => (long)(await Scalar("SELECT count(*) FROM suite.suite_extraction_notification_outbox") ?? -1L);
static string Hex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
static string Item(int index) => index.ToString("x32");
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed record Fixture(string License, string Device, string PublicKey, string Session, RSA Key);
