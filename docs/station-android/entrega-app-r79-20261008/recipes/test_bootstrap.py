"""Exercise actual optional-discovery method with controlled API and UI callbacks."""
from pathlib import Path
import subprocess,json,hashlib
ROOT=Path(__file__).resolve().parent.parent
WORK=Path(r'E:\ESTUDO APK\work\station-room-bootstrap-r79-20261008\bootstrap-tests')
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
source=ROOT/'java/netplay-src/org/emulationstation/frontend/netplay/StationRoomsActivity.java'
text=source.read_text('utf8');start=text.index('    private void beginMultiplayerDiscovery(');end=text.index('    private void deliver(',start);method=text[start:end]
guards=[text.index('deliver(current,generation);beginMultiplayerDiscovery')>0,
 'state.get()==null' not in text[text.index('    private void loadHero(){'):text.index('    private void loadHeroProfile(')],
 'substring' not in text[text.index('    private void loadHero(){'):text.index('    private void loadHeroProfile(')],
 'stopMultiplayerPoll();cancelAll();' in text,
 'if(active&&epoch==generation){roomLayout(room()!=null);loadHero();}' in text]
assert all(guards)
harness='''import java.util.*;import java.util.concurrent.*;import java.io.*;
class StationApi {static final class Cancellation {boolean stopped;void cancel(){stopped=true;}boolean cancelled(){return stopped;}void check()throws IOException{if(stopped)throw new IOException("cancelled");}}}
class StationOnlineClient {boolean multiplayerEnabled;int scenario;CountDownLatch entered=new CountDownLatch(1),release=new CountDownLatch(1);StationOnlineClient(int s){scenario=s;}
 void discoverMultiplayer(StationApi.Cancellation c)throws Exception{entered.countDown();if(scenario>=3)release.await();if(scenario==1)throw new IOException("fixture outage");if(scenario==4)c.cancel();multiplayerEnabled=scenario==2;c.check();}
 Object compose(Object ignored){return new Object();}Object pollMultiplayer(StationApi.Cancellation c){return new Object();}}
public class BootstrapProbe {
 boolean active=true,busy,creating;int epoch=1;StationOnlineClient client;String notice="unset";int delivered,failures,hero;
 Set<StationApi.Cancellation> requests=Collections.synchronizedSet(new HashSet<>());ScheduledExecutorService multiplayerEvents=Executors.newSingleThreadScheduledExecutor();ScheduledFuture<?> multiplayerPolling;
 CountDownLatch painted=new CountDownLatch(1);void runOnUiThread(Runnable r){r.run();}void showCreateMessage(String s,boolean error){notice=s;}void loadHero(){hero++;painted.countDown();}void deliver(Object o,int g){if(active&&g==epoch)delivered++;}void recordFailure(Throwable error){failures++;}
'''+method+'''
 static int checks;static void ok(boolean b){checks++;if(!b)throw new AssertionError("check "+checks);}
 static void awaitDone(BootstrapProbe p)throws Exception{p.multiplayerEvents.submit(()->{}).get(2,TimeUnit.SECONDS);}
 public static void main(String[] args)throws Exception{
  for(int scenario=0;scenario<=4;scenario++){BootstrapProbe p=new BootstrapProbe();p.client=new StationOnlineClient(scenario);
   try{p.beginMultiplayerDiscovery(p.client,1);ok(p.client.entered.await(2,TimeUnit.SECONDS));
    if(scenario==3){p.epoch++;p.client.release.countDown();awaitDone(p);ok(p.hero==0&&p.delivered==0);}
    else if(scenario==4){p.client.release.countDown();awaitDone(p);ok(p.hero==0&&p.failures==0);}
    else{ok(p.painted.await(2,TimeUnit.SECONDS));awaitDone(p);ok(p.hero==1);ok(p.delivered==(scenario==1?0:1));ok(p.failures==(scenario==1?1:0));ok(p.notice.isEmpty()==(scenario==2));}
    ok(p.requests.isEmpty());
   }finally{p.multiplayerEvents.shutdownNow();}}
  BootstrapProbe stale=new BootstrapProbe();stale.client=new StationOnlineClient(0);
  try{stale.beginMultiplayerDiscovery(stale.client,0);ok(stale.multiplayerPolling==null);stale.active=false;stale.beginMultiplayerDiscovery(stale.client,1);ok(stale.multiplayerPolling==null);}
  finally{stale.multiplayerEvents.shutdownNow();}
  System.out.println("PASS "+checks+" discovery lifecycle checks; controlled API/UI fixtures");
 }
}
'''
WORK.mkdir(parents=True,exist_ok=True);java=WORK/'BootstrapProbe.java';java.write_text(harness,'utf8')
for cmd in [[str(JDK/'javac.exe'),'-encoding','UTF-8','-d',str(WORK),str(java)],[str(JDK/'java.exe'),'-cp',str(WORK),'BootstrapProbe']]:
 r=subprocess.run(cmd,capture_output=True,text=True,encoding='utf8',timeout=20)
 if r.returncode:raise RuntimeError(r.stdout+r.stderr)
 print(r.stdout,end='')
(ROOT/'evidence/bootstrap-tests.json').write_text(json.dumps(dict(passed=True,sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),actualMethodSHA256=hashlib.sha256(method.encode()).hexdigest(),result=r.stdout.strip(),staticGuards=len(guards),scope='Actual discovery method, controlled API/UI callbacks. Not Android/HTTP/gameplay validation.'),indent=2)+'\n','utf8')
