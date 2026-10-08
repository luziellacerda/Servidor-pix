using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public sealed class StationAttestationStatus(HttpClient client, TimeProvider clock)
    : BackgroundService, IStationAttestationStatus
{
    private sealed record Snapshot(HashSet<string> Revoked, DateTimeOffset ValidUntil);
    private Snapshot? snapshot;
    private const int MaximumBytes = 4 * 1024 * 1024;
    public bool Ready => Volatile.Read(ref snapshot) is { } value && value.ValidUntil > clock.GetUtcNow();

    public void RequireAllowed(IEnumerable<X509Certificate2> certificates)
    {
        var value = Volatile.Read(ref snapshot);
        if (value is null || value.ValidUntil <= clock.GetUtcNow())
            throw new SuiteException(503, "STATION_ATTESTATION_STATUS_UNAVAILABLE",
                "Station application verification is temporarily unavailable.");
        foreach (var certificate in certificates)
        {
            var serial = certificate.SerialNumber.TrimStart('0').ToLowerInvariant();
            if (value.Revoked.Contains(serial)) throw new SuiteException(403,
                "STATION_APP_ATTESTATION_REVOKED", "Station application verification was revoked.");
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(60);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                // Fixed Google endpoint; no certificate AIA, user URL or redirect.
                using var response = await client.GetAsync("https://android.googleapis.com/attestation/status",
                    HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                response.EnsureSuccessStatusCode();
                if (response.Headers.Location is not null || response.Content.Headers.ContentLength > MaximumBytes)
                    throw new InvalidOperationException("Invalid official attestation status.");
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var memory = new MemoryStream(); var buffer = new byte[16384]; int count;
                while ((count = await stream.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    if (memory.Length + count > MaximumBytes) throw new InvalidOperationException("Status is too large.");
                    memory.Write(buffer, 0, count);
                }
                using var document = JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 5 });
                StationProtocol.RejectDuplicates(document.RootElement);
                var entries = document.RootElement.GetProperty("entries");
                if (entries.ValueKind != JsonValueKind.Object) throw new JsonException();
                var revoked = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries.EnumerateObject())
                {
                    if (entry.Name.Length is < 1 or > 128 || entry.Name.Any(c => !char.IsAsciiHexDigit(c)) ||
                        entry.Value.GetProperty("status").GetString() is not ("REVOKED" or "SUSPENDED"))
                        throw new JsonException();
                    revoked.Add(entry.Name.TrimStart('0').ToLowerInvariant());
                }
                var age = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromHours(1);
                if (response.Headers.Age is { } elapsed) age -= elapsed;
                age = age > TimeSpan.FromHours(24) ? TimeSpan.FromHours(24) : age;
                if (age <= TimeSpan.Zero) throw new InvalidOperationException("Official status is stale.");
                Volatile.Write(ref snapshot, new Snapshot(revoked, clock.GetUtcNow() + age));
                delay = TimeSpan.FromSeconds(Math.Max(10, age.TotalSeconds / 2));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or
                KeyNotFoundException or InvalidOperationException or SuiteException or IOException)
            { /* New verified enrollment fails closed; legacy/proof-only clients remain available. */ }
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
