package org.emulationstation.frontend.station;

import java.io.*;
import java.net.*;
import java.security.*;
import java.security.cert.*;
import javax.net.ssl.*;

/** The only HTTP transport of the reconstructed Station service. */
public final class StationHttp implements StationApi.Transport {
    private final SSLSocketFactory sockets;
    private final URL origin;

    private final java.util.function.BooleanSupplier connected;
    public StationHttp()throws Exception {this(()->true);}
    public StationHttp(java.util.function.BooleanSupplier connected) throws Exception {
        this.connected=connected;
        origin = new URL(StationConfig.BASE_URL);
        if (!origin.getProtocol().equals("https") || origin.getUserInfo() != null
                || origin.getQuery() != null || origin.getRef() != null
                || !origin.getPath().isEmpty()) throw new SecurityException("Invalid Station origin");
        TrustManagerFactory factory = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm());
        factory.init((KeyStore)null);
        X509TrustManager system = null;
        for (TrustManager manager : factory.getTrustManagers())
            if (manager instanceof X509TrustManager) system = (X509TrustManager)manager;
        if (system == null) throw new GeneralSecurityException("System trust unavailable");
        final X509TrustManager trust = system;
        final byte[] pin = decodeHex(StationConfig.TLS_SPKI_SHA256);
        X509TrustManager pinned = new X509TrustManager() {
            public X509Certificate[] getAcceptedIssuers() { return trust.getAcceptedIssuers(); }
            public void checkClientTrusted(X509Certificate[] chain, String type) throws CertificateException {
                trust.checkClientTrusted(chain, type);
            }
            public void checkServerTrusted(X509Certificate[] chain, String type) throws CertificateException {
                trust.checkServerTrusted(chain, type);
                if (chain == null || chain.length == 0) throw new CertificateException("Missing certificate");
                try {
                    byte[] actual = MessageDigest.getInstance("SHA-256").digest(chain[0].getPublicKey().getEncoded());
                    if (!MessageDigest.isEqual(pin, actual)) throw new CertificateException("Station pin mismatch");
                } catch (GeneralSecurityException e) { throw new CertificateException("Station TLS rejected", e); }
            }
        };
        SSLContext tls = SSLContext.getInstance("TLS");
        tls.init(null, new TrustManager[]{pinned}, null);
        sockets = tls.getSocketFactory();
    }

    @Override public boolean available(){return connected.getAsBoolean();}
    @Override public StationApi.Response exchange(String method, String path, byte[] body,
            String token, StationApi.Cancellation cancellation) throws IOException {
        return exchange(method,path,body,token,null,cancellation);
    }
    @Override public boolean supportsRequestProof(){return true;}
    @Override public StationApi.Response exchange(String method,String path,byte[] body,
            String token,String proof,StationApi.Cancellation cancellation)throws IOException {
        int maximum=path.equals("/v1/station/online/multiplayer/command")?8192:path.equals("/v1/station/activations/complete")||path.equals("/v1/station/sessions")?65536:StationProtocol.MAXIMUM_BODY_BYTES;
        if (!allowed(method, path) || body != null && body.length > maximum)
            throw new IOException("Unsupported Station request");
        if (token != null && !StationProtocol.activationCode(token)) throw new IOException("Invalid session token");
        cancellation.check();
        final HttpsURLConnection connection = (HttpsURLConnection)new URL(origin, path).openConnection();
        boolean handedOff = false;
        Runnable abort = connection::disconnect;
        cancellation.attach(abort);
        try {
            connection.setSSLSocketFactory(sockets);
            // The platform's hostname verifier and certificate validation remain enabled.
            connection.setInstanceFollowRedirects(false);
            connection.setConnectTimeout(10000);
            connection.setReadTimeout(30000);
            connection.setUseCaches(false);
            connection.setRequestMethod(method);
            String correlation=java.util.UUID.randomUUID().toString().replace("-","");
            connection.setRequestProperty("X-Correlation-ID",correlation);
            connection.setRequestProperty("Accept-Encoding", "identity");
            if (token != null) connection.setRequestProperty("Authorization", "Bearer " + token);
            if(proof!=null){
                if(proof.length()>1024||!proof.matches("v1\\.[0-9]{9,12}\\.[A-Za-z0-9_-]{22}\\.[A-Za-z0-9_-]+"))throw new IOException("Invalid Station request proof");
                connection.setRequestProperty("X-Station-Request-Proof",proof);
            }
            if (body != null) {
                connection.setDoOutput(true);
                connection.setFixedLengthStreamingMode(body.length);
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                try (OutputStream out = connection.getOutputStream()) { out.write(body); }
            }
            cancellation.check();
            int status = connection.getResponseCode();
            StationDiagnostics.record(StationDiagnostics.route(path),status,0);
            // Log our generated UUID, never arbitrary text supplied in a response header.
            StationDiagnostics.trace(StationDiagnostics.route(path),status,correlation);
            if (status / 100 == 3 || connection.getHeaderField("Location") != null)
                throw new IOException("Station redirect refused");
            String encoding = connection.getHeaderField("Content-Encoding");
            if (encoding != null && !encoding.equalsIgnoreCase("identity"))
                throw new IOException("Unexpected Station content encoding");
            InputStream stream = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
            if (stream == null) stream = new ByteArrayInputStream(new byte[0]);
            final ResponseBody owned = new ResponseBody(stream,connection.getContentLengthLong());
            StationApi.Response response = new StationApi.Response(status, connection.getContentType(),
                connection.getContentLengthLong(), owned, () -> closeResponse(owned,cancellation,abort),
                StationRetryAfter.parse(connection.getHeaderField("Retry-After"),System.currentTimeMillis()));
            handedOff = true;
            return response;
        } catch(IOException failure) {
            StationDiagnostics.record(StationDiagnostics.Event.REQUEST_FAILED,0,0);throw failure;
        } finally {
            if (!handedOff) { cancellation.detach(abort); connection.disconnect(); }
        }
    }

    // Based on upstream 1dc8c381: allow the platform pool to reuse fully consumed TLS responses.
    // Also check announced length; truncated, failed or cancelled bodies are disconnected first.
    static final class ResponseBody extends FilterInputStream {
        final long expected;long received;boolean finished;
        ResponseBody(InputStream source){this(source,-1);}
        ResponseBody(InputStream source,long expected){super(source);this.expected=expected;}
        private int observed(int count){if(count<0)finished=expected<0||received==expected;else received+=count;return count;}
        @Override public int read()throws IOException{int value=in.read();observed(value<0?-1:1);return value;}
        @Override public int read(byte[] bytes,int offset,int length)throws IOException{return observed(in.read(bytes,offset,length));}
    }
    static void closeResponse(ResponseBody body,StationApi.Cancellation cancellation,Runnable disconnect)throws IOException {
        if(!body.finished||cancellation.cancelled())disconnect.run();
        try{body.close();}
        catch(IOException failed){disconnect.run();throw failed;}
        finally{cancellation.detach(disconnect);}
    }

    static boolean allowed(String method, String path) {
        if ("POST".equals(method)) return path.equals("/v1/station/activations/challenge")
            || path.equals("/v1/station/activations/complete") || path.equals("/v1/station/challenges")
            || path.equals("/v1/station/sessions") || path.equals("/v1/station/downloads/authorize")
            || path.equals("/v1/station/online/command") || path.equals("/v1/station/online/events")
            || path.equals("/v1/station/online/multiplayer/command");
        if (!"GET".equals(method)) return false;
        if (path.equals("/v1/station/me") || path.equals("/v1/station/catalog") || path.equals("/v1/station/catalog?metadata=1")) return true;
        String covers = "/v1/station/covers/", artifacts = "/v1/station/artifacts/";
        return path.startsWith(covers) && StationProtocol.libraryId(path.substring(covers.length()))
            || path.startsWith(artifacts) && StationProtocol.activationCode(path.substring(artifacts.length()));
    }

    private static byte[] decodeHex(String value) throws GeneralSecurityException {
        if (value == null || !value.matches("[0-9a-f]{64}")) throw new GeneralSecurityException("Invalid TLS pin");
        byte[] bytes = new byte[32];
        for (int i = 0; i < bytes.length; i++) bytes[i] = (byte)Integer.parseInt(value.substring(i*2, i*2+2), 16);
        return bytes;
    }
}
