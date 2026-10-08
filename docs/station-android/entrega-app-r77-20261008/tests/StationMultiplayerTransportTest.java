package org.emulationstation.frontend.netplay;
import org.emulationstation.frontend.station.StationRecoveryProofTest;
import org.emulationstation.frontend.relay.ws.client.WebSocketClient;
import org.json.*;
import javax.net.ssl.*;
import java.io.*;import java.net.*;import java.nio.charset.StandardCharsets;import java.security.*;import java.security.cert.*;import java.security.spec.*;import java.util.*;import java.util.concurrent.*;import java.util.concurrent.atomic.*;import java.lang.reflect.*;

/** Real Java Session/Tunnel/WebSocket and C# endpoints. Native emulation is a controlled TCP fixture. */
public final class StationMultiplayerTransportTest {
 static int checks;static long bytesVerified;static Properties fixture;static SSLSocketFactory tls;static URI endpoint;static JSONObject room;static int count;
 static final ExecutorService io=Executors.newCachedThreadPool(r->{Thread t=new Thread(r,"Lab-IO");t.setDaemon(true);return t;});
 static final List<Peer> peers=new ArrayList<>();static final ConcurrentMap<Integer,Integer> sourceSlots=new ConcurrentHashMap<>();
 static void check(boolean value,String label){checks++;if(!value)throw new AssertionError(label);}
 static void until(Callable<Boolean> condition,String label)throws Exception {long end=System.nanoTime()+TimeUnit.SECONDS.toNanos(30);while(!condition.call()){if(System.nanoTime()>end)throw new AssertionError(label+" "+summary());Thread.sleep(25);}check(true,label);}
 static String summary(){StringBuilder out=new StringBuilder();for(Peer p:peers)out.append(p.index).append(':').append(p.epoch).append('/').append(p.paused).append('/').append(p.ready).append('/').append(p.error).append(' ');return out.toString();}
 static byte[] read(InputStream input)throws Exception{ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=input.read(b))>=0)out.write(b,0,n);return out.toByteArray();}
 static JSONObject cmd(String action){JSONObject b=new JSONObject().put("action",action).put("requestId",UUID.randomUUID().toString());if(room!=null)b.put("roomId",room.getString("roomId")).put("generation",room.getLong("generation"));return b;}
 static JSONObject profile(JSONObject body){return body.put("itemId","synthetic-item").put("contentSha256","a".repeat(64)).put("engineId","synthetic-engine").put("coreSha256","b".repeat(64)).put("runtimeSha256","c".repeat(64)).put("profileId","synthetic-profile").put("profileSha256","d".repeat(64));}
 static JSONObject post(Peer peer,JSONObject body)throws Exception {
  String path="/v1/station/online/multiplayer/command",credential=fixture.getProperty("user"+peer.index+".token");byte[] bytes=body.toString().getBytes(StandardCharsets.UTF_8);
  HttpsURLConnection connection=(HttpsURLConnection)new URL(fixture.getProperty("url")+path).openConnection();connection.setSSLSocketFactory(tls);connection.setConnectTimeout(5000);connection.setReadTimeout(15000);connection.setRequestMethod("POST");connection.setDoOutput(true);connection.setFixedLengthStreamingMode(bytes.length);connection.setRequestProperty("Content-Type","application/json");connection.setRequestProperty("Authorization","Bearer "+credential);connection.setRequestProperty("X-Station-Request-Proof",StationRecoveryProofTest.proof(peer.key,credential,"POST",path,bytes));
  try{connection.getOutputStream().write(bytes);int status=connection.getResponseCode();if(status!=200){String error=new String(read(connection.getErrorStream()),StandardCharsets.UTF_8);throw new IOException("HTTP "+status+" "+body.optString("action")+" "+new JSONObject(error).optString("code"));}
   JSONObject signed=new JSONObject(new String(read(connection.getInputStream()),StandardCharsets.UTF_8));byte[] payload=Base64.getUrlDecoder().decode(signed.getString("payload"));PublicKey signer=KeyFactory.getInstance("RSA").generatePublic(new X509EncodedKeySpec(Base64.getUrlDecoder().decode(fixture.getProperty("responseSPKI"))));Signature verify=Signature.getInstance("RSASSA-PSS");verify.initVerify(signer);verify.setParameter(new PSSParameterSpec("SHA-256","MGF1",MGF1ParameterSpec.SHA256,32,1));verify.update(payload);if(!verify.verify(Base64.getUrlDecoder().decode(signed.getString("signature"))))throw new IOException("Response signature");
   JSONObject result=new JSONObject(new String(payload,StandardCharsets.UTF_8));if(!body.getString("requestId").equals(result.getString("requestId"))||!fixture.getProperty("user"+peer.index+".license").equals(result.getString("licenseId"))||!fixture.getProperty("user"+peer.index+".device").equals(result.getString("deviceId")))throw new IOException("Response binding");return result.getJSONObject("snapshot");
  }finally{connection.disconnect();}
 }
 static final class Peer implements StationMultiplayerSession.Listener {
  final int index;final PrivateKey key;final AtomicInteger credentials=new AtomicInteger(),pauses=new AtomicInteger();volatile long epoch;volatile boolean paused=true,ready,holdReady;volatile String error="";StationMultiplayerSession session;
  Peer(int index)throws Exception{this.index=index;key=KeyFactory.getInstance("RSA").generatePrivate(new PKCS8EncodedKeySpec(Base64.getDecoder().decode(fixture.getProperty("user"+index+".key"))));}
  JSONObject ticket(String link,String action)throws Exception{JSONObject snapshot=post(this,profile(cmd(action).put("linkId",link)));JSONObject ticket=snapshot.getJSONObject("ticket");ticket.put("requestProof",StationRecoveryProofTest.proof(key,ticket.getString("ticket"),"GET","/v1/station/online/multiplayer/relay",new byte[0]));credentials.incrementAndGet();return ticket;}
  public void ticket(String link,long serial){io.execute(()->{try{session.provide(link,serial,ticket(link,"resume"));}catch(Exception e){session.unavailable(link,serial,false);}});}
  public synchronized void nativeControl(long epoch,boolean pause,boolean visible){this.epoch=epoch;paused=pause;if(pause)pauses.incrementAndGet();}
  public synchronized long nativeStatus(){return(epoch<<3)|(paused?1:0)|(ready&&!holdReady?2:0);}
  public boolean nativeStalled(){return false;}
  public boolean bind(int port,int slot){return sourceSlots.putIfAbsent(port,slot)==null;}
  public void unbind(int port,int slot){sourceSlots.remove(port,slot);}
  public void ready(){ready=true;}public void state(boolean waiting,boolean synchronizing){}
  public void fatal(String category){error=category;}public void trace(String safe){if(safe.contains("socket-error")||safe.contains("transport-wait")||safe.contains("event=state")||safe.contains("wait-diagnostic"))System.err.println("lab peer="+index+" "+safe);}
 }
 static Object field(Object owner,String name)throws Exception{Field f=owner.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(owner);}
 static List<StationMultiplayerTunnel> tunnels(Peer peer)throws Exception{List<StationMultiplayerTunnel> result=new ArrayList<>();for(Object channel:(List<?>)field(peer.session,"channels"))result.add((StationMultiplayerTunnel)field(channel,"tunnel"));return result;}
 static void drop(Peer peer)throws Exception{WebSocketClient remote=(WebSocketClient)field(tunnels(peer).get(0),"remote");check(remote!=null&&remote.isOpen(),"actual WSS alive before cut");remote.getSocket().close();}
 static void transfer(Socket from,Socket to,int seed,int size)throws Exception{byte[] payload=new byte[size];new Random(seed).nextBytes(payload);Future<byte[]> received=io.submit(()->{byte[] b=new byte[size];new DataInputStream(to.getInputStream()).readFully(b);return b;});Future<?> sent=io.submit(()->{try{from.getOutputStream().write(payload);}catch(IOException e){throw new UncheckedIOException(e);}});sent.get(30,TimeUnit.SECONDS);check(Arrays.equals(payload,received.get(30,TimeUnit.SECONDS)),"exact ordered isolated TCP bytes");bytesVerified+=size;}
 static boolean playing(){for(Peer peer:peers)if(peer.paused||!peer.error.isEmpty())return false;return true;}
 static boolean drained()throws Exception{for(Peer peer:peers)for(StationMultiplayerTunnel tunnel:tunnels(peer)){Object gate=field(tunnel,"gate");synchronized(gate){var tx=(StationMultiplayerWire.Bytes)field(tunnel,"tx");var rx=(StationMultiplayerWire.Bytes)field(tunnel,"rx");if(tx.pending()!=0||rx.pending()!=0)return false;}}return true;}
 public static void main(String[] args)throws Exception{
  count=Integer.parseInt(args[1]);fixture=new Properties();try(InputStream in=new FileInputStream(args[0])){fixture.load(in);}
  java.security.cert.Certificate cert=CertificateFactory.getInstance("X.509").generateCertificate(new ByteArrayInputStream(Base64.getDecoder().decode(fixture.getProperty("cert"))));KeyStore trust=KeyStore.getInstance(KeyStore.getDefaultType());trust.load(null,null);trust.setCertificateEntry("only-synthetic-lab",cert);TrustManagerFactory tm=TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm());tm.init(trust);SSLContext ssl=SSLContext.getInstance("TLS");ssl.init(null,tm.getTrustManagers(),new SecureRandom());tls=ssl.getSocketFactory();endpoint=URI.create(fixture.getProperty("url").replace("https:","wss:")+"/v1/station/online/multiplayer/relay");
  for(int n=0;n<count;n++)peers.add(new Peer(n));Peer host=peers.get(0);
  room=post(host,profile(cmd("create").put("capacity",count))).getJSONObject("room");for(int n=1;n<count;n++)room=post(peers.get(n),profile(cmd("join"))).getJSONObject("room");for(Peer peer:peers)room=post(peer,cmd("ready").put("value",true)).getJSONObject("room");room=post(host,cmd("start")).getJSONObject("room");check(room.getJSONArray("roster").length()==count,"real HTTP roster count");check(room.getString("connectionPassword").matches("[0-9a-f]{64}"),"actual password wire format");
  ServerSocket listener=new ServerSocket(0,8,InetAddress.getByName("127.0.0.1"));Map<Integer,Socket> accepted=new ConcurrentHashMap<>();List<Socket> guests=new ArrayList<>();List<Future<?>> accepts=new ArrayList<>();
  try{
   for(int n=1;n<count;n++)accepts.add(io.submit(()->{try{Socket socket=listener.accept();socket.setSoTimeout(30000);Integer slot=sourceSlots.get(socket.getPort());if(slot==null)throw new IOException("Native slot missing");if(accepted.put(slot,socket)!=null)throw new IOException("Native slot duplicated");}catch(Exception e){throw new RuntimeException(e);}}));
   JSONArray allLinks=room.getJSONArray("links");
   for(Peer peer:peers){JSONArray links=new JSONArray();for(int n=allLinks.length()-1;n>=0;n--){JSONObject link=allLinks.getJSONObject(n);if(peer.index==0||link.getInt("guestSlot")==peer.index+1)links.put(new JSONObject(link.toString()).put("ticket",peer.ticket(link.getString("linkId"),"ticket")));}JSONObject launch=new JSONObject().put("role",peer.index==0?"host":"guest").put("localSlot",peer.index+1).put("participantCount",count).put("expectedDeviceMask",(1<<count)-1).put("port",listener.getLocalPort()).put("multiplayerLinks",links);peer.session=new StationMultiplayerSession(launch,peer);}
   // Reverse guest start order is deliberate; TCP arrival order must not become controller order.
   for(int i=count-1;i>=0;i--)peers.get(i).session.start();host.session.listening();for(int i=1;i<count;i++){Socket socket=new Socket("127.0.0.1",peers.get(i).session.localPort());socket.setSoTimeout(30000);guests.add(socket);}for(Future<?> accept:accepts)accept.get(15,TimeUnit.SECONDS);
   check(accepted.size()==count-1,"all source ports mapped to independent guest slots");until(()->playing(),"global initial barrier");
   for(int n=1;n<count;n++){check(accepted.containsKey(n),"host source belongs to signed slot");transfer(accepted.get(n),guests.get(n-1),100+n,350000);transfer(guests.get(n-1),accepted.get(n),200+n,350000);}
   until(()->drained(),"initial credit reclaimed");
   for(int target:new int[]{count-1,0}){
    long previous=host.epoch;int before=peers.stream().mapToInt(p->p.credentials.get()).sum();peers.get(target).holdReady=true;drop(peers.get(target));until(()->host.epoch>previous&&peers.stream().allMatch(p->p.paused),"one failed endpoint pauses whole room");
    for(int n=1;n<count;n++){transfer(accepted.get(n),guests.get(n-1),300+target+n,180000);transfer(guests.get(n-1),accepted.get(n),400+target+n,180000);}
    check(peers.stream().allMatch(p->p.paused),"no participant resumes while one native ACK is withheld");peers.get(target).holdReady=false;
    until(()->playing()&&peers.stream().mapToInt(p->p.credentials.get()).sum()>before,"new proof/ticket restores same TCP streams");check(peers.stream().allMatch(p->p.epoch==host.epoch),"all participants same recovery epoch");
   }
   peers.get(count-1).session.visible(false);until(()->peers.stream().allMatch(p->p.paused),"background pauses all players");peers.get(count-1).session.visible(true);until(()->playing(),"foreground restores barrier");
   host.holdReady=true;host.session.visible(false);until(()->peers.stream().allMatch(p->p.paused),"host background pauses every link");host.session.visible(true);host.session.visible(false);host.session.visible(true);
   until(()->tunnels(host).stream().allMatch(t->t.available()),"host authenticates all links after rapid visibility change");check(peers.stream().allMatch(p->p.paused),"all players wait for withheld host native acknowledgement");host.holdReady=false;until(()->playing(),"host visibility flap restores whole room barrier");
   until(()->drained(),"all bounded replay queues drained");
   check(peers.stream().allMatch(p->p.error.isEmpty()),"no fatal Java transport");JSONObject finalRoom=post(host,cmd("heartbeat")).getJSONObject("room");check(finalRoom.getString("roomId").equals(room.getString("roomId"))&&finalRoom.getString("state").equals("playing"),"same real server room playing");
   check(post(host,cmd("leave")).optJSONObject("room")==null,"generation-bound human leave accepted");
   System.out.println(new JSONObject().put("passed",true).put("players",count).put("checks",checks).put("tcpBytesVerified",bytesVerified).put("epoch",host.epoch).put("scope","real Java Session/Tunnel/WebSocket and C# HTTPS/WSS; synthetic native acknowledgements/TCP, no Android gameplay"));
  }finally{for(Peer peer:peers)if(peer.session!=null)peer.session.close();for(Socket socket:guests)socket.close();for(Socket socket:accepted.values())socket.close();listener.close();io.shutdownNow();}
 }
}
/** Test-only TLS dependency injection. Production endpoint/pins are untouched. */
final class StationRelayTls {
 static URI multiplayerEndpoint(){return StationMultiplayerTransportTest.endpoint;}
 static SSLSocketFactory create(){return StationMultiplayerTransportTest.tls;}
}
