using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public sealed class SuiteService
{
    private const int ChallengeLifetime = 60, SessionLifetime = 180, HeartbeatAfter = 60;
    private readonly ISuiteStore _store; private readonly IAssertionSigner _signer; private readonly TimeProvider _time; private readonly string _pepper;
    public SuiteService(ISuiteStore store, IAssertionSigner signer, TimeProvider time, string pepper) => (_store, _signer, _time, _pepper) = (store, signer, time, pepper);
    private long Now() => _time.GetUtcNow().ToUnixTimeSeconds();

    public async Task<SignedAssertionEnvelope> ActivationChallengeAsync(ActivationChallengeRequest request, CancellationToken ct)
    {
        Protocol.RequireVersion(request.SchemaVersion); Protocol.RequireProduct(request.ProductId); Protocol.ValidateDevice(request.Device);
        Protocol.ValidateActivationCode(request.ActivationCode);
        var license = await ActiveLicense(request.LicenseId, ct); var verifier = ActivationCodes.Verify(_pepper, request.ActivationCode);
        if (license.ActivationConsumed || license.ActivationExpiresAt <= Now() || !Protocol.FixedEquals(verifier, license.ActivationVerifier)) throw new SuiteException(403, "ACTIVATION_INVALID", "Activation is not authorized.");
        var hash = Protocol.ActivationContextHash(request.LicenseId, request.Device); var challenge = NewChallenge(request.LicenseId, request.Device.DeviceId, "", "device.activate", hash, verifier, JsonSerializer.Serialize(request.Device, StrictJson.Options)); await _store.InsertChallengeAsync(challenge, ct);
        return _signer.Sign(new ActivationChallengeAssertion(1, Protocol.ActivationChallengeKind, Protocol.ProductId, request.LicenseId, request.Device.DeviceId, "device.activate", hash, challenge.ChallengeId, challenge.Nonce, "ISSUED", Now(), challenge.ExpiresAt));
    }
    public async Task<SignedAssertionEnvelope> CompleteActivationAsync(ActivationProof proof, CancellationToken ct)
    {
        Protocol.RequireVersion(proof.SchemaVersion); Protocol.RequireProduct(proof.ProductId); Protocol.ValidateDevice(proof.Device);
        var digest = Digest(JsonSerializer.SerializeToUtf8Bytes(proof, StrictJson.Options)); var prior = await _store.FindCompletionAsync(proof.ChallengeId, ct); if (prior is not null) { if (!Protocol.FixedEquals(prior.RequestDigest, digest)) throw new SuiteException(409, "REPLAY_DENIED", "Replay was denied."); return prior.Result; }
        var challenge = await _store.FindChallengeAsync(proof.ChallengeId, "device.activate", Now(), ct) ?? throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired.");
        var contextHash = Protocol.ActivationContextHash(proof.LicenseId, proof.Device); Match(challenge, proof.LicenseId, proof.Device.DeviceId, "", contextHash);
        if (!Protocol.Verify(proof.Device, new(1, challenge.ChallengeId, challenge.Nonce, challenge.ExpiresAt), proof.LicenseId, "", "device.activate", contextHash, proof.Signature)) throw new SuiteException(403, "PROOF_INVALID", "Machine proof is invalid.");
        var result = _signer.Sign(new ActivationResultAssertion(1, Protocol.ActivationResultKind, Protocol.ProductId, proof.LicenseId, proof.Device.DeviceId, "device.activate", contextHash, challenge.ChallengeId, "ACTIVE", proof.Device.BindingType, Now()));
        return await _store.CompleteActivationAsync(challenge, digest, new(proof.LicenseId, proof.Device.DeviceId, proof.Device.BindingType, proof.Device.PublicKeySpki, proof.Device.HardwareFingerprint, "ACTIVE"), result, ct);
    }
    public async Task<SignedAssertionEnvelope> ChallengeAsync(ChallengeRequest request, CancellationToken ct)
    {
        Protocol.RequireVersion(request.SchemaVersion); Protocol.RequireProduct(request.ProductId); if (request.Action is not ("session.open" or "session.heartbeat")) throw new SuiteException(400, "ACTION_INVALID", "Action is invalid.");
        _ = await ActiveLicense(request.LicenseId, ct); var device = await _store.FindDeviceAsync(request.LicenseId, request.DeviceId, ct) ?? throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized."); if (device.Status != "ACTIVE") throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized.");
        var challenge = NewChallenge(request.LicenseId, request.DeviceId, request.SessionId, request.Action, request.ContextHash, null, null); await _store.InsertChallengeAsync(challenge, ct); var kind = request.Action == "session.open" ? Protocol.SessionOpenChallengeKind : Protocol.SessionHeartbeatChallengeKind;
        return _signer.Sign(new OperationChallengeAssertion(1, kind, Protocol.ProductId, request.LicenseId, request.DeviceId, request.SessionId, request.Action, request.ContextHash, challenge.ChallengeId, challenge.Nonce, "ISSUED", Now(), challenge.ExpiresAt));
    }
    public async Task<SignedAssertionEnvelope> SessionAsync(SessionProof request, CancellationToken ct)
    {
        Protocol.RequireVersion(request.Proof.SchemaVersion); Protocol.RequireProduct(request.Proof.ProductId); var hash = Protocol.SessionContextHash(request.Context); if (!Protocol.FixedEquals(hash, request.Proof.ContextHash)) throw new SuiteException(400, "CONTEXT_INVALID", "Context is invalid.");
        if (request.Proof.Action != request.Context.Action || request.Proof.LicenseId != request.Context.LicenseId || request.Proof.DeviceId != request.Context.DeviceId || request.Proof.SessionId != request.Context.SessionId) throw new SuiteException(400, "CONTEXT_INVALID", "Context is invalid.");
        _ = await ActiveLicense(request.Proof.LicenseId, ct); var challenge = await _store.FindChallengeAsync(request.Proof.ChallengeId, request.Proof.Action, Now(), ct) ?? throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired."); Match(challenge, request.Proof.LicenseId, request.Proof.DeviceId, request.Proof.SessionId, hash);
        var device = await _store.FindDeviceAsync(request.Proof.LicenseId, request.Proof.DeviceId, ct) ?? throw new SuiteException(403, "DEVICE_DENIED", "Device is not authorized."); var descriptor = new DeviceDescriptor(1, device.DeviceId, device.BindingType, Protocol.Algorithm, device.PublicKeySpki, request.Context.HardwareFingerprint, request.Context.ClientVersion);
        if (!Protocol.FixedEquals(device.HardwareFingerprint, request.Context.HardwareFingerprint) || !Protocol.Verify(descriptor, new(1, challenge.ChallengeId, challenge.Nonce, challenge.ExpiresAt), request.Proof.LicenseId, request.Proof.SessionId, request.Proof.Action, hash, request.Proof.Signature)) throw new SuiteException(403, "PROOF_INVALID", "Machine proof is invalid.");
        var now = Now(); var session = await _store.CompleteSessionAsync(challenge, new(request.Proof.LicenseId, request.Proof.DeviceId, request.Proof.SessionId, "ACTIVE", now + SessionLifetime, now, 0), request.Proof.Action, now, ct); var kind = request.Proof.Action == "session.open" ? Protocol.SessionOpenKind : Protocol.SessionHeartbeatKind;
        return _signer.Sign(new SessionAssertion(1, kind, Protocol.ProductId, session.LicenseId, session.DeviceId, session.SessionId, request.Proof.Action, hash, challenge.ChallengeId, "ACTIVE", session.LastServerTime, session.AuthorizedUntil, HeartbeatAfter));
    }
    private async Task<LicenseRecord> ActiveLicense(string id, CancellationToken ct) { var l = await _store.FindLicenseAsync(id, ct) ?? throw new SuiteException(404, "LICENSE_NOT_FOUND", "License was not found."); if (l.ProductId != Protocol.ProductId || l.Status != "ACTIVE" || l.LicenseTerm != "LIFETIME" || l.ExpiresAt is not null || l.MaximumActiveDevices != 1) throw new SuiteException(403, "LICENSE_DENIED", "License is not active."); return l; }
    private ChallengeRecord NewChallenge(string l, string d, string s, string a, string h, string? v, string? j) { var now = Now(); return new(Digest(RandomNumberGenerator.GetBytes(32)), Protocol.ProductId, l, d, s, a, h, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), now + ChallengeLifetime, v, j); }
    private static string Digest(ReadOnlySpan<byte> b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    private static void Match(ChallengeRecord c, string l, string d, string s, string h) { if (c.ProductId != Protocol.ProductId || c.LicenseId != l || c.DeviceId != d || c.SessionId != s || !Protocol.FixedEquals(c.ContextHash, h)) throw new SuiteException(409, "CHALLENGE_MISMATCH", "Challenge does not match the request."); }
}
