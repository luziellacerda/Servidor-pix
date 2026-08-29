using Npgsql;

namespace TurboRamaSuiteContentMonitor;

internal static class MonitorPostgresSelfTest
{
    public static async Task<int> RunAsync()
    {
        var connection = Environment.GetEnvironmentVariable("SUITE_TEST_CONNECTION") ??
            throw new InvalidOperationException("SUITE_TEST_CONNECTION missing");
        await using var dataSource = NpgsqlDataSource.Create(connection);
        using var store = new MonitorStore(connection);
        await CandidateLeaseOwnershipAsync(dataSource, store);
        await HealthCatalogCasAsync(dataSource, store);
        await SameVersionOriginSwapAfterHealthAsync(dataSource, store);
        await AlertOutboxDeliveryAsync(dataSource, store);
        Console.WriteLine("SUITE CONTENT MONITOR POSTGRES SELF-TEST: OK " +
            "(expired lease ownership CAS, stale worker denied, concurrent catalog health CAS, " +
            "new-origin health reset, same-version origin swap denied, sanitized alert outbox delivery)");
        return 0;
    }

    private static async Task CandidateLeaseOwnershipAsync(NpgsqlDataSource dataSource,
        MonitorStore store)
    {
        var snapshot = RandomHex(64);
        var candidateId = RandomHex(64);
        var itemId = RandomHex(32);
        var oldOwner = RandomHex(64);
        var newOwner = RandomHex(64);
        await ExecuteSetupAsync(dataSource, """
            INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,
              inventory_sha256,visual_catalog_sha256,item_count,ready_item_count,
              maintenance_item_count,status,origin_active_key_version,
              origin_key_set_fingerprint,origin_allowlist_fingerprint)
            VALUES($1,(SELECT coalesce(max(catalog_sequence),0)+1
              FROM suite.suite_content_snapshots),$2,$3,902,0,902,'STAGING',1,$4,$5);
            INSERT INTO suite.suite_content_origin_candidates(candidate_id,product_id,item_id,
              base_catalog_identity,request_id,state,upstream_url_ciphertext,
              upstream_url_nonce,upstream_url_tag,key_version,change_intent,
              expected_extract_policy,submitted_by,lease_owner,lease_expires_at)
            VALUES($6,'TURBORAMA_SUITE',$7,$1,$8,'VALIDATING',$9,$10,$11,1,
              'INITIAL_RECOVERY','NONE','postgres-self-test',$12,
              clock_timestamp()-interval '1 minute')
            """, snapshot, RandomHex(64), RandomHex(64), RandomHex(64), RandomHex(64),
            candidateId, itemId, RandomHex(32), new byte[] { 1 }, new byte[12],
            new byte[16], oldOwner);

        var stale = new CandidateLease(candidateId, itemId, snapshot, RandomHex(32),
            "VALIDATING", [1], new byte[12], new byte[16], 1, "INITIAL_RECOVERY", null,
            null, null, null, null, "NONE", "postgres-self-test", oldOwner);
        var stolen = await store.LeaseCandidatesAsync(newOwner, 1, CancellationToken.None);
        if (stolen.Count != 1 || stolen[0].CandidateId != candidateId ||
            stolen[0].LeaseOwner != newOwner)
            throw new InvalidOperationException("expired candidate lease was not acquired atomically");
        await ExpectLeaseLostAsync(() => store.MarkCandidateRejectedAsync(stale,
            "ORIGIN_REQUEST_FAILED", CancellationToken.None));
        await ExpectLeaseLostAsync(() => store.MarkCandidateSupersededAsync(stale,
            CancellationToken.None));
        await store.MarkCandidateRejectedAsync(stolen[0], "ORIGIN_REQUEST_FAILED",
            CancellationToken.None);
        await using var check = dataSource.CreateCommand("""
            SELECT state,lease_owner FROM suite.suite_content_origin_candidates
            WHERE candidate_id=$1
            """);
        check.Parameters.AddWithValue(candidateId);
        await using var row = await check.ExecuteReaderAsync();
        if (!await row.ReadAsync() || row.GetString(0) != "REJECTED" || !row.IsDBNull(1))
            throw new InvalidOperationException("lease owner transition did not finalize candidate");
    }

    private static async Task HealthCatalogCasAsync(NpgsqlDataSource dataSource,
        MonitorStore store)
    {
        var firstCatalog = RandomHex(64);
        var secondCatalog = RandomHex(64);
        var itemId = RandomHex(32);
        await ExecuteSetupAsync(dataSource, """
            INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,
              inventory_sha256,visual_catalog_sha256,item_count,ready_item_count,
              maintenance_item_count,status,published_at,origin_active_key_version,
              origin_key_set_fingerprint,origin_allowlist_fingerprint)
            VALUES
              ($1,(SELECT coalesce(max(catalog_sequence),0)+1 FROM suite.suite_content_snapshots),
               $3,$4,902,902,0,'STAGING',NULL,1,$5,$6),
              ($2,(SELECT coalesce(max(catalog_sequence),0)+2 FROM suite.suite_content_snapshots),
               $3,$4,902,902,0,'STAGING',NULL,1,$5,$6);
            INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,
              display_name,visual_extract_policy,artifact_id,artifact_version,content_length,
              sha256,safe_file_name,file_extension,extract_policy,manifest_identity,
              descriptor_hash,content_type,status)
            VALUES
              ($1,$7,1,'Postgres CAS A','NONE',$7,1,12,$8,'cas.zip','.zip','NONE',
               $1,$9,'application/octet-stream','READY'),
              ($2,$7,1,'Postgres CAS B','NONE',$7,2,12,$8,'cas.zip','.zip','NONE',
               $2,$9,'application/octet-stream','READY');
            UPDATE suite.suite_content_snapshots SET status='PUBLISHED',
              published_at=clock_timestamp() WHERE catalog_identity IN ($1,$2);
            INSERT INTO suite.suite_content_catalog_state(product_id,active_catalog_identity)
            VALUES('TURBORAMA_SUITE',$1)
            ON CONFLICT(product_id) DO UPDATE SET active_catalog_identity=excluded.active_catalog_identity;
            INSERT INTO suite.suite_content_item_health(product_id,item_id,
              observed_catalog_identity,observed_availability,consecutive_terminal_failures,
              consecutive_successes,last_terminal_failure_at,last_full_validation_at,
              last_result_code,next_check_at,row_version)
            VALUES('TURBORAMA_SUITE',$7,$1,'READY',2,4,clock_timestamp(),
              clock_timestamp(),'ORIGIN_REQUEST_FAILED',clock_timestamp(),1)
            """, firstCatalog, secondCatalog, RandomHex(64), RandomHex(64), RandomHex(64),
            RandomHex(64), itemId, new string('a', 64), new string('b', 64));

        var target = new HealthTarget(itemId, firstCatalog, 1, 12, new string('a', 64),
            ".zip", null, null, [], [], [], 1, 2, null, null, 1);
        await using (var publish = dataSource.CreateCommand("""
            UPDATE suite.suite_content_catalog_state SET active_catalog_identity=$1,
              updated_at=clock_timestamp() WHERE product_id='TURBORAMA_SUITE'
            """))
        {
            publish.Parameters.AddWithValue(secondCatalog);
            await publish.ExecuteNonQueryAsync();
        }
        var stalePromotions = await store.ApplyHealthResultsAsync(
            [new HealthProbeResult(target, false, false, false, "ORIGIN_REQUEST_FAILED")],
            CancellationToken.None);
        if (stalePromotions.Count != 0)
            throw new InvalidOperationException("stale health result promoted a newer catalog");
        await using var check = dataSource.CreateCommand("""
            SELECT consecutive_terminal_failures,row_version
            FROM suite.suite_content_item_health
            WHERE product_id='TURBORAMA_SUITE' AND item_id=$1
            """);
        check.Parameters.AddWithValue(itemId);
        await using var row = await check.ExecuteReaderAsync();
        if (!await row.ReadAsync() || row.GetInt32(0) != 2 || row.GetInt64(1) != 1)
            throw new InvalidOperationException("stale health result changed health row");
    }

    private static async Task SameVersionOriginSwapAfterHealthAsync(NpgsqlDataSource dataSource,
        MonitorStore store)
    {
        var firstCatalog = RandomHex(64);
        var secondCatalog = RandomHex(64);
        var itemId = RandomHex(32);
        await ExecuteSetupAsync(dataSource, """
            INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,
              inventory_sha256,visual_catalog_sha256,item_count,ready_item_count,
              maintenance_item_count,status,published_at,origin_active_key_version,
              origin_key_set_fingerprint,origin_allowlist_fingerprint)
            VALUES
              ($1,(SELECT coalesce(max(catalog_sequence),0)+1 FROM suite.suite_content_snapshots),
               $3,$4,902,902,0,'STAGING',NULL,1,$5,$6),
              ($2,(SELECT coalesce(max(catalog_sequence),0)+2 FROM suite.suite_content_snapshots),
               $3,$4,902,902,0,'STAGING',NULL,1,$5,$6);
            INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,
              display_name,visual_extract_policy,artifact_id,artifact_version,content_length,
              sha256,safe_file_name,file_extension,extract_policy,manifest_identity,
              descriptor_hash,content_type,status)
            VALUES
              ($1,$7,1,'Old origin','NONE',$7,7,12,$8,'same.zip','.zip','NONE',
               $1,$9,'application/octet-stream','READY'),
              ($2,$7,1,'Corrected mirror','NONE',$7,7,12,$8,'same.zip','.zip','NONE',
               $2,$9,'application/octet-stream','READY');
            UPDATE suite.suite_content_snapshots SET status='PUBLISHED',
              published_at=clock_timestamp() WHERE catalog_identity IN ($1,$2);
            UPDATE suite.suite_content_catalog_state SET active_catalog_identity=$1,
              updated_at=clock_timestamp() WHERE product_id='TURBORAMA_SUITE';
            INSERT INTO suite.suite_content_item_health(product_id,item_id,
              observed_catalog_identity,observed_availability,consecutive_terminal_failures,
              next_check_at,row_version)
            VALUES('TURBORAMA_SUITE',$7,$1,'READY',2,clock_timestamp(),1)
            """, firstCatalog, secondCatalog, RandomHex(64), RandomHex(64), RandomHex(64),
            RandomHex(64), itemId, new string('c', 64), new string('d', 64));

        var target = new HealthTarget(itemId, firstCatalog, 7, 12, new string('c', 64),
            ".zip", null, null, [], [], [], 1, 2, null, null, 1);
        var promotions = await store.ApplyHealthResultsAsync(
            [new HealthProbeResult(target, false, false, false, "ORIGIN_REQUEST_FAILED")],
            CancellationToken.None);
        if (promotions.Count != 1)
            throw new InvalidOperationException("health threshold did not produce promotion token");

        await using (var publish = dataSource.CreateCommand("""
            UPDATE suite.suite_content_catalog_state SET active_catalog_identity=$1,
              updated_at=clock_timestamp() WHERE product_id='TURBORAMA_SUITE'
            """))
        {
            publish.Parameters.AddWithValue(secondCatalog);
            await publish.ExecuteNonQueryAsync();
        }
        await store.SynchronizeHealthAsync(CancellationToken.None);
        await using (var resetCheck = dataSource.CreateCommand("""
            SELECT observed_catalog_identity,consecutive_terminal_failures,
              consecutive_successes,last_terminal_failure_at,last_full_validation_at,
              last_result_code,row_version,next_check_at<=clock_timestamp()
            FROM suite.suite_content_item_health
            WHERE product_id='TURBORAMA_SUITE' AND item_id=$1
            """))
        {
            resetCheck.Parameters.AddWithValue(itemId);
            await using var resetRow = await resetCheck.ExecuteReaderAsync();
            if (!await resetRow.ReadAsync() || resetRow.GetString(0) != secondCatalog ||
                resetRow.GetInt32(1) != 0 || resetRow.GetInt32(2) != 0 ||
                !resetRow.IsDBNull(3) || !resetRow.IsDBNull(4) ||
                resetRow.GetString(5) != "NOT_CHECKED" || resetRow.GetInt64(6) != 3 ||
                !resetRow.GetBoolean(7))
                throw new InvalidOperationException(
                    "new origin inherited health state from the previous catalog");
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await MonitorStore.ValidateMaintenanceTargetsAsync(connection, transaction,
                firstCatalog, promotions, CancellationToken.None);
        }
        catch (MonitorFailure exception) when (exception.Code == "CATALOG_CHANGED")
        {
            await transaction.RollbackAsync();
            return;
        }
        await transaction.RollbackAsync();
        throw new InvalidOperationException(
            "old-origin health result accepted after same-version mirror publication");
    }

    private static async Task AlertOutboxDeliveryAsync(NpgsqlDataSource dataSource,
        MonitorStore store)
    {
        var deduplicationKey = RandomHex(64);
        await using (var insert = dataSource.CreateCommand("""
            INSERT INTO suite.suite_content_alert_outbox(
              deduplication_key,severity,alert_code)
            VALUES($1,'WARNING','CONTENT_CANDIDATE_REJECTED')
            """))
        {
            insert.Parameters.AddWithValue(deduplicationKey);
            await insert.ExecuteNonQueryAsync();
        }
        if (await store.DispatchPendingAlertsAsync(100, CancellationToken.None) < 1)
            throw new InvalidOperationException("pending alert was not dispatched");
        await using var check = dataSource.CreateCommand("""
            SELECT delivered_at IS NOT NULL,delivery_attempts
            FROM suite.suite_content_alert_outbox WHERE deduplication_key=$1
            """);
        check.Parameters.AddWithValue(deduplicationKey);
        await using var row = await check.ExecuteReaderAsync();
        if (!await row.ReadAsync() || !row.GetBoolean(0) || row.GetInt32(1) != 1)
            throw new InvalidOperationException("alert delivery was not acknowledged exactly once");
    }

    private static async Task ExpectLeaseLostAsync(Func<Task> action)
    {
        try { await action(); }
        catch (MonitorFailure exception) when (exception.Code == "CANDIDATE_LEASE_LOST")
        {
            return;
        }
        throw new InvalidOperationException("stale candidate worker transition was accepted");
    }

    private static async Task ExecuteSetupAsync(NpgsqlDataSource dataSource, string sql,
        params object[] parameters)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            await using var command = new NpgsqlCommand(statement, connection, transaction);
            var highestParameter = 0;
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(statement, @"\$(\d+)"))
                highestParameter = Math.Max(highestParameter, int.Parse(match.Groups[1].Value));
            for (var index = 0; index < highestParameter; index++)
                command.Parameters.AddWithValue(parameters[index]);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static string RandomHex(int length) => Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(length / 2)).ToLowerInvariant();
}
