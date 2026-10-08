"""Exercise real R77 tunnel callbacks/locks with an in-memory WebSocket transport boundary."""
from pathlib import Path
import argparse,hashlib,json,subprocess
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parent.parent
PACKAGE='org/emulationstation/frontend/netplay'
JDK=Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
WORK=Path(r'E:\ESTUDO APK\work\station-multiplayer-r77-20261008\tunnel-concurrency-tests')
STUBS={
'org/emulationstation/frontend/relay/ws/WebSocketImpl.java':'''package org.emulationstation.frontend.relay.ws;public class WebSocketImpl {public final java.util.Queue<java.nio.ByteBuffer> outQueue=new java.util.concurrent.ConcurrentLinkedQueue<>();}''',
'org/emulationstation/frontend/relay/ws/handshake/ServerHandshake.java':'''package org.emulationstation.frontend.relay.ws.handshake;public interface ServerHandshake {String getFieldValue(String name);}''',
'org/emulationstation/frontend/relay/ws/protocols/Protocol.java':'''package org.emulationstation.frontend.relay.ws.protocols;public class Protocol {public Protocol(String protocol){}}''',
'org/emulationstation/frontend/relay/ws/drafts/Draft_6455.java':'''package org.emulationstation.frontend.relay.ws.drafts;public class Draft_6455 {public Draft_6455(java.util.List<?> a,java.util.List<?> b,int n){}}''',
'org/emulationstation/frontend/relay/ws/client/WebSocketClient.java':'''package org.emulationstation.frontend.relay.ws.client;
import java.net.*;import java.util.*;import java.nio.*;import javax.net.ssl.*;import org.emulationstation.frontend.relay.ws.*;import org.emulationstation.frontend.relay.ws.drafts.*;import org.emulationstation.frontend.relay.ws.handshake.*;
public abstract class WebSocketClient {
 private boolean open=true;private final WebSocketImpl connection=new WebSocketImpl();public final List<byte[]> sent=new ArrayList<>();
 public WebSocketClient(URI uri,Draft_6455 draft,Map<String,String> headers,int timeout){}
 public void setSocketFactory(SSLSocketFactory value){}public void setTcpNoDelay(boolean value){}public void setConnectionLostTimeout(int value){}public void setDaemon(boolean value){}public void connect(){}
 public boolean isOpen(){return open;}public boolean isClosed(){return !open;}public Object getConnection(){return connection;}
 public void send(byte[] value){if(!open)throw new IllegalStateException("closed");sent.add(value.clone());}public void closeConnection(int code,String reason){open=false;}
 protected void onSetSSLParameters(SSLParameters parameters){}public abstract void onOpen(ServerHandshake handshake);public abstract void onMessage(String text);public abstract void onMessage(ByteBuffer data);public abstract void onClose(int code,String reason,boolean peer);public abstract void onError(Exception error);
}'''}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,default=WORK);p.add_argument('--observe-baseline',action='store_true');a=p.parse_args()
 source=a.work/'src';source.mkdir(parents=True,exist_ok=True);hashes={}
 for name in ['StationMultiplayerTunnel.java','StationMultiplayerWire.java']:
  original=ROOT/'java/netplay-src'/PACKAGE/name;dest=source/PACKAGE/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(original.read_bytes());hashes[name]=sha(original)
 for name,content in STUBS.items():
  dest=source/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(content,'utf8')
 template=Path(__file__).parent/'StationMultiplayerTunnelConcurrencyTest.java.in';(source/PACKAGE/'StationMultiplayerTunnelConcurrencyTest.java').write_bytes(template.read_bytes())
 classes=a.work/'classes';classes.mkdir(exist_ok=True)
 cp=subprocess.run([str(JDK/'javac.exe'),'--release','17','-encoding','UTF-8','-d',str(classes),*[str(n) for n in source.rglob('*.java')]],capture_output=True,text=True,encoding='utf8');(a.work/'compile.log').write_text(cp.stdout+cp.stderr,'utf8')
 if cp.returncode:raise RuntimeError(cp.stdout+cp.stderr)
 run=subprocess.run([str(JDK/'java.exe'),'-cp',str(classes),'org.emulationstation.frontend.netplay.StationMultiplayerTunnelConcurrencyTest'],capture_output=True,text=True,encoding='utf8',timeout=30);(a.work/'test.log').write_text(run.stdout+run.stderr,'utf8')
 if run.returncode:raise RuntimeError(run.stdout+run.stderr)
 metrics=json.loads(run.stdout.strip().splitlines()[-1]);receipt={'utc':datetime.now(timezone.utc).isoformat(),'sources':hashes,'templateSHA256':sha(template),'recipeSHA256':sha(__file__),'metrics':metrics,'passed':metrics['failures']==0,'realCode':['Complete StationMultiplayerTunnel and StationMultiplayerWire without edits'],'models':['WebSocket library IO/TLS boundary is stubbed; Remote callbacks and concurrent pump execute unchanged'],'limits':['No real sockets, Android, C# or gameplay','Detects callbacks owning gate via Thread.holdsLock; does not deliberately leave deadlocked processes']};(a.work/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n','utf8');print(run.stdout,end='')
 if metrics['failures'] and not a.observe_baseline:raise RuntimeError('R77 concurrency regressions remain: '+str(metrics))
if __name__=='__main__':main()
