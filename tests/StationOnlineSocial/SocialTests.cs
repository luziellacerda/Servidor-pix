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
Console.WriteLine($"PASS {checks} server social checks");
