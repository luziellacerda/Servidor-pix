using System.Data;
using System.Security.Cryptography;
using Npgsql;

namespace TurboRamaSuiteOnlineServer;

public sealed record StationLicense(string LicenseId, string ActivationVerifier,
    long ActivationGeneration, long RevocationGeneration);
public sealed record StationChallenge(string ChallengeId, string LicenseId,
    string DeviceId, string Action, string Nonce, string? PublicKeySpki,
    string? ActivationVerifier, long? ActivationGeneration,
    long RevocationGeneration);
public sealed record StationDevice(string LicenseId, string DeviceId,
    string PublicKeySpki, bool RequestProofRequired = false, bool VerifiedAppRequired = false);
public sealed record StationSession(string SessionId, string LicenseId,
    string DeviceId, string? DisplayName, long? ProfileVersion,
    string ProofMode = "none", string? ProofKeySpki = null);

public sealed class PostgresStationStore(NpgsqlDataSource database, bool isolated = false)
{
    // SQL comes only from the constants below; no client value can select a schema.
    private string ScopedSql(string sql) => isolated
        ? sql.Replace("suite.", "station_api.", StringComparison.Ordinal) : sql;
    private NpgsqlCommand Command(string sql) => database.CreateCommand(ScopedSql(sql));
    private NpgsqlCommand TransactionCommand(string sql, NpgsqlConnection connection,
        NpgsqlTransaction transaction) => new(ScopedSql(sql), connection, transaction);
    private const string EligibleDelivery = """
        EXISTS (SELECT 1 FROM suite.suite_license_deliveries d
          WHERE d.license_id=l.license_id
            AND d.product_id='TURBORAMA_STATION_ANDROID'
            AND d.source_product_sku='STATION_ANDROID_LIFETIME_1_DEVICE'
            AND d.provisioning_state='PROVISIONED'
            AND d.financial_state='PAID')
        """;

    public async Task<StationLicense?> FindActivationAsync(string verifier,
        CancellationToken cancellationToken)
    {
        await using var command = Command($"""
            SELECT l.license_id,l.activation_verifier,l.activation_generation,
                   l.revocation_generation
            FROM suite.suite_licenses l
            WHERE l.product_id='TURBORAMA_STATION_ANDROID' AND l.status='ACTIVE'
              AND l.license_term='LIFETIME' AND l.expires_at IS NULL
              AND l.enrollment_state='PENDING_ENROLLMENT'
              AND l.maximum_active_devices=1 AND NOT l.activation_consumed
              AND l.activation_verifier=$1 AND l.activation_expires_at>clock_timestamp()
              AND {EligibleDelivery}
            """);
        command.Parameters.AddWithValue(verifier);
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken)
            ? new(row.GetString(0), row.GetString(1), row.GetInt64(2), row.GetInt64(3))
            : null;
    }

    public async Task InsertChallengeAsync(StationChallenge challenge,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            INSERT INTO suite.station_challenges(challenge_id,license_id,device_id,action,
              nonce,public_key_spki,activation_verifier,activation_generation,
              revocation_generation,expires_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,clock_timestamp()+interval '60 seconds')
            """);
        BindChallenge(command, challenge);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StationChallenge?> FindChallengeAsync(string challengeId,
        string action, CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT challenge_id,license_id,device_id,action,nonce,public_key_spki,
                   activation_verifier,activation_generation,revocation_generation
            FROM suite.station_challenges
            WHERE challenge_id=$1 AND action=$2 AND consumed_at IS NULL
              AND expires_at>clock_timestamp()
            """);
        command.Parameters.AddWithValue(challengeId);
        command.Parameters.AddWithValue(action);
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken) ? ReadChallenge(row) : null;
    }

    public async Task<StationDevice?> FindDeviceAsync(string licenseId,
        string deviceId, CancellationToken cancellationToken)
    {
        await using var command = Command($"""
            SELECT d.license_id,d.device_id,d.public_key_spki,d.request_proof_required,d.verified_app_required
            FROM suite.station_devices d JOIN suite.suite_licenses l
              ON l.license_id=d.license_id
            WHERE d.license_id=$1 AND d.device_id=$2 AND d.status='ACTIVE'
              AND l.product_id='TURBORAMA_STATION_ANDROID' AND l.status='ACTIVE'
              AND l.license_term='LIFETIME' AND l.expires_at IS NULL
              AND l.enrollment_state='BOUND'
              AND {EligibleDelivery}
            """);
        command.Parameters.AddWithValue(licenseId);
        command.Parameters.AddWithValue(deviceId);
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken)
            ? new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetBoolean(3), row.GetBoolean(4)) : null;
    }

    public async Task<long> GetRevocationGenerationAsync(string licenseId,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT revocation_generation FROM suite.suite_licenses
            WHERE license_id=$1 AND product_id='TURBORAMA_STATION_ANDROID'
              AND status='ACTIVE'
            """);
        command.Parameters.AddWithValue(licenseId);
        return (long?)await command.ExecuteScalarAsync(cancellationToken) ??
            throw new SuiteException(403, "STATION_LICENSE_DENIED",
                "Station license is not active.");
    }

    public async Task<string> ActivateAsync(StationChallenge challenge,
        string verifier, string manufacturer, string model, int sdk,
        string version, CancellationToken cancellationToken, StationSessionSecurity? security = null)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await ActivateOnceAsync(challenge, verifier, manufacturer,
                model, sdk, version, cancellationToken, security ?? StationSessionSecurity.Legacy); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                && attempt < 3)
            { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt,
                cancellationToken); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            { throw new SuiteException(409, "STATION_TRANSACTION_CONFLICT",
                "Station state changed during activation.", exception); }
        }
        throw new SuiteException(409, "STATION_TRANSACTION_CONFLICT",
            "Station state changed during activation.");
    }

    private async Task<string> ActivateOnceAsync(StationChallenge challenge,
        string verifier, string manufacturer, string model, int sdk,
        string version, CancellationToken cancellationToken, StationSessionSecurity security)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await using (var lockLicense = TransactionCommand($"""
            SELECT l.license_id FROM suite.suite_licenses l
            WHERE l.license_id=$1 AND l.product_id='TURBORAMA_STATION_ANDROID'
              AND l.status='ACTIVE' AND l.license_term='LIFETIME'
              AND l.expires_at IS NULL AND l.maximum_active_devices=1
              AND l.enrollment_state='PENDING_ENROLLMENT'
              AND NOT l.activation_consumed AND l.activation_verifier=$2
              AND l.activation_expires_at>clock_timestamp()
              AND l.activation_generation=$3 AND l.revocation_generation=$4
              AND {EligibleDelivery}
            FOR UPDATE OF l
            """, connection, transaction))
        {
            lockLicense.Parameters.AddWithValue(challenge.LicenseId);
            lockLicense.Parameters.AddWithValue(verifier);
            lockLicense.Parameters.AddWithValue(challenge.ActivationGeneration!.Value);
            lockLicense.Parameters.AddWithValue(challenge.RevocationGeneration);
            if (await lockLicense.ExecuteScalarAsync(cancellationToken) is null)
                throw new SuiteException(409, "STATION_ACTIVATION_REPLAY",
                    "Activation is no longer available.");
        }
        await using (var consume = TransactionCommand("""
            UPDATE suite.station_challenges SET consumed_at=clock_timestamp()
            WHERE challenge_id=$1 AND action='ACTIVATE' AND consumed_at IS NULL
              AND expires_at>clock_timestamp() AND license_id=$2 AND device_id=$3
              AND activation_verifier=$4 AND activation_generation=$5
              AND revocation_generation=$6
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue(challenge.ChallengeId);
            consume.Parameters.AddWithValue(challenge.LicenseId);
            consume.Parameters.AddWithValue(challenge.DeviceId);
            consume.Parameters.AddWithValue(verifier);
            consume.Parameters.AddWithValue(challenge.ActivationGeneration!.Value);
            consume.Parameters.AddWithValue(challenge.RevocationGeneration);
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new SuiteException(409, "STATION_CHALLENGE_INVALID",
                    "Challenge is invalid or expired.");
        }
        await using (var insert = TransactionCommand(isolated
            ? "SELECT station_api.bind_device($1,$2,$3,$4,$5,$6,$7)" : """
            INSERT INTO suite.station_devices(license_id,device_id,public_key_spki,
              manufacturer,model,android_sdk,client_version,status)
            VALUES($1,$2,$3,$4,$5,$6,$7,'ACTIVE')
            ON CONFLICT (license_id,device_id) DO UPDATE SET
              public_key_spki=EXCLUDED.public_key_spki,
              manufacturer=EXCLUDED.manufacturer,model=EXCLUDED.model,
              android_sdk=EXCLUDED.android_sdk,client_version=EXCLUDED.client_version,
              status='ACTIVE',updated_at=clock_timestamp()
            WHERE suite.station_devices.status='REVOKED'
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(challenge.LicenseId);
            insert.Parameters.AddWithValue(challenge.DeviceId);
            insert.Parameters.AddWithValue(challenge.PublicKeySpki!);
            insert.Parameters.AddWithValue(manufacturer);
            insert.Parameters.AddWithValue(model);
            insert.Parameters.AddWithValue(sdk);
            insert.Parameters.AddWithValue(version);
            try
            {
                var inserted = isolated
                    ? (bool?)await insert.ExecuteScalarAsync(cancellationToken) == true
                    : await insert.ExecuteNonQueryAsync(cancellationToken) == 1;
                if (!inserted)
                    throw new SuiteException(409, "STATION_DEVICE_ALREADY_BOUND",
                        "Device is already bound.");
            }
            catch (PostgresException exception) when (exception.SqlState ==
                PostgresErrorCodes.UniqueViolation)
            {
                throw new SuiteException(409, "STATION_DEVICE_ALREADY_BOUND",
                    "Device is already bound.", exception);
            }
        }
        await BindSecurityAsync(connection, transaction, challenge, security, cancellationToken);
        await using (var update = TransactionCommand("""
            UPDATE suite.suite_licenses SET activation_consumed=true,
              enrollment_state='BOUND',updated_at=clock_timestamp()
            WHERE license_id=$1
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(challenge.LicenseId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return challenge.LicenseId;
    }

    public async Task<(string SessionId, string AccessToken)> OpenSessionAsync(
        StationChallenge challenge, string token, string sessionId,
        CancellationToken cancellationToken, StationSessionSecurity? security = null)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try { return await OpenSessionOnceAsync(challenge, token, sessionId,
                cancellationToken, security ?? StationSessionSecurity.Legacy); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
                && attempt < 3)
            { await Task.Delay(RandomNumberGenerator.GetInt32(15, 75) * attempt,
                cancellationToken); }
            catch (PostgresException exception) when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            { throw new SuiteException(409, "STATION_TRANSACTION_CONFLICT",
                "Station state changed during session creation.", exception); }
        }
        throw new SuiteException(409, "STATION_TRANSACTION_CONFLICT",
            "Station state changed during session creation.");
    }

    private async Task<(string SessionId, string AccessToken)> OpenSessionOnceAsync(
        StationChallenge challenge, string token, string sessionId,
        CancellationToken cancellationToken, StationSessionSecurity security)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        // The authoritative license row below is locked through commit. That
        // serializes renewal/revocation for this owner; the conditional challenge
        // update preserves single use and the locked transaction replaces sessions.
        // Serializable predicate locks also conflicted across unrelated licenses
        // during concurrent renewals, despite the existing bounded retries.
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await using (var valid = TransactionCommand($"""
            SELECT l.license_id FROM suite.suite_licenses l
            JOIN suite.station_devices d ON d.license_id=l.license_id
            WHERE l.license_id=$1 AND d.device_id=$2 AND d.status='ACTIVE'
              AND l.product_id='TURBORAMA_STATION_ANDROID' AND l.status='ACTIVE'
              AND l.enrollment_state='BOUND'
              AND l.revocation_generation=$3 AND {EligibleDelivery}
            FOR UPDATE OF l
            """, connection, transaction))
        {
            valid.Parameters.AddWithValue(challenge.LicenseId);
            valid.Parameters.AddWithValue(challenge.DeviceId);
            valid.Parameters.AddWithValue(challenge.RevocationGeneration);
            if (await valid.ExecuteScalarAsync(cancellationToken) is null)
                throw new SuiteException(403, "STATION_LICENSE_DENIED",
                    "Station license is not active.");
        }
        await BindSecurityAsync(connection, transaction, challenge, security, cancellationToken);
        await using (var consume = TransactionCommand("""
            UPDATE suite.station_challenges SET consumed_at=clock_timestamp()
            WHERE challenge_id=$1 AND action='SESSION' AND consumed_at IS NULL
              AND expires_at>clock_timestamp() AND license_id=$2 AND device_id=$3
              AND revocation_generation=$4
            """, connection, transaction))
        {
            consume.Parameters.AddWithValue(challenge.ChallengeId);
            consume.Parameters.AddWithValue(challenge.LicenseId);
            consume.Parameters.AddWithValue(challenge.DeviceId);
            consume.Parameters.AddWithValue(challenge.RevocationGeneration);
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new SuiteException(409, "STATION_CHALLENGE_INVALID",
                    "Challenge is invalid or expired.");
        }
        await using (var revoke = TransactionCommand("""
            UPDATE suite.station_sessions SET status='REVOKED'
            WHERE license_id=$1 AND device_id=$2 AND status='ACTIVE'
            """, connection, transaction))
        {
            revoke.Parameters.AddWithValue(challenge.LicenseId);
            revoke.Parameters.AddWithValue(challenge.DeviceId);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var insert = TransactionCommand("""
            INSERT INTO suite.station_sessions(session_id,license_id,device_id,token_digest,
              revocation_generation,status,authorized_until,request_proof_mode,request_proof_key_spki)
            VALUES($1,$2,$3,$4,$5,'ACTIVE',clock_timestamp()+interval '180 seconds',$6,$7)
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue(sessionId);
            insert.Parameters.AddWithValue(challenge.LicenseId);
            insert.Parameters.AddWithValue(challenge.DeviceId);
            insert.Parameters.AddWithValue(StationProtocol.HashToken(token));
            insert.Parameters.AddWithValue(challenge.RevocationGeneration);
            insert.Parameters.AddWithValue(security.Mode);
            insert.Parameters.AddWithValue((object?)security.PublicKeySpki ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (sessionId, token);
    }

    public async Task<StationSession?> FindSessionAsync(string token,
        CancellationToken cancellationToken)
    {
        await using var command = Command($"""
            SELECT s.session_id,s.license_id,s.device_id,p.display_name,p.profile_version,
                   s.request_proof_mode,s.request_proof_key_spki
            FROM suite.station_sessions s
            JOIN suite.suite_licenses l ON l.license_id=s.license_id
            JOIN suite.station_devices d ON d.license_id=s.license_id AND d.device_id=s.device_id
            LEFT JOIN suite.station_customer_projection p ON p.license_id=s.license_id
              AND EXISTS (SELECT 1 FROM suite.suite_license_deliveries pd
                WHERE pd.license_id=p.license_id
                  AND pd.product_id='TURBORAMA_STATION_ANDROID'
                  AND pd.source_system=p.source_system
                  AND pd.source_purchase_id=p.source_purchase_id
                  AND pd.source_item_key=p.source_item_key)
            WHERE s.token_digest=$1 AND s.status='ACTIVE'
              AND s.authorized_until>clock_timestamp()
              AND s.revocation_generation=l.revocation_generation
              AND d.status='ACTIVE' AND l.product_id='TURBORAMA_STATION_ANDROID'
              AND l.status='ACTIVE' AND l.enrollment_state='BOUND'
              AND {EligibleDelivery}
            """);
        command.Parameters.AddWithValue(StationProtocol.HashToken(token));
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken)
            ? new(row.GetString(0), row.GetString(1), row.GetString(2),
                row.IsDBNull(3) ? null : row.GetString(3),
                row.IsDBNull(4) ? null : row.GetInt64(4),
                row.GetString(5), row.IsDBNull(6) ? null : row.GetString(6)) : null;
    }

    private async Task BindSecurityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        StationChallenge challenge, StationSessionSecurity security, CancellationToken token)
    {
        // The owner license is already locked. A client that has upgraded cannot
        // obtain a weaker session, including a racing request started before upgrade.
        await using var command = TransactionCommand("""
            UPDATE suite.station_devices SET
              request_proof_required=request_proof_required OR $3,
              verified_app_required=verified_app_required OR $4
            WHERE license_id=$1 AND device_id=$2 AND status='ACTIVE'
              AND (NOT request_proof_required OR $3)
              AND (NOT verified_app_required OR $4)
            RETURNING device_id
            """, connection, transaction);
        command.Parameters.AddWithValue(challenge.LicenseId);
        command.Parameters.AddWithValue(challenge.DeviceId);
        command.Parameters.AddWithValue(security.Mode != "none");
        command.Parameters.AddWithValue(security.Mode == "ec-p256-v1");
        if (await command.ExecuteScalarAsync(token) is null) throw new SuiteException(403,
            "STATION_SECURITY_DOWNGRADE_DENIED", "The device requires its current Station security mode.");
    }

    public async Task InsertGrantAsync(StationGrantRecord grant, int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            INSERT INTO suite.station_download_grants(grant_id,license_id,device_id,item_id,
              key_version,nonce,ciphertext,tag,expires_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,clock_timestamp()+make_interval(secs=>$9))
            """);
        command.Parameters.AddWithValue(grant.GrantId);
        command.Parameters.AddWithValue(grant.LicenseId);
        command.Parameters.AddWithValue(grant.DeviceId);
        command.Parameters.AddWithValue(grant.ItemId);
        command.Parameters.AddWithValue(grant.KeyVersion);
        command.Parameters.AddWithValue(grant.Nonce);
        command.Parameters.AddWithValue(grant.Ciphertext);
        command.Parameters.AddWithValue(grant.Tag);
        command.Parameters.AddWithValue(expiresInSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<StationGrantRecord?> PeekGrantAsync(string grantId,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT grant_id,license_id,device_id,item_id,key_version,nonce,ciphertext,tag
            FROM suite.station_download_grants
            WHERE grant_id=$1 AND consumed_at IS NULL AND expires_at>clock_timestamp()
            """);
        command.Parameters.AddWithValue(grantId);
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken) ? ReadGrant(row) : null;
    }

    public async Task<StationGrantRecord?> ConsumeGrantAsync(string grantId,
        string licenseId, string deviceId, CancellationToken cancellationToken)
    {
        await using var command = Command("""
            UPDATE suite.station_download_grants
            SET consumed_at=clock_timestamp()
            WHERE grant_id=$1 AND license_id=$2 AND device_id=$3
              AND consumed_at IS NULL AND expires_at>clock_timestamp()
            RETURNING grant_id,license_id,device_id,item_id,key_version,nonce,ciphertext,tag
            """);
        command.Parameters.AddWithValue(grantId);
        command.Parameters.AddWithValue(licenseId);
        command.Parameters.AddWithValue(deviceId);
        await using var row = await command.ExecuteReaderAsync(cancellationToken);
        return await row.ReadAsync(cancellationToken) ? ReadGrant(row) : null;
    }

    private static StationGrantRecord ReadGrant(NpgsqlDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3),
            row.GetInt32(4), (byte[])row.GetValue(5), (byte[])row.GetValue(6),
            (byte[])row.GetValue(7));

    private static void BindChallenge(NpgsqlCommand command, StationChallenge value)
    {
        command.Parameters.AddWithValue(value.ChallengeId);
        command.Parameters.AddWithValue(value.LicenseId);
        command.Parameters.AddWithValue(value.DeviceId);
        command.Parameters.AddWithValue(value.Action);
        command.Parameters.AddWithValue(value.Nonce);
        command.Parameters.AddWithValue((object?)value.PublicKeySpki ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)value.ActivationVerifier ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)value.ActivationGeneration ?? DBNull.Value);
        command.Parameters.AddWithValue(value.RevocationGeneration);
    }

    private static StationChallenge ReadChallenge(NpgsqlDataReader row) =>
        new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3),
            row.GetString(4), row.IsDBNull(5) ? null : row.GetString(5),
            row.IsDBNull(6) ? null : row.GetString(6),
            row.IsDBNull(7) ? null : row.GetInt64(7), row.GetInt64(8));
}
