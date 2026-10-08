using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteNotifications;

// No destination phone, free-form message, IP, MAC, local path or secret is
// accepted from the client. A report authenticates a CLIENT assertion of local
// completion; the server cannot independently inspect the Windows filesystem.
public sealed record ExtractionCompletionEvent(int SchemaVersion, string ProductId,
    string EventId, string LicenseId, string DeviceId, string ItemId,
    string ArtifactId, int ArtifactVersion, string ManifestIdentity,
    string ArchiveSha256, string CategoryId, long CompletedAtUnixSeconds);
public sealed record ExtractionCompletionProof(ExtractionCompletionEvent Event,
    string SessionId, long SentAtUnixSeconds, string Signature);
public sealed record ExtractionCompletionAck(int SchemaVersion, string EventId, string Status);

public static class ExtractionCompletionProtocol
{
    public const string Route = "/v1/suite/notifications/extraction-completed";
    public const int MaximumBodyBytes = 16 * 1024;
    public const long MaximumAgeSeconds = 7 * 24 * 60 * 60;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 4
    };
    private const string Domain = "TurboRamaSuiteExtractionCompletion/v1\0";

    public static string CategoryName(string category) => category switch
    {
        "system-tools" => "Sistema e utilitários", "emulators" => "Emuladores",
        "retro-games" => "Jogos retrô", "nintendo-3ds" => "Nintendo 3DS",
        "gamecube" => "Nintendo GameCube", "playstation-1" => "PlayStation 1",
        "playstation-2" => "PlayStation 2", "playstation-2-br" => "PlayStation 2 • BR",
        "playstation-3" => "PlayStation 3", "playstation-4" => "PlayStation 4",
        "playstation-5" => "PlayStation 5", "psp" => "PSP", "ps-vita" => "PS Vita",
        "sega-saturn" => "Sega Saturn", "nintendo-switch" => "Nintendo Switch",
        "nintendo-wii" => "Nintendo Wii", "nintendo-wii-u" => "Nintendo Wii U",
        "windows" => "Windows", "xbox" => "Xbox", "xbox-360" => "Xbox 360",
        "xbox-one" => "Xbox One", "xbox-series" => "Xbox Series",
        _ => throw new ArgumentException("Notification category is invalid.")
    };

    // One notification per license/device/content version, including retries,
    // repeated publication and process restarts. No session/time in this key.
    public static string EventId(ExtractionCompletionEvent value)
    {
        var identity = string.Join('\n', Domain, value.ProductId, value.LicenseId,
            value.DeviceId, value.ItemId, value.ArtifactId,
            value.ArtifactVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.ManifestIdentity, value.ArchiveSha256);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    public static void ValidateEvent(ExtractionCompletionEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion != 1 || value.ProductId != "TURBORAMA_SUITE"
            || value.LicenseId is not { Length: >= 6 and <= 64 }
            || value.LicenseId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            || !IsHex(value.DeviceId, 64) || !IsHex(value.ItemId, 32)
            || !IsHex(value.ArtifactId, 32) || !IsHex(value.ManifestIdentity, 64)
            || !IsHex(value.ArchiveSha256, 64) || value.ArtifactVersion < 1
            || value.CompletedAtUnixSeconds is < 1577836800 or > 4102444800
            || !IsHex(value.EventId, 64) || value.EventId != EventId(value))
            throw new ArgumentException("Notification event is invalid.");
        _ = CategoryName(value.CategoryId);
    }

    public static byte[] SigningBytes(ExtractionCompletionEvent value,
        string sessionId, long sentAt)
    {
        ValidateEvent(value);
        if (!IsHex(sessionId, 64) || sentAt is < 1577836800 or > 4102444800)
            throw new ArgumentException("Notification proof is invalid.");
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes(Domain));
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("event");
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", value.SchemaVersion);
            writer.WriteString("productId", value.ProductId);
            writer.WriteString("eventId", value.EventId);
            writer.WriteString("licenseId", value.LicenseId);
            writer.WriteString("deviceId", value.DeviceId);
            writer.WriteString("itemId", value.ItemId);
            writer.WriteString("artifactId", value.ArtifactId);
            writer.WriteNumber("artifactVersion", value.ArtifactVersion);
            writer.WriteString("manifestIdentity", value.ManifestIdentity);
            writer.WriteString("archiveSha256", value.ArchiveSha256);
            writer.WriteString("categoryId", value.CategoryId);
            writer.WriteNumber("completedAtUnixSeconds", value.CompletedAtUnixSeconds);
            writer.WriteEndObject();
            writer.WriteString("sessionId", sessionId);
            writer.WriteNumber("sentAtUnixSeconds", sentAt);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static bool Verify(ExtractionCompletionProof proof, string publicKeySpki, long now)
    {
        try
        {
            if (proof.Event is null || proof.Signature is not { Length: >= 344 and <= 684 }
                || proof.SentAtUnixSeconds < now - 300 || proof.SentAtUnixSeconds > now + 300
                || proof.Event.CompletedAtUnixSeconds > now + 300
                || proof.Event.CompletedAtUnixSeconds < now - MaximumAgeSeconds)
                return false;
            var bytes = SigningBytes(proof.Event, proof.SessionId, proof.SentAtUnixSeconds);
            var signature = Convert.FromBase64String(proof.Signature);
            var spki = Convert.FromBase64String(publicKeySpki);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out var read);
            return read == spki.Length && rsa.KeySize is >= 2048 and <= 4096
                && signature.Length == rsa.KeySize / 8
                && Convert.ToBase64String(signature) == proof.Signature
                && rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or CryptographicException)
        { return false; }
    }

    public static T Parse<T>(ReadOnlySpan<byte> bytes) where T : class
    {
        if (bytes.Length is 0 or > MaximumBodyBytes) throw new JsonException("Invalid body.");
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions) ?? throw new JsonException("Null body.");
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate member.");
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }

    public static bool IsHex(string? value, int length) =>
        value?.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
