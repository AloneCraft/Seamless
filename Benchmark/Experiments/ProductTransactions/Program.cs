using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;

internal sealed partial class Program{
    private static async Task Main(string[] args){
        if(args.Length==6&&args[0]=="--client-result-socket-comparison"){
            await CompareClientResultSocket(args[1],args[2],args[3],int.Parse(args[4]),args[5]);
            return;
        }
        if(args.Length==4&&args[0]=="--client-result-socket"){
            await VerifyClientResultSocket(args[1],args[2],args[3]);
            return;
        }
        if(args.Length==6&&args[0]=="--client-results"){
            await CompareClientResults(args[1],args[2],args[3],int.Parse(args[4]),args[5]);
            return;
        }
        if(args.Length==7&&args[0]=="--collection-carrier"){
            await CompareCollectionCarrier(args[1],args[2],args[3],int.Parse(args[4]),args[5],args[6]);
            return;
        }
        if(args.Length==10&&args[0]=="--carrier-comparison"){
            await CompareOrdered(args[1],args[2],args[3],int.Parse(args[4]),Enum.Parse<DatabaseStorageMode>(args[5]),int.Parse(args[6]),args[7],int.Parse(args[8]),args[9]);
            return;
        }
        if(args.Length==9&&args[0]=="--ordered-comparison"){await CompareOrdered(args[1],args[2],args[3],int.Parse(args[4]),Enum.Parse<DatabaseStorageMode>(args[5]),int.Parse(args[6]),args[7],int.Parse(args[8]));return;}
        if(args.Length==8&&args[0]=="--comparison"){await CompareSources(args[1],args[2],args[3],int.Parse(args[4]),Enum.Parse<DatabaseStorageMode>(args[5]),int.Parse(args[6]),args[7]);return;}
        if(args.Length==3&&args[0] is "--crash-write" or "--crash-read"){await AbruptRecovery(args[0]=="--crash-write",args[1],args[2]);return;}
        if(args.Length is 5 or 6&&args[0]=="--endurance"){
            var churn=args.Length==6&&args[5]=="Churn";if(args.Length==6&&!churn&&args[5]!="Update"){throw new ArgumentException("Endurance workload must be Update or Churn.");}
            await Endurance(args[1],args[2],args[3],int.Parse(args[4],global::System.Globalization.CultureInfo.InvariantCulture),churn);return;
        }
        if(args.Length is<4 or>6){throw new ArgumentException("Arguments: F-data-root F-report.json row-count source-manifest-sha256 [Memory|Storage] [operations-per-worker]");}
        var root=Path.GetFullPath(args[0]);var report=Path.GetFullPath(args[1]);var count=int.Parse(args[2],global::System.Globalization.CultureInfo.InvariantCulture);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||count is<64 or>100000){throw new ArgumentException("Use F data, F reports and 64..100000 rows.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var storage=DatabaseStorageMode.Memory;if(args.Length>=5){storage=Enum.Parse<DatabaseStorageMode>(args[4]);}if(!Enum.IsDefined(storage)){throw new ArgumentException("Unknown storage mode.");}
        var operations=128;if(args.Length>=6){operations=int.Parse(args[5],global::System.Globalization.CultureInfo.InvariantCulture);}if(operations is<8 or>4096){throw new ArgumentOutOfRangeException(nameof(operations));}
        var records=new List<object>();
        foreach(var mode in new[]{TransactionConcurrencyMode.Exclusive,TransactionConcurrencyMode.Optimistic}){
            foreach(var workers in new[]{1,4}){
                var options=new DatabaseContainerOptions{TransactionConcurrency=mode,MaximumQueryWorkers=workers,
                    StorageMode=storage,StorageDirectory=Path.Combine(root,mode+"-"+workers,"database"),IndexSize=1024,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<25,ManagedMemoryLimitBytes=192L<<20,TemporaryStorageLimitBytes=128L<<20,TransactionMemoryLimit=2L<<20,TemporaryDirectory=Path.Combine(root,mode+"-"+workers,"temporary"),
                    TransactionHistory=new(){MaxRetainedMemoryBytes=32L<<20,MaxRetainedStorageBytes=32L<<20,MaxMetadataBytes=64L<<20,MaintenanceReserveMemoryBytes=2L<<20,MaintenanceReserveStorageBytes=2L<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(15)}};
                if(storage==DatabaseStorageMode.Memory){options=options with{IndexSize=16384,PageSize=65536,SegmentSize=1<<26,TransactionHistory=options.TransactionHistory! with{MaxRetainedStorageBytes=0,MaintenanceReserveStorageBytes=0}};}
                using var db=new Database(options);
                for(var start=0;start<count;start+=4096){await using var seed=await db.BeginTransactionAsync();for(var id=start;id<Math.Min(count,start+4096);id++){seed.Accounts.Add(new(id,100));}await seed.CommitAsync();}
                await db.CheckpointTransactionsAsync();
                long expectedBalance=(long)count*100;
                for(var repeat=0;repeat<3;repeat++){
                    foreach(var native in new[]{false,true}){
                        await using var tx=await db.BeginReadOnlyTransactionAsync();var before=GC.GetTotalAllocatedBytes(false);var watch=Stopwatch.StartNew();long value;
                        if(native){value=Queries.Total(tx);}else{value=tx.Accounts.Where(static row=>row.Id%2==0).Sum(static row=>row.Balance);}
                        watch.Stop();if(value!=(long)((count+1)/2)*100){throw new InvalidDataException("Sum mismatch.");}await tx.CommitAsync();
                        records.Add(new{Kind="scan",Mode=mode.ToString(),Workers=workers,Repeat=repeat,Native=native,Milliseconds=watch.Elapsed.TotalMilliseconds,Allocated=GC.GetTotalAllocatedBytes(false)-before});
                    }
                }
                await using(var tx=await db.BeginReadOnlyTransactionAsync()){
                    var watch=Stopwatch.StartNew();if(Queries.Count(tx)!=count){throw new InvalidDataException("Parallel count mismatch.");}watch.Stop();records.Add(new{Kind="count",Mode=mode.ToString(),Workers=workers,Milliseconds=watch.Elapsed.TotalMilliseconds});
                    watch.Restart();var groups=Queries.Groups(tx);watch.Stop();if(groups.Sum(static group=>group.Count)!=count||groups.Sum(static group=>group.Total)!=(long)count*100){throw new InvalidDataException("Group mismatch.");}records.Add(new{Kind="group",Mode=mode.ToString(),Workers=workers,Milliseconds=watch.Elapsed.TotalMilliseconds,Groups=groups.Length});
                    watch.Restart();var joined=Queries.Join(tx);watch.Stop();if(joined.Length!=count||joined.Any(static pair=>pair.Left!=pair.Right%64)){throw new InvalidDataException("Join mismatch.");}records.Add(new{Kind="join",Mode=mode.ToString(),Workers=workers,Milliseconds=watch.Elapsed.TotalMilliseconds,Rows=joined.Length});await tx.CommitAsync();
                }
                foreach(var concurrency in new[]{1,4}){
                    foreach(var readPercent in new[]{95,50}){
                        foreach(var hot in new[]{false,true}){
                            for(var repeat=0;repeat<3;repeat++){
                                var before=GC.GetTotalAllocatedBytes(false);var watch=Stopwatch.StartNew();var committed=0;var updates=0;var conflicts=0;var latency=new List<double>[concurrency];
                                var tasks=Enumerable.Range(0,concurrency).Select(worker=>Task.Run(async()=>{
                                    var times=latency[worker]=new();var random=new Random(173+worker);
                                    for(var operation=0;operation<operations;operation++){
                                        var start=Stopwatch.GetTimestamp();
                                        try{
                                            await using var tx=await db.BeginTransactionAsync();var range=count;if(hot){range=Math.Max(1,count/100);}var key=random.Next(range);var row=tx.Accounts[key];var write=random.Next(100)>=readPercent;
                                            if(write){tx.Accounts[key]=row with{Balance=row.Balance+1};}await tx.CommitAsync();Interlocked.Increment(ref committed);if(write){Interlocked.Increment(ref updates);}times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                                        }catch(TransactionAbortedException error)when(error.Reason==TransactionAbortReason.Conflict){Interlocked.Increment(ref conflicts);}
                                    }
                                })).ToArray();
                                await Task.WhenAll(tasks);watch.Stop();var allocated=GC.GetTotalAllocatedBytes(false)-before;var samples=latency.SelectMany(static times=>times).Order().ToArray();expectedBalance=checked(expectedBalance+updates);
                                await using(var verify=await db.BeginReadOnlyTransactionAsync()){if(verify.Accounts.Sum(static row=>row.Balance)!=expectedBalance){throw new InvalidDataException("Successful updates and committed values disagree.");}await verify.CommitAsync();}
                                double Percentile(double fraction){if(samples.Length==0){return 0;}return samples[Math.Min(samples.Length-1,(int)Math.Ceiling(samples.Length*fraction)-1)];}
                                records.Add(new{Kind="mixed",Mode=mode.ToString(),Workers=workers,Concurrency=concurrency,ReadPercent=readPercent,Hot=hot,Repeat=repeat,Committed=committed,Updates=updates,Conflicts=conflicts,Seconds=watch.Elapsed.TotalSeconds,Throughput=committed/watch.Elapsed.TotalSeconds,P50=Percentile(.5),P95=Percentile(.95),P99=Percentile(.99),Allocated=allocated});
                                await db.CheckpointTransactionsAsync();
                            }
                        }
                    }
                }
                records.Add(new{Kind="resources",Mode=mode.ToString(),Workers=workers,Usage=db.ResourceUsage,Cpu=CpuScheduler.Shared.Usage,WorkingSet=Process.GetCurrentProcess().PeakWorkingSet64});
                if(storage==DatabaseStorageMode.Storage){
                    db.Dispose();var recovery=Stopwatch.StartNew();using var reopened=new Database(options);
                    await using var verify=await reopened.BeginReadOnlyTransactionAsync();
                    if(verify.Accounts.Sum(static row=>row.Balance)!=expectedBalance||verify.Accounts.Count()!=count){throw new InvalidDataException("Reopened database disagrees with committed work.");}
                    await verify.CommitAsync();recovery.Stop();records.Add(new{Kind="reopen",Mode=mode.ToString(),Workers=workers,Milliseconds=recovery.Elapsed.TotalMilliseconds});
                }
                Console.WriteLine($"{storage}, {mode}, workers={workers}: {count} rows completed");
            }
        }
        var assembly=typeof(Database).Assembly;
        var result=new{Format="product-mvcc-probe-v2",Utc=DateTimeOffset.UtcNow,Rows=count,StorageMode=storage.ToString(),OperationsPerWorker=operations,SourceManifestSha256=args[3],AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),Records=records};
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static async Task AbruptRecovery(bool write,string root,string evidence){
        if(Path.GetPathRoot(Path.GetFullPath(root))!="F:\\"||Path.GetPathRoot(Path.GetFullPath(evidence))!="F:\\"){throw new ArgumentException("Use F data and F evidence.");}
        var options=new DatabaseContainerOptions{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
            StorageMode=DatabaseStorageMode.Storage,StorageDirectory=root,TemporaryDirectory=Path.Combine(root,"temporary"),IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,
            ManagedMemoryLimitBytes=16L<<20,TemporaryStorageLimitBytes=8L<<20,TransactionMemoryLimit=65536,
            TransactionHistory=new(){MaxRetainedMemoryBytes=1L<<20,MaxRetainedStorageBytes=1L<<20,MaxMetadataBytes=1L<<20,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=65536,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
        using var db=new Database(options);
        if(write){
            await using var tx=await db.BeginTransactionAsync();for(var id=0;id<64;id++){tx.Accounts.Add(new(id,100));}var outcome=await tx.CommitAsync();
            await File.WriteAllTextAsync(evidence,JsonSerializer.Serialize(new{Phase="commit-returned",outcome.Id,outcome.Lsn}));
            // finally/Dispose/checkpointを通さずプロセスを終了。WALからの復元を次の独立プロセスで確認する。
            Environment.Exit(73);
        }else{
            await using var tx=await db.BeginReadOnlyTransactionAsync();var rows=tx.Accounts.OrderBy(static row=>row.Id).ToArray();
            if(rows.Length!=64||rows.Where((row,index)=>row.Id!=index||row.Balance!=100).Any()){throw new InvalidDataException("Abrupt process recovery lost committed rows.");}
            await tx.CommitAsync();await File.WriteAllTextAsync(evidence,JsonSerializer.Serialize(new{Phase="recovered",Rows=rows.Length,Total=rows.Sum(static row=>row.Balance)}));
        }
    }
}
