using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public interface IContentGatewayPepperVerifier
{
    Task RequireMatchingAsync(CancellationToken cancellationToken);
}

public sealed class ContentGatewayPepperVerifier : IContentGatewayPepperVerifier, IDisposable
{
    private const int SuccessCacheSeconds = 5;

    private readonly ContentGrantTokenHasher _hasher;
    private readonly TimeProvider _time;
    private readonly HttpClient _client;
    private readonly Uri _proofUri;
    private readonly Uri _readyUri;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _verifiedThroughUnixSeconds;

    public ContentGatewayPepperVerifier(
        Uri proofUri,
        ContentGrantTokenHasher hasher,
        TimeProvider time)
    {
        _proofUri = RequireLoopbackProofUri(proofUri);
        _readyUri = RequireLoopbackReadyUri(
            new UriBuilder(_proofUri) { Path = "/ready" }.Uri);
        _hasher = hasher;
        _time = time;
        _client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        })
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
    }

    public async Task RequireMatchingAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (Volatile.Read(ref _verifiedThroughUnixSeconds) >= now) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow().ToUnixTimeSeconds();
            if (Volatile.Read(ref _verifiedThroughUnixSeconds) >= now) return;
            await ProveAsync(cancellationToken);
            Volatile.Write(ref _verifiedThroughUnixSeconds,
                checked(now + SuccessCacheSeconds));
        }
        finally { _gate.Release(); }
    }

    public static Uri RequireLoopbackProofUri(Uri? uri)
    {
        if (uri is null || uri.Scheme != Uri.UriSchemeHttp || uri.Host != "127.0.0.1" ||
            uri.Port != 5191 || uri.AbsolutePath != "/ready/grant-pepper/prove" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException(
                "Content gateway grant pepper proof endpoint is invalid.");
        return uri;
    }

    public static Uri RequireLoopbackReadyUri(Uri? uri)
    {
        if (uri is null || uri.Scheme != Uri.UriSchemeHttp || uri.Host != "127.0.0.1" ||
            uri.Port != 5191 || uri.AbsolutePath != "/ready" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException(
                "Content gateway readiness endpoint is invalid.");
        return uri;
    }

    private async Task ProveAsync(CancellationToken cancellationToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(32);
        var proof = Array.Empty<byte>();
        var body = Array.Empty<byte>();
        try
        {
            proof = _hasher.CreateGatewayReadinessProof(nonce);
            body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = Protocol.SchemaVersion,
                nonce = Convert.ToBase64String(nonce),
                proof = Convert.ToBase64String(proof)
            }, StrictJson.Options);
            using var request = new HttpRequestMessage(HttpMethod.Post, _proofUri)
            {
                Content = new ByteArrayContent(body)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            using var response = await _client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.NoContent) Failure();
            using var readinessRequest = new HttpRequestMessage(HttpMethod.Get, _readyUri);
            using var readinessResponse = await _client.SendAsync(readinessRequest,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (readinessResponse.StatusCode != HttpStatusCode.OK) Failure();
        }
        catch (SuiteException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or
                                           TaskCanceledException or JsonException)
        {
            throw new SuiteException(503, "CONTENT_GATEWAY_NOT_READY",
                "The content gateway is not ready.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            if (proof.Length != 0) CryptographicOperations.ZeroMemory(proof);
            if (body.Length != 0) CryptographicOperations.ZeroMemory(body);
        }
    }

    private static void Failure() => throw new SuiteException(503,
        "CONTENT_GATEWAY_NOT_READY", "The content gateway is not ready.");

    public void Dispose()
    {
        _client.Dispose();
        _gate.Dispose();
    }
}
