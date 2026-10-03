using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

internal static class StationCodeManagement
{
    public static void Map(WebApplication app,bool enabled)
    {
        app.MapPost("/station/licenses/{licenseId}/actions/replace-code", async
            (string licenseId,StationReplaceCodeRequest request,HttpContext context,
             NpgsqlDataSource database,CancellationToken token)=>
        {
            if(!enabled)return Results.NotFound();
            var security=ContentAdminSecurity.Authorize(context,"station.licenses.manage",true,false);
            if(!security.Allowed)return Failure(403,"STATION_PERMISSION_DENIED");
            if(request is null || request.Actor!=security.Actor || !Id(licenseId,6,64) ||
                !Id(request.RequestId,16,64) || request.ExpectedGeneration<0 || request.ExpectedActivationGeneration<0 ||
                request.Kind is not ("human" or "purchase") ||
                request.Reason is not null && (request.Reason.Length is <10 or >256 || request.Reason.Any(char.IsControl)))
                return Failure(400,"STATION_REQUEST_INVALID");
            try
            {
                await using var connection=await database.OpenConnectionAsync(token);
                await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.Serializable,token);
                await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('station-issue:'||$1,0))",connection,transaction))
                {gate.Parameters.AddWithValue(licenseId);await gate.ExecuteNonQueryAsync(token);}
                await using(var prior=new NpgsqlCommand("SELECT 1 FROM suite.station_management_audit WHERE request_id=$1 AND license_id=$2",connection,transaction))
                {prior.Parameters.AddWithValue(request.RequestId);prior.Parameters.AddWithValue(licenseId);if(await prior.ExecuteScalarAsync(token) is not null)
                    return Failure(409,"STATION_REQUEST_ALREADY_COMPLETED");}
                long activation;
                await using(var license=new NpgsqlCommand("""
                    SELECT activation_generation,revocation_generation,status,enrollment_state,activation_consumed,
                      EXISTS(SELECT 1 FROM suite.suite_license_deliveries d
                        WHERE d.license_id=l.license_id AND d.product_id='TURBORAMA_STATION_ANDROID'
                          AND d.source_product_sku='STATION_ANDROID_LIFETIME_1_DEVICE'
                          AND d.provisioning_state='PROVISIONED' AND d.financial_state='PAID'),
                      license_term='LIFETIME' AND expires_at IS NULL AND maximum_active_devices=1
                    FROM suite.suite_licenses l WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
                    FOR UPDATE OF l
                    """,connection,transaction))
                {
                    license.Parameters.AddWithValue(licenseId);
                    await using var row=await license.ExecuteReaderAsync(token);
                    if(!await row.ReadAsync(token))return Failure(404,"STATION_NOT_FOUND");
                    activation=row.GetInt64(0);
                    if(request.ExpectedGeneration is not null && row.GetInt64(1)!=request.ExpectedGeneration ||
                       request.ExpectedActivationGeneration is not null && activation!=request.ExpectedActivationGeneration)
                        return Failure(409,"STATION_STATE_CHANGED");
                    if(row.GetBoolean(4)||row.GetString(3)=="BOUND")return Failure(409,"STATION_ALREADY_ACTIVATED");
                    if(row.GetString(2)!="ACTIVE"||row.GetString(3)!="PENDING_ENROLLMENT"||!row.GetBoolean(5)||!row.GetBoolean(6))
                        return Failure(409,"STATION_DELIVERY_NOT_ELIGIBLE");
                }
                var bytes=RandomNumberGenerator.GetBytes(32);
                var code=Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
                CryptographicOperations.ZeroMemory(bytes);
                var pepper=Convert.FromBase64String((await File.ReadAllTextAsync(
                    Environment.GetEnvironmentVariable("STATION_ADMIN_PEPPER_FILE")!,token)).Trim());
                if(pepper.Length<32)throw new InvalidOperationException("Station pepper invalid");
                string verifier;
                try{verifier=Convert.ToHexString(HMACSHA256.HashData(pepper,Encoding.UTF8.GetBytes(code))).ToLowerInvariant();}
                finally{CryptographicOperations.ZeroMemory(pepper);}
                var ttl=request.Kind=="purchase"?2880:30;
                DateTime expires;
                await using(var update=new NpgsqlCommand("""
                    UPDATE suite.suite_licenses SET activation_verifier=$2,
                      activation_expires_at=clock_timestamp()+make_interval(mins=>$3),
                      activation_consumed=false,activation_generation=activation_generation+1,updated_at=clock_timestamp()
                    WHERE license_id=$1 RETURNING activation_expires_at
                    """,connection,transaction))
                {
                    update.Parameters.AddWithValue(licenseId);update.Parameters.AddWithValue(verifier);update.Parameters.AddWithValue(ttl);
                    expires=(DateTime)(await update.ExecuteScalarAsync(token)??throw new InvalidOperationException());
                }
                await using(var receipt=new NpgsqlCommand("""
                    INSERT INTO suite.station_admin_code_issues(request_id,license_id,activation_generation,actor,kind,reason)
                    VALUES($1,$2,$3,$4,$5,$6)
                    """,connection,transaction))
                {
                    receipt.Parameters.AddWithValue(request.RequestId);receipt.Parameters.AddWithValue(licenseId);
                    receipt.Parameters.AddWithValue(activation+1);receipt.Parameters.AddWithValue(request.Actor);
                    receipt.Parameters.AddWithValue(request.Kind);receipt.Parameters.AddWithValue(request.Reason??
                        (request.Kind=="purchase"?"Emissão inicial de código da compra.":"Reemissão de código pelo atendimento."));
                    await receipt.ExecuteNonQueryAsync(token);
                }
                await using(var audit=new NpgsqlCommand("""
                    INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,outcome,
                      detail_code,admin_actor,request_id,otp_expires_at)
                    VALUES('STATION_CODE_ISSUED',$1,$2,'SUCCESS',$3,$4,$2,$5)
                    """,connection,transaction))
                {
                    audit.Parameters.AddWithValue(licenseId);audit.Parameters.AddWithValue(request.RequestId);
                    audit.Parameters.AddWithValue(request.Kind=="purchase"?"PURCHASE_FIRST":"ADMIN_REISSUE");
                    audit.Parameters.AddWithValue(request.Actor);audit.Parameters.AddWithValue(expires);
                    await audit.ExecuteNonQueryAsync(token);
                }
                await transaction.CommitAsync(token);
                return Results.Json(new {licenseId,activationCode=code,expiresAt=expires,ttlMinutes=ttl,
                    firstIssue=request.Kind=="purchase",kind=request.Kind,activationGeneration=activation+1});
            }
            catch(PostgresException e) when(e.SqlState is PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
            {return Failure(409,"STATION_STATE_CHANGED");}
            catch(Exception){return Failure(503,"STATION_ADMIN_UNAVAILABLE");}
        });
    }
    private static bool Id(string? s,int min,int max)=>s?.Length>=min&&s.Length<=max&&s.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static IResult Failure(int status,string code)=>Results.Json(new {code},statusCode:status);
}
internal sealed record StationReplaceCodeRequest(string Actor,string RequestId,
    long? ExpectedGeneration,long? ExpectedActivationGeneration,string Kind="human",string? Reason=null);
