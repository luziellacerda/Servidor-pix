using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

sealed record SuiteAuditItem(string OccurredAt,string EventType,string Outcome,string DetailCode,
    string Actor,string RequestId,string? OtpExpiresAt);
sealed record SuiteAdminStatus(string LicenseId,string ProductId,string Status,string LicenseTerm,
    DateTime? ExpiresAt,string IdentityPolicy,int MaximumActiveDevices,bool ActivationConsumed,
    bool OtpIssued,DateTime? OtpExpiresAt,string DeviceId,string BindingType,string EnrollmentPolicy,
    string Algorithm,string HardwareFingerprint,long ActiveDevices,string? SessionId,string OtpState,
    bool CanIssue,IReadOnlyList<SuiteAuditItem> RecentEvents);
sealed record AuditResult(string Code);
sealed record SuiteOtpResult(string ProductId,string LicenseId,string DeviceId,string Otp,DateTime ExpiresAt);

sealed class SuiteAdminBff : IDisposable
{
    private readonly HttpClient client;
    private readonly string token;
    public bool Enabled { get; }

    public SuiteAdminBff()
    {
        var socket=(Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_SOCKET")??"").Trim();
        var tokenFile=(Environment.GetEnvironmentVariable("TURBORAMA_SUITE_ADMIN_TOKEN_FILE")??"").Trim();
        Enabled=socket.Length!=0&&tokenFile.Length!=0;
        token=Enabled?LoadToken(tokenFile):"";
        var handler=new SocketsHttpHandler { ConnectCallback=async(_,ct)=>
        {
            if(!Enabled)throw new HttpRequestException("SUITE_ADMIN_DISABLED");
            var raw=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
            try{await raw.ConnectAsync(new UnixDomainSocketEndPoint(socket),ct);return new NetworkStream(raw,true);}
            catch{raw.Dispose();throw;}
        }};
        client=new HttpClient(handler){BaseAddress=new Uri("http://localhost/"),Timeout=TimeSpan.FromSeconds(10)};
    }

    public Task<SuiteAdminStatus>StatusAsync(string id,CancellationToken ct)
        =>Send<SuiteAdminStatus>(HttpMethod.Get,"status/"+Uri.EscapeDataString(id),null,ct);
    public Task<SuiteOtpResult>IssueAsync(string id,string device,string actor,string requestId,CancellationToken ct)
        =>Send<SuiteOtpResult>(HttpMethod.Post,"issue",new{licenseId=id,deviceId=device,ttlSeconds=900,actor,requestId},ct);
    public Task<AuditResult>DenyAsync(string id,string device,string actor,string requestId,string detail,CancellationToken ct)
        =>Send<AuditResult>(HttpMethod.Post,"deny",new{licenseId=id,deviceId=device,actor,requestId,detailCode=detail},ct);
    public async Task<byte[]>AuditAsync(CancellationToken ct)
    {using var request=Request(HttpMethod.Get,"audit.csv",null);using var response=await client.SendAsync(request,ct);response.EnsureSuccessStatusCode();return await response.Content.ReadAsByteArrayAsync(ct);}
    private async Task<T>Send<T>(HttpMethod method,string path,object? body,CancellationToken ct)
    {using var request=Request(method,path,body);using var response=await client.SendAsync(request,ct);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<T>(cancellationToken:ct)??throw new HttpRequestException("SUITE_ADMIN_EMPTY");}
    private HttpRequestMessage Request(HttpMethod method,string path,object? body)
    {
        if(!Enabled||token.Length==0)throw new HttpRequestException("SUITE_ADMIN_DISABLED");
        var request=new HttpRequestMessage(method,path);request.Headers.TryAddWithoutValidation("X-Suite-Admin-Token",token);
        if(body is not null)request.Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json");
        return request;
    }
    private static string LoadToken(string path)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        var mode=File.GetUnixFileMode(path);
        if((mode&(UnixFileMode.GroupWrite|UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))!=0)
            throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID");
        var text=File.ReadAllText(path).Trim();byte[] decoded;
        try{decoded=Convert.FromBase64String(text);}catch(FormatException){throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID");}
        try
        {
            if(decoded.Length<32||Convert.ToBase64String(decoded)!=text)throw new InvalidOperationException("SUITE_ADMIN_TOKEN_INVALID");
            return text;
        }
        finally{CryptographicOperations.ZeroMemory(decoded);}
    }
    public void Dispose()=>client.Dispose();
}

sealed class SuiteIssueGuard
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,long> last=new();
    public bool Allow(string key)
    {
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while(true){if(!last.TryGetValue(key,out var prior))return last.TryAdd(key,now);if(now-prior<60)return false;if(last.TryUpdate(key,now,prior))return true;}
    }
}
