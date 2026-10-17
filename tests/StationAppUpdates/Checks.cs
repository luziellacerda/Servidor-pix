// Disposable loopback lab. No production key, license, APK, ROM or database.
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TurboRamaSuiteOnlineServer;

var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
var root = Path.Combine(Path.GetTempPath(), "station-update-lab-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root,"apks"));
using var rsa = RSA.Create(2048);
using var signer = new StationResponseSigner(rsa.ExportPkcs8PrivateKeyPem());
var bytes = RandomNumberGenerator.GetBytes(8192);
var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
var release = new StationAppRelease(1, StationAppUpdateCatalog.Package, 200, "Lab 200", digest, digest,
    bytes.Length, new string('c',64), 28, "arm64-v8a", "Synthetic release, not an APK.");
var keyFile = Path.Combine(root,"grant-key"); File.WriteAllBytes(keyFile,RandomNumberGenerator.GetBytes(32));
if (OperatingSystem.IsLinux()) File.SetUnixFileMode(keyFile,UnixFileMode.UserRead|UnixFileMode.UserWrite);
using var cipher=StationGrantCipher.Load(keyFile);
File.WriteAllBytes(Path.Combine(root,"apks",digest+".apk"),bytes);
void Publish(StationAppRelease r) => File.WriteAllBytes(Path.Combine(root,"current.json"),JsonSerializer.SerializeToUtf8Bytes(r,StrictJson.Options));
Publish(release);
var access = new LabAccess();
var updates = new StationAppUpdates(new(root,true),access,signer,cipher);
StationAppUpdateRequest Request(bool auth=false) => new(1,StationProtocol.Prefix+(auth?"request-app-update-download":"request-app-update-check")+"/v1",
    StationProtocol.Product,StationProtocol.Application,access.User.DeviceId,Guid.NewGuid().ToString("N"),StationAppUpdateCatalog.Package,
    100,35,"arm64-v8a",release.CertificateSha256,auth?release.ReleaseId:"");
JsonElement Verified(object envelope) {
    var e=JsonSerializer.SerializeToElement(envelope,StrictJson.Options);
    var p=StationProtocol.Decode(e.GetProperty("payload").GetString()!,16384);
    var s=StationProtocol.Decode(e.GetProperty("signature").GetString()!,512);
    Check(rsa.VerifyData(p,s,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"real signed update response");
    var value=JsonSerializer.Deserialize<JsonElement>(p);
    Check(value.GetProperty("sessionId").GetString()==access.User.SessionId,"bound session identity");
    Check(!System.Text.Encoding.UTF8.GetString(p).Contains(root),"private filesystem never appears on wire");
    return value;
}
async Task Reject(Func<Task> action,int status,string name) {
    try {await action();throw new Exception("Expected rejection: "+name);}
    catch(SuiteException ex){Check(ex.StatusCode==status,name);}
}
string Grant(object envelope)=>Verified(envelope).GetProperty("grantId").GetString()!;
try {
    var checkRequest=Request(); var response=Verified(await updates.CheckAsync("valid",checkRequest,default));
    Check(response.GetProperty("available").GetBoolean(),"new version offered");
    Check(response.GetProperty("requestId").GetString()==checkRequest.RequestId,"fresh check request bound");
    Check(!Verified(await updates.CheckAsync("valid",Request() with {VersionCode=200},default)).GetProperty("available").GetBoolean(),"same version not offered");
    foreach(var request in new[]{Request() with {VersionCode=201},Request() with {AndroidSdk=27},Request() with {Abi="armeabi-v7a"},Request() with {CertificateSha256=new string('e',64)}})
        Check(!Verified(await updates.CheckAsync("valid",request,default)).GetProperty("available").GetBoolean(),"incompatible/downgrade candidate not offered");
    await Reject(async()=>{await updates.CheckAsync("invalid",Request(),default);},401,"no anonymous manifest access");
    await Reject(async()=>{await updates.CheckAsync("valid",Request() with {DeviceId=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32))},default);},403,"device mismatch");
    await Reject(async()=>{await updates.CheckAsync("valid",Request() with {Domain=StationProtocol.Prefix+"request-download/v1"},default);},400,"domain separation");
    await Reject(async()=>{await updates.CheckAsync("valid",Request() with {RequestId="../test"},default);},400,"request nonce syntax");
    await Reject(async()=>{await updates.AuthorizeAsync("valid",Request(true) with {ReleaseId=new string('d',64)},default);},409,"changed release cannot authorize");
    var grant=Grant(await updates.AuthorizeAsync("valid",Request(true),default));
    var resolved=await updates.ConsumeAsync("valid",grant,default);
    Check(File.ReadAllBytes(resolved.Path).SequenceEqual(bytes),"authorized immutable file selected");
    await Reject(async()=>{await updates.ConsumeAsync("valid",grant,default);},404,"one-use grant replay rejected");
    grant=Grant(await updates.AuthorizeAsync("valid",Request(true),default));
    access.Expired=true;
    await Reject(async()=>{await updates.ConsumeAsync("valid",grant,default);},404,"expired grant rejected");access.Expired=false;
    grant=Grant(await updates.AuthorizeAsync("valid",Request(true),default));
    var original=access.User;access.User=original with {DeviceId=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32))};
    await Reject(async()=>{await updates.ConsumeAsync("valid",grant,default);},404,"other device cannot consume");
    access.User=original with {SessionId=new string('b',64)};
    await Reject(async()=>{await updates.ConsumeAsync("valid",grant,default);},404,"rotated session cannot consume bound ciphertext");access.User=original;
    grant=Grant(await updates.AuthorizeAsync("valid",Request(true),default));
    var results=await Task.WhenAll(Enumerable.Range(0,8).Select(async _=>{try{await updates.ConsumeAsync("valid",grant,default);return true;}catch(SuiteException){return false;}}));
    Check(results.Count(x=>x)==1,"concurrent grant consumption exactly once");
    grant=Grant(await updates.AuthorizeAsync("valid",Request(true),default));
    File.Delete(Path.Combine(root,"current.json"));
    await Reject(async()=>{await updates.ConsumeAsync("valid",grant,default);},404,"withdrawing current update revokes pending grant");
    Check(!Verified(await updates.CheckAsync("valid",Request(),default)).GetProperty("available").GetBoolean(),"empty release directory cleanly means no update");Publish(release);
    if(OperatingSystem.IsLinux()){
        File.Move(Path.Combine(root,"apks",digest+".apk"),Path.Combine(root,"immutable.apk"));
        File.CreateSymbolicLink(Path.Combine(root,"apks",digest+".apk"),Path.Combine(root,"immutable.apk"));
        await Reject(async()=>{await updates.CheckAsync("valid",Request(),default);},503,"symlink target denied");
        File.Delete(Path.Combine(root,"apks",digest+".apk"));File.Move(Path.Combine(root,"immutable.apk"),Path.Combine(root,"apks",digest+".apk"));
    }
    Publish(release with {PackageName="other.package"});
    await Reject(async()=>{await updates.CheckAsync("valid",Request(),default);},503,"wrong package publication denied");Publish(release);
    // Real HTTP routes, strict JSON, MIME/length, cache headers and streaming.
    var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,0));
    builder.Services.AddSingleton(updates);await using var app=builder.Build();app.MapStationAppUpdates(true);await app.StartAsync();
    var url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    using var http=new HttpClient{BaseAddress=new Uri(url)};
    async Task<HttpResponseMessage> Post(string route,StationAppUpdateRequest request,string? bearer="valid"){
        using var message=new HttpRequestMessage(HttpMethod.Post,route){Content=new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request,StrictJson.Options))};
        message.Content.Headers.ContentType=new("application/json");if(bearer is not null)message.Headers.Add("Authorization","Bearer "+bearer);
        return await http.SendAsync(message);
    }
    using(var result=await Post(StationAppUpdates.Root+"/check",Request())){
        Check(result.StatusCode==HttpStatusCode.OK,"check HTTP success "+(int)result.StatusCode+" "+(result.StatusCode==HttpStatusCode.OK?"":await result.Content.ReadAsStringAsync()));Check(result.Headers.CacheControl?.NoStore==true,"check not cached");
        Verified(JsonSerializer.Deserialize<JsonElement>(await result.Content.ReadAsByteArrayAsync()));
    }
    using(var result=await Post(StationAppUpdates.Root+"/check",Request(),null))Check(result.StatusCode==HttpStatusCode.Unauthorized,"HTTP missing bearer denied");
    // The Java check omits the optional empty releaseId; Suite's shared
    // RejectNullProperties contract remains untouched for all other products.
    var wireRequest=JsonSerializer.SerializeToNode(Request(),StrictJson.Options)!.AsObject();wireRequest.Remove("releaseId");
    using(var request=new HttpRequestMessage(HttpMethod.Post,StationAppUpdates.Root+"/check"){Content=new StringContent(wireRequest.ToJsonString())}){
        request.Headers.Add("Authorization","Bearer valid");using var result=await http.SendAsync(request);Check(result.StatusCode==HttpStatusCode.OK,"Java-compatible omitted releaseId accepted");
    }
    wireRequest["filePath"]="/private/never-selected";
    using(var request=new HttpRequestMessage(HttpMethod.Post,StationAppUpdates.Root+"/check"){Content=new StringContent(wireRequest.ToJsonString())}){
        request.Headers.Add("Authorization","Bearer valid");using var result=await http.SendAsync(request);Check(result.StatusCode==HttpStatusCode.BadRequest,"arbitrary file field refused");
    }
    using(var result=await Post(StationAppUpdates.Root+"/check?extra=1",Request()))Check(result.StatusCode==HttpStatusCode.BadRequest,"query on signed POST refused");
    using(var result=await Post(StationAppUpdates.Root+"/authorize",Request(true))){
        Check(result.StatusCode==HttpStatusCode.OK,"authorize HTTP success");grant=Grant(JsonSerializer.Deserialize<JsonElement>(await result.Content.ReadAsByteArrayAsync()));
    }
    using(var request=new HttpRequestMessage(HttpMethod.Get,StationAppUpdates.Root+"/artifacts/"+grant)){
        request.Headers.Add("Authorization","Bearer valid");using var result=await http.SendAsync(request);
        Check(result.StatusCode==HttpStatusCode.OK,"authorized HTTP stream");
        Check(result.Content.Headers.ContentType?.MediaType=="application/vnd.android.package-archive","APK MIME");
        Check(result.Content.Headers.ContentLength==bytes.Length,"APK exact length");
        Check((await result.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes),"APK streamed complete");
        Check(result.Headers.CacheControl?.NoStore==true,"APK never public cached");
    }
    foreach(var suffix in new[]{"?token=test",""}){
        using var request=new HttpRequestMessage(HttpMethod.Get,StationAppUpdates.Root+"/artifacts/"+grant+suffix);
        request.Headers.Add("Authorization","Bearer valid");if(suffix=="")request.Headers.Add("Range","bytes=0-1");
        using var result=await http.SendAsync(request);Check(result.StatusCode==HttpStatusCode.BadRequest,"query/Range refused");
    }
    using(var request=new HttpRequestMessage(HttpMethod.Post,StationAppUpdates.Root+"/check"){Content=new StringContent(new string('x',9000))}){
        using var result=await http.SendAsync(request);Check(result.StatusCode==HttpStatusCode.RequestEntityTooLarge,"bounded HTTP request body");
    }
    using(var result=await http.GetAsync("/apks/"+digest+".apk"))Check(result.StatusCode==HttpStatusCode.NotFound,"no static APK path route");
    await app.StopAsync();
    Console.WriteLine($"PASS StationAppUpdates checks={checks} realSignature=true syntheticGrantStore=true productionDatabase=false physicalAndroid=false");
}finally{Directory.Delete(root,true);}

sealed class LabAccess:IStationAppUpdateAccess {
    public StationSession User=new(new string('a',64),"lab-license",StationProtocol.Encode(RandomNumberGenerator.GetBytes(32)),null,null);
    public bool Expired;
    readonly object gate=new();readonly Dictionary<string,StationGrantRecord> grants=new();
    public Task<StationSession> SessionAsync(string bearer,CancellationToken t)=>bearer=="valid"?Task.FromResult(User):throw new SuiteException(401,"STATION_SESSION_INVALID","Denied");
    public Task InsertAsync(StationGrantRecord g,int seconds,CancellationToken t){if(seconds!=60)throw new Exception("TTL changed");lock(gate)grants.Add(g.GrantId,g);return Task.CompletedTask;}
    public Task<StationGrantRecord?> PeekAsync(string id,CancellationToken t){lock(gate)return Task.FromResult(!Expired&&grants.TryGetValue(id,out var g)?g:null);}
    public Task<StationGrantRecord?> ConsumeAsync(string id,string license,string device,CancellationToken t){lock(gate){if(!Expired&&grants.TryGetValue(id,out var g)&&g.LicenseId==license&&g.DeviceId==device){grants.Remove(id);return Task.FromResult<StationGrantRecord?>(g);}return Task.FromResult<StationGrantRecord?>(null);}}
}
