using System.Net;
using System.Security.Cryptography;
using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentMonitor;

internal sealed class MonitorCoordinator : IDisposable
{
    private readonly MonitorStore store;
    private readonly MonitorKeyRing candidateKeys;
    private readonly MonitorKeyRing originKeys;
    private readonly OriginPolicy originPolicy;
    private readonly OriginVerifier verifier;
    private readonly Uri gatewayKeyRingReadinessUri;
    private readonly int maximumConcurrency;
    private readonly bool directMode;

    private MonitorCoordinator(MonitorStore store, MonitorKeyRing candidateKeys,
        MonitorKeyRing originKeys, OriginPolicy originPolicy, Uri gatewayKeyRingReadinessUri,
        int maximumConcurrency, bool directMode)
    {
        this.store = store;
        this.candidateKeys = candidateKeys;
        this.originKeys = originKeys;
        this.originPolicy = originPolicy;
        this.gatewayKeyRingReadinessUri = gatewayKeyRingReadinessUri;
        this.maximumConcurrency = maximumConcurrency;
        this.directMode = directMode;
        verifier = new OriginVerifier(originPolicy);
    }

    public static async Task<MonitorCoordinator> CreateAsync(MonitorOptions options,
        CancellationToken ct)
    {
        ProtectedMonitorFile.Require(options.ConnectionFile, 16 * 1024);
        var connection = (await File.ReadAllTextAsync(options.ConnectionFile, ct)).Trim();
        if (connection.Length is < 1 or > 16000 || connection.Any(c => c is '\r' or '\n' or '\0'))
            throw new MonitorFailure("CONNECTION_FILE_INVALID");
        try
        {
            var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
            if (!string.Equals(parsed.Username, "turborama-suite-content-monitor",
                    StringComparison.Ordinal))
                throw new MonitorFailure("CONNECTION_ROLE_INVALID");
        }
        catch (ArgumentException) { throw new MonitorFailure("CONNECTION_FILE_INVALID"); }
        var candidates = await MonitorKeyRing.LoadAsync(options.CandidateKeyRingFile, ct);
        try
        {
            var origins = await MonitorKeyRing.LoadAsync(options.OriginKeyRingFile, ct);
            try
            {
                if (candidates.SharesAnyKeyMaterialWith(origins))
                    throw new MonitorFailure("KEY_RING_DOMAIN_SEPARATION_INVALID");
                var policy = await OriginPolicy.LoadAsync(options.AllowedHostsFile, ct);
                return new MonitorCoordinator(new MonitorStore(connection), candidates,
                    origins, policy, options.GatewayKeyRingReadinessUri,
                    options.MaximumConcurrency, options.DirectMode);
            }
            catch { origins.Dispose(); throw; }
        }
        catch { candidates.Dispose(); throw; }
        finally { connection = string.Empty; }
    }

    public async Task<MonitorRunResult> RunOnceAsync(CancellationToken ct)
    {
        await using var cycleLock = await store.TryAcquireCycleLockAsync(ct);
        if (cycleLock is null)
            return new MonitorRunResult(MonitorRunOutcome.AlreadyRunning,
                "CYCLE_ALREADY_RUNNING");
        var cycleId = RandomId();
        var databaseStage = "START_CYCLE";
        try
        {
            await store.StartCycleAsync(cycleId, ct);
            databaseStage = "SYNCHRONIZE_HEALTH";
            await store.SynchronizeHealthAsync(ct);
            databaseStage = "LOAD_TARGETS";
            var targets = await store.GetDueHealthTargetsAsync(850, ct);
            databaseStage = "PROBE_HEALTH";
            var results = await ProbeHealthAsync(targets, ct);
            await store.WriteLinkAlertReportAsync(results, ct);
            if (results.Any(result => result.SecurityFailure))
            {
                await store.RecordSecurityAbortAsync(cycleId, ct);
                await store.CompleteCycleAsync(cycleId, "BLOCKED", "SECURITY_CYCLE_ABORTED", ct);
                await store.DispatchPendingAlertsAsync(100, ct);
                return new MonitorRunResult(MonitorRunOutcome.Blocked,
                    "SECURITY_CYCLE_ABORTED");
            }
            databaseStage = "APPLY_RESULTS";
            var promotions = await store.ApplyHealthResultsAsync(results, ct);
            if (promotions.Count > 0)
            {
                var header = await store.LoadActiveHeaderAsync(ct);
                var items = await store.LoadSnapshotItemsAsync(header.CatalogIdentity, ct);
                try
                {
                    var resultingMaintenance = MonitorStore.ResultingMaintenanceCount(items,
                        header.CatalogIdentity, promotions);
                    if (HealthPolicy.OpensMassFailureGuard(resultingMaintenance))
                    {
                        await store.RecordMassFailureGuardAsync(cycleId,
                            resultingMaintenance, ct);
                        await store.CompleteCycleAsync(cycleId, "BLOCKED",
                            "MASS_FAILURE_GUARD", ct);
                        await store.DispatchPendingAlertsAsync(100, ct);
                        return new MonitorRunResult(MonitorRunOutcome.Blocked,
                            "MASS_FAILURE_GUARD");
                    }
                    var provenance = await GatewayDeploymentPreflight.RequireAsync(
                        gatewayKeyRingReadinessUri,
                        originKeys, originPolicy, ct);
                    await store.PublishMutationAsync(header, items,
                        new SnapshotMutation(cycleId, promotions, null), originKeys,
                        provenance, ct);
                }
                finally { ClearOrigins(items); }
            }
            databaseStage = "COMPLETE_CYCLE";
            await store.CompleteCycleAsync(cycleId, "SUCCESS", "CYCLE_COMPLETED", ct);
            databaseStage = "DISPATCH_ALERTS";
            await store.DispatchPendingAlertsAsync(100, ct);
            return new MonitorRunResult(MonitorRunOutcome.Success, "CYCLE_COMPLETED");
        }
        catch (Npgsql.PostgresException)
        {
            throw new MonitorFailure("DATABASE_" + databaseStage);
        }
        catch
        {
            using var auditTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await store.CompleteCycleAsync(cycleId, "FAILED", "CYCLE_FAILED",
                    auditTimeout.Token);
                await store.RecordCycleFailureAsync(cycleId, auditTimeout.Token);
                await store.DispatchPendingAlertsAsync(100, auditTimeout.Token);
            }
            catch { }
            throw;
        }
    }

    public async Task<MonitorRunResult> RunCandidateOnceAsync(CancellationToken ct)
    {
        await using var cycleLock = await store.TryAcquireCandidateCycleLockAsync(ct);
        if (cycleLock is null)
            return new MonitorRunResult(MonitorRunOutcome.AlreadyRunning,
                "CANDIDATE_CYCLE_ALREADY_RUNNING");
        var workerId = RandomId();
        try
        {
            var processed = await ProcessOneCandidateAsync(workerId, ct);
            await store.DispatchPendingAlertsAsync(100, ct);
            return new MonitorRunResult(MonitorRunOutcome.Success,
                processed ? "CANDIDATE_CYCLE_COMPLETED" : "CANDIDATE_QUEUE_EMPTY");
        }
        catch
        {
            using var alertTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await store.RecordCycleFailureAsync(workerId, alertTimeout.Token);
                await store.DispatchPendingAlertsAsync(100, alertTimeout.Token);
            }
            catch { }
            throw;
        }
    }

    private async Task<bool> ProcessOneCandidateAsync(string workerId, CancellationToken ct)
    {
        var leased = await store.LeaseCandidatesAsync(workerId, 1, ct);
        if (leased.Count == 0) return false;
        var candidate = leased[0];
        ValidatedCandidate? validated = null;
        using var leaseLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Exception? leaseFailure = null;
        var leaseHeartbeat = MaintainCandidateLeaseAsync(candidate, leaseLifetime, failure =>
            leaseFailure = failure);
        try
        {
            await store.MarkCandidateValidatingAuditAsync(candidate, ct);
            validated = await ValidateCandidateAsync(candidate, leaseLifetime.Token);
            await store.MarkCandidateVerifiedAsync(validated, ct);
            var header = await store.LoadActiveHeaderAsync(ct);
            if (!string.Equals(header.CatalogIdentity, candidate.BaseCatalogIdentity,
                    StringComparison.Ordinal))
            {
                await store.MarkCandidateSupersededAsync(candidate, ct);
                return true;
            }
            var items = await store.LoadSnapshotItemsAsync(header.CatalogIdentity, ct);
            try
            {
                var provenance = await GatewayDeploymentPreflight.RequireAsync(
                    gatewayKeyRingReadinessUri,
                    originKeys, originPolicy, leaseLifetime.Token);
                await store.PublishMutationAsync(header, items,
                    new SnapshotMutation(candidate.CandidateId,
                        new Dictionary<string, MaintenancePromotion>(StringComparer.Ordinal),
                        validated), originKeys,
                    provenance, leaseLifetime.Token);
            }
            finally { ClearOrigins(items); }
        }
        catch (PublisherFailure ex)
        {
            await store.MarkCandidateRejectedAsync(candidate, ex.Code, ct);
        }
        catch (MonitorFailure ex) when (ex.Code == "CATALOG_CHANGED")
        {
            await store.MarkCandidateSupersededAsync(candidate, ct);
        }
        catch (MonitorFailure ex) when (ex.Code is "URL_DECRYPTION_INVALID" or
            "URL_DECRYPTION_FAILED" or "KEY_VERSION_UNKNOWN")
        {
            await store.MarkCandidateRejectedAsync(candidate, "UPSTREAM_URL_POLICY_DENIED", ct);
        }
        finally
        {
            leaseLifetime.Cancel();
            await leaseHeartbeat;
            validated?.Url.Dispose();
            ClearCandidate(candidate);
            if (leaseFailure is not null)
                throw new MonitorFailure("CANDIDATE_LEASE_LOST");
        }
        return true;
    }

    private async Task MaintainCandidateLeaseAsync(CandidateLease candidate,
        CancellationTokenSource lifetime, Action<Exception> recordFailure)
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                if (!await store.RenewCandidateLeaseAsync(candidate, lifetime.Token)) return;
                await Task.Delay(TimeSpan.FromMinutes(5), lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            recordFailure(exception);
            lifetime.Cancel();
        }
    }

    private async Task<ValidatedCandidate> ValidateCandidateAsync(CandidateLease candidate,
        CancellationToken ct)
    {
        var decrypted = candidateKeys.DecryptCandidate(candidate);
        try
        {
            originPolicy.ValidateUri(decrypted.Uri);
            var (safeFileName, extension, extractPolicy) = ArtifactName(decrypted.Uri,
                candidate.ExpectedExtractPolicy ?? throw new PublisherFailure("ORIGIN_CONTENT_INVALID"));
            if (candidate.ChangeIntent == "MIRROR_REPLACEMENT" &&
                !string.Equals(extension, candidate.ExpectedFileExtension, StringComparison.Ordinal))
                throw new PublisherFailure("MIRROR_EXTENSION_MISMATCH");
            var probe = await verifier.ProbeOnlyAsync(decrypted.Uri, ct);
            if (directMode)
            {
                if (candidate.ChangeIntent == "MIRROR_REPLACEMENT" &&
                    probe.ContentLength != candidate.ExpectedContentLength)
                    throw new PublisherFailure("MIRROR_LENGTH_MISMATCH");
                return new ValidatedCandidate(candidate, decrypted, probe.ContentLength,
                    new string('0', 64), safeFileName, extension, extractPolicy,
                    probe.ContentType, probe.Etag, probe.LastModified);
            }
            var full = await verifier.HashOnlyAsync(decrypted.Uri, probe, extension, ct);
            if (probe.Etag is not null && full.Etag is not null && probe.Etag != full.Etag ||
                probe.LastModified is not null && full.LastModified is not null &&
                probe.LastModified != full.LastModified)
                throw new PublisherFailure("SOURCE_CHANGED");
            if (candidate.ChangeIntent == "MIRROR_REPLACEMENT")
            {
                if (full.ContentLength != candidate.ExpectedContentLength)
                    throw new PublisherFailure("MIRROR_LENGTH_MISMATCH");
                if (!FixedAscii(full.Sha256, candidate.ExpectedSha256))
                    throw new PublisherFailure("MIRROR_SHA256_MISMATCH");
            }
            return new ValidatedCandidate(candidate, decrypted, full.ContentLength, full.Sha256,
                safeFileName, extension, extractPolicy, full.ContentType, full.Etag, full.LastModified);
        }
        catch
        {
            decrypted.Dispose();
            throw;
        }
    }

    private async Task<IReadOnlyList<HealthProbeResult>> ProbeHealthAsync(
        IReadOnlyList<HealthTarget> targets, CancellationToken ct)
    {
        var results = new HealthProbeResult[targets.Count];
        var fullValidationTargets = directMode
            ? new HashSet<string>(StringComparer.Ordinal)
            : targets.Where(target =>
                HealthPolicy.RequiresFullValidation(target, DateTime.UtcNow))
            .Take(HealthPolicy.MaximumFullValidationsPerCycle)
            .Select(target => target.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        await Parallel.ForEachAsync(Enumerable.Range(0, targets.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maximumConcurrency,
                CancellationToken = ct
            }, async (index, token) =>
            {
                var target = targets[index];
                DecryptedUrl? decrypted = null;
                try
                {
                    decrypted = originKeys.DecryptOrigin(target);
                    originPolicy.ValidateUri(decrypted.Uri);
                    var probe = await verifier.ProbeOnlyAsync(decrypted.Uri, token);
                    if ((!directMode && probe.ContentLength != target.ExpectedContentLength) ||
                        !HealthPolicy.ValidatorsMatch(target, probe.Etag,
                            probe.LastModified))
                        throw new PublisherFailure("ORIGIN_LENGTH_CHANGED");
                    var fullValidation = fullValidationTargets.Contains(target.ItemId);
                    if (fullValidation)
                    {
                        var full = await verifier.HashOnlyAsync(decrypted.Uri, probe,
                            target.FileExtension, token);
                        if (!HealthPolicy.FullArtifactMatches(target, full))
                            throw new PublisherFailure("ORIGIN_LENGTH_CHANGED");
                    }
                    results[index] = new HealthProbeResult(target, true, false,
                        fullValidation, "CHECK_OK");
                }
                catch (PublisherFailure ex)
                {
                    results[index] = new HealthProbeResult(target, false,
                        ResultCodes.IsSecurity(ex.Code), false, ResultCodes.Health(ex.Code));
                }
                catch (MonitorFailure)
                {
                    results[index] = new HealthProbeResult(target, false, true,
                        false, "SECURITY_CYCLE_ABORTED");
                }
                finally
                {
                    decrypted?.Dispose();
                    ClearHealth(target);
                }
            });
        return results;
    }

    internal static (string Name, string Extension, string ExtractPolicy) ArtifactName(
        Uri uri, string expectedExtractPolicy)
    {
        string name;
        try { name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)); }
        catch (UriFormatException) { throw new PublisherFailure("ORIGIN_CONTENT_INVALID"); }
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 ||
            System.Text.Encoding.UTF8.GetByteCount(name) > 180 || name is "." or ".." ||
            name[^1] is ' ' or '.' || name.Any(c => char.IsControl(c) || char.IsSurrogate(c) ||
                c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            Reserved(stem, "COM") || Reserved(stem, "LPT"))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        var sourceExtension = Path.GetExtension(name);
        var extension = sourceExtension.ToLowerInvariant();
        if (!ContentSignatureValidator.IsSupportedExtension(extension))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        if (expectedExtractPolicy is not ("NONE" or "EXTRACT_ARCHIVE") ||
            expectedExtractPolicy == "EXTRACT_ARCHIVE" &&
            extension is not (".zip" or ".rar" or ".7z"))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        var normalizedName = name[..^sourceExtension.Length] + extension;
        return (normalizedName, extension, expectedExtractPolicy);
    }

    private static bool Reserved(string value, string prefix) => value.Length == 4 &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value[3] is >= '1' and <= '9';
    private static bool FixedAscii(string left, string? right)
    {
        if (right is null || left.Length != right.Length) return false;
        var a = System.Text.Encoding.ASCII.GetBytes(left);
        var b = System.Text.Encoding.ASCII.GetBytes(right);
        try { return CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }
    private static string RandomId() => Convert.ToHexString(
        RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static void ClearCandidate(CandidateLease value)
    {
        CryptographicOperations.ZeroMemory(value.Ciphertext);
        CryptographicOperations.ZeroMemory(value.Nonce);
        CryptographicOperations.ZeroMemory(value.Tag);
    }
    private static void ClearHealth(HealthTarget value)
    {
        CryptographicOperations.ZeroMemory(value.Ciphertext);
        CryptographicOperations.ZeroMemory(value.Nonce);
        CryptographicOperations.ZeroMemory(value.Tag);
    }
    private static void ClearOrigins(IReadOnlyList<SnapshotItem> items)
    {
        foreach (var item in items)
        {
            if (item.OriginCiphertext is not null) CryptographicOperations.ZeroMemory(item.OriginCiphertext);
            if (item.OriginNonce is not null) CryptographicOperations.ZeroMemory(item.OriginNonce);
            if (item.OriginTag is not null) CryptographicOperations.ZeroMemory(item.OriginTag);
        }
    }

    public void Dispose()
    {
        verifier.Dispose();
        candidateKeys.Dispose();
        originKeys.Dispose();
        store.Dispose();
    }
}
