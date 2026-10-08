package org.emulationstation.frontend.netplay;
import org.emulationstation.frontend.relay.ws.client.WebSocketClient;
import org.emulationstation.frontend.relay.ws.drafts.Draft_6455;
import org.emulationstation.frontend.relay.ws.protocols.Protocol;
import org.emulationstation.frontend.relay.ws.handshake.ServerHandshake;
import javax.net.ssl.*;
import java.io.*;
import java.net.*;
import java.nio.ByteBuffer;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;

/** Keeps the native TCP stream. Replaces only authenticated WSS, using accepted/delivered offsets. */
final class StationRecoveryTunnel implements Closeable {
    interface Listener {
        default void trace(String safeEvent){}
        void ticket();void ready();void state(boolean waiting,boolean synchronizing);
        void nativeControl(long epoch,boolean pause,boolean visible);long nativeStatus();boolean nativeStalled();void unrecoverable(String category);
    }
    private final Object gate=new Object();private final URI uri;private final SSLSocketFactory tls;
    private final boolean host;private final int hostPort;private final Listener listener;
    private final StationRecoveryWire.Bytes tx,rx;
    private final ScheduledExecutorService worker=Executors.newSingleThreadScheduledExecutor(r->{Thread t=new Thread(r,"Station-recovery");t.setDaemon(true);return t;});
    private final ExecutorService writes=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"Station-recovery-send");t.setDaemon(true);return t;});
    private final Semaphore nativeHint=new Semaphore(0);
    private final AtomicBoolean closed=new AtomicBoolean(),pumpPending=new AtomicBoolean(),pumpRequested=new AtomicBoolean();
    private final ServerSocket accept;private volatile Socket local;private volatile Remote remote;
    private boolean foreground=true,welcome,ticketPending,terminal,localReady,readyNotified;
    private long epoch,state,accepted,cursor,ackSent=-1,pauseSent=-1,readySent=-1,pingSent,nextTicket,ticketRequested;
    private long started=System.nanoTime(),lastReceive=System.nanoTime();private int attempts;
    private volatile boolean startedTransport;
    private long syncRequested=-1;
    private final WaitDiagnostics waitDiagnostics=new WaitDiagnostics();
    StationRecoveryTunnel(URI uri,String ticket,String proof,int window,boolean host,int hostPort,SSLSocketFactory tls,Listener listener)throws Exception {
        if(!"wss".equals(uri.getScheme())||uri.getUserInfo()!=null||uri.getQuery()!=null||uri.getFragment()!=null||!"/v1/station/online/relay".equals(uri.getPath()))throw new IOException("ENDPOINT");
        this.uri=uri;this.tls=tls;this.host=host;this.hostPort=hostPort;this.listener=listener;
        tx=new StationRecoveryWire.Bytes(window);rx=new StationRecoveryWire.Bytes(window);
        accept=host?null:new ServerSocket(0,1,InetAddress.getByName("127.0.0.1"));
        provide(ticket,proof,window);
    }
    int localPort(){return host?hostPort:accept.getLocalPort();}
    void listening(){nativeHint.release();}
    boolean available(){synchronized(gate){return !closed.get()&&!terminal&&localReady&&welcome&&remote!=null&&remote.isOpen();}}
    void start(){
        startedTransport=true;
        Thread input=new Thread(this::nativeInput,"Station-recovery-native-input");input.setDaemon(true);input.start();
        Thread output=new Thread(this::nativeOutput,"Station-recovery-native-output");output.setDaemon(true);output.start();
        worker.scheduleWithFixedDelay(this::tick,0,250,TimeUnit.MILLISECONDS);
        Remote current=remote;if(current!=null)current.begin();
    }
    void provide(String ticket,String proof,int window)throws Exception {
        if(ticket==null||!ticket.matches("[A-Za-z0-9_-]{43}")||proof==null||proof.length()>1024||!proof.matches("v1\\.[0-9]{9,12}\\.[A-Za-z0-9_-]{22}\\.[A-Za-z0-9_-]+")||window!=tx.capacity())throw new IOException("CREDENTIAL");
        Remote next=new Remote(ticket,proof);
        synchronized(gate){if(closed.get()||terminal||!foreground)return;remote=next;welcome=false;ticketPending=false;}
        if(startedTransport)next.begin(); // Initial connection is started after NativeActivity preparation.
    }
    void unavailable(){synchronized(gate){ticketPending=false;attempts=Math.min(attempts+1,5);nextTicket=System.nanoTime()+TimeUnit.SECONDS.toNanos(Math.min(10,1<<attempts));}}
    void reject(){fatal("AUTHORITY");}
    void visible(boolean value){
        Remote current;long nowEpoch;
        synchronized(gate){foreground=value;current=remote;nowEpoch=epoch;readySent=-1;if(!value){welcome=false;ticketPending=false;remote=null;}else{nextTicket=0;ticketPending=false;}gate.notifyAll();}
        if(!value){listener.nativeControl(nowEpoch,true,false);listener.state(true,false);if(current!=null){if(current.isOpen())try{current.send(new StationRecoveryWire(11,nowEpoch,0).encode());}catch(RuntimeException ignored){}current.closeConnection(1000,"");}}
        else {listener.nativeControl(nowEpoch,true,true);tick();}
    }
    private void nativeInput(){try{
        local=host?StationHostConnector.connect(hostPort,System.nanoTime()+TimeUnit.SECONDS.toNanos(45),closed::get,s->local=s,nativeHint):accept.accept();
        local.setTcpNoDelay(true);local.setReceiveBufferSize(32768);local.setSendBufferSize(32768);
        synchronized(gate){localReady=true;gate.notifyAll();}notifyReady();
        InputStream in=local.getInputStream();
        while(!closed.get()){
            int credit; synchronized(gate){while(!closed.get()&&(terminal||tx.credit()==0))gate.wait();if(closed.get())return;credit=Math.min(16384,tx.credit());}
            byte[] bytes=new byte[credit];int count=in.read(bytes);
            if(count<0)throw new IOException("NATIVE_EOF");if(count==0)continue;
            synchronized(gate){tx.append(tx.next,Arrays.copyOf(bytes,count));readySent=-1;gate.notifyAll();}pump();
        }
    }catch(Exception error){if(!closed.get())fatal("NATIVE_STREAM");}}
    private void nativeOutput(){try{
        while(!closed.get()){
            byte[] bytes;long at;
            synchronized(gate){while(!closed.get()&&(!localReady||terminal||rx.pending()==0))gate.wait();if(closed.get())return;at=rx.delivered;bytes=rx.read(at);}
            local.getOutputStream().write(bytes);
            synchronized(gate){rx.confirm(at+bytes.length);readySent=-1;gate.notifyAll();}pump();
        }
    }catch(Exception error){if(!closed.get())fatal("NATIVE_STREAM");}}
    private void notifyReady(){boolean notify;synchronized(gate){notify=localReady&&welcome&&!readyNotified;if(notify)readyNotified=true;}if(notify)listener.ready();}
    private void tick(){try{
        if(closed.get())return;
        Remote current;boolean request=false;long now=System.nanoTime();
        synchronized(gate){if(terminal)return;current=remote;
            if(ticketPending&&now-ticketRequested>TimeUnit.SECONDS.toNanos(20)){ticketPending=false;nextTicket=now+TimeUnit.SECONDS.toNanos(2);}
            if(foreground&&(current==null||current.isClosed())&&!ticketPending&&now>=nextTicket){ticketPending=true;ticketRequested=now;request=true;}
        }
        if(request)listener.ticket();
        boolean stalled=false;long atEpoch=0;
        synchronized(gate){if(state==2&&welcome&&current!=null&&current.isOpen()&&syncRequested!=epoch&&listener.nativeStalled()){syncRequested=epoch;atEpoch=epoch;stalled=true;}}
        if(stalled){listener.nativeControl(atEpoch,true,foreground);listener.state(true,false);current.send(new StationRecoveryWire(13,atEpoch,0).encode());}
        if(current!=null&&current.isOpen()&&now-lastReceive>TimeUnit.SECONDS.toNanos(30))lost(current);
        pump();traceWaiting(now);
    }catch(RuntimeException error){fatal("CONTROL");}}
    private void traceWaiting(long now){
        // Diagnostics never change transport state or turn a logging failure into session exit.
        try{String event;
            synchronized(gate){
                if(state==2){waitDiagnostics.reset();return;}
                if(closed.get()||terminal||!foreground||!welcome||remote==null||!remote.isOpen()||!waitDiagnostics.due(now))return;
                event=waitDiagnostics.sample(now,epoch,state,listener.nativeStatus(),tx.next,accepted,cursor,rx.next,rx.delivered,localReady,readySent);
            }
            if(event!=null)listener.trace(event);
        }catch(RuntimeException ignored){}
    }
    /** Six numeric snapshots in the first minute of each wait, never tickets, paths or payloads. */
    static final class WaitDiagnostics {
        private boolean started;private long first,last;private int count;
        boolean due(long now){return !started||(count<6&&now-first<TimeUnit.SECONDS.toNanos(60)&&now-last>=TimeUnit.SECONDS.toNanos(10));}
        void reset(){started=false;count=0;}
        String sample(long now,long epoch,long state,long nativeStatus,long txNext,long accepted,long cursor,long rxNext,long rxDelivered,boolean localReady,long readySent){
            if(state==2){reset();return null;}if(!due(now))return null;
            if(!started){started=true;first=now;}last=now;count++;
            return "event=wait-diagnostic epoch="+epoch+" state="+state+" nativeStatus="+nativeStatus+" tx.next="+txNext+" accepted="+accepted+" cursor="+cursor+" rx.next="+rxNext+" rx.delivered="+rxDelivered+" localReady="+localReady+" readySent="+readySent;
        }
    }
    /** Remember a request even when the current writer has already observed an empty queue.
     * One executor owns frame selection/sending; a request is consumed before the next sample.
     * Rejection releases ownership but retains the request for the next caller (no retry spin). */
    private void pump(){
        if(closed.get())return;
        pumpRequested.set(true);
        if(!pumpPending.compareAndSet(false,true))return;
        try{writes.execute(()->{Remote sending=null;try{
        for(;;){pumpRequested.set(false);sending=remote;Remote current;StationRecoveryWire frame=null;boolean retry=false;long nativeState=listener.nativeStatus();
            synchronized(gate){current=remote;sending=current;if(closed.get()||terminal||!welcome||current==null||!current.isOpen())return;
                if(nativeState>=0&&(nativeState&4)!=0){fatal("NATIVE_PROTOCOL");return;}
                if(rx.delivered!=ackSent){ackSent=rx.delivered;frame=new StationRecoveryWire(4,ackSent,0);}
                else if(state!=2&&nativeState>=0&&(nativeState>>3)==epoch&&(nativeState&1)!=0&&pauseSent!=epoch){pauseSent=epoch;frame=new StationRecoveryWire(7,epoch,0);}
                else if(cursor<tx.next){if(queued(current)>65536)retry=true;else{byte[] bytes=tx.read(cursor);frame=new StationRecoveryWire(3,cursor,0,bytes);cursor+=bytes.length;}}
                else if(state!=2&&foreground&&nativeState>=0&&(nativeState>>3)==epoch&&(nativeState&3)==3&&accepted==tx.next&&rx.pending()==0&&readySent!=tx.next){readySent=tx.next;frame=new StationRecoveryWire(8,epoch,tx.next);}
                else if(System.nanoTime()-pingSent>TimeUnit.SECONDS.toNanos(10)){pingSent=System.nanoTime();frame=new StationRecoveryWire(9,TimeUnit.NANOSECONDS.toMillis(pingSent-started),0);}
            }
            if(retry){Thread.sleep(25);continue;}
            if(frame==null)return;
            sending=current;current.send(frame.encode());sending=null;
        }
    }catch(InterruptedException error){Thread.currentThread().interrupt();}
    catch(Exception error){if(sending!=null)lost(sending);}
    finally{
        pumpPending.set(false);
        // A producer may have signalled after the final empty decision but before this release.
        if(pumpRequested.get()&&!closed.get())pump();
    }});}catch(RejectedExecutionException ignored){pumpPending.set(false);}
    }
    private static long queued(Remote remote){long count=0;for(ByteBuffer b:((org.emulationstation.frontend.relay.ws.WebSocketImpl)remote.getConnection()).outQueue)count+=b.remaining();return count;}
    private void lost(Remote current){long nowEpoch;boolean visible;
        synchronized(gate){if(remote!=current||closed.get()||terminal)return;remote=null;welcome=false;readyNotified=false;ticketPending=false;pauseSent=-1;readySent=-1;nowEpoch=epoch;visible=foreground;attempts=Math.min(attempts+1,5);nextTicket=System.nanoTime()+TimeUnit.SECONDS.toNanos(Math.min(10,1<<attempts));gate.notifyAll();}
        listener.trace("event=transport-wait epoch="+nowEpoch+" accepted="+accepted+" delivered="+rx.delivered);
        current.closeConnection(1000,"");listener.nativeControl(nowEpoch,true,visible);listener.state(true,false);
    }
    private void fatal(String category){long nowEpoch;boolean visible;Remote current;
        synchronized(gate){if(terminal||closed.get())return;terminal=true;current=remote;remote=null;nowEpoch=epoch;visible=foreground;gate.notifyAll();}
        if(current!=null)current.closeConnection(1000,"");listener.nativeControl(nowEpoch,true,visible);listener.unrecoverable(category);
    }
    @Override public void close(){if(!closed.compareAndSet(false,true))return;
        synchronized(gate){gate.notifyAll();}nativeHint.release();Remote current=remote;if(current!=null)current.closeConnection(1000,"");
        try{if(accept!=null)accept.close();}catch(IOException ignored){}try{if(local!=null)local.close();}catch(IOException ignored){}
        worker.shutdownNow();writes.shutdownNow();
    }
    private final class Remote extends WebSocketClient {
        private final AtomicBoolean begun=new AtomicBoolean();
        Remote(String ticket,String proof){super(StationRecoveryTunnel.this.uri,new Draft_6455(Collections.emptyList(),Collections.singletonList(new Protocol("station-stream.v2")),65536),headers(ticket,proof),10000);setSocketFactory(tls);setTcpNoDelay(true);setConnectionLostTimeout(20);setDaemon(true);}
        void begin(){if(begun.compareAndSet(false,true))connect();}
        @Override protected void onSetSSLParameters(SSLParameters parameters){parameters.setEndpointIdentificationAlgorithm("HTTPS");}
        @Override public void onOpen(ServerHandshake handshake){if(!"station-stream.v2".equals(handshake.getFieldValue("Sec-WebSocket-Protocol"))){fatal("PROTOCOL");return;}
            synchronized(gate){if(remote!=this||closed.get())return;send(new StationRecoveryWire(1,accepted,rx.delivered).encode());lastReceive=System.nanoTime();}}
        @Override public void onMessage(String text){fatal("PROTOCOL");}
        @Override public void onMessage(ByteBuffer message){try{
            StationRecoveryWire frame=StationRecoveryWire.decode(message);boolean control=false;long desiredEpoch=0,desiredState=0;boolean visible;
            synchronized(gate){if(remote!=this||closed.get()||terminal)return;lastReceive=System.nanoTime();visible=foreground;
                switch(frame.type){
                    case StationRecoveryWire.STATE:
                        if(frame.offset<epoch||frame.value>3)throw new IOException("EPOCH");
                        epoch=frame.offset;state=frame.value;if(state==2)waitDiagnostics.reset();pauseSent=-1;readySent=-1;desiredEpoch=epoch;desiredState=state;control=true;break;
                    case StationRecoveryWire.WELCOME:
                    case StationRecoveryWire.ACCEPTED:
                        if(frame.offset<accepted||frame.offset>tx.next||frame.value>frame.offset)throw new IOException("OFFSET");
                        accepted=frame.offset;tx.confirm(frame.value);
                        if(frame.type==2){cursor=accepted;ackSent=-1;welcome=true;attempts=0;}
                        readySent=-1;gate.notifyAll();break;
                    case StationRecoveryWire.DATA:
                        rx.append(frame.offset,frame.data);readySent=-1;gate.notifyAll();break;
                    case StationRecoveryWire.PONG:
                        if(frame.offset==TimeUnit.NANOSECONDS.toMillis(pingSent-started))listener.trace("event=pong epoch="+epoch+" rttMs="+TimeUnit.NANOSECONDS.toMillis(System.nanoTime()-pingSent));break;
                    default:throw new IOException("PROTOCOL");
                }
            }
            if(control){listener.trace("event=state epoch="+desiredEpoch+" state="+desiredState);if(desiredState==3){fatal("SERVER_STATE");return;}listener.nativeControl(desiredEpoch,desiredState!=2,visible);listener.state(desiredState!=2,desiredState==1);}
            notifyReady();pump();
        }catch(Exception error){fatal("PROTOCOL");}}
        @Override public void onClose(int code,String reason,boolean peer){listener.trace("event=socket-close epoch="+epoch+" code="+code+" peer="+peer);lost(this);}
        @Override public void onError(Exception error){listener.trace("event=socket-error epoch="+epoch+" type="+error.getClass().getSimpleName());lost(this);}
    }
    private static Map<String,String> headers(String ticket,String proof){Map<String,String> value=new HashMap<>();value.put("Authorization","StationRelay "+ticket);value.put("X-Station-Request-Proof",proof);return value;}
}
