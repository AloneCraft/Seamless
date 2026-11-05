using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace TransactionModes;

public static class Benchmarks{
    public static void Run(string root,string report){
        Directory.CreateDirectory(root);var results=new List<object>();var finished=new HashSet<string>();
        if(File.Exists(report)){foreach(var line in File.ReadLines(report)){using var document=JsonDocument.Parse(line);if(document.RootElement.GetProperty("outcome").GetString()=="Passed"&&document.RootElement.TryGetProperty("name",out var name)){finished.Add(name.GetString()!);}}}
        foreach(var disk in new[]{false,true}){
            foreach(var size in new[]{10_000,100_000}){
                foreach(var concurrency in new[]{1,2,4,8}){
                    foreach(var readPercent in new[]{100,95,50}){
                        foreach(var hot in new[]{false,true}){
                            for(var repeat=0;repeat<5;repeat++){
                                foreach(var mode in Order(repeat)){
                                    var name=$"{disk}-{size}-{concurrency}-{readPercent}-{hot}-{repeat}-{mode}";
                                    if(finished.Contains(name)){continue;}
                                    var cellPath=Path.Combine(root,name+"-"+Guid.NewGuid().ToString("N"));var passed=false;
                                    var watch=Stopwatch.StartNew();
                                    try{
                                        using var engine=new Engine(mode,cellPath,disk,size);
                                        Warmup(engine,size);
                                        if(disk){engine.Evict();}
                                        var sample=Measure(engine,size,concurrency,readPercent,hot,repeat);
                                        var outcome="Passed";if(sample.Errors>0){outcome="Failed";}else{passed=true;}
                                        var row=new{name,kind="mixed",mode,disk,size,concurrency,readPercent,hot,repeat,outcome,sample};results.Add(row);Append(report,row);
                                        Console.WriteLine($"BENCH {name} {sample.SuccessPerSecond:F1}/s p95={sample.P95Ms:F3}ms");
                                    }catch(Exception error){var row=new{name,kind="mixed",mode,disk,size,concurrency,readPercent,hot,repeat,outcome="Failed",error=error.ToString()};results.Add(row);Append(report,row);Console.WriteLine($"BENCH FAIL {name}: {error.Message}");}
                                    if(passed){Cleanup(root,cellPath);}
                                }
                            }
                        }
                    }
                }
                foreach(var mode in Enum.GetValues<Mode>()){
                    using var engine=new Engine(mode,Path.Combine(root,$"query-{disk}-{size}-{mode}"),disk,size);
                    using var tx=engine.Begin();_ =Queries.Sum(tx);_ =Queries.SumLinq(tx);
                    for(var repeat=0;repeat<5;repeat++){
                        var variants=new[]{true,false};if(repeat%2!=0){variants=[false,true];}
                        foreach(var flinq in variants){
                            var name=$"scan-{disk}-{size}-{mode}-{repeat}-{flinq}";if(finished.Contains(name)){continue;}
                            if(disk){engine.Evict();}
                            var clock=Stopwatch.StartNew();long checksum=0;
                            for(var i=0;i<8;i++){if(flinq){checksum+=Queries.Sum(tx);}else{checksum+=Queries.SumLinq(tx);}}
                            clock.Stop();Acceptance.Check(checksum==size*50L*8,"Scan checksum");var row=new{name,kind="scan",mode,disk,size,repeat,flinq,elapsedMs=clock.Elapsed.TotalMilliseconds,checksum,outcome="Passed"};results.Add(row);Append(report,row);
                        }
                    }
                }
            }
        }
        Special(root,report,finished);
        var allResults=File.ReadLines(report).Select(static line=>JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
        File.WriteAllText(report+".summary.json",JsonSerializer.Serialize(allResults,new JsonSerializerOptions{WriteIndented=true}));
        Acceptance.Check(allResults.All(static row=>row.GetProperty("outcome").GetString()=="Passed"),"One or more benchmark cells failed; exclude them from adoption decisions");
    }
    private static void Cleanup(string root,string path){
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var target=Path.GetFullPath(path);if(!target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException("Cleanup target outside trial root");}Directory.Delete(target,true);
    }
    private static void Special(string root,string report,HashSet<string> finished){
        foreach(var disk in new[]{false,true}){foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){
            var name=$"history-{disk}-{mode}";if(Enumerable.Range(0,5).All(repeat=>finished.Contains(name+"-"+repeat))){continue;}
            using var engine=new Engine(mode,Path.Combine(root,name+"-"+Guid.NewGuid().ToString("N")),disk,10_000);using var old=engine.Begin();
            for(var revision=0;revision<8;revision++){for(var start=0;start<10_000;start+=250){using var tx=engine.Begin();for(var id=start;id<Math.Min(10_000,start+250);id++){tx.Set(id,101+revision);}Acceptance.Check(tx.Commit(),"History seeding conflict");}}
            for(var repeat=0;repeat<5;repeat++){if(finished.Contains(name+"-"+repeat)){continue;}var clock=Stopwatch.StartNew();var sum=Queries.Sum(old);clock.Stop();Acceptance.Check(sum==500_000,"History scan differs from snapshot");Append(report,new{name=name+"-"+repeat,kind="history",mode,disk,repeat,elapsedMs=clock.Elapsed.TotalMilliseconds,nativeBytes=engine.Tail,outcome="Passed"});}
        }}
        foreach(var mode in new[]{Mode.Deferred,Mode.Immediate}){var name="repeated-aborted-"+mode;if(finished.Contains(name)){continue;}
            using var engine=new Engine(mode,Path.Combine(root,name+"-"+Guid.NewGuid().ToString("N")),false,64);var before=engine.Tail;var clock=Stopwatch.StartNew();
            for(var iteration=0;iteration<100;iteration++){using var tx=engine.Begin();for(var update=0;update<32;update++){tx.Set(1,update);}}
            clock.Stop();using var verify=engine.Begin();Acceptance.Check(verify.Read(1).Value==100,"Aborted writes leaked");Append(report,new{name,kind="aborted",mode,elapsedMs=clock.Elapsed.TotalMilliseconds,addedNativeBytes=engine.Tail-before,outcome="Passed"});
        }
    }
    private static Mode[] Order(int repeat){if(repeat%2==0){return[Mode.Single,Mode.Deferred,Mode.Immediate];}return[Mode.Immediate,Mode.Deferred,Mode.Single];}
    private static void Append(string path,object row)=>File.AppendAllText(path,JsonSerializer.Serialize(row)+Environment.NewLine);
    private static void Warmup(Engine engine,int size){for(var i=0;i<30;i++){using var tx=engine.Begin();_ =tx.Read(i%size);tx.Commit();}}
    public readonly record struct Sample(double SuccessPerSecond,double P50Ms,double P95Ms,double P99Ms,long Success,long Conflicts,long Errors,long Allocated,long PeakWorkingSet,long NativeLogBytes,long WalBytes,double Seconds,string? FirstError);
    private static Sample Measure(Engine engine,int size,int concurrency,int readPercent,bool hot,int seed){
        var latencies=new ConcurrentBag<double>();var expected=new ConcurrentDictionary<int,long>();var failures=new ConcurrentQueue<string>();long success=0,conflicts=0,errors=0;long peak=Process.GetCurrentProcess().WorkingSet64;
        GC.Collect();GC.WaitForPendingFinalizers();
        var allocated=GC.GetTotalAllocatedBytes(true);var start=new ManualResetEventSlim();var deadline=0L;
        var threads=Enumerable.Range(0,concurrency).Select(worker=>new Thread(()=>{
            var random=new Random(seed*100+worker+1);start.Wait();
            while(Stopwatch.GetTimestamp()<Volatile.Read(ref deadline)){
                var clock=Stopwatch.GetTimestamp();
                try{
                    using var tx=engine.Begin();var range=size;if(hot){range=Math.Max(1,size/100);}var key=random.Next(range);
                    var row=tx.Read(key);Acceptance.Check(row.Id==key&&row.Deleted==0&&row.Value>=100,"Point read returned invalid row");var write=random.Next(100)>=readPercent;if(write){tx.Set(key,row.Value+1);}
                    // 点検索中心の混合負荷。全走査の時間は scan のセルで独立して測る。
                    if(tx.Commit()){Interlocked.Increment(ref success);if(write){expected.AddOrUpdate(key,1,static (_,value)=>value+1);}latencies.Add(Stopwatch.GetElapsedTime(clock).TotalMilliseconds);}else{Interlocked.Increment(ref conflicts);}
                }catch(Exception error){failures.Enqueue(error.ToString());Interlocked.Increment(ref errors);break;}
            }
        }){IsBackground=true}).ToArray();
        foreach(var thread in threads){thread.Start();}
        using var stopSampler=new CancellationTokenSource();var sampler=Task.Run(()=>{using var process=Process.GetCurrentProcess();while(!stopSampler.IsCancellationRequested){process.Refresh();Interlocked.Exchange(ref peak,Math.Max(Interlocked.Read(ref peak),process.WorkingSet64));Thread.Sleep(20);}});
        var measured=Stopwatch.StartNew();Volatile.Write(ref deadline,Stopwatch.GetTimestamp()+Stopwatch.Frequency/2);start.Set();
        foreach(var thread in threads){if(!thread.Join(TimeSpan.FromSeconds(30))){throw new TimeoutException("Benchmark worker did not stop");}}
        measured.Stop();stopSampler.Cancel();sampler.GetAwaiter().GetResult();start.Dispose();var samples=latencies.Order().ToArray();
        using(var verify=engine.Begin()){foreach(var pair in expected){Acceptance.Check(verify.Read(pair.Key).Value==100+pair.Value,"Benchmark lost committed update");}}
        double Percentile(double fraction){if(samples.Length==0){return 0;}return samples[Math.Min(samples.Length-1,(int)Math.Floor(fraction*(samples.Length-1)))];}
        failures.TryPeek(out var firstError);
        return new(success/measured.Elapsed.TotalSeconds,Percentile(.5),Percentile(.95),Percentile(.99),success,conflicts,errors,GC.GetTotalAllocatedBytes(true)-allocated,peak,engine.Tail,engine.WalBytes,measured.Elapsed.TotalSeconds,firstError);
    }
}
