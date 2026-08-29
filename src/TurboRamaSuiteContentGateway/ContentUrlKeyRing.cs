using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TurboRamaSuiteOnlineServer;

namespace TurboRamaSuiteContentGateway;

public sealed class ContentUrlKeyRing : IDisposable
{
    public const int MaximumUrlUtf8Bytes = 4096;
    private readonly Dictionary<int, byte[]> _keys;
    private readonly byte[] _keySetFingerprint;

    private ContentUrlKeyRing(int activeKeyVersion, Dictionary<int, byte[]> keys)
    {
        ActiveKeyVersion = activeKeyVersion;
        _keys = keys;
        _keySetFingerprint = ContentDeploymentProofProtocol.KeySetFingerprint(keys);
        VerifyCanary(keys[activeKeyVersion], activeKeyVersion);
    }

    public int ActiveKeyVersion { get; }

    public byte[] CopyKeySetFingerprint() => _keySetFingerprint.ToArray();

    public int[] CopyKeyVersions() => _keys.Keys.Order().ToArray();

    public bool VerifyDeploymentProof(
        int activeKeyVersion,
        ReadOnlySpan<byte> suppliedKeySetFingerprint,
        ReadOnlySpan<byte> suppliedAllowlistFingerprint,
        ReadOnlySpan<byte> loadedAllowlistFingerprint,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> proof)
    {
        if (activeKeyVersion != ActiveKeyVersion || nonce.Length != 32 || proof.Length != 32 ||
            suppliedKeySetFingerprint.Length != 32 || suppliedAllowlistFingerprint.Length != 32 ||
            loadedAllowlistFingerprint.Length != 32 ||
            !CryptographicOperations.FixedTimeEquals(
                _keySetFingerprint, suppliedKeySetFingerprint) ||
            !CryptographicOperations.FixedTimeEquals(
                loadedAllowlistFingerprint, suppliedAllowlistFingerprint))
            return false;
        var message = ContentDeploymentProofProtocol.ProofMessage(activeKeyVersion,
            _keySetFingerprint, loadedAllowlistFingerprint, nonce);
        byte[] expected = [];
        try
        {
            expected = HMACSHA256.HashData(_keys[ActiveKeyVersion], message);
            return CryptographicOperations.FixedTimeEquals(expected, proof);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
            if (expected.Length != 0) CryptographicOperations.ZeroMemory(expected);
        }
    }

    public static ContentUrlKeyRing Load(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Content URL key ring is unavailable.");
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        info.Refresh();
        if (!info.Exists || (info.Attributes & (FileAttributes.Directory |
                                                FileAttributes.ReparsePoint)) != 0 ||
            info.LinkTarget is not null || info.Length is < 32 or > 64 * 1024)
            throw new InvalidOperationException("Content URL key ring is unavailable.");
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(fullPath);
            var allowed = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if ((mode & ~allowed) != 0 || (mode & UnixFileMode.UserRead) == 0)
                throw new InvalidOperationException(
                    "Content URL key ring permissions are unsafe.");
        }
        var fileBytes = File.ReadAllBytes(fullPath);
        KeyRingDocument document;
        try { document = StrictJson.Parse<KeyRingDocument>(fileBytes); }
        finally { CryptographicOperations.ZeroMemory(fileBytes); }
        if (document.SchemaVersion != Protocol.SchemaVersion ||
            document.ActiveKeyVersion < 1 || document.Keys.Count == 0)
            throw new InvalidOperationException("Content URL key ring is invalid.");
        var keys = new Dictionary<int, byte[]>();
        try
        {
            foreach (var entry in document.Keys)
            {
                if (entry is null || entry.Version < 1 ||
                    !keys.TryAdd(entry.Version, DecodeKey(entry.Key)))
                    throw new InvalidOperationException("Content URL key ring is invalid.");
            }
            if (!keys.ContainsKey(document.ActiveKeyVersion))
                throw new InvalidOperationException("Content URL key ring is invalid.");
            return new ContentUrlKeyRing(document.ActiveKeyVersion, keys);
        }
        catch
        {
            foreach (var key in keys.Values) CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    public Uri Decrypt(ClaimedContentGrantRecord grant)
    {
        if (!_keys.TryGetValue(grant.KeyVersion, out var key) ||
            grant.UpstreamUrlNonce.Length != 12 || grant.UpstreamUrlTag.Length != 16 ||
            grant.UpstreamUrlCiphertext.Length is < 1 or > MaximumUrlUtf8Bytes)
            throw Failure();
        var associatedData = ContentProtocol.UrlEncryptionAssociatedData(
            grant.CatalogIdentity, grant.ItemId, grant.ArtifactId,
            grant.ArtifactVersion, grant.ManifestIdentity, grant.KeyVersion);
        var plaintext = new byte[grant.UpstreamUrlCiphertext.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(grant.UpstreamUrlNonce, grant.UpstreamUrlCiphertext,
                grant.UpstreamUrlTag, plaintext, associatedData);
            var text = new UTF8Encoding(false, true).GetString(plaintext);
            if (Encoding.UTF8.GetByteCount(text) > MaximumUrlUtf8Bytes ||
                !Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                !string.Equals(text, uri.AbsoluteUri, StringComparison.Ordinal) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Fragment) || uri.Port != 443)
                throw Failure();
            return uri;
        }
        catch (CryptographicException) { throw Failure(); }
        catch (DecoderFallbackException) { throw Failure(); }
        finally
        {
            CryptographicOperations.ZeroMemory(associatedData);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Dispose()
    {
        foreach (var key in _keys.Values) CryptographicOperations.ZeroMemory(key);
        _keys.Clear();
        CryptographicOperations.ZeroMemory(_keySetFingerprint);
    }

    private static byte[] DecodeKey(string value)
    {
        byte[] key;
        try { key = Convert.FromBase64String(value); }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Content URL key ring is invalid.", exception);
        }
        if (key.Length != 32 || Convert.ToBase64String(key) != value)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException("Content URL key ring is invalid.");
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
                throw new CryptographicException();
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Content URL key ring is invalid.", exception);
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

    private static SuiteException Failure() => new(502, "CONTENT_UPSTREAM_FAILURE",
        "The content transfer could not be completed.");

    private sealed record KeyRingDocument(
        int SchemaVersion,
        int ActiveKeyVersion,
        IReadOnlyList<KeyRingEntry?> Keys);

    private sealed record KeyRingEntry(int Version, string Key);
}

public static class ContentDeploymentProofProtocol
{
    private static readonly byte[] KeySetDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentKeySetFingerprint/v1\0");
    private static readonly byte[] AllowlistDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentOriginAllowlist/v1\0");
    private static readonly byte[] ProofDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentDeploymentProof/v1\0");

    public static byte[] KeySetFingerprint(
        IEnumerable<KeyValuePair<int, byte[]>> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var ordered = keys.OrderBy(entry => entry.Key).ToArray();
        if (ordered.Length == 0 || ordered.Any(entry => entry.Key < 1 || entry.Value.Length != 32))
            throw new ArgumentException("The key set is invalid.", nameof(keys));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(KeySetDomain);
        Span<byte> number = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(number, ordered.Length);
        hash.AppendData(number);
        foreach (var entry in ordered)
        {
            BinaryPrimitives.WriteInt32BigEndian(number, entry.Key);
            hash.AppendData(number);
            hash.AppendData(entry.Value);
        }
        return hash.GetHashAndReset();
    }

    public static byte[] AllowlistFingerprint(IEnumerable<string> canonicalHosts)
    {
        ArgumentNullException.ThrowIfNull(canonicalHosts);
        var ordered = canonicalHosts.Distinct(StringComparer.Ordinal)
            .OrderBy(host => host, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0)
            throw new ArgumentException("The allowlist is empty.", nameof(canonicalHosts));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(AllowlistDomain);
        Span<byte> number = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(number, ordered.Length);
        hash.AppendData(number);
        foreach (var host in ordered)
        {
            var bytes = Encoding.UTF8.GetBytes(host);
            try
            {
                BinaryPrimitives.WriteInt32BigEndian(number, bytes.Length);
                hash.AppendData(number);
                hash.AppendData(bytes);
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return hash.GetHashAndReset();
    }

    public static byte[] ProofMessage(
        int activeKeyVersion,
        ReadOnlySpan<byte> keySetFingerprint,
        ReadOnlySpan<byte> allowlistFingerprint,
        ReadOnlySpan<byte> nonce)
    {
        if (activeKeyVersion < 1 || keySetFingerprint.Length != 32 ||
            allowlistFingerprint.Length != 32 || nonce.Length != 32)
            throw new ArgumentException("The deployment proof input is invalid.");
        var message = new byte[ProofDomain.Length + sizeof(int) + 96];
        ProofDomain.CopyTo(message, 0);
        var offset = ProofDomain.Length;
        BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(offset, sizeof(int)),
            activeKeyVersion);
        offset += sizeof(int);
        keySetFingerprint.CopyTo(message.AsSpan(offset, 32));
        offset += 32;
        allowlistFingerprint.CopyTo(message.AsSpan(offset, 32));
        offset += 32;
        nonce.CopyTo(message.AsSpan(offset, 32));
        return message;
    }

    public static byte[] DecodeFingerprint(string value)
    {
        if (value.Length != 64 || value.Any(character =>
                !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new FormatException();
        var bytes = Convert.FromHexString(value);
        if (bytes.Length != 32) throw new FormatException();
        return bytes;
    }
}
