namespace TurboRamaSuiteOnlineServer;

public sealed record CatalogPageContext(
    int SchemaVersion,
    string ProductId,
    string LicenseId,
    string DeviceId,
    string SessionId,
    string Action,
    string Cursor,
    int PageSize);

public sealed record CatalogPageProof(OperationProof Proof, CatalogPageContext Context);

public sealed record ContentArtifactDescriptor(
    string ArtifactId,
    int ArtifactVersion,
    string SafeFileName,
    string FileExtension,
    string ExtractPolicy,
    string ManifestIdentity);

public sealed record AuthorizedCatalogItem(
    string ItemId,
    string Availability,
    ContentArtifactDescriptor? Descriptor,
    string? ReasonCode);

public sealed record CatalogPageAssertion(
    int SchemaVersion,
    string Kind,
    string ProductId,
    string LicenseId,
    string DeviceId,
    string SessionId,
    string Action,
    string ContextHash,
    string ChallengeId,
    string Status,
    long ServerTimeUnixSeconds,
    long ExpiresAtUnixSeconds,
    string CatalogIdentity,
    long CatalogSequence,
    IReadOnlyList<AuthorizedCatalogItem> Items,
    string? NextCursor);

public sealed record DownloadAuthorizationContext(
    int SchemaVersion,
    string ProductId,
    string LicenseId,
    string DeviceId,
    string SessionId,
    string Action,
    string CatalogIdentity,
    string ItemId,
    string ArtifactId,
    int ArtifactVersion,
    string ManifestIdentity,
    string DescriptorHash,
    long Offset,
    string SourceETag,
    string SourceLastModified);

public sealed record DownloadAuthorizationProof(
    OperationProof Proof,
    DownloadAuthorizationContext Context);

public sealed record DownloadGrantAssertion(
    int SchemaVersion,
    string Kind,
    string ProductId,
    string LicenseId,
    string DeviceId,
    string SessionId,
    string Action,
    string ContextHash,
    string ChallengeId,
    string Status,
    long ServerTimeUnixSeconds,
    long ExpiresAtUnixSeconds,
    string CatalogIdentity,
    string ItemId,
    string ArtifactId,
    int ArtifactVersion,
    string ManifestIdentity,
    string DescriptorHash,
    long RangeStart,
    string GrantId,
    string ContentPath,
    string BearerToken);
