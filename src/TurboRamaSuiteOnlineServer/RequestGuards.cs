using System.Collections.Concurrent;

namespace TurboRamaSuiteOnlineServer;

public sealed class SuiteRateLimiter
{
    private const int RequestsPerMinute = 30;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public SuiteRateLimiter(TimeProvider time) => _time = time;

    public bool Allow(string origin, string route, object request)
    {
        var (license, device) = request switch
        {
            ActivationChallengeRequest value => (value.LicenseId, value.Device.DeviceId),
            ActivationProof value => (value.LicenseId, value.Device.DeviceId),
            ChallengeRequest value => (value.LicenseId, value.DeviceId),
            SessionProof value => (value.Proof.LicenseId, value.Proof.DeviceId),
            _ => ("unknown", "unknown")
        };
        var minute = _time.GetUtcNow().ToUnixTimeSeconds() / 60;
        var key = string.Concat(origin, "\n", route, "\n", license, "\n", device);
        var current = _windows.AddOrUpdate(key, _ => new(minute, 1), (_, old) =>
            old.Minute == minute ? old with { Count = old.Count + 1 } : new(minute, 1));
        if (_windows.Count > 10_000)
            foreach (var pair in _windows.Where(pair => pair.Value.Minute < minute - 1).Take(1_000))
                _windows.TryRemove(pair.Key, out _);
        return current.Count <= RequestsPerMinute;
    }

    private sealed record Window(long Minute, int Count);
}
