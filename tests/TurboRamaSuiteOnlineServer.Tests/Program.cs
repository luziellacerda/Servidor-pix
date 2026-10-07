using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using TurboRamaSuiteContentGateway;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteContentPublisher;

await ExtractionNotificationHttpChecks.RunAsync();
StationProtocolChecks.Run();
StationSecurityChecks.Run();
StationTransferRateChecks.Run();
if (Environment.GetEnvironmentVariable("STATION_TEST_PG") is { Length: > 0 } stationConnection)
    await StationPostgresChecks.RunAsync(stationConnection);

const string licenseId = "TR-000125";
const string inventoryLicense="TS-INVENTORY-FIXTURE-00000001";
var inventory=new SuiteMotherboardInventoryV1(1,inventoryLicense,new string('a',64),"e2b57c24e8e7ebf36d63e93f6f1021c3eb9990d95a8c50494fa6e2604749d283","Gigabyte Technology Co., Ltd.","B550M AORUS ELITE","x.x","SN-000001","Gigabyte Technology Co., Ltd.","B550M AORUS ELITE","03560230-040f-0585-c906-a80700080009","American Megatrends International, LLC.","FG","Microsoft Windows 10 Pro","10.0.19044.0","X64","2.0.0.0","CIM",1_800_000_000);
Equal("3a60c9704cdc1f1ee8da929e4bedbcabe6fddfe053d470d27f29ab25557deec3",Sha(SuiteDeviceInventoryProtocol.CanonicalInventory(inventory)),"R25 canonical inventory");
Equal("ac3a2717355c268cb2150f06e04a0a708f7881f580a90a7cdfe903b88f5fe2df",Sha(SuiteDeviceInventoryProtocol.CanonicalInventoryState(inventory)),"R25 canonical state");
var inventoryHash=SuiteDeviceInventoryProtocol.InventoryHash(inventory);Equal("4282fabba3c01c0fd12c8ba51309c76a6db022b832039016d7df6e25fca9c4f5",inventoryHash,"R25 inventory hash");
var inventorySession=new string('b',64);var inventoryChallengeId=new string('c',64);var inventoryNonce="AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
var inventoryRequest=new SuiteDeviceInventoryChallengeRequestV1(1,Protocol.ProductId,inventoryLicense,inventory.DeviceId,inventorySession,SuiteDeviceInventoryProtocol.Action,inventoryHash);
Equal(true,new SuiteRateLimiter(TimeProvider.System).Allow("127.0.0.1","/v1/suite/devices/inventory/challenge",inventoryRequest),"R25 challenge rate classification");
Equal("ecf8c37d6c468730a01a0ce68c274fef5d41ccbde0760a505a47bda7dfdcbacc",Sha(SuiteDeviceInventoryProtocol.CanonicalChallengeRequest(inventoryRequest)),"R25 challenge request");
var inventoryChallenge=new ChallengeResponse(1,inventoryChallengeId,inventoryNonce,1_800_000_060);
Equal("465f80c902e286a8af0a067c0ac8e24ae16c2bd4d1e95d1af72b3d2437798bfb",Sha(SuiteDeviceInventoryProtocol.BuildProofSigningMessage(inventoryChallenge,inventoryLicense,inventory.DeviceId,inventorySession,inventoryHash)),"R25 proof signing message");
var syntheticSignature=Convert.ToBase64String(Enumerable.Range(0,256).Select(i=>(byte)i).ToArray());
var inventoryProof=new SuiteDeviceInventoryProofV1(1,Protocol.ProductId,inventoryLicense,inventory.DeviceId,inventorySession,SuiteDeviceInventoryProtocol.Action,inventoryHash,inventoryChallengeId,syntheticSignature,inventory);
Equal(true,new SuiteRateLimiter(TimeProvider.System).Allow("127.0.0.1","/v1/suite/devices/inventory",inventoryProof),"R25 proof rate classification");
Equal("6e355cd41a463b9e8990864c687ca11e5bba103fba61831aaac8a808e62d292d",Sha(SuiteDeviceInventoryProtocol.CanonicalProof(inventoryProof)),"R25 canonical proof");
var inventoryChallengeAssertion=new SuiteDeviceInventoryChallengeAssertionV1(1,SuiteDeviceInventoryProtocol.ChallengeAssertionKind,Protocol.ProductId,inventoryLicense,inventory.DeviceId,inventorySession,SuiteDeviceInventoryProtocol.Action,inventoryHash,inventoryChallengeId,inventoryNonce,SuiteDeviceInventoryProtocol.ChallengeStatus,1_800_000_000,1_800_000_060);
Equal("beab807bddc94a88379a7862c528d5a58cdb9f97b355946a47d4d11f767615f2",Sha(SuiteDeviceInventoryProtocol.CanonicalChallengeAssertion(inventoryChallengeAssertion)),"R25 challenge assertion");
Equal("7ae0f0e8933a85936dbf1a9e369a6e738819daea2801077bc9a18ecc23850d69",Sha(SuiteDeviceInventoryProtocol.BuildChallengeAssertionSigningMessage(inventoryChallengeAssertion)),"R25 challenge assertion signing message");
var inventoryResultAssertion=new SuiteDeviceInventoryResultAssertionV1(1,SuiteDeviceInventoryProtocol.ResultAssertionKind,Protocol.ProductId,inventoryLicense,inventory.DeviceId,inventorySession,SuiteDeviceInventoryProtocol.Action,inventoryHash,inventoryChallengeId,SuiteDeviceInventoryProtocol.ResultStatus,1_800_000_000);
Equal("7fc90ae16476b900ea7003b475a3b1d9c01b5e3943fd3ea1c6fa9ec0d8ab3ac6",Sha(SuiteDeviceInventoryProtocol.CanonicalResultAssertion(inventoryResultAssertion)),"R25 result assertion");
Equal("6d394b689b8dabb0c8afc6cce11181742a3152752ee7103b0ecc0eea4550c2b3",Sha(SuiteDeviceInventoryProtocol.BuildResultAssertionSigningMessage(inventoryResultAssertion)),"R25 result assertion signing message");
Equal(false, PublicNetworkPolicy.IsGloballyRoutable(IPAddress.Parse("2002:0a00:0001::")),
    "gateway denies 6to4 with embedded private IPv4");
Equal(false, PublicNetworkPolicy.IsGloballyRoutable(IPAddress.Parse("2001:0000::1")),
    "gateway denies Teredo transition range");
Equal(false, PublicNetworkPolicy.IsGloballyRoutable(IPAddress.Parse("2001:0020::1")),
    "gateway denies ORCHID range");
Equal(false, PublicNetworkPolicy.IsGloballyRoutable(IPAddress.Parse("198.18.0.1")),
    "gateway denies IPv4 benchmarking range");
Equal(true, PublicNetworkPolicy.IsGloballyRoutable(IPAddress.Parse("2606:4700:4700::1111")),
    "gateway allows global unicast IPv6");
using var machine = RSA.Create(2048); using var online = RSA.Create(2048);
var spki = machine.ExportSubjectPublicKeyInfo(); var deviceId = Sha(spki); var fingerprint = new string('a', 64);
var device = new DeviceDescriptor(1, deviceId, "SOFTWARE_BOUND_ONLINE", Protocol.Algorithm, Convert.ToBase64String(spki), fingerprint, "1.0.0");
var fixtureDevice = new DeviceDescriptor(1, new string('4', 64), "SOFTWARE_BOUND_ONLINE", Protocol.Algorithm, new string('A', 344), new string('2', 64), "25.0.0.0");
Equal("ed47acd52669a3901931427994a191cae8a50af369b3cf1b81a5221b46623958", Protocol.ActivationContextHash(licenseId, fixtureDevice), "activation context golden vector");
var activation = Protocol.ActivationContextHash(licenseId, device);
var sessionId = new string('2', 64); var sessionContext = new SessionContext(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.open", fingerprint, "1.0.0");
var fixtureSession = new SessionContext(1, Protocol.ProductId, licenseId, new string('4', 64), new string('1', 64), "session.open", new string('2', 64), "1.7.0");
Equal("5e05775e2819b9cfce3f5c16662cff88bcf627d26f26695c2a993bc04d0f9989", Protocol.SessionContextHash(fixtureSession), "session context golden vector");
var challenge = new ChallengeResponse(1, new string('3', 64), "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", 1_800_000_060);
var message = Protocol.SigningMessage(challenge, licenseId, new string('4', 64), new string('1', 64), "session.open", "77c209a4ab9fd413b3c39f9d150b8605f6bf4fdcf63a1c7698606975b24fdeb0");
Equal(506, message.Length, "machine proof length"); Equal("e446888f27083109d8fb454ba287a2c524691329434906971f1f5a3a870bc481", Sha(message), "machine proof hash");

var contentItemId = "0123456789abcdef0123456789abcdef";
var contentDescriptor = new ContentArtifactDescriptor(
    "fedcba9876543210fedcba9876543210", 1,
    "arquivo.zip", ".zip", "EXTRACT_ARCHIVE",
    new string('b', 64));
var descriptorHash = ContentProtocol.DescriptorHash(contentItemId, contentDescriptor);
Equal("c99cff0e70f7783bcf79536670e675d61a0fa37b5a4acdf4d9704f764aa825c1",
    descriptorHash, "content descriptor hash golden vector");
var catalogContext = new CatalogPageContext(1, Protocol.ProductId, licenseId,
    new string('4', 64), new string('1', 64), ContentProtocol.CatalogReadAction,
    "", 64);
Equal("45fd00059fc5b0ad2e5c42834d19948611fd37aa27ccea429893e2b662ff9651",
    ContentProtocol.CatalogContextHash(catalogContext),
    "catalog context hash golden vector");
var downloadContext = new DownloadAuthorizationContext(1, Protocol.ProductId,
    licenseId, new string('4', 64), new string('1', 64),
    ContentProtocol.DownloadAuthorizeAction, new string('b', 64), contentItemId,
    contentDescriptor.ArtifactId, contentDescriptor.ArtifactVersion,
    contentDescriptor.ManifestIdentity, descriptorHash, 4096, "\"etag\"", "");
Equal("7f8f91c6e6ba7d75ee1403d3793a68c690b820c1d6a54049c4130ed861cd5a1a",
    ContentProtocol.DownloadContextHash(downloadContext),
    "download context hash golden vector");
Equal(contentItemId,
    ContentProtocol.DecodeCursor(ContentProtocol.EncodeCursor(contentItemId)),
    "opaque cursor round trip");
Expect<SuiteException>(() => ContentProtocol.DescriptorHash(
    contentItemId.ToUpperInvariant(), contentDescriptor), "uppercase content item id");
Expect<SuiteException>(() => ContentProtocol.DescriptorHash(contentItemId,
    contentDescriptor with { SafeFileName = "../arquivo.zip" }),
    "unsafe content file name");
Expect<SuiteException>(() => ContentProtocol.DescriptorHash(contentItemId,
    contentDescriptor with { SafeFileName = "arquivo.ZIP" }),
    "noncanonical uppercase content extension");
using var contentKey = RSA.Create(2048);
using var contentSigner = new RsaContentAssertionSigner(contentKey);
Equal(true, ContentStartupIsolation.TryInitialize(() => { }),
    "content startup accepts valid dependencies");
Equal(false, ContentStartupIsolation.TryInitialize(() =>
    ContentProtectedSecret.ReadFile(Path.Combine(Path.GetTempPath(),
        Guid.NewGuid().ToString("N"), "missing-content-secret"), 1024)),
    "missing content credential degrades only content");
Equal(false, ContentStartupIsolation.TryInitialize(() =>
{
    using var invalidContentKey = RSA.Create();
    invalidContentKey.ImportFromPem("not-a-private-key");
}), "corrupt content key degrades only content");
const string contentApiConnection =
    "Host=/var/run/postgresql;Database=turborama;Username=turborama-suite-content-api";
Equal(contentApiConnection, ContentConnectionPolicy.RequireRole(contentApiConnection,
    "turborama-suite-content-api"), "content API exact database role");
Expect<InvalidOperationException>(() => ContentConnectionPolicy.RequireRole(
    "Host=/var/run/postgresql;Database=turborama;Username=postgres",
    "turborama-suite-content-api"), "content API rejects superuser database role");
const string gatewayConnection =
    "Host=/var/run/postgresql;Database=turborama;Username=turborama-suite-gateway";
Equal(gatewayConnection, ContentConnectionPolicy.RequireRole(gatewayConnection,
    "turborama-suite-gateway"), "gateway exact database role");
Expect<InvalidOperationException>(() => ContentConnectionPolicy.RequireRole(
    contentApiConnection, "turborama-suite-gateway"),
    "gateway rejects content API database role");
ContentAssertionKeyPolicy.RequireExpectedKeyId(contentSigner.KeyId,
    contentSigner.KeyId, true);
ContentAssertionKeyPolicy.RequireExpectedKeyId(contentSigner.KeyId, null, false);
Expect<InvalidOperationException>(() =>
    ContentAssertionKeyPolicy.RequireExpectedKeyId(contentSigner.KeyId, null, true),
    "missing production content key pin");
Expect<InvalidOperationException>(() =>
    ContentAssertionKeyPolicy.RequireExpectedKeyId(contentSigner.KeyId,
        (contentSigner.KeyId[0] == 'f' ? "e" : "f") + contentSigner.KeyId[1..], true),
    "mismatched production content key pin");
using (var undersizedContentKey = RSA.Create(1024))
    Expect<InvalidOperationException>(() =>
        _ = new RsaContentAssertionSigner(undersizedContentKey),
        "undersized content assertion key");
using (var publicOnlyContentKey = RSA.Create())
{
    publicOnlyContentKey.ImportSubjectPublicKeyInfo(contentKey.ExportSubjectPublicKeyInfo(), out _);
    Expect<InvalidOperationException>(() =>
        _ = new RsaContentAssertionSigner(publicOnlyContentKey),
        "public-only content assertion key");
}
Equal("TurboRamaSuiteContentAssertion/catalog-page/v1\0",
    ContentProtocol.CatalogAssertionDomain, "catalog assertion domain");
Equal("TurboRamaSuiteContentAssertion/download-grant/v1\0",
    ContentProtocol.DownloadAssertionDomain, "download assertion domain");
var catalogAssertion = new CatalogPageAssertion(1, ContentProtocol.CatalogPageKind,
    Protocol.ProductId, licenseId, new string('4', 64), new string('1', 64),
    ContentProtocol.CatalogReadAction,
    ContentProtocol.CatalogContextHash(catalogContext), new string('c', 64),
    "AUTHORIZED", 1_800_000_000, 1_800_000_060, new string('b', 64), 1,
    [new AuthorizedCatalogItem(contentItemId, ContentProtocol.ReadyAvailability,
        contentDescriptor, null)], null);
var contentEnvelope = contentSigner.Sign(catalogAssertion);
var contentPayload = Convert.FromBase64String(contentEnvelope.Payload);
var contentDomain = Encoding.ASCII.GetBytes(ContentProtocol.CatalogAssertionDomain);
var contentMessage = new byte[contentDomain.Length + contentPayload.Length];
contentDomain.CopyTo(contentMessage, 0); contentPayload.CopyTo(contentMessage, contentDomain.Length);
Equal("eeb8cf7f645f32b1a25caab01721d0071d15d4b17dc1431644e2479791c94b35",
    Sha(contentPayload),
    "ready catalog item canonical golden vector");
Equal(true, contentKey.VerifyData(contentMessage,
    Convert.FromBase64String(contentEnvelope.Signature), HashAlgorithmName.SHA256,
    RSASignaturePadding.Pss), "content assertion dedicated signature domain");
CryptographicOperations.ZeroMemory(contentPayload);
CryptographicOperations.ZeroMemory(contentMessage);
var maximumName = new string('&', 176) + ".zip";
var maximumPageItems = Enumerable.Range(0,
        ContentProtocol.MaximumCatalogResponseItems)
    .Select(index =>
    {
        var id = index.ToString("x32");
        return new AuthorizedCatalogItem(id, ContentProtocol.ReadyAvailability,
            contentDescriptor with
            {
                ArtifactId = id,
                SafeFileName = maximumName
            }, null);
    }).ToArray();
var maximumPageEnvelope = contentSigner.Sign(catalogAssertion with
{
    Items = maximumPageItems,
    NextCursor = ContentProtocol.EncodeCursor(maximumPageItems[^1].ItemId)
});
var maximumPageWire = JsonSerializer.SerializeToUtf8Bytes(maximumPageEnvelope,
    StrictJson.Options);
Equal(true, maximumPageWire.Length < Protocol.MaximumBodyBytes,
    "maximum catalog page stays inside wire limit");
CryptographicOperations.ZeroMemory(maximumPageWire);
var maintenanceItem = new AuthorizedCatalogItem(
    "11111111111111111111111111111111",
    ContentProtocol.MaintenanceAvailability,
    null,
    ContentProtocol.MaintenanceReasonCode);
var maintenanceCanonical = ContentProtocol.CanonicalAssertion(catalogAssertion with
{
    Items = [maintenanceItem]
});
Equal("5a20353458aa364bd0832efd8c6f594c02c48b238b49c7f1452ee72be980f236",
    Sha(maintenanceCanonical),
    "maintenance catalog item canonical golden vector");
CryptographicOperations.ZeroMemory(maintenanceCanonical);
Expect<SuiteException>(() => ContentProtocol.CanonicalAssertion(catalogAssertion with
{
    Items = [maintenanceItem with { Descriptor = contentDescriptor }]
}), "maintenance item with descriptor");
Expect<SuiteException>(() => ContentProtocol.CanonicalAssertion(catalogAssertion with
{
    Items = [maintenanceItem with { ReasonCode = "ORIGIN_HTTP_404" }]
}), "maintenance item with internal reason");

var secretTestRoot = Path.Combine(Path.GetTempPath(),
    "turborama-content-secret-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(secretTestRoot);
try
{
    var secretPath = Path.Combine(secretTestRoot, "secret.txt");
    File.WriteAllText(secretPath, "  protected-value  ");
    if (!OperatingSystem.IsWindows())
        File.SetUnixFileMode(secretPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Equal("protected-value", ContentProtectedSecret.ReadFile(secretPath, 1024),
        "protected content secret regular file");
    var keyRingPath = Path.Combine(secretTestRoot, "content-url-keyring.json");
    var keyRingKey = RandomNumberGenerator.GetBytes(32);
    var inactiveKeyRingKey = RandomNumberGenerator.GetBytes(32);
    try
    {
        File.WriteAllBytes(keyRingPath, JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            activeKeyVersion = 7,
            keys = new[]
            {
                new { version = 3, key = Convert.ToBase64String(inactiveKeyRingKey) },
                new { version = 7, key = Convert.ToBase64String(keyRingKey) }
            }
        }, StrictJson.Options));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keyRingPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        using var keyRing = ContentUrlKeyRing.Load(keyRingPath);
        Equal(7, keyRing.ActiveKeyVersion,
            "protected keyring startup canary and active version");
        using (var guardedUpstream = new SafeUpstreamClient(
                   ["origin-a.example.invalid", "origin-b.example.invalid"]))
        using (var guardedHasher = new ContentGrantTokenHasher(
                   Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))))
        {
            var unavailableStore = new GatewayStoreStub(false);
            var deploymentGuard = new ContentGatewayDeploymentGuard(
                unavailableStore, keyRing, guardedUpstream,
                new ManualTime(1_800_000_000));
            var guardedService = new ContentGatewayService(unavailableStore,
                guardedHasher, keyRing, guardedUpstream,
                new ManualTime(1_800_000_000), deploymentGuard,
                NullLogger<ContentGatewayService>.Instance);
            await ExpectSuiteAsync(() => guardedService.StreamAsync(
                    new DefaultHttpContext(), new string('7', 64), default),
                "CONTENT_RELAY_DISABLED",
                "server-side relay is permanently disabled");
            Equal(0, unavailableStore.ClaimCalls,
                "deployment mismatch does not consume a one-use grant");
        }
        const string maximumUrlPrefix = "https://origin-a.example.invalid/";
        var maximumUrlText = maximumUrlPrefix + new string('a',
            ContentUrlKeyRing.MaximumUrlUtf8Bytes - maximumUrlPrefix.Length);
        var urlCatalogIdentity = new string('9', 64);
        var urlItemId = new string('8', 32);
        var urlAad = ContentProtocol.UrlEncryptionAssociatedData(urlCatalogIdentity,
            urlItemId, urlItemId, 1, urlCatalogIdentity, 7);
        var urlNonce = RandomNumberGenerator.GetBytes(12);
        var urlTag = new byte[16];
        var urlPlaintext = Encoding.UTF8.GetBytes(maximumUrlText);
        var urlCiphertext = new byte[urlPlaintext.Length];
        try
        {
            using var aes = new AesGcm(keyRingKey, 16);
            aes.Encrypt(urlNonce, urlPlaintext, urlCiphertext, urlTag, urlAad);
            var boundaryGrant = new ClaimedContentGrantRecord(new string('7', 64),
                urlCatalogIdentity, urlItemId, urlItemId, 1, urlCatalogIdentity,
                new string('6', 64), 0, "file.zip", ".zip",
                "NONE", "application/octet-stream", null, null, urlCiphertext,
                urlNonce, urlTag, 7);
            Equal(maximumUrlText, keyRing.Decrypt(boundaryGrant).AbsoluteUri,
                "gateway accepts canonical 4096-byte URL");
            Expect<SuiteException>(() => keyRing.Decrypt(boundaryGrant with
            {
                UpstreamUrlCiphertext = new byte[ContentUrlKeyRing.MaximumUrlUtf8Bytes + 1]
            }), "gateway rejects 4097-byte URL ciphertext");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(urlAad);
            CryptographicOperations.ZeroMemory(urlNonce);
            CryptographicOperations.ZeroMemory(urlTag);
            CryptographicOperations.ZeroMemory(urlPlaintext);
            CryptographicOperations.ZeroMemory(urlCiphertext);
        }
        var proofNonce = RandomNumberGenerator.GetBytes(32);
        var keySetFingerprint = ContentDeploymentProofProtocol.KeySetFingerprint(
            new Dictionary<int, byte[]> { [3] = inactiveKeyRingKey, [7] = keyRingKey });
        var allowlistFingerprint = ContentDeploymentProofProtocol.AllowlistFingerprint(
            ["origin-a.example.invalid", "origin-b.example.invalid"]);
        var proofMessage = ContentDeploymentProofProtocol.ProofMessage(7,
            keySetFingerprint, allowlistFingerprint, proofNonce);
        var keyRingProof = Array.Empty<byte>();
        try
        {
            keyRingProof = HMACSHA256.HashData(keyRingKey, proofMessage);
            Equal(true, keyRing.VerifyDeploymentProof(7, keySetFingerprint,
                allowlistFingerprint, allowlistFingerprint, proofNonce, keyRingProof),
                "publisher and gateway deployment proof");
            Equal(false, keyRing.VerifyDeploymentProof(6, keySetFingerprint,
                allowlistFingerprint, allowlistFingerprint, proofNonce, keyRingProof),
                "deployment proof binds active version");
            var mismatchedKeySet = keySetFingerprint.ToArray();
            mismatchedKeySet[0] ^= 1;
            Equal(false, keyRing.VerifyDeploymentProof(7, mismatchedKeySet,
                allowlistFingerprint, allowlistFingerprint, proofNonce, keyRingProof),
                "deployment proof binds every key version and material");
            var mismatchedAllowlist = allowlistFingerprint.ToArray();
            mismatchedAllowlist[0] ^= 1;
            Equal(false, keyRing.VerifyDeploymentProof(7, keySetFingerprint,
                mismatchedAllowlist, allowlistFingerprint, proofNonce, keyRingProof),
                "deployment proof binds loaded origin allowlist");
            CryptographicOperations.ZeroMemory(mismatchedKeySet);
            CryptographicOperations.ZeroMemory(mismatchedAllowlist);
            keyRingProof[0] ^= 1;
            Equal(false, keyRing.VerifyDeploymentProof(7, keySetFingerprint,
                allowlistFingerprint, allowlistFingerprint, proofNonce, keyRingProof),
                "deployment proof rejects different material");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(proofNonce);
            CryptographicOperations.ZeroMemory(keySetFingerprint);
            CryptographicOperations.ZeroMemory(allowlistFingerprint);
            CryptographicOperations.ZeroMemory(proofMessage);
            if (keyRingProof.Length != 0)
                CryptographicOperations.ZeroMemory(keyRingProof);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(keyRingKey);
        CryptographicOperations.ZeroMemory(inactiveKeyRingKey);
    }
    Expect<InvalidOperationException>(() =>
        ContentProtectedSecret.ReadFile("relative-secret.txt", 1024),
        "relative protected content secret");
    Expect<InvalidOperationException>(() =>
        ContentProtectedSecret.ReadFile(secretTestRoot, 1024),
        "directory protected content secret");
    var linkPath = Path.Combine(secretTestRoot, "secret-link.txt");
    try
    {
        _ = File.CreateSymbolicLink(linkPath, secretPath);
        Expect<InvalidOperationException>(() =>
            ContentProtectedSecret.ReadFile(linkPath, 1024),
            "symlink protected content secret");
    }
    catch (Exception exception) when (exception is UnauthorizedAccessException or
                                      PlatformNotSupportedException or IOException)
    {
        // Creating symlinks is optional on locked-down Windows test hosts.
    }
}
finally
{
    Directory.Delete(secretTestRoot, true);
}

Expect<SuiteException>(() => StrictJson.Parse<ErrorResponse>(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1,\"code\":\"X\",\"message\":\"x\"}")), "duplicate JSON");
Expect<SuiteException>(() => StrictJson.Parse<ErrorResponse>(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"code\":\"X\",\"message\":\"x\",\"extra\":1}")), "unknown JSON");
Expect<SuiteException>(() => StrictJson.Parse<ActivationChallengeRequest>(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"productId\":\"TURBORAMA_SUITE\",\"licenseId\":\"ABCDEF\",\"activationCode\":\"1234567890123456\"}")), "missing required JSON member");
Expect<SuiteException>(() => Protocol.RequireProduct("TURBORAMA_PIX"), "cross product");
Expect<SuiteException>(() => Protocol.ValidateActivationCode("too-short"), "short activation code");
Expect<SuiteException>(() => Protocol.ValidateActivationCode("sixteen chars bad "), "activation code whitespace");
var forwardedHeaders = new ForwardedHeadersOptions();
SuiteTrustedProxyPolicy.Configure(forwardedHeaders);
Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    forwardedHeaders.ForwardedHeaders, "trusted proxy forwarded headers");
Equal(1, forwardedHeaders.ForwardLimit, "trusted proxy forward hop limit");
Equal(true, forwardedHeaders.RequireHeaderSymmetry,
    "trusted proxy header symmetry");
Equal(2, forwardedHeaders.KnownProxies.Count,
    "only loopback proxies are trusted");
Equal(true, forwardedHeaders.KnownProxies.Contains(System.Net.IPAddress.Loopback) &&
            forwardedHeaders.KnownProxies.Contains(System.Net.IPAddress.IPv6Loopback),
    "direct caller forwarding headers are not trusted");
var rateClock = new ManualTime(1_800_000_000); var limiter = new SuiteRateLimiter(rateClock);
for (var i = 0; i < 30; i++) Equal(true, limiter.Allow("127.0.0.1", "/route", new ChallengeRequest(1, Protocol.ProductId, licenseId, new string('1', 64), new string('2', 64), "session.open", new string('3', 64))), "rate allowance");
Equal(false, limiter.Allow("127.0.0.1", "/route", new ChallengeRequest(1, Protocol.ProductId, licenseId, new string('1', 64), new string('2', 64), "session.open", new string('3', 64))), "rate rejection");
var pagedLimiter = new SuiteRateLimiter(rateClock);
var rateDeviceId = new string('1', 64);
var rateSessionId = new string('2', 64);
for (var page = 0; page < 36; page++)
{
    var pageContext = catalogContext with
    {
        DeviceId = rateDeviceId,
        SessionId = rateSessionId,
        Cursor = page == 0 ? "" : ContentProtocol.EncodeCursor(page.ToString("x32"))
    };
    var pageContextHash = ContentProtocol.CatalogContextHash(pageContext);
    var pageChallenge = new ChallengeRequest(1, Protocol.ProductId, licenseId,
        rateDeviceId, rateSessionId, ContentProtocol.CatalogReadAction, pageContextHash);
    var pageProof = new OperationProof(1, Protocol.ProductId, licenseId,
        rateDeviceId, rateSessionId, ContentProtocol.CatalogReadAction,
        pageContextHash, new string('3', 64), new string('A', 344));
    Equal(true, pagedLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
        pageChallenge), "content challenge page allowance");
    Equal(true, pagedLimiter.Allow("127.0.0.1",
        "/v1/suite-content/catalog/current", new CatalogPageProof(pageProof, pageContext)),
        "content catalog page allowance");
}
for (var i = 0; i < 30; i++)
    Equal(true, pagedLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
        new ChallengeRequest(1, Protocol.ProductId, licenseId, rateDeviceId,
            rateSessionId, "session.open", new string('3', 64))),
        "core rate remains isolated from content");
Equal(false, pagedLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
    new ChallengeRequest(1, Protocol.ProductId, licenseId, rateDeviceId,
        rateSessionId, "session.open", new string('3', 64))),
    "core rate still rejects request 31");
var floodLimiter = new SuiteRateLimiter(rateClock);
var oversized = new string('x', 100_000);
for (var i = 0; i < 100; i++)
    Equal(false, floodLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
        new ChallengeRequest(1, Protocol.ProductId, oversized, oversized,
            rateSessionId, "session.open", new string('3', 64))),
        "oversized rate identity rejected without tracking");
Equal(0, floodLimiter.TrackedWindowCount, "oversized rate flood has no state growth");
Equal(false, floodLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
    new ChallengeRequest(1, Protocol.ProductId, licenseId, rateDeviceId,
        rateSessionId, "unrecognized.action", new string('3', 64))),
    "unrecognized action is rejected before limiter state allocation");
Equal(0, floodLimiter.TrackedWindowCount,
    "unrecognized action has no limiter state growth");
for (var i = 0; i < SuiteRateLimiter.MaximumIdentitiesPerOriginPerBucket + 32; i++)
{
    var uniqueLicense = $"TR-{i:D8}";
    var uniqueDevice = i.ToString("x64");
    _ = floodLimiter.Allow("127.0.0.1", "/v1/suite/challenges",
        new ChallengeRequest(1, Protocol.ProductId, uniqueLicense, uniqueDevice,
            rateSessionId, "session.open", new string('3', 64)));
}
Equal(SuiteRateLimiter.MaximumIdentitiesPerOriginPerBucket + 1,
    floodLimiter.TrackedWindowCount,
    "one origin cannot fill the global core rate window cardinality cap");
Equal(true, floodLimiter.Allow("192.0.2.200", "/v1/suite/challenges",
    new ChallengeRequest(1, Protocol.ProductId, "TR-LEGIT-0001",
        new string('f', 64), rateSessionId, "session.open", new string('3', 64))),
    "a legitimate second origin remains available after one-origin identity flood");
Equal(SuiteRateLimiter.MaximumIdentitiesPerOriginPerBucket + 3,
    floodLimiter.TrackedWindowCount,
    "second origin receives its own fixed-cardinality budget");
var globalRateLimiter = new SuiteRateLimiter(rateClock);
for (var i = 0; i < SuiteRateLimiter.MaximumTrackedWindows / 2 + 32; i++)
    _ = globalRateLimiter.Allow($"2001:db8:{i >> 16:x}:{i & 0xffff:x}::1",
        "/v1/suite/challenges",
        new ChallengeRequest(1, Protocol.ProductId, $"TR-GLOBAL-{i:D6}",
            i.ToString("x64"), rateSessionId, "session.open", new string('3', 64)));
Equal(SuiteRateLimiter.MaximumTrackedWindows / 2,
    globalRateLimiter.TrackedWindowCount,
    "control-plane core limiter global cardinality budget");
Equal(true, globalRateLimiter.Allow("198.51.100.200", "/v1/suite/challenges",
    new ChallengeRequest(1, Protocol.ProductId, "TR-AFTER-FLOOD",
        new string('e', 64), rateSessionId, "session.open", new string('3', 64))),
    "control-plane global cardinality uses bounded eviction, not denial");
Equal(SuiteRateLimiter.MaximumTrackedWindows / 2,
    globalRateLimiter.TrackedWindowCount,
    "control-plane eviction keeps its fixed memory budget");

var gatewayLimiter = new GatewayRateLimiter(rateClock);
var sharedGrant = new string('a', 64);
for (var client = 1; client <= 12; client++)
    Equal(true, gatewayLimiter.Allow($"192.0.2.{client}", sharedGrant),
        "grant allowance across distinct trusted client addresses");
Equal(false, gatewayLimiter.Allow("192.0.2.13", sharedGrant),
    "grant rate is independent of client address");
var invalidGatewayCount = gatewayLimiter.TrackedWindowCount;
for (var i = 0; i < 100; i++)
    Equal(false, gatewayLimiter.Allow("127.0.0.1", oversized),
        "oversized grant id rejected without tracking");
Equal(invalidGatewayCount, gatewayLimiter.TrackedWindowCount,
    "oversized gateway flood has no state growth");
var gatewayFlood = new GatewayRateLimiter(rateClock);
for (var i = 0; i < GatewayRateLimiter.MaximumTrackedWindows / 2 + 32; i++)
    _ = gatewayFlood.Allow("127.0.0.1", i.ToString("x64"));
Equal(121, gatewayFlood.TrackedWindowCount,
    "client cap prevents one address from filling grant cardinality");
var gatewayGlobalFlood = new GatewayRateLimiter(rateClock);
for (var i = 0; i < GatewayRateLimiter.MaximumTrackedWindows / 2 + 32; i++)
    _ = gatewayGlobalFlood.Allow($"2001:db8:{i >> 16:x}:{i & 0xffff:x}::2",
        i.ToString("x64"));
Equal(GatewayRateLimiter.MaximumTrackedWindows,
    gatewayGlobalFlood.TrackedWindowCount,
    "gateway limiter global client and grant cardinality caps");
Equal(true, gatewayGlobalFlood.Allow("198.51.100.201", new string('f', 64)),
    "gateway cardinality flood cannot deny a subsequent legitimate grant");
Equal(GatewayRateLimiter.MaximumTrackedWindows,
    gatewayGlobalFlood.TrackedWindowCount,
    "gateway bounded eviction keeps its fixed memory budget");
var proofLimiter = new GatewayRateLimiter(rateClock);
for (var i = 0; i < 30; i++)
    Equal(true, proofLimiter.AllowKeyRingProof("127.0.0.1"),
        "loopback keyring proof allowance");
Equal(false, proofLimiter.AllowKeyRingProof("127.0.0.1"),
    "loopback keyring proof cap");
Equal(false, new GatewayRateLimiter(rateClock).AllowKeyRingProof("192.0.2.1"),
    "non-loopback keyring proof denied");
ContentGatewayUpstreamPolicy.RequireExpectedStatus(HttpStatusCode.OK, 0);
ContentGatewayUpstreamPolicy.RequireExpectedStatus(HttpStatusCode.PartialContent, 1);
Expect<SuiteException>(() => ContentGatewayUpstreamPolicy.RequireExpectedStatus(
    HttpStatusCode.OK, 1),
    "range resume rejects upstream full response before forwarding bytes");
Expect<SuiteException>(() => ContentGatewayUpstreamPolicy.RequireExpectedStatus(
    HttpStatusCode.PartialContent, 0),
    "full transfer rejects unexpected partial response");
Equal(false, ContentGatewayRevalidationPolicy.IsDue(
    TimeSpan.FromSeconds(4.999), ContentGatewayRevalidationPolicy.MaximumBytes - 1),
    "content stream revalidation remains below both boundaries");
Equal(true, ContentGatewayRevalidationPolicy.IsDue(
    ContentGatewayRevalidationPolicy.MaximumInterval, 1),
    "content stream revalidates at five seconds");
Equal(true, ContentGatewayRevalidationPolicy.IsDue(
    TimeSpan.Zero, ContentGatewayRevalidationPolicy.MaximumBytes),
    "content stream revalidates at eight MiB");
await ExpectAsync<SuiteException>(() =>
    ContentGatewayRevalidationPolicy.RequireCurrentAsync(
        _ => Task.FromResult(false), TimeSpan.FromMilliseconds(10), default),
    "content stream aborts after authorization denial");
await ExpectAsync<SuiteException>(() =>
    ContentGatewayRevalidationPolicy.RequireCurrentAsync(
        async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        }, TimeSpan.FromMilliseconds(10), default),
    "content stream aborts after authorization timeout");
var grantPepperLimiter = new GatewayRateLimiter(rateClock);
for (var i = 0; i < 120; i++)
    Equal(true, grantPepperLimiter.AllowGrantPepperProof("::1"),
        "loopback grant pepper proof allowance");
Equal(false, grantPepperLimiter.AllowGrantPepperProof("::1"),
    "loopback grant pepper proof cap");
var grantPepperBytes = RandomNumberGenerator.GetBytes(32);
var otherGrantPepperBytes = RandomNumberGenerator.GetBytes(32);
var grantProofNonce = RandomNumberGenerator.GetBytes(32);
var grantProof = Array.Empty<byte>();
try
{
    using var controlGrantHasher = new ContentGrantTokenHasher(
        Convert.ToBase64String(grantPepperBytes));
    using var gatewayGrantHasher = new ContentGrantTokenHasher(
        Convert.ToBase64String(grantPepperBytes));
    using var wrongGatewayGrantHasher = new ContentGrantTokenHasher(
        Convert.ToBase64String(otherGrantPepperBytes));
    grantProof = controlGrantHasher.CreateGatewayReadinessProof(grantProofNonce);
    Equal(true, gatewayGrantHasher.VerifyGatewayReadinessProof(
        grantProofNonce, grantProof), "control and gateway grant pepper proof");
    Equal(false, wrongGatewayGrantHasher.VerifyGatewayReadinessProof(
        grantProofNonce, grantProof), "grant pepper proof rejects different material");
    Equal(new Uri("http://127.0.0.1:5191/ready/grant-pepper/prove"),
        ContentGatewayPepperVerifier.RequireLoopbackProofUri(
            new Uri("http://127.0.0.1:5191/ready/grant-pepper/prove")),
        "grant pepper proof endpoint is exact loopback");
    Equal(new Uri("http://127.0.0.1:5191/ready"),
        ContentGatewayPepperVerifier.RequireLoopbackReadyUri(
            new Uri("http://127.0.0.1:5191/ready")),
        "deployment readiness endpoint is exact loopback");
    Expect<InvalidOperationException>(() =>
        ContentGatewayPepperVerifier.RequireLoopbackProofUri(
            new Uri("https://example.invalid/ready/grant-pepper/prove")),
        "external grant pepper proof endpoint rejected");
    Expect<InvalidOperationException>(() =>
        ContentGatewayPepperVerifier.RequireLoopbackReadyUri(
            new Uri("http://127.0.0.1:5191/health")),
        "wrong loopback deployment readiness path rejected");
}
finally
{
    CryptographicOperations.ZeroMemory(grantPepperBytes);
    CryptographicOperations.ZeroMemory(otherGrantPepperBytes);
    CryptographicOperations.ZeroMemory(grantProofNonce);
    if (grantProof.Length != 0) CryptographicOperations.ZeroMemory(grantProof);
}

var pepper = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); var code = "test-activation-code"; var verifier = ActivationCodes.Verify(pepper, code);
var store = new MemoryStore(new LicenseRecord(licenseId, Protocol.ProductId, "ACTIVE", verifier, 2_000_000_000, false), device); var clock = new ManualTime(1_800_000_000); using var signer = new RsaAssertionSigner(online); var serviceA = new SuiteService(store, signer, clock, pepper); var serviceB = new SuiteService(store, signer, clock, pepper);
await ExpectAsync<SuiteException>(() => serviceA.ActivationChallengeAsync(new(1, Protocol.ProductId, licenseId, code, device with { HardwareFingerprint = new string('b', 64) }), default), "unenrolled device");
var nonLifetime = new SuiteService(new MemoryStore(new LicenseRecord(licenseId, Protocol.ProductId, "ACTIVE", verifier, 2_000_000_000, false, "TERM"), device), signer, clock, pepper);
await ExpectAsync<SuiteException>(() => nonLifetime.ActivationChallengeAsync(new(1, Protocol.ProductId, licenseId, code, device), default), "non-lifetime license");
var issued = await serviceA.ActivationChallengeAsync(new(1, Protocol.ProductId, licenseId, code, device), default); var issuedPayload = Payload<ActivationChallengeAssertion>(issued); var activationChallenge = new ChallengeResponse(1, issuedPayload.ChallengeId, issuedPayload.Nonce, issuedPayload.ExpiresAtUnixSeconds);
var activationSignature = Sign(machine, activationChallenge, licenseId, deviceId, "", "device.activate", activation);
var proof = new ActivationProof(1, Protocol.ProductId, licenseId, issuedPayload.ChallengeId, device, activationSignature); var completed = await serviceB.CompleteActivationAsync(proof, default); var retry = await serviceA.CompleteActivationAsync(proof, default); Equal(completed.Signature, retry.Signature, "idempotent activation");
await ExpectAsync<SuiteException>(() => serviceA.CompleteActivationAsync(proof with { Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) }, default), "different replay");
var raceInner = new MemoryStore(new LicenseRecord(licenseId, Protocol.ProductId, "ACTIVE", verifier, 2_000_000_000, false), device);
var raceStore = new CoordinatedActivationStore(raceInner); var raceService = new SuiteService(raceStore, signer, clock, pepper);
var raceIssued = Payload<ActivationChallengeAssertion>(await raceService.ActivationChallengeAsync(new(1, Protocol.ProductId, licenseId, code, device), default));
var raceChallenge = new ChallengeResponse(1, raceIssued.ChallengeId, raceIssued.Nonce, raceIssued.ExpiresAtUnixSeconds);
var raceProof = new ActivationProof(1, Protocol.ProductId, licenseId, raceIssued.ChallengeId, device, Sign(machine, raceChallenge, licenseId, deviceId, "", "device.activate", activation));
var delayedB = raceService.CompleteActivationAsync(raceProof, default);
await raceStore.FirstCompletionObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
var winnerA = await raceService.CompleteActivationAsync(raceProof, default);
raceStore.ReleaseFirstCompletion.TrySetResult();
var recoveredB = await delayedB;
Equal(winnerA.Signature, recoveredB.Signature, "deterministic service race returns identical completion");
await ExpectSuiteAsync(() => raceService.CompleteActivationAsync(raceProof with { Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) }, default), "REPLAY_DENIED", "deterministic divergent replay");
await ExpectSuiteAsync(() => raceService.CompleteActivationAsync(raceProof with { ChallengeId = new string('f', 64) }, default), "CHALLENGE_INVALID", "missing challenge without completion");

foreach (var contentAction in new[]
         {
             ContentProtocol.CatalogReadAction,
             ContentProtocol.DownloadAuthorizeAction
         })
{
    var invalidSessionContext = sessionContext with { Action = contentAction };
    var invalidSessionHash = Protocol.SessionContextHash(invalidSessionContext);
    var invalidSessionProof = new OperationProof(1, Protocol.ProductId, licenseId,
        deviceId, sessionId, contentAction, invalidSessionHash, new string('e', 64),
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)));
    await ExpectSuiteAsync(() => serviceA.SessionAsync(
        new SessionProof(invalidSessionProof, invalidSessionContext), default),
        "ACTION_INVALID", "content action cannot enter session completion");
}

var contextHash = Protocol.SessionContextHash(sessionContext); var challengeEnvelope = await serviceA.ChallengeAsync(new(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.open", contextHash), default); var operation = Payload<OperationChallengeAssertion>(challengeEnvelope); var operationChallenge = new ChallengeResponse(1, operation.ChallengeId, operation.Nonce, operation.ExpiresAtUnixSeconds); var operationProof = new OperationProof(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.open", contextHash, operation.ChallengeId, Sign(machine, operationChallenge, licenseId, deviceId, sessionId, "session.open", contextHash));
await ExpectSuiteAsync(() => serviceA.ChallengeAsync(new(1, Protocol.ProductId,
    licenseId, deviceId, sessionId, ContentProtocol.CatalogReadAction,
    new string('a', 64)), default), "SESSION_INVALID",
    "content challenge requires an active session");
_ = await serviceB.SessionAsync(new(operationProof, sessionContext), default); await ExpectAsync<SuiteException>(() => serviceA.SessionAsync(new(operationProof, sessionContext), default), "atomic challenge consumption");
_ = await serviceA.ChallengeAsync(new(1, Protocol.ProductId, licenseId, deviceId,
    sessionId, ContentProtocol.CatalogReadAction, new string('a', 64)), default);
await ExpectSuiteAsync(() => serviceA.ChallengeAsync(new(1, Protocol.ProductId,
    licenseId, deviceId, new string('f', 64), ContentProtocol.DownloadAuthorizeAction,
    new string('b', 64)), default), "SESSION_INVALID",
    "content challenge rejects a rotated caller session id");
var heartbeatContext = sessionContext with { Action = "session.heartbeat" }; var heartbeatHash = Protocol.SessionContextHash(heartbeatContext); var heartbeatEnvelope = await serviceA.ChallengeAsync(new(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.heartbeat", heartbeatHash), default); var heartbeat = Payload<OperationChallengeAssertion>(heartbeatEnvelope); var heartbeatChallenge = new ChallengeResponse(1, heartbeat.ChallengeId, heartbeat.Nonce, heartbeat.ExpiresAtUnixSeconds); var heartbeatProof = new OperationProof(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.heartbeat", heartbeatHash, heartbeat.ChallengeId, Sign(machine, heartbeatChallenge, licenseId, deviceId, sessionId, "session.heartbeat", heartbeatHash));
var contenders = new[] { serviceA.SessionAsync(new(heartbeatProof, heartbeatContext), default), serviceB.SessionAsync(new(heartbeatProof, heartbeatContext), default) }; try { await Task.WhenAll(contenders); } catch (SuiteException) { }
Equal(1, contenders.Count(task => task.Status == TaskStatus.RanToCompletion), "concurrent heartbeat has one winner");
Console.WriteLine("SUITE TESTS: OK (golden vectors, strict JSON, cross-product, restart/shared store, idempotency, replay, concurrent heartbeat)");

static T Payload<T>(SignedAssertionEnvelope envelope) where T : class => StrictJson.Parse<T>(Convert.FromBase64String(envelope.Payload));
static string Sign(RSA rsa, ChallengeResponse c, string l, string d, string s, string a, string h) { var m = Protocol.SigningMessage(c, l, d, s, a, h); try { return Convert.ToBase64String(rsa.SignData(m, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)); } finally { CryptographicOperations.ZeroMemory(m); } }
static string Sha(ReadOnlySpan<byte> b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
static void Equal<T>(T expected, T actual, string label) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected {expected}, got {actual}"); }
static void Expect<T>(Action action, string label) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException(label + " was accepted"); }
static async Task ExpectAsync<T>(Func<Task> action, string label) where T : Exception { try { await action(); } catch (T) { return; } throw new InvalidOperationException(label + " was accepted"); }
static async Task ExpectSuiteAsync(Func<Task> action, string code, string label) { try { await action(); } catch (SuiteException ex) when (ex.Code == code) { return; } throw new InvalidOperationException(label + " did not return " + code); }

sealed class ManualTime(long unix) : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unix); }
sealed class MemoryStore(LicenseRecord license, DeviceDescriptor enrolled) : ISuiteStore
{
    private LicenseRecord _license = license; private readonly ConcurrentDictionary<string, ChallengeRecord> _challenges = new(); private readonly ConcurrentDictionary<string, CompletionRecord> _completions = new(); private readonly ConcurrentDictionary<string, DeviceRecord> _devices = new(); private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    public Task<LicenseRecord?> FindLicenseAsync(string id, CancellationToken _) => Task.FromResult<LicenseRecord?>(_license.LicenseId == id ? _license : null);
    public Task<EnrollmentRecord?> FindEnrollmentAsync(string id, CancellationToken _) => Task.FromResult<EnrollmentRecord?>(_license.LicenseId == id ? new(id, enrolled.DeviceId, enrolled.BindingType, "SOFTWARE_ONLY", enrolled.Algorithm, enrolled.PublicKeySpki, enrolled.HardwareFingerprint) : null);
    public Task<DeviceRecord?> FindDeviceAsync(string l, string d, CancellationToken _) { _devices.TryGetValue(l + ":" + d, out var value); return Task.FromResult<DeviceRecord?>(value); }
    public Task<bool> IsActiveSessionAsync(string l, string d, string s, long now,
        CancellationToken _)
    {
        _sessions.TryGetValue(l + ":" + d, out var value);
        return Task.FromResult(value is { Status: "ACTIVE" } && value.SessionId == s &&
            value.AuthorizedUntil > now && value.RevocationGeneration == _license.RevocationGeneration);
    }
    public Task InsertChallengeAsync(ChallengeRecord c, CancellationToken _) { if (!_challenges.TryAdd(c.ChallengeId, c)) throw new InvalidOperationException(); return Task.CompletedTask; }
    public Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken _) { if (_challenges.TryGetValue(id, out var c) && c.Action == action && c.ExpiresAt > now) return Task.FromResult<ChallengeRecord?>(c); return Task.FromResult<ChallengeRecord?>(null); }
    public Task<CompletionRecord?> FindCompletionAsync(string id, CancellationToken _) { _completions.TryGetValue(id, out var c); return Task.FromResult<CompletionRecord?>(c); }
    public Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord c, string digest, DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct) { _ = ct; lock (this) { if (_completions.TryGetValue(c.ChallengeId, out var prior)) { if (prior.RequestDigest != digest) throw new SuiteException(409, "REPLAY_DENIED", "Replay was denied."); return Task.FromResult(prior.Result); } if (!_challenges.TryRemove(c.ChallengeId, out _) || _license.ActivationConsumed) throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available."); _license = _license with { ActivationConsumed = true }; _devices[d.LicenseId + ":" + d.DeviceId] = d; _completions[c.ChallengeId] = new(c.ChallengeId, digest, result); return Task.FromResult(result); } }
    public Task<SessionRecord> CompleteSessionAsync(ChallengeRecord c, SessionRecord s, string action, long now, CancellationToken ct) { _ = ct; lock (this) { if (!_challenges.TryRemove(c.ChallengeId, out _)) throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired."); var key = s.LicenseId + ":" + s.DeviceId; if (_sessions.TryGetValue(key, out var old) && action != "session.open" && old.SessionId != s.SessionId) throw new SuiteException(409, "SESSION_INVALID", "Session is not current."); var time = Math.Max(now, (old?.LastServerTime ?? 0) + 1); var value = s with { LastServerTime = time }; _sessions[key] = value; return Task.FromResult(value); } }
}

sealed class CoordinatedActivationStore(ISuiteStore inner) : ISuiteStore
{
    private int completionCalls;
    public TaskCompletionSource FirstCompletionObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseFirstCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<LicenseRecord?> FindLicenseAsync(string id, CancellationToken ct) => inner.FindLicenseAsync(id, ct);
    public Task<EnrollmentRecord?> FindEnrollmentAsync(string id, CancellationToken ct) => inner.FindEnrollmentAsync(id, ct);
    public Task<DeviceRecord?> FindDeviceAsync(string l, string d, CancellationToken ct) => inner.FindDeviceAsync(l, d, ct);
    public Task<bool> IsActiveSessionAsync(string l, string d, string s, long now,
        CancellationToken ct) => inner.IsActiveSessionAsync(l, d, s, now, ct);
    public Task InsertChallengeAsync(ChallengeRecord c, CancellationToken ct) => inner.InsertChallengeAsync(c, ct);
    public Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken ct) => inner.FindChallengeAsync(id, action, now, ct);
    public async Task<CompletionRecord?> FindCompletionAsync(string id, CancellationToken ct)
    {
        var observed = await inner.FindCompletionAsync(id, ct);
        if (Interlocked.Increment(ref completionCalls) == 1)
        {
            FirstCompletionObserved.TrySetResult();
            await ReleaseFirstCompletion.Task.WaitAsync(ct);
        }
        return observed;
    }
    public Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord c, string digest, DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct) => inner.CompleteActivationAsync(c, digest, d, result, ct);
    public Task<SessionRecord> CompleteSessionAsync(ChallengeRecord c, SessionRecord s, string action, long now, CancellationToken ct) => inner.CompleteSessionAsync(c, s, action, now, ct);
}

sealed class GatewayStoreStub(bool ready) : IContentGatewayStore
{
    public int ClaimCalls { get; private set; }

    public Task<bool> IsProductionGatewayReadyAsync(int activeKeyVersion,
        string keySetFingerprint, string allowlistFingerprint,
        int[] supportedKeyVersions, CancellationToken cancellationToken)
    {
        _ = activeKeyVersion;
        _ = keySetFingerprint;
        _ = allowlistFingerprint;
        _ = supportedKeyVersions;
        _ = cancellationToken;
        return Task.FromResult(ready);
    }

    public Task<ClaimedContentGrantRecord> ClaimDownloadGrantAsync(string grantId,
        string tokenDigest, long requestedRangeStart, long now,
        CancellationToken cancellationToken)
    {
        _ = grantId;
        _ = tokenDigest;
        _ = requestedRangeStart;
        _ = now;
        _ = cancellationToken;
        ClaimCalls++;
        throw new InvalidOperationException("claim must not be reached");
    }

    public Task CompleteDownloadGrantAsync(string grantId, bool succeeded,
        string? failureCode, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<bool> IsDownloadGrantAuthorizationCurrentAsync(string grantId,
        long now, CancellationToken cancellationToken) => Task.FromResult(false);
}
