using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

// Operators publish a whole file with atomic rename. Invalid or incomplete files
// leave the last valid registry and every running/recovering room untouched.
public sealed class StationMultiplayerProfileMonitor(string path, StationMultiplayer hub,
    ILogger<StationMultiplayerProfileMonitor> logger) : BackgroundService
{
    private readonly object gate = new();
    // Force one read after registration so a publication between the initial
    // load and monitor construction cannot be mistaken for an unchanged file.
    private (long Length, long Ticks) stamp = (-1, -1);
    private static (long, long) Stamp(string file)
    {
        var info = new FileInfo(file);
        return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (0, 0);
    }
    public bool Reload()
    {
        lock (gate)
        {
            var next = Stamp(path);
            if (next == stamp) return false;
            var profiles = Read(path);
            if (Stamp(path) != next) throw new InvalidOperationException("Profile registry changed during reading.");
            hub.ReplaceProfiles(profiles);
            stamp = next;
            return true;
        }
    }
    public static StationMultiplayerProfile[] Read(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
            new FileInfo(path).Length > 16 * 1024 * 1024 ||
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Bounded regular profile registry required.");
        using var file = File.OpenRead(path);
        // The bounded read also covers a file enlarged after its initial stat.
        using var data = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = file.Read(buffer)) != 0)
        {
            if (data.Length + count > 16 * 1024 * 1024)
                throw new InvalidOperationException("Profile registry exceeds the byte limit.");
            data.Write(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<StationMultiplayerProfile[]>(data.ToArray(), StrictJson.Options)
            ?? throw new InvalidOperationException("Profile array required.");
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try { Reload(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or JsonException or ArgumentException or OnlineFailure)
            {
                logger.LogWarning("Station profile reload rejected; last valid registry retained.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
