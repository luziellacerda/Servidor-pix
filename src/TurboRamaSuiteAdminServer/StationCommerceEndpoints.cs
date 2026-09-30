using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

internal static class StationCommerceEndpoints
{
    private const string Product = "TURBORAMA_STATION_ANDROID";
    private const string Sku = "STATION_ANDROID_LIFETIME_1_DEVICE";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict
    };

    public static void Map(WebApplication app, bool enabled, string pepperFile)
    {
        app.MapPost("/commerce/station/events", async (HttpContext context,
            NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            try
            {
                var request = await Read<StationCommerceEvent>(context, token);
                return Results.Json(await ApplyAsync(request, database, token));
            }
            catch (StationCommerceFailure failure)
            { return Results.Json(new { code = failure.Code }, statusCode: failure.Status); }
        });
        app.MapPost("/commerce/station/deliveries/{purchase}/{item}/issue",
            async (string purchase, string item, HttpContext context,
                NpgsqlDataSource database, CancellationToken token) =>
            {
                if (!enabled) return Results.NotFound();
                try { return Results.Json(await IssueAsync(purchase, item,
                        await Read<StationIssueRequest>(context, token), database,
                        pepperFile, token)); }
                catch (StationCommerceFailure failure)
                { return Results.Json(new { code = failure.Code }, statusCode: failure.Status); }
            });
        app.MapPost("/commerce/station/profiles", async (HttpContext context,
            NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            try
            {
                var request = await Read<StationProfileSyncRequest>(context, token);
                return Results.Json(await SyncProfileAsync(request, database, token));
            }
            catch (StationCommerceFailure failure)
            { return Results.Json(new { code = failure.Code }, statusCode: failure.Status); }
        });
    }

    private static async Task<StationCommerceResult> ApplyAsync(
        StationCommerceEvent request, NpgsqlDataSource database,
        CancellationToken token)
    {
        Validate(request);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', request.SourceSystem, request.SourceEventId,
                request.SourcePurchaseId, request.SourceItemKey,
                request.SourceVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.SourceProductSku, request.EventType,
                request.AmountCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.Currency, request.CustomerRef, request.DisplayName)))).ToLowerInvariant();
        if (!Fixed(digest, request.PayloadDigest))
            throw new StationCommerceFailure(409, "STATION_EVENT_DIGEST_MISMATCH");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await ApplyOnceAsync(request, database, token); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                && attempt < 3)
            { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt, token); }
            catch (PostgresException exception) when (exception.SqlState ==
                PostgresErrorCodes.UniqueViolation && attempt < 3)
            { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt, token); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                or PostgresErrorCodes.UniqueViolation)
            { throw new StationCommerceFailure(409, "STATION_TRANSACTION_CONFLICT"); }
        }
        throw new StationCommerceFailure(409, "STATION_TRANSACTION_CONFLICT");
    }

    private static async Task<StationCommerceResult> ApplyOnceAsync(
        StationCommerceEvent request, NpgsqlDataSource database,
        CancellationToken token)
    {
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, token);
        await using (var prior = new NpgsqlCommand("""
            SELECT payload_digest,result_json::text FROM suite.suite_commerce_inbox
            WHERE source_system=$1 AND source_event_id=$2 FOR UPDATE
            """, connection, transaction))
        {
            prior.Parameters.AddWithValue(request.SourceSystem);
            prior.Parameters.AddWithValue(request.SourceEventId);
            await using var row = await prior.ExecuteReaderAsync(token);
            if (await row.ReadAsync(token))
            {
                if (!Fixed(row.GetString(0), request.PayloadDigest))
                    throw new StationCommerceFailure(409, "STATION_EVENT_CONFLICT");
                var priorResult = JsonSerializer.Deserialize<StationCommerceResult>(
                    row.GetString(1), JsonOptions) ??
                    throw new StationCommerceFailure(409, "STATION_EVENT_CONFLICT");
                await row.DisposeAsync();
                await transaction.CommitAsync(token);
                return priorResult;
            }
        }

        StationCommerceResult result;
        await using (var delivery = new NpgsqlCommand("""
            SELECT coalesce(license_id,''),provisioning_state,financial_state,
                   last_source_version FROM suite.suite_license_deliveries
            WHERE source_system=$1 AND source_purchase_id=$2 AND source_item_key=$3
              AND product_id='TURBORAMA_STATION_ANDROID' FOR UPDATE
            """, connection, transaction))
        {
            delivery.Parameters.AddWithValue(request.SourceSystem);
            delivery.Parameters.AddWithValue(request.SourcePurchaseId);
            delivery.Parameters.AddWithValue(request.SourceItemKey);
            await using var row = await delivery.ExecuteReaderAsync(token);
            if (await row.ReadAsync(token))
            {
                var license = row.GetString(0);
                var state = row.GetString(1);
                var financial = row.GetString(2);
                var version = row.GetInt64(3);
                result = new(request.SourcePurchaseId, request.SourceItemKey,
                    license.Length == 0 ? null : license, state, financial, version,
                    "STALE");
            }
            else result = new(request.SourcePurchaseId, request.SourceItemKey,
                null, "UNKNOWN", "UNKNOWN", 0, "NEW");
        }

        if (request.SourceVersion <= result.SourceVersion && result.Outcome != "NEW")
        {
            // An older paid notification never revives a newer suspension.
        }
        else if (request.EventType == "PURCHASE_SUSPENDED")
            result = await SuspendAsync(request, result, connection, transaction, token);
        else if (result.Outcome == "NEW" && request.SourceVersion == 1)
            result = await ProvisionAsync(request, connection, transaction, token);
        else
            result = result with { Outcome = "GAP_OR_RESUME_REQUIRES_REVIEW" };

        await using (var inbox = new NpgsqlCommand("""
            INSERT INTO suite.suite_commerce_inbox(source_system,source_event_id,
              source_purchase_id,source_item_key,source_version,source_product_sku,
              event_type,payload_digest,processed_at,outcome,detail_code,result_json)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,clock_timestamp(),$9,$9,$10::jsonb)
            """, connection, transaction))
        {
            inbox.Parameters.AddWithValue(request.SourceSystem);
            inbox.Parameters.AddWithValue(request.SourceEventId);
            inbox.Parameters.AddWithValue(request.SourcePurchaseId);
            inbox.Parameters.AddWithValue(request.SourceItemKey);
            inbox.Parameters.AddWithValue(request.SourceVersion);
            inbox.Parameters.AddWithValue(request.SourceProductSku);
            inbox.Parameters.AddWithValue(request.EventType);
            inbox.Parameters.AddWithValue(request.PayloadDigest);
            inbox.Parameters.AddWithValue(result.Outcome);
            inbox.Parameters.AddWithValue(JsonSerializer.Serialize(result, JsonOptions));
            try { await inbox.ExecuteNonQueryAsync(token); }
            catch (PostgresException exception) when (exception.SqlState ==
                PostgresErrorCodes.UniqueViolation)
            { throw new StationCommerceFailure(409, "STATION_EVENT_CONFLICT"); }
        }
        await transaction.CommitAsync(token);
        return result;
    }

    private static async Task<StationCommerceResult> ProvisionAsync(
        StationCommerceEvent request, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken token)
    {
        var licenseId = "STA-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await using (var license = new NpgsqlCommand("""
            INSERT INTO suite.suite_licenses(license_id,product_id,status,
              activation_verifier,activation_expires_at,activation_consumed,
              license_term,expires_at,identity_policy,maximum_active_devices,
              provisioning_origin,enrollment_state,claim_mode)
            VALUES($1,'TURBORAMA_STATION_ANDROID','ACTIVE',NULL,NULL,false,
              'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE',
              'PENDING_ENROLLMENT','FIRST_CLAIM')
            """, connection, transaction))
        {
            license.Parameters.AddWithValue(licenseId);
            await license.ExecuteNonQueryAsync(token);
        }
        await using (var delivery = new NpgsqlCommand("""
            INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,
              source_item_key,source_product_sku,product_id,license_id,
              provisioning_state,financial_state,last_source_version)
            VALUES($1,$2,$3,'STATION_ANDROID_LIFETIME_1_DEVICE',
              'TURBORAMA_STATION_ANDROID',$4,'PROVISIONED','PAID',$5)
            """, connection, transaction))
        {
            delivery.Parameters.AddWithValue(request.SourceSystem);
            delivery.Parameters.AddWithValue(request.SourcePurchaseId);
            delivery.Parameters.AddWithValue(request.SourceItemKey);
            delivery.Parameters.AddWithValue(licenseId);
            delivery.Parameters.AddWithValue(request.SourceVersion);
            await delivery.ExecuteNonQueryAsync(token);
        }
        await using (var profile = new NpgsqlCommand("""
            INSERT INTO suite.station_customer_projection(license_id,source_system,
              source_purchase_id,source_item_key,customer_ref,display_name)
            VALUES($1,$2,$3,$4,$5,$6)
            """, connection, transaction))
        {
            profile.Parameters.AddWithValue(licenseId);
            profile.Parameters.AddWithValue(request.SourceSystem);
            profile.Parameters.AddWithValue(request.SourcePurchaseId);
            profile.Parameters.AddWithValue(request.SourceItemKey);
            profile.Parameters.AddWithValue(request.CustomerRef);
            profile.Parameters.AddWithValue(request.DisplayName);
            await profile.ExecuteNonQueryAsync(token);
        }
        return new(request.SourcePurchaseId, request.SourceItemKey, licenseId,
            "PROVISIONED", "PAID", request.SourceVersion, "PROVISIONED");
    }

    private static async Task<StationCommerceResult> SuspendAsync(
        StationCommerceEvent request, StationCommerceResult current,
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken token)
    {
        if (current.LicenseId is not null)
        {
            await using (var license = new NpgsqlCommand("""
                UPDATE suite.suite_licenses SET status=CASE WHEN status='REVOKED'
                  THEN status ELSE 'SUSPENDED' END,
                  revocation_generation=revocation_generation+1,
                  activation_generation=activation_generation+1,
                  activation_verifier=NULL,activation_expires_at=NULL,
                  updated_at=clock_timestamp()
                WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
                """, connection, transaction))
            {
                license.Parameters.AddWithValue(current.LicenseId);
                await license.ExecuteNonQueryAsync(token);
            }
            await using (var sessions = new NpgsqlCommand("""
                UPDATE suite.station_sessions SET status='REVOKED'
                WHERE license_id=$1 AND status='ACTIVE'
                """, connection, transaction))
            {
                sessions.Parameters.AddWithValue(current.LicenseId);
                await sessions.ExecuteNonQueryAsync(token);
            }
            await using (var challenges = new NpgsqlCommand("""
                UPDATE suite.station_challenges SET consumed_at=clock_timestamp()
                WHERE license_id=$1 AND consumed_at IS NULL
                """, connection, transaction))
            {
                challenges.Parameters.AddWithValue(current.LicenseId);
                await challenges.ExecuteNonQueryAsync(token);
            }
            await using (var devices = new NpgsqlCommand("""
                UPDATE suite.station_devices SET status='REVOKED',
                  updated_at=clock_timestamp()
                WHERE license_id=$1 AND status='ACTIVE'
                """, connection, transaction))
            {
                devices.Parameters.AddWithValue(current.LicenseId);
                await devices.ExecuteNonQueryAsync(token);
            }
            await using (var delivery = new NpgsqlCommand("""
                UPDATE suite.suite_license_deliveries
                SET provisioning_state='SUSPENDED',financial_state='SUSPENDED',
                  last_source_version=$2,updated_at=clock_timestamp()
                WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
                """, connection, transaction))
            {
                delivery.Parameters.AddWithValue(current.LicenseId);
                delivery.Parameters.AddWithValue(request.SourceVersion);
                await delivery.ExecuteNonQueryAsync(token);
            }
            return current with { ProvisioningState = "SUSPENDED",
                FinancialState = "SUSPENDED", SourceVersion = request.SourceVersion,
                Outcome = "SUSPENDED" };
        }
        if (current.Outcome == "NEW")
        {
            await using var tombstone = new NpgsqlCommand("""
                INSERT INTO suite.suite_license_deliveries(source_system,
                  source_purchase_id,source_item_key,source_product_sku,
                  product_id,license_id,provisioning_state,financial_state,
                  last_source_version)
                VALUES($1,$2,$3,'STATION_ANDROID_LIFETIME_1_DEVICE',
                  'TURBORAMA_STATION_ANDROID',NULL,'TOMBSTONE','SUSPENDED',$4)
                """, connection, transaction);
            tombstone.Parameters.AddWithValue(request.SourceSystem);
            tombstone.Parameters.AddWithValue(request.SourcePurchaseId);
            tombstone.Parameters.AddWithValue(request.SourceItemKey);
            tombstone.Parameters.AddWithValue(request.SourceVersion);
            await tombstone.ExecuteNonQueryAsync(token);
            return current with { ProvisioningState = "TOMBSTONE",
                FinancialState = "SUSPENDED", SourceVersion = request.SourceVersion,
                Outcome = "TOMBSTONE_APPLIED" };
        }
        return current with { Outcome = "SUSPENDED_NO_LICENSE" };
    }

    private static async Task<StationIssueResult> IssueAsync(string purchase,
        string item, StationIssueRequest request, NpgsqlDataSource database,
        string pepperFile, CancellationToken token)
    {
        Text(purchase, 64); Text(item, 32); Text(request.Actor, 128);
        Text(request.RequestId, 128);
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, token);
        string licenseId;
        await using (var find = new NpgsqlCommand("""
            SELECT l.license_id FROM suite.suite_license_deliveries d
            JOIN suite.suite_licenses l ON l.license_id=d.license_id
            WHERE d.source_system='TURBOBOX_V1' AND d.source_purchase_id=$1
              AND d.source_item_key=$2 AND d.product_id='TURBORAMA_STATION_ANDROID'
              AND d.provisioning_state='PROVISIONED' AND d.financial_state='PAID'
              AND l.status='ACTIVE' AND l.enrollment_state='PENDING_ENROLLMENT'
              AND NOT l.activation_consumed
            FOR UPDATE OF l
            """, connection, transaction))
        {
            find.Parameters.AddWithValue(purchase);
            find.Parameters.AddWithValue(item);
            licenseId = (string?)await find.ExecuteScalarAsync(token) ??
                throw new StationCommerceFailure(409, "STATION_DELIVERY_NOT_ELIGIBLE");
        }
        await using (var existing = new NpgsqlCommand("""
            SELECT 1 FROM suite.suite_licenses
            WHERE license_id=$1 AND activation_verifier IS NOT NULL
              AND activation_expires_at>clock_timestamp()
            """, connection, transaction))
        {
            existing.Parameters.AddWithValue(licenseId);
            if (await existing.ExecuteScalarAsync(token) is not null)
                throw new StationCommerceFailure(409, "STATION_CODE_ALREADY_ACTIVE");
        }
        var codeBytes = RandomNumberGenerator.GetBytes(32);
        var code = Convert.ToBase64String(codeBytes).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
        var pepperBytes = Convert.FromBase64String((await File.ReadAllTextAsync(
            pepperFile, token)).Trim());
        if (pepperBytes.Length < 32)
            throw new InvalidOperationException("Station pepper is invalid.");
        var verifier = Convert.ToHexString(HMACSHA256.HashData(pepperBytes,
            Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(codeBytes);
        CryptographicOperations.ZeroMemory(pepperBytes);
        DateTime expires;
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_licenses SET activation_verifier=$2,
              activation_expires_at=clock_timestamp()+interval '15 minutes',
              activation_consumed=false,activation_generation=activation_generation+1,
              updated_at=clock_timestamp()
            WHERE license_id=$1 RETURNING activation_expires_at
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(licenseId);
            update.Parameters.AddWithValue(verifier);
            expires = (DateTime)(await update.ExecuteScalarAsync(token) ??
                throw new StationCommerceFailure(409, "STATION_DELIVERY_NOT_ELIGIBLE"));
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO suite.suite_audit_events(event_type,license_id,
              correlation_id,outcome,detail_code,admin_actor,request_id)
            VALUES('STATION_CODE_ISSUED',$1,$2,'SUCCESS','FIRST_CLAIM',$3,$2)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(licenseId);
            audit.Parameters.AddWithValue(request.RequestId);
            audit.Parameters.AddWithValue(request.Actor);
            await audit.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
        return new(licenseId, code, expires);
    }

    internal static async Task<StationIssueResult> IssueForAdminAsync(string licenseId,
        StationIssueRequest request, NpgsqlDataSource database, string pepperFile,
        CancellationToken token)
    {
        Text(licenseId, 64);
        await using var find = database.CreateCommand("""
            SELECT source_purchase_id,source_item_key
            FROM suite.suite_license_deliveries
            WHERE source_system='TURBOBOX_V1' AND license_id=$1
              AND product_id='TURBORAMA_STATION_ANDROID'
            """);
        find.Parameters.AddWithValue(licenseId);
        await using var row = await find.ExecuteReaderAsync(token);
        if (!await row.ReadAsync(token))
            throw new StationCommerceFailure(409, "STATION_DELIVERY_NOT_ELIGIBLE");
        var purchase = row.GetString(0);
        var item = row.GetString(1);
        await row.DisposeAsync();
        return await IssueAsync(purchase, item, request, database, pepperFile, token);
    }

    private static async Task<StationProfileSyncResult> SyncProfileAsync(
        StationProfileSyncRequest request, NpgsqlDataSource database,
        CancellationToken token)
    {
        if (request.SourceSystem != "TURBOBOX_V1" || request.ProfileVersion < 1 ||
            request.DisplayName.Length > 256 || request.DisplayName.Any(char.IsControl))
            throw new StationCommerceFailure(400, "STATION_PROFILE_INVALID");
        Text(request.SourcePurchaseId, 64);
        Text(request.SourceItemKey, 32);
        Text(request.CustomerRef, 128);
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        string licenseId;
        long currentVersion;
        string currentName;
        await using (var current = new NpgsqlCommand("""
            SELECT p.license_id,p.profile_version,p.display_name
            FROM suite.station_customer_projection p
            JOIN suite.suite_license_deliveries d ON d.license_id=p.license_id
            WHERE p.source_system=$1 AND p.source_purchase_id=$2
              AND p.source_item_key=$3 AND p.customer_ref=$4
              AND d.product_id='TURBORAMA_STATION_ANDROID'
              AND d.source_system=p.source_system
              AND d.source_purchase_id=p.source_purchase_id
              AND d.source_item_key=p.source_item_key
            FOR UPDATE OF p
            """, connection, transaction))
        {
            current.Parameters.AddWithValue(request.SourceSystem);
            current.Parameters.AddWithValue(request.SourcePurchaseId);
            current.Parameters.AddWithValue(request.SourceItemKey);
            current.Parameters.AddWithValue(request.CustomerRef);
            await using var row = await current.ExecuteReaderAsync(token);
            if (!await row.ReadAsync(token))
                throw new StationCommerceFailure(404, "STATION_PROFILE_NOT_FOUND");
            licenseId = row.GetString(0);
            currentVersion = row.GetInt64(1);
            currentName = row.GetString(2);
        }
        if (request.ProfileVersion == currentVersion &&
            request.DisplayName != currentName)
            throw new StationCommerceFailure(409, "STATION_PROFILE_CONFLICT");
        if (request.ProfileVersion > currentVersion)
        {
            await using var update = new NpgsqlCommand("""
                UPDATE suite.station_customer_projection
                SET display_name=$2,profile_version=$3,updated_at=clock_timestamp()
                WHERE license_id=$1
                """, connection, transaction);
            update.Parameters.AddWithValue(licenseId);
            update.Parameters.AddWithValue(request.DisplayName);
            update.Parameters.AddWithValue(request.ProfileVersion);
            await update.ExecuteNonQueryAsync(token);
            currentVersion = request.ProfileVersion;
        }
        await transaction.CommitAsync(token);
        return new(licenseId, currentVersion);
    }

    private static async Task<T> Read<T>(HttpContext context,
        CancellationToken token) where T : class
    {
        if (context.Request.ContentLength is > 8192)
            throw new StationCommerceFailure(413, "STATION_EVENT_TOO_LARGE");
        using var body = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(chunk, token);
            if (read == 0) break;
            if (body.Length + read > 8192)
                throw new StationCommerceFailure(413, "STATION_EVENT_TOO_LARGE");
            body.Write(chunk, 0, read);
        }
        if (body.Length is 0 or > 8192)
            throw new StationCommerceFailure(413, "STATION_EVENT_TOO_LARGE");
        try
        {
            using var json = JsonDocument.Parse(body.ToArray());
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in json.RootElement.EnumerateObject())
                if (!names.Add(property.Name))
                    throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
            var result = JsonSerializer.Deserialize<T>(body.ToArray(), JsonOptions) ??
                throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
            if (typeof(T).GetProperties().Any(property =>
                    property.GetValue(result) is null))
                throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
            return result;
        }
        catch (JsonException)
        { throw new StationCommerceFailure(400, "STATION_EVENT_INVALID"); }
    }

    private static void Validate(StationCommerceEvent value)
    {
        if (value.SourceSystem != "TURBOBOX_V1" || value.SourceProductSku != Sku ||
            value.EventType is not ("PURCHASE_PAID" or "PURCHASE_SUSPENDED") ||
            value.SourceVersion < 1 || value.AmountCents != 9990 ||
            value.Currency != "BRL")
            throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
        Hex(value.SourceEventId, 32); Hex(value.PayloadDigest, 64);
        Text(value.SourcePurchaseId, 64); Text(value.SourceItemKey, 32);
        Text(value.CustomerRef, 128);
        if (value.DisplayName.Length > 256 || value.DisplayName.Any(char.IsControl))
            throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
    }

    private static void Hex(string value, int length)
    {
        if (value.Length != length || value.Any(c => c is not
            (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
    }

    private static void Text(string value, int maximum)
    {
        if (value.Length is < 1 || value.Length > maximum ||
            value.Any(c => char.IsControl(c) || c is '/' or '\\' or ':' or '|'))
            throw new StationCommerceFailure(400, "STATION_EVENT_INVALID");
    }

    private static bool Fixed(string left, string right)
    {
        var a = Encoding.ASCII.GetBytes(left);
        var b = Encoding.ASCII.GetBytes(right);
        try { return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record StationCommerceEvent(string SourceSystem,string SourceEventId,
    string SourcePurchaseId,string SourceItemKey,long SourceVersion,
    string SourceProductSku,string EventType,long AmountCents,string Currency,
    string CustomerRef,string DisplayName,string PayloadDigest);
internal sealed record StationCommerceResult(string SourcePurchaseId,
    string SourceItemKey,string? LicenseId,string ProvisioningState,
    string FinancialState,long SourceVersion,string Outcome);
internal sealed record StationIssueRequest(string Actor,string RequestId);
internal sealed record StationIssueResult(string LicenseId,string ActivationCode,
    DateTime ExpiresAt);
internal sealed record StationProfileSyncRequest(string SourceSystem,
    string SourcePurchaseId,string SourceItemKey,string CustomerRef,
    long ProfileVersion,string DisplayName);
internal sealed record StationProfileSyncResult(string LicenseId,long ProfileVersion);
internal sealed class StationCommerceFailure(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
