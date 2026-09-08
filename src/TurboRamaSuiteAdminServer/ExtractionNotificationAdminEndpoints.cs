using System.Text.RegularExpressions;
using Npgsql;
using TurboRamaSuiteNotifications;

// Runs behind the existing authenticated Unix-socket admin middleware. No new
// public admin endpoint and no credentials are delivered to the Windows client.
internal static class ExtractionNotificationAdminEndpoints
{
    internal static void Map(WebApplication app)
    {
        if (Environment.GetEnvironmentVariable("SUITE_EXTRACTION_NOTICES_ENABLED") != "1") return;
        app.MapPost("/extraction-notifications/lease", async (NpgsqlDataSource db, CancellationToken ct) =>
        {
            await using var connection = await db.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            // A crash after dispatch may have queued a message. NEVER recycle
            // that job automatically without provider/queue reconciliation.
            await using (var stale = new NpgsqlCommand("""
                UPDATE suite.suite_extraction_notification_outbox
                SET status=CASE WHEN status='DISPATCHING' THEN 'UNCERTAIN' ELSE 'DEAD' END,
                    last_error_code=CASE WHEN status='DISPATCHING' THEN 'DISPATCH_OUTCOME_UNKNOWN' ELSE 'ATTEMPTS_EXHAUSTED' END,
                    finished_at=clock_timestamp()
                WHERE (status='DISPATCHING' AND lease_until<clock_timestamp())
                   OR (status IN('PENDING','LEASED') AND attempts>=8 AND (lease_until IS NULL OR lease_until<clock_timestamp()))
                """, connection, tx)) await stale.ExecuteNonQueryAsync(ct);
            await using var command = new NpgsqlCommand("""
                WITH candidate AS (
                  SELECT event_id FROM suite.suite_extraction_notification_outbox
                  WHERE (status='PENDING' OR status='LEASED' AND lease_until<clock_timestamp())
                    AND attempts<8 AND next_attempt_at<=clock_timestamp()
                  ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED)
                UPDATE suite.suite_extraction_notification_outbox o
                SET status='LEASED',lease_token=$1,lease_until=clock_timestamp()+interval '60 seconds',attempts=attempts+1
                FROM candidate c WHERE o.event_id=c.event_id
                RETURNING o.event_id,o.lease_token,o.source_purchase_id
                """, connection, tx);
            command.Parameters.AddWithValue(Guid.NewGuid());
            ExtractionNoticeLease? result = null;
            await using (var reader = await command.ExecuteReaderAsync(ct))
                if (await reader.ReadAsync(ct)) result = new(reader.GetString(0),reader.GetGuid(1),reader.GetString(2));
            await tx.CommitAsync(ct);
            return result is null ? Results.NoContent() : Results.Json(result);
        });

        app.MapPost("/extraction-notifications/begin-dispatch", async (
            ExtractionNoticeDispatch request, NpgsqlDataSource db, CancellationToken ct) =>
        {
            if (!Valid(request.EventId,request.LeaseToken) || request.CustomerName is not { Length: >= 1 and <= 256 })
                return Results.BadRequest();
            await using var connection = await db.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            await using var read = new NpgsqlCommand("""
                SELECT o.content_name,o.category_id,o.completed_at,o.template_variant
                FROM suite.suite_extraction_notification_outbox o
                WHERE o.event_id=$1 AND o.lease_token=$2 AND o.status='LEASED' AND o.lease_until>clock_timestamp()
                  AND EXISTS(SELECT 1 FROM suite.suite_licenses l JOIN suite.suite_devices d USING(license_id)
                    JOIN suite.suite_license_deliveries p USING(license_id)
                    WHERE l.license_id=o.license_id AND l.status='ACTIVE' AND d.device_id=o.device_id AND d.status='ACTIVE'
                      AND p.source_system='TURBOBOX_V1' AND p.source_purchase_id=o.source_purchase_id
                      AND p.financial_state='PAID' AND p.provisioning_state='PROVISIONED')
                FOR UPDATE OF o
                """,connection,tx);
            read.Parameters.AddWithValue(request.EventId); read.Parameters.AddWithValue(request.LeaseToken);
            string message;
            await using (var reader=await read.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct)) return Results.Conflict();
                var data=new ExtractionCompletionMessageData(request.CustomerName,reader.GetString(0),
                    ExtractionCompletionProtocol.CategoryName(reader.GetString(1)),
                    new DateTimeOffset(reader.GetDateTime(2)),"TS-"+request.EventId[..12].ToUpperInvariant());
                try { message=ExtractionCompletionMessage.Format(data,DateTimeOffset.UtcNow,reader.GetInt16(3)); }
                catch (ArgumentException) { return Results.BadRequest(); }
            }
            await using var dispatch=new NpgsqlCommand("""
                UPDATE suite.suite_extraction_notification_outbox SET status='DISPATCHING',
                  lease_until=clock_timestamp()+interval '2 minutes'
                WHERE event_id=$1 AND lease_token=$2 AND status='LEASED'
                """,connection,tx);
            dispatch.Parameters.AddWithValue(request.EventId);dispatch.Parameters.AddWithValue(request.LeaseToken);
            if(await dispatch.ExecuteNonQueryAsync(ct)!=1)return Results.Conflict();
            await tx.CommitAsync(ct);
            return Results.Json(new { request.EventId, Message=message });
        });

        app.MapPost("/extraction-notifications/complete", async (
            ExtractionNoticeResult request,NpgsqlDataSource db,CancellationToken ct) =>
        {
            if (!Valid(request.EventId,request.LeaseToken)
                || request.Outcome is not ("QUEUED" or "SKIPPED" or "RETRY" or "UNCERTAIN" or "DEAD")
                || request.ErrorCode is null || !Regex.IsMatch(request.ErrorCode,"^[A-Z0-9_]{0,64}$"))
                return Results.BadRequest();
            var status=request.Outcome=="RETRY"?"PENDING":request.Outcome;
            var allowed=request.Outcome is "QUEUED" or "UNCERTAIN"
                ? new[]{"DISPATCHING","UNCERTAIN",status} : new[]{"LEASED",status};
            await using var command=db.CreateCommand("""
                UPDATE suite.suite_extraction_notification_outbox
                SET status=$3::varchar,lease_until=NULL,last_error_code=NULLIF($4::text,''),
                  next_attempt_at=CASE WHEN $3::varchar='PENDING' THEN clock_timestamp()+interval '2 minutes' ELSE next_attempt_at END,
                  finished_at=CASE WHEN $3::varchar='PENDING' THEN NULL ELSE clock_timestamp() END
                WHERE event_id=$1 AND lease_token=$2 AND status=ANY($5::varchar[])
                """);
            command.Parameters.AddWithValue(request.EventId);command.Parameters.AddWithValue(request.LeaseToken);
            command.Parameters.AddWithValue(status);command.Parameters.AddWithValue(request.ErrorCode);
            command.Parameters.AddWithValue(allowed);
            return await command.ExecuteNonQueryAsync(ct)==1 ? Results.Json(new{status="RECORDED"}) : Results.Conflict();
        });
    }
    private static bool Valid(string? eventId,Guid token)=>ExtractionCompletionProtocol.IsHex(eventId,64)&&token!=Guid.Empty;
}
internal sealed record ExtractionNoticeLease(string EventId,Guid LeaseToken,string SourcePurchaseId);
internal sealed record ExtractionNoticeDispatch(string EventId,Guid LeaseToken,string CustomerName);
internal sealed record ExtractionNoticeResult(string EventId,Guid LeaseToken,string Outcome,string ErrorCode);
