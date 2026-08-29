using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteContentGateway;

public sealed class GatewayRateLimiter
{
    private const int ClientRequestsPerMinute = 120;
    private const int GrantRequestsPerMinute = 12;
    private const int MaximumWindowsPerBucket = 4096;
    public const int MaximumTrackedWindows = MaximumWindowsPerBucket * 2;

    private readonly TimeProvider _time;
    private readonly object _sync = new();
    private readonly Dictionary<string, Window> _clientWindows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Window> _grantWindows = new(StringComparer.Ordinal);
    private readonly Queue<string> _clientInsertionOrder = new();
    private readonly Queue<string> _grantInsertionOrder = new();
    private long _lastSweepMinute = long.MinValue;
    private long _proofMinute = long.MinValue;
    private int _proofCount;
    private long _grantPepperProofMinute = long.MinValue;
    private int _grantPepperProofCount;

    public GatewayRateLimiter(TimeProvider time) => _time = time;

    public int TrackedWindowCount
    {
        get
        {
            lock (_sync) return _clientWindows.Count + _grantWindows.Count;
        }
    }

    public bool Allow(string origin, string grantId)
    {
        if (origin is not { Length: >= 2 and <= 64 } ||
            !IPAddress.TryParse(origin, out _) || !ValidGrantId(grantId))
            return false;
        var clientKey = Digest("client", origin);
        var grantKey = Digest("grant", grantId);
        var minute = _time.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (_sync)
        {
            Sweep(minute);
            var clientCount = Increment(_clientWindows, _clientInsertionOrder,
                clientKey, minute);
            if (clientCount > ClientRequestsPerMinute) return false;
            var grantCount = Increment(_grantWindows, _grantInsertionOrder,
                grantKey, minute);
            return grantCount is >= 1 and <= GrantRequestsPerMinute;
        }
    }

    public bool AllowKeyRingProof(string origin)
        => AllowLoopbackProof(origin, 30, ref _proofMinute, ref _proofCount);

    public bool AllowGrantPepperProof(string origin)
        => AllowLoopbackProof(origin, 120, ref _grantPepperProofMinute,
            ref _grantPepperProofCount);

    private bool AllowLoopbackProof(
        string origin,
        int limit,
        ref long trackedMinute,
        ref int trackedCount)
    {
        if (!IPAddress.TryParse(origin, out var address) || !IPAddress.IsLoopback(address))
            return false;
        var minute = _time.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (_sync)
        {
            if (trackedMinute != minute)
            {
                trackedMinute = minute;
                trackedCount = 0;
            }
            trackedCount++;
            return trackedCount <= limit;
        }
    }

    private int Increment(
        Dictionary<string, Window> windows,
        Queue<string> insertionOrder,
        string key,
        long minute)
    {
        if (windows.TryGetValue(key, out var old))
        {
            var current = old.Minute == minute
                ? old with { Count = old.Count + 1 }
                : new Window(minute, 1);
            windows[key] = current;
            return current.Count;
        }
        // Bounded least-recent eviction preserves availability for a real grant
        // after a distributed flood instead of turning cardinality into a global
        // denial switch. Edge request/connection limits remain the first layer.
        if (windows.Count >= MaximumWindowsPerBucket)
        {
            while (insertionOrder.TryDequeue(out var victim))
                if (windows.Remove(victim)) break;
        }
        windows.Add(key, new Window(minute, 1));
        insertionOrder.Enqueue(key);
        return 1;
    }

    private void Sweep(long minute)
    {
        if (_lastSweepMinute == minute) return;
        RemoveExpired(_clientWindows, minute);
        RemoveExpired(_grantWindows, minute);
        RebuildInsertionOrder(_clientInsertionOrder, _clientWindows);
        RebuildInsertionOrder(_grantInsertionOrder, _grantWindows);
        _lastSweepMinute = minute;
    }

    private static void RebuildInsertionOrder(
        Queue<string> insertionOrder,
        Dictionary<string, Window> windows)
    {
        insertionOrder.Clear();
        foreach (var key in windows.Keys) insertionOrder.Enqueue(key);
    }

    private static void RemoveExpired(Dictionary<string, Window> windows, long minute)
    {
        foreach (var key in windows.Where(pair => pair.Value.Minute < minute)
                     .Select(pair => pair.Key).ToArray())
            windows.Remove(key);
    }

    private static bool ValidGrantId(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Digest(string domain, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(domain + '\0' + value);
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed record Window(long Minute, int Count);
}
