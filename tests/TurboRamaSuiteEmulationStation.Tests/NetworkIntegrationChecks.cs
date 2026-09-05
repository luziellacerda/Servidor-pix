using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TurboRamaSuite.Network;
using TurboRamaSuiteOnlineServer;
using static SharedIntegrationChecks;

internal static class NetworkIntegrationChecks
{
    private static void Check(bool condition,string message)
    { if(!condition)throw new InvalidOperationException(message); }

    public static async Task RunAsync(string connection)
    {
        await using var db=NpgsqlDataSource.Create(connection);
        using var online=RSA.Create(2048);using var signer=new RsaAssertionSigner(online);
        using var a=new SyntheticClient();using var b=new SyntheticClient();await a.Seed(db);await b.Seed(db);
        await using var app=CreateApp(connection,signer,true,true);await app.StartAsync();
        using var http=new HttpClient {BaseAddress=new Uri(app.Urls.Single())};
        http.DefaultRequestHeaders.Add("X-Forwarded-For","198.51.100.50");
        http.DefaultRequestHeaders.Add("X-Forwarded-Proto","https");
        http.DefaultRequestHeaders.Add("CF-Connecting-IP","192.0.2.50");
        var suite=SyntheticClient.Hex();var es=SyntheticClient.Hex();var other=SyntheticClient.Hex();
        await Exchange(http,online,a,suite,"SUITE");await Exchange(http,online,a,es,"SHARED");await Exchange(http,online,b,other,"SHARED");
        var context=new NetworkInventoryContext(1,NetworkInventoryContract.Product,a.License,a.Device,es,
            "EMULATIONSTATION",NetworkInventoryContract.Action,a.Fingerprint,"ES-1.1.0",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),[new("02:11:22:33:44:55","WIRELESS",true,false)]);
        var proof=await Sign(context);
        using(var accepted=await Send(http,NetworkInventoryContract.InventoryRoute,proof,"NETWORK"))
        {
            accepted.EnsureSuccessStatusCode();
            var envelope=(await accepted.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
            Check(envelope.Kind==NetworkInventoryContract.ResultKind,"Network result kind must be explicit.");
            var value=StrictJson.Parse<NetworkAssertion>(Convert.FromBase64String(envelope.Payload));
            var message=Encoding.ASCII.GetBytes(NetworkInventoryContract.ResultDomain).Concat(NetworkInventoryContract.Canonical(value)).ToArray();
            Check(online.VerifyData(message,Convert.FromBase64String(envelope.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"Network result signature must verify.");
        }
        var protector=app.Services.GetRequiredService<InventorySensitiveProtector>();
        using(var cmd=db.CreateCommand("SELECT protected_payload,ip_masked,interfaces_masked::text FROM suite.suite_network_inventory WHERE license_id=$1"))
        {
            cmd.Parameters.AddWithValue(a.License);await using var row=await cmd.ExecuteReaderAsync();Check(await row.ReadAsync(),"Authenticated report must be stored.");
            var cipher=(byte[])row[0];var plain=protector.Unprotect(cipher);
            Check(plain.Contains("203.0.113.20",StringComparison.Ordinal)&&!plain.Contains("198.51.100.50",StringComparison.Ordinal),"Untrusted forwarded IP must not replace the observed peer.");
            Check(row.GetString(1)=="203.0.*.*"&&!row.GetString(2).Contains("02:11:22:33",StringComparison.Ordinal),"Public admin fields must mask network addresses.");
            cipher[^1]^=1;try{protector.Unprotect(cipher);throw new InvalidOperationException("Cipher tampering was accepted.");}catch(CryptographicException){}
        }
        await Deny(proof,409);
        var fresh=await Sign(context);
        await Deny(fresh with {Signature=Convert.ToBase64String(RandomNumberGenerator.GetBytes(256))},403);
        await Deny(fresh with {Context=context with {LicenseId=b.License}},403);
        await Deny(fresh with {Context=context with {AppScope="SUITE"}},403);
        await Deny(fresh with {Context=context with {Interfaces=Enumerable.Repeat(context.Interfaces[0],9).ToArray()}},400);
        await Deny(fresh with {Context=context with {Interfaces=[context.Interfaces[0],context.Interfaces[0]]}},400);
        await Deny(fresh with {Context=context with {Interfaces=[context.Interfaces[0] with {Mac="invalid"}]}},400);
        await Deny(fresh with {Context=context with {CollectedAtUnixSeconds=1}},400);
        var moved=await Sign(context with {Interfaces=[new("00:22:33:44:55:66","ETHERNET",false,false)]});
        using(var accepted=await Send(http,NetworkInventoryContract.InventoryRoute,moved,"NETWORK"))accepted.EnsureSuccessStatusCode();
        Check(NetworkInventoryContract.NormalizeIp(System.Net.IPAddress.Parse("::ffff:192.0.2.1"))=="192.0.2.1","Equivalent IP addresses must normalize.");
        Check(NetworkInventoryContract.MaskIp(System.Net.IPAddress.Parse("2001:db8::1"))=="2001:0db8:*:*","IPv6 must be masked.");
        await Exchange(http,online,a,suite,"SUITE",true);await Exchange(http,online,a,es,"SHARED",true);await Exchange(http,online,b,other,"SHARED",true);
        await using(var expire=db.CreateCommand("UPDATE suite.suite_network_inventory SET expires_at=clock_timestamp()-interval '1 second' WHERE license_id=$1")){expire.Parameters.AddWithValue(a.License);await expire.ExecuteNonQueryAsync();}
        await app.Services.GetRequiredService<NetworkInventoryService>().PurgeExpiredAsync(default);
        await using(var count=db.CreateCommand("SELECT count(*) FROM suite.suite_network_inventory WHERE license_id=$1")){count.Parameters.AddWithValue(a.License);Check((long)(await count.ExecuteScalarAsync())! ==0,"Expired telemetry must be deleted.");}
        await app.StopAsync();
        Console.WriteLine("NETWORK HTTP/POSTGRES PASSED: signatures, encryption/tamper, trusted IP, masking, replay, scope, payload limits, MAC change without revocation and retention.");

        async Task<NetworkInventoryProof> Sign(NetworkInventoryContext value)
        {
            var hash=NetworkInventoryContract.Hash(value);
            using var response=await Send(http,NetworkInventoryContract.ChallengeRoute,new NetworkChallengeRequest(1,value.ProductId,value.LicenseId,value.DeviceId,value.SessionId,value.AppScope,value.Action,hash),"NETWORK");
            response.EnsureSuccessStatusCode();var envelope=(await response.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
            var challenge=StrictJson.Parse<NetworkAssertion>(Convert.FromBase64String(envelope.Payload));
            var bytes=NetworkInventoryContract.SigningMessage(new(1,value.ProductId,value.LicenseId,value.DeviceId,value.SessionId,value.AppScope,value.Action,hash,challenge.ChallengeId,challenge.Nonce,challenge.ExpiresAtUnixSeconds));
            return new(value,challenge.ChallengeId,Convert.ToBase64String(a.Key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));
        }
        async Task Deny(NetworkInventoryProof value,int status)
        {using var response=await Send(http,NetworkInventoryContract.InventoryRoute,value,"NETWORK");Check((int)response.StatusCode==status,"Network report must be refused: "+status+" actual "+(int)response.StatusCode);}
    }
}
