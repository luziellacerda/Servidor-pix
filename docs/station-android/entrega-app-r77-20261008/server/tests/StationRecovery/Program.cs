using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

int checks=0;
void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
void Reject(Action action,string code){try{action();throw new Exception("Accepted "+code);}catch(OnlineFailure error){Check(error.Code==code,code);}}
string id()=>Guid.NewGuid().ToString();string hash=new('a',64);
using(var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"contract-vectors.json")))){
    using var vectorKey=RSA.Create();vectorKey.ImportSubjectPublicKeyInfo(StationProtocol.Decode(vectors.RootElement.GetProperty("publicKeySpkiBase64Url").GetString()!,4096),out _);
    foreach(var v in vectors.RootElement.GetProperty("requestProof").EnumerateArray()){
        var canonical=StationRequestProof.Canonical(v.GetProperty("method").GetString()!,v.GetProperty("target").GetString()!,(v.GetProperty("bodyBase64Url").GetString()!.Length==0?[]:StationProtocol.Decode(v.GetProperty("bodyBase64Url").GetString()!,8192)),v.GetProperty("syntheticCredential").GetString()!,v.GetProperty("timestamp").GetInt64(),v.GetProperty("nonce").GetString()!);
        Check(System.Text.Encoding.UTF8.GetString(canonical)==v.GetProperty("canonicalUTF8").GetString(),"public canonical vector");
        Check(vectorKey.VerifyData(canonical,StationProtocol.Decode(v.GetProperty("signatureBase64Url").GetString()!,512),HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"public RSA-PSS vector");
    }
    foreach(var v in vectors.RootElement.GetProperty("wire").EnumerateArray()){
        var bytes=Convert.FromHexString(v.GetProperty("frameHex").GetString()!);var frame=StationStreamFrame.Decode(bytes);
        Check(frame.Type==v.GetProperty("type").GetByte()&&frame.Offset==v.GetProperty("offset").GetInt64()&&frame.Value==v.GetProperty("value").GetInt64(),"public binary vector");
        Check(frame.Encode().SequenceEqual(bytes),"public binary encoding");
    }
}
var window=new StationStreamWindow(32768);
byte[] data=RandomNumberGenerator.GetBytes(16384);
window.Append(0,data);window.Append(0,data);Check(window.Accepted==data.Length,"duplicate upload not appended");
Check(window.Read(0).SequenceEqual(data),"unaltered stream bytes");
Reject(()=>window.Append(1,data),"STATION_RECOVERY_OFFSET_INVALID");
Reject(()=>window.Confirm(16385),"STATION_RECOVERY_OFFSET_INVALID");
var changed=data.ToArray();changed[0]^=1;Reject(()=>window.Append(0,changed),"STATION_RECOVERY_BYTES_MISMATCH");
window.Append(16384,data);Reject(()=>window.Append(32768,data),"STATION_RECOVERY_WINDOW_EXCEEDED");
window.Confirm(16384);window.Append(32768,data);Check(window.Pending==32768,"fixed ring storage after wrap");
Check(window.Read(16384).SequenceEqual(data),"ring replay intact");
foreach(byte type in Enumerable.Range(1,13).Select(x=>(byte)x)){
    var frame=new StationStreamFrame(type,123,0,type==3?data:[]);var decoded=StationStreamFrame.Decode(frame.Encode());
    Check(decoded.Type==type&&decoded.Offset==123&&decoded.Data.SequenceEqual(frame.Data),"frame roundtrip");
}
Reject(()=>StationStreamFrame.Decode(new byte[24]),"STATION_RECOVERY_FRAME_INVALID");
var corrupt=new StationStreamFrame(1,0,0,[]).Encode();corrupt[7]=1;Reject(()=>StationStreamFrame.Decode(corrupt),"STATION_RECOVERY_FRAME_INVALID");

var stream=new StationStreamSession(32768);var a=stream.Attach(true,"A");var b=stream.Attach(false,"B");
void Receive(bool host,StationStreamSession.Connection c,byte type,long offset=0,long value=0,byte[]? bytes=null)=>stream.Receive(host,c,new(type,offset,value,bytes??[]));
List<StationStreamFrame> Drain(bool host,StationStreamSession.Connection c){List<StationStreamFrame> result=[];while(stream.Next(host,c).Frame is {} f)result.Add(f);return result;}
Receive(true,a,1);Receive(false,b,1);Drain(true,a);Drain(false,b);
long epoch=stream.Snapshot().Epoch;Receive(true,a,7,epoch);Receive(false,b,7,epoch);
Receive(true,a,8,epoch);Check(stream.CurrentState!=StationStreamSession.Playing,"one ready never resumes");
Receive(false,b,8,epoch);Check(stream.CurrentState==StationStreamSession.Playing,"both pause and empty-stream barrier");
Receive(true,a,3,0,bytes:data);var received=Drain(false,b).Single(f=>f.Type==3);
Check(received.Data.SequenceEqual(data),"real byte stream queued for guest");
// Native write succeeded but ACK was lost with the guest socket. HELLO advances delivery once.
stream.Detach(false,b);Check(stream.CurrentState==StationStreamSession.Waiting,"transport loss retains stream");
b=stream.Attach(false,"B2");Receive(false,b,1,value:data.Length);
Check(stream.Snapshot().Pending==0,"lost native-write ack reconciled by HELLO");
Check(Drain(false,b).All(f=>f.Type!=3),"already delivered bytes not replayed");
epoch=stream.Snapshot().Epoch;Receive(true,a,7,epoch);Receive(false,b,7,epoch);
Receive(true,a,8,epoch,value:data.Length);Receive(false,b,8,epoch);
Check(stream.CurrentState==StationStreamSession.Playing,"stream recovery requires both participants");
Receive(false,b,11,epoch);Check(stream.CurrentState==StationStreamSession.Waiting,"background keeps logical match");
epoch=stream.Snapshot().Epoch;Receive(true,a,7,epoch);Receive(false,b,7,epoch);Receive(true,a,8,epoch,data.Length);Receive(false,b,8,epoch);
Check(stream.CurrentState==StationStreamSession.Waiting,"background cannot resume");
Receive(false,b,12,epoch);Check(stream.CurrentState==StationStreamSession.Playing,"foreground rejoins verified barrier");
stream.Detach(true,a);a=stream.Attach(true,"A2");Receive(true,a,1,offset:data.Length);
Receive(false,b,3,0,bytes:data);received=Drain(true,a).Single(f=>f.Type==3);
stream.Detach(true,a);a=stream.Attach(true,"A3");Receive(true,a,1,offset:data.Length);
received=Drain(true,a).Single(f=>f.Type==3);Check(received.Offset==0&&received.Data.SequenceEqual(data),"undelivered bytes replay exactly");
Receive(true,a,4,data.Length);Check(stream.Snapshot().Pending==0,"replay reclaimed only after native ack");
stream.Detach(true,a);stream.Detach(false,b);Check(stream.Snapshot().Connections==0&&stream.Snapshot().Accepted==32768,"both offline retain bounded stream");

long now=0;var hub=new StationOnline([new("new-runtime","snes",hash,hash,"station-stream.v2")],_=>"snes",()=>now,relayEnabled:true,recoveryEnabled:true);
OnlineIdentity host=new("synthetic-A","device-A"),guest=new("synthetic-B","device-B");
using var rsa=RSA.Create(2048);var security=new StationSessionSecurity("rsa-pss-v1",StationProtocol.Encode(rsa.ExportSubjectPublicKeyInfo()));
JsonElement Command(OnlineIdentity who,OnlineCommand cmd)=>JsonSerializer.SerializeToElement(hub.Command(who,cmd,security));
Command(host,new("enter",id(),Nickname:"Synthetic A"));Command(guest,new("enter",id(),Nickname:"Synthetic B"));
var room=Command(host,new("create",id(),ItemId:"synthetic",EngineId:"new-runtime",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2")).GetProperty("room");
string rid=room.GetProperty("roomId").GetString()!;
Command(guest,new("join",id(),RoomId:rid,ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2"));
Command(host,new("ready",id(),RoomId:rid,Value:true));Command(guest,new("ready",id(),RoomId:rid,Value:true));
room=Command(host,new("start",id(),RoomId:rid,Transport:"relay-wss-v2")).GetProperty("room");
long generation=room.GetProperty("generation").GetInt64();
OnlineCommand Ticket(string action)=>new(action,id(),RoomId:rid,EngineId:"new-runtime",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash,RecoveryProtocol:"station-stream.v2",Generation:generation);
room=Command(host,Ticket("relay-ticket")).GetProperty("room");string ticket=room.GetProperty("relay").GetProperty("ticket").GetString()!;
Reject(()=>hub.TakeRelayTicket(ticket),"STATION_REQUEST_PROOF_REQUIRED");
var lease=hub.TakeRelayTicket(ticket,_=>{});Check(lease.Protocol=="station-stream.v2","v2 credential scoped to protocol");
Reject(()=>hub.TakeRelayTicket(ticket,_=>{}),"STATION_ONLINE_RELAY_TICKET_INVALID");
hub.CloseRelay(lease);now=180000;
room=Command(host,new("heartbeat",id())).GetProperty("room");
Check(room.GetProperty("roomId").GetString()==rid&&room.GetProperty("members").GetArrayLength()==2,"membership survives 60/120 seconds, both absent");
Check(room.GetProperty("generation").GetInt64()==generation,"transport loss never changes game generation");
room=Command(host,Ticket("resume-relay")).GetProperty("room");
var lease2=hub.TakeRelayTicket(room.GetProperty("relay").GetProperty("ticket").GetString()!,_=>{});
hub.CloseRelay(lease);Check(hub.RelayCurrent(lease2),"stale finalizer cannot close new attachment");
hub.CloseRelay(lease2);
Reject(()=>Command(host,Ticket("resume-relay") with{Generation=generation+1}),"STATION_RECOVERY_GENERATION_MISMATCH");
Reject(()=>Command(host,Ticket("resume-relay") with{RuntimeSha256=new string('b',64)}),"STATION_ONLINE_BUILD_MISMATCH");
room=Command(host,Ticket("resume-relay")).GetProperty("room");
var terminalLease=hub.TakeRelayTicket(room.GetProperty("relay").GetProperty("ticket").GetString()!,_=>{});
hub.RecoveryState(terminalLease,"unrecoverable");hub.CloseRelay(terminalLease);now+=180000;
Check(Command(host,new("heartbeat",id())).GetProperty("room").GetProperty("state").GetString()=="unrecoverable","social expiry cannot make terminal state recoverable");
Reject(()=>Command(host,Ticket("resume-relay")),"STATION_RECOVERY_UNRECOVERABLE");
hub.Revoke(guest);Check(Command(host,new("heartbeat",id())).GetProperty("room").ValueKind==JsonValueKind.Null,"revocation still ends authorized membership");
// Own-room names must remain authoritative even when neither member is in the social page.
var paged=new StationOnline([new("legacy-names","snes",hash,hash)],_=>"snes",()=>0L,relayEnabled:true);
JsonElement PageCommand(OnlineIdentity who,OnlineCommand cmd)=>JsonSerializer.SerializeToElement(paged.Command(who,cmd));
OnlineIdentity namesHost=new("synthetic-names-host","device-names-host"),namesGuest=new("synthetic-names-guest","device-names-guest");
var hostView=PageCommand(namesHost,new("enter",id(),Nickname:"Synthetic host"));
var guestView=PageCommand(namesGuest,new("enter",id(),Nickname:"Synthetic guest"));
for(int i=0;i<105;i++)PageCommand(new("synthetic-page-"+i,"device-page-"+i),new("enter",id(),Nickname:"Synthetic peer "+i));
var namesRoom=PageCommand(namesHost,new("create",id(),ItemId:"synthetic",EngineId:"legacy-names",ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash)).GetProperty("room");
string namesRid=namesRoom.GetProperty("roomId").GetString()!;
PageCommand(namesGuest,new("join",id(),RoomId:namesRid,ContentSha256:hash,OptionsSha256:hash,CoreSha256:hash,RuntimeSha256:hash));
PageCommand(namesGuest,new("ready",id(),RoomId:namesRid,Value:true));
foreach(var who in new[]{namesHost,namesGuest}){
    var view=PageCommand(who,new("heartbeat",id(),Page:40));
    Check(view.GetProperty("peers").GetArrayLength()==0,"members absent from selected social page");
    Check(view.GetProperty("roomCapabilities").EnumerateArray().Any(x=>x.GetString()=="own-room-member-profiles-v1"),"own-room profile capability");
    var own=view.GetProperty("room");var profiles=own.GetProperty("memberProfiles").EnumerateArray().ToArray();
    Check(profiles.Length==2&&profiles.Select(x=>x.GetProperty("peerId").GetString()).SequenceEqual(own.GetProperty("members").EnumerateArray().Select(x=>x.GetString())),"profile IDs match complete membership");
    Check(profiles.Single(x=>x.GetProperty("peerId").GetString()==hostView.GetProperty("selfId").GetString()).GetProperty("nickname").GetString()=="Synthetic host","host name independent of social page");
    Check(profiles.Single(x=>x.GetProperty("peerId").GetString()==guestView.GetProperty("selfId").GetString()).GetProperty("nickname").GetString()=="Synthetic guest","guest name independent of social page");
    Check(own.GetProperty("ready").GetArrayLength()==1,"profile extension preserves ready IDs");
}
Console.WriteLine(JsonSerializer.Serialize(new{passed=true,checks,scope="Production v2 ledger/state/negotiation with synthetic identities; not Android gameplay"}));
