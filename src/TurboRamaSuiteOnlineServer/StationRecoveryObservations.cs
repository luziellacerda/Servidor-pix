using System.Diagnostics;

namespace TurboRamaSuiteOnlineServer;

// In-memory, bounded metadata only. Never retains payload, credentials or room/device identifiers.
// Data residence is sampled once per 16 newly accepted frames; other phase timers are unsampled.
public sealed class StationRecoveryConnectionObservation(bool host)
{
    public enum Phase { ReceiveToLock, ReceiveLockHeld, SelectionLockWait, SelectionLockHeld,
        ReceiveToSelectionSampled, ReceiveToSendStartSampled, SelectionToSendStart, SendAsyncAwait }
    private static readonly double[] LimitsMs = [.01,.025,.05,.1,.25,.5,1,2.5,5,10,25,50,100,250,500,1000,2500,5000,double.PositiveInfinity];
    private sealed class Histogram
    {
        private readonly long[] buckets=new long[LimitsMs.Length];
        private long sumTicks,maximumTicks;
        public void Add(long ticks)
        {
            ticks=Math.Max(0,ticks);double ms=ticks*1000.0/Stopwatch.Frequency;int bucket=0;
            while(ms>LimitsMs[bucket])bucket++;
            long current=Volatile.Read(ref maximumTicks);
            while(ticks>current){long prior=Interlocked.CompareExchange(ref maximumTicks,ticks,current);if(prior==current)break;current=prior;}
            Interlocked.Add(ref sumTicks,ticks);Interlocked.Increment(ref buckets[bucket]);
        }
        public object Snapshot()
        {
            var bins=new long[buckets.Length];for(int i=0;i<bins.Length;i++)bins[i]=Volatile.Read(ref buckets[i]);
            long n=bins.Sum(),max=Volatile.Read(ref maximumTicks);
            double? Quantile(double q){if(n==0)return null;long target=(long)Math.Ceiling(n*q),seen=0;
                for(int i=0;i<bins.Length;i++){seen+=bins[i];if(seen>=target)return double.IsPositiveInfinity(LimitsMs[i])?max*1000.0/Stopwatch.Frequency:Math.Min(LimitsMs[i],max*1000.0/Stopwatch.Frequency);}return null;}
            return new {count=n,p50UpperBoundMs=Quantile(.5),p95UpperBoundMs=Quantile(.95),p99UpperBoundMs=Quantile(.99),
                maximumMs=max*1000.0/Stopwatch.Frequency,meanMs=n==0?(double?)null:Volatile.Read(ref sumTicks)*1000.0/Stopwatch.Frequency/n};
        }
    }
    private readonly Histogram[,] histograms=CreateHistograms();
    private static Histogram[,] CreateHistograms(){var result=new Histogram[4,8];for(int g=0;g<4;g++)for(int p=0;p<8;p++)result[g,p]=new();return result;}
    private readonly record struct Sample(long Begin,long End,long ReceivedAt);
    private readonly Sample[] samples=new Sample[256];
    private int head,sampleCount;
    private long acceptedFrameCount,discardedSamples,matchedSelections;
    private long selectedAt,selectedReceivedAt,pingReceivedAt,inFlight,completedSends,failedSends;
    private long controlsBeforeData,maximumControlsBeforeData;private readonly Histogram publishStateTiming=new();
    public string Reference {get;}=Guid.NewGuid().ToString("N")[..12];
    public bool Host {get;}=host;
    public static long Stamp()=>Stopwatch.GetTimestamp();
    private static int Group(byte type,int size)=>type is StationStreamFrame.Ping or StationStreamFrame.Pong?3:
        type==StationStreamFrame.DataPacket?(size<=1024?0:size<=4096?1:2):-1;
    public void Measure(byte type,int bytes,Phase phase,long begin,long end){int group=Group(type,bytes);if(group>=0)histograms[group,(int)phase].Add(end-begin);}
    // Called only while the stream lock is held. Replay is never sampled as new acceptance.
    public void Accepted(StationStreamFrame frame,long receivedAt)
    {
        acceptedFrameCount++;
        if((acceptedFrameCount-1)%16!=0)return;
        if(sampleCount==samples.Length){head=(head+1)%samples.Length;sampleCount--;discardedSamples++;}
        samples[(head+sampleCount)%samples.Length]=new(frame.Offset,frame.Offset+frame.Data.Length,receivedAt);sampleCount++;
    }
    public void Confirmed(long offset){while(sampleCount>0&&samples[head].End<=offset){head=(head+1)%samples.Length;sampleCount--;}}
    public long OldestSampleAt(long begin,long end)
    {
        for(int i=0;i<sampleCount;i++){var sample=samples[(head+i)%samples.Length];if(sample.Begin>=end)break;if(sample.End>begin)return sample.ReceivedAt;}
        return 0;
    }
    public void PingReceived(long at)=>pingReceivedAt=at;
    public void SelectedControl(byte type,bool dataAvailable)
    {if(type==StationStreamFrame.DataPacket||!dataAvailable)controlsBeforeData=0;
        else{controlsBeforeData++;maximumControlsBeforeData=Math.Max(maximumControlsBeforeData,controlsBeforeData);}}
    public void PublishedState(long begin)=>publishStateTiming.Add(Stamp()-begin);
    public void Selected(StationStreamFrame frame,long lockedAt,long waitingAt,StationRecoveryConnectionObservation? source)
    {
        long at=Stamp();selectedAt=at;
        Measure(frame.Type,frame.Data.Length,Phase.SelectionLockWait,waitingAt,lockedAt);
        Measure(frame.Type,frame.Data.Length,Phase.SelectionLockHeld,lockedAt,at);
        long received=frame.Type==StationStreamFrame.Pong?pingReceivedAt:frame.Type==StationStreamFrame.DataPacket?
            source?.OldestSampleAt(frame.Offset,frame.Offset+frame.Data.Length)??0:0;
        selectedReceivedAt=received;
        if(received!=0){Measure(frame.Type,frame.Data.Length,Phase.ReceiveToSelectionSampled,received,at);matchedSelections++;}
    }
    public long Sending(StationStreamFrame frame){long at=Stamp();Interlocked.Exchange(ref inFlight,1);
        Measure(frame.Type,frame.Data.Length,Phase.SelectionToSendStart,selectedAt,at);
        if(selectedReceivedAt!=0)Measure(frame.Type,frame.Data.Length,Phase.ReceiveToSendStartSampled,selectedReceivedAt,at);return at;}
    public void Sent(StationStreamFrame frame,long at,bool succeeded){Measure(frame.Type,frame.Data.Length,Phase.SendAsyncAwait,at,Stamp());Interlocked.Exchange(ref inFlight,0);if(succeeded)Interlocked.Increment(ref completedSends);else Interlocked.Increment(ref failedSends);}
    public object Snapshot(long delivered,long accepted)
    {
        string[] names=["DATA_1_1024","DATA_1025_4096","DATA_4097_16384","PONG"];
        var result=new Dictionary<string,object>();for(int g=0;g<4;g++){var phases=new Dictionary<string,object>();for(int p=0;p<8;p++)phases[((Phase)p).ToString()]=histograms[g,p].Snapshot();result[names[g]]=phases;}
        long oldest=OldestSampleAt(delivered,accepted);
        return new {reference=Reference,role=Host?"host":"client",phases=result,dataSamplingStride=16,dataSampleCapacity=samples.Length,
            acceptedDataFrames=acceptedFrameCount,retainedDataSamples=sampleCount,discardedDataSamples=discardedSamples,matchedSelections,
            oldestSampledPendingByteAgeMs=oldest==0?(double?)null:(Stamp()-oldest)*1000.0/Stopwatch.Frequency,
            consecutiveControlsWhileDataQueued=controlsBeforeData,maximumConsecutiveControlsWhileDataQueued=maximumControlsBeforeData,
            publishStateCall=publishStateTiming.Snapshot(),
            writerInFlightFrames=Volatile.Read(ref inFlight),completedSendAttempts=Volatile.Read(ref completedSends),failedSendAttempts=Volatile.Read(ref failedSends)};
    }
}

public sealed class StationRecoveryTransitions
{
    private readonly object gate=new();
    private readonly Queue<object> events=new();
    private readonly long[] receivedTypes=new long[14];
    private long discardedEvents;
    public void Receive(byte type){if(type<receivedTypes.Length)lock(gate)receivedTypes[type]++;}
    public void Add(string cause,bool? host,long beforeEpoch,long afterEpoch,long beforeState,long afterState)
    {
        lock(gate){if(events.Count==128){events.Dequeue();discardedEvents++;}events.Enqueue(new {utc=DateTimeOffset.UtcNow,
            monotonicTicks=Stopwatch.GetTimestamp(),cause,role=host.HasValue?(host.Value?"host":"client"):"operator",beforeEpoch,afterEpoch,beforeState,afterState});}
    }
    public object Snapshot(){lock(gate)return new {capacity=128,discardedEvents,receivedTypeCounts=receivedTypes.ToArray(),events=events.ToArray()};}
}
