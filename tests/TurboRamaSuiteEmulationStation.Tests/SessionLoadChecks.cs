using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TurboRamaSuiteOnlineServer;

internal static class SessionLoadChecks
{
    public static async Task RunAsync(string connection)
    {
        var builder=new NpgsqlConnectionStringBuilder(connection);
        if(builder.Database is null||!(builder.Database.Contains("integration",StringComparison.Ordinal)||builder.Database.EndsWith("_ci",StringComparison.Ordinal)))
            throw new InvalidOperationException("Load checks require a disposable integration/CI database.");
        await using var db=NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connection){MaxPoolSize=4}.ConnectionString);
        using var online=RSA.Create(3072);using var signer=new RsaAssertionSigner(online);
        var reports=new List<object>();
        foreach(var count in Environment.GetEnvironmentVariable("SUITE_ES_LOAD_COUNTS")?.Split(',').Select(int.Parse).ToArray()??[500,1000])
        {
            Console.WriteLine($"LOAD preparing {count} signed sessions ({count/2} synthetic computers, one NAT).");
            var people=new ConcurrentBag<SyntheticClient>();
            await Parallel.ForEachAsync(Enumerable.Range(0,count/2),new ParallelOptions{MaxDegreeOfParallelism=4},async(_,_)=>
            {var person=new SyntheticClient();await person.Seed(db);people.Add(person);});
            var sessions=people.SelectMany(p=>new[]{new Target(p,SyntheticClient.Hex(),"SUITE"),new Target(p,SyntheticClient.Hex(),"SHARED")}).ToArray();
            await using var app=SharedIntegrationChecks.CreateApp(connection,signer,true);await app.StartAsync();
            using var meter=new Meter();using var http=new HttpClient(meter){BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(30)};
            meter.Phase="opening";
            await Parallel.ForEachAsync(sessions,new ParallelOptions{MaxDegreeOfParallelism=32},async(t,_)=>await Exchange(t,false));
            Console.WriteLine("LOAD opening result "+JsonSerializer.Serialize(new{sessions=count,exchangeErrors=meter.Errors,denialCodes=meter.Codes}));
            SharedIntegrationChecks.Check(meter.Errors==0,"Every measured session must open successfully before the capacity phase.");
            Console.WriteLine($"LOAD {count}: all sessions opened; 60s steady heartbeat at five seconds.");
            await Phase("steady",60,true);
            await Phase("burst",0,false);
            Console.WriteLine($"LOAD {count}: simulated 20s network interruption, then reconnect the still-authorized instances.");
            await Task.Delay(TimeSpan.FromSeconds(20));
            await Phase("reconnect",0,false);
            if(count==1000)
            {
                Console.WriteLine("LOAD 1000: 180s soak with five-second heartbeat.");
                await Phase("soak",180,true);
            }
            SharedIntegrationChecks.Check(meter.Errors==0,"Capacity run must complete without failed exchanges.");
            await app.StopAsync();foreach(var person in people)person.Dispose();

            async Task Exchange(Target target,bool heartbeat)
            {
                try{await SharedIntegrationChecks.Exchange(http,online,target.Person,target.Session,target.Scope,heartbeat);}
                catch(Exception exception)
                {
                    var n=Interlocked.Increment(ref meter.Errors);
                    if(n<=5)Console.WriteLine("LOAD FAILURE "+target.Scope+" "+exception.GetType().Name+" "+exception.Message);
                }
            }
            async Task Phase(string name,int seconds,bool paced)
            {
                meter.Reset(name);using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
                var clock=Stopwatch.StartNew();long peakWorking=0,peakPrivate=0;int maxDbActive=0,maxDbLockWait=0;
                using var stop=new CancellationTokenSource();
                var sample=Task.Run(async()=>
                {
                    while(!stop.IsCancellationRequested)
                    {
                        process.Refresh();peakWorking=Math.Max(peakWorking,process.WorkingSet64);peakPrivate=Math.Max(peakPrivate,process.PrivateMemorySize64);
                        await using var command=db.CreateCommand("SELECT count(*) FILTER(WHERE state='active'),count(*) FILTER(WHERE wait_event_type='Lock') FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid()");
                        await using var row=await command.ExecuteReaderAsync();await row.ReadAsync();maxDbActive=Math.Max(maxDbActive,(int)row.GetInt64(0));maxDbLockWait=Math.Max(maxDbLockWait,(int)row.GetInt64(1));
                        try{await Task.Delay(1000,stop.Token);}catch(OperationCanceledException){break;}
                    }
                });
                if(paced)
                    await Task.WhenAll(sessions.Select(async(t,index)=>
                    {
                        await Task.Delay(index*5000/sessions.Length);
                        for(var n=0;n<seconds/5;n++)
                        {
                            var due=Stopwatch.StartNew();await Exchange(t,true);
                            var delay=TimeSpan.FromSeconds(5)-due.Elapsed;if(n+1<seconds/5&&delay>TimeSpan.Zero)await Task.Delay(delay);
                        }
                    }));
                else await Task.WhenAll(sessions.Select(t=>Exchange(t,true)));
                stop.Cancel();await sample;clock.Stop();process.Refresh();
                var times=meter.Latencies.Order().ToArray();
                var report=new {sessions=count,computers=count/2,phase=name,seconds=Math.Round(clock.Elapsed.TotalSeconds,3),requests=times.Length,
                    requestsPerSecond=Math.Round(times.Length/clock.Elapsed.TotalSeconds,2),p50Ms=P(0.5),p95Ms=P(0.95),p99Ms=P(0.99),maxMs=P(1),
                    httpStatus=meter.Status.OrderBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Value),denialCodes=meter.Codes,exchangeErrors=meter.Errors,
                    combinedApiAndGeneratorCpuSeconds=Math.Round((process.TotalProcessorTime-cpu).TotalSeconds,3),peakWorkingMiB=peakWorking/1048576,peakPrivateMiB=peakPrivate/1048576,
                    maxDatabaseActive=maxDbActive,maxDatabaseLockWaiters=maxDbLockWait,trackedRateLimitWindows=app.Services.GetRequiredService<SuiteRateLimiter>().TrackedWindowCount};
                reports.Add(report);Console.WriteLine(JsonSerializer.Serialize(report));
                double P(double percentile)=>times.Length==0?0:Math.Round(times[Math.Min(times.Length-1,(int)Math.Ceiling(times.Length*percentile)-1)],3);
            }
        }
        var output=Environment.GetEnvironmentVariable("SUITE_ES_LOAD_REPORT")??"outputs/es-session-load.json";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new {runtime=Environment.Version.ToString(),logicalProcessors=Environment.ProcessorCount,
            apiMaximumDatabaseConnections=new NpgsqlConnectionStringBuilder(SuiteDatabasePoolPolicy.ApplyDefaults(connection)).MaxPoolSize,
            machineRsaBits=2048,onlineRsaBits=3072,heartbeatSeconds=5,transport="Kestrel HTTP loopback; real PostgreSQL; one NAT; no public proxy/TLS",resourceScope="API and synthetic generator share this process; database measured separately",reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("LOAD REPORT: "+output);
    }
    private sealed record Target(SyntheticClient Person,string Session,string Scope);
    private sealed class Meter:DelegatingHandler
    {
        public string Phase="";public int Errors;
        public ConcurrentBag<double> Latencies=new();public ConcurrentDictionary<int,int> Status=new();
        public ConcurrentDictionary<string,int> Codes=new();
        public Meter():base(new SocketsHttpHandler{MaxConnectionsPerServer=2048}){}
        public void Reset(string phase){Phase=phase;Latencies=new();Status=new();Codes=new();}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var clock=Stopwatch.StartNew();
            try
            {
                var response=await base.SendAsync(request,ct);Status.AddOrUpdate((int)response.StatusCode,1,(_,n)=>n+1);
                if(!response.IsSuccessStatusCode)
                {
                    var content=await response.Content.ReadAsByteArrayAsync(ct);
                    try{using var error=JsonDocument.Parse(content);var code=error.RootElement.GetProperty("code").GetString()??"unknown";Codes.AddOrUpdate(request.RequestUri!.AbsolutePath+":"+code,1,(_,n)=>n+1);}catch(JsonException){}
                }
                return response;
            }
            finally{Latencies.Add(clock.Elapsed.TotalMilliseconds);}
        }
    }
}
