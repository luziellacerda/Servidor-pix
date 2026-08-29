using System.Security.Cryptography;
using TurboRamaSuiteOnlineServer;

namespace TurboRamaSuiteContentGateway;

public sealed class ContentGatewayDeploymentGuard
{
    private const int SuccessCacheSeconds = 5;
    private readonly IContentGatewayStore _store;
    private readonly ContentUrlKeyRing _keyRing;
    private readonly SafeUpstreamClient _upstream;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _verifiedThroughUnixSeconds;

    public ContentGatewayDeploymentGuard(
        IContentGatewayStore store,
        ContentUrlKeyRing keyRing,
        SafeUpstreamClient upstream,
        TimeProvider time) =>
        (_store, _keyRing, _upstream, _time) =
        (store, keyRing, upstream, time);

    public async Task RequireReadyAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (Volatile.Read(ref _verifiedThroughUnixSeconds) >= now) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow().ToUnixTimeSeconds();
            if (Volatile.Read(ref _verifiedThroughUnixSeconds) >= now) return;
            var keySetFingerprint = _keyRing.CopyKeySetFingerprint();
            var allowlistFingerprint = _upstream.CopyAllowlistFingerprint();
            try
            {
                if (!await _store.IsProductionGatewayReadyAsync(
                        _keyRing.ActiveKeyVersion,
                        Convert.ToHexString(keySetFingerprint).ToLowerInvariant(),
                        Convert.ToHexString(allowlistFingerprint).ToLowerInvariant(),
                        _keyRing.CopyKeyVersions(), cancellationToken))
                    throw NotReady();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(keySetFingerprint);
                CryptographicOperations.ZeroMemory(allowlistFingerprint);
            }
            Volatile.Write(ref _verifiedThroughUnixSeconds,
                checked(now + SuccessCacheSeconds));
        }
        finally { _gate.Release(); }
    }

    private static SuiteException NotReady() => new(503,
        "CONTENT_GATEWAY_NOT_READY", "The content gateway is not ready.");
}
