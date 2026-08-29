using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using TurboRamaSuiteContentAuthorityTool;

const long Now = 2_000_000_000;
const string GoldenContentKeyId =
    "85db9d405626bb3c03347880a2d0227951c8914ce083067f4c9633fb831506b6";
const string GoldenSigningMessageSha256 =
    "5074216342f7b284f09cf24446ff7630d920ba4760643f74e38163d332217152";
const string GoldenContentSpkiBase64 =
    "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA35/mjFW3+9ZZUV9UTH0G" +
    "bkjwAzOfCqQoWLNB4JObNd7yi6Gfmi7toNbhfez3fGN8/Jr3j849F16q7oFD9y" +
    "sQyCQ79fwFWZfDemckS5B/VWuEyx9Uj0rPXaXMeEMv4V8/bsyQm+nVdRZxCqhv" +
    "/ssoWDXq9QJxGFoypAGqGX3ngzJ8qqaESTMCryYednPgCFIu8/hEMDJ3eACLcZ" +
    "ef5gx0tVT6ZBGvY47ZYz8/SomkTP73nGbH2dp1PpnrIpswGmKWOnvN5bgBx8cr" +
    "kDj3LidKY+7YDyctXgh1pzP5TflNitjXCMVr+IS5ag7rQ9LzBMRogwe93F7v72" +
    "nPhTjQs+8rdQIDAQAB";

var tests = new (string Name, Action Body)[]
{
    ("golden canonical payload and signing domain", GoldenCanonicalPayload),
    ("round trip and idempotent immutable output", RoundTripAndIdempotency),
    ("tamper and independent hash rejection", TamperIsRejected),
    ("invalid inputs fail closed", InvalidInputsFailClosed)
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS: {test.Name}");
}

Console.WriteLine("CONTENT AUTHORITY TOOL TESTS: OK");

static void GoldenCanonicalPayload()
{
    var contentSpki = Convert.FromBase64String(GoldenContentSpkiBase64);
    try
    {
        var keyId = ContentAuthorityProtocol.KeyIdFromSpki(contentSpki);
        Check(keyId == GoldenContentKeyId,
            "Golden content SPKI KeyId changed.");
        var escapedSpki = GoldenContentSpkiBase64.Replace(
            "+", "\\u002B", StringComparison.Ordinal);
        var payload = new ContentAuthorityPayload(
            1,
            "TURBORAMA_SUITE_CONTENT_AUTHORITY",
            "TURBORAMA_SUITE",
            "https://content.example.invalid/",
            "rsa-pss-sha256",
            keyId,
            GoldenContentSpkiBase64,
            new string('a', 64),
            new string('b', 64),
            1_999_999_940,
            2_000_003_600);
        var expected =
            "{\"schemaVersion\":1," +
            "\"kind\":\"TURBORAMA_SUITE_CONTENT_AUTHORITY\"," +
            "\"productId\":\"TURBORAMA_SUITE\"," +
            "\"baseUrl\":\"https://content.example.invalid/\"," +
            "\"contentAssertionAlgorithm\":\"rsa-pss-sha256\"," +
            $"\"contentAssertionKeyId\":\"{keyId}\"," +
            $"\"contentAssertionPublicKeySpki\":\"{escapedSpki}\"," +
            $"\"tlsServerSpkiSha256Current\":\"{new string('a', 64)}\"," +
            $"\"tlsServerSpkiSha256Next\":\"{new string('b', 64)}\"," +
            "\"issuedAtUnixSeconds\":1999999940," +
            "\"expiresAtUnixSeconds\":2000003600}";
        var canonical = ContentAuthorityProtocol.CanonicalPayload(payload);
        var message = ContentAuthorityProtocol.BuildSigningMessage(payload);
        var expectedCanonical = Encoding.UTF8.GetBytes(expected);
        var domain = Encoding.ASCII.GetBytes(
            "TurboRamaSuiteContentAuthorityConfiguration/v1\0");
        try
        {
            Equal(expectedCanonical, canonical,
                "Canonical payload drifted from the client contract.");
            Check(message.AsSpan(0, domain.Length).SequenceEqual(domain) &&
                  message.AsSpan(domain.Length).SequenceEqual(expectedCanonical),
                "Signing message domain or payload order drifted.");
            Check(ContentAuthorityProtocol.LowerSha256(message) ==
                  GoldenSigningMessageSha256,
                "Golden signing-message SHA-256 changed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(message);
            CryptographicOperations.ZeroMemory(expectedCanonical);
            CryptographicOperations.ZeroMemory(domain);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(contentSpki);
    }
}

static void RoundTripAndIdempotency()
{
    using var fixture = new Fixture();
    var request = fixture.Request();
    var first = AuthorityArtifactGenerator.Generate(request, fixture.TimeProvider);
    Check(!first.ReusedExistingArtifact, "First generation unexpectedly reused output.");
    var envelopePath = Path.Combine(
        fixture.OutputDirectory, AuthorityArtifactNames.Envelope);
    var issuerPath = Path.Combine(
        fixture.OutputDirectory, AuthorityArtifactNames.IssuerSpki);
    var firstEnvelope = File.ReadAllBytes(envelopePath);
    try
    {
        var verified = AuthorityArtifactVerifier.Verify(
            new AuthorityVerificationRequest(
                envelopePath,
                issuerPath,
                first.EnvelopeSha256,
                first.IssuerSpkiSha256),
            fixture.TimeProvider);
        Check(verified.Payload.BaseUrl == request.BaseUrl &&
              verified.Payload.ContentAssertionKeyId == fixture.ContentKeyId,
            "Verified payload did not preserve generation inputs.");

        var second = AuthorityArtifactGenerator.Generate(request, fixture.TimeProvider);
        var secondEnvelope = File.ReadAllBytes(envelopePath);
        try
        {
            Check(second.ReusedExistingArtifact,
                "Second generation did not reuse the immutable approved set.");
            Equal(firstEnvelope, secondEnvelope,
                "Idempotent generation changed RSA-PSS artifact bytes.");
            Check(first.EnvelopeSha256 == second.EnvelopeSha256 &&
                  first.IssuerSpkiSha256 == second.IssuerSpkiSha256,
                "Idempotent generation changed artifact hashes.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secondEnvelope);
        }

        ExpectFailure(
            () => AuthorityArtifactGenerator.Generate(
                request with { BaseUrl = "https://other.example.invalid/" },
                fixture.TimeProvider),
            "Existing output accepted different generation inputs.");
        Equal(firstEnvelope, File.ReadAllBytes(envelopePath),
            "Rejected overwrite changed the approved envelope.");
        VerifySidecar(
            fixture.OutputDirectory,
            AuthorityArtifactNames.EnvelopeSha256,
            first.EnvelopeSha256,
            AuthorityArtifactNames.Envelope);
        VerifySidecar(
            fixture.OutputDirectory,
            AuthorityArtifactNames.IssuerSpkiSha256,
            first.IssuerSpkiSha256,
            AuthorityArtifactNames.IssuerSpki);
        var unexpectedPath = Path.Combine(
            fixture.OutputDirectory, "unexpected-public-file.txt");
        File.WriteAllText(unexpectedPath, "unexpected");
        try
        {
            ExpectFailure(
                () => AuthorityArtifactGenerator.Generate(
                    request, fixture.TimeProvider),
                "Existing output with an unexpected file was reused.");
        }
        finally
        {
            File.Delete(unexpectedPath);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(firstEnvelope);
    }
}

static void TamperIsRejected()
{
    using var fixture = new Fixture();
    var generated = AuthorityArtifactGenerator.Generate(
        fixture.Request(), fixture.TimeProvider);
    var envelope = File.ReadAllBytes(Path.Combine(
        fixture.OutputDirectory, AuthorityArtifactNames.Envelope));
    var issuerSpki = File.ReadAllBytes(Path.Combine(
        fixture.OutputDirectory, AuthorityArtifactNames.IssuerSpki));
    try
    {
        var hashTamper = envelope.ToArray();
        hashTamper[^1] ^= 1;
        try
        {
            ExpectFailure(
                () => AuthorityArtifactVerifier.VerifyBytes(
                    hashTamper,
                    issuerSpki,
                    generated.EnvelopeSha256,
                    generated.IssuerSpkiSha256,
                    Now),
                "Tampered envelope bypassed its independently approved hash.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hashTamper);
        }

        var parsed = JsonSerializer.Deserialize<ContentAuthorityEnvelope>(
            envelope,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Test envelope was empty.");
        var payloadBytes = Convert.FromBase64String(parsed.Payload);
        try
        {
            var marker = Encoding.ASCII.GetBytes("content.example.invalid");
            var offset = payloadBytes.AsSpan().IndexOf(marker);
            Check(offset >= 0, "Could not locate test URL inside canonical payload.");
            payloadBytes[offset] = (byte)'x';
            var forged = ContentAuthorityProtocol.CanonicalEnvelope(
                parsed with { Payload = Convert.ToBase64String(payloadBytes) });
            try
            {
                ExpectFailure(
                    () => AuthorityArtifactVerifier.VerifyBytes(
                        forged,
                        issuerSpki,
                        ContentAuthorityProtocol.LowerSha256(forged),
                        generated.IssuerSpkiSha256,
                        Now),
                    "Tampered payload with a recomputed hash bypassed RSA-PSS.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(forged);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payloadBytes);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(envelope);
        CryptographicOperations.ZeroMemory(issuerSpki);
    }
}

static void InvalidInputsFailClosed()
{
    using var fixture = new Fixture();
    var request = fixture.Request();
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                BaseUrl = "http://content.example.invalid/",
                OutputDirectory = fixture.NewOutput("http")
            },
            fixture.TimeProvider),
        "HTTP base URL was accepted.");
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                BaseUrl = "https://content.example.invalid/prefix/",
                OutputDirectory = fixture.NewOutput("path-prefix")
            },
            fixture.TimeProvider),
        "A path-prefixed base URL was accepted.");
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                TlsServerSpkiSha256Current = new string('A', 64),
                OutputDirectory = fixture.NewOutput("uppercase")
            },
            fixture.TimeProvider),
        "Uppercase TLS pin was accepted.");
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                TlsServerSpkiSha256Next = request.TlsServerSpkiSha256Current,
                OutputDirectory = fixture.NewOutput("duplicate-pin")
            },
            fixture.TimeProvider),
        "Duplicate TLS pin was accepted.");
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                ExpiresAtUnixSeconds = request.IssuedAtUnixSeconds +
                    ContentAuthorityProtocol.MaximumValiditySeconds + 1,
                OutputDirectory = fixture.NewOutput("long-validity")
            },
            fixture.TimeProvider),
        "Overlong authority validity was accepted.");
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                IssuerPrivateKeyPemPath = Path.Combine(fixture.Root, "missing.pem"),
                OutputDirectory = fixture.NewOutput("missing-key")
            },
            fixture.TimeProvider),
        "Missing issuer private key was accepted.");

    using var broadIssuer = RSA.Create(2048);
    var broadKeyPath = Path.Combine(fixture.Root, "broad-private.pem");
    TestSupport.WritePrivatePem(broadIssuer, broadKeyPath, ownerOnly: false);
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                IssuerPrivateKeyPemPath = broadKeyPath,
                OutputDirectory = fixture.NewOutput("broad-key-acl")
            },
            fixture.TimeProvider),
        "Issuer private key with broad permissions was accepted.");

    using var weakIssuer = RSA.Create(1024);
    var weakKeyPath = Path.Combine(fixture.Root, "weak-private.pem");
    TestSupport.WritePrivatePem(weakIssuer, weakKeyPath);
    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                IssuerPrivateKeyPemPath = weakKeyPath,
                OutputDirectory = fixture.NewOutput("weak-key")
            },
            fixture.TimeProvider),
        "RSA issuer below 2048 bits was accepted.");

    using var weakContent = RSA.Create(1024);
    var weakContentPath = Path.Combine(fixture.Root, "weak-content.der");
    var weakContentSpki = weakContent.ExportSubjectPublicKeyInfo();
    try
    {
        File.WriteAllBytes(weakContentPath, weakContentSpki);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(weakContentSpki);
    }

    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                ContentAssertionPublicSpkiPath = weakContentPath,
                OutputDirectory = fixture.NewOutput("weak-content-key")
            },
            fixture.TimeProvider),
        "RSA content assertion key below 2048 bits was accepted.");

    var issuerPublicPath = Path.Combine(fixture.Root, "issuer-public.der");
    var issuerPublic = fixture.Issuer.ExportSubjectPublicKeyInfo();
    try
    {
        File.WriteAllBytes(issuerPublicPath, issuerPublic);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(issuerPublic);
    }

    ExpectFailure(
        () => AuthorityArtifactGenerator.Generate(
            request with
            {
                ContentAssertionPublicSpkiPath = issuerPublicPath,
                OutputDirectory = fixture.NewOutput("same-key")
            },
            fixture.TimeProvider),
        "Offline issuer was accepted as the online assertion key.");
}

static void VerifySidecar(
    string directory,
    string sidecar,
    string hash,
    string artifact)
{
    var expected = Encoding.UTF8.GetBytes($"{hash}  {artifact}\n");
    var actual = File.ReadAllBytes(Path.Combine(directory, sidecar));
    try
    {
        Equal(expected, actual, "SHA-256 sidecar is not exact sha256sum format.");
    }
    finally
    {
        CryptographicOperations.ZeroMemory(expected);
        CryptographicOperations.ZeroMemory(actual);
    }
}

static void ExpectFailure(Action action, string message)
{
    try
    {
        action();
    }
    catch (AuthorityArtifactException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Equal(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual,
    string message)
{
    if (!expected.SequenceEqual(actual))
    {
        var firstDifference = 0;
        while (firstDifference < Math.Min(expected.Length, actual.Length) &&
               expected[firstDifference] == actual[firstDifference])
        {
            firstDifference++;
        }

        throw new InvalidOperationException(
            $"{message} First difference: {firstDifference}; " +
            $"expected length: {expected.Length}; actual length: {actual.Length}.");
    }
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

static class TestSupport
{
    internal const long Now = 2_000_000_000;

    internal static void WritePrivatePem(
        RSA rsa,
        string path,
        bool ownerOnly = true)
    {
        var privateKey = rsa.ExportPkcs8PrivateKey();
        var pemCharacters = PemEncoding.Write("PRIVATE KEY", privateKey);
        var pemBytes = Encoding.UTF8.GetBytes(pemCharacters);
        try
        {
            File.WriteAllBytes(path, pemBytes);
            if (OperatingSystem.IsWindows())
            {
                SetWindowsAcl(path, ownerOnly);
            }
            else
            {
                File.SetUnixFileMode(path,
                    ownerOnly
                        ? UnixFileMode.UserRead | UnixFileMode.UserWrite
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite |
                          UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
            CryptographicOperations.ZeroMemory(pemBytes);
            CryptographicOperations.ZeroMemory(
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                    pemCharacters.AsSpan()));
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsAcl(string path, bool ownerOnly)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var currentUser = identity.User ??
            throw new InvalidOperationException("Test user has no Windows SID.");
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true,
            preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        if (!ownerOnly)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.Read,
                AccessControlType.Allow));
        }

        FileSystemAclExtensions.SetAccessControl(new FileInfo(path), security);
    }
}

sealed class Fixture : IDisposable
{
    public Fixture()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            $"turborama-content-authority-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        Issuer = RSA.Create(2048);
        Content = RSA.Create(2048);
        IssuerPrivatePath = Path.Combine(Root, "issuer-private.pem");
        ContentPublicPath = Path.Combine(Root, "content-public.der");
        OutputDirectory = Path.Combine(Root, "approved-authority");
        TestSupport.WritePrivatePem(Issuer, IssuerPrivatePath);
        var contentSpki = Content.ExportSubjectPublicKeyInfo();
        try
        {
            File.WriteAllBytes(ContentPublicPath, contentSpki);
            ContentKeyId = ContentAuthorityProtocol.KeyIdFromSpki(contentSpki);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contentSpki);
        }

        TimeProvider = new FixedTimeProvider(
            DateTimeOffset.FromUnixTimeSeconds(TestSupport.Now));
    }

    public string Root { get; }
    public RSA Issuer { get; }
    public RSA Content { get; }
    public string IssuerPrivatePath { get; }
    public string ContentPublicPath { get; }
    public string OutputDirectory { get; }
    public string ContentKeyId { get; }
    public TimeProvider TimeProvider { get; }

    public AuthorityGenerationRequest Request() => new(
        "https://content.example.invalid/",
        ContentPublicPath,
        new string('a', 64),
        new string('b', 64),
        TestSupport.Now - 60,
        TestSupport.Now + 3600,
        IssuerPrivatePath,
        OutputDirectory);

    public string NewOutput(string suffix) =>
        Path.Combine(Root, $"approved-authority-{suffix}");

    public void Dispose()
    {
        Issuer.Dispose();
        Content.Dispose();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Test secrets are ephemeral and the process never logs their paths or bytes.
        }
    }
}
