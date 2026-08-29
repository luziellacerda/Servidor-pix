using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteContentPublisher;

internal static class SelfTest
{
    public static int Run()
    {
        if (OriginPolicy.IsPublic(IPAddress.Parse("127.0.0.1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("10.10.10.10")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("169.254.1.1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("64:ff9b::c000:201")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("64:ff9b:1::c000:201")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("192.0.2.1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2002:7f00:1::")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:0000::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:0002::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:0020::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:db8::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("3fff::1")) ||
            !OriginPolicy.IsPublic(IPAddress.Parse("1.1.1.1")) ||
            !OriginPolicy.IsPublic(IPAddress.Parse("2606:4700:4700::1111")))
            throw new InvalidOperationException("address policy failed");
        ConnectionSecret.RequirePublisherRole(
            "Host=/var/run/postgresql;Database=turborama;Username=turborama-suite-publisher");
        try
        {
            ConnectionSecret.RequirePublisherRole(
                "Host=/var/run/postgresql;Database=turborama;Username=postgres");
            throw new InvalidOperationException("publisher role boundary failed");
        }
        catch (PublisherFailure failure) when (failure.Code == "CONNECTION_ROLE_INVALID") { }
        CatalogLoader.EnsureVisualCatalogHasNoLocators(Encoding.UTF8.GetBytes(
            "{\"items\":[{\"downloadUrl\":null,\"downloadFileExtension\":\".zip\",\"assetPath\":\"covers/game.webp\"}]}"));
        foreach (var locatorJson in new[]
                 {
                     "{\"items\":[{\"DownloadURL\":\"https://private.example/file\"}]}",
                     "{\"items\":[{\"metadata\":{\"originUri\":\"hidden\"}}]}",
                     "{\"items\":[{\"assetPath\":\"https://private.example/cover\"}]}"
                 })
        {
            try
            {
                CatalogLoader.EnsureVisualCatalogHasNoLocators(
                    Encoding.UTF8.GetBytes(locatorJson));
                throw new InvalidOperationException("visual locator leak gate failed");
            }
            catch (PublisherFailure failure) when (
                failure.Code == "VISUAL_CATALOG_CONTAINS_URL")
            {
            }
        }

        var testOriginPolicy = OriginPolicy.ForSelfTest("example.invalid");
        using (var tlsHandler = testOriginPolicy.CreateHandler())
        {
            if (tlsHandler.SslOptions.EnabledSslProtocols !=
                    (System.Security.Authentication.SslProtocols.Tls12 |
                     System.Security.Authentication.SslProtocols.Tls13) ||
                tlsHandler.SslOptions.CertificateRevocationCheckMode !=
                    System.Security.Cryptography.X509Certificates.X509RevocationMode.Online)
                throw new InvalidOperationException("origin TLS policy failed");
        }
        const string urlPrefix = "https://example.invalid/";
        var maximumUrl = new Uri(urlPrefix +
            new string('a', OriginPolicy.MaximumUrlUtf8Bytes - urlPrefix.Length));
        testOriginPolicy.ValidateUri(maximumUrl);
        try
        {
            testOriginPolicy.ValidateUri(new Uri(maximumUrl.AbsoluteUri + "a"));
            throw new InvalidOperationException("URL UTF-8 maximum was not enforced");
        }
        catch (PublisherFailure failure) when (failure.Code == "UPSTREAM_URL_POLICY_DENIED") { }

        var prepared = new PreparedItem(
            "0123456789abcdef0123456789abcdef", "Example / Game", 1,
            new Uri("https://example.com/private/object?opaque=1"), ".zip", "NONE", []);
        if (CatalogLoader.VisualExtractPolicy(false) != "NONE" ||
            !CatalogLoader.IsExtractPolicyCompatible("NONE", ".zip") ||
            CatalogLoader.IsExtractPolicyCompatible("EXTRACT_ARCHIVE", ".iso"))
            throw new InvalidOperationException("visual extract policy authority failed");
        if (!ContentArtifactLimits.IsSupportedLength(512L * 1024 * 1024 * 1024) ||
            ContentArtifactLimits.IsSupportedLength(512L * 1024 * 1024 * 1024 + 1))
            throw new InvalidOperationException("maximum content length policy failed");
        var verified = new VerifiedItem(prepared.ItemId, 1, 1234,
            new string('a', 64), CatalogLoader.SafeFileName(prepared), ".zip", "NONE", "\"etag\"",
            "Sat, 29 Aug 2026 00:00:00 GMT", "application/octet-stream");
        var strongProbe = new OriginMetadata(1234, "", "\"etag\"",
            "Sat, 29 Aug 2026 00:00:00 GMT", "application/octet-stream");
        if (!JournalStore.OriginIdentityMatches(verified, strongProbe) ||
            JournalStore.OriginIdentityMatches(verified,
                strongProbe with { Etag = "W/\"etag\"" }) ||
            JournalStore.OriginIdentityMatches(verified,
                strongProbe with { Etag = null }) ||
            JournalStore.OriginIdentityMatches(verified,
                strongProbe with { Etag = "\"changed\"" }))
            throw new InvalidOperationException(
                "journal reuse must require an identical strong ETag");
        var fullMetadata = strongProbe with
        {
            Sha256 = new string('a', 64),
            LastModified = "Sun, 30 Aug 2026 00:00:00 GMT"
        };
        var bodyBound = OriginVerifier.BindFullResponseIdentity(strongProbe,
            fullMetadata);
        var missingOnFull = OriginVerifier.BindFullResponseIdentity(strongProbe,
            fullMetadata with { Etag = null });
        var noFullValidators = OriginVerifier.BindFullResponseIdentity(strongProbe,
            fullMetadata with { Etag = null, LastModified = null });
        if (bodyBound.Etag != "\"etag\"" || missingOnFull.Etag is not null ||
            missingOnFull.LastModified != fullMetadata.LastModified ||
            noFullValidators.LastModified is not null ||
            JournalStore.OriginIdentityMatches(verified,
                missingOnFull with { Sha256 = string.Empty }))
            throw new InvalidOperationException(
                "full response must independently bind a reusable strong ETag");
        try
        {
            _ = OriginVerifier.BindFullResponseIdentity(strongProbe,
                fullMetadata with { Etag = "\"changed\"" });
            throw new InvalidOperationException("changed full response ETag accepted");
        }
        catch (PublisherFailure failure) when (failure.Code == "SOURCE_CHANGED") { }
        var hostile = prepared with { Title = "CON<>:\"/\\|?*\ud800" };
        var hostileName = CatalogLoader.SafeFileName(hostile);
        if (!CatalogLoader.IsSafeFileName(hostileName) || CatalogLoader.IsSafeFileName("CON.txt") ||
            CatalogLoader.IsSafeFileName("LPT9.zip") || hostileName.Length > 180)
            throw new InvalidOperationException("Windows filename policy failed");
        var unicodeName = CatalogLoader.SafeFileName(prepared with { Title = new string('\u00e1', 400) });
        if (Encoding.UTF8.GetByteCount(unicodeName) > 180)
            throw new InvalidOperationException("UTF-8 filename limit failed");
        var catalogIdentity = new string('b', 64);
        var firstDescriptor = CanonicalIdentity.DescriptorHash(verified, catalogIdentity);
        var secondDescriptor = CanonicalIdentity.DescriptorHash(verified, catalogIdentity);
        if (firstDescriptor.Length != 64 || firstDescriptor != secondDescriptor)
            throw new InvalidOperationException("descriptor canonicalization failed");

        var unavailableSource = prepared with
        {
            ItemId = "fedcba9876543210fedcba9876543210",
            DisplayOrder = 2,
            UpstreamUri = null
        };
        var publicationCatalog = new PreparedCatalog(new string('c', 64), new string('d', 64),
            [prepared, unavailableSource], 2);
        var unavailable = new MaintenanceItem(unavailableSource.ItemId, 2,
            CatalogPublisher.MaintenanceReason);
        var firstCatalog = CanonicalIdentity.CatalogIdentity(publicationCatalog,
            [verified], [unavailable], 1, new string('e', 64), new string('f', 64));
        var repeatedCatalog = CanonicalIdentity.CatalogIdentity(publicationCatalog,
            [verified], [unavailable], 1, new string('e', 64), new string('f', 64));
        var changedCatalog = CanonicalIdentity.CatalogIdentity(publicationCatalog, [verified],
            [unavailable with { ReasonCode = "DIFFERENT_REASON" }], 1,
            new string('e', 64), new string('f', 64));
        var changedDeployment = CanonicalIdentity.CatalogIdentity(publicationCatalog,
            [verified], [unavailable], 2, new string('a', 64), new string('f', 64));
        if (firstCatalog.Length != 64 || firstCatalog != repeatedCatalog ||
            firstCatalog == changedCatalog || firstCatalog == changedDeployment)
            throw new InvalidOperationException("catalog availability canonicalization failed");
        if (OriginVerifier.ClassifyStatus(404) != "ORIGIN_TERMINAL_UNAVAILABLE" ||
            OriginVerifier.ClassifyStatus(416) != "ORIGIN_TERMINAL_EMPTY" ||
            OriginVerifier.ClassifyStatus(503) != "ORIGIN_RETRYABLE_STATUS" ||
            !CatalogPublisher.IsMaintenanceCode("FILE_EXTENSION_INVALID") ||
            !CatalogPublisher.IsMaintenanceCode("ORIGIN_REQUEST_FAILED") ||
            !CatalogPublisher.IsMaintenanceCode("ORIGIN_TOTAL_TIMEOUT") ||
            CatalogPublisher.IsMaintenanceCode("UPSTREAM_HOST_DENIED") ||
            CatalogPublisher.IsMaintenanceCode("ORIGIN_REDIRECT_DENIED") ||
            CatalogPublisher.IsMaintenanceCode("ORIGIN_CONTENT_ENCODING_DENIED") ||
            CatalogPublisher.IsMaintenanceCode("ORIGIN_TLS_SECURITY_FAILURE") ||
            CatalogPublisher.MaximumMaintenanceItems < 6)
            throw new InvalidOperationException("maintenance classification failed");
        RunContentSignatureTests();

        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var ring = ContentKeyRing.ForSelfTest(key);
            var proofNonce = RandomNumberGenerator.GetBytes(32);
            var allowlistFingerprint = RandomNumberGenerator.GetBytes(32);
            var keySetDomain = Encoding.ASCII.GetBytes(
                "TurboRamaSuiteContentKeySetFingerprint/v1\0");
            var keySetMessage = new byte[keySetDomain.Length + sizeof(int) * 2 + key.Length];
            keySetDomain.CopyTo(keySetMessage, 0);
            BinaryPrimitives.WriteInt32BigEndian(
                keySetMessage.AsSpan(keySetDomain.Length, sizeof(int)), 1);
            BinaryPrimitives.WriteInt32BigEndian(
                keySetMessage.AsSpan(keySetDomain.Length + sizeof(int), sizeof(int)), 1);
            key.CopyTo(keySetMessage.AsSpan(keySetDomain.Length + sizeof(int) * 2));
            var keySetFingerprint = SHA256.HashData(keySetMessage);
            var proofDomain = Encoding.ASCII.GetBytes(
                "TurboRamaSuiteContentDeploymentProof/v1\0");
            var proofMessage = new byte[proofDomain.Length + sizeof(int) + 96];
            var proof = Array.Empty<byte>();
            var expectedProof = Array.Empty<byte>();
            try
            {
                if (!string.Equals(ring.KeySetFingerprint,
                        Convert.ToHexString(keySetFingerprint).ToLowerInvariant(),
                        StringComparison.Ordinal))
                    throw new InvalidOperationException("keyring set fingerprint failed");
                proofDomain.CopyTo(proofMessage, 0);
                BinaryPrimitives.WriteInt32BigEndian(
                    proofMessage.AsSpan(proofDomain.Length, sizeof(int)), ring.ActiveVersion);
                var offset = proofDomain.Length + sizeof(int);
                keySetFingerprint.CopyTo(proofMessage.AsSpan(offset, 32));
                offset += 32;
                allowlistFingerprint.CopyTo(proofMessage.AsSpan(offset, 32));
                offset += 32;
                proofNonce.CopyTo(proofMessage.AsSpan(offset, 32));
                proof = ring.CreateGatewayProof(proofNonce, allowlistFingerprint);
                expectedProof = HMACSHA256.HashData(key, proofMessage);
                if (!CryptographicOperations.FixedTimeEquals(proof, expectedProof))
                    throw new InvalidOperationException("keyring cross-process proof failed");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(proofNonce);
                CryptographicOperations.ZeroMemory(allowlistFingerprint);
                CryptographicOperations.ZeroMemory(keySetDomain);
                CryptographicOperations.ZeroMemory(keySetMessage);
                CryptographicOperations.ZeroMemory(keySetFingerprint);
                CryptographicOperations.ZeroMemory(proofDomain);
                CryptographicOperations.ZeroMemory(proofMessage);
                if (proof.Length != 0) CryptographicOperations.ZeroMemory(proof);
                if (expectedProof.Length != 0)
                    CryptographicOperations.ZeroMemory(expectedProof);
            }
            var encrypted = ring.Encrypt(prepared.UpstreamUri!, catalogIdentity, prepared.ItemId,
                prepared.ItemId, 1, catalogIdentity);
            try
            {
                var plaintext = new byte[encrypted.Ciphertext.Length];
                var aad = CanonicalIdentity.UrlAad(catalogIdentity, prepared.ItemId, prepared.ItemId, 1,
                    catalogIdentity, 1);
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag, plaintext, aad);
                if (Encoding.UTF8.GetString(plaintext) != prepared.UpstreamUri!.AbsoluteUri)
                    throw new InvalidOperationException("URL encryption roundtrip failed");
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(aad);
            }
            finally { encrypted.Clear(); }
            var boundaryEncrypted = ring.Encrypt(maximumUrl, catalogIdentity, prepared.ItemId,
                prepared.ItemId, 1, catalogIdentity);
            try
            {
                if (boundaryEncrypted.Ciphertext.Length != OriginPolicy.MaximumUrlUtf8Bytes)
                    throw new InvalidOperationException("URL ciphertext maximum failed");
            }
            finally { boundaryEncrypted.Clear(); }
            try
            {
                _ = ring.Encrypt(new Uri(maximumUrl.AbsoluteUri + "a"), catalogIdentity,
                    prepared.ItemId, prepared.ItemId, 1, catalogIdentity);
                throw new InvalidOperationException("URL encryption accepted 4097 bytes");
            }
            catch (PublisherFailure failure) when (failure.Code == "UPSTREAM_URL_POLICY_DENIED") { }
        }
        finally { CryptographicOperations.ZeroMemory(key); }

        Console.WriteLine("SUITE CONTENT PUBLISHER SELF-TEST: OK (SSRF policy, maintenance classification, streamed content signatures/XML, canonical descriptors/catalog, AES-256-GCM/AAD)");
        return 0;
    }

    private static void RunContentSignatureTests()
    {
        if (new VerificationJournal().SchemaVersion != 2)
            throw new InvalidOperationException("content-validation journal version failed");
        var signatures = new Dictionary<string, (int Offset, byte[] Value)>(StringComparer.Ordinal)
        {
            [".rar"] = (0, "Rar!\x1a\x07\x01\0"u8.ToArray()),
            [".7z"] = (0, [0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c]),
            [".iso"] = (16 * 2048 + 1, "CD001"u8.ToArray()),
            [".pbp"] = (0, "\0PBP"u8.ToArray()),
            [".zip"] = (0, "PK\x03\x04"u8.ToArray()),
            [".nsp"] = (0, "PFS0"u8.ToArray()),
            [".chd"] = (0, "MComprHD"u8.ToArray()),
            [".cso"] = (0, "CISO"u8.ToArray()),
            [".3ds"] = (0x100, "NCSD"u8.ToArray()),
            [".xci"] = (0x100, "HEAD"u8.ToArray()),
            [".rvz"] = (0, "RVZ\x01"u8.ToArray()),
            [".wux"] = (0, "WUX0"u8.ToArray()),
            [".bin"] = (32, [0x89, 0x42, 0x49, 0x4e]),
            [".wbfs"] = (0, "WBFS"u8.ToArray())
        };
        foreach (var (extension, signature) in signatures)
        {
            var capture = new byte[ContentSignatureValidator.CaptureLimit];
            signature.Value.CopyTo(capture, signature.Offset);
            ContentSignatureValidator.ValidateBinary(extension, "application/octet-stream",
                Math.Max(100_000, ContentSignatureValidator.MinimumGenericBinLength), capture);
        }

        var html = Encoding.UTF8.GetBytes("  <!DOCTYPE html><html><body>error</body></html>");
        ExpectInvalid(() => ContentSignatureValidator.ValidateBinary(
            ".bin", "application/octet-stream", 100_000, html));
        ExpectInvalid(() => ContentSignatureValidator.ValidateContentType(
            ".zip", "application/problem+json"));
        ExpectInvalid(() => ContentSignatureValidator.ValidateContentType(
            ".iso", "text/html"));
        ExpectInvalid(() => ContentSignatureValidator.ValidateBinary(
            ".bin", "application/octet-stream", ContentSignatureValidator.MinimumGenericBinLength - 1,
            [0x01, 0x02, 0x03]));

        var xml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><gameList><game><name>OK</name></game></gameList>");
        using (var source = new MemoryStream(xml))
        using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var capture = new byte[ContentSignatureValidator.CaptureLimit];
            using var hashing = new HashingCaptureStream(source, hash, capture,
                CancellationToken.None, CancellationToken.None, CancellationToken.None);
            ContentSignatureValidator.ValidateXmlAsync(hashing, xml.Length, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (hashing.TotalBytes != xml.Length || hashing.CapturedBytes != xml.Length)
                throw new InvalidOperationException("XML streaming hash validation failed");
        }
        ExpectInvalidAsync(() => ContentSignatureValidator.ValidateXmlAsync(
            new MemoryStream("<html><body>error</body></html>"u8.ToArray()), 32, CancellationToken.None));
        ExpectInvalidAsync(() => ContentSignatureValidator.ValidateXmlAsync(
            new MemoryStream("<gameList><game></gameList>"u8.ToArray()), 28, CancellationToken.None));
    }

    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (PublisherFailure ex) when (ex.Code == "ORIGIN_CONTENT_INVALID") { return; }
        throw new InvalidOperationException("invalid content was accepted");
    }

    private static void ExpectInvalidAsync(Func<Task> action)
    {
        try { action().GetAwaiter().GetResult(); }
        catch (PublisherFailure ex) when (ex.Code == "ORIGIN_CONTENT_INVALID") { return; }
        throw new InvalidOperationException("invalid XML was accepted");
    }
}
