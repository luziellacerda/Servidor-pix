using System.Diagnostics;
using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

static class ObservationChecks
{
    public static int Run()
    {
        int checks=0;void Check(bool value,string label){checks++;if(!value)throw new InvalidOperationException(label);}
        var plain=new StationStreamSession(32768);var observed=new StationStreamSession(32768,true);
        var pa=plain.Attach(true,"private-host-identity");var pb=plain.Attach(false,"private-client-identity");
        var oa=observed.Attach(true,"private-host-identity");var ob=observed.Attach(false,"private-client-identity");
        void Input(bool host,StationStreamFrame frame){plain.Receive(host,host?pa:pb,frame);observed.Receive(host,host?oa:ob,frame);}
        void Drain(bool host){while(true){var p=plain.Next(host,host?pa:pb).Frame;var o=observed.Next(host,host?oa:ob).Frame;
            Check(p.HasValue==o.HasValue,"observer does not create or consume protocol frames");if(p is not {} pf||o is not {} of)break;
            Check(pf.Encode().SequenceEqual(of.Encode()),"observer preserves exact encoded bytes/order/epochs");
            var observation=(host?oa:ob).Observation!;long at=observation.Sending(of);observation.Sent(of,at,true);}}
        Input(true,new(1,0,0,[]));Input(false,new(1,0,0,[]));Drain(true);Drain(false);
        long epoch=plain.Snapshot().Epoch;
        Input(true,new(7,epoch,0,[]));Input(false,new(7,epoch,0,[]));Input(true,new(8,epoch,0,[]));Input(false,new(8,epoch,0,[]));
        var payload=Enumerable.Range(0,1024).Select(i=>(byte)(i%251)).ToArray();
        Input(true,new(3,0,0,payload));Input(true,new(3,0,0,payload));Drain(false);Drain(true);
        plain.Detach(false,pb);observed.Detach(false,ob);pb=plain.Attach(false,"private-client-reconnect");ob=observed.Attach(false,"private-client-reconnect");
        Input(false,new(1,0,payload.Length,[]));Drain(false);Drain(true);
        Check(plain.Snapshot()==observed.Snapshot(),"lost ACK/reconnect/HELLO accounting identical with metrics");
        for(int i=0;i<60;i++){
            Input(true,new(3,(i+1)*payload.Length,0,payload));Drain(false);Input(false,new(4,(i+2)*payload.Length,0,[]));Drain(true);
            Input(false,new(9,i,0,[]));Drain(false);
        }
        epoch=plain.Snapshot().Epoch;
        Input(true,new(7,epoch,0,[]));Input(false,new(7,epoch,0,[]));Input(true,new(8,epoch,61*payload.Length,[]));Input(false,new(8,epoch,0,[]));
        Input(true,new(13,epoch,0,[]));Drain(false);Drain(true);
        Check(plain.Snapshot()==observed.Snapshot(),"NeedSync and empty-stream barrier unchanged");
        JsonElement metrics=JsonSerializer.SerializeToElement(observed.Diagnostics());
        string encoded=metrics.GetRawText();
        Check(!encoded.Contains("private-host")&&!encoded.Contains("private-client")&&!encoded.Contains(Convert.ToBase64String(payload)),"no identity or payload in observations");
        Check(metrics.GetProperty("connections")[0].GetProperty("acceptedDataFrames").GetInt64()==61,"duplicate replay not counted as new acceptance");
        Check(metrics.GetProperty("connections")[0].GetProperty("retainedDataSamples").GetInt32()==0,"native ACK/HELLO reclaim metadata");
        var pong=metrics.GetProperty("connections")[2].GetProperty("phases").GetProperty("PONG");
        Check(pong.GetProperty("SendAsyncAwait").GetProperty("count").GetInt64()==60,"PONG separated from DATA sends");
        Check(metrics.GetProperty("transitions").GetProperty("events").EnumerateArray().Any(e=>e.GetProperty("cause").GetString()=="NeedSync13"),"actual state trigger retained with time and role");
        for(int i=0;i<150;i++){observed.Detach(false,ob);ob=observed.Attach(false,"discarded-private-identity");}
        metrics=JsonSerializer.SerializeToElement(observed.Diagnostics());
        Check(metrics.GetProperty("connections").GetArrayLength()==8,"reconnect observer retention bounded");
        Check(metrics.GetProperty("connections").EnumerateArray().Any(c=>c.GetProperty("role").GetString()=="host"&&c.GetProperty("acceptedDataFrames").GetInt64()==61),"active host retained across guest reconnects");
        Check(metrics.GetProperty("discardedObservations").GetInt64()==145,"discarded reconnect history reported");
        Check(metrics.GetProperty("transitions").GetProperty("events").GetArrayLength()==128,"transition history bounded");
        Check(metrics.GetProperty("transitions").GetProperty("discardedEvents").GetInt64()>0,"transition truncation reported");
        var invalid=new StationStreamFrame(255,0,0,[]);observed.Receive(false,ob,new(1,0,61*payload.Length,[]));
        try{observed.Receive(false,ob,invalid);throw new InvalidOperationException("invalid frame accepted");}
        catch(OnlineFailure error){Check(error.Code=="STATION_RECOVERY_FRAME_INVALID","diagnostics do not replace protocol errors");}
        return checks;
    }

    // Local sequential stream operations, not capacity qualification or Android frame timing.
    public static object Benchmark()
    {
        static double Trial(bool observe,int iterations){var stream=new StationStreamSession(32768,observe);var host=stream.Attach(true,"synthetic");var guest=stream.Attach(false,"synthetic");
            stream.Receive(true,host,new(1,0,0,[]));stream.Receive(false,guest,new(1,0,0,[]));
            while(stream.Next(true,host).Frame is not null){}while(stream.Next(false,guest).Frame is not null){}
            byte[] bytes=new byte[64];long at=Stopwatch.GetTimestamp();
            for(int i=0;i<iterations;i++){stream.Receive(true,host,new(3,i*64L,0,bytes));
                while(stream.Next(false,guest).Frame is {} frame){long sending=guest.Observation?.Sending(frame)??0;guest.Observation?.Sent(frame,sending,true);}
                stream.Receive(false,guest,new(4,(i+1)*64L,0,[]));while(stream.Next(true,host).Frame is not null){}}
            return Stopwatch.GetElapsedTime(at).TotalMilliseconds*1000/iterations;}
        Trial(false,3000);Trial(true,3000);double[] off=new double[5],on=new double[5];
        for(int i=0;i<5;i++){if(i%2==0){off[i]=Trial(false,20000);on[i]=Trial(true,20000);}else{on[i]=Trial(true,20000);off[i]=Trial(false,20000);}}
        return new {scope="Sequential loopback-independent stream operations; not throughput SLA or phone/game latency",iterationsPerTrial=20000,
            withoutDiagnosticsUs=off,withDiagnosticsUs=on,medianWithoutUs=off.Order().ElementAt(2),medianWithUs=on.Order().ElementAt(2)};
    }
}
