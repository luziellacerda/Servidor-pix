using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Npgsql;

static class CommerceEndpoints
{
    const string Product="TURBORAMA_SUITE",Sku="SUITE_LIFETIME_1_DEVICE";
    public static void Map(WebApplication app,bool enabled,string pepperFile)
    {
        app.MapPost("/commerce/events",async(HttpContext context,NpgsqlDataSource db,CancellationToken ct)=>
        {
            if(!enabled)return Results.NotFound();
            CommerceEvent request;
            try{request=await context.Request.ReadFromJsonAsync<CommerceEvent>(cancellationToken:ct)??throw new BadHttpRequestException("invalid",400);}
            catch(System.Text.Json.JsonException){return Results.BadRequest(new CommerceError("REQUEST_INVALID"));}
            try{return Results.Json(await Apply(request,db,ct));}
            catch(CommerceConflict ex){return Results.Conflict(new CommerceError(ex.Code));}
            catch(CommerceInvalid ex){return Results.BadRequest(new CommerceError(ex.Code));}
        });
        app.MapGet("/commerce/deliveries/{purchase}/{item}",async(string purchase,string item,NpgsqlDataSource db,CancellationToken ct)=>
        {
            if(!enabled)return Results.NotFound();ValidateText(purchase,64);ValidateText(item,32);
            await using var cmd=db.CreateCommand("""
              SELECT source_purchase_id,source_item_key,source_product_sku,coalesce(license_id,''),
                provisioning_state,financial_state,last_source_version
              FROM suite.suite_license_deliveries
              WHERE source_system='TURBOBOX_V1' AND source_purchase_id=$1 AND source_item_key=$2 AND product_id='TURBORAMA_SUITE'
              """);
            cmd.Parameters.AddWithValue(purchase);cmd.Parameters.AddWithValue(item);await using var r=await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct)?Results.Json(new CommerceResult(r.GetString(0),r.GetString(1),r.GetString(2),Empty(r.GetString(3)),r.GetString(4),r.GetString(5),r.GetInt64(6),"CURRENT")):Results.NotFound();
        });
        app.MapPost("/commerce/deliveries/{purchase}/{item}/issue",async(string purchase,string item,CommerceIssueRequest request,NpgsqlDataSource db,CancellationToken ct)=>{if(!enabled)return Results.NotFound();ValidateText(purchase,64);ValidateText(item,32);ValidateText(request.Actor,64);ValidateText(request.RequestId,128);try{return Results.Json(await Issue(purchase,item,request,db,pepperFile,ct));}catch(CommerceConflict ex){return Results.Conflict(new CommerceError(ex.Code));}});
    }

    static async Task<CommerceResult> Apply(CommerceEvent e,NpgsqlDataSource db,CancellationToken ct)
    {
        Validate(e);var calculated=Digest(Canonical(e));if(!Fixed(calculated,e.PayloadDigest))throw new CommerceConflict("PAYLOAD_DIGEST_MISMATCH");
        for(var attempt=1;attempt<=3;attempt++)
        {
            try{return await ApplyOnce(e,db,ct);}
            catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected&&attempt<3)
            {await Task.Delay(RandomNumberGenerator.GetInt32(15,75)*attempt,ct);}
            catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {throw new CommerceConflict("TRANSACTION_RETRY_EXHAUSTED");}
        }
        throw new CommerceConflict("TRANSACTION_RETRY_EXHAUSTED");
    }

    static async Task<CommerceResult> ApplyOnce(CommerceEvent e,NpgsqlDataSource db,CancellationToken ct)
    {
        await using var conn=await db.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var prior=await Prior(e,conn,tx,ct);if(prior is not null){await tx.CommitAsync(ct);return prior;}

        string? knownLicense=null;
        await using(var discover=new NpgsqlCommand("""
          SELECT license_id FROM suite.suite_license_deliveries
          WHERE source_system=$1 AND source_purchase_id=$2 AND source_item_key=$3 AND product_id=$4
          """,conn,tx))
        {BindAggregate(discover,e);var scalar=await discover.ExecuteScalarAsync(ct);knownLicense=scalar is string value?value:null;}
        if(!string.IsNullOrEmpty(knownLicense))
        {
            await using var licenseLock=new NpgsqlCommand("SELECT license_id FROM suite.suite_licenses WHERE license_id=$1 FOR UPDATE",conn,tx);
            licenseLock.Parameters.AddWithValue(knownLicense);_ = await licenseLock.ExecuteScalarAsync(ct);
        }
        Delivery? delivery;
        await using(var lockDelivery=new NpgsqlCommand("""
          SELECT coalesce(license_id,''),provisioning_state,financial_state,last_source_version
          FROM suite.suite_license_deliveries
          WHERE source_system=$1 AND source_purchase_id=$2 AND source_item_key=$3 AND product_id=$4 FOR UPDATE
          """,conn,tx))
        {BindAggregate(lockDelivery,e);await using var r=await lockDelivery.ExecuteReaderAsync(ct);delivery=await r.ReadAsync(ct)?new(Empty(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetInt64(3)):null;}

        CommerceResult result;
        if(delivery is not null&&e.SourceVersion<=delivery.Version)
            result=Result(e,delivery,"STALE");
        else if(e.EventType=="PURCHASE_SUSPENDED")
            result=await Suspend(e,delivery,conn,tx,ct);
        else if(delivery is null&&e.SourceVersion==1)
            result=await Provision(e,conn,tx,ct);
        else if(delivery is not null&&delivery.Financial=="PAID"&&e.SourceVersion==delivery.Version+1)
            result=Result(e,delivery,"NOOP_PAID");
        else
            result=new(e.SourcePurchaseId,e.SourceItemKey,e.SourceProductSku,delivery?.LicenseId,delivery?.State??"GAP_PENDING",delivery?.Financial??"UNKNOWN",delivery?.Version??0,"GAP_PENDING");

        await using(var inbox=new NpgsqlCommand("""
          INSERT INTO suite.suite_commerce_inbox(source_system,source_event_id,source_purchase_id,source_item_key,
            source_version,source_product_sku,event_type,payload_digest,processed_at,outcome,detail_code,result_json)
          VALUES($1,$2,$3,$4,$5,$6,$7,$8,clock_timestamp(),$9,$9,$10::jsonb)
          """,conn,tx))
        {
            BindEvent(inbox,e);inbox.Parameters.AddWithValue(result.Outcome);
            inbox.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(result));
            try{await inbox.ExecuteNonQueryAsync(ct);}
            catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){throw new CommerceConflict("EVENT_CONFLICT");}
        }
        await tx.CommitAsync(ct);return result;
    }

    static async Task<CommerceResult?> Prior(CommerceEvent e,NpgsqlConnection conn,NpgsqlTransaction tx,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
          SELECT source_system,source_event_id,source_purchase_id,source_item_key,source_version,source_product_sku,
            event_type,payload_digest,result_json::text
          FROM suite.suite_commerce_inbox
          WHERE (source_system=$1 AND source_event_id=$2)
             OR (source_system=$1 AND source_purchase_id=$3 AND source_item_key=$4 AND source_product_sku=$6 AND source_version=$5)
          FOR UPDATE
          """,conn,tx);BindEvent(cmd,e);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;
        var identical=r.GetString(0)==e.SourceSystem&&r.GetString(1)==e.SourceEventId&&r.GetString(2)==e.SourcePurchaseId&&r.GetString(3)==e.SourceItemKey&&r.GetInt64(4)==e.SourceVersion&&r.GetString(5)==e.SourceProductSku&&r.GetString(6)==e.EventType&&Fixed(r.GetString(7),e.PayloadDigest);
        if(!identical)throw new CommerceConflict("EVENT_CONFLICT");
        return System.Text.Json.JsonSerializer.Deserialize<CommerceResult>(r.GetString(8))??throw new CommerceConflict("RESULT_INVALID");
    }

    static async Task<CommerceResult> Provision(CommerceEvent e,NpgsqlConnection conn,NpgsqlTransaction tx,CancellationToken ct)
    {
        string licenseId="";
        for(var attempt=0;attempt<5;attempt++)
        {
            licenseId="TS-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            await using var insert=new NpgsqlCommand("""
              INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,activation_expires_at,
                activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices,provisioning_origin,
                enrollment_state,claim_mode,activation_generation,applied_journal_sequence)
              VALUES($1,'TURBORAMA_SUITE','ACTIVE',NULL,NULL,false,'LIFETIME',NULL,'SOFTWARE_ONLY',1,
                'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM',0,0)
              ON CONFLICT(license_id) DO NOTHING
              """,conn,tx);insert.Parameters.AddWithValue(licenseId);if(await insert.ExecuteNonQueryAsync(ct)==1)break;if(attempt==4)throw new CommerceConflict("LICENSE_ID_COLLISION");
        }
        await using(var delivery=new NpgsqlCommand("""
          INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,source_product_sku,
            product_id,license_id,provisioning_state,financial_state,last_source_version)
          VALUES($1,$2,$3,$5,$4,$6,'PROVISIONED','PAID',$7)
          """,conn,tx))
        {BindAggregate(delivery,e);delivery.Parameters.AddWithValue(e.SourceProductSku);delivery.Parameters.AddWithValue(licenseId);delivery.Parameters.AddWithValue(e.SourceVersion);await delivery.ExecuteNonQueryAsync(ct);}
        await Audit(conn,tx,"SUITE_COMMERCE_PROVISIONED",licenseId,"SUCCESS","PURCHASE_PAID",e.SourceEventId,ct);
        return new(e.SourcePurchaseId,e.SourceItemKey,e.SourceProductSku,licenseId,"PROVISIONED","PAID",e.SourceVersion,"PROVISIONED");
    }

    static async Task<CommerceResult> Suspend(CommerceEvent e,Delivery? d,NpgsqlConnection conn,NpgsqlTransaction tx,CancellationToken ct)
    {
        if(d is null)
        {
            await using var tombstone=new NpgsqlCommand("""
              INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,source_product_sku,
                product_id,license_id,provisioning_state,financial_state,last_source_version)
              VALUES($1,$2,$3,$5,$4,NULL,'TOMBSTONE','SUSPENDED',$6)
              """,conn,tx);BindAggregate(tombstone,e);tombstone.Parameters.AddWithValue(e.SourceProductSku);tombstone.Parameters.AddWithValue(e.SourceVersion);await tombstone.ExecuteNonQueryAsync(ct);
            return new(e.SourcePurchaseId,e.SourceItemKey,e.SourceProductSku,null,"TOMBSTONE","SUSPENDED",e.SourceVersion,"TOMBSTONE_APPLIED");
        }
        if(d.LicenseId is not null)
        {
            await using(var license=new NpgsqlCommand("""
              UPDATE suite.suite_licenses SET status=CASE WHEN status='REVOKED' THEN status ELSE 'SUSPENDED' END,
                revocation_generation=revocation_generation+1,activation_generation=activation_generation+1,
                activation_verifier=NULL,activation_expires_at=NULL,updated_at=clock_timestamp()
              WHERE license_id=$1
              """,conn,tx)){license.Parameters.AddWithValue(d.LicenseId);await license.ExecuteNonQueryAsync(ct);}
            await using(var challenges=new NpgsqlCommand("UPDATE suite.suite_challenges SET invalidated_at=clock_timestamp(),invalidation_reason='FINANCIAL_SUSPEND',consumed_at=coalesce(consumed_at,clock_timestamp()) WHERE license_id=$1 AND invalidated_at IS NULL",conn,tx)){challenges.Parameters.AddWithValue(d.LicenseId);await challenges.ExecuteNonQueryAsync(ct);}
            await using(var sessions=new NpgsqlCommand("UPDATE suite.suite_sessions SET status='REVOKED',revoked_at=clock_timestamp(),revocation_reason='FINANCIAL_SUSPEND',authorized_until=least(authorized_until,clock_timestamp()) WHERE license_id=$1 AND status='ACTIVE'",conn,tx)){sessions.Parameters.AddWithValue(d.LicenseId);await sessions.ExecuteNonQueryAsync(ct);}
            await using(var devices=new NpgsqlCommand("UPDATE suite.suite_devices SET status='SUSPENDED',updated_at=clock_timestamp() WHERE license_id=$1 AND status='ACTIVE'",conn,tx)){devices.Parameters.AddWithValue(d.LicenseId);await devices.ExecuteNonQueryAsync(ct);}
        }
        await using(var update=new NpgsqlCommand("""
          UPDATE suite.suite_license_deliveries SET provisioning_state='SUSPENDED',financial_state='SUSPENDED',
            last_source_version=$5,administrative_resume_source_version=NULL,administrative_resume_actor=NULL,
            administrative_resume_reason=NULL,administrative_resume_request_id=NULL,updated_at=clock_timestamp()
          WHERE source_system=$1 AND source_purchase_id=$2 AND source_item_key=$3 AND product_id=$4
          """,conn,tx)){BindAggregate(update,e);update.Parameters.AddWithValue(e.SourceVersion);await update.ExecuteNonQueryAsync(ct);}
        if(d.LicenseId is not null)await Audit(conn,tx,"SUITE_COMMERCE_SUSPENDED",d.LicenseId,"SUCCESS","PURCHASE_SUSPENDED",e.SourceEventId,ct);
        return new(e.SourcePurchaseId,e.SourceItemKey,e.SourceProductSku,d.LicenseId,"SUSPENDED","SUSPENDED",e.SourceVersion,"SUSPENDED");
    }

    static async Task<CommerceOtpResult> Issue(string purchase,string item,CommerceIssueRequest request,NpgsqlDataSource db,string pepperFile,CancellationToken ct)
    {
        string licenseId;await using(var find=db.CreateCommand("SELECT license_id FROM suite.suite_license_deliveries WHERE source_system='TURBOBOX_V1' AND source_purchase_id=$1 AND source_item_key=$2 AND product_id='TURBORAMA_SUITE'")){find.Parameters.AddWithValue(purchase);find.Parameters.AddWithValue(item);licenseId=(string?)(await find.ExecuteScalarAsync(ct))??throw new CommerceConflict("DELIVERY_NOT_READY");}
        await using var conn=await db.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using(var license=new NpgsqlCommand("SELECT status,enrollment_state,activation_consumed,activation_verifier IS NOT NULL AND activation_expires_at>clock_timestamp(),activation_generation FROM suite.suite_licenses WHERE license_id=$1 AND product_id='TURBORAMA_SUITE' FOR UPDATE",conn,tx)){license.Parameters.AddWithValue(licenseId);await using var r=await license.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||r.GetString(0)!="ACTIVE"||r.GetString(1)!="PENDING_ENROLLMENT"||r.GetBoolean(2)||r.GetBoolean(3))throw new CommerceConflict("OTP_NOT_ELIGIBLE");}
        await using(var delivery=new NpgsqlCommand("SELECT provisioning_state,financial_state,administrative_resume_source_version FROM suite.suite_license_deliveries WHERE license_id=$1 FOR UPDATE",conn,tx)){delivery.Parameters.AddWithValue(licenseId);await using var r=await delivery.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||r.GetString(0)!="PROVISIONED"||r.GetString(1)!="PAID")throw new CommerceConflict("DELIVERY_NOT_ELIGIBLE");}
        await using(var count=new NpgsqlCommand("SELECT count(*) FROM suite.suite_devices WHERE license_id=$1 AND status='ACTIVE'",conn,tx)){count.Parameters.AddWithValue(licenseId);if((long)(await count.ExecuteScalarAsync(ct)??0L)!=0)throw new CommerceConflict("DEVICE_LIMIT_REACHED");}
        var otpBytes=RandomNumberGenerator.GetBytes(32);byte[] pepper=[];byte[] otpUtf8=[];var otp=Convert.ToBase64String(otpBytes).TrimEnd('=').Replace('+','-').Replace('/','_');try{pepper=Convert.FromBase64String((await File.ReadAllTextAsync(pepperFile,ct)).Trim());if(pepper.Length<32)throw new InvalidOperationException("PEPPER_INVALID");otpUtf8=Encoding.UTF8.GetBytes(otp);var verifier=Convert.ToHexString(HMACSHA256.HashData(pepper,otpUtf8)).ToLowerInvariant();
        await using(var invalidate=new NpgsqlCommand("UPDATE suite.suite_challenges SET invalidated_at=clock_timestamp(),invalidation_reason='NEW_OTP',consumed_at=coalesce(consumed_at,clock_timestamp()) WHERE license_id=$1 AND action='device.activate' AND invalidated_at IS NULL",conn,tx)){invalidate.Parameters.AddWithValue(licenseId);await invalidate.ExecuteNonQueryAsync(ct);}
        DateTime expires;await using(var update=new NpgsqlCommand("UPDATE suite.suite_licenses SET activation_verifier=$2,activation_expires_at=clock_timestamp()+interval '15 minutes',activation_consumed=false,activation_generation=activation_generation+1,updated_at=clock_timestamp() WHERE license_id=$1 RETURNING activation_expires_at",conn,tx)){update.Parameters.AddWithValue(licenseId);update.Parameters.AddWithValue(verifier);expires=(DateTime)(await update.ExecuteScalarAsync(ct)??throw new CommerceConflict("OTP_NOT_ELIGIBLE"));}
        await using(var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,outcome,detail_code,admin_actor,request_id,otp_expires_at) VALUES('SUITE_OTP_ISSUED',$1,$2,'SUCCESS','FIRST_CLAIM_OTP_ISSUED',$3,$2,$4)",conn,tx)){audit.Parameters.AddWithValue(licenseId);audit.Parameters.AddWithValue(request.RequestId);audit.Parameters.AddWithValue(request.Actor);audit.Parameters.AddWithValue(expires);await audit.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);return new(licenseId,otp,expires);}
        finally{CryptographicOperations.ZeroMemory(otpBytes);if(pepper.Length>0)CryptographicOperations.ZeroMemory(pepper);if(otpUtf8.Length>0)CryptographicOperations.ZeroMemory(otpUtf8);}
    }
    static async Task Audit(NpgsqlConnection conn,NpgsqlTransaction tx,string type,string license,string outcome,string detail,string correlation,CancellationToken ct)
    {await using var cmd=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,outcome,detail_code) VALUES($1,$2,$3,$4,$5)",conn,tx);cmd.Parameters.AddWithValue(type);cmd.Parameters.AddWithValue(license);cmd.Parameters.AddWithValue(correlation);cmd.Parameters.AddWithValue(outcome);cmd.Parameters.AddWithValue(detail);await cmd.ExecuteNonQueryAsync(ct);}
    static CommerceResult Result(CommerceEvent e,Delivery d,string outcome)=>new(e.SourcePurchaseId,e.SourceItemKey,e.SourceProductSku,d.LicenseId,d.State,d.Financial,d.Version,outcome);
    static void BindAggregate(NpgsqlCommand c,CommerceEvent e){c.Parameters.AddWithValue(e.SourceSystem);c.Parameters.AddWithValue(e.SourcePurchaseId);c.Parameters.AddWithValue(e.SourceItemKey);c.Parameters.AddWithValue(Product);}
    static void BindEvent(NpgsqlCommand c,CommerceEvent e){c.Parameters.AddWithValue(e.SourceSystem);c.Parameters.AddWithValue(e.SourceEventId);c.Parameters.AddWithValue(e.SourcePurchaseId);c.Parameters.AddWithValue(e.SourceItemKey);c.Parameters.AddWithValue(e.SourceVersion);c.Parameters.AddWithValue(e.SourceProductSku);c.Parameters.AddWithValue(e.EventType);c.Parameters.AddWithValue(e.PayloadDigest);}
    static byte[] Canonical(CommerceEvent e)=>Encoding.UTF8.GetBytes(string.Join('\n',[e.SourceSystem,e.SourceEventId,e.SourcePurchaseId,e.SourceItemKey,e.SourceVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),e.SourceProductSku,e.EventType]));
    static string Digest(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static bool Fixed(string a,string b){var x=Encoding.ASCII.GetBytes(a);var y=Encoding.ASCII.GetBytes(b);try{return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}}
    static string? Empty(string value)=>value.Length==0?null:value;
    static void Validate(CommerceEvent e){if(e.SourceSystem!="TURBOBOX_V1"||e.SourceProductSku!=Sku||e.EventType is not("PURCHASE_PAID" or "PURCHASE_SUSPENDED")||e.SourceVersion<1)throw new CommerceInvalid("EVENT_INVALID");Hex(e.SourceEventId,32);Hex(e.PayloadDigest,64);ValidateText(e.SourcePurchaseId,64);ValidateText(e.SourceItemKey,32);}
    static void Hex(string value,int length){if(value.Length!=length||value.Any(c=>!(c is>='0'and<='9'or>='a'and<='f')))throw new CommerceInvalid("EVENT_INVALID");}
    static void ValidateText(string value,int max){if(value.Length<1||value.Length>max||value.Any(c=>char.IsControl(c)||c is '\\' or '/' or ':' or '|'))throw new CommerceInvalid("EVENT_INVALID");}
    sealed record Delivery(string? LicenseId,string State,string Financial,long Version);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
sealed record CommerceEvent(string SourceSystem,string SourceEventId,string SourcePurchaseId,string SourceItemKey,long SourceVersion,string SourceProductSku,string EventType,string PayloadDigest);
sealed record CommerceResult(string SourcePurchaseId,string SourceItemKey,string SourceProductSku,string? LicenseId,string ProvisioningState,string FinancialState,long SourceVersion,string Outcome);
sealed record CommerceIssueRequest(string Actor,string RequestId);
sealed record CommerceOtpResult(string LicenseId,string Otp,DateTime ExpiresAt);
sealed record CommerceError(string Code);
sealed class CommerceConflict : Exception { public CommerceConflict(string code) : base(code) { Code = code; } public string Code { get; } }
sealed class CommerceInvalid : Exception { public CommerceInvalid(string code) : base(code) { Code = code; } public string Code { get; } }
