using System.Text.Json;
using Npgsql;

internal static class StationManagementEndpoints
{
    private const string ReadClaim = "station.licenses.read";
    private const string From = """
        FROM suite.suite_licenses l
        LEFT JOIN LATERAL (SELECT * FROM suite.suite_license_deliveries x
          WHERE x.license_id=l.license_id AND x.product_id='TURBORAMA_STATION_ANDROID'
          ORDER BY x.last_source_version DESC LIMIT 1) d ON true
        LEFT JOIN suite.station_customer_projection p ON p.license_id=l.license_id
        LEFT JOIN LATERAL (SELECT * FROM suite.station_devices x
          WHERE x.license_id=l.license_id AND x.status='ACTIVE'
          ORDER BY x.updated_at DESC LIMIT 1) v ON true
        LEFT JOIN LATERAL (SELECT * FROM suite.station_sessions x
          WHERE x.license_id=l.license_id AND x.status='ACTIVE'
          ORDER BY x.created_at DESC LIMIT 1) s ON true
        """ + "\n";
    private const string Fields = """
        json_build_object('licenseId',l.license_id,'status',l.status,
          'enrollmentState',l.enrollment_state,'activationConsumed',l.activation_consumed,
          'activationGeneration',l.activation_generation,'revocationGeneration',l.revocation_generation,
          'codeValid',NOT l.activation_consumed AND l.activation_verifier IS NOT NULL
            AND l.activation_expires_at>clock_timestamp(),
          'codeIssued',l.activation_verifier IS NOT NULL,'activationExpiresAt',l.activation_expires_at,
          'displayName',coalesce(p.display_name,''),'customerRef',coalesce(p.customer_ref,''),
          'financialState',coalesce(d.financial_state,'MISSING'),
          'provisioningState',coalesce(d.provisioning_state,'MISSING'),
          'sourcePurchaseId',coalesce(d.source_purchase_id,''),'sourceItemKey',coalesce(d.source_item_key,''),
          'eligibleDelivery',d.financial_state='PAID' AND d.provisioning_state='PROVISIONED'
            AND d.source_product_sku='STATION_ANDROID_LIFETIME_1_DEVICE'
            AND l.license_term='LIFETIME' AND l.expires_at IS NULL AND l.maximum_active_devices=1,
          'manufacturer',coalesce(v.manufacturer,''),'model',coalesce(v.model,''),
          'deviceClientVersion',coalesce(v.client_version,''),'deviceLinked',v.device_id IS NOT NULL,
          'deviceUpdatedAt',v.updated_at,'sessionId',coalesce(s.session_id,''),
          'sessionAuthorizedUntil',s.authorized_until,'lastContactAt',s.last_contact_at,
          'sessionLive',s.authorized_until>clock_timestamp(),
          'isTest',coalesce(p.customer_ref,'')='teste-station')::text
        """ + "\n";

    public static void Map(WebApplication app, bool enabled)
    {
        StationCodeManagement.Map(app,enabled);
        app.MapGet("/station/licenses", async (HttpContext context,
            NpgsqlDataSource database, CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            if (!ContentAdminSecurity.Authorize(context,ReadClaim,false,false).Allowed)
                return Failure(403,"STATION_PERMISSION_DENIED");
            var query = context.Request.Query["q"].ToString().Trim();
            var state = context.Request.Query["state"].ToString();
            if (state.Length==0) state="all";
            if (query.Length>128 || query.Any(char.IsControl) ||
                state is not ("all" or "active" or "pending" or "suspended" or "attention") ||
                !Page(context,"offset",0,1000000,out var offset) ||
                !Page(context,"limit",20,100,out var limit) || limit<1)
                return Failure(400,"STATION_REQUEST_INVALID");
            try
            {
                var filter = """
                    WHERE l.product_id='TURBORAMA_STATION_ANDROID'
                      AND (l.license_id ILIKE $1 OR coalesce(p.display_name,'') ILIKE $1
                        OR coalesce(p.customer_ref,'') ILIKE $1 OR coalesce(d.source_purchase_id,'') ILIKE $1)
                    """ + (state switch {
                        "active" => " AND l.status='ACTIVE' AND l.enrollment_state='BOUND'",
                        "pending" => " AND l.status='ACTIVE' AND l.enrollment_state='PENDING_ENROLLMENT'",
                        "suspended" => " AND l.status='SUSPENDED'",
                        "attention" => " AND (coalesce(d.financial_state,'')<>'PAID' OR coalesce(d.provisioning_state,'')<>'PROVISIONED' OR l.status NOT IN ('ACTIVE','SUSPENDED'))",
                        _ => "" });
                var search="%"+query.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%";
                long total;
                await using (var count=database.CreateCommand("SELECT count(*) "+From+filter))
                { count.Parameters.AddWithValue(search);total=(long)(await count.ExecuteScalarAsync(token)??0L); }
                var rows=new List<JsonElement>();
                await using (var list=database.CreateCommand("SELECT "+Fields+From+filter+
                    " ORDER BY l.updated_at DESC,l.license_id LIMIT $2 OFFSET $3"))
                {
                    list.Parameters.AddWithValue(search);list.Parameters.AddWithValue(limit);list.Parameters.AddWithValue(offset);
                    await using var reader=await list.ExecuteReaderAsync(token);
                    while(await reader.ReadAsync(token)) rows.Add(Parse(reader.GetString(0)));
                }
                JsonElement summary;
                await using (var counts=database.CreateCommand("""
                    SELECT json_build_object('total',count(*),
                      'active',count(*) FILTER(WHERE l.status='ACTIVE' AND l.enrollment_state='BOUND'),
                      'pending',count(*) FILTER(WHERE l.status='ACTIVE' AND l.enrollment_state='PENDING_ENROLLMENT'),
                      'suspended',count(*) FILTER(WHERE l.status='SUSPENDED'),
                      'attention',count(*) FILTER(WHERE coalesce(d.financial_state,'')<>'PAID'
                        OR coalesce(d.provisioning_state,'')<>'PROVISIONED' OR l.status NOT IN ('ACTIVE','SUSPENDED')))::text
                    """+"\n"+From+" WHERE l.product_id='TURBORAMA_STATION_ANDROID'"))
                    summary=Parse((string)(await counts.ExecuteScalarAsync(token)??"{}"));
                return Results.Json(new {licenses=rows,total,offset,limit,summary,managementAvailable=true});
            }
            catch(Exception) { return Failure(503,"STATION_ADMIN_UNAVAILABLE"); }
        });
        app.MapGet("/station/licenses/{licenseId}/support", async (string licenseId,
            HttpContext context,NpgsqlDataSource database,CancellationToken token) =>
        {
            if (!enabled) return Results.NotFound();
            if (!ContentAdminSecurity.Authorize(context,ReadClaim,false,false).Allowed)
                return Failure(403,"STATION_PERMISSION_DENIED");
            if (!Id(licenseId)) return Failure(400,"STATION_LICENSE_INVALID");
            try
            {
                JsonElement license;
                await using (var row=database.CreateCommand("SELECT "+Fields+From+
                    " WHERE l.license_id=$1 AND l.product_id='TURBORAMA_STATION_ANDROID'"))
                {
                    row.Parameters.AddWithValue(licenseId);
                    var raw=await row.ExecuteScalarAsync(token) as string;
                    if(raw is null)return Failure(404,"STATION_NOT_FOUND");
                    license=Parse(raw);
                }
                var history=new List<JsonElement>();
                await using(var events=database.CreateCommand("""
                    SELECT json_build_object('event',a.event_type,'outcome',a.outcome,
                      'detail',a.detail_code,'actor',coalesce(a.admin_actor,''),
                      'createdAt',a.occurred_at,'reason',coalesce(nullif(a.reason,''),c.reason,''))::text
                    FROM suite.station_management_audit a
                    LEFT JOIN suite.suite_lifecycle_commands c ON c.license_id=a.license_id
                      AND c.request_id=a.request_id AND c.scope='STATION_ANDROID'
                    WHERE a.license_id=$1 ORDER BY a.occurred_at DESC,a.event_id DESC LIMIT 50
                    """))
                {
                    events.Parameters.AddWithValue(licenseId);
                    await using var reader=await events.ExecuteReaderAsync(token);
                    while(await reader.ReadAsync(token))history.Add(Parse(reader.GetString(0)));
                }
                var devices=new List<JsonElement>();
                await using(var rows=database.CreateCommand("""
                    SELECT json_build_object('status',status,'manufacturer',coalesce(manufacturer,''),
                      'model',coalesce(model,''),'clientVersion',coalesce(client_version,''),
                      'createdAt',created_at,'updatedAt',updated_at)::text
                    FROM suite.station_devices WHERE license_id=$1 ORDER BY updated_at DESC LIMIT 50
                    """))
                {
                    rows.Parameters.AddWithValue(licenseId);await using var reader=await rows.ExecuteReaderAsync(token);
                    while(await reader.ReadAsync(token))devices.Add(Parse(reader.GetString(0)));
                }
                return Results.Json(new {license,history,devices});
            }
            catch(Exception) { return Failure(503,"STATION_ADMIN_UNAVAILABLE"); }
        });
    }
    private static bool Page(HttpContext c,string key,int fallback,int max,out int n)
    { var raw=c.Request.Query[key].ToString();if(raw.Length==0){n=fallback;return true;}return int.TryParse(raw,out n)&&n>=0&&n<=max; }
    private static bool Id(string s)=>s.Length is >=6 and <=64 && s.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static JsonElement Parse(string raw){using var d=JsonDocument.Parse(raw);return d.RootElement.Clone();}
    private static IResult Failure(int status,string code)=>Results.Json(new {code},statusCode:status);
}
