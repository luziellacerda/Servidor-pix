using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

const int pairCount=256, frames=1200;
string hash=new('a',64);
var hub=new StationOnline([new OnlineEngine("load-synthetic","snes",hash,hash)],_=>"snes",relayEnabled:true);
var relay=new StationRelay(hub,512);
foreach(int invalid in new[]{0,2049})
{
    try { _=new StationRelay(hub,invalid);throw new Exception("Invalid capacity accepted"); }
    catch(ArgumentOutOfRangeException) { }
}
using var key=RSA.Create(2048);
var certificateRequest=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");certificateRequest.CertificateExtensions.Add(san.Build());
using var ephemeral=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1));
using var cert=new X509Certificate2(ephemeral.Export(X509ContentType.Pfx));
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.Logging.AddConsole();builder.Logging.SetMinimumLevel(LogLevel.Error);
builder.Configuration["Station:Online:RelayEnabled"]="true";
builder.WebHost.ConfigureKestrel(options=>options.Listen(IPAddress.Loopback,0,listen=>listen.UseHttps(cert)));
builder.Services.AddSingleton(hub);builder.Services.AddSingleton(relay);
await using var app=builder.Build();app.MapStationOnline(true);await app.StartAsync();
var uri=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()
    .Replace("127.0.0.1","localhost").Replace("https:","wss:")+"/v1/station/online/relay");
OnlineCommand C(string action)=>new(action,Guid.NewGuid().ToString());
JsonElement J(object value)=>JsonSerializer.SerializeToElement(value);
string Ticket(OnlineIdentity peer,string room)=>J(hub.Command(peer,C("relay-ticket") with{RoomId=room}))
    .GetProperty("room").GetProperty("relay").GetProperty("ticket").GetString()!;
var identities=new List<OnlineIdentity>();var pairs=new List<(ClientWebSocket Host,ClientWebSocket Guest)>();
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
async Task<ClientWebSocket> Open(string ticket)
{
    var socket=new ClientWebSocket();socket.Options.AddSubProtocol("station-relay.v1");
    socket.Options.SetRequestHeader("Authorization","StationRelay "+ticket);
    // Exact ephemeral certificate, exclusively for this loopback fixture.
    socket.Options.RemoteCertificateValidationCallback=(_,candidate,_,_)=>candidate?.GetCertHashString()==cert.GetCertHashString();
    await socket.ConnectAsync(uri,timeout.Token);return socket;
}
async Task Receive(ClientWebSocket socket,byte[] expected)
{
    byte[] actual=new byte[expected.Length];int received=0;WebSocketReceiveResult read;
    do {
        read=await socket.ReceiveAsync(new ArraySegment<byte>(actual,received,actual.Length-received),timeout.Token);
        if(read.MessageType!=WebSocketMessageType.Binary)throw new Exception("Nonbinary load response");
        received+=read.Count;
    }while(!read.EndOfMessage);
    if(received!=expected.Length||!actual.AsSpan().SequenceEqual(expected))throw new Exception("Dropped, reordered or corrupt load message");
}
var setup=Stopwatch.StartNew();
var process=Process.GetCurrentProcess();var cpuBefore=process.TotalProcessorTime;long memoryBefore=process.WorkingSet64;
for(int index=0;index<pairCount;index++)
{
    var host=new OnlineIdentity("load-host-"+index,"load-host-"+index);
    var guest=new OnlineIdentity("load-guest-"+index,"load-guest-"+index);identities.Add(host);identities.Add(guest);
    hub.Command(host,C("enter") with{Nickname="Load host "+index});hub.Command(guest,C("enter") with{Nickname="Load guest "+index});
    string room=J(hub.Command(host,C("create") with{ItemId="load-item",EngineId="load-synthetic",
        ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash})).GetProperty("room").GetProperty("roomId").GetString()!;
    hub.Command(guest,C("join") with{RoomId=room,ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash});
    hub.Command(host,C("ready") with{RoomId=room,Value=true});hub.Command(guest,C("ready") with{RoomId=room,Value=true});
    hub.Command(host,C("start") with{RoomId=room,Transport="relay-wss-v1"});
    var first=await Open(Ticket(host,room));hub.Command(host,C("host-listening") with{RoomId=room});
    var second=await Open(Ticket(guest,room));pairs.Add((first,second));
}
if(relay.ActiveRooms!=pairCount)throw new Exception("Missing active load pair");
var peakSnapshot=relay.Snapshot();process.Refresh();long connectedMemory=process.WorkingSet64;
setup.Stop();
var latencies=new ConcurrentBag<double>();var load=Stopwatch.StartNew();
await Task.WhenAll(pairs.Select(async (pair,pairIndex)=>{
    byte[] state=new byte[65536];RandomNumberGenerator.Fill(state);
    await pair.Host.SendAsync(state,WebSocketMessageType.Binary,true,timeout.Token);await Receive(pair.Guest,state);
    byte[] payload=new byte[32];var timer=Stopwatch.StartNew();
    for(int frame=0;frame<frames;frame++)
    {
        BitConverter.TryWriteBytes(payload.AsSpan(),pairIndex);BitConverter.TryWriteBytes(payload.AsSpan(4),frame);
        long sent=Stopwatch.GetTimestamp();
        await pair.Host.SendAsync(payload,WebSocketMessageType.Binary,true,timeout.Token);await Receive(pair.Guest,payload);
        await pair.Guest.SendAsync(payload,WebSocketMessageType.Binary,true,timeout.Token);await Receive(pair.Host,payload);
        latencies.Add(Stopwatch.GetElapsedTime(sent).TotalMilliseconds);
        double delay=(frame+1)*1000.0/60-timer.Elapsed.TotalMilliseconds;
        if(delay>0)await Task.Delay(TimeSpan.FromMilliseconds(delay),timeout.Token);
    }
}));
load.Stop();process.Refresh();long peakMemory=process.PeakWorkingSet64;double cpuSeconds=(process.TotalProcessorTime-cpuBefore).TotalSeconds;
double[] sorted=latencies.Order().ToArray();double Percentile(double percentile)=>sorted[(int)Math.Min(sorted.Length-1,Math.Ceiling(sorted.Length*percentile)-1)];
foreach(var peer in identities)hub.Command(peer,C("offline"));
foreach(var pair in pairs){pair.Host.Abort();pair.Guest.Abort();pair.Host.Dispose();pair.Guest.Dispose();}
for(int attempt=0;attempt<150&&relay.ActiveRooms>0;attempt++)await Task.Delay(100);
if(relay.ActiveRooms!=0)throw new Exception("Relay slots retained after load: "+JsonSerializer.Serialize(relay.Snapshot()));
await app.StopAsync();
Console.WriteLine(JsonSerializer.Serialize(new {passed=true,scope="Isolated real TLS Kestrel and production relay classes; synthetic identities, no production DB or public proxy",
    players=pairCount*2,rooms=pairCount,maximumRooms=512,framesPerSecondTarget=60,framesPerPair=frames,
    orderedRoundTrips=sorted.Length,setupSeconds=setup.Elapsed.TotalSeconds,
    loadSeconds=load.Elapsed.TotalSeconds,roundTripP50Ms=Percentile(.50),roundTripP95Ms=Percentile(.95),roundTripP99Ms=Percentile(.99),
    initialStateBytes=pairCount*65536L,inputBytes=pairCount*frames*32L*2,peakSnapshot,
    processMemoryBefore=memoryBefore,processMemoryConnected=connectedMemory,processPeakMemory=peakMemory,cpuSeconds,
    droppedOrCorruptMessages=0,roomsAfterCleanup=relay.ActiveRooms,androidGameplay=false}));
