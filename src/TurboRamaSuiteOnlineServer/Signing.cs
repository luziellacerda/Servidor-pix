using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

public interface IAssertionSigner
{
    string KeyId { get; }
    SignedAssertionEnvelope Sign(object assertion);
}

public sealed class RsaAssertionSigner : IAssertionSigner, IDisposable
{
    private readonly RSA _rsa;
    public RsaAssertionSigner(RSA rsa)
    {
        _rsa = rsa; KeyId = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }
    public string KeyId { get; }
    public SignedAssertionEnvelope Sign(object assertion)
    {
        var payload = Protocol.CanonicalAssertion(assertion); var domain = Encoding.ASCII.GetBytes(Protocol.AssertionDomain(assertion));
        var message = new byte[domain.Length + payload.Length]; domain.CopyTo(message, 0); payload.CopyTo(message, domain.Length);
        try { return new(1, assertion switch { ActivationChallengeAssertion a => a.Kind, OperationChallengeAssertion a => a.Kind, ActivationResultAssertion a => a.Kind, SessionAssertion a => a.Kind, _ => throw new ArgumentOutOfRangeException(nameof(assertion)) }, Protocol.Algorithm, KeyId, Convert.ToBase64String(payload), Convert.ToBase64String(_rsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))); }
        finally { CryptographicOperations.ZeroMemory(payload); CryptographicOperations.ZeroMemory(message); }
    }
    public void Dispose() => _rsa.Dispose();
}
