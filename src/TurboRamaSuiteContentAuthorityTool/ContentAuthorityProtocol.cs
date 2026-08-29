using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteContentAuthorityTool;

public static class ContentAuthorityProtocol
{
    public const int SchemaVersion = 1;
    public const string Kind = "TURBORAMA_SUITE_CONTENT_AUTHORITY";
    public const string ProductId = "TURBORAMA_SUITE";
    public const string Algorithm = "rsa-pss-sha256";
    public const long MaximumValiditySeconds = 366L * 24 * 60 * 60;

    private const long MinimumUnixTimeSeconds = 1;
    private const long MaximumUnixTimeSeconds = 253_402_300_799;
    private static readonly byte[] SignatureDomain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentAuthorityConfiguration/v1\0");

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        Indented = false,
        SkipValidation = false
    };

    private static readonly JsonSerializerOptions StrictJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false
    };

    public static byte[] CanonicalPayload(ContentAuthorityPayload payload)
    {
        ValidatePayloadShape(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", payload.SchemaVersion);
            writer.WriteString("kind", payload.Kind);
            writer.WriteString("productId", payload.ProductId);
            writer.WriteString("baseUrl", payload.BaseUrl);
            writer.WriteString("contentAssertionAlgorithm",
                payload.ContentAssertionAlgorithm);
            writer.WriteString("contentAssertionKeyId",
                payload.ContentAssertionKeyId);
            writer.WriteString("contentAssertionPublicKeySpki",
                payload.ContentAssertionPublicKeySpki);
            writer.WriteString("tlsServerSpkiSha256Current",
                payload.TlsServerSpkiSha256Current);
            writer.WriteString("tlsServerSpkiSha256Next",
                payload.TlsServerSpkiSha256Next);
            writer.WriteNumber("issuedAtUnixSeconds",
                payload.IssuedAtUnixSeconds);
            writer.WriteNumber("expiresAtUnixSeconds",
                payload.ExpiresAtUnixSeconds);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static byte[] BuildSigningMessage(ContentAuthorityPayload payload)
    {
        var canonical = CanonicalPayload(payload);
        try
        {
            var message = new byte[SignatureDomain.Length + canonical.Length];
            SignatureDomain.CopyTo(message, 0);
            canonical.CopyTo(message, SignatureDomain.Length);
            return message;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    public static byte[] CanonicalEnvelope(ContentAuthorityEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", envelope.SchemaVersion);
            writer.WriteString("algorithm", envelope.Algorithm);
            writer.WriteString("keyId", envelope.KeyId);
            writer.WriteString("payload", envelope.Payload);
            writer.WriteString("signature", envelope.Signature);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string KeyIdFromSpki(ReadOnlySpan<byte> spki)
    {
        ValidateRsaSpki(spki, "The RSA public key");
        return LowerSha256(spki);
    }

    public static string LowerSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static ContentAuthorityEnvelope ParseEnvelope(
        ReadOnlySpan<byte> envelopeUtf8)
    {
        if (envelopeUtf8.Length is < 64 or > 32 * 1024)
        {
            throw new AuthorityArtifactException(
                "The content-authority envelope has an invalid size.");
        }

        return ParseStrict<ContentAuthorityEnvelope>(envelopeUtf8);
    }

    internal static ContentAuthorityPayload ParsePayload(
        ReadOnlySpan<byte> payloadUtf8) =>
        ParseStrict<ContentAuthorityPayload>(payloadUtf8);

    internal static void ValidatePayloadForTime(
        ContentAuthorityPayload payload,
        long nowUnixSeconds)
    {
        ValidatePayloadShape(payload);
        ValidateUnixTime(nowUnixSeconds, "The verification time");
        var validity = payload.ExpiresAtUnixSeconds - payload.IssuedAtUnixSeconds;
        if (payload.IssuedAtUnixSeconds > nowUnixSeconds + 300 ||
            payload.ExpiresAtUnixSeconds <= nowUnixSeconds ||
            validity > MaximumValiditySeconds)
        {
            throw new AuthorityArtifactException(
                "The content-authority validity window is not acceptable.");
        }
    }

    internal static void ValidatePayloadShape(ContentAuthorityPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.BaseUrl is null ||
            payload.ContentAssertionKeyId is null ||
            payload.ContentAssertionPublicKeySpki is null ||
            payload.TlsServerSpkiSha256Current is null ||
            payload.TlsServerSpkiSha256Next is null ||
            payload.SchemaVersion != SchemaVersion ||
            !string.Equals(payload.Kind, Kind, StringComparison.Ordinal) ||
            !string.Equals(payload.ProductId, ProductId, StringComparison.Ordinal) ||
            !string.Equals(payload.ContentAssertionAlgorithm, Algorithm,
                StringComparison.Ordinal))
        {
            throw new AuthorityArtifactException(
                "The content-authority payload type is invalid.");
        }

        RequireCanonicalHex(payload.ContentAssertionKeyId,
            "The content assertion key identifier");
        RequireCanonicalHex(payload.TlsServerSpkiSha256Current,
            "The current TLS SPKI pin");
        if (payload.TlsServerSpkiSha256Next.Length != 0)
        {
            RequireCanonicalHex(payload.TlsServerSpkiSha256Next,
                "The next TLS SPKI pin");
            if (FixedHexEquals(payload.TlsServerSpkiSha256Current,
                    payload.TlsServerSpkiSha256Next))
            {
                throw new AuthorityArtifactException(
                    "The current and next TLS SPKI pins must be distinct.");
            }
        }

        var contentSpki = DecodeCanonicalBase64(
            payload.ContentAssertionPublicKeySpki,
            "The content assertion public key", 256, 4096);
        try
        {
            ValidateRsaSpki(contentSpki, "The content assertion public key");
            if (!FixedHexEquals(payload.ContentAssertionKeyId,
                    KeyIdFromSpki(contentSpki)))
            {
                throw new AuthorityArtifactException(
                    "The content assertion key identifier does not match its SPKI.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentSpki);
        }

        ValidateCanonicalBaseUrl(payload.BaseUrl);
        ValidateUnixTime(payload.IssuedAtUnixSeconds, "The issue time");
        ValidateUnixTime(payload.ExpiresAtUnixSeconds, "The expiration time");
        if (payload.ExpiresAtUnixSeconds <= payload.IssuedAtUnixSeconds)
        {
            throw new AuthorityArtifactException(
                "The content-authority validity window is invalid.");
        }
    }

    internal static void ValidateRsaSpki(ReadOnlySpan<byte> spki, string label)
    {
        if (spki.Length is < 256 or > 4096)
        {
            throw new AuthorityArtifactException($"{label} has an invalid size.");
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out var consumed);
            if (consumed != spki.Length || rsa.KeySize is < 2048 or > 4096)
            {
                throw new AuthorityArtifactException(
                    $"{label} must be canonical RSA with 2048 to 4096 bits.");
            }

            var canonical = rsa.ExportSubjectPublicKeyInfo();
            try
            {
                if (!canonical.AsSpan().SequenceEqual(spki))
                {
                    throw new AuthorityArtifactException(
                        $"{label} is not canonical DER SubjectPublicKeyInfo.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }
        }
        catch (CryptographicException)
        {
            throw new AuthorityArtifactException(
                $"{label} is not a valid RSA SubjectPublicKeyInfo.");
        }
    }

    internal static byte[] DecodeCanonicalBase64(
        string? value,
        string label,
        int minimumBytes,
        int maximumBytes)
    {
        var encoded = value ?? string.Empty;
        if (encoded.Any(char.IsWhiteSpace))
        {
            throw new AuthorityArtifactException($"{label} is not canonical Base64.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new AuthorityArtifactException($"{label} is not valid Base64.");
        }

        if (bytes.Length < minimumBytes || bytes.Length > maximumBytes ||
            !string.Equals(Convert.ToBase64String(bytes), encoded,
                StringComparison.Ordinal))
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new AuthorityArtifactException($"{label} is not canonical Base64.");
        }

        return bytes;
    }

    internal static string RequireCanonicalHex(string? value, string label)
    {
        var text = value ?? string.Empty;
        if (text.Length != 64 || text.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new AuthorityArtifactException(
                $"{label} must be exactly 64 lowercase hexadecimal characters.");
        }

        return text;
    }

    internal static bool FixedHexEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        try
        {
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private static void ValidateCanonicalBaseUrl(string value)
    {
        if (value.Length is < 9 or > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 ||
            !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ||
            !string.Equals(uri.AbsoluteUri, value, StringComparison.Ordinal))
        {
            throw new AuthorityArtifactException(
                "The content-authority base URL is not canonical HTTPS.");
        }
    }

    private static void ValidateUnixTime(long value, string label)
    {
        if (value is < MinimumUnixTimeSeconds or > MaximumUnixTimeSeconds)
        {
            throw new AuthorityArtifactException($"{label} is outside the Unix range.");
        }
    }

    private static T ParseStrict<T>(ReadOnlySpan<byte> utf8)
    {
        var copy = utf8.ToArray();
        try
        {
            using var document = JsonDocument.Parse(copy, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            RejectDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<T>(copy, StrictJsonOptions) ??
                throw new AuthorityArtifactException(
                    "The content-authority JSON is empty.");
        }
        catch (JsonException)
        {
            throw new AuthorityArtifactException(
                "The content-authority JSON is invalid.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new AuthorityArtifactException(
                        "The content-authority JSON contains a duplicate property.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }
}
