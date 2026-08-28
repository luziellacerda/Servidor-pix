using Npgsql;
using System.Data;
using System.Security.Cryptography;

namespace TurboRamaSuiteOnlineServer;

public sealed record LicenseRecord(string LicenseId, string ProductId, string Status,
    string? ActivationVerifier, long? ActivationExpiresAt, bool ActivationConsumed,
    string LicenseTerm = "LIFETIME", long? ExpiresAt = null,
    string IdentityPolicy = "SOFTWARE_ONLY", int MaximumActiveDevices = 1,
    string ProvisioningOrigin = "LEGACY_ADMIN", string EnrollmentState = "PREBOUND_PENDING_ACTIVATION",
    string ClaimMode = "PREBOUND", long ActivationGeneration = 0, long RevocationGeneration = 0);
public sealed record DeviceRecord(string LicenseId, string DeviceId, string BindingType,
    string PublicKeySpki, string HardwareFingerprint, string Status,
    string Algorithm = Protocol.Algorithm);
public sealed record EnrollmentRecord(string LicenseId, string DeviceId, string BindingType,
    string IdentityPolicy, string Algorithm, string PublicKeySpki,
    string HardwareFingerprint);
public sealed record ChallengeRecord(string ChallengeId, string ProductId, string LicenseId,
    string DeviceId, string SessionId, string Action, string ContextHash, string Nonce,
    long ExpiresAt, string? ActivationVerifier, string? DeviceJson);
public sealed record SessionRecord(string LicenseId, string DeviceId, string SessionId,
    string Status, long AuthorizedUntil, long LastServerTime, long RevocationGeneration);
public sealed record CompletionRecord(string ChallengeId, string RequestDigest,
    SignedAssertionEnvelope Result);

public interface ISuiteStore
{
    Task<LicenseRecord?> FindLicenseAsync(string licenseId, CancellationToken token);
    Task<EnrollmentRecord?> FindEnrollmentAsync(string licenseId, CancellationToken token);
    Task<DeviceRecord?> FindDeviceAsync(string licenseId, string deviceId, CancellationToken token);
    Task InsertChallengeAsync(ChallengeRecord challenge, CancellationToken token);
    Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now,
        CancellationToken token);
    Task<CompletionRecord?> FindCompletionAsync(string challengeId, CancellationToken token);
    Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord challenge,
        string requestDigest, DeviceRecord device, SignedAssertionEnvelope result,
        CancellationToken token);
    Task<SessionRecord> CompleteSessionAsync(ChallengeRecord challenge, SessionRecord session, string action, long now,
        CancellationToken token);
}

public sealed class PostgresSuiteStore : ISuiteStore
{
    private readonly NpgsqlDataSource _dataSource;
    public PostgresSuiteStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<LicenseRecord?> FindLicenseAsync(string id, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT license_id,product_id,status,activation_verifier,extract(epoch from activation_expires_at)::bigint,activation_consumed,license_term,CASE WHEN expires_at IS NULL THEN NULL ELSE extract(epoch from expires_at)::bigint END,identity_policy,maximum_active_devices,provisioning_origin,enrollment_state,claim_mode,activation_generation,revocation_generation FROM suite.suite_licenses WHERE license_id=$1");
        cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetInt64(4), r.GetBoolean(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetString(8), r.GetInt16(9), r.GetString(10), r.GetString(11), r.GetString(12), r.GetInt64(13), r.GetInt64(14)) : null;
    }
    public async Task<DeviceRecord?> FindDeviceAsync(string l, string d, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm FROM suite.suite_devices WHERE license_id=$1 AND device_id=$2"); cmd.Parameters.AddWithValue(l); cmd.Parameters.AddWithValue(d); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6)) : null;
    }
    public async Task<EnrollmentRecord?> FindEnrollmentAsync(string licenseId, CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand("SELECT license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint FROM suite.suite_license_enrollments WHERE license_id=$1");
        cmd.Parameters.AddWithValue(licenseId); await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6)) : null;
    }
    public async Task InsertChallengeAsync(ChallengeRecord c, CancellationToken ct)
    { await using var cmd = _dataSource.CreateCommand("INSERT INTO suite.suite_challenges(challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,expires_at,activation_verifier,device_json,activation_generation,revocation_generation) SELECT $1,$2,$3,$4,$5,$6,$7,$8,to_timestamp($9),$10,$11::jsonb,CASE WHEN $6='device.activate' THEN activation_generation ELSE NULL END,revocation_generation FROM suite.suite_licenses WHERE license_id=$3"); cmd.Parameters.AddWithValue(c.ChallengeId); cmd.Parameters.AddWithValue(c.ProductId); cmd.Parameters.AddWithValue(c.LicenseId); cmd.Parameters.AddWithValue(c.DeviceId); cmd.Parameters.AddWithValue(c.SessionId); cmd.Parameters.AddWithValue(c.Action); cmd.Parameters.AddWithValue(c.ContextHash); cmd.Parameters.AddWithValue(c.Nonce); cmd.Parameters.AddWithValue(c.ExpiresAt); cmd.Parameters.AddWithValue((object?)c.ActivationVerifier ?? DBNull.Value); cmd.Parameters.AddWithValue((object?)c.DeviceJson ?? DBNull.Value); await cmd.ExecuteNonQueryAsync(ct); }
    public async Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken ct)
    { await using var cmd = _dataSource.CreateCommand("SELECT challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,extract(epoch from expires_at)::bigint,activation_verifier,device_json::text FROM suite.suite_challenges WHERE challenge_id=$1 AND action=$2 AND consumed_at IS NULL AND expires_at>to_timestamp($3)"); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(action); cmd.Parameters.AddWithValue(now); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadChallenge(r) : null; }
    public async Task<CompletionRecord?> FindCompletionAsync(string id, CancellationToken ct)
    { await using var cmd = _dataSource.CreateCommand("SELECT c.challenge_id,c.request_digest,c.result_json::text FROM suite.suite_activation_completions c JOIN suite.suite_challenges ch USING(challenge_id) JOIN suite.suite_licenses l ON l.license_id=ch.license_id JOIN suite.suite_devices d ON d.license_id=ch.license_id AND d.device_id=ch.device_id WHERE c.challenge_id=$1 AND c.invalidated_at IS NULL AND l.status='ACTIVE' AND l.enrollment_state='BOUND' AND c.revocation_generation=l.revocation_generation AND c.activation_generation=l.activation_generation AND d.status='ACTIVE'"); cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; return new(r.GetString(0), r.GetString(1), StrictJson.Parse<SignedAssertionEnvelope>(System.Text.Encoding.UTF8.GetBytes(r.GetString(2)))); }
    public async Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord c, string digest,
        DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await CompleteActivationOnceAsync(c, digest, d, result, ct); }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected && attempt < 3)
            { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt, ct); }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected)
            { throw new SuiteException(409, "TRANSACTION_CONFLICT", "The operation could not be completed safely.", ex); }
        }
        throw new SuiteException(409, "TRANSACTION_CONFLICT", "The operation could not be completed safely.");
    }

    private async Task<SignedAssertionEnvelope> CompleteActivationOnceAsync(ChallengeRecord c,
        string digest, DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using (var license = new NpgsqlCommand("""
            SELECT activation_verifier,activation_expires_at>clock_timestamp(),activation_consumed,
              status,license_term,expires_at,maximum_active_devices
            FROM suite.suite_licenses WHERE license_id=$1 AND product_id=$2 FOR UPDATE
            """, conn, tx))
        {
            license.Parameters.AddWithValue(c.LicenseId); license.Parameters.AddWithValue(Protocol.ProductId);
            await using var reader = await license.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available.");
            var verifier = reader.IsDBNull(0) ? null : reader.GetString(0);
            var verifierValid = !reader.IsDBNull(1) && reader.GetBoolean(1);
            var consumed = reader.GetBoolean(2); var status = reader.GetString(3);
            var term = reader.GetString(4); var lifetime = reader.IsDBNull(5);
            var maximum = reader.GetInt16(6); await reader.DisposeAsync();

            await using var prior = new NpgsqlCommand("SELECT c.request_digest,c.result_json::text FROM suite.suite_activation_completions c JOIN suite.suite_challenges ch USING(challenge_id) JOIN suite.suite_licenses l ON l.license_id=ch.license_id JOIN suite.suite_devices d ON d.license_id=ch.license_id AND d.device_id=ch.device_id WHERE c.challenge_id=$1 AND c.invalidated_at IS NULL AND l.status='ACTIVE' AND l.enrollment_state='BOUND' AND c.revocation_generation=l.revocation_generation AND c.activation_generation=l.activation_generation AND d.status='ACTIVE'", conn, tx);
            prior.Parameters.AddWithValue(c.ChallengeId);
            await using var priorReader = await prior.ExecuteReaderAsync(ct);
            if (await priorReader.ReadAsync(ct))
            {
                var priorDigest = priorReader.GetString(0);
                var priorResult = StrictJson.Parse<SignedAssertionEnvelope>(System.Text.Encoding.UTF8.GetBytes(priorReader.GetString(1)));
                await priorReader.DisposeAsync();
                if (!Protocol.FixedEquals(priorDigest, digest)) throw new SuiteException(409, "REPLAY_DENIED", "Replay was denied.");
                await tx.CommitAsync(ct); return priorResult;
            }
            await priorReader.DisposeAsync();
            if (consumed || status != "ACTIVE" || term != "LIFETIME" || !lifetime || maximum != 1
                || !verifierValid || c.ActivationVerifier is null || !Protocol.FixedEquals(verifier ?? "", c.ActivationVerifier))
                throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available.");
        }

        await using (var enrollment = new NpgsqlCommand("""
            INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint)
            VALUES($1,$2,$3,'SOFTWARE_ONLY',$4,$5,$6) ON CONFLICT(license_id) DO NOTHING
            """, conn, tx))
        {
            enrollment.Parameters.AddWithValue(d.LicenseId); enrollment.Parameters.AddWithValue(d.DeviceId);
            enrollment.Parameters.AddWithValue(d.BindingType); enrollment.Parameters.AddWithValue(d.Algorithm);
            enrollment.Parameters.AddWithValue(d.PublicKeySpki); enrollment.Parameters.AddWithValue(d.HardwareFingerprint);
            await enrollment.ExecuteNonQueryAsync(ct);
        }
        await using (var enrollmentCheck = new NpgsqlCommand("SELECT device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint FROM suite.suite_license_enrollments WHERE license_id=$1 FOR UPDATE", conn, tx))
        {
            enrollmentCheck.Parameters.AddWithValue(d.LicenseId); await using var er = await enrollmentCheck.ExecuteReaderAsync(ct);
            if (!await er.ReadAsync(ct) || er.GetString(0) != d.DeviceId || er.GetString(1) != d.BindingType || er.GetString(2) != "SOFTWARE_ONLY" || er.GetString(3) != d.Algorithm || er.GetString(4) != d.PublicKeySpki || er.GetString(5) != d.HardwareFingerprint)
                throw new SuiteException(409, "DEVICE_LIMIT_REACHED", "The license already has a different enrollment.");
        }
        await using (var device = new NpgsqlCommand("""
            INSERT INTO suite.suite_devices(license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm)
            VALUES($1,$2,$3,$4,$5,'ACTIVE',$6)
            ON CONFLICT(license_id,device_id) DO UPDATE SET hardware_fingerprint=excluded.hardware_fingerprint,
              status='ACTIVE',algorithm=excluded.algorithm
            """, conn, tx))
        {
            device.Parameters.AddWithValue(d.LicenseId);device.Parameters.AddWithValue(d.DeviceId);
            device.Parameters.AddWithValue(d.BindingType);device.Parameters.AddWithValue(d.PublicKeySpki);
            device.Parameters.AddWithValue(d.HardwareFingerprint);device.Parameters.AddWithValue(d.Algorithm);
            try { await device.ExecuteNonQueryAsync(ct); }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                && ex.ConstraintName == "ux_suite_devices_one_active_per_license")
            { throw new SuiteException(409, "DEVICE_LIMIT_REACHED", "The license already has an active device.", ex); }
        }
        await Consume(c, conn, tx, ct);
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_licenses SET activation_consumed=true,activation_verifier=NULL,
              activation_expires_at=NULL,enrollment_state='BOUND',updated_at=clock_timestamp()
            WHERE license_id=$1 AND product_id=$2 AND status='ACTIVE' AND license_term='LIFETIME'
              AND expires_at IS NULL AND maximum_active_devices=1 AND activation_consumed=false
              AND activation_verifier=$3 AND activation_expires_at>clock_timestamp()
            """, conn, tx))
        {
            update.Parameters.AddWithValue(c.LicenseId);update.Parameters.AddWithValue(Protocol.ProductId);
            update.Parameters.AddWithValue(c.ActivationVerifier!);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available.");
        }
        await using (var completion = new NpgsqlCommand("INSERT INTO suite.suite_activation_completions(challenge_id,request_digest,result_json,revocation_generation,activation_generation) SELECT $1,$2,$3::jsonb,revocation_generation,activation_generation FROM suite.suite_licenses WHERE license_id=$4", conn, tx))
        {
            completion.Parameters.AddWithValue(c.ChallengeId);completion.Parameters.AddWithValue(digest);
            completion.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(result, StrictJson.Options));
            completion.Parameters.AddWithValue(c.LicenseId);
            await completion.ExecuteNonQueryAsync(ct);
        }
        await using (var transfer = new NpgsqlCommand("UPDATE suite.suite_transfer_history SET status='COMPLETED',completed_at=clock_timestamp() WHERE license_id=$1 AND status='PENDING' AND activation_generation=(SELECT activation_generation FROM suite.suite_licenses WHERE license_id=$1)", conn, tx))
        { transfer.Parameters.AddWithValue(c.LicenseId); if(await transfer.ExecuteNonQueryAsync(ct)>0){await using var audit=new NpgsqlCommand("INSERT INTO suite.suite_audit_events(event_type,license_id,device_id,correlation_id,outcome,detail_code) VALUES('SUITE_DEVICE_TRANSFER_COMPLETED',$1,$2,$3,'SUCCESS','NEW_DEVICE_BOUND')",conn,tx);audit.Parameters.AddWithValue(c.LicenseId);audit.Parameters.AddWithValue(c.DeviceId);audit.Parameters.AddWithValue(c.ChallengeId);await audit.ExecuteNonQueryAsync(ct);} }
        await tx.CommitAsync(ct); return result;
    }
    public async Task<SessionRecord> CompleteSessionAsync(ChallengeRecord c,SessionRecord s,string action,long now,CancellationToken ct)
    {
      for(var attempt=1;attempt<=3;attempt++){try{return await Once();}catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected && attempt<3){await Task.Delay(RandomNumberGenerator.GetInt32(15,75)*attempt,ct);}catch(PostgresException ex)when(ex.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected){throw new SuiteException(409,"TRANSACTION_CONFLICT","The operation could not be completed safely.",ex);}}throw new SuiteException(409,"TRANSACTION_CONFLICT","The operation could not be completed safely.");
      async Task<SessionRecord> Once(){await using var conn=await _dataSource.OpenConnectionAsync(ct);await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);long generation;string origin;
       await using(var license=new NpgsqlCommand("SELECT status,revocation_generation,provisioning_origin FROM suite.suite_licenses WHERE license_id=$1 AND product_id=$2 FOR UPDATE",conn,tx)){license.Parameters.AddWithValue(s.LicenseId);license.Parameters.AddWithValue(Protocol.ProductId);await using var r=await license.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||r.GetString(0)!="ACTIVE")throw new SuiteException(403,"LICENSE_DENIED","License is not active.");generation=r.GetInt64(1);origin=r.GetString(2);}
       if(origin=="COMMERCE"){await using var delivery=new NpgsqlCommand("SELECT provisioning_state,financial_state,last_source_version,administrative_resume_source_version FROM suite.suite_license_deliveries WHERE license_id=$1 FOR UPDATE",conn,tx);delivery.Parameters.AddWithValue(s.LicenseId);await using var r=await delivery.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct)||r.GetString(0)!="PROVISIONED"||!(r.GetString(1)=="PAID"||(r.GetString(1)=="SUSPENDED"&&!r.IsDBNull(3)&&r.GetInt64(3)==r.GetInt64(2))))throw new SuiteException(403,"LICENSE_DENIED","License is not active.");}
       await using(var enrollment=new NpgsqlCommand("SELECT device_id FROM suite.suite_license_enrollments WHERE license_id=$1 FOR UPDATE",conn,tx)){enrollment.Parameters.AddWithValue(s.LicenseId);if((string?)(await enrollment.ExecuteScalarAsync(ct))!=s.DeviceId)throw new SuiteException(403,"DEVICE_DENIED","Device is not authorized.");}
       await using(var device=new NpgsqlCommand("SELECT status FROM suite.suite_devices WHERE license_id=$1 AND device_id=$2 FOR UPDATE",conn,tx)){device.Parameters.AddWithValue(s.LicenseId);device.Parameters.AddWithValue(s.DeviceId);if((string?)(await device.ExecuteScalarAsync(ct))!="ACTIVE")throw new SuiteException(403,"DEVICE_DENIED","Device is not authorized.");}
       var sql=action=="session.open"?"INSERT INTO suite.suite_sessions(license_id,device_id,session_id,status,authorized_until,last_server_time,revocation_generation) VALUES($1,$2,$3,'ACTIVE',to_timestamp($4),$5,$6) ON CONFLICT(license_id,device_id) DO UPDATE SET session_id=$3,status='ACTIVE',authorized_until=to_timestamp($4),last_server_time=GREATEST(suite.suite_sessions.last_server_time+1,$5),revocation_generation=$6,revoked_at=NULL,revocation_reason=NULL RETURNING license_id,device_id,session_id,status,extract(epoch from authorized_until)::bigint,last_server_time,revocation_generation":"UPDATE suite.suite_sessions SET authorized_until=to_timestamp($4),last_server_time=GREATEST(last_server_time+1,$5) WHERE license_id=$1 AND device_id=$2 AND session_id=$3 AND status='ACTIVE' AND revocation_generation=$6 RETURNING license_id,device_id,session_id,status,extract(epoch from authorized_until)::bigint,last_server_time,revocation_generation";
       await using var cmd=new NpgsqlCommand(sql,conn,tx);cmd.Parameters.AddWithValue(s.LicenseId);cmd.Parameters.AddWithValue(s.DeviceId);cmd.Parameters.AddWithValue(s.SessionId);cmd.Parameters.AddWithValue(s.AuthorizedUntil);cmd.Parameters.AddWithValue(now);cmd.Parameters.AddWithValue(generation);await using var sr=await cmd.ExecuteReaderAsync(ct);if(!await sr.ReadAsync(ct))throw new SuiteException(409,"SESSION_INVALID","Session is not current.");var result=new SessionRecord(sr.GetString(0),sr.GetString(1),sr.GetString(2),sr.GetString(3),sr.GetInt64(4),sr.GetInt64(5),sr.GetInt64(6));await sr.DisposeAsync();await Consume(c,conn,tx,ct);await tx.CommitAsync(ct);return result;}
    }
    private static async Task Consume(ChallengeRecord c, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct) { await using var cmd = new NpgsqlCommand("UPDATE suite.suite_challenges c SET consumed_at=clock_timestamp() FROM suite.suite_licenses l WHERE c.challenge_id=$1 AND c.action=$2 AND c.consumed_at IS NULL AND c.invalidated_at IS NULL AND c.expires_at>clock_timestamp() AND l.license_id=c.license_id AND l.revocation_generation=c.revocation_generation AND (c.action<>'device.activate' OR l.activation_generation=c.activation_generation)", connection, transaction); cmd.Parameters.AddWithValue(c.ChallengeId); cmd.Parameters.AddWithValue(c.Action); if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired."); }
    private static ChallengeRecord ReadChallenge(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetInt64(8), r.IsDBNull(9) ? null : r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10));
}

public static class ActivationCodes
{
    public static string Verify(string pepper, string code)
    {
        var key = Convert.FromBase64String(pepper); try { return Convert.ToHexString(HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant(); } finally { CryptographicOperations.ZeroMemory(key); }
    }
}
