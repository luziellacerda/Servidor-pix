using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteOnlineServer;

public static class ContentStartupIsolation
{
    public static bool TryInitialize(Action initialize)
    {
        ArgumentNullException.ThrowIfNull(initialize);
        try
        {
            initialize();
            return true;
        }
        catch (Exception)
        {
            // Content is an independently fail-closed subsystem. Startup configuration
            // failures must never make the already deployed licensing v1 unavailable.
            return false;
        }
    }
}

public static class ContentConnectionPolicy
{
    public static string RequireRole(string connectionString, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(requiredRole))
            throw new InvalidOperationException("Content database configuration is invalid.");
        Npgsql.NpgsqlConnectionStringBuilder parsed;
        try { parsed = new Npgsql.NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Content database configuration is invalid.", exception);
        }
        if (!string.Equals(parsed.Username, requiredRole, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Content database role does not match the service boundary.");
        return connectionString;
    }
}

public static class ContentProtectedSecret
{
    public static string ReadFile(string path, long maximumBytes)
    {
        if (maximumBytes < 1 || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Protected content secret is unavailable.");
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        info.Refresh();
        if (!info.Exists || (info.Attributes & (FileAttributes.Directory |
                                                FileAttributes.ReparsePoint)) != 0 ||
            info.LinkTarget is not null || info.Length is < 1 ||
            info.Length > maximumBytes)
            throw new InvalidOperationException("Protected content secret is unavailable.");
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(fullPath);
            // Production secrets may be root-owned and projected to the service
            // through a dedicated group (0440). Group write and all public bits
            // remain forbidden.
            var allowed = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                          UnixFileMode.GroupRead;
            if ((mode & ~allowed) != 0 || (mode & UnixFileMode.UserRead) == 0)
                throw new InvalidOperationException(
                    "Protected content secret permissions are unsafe.");
        }
        var value = File.ReadAllText(fullPath).Trim();
        if (value.Length == 0 || Encoding.UTF8.GetByteCount(value) > maximumBytes)
            throw new InvalidOperationException("Protected content secret is unavailable.");
        return value;
    }

    public static bool IsSystemdCredential(string path)
    {
        var directory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY");
        return !string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory) &&
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),
                StringComparison.Ordinal);
    }
}

public interface IContentAssertionSigner
{
    string KeyId { get; }
    SignedAssertionEnvelope Sign(object assertion);
}

public static class ContentAssertionKeyPolicy
{
    public static string ValidatePrivateKeyAndGetKeyId(RSA rsa)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        if (rsa.KeySize is < 2048 or > 4096)
            throw new InvalidOperationException("Content assertion key is invalid.");
        var probeHash = new byte[32];
        byte[]? signature = null;
        try
        {
            signature = rsa.SignHash(probeHash, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Content assertion key is invalid.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(probeHash);
            if (signature is not null) CryptographicOperations.ZeroMemory(signature);
        }
        var spki = rsa.ExportSubjectPublicKeyInfo();
        try
        {
            return Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();
        }
        finally { CryptographicOperations.ZeroMemory(spki); }
    }

    public static void RequireExpectedKeyId(
        string actualKeyId,
        string? expectedKeyId,
        bool production)
    {
        if (string.IsNullOrWhiteSpace(expectedKeyId))
        {
            if (production)
                throw new InvalidOperationException(
                    "Content assertion expected key id is required in Production.");
            return;
        }
        if (expectedKeyId.Length != 64 || expectedKeyId.Any(character =>
                !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidOperationException("Content assertion expected key id is invalid.");
        var actual = Encoding.ASCII.GetBytes(actualKeyId);
        var expected = Encoding.ASCII.GetBytes(expectedKeyId);
        try
        {
            if (actual.Length != expected.Length ||
                !CryptographicOperations.FixedTimeEquals(actual, expected))
                throw new InvalidOperationException("Content assertion key id does not match.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
            CryptographicOperations.ZeroMemory(expected);
        }
    }
}

public sealed class RsaContentAssertionSigner : IContentAssertionSigner, IDisposable
{
    private readonly RSA _rsa;

    public RsaContentAssertionSigner(RSA rsa)
    {
        _rsa = rsa;
        KeyId = ContentAssertionKeyPolicy.ValidatePrivateKeyAndGetKeyId(rsa);
    }

    public string KeyId { get; }

    public SignedAssertionEnvelope Sign(object assertion)
    {
        var payload = ContentProtocol.CanonicalAssertion(assertion);
        var domain = Encoding.ASCII.GetBytes(ContentProtocol.AssertionDomain(assertion));
        var message = new byte[domain.Length + payload.Length];
        domain.CopyTo(message, 0);
        payload.CopyTo(message, domain.Length);
        try
        {
            return new SignedAssertionEnvelope(
                Protocol.SchemaVersion,
                ContentProtocol.AssertionKind(assertion),
                Protocol.Algorithm,
                KeyId,
                Convert.ToBase64String(payload),
                Convert.ToBase64String(_rsa.SignData(message, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(message);
        }
    }

    public void Dispose() => _rsa.Dispose();
}

public sealed class ContentGrantTokenHasher : IDisposable
{
    private readonly byte[] _key;

    public ContentGrantTokenHasher(string base64Key)
    {
        try { _key = Convert.FromBase64String(base64Key); }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Content grant token key is invalid.", exception);
        }
        if (_key.Length != 32 || Convert.ToBase64String(_key) != base64Key)
        {
            CryptographicOperations.ZeroMemory(_key);
            throw new InvalidOperationException("Content grant token key is invalid.");
        }
    }

    public string Digest(string bearerToken)
    {
        var tokenBytes = Encoding.ASCII.GetBytes(bearerToken);
        try
        {
            return Convert.ToHexString(HMACSHA256.HashData(_key, tokenBytes))
                .ToLowerInvariant();
        }
        finally { CryptographicOperations.ZeroMemory(tokenBytes); }
    }

    public byte[] CreateGatewayReadinessProof(ReadOnlySpan<byte> nonce)
    {
        if (nonce.Length != 32)
            throw new ArgumentException("Readiness nonce is invalid.", nameof(nonce));
        var message = GatewayReadinessMessage(nonce);
        try { return HMACSHA256.HashData(_key, message); }
        finally { CryptographicOperations.ZeroMemory(message); }
    }

    public bool VerifyGatewayReadinessProof(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> proof)
    {
        if (nonce.Length != 32 || proof.Length != 32) return false;
        var expected = CreateGatewayReadinessProof(nonce);
        try { return CryptographicOperations.FixedTimeEquals(expected, proof); }
        finally { CryptographicOperations.ZeroMemory(expected); }
    }

    private static byte[] GatewayReadinessMessage(ReadOnlySpan<byte> nonce)
    {
        var domain = Encoding.ASCII.GetBytes(
            "TurboRamaSuiteContentGrantPepperProof/v1\0");
        var message = new byte[domain.Length + nonce.Length];
        domain.CopyTo(message, 0);
        nonce.CopyTo(message.AsSpan(domain.Length));
        CryptographicOperations.ZeroMemory(domain);
        return message;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}

public sealed class ContentService
{
    private const int AssertionLifetimeSeconds = 60;
    private const int GrantLifetimeSeconds = 60;

    private readonly ISuiteStore _suiteStore;
    private readonly IContentControlStore _contentStore;
    private readonly IContentAssertionSigner _signer;
    private readonly ContentGrantTokenHasher _tokenHasher;
    private readonly IContentGatewayPepperVerifier _gatewayPepperVerifier;
    private readonly TimeProvider _time;

    public ContentService(
        ISuiteStore suiteStore,
        IContentControlStore contentStore,
        IContentAssertionSigner signer,
        ContentGrantTokenHasher tokenHasher,
        IContentGatewayPepperVerifier gatewayPepperVerifier,
        TimeProvider time) =>
        (_suiteStore, _contentStore, _signer, _tokenHasher, _gatewayPepperVerifier, _time) =
        (suiteStore, contentStore, signer, tokenHasher, gatewayPepperVerifier, time);

    public async Task<SignedAssertionEnvelope> CatalogAsync(
        CatalogPageProof request,
        CancellationToken cancellationToken)
    {
        var hash = ContentProtocol.CatalogContextHash(request.Context);
        ValidateProofContext(request.Proof, request.Context.LicenseId,
            request.Context.DeviceId, request.Context.SessionId,
            request.Context.Action, hash);
        var challenge = await VerifyMachineProofAsync(request.Proof, hash,
            cancellationToken);
        await _gatewayPepperVerifier.RequireMatchingAsync(cancellationToken);
        var now = Now();
        var page = await _contentStore.ReadCatalogPageAsync(challenge,
            request.Context, now, cancellationToken);
        var items = page.Items.Select(item =>
            new AuthorizedCatalogItem(item.ItemId, item.Availability, item.Descriptor,
                item.ReasonCode)).ToArray();
        return _signer.Sign(new CatalogPageAssertion(
            Protocol.SchemaVersion,
            ContentProtocol.CatalogPageKind,
            Protocol.ProductId,
            request.Context.LicenseId,
            request.Context.DeviceId,
            request.Context.SessionId,
            ContentProtocol.CatalogReadAction,
            hash,
            challenge.ChallengeId,
            "AUTHORIZED",
            now,
            now + AssertionLifetimeSeconds,
            page.CatalogIdentity,
            page.CatalogSequence,
            items,
            page.NextCursor));
    }

    public async Task<SignedAssertionEnvelope> AuthorizeDownloadAsync(
        DownloadAuthorizationProof request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var hash = ContentProtocol.DownloadContextHash(request.Context);
        ValidateProofContext(request.Proof, request.Context.LicenseId,
            request.Context.DeviceId, request.Context.SessionId,
            request.Context.Action, hash);
        var challenge = await VerifyMachineProofAsync(request.Proof, hash,
            cancellationToken);
        await _gatewayPepperVerifier.RequireMatchingAsync(cancellationToken);
        var now = Now();
        var bearerBytes = RandomNumberGenerator.GetBytes(32);
        var grantBytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            var bearerToken = Base64Url(bearerBytes);
            var grantId = Convert.ToHexString(SHA256.HashData(grantBytes)).ToLowerInvariant();
            var draft = new ContentGrantDraft(grantId, _tokenHasher.Digest(bearerToken),
                now + GrantLifetimeSeconds, correlationId);
            var grant = await _contentStore.CreateDownloadGrantAsync(challenge,
                request.Context, draft, now, cancellationToken);
            return _signer.Sign(new DownloadGrantAssertion(
                Protocol.SchemaVersion,
                ContentProtocol.DownloadGrantKind,
                Protocol.ProductId,
                request.Context.LicenseId,
                request.Context.DeviceId,
                request.Context.SessionId,
                ContentProtocol.DownloadAuthorizeAction,
                hash,
                challenge.ChallengeId,
                "GRANTED",
                now,
                grant.ExpiresAt,
                request.Context.CatalogIdentity,
                request.Context.ItemId,
                request.Context.ArtifactId,
                request.Context.ArtifactVersion,
                request.Context.ManifestIdentity,
                request.Context.DescriptorHash,
                grant.RangeStart,
                grant.GrantId,
                "/v1/suite-content/artifacts/" + grant.GrantId,
                bearerToken));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bearerBytes);
            CryptographicOperations.ZeroMemory(grantBytes);
        }
    }

    private async Task<ChallengeRecord> VerifyMachineProofAsync(
        OperationProof proof,
        string contextHash,
        CancellationToken cancellationToken)
    {
        var now = Now();
        var challenge = await _suiteStore.FindChallengeAsync(proof.ChallengeId,
            proof.Action, now, cancellationToken) ??
            throw new SuiteException(409, "CHALLENGE_INVALID",
                "Challenge is invalid or expired.");
        Match(challenge, proof, contextHash);
        var device = await _suiteStore.FindDeviceAsync(proof.LicenseId,
            proof.DeviceId, cancellationToken) ??
            throw new SuiteException(403, "DEVICE_DENIED",
                "Device is not authorized.");
        if (device.Status != "ACTIVE")
            throw new SuiteException(403, "DEVICE_DENIED",
                "Device is not authorized.");
        var descriptor = new DeviceDescriptor(
            Protocol.SchemaVersion,
            device.DeviceId,
            device.BindingType,
            device.Algorithm,
            device.PublicKeySpki,
            device.HardwareFingerprint,
            "content-1");
        if (!Protocol.Verify(descriptor,
                new ChallengeResponse(Protocol.SchemaVersion, challenge.ChallengeId,
                    challenge.Nonce, challenge.ExpiresAt), proof.LicenseId,
                proof.SessionId, proof.Action, contextHash, proof.Signature))
            throw new SuiteException(403, "PROOF_INVALID",
                "Machine proof is invalid.");
        return challenge;
    }

    private static void ValidateProofContext(
        OperationProof proof,
        string licenseId,
        string deviceId,
        string sessionId,
        string action,
        string contextHash)
    {
        Protocol.RequireVersion(proof.SchemaVersion);
        Protocol.RequireProduct(proof.ProductId);
        if (proof.LicenseId != licenseId || proof.DeviceId != deviceId ||
            proof.SessionId != sessionId || proof.Action != action ||
            !Protocol.FixedEquals(proof.ContextHash, contextHash))
            throw new SuiteException(400, "CONTEXT_INVALID",
                "Context is invalid.");
    }

    private static void Match(
        ChallengeRecord challenge,
        OperationProof proof,
        string contextHash)
    {
        if (challenge.ProductId != Protocol.ProductId ||
            challenge.LicenseId != proof.LicenseId ||
            challenge.DeviceId != proof.DeviceId ||
            challenge.SessionId != proof.SessionId ||
            challenge.Action != proof.Action ||
            !Protocol.FixedEquals(challenge.ContextHash, contextHash))
            throw new SuiteException(409, "CHALLENGE_MISMATCH",
                "Challenge does not match the request.");
    }

    private static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private long Now() => _time.GetUtcNow().ToUnixTimeSeconds();
}
