using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using TurboRamaSuite.Management;
using TurboRamaSuiteOnlineServer;
using static SharedIntegrationChecks;

internal static class SessionAdminHttpChecks
{
    private static void Check(bool condition,string message)=>SharedIntegrationChecks.Check(condition,message);
    public static async Task RunAsync(string connection)
    {
        await using var db=NpgsqlDataSource.Create(connection);
        using var online=RSA.Create(2048);using var signer=new RsaAssertionSigner(online);
        using var a=new SyntheticClient();using var b=new SyntheticClient();await a.Seed(db);await b.Seed(db);
        await using var app=CreateApp(connection,signer,true);await app.StartAsync();
        using var api=new HttpClient {BaseAddress=new Uri(app.Urls.Single())};
        var suite=SyntheticClient.Hex();var es=SyntheticClient.Hex();var other=SyntheticClient.Hex();
        await Exchange(api,online,a,suite,"SUITE");await Exchange(api,online,a,es,"SHARED");await Exchange(api,online,b,other,"SHARED");
        await a.PublishNetworkAsync(api,es);
        await using var admin=await SyntheticAdminHost.StartAsync(connection);
        var query=new SessionQuery([a.License]);
        using(var denied=await admin.Post("sessions/query",query,null,token:false))Check(denied.StatusCode==HttpStatusCode.NotFound,"Missing internal token must fail.");
        using(var denied=await admin.Post("sessions/query",query,null))Check(denied.StatusCode==HttpStatusCode.Forbidden,"Token alone must not authorize a session query.");
        using(var denied=await admin.Post("sessions/network",query,SessionManagementPermissions.Read))Check(denied.StatusCode==HttpStatusCode.Forbidden,"Session permission must not grant network access.");
        using(var network=await admin.Post("sessions/network",query,SessionManagementPermissions.NetworkRead))
        {
            network.EnsureSuccessStatusCode();var reports=(await network.Content.ReadFromJsonAsync<ManagedNetworkReports>())!.Reports;
            Check(reports.Length==1&&reports[0].LicenseId==a.License&&reports[0].Interfaces[0].Mac=="**:**:**:**:44:55",
                "The actual admin database role must query only the selected masked network report.");
        }
        using(var list=await admin.Post("sessions/query",query,SessionManagementPermissions.Read))
        {
            list.EnsureSuccessStatusCode();var rows=(await list.Content.ReadFromJsonAsync<ManagedSessions>())!.Sessions;
            Check(rows.Length==2&&rows.All(s=>s.LicenseId==a.License&&s.State=="ONLINE"&&s.LastContactAtUnixSeconds>0),"A query must show separate real Suite/ES contacts for the selected license.");
        }
        using(var tooMany=await admin.Post("sessions/query",new SessionQuery(Enumerable.Range(0,51).Select(i=>"TS-SYNTHETIC-"+i).ToArray()),SessionManagementPermissions.Read))Check((int)tooMany.StatusCode==400,"Batch limits must be enforced.");
        var target=new RevokeEsSessionRequest(a.License,a.Device,"EMULATIONSTATION",es,es,Guid.NewGuid().ToString("N"));
        foreach(var controls in new[]{(Claim:SessionManagementPermissions.Read,Csrf:true,Step:true),(Claim:SessionManagementPermissions.Revoke,Csrf:false,Step:true),(Claim:SessionManagementPermissions.Revoke,Csrf:true,Step:false)})
        {using var denied=await admin.Post("sessions/revoke",target,controls.Claim,controls.Csrf,controls.Step);Check((int)denied.StatusCode==403,"Revocation requires its permission, CSRF and fresh step-up.");}
        using(var wrongApp=await admin.Post("sessions/revoke",target with {AppScope="SUITE"},SessionManagementPermissions.Revoke,true,true))Check((int)wrongApp.StatusCode==400,"A Suite target cannot be revoked by ES action.");
        using(var wrongLicense=await admin.Post("sessions/revoke",target with {LicenseId=b.License},SessionManagementPermissions.Revoke,true,true))Check((int)wrongLicense.StatusCode==409,"Cross-license device target must not affect another customer.");
        var queued=await SharedIntegrationChecks.Proof(api,online,a,es,"SHARED",true);
        using(var result=await admin.Post("sessions/revoke",target,SessionManagementPermissions.Revoke,true,true))
        {result.EnsureSuccessStatusCode();Check((await result.Content.ReadFromJsonAsync<RevokeEsSessionResult>())!.Code=="REVOKED","Exact ES session must be revoked.");}
        using(var denied=await Send(api,"/v1/suite/sessions",queued,"SHARED"))Check(!denied.IsSuccessStatusCode,"An outstanding target heartbeat must not revive a revoked session.");
        await Exchange(api,online,a,suite,"SUITE",true);await Exchange(api,online,b,other,"SHARED",true);
        var replacement=SyntheticClient.Hex();await Exchange(api,online,a,replacement,"SHARED");
        using(var replay=await admin.Post("sessions/revoke",target,SessionManagementPermissions.Revoke,true,true))
        {replay.EnsureSuccessStatusCode();Check((await replay.Content.ReadFromJsonAsync<RevokeEsSessionResult>())!.Code=="ALREADY_REVOKED","Retry must return the original receipt.");}
        using(var stale=await admin.Post("sessions/revoke",target with {RequestId=Guid.NewGuid().ToString("N")},SessionManagementPermissions.Revoke,true,true))Check((int)stale.StatusCode==409,"Stale confirmation must not revoke a newer session.");
        await Exchange(api,online,a,replacement,"SHARED",true);
        // Race a validated shared-ES reopening against an exact old target confirmation. Either
        // lock order is permitted, but the subsequently created session survives.
        var next=SyntheticClient.Hex();var raceTarget=target with {TargetSessionId=replacement,ExpectedSessionId=replacement,RequestId=Guid.NewGuid().ToString("N")};
        await Task.WhenAll(Exchange(api,online,a,next,"SHARED"),Task.Run(async()=>
        {using var result=await admin.Post("sessions/revoke",raceTarget,SessionManagementPermissions.Revoke,true,true);Check(result.IsSuccessStatusCode||(int)result.StatusCode==409,"CAS race must be decided atomically.");}));
        using(var stale=await admin.Post("sessions/revoke",raceTarget with {RequestId=Guid.NewGuid().ToString("N")},SessionManagementPermissions.Revoke,true,true))Check((int)stale.StatusCode==409,"An old confirmation must not revoke the replacement shared ES session.");
        await Exchange(api,online,a,next,"SHARED",true);await Exchange(api,online,a,suite,"SUITE",true);await Exchange(api,online,b,other,"SHARED",true);
        using(var refreshed=await admin.Post("sessions/query",query,SessionManagementPermissions.Read))
        {
            refreshed.EnsureSuccessStatusCode();var rows=(await refreshed.Content.ReadFromJsonAsync<ManagedSessions>())!.Sessions;
            Check(rows.Length==2&&rows.Single(s=>s.AppScope=="EMULATIONSTATION") is {State:"ONLINE"} current&&
                current.SessionId==next&&current.LastContactAtUnixSeconds>0&&
                rows.Single(s=>s.AppScope=="SUITE").SessionId==suite,
                "The real panel query must show the current reopened ES online without inheriting the old revocation.");
        }
        using(var history=await admin.Get("customer-activity/"+a.License))history.EnsureSuccessStatusCode();
        await using(var grant=db.CreateCommand("SELECT has_column_privilege('turborama-suite-admin','suite.suite_network_inventory','protected_payload','SELECT'),has_column_privilege('turborama-suite-admin','suite.suite_network_inventory','ip_masked','SELECT')"))
        {await using var row=await grant.ExecuteReaderAsync();await row.ReadAsync();Check(!row.GetBoolean(0)&&row.GetBoolean(1),"Admin database role must read only masked network fields.");}
        await using(var audit=db.CreateCommand("SELECT count(*) FROM suite.suite_audit_events WHERE license_id=$1 AND event_type='SUITE_ES_SESSION_REVOKED'"))
        {audit.Parameters.AddWithValue(a.License);Check((long)(await audit.ExecuteScalarAsync())!>=1,"The target revocation must be audited.");}
        await app.StopAsync();
        Console.WriteLine("SESSION ADMIN HTTP/POSTGRES PASSED: Unix socket token, claims, CSRF, step-up, batch bound, contacts, exact target, CAS race, replay, audit, A/B and masked DB grants.");
    }
}

internal sealed class SyntheticAdminHost : IAsyncDisposable
{
    internal string SocketPath=>Path.Combine(directory,"admin.sock");
    internal string TokenPath=>Path.Combine(directory,"token");
    private readonly Process process;
    private readonly string directory;
    private readonly string token;
    private readonly HttpClient http;
    private SyntheticAdminHost(Process p,string d,string t,HttpClient h)=>(process,directory,token,http)=(p,d,t,h);
    public static async Task<SyntheticAdminHost> StartAsync(string connection)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        var directory=Path.Combine(Path.GetTempPath(),"es-admin-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var token=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenPath=Path.Combine(directory,"token");await File.WriteAllTextAsync(tokenPath,token);File.SetUnixFileMode(tokenPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        var socket=Path.Combine(directory,"admin.sock");
        var binary=Path.GetFullPath("src/TurboRamaSuiteAdminServer/bin/Release/net8.0/TurboRamaSuiteAdminServer.dll");
        var start=new ProcessStartInfo("dotnet",[binary]){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["SUITE_ADMIN_SOCKET"]=socket;start.Environment["SUITE_ADMIN_TOKEN_FILE"]=tokenPath;
        start.Environment["SUITE_ADMIN_PEPPER_FILE"]=tokenPath;
        start.Environment["SUITE_ADMIN_CONNECTION"]=new NpgsqlConnectionStringBuilder(connection){Options="-c role=turborama-suite-admin"}.ConnectionString;
        start.Environment["SUITE_CONTENT_ADMIN_ENABLED"]="0";start.Environment["SUITE_COMMERCE_ENABLED"]="0";
        var process=Process.Start(start)??throw new InvalidOperationException("Synthetic admin failed to start.");
        _=process.StandardOutput.ReadToEndAsync();_=process.StandardError.ReadToEndAsync();
        var handler=new SocketsHttpHandler {ConnectCallback=async(_,ct)=>
        {var client=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);try{await client.ConnectAsync(new UnixDomainSocketEndPoint(socket),ct);return new NetworkStream(client,true);}catch{client.Dispose();throw;}}};
        var host=new SyntheticAdminHost(process,directory,token,new HttpClient(handler){BaseAddress=new Uri("http://localhost/"),Timeout=TimeSpan.FromSeconds(10)});
        for(var attempt=0;attempt<100;attempt++)
        {
            if(process.HasExited)break;
            if(File.Exists(socket))try{using var ready=await host.Get("health");if(ready.IsSuccessStatusCode)return host;}catch(HttpRequestException){}
            await Task.Delay(100);
        }
        await host.DisposeAsync();throw new InvalidOperationException("Synthetic admin did not become healthy.");
    }
    public Task<HttpResponseMessage> Post(string path,object body,string? claim,bool csrf=false,bool stepUp=false,bool token=true)
    {
        var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
        if(token)request.Headers.Add("X-Suite-Admin-Token",this.token);
        if(claim is not null)
        {
            request.Headers.Add("X-Suite-Admin-Actor","synthetic-admin");request.Headers.Add("X-Suite-Admin-Claims",claim);
            request.Headers.Add("X-Suite-Client-Ip-Digest",new string('a',64));
        }
        if(csrf)request.Headers.Add("X-Suite-Csrf-Verified","1");
        if(stepUp)request.Headers.Add("X-Suite-Step-Up-At",DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Send(request);
    }
    public Task<HttpResponseMessage> Get(string path)
    {var request=new HttpRequestMessage(HttpMethod.Get,path);request.Headers.Add("X-Suite-Admin-Token",token);return Send(request);}
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {using(request)return await http.SendAsync(request);}
    public async ValueTask DisposeAsync()
    {
        http.Dispose();if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();process.Dispose();
        Directory.Delete(directory,true);
    }
}
