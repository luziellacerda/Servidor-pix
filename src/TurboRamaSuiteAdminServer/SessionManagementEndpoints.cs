using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TurboRamaSuite.Management;

static class SessionManagementEndpoints
{
    private static readonly ContentAdminRateLimiter Limiter = new();

    public static void Map(WebApplication app)
    {
        app.MapPost("/sessions/query", async (SessionQuery request,HttpContext context,NpgsqlDataSource db,CancellationToken ct) =>
        {
            var security=ContentAdminSecurity.Authorize(context,SessionManagementPermissions.Read,false,false);
            if(!security.Allowed)return await Denied(security,db,ct);
            if(!Valid(request))return Failure(400,"QUERY_INVALID");
            if(!Limiter.TryAcquire(security.Actor,"sessions","query",120))return Failure(429,"RATE_LIMITED");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await using var cmd=db.CreateCommand("""
                    SELECT license_id,device_id,session_id,app_scope,state,
                      coalesce(extract(epoch from last_contact_at)::bigint,0),extract(epoch from authorized_until)::bigint
                    FROM (SELECT *,row_number() OVER(PARTITION BY license_id,app_scope
                        ORDER BY (state IN ('ONLINE','NO_RECENT_CONTACT')) DESC,
                        last_contact_at DESC NULLS LAST,authorized_until DESC,device_id) AS position
                      FROM suite.suite_application_sessions WHERE license_id=ANY($1)) latest
                    WHERE position=1
                    ORDER BY license_id,device_id,app_scope LIMIT 100
                    """);
                cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Array|NpgsqlDbType.Text,Value=request.LicenseIds });
                var rows=new List<ManagedSession>();await using var reader=await cmd.ExecuteReaderAsync(timeout.Token);
                while(await reader.ReadAsync(timeout.Token))rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetInt64(5),reader.GetInt64(6)));
                return Results.Json(new ManagedSessions(rows.ToArray()));
            }
            catch(Exception){return Failure(503,"SESSIONS_UNAVAILABLE");}
        });
        app.MapPost("/sessions/network",async(SessionQuery request,HttpContext context,NpgsqlDataSource db,CancellationToken ct)=>
        {
            var security=ContentAdminSecurity.Authorize(context,SessionManagementPermissions.NetworkRead,false,false);
            if(!security.Allowed)return await Denied(security,db,ct);
            if(!Valid(request))return Failure(400,"QUERY_INVALID");
            if(!Limiter.TryAcquire(security.Actor,"network","query",120))return Failure(429,"RATE_LIMITED");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await using var cmd=db.CreateCommand("""
                    SELECT license_id,device_id,app_scope,session_id,ip_masked,interfaces_masked::text,
                      extract(epoch from collected_at)::bigint,extract(epoch from received_at)::bigint
                    FROM (SELECT license_id,device_id,app_scope,session_id,ip_masked,interfaces_masked,collected_at,received_at,
                        row_number() OVER(PARTITION BY license_id,app_scope ORDER BY received_at DESC,device_id) AS position
                      FROM suite.suite_network_inventory WHERE license_id=ANY($1) AND expires_at>clock_timestamp()) latest
                    WHERE position=1
                    ORDER BY license_id,device_id,app_scope LIMIT 100
                    """);
                cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Array|NpgsqlDbType.Text,Value=request.LicenseIds });
                var rows=new List<ManagedNetworkReport>();await using var reader=await cmd.ExecuteReaderAsync(timeout.Token);
                while(await reader.ReadAsync(timeout.Token))rows.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),JsonSerializer.Deserialize<MaskedNetworkInterface[]>(reader.GetString(5),new JsonSerializerOptions(JsonSerializerDefaults.Web))??[],reader.GetInt64(6),reader.GetInt64(7)));
                return Results.Json(new ManagedNetworkReports(rows.ToArray()));
            }
            catch(Exception){return Failure(503,"NETWORK_UNAVAILABLE");}
        });
        app.MapPost("/sessions/revoke",async(RevokeEsSessionRequest request,HttpContext context,NpgsqlDataSource db,CancellationToken ct)=>
        {
            var security=ContentAdminSecurity.Authorize(context,SessionManagementPermissions.Revoke,true,false);
            if(!security.Allowed)return await Denied(security,db,ct);
            if(!Id(request.LicenseId)||!Hex(request.DeviceId)||!Hex(request.TargetSessionId)||
                request.ExpectedSessionId!=request.TargetSessionId||request.AppScope!="EMULATIONSTATION"||
                request.RequestId is not {Length:>=16 and <=64}||!request.RequestId.All(char.IsAsciiLetterOrDigit))
                return Failure(400,"TARGET_INVALID");
            if(!Limiter.TryAcquire(security.Actor,request.LicenseId,"revoke",20))return Failure(429,"RATE_LIMITED");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try{return await Revoke(request,security.Actor,db,timeout.Token);}
            catch(PostgresException e)when(e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation){return Failure(409,"SESSION_CHANGED");}
            catch(Exception){return Failure(503,"SESSIONS_UNAVAILABLE");}
        });
    }

    private static async Task<IResult> Revoke(RevokeEsSessionRequest r,string actor,NpgsqlDataSource db,CancellationToken ct)
    {
        await using var connection=await db.OpenConnectionAsync(ct);
        await using var tx=await connection.BeginTransactionAsync(ct);
        await using(var license=new NpgsqlCommand("SELECT license_id FROM suite.suite_licenses WHERE license_id=$1 AND product_id='TURBORAMA_SUITE' FOR UPDATE",connection,tx))
        { Add(license,r.LicenseId);if(await license.ExecuteScalarAsync(ct)is null)return Failure(404,"TARGET_NOT_FOUND"); }
        await using(var prior=new NpgsqlCommand("SELECT actor,license_id,device_id,target_session_id FROM suite.suite_es_session_revocations WHERE request_id=$1",connection,tx))
        {
            Add(prior,r.RequestId);await using var row=await prior.ExecuteReaderAsync(ct);
            if(await row.ReadAsync(ct))return row.GetString(0)==actor&&row.GetString(1)==r.LicenseId&&row.GetString(2)==r.DeviceId&&row.GetString(3)==r.TargetSessionId
                ? Results.Json(new RevokeEsSessionResult("ALREADY_REVOKED")):Failure(409,"REQUEST_CONFLICT");
        }
        await using(var target=new NpgsqlCommand("""
            SELECT session_id FROM suite.suite_es_sessions
            WHERE license_id=$1 AND device_id=$2 FOR UPDATE
            """,connection,tx))
        { Add(target,r.LicenseId,r.DeviceId);if((string?)await target.ExecuteScalarAsync(ct)!=r.ExpectedSessionId)return Failure(409,"SESSION_CHANGED"); }
        await using(var revoke=new NpgsqlCommand("UPDATE suite.suite_es_sessions SET status='REVOKED',updated_at=clock_timestamp() WHERE license_id=$1 AND device_id=$2 AND session_id=$3",connection,tx))
        { Add(revoke,r.LicenseId,r.DeviceId,r.TargetSessionId);if(await revoke.ExecuteNonQueryAsync(ct)!=1)return Failure(409,"SESSION_CHANGED"); }
        await using(var challenges=new NpgsqlCommand("UPDATE suite.suite_es_challenges SET consumed_at=clock_timestamp() WHERE license_id=$1 AND device_id=$2 AND session_id=$3 AND consumed_at IS NULL",connection,tx))
        { Add(challenges,r.LicenseId,r.DeviceId,r.TargetSessionId);await challenges.ExecuteNonQueryAsync(ct); }
        await using(var receipt=new NpgsqlCommand("INSERT INTO suite.suite_es_session_revocations(request_id,actor,license_id,device_id,target_session_id) VALUES($1,$2,$3,$4,$5)",connection,tx))
        { Add(receipt,r.RequestId,actor,r.LicenseId,r.DeviceId,r.TargetSessionId);await receipt.ExecuteNonQueryAsync(ct); }
        await using(var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id) VALUES('SUITE_ES_SESSION_REVOKED',$1,$2,$3,'SUCCESS','EXACT_ES_SESSION_REVOKED',$4,$3)",connection,tx))
        { Add(audit,r.LicenseId,r.DeviceId,r.RequestId,actor);await audit.ExecuteNonQueryAsync(ct); }
        await tx.CommitAsync(ct);
        return Results.Json(new RevokeEsSessionResult("REVOKED"));
    }
    private static bool Valid(SessionQuery query)=>query.LicenseIds is {Length:>=1 and <=50}&&query.LicenseIds.All(Id)&&query.LicenseIds.Distinct(StringComparer.Ordinal).Count()==query.LicenseIds.Length;
    private static bool Id(string? value)=>value is {Length:>=6 and <=64}&&value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static bool Hex(string? value)=>value is {Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static async Task<IResult> Denied(ContentSecurityResult security,NpgsqlDataSource db,CancellationToken ct)
    {
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var audit=db.CreateCommand("INSERT INTO suite.suite_audit_events(event_type,correlation_id,outcome,detail_code,admin_actor) VALUES('SUITE_SESSION_ADMIN_DENIED',$1,'DENIED',$2,$3)");
            Add(audit,Guid.NewGuid().ToString("N"),security.DetailCode,security.Actor);await audit.ExecuteNonQueryAsync(timeout.Token);
        }
        catch(Exception) { /* A failed audit sink never grants the denied operation. */ }
        return Failure(403,"PERMISSION_DENIED");
    }
    private static IResult Failure(int status,string code)=>Results.Json(new RevokeEsSessionResult(code),statusCode:status);
    private static void Add(NpgsqlCommand command,params object[] values){foreach(var value in values)command.Parameters.AddWithValue(value);}
}
