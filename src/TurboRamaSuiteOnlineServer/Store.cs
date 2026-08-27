using Npgsql;
using System.Data;
using System.Security.Cryptography;

namespace TurboRamaSuiteOnlineServer;

public sealed record LicenseRecord(string LicenseId, string ProductId, string Status,
    string? ActivationVerifier, long? ActivationExpiresAt, bool ActivationConsumed,
    string LicenseTerm = "LIFETIME", long? ExpiresAt = null,
    string IdentityPolicy = "SOFTWARE_ONLY", int MaximumActiveDevices = 1);
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
        await using var cmd = _dataSource.CreateCommand("SELECT license_id,product_id,status,activation_verifier,extract(epoch from activation_expires_at)::bigint,activation_consumed,license_term,CASE WHEN expires_at IS NULL THEN NULL ELSE extract(epoch from expires_at)::bigint END,identity_policy,maximum_active_devices FROM suite.suite_licenses WHERE license_id=$1");
        cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.IsDBNull(4) ? null : r.GetInt64(4), r.GetBoolean(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetString(8), r.GetInt16(9)) : null;
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
    { await using var cmd = _dataSource.CreateCommand("INSERT INTO suite.suite_challenges(challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,expires_at,activation_verifier,device_json) VALUES($1,$2,$3,$4,$5,$6,$7,$8,to_timestamp($9),$10,$11::jsonb)"); cmd.Parameters.AddWithValue(c.ChallengeId); cmd.Parameters.AddWithValue(c.ProductId); cmd.Parameters.AddWithValue(c.LicenseId); cmd.Parameters.AddWithValue(c.DeviceId); cmd.Parameters.AddWithValue(c.SessionId); cmd.Parameters.AddWithValue(c.Action); cmd.Parameters.AddWithValue(c.ContextHash); cmd.Parameters.AddWithValue(c.Nonce); cmd.Parameters.AddWithValue(c.ExpiresAt); cmd.Parameters.AddWithValue((object?)c.ActivationVerifier ?? DBNull.Value); cmd.Parameters.AddWithValue((object?)c.DeviceJson ?? DBNull.Value); await cmd.ExecuteNonQueryAsync(ct); }
    public async Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken ct)
    { await using var cmd = _dataSource.CreateCommand("SELECT challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,extract(epoch from expires_at)::bigint,activation_verifier,device_json::text FROM suite.suite_challenges WHERE challenge_id=$1 AND action=$2 AND consumed_at IS NULL AND expires_at>to_timestamp($3)"); cmd.Parameters.AddWithValue(id); cmd.Parameters.AddWithValue(action); cmd.Parameters.AddWithValue(now); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadChallenge(r) : null; }
    public async Task<CompletionRecord?> FindCompletionAsync(string id, CancellationToken ct)
    { await using var cmd = _dataSource.CreateCommand("SELECT challenge_id,request_digest,result_json::text FROM suite.suite_activation_completions WHERE challenge_id=$1"); cmd.Parameters.AddWithValue(id); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; return new(r.GetString(0), r.GetString(1), StrictJson.Parse<SignedAssertionEnvelope>(System.Text.Encoding.UTF8.GetBytes(r.GetString(2)))); }
    public async Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord c, string digest, DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct)
    { await using var conn = await _dataSource.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Consume(c, conn, tx, ct); await using (var device = new NpgsqlCommand("INSERT INTO suite.suite_devices(license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm) VALUES($1,$2,$3,$4,$5,'ACTIVE',$6) ON CONFLICT(license_id,device_id) DO UPDATE SET hardware_fingerprint=excluded.hardware_fingerprint,status='ACTIVE',algorithm=excluded.algorithm", conn, tx)) { device.Parameters.AddWithValue(d.LicenseId); device.Parameters.AddWithValue(d.DeviceId); device.Parameters.AddWithValue(d.BindingType); device.Parameters.AddWithValue(d.PublicKeySpki); device.Parameters.AddWithValue(d.HardwareFingerprint); device.Parameters.AddWithValue(d.Algorithm); try { await device.ExecuteNonQueryAsync(ct); } catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "ux_suite_devices_one_active_per_license") { throw new SuiteException(409, "DEVICE_LIMIT_REACHED", "The license already has an active device.", ex); } } await using (var license = new NpgsqlCommand("UPDATE suite.suite_licenses SET activation_consumed=true,activation_verifier=NULL,activation_expires_at=NULL,updated_at=clock_timestamp() WHERE license_id=$1 AND product_id=$2 AND status='ACTIVE' AND license_term='LIFETIME' AND expires_at IS NULL AND maximum_active_devices=1 AND activation_consumed=false AND activation_verifier=$3 AND activation_expires_at>clock_timestamp()", conn, tx)) { license.Parameters.AddWithValue(c.LicenseId); license.Parameters.AddWithValue(Protocol.ProductId); license.Parameters.AddWithValue((object?)c.ActivationVerifier ?? DBNull.Value); if (await license.ExecuteNonQueryAsync(ct) != 1) throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available."); } await using (var completion = new NpgsqlCommand("INSERT INTO suite.suite_activation_completions(challenge_id,request_digest,result_json) VALUES($1,$2,$3::jsonb)", conn, tx)) { completion.Parameters.AddWithValue(c.ChallengeId); completion.Parameters.AddWithValue(digest); completion.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(result, StrictJson.Options)); await completion.ExecuteNonQueryAsync(ct); } await tx.CommitAsync(ct); return result; }
    public async Task<SessionRecord> CompleteSessionAsync(ChallengeRecord c, SessionRecord s, string action, long now, CancellationToken ct)
    { await using var conn = await _dataSource.OpenConnectionAsync(ct); await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct); await Consume(c, conn, tx, ct); await using var cmd = new NpgsqlCommand("INSERT INTO suite.suite_sessions(license_id,device_id,session_id,status,authorized_until,last_server_time,revocation_generation) VALUES($1,$2,$3,'ACTIVE',to_timestamp($4),$5,$6) ON CONFLICT(license_id,device_id) DO UPDATE SET session_id=CASE WHEN $7='session.open' OR suite.suite_sessions.session_id=$3 THEN $3 ELSE suite.suite_sessions.session_id END,status='ACTIVE',authorized_until=to_timestamp($4),last_server_time=GREATEST(suite.suite_sessions.last_server_time+1,$5) WHERE $7='session.open' OR (suite.suite_sessions.session_id=$3 AND suite.suite_sessions.status='ACTIVE') RETURNING license_id,device_id,session_id,status,extract(epoch from authorized_until)::bigint,last_server_time,revocation_generation", conn, tx); cmd.Parameters.AddWithValue(s.LicenseId); cmd.Parameters.AddWithValue(s.DeviceId); cmd.Parameters.AddWithValue(s.SessionId); cmd.Parameters.AddWithValue(s.AuthorizedUntil); cmd.Parameters.AddWithValue(now); cmd.Parameters.AddWithValue(s.RevocationGeneration); cmd.Parameters.AddWithValue(action); await using var r = await cmd.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new SuiteException(409, "SESSION_INVALID", "Session is not current."); var result = new SessionRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6)); await r.DisposeAsync(); await tx.CommitAsync(ct); return result; }
    private static async Task Consume(ChallengeRecord c, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct) { await using var cmd = new NpgsqlCommand("UPDATE suite.suite_challenges SET consumed_at=clock_timestamp() WHERE challenge_id=$1 AND action=$2 AND consumed_at IS NULL AND expires_at>clock_timestamp()", connection, transaction); cmd.Parameters.AddWithValue(c.ChallengeId); cmd.Parameters.AddWithValue(c.Action); if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired."); }
    private static ChallengeRecord ReadChallenge(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetInt64(8), r.IsDBNull(9) ? null : r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10));
}

public static class ActivationCodes
{
    public static string Verify(string pepper, string code)
    {
        var key = Convert.FromBase64String(pepper); try { return Convert.ToHexString(HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant(); } finally { CryptographicOperations.ZeroMemory(key); }
    }
}
