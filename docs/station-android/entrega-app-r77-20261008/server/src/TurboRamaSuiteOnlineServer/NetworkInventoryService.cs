using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TurboRamaSuite.Network;

namespace TurboRamaSuiteOnlineServer;

public sealed record NetworkInventoryOptions(int RetentionDays = 30);

public sealed class NetworkInventoryService(NpgsqlDataSource db, IAssertionSigner signer,
    TimeProvider clock, InventorySensitiveProtector protector, NetworkInventoryOptions options)
{
    private long Now => clock.GetUtcNow().ToUnixTimeSeconds();

    public async Task<SignedAssertionEnvelope> ChallengeAsync(NetworkChallengeRequest request, CancellationToken ct)
    {
        NetworkInventoryContract.Validate(request);
        var now = Now;
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var identity = await RequireActive(connection, tx, request.LicenseId, request.DeviceId,
            request.SessionId, request.AppScope, ct);
        await using var query = new NpgsqlCommand("""
            INSERT INTO suite.suite_network_challenges(challenge_id,license_id,device_id,session_id,
              app_scope,context_hash,nonce,revocation_generation,expires_at)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8,to_timestamp($9)
            WHERE (SELECT count(*) FROM suite.suite_network_challenges
              WHERE license_id=$2 AND device_id=$3 AND app_scope=$5 AND expires_at>clock_timestamp())<8
            """, connection, tx);
        Add(query,id,request.LicenseId,request.DeviceId,request.SessionId,request.AppScope,
            request.ContextHash,nonce,identity.Generation,now+60);
        if (await query.ExecuteNonQueryAsync(ct) != 1)
            throw new SuiteException(429,"RATE_LIMITED","Too many requests.");
        await tx.CommitAsync(ct);
        return signer.Sign(new NetworkAssertion(1,NetworkInventoryContract.ChallengeKind,
            NetworkInventoryContract.Product,request.LicenseId,request.DeviceId,request.SessionId,
            request.AppScope,NetworkInventoryContract.Action,request.ContextHash,id,nonce,"ISSUED",now,now+60));
    }

    public async Task<SignedAssertionEnvelope> AcceptAsync(NetworkInventoryProof proof, IPAddress ip, CancellationToken ct)
    {
        var now = Now;
        var context = proof.Context ?? throw new SecurityException("Network context is missing.");
        NetworkInventoryContract.Validate(context,now);
        if (!NetworkInventoryContract.Hex(proof.ChallengeId) || proof.Signature is not { Length: >= 300 and <= 1024 })
            throw new SecurityException("Network proof is invalid.");
        var hash = NetworkInventoryContract.Hash(context);
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var identity = await RequireActive(connection,tx,context.LicenseId,context.DeviceId,
            context.SessionId,context.AppScope,ct);
        if (!Protocol.FixedEquals(identity.Fingerprint,context.HardwareFingerprint))
            throw new SuiteException(403,"PROOF_INVALID","Machine proof is invalid.");
        string nonce; long expiry;
        await using (var challenge = new NpgsqlCommand("""
            SELECT nonce,extract(epoch from expires_at)::bigint FROM suite.suite_network_challenges
            WHERE challenge_id=$1 AND license_id=$2 AND device_id=$3 AND session_id=$4
              AND app_scope=$5 AND context_hash=$6 AND revocation_generation=$7
              AND consumed_at IS NULL AND expires_at>clock_timestamp() FOR UPDATE
            """,connection,tx))
        {
            Add(challenge,proof.ChallengeId,context.LicenseId,context.DeviceId,context.SessionId,
                context.AppScope,hash,identity.Generation);
            await using var row = await challenge.ExecuteReaderAsync(ct);
            if (!await row.ReadAsync(ct)) throw new SuiteException(409,"CHALLENGE_INVALID","Challenge is invalid or expired.");
            nonce=row.GetString(0); expiry=row.GetInt64(1);
        }
        var message = NetworkInventoryContract.SigningMessage(new(1,context.ProductId,context.LicenseId,
            context.DeviceId,context.SessionId,context.AppScope,context.Action,hash,proof.ChallengeId,nonce,expiry));
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(identity.Spki),out _);
            if (!rsa.VerifyData(message,Convert.FromBase64String(proof.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss))
                throw new SuiteException(403,"PROOF_INVALID","Machine proof is invalid.");
        }
        catch (FormatException) { throw new SuiteException(403,"PROOF_INVALID","Machine proof is invalid."); }
        finally { CryptographicOperations.ZeroMemory(message); }

        // Network data never participates in eligibility, fingerprints, revocation,
        // session lifetime or scores. Only authenticated reports reach this store.
        var raw = JsonSerializer.Serialize(new { context, observedIp=NetworkInventoryContract.NormalizeIp(ip) }, NetworkInventoryContract.Json);
        var encrypted = protector.Protect(raw);
        var masked = JsonSerializer.Serialize(context.Interfaces.Select(i => new NetworkInterfaceSignal(
            NetworkInventoryContract.MaskMac(i.Mac),i.InterfaceType,i.LocallyAdministered,i.Virtual)),NetworkInventoryContract.Json);
        try
        {
            await using (var store = new NpgsqlCommand("""
                INSERT INTO suite.suite_network_inventory(license_id,device_id,app_scope,session_id,
                  protected_payload,ip_masked,interfaces_masked,collected_at,expires_at)
                VALUES($1,$2,$3,$4,$5,$6,$7,to_timestamp($8),clock_timestamp()+make_interval(days=>$9))
                ON CONFLICT(license_id,device_id,app_scope) DO UPDATE SET
                  session_id=excluded.session_id,protected_payload=excluded.protected_payload,
                  ip_masked=excluded.ip_masked,interfaces_masked=excluded.interfaces_masked,
                  collected_at=excluded.collected_at,received_at=clock_timestamp(),expires_at=excluded.expires_at
                """,connection,tx))
            {
                Add(store,context.LicenseId,context.DeviceId,context.AppScope,context.SessionId,encrypted,
                    NetworkInventoryContract.MaskIp(ip));
                store.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Jsonb, Value=masked });
                Add(store,context.CollectedAtUnixSeconds,Math.Clamp(options.RetentionDays,1,365));
                await store.ExecuteNonQueryAsync(ct);
            }
            await using var consume = new NpgsqlCommand("UPDATE suite.suite_network_challenges SET consumed_at=clock_timestamp() WHERE challenge_id=$1",connection,tx);
            Add(consume,proof.ChallengeId);
            await consume.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
        return signer.Sign(new NetworkAssertion(1,NetworkInventoryContract.ResultKind,context.ProductId,
            context.LicenseId,context.DeviceId,context.SessionId,context.AppScope,context.Action,hash,
            proof.ChallengeId,"","ACCEPTED",now,now));
    }

    private static async Task<(string Spki,string Fingerprint,long Generation)> RequireActive(
        NpgsqlConnection connection,NpgsqlTransaction tx,string license,string device,string session,string scope,CancellationToken ct)
    {
        var table = scope switch { "SUITE" => "suite_sessions", "EMULATIONSTATION" => "suite_es_sessions",
            _ => throw new SecurityException("Application scope is invalid.") };
        // Same per-license lock order as licensing and administrative revocation.
        await using(var gate=new NpgsqlCommand("SELECT license_id FROM suite.suite_licenses WHERE license_id=$1 FOR UPDATE",connection,tx))
        { Add(gate,license); await gate.ExecuteScalarAsync(ct); }
        await using var query = new NpgsqlCommand($"""
            SELECT d.public_key_spki,d.hardware_fingerprint,l.revocation_generation
            FROM suite.suite_licenses l JOIN suite.suite_devices d ON d.license_id=l.license_id
            JOIN suite.suite_license_enrollments e ON e.license_id=l.license_id AND e.device_id=d.device_id
            JOIN suite.{table} s ON s.license_id=l.license_id AND s.device_id=d.device_id
            WHERE l.license_id=$1 AND d.device_id=$2 AND s.session_id=$3
              AND l.product_id='TURBORAMA_SUITE' AND l.status='ACTIVE' AND l.activation_consumed
              AND l.enrollment_state='BOUND' AND l.license_term='LIFETIME' AND l.expires_at IS NULL
              AND l.maximum_active_devices=1 AND d.status='ACTIVE' AND s.status='ACTIVE'
              AND s.revocation_generation=l.revocation_generation AND s.authorized_until>clock_timestamp()
              AND (l.provisioning_origin<>'COMMERCE' OR EXISTS(
                SELECT 1 FROM suite.suite_license_deliveries x WHERE x.license_id=l.license_id
                  AND x.provisioning_state='PROVISIONED' AND (x.financial_state='PAID' OR
                    (x.financial_state='SUSPENDED' AND x.administrative_resume_source_version=x.last_source_version))))
            FOR SHARE OF d,e,s
            """,connection,tx);
        Add(query,license,device,session);
        await using var row=await query.ExecuteReaderAsync(ct);
        if(!await row.ReadAsync(ct))throw new SuiteException(403,"SESSION_INVALID","Session is not authorized.");
        return(row.GetString(0),row.GetString(1),row.GetInt64(2));
    }

    public async Task PurgeExpiredAsync(CancellationToken ct)
    {
        foreach(var (table,key) in new[] {("suite_network_challenges","challenge_id"),("suite_network_inventory","ctid")})
        {
            await using var cmd=db.CreateCommand($"DELETE FROM suite.{table} WHERE {key} IN (SELECT {key} FROM suite.{table} WHERE expires_at<=clock_timestamp() ORDER BY expires_at LIMIT 500)");
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
    private static void Add(NpgsqlCommand command,params object[] values)
    { foreach(var value in values)command.Parameters.AddWithValue(value); }
}

public sealed class NetworkInventoryRetentionWorker(NetworkInventoryService service,ILogger<NetworkInventoryRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);timeout.CancelAfter(TimeSpan.FromSeconds(5));await service.PurgeExpiredAsync(timeout.Token); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception) { logger.LogWarning("Complementary network retention is temporarily unavailable."); }
        }
    }
}

public static class NetworkInventoryEndpoints
{
    public static void MapNetworkInventory(this WebApplication app,bool enabled)
    {
        Map<NetworkChallengeRequest>(app,enabled,NetworkInventoryContract.ChallengeRoute,(s,r,_,c)=>s.ChallengeAsync(r,c));
        Map<NetworkInventoryProof>(app,enabled,NetworkInventoryContract.InventoryRoute,
            (s,r,h,c)=>s.AcceptAsync(r,h.Connection.RemoteIpAddress??IPAddress.None,c));
    }
    private static void Map<T>(WebApplication app,bool enabled,string path,Func<NetworkInventoryService,T,HttpContext,CancellationToken,Task<SignedAssertionEnvelope>> action) where T:class
    {
        app.MapPost(path,async(HttpContext context)=>
        {
            if(!enabled)return Results.Json(new ErrorResponse(1,"NETWORK_INVENTORY_DISABLED","Complementary inventory is unavailable."),StrictJson.Options,statusCode:503);
            try
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using var memory=new MemoryStream();var buffer=new byte[1024];
                while(true){var read=await context.Request.Body.ReadAsync(buffer,timeout.Token);if(read==0)break;if(memory.Length+read>NetworkInventoryContract.MaximumBodyBytes)throw new SuiteException(413,"BODY_INVALID","Request body is invalid.");memory.Write(buffer,0,read);}
                var body=memory.ToArray();var request=StrictJson.Parse<T>(body);
                if(!body.AsSpan().SequenceEqual(NetworkInventoryContract.Canonical(request)))throw new SecurityException("Network JSON is not canonical.");
                var limiter=context.RequestServices.GetRequiredService<SuiteRateLimiter>();
                var scope=request switch {NetworkChallengeRequest n=>n.AppScope,NetworkInventoryProof n=>n.Context?.AppScope??"",_=>""};
                if(!limiter.Allow(context.Connection.RemoteIpAddress?.ToString()??"unknown",path+"/"+scope,request))
                    throw new SuiteException(429,"RATE_LIMITED","Too many requests.");
                return Results.Json(await action(context.RequestServices.GetRequiredService<NetworkInventoryService>(),request,context,timeout.Token),StrictJson.Options);
            }
            catch(SuiteException e){return Results.Json(new ErrorResponse(1,e.Code,e.Message),StrictJson.Options,statusCode:e.StatusCode);}
            catch(SecurityException){return Results.Json(new ErrorResponse(1,"NETWORK_CONTRACT_INVALID","Network report is invalid."),StrictJson.Options,statusCode:400);}
            catch(OperationCanceledException){return Results.Json(new ErrorResponse(1,"REQUEST_TIMEOUT","Request timed out."),StrictJson.Options,statusCode:504);}
            catch(Exception){app.Logger.LogWarning("Complementary network inventory request failed.");return Results.Json(new ErrorResponse(1,"NETWORK_UNAVAILABLE","Complementary inventory is unavailable."),StrictJson.Options,statusCode:503);}
        }).DisableAntiforgery();
    }
}
