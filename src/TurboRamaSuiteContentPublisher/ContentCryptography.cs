using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteContentPublisher;

internal sealed class ContentKeyRing : IDisposable
{
    private static readonly byte[] KeySetDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentKeySetFingerprint/v1\0");
    private static readonly byte[] ProofDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentDeploymentProof/v1\0");
    private readonly Dictionary<int, byte[]> keys;
    private readonly byte[] keySetFingerprint;
    private bool disposed;

    private ContentKeyRing(int activeVersion, Dictionary<int, byte[]> keySet)
    {
        ActiveVersion = activeVersion;
        keys = keySet;
        keySetFingerprint = ComputeKeySetFingerprint(keys);
        VerifyCanary(keys[ActiveVersion], ActiveVersion);
    }

    public int ActiveVersion { get; }
    public string KeySetFingerprint => Convert.ToHexString(keySetFingerprint).ToLowerInvariant();

    public byte[] CreateGatewayProof(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> allowlistFingerprint)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (nonce.Length != 32 || allowlistFingerprint.Length != 32)
            throw new PublisherFailure("KEY_RING_PROOF_INVALID");
        var message = ProofMessage(ActiveVersion, keySetFingerprint,
            allowlistFingerprint, nonce);
        try { return HMACSHA256.HashData(keys[ActiveVersion], message); }
        finally { CryptographicOperations.ZeroMemory(message); }
    }

    public static async Task<ContentKeyRing> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ProtectedFile.Require(path, "KEY_RING_FILE_INVALID");
        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(path, cancellationToken); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new PublisherFailure("KEY_RING_FILE_INVALID"); }
        try
        {
            var document = JsonSerializer.Deserialize<KeyRingDocument>(bytes, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
                MaxDepth = 8
            }) ?? throw new PublisherFailure("KEY_RING_INVALID");
            if (document.SchemaVersion != 1 || document.ActiveKeyVersion <= 0 || document.Keys is null ||
                document.Keys.Count == 0 || document.Keys.Any(key => key.Version <= 0) ||
                document.Keys.Select(key => key.Version).Distinct().Count() != document.Keys.Count)
                throw new PublisherFailure("KEY_RING_INVALID");
            var keys = new Dictionary<int, byte[]>();
            try
            {
                foreach (var item in document.Keys)
                    keys.Add(item.Version, CanonicalKey(item.Key));
                if (!keys.ContainsKey(document.ActiveKeyVersion))
                    throw new PublisherFailure("KEY_RING_INVALID");
                var result = new ContentKeyRing(document.ActiveKeyVersion, keys);
                keys = [];
                return result;
            }
            catch
            {
                foreach (var key in keys.Values)
                    CryptographicOperations.ZeroMemory(key);
                throw;
            }
        }
        catch (JsonException) { throw new PublisherFailure("KEY_RING_INVALID"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static ContentKeyRing ForSelfTest(byte[] key) => new(1,
        new Dictionary<int, byte[]> { [1] = key.ToArray() });

    public EncryptedLocator Encrypt(Uri uri, string catalogIdentity, string itemId, string artifactId,
        int artifactVersion, string manifestIdentity)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var plaintext = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
        if (plaintext.Length is < 1 or > OriginPolicy.MaximumUrlUtf8Bytes)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new PublisherFailure("UPSTREAM_URL_POLICY_DENIED");
        }
        var aad = CanonicalIdentity.UrlAad(catalogIdentity, itemId, artifactId, artifactVersion,
            manifestIdentity, ActiveVersion);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(keys[ActiveVersion], 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
            return new EncryptedLocator(ciphertext, nonce, tag, ActiveVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static byte[] CanonicalKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new PublisherFailure("KEY_RING_INVALID");
        byte[] key;
        try { key = Convert.FromBase64String(text); }
        catch (FormatException) { throw new PublisherFailure("KEY_RING_INVALID"); }
        var canonical = Convert.ToBase64String(key);
        if (key.Length != 32 || !CatalogLoader.FixedAscii(canonical, text))
        {
            CryptographicOperations.ZeroMemory(key);
            throw new PublisherFailure("KEY_RING_INVALID");
        }
        return key;
    }

    private static void VerifyCanary(byte[] key, int keyVersion)
    {
        var plaintext = Encoding.ASCII.GetBytes("turborama-suite-content-keyring-canary");
        var decrypted = new byte[plaintext.Length];
        var ciphertext = new byte[plaintext.Length];
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var associatedData = Encoding.ASCII.GetBytes(
            "TurboRamaSuiteContentKeyRingCanary/v1\0" + keyVersion);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            aes.Decrypt(nonce, ciphertext, tag, decrypted, associatedData);
            if (!CryptographicOperations.FixedTimeEquals(plaintext, decrypted))
                throw new PublisherFailure("KEY_RING_CANARY_FAILED");
        }
        catch (CryptographicException)
        {
            throw new PublisherFailure("KEY_RING_CANARY_FAILED");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(decrypted);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    private static byte[] ComputeKeySetFingerprint(
        IReadOnlyDictionary<int, byte[]> keySet)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(KeySetDomain);
        Span<byte> number = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(number, keySet.Count);
        hash.AppendData(number);
        foreach (var entry in keySet.OrderBy(entry => entry.Key))
        {
            BinaryPrimitives.WriteInt32BigEndian(number, entry.Key);
            hash.AppendData(number);
            hash.AppendData(entry.Value);
        }
        return hash.GetHashAndReset();
    }

    private static byte[] ProofMessage(
        int keyVersion,
        ReadOnlySpan<byte> keyFingerprint,
        ReadOnlySpan<byte> allowlistFingerprint,
        ReadOnlySpan<byte> nonce)
    {
        var message = new byte[ProofDomain.Length + sizeof(int) + 96];
        ProofDomain.CopyTo(message, 0);
        var offset = ProofDomain.Length;
        BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(offset, sizeof(int)),
            keyVersion);
        offset += sizeof(int);
        keyFingerprint.CopyTo(message.AsSpan(offset, 32));
        offset += 32;
        allowlistFingerprint.CopyTo(message.AsSpan(offset, 32));
        offset += 32;
        nonce.CopyTo(message.AsSpan(offset, 32));
        return message;
    }

    public void Dispose()
    {
        if (disposed) return;
        foreach (var key in keys.Values) CryptographicOperations.ZeroMemory(key);
        keys.Clear();
        CryptographicOperations.ZeroMemory(keySetFingerprint);
        disposed = true;
    }
}

internal sealed record EncryptedLocator(byte[] Ciphertext, byte[] Nonce, byte[] Tag, int KeyVersion)
{
    public void Clear()
    {
        CryptographicOperations.ZeroMemory(Ciphertext);
        CryptographicOperations.ZeroMemory(Nonce);
        CryptographicOperations.ZeroMemory(Tag);
    }
}

internal static class CanonicalIdentity
{
    private static readonly byte[] UrlDomain = Encoding.ASCII.GetBytes("TurboRamaSuiteContentUrl/v1\0");
    private static readonly byte[] CatalogDomain = Encoding.ASCII.GetBytes("TurboRamaSuiteContentCatalog/v1\0");

    public static byte[] UrlAad(string catalogIdentity, string itemId, string artifactId,
        int artifactVersion, string manifestIdentity, int keyVersion)
    {
        using var stream = new MemoryStream();
        stream.Write(UrlDomain);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("catalogIdentity", catalogIdentity);
            writer.WriteString("itemId", itemId);
            writer.WriteString("artifactId", artifactId);
            writer.WriteNumber("artifactVersion", artifactVersion);
            writer.WriteString("manifestIdentity", manifestIdentity);
            writer.WriteNumber("keyVersion", keyVersion);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static string DescriptorHash(VerifiedItem item, string catalogIdentity)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("productId", "TURBORAMA_SUITE");
            writer.WriteString("itemId", item.ItemId);
            writer.WriteString("artifactId", item.ItemId);
            writer.WriteNumber("artifactVersion", 1);
            writer.WriteNumber("contentLength", item.ContentLength);
            writer.WriteString("sha256", item.Sha256);
            writer.WriteString("safeFileName", item.SafeFileName);
            writer.WriteString("fileExtension", item.FileExtension);
            writer.WriteString("extractPolicy", item.ExtractPolicy);
            writer.WriteString("manifestIdentity", catalogIdentity);
            writer.WriteEndObject();
        }
        return CatalogLoader.Hex(SHA256.HashData(stream.ToArray()));
    }

    public static string CatalogIdentity(
        PreparedCatalog catalog,
        IReadOnlyList<VerifiedItem> readyItems,
        IReadOnlyList<MaintenanceItem> maintenanceItems,
        int originActiveKeyVersion,
        string originKeySetFingerprint,
        string originAllowlistFingerprint)
    {
        if (originActiveKeyVersion < 1 ||
            !IsLowerSha256(originKeySetFingerprint) ||
            !IsLowerSha256(originAllowlistFingerprint))
            throw new PublisherFailure("CATALOG_DEPLOYMENT_PROVENANCE_INVALID");
        var ready = readyItems.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var maintenance = maintenanceItems.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        using var stream = new MemoryStream();
        stream.Write(CatalogDomain);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("productId", "TURBORAMA_SUITE");
            writer.WriteString("inventorySha256", catalog.InventorySha256);
            writer.WriteString("visualCatalogSha256", catalog.VisualCatalogSha256);
            writer.WriteNumber("originActiveKeyVersion", originActiveKeyVersion);
            writer.WriteString("originKeySetFingerprint", originKeySetFingerprint);
            writer.WriteString("originAllowlistFingerprint", originAllowlistFingerprint);
            writer.WriteStartArray("items");
            foreach (var source in catalog.Items.OrderBy(item => item.DisplayOrder)
                         .ThenBy(item => item.ItemId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("itemId", source.ItemId);
                if (ready.TryGetValue(source.ItemId, out var item))
                {
                    writer.WriteString("status", "READY");
                    writer.WriteNumber("contentLength", item.ContentLength);
                    writer.WriteString("sha256", item.Sha256);
                    writer.WriteString("fileExtension", item.FileExtension);
                    writer.WriteString("extractPolicy", item.ExtractPolicy);
                    writer.WriteNull("maintenanceReason");
                }
                else if (maintenance.TryGetValue(source.ItemId, out var unavailable))
                {
                    writer.WriteString("status", "MAINTENANCE");
                    writer.WriteNull("contentLength");
                    writer.WriteNull("sha256");
                    writer.WriteNull("fileExtension");
                    writer.WriteNull("extractPolicy");
                    writer.WriteString("maintenanceReason", unavailable.ReasonCode);
                }
                else throw new PublisherFailure("CATALOG_IDENTITY_ITEM_MISSING");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return CatalogLoader.Hex(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsLowerSha256(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

internal static class ProtectedFile
{
    public static void Require(string path, string code, bool requirePrivateMode = true)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > 64 * 1024 * 1024)
                throw new PublisherFailure(code);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new PublisherFailure(code);
            if (!OperatingSystem.IsLinux() || !requirePrivateMode) return;
            var mode = File.GetUnixFileMode(path);
            var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((mode & forbidden) != 0) throw new PublisherFailure(code);
        }
        catch (PublisherFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new PublisherFailure(code); }
    }

    public static void Restrict(string path)
    {
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

internal static class ConnectionSecret
{
    public static async Task<string> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ProtectedFile.Require(path, "CONNECTION_FILE_INVALID");
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 16 * 1024) throw new PublisherFailure("CONNECTION_FILE_INVALID");
            var connection = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
            if (connection.Length is < 1 or > 16_000 || connection.Any(character => character is '\r' or '\n' or '\0'))
                throw new PublisherFailure("CONNECTION_FILE_INVALID");
            RequirePublisherRole(connection);
            return connection;
        }
        catch (PublisherFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { throw new PublisherFailure("CONNECTION_FILE_INVALID"); }
    }

    internal static void RequirePublisherRole(string connection)
    {
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (!string.Equals(parsed.Username, "turborama-suite-publisher",
                StringComparison.Ordinal))
            throw new PublisherFailure("CONNECTION_ROLE_INVALID");
    }
}
