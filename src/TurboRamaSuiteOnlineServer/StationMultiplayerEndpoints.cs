using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

public static class StationMultiplayerEndpoints
{
    public static void MapStationMultiplayer(this WebApplication app,bool enabled)
    {
        app.MapPost(StationMultiplayer.CommandPath,async (HttpContext context)=>{
            context.Response.Headers.CacheControl="no-store";context.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(!enabled)return Error(503,"STATION_MULTIPLAYER_DISABLED");
            try{
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(15000);
                Need(!context.Request.QueryString.HasValue,400,"STATION_MULTIPLAYER_QUERY_INVALID");
                var header=context.Request.Headers.Authorization.ToString();
                Need(header.StartsWith("Bearer ",StringComparison.Ordinal)&&header.Length==50&&StationProtocol.IsCanonicalBase64Url(header[7..],32),401,"STATION_SESSION_INVALID");
                string bearer=header[7..];var access=context.RequestServices.GetRequiredService<IStationOnlineAccess>();
                bool cached=context.Items[typeof(StationSession)] is ValueTuple<string,StationSession> found&&found.Item1==bearer;
                var session=cached?((ValueTuple<string,StationSession>)context.Items[typeof(StationSession)]!).Item2:await access.Authenticate(bearer,timeout.Token);
                Need(session is not null,401,"STATION_SESSION_INVALID");
                byte[] bytes=await Body(context,timeout.Token);
                // Guard normally validates the HTTP proof. Retain verification if this endpoint
                // is composed into a test/host which authenticates without that middleware.
                if(!cached&&session!.ProofMode!="none")context.RequestServices.GetRequiredService<StationRequestProof>().Verify(
                    context.Request.Headers[StationRequestProof.Header].ToString(),session.ProofMode,session.ProofKeySpki!,bearer,"POST",StationMultiplayer.CommandPath,bytes);
                var command=Parse(bytes);var hub=context.RequestServices.GetRequiredService<StationMultiplayer>();
                var identity=new OnlineIdentity(session!.LicenseId,session.DeviceId);object snapshot;
                lock(hub.AdmissionGate){
                    var legacy=context.RequestServices.GetRequiredService<StationOnline>();
                    if(command.Action is "create" or "join")Need(!legacy.HasMembership(identity),409,"STATION_MULTIPLAYER_ALREADY_IN_LEGACY_ROOM");
                    snapshot=hub.Command(identity,command,new(session.ProofMode,session.ProofKeySpki),legacy.MultiplayerIdentity(identity),legacy.MultiplayerBlockedPeers(identity));
                }
                if(await access.Authenticate(bearer,timeout.Token) is null){hub.Revoke(identity);return Error(401,"STATION_SESSION_INVALID");}
                return Results.Json(access.Sign(new{schemaVersion=1,domain=StationProtocol.Prefix+"online/v1",productId=StationProtocol.Product,applicationId=StationProtocol.Application,
                    licenseId=session.LicenseId,deviceId=session.DeviceId,sessionId=session.SessionId,requestId=command.RequestId,snapshot}),StrictJson.Options);
            }catch(OnlineFailure e){if(e.Status==429)context.Response.Headers.RetryAfter="1";return Error(e.Status,e.Code);}
            catch(SuiteException e){return Error(e.StatusCode,e.Code);}
            catch(JsonException){return Error(400,"STATION_MULTIPLAYER_BODY_INVALID");}
            catch(OperationCanceledException)when(!context.RequestAborted.IsCancellationRequested){return Error(504,"STATION_MULTIPLAYER_TIMEOUT");}
        }).DisableAntiforgery();
        app.MapGet(StationMultiplayer.RelayPath,async (HttpContext context)=>{
            context.Response.Headers.CacheControl="no-store";
            try{
                Need(enabled,503,"STATION_MULTIPLAYER_DISABLED");Need(context.WebSockets.IsWebSocketRequest&&!context.Request.QueryString.HasValue&&
                    context.WebSockets.WebSocketRequestedProtocols.Contains(StationMultiplayer.Protocol),400,"STATION_MULTIPLAYER_UPGRADE_REQUIRED");
                var header=context.Request.Headers.Authorization.ToString();Need(header.StartsWith("StationRelay ",StringComparison.Ordinal)&&header.Length==56,401,"STATION_MULTIPLAYER_TICKET_INVALID");
                var hub=context.RequestServices.GetRequiredService<StationMultiplayer>();
                var lease=hub.TakeTicket(header[13..],value=>context.RequestServices.GetRequiredService<StationRequestProof>().Verify(
                    context.Request.Headers[StationRequestProof.Header].ToString(),value.ProofMode,value.ProofKeySpki!,header[13..],"GET",StationMultiplayer.RelayPath,[]));
                try{
                    using var authTimeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);authTimeout.CancelAfter(5000);
                    Need(await context.RequestServices.GetRequiredService<IStationOnlineAccess>().AuthorizeRelay(lease.AuthorizationLease,authTimeout.Token),401,"STATION_SESSION_INVALID");
                    using var socket=await context.WebSockets.AcceptWebSocketAsync(StationMultiplayer.Protocol);
                    await context.RequestServices.GetRequiredService<StationMultiplayerRelay>().Attach(lease,socket,context.RequestAborted);
                }finally{hub.Close(lease);}
            }catch(OnlineFailure e){if(!context.Response.HasStarted)await RelayError(context,e.Status,e.Code);}
            catch(SuiteException e){if(!context.Response.HasStarted)await RelayError(context,e.StatusCode,e.Code);}
            catch(OperationCanceledException){}catch(System.Net.WebSockets.WebSocketException){}
        });
    }
    private static void Need(bool ok,int status,string code){if(!ok)throw new OnlineFailure(status,code);}
    public static StationMultiplayerCommand Parse(byte[] bytes)
    {
        using var document=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=16});
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new JsonException("Object required.");
        var names=new HashSet<string>(StringComparer.Ordinal);foreach(var property in document.RootElement.EnumerateObject())if(!names.Add(property.Name))throw new JsonException("Duplicate member.");
        return document.RootElement.Deserialize<StationMultiplayerCommand>(StrictJson.Options)??throw new JsonException("Command required.");
    }
    private static async Task<byte[]> Body(HttpContext context,CancellationToken cancel)
    {
        Need(context.Request.ContentLength is not >8192,413,"STATION_MULTIPLAYER_BODY_TOO_LARGE");
        using var body=new MemoryStream();byte[] buffer=new byte[2048];int count;
        while((count=await context.Request.Body.ReadAsync(buffer,cancel))>0){Need(body.Length+count<=8192,413,"STATION_MULTIPLAYER_BODY_TOO_LARGE");body.Write(buffer,0,count);}return body.ToArray();
    }
    private static IResult Error(int status,string code)=>Results.Json(new{schemaVersion=1,code,message=code},StrictJson.Options,statusCode:status);
    private static async Task RelayError(HttpContext context,int status,string code)
    {var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,code,message=code},StrictJson.Options);context.Response.StatusCode=status;context.Response.ContentType="application/json; charset=utf-8";context.Response.ContentLength=bytes.Length;await context.Response.Body.WriteAsync(bytes,context.RequestAborted);}
}
