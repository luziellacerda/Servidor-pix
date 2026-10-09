using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

static class CapacityChecks
{
    public static void Run(Action<bool,string> check)
    {
        var budget=new StationReplayBudget(128L*1024*1024);
        var profile=new StationMultiplayerProfile("capacity-game",new('a',64),"snes","capacity-engine",new('b',64),new('c',64),"capacity-profile",new('d',64),4,true,"snes-multitap-port2-v1","battle",[2,3,4]);
        var hub=new StationMultiplayer([profile],_=>"snes",budget,()=>0);
        var security=new StationSessionSecurity("rsa-pss-v1","synthetic-capacity-key");
        JsonElement Command(OnlineIdentity person,string action,JsonElement room=default,string? link=null,bool ready=false)
        {
            var command=new StationMultiplayerCommand(action,Guid.NewGuid().ToString(),room.ValueKind==JsonValueKind.Object?room.GetProperty("roomId").GetString():null,
                profile.ItemId,profile.ContentSha256,profile.EngineId,profile.CoreSha256,profile.RuntimeSha256,profile.ProfileId,profile.ProfileSha256,
                4,room.ValueKind==JsonValueKind.Object?room.GetProperty("generation").GetInt64():0,link,ready,ClientMaximumPlayers:5);
            return JsonSerializer.SerializeToElement(hub.Command(person,command,security),StrictJson.Options);
        }
        var rooms=new List<(OnlineIdentity Host,JsonElement Room,StationMultiplayerStream Stream,List<(StationMultiplayer.Lease Lease,StationMultiplayerStream.Connection Connection)> Peers)>();
        long forwarded=0;int participants=0;
        try
        {
            for(int number=0;number<80;number++)
            {
                var people=Enumerable.Range(0,4).Select(slot=>new OnlineIdentity("capacity-license-"+number+"-"+slot,"capacity-device-"+number+"-"+slot)).ToArray();
                var room=Command(people[0],"create").GetProperty("room");
                foreach(var person in people.Skip(1))room=Command(person,"join",room).GetProperty("room");
                foreach(var person in people)room=Command(person,"ready",room,ready:true).GetProperty("room");
                room=Command(people[0],"start",room).GetProperty("room");
                var peers=new List<(StationMultiplayer.Lease Lease,StationMultiplayerStream.Connection Connection)>();
                StationMultiplayerStream? stream=null;
                int guest=1;
                foreach(var link in room.GetProperty("links").EnumerateArray())
                {
                    string id=link.GetProperty("linkId").GetString()!;
                    foreach(var person in new[]{people[0],people[guest]})
                    {
                        string token=Command(person,"ticket",room,id).GetProperty("ticket").GetProperty("ticket").GetString()!;
                        var lease=hub.TakeTicket(token,_=>{});stream=hub.Stream(lease);
                        var connection=stream.Attach(lease.LinkId,lease.HostSide,lease.AttachmentId);peers.Add((lease,connection));
                    }
                    guest++;
                }
                rooms.Add((people[0],room,stream!,peers));participants+=4;
                foreach(var peer in peers)
                {
                    stream!.Receive(peer.Lease.LinkId,peer.Lease.HostSide,peer.Connection,new(StationStreamFrame.Hello,0,0,[]));
                    stream.Receive(peer.Lease.LinkId,peer.Lease.HostSide,peer.Connection,new(StationStreamFrame.Paused,1,0,[]));
                    stream.Receive(peer.Lease.LinkId,peer.Lease.HostSide,peer.Connection,new(StationStreamFrame.Ready,1,0,[]));
                }
                check(stream!.CurrentState==StationStreamSession.Playing,"capacity room global playing barrier");
            }
            check(participants==320&&budget.UsedBytes==120L*1024*1024,"320 synthetic peers fit the configured 128 MiB aggregate budget");
            byte[] payload=Enumerable.Range(0,StationStreamFrame.MaximumDataBytes).Select(i=>(byte)(i%251)).ToArray();
            foreach(var room in rooms)
                for(int at=0;at<room.Peers.Count;at+=2)
                    foreach(var direction in new[]{(at,at+1),(at+1,at)})
                    {
                        var sender=room.Peers[direction.Item1];var recipient=room.Peers[direction.Item2];
                        room.Stream.Receive(sender.Lease.LinkId,sender.Lease.HostSide,sender.Connection,new(StationStreamFrame.DataPacket,0,0,payload));
                        StationStreamFrame? next=null;
                        for(int n=0;n<6;n++){next=room.Stream.Next(recipient.Lease.LinkId,recipient.Lease.HostSide,recipient.Connection).Frame;if(next?.Type==StationStreamFrame.DataPacket)break;}
                        check(next?.Data.SequenceEqual(payload)==true,"exact isolated bidirectional data under populated room load");
                        room.Stream.Receive(recipient.Lease.LinkId,recipient.Lease.HostSide,recipient.Connection,new(StationStreamFrame.Ack,payload.Length,0,[]));forwarded+=payload.Length;
                    }
            check(rooms.All(r=>r.Stream.Snapshot().Pending==0),"no pending data across 80 populated rooms");
        }
        finally
        {
            foreach(var room in rooms)
            {
                Command(room.Host,"leave",room.Room);
                foreach(var peer in room.Peers)
                {
                    room.Stream.Detach(peer.Lease.LinkId,peer.Lease.HostSide,peer.Connection);hub.Close(peer.Lease);peer.Connection.Stop.Dispose();
                }
            }
        }
        check(budget.UsedBytes==0,"aggregate budget released after all owned rooms detach");
        Console.WriteLine(JsonSerializer.Serialize(new{passed=true,syntheticParticipants=participants,rooms=80,bidirectionalBytes=forwarded,maximumReplayBytes=budget.MaximumBytes,scope="in-process bounded hub/stream capacity; no WAN or Android gameplay"}));
    }
}
