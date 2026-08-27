using System.Net.Sockets;
using System.Text;
using System.Text.Json;
sealed record SuiteAdminStatus(string LicenseId,string ProductId,string Status,string LicenseTerm,DateTime? ExpiresAt,string IdentityPolicy,int MaximumActiveDevices,bool ActivationConsumed,bool OtpIssued,DateTime? OtpExpiresAt,string DeviceId,string BindingType,string EnrollmentPolicy,string Algorithm,string HardwareFingerprint,long ActiveDevices,string? SessionId);
sealed record SuiteOtpResult(string ProductId,string LicenseId,string DeviceId,string Otp,DateTime ExpiresAt);
sealed class SuiteAdminBff:IDisposable
{
 readonly HttpClient client;readonly string tokenFile;public bool Enabled{get;}
 public SuiteAdminBff(){var socket=(Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_SOCKET")??"").Trim();tokenFile=(Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_TOKEN_FILE")??"").Trim();Enabled=socket.Length!=0&&tokenFile.Length!=0;var h=new SocketsHttpHandler{ConnectCallback=async(_,ct)=>{if(!Enabled)throw new HttpRequestException("SUITE_ADMIN_DISABLED");var s=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);await s.ConnectAsync(new UnixDomainSocketEndPoint(socket),ct);return new NetworkStream(s,true);}};client=new HttpClient(h){BaseAddress=new Uri("http://localhost/"),Timeout=TimeSpan.FromSeconds(10)};}
 public Task<SuiteAdminStatus>StatusAsync(string id,CancellationToken ct)=>Send<SuiteAdminStatus>(HttpMethod.Get,"status/"+Uri.EscapeDataString(id),null,ct);
 public Task<SuiteOtpResult>IssueAsync(string id,string device,string actor,string requestId,CancellationToken ct)=>Send<SuiteOtpResult>(HttpMethod.Post,"issue",new{licenseId=id,deviceId=device,ttlSeconds=900,actor,requestId},ct);
 public async Task<byte[]>AuditAsync(CancellationToken ct){using var q=await Request(HttpMethod.Get,"audit.csv",null,ct);using var r=await client.SendAsync(q,ct);r.EnsureSuccessStatusCode();return await r.Content.ReadAsByteArrayAsync(ct);}
 async Task<T>Send<T>(HttpMethod method,string path,object? body,CancellationToken ct){using var q=await Request(method,path,body,ct);using var r=await client.SendAsync(q,ct);r.EnsureSuccessStatusCode();return await r.Content.ReadFromJsonAsync<T>(cancellationToken:ct)??throw new HttpRequestException("SUITE_ADMIN_EMPTY");}
 async Task<HttpRequestMessage>Request(HttpMethod method,string path,object? body,CancellationToken ct){if(!Enabled)throw new HttpRequestException("SUITE_ADMIN_DISABLED");var token=(await File.ReadAllTextAsync(tokenFile,ct)).Trim();if(token.Length<32)throw new HttpRequestException("SUITE_ADMIN_TOKEN_INVALID");var q=new HttpRequestMessage(method,path);q.Headers.TryAddWithoutValidation("X-Suite-Admin-Token",token);if(body is not null)q.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");return q;}
 public void Dispose()=>client.Dispose();
}
sealed class SuiteIssueGuard{readonly System.Collections.Concurrent.ConcurrentDictionary<string,long> last=new();public bool Allow(string key){var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();while(true){if(!last.TryGetValue(key,out var prior))return last.TryAdd(key,now);if(now-prior<60)return false;if(last.TryUpdate(key,now,prior))return true;}}}
