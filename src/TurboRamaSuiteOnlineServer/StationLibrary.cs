using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteOnlineServer;

public sealed record StationCatalogEntry(
    string ItemId, string Name, string Platform, long Revision, string CoverId);

public sealed record StationCoverBlob(byte[] Bytes, string ContentType);

public sealed class StationLibrary
{
    public const int MaximumItems = 4096;
    public const int MaximumCoverBytes = 5 * 1024 * 1024;
    public long Revision { get; }
    public IReadOnlyList<StationCatalogEntry> Catalog { get; }

    private readonly Dictionary<string, Resolved> _items;
    private readonly Dictionary<string, string> _covers;

    private StationLibrary(long revision, Dictionary<string, Resolved> items,
        Dictionary<string, string> covers)
    {
        Revision = revision;
        _items = items;
        _covers = covers;
        Catalog = items.Values.Select(item => item.Entry).ToArray();
    }

    public static StationLibrary? TryLoad(string? path)
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
        var covers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in itemsElement.EnumerateArray())
        {
            var itemId = RequireId(row, "itemId");
            var coverId = RequireId(row, "coverId");
            var name = RequireName(row, "name");
            var platform = RequireName(row, "platform");
            var itemRevision = row.TryGetProperty("revision", out var itemRev) &&
                itemRev.TryGetInt64(out var parsedItemRev) && parsedItemRev > 0
                ? parsedItemRev : revision;
            var filePath = RequirePath(row, "filePath");
            var coverPath = RequirePath(row, "coverPath");
            if (!items.TryAdd(itemId, new Resolved(
                    new StationCatalogEntry(itemId, name, platform, itemRevision, coverId),
                    filePath)))
                throw new InvalidOperationException("Station library index is invalid.");
            covers[coverId] = coverPath;
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

    public StationCoverBlob? ReadCover(string coverId)
    {
        if (!_covers.TryGetValue(coverId, out var path) || !File.Exists(path))
            return null;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 1 or > MaximumCoverBytes)
            return null;
        var bytes = File.ReadAllBytes(path);
        return new StationCoverBlob(bytes, ContentType(path));
    }

    public int ItemCount => _items.Count;

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

    private sealed record Resolved(StationCatalogEntry Entry, string FilePath);
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
