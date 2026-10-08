using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteNotifications;

// A signed assertion of local completion, not evidence that the Linux server
// inspected the client's disk. DownloadId survives pause, restart and HTTP retry.
public sealed record DownloadCompletionEvent(int SchemaVersion, string ProductId,
    string EventId, string LicenseId, string DeviceId, string DownloadId, string ItemId,
    string ArtifactId, int ArtifactVersion, string ManifestIdentity, string FileSha256,
    string CategoryId, string CompletionKind, long CompletedAtUnixSeconds);
public sealed record DownloadCompletionProof(DownloadCompletionEvent Event,
    string SessionId, long SentAtUnixSeconds, string Signature);

public sealed record DownloadCompletionAck(int SchemaVersion, string EventId, string Status);

public static class DownloadCompletionProtocol
{
    public const string Route = "/v1/suite/notifications/download-completed";
    public const int MaximumBodyBytes = ExtractionCompletionProtocol.MaximumBodyBytes;
    public static JsonSerializerOptions JsonOptions => ExtractionCompletionProtocol.JsonOptions;
    public const string FileReady = "FILE_READY";
    public const string Extracted = "EXTRACTED";
    private const string Domain = "TurboRamaSuiteDownloadCompletion/v1\0";

    public static string EventId(DownloadCompletionEvent value)
    {
        var identity = string.Join('\n', Domain, value.ProductId, value.LicenseId,
            value.DeviceId, value.DownloadId, value.ItemId, value.ArtifactId,
            value.ArtifactVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            value.ManifestIdentity, value.FileSha256, value.CompletionKind);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    public static void ValidateEvent(DownloadCompletionEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion != 1 || value.ProductId != "TURBORAMA_SUITE"
            || value.LicenseId is not { Length: >= 6 and <= 64 }
            || value.LicenseId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            || !ExtractionCompletionProtocol.IsHex(value.DeviceId, 64)
            || !ExtractionCompletionProtocol.IsHex(value.DownloadId, 32)
            || !ExtractionCompletionProtocol.IsHex(value.ItemId, 32)
            || !ExtractionCompletionProtocol.IsHex(value.ArtifactId, 32)
            || !ExtractionCompletionProtocol.IsHex(value.ManifestIdentity, 64)
            || !ExtractionCompletionProtocol.IsHex(value.FileSha256, 64)
            || value.ArtifactVersion < 1 || value.CompletionKind is not (FileReady or Extracted)
            || value.CompletedAtUnixSeconds is < 1577836800 or > 4102444800
            || !ExtractionCompletionProtocol.IsHex(value.EventId, 64)
            || value.EventId != EventId(value))
            throw new ArgumentException("Download completion is invalid.");
        _ = ExtractionCompletionProtocol.CategoryName(value.CategoryId);
    }

    public static byte[] SigningBytes(DownloadCompletionEvent value, string sessionId, long sentAt)
    {
        ValidateEvent(value);
        if (!ExtractionCompletionProtocol.IsHex(sessionId, 64)
            || sentAt is < 1577836800 or > 4102444800)
            throw new ArgumentException("Download proof is invalid.");
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
            writer.WriteString("downloadId", value.DownloadId);
            writer.WriteString("itemId", value.ItemId);
            writer.WriteString("artifactId", value.ArtifactId);
            writer.WriteNumber("artifactVersion", value.ArtifactVersion);
            writer.WriteString("manifestIdentity", value.ManifestIdentity);
            writer.WriteString("fileSha256", value.FileSha256);
            writer.WriteString("categoryId", value.CategoryId);
            writer.WriteString("completionKind", value.CompletionKind);
            writer.WriteNumber("completedAtUnixSeconds", value.CompletedAtUnixSeconds);
            writer.WriteEndObject();
            writer.WriteString("sessionId", sessionId);
            writer.WriteNumber("sentAtUnixSeconds", sentAt);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public static bool Verify(DownloadCompletionProof proof, string publicKeySpki, long now)
    {
        try
        {
            if (proof.Event is null || proof.Signature is not { Length: >= 344 and <= 684 }
                || proof.SentAtUnixSeconds < now - 300 || proof.SentAtUnixSeconds > now + 300
                || proof.Event.CompletedAtUnixSeconds > now + 300
                || proof.Event.CompletedAtUnixSeconds < now - ExtractionCompletionProtocol.MaximumAgeSeconds)
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
}
