using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed record SuiteAuditItem(string OccurredAt, string EventType, string Outcome, string DetailCode,
    string Actor, string RequestId, string? OtpExpiresAt);
sealed record SuiteAdminStatus(string LicenseId, string ProductId, string Status, string LicenseTerm,
    DateTime? ExpiresAt, string IdentityPolicy, int MaximumActiveDevices, bool ActivationConsumed,
    bool OtpIssued, DateTime? OtpExpiresAt, string DeviceId, string BindingType, string EnrollmentPolicy,
    string Algorithm, string HardwareFingerprint, long ActiveDevices, string? SessionId, string OtpState,
    bool CanIssue, IReadOnlyList<SuiteAuditItem> RecentEvents);
sealed record AuditResult(string Code);
sealed record SuiteOtpResult(string ProductId, string LicenseId, string DeviceId, string Otp,
    DateTime ExpiresAt);

sealed record SuiteContentItem(string ItemId, string DisplayName, string Availability,
    int? ArtifactVersion, string? LastCheckedAt, string LastResultCode, string? JobState,
    string? CandidateId, string? JobUpdatedAt);
sealed record SuiteContentItemPage(IReadOnlyList<SuiteContentItem> Items, string? NextCursor);
sealed record SuiteContentAuditItem(long AuditId, string EventType, string Actor, string? ItemId,
    string? CandidateId, string? JobId, string CorrelationId, string RequestId, string Outcome,
    string DetailCode, string? PreviousCatalogIdentity, string? NewCatalogIdentity, string OccurredAt);
sealed record SuiteContentAuditPage(IReadOnlyList<SuiteContentAuditItem> Events, string? NextCursor);
sealed record SuiteContentJob(string CandidateId, string ItemId, string State, string ChangeIntent,
    string ResultCode, string SubmittedAt, string UpdatedAt, string? VerifiedAt, string? PublishedAt);
sealed record SuiteContentAccepted(string CandidateId, string State);
sealed record SuiteContentCheckAccepted(string State);
sealed record SuiteContentAdminProof(string Actor, string IpDigest, string Claim,
    long? StepUpAt = null, long? VersionStepUpAt = null, string? VersionConfirmation = null);

sealed class SuiteAdminBff : IDisposable
{
    private const int MaximumContentResponseBytes = 512 * 1024;
    private static readonly JsonSerializerOptions StrictContentJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    private static readonly HashSet<string> ContentClaims = new(StringComparer.Ordinal)
    {
        "suite.content.read",
        "suite.content.origin.replace",
        "suite.content.version.publish",
        "suite.content.check"
    };

    private readonly HttpClient client;
    private readonly string token;
    public bool Enabled { get; }

    public SuiteAdminBff()
    {
        var socket = (Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_SOCKET") ?? "").Trim();
        var tokenFile = (Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_TOKEN_FILE") ?? "").Trim();
        Enabled = socket.Length != 0 && tokenFile.Length != 0;
        if (Enabled && (!Path.IsPathFullyQualified(socket) ||
                socket.Any(character => character is '\0' or '\r' or '\n')))
            throw new InvalidOperationException("SUITE_ADMIN_SOCKET_INVALID");
        token = Enabled ? LoadToken(tokenFile) : "";
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectCallback = async (_, ct) =>
            {
                if (!Enabled) throw new HttpRequestException("SUITE_ADMIN_DISABLED");
                var raw = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await raw.ConnectAsync(new UnixDomainSocketEndPoint(socket), ct);
                    return new NetworkStream(raw, true);
                }
                catch { raw.Dispose(); throw; }
            }
        };
        client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public Task<SuiteAdminStatus> StatusAsync(string id, CancellationToken ct) =>
        Send<SuiteAdminStatus>(HttpMethod.Get, "status/" + Uri.EscapeDataString(id), null, ct);
    public Task<SuiteOtpResult> IssueAsync(string id, string device, string actor, string requestId,
        CancellationToken ct) => Send<SuiteOtpResult>(HttpMethod.Post, "issue",
            new { licenseId = id, deviceId = device, ttlSeconds = 900, actor, requestId }, ct);
    public Task<AuditResult> DenyAsync(string id, string device, string actor, string requestId,
        string detail, CancellationToken ct) => Send<AuditResult>(HttpMethod.Post, "deny",
            new { licenseId = id, deviceId = device, actor, requestId, detailCode = detail }, ct);
    public async Task<byte[]> AuditAsync(CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, "audit.csv", null);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public Task<SuiteContentItemPage> ContentItemsAsync(SuiteContentAdminProof proof,
        string? cursor, int limit, string? availability, string? resultCode, string? jobState,
        string? itemPrefix, string? name, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.read", false, false);
        if (limit is < 1 or > 1000 || !ValidCursor(cursor) || !ValidAvailability(availability) ||
            !ValidCode(resultCode, 64) || !ValidCode(jobState, 16) || !ValidItemPrefix(itemPrefix) ||
            name is { Length: > 100 } || name?.Any(char.IsControl) == true)
            throw new HttpRequestException("SUITE_CONTENT_QUERY_INVALID");
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["cursor"] = cursor,
            ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["availability"] = availability,
            ["resultCode"] = resultCode,
            ["jobState"] = jobState,
            ["item"] = itemPrefix,
            ["name"] = name
        }.Where(pair => !string.IsNullOrEmpty(pair.Value)));
        return SendContent<SuiteContentItemPage>(HttpMethod.Get,
            "content/items" + query.ToUriComponent(), null, proof, ct);
    }

    public Task<SuiteContentAuditPage> ContentAuditAsync(SuiteContentAdminProof proof,
        string? cursor, int limit, string? itemId, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.read", false, false);
        if (limit is < 1 or > 100 || !ValidCursor(cursor) ||
            itemId is not null && !IsHex(itemId, 32))
            throw new HttpRequestException("SUITE_CONTENT_QUERY_INVALID");
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["cursor"] = cursor,
            ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["itemId"] = itemId
        }.Where(pair => !string.IsNullOrEmpty(pair.Value)));
        return SendContent<SuiteContentAuditPage>(HttpMethod.Get,
            "content/audit" + query.ToUriComponent(), null, proof, ct);
    }

    public Task<SuiteContentJob> ContentJobAsync(SuiteContentAdminProof proof,
        string candidateId, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.read", false, false);
        if (!IsHex(candidateId, 64)) throw new HttpRequestException("SUITE_CONTENT_ID_INVALID");
        return SendContent<SuiteContentJob>(HttpMethod.Get,
            "content/jobs/" + candidateId, null, proof, ct);
    }

    public Task<SuiteContentAccepted> ReplaceContentOriginAsync(SuiteContentAdminProof proof,
        string itemId, string candidateUrl, string requestId, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.origin.replace", true, false);
        RequireContentMutation(itemId, candidateUrl, requestId);
        return SendContent<SuiteContentAccepted>(HttpMethod.Post,
            "content/items/" + itemId + "/origin-candidates",
            new { candidateUrl, requestId, actor = proof.Actor }, proof, ct);
    }

    public Task<SuiteContentAccepted> PublishContentVersionAsync(SuiteContentAdminProof proof,
        string itemId, string candidateUrl, string requestId, string changeReason,
        int confirmedArtifactVersion, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.version.publish", true, true);
        RequireContentMutation(itemId, candidateUrl, requestId);
        if (changeReason is not ("VENDOR_RELEASE" or "SECURITY_UPDATE" or
                "CONTENT_CORRECTION" or "PLATFORM_UPDATE") || confirmedArtifactVersion < 1 ||
            proof.VersionConfirmation != itemId + ":" + confirmedArtifactVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture))
            throw new HttpRequestException("SUITE_CONTENT_CONFIRMATION_INVALID");
        return SendContent<SuiteContentAccepted>(HttpMethod.Post,
            "content/items/" + itemId + "/versions",
            new
            {
                candidateUrl,
                requestId,
                actor = proof.Actor,
                changeReason,
                confirmedArtifactVersion
            }, proof, ct);
    }

    public Task<SuiteContentCheckAccepted> CheckContentAsync(SuiteContentAdminProof proof,
        string itemId, string requestId, CancellationToken ct)
    {
        RequireContentProof(proof, "suite.content.check", true, false);
        if (!IsHex(itemId, 32) || !ValidRequestId(requestId))
            throw new HttpRequestException("SUITE_CONTENT_REQUEST_INVALID");
        return SendContent<SuiteContentCheckAccepted>(HttpMethod.Post,
            "content/items/" + itemId + "/checks",
            new { requestId, actor = proof.Actor }, proof, ct);
    }

    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = Request(method, path, body);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new HttpRequestException("SUITE_ADMIN_EMPTY");
    }

    private async Task<T> SendContent<T>(HttpMethod method, string path, object? body,
        SuiteContentAdminProof proof, CancellationToken ct)
    {
        byte[] payload = [];
        try
        {
            using var request = Request(method, path, null);
            request.Headers.TryAddWithoutValidation("X-Suite-Admin-Actor", proof.Actor);
            request.Headers.TryAddWithoutValidation("X-Suite-Admin-Claims", proof.Claim);
            request.Headers.TryAddWithoutValidation("X-Suite-Client-Ip-Digest", proof.IpDigest);
            if (body is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Suite-Csrf-Verified", "1");
                request.Headers.TryAddWithoutValidation("X-Suite-Step-Up-At", proof.StepUpAt!
                    .Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (proof.VersionStepUpAt is not null)
                    request.Headers.TryAddWithoutValidation("X-Suite-Version-Step-Up-At",
                        proof.VersionStepUpAt.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (proof.VersionConfirmation is not null)
                    request.Headers.TryAddWithoutValidation("X-Suite-Version-Confirmation",
                        proof.VersionConfirmation);
                payload = JsonSerializer.SerializeToUtf8Bytes(body);
                request.Content = new ByteArrayContent(payload);
                request.Content.Headers.ContentType = new("application/json") { CharSet = "utf-8" };
            }
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("SUITE_CONTENT_REQUEST_FAILED", null,
                    response.StatusCode);
            return await ReadBoundedAsync<T>(response.Content, ct);
        }
        finally
        {
            if (payload.Length != 0) CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static async Task<T> ReadBoundedAsync<T>(HttpContent content, CancellationToken ct)
    {
        if (content.Headers.ContentLength is > MaximumContentResponseBytes)
            throw new HttpRequestException("SUITE_CONTENT_RESPONSE_TOO_LARGE");
        await using var source = await content.ReadAsStreamAsync(ct);
        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer, ct);
                if (read == 0) break;
                if (destination.Length + read > MaximumContentResponseBytes)
                    throw new HttpRequestException("SUITE_CONTENT_RESPONSE_TOO_LARGE");
                destination.Write(buffer, 0, read);
            }
            return JsonSerializer.Deserialize<T>(destination.GetBuffer().AsSpan(0,
                checked((int)destination.Length)), StrictContentJson)
                ?? throw new HttpRequestException("SUITE_CONTENT_RESPONSE_INVALID");
        }
        catch (JsonException)
        {
            throw new HttpRequestException("SUITE_CONTENT_RESPONSE_INVALID");
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private HttpRequestMessage Request(HttpMethod method, string path, object? body)
    {
        if (!Enabled || token.Length == 0) throw new HttpRequestException("SUITE_ADMIN_DISABLED");
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Suite-Admin-Token", token);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8,
                "application/json");
        return request;
    }

    internal static bool IsValidContentProofForTest(SuiteContentAdminProof proof,
        string expectedClaim, bool stepUp, bool versionStepUp)
    {
        try { RequireContentProof(proof, expectedClaim, stepUp, versionStepUp); return true; }
        catch (HttpRequestException) { return false; }
    }

    private static void RequireContentProof(SuiteContentAdminProof proof, string expectedClaim,
        bool stepUp, bool versionStepUp)
    {
        if (!ContentClaims.Contains(proof.Claim) || proof.Claim != expectedClaim ||
            proof.Actor.Length is < 1 or > 64 || proof.Actor.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '@' or '.' or '_' or '-')) ||
            !IsHex(proof.IpDigest, 64) || stepUp && proof.StepUpAt is null ||
            versionStepUp && (proof.VersionStepUpAt is null || proof.VersionConfirmation is null))
            throw new HttpRequestException("SUITE_CONTENT_PROOF_INVALID");
    }

    private static void RequireContentMutation(string itemId, string candidateUrl, string requestId)
    {
        if (!IsHex(itemId, 32) || !ValidRequestId(requestId) || candidateUrl.Length is < 1 or > 4096 ||
            Encoding.UTF8.GetByteCount(candidateUrl) > 4096 ||
            candidateUrl.Any(character => character is '\0' or '\r' or '\n'))
            throw new HttpRequestException("SUITE_CONTENT_REQUEST_INVALID");
    }

    private static bool ValidRequestId(string value) => value.Length is >= 16 and <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    private static bool IsHex(string? value, int length) => value?.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool ValidCursor(string? value) => string.IsNullOrEmpty(value) ||
        value.Length <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');
    private static bool ValidAvailability(string? value) => string.IsNullOrEmpty(value) ||
        value is "ONLINE" or "EM_MANUTENCAO";
    private static bool ValidCode(string? value, int maximum) => string.IsNullOrEmpty(value) ||
        value.Length <= maximum && value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '_');
    private static bool ValidItemPrefix(string? value) => string.IsNullOrEmpty(value) ||
        value.Length <= 32 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string LoadToken(string path)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        var mode = File.GetUnixFileMode(path);
        if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite |
                UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID");
        var text = File.ReadAllText(path).Trim();
        byte[] decoded;
        try { decoded = Convert.FromBase64String(text); }
        catch (FormatException) { throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID"); }
        try
        {
            if (decoded.Length < 32 || Convert.ToBase64String(decoded) != text)
                throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID");
            return text;
        }
        finally { CryptographicOperations.ZeroMemory(decoded); }
    }

    public void Dispose() => client.Dispose();
}

sealed class SuiteIssueGuard
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> last = new();
    public bool Allow(string key)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (true)
        {
            if (!last.TryGetValue(key, out var prior)) return last.TryAdd(key, now);
            if (now - prior < 60) return false;
            if (last.TryUpdate(key, now, prior)) return true;
        }
    }
}

sealed class SuiteContentAdminGuard
{
    private const int MaximumBuckets = 4096;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Bucket> buckets = new();
    private readonly TimeProvider clock;

    public SuiteContentAdminGuard() : this(TimeProvider.System) { }
    internal SuiteContentAdminGuard(TimeProvider clock) => this.clock = clock;

    public bool Allow(string actor, string ipDigest, string itemId, string action,
        int limit, int windowSeconds)
    {
        if (limit < 1 || windowSeconds < 1) return false;
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var window = now / windowSeconds;
        var keyBytes = Encoding.UTF8.GetBytes(actor + "\0" + ipDigest + "\0" + itemId + "\0" + action);
        var key = Convert.ToHexString(SHA256.HashData(keyBytes)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(keyBytes);
        if (buckets.Count >= MaximumBuckets && !buckets.ContainsKey(key)) Sweep(window);
        if (buckets.Count >= MaximumBuckets && !buckets.ContainsKey(key)) return false;
        var bucket = buckets.GetOrAdd(key, _ => new Bucket(window));
        lock (bucket)
        {
            if (bucket.Window != window)
            {
                bucket.Window = window;
                bucket.Count = 0;
            }
            if (bucket.Count >= limit) return false;
            bucket.Count++;
            return true;
        }
    }

    internal int Count => buckets.Count;

    private void Sweep(long currentWindow)
    {
        foreach (var pair in buckets)
            if (pair.Value.Window < currentWindow - 1) buckets.TryRemove(pair.Key, out _);
    }

    private sealed class Bucket(long window)
    {
        public long Window = window;
        public int Count;
    }
}
