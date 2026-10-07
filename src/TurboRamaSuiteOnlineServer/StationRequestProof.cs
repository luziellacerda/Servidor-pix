using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteOnlineServer;

// Proofs cover the credential, request target and small control body. They do
// not hash ROMs, inspect downloaded files or sign game traffic frames.
public sealed class StationRequestProof(TimeProvider clock, int maximumNonces = 262144)
{
    public const string Header = "X-Station-Request-Proof";
    public const string Domain = "TurboRamaStationAndroid/request/v1";
    private readonly object gate = new();
    private readonly Dictionary<string, long> nonces = new(StringComparer.Ordinal);
    private readonly Queue<(string Key, long Expires)> expiry = new();
    private const int WindowSeconds = 90;

    public static byte[] Canonical(string method, string target, ReadOnlySpan<byte> body,
        string credential, long timestamp, string nonce) => Encoding.UTF8.GetBytes(
            $"{Domain}\n{method}\n{target}\n{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}\n" +
            $"{StationProtocol.HashToken(credential)}\n{timestamp.ToString(CultureInfo.InvariantCulture)}\n{nonce}\n");

    public void Verify(string? header, string mode, string spki, string credential,
        string method, string target, ReadOnlySpan<byte> body)
    {
        if (header is null || header.Length is < 80 or > 1024 || body.Length > 8192 ||
            target.Length > 512 || !target.StartsWith("/v1/station/", StringComparison.Ordinal))
            throw Denied("STATION_REQUEST_PROOF_REQUIRED");
        var parts = header.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || parts[1].Length is < 9 or > 12 ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
            parts[1] != timestamp.ToString(CultureInfo.InvariantCulture) ||
            !StationProtocol.IsCanonicalBase64Url(parts[2], 16))
            throw Denied("STATION_REQUEST_PROOF_INVALID");
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (timestamp < now - WindowSeconds || timestamp > now + WindowSeconds)
            throw Denied("STATION_REQUEST_PROOF_EXPIRED");
        try
        {
            var signature = StationProtocol.Decode(parts[3], 512);
            var key = StationProtocol.Decode(spki, 4096);
            var bytes = Canonical(method, target, body, credential, timestamp, parts[2]);
            var valid = false;
            if (mode == "rsa-pss-v1")
            {
                using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(key, out var used);
                valid = used == key.Length && rsa.KeySize == 2048 && signature.Length == 256 &&
                    rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            }
            else if (mode == "ec-p256-v1")
            {
                using var ec = ECDsa.Create(); ec.ImportSubjectPublicKeyInfo(key, out var used);
                valid = used == key.Length && ec.KeySize == 256 &&
                    ec.ExportParameters(false).Curve.Oid.Value == "1.2.840.10045.3.1.7" &&
                    ec.VerifyData(bytes, signature, HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence);
            }
            if (!valid) throw Denied("STATION_REQUEST_PROOF_INVALID");
        }
        catch (Exception ex) when (ex is CryptographicException or SuiteException)
        { throw Denied("STATION_REQUEST_PROOF_INVALID"); }
        // Insert after signature verification so invalid clients cannot fill the cache.
        var nonceKey = StationProtocol.HashToken(credential) + ":" + parts[2];
        lock (gate)
        {
            while (expiry.TryPeek(out var old) && old.Expires < now)
            { expiry.Dequeue(); nonces.Remove(old.Key); }
            if (nonces.ContainsKey(nonceKey)) throw new SuiteException(409,
                "STATION_REQUEST_PROOF_REPLAY", "Station request was already used.");
            if (nonces.Count >= maximumNonces) throw new SuiteException(429,
                "STATION_REQUEST_PROOF_BUSY", "Station request protection is busy.");
            // Queue deadlines are monotonic for normal clocks; retain at least the
            // complete acceptance window, including proofs stamped in the future.
            var until = now + 2 * WindowSeconds + 1;
            if (expiry.TryPeek(out _) && expiry.Count > 0)
                until = Math.Max(until, lastExpiry);
            lastExpiry = until;
            nonces.Add(nonceKey, until); expiry.Enqueue((nonceKey, until));
        }
    }
    private long lastExpiry;
    private static SuiteException Denied(string code) => new(401, code,
        "Station request ownership was not confirmed.");

    public static async Task Guard(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/v1/station") ||
            context.Request.Path == "/v1/station/online/relay") { await next(context); return; }
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal) ||
            !StationProtocol.IsCanonicalBase64Url(header[7..], 32)) { await next(context); return; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var session = await context.RequestServices.GetRequiredService<PostgresStationStore>()
                .FindSessionAsync(header[7..], timeout.Token);
            if (session is not null)
            {
                if (session.ProofMode != "none")
                {
                    if (context.Request.ContentLength is > 8192) throw new SuiteException(413,
                        "STATION_BODY_INVALID", "Station request body is invalid.");
                    context.Request.EnableBuffering(bufferThreshold: 8192, bufferLimit: 8192);
                    using var memory = new MemoryStream();
                    try { await context.Request.Body.CopyToAsync(memory, timeout.Token); }
                    catch (IOException) { throw new SuiteException(413, "STATION_BODY_INVALID",
                        "Station request body is invalid."); }
                    context.Request.Body.Position = 0;
                    context.RequestServices.GetRequiredService<StationRequestProof>().Verify(
                        context.Request.Headers[Header].ToString(), session.ProofMode,
                        session.ProofKeySpki!, header[7..], context.Request.Method,
                        context.Request.Path.Value + context.Request.QueryString.Value, memory.ToArray());
                }
                context.Items[typeof(StationSession)] = (header[7..], session);
            }
            await next(context);
        }
        catch (SuiteException ex) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = ex.StatusCode;
            if (ex.StatusCode == 429) context.Response.Headers.RetryAfter = "1";
            await context.Response.WriteAsJsonAsync(new ErrorResponse(1, ex.Code, ex.Message),
                StrictJson.Options, context.RequestAborted);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested &&
            !context.Response.HasStarted)
        { context.Response.StatusCode = 504; }
    }
}
