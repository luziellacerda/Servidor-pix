using System.Text.Json.Serialization;

namespace TurboRamaSuiteContentPublisher;

internal sealed class CatalogDocument
{
    [JsonPropertyName("items")]
    public List<CatalogItem>? Items { get; init; }
}

internal sealed class CatalogItem
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    [JsonPropertyName("order")]
    public int? Order { get; init; }
    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; init; }
    [JsonPropertyName("downloadFileExtension")]
    public string? DownloadFileExtension { get; init; }
    [JsonPropertyName("extract")]
    public bool? Extract { get; init; }
}

internal sealed record PreparedCatalog(
    string InventorySha256,
    string VisualCatalogSha256,
    IReadOnlyList<PreparedItem> Items,
    int RejectedExtraCount);

internal sealed record PreparedItem(
    string ItemId,
    string Title,
    int DisplayOrder,
    Uri? UpstreamUri,
    string DeclaredExtension,
    string ExtractPolicy,
    IReadOnlyList<string> ValidationCodes);

internal sealed record VerifiedItem(
    string ItemId,
    int DisplayOrder,
    long ContentLength,
    string Sha256,
    string SafeFileName,
    string FileExtension,
    string ExtractPolicy,
    string? SourceEtag,
    string? SourceLastModified,
    string ContentType);

internal sealed record MaintenanceItem(
    string ItemId,
    int DisplayOrder,
    string ReasonCode);

internal sealed record PublicationPlan(
    IReadOnlyList<VerifiedItem> ReadyItems,
    IReadOnlyList<MaintenanceItem> MaintenanceItems);

internal sealed class VerificationJournal
{
    public int SchemaVersion { get; init; } = 2;
    public string InventorySha256 { get; init; } = "";
    public string VisualCatalogSha256 { get; init; } = "";
    public Dictionary<string, VerifiedItem> Items { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class KeyRingDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
    [JsonPropertyName("activeKeyVersion")]
    public int ActiveKeyVersion { get; init; }
    [JsonPropertyName("keys")]
    public List<KeyRingItem>? Keys { get; init; }
}

internal sealed class KeyRingItem
{
    [JsonPropertyName("version")]
    public int Version { get; init; }
    [JsonPropertyName("key")]
    public string? Key { get; init; }
}
