using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentMonitor;

internal static class MonitorSelfTest
{
    public static int Run()
    {
        CryptoRoundTrip();
        KeyRingDomainSeparation();
        IdentityAndPolicy();
        SignatureAndNetworkPolicy();
        GatewayDeploymentProofAsync().GetAwaiter().GetResult();
        Console.WriteLine("SUITE CONTENT MONITOR SELF-TEST: OK (AAD, AES-256-GCM, keyring domain separation, deployment-bound descriptor identity, validator/full-hash policy, hysteresis, cumulative mass guard, signatures, IPv4/IPv6/NAT64 policy, gateway keyset/allowlist proof fail-closed)");
        return 0;
    }

    private static void KeyRingDomainSeparation()
    {
        var shared = RandomNumberGenerator.GetBytes(32);
        var distinctCandidate = RandomNumberGenerator.GetBytes(32);
        var distinctOrigin = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var candidate = MonitorKeyRing.ForTest(2, new Dictionary<int, byte[]>
            {
                [1] = distinctCandidate,
                [2] = shared
            });
            using var identical = MonitorKeyRing.ForTest(2, new Dictionary<int, byte[]>
            {
                [1] = distinctCandidate,
                [2] = shared
            });
            using var partial = MonitorKeyRing.ForTest(3, new Dictionary<int, byte[]>
            {
                [1] = distinctOrigin,
                [3] = shared
            });
            using var separate = MonitorKeyRing.ForTest(distinctOrigin);
            if (!candidate.SharesAnyKeyMaterialWith(identical) ||
                !candidate.SharesAnyKeyMaterialWith(partial) ||
                candidate.SharesAnyKeyMaterialWith(separate))
                throw new InvalidOperationException("candidate/origin keyring separation failed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(distinctCandidate);
            CryptographicOperations.ZeroMemory(distinctOrigin);
        }
    }

    private static void CryptoRoundTrip()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        using var ring = MonitorKeyRing.ForTest(key);
        CryptographicOperations.ZeroMemory(key);
        var target = new HealthTarget(new string('a', 32), new string('b', 64), 3, 123,
            new string('c', 64), ".zip", null, null, [], [], [], 1, 0, null, null, 1);
        using var encrypted = ring.EncryptOrigin(new Uri("https://example.invalid/file.zip"),
            target.CatalogIdentity, target.ItemId, target.ArtifactVersion);
        var material = target with
        {
            Ciphertext = encrypted.Ciphertext.ToArray(),
            Nonce = encrypted.Nonce.ToArray(),
            Tag = encrypted.Tag.ToArray()
        };
        using var clear = ring.DecryptOrigin(material);
        if (clear.Uri.AbsoluteUri != "https://example.invalid/file.zip")
            throw new InvalidOperationException("origin AAD round-trip failed");
        var aad = MonitorKeyRing.CandidateAad(new string('c', 64), target.ItemId,
            target.CatalogIdentity, 1);
        if (!Encoding.ASCII.GetString(aad).StartsWith(
                "TurboRamaSuiteContentCandidateUrl/v1\0{\"candidateId\":", StringComparison.Ordinal))
            throw new InvalidOperationException("candidate AAD is not canonical");
        CryptographicOperations.ZeroMemory(aad);
        CryptographicOperations.ZeroMemory(material.Ciphertext);
        CryptographicOperations.ZeroMemory(material.Nonce);
        CryptographicOperations.ZeroMemory(material.Tag);
        const string urlPrefix = "https://example.invalid/";
        var exactLimit = new Uri(urlPrefix + new string('a', 4096 - urlPrefix.Length));
        using (var exact = ring.EncryptOrigin(exactLimit, target.CatalogIdentity,
                   target.ItemId, target.ArtifactVersion))
            if (exact.Ciphertext.Length != 4096)
                throw new InvalidOperationException("origin URL 4096-byte boundary failed");
        var oversizedDenied = false;
        try
        {
            using var ignored = ring.EncryptOrigin(new Uri(exactLimit.AbsoluteUri + "a"),
                target.CatalogIdentity, target.ItemId, target.ArtifactVersion);
        }
        catch (MonitorFailure) { oversizedDenied = true; }
        if (!oversizedDenied)
            throw new InvalidOperationException("origin URL 4097-byte boundary accepted");
    }

    private static void IdentityAndPolicy()
    {
        var header = new SnapshotHeader(new string('a', 64), 1, new string('b', 64),
            new string('c', 64));
        var provenance = new MonitorDeploymentProvenance(2, new string('4', 64),
            new string('5', 64));
        var item = new SnapshotItem(new string('d', 32), 0, "Jogo & <seguro>", "NONE", "READY", 2, 12,
            new string('e', 64), "safe.zip", ".zip", "EXTRACT_ARCHIVE",
            "application/octet-stream", null, null, null, [], [], [], 1);
        var first = ManagedSnapshotIdentity.Compute(header, provenance, new string('f', 64), [item]);
        var second = ManagedSnapshotIdentity.Compute(header, provenance, new string('f', 64), [item]);
        var maintenance = ManagedSnapshotIdentity.Compute(header, provenance, new string('f', 64),
            [item with { Status = "MAINTENANCE", ArtifactVersion = null, ContentLength = null,
                Sha256 = null, SafeFileName = null, FileExtension = null, ExtractPolicy = null,
                ContentType = null, MaintenanceReason = "CONTENT_TEMPORARILY_UNAVAILABLE" }]);
        var rotated = ManagedSnapshotIdentity.Compute(header,
            provenance with { ActiveKeyVersion = 3 }, new string('f', 64), [item]);
        if (first != second || first == maintenance || first == rotated ||
            ManagedSnapshotIdentity.DescriptorHash(item, first).Length != 64)
            throw new InvalidOperationException("managed identity is not deterministic/status-bound");
        if (HealthPolicy.OpensMassFailureGuard(25) || !HealthPolicy.OpensMassFailureGuard(26) ||
            HealthPolicy.FailureThreshold != 3)
            throw new InvalidOperationException("health hysteresis policy changed");
        var validatorTarget = new HealthTarget(new string('1', 32), header.CatalogIdentity, 1,
            12, new string('2', 64), ".zip", "etag", null, [], [], [], 1, 0, null,
            null, 1);
        if (HealthPolicy.ValidatorsMatch(validatorTarget, null, null) ||
            !HealthPolicy.ValidatorsMatch(validatorTarget, "etag", null))
            throw new InvalidOperationException("missing health validator was accepted");
        var noValidatorTarget = validatorTarget with
        {
            ExpectedEtag = null,
            LastFullValidationAt = DateTime.UtcNow.AddDays(-8)
        };
        if (!HealthPolicy.RequiresFullValidation(noValidatorTarget, DateTime.UtcNow))
            throw new InvalidOperationException("validator-less origin was not scheduled for full hash");
        var lastModifiedOnlyTarget = noValidatorTarget with
        {
            ExpectedLastModified = "Sat, 29 Aug 2026 12:00:00 GMT"
        };
        if (!HealthPolicy.ValidatorsMatch(lastModifiedOnlyTarget, null,
                "Sat, 29 Aug 2026 12:00:00 GMT") ||
            !HealthPolicy.RequiresFullValidation(lastModifiedOnlyTarget, DateTime.UtcNow))
            throw new InvalidOperationException(
                "Last-Modified-only origin bypassed periodic full validation");
        var sameLengthAndDateButDifferentBody = new OriginMetadata(
            lastModifiedOnlyTarget.ExpectedContentLength!.Value, new string('3', 64),
            "application/octet-stream", null, lastModifiedOnlyTarget.ExpectedLastModified);
        if (HealthPolicy.FullArtifactMatches(lastModifiedOnlyTarget,
                sameLengthAndDateButDifferentBody))
            throw new InvalidOperationException(
                "same length/date with different body hash was accepted");
        var bodyBoundEtagTarget = lastModifiedOnlyTarget with
        {
            ExpectedEtag = "\"body-bound\""
        };
        if (HealthPolicy.RequiresFullValidation(bodyBoundEtagTarget, DateTime.UtcNow))
            throw new InvalidOperationException(
                "body-bound strong ETag was scheduled as an untrusted validator");
        var source = Enumerable.Range(0, 30).Select(index => item with
        {
            ItemId = index.ToString("x32"),
            Status = index < 20 ? "MAINTENANCE" : "READY",
            ArtifactVersion = index < 20 ? null : 2,
            ContentLength = index < 20 ? null : 12,
            Sha256 = index < 20 ? null : new string('e', 64),
            SafeFileName = index < 20 ? null : "safe.zip",
            FileExtension = index < 20 ? null : ".zip",
            ExtractPolicy = index < 20 ? null : "EXTRACT_ARCHIVE",
            ContentType = index < 20 ? null : "application/octet-stream"
        }).ToArray();
        var promotions = Enumerable.Range(20, 10).ToDictionary(index => index.ToString("x32"),
            index => new MaintenancePromotion(index.ToString("x32"), header.CatalogIdentity,
                2, index + 1L), StringComparer.Ordinal);
        if (MonitorStore.ResultingMaintenanceCount(source, header.CatalogIdentity, promotions) != 30 ||
            !HealthPolicy.OpensMassFailureGuard(
                MonitorStore.ResultingMaintenanceCount(source, header.CatalogIdentity, promotions)))
            throw new InvalidOperationException("cumulative mass-failure guard failed");
        var staleTargetDenied = false;
        try
        {
            _ = MonitorStore.ResultingMaintenanceCount(source, header.CatalogIdentity,
                new Dictionary<string, MaintenancePromotion>
                {
                    [20.ToString("x32")] = new(20.ToString("x32"),
                        header.CatalogIdentity, 3, 1)
                });
        }
        catch (MonitorFailure exception) when (exception.Code == "CATALOG_CHANGED")
        {
            staleTargetDenied = true;
        }
        if (!staleTargetDenied)
            throw new InvalidOperationException("stale artifact version promotion was accepted");
        var oldCatalogDenied = false;
        try
        {
            _ = MonitorStore.ResultingMaintenanceCount(source, new string('9', 64), promotions);
        }
        catch (MonitorFailure exception) when (exception.Code == "CATALOG_CHANGED")
        {
            oldCatalogDenied = true;
        }
        if (!oldCatalogDenied)
            throw new InvalidOperationException("old-catalog promotion was accepted");
        var artifact = MonitorCoordinator.ArtifactName(
            new Uri("https://example.invalid/game.7z"), "NONE");
        if (artifact.Extension != ".7z" || artifact.ExtractPolicy != "NONE")
            throw new InvalidOperationException("artifact policy failed");
        var upperCaseArtifact = MonitorCoordinator.ArtifactName(
            new Uri("https://example.invalid/GAME.ZIP"), "NONE");
        if (upperCaseArtifact.Name != "GAME.zip" || upperCaseArtifact.Extension != ".zip" ||
            upperCaseArtifact.ExtractPolicy != "NONE" ||
            !upperCaseArtifact.Name.EndsWith(upperCaseArtifact.Extension, StringComparison.Ordinal))
            throw new InvalidOperationException("artifact extension casing policy failed");
        try
        {
            _ = MonitorCoordinator.ArtifactName(
                new Uri("https://example.invalid/game.iso"), "EXTRACT_ARCHIVE");
        }
        catch (PublisherFailure) { return; }
        throw new InvalidOperationException("artifact extract policy divergence was accepted");
    }

    private static void SignatureAndNetworkPolicy()
    {
        var sevenZip = new byte[32];
        new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c }.CopyTo(sevenZip, 0);
        ContentSignatureValidator.ValidateBinary(".7z", "application/octet-stream", 32, sevenZip);
        if (OriginPolicy.IsPublic(IPAddress.Loopback) ||
            OriginPolicy.IsPublic(IPAddress.Parse("10.0.0.1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("64:ff9b::7f00:1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:db8::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2002:0101:0101::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:0000::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("2001:20::1")) ||
            OriginPolicy.IsPublic(IPAddress.Parse("3fff::1")) ||
            !OriginPolicy.IsPublic(IPAddress.Parse("1.1.1.1")))
            throw new InvalidOperationException("network destination policy failed");
    }

    private static async Task GatewayDeploymentProofAsync()
    {
        var firstKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var activeKey = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();
        var allowlist = SHA256.HashData(Encoding.ASCII.GetBytes("monitor-allowlist-self-test"));
        using var ring = MonitorKeyRing.ForTest(2, new Dictionary<int, byte[]>
        {
            [1] = firstKey,
            [2] = activeKey
        });
        try
        {
            using (var success = new ProofHandler(firstKey, activeKey, allowlist,
                       HttpStatusCode.NoContent, false))
                await GatewayDeploymentPreflight.RequireAsync(
                    new Uri("http://127.0.0.1:5191/ready/keyring/prove"), ring,
                    allowlist.ToArray(), CancellationToken.None, success);

            var mismatchDenied = false;
            try
            {
                using var mismatch = new ProofHandler(firstKey, activeKey, allowlist,
                    HttpStatusCode.NotFound, false);
                await GatewayDeploymentPreflight.RequireAsync(
                    new Uri("http://127.0.0.1:5191/ready/keyring/prove"), ring,
                    allowlist.ToArray(), CancellationToken.None, mismatch);
            }
            catch (MonitorFailure exception)
            {
                mismatchDenied = exception.Code == "GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED";
            }

            var unavailableDenied = false;
            try
            {
                using var unavailable = new ProofHandler(firstKey, activeKey, allowlist,
                    HttpStatusCode.NoContent, true);
                await GatewayDeploymentPreflight.RequireAsync(
                    new Uri("http://127.0.0.1:5191/ready/keyring/prove"), ring,
                    allowlist.ToArray(), CancellationToken.None, unavailable);
            }
            catch (MonitorFailure exception)
            {
                unavailableDenied = exception.Code == "GATEWAY_DEPLOYMENT_PREFLIGHT_FAILED";
            }
            if (!mismatchDenied || !unavailableDenied)
                throw new InvalidOperationException("gateway deployment preflight did not fail closed");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(firstKey);
            CryptographicOperations.ZeroMemory(activeKey);
            CryptographicOperations.ZeroMemory(allowlist);
        }
    }

    private sealed class ProofHandler(byte[] firstKey, byte[] activeKey,
        byte[] allowlistFingerprint, HttpStatusCode statusCode, bool unavailable) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (unavailable) throw new HttpRequestException("self-test unavailable");
            if (request.Method != HttpMethod.Post || request.RequestUri?.AbsoluteUri !=
                    "http://127.0.0.1:5191/ready/keyring/prove")
                throw new InvalidOperationException("gateway proof destination changed");
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            try
            {
                using var document = JsonDocument.Parse(bytes);
                var root = document.RootElement;
                var nonce = Convert.FromBase64String(root.GetProperty("nonce").GetString()!);
                var proof = Convert.FromBase64String(root.GetProperty("proof").GetString()!);
                var keySet = ExpectedKeySet(firstKey, activeKey);
                var message = ExpectedProofMessage(keySet, allowlistFingerprint, nonce);
                var expectedProof = HMACSHA256.HashData(activeKey, message);
                try
                {
                    if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
                        root.GetProperty("activeKeyVersion").GetInt32() != 2 ||
                        root.GetProperty("keySetFingerprint").GetString() !=
                            Convert.ToHexString(keySet).ToLowerInvariant() ||
                        root.GetProperty("allowlistFingerprint").GetString() !=
                            Convert.ToHexString(allowlistFingerprint).ToLowerInvariant() ||
                        nonce.Length != 32 || proof.Length != 32 ||
                        !CryptographicOperations.FixedTimeEquals(proof, expectedProof))
                        throw new InvalidOperationException("gateway deployment proof is not canonical");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(nonce);
                    CryptographicOperations.ZeroMemory(proof);
                    CryptographicOperations.ZeroMemory(keySet);
                    CryptographicOperations.ZeroMemory(message);
                    CryptographicOperations.ZeroMemory(expectedProof);
                }
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            return new HttpResponseMessage(statusCode);
        }

        private static byte[] ExpectedKeySet(byte[] first, byte[] active)
        {
            using var stream = new MemoryStream();
            stream.Write(Encoding.ASCII.GetBytes("TurboRamaSuiteContentKeySetFingerprint/v1\0"));
            WriteInt32(stream, 2);
            WriteInt32(stream, 1);
            stream.Write(first);
            WriteInt32(stream, 2);
            stream.Write(active);
            return SHA256.HashData(stream.ToArray());
        }

        private static byte[] ExpectedProofMessage(byte[] keySet, byte[] allowlist, byte[] nonce)
        {
            using var stream = new MemoryStream();
            stream.Write(Encoding.ASCII.GetBytes("TurboRamaSuiteContentDeploymentProof/v1\0"));
            WriteInt32(stream, 2);
            stream.Write(keySet);
            stream.Write(allowlist);
            stream.Write(nonce);
            return stream.ToArray();
        }

        private static void WriteInt32(Stream stream, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            stream.Write(bytes);
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
