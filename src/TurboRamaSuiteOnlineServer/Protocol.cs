using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public static class Protocol
{
    public const int SchemaVersion = 1;
    public const string ProductId = "TURBORAMA_SUITE";
    public const string Algorithm = "rsa-pss-sha256";
    public const int MaximumBodyBytes = 64 * 1024;
    public const string ActivationChallengeKind = "TURBORAMA_SUITE_ACTIVATION_CHALLENGE";
    public const string ActivationResultKind = "TURBORAMA_SUITE_ACTIVATION_RESULT";
    public const string SessionOpenChallengeKind = "TURBORAMA_SUITE_SESSION_OPEN_CHALLENGE";
    public const string SessionHeartbeatChallengeKind = "TURBORAMA_SUITE_SESSION_HEARTBEAT_CHALLENGE";
    public const string SessionOpenKind = "TURBORAMA_SUITE_SESSION_OPEN";
    public const string SessionHeartbeatKind = "TURBORAMA_SUITE_SESSION_HEARTBEAT";
    private static readonly byte[] ProofDomain = Encoding.ASCII.GetBytes("TurboRamaOnlineMachineProof/v1\0");
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        Indented = false,
        SkipValidation = false
    };

    public static string ActivationContextHash(string licenseId, DeviceDescriptor device)
        => Hash(Canonical(writer =>
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("productId", ProductId);
            writer.WriteString("licenseId", Identifier(licenseId, 6, 64));
            writer.WriteString("action", "device.activate");
            writer.WritePropertyName("device"); WriteDevice(writer, device); writer.WriteEndObject();
        }));

    public static string SessionContextHash(SessionContext context)
    {
        ValidateContext(context);
        return Hash(Canonical(writer =>
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", context.SchemaVersion);
            writer.WriteString("productId", context.ProductId);
            writer.WriteString("licenseId", context.LicenseId);
            writer.WriteString("deviceId", context.DeviceId);
            writer.WriteString("sessionId", context.SessionId);
            writer.WriteString("action", context.Action);
            writer.WriteString("hardwareFingerprint", context.HardwareFingerprint);
            writer.WriteString("clientVersion", context.ClientVersion); writer.WriteEndObject();
        }));
    }

    public static byte[] SigningMessage(ChallengeResponse challenge, string licenseId,
        string deviceId, string sessionId, string action, string contextHash)
    {
        Hex(challenge.ChallengeId); CanonicalBase64(challenge.Nonce, 32, 64);
        var json = Canonical(writer =>
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("challengeId", challenge.ChallengeId);
            writer.WriteString("nonce", challenge.Nonce);
            writer.WriteNumber("expiresAtUnixSeconds", challenge.ExpiresAtUnixSeconds);
            writer.WriteString("licenseId", Identifier(licenseId, 6, 64));
            writer.WriteString("deviceId", Hex(deviceId));
            writer.WriteString("sessionId", sessionId.Length == 0 ? "" : Hex(sessionId));
            writer.WriteString("action", Action(action));
            writer.WriteString("contextHash", Hex(contextHash)); writer.WriteEndObject();
        });
        var result = new byte[ProofDomain.Length + json.Length];
        ProofDomain.CopyTo(result, 0); json.CopyTo(result, ProofDomain.Length);
        CryptographicOperations.ZeroMemory(json); return result;
    }

    public static void ValidateDevice(DeviceDescriptor device)
    {
        ValidateDeviceShape(device);
        var spki = CanonicalBase64(device.PublicKeySpki, 256, 4096);
        try
        {
            using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(spki, out var used);
            if (used != spki.Length || rsa.KeySize is < 2048 or > 4096) Invalid();
            var canonical = rsa.ExportSubjectPublicKeyInfo();
            try { if (!canonical.AsSpan().SequenceEqual(spki)) Invalid(); }
            finally { CryptographicOperations.ZeroMemory(canonical); }
            if (!FixedEquals(Hash(spki), device.DeviceId)) Invalid();
        }
        catch (CryptographicException) { Invalid(); }
        finally { CryptographicOperations.ZeroMemory(spki); }
    }

    public static bool Verify(DeviceDescriptor device, ChallengeResponse challenge, string licenseId,
        string sessionId, string action, string contextHash, string signatureText)
    {
        ValidateDevice(device); var signature = CanonicalBase64(signatureText, 256, 512);
        var spki = Convert.FromBase64String(device.PublicKeySpki);
        var message = SigningMessage(challenge, licenseId, device.DeviceId, sessionId, action, contextHash);
        try
        {
            using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(spki, out var used);
            return used == spki.Length && rsa.VerifyData(message, signature,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signature); CryptographicOperations.ZeroMemory(spki);
            CryptographicOperations.ZeroMemory(message);
        }
    }

    public static byte[] CanonicalAssertion(object assertion) => assertion switch
    {
        ActivationChallengeAssertion value => Canonical(w => Write(w, value)),
        OperationChallengeAssertion value => Canonical(w => Write(w, value)),
        ActivationResultAssertion value => Canonical(w => Write(w, value)),
        SessionAssertion value => Canonical(w => Write(w, value)),
        _ => throw new ArgumentOutOfRangeException(nameof(assertion))
    };

    public static string AssertionDomain(object assertion) => assertion switch
    {
        ActivationChallengeAssertion => "TurboRamaSuiteOnlineAssertion/activation-challenge/v1\0",
        ActivationResultAssertion => "TurboRamaSuiteOnlineAssertion/activation-result/v1\0",
        OperationChallengeAssertion value when value.Action == "session.open" =>
            "TurboRamaSuiteOnlineAssertion/session-open-challenge/v1\0",
        OperationChallengeAssertion => "TurboRamaSuiteOnlineAssertion/session-heartbeat-challenge/v1\0",
        SessionAssertion value when value.Action == "session.open" =>
            "TurboRamaSuiteOnlineAssertion/session-open/v1\0",
        SessionAssertion => "TurboRamaSuiteOnlineAssertion/session-heartbeat/v1\0",
        _ => throw new ArgumentOutOfRangeException(nameof(assertion))
    };

    private static void Write(Utf8JsonWriter w, ActivationChallengeAssertion a)
    { w.WriteStartObject(); Common(w, a.SchemaVersion, a.Kind, a.ProductId, a.LicenseId, a.DeviceId); w.WriteString("action", a.Action); w.WriteString("contextHash", a.ContextHash); w.WriteString("challengeId", a.ChallengeId); w.WriteString("nonce", a.Nonce); w.WriteString("status", a.Status); w.WriteNumber("serverTimeUnixSeconds", a.ServerTimeUnixSeconds); w.WriteNumber("expiresAtUnixSeconds", a.ExpiresAtUnixSeconds); w.WriteEndObject(); }
    private static void Write(Utf8JsonWriter w, OperationChallengeAssertion a)
    { w.WriteStartObject(); Common(w, a.SchemaVersion, a.Kind, a.ProductId, a.LicenseId, a.DeviceId); w.WriteString("sessionId", a.SessionId); w.WriteString("action", a.Action); w.WriteString("contextHash", a.ContextHash); w.WriteString("challengeId", a.ChallengeId); w.WriteString("nonce", a.Nonce); w.WriteString("status", a.Status); w.WriteNumber("serverTimeUnixSeconds", a.ServerTimeUnixSeconds); w.WriteNumber("expiresAtUnixSeconds", a.ExpiresAtUnixSeconds); w.WriteEndObject(); }
    private static void Write(Utf8JsonWriter w, ActivationResultAssertion a)
    { w.WriteStartObject(); Common(w, a.SchemaVersion, a.Kind, a.ProductId, a.LicenseId, a.DeviceId); w.WriteString("action", a.Action); w.WriteString("contextHash", a.ContextHash); w.WriteString("challengeId", a.ChallengeId); w.WriteString("status", a.Status); w.WriteString("bindingType", a.BindingType); w.WriteNumber("serverTimeUnixSeconds", a.ServerTimeUnixSeconds); w.WriteEndObject(); }
    private static void Write(Utf8JsonWriter w, SessionAssertion a)
    { w.WriteStartObject(); Common(w, a.SchemaVersion, a.Kind, a.ProductId, a.LicenseId, a.DeviceId); w.WriteString("sessionId", a.SessionId); w.WriteString("action", a.Action); w.WriteString("contextHash", a.ContextHash); w.WriteString("challengeId", a.ChallengeId); w.WriteString("status", a.Status); w.WriteNumber("serverTimeUnixSeconds", a.ServerTimeUnixSeconds); w.WriteNumber("authorizedUntilUnixSeconds", a.AuthorizedUntilUnixSeconds); w.WriteNumber("heartbeatAfterSeconds", a.HeartbeatAfterSeconds); w.WriteEndObject(); }
    private static void Common(Utf8JsonWriter w, int v, string k, string p, string l, string d)
    { w.WriteNumber("schemaVersion", v); w.WriteString("kind", k); w.WriteString("productId", p); w.WriteString("licenseId", l); w.WriteString("deviceId", d); }
    private static void WriteDevice(Utf8JsonWriter w, DeviceDescriptor d)
    { ValidateDeviceShape(d); w.WriteStartObject(); w.WriteNumber("schemaVersion", d.SchemaVersion); w.WriteString("deviceId", d.DeviceId); w.WriteString("bindingType", d.BindingType); w.WriteString("algorithm", d.Algorithm); w.WriteString("publicKeySpki", d.PublicKeySpki); w.WriteString("hardwareFingerprint", d.HardwareFingerprint); w.WriteString("agentVersion", d.AgentVersion); w.WriteEndObject(); }
    private static void ValidateDeviceShape(DeviceDescriptor device)
    { RequireVersion(device.SchemaVersion); Hex(device.DeviceId); if (device.BindingType is not ("TPM_BOUND" or "SOFTWARE_BOUND_ONLINE")) Invalid(); if (device.Algorithm != Algorithm) Invalid(); Hex(device.HardwareFingerprint); if (device.AgentVersion.Length is < 1 or > 64 || device.AgentVersion.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+'))) Invalid(); if (device.PublicKeySpki.Length is < 300 or > 8192 || device.PublicKeySpki.Any(char.IsWhiteSpace)) Invalid(); }
    private static byte[] Canonical(Action<Utf8JsonWriter> action)
    { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream, WriterOptions)) { action(writer); writer.Flush(); } return stream.ToArray(); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Identifier(string value, int min, int max)
    { if (value.Length < min || value.Length > max || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) Invalid(); return value; }
    private static string Hex(string value)
    { if (value.Length != 64 || value.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))) Invalid(); return value; }
    private static string Action(string value)
    { if (value is not ("device.activate" or "session.open" or "session.heartbeat")) Invalid(); return value; }
    private static byte[] CanonicalBase64(string value, int min, int max)
    { byte[] b; try { b = Convert.FromBase64String(value); } catch (FormatException) { Invalid(); throw; } if (b.Length < min || b.Length > max || Convert.ToBase64String(b) != value) { CryptographicOperations.ZeroMemory(b); Invalid(); } return b; }
    private static void ValidateContext(SessionContext c)
    { RequireVersion(c.SchemaVersion); if (c.ProductId != ProductId) Invalid(); Identifier(c.LicenseId, 6, 64); Hex(c.DeviceId); Hex(c.SessionId); Action(c.Action); Hex(c.HardwareFingerprint); if (c.ClientVersion.Length is < 1 or > 64 || c.ClientVersion.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '+'))) Invalid(); }
    public static void RequireProduct(string product) { if (product != ProductId) throw new SuiteException(403, "PRODUCT_DENIED", "Product is not authorized."); }
    public static void RequireVersion(int version) { if (version != 1) Invalid(); }
    public static bool FixedEquals(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
    private static void Invalid() => throw new SuiteException(400, "CONTRACT_INVALID", "Request contract is invalid.");
}
