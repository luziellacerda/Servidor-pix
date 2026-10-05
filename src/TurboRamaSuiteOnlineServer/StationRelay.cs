using System.Net.WebSockets;
using System.Text;
using TurboRamaSuiteOnlineServer.Online;
namespace TurboRamaSuiteOnlineServer;

// Only pairs authenticated members of one Station room. Never connects to arbitrary addresses.
public sealed class StationRelay(StationOnline hub)
{
    private sealed class Pair(long generation)
    {
        public readonly long Generation=generation;
        public WebSocket? Host,Client;
        public readonly TaskCompletionSource Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly CancellationTokenSource Stop=new();
        public int Users;
    }
    private readonly object gate=new();private readonly Dictionary<string,Pair> pairs=[];
    public int ActiveRooms {get{lock(gate)return pairs.Count;}}
    public async Task Attach(StationOnline.RelayLease lease,WebSocket socket,CancellationToken aborted)
    {
        Pair pair;
        lock(gate){
            if(!pairs.TryGetValue(lease.RoomId,out pair!)){
                if(pairs.Count>=128)throw new OnlineFailure(503,"STATION_ONLINE_RELAY_FULL");
                pairs[lease.RoomId]=pair=new Pair(lease.Generation);
            }
            if(pair.Generation!=lease.Generation||pair.Stop.IsCancellationRequested||(lease.Host?pair.Host:pair.Client)!=null)throw new OnlineFailure(409,"STATION_ONLINE_RELAY_ALREADY_ATTACHED");
            if(lease.Host)pair.Host=socket;else pair.Client=socket;pair.Users++;
            if(pair.Host!=null&&pair.Client!=null)pair.Ready.TrySetResult();
        }
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(aborted,pair.Stop.Token);
        Task watch=Watch(lease,cancel);
        try{
            await pair.Ready.Task.WaitAsync(TimeSpan.FromSeconds(60),cancel.Token);
            var destination=lease.Host?pair.Client!:pair.Host!;
            // One reader and one writer per WebSocket: the other peer sends this socket's bytes.
            byte[] buffer=new byte[32768];long window=Environment.TickCount64;long bytes=0;
            while(!cancel.IsCancellationRequested){
                var read=await socket.ReceiveAsync(buffer.AsMemory(),cancel.Token);
                if(read.MessageType==WebSocketMessageType.Close)break;
                if(read.MessageType!=WebSocketMessageType.Binary)throw new IOException("Binary relay required");
                long now=Environment.TickCount64;if(now-window>=1000){window=now;bytes=0;}
                bytes+=read.Count;if(bytes>8*1024*1024){await Task.Delay((int)Math.Max(1,1000-(now-window)),cancel.Token);window=Environment.TickCount64;bytes=read.Count;}
                // Await the peer write; no unbounded queue or user-controlled destination.
                await destination.SendAsync(buffer.AsMemory(0,read.Count),WebSocketMessageType.Binary,read.EndOfMessage,cancel.Token);
            }
        }
        catch(Exception e)when(e is OperationCanceledException or TimeoutException or WebSocketException or IOException){}
        finally{
            cancel.Cancel();socket.Abort();
            lock(gate){pair.Stop.Cancel();pair.Host?.Abort();pair.Client?.Abort();pair.Users--;if(pair.Users==0){pairs.Remove(lease.RoomId);pair.Stop.Dispose();}}
            hub.CloseRelay(lease);try{await watch;}catch(OperationCanceledException){}
        }
    }
    private async Task Watch(StationOnline.RelayLease lease,CancellationTokenSource stop)
    {
        try{while(!stop.IsCancellationRequested){if(!hub.RelayCurrent(lease)){stop.Cancel();return;}await Task.Delay(2000,stop.Token);}}
        catch(OperationCanceledException){}
    }
}
