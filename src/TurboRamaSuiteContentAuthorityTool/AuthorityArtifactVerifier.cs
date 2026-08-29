using System.Security.Cryptography;

namespace TurboRamaSuiteContentAuthorityTool;

public static class AuthorityArtifactVerifier
{
    public static VerifiedContentAuthority Verify(
        AuthorityVerificationRequest request,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var envelopeHash = ContentAuthorityProtocol.RequireCanonicalHex(
            request.ExpectedEnvelopeSha256, "The expected envelope SHA-256");
        var issuerHash = ContentAuthorityProtocol.RequireCanonicalHex(
            request.ExpectedIssuerSpkiSha256,
            "The expected issuer SPKI SHA-256");
        var envelope = SecureFile.ReadBoundedRegularFile(
            request.EnvelopePath, 64, 32 * 1024, false);
        var issuerSpki = SecureFile.ReadBoundedRegularFile(
            request.IssuerPublicSpkiPath, 256, 4096, false);
        try
        {
            return VerifyBytes(
                envelope,
                issuerSpki,
                envelopeHash,
                issuerHash,
                (timeProvider ?? TimeProvider.System).GetUtcNow()
                    .ToUnixTimeSeconds());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(envelope);
            CryptographicOperations.ZeroMemory(issuerSpki);
        }
    }

    public static VerifiedContentAuthority VerifyBytes(
        ReadOnlySpan<byte> envelopeUtf8,
        ReadOnlySpan<byte> issuerSpki,
        string expectedEnvelopeSha256,
        string expectedIssuerSpkiSha256,
        long nowUnixSeconds)
    {
        var expectedEnvelopeHash = ContentAuthorityProtocol.RequireCanonicalHex(
            expectedEnvelopeSha256, "The expected envelope SHA-256");
        var expectedIssuerHash = ContentAuthorityProtocol.RequireCanonicalHex(
            expectedIssuerSpkiSha256, "The expected issuer SPKI SHA-256");
        var actualEnvelopeHash = ContentAuthorityProtocol.LowerSha256(envelopeUtf8);
        var actualIssuerHash = ContentAuthorityProtocol.LowerSha256(issuerSpki);
        if (!ContentAuthorityProtocol.FixedHexEquals(
                expectedEnvelopeHash, actualEnvelopeHash) ||
            !ContentAuthorityProtocol.FixedHexEquals(
                expectedIssuerHash, actualIssuerHash))
        {
            throw new AuthorityArtifactException(
                "The content-authority artifact does not match its independently approved hash.");
        }

        ContentAuthorityProtocol.ValidateRsaSpki(
            issuerSpki, "The offline issuer public key");
        var issuerKeyId = ContentAuthorityProtocol.KeyIdFromSpki(issuerSpki);
        var envelope = ContentAuthorityProtocol.ParseEnvelope(envelopeUtf8);
        if (envelope.SchemaVersion != ContentAuthorityProtocol.SchemaVersion ||
            !string.Equals(envelope.Algorithm, ContentAuthorityProtocol.Algorithm,
                StringComparison.Ordinal) ||
            !string.Equals(ContentAuthorityProtocol.RequireCanonicalHex(
                    envelope.KeyId, "The envelope issuer key identifier"),
                issuerKeyId, StringComparison.Ordinal))
        {
            throw new AuthorityArtifactException(
                "The content-authority envelope header is invalid.");
        }

        var canonicalEnvelope = ContentAuthorityProtocol.CanonicalEnvelope(envelope);
        try
        {
            if (!canonicalEnvelope.AsSpan().SequenceEqual(envelopeUtf8))
            {
                throw new AuthorityArtifactException(
                    "The content-authority envelope JSON is not canonical.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalEnvelope);
        }

        var payloadBytes = ContentAuthorityProtocol.DecodeCanonicalBase64(
            envelope.Payload, "The envelope payload", 64, 16 * 1024);
        var signature = ContentAuthorityProtocol.DecodeCanonicalBase64(
            envelope.Signature, "The envelope signature", 256, 512);
        byte[] signingMessage = [];
        try
        {
            var payload = ContentAuthorityProtocol.ParsePayload(payloadBytes);
            var canonicalPayload = ContentAuthorityProtocol.CanonicalPayload(payload);
            try
            {
                if (!canonicalPayload.AsSpan().SequenceEqual(payloadBytes))
                {
                    throw new AuthorityArtifactException(
                        "The content-authority payload JSON is not canonical.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonicalPayload);
            }

            ContentAuthorityProtocol.ValidatePayloadForTime(
                payload, nowUnixSeconds);
            if (ContentAuthorityProtocol.FixedHexEquals(
                    payload.ContentAssertionKeyId, issuerKeyId))
            {
                throw new AuthorityArtifactException(
                    "The offline issuer and online content assertion keys must be distinct.");
            }

            signingMessage = ContentAuthorityProtocol.BuildSigningMessage(payload);
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(issuerSpki, out var consumed);
                if (consumed != issuerSpki.Length ||
                    signature.Length != rsa.KeySize / 8 ||
                    !rsa.VerifyData(signingMessage, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                {
                    throw new AuthorityArtifactException(
                        "The content-authority signature is invalid.");
                }
            }
            catch (CryptographicException)
            {
                throw new AuthorityArtifactException(
                    "The content-authority signature could not be verified.");
            }

            return new VerifiedContentAuthority(
                payload,
                issuerKeyId,
                actualEnvelopeHash,
                actualIssuerHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
            CryptographicOperations.ZeroMemory(signature);
            if (signingMessage.Length != 0)
            {
                CryptographicOperations.ZeroMemory(signingMessage);
            }
        }
    }
}
