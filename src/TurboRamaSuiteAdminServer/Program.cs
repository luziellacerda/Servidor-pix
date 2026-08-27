using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

const string product = "TURBORAMA_SUITE";
var builder = WebApplication.CreateBuilder(args);
var socket = Required("SUITE_ADMIN_SOCKET");
var tokenFile = Required("SUITE_ADMIN_TOKEN_FILE");
var pepperFile = Required("SUITE_ADMIN_PEPPER_FILE");
var connection = Required("SUITE_ADMIN_CONNECTION");
if (File.Exists(socket)) File.Delete(socket);
builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(socket));
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
var app = builder.Build();
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Suite-Admin-Token"].ToString();
    var expected = await File.ReadAllTextAsync(tokenFile, context.RequestAborted);
    if (!Fixed(supplied, expected.Trim())) { context.Response.StatusCode = 404; return; }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.MapGet("/health", () => Results.Json(new { status = "ok", service = "turborama-suite-admin" }));
app.MapGet("/status/{licenseId}", async (string licenseId, NpgsqlDataSource db, CancellationToken ct) =>
{
    ValidateId(licenseId);
    await using var cmd = db.CreateCommand("SELECT l.license_id,l.product_id,l.status,l.license_term,l.expires_at,l.identity_policy,l.maximum_active_devices,l.activation_consumed,l.activation_verifier IS NOT NULL,l.activation_expires_at,e.device_id,e.binding_type,e.identity_policy,e.algorithm,e.hardware_fingerprint,(SELECT count(*) FROM suite.suite_devices d WHERE d.license_id=l.license_id AND d.status='ACTIVE'),(SELECT session_id FROM suite.suite_sessions s WHERE s.license_id=l.license_id AND s.status='ACTIVE' LIMIT 1) FROM suite.suite_licenses l JOIN suite.suite_license_enrollments e USING(license_id) WHERE l.license_id=$1 AND l.product_id='TURBORAMA_SUITE'");
    cmd.Parameters.AddWithValue(licenseId); await using var r = await cmd.ExecuteReaderAsync(ct);
    if (!await r.ReadAsync(ct)) return Results.NotFound();
    return Results.Json(new SuiteStatus(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetDateTime(4),r.GetString(5),r.GetInt16(6),r.GetBoolean(7),r.GetBoolean(8),r.IsDBNull(9)?null:r.GetDateTime(9),r.GetString(10),r.GetString(11),r.GetString(12),r.GetString(13),r.GetString(14),r.GetInt64(15),r.IsDBNull(16)?null:r.GetString(16)));
});
app.MapPost("/issue", async (IssueRequest request, NpgsqlDataSource db, CancellationToken ct) =>
{
    ValidateId(request.LicenseId); Hex(request.DeviceId); ValidateText(request.Actor,64); ValidateText(request.RequestId,128);
    if (request.TtlSeconds is < 300 or > 1800) return Results.BadRequest(new Error("TTL_INVALID"));
    var pepper = Convert.FromBase64String((await File.ReadAllTextAsync(pepperFile,ct)).Trim());
    var otpBytes = RandomNumberGenerator.GetBytes(32); var otp = Convert.ToBase64String(otpBytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    var verifier = Convert.ToHexString(HMACSHA256.HashData(pepper,Encoding.UTF8.GetBytes(otp))).ToLowerInvariant();
    CryptographicOperations.ZeroMemory(pepper); CryptographicOperations.ZeroMemory(otpBytes);
    await using var conn = await db.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
    try
    {
        await using var read = new NpgsqlCommand("SELECT l.status,l.license_term,l.expires_at,l.maximum_active_devices,l.activation_consumed,l.activation_verifier,l.activation_expires_at,e.device_id,e.binding_type,e.identity_policy,e.algorithm,e.public_key_spki,e.hardware_fingerprint FROM suite.suite_licenses l JOIN suite.suite_license_enrollments e USING(license_id) WHERE l.license_id=$1 AND l.product_id=$2 FOR UPDATE OF l,e",conn,tx);
        read.Parameters.AddWithValue(request.LicenseId); read.Parameters.AddWithValue(product); await using var row=await read.ExecuteReaderAsync(ct);
        if(!await row.ReadAsync(ct)){await row.DisposeAsync();await Deny("LICENSE_NOT_FOUND");return Results.NotFound(new Error("LICENSE_NOT_FOUND"));}
        var status=row.GetString(0);var term=row.GetString(1);var expires=row.IsDBNull(2);var max=row.GetInt16(3);var consumed=row.GetBoolean(4);var hasVerifier=!row.IsDBNull(5);var oldExpiry=row.IsDBNull(6)?(DateTime?)null:row.GetDateTime(6);var device=row.GetString(7);var binding=row.GetString(8);var policy=row.GetString(9);var algorithm=row.GetString(10);var spki=row.GetString(11);var fingerprint=row.GetString(12);await row.DisposeAsync();
        var now=DateTime.UtcNow;
        if(status!="ACTIVE"||term!="LIFETIME"||!expires||max!=1||consumed||device!=request.DeviceId||binding!="SOFTWARE_BOUND_ONLINE"||policy!="SOFTWARE_ONLY"||algorithm!="rsa-pss-sha256"||spki.Length<300||fingerprint.Length!=64){await Deny("LICENSE_OR_ENROLLMENT_DENIED");return Results.StatusCode(409);}
        await using(var count=new NpgsqlCommand("SELECT count(*) FROM suite.suite_devices WHERE license_id=$1 AND status='ACTIVE'",conn,tx)){count.Parameters.AddWithValue(request.LicenseId);if((long)(await count.ExecuteScalarAsync(ct)??0L)!=0){await Deny("ACTIVE_DEVICE_EXISTS");return Results.StatusCode(409);}}
        if(hasVerifier&&oldExpiry>now){await Deny("OTP_STILL_VALID");return Results.StatusCode(409);}
        await using(var invalidate=new NpgsqlCommand("UPDATE suite.suite_challenges SET consumed_at=clock_timestamp() WHERE license_id=$1 AND action='device.activate' AND consumed_at IS NULL",conn,tx)){invalidate.Parameters.AddWithValue(request.LicenseId);await invalidate.ExecuteNonQueryAsync(ct);}
        var otpExpiry=now.AddSeconds(request.TtlSeconds);
        await using(var update=new NpgsqlCommand("UPDATE suite.suite_licenses SET activation_verifier=$2,activation_expires_at=$3,activation_consumed=false,updated_at=clock_timestamp() WHERE license_id=$1",conn,tx)){update.Parameters.AddWithValue(request.LicenseId);update.Parameters.AddWithValue(verifier);update.Parameters.AddWithValue(otpExpiry);if(await update.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException();}
        await using(var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id,otp_expires_at) VALUES('SUITE_OTP_ISSUED',$1,$2,$3,'SUCCESS','OTP_ISSUED',$4,$5,$6)",conn,tx)){audit.Parameters.AddWithValue(request.LicenseId);audit.Parameters.AddWithValue(request.DeviceId);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(request.Actor);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(otpExpiry);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct); return Results.Json(new IssueResponse(product,request.LicenseId,request.DeviceId,otp,otpExpiry));
        async Task Deny(string code){await using var a=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id) VALUES('SUITE_OTP_DENIED',$1,$2,$3,'DENIED',$4,$5,$6)",conn,tx);a.Parameters.AddWithValue(request.LicenseId);a.Parameters.AddWithValue(request.DeviceId);a.Parameters.AddWithValue(request.RequestId);a.Parameters.AddWithValue(code);a.Parameters.AddWithValue(request.Actor);a.Parameters.AddWithValue(request.RequestId);await a.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);}
    }
    catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.UniqueViolation){await tx.RollbackAsync(ct);return Results.Conflict(new Error("REQUEST_REPLAY"));}
    finally { verifier=""; otp=""; }
});
app.MapGet("/audit.csv", async (NpgsqlDataSource db,CancellationToken ct)=>{await using var cmd=db.CreateCommand("SELECT occurred_at,event_type,coalesce(admin_actor,''),coalesce(license_id,''),coalesce(device_id,''),outcome,detail_code,coalesce(request_id,''),coalesce(otp_expires_at::text,'') FROM suite.suite_audit_events WHERE event_type LIKE 'SUITE_OTP_%' ORDER BY occurred_at DESC LIMIT 1000");await using var r=await cmd.ExecuteReaderAsync(ct);var b=new StringBuilder("occurred_at,event,actor,license_id,device_id,outcome,detail,request_id,otp_expires_at\n");while(await r.ReadAsync(ct)){for(var i=0;i<9;i++){if(i>0)b.Append(',');b.Append('"').Append(r.GetString(i).Replace("\"","\"\"")).Append('"');}b.AppendLine();}return Results.Text(b.ToString(),"text/csv; charset=utf-8");});
app.Run();

string Required(string key)=>Environment.GetEnvironmentVariable(key)?.Trim() is {Length:>0} value?value:throw new InvalidOperationException(key+" missing");
static bool Fixed(string a,string b)=>a.Length==b.Length&&CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a),Encoding.UTF8.GetBytes(b));
static void ValidateId(string v){if(v.Length is <6 or >64||v.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))throw new BadHttpRequestException("invalid",400);}
static void Hex(string v){if(v.Length!=64||v.Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f')))throw new BadHttpRequestException("invalid",400);}
static void ValidateText(string v,int max){if(v.Length is <1||v.Length>max||v.Any(c=>char.IsControl(c)))throw new BadHttpRequestException("invalid",400);}
sealed record IssueRequest(string LicenseId,string DeviceId,int TtlSeconds,string Actor,string RequestId);
sealed record IssueResponse(string ProductId,string LicenseId,string DeviceId,string Otp,DateTime ExpiresAt);
sealed record Error(string Code);
sealed record SuiteStatus(string LicenseId,string ProductId,string Status,string LicenseTerm,DateTime? ExpiresAt,string IdentityPolicy,int MaximumActiveDevices,bool ActivationConsumed,bool OtpIssued,DateTime? OtpExpiresAt,string DeviceId,string BindingType,string EnrollmentPolicy,string Algorithm,string HardwareFingerprint,long ActiveDevices,string? SessionId);
public partial class Program;
