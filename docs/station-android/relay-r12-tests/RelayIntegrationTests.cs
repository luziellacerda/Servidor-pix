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

int checks=0;string hash=new('a',64);long now=1000;
OnlineCommand C(string action)=>new(action,Guid.NewGuid().ToString());
JsonElement J(object v)=>JsonSerializer.SerializeToElement(v);
void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
void Fail(string code,Action action){try{action();throw new Exception("accepted "+code);}catch(OnlineFailure e){Check(e.Code==code,code);}}
var hub=new StationOnline([new OnlineEngine("fixture","snes",hash,hash)],_=>"snes",()=>now,true);
var a=new OnlineIdentity("A","A");var b=new OnlineIdentity("B","B");var c=new OnlineIdentity("C","C");
foreach(var p in new[]{a,b,c})hub.Command(p,C("enter") with{Nickname=p.LicenseId});
var create=C("create") with{ItemId="fixture",EngineId="fixture",ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash};
string rid=J(hub.Command(a,create)).GetProperty("room").GetProperty("roomId").GetString()!;
hub.Command(b,C("join") with{RoomId=rid,ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash});
hub.Command(a,C("ready") with{RoomId=rid,Value=true});hub.Command(b,C("ready") with{RoomId=rid,Value=true});
var started=J(hub.Command(a,C("start") with{RoomId=rid,Transport="relay-wss-v1"}));
Check(started.GetProperty("room").GetProperty("directEndpoint").ValueKind==JsonValueKind.Null,"no public IP needed");
long generation=started.GetProperty("room").GetProperty("generation").GetInt64();
Fail("STATION_ONLINE_ROOM_NOT_FOUND",()=>hub.Command(c,C("relay-ticket") with{RoomId=rid}));
string Ticket(OnlineIdentity p)=>J(hub.Command(p,C("relay-ticket") with{RoomId=rid})).GetProperty("room").GetProperty("relay").GetProperty("ticket").GetString()!;
var expired=Ticket(a);now+=60000;
// Keep membership alive while expiring the grant, without a real-minute wait.
// Refresh before the exact membership expiration on a separate clock run below.
// The room expires along with the grant; verify rejection, then rebuild fixture.
Fail("STATION_ONLINE_RELAY_TICKET_INVALID",()=>hub.TakeRelayTicket(expired));
foreach(var p in new[]{a,b,c})hub.Command(p,C("enter") with{Nickname=p.LicenseId});
rid=J(hub.Command(a,create with{RequestId=Guid.NewGuid().ToString()})).GetProperty("room").GetProperty("roomId").GetString()!;
hub.Command(b,C("join") with{RoomId=rid,ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash});
hub.Command(a,C("ready") with{RoomId=rid,Value=true});hub.Command(b,C("ready") with{RoomId=rid,Value=true});
var state=J(hub.Command(a,C("start") with{RoomId=rid,Transport="relay-wss-v1"}));generation=state.GetProperty("room").GetProperty("generation").GetInt64();
var host=Ticket(a);var guest=Ticket(b);
Check(!J(hub.Command(c,C("heartbeat"))).GetRawText().Contains(host),"ticket not visible to outsider");
Check(!J(hub.Command(b,C("heartbeat"))).GetRawText().Contains(host),"host ticket not visible to guest");
Check(J(hub.Command(a,C("host-listening") with{RoomId=rid})).GetProperty("room").GetProperty("generation").GetInt64()==generation,"host readiness retains ticket generation");
var relay=new StationRelay(hub);
using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,false));
using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
using var cert=new X509Certificate2(ephemeral.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.Configuration["Station:Online:RelayEnabled"]="true";
builder.WebHost.ConfigureKestrel(k=>k.Listen(IPAddress.Loopback,0,l=>l.UseHttps(cert)));
builder.Services.AddSingleton(hub);builder.Services.AddSingleton(relay);
await using var app=builder.Build();app.MapStationOnline(true);await app.StartAsync();
string origin=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().Replace("127.0.0.1","localhost");
var uri=new Uri(origin.Replace("https:","wss:")+"/v1/station/online/relay");
async Task Rejected(string token){using var ws=new ClientWebSocket();ws.Options.AddSubProtocol("station-relay.v1");ws.Options.SetRequestHeader("Authorization","StationRelay "+token);ws.Options.RemoteCertificateValidationCallback=(_,c,_,_)=>c?.GetCertHashString()==cert.GetCertHashString();try{await ws.ConnectAsync(uri,CancellationToken.None);throw new Exception("invalid upgrade accepted");}catch(WebSocketException){checks++;}}
await Rejected(new string('z',43));
using(var httpHandler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,c,_,_)=>c?.Thumbprint==cert.Thumbprint})
using(var http=new HttpClient(httpHandler)){
 using var response=await http.GetAsync(origin+"/v1/station/online/relay");Check((int)response.StatusCode==400,"ordinary HTTP cannot open relay");
}
app.Configuration["Station:Online:RelayEnabled"]="false";await Rejected(host);app.Configuration["Station:Online:RelayEnabled"]="true";

string folder=Path.GetFullPath(args[0]);File.WriteAllBytes(Path.Combine(folder,"fixture.cer"),cert.Export(X509ContentType.Cert));
File.WriteAllText(Path.Combine(folder,"fixture.properties"),$"uri={uri}\nhost={host}\nclient={guest}\ncert={Path.Combine(folder,"fixture.cer").Replace('\\','/')}\n");
var psi=new ProcessStartInfo(args[1]){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
psi.ArgumentList.Add("-cp");psi.ArgumentList.Add(args[2]);psi.ArgumentList.Add("org.emulationstation.frontend.netplay.RelayBridgeTests");psi.ArgumentList.Add(Path.Combine(folder,"fixture.properties"));
using var process=Process.Start(psi)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(55));string output=await stdout,error=await stderr;Console.WriteLine(output);if(process.ExitCode!=0)throw new Exception(error);Check(process.ExitCode==0,"Java TLS bridge integration");
await Task.Delay(500);Check(relay.ActiveRooms==0,"no retained relay after disconnect");
await Rejected(host);Check(J(hub.Command(b,C("heartbeat"))).GetProperty("room").ValueKind==JsonValueKind.Null,"disconnect closes room");
await app.StopAsync();File.Delete(Path.Combine(folder,"fixture.properties"));
// A consumed ticket is single-use, and revocation invalidates the resulting lease.
now+=11000;
rid=J(hub.Command(a,create with{RequestId=Guid.NewGuid().ToString()})).GetProperty("room").GetProperty("roomId").GetString()!;
hub.Command(b,C("join") with{RoomId=rid,ContentSha256=hash,OptionsSha256=hash,CoreSha256=hash,RuntimeSha256=hash});
hub.Command(a,C("ready") with{RoomId=rid,Value=true});hub.Command(b,C("ready") with{RoomId=rid,Value=true});
hub.Command(a,C("start") with{RoomId=rid,Transport="relay-wss-v1"});
string once=Ticket(a);var lease=hub.TakeRelayTicket(once);Check(hub.RelayCurrent(lease),"lease bound to current room");
Fail("STATION_ONLINE_RELAY_TICKET_INVALID",()=>hub.TakeRelayTicket(once));
Fail("STATION_ONLINE_RELAY_ALREADY_ATTACHED",()=>Ticket(a));
hub.Revoke(a);Check(!hub.RelayCurrent(lease),"revocation invalidates lease");

Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="Isolated TLS Kestrel with real Station routes and Java client; no production or Android gameplay"}));
