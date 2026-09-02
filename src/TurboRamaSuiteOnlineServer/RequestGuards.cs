using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;

namespace TurboRamaSuiteOnlineServer;

public static class SuiteTrustedProxyPolicy
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.RequireHeaderSymmetry = true;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    }
}

public sealed class SuiteRateLimiter
{
    private const int CoreRequestsPerMinute = 30;
    private const int ContentRequestsPerMinute = 120;
    private const int MaximumWindowsPerBucket = 4096;
    public const int MaximumIdentitiesPerOriginPerBucket = 256;
    public const int MaximumTrackedWindows = MaximumWindowsPerBucket * 4;

    private readonly TimeProvider _time;
    private readonly object _sync = new();
    private readonly Dictionary<string, Window> _coreWindows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Window> _contentWindows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OriginWindow> _coreOrigins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OriginWindow> _contentOrigins = new(StringComparer.Ordinal);
    private readonly Queue<string> _coreInsertionOrder = new();
    private readonly Queue<string> _contentInsertionOrder = new();
    private long _lastSweepMinute = long.MinValue;

    public SuiteRateLimiter(TimeProvider time) => _time = time;

    public int TrackedWindowCount
    {
        get
        {
            lock (_sync)
                return _coreWindows.Count + _contentWindows.Count +
                       _coreOrigins.Count + _contentOrigins.Count;
        }
    }

    public bool Allow(string origin, string route, object request)
    {
        if (!TryClassify(request, out var license, out var device, out var contentRequest) ||
            !ValidOrigin(origin) || !ValidRoute(route) || !ValidLicense(license) ||
            !ValidDevice(device))
            return false;

        var bucket = contentRequest ? "content" : "core";
        var key = Digest(origin, bucket, route, license, device);
        var originKey = Digest("origin", bucket, origin);
        var minute = _time.GetUtcNow().ToUnixTimeSeconds() / 60;
        var limit = contentRequest ? ContentRequestsPerMinute : CoreRequestsPerMinute;
        lock (_sync)
        {
            Sweep(minute);
            var windows = contentRequest ? _contentWindows : _coreWindows;
            var origins = contentRequest ? _contentOrigins : _coreOrigins;
            var insertionOrder = contentRequest
                ? _contentInsertionOrder
                : _coreInsertionOrder;
            if (windows.TryGetValue(key, out var old))
            {
                var current = old.Minute == minute
                    ? old with { Count = old.Count + 1 }
                    : new Window(minute, 1, old.OriginKey);
                windows[key] = current;
                return current.Count <= limit;
            }

            // A distributed syntactically-valid identity flood must not poison the
            // process-wide cardinality budget. Evict the least-recent identity in
            // this isolated bucket and keep the per-origin fixed budget below.
            if (windows.Count >= MaximumWindowsPerBucket)
                EvictOldest(windows, origins, insertionOrder);
            if (!origins.TryGetValue(originKey, out var originWindow))
            {
                originWindow = new OriginWindow(minute, 0);
            }
            else if (originWindow.Minute != minute)
                originWindow = new OriginWindow(minute, 0);

            if (originWindow.IdentityCount >= MaximumIdentitiesPerOriginPerBucket)
                return false;

            windows.Add(key, new Window(minute, 1, originKey));
            insertionOrder.Enqueue(key);
            origins[originKey] = originWindow with
            {
                IdentityCount = originWindow.IdentityCount + 1
            };
            return true;
        }
    }

    private static void EvictOldest(
        Dictionary<string, Window> windows,
        Dictionary<string, OriginWindow> origins,
        Queue<string> insertionOrder)
    {
        string? victimKey = null;
        Window? victim = null;
        while (insertionOrder.TryDequeue(out var candidate))
        {
            if (!windows.Remove(candidate, out victim)) continue;
            victimKey = candidate;
            break;
        }
        if (victimKey is null || victim is null ||
            !origins.TryGetValue(victim.OriginKey, out var origin)) return;
        if (origin.IdentityCount <= 1)
            origins.Remove(victim.OriginKey);
        else
            origins[victim.OriginKey] = origin with
            {
                IdentityCount = origin.IdentityCount - 1
            };
    }

    private void Sweep(long minute)
    {
        if (_lastSweepMinute == minute) return;
        RemoveExpired(_coreWindows, minute);
        RemoveExpired(_contentWindows, minute);
        RemoveExpired(_coreOrigins, minute);
        RemoveExpired(_contentOrigins, minute);
        RebuildInsertionOrder(_coreInsertionOrder, _coreWindows);
        RebuildInsertionOrder(_contentInsertionOrder, _contentWindows);
        _lastSweepMinute = minute;
    }

    private static void RebuildInsertionOrder(
        Queue<string> insertionOrder,
        Dictionary<string, Window> windows)
    {
        insertionOrder.Clear();
        foreach (var key in windows.Keys) insertionOrder.Enqueue(key);
    }

    private static void RemoveExpired<TWindow>(
        Dictionary<string, TWindow> windows,
        long minute)
        where TWindow : IMinuteWindow
    {
        foreach (var key in windows.Where(pair => pair.Value.Minute < minute)
                     .Select(pair => pair.Key).ToArray())
            windows.Remove(key);
    }

    private static bool TryClassify(
        object request,
        out string license,
        out string device,
        out bool contentRequest)
    {
        contentRequest = false;
        switch (request)
        {
            case ActivationChallengeRequest value when value.Device is not null:
                (license, device) = (value.LicenseId, value.Device.DeviceId);
                return true;
            case ActivationProof value when value.Device is not null:
                (license, device) = (value.LicenseId, value.Device.DeviceId);
                return true;
            case ChallengeRequest value:
                (license, device) = (value.LicenseId, value.DeviceId);
                contentRequest = ContentProtocol.IsContentAction(value.Action);
                return contentRequest || IsCoreSessionAction(value.Action);
            case SessionProof value when value.Proof is not null &&
                                               IsCoreSessionAction(value.Proof.Action):
                (license, device) = (value.Proof.LicenseId, value.Proof.DeviceId);
                return true;
            case CatalogPageProof value when value.Proof is not null &&
                                                  value.Proof.Action ==
                                                  ContentProtocol.CatalogReadAction:
                (license, device) = (value.Proof.LicenseId, value.Proof.DeviceId);
                contentRequest = true;
                return true;
            case DownloadAuthorizationProof value when value.Proof is not null &&
                                                            value.Proof.Action ==
                                                            ContentProtocol.DownloadAuthorizeAction:
                (license, device) = (value.Proof.LicenseId, value.Proof.DeviceId);
                contentRequest = true;
                return true;
            case SuiteDeviceInventoryChallengeRequestV1 value when
                value.Action == SuiteDeviceInventoryProtocol.Action:
                (license, device) = (value.LicenseId, value.DeviceId);
                return true;
            case SuiteDeviceInventoryProofV1 value when
                value.Action == SuiteDeviceInventoryProtocol.Action:
                (license, device) = (value.LicenseId, value.DeviceId);
                return true;
            default:
                license = string.Empty;
                device = string.Empty;
                return false;
        }
    }

    private static bool IsCoreSessionAction(string value) =>
        value is "session.open" or "session.heartbeat";

    private static bool ValidOrigin(string value) =>
        value is { Length: >= 2 and <= 64 } && IPAddress.TryParse(value, out _);

    private static bool ValidRoute(string value) =>
        value is { Length: >= 2 and <= 128 } && value[0] == '/' &&
        value.All(character => char.IsAsciiLetterOrDigit(character) ||
                               character is '/' or '-' or '_' or '.');

    private static bool ValidLicense(string value) =>
        value is { Length: >= 6 and <= 64 } && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool ValidDevice(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Digest(params string[] fields)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join('\0', fields));
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private interface IMinuteWindow
    {
        long Minute { get; }
    }

    private sealed record Window(
        long Minute,
        int Count,
        string OriginKey) : IMinuteWindow;
    private sealed record OriginWindow(long Minute, int IdentityCount) : IMinuteWindow;
}
