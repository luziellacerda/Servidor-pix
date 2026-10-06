using System.Text.Json;
using System.Text.RegularExpressions;
using TurboRamaSuiteOnlineServer.Online;

int checks=0;long now=1000;string hash=new('a',64),different=new('b',64);
var host=new OnlineIdentity("synthetic-host","host-device");
var guest=new OnlineIdentity("synthetic-guest","guest-device");
var other=new OnlineIdentity("synthetic-other","other-device");
StationOnline Hub()=>new([new OnlineEngine("engine","snes",hash,hash)],_=>"snes",()=>now,relayEnabled:true);
OnlineCommand Cmd(string action)=>new(action,Guid.NewGuid().ToString());
OnlineCommand Game(string action)=>Cmd(action) with{ItemId="synthetic-game",EngineId="engine",CoreSha256=hash,RuntimeSha256=hash,ContentSha256=hash,OptionsSha256=hash};
JsonElement Call(StationOnline hub,OnlineIdentity peer,OnlineCommand cmd)=>JsonSerializer.SerializeToElement(hub.Command(peer,cmd));
void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
void Fails(Action act,string code){try{act();throw new Exception("Accepted invalid operation: "+code);}catch(OnlineFailure e){Check(e.Code==code,"wrong failure: "+e.Code+" expected "+code);}}
void Enter(StationOnline hub,OnlineIdentity peer)=>Call(hub,peer,Cmd("enter") with{Nickname="Synthetic"});
var h=Hub();Enter(h,host);Enter(h,guest);Enter(h,other);
var made=Call(h,host,Game("create"));var room=made.GetProperty("room");string roomId=room.GetProperty("roomId").GetString()!,code=room.GetProperty("inviteCode").GetString()!,secret=room.GetProperty("connectionPassword").GetString()!;
Check(Regex.IsMatch(code,"^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{8}$"),"8 unambiguous characters");
Check(made.GetProperty("roomCapabilities")[0].GetString()=="short-invite-v1","capability advertised");
var resolve=Cmd("resolve-code") with{InviteCode=code};
var target=Call(h,guest,resolve);
Check(target.GetProperty("resolvedRoom").GetProperty("roomId").GetString()==roomId,"correct room resolved");
Check(target.GetProperty("resolvedRoom").GetProperty("itemId").GetString()=="synthetic-game","item returned for local preparation");
Check(!target.GetRawText().Contains(secret),"lookup reveals no room password");
Check(target.GetProperty("room").ValueKind==JsonValueKind.Null,"lookup does not join or mark ready");
Check(target.GetProperty("resolvedRoom").GetRawText()==Call(h,guest,resolve).GetProperty("resolvedRoom").GetRawText(),"same request id resolves idempotently");
Fails(()=>Call(h,guest,resolve with{InviteCode="ABCDEFGH"}),"STATION_ONLINE_REQUEST_REUSED");
var formatted=code[..4]+"-"+code[4..];
Check(Call(h,guest,Cmd("resolve-code") with{InviteCode=" "+formatted.ToLowerInvariant()+" "}).GetProperty("resolvedRoom").GetProperty("roomId").GetString()==roomId,"lowercase and grouped code accepted");
foreach(string? value in new string?[]{null,"","ABC","01234567","IIIIIIII","OOOOOOOO","ABCD--EFG","ABCD EFGH","ABCD-EFGH-extra","ABCDEFGH\nX"})
    Fails(()=>Call(h,guest,Cmd("resolve-code") with{InviteCode=value}),"STATION_ONLINE_CODE_INVALID");
string absent=code=="ABCDEFGH"?"BCDEFGHJ":"ABCDEFGH";
Fails(()=>Call(h,guest,Cmd("resolve-code") with{InviteCode=absent}),"STATION_ONLINE_CODE_NOT_FOUND");
Fails(()=>Call(h,new OnlineIdentity("outsider","device"),resolve with{RequestId=Guid.NewGuid().ToString()}),"STATION_ONLINE_ENTER_REQUIRED");
Fails(()=>Call(h,guest,Game("join") with{RoomId=roomId,RuntimeSha256=different}),"STATION_ONLINE_BUILD_MISMATCH");
var joined=Call(h,guest,Game("join") with{RoomId=roomId});
Check(joined.GetProperty("room").GetProperty("connectionPassword").GetString()==secret,"only joined member receives password automatically");
Check(joined.GetProperty("room").GetProperty("members").GetArrayLength()==2,"joined with original compatibility checks");
Fails(()=>Call(h,other,Cmd("resolve-code") with{InviteCode=code}),"STATION_ONLINE_ROOM_FULL");
Call(h,guest,Cmd("leave"));
Check(Call(h,other,Cmd("resolve-code") with{InviteCode=code}).GetProperty("resolvedRoom").GetProperty("roomId").GetString()==roomId,"code survives guest leaving waiting room");
var guestId=Call(h,guest,Cmd("heartbeat")).GetProperty("selfId").GetString();
Call(h,host,Cmd("block") with{PeerId=guestId});
Fails(()=>Call(h,guest,Cmd("resolve-code") with{InviteCode=code}),"STATION_ONLINE_BLOCKED");
Call(h,host,Cmd("leave"));
Fails(()=>Call(h,other,Cmd("resolve-code") with{InviteCode=code}),"STATION_ONLINE_CODE_NOT_FOUND");
now+=10001;
Enter(h,guest);var newRoom=Call(h,host,Game("create")).GetProperty("room");
string newCode=newRoom.GetProperty("inviteCode").GetString()!;
h.Revoke(host);
Fails(()=>Call(h,other,Cmd("resolve-code") with{InviteCode=newCode}),"STATION_ONLINE_CODE_NOT_FOUND");
var restarted=Hub();Enter(restarted,guest);
Fails(()=>Call(restarted,guest,Cmd("resolve-code") with{InviteCode=code}),"STATION_ONLINE_CODE_NOT_FOUND");
var rate=Hub();Enter(rate,guest);
for(int i=0;i<29;i++)Fails(()=>Call(rate,guest,Cmd("resolve-code") with{InviteCode=absent}),"STATION_ONLINE_CODE_NOT_FOUND");
Fails(()=>Call(rate,guest,Cmd("resolve-code") with{InviteCode=absent}),"STATION_ONLINE_RATE_LIMITED");
now+=10001;
Fails(()=>Call(rate,guest,Cmd("resolve-code") with{InviteCode=absent}),"STATION_ONLINE_CODE_NOT_FOUND");
var many=Hub();var codes=new HashSet<string>();
for(int i=0;i<256;i++){
    var peer=new OnlineIdentity("synthetic-"+i,"device-"+i);Enter(many,peer);
    var item=Call(many,peer,Game("create")).GetProperty("room");
    Check(codes.Add(item.GetProperty("inviteCode").GetString()!),"live code collision");
}
now+=60001;
Enter(many,guest);
Fails(()=>Call(many,guest,Cmd("resolve-code") with{InviteCode=codes.First()}),"STATION_ONLINE_CODE_NOT_FOUND");
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,syntheticRooms=256,productionTouched=false,androidGameplay=false}));
