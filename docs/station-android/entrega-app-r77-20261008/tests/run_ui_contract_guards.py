"""Execute exact UI method/lambda excerpts in a host Java harness with explicit UI/HTTP stubs.

This does not instantiate Android views or qualify the real server. The actual profile,
player presentation and start-state classes are compiled unchanged beside the excerpts.
"""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parent
PACKAGE=Path('org/emulationstation/frontend/netplay')
JAVA=ROOT/'java/netplay-src'/PACKAGE
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
JSON=Path(r'E:\ESTUDO APK\work\turbostations-reconstruction-20261002\tools\json-20250517.jar')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def extract(text,signature):
    assert text.count(signature)==1,signature
    start=text.index(signature);opening=text.index('{',start);depth=0;state='code';i=opening
    while i<len(text):
        c=text[i];n=text[i+1:i+2]
        if state=='line':
            if c=='\n':state='code'
        elif state=='block':
            if c=='*' and n=='/':state='code';i+=1
        elif state in ['string','char']:
            if c=='\\':i+=1
            elif c==('"' if state=='string' else "'"):state='code'
        elif c=='/' and n=='/':state='line';i+=1
        elif c=='/' and n=='*':state='block';i+=1
        elif c=='"':state='string'
        elif c=="'":state='char'
        elif c=='{':depth+=1
        elif c=='}':
            depth-=1
            if not depth:return text[start:i+1]
        i+=1
    raise ValueError('Unterminated method '+signature)

HARNESS=r'''
package org.emulationstation.frontend.netplay;
import org.json.*;import java.util.*;import java.util.concurrent.atomic.AtomicReference;

public final class UIContractHarness {
 static int checks;static void check(boolean value,String message){checks++;if(!value)throw new AssertionError(message+" (#"+checks+")");}
 interface Checked{void run()throws Exception;}static void reject(Checked c,String message)throws Exception{boolean failed=false;try{c.run();}catch(Exception expected){failed=true;}check(failed,message);}
 boolean busy,launching,active=true;int epoch=1,muted;StationOnlineGame prepared;
 final AtomicReference<JSONObject> state=new AtomicReference<>();final StationOnlineClient client=new StationOnlineClient();
 final Label status=new Label();final Feedback feedback=new Feedback();Dialog codeDialog=new Dialog(),playerSheet;Button confirmStart;LinearLayout confirmationPlayers;
 final Map<String,Runnable> actions=new LinkedHashMap<>();String confirmationText="",cover="";Runnable acceptance,after;Operation pending;
 interface Operation{JSONObject run(StationApi.Cancellation cancel)throws Exception;}
 void action(Operation operation){action(operation,null);}void action(Operation operation,Runnable done){pending=operation;after=done;}
 JSONObject execute()throws Exception{Operation o=pending;pending=null;JSONObject result=o.run(new StationApi.Cancellation());if(after!=null)after.run();return result;}
 JSONObject room(){JSONObject snapshot=state.get();return snapshot==null?null:snapshot.optJSONObject("room");}
 void deliver(JSONObject value,int generation){if(generation==epoch)state.set(value);}
 void confirmWithCover(String title,String body,String positive,Runnable yes,String item){confirmationText=body;acceptance=yes;cover=item;}
 LinearLayout codePanel(String title,String game,String item){codeDialog=new Dialog();cover=item;return new LinearLayout();}
 LinearLayout vertical(){return new LinearLayout();}LinearLayout actionLine(LinearLayout parent){return new LinearLayout();}
 Label text(LinearLayout parent,String value,int size,int color){confirmationText+=value;return new Label();}
 Button roomAction(LinearLayout parent,String title,int icon,int style,Runnable operation){actions.put(title,operation);return new Button();}
 void renderParticipants(LinearLayout layout,JSONObject snapshot){}void displayCodePanel(){}void updateStartState(){}
 /*EXTRACTED*/
 static String[] compact(java.util.List<StationMultiplayerProfile> profiles){
 /*COMPACT*/
 return new String[]{b,d};
 }
 static boolean roomAvailable(JSONObject r,String self){/*AVAILABILITY*/return available;}
 static final String H="a".repeat(64);
 static JSONObject engine(){return new JSONObject().put("engineId","fixture-engine").put("platform","snes").put("coreSha256",H).put("runtimeSha256",H).put("options","");}
 static JSONObject profileRow(String mode,String id,int[] counts)throws Exception{
  return new JSONObject().put("approved",true).put("itemId","fixture-item").put("contentSha256",H).put("platform","snes").put("engineId","fixture-engine").put("coreSha256",H).put("runtimeSha256",H).put("profileId",id).put("profileSha256",StationOnlineGame.hex(java.security.MessageDigest.getInstance("SHA-256").digest(StationMultiplayerProfile.canonical("snes-multitap-port2-v1",new int[]{1,257,1,1,1},"").getBytes(java.nio.charset.StandardCharsets.UTF_8)))).put("controllerProfile","snes-multitap-port2-v1").put("mode",mode).put("maximumPlayers",4).put("allowedPlayerCounts",new JSONArray(counts));
 }
 static StationMultiplayerProfile profile(String mode,String id,int[] counts)throws Exception{return StationMultiplayerProfile.find(new JSONObject().put("multiplayerVersion",3).put("capability","station-multiplayer.v3").put("profiles",new JSONArray().put(profileRow(mode,id,counts))),"fixture-item",engine(),id);}
 static JSONObject copy(JSONObject value){return new JSONObject(value.toString());}
 static JSONObject room(StationMultiplayerProfile profile,int capacity,int people)throws Exception{
  JSONArray roster=new JSONArray();for(int i=1;i<=people;i++)roster.put(new JSONObject().put("peerId","p"+i).put("slot",i).put("nickname","Pessoa "+i).put("ready",true));
  return profile.fields(new JSONObject()).put("roomId","room-target").put("generation",17).put("platform","snes").put("mode",profile.mode).put("controllerProfile",profile.controllerProfile).put("maximumPlayers",4).put("allowedPlayerCounts",new JSONArray(profile.allowed)).put("capacity",capacity).put("players",people).put("hostId","p1").put("state","waiting").put("roster",roster).put("recoveryProtocol","station-stream.v3").put("transport","relay-wss-v3");
 }
 static JSONObject snapshot(JSONObject room){return new JSONObject().put("instance","fixture-instance").put("selfId","p1").put("room",room).put("transports",new JSONArray().put("relay-wss-v3"));}
 public static void main(String[]args)throws Exception{
  StationMultiplayerProfile profile=profile("battle-single","battle-fixture",new int[]{2,3,4});StationOnlineGame.profile=profile;
  // Exact command method: delayed execution must retain the room/generation displayed at click.
  UIContractHarness ui=new UIContractHarness();JSONObject original=room(profile,4,2);ui.state.set(snapshot(original));ui.command("ready","ready",true);
  ui.state.set(snapshot(copy(original).put("roomId","other-room").put("generation",99)));ui.execute();
  check(ui.client.sent.getString("roomId").equals("room-target"),"ready captured room");check(ui.client.sent.getLong("generation")==17,"ready captured generation");check(ui.client.sent.getBoolean("ready"),"ready field");
  JSONObject legacy=copy(original).put("recoveryProtocol","station-stream.v2");ui.state.set(snapshot(legacy));ui.command("ready","ready",false);ui.execute();check(!ui.client.sent.has("generation"),"legacy command unchanged");
  // Actual room adapter never treats maximum four as capacity three or substitutes default two.
  for(int capacity=2;capacity<=4;capacity++)for(int present=1;present<=capacity;present++){
   JSONObject r=room(profile,capacity,present);StationGamePlayerInfo.RoomInfo info=roomInfo(r,"p1");
   check(info.confirmed,"room information confirmed");check(info.capacity==capacity&&info.present==present&&info.vacancies==capacity-present,"capacity/presence exact");
   JSONObject noCapacity=copy(r);noCapacity.remove("capacity");check(!roomInfo(noCapacity,"p1").confirmed,"no capacity fallback");
   check(roomAvailable(r,"p1")== (present<capacity),"join availability uses exact vacancies");check(!roomAvailable(noCapacity,"p1"),"missing v3 capacity cannot default to maximum or two");
  }
  JSONObject c3=room(profile,3,2);c3.getJSONArray("roster").getJSONObject(1).put("slot",4);reject(()->profile.verify(c3),"slot4 invalid for capacity3 even maximum4");check(!roomInfo(c3,"p1").confirmed,"display rejects slot beyond capacity");
  check(!roomAvailable(c3,"p1"),"invalid slot disables join");JSONObject unclassified=room(profile,4,2);unclassified.remove("allowedPlayerCounts");check(!roomAvailable(unclassified,"p1"),"unclassified v3 disables join");
  check(!roomAvailable(room(profile,4,2).put("state","playing"),"p1"),"active match has no new seats");
  JSONObject legacyAvailability=new JSONObject().put("state","waiting").put("players",1).put("recoveryProtocol","station-stream.v2");check(roomAvailable(legacyAvailability,"p1"),"legacy two-seat fallback preserved");legacyAvailability.put("players",2);check(!roomAvailable(legacyAvailability,"p1"),"legacy full preserved");
  // Actual profile methods: exact game mode choice and no default two on missing profile.
  StationMultiplayerProfile team=profile("battle-team","team-fixture",new int[]{2,3,4});String[] compact=compact(Arrays.asList(profile,team));
  check(compact[0].equals("2, 3, 4 jogadores · 2 modos"),"compact distinct counts/modes");check(compact[1].contains("\n"),"two detail modes separated");
  StationMultiplayerProfile gaps=profile("battle-single","gap-fixture",new int[]{2,4});String[] gapText=compact(Collections.singletonList(gaps));check(gapText[0].equals("2, 4 jogadores"),"no invented 3");
  check(compact(Collections.emptyList())[0].contains("confirmar"),"no profiles pending not two");
  // Both entry confirmations execute actual join method and retain the target's mode/roster.
  for(boolean leaving:new boolean[]{false,true}){
   ui=new UIContractHarness();JSONObject target=room(profile,4,2);ui.client.target=target;
   JSONObject s=new JSONObject().put("selfId","p3").put("rooms",new JSONArray().put(target));
   if(leaving)s.put("room",room(profile,4,1).put("roomId","old-room"));ui.state.set(s);
   ui.join("room-target","fixture-item");check(ui.acceptance!=null,"entry acceptance exists");check(ui.confirmationText.contains("P1")&&ui.confirmationText.contains("P4"),"entry names and seats");check(ui.confirmationText.contains("Batalha individual")&&ui.confirmationText.contains("Multitap"),"entry mode and controller");check(ui.cover.equals("fixture-item"),"entry cover item");
   ui.acceptance.run();ui.execute();check(ui.client.sent.getString("action").equals("join")&&ui.client.sent.getLong("generation")==17,"join sends confirmed generation");check(ui.client.sent.getString("profileId").equals("battle-fixture"),"join sends mode identity");check(ui.client.leaves==(leaving?1:0),"leave only solo branch");
  }
  // Real joinConfirmed closure rejects a fresh generation before leaving the old room.
  UIContractHarness stale=new UIContractHarness();JSONObject target=room(profile,4,2);stale.client.target=target;
  stale.state.set(new JSONObject().put("selfId","p3").put("room",room(profile,4,1).put("roomId","old-room")).put("rooms",new JSONArray().put(target)));
  stale.join("room-target","fixture-item");stale.acceptance.run();stale.client.target=copy(target).put("generation",18);reject(stale::execute,"join refuses changed generation");check(stale.client.leaves==0,"changed target preserves own room");
  // Exact startDialog callbacks + actual start-state validation: execution transmits the same generation.
  ui=new UIContractHarness();ui.state.set(snapshot(room(profile,4,3)));ui.startDialog();check(ui.actions.containsKey("Iniciar"),"start available for three permitted players");ui.actions.get("Iniciar").run();ui.execute();check(ui.client.sent.getLong("generation")==17,"start generation sent");check(ui.client.sent.getString("transport").equals("relay-wss-v3"),"start v3 transport");
  for(String field:new String[]{"generation","capacity","profileId","roster"}){
   UIContractHarness changed=new UIContractHarness();JSONObject ready=room(profile,4,3);changed.state.set(snapshot(ready));changed.startDialog();changed.actions.get("Iniciar").run();JSONObject next=copy(ready);
   if(field.equals("generation"))next.put(field,18);else if(field.equals("capacity"))next.put(field,3);else if(field.equals("profileId"))next.put(field,"different");else next.getJSONArray("roster").getJSONObject(1).put("nickname","Changed name");
   changed.state.set(snapshot(next));reject(changed::execute,"start confirms "+field);check(changed.client.sent==null,"stale start not sent");
  }
  System.out.println("{\"checks\":"+checks+",\"passed\":true,\"actualExtractedMethods\":11}");
 }
}
class Label{void setText(String text){}}class Button{}class LinearLayout{Object tag;void setTag(Object v){tag=v;}Object getTag(){return tag;}void addView(Object view,Object params){}static class LayoutParams{LayoutParams(int w,int h){}}}
class Dialog{void dismiss(){}void setOnDismissListener(java.util.function.Consumer<Object> listener){}}
class Feedback{void fail(String message){}String text(JSONObject s,boolean busy){return "";}}
class StationSocialIcon{static final int BACK=1,PLAY=2;}class StationActionButton{static final int PRIMARY=1,SECONDARY=2;}
class StationApi{static class Cancellation{}}
class StationOnlineClient{
 JSONObject sent,target;int leaves;static JSONObject command(String action){return new JSONObject().put("action",action);}String name(String item){return "Fixture game";}
 JSONObject multiplayerRoom(String id){return target;}
 JSONObject call(JSONObject request,boolean ignored,StationApi.Cancellation cancel){sent=new JSONObject(request.toString());
  if(request.optString("action").equals("leave")){leaves++;return new JSONObject().put("selfId","p3");}
  if(request.optString("action").equals("join")){JSONObject joined=new JSONObject(target.toString());joined.getJSONArray("roster").put(new JSONObject().put("peerId","p3").put("slot",3).put("nickname","Pessoa 3").put("ready",false));return new JSONObject().put("selfId","p3").put("room",joined);}
  return new JSONObject();
 }
}
class StationPlayerModel{
 static boolean soloWaitingRoom(JSONObject snapshot){JSONObject r=snapshot.optJSONObject("room");return r!=null&&r.optString("state").equals("waiting")&&r.getJSONArray("roster").length()==1;}
 static boolean member(JSONObject room,String id){JSONArray people=room.getJSONArray("roster");for(int i=0;i<people.length();i++)if(id.equals(people.getJSONObject(i).optString("peerId")))return true;return false;}
}
class StationOnlineGame{
 static StationMultiplayerProfile profile;static class Unavailable extends Exception{Unavailable(String message){super(message);}}
 static StationOnlineGame prepare(Object context,StationOnlineClient client,String item,JSONObject snapshot,StationApi.Cancellation cancel,String profileId)throws Exception{if(!profile.profileId.equals(profileId))throw new Unavailable("Profile changed");return new StationOnlineGame();}
 void verifyRoom(JSONObject room)throws Exception{profile.verify(room);}JSONObject fields(JSONObject c){return profile.fields(c);}
 static String hex(byte[] value){StringBuilder s=new StringBuilder();for(byte b:value)s.append(String.format(java.util.Locale.ROOT,"%02x",b&255));return s.toString();}
}
'''

def main():
 p=argparse.ArgumentParser();p.add_argument('--work',type=Path,default=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\ui-contract-guards'));a=p.parse_args();assert a.work.drive.upper()=='E:'
 source=JAVA/'StationRoomsActivity.java';text=source.read_text('utf8');source_hash=sha(source)
 signatures=['private static StationGamePlayerInfo profileInfo(', 'private static StationGamePlayerInfo roomGameInfo(', 'private static StationGamePlayerInfo.RoomInfo roomInfo(', 'private String roomExplanation(', 'private void join(', 'private void joinConfirmed(', 'private void command(', 'private static boolean contains(', 'private static JSONObject findRoom(', 'private static String roomConfirmationKey(', 'private void startDialog(']
 methods=[extract(text,s) for s in signatures]
 start=text.index('StringBuilder brief=new StringBuilder(),detail=new StringBuilder();');end=text.index('                    runOnUiThread',start)
 compact=text[start:end]
 availability_start=text.index('StationGamePlayerInfo.RoomInfo seats=roomInfo(r,self);boolean available=')
 availability_end=text.index(';',text.index('boolean available=',availability_start))+1
 availability=text[availability_start:availability_end]
 harness=HARNESS.replace('/*EXTRACTED*/','\n'.join(methods)).replace('/*COMPACT*/',compact).replace('/*AVAILABILITY*/',availability)
 src=a.work/'src'/PACKAGE;src.mkdir(parents=True,exist_ok=True);classes=a.work/'classes';classes.mkdir(exist_ok=True)
 target=src/'UIContractHarness.java';target.write_text(harness,'utf8');hashes={'StationRoomsActivity.java':source_hash}
 for name in ['StationGamePlayerInfo.java','StationRoomStartState.java','StationMultiplayerProfile.java']:
  hashes[name]=sha(JAVA/name);shutil.copyfile(JAVA/name,src/name)
 def run(args,label):
  r=subprocess.run(list(map(str,args)),capture_output=True,text=True,encoding='utf8',timeout=60);(a.work/(label+'.log')).write_text(r.stdout+r.stderr,'utf8')
  if r.returncode:raise RuntimeError(r.stdout+r.stderr)
  return r.stdout
 run([JDK/'javac.exe','--release','17','-encoding','UTF-8','-cp',JSON,'-d',classes,*src.glob('*.java')],'compile')
 result=json.loads(run([JDK/'java.exe','-ea','-cp',str(classes)+';'+str(JSON),'org.emulationstation.frontend.netplay.UIContractHarness'],'tests').strip())
 for name,h in hashes.items():assert sha(JAVA/name)==h,'Source changed during tests '+name
 receipt={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':True,'metrics':result,'sourceHashes':hashes,'recipeSHA256':sha(__file__),'jsonJarSHA256':sha(JSON),'harnessSHA256':sha(target),'extractedMethods':{s:hashlib.sha256(m.encode()).hexdigest() for s,m in zip(signatures,methods)},'compactHeaderExcerptSHA256':hashlib.sha256(compact.encode()).hexdigest(),'roomAvailabilityExcerptSHA256':hashlib.sha256(availability.encode()).hexdigest(),'actualFullClasses':['StationGamePlayerInfo','StationRoomStartState','StationMultiplayerProfile'],'stubs':['Android layout/dialog callbacks','HTTP responses and deferred action executor','OnlineGame IO preparation (actual profile verification retained)','solo room/member lookup'],'limitations':['No Android view rendering or lifecycle verification','No real HTTP authentication, server, native gameplay or approval of fixture profiles','Methods and lambdas extracted verbatim; complete Activity is compiled in separate APK build']}
 (a.work/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8');(HERE/'ui-contract-guards-result.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8');print(json.dumps(receipt,indent=2))
if __name__=='__main__':main()
