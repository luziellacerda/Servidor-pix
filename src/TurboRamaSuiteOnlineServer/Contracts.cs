using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TurboRamaSuiteOnlineServer;

public sealed record DeviceDescriptor(int SchemaVersion, string DeviceId, string BindingType,
    string Algorithm, string PublicKeySpki, string HardwareFingerprint, string AgentVersion);
public sealed record ActivationChallengeRequest(int SchemaVersion, string ProductId, string LicenseId,
    string ActivationCode, DeviceDescriptor Device);
public sealed record ChallengeRequest(int SchemaVersion, string ProductId, string LicenseId,
    string DeviceId, string SessionId, string Action, string ContextHash);
public sealed record ChallengeResponse(int SchemaVersion, string ChallengeId, string Nonce,
    long ExpiresAtUnixSeconds);
public sealed record ActivationProof(int SchemaVersion, string ProductId, string LicenseId,
    string ChallengeId, DeviceDescriptor Device, string Signature);
public sealed record OperationProof(int SchemaVersion, string ProductId, string LicenseId,
    string DeviceId, string SessionId, string Action, string ContextHash, string ChallengeId,
    string Signature);
public sealed record SessionContext(int SchemaVersion, string ProductId, string LicenseId,
    string DeviceId, string SessionId, string Action, string HardwareFingerprint,
    string ClientVersion);
public sealed record SessionProof(OperationProof Proof, SessionContext Context);
public sealed record SignedAssertionEnvelope(int SchemaVersion, string Kind, string Algorithm,
    string KeyId, string Payload, string Signature);
public sealed record ActivationChallengeAssertion(int SchemaVersion, string Kind, string ProductId,
    string LicenseId, string DeviceId, string Action, string ContextHash, string ChallengeId,
    string Nonce, string Status, long ServerTimeUnixSeconds, long ExpiresAtUnixSeconds);
public sealed record OperationChallengeAssertion(int SchemaVersion, string Kind, string ProductId,
    string LicenseId, string DeviceId, string SessionId, string Action, string ContextHash,
    string ChallengeId, string Nonce, string Status, long ServerTimeUnixSeconds,
    long ExpiresAtUnixSeconds);
public sealed record ActivationResultAssertion(int SchemaVersion, string Kind, string ProductId,
    string LicenseId, string DeviceId, string Action, string ContextHash, string ChallengeId,
    string Status, string BindingType, long ServerTimeUnixSeconds);
public sealed record SessionAssertion(int SchemaVersion, string Kind, string ProductId,
    string LicenseId, string DeviceId, string SessionId, string Action, string ContextHash,
    string ChallengeId, string Status, long ServerTimeUnixSeconds,
    long AuthorizedUntilUnixSeconds, int HeartbeatAfterSeconds);
public sealed record ErrorResponse(int SchemaVersion, string Code, string Message);

public static class StrictJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false
    };

    public static T Parse<T>(ReadOnlySpan<byte> body) where T : class
    {
        if (body.Length is 0 or > Protocol.MaximumBodyBytes)
            throw new SuiteException(413, "BODY_INVALID", "Request body is invalid.");
        try
        {
            using var document = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            RejectDuplicateProperties(document.RootElement);
            return JsonSerializer.Deserialize<T>(body, Options)
                ?? throw new JsonException("Null root.");
        }
        catch (JsonException ex)
        {
            throw new SuiteException(400, "JSON_INVALID", "Request JSON is invalid.", ex);
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate member.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }
}

public sealed class SuiteException : Exception
{
    public SuiteException(int statusCode, string code, string safeMessage, Exception? inner = null)
        : base(safeMessage, inner) => (StatusCode, Code) = (statusCode, code);
    public int StatusCode { get; }
    public string Code { get; }
}
