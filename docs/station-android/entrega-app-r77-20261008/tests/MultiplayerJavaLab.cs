// Synthetic identities and keys, loopback TLS only. No production credentials or data.
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

string H(char c)=>new(c,64);
using var rsa=RSA.Create(2048);var cr=new CertificateRequest("CN=StationMultiplayerJavaLab",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var names=new SubjectAlternativeNameBuilder();names.AddIpAddress(IPAddress.Loopback);cr.CertificateExtensions.Add(names.Build());
using var generated=cr.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddMinutes(30));
using var cert=new X509Certificate2(generated.Export(X509ContentType.Pkcs12),"",X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.Exportable);
using var signer=new StationResponseSigner(rsa.ExportPkcs8PrivateKeyPem());using var access=new JavaLabAccess(signer);
var profile=new StationMultiplayerProfile("synthetic-item",H('a'),"snes","synthetic-engine",H('b'),H('c'),"synthetic-profile",H('d'),4,true,"snes-multitap-port2-v1","battle",[2,3,4]);
var multi=new StationMultiplayer([profile],_=>"snes",new StationReplayBudget());
var legacy=new StationOnline([new(profile.EngineId,"snes",profile.CoreSha256,profile.RuntimeSha256,"station-stream.v2")],_=>"snes",relayEnabled:true,recoveryEnabled:true,legacyAdmission:multi.LegacyAllowed,externalMembership:multi.HasRoom);
foreach(var user in access.Users)legacy.Command(user.Identity,new("enter",Guid.NewGuid().ToString(),Nickname:user.Nickname));
var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,0,l=>l.UseHttps(cert)));
builder.Configuration["Station:Online:MultiplayerEnabled"]="true";
builder.Services.AddSingleton(legacy);builder.Services.AddSingleton(multi);builder.Services.AddSingleton<IStationOnlineAccess>(access);
builder.Services.AddSingleton(new StationRequestProof(TimeProvider.System));builder.Services.AddSingleton<StationMultiplayerRelay>();
await using var app=builder.Build();app.MapStationOnline(true);await app.StartAsync();
var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var lines=new List<string>{"url="+address,"cert="+Convert.ToBase64String(cert.RawData),"responseSPKI="+StationProtocol.Encode(rsa.ExportSubjectPublicKeyInfo())};
for(int i=0;i<access.Users.Length;i++){var u=access.Users[i];lines.Add($"user{i}.key="+Convert.ToBase64String(u.Key.ExportPkcs8PrivateKey()));lines.Add($"user{i}.token="+u.Bearer);lines.Add($"user{i}.license="+u.Identity.LicenseId);lines.Add($"user{i}.device="+u.Identity.DeviceId);}
await File.WriteAllLinesAsync(args[0],lines);
try{await Console.In.ReadLineAsync();}finally{await app.StopAsync();File.Delete(args[0]);}

sealed class JavaLabAccess(StationResponseSigner signer):IStationOnlineAccess,IDisposable
{
 public sealed class User(int i):IDisposable {
  public readonly RSA Key=RSA.Create(2048);public readonly string Bearer=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
  public readonly OnlineIdentity Identity=new("synthetic-license-"+i,"synthetic-device-"+i);public string Nickname=>"Synthetic "+i;
  public void Dispose()=>Key.Dispose();
 }
 public User[] Users {get;}=Enumerable.Range(0,4).Select(i=>new User(i)).ToArray();
 public Task<StationSession?> Authenticate(string bearer,CancellationToken token){var u=Users.FirstOrDefault(u=>u.Bearer==bearer);return Task.FromResult(u is null?null:new StationSession("synthetic-session",u.Identity.LicenseId,u.Identity.DeviceId,u.Nickname,1,"rsa-pss-v1",StationProtocol.Encode(u.Key.ExportSubjectPublicKeyInfo())));}
 public Task<bool> AuthorizeRelay(StationOnline.RelayLease lease,CancellationToken token)=>Task.FromResult(Users.Any(u=>u.Identity==lease.Identity));
 public object Sign(object payload)=>signer.Sign(payload);public void Dispose(){foreach(var user in Users)user.Dispose();}
}
