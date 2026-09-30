using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;

internal static class StationProtocolChecks
{
    public static void Run()
    {
        using var device = RSA.Create(2048);
        var spki = device.ExportSubjectPublicKeyInfo();
        var deviceId = StationProtocol.Encode(SHA256.HashData(spki));
        var spkiText = StationProtocol.Encode(spki);
        if (!StationProtocol.DeviceKey(deviceId, spkiText).AsSpan().SequenceEqual(spki))
            throw new Exception("Station SPKI identity mismatch.");
        if (StationProtocol.IsCanonicalBase64Url(deviceId + "=", 32))
            throw new Exception("Station padded Base64URL accepted.");
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"domain\":\"TurboRamaStationAndroid/activate/v1\"}");
        var signature = device.SignData(payload, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);
        using (var proof = StationProtocol.VerifyDeviceEnvelope(new(
            StationProtocol.Encode(payload), StationProtocol.Encode(signature)), deviceId,
            spkiText))
        {
            if (proof.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
                throw new Exception("Station proof payload mismatch.");
        }
        signature[0] ^= 1;
        try
        {
            using var _ = StationProtocol.VerifyDeviceEnvelope(new(
                StationProtocol.Encode(payload), StationProtocol.Encode(signature)), deviceId,
                spkiText);
            throw new Exception("Station invalid proof accepted.");
        }
        catch (SuiteException exception) when (exception.Code == "STATION_PROOF_INVALID") { }

        using var server = RSA.Create(2048);
        var privatePem = server.ExportRSAPrivateKeyPem();
        using var signer = new StationResponseSigner(privatePem);
        var response = signer.Sign(new { schemaVersion = 1,
            domain = "TurboRamaStationAndroid/activated/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            deviceId, licenseId = "synthetic" });
        using var envelope = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var bytes = StationProtocol.Decode(envelope.RootElement.GetProperty("payload").GetString()!, 4096);
        var signed = StationProtocol.Decode(envelope.RootElement.GetProperty("signature").GetString()!, 512);
        if (!server.VerifyData(bytes, signed, HashAlgorithmName.SHA256, RSASignaturePadding.Pss) ||
            envelope.RootElement.GetProperty("keyId").GetString() != signer.KeyId ||
            StationProtocol.Encode(server.ExportSubjectPublicKeyInfo()) !=
            signer.PublicKeySpkiBase64Url)
            throw new Exception("Station signed envelope mismatch.");
    }
}
