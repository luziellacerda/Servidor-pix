using System.Security.Cryptography;

namespace TurboRamaSuiteContentAuthorityTool;

public static class AuthorityArtifactGenerator
{
    public static AuthorityGenerationResult Generate(
        AuthorityGenerationRequest request,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow()
            .ToUnixTimeSeconds();
        var issuerPrivateBytes = SecureFile.ReadBoundedRegularFile(
            request.IssuerPrivateKeyPemPath,
            64,
            64 * 1024,
            requirePrivatePermissions: true);
        try
        {
            var contentSpki = SecureFile.ReadBoundedRegularFile(
                request.ContentAssertionPublicSpkiPath,
                256,
                4096,
                requirePrivatePermissions: false);
            try
            {
                ContentAuthorityProtocol.ValidateRsaSpki(
                    contentSpki, "The online content assertion public key");
                using var issuer = SecureFile.ImportPrivateRsaPem(issuerPrivateBytes);
                var issuerSpki = issuer.ExportSubjectPublicKeyInfo();
                try
                {
                    ContentAuthorityProtocol.ValidateRsaSpki(
                        issuerSpki, "The offline issuer public key");
                    var issuerKeyId = ContentAuthorityProtocol.KeyIdFromSpki(issuerSpki);
                    var contentKeyId = ContentAuthorityProtocol.KeyIdFromSpki(contentSpki);
                    if (ContentAuthorityProtocol.FixedHexEquals(
                            issuerKeyId, contentKeyId))
                    {
                        throw new AuthorityArtifactException(
                            "The offline issuer and online content assertion keys must be distinct.");
                    }

                    var payload = new ContentAuthorityPayload(
                        ContentAuthorityProtocol.SchemaVersion,
                        ContentAuthorityProtocol.Kind,
                        ContentAuthorityProtocol.ProductId,
                        request.BaseUrl,
                        ContentAuthorityProtocol.Algorithm,
                        contentKeyId,
                        Convert.ToBase64String(contentSpki),
                        request.TlsServerSpkiSha256Current,
                        request.TlsServerSpkiSha256Next,
                        request.IssuedAtUnixSeconds,
                        request.ExpiresAtUnixSeconds);
                    ContentAuthorityProtocol.ValidatePayloadForTime(payload, now);
                    return GenerateValidated(
                        request.OutputDirectory,
                        payload,
                        issuer,
                        issuerSpki,
                        now);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(issuerSpki);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(contentSpki);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(issuerPrivateBytes);
        }
    }

    private static AuthorityGenerationResult GenerateValidated(
        string outputDirectory,
        ContentAuthorityPayload payload,
        RSA issuer,
        ReadOnlySpan<byte> issuerSpki,
        long nowUnixSeconds)
    {
        var outputPath = ValidateOutputPath(outputDirectory);
        var canonicalPayload = ContentAuthorityProtocol.CanonicalPayload(payload);
        try
        {
            if (Directory.Exists(outputPath))
            {
                return VerifyExistingOutput(
                    outputPath,
                    canonicalPayload,
                    issuerSpki,
                    nowUnixSeconds);
            }

            var message = ContentAuthorityProtocol.BuildSigningMessage(payload);
            var signature = Array.Empty<byte>();
            var envelopeBytes = Array.Empty<byte>();
            try
            {
                signature = issuer.SignData(
                    message,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss);
                var envelope = new ContentAuthorityEnvelope(
                    ContentAuthorityProtocol.SchemaVersion,
                    ContentAuthorityProtocol.Algorithm,
                    ContentAuthorityProtocol.KeyIdFromSpki(issuerSpki),
                    Convert.ToBase64String(canonicalPayload),
                    Convert.ToBase64String(signature));
                envelopeBytes = ContentAuthorityProtocol.CanonicalEnvelope(envelope);
                var envelopeHash = ContentAuthorityProtocol.LowerSha256(envelopeBytes);
                var issuerHash = ContentAuthorityProtocol.LowerSha256(issuerSpki);
                AuthorityArtifactVerifier.VerifyBytes(
                    envelopeBytes,
                    issuerSpki,
                    envelopeHash,
                    issuerHash,
                    nowUnixSeconds);
                WriteArtifactSet(
                    outputPath,
                    envelopeBytes,
                    issuerSpki,
                    envelopeHash,
                    issuerHash);
                return new AuthorityGenerationResult(
                    false, envelopeHash, issuerHash);
            }
            catch (CryptographicException)
            {
                throw new AuthorityArtifactException(
                    "The offline issuer could not create the RSA-PSS signature.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(message);
                CryptographicOperations.ZeroMemory(signature);
                CryptographicOperations.ZeroMemory(envelopeBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalPayload);
        }
    }

    private static AuthorityGenerationResult VerifyExistingOutput(
        string outputPath,
        ReadOnlySpan<byte> expectedCanonicalPayload,
        ReadOnlySpan<byte> expectedIssuerSpki,
        long nowUnixSeconds)
    {
        var envelopePath = Path.Combine(outputPath, AuthorityArtifactNames.Envelope);
        var issuerPath = Path.Combine(outputPath, AuthorityArtifactNames.IssuerSpki);
        var envelopeHashPath = Path.Combine(
            outputPath, AuthorityArtifactNames.EnvelopeSha256);
        var issuerHashPath = Path.Combine(
            outputPath, AuthorityArtifactNames.IssuerSpkiSha256);
        SecureFile.RequireOwnerOnlyArtifactSet(outputPath);
        var envelope = SecureFile.ReadBoundedRegularFile(
            envelopePath, 64, 32 * 1024, false);
        var issuerSpki = SecureFile.ReadBoundedRegularFile(
            issuerPath, 256, 4096, false);
        var envelopeSidecar = SecureFile.ReadBoundedRegularFile(
            envelopeHashPath, 68, 512, false);
        var issuerSidecar = SecureFile.ReadBoundedRegularFile(
            issuerHashPath, 68, 512, false);
        try
        {
            if (!issuerSpki.AsSpan().SequenceEqual(expectedIssuerSpki))
            {
                throw new AuthorityArtifactException(
                    "The existing output belongs to a different offline issuer.");
            }

            var envelopeHash = ContentAuthorityProtocol.LowerSha256(envelope);
            var issuerHash = ContentAuthorityProtocol.LowerSha256(issuerSpki);
            RequireExactSidecar(
                envelopeSidecar,
                envelopeHash,
                AuthorityArtifactNames.Envelope);
            RequireExactSidecar(
                issuerSidecar,
                issuerHash,
                AuthorityArtifactNames.IssuerSpki);
            var verified = AuthorityArtifactVerifier.VerifyBytes(
                envelope,
                issuerSpki,
                envelopeHash,
                issuerHash,
                nowUnixSeconds);
            var existingCanonicalPayload = ContentAuthorityProtocol.CanonicalPayload(
                verified.Payload);
            try
            {
                if (!existingCanonicalPayload.AsSpan()
                    .SequenceEqual(expectedCanonicalPayload))
                {
                    throw new AuthorityArtifactException(
                        "The existing output has different content-authority inputs.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(existingCanonicalPayload);
            }

            return new AuthorityGenerationResult(true, envelopeHash, issuerHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            CryptographicOperations.ZeroMemory(issuerSpki);
            CryptographicOperations.ZeroMemory(envelopeSidecar);
            CryptographicOperations.ZeroMemory(issuerSidecar);
        }
    }

    private static void WriteArtifactSet(
        string outputPath,
        ReadOnlySpan<byte> envelope,
        ReadOnlySpan<byte> issuerSpki,
        string envelopeHash,
        string issuerHash)
    {
        var parent = Directory.GetParent(outputPath) ??
            throw new AuthorityArtifactException(
                "The output directory must have an existing parent directory.");
        var leaf = Path.GetFileName(outputPath);
        var stagingPath = Path.Combine(
            parent.FullName,
            $".{leaf}.staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingPath);
            SecureFile.SetPrivateDirectoryMode(stagingPath);
            SecureFile.WriteNewFile(
                Path.Combine(stagingPath, AuthorityArtifactNames.Envelope),
                envelope);
            SecureFile.WriteNewFile(
                Path.Combine(stagingPath, AuthorityArtifactNames.IssuerSpki),
                issuerSpki);
            WriteSidecar(
                stagingPath,
                AuthorityArtifactNames.EnvelopeSha256,
                envelopeHash,
                AuthorityArtifactNames.Envelope);
            WriteSidecar(
                stagingPath,
                AuthorityArtifactNames.IssuerSpkiSha256,
                issuerHash,
                AuthorityArtifactNames.IssuerSpki);
            SecureFile.RequireOwnerOnlyArtifactSet(stagingPath);
            Directory.Move(stagingPath, outputPath);
        }
        catch (AuthorityArtifactException)
        {
            CleanupStaging(stagingPath);
            throw;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException)
        {
            CleanupStaging(stagingPath);
            throw new AuthorityArtifactException(
                "The content-authority artifact set could not be committed atomically.");
        }
    }

    private static string ValidateOutputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AuthorityArtifactException("The output directory is required.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or
            NotSupportedException or PathTooLongException)
        {
            throw new AuthorityArtifactException("The output path is invalid.");
        }

        var parent = Directory.GetParent(fullPath);
        if (parent is null ||
            !parent.Exists ||
            parent.LinkTarget is not null ||
            parent.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
            SecureFile.HasLinkedDirectoryAncestor(parent) ||
            File.Exists(fullPath) ||
            SecureFile.IsInsideGitWorkTree(parent.FullName))
        {
            throw new AuthorityArtifactException(
                "The output must be a new directory under an existing non-Git regular directory.");
        }

        if (Directory.Exists(fullPath))
        {
            var information = new DirectoryInfo(fullPath);
            if (information.LinkTarget is not null ||
                information.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new AuthorityArtifactException(
                    "The existing output directory must not be a link.");
            }
        }

        return fullPath;
    }

    private static void WriteSidecar(
        string directory,
        string sidecarName,
        string hash,
        string artifactName)
    {
        var bytes = SecureFile.Utf8WithoutBom($"{hash}  {artifactName}\n");
        try
        {
            SecureFile.WriteNewFile(Path.Combine(directory, sidecarName), bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void RequireExactSidecar(
        ReadOnlySpan<byte> sidecar,
        string hash,
        string artifactName)
    {
        var expected = SecureFile.Utf8WithoutBom($"{hash}  {artifactName}\n");
        try
        {
            if (!sidecar.SequenceEqual(expected))
            {
                throw new AuthorityArtifactException(
                    "An existing SHA-256 sidecar is invalid.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    private static void CleanupStaging(string stagingPath)
    {
        try
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }
        }
        catch
        {
            // The failed staging directory never becomes an approved artifact set.
        }
    }
}
