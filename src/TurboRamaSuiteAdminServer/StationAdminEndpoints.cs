using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

internal static class StationAdminEndpoints
{
    private const string Product = "TURBORAMA_STATION_ANDROID";
    private const string ReadClaim = "station.licenses.read";
    private const string ManageClaim = "station.licenses.manage";
    private const string RevokeClaim = "station.sessions.revoke";
    private static readonly ContentAdminRateLimiter Limiter = new();

    public static void Map(WebApplication app, bool enabled, string pepperFile)
    {
        StationManagementEndpoints.Map(app, enabled);
        app.MapGet("/station/licenses/{licenseId}", async (string licenseId,
            HttpContext context, NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            var security = ContentAdminSecurity.Authorize(context, ReadClaim, false, false);
            if (!security.Allowed) return Failure(403, "STATION_PERMISSION_DENIED");
            if (!ValidId(licenseId)) return Failure(400, "STATION_LICENSE_INVALID");
            try
            {
                await using var command = database.CreateCommand("""
                    SELECT l.license_id,l.status,l.revocation_generation,
                           l.enrollment_state,d.provisioning_state,d.financial_state,
                           d.source_purchase_id,p.display_name,
                           coalesce(v.device_id,''),coalesce(v.manufacturer,''),
                           coalesce(v.model,''),coalesce(v.android_sdk,0),
                           coalesce(v.client_version,''),
                           coalesce(s.session_id,''),
                           coalesce(extract(epoch from s.authorized_until)::bigint,0),
                           coalesce(extract(epoch from s.last_contact_at)::bigint,0),
                           (SELECT count(*) FROM suite.station_devices active
                             WHERE active.license_id=l.license_id
                               AND active.status='ACTIVE')
                    FROM suite.suite_licenses l
                    JOIN suite.suite_license_deliveries d ON d.license_id=l.license_id
                      AND d.product_id='TURBORAMA_STATION_ANDROID'
                    LEFT JOIN suite.station_customer_projection p
                      ON p.license_id=l.license_id
                    LEFT JOIN LATERAL (
                      SELECT * FROM suite.station_devices
                      WHERE license_id=l.license_id AND status='ACTIVE'
                      ORDER BY updated_at DESC LIMIT 1) v ON true
                    LEFT JOIN LATERAL (
                      SELECT * FROM suite.station_sessions
                      WHERE license_id=l.license_id AND status='ACTIVE'
                      ORDER BY created_at DESC LIMIT 1) s ON true
                    WHERE l.license_id=$1 AND l.product_id='TURBORAMA_STATION_ANDROID'
                    """);
                command.Parameters.AddWithValue(licenseId);
                await using var row = await command.ExecuteReaderAsync(token);
                if (!await row.ReadAsync(token)) return Failure(404, "STATION_NOT_FOUND");
                return Results.Json(new StationAdminStatus(row.GetString(0),
                    row.GetString(1), row.GetInt64(2), row.GetString(3),
                    row.GetString(4), row.GetString(5), row.GetString(6),
                    row.IsDBNull(7) ? "" : row.GetString(7), row.GetString(8),
                    row.GetString(9), row.GetString(10), row.GetInt32(11),
                    row.GetString(12), row.GetString(13), row.GetInt64(14),
                    row.GetInt64(15), row.GetInt64(16)));
            }
            catch (Exception) { return Failure(503, "STATION_ADMIN_UNAVAILABLE"); }
        });
        app.MapGet("/station/licenses/{licenseId}/devices", async (string licenseId,
            HttpContext context, NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            var security = ContentAdminSecurity.Authorize(context, ReadClaim, false, false);
            if (!security.Allowed) return Failure(403, "STATION_PERMISSION_DENIED");
            if (!ValidId(licenseId) ||
                !PageNumber(context.Request.Query["offset"].ToString(), 0, 1000000,
                    out var offset) ||
                !PageNumber(context.Request.Query["limit"].ToString(), 50, 100,
                    out var limit) || limit < 1)
                return Failure(400, "STATION_REQUEST_INVALID");
            try
            {
                await using var exists = database.CreateCommand("""
                    SELECT 1 FROM suite.suite_licenses
                    WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
                    """);
                exists.Parameters.AddWithValue(licenseId);
                if (await exists.ExecuteScalarAsync(token) is null)
                    return Failure(404, "STATION_NOT_FOUND");
                await using var command = database.CreateCommand("""
                    SELECT d.device_id,d.status,coalesce(d.manufacturer,''),
                           coalesce(d.model,''),coalesce(d.android_sdk,0),
                           coalesce(d.client_version,''),d.created_at,d.updated_at
                    FROM suite.station_devices d
                    WHERE d.license_id=$1
                    ORDER BY d.created_at DESC,d.device_id DESC
                    LIMIT $2 OFFSET $3
                    """);
                command.Parameters.AddWithValue(licenseId);
                command.Parameters.AddWithValue(limit);
                command.Parameters.AddWithValue(offset);
                var devices = new List<StationAdminDevice>();
                await using var row = await command.ExecuteReaderAsync(token);
                while (await row.ReadAsync(token))
                    devices.Add(new(row.GetString(0), row.GetString(1),
                        row.GetString(2), row.GetString(3), row.GetInt32(4),
                        row.GetString(5), row.GetDateTime(6), row.GetDateTime(7)));
                return Results.Json(new { licenseId, offset, limit, devices });
            }
            catch (Exception) { return Failure(503, "STATION_ADMIN_UNAVAILABLE"); }
        });
        app.MapPost("/station/licenses/{licenseId}/actions/issue-code",
            async (string licenseId, StationIssueRequest request,
                HttpContext context, NpgsqlDataSource database,
                CancellationToken token) =>
            {
                if (!enabled) return Results.NotFound();
                var security = ContentAdminSecurity.Authorize(context, ManageClaim, true,
                    false);
                if (!security.Allowed) return Failure(403, "STATION_PERMISSION_DENIED");
                if (request is null || !ValidId(licenseId) ||
                    request.Actor != security.Actor ||
                    !ValidRequestId(request.RequestId))
                    return Failure(400, "STATION_REQUEST_INVALID");
                if (!Limiter.TryAcquire(security.Actor, licenseId, "issue", 10))
                    return Failure(429, "STATION_RATE_LIMITED");
                try { return Results.Json(await StationCommerceEndpoints.IssueForAdminAsync(
                    licenseId, request, database, pepperFile, token)); }
                catch (StationCommerceFailure failure)
                { return Failure(failure.Status, failure.Code); }
                catch (Exception) { return Failure(503, "STATION_ADMIN_UNAVAILABLE"); }
            });
        foreach (var action in new[] { "block", "unblock", "transfer", "cancel-code",
            "revoke-session" })
            MapAction(app, enabled, action);
    }

    private static void MapAction(WebApplication app, bool enabled, string action)
    {
        app.MapPost("/station/licenses/{licenseId}/actions/" + action,
            async (string licenseId, StationAdminActionRequest request,
                HttpContext context, NpgsqlDataSource database,
                CancellationToken token) =>
            {
                if (!enabled) return Results.NotFound();
                var claim = action == "revoke-session" ? RevokeClaim : ManageClaim;
                var security = ContentAdminSecurity.Authorize(context, claim, true, false);
                if (!security.Allowed) return Failure(403, "STATION_PERMISSION_DENIED");
                if (request is null || !ValidId(licenseId) ||
                    !ValidRequestId(request.RequestId) ||
                    request.ExpectedGeneration < 0 || request.Reason is null ||
                    request.ExpectedActivationGeneration < 0 ||
                    request.Reason.Length is < 10 or > 256 ||
                    request.Reason.Any(char.IsControl) ||
                    action == "revoke-session" &&
                    (request.TargetSessionId is null ||
                     !ValidHex(request.TargetSessionId, 64)))
                    return Failure(400, "STATION_REQUEST_INVALID");
                if (!Limiter.TryAcquire(security.Actor, licenseId, action, 20))
                    return Failure(429, "STATION_RATE_LIMITED");
                try { return Results.Json(await ApplyAction(action, licenseId, request,
                    security.Actor, database, token)); }
                catch (StationCommerceFailure failure)
                { return Failure(failure.Status, failure.Code); }
                catch (PostgresException exception) when (exception.SqlState is
                    PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                    or PostgresErrorCodes.UniqueViolation)
                { return Failure(409, "STATION_STATE_CHANGED"); }
                catch (Exception) { return Failure(503, "STATION_ADMIN_UNAVAILABLE"); }
            });
    }

    private static async Task<StationActionResult> ApplyAction(string action,
        string licenseId, StationAdminActionRequest request, string actor,
        NpgsqlDataSource database, CancellationToken token)
    {
        var digestInput=string.Join('\n', action, licenseId,
                request.ExpectedGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.Reason, request.TargetSessionId ?? "", actor);
        if(request.ExpectedActivationGeneration is not null)
            digestInput+="\n"+request.ExpectedActivationGeneration.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestInput))).ToLowerInvariant();
        await using var connection = await database.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, token);
        long generation;long activationGeneration;
        string status;
        await using (var license = new NpgsqlCommand("""
            SELECT revocation_generation,status,activation_generation FROM suite.suite_licenses
            WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
            FOR UPDATE
            """, connection, transaction))
        {
            license.Parameters.AddWithValue(licenseId);
            await using var row = await license.ExecuteReaderAsync(token);
            if (!await row.ReadAsync(token))
                throw new StationCommerceFailure(404, "STATION_NOT_FOUND");
            generation = row.GetInt64(0);
            status = row.GetString(1);
            activationGeneration=row.GetInt64(2);
        }
        await using (var prior = new NpgsqlCommand("""
            SELECT request_digest,result_json::text FROM suite.suite_lifecycle_commands
            WHERE scope='STATION_ANDROID' AND request_id=$1
            """, connection, transaction))
        {
            prior.Parameters.AddWithValue(request.RequestId);
            await using var row = await prior.ExecuteReaderAsync(token);
            if (await row.ReadAsync(token))
            {
                if (row.GetString(0) != digest)
                    throw new StationCommerceFailure(409, "STATION_REQUEST_CONFLICT");
                var priorResult = JsonSerializer.Deserialize<StationActionResult>(row.GetString(1)) ??
                    throw new StationCommerceFailure(409, "STATION_REQUEST_CONFLICT");
                await row.DisposeAsync();
                await transaction.CommitAsync(token);
                return priorResult;
            }
        }
        if (generation != request.ExpectedGeneration || request.ExpectedActivationGeneration is not null &&
            activationGeneration != request.ExpectedActivationGeneration)
            throw new StationCommerceFailure(409, "STATION_STATE_CHANGED");
        if (action == "revoke-session")
        {
            await using var revoke = new NpgsqlCommand("""
                UPDATE suite.station_sessions SET status='REVOKED'
                WHERE license_id=$1 AND session_id=$2 AND status='ACTIVE'
                """, connection, transaction);
            revoke.Parameters.AddWithValue(licenseId);
            revoke.Parameters.AddWithValue(request.TargetSessionId!);
            if (await revoke.ExecuteNonQueryAsync(token) != 1)
                throw new StationCommerceFailure(409, "STATION_SESSION_CHANGED");
        }
        else
        {
            if (action is "unblock" or "transfer")
            {
                await using var paid = new NpgsqlCommand("""
                    SELECT 1 FROM suite.suite_license_deliveries
                    WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
                      AND source_product_sku='STATION_ANDROID_LIFETIME_1_DEVICE'
                      AND financial_state='PAID' AND provisioning_state='PROVISIONED'
                    """, connection, transaction);
                paid.Parameters.AddWithValue(licenseId);
                if (status != (action == "unblock" ? "SUSPENDED" : "ACTIVE") ||
                    await paid.ExecuteScalarAsync(token) is null)
                    throw new StationCommerceFailure(409, "STATION_FINANCIAL_BLOCK");
            }
            else if (status != "ACTIVE")
                throw new StationCommerceFailure(409, "STATION_STATE_CHANGED");
            if (action == "cancel-code")
            {
                await using var pending = new NpgsqlCommand("""
                    SELECT 1 FROM suite.suite_licenses WHERE license_id=$1
                      AND enrollment_state='PENDING_ENROLLMENT'
                      AND NOT activation_consumed AND activation_verifier IS NOT NULL
                    """, connection, transaction);
                pending.Parameters.AddWithValue(licenseId);
                if (await pending.ExecuteScalarAsync(token) is null)
                    throw new StationCommerceFailure(409, "STATION_STATE_CHANGED");
            }
            await using (var update = new NpgsqlCommand("""
                UPDATE suite.suite_licenses SET
                  status=$2,revocation_generation=revocation_generation+1,
                  activation_generation=activation_generation+$3,
                  enrollment_state=CASE WHEN $4 THEN 'PENDING_ENROLLMENT'
                    ELSE enrollment_state END,
                  activation_consumed=CASE WHEN $4 THEN false ELSE activation_consumed END,
                  activation_verifier=CASE WHEN $4 THEN NULL ELSE activation_verifier END,
                  activation_expires_at=CASE WHEN $4 THEN NULL ELSE activation_expires_at END,
                  updated_at=clock_timestamp()
                WHERE license_id=$1
                """, connection, transaction))
            {
                update.Parameters.AddWithValue(licenseId);
                update.Parameters.AddWithValue(action == "block" ? "SUSPENDED" : "ACTIVE");
                update.Parameters.AddWithValue(action is "transfer" or "cancel-code" ? 1 : 0);
                update.Parameters.AddWithValue(action is "transfer" or "cancel-code");
                await update.ExecuteNonQueryAsync(token);
            }
            await using (var sessions = new NpgsqlCommand("""
                UPDATE suite.station_sessions SET status='REVOKED'
                WHERE license_id=$1 AND status='ACTIVE'
                """, connection, transaction))
            {
                sessions.Parameters.AddWithValue(licenseId);
                await sessions.ExecuteNonQueryAsync(token);
            }
            await using (var challenges = new NpgsqlCommand("""
                UPDATE suite.station_challenges SET consumed_at=clock_timestamp()
                WHERE license_id=$1 AND consumed_at IS NULL
                """, connection, transaction))
            {
                challenges.Parameters.AddWithValue(licenseId);
                await challenges.ExecuteNonQueryAsync(token);
            }
            if (action == "transfer")
            {
                await using var device = new NpgsqlCommand("""
                    UPDATE suite.station_devices SET status='REVOKED',
                      updated_at=clock_timestamp()
                    WHERE license_id=$1 AND status='ACTIVE'
                    """, connection, transaction);
                device.Parameters.AddWithValue(licenseId);
                await device.ExecuteNonQueryAsync(token);
            }
            generation++;
        }
        var result = new StationActionResult(action.ToUpperInvariant(),
            licenseId, generation);
        await using (var receipt = new NpgsqlCommand("""
            INSERT INTO suite.suite_lifecycle_commands(scope,request_id,request_digest,
              license_id,action,expected_generation,resulting_generation,
              actor,reason,outcome,result_json)
            VALUES('STATION_ANDROID',$1,$2,$3,$4,$5,$6,$7,$8,'SUCCESS',$9::jsonb)
            """, connection, transaction))
        {
            receipt.Parameters.AddWithValue(request.RequestId);
            receipt.Parameters.AddWithValue(digest);
            receipt.Parameters.AddWithValue(licenseId);
            receipt.Parameters.AddWithValue(action switch
            {
                "block" => "SUSPEND", "unblock" => "RESUME",
                "transfer" => "TRANSFER", _ => "FORCE_REAUTH"
            });
            receipt.Parameters.AddWithValue(request.ExpectedGeneration);
            receipt.Parameters.AddWithValue(generation);
            receipt.Parameters.AddWithValue(actor);
            receipt.Parameters.AddWithValue(request.Reason);
            receipt.Parameters.AddWithValue(JsonSerializer.Serialize(result));
            await receipt.ExecuteNonQueryAsync(token);
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO suite.suite_audit_events(event_type,license_id,
              correlation_id,outcome,detail_code,admin_actor,request_id)
            VALUES($1,$2,$3,'SUCCESS',$4,$5,$3)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue("STATION_" + action.ToUpperInvariant()
                .Replace('-', '_'));
            audit.Parameters.AddWithValue(licenseId);
            audit.Parameters.AddWithValue(request.RequestId);
            audit.Parameters.AddWithValue(action.ToUpperInvariant());
            audit.Parameters.AddWithValue(actor);
            await audit.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
        return result;
    }

    private static bool ValidId(string? value) => value?.Length is >= 6 and <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool ValidHex(string? value, int length) => value?.Length == length &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidRequestId(string? value) => value?.Length is >= 16 and <= 64 &&
        value.All(char.IsAsciiLetterOrDigit);
    private static bool PageNumber(string value, int fallback, int maximum,
        out int result)
    {
        if (value.Length == 0) { result = fallback; return true; }
        return int.TryParse(value, out result) && result >= 0 && result <= maximum;
    }
    private static IResult Failure(int status, string code) =>
        Results.Json(new { code }, statusCode: status);
}

internal sealed record StationAdminStatus(string LicenseId,string Status,
    long RevocationGeneration,string EnrollmentState,string ProvisioningState,
    string FinancialState,string PurchaseId,string DisplayName,string DeviceId,
    string Manufacturer,string Model,int AndroidSdk,string ClientVersion,
    string SessionId,long AuthorizedUntilUnixSeconds,long LastContactAtUnixSeconds,
    long ActiveDeviceCount);
internal sealed record StationAdminDevice(string DeviceId,string Status,
    string Manufacturer,string Model,int AndroidSdk,string ClientVersion,
    DateTime CreatedAt,DateTime UpdatedAt);
internal sealed record StationAdminActionRequest(string RequestId,
    long ExpectedGeneration,string Reason,string? TargetSessionId,long? ExpectedActivationGeneration=null);
internal sealed record StationActionResult(string Code,string LicenseId,
    long RevocationGeneration);
