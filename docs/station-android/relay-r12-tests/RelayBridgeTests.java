package org.emulationstation.frontend.netplay;
import java.io.*;
import java.net.*;
import java.security.*;
import java.security.cert.*;
import java.util.*;
import java.util.concurrent.*;
import javax.net.ssl.*;

public final class RelayBridgeTests {
 public static void main(String[] args)throws Exception {
  Properties p=new Properties();try(InputStream in=new FileInputStream(args[0])){p.load(in);}
  X509Certificate cert;try(InputStream in=new FileInputStream(p.getProperty("cert"))){cert=(X509Certificate)CertificateFactory.getInstance("X.509").generateCertificate(in);}
  KeyStore ks=KeyStore.getInstance(KeyStore.getDefaultType());ks.load(null);ks.setCertificateEntry("isolated-fixture",cert);
  TrustManagerFactory tm=TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm());tm.init(ks);
  X509TrustManager trust=(X509TrustManager)tm.getTrustManagers()[0];byte[] pin=MessageDigest.getInstance("SHA-256").digest(cert.getPublicKey().getEncoded());
  SSLSocketFactory tls=StationRelayTls.pinned(trust,pin);URI uri=new URI(p.getProperty("uri"));
  int checks=0;
  try{new StationRelayTunnel(new URI(uri.toString().replace("wss:","ws:")),p.getProperty("host"),true,1,tls,()->{});throw new AssertionError("plaintext accepted");}catch(IOException expected){checks++;}
  CountDownLatch rejected=new CountDownLatch(1);
  try(StationRelayTunnel wrong=new StationRelayTunnel(uri,p.getProperty("host"),false,1,StationRelayTls.pinned(trust,new byte[32]),rejected::countDown)){
   wrong.start();if(!rejected.await(10,TimeUnit.SECONDS))throw new AssertionError("pin ignored");checks++;
  }
  CountDownLatch failure=new CountDownLatch(1);
  try(ServerSocket nativeHost=new ServerSocket(0,1,InetAddress.getByName("127.0.0.1"));
      StationRelayTunnel host=new StationRelayTunnel(uri,p.getProperty("host"),true,nativeHost.getLocalPort(),tls,failure::countDown);
      StationRelayTunnel client=new StationRelayTunnel(uri,p.getProperty("client"),false,1,tls,failure::countDown)){
   nativeHost.setSoTimeout(15000);host.start();host.listening();client.start();
   try(Socket a=nativeHost.accept();Socket b=new Socket("127.0.0.1",client.localPort())){
    a.setSoTimeout(20000);b.setSoTimeout(20000);
    ExecutorService workers=Executors.newFixedThreadPool(2);
    try{
     byte[] payload=new byte[12*1024*1024+117];new Random(42).nextBytes(payload);
     Future<?> send=workers.submit(()->{try{a.getOutputStream().write(payload);}catch(IOException e){throw new RuntimeException(e);}});
     byte[] actual=read(b,payload.length);send.get(20,TimeUnit.SECONDS);
     if(!Arrays.equals(payload,actual))throw new AssertionError("forward bytes corrupted");checks++;
     Future<?> back=workers.submit(()->{try{b.getOutputStream().write(payload);}catch(IOException e){throw new RuntimeException(e);}});
     if(!Arrays.equals(payload,read(a,payload.length)))throw new AssertionError("reverse bytes corrupted");back.get(20,TimeUnit.SECONDS);checks++;
     for(int i=0;i<50;i++){b.getOutputStream().write(i);if(a.getInputStream().read()!=i)throw new AssertionError("ordered input");}checks++;
     if(failure.getCount()!=1)throw new AssertionError("unexpected disconnect");checks++;
    }finally{workers.shutdownNow();}
   }
   if(!failure.await(5,TimeUnit.SECONDS))throw new AssertionError("native disconnect ignored");checks++;
  }
  System.out.println("{\"passed\":true,\"checks\":"+checks+",\"bytesEachDirection\":12583029,\"scope\":\"Production Java tunnel through production C# WSS relay; synthetic TCP endpoints, isolated trusted TLS certificate\"}");
 }
 static byte[] read(Socket s,int size)throws IOException{byte[] bytes=new byte[size];int n=0,c;while(n<size&&(c=s.getInputStream().read(bytes,n,size-n))>0)n+=c;if(n!=size)throw new EOFException(n+"/"+size);return bytes;}
}
