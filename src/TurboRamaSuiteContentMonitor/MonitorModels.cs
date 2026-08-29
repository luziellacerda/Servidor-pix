namespace TurboRamaSuiteContentMonitor;

internal sealed record MonitorOptions(
    string ConnectionFile,
    string CandidateKeyRingFile,
    string OriginKeyRingFile,
    string AllowedHostsFile,
    Uri GatewayKeyRingReadinessUri,
    int MaximumConcurrency,
    bool DirectMode)
{
    public static MonitorOptions FromEnvironment()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key)?.Trim()
            is { Length: > 0 } value ? value : throw new MonitorFailure("CONFIGURATION_MISSING");
        static string ProtectedPath(string fileKey, string credentialKey)
        {
            var file = Environment.GetEnvironmentVariable(fileKey)?.Trim();
            var credential = Environment.GetEnvironmentVariable(credentialKey)?.Trim();
            if (!string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(credential))
                throw new MonitorFailure("CONFIGURATION_INVALID");
            if (string.IsNullOrEmpty(credential))
                return !string.IsNullOrEmpty(file) ? file : throw new MonitorFailure("CONFIGURATION_MISSING");
            if (credential.Length > 128 || credential.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
                throw new MonitorFailure("CONFIGURATION_INVALID");
            var directory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY")?.Trim();
            if (string.IsNullOrEmpty(directory) || !Path.IsPathFullyQualified(directory))
                throw new MonitorFailure("CONFIGURATION_INVALID");
            return Path.Combine(directory, credential);
        }
        var concurrency = 2;
        var text = Environment.GetEnvironmentVariable("SUITE_CONTENT_MONITOR_MAX_CONCURRENCY")?.Trim();
        if (text is not null && (!int.TryParse(text, out concurrency) || concurrency is < 1 or > 4))
            throw new MonitorFailure("CONFIGURATION_INVALID");
        var readinessText = Required("SUITE_CONTENT_MONITOR_GATEWAY_KEYRING_READINESS_URL");
        if (!Uri.TryCreate(readinessText, UriKind.Absolute, out var readinessUri) ||
            readinessUri.Scheme != Uri.UriSchemeHttp || readinessUri.Host != "127.0.0.1" ||
            readinessUri.Port != 5191 || readinessUri.AbsolutePath != "/ready/keyring/prove" ||
            readinessUri.Query.Length != 0 || readinessUri.Fragment.Length != 0 ||
            readinessUri.UserInfo.Length != 0)
            throw new MonitorFailure("CONFIGURATION_INVALID");
        var directModeText = Environment.GetEnvironmentVariable(
            "SUITE_CONTENT_MONITOR_DIRECT_MODE")?.Trim();
        if (directModeText is not ("0" or "1"))
            throw new MonitorFailure("CONFIGURATION_INVALID");
        return new MonitorOptions(
            Required("SUITE_CONTENT_MONITOR_CONNECTION_FILE"),
            ProtectedPath("SUITE_CONTENT_MONITOR_CANDIDATE_KEYRING_FILE",
                "SUITE_CONTENT_MONITOR_CANDIDATE_KEYRING_CREDENTIAL"),
            ProtectedPath("SUITE_CONTENT_MONITOR_ORIGIN_KEYRING_FILE",
                "SUITE_CONTENT_MONITOR_ORIGIN_KEYRING_CREDENTIAL"),
            ProtectedPath("SUITE_CONTENT_MONITOR_ALLOWED_HOSTS_FILE",
                "SUITE_CONTENT_MONITOR_ALLOWED_HOSTS_CREDENTIAL"), readinessUri, concurrency,
            directModeText == "1");
    }
}

internal sealed record CandidateLease(
    string CandidateId,
    string ItemId,
    string BaseCatalogIdentity,
    string RequestId,
    string State,
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    int KeyVersion,
    string ChangeIntent,
    string? ChangeReason,
    long? ExpectedContentLength,
    string? ExpectedSha256,
    int? ExpectedArtifactVersion,
    string? ExpectedFileExtension,
    string? ExpectedExtractPolicy,
    string SubmittedBy,
    string LeaseOwner);

internal sealed record ValidatedCandidate(
    CandidateLease Candidate,
    DecryptedUrl Url,
    long ContentLength,
    string Sha256,
    string SafeFileName,
    string FileExtension,
    string ExtractPolicy,
    string ContentType,
    string? Etag,
    string? LastModified);

internal sealed record HealthTarget(
    string ItemId,
    string CatalogIdentity,
    int ArtifactVersion,
    long ExpectedContentLength,
    string ExpectedSha256,
    string FileExtension,
    string? ExpectedEtag,
    string? ExpectedLastModified,
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    int KeyVersion,
    int ConsecutiveFailures,
    DateTime? LastCheckedAt,
    DateTime? LastFullValidationAt,
    long RowVersion);

internal sealed record HealthProbeResult(
    HealthTarget Target,
    bool Success,
    bool SecurityFailure,
    bool FullValidation,
    string ResultCode);

internal sealed record MaintenancePromotion(
    string ItemId,
    string CatalogIdentity,
    int ArtifactVersion,
    long HealthRowVersion);

internal sealed record SnapshotHeader(
    string CatalogIdentity,
    long CatalogSequence,
    string InventorySha256,
    string VisualCatalogSha256);

internal sealed record SnapshotItem(
    string ItemId,
    int DisplayOrder,
    string DisplayName,
    string VisualExtractPolicy,
    string Status,
    int? ArtifactVersion,
    long? ContentLength,
    string? Sha256,
    string? SafeFileName,
    string? FileExtension,
    string? ExtractPolicy,
    string? ContentType,
    string? Etag,
    string? LastModified,
    string? MaintenanceReason,
    byte[]? OriginCiphertext,
    byte[]? OriginNonce,
    byte[]? OriginTag,
    int? OriginKeyVersion);

internal sealed record SnapshotMutation(
    string OperationId,
    IReadOnlyDictionary<string, MaintenancePromotion> MaintenanceItems,
    ValidatedCandidate? Candidate);

internal sealed record MonitorDeploymentProvenance(
    int ActiveKeyVersion,
    string KeySetFingerprint,
    string AllowlistFingerprint);

internal enum MonitorRunOutcome { Success, AlreadyRunning, Blocked }

internal sealed record MonitorRunResult(MonitorRunOutcome Outcome, string Code);

internal sealed class MonitorFailure : Exception
{
    public MonitorFailure(string code) : base(code) => Code = code;
    public string Code { get; }
}
