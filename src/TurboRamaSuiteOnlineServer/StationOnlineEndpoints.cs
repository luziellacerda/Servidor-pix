using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

public interface IStationOnlineAccess
{
    Task<StationSession?> Authenticate(string bearer,CancellationToken token);
    object Sign(object payload);
}
public sealed class StationOnlineAccess(PostgresStationStore store,StationResponseSigner signer) : IStationOnlineAccess
{
    public Task<StationSession?> Authenticate(string bearer,CancellationToken token)=>store.FindSessionAsync(bearer,token);
    public object Sign(object payload)=>signer.Sign(payload);
}

// Additive Station-only routes. Disabled unless the operator enables Station:Online:Enabled.
public static class StationOnlineEndpoints
{
    public sealed record EventsRequest(string RequestId, string? Instance, long Revision, int Page);
    public static void MapStationOnline(this WebApplication app, bool enabled)
    {
        app.UseWebSockets(new WebSocketOptions {KeepAliveInterval=TimeSpan.FromSeconds(20)});
        // Aggregate operator telemetry only, outside the public /v1 route.
        app.MapGet("/ready/station/online",(HttpContext context)=>{
            if(context.Connection.RemoteIpAddress is not {} address || !System.Net.IPAddress.IsLoopback(address))
                return Results.NotFound();
            context.Response.Headers.CacheControl="no-store";
            if(!enabled)return Results.StatusCode(503);
            return Results.Json(context.RequestServices.GetRequiredService<StationRelay>().Snapshot());
        });
        app.MapGet("/v1/station/online/relay",async (HttpContext context)=>{
            context.Response.Headers.CacheControl="no-store";
            if(!enabled||!app.Configuration.GetValue("Station:Online:RelayEnabled",false)){await RelayError(context,503,"STATION_ONLINE_RELAY_DISABLED");return;}
            if(!context.WebSockets.IsWebSocketRequest||context.Request.QueryString.HasValue||!context.WebSockets.WebSocketRequestedProtocols.Contains("station-relay.v1")){await RelayError(context,400,"STATION_ONLINE_RELAY_UPGRADE_REQUIRED");return;}
            try{
                var header=context.Request.Headers.Authorization.ToString();
                if(!header.StartsWith("StationRelay ",StringComparison.Ordinal)||header.Length!=56)throw new OnlineFailure(401,"STATION_ONLINE_RELAY_TICKET_INVALID");
                var hub=context.RequestServices.GetRequiredService<StationOnline>();var relay=context.RequestServices.GetRequiredService<StationRelay>();
                var lease=hub.TakeRelayTicket(header[13..]);
                try{
                    using var socket=await context.WebSockets.AcceptWebSocketAsync("station-relay.v1");
                    await relay.Attach(lease,socket,context.RequestAborted);
                }finally{hub.CloseRelay(lease);}
            }catch(OnlineFailure e){if(!context.Response.HasStarted)await RelayError(context,e.Status,e.Code);}
            catch(OperationCanceledException){}catch(System.Net.WebSockets.WebSocketException){}
        });
        app.MapPost("/v1/station/online/command", async (HttpContext context) => {
            var result=await Handle(context,enabled,async (hub,identity,cancel) => {
                var cmd=await Read<OnlineCommand>(context,cancel);
                return (cmd.RequestId,hub.Command(identity,cmd));
            });await result.ExecuteAsync(context);
        }).DisableAntiforgery();
        app.MapPost("/v1/station/online/events", async (HttpContext context) => {
            var result=await Handle(context,enabled,async (hub,identity,cancel) => {
                var request=await Read<EventsRequest>(context,cancel);
                if(!Guid.TryParseExact(request.RequestId,"D",out _))throw new OnlineFailure(400,"STATION_ONLINE_REQUEST_INVALID");
                return (request.RequestId,await hub.Events(identity,request.Instance,request.Revision,request.Page,cancel));
            });await result.ExecuteAsync(context);
        }).DisableAntiforgery();
    }
    private static async Task<IResult> Handle(HttpContext context,bool enabled,
        Func<StationOnline,OnlineIdentity,CancellationToken,Task<(string RequestId,object Snapshot)>> action)
    {
        context.Response.Headers.CacheControl="no-store";
        context.Response.Headers["X-Content-Type-Options"]="nosniff";
        if(!enabled)return Error(503,"STATION_ONLINE_DISABLED");
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            string header=context.Request.Headers.Authorization.ToString();
            if(!header.StartsWith("Bearer ",StringComparison.Ordinal)||header.Length>128)
                return Error(401,"STATION_SESSION_INVALID");
            string bearer=header[7..];
            if(!StationProtocol.IsCanonicalBase64Url(bearer,32))return Error(401,"STATION_SESSION_INVALID");
            var access=context.RequestServices.GetRequiredService<IStationOnlineAccess>();
            var session=await access.Authenticate(bearer,timeout.Token);
            if(session is null)return Error(401,"STATION_SESSION_INVALID");
            var identity=new OnlineIdentity(session.LicenseId,session.DeviceId);
            var hub=context.RequestServices.GetRequiredService<StationOnline>();
            var result=await action(hub,identity,timeout.Token);
            // Revalidate after a wait: blocked/unpaid/revoked licenses cannot receive a fresh room secret.
            if(await access.Authenticate(bearer,timeout.Token) is null)
            { hub.Revoke(identity);return Error(401,"STATION_SESSION_INVALID"); }
            return Results.Json(access.Sign(new {
                schemaVersion=1,domain=StationProtocol.Prefix+"online/v1",
                productId=StationProtocol.Product,applicationId=StationProtocol.Application,
                licenseId=session.LicenseId,deviceId=session.DeviceId,sessionId=session.SessionId,
                requestId=result.RequestId,snapshot=result.Snapshot
            }),StrictJson.Options);
        }
        catch(OnlineFailure e){if(e.Status==429)context.Response.Headers.RetryAfter="1";return Error(e.Status,e.Code);}
        catch(JsonException){return Error(400,"STATION_ONLINE_BODY_INVALID");}
        catch(OperationCanceledException) when(!context.RequestAborted.IsCancellationRequested){return Error(504,"STATION_ONLINE_TIMEOUT");}
    }
    private static IResult Error(int status,string code)=>Results.Json(new {schemaVersion=1,code,message=code},StrictJson.Options,statusCode:status);
    private static async Task RelayError(HttpContext context,int status,string code)
    {
        // Failed upgrades are finite HTTP responses. Frame the complete JSON
        // explicitly so intermediary WebSocket proxies need not stream a
        // chunked denial before deciding whether an upgrade succeeded.
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new {schemaVersion=1,code,message=code},StrictJson.Options);
        context.Response.StatusCode=status;
        context.Response.ContentType="application/json; charset=utf-8";
        context.Response.ContentLength=bytes.Length;
        await context.Response.Body.WriteAsync(bytes,context.RequestAborted);
    }
    private static async Task<T> Read<T>(HttpContext context,CancellationToken cancel)
    {
        if(context.Request.ContentLength is >8192)throw new OnlineFailure(413,"STATION_ONLINE_BODY_TOO_LARGE");
        using var stream=new MemoryStream();var buffer=new byte[2048];int count;
        while((count=await context.Request.Body.ReadAsync(buffer,cancel))>0)
        {if(stream.Length+count>8192)throw new OnlineFailure(413,"STATION_ONLINE_BODY_TOO_LARGE");stream.Write(buffer,0,count);}
        using var document=JsonDocument.Parse(stream.ToArray(),new JsonDocumentOptions { MaxDepth=16 });
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new JsonException("Object required.");
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in document.RootElement.EnumerateObject())
            if(!names.Add(property.Name))throw new JsonException("Duplicate member.");
        return document.RootElement.Deserialize<T>(StrictJson.Options)??throw new OnlineFailure(400,"STATION_ONLINE_BODY_INVALID");
    }
}
