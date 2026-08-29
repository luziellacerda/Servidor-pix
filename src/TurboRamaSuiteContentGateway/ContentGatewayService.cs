using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Primitives;
using TurboRamaSuiteOnlineServer;

namespace TurboRamaSuiteContentGateway;

public sealed class ContentGatewayService
{
    private static readonly TimeSpan TransferInactivityTimeout =
        TimeSpan.FromSeconds(90);
    private readonly IContentGatewayStore _store;
    private readonly ContentGrantTokenHasher _tokenHasher;
    private readonly ContentUrlKeyRing _keyRing;
    private readonly SafeUpstreamClient _upstream;
    private readonly TimeProvider _time;
    private readonly ILogger<ContentGatewayService> _logger;
    private readonly ContentGatewayDeploymentGuard _deploymentGuard;

    public ContentGatewayService(
        IContentGatewayStore store,
        ContentGrantTokenHasher tokenHasher,
        ContentUrlKeyRing keyRing,
        SafeUpstreamClient upstream,
        TimeProvider time,
        ContentGatewayDeploymentGuard deploymentGuard,
        ILogger<ContentGatewayService> logger) =>
        (_store, _tokenHasher, _keyRing, _upstream, _time, _deploymentGuard, _logger) =
        (store, tokenHasher, keyRing, upstream, time, deploymentGuard, logger);

    public async Task StreamAsync(
        HttpContext context,
        string grantId,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new SuiteException(410, "CONTENT_RELAY_DISABLED",
            "Server-side content transfer is permanently disabled.");
    }

    public async Task<Uri> AuthorizeDirectAsync(
        HttpContext context,
        string grantId,
        CancellationToken cancellationToken)
    {
        ValidateGrantId(grantId);
        await _deploymentGuard.RequireReadyAsync(cancellationToken);
        var bearer = ParseBearer(context.Request.Headers.Authorization);
        var requestedRange = ParseRange(context.Request.Headers.Range);
        var tokenDigest = _tokenHasher.Digest(bearer);
        ClaimedContentGrantRecord? grant = null;
        try
        {
            grant = await _store.ClaimDownloadGrantAsync(grantId, tokenDigest,
                requestedRange, Now(), cancellationToken);
            ValidateGrantMetadata(grant);
            ValidateIfRange(context.Request.Headers.IfRange, requestedRange,
                grant.SourceETag, grant.SourceLastModified);
            var uri = _keyRing.Decrypt(grant);
            _upstream.ValidateUri(uri);
            await TryFinalizeAsync(grantId, true, null);
            return uri;
        }
        catch
        {
            if (grant is not null) await TryFinalizeAsync(grantId, false, "AUTHORIZATION_DENIED");
            throw;
        }
        finally
        {
            if (grant is not null)
            {
                CryptographicOperations.ZeroMemory(grant.UpstreamUrlCiphertext);
                CryptographicOperations.ZeroMemory(grant.UpstreamUrlNonce);
                CryptographicOperations.ZeroMemory(grant.UpstreamUrlTag);
            }
        }
    }

    private static TransferPlan ValidateUpstream(
        HttpResponseMessage response,
        ClaimedContentGrantRecord grant,
        long requestedRange)
    {
        if (response.Headers.Location is not null ||
            response.Content.Headers.ContentEncoding.Count != 0)
            throw UpstreamFailure();
        ValidateSourceValidators(response, grant);
        ContentGatewayUpstreamPolicy.RequireExpectedStatus(
            response.StatusCode, requestedRange);

        if (requestedRange == 0)
        {
            if (response.Content.Headers.ContentLength is not long responseLength)
                throw UpstreamFailure();
            return new TransferPlan(HttpStatusCode.OK, 0, responseLength,
                responseLength, true);
        }

        var contentRange = response.Content.Headers.ContentRange;
        var expectedLength = response.Content.Headers.ContentLength ?? throw UpstreamFailure();
        if (contentRange?.Unit != "bytes" || contentRange.From != requestedRange ||
            response.Content.Headers.ContentLength != expectedLength)
            throw UpstreamFailure();
        return new TransferPlan(HttpStatusCode.PartialContent, requestedRange,
            expectedLength, contentRange.Length ?? checked(requestedRange + expectedLength), false);
    }

    private static void ValidateSourceValidators(
        HttpResponseMessage response,
        ClaimedContentGrantRecord grant)
    {
        if (grant.SourceETag is not null &&
            !string.Equals(response.Headers.ETag?.ToString(), grant.SourceETag,
                StringComparison.Ordinal))
            throw UpstreamFailure();
        if (grant.SourceLastModified is null) return;
        if (!DateTimeOffset.TryParse(grant.SourceLastModified,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var expected) || response.Content.Headers.LastModified is null ||
            response.Content.Headers.LastModified.Value.ToUniversalTime()
                .ToUnixTimeSeconds() != expected.ToUniversalTime().ToUnixTimeSeconds())
            throw UpstreamFailure();
    }

    private static void ConfigureResponse(
        HttpResponse response,
        ClaimedContentGrantRecord grant,
        string ownETag,
        TransferPlan transfer)
    {
        response.StatusCode = (int)transfer.StatusCode;
        response.ContentType = "application/octet-stream";
        response.ContentLength = transfer.ResponseLength;
        response.Headers.ETag = ownETag;
        response.Headers.AcceptRanges = "bytes";
        response.Headers.Remove("Location");
        var disposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = grant.SafeFileName
        };
        response.Headers.ContentDisposition = disposition.ToString();
        if (transfer.StatusCode == HttpStatusCode.PartialContent)
            response.Headers.ContentRange = string.Create(CultureInfo.InvariantCulture,
                $"bytes {transfer.SourceStart}-{transfer.TotalLength - 1}/{transfer.TotalLength}");
    }

    private async Task CopyExactAsync(
        HttpResponseMessage sourceResponse,
        HttpResponse destinationResponse,
        string grantId,
        string expectedSha256,
        TransferPlan transfer,
        CancellationToken cancellationToken)
    {
        await using var source = await sourceResponse.Content.ReadAsStreamAsync(
            cancellationToken);
        using var hash = transfer.VerifyWholeArtifact
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            : null;
        var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        using var transferCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var authorizationGate = new SemaphoreSlim(1, 1);
        var monitorFailure = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bytesSinceAuthorizationCheck = 0L;
        var authorizationMonitor = MonitorAuthorizationAsync(grantId,
            authorizationGate, () => Interlocked.Exchange(
                ref bytesSinceAuthorizationCheck, 0), transferCancellation,
            monitorFailure);
        try
        {
            var remaining = transfer.ResponseLength;
            while (remaining > 0)
            {
                var read = await ReadWithInactivityTimeoutAsync(source,
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    transferCancellation.Token);
                if (read == 0) throw UpstreamFailure();
                hash?.AppendData(buffer.AsSpan(0, read));
                await WriteWithInactivityTimeoutAsync(destinationResponse.Body,
                    buffer.AsMemory(0, read), transferCancellation.Token);
                remaining -= read;
                var transferred = Interlocked.Add(
                    ref bytesSinceAuthorizationCheck, read);
                if (ContentGatewayRevalidationPolicy.IsDue(TimeSpan.Zero, transferred))
                {
                    await RequireCurrentAuthorizationAsync(grantId,
                        authorizationGate, transferCancellation.Token);
                    Interlocked.Exchange(ref bytesSinceAuthorizationCheck, 0);
                }
            }
            if (await ReadWithInactivityTimeoutAsync(source, buffer.AsMemory(0, 1),
                    transferCancellation.Token) != 0)
                throw UpstreamFailure();
            ThrowMonitorFailure(monitorFailure);
            if (hash is not null)
            {
                var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (!Protocol.FixedEquals(actual, expectedSha256)) throw UpstreamFailure();
            }
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && monitorFailure.Task.IsCompleted)
        {
            ThrowMonitorFailure(monitorFailure);
            throw;
        }
        finally
        {
            transferCancellation.Cancel();
            await authorizationMonitor;
            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }
        if (!cancellationToken.IsCancellationRequested)
            ThrowMonitorFailure(monitorFailure);
    }

    private async Task MonitorAuthorizationAsync(
        string grantId,
        SemaphoreSlim authorizationGate,
        Action resetTransferredBytes,
        CancellationTokenSource transferCancellation,
        TaskCompletionSource<Exception> failure)
    {
        try
        {
            while (true)
            {
                await Task.Delay(ContentGatewayRevalidationPolicy.MaximumInterval,
                    _time, transferCancellation.Token);
                await RequireCurrentAuthorizationAsync(grantId, authorizationGate,
                    transferCancellation.Token);
                resetTransferredBytes();
            }
        }
        catch (OperationCanceledException) when (transferCancellation.IsCancellationRequested)
        {
            // Normal transfer completion/client cancellation stops the monitor.
        }
        catch (Exception exception)
        {
            failure.TrySetResult(exception);
            transferCancellation.Cancel();
        }
    }

    private async Task RequireCurrentAuthorizationAsync(
        string grantId,
        SemaphoreSlim authorizationGate,
        CancellationToken cancellationToken)
    {
        await authorizationGate.WaitAsync(cancellationToken);
        try
        {
            await ContentGatewayRevalidationPolicy.RequireCurrentAsync(
                token => _store.IsDownloadGrantAuthorizationCurrentAsync(
                    grantId, Now(), token),
                ContentGatewayRevalidationPolicy.MaximumInterval, cancellationToken);
        }
        finally { authorizationGate.Release(); }
    }

    private static void ThrowMonitorFailure(
        TaskCompletionSource<Exception> monitorFailure)
    {
        if (monitorFailure.Task.IsCompleted)
            throw monitorFailure.Task.GetAwaiter().GetResult();
    }

    private static async Task<int> ReadWithInactivityTimeoutAsync(
        Stream source,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TransferInactivityTimeout);
        try { return await source.ReadAsync(destination, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw UpstreamFailure();
        }
    }

    private static async Task WriteWithInactivityTimeoutAsync(
        Stream destination,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TransferInactivityTimeout);
        try { await destination.WriteAsync(source, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw UpstreamFailure();
        }
    }

    private static void ValidateGrantMetadata(ClaimedContentGrantRecord grant)
    {
        var descriptor = new ContentArtifactDescriptor(grant.ArtifactId,
            grant.ArtifactVersion, grant.SafeFileName, grant.FileExtension, grant.ExtractPolicy,
            grant.ManifestIdentity);
        ContentProtocol.ValidateGrantId(grant.GrantId);
        ContentProtocol.Validate(descriptor);
        var expectedHash = ContentProtocol.DescriptorHash(grant.ItemId, descriptor);
        if (!Protocol.FixedEquals(expectedHash, grant.DescriptorHash))
            throw new SuiteException(502, "CONTENT_UPSTREAM_FAILURE",
                "The content transfer could not be completed.");
    }

    private static string ParseBearer(StringValues values)
    {
        if (values.Count != 1) NotAvailable();
        var value = values[0] ?? string.Empty;
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            value.Length != prefix.Length + 43)
            NotAvailable();
        var token = value[prefix.Length..];
        if (token.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
            NotAvailable();
        return token;
    }

    private static long ParseRange(StringValues values)
    {
        if (values.Count == 0) return 0;
        if (values.Count != 1) NotAvailable();
        var value = values[0] ?? string.Empty;
        const string prefix = "bytes=";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith('-') ||
            value.AsSpan(prefix.Length, value.Length - prefix.Length - 1).Contains(','))
            NotAvailable();
        var number = value[prefix.Length..^1];
        if (number.Length == 0 || number.Any(character => !char.IsAsciiDigit(character)))
            NotAvailable();
        var result = -1L;
        if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture,
                out result) || result < 0)
            NotAvailable();
        return result;
    }

    private static void ValidateIfRange(
        StringValues values,
        long requestedRange,
        string? sourceETag,
        string? sourceLastModified)
    {
        if (requestedRange == 0)
        {
            if (values.Count != 0) NotAvailable();
            return;
        }
        if (values.Count != 1 ||
            (!string.Equals(values[0], sourceETag, StringComparison.Ordinal) &&
             !string.Equals(values[0], sourceLastModified, StringComparison.Ordinal)))
            NotAvailable();
    }

    private static void ValidateGrantId(string grantId)
    {
        try { ContentProtocol.ValidateGrantId(grantId); }
        catch (SuiteException) { NotAvailable(); }
    }

    private async Task TryFinalizeAsync(
        string grantId,
        bool succeeded,
        string? failureCode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await _store.CompleteDownloadGrantAsync(grantId, succeeded, failureCode,
                timeout.Token);
        }
        catch (Exception)
        {
            // Store exceptions can include command/connection details; keep this log URL/token-free.
            _logger.LogError(
                "Content grant finalization failed without exposing private origin data.");
        }
    }

    private long Now() => _time.GetUtcNow().ToUnixTimeSeconds();

    private static SuiteException UpstreamFailure() => new(502,
        "CONTENT_UPSTREAM_FAILURE", "The content transfer could not be completed.");

    private static void NotAvailable() => throw new SuiteException(404,
        "CONTENT_NOT_AVAILABLE", "The requested content is not available.");

    private sealed record TransferPlan(
        HttpStatusCode StatusCode,
        long SourceStart,
        long ResponseLength,
        long TotalLength,
        bool VerifyWholeArtifact);
}

public static class ContentGatewayUpstreamPolicy
{
    public static void RequireExpectedStatus(
        HttpStatusCode statusCode,
        long requestedRange)
    {
        if (requestedRange < 0)
            throw new ArgumentOutOfRangeException(nameof(requestedRange));
        var expected = requestedRange == 0
            ? HttpStatusCode.OK
            : HttpStatusCode.PartialContent;
        if (statusCode != expected)
            throw new SuiteException(502, "CONTENT_UPSTREAM_FAILURE",
                "The content transfer could not be completed.");
    }
}

public static class ContentGatewayRevalidationPolicy
{
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromSeconds(5);
    public const long MaximumBytes = 8L * 1024 * 1024;

    public static bool IsDue(TimeSpan elapsed, long bytesTransferred) =>
        elapsed >= MaximumInterval || bytesTransferred >= MaximumBytes;

    public static async Task RequireCurrentAsync(
        Func<CancellationToken, Task<bool>> authorizationProbe,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorizationProbe);
        if (timeout <= TimeSpan.Zero || timeout > MaximumInterval)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        using var validationTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        validationTimeout.CancelAfter(timeout);
        try
        {
            if (!await authorizationProbe(validationTimeout.Token))
                throw new SuiteException(403, "CONTENT_DENIED",
                    "Content access is not authorized.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SuiteException(503, "CONTENT_AUTHORIZATION_UNAVAILABLE",
                "Content authorization could not be revalidated.");
        }
    }
}
