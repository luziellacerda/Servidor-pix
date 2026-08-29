using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TurboRamaSuiteContentPublisher;

internal sealed class OriginPolicy
{
    internal const int MaximumUrlUtf8Bytes = 4096;
    private static readonly byte[] AllowlistDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentOriginAllowlist/v1\0");
    private readonly HashSet<string> allowedHosts;
    private readonly byte[] deploymentFingerprint;

    private OriginPolicy(HashSet<string> hosts)
    {
        allowedHosts = hosts;
        deploymentFingerprint = ComputeDeploymentFingerprint(hosts);
    }

    internal byte[] CopyDeploymentFingerprint() => deploymentFingerprint.ToArray();

    internal static OriginPolicy ForSelfTest(params string[] canonicalHosts) =>
        new(new HashSet<string>(canonicalHosts, StringComparer.OrdinalIgnoreCase));

    internal bool HasSameDeploymentFingerprint(OriginPolicy other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CryptographicOperations.FixedTimeEquals(
            deploymentFingerprint, other.deploymentFingerprint);
    }

    public static async Task<OriginPolicy> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ProtectedFile.Require(path, "ALLOWED_HOSTS_FILE_INVALID", requirePrivateMode: false);
        string[] lines;
        try { lines = await File.ReadAllLinesAsync(path, cancellationToken); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new PublisherFailure("ALLOWED_HOSTS_FILE_INVALID"); }
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.Contains('/') || line.Contains(':') || line.Contains('@') || line.Contains('*') ||
                !Uri.CheckHostName(line).Equals(UriHostNameType.Dns))
                throw new PublisherFailure("ALLOWED_HOST_INVALID");
            hosts.Add(new IdnMapping().GetAscii(line).ToLowerInvariant());
        }
        if (hosts.Count == 0) throw new PublisherFailure("ALLOWED_HOSTS_EMPTY");
        return new OriginPolicy(hosts);
    }

    private static byte[] ComputeDeploymentFingerprint(IEnumerable<string> canonicalHosts)
    {
        var ordered = canonicalHosts.OrderBy(host => host, StringComparer.Ordinal).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(AllowlistDomain);
        Span<byte> number = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(number, ordered.Length);
        hash.AppendData(number);
        foreach (var host in ordered)
        {
            var bytes = Encoding.UTF8.GetBytes(host);
            try
            {
                BinaryPrimitives.WriteInt32BigEndian(number, bytes.Length);
                hash.AppendData(number);
                hash.AppendData(bytes);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return hash.GetHashAndReset();
    }

    public void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.HostNameType != UriHostNameType.Dns ||
            Encoding.UTF8.GetByteCount(uri.AbsoluteUri) > MaximumUrlUtf8Bytes)
            throw new PublisherFailure("UPSTREAM_URL_POLICY_DENIED");
        var host = new IdnMapping().GetAscii(uri.DnsSafeHost).ToLowerInvariant();
        if (!allowedHosts.Contains(host)) throw new PublisherFailure("UPSTREAM_HOST_DENIED");
    }

    public SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 4,
            MaxResponseDrainSize = 0,
            UseProxy = false,
            UseCookies = false,
            ConnectCallback = ConnectPublicAsync,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online
            }
        };
    }

    private async ValueTask<Stream> ConnectPublicAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = new IdnMapping().GetAscii(context.DnsEndPoint.Host).ToLowerInvariant();
        if (!allowedHosts.Contains(host) || context.DnsEndPoint.Port != 443)
            throw new OriginPolicyViolationException("UPSTREAM_HOST_DENIED");
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(host, cancellationToken); }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        { throw new HttpRequestException("DNS_FAILED"); }
        if (addresses.Length == 0 || addresses.Any(address => !IsPublic(address)))
            throw new OriginPolicyViolationException("UPSTREAM_DNS_ADDRESS_DENIED");

        Exception? failure = null;
        foreach (var address in addresses.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(address, 443, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                failure = ex;
                socket.Dispose();
                if (ex is OperationCanceledException) throw;
            }
        }
        throw new HttpRequestException("CONNECT_FAILED", failure);
    }

    internal static bool IsPublic(IPAddress address) =>
        PublicNetworkPolicy.IsGloballyRoutable(address);
}

internal sealed class OriginPolicyViolationException(string code) : IOException
{
    public string Code { get; } = code;
}

internal sealed record OriginMetadata(
    long ContentLength,
    string Sha256,
    string? Etag,
    string? LastModified,
    string ContentType);

internal static class ContentArtifactLimits
{
    public const long MaximumContentLength = 512L * 1024 * 1024 * 1024;
    public static bool IsSupportedLength(long value) => value is >= 1 and <= MaximumContentLength;
}

internal sealed class OriginVerifier : IDisposable
{
    private static readonly ProductInfoHeaderValue UserAgent = new("TurboRama-Suite-Content-Publisher", "1.0");
    private readonly OriginPolicy policy;
    private readonly HttpClient client;

    public OriginVerifier(OriginPolicy policy)
    {
        this.policy = policy;
        client = new HttpClient(policy.CreateHandler(), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.Add(UserAgent);
    }

    public Task<OriginMetadata> ProbeOnlyAsync(Uri uri, CancellationToken cancellationToken)
        => RetryAsync(ct => ProbeAsync(uri, ct), cancellationToken);

    public async Task<OriginMetadata> HashOnlyAsync(
        Uri uri,
        OriginMetadata probe,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        var full = await RetryAsync(ct => HashAsync(uri, fileExtension, probe.ContentLength, ct), cancellationToken);
        if (probe.ContentLength is > 0 && probe.ContentLength != full.ContentLength)
            throw new PublisherFailure("ORIGIN_LENGTH_CHANGED");
        if (!ContentArtifactLimits.IsSupportedLength(full.ContentLength))
        {
            if (full.ContentLength > ContentArtifactLimits.MaximumContentLength)
                throw new PublisherFailure("ORIGIN_CONTENT_TOO_LARGE");
            throw new PublisherFailure("ORIGIN_TERMINAL_EMPTY");
        }
        if (full.Sha256.Length != 64)
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        return BindFullResponseIdentity(probe, full);
    }

    internal static OriginMetadata BindFullResponseIdentity(
        OriginMetadata probe,
        OriginMetadata full)
    {
        var probeStrong = IsStrongEtag(probe.Etag);
        var fullStrong = IsStrongEtag(full.Etag);
        if (probeStrong && fullStrong &&
            !string.Equals(probe.Etag, full.Etag, StringComparison.Ordinal))
            throw new PublisherFailure("SOURCE_CHANGED");

        // A checkpoint validator is body-bound only when the same strong ETag
        // appeared on both the preliminary response and the full GET. Never
        // borrow ETag/Last-Modified metadata from a response whose body was not
        // hashed.
        return full with
        {
            Etag = probeStrong && fullStrong ? full.Etag : null
        };
    }

    private static bool IsStrongEtag(string? value) =>
        value is { Length: >= 2 } && value[0] == '"' && value[^1] == '"' &&
        !value.StartsWith("W/", StringComparison.OrdinalIgnoreCase);

    private async Task<OriginMetadata> ProbeAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        HttpResponseMessage rangeResponse;
        try { rangeResponse = await SendAsync(HttpMethod.Get, uri, new RangeHeaderValue(0, 0), timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new PublisherFailure("ORIGIN_HEADER_TIMEOUT"); }
        using var response = rangeResponse;
        EnsureIdentityEncoding(response);
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var range = response.Content.Headers.ContentRange;
            if (range?.From != 0 || range.To != 0 || range.Length is null or <= 0)
                throw new PublisherFailure("ORIGIN_RANGE_INVALID");
            if (!ContentArtifactLimits.IsSupportedLength(range.Length.Value))
                throw new PublisherFailure("ORIGIN_CONTENT_TOO_LARGE");
            await ReadSingleProbeByteAsync(response.Content, timeout.Token);
            return Metadata(range.Length.Value, "", response);
        }
        if (response.StatusCode != HttpStatusCode.OK)
            throw StatusFailure(response.StatusCode);
        var rangeIgnoredLength = response.Content.Headers.ContentLength;
        await ReadSingleProbeByteAsync(response.Content, timeout.Token);
        response.Dispose(); // Close immediately after the single-byte sample.
        HttpResponseMessage headResponse;
        try { headResponse = await SendAsync(HttpMethod.Head, uri, null, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new PublisherFailure("ORIGIN_HEADER_TIMEOUT"); }
        using var head = headResponse;
        EnsureIdentityEncoding(head);
        if (head.StatusCode != HttpStatusCode.OK)
            throw StatusFailure(head.StatusCode);
        var length = head.Content.Headers.ContentLength ?? rangeIgnoredLength;
        if (length is null or <= 0) throw new PublisherFailure("ORIGIN_LENGTH_MISSING");
        if (!ContentArtifactLimits.IsSupportedLength(length.Value))
            throw new PublisherFailure("ORIGIN_CONTENT_TOO_LARGE");
        return Metadata(length.Value, "", head);
    }

    private static async Task ReadSingleProbeByteAsync(HttpContent content,
        CancellationToken cancellationToken)
    {
        var sample = new byte[1];
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            if (await stream.ReadAsync(sample.AsMemory(0, 1), cancellationToken) != 1)
                throw new PublisherFailure("ORIGIN_BODY_EMPTY");
        }
        finally { CryptographicOperations.ZeroMemory(sample); }
    }

    private async Task<OriginMetadata> HashAsync(
        Uri uri,
        string fileExtension,
        long probedContentLength,
        CancellationToken cancellationToken)
    {
        using var totalTimeout = new CancellationTokenSource(TimeSpan.FromHours(96));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, totalTimeout.Token);
        HttpResponseMessage fullResponse;
        try { fullResponse = await SendAsync(HttpMethod.Get, uri, null, timeout.Token); }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && totalTimeout.IsCancellationRequested)
        { throw new PublisherFailure("ORIGIN_TOTAL_TIMEOUT"); }
        using var response = fullResponse;
        EnsureIdentityEncoding(response);
        if (response.StatusCode != HttpStatusCode.OK)
            throw StatusFailure(response.StatusCode);
        var contentType = CleanContentType(response.Content.Headers.ContentType?.MediaType);
        ContentSignatureValidator.ValidateContentType(fileExtension, contentType);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var capture = new byte[ContentSignatureValidator.CaptureLimit];
        var buffer = fileExtension == ".xml" ? [] : new byte[1024 * 1024];
        try
        {
            using var hashingStream = new HashingCaptureStream(stream, hash, capture, timeout.Token,
                cancellationToken, totalTimeout.Token);
            if (fileExtension == ".xml")
                await ContentSignatureValidator.ValidateXmlAsync(
                    hashingStream,
                    response.Content.Headers.ContentLength ?? probedContentLength,
                    timeout.Token);
            else
            {
                var signatureValidated = false;
                while (await hashingStream.ReadAsync(buffer, timeout.Token) != 0)
                {
                    if (signatureValidated || hashingStream.CapturedBytes != capture.Length) continue;
                    ContentSignatureValidator.ValidateBinary(fileExtension, contentType,
                        response.Content.Headers.ContentLength ?? probedContentLength, capture);
                    signatureValidated = true;
                }
                if (!signatureValidated)
                    ContentSignatureValidator.ValidateBinary(fileExtension, contentType,
                        hashingStream.TotalBytes, capture.AsSpan(0, hashingStream.CapturedBytes));
            }
            var total = hashingStream.TotalBytes;
            if (response.Content.Headers.ContentLength is long expected && expected != total)
                throw new PublisherFailure("ORIGIN_LENGTH_MISMATCH");
            return Metadata(total, CatalogLoader.Hex(hash.GetHashAndReset()), response);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(capture);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri original,
        RangeHeaderValue? range,
        CancellationToken cancellationToken)
    {
        policy.ValidateUri(original);
        using var request = new HttpRequestMessage(method, original);
        request.Headers.AcceptEncoding.ParseAdd("identity");
        request.Headers.Range = range;
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OriginPolicyViolationException ex)
        { throw new PublisherFailure(ex.Code); }
        catch (AuthenticationException)
        { throw new PublisherFailure("ORIGIN_TLS_SECURITY_FAILURE"); }
        catch (HttpRequestException ex)
        {
            if (FindCause<OriginPolicyViolationException>(ex) is { } violation)
                throw new PublisherFailure(violation.Code);
            if (ex.HttpRequestError == HttpRequestError.SecureConnectionError ||
                FindCause<AuthenticationException>(ex) is not null)
                throw new PublisherFailure("ORIGIN_TLS_SECURITY_FAILURE");
            throw new PublisherFailure("ORIGIN_REQUEST_FAILED");
        }
        catch (IOException)
        { throw new PublisherFailure("ORIGIN_REQUEST_FAILED"); }
        if (IsRedirect(response.StatusCode) || response.Headers.Location is not null)
        {
            response.Dispose();
            throw new PublisherFailure("ORIGIN_REDIRECT_DENIED");
        }
        return response;
    }

    private static OriginMetadata Metadata(long length, string sha, HttpResponseMessage response)
        => new(
            length,
            sha,
            CleanHeader(response.Headers.ETag?.ToString(), 512),
            CleanHeader(response.Content.Headers.LastModified?.ToString("R"), 128),
            CleanContentType(response.Content.Headers.ContentType?.MediaType));

    private static string CleanContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl))
            return "application/octet-stream";
        return value;
    }

    private static string? CleanHeader(string? value, int maximum)
        => string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)
            ? null
            : value;

    private static void EnsureIdentityEncoding(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentEncoding.Count != 0)
            throw new PublisherFailure("ORIGIN_CONTENT_ENCODING_DENIED");
    }

    private static PublisherFailure StatusFailure(HttpStatusCode status)
        => new(ClassifyStatus((int)status));

    internal static string ClassifyStatus(int status)
        => status switch
        {
            416 => "ORIGIN_TERMINAL_EMPTY",
            408 or 425 or 429 or >= 500 => "ORIGIN_RETRYABLE_STATUS",
            >= 400 and < 500 => "ORIGIN_TERMINAL_UNAVAILABLE",
            _ => "ORIGIN_STATUS_DENIED"
        };

    private static T? FindCause<T>(Exception exception) where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is T match) return match;
        return null;
    }

    private static bool IsRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or
            HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static async Task<T> RetryAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var delays = new[] { 2, 5, 15, 30 };
        for (var attempt = 0; ; attempt++)
        {
            try { return await action(cancellationToken); }
            catch (PublisherFailure ex) when (
                ex.Code is "ORIGIN_REQUEST_FAILED" or "ORIGIN_RETRYABLE_STATUS" or "ORIGIN_LENGTH_CHANGED" or
                    "ORIGIN_READ_IDLE_TIMEOUT" or "ORIGIN_HEADER_TIMEOUT" &&
                attempt < delays.Length)
            {
                var jitter = RandomNumberGenerator.GetInt32(250, 1250);
                await Task.Delay(TimeSpan.FromMilliseconds(delays[attempt] * 1000 + jitter), cancellationToken);
            }
        }
    }

    public void Dispose() => client.Dispose();
}
