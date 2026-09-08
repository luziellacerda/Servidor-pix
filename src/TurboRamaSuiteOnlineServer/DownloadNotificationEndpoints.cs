using System.Text.Json;
using Npgsql;
using TurboRamaSuiteNotifications;

namespace TurboRamaSuiteOnlineServer;

public static class DownloadNotificationEndpoints
{
    public static void Map(WebApplication app, bool enabled)
    {
        // Separate limiter: optional notifications cannot consume login buckets.
        var limiter = new SuiteRateLimiter(TimeProvider.System);
        app.MapPost(DownloadCompletionProtocol.Route, async (HttpContext http) =>
        {
            if (!enabled) return Results.NotFound();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                if (http.Request.ContentLength > DownloadCompletionProtocol.MaximumBodyBytes)
                    return Results.StatusCode(413);
                // Also bound chunked requests where Content-Length is absent.
                var body = new byte[DownloadCompletionProtocol.MaximumBodyBytes + 1];
                var length = 0;
                while (length < body.Length)
                {
                    var read = await http.Request.Body.ReadAsync(body.AsMemory(length), timeout.Token);
                    if (read == 0) break;
                    length += read;
                }
                if (length > DownloadCompletionProtocol.MaximumBodyBytes) return Results.StatusCode(413);
                var proof = ExtractionCompletionProtocol.Parse<DownloadCompletionProof>(body.AsSpan(0, length));
                DownloadCompletionProtocol.ValidateEvent(proof.Event);
                if (!ExtractionCompletionProtocol.IsHex(proof.SessionId, 64)) return Results.BadRequest();
                // Adapt identity only for the existing bounded limiter; this does
                // not request a challenge or alter the licensing protocol.
                var identity = new ChallengeRequest(1, Protocol.ProductId, proof.Event.LicenseId,
                    proof.Event.DeviceId, proof.SessionId, ContentProtocol.DownloadAuthorizeAction,
                    proof.Event.EventId);
                if (!limiter.Allow(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    DownloadCompletionProtocol.Route, identity)) return Results.StatusCode(429);
                var store = new DownloadNotificationStore(http.RequestServices.GetRequiredService<NpgsqlDataSource>());
                var result = await store.AcceptAsync(proof, timeout.Token);
                return Results.Json(result, DownloadCompletionProtocol.JsonOptions,
                    statusCode: result.Status == "ACCEPTED" ? 202 : 200);
            }
            catch (SuiteException ex)
            { return Results.Json(new ErrorResponse(1, ex.Code, ex.Message), StrictJson.Options, statusCode: ex.StatusCode); }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            { return Results.Json(new ErrorResponse(1,"NOTICE_INVALID","Notification is invalid."),StrictJson.Options,statusCode:400); }
            catch (OperationCanceledException)
            { return Results.Json(new ErrorResponse(1,"NOTICE_TIMEOUT","Notification is deferred."),StrictJson.Options,statusCode:503); }
            catch (Exception)
            {
                // No exception body, signature, license, network or recipient data.
                app.Logger.LogWarning("Download notification could not be accepted.");
                return Results.Json(new ErrorResponse(1,"NOTICE_UNAVAILABLE","Notification is deferred."),StrictJson.Options,statusCode:503);
            }
        });
    }
}

public sealed class DownloadNotificationStore(NpgsqlDataSource db)
{
    private const string ContextFilter = """
        license_id=$1 AND device_id=$2::bpchar AND session_id=$3::bpchar
        AND item_id=$4::bpchar AND artifact_id=$5::bpchar AND artifact_version=$6
        AND manifest_identity=$7::bpchar AND (sha256 IS NULL OR sha256=$8::bpchar)
        """;

    public async Task<DownloadCompletionAck> AcceptAsync(DownloadCompletionProof proof, CancellationToken ct)
    {
        // Since migration 015, direct-mode grants deliberately have NULL hashes.
        // Bind to the authorized artifact; the signed archive hash is a CLIENT
        // completion claim, not a digest independently verified by this server.
        var value = proof.Event;
        await using var connection = await db.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var context = new NpgsqlCommand(
            "SELECT public_key_spki FROM suite.suite_extraction_notice_context WHERE " + ContextFilter + " LIMIT 2",connection,tx);
        AddContext(context, proof);
        string publicKey;
        await using (var reader = await context.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw Denied();
            publicKey = reader.GetString(0);
            if (await reader.ReadAsync(ct)) throw Denied();
        }
        if (!DownloadCompletionProtocol.Verify(proof, publicKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            throw new SuiteException(403,"NOTICE_PROOF_INVALID","Notification proof is invalid.");
        await using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))",connection,tx))
        {
            gate.Parameters.AddWithValue("suite:extraction-notice:" + value.LicenseId);
            await gate.ExecuteNonQueryAsync(ct);
        }
        await using (var prior = new NpgsqlCommand("SELECT event_id FROM suite.suite_extraction_notification_outbox WHERE license_id=$1 AND device_id=$2::bpchar AND download_id=$3::bpchar",connection,tx))
        {
            prior.Parameters.AddWithValue(value.LicenseId); prior.Parameters.AddWithValue(value.DeviceId);
            prior.Parameters.AddWithValue(value.DownloadId);
            if (await prior.ExecuteScalarAsync(ct) is string acceptedId)
            {
                if (acceptedId != value.EventId)
                    throw new SuiteException(409,"NOTICE_OPERATION_CONFLICT","This download operation already has a different completion.");
                await tx.CommitAsync(ct);
                return new(1,value.EventId,"ALREADY_ACCEPTED");
            }
        }
        await using (var quota = new NpgsqlCommand("SELECT count(*) FROM suite.suite_extraction_notification_outbox WHERE license_id=$1 AND created_at>clock_timestamp()-interval '1 hour'",connection,tx))
        {
            quota.Parameters.AddWithValue(value.LicenseId);
            if ((long)(await quota.ExecuteScalarAsync(ct) ?? 0L) >= 60)
                throw new SuiteException(429,"NOTICE_QUOTA","Notification quota exceeded.");
        }
        await using var insert = new NpgsqlCommand("""
            INSERT INTO suite.suite_extraction_notification_outbox
              (event_id,license_id,device_id,item_id,source_purchase_id,content_name,
               category_id,completed_at,template_variant,download_id,completion_kind)
            SELECT $9,license_id,device_id,item_id,source_purchase_id,display_name,$10,to_timestamp($11),$12,$14,$15
            FROM suite.suite_extraction_notice_context WHERE
            """ + "\n" + ContextFilter + " AND public_key_spki=$13 ON CONFLICT(event_id) DO NOTHING",connection,tx);
        AddContext(insert,proof);
        insert.Parameters.AddWithValue(value.EventId); insert.Parameters.AddWithValue(value.CategoryId);
        insert.Parameters.AddWithValue(value.CompletedAtUnixSeconds);
        insert.Parameters.AddWithValue(ExtractionCompletionMessage.SelectVariant()); insert.Parameters.AddWithValue(publicKey);
        insert.Parameters.AddWithValue(value.DownloadId); insert.Parameters.AddWithValue(value.CompletionKind);
        if (await insert.ExecuteNonQueryAsync(ct) != 1) throw Denied();
        await tx.CommitAsync(ct);
        return new(1,value.EventId,"ACCEPTED");
    }

    private static void AddContext(NpgsqlCommand command, DownloadCompletionProof proof)
    {
        var value=proof.Event;
        command.Parameters.AddWithValue(value.LicenseId); command.Parameters.AddWithValue(value.DeviceId);
        command.Parameters.AddWithValue(proof.SessionId); command.Parameters.AddWithValue(value.ItemId);
        command.Parameters.AddWithValue(value.ArtifactId); command.Parameters.AddWithValue(value.ArtifactVersion);
        command.Parameters.AddWithValue(value.ManifestIdentity); command.Parameters.AddWithValue(value.FileSha256);
    }
    private static SuiteException Denied() => new(409,"NOTICE_TARGET_UNAVAILABLE","Notification target is unavailable.");
}
