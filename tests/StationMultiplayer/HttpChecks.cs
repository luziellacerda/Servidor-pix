// Loopback TLS only. Keys, identities, licenses and content hashes are synthetic.
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

int checks=0;void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
string H(char c)=>new(c,64);
using var rsa=RSA.Create(2048);var cr=new CertificateRequest("CN=StationMultiplayerSyntheticLab",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);cr.CertificateExtensions.Add(names.Build());
using var generatedCertificate=cr.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(20));
// Re-import into the current user's transient key container: Windows Schannel
// cannot always acquire the ephemeral handle returned by CreateSelfSigned.
using var certificate=new X509Certificate2(generatedCertificate.Export(X509ContentType.Pkcs12),"",X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.Exportable);
using var signer=new StationResponseSigner(rsa.ExportPkcs8PrivateKeyPem());using var access=new LabAccess(signer);
var profile=new StationMultiplayerProfile("synthetic-item",H('a'),"snes","synthetic-engine",H('b'),H('c'),"synthetic-profile",H('d'),5,true,"snes-multitap-port2-v1","battle-single",[2,3,4,5]);
var budget=new StationReplayBudget();var multi=new StationMultiplayer([profile],_=>"snes",budget);
var legacy=new StationOnline([new(profile.EngineId,"snes",profile.CoreSha256,profile.RuntimeSha256,"station-stream.v2")],_=>"snes",relayEnabled:true,recoveryEnabled:true,
    legacyAdmission:multi.LegacyAllowed,externalMembership:multi.HasRoom);
var socialIds=new List<string>();
foreach(var user in access.Users){var e=JsonSerializer.SerializeToElement(legacy.Command(user.Identity,new("enter",Guid.NewGuid().ToString(),Nickname:user.Nickname)));socialIds.Add(e.GetProperty("selfId").GetString()!);}
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,0,l=>l.UseHttps(certificate)));
builder.Configuration["Station:Online:MultiplayerEnabled"]="true";
builder.Services.AddSingleton(legacy);builder.Services.AddSingleton(multi);builder.Services.AddSingleton<IStationOnlineAccess>(access);
builder.Services.AddSingleton(new StationRequestProof(TimeProvider.System));builder.Services.AddSingleton<StationMultiplayerRelay>();
await using var app=builder.Build();app.MapStationOnline(true);await app.StartAsync();
var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
bool Pin(X509Certificate? cert)=>cert is not null&&CryptographicOperations.FixedTimeEquals(SHA256.HashData(cert.GetRawCertData()),SHA256.HashData(certificate.RawData));
using var handler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,cert,_,_)=>Pin(cert)};
using var http=new HttpClient(handler){BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(10)};
JsonElement room=default;
StationMultiplayerCommand Cmd(string action,string? link=null,bool value=false,int capacity=0)=>new(action,Guid.NewGuid().ToString(),room.ValueKind==JsonValueKind.Object?room.GetProperty("roomId").GetString():null,
    profile.ItemId,profile.ContentSha256,profile.EngineId,profile.CoreSha256,profile.RuntimeSha256,profile.ProfileId,profile.ProfileSha256,capacity,
    room.ValueKind==JsonValueKind.Object?room.GetProperty("generation").GetInt64():0,link,value,ClientMaximumPlayers:5);
async Task<JsonElement> Post(int user,StationMultiplayerCommand command,HttpStatusCode expected=HttpStatusCode.OK,bool proof=true,string? proofPath=null)
{
    byte[] body=JsonSerializer.SerializeToUtf8Bytes(command,StrictJson.Options);using var request=new HttpRequestMessage(HttpMethod.Post,StationMultiplayer.CommandPath){Content=new ByteArrayContent(body)};
    request.Content.Headers.ContentType=new("application/json");request.Headers.Add("Authorization","Bearer "+access.Users[user].Bearer);
    if(proof)request.Headers.Add(StationRequestProof.Header,access.Users[user].Proof("POST",proofPath??StationMultiplayer.CommandPath,body,access.Users[user].Bearer));
    using var response=await http.SendAsync(request);Check(response.StatusCode==expected,"HTTP "+command.Action+" "+(int)response.StatusCode+" expected "+(int)expected);
    var result=JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsByteArrayAsync());
    if(expected!=HttpStatusCode.OK)return result;
    byte[] payload=StationProtocol.Decode(result.GetProperty("payload").GetString()!,524288),signature=StationProtocol.Decode(result.GetProperty("signature").GetString()!,512);
    Check(rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"signed envelope");
    var data=JsonSerializer.Deserialize<JsonElement>(payload);Check(data.GetProperty("requestId").GetString()==command.RequestId&&data.GetProperty("licenseId").GetString()==access.Users[user].Identity.LicenseId,"signed request/session identity");
    return data.GetProperty("snapshot");
}
async Task Block(int user,int other)
{
    var command=new OnlineCommand("block",Guid.NewGuid().ToString(),PeerId:socialIds[other]);
    byte[] body=JsonSerializer.SerializeToUtf8Bytes(command,StrictJson.Options);
    using var request=new HttpRequestMessage(HttpMethod.Post,"/v1/station/online/command"){Content=new ByteArrayContent(body)};
    request.Content.Headers.ContentType=new("application/json");request.Headers.Add("Authorization","Bearer "+access.Users[user].Bearer);
    request.Headers.Add(StationRequestProof.Header,access.Users[user].Proof("POST","/v1/station/online/command",body,access.Users[user].Bearer));
    using var response=await http.SendAsync(request);Check(response.StatusCode==HttpStatusCode.OK,"human social block accepted");
    var result=JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsByteArrayAsync());
    byte[] payload=StationProtocol.Decode(result.GetProperty("payload").GetString()!,524288),signature=StationProtocol.Decode(result.GetProperty("signature").GetString()!,512);
    Check(rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"signed social block envelope");
    Check(JsonSerializer.Deserialize<JsonElement>(payload).GetProperty("requestId").GetString()==command.RequestId,"social block request identity");
}
var sockets=new List<ClientWebSocket>();
try{
    await Post(0,Cmd("capabilities"),HttpStatusCode.Unauthorized,proof:false);
    await Post(0,Cmd("capabilities"),HttpStatusCode.Unauthorized,proofPath:"/v1/station/online/command");
    var cap=await Post(0,Cmd("capabilities"));Check(cap.GetProperty("selfId").GetString()==socialIds[0],"same authoritative social identity");
    room=(await Post(0,Cmd("create",capacity:5))).GetProperty("room");
    for(int i=1;i<5;i++)room=(await Post(i,Cmd("join"))).GetProperty("room");
    for(int i=0;i<5;i++)room=(await Post(i,Cmd("ready",value:true))).GetProperty("room");
    room=(await Post(0,Cmd("start"))).GetProperty("room");Check(room.GetProperty("state").GetString()=="starting","HTTP starting");
    var links=room.GetProperty("links").EnumerateArray().Select(x=>x.GetProperty("linkId").GetString()!).ToArray();
    await Post(1,Cmd("ticket",link:links[1]),HttpStatusCode.Forbidden);
    async Task<ClientWebSocket> Connect(int user,string link,string action="ticket"){
        var ticket=(await Post(user,Cmd(action,link:link))).GetProperty("ticket").GetProperty("ticket").GetString()!;
        var ws=new ClientWebSocket();ws.Options.AddSubProtocol(StationMultiplayer.Protocol);ws.Options.RemoteCertificateValidationCallback=(_,cert,_,_)=>Pin(cert);
        ws.Options.SetRequestHeader("Authorization","StationRelay "+ticket);ws.Options.SetRequestHeader(StationRequestProof.Header,access.Users[user].Proof("GET",StationMultiplayer.RelayPath,[],ticket));
        using var timeout=new CancellationTokenSource(5000);await ws.ConnectAsync(new Uri(address.Replace("https://","wss://",StringComparison.Ordinal)+StationMultiplayer.RelayPath),timeout.Token);
        Check(ws.SubProtocol==StationMultiplayer.Protocol,"negotiated TSR3");return ws;
    }
    async Task Send(ClientWebSocket ws,byte type,long offset=0,long value=0,byte[]? bytes=null){using var timeout=new CancellationTokenSource(5000);await ws.SendAsync(StationMultiplayerFrame.Encode(new(type,offset,value,bytes??[])).AsMemory(),WebSocketMessageType.Binary,true,timeout.Token);}
    async Task<StationStreamFrame> Until(ClientWebSocket ws,Func<StationStreamFrame,bool> predicate){byte[] buffer=new byte[16408];using var timeout=new CancellationTokenSource(5000);
        for(int k=0;k<100;k++){int count=0;ValueWebSocketReceiveResult result;do{result=await ws.ReceiveAsync(buffer.AsMemory(count),timeout.Token);if(result.MessageType!=WebSocketMessageType.Binary)throw new Exception("unexpected close");count+=result.Count;}while(!result.EndOfMessage);
            var frame=StationMultiplayerFrame.Decode(buffer.AsSpan(0,count));if(predicate(frame))return frame;}
        throw new Exception("frame limit");}
    for(int l=0;l<4;l++){sockets.Add(await Connect(0,links[l]));sockets.Add(await Connect(l+1,links[l]));}
    foreach(var ws in sockets){await Send(ws,StationStreamFrame.Hello);await Send(ws,StationStreamFrame.Paused,1);await Send(ws,StationStreamFrame.Ready,1);}
    foreach(var ws in sockets){await Until(ws,f=>f.Type==StationStreamFrame.State&&f.Offset==1&&f.Value==2);checks++;}
    await Send(sockets[1],StationStreamFrame.DataPacket,bytes:[11,22,33]);
    var received=await Until(sockets[0],f=>f.Type==StationStreamFrame.DataPacket);Check(received.Offset==0&&received.Data.SequenceEqual(new byte[]{11,22,33}),"TLS guest2 bytes only to its host link");
    await Send(sockets[0],StationStreamFrame.Ack,3);
    // Dropping one endpoint requires every surviving endpoint to observe the same new epoch.
    sockets[1].Abort();sockets[1].Dispose();
    long epoch=(await Until(sockets[0],f=>f.Type==StationStreamFrame.State&&f.Offset>1&&f.Value==0)).Offset;
    for(int i=2;i<sockets.Count;i++){var next=await Until(sockets[i],f=>f.Type==StationStreamFrame.State&&f.Value==0&&f.Offset>1);Check(next.Offset==epoch,"TLS global epoch");}
    room=(await Post(0,Cmd("heartbeat"))).GetProperty("room");Check(room.GetProperty("recoveryStarted").GetBoolean(),"sticky played status during reconnect");
    sockets[1]=await Connect(1,links[0],"resume");await Send(sockets[1],StationStreamFrame.Hello,3,0);
    for(int i=0;i<sockets.Count;i++){await Send(sockets[i],StationStreamFrame.Paused,epoch);await Send(sockets[i],StationStreamFrame.Ready,epoch,i==1?3:0);}
    foreach(var ws in sockets){await Until(ws,f=>f.Type==StationStreamFrame.State&&f.Offset==epoch&&f.Value==2);checks++;}
    var chat=await Post(2,Cmd("chat") with{Text="synthetic TLS message"});Check(chat.GetProperty("room").GetProperty("messages").GetArrayLength()==1,"TLS room chat");
    room=(await Post(1,Cmd("failed"))).GetProperty("room");Check(room.GetProperty("state").GetString()=="unrecoverable","member native failure terminal");
    await Post(0,Cmd("resume",link:links[0]),HttpStatusCode.Conflict);
    var left=await Post(0,Cmd("leave"));Check(left.GetProperty("room").ValueKind==JsonValueKind.Null,"human leave ends TLS room");
    await Task.Delay(1100); // Separate the new scenario from the production per-second command cap.
    room=(await Post(0,Cmd("create",capacity:5))).GetProperty("room");
    room=(await Post(1,Cmd("join"))).GetProperty("room");room=(await Post(2,Cmd("join"))).GetProperty("room");
    await Block(1,2);
    var blocked=await Post(1,Cmd("snapshot"));Check(blocked.GetProperty("room").ValueKind==JsonValueKind.Null&&blocked.GetProperty("totalRooms").GetInt32()==0,"blocking guest leaves waiting room and blocked rooms are hidden");
    room=(await Post(0,Cmd("heartbeat"))).GetProperty("room");Check(room.GetProperty("players").GetInt32()==2,"waiting block preserves uninvolved host and member");
    Check((await Post(1,Cmd("join"),HttpStatusCode.Forbidden)).GetProperty("code").GetString()=="STATION_ONLINE_BLOCKED","join cannot bypass blocked non-host member");
    var originalRoom=room;room=(await Post(1,Cmd("create",capacity:5))).GetProperty("room");
    Check((await Post(2,Cmd("snapshot"))).GetProperty("rooms").EnumerateArray().All(r=>r.GetProperty("hostId").GetString()!=socialIds[1]),"incoming host block hides room bidirectionally");
    await Post(2,Cmd("join"),HttpStatusCode.Conflict); // Existing own membership still wins over another room.
    room=originalRoom;room=(await Post(3,Cmd("join"))).GetProperty("room");
    foreach(int user in new[]{0,2,3})room=(await Post(user,Cmd("ready",value:true))).GetProperty("room");
    room=(await Post(0,Cmd("start"))).GetProperty("room");await Block(2,3);
    Check(!multi.HasRoom(access.Users[0].Identity)&&!multi.HasRoom(access.Users[2].Identity)&&!multi.HasRoom(access.Users[3].Identity),"human block ends frozen shared v3 room");
    Check(multi.HasRoom(access.Users[1].Identity),"block leaves unrelated v3 room intact");
    room=(await Post(1,Cmd("heartbeat"))).GetProperty("room");
    Check((await Post(2,Cmd("join"),HttpStatusCode.Forbidden)).GetProperty("code").GetString()=="STATION_ONLINE_BLOCKED","incoming host block denies guessed room ID");
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="synthetic loopback TLS: five authenticated players, eight WS links, signed HTTP, bound proofs, stream, disconnect/resume and chat; not Android gameplay or public network"}));
}finally{foreach(var ws in sockets)ws.Dispose();await app.StopAsync();}

sealed class LabAccess(StationResponseSigner signer):IStationOnlineAccess,IDisposable
{
    public sealed class User(int i):IDisposable
    {
        public readonly RSA Key=RSA.Create(2048);public readonly string Bearer=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        public readonly OnlineIdentity Identity=new("synthetic-license-"+i,"synthetic-device-"+i);public string Nickname=>"Synthetic "+i;
        public string Proof(string method,string path,byte[] body,string credential){long stamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();string nonce=StationProtocol.Encode(RandomNumberGenerator.GetBytes(16));
            var bytes=StationRequestProof.Canonical(method,path,body,credential,stamp,nonce);return $"v1.{stamp}.{nonce}."+StationProtocol.Encode(Key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));}
        public void Dispose()=>Key.Dispose();
    }
    public User[] Users {get;}=Enumerable.Range(0,5).Select(i=>new User(i)).ToArray();
    public Task<StationSession?> Authenticate(string bearer,CancellationToken token){var u=Users.FirstOrDefault(u=>u.Bearer==bearer);return Task.FromResult(u is null?null:new StationSession("synthetic-session",u.Identity.LicenseId,u.Identity.DeviceId,u.Nickname,1,"rsa-pss-v1",StationProtocol.Encode(u.Key.ExportSubjectPublicKeyInfo())));}
    public Task<bool> AuthorizeRelay(StationOnline.RelayLease lease,CancellationToken token)=>Task.FromResult(Users.Any(u=>u.Identity==lease.Identity));
    public object Sign(object payload)=>signer.Sign(payload);public void Dispose(){foreach(var user in Users)user.Dispose();}
}
