using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentMonitor;

internal static class GatewayDeploymentPreflight
{
    public static Task<MonitorDeploymentProvenance> RequireAsync(Uri uri, MonitorKeyRing originKeys,
        OriginPolicy originPolicy, CancellationToken cancellationToken) =>
        RequireAsync(uri, originKeys, originPolicy.CopyDeploymentFingerprint(),
            cancellationToken, null);

    internal static async Task<MonitorDeploymentProvenance> RequireAsync(Uri uri,
        MonitorKeyRing originKeys,
        byte[] allowlistFingerprint, CancellationToken cancellationToken,
        HttpMessageHandler? testHandler)
    {
        var keySetFingerprint = Array.Empty<byte>();
        var nonce = Array.Empty<byte>();
        var proof = Array.Empty<byte>();
        var body = Array.Empty<byte>();
        try
        {
            if (allowlistFingerprint.Length != 32)
                throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
            keySetFingerprint = originKeys.CopyDeploymentKeySetFingerprint();
            nonce = RandomNumberGenerator.GetBytes(32);
            proof = originKeys.CreateDeploymentProof(keySetFingerprint,
                allowlistFingerprint, nonce);
            var provenance = new MonitorDeploymentProvenance(originKeys.ActiveVersion,
                Convert.ToHexString(keySetFingerprint).ToLowerInvariant(),
                Convert.ToHexString(allowlistFingerprint).ToLowerInvariant());
            body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                activeKeyVersion = provenance.ActiveKeyVersion,
                keySetFingerprint = provenance.KeySetFingerprint,
                allowlistFingerprint = provenance.AllowlistFingerprint,
                nonce = Convert.ToBase64String(nonce),
                proof = Convert.ToBase64String(proof)
            });
            var ownsHandler = testHandler is null;
            var handler = testHandler ?? new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            using var client = new HttpClient(handler, ownsHandler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
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
                throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
            return provenance;
        }
        catch (MonitorFailure) { throw; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or
            InvalidOperationException)
        {
            throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(allowlistFingerprint);
            if (keySetFingerprint.Length != 0)
                CryptographicOperations.ZeroMemory(keySetFingerprint);
            if (nonce.Length != 0) CryptographicOperations.ZeroMemory(nonce);
            if (proof.Length != 0) CryptographicOperations.ZeroMemory(proof);
            if (body.Length != 0) CryptographicOperations.ZeroMemory(body);
        }
    }
}
