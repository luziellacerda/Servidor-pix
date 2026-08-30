using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
if (args.Contains("--content-self-test", StringComparer.OrdinalIgnoreCase))
{
    ContentManagementSelfTest.Run();
    Console.WriteLine("SUITE CONTENT ADMIN SELF-TEST: OK");
    return 0;
}
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) return AdminSelfTest.Run();

var builder = WebApplication.CreateBuilder(args);
var socketPath = Required("SUITE_ADMIN_SOCKET");
var token = InternalToken.Load(Required("SUITE_ADMIN_TOKEN_FILE"));
var commerceEnabled = Environment.GetEnvironmentVariable("SUITE_COMMERCE_ENABLED") == "1";
var commerceToken = commerceEnabled ? InternalToken.Load(Required("SUITE_COMMERCE_TOKEN_FILE")) : null;
var pepperFile = Required("SUITE_ADMIN_PEPPER_FILE");
var connection = Required("SUITE_ADMIN_CONNECTION");
var contentManagementEnabled = Environment.GetEnvironmentVariable("SUITE_CONTENT_ADMIN_ENABLED") == "1";
using var contentManagement = await ContentManagementBootstrap.TryLoadAsync(
    contentManagementEnabled, CancellationToken.None);
if (File.Exists(socketPath)) File.Delete(socketPath);
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 16 * 1024; options.ListenUnixSocket(socketPath); });
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton(token);
var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => { if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException(); File.SetUnixFileMode(socketPath,
    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite); });
app.Use(async (context, next) =>
{
    var commerceRequest = context.Request.Path.StartsWithSegments("/commerce");
    var authenticated = commerceRequest
        ? commerceEnabled && commerceToken!.Authenticates(context.Request.Headers["X-Suite-Commerce-Token"].ToString())
        : token.Authenticates(context.Request.Headers["X-Suite-Admin-Token"].ToString());
    if (!authenticated)
    {
        if (contentManagement is not null && context.Request.Path.StartsWithSegments("/content"))
            await contentManagement.TryAuditDenialAsync("CONTENT_AUTH_DENIED",
                context.Request.Headers["X-Suite-Admin-Actor"].ToString(), null,
                "AUTH_REQUIRED", context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    await next();
});
app.MapGet("/health", () => Results.Json(new { status = "ok", service = "turborama-suite-admin" }));
app.MapGet("/readiness", async (NpgsqlDataSource db,CancellationToken ct) =>
{
    var schemaReady=false;long inconsistentDeliveries=-1;
    try
    {
        await using var cmd=db.CreateCommand("""
          SELECT EXISTS(SELECT 1 FROM suite.schema_migrations WHERE version='005_suite_commerce_permissions'),
            (SELECT count(*) FROM suite.suite_license_deliveries
             WHERE (provisioning_state='PROVISIONED' AND license_id IS NULL)
                OR (financial_state='PAID' AND provisioning_state<>'PROVISIONED'))
          """);
        await using var row=await cmd.ExecuteReaderAsync(ct);
        if(await row.ReadAsync(ct)){schemaReady=row.GetBoolean(0);inconsistentDeliveries=row.GetInt64(1);}
    }
    catch { schemaReady=false;inconsistentDeliveries=-1; }
    var ready=commerceEnabled&&schemaReady&&inconsistentDeliveries==0;
    var contentReady=!contentManagementEnabled||contentManagement is not null&&await contentManagement.IsReadyAsync(ct);
    return Results.Json(new
    {
        status=ready?(contentReady?"ready":"degraded"):"not_ready", service="turborama-suite-admin", commerce_enabled=commerceEnabled,
        content_management_enabled=contentManagementEnabled,
        checks=new { database=schemaReady?"ok":"unavailable",migration_005=schemaReady?"ok":"missing",
          migration_013=contentReady?"ok":"missing_or_unreachable",
          content_management=contentReady?"ok":"unavailable",
          delivery_consistency=inconsistentDeliveries==0?"ok":"blocked",inconsistent_deliveries=inconsistentDeliveries }
    },statusCode:ready?StatusCodes.Status200OK:StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/readiness/content", async (CancellationToken ct) =>
{
    var ready=contentManagementEnabled&&contentManagement is not null&&
      await contentManagement.IsReadyAsync(ct);
    return Results.Json(new { status=ready?"ready":"unavailable",
      service="turborama-suite-content-admin" },
      statusCode:ready?StatusCodes.Status200OK:StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/customer-activity/{licenseId}", async (string licenseId, NpgsqlDataSource db,
    CancellationToken ct) =>
{
    ValidateId(licenseId);
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var summary = new NpgsqlCommand("""
        SELECT l.status,l.activation_consumed,l.enrollment_state,
          coalesce(e.device_id,''),coalesce(d.status,''),coalesce((SELECT c.device_json->>'clientVersion'
            FROM suite.suite_challenges c WHERE c.license_id=l.license_id AND c.device_json IS NOT NULL
            ORDER BY c.created_at DESC LIMIT 1),''),
          coalesce(extract(epoch from d.updated_at)::bigint,0),
          EXISTS(SELECT 1 FROM suite.suite_sessions s WHERE s.license_id=l.license_id
            AND s.status='ACTIVE' AND s.authorized_until>clock_timestamp()),
          (SELECT count(DISTINCT g.item_id) FROM suite.suite_content_grants g
            WHERE g.license_id=l.license_id AND g.state='COMPLETED'),
          (SELECT count(*) FROM suite.suite_content_grants g
            WHERE g.license_id=l.license_id AND g.state='COMPLETED'),
          (SELECT max(extract(epoch from s.updated_at)::bigint) FROM suite.suite_sessions s
            WHERE s.license_id=l.license_id)
        FROM suite.suite_licenses l
        LEFT JOIN suite.suite_license_enrollments e USING(license_id)
        LEFT JOIN suite.suite_devices d ON d.license_id=l.license_id AND d.device_id=e.device_id
        WHERE l.license_id=$1 AND l.product_id='TURBORAMA_SUITE'
        """, conn);
    summary.Parameters.AddWithValue(licenseId);
    string status,enrollment,deviceId,deviceStatus,agentVersion;bool verified,online;
    long deviceUpdated,uniqueDownloads,downloadAttempts,lastSession;
    await using(var row=await summary.ExecuteReaderAsync(ct))
    {
        if(!await row.ReadAsync(ct))return Results.NotFound();
        status=row.GetString(0);verified=row.GetBoolean(1);enrollment=row.GetString(2);
        deviceId=row.GetString(3);deviceStatus=row.GetString(4);agentVersion=row.GetString(5);
        deviceUpdated=row.GetInt64(6);online=row.GetBoolean(7);uniqueDownloads=row.GetInt64(8);
        downloadAttempts=row.GetInt64(9);lastSession=row.IsDBNull(10)?0:row.GetInt64(10);
    }
    var events=new List<CustomerActivityEvent>();
    await using var history=new NpgsqlCommand("""
        SELECT kind,at_unix,title,detail FROM (
          SELECT 'DOWNLOAD'::text kind,extract(epoch from coalesce(x.completed_at,x.created_at))::bigint at_unix,
            coalesce(i.display_name,x.item_id::text) title,
            (CASE x.state WHEN 'COMPLETED' THEN 'Download concluído' ELSE 'Última tentativa: '||x.state END||
              ' · versão '||x.artifact_version::text)::text detail
          FROM (
            SELECT DISTINCT ON(g.item_id) g.* FROM suite.suite_content_grants g
            WHERE g.license_id=$1
            ORDER BY g.item_id,(g.state='COMPLETED') DESC,coalesce(g.completed_at,g.created_at) DESC
          ) x LEFT JOIN suite.suite_content_items i
            ON i.catalog_identity=x.catalog_identity AND i.item_id=x.item_id
          UNION ALL
          SELECT 'SESSAO',extract(epoch from s.updated_at)::bigint,'Sessão do programa',
            (s.status||' · autorização até '||to_char(s.authorized_until AT TIME ZONE 'UTC','YYYY-MM-DD HH24:MI:SS')||' UTC')::text
          FROM suite.suite_sessions s WHERE s.license_id=$1
          UNION ALL
          SELECT 'SEGURANCA',extract(epoch from a.occurred_at)::bigint,a.event_type,
            (a.outcome||' · '||a.detail_code)::text
          FROM suite.suite_audit_events a WHERE a.license_id=$1
        ) h ORDER BY at_unix DESC LIMIT 200
        """,conn);
    history.Parameters.AddWithValue(licenseId);
    await using var hr=await history.ExecuteReaderAsync(ct);
    while(await hr.ReadAsync(ct))events.Add(new(hr.GetString(0),hr.GetInt64(1),hr.GetString(2),hr.GetString(3)));
    return Results.Json(new CustomerActivity(status,verified,enrollment,deviceId,deviceStatus,
        agentVersion,deviceUpdated,online,uniqueDownloads,downloadAttempts,lastSession,events));
});
app.MapPost("/customer-activity/clear",async(CustomerActivityClearRequest request,NpgsqlDataSource db,CancellationToken ct)=>
{
    ValidateId(request.LicenseId);ValidateText(request.Actor,64);ValidateText(request.RequestId,128);
    await using var conn=await db.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(ct);
    await using var clear=new NpgsqlCommand("DELETE FROM suite.suite_content_grants WHERE license_id=$1 AND state IN('COMPLETED','FAILED','REVOKED','EXPIRED')",conn,tx);
    clear.Parameters.AddWithValue(request.LicenseId);var deleted=await clear.ExecuteNonQueryAsync(ct);
    await using var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,outcome,detail_code,admin_actor,request_id) VALUES('SUITE_DOWNLOAD_HISTORY_CLEARED',$1,$2,'SUCCESS',$3,$4,$2)",conn,tx);
    audit.Parameters.AddWithValue(request.LicenseId);audit.Parameters.AddWithValue(request.RequestId);
    audit.Parameters.AddWithValue("REMOVED_"+deleted.ToString(CultureInfo.InvariantCulture)+"_RECORDS");audit.Parameters.AddWithValue(request.Actor);
    await audit.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    return Results.Json(new CustomerActivityClearResult(deleted));
});
CommerceEndpoints.Map(app, commerceEnabled, pepperFile);
ContentManagementEndpoints.Map(app, contentManagement, contentManagementEnabled);
app.MapPost("/issue-first-claim",async(CommerceAdminIssueRequest request,NpgsqlDataSource db,CancellationToken ct)=>
{
    ValidateId(request.LicenseId);ValidateText(request.Actor,64);ValidateText(request.RequestId,128);
    if(!commerceEnabled)return Results.NotFound();
    try{return Results.Json(await CommerceEndpoints.IssueForAdmin(request.LicenseId,
        new CommerceIssueRequest(request.Actor,request.RequestId),db,pepperFile,ct));}
    catch(CommerceConflict ex){return Results.Conflict(new Error(ex.Code));}
});
app.MapGet("/status/{licenseId}", async (string licenseId, NpgsqlDataSource db, CancellationToken ct) =>
{
    ValidateId(licenseId);
    await using var conn = await db.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("""
        SELECT l.license_id,l.product_id,l.status,l.license_term,l.expires_at,l.identity_policy,
          l.maximum_active_devices,l.activation_consumed,l.activation_verifier IS NOT NULL,l.activation_expires_at,
          e.device_id,e.binding_type,e.identity_policy,e.algorithm,e.hardware_fingerprint,e.public_key_spki,
          (SELECT count(*) FROM suite.suite_devices d WHERE d.license_id=l.license_id AND d.status='ACTIVE'),
          (SELECT session_id FROM suite.suite_sessions s WHERE s.license_id=l.license_id AND s.status='ACTIVE' LIMIT 1),
          CASE WHEN l.activation_consumed THEN 'CONSUMED' WHEN l.activation_verifier IS NULL THEN 'NOT_ISSUED'
               WHEN l.activation_expires_at > clock_timestamp() THEN 'VALID' ELSE 'EXPIRED' END,clock_timestamp()
        FROM suite.suite_licenses l LEFT JOIN suite.suite_license_enrollments e USING(license_id)
        WHERE l.license_id=$1 AND l.product_id='TURBORAMA_SUITE'
        """, conn);
    cmd.Parameters.AddWithValue(licenseId);SuiteStatus status;
    await using (var reader = await cmd.ExecuteReaderAsync(ct))
    {
        if (!await reader.ReadAsync(ct)) return Results.NotFound();
        var expires=reader.IsDBNull(4)?null:(DateTime?)reader.GetDateTime(4);var otpExpires=reader.IsDBNull(9)?null:(DateTime?)reader.GetDateTime(9);var active=reader.GetInt64(16);
        var hasEnrollment=!reader.IsDBNull(10);
        var device=hasEnrollment?reader.GetString(10):null;var binding=hasEnrollment?reader.GetString(11):null;var enrollmentPolicy=hasEnrollment?reader.GetString(12):null;
        var algorithm=hasEnrollment?reader.GetString(13):null;var fingerprint=hasEnrollment?reader.GetString(14):null;var spki=hasEnrollment?reader.GetString(15):null;
        var canIssue=false;
        if(hasEnrollment)canIssue=SuiteEligibility.CanIssue(new SuiteEligibilityInput(reader.GetString(1),reader.GetString(2),reader.GetString(3),expires is null,reader.GetString(5),reader.GetInt16(6),reader.GetBoolean(7),reader.GetBoolean(8),otpExpires,reader.GetDateTime(19),device!,device!,binding!,enrollmentPolicy!,algorithm!,spki!,fingerprint!,active));
        status=new SuiteStatus(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),expires,reader.GetString(5),reader.GetInt16(6),reader.GetBoolean(7),reader.GetBoolean(8),otpExpires,device,binding,enrollmentPolicy,algorithm,fingerprint,active,reader.IsDBNull(17)?null:reader.GetString(17),reader.GetString(18),canIssue,[]);
    }
    var events = new List<SuiteAuditItem>();
    await using var audit = new NpgsqlCommand("""
        SELECT to_char(occurred_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),event_type,
          outcome,detail_code,coalesce(admin_actor,''),coalesce(request_id,''),
          CASE WHEN otp_expires_at IS NULL THEN NULL ELSE to_char(otp_expires_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END
        FROM suite.suite_audit_events WHERE license_id=$1 AND event_type LIKE 'SUITE_OTP_%' ORDER BY occurred_at DESC LIMIT 20
        """,conn);
    audit.Parameters.AddWithValue(licenseId);await using var ar=await audit.ExecuteReaderAsync(ct);
    while(await ar.ReadAsync(ct))events.Add(new(ar.GetString(0),ar.GetString(1),ar.GetString(2),ar.GetString(3),ar.GetString(4),ar.GetString(5),ar.IsDBNull(6)?null:ar.GetString(6)));
    return Results.Json(status with { RecentEvents=events });
});
app.MapPost("/issue", async (IssueRequest request, NpgsqlDataSource db, CancellationToken ct) =>
{
    ValidateId(request.LicenseId); Hex(request.DeviceId); ValidateText(request.Actor,64); ValidateText(request.RequestId,128);
    if (request.TtlSeconds is < 300 or > 1800) return Results.BadRequest(new Error("TTL_INVALID"));
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try { return await IssueOnce(request, db, pepperFile, ct); }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected && attempt < 3)
        { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt, ct); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { return Results.Conflict(new Error("REQUEST_REPLAY")); }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        { return Results.Conflict(new Error("TRANSACTION_RETRY_EXHAUSTED")); }
    }
    return Results.Conflict(new Error("TRANSACTION_RETRY_EXHAUSTED"));
});
app.MapPost("/deny", async (DenialRequest request,NpgsqlDataSource db,CancellationToken ct) =>
{
    ValidateId(request.LicenseId);Hex(request.DeviceId);ValidateText(request.Actor,64);ValidateText(request.RequestId,64);
    if(request.DetailCode is not ("STEP_UP_DENIED" or "RATE_LIMIT"))return Results.BadRequest(new Error("DETAIL_INVALID"));
    try
    {
        await using var cmd=db.CreateCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id) SELECT 'SUITE_OTP_DENIED',$1,$2,$3,'DENIED',$4,$5,$3 FROM suite.suite_licenses l JOIN suite.suite_license_enrollments e USING(license_id) WHERE l.license_id=$1 AND l.product_id='TURBORAMA_SUITE' AND e.device_id=$2");
        cmd.Parameters.AddWithValue(request.LicenseId);cmd.Parameters.AddWithValue(request.DeviceId);cmd.Parameters.AddWithValue(request.RequestId);cmd.Parameters.AddWithValue(request.DetailCode);cmd.Parameters.AddWithValue(request.Actor);
        return await cmd.ExecuteNonQueryAsync(ct)==1?Results.Json(new Error("AUDITED")):Results.NotFound(new Error("LICENSE_NOT_FOUND"));
    }
    catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){return Results.Conflict(new Error("REQUEST_REPLAY"));}
});
app.MapGet("/audit.csv", async (NpgsqlDataSource db,CancellationToken ct) =>
{
    await using var cmd=db.CreateCommand("""
        SELECT to_char(occurred_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),event_type,
          coalesce(admin_actor,''),coalesce(license_id,''),coalesce(device_id,''),outcome,detail_code,
          coalesce(request_id,''),coalesce(to_char(otp_expires_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),'')
        FROM suite.suite_audit_events WHERE event_type LIKE 'SUITE_OTP_%'
        ORDER BY occurred_at DESC LIMIT 1000
        """);
    await using var reader=await cmd.ExecuteReaderAsync(ct);
    var csv=new StringBuilder("occurred_at,event,actor,license_id,device_id,outcome,detail,request_id,otp_expires_at\r\n");
    while(await reader.ReadAsync(ct))
    {
        for(var i=0;i<9;i++){if(i>0)csv.Append(',');csv.Append('"').Append(reader.GetString(i).Replace("\"","\"\"")).Append('"');}
        csv.Append("\r\n");
    }
    return Results.Text(csv.ToString(),"text/csv; charset=utf-8");
});
await app.RunAsync();
return 0;

static async Task<IResult> IssueOnce(IssueRequest request,NpgsqlDataSource db,string pepperFile,CancellationToken ct)
{
    const string product="TURBORAMA_SUITE";
    await using var conn=await db.OpenConnectionAsync(ct);
    await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
    await using var read=new NpgsqlCommand("""
        SELECT l.status,l.license_term,l.expires_at,l.maximum_active_devices,l.identity_policy,l.activation_consumed,
          l.activation_verifier,l.activation_expires_at,e.device_id,e.binding_type,e.identity_policy,e.algorithm,
          e.public_key_spki,e.hardware_fingerprint,clock_timestamp()
        FROM suite.suite_licenses l JOIN suite.suite_license_enrollments e USING(license_id)
        WHERE l.license_id=$1 AND l.product_id=$2 FOR UPDATE OF l
        """,conn,tx);
    read.Parameters.AddWithValue(request.LicenseId);read.Parameters.AddWithValue(product);await using var row=await read.ExecuteReaderAsync(ct);
    if(!await row.ReadAsync(ct)){await row.DisposeAsync();await Deny("LICENSE_NOT_FOUND");return Results.NotFound(new Error("LICENSE_NOT_FOUND"));}
    var status=row.GetString(0);var term=row.GetString(1);var noExpiry=row.IsDBNull(2);var maximum=row.GetInt16(3);var licensePolicy=row.GetString(4);
    var consumed=row.GetBoolean(5);var hasVerifier=!row.IsDBNull(6);var oldExpiry=row.IsDBNull(7)?(DateTime?)null:row.GetDateTime(7);
    var device=row.GetString(8);var binding=row.GetString(9);var policy=row.GetString(10);var algorithm=row.GetString(11);var spki=row.GetString(12);var fingerprint=row.GetString(13);var databaseNow=row.GetDateTime(14);await row.DisposeAsync();
    long activeDevices;await using(var count=new NpgsqlCommand("SELECT count(*) FROM suite.suite_devices WHERE license_id=$1 AND status='ACTIVE'",conn,tx)){count.Parameters.AddWithValue(request.LicenseId);activeDevices=(long)(await count.ExecuteScalarAsync(ct)??0L);}
    var eligible=new SuiteEligibilityInput(product,status,term,noExpiry,licensePolicy,maximum,consumed,hasVerifier,oldExpiry,databaseNow,device,request.DeviceId,binding,policy,algorithm,spki,fingerprint,activeDevices);
    if(!SuiteEligibility.CanIssue(eligible)){await Deny("LICENSE_OR_ENROLLMENT_DENIED");return Results.Conflict(new Error("LICENSE_OR_ENROLLMENT_DENIED"));}
    await using(var invalidate=new NpgsqlCommand("UPDATE suite.suite_challenges SET consumed_at=clock_timestamp() WHERE license_id=$1 AND action='device.activate' AND consumed_at IS NULL",conn,tx))
    {invalidate.Parameters.AddWithValue(request.LicenseId);await invalidate.ExecuteNonQueryAsync(ct);}
    var otpBytes=RandomNumberGenerator.GetBytes(32);
    var otp=Convert.ToBase64String(otpBytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    byte[] pepper=[];byte[] otpUtf8=[];string verifier="";
    try
    {
        pepper=Convert.FromBase64String((await File.ReadAllTextAsync(pepperFile,ct)).Trim());
        if(pepper.Length<32)throw new InvalidOperationException("PEPPER_INVALID");
        otpUtf8=Encoding.UTF8.GetBytes(otp);
        verifier=Convert.ToHexString(HMACSHA256.HashData(pepper,otpUtf8)).ToLowerInvariant();
        DateTime otpExpiry;
        await using(var update=new NpgsqlCommand("UPDATE suite.suite_licenses SET activation_verifier=$2,activation_expires_at=clock_timestamp()+make_interval(secs=>$3),activation_consumed=false,updated_at=clock_timestamp() WHERE license_id=$1 RETURNING activation_expires_at",conn,tx))
        {update.Parameters.AddWithValue(request.LicenseId);update.Parameters.AddWithValue(verifier);update.Parameters.AddWithValue(request.TtlSeconds);otpExpiry=(DateTime)(await update.ExecuteScalarAsync(ct)??throw new InvalidOperationException("UPDATE_FAILED"));}
        await using(var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id,otp_expires_at) VALUES('SUITE_OTP_ISSUED',$1,$2,$3,'SUCCESS','OTP_ISSUED',$4,$5,$6)",conn,tx))
        {audit.Parameters.AddWithValue(request.LicenseId);audit.Parameters.AddWithValue(request.DeviceId);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(request.Actor);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(otpExpiry);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);
        return Results.Json(new IssueResponse(product,request.LicenseId,request.DeviceId,otp,otpExpiry));
    }
    finally
    {CryptographicOperations.ZeroMemory(otpBytes);if(pepper.Length>0)CryptographicOperations.ZeroMemory(pepper);if(otpUtf8.Length>0)CryptographicOperations.ZeroMemory(otpUtf8);verifier="";otp="";}
    async Task Deny(string code)
    {await using var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code,admin_actor,request_id) VALUES('SUITE_OTP_DENIED',$1,$2,$3,'DENIED',$4,$5,$6)",conn,tx);audit.Parameters.AddWithValue(request.LicenseId);audit.Parameters.AddWithValue(request.DeviceId);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(code);audit.Parameters.AddWithValue(request.Actor);audit.Parameters.AddWithValue(request.RequestId);await audit.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);}
}

static string Required(string key)=>Environment.GetEnvironmentVariable(key)?.Trim() is {Length:>0} value?value:throw new InvalidOperationException("Required configuration is missing.");
static void ValidateId(string value){if(value.Length is <6 or >64||value.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))throw new BadHttpRequestException("invalid",400);}
static void Hex(string value){if(value.Length!=64||value.Any(c=>!(c is >= '0' and <= '9' or >= 'a' and <= 'f')))throw new BadHttpRequestException("invalid",400);}
static void ValidateText(string value,int max){if(value.Length is <1||value.Length>max||value.Any(char.IsControl))throw new BadHttpRequestException("invalid",400);}

sealed record CustomerActivityEvent(string Kind,long AtUnixSeconds,string Title,string Detail);
sealed record CustomerActivity(string Status,bool Verified,string EnrollmentState,string DeviceId,
    string DeviceStatus,string AgentVersion,long DeviceUpdatedAtUnixSeconds,bool Online,
    long UniqueDownloads,long DownloadAttempts,long LastSessionAtUnixSeconds,
    IReadOnlyList<CustomerActivityEvent> Events);
sealed record CustomerActivityClearRequest(string LicenseId,string Actor,string RequestId);
sealed record CustomerActivityClearResult(int DeletedRecords);
sealed class InternalToken
{
    private readonly byte[] bytes;
    private InternalToken(byte[] value)=>bytes=value;
    public static InternalToken Load(string path)
    {
        try{return LoadValidated(path);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {throw new InvalidOperationException("Internal authentication configuration is invalid.");}
    }
    private static InternalToken LoadValidated(string path)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        var mode=File.GetUnixFileMode(path);
        var forbidden=UnixFileMode.GroupWrite|UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute;
        if((mode&forbidden)!=0)throw new InvalidOperationException();
        using(var stat=Process.Start(new ProcessStartInfo("/usr/bin/stat",["-c","%U",path]){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false})??throw new InvalidOperationException())
        {var owner=stat.StandardOutput.ReadToEnd().Trim();stat.WaitForExit();if(stat.ExitCode!=0||owner!=Environment.UserName)throw new InvalidOperationException();}
        var text=File.ReadAllText(path).Trim();var decoded=Convert.FromBase64String(text);
        var canonical=Encoding.ASCII.GetBytes(Convert.ToBase64String(decoded));var supplied=Encoding.ASCII.GetBytes(text);
        try
        {if(decoded.Length<32||canonical.Length!=supplied.Length||!CryptographicOperations.FixedTimeEquals(canonical,supplied)){CryptographicOperations.ZeroMemory(decoded);throw new InvalidOperationException();}return new InternalToken(decoded);}
        finally{CryptographicOperations.ZeroMemory(canonical);CryptographicOperations.ZeroMemory(supplied);}
    }
    public bool Authenticates(string supplied)
    {
        if(string.IsNullOrEmpty(supplied))return false;byte[] candidate;
        try{candidate=Convert.FromBase64String(supplied);}catch(FormatException){return false;}
        try{return candidate.Length==bytes.Length&&CryptographicOperations.FixedTimeEquals(candidate,bytes);}
        finally{CryptographicOperations.ZeroMemory(candidate);}
    }
}
sealed record DenialRequest(string LicenseId,string DeviceId,string Actor,string RequestId,string DetailCode);
sealed record IssueRequest(string LicenseId,string DeviceId,int TtlSeconds,string Actor,string RequestId);
sealed record IssueResponse(string ProductId,string LicenseId,string DeviceId,string Otp,DateTime ExpiresAt);
sealed record Error(string Code);
sealed record SuiteAuditItem(string OccurredAt,string EventType,string Outcome,string DetailCode,string Actor,string RequestId,string? OtpExpiresAt);
sealed record SuiteStatus(string LicenseId,string ProductId,string Status,string LicenseTerm,DateTime? ExpiresAt,string IdentityPolicy,int MaximumActiveDevices,bool ActivationConsumed,bool OtpIssued,DateTime? OtpExpiresAt,string? DeviceId,string? BindingType,string? EnrollmentPolicy,string? Algorithm,string? HardwareFingerprint,long ActiveDevices,string? SessionId,string OtpState,bool CanIssue,IReadOnlyList<SuiteAuditItem> RecentEvents);
sealed record CommerceAdminIssueRequest(string LicenseId,string Actor,string RequestId);
public partial class Program;

static class AdminSelfTest
{
    public static int Run()
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        ContentManagementSelfTest.Run();
        var directory=Path.Combine(Path.GetTempPath(),"suite-admin-token-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ExpectFailure(Path.Combine(directory,"missing"),"missing");
            var path=Path.Combine(directory,"token");
            File.WriteAllText(path,"");File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.GroupRead);ExpectFailure(path,"empty");
            File.WriteAllText(path,Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));ExpectFailure(path,"short");
            File.WriteAllText(path,"not-base64!");ExpectFailure(path,"malformed");
            var valid=Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));File.WriteAllText(path,valid);
            var token=InternalToken.Load(path);
            if(!token.Authenticates(valid)||token.Authenticates("")||token.Authenticates(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))))throw new InvalidOperationException("token authentication test failed");
            File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.GroupRead|UnixFileMode.OtherRead);ExpectFailure(path,"permissions");
            using(var rsa=RSA.Create(2048))
            {
                var der=rsa.ExportSubjectPublicKeyInfo();var encoded=Convert.ToBase64String(der);var device=Convert.ToHexString(SHA256.HashData(der)).ToLowerInvariant();
                if(!SuiteEligibility.ValidIdentity(encoded,device,device)||!SuiteEligibility.ValidFingerprint(new string('b',64)))throw new InvalidOperationException("valid SPKI rejected");
                if(SuiteEligibility.ValidIdentity(encoded+"\n",device,device))throw new InvalidOperationException("noncanonical Base64 accepted");
                if(SuiteEligibility.ValidIdentity(Convert.ToBase64String(RandomNumberGenerator.GetBytes(300)),device,device))throw new InvalidOperationException("invalid DER accepted");
                if(SuiteEligibility.ValidIdentity(new string('A',300),device,device))throw new InvalidOperationException("300 A accepted");
                if(SuiteEligibility.ValidIdentity(encoded,new string('a',64),new string('a',64)))throw new InvalidOperationException("divergent SPKI hash accepted");
                if(SuiteEligibility.ValidFingerprint(new string('G',64)))throw new InvalidOperationException("invalid fingerprint accepted");
            }
            using(var weak=RSA.Create(1024)){var der=weak.ExportSubjectPublicKeyInfo();var encoded=Convert.ToBase64String(der);var device=Convert.ToHexString(SHA256.HashData(der)).ToLowerInvariant();if(SuiteEligibility.ValidIdentity(encoded,device,device))throw new InvalidOperationException("weak RSA accepted");}
            Console.WriteLine("SUITE ADMIN SELF-TEST: OK (missing, empty, short, malformed, permissions, absent header, divergent and valid token; SPKI canonical DER RSA 2048-4096, hash and fingerprint)");
            return 0;
        }
        finally{Directory.Delete(directory,true);}
    }
    static void ExpectFailure(string path,string label){try{_ = InternalToken.Load(path);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException){return;}throw new InvalidOperationException(label+" token was accepted");}
}
