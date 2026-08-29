using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurboRamaSuiteContentPublisher;

internal sealed partial class JournalStore : IDisposable
{
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly VerificationJournal journal;

    private JournalStore(string path, VerificationJournal journal)
    {
        this.path = path;
        this.journal = journal;
    }

    public static async Task<JournalStore> OpenAsync(
        string path,
        string inventorySha256,
        string visualCatalogSha256,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new PublisherFailure("JOURNAL_DIRECTORY_INVALID");
        if (OperatingSystem.IsLinux())
        {
            var info = new DirectoryInfo(directory);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new PublisherFailure("JOURNAL_DIRECTORY_INVALID");
            var mode = File.GetUnixFileMode(directory);
            var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((mode & forbidden) != 0) throw new PublisherFailure("JOURNAL_DIRECTORY_INVALID");
        }
        VerificationJournal state;
        if (File.Exists(fullPath))
        {
            ProtectedFile.Require(fullPath, "JOURNAL_FILE_INVALID");
            try
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                state = JsonSerializer.Deserialize<VerificationJournal>(bytes, JsonOptions())
                    ?? throw new PublisherFailure("JOURNAL_FILE_INVALID");
            }
            catch (PublisherFailure) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { throw new PublisherFailure("JOURNAL_FILE_INVALID"); }
            if (state.SchemaVersion != 2 ||
                !CatalogLoader.FixedAscii(state.InventorySha256, inventorySha256) ||
                !CatalogLoader.FixedAscii(state.VisualCatalogSha256, visualCatalogSha256))
                throw new PublisherFailure("JOURNAL_CATALOG_MISMATCH");
        }
        else
        {
            state = new VerificationJournal
            {
                InventorySha256 = inventorySha256,
                VisualCatalogSha256 = visualCatalogSha256
            };
        }
        return new JournalStore(fullPath, state);
    }

    public bool TryGet(PreparedItem item, OriginMetadata probe, out VerifiedItem verified)
    {
        if (journal.Items.TryGetValue(item.ItemId, out var candidate) && Valid(candidate, item, probe))
        {
            verified = candidate;
            return true;
        }
        verified = null!;
        return false;
    }

    public async Task RecordAsync(VerifiedItem item, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            journal.Items[item.ItemId] = item;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions());
            var asText = System.Text.Encoding.UTF8.GetString(bytes);
            if (UrlPropertyRegex().IsMatch(asText))
                throw new PublisherFailure("JOURNAL_URL_GUARD_FAILED");
            var temporary = path + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            ProtectedFile.Restrict(temporary);
            File.Move(temporary, path, overwrite: true);
            ProtectedFile.Restrict(path);
        }
        catch (PublisherFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new PublisherFailure("JOURNAL_WRITE_FAILED"); }
        finally { gate.Release(); }
    }

    private static bool Valid(VerifiedItem value, PreparedItem item, OriginMetadata probe)
        => value.ItemId == item.ItemId && value.DisplayOrder == item.DisplayOrder &&
           ContentArtifactLimits.IsSupportedLength(value.ContentLength) && ShaRegex().IsMatch(value.Sha256) &&
           value.ContentLength == probe.ContentLength && OriginIdentityMatches(value, probe) &&
           value.FileExtension == item.DeclaredExtension && value.ExtractPolicy == item.ExtractPolicy &&
           value.SafeFileName == CatalogLoader.SafeFileName(item) &&
           value.ContentType.Length is > 0 and <= 128 &&
           value.SourceEtag?.Length is null or <= 512 && value.SourceLastModified?.Length is null or <= 128;

    internal static bool OriginIdentityMatches(VerifiedItem value, OriginMetadata probe)
    {
        if (probe.Etag is not { Length: >= 2 } etag || etag[0] != '"' || etag[^1] != '"' ||
            etag.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ||
            value.SourceEtag is not { Length: >= 2 } stored || stored[0] != '"' ||
            stored[^1] != '"' || stored.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            return false;
        return string.Equals(stored, etag, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
        MaxDepth = 8
    };

    public void Dispose() => gate.Dispose();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaRegex();
    [GeneratedRegex("\\\"[^\\\"]*url[^\\\"]*\\\"\\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPropertyRegex();
}
