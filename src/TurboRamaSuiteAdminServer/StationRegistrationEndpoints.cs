using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

internal static class StationRegistrationEndpoints
{
    private const string Source = "STATION_ADMIN_V1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app, bool enabled)
    {
        app.MapPost("/station/registrations", async (StationRegistrationRequest request,
            HttpContext context, NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            var security = ContentAdminSecurity.Authorize(context, "station.licenses.manage", true, false);
            if (!security.Allowed) return Failure(403, "STATION_PERMISSION_DENIED");
            if (request is null || !Hex(request.RequestId) ||
                !System.Text.RegularExpressions.Regex.IsMatch(request.CustomerRef ?? "", @"^TBX-USER-[1-9][0-9]{0,11}$") ||
                !Text(request.DisplayName, 1, 256) || !Text(request.Reason, 10, 200) ||
                request.GrantKind is not ("paid" or "courtesy" or "test"))
                return Failure(400, "STATION_REGISTRATION_INVALID");
            try
            {
                return await Create(database, request, security.Actor!, token);
            }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
            { return Failure(409, "STATION_REGISTRATION_CONFLICT"); }
            catch (Exception) { return Failure(503, "STATION_ADMIN_UNAVAILABLE"); }
        });
    }

    private static async Task<IResult> Create(NpgsqlDataSource database,
        StationRegistrationRequest request, string actor, CancellationToken token)
    {
        var digest = Sha(JsonSerializer.Serialize(new { actor, request }, Json));
        var purchase = "STMAN-" + Sha(actor + "|" + request.RequestId)[..32];
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        // The customer lock also serializes two different requests for the same customer.
        await using (var gate = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended('station-register:'||$1,0))", connection, transaction))
        { gate.Parameters.AddWithValue(request.CustomerRef); await gate.ExecuteNonQueryAsync(token); }
        await using (var receipt = new NpgsqlCommand("""
            SELECT payload_digest,result_json::text FROM suite.suite_commerce_inbox
            WHERE source_system='STATION_ADMIN_V1' AND source_event_id=$1 FOR UPDATE
            """, connection, transaction))
        {
            receipt.Parameters.AddWithValue(request.RequestId);
            await using var row = await receipt.ExecuteReaderAsync(token);
            if (await row.ReadAsync(token))
            {
                if (row.GetString(0) != digest) return Failure(409, "STATION_REGISTRATION_CONFLICT");
                var saved = JsonSerializer.Deserialize<StationRegistrationResult>(row.GetString(1), Json)
                    ?? throw new InvalidOperationException();
                await row.DisposeAsync();
                await transaction.CommitAsync(token);
                return Results.Json(saved with { Replayed = true });
            }
        }
        if (!request.AllowAdditional)
        {
            await using var current = new NpgsqlCommand("""
                SELECT l.license_id FROM suite.station_customer_projection p
                JOIN suite.suite_licenses l USING(license_id)
                WHERE p.customer_ref=$1 AND l.product_id='TURBORAMA_STATION_ANDROID'
                  AND l.status<>'REVOKED' ORDER BY l.updated_at DESC LIMIT 1
                """, connection, transaction);
            current.Parameters.AddWithValue(request.CustomerRef);
            if (await current.ExecuteScalarAsync(token) is string existing)
                return Results.Json(new { code = "STATION_CUSTOMER_ALREADY_LICENSED", licenseId = existing }, statusCode: 409);
        }
        var license = "STA-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var amount = request.GrantKind == "paid" ? 9990 : 0;
        await using (var create = new NpgsqlCommand("""
            INSERT INTO suite.suite_licenses(license_id,product_id,status,
              activation_verifier,activation_expires_at,activation_consumed,license_term,
              expires_at,identity_policy,maximum_active_devices,provisioning_origin,enrollment_state,claim_mode)
            VALUES($1,'TURBORAMA_STATION_ANDROID','ACTIVE',NULL,NULL,false,
              'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')
            """, connection, transaction))
        { create.Parameters.AddWithValue(license); await create.ExecuteNonQueryAsync(token); }
        await using (var delivery = new NpgsqlCommand("""
            INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,
              source_item_key,source_product_sku,product_id,license_id,provisioning_state,
              financial_state,last_source_version)
            VALUES('STATION_ADMIN_V1',$1,'station','STATION_ANDROID_LIFETIME_1_DEVICE',
              'TURBORAMA_STATION_ANDROID',$2,'PROVISIONED','PAID',1)
            """, connection, transaction))
        {
            delivery.Parameters.AddWithValue(purchase); delivery.Parameters.AddWithValue(license);
            await delivery.ExecuteNonQueryAsync(token);
        }
        await using (var profile = new NpgsqlCommand("""
            INSERT INTO suite.station_customer_projection(license_id,source_system,
              source_purchase_id,source_item_key,customer_ref,display_name)
            VALUES($1,'STATION_ADMIN_V1',$2,'station',$3,$4)
            """, connection, transaction))
        {
            profile.Parameters.AddWithValue(license); profile.Parameters.AddWithValue(purchase);
            profile.Parameters.AddWithValue(request.CustomerRef); profile.Parameters.AddWithValue(request.DisplayName);
            await profile.ExecuteNonQueryAsync(token);
        }
        var result = new StationRegistrationResult(license, request.CustomerRef, request.DisplayName,
            request.GrantKind, amount, purchase, false);
        // Existing inbox is a bounded private receipt. It never stores an activation code.
        // PAID settles a zero-price courtesy/test; it does not report a provider payment.
        var savedResult = JsonSerializer.Serialize(new { result.LicenseId, result.CustomerRef,
            result.DisplayName, result.GrantKind, result.AmountCents, result.SourcePurchaseId,
            result.Replayed, actor, request.Reason }, Json);
        await using (var receipt = new NpgsqlCommand("""
            INSERT INTO suite.suite_commerce_inbox(source_system,source_event_id,source_purchase_id,
              source_item_key,source_version,source_product_sku,event_type,payload_digest,
              processed_at,outcome,detail_code,result_json)
            VALUES('STATION_ADMIN_V1',$1,$2,'station',1,'STATION_ANDROID_LIFETIME_1_DEVICE',
              'PURCHASE_PAID',$3,clock_timestamp(),'PROVISIONED',$4,$5::jsonb)
            """, connection, transaction))
        {
            receipt.Parameters.AddWithValue(request.RequestId); receipt.Parameters.AddWithValue(purchase);
            receipt.Parameters.AddWithValue(digest); receipt.Parameters.AddWithValue("ADMIN_" + request.GrantKind.ToUpperInvariant());
            receipt.Parameters.AddWithValue(savedResult); await receipt.ExecuteNonQueryAsync(token);
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,outcome,
              detail_code,admin_actor,request_id)
            VALUES('STATION_ADMIN_LICENSE_CREATED',$1,$2,'SUCCESS',$3,$4,$2)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(license); audit.Parameters.AddWithValue(request.RequestId);
            audit.Parameters.AddWithValue("ADMIN_" + request.GrantKind.ToUpperInvariant()); audit.Parameters.AddWithValue(actor);
            await audit.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
        return Results.Json(result);
    }

    private static bool Hex(string? value) => value?.Length == 32 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Text(string? value, int minimum, int maximum) =>
        value is not null && value.Trim().Length >= minimum && value.Length <= maximum && !value.Any(char.IsControl);
    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static IResult Failure(int status, string code) => Results.Json(new { code }, statusCode: status);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StationRegistrationRequest(string RequestId, string CustomerRef,
    string DisplayName, string GrantKind, string Reason, bool AllowAdditional = false);
internal sealed record StationRegistrationResult(string LicenseId, string CustomerRef,
    string DisplayName, string GrantKind, int AmountCents, string SourcePurchaseId, bool Replayed);
