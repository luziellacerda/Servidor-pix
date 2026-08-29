using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurboRamaSuiteContentPublisher;

internal static partial class CatalogLoader
{
    public static async Task<PreparedCatalog> LoadAsync(
        PublisherOptions options,
        OriginPolicy originPolicy,
        CancellationToken cancellationToken)
    {
        ProtectedFile.Require(options.CatalogPath!, "CATALOG_FILE_INVALID");
        ProtectedFile.Require(options.VisualCatalogPath!, "VISUAL_CATALOG_FILE_INVALID", requirePrivateMode: false);
        var inventoryBytes = await ReadBoundedAsync(options.CatalogPath!, 32 * 1024 * 1024, cancellationToken);
        var visualBytes = await ReadBoundedAsync(options.VisualCatalogPath!, 32 * 1024 * 1024, cancellationToken);
        try
        {
            var inventorySha = Hex(SHA256.HashData(inventoryBytes));
            var visualSha = Hex(SHA256.HashData(visualBytes));
            ExpectedHash(options.ExpectedInventorySha256, inventorySha, "INVENTORY_DIGEST_MISMATCH");
            ExpectedHash(options.ExpectedVisualCatalogSha256, visualSha, "VISUAL_DIGEST_MISMATCH");

            var source = Deserialize(inventoryBytes, "INVENTORY_INVALID");
            EnsureVisualCatalogHasNoLocators(visualBytes);
            var visual = Deserialize(visualBytes, "VISUAL_CATALOG_INVALID");
            var sourceItems = Unique(source.Items, "INVENTORY_INVALID");
            var visualItems = Unique(visual.Items, "VISUAL_CATALOG_INVALID");
            if (visualItems.Count != options.ExpectedItemCount)
                throw new PublisherFailure("VISUAL_ITEM_COUNT_MISMATCH");
            if (visualItems.Values.Any(item => !string.IsNullOrWhiteSpace(item.DownloadUrl)))
                throw new PublisherFailure("VISUAL_CATALOG_CONTAINS_URL");

            var missing = visualItems.Keys.Except(sourceItems.Keys, StringComparer.Ordinal).Count();
            var extras = sourceItems.Keys.Except(visualItems.Keys, StringComparer.Ordinal).Count();
            if (missing != 0) throw new PublisherFailure("INVENTORY_MISSING_VISUAL_ITEMS");
            if (extras != options.ExpectedRejectedExtraCount)
                throw new PublisherFailure("REJECTED_EXTRA_COUNT_MISMATCH");

            var prepared = new List<PreparedItem>(visualItems.Count);
            foreach (var visualItem in visualItems.Values
                         .OrderBy(item => item.Order ?? int.MaxValue)
                         .ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                var sourceItem = sourceItems[visualItem.Id!];
                var validationCodes = new List<string>();
                Uri? uri = null;
                if (string.IsNullOrWhiteSpace(sourceItem.DownloadUrl) ||
                    !Uri.TryCreate(sourceItem.DownloadUrl, UriKind.Absolute, out uri))
                    validationCodes.Add("UPSTREAM_URL_INVALID");
                else
                {
                    try { originPolicy.ValidateUri(uri); }
                    catch (PublisherFailure ex) { validationCodes.Add(ex.Code); }
                }
                var extension = NormalizeExtension(sourceItem.DownloadFileExtension, uri);
                if (extension.Length == 0) validationCodes.Add("FILE_EXTENSION_INVALID");
                else if (!ContentSignatureValidator.IsSupportedExtension(extension))
                    validationCodes.Add("FILE_EXTENSION_UNSUPPORTED");
                var extract = VisualExtractPolicy(visualItem.Extract);
                if (!IsExtractPolicyCompatible(extract, extension))
                    validationCodes.Add("EXTRACT_POLICY_EXTENSION_MISMATCH");
                var title = NormalizeTitle(visualItem.Title);
                prepared.Add(new PreparedItem(
                    visualItem.Id!,
                    title,
                    visualItem.Order ?? prepared.Count,
                    uri,
                    extension,
                    extract,
                    validationCodes));
            }
            return new PreparedCatalog(inventorySha, visualSha, prepared, extras);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inventoryBytes);
            CryptographicOperations.ZeroMemory(visualBytes);
        }
    }

    private static CatalogDocument Deserialize(byte[] bytes, string code)
    {
        try
        {
            return JsonSerializer.Deserialize<CatalogDocument>(bytes, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip,
                MaxDepth = 16
            }) ?? throw new PublisherFailure(code);
        }
        catch (JsonException) { throw new PublisherFailure(code); }
    }

    internal static void EnsureVisualCatalogHasNoLocators(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
            InspectVisualValue(document.RootElement);
        }
        catch (PublisherFailure) { throw; }
        catch (JsonException) { throw new PublisherFailure("VISUAL_CATALOG_INVALID"); }
    }

    private static void InspectVisualValue(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (IsLocatorProperty(property.Name) && HasNonEmptyValue(property.Value))
                        throw new PublisherFailure("VISUAL_CATALOG_CONTAINS_URL");
                    InspectVisualValue(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()) InspectVisualValue(item);
                break;
            case JsonValueKind.String:
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    uri.Scheme is "http" or "https")
                    throw new PublisherFailure("VISUAL_CATALOG_CONTAINS_URL");
                break;
        }
    }

    private static bool IsLocatorProperty(string name) =>
        !name.Equals("downloadFileExtension", StringComparison.OrdinalIgnoreCase) &&
        (name.Contains("url", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("uri", StringComparison.OrdinalIgnoreCase) ||
         name.StartsWith("download", StringComparison.OrdinalIgnoreCase));

    private static bool HasNonEmptyValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => false,
        JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
        JsonValueKind.Array => value.GetArrayLength() != 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => true
    };

    private static Dictionary<string, CatalogItem> Unique(List<CatalogItem>? items, string code)
    {
        if (items is null) throw new PublisherFailure(code);
        var result = new Dictionary<string, CatalogItem>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item.Id is null || !ItemIdRegex().IsMatch(item.Id) || !result.TryAdd(item.Id, item))
                throw new PublisherFailure(code);
        }
        return result;
    }

    private static string NormalizeExtension(string? declared, Uri? uri)
    {
        var extension = declared?.Trim();
        if (string.IsNullOrEmpty(extension) && uri is not null) extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrEmpty(extension) || !ExtensionRegex().IsMatch(extension))
            return "";
        return extension.ToLowerInvariant();
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new PublisherFailure("VISUAL_TITLE_INVALID");
        var normalized = title.Normalize(NormalizationForm.FormKC).Trim();
        if (normalized.Length > 512 || normalized.Any(char.IsControl))
            throw new PublisherFailure("VISUAL_TITLE_INVALID");
        return normalized;
    }

    public static string SafeFileName(PreparedItem item)
    {
        var builder = new StringBuilder(item.Title.Length);
        foreach (var character in item.Title)
        {
            if (char.IsControl(character) || char.IsSurrogate(character) ||
                character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
                builder.Append('_');
            else
                builder.Append(character);
        }
        var stem = MultiSpaceRegex().Replace(builder.ToString(), " ").Trim(' ', '.');
        if (stem.Length == 0) stem = "item";
        var maximumStem = 180 - 1 - Math.Min(12, item.ItemId.Length) - item.DeclaredExtension.Length;
        if (stem.Length > maximumStem) stem = stem[..maximumStem].TrimEnd(' ', '.');
        var suffix = $"-{item.ItemId[..Math.Min(12, item.ItemId.Length)]}{item.DeclaredExtension}";
        while (stem.Length > 0 && Encoding.UTF8.GetByteCount(stem + suffix) > 180)
            stem = stem[..^1].TrimEnd(' ', '.');
        if (stem.Length == 0) stem = "item";
        var result = stem + suffix;
        if (!IsSafeFileName(result)) throw new PublisherFailure("SAFE_FILE_NAME_INVALID");
        return result;
    }

    internal static bool IsSafeFileName(string value)
    {
        if (value.Length is < 1 or > 180 || Encoding.UTF8.GetByteCount(value) > 180 ||
            value is "." or ".." || value[^1] is ' ' or '.' ||
            value.Any(character => char.IsControl(character) || char.IsSurrogate(character) ||
                                   character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            return false;
        var stem = value.Split('.')[0];
        return !(stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                 stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                 stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                 stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                 IsReservedNumberedName(stem, "COM") || IsReservedNumberedName(stem, "LPT"));
    }

    private static bool IsReservedNumberedName(string value, string prefix)
        => value.Length == 4 && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
           value[3] is >= '1' and <= '9';

    private static bool IsArchive(string extension)
        => extension is ".zip" or ".rar" or ".7z";

    internal static string VisualExtractPolicy(bool? extract) => extract is true
        ? "EXTRACT_ARCHIVE" : "NONE";

    internal static bool IsExtractPolicyCompatible(string policy, string extension) =>
        policy == "NONE" || policy == "EXTRACT_ARCHIVE" && IsArchive(extension);

    private static async Task<byte[]> ReadBoundedAsync(string path, int maximumBytes, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > maximumBytes)
                throw new PublisherFailure("CATALOG_FILE_INVALID");
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (PublisherFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new PublisherFailure("CATALOG_FILE_INVALID"); }
    }

    private static void ExpectedHash(string? expected, string actual, string code)
    {
        if (expected is null) return;
        var normalized = expected.Trim().ToLowerInvariant();
        if (!ShaRegex().IsMatch(normalized) || !FixedAscii(normalized, actual))
            throw new PublisherFailure(code);
    }

    internal static bool FixedAscii(string left, string right)
    {
        var a = Encoding.ASCII.GetBytes(left);
        var b = Encoding.ASCII.GetBytes(right);
        try { return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }

    internal static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemIdRegex();
    [GeneratedRegex("^\\.[A-Za-z0-9]{1,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionRegex();
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaRegex();
    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpaceRegex();
}
