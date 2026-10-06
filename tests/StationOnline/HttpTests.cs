using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

int checks=0; string hash=new('a',64); using var rsa=RSA.Create(2048);
using var signer=new StationResponseSigner(rsa.ExportPkcs8PrivateKeyPem());
var access=new TestAccess(signer);
var hub=new StationOnline([new OnlineEngine("synthetic-engine","snes",hash,hash)],x=>x=="synthetic-item"?"snes":null);
var builder=WebApplication.CreateBuilder();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();builder.Logging.AddConsole();
builder.Services.AddSingleton(hub);builder.Services.AddSingleton<IStationOnlineAccess>(access);
await using var app=builder.Build();app.MapStationOnline(true);await app.StartAsync();
var url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var http=new HttpClient { BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20) };
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
async Task<(int Status,JsonElement Body)> Post(string route,object body,string? token)
{
 using var request=new HttpRequestMessage(HttpMethod.Post,"/v1/station/online/"+route){Content=JsonContent.Create(body)};
 if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
 using var response=await http.SendAsync(request);var raw=await response.Content.ReadAsStringAsync();
 if(raw.Length==0)throw new Exception("Empty response: "+response.StatusCode+" "+route);
 var json=JsonSerializer.Deserialize<JsonElement>(raw);
 Check(response.Headers.CacheControl?.NoStore==true,"no-store response");
 return ((int)response.StatusCode,json);
}
JsonElement Payload(JsonElement envelope,string requestId,string license)
{
 var bytes=StationProtocol.Decode(envelope.GetProperty("payload").GetString()!,262144);
 var sig=StationProtocol.Decode(envelope.GetProperty("signature").GetString()!,512);
 Check(rsa.VerifyData(bytes,sig,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"real RSA-PSS signature");
 var payload=JsonSerializer.Deserialize<JsonElement>(bytes);
 Check(payload.GetProperty("domain").GetString()=="TurboRamaStationAndroid/online/v1","domain separated");
 Check(payload.GetProperty("requestId").GetString()==requestId,"request binding");
 Check(payload.GetProperty("licenseId").GetString()==license,"session owner binding");
 return payload.GetProperty("snapshot");
}
string Id()=>Guid.NewGuid().ToString();
Check((await Post("command",new {action="enter",requestId=Id(),nickname="Test A"},null)).Status==401,"missing auth rejected");
Check((await Post("command",new {action="enter",requestId=Id(),nickname="Test A"},"invalid")).Status==401,"malformed auth rejected");
foreach(var route in new[]{"command","events"})
{
 string body=route=="command"
  ? "{\"action\":\"enter\",\"action\":\"offline\",\"requestId\":\""+Id()+"\",\"nickname\":\"Test A\"}"
  : "{\"requestId\":\""+Id()+"\",\"revision\":0,\"page\":0,\"page\":40}";
 using var duplicate=new HttpRequestMessage(HttpMethod.Post,"/v1/station/online/"+route)
  { Content=new StringContent(body,Encoding.UTF8,"application/json") };
 duplicate.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access.A);
 using var rejected=await http.SendAsync(duplicate);
 Check(rejected.StatusCode==HttpStatusCode.BadRequest,"duplicate JSON member rejected for "+route);
 Check(rejected.Headers.CacheControl?.NoStore==true,"duplicate member error is private");
}
var command=new {action="enter",requestId=Id(),nickname="Test A"};
var first=await Post("command",command,access.A);Check(first.Status==200,"authenticated enter");
var snapshot=Payload(first.Body,command.requestId,"license-A");
Check(snapshot.GetProperty("totalPeers").GetInt32()==1,"only real entered identities");
string createId=Id();
var created=await Post("command",new {action="create",requestId=createId,itemId="synthetic-item",engineId="synthetic-engine",contentSha256=hash,optionsSha256=hash,coreSha256=hash,runtimeSha256=hash},access.A);
var room=Payload(created.Body,createId,"license-A").GetProperty("room");
string secret=room.GetProperty("connectionPassword").GetString()!;
var enterB=new {action="enter",requestId=Id(),nickname="Test B"};
var b=Payload((await Post("command",enterB,access.B)).Body,enterB.requestId,"license-B");
Check(!b.GetRawText().Contains(secret)&&!b.GetRawText().Contains("directEndpoint"),"outsider gets no member secrets");
string code=room.GetProperty("inviteCode").GetString()!;
Check((await Post("command",new {action="resolve-code",requestId=Id(),inviteCode=code},null)).Status==401,"code lookup requires authentication");
string resolveId=Id();
var resolvedResponse=await Post("command",new {action="resolve-code",requestId=resolveId,inviteCode=code},access.B);
Check(resolvedResponse.Status==200,"authenticated short code lookup");
var resolved=Payload(resolvedResponse.Body,resolveId,"license-B");
Check(resolved.GetProperty("resolvedRoom").GetProperty("roomId").GetString()==room.GetProperty("roomId").GetString(),"signed lookup finds correct room");
Check(!resolved.GetRawText().Contains(secret)&&resolved.GetProperty("room").ValueKind==JsonValueKind.Null,"signed lookup has no room credentials or implicit membership");
Check((await Post("command",new {action="resolve-code",requestId=Id(),inviteCode="00000000"},access.B)).Status==400,"invalid code format rejected through route");
Check((await Post("command",new {action="chat",requestId=Id(),roomId=room.GetProperty("roomId").GetString(),text="no access"},access.B)).Status==404,"outsider chat rejected");
Check((await Post("command",new {action="enter",requestId=Id(),nickname=new string('x',8500)},access.A)).Status==413,"body size limit");
string heartbeatId=Id();var current=Payload((await Post("command",new {action="heartbeat",requestId=heartbeatId},access.A)).Body,heartbeatId,"license-A");
var waitBody=new {requestId=Id(),instance=current.GetProperty("instance").GetString(),revision=current.GetProperty("revision").GetInt64(),page=0};
var waiting=Post("events",waitBody,access.A);await Task.Delay(100);
Check((await Post("events",new {requestId=Id(),instance=waitBody.instance,revision=waitBody.revision,page=0},access.A)).Status==429,"one pending event request per device");
access.Revoked=true;
hub.Command(new OnlineIdentity("license-B","device-B"),new OnlineCommand("create",Id(),ItemId:"synthetic-item",EngineId:"synthetic-engine",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash));
var denied=await waiting;Check(denied.Status==401,"revoked while waiting rejected before secret response");
Check(!denied.Body.GetRawText().Contains(secret),"no secret after revocation");
await app.StopAsync();
var disabledBuilder=WebApplication.CreateBuilder();disabledBuilder.WebHost.UseUrls("http://127.0.0.1:0");disabledBuilder.Logging.ClearProviders();
await using var disabled=disabledBuilder.Build();disabled.MapStationOnline(false);await disabled.StartAsync();
using var dc=new HttpClient {BaseAddress=new Uri(disabled.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())};
using var dr=await dc.PostAsJsonAsync("/v1/station/online/command",new {action="enter",requestId=Id(),nickname="Test"});
Check(dr.StatusCode==HttpStatusCode.ServiceUnavailable,"disabled requires no database or signing services");await disabled.StopAsync();
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,checks,scope="Loopback Kestrel with production routes and signer, synthetic authentication; no production database or Android"}));

sealed class TestAccess(StationResponseSigner signer):IStationOnlineAccess
{
 public string A {get;}=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
 public string B {get;}=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
 public volatile bool Revoked;
 public Task<StationSession?> Authenticate(string token,CancellationToken cancellation)
 {cancellation.ThrowIfCancellationRequested();return Task.FromResult<StationSession?>(token==A&&!Revoked?new(new string('a',64),"license-A","device-A","Test A",1):token==B?new(new string('b',64),"license-B","device-B","Test B",1):null);}
 public object Sign(object payload)=>signer.Sign(payload);
}
