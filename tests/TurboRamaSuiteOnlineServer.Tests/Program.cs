using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;

const string licenseId = "TR-000125";
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

Expect<SuiteException>(() => StrictJson.Parse<ErrorResponse>(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1,\"code\":\"X\",\"message\":\"x\"}")), "duplicate JSON");
Expect<SuiteException>(() => StrictJson.Parse<ErrorResponse>(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"code\":\"X\",\"message\":\"x\",\"extra\":1}")), "unknown JSON");
Expect<SuiteException>(() => Protocol.RequireProduct("TURBORAMA_PIX"), "cross product");

var pepper = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); var code = "test-activation-code"; var verifier = ActivationCodes.Verify(pepper, code);
var store = new MemoryStore(new LicenseRecord(licenseId, Protocol.ProductId, "ACTIVE", verifier, 2_000_000_000, false)); var clock = new ManualTime(1_800_000_000); using var signer = new RsaAssertionSigner(online); var serviceA = new SuiteService(store, signer, clock, pepper); var serviceB = new SuiteService(store, signer, clock, pepper);
var issued = await serviceA.ActivationChallengeAsync(new(1, Protocol.ProductId, licenseId, code, device), default); var issuedPayload = Payload<ActivationChallengeAssertion>(issued); var activationChallenge = new ChallengeResponse(1, issuedPayload.ChallengeId, issuedPayload.Nonce, issuedPayload.ExpiresAtUnixSeconds);
var activationSignature = Sign(machine, activationChallenge, licenseId, deviceId, "", "device.activate", activation);
var proof = new ActivationProof(1, Protocol.ProductId, licenseId, issuedPayload.ChallengeId, device, activationSignature); var completed = await serviceB.CompleteActivationAsync(proof, default); var retry = await serviceA.CompleteActivationAsync(proof, default); Equal(completed.Signature, retry.Signature, "idempotent activation");
await ExpectAsync<SuiteException>(() => serviceA.CompleteActivationAsync(proof with { Signature = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) }, default), "different replay");

var contextHash = Protocol.SessionContextHash(sessionContext); var challengeEnvelope = await serviceA.ChallengeAsync(new(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.open", contextHash), default); var operation = Payload<OperationChallengeAssertion>(challengeEnvelope); var operationChallenge = new ChallengeResponse(1, operation.ChallengeId, operation.Nonce, operation.ExpiresAtUnixSeconds); var operationProof = new OperationProof(1, Protocol.ProductId, licenseId, deviceId, sessionId, "session.open", contextHash, operation.ChallengeId, Sign(machine, operationChallenge, licenseId, deviceId, sessionId, "session.open", contextHash));
_ = await serviceB.SessionAsync(new(operationProof, sessionContext), default); await ExpectAsync<SuiteException>(() => serviceA.SessionAsync(new(operationProof, sessionContext), default), "atomic challenge consumption");
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

sealed class ManualTime(long unix) : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unix); }
sealed class MemoryStore(LicenseRecord license) : ISuiteStore
{
    private LicenseRecord _license = license; private readonly ConcurrentDictionary<string, ChallengeRecord> _challenges = new(); private readonly ConcurrentDictionary<string, CompletionRecord> _completions = new(); private readonly ConcurrentDictionary<string, DeviceRecord> _devices = new(); private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new();
    public Task<LicenseRecord?> FindLicenseAsync(string id, CancellationToken _) => Task.FromResult<LicenseRecord?>(_license.LicenseId == id ? _license : null);
    public Task<DeviceRecord?> FindDeviceAsync(string l, string d, CancellationToken _) { _devices.TryGetValue(l + ":" + d, out var value); return Task.FromResult<DeviceRecord?>(value); }
    public Task InsertChallengeAsync(ChallengeRecord c, CancellationToken _) { if (!_challenges.TryAdd(c.ChallengeId, c)) throw new InvalidOperationException(); return Task.CompletedTask; }
    public Task<ChallengeRecord?> FindChallengeAsync(string id, string action, long now, CancellationToken _) { if (_challenges.TryGetValue(id, out var c) && c.Action == action && c.ExpiresAt > now) return Task.FromResult<ChallengeRecord?>(c); return Task.FromResult<ChallengeRecord?>(null); }
    public Task<CompletionRecord?> FindCompletionAsync(string id, CancellationToken _) { _completions.TryGetValue(id, out var c); return Task.FromResult<CompletionRecord?>(c); }
    public Task<SignedAssertionEnvelope> CompleteActivationAsync(ChallengeRecord c, string digest, DeviceRecord d, SignedAssertionEnvelope result, CancellationToken ct) { _ = ct; lock (this) { if (_completions.TryGetValue(c.ChallengeId, out var prior)) { if (prior.RequestDigest != digest) throw new SuiteException(409, "REPLAY_DENIED", "Replay was denied."); return Task.FromResult(prior.Result); } if (!_challenges.TryRemove(c.ChallengeId, out _) || _license.ActivationConsumed) throw new SuiteException(409, "ACTIVATION_REPLAY", "Activation is no longer available."); _license = _license with { ActivationConsumed = true }; _devices[d.LicenseId + ":" + d.DeviceId] = d; _completions[c.ChallengeId] = new(c.ChallengeId, digest, result); return Task.FromResult(result); } }
    public Task<SessionRecord> CompleteSessionAsync(ChallengeRecord c, SessionRecord s, string action, long now, CancellationToken ct) { _ = ct; lock (this) { if (!_challenges.TryRemove(c.ChallengeId, out _)) throw new SuiteException(409, "CHALLENGE_INVALID", "Challenge is invalid or expired."); var key = s.LicenseId + ":" + s.DeviceId; if (_sessions.TryGetValue(key, out var old) && action != "session.open" && old.SessionId != s.SessionId) throw new SuiteException(409, "SESSION_INVALID", "Session is not current."); var time = Math.Max(now, (old?.LastServerTime ?? 0) + 1); var value = s with { LastServerTime = time }; _sessions[key] = value; return Task.FromResult(value); } }
}
