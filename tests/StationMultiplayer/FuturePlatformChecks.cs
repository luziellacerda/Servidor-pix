using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

static class FuturePlatformChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(StationMultiplayerPlatformPolicy.ServerReady("ps2")&&StationMultiplayerPlatformPolicy.ServerReady("PlayStation 2 - BR")
            &&StationMultiplayerPlatformPolicy.ServerReady("Sega Saturn"),"PS2 and Saturn server policies and aliases");
        check(StationMultiplayerPlatformPolicy.MaximumPlayers("ps2")==2&&StationMultiplayerPlatformPolicy.MaximumPlayers("saturn")==2,
            "PS2 and Saturn retain maintainer two-player ceilings");
        foreach(string platform in new[]{"dreamcast","gamecube","wii","wiiu","switch","ps2","saturn"})
        for(int players=2;players<=StationMultiplayerPlatformPolicy.MaximumPlayers(platform);players++)
        {
            var budget=new StationReplayBudget();
            var profile=new StationMultiplayerProfile("fixture-"+platform,new('a',64),platform,"fixture-native-engine",new('b',64),new('c',64),"fixture-native-layout",new('d',64),players,true,"fixture-native-controls","local-multiplayer",Enumerable.Range(2,players-1).ToArray());
            var hub=new StationMultiplayer([profile],_=>platform,budget,()=>0);
            var security=new StationSessionSecurity("rsa-pss-v1","fixture-not-a-real-device-key");
            var people=Enumerable.Range(0,players).Select(i=>new OnlineIdentity(platform+"-license-"+i,platform+"-device-"+i)).ToArray();
            JsonElement Command(OnlineIdentity who,string action,JsonElement room=default,string? link=null,bool ready=false)
            {
                var command=new StationMultiplayerCommand(action,Guid.NewGuid().ToString(),room.ValueKind==JsonValueKind.Object?room.GetProperty("roomId").GetString():null,
                    profile.ItemId,profile.ContentSha256,profile.EngineId,profile.CoreSha256,profile.RuntimeSha256,profile.ProfileId,profile.ProfileSha256,
                    players,room.ValueKind==JsonValueKind.Object?room.GetProperty("generation").GetInt64():0,link,ready,ClientMaximumPlayers:5);
                return JsonSerializer.SerializeToElement(hub.Command(who,command,security),StrictJson.Options);
            }
            var room=Command(people[0],"create").GetProperty("room");
            foreach(var person in people.Skip(1))room=Command(person,"join",room).GetProperty("room");
            foreach(var person in people)room=Command(person,"ready",room,ready:true).GetProperty("room");
            room=Command(people[0],"start",room).GetProperty("room");
            check(room.GetProperty("roster").GetArrayLength()==players&&room.GetProperty("links").GetArrayLength()==players-1,"future native roster/link count "+platform);
            var endpoints=new List<(StationMultiplayer.Lease Lease,StationMultiplayerStream.Connection Connection)>();
            StationMultiplayerStream? stream=null;
            int guest=1;
            foreach(var link in room.GetProperty("links").EnumerateArray())
            {
                string id=link.GetProperty("linkId").GetString()!;
                foreach(var person in new[]{people[0],people[guest]})
                {
                    string ticket=Command(person,"ticket",room,id).GetProperty("ticket").GetProperty("ticket").GetString()!;
                    var lease=hub.TakeTicket(ticket,_=>{});stream=hub.Stream(lease);
                    endpoints.Add((lease,stream.Attach(lease.LinkId,lease.HostSide,lease.AttachmentId)));
                }
                guest++;
            }
            foreach(var e in endpoints)
            {
                stream!.Receive(e.Lease.LinkId,e.Lease.HostSide,e.Connection,new(StationStreamFrame.Hello,0,0,[]));
                stream.Receive(e.Lease.LinkId,e.Lease.HostSide,e.Connection,new(StationStreamFrame.Paused,1,0,[]));
                stream.Receive(e.Lease.LinkId,e.Lease.HostSide,e.Connection,new(StationStreamFrame.Ready,1,0,[]));
            }
            check(stream!.CurrentState==StationStreamSession.Playing,"future native global start "+platform);
            byte[] payload=Enumerable.Range(0,16).Select(i=>(byte)(i+players)).ToArray();
            for(int i=0;i<endpoints.Count;i++)
            {
                var sender=endpoints[i];var receiver=endpoints[i^1];
                stream.Receive(sender.Lease.LinkId,sender.Lease.HostSide,sender.Connection,new(StationStreamFrame.DataPacket,0,0,payload));
                StationStreamFrame? data=null;
                for(int j=0;j<8;j++){data=stream.Next(receiver.Lease.LinkId,receiver.Lease.HostSide,receiver.Connection).Frame;if(data?.Type==StationStreamFrame.DataPacket)break;}
                check(data?.Data.SequenceEqual(payload)==true,"future native bidirectional bytes "+platform);
                stream.Receive(receiver.Lease.LinkId,receiver.Lease.HostSide,receiver.Connection,new(StationStreamFrame.Ack,payload.Length,0,[]));
            }
            check(stream.Snapshot().Pending==0,"future native all bytes acknowledged "+platform);
            var dropped=endpoints[1];stream.Detach(dropped.Lease.LinkId,false,dropped.Connection);hub.Close(dropped.Lease);dropped.Connection.Stop.Dispose();
            long epoch=stream.Snapshot().Epoch;
            check(epoch==2&&stream.CurrentState==StationStreamSession.Waiting,"future native disconnect pauses all links "+platform);
            // A real deserialize/reload must also preserve native-engine rooms.
            hub.ReplaceProfiles(JsonSerializer.Deserialize<StationMultiplayerProfile[]>(JsonSerializer.Serialize(new[]{profile},StrictJson.Options),StrictJson.Options)!);
            var resumed=Command(people[1],"resume",room,dropped.Lease.LinkId).GetProperty("ticket");
            var replacement=hub.TakeTicket(resumed.GetProperty("ticket").GetString()!,_=>{});
            var connection=stream.Attach(replacement.LinkId,false,replacement.AttachmentId);endpoints[1]=(replacement,connection);
            stream.Receive(replacement.LinkId,false,connection,new(StationStreamFrame.Hello,payload.Length,payload.Length,[]));
            foreach(var e in endpoints)
            {
                stream.Receive(e.Lease.LinkId,e.Lease.HostSide,e.Connection,new(StationStreamFrame.Paused,epoch,0,[]));
                stream.Receive(e.Lease.LinkId,e.Lease.HostSide,e.Connection,new(StationStreamFrame.Ready,epoch,payload.Length,[]));
            }
            check(stream.CurrentState==StationStreamSession.Playing,"future native synchronized resume after profile reload "+platform);
            Command(people[0],"leave",room);
            foreach(var e in endpoints){stream.Detach(e.Lease.LinkId,e.Lease.HostSide,e.Connection);hub.Close(e.Lease);e.Connection.Stop.Dispose();}
            check(budget.UsedBytes==0&&JsonSerializer.SerializeToElement(hub.Snapshot()).GetProperty("activeRooms").GetInt32()==0,"future native room and budget cleanup "+platform);
        }
    }
}
