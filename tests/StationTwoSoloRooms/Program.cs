using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new InvalidOperationException(message);}
JsonElement View(object result)=>JsonSerializer.SerializeToElement(result);
var hash=new string('a',64);
var hub=new StationOnline([new OnlineEngine("engine","snes",hash,hash)],_=>"snes",relayEnabled:true);
var host=new OnlineIdentity("fixture-first","fixture-first-device");
var poco=new OnlineIdentity("fixture-second","fixture-second-device");
OnlineCommand Command(string action)=>new(action,Guid.NewGuid().ToString());
OnlineCommand Prepare(string action)=>Command(action) with{ItemId="fixture-item",EngineId="engine",
    ContentSha256=hash,CoreSha256=hash,RuntimeSha256=hash,OptionsSha256=hash};
foreach(var identity in new[]{host,poco})hub.Command(identity,Command("enter") with{Nickname=identity.LicenseId});
var first=View(hub.Command(host,Prepare("create")));
var second=View(hub.Command(poco,Prepare("create")));
string target=first.GetProperty("room").GetProperty("roomId").GetString()!;
string own=second.GetProperty("room").GetProperty("roomId").GetString()!;
Check(target!=own,"two create actions produce two separate rooms");
var ready=View(hub.Command(poco,Command("ready") with{RoomId=own,Value=true}));
Check(ready.GetProperty("room").GetProperty("roomId").GetString()==own&&
    ready.GetProperty("room").GetProperty("members").GetArrayLength()==1,"ready confirms the solo room and does not join another");
try{hub.Command(host,Command("start") with{RoomId=target,Transport="relay-wss-v1"});throw new Exception("solo start accepted");}
catch(OnlineFailure e){Check(e.Code=="STATION_ONLINE_NOT_READY","host cannot start alone");}
try{hub.Command(poco,Prepare("join") with{RoomId=target});throw new Exception("duplicate membership accepted");}
catch(OnlineFailure e){Check(e.Code=="STATION_ONLINE_ALREADY_IN_ROOM","must leave the separate solo room before joining");}
var left=View(hub.Command(poco,Command("leave") with{RoomId=own}));
Check(left.GetProperty("room").ValueKind==JsonValueKind.Null,"real departure acknowledged");
var joined=View(hub.Command(poco,Prepare("join") with{RoomId=target}));
Check(joined.GetProperty("room").GetProperty("roomId").GetString()==target&&
    joined.GetProperty("room").GetProperty("members").GetArrayLength()==2,"second participant enters the actual first room");
var current=View(hub.Command(host,Command("heartbeat")));
Check(current.GetProperty("room").GetProperty("members").GetArrayLength()==2,"host sees both actual participants");
Check(current.GetProperty("rooms").GetArrayLength()==1,"unused second room closed");
hub.Command(host,Command("ready") with{RoomId=target,Value=true});
hub.Command(poco,Command("ready") with{RoomId=target,Value=true});
var started=View(hub.Command(host,Command("start") with{RoomId=target,Transport="relay-wss-v1"}));
Check(started.GetProperty("room").GetProperty("state").GetString()=="starting","two confirmations permit the host start");
hub.Command(host,Command("host-listening") with{RoomId=target});
Check(View(hub.Command(poco,Command("heartbeat"))).GetProperty("room").GetProperty("state").GetString()=="connecting",
    "guest launch released only after real host readiness");
hub.Command(poco,Command("leave") with{RoomId=target});
Check(View(hub.Command(host,Command("heartbeat"))).GetProperty("room").ValueKind==JsonValueKind.Null,"closing active fixture clears membership");
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="Isolated production room logic; no PostgreSQL, real licenses or Android gameplay"}));
