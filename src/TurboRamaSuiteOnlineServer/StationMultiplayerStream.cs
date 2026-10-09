using System.Net.WebSockets;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

public static class StationMultiplayerFrame
{
    public static byte[] Encode(StationStreamFrame frame){var bytes=frame.Encode();bytes[3]=(byte)'3';return bytes;}
    public static StationStreamFrame Decode(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<StationStreamFrame.HeaderBytes||bytes.Length>StationStreamFrame.HeaderBytes+StationStreamFrame.MaximumDataBytes||
            bytes[0]!='T'||bytes[1]!='S'||bytes[2]!='R'||bytes[3]!='3')throw new OnlineFailure(400,"STATION_MULTIPLAYER_FRAME_INVALID");
        byte[] copy=bytes.ToArray();copy[3]=(byte)'2';return StationStreamFrame.Decode(copy);
    }
}

// One global barrier. Every link has two independent FIFO replay windows; guest data
// can only reach the native host connection authenticated for that guest's slot.
public sealed class StationMultiplayerStream
{
    public sealed class Connection(string id)
    {
        public readonly string Id=id;public bool Hello,Paused,Ready;
        public long Sent,PeerAccepted=-1,PeerDelivered=-1,StateEpoch=-1,StateValue=-1,Pong=-1;
        public readonly CancellationTokenSource Stop=new();public long LastReceive=Environment.TickCount64;
    }
    private readonly object gate=new();private readonly string[] links;
    private readonly StationStreamWindow[] windows;private readonly Connection?[] connections;private readonly long[] maximumSent;
    private readonly HashSet<int> suspended=[];private TaskCompletionSource pulse=NewPulse();
    private long epoch=1,state=StationStreamSession.Waiting;private readonly Action? retired;private bool released,retirementRequested,hasPlayed;
    public StationMultiplayerStream(string[] links,int windowBytes=StationMultiplayer.WindowBytes,Action? retired=null)
    {
        if(links.Length is <1 or >4||links.Distinct(StringComparer.Ordinal).Count()!=links.Length)throw new ArgumentException("One to four unique links required.");
        this.retired=retired;this.links=links.ToArray();windows=Enumerable.Range(0,links.Length*2).Select(_=>new StationStreamWindow(windowBytes)).ToArray();
        connections=new Connection?[windows.Length];maximumSent=new long[windows.Length];
    }
    private static TaskCompletionSource NewPulse()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Changed(){var old=pulse;pulse=NewPulse();old.TrySetResult();}
    private int Side(string link,bool host){int at=Array.IndexOf(links,link);if(at<0)throw new OnlineFailure(404,"STATION_MULTIPLAYER_LINK_UNKNOWN");return at*2+(host?0:1);}
    private static int Owner(int side)=>side%2==0?0:side/2+1;
    private void Current(int side,Connection c){if(connections[side]!=c)throw new OperationCanceledException();}
    private void Pause(){state=StationStreamSession.Waiting;epoch++;foreach(var c in connections)if(c is not null){c.Paused=false;c.Ready=false;}}
    public Connection Attach(string link,bool host,string id)
    {lock(gate){int side=Side(link,host);if(connections[side] is not null)throw new OnlineFailure(409,"STATION_MULTIPLAYER_ALREADY_ATTACHED");
        if(state==StationStreamSession.Unrecoverable)throw new OnlineFailure(409,"STATION_MULTIPLAYER_UNRECOVERABLE");
        var c=new Connection(id);connections[side]=c;Changed();return c;}}
    public void Detach(string link,bool host,Connection c)
    {lock(gate){int side=Side(link,host);if(connections[side]!=c)return;connections[side]=null;
        if(state!=StationStreamSession.Unrecoverable)Pause();Retire();Changed();}}
    public void End()
    {Connection?[] copy;lock(gate){state=StationStreamSession.Unrecoverable;epoch++;copy=connections.ToArray();Retire();Changed();}
        foreach(var c in copy)if(c is not null)try{c.Stop.Cancel();}catch(ObjectDisposedException){}}
    public void RetireRoom(){lock(gate)retirementRequested=true;End();}
    private void Retire(){if(retirementRequested&&!released&&state==StationStreamSession.Unrecoverable&&connections.All(c=>c is null)){released=true;retired?.Invoke();}}
    public long CurrentState {get{lock(gate)return state;}}
    public bool HasPlayed {get{lock(gate)return hasPlayed;}}
    public (long Epoch,long State,long Pending,long Accepted,long Delivered,int Connections) Snapshot()
    {lock(gate)return(epoch,state,windows.Sum(w=>w.Pending),windows.Sum(w=>w.Accepted),windows.Sum(w=>w.Delivered),connections.Count(c=>c is not null));}
    public void Receive(string link,bool host,Connection c,StationStreamFrame frame)
    {
        lock(gate){int side=Side(link,host);Current(side,c);var own=windows[side];var incoming=windows[side^1];
            if(state==StationStreamSession.Unrecoverable)throw new OperationCanceledException();
            Volatile.Write(ref c.LastReceive,Environment.TickCount64);
            if(!c.Hello){
                if(frame.Type!=StationStreamFrame.Hello||frame.Offset>own.Accepted)throw new OnlineFailure(409,"STATION_RECOVERY_HELLO_REQUIRED");
                if(frame.Value>maximumSent[side])throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
                incoming.Confirm(frame.Value);c.Sent=frame.Value;c.Hello=true;
            }else switch(frame.Type){
                case StationStreamFrame.DataPacket:{
                    long before=own.Accepted;own.Append(frame.Offset,frame.Data);
                    // Only this link receives new ACCEPTED/DATA watermarks. Unchanged links
                    // cannot re-acknowledge an invisible invalidation. Duplicate replay also
                    // leaves readiness intact when it does not advance the accepted offset.
                    if(own.Accepted!=before){c.Ready=false;if(connections[side^1] is {} other)other.Ready=false;}
                    break;}
                case StationStreamFrame.Ack:
                    if(frame.Value!=0||frame.Offset>maximumSent[side])throw new OnlineFailure(409,"STATION_RECOVERY_OFFSET_INVALID");
                    incoming.Confirm(frame.Offset);c.Sent=Math.Max(c.Sent,frame.Offset);break;
                case StationStreamFrame.Paused:
                    Zero(frame);if(frame.Offset==epoch)c.Paused=true;break;
                case StationStreamFrame.Ready:
                    if(frame.Offset==epoch&&c.Paused&&frame.Value==own.Accepted)c.Ready=true;break;
                case StationStreamFrame.Ping:Zero(frame);c.Pong=frame.Offset;break;
                case StationStreamFrame.Suspend:
                    Zero(frame);if(suspended.Add(Owner(side)))Pause();break;
                case StationStreamFrame.Foreground:Zero(frame);suspended.Remove(Owner(side));break;
                case StationStreamFrame.NeedSync:
                    Zero(frame);if(frame.Offset==epoch&&state==StationStreamSession.Playing)Pause();break;
                default:throw new OnlineFailure(400,"STATION_MULTIPLAYER_FRAME_INVALID");
            }
            if(state!=StationStreamSession.Playing&&state!=StationStreamSession.Unrecoverable&&suspended.Count==0&&connections.All(peer=>peer is {Hello:true,Paused:true})){
                state=StationStreamSession.Synchronizing;
                if(connections.All(peer=>peer is {Ready:true})&&windows.All(w=>w.Pending==0)){state=StationStreamSession.Playing;hasPlayed=true;}
            }
            Changed();
        }
    }
    private static void Zero(StationStreamFrame frame){if(frame.Value!=0)throw new OnlineFailure(400,"STATION_MULTIPLAYER_FRAME_INVALID");}
    public (StationStreamFrame? Frame,Task Changed) Next(string link,bool host,Connection c)
    {
        lock(gate){int side=Side(link,host);Current(side,c);var own=windows[side];var incoming=windows[side^1];StationStreamFrame? frame=null;
            if(c.StateEpoch!=epoch||c.StateValue!=state){c.StateEpoch=epoch;c.StateValue=state;frame=new(StationStreamFrame.State,epoch,state,[]);}
            else if(c.Hello&&(c.PeerAccepted!=own.Accepted||c.PeerDelivered!=own.Delivered)){
                byte type=c.PeerAccepted<0?StationStreamFrame.Welcome:StationStreamFrame.Accepted;
                c.PeerAccepted=own.Accepted;c.PeerDelivered=own.Delivered;frame=new(type,own.Accepted,own.Delivered,[]);
            }else if(c.Pong>=0){frame=new(StationStreamFrame.Pong,c.Pong,0,[]);c.Pong=-1;}
            else if(c.Hello&&c.Sent<incoming.Accepted){var data=incoming.Read(c.Sent);frame=new(StationStreamFrame.DataPacket,c.Sent,0,data);c.Sent+=data.Length;maximumSent[side]=Math.Max(maximumSent[side],c.Sent);}
            return(frame,pulse.Task);
        }
    }
}

public sealed class StationMultiplayerRelay(StationMultiplayer hub,IStationOnlineAccess access,ILogger<StationMultiplayerRelay> logger)
{
    public async Task Attach(StationMultiplayer.Lease lease,WebSocket socket,CancellationToken aborted)
    {
        var stream=hub.Stream(lease);var connection=stream.Attach(lease.LinkId,lease.HostSide,lease.AttachmentId);
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(aborted,connection.Stop.Token);
        string reason="transport-closed";long epochAtFirstCause=0;object endGate=new();
        void First(string category){lock(endGate){if(epochAtFirstCause!=0)return;reason=category;epochAtFirstCause=stream.Snapshot().Epoch;}}
        Task send=Send(),watch=Watch();bool closeReceived=false;
        try{
            byte[] buffer=new byte[StationStreamFrame.HeaderBytes+StationStreamFrame.MaximumDataBytes];
            while(!stop.IsCancellationRequested){
                int count=0;ValueWebSocketReceiveResult read;
                do{read=await socket.ReceiveAsync(buffer.AsMemory(count),stop.Token);
                    if(read.MessageType==WebSocketMessageType.Close){First("peer-close");closeReceived=true;return;}
                    if(read.MessageType!=WebSocketMessageType.Binary||count+read.Count>buffer.Length||!read.EndOfMessage&&count+read.Count==buffer.Length)throw new OnlineFailure(400,"STATION_MULTIPLAYER_FRAME_INVALID");
                    count+=read.Count;
                }while(!read.EndOfMessage);
                stream.Receive(lease.LinkId,lease.HostSide,connection,StationMultiplayerFrame.Decode(buffer.AsSpan(0,count)));
            }
        }catch(OnlineFailure e){First(e.Code);stream.End();}
        catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){First(aborted.IsCancellationRequested?"request-abort":"transport-exception");}
        finally{
            First("transport-closed");stop.Cancel();
            // The send loop is the sole data/control writer. Join it BEFORE close output.
            try{await Task.WhenAll(send,watch);}catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){}
            if(closeReceived&&socket.State==WebSocketState.CloseReceived){using var closeTimeout=new CancellationTokenSource(2000);
                try{await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"",closeTimeout.Token);}catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){}}
            socket.Abort();stream.Detach(lease.LinkId,lease.HostSide,connection);hub.Close(lease);
            logger.LogInformation("Station multiplayer transport-end generation={Generation} slot={Slot} hostSide={HostSide} cause={Cause} epochAtFirstCause={FirstEpoch} epochAfterDetach={AfterEpoch}",lease.Generation,lease.OwnerSlot,lease.HostSide,reason,epochAtFirstCause,stream.Snapshot().Epoch);
            connection.Stop.Dispose();
        }
        async Task Send(){try{while(!stop.IsCancellationRequested){var next=stream.Next(lease.LinkId,lease.HostSide,connection);
            if(next.Frame is {} frame){await socket.SendAsync(StationMultiplayerFrame.Encode(frame).AsMemory(),WebSocketMessageType.Binary,true,stop.Token);continue;}
            await next.Changed.WaitAsync(stop.Token);
        }}catch(Exception e)when(e is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException){if(!stop.IsCancellationRequested){First("send-failure");stop.Cancel();}}}
        async Task Watch(){try{while(!stop.IsCancellationRequested){
            if(!hub.Current(lease)){First("lease-invalidated");stop.Cancel();return;}
            if(hub.PresenceAge(lease)>=60000){First("auth-heartbeat-missing");stop.Cancel();return;}
            if(Environment.TickCount64-Volatile.Read(ref connection.LastReceive)>=30000){First("transport-idle");stop.Cancel();return;}
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(5000);
            bool valid;
            try{valid=await access.AuthorizeRelay(lease.AuthorizationLease,timeout.Token);}
            catch(Exception e)when(e is OperationCanceledException or Npgsql.NpgsqlException or IOException){First("auth-unavailable");stop.Cancel();return;}
            if(!valid){First("license-revoked");hub.Revoke(lease.Identity);stop.Cancel();return;}
            await Task.Delay(10000,stop.Token);
        }}catch(OperationCanceledException){}}
    }
}
