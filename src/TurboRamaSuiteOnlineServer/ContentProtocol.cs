using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public static class ContentProtocol
{
    public const string CatalogReadAction = "catalog.read";
    public const string DownloadAuthorizeAction = "download.authorize";
    public const string CatalogPageKind = "TURBORAMA_SUITE_CATALOG_PAGE";
    public const string DownloadGrantKind = "TURBORAMA_SUITE_DOWNLOAD_GRANT";
    public const string CatalogChallengeKind = "TURBORAMA_SUITE_CATALOG_READ_CHALLENGE";
    public const string DownloadChallengeKind = "TURBORAMA_SUITE_DOWNLOAD_AUTHORIZE_CHALLENGE";
    public const string ReadyAvailability = "READY";
    public const string MaintenanceAvailability = "MAINTENANCE";
    public const string MaintenanceReasonCode = "CONTENT_TEMPORARILY_UNAVAILABLE";
    public const string CatalogAssertionDomain = "TurboRamaSuiteContentAssertion/catalog-page/v1\0";
    public const string DownloadAssertionDomain = "TurboRamaSuiteContentAssertion/download-grant/v1\0";
    public const string CatalogChallengeAssertionDomain = "TurboRamaSuiteOnlineAssertion/catalog-read-challenge/v1\0";
    public const string DownloadChallengeAssertionDomain = "TurboRamaSuiteOnlineAssertion/download-authorize-challenge/v1\0";
    public const int MaximumCatalogPageSize = 64;
    public const int MaximumCatalogResponseItems = 24;
    public const int ExpectedProductionItemCount = 850;
    public const int MaximumSafeFileNameLength = 180;
    public const long MaximumContentLength = 512L * 1024 * 1024 * 1024;

    private static readonly byte[] UrlEncryptionDomain =
        Encoding.ASCII.GetBytes("TurboRamaSuiteContentUrl/v1\0");

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        Indented = false,
        SkipValidation = false
    };

    public static bool IsContentAction(string action) =>
        action is CatalogReadAction or DownloadAuthorizeAction;

    public static string CatalogContextHash(CatalogPageContext context)
    {
        Validate(context);
        return Hash(Canonical(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", context.SchemaVersion);
            writer.WriteString("productId", context.ProductId);
            writer.WriteString("licenseId", context.LicenseId);
            writer.WriteString("deviceId", context.DeviceId);
            writer.WriteString("sessionId", context.SessionId);
            writer.WriteString("action", context.Action);
            writer.WriteString("cursor", context.Cursor);
            writer.WriteNumber("pageSize", context.PageSize);
            writer.WriteEndObject();
        }));
    }

    public static string DownloadContextHash(DownloadAuthorizationContext context)
    {
        Validate(context);
        return Hash(Canonical(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", context.SchemaVersion);
            writer.WriteString("productId", context.ProductId);
            writer.WriteString("licenseId", context.LicenseId);
            writer.WriteString("deviceId", context.DeviceId);
            writer.WriteString("sessionId", context.SessionId);
            writer.WriteString("action", context.Action);
            writer.WriteString("catalogIdentity", context.CatalogIdentity);
            writer.WriteString("itemId", context.ItemId);
            writer.WriteString("artifactId", context.ArtifactId);
            writer.WriteNumber("artifactVersion", context.ArtifactVersion);
            writer.WriteString("manifestIdentity", context.ManifestIdentity);
            writer.WriteString("descriptorHash", context.DescriptorHash);
            writer.WriteNumber("offset", context.Offset);
            writer.WriteEndObject();
        }));
    }

    public static string DescriptorHash(string itemId, ContentArtifactDescriptor descriptor)
    {
        ValidateItemId(itemId);
        Validate(descriptor);
        return Hash(Canonical(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", Protocol.SchemaVersion);
            writer.WriteString("productId", Protocol.ProductId);
            writer.WriteString("itemId", itemId);
            WriteDescriptorProperties(writer, descriptor);
            writer.WriteEndObject();
        }));
    }

    public static byte[] CanonicalAssertion(object assertion) => assertion switch
    {
        CatalogPageAssertion value => Canonical(writer => Write(writer, value)),
        DownloadGrantAssertion value => Canonical(writer => Write(writer, value)),
        _ => throw new ArgumentOutOfRangeException(nameof(assertion))
    };

    public static string AssertionDomain(object assertion) => assertion switch
    {
        CatalogPageAssertion => CatalogAssertionDomain,
        DownloadGrantAssertion => DownloadAssertionDomain,
        _ => throw new ArgumentOutOfRangeException(nameof(assertion))
    };

    public static string AssertionKind(object assertion) => assertion switch
    {
        CatalogPageAssertion value => value.Kind,
        DownloadGrantAssertion value => value.Kind,
        _ => throw new ArgumentOutOfRangeException(nameof(assertion))
    };

    public static string EncodeCursor(string itemId)
    {
        ValidateItemId(itemId);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(itemId))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string DecodeCursor(string cursor)
    {
        if (cursor.Length == 0) return string.Empty;
        if (cursor.Length > 256 || cursor.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
            Invalid();
        var padded = cursor.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            0 => "",
            2 => "==",
            3 => "=",
            _ => InvalidString()
        };
        try
        {
            var bytes = Convert.FromBase64String(padded);
            var decoded = new UTF8Encoding(false, true).GetString(bytes);
            if (!FixedEquals(EncodeCursor(decoded), cursor)) Invalid();
            return decoded;
        }
        catch (FormatException) { Invalid(); throw; }
        catch (DecoderFallbackException) { Invalid(); throw; }
    }

    public static byte[] UrlEncryptionAssociatedData(
        string catalogIdentity,
        string itemId,
        string artifactId,
        int artifactVersion,
        string manifestIdentity,
        int keyVersion)
    {
        ValidateHex(catalogIdentity);
        ValidateItemId(itemId);
        ValidateArtifactId(artifactId);
        if (artifactVersion < 1 || keyVersion < 1) Invalid();
        ValidateHex(manifestIdentity);
        var json = Canonical(writer =>
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
        var result = new byte[UrlEncryptionDomain.Length + json.Length];
        UrlEncryptionDomain.CopyTo(result, 0);
        json.CopyTo(result, UrlEncryptionDomain.Length);
        CryptographicOperations.ZeroMemory(json);
        return result;
    }

    public static void Validate(CatalogPageContext context)
    {
        CommonContext(context.SchemaVersion, context.ProductId, context.LicenseId,
            context.DeviceId, context.SessionId, context.Action, CatalogReadAction);
        _ = DecodeCursor(context.Cursor);
        if (context.PageSize is < 1 or > MaximumCatalogPageSize) Invalid();
    }

    public static void Validate(DownloadAuthorizationContext context)
    {
        CommonContext(context.SchemaVersion, context.ProductId, context.LicenseId,
            context.DeviceId, context.SessionId, context.Action, DownloadAuthorizeAction);
        ValidateHex(context.CatalogIdentity);
        ValidateItemId(context.ItemId);
        ValidateArtifactId(context.ArtifactId);
        if (context.ArtifactVersion < 1) Invalid();
        ValidateHex(context.ManifestIdentity);
        ValidateHex(context.DescriptorHash);
        if (context.Offset < 0) Invalid();
    }

    public static void Validate(ContentArtifactDescriptor descriptor)
    {
        ValidateArtifactId(descriptor.ArtifactId);
        if (descriptor.ArtifactVersion < 1 || descriptor.ContentLength is < 1 or > MaximumContentLength)
            Invalid();
        ValidateHex(descriptor.Sha256);
        ValidateSafeFileName(descriptor.SafeFileName);
        ValidateExtension(descriptor.FileExtension);
        if (!descriptor.SafeFileName.EndsWith(descriptor.FileExtension,
                StringComparison.Ordinal)) Invalid();
        if (descriptor.ExtractPolicy is not ("NONE" or "EXTRACT_ARCHIVE")) Invalid();
        ValidateHex(descriptor.ManifestIdentity);
    }

    public static void ValidateGrantId(string value) => ValidateHex(value);
    public static void ValidateTokenDigest(string value) => ValidateHex(value);
    public static bool IsSha256(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void Write(Utf8JsonWriter writer, CatalogPageAssertion assertion)
    {
        writer.WriteStartObject();
        WriteCommon(writer, assertion.SchemaVersion, assertion.Kind, assertion.ProductId,
            assertion.LicenseId, assertion.DeviceId, assertion.SessionId, assertion.Action,
            assertion.ContextHash, assertion.ChallengeId, assertion.Status,
            assertion.ServerTimeUnixSeconds, assertion.ExpiresAtUnixSeconds);
        writer.WriteString("catalogIdentity", assertion.CatalogIdentity);
        writer.WriteNumber("catalogSequence", assertion.CatalogSequence);
        writer.WritePropertyName("items");
        writer.WriteStartArray();
        foreach (var item in assertion.Items)
        {
            Validate(item);
            writer.WriteStartObject();
            writer.WriteString("itemId", item.ItemId);
            writer.WriteString("availability", item.Availability);
            if (item.Descriptor is null) writer.WriteNull("descriptor");
            else
            {
                writer.WritePropertyName("descriptor");
                WriteDescriptor(writer, item.Descriptor);
            }
            if (item.ReasonCode is null) writer.WriteNull("reasonCode");
            else writer.WriteString("reasonCode", item.ReasonCode);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        if (assertion.NextCursor is null) writer.WriteNull("nextCursor");
        else writer.WriteString("nextCursor", assertion.NextCursor);
        writer.WriteEndObject();
    }

    private static void Write(Utf8JsonWriter writer, DownloadGrantAssertion assertion)
    {
        writer.WriteStartObject();
        WriteCommon(writer, assertion.SchemaVersion, assertion.Kind, assertion.ProductId,
            assertion.LicenseId, assertion.DeviceId, assertion.SessionId, assertion.Action,
            assertion.ContextHash, assertion.ChallengeId, assertion.Status,
            assertion.ServerTimeUnixSeconds, assertion.ExpiresAtUnixSeconds);
        writer.WriteString("catalogIdentity", assertion.CatalogIdentity);
        writer.WriteString("itemId", assertion.ItemId);
        writer.WriteString("artifactId", assertion.ArtifactId);
        writer.WriteNumber("artifactVersion", assertion.ArtifactVersion);
        writer.WriteString("manifestIdentity", assertion.ManifestIdentity);
        writer.WriteString("descriptorHash", assertion.DescriptorHash);
        writer.WriteNumber("rangeStart", assertion.RangeStart);
        writer.WriteString("grantId", assertion.GrantId);
        writer.WriteString("contentPath", assertion.ContentPath);
        writer.WriteString("bearerToken", assertion.BearerToken);
        writer.WriteEndObject();
    }

    private static void WriteCommon(
        Utf8JsonWriter writer,
        int schemaVersion,
        string kind,
        string productId,
        string licenseId,
        string deviceId,
        string sessionId,
        string action,
        string contextHash,
        string challengeId,
        string status,
        long serverTimeUnixSeconds,
        long expiresAtUnixSeconds)
    {
        writer.WriteNumber("schemaVersion", schemaVersion);
        writer.WriteString("kind", kind);
        writer.WriteString("productId", productId);
        writer.WriteString("licenseId", licenseId);
        writer.WriteString("deviceId", deviceId);
        writer.WriteString("sessionId", sessionId);
        writer.WriteString("action", action);
        writer.WriteString("contextHash", contextHash);
        writer.WriteString("challengeId", challengeId);
        writer.WriteString("status", status);
        writer.WriteNumber("serverTimeUnixSeconds", serverTimeUnixSeconds);
        writer.WriteNumber("expiresAtUnixSeconds", expiresAtUnixSeconds);
    }

    private static void WriteDescriptor(Utf8JsonWriter writer, ContentArtifactDescriptor descriptor)
    {
        writer.WriteStartObject();
        WriteDescriptorProperties(writer, descriptor);
        writer.WriteEndObject();
    }

    private static void WriteDescriptorProperties(
        Utf8JsonWriter writer,
        ContentArtifactDescriptor descriptor)
    {
        writer.WriteString("artifactId", descriptor.ArtifactId);
        writer.WriteNumber("artifactVersion", descriptor.ArtifactVersion);
        writer.WriteNumber("contentLength", descriptor.ContentLength);
        writer.WriteString("sha256", descriptor.Sha256);
        writer.WriteString("safeFileName", descriptor.SafeFileName);
        writer.WriteString("fileExtension", descriptor.FileExtension);
        writer.WriteString("extractPolicy", descriptor.ExtractPolicy);
        writer.WriteString("manifestIdentity", descriptor.ManifestIdentity);
    }

    private static void Validate(AuthorizedCatalogItem item)
    {
        ValidateItemId(item.ItemId);
        if (item.Availability == ReadyAvailability && item.Descriptor is not null &&
            item.ReasonCode is null)
        {
            Validate(item.Descriptor);
            return;
        }
        if (item.Availability == MaintenanceAvailability && item.Descriptor is null &&
            item.ReasonCode == MaintenanceReasonCode)
            return;
        Invalid();
    }

    private static void CommonContext(
        int schemaVersion,
        string productId,
        string licenseId,
        string deviceId,
        string sessionId,
        string action,
        string expectedAction)
    {
        Protocol.RequireVersion(schemaVersion);
        Protocol.RequireProduct(productId);
        ValidateIdentifier(licenseId, 6, 64);
        ValidateHex(deviceId);
        ValidateHex(sessionId);
        if (action != expectedAction) Invalid();
    }

    private static void ValidateIdentifier(string value, int minimum, int maximum)
    {
        if (value.Length < minimum || value.Length > maximum || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))) Invalid();
    }

    private static void ValidateItemId(string value)
    {
        if (value.Length != 32 || value.Any(character =>
                !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))) Invalid();
    }

    private static void ValidateArtifactId(string value) => ValidateItemId(value);

    private static void ValidateHex(string value)
    {
        if (value.Length != 64 || value.Any(character =>
                !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))) Invalid();
    }

    private static void ValidateSafeFileName(string value)
    {
        if (value.Length is < 1 or > MaximumSafeFileNameLength ||
            Encoding.UTF8.GetByteCount(value) > MaximumSafeFileNameLength ||
            value is "." or ".." ||
            value[^1] is ' ' or '.' || value.Any(character =>
                char.IsControl(character) || char.IsSurrogate(character) ||
                character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            Invalid();
        var stem = value.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            IsReservedNumberedName(stem, "COM") || IsReservedNumberedName(stem, "LPT"))
            Invalid();
    }

    private static bool IsReservedNumberedName(string value, string prefix) =>
        value.Length == 4 && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        value[3] is >= '1' and <= '9';

    private static void ValidateExtension(string value)
    {
        if (value.Length is < 2 or > 11 || value[0] != '.' ||
            value.Skip(1).Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9'))) Invalid();
    }

    private static byte[] Canonical(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
            writer.Flush();
        }
        return stream.ToArray();
    }

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static bool FixedEquals(string left, string right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private static string InvalidString()
    {
        Invalid();
        return string.Empty;
    }

    private static void Invalid() =>
        throw new SuiteException(400, "CONTRACT_INVALID", "Request contract is invalid.");
}
