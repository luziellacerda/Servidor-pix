using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurboRamaSuiteOnlineServer;

public sealed record StationCarouselEntry(string Asset, string Sha256, long SizeBytes,
    string ContentType, int Width, int Height, int Fps, int AudioTracks);

public sealed record StationCarouselSnapshot(long Revision, IReadOnlyList<StationCarouselEntry> Items);

/// <summary>Immutable, operator-published MP4s; invalid updates retain the last valid snapshot.</summary>
public sealed class StationCarouselRegistry(string root, TimeProvider? time = null)
{
    public const int MaximumItems = 128;
    public const long MaximumFileBytes = 32L * 1024 * 1024;
    public const long MaximumTotalBytes = 256L * 1024 * 1024;
    private readonly string directory = Path.GetFullPath(root);
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1);
    private StationCarouselSnapshot? current;
    private DateTimeOffset nextRead;
    private string? acceptedIndexHash;
    public static bool SafeHash(string value) => Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z");
    public static bool SafeAsset(string value) => Regex.IsMatch(value,
        "\\Aturbo-system-videos/720-[a-z0-9][a-z0-9_-]{0,95}\\.mp4\\z");

    public async Task<StationCarouselSnapshot?> ReadAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (clock.GetUtcNow() < nextRead) return current;
            nextRead = clock.GetUtcNow().AddSeconds(60);
            try
            {
                CheckPath(directory);
                var index = Path.Combine(directory, "index.json");
                CheckPath(index);
                var info = new FileInfo(index);
                if (info.Length is < 1 or > 131072) throw new IOException("Invalid media index size.");
                var bytes = await File.ReadAllBytesAsync(index, token);
                if (bytes.Length > 131072) throw new IOException("Invalid media index size.");
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (hash == acceptedIndexHash) return current;
                var candidate = await ValidateAsync(bytes, token);
                if (current is not null && candidate.Revision <= current.Revision)
                    throw new IOException("Media revision must increase.");
                current = candidate;
                acceptedIndexHash = hash;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
            {
                // Bad publication cannot remove already validated media or affect game endpoints.
            }
            return current;
        }
        finally { gate.Release(); }
    }

    private async Task<StationCarouselSnapshot> ValidateAsync(byte[] bytes, CancellationToken token)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        var data = json.RootElement;
        if (data.GetProperty("schemaVersion").GetInt32() != 1) throw new IOException("Invalid media schema.");
        var revision = data.GetProperty("revision").GetInt64();
        if (revision < 1 || revision > 9007199254740991L) throw new IOException("Invalid media revision.");
        var rows = data.GetProperty("items");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() is < 1 or > MaximumItems)
            throw new IOException("Invalid media count.");
        var entries = new List<StationCarouselEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var row in rows.EnumerateArray())
        {
            var item = JsonSerializer.Deserialize<StationCarouselEntry>(row.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (item is null || item.Asset is null || item.Sha256 is null || !SafeAsset(item.Asset) || !names.Add(item.Asset)
                || !SafeHash(item.Sha256) || item.SizeBytes is < 16 or > MaximumFileBytes
                || item.ContentType != "video/mp4" || item.Width != 720 || item.Height != 720 || item.Fps != 30 || item.AudioTracks != 0
                || (total += item.SizeBytes) > MaximumTotalBytes)
                throw new IOException("Invalid media entry.");
            var path = FilePath(item.Sha256);
            CheckPath(path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != item.SizeBytes) throw new IOException("Media length mismatch.");
            var prefix = new byte[12];
            await stream.ReadExactlyAsync(prefix, token);
            if (prefix[4] != 'f' || prefix[5] != 't' || prefix[6] != 'y' || prefix[7] != 'p')
                throw new IOException("Invalid MP4 signature.");
            stream.Position = 0;
            if (Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant() != item.Sha256)
                throw new IOException("Media hash mismatch.");
            entries.Add(item);
        }
        return new StationCarouselSnapshot(revision, entries.AsReadOnly());
    }

    public string FilePath(string hash)
    {
        if (!SafeHash(hash)) throw new IOException("Invalid media hash.");
        return Path.Combine(directory, hash + ".mp4");
    }
    public static void CheckPath(string path)
    {
        // Refuse symlinks/reparse points in the file AND every parent; no arbitrary manifest paths.
        for (string? check = Path.GetFullPath(path); check is not null; check = Path.GetDirectoryName(check))
            if ((File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic media path refused.");
    }
}

public static class StationCarouselEndpoints
{
    public static void MapStationCarouselMedia(this WebApplication app, bool stationEnabled)
    {
        var enabled = stationEnabled && app.Configuration.GetValue("Station:CarouselMediaEnabled", false);
        var root = app.Configuration["Station:CarouselMediaDirectory"];
        var registry = enabled && !string.IsNullOrWhiteSpace(root) ? new StationCarouselRegistry(root) : null;
        var limiter = new StationRateLimiter(originRequestsPerMinute: 120);

        app.MapGet("/v1/station/media/catalog", async (HttpContext context) =>
        {
            if (registry is null) return Error(503, "STATION_MEDIA_NOT_READY");
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "station-media-catalog")) return Error(429, "STATION_RATE_LIMITED");
            var requestId = context.Request.Query["requestId"].ToString();
            if (!Regex.IsMatch(requestId, "\\A[0-9a-f]{32}\\z")) return Error(400, "STATION_MEDIA_REQUEST_INVALID");
            try
            {
                var session = await context.RequestServices.GetRequiredService<StationService>().Session(Bearer(context), context.RequestAborted);
                var snapshot = await registry.ReadAsync(context.RequestAborted);
                if (snapshot is null) return Error(503, "STATION_MEDIA_NOT_READY");
                context.Response.Headers.CacheControl = "no-store";
                return Results.Json(context.RequestServices.GetRequiredService<StationResponseSigner>().Sign(new
                {
                    schemaVersion = 1, domain = StationProtocol.Prefix + "media-catalog/v1",
                    productId = StationProtocol.Product, applicationId = StationProtocol.Application,
                    licenseId = session.LicenseId, deviceId = session.DeviceId, sessionId = session.SessionId,
                    requestId, revision = snapshot.Revision, items = snapshot.Items
                }), StrictJson.Options);
            }
            catch (SuiteException error) { return Error(error.StatusCode, error.Code); }
        }).TraceStation("media-catalog");

        app.MapGet("/v1/station/media/files/{hash}", async (HttpContext context, string hash) =>
        {
            if (registry is null) return Error(503, "STATION_MEDIA_NOT_READY");
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "station-media-file")) return Error(429, "STATION_RATE_LIMITED");
            try
            {
                _ = await context.RequestServices.GetRequiredService<StationService>().Session(Bearer(context), context.RequestAborted);
                if (!StationCarouselRegistry.SafeHash(hash)) return Error(404, "STATION_MEDIA_NOT_FOUND");
                var snapshot = await registry.ReadAsync(context.RequestAborted);
                var item = snapshot?.Items.FirstOrDefault(x => x.Sha256 == hash);
                if (item is null) return Error(404, "STATION_MEDIA_NOT_FOUND");
                var path = registry.FilePath(hash); StationCarouselRegistry.CheckPath(path);
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (file.Length != item.SizeBytes) return Error(404, "STATION_MEDIA_NOT_FOUND");
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.ContentType = item.ContentType; context.Response.ContentLength = item.SizeBytes;
                await file.CopyToAsync(context.Response.Body, context.RequestAborted);
                return Results.Empty;
            }
            catch (SuiteException error) { return Error(error.StatusCode, error.Code); }
            catch (IOException) when (!context.Response.HasStarted) { return Error(404, "STATION_MEDIA_NOT_FOUND"); }
            catch (UnauthorizedAccessException) when (!context.Response.HasStarted) { return Error(404, "STATION_MEDIA_NOT_FOUND"); }
        }).TraceStation("media-file");
    }
    private static string Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) && header.Length <= 128 ? header[7..] : "";
    }
    private static IResult Error(int status, string code) => Results.Json(new ErrorResponse(1, code,
        "Station media is unavailable."), StrictJson.Options, statusCode: status);
}
