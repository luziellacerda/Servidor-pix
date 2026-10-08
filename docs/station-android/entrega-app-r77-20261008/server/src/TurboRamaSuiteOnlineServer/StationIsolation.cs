using System.Security.Cryptography;
using Npgsql;

namespace TurboRamaSuiteOnlineServer;

public static class StationIsolation
{
    public static string Connection(string file)
    {
        var text=File.ReadAllText(file).Trim();
        if(text.Length is 0 or >4096)throw new InvalidOperationException("Station database configuration is invalid.");
        var parsed=new NpgsqlConnectionStringBuilder(text);
        if(parsed.Username!="turborama-station-api" || string.IsNullOrWhiteSpace(parsed.Password)
            || parsed.Host is not ("127.0.0.1" or "localhost" or "/var/run/postgresql"))
            throw new InvalidOperationException("Dedicated Station database identity is required.");
        return text;
    }
    public static void IndependentSecrets(IConfiguration configuration,string pepper,string signingPem,string? downloadFile)
    {
        var expectedPepper=configuration["Station:Isolation:SuitePepperSha256"];
        var expectedKey=configuration["Station:Isolation:SuiteAssertionKeyId"];
        if(!Fingerprint(expectedPepper) || !Fingerprint(expectedKey))
            throw new InvalidOperationException("Suite key fingerprints are required for isolated Station.");
        var bytes=Convert.FromBase64String(pepper);
        try
        {
            if(bytes.Length<32 || Hash(bytes)==expectedPepper)
                throw new InvalidOperationException("Station activation secret must be independent.");
            using var rsa=RSA.Create();rsa.ImportFromPem(signingPem);
            if(Hash(rsa.ExportSubjectPublicKeyInfo())==expectedKey)
                throw new InvalidOperationException("Station assertion key must be independent.");
            if(downloadFile is not null)
            {
                var key=File.ReadAllBytes(downloadFile);
                try
                {
                    if(key.Length!=32 || Hash(key)==expectedPepper || CryptographicOperations.FixedTimeEquals(bytes,key))
                        throw new InvalidOperationException("Station download secret must be independent.");
                }
                finally{CryptographicOperations.ZeroMemory(key);}
            }
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    private static bool Fingerprint(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
