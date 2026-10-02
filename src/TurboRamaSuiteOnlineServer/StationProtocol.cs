using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

// Independent wire protocol: Suite/EmulationStation canonical bytes must not change.
public static class StationProtocol
{
    public const string Product = "TURBORAMA_STATION_ANDROID";
    public const string Application = Product;
    public const string Prefix = "TurboRamaStationAndroid/";
    public const int MaximumBodyBytes = 8192;

    public static void RequireIdentity(int version, string domain, string expectedDomain,
        string product, string application, string deviceId)
    {
        if (version != 1 || domain != Prefix + expectedDomain + "/v1" ||
            product != Product || application != Application ||
            !IsCanonicalBase64Url(deviceId, 32))
            throw new SuiteException(400, "STATION_IDENTITY_INVALID", "Station identity is invalid.");
    }

    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value, int maximumBytes)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumBytes * 4 / 3 + 4 ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new SuiteException(400, "STATION_ENCODING_INVALID", "Station encoding is invalid.");
        try
        {
            var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/')
                .PadRight((value.Length + 3) / 4 * 4, '='));
            if (bytes.Length > maximumBytes || Encode(bytes) != value)
                throw new SuiteException(400, "STATION_ENCODING_INVALID", "Station encoding is invalid.");
            return bytes;
        }
        catch (FormatException ex)
        {
            throw new SuiteException(400, "STATION_ENCODING_INVALID", "Station encoding is invalid.", ex);
        }
    }

    public static bool IsCanonicalBase64Url(string value, int expectedBytes)
    {
        try { return Decode(value, expectedBytes).Length == expectedBytes; }
        catch (SuiteException) { return false; }
    }

    public static bool IsSafeLibraryId(string value) =>
        value.Length is >= 8 and <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') &&
        !value.Contains("..", StringComparison.Ordinal);

    public static byte[] DeviceKey(string deviceId, string spkiText)
    {
        var spki = Decode(spkiText, 4096);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out var consumed);
            if (consumed != spki.Length || rsa.KeySize != 2048 ||
                Encode(SHA256.HashData(spki)) != deviceId ||
                !rsa.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(spki))
                throw new SuiteException(400, "STATION_DEVICE_INVALID", "Station device is invalid.");
            return spki;
        }
        catch (CryptographicException ex)
        {
            throw new SuiteException(400, "STATION_DEVICE_INVALID", "Station device is invalid.", ex);
        }
    }

    public static JsonDocument VerifyDeviceEnvelope(StationDeviceEnvelope envelope,
        string expectedDeviceId, string expectedSpki)
    {
        var payload = Decode(envelope.Payload, MaximumBodyBytes);
        var signature = Decode(envelope.Signature, 512);
        var spki = DeviceKey(expectedDeviceId, expectedSpki);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out _);
            if (signature.Length != 256 || !rsa.VerifyData(payload, signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new SuiteException(403, "STATION_PROOF_INVALID", "Station proof is invalid.");
            var json = JsonDocument.Parse(payload.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                json.Dispose();
                throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.");
            }
            RejectDuplicates(json.RootElement);
            return json;
        }
        catch (JsonException ex)
        {
            throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(signature);
            CryptographicOperations.ZeroMemory(spki);
        }
    }

    public static string RequireString(JsonElement json, string name, int maximumLength)
    {
        if (!json.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.");
        var value = field.GetString()!;
        if (value.Length is 0 || value.Length > maximumLength || value.Any(char.IsControl))
            throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.");
        return value;
    }

    public static int RequireInt(JsonElement json, string name, int minimum,
        int maximum)
    {
        if (!json.TryGetProperty(name, out var field) ||
            field.ValueKind != JsonValueKind.Number || !field.TryGetInt32(out var value) ||
            value < minimum || value > maximum)
            throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.");
        return value;
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();

    public static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new SuiteException(400, "STATION_JSON_INVALID",
                        "Station JSON is invalid.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicates(child);
    }
}

public sealed record StationDeviceEnvelope(string Payload, string Signature);

public sealed class StationResponseSigner : IDisposable
{
    private readonly RSA _rsa;
    public StationResponseSigner(string privatePem)
    {
        _rsa = RSA.Create();
        _rsa.ImportFromPem(privatePem);
        if (_rsa.KeySize < 2048) throw new InvalidOperationException("Station signing key is too small.");
        KeyId = Convert.ToHexString(SHA256.HashData(_rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    public string KeyId { get; }
    public string PublicKeySpkiBase64Url => StationProtocol.Encode(_rsa.ExportSubjectPublicKeyInfo());

    public object Sign(object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, StrictJson.Options);
        try
        {
            return new
            {
                keyId = KeyId,
                payload = StationProtocol.Encode(bytes),
                signature = StationProtocol.Encode(_rsa.SignData(bytes, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss))
            };
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void Dispose() => _rsa.Dispose();
}
