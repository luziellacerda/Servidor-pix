using System.Security.Cryptography;
using System.Text;
using TurboRamaSuiteNotifications;

internal static class DownloadCompletionChecks
{
    internal static void Run()
    {
        using var key = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var item = new string('a', 32);
        var device = new string('b', 64);
        var session = new string('c', 64);
        var value = new DownloadCompletionEvent(1, "TURBORAMA_SUITE", "", "TS-TEST-DOWNLOAD",
            device, new string('d', 32), item, item, 1, new string('e', 64),
            new string('f', 64), "xbox-360", DownloadCompletionProtocol.FileReady, now - 1);
        value = value with { EventId = DownloadCompletionProtocol.EventId(value) };
        var proof = Sign(value);
        var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        Check(DownloadCompletionProtocol.Verify(proof, spki, now), "Valid ISO completion proof rejected.");
        Check(DownloadCompletionProtocol.EventId(value with { CompletedAtUnixSeconds = now - 2 }) == value.EventId,
            "Retry completion clock changed the operation identity.");
        Check(DownloadCompletionProtocol.EventId(value with { DownloadId = new string('1', 32) }) != value.EventId,
            "A distinct download of the same content was deduplicated.");
        Check(!DownloadCompletionProtocol.Verify(proof with { SessionId = new string('2', 64) }, spki, now),
            "Proof crossed sessions.");
        Check(!DownloadCompletionProtocol.Verify(proof with { Event = value with { CategoryId = "psp" } }, spki, now),
            "Category tampering accepted.");
        Check(!DownloadCompletionProtocol.Verify(proof with { SentAtUnixSeconds = now - 301 }, spki, now),
            "Stale proof accepted.");
        Check(!DownloadCompletionProtocol.Verify(Sign(value with { CompletedAtUnixSeconds = now - 8 * 86400 }), spki, now),
            "Expired completion accepted.");
        foreach (var kind in new[] { "", "DOWNLOADING", "FAILED", "CANCELED" })
        {
            var bad = value with { CompletionKind = kind };
            try { DownloadCompletionProtocol.ValidateEvent(bad); throw new InvalidOperationException("Invalid success state accepted."); }
            catch (ArgumentException) { }
        }
        var data = new ExtractionCompletionMessageData("Pessoa Teste", "Halo 3.iso", "Xbox 360",
            DateTimeOffset.FromUnixTimeSeconds(now - 1), "TS-0123456789AB");
        foreach (var kind in new[] { DownloadCompletionProtocol.FileReady, DownloadCompletionProtocol.Extracted })
        {
            var messages = Enumerable.Range(0, 10).Select(v => DownloadCompletionMessage.Format(data,
                DateTimeOffset.FromUnixTimeSeconds(now), v, kind)).ToArray();
            Check(messages.Distinct().Count() == 10, "Download openings are not distinct.");
            foreach (var message in messages)
            {
                Check(message.Contains("Halo 3.iso") && message.Contains("TS-0123456789AB"), "Completion details missing.");
                Check(!message.Contains("arquivos verificados", StringComparison.OrdinalIgnoreCase), "Unverified integrity claim.");
                if (kind == DownloadCompletionProtocol.FileReady)
                    Check(message.Contains("*DOWNLOAD CONCLUÍDO*")
                        && !message.Contains("descompactação concluída", StringComparison.OrdinalIgnoreCase)
                        && !message.Contains("descompactação concluídos", StringComparison.OrdinalIgnoreCase)
                        && !message.Contains("foram descompactados", StringComparison.OrdinalIgnoreCase),
                        "Raw download claims extraction.");
                else Check(message.Contains("DOWNLOAD E DESCOMPACTAÇÃO CONCLUÍDOS"), "Extracted completion title missing.");
            }
        }
        Console.WriteLine("PASS: all-download RSA-PSS proof, distinct operations, retry identity, tampering, expiry and 20 accurate message variants.");

        DownloadCompletionProof Sign(DownloadCompletionEvent e) => new(e, session, now,
            Convert.ToBase64String(key.SignData(DownloadCompletionProtocol.SigningBytes(e, session, now),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
    }

    private static void Check(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); }
}
