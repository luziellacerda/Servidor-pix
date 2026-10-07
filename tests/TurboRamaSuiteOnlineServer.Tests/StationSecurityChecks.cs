using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurboRamaSuiteOnlineServer;

internal static class StationSecurityChecks
{
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1800000000); }
    private sealed class Status(bool revoked = false) : IStationAttestationStatus
    {
        public void RequireAllowed(IEnumerable<X509Certificate2> certificates)
        {
            if (revoked) throw new SuiteException(403, "STATION_APP_ATTESTATION_REVOKED", "Synthetic revoked certificate.");
        }
    }
    private enum Level { Software = 0, Hardware = 1 }
    private enum Boot { Verified = 0, Unverified = 2 }
    public static void Run()
    {
        var clock = new Clock(); var token = StationProtocol.Encode(new byte[32]);
        var nonce = StationProtocol.Encode(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds();
        var target = "/v1/station/catalog?metadata=1";
        var canonical = StationRequestProof.Canonical("GET", target, [], token, timestamp, nonce);
        var expected = $"TurboRamaStationAndroid/request/v1\nGET\n{target}\n" +
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\n" +
            StationProtocol.HashToken(token) + "\n1800000000\nAAECAwQFBgcICQoLDA0ODw\n";
        if (!canonical.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected))) throw new Exception("Request canonical vector changed.");
        using var rsa = RSA.Create(2048); var spki = StationProtocol.Encode(rsa.ExportSubjectPublicKeyInfo());
        var signature = StationProtocol.Encode(rsa.SignData(canonical, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var header = $"v1.{timestamp.ToString(CultureInfo.InvariantCulture)}.{nonce}.{signature}";
        var validator = new StationRequestProof(clock);
        validator.Verify(header, "rsa-pss-v1", spki, token, "GET", target, []);
        Denied(() => validator.Verify(header, "rsa-pss-v1", spki, token, "GET", target, []), "STATION_REQUEST_PROOF_REPLAY");
        foreach (var alternative in new[] { "/v1/station/me", "/v1/station/catalog?metadata=0" })
            Denied(() => new StationRequestProof(clock).Verify(header, "rsa-pss-v1", spki, token, "GET", alternative, []), "STATION_REQUEST_PROOF_INVALID");
        Denied(() => new StationRequestProof(clock).Verify(header, "rsa-pss-v1", spki, token, "POST", target, []), "STATION_REQUEST_PROOF_INVALID");
        Denied(() => new StationRequestProof(clock).Verify(header, "rsa-pss-v1", spki, token, "GET", target, [1]), "STATION_REQUEST_PROOF_INVALID");
        Denied(() => new StationRequestProof(clock).Verify(header, "rsa-pss-v1", spki, StationProtocol.Encode(new byte[32].Select(_ => (byte)1).ToArray()), "GET", target, []), "STATION_REQUEST_PROOF_INVALID");
        Denied(() => validator.Verify(null, "rsa-pss-v1", spki, token, "GET", target, []), "STATION_REQUEST_PROOF_REQUIRED");
        Denied(() => validator.Verify(header.Replace("1800000000", "1799999900"), "rsa-pss-v1", spki, token, "GET", target, []), "STATION_REQUEST_PROOF_EXPIRED");
        // Valid EC/DER requests and a bounded cache, with RSA proof replay still denied.
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ecSpki = StationProtocol.Encode(ec.ExportSubjectPublicKeyInfo());
        var ecHeader = $"v1.{timestamp}.{nonce}." + StationProtocol.Encode(ec.SignData(canonical,
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        new StationRequestProof(clock).Verify(ecHeader, "ec-p256-v1", ecSpki, token, "GET", target, []);
        var full = new StationRequestProof(clock, 1); full.Verify(header, "rsa-pss-v1", spki, token, "GET", target, []);
        var nonce2 = StationProtocol.Encode(RandomNumberGenerator.GetBytes(16));
        var other = $"v1.{timestamp}.{nonce2}." + StationProtocol.Encode(rsa.SignData(
            StationRequestProof.Canonical("GET", target, [], token, timestamp, nonce2), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        Denied(() => full.Verify(other, "rsa-pss-v1", spki, token, "GET", target, []), "STATION_REQUEST_PROOF_BUSY");
        Attestation(clock);
        Console.WriteLine("STATION SECURITY: request ownership, replay/target/body/token binding, EC/RSA, cache capacity and certified APK/boot/root/revocation gates passed.");
    }
    private static void Attestation(Clock clock)
    {
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rootRequest = new CertificateRequest("CN=Synthetic root for isolated tests", rootKey, HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(clock.GetUtcNow().AddDays(-1), clock.GetUtcNow().AddDays(1));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var deviceId = StationProtocol.Encode(new byte[32]); var authority = new string('a', 64);
        var challenge = StationAttestation.Challenge(deviceId, authority);
        var spki = StationProtocol.Encode(key.ExportSubjectPublicKeyInfo());
        using var validator = new StationAttestation(new Status(), clock, [root]);
        using var realRoots = new StationAttestation(new Status(), clock);
        X509Certificate2 Leaf(byte[] description, X509Certificate2? issuer = null, ECDsa? leafKey = null, bool ca = false)
        {
            var request = new CertificateRequest("CN=Synthetic Android key", leafKey ?? key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(ca, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(ca ? X509KeyUsageFlags.KeyCertSign : X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509Extension(StationAttestation.ExtensionOid, description, false));
            return request.Create(issuer ?? root, clock.GetUtcNow().AddHours(-1), clock.GetUtcNow().AddHours(1), RandomNumberGenerator.GetBytes(16));
        }
        string[] Encoded(params X509Certificate2[] certs) => certs.Select(c => StationProtocol.Encode(c.RawData)).ToArray();
        using var valid = Leaf(Description(challenge));
        validator.Verify(Encoded(valid, root), spki, deviceId, authority);
        Denied(() => realRoots.Verify(Encoded(valid, root), spki, deviceId, authority), "STATION_APP_ATTESTATION_INVALID");
        Denied(() => validator.Verify(Encoded(valid, root), spki, deviceId, new string('b',64)), "STATION_APP_ATTESTATION_INVALID");
        foreach (var bad in new[] {
            Description(challenge, package:"org.attacker.client"), Description(challenge, signer:new byte[32]),
            Description(challenge, locked:false), Description(challenge, verified:false), Description(challenge, hardware:false) })
        {
            using var invalid = Leaf(bad);
            Denied(() => validator.Verify(Encoded(invalid, root), spki, deviceId, authority), "STATION_APP_ATTESTATION_INVALID");
        }
        using var revoked = new StationAttestation(new Status(true), clock, [root]);
        Denied(() => revoked.Verify(Encoded(valid, root), spki, deviceId, authority), "STATION_APP_ATTESTATION_REVOKED");
        // A key certified for another application cannot sign a forged leaf with
        // our identity and get the verifier to inspect that nearer-leaf extension.
        using var issuerNoKey = Leaf(Description(challenge, package:"org.attacker.client"), ca:true);
        using var issuer = issuerNoKey.CopyWithPrivateKey(key);
        using var forgedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var forged = Leaf(Description(challenge), issuer, forgedKey);
        Denied(() => validator.Verify(Encoded(forged, issuer, root), StationProtocol.Encode(forgedKey.ExportSubjectPublicKeyInfo()),
            deviceId, authority), "STATION_APP_ATTESTATION_INVALID");
    }
    private static byte[] Description(byte[] challenge, string package=StationAttestation.Package,
        byte[]? signer=null, bool locked=true, bool verified=true, bool hardware=true)
    {
        var app = new AsnWriter(AsnEncodingRules.DER); app.PushSequence(); app.PushSetOf(); app.PushSequence();
        app.WriteOctetString(Encoding.UTF8.GetBytes(package)); app.WriteInteger(57);
        app.PopSequence(); app.PopSetOf(); app.PushSetOf();
        app.WriteOctetString(signer ?? Convert.FromHexString(StationAttestation.SigningCertificateSha256));
        app.PopSetOf(); app.PopSequence();
        var writer = new AsnWriter(AsnEncodingRules.DER); writer.PushSequence(); writer.WriteInteger(3);
        writer.WriteEnumeratedValue(hardware ? Level.Hardware : Level.Software); writer.WriteInteger(4);
        writer.WriteEnumeratedValue(hardware ? Level.Hardware : Level.Software);
        writer.WriteOctetString(challenge); writer.WriteOctetString([]);
        writer.PushSequence(); Tag(709, () => writer.WriteOctetString(app.Encode())); writer.PopSequence();
        writer.PushSequence();
        Tag(1, () => {writer.PushSetOf(); writer.WriteInteger(2); writer.PopSetOf();});
        Tag(2, () => writer.WriteInteger(3)); Tag(3, () => writer.WriteInteger(256));
        Tag(5, () => {writer.PushSetOf();writer.WriteInteger(4);writer.PopSetOf();});
        Tag(10, () => writer.WriteInteger(1));
        Tag(704, () => {writer.PushSequence();writer.WriteOctetString(new byte[32]);writer.WriteBoolean(locked);
            writer.WriteEnumeratedValue(verified ? Boot.Verified : Boot.Unverified);writer.WriteOctetString(new byte[32]);writer.PopSequence();});
        writer.PopSequence(); writer.PopSequence(); return writer.Encode();
        void Tag(int number, Action action)
        {var tag = new Asn1Tag(TagClass.ContextSpecific,number,true);writer.PushSequence(tag);action();writer.PopSequence(tag);}
    }
    private static void Denied(Action action, string code)
    {
        try { action(); throw new Exception("Security gate accepted: " + code); }
        catch (SuiteException ex) when (ex.Code == code) { }
    }
}
