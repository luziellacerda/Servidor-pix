using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TurboRamaSuiteOnlineServer;

public interface IStationAttestationStatus
{
    void RequireAllowed(IEnumerable<X509Certificate2> certificates);
}

public sealed class StationAttestation : IDisposable
{
    public const string ExtensionOid = "1.3.6.1.4.1.11129.2.1.17";
    public const string Package = "org.turboramastation.frontend";
    public const string SigningCertificateSha256 =
        "7b16ee1aca7db7a50e7cc6c8612cf2a3568f474894a468865d842bf720c89825";
    private readonly X509Certificate2Collection roots = new();
    private readonly IStationAttestationStatus status;
    private readonly TimeProvider clock;

    public StationAttestation(IStationAttestationStatus status, TimeProvider clock)
    {
        this.status = status; this.clock = clock;
        using var stream = typeof(StationAttestation).Assembly.GetManifestResourceStream(
            "TurboRamaSuiteOnlineServer.Security.android-attestation-roots.pem") ??
            throw new InvalidOperationException("Official Android roots are missing.");
        using var reader = new StreamReader(stream); roots.ImportFromPem(reader.ReadToEnd());
        if (roots.Count != 2) throw new InvalidOperationException("Official Android root bundle is invalid.");
    }
    // Test-only trust injection is unavailable to production configuration.
    internal StationAttestation(IStationAttestationStatus status, TimeProvider clock,
        IEnumerable<X509Certificate2> testRoots)
    { this.status = status; this.clock = clock; roots.AddRange(testRoots.ToArray()); }

    public static byte[] Challenge(string deviceId, string authorityId) => SHA256.HashData(
        Encoding.UTF8.GetBytes($"TurboRamaStationAndroid/key-attestation/v1\n{deviceId}\n{authorityId}\n"));

    public void Verify(string[]? encodedChain, string spki, string deviceId, string authorityId)
    {
        var certificates = new List<X509Certificate2>();
        try
        {
            if (encodedChain is null || encodedChain.Length is < 2 or > 6 ||
                encodedChain.Any(s => s is null) || encodedChain.Sum(s => s.Length) > 49152)
                throw Invalid();
            foreach (var encoded in encodedChain)
            {
                var bytes = StationProtocol.Decode(encoded, 8192);
                var certificate = new X509Certificate2(bytes);
                if (!certificate.RawData.AsSpan().SequenceEqual(bytes))
                { certificate.Dispose(); throw Invalid(); }
                certificates.Add(certificate);
            }
            using var ec = ECDsa.Create();
            var key = StationProtocol.Decode(spki, 256); ec.ImportSubjectPublicKeyInfo(key, out var consumed);
            if (consumed != key.Length || ec.KeySize != 256 ||
                ec.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" ||
                !ec.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(key)) throw Invalid();
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(roots);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.VerificationTime = clock.GetUtcNow().UtcDateTime;
            foreach (var certificate in certificates.Skip(1))
            {
                // Old factory root certificates may have the same public key as
                // the renewed official anchor. Never promote an untrusted key.
                if (!roots.Cast<X509Certificate2>().Any(r => SameKey(r, certificate)))
                    chain.ChainPolicy.ExtraStore.Add(certificate);
            }
            if (!chain.Build(certificates[0]) || chain.ChainElements.Count is < 2 or > 6)
                throw Invalid();
            var built = chain.ChainElements.Cast<X509ChainElement>().Select(e => e.Certificate).ToArray();
            status.RequireAllowed(built);
            // Inspect the first attestation extension from the root towards the
            // leaf. A certified attacker key cannot attach a forged leaf extension.
            X509Certificate2? attested = null; X509Extension? extension = null;
            foreach (var certificate in built.Reverse())
            {
                var matches = certificate.Extensions.Cast<X509Extension>()
                    .Where(e => e.Oid?.Value == ExtensionOid).ToArray();
                if (matches.Length > 1) throw Invalid();
                if (matches.Length == 1) { attested = certificate; extension = matches[0]; break; }
            }
            if (attested is null || extension is null ||
                !attested.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(key)) throw Invalid();
            VerifyDescription(extension.RawData, Challenge(deviceId, authorityId));
        }
        catch (Exception ex) when (ex is AsnContentException or CryptographicException or
            ArgumentException or OverflowException or InvalidOperationException)
        { throw Invalid(); }
        finally { foreach (var certificate in certificates) certificate.Dispose(); }
    }

    private static void VerifyDescription(byte[] der, byte[] challenge)
    {
        var reader = new AsnReader(der, AsnEncodingRules.DER); var sequence = reader.ReadSequence();
        var attestationVersion = sequence.ReadInteger();
        var attestationLevel = sequence.ReadEnumeratedValue<SecurityLevel>();
        var keyVersion = sequence.ReadInteger();
        var keyLevel = sequence.ReadEnumeratedValue<SecurityLevel>();
        if (attestationVersion < 2 || keyVersion < 3 ||
            attestationLevel is not (SecurityLevel.TrustedEnvironment or SecurityLevel.StrongBox) ||
            keyLevel is not (SecurityLevel.TrustedEnvironment or SecurityLevel.StrongBox) ||
            !CryptographicOperations.FixedTimeEquals(sequence.ReadOctetString(), challenge)) throw Invalid();
        _ = sequence.ReadOctetString();
        var software = Authorizations(sequence.ReadSequence());
        var hardware = Authorizations(sequence.ReadSequence());
        sequence.ThrowIfNotEmpty(); reader.ThrowIfNotEmpty();
        if (software.ContainsKey(600) || hardware.ContainsKey(600) ||
            software.ContainsKey(709) && hardware.ContainsKey(709)) throw Invalid();
        if (!software.TryGetValue(709, out var app) && !hardware.TryGetValue(709, out app)) throw Invalid();
        var appReader = new AsnReader(app, AsnEncodingRules.DER);
        VerifyApplication(appReader.ReadOctetString()); appReader.ThrowIfNotEmpty();
        if (!hardware.TryGetValue(704, out var trust)) throw Invalid();
        var trustReader = new AsnReader(trust, AsnEncodingRules.DER); var root = trustReader.ReadSequence();
        if (root.ReadOctetString().Length is < 16 or > 128 || !root.ReadBoolean() ||
            root.ReadEnumeratedValue<BootState>() != BootState.Verified) throw Invalid();
        if (root.HasData && root.ReadOctetString().Length is < 16 or > 128) throw Invalid();
        root.ThrowIfNotEmpty(); trustReader.ThrowIfNotEmpty();
        Integer(hardware, 2, 3); Integer(hardware, 3, 256); Integer(hardware, 10, 1);
        Set(hardware, 1, 2, [2, 3]); Set(hardware, 5, 4, [4]);
    }
    private static Dictionary<int, byte[]> Authorizations(AsnReader reader)
    {
        var result = new Dictionary<int, byte[]>();
        while (reader.HasData)
        {
            var tag = reader.PeekTag();
            if (tag.TagClass != TagClass.ContextSpecific || !tag.IsConstructed || result.ContainsKey(tag.TagValue))
                throw Invalid();
            var tagged = reader.ReadSequence(tag);
            var bytes = tagged.ReadEncodedValue().ToArray(); tagged.ThrowIfNotEmpty();
            result.Add(tag.TagValue, bytes);
        }
        return result;
    }
    private static void Integer(Dictionary<int, byte[]> tags, int tag, int expected)
    {
        if (!tags.TryGetValue(tag, out var bytes)) throw Invalid();
        var reader = new AsnReader(bytes, AsnEncodingRules.DER);
        if (reader.ReadInteger() != expected) throw Invalid(); reader.ThrowIfNotEmpty();
    }
    private static void Set(Dictionary<int, byte[]> tags, int tag, int required, int[] allowed)
    {
        if (!tags.TryGetValue(tag, out var bytes)) throw Invalid();
        var reader = new AsnReader(bytes, AsnEncodingRules.DER); var values = reader.ReadSetOf();
        var seen = new HashSet<int>();
        while (values.HasData)
        {
            var value = checked((int)values.ReadInteger());
            if (!allowed.Contains(value) || !seen.Add(value)) throw Invalid();
        }
        if (!seen.Contains(required)) throw Invalid(); reader.ThrowIfNotEmpty();
    }
    private static void VerifyApplication(byte[] encoded)
    {
        var reader = new AsnReader(encoded, AsnEncodingRules.DER); var app = reader.ReadSequence();
        var packages = app.ReadSetOf(); var package = packages.ReadSequence();
        if (!package.ReadOctetString().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Package)) ||
            package.ReadInteger() < 1) throw Invalid();
        package.ThrowIfNotEmpty(); packages.ThrowIfNotEmpty();
        var signatures = app.ReadSetOf();
        if (!CryptographicOperations.FixedTimeEquals(signatures.ReadOctetString(),
            Convert.FromHexString(SigningCertificateSha256))) throw Invalid();
        signatures.ThrowIfNotEmpty(); app.ThrowIfNotEmpty(); reader.ThrowIfNotEmpty();
    }
    private static bool SameKey(X509Certificate2 left, X509Certificate2 right) =>
        left.PublicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(right.PublicKey.ExportSubjectPublicKeyInfo());
    private enum SecurityLevel { Software = 0, TrustedEnvironment = 1, StrongBox = 2 }
    private enum BootState { Verified = 0, SelfSigned = 1, Unverified = 2, Failed = 3 }
    private static SuiteException Invalid() => new(403, "STATION_APP_ATTESTATION_INVALID",
        "Station application could not be verified.");
    public void Dispose() { foreach (var root in roots) root.Dispose(); }
}
