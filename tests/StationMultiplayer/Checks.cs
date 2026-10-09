using System.Text.Json;
using System.Net.WebSockets;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

int checks=0;void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
CatalogChecks.Run(Check);
void Denied(Action action,string code){checks++;try{action();throw new Exception("Expected "+code);}catch(OnlineFailure e){if(e.Code!=code)throw new Exception("Expected "+code+" got "+e.Code);}}
string H(char c)=>new(c,64);
var security=new StationSessionSecurity("rsa-pss-v1","synthetic-key-only");
StationMultiplayerProfile Profile(int count=5,int[]? allowed=null)=>new("synthetic-game",H('a'),"snes","synthetic-engine",H('b'),H('c'),"synthetic-profile",H('d'),count,true,count>2?"snes-multitap-port2-v1":"standard-2p-v1","simultaneous",allowed??Enumerable.Range(2,Math.Max(0,count-1)).ToArray());
JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,StrictJson.Options);
StationMultiplayerCommand Cmd(string action,JsonElement room=default,int capacity=0,string? link=null,bool value=false,StationMultiplayerProfile? p=null)
{p??=Profile();return new(action,Guid.NewGuid().ToString(),room.ValueKind==JsonValueKind.Object?room.GetProperty("roomId").GetString():null,p.ItemId,p.ContentSha256,p.EngineId,p.CoreSha256,p.RuntimeSha256,p.ProfileId,p.ProfileSha256,capacity,room.ValueKind==JsonValueKind.Object?room.GetProperty("generation").GetInt64():0,link,value,ClientMaximumPlayers:5);}
(StationMultiplayer Hub,StationReplayBudget Budget,OnlineIdentity[] People,JsonElement Room) Make(int n,int? capacity=null,StationMultiplayerProfile? profile=null)
{
    profile??=Profile();long time=0;var budget=new StationReplayBudget();var hub=new StationMultiplayer([profile],_=>"snes",budget,()=>time+=100);
    var people=Enumerable.Range(0,n).Select(i=>new OnlineIdentity("synthetic-license-"+i,"synthetic-device-"+i)).ToArray();
    var room=Json(hub.Command(people[0],Cmd("create",capacity:capacity??n,p:profile),security)).GetProperty("room");
    for(int i=1;i<n;i++)room=Json(hub.Command(people[i],Cmd("join",room,p:profile),security)).GetProperty("room");
    foreach(var person in people)room=Json(hub.Command(person,Cmd("ready",room,value:true,p:profile),security)).GetProperty("room");
    return(hub,budget,people,room);
}
// Old clients must never receive a five-player profile they cannot parse.
var helpProfile=Profile() with{ModeTitle="Batalha",Instructions=["Escolha BATTLE GAME."],Sources=["https://docs.libretro.com/library/bsnes_mercury_performance/"]};
var negotiation=new StationMultiplayer([helpProfile,altForOld()],_=>"snes",new());
StationMultiplayerProfile altForOld()=>Profile(4) with{ProfileId="synthetic-four-compatible",ProfileSha256=H('e')};
var oldPerson=new OnlineIdentity("legacy-capacity","legacy-capacity");
var oldCap=Json(negotiation.Command(oldPerson,Cmd("capabilities") with{ClientMaximumPlayers=4},security));
Check(oldCap.GetProperty("maximumPlayers").GetInt32()==5&&oldCap.GetProperty("profiles").GetArrayLength()==1,"old client sees only its compatible profiles");
Denied(()=>negotiation.Command(oldPerson,Cmd("create",capacity:5) with{ClientMaximumPlayers=4},security),"STATION_MULTIPLAYER_CLIENT_UPDATE_REQUIRED");
Denied(()=>negotiation.Command(oldPerson,Cmd("capabilities") with{ClientMaximumPlayers=6},security),"STATION_MULTIPLAYER_CLIENT_CAPACITY_INVALID");
var newPerson=new OnlineIdentity("five-capacity","five-capacity");
var fiveCap=Json(negotiation.Command(newPerson,Cmd("capabilities"),security));
Check(fiveCap.GetProperty("profiles").GetArrayLength()==2,"new client receives both classifications");
var fiveRoom=Json(negotiation.Command(newPerson,Cmd("create",capacity:5),security)).GetProperty("room");
Check(fiveRoom.GetProperty("modeTitle").GetString()=="Batalha"&&fiveRoom.GetProperty("instructions").GetArrayLength()==1,"mode help is bound to exact room");
Check(Json(negotiation.Command(oldPerson,Cmd("snapshot") with{ClientMaximumPlayers=4},security)).GetProperty("totalRooms").GetInt32()==0,"old client cannot list an incompatible five-player mode");
Denied(()=>negotiation.Command(oldPerson,Cmd("join",fiveRoom) with{ClientMaximumPlayers=4},security),"STATION_MULTIPLAYER_CLIENT_UPDATE_REQUIRED");
foreach(var bad in new[]{Profile(6),Profile() with{ControllerProfile="standard-2p-v1"},Profile() with{Sources=["http://example.test/manual"]},Profile() with{Instructions=["line\nbreak"]}})
    Denied(()=>new StationMultiplayer([bad],_=>"snes",new()),"STATION_MULTIPLAYER_PROFILE_INVALID");
negotiation.Command(newPerson,Cmd("leave",fiveRoom),security);
var unknown=new StationMultiplayer([], _=>"snes",new());
Denied(()=>unknown.Command(new("u","d"),Cmd("create",capacity:2),security),"STATION_MULTIPLAYER_GAME_UNCLASSIFIED");
foreach(int n in new[]{1,2,3,4,5}){
    var profile=Profile(n);var hub=new StationMultiplayer([profile],_=>"snes",new());
    if(n==1)Denied(()=>hub.Command(new("u","d"),Cmd("create",capacity:2,p:profile),security),"STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED");
    else Denied(()=>hub.Command(new("u","d"),Cmd("create",capacity:n+1,p:profile),security),"STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED");
}
Denied(()=>new StationMultiplayer([Profile(),Profile()],_=>"snes",new()),"STATION_MULTIPLAYER_PROFILE_AMBIGUOUS");
var altProfile=Profile(2) with{ProfileId="synthetic-standard-profile",ProfileSha256=H('e')};
var variants=new StationMultiplayer([Profile(),altProfile],_=>"snes",new());
Check(Json(variants.Command(new("variant","variant"),Cmd("create",capacity:2,p:altProfile),security)).GetProperty("room").GetProperty("profileId").GetString()==altProfile.ProfileId,"explicit distinct profile selection");
var ambiguousLegacy=new StationMultiplayer([altProfile,altProfile with{ProfileId="synthetic-other-mode",ProfileSha256=H('f')}],_=>"snes",new());
Check(!ambiguousLegacy.LegacyAllowed("synthetic-game",H('a'),"synthetic-engine",H('b'),H('c')),"legacy cannot select between two approved profiles");
Denied(()=>new StationMultiplayer([Profile() with{AllowedPlayerCounts=[4,2]}],_=>"snes",new()),"STATION_MULTIPLAYER_PROFILE_INVALID");
var unapproved=new StationMultiplayer([Profile() with{Approved=false}],_=>"snes",new());
Denied(()=>unapproved.Command(new("u","d"),Cmd("create",capacity:2),security),"STATION_MULTIPLAYER_PROFILE_UNAPPROVED");
var wrongPlatform=new StationMultiplayer([Profile()],_=>"megadrive",new());
Denied(()=>wrongPlatform.Command(new("u","d"),Cmd("create",capacity:2),security),"STATION_MULTIPLAYER_PROFILE_UNAPPROVED");
Check(Json(wrongPlatform.Command(new("u","d"),Cmd("capabilities"),security)).GetProperty("profiles").GetArrayLength()==0,"mismatched platform is not advertised");
string? catalogHash=H('a');
var bound=new StationMultiplayer([altProfile],_=>"snes",new(),currentContentHash:_=>catalogHash);
Check(bound.LegacyAllowed("synthetic-game",H('a'),"synthetic-engine",H('b'),H('c')),"exact current content permits approved legacy profile");
Check(Json(bound.Command(new("bound","bound"),Cmd("capabilities",p:altProfile),security)).GetProperty("profiles").GetArrayLength()==1,"exact current content advertised");
foreach(var invalidHash in new string?[]{null,H('f')}){
    catalogHash=invalidHash;
    Denied(()=>bound.Command(new("bound","bound"),Cmd("create",capacity:2,p:altProfile),security),"STATION_MULTIPLAYER_PROFILE_UNAPPROVED");
    Check(!bound.LegacyAllowed("synthetic-game",H('a'),"synthetic-engine",H('b'),H('c')),"unknown or replaced content refuses legacy profile");
    Check(Json(bound.Command(new("bound","bound"),Cmd("capabilities",p:altProfile),security)).GetProperty("profiles").GetArrayLength()==0,"unknown or replaced content is not advertised");
}
catalogHash=H('a');
Check(Json(bound.Command(new("bound","bound"),Cmd("create",capacity:2,p:altProfile),security)).GetProperty("room").GetProperty("contentSha256").GetString()==catalogHash,"qualified content creates a bound room");
var twoFour=Make(3,4,Profile(4,[2,4]));
Denied(()=>twoFour.Hub.Command(twoFour.People[0],Cmd("start",twoFour.Room),security),"STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED");
var fourth=new OnlineIdentity("synthetic-fourth","synthetic-fourth");
var room4=Json(twoFour.Hub.Command(fourth,Cmd("join",twoFour.Room),security)).GetProperty("room");
Check(room4.GetProperty("roster").GetArrayLength()==4,"transient3 can reach4");
Denied(()=>twoFour.Hub.Command(twoFour.People[0],Cmd("start",room4),security),"STATION_MULTIPLAYER_NOT_READY");
foreach(var person in twoFour.People.Append(fourth))room4=Json(twoFour.Hub.Command(person,Cmd("ready",room4,value:true),security)).GetProperty("room");
Check(Json(twoFour.Hub.Command(twoFour.People[0],Cmd("start",room4),security)).GetProperty("room").GetProperty("links").GetArrayLength()==3,"2or4 profile starts4");

for(int players=2;players<=5;players++){
    var f=Make(players);var before=f.Room;var start=Cmd("start",before);
    Denied(()=>f.Hub.Command(f.People[1],start,security),"STATION_MULTIPLAYER_HOST_REQUIRED");
    var snapshot=Json(f.Hub.Command(f.People[0],start,security));var room=snapshot.GetProperty("room");
    Check(room.GetProperty("state").GetString()=="starting","starts starting");
    Check(room.GetProperty("generation").GetInt64()==before.GetProperty("generation").GetInt64()+1,"start generation");
    Check(room.GetProperty("links").GetArrayLength()==players-1,"N-1 links");
    Check(room.GetProperty("roster").EnumerateArray().Select(x=>x.GetProperty("slot").GetInt32()).SequenceEqual(Enumerable.Range(1,players)),"contiguous slots");
    Check(f.Budget.UsedBytes==(players-1)*2L*StationMultiplayer.WindowBytes,"budget exact");
    Check(Json(f.Hub.Command(f.People[0],start,security)).GetProperty("room").GetProperty("generation").GetInt64()==room.GetProperty("generation").GetInt64(),"idempotent start");
    Denied(()=>f.Hub.Command(f.People[0],start with{Value=true},security),"STATION_MULTIPLAYER_REQUEST_REUSED");
    Denied(()=>f.Hub.Command(new("overflow","overflow"),Cmd("join",room),security),"STATION_MULTIPLAYER_ROOM_FULL");
    Denied(()=>f.Hub.Command(f.People[0],Cmd("ready",before,value:true),security),"STATION_MULTIPLAYER_GENERATION_MISMATCH");
    var links=room.GetProperty("links").EnumerateArray().Select(x=>x.GetProperty("linkId").GetString()!).ToArray();
    if(players>2)Denied(()=>f.Hub.Command(f.People[1],Cmd("ticket",room,link:links[1]),security),"STATION_MULTIPLAYER_LINK_FORBIDDEN");
    Denied(()=>f.Hub.Command(f.People[0],Cmd("ticket",room,link:links[0]),StationSessionSecurity.Legacy),"STATION_REQUEST_PROOF_REQUIRED");
    var all=new List<(StationMultiplayer.Lease Lease,StationMultiplayerStream.Connection Connection)>();StationMultiplayerStream? stream=null;
    for(int at=0;at<links.Length;at++)foreach(bool host in new[]{true,false}){
        var who=f.People[host?0:at+1];var cmd=Cmd("ticket",room,link:links[at]);
        var t=Json(f.Hub.Command(who,cmd,security)).GetProperty("ticket");string token=t.GetProperty("ticket").GetString()!;
        Check(t.GetProperty("ownerSlot").GetInt32()==(host?1:at+2),"ticket owner slot");
        int proof=0;var lease=f.Hub.TakeTicket(token,_=>proof++);Check(proof==1,"proof validated");
        Denied(()=>f.Hub.TakeTicket(token,_=>{}),"STATION_MULTIPLAYER_TICKET_INVALID");
        stream=f.Hub.Stream(lease);var c=stream.Attach(lease.LinkId,lease.HostSide,lease.AttachmentId);all.Add((lease,c));
        stream.Receive(lease.LinkId,lease.HostSide,c,new(StationStreamFrame.Hello,0,0,[]));
    }
    var s=stream!;long epoch=s.Snapshot().Epoch;
    for(int i=0;i<all.Count;i++){
        var a=all[i];s.Receive(a.Lease.LinkId,a.Lease.HostSide,a.Connection,new(StationStreamFrame.Paused,epoch,0,[]));
        s.Receive(a.Lease.LinkId,a.Lease.HostSide,a.Connection,new(StationStreamFrame.Ready,epoch,0,[]));
        Check(s.CurrentState==(i==all.Count-1?StationStreamSession.Playing:StationStreamSession.Waiting),"global barrier waits all endpoints");
    }
    // Every guest owns an independent stream; bytes from one cannot appear on another.
    for(int i=0;i<links.Length;i++){
        var host=all[i*2];var guest=all[i*2+1];byte[] data=[(byte)(20+i),1,2,3];
        s.Receive(guest.Lease.LinkId,false,guest.Connection,new(StationStreamFrame.DataPacket,0,0,data));
        StationStreamFrame? got=null;
        for(int k=0;k<8;k++){var next=s.Next(host.Lease.LinkId,true,host.Connection);if(next.Frame?.Type==StationStreamFrame.DataPacket){got=next.Frame;break;}}
        Check(got is not null&&got.Value.Data.SequenceEqual(data),"per-link bytes");
        s.Receive(host.Lease.LinkId,true,host.Connection,new(StationStreamFrame.Ack,4,0,[]));
        s.Receive(guest.Lease.LinkId,false,guest.Connection,new(StationStreamFrame.DataPacket,0,0,data));
        Check(s.Snapshot().Pending==0,"duplicate delivered data is not replayed twice");
    }
    // A single guest loss pauses every host link and advances ONE shared epoch.
    var drop=all[1];s.Detach(drop.Lease.LinkId,false,drop.Connection);f.Hub.Close(drop.Lease);
    Check(s.CurrentState==StationStreamSession.Waiting&&s.Snapshot().Epoch==epoch+1,"global drop epoch");
    var survivor=all[0];s.Receive(survivor.Lease.LinkId,true,survivor.Connection,new(StationStreamFrame.Paused,epoch,0,[]));
    s.Receive(survivor.Lease.LinkId,true,survivor.Connection,new(StationStreamFrame.Ready,epoch,0,[]));
    Check(s.CurrentState==StationStreamSession.Waiting,"stale readiness ignored");
    var resume=Json(f.Hub.Command(f.People[1],Cmd("resume",room,link:links[0]),security)).GetProperty("ticket");
    var replacement=f.Hub.TakeTicket(resume.GetProperty("ticket").GetString()!,_=>{});
    var rc=s.Attach(replacement.LinkId,false,replacement.AttachmentId);all[1]=(replacement,rc);
    s.Receive(replacement.LinkId,false,rc,new(StationStreamFrame.Hello,4,0,[]));
    foreach(var a in all){s.Receive(a.Lease.LinkId,a.Lease.HostSide,a.Connection,new(StationStreamFrame.Paused,epoch+1,0,[]));
        s.Receive(a.Lease.LinkId,a.Lease.HostSide,a.Connection,new(StationStreamFrame.Ready,epoch+1,a.Lease.HostSide?0:4,[]));}
    Check(s.CurrentState==StationStreamSession.Playing,"all links resume together");
    // Duplicate host suspension on several links must not create several epochs.
    foreach(var a in all.Where(x=>x.Lease.HostSide))s.Receive(a.Lease.LinkId,true,a.Connection,new(StationStreamFrame.Suspend,0,0,[]));
    Check(s.Snapshot().Epoch==epoch+2,"host suspension coalesced by participant");
    f.Hub.Command(f.People[0],Cmd("leave",room),security);
    Check(f.Budget.UsedBytes>0,"budget retained until writers detached");
    foreach(var a in all){s.Detach(a.Lease.LinkId,a.Lease.HostSide,a.Connection);f.Hub.Close(a.Lease);}
    Check(f.Budget.UsedBytes==0,"budget released after final detach");
    Check(Json(f.Hub.Command(f.People[1],Cmd("snapshot"),security)).GetProperty("room").ValueKind==JsonValueKind.Null,"leave clears whole frozen room");
}
// During a global pause, a payload on one link must not require imaginary READY
// events on unchanged links. Cover every link and both directions for 2/3/4 people.
for(int playerCount=2;playerCount<=5;playerCount++)for(int touched=0;touched<playerCount-1;touched++)foreach(bool hostSender in new[]{false,true}){
    string[] linkNames=Enumerable.Range(0,playerCount-1).Select(i=>"ready-link-"+i).ToArray();var stream=new StationMultiplayerStream(linkNames);
    var endpoints=new StationMultiplayerStream.Connection[linkNames.Length*2];int sender=touched*2+(hostSender?0:1),receiver=sender^1;
    for(int i=0;i<endpoints.Length;i++){
        endpoints[i]=stream.Attach(linkNames[i/2],i%2==0,"ready-endpoint-"+i);
        stream.Receive(linkNames[i/2],i%2==0,endpoints[i],new(StationStreamFrame.Hello,0,0,[]));
        stream.Receive(linkNames[i/2],i%2==0,endpoints[i],new(StationStreamFrame.Paused,1,0,[]));
    }
    for(int i=0;i<endpoints.Length;i++)if(i!=sender)stream.Receive(linkNames[i/2],i%2==0,endpoints[i],new(StationStreamFrame.Ready,1,0,[]));
    Check(stream.CurrentState==StationStreamSession.Synchronizing,"partial barrier remains paused");
    stream.Receive(linkNames[touched],hostSender,endpoints[sender],new(StationStreamFrame.DataPacket,0,0,[7,8,9]));
    Check(!endpoints[sender].Ready&&!endpoints[receiver].Ready,"new bytes invalidate both touched endpoints");
    Check(endpoints.Where((_,i)=>i!=sender&&i!=receiver).All(c=>c.Ready),"untouched links retain their acknowledged readiness");
    stream.Receive(linkNames[touched],hostSender,endpoints[sender],new(StationStreamFrame.Ready,1,0,[]));
    Check(!endpoints[sender].Ready,"old sender watermark does not approve new bytes");
    stream.Receive(linkNames[touched],hostSender,endpoints[sender],new(StationStreamFrame.Ready,1,3,[]));
    stream.Receive(linkNames[touched],hostSender,endpoints[sender],new(StationStreamFrame.DataPacket,0,0,[7,8,9]));
    Check(endpoints[sender].Ready,"duplicate replay does not revoke valid readiness");
    bool delivered=false;for(int k=0;k<8;k++){var frame=stream.Next(linkNames[touched],!hostSender,endpoints[receiver]).Frame;if(frame?.Type==StationStreamFrame.DataPacket){delivered=frame.Value.Data.SequenceEqual(new byte[]{7,8,9});break;}}
    Check(delivered,"touched receiver obtains exact data");
    stream.Receive(linkNames[touched],!hostSender,endpoints[receiver],new(StationStreamFrame.Ack,3,0,[]));
    Check(stream.CurrentState==StationStreamSession.Synchronizing,"drained bytes alone cannot resume globally");
    stream.Receive(linkNames[touched],!hostSender,endpoints[receiver],new(StationStreamFrame.Ready,1,0,[]));
    Check(stream.CurrentState==StationStreamSession.Playing,"touched pair ready and every link drained resumes once");
}
var budgetOnly=new StationReplayBudget(524288);Check(budgetOnly.TryReserve("v2:a",524288),"v2 allocation");Check(!budgetOnly.TryReserve("v3:a",524288),"v2/v3 aggregate budget");budgetOnly.Release("v2:a");Check(budgetOnly.TryReserve("v3:a",524288),"reuse free budget");
int releases=0;var retiredV2=new StationStreamSession(32768,true,()=>releases++);
var retainedHost=retiredV2.Attach(true,"retained-host");var retainedGuest=retiredV2.Attach(false,"retained-guest");
retiredV2.RetireRoom();Check(releases==0,"v2 allocation retained while both writers detach");
retiredV2.Detach(true,retainedHost);Check(releases==0,"v2 allocation retained while guest writer detaches");
retiredV2.Detach(false,retainedGuest);Check(releases==1,"v2 allocation retired after final detach");
retiredV2.RetireRoom();Check(releases==1,"v2 allocation released once");
Check(Json(retiredV2.Diagnostics()).GetProperty("enabled").GetBoolean(),"existing v2 diagnostics preserved alongside shared budget");
var window=new StationStreamWindow(32768);for(int i=0;i<2;i++)window.Append(i*16384,new byte[16384]);
Denied(()=>window.Append(32768,[1]),"STATION_RECOVERY_WINDOW_EXCEEDED");window.Confirm(16384);window.Append(32768,[1]);Check(window.Pending==16385,"bounded credit refill");
var bytes=StationMultiplayerFrame.Encode(new(StationStreamFrame.DataPacket,0,0,[1,2,3]));Check(System.Text.Encoding.ASCII.GetString(bytes,0,4)=="TSR3","wire magic");Check(StationMultiplayerFrame.Decode(bytes).Data.Length==3,"wire decode");
bytes[3]=(byte)'2';Denied(()=>StationMultiplayerFrame.Decode(bytes),"STATION_MULTIPLAYER_FRAME_INVALID");
try{StationMultiplayerEndpoints.Parse(System.Text.Encoding.UTF8.GetBytes("{\"action\":\"snapshot\",\"action\":\"create\"}"));throw new Exception("duplicate accepted");}catch(JsonException){checks++;}
try{StationMultiplayerEndpoints.Parse(System.Text.Encoding.UTF8.GetBytes("{\"action\":\"snapshot\",\"requestId\":\"x\",\"peerId\":\"fake\"}"));throw new Exception("unknown accepted");}catch(JsonException){checks++;}
Check(!new StationMultiplayer([Profile()],_=>"snes",new()).LegacyAllowed("synthetic-game",H('a'),"synthetic-engine",H('b'),H('c')),"multitap cannot authorize legacy");
Check(new StationMultiplayer([Profile(2)],_=>"snes",new()).LegacyAllowed("synthetic-game",H('a'),"synthetic-engine",H('b'),H('c')),"exact standard2 legacy");
long visitorClock=0;var visitors=new StationMultiplayer([], _=>"snes",new(),()=>visitorClock);
for(int i=0;i<4100;i++){visitorClock+=61000;visitors.Command(new("historical-"+i,"historical-"+i),Cmd("snapshot"),security);}Check(true,"idle peers without room do not exhaust lifetime capacity");
// Turning on the capacity gate blocks new legacy admission, without deleting an active session.
bool legacyApproved=false;var oldEngine=new OnlineEngine("old-synthetic","snes",H('b'),H('c'),"station-stream.v2");
var old=new StationOnline([oldEngine],_=>"snes",relayEnabled:true,recoveryEnabled:true,legacyAdmission:(_,_,_,_,_)=>legacyApproved);
var oldHost=new OnlineIdentity("old-host","old-host");var oldGuest=new OnlineIdentity("old-guest","old-guest");
old.Command(oldHost,new("enter",Guid.NewGuid().ToString(),Nickname:"Old host"));old.Command(oldGuest,new("enter",Guid.NewGuid().ToString(),Nickname:"Old guest"));
OnlineCommand OldCreate()=>new("create",Guid.NewGuid().ToString(),ItemId:"synthetic-item",EngineId:oldEngine.Id,ContentSha256:H('a'),OptionsSha256:H('d'),CoreSha256:H('b'),RuntimeSha256:H('c'),RecoveryProtocol:"station-stream.v2");
Denied(()=>old.Command(oldHost,OldCreate()),"STATION_MULTIPLAYER_PROFILE_REQUIRED");legacyApproved=true;
var oldRoom=Json(old.Command(oldHost,OldCreate())).GetProperty("room");string oldRoomId=oldRoom.GetProperty("roomId").GetString()!;
old.Command(oldGuest,new("join",Guid.NewGuid().ToString(),RoomId:oldRoomId,ContentSha256:H('a'),OptionsSha256:H('d'),CoreSha256:H('b'),RuntimeSha256:H('c'),RecoveryProtocol:"station-stream.v2"));
foreach(var who in new[]{oldHost,oldGuest})old.Command(who,new("ready",Guid.NewGuid().ToString(),RoomId:oldRoomId,Value:true));
oldRoom=Json(old.Command(oldHost,new("start",Guid.NewGuid().ToString(),RoomId:oldRoomId,Transport:"relay-wss-v2"))).GetProperty("room");legacyApproved=false;
Check(Json(old.Command(oldHost,new("heartbeat",Guid.NewGuid().ToString()))).GetProperty("room").GetProperty("roomId").GetString()==oldRoomId,"gate preserves active legacy room");
var oldTicket=Json(old.Command(oldHost,new("relay-ticket",Guid.NewGuid().ToString(),RoomId:oldRoomId,Generation:oldRoom.GetProperty("generation").GetInt64(),EngineId:oldEngine.Id,ContentSha256:H('a'),OptionsSha256:H('d'),CoreSha256:H('b'),RuntimeSha256:H('c'),RecoveryProtocol:"station-stream.v2"),security));
Check(oldTicket.GetProperty("room").GetProperty("relay").GetProperty("ticket").GetString()!.Length==43,"gate preserves active legacy recovery ticket");
// The final CloseOutput must never overlap a data/control SendAsync.
var writerFixture=Make(2);var wr=Json(writerFixture.Hub.Command(writerFixture.People[0],Cmd("start",writerFixture.Room),security)).GetProperty("room");
var wt=Json(writerFixture.Hub.Command(writerFixture.People[0],Cmd("ticket",wr,link:wr.GetProperty("links")[0].GetProperty("linkId").GetString()),security)).GetProperty("ticket").GetProperty("ticket").GetString()!;
var wl=writerFixture.Hub.TakeTicket(wt,_=>{});using var ws=new CloseDuringSendSocket();
await new StationMultiplayerRelay(writerFixture.Hub,new SyntheticAccess(),NullLogger<StationMultiplayerRelay>.Instance).Attach(wl,ws,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
Check(ws.MaximumWriters==1&&ws.CloseCalls==1,"close serialized behind sender");
Check(writerFixture.Budget.UsedBytes==524288,"network close retains logical allocation");
writerFixture.Hub.Command(writerFixture.People[0],Cmd("leave",wr),security);Check(writerFixture.Budget.UsedBytes==0,"human leave releases retained empty stream");
// Proof canonicalization binds the credential AND exact v3 route, not the old relay path.
using(var key=RSA.Create(2048)){
    var proof=new StationRequestProof(TimeProvider.System);string credential=StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
    long stamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();string nonce=StationProtocol.Encode(RandomNumberGenerator.GetBytes(16));
    byte[] canonical=StationRequestProof.Canonical("GET",StationMultiplayer.RelayPath,[],credential,stamp,nonce);
    string header=$"v1.{stamp}.{nonce}."+StationProtocol.Encode(key.SignData(canonical,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
    string spki=StationProtocol.Encode(key.ExportSubjectPublicKeyInfo());
    try{proof.Verify(header,"rsa-pss-v1",spki,credential,"GET","/v1/station/online/relay",[]);throw new Exception("old route accepted");}catch(SuiteException e){Check(e.Code=="STATION_REQUEST_PROOF_INVALID","proof route binding");}
    proof.Verify(header,"rsa-pss-v1",spki,credential,"GET",StationMultiplayer.RelayPath,[]);checks++;
    try{proof.Verify(header,"rsa-pss-v1",spki,credential,"GET",StationMultiplayer.RelayPath,[]);throw new Exception("proof replay accepted");}catch(SuiteException e){Check(e.Code=="STATION_REQUEST_PROOF_REPLAY","proof replay rejected");}
}
// New HTTP identities must reuse the already authenticated social peer ID.
var linked=Make(2);var linkedSnapshot=Json(linked.Hub.Command(linked.People[0],Cmd("snapshot"),security));
Denied(()=>linked.Hub.Command(linked.People[0],Cmd("snapshot"),security,("different-id","Attacker")),"STATION_MULTIPLAYER_IDENTITY_CHANGED");
var chat=Json(linked.Hub.Command(linked.People[0],Cmd("chat",linked.Room) with{Text="synthetic room message"},security));
Check(chat.GetProperty("room").GetProperty("messages").GetArrayLength()==1,"member chat");
var outsider=Json(linked.Hub.Command(new("spectator","spectator"),Cmd("snapshot"),security));
Check(outsider.GetProperty("rooms")[0].GetProperty("messages").GetArrayLength()==0&&outsider.GetProperty("rooms")[0].GetProperty("connectionPassword").ValueKind==JsonValueKind.Null,"public list never exposes chat or password");
Denied(()=>linked.Hub.Command(new("intruder","intruder"),Cmd("chat",linked.Room) with{Text="intrusion"},security),"STATION_MULTIPLAYER_MEMBERSHIP_REQUIRED");
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="isolated synthetic C# hub/stream/codec/budget; no Android gameplay or deployment"}));

sealed class SyntheticAccess:IStationOnlineAccess
{
    public Task<StationSession?> Authenticate(string bearer,CancellationToken token)=>Task.FromResult<StationSession?>(null);
    public Task<bool> AuthorizeRelay(StationOnline.RelayLease lease,CancellationToken token)=>Task.FromResult(true);
    public object Sign(object payload)=>payload;
}
sealed class CloseDuringSendSocket:WebSocket
{
    private readonly TaskCompletionSource started=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebSocketState state=WebSocketState.Open;private int writers;
    public int MaximumWriters,CloseCalls;
    public override WebSocketCloseStatus? CloseStatus=>WebSocketCloseStatus.NormalClosure;
    public override string? CloseStatusDescription=>"";public override WebSocketState State=>state;public override string? SubProtocol=>StationMultiplayer.Protocol;
    public override void Abort()=>state=WebSocketState.Aborted;public override void Dispose()=>Abort();
    public override Task CloseAsync(WebSocketCloseStatus closeStatus,string? statusDescription,CancellationToken cancellationToken)=>CloseOutputAsync(closeStatus,statusDescription,cancellationToken);
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus,string? statusDescription,CancellationToken cancellationToken)
    {CloseCalls++;if(Interlocked.Increment(ref writers)!=1)throw new Exception("overlapping writer");MaximumWriters=Math.Max(MaximumWriters,writers);Interlocked.Decrement(ref writers);state=WebSocketState.Closed;return Task.CompletedTask;}
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken cancellationToken)
    {await started.Task.WaitAsync(cancellationToken);state=WebSocketState.CloseReceived;return new(0,WebSocketMessageType.Close,true);}
    public override async Task SendAsync(ArraySegment<byte> buffer,WebSocketMessageType messageType,bool endOfMessage,CancellationToken cancellationToken)
    {int n=Interlocked.Increment(ref writers);MaximumWriters=Math.Max(MaximumWriters,n);if(n!=1)throw new Exception("overlapping send");started.TrySetResult();try{await Task.Delay(Timeout.Infinite,cancellationToken);}finally{Interlocked.Decrement(ref writers);}}
}
