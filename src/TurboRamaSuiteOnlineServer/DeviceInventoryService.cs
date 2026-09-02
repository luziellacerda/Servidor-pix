using System.Data;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace TurboRamaSuiteOnlineServer;

public sealed class InventorySensitiveProtector
{
    private readonly byte[] _key;
    public InventorySensitiveProtector(string base64Key)
    {
        try { _key = Convert.FromBase64String(base64Key); }
        catch (FormatException ex) { throw new InvalidOperationException("Inventory encryption key is invalid.", ex); }
        if (_key.Length != 32) throw new InvalidOperationException("Inventory encryption key must contain 32 bytes.");
    }
    public byte[] Protect(string value)
    {
        var nonce=RandomNumberGenerator.GetBytes(12); var plain=Encoding.UTF8.GetBytes(value); var cipher=new byte[plain.Length]; var tag=new byte[16];
        try { using var aes=new AesGcm(_key,16); aes.Encrypt(nonce,plain,cipher,tag); return [1,..nonce,..tag,..cipher]; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public string Unprotect(byte[] value)
    {
        if(value.Length<29||value[0]!=1) throw new SecurityException("Protected inventory value is invalid.");
        var plain=new byte[value.Length-29];
        try { using var aes=new AesGcm(_key,16); aes.Decrypt(value.AsSpan(1,12),value.AsSpan(13,16),value.AsSpan(29),plain); return Encoding.UTF8.GetString(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static string Mask(string value) => value.Length==0 ? "Não informado" : value.Length<=4 ? new string('•',value.Length) : $"••••{value[^4..]}";
}

public sealed class DeviceInventoryService
{
    private readonly NpgsqlDataSource _db; private readonly IAssertionSigner _signer; private readonly TimeProvider _clock; private readonly InventorySensitiveProtector _protector;
    public DeviceInventoryService(NpgsqlDataSource db,IAssertionSigner signer,TimeProvider clock,InventorySensitiveProtector protector)=>(_db,_signer,_clock,_protector)=(db,signer,clock,protector);

    public async Task<SignedAssertionEnvelope> ChallengeAsync(SuiteDeviceInventoryChallengeRequestV1 request,string correlationId,CancellationToken ct)
    {
        ValidateContract(()=>SuiteDeviceInventoryProtocol.ValidateChallengeRequest(request)); var now=_clock.GetUtcNow().ToUnixTimeSeconds();
        await RequireAuthorityAsync(request.LicenseId,request.DeviceId,request.SessionId,now,ct);
        var id=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); var nonce=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); var expires=now+60;
        await using var cmd=_db.CreateCommand("INSERT INTO suite.suite_device_inventory_challenges(challenge_id,schema_version,product_id,license_id,device_id,session_id,action,inventory_hash,nonce,issued_at,expires_at,correlation_id) VALUES($1,1,$2,$3,$4,$5,$6,$7,$8,to_timestamp($9),to_timestamp($10),$11)");
        object[] p=[id,request.ProductId,request.LicenseId,request.DeviceId,request.SessionId,request.Action,request.InventoryHash,nonce,now,expires,correlationId]; foreach(var v in p)cmd.Parameters.AddWithValue(v); await cmd.ExecuteNonQueryAsync(ct);
        return _signer.Sign(new SuiteDeviceInventoryChallengeAssertionV1(1,SuiteDeviceInventoryProtocol.ChallengeAssertionKind,request.ProductId,request.LicenseId,request.DeviceId,request.SessionId,request.Action,request.InventoryHash,id,nonce,SuiteDeviceInventoryProtocol.ChallengeStatus,now,expires));
    }

    public async Task<SignedAssertionEnvelope> AcceptAsync(SuiteDeviceInventoryProofV1 proof,string correlationId,CancellationToken ct)
    {
        ValidateContract(()=>SuiteDeviceInventoryProtocol.ValidateProof(proof)); var now=_clock.GetUtcNow().ToUnixTimeSeconds();
        await using var conn=await _db.OpenConnectionAsync(ct); await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        string nonce; long expires; string spki;
        await using(var cmd=new NpgsqlCommand("SELECT c.nonce,extract(epoch from c.expires_at)::bigint,d.public_key_spki FROM suite.suite_device_inventory_challenges c JOIN suite.suite_licenses l ON l.license_id=c.license_id JOIN suite.suite_license_enrollments e ON e.license_id=c.license_id AND e.device_id=c.device_id JOIN suite.suite_devices d ON d.license_id=c.license_id AND d.device_id=c.device_id JOIN suite.suite_sessions s ON s.license_id=c.license_id AND s.device_id=c.device_id AND s.session_id=c.session_id WHERE c.challenge_id=$1 AND c.license_id=$2 AND c.device_id=$3 AND c.session_id=$4 AND c.action=$5 AND c.inventory_hash=$6 AND c.consumed_at IS NULL AND c.expires_at>clock_timestamp() AND l.status='ACTIVE' AND l.enrollment_state='BOUND' AND d.status='ACTIVE' AND s.status='ACTIVE' AND s.authorized_until>clock_timestamp() FOR UPDATE OF c",conn,tx))
        { cmd.Parameters.AddWithValue(proof.ChallengeId);cmd.Parameters.AddWithValue(proof.LicenseId);cmd.Parameters.AddWithValue(proof.DeviceId);cmd.Parameters.AddWithValue(proof.SessionId);cmd.Parameters.AddWithValue(proof.Action);cmd.Parameters.AddWithValue(proof.InventoryHash); await using var r=await cmd.ExecuteReaderAsync(ct); if(!await r.ReadAsync(ct))throw new SuiteException(409,"CHALLENGE_INVALID","Challenge is invalid or expired."); nonce=r.GetString(0);expires=r.GetInt64(1);spki=r.GetString(2); }
        byte[] key; try{key=Convert.FromBase64String(spki);}catch(FormatException ex){throw new SuiteException(403,"DEVICE_DENIED","Device is not authorized.",ex);}
        try { if(!SuiteDeviceInventoryProtocol.VerifyProof(key,new ChallengeResponse(1,proof.ChallengeId,nonce,expires),proof))throw new SuiteException(403,"PROOF_INVALID","Inventory proof is invalid."); }
        catch(SecurityException ex){throw new SuiteException(403,"PROOF_INVALID","Inventory proof is invalid.",ex);} finally{CryptographicOperations.ZeroMemory(key);}

        var prior=await ReadPriorAsync(conn,tx,proof.LicenseId,proof.DeviceId,ct); var comparison=Compare(prior,proof.Inventory);
        var serialCipher=_protector.Protect(proof.Inventory.BaseboardSerial); var uuidCipher=_protector.Protect(proof.Inventory.SystemUuid);
        long eventId=await InsertEventAsync(conn,tx,proof,comparison,serialCipher,uuidCipher,correlationId,ct);
        if(comparison.Status=="PROBABLE_BOARD_CHANGE" && prior is not null)
        {
            await using var review=new NpgsqlCommand("INSERT INTO suite.suite_machine_change_reviews(license_id,device_id,baseline_event_id,candidate_event_id,reason_codes,confidence,request_id) VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING",conn,tx);
            review.Parameters.AddWithValue(proof.LicenseId);review.Parameters.AddWithValue(proof.DeviceId);review.Parameters.AddWithValue(prior.EventId);review.Parameters.AddWithValue(eventId);review.Parameters.AddWithValue(comparison.Reasons);review.Parameters.AddWithValue(comparison.Confidence);review.Parameters.AddWithValue(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes($"{proof.LicenseId}:{proof.DeviceId}:{proof.InventoryHash}"))).ToLowerInvariant());await review.ExecuteNonQueryAsync(ct);
        }
        else if(comparison.Status!="INCONCLUSIVE") await UpsertCurrentAsync(conn,tx,proof,comparison,serialCipher,uuidCipher,ct);
        await using(var consume=new NpgsqlCommand("UPDATE suite.suite_device_inventory_challenges SET consumed_at=clock_timestamp(),technical_result='ACCEPTED' WHERE challenge_id=$1 AND consumed_at IS NULL AND expires_at>clock_timestamp()",conn,tx)){consume.Parameters.AddWithValue(proof.ChallengeId);if(await consume.ExecuteNonQueryAsync(ct)!=1)throw new SuiteException(409,"CHALLENGE_INVALID","Challenge is invalid or expired.");}
        await using(var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code) VALUES('SUITE_DEVICE_INVENTORY_ACCEPTED',$1,$2,$3,'SUCCESS',$4)",conn,tx)){audit.Parameters.AddWithValue(proof.LicenseId);audit.Parameters.AddWithValue(proof.DeviceId);audit.Parameters.AddWithValue(correlationId);audit.Parameters.AddWithValue(comparison.Status);await audit.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);
        return _signer.Sign(new SuiteDeviceInventoryResultAssertionV1(1,SuiteDeviceInventoryProtocol.ResultAssertionKind,proof.ProductId,proof.LicenseId,proof.DeviceId,proof.SessionId,proof.Action,proof.InventoryHash,proof.ChallengeId,SuiteDeviceInventoryProtocol.ResultStatus,now));
    }

    private async Task RequireAuthorityAsync(string license,string device,string session,long now,CancellationToken ct){await using var cmd=_db.CreateCommand("SELECT EXISTS(SELECT 1 FROM suite.suite_licenses l JOIN suite.suite_license_enrollments e ON e.license_id=l.license_id JOIN suite.suite_devices d ON d.license_id=e.license_id AND d.device_id=e.device_id JOIN suite.suite_sessions s ON s.license_id=d.license_id AND s.device_id=d.device_id WHERE l.license_id=$1 AND e.device_id=$2 AND s.session_id=$3 AND l.status='ACTIVE' AND l.enrollment_state='BOUND' AND d.status='ACTIVE' AND s.status='ACTIVE' AND s.authorized_until>to_timestamp($4))");cmd.Parameters.AddWithValue(license);cmd.Parameters.AddWithValue(device);cmd.Parameters.AddWithValue(session);cmd.Parameters.AddWithValue(now);if(!((bool?)await cmd.ExecuteScalarAsync(ct)??false))throw new SuiteException(403,"SESSION_DENIED","Session is not active.");}
    private sealed record Prior(long EventId,string Fingerprint,string Serial,string Uuid,string BoardMaker,string BoardProduct,string SystemMaker,string SystemModel,string Bios,string Os,string Arch,string Client);
    private async Task<Prior?> ReadPriorAsync(NpgsqlConnection c,NpgsqlTransaction t,string l,string d,CancellationToken ct){await using var q=new NpgsqlCommand("SELECT e.event_id,i.motherboard_fingerprint,i.baseboard_serial_cipher,i.system_uuid_cipher,i.baseboard_manufacturer,i.baseboard_product,i.system_manufacturer,i.system_model,i.bios_version,i.os_version,i.architecture,i.client_version FROM suite.suite_device_inventory i JOIN LATERAL(SELECT event_id FROM suite.suite_device_inventory_events WHERE license_id=i.license_id AND device_id=i.device_id ORDER BY received_at,event_id LIMIT 1)e ON true WHERE i.license_id=$1 AND i.device_id=$2 FOR UPDATE OF i",c,t);q.Parameters.AddWithValue(l);q.Parameters.AddWithValue(d);await using var r=await q.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new(r.GetInt64(0),r.GetString(1),_protector.Unprotect((byte[])r[2]),_protector.Unprotect((byte[])r[3]),r.GetString(4),r.GetString(5),r.GetString(6),r.GetString(7),r.GetString(8),r.GetString(9),r.GetString(10),r.GetString(11)):null;}
    private sealed record Comparison(string Status,string Confidence,string[] Reasons);
    private static Comparison Compare(Prior? p,SuiteMotherboardInventoryV1 n){if(p is null)return new("MATCH","NONE",["BASELINE_CREATED"]);if(Protocol.FixedEquals(p.Fingerprint,n.MotherboardFingerprint)){var enrich=(p.Serial.Length==0&&n.BaseboardSerial.Length>0)||(p.Uuid.Length==0&&n.SystemUuid.Length>0);var context=p.Bios!=n.BiosVersion||p.Os!=n.OsVersion||p.Arch!=n.Architecture||p.Client!=n.ClientVersion;return enrich?new("ENRICHED","NONE",["IDENTITY_ENRICHED"]):context?new("NON_IDENTITY_CHANGE","LOW",["CONTEXT_CHANGED"]):new("MATCH","NONE",["FINGERPRINT_MATCH"]);}var reasons=new List<string>();if(p.Serial.Length>0&&n.BaseboardSerial.Length>0&&p.Serial!=n.BaseboardSerial)reasons.Add("SERIAL_CHANGED");if(p.Uuid.Length>0&&n.SystemUuid.Length>0&&p.Uuid!=n.SystemUuid)reasons.Add("UUID_CHANGED");var medium=p.BoardMaker!=n.BaseboardManufacturer&&p.BoardProduct!=n.BaseboardProduct||p.SystemMaker!=n.SystemManufacturer&&p.SystemModel!=n.SystemModel;if(medium)reasons.Add("BOARD_IDENTITY_PAIR_CHANGED");return reasons.Count>0?new("PROBABLE_BOARD_CHANGE",reasons.Any(x=>x is "SERIAL_CHANGED" or "UUID_CHANGED")?"HIGH":"MEDIUM",reasons.ToArray()):new("INCONCLUSIVE","LOW",["FINGERPRINT_CHANGED_WITHOUT_STRONG_EVIDENCE"]);}
    private static void ValidateContract(Action action){try{action();}catch(SecurityException ex){throw new SuiteException(400,"CONTRACT_INVALID","Request contract is invalid.",ex);}}
    private async Task<long> InsertEventAsync(NpgsqlConnection c,NpgsqlTransaction t,SuiteDeviceInventoryProofV1 p,Comparison x,byte[] sc,byte[] uc,string cid,CancellationToken ct){var i=p.Inventory;await using var q=new NpgsqlCommand("INSERT INTO suite.suite_device_inventory_events(license_id,device_id,session_id,inventory_hash,motherboard_fingerprint,schema_version,baseboard_manufacturer,baseboard_product,baseboard_version,baseboard_serial_cipher,baseboard_serial_masked,system_manufacturer,system_model,system_uuid_cipher,system_uuid_masked,bios_manufacturer,bios_version,os_name,os_version,architecture,client_version,source,collected_at_unix_seconds,comparison_status,comparison_confidence,correlation_id) VALUES($1,$2,$3,$4,$5,1,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25) ON CONFLICT(license_id,device_id,inventory_hash) DO UPDATE SET inventory_hash=excluded.inventory_hash RETURNING event_id",c,t);object[] v=[p.LicenseId,p.DeviceId,p.SessionId,p.InventoryHash,i.MotherboardFingerprint,i.BaseboardManufacturer,i.BaseboardProduct,i.BaseboardVersion,sc,InventorySensitiveProtector.Mask(i.BaseboardSerial),i.SystemManufacturer,i.SystemModel,uc,InventorySensitiveProtector.Mask(i.SystemUuid),i.BiosManufacturer,i.BiosVersion,i.OsName,i.OsVersion,i.Architecture,i.ClientVersion,i.Source,i.CollectedAtUnixSeconds,x.Status,x.Confidence,cid];foreach(var a in v)q.Parameters.AddWithValue(a);return (long)(await q.ExecuteScalarAsync(ct)??throw new InvalidOperationException());}
    private async Task UpsertCurrentAsync(NpgsqlConnection c,NpgsqlTransaction t,SuiteDeviceInventoryProofV1 p,Comparison x,byte[] sc,byte[] uc,CancellationToken ct){var i=p.Inventory;await using var q=new NpgsqlCommand("INSERT INTO suite.suite_device_inventory(license_id,device_id,schema_version,inventory_hash,motherboard_fingerprint,baseboard_manufacturer,baseboard_product,baseboard_version,baseboard_serial_cipher,baseboard_serial_masked,system_manufacturer,system_model,system_uuid_cipher,system_uuid_masked,bios_manufacturer,bios_version,os_name,os_version,architecture,client_version,source,collected_at_unix_seconds,comparison_status,comparison_confidence) VALUES($1,$2,1,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23) ON CONFLICT(license_id,device_id) DO UPDATE SET inventory_hash=excluded.inventory_hash,motherboard_fingerprint=excluded.motherboard_fingerprint,baseboard_manufacturer=excluded.baseboard_manufacturer,baseboard_product=excluded.baseboard_product,baseboard_version=excluded.baseboard_version,baseboard_serial_cipher=CASE WHEN excluded.baseboard_serial_masked='Não informado' THEN suite.suite_device_inventory.baseboard_serial_cipher ELSE excluded.baseboard_serial_cipher END,baseboard_serial_masked=CASE WHEN excluded.baseboard_serial_masked='Não informado' THEN suite.suite_device_inventory.baseboard_serial_masked ELSE excluded.baseboard_serial_masked END,system_manufacturer=excluded.system_manufacturer,system_model=excluded.system_model,system_uuid_cipher=CASE WHEN excluded.system_uuid_masked='Não informado' THEN suite.suite_device_inventory.system_uuid_cipher ELSE excluded.system_uuid_cipher END,system_uuid_masked=CASE WHEN excluded.system_uuid_masked='Não informado' THEN suite.suite_device_inventory.system_uuid_masked ELSE excluded.system_uuid_masked END,bios_manufacturer=excluded.bios_manufacturer,bios_version=excluded.bios_version,os_name=excluded.os_name,os_version=excluded.os_version,architecture=excluded.architecture,client_version=excluded.client_version,source=excluded.source,collected_at_unix_seconds=excluded.collected_at_unix_seconds,comparison_status=excluded.comparison_status,comparison_confidence=excluded.comparison_confidence,updated_at=clock_timestamp()",c,t);object[] v=[p.LicenseId,p.DeviceId,p.InventoryHash,i.MotherboardFingerprint,i.BaseboardManufacturer,i.BaseboardProduct,i.BaseboardVersion,sc,InventorySensitiveProtector.Mask(i.BaseboardSerial),i.SystemManufacturer,i.SystemModel,uc,InventorySensitiveProtector.Mask(i.SystemUuid),i.BiosManufacturer,i.BiosVersion,i.OsName,i.OsVersion,i.Architecture,i.ClientVersion,i.Source,i.CollectedAtUnixSeconds,x.Status,x.Confidence];foreach(var a in v)q.Parameters.AddWithValue(a);await q.ExecuteNonQueryAsync(ct);}
}
