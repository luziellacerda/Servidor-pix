using System.Data;
using System.Security.Cryptography;
using Npgsql;

namespace TurboRamaSuiteOnlineServer;

/// <summary>Only the two suite_es_* tables are written by this adapter.</summary>
public class PostgresEmulationStationStore : IEmulationStationStore
{
    private readonly NpgsqlDataSource _data;
    private readonly PostgresSuiteStore _identities;
    private readonly string _clientContract;

    public PostgresEmulationStationStore(NpgsqlDataSource data) : this(data, false) { }

    protected PostgresEmulationStationStore(NpgsqlDataSource data, bool sharedContract)
    {
        _data = data;
        _identities = new PostgresSuiteStore(data);
        // Reopening policy does not select the cryptographic challenge namespace.
        _clientContract = sharedContract ? "SHARED_V1" : "DEDICATED_V1";
    }

    public Task<LicenseRecord?> FindLicenseAsync(string licenseId, CancellationToken token) =>
        _identities.FindLicenseAsync(licenseId, token);
    public Task<EnrollmentRecord?> FindEnrollmentAsync(string licenseId, CancellationToken token) =>
        _identities.FindEnrollmentAsync(licenseId, token);
    public Task<DeviceRecord?> FindDeviceAsync(string licenseId, string deviceId,
        CancellationToken token) => _identities.FindDeviceAsync(licenseId, deviceId, token);

    public async Task<bool> IsActiveSessionAsync(string licenseId, string deviceId,
        string sessionId, long now, CancellationToken token)
    {
        await using var command = _data.CreateCommand("""
            SELECT EXISTS(SELECT 1 FROM suite.suite_es_sessions s
              JOIN suite.suite_licenses l ON l.license_id=s.license_id
              JOIN suite.suite_devices d ON d.license_id=s.license_id AND d.device_id=s.device_id
              JOIN suite.suite_license_enrollments e ON e.license_id=s.license_id AND e.device_id=s.device_id
              WHERE s.license_id=$1 AND s.device_id=$2 AND s.session_id=$3
                AND s.status='ACTIVE' AND s.authorized_until>to_timestamp($4)
                AND s.revocation_generation=l.revocation_generation
                AND l.status='ACTIVE' AND d.status='ACTIVE' AND l.enrollment_state='BOUND')
            """);
        command.Parameters.AddWithValue(licenseId);
        command.Parameters.AddWithValue(deviceId);
        command.Parameters.AddWithValue(sessionId);
        command.Parameters.AddWithValue(now);
        return (bool)(await command.ExecuteScalarAsync(token) ?? false);
    }

    public async Task InsertChallengeAsync(ChallengeRecord challenge, CancellationToken token)
    {
        EmulationStationService.RequireSessionAction(challenge.Action);
        await using var connection = await _data.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        // Serialize issuance for this license and bound outstanding ES challenges.
        await using (var locked = new NpgsqlCommand(
            "SELECT license_id FROM suite.suite_licenses WHERE license_id=$1 FOR UPDATE",
            connection, transaction))
        {
            locked.Parameters.AddWithValue(challenge.LicenseId);
            if (await locked.ExecuteScalarAsync(token) is null)
                throw new SuiteException(403, "LICENSE_DENIED", "License is not active.");
        }
        await using (var cleanup = new NpgsqlCommand("""
            DELETE FROM suite.suite_es_challenges WHERE license_id=$1
              AND expires_at<=clock_timestamp()
            """, connection, transaction))
        {
            cleanup.Parameters.AddWithValue(challenge.LicenseId);
            await cleanup.ExecuteNonQueryAsync(token);
        }
        await using (var quota = new NpgsqlCommand("""
            SELECT count(*) FROM suite.suite_es_challenges
              WHERE license_id=$1 AND device_id=$2 AND expires_at>clock_timestamp()
            """, connection, transaction))
        {
            quota.Parameters.AddWithValue(challenge.LicenseId);
            quota.Parameters.AddWithValue(challenge.DeviceId);
            if ((long)(await quota.ExecuteScalarAsync(token) ?? 64L) >= 64)
                throw new SuiteException(429, "RATE_LIMITED", "Too many requests.");
        }
        await using var command = new NpgsqlCommand("""
            INSERT INTO suite.suite_es_challenges(challenge_id,product_id,license_id,
              device_id,session_id,action,context_hash,nonce,expires_at,revocation_generation,client_contract)
            SELECT $1,$2,$3,$4,$5,$6,$7,$8,to_timestamp($9),l.revocation_generation,$10
            FROM suite.suite_licenses l
              JOIN suite.suite_devices d ON d.license_id=l.license_id AND d.device_id=$4
              JOIN suite.suite_license_enrollments e ON e.license_id=l.license_id AND e.device_id=$4
            WHERE l.license_id=$3 AND l.product_id=$2 AND l.status='ACTIVE'
              AND l.enrollment_state='BOUND' AND l.activation_consumed AND d.status='ACTIVE'
            """, connection, transaction);
        AddChallengeParameters(command, challenge);
        command.Parameters.AddWithValue(_clientContract);
        if (await command.ExecuteNonQueryAsync(token) != 1)
            throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized.");
        await transaction.CommitAsync(token);
    }

    public async Task<ChallengeRecord?> FindChallengeAsync(string id, string action,
        long now, CancellationToken token)
    {
        EmulationStationService.RequireSessionAction(action);
        await using var command = _data.CreateCommand("""
            SELECT c.challenge_id,c.product_id,c.license_id,c.device_id,c.session_id,
              c.action,c.context_hash,c.nonce,extract(epoch from c.expires_at)::bigint
            FROM suite.suite_es_challenges c
              JOIN suite.suite_licenses l ON l.license_id=c.license_id
            WHERE c.challenge_id=$1 AND c.action=$2 AND c.consumed_at IS NULL
              AND c.expires_at>to_timestamp($3) AND l.status='ACTIVE'
              AND c.revocation_generation=l.revocation_generation AND c.client_contract=$4
            """);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(action);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(_clientContract);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetString(6), reader.GetString(7), reader.GetInt64(8), null, null)
            : null;
    }

    public async Task<SessionRecord> CompleteSessionAsync(ChallengeRecord challenge,
        SessionRecord session, string action, long now, CancellationToken token)
    {
        EmulationStationService.RequireSessionAction(action);
        // Each bounded retry reruns all authorization, generation and CAS checks.
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            try { return await CompleteOnceAsync(challenge, session, action, now, token); }
            catch (PostgresException exception) when (
                exception.SqlState is PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected)
            {
                if (attempt == 12)
                    throw new SuiteException(409, "TRANSACTION_CONFLICT",
                        "The operation could not be completed safely.", exception);
                await Task.Delay(RandomNumberGenerator.GetInt32(40, 120) * Math.Min(attempt, 4), token);
            }
        }
        throw new SuiteException(409, "TRANSACTION_CONFLICT",
            "The operation could not be completed safely.");
    }

    private async Task<SessionRecord> CompleteOnceAsync(ChallengeRecord challenge,
        SessionRecord session, string action, long now, CancellationToken token)
    {
        await using var connection = await _data.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, token);
        long generation;
        string origin;
        await using (var license = new NpgsqlCommand("""
            SELECT revocation_generation,provisioning_origin FROM suite.suite_licenses
            WHERE license_id=$1 AND product_id=$2 AND status='ACTIVE'
              AND license_term='LIFETIME' AND expires_at IS NULL AND maximum_active_devices=1
              AND enrollment_state='BOUND' AND activation_consumed FOR UPDATE
            """, connection, transaction))
        {
            license.Parameters.AddWithValue(session.LicenseId);
            license.Parameters.AddWithValue(Protocol.ProductId);
            await using var reader = await license.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
                throw new SuiteException(403, "LICENSE_DENIED", "License is not active.");
            generation = reader.GetInt64(0);
            origin = reader.GetString(1);
        }
        if (origin == "COMMERCE")
        {
            await using var delivery = new NpgsqlCommand("""
                SELECT provisioning_state,financial_state,last_source_version,
                  administrative_resume_source_version FROM suite.suite_license_deliveries
                WHERE license_id=$1 FOR UPDATE
                """, connection, transaction);
            delivery.Parameters.AddWithValue(session.LicenseId);
            await using var reader = await delivery.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || reader.GetString(0) != "PROVISIONED" ||
                !(reader.GetString(1) == "PAID" || (reader.GetString(1) == "SUSPENDED" &&
                    !reader.IsDBNull(3) && reader.GetInt64(3) == reader.GetInt64(2))))
                throw new SuiteException(403, "LICENSE_DENIED", "License is not active.");
        }
        await using (var enrollment = new NpgsqlCommand("""
            SELECT device_id FROM suite.suite_license_enrollments
            WHERE license_id=$1 FOR UPDATE
            """, connection, transaction))
        {
            enrollment.Parameters.AddWithValue(session.LicenseId);
            if ((string?)await enrollment.ExecuteScalarAsync(token) != session.DeviceId)
                throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized.");
        }
        await using (var device = new NpgsqlCommand("""
            SELECT status FROM suite.suite_devices WHERE license_id=$1 AND device_id=$2 FOR UPDATE
            """, connection, transaction))
        {
            device.Parameters.AddWithValue(session.LicenseId);
            device.Parameters.AddWithValue(session.DeviceId);
            if ((string?)await device.ExecuteScalarAsync(token) != "ACTIVE")
                throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized.");
        }

        // Consume inside the same transaction and bind every field. An ES proof
        // cannot consume a Suite/content challenge, even with the same CNG key.
        await using (var consume = new NpgsqlCommand("""
            UPDATE suite.suite_es_challenges SET consumed_at=clock_timestamp()
            WHERE challenge_id=$1 AND license_id=$2 AND device_id=$3 AND session_id=$4
              AND action=$5 AND context_hash=$6 AND product_id=$7 AND revocation_generation=$8
              AND consumed_at IS NULL AND expires_at>clock_timestamp() AND client_contract=$9
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue(challenge.ChallengeId);
            consume.Parameters.AddWithValue(session.LicenseId);
            consume.Parameters.AddWithValue(session.DeviceId);
            consume.Parameters.AddWithValue(session.SessionId);
            consume.Parameters.AddWithValue(action);
            consume.Parameters.AddWithValue(challenge.ContextHash);
            consume.Parameters.AddWithValue(Protocol.ProductId);
            consume.Parameters.AddWithValue(generation);
            consume.Parameters.AddWithValue(_clientContract);
            if (await consume.ExecuteNonQueryAsync(token) != 1)
                throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired.");
        }

        // Match Suite: a freshly proven open atomically replaces only this
        // license/device's ES session. A previous sessionId can no longer renew,
        // including with a heartbeat challenge issued before this replacement.
        var sql = action == "session.open" ? """
            INSERT INTO suite.suite_es_sessions(license_id,device_id,session_id,status,
              authorized_until,last_server_time,revocation_generation)
            VALUES($1,$2,$3,'ACTIVE',to_timestamp($4),$5,$6)
            ON CONFLICT(license_id,device_id) DO UPDATE SET session_id=$3,status='ACTIVE',
              authorized_until=to_timestamp($4),
              last_server_time=GREATEST(suite.suite_es_sessions.last_server_time+1,$5),
              revocation_generation=$6,updated_at=clock_timestamp()
            """ : """
            UPDATE suite.suite_es_sessions SET authorized_until=to_timestamp($4),
              last_server_time=GREATEST(last_server_time+1,$5),updated_at=clock_timestamp()
            WHERE license_id=$1 AND device_id=$2 AND session_id=$3 AND status='ACTIVE'
              AND revocation_generation=$6 AND authorized_until>clock_timestamp()
            """;
        await using var command = new NpgsqlCommand(sql + """

            RETURNING license_id,device_id,session_id,status,
              extract(epoch from authorized_until)::bigint,last_server_time,revocation_generation
            """, connection, transaction);
        command.Parameters.AddWithValue(session.LicenseId);
        command.Parameters.AddWithValue(session.DeviceId);
        command.Parameters.AddWithValue(session.SessionId);
        command.Parameters.AddWithValue(session.AuthorizedUntil);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(generation);
        SessionRecord result;
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token))
                throw new SuiteException(409, "SESSION_INVALID", "Session is not current.");
            result = new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
        }
        await transaction.CommitAsync(token);
        return result;
    }

    private static void AddChallengeParameters(NpgsqlCommand command, ChallengeRecord challenge)
    {
        command.Parameters.AddWithValue(challenge.ChallengeId);
        command.Parameters.AddWithValue(challenge.ProductId);
        command.Parameters.AddWithValue(challenge.LicenseId);
        command.Parameters.AddWithValue(challenge.DeviceId);
        command.Parameters.AddWithValue(challenge.SessionId);
        command.Parameters.AddWithValue(challenge.Action);
        command.Parameters.AddWithValue(challenge.ContextHash);
        command.Parameters.AddWithValue(challenge.Nonce);
        command.Parameters.AddWithValue(challenge.ExpiresAt);
    }

    public Task<CompletionRecord?> FindCompletionAsync(string challengeId, CancellationToken token) =>
        throw new SuiteException(400, "ACTION_INVALID", "Activation belongs to TurboRama Suite.");
    public Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord challenge,
        string requestDigest, DeviceRecord device, SignedAssertionEnvelope result, CancellationToken token) =>
        throw new SuiteException(400, "ACTION_INVALID", "Activation belongs to TurboRama Suite.");
}
