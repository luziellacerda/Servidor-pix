using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

// Ephemeral tickets arrive only on an anonymous stdin pipe. Stdout is metrics.
const string Pin="13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7";
const int Frames=1200;
var streams=new List<ClientWebSocket>();
string stage="input";
try
{
    var input=JsonSerializer.Deserialize<Input>((await Console.In.ReadLineAsync())!)!;
    if(input.Url is not ("wss://app.lzgames.com.br/v1/station/online/relay" or "ws://127.0.0.1:5192/v1/station/online/relay" or "ws://127.0.0.1/v1/station/online/relay")||input.Tickets.Length is <1 or >512||
        input.Tickets.Any(p=>p.Length!=2||p.Any(t=>!Regex.IsMatch(t,"\\A[A-Za-z0-9_-]{43}\\z"))))
        throw new InvalidOperationException("Bounded Station input required");
    using var limit=new SemaphoreSlim(32);
    using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(100));
    var process=Process.GetCurrentProcess();double cpuBefore=process.TotalProcessorTime.TotalSeconds;
    stage="connect";
    async Task<ClientWebSocket> Open(string ticket)
    {
        await limit.WaitAsync(cancel.Token);
        try
        {
            var socket=new ClientWebSocket();lock(streams)streams.Add(socket);
            socket.Options.Proxy=null;
            socket.Options.AddSubProtocol("station-relay.v1");
            socket.Options.SetRequestHeader("Authorization","StationRelay "+ticket);
            socket.Options.SetRequestHeader("User-Agent","Dalvik/2.1.0 Station synthetic CSharp capacity check");
            if(input.Url=="ws://127.0.0.1/v1/station/online/relay")socket.Options.SetRequestHeader("Host","app.lzgames.com.br");
            socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(20);
            socket.Options.RemoteCertificateValidationCallback=(_,certificate,_,errors)=>
                errors==SslPolicyErrors.None&&certificate is System.Security.Cryptography.X509Certificates.X509Certificate2 cert&&
                Convert.ToHexString(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant()==Pin;
            await socket.ConnectAsync(new Uri(input.Url),cancel.Token);
            if(socket.SubProtocol!="station-relay.v1")throw new InvalidOperationException("Relay protocol mismatch");
            return socket;
        }
        finally{limit.Release();}
    }
    var pairs=await Task.WhenAll(input.Tickets.Select(async pair=>await Task.WhenAll(pair.Select(Open))));
    Console.WriteLine(JsonSerializer.Serialize(new {stage="connected",connections=streams.Count}));
    if(await Console.In.ReadLineAsync()!="run")throw new InvalidOperationException("Operator heartbeat acknowledgement required");
    stage="data";
    var watch=Stopwatch.StartNew();
    async Task<int> Receive(ClientWebSocket socket,Memory<byte> buffer)
    {
        int count=0;bool end=false;
        while(!end)
        {
            var read=await socket.ReceiveAsync(buffer[count..],cancel!.Token);
            if(read.MessageType!=WebSocketMessageType.Binary||read.Count==0||count+read.Count>32)
                throw new InvalidOperationException("Unexpected relay message");
            count+=read.Count;end=read.EndOfMessage;
            if(count==32&&!end)throw new InvalidOperationException("Oversized relay message");
        }
        if(count!=32)throw new InvalidOperationException("Truncated relay message");
        return count;
    }
    async Task<(double[] Times,int Late)> Exchange(int pair,ClientWebSocket[] sockets)
    {
        var host=sockets[0];var guest=sockets[1];var times=new double[Frames];int late=0;
        long origin=Stopwatch.GetTimestamp();
        async Task Sender()
        {
            var payload=new byte[32];
            for(int frame=0;frame<Frames;frame++)
            {
                long now=Stopwatch.GetTimestamp();
                if(Stopwatch.GetElapsedTime(origin,now).TotalSeconds-frame/60d>1d/60)late++;
                BinaryPrimitives.WriteInt64LittleEndian(payload,now);
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8),pair);
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12),frame);
                await host.SendAsync(payload.AsMemory(),WebSocketMessageType.Binary,true,cancel.Token);
                double delay=(frame+1)/60d-Stopwatch.GetElapsedTime(origin).TotalSeconds;
                if(delay>0)await Task.Delay(TimeSpan.FromSeconds(delay),cancel.Token);
            }
        }
        async Task Echo()
        {
            var payload=new byte[32];
            for(int frame=0;frame<Frames;frame++)
            {
                await Receive(guest,payload);
                if(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(8))!=pair||
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(12))!=frame)
                    throw new InvalidOperationException("Forward packet order mismatch");
                await guest.SendAsync(payload.AsMemory(),WebSocketMessageType.Binary,true,cancel.Token);
            }
        }
        async Task Receiver()
        {
            var payload=new byte[32];
            for(int frame=0;frame<Frames;frame++)
            {
                await Receive(host,payload);
                if(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(8))!=pair||
                    BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(12))!=frame||
                    payload.AsSpan(16).IndexOfAnyExcept((byte)0)>=0)
                    throw new InvalidOperationException("Reverse packet order mismatch");
                times[frame]=Stopwatch.GetElapsedTime(BinaryPrimitives.ReadInt64LittleEndian(payload)).TotalMilliseconds;
            }
        }
        await Task.WhenAll(Sender(),Echo(),Receiver());return(times,late);
    }
    var results=await Task.WhenAll(pairs.Select((pair,index)=>Exchange(index,pair)));
    var timings=results.SelectMany(x=>x.Times).Order().ToArray();
    double Quantile(double q)=>timings[Math.Min(timings.Length-1,(int)(timings.Length*q))];
    process.Refresh();
    Console.WriteLine(JsonSerializer.Serialize(new {passed=true,client="csharp_multithreaded",publicConnections=streams.Count,
        framesPerSecondTarget=60,framesPerPair=Frames,orderedRoundTrips=timings.Length,payloadBytes=timings.Length*32L*2,
        loadSeconds=watch.Elapsed.TotalSeconds,roundTripP50Ms=Quantile(.50),roundTripP95Ms=Quantile(.95),
        roundTripP99Ms=Quantile(.99),droppedOrCorruptPackets=0,sendScheduleLateFrames=results.Sum(x=>x.Late),
        clientCpuSeconds=process.TotalProcessorTime.TotalSeconds-cpuBefore,clientPeakMemory=process.PeakWorkingSet64}));
}
catch(Exception error)
{
    Console.WriteLine(JsonSerializer.Serialize(new {passed=false,failedStage=stage,errorType=error.GetType().Name}));
    Environment.ExitCode=1;
}
finally
{
    await Task.WhenAll(streams.Select(async socket=>{
        try{using var timeout=new CancellationTokenSource(2000);await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"",timeout.Token);}
        catch(WebSocketException){}catch(OperationCanceledException){}finally{socket.Dispose();}
    }));
}
sealed record Input(string Url,string[][] Tickets);
