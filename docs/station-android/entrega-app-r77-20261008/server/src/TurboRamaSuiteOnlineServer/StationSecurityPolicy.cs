using System.Security.Cryptography;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public sealed class StationSecurityPolicy(StationAttestation attestation, bool requireVerifiedApp = false)
{
    public bool RequireVerifiedApp { get; } = requireVerifiedApp;
    public StationSessionSecurity Negotiate(StationDeviceEnvelope envelope, JsonElement root,
        string deviceId, string primarySpki, string authorityId, bool previouslyVerified = false)
    {
        var mode = root.TryGetProperty("requestProof", out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : "invalid" : "none";
        if (mode == "none")
        {
            if (RequireVerifiedApp || previouslyVerified) throw VerifiedRequired();
            if (envelope.KeySignature is not null || envelope.AttestationChain is not null) throw Invalid();
            return StationSessionSecurity.Legacy;
        }
        if (mode == "rsa-pss-v1")
        {
            if (RequireVerifiedApp || previouslyVerified) throw VerifiedRequired();
            if (envelope.KeySignature is not null || envelope.AttestationChain is not null) throw Invalid();
            return new(mode, primarySpki);
        }
        if (mode != "ec-p256-v1" || envelope.KeySignature is null) throw Invalid();
        var spki = StationProtocol.RequireString(root, "requestProofKey", 512);
        try
        {
            using var ec = ECDsa.Create(); var bytes = StationProtocol.Decode(spki, 256);
            ec.ImportSubjectPublicKeyInfo(bytes, out var used);
            if (used != bytes.Length || ec.KeySize != 256 ||
                ec.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" ||
                !ec.VerifyData(StationProtocol.Decode(envelope.Payload, 8192),
                    StationProtocol.Decode(envelope.KeySignature, 80), HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence)) throw Invalid();
        }
        catch (CryptographicException) { throw Invalid(); }
        try { attestation.Verify(envelope.AttestationChain, spki, deviceId, authorityId); }
        catch (SuiteException ex) when (!RequireVerifiedApp && !previouslyVerified &&
            (ex.Code is "STATION_APP_ATTESTATION_INVALID" or "STATION_ATTESTATION_STATUS_UNAVAILABLE") &&
            root.TryGetProperty("allowUnverifiedApp", out var allow) && allow.ValueKind == JsonValueKind.True)
        {
            // An explicit, RSA-signed compatibility choice. Never claim verified
            // APK status or use the untrusted EC key in this downgraded session.
            return new("rsa-pss-v1", primarySpki);
        }
        return new(mode, spki);
    }
    private static SuiteException Invalid() => new(403, "STATION_REQUEST_KEY_INVALID",
        "Station request key is invalid.");
    private static SuiteException VerifiedRequired() => new(403, "STATION_VERIFIED_APP_REQUIRED",
        "Update Station to its verified application version.");
}
