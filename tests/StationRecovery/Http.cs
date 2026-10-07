// Isolated TLS harness. All credentials/identities/certificates are synthetic and temporary.
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

if(args.Length!=1||!Path.IsPathFullyQualified(args[0])||File.Exists(args[0]))throw new ArgumentException("New absolute fixture path required");
using var rsa=RSA.Create(2048);var certificateRequest=new CertificateRequest("CN=StationRecoveryLab",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);certificateRequest.CertificateExtensions.Add(names.Build());
certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
using var certificate=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(30));
using var signer=new StationResponseSigner(rsa.ExportPkcs8PrivateKeyPem());
var access=new Access(signer);string hash=new('a',64),engine="synthetic-recovery";
var hub=new StationOnline([new(engine,"snes",hash,hash,"station-stream.v2")],_=>"snes",relayEnabled:true,recoveryEnabled:true);
JsonElement Command(Access.User user,OnlineCommand command)=>JsonSerializer.SerializeToElement(hub.Command(user.Identity,command,new("rsa-pss-v1",StationProtocol.Encode(user.Key.ExportSubjectPublicKeyInfo()))));
string id()=>Guid.NewGuid().ToString();
foreach(var user in access.Users)Command(user,new("enter",id(),Nickname:user.Role));
var room=Command(access.Users[0],new("create",id(),ItemId:"synthetic-item",EngineId:engine,ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2")).GetProperty("room");
string rid=room.GetProperty("roomId").GetString()!;
Command(access.Users[1],new("join",id(),RoomId:rid,ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2"));
foreach(var user in access.Users)Command(user,new("ready",id(),RoomId:rid,Value:true));
room=Command(access.Users[0],new("start",id(),RoomId:rid,Transport:"relay-wss-v2")).GetProperty("room");
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options=>options.Listen(IPAddress.Loopback,0,listen=>listen.UseHttps(certificate)));
builder.Configuration["Station:Online:RelayEnabled"]="true";builder.Configuration["Station:Online:RecoveryEnabled"]="true";
builder.Services.AddSingleton(hub);builder.Services.AddSingleton<IStationOnlineAccess>(access);builder.Services.AddSingleton(new StationRequestProof(TimeProvider.System));
builder.Services.AddSingleton(sp=>new StationRelay(hub,4,sp.GetRequiredService<ILogger<StationRelay>>()));
builder.Services.AddSingleton(sp=>new StationRecoveryRelay(hub,access,sp.GetRequiredService<ILogger<StationRecoveryRelay>>(),4));
builder.Services.AddHostedService(sp=>sp.GetRequiredService<StationRecoveryRelay>());
await using var app=builder.Build();string labSecret=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
app.UseWebSockets();
app.Use(async(context,next)=>{
    if(context.Request.Method=="POST"&&context.Request.Path.StartsWithSegments("/v1/station/online")){
        var header=context.Request.Headers.Authorization.ToString();var user=access.Users.FirstOrDefault(u=>header=="Bearer "+u.Token);
        if(user is null){context.Response.StatusCode=401;return;}
        context.Request.EnableBuffering();using var bytes=new MemoryStream();await context.Request.Body.CopyToAsync(bytes);context.Request.Body.Position=0;
        try{context.RequestServices.GetRequiredService<StationRequestProof>().Verify(context.Request.Headers[StationRequestProof.Header].ToString(),"rsa-pss-v1",StationProtocol.Encode(user.Key.ExportSubjectPublicKeyInfo()),user.Token,"POST",context.Request.Path.Value!,bytes.ToArray());}
        catch(SuiteException error){context.Response.StatusCode=error.StatusCode;await context.Response.WriteAsJsonAsync(new{code=error.Code});return;}
        context.Items[typeof(StationSession)]=(user.Token,user.Session);
    }
    if(context.WebSockets.IsWebSocketRequest&&context.Features.Get<IHttpWebSocketFeature>() is {} original)
        context.Features.Set<IHttpWebSocketFeature>(new TrackingSockets(original,access,context.Request.Headers.Authorization.ToString()));
    await next(context);
});
app.MapStationOnline(true);
app.MapPost("/lab/drop",async(HttpContext context)=>{
    if(context.Request.Headers["X-Station-Lab"]!=labSecret)return Results.NotFound();
    using var reader=new StreamReader(context.Request.Body);string role=await reader.ReadToEndAsync();
    if(role is not("host" or "client" or "both"))return Results.BadRequest();
    foreach(var user in access.Users.Where(user=>role=="both"||user.Role==role))lock(access.Gate){if(access.Sockets.TryGetValue(user.Role,out var socket))socket.Abort();}
    return Results.Ok();
});
app.MapGet("/lab/state",async(HttpContext context)=>context.Request.Headers["X-Station-Lab"]==labSecret?
    Results.Json(new{recovery=app.Services.GetRequiredService<StationRecoveryRelay>().Snapshot(),snapshot=await hub.Events(access.Users[0].Identity,null,0,0,context.RequestAborted)}):Results.NotFound());
await app.StartAsync();string url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var fixture=new List<string>{"url="+url,"lab="+labSecret,"room="+rid,"generation="+room.GetProperty("generation").GetInt64(),"hash="+hash,"engine="+engine,
    "cert="+Convert.ToBase64String(certificate.Export(X509ContentType.Cert)),"responseSPKI="+StationProtocol.Encode(rsa.ExportSubjectPublicKeyInfo())};
foreach(var user in access.Users){fixture.Add(user.Role+".token="+user.Token);fixture.Add(user.Role+".key="+Convert.ToBase64String(user.Key.ExportPkcs8PrivateKey()));fixture.Add(user.Role+".license="+user.Identity.LicenseId);}
Directory.CreateDirectory(Path.GetDirectoryName(args[0])!);
await File.WriteAllLinesAsync(args[0],fixture);
if(!OperatingSystem.IsWindows())File.SetUnixFileMode(args[0],UnixFileMode.UserRead|UnixFileMode.UserWrite);
Console.WriteLine("TLS recovery harness ready on loopback; synthetic fixture written privately");
await app.WaitForShutdownAsync();File.Delete(args[0]);

sealed class Access(StationResponseSigner signer):IStationOnlineAccess
{
    public sealed class User(string role,char label){
        public string Role=role;public RSA Key=RSA.Create(2048);public string Token=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        public OnlineIdentity Identity=new(new string(label,64),new string(label,64));
        public StationSession Session=>new(new string('c',64),Identity.LicenseId,Identity.DeviceId,null,1,"rsa-pss-v1",StationProtocol.Encode(Key.ExportSubjectPublicKeyInfo()));
    }
    public readonly User[] Users=[new("host",'a'),new("client",'b')];
    public readonly object Gate=new();public readonly Dictionary<string,string> Roles=[];public readonly Dictionary<string,WebSocket> Sockets=[];
    public Task<StationSession?> Authenticate(string bearer,CancellationToken token)=>Task.FromResult(Users.FirstOrDefault(user=>user.Token==bearer)?.Session);
    public Task<bool> AuthorizeRelay(StationOnline.RelayLease lease,CancellationToken token)=>Task.FromResult(Users.Any(user=>user.Identity==lease.Identity));
    public object Sign(object payload){
        var json=JsonSerializer.SerializeToElement(payload);var snapshot=json.GetProperty("snapshot");
        if(snapshot.TryGetProperty("room",out var room)&&room.ValueKind==JsonValueKind.Object&&room.TryGetProperty("relay",out var relay)&&relay.ValueKind==JsonValueKind.Object){
            var role=Users.Single(user=>user.Identity.LicenseId==json.GetProperty("licenseId").GetString()).Role;
            lock(Gate){foreach(var old in Roles.Where(pair=>pair.Value==role).Select(pair=>pair.Key).ToArray())Roles.Remove(old);Roles[relay.GetProperty("ticket").GetString()!]=role;}
        }
        return signer.Sign(payload);
    }
}
sealed class TrackingSockets(IHttpWebSocketFeature original,Access access,string authorization):IHttpWebSocketFeature
{
    public bool IsWebSocketRequest=>original.IsWebSocketRequest;
    public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context){var socket=await original.AcceptAsync(context);lock(access.Gate){if(authorization.StartsWith("StationRelay ")&&access.Roles.TryGetValue(authorization[13..],out var role))access.Sockets[role]=socket;}return socket;}
}
