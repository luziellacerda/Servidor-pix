// Synthetic transport faults and controlled Close races. Production Attach/hub/stream run unchanged.
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

int checks=0;void Check(bool ok,string name){checks++;if(!ok)throw new InvalidOperationException(name);}
var cases=new List<object>();
foreach(string fault in new[]{"close","close-in-flight","close-timeout","eof","receive-io","send-io","request-cancel","close-cancel","human-leave"}){
    string hash=new('a',64);using var key=RSA.Create(2048);var security=new StationSessionSecurity("rsa-pss-v1",StationProtocol.Encode(key.ExportSubjectPublicKeyInfo()));
    var hub=new StationOnline([new("synthetic","snes",hash,hash,"station-stream.v2")],_=>"snes",()=>0L,relayEnabled:true,recoveryEnabled:true);
    var host=new OnlineIdentity("private-host","private-device-host");var guest=new OnlineIdentity("private-guest","private-device-guest");
    JsonElement Command(OnlineIdentity who,OnlineCommand c)=>JsonSerializer.SerializeToElement(hub.Command(who,c,security));
    string Id()=>Guid.NewGuid().ToString();
    Command(host,new("enter",Id(),Nickname:"Synthetic host"));Command(guest,new("enter",Id(),Nickname:"Synthetic guest"));
    string rid=Command(host,new("create",Id(),ItemId:"synthetic",EngineId:"synthetic",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2")).GetProperty("room").GetProperty("roomId").GetString()!;
    Command(guest,new("join",Id(),RoomId:rid,ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2"));
    Command(host,new("ready",Id(),RoomId:rid,Value:true));Command(guest,new("ready",Id(),RoomId:rid,Value:true));
    long generation=Command(host,new("start",Id(),RoomId:rid,Transport:"relay-wss-v2")).GetProperty("room").GetProperty("generation").GetInt64();
    StationOnline.RelayLease Ticket(string action){var room=Command(host,new(action,Id(),RoomId:rid,Generation:generation,EngineId:"synthetic",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2")).GetProperty("room");return hub.TakeRelayTicket(room.GetProperty("relay").GetProperty("ticket").GetString()!,_=>{});}
    var logger=new Recorder();using var relay=new StationRecoveryRelay(hub,new SyntheticAccess(),logger);
    using var ws=new ControlledSocket{BlockSend=fault is "close-in-flight" or "close-timeout",FailSend=fault=="send-io"};
    using var aborted=new CancellationTokenSource();var started=System.Diagnostics.Stopwatch.GetTimestamp();
    Task attach=relay.Attach(Ticket("relay-ticket"),ws,aborted.Token);
    await ws.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
    switch(fault){
        case "close":case "close-in-flight":case "close-timeout":ws.Incoming.Writer.TryWrite(WebSocketCloseStatus.NormalClosure);break;
        case "close-cancel":ws.Incoming.Writer.TryWrite(WebSocketCloseStatus.NormalClosure);aborted.Cancel();break;
        case "eof":ws.Incoming.Writer.TryWrite(new WebSocketException(WebSocketError.ConnectionClosedPrematurely));break;
        case "receive-io":ws.Incoming.Writer.TryWrite(new IOException());break;
        case "request-cancel":aborted.Cancel();break;
        case "human-leave":Command(host,new("leave",Id()));relay.Prune();break;
    }
    if(fault=="close-in-flight"){
        await ws.CloseReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(ws.CloseOutputCalls==0,"close reply waits for outstanding send");ws.ReleaseSend.TrySetResult();
    }
    await attach.WaitAsync(TimeSpan.FromSeconds(5));double elapsed=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    Check(logger.Ends.Count==1,"one first cause for racing finalizers");var end=logger.Ends.Single();
    string cause=end["Cause"]?.ToString()??"";
    Check((long)end["FirstEpoch"]! ==(fault=="human-leave"?2:1),"snapshot epoch before own detach");
    Check((long)end["AfterEpoch"]! ==2,"after detach epoch distinct");
    Check(ws.MaximumWriters==1,"single Send/CloseOutput writer");
    if(fault is "close" or "close-in-flight"){
        Check(ws.CloseOutputCalls==1&&ws.RepliedCode==WebSocketCloseStatus.NormalClosure,"reply Close1000 exactly once");
        Check(ws.AbortCalls==0&&end["GracefulClose"] is true,"no Abort on completed handshake");Check(cause=="PEER_CLOSE","Close classified separately");
    }else{
        Check(ws.AbortCalls==1,"broken/cancelled connection aborted once");
        Check(end["GracefulClose"] is false,"no fabricated graceful completion");
    }
    if(fault=="close-timeout")Check(elapsed<4500&&elapsed>=1800,"closing timeout bounded, no second writer");
    if(fault=="eof"||fault=="receive-io")Check(cause=="RECEIVE_FAILURE","typed receive failure distinct from Close");
    if(fault=="send-io")Check(cause=="SEND_FAILURE","send failure first cause");
    if(fault=="request-cancel")Check(cause=="REQUEST_ABORT","request cancellation separate");
    if(fault=="human-leave"){
        Check(cause=="ROOM_ENDED","room end distinguishable from transport error");
        Check(Command(guest,new("heartbeat",Id())).GetProperty("room").ValueKind==JsonValueKind.Null,"human leave ends room deliberately");
    }else{
        var recovered=Ticket("resume-relay");Check(recovered.Generation==generation,"authorized resume after transport end preserves generation");hub.CloseRelay(recovered);
        var diagnostic=JsonSerializer.SerializeToElement(relay.Diagnostics());
        var stream=diagnostic.GetProperty("active")[0].GetProperty("stream");
        Check(stream.GetProperty("terminations").GetArrayLength()==1,"one bounded anonymous termination record");
        Check(!diagnostic.GetRawText().Contains("private-"),"no device/peer identity in diagnostics");
    }
    cases.Add(new{fault,cause,elapsedMs=elapsed,singleWriter=true});
}
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,cases,scope="Production relay with simulated faulting sockets; real TLS/WSS qualification required separately"}));

sealed class SyntheticAccess:IStationOnlineAccess
{
    public Task<StationSession?> Authenticate(string bearer,CancellationToken token)=>Task.FromResult<StationSession?>(null);
    public Task<bool> AuthorizeRelay(StationOnline.RelayLease lease,CancellationToken token)=>Task.FromResult(true);
    public object Sign(object payload)=>payload;
}
sealed class Recorder:ILogger<StationRecoveryRelay>
{
    public readonly List<Dictionary<string,object?>> Ends=[];
    public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
    public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter){
        if(state is IEnumerable<KeyValuePair<string,object?>> values){var fields=values.ToDictionary(v=>v.Key,v=>v.Value);if(fields.ContainsKey("Cause"))lock(Ends)Ends.Add(fields);}}
}
sealed class ControlledSocket:WebSocket
{
    public readonly Channel<object> Incoming=Channel.CreateUnbounded<object>();
    public readonly TaskCompletionSource SendStarted=new(TaskCreationOptions.RunContinuationsAsynchronously),ReleaseSend=new(TaskCreationOptions.RunContinuationsAsynchronously),CloseReceived=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool BlockSend,FailSend;public int AbortCalls,CloseOutputCalls,MaximumWriters;private int writers;
    private WebSocketState state=WebSocketState.Open;public WebSocketCloseStatus? RepliedCode;
    private WebSocketCloseStatus? receivedCloseStatus;
    public override WebSocketCloseStatus? CloseStatus=>receivedCloseStatus;
    public override string? CloseStatusDescription=>null;
    public override WebSocketState State=>state;
    public override string? SubProtocol=>"station-stream.v2";
    public override void Abort(){AbortCalls++;state=WebSocketState.Aborted;}
    public override void Dispose(){}
    public override Task CloseAsync(WebSocketCloseStatus code,string? reason,CancellationToken token)=>throw new InvalidOperationException("CloseAsync is not the single writer");
    public override async Task CloseOutputAsync(WebSocketCloseStatus code,string? reason,CancellationToken token){
        int count=Interlocked.Increment(ref writers);MaximumWriters=Math.Max(MaximumWriters,count);
        try{token.ThrowIfCancellationRequested();if(count!=1)throw new InvalidOperationException("two writers");CloseOutputCalls++;RepliedCode=code;await Task.Yield();state=WebSocketState.Closed;}
        finally{Interlocked.Decrement(ref writers);}}
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken token){
        object value=await Incoming.Reader.ReadAsync(token);if(value is Exception error)throw error;
        if(value is WebSocketCloseStatus code){receivedCloseStatus=code;state=WebSocketState.CloseReceived;CloseReceived.TrySetResult();return new(0,WebSocketMessageType.Close,true,code,null);}
        throw new InvalidOperationException("Unknown synthetic frame");}
    public override async Task SendAsync(ArraySegment<byte> buffer,WebSocketMessageType type,bool final,CancellationToken token){
        int count=Interlocked.Increment(ref writers);MaximumWriters=Math.Max(MaximumWriters,count);SendStarted.TrySetResult();
        try{if(count!=1)throw new InvalidOperationException("two writers");if(FailSend)throw new IOException();if(BlockSend)await ReleaseSend.Task.WaitAsync(token);}
        finally{Interlocked.Decrement(ref writers);}}
}
