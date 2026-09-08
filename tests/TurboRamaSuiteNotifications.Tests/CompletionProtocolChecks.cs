using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurboRamaSuiteNotifications;

internal static class CompletionProtocolChecks
{
    internal static void Run()
    {
        using var key=RSA.Create(2048);
        var spki=key.ExportSubjectPublicKeyInfo();
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var value=new ExtractionCompletionEvent(1,"TURBORAMA_SUITE","", "TS-NOTICE-TEST",
            Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant(),
            new string('a',32),new string('a',32),1,new string('b',64),
            new string('c',64),"emulators",now-30);
        value=value with{EventId=ExtractionCompletionProtocol.EventId(value)};
        var session=new string('d',64);
        var signature=Convert.ToBase64String(key.SignData(
            ExtractionCompletionProtocol.SigningBytes(value,session,now),
            HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
        var proof=new ExtractionCompletionProof(value,session,now,signature);
        var publicKey=Convert.ToBase64String(spki);
        Check(ExtractionCompletionProtocol.Verify(proof,publicKey,now),"Valid proof rejected.");
        Check(!ExtractionCompletionProtocol.Verify(proof with{SessionId=new string('e',64)},publicKey,now),"Session binding.");
        Check(!ExtractionCompletionProtocol.Verify(proof with{Event=value with{CategoryId="windows"}},publicKey,now),"Category signature.");
        Check(!ExtractionCompletionProtocol.Verify(proof with{Event=value with{CompletedAtUnixSeconds=now}},publicKey,now),"Completion time signature.");
        Check(!ExtractionCompletionProtocol.Verify(proof with{SentAtUnixSeconds=now+1},publicKey,now),"Sending time signature.");
        Check(!ExtractionCompletionProtocol.Verify(proof,publicKey,now+301),"Expired proof.");
        Check(!ExtractionCompletionProtocol.Verify(proof,"bad key",now),"Bad key.");
        using var other=RSA.Create(2048);
        Check(!ExtractionCompletionProtocol.Verify(proof,Convert.ToBase64String(other.ExportSubjectPublicKeyInfo()),now),"Wrong key.");
        Check(ExtractionCompletionProtocol.EventId(value with{CompletedAtUnixSeconds=now+20})==value.EventId,"Retry identity changed.");
        Check(ExtractionCompletionProtocol.EventId(value with{DeviceId=new string('f',64)})!=value.EventId,"Device isolation.");
        Check(ExtractionCompletionProtocol.EventId(value with{LicenseId="TS-OTHER-TEST"})!=value.EventId,"Account isolation.");
        Check(ExtractionCompletionProtocol.EventId(value with{ArtifactVersion=2})!=value.EventId,"Version isolation.");
        var bytes=JsonSerializer.SerializeToUtf8Bytes(proof,ExtractionCompletionProtocol.JsonOptions);
        Check(ExtractionCompletionProtocol.Parse<ExtractionCompletionProof>(bytes)==proof,"Wire roundtrip.");
        foreach(var extra in new[]{"phone","ip","mac","message","destination"})
        {
            var modified=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Insert(1,"\""+extra+"\":\"untrusted\","));
            Reject(()=>ExtractionCompletionProtocol.Parse<ExtractionCompletionProof>(modified));
        }
        var duplicate=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Insert(1,"\"sessionId\":\""+session+"\","));
        Reject(()=>ExtractionCompletionProtocol.Parse<ExtractionCompletionProof>(duplicate));
        Reject(()=>ExtractionCompletionProtocol.Parse<ExtractionCompletionProof>(new byte[17000]));
        Check(!Encoding.UTF8.GetString(bytes).Contains("phone"),"Recipient must not come from Windows.");
        Console.WriteLine("PASS: completion proof, strict JSON, isolation, freshness and stable event identity.");
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    private static void Reject(Action action)
    {try{action();}catch(JsonException){return;}throw new InvalidOperationException("Invalid JSON accepted.");}
}
