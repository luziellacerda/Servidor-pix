using System.Text.Json;using TurboRamaSuiteOnlineServer.Online;
long now=1000;int checks=0;string hash=new('a',64);var engine=new OnlineEngine("snes","snes",hash,hash);
StationOnline Hub(bool social=true)=>new([engine],id=>id=="game"?"snes":null,()=>now,relayEnabled:true,socialEnabled:social);
OnlineIdentity a=new OnlineIdentity("a","a"),b=new OnlineIdentity("b","b"),c=new OnlineIdentity("c","c");
OnlineCommand Cmd(string action)=>new(action,Guid.NewGuid().ToString());
JsonElement View(object o)=>JsonSerializer.SerializeToElement(o);
void Check(bool pass,string name){checks++;if(!pass)throw new Exception(name);}
void Fails(Action action,string code){try{action();throw new Exception("Expected "+code);}catch(OnlineFailure f){Check(f.Code==code,"code "+f.Code+" != "+code);}}
JsonElement Call(StationOnline h,OnlineIdentity id,OnlineCommand command)=>View(h.Command(id,command));
JsonElement Enter(StationOnline h,OnlineIdentity id,string nick)=>Call(h,id,Cmd("enter") with{Nickname=nick});
JsonElement Heart(StationOnline h,OnlineIdentity id)=>Call(h,id,Cmd("heartbeat"));
JsonElement Create(StationOnline h,OnlineIdentity id)=>Call(h,id,Cmd("create") with{ItemId="game",EngineId="snes",CoreSha256=hash,RuntimeSha256=hash,ContentSha256=hash,OptionsSha256=hash});
var h=Hub();var aa=Enter(h,a,"Alice");string aid=aa.GetProperty("selfId").GetString()!;string bid=Enter(h,b,"Bruno").GetProperty("selfId").GetString()!;Enter(h,c,"Cris");
Check(aa.GetProperty("socialCapabilities").GetArrayLength()==2,"capabilities");
var dm=Cmd("direct-chat") with{PeerId=bid,Text="Olá, vamos jogar?"};var sent=Call(h,a,dm);
Check(sent.GetProperty("room").ValueKind==JsonValueKind.Null,"chat without room");
Check(Heart(h,b).GetProperty("directMessages").GetArrayLength()==1,"recipient receives");Check(Heart(h,c).GetProperty("directMessages").GetArrayLength()==0,"third party cannot read");
Call(h,a,dm);Check(Heart(h,b).GetProperty("directMessages").GetArrayLength()==1,"idempotent message");
Fails(()=>Call(h,a,dm with{Text="changed"}),"STATION_ONLINE_REQUEST_REUSED");Fails(()=>Call(h,a,Cmd("direct-chat") with{PeerId=bid,Text="fast"}),"STATION_ONLINE_CHAT_LIMIT");
now+=1100;Fails(()=>Call(h,a,Cmd("direct-chat") with{PeerId=bid,Text=new string('a',501)}),"STATION_ONLINE_TEXT_INVALID");
Fails(()=>Call(h,a,Cmd("direct-chat") with{PeerId=aid,Text="self"}),"STATION_ONLINE_PEER_NOT_FOUND");
string room=Create(h,a).GetProperty("room").GetProperty("roomId").GetString()!;
Call(h,b,Cmd("request-join") with{RoomId=room});var host=Heart(h,a);string request=host.GetProperty("joinRequests")[0].GetProperty("requestId").GetString()!;
Check(host.GetProperty("room").GetProperty("members").GetArrayLength()==1,"request does not join");
Check(Heart(h,c).GetProperty("joinRequests").GetArrayLength()==0,"request private to host");
Fails(()=>Call(h,b,Cmd("request-join") with{RoomId=room}),"STATION_ONLINE_REQUEST_EXISTS");Fails(()=>Call(h,c,Cmd("accept-request") with{Text=request}),"STATION_ONLINE_ROOM_NOT_FOUND");
Call(h,a,Cmd("accept-request") with{Text=request});var invited=Heart(h,b);Check(invited.GetProperty("invites").GetArrayLength()==1,"approval creates real invitation");
Call(h,b,Cmd("join") with{RoomId=room,CoreSha256=hash,RuntimeSha256=hash,ContentSha256=hash,OptionsSha256=hash});var joined=Heart(h,b);Check(joined.GetProperty("room").GetProperty("members").GetArrayLength()==2,"two members");Check(joined.GetProperty("invites").GetArrayLength()==0,"consumed invite removed");
Call(h,a,Cmd("ready") with{RoomId=room,Value=true});Call(h,b,Cmd("ready") with{RoomId=room,Value=true});Call(h,a,Cmd("start") with{RoomId=room,Transport="relay-wss-v1"});Check(Heart(h,b).GetProperty("room").GetProperty("state").GetString()=="starting","relay launch retained");
Call(h,a,Cmd("leave"));now+=1100;Call(h,b,Cmd("block") with{PeerId=aid});Check(Heart(h,b).GetProperty("directMessages").GetArrayLength()==0,"blocked history removed");Fails(()=>Call(h,a,Cmd("direct-chat") with{PeerId=bid,Text="blocked"}),"STATION_ONLINE_BLOCKED");
var off=Hub(false);Enter(off,a,"Alice");var ob=Enter(off,b,"Bruno");Check(Heart(off,a).GetProperty("socialCapabilities").GetArrayLength()==0,"flag off default-compatible");Fails(()=>Call(off,a,Cmd("direct-chat") with{PeerId=ob.GetProperty("selfId").GetString(),Text="x"}),"STATION_ONLINE_SOCIAL_DISABLED");
h=Hub();aid=Enter(h,a,"Alice").GetProperty("selfId").GetString()!;bid=Enter(h,b,"Bruno").GetProperty("selfId").GetString()!;
for(int i=0;i<40;i++){now+=1200;Call(h,a,Cmd("direct-chat") with{PeerId=bid,Text="message "+i});if(i%10==0)Heart(h,b);}
Check(Heart(h,b).GetProperty("directMessages").GetArrayLength()==32,"bounded inbox");
room=Create(h,a).GetProperty("room").GetProperty("roomId").GetString()!;Call(h,b,Cmd("request-join") with{RoomId=room});request=Heart(h,a).GetProperty("joinRequests")[0].GetProperty("requestId").GetString()!;
now+=30000;Heart(h,a);Heart(h,b);now+=30001;Heart(h,a);Heart(h,b);Check(Heart(h,a).GetProperty("joinRequests").GetArrayLength()==0,"requests expire");Fails(()=>Call(h,a,Cmd("accept-request") with{Text=request}),"STATION_ONLINE_JOIN_REQUEST_NOT_FOUND");
h.Revoke(b);Check(Heart(h,a).GetProperty("totalPeers").GetInt32()==1,"revocation removes presence");
// Worst supported page/history, including JSON escaping, must fit Android's 512 KiB envelope.
now=1000;
var manyEngines=Enumerable.Range(0,32).Select(i=>new OnlineEngine(new string('界',62)+i.ToString("D2"),new string('界',120),hash,hash)).ToArray();
var wide=new StationOnline(manyEngines,_=>manyEngines[0].Platform,()=>now,relayEnabled:true,socialEnabled:true);
var people=Enumerable.Range(0,102).Select(i=>new OnlineIdentity("license"+i,"device"+i)).ToArray();
var peerIds=people.Select(p=>Enter(wide,p,new string('界',24)).GetProperty("selfId").GetString()!).ToArray();
OnlineCommand WideCreate()=>Cmd("create") with{ItemId=new string('a',64),EngineId=manyEngines[0].Id,CoreSha256=hash,RuntimeSha256=hash,ContentSha256=hash,OptionsSha256=hash};
string wideRoom=Call(wide,people[0],WideCreate()).GetProperty("room").GetProperty("roomId").GetString()!;
var otherRooms=people.Skip(2).Select(p=>Call(wide,p,WideCreate()).GetProperty("room").GetProperty("roomId").GetString()!).ToArray();
for(int i=0;i<50;i++){
    now+=1100;
    Call(wide,people[0],Cmd("chat") with{RoomId=wideRoom,Text=new string('界',500)});
    if(i>=18)Call(wide,people[1],Cmd("direct-chat") with{PeerId=peerIds[0],Text=new string('界',498)+i.ToString("D2")});
}
for(int i=0;i<20;i++){
    Call(wide,people[i+2],Cmd("request-join") with{RoomId=wideRoom});
    Call(wide,people[i+2],Cmd("invite") with{RoomId=otherRooms[i],PeerId=peerIds[0]});
}
for(int i=0;i<5;i++)Call(wide,people[0],Cmd("request-join") with{RoomId=otherRooms[i]});
var crowded=Heart(wide,people[0]);
Check(crowded.GetProperty("room").GetProperty("messages").GetArrayLength()==50,"full shared history");
var boundedHistory=crowded.GetProperty("directMessages");
Check(boundedHistory.GetArrayLength() is >0 and <=32 &&
    boundedHistory[boundedHistory.GetArrayLength()-1].GetProperty("text").GetString()!.EndsWith("49"),"newest complete private messages retained");
Check(JsonSerializer.SerializeToUtf8Bytes(boundedHistory).Length<=65536,"private history byte budget");
Check(!boundedHistory[0].TryGetProperty("encodedBytes",out _),"internal byte cost not exposed");
Check(crowded.GetProperty("joinRequests").GetArrayLength()==20 && crowded.GetProperty("sentJoinRequests").GetArrayLength()==5,"maximum request inbox/outbox");
Check(crowded.GetProperty("invites").GetArrayLength()==20,"maximum invitation inbox");
Fails(()=>Call(wide,people[22],Cmd("request-join") with{RoomId=wideRoom}),"STATION_ONLINE_INVITE_LIMIT");
Fails(()=>Call(wide,people[0],Cmd("request-join") with{RoomId=otherRooms[5]}),"STATION_ONLINE_INVITE_LIMIT");
var payload=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,domain="TurboRamaStationAndroid/online/v1",productId="TURBORAMA_STATION_ANDROID",applicationId="TURBORAMA_STATION_ANDROID",licenseId=new string('a',64),deviceId=new string('a',43),sessionId=new string('a',32),requestId=Guid.NewGuid().ToString(),snapshot=crowded});
int envelopeBytes=JsonSerializer.SerializeToUtf8Bytes(new{keyId=new string('a',64),payload=Convert.ToBase64String(payload),signature=new string('a',684)}).Length;
Console.WriteLine($"Maximum fixture payload {payload.Length} bytes, envelope {envelopeBytes} bytes");
Check(envelopeBytes<=524288,"full social snapshot fits actual Android envelope limit");
Console.WriteLine($"PASS {checks} server social checks; maximum fixture payload {payload.Length} bytes, envelope {envelopeBytes} bytes");
