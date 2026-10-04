namespace TurboRamaSuiteOnlineServer;

// Atomically exchange validated immutable snapshots; a bad write keeps the last good one.
public sealed class StationLibraryMonitor(string path, StationLibrary initial) : BackgroundService
{
    private StationLibrary current = initial;
    private readonly object gate = new();
    private readonly List<(StationLibrary Library, DateTimeOffset Until)> previous = [];
    private (long Length, long Ticks) stamp = FileStamp(path);
    public StationLibrary Current => Volatile.Read(ref current);
    private static (long, long) FileStamp(string file)
    {
        var info = new FileInfo(file);
        return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (0, 0);
    }
    public bool Reload()
    {
        var nextStamp = FileStamp(path);
        if (nextStamp == stamp) return false;
        var before = Current;
        var next = StationLibrary.TryLoad(path, before);
        if (next is null || next.Revision <= before.Revision || FileStamp(path) != nextStamp)
            throw new InvalidOperationException("Station catalog revision did not advance atomically.");
        lock (gate)
        {
            previous.RemoveAll(x => x.Until < DateTimeOffset.UtcNow);
            previous.Add((before, DateTimeOffset.UtcNow.AddSeconds(120)));
            Volatile.Write(ref current, next);
            stamp = nextStamp;
        }
        return true;
    }
    public bool TryResolveGrant(string id, string file, long revision, out StationResolvedArtifact artifact)
    {
        lock (gate)
        {
            previous.RemoveAll(x => x.Until < DateTimeOffset.UtcNow);
            foreach (var snapshot in new[] { Current }.Concat(previous.Select(x => x.Library)))
                if (snapshot.TryResolveArtifact(id, out artifact) && artifact.FilePath == file &&
                    artifact.Entry.Revision == revision) return true;
        }
        artifact = null!;
        return false;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { Reload(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or System.Text.Json.JsonException or ArgumentException)
            {
                // No index paths, filenames, credentials or exception messages in the log.
                Console.Error.WriteLine("Station catalog reload rejected; last valid catalog retained.");
            }
        }
    }
}
