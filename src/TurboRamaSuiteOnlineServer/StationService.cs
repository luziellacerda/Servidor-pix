using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public sealed record StationActivationChallengeRequest(int SchemaVersion, string Domain,
    string ProductId, string ApplicationId, string DeviceId, string ClientVersion,
    string DeviceManufacturer, string DeviceModel, int AndroidSdk,
    string ActivationCode, string DevicePublicKey);
public sealed record StationSessionChallengeRequest(int SchemaVersion, string Domain,
    string ProductId, string ApplicationId, string DeviceId, string ClientVersion,
    string DeviceManufacturer, string DeviceModel, int AndroidSdk,
    string LicenseId);
public sealed record StationDownloadRequest(int SchemaVersion, string Domain,
    string ProductId, string ApplicationId, string DeviceId, string ClientVersion,
    string DeviceManufacturer, string DeviceModel, int AndroidSdk,
    string ItemId);

public sealed class StationService(PostgresStationStore store,
    StationResponseSigner signer, string activationPepper)
{
    public async Task<object> ActivationChallengeAsync(
        StationActivationChallengeRequest request, CancellationToken token)
    {
        StationProtocol.RequireIdentity(request.SchemaVersion, request.Domain,
            "request-activation-challenge", request.ProductId, request.ApplicationId,
            request.DeviceId);
        ValidateClient(request.ClientVersion, request.DeviceManufacturer,
            request.DeviceModel, request.AndroidSdk);
        _ = StationProtocol.DeviceKey(request.DeviceId, request.DevicePublicKey);
        var verifier = Verifier(request.ActivationCode);
        var license = await store.FindActivationAsync(verifier, token) ??
            throw new SuiteException(403, "STATION_ACTIVATION_INVALID",
                "Activation is not authorized.");
        var challenge = NewChallenge(license.LicenseId, request.DeviceId, "ACTIVATE",
            request.DevicePublicKey, verifier, license.ActivationGeneration,
            license.RevocationGeneration);
        await store.InsertChallengeAsync(challenge, token);
        return signer.Sign(new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "activation-challenge/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            deviceId = request.DeviceId, challengeId = challenge.ChallengeId,
            nonce = challenge.Nonce, expiresInSeconds = 60
        });
    }

    public async Task<object> CompleteActivationAsync(StationDeviceEnvelope envelope,
        CancellationToken token)
    {
        var untrusted = UntrustedPayload(envelope);
        var challengeId = StationProtocol.RequireString(untrusted.RootElement,
            "challengeId", 64);
        using (untrusted)
        {
            var challenge = await store.FindChallengeAsync(challengeId, "ACTIVATE",
                token) ?? throw new SuiteException(409, "STATION_CHALLENGE_INVALID",
                    "Challenge is invalid or expired.");
            using var payload = StationProtocol.VerifyDeviceEnvelope(envelope,
                challenge.DeviceId, challenge.PublicKeySpki!);
            var root = payload.RootElement;
            RequireSignedIdentity(root, "activate", challenge.DeviceId);
            Match(root, challenge);
            var code = StationProtocol.RequireString(root, "activationCode", 256);
            var verifier = Verifier(code);
            if (!FixedEquals(verifier, challenge.ActivationVerifier!))
                throw new SuiteException(403, "STATION_ACTIVATION_INVALID",
                    "Activation is not authorized.");
            var spki = StationProtocol.RequireString(root, "devicePublicKey", 6000);
            if (!FixedEquals(spki, challenge.PublicKeySpki!))
                throw new SuiteException(403, "STATION_PROOF_INVALID",
                    "Station proof is invalid.");
            var version = StationProtocol.RequireString(root, "clientVersion", 64);
            var manufacturer = StationProtocol.RequireString(root, "deviceManufacturer", 100);
            var model = StationProtocol.RequireString(root, "deviceModel", 100);
            var sdk = StationProtocol.RequireInt(root, "androidSdk", 1, 1000);
            ValidateClient(version, manufacturer, model, sdk);
            var licenseId = await store.ActivateAsync(challenge, verifier, manufacturer,
                model, sdk, version, token);
            return signer.Sign(new
            {
                schemaVersion = 1, domain = StationProtocol.Prefix + "activated/v1",
                productId = StationProtocol.Product, applicationId = StationProtocol.Application,
                licenseId, deviceId = challenge.DeviceId,
                challengeId = challenge.ChallengeId, nonce = challenge.Nonce
            });
        }
    }

    public async Task<object> SessionChallengeAsync(
        StationSessionChallengeRequest request, CancellationToken token)
    {
        StationProtocol.RequireIdentity(request.SchemaVersion, request.Domain,
            "request-session-challenge", request.ProductId, request.ApplicationId,
            request.DeviceId);
        ValidateClient(request.ClientVersion, request.DeviceManufacturer,
            request.DeviceModel, request.AndroidSdk);
        ValidateLicenseId(request.LicenseId);
        var device = await store.FindDeviceAsync(request.LicenseId, request.DeviceId,
            token) ?? throw new SuiteException(403, "STATION_DEVICE_DENIED",
                "Device is not authorized.");
        var generation = await store.GetRevocationGenerationAsync(request.LicenseId,
            token);
        var challenge = NewChallenge(request.LicenseId, request.DeviceId, "SESSION",
            null, null, null, generation);
        await store.InsertChallengeAsync(challenge, token);
        return signer.Sign(new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "session-challenge/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            licenseId = device.LicenseId, deviceId = device.DeviceId,
            challengeId = challenge.ChallengeId, nonce = challenge.Nonce,
            expiresInSeconds = 60
        });
    }

    public async Task<object> OpenSessionAsync(StationDeviceEnvelope envelope,
        CancellationToken token)
    {
        using var untrusted = UntrustedPayload(envelope);
        var challengeId = StationProtocol.RequireString(untrusted.RootElement,
            "challengeId", 64);
        var challenge = await store.FindChallengeAsync(challengeId, "SESSION", token) ??
            throw new SuiteException(409, "STATION_CHALLENGE_INVALID",
                "Challenge is invalid or expired.");
        var device = await store.FindDeviceAsync(challenge.LicenseId, challenge.DeviceId,
            token) ?? throw new SuiteException(403, "STATION_DEVICE_DENIED",
                "Device is not authorized.");
        using var payload = StationProtocol.VerifyDeviceEnvelope(envelope,
            challenge.DeviceId, device.PublicKeySpki);
        var root = payload.RootElement;
        RequireSignedIdentity(root, "open-session", challenge.DeviceId);
        Match(root, challenge);
        if (!FixedEquals(StationProtocol.RequireString(root, "licenseId", 64),
                challenge.LicenseId))
            throw new SuiteException(403, "STATION_LICENSE_DENIED",
                "Station license is not active.");
        ValidateClient(StationProtocol.RequireString(root, "clientVersion", 64),
            StationProtocol.RequireString(root, "deviceManufacturer", 100),
            StationProtocol.RequireString(root, "deviceModel", 100),
            StationProtocol.RequireInt(root, "androidSdk", 1, 1000));
        var sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var accessToken = StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        await store.OpenSessionAsync(challenge, accessToken, sessionId, token);
        return signer.Sign(new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "session/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            licenseId = challenge.LicenseId, deviceId = challenge.DeviceId,
            challengeId = challenge.ChallengeId, nonce = challenge.Nonce,
            sessionId, accessToken, expiresInSeconds = 180
        });
    }

    public async Task<object> ProfileAsync(string bearer, CancellationToken token)
    {
        var session = await Session(bearer, token);
        if (session.DisplayName is null || session.ProfileVersion is null)
            throw new SuiteException(503, "STATION_PROFILE_NOT_READY",
                "Buyer profile is not ready.");
        return signer.Sign(new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "profile/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            licenseId = session.LicenseId, deviceId = session.DeviceId,
            sessionId = session.SessionId,
            displayName = session.DisplayName.Length <= 80
                ? session.DisplayName : session.DisplayName[..80],
            profileVersion = session.ProfileVersion.Value
        });
    }

    public async Task<StationSession> Session(string bearer, CancellationToken token)
    {
        if (!StationProtocol.IsCanonicalBase64Url(bearer, 32))
            throw new SuiteException(401, "STATION_SESSION_INVALID",
                "Station session is invalid.");
        return await store.FindSessionAsync(bearer, token) ??
            throw new SuiteException(401, "STATION_SESSION_INVALID",
                "Station session is invalid.");
    }

    private string Verifier(string code)
    {
        if (!StationProtocol.IsCanonicalBase64Url(code, 32))
            throw new SuiteException(403, "STATION_ACTIVATION_INVALID",
                "Activation is not authorized.");
        return ActivationCodes.Verify(activationPepper, code);
    }

    private StationChallenge NewChallenge(string licenseId, string deviceId,
        string action, string? spki, string? verifier, long? activationGeneration,
        long revocationGeneration) => new(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            licenseId, deviceId, action,
            StationProtocol.Encode(RandomNumberGenerator.GetBytes(32)), spki,
            verifier, activationGeneration, revocationGeneration);

    private static JsonDocument UntrustedPayload(StationDeviceEnvelope envelope)
    {
        var bytes = StationProtocol.Decode(envelope.Payload, StationProtocol.MaximumBodyBytes);
        try
        {
            var json = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                json.Dispose();
                throw new SuiteException(400, "STATION_JSON_INVALID",
                    "Station JSON is invalid.");
            }
            StationProtocol.RejectDuplicates(json.RootElement);
            return json;
        }
        catch (JsonException ex)
        { throw new SuiteException(400, "STATION_JSON_INVALID", "Station JSON is invalid.", ex); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void RequireSignedIdentity(JsonElement value, string domain,
        string deviceId)
    {
        StationProtocol.RequireIdentity(StationProtocol.RequireInt(value,
                "schemaVersion", 1, 1),
            StationProtocol.RequireString(value, "domain", 96), domain,
            StationProtocol.RequireString(value, "productId", 64),
            StationProtocol.RequireString(value, "applicationId", 64),
            StationProtocol.RequireString(value, "deviceId", 64));
        if (!FixedEquals(StationProtocol.RequireString(value, "deviceId", 64), deviceId))
            throw new SuiteException(403, "STATION_DEVICE_DENIED",
                "Device is not authorized.");
    }

    private static void Match(JsonElement value, StationChallenge challenge)
    {
        if (!FixedEquals(StationProtocol.RequireString(value, "challengeId", 64),
                challenge.ChallengeId) ||
            !FixedEquals(StationProtocol.RequireString(value, "nonce", 64),
                challenge.Nonce))
            throw new SuiteException(409, "STATION_CHALLENGE_MISMATCH",
                "Challenge does not match the request.");
    }

    private static void ValidateClient(string version, string manufacturer,
        string model, int sdk)
    {
        if (version.Length is 0 or > 64 || manufacturer.Length is 0 or > 100 ||
            model.Length is 0 or > 100 || sdk is < 1 or > 1000 ||
            version.Any(char.IsControl) || manufacturer.Any(char.IsControl) ||
            model.Any(char.IsControl))
            throw new SuiteException(400, "STATION_CLIENT_INVALID",
                "Station client metadata is invalid.");
    }

    private static void ValidateLicenseId(string value)
    {
        if (value.Length is < 6 or > 64 || value.Any(c =>
                !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new SuiteException(400, "STATION_LICENSE_INVALID",
                "Station license is invalid.");
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        try { return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }
}
