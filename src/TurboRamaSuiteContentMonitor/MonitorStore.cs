using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentMonitor;

internal sealed class MonitorStore : IDisposable
{
    private const string LinkAlertReport =
        "/run/turborama-suite-content-monitor/link-alerts.json";
    private readonly NpgsqlDataSource dataSource;
    public MonitorStore(string connection) => dataSource = NpgsqlDataSource.Create(connection);

    public async Task<CycleLock?> TryAcquireCycleLockAsync(CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended('suite:content-monitor-cycle',0))", connection);
            if ((bool)(await command.ExecuteScalarAsync(ct) ?? false)) return new CycleLock(connection);
            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<CycleLock?> TryAcquireCandidateCycleLockAsync(CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtextextended('suite:content-candidate-cycle',0))",
                connection);
            if ((bool)(await command.ExecuteScalarAsync(ct) ?? false))
                return new CycleLock(connection, "suite:content-candidate-cycle");
            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task StartCycleAsync(string cycleId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_content_monitor_state
            SET last_cycle_id=$1,last_started_at=clock_timestamp(),last_outcome='RUNNING',
                last_result_code='CYCLE_STARTED',updated_at=clock_timestamp()
            WHERE product_id='TURBORAMA_SUITE'
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(cycleId);
            await update.ExecuteNonQueryAsync(ct);
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_management_audit(
              event_type,actor,job_id,correlation_id,outcome,detail_code)
            VALUES('CONTENT_WORKER_CYCLE_STARTED','content-monitor',$1,$1,'ACCEPTED','CYCLE_STARTED')
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(cycleId);
            await audit.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task CompleteCycleAsync(string cycleId, string outcome, string code,
        CancellationToken ct)
    {
        var eventType = outcome == "SUCCESS" ? "CONTENT_WORKER_CYCLE_COMPLETED" :
            "CONTENT_WORKER_CYCLE_FAILED";
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_content_monitor_state
            SET last_completed_at=clock_timestamp(),last_outcome=$2,last_result_code=$3,
                updated_at=clock_timestamp()
            WHERE product_id='TURBORAMA_SUITE' AND last_cycle_id=$1
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(cycleId);
            update.Parameters.AddWithValue(outcome);
            update.Parameters.AddWithValue(code);
            await update.ExecuteNonQueryAsync(ct);
        }
        await using (var audit = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_management_audit(
              event_type,actor,job_id,correlation_id,outcome,detail_code)
            VALUES($4,'content-monitor',$1,$1,$5,$3)
            """, connection, transaction))
        {
            audit.Parameters.AddWithValue(cycleId);
            audit.Parameters.AddWithValue(outcome);
            audit.Parameters.AddWithValue(code);
            audit.Parameters.AddWithValue(eventType);
            audit.Parameters.AddWithValue(outcome == "SUCCESS" ? "SUCCESS" : "FAILED");
            await audit.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task SynchronizeHealthAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO suite.suite_content_item_health(
              product_id,item_id,observed_catalog_identity,observed_availability,next_check_at)
            SELECT 'TURBORAMA_SUITE',i.item_id,cs.active_catalog_identity,i.status,clock_timestamp()
            FROM suite.suite_content_catalog_state cs
            JOIN suite.suite_content_items i ON i.catalog_identity=cs.active_catalog_identity
            WHERE cs.product_id='TURBORAMA_SUITE'
            ON CONFLICT(product_id,item_id) DO UPDATE
              SET observed_catalog_identity=excluded.observed_catalog_identity,
                  observed_availability=excluded.observed_availability,
                  last_checked_at=NULL,
                  last_success_at=NULL,
                  last_full_validation_at=NULL,
                  last_terminal_failure_at=NULL,
                  next_check_at=clock_timestamp(),
                  consecutive_terminal_failures=0,
                  consecutive_successes=0,
                  last_result_code='NOT_CHECKED',
                  row_version=suite_content_item_health.row_version+1,
                  updated_at=clock_timestamp()
              WHERE suite_content_item_health.observed_catalog_identity<>excluded.observed_catalog_identity
                 OR suite_content_item_health.observed_availability<>excluded.observed_availability
            """);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<CandidateLease>> LeaseCandidatesAsync(string workerId,
        int count, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using var command = new NpgsqlCommand("""
            WITH selected AS(
              SELECT candidate_id FROM suite.suite_content_origin_candidates
              WHERE (state='STAGED' OR (state IN('VALIDATING','VERIFIED') AND lease_expires_at<clock_timestamp()))
              ORDER BY submitted_at,candidate_id FOR UPDATE SKIP LOCKED LIMIT $1
            )
            UPDATE suite.suite_content_origin_candidates candidate
            SET state='VALIDATING',lease_owner=$2,lease_expires_at=clock_timestamp()+interval '30 minutes',
                attempt_count=attempt_count+1,updated_at=clock_timestamp(),
                internal_result_code=CASE WHEN candidate.state='STAGED' THEN 'STAGED' ELSE 'LEASE_RECOVERED' END
            FROM selected WHERE candidate.candidate_id=selected.candidate_id
            RETURNING btrim(candidate.candidate_id),btrim(candidate.item_id),
              btrim(candidate.base_catalog_identity),candidate.request_id,candidate.state,
              candidate.upstream_url_ciphertext,candidate.upstream_url_nonce,candidate.upstream_url_tag,
              candidate.key_version,candidate.change_intent,candidate.change_reason,
              candidate.expected_content_length,btrim(candidate.expected_sha256),
              candidate.expected_artifact_version,candidate.expected_file_extension,
              candidate.expected_extract_policy,candidate.submitted_by
            """, connection, transaction);
        command.Parameters.AddWithValue(count);
        command.Parameters.AddWithValue(workerId);
        var candidates = new List<CandidateLease>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            candidates.Add(new CandidateLease(reader.GetString(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4),
                (byte[])reader[5], (byte[])reader[6], (byte[])reader[7], reader.GetInt32(8),
                reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt64(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15), reader.GetString(16), workerId));
        await reader.DisposeAsync();
        await transaction.CommitAsync(ct);
        return candidates;
    }

    public async Task MarkCandidateValidatingAuditAsync(CandidateLease candidate,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO suite.suite_content_management_audit(
              event_type,actor,item_id,candidate_id,job_id,correlation_id,outcome,detail_code,
              previous_catalog_identity)
            SELECT 'CONTENT_CANDIDATE_VALIDATING','content-monitor',$1,$2,$3,$3,
              'ACCEPTED','VALIDATION_STARTED',$4
            WHERE EXISTS(SELECT 1 FROM suite.suite_content_origin_candidates
              WHERE candidate_id=$2 AND state='VALIDATING' AND lease_owner=$3)
            """);
        command.Parameters.AddWithValue(candidate.ItemId);
        command.Parameters.AddWithValue(candidate.CandidateId);
        command.Parameters.AddWithValue(candidate.LeaseOwner);
        command.Parameters.AddWithValue(candidate.BaseCatalogIdentity);
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new MonitorFailure("CANDIDATE_LEASE_LOST");
    }

    public async Task<bool> RenewCandidateLeaseAsync(CandidateLease candidate, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE suite.suite_content_origin_candidates
            SET lease_expires_at=clock_timestamp()+interval '30 minutes',
                updated_at=clock_timestamp()
            WHERE candidate_id=$1 AND state IN('VALIDATING','VERIFIED') AND lease_owner=$2
            """);
        command.Parameters.AddWithValue(candidate.CandidateId);
        command.Parameters.AddWithValue(candidate.LeaseOwner);
        if (await command.ExecuteNonQueryAsync(ct) == 1) return true;
        await using var state = dataSource.CreateCommand("""
            SELECT state FROM suite.suite_content_origin_candidates WHERE candidate_id=$1
            """);
        state.Parameters.AddWithValue(candidate.CandidateId);
        var current = await state.ExecuteScalarAsync(ct) as string;
        if (current is "PUBLISHED" or "REJECTED" or "SUPERSEDED") return false;
        throw new MonitorFailure("CANDIDATE_LEASE_LOST");
    }

    public async Task MarkCandidateRejectedAsync(CandidateLease candidate, string code,
        CancellationToken ct)
    {
        var internalCode = ResultCodes.Candidate(code);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_content_origin_candidates
            SET state='REJECTED',internal_result_code=$2,lease_owner=NULL,lease_expires_at=NULL,
                updated_at=clock_timestamp()
            WHERE candidate_id=$1 AND state='VALIDATING' AND lease_owner=$3
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(candidate.CandidateId);
            update.Parameters.AddWithValue(internalCode);
            update.Parameters.AddWithValue(candidate.LeaseOwner);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new MonitorFailure("CANDIDATE_LEASE_LOST");
        }
        await InsertAuditAsync(connection, transaction, "CONTENT_CANDIDATE_REJECTED",
            candidate.ItemId, candidate.CandidateId, candidate.CandidateId, "REJECTED",
            "CANDIDATE_REJECTED", candidate.BaseCatalogIdentity, null, ct);
        await InsertAlertAsync(connection, transaction, candidate.ItemId,
            "WARNING", "CONTENT_CANDIDATE_REJECTED", candidate.CandidateId, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task MarkCandidateVerifiedAsync(ValidatedCandidate candidate,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var update = new NpgsqlCommand("""
            UPDATE suite.suite_content_origin_candidates SET state='VERIFIED',
              verified_content_length=$3,verified_sha256=$4,verified_file_extension=$5,
              verified_safe_file_name=$6,verified_extract_policy=$7,verified_content_type=$8,
              verified_source_etag=$9,verified_source_last_modified=$10,
              verified_at=clock_timestamp(),updated_at=clock_timestamp(),internal_result_code='VALIDATION_OK'
            WHERE candidate_id=$1 AND state='VALIDATING' AND lease_owner=$2
            """, connection, transaction))
        {
            update.Parameters.AddWithValue(candidate.Candidate.CandidateId);
            update.Parameters.AddWithValue(candidate.Candidate.LeaseOwner);
            update.Parameters.AddWithValue(candidate.ContentLength);
            update.Parameters.AddWithValue(candidate.Sha256);
            update.Parameters.AddWithValue(candidate.FileExtension);
            update.Parameters.AddWithValue(candidate.SafeFileName);
            update.Parameters.AddWithValue(candidate.ExtractPolicy);
            update.Parameters.AddWithValue(candidate.ContentType);
            update.Parameters.AddWithValue((object?)candidate.Etag ?? DBNull.Value);
            update.Parameters.AddWithValue((object?)candidate.LastModified ?? DBNull.Value);
            if (await update.ExecuteNonQueryAsync(ct) != 1) throw new MonitorFailure("CANDIDATE_LEASE_LOST");
        }
        await InsertAuditAsync(connection, transaction, "CONTENT_CANDIDATE_VERIFIED",
            candidate.Candidate.ItemId, candidate.Candidate.CandidateId,
            candidate.Candidate.CandidateId, "SUCCESS", "CANDIDATE_VERIFIED",
            candidate.Candidate.BaseCatalogIdentity, null, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<HealthTarget>> GetDueHealthTargetsAsync(int maximum,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT btrim(i.item_id),btrim(cs.active_catalog_identity),i.artifact_version,
              i.content_length,btrim(i.sha256),i.file_extension,i.source_etag,i.source_last_modified,
              o.upstream_url_ciphertext,o.upstream_url_nonce,o.upstream_url_tag,o.key_version,
              h.consecutive_terminal_failures,h.last_checked_at,h.last_full_validation_at,h.row_version
            FROM suite.suite_content_catalog_state cs
            JOIN suite.suite_content_items i ON i.catalog_identity=cs.active_catalog_identity AND i.status='READY'
            JOIN suite.suite_content_artifact_origins o
              ON o.catalog_identity=i.catalog_identity AND o.item_id=i.item_id
            JOIN suite.suite_content_item_health h
              ON h.product_id=cs.product_id AND h.item_id=i.item_id
            WHERE cs.product_id='TURBORAMA_SUITE' AND h.next_check_at<=clock_timestamp()
            ORDER BY CASE WHEN (i.source_etag IS NULL OR char_length(i.source_etag)<2 OR
              left(i.source_etag,1)<>'"' OR right(i.source_etag,1)<>'"' OR
              i.source_etag ILIKE 'W/%') AND
              (h.last_full_validation_at IS NULL OR
               h.last_full_validation_at<clock_timestamp()-interval '7 days') THEN 0 ELSE 1 END,
              h.last_full_validation_at NULLS FIRST,h.next_check_at,i.item_id LIMIT $1
            """);
        command.Parameters.AddWithValue(maximum);
        var rows = new List<HealthTarget>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new HealthTarget(reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7), (byte[])reader[8],
                (byte[])reader[9], (byte[])reader[10], reader.GetInt32(11), reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                reader.IsDBNull(14) ? null : reader.GetDateTime(14), reader.GetInt64(15)));
        return rows;
    }

    public async Task WriteLinkAlertReportAsync(IReadOnlyList<HealthProbeResult> results,
        CancellationToken ct)
    {
        var failures = new List<object>();
        foreach (var result in results.Where(value => !value.Success))
        {
            await using var command = dataSource.CreateCommand("""
                SELECT display_name FROM suite.suite_content_management_items
                WHERE item_id=$1 LIMIT 1
                """);
            command.Parameters.AddWithValue(result.Target.ItemId);
            if (await command.ExecuteScalarAsync(ct) is string displayName)
                failures.Add(new { game = displayName, code = result.ResultCode });
        }
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            failures
        });
        var temporary = LinkAlertReport + ".new";
        try
        {
            await File.WriteAllBytesAsync(temporary, payload, ct);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            File.Move(temporary, LinkAlertReport, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            try { File.Delete(temporary); } catch (IOException) { }
        }
    }

    public async Task<IReadOnlyDictionary<string, MaintenancePromotion>> ApplyHealthResultsAsync(
        IReadOnlyList<HealthProbeResult> results, CancellationToken ct)
    {
        var promotions = new Dictionary<string, MaintenancePromotion>(StringComparer.Ordinal);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var result in results)
        {
            if (result.SecurityFailure) continue;
            var canIncrement = result.Target.LastCheckedAt is null ||
                result.Target.LastCheckedAt <= DateTime.UtcNow.AddMinutes(-14);
            var failures = result.Success ? 0 : result.Target.ConsecutiveFailures + (canIncrement ? 1 : 0);
            await using var command = new NpgsqlCommand("""
                UPDATE suite.suite_content_item_health SET
                  last_checked_at=clock_timestamp(),
                  last_success_at=CASE WHEN $2 THEN clock_timestamp() ELSE last_success_at END,
                  last_full_validation_at=CASE WHEN $2 AND $5 THEN clock_timestamp()
                    ELSE last_full_validation_at END,
                  last_terminal_failure_at=CASE WHEN $2 THEN last_terminal_failure_at ELSE clock_timestamp() END,
                  next_check_at=clock_timestamp()+interval '15 minutes',
                  consecutive_terminal_failures=$3,
                  consecutive_successes=CASE WHEN $2 THEN least(consecutive_successes+1,1000000) ELSE 0 END,
                  last_result_code=$4,row_version=row_version+1,updated_at=clock_timestamp()
                WHERE product_id='TURBORAMA_SUITE' AND item_id=$1
                  AND observed_catalog_identity=$6 AND row_version=$7
                  AND EXISTS(
                    SELECT 1 FROM suite.suite_content_catalog_state cs
                    JOIN suite.suite_content_items i
                      ON i.catalog_identity=cs.active_catalog_identity AND i.item_id=$1
                    WHERE cs.product_id='TURBORAMA_SUITE'
                      AND cs.active_catalog_identity=$6 AND i.status='READY'
                      AND i.artifact_version=$8)
                RETURNING row_version
                """, connection, transaction);
            command.Parameters.AddWithValue(result.Target.ItemId);
            command.Parameters.AddWithValue(result.Success);
            command.Parameters.AddWithValue(failures);
            command.Parameters.AddWithValue(result.ResultCode);
            command.Parameters.AddWithValue(result.FullValidation);
            command.Parameters.AddWithValue(result.Target.CatalogIdentity);
            command.Parameters.AddWithValue(result.Target.RowVersion);
            command.Parameters.AddWithValue(result.Target.ArtifactVersion);
            var updatedRowVersion = await command.ExecuteScalarAsync(ct);
            if (updatedRowVersion is long rowVersion && !result.Success &&
                failures >= HealthPolicy.FailureThreshold)
                promotions.Add(result.Target.ItemId, new MaintenancePromotion(
                    result.Target.ItemId, result.Target.CatalogIdentity,
                    result.Target.ArtifactVersion, rowVersion));
        }
        await transaction.CommitAsync(ct);
        return promotions;
    }

    public async Task<SnapshotHeader> LoadActiveHeaderAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT btrim(s.catalog_identity),s.catalog_sequence,btrim(s.inventory_sha256),
              btrim(s.visual_catalog_sha256)
            FROM suite.suite_content_catalog_state cs JOIN suite.suite_content_snapshots s
              ON s.catalog_identity=cs.active_catalog_identity
            WHERE cs.product_id='TURBORAMA_SUITE' AND s.status='PUBLISHED'
            """);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new MonitorFailure("ACTIVE_CATALOG_MISSING");
        return new SnapshotHeader(reader.GetString(0), reader.GetInt64(1),
            reader.GetString(2), reader.GetString(3));
    }

    public async Task<IReadOnlyList<SnapshotItem>> LoadSnapshotItemsAsync(string catalogIdentity,
        CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT btrim(i.item_id),i.display_order,i.display_name,i.visual_extract_policy,i.status,
              i.artifact_version,i.content_length,
              btrim(i.sha256),i.safe_file_name,i.file_extension,i.extract_policy,i.content_type,
              i.source_etag,i.source_last_modified,i.maintenance_reason,
              o.upstream_url_ciphertext,o.upstream_url_nonce,o.upstream_url_tag,o.key_version
            FROM suite.suite_content_items i LEFT JOIN suite.suite_content_artifact_origins o
              ON o.catalog_identity=i.catalog_identity AND o.item_id=i.item_id
            WHERE i.catalog_identity=$1 ORDER BY i.display_order,i.item_id
            """);
        command.Parameters.AddWithValue(catalogIdentity);
        var rows = new List<SnapshotItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new SnapshotItem(reader.GetString(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : (byte[])reader[15],
                reader.IsDBNull(16) ? null : (byte[])reader[16],
                reader.IsDBNull(17) ? null : (byte[])reader[17],
                reader.IsDBNull(18) ? null : reader.GetInt32(18)));
        if (rows.Count != 850 || rows.Select(item => item.ItemId).Distinct(StringComparer.Ordinal).Count() != 850)
            throw new MonitorFailure("CATALOG_CARDINALITY_INVALID");
        return rows;
    }

    public async Task<string> PublishMutationAsync(SnapshotHeader header,
        IReadOnlyList<SnapshotItem> sourceItems, SnapshotMutation mutation,
        MonitorKeyRing originKeys, MonitorDeploymentProvenance provenance,
        CancellationToken ct)
    {
        if (mutation.Candidate is not null &&
            mutation.Candidate.Candidate.BaseCatalogIdentity != header.CatalogIdentity)
            throw new MonitorFailure("CATALOG_CHANGED");
        var loadedKeySetFingerprint = originKeys.CopyDeploymentKeySetFingerprint();
        try
        {
            if (provenance.ActiveKeyVersion != originKeys.ActiveVersion ||
                !FixedLowerHex(provenance.KeySetFingerprint,
                    Convert.ToHexString(loadedKeySetFingerprint).ToLowerInvariant()) ||
                !IsLowerHex(provenance.AllowlistFingerprint, 64))
                throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
        }
        finally { CryptographicOperations.ZeroMemory(loadedKeySetFingerprint); }
        ValidateMaintenanceTargets(header.CatalogIdentity, sourceItems,
            mutation.MaintenanceItems);
        var items = Transform(sourceItems, mutation);
        var readyCount = items.Count(item => item.Status == "READY");
        var maintenanceCount = items.Count - readyCount;
        if (items.Count != 850 || readyCount + maintenanceCount != 850)
            throw new MonitorFailure("CATALOG_CARDINALITY_INVALID");
        if (HealthPolicy.OpensMassFailureGuard(maintenanceCount))
            throw new MonitorFailure("MASS_FAILURE_GUARD");
        if (items.Any(item => item.Status == "READY" &&
                !string.Equals(item.ExtractPolicy, item.VisualExtractPolicy,
                    StringComparison.Ordinal)))
            throw new MonitorFailure("CATALOG_EXTRACT_POLICY_INVALID");
        var identity = ManagedSnapshotIdentity.Compute(header, provenance,
            mutation.OperationId, items);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using (var guard = new NpgsqlCommand("""
            SELECT pg_advisory_xact_lock(hashtextextended('suite:content-publish',0));
            SELECT btrim(active_catalog_identity) FROM suite.suite_content_catalog_state
              WHERE product_id='TURBORAMA_SUITE' FOR UPDATE
            """, connection, transaction))
        {
            await using var result = await guard.ExecuteReaderAsync(ct);
            if (!await result.ReadAsync(ct) || !await result.NextResultAsync(ct) ||
                !await result.ReadAsync(ct)) throw new MonitorFailure("ACTIVE_CATALOG_MISSING");
            if (!string.Equals(result.GetString(0), header.CatalogIdentity, StringComparison.Ordinal))
                throw new MonitorFailure("CATALOG_CHANGED");
        }
        ValidateMaintenanceTargets(header.CatalogIdentity, sourceItems,
            mutation.MaintenanceItems);
        await ValidateMaintenanceTargetsAsync(connection, transaction, header.CatalogIdentity,
            mutation.MaintenanceItems, ct);
        if (HealthPolicy.OpensMassFailureGuard(maintenanceCount))
            throw new MonitorFailure("MASS_FAILURE_GUARD");
        long sequence;
        await using (var next = new NpgsqlCommand(
            "SELECT coalesce(max(catalog_sequence),0)+1 FROM suite.suite_content_snapshots",
            connection, transaction))
            sequence = (long)(await next.ExecuteScalarAsync(ct) ?? throw new MonitorFailure("SEQUENCE_FAILED"));
        await using (var snapshot = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,
              inventory_sha256,visual_catalog_sha256,item_count,ready_item_count,
              maintenance_item_count,status,origin_active_key_version,
              origin_key_set_fingerprint,origin_allowlist_fingerprint)
            VALUES($1,$2,$3,$4,850,$5,$6,'STAGING',$7,$8,$9)
            """, connection, transaction))
        {
            snapshot.Parameters.AddWithValue(identity);
            snapshot.Parameters.AddWithValue(sequence);
            snapshot.Parameters.AddWithValue(header.InventorySha256);
            snapshot.Parameters.AddWithValue(header.VisualCatalogSha256);
            snapshot.Parameters.AddWithValue(readyCount);
            snapshot.Parameters.AddWithValue(maintenanceCount);
            snapshot.Parameters.AddWithValue(provenance.ActiveKeyVersion);
            snapshot.Parameters.AddWithValue(provenance.KeySetFingerprint);
            snapshot.Parameters.AddWithValue(provenance.AllowlistFingerprint);
            await snapshot.ExecuteNonQueryAsync(ct);
        }

        foreach (var item in items)
        {
            if (item.Status == "MAINTENANCE")
            {
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO suite.suite_content_items(
                      catalog_identity,item_id,display_order,display_name,visual_extract_policy,
                      status,maintenance_reason)
                    VALUES($1,$2,$3,$4,$5,'MAINTENANCE','CONTENT_TEMPORARILY_UNAVAILABLE')
                    """, connection, transaction);
                insert.Parameters.AddWithValue(identity);
                insert.Parameters.AddWithValue(item.ItemId);
                insert.Parameters.AddWithValue(item.DisplayOrder);
                insert.Parameters.AddWithValue(item.DisplayName);
                insert.Parameters.AddWithValue(item.VisualExtractPolicy);
                await insert.ExecuteNonQueryAsync(ct);
                continue;
            }
            var descriptorHash = ManagedSnapshotIdentity.DescriptorHash(item, identity);
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,
                  display_name,visual_extract_policy,artifact_id,artifact_version,content_length,sha256,safe_file_name,file_extension,
                  extract_policy,manifest_identity,descriptor_hash,content_type,source_etag,
                  source_last_modified,status)
                VALUES($1,$2,$3,$14,$15,$2,$4,$5,$6,$7,$8,$9,$1,$10,$11,$12,$13,'READY')
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(identity);
                insert.Parameters.AddWithValue(item.ItemId);
                insert.Parameters.AddWithValue(item.DisplayOrder);
                insert.Parameters.AddWithValue(item.ArtifactVersion!.Value);
                insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint,
                    Value = DBNull.Value });
                insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Char,
                    Value = DBNull.Value });
                insert.Parameters.AddWithValue(item.SafeFileName!);
                insert.Parameters.AddWithValue(item.FileExtension!);
                insert.Parameters.AddWithValue(item.ExtractPolicy!);
                insert.Parameters.AddWithValue(descriptorHash);
                insert.Parameters.AddWithValue(item.ContentType!);
                insert.Parameters.AddWithValue((object?)item.Etag ?? DBNull.Value);
                insert.Parameters.AddWithValue((object?)item.LastModified ?? DBNull.Value);
                insert.Parameters.AddWithValue(item.DisplayName);
                insert.Parameters.AddWithValue(item.VisualExtractPolicy);
                await insert.ExecuteNonQueryAsync(ct);
            }
            DecryptedUrl? decrypted = null;
            EncryptedOrigin? encrypted = null;
            try
            {
                var uri = mutation.Candidate?.Candidate.ItemId == item.ItemId
                    ? mutation.Candidate.Url.Uri
                    : (decrypted = originKeys.DecryptOrigin(header.CatalogIdentity,
                        sourceItems.Single(source => source.ItemId == item.ItemId))).Uri;
                encrypted = originKeys.EncryptOrigin(uri, identity, item.ItemId,
                    item.ArtifactVersion.Value);
                await using var origin = new NpgsqlCommand("""
                    INSERT INTO suite.suite_content_artifact_origins(
                      catalog_identity,item_id,upstream_url_ciphertext,upstream_url_nonce,
                      upstream_url_tag,key_version) VALUES($1,$2,$3,$4,$5,$6)
                    """, connection, transaction);
                origin.Parameters.AddWithValue(identity);
                origin.Parameters.AddWithValue(item.ItemId);
                origin.Parameters.AddWithValue(encrypted.Ciphertext);
                origin.Parameters.AddWithValue(encrypted.Nonce);
                origin.Parameters.AddWithValue(encrypted.Tag);
                origin.Parameters.AddWithValue(encrypted.KeyVersion);
                await origin.ExecuteNonQueryAsync(ct);
            }
            finally { encrypted?.Dispose(); decrypted?.Dispose(); }
        }
        var runId = Hash("monitor-run\0" + identity);
        await using (var run = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_publish_runs(run_id,catalog_identity,inventory_sha256,
              expected_item_count,published_item_count,ready_item_count,maintenance_item_count,
              outcome,detail_code)
            VALUES($1,$2,$3,850,850,$4,$5,'PUBLISHED','CONTENT_MONITOR_PUBLISHED')
            """, connection, transaction))
        {
            run.Parameters.AddWithValue(runId);
            run.Parameters.AddWithValue(identity);
            run.Parameters.AddWithValue(header.InventorySha256);
            run.Parameters.AddWithValue(readyCount);
            run.Parameters.AddWithValue(maintenanceCount);
            await run.ExecuteNonQueryAsync(ct);
        }
        await using (var publish = new NpgsqlCommand(
            "SELECT suite.publish_suite_content_catalog($1)", connection, transaction))
        {
            publish.Parameters.AddWithValue(identity);
            await publish.ExecuteNonQueryAsync(ct);
        }
        if (mutation.Candidate is not null)
        {
            var candidate = mutation.Candidate.Candidate;
            await using var update = new NpgsqlCommand("""
                UPDATE suite.suite_content_origin_candidates SET state='PUBLISHED',
                  published_catalog_identity=$2,published_at=clock_timestamp(),
                  updated_at=clock_timestamp(),lease_owner=NULL,lease_expires_at=NULL,
                  internal_result_code='VALIDATION_OK'
                WHERE candidate_id=$1 AND state='VERIFIED'
                  AND lease_owner=$3
                """, connection, transaction);
            update.Parameters.AddWithValue(candidate.CandidateId);
            update.Parameters.AddWithValue(identity);
            update.Parameters.AddWithValue(candidate.LeaseOwner);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new MonitorFailure("CANDIDATE_STATE_CHANGED");
            await InsertAuditAsync(connection, transaction, "CONTENT_CANDIDATE_PUBLISHED",
                candidate.ItemId, candidate.CandidateId, candidate.CandidateId, "SUCCESS",
                "CANDIDATE_PUBLISHED", header.CatalogIdentity, identity, ct);
        }
        foreach (var itemId in mutation.MaintenanceItems.Keys)
        {
            await InsertAuditAsync(connection, transaction, "CONTENT_ITEM_MAINTENANCE",
                itemId, null, mutation.OperationId, "SUCCESS", "ITEM_MAINTENANCE",
                header.CatalogIdentity, identity, ct);
            await InsertAlertAsync(connection, transaction, itemId, "WARNING",
                "CONTENT_ITEM_MAINTENANCE", mutation.OperationId + itemId, ct);
        }
        await using (var health = new NpgsqlCommand("""
            UPDATE suite.suite_content_item_health h SET
              observed_catalog_identity=$1,observed_availability=i.status,
              last_checked_at=CASE WHEN i.status='READY' AND i.item_id=$2
                THEN clock_timestamp() ELSE NULL END,
              last_success_at=CASE WHEN i.status='READY' AND i.item_id=$2
                THEN clock_timestamp() ELSE NULL END,
              last_terminal_failure_at=NULL,
              next_check_at=clock_timestamp(),
              consecutive_terminal_failures=0,
              consecutive_successes=0,
              last_result_code=CASE WHEN i.status='READY' AND i.item_id=$2
                THEN 'CANDIDATE_PUBLISHED' ELSE 'NOT_CHECKED' END,
              last_full_validation_at=CASE WHEN i.status='READY' AND i.item_id=$2
                THEN clock_timestamp() ELSE NULL END,
              row_version=h.row_version+1,updated_at=clock_timestamp()
            FROM suite.suite_content_items i
            WHERE i.catalog_identity=$1 AND h.product_id='TURBORAMA_SUITE' AND h.item_id=i.item_id
            """, connection, transaction))
        {
            health.Parameters.AddWithValue(identity);
            health.Parameters.AddWithValue(mutation.Candidate?.Candidate.ItemId ?? string.Empty);
            await health.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return identity;
    }

    public async Task MarkCandidateSupersededAsync(CandidateLease candidate, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE suite.suite_content_origin_candidates SET state='SUPERSEDED',
              internal_result_code='SUPERSEDED',lease_owner=NULL,lease_expires_at=NULL,
              updated_at=clock_timestamp() WHERE candidate_id=$1 AND state IN('VALIDATING','VERIFIED')
              AND lease_owner=$2
            """);
        command.Parameters.AddWithValue(candidate.CandidateId);
        command.Parameters.AddWithValue(candidate.LeaseOwner);
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new MonitorFailure("CANDIDATE_LEASE_LOST");
    }

    public async Task RecordSecurityAbortAsync(string cycleId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await InsertAuditAsync(connection, transaction, "CONTENT_SECURITY_CYCLE_ABORTED",
            null, null, cycleId, "BLOCKED", "SECURITY_CYCLE_ABORTED", null, null, ct);
        await InsertAlertAsync(connection, transaction, null, "CRITICAL",
            "CONTENT_SECURITY_CYCLE_ABORTED", cycleId, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RecordMassFailureGuardAsync(string cycleId, int count, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await InsertAuditAsync(connection, transaction, "CONTENT_MASS_FAILURE_GUARD",
            null, null, cycleId, "BLOCKED", "MASS_FAILURE_GUARD", null, null, ct);
        await InsertAlertAsync(connection, transaction, null, "CRITICAL",
            "CONTENT_MASS_FAILURE_GUARD", cycleId + count.ToString(), ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RecordCycleFailureAsync(string cycleId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await InsertAlertAsync(connection, transaction, null, "CRITICAL",
            "CONTENT_WORKER_CYCLE_FAILED", cycleId, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<int> DispatchPendingAlertsAsync(int maximum, CancellationToken ct)
    {
        if (maximum is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximum));
        var delivered = 0;
        while (delivered < maximum)
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var select = new NpgsqlCommand("""
                SELECT alert_id,severity,alert_code,occurrence_count
                FROM suite.suite_content_alert_outbox
                WHERE delivered_at IS NULL
                ORDER BY created_at,alert_id FOR UPDATE SKIP LOCKED LIMIT 1
                """, connection, transaction);
            await using var reader = await select.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                break;
            }
            var alertId = reader.GetInt64(0);
            var severity = reader.GetString(1);
            var code = reader.GetString(2);
            var occurrences = reader.GetInt32(3);
            await reader.DisposeAsync();
            Console.Error.WriteLine("SUITE CONTENT ALERT severity={0} code={1} occurrences={2}",
                severity, code, occurrences);
            await using var update = new NpgsqlCommand("""
                UPDATE suite.suite_content_alert_outbox
                SET delivered_at=clock_timestamp(),delivery_attempts=delivery_attempts+1
                WHERE alert_id=$1 AND delivered_at IS NULL
                """, connection, transaction);
            update.Parameters.AddWithValue(alertId);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new MonitorFailure("ALERT_DELIVERY_CONFLICT");
            await transaction.CommitAsync(ct);
            delivered++;
        }

        await using var backlog = dataSource.CreateCommand("""
            SELECT count(*) FROM suite.suite_content_alert_outbox WHERE delivered_at IS NULL
            """);
        var remaining = (long)(await backlog.ExecuteScalarAsync(ct) ?? 0L);
        if (remaining > 0)
            Console.Error.WriteLine("SUITE CONTENT ALERT BACKLOG count={0}", remaining);
        return delivered;
    }

    private static List<SnapshotItem> Transform(IReadOnlyList<SnapshotItem> source,
        SnapshotMutation mutation)
    {
        var result = new List<SnapshotItem>(source.Count);
        foreach (var item in source)
        {
            if (mutation.MaintenanceItems.ContainsKey(item.ItemId))
            {
                result.Add(item with
                {
                    Status = "MAINTENANCE",
                    ArtifactVersion = null,
                    ContentLength = null,
                    Sha256 = null,
                    SafeFileName = null,
                    FileExtension = null,
                    ExtractPolicy = null,
                    ContentType = null,
                    Etag = null,
                    LastModified = null,
                    MaintenanceReason = "CONTENT_TEMPORARILY_UNAVAILABLE",
                    OriginCiphertext = null,
                    OriginNonce = null,
                    OriginTag = null,
                    OriginKeyVersion = null
                });
                continue;
            }
            if (mutation.Candidate?.Candidate.ItemId == item.ItemId)
            {
                var candidate = mutation.Candidate;
                var version = candidate.Candidate.ChangeIntent switch
                {
                    "INITIAL_RECOVERY" => 1,
                    "NEW_ARTIFACT_VERSION" => checked(candidate.Candidate.ExpectedArtifactVersion!.Value + 1),
                    _ => candidate.Candidate.ExpectedArtifactVersion!.Value
                };
                result.Add(item with
                {
                    Status = "READY",
                    ArtifactVersion = version,
                    ContentLength = candidate.ContentLength,
                    Sha256 = candidate.Sha256,
                    SafeFileName = candidate.SafeFileName,
                    FileExtension = candidate.FileExtension,
                    ExtractPolicy = candidate.ExtractPolicy,
                    ContentType = candidate.ContentType,
                    Etag = candidate.Etag,
                    LastModified = candidate.LastModified,
                    MaintenanceReason = null,
                    OriginCiphertext = null,
                    OriginNonce = null,
                    OriginTag = null,
                    OriginKeyVersion = null
                });
                continue;
            }
            result.Add(item);
        }
        return result;
    }

    internal static int ResultingMaintenanceCount(IReadOnlyList<SnapshotItem> source,
        string catalogIdentity,
        IReadOnlyDictionary<string, MaintenancePromotion> promotions)
    {
        ValidateMaintenanceTargets(catalogIdentity, source, promotions);
        return source.Count(item => item.Status == "MAINTENANCE") + promotions.Count;
    }

    private static void ValidateMaintenanceTargets(string catalogIdentity,
        IReadOnlyList<SnapshotItem> source,
        IReadOnlyDictionary<string, MaintenancePromotion> promotions)
    {
        foreach (var promotion in promotions)
        {
            var item = source.SingleOrDefault(value => value.ItemId == promotion.Key);
            if (!string.Equals(promotion.Key, promotion.Value.ItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(catalogIdentity, promotion.Value.CatalogIdentity,
                    StringComparison.Ordinal) ||
                item is null || item.Status != "READY" ||
                item.ArtifactVersion != promotion.Value.ArtifactVersion)
                throw new MonitorFailure("CATALOG_CHANGED");
        }
    }

    internal static async Task ValidateMaintenanceTargetsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string catalogIdentity,
        IReadOnlyDictionary<string, MaintenancePromotion> promotions, CancellationToken ct)
    {
        foreach (var promotion in promotions.Values)
        {
            await using var command = new NpgsqlCommand("""
                SELECT 1
                FROM suite.suite_content_item_health h
                JOIN suite.suite_content_catalog_state cs ON cs.product_id=h.product_id
                JOIN suite.suite_content_items i
                  ON i.catalog_identity=cs.active_catalog_identity AND i.item_id=h.item_id
                WHERE h.product_id='TURBORAMA_SUITE' AND h.item_id=$1
                  AND h.observed_catalog_identity=$2 AND h.row_version=$3
                  AND cs.active_catalog_identity=$2 AND i.status='READY'
                  AND i.artifact_version=$4
                FOR UPDATE OF h
                """, connection, transaction);
            command.Parameters.AddWithValue(promotion.ItemId);
            command.Parameters.AddWithValue(catalogIdentity);
            command.Parameters.AddWithValue(promotion.HealthRowVersion);
            command.Parameters.AddWithValue(promotion.ArtifactVersion);
            if (await command.ExecuteScalarAsync(ct) is not int)
                throw new MonitorFailure("CATALOG_CHANGED");
        }
    }

    private static bool IsLowerHex(string value, int length) => value.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool FixedLowerHex(string left, string right)
    {
        if (!IsLowerHex(left, 64) || !IsLowerHex(right, 64)) return false;
        var first = Encoding.ASCII.GetBytes(left);
        var second = Encoding.ASCII.GetBytes(right);
        try { return CryptographicOperations.FixedTimeEquals(first, second); }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string eventType, string? itemId, string? candidateId,
        string correlationId, string outcome, string detailCode, string? previous,
        string? current, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_management_audit(
              event_type,actor,item_id,candidate_id,correlation_id,outcome,detail_code,
              previous_catalog_identity,new_catalog_identity)
            VALUES($1,'content-monitor',$2,$3,$4,$5,$6,$7,$8)
            """, connection, transaction);
        command.Parameters.AddWithValue(eventType);
        command.Parameters.AddWithValue((object?)itemId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)candidateId ?? DBNull.Value);
        command.Parameters.AddWithValue(correlationId);
        command.Parameters.AddWithValue(outcome);
        command.Parameters.AddWithValue(detailCode);
        command.Parameters.AddWithValue((object?)previous ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)current ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertAlertAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string? itemId, string severity, string code,
        string material, CancellationToken ct)
    {
        var key = Hash(code + "\0" + (itemId ?? string.Empty) + "\0" + material);
        await using var command = new NpgsqlCommand("""
            INSERT INTO suite.suite_content_alert_outbox(
              deduplication_key,item_id,severity,alert_code)
            VALUES($1,$2,$3,$4) ON CONFLICT(deduplication_key) DO UPDATE
              SET occurrence_count=suite_content_alert_outbox.occurrence_count+1
            """, connection, transaction);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue((object?)itemId ?? DBNull.Value);
        command.Parameters.AddWithValue(severity);
        command.Parameters.AddWithValue(code);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public void Dispose() => dataSource.Dispose();
}

internal sealed class CycleLock(NpgsqlConnection connection,
    string lockName = "suite:content-monitor-cycle") : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(hashtextextended($1,0))", connection);
            command.Parameters.AddWithValue(lockName);
            await command.ExecuteNonQueryAsync();
        }
        finally { await connection.DisposeAsync(); }
    }
}

internal static class HealthPolicy
{
    public const int FailureThreshold = 3;
    public const int MassFailureLimit = 25;
    public const int MaximumFullValidationsPerCycle = 2;
    public static readonly TimeSpan FullValidationInterval = TimeSpan.FromDays(7);
    public static bool OpensMassFailureGuard(int count) => count > MassFailureLimit;
    public static bool RequiresFullValidation(HealthTarget target, DateTime utcNow) =>
        !HasBodyBoundStrongEtag(target.ExpectedEtag) &&
        (target.LastFullValidationAt is null ||
         target.LastFullValidationAt <= utcNow.Subtract(FullValidationInterval));
    public static bool FullArtifactMatches(HealthTarget target, OriginMetadata full) =>
        target.ExpectedContentLength is long expectedLength &&
        target.ExpectedSha256 is string expectedSha256 &&
        full.ContentLength == expectedLength &&
        FixedHexEquals(expectedSha256, full.Sha256);
    public static bool ValidatorsMatch(HealthTarget target, string? observedEtag,
        string? observedLastModified) =>
        (target.ExpectedEtag is null || string.Equals(target.ExpectedEtag, observedEtag,
            StringComparison.Ordinal)) &&
        (target.ExpectedLastModified is null || string.Equals(target.ExpectedLastModified,
            observedLastModified, StringComparison.Ordinal));

    private static bool HasBodyBoundStrongEtag(string? value) =>
        value is { Length: >= 2 } && value[0] == '"' && value[^1] == '"' &&
        !value.StartsWith("W/", StringComparison.OrdinalIgnoreCase);

    private static bool FixedHexEquals(string expected, string observed)
    {
        byte[]? expectedBytes = null;
        byte[]? observedBytes = null;
        try
        {
            expectedBytes = Convert.FromHexString(expected);
            observedBytes = Convert.FromHexString(observed);
            return expectedBytes.Length == 32 && observedBytes.Length == 32 &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, observedBytes);
        }
        catch (FormatException) { return false; }
        finally
        {
            if (expectedBytes is not null) CryptographicOperations.ZeroMemory(expectedBytes);
            if (observedBytes is not null) CryptographicOperations.ZeroMemory(observedBytes);
        }
    }
}

internal static class ResultCodes
{
    public static string Health(string code) => code switch
    {
        "ORIGIN_TERMINAL_UNAVAILABLE" => code,
        "ORIGIN_TERMINAL_EMPTY" => code,
        "ORIGIN_REQUEST_FAILED" => code,
        "ORIGIN_RETRYABLE_STATUS" => code,
        "ORIGIN_LENGTH_MISMATCH" => code,
        "ORIGIN_CONTENT_TOO_LARGE" => "ORIGIN_LENGTH_MISMATCH",
        "ORIGIN_LENGTH_CHANGED" => code,
        "ORIGIN_HEADER_TIMEOUT" => code,
        "ORIGIN_READ_IDLE_TIMEOUT" => code,
        "ORIGIN_TOTAL_TIMEOUT" => code,
        _ => "ORIGIN_REQUEST_FAILED"
    };

    public static string Candidate(string code) => code switch
    {
        "UPSTREAM_URL_POLICY_DENIED" => "URL_POLICY_DENIED",
        "UPSTREAM_HOST_DENIED" => "HOST_DENIED",
        "UPSTREAM_DNS_ADDRESS_DENIED" => "DNS_ADDRESS_DENIED",
        "ORIGIN_TLS_SECURITY_FAILURE" => "TLS_SECURITY_FAILURE",
        "ORIGIN_REDIRECT_DENIED" => "REDIRECT_DENIED",
        "ORIGIN_CONTENT_ENCODING_DENIED" => "CONTENT_ENCODING_DENIED",
        "ORIGIN_TERMINAL_UNAVAILABLE" => "TERMINAL_UNAVAILABLE",
        "ORIGIN_TERMINAL_EMPTY" => "TERMINAL_EMPTY",
        "ORIGIN_REQUEST_FAILED" => "REQUEST_FAILED",
        "ORIGIN_RETRYABLE_STATUS" => "RETRYABLE_STATUS",
        "ORIGIN_HEADER_TIMEOUT" => "HEADER_TIMEOUT",
        "ORIGIN_READ_IDLE_TIMEOUT" => "READ_IDLE_TIMEOUT",
        "ORIGIN_TOTAL_TIMEOUT" => "TOTAL_TIMEOUT",
        "ORIGIN_LENGTH_CHANGED" => "LENGTH_CHANGED",
        "ORIGIN_LENGTH_MISMATCH" => "LENGTH_MISMATCH",
        "ORIGIN_CONTENT_INVALID" => "CONTENT_INVALID",
        "ORIGIN_CONTENT_TOO_LARGE" => "CONTENT_INVALID",
        "MIRROR_LENGTH_MISMATCH" => "MIRROR_LENGTH_MISMATCH",
        "MIRROR_SHA256_MISMATCH" => "MIRROR_SHA256_MISMATCH",
        "MIRROR_EXTENSION_MISMATCH" => "MIRROR_EXTENSION_MISMATCH",
        "SOURCE_CHANGED" => "SOURCE_CHANGED",
        _ => "REQUEST_FAILED"
    };

    public static bool IsSecurity(string code) => code is
        "UPSTREAM_URL_POLICY_DENIED" or "UPSTREAM_HOST_DENIED" or
        "UPSTREAM_DNS_ADDRESS_DENIED" or "ORIGIN_TLS_SECURITY_FAILURE" or
        "ORIGIN_REDIRECT_DENIED" or "ORIGIN_CONTENT_ENCODING_DENIED" or
        "ORIGIN_STATUS_DENIED";
}
