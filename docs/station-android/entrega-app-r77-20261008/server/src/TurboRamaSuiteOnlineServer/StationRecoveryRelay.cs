using System.Buffers.Binary;
using System.Net.WebSockets;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

// v2 is an ordered byte stream over replaceable WSS, never a restart of the native TCP session.
public readonly record struct StationStreamFrame(byte Type, long Offset, long Value, byte[] Data)
{
    public const int HeaderBytes=24, MaximumDataBytes=16384;
    public const byte Hello=1, Welcome=2, DataPacket=3, Ack=4, Accepted=5,
        State=6, Paused=7, Ready=8, Ping=9, Pong=10, Suspend=11, Foreground=12, NeedSync=13;
    public byte[] Encode()
    {
        var result=new byte[HeaderBytes+Data.Length];"TSR2"u8.CopyTo(result);
        result[4]=Type;BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(8),Offset);
        BinaryPrimitives.WriteInt64BigEndian(result.AsSpan(16),Value);Data.CopyTo(result,HeaderBytes);
        return result;
    }
    public static StationStreamFrame Decode(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<HeaderBytes||bytes.Length>HeaderBytes+MaximumDataBytes||!bytes[..4].SequenceEqual("TSR2"u8)||
            bytes[5]!=0||bytes[6]!=0||bytes[7]!=0||bytes[4] is <Hello or >NeedSync)
            throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
        long offset=BinaryPrimitives.ReadInt64BigEndian(bytes[8..]),value=BinaryPrimitives.ReadInt64BigEndian(bytes[16..]);
        if(offset<0||value<0||offset>long.MaxValue-MaximumDataBytes||
            (bytes[4]==DataPacket?bytes.Length==HeaderBytes||value!=0:bytes.Length!=HeaderBytes))
            throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
        return new(bytes[4],offset,value,bytes[HeaderBytes..].ToArray());
    }
}

// Fixed storage bound. Bytes remain here until the other phone confirms its native TCP write.
public sealed class StationStreamWindow(int capacity)
{
    private readonly byte[] buffer=capacity is >=32768 and <=1048576 ? new byte[capacity]
        :throw new ArgumentOutOfRangeException(nameof(capacity));
    public int Capacity=>buffer.Length;
    public long Accepted {get;private set;}
    public long Delivered {get;private set;}
    public long Pending=>Accepted-Delivered;
    public void Append(long offset,ReadOnlySpan<byte> data)
    {
        if(data.Length is <1 or >StationStreamFrame.MaximumDataBytes||offset<0||offset>long.MaxValue-data.Length||offset>Accepted)
            throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
        long end=offset+data.Length;
        if(offset<Accepted){
            if(end>Accepted)throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
            for(long at=Math.Max(offset,Delivered);at<end;at++)
                if(buffer[at%Capacity]!=data[(int)(at-offset)])throw new OnlineFailure(409,"STATION_RECOVERY_BYTES_MISMATCH");
            return; // Already accepted/delivered; never deliver an input twice.
        }
        if(Pending+data.Length>Capacity)throw new OnlineFailure(409,"STATION_RECOVERY_WINDOW_EXCEEDED");
        for(int i=0;i<data.Length;i++)buffer[(offset+i)%Capacity]=data[i];
        Accepted=end;
    }
    public void Confirm(long offset)
    {
        if(offset<Delivered||offset>Accepted)throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
        Delivered=offset;
    }
    public byte[] Read(long offset)
    {
        if(offset<Delivered||offset>Accepted)throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
        int count=(int)Math.Min(StationStreamFrame.MaximumDataBytes,Accepted-offset);var result=new byte[count];
        for(int i=0;i<count;i++)result[i]=buffer[(offset+i)%Capacity];return result;
    }
}

public sealed class StationStreamSession(int windowBytes,Action? retired=null)
{
    public const long Waiting=0,Synchronizing=1,Playing=2,Unrecoverable=3;
    public sealed class Connection(string id)
    {
        public readonly string Id=id;
        public bool Hello,Paused,Ready,Suspended;
        public long Sent,PeerAccepted=-1,PeerDelivered=-1,StateEpoch=-1,StateValue=-1,Pong=-1;
        public readonly CancellationTokenSource Stop=new();
        public long LastReceive=Environment.TickCount64;
    }
    private readonly object gate=new();
    private readonly StationStreamWindow[] windows=[new(windowBytes),new(windowBytes)];
    private readonly Connection?[] connections=new Connection?[2];
    private readonly long[] maximumSent=new long[2];
    private TaskCompletionSource pulse=NewPulse();
    private long epoch=1,state=Waiting;
    private bool retirementRequested,released;
    public void RetireRoom(){lock(gate)retirementRequested=true;End();}
    private void Retire(){if(retirementRequested&&!released&&connections.All(c=>c is null)){released=true;retired?.Invoke();}}
    private static TaskCompletionSource NewPulse()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Changed(){var old=pulse;pulse=NewPulse();old.TrySetResult();}
    private static int Side(bool host)=>host?0:1;
    private void Current(int side,Connection c){if(connections[side]!=c)throw new OperationCanceledException();}
    public Connection Attach(bool host,string id)
    {
        lock(gate){int side=Side(host);if(connections[side] is not null)throw new OnlineFailure(409,"STATION_ONLINE_RELAY_ALREADY_ATTACHED");
            if(state==Unrecoverable)throw new OnlineFailure(409,"STATION_RECOVERY_UNRECOVERABLE");
            var c=new Connection(id);connections[side]=c;Changed();return c;}
    }
    public void Detach(bool host,Connection c)
    {
        lock(gate){int side=Side(host);if(connections[side]!=c)return;connections[side]=null;
            if(state!=Unrecoverable){state=Waiting;epoch++;foreach(var peer in connections)if(peer is not null){peer.Paused=false;peer.Ready=false;}}
            Retire();Changed();}
    }
    public void End()
    {
        Connection?[] copy;lock(gate){state=Unrecoverable;epoch++;copy=connections.ToArray();Retire();Changed();}
        foreach(var c in copy)if(c is not null)try{c.Stop.Cancel();}catch(ObjectDisposedException){}
    }
    public long CurrentState {get{lock(gate)return state;}}
    public (long Epoch,long State,long Pending,long Accepted,long Delivered,int Connections) Snapshot()
    {lock(gate)return(epoch,state,windows.Sum(w=>w.Pending),windows.Sum(w=>w.Accepted),windows.Sum(w=>w.Delivered),connections.Count(c=>c is not null));}
    public (long HostAccepted,long HostDelivered,long ClientAccepted,long ClientDelivered) Directional()
    {lock(gate)return(windows[0].Accepted,windows[0].Delivered,windows[1].Accepted,windows[1].Delivered);}
    public void Receive(bool host,Connection c,StationStreamFrame frame)
    {
        lock(gate){int side=Side(host);Current(side,c);var own=windows[side];var incoming=windows[1-side];
            c.LastReceive=Environment.TickCount64;
            if(!c.Hello){
                if(frame.Type!=StationStreamFrame.Hello||frame.Offset>own.Accepted)
                    throw new OnlineFailure(409,"STATION_RECOVERY_HELLO_REQUIRED");
                if(frame.Value>maximumSent[side])throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
                incoming.Confirm(frame.Value);c.Sent=frame.Value;c.Hello=true;
            }else switch(frame.Type){
                case StationStreamFrame.DataPacket:
                    own.Append(frame.Offset,frame.Data);
                    foreach(var peer in connections)if(peer is not null)peer.Ready=false;
                    break;
                case StationStreamFrame.Ack:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    if(frame.Offset>maximumSent[side])throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
                    incoming.Confirm(frame.Offset);c.Sent=Math.Max(c.Sent,frame.Offset);break;
                case StationStreamFrame.Paused:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    if(frame.Offset==epoch)c.Paused=true;break;
                case StationStreamFrame.Ready:
                    if(frame.Offset==epoch&&c.Paused&&frame.Value==own.Accepted)c.Ready=true;
                    break;
                case StationStreamFrame.Ping:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    c.Pong=frame.Offset;break;
                case StationStreamFrame.Suspend:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    if(!c.Suspended){c.Suspended=true;state=Waiting;epoch++;foreach(var peer in connections)if(peer is not null){peer.Paused=false;peer.Ready=false;}}
                    break;
                case StationStreamFrame.Foreground:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    c.Suspended=false;break;
                case StationStreamFrame.NeedSync:
                    if(frame.Value!=0)throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    if(frame.Offset==epoch&&state==Playing){state=Waiting;epoch++;foreach(var peer in connections)if(peer is not null){peer.Paused=false;peer.Ready=false;}}
                    break;
                default:throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
            }
            if(state!=Playing&&state!=Unrecoverable&&connections.All(peer=>peer is {Hello:true,Paused:true,Suspended:false})){
                state=Synchronizing;
                if(connections.All(peer=>peer is {Ready:true})&&windows.All(w=>w.Pending==0))state=Playing;
            }
            Changed();}
    }
    public (StationStreamFrame? Frame,Task Changed) Next(bool host,Connection c)
    {
        lock(gate){int side=Side(host);Current(side,c);var own=windows[side];var incoming=windows[1-side];
            StationStreamFrame? frame=null;
            if(c.StateEpoch!=epoch||c.StateValue!=state){c.StateEpoch=epoch;c.StateValue=state;frame=new(StationStreamFrame.State,epoch,state,[]);}
            else if(c.Hello&&(c.PeerAccepted!=own.Accepted||c.PeerDelivered!=own.Delivered)){
                byte type=c.PeerAccepted<0?StationStreamFrame.Welcome:StationStreamFrame.Accepted;
                c.PeerAccepted=own.Accepted;c.PeerDelivered=own.Delivered;frame=new(type,own.Accepted,own.Delivered,[]);
            }else if(c.Pong>=0){frame=new(StationStreamFrame.Pong,c.Pong,0,[]);c.Pong=-1;}
            else if(c.Hello&&c.Sent<incoming.Accepted){
                var data=incoming.Read(c.Sent);frame=new(StationStreamFrame.DataPacket,c.Sent,0,data);c.Sent+=data.Length;
                maximumSent[side]=Math.Max(maximumSent[side],c.Sent);
            }
            return(frame,pulse.Task);}
    }
}

// Waiting rooms occupy a bounded slot indefinitely; admission fails honestly when slots fill.
// No room eviction for age. Process/native-process restart cannot restore this in-memory stream.
public sealed class StationRecoveryRelay(StationOnline hub,IStationOnlineAccess access,
    ILogger<StationRecoveryRelay> logger,int maximumRooms=64,int windowBytes=262144,StationReplayBudget? sharedBudget=null):BackgroundService
{
    private sealed record Match(StationOnline.RelayLease Lease,StationStreamSession Stream);
    private readonly object gate=new();private readonly Dictionary<string,Match> matches=[];
    public object Snapshot(){lock(gate)return new{protocol="station-stream.v2",maximumRooms,windowBytes,
        retainedRooms=matches.Count,activeConnections=matches.Values.Sum(m=>m.Stream.Snapshot().Connections),
        pendingBytes=matches.Values.Sum(m=>m.Stream.Snapshot().Pending),
        maximumRetainedBytes=(long)maximumRooms*windowBytes*2,persistent=false};}
    public async Task Attach(StationOnline.RelayLease lease,WebSocket socket,CancellationToken aborted)
    {
        Match match;lock(gate){
            if(!matches.TryGetValue(lease.RoomId,out match!)){
                if(matches.Count>=maximumRooms)throw new OnlineFailure(503,"STATION_RECOVERY_FULL");
                if(sharedBudget is not null&&!sharedBudget.TryReserve("v2:"+lease.RoomId,(long)windowBytes*2))throw new OnlineFailure(503,"STATION_RECOVERY_FULL");
                try{matches.Add(lease.RoomId,match=new(lease,new StationStreamSession(windowBytes,()=>sharedBudget?.Release("v2:"+lease.RoomId))));}
                catch{sharedBudget?.Release("v2:"+lease.RoomId);throw;}
            }
            if(match.Lease.Generation!=lease.Generation)throw new OnlineFailure(409,"STATION_RECOVERY_GENERATION_MISMATCH");
        }
        var c=match.Stream.Attach(lease.Host,lease.AttachmentId!);
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(aborted,c.Stop.Token);
        string reason="TRANSPORT_CLOSED",exceptionType="none";int? closeCode=null;long firstUtc=0;
        object endGate=new();
        void First(string category,Exception? error=null,int? code=null){lock(endGate){if(firstUtc!=0)return;
            firstUtc=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();reason=category;exceptionType=error?.GetType().Name??"none";closeCode=code;}}
        Task send=Send(),watch=Watch();
        try{
            byte[] buffer=new byte[StationStreamFrame.HeaderBytes+StationStreamFrame.MaximumDataBytes];
            while(!stop.IsCancellationRequested){
                int count=0;ValueWebSocketReceiveResult read;
                do{
                    read=await socket.ReceiveAsync(buffer.AsMemory(count),stop.Token);
                    if(read.MessageType==WebSocketMessageType.Close){First("PEER_CLOSE",code:(int?)socket.CloseStatus);return;}
                    if(read.MessageType!=WebSocketMessageType.Binary||count+read.Count>buffer.Length||!read.EndOfMessage&&count+read.Count==buffer.Length)
                        throw new OnlineFailure(400,"STATION_RECOVERY_FRAME_INVALID");
                    count+=read.Count;
                }while(!read.EndOfMessage);
                match.Stream.Receive(lease.Host,c,StationStreamFrame.Decode(buffer.AsSpan(0,count)));
                PublishState();
            }
        }
        catch(OnlineFailure e){First(e.Code,e);hub.RecoveryState(lease,"unrecoverable");match.Stream.End();}
        catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){First(aborted.IsCancellationRequested?"REQUEST_ABORT":"TRANSPORT_EXCEPTION",e);}
        finally{
            First("TRANSPORT_CLOSED");
            stop.Cancel();socket.Abort();match.Stream.Detach(lease.Host,c);hub.CloseRelay(lease);
            var ages=hub.RelayPresence(lease);var stats=match.Stream.Snapshot();
            var directions=match.Stream.Directional();
            logger.LogInformation("Station recovery event=first-transport-end utcMs={UtcMs} correlation={Correlation} generation={Generation} streamEpoch={Epoch} attachment={Attachment} role={Role} cause={Cause} closeCode={CloseCode} exceptionType={ExceptionType} hostHeartbeatAgeMs={HostAge} clientHeartbeatAgeMs={ClientAge} acceptedBytes={Accepted} deliveredBytes={Delivered} pendingBytes={Pending} state={State} hostAcceptedBytes={HostAccepted} hostDeliveredBytes={HostDelivered} clientAcceptedBytes={ClientAccepted} clientDeliveredBytes={ClientDelivered}",
                firstUtc,lease.Correlation,lease.Generation,stats.Epoch,lease.AttachmentId,lease.Host?"host":"client",reason,closeCode,exceptionType,ages.HostAgeMs,ages.ClientAgeMs,stats.Accepted,stats.Delivered,stats.Pending,stats.State,directions.HostAccepted,directions.HostDelivered,directions.ClientAccepted,directions.ClientDelivered);
            try{await Task.WhenAll(send,watch);}catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){}
            c.Stop.Dispose();
        }
        void PublishState()=>hub.RecoveryState(lease,match.Stream.CurrentState switch{
            StationStreamSession.Playing=>"playing",StationStreamSession.Synchronizing=>"synchronizing",
            StationStreamSession.Unrecoverable=>"unrecoverable",_=>"waiting-reconnect"});
        async Task Send(){try{
            while(!stop.IsCancellationRequested){var next=match.Stream.Next(lease.Host,c);
                if(next.Frame is {} frame){await socket.SendAsync(frame.Encode().AsMemory(),WebSocketMessageType.Binary,true,stop.Token);continue;}
                await next.Changed.WaitAsync(stop.Token);
            }
        }catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){if(!stop.IsCancellationRequested){First("SEND_FAILURE",e);stop.Cancel();}}}
        async Task Watch(){try{
            while(!stop.IsCancellationRequested){
                if(!hub.RelayCurrent(lease)){First("LEASE_INVALIDATED");stop.Cancel();return;}
                var ages=hub.RelayPresence(lease);
                if((lease.Host?ages.HostAgeMs:ages.ClientAgeMs)>=60000){First("AUTH_HEARTBEAT_MISSING");stop.Cancel();return;}
                if(Environment.TickCount64-Volatile.Read(ref c.LastReceive)>=30000){First("TRANSPORT_IDLE");stop.Cancel();return;}
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(5000);
                bool valid;
                try{valid=await access.AuthorizeRelay(lease,timeout.Token);}
                catch(Exception e)when(e is OperationCanceledException or Npgsql.NpgsqlException or IOException){First("AUTH_UNAVAILABLE",e);stop.Cancel();return;}
                if(!valid){First("LICENSE_REVOKED");match.Stream.End();if(lease.Identity is {} identity)hub.Revoke(identity);stop.Cancel();return;}
                await Task.Delay(10000,stop.Token);
            }
        }catch(OperationCanceledException){}}
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try{while(!stoppingToken.IsCancellationRequested){await Task.Delay(15000,stoppingToken);Prune();}}
        catch(OperationCanceledException){}
        Match[] retained;lock(gate){retained=matches.Values.ToArray();matches.Clear();}
        foreach(var match in retained)match.Stream.RetireRoom();
    }
    public void Prune()
    {
        Match[] copy;lock(gate)copy=matches.Values.ToArray();
        foreach(var match in copy)if(!hub.RecoveryRoomCurrent(match.Lease.RoomId,match.Lease.Generation)){
            match.Stream.RetireRoom();lock(gate)if(matches.GetValueOrDefault(match.Lease.RoomId)==match)matches.Remove(match.Lease.RoomId);
        }
    }
}
