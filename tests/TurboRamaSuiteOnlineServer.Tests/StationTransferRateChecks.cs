using System.Net;
using TurboRamaSuiteOnlineServer;

internal static class StationTransferRateChecks
{
    public static void Run()
    {
        var clock = new Clock();
        var limiter = new StationRateLimiter(clock);
        var origin = IPAddress.Parse("192.0.2.1");
        var device = StationProtocol.Encode(new byte[32]);
        var otherDevice = StationProtocol.Encode(Enumerable.Repeat((byte)1, 32).ToArray());
        const string license = "STATION_SYNTHETIC_RATE";
        void Check(bool condition, string why)
        { if (!condition) throw new Exception("Station transfer rate: " + why); }

        // One complete catalog fits in a minute; a device on the same NAT has its own budget.
        for (var i = 0; i < StationRateLimiter.CoverDeviceRequestsPerMinute; i++)
        {
            Check(limiter.Allow(origin, "/v1/station/covers"), "cover origin accepts full catalog");
            Check(limiter.AllowCoverDevice(license, device), "device accepts full catalog");
        }
        Check(!limiter.AllowCoverDevice(license, device), "device budget rejects overflow");
        Check(limiter.AllowCoverDevice(license, otherDevice), "another device on NAT remains usable");
        Check(limiter.AllowCoverDevice("STATION_OTHER_LICENSE", device), "licenses remain isolated");
        Check(!limiter.AllowCoverDevice(new string('x', 10000), device), "invalid identity is not tracked");
        foreach (var route in new[] { "/v1/station/sessions", "/v1/station/downloads/authorize", "/v1/station/artifacts" })
        {
            for (var i = 0; i < 30; i++) Check(limiter.Allow(origin, route), "original allowance");
            Check(!limiter.Allow(origin, route), "original limit remains 30");
        }
        for (var i = StationRateLimiter.CoverDeviceRequestsPerMinute; i < StationRateLimiter.CoverOriginRequestsPerMinute; i++)
            Check(limiter.Allow(origin, "/v1/station/covers"), "NAT cover origin allowance");
        Check(!limiter.Allow(origin, "/v1/station/covers"), "origin budget is bounded");
        clock.Now = clock.Now.AddMinutes(1);
        Check(limiter.Allow(origin, "/v1/station/covers") && limiter.AllowCoverDevice(license, device), "next minute recovers");

        var concurrent = new StationRateLimiter(clock);
        var allowed = 0;
        Parallel.For(0, StationRateLimiter.CoverDeviceRequestsPerMinute + 64, _ =>
        {
            if (concurrent.AllowCoverDevice(license, device)) Interlocked.Increment(ref allowed);
        });
        Check(allowed == StationRateLimiter.CoverDeviceRequestsPerMinute, "parallel requests cannot overrun device budget");
        Console.WriteLine("STATION TRANSFER RATE: OK (full catalog, NAT isolation, concurrent bound, original auth/download limits)");
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
