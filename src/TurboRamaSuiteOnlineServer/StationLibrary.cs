using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteOnlineServer;

public sealed record StationCatalogEntry(
    string ItemId, string Name, string Platform, long Revision, string CoverId,
    StationItemMetadata? Metadata = null)
{
    public IReadOnlyList<string> FolderPath { get; init; } = Array.Empty<string>();

    // SHA of the exact launch payload or declared multi-file content set, supplied offline.
    // It is independent of an archive/container descriptor and never computed on a request.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentSha256 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentIdentityScheme { get; init; }

    public object PublicValue(bool includeMetadata)
    {
        var result = new Dictionary<string, object?>
        {
            ["itemId"] = ItemId, ["name"] = Name, ["platform"] = Platform,
            ["revision"] = Revision, ["coverId"] = CoverId
        };
        if (includeMetadata)
            result["metadata"] = Metadata ?? new StationItemMetadata("", "", "", "", "", "");
        result["folderPath"] = FolderPath;
        // Preserve the old wire shape for unqualified items; do not invent a hash.
        if (ContentSha256 is not null)
            result["contentSha256"] = ContentSha256;
        if (ContentIdentityScheme is not null)
            result["contentIdentityScheme"] = ContentIdentityScheme;
        return result;
    }
}

public sealed record StationItemMetadata(string Description, string Developer,
    string Publisher, string Genre, string Players, string ReleaseDate);

public sealed record StationCoverBlob(byte[] Bytes, string ContentType);

public sealed record StationArtifactDescriptor(string FileName, long SizeBytes,
    string Sha256, string Format, string LaunchPath, long ExpandedSizeBytes,
    int FileCount);

public sealed record StationResolvedArtifact(string FilePath, StationCatalogEntry Entry,
    StationArtifactDescriptor Descriptor, long LastWriteUtcTicks)
{
    public bool MatchesFile()
    {
        var info = new FileInfo(FilePath);
        return info.Exists && info.Length == Descriptor.SizeBytes &&
            info.LastWriteTimeUtc.Ticks == LastWriteUtcTicks;
    }
}

public sealed class StationLibrary
{
    public const int MaximumItems = 4096;
    public const int MaximumCoverBytes = 5 * 1024 * 1024;
    public const long MaximumArtifactBytes = 1L << 40;
    public const long MaximumExpandedBytes = 4L << 40;
    public const int MaximumArtifactFiles = 100_000;
    public long Revision { get; }
    public IReadOnlyList<StationCatalogEntry> Catalog { get; }

    private readonly Dictionary<string, Resolved> _items;
    private readonly Dictionary<string, (string Path, long Revision)> _covers;

    private StationLibrary(long revision, Dictionary<string, Resolved> items,
        Dictionary<string, (string Path, long Revision)> covers)
    {
        Revision = revision;
        _items = items;
        _covers = covers;
        Catalog = items.Values.Where(item => item.CatalogVisible)
            .Select(item => item.Entry).ToArray();
    }

    public static StationLibrary? TryLoad(string? path, StationLibrary? previous = null,
        bool verifyContent = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Station library index is invalid.");
        var revision = root.TryGetProperty("revision", out var revisionElement) &&
            revisionElement.TryGetInt64(out var parsedRevision) && parsedRevision > 0
            ? parsedRevision : 1;
        if (!root.TryGetProperty("items", out var itemsElement) ||
            itemsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Station library index is invalid.");
        if (itemsElement.GetArrayLength() > MaximumItems)
            throw new InvalidOperationException("Station library index is invalid.");
        var items = new Dictionary<string, Resolved>(StringComparer.Ordinal);
        var covers = new Dictionary<string, (string Path, long Revision)>(StringComparer.Ordinal);
        foreach (var row in itemsElement.EnumerateArray())
        {
            var itemId = RequireId(row, "itemId");
            var coverId = RequireId(row, "coverId");
            var name = RequireName(row, "name");
            var platform = RequireName(row, "platform");
            var itemRevision = row.TryGetProperty("revision", out var itemRev) &&
                itemRev.TryGetInt64(out var parsedItemRev) && parsedItemRev > 0
                ? parsedItemRev : revision;
            var contentSha256 = ReadContentSha256(row);
            var contentIdentityScheme = ReadContentIdentityScheme(row,contentSha256);
            var filePath = RequirePath(row, "filePath");
            var coverPath = RequirePath(row, "coverPath");
            var catalogVisible = true;
            if (row.TryGetProperty("catalogVisible", out var visibility))
            {
                if (visibility.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new InvalidOperationException("Station catalog visibility is invalid.");
                catalogVisible = visibility.GetBoolean();
            }
            StationArtifactDescriptor? artifact = null;
            long lastWriteUtcTicks = 0;
            if (row.TryGetProperty("artifact", out var artifactElement))
            {
                artifact = ReadArtifact(artifactElement, filePath);
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length != artifact.SizeBytes)
                    throw new InvalidOperationException("Station artifact index is stale.");
                lastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                var reused = previous is not null && previous._items.TryGetValue(itemId, out var old) &&
                    old.FilePath == filePath && old.Artifact == artifact &&
                    old.LastWriteUtcTicks == lastWriteUtcTicks;
                // The root-owned automatic importer already verifies and hashes
                // content before publishing its index. Its API can skip a second
                // full body pass at startup/reload; schema, file stat and grant
                // bindings are still checked. Other callers keep strict defaults.
                if (!reused && verifyContent)
                {
                using var source = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
                if (!MatchesFormat(source, artifact.Format))
                    throw new InvalidOperationException("Station artifact format is invalid.");
                if (artifact.Format == "zip") ValidateZip(source, artifact);
                source.Position = 0;
                var digest = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
                info.Refresh();
                if (digest != artifact.Sha256 || info.Length != artifact.SizeBytes ||
                    info.LastWriteTimeUtc.Ticks != lastWriteUtcTicks)
                    throw new InvalidOperationException("Station artifact index is stale.");
                }
            }
            if (contentIdentityScheme is not null && (artifact is null || artifact.Format != "zip" ||
                (contentIdentityScheme == "cue-set-v1" && !artifact.LaunchPath.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)) ||
                (contentIdentityScheme == "wiiu-set-v1" && (platform != "wiiu" || !artifact.LaunchPath.EndsWith(".rpx", StringComparison.OrdinalIgnoreCase)))))
                throw new InvalidOperationException("Station content identity scheme does not match the artifact.");
            if (!items.TryAdd(itemId, new Resolved(
                    new StationCatalogEntry(itemId, name, platform, itemRevision, coverId, ReadMetadata(row))
                    { FolderPath = ReadFolderPath(row), ContentSha256 = contentSha256, ContentIdentityScheme = contentIdentityScheme },
                    filePath, artifact, lastWriteUtcTicks, catalogVisible)))
                throw new InvalidOperationException("Station library index is invalid.");
            if (covers.TryGetValue(coverId, out var existing) &&
                (existing.Path != coverPath || existing.Revision != itemRevision))
                throw new InvalidOperationException("Station cover ID has conflicting revisions.");
            covers[coverId] = (coverPath, itemRevision);
        }
        return new StationLibrary(revision, items, covers);
    }

    public bool TryResolve(string itemId, out string filePath, out StationCatalogEntry entry)
    {
        if (_items.TryGetValue(itemId, out var resolved) && File.Exists(resolved.FilePath))
        {
            filePath = resolved.FilePath;
            entry = resolved.Entry;
            return true;
        }
        filePath = "";
        entry = null!;
        return false;
    }

    public bool TryResolveArtifact(string itemId, out StationResolvedArtifact artifact)
    {
        if (_items.TryGetValue(itemId, out var resolved) && resolved.Artifact is not null)
        {
            artifact = new StationResolvedArtifact(resolved.FilePath, resolved.Entry,
                resolved.Artifact, resolved.LastWriteUtcTicks);
            return artifact.MatchesFile();
        }
        artifact = null!;
        return false;
    }

    public StationCoverBlob? ReadCover(string coverId)
    {
        if (!_covers.TryGetValue(coverId, out var cover) || !File.Exists(cover.Path))
            return null;
        var info = new FileInfo(cover.Path);
        if (!info.Exists || info.Length is < 1 or > MaximumCoverBytes)
            return null;
        var bytes = File.ReadAllBytes(cover.Path);
        if (bytes.Length != info.Length) return null;
        var contentType = ContentType(cover.Path);
        if (!MatchesCover(bytes, contentType)) return null;
        return new StationCoverBlob(bytes, contentType);
    }

    public int ItemCount => Catalog.Count;
    public StationCatalogEntry? FindCatalogItem(string id) =>
        _items.TryGetValue(id, out var item) && item.CatalogVisible ? item.Entry : null;
    public int CompatibilityItemCount => _items.Count - Catalog.Count;
    public bool ContainsItem(string itemId) => _items.ContainsKey(itemId);

    private static string? ReadContentSha256(JsonElement row)
    {
        if (!row.TryGetProperty("contentSha256", out var value)) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Station content identity is invalid.");
        var text = value.GetString();
        if (text is not { Length: 64 } ||
            text.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidOperationException("Station content identity is invalid.");
        return text;
    }
    private static string? ReadContentIdentityScheme(JsonElement row,string? contentHash)
    {
        if (!row.TryGetProperty("contentIdentityScheme", out var value)) return null;
        if (contentHash is null || value.ValueKind != JsonValueKind.String ||
            value.GetString() is not ("cue-set-v1" or "wiiu-set-v1"))
            throw new InvalidOperationException("Station content identity scheme is invalid.");
        return value.GetString();
    }

    private static StationItemMetadata? ReadMetadata(JsonElement row)
    {
        if (!row.TryGetProperty("metadata", out var value)) return null;
        if (value.ValueKind != JsonValueKind.Object ||
            System.Text.Encoding.UTF8.GetByteCount(value.GetRawText()) > 8192)
            throw new InvalidOperationException("Station metadata is invalid.");
        string Read(string key, int limit)
        {
            if (!value.TryGetProperty(key, out var field)) return "";
            if (field.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Station metadata is invalid.");
            var text = field.GetString() ?? "";
            if (text.Length > limit || text.Any(c => char.IsControl(c) && c != '\n' && c != '\t'))
                throw new InvalidOperationException("Station metadata is invalid.");
            return text;
        }
        var metadata = new StationItemMetadata(Read("description", 2000), Read("developer", 80),
            Read("publisher", 80), Read("genre", 80), Read("players", 40), Read("releaseDate", 40));
        if (JsonSerializer.SerializeToUtf8Bytes(metadata, StrictJson.Options).Length > 8192)
            throw new InvalidOperationException("Station metadata is invalid.");
        return metadata;
    }

    // Signed presentation metadata only; never a download or filesystem destination.
    private static IReadOnlyList<string> ReadFolderPath(JsonElement row)
    {
        if (!row.TryGetProperty("folderPath", out var value)) return Array.Empty<string>();
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 8)
            throw new InvalidOperationException("Station folder path is invalid.");
        var result = new List<string>();
        foreach (var segment in value.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Station folder name is invalid.");
            var name = segment.GetString()!;
            if (name.Length is 0 or > 80 || name.Trim().Length == 0 || name is "." or ".." ||
                name.Contains('/') || name.Contains('\\') || name.Any(char.IsControl))
                throw new InvalidOperationException("Station folder name is invalid.");
            for (var i = 0; i < name.Length; i++)
                if (char.IsSurrogate(name[i]) && (i + 1 >= name.Length ||
                    !char.IsSurrogatePair(name[i], name[++i])))
                    throw new InvalidOperationException("Station folder name is invalid.");
            result.Add(name);
        }
        return result.AsReadOnly();
    }

    private static string RequireId(JsonElement row, string name)
    {
        var value = RequireName(row, name);
        if (!StationProtocol.IsSafeLibraryId(value))
            throw new InvalidOperationException("Station library index is invalid.");
        return value;
    }

    private static string RequireName(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Station library index is invalid.");
        var value = field.GetString() ?? "";
        if (value.Length is 0 or > 120 || value.Any(char.IsControl))
            throw new InvalidOperationException("Station library index is invalid.");
        return value;
    }

    private static string RequirePath(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Station library index is invalid.");
        var value = field.GetString() ?? "";
        if (value.Length is 0 or > 1024 || !Path.IsPathRooted(value) ||
            value.Contains('\0') || value.Contains('\n') ||
            value.Split('/', '\\').Any(part => part == ".."))
            throw new InvalidOperationException("Station library index is invalid.");
        return value;
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };

    private static bool MatchesCover(ReadOnlySpan<byte> bytes, string contentType) =>
        contentType switch
        {
            "image/png" => bytes.Length >= 8 &&
                bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 255 &&
                bytes[1] == 216 && bytes[2] == 255,
            "image/gif" => bytes.Length >= 6 &&
                (bytes[..6].SequenceEqual("GIF87a"u8) ||
                 bytes[..6].SequenceEqual("GIF89a"u8)),
            "image/webp" => bytes.Length >= 12 &&
                bytes[..4].SequenceEqual("RIFF"u8) &&
                bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

    private static StationArtifactDescriptor ReadArtifact(JsonElement row, string filePath)
    {
        if (row.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Station artifact index is invalid.");
        var fileName = RequireName(row, "fileName", 255);
        var sha256 = RequireName(row, "sha256");
        var format = RequireName(row, "format");
        var launchPath = RequireName(row, "launchPath", 512);
        var size = RequirePositiveLong(row, "sizeBytes");
        var expanded = RequirePositiveLong(row, "expandedSizeBytes");
        var count = RequirePositiveLong(row, "fileCount");
        if (fileName != Path.GetFileName(filePath) || fileName is "." or ".." ||
            fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(':') ||
            sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c) || char.IsUpper(c)) ||
            format is not ("raw" or "zip" or "rar" or "7z") ||
            (format == "zip" && !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ||
            (format == "rar" && !fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)) ||
            (format == "7z" && !fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) ||
            (format == "raw" && Path.GetExtension(fileName).ToLowerInvariant() is
                (".zip" or ".rar" or ".7z")) ||
            launchPath.StartsWith('/') || launchPath.Contains('\\') ||
            launchPath.Contains(':') || launchPath.Split('/').Any(part => part is "" or "." or "..") ||
            size > MaximumArtifactBytes || expanded > MaximumExpandedBytes ||
            count > MaximumArtifactFiles ||
            (format == "raw" && (launchPath != fileName || expanded != size || count != 1)))
            throw new InvalidOperationException("Station artifact index is invalid.");
        return new StationArtifactDescriptor(fileName, size, sha256, format,
            launchPath, expanded, checked((int)count));
    }

    private static long RequirePositiveLong(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var field) || !field.TryGetInt64(out var value) ||
            value <= 0)
            throw new InvalidOperationException("Station artifact index is invalid.");
        return value;
    }

    private static string RequireName(JsonElement row, string name, int maximum)
    {
        if (!row.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Station artifact index is invalid.");
        var value = field.GetString() ?? "";
        if (value.Length is 0 || value.Length > maximum || value.Any(char.IsControl))
            throw new InvalidOperationException("Station artifact index is invalid.");
        return value;
    }

    private static bool MatchesFormat(Stream source, string format)
    {
        Span<byte> header = stackalloc byte[8];
        var length = source.Read(header);
        source.Position = 0;
        var zip = length >= 4 && header[..4].SequenceEqual("PK\x03\x04"u8);
        var rar = length >= 7 && header[..6].SequenceEqual("Rar!\x1a\x07"u8) &&
            (header[6] == 0 || (length >= 8 && header[6] == 1 && header[7] == 0));
        var sevenZip = length >= 6 && header[..6].SequenceEqual(new byte[]
            { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c });
        return format switch
        {
            "zip" => zip,
            "rar" => rar,
            "7z" => sevenZip,
            "raw" => !zip && !rar && !sevenZip,
            _ => false
        };
    }

    private static void ValidateZip(Stream source, StationArtifactDescriptor artifact)
    {
        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            var count = 0;
            long expanded = 0;
            var launchFound = false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                var directory = name.EndsWith('/');
                var segments = (directory ? name[..^1] : name).Split('/');
                if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') ||
                    segments.Any(part => part is "" or "." or "..") ||
                    (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000 ||
                    !names.Add(name))
                    throw new InvalidDataException();
                if (directory) continue;
                count++;
                expanded = checked(expanded + entry.Length);
                if (name == artifact.LaunchPath) launchFound = true;
                if (count > MaximumArtifactFiles || expanded > MaximumExpandedBytes)
                    throw new InvalidDataException();
            }
            if (!launchFound || count != artifact.FileCount ||
                expanded != artifact.ExpandedSizeBytes)
                throw new InvalidDataException();
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException)
        {
            throw new InvalidOperationException("Station ZIP metadata is invalid.", exception);
        }
    }

    private sealed record Resolved(StationCatalogEntry Entry, string FilePath,
        StationArtifactDescriptor? Artifact, long LastWriteUtcTicks, bool CatalogVisible);
}

public sealed class StationGrantCipher : IDisposable
{
    private readonly byte[] _key;
    public int KeyVersion { get; } = 1;

    private StationGrantCipher(byte[] key) => _key = key;

    public static StationGrantCipher Load(string path, params byte[][] forbidden)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 32 or > 64)
            throw new InvalidOperationException("Station download key is invalid.");
        if (OperatingSystem.IsLinux() &&
            (info.UnixFileMode & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                UnixFileMode.OtherExecute | UnixFileMode.GroupExecute)) != 0)
            throw new InvalidOperationException("Station download key permissions are too open.");
        var key = File.ReadAllBytes(path);
        try
        {
            foreach (var other in forbidden)
            {
                if (other.Length == 0) continue;
                if (CryptographicOperations.FixedTimeEquals(
                        SHA256.HashData(key), SHA256.HashData(other)))
                    throw new InvalidOperationException(
                        "Station download key must be independent of Suite and activation secrets.");
            }
            return new StationGrantCipher(key);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    public (byte[] Nonce, byte[] Ciphertext, byte[] Tag) Seal(string associated, string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ad = System.Text.Encoding.UTF8.GetBytes(associated);
        var ciphertext = new byte[plain.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(_key, 16);
            aes.Encrypt(nonce, plain, ciphertext, tag, ad);
            return (nonce, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(ad);
        }
    }

    public string Open(string associated, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        if (nonce.Length != 12 || tag.Length != 16 || ciphertext.Length is < 1 or > 2048)
            throw new CryptographicException();
        var plain = new byte[ciphertext.Length];
        var ad = System.Text.Encoding.UTF8.GetBytes(associated);
        try
        {
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plain, ad);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(ad);
        }
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}

public sealed record StationGrantRecord(string GrantId, string LicenseId, string DeviceId,
    string ItemId, int KeyVersion, byte[] Nonce, byte[] Ciphertext, byte[] Tag);
