using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Npgsql;
using TurboRamaSuiteOnlineServer;

internal static class BrowserFixtureHost
{
    public static async Task RunAsync(string connection)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        var directory=Path.GetFullPath("outputs/es-browser-fixtures");Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        if(File.Exists(Path.Combine(directory,"state.json")))File.Delete(Path.Combine(directory,"state.json"));
        await using var db=NpgsqlDataSource.Create(connection);
        using var online=RSA.Create(2048);using var signer=new RsaAssertionSigner(online);
        using var a=new SyntheticClient();using var b=new SyntheticClient();await a.Seed(db);await b.Seed(db);
        await using var api=SharedIntegrationChecks.CreateApp(connection,signer,true);await api.StartAsync();
        using var http=new HttpClient{BaseAddress=new Uri(api.Urls.Single())};
        var sessions=new[]{(Person:a,Id:SyntheticClient.Hex(),Scope:"SUITE"),(Person:a,Id:SyntheticClient.Hex(),Scope:"SHARED"),(Person:b,Id:SyntheticClient.Hex(),Scope:"SHARED")};
        foreach(var session in sessions)await SharedIntegrationChecks.Exchange(http,online,session.Person,session.Id,session.Scope);
        await a.PublishNetworkAsync(http,sessions[1].Id);
        await using var admin=await SyntheticAdminHost.StartAsync(connection);
        var ledger=Enumerable.Range(0,27).Select(i=>new
        {
            licenseId=i==0?a.License:i==1?b.License:"TS-SYNTHETIC-PAGE-"+i.ToString("D2"),customerId=i+1,
            customer=i==0?"Cliente A <img src=x onerror=alert(1)>":"Cliente sintético "+i,email="fixture"+i+"@example.invalid",phone="",orderRef="synthetic",purchaseId=i+1,
            deliveryState="PROVISIONED",financialState="PAID",createdAt="2026-09-05",updatedAt="2026-09-05",status="ACTIVE",enrollmentState="BOUND",activationConsumed=true,
            otpState="CONSUMED",activeDevices=1,sessionId=(string?)null,entitlement="",outboxStatus="",outboxError="",deviceId=i==0?a.Device:i==1?b.Device:"",hardwareFingerprint="",
            bindingType="SOFTWARE_BOUND_ONLINE",deviceAlgorithm="rsa-pss-sha256",deviceStatus="ACTIVE",deviceCreatedAt="2026-09-05",deviceUpdatedAt="2026-09-05"
        });
        var ledgerPath=Path.Combine(directory,"customers.json");await File.WriteAllTextAsync(ledgerPath,JsonSerializer.Serialize(ledger));
        using var tls=RSA.Create(2048);var certRequest=new CertificateRequest("CN=localhost",tls,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(IPAddress.Loopback);certRequest.CertificateExtensions.Add(san.Build());
        using var cert=certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
        var certPath=Path.Combine(directory,"synthetic.pfx");await File.WriteAllBytesAsync(certPath,cert.Export(X509ContentType.Pfx,"synthetic-only"));
        using var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();var port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        var password="Synthetic-browser-only-2026!";var salt=RandomNumberGenerator.GetBytes(16);
        var hash="pbkdf2-sha256$600000$"+Convert.ToBase64String(salt)+"$"+Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32));
        var binary=Path.GetFullPath("src/TurboRamaPixOnlineServer/bin/Release/net8.0/TurboRamaPixOnlineServer.dll");
        var start=new ProcessStartInfo("dotnet",[binary]){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var pair in new Dictionary<string,string>
        {
            ["ASPNETCORE_URLS"]="https://localhost:"+port,["Kestrel__Certificates__Default__Path"]=certPath,["Kestrel__Certificates__Default__Password"]="synthetic-only",
            ["TURBORAMA_SERVER_STATE_FILE"]=Path.Combine(directory,"state.json"),["TURBORAMA_SERVER_STATE_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["TURBORAMA_SERVER_SECRET_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["TURBORAMA_ADMIN_USERNAME"]="synthetic-admin",
            ["TURBORAMA_ADMIN_PASSWORD_HASH"]=hash,["TURBORAMA_ADMIN_PUBLIC_HOST"]="localhost",["TURBORAMA_ADMIN_KEY_DIRECTORY"]=Path.Combine(directory,"dpapi"),
            ["TURBORAMA_SUITE_ADMIN_SOCKET"]=admin.SocketPath,["TURBORAMA_SUITE_ADMIN_TOKEN_FILE"]=admin.TokenPath,["TURBORAMA_SUITE_CUSTOMERS_FILE"]=ledgerPath
        })start.Environment[pair.Key]=pair.Value;
        using var process=Process.Start(start)??throw new InvalidOperationException("Synthetic panel failed to start.");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        try
        {
            using var readyClient=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,remote,_,_)=>remote?.Thumbprint==cert.Thumbprint});
            var ready=false;
            for(var n=0;n<100&&!process.HasExited;n++)
            {try{using var response=await readyClient.GetAsync("https://localhost:"+port+"/admin/login");if(response.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(100);}
            if(!ready)throw new InvalidOperationException("Synthetic panel did not become healthy.");
            await File.WriteAllTextAsync(Path.Combine(directory,"fixture.json"),JsonSerializer.Serialize(new{url="https://localhost:"+port,username="synthetic-admin",password,licenseA=a.License,licenseB=b.License,sessionA=sessions[1].Id}));
            Console.WriteLine("BROWSER_FIXTURES_READY");
            var stopFile=Path.Combine(directory,"stop");if(File.Exists(stopFile))File.Delete(stopFile);
            var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
            while(!File.Exists(stopFile)&&DateTimeOffset.UtcNow<deadline)
            {
                await Task.Delay(5000);
                foreach(var session in sessions)try{await SharedIntegrationChecks.Exchange(http,online,session.Person,session.Id,session.Scope,true);}catch(HttpRequestException){}
            }
        }
        finally
        {
            if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(directory,"panel.log"),await stdout+await stderr);await api.StopAsync();
        }
    }
}
