using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

// Deterministic synthetic identities only. Exercises the actual hub, stream and replay windows.
static class BarrierDiagnosticsChecks
{
    public static void Run(Action<bool,string> check)
    {
        int localChecks=0;
        void Expect(bool value,string name){check(value,"three-player barrier: "+name);localChecks++;}
        using(var stalled=new Fixture()){
            stalled.Start();
            var initial=stalled.Diagnostics();
            Expect(initial.GetProperty("linkCount").GetInt32()==2,"three players create two host-star links");
            Expect(initial.GetProperty("expectedAttachments").GetInt32()==4,"all four attachments are required");
            Expect(initial.GetProperty("attachedConnections").GetInt32()==0,"missing attachments are numeric");
            Expect(Reason(initial,"missing-attachment"),"missing attachment has an explicit waiting reason");
            var outsider=stalled.Issue(new OnlineIdentity("synthetic-outsider","synthetic-outsider"),"snapshot");
            Expect(outsider.GetProperty("rooms")[0].GetProperty("barrierDiagnostics").ValueKind==JsonValueKind.Null,
                "nonmember room listing does not expose detailed diagnostics");
            stalled.AttachAll();
            stalled.HelloAndPauseAll();
            for(int i=0;i<stalled.Connections.Count-1;i++)stalled.Ready(i);
            var before=stalled.Diagnostics();
            var coherent=stalled.Issue(stalled.People[0],"snapshot").GetProperty("room");
            Expect(coherent.GetProperty("epoch").GetInt64()==coherent.GetProperty("barrierDiagnostics").GetProperty("epoch").GetInt64(),
                "room epoch and member diagnostics share one locked snapshot");
            Expect(before.GetProperty("state").GetInt64()==StationStreamSession.Synchronizing,"one missing READY blocks play");
            Expect(before.GetProperty("helloCount").GetInt32()==4&&before.GetProperty("pausedCount").GetInt32()==4,
                "all four real stream handshakes are counted");
            Expect(before.GetProperty("readyCount").GetInt32()==3&&before.GetProperty("pendingBytes").GetInt64()==0,
                "missing READY remains distinct from replay bytes");
            Expect(Reason(before,"awaiting-ready"),"the missing READY cause is explicit");
            // Five minutes of the hub's monotonic fixture clock. PING is not READY.
            for(int pulse=0;pulse<60;pulse++){
                stalled.Advance(5000);
                foreach(var person in stalled.People)stalled.Issue(person,"heartbeat");
                for(int i=0;i<stalled.Connections.Count;i++)stalled.Receive(i,StationStreamFrame.Ping,pulse,0,[]);
            }
            var still=stalled.Diagnostics();
            Expect(still.GetProperty("state").GetInt64()==StationStreamSession.Synchronizing&&
                still.GetProperty("readyCount").GetInt32()==3,"heartbeat and PING never fabricate the missing READY");
            Expect(!stalled.Stream!.HasPlayed,"no automatic start after five fixture minutes");
            Expect(stalled.Budget.UsedBytes==1048576,"three-player replay reservation is exactly one MiB");
            long generation=stalled.Room.GetProperty("generation").GetInt64();
            try{stalled.Issue(stalled.People[0],"leave",generation:generation-1);Expect(false,"stale generation must fail");}
            catch(OnlineFailure error){Expect(error.Code=="STATION_MULTIPLAYER_GENERATION_MISMATCH",
                "stale generation rejects leave before membership changes");}
            Expect(stalled.Issue(stalled.People[0],"snapshot").GetProperty("room").ValueKind==JsonValueKind.Object,
                "a rejected stale leave did not silently leave");
            stalled.Ready(stalled.Connections.Count-1);
            Expect(stalled.Stream.CurrentState==StationStreamSession.Playing,"four READY with no pending bytes permits play");
            var serialized=stalled.Diagnostics().GetRawText();
            Expect(!new[]{"peerId","roomId","linkId","ticket","licenseId","deviceId","password","payload","data"}
                .Any(key=>serialized.Contains("\""+key+"\"",StringComparison.OrdinalIgnoreCase)),
                "diagnostics contain no identities credentials or payload bytes");
            stalled.Issue(stalled.People[0],"leave",generation:generation);
            Expect(stalled.Issue(stalled.People[1],"snapshot").GetProperty("room").ValueKind==JsonValueKind.Null,
                "human leave clears the whole started room");
            Expect(stalled.Budget.UsedBytes==1048576,"writers retain reservation until detach");
            stalled.DetachAll();
            Expect(stalled.Budget.UsedBytes==0,"last detach releases the reservation");
        }
        using(var pending=new Fixture()){
            pending.Start();pending.AttachAll();pending.HelloAndPauseAll();
            for(int i=0;i<pending.Connections.Count;i++)pending.Ready(i);
            Expect(pending.Stream!.CurrentState==StationStreamSession.Playing,"complete three-player handshake enters play");
            pending.Receive(0,StationStreamFrame.NeedSync,pending.Epoch,0,[]);
            long epoch=pending.Epoch;
            for(int i=0;i<pending.Connections.Count;i++){
                pending.Receive(i,StationStreamFrame.Paused,epoch,0,[]);
                if(i!=0)pending.Ready(i);
            }
            pending.Receive(0,StationStreamFrame.DataPacket,0,0,[1,2,3]);
            var diag=pending.Diagnostics();
            Expect(diag.GetProperty("pendingBytes").GetInt64()==3&&Reason(diag,"pending-replay"),
                "pending replay is separately exposed");
            Expect(diag.GetProperty("readyCount").GetInt32()==2,"new data invalidates only its touched pair");
            pending.Receive(0,StationStreamFrame.Ready,epoch,0,[]);
            Expect(!pending.Connections[0].Connection.Ready,"old watermark cannot mark new data ready");
            pending.Receive(0,StationStreamFrame.Ready,epoch,3,[]);
            bool delivered=false;
            for(int i=0;i<8;i++){
                var side=pending.Connections[1];
                var next=pending.Stream.Next(side.Lease.LinkId,side.Lease.HostSide,side.Connection).Frame;
                if(next?.Type==StationStreamFrame.DataPacket){delivered=next.Value.Data.SequenceEqual(new byte[]{1,2,3});break;}
            }
            Expect(delivered,"the exact replay bytes reach their authenticated recipient");
            pending.Receive(1,StationStreamFrame.Ack,3,0,[]);
            Expect(pending.Stream.CurrentState==StationStreamSession.Synchronizing,"ACK alone cannot replace the last READY");
            pending.Receive(1,StationStreamFrame.Ready,epoch,0,[]);
            Expect(pending.Stream.CurrentState==StationStreamSession.Playing&&
                pending.Diagnostics().GetProperty("waitingReasons").GetArrayLength()==0,"all READY and drained windows resume together");
            pending.Issue(pending.People[0],"failed");
            Expect(pending.Stream.CurrentState==StationStreamSession.Unrecoverable&&Reason(pending.Diagnostics(),"unrecoverable"),
                "terminal protocol state remains explicit");
            pending.Issue(pending.People[0],"leave");
            pending.DetachAll();
            Expect(pending.Budget.UsedBytes==0,"human leave still releases an unrecoverable room");
        }
        Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks=localChecks,players=3,links=2,
            expectedAttachments=4,fixtureWaitMillis=300000,scope="actual C# hub/stream/replay; synthetic in-process identities; no TLS WAN Android gameplay or deployment"}));
    }
    private static bool Reason(JsonElement data,string reason)=>data.GetProperty("waitingReasons")
        .EnumerateArray().Any(value=>value.GetString()==reason);
    private sealed class Fixture:IDisposable
    {
        public readonly StationReplayBudget Budget=new();
        private readonly StationMultiplayerProfile profile=new("synthetic-three-player-game",new('a',64),"snes",
            "synthetic-three-player-engine",new('b',64),new('c',64),"synthetic-three-player-profile",
            new('d',64),4,true,"snes-multitap-port2-v1","battle",[2,3,4]);
        private readonly StationSessionSecurity security=new("rsa-pss-v1","synthetic-fixture-key");
        private readonly StationMultiplayer hub;private long clock;
        public readonly OnlineIdentity[] People=Enumerable.Range(1,3).Select(i=>
            new OnlineIdentity("synthetic-license-"+i,"synthetic-device-"+i)).ToArray();
        public JsonElement Room;public StationMultiplayerStream? Stream;
        public readonly List<(StationMultiplayer.Lease Lease,StationMultiplayerStream.Connection Connection)> Connections=[];
        public long Epoch=>Stream!.Snapshot().Epoch;
        public Fixture(){
            hub=new StationMultiplayer([profile],_=>"snes",Budget,()=>clock);
            Room=Issue(People[0],"create").GetProperty("room");
            foreach(var person in People.Skip(1))Room=Issue(person,"join").GetProperty("room");
            foreach(var person in People)Room=Issue(person,"ready",value:true).GetProperty("room");
        }
        public JsonElement Issue(OnlineIdentity person,string action,string? link=null,bool value=false,long? generation=null){
            var command=new StationMultiplayerCommand(action,Guid.NewGuid().ToString(),
                Room.ValueKind==JsonValueKind.Object?Room.GetProperty("roomId").GetString():null,
                profile.ItemId,profile.ContentSha256,profile.EngineId,profile.CoreSha256,profile.RuntimeSha256,
                profile.ProfileId,profile.ProfileSha256,3,generation??(Room.ValueKind==JsonValueKind.Object?
                Room.GetProperty("generation").GetInt64():0),link,value,ClientMaximumPlayers:5);
            return JsonSerializer.SerializeToElement(hub.Command(person,command,security),StrictJson.Options);
        }
        public void Advance(long delta)=>clock+=delta;
        public void Start()=>Room=Issue(People[0],"start").GetProperty("room");
        public JsonElement Diagnostics()=>Issue(People[0],"snapshot").GetProperty("room").GetProperty("barrierDiagnostics");
        public void AttachAll(){
            foreach(var link in Room.GetProperty("links").EnumerateArray()){
                string id=link.GetProperty("linkId").GetString()!;
                int guest=link.GetProperty("guestSlot").GetInt32()-1;
                foreach(var person in new[]{People[0],People[guest]}){
                    var ticket=Issue(person,"ticket",id).GetProperty("ticket").GetProperty("ticket").GetString()!;
                    var lease=hub.TakeTicket(ticket,_=>{});Stream=hub.Stream(lease);
                    Connections.Add((lease,Stream.Attach(lease.LinkId,lease.HostSide,lease.AttachmentId)));
                }
            }
        }
        public void Receive(int index,byte kind,long offset,long value,byte[] bytes){
            var side=Connections[index];Stream!.Receive(side.Lease.LinkId,side.Lease.HostSide,side.Connection,new(kind,offset,value,bytes));
        }
        public void HelloAndPauseAll(){
            for(int i=0;i<Connections.Count;i++){Receive(i,StationStreamFrame.Hello,0,0,[]);Receive(i,StationStreamFrame.Paused,Epoch,0,[]);}
        }
        public void Ready(int index)=>Receive(index,StationStreamFrame.Ready,Epoch,0,[]);
        public void DetachAll(){
            foreach(var side in Connections){Stream!.Detach(side.Lease.LinkId,side.Lease.HostSide,side.Connection);hub.Close(side.Lease);side.Connection.Stop.Dispose();}
            Connections.Clear();
        }
        public void Dispose(){
            if(Issue(People[0],"snapshot").GetProperty("room").ValueKind==JsonValueKind.Object)Issue(People[0],"leave");
            DetachAll();
        }
    }
}
