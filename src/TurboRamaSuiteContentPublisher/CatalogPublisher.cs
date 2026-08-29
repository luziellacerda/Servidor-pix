using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace TurboRamaSuiteContentPublisher;

internal static class CatalogPublisher
{
    internal const string MaintenanceReason = "CONTENT_TEMPORARILY_UNAVAILABLE";
    internal const int MaximumMaintenanceItems = 25;

    public static async Task<int> ValidateAsync(PublisherOptions options, CancellationToken cancellationToken)
    {
        var policy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!, cancellationToken);
        var catalog = await CatalogLoader.LoadAsync(options, policy, cancellationToken);
        RequireProductionCardinality(catalog, options);
        var fatal = 0;
        var maintenance = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in catalog.Items)
        {
            foreach (var code in item.ValidationCodes.Distinct(StringComparer.Ordinal))
            {
                if (IsMaintenanceCode(code))
                {
                    maintenance.Add(item.ItemId);
                    Console.Error.WriteLine($"VALIDATE item={item.ItemId} state=maintenance code={code}");
                }
                else
                {
                    fatal++;
                    Console.Error.WriteLine($"VALIDATE item={item.ItemId} state=blocked code={code}");
                }
            }
        }
        if (fatal != 0) throw new PublisherFailure("CATALOG_VALIDATION_BLOCKED");
        RequireMaintenanceBelowMassFailureThreshold(maintenance.Count);
        Console.WriteLine($"SUITE CONTENT VALIDATION: OK items=902 maintenance_candidates={maintenance.Count} rejected_extras=0 urls=private");
        return 0;
    }

    public static async Task<int> ProbeAsync(PublisherOptions options, CancellationToken cancellationToken)
    {
        var policy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!, cancellationToken);
        var catalog = await CatalogLoader.LoadAsync(options, policy, cancellationToken);
        RequireProductionCardinality(catalog, options);
        var batch = await ProbeAllAsync(catalog, policy, options.MaximumConcurrency, cancellationToken);
        if (batch.FatalFailures != 0) throw new PublisherFailure("PROBE_BATCH_BLOCKED");
        var bytes = batch.Probes.Values.Aggregate(0L, (total, item) => checked(total + item.ContentLength));
        Console.WriteLine($"SUITE CONTENT PROBE: OK total=902 ready={batch.Probes.Count} maintenance={batch.MaintenanceItems.Count} ready_bytes={bytes} urls=private");
        return 0;
    }

    public static async Task<int> PublishAsync(PublisherOptions options, CancellationToken cancellationToken)
    {
        var policy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!, cancellationToken);
        using (var preflightKeyRing = await ContentKeyRing.LoadAsync(options.KeyRingPath!,
                   cancellationToken))
            await RequireGatewayKeyRingAsync(options.GatewayKeyRingReadinessUri!,
                preflightKeyRing, policy, cancellationToken);
        var catalog = await CatalogLoader.LoadAsync(options, policy, cancellationToken);
        RequireProductionCardinality(catalog, options);
        var probes = await ProbeAllAsync(catalog, policy, options.MaximumConcurrency, cancellationToken);
        if (probes.FatalFailures != 0) throw new PublisherFailure("PROBE_BATCH_BLOCKED");
        using var journal = await JournalStore.OpenAsync(options.JournalPath!, catalog.InventorySha256,
            catalog.VisualCatalogSha256, cancellationToken);
        var plan = await HashAllAsync(catalog, probes, journal, policy, options.MaximumConcurrency,
            cancellationToken);
        RequireCompletePlan(catalog, plan);

        var currentPolicy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!,
            cancellationToken);
        if (!policy.HasSameDeploymentFingerprint(currentPolicy))
            throw new PublisherFailure("ALLOWED_HOSTS_CHANGED_DURING_PUBLISH");
        using var keyRing = await ContentKeyRing.LoadAsync(options.KeyRingPath!, cancellationToken);
        await RequireGatewayKeyRingAsync(options.GatewayKeyRingReadinessUri!,
            keyRing, currentPolicy, cancellationToken);
        var connectionString = await ConnectionSecret.LoadAsync(options.ConnectionFilePath!, cancellationToken);
        var allowlistFingerprint = currentPolicy.CopyDeploymentFingerprint();
        try
        {
            var allowlistFingerprintHex = Convert.ToHexString(
                allowlistFingerprint).ToLowerInvariant();
            var catalogIdentity = CanonicalIdentity.CatalogIdentity(
                catalog, plan.ReadyItems, plan.MaintenanceItems,
                keyRing.ActiveVersion, keyRing.KeySetFingerprint,
                allowlistFingerprintHex);
            await PersistAsync(connectionString, catalog, plan, catalogIdentity, keyRing,
                allowlistFingerprintHex, cancellationToken);
            Console.WriteLine($"SUITE CONTENT PUBLISH: OK total=902 ready={plan.ReadyItems.Count} maintenance={plan.MaintenanceItems.Count} catalog={catalogIdentity} urls=encrypted");
        }
        finally { CryptographicOperations.ZeroMemory(allowlistFingerprint); }
        return 0;
    }

    public static async Task<int> PublishDirectAsync(
        PublisherOptions options, CancellationToken cancellationToken)
    {
        var policy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!, cancellationToken);
        using (var preflightKeyRing = await ContentKeyRing.LoadAsync(options.KeyRingPath!,
                   cancellationToken))
            await RequireGatewayKeyRingAsync(options.GatewayKeyRingReadinessUri!,
                preflightKeyRing, policy, cancellationToken);
        var catalog = await CatalogLoader.LoadAsync(options, policy, cancellationToken);
        RequireProductionCardinality(catalog, options);
        var probes = await ProbeAllAsync(catalog, policy, options.MaximumConcurrency,
            cancellationToken);
        if (probes.FatalFailures != 0) throw new PublisherFailure("PROBE_BATCH_BLOCKED");

        var ready = catalog.Items.Where(item => probes.Probes.ContainsKey(item.ItemId))
            .Select(item =>
            {
                var probe = probes.Probes[item.ItemId];
                return new VerifiedItem(item.ItemId, item.DisplayOrder, probe.ContentLength,
                    new string('0', 64), CatalogLoader.SafeFileName(item), item.DeclaredExtension,
                    item.ExtractPolicy, probe.Etag, probe.LastModified, probe.ContentType);
            }).ToArray();
        var maintenanceIds = probes.MaintenanceItems
            .Select(item => item.ItemId).ToHashSet(StringComparer.Ordinal);
        var maintenance = catalog.Items.Where(item => maintenanceIds.Contains(item.ItemId))
            .Select(item => new MaintenanceItem(item.ItemId, item.DisplayOrder,
                MaintenanceReason)).ToArray();
        var plan = new PublicationPlan(ready, maintenance);
        RequireCompletePlan(catalog, plan);

        var currentPolicy = await OriginPolicy.LoadAsync(options.AllowedHostsPath!,
            cancellationToken);
        if (!policy.HasSameDeploymentFingerprint(currentPolicy))
            throw new PublisherFailure("ALLOWED_HOSTS_CHANGED_DURING_PUBLISH");
        using var keyRing = await ContentKeyRing.LoadAsync(options.KeyRingPath!, cancellationToken);
        await RequireGatewayKeyRingAsync(options.GatewayKeyRingReadinessUri!, keyRing,
            currentPolicy, cancellationToken);
        var connectionString = await ConnectionSecret.LoadAsync(options.ConnectionFilePath!,
            cancellationToken);
        var allowlistFingerprint = currentPolicy.CopyDeploymentFingerprint();
        try
        {
            var allowlistFingerprintHex = Convert.ToHexString(allowlistFingerprint).ToLowerInvariant();
            var catalogIdentity = CanonicalIdentity.CatalogIdentity(catalog, ready, maintenance,
                keyRing.ActiveVersion, keyRing.KeySetFingerprint, allowlistFingerprintHex);
            await PersistAsync(connectionString, catalog, plan, catalogIdentity, keyRing,
                allowlistFingerprintHex, cancellationToken);
            Console.WriteLine($"SUITE CONTENT DIRECT PUBLISH: OK total=902 ready={ready.Length} maintenance={maintenance.Length} catalog={catalogIdentity} urls=encrypted hashes=deferred-to-client");
        }
        finally { CryptographicOperations.ZeroMemory(allowlistFingerprint); }
        return 0;
    }

    private static async Task RequireGatewayKeyRingAsync(
        Uri readinessUri,
        ContentKeyRing keyRing,
        OriginPolicy originPolicy,
        CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var nonce = RandomNumberGenerator.GetBytes(32);
        var proof = Array.Empty<byte>();
        var allowlistFingerprint = Array.Empty<byte>();
        var body = Array.Empty<byte>();
        try
        {
            allowlistFingerprint = originPolicy.CopyDeploymentFingerprint();
            proof = keyRing.CreateGatewayProof(nonce, allowlistFingerprint);
            body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                activeKeyVersion = keyRing.ActiveVersion,
                keySetFingerprint = keyRing.KeySetFingerprint,
                allowlistFingerprint = Convert.ToHexString(
                    allowlistFingerprint).ToLowerInvariant(),
                nonce = Convert.ToBase64String(nonce),
                proof = Convert.ToBase64String(proof)
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, readinessUri)
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.NoContent)
                throw new PublisherFailure("GATEWAY_KEY_RING_PREFLIGHT_FAILED");
        }
        catch (PublisherFailure) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or
                                           TaskCanceledException or JsonException)
        {
            throw new PublisherFailure("GATEWAY_KEY_RING_PREFLIGHT_FAILED");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            if (allowlistFingerprint.Length != 0)
                CryptographicOperations.ZeroMemory(allowlistFingerprint);
            if (proof.Length != 0) CryptographicOperations.ZeroMemory(proof);
            if (body.Length != 0) CryptographicOperations.ZeroMemory(body);
        }
    }

    public static async Task<int> ReconcileEntitlementsAsync(
        PublisherOptions options,
        CancellationToken cancellationToken)
    {
        var connectionString = await ConnectionSecret.LoadAsync(options.ConnectionFilePath!, cancellationToken);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(
            "SELECT suite.reconcile_suite_content_entitlements()");
        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            var affected = result is long count ? count : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine($"SUITE CONTENT ENTITLEMENT RECONCILIATION: OK eligible_commerce_rows={affected}");
            return 0;
        }
        catch (PostgresException) { throw new PublisherFailure("ENTITLEMENT_RECONCILIATION_FAILED"); }
        catch (NpgsqlException) { throw new PublisherFailure("DATABASE_UNAVAILABLE"); }
    }

    private static void RequireProductionCardinality(PreparedCatalog catalog, PublisherOptions options)
    {
        if (options.ExpectedItemCount != 902 || options.ExpectedRejectedExtraCount != 0 ||
            catalog.Items.Count != 902 || catalog.RejectedExtraCount != 0)
            throw new PublisherFailure("PRODUCTION_CATALOG_REQUIRES_902_ITEMS");
    }

    private static void RequireCompletePlan(PreparedCatalog catalog, PublicationPlan plan)
    {
        var identities = plan.ReadyItems.Select(item => item.ItemId)
            .Concat(plan.MaintenanceItems.Select(item => item.ItemId)).ToArray();
        if (identities.Length != 902 || identities.Distinct(StringComparer.Ordinal).Count() != 902 ||
            identities.Except(catalog.Items.Select(item => item.ItemId), StringComparer.Ordinal).Any() ||
            catalog.Items.Select(item => item.ItemId).Except(identities, StringComparer.Ordinal).Any())
            throw new PublisherFailure("PUBLICATION_PLAN_INCOMPLETE");
    }

    internal static bool IsMaintenanceCode(string code)
        => code is "UPSTREAM_URL_INVALID" or "FILE_EXTENSION_INVALID" or
            "FILE_EXTENSION_UNSUPPORTED" or "EXTRACT_POLICY_EXTENSION_MISMATCH" or
            "ORIGIN_TERMINAL_UNAVAILABLE" or
            "ORIGIN_TERMINAL_EMPTY" or "ORIGIN_LENGTH_MISSING" or "ORIGIN_RANGE_INVALID" or
            "ORIGIN_CONTENT_TOO_LARGE" or
            "ORIGIN_REQUEST_FAILED" or "ORIGIN_RETRYABLE_STATUS" or "ORIGIN_HEADER_TIMEOUT" or
            "ORIGIN_READ_IDLE_TIMEOUT" or "ORIGIN_TOTAL_TIMEOUT" or "ORIGIN_LENGTH_CHANGED" or
            "ORIGIN_LENGTH_MISMATCH" or "ORIGIN_CONTENT_INVALID" or "ORIGIN_STATUS_DENIED";

    private static void RequireMaintenanceBelowMassFailureThreshold(int count)
    {
        if (count <= MaximumMaintenanceItems) return;
        Console.Error.WriteLine($"MAINTENANCE GUARD: blocked maintenance={count} maximum={MaximumMaintenanceItems} active_snapshot=preserved");
        throw new PublisherFailure("MASS_FAILURE_GUARD_TRIGGERED");
    }

    private static async Task<ProbeBatch> ProbeAllAsync(
        PreparedCatalog catalog,
        OriginPolicy policy,
        int maximumConcurrency,
        CancellationToken cancellationToken)
    {
        var probes = new ConcurrentDictionary<string, OriginMetadata>(StringComparer.Ordinal);
        var maintenance = new ConcurrentDictionary<string, MaintenanceItem>(StringComparer.Ordinal);
        var fatalCount = 0;
        var processed = 0;
        await Parallel.ForEachAsync(catalog.Items,
            new ParallelOptions { MaxDegreeOfParallelism = maximumConcurrency, CancellationToken = cancellationToken },
            async (item, ct) =>
            {
                var failures = new List<string>(item.ValidationCodes);
                var uriPolicyFailed = failures.Any(code =>
                    code is "UPSTREAM_URL_INVALID" or "UPSTREAM_URL_POLICY_DENIED" or "UPSTREAM_HOST_DENIED");
                if (item.UpstreamUri is not null && !uriPolicyFailed)
                {
                    try
                    {
                        using var verifier = new OriginVerifier(policy);
                        probes[item.ItemId] = await verifier.ProbeOnlyAsync(item.UpstreamUri, ct);
                    }
                    catch (PublisherFailure ex) { failures.Add(ex.Code); }
                }

                var distinct = failures.Distinct(StringComparer.Ordinal).ToArray();
                var fatal = distinct.Where(code => !IsMaintenanceCode(code)).ToArray();
                var count = Interlocked.Increment(ref processed);
                if (fatal.Length != 0)
                {
                    probes.TryRemove(item.ItemId, out _);
                    Interlocked.Add(ref fatalCount, fatal.Length);
                    foreach (var code in distinct)
                        Console.Error.WriteLine($"PROBE {count}/902 item={item.ItemId} state=blocked code={code}");
                }
                else if (distinct.Length != 0)
                {
                    probes.TryRemove(item.ItemId, out _);
                    maintenance[item.ItemId] = new MaintenanceItem(item.ItemId, item.DisplayOrder, MaintenanceReason);
                    foreach (var code in distinct)
                        Console.Error.WriteLine($"PROBE {count}/902 item={item.ItemId} state=maintenance code={code}");
                }
                else
                    Console.WriteLine($"PROBE {count}/902 item={item.ItemId} state=ready bytes={probes[item.ItemId].ContentLength}");
            });
        if (maintenance.Count > MaximumMaintenanceItems)
        {
            Interlocked.Increment(ref fatalCount);
            Console.Error.WriteLine($"PROBE item=batch state=blocked code=MASS_FAILURE_GUARD_TRIGGERED maintenance={maintenance.Count} maximum={MaximumMaintenanceItems}");
        }
        if (fatalCount != 0)
            Console.Error.WriteLine($"PROBE SUMMARY: blocked fatal_failures={fatalCount} ready={probes.Count} maintenance={maintenance.Count} required=902");
        else
            Console.WriteLine($"PROBE SUMMARY: complete ready={probes.Count} maintenance={maintenance.Count} required=902");
        return new ProbeBatch(probes, maintenance.Values.ToArray(), fatalCount);
    }

    private static async Task<PublicationPlan> HashAllAsync(
        PreparedCatalog catalog,
        ProbeBatch probes,
        JournalStore journal,
        OriginPolicy policy,
        int maximumConcurrency,
        CancellationToken cancellationToken)
    {
        var completed = new ConcurrentDictionary<string, VerifiedItem>(StringComparer.Ordinal);
        var maintenance = new ConcurrentDictionary<string, MaintenanceItem>(
            probes.MaintenanceItems.ToDictionary(item => item.ItemId, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var failures = new ConcurrentBag<(string ItemId, string Code)>();
        var readySources = catalog.Items.Where(item => probes.Probes.ContainsKey(item.ItemId)).ToArray();
        var processed = 0;
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Parallel.ForEachAsync(readySources,
                new ParallelOptions { MaxDegreeOfParallelism = maximumConcurrency, CancellationToken = abort.Token },
                async (item, ct) =>
                {
                    var probe = probes.Probes[item.ItemId];
                    if (journal.TryGet(item, probe, out var prior))
                    {
                        completed[item.ItemId] = prior;
                        var priorCount = Interlocked.Increment(ref processed);
                        Console.WriteLine($"HASH {priorCount}/{readySources.Length} item={item.ItemId} state=journal");
                        return;
                    }
                    try
                    {
                        using var verifier = new OriginVerifier(policy);
                        var metadata = await verifier.HashOnlyAsync(
                            item.UpstreamUri!, probe, item.DeclaredExtension, ct);
                        var result = new VerifiedItem(
                            item.ItemId,
                            item.DisplayOrder,
                            metadata.ContentLength,
                            metadata.Sha256,
                            CatalogLoader.SafeFileName(item),
                            item.DeclaredExtension,
                            item.ExtractPolicy,
                            metadata.Etag,
                            metadata.LastModified,
                            metadata.ContentType);
                        await journal.RecordAsync(result, ct);
                        completed[item.ItemId] = result;
                        var count = Interlocked.Increment(ref processed);
                        Console.WriteLine($"HASH {count}/{readySources.Length} item={item.ItemId} state=verified bytes={metadata.ContentLength}");
                    }
                    catch (PublisherFailure ex) when (IsMaintenanceCode(ex.Code))
                    {
                        maintenance[item.ItemId] = new MaintenanceItem(item.ItemId, item.DisplayOrder, MaintenanceReason);
                        var count = Interlocked.Increment(ref processed);
                        Console.Error.WriteLine($"HASH {count}/{readySources.Length} item={item.ItemId} state=maintenance code={ex.Code}");
                    }
                    catch (PublisherFailure ex)
                    {
                        failures.Add((item.ItemId, ex.Code));
                        var count = Interlocked.Increment(ref processed);
                        Console.Error.WriteLine($"HASH {count}/{readySources.Length} item={item.ItemId} state=blocked code={ex.Code}");
                        abort.Cancel();
                    }
                });
        }
        catch (OperationCanceledException) when (!failures.IsEmpty && !cancellationToken.IsCancellationRequested) { }
        if (!failures.IsEmpty)
        {
            Console.Error.WriteLine($"HASH SUMMARY: blocked fatal_failures={failures.Count} ready={completed.Count} maintenance={maintenance.Count}");
            throw new PublisherFailure("HASH_BATCH_BLOCKED");
        }
        RequireMaintenanceBelowMassFailureThreshold(maintenance.Count);
        return new PublicationPlan(
            catalog.Items.Where(item => completed.ContainsKey(item.ItemId)).Select(item => completed[item.ItemId]).ToArray(),
            catalog.Items.Where(item => maintenance.ContainsKey(item.ItemId)).Select(item => maintenance[item.ItemId]).ToArray());
    }

    private static async Task PersistAsync(
        string connectionString,
        PreparedCatalog catalog,
        PublicationPlan plan,
        string catalogIdentity,
        ContentKeyRing keyRing,
        string allowlistFingerprint,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable,
                cancellationToken);
            await AdvisoryLockAsync(connection, transaction, cancellationToken);

            await using (var existing = new NpgsqlCommand("""
                SELECT status,item_count,ready_item_count,maintenance_item_count,
                  origin_active_key_version,btrim(origin_key_set_fingerprint),
                  btrim(origin_allowlist_fingerprint)
                FROM suite.suite_content_snapshots WHERE catalog_identity=$1
                """, connection, transaction))
            {
                existing.Parameters.AddWithValue(catalogIdentity);
                await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var status = reader.GetString(0);
                    var count = reader.GetInt32(1);
                    var readyCount = reader.GetInt32(2);
                    var maintenanceCount = reader.GetInt32(3);
                    if (status != "PUBLISHED" || count != 902 || readyCount != plan.ReadyItems.Count ||
                        maintenanceCount != plan.MaintenanceItems.Count || reader.IsDBNull(4) ||
                        reader.GetInt32(4) != keyRing.ActiveVersion || reader.IsDBNull(5) ||
                        !CatalogLoader.FixedAscii(reader.GetString(5), keyRing.KeySetFingerprint) ||
                        reader.IsDBNull(6) ||
                        !CatalogLoader.FixedAscii(reader.GetString(6), allowlistFingerprint))
                        throw new PublisherFailure("EXISTING_SNAPSHOT_CONFLICT");
                    await reader.DisposeAsync();
                    await ActivateAsync(connection, transaction, catalogIdentity, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }
            }

            long sequence;
            await using (var next = new NpgsqlCommand(
                "SELECT coalesce(max(catalog_sequence),0)+1 FROM suite.suite_content_snapshots",
                connection, transaction))
                sequence = (long)(await next.ExecuteScalarAsync(cancellationToken) ?? 1L);

            await using (var snapshot = new NpgsqlCommand("""
                INSERT INTO suite.suite_content_snapshots(catalog_identity,catalog_sequence,inventory_sha256,
                  visual_catalog_sha256,item_count,ready_item_count,maintenance_item_count,status,
                  origin_active_key_version,origin_key_set_fingerprint,origin_allowlist_fingerprint)
                VALUES($1,$2,$3,$4,902,$5,$6,'STAGING',$7,$8,$9)
                """, connection, transaction))
            {
                snapshot.Parameters.AddWithValue(catalogIdentity);
                snapshot.Parameters.AddWithValue(sequence);
                snapshot.Parameters.AddWithValue(catalog.InventorySha256);
                snapshot.Parameters.AddWithValue(catalog.VisualCatalogSha256);
                snapshot.Parameters.AddWithValue(plan.ReadyItems.Count);
                snapshot.Parameters.AddWithValue(plan.MaintenanceItems.Count);
                snapshot.Parameters.AddWithValue(keyRing.ActiveVersion);
                snapshot.Parameters.AddWithValue(keyRing.KeySetFingerprint);
                snapshot.Parameters.AddWithValue(allowlistFingerprint);
                await snapshot.ExecuteNonQueryAsync(cancellationToken);
            }

            var ready = plan.ReadyItems.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
            var maintenance = plan.MaintenanceItems.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
            foreach (var source in catalog.Items)
            {
                if (maintenance.TryGetValue(source.ItemId, out var unavailable))
                {
                    await using var insertMaintenance = new NpgsqlCommand("""
                        INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,
                          display_name,visual_extract_policy,status,maintenance_reason)
                        VALUES($1,$2,$3,$4,$5,'MAINTENANCE',$6)
                        """, connection, transaction);
                    insertMaintenance.Parameters.AddWithValue(catalogIdentity);
                    insertMaintenance.Parameters.AddWithValue(unavailable.ItemId);
                    insertMaintenance.Parameters.AddWithValue(unavailable.DisplayOrder);
                    insertMaintenance.Parameters.AddWithValue(source.Title);
                    insertMaintenance.Parameters.AddWithValue(source.ExtractPolicy);
                    insertMaintenance.Parameters.AddWithValue(unavailable.ReasonCode);
                    await insertMaintenance.ExecuteNonQueryAsync(cancellationToken);
                    continue;
                }

                var item = ready[source.ItemId];
                var descriptorHash = CanonicalIdentity.DescriptorHash(item, catalogIdentity);
                var encrypted = keyRing.Encrypt(source.UpstreamUri!, catalogIdentity, item.ItemId, item.ItemId, 1,
                    catalogIdentity);
                try
                {
                    await using var insert = new NpgsqlCommand("""
                        INSERT INTO suite.suite_content_items(catalog_identity,item_id,display_order,
                          display_name,visual_extract_policy,artifact_id,
                          artifact_version,content_length,sha256,safe_file_name,file_extension,extract_policy,
                          manifest_identity,descriptor_hash,content_type,source_etag,source_last_modified,status)
                        VALUES($1,$2,$3,$13,$8,$2,1,$4,$5,$6,$7,$8,$1,$9,$10,$11,$12,'READY')
                        """, connection, transaction);
                    insert.Parameters.AddWithValue(catalogIdentity);
                    insert.Parameters.AddWithValue(item.ItemId);
                    insert.Parameters.AddWithValue(item.DisplayOrder);
                    insert.Parameters.Add(new NpgsqlParameter
                    {
                        NpgsqlDbType = NpgsqlDbType.Bigint,
                        Value = DBNull.Value
                    });
                    insert.Parameters.Add(new NpgsqlParameter
                    {
                        NpgsqlDbType = NpgsqlDbType.Char,
                        Value = DBNull.Value
                    });
                    insert.Parameters.AddWithValue(item.SafeFileName);
                    insert.Parameters.AddWithValue(item.FileExtension);
                    insert.Parameters.AddWithValue(item.ExtractPolicy);
                    insert.Parameters.AddWithValue(descriptorHash);
                    insert.Parameters.AddWithValue(item.ContentType);
                    insert.Parameters.AddWithValue((object?)item.SourceEtag ?? DBNull.Value);
                    insert.Parameters.AddWithValue((object?)item.SourceLastModified ?? DBNull.Value);
                    insert.Parameters.AddWithValue(source.Title);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                    await using var origin = new NpgsqlCommand("""
                        INSERT INTO suite.suite_content_artifact_origins(catalog_identity,item_id,
                          upstream_url_ciphertext,upstream_url_nonce,upstream_url_tag,key_version)
                        VALUES($1,$2,$3,$4,$5,$6)
                        """, connection, transaction);
                    origin.Parameters.AddWithValue(catalogIdentity);
                    origin.Parameters.AddWithValue(item.ItemId);
                    origin.Parameters.AddWithValue(encrypted.Ciphertext);
                    origin.Parameters.AddWithValue(encrypted.Nonce);
                    origin.Parameters.AddWithValue(encrypted.Tag);
                    origin.Parameters.AddWithValue(encrypted.KeyVersion);
                    await origin.ExecuteNonQueryAsync(cancellationToken);
                }
                finally { encrypted.Clear(); }
            }

            await ActivateAsync(connection, transaction, catalogIdentity, cancellationToken);
            await using (var run = new NpgsqlCommand("""
                INSERT INTO suite.suite_content_publish_runs(run_id,catalog_identity,inventory_sha256,
                  expected_item_count,published_item_count,ready_item_count,maintenance_item_count,outcome,detail_code)
                VALUES($1,$2,$3,902,902,$4,$5,'PUBLISHED','READY_HASHED_MAINTENANCE_WITHOUT_ORIGIN')
                ON CONFLICT(catalog_identity,inventory_sha256) DO NOTHING
                """, connection, transaction))
            {
                run.Parameters.AddWithValue(CatalogLoader.Hex(RandomNumberGenerator.GetBytes(32)));
                run.Parameters.AddWithValue(catalogIdentity);
                run.Parameters.AddWithValue(catalog.InventorySha256);
                run.Parameters.AddWithValue(plan.ReadyItems.Count);
                run.Parameters.AddWithValue(plan.MaintenanceItems.Count);
                await run.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PublisherFailure) { throw; }
        catch (PostgresException) { throw new PublisherFailure("CATALOG_DATABASE_WRITE_FAILED"); }
        catch (NpgsqlException) { throw new PublisherFailure("DATABASE_UNAVAILABLE"); }
    }

    private static async Task AdvisoryLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended('suite:content-publish',0))",
            connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ActivateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string catalogIdentity,
        CancellationToken cancellationToken)
    {
        await using var activate = new NpgsqlCommand(
            "SELECT suite.publish_suite_content_catalog($1)", connection, transaction);
        activate.Parameters.AddWithValue(catalogIdentity);
        await activate.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ProbeBatch(
        IReadOnlyDictionary<string, OriginMetadata> Probes,
        IReadOnlyList<MaintenanceItem> MaintenanceItems,
        int FatalFailures);
}
