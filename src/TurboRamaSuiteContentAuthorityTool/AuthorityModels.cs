namespace TurboRamaSuiteContentAuthorityTool;

public sealed record ContentAuthorityPayload(
    int SchemaVersion,
    string Kind,
    string ProductId,
    string BaseUrl,
    string ContentAssertionAlgorithm,
    string ContentAssertionKeyId,
    string ContentAssertionPublicKeySpki,
    string TlsServerSpkiSha256Current,
    string TlsServerSpkiSha256Next,
    long IssuedAtUnixSeconds,
    long ExpiresAtUnixSeconds);

public sealed record ContentAuthorityEnvelope(
    int SchemaVersion,
    string Algorithm,
    string KeyId,
    string Payload,
    string Signature);

public sealed record AuthorityGenerationRequest(
    string BaseUrl,
    string ContentAssertionPublicSpkiPath,
    string TlsServerSpkiSha256Current,
    string TlsServerSpkiSha256Next,
    long IssuedAtUnixSeconds,
    long ExpiresAtUnixSeconds,
    string IssuerPrivateKeyPemPath,
    string OutputDirectory);

public sealed record AuthorityVerificationRequest(
    string EnvelopePath,
    string IssuerPublicSpkiPath,
    string ExpectedEnvelopeSha256,
    string ExpectedIssuerSpkiSha256);

public sealed record AuthorityGenerationResult(
    bool ReusedExistingArtifact,
    string EnvelopeSha256,
    string IssuerSpkiSha256);

public sealed record VerifiedContentAuthority(
    ContentAuthorityPayload Payload,
    string IssuerKeyId,
    string EnvelopeSha256,
    string IssuerSpkiSha256);

public sealed class AuthorityArtifactException : Exception
{
    public AuthorityArtifactException(string message) : base(message)
    {
    }
}

public static class AuthorityArtifactNames
{
    public const string Envelope = "content-authority-envelope.json";
    public const string IssuerSpki = "content-authority-issuer.spki.der";
    public const string EnvelopeSha256 = "content-authority-envelope.json.sha256";
    public const string IssuerSpkiSha256 = "content-authority-issuer.spki.der.sha256";
}
