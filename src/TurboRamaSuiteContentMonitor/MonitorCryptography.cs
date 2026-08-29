using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteContentMonitor;

internal sealed class MonitorKeyRing : IDisposable
{
    private static readonly byte[] CandidateDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentCandidateUrl/v1\0");
    private static readonly byte[] OriginDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentUrl/v1\0");
    private static readonly byte[] KeySetFingerprintDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentKeySetFingerprint/v1\0");
    private static readonly byte[] DeploymentProofDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentDeploymentProof/v1\0");
    private readonly Dictionary<int, byte[]> keys;
    private bool disposed;

    private MonitorKeyRing(int activeVersion, Dictionary<int, byte[]> keys) =>
        (ActiveVersion, this.keys) = (activeVersion, keys);
    public int ActiveVersion { get; }

    public static async Task<MonitorKeyRing> LoadAsync(string path, CancellationToken ct)
    {
        ProtectedMonitorFile.Require(path, 64 * 1024);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        try
        {
            var document = JsonSerializer.Deserialize<MonitorKeyRingDocument>(bytes,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                    MaxDepth = 8
                }) ?? throw new MonitorFailure("KEY_RING_INVALID");
            if (document.SchemaVersion != 1 || document.ActiveKeyVersion < 1 ||
                document.Keys is null || document.Keys.Count == 0 ||
                document.Keys.Select(item => item.Version).Distinct().Count() != document.Keys.Count)
                throw new MonitorFailure("KEY_RING_INVALID");
            var keys = new Dictionary<int, byte[]>();
            foreach (var item in document.Keys)
            {
                byte[] key;
                try { key = Convert.FromBase64String(item.Key ?? string.Empty); }
                catch (FormatException) { throw new MonitorFailure("KEY_RING_INVALID"); }
                if (item.Version < 1 || key.Length != 32 ||
                    !string.Equals(Convert.ToBase64String(key), item.Key, StringComparison.Ordinal))
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new MonitorFailure("KEY_RING_INVALID");
                }
                keys.Add(item.Version, key);
            }
            if (!keys.ContainsKey(document.ActiveKeyVersion)) throw new MonitorFailure("KEY_RING_INVALID");
            return new MonitorKeyRing(document.ActiveKeyVersion, keys);
        }
        catch (JsonException) { throw new MonitorFailure("KEY_RING_INVALID"); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static MonitorKeyRing ForTest(byte[] key) =>
        new(1, new Dictionary<int, byte[]> { [1] = key.ToArray() });

    internal static MonitorKeyRing ForTest(int activeVersion,
        IReadOnlyDictionary<int, byte[]> keySet) => new(activeVersion,
            keySet.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));

    internal byte[] CopyDeploymentKeySetFingerprint()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var stream = new MemoryStream();
        stream.Write(KeySetFingerprintDomain);
        WriteInt32(stream, keys.Count);
        foreach (var pair in keys.OrderBy(pair => pair.Key))
        {
            WriteInt32(stream, pair.Key);
            stream.Write(pair.Value);
        }
        var material = stream.ToArray();
        try { return SHA256.HashData(material); }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    internal bool SharesAnyKeyMaterialWith(MonitorKeyRing other)
    {
        ArgumentNullException.ThrowIfNull(other);
        ObjectDisposedException.ThrowIf(disposed, this);
        ObjectDisposedException.ThrowIf(other.disposed, other);
        var shared = false;
        foreach (var candidate in keys.Values)
        {
            foreach (var origin in other.keys.Values)
                shared |= CryptographicOperations.FixedTimeEquals(candidate, origin);
        }
        return shared;
    }

    internal byte[] CreateDeploymentProof(ReadOnlySpan<byte> keySetFingerprint,
        ReadOnlySpan<byte> allowlistFingerprint, ReadOnlySpan<byte> nonce)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (keySetFingerprint.Length != 32 || allowlistFingerprint.Length != 32 ||
            nonce.Length != 32)
            throw new MonitorFailure("GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED");
        using var stream = new MemoryStream();
        stream.Write(DeploymentProofDomain);
        WriteInt32(stream, ActiveVersion);
        stream.Write(keySetFingerprint);
        stream.Write(allowlistFingerprint);
        stream.Write(nonce);
        var material = stream.ToArray();
        try { return HMACSHA256.HashData(keys[ActiveVersion], material); }
        finally { CryptographicOperations.ZeroMemory(material); }
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> encoded = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(encoded, value);
        stream.Write(encoded);
        CryptographicOperations.ZeroMemory(encoded);
    }

    public DecryptedUrl DecryptCandidate(CandidateLease candidate)
    {
        var aad = CandidateAad(candidate.CandidateId, candidate.ItemId,
            candidate.BaseCatalogIdentity, candidate.KeyVersion);
        return Decrypt(candidate.Ciphertext, candidate.Nonce, candidate.Tag,
            candidate.KeyVersion, aad);
    }

    public DecryptedUrl DecryptOrigin(HealthTarget target)
    {
        var aad = OriginAad(target.CatalogIdentity, target.ItemId, target.ItemId,
            target.ArtifactVersion,
            target.CatalogIdentity, target.KeyVersion);
        return Decrypt(target.Ciphertext, target.Nonce, target.Tag, target.KeyVersion, aad);
    }

    public DecryptedUrl DecryptOrigin(string catalogIdentity, SnapshotItem item)
    {
        if (item.ArtifactVersion is null || item.OriginCiphertext is null ||
            item.OriginNonce is null || item.OriginTag is null || item.OriginKeyVersion is null)
            throw new MonitorFailure("ORIGIN_MISSING");
        var aad = OriginAad(catalogIdentity, item.ItemId, item.ItemId,
            item.ArtifactVersion.Value, catalogIdentity, item.OriginKeyVersion.Value);
        return Decrypt(item.OriginCiphertext, item.OriginNonce, item.OriginTag,
            item.OriginKeyVersion.Value, aad);
    }

    public EncryptedOrigin EncryptOrigin(Uri uri, string catalogIdentity, string itemId,
        int artifactVersion)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var plaintext = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
        if (plaintext.Length is < 1 or > 4096)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new MonitorFailure("URL_ENCRYPTION_INVALID");
        }
        var aad = OriginAad(catalogIdentity, itemId, itemId, artifactVersion,
            catalogIdentity, ActiveVersion);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(keys[ActiveVersion], 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
            return new EncryptedOrigin(ciphertext, nonce, tag, ActiveVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    private DecryptedUrl Decrypt(byte[] ciphertext, byte[] nonce, byte[] tag,
        int keyVersion, byte[] aad)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!keys.TryGetValue(keyVersion, out var key)) throw new MonitorFailure("KEY_VERSION_UNKNOWN");
        var plaintext = new byte[ciphertext.Length];
        try
        {
            if (plaintext.Length is < 1 or > 4096)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new MonitorFailure("URL_DECRYPTION_INVALID");
            }
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            var value = new UTF8Encoding(false, true).GetString(plaintext);
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new MonitorFailure("URL_DECRYPTION_INVALID");
            value = string.Empty;
            return new DecryptedUrl(uri, plaintext);
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new MonitorFailure("URL_DECRYPTION_FAILED");
        }
        catch (DecoderFallbackException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new MonitorFailure("URL_DECRYPTION_FAILED");
        }
        finally { CryptographicOperations.ZeroMemory(aad); }
    }

    internal static byte[] CandidateAad(string candidateId, string itemId,
        string baseCatalogIdentity, int keyVersion) => DomainJson(CandidateDomain, writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("candidateId", candidateId);
        writer.WriteString("itemId", itemId);
        writer.WriteString("baseCatalogIdentity", baseCatalogIdentity);
        writer.WriteNumber("keyVersion", keyVersion);
        writer.WriteEndObject();
    });

    internal static byte[] OriginAad(string catalogIdentity, string itemId, string artifactId,
        int artifactVersion, string manifestIdentity, int keyVersion) =>
        DomainJson(OriginDomain, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("catalogIdentity", catalogIdentity);
            writer.WriteString("itemId", itemId);
            writer.WriteString("artifactId", artifactId);
            writer.WriteNumber("artifactVersion", artifactVersion);
            writer.WriteString("manifestIdentity", manifestIdentity);
            writer.WriteNumber("keyVersion", keyVersion);
            writer.WriteEndObject();
        });

    private static byte[] DomainJson(byte[] domain, Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        stream.Write(domain);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        { Encoder = JavaScriptEncoder.Default })) write(writer);
        return stream.ToArray();
    }

    public void Dispose()
    {
        if (disposed) return;
        foreach (var key in keys.Values) CryptographicOperations.ZeroMemory(key);
        keys.Clear();
        disposed = true;
    }
}

internal sealed class DecryptedUrl(Uri uri, byte[] plaintext) : IDisposable
{
    public Uri Uri { get; } = uri;
    public void Dispose() => CryptographicOperations.ZeroMemory(plaintext);
}

internal sealed class EncryptedOrigin(byte[] ciphertext, byte[] nonce, byte[] tag,
    int keyVersion) : IDisposable
{
    public byte[] Ciphertext { get; } = ciphertext;
    public byte[] Nonce { get; } = nonce;
    public byte[] Tag { get; } = tag;
    public int KeyVersion { get; } = keyVersion;
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Ciphertext);
        CryptographicOperations.ZeroMemory(Nonce);
        CryptographicOperations.ZeroMemory(Tag);
    }
}

internal static class ManagedSnapshotIdentity
{
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentManagedSnapshot/v1\0");

    public static string Compute(SnapshotHeader header, MonitorDeploymentProvenance provenance,
        string operationId, IReadOnlyList<SnapshotItem> items)
    {
        using var stream = new MemoryStream();
        stream.Write(Domain);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteString("productId", "TURBORAMA_SUITE");
            writer.WriteString("baseCatalogIdentity", header.CatalogIdentity);
            writer.WriteString("inventorySha256", header.InventorySha256);
            writer.WriteString("visualCatalogSha256", header.VisualCatalogSha256);
            writer.WriteNumber("originActiveKeyVersion", provenance.ActiveKeyVersion);
            writer.WriteString("originKeySetFingerprint", provenance.KeySetFingerprint);
            writer.WriteString("originAllowlistFingerprint", provenance.AllowlistFingerprint);
            writer.WriteString("operationId", operationId);
            writer.WriteStartArray("items");
            foreach (var item in items.OrderBy(value => value.DisplayOrder).ThenBy(value => value.ItemId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("itemId", item.ItemId);
                writer.WriteString("displayName", item.DisplayName);
                writer.WriteString("visualExtractPolicy", item.VisualExtractPolicy);
                writer.WriteString("status", item.Status);
                if (item.Status == "READY")
                {
                    writer.WriteNumber("artifactVersion", item.ArtifactVersion!.Value);
                    writer.WriteNumber("contentLength", item.ContentLength!.Value);
                    writer.WriteString("sha256", item.Sha256);
                    writer.WriteString("safeFileName", item.SafeFileName);
                    writer.WriteString("fileExtension", item.FileExtension);
                    writer.WriteString("extractPolicy", item.ExtractPolicy);
                    writer.WriteNull("maintenanceReason");
                }
                else
                {
                    writer.WriteNull("artifactVersion");
                    writer.WriteNull("contentLength");
                    writer.WriteNull("sha256");
                    writer.WriteNull("safeFileName");
                    writer.WriteNull("fileExtension");
                    writer.WriteNull("extractPolicy");
                    writer.WriteString("maintenanceReason", "CONTENT_TEMPORARILY_UNAVAILABLE");
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    public static string DescriptorHash(SnapshotItem item, string catalogIdentity)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("productId", "TURBORAMA_SUITE");
            writer.WriteString("itemId", item.ItemId);
            writer.WriteString("artifactId", item.ItemId);
            writer.WriteNumber("artifactVersion", item.ArtifactVersion!.Value);
            writer.WriteNumber("contentLength", item.ContentLength!.Value);
            writer.WriteString("sha256", item.Sha256);
            writer.WriteString("safeFileName", item.SafeFileName);
            writer.WriteString("fileExtension", item.FileExtension);
            writer.WriteString("extractPolicy", item.ExtractPolicy);
            writer.WriteString("manifestIdentity", catalogIdentity);
            writer.WriteEndObject();
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}

internal static class ProtectedMonitorFile
{
    public static void Require(string path, long maximum)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 || info.Length > maximum ||
                (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new MonitorFailure("PROTECTED_FILE_INVALID");
            if (!OperatingSystem.IsLinux()) return;
            var forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & forbidden) != 0)
                throw new MonitorFailure("PROTECTED_FILE_INVALID");
        }
        catch (MonitorFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new MonitorFailure("PROTECTED_FILE_INVALID"); }
    }
}

internal sealed class MonitorKeyRingDocument
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("activeKeyVersion")] public int ActiveKeyVersion { get; init; }
    [JsonPropertyName("keys")] public List<MonitorKeyRingItem>? Keys { get; init; }
}
internal sealed class MonitorKeyRingItem
{
    [JsonPropertyName("version")] public int Version { get; init; }
    [JsonPropertyName("key")] public string? Key { get; init; }
}
